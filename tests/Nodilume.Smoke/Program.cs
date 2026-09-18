using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Desktop;
using Nodilume.Infrastructure.Sqlite;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var root = Path.Combine(Path.GetTempPath(), "NodilumeSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "demo.sqlite");
        var result = 1;
        var pass = 0;
        DatabaseEvidence? firstEvidence = null;

        void StartWindow()
        {
            pass++;
            var currentPass = pass;
            var profile = Path.Combine(root, $"WebView2-{currentPass}");
            var window = new MainWindow(profile, databasePath);
            window.Loaded += async (_, _) =>
            {
                var success = false;
                try
                {
                    await VerifyWindowAsync(window, fullInteraction: currentPass == 1);
                    var evidence = await ReadEvidenceAsync(databasePath);
                    if (currentPass == 1)
                    {
                        firstEvidence = evidence;
                    }
                    else
                    {
                        if (firstEvidence is null || firstEvidence != evidence)
                            throw new Exception("Persistent map identity or values changed across real window reopen.");
                        result = 0;
                        Console.WriteLine(
                            $"PASS: WPF/WebView2 persistent reopen, 25 placements · 27 links, stable IDs/values, " +
                            $"local resources, selection, projected-node focus/home movement, resize, runtime " +
                            $"{((WebView2)window.FindName("Viewer")).CoreWebView2.Environment.BrowserVersionString}");
                    }
                    success = true;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);
                }
                finally
                {
                    window.Close();
                    if (success && currentPass == 1)
                        _ = app.Dispatcher.BeginInvoke(StartWindow);
                    else
                        app.Shutdown();
                }
            };
            window.Show();
        }

        StartWindow();
        app.Run();
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return result;
    }

    private static async Task VerifyWindowAsync(MainWindow window, bool fullInteraction)
    {
        var web = (WebView2)window.FindName("Viewer");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> Script(string source) => web.ExecuteScriptAsync(source).WaitAsync(timeout.Token);

        async Task WaitFor(string expression)
        {
            while (await Script(expression) != "true")
                await Task.Delay(50, timeout.Token);
        }

        while (web.CoreWebView2 is null)
            await Task.Delay(100, timeout.Token);
        while (await Script("document.body?.dataset.state") != "\"ready\"")
            await Task.Delay(100, timeout.Token);

        var counts = await Script("document.getElementById('counts').textContent");
        if (!counts.Contains("25 nodi") || !counts.Contains("27 connessioni"))
            throw new Exception($"Unexpected persistent scene: {counts}");
        if (await Script("document.querySelector('canvas')?.width > 0") != "true")
            throw new Exception("Canvas missing");

        var resources = await Script("performance.getEntriesByType('resource').map(x=>x.name)");
        if (JsonSerializer.Deserialize<string[]>(resources)!.Any(x => !x.StartsWith("https://nodilume.local/")))
            throw new Exception("Remote resource requested");
        if (!fullInteraction) return;

        await Script("document.querySelector('.node-label').click(); document.getElementById('focus').click()");
        const string centered = "(()=>{const r=document.querySelector('.node-label').getBoundingClientRect(); return Math.abs(r.x+r.width/2-innerWidth/2)<1 && Math.abs(r.y+r.height/2-innerHeight/2-18)<1})()";
        await WaitFor(centered);
        var selection = await Script("document.getElementById('selection-title').textContent");
        if (!selection.Contains("Idee connesse")) throw new Exception("Selection did not reach UI");

        await Script("document.getElementById('home').click()");
        await WaitFor("(()=>{const r=document.querySelector('.node-label').getBoundingClientRect(); return Math.abs(r.x+r.width/2-innerWidth/2)>30})()");
        var initialWidth = int.Parse(await Script("innerWidth"));
        window.Width = 1000;
        await WaitFor($"innerWidth < {initialWidth} && document.querySelector('canvas').clientWidth === innerWidth");
        window.Width = 1280;
        await WaitFor($"innerWidth === {initialWidth} && document.querySelector('canvas').clientWidth === innerWidth");

        Directory.CreateDirectory("artifacts");
        await using var image = File.Create("artifacts/graph-02-smoke.png");
        await web.CoreWebView2.CapturePreviewAsync(
            CoreWebView2CapturePreviewImageFormat.Png,
            image).WaitAsync(timeout.Token);
    }

    private static async Task<DatabaseEvidence> ReadEvidenceAsync(string databasePath)
    {
        await using var store = new SqliteMapStore(databasePath);
        await store.InitializeAsync();
        var graph = await store.LoadGraphAsync();
        var placements = graph.Placements.Values
            .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        var sample = placements[^1];
        return new DatabaseEvidence(
            graph.Map.Id.ToString(),
            graph.Map.Revision,
            string.Join("|", placements.Select(x => x.Id.ToString())),
            string.Join("|", graph.Ideas.Keys.OrderBy(x => x.ToString(), StringComparer.Ordinal)),
            sample.Id.ToString(),
            sample.IdeaId.ToString(),
            sample.ParentId?.ToString(),
            sample.X,
            sample.Y,
            sample.Z,
            sample.Annotation,
            graph.Ideas.Values.Count(x => x.Title == "Domande"),
            placements.Count(x => graph.Ideas[x.IdeaId].Title == "Domande"));
    }

    private sealed record DatabaseEvidence(
        string MapId,
        long Revision,
        string PlacementIds,
        string IdeaIds,
        string SamplePlacementId,
        string SampleIdeaId,
        string? SampleParentId,
        double X,
        double Y,
        double Z,
        string Annotation,
        int SharedIdeaCount,
        int SharedPlacementCount);
}