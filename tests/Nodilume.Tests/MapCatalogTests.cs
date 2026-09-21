using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class MapCatalogTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("legacy");
        await using (var seed = new SqliteMapStore(temp.Path))
            await DemoMapInitializer.EnsureAsync(seed);
        var catalog = new MapCatalog(temp.Root, temp.Path);
        var initial = await catalog.ListAsync();
        Check.Equal(1, initial.Count, "Legacy demo was not discovered.");
        var legacy = initial[0];
        Check.Equal(temp.Path, legacy.DatabasePath, "Legacy database moved or renamed.");
        Check.Equal(legacy.Map.Id, (await catalog.RestoreSelectionAsync())!.Map.Id,
            "Legacy fallback was not selected.");

        await Check.ThrowsAsync<ArgumentException>(() => catalog.CreateAsync("  "),
            "Empty title was accepted.");
        await Check.ThrowsAsync<ArgumentException>(() => catalog.CreateAsync(new string('a', 121)),
            "Overlong title was accepted.");
        await Check.ThrowsAsync<ArgumentException>(() => catalog.CreateAsync("newline\nmap"),
            "Control character was accepted.");
        Check.Equal(1, (await catalog.ListAsync()).Count, "Rejected creation wrote a database.");

        var first = await catalog.CreateAsync("  Prima mappa 🗺️ ");
        Check.Equal("Prima mappa 🗺️", first.Map.Title, "Title was not trimmed.");
        Check.True(first.Map.Id != legacy.Map.Id && first.Map.Revision == 0,
            "New map reused legacy identity or nonzero revision.");
        await using (var store = new SqliteMapStore(first.DatabasePath))
        {
            var graph = await store.LoadGraphAsync();
            Check.True(graph.Ideas.Count == 0 && graph.Placements.Count == 0
                && graph.Relations.Count == 0, "New map was seeded with demo content.");
        }

        var second = await catalog.CreateAsync("Seconda");
        Check.Equal(3, (await catalog.ListAsync()).Count, "Catalog lost one of the databases.");
        await catalog.SelectAsync(first.Map.Id);
        var reopened = new MapCatalog(temp.Root, temp.Path);
        Check.Equal(first.Map.Id, (await reopened.RestoreSelectionAsync())!.Map.Id,
            "Selected map did not survive catalog reopen.");

        var renamed = await reopened.RenameAsync(first.Map.Id, first.Map.Revision, "Rinominata 🌱");
        Check.Equal("Rinominata 🌱", renamed.Map.Title, "Rename title mismatch.");
        Check.Equal(first.Map.Revision + 1, renamed.Map.Revision,
            "Rename did not advance revision.");
        var noOp = await reopened.RenameAsync(first.Map.Id, renamed.Map.Revision, "Rinominata 🌱");
        Check.Equal(renamed.Map.Revision, noOp.Map.Revision, "No-op rename advanced revision.");
        await Check.ThrowsAsync<StaleMapRevisionException>(
            () => reopened.RenameAsync(first.Map.Id, first.Map.Revision, "Stale"),
            "Stale rename was accepted.");
        await Check.ThrowsAsync<ArgumentException>(
            () => reopened.RenameAsync(first.Map.Id, noOp.Map.Revision, " "),
            "Invalid rename was accepted.");

        await using (var store = new SqliteMapStore(first.DatabasePath))
        {
            var graph = await store.LoadGraphAsync();
            Check.Equal("Rinominata 🌱", graph.Map.Title, "Rename did not persist.");
            Check.True(graph.Placements.Count == 0, "Rename inserted unrelated content.");
        }
        await using (var store = new SqliteMapStore(second.DatabasePath))
        {
            var graph = await store.LoadGraphAsync();
            Check.Equal("Seconda", graph.Map.Title, "Renaming one map changed another.");
            Check.Equal(0L, graph.Map.Revision, "Renaming one map changed another revision.");
        }
        await using (var store = new SqliteMapStore(temp.Path))
        {
            var graph = await store.LoadGraphAsync();
            Check.Equal(25, graph.Placements.Count,
                "Creating/renaming maps changed the legacy demo.");
        }

        await reopened.SelectAsync(second.Map.Id);
        Check.Equal(second.Map.Id, (await reopened.RestoreSelectionAsync())!.Map.Id,
            "Switching to another map was not persisted.");
        await Check.ThrowsAsync<FileNotFoundException>(() => reopened.SelectAsync(MapId.New()),
            "Unknown map was selected.");

        var unrelated = Path.Combine(temp.Root, "private-export.sqlite");
        await File.WriteAllTextAsync(unrelated, "not a map");
        Check.Equal(3, (await reopened.ListAsync()).Count,
            "Catalog inspected an unrelated non-GUID file.");
        // Missing or malformed selected ID must fall back to existing legacy map.
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "active-map.txt"), MapId.New().ToString());
        Check.Equal(legacy.Map.Id, (await reopened.RestoreSelectionAsync())!.Map.Id,
            "Missing selected map did not fall back to the legacy map.");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "active-map.txt"), "../elsewhere");
        Check.Equal(legacy.Map.Id, (await reopened.RestoreSelectionAsync())!.Map.Id,
            "Malformed selection was treated as a filesystem path.");
    }
}
