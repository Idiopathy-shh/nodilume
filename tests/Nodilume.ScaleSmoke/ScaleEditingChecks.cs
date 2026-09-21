using System.IO;
using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Infrastructure.Sqlite;

internal static class ScaleEditingChecks
{
    public static async Task CloneAndVerifyAsync(string source, string destination)
    {
        await using (var input = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = source, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        await using (var output = new SqliteConnection($"Data Source={destination}"))
        {
            await input.OpenAsync();
            await output.OpenAsync();
            input.BackupDatabase(output);
        }
        await using var store = new SqliteMapStore(destination);
        await store.InitializeAsync();
        var map = (await store.GetMapAsync())!;
        await using var connection = new SqliteConnection($"Data Source={destination}");
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM ideas";
        if (Convert.ToInt64(await count.ExecuteScalarAsync()) != 300000)
            throw new Exception("Scale fixture must contain exactly 300000 Idea.");
        var root = (await store.ReadChildrenPageAsync(map.Id,null,1)).Items.Single();
        var child = (await store.ReadChildrenPageAsync(map.Id,root.Id,16)).Items.First(p => !p.IsPinned);
        var cache = new SceneProjectionCache();
        var service = new SceneService(store, cache);
        await service.LoadProjectionAsync("scale-before", root.Id, preserveSelectionPlacementId:child.Id);
        var changed = await store.ApplyLocalEditAsync(new("scale-move",map.Id,map.Revision,
            "move",child.Id,child.X+15,child.Y,child.Z));
        cache.RetainMapRevision(map.Id.ToString(),changed.Revision);
        var after = await service.LoadProjectionAsync("scale-after",root.Id, preserveSelectionPlacementId:child.Id);
        if (after.Nodes.Single(n => n.PlacementId == child.Id.ToString()).LocalX != child.X+15)
            throw new Exception("Scale edit returned a stale cached position.");
        var undo = await store.ApplyLocalEditAsync(new("scale-undo",map.Id,changed.Revision,"undo"));
        if ((await store.ReadPlacementAsync(map.Id,child.Id)) != child)
            throw new Exception("Scale undo did not restore local coordinates.");
        var view = new PersistedViewState([root.Id.ToString()],[10,20,100],[0,0,0],child.Id.ToString(),null);
        await store.SaveViewStateAsync(map.Id,view);
        await using (var reopened = new SqliteMapStore(destination))
        {
            await reopened.InitializeAsync();
            var restored = await reopened.ReadViewStateAsync(map.Id);
            if (restored?.SelectedPlacementId != child.Id.ToString()
                || (await reopened.GetMapAsync())!.Revision != undo.Revision)
                throw new Exception("Scale view round-trip changed revision or lost selection.");
        }
        count.CommandText = "DELETE FROM view_state";
        await count.ExecuteNonQueryAsync(); // Only the disposable clone; keep cold-view measurement comparable.
        Console.WriteLine("PASS: GRAPH.05 300k local edit, undo, cache invalidation and view round-trip on a SQLite backup clone.");
    }
}
