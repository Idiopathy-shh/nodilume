using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Application;
using Nodilume.Desktop;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class FileTransferSmoke
{
    public static async Task RunAsync(
        MainWindow window,
        string root,
        string legacyPath,
        SmokeMapFileDialog dialog)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var export = (Button)window.FindName("ExportMapButton");
        var import = (Button)window.FindName("ImportMapButton");
        var status = (TextBlock)window.FindName("MapStatus");
        var web = (WebView2)window.FindName("Viewer");
        var source = (CatalogMap)picker.SelectedItem;
        if (source.DatabasePath == legacyPath)
            throw new InvalidOperationException("File transfer requires the personal map.");

        string sourceJson;
        await using (var store = new SqliteMapStore(source.DatabasePath))
            sourceJson = PortableMapJson.Export(await store.LoadGraphAsync());
        async Task<bool> Script(string expression)
        {
            if (web.CoreWebView2 is null) return false;
            try { return await web.ExecuteScriptAsync(expression) == "true"; }
            catch (InvalidOperationException) { return false; }
        }
        async Task Wait(Func<Task<bool>> predicate, string error)
        {
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(24))
            {
                if (await predicate()) return;
                await Task.Delay(65);
            }
            var dom = web.CoreWebView2 is null ? "no-webview" :
                await web.ExecuteScriptAsync(
                    "JSON.stringify({state:document.body.dataset.state,"
                    + "title:document.querySelector('#breadcrumbs .current')?.textContent})");
            throw new TimeoutException(error + " DOM=" + dom + " UI=" + status.Text);
        }
        static void Click(Button button)
        {
            if (!button.IsEnabled)
                throw new InvalidOperationException("Disabled file control: " + button.Name);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        }

        var exportPath = Path.Combine(root, "roundtrip.nodilume.json");
        dialog.ExportPath = exportPath;
        Click(export);
        await Wait(() => Task.FromResult(File.Exists(exportPath)
            && status.Text.StartsWith("Mappa esportata:", StringComparison.Ordinal)
            && export.IsEnabled && import.IsEnabled),
            "Real export button did not produce the portable file.");
        if (dialog.SuggestedFileName is null
            || !dialog.SuggestedFileName.EndsWith(".nodilume.json",
                StringComparison.OrdinalIgnoreCase)
            || await File.ReadAllTextAsync(exportPath) != sourceJson)
            throw new InvalidOperationException(
                "Export filename/document does not match the active map.");

        dialog.ImportPath = exportPath;
        Click(import);
        await Wait(async () =>
            picker.Items.Count == 3
            && picker.SelectedItem is CatalogMap entry
            && entry.Map.Id != source.Map.Id
            && await Script("document.body.dataset.state==='ready'"
                + "&& [...document.querySelectorAll('.node-label')]"
                + ".some(x=>x.textContent==='Radice aggiornata')"),
            "Real import button did not create and open a new map.");
        var imported = (CatalogMap)picker.SelectedItem;

        await using (var importedStore = new SqliteMapStore(imported.DatabasePath))
        await using (var sourceStore = new SqliteMapStore(source.DatabasePath))
        {
            var importedGraph = await importedStore.LoadGraphAsync();
            var sourceGraph = await sourceStore.LoadGraphAsync();
            if (imported.Map.Revision != 0
                || PortableMapJson.Export(importedGraph) != sourceJson
                || PortableMapJson.Export(sourceGraph) != sourceJson)
                throw new InvalidOperationException(
                    "File round-trip changed content, revision or source map.");
        }

        var malformed = Path.Combine(root, "malformed.nodilume.json");
        await File.WriteAllTextAsync(malformed, "{");
        var databasesBefore = GuidDatabaseCount(root);
        dialog.ImportPath = malformed;
        Click(import);
        await Wait(() => Task.FromResult(status.Text.StartsWith(
            "Importazione rifiutata:", StringComparison.Ordinal)),
            "Malformed UI import was not rejected.");
        if (picker.Items.Count != 3
            || ((CatalogMap)picker.SelectedItem).Map.Id != imported.Map.Id
            || GuidDatabaseCount(root) != databasesBefore)
            throw new InvalidOperationException(
                "Rejected UI import changed catalog or active map.");

        dialog.ImportPath = null;
        Click(import);
        await Wait(() => Task.FromResult(status.Text == "Importazione annullata."),
            "Cancelled UI import was not reported.");
        if (((CatalogMap)picker.SelectedItem).Map.Id != imported.Map.Id)
            throw new InvalidOperationException("Cancelled import changed active map.");

        var reopened = new MainWindow(
            Path.Combine(root, "WebView2-import-reopen"),
            legacyPath,
            new SmokeMapFileDialog());
        try
        {
            reopened.Show();
            var nextPicker = (ComboBox)reopened.FindName("MapPicker");
            var nextWeb = (WebView2)reopened.FindName("Viewer");
            await Wait(async () =>
                nextPicker.SelectedItem is CatalogMap entry
                && entry.Map.Id == imported.Map.Id
                && nextWeb.CoreWebView2 is not null
                && await nextWeb.ExecuteScriptAsync(
                    "document.body.dataset.state==='ready'"
                    + "&& [...document.querySelectorAll('.node-label')]"
                    + ".some(x=>x.textContent==='Radice aggiornata')") == "true",
                "Imported map selection did not survive reopen.");
        }
        finally
        {
            reopened.Close();
        }

        Console.WriteLine(
            "PASS: GRAPH.06.06 WPF file export/import, fresh map identity, rejection and reopen.");
    }

    private static int GuidDatabaseCount(string root) =>
        Directory.EnumerateFiles(root, "*.sqlite")
            .Count(path => Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(path), "D", out _));
}
