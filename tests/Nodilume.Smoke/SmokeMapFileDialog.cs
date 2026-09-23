using Nodilume.Desktop;

internal sealed class SmokeMapFileDialog : IMapFileDialog
{
    public string? ImportPath { get; set; }
    public string? ExportPath { get; set; }
    public string? BackupPath { get; set; }
    public string? SuggestedFileName { get; private set; }
    public string? RequestedBackupDirectory { get; private set; }

    public string? ChooseImportPath() => ImportPath;

    public string? ChooseExportPath(string suggestedFileName)
    {
        SuggestedFileName = suggestedFileName;
        return ExportPath;
    }

    public string? ChooseBackupPath(string initialDirectory)
    {
        RequestedBackupDirectory = initialDirectory;
        return BackupPath;
    }
}
