using System.IO;
using System.Windows;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private async void BackupMapButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy || _selectedMap is null) return;
        var selected = _selectedMap;
        SetMapControlsBusy(true);
        try
        {
            var backup = await _mapCatalog.CreateBackupAsync(
                selected.Map.Id);
            if (!_closed)
            {
                MapStatus.Text = backup.RetentionWarning is null
                    ? "Backup creato: "
                        + Path.GetFileName(backup.FilePath)
                        + " (retention "
                        + Nodilume.Infrastructure.MapCatalog
                            .DefaultBackupRetentionCount
                        + ")."
                    : "Backup creato, ma retention incompleta: "
                        + backup.RetentionWarning;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
                MapStatus.Text = "Backup rifiutato: " + ex.Message;
        }
        finally
        {
            if (!_closed) SetMapControlsBusy(false);
        }
    }
    private async void RestoreBackupButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy) return;
        var source = _mapFileDialog.ChooseBackupPath(
            _mapCatalog.BackupDirectory);
        if (source is null)
        {
            MapStatus.Text = "Ripristino annullato.";
            return;
        }

        SetMapControlsBusy(true);
        try
        {
            var restored =
                await _mapCatalog.RestoreBackupAsync(source);
            var selected =
                await _mapCatalog.SelectAsync(restored.Map.Id);
            if (!_closed)
            {
                await ActivateMapAsync(selected);
                MapStatus.Text = "Ripristinata nuova mappa: "
                    + selected.Map.Title;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                if (_selectedMap is not null)
                    await RefreshMapPickerAsync(
                        _selectedMap.Map.Id);
                MapStatus.Text =
                    "Ripristino rifiutato: " + ex.Message;
            }
        }
        finally
        {
            if (!_closed) SetMapControlsBusy(false);
        }
    }
}