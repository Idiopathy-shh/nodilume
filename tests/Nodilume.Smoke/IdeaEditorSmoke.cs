using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Desktop;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class IdeaEditorSmoke
{
    public static async Task RunAsync(MainWindow window, string root, string legacyPath)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var active = (CatalogMap)picker.SelectedItem;
        if (active.DatabasePath == legacyPath)
            throw new InvalidOperationException("Editor fixture was not the new personal map.");
        var web = (WebView2)window.FindName("Viewer");
        var title = (TextBox)window.FindName("IdeaTitleInput");
        var content = (TextBox)window.FindName("IdeaContentInput");
        var note = (TextBox)window.FindName("PlacementAnnotationInput");
        var selection = (TextBlock)window.FindName("EditorSelectionText");
        var rootButton = (Button)window.FindName("CreateRootIdeaButton");
        var childButton = (Button)window.FindName("CreateChildIdeaButton");
        var saveIdea = (Button)window.FindName("SaveIdeaButton");
        var saveNote = (Button)window.FindName("SaveAnnotationButton");
        var representationButton = (Button)window.FindName("CreateRepresentationButton");

        async Task<Nodilume.Core.MapGraph> Graph()
        {
            await using var store = new SqliteMapStore(active.DatabasePath);
            return await store.LoadGraphAsync();
        }

        async Task Wait(Func<Task<bool>> predicate, string error)
        {
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(22))
            {
                if (await predicate()) return;
                await Task.Delay(65);
            }
            var diagnostic = web.CoreWebView2 is null ? "no-webview" :
                await web.ExecuteScriptAsync("JSON.stringify({state:document.body.dataset.state,"
                + "load:document.body.dataset.loadState,selection:document.getElementById('selection-title')?.textContent,"
                + "status:document.getElementById('projection-status')?.textContent})");
            throw new TimeoutException(error + " DOM=" + diagnostic
                + " UI=" + ((TextBlock)window.FindName("EditorStatus")).Text);
        }

        async Task<bool> Script(string js)
        {
            if (web.CoreWebView2 is null) return false;
            try { return await web.ExecuteScriptAsync(js) == "true"; }
            catch (InvalidOperationException) { return false; }
        }

        async Task Select(string name)
        {
            var encoded = JsonSerializer.Serialize(name);
            await Wait(() => Script("(()=>{const b=[...document.querySelectorAll('.node-label')]"
                + ".find(x=>x.textContent===" + encoded + ");"
                + "if(!b||document.body.dataset.state!=='ready')return false;b.click();return true})()"),
                "Node label missing in the real viewer: " + name);
            await Wait(() => Task.FromResult(selection.Text.Contains(name, StringComparison.Ordinal)),
                "WPF editor did not receive selected node: " + name);
        }

        static void Click(Button button)
        {
            if (!button.IsEnabled) throw new InvalidOperationException("Editor control disabled: " + button.Name);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        }

        title.Text = "Radice editor";
        content.Text = "Testo iniziale";
        Click(rootButton);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Ideas.Count == 1 && graph.Placements.Count == 1
                && await Script("document.body.dataset.state==='ready' "
                    + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Radice editor')");
        }, "New Idea root was not persisted and shown in WPF/WebView2.");
        await Select("Radice editor");
        title.Text = "Radice aggiornata";
        content.Text = "Contenuto condiviso 🪴";
        Click(saveIdea);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Ideas.Values.Single().Title == "Radice aggiornata"
                && graph.Ideas.Values.Single().Content == "Contenuto condiviso 🪴"
                && await Script("document.body.dataset.state==='ready' "
                    + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Radice aggiornata')");
        }, "Edited root did not round-trip into the real viewer.");
        await Select("Radice aggiornata");

        title.Text = "Idea figlia";
        content.Text = "Condiviso fra nodi";
        Click(childButton);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Ideas.Count == 2 && graph.Placements.Count == 2
                && graph.Placements.Values.Count(x => x.ParentId is not null) == 1
                && await Script("document.body.dataset.state==='ready'");
        }, "New child Idea was not persisted.");
        await Select("Radice aggiornata");
        await Wait(() => Script("document.getElementById('enter').disabled===false"),
            "Created child was not exposed as navigable.");
        await web.ExecuteScriptAsync("document.getElementById('enter').click()");
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Radice aggiornata' "
            + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Idea figlia')"),
            "Navigating into created child did not show it.");
        await Select("Idea figlia");
        note.Text = "Annotazione locale A";
        Click(saveNote);
        await Wait(async () =>
        {
            var graph = await Graph();
            return graph.Placements.Values.Any(x => x.Annotation == "Annotazione locale A")
                && await Script("document.body.dataset.state==='ready'");
        }, "Local note was not saved.");

        await Select("Idea figlia");
        Click(representationButton);
        await Wait(async () =>
        {
            var graph = await Graph();
            var childIdea = graph.Ideas.Values.Single(x => x.Title == "Idea figlia");
            return graph.Ideas.Count == 2 && graph.Placements.Count == 3
                && graph.Placements.Values.Count(x => x.IdeaId == childIdea.Id) == 2
                && await Script("document.body.dataset.state==='ready' "
                    + "&& [...document.querySelectorAll('.node-label')].filter(x=>x.textContent==='Idea figlia').length===2");
        }, "Second representation did not appear alongside first.");
        await Select("Idea figlia");
        title.Text = "Idea condivisa aggiornata";
        content.Text = "Il contenuto e' comune";
        Click(saveIdea);
        await Wait(async () =>
        {
            var graph = await Graph();
            var idea = graph.Ideas.Values.Single(x => x.Title == "Idea condivisa aggiornata");
            return graph.Placements.Values.Count(x => x.IdeaId == idea.Id) == 2
                && graph.Placements.Values.Count(x => x.Annotation == "Annotazione locale A") == 1
                && await Script("document.body.dataset.state==='ready' "
                    + "&& [...document.querySelectorAll('.node-label')].filter(x=>x.textContent==='Idea condivisa aggiornata').length===2");
        }, "Shared edit/local annotation identity was not preserved in both nodes.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>().Single(x => x.DatabasePath == legacyPath);
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Radice'"),
            "Switching to legacy map after edits failed.");
        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.Map.Id == active.Map.Id);
        await Wait(() => Script("document.body.dataset.state==='ready' "
            + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Radice aggiornata')"),
            "Switch back to edited map did not restore its persisted ideas.");
        var reopened = new MainWindow(Path.Combine(root, "WebView2-editor-reopen"), legacyPath);
        try
        {
            reopened.Show();
            var nextPicker = (ComboBox)reopened.FindName("MapPicker");
            var nextWeb = (WebView2)reopened.FindName("Viewer");
            await Wait(async () =>
                nextPicker.SelectedItem is CatalogMap entry && entry.Map.Id == active.Map.Id
                && nextWeb.CoreWebView2 is not null
                && await nextWeb.ExecuteScriptAsync("document.body.dataset.state==='ready' "
                    + "&& [...document.querySelectorAll('.node-label')].some(x=>x.textContent==='Radice aggiornata')") == "true",
                "Edited map was not retained after real window reopen.");
        }
        finally { reopened.Close(); }
        Console.WriteLine("PASS: GRAPH.06.03 WPF/WebView2 create root and child, select, edit shared Idea and local annotation, multi-placement and reopen.");
    }
}
