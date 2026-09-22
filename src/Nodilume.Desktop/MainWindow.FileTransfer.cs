using System.IO;
using System.Windows;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private async void ExportMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy || _selectedMap is null) return;
        var selected = _selectedMap;
        var destination = _mapFileDialog.ChooseExportPath(
            SuggestedExportFileName(selected.Map.Title, selected.Map.Id.ToString()[..8]));
        if (destination is null)
        {
            MapStatus.Text = "Esportazione annullata.";
            return;
        }

        SetMapControlsBusy(true);
        try
        {
            await _mapCatalog.ExportPortableAsync(selected.Map.Id, destination);
            if (!_closed)
                MapStatus.Text = "Mappa esportata: " + Path.GetFileName(destination);
        }
        catch (Exception ex)
        {
            if (!_closed) MapStatus.Text = "Esportazione rifiutata: " + ex.Message;
        }
        finally
        {
            if (!_closed) SetMapControlsBusy(false);
        }
    }

    private async void ImportMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy) return;
        var source = _mapFileDialog.ChooseImportPath();
        if (source is null)
        {
            MapStatus.Text = "Importazione annullata.";
            return;
        }

        SetMapControlsBusy(true);
        try
        {
            var imported = await _mapCatalog.ImportPortableAsync(source);
            var selected = await _mapCatalog.SelectAsync(imported.Map.Id);
            if (!_closed)
            {
                await ActivateMapAsync(selected);
                MapStatus.Text = "Importata nuova mappa: " + selected.Map.Title;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                if (_selectedMap is not null)
                    await RefreshMapPickerAsync(_selectedMap.Map.Id);
                MapStatus.Text = "Importazione rifiutata: " + ex.Message;
            }
        }
        finally
        {
            if (!_closed) SetMapControlsBusy(false);
        }
    }

    private static string SuggestedExportFileName(string title, string shortId)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(title.Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray()).Trim(' ', '.');
        if (safe.Length == 0) safe = "mappa";
        if (safe.Length > 80) safe = safe[..80].TrimEnd(' ', '.');
        return safe + "-" + shortId + ".nodilume.json";
    }
}
