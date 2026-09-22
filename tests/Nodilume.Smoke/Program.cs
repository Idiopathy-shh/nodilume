using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Core;
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
        var databasePath = Path.Combine(root, "graph03.sqlite");
        SeedAsync(databasePath).GetAwaiter().GetResult();

        var result = 1;
        var pass = 0;
        DatabaseEvidence? firstEvidence = null;

        void StartWindow()
        {
            pass++;
            var currentPass = pass;
            var profile = Path.Combine(root, "WebView2-" + currentPass);
            var fileDialog = new SmokeMapFileDialog();
            var window = new MainWindow(profile, databasePath, fileDialog);
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
                            throw new Exception("Persistent graph identity or values changed across real window reopen.");

                        await MapManagementSmoke.RunAsync(window, root, databasePath);
                        await IdeaEditorSmoke.RunAsync(window, root, databasePath);
                        await RelationEditorSmoke.RunAsync(window, root, databasePath);
                        await SearchUiSmoke.RunAsync(window, databasePath);
                        await FileTransferSmoke.RunAsync(
                            window, root, databasePath, fileDialog);
                        result = 0;
                        Console.WriteLine(
                            "PASS: GRAPH.03 WPF/WebView2 semantic navigation, three nested contexts, "
                            + "transverse destination/return, ambiguity, resize and persistent reopen; runtime "
                            + ((WebView2)window.FindName("Viewer")).CoreWebView2.Environment.BrowserVersionString);
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        Task<string> Script(string source) => web.ExecuteScriptAsync(source).WaitAsync(timeout.Token);

        async Task WaitFor(string expression)
        {
            var started = DateTime.UtcNow;
            while (await Script(expression) != "true")
            {
                if (DateTime.UtcNow - started > TimeSpan.FromSeconds(10))
                {
                    var diagnostic = await Script(
                        "JSON.stringify({state:document.body?.dataset.state,"
                        + "load:document.body?.dataset.loadState,"
                        + "crumb:document.querySelector('#breadcrumbs .current')?.textContent,"
                        + "projection:document.getElementById('projection-status')?.textContent,"
                        + "selection:document.getElementById('selection-title')?.textContent,"
                        + "requestContext:document.body?.dataset.requestContext,"
                        + "responseContext:document.body?.dataset.responseContext,"
                        + "requestId:document.body?.dataset.requestId,"
                        + "responseRequestId:document.body?.dataset.responseRequestId})");
                    throw new Exception("Timed out waiting for: " + expression + " DOM=" + diagnostic);
                }
                await Task.Delay(50, timeout.Token);
            }
        }

        while (web.CoreWebView2 is null)
            await Task.Delay(100, timeout.Token);
        await WaitFor("document.body?.dataset.state === 'ready'");

        if (await Script("document.querySelector('canvas')?.width > 0") != "true")
            throw new Exception("Canvas missing.");

        var resources = await Script("performance.getEntriesByType('resource').map(x=>x.name)");
        if (JsonSerializer.Deserialize<string[]>(resources)!.Any(x => !x.StartsWith("https://nodilume.local/")))
            throw new Exception("Remote resource requested.");

        var rootPath = await Script("document.querySelector('#breadcrumbs .current')?.textContent");
        if (!rootPath.Contains("Radice"))
            throw new Exception("Initial semantic context is not the fixture root.");

        if (!fullInteraction) return;
        await SelectByTitleAsync(Script, "Gruppo A");
        if (await Script("document.getElementById('enter').disabled") != "false")
            throw new Exception("Gruppo A was projected as a leaf and cannot be entered.");
        await Script("document.getElementById('enter').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Gruppo A'");

        await SelectByTitleAsync(Script, "Sottogruppo A1");
        await Script("document.getElementById('enter').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Sottogruppo A1'");

        await SelectByTitleAsync(Script, "Foglia profonda");
        var selection = await Script("document.getElementById('selection-title').textContent");
        if (!selection.Contains("Foglia profonda"))
            throw new Exception("Deep leaf selection did not reach the UI.");

        if (await Script("document.getElementById('page-next').disabled") != "false")
            throw new Exception("Wide context did not expose the next child page.");
        await Script("document.getElementById('page-next').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.getElementById('page-prev').disabled === false "
            + "&& document.getElementById('selection-title').textContent === 'Foglia profonda' "
            + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Elemento ampio 139')");
        await Script("document.getElementById('page-prev').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.body.dataset.pageAfter === '' "
            + "&& document.getElementById('selection-title').textContent === 'Foglia profonda'");

        var ambiguousChoices = await Script(
            "[...document.querySelectorAll('.destination')].filter(x=>x.textContent.includes('Idea multipla')).length");
        if (ambiguousChoices != "2")
            throw new Exception("Multiple Placement destination chooser was not exposed.");

        var externalChoices = await Script(
            "[...document.querySelectorAll('.destination')].filter(x=>x.textContent.includes('Foglia B1')).length");
        if (externalChoices != "2")
            throw new Exception(
                "Opposite directed transverse relations were not exposed independently.");

        await Script(
            "[...document.querySelectorAll('.destination')].find(x=>x.textContent.includes('Foglia B1')).click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Gruppo B' "
            + "&& document.getElementById('selection-title').textContent === 'Foglia B1'");

        await WaitFor(
            "(()=>{const b=[...document.querySelectorAll('.node-label')].find(x=>x.textContent==='Foglia B1');"
            + "if(!b)return false; const r=b.getBoundingClientRect();"
            + "return Math.abs(r.x+r.width/2-innerWidth/2)<4 "
            + "&& Math.abs(r.y+r.height/2-innerHeight/2-18)<4})()");

        await Script("document.getElementById('back').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Sottogruppo A1' "
            + "&& document.getElementById('selection-title').textContent === 'Foglia profonda'");

        await Script("document.getElementById('up').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Gruppo A'");

        await Script("document.getElementById('home').click()");
        await WaitFor(
            "document.body.dataset.state === 'ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Radice'");

        var initialWidth = int.Parse(await Script("innerWidth"));
        window.Width = 1000;
        await WaitFor(
            "innerWidth < " + initialWidth
            + " && document.querySelector('canvas').clientWidth === innerWidth");
        window.Width = 1280;
        await WaitFor(
            "innerWidth === " + initialWidth
            + " && document.querySelector('canvas').clientWidth === innerWidth");

        Directory.CreateDirectory("artifacts");
        await using var image = File.Create("artifacts/graph-03-smoke.png");
        await web.CoreWebView2.CapturePreviewAsync(
            CoreWebView2CapturePreviewImageFormat.Png,
            image).WaitAsync(timeout.Token);
    }

    private static async Task SelectByTitleAsync(
        Func<string, Task<string>> script,
        string title)
    {
        var encoded = JsonSerializer.Serialize(title);
        var result = await script(
            "(()=>{const b=[...document.querySelectorAll('.node-label')].find(x=>x.textContent==="
            + encoded + "); if(!b)return false; b.click(); return true})()");
        if (result != "true")
            throw new Exception("Could not select visible node: " + title);
    }
    private static async Task SeedAsync(string databasePath)
    {
        await using var store = new SqliteMapStore(databasePath);
        await store.InitializeAsync();
        await store.CreateMapAsync(CreateGraph());
    }

    private static MapGraph CreateGraph()
    {
        var mapId = new MapId(G(1));
        var graph = new MapGraph(new MapInfo(mapId, "GRAPH.03 smoke", 0, 1));

        var rootIdea = new IdeaId(G(100));
        var groupAIdea = new IdeaId(G(101));
        var groupBIdea = new IdeaId(G(102));
        var groupA1Idea = new IdeaId(G(103));
        var deepLeafIdea = new IdeaId(G(104));
        var bLeafIdea = new IdeaId(G(105));
        var duplicateIdea = new IdeaId(G(106));
        var siblingIdea = new IdeaId(G(107));

        graph.AddIdea(rootIdea, "Radice", "root");
        graph.AddIdea(groupAIdea, "Gruppo A", "group a");
        graph.AddIdea(groupBIdea, "Gruppo B", "group b");
        graph.AddIdea(groupA1Idea, "Sottogruppo A1", "nested");
        graph.AddIdea(deepLeafIdea, "Foglia profonda", "deep");
        graph.AddIdea(bLeafIdea, "Foglia B1", "external");
        graph.AddIdea(duplicateIdea, "Idea multipla", "shared");
        graph.AddIdea(siblingIdea, "Foglia sorella", "sibling");

        var root = new PlacementId(G(200));
        var groupA = new PlacementId(G(201));
        var groupB = new PlacementId(G(202));
        var groupA1 = new PlacementId(G(203));
        var deepLeaf = new PlacementId(G(204));
        var bLeaf = new PlacementId(G(205));
        var duplicateA = new PlacementId(G(206));
        var duplicateB = new PlacementId(G(207));
        var sibling = new PlacementId(G(208));

        graph.AddPlacement(root, rootIdea, null, 0, 0, 0);
        graph.AddPlacement(groupA, groupAIdea, root, -115, 0, 0);
        graph.AddPlacement(groupB, groupBIdea, root, 115, 0, 0);
        graph.AddPlacement(groupA1, groupA1Idea, groupA, -10, 0, 0);
        graph.AddPlacement(deepLeaf, deepLeafIdea, groupA1, -36, 12, 0);
        graph.AddPlacement(duplicateA, duplicateIdea, groupA1, 34, 18, 0);
        graph.AddPlacement(sibling, siblingIdea, groupA1, 5, -42, 0);
        graph.AddPlacement(bLeaf, bLeafIdea, groupB, 26, 6, 0);
        graph.AddPlacement(duplicateB, duplicateIdea, groupB, -28, -16, 0);

        for (var index = 0; index < 140; index++)
        {
            var idea = new IdeaId(G(1000 + index));
            var placement = new PlacementId(G(2000 + index));
            graph.AddIdea(idea, $"Elemento ampio {index:D3}", "wide paging fixture");
            graph.AddPlacement(
                placement,
                idea,
                groupA1,
                (index % 14 - 7) * 13,
                (index / 14 - 5) * 13,
                (index % 5 - 2) * 5);
        }

        graph.AddRelation(new RelationId(G(300)), deepLeafIdea, bLeafIdea, "cross", true, "salto esterno");
        graph.AddRelation(new RelationId(G(301)), deepLeafIdea, duplicateIdea, "reference", true, "rappresentazioni multiple");
        graph.AddRelation(new RelationId(G(302)), deepLeafIdea, siblingIdea, "internal", false, "relazione interna");
        graph.AddRelation(new RelationId(G(303)), bLeafIdea, deepLeafIdea, "cross", true, "direzione opposta");
        return graph;
    }

    private static Guid G(int value) =>
        Guid.Parse("20000000-0000-0000-0000-" + value.ToString("D12"));
    private static async Task<DatabaseEvidence> ReadEvidenceAsync(string databasePath)
    {
        await using var store = new SqliteMapStore(databasePath);
        await store.InitializeAsync();
        var graph = await store.LoadGraphAsync();
        var placements = graph.Placements.Values
            .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        var relations = graph.Relations.Values
            .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .ToArray();

        return new DatabaseEvidence(
            graph.Map.Id.ToString(),
            graph.Map.Revision,
            string.Join("|", placements.Select(x => x.Id.ToString())),
            string.Join("|", graph.Ideas.Keys.OrderBy(x => x.ToString(), StringComparer.Ordinal)),
            string.Join("|", relations.Select(x => x.Id.ToString())),
            placements.Length,
            relations.Length,
            placements.Count(x => graph.Ideas[x.IdeaId].Title == "Idea multipla"));
    }

    private sealed record DatabaseEvidence(
        string MapId,
        long Revision,
        string PlacementIds,
        string IdeaIds,
        string RelationIds,
        int PlacementCount,
        int RelationCount,
        int SharedPlacementCount);
}
