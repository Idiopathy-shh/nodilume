using Microsoft.Win32;

namespace Nodilume.Desktop;

public interface IMapFileDialog
{
    string? ChooseImportPath();
    string? ChooseExportPath(string suggestedFileName);
    string? ChooseBackupPath(string initialDirectory);
}

internal sealed class WindowsMapFileDialog : IMapFileDialog
{
    private const string Filter =
        "Mappa Nodilume (*.nodilume.json)|*.nodilume.json|File JSON (*.json)|*.json";

    public string? ChooseImportPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importa mappa Nodilume",
            Filter = Filter,
            CheckFileExists = true,
            Multiselect = false,
            DereferenceLinks = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChooseExportPath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Esporta mappa Nodilume",
            Filter = Filter,
            FileName = suggestedFileName,
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            ValidateNames = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChooseBackupPath(string initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ripristina backup Nodilume",
            Filter = "Backup Nodilume (*.nodilume-backup)|*.nodilume-backup",
            InitialDirectory = initialDirectory,
            CheckFileExists = true,
            Multiselect = false,
            DereferenceLinks = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
