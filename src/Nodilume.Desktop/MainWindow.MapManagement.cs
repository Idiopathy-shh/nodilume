using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nodilume.Core;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;
using Nodilume.Application;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private async Task InitializeMapCatalogAsync()
    {
        if (_seedDemoOnStartup && !File.Exists(_legacyDatabasePath)
            && (await _mapCatalog.ListAsync()).Count == 0)
        {
            await using var store = new SqliteMapStore(_legacyDatabasePath);
            await DemoMapInitializer.EnsureAsync(store);
        }
        var selected = await _mapCatalog.RestoreSelectionAsync()
            ?? throw new InvalidOperationException("Nessuna mappa disponibile.");
        _databasePath = selected.DatabasePath;
        _selectedMap = selected;
        await RefreshMapPickerAsync(selected.Map.Id);
    }

    private async Task RefreshMapPickerAsync(MapId selectedId)
    {
        var maps = await _mapCatalog.ListAsync();
        _updatingPicker = true;
        try
        {
            MapPicker.ItemsSource = maps;
            MapPicker.SelectedItem = maps.SingleOrDefault(x => x.Map.Id == selectedId);
            if (MapPicker.SelectedItem is CatalogMap current)
            {
                _selectedMap = current;
                MapNameInput.Text = current.Map.Title;
                Title = "Nodilume — " + current.Map.Title;
                MapStatus.Text = "Mappa attiva: " + current.Map.Title;
            }
            else
                throw new FileNotFoundException("La mappa selezionata non è più disponibile.");
        }
        finally { _updatingPicker = false; }
    }

    private async Task ActivateMapAsync(CatalogMap selected)
    {
        _mapGeneration++;
        ResetIdeaEditor();
        ResetSearch();
        Interlocked.Exchange(ref _projectionCancellation, null)?.Cancel();
        _projectionCache.Clear();
        _lastContextPlacementId = null;
        _activeMapId = null;
        _activeRevision = -1;
        _databasePath = selected.DatabasePath;
        _selectedMap = selected;
        _recoveringViewer = false;
        await RefreshMapPickerAsync(selected.Map.Id);
        if (_closed) return;
        Status.Text = "Caricamento mappa selezionata…";
        // The renderer must discard old map/context/selection/page/camera state.
        Viewer.CoreWebView2?.Reload();
    }

    private void SetMapControlsBusy(bool busy)
    {
        _mapActionBusy = busy;
        SetEditorBusy(_editorBusy);
    }

    private async void MapPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_closed || _updatingPicker || _mapActionBusy
            || MapPicker.SelectedItem is not CatalogMap requested
            || requested.Map.Id == _selectedMap?.Map.Id) return;

        SetMapControlsBusy(true);
        try
        {
            var selected = await _mapCatalog.SelectAsync(requested.Map.Id);
            if (!_closed) await ActivateMapAsync(selected);
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                MapStatus.Text = "Apertura rifiutata: " + ex.Message;
                if (_selectedMap is not null) await RefreshMapPickerAsync(_selectedMap.Map.Id);
            }
        }
        finally { if (!_closed) SetMapControlsBusy(false); }
    }

    private async void NewMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy) return;
        SetMapControlsBusy(true);
        try
        {
            var created = await _mapCatalog.CreateAsync(MapNameInput.Text);
            var selected = await _mapCatalog.SelectAsync(created.Map.Id);
            if (!_closed) await ActivateMapAsync(selected);
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                MapStatus.Text = "Creazione rifiutata: " + ex.Message;
                if (_selectedMap is not null) await RefreshMapPickerAsync(_selectedMap.Map.Id);
            }
        }
        finally { if (!_closed) SetMapControlsBusy(false); }
    }

    private async void RenameMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy || _selectedMap is null) return;
        SetMapControlsBusy(true);
        try
        {
            var updated = await _mapCatalog.RenameAsync(
                _selectedMap.Map.Id, _selectedMap.Map.Revision, MapNameInput.Text);
            if (!_closed) await ActivateMapAsync(updated);
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                MapStatus.Text = "Rinomina rifiutata: " + ex.Message;
                if (_selectedMap is not null) await RefreshMapPickerAsync(_selectedMap.Map.Id);
            }
        }
        finally { if (!_closed) SetMapControlsBusy(false); }
    }
}
