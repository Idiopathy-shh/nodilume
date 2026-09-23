using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Application;
using Nodilume.Desktop;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class BackupRecoverySmoke
{
    public static async Task RunAsync(
        MainWindow window,
        string root,
        string legacyPath,
        SmokeMapFileDialog dialog)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var backup = (Button)window.FindName("BackupMapButton");
        var restore = (Button)window.FindName("RestoreBackupButton");
        var status = (TextBlock)window.FindName("MapStatus");
        var web = (WebView2)window.FindName("Viewer");
        var source = (CatalogMap)picker.SelectedItem;
        var catalog = new MapCatalog(root, legacyPath);
        string sourceJson;
        await using (var store =
                     new SqliteMapStore(source.DatabasePath))
            sourceJson = PortableMapJson.Export(
                await store.LoadGraphAsync());

        async Task<bool> Script(string expression)
        {
            if (web.CoreWebView2 is null) return false;
            try
            {
                return await web.ExecuteScriptAsync(expression)
                    == "true";
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        async Task Wait(
            Func<Task<bool>> predicate,
            string error)
        {
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started
                   < TimeSpan.FromSeconds(30))
            {
                if (await predicate()) return;
                await Task.Delay(65);
            }
            var dom = web.CoreWebView2 is null
                ? "no-webview"
                : await web.ExecuteScriptAsync(
                    "JSON.stringify({state:document.body.dataset.state,"
                    + "title:document.querySelector("
                    + "'#breadcrumbs .current')?.textContent})");
            throw new TimeoutException(
                error + " DOM=" + dom + " UI=" + status.Text);
        }

        static void Click(Button button)
        {
            if (!button.IsEnabled)
                throw new InvalidOperationException(
                    "Disabled backup control: " + button.Name);
            button.RaiseEvent(new RoutedEventArgs(
                ButtonBase.ClickEvent, button));
        }

        Click(backup);
        await Wait(() => Task.FromResult(
                status.Text.StartsWith(
                    "Backup creato:", StringComparison.Ordinal)
                && backup.IsEnabled
                && restore.IsEnabled),
            "Real backup button did not create a package.");
        var packages = Directory.EnumerateFiles(
                catalog.BackupDirectory,
                "*" + MapCatalog.BackupExtension)
            .ToArray();
        if (packages.Length != 1
            || !status.Text.Contains(
                "retention 10", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Managed backup or retention status is missing.");
        dialog.BackupPath = packages[0];
        Click(restore);
        await Wait(async () =>
            picker.Items.Count == 4
            && picker.SelectedItem is CatalogMap entry
            && entry.Map.Id != source.Map.Id
            && await Script(
                "document.body.dataset.state==='ready'"
                + "&& [...document.querySelectorAll('.node-label')]"
                + ".some(x=>x.textContent==='Radice aggiornata')"),
            "Real restore button did not create and open a new map.");
        var restored = (CatalogMap)picker.SelectedItem;
        if (!string.Equals(
                dialog.RequestedBackupDirectory,
                catalog.BackupDirectory,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Restore dialog did not start in the managed folder.");

        await using (var restoredStore =
                     new SqliteMapStore(restored.DatabasePath))
        await using (var sourceStore =
                     new SqliteMapStore(source.DatabasePath))
        {
            var restoredGraph =
                await restoredStore.LoadGraphAsync();
            var sourceGraph =
                await sourceStore.LoadGraphAsync();
            if (restored.Map.Revision != source.Map.Revision
                || PortableMapJson.Export(restoredGraph)
                    != sourceJson
                || PortableMapJson.Export(sourceGraph)
                    != sourceJson)
                throw new InvalidOperationException(
                    "Backup restore changed content, revision "
                    + "or source map.");
        }

        var malformed = Path.Combine(
            root, "malformed" + MapCatalog.BackupExtension);
        await File.WriteAllTextAsync(malformed, "not a zip");
        var databaseCount = GuidDatabaseCount(root);
        dialog.BackupPath = malformed;
        Click(restore);
        await Wait(() => Task.FromResult(
                status.Text.StartsWith(
                    "Ripristino rifiutato:",
                    StringComparison.Ordinal)),
            "Malformed UI restore was not rejected.");
        if (picker.Items.Count != 4
            || ((CatalogMap)picker.SelectedItem).Map.Id
                != restored.Map.Id
            || GuidDatabaseCount(root) != databaseCount)
            throw new InvalidOperationException(
                "Rejected UI restore changed catalog or active map.");

        dialog.BackupPath = null;
        Click(restore);
        await Wait(() => Task.FromResult(
                status.Text == "Ripristino annullato."),
            "Cancelled UI restore was not reported.");
        if (((CatalogMap)picker.SelectedItem).Map.Id
            != restored.Map.Id)
            throw new InvalidOperationException(
                "Cancelled restore changed active map.");

        var reopened = new MainWindow(
            Path.Combine(root, "WebView2-backup-reopen"),
            legacyPath,
            new SmokeMapFileDialog());
        try
        {
            reopened.Show();
            var nextPicker =
                (ComboBox)reopened.FindName("MapPicker");
            var nextWeb =
                (WebView2)reopened.FindName("Viewer");
            await Wait(async () =>
                nextPicker.SelectedItem is CatalogMap entry
                && entry.Map.Id == restored.Map.Id
                && nextWeb.CoreWebView2 is not null
                && await nextWeb.ExecuteScriptAsync(
                    "document.body.dataset.state==='ready'"
                    + "&& [...document.querySelectorAll("
                    + "'.node-label')]"
                    + ".some(x=>x.textContent==='Radice aggiornata')")
                    == "true",
                "Restored selection did not survive reopen.");
        }
        finally
        {
            reopened.Close();
        }
        Console.WriteLine(
            "PASS: GRAPH.06.07 WPF SQLite backup/recovery, "
            + "fresh identity, rejection and reopen.");
    }

    private static int GuidDatabaseCount(string root) =>
        Directory.EnumerateFiles(root, "*.sqlite")
            .Count(path => Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(path),
                "D",
                out _));
}