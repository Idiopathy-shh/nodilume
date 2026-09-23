using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Core;
using Nodilume.Desktop;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class MapManagementSmoke
{
    public static async Task RunAsync(MainWindow window, string root, string legacyPath)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var input = (TextBox)window.FindName("MapNameInput");
        var create = (Button)window.FindName("NewMapButton");
        var rename = (Button)window.FindName("RenameMapButton");
        var web = (WebView2)window.FindName("Viewer");
        var legacy = (CatalogMap)picker.SelectedItem;
        if (legacy.DatabasePath != legacyPath || picker.Items.Count != 1)
            throw new InvalidOperationException("Legacy map missing in real picker.");

        input.Text = "  Nuova mappa 🌱  ";
        Click(create);
        await WaitAsync(async () =>
        {
            var entry = picker.SelectedItem as CatalogMap;
            return entry is not null && entry.Map.Id != legacy.Map.Id
                && entry.Map.Title == "Nuova mappa 🌱"
                && picker.Items.Count == 2
                && await ScriptAsync(web,
                    "document.body.dataset.state==='ready' && document.body.dataset.loadState==='empty'");
        }, "New empty map did not open in WebView2.");
        var newMap = (CatalogMap)picker.SelectedItem;
        if (newMap.DatabasePath == legacyPath)
            throw new InvalidOperationException("New map reused the legacy database.");

        input.Text = "Rinominata 🌳";
        Click(rename);
        await WaitAsync(async () =>
            picker.SelectedItem is CatalogMap entry
            && entry.Map.Id == newMap.Map.Id && entry.Map.Title == "Rinominata 🌳"
            && await ScriptAsync(web,
                "document.body.dataset.state==='ready' && document.body.dataset.loadState==='empty'"),
            "Rename was not applied to the active map.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.Map.Id == legacy.Map.Id);
        await WaitAsync(async () =>
            picker.SelectedItem is CatalogMap entry && entry.Map.Id == legacy.Map.Id
            && await ScriptAsync(web,
                "document.body.dataset.state==='ready' "
                + "&& document.querySelector('#breadcrumbs .current')?.textContent === 'Gruppo A' "
                + "&& document.getElementById('selection-title').textContent === 'Sottogruppo A1'"),
            "Switch back to the legacy graph did not restore its persisted context and selection.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.Map.Id == newMap.Map.Id);
        await WaitAsync(async () =>
            picker.SelectedItem is CatalogMap entry && entry.Map.Id == newMap.Map.Id
            && await ScriptAsync(web,
                "document.body.dataset.state==='ready' && document.body.dataset.loadState==='empty'"),
            "Switch to the empty graph did not clear the legacy renderer.");

        await using (var legacyStore = new SqliteMapStore(legacyPath))
        await using (var newStore = new SqliteMapStore(newMap.DatabasePath))
        {
            var oldGraph = await legacyStore.LoadGraphAsync();
            var emptyGraph = await newStore.LoadGraphAsync();
            if (oldGraph.Placements.Count != 149 || emptyGraph.Placements.Count != 0
                || emptyGraph.Map.Title != "Rinominata 🌳")
                throw new InvalidOperationException("Switch/rename caused cross-map data changes.");
        }

        var reopened = new MainWindow(Path.Combine(root, "WebView2-map-reopen"), legacyPath);
        try
        {
            reopened.Show();
            var reopenedPicker = (ComboBox)reopened.FindName("MapPicker");
            var reopenedWeb = (WebView2)reopened.FindName("Viewer");
            await WaitAsync(async () =>
                reopenedPicker.SelectedItem is CatalogMap entry
                && entry.Map.Id == newMap.Map.Id && entry.Map.Title == "Rinominata 🌳"
                && await ScriptAsync(reopenedWeb,
                    "document.body.dataset.state==='ready' && document.body.dataset.loadState==='empty'"),
                "Selected map did not survive a real WPF/WebView2 window reopen.");
        }
        finally { reopened.Close(); }
        Console.WriteLine("PASS: GRAPH.06.02 WPF/WebView2 create, rename, switch, isolation and persistent reopen.");
    }

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static async Task<bool> ScriptAsync(WebView2 web, string expression)
    {
        if (web.CoreWebView2 is null) return false;
        try { return await web.ExecuteScriptAsync(expression) == "true"; }
        catch (InvalidOperationException) { return false; }
    }

    private static async Task WaitAsync(Func<Task<bool>> predicate, string error)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(22));
        while (!timeout.IsCancellationRequested)
        {
            if (await predicate()) return;
            await Task.Delay(55);
        }
        throw new TimeoutException(error);
    }
}
