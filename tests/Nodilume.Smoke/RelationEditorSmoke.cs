using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Core;
using Nodilume.Desktop;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class RelationEditorSmoke
{
    public static async Task RunAsync(MainWindow window, string root, string legacyPath)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var active = (CatalogMap)picker.SelectedItem;
        if (active.DatabasePath == legacyPath)
            throw new InvalidOperationException("Relation editor must use personal fixture.");
        var web = (WebView2)window.FindName("Viewer");
        var source = (Button)window.FindName("SetRelationSourceButton");
        var target = (Button)window.FindName("SetRelationTargetButton");
        var swap = (Button)window.FindName("SwapRelationEndpointsButton");
        var list = (ComboBox)window.FindName("RelationPicker");
        var kind = (TextBox)window.FindName("RelationKindInput");
        var explanation = (TextBox)window.FindName("RelationExplanationInput");
        var directed = (CheckBox)window.FindName("RelationDirectedCheck");
        var create = (Button)window.FindName("CreateRelationButton");
        var save = (Button)window.FindName("SaveRelationButton");
        var delete = (Button)window.FindName("DeleteRelationButton");
        var selection = (TextBlock)window.FindName("EditorSelectionText");
        var status = (TextBlock)window.FindName("RelationStatus");

        async Task<MapGraph> Graph()
        {
            await using var store = new SqliteMapStore(active.DatabasePath);
            return await store.LoadGraphAsync();
        }
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
                await web.ExecuteScriptAsync("JSON.stringify({state:document.body.dataset.state,"
                + "load:document.body.dataset.loadState,selected:document.getElementById('selection-title')?.textContent,"
                + "relations:document.getElementById('relations')?.textContent})");
            throw new TimeoutException(error + " DOM=" + dom + " UI=" + status.Text);
        }
        static void Click(Button button)
        {
            if (!button.IsEnabled)
                throw new InvalidOperationException("Disabled relation control: " + button.Name);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        }
        async Task Select(string label)
        {
            var encoded = JsonSerializer.Serialize(label);
            await Wait(() => Script("(()=>{if(document.body.dataset.state!=='ready')return false;"
                + "const b=[...document.querySelectorAll('.node-label')]"
                + ".find(x=>x.textContent===" + encoded + ");"
                + "if(!b)return false;b.click();return true})()"),
                "Node not available in relation viewer: " + label);
            await Wait(() => Task.FromResult(selection.Text.Contains(label, StringComparison.Ordinal)),
                "WPF relation editor did not receive selected Idea.");
        }

        await Wait(() => Script("document.body.dataset.state==='ready'"),
            "Personal map not ready for relation editor.");
        await Select("Radice aggiornata");
        Click(source);
        await Wait(() => Task.FromResult(((TextBlock)window.FindName("RelationSourceText"))
            .Text.Contains("Radice aggiornata", StringComparison.Ordinal)), "Source idea not captured.");
        await web.ExecuteScriptAsync("document.getElementById('enter').click()");
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Radice aggiornata' "
            + "&& [...document.querySelectorAll('.node-label')]"
            + ".some(x=>x.textContent==='Idea condivisa aggiornata')"),
            "Child context not ready for destination.");
        await Select("Idea condivisa aggiornata");
        Click(target);
        kind.Text = "sostiene";
        directed.IsChecked = true;
        explanation.Text = "Spiegazione 🌱";
        Click(create);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Relations.Count == 1
                && await Script("document.body.dataset.state==='ready' "
                    + "&& document.getElementById('relations')?.textContent?.includes('sostiene')");
        }, "Relation did not persist and render in the real WPF/WebView2 graph.");
        var first = (await Graph()).Relations.Values.Single();
        await Wait(() => Task.FromResult(list.Items.Count == 1 && list.IsEnabled),
            "New relation not available for editing.");
        list.SelectedIndex = 0;
        await Wait(() => Task.FromResult(save.IsEnabled && delete.IsEnabled && swap.IsEnabled),
            "Existing relation was not selected.");
        Click(swap);
        kind.Text = "contraddice";
        directed.IsChecked = false;
        explanation.Text = "Spiegazione aggiornata";
        Click(save);
        await Wait(async () =>
        {
            var graph = await Graph();
            var updated = graph.Relations.Values.SingleOrDefault();
            return updated is not null && updated.Id == first.Id
                && updated.SourceIdeaId == first.TargetIdeaId
                && updated.TargetIdeaId == first.SourceIdeaId
                && !updated.IsDirected && updated.Kind == "contraddice"
                && updated.Explanation == "Spiegazione aggiornata"
                && await Script("document.body.dataset.state==='ready' "
                    + "&& document.getElementById('relations')?.textContent?.includes('contraddice')");
        }, "Relation update/inversion did not survive SQLite and 3D refresh.");
        await Wait(() => Task.FromResult(delete.IsEnabled), "Delete relation was disabled.");
        Click(delete);
        if ((await Graph()).Relations.Count != 1
            || delete.Content?.ToString() != "Conferma eliminazione")
            throw new InvalidOperationException("First delete click must only arm confirmation.");
        Click(delete);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Relations.Count == 0 && graph.Ideas.Count == 2
                && graph.Placements.Count == 3
                && await Script("document.body.dataset.state==='ready' "
                    + "&& !document.getElementById('relations')?.textContent?.includes('contraddice')");
        }, "Confirmed deletion did not remove only the conceptual relation.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.DatabasePath == legacyPath);
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Gruppo A' "
            + "&& document.getElementById('selection-title').textContent==='Sottogruppo A1'"),
            "Switching to legacy map after relation edit did not restore its persisted view.");
        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.Map.Id == active.Map.Id);
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& [...document.querySelectorAll('.node-label')]"
            + ".some(x=>x.textContent==='Radice aggiornata')"),
            "Returning to personal map after relation edit failed.");
        var reopened = new MainWindow(Path.Combine(root, "WebView2-relation-reopen"), legacyPath);
        try
        {
            reopened.Show();
            var nextPicker = (ComboBox)reopened.FindName("MapPicker");
            var nextWeb = (WebView2)reopened.FindName("Viewer");
            await Wait(async () => nextPicker.SelectedItem is CatalogMap current
                && current.Map.Id == active.Map.Id && nextWeb.CoreWebView2 is not null
                && await nextWeb.ExecuteScriptAsync("document.body.dataset.state==='ready' "
                + "&& [...document.querySelectorAll('.node-label')]"
                + ".some(x=>x.textContent==='Radice aggiornata')") == "true",
                "Selected personal map not restored in reopened WPF window.");
            if ((await Graph()).Relations.Count != 0)
                throw new InvalidOperationException("Deleted conceptual relation reappeared after reopen.");
        }
        finally { reopened.Close(); }
        Console.WriteLine("PASS: GRAPH.06.04 WPF/WebView2 create, view, invert/edit and two-click delete relation; map isolation/reopen.");
    }
}
