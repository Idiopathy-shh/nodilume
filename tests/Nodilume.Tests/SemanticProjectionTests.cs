using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class SemanticProjectionTests
{
    public static async Task RunAsync()
    {
        await ContextUsesRealAncestorPathWhenChildrenArePartialAsync();
        await RelationsAggregateAndRedistributeAsync();
        await MultiplePlacementsAndUnplacedIdeasStayExplicitAsync();
        await LeafIsNotTreatedAsEmptyGroupAsync();
        await MissingContextFailsExplicitlyAsync();
        await AncestorDepthExhaustionFailsClosedAsync();
    }

    private static async Task ContextUsesRealAncestorPathWhenChildrenArePartialAsync()
    {
        await using var temp = new TempDatabase("graph03-context");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());

        var projection = await new SceneService(store).LoadProjectionAsync(
            "partial-context",
            Graph03Fixture.GroupA1,
            new SceneProjectionLimits(ChildLimit: 1));

        Check.Equal("partial", projection.State, "Limited children must be reported as partial.");
        Check.Equal(3, projection.Path.Count, "Ancestor path was truncated by the child page.");
        Check.Equal(Graph03Fixture.Root.ToString(), projection.Path[0].PlacementId,
            "A child page must not manufacture a false root.");
        Check.Equal(Graph03Fixture.GroupA1.ToString(), projection.ContextPlacementId!,
            "Requested context was not retained.");
        Check.True(projection.PartialReasons.Any(x => x.StartsWith("child-limit:", StringComparison.Ordinal)),
            "Child limit exhaustion was not disclosed.");
    }

    private static async Task RelationsAggregateAndRedistributeAsync()
    {
        await using var temp = new TempDatabase("graph03-relations");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());
        var service = new SceneService(store);

        var root = await service.LoadProjectionAsync("root");
        var groupA = root.Nodes.Single(x => x.Title == "Gruppo A");
        var groupB = root.Nodes.Single(x => x.Title == "Gruppo B");
        var forward = root.Links.Single(x =>
            x.Category == "relation"
            && x.Source == groupA.Id
            && x.Target == groupB.Id
            && x.Kind == "cross"
            && x.IsDirected);
        Check.Equal(2, forward.Count, "Collapsed A→B relations were not aggregated.");
        Check.Equal(2, forward.RelationIds.Distinct().Count(), "Aggregated provenance was duplicated or lost.");

        var reverse = root.Links.Single(x =>
            x.Category == "relation"
            && x.Source == groupB.Id
            && x.Target == groupA.Id
            && x.Kind == "cross"
            && x.IsDirected);
        Check.Equal(1, reverse.Count, "Opposite directed relation was merged with A→B.");
        Check.True(root.Links.All(x => x.Category != "relation" || x.Source != x.Target),
            "Internal relation produced a false self-link.");
        Check.True(root.HiddenInternalRelationCount >= 1,
            "Internal relations were not accounted for while collapsed.");
        var expanded = await service.LoadProjectionAsync("expanded", Graph03Fixture.GroupA1);
        var relationIds = expanded.Links
            .Where(x => x.Category == "relation")
            .SelectMany(x => x.RelationIds)
            .ToArray();
        Check.Equal(1, relationIds.Count(x => x == Graph03Fixture.RelationForward1.ToString()),
            "First relation was lost or double counted after expansion.");
        Check.Equal(1, relationIds.Count(x => x == Graph03Fixture.RelationForward2.ToString()),
            "Second relation was lost or double counted after expansion.");
    }

    private static async Task MultiplePlacementsAndUnplacedIdeasStayExplicitAsync()
    {
        await using var temp = new TempDatabase("graph03-destinations");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());

        var projection = await new SceneService(store).LoadProjectionAsync("destinations");
        var ambiguous = projection.Relations.Single(x =>
            x.RelationId == Graph03Fixture.RelationAmbiguous.ToString());
        Check.Equal("ambiguous", ambiguous.Source.Status,
            "Multiple placements were silently resolved.");
        Check.Equal(2, ambiguous.Source.Candidates.Count,
            "All placement choices must be surfaced.");
        Check.True(ambiguous.Source.Candidates.Any(x => x.Path.Contains("Gruppo A", StringComparison.Ordinal)),
            "First ambiguous path is not understandable.");
        Check.True(ambiguous.Source.Candidates.Any(x => x.Path.Contains("Gruppo B", StringComparison.Ordinal)),
            "Second ambiguous path is not understandable.");

        var unplaced = projection.Relations.Single(x =>
            x.RelationId == Graph03Fixture.RelationUnplaced.ToString());
        Check.Equal("unplaced", unplaced.Target.Status,
            "Idea without Placement was presented as navigable.");
        Check.Equal(0, unplaced.Target.Candidates.Count,
            "Unplaced Idea unexpectedly has a destination.");
    }
    private static async Task LeafIsNotTreatedAsEmptyGroupAsync()
    {
        await using var temp = new TempDatabase("graph03-leaf");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());

        var projection = await new SceneService(store).LoadProjectionAsync(
            "leaf",
            Graph03Fixture.LeafA1);
        var leaf = projection.Nodes.Single(x => x.PlacementId == Graph03Fixture.LeafA1.ToString());
        Check.True(!leaf.HasChildren, "Leaf advertises semantic expansion.");
        Check.Equal(0, leaf.DirectChildCount, "Leaf has an invented child count.");
        Check.Equal("leaf", projection.State,
            "A leaf must be explicit and not masquerade as an empty group.");
    }

    private static async Task MissingContextFailsExplicitlyAsync()
    {
        await using var temp = new TempDatabase("graph03-missing-context");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());

        await Check.ThrowsAsync<InvalidOperationException>(
            () => new SceneService(store).LoadProjectionAsync(
                "missing-context",
                TestIds.Placement(999999)),
            "A missing context must fail explicitly instead of rendering an empty graph.");
    }

    private static async Task AncestorDepthExhaustionFailsClosedAsync()
    {
        await using var temp = new TempDatabase("graph03-depth");
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(Graph03Fixture.Create());

        await Check.ThrowsAsync<InvalidDataException>(
            () => new SceneService(store).LoadProjectionAsync(
                "too-shallow",
                Graph03Fixture.GroupA1,
                new SceneProjectionLimits(AncestorDepthLimit: 2)),
            "Incomplete ancestry must not be accepted as a new root.");
    }
}

internal static class Graph03Fixture
{
    public static PlacementId Root => TestIds.Placement(900);
    public static PlacementId GroupA => TestIds.Placement(901);
    public static PlacementId GroupB => TestIds.Placement(902);
    public static PlacementId GroupC => TestIds.Placement(903);
    public static PlacementId GroupA1 => TestIds.Placement(904);
    public static PlacementId LeafA1 => TestIds.Placement(905);
    public static PlacementId LeafA2 => TestIds.Placement(906);
    public static PlacementId GroupB1 => TestIds.Placement(907);
    public static PlacementId LeafB1 => TestIds.Placement(908);
    public static PlacementId DuplicateA => TestIds.Placement(909);
    public static PlacementId DuplicateB => TestIds.Placement(910);

    public static RelationId RelationForward1 => TestIds.Relation(950);
    public static RelationId RelationForward2 => TestIds.Relation(951);
    public static RelationId RelationReverse => TestIds.Relation(952);
    public static RelationId RelationInternal => TestIds.Relation(953);
    public static RelationId RelationAmbiguous => TestIds.Relation(954);
    public static RelationId RelationUnplaced => TestIds.Relation(955);

    public static MapGraph Create()
    {
        var graph = new MapGraph(new MapInfo(TestIds.Map(900), "GRAPH.03 fixture", 0, 1));
        var root = Add(graph, 900, "Radice");
        var a = Add(graph, 901, "Gruppo A");
        var b = Add(graph, 902, "Gruppo B");
        var c = Add(graph, 903, "Gruppo C");
        var a1 = Add(graph, 904, "Sottogruppo A1");
        var leafA1 = Add(graph, 905, "Foglia A1");
        var leafA2 = Add(graph, 906, "Foglia A2");
        var b1 = Add(graph, 907, "Sottogruppo B1");
        var leafB1 = Add(graph, 908, "Foglia B1");
        var duplicate = Add(graph, 909, "Idea multipla");
        var unplaced = Add(graph, 911, "Idea non collocata");
        graph.AddPlacement(Root, root.Id, null, 0, 0, 0);
        graph.AddPlacement(GroupA, a.Id, Root, -120, 0, 0);
        graph.AddPlacement(GroupB, b.Id, Root, 120, 0, 0);
        graph.AddPlacement(GroupC, c.Id, Root, 0, 110, 0);
        graph.AddPlacement(GroupA1, a1.Id, GroupA, -15, 0, 0);
        graph.AddPlacement(LeafA1, leafA1.Id, GroupA1, -35, 20, 0);
        graph.AddPlacement(LeafA2, leafA2.Id, GroupA1, 35, -20, 0);
        graph.AddPlacement(GroupB1, b1.Id, GroupB, 0, 0, 0);
        graph.AddPlacement(LeafB1, leafB1.Id, GroupB1, 25, 10, 0);
        graph.AddPlacement(DuplicateA, duplicate.Id, GroupA1, 0, 45, 0);
        graph.AddPlacement(DuplicateB, duplicate.Id, GroupB, 0, -55, 0);

        graph.AddRelation(RelationForward1, leafA1.Id, leafB1.Id, "cross", true, "A1 verso B1");
        graph.AddRelation(RelationForward2, leafA2.Id, leafB1.Id, "cross", true, "A2 verso B1");
        graph.AddRelation(RelationReverse, leafB1.Id, leafA1.Id, "cross", true, "B1 verso A1");
        graph.AddRelation(RelationInternal, leafA1.Id, leafA2.Id, "internal", false, "interna ad A");
        graph.AddRelation(RelationAmbiguous, duplicate.Id, leafB1.Id, "reference", true, "destinazione multipla");
        graph.AddRelation(RelationUnplaced, leafA1.Id, unplaced.Id, "reference", true, "idea non collocata");
        return graph;
    }

    private static Idea Add(MapGraph graph, int id, string title) =>
        graph.AddIdea(TestIds.Idea(id), title, $"Contenuto {title}");
}
