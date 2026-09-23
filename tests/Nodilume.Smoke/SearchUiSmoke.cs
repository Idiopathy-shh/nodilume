using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Desktop;
using Nodilume.Infrastructure;

internal static class SearchUiSmoke
{
    public static async Task RunAsync(MainWindow window, string legacyPath)
    {
        var picker = (ComboBox)window.FindName("MapPicker");
        var personal = (CatalogMap)picker.SelectedItem;
        if (personal.DatabasePath == legacyPath)
            throw new InvalidOperationException("Search smoke requires the personal map.");
        var web = (WebView2)window.FindName("Viewer");
        var input = (TextBox)window.FindName("SearchInput");
        var search = (Button)window.FindName("SearchButton");
        var more = (Button)window.FindName("SearchNextButton");
        var open = (Button)window.FindName("OpenSearchResultButton");
        var results = (ListBox)window.FindName("SearchResultPicker");
        var preview = (TextBlock)window.FindName("SearchPreviewText");
        var status = (TextBlock)window.FindName("SearchStatus");
        var editorSelection = (TextBlock)window.FindName("EditorSelectionText");

        async Task<bool> Script(string expression)
        {
            if (web.CoreWebView2 is null) return false;
            try { return await web.ExecuteScriptAsync(expression) == "true"; }
            catch (InvalidOperationException) { return false; }
        }        async Task Wait(Func<Task<bool>> predicate, string error)
        {
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(24))
            {
                if (await predicate()) return;
                await Task.Delay(65);
            }
            var dom = web.CoreWebView2 is null ? "no-webview" :
                await web.ExecuteScriptAsync("JSON.stringify({state:document.body.dataset.state,"
                + "crumb:document.querySelector('#breadcrumbs .current')?.textContent,"
                + "selected:document.body.dataset.selectedPlacement,"
                + "title:document.getElementById('selection-title')?.textContent})");
            throw new TimeoutException(error + " DOM=" + dom + " UI=" + status.Text);
        }
        static void Click(Button button)
        {
            if (!button.IsEnabled)
                throw new InvalidOperationException("Disabled search control: " + button.Name);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        }
        static string ShortId(object choice)
        {
            var label = choice.ToString() ?? "";
            var marker = "nodo ";
            var index = label.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("Result omitted Placement identity.");
            return label[(index + marker.Length)..];
        }

        await Wait(() => Script("document.body.dataset.state==='ready'"),
            "Personal map was not ready for search.");        input.Text = "";
        Click(search);
        await Wait(() => Task.FromResult(status.Text.StartsWith(
            "Ricerca rifiutata:", StringComparison.Ordinal)),
            "Blank search did not fail safely.");

        input.Text = "Idea";
        Click(search);
        await Wait(() => Task.FromResult(results.Items.Count == 2),
            "Shared Idea did not expose both representations.");
        if (more.IsEnabled || results.Items.Cast<object>().Any(x =>
            !x.ToString()!.Contains("Radice aggiornata / Idea condivisa aggiornata",
                StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Search paging/path labels do not match the shared Idea fixture.");

        results.SelectedIndex = 0;
        await Wait(() => Task.FromResult(open.IsEnabled
            && preview.Text.Contains("Il contenuto e' comune", StringComparison.Ordinal)),
            "Selecting a search result did not expose preview/open.");
        var selectedPrefix = ShortId(results.SelectedItem);
        Click(open);
        await Wait(() => Script("document.body.dataset.state==='ready'"
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Radice aggiornata'"
            + "&& document.getElementById('selection-title')?.textContent==='Idea condivisa aggiornata'"
            + "&& document.body.dataset.selectedPlacement.startsWith('" + selectedPrefix + "')"),
            "Search did not navigate to the exact Placement.");
        await Wait(() => Task.FromResult(editorSelection.Text.Contains(
            "Idea condivisa aggiornata", StringComparison.Ordinal)),
            "Search selection did not synchronize the WPF editor.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.DatabasePath == legacyPath);
        await Wait(() => Script("document.body.dataset.state==='ready'"
            + "&& document.querySelector('#breadcrumbs .current')?.textContent==='Gruppo A'"
            + "&& document.getElementById('selection-title').textContent==='Sottogruppo A1'"),
            "Legacy map did not restore its persisted view after search.");
        if (input.Text.Length != 0 || results.Items.Count != 0)
            throw new InvalidOperationException("Search state leaked across maps.");
        input.Text = "Idea condivisa aggiornata";
        Click(search);
        await Wait(() => Task.FromResult(results.Items.Count == 0
            && status.Text == "Nessun risultato."),
            "Search crossed the active-map boundary.");

        picker.SelectedItem = picker.Items.Cast<CatalogMap>()
            .Single(x => x.Map.Id == personal.Map.Id);
        await Wait(() => Script("document.body.dataset.state==='ready'"
            + "&& [...document.querySelectorAll('.node-label')]"
            + ".some(x=>x.textContent==='Radice aggiornata')"),
            "Personal map was not restored after search isolation check.");
        input.Text = "Radice";
        Click(search);
        await Wait(() => Task.FromResult(results.Items.Count == 1),
            "Root Idea was not searchable.");
        results.SelectedIndex = 0;
        var rootPrefix = ShortId(results.SelectedItem);
        Click(open);
        await Wait(() => Script("document.body.dataset.state==='ready'"
            + "&& document.getElementById('selection-title')?.textContent==='Radice aggiornata'"
            + "&& document.body.dataset.selectedPlacement.startsWith('" + rootPrefix + "')"),
            "Root search result did not open from the overview.");

        Console.WriteLine(
            "PASS: GRAPH.06.05 indexed WPF search, explicit representations, exact viewer navigation and map isolation.");
    }
}
