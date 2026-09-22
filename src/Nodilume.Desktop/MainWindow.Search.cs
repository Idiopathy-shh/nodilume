using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private sealed record SearchChoice(
        IdeaId IdeaId,
        PlacementId? PlacementId,
        PlacementId? OpenContextPlacementId,
        string Title,
        string Content,
        string Label)
    {
        public override string ToString() => Label;
    }

    private readonly List<SearchChoice> _searchChoices = [];
    private IdeaSearchCursor? _searchCursor;
    private string _searchPrefix = "";
    private int _searchSerial;
    private bool _searchBusy;

    private void UpdateSearchControls()
    {
        var busy = _searchBusy || _mapActionBusy || _editorBusy;
        SearchInput.IsEnabled = !busy;
        SearchButton.IsEnabled = !busy;
        SearchNextButton.IsEnabled = !busy && _searchCursor is not null
            && string.Equals(SearchInput.Text.Trim(), _searchPrefix, StringComparison.Ordinal);
        SearchResultPicker.IsEnabled = !busy && _searchChoices.Count > 0;
        OpenSearchResultButton.IsEnabled = !busy
            && SearchResultPicker.SelectedItem is SearchChoice { PlacementId: not null };
    }

    private void ResetSearch()
    {
        _searchSerial++;
        _searchBusy = false;
        _searchCursor = null;
        _searchPrefix = "";
        _searchChoices.Clear();
        SearchInput.Clear();
        SearchResultPicker.ItemsSource = null;
        SearchPreviewText.Text = "";
        SearchStatus.Text = "Prefisso esatto nella mappa attiva (maiuscole/minuscole distinte).";
        UpdateSearchControls();
    }

    private async Task LoadSearchPageAsync(bool reset)
    {
        if (_closed || _searchBusy || _mapActionBusy || _editorBusy
            || _selectedMap is null || _activeMapId != _selectedMap.Map.Id.ToString())
            return;
        var prefix = reset ? SearchInput.Text : _searchPrefix;
        string normalized;
        try
        {
            normalized = MapSearchService.Prefix(prefix);
        }
        catch (Exception ex)
        {
            SearchStatus.Text = "Ricerca rifiutata: " + ex.Message;
            return;
        }
        var cursor = reset ? null : _searchCursor;
        if (!reset && cursor is null) return;
        var serial = ++_searchSerial;
        var generation = _mapGeneration;
        var mapId = _selectedMap.Map.Id;
        var path = _databasePath;
        _searchBusy = true;
        UpdateSearchControls();
        SearchStatus.Text = "Ricerca…";
        try
        {
            await using var store = new SqliteMapStore(path);
            var page = await new MapSearchService(store).SearchAsync(
                mapId, normalized, after: cursor);
            if (_closed || serial != _searchSerial || generation != _mapGeneration)
                return;
            if (reset)
            {
                _searchChoices.Clear();
                _searchPrefix = normalized;
            }
            foreach (var idea in page.Items)
            {
                if (idea.Representations.Count == 0)
                {
                    _searchChoices.Add(new SearchChoice(
                        idea.IdeaId, null, null, idea.Title, idea.Content,
                        idea.Title + (page.RepresentationsPartial
                            ? " · nessuna rappresentazione caricata"
                            : " · nessuna rappresentazione")));
                    continue;
                }
                foreach (var representation in idea.Representations)
                    _searchChoices.Add(new SearchChoice(
                        idea.IdeaId, representation.PlacementId,
                        representation.OpenContextPlacementId,
                        idea.Title, idea.Content,
                        idea.Title + " · " + representation.Path
                        + " · nodo " + representation.PlacementId.ToString()[..8]));
            }
            _searchCursor = page.NextCursor;
            SearchResultPicker.ItemsSource = null;
            SearchResultPicker.ItemsSource = _searchChoices.ToArray();
            SearchStatus.Text = _searchChoices.Count == 0
                ? "Nessun risultato."
                : _searchChoices.Count + " rappresentazioni"
                  + (page.HasMoreIdeas ? " · altre Idee disponibili" : "")
                  + (page.RepresentationsPartial ? " · rappresentazioni parziali" : "");
        }
        catch (Exception ex)
        {
            if (!_closed && serial == _searchSerial && generation == _mapGeneration)
                SearchStatus.Text = "Ricerca rifiutata: " + ex.Message;
        }
        finally
        {
            if (!_closed && serial == _searchSerial && generation == _mapGeneration)
            {
                _searchBusy = false;
                UpdateSearchControls();
            }
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e) =>
        await LoadSearchPageAsync(reset: true);

    private async void SearchNextButton_Click(object sender, RoutedEventArgs e) =>
        await LoadSearchPageAsync(reset: false);

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateSearchControls();

    private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await LoadSearchPageAsync(reset: true);
    }

    private void SearchResultPicker_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultPicker.SelectedItem is SearchChoice choice)
            SearchPreviewText.Text = string.IsNullOrWhiteSpace(choice.Content)
                ? "(nessun contenuto)" : choice.Content;
        else
            SearchPreviewText.Text = "";
        UpdateSearchControls();
    }

    private void OpenSearchResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _searchBusy || _mapActionBusy || _editorBusy
            || SearchResultPicker.SelectedItem is not SearchChoice
                { PlacementId: { } placementId } choice
            || _selectedMap is null || _activeMapId != _selectedMap.Map.Id.ToString()
            || _activeRevision < 0 || Viewer.CoreWebView2 is null)
            return;
        Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            version = 2,
            type = "navigateToPlacement",
            mapId = _activeMapId,
            revision = _activeRevision,
            placementId = placementId.ToString(),
            openContextPlacementId = choice.OpenContextPlacementId?.ToString()
        }, JsonOptions));
        SearchStatus.Text = "Apertura di " + choice.Title + "…";
    }
}