using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int result = 1;
        var profile = Path.Combine(Path.GetTempPath(), "NodilumeSmoke", Guid.NewGuid().ToString("N"));
        var window = new MainWindow(profile);
        window.Loaded += async (_, _) =>
        {
            try
            {
                var web = (WebView2)window.FindName("Viewer");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                Task<string> Script(string source) => web.ExecuteScriptAsync(source).WaitAsync(timeout.Token);
                async Task WaitFor(string expression)
                {
                    while (await Script(expression) != "true") await Task.Delay(50, timeout.Token);
                }
                while (web.CoreWebView2 is null || await Script("document.body?.dataset.state") != "\"ready\"")
                    await Task.Delay(100, timeout.Token);
                var counts = await Script("document.getElementById('counts').textContent");
                if (!counts.Contains("25 nodi")) throw new Exception($"Unexpected scene: {counts}");
                var canvas = await Script("document.querySelector('canvas')?.width > 0");
                if (canvas != "true") throw new Exception("Canvas missing");
                var resources = await Script("performance.getEntriesByType('resource').map(x=>x.name)");
                if (JsonSerializer.Deserialize<string[]>(resources)!.Any(x => !x.StartsWith("https://nodilume.local/")))
                    throw new Exception("Remote resource requested");
                // Exercise real selection and animated focus through the visible UI.
                await Script("document.querySelector('.node-label').click(); document.getElementById('focus').click()");
                const string centered = "(()=>{const r=document.querySelector('.node-label').getBoundingClientRect(); return Math.abs(r.x+r.width/2-innerWidth/2)<1 && Math.abs(r.y+r.height/2-innerHeight/2-18)<1})()";
                await WaitFor(centered);
                var selection = await Script("document.getElementById('selection-title').textContent");
                if (!selection.Contains("Idee connesse")) throw new Exception("Selection did not reach UI");
                await Script("document.getElementById('home').click()");
                await WaitFor("(()=>{const r=document.querySelector('.node-label').getBoundingClientRect(); return Math.abs(r.x+r.width/2-innerWidth/2)>30})()");
                int initialWidth = int.Parse(await Script("innerWidth"));
                window.Width = 1000;
                await WaitFor($"innerWidth < {initialWidth} && document.querySelector('canvas').clientWidth === innerWidth");
                window.Width = 1280;
                await WaitFor($"innerWidth === {initialWidth} && document.querySelector('canvas').clientWidth === innerWidth");
                Directory.CreateDirectory("artifacts");
                await using var image = File.Create("artifacts/graph-01-smoke.png");
                await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image).WaitAsync(timeout.Token);
                var report = $"PASS: WPF/WebView2, {counts}, local resources, selection, projected-node focus/home movement, resize, runtime {web.CoreWebView2.Environment.BrowserVersionString}";
                File.WriteAllText("artifacts/graph-01-smoke.txt", report);
                Console.WriteLine(report);
                result = 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show();
        app.Run();
        return result;
    }
}
