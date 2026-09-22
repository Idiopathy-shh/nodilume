using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class MapSearchServiceTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("map-search");
        var mapId = TestIds.Map(800);
        var graph = CreateGraph(mapId);
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(graph);
        var search = new MapSearchService(store);

        var first = await search.SearchAsync(mapId, "  Al  ", ideaLimit: 1);
        Check.Equal(1, first.Items.Count, "First page size mismatch.");
        Check.Equal("Alpha", first.Items[0].Title, "Search order is not stable.");
        Check.True(first.HasMoreIdeas && first.NextCursor is not null,
            "Search page did not expose its keyset cursor.");
        Check.Equal(2, first.Items[0].Representations.Count,
            "Shared Idea representations were not explicit.");
        Check.True(first.Items[0].Representations.Select(x => x.Path).SequenceEqual(
            new[] { "Root / Destra / Alpha", "Root / Sinistra / Alpha" }),
            "Representation paths were not complete and sorted.");

        var second = await search.SearchAsync(
            mapId, "Al", ideaLimit: 1, after: first.NextCursor);        Check.Equal(1, second.Items.Count, "Second page size mismatch.");
        Check.Equal("Alpine", second.Items[0].Title, "Cursor skipped the next Idea.");
        Check.True(!second.HasMoreIdeas && second.NextCursor is null,
            "Final search page exposed a spurious cursor.");

        var bounded = await search.SearchAsync(
            mapId, "Alpha", placementLimit: 1);
        Check.Equal(1, bounded.Items[0].Representations.Count,
            "Placement bound was not enforced.");
        Check.True(bounded.RepresentationsPartial,
            "Truncated representations were not disclosed.");

        var shallow = await search.SearchAsync(
            mapId, "Alpha", ancestorDepthLimit: 1);
        Check.True(shallow.RepresentationsPartial
            && shallow.Items[0].Representations.All(x =>
                x.IsPathPartial && x.Path.StartsWith("… / Alpha", StringComparison.Ordinal)),
            "Truncated ancestor paths were not disclosed.");

        var wrongCase = await search.SearchAsync(mapId, "al");
        Check.Equal(0, wrongCase.Items.Count,
            "Indexed prefix search unexpectedly changed ordinal case semantics.");
        await Check.ThrowsAsync<ArgumentException>(
            () => search.SearchAsync(mapId, " "),
            "Blank prefix was accepted.");
        await Check.ThrowsAsync<ArgumentException>(
            () => search.SearchAsync(mapId, "A\nB"),
            "Control character in prefix was accepted.");
        await Check.ThrowsAsync<ArgumentOutOfRangeException>(
            () => search.SearchAsync(mapId, "Al", ideaLimit: 0),
            "Unbounded/empty Idea page was accepted.");
    }

    private static MapGraph CreateGraph(MapId mapId)
    {
        var graph = new MapGraph(new MapInfo(mapId, "Search", 0, 2));
        var rootIdea = TestIds.Idea(801);
        var leftIdea = TestIds.Idea(802);
        var rightIdea = TestIds.Idea(803);
        var alphaIdea = TestIds.Idea(804);
        var alpineIdea = TestIds.Idea(805);        graph.AddIdea(rootIdea, "Root", "");
        graph.AddIdea(leftIdea, "Sinistra", "");
        graph.AddIdea(rightIdea, "Destra", "");
        graph.AddIdea(alphaIdea, "Alpha", "shared content");
        graph.AddIdea(alpineIdea, "Alpine", "next page");

        var root = TestIds.Placement(801);
        var left = TestIds.Placement(802);
        var right = TestIds.Placement(803);
        graph.AddPlacement(root, rootIdea, null, 0, 0, 0);
        graph.AddPlacement(left, leftIdea, root, -1, 0, 0);
        graph.AddPlacement(right, rightIdea, root, 1, 0, 0);
        graph.AddPlacement(TestIds.Placement(804), alphaIdea, left, 0, 0, 0);
        graph.AddPlacement(TestIds.Placement(805), alphaIdea, right, 0, 0, 0);
        graph.AddPlacement(TestIds.Placement(806), alpineIdea, root, 0, 1, 0);
        return graph;
    }
}
