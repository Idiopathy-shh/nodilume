using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class SqliteTests
{
    public static async Task RunAsync()
    {
        await RoundTripAndIdempotentInitializationAsync();
        await AtomicRollbackAndStaleRevisionAsync();
        await PaginationIsStableAsync();
        await SelectivePagesAndIndexedSearchAreStableAsync();
        await FutureSchemaDoesNotRewriteDataAsync();
        await SeparateDatabasesStayIndependentAsync();
    }

    private static async Task RoundTripAndIdempotentInitializationAsync()
    {
        await using var temp = new TempDatabase("roundtrip");
        MapGraph before;
        long editedRevision;
        await using (var store = new SqliteMapStore(temp.Path))
        {
            await DemoMapInitializer.EnsureAsync(store);
            before = await store.LoadGraphAsync();
            Check.Equal(25, before.Placements.Count, "Demo placement count mismatch.");
            Check.Equal(24, before.Ideas.Count, "Demo idea count mismatch.");
            Check.Equal(3, before.Relations.Count, "Demo relation count mismatch.");

            var shared = before.Ideas.Values.Single(x => x.Title == "Domande");
            var editor = new MapEditor(store);
            editedRevision = await editor.UpdateIdeaAsync(shared.Id, shared.Title, "persisted shared content");
        }

        await using (var reopened = new SqliteMapStore(temp.Path))
        {
            await reopened.InitializeAsync();
            await DemoMapInitializer.EnsureAsync(reopened);
            var after = await reopened.LoadGraphAsync();
            Check.Equal(before.Map.Id, after.Map.Id, "Map identity changed after reopen.");
            Check.Equal(editedRevision, after.Map.Revision, "Revision was reset by idempotent initialization.");
            Check.Equal("persisted shared content", after.Ideas.Values.Single(x => x.Title == "Domande").Content,
                "Existing content was overwritten by demo initialization.");

            var placementId = before.Placements.Keys.OrderBy(x => x.ToString(), StringComparer.Ordinal).Last();
            Check.Equal(before.Placements[placementId], after.Placements[placementId],
                "Placement identity or local values changed after reopen.");
        }
    }

    private static async Task AtomicRollbackAndStaleRevisionAsync()
    {
        await using var temp = new TempDatabase("atomic");
        await using var store = new SqliteMapStore(temp.Path);
        await DemoMapInitializer.EnsureAsync(store);
        var graph = await store.LoadGraphAsync();
        var revision = graph.Map.Revision;

        var idea = new Idea(TestIds.Idea(500), graph.Map.Id, "Rollback", "must disappear");
        var placement = new Placement(
            TestIds.Placement(500),
            graph.Map.Id,
            idea.Id,
            TestIds.Placement(999999),
            1, 2, 3, false, "invalid parent");

        await Check.ThrowsAsync<SqliteException>(
            () => store.ApplyAsync(
                graph.Map.Id,
                revision,
                new CreateIdeaWithPlacementChange(idea, placement)),
            "Foreign-key failure should abort the transaction.");

        var afterFailure = await store.LoadGraphAsync();
        Check.Equal(revision, afterFailure.Map.Revision, "Failed write advanced revision.");
        Check.True(!afterFailure.Ideas.ContainsKey(idea.Id), "Failed write left a partial idea row.");

        var root = afterFailure.Ideas.Values.Single(x => x.Title == "Idee connesse");
        var editor = new MapEditor(store);
        var committed = await editor.UpdateIdeaAsync(root.Id, root.Title, "committed");
        Check.Equal(revision + 1, committed, "Successful write did not advance revision exactly once.");

        var staleIdea = root with { Content = "stale overwrite" };
        await Check.ThrowsAsync<StaleMapRevisionException>(
            () => store.ApplyAsync(afterFailure.Map.Id, revision, new UpdateIdeaChange(staleIdea)),
            "Stale revision must be rejected.");
        var final = await store.LoadGraphAsync();
        Check.Equal("committed", final.Ideas[root.Id].Content, "Stale write overwrote committed content.");
    }

    private static async Task PaginationIsStableAsync()
    {
        await using var temp = new TempDatabase("pagination");
        await using var store = new SqliteMapStore(temp.Path);
        await DemoMapInitializer.EnsureAsync(store);
        var map = await store.GetMapAsync() ?? throw new InvalidOperationException("Map missing.");

        var all = new List<Placement>();
        PlacementId? after = null;
        while (true)
        {
            var page = await store.ReadPlacementPageAsync(map.Id, 7, after);
            if (page.Count == 0) break;
            all.AddRange(page);
            after = page[^1].Id;
        }

        Check.Equal(25, all.Count, "Pagination skipped placements.");
        Check.Equal(25, all.Select(x => x.Id).Distinct().Count(), "Pagination duplicated placements.");
        var ordered = all.Select(x => x.Id.ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check.True(all.Select(x => x.Id.ToString()).SequenceEqual(ordered), "Pagination order is not stable.");
    }

    private static async Task SelectivePagesAndIndexedSearchAreStableAsync()
    {
        await using var temp = new TempDatabase("graph04-pages");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());
        var map = await store.GetMapAsync() ?? throw new InvalidOperationException("Map missing.");

        var children = new List<Placement>();
        PlacementId? childCursor = null;
        while (true)
        {
            var page = await store.ReadChildrenPageAsync(map.Id, Graph03Fixture.Root, 1, childCursor);
            children.AddRange(page.Items);
            if (!page.HasMore) break;
            Check.True(page.NextCursor is not null, "Child page with more data did not expose a cursor.");
            childCursor = page.NextCursor;
        }
        Check.Equal(3, children.Count, "Child paging lost rows.");
        Check.Equal(3, children.Select(x => x.Id).Distinct().Count(), "Child paging duplicated rows.");
        Check.True(children.Select(x => x.Id.ToString()).SequenceEqual(
            children.Select(x => x.Id.ToString()).OrderBy(x => x, StringComparer.Ordinal)),
            "Child paging order is not stable.");

        var search = new List<Idea>();
        IdeaSearchCursor? searchCursor = null;
        while (true)
        {
            var page = await store.SearchIdeasByTitlePrefixAsync(map.Id, "Gruppo", 2, searchCursor);
            search.AddRange(page.Items);
            if (!page.HasMore) break;
            Check.True(page.NextCursor is not null, "Search page with more data did not expose a cursor.");
            searchCursor = page.NextCursor;
        }
        Check.Equal(3, search.Count, "Indexed prefix search paging lost rows.");
        Check.True(search.All(x => x.Title.StartsWith("Gruppo", StringComparison.Ordinal)),
            "Prefix search returned an unrelated idea.");

        var relations = new List<Relation>();
        RelationId? relationCursor = null;
        while (true)
        {
            var page = await store.ReadRelationPageAsync(map.Id, 2, relationCursor);
            relations.AddRange(page.Items);
            if (!page.HasMore) break;
            Check.True(page.NextCursor is not null, "Relation page with more data did not expose a cursor.");
            relationCursor = page.NextCursor;
        }
        Check.Equal(6, relations.Count, "Relation paging lost rows.");
        Check.Equal(6, relations.Select(x => x.Id).Distinct().Count(), "Relation paging duplicated rows.");

        await using var connection = new SqliteConnection($"Data Source={temp.Path}");
        await connection.OpenAsync();
        await using var index = connection.CreateCommand();
        index.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='ix_relations_map_id';";
        Check.Equal(1L, Convert.ToInt64(await index.ExecuteScalarAsync()),
            "GRAPH.04 relation paging index was not migrated.");
    }

    private static async Task FutureSchemaDoesNotRewriteDataAsync()
    {
        await using var temp = new TempDatabase("future-schema");
        MapId mapId;
        await using (var store = new SqliteMapStore(temp.Path))
        {
            await DemoMapInitializer.EnsureAsync(store);
            mapId = (await store.GetMapAsync() ?? throw new InvalidOperationException()).Id;
        }

        await using (var connection = new SqliteConnection($"Data Source={temp.Path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_info SET version=99 WHERE id=1;";
            await command.ExecuteNonQueryAsync();
        }

        await using (var futureStore = new SqliteMapStore(temp.Path))
            await Check.ThrowsAsync<UnsupportedSchemaVersionException>(
                () => futureStore.InitializeAsync(),
                "Future schema must be rejected.");

        await using (var connection = new SqliteConnection($"Data Source={temp.Path}"))
        {
            await connection.OpenAsync();
            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM placements;";
            Check.Equal(25L, Convert.ToInt64(await count.ExecuteScalarAsync()), "Future-schema rejection lost data.");
            await using var map = connection.CreateCommand();
            map.CommandText = "SELECT id FROM maps;";
            Check.Equal(mapId.ToString(), Convert.ToString(await map.ExecuteScalarAsync())!,
                "Future-schema rejection rewrote map identity.");
        }
    }

    private static async Task SeparateDatabasesStayIndependentAsync()
    {
        await using var firstTemp = new TempDatabase("first");
        await using var secondTemp = new TempDatabase("second");
        var firstGraph = SmallGraph(701, "First");
        var secondGraph = SmallGraph(702, "Second");

        await using var first = new SqliteMapStore(firstTemp.Path);
        await using var second = new SqliteMapStore(secondTemp.Path);
        await first.InitializeAsync();
        await second.InitializeAsync();
        await first.CreateMapAsync(firstGraph);
        await second.CreateMapAsync(secondGraph);

        Check.Equal(firstGraph.Map.Id, (await first.GetMapAsync())!.Id, "First DB map identity mismatch.");
        Check.Equal(secondGraph.Map.Id, (await second.GetMapAsync())!.Id, "Second DB map identity mismatch.");

        var foreignIdea = secondGraph.Ideas.Values.Single();
        var foreignPlacement = secondGraph.Placements.Values.Single();
        await Check.ThrowsAsync<DomainRuleException>(
            () => first.ApplyAsync(
                firstGraph.Map.Id,
                firstGraph.Map.Revision,
                new CreateIdeaWithPlacementChange(foreignIdea, foreignPlacement)),
            "Cross-map persistence change must be rejected.");

        Check.Equal(1, (await first.LoadGraphAsync()).Ideas.Count, "Rejected cross-map write changed first DB.");
        Check.Equal(1, (await second.LoadGraphAsync()).Ideas.Count, "Rejected cross-map write changed second DB.");
    }

    private static MapGraph SmallGraph(int seed, string title)
    {
        var map = new MapGraph(new MapInfo(TestIds.Map(seed), title, 0, 1));
        var idea = map.AddIdea(TestIds.Idea(seed), title, "content");
        map.AddPlacement(TestIds.Placement(seed), idea.Id, null, 0, 0, 0);
        return map;
    }
}