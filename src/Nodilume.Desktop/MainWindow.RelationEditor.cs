using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private sealed record RelationChoice(Relation Relation, string Label,
        string SourceTitle, string TargetTitle)
    {
        public override string ToString() => Label;
    }

    private IdeaId? _relationSourceId;
    private IdeaId? _relationTargetId;
    private RelationId? _selectedRelationId;
    private RelationId? _deleteArmedId;
    private long _deleteArmedRevision = -1;
    private bool _loadingRelations;
    private string _relationSourceLabel = "";
    private string _relationTargetLabel = "";

    private void SetRelationControlsBusy(bool busy)
    {
        var ready = !busy && !_mapActionBusy;
        SetRelationSourceButton.IsEnabled = ready && _editorIdeaId is not null;
        SetRelationTargetButton.IsEnabled = ready && _editorIdeaId is not null;
        SwapRelationEndpointsButton.IsEnabled = ready && _relationSourceId is not null
            && _relationTargetId is not null;
        RelationPicker.IsEnabled = ready && _editorIdeaId is not null;
        RelationKindInput.IsEnabled = ready;
        RelationDirectedCheck.IsEnabled = ready;
        RelationExplanationInput.IsEnabled = ready;
        CreateRelationButton.IsEnabled = ready && _relationSourceId is not null
            && _relationTargetId is not null;
        SaveRelationButton.IsEnabled = ready && _selectedRelationId is not null;
        DeleteRelationButton.IsEnabled = ready && _selectedRelationId is not null;
    }

    private void ResetRelationEditor()
    {
        _relationSourceId = null;
        _relationTargetId = null;
        _selectedRelationId = null;
        _deleteArmedId = null;
        _deleteArmedRevision = -1;
        _relationSourceLabel = "";
        _relationTargetLabel = "";
        _loadingRelations = true;
        RelationPicker.ItemsSource = null;
        RelationPicker.SelectedItem = null;
        _loadingRelations = false;
        RelationSourceText.Text = "Origine: non scelta";
        RelationTargetText.Text = "Destinazione: non scelta";
        RelationListStatus.Text = "";
        RelationKindInput.Clear();
        RelationExplanationInput.Clear();
        RelationDirectedCheck.IsChecked = true;
        RelationStatus.Text = "";
        DeleteRelationButton.Content = "Elimina relazione";
        SetRelationControlsBusy(_editorBusy);
    }

    private void DisarmRelationDelete()
    {
        _deleteArmedId = null;
        _deleteArmedRevision = -1;
        DeleteRelationButton.Content = "Elimina relazione";
    }

    private async Task RefreshRelationListAsync(IdeaId ideaId, int selectionSerial,
        int generation, RelationId? preferredId = null)
    {
        if (_selectedMap is null) return;
        var mapId = _selectedMap.Map.Id;
        var path = _databasePath;
        await using var store = new SqliteMapStore(path);
        var page = await store.ReadRelationsTouchingIdeasAsync(mapId, [ideaId], 64);
        var relations = page.Items.ToList();
        // Preserve an explicitly edited relation while the user picks a new
        // endpoint that may not currently be connected to it.
        if (preferredId is not null && relations.All(x => x.Id != preferredId))
        {
            var edited = await store.ReadRelationAsync(mapId, preferredId.Value);
            if (edited is not null) relations.Add(edited);
        }
        var endpoints = relations.SelectMany(x => new[] {x.SourceIdeaId, x.TargetIdeaId})
            .Distinct().ToArray();
        var ideas = (await store.ReadIdeasAsync(mapId, endpoints))
            .ToDictionary(x => x.Id, x => x.Title);
        if (_closed || generation != _mapGeneration || selectionSerial != _editorSelectionSerial
            || _editorIdeaId != ideaId || _editorBusy) return;
        _loadingRelations = true;
        try
        {
            var choices = relations.Select(x => new RelationChoice(x,
                (x.IsDirected ? "→ " : "↔ ") +
                (ideas.GetValueOrDefault(x.SourceIdeaId) ?? x.SourceIdeaId.ToString()[..8])
                + " / " +
                (ideas.GetValueOrDefault(x.TargetIdeaId) ?? x.TargetIdeaId.ToString()[..8])
                + " · " + x.Kind,
                ideas.GetValueOrDefault(x.SourceIdeaId) ?? x.SourceIdeaId.ToString()[..8],
                ideas.GetValueOrDefault(x.TargetIdeaId) ?? x.TargetIdeaId.ToString()[..8])).ToArray();
            RelationPicker.ItemsSource = choices;
            RelationPicker.SelectedItem = preferredId is null ? null :
                choices.FirstOrDefault(x => x.Relation.Id == preferredId.Value);
            RelationListStatus.Text = page.HasMore
                ? "Prime 64 relazioni: elenco parziale. Seleziona un altro nodo per altre relazioni."
                : choices.Length + " relazioni per l'idea selezionata.";
            if (RelationPicker.SelectedItem is RelationChoice selected)
                LoadRelation(selected);
            else
            {
                _selectedRelationId = null;
                DisarmRelationDelete();
            }
        }
        finally { _loadingRelations = false; }
        SetRelationControlsBusy(_editorBusy);
    }

    private void LoadRelation(RelationChoice choice)
    {
        var relation = choice.Relation;
        _selectedRelationId = relation.Id;
        _relationSourceId = relation.SourceIdeaId;
        _relationTargetId = relation.TargetIdeaId;
        _relationSourceLabel = choice.SourceTitle;
        _relationTargetLabel = choice.TargetTitle;
        RelationSourceText.Text = "Origine: " + _relationSourceLabel;
        RelationTargetText.Text = "Destinazione: " + _relationTargetLabel;
        RelationKindInput.Text = relation.Kind;
        RelationDirectedCheck.IsChecked = relation.IsDirected;
        RelationExplanationInput.Text = relation.Explanation;
        DisarmRelationDelete();
        RelationStatus.Text = "Relazione esistente selezionata.";
    }

    private void RelationPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingRelations || _editorBusy) return;
        if (RelationPicker.SelectedItem is RelationChoice selected)
        {
            LoadRelation(selected);
            RelationStatus.Text = "Modifica la relazione selezionata oppure eliminala con doppia conferma.";
        }
        else
        {
            _selectedRelationId = null;
            DisarmRelationDelete();
        }
        SetRelationControlsBusy(_editorBusy);
    }

    private void SetRelationSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorBusy || _editorIdeaId is null) return;
        _relationSourceId = _editorIdeaId;
        _relationSourceLabel = IdeaTitleInput.Text;
        RelationSourceText.Text = "Origine: " + _relationSourceLabel;
        DisarmRelationDelete();
        SetRelationControlsBusy(false);
    }

    private void SetRelationTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorBusy || _editorIdeaId is null) return;
        _relationTargetId = _editorIdeaId;
        _relationTargetLabel = IdeaTitleInput.Text;
        RelationTargetText.Text = "Destinazione: " + _relationTargetLabel;
        DisarmRelationDelete();
        SetRelationControlsBusy(false);
    }

    private void SwapRelationEndpointsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorBusy || _relationSourceId is null || _relationTargetId is null) return;
        (_relationSourceId, _relationTargetId) = (_relationTargetId, _relationSourceId);
        (_relationSourceLabel, _relationTargetLabel) = (_relationTargetLabel, _relationSourceLabel);
        RelationSourceText.Text = "Origine: " + _relationSourceLabel;
        RelationTargetText.Text = "Destinazione: " + _relationTargetLabel;
        DisarmRelationDelete();
        SetRelationControlsBusy(false);
    }

    private async Task ApplyRelationAsync(
        Func<MapRelationEditor, MapId, long, Task<long>> action, string success)
    {
        if (_closed || _editorBusy || _mapActionBusy || _selectedMap is null
            || _activeMapId != _selectedMap.Map.Id.ToString() || _activeRevision < 0)
            return;
        var mapId = _selectedMap.Map.Id;
        var revision = _activeRevision;
        var generation = _mapGeneration;
        var selectedIdea = _editorIdeaId;
        var selectedRelation = _selectedRelationId;
        _editorSelectionSerial++;
        var serial = _editorSelectionSerial;
        SetEditorBusy(true);
        try
        {
            await using var store = new SqliteMapStore(_databasePath);
            var editor = new MapRelationEditor(store);
            var nextRevision = await action(editor, mapId, revision);
            if (_closed || generation != _mapGeneration) return;
            Interlocked.Exchange(ref _projectionCancellation, null)?.Cancel();
            _activeRevision = nextRevision;
            _projectionCache.Clear();
            await RefreshMapPickerAsync(mapId);
            if (_closed || generation != _mapGeneration) return;
            SetEditorBusy(false);
            if (selectedIdea is not null)
                await RefreshRelationListAsync(selectedIdea.Value, serial, generation, selectedRelation);
            if (_closed || generation != _mapGeneration) return;
            RelationStatus.Text = success;
            Viewer.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                version = 2, type = "refreshProjection",
                mapId = mapId.ToString(), revision = nextRevision
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            if (!_closed && generation == _mapGeneration)
                RelationStatus.Text = "Relazione rifiutata: " + ex.Message;
        }
        finally { if (!_closed && generation == _mapGeneration) SetEditorBusy(false); }
    }

    private async void CreateRelationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_relationSourceId is null || _relationTargetId is null) return;
        var source = _relationSourceId.Value;
        var target = _relationTargetId.Value;
        var kind = RelationKindInput.Text;
        var directed = RelationDirectedCheck.IsChecked == true;
        var explanation = RelationExplanationInput.Text;
        await ApplyRelationAsync(async (editor, mapId, revision) =>
            (await editor.CreateAsync(mapId, revision, source, target, kind,
                directed, explanation)).Revision, "Relazione creata e salvata.");
    }

    private async void SaveRelationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRelationId is null || _relationSourceId is null
            || _relationTargetId is null) return;
        var id = _selectedRelationId.Value;
        var source = _relationSourceId.Value;
        var target = _relationTargetId.Value;
        var kind = RelationKindInput.Text;
        var directed = RelationDirectedCheck.IsChecked == true;
        var explanation = RelationExplanationInput.Text;
        await ApplyRelationAsync(async (editor, mapId, revision) =>
            (await editor.UpdateAsync(mapId, revision, id, source, target,
                kind, directed, explanation)).Revision, "Relazione aggiornata.");
    }

    private async void DeleteRelationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorBusy || _selectedRelationId is null) return;
        var id = _selectedRelationId.Value;
        if (_deleteArmedId != id || _deleteArmedRevision != _activeRevision)
        {
            _deleteArmedId = id;
            _deleteArmedRevision = _activeRevision;
            DeleteRelationButton.Content = "Conferma eliminazione";
            RelationStatus.Text = "Premi di nuovo per eliminare solo questa relazione.";
            return;
        }
        DisarmRelationDelete();
        await ApplyRelationAsync((editor, mapId, revision) =>
            editor.RemoveAsync(mapId, revision, id), "Relazione eliminata.");
    }
}
