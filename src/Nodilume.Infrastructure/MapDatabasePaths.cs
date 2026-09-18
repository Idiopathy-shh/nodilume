namespace Nodilume.Infrastructure;

public static class MapDatabasePaths
{
    public static string Demo => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nodilume",
        "Maps",
        "demo.sqlite");
}