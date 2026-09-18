using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: Nodilume.ScaleSmoke <benchmark.sqlite> [result.json]");
            return 2;
        }

        var databasePath = Path.GetFullPath(args[0]);
        var outputPath = args.Length == 2 ? Path.GetFullPath(args[1]) : null;
        var profile = Path.Combine(Path.GetTempPath(), "NodilumeScaleSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var result = 1;
        var opening = Stopwatch.StartNew();
        var window = new MainWindow(profile, databasePath);

        window.Loaded += async (_, _) =>
        {
            try
            {
                var metrics = await VerifyAsync(window, opening, databasePath);
                result = 0;
                var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
                if (outputPath is null)
                    Console.WriteLine(json);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    await File.WriteAllTextAsync(outputPath, json);
                    Console.WriteLine("Metrics: " + outputPath);
                }
                Console.WriteLine("PASS: GRAPH.04 300k WPF/WebView2 scale smoke.");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
            finally
            {
                window.Close();
                app.Shutdown();
            }
        };
        window.Show();
        app.Run();
        try { Directory.Delete(profile, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return result;
    }

    private static async Task<object> VerifyAsync(MainWindow window, Stopwatch opening, string databasePath)
    {
        var web = (WebView2)window.FindName("Viewer");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> Script(string source) => web.ExecuteScriptAsync(source).WaitAsync(timeout.Token);

        async Task WaitFor(string expression, int seconds = 12)
        {
            var started = Stopwatch.StartNew();
            while (await Script(expression) != "true")
            {
                if (started.Elapsed > TimeSpan.FromSeconds(seconds))
                    throw new Exception("Timed out waiting for " + expression);
                await Task.Delay(40, timeout.Token);
            }
        }

        while (web.CoreWebView2 is null)
            await Task.Delay(50, timeout.Token);
        await WaitFor("document.body?.dataset.state === 'ready'", 15);
        opening.Stop();

        var openingMs = opening.Elapsed.TotalMilliseconds;
        var viewerFirstUsefulMs = await DatasetDoubleAsync(Script, "firstUsefulMs");
        var initialBridgeTransferMs = await DatasetDoubleAsync(Script, "bridgeTransferMs");
        var initialViewerRenderMs = await DatasetDoubleAsync(Script, "viewerRenderMs");
        if (openingMs > 5_000)
            throw new Exception($"300k first useful view exceeded 5 s: {openingMs:F1} ms.");

        await WaitFor("document.body?.dataset.frameP95Ms !== undefined");
        var frameP95Ms = await DatasetDoubleAsync(Script, "frameP95Ms");
        if (frameP95Ms > 33)
            throw new Exception($"Stabilized frame p95 exceeded 33 ms: {frameP95Ms:F2} ms.");

        var selectionSamplesMs = new List<double>();
        for (var index = 0; index < 9; index++)
        {
            var selected = await Script($$"""
(() => {
  document.body.dataset.selectionLatencyMs = '';
  const labels = [...document.querySelectorAll('.node-label')];
  const label = labels[{{index}} % labels.length];
  if (!label) return false;
  label.click();
  return true;
})()
""");
            if (selected != "true") throw new Exception("No visible label available for selection timing.");
            await WaitFor("document.body.dataset.selectionLatencyMs !== ''");
            selectionSamplesMs.Add(await DatasetDoubleAsync(Script, "selectionLatencyMs"));
        }
        var selectionP95Ms = Percentile95(selectionSamplesMs);
        if (selectionP95Ms > 100)
            throw new Exception($"Selection feedback p95 exceeded 100 ms: {selectionP95Ms:F2} ms.");

        if (await Script("document.getElementById('page-next').disabled") != "false")
            throw new Exception("300k root did not expose progressive child paging.");

        async Task CyclePageAsync()
        {
            await Script("document.getElementById('page-next').click()");
            await WaitFor(
                "document.body.dataset.responseRequestId === document.body.dataset.requestId "
                + "&& document.body.dataset.state === 'ready' "
                + "&& document.body.dataset.pageAfter !== ''");
            await Script("document.getElementById('page-prev').click()");
            await WaitFor(
                "document.body.dataset.responseRequestId === document.body.dataset.requestId "
                + "&& document.body.dataset.state === 'ready' "
                + "&& document.body.dataset.pageAfter === ''");
        }

        await CyclePageAsync();
        await CyclePageAsync();

        var memorySamples = new List<long>();
        for (var index = 0; index < 6; index++)
        {
            await CyclePageAsync();
            await Task.Delay(120, timeout.Token);
            memorySamples.Add(await WorkingSetTreeAsync(web));
        }

        const long tolerance = 2L * 1024 * 1024;
        var monotonic = true;
        for (var index = 1; index < memorySamples.Count; index++)
            monotonic &= memorySamples[index] > memorySamples[index - 1] + tolerance;
        var memoryGrowth = memorySamples[^1] - memorySamples[0];
        if (monotonic && memoryGrowth > 32L * 1024 * 1024)
            throw new Exception($"Working set grew monotonically by {memoryGrowth} bytes.");

        await Task.Delay(700, timeout.Token);
        var finalFrameP95Ms = await DatasetDoubleAsync(Script, "frameP95Ms");
        var frameSamplesMs = (await DatasetStringAsync(Script, "frameSamplesMs"))
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        if (finalFrameP95Ms > 33)
            throw new Exception($"Frame p95 after repeated paging exceeded 33 ms: {finalFrameP95Ms:F2} ms.");

        var screenshotPath = Path.ChangeExtension(databasePath, ".png");
        await using (var image = File.Create(screenshotPath))
        {
            await web.CoreWebView2.CapturePreviewAsync(
                Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,
                image).WaitAsync(timeout.Token);
        }

        return new
        {
            openingToFirstUsefulMs = openingMs,
            viewerNavigationFirstUsefulMs = viewerFirstUsefulMs,
            initialBridgeTransferMs,
            initialViewerRenderMs,
            frameP95Ms,
            finalFrameP95Ms,
            frameSamplesMs,
            selectionP95Ms,
            selectionSamplesMs,
            memorySamplesBytes = memorySamples,
            memoryRangeBytes = memorySamples.Max() - memorySamples.Min(),
            monotonicMemoryGrowth = monotonic,
            screenshotPath,
            webViewRuntime = web.CoreWebView2.Environment.BrowserVersionString,
            window = new { window.ActualWidth, window.ActualHeight },
            primaryScreen = new { Width = SystemParameters.PrimaryScreenWidth, Height = SystemParameters.PrimaryScreenHeight }
        };
    }

    private static async Task<double> DatasetDoubleAsync(
        Func<string, Task<string>> script,
        string name)
    {
        var value = await DatasetStringAsync(script, name);
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string> DatasetStringAsync(
        Func<string, Task<string>> script,
        string name)
    {
        var json = await script($"document.body.dataset.{name}");
        return JsonSerializer.Deserialize<string>(json)
            ?? throw new Exception($"Missing viewer metric {name}.");
    }

    private static double Percentile95(IEnumerable<double> samples)
    {
        var ordered = samples.OrderBy(value => value).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("At least one sample is required.");
        return ordered[Math.Min(ordered.Length - 1, (int)Math.Ceiling(ordered.Length * 0.95) - 1)];
    }

    private static Task<long> WorkingSetTreeAsync(WebView2 web)
    {
        long total = Process.GetCurrentProcess().WorkingSet64;
        var current = Environment.ProcessId;
        var processInfos = web.CoreWebView2.Environment.GetProcessInfos();
        foreach (var processId in processInfos.Select(x => x.ProcessId).Distinct())
        {
            if (processId == current) continue;
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Refresh();
                total += process.WorkingSet64;
            }
            catch (ArgumentException)
            {
                // A short-lived WebView2 helper may exit between enumeration and sampling.
            }
        }
        return Task.FromResult(total);
    }
}
