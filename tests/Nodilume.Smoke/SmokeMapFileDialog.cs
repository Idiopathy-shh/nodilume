using Nodilume.Desktop;

internal sealed class SmokeMapFileDialog : IMapFileDialog
{
    public string? ImportPath { get; set; }
    public string? ExportPath { get; set; }
    public string? SuggestedFileName { get; private set; }

    public string? ChooseImportPath() => ImportPath;

    public string? ChooseExportPath(string suggestedFileName)
    {
        SuggestedFileName = suggestedFileName;
        return ExportPath;
    }
}
