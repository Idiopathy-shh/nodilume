using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;
using Nodilume.Desktop;

internal static class LocalEditTests
{
    public static async Task RunAsync()
    {
        await EditingAndHistoryAsync();
        await RollbackAndMigrationAsync();
        await ViewStateAsync();
        await CacheAndSelectionAsync();
    }

    private static async Task EditingAndHistoryAsync()
    {
        await using var temp = new TempDatabase("graph05-edits");
        await using var store = new SqliteMapStore(temp.Path);
        await DemoMapInitializer.EnsureAsync(store);
        var original = await store.LoadGraphAsync();
        var map = original.Map;
        var duplicates = original.Placements.Values.GroupBy(x => x.IdeaId)
            .First(x => x.Count() > 1).ToArray();
        var item = duplicates.First(x => !x.IsPinned);
        var other = duplicates.First(x => x.Id != item.Id);
        var move = new LocalEditCommand("move-1", map.Id, map.Revision, "move",
            item.Id, item.X + 17, item.Y - 11, item.Z + 5);
        var moved = await store.ApplyLocalEditAsync(move);
        Check.True(moved.Changed && moved.CanUndo && !moved.CanRedo, "Move/history missing.");
        Check.Equal(map.Revision + 1, moved.Revision, "Move revision mismatch.");
        Check.Equal(other, (await store.ReadPlacementAsync(map.Id, other.Id))!, "Other representation moved.");
        var replay = await store.ApplyLocalEditAsync(move);
        Check.True(replay.Replay && replay.CanUndo, "Receipt did not preserve history status.");
        await Check.ThrowsAsync<DomainRuleException>(
            () => store.ApplyLocalEditAsync(move with { X = 999 }), "Reused ID accepted different payload.");
        await Check.ThrowsAsync<StaleMapRevisionException>(
            () => store.ApplyLocalEditAsync(move with { RequestId = "stale" }), "Stale edit accepted.");
        var pin = await store.ApplyLocalEditAsync(new("pin", map.Id, moved.Revision, "pin", item.Id, IsPinned: true));
        replay = await store.ApplyLocalEditAsync(move);
        Check.Equal(pin.Revision, replay.Revision, "Old receipt regressed authoritative revision.");
        await Check.ThrowsAsync<DomainRuleException>(
            () => store.ApplyLocalEditAsync(new("pinned", map.Id, pin.Revision, "move", item.Id, 1,2,3)),
            "Pinned move accepted.");
        var noOp = await store.ApplyLocalEditAsync(new("noop", map.Id, pin.Revision, "pin", item.Id, IsPinned: true));
        Check.True(!noOp.Changed && noOp.Revision == pin.Revision, "No-op advanced revision.");
        var unpin = await store.ApplyLocalEditAsync(new("undo-pin", map.Id, pin.Revision, "undo"));
        Check.True(unpin.CanRedo && !unpin.Placement!.IsPinned, "Undo pin failed.");
        var undo = await store.ApplyLocalEditAsync(new("undo-move", map.Id, unpin.Revision, "undo"));
        Check.Equal(item, undo.Placement!, "Undo did not restore exact local placement.");
        Check.True(!undo.CanUndo && undo.CanRedo, "Unexpected extra history from no-op.");
        await using (var reopened = new SqliteMapStore(temp.Path))
        {
            await reopened.InitializeAsync();
            var redo = await reopened.ApplyLocalEditAsync(new("redo-move", map.Id, undo.Revision, "redo"));
            Check.Equal(moved.Placement!, redo.Placement!, "Persistent redo lost coordinates.");
            var fresh = await reopened.ApplyLocalEditAsync(new("fresh", map.Id, redo.Revision, "move",
                item.Id, 70,80,90));
            Check.True(!fresh.CanRedo, "New edit did not discard redo branch.");
        }

        var current = (await store.GetMapAsync())!;
        var group = original.Placements.Values.First(p => !p.IsPinned &&
            original.Placements.Values.Any(c => c.ParentId == p.Id));
        var children = original.Placements.Values.Where(p => p.ParentId == group.Id).ToArray();
        await store.ApplyLocalEditAsync(new("move-group", map.Id, current.Revision, "move",
            group.Id, group.X + 100, group.Y + 100, group.Z));
        foreach (var child in children.Where(c => c.Id != item.Id))
            Check.Equal(child, (await store.ReadPlacementAsync(map.Id, child.Id))!, "Group move rewrote descendants.");

        current = (await store.GetMapAsync())!;
        await Check.ThrowsAsync<DomainRuleException>(() => store.ApplyLocalEditAsync(
            new("foreign", new MapId(Guid.NewGuid()), current.Revision, "pin", item.Id, IsPinned:false)),
            "Foreign map edit accepted.");
        await Check.ThrowsAsync<DomainRuleException>(() => store.ApplyLocalEditAsync(
            new("nan", map.Id, current.Revision, "move", item.Id, double.NaN,0,0)), "NaN accepted.");
        Check.Equal(current.Revision, (await store.GetMapAsync())!.Revision, "Rejected edit changed map.");
        for (var i=0; i<105; i++)
        {
            current = (await store.GetMapAsync())!;
            await store.ApplyLocalEditAsync(new("bound-"+i, map.Id, current.Revision, "move", item.Id, i+1000,0,0));
        }
        await using var connection = new SqliteConnection($"Data Source={temp.Path}");
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM graph_edits";
        Check.Equal(100L, Convert.ToInt64(await count.ExecuteScalarAsync()), "History is unbounded.");
    }

    private static async Task RollbackAndMigrationAsync()
    {
        await using var temp = new TempDatabase("graph05-migration");
        await using var store = new SqliteMapStore(temp.Path);
        await DemoMapInitializer.EnsureAsync(store);
        var graph = await store.LoadGraphAsync();
        await using (var connection = new SqliteConnection($"Data Source={temp.Path}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP TABLE graph_edits; DROP TABLE edit_cursor; DROP TABLE edit_receipts; DROP TABLE view_state; UPDATE schema_info SET version=2;";
            await cmd.ExecuteNonQueryAsync();
        }
        await store.InitializeAsync();
        var after = await store.LoadGraphAsync();
        Check.Equal(graph.Map, after.Map, "Migration changed graph identity/revision.");
        foreach (var p in graph.Placements.Values)
            Check.Equal(p, after.Placements[p.Id], "Migration changed placement.");
        var item = graph.Placements.Values.First(p => !p.IsPinned);
        await using (var connection = new SqliteConnection($"Data Source={temp.Path}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TRIGGER fail_graph05 BEFORE UPDATE ON placements BEGIN SELECT RAISE(ABORT,'injected write failure'); END;";
            await cmd.ExecuteNonQueryAsync();
        }
        await Check.ThrowsAsync<SqliteException>(() => store.ApplyLocalEditAsync(
            new("rollback", graph.Map.Id, graph.Map.Revision, "move", item.Id, 901,902,903)), "Injected failure was not raised.");
        Check.Equal(graph.Map.Revision, (await store.GetMapAsync())!.Revision, "Rollback advanced revision.");
        Check.Equal(item, (await store.ReadPlacementAsync(graph.Map.Id, item.Id))!, "Rollback left partial edit.");
        var status = await store.ReadEditHistoryStatusAsync(graph.Map.Id);
        Check.True(!status.CanUndo && !status.CanRedo, "Rollback left history.");
    }

    private static async Task CacheAndSelectionAsync()
    {
        await using var temp = new TempDatabase("graph05-cache");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());
        var map = (await store.GetMapAsync())!;
        var children = await store.ReadChildrenPageAsync(map.Id, Graph03Fixture.Root, 3);
        var selected = children.Items.Last();
        var limits = new SceneProjectionLimits(ChildLimit: 1);
        var cache = new SceneProjectionCache();
        var scene = new SceneService(store, cache);
        var before = await scene.LoadProjectionAsync("before", Graph03Fixture.Root,
            limits, preserveSelectionPlacementId: selected.Id);
        Check.True(before.Nodes.Any(n => n.PlacementId == selected.Id.ToString()),
            "Selection outside the first page was omitted.");
        var edit = await store.ApplyLocalEditAsync(new("cache-move", map.Id, map.Revision,
            "move", selected.Id, selected.X + 9, selected.Y, selected.Z));
        cache.RetainMapRevision(map.Id.ToString(), edit.Revision);
        var after = await scene.LoadProjectionAsync("after", Graph03Fixture.Root,
            limits, preserveSelectionPlacementId: selected.Id);
        Check.Equal(edit.Revision, after.Revision, "Cache returned an old revision.");
        Check.Equal(selected.X + 9, after.Nodes.Single(n => n.PlacementId == selected.Id.ToString()).LocalX,
            "Cache returned the previous position.");
        var undo = await store.ApplyLocalEditAsync(new("cache-undo", map.Id, edit.Revision, "undo"));
        cache.RetainMapRevision(map.Id.ToString(), undo.Revision);
        var restored = await scene.LoadProjectionAsync("restored", Graph03Fixture.Root,
            limits, preserveSelectionPlacementId: selected.Id);
        Check.Equal(selected.X, restored.Nodes.Single(n => n.PlacementId == selected.Id.ToString()).LocalX,
            "Undo did not invalidate the cached position.");
    }

    private static async Task ViewStateAsync()
    {
        await using var temp = new TempDatabase("graph05-view");
        await using var store = new SqliteMapStore(temp.Path);
        await DemoMapInitializer.EnsureAsync(store);
        var graph = await store.LoadGraphAsync();
        var root = graph.Placements.Values.First(p => p.ParentId is null);
        var child = graph.Placements.Values.First(p => p.ParentId == root.Id);
        var state = new PersistedViewState([root.Id.ToString()], [10,20,100], [0,0,0],
            child.Id.ToString(), null, [0,1,0]);
        await store.SaveViewStateAsync(graph.Map.Id, state);
        Check.Equal(graph.Map.Revision, (await store.GetMapAsync())!.Revision, "Camera invalidated graph revision.");
        var resolved = await ViewStateResolver.ResolveAsync(store, graph.Map.Id);
        Check.True(resolved.Context == root.Id && resolved.Selection == child.Id &&
            resolved.CameraState is not null, "Valid view did not resolve.");
        await store.SaveViewStateAsync(graph.Map.Id, state with { Path = [root.Id.ToString(), Guid.NewGuid().ToString()] });
        resolved = await ViewStateResolver.ResolveAsync(store, graph.Map.Id);
        Check.True(resolved.Context == root.Id && resolved.CameraState is null, "Missing context did not fallback.");
        await Check.ThrowsAsync<DomainRuleException>(() => store.SaveViewStateAsync(graph.Map.Id,
            state with { Camera = [double.NaN, 0, 100] }), "Nonfinite view accepted.");
        await using var connection = new SqliteConnection($"Data Source={temp.Path}");
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE view_state SET payload='{broken'";
        await cmd.ExecuteNonQueryAsync();
        resolved = await ViewStateResolver.ResolveAsync(store, graph.Map.Id);
        Check.True(resolved.Context is null && resolved.CameraState is null, "Malformed view prevented fallback.");
    }
}
