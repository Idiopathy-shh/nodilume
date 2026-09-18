using Nodilume.Core;

internal static class DomainTests
{
    public static Task RunAsync()
    {
        SharedIdeaKeepsLocalPlacementState();
        ContainmentRejectsCyclesAndRepeatedIdeas();
        CrossMapReferencesAreRejected();
        ConceptRelationsMayCycleAndLeafRemovalIsLocal();
        NonFiniteCoordinatesDoNotMutateState();
        return Task.CompletedTask;
    }

    private static MapGraph NewGraph(int id = 1)
        => new(new MapInfo(TestIds.Map(id), $"Map {id}", 0, 1));

    private static void SharedIdeaKeepsLocalPlacementState()
    {
        var graph = NewGraph();
        var rootIdea = graph.AddIdea(TestIds.Idea(1), "Root", "root");
        var root = graph.AddPlacement(TestIds.Placement(1), rootIdea.Id, null, 0, 0, 0);
        var shared = graph.AddIdea(TestIds.Idea(2), "Shared", "before");
        var left = graph.AddPlacement(TestIds.Placement(2), shared.Id, null, 1, 2, 3, annotation: "left note");
        var right = graph.AddPlacement(TestIds.Placement(3), shared.Id, root.Id, 4, 5, 6, true, "right note");

        graph.UpdateIdea(shared.Id, "Shared", "after");
        Check.Equal("after", graph.Ideas[left.IdeaId].Content, "Shared content did not update.");
        Check.Equal(left.IdeaId, right.IdeaId, "Representations must share one idea identity.");
        Check.Equal("left note", graph.Placements[left.Id].Annotation, "Left annotation changed unexpectedly.");
        Check.Equal("right note", graph.Placements[right.Id].Annotation, "Right annotation changed unexpectedly.");
        Check.True(graph.Placements[right.Id].IsPinned, "Placement-local pin state was lost.");
    }

    private static void ContainmentRejectsCyclesAndRepeatedIdeas()
    {
        var graph = NewGraph(2);
        var a = graph.AddIdea(TestIds.Idea(10), "A", "");
        var b = graph.AddIdea(TestIds.Idea(11), "B", "");
        var d = graph.AddIdea(TestIds.Idea(12), "D", "");
        var e = graph.AddIdea(TestIds.Idea(13), "E", "");
        var pa = graph.AddPlacement(TestIds.Placement(10), a.Id, null, 0, 0, 0);
        var pb = graph.AddPlacement(TestIds.Placement(11), b.Id, pa.Id, 1, 0, 0);
        var pd = graph.AddPlacement(TestIds.Placement(12), d.Id, null, 0, 1, 0);
        graph.AddPlacement(TestIds.Placement(13), a.Id, pd.Id, 0, 0, 1);
        var pe = graph.AddPlacement(TestIds.Placement(14), e.Id, null, 0, 0, 2);

        Check.Throws<DomainRuleException>(() => graph.MovePlacement(pa.Id, pb.Id, 0, 0, 0), "Cycle move must fail.");
        Check.Equal<PlacementId?>(null, graph.Placements[pa.Id].ParentId, "Failed cycle move mutated the graph.");
        Check.Throws<DomainRuleException>(() => graph.MovePlacement(pe.Id, pe.Id, 0, 0, 0), "Self-parent must fail.");
        Check.Equal<PlacementId?>(null, graph.Placements[pe.Id].ParentId, "Failed self-parent mutated the graph.");
        Check.Throws<DomainRuleException>(
            () => graph.MovePlacement(pe.Id, TestIds.Placement(99999), 0, 0, 0),
            "Missing parent must fail.");
        Check.Equal<PlacementId?>(null, graph.Placements[pe.Id].ParentId, "Missing-parent rejection mutated the graph.");
        Check.Throws<DomainRuleException>(() => graph.MovePlacement(pd.Id, pb.Id, 0, 0, 0), "Subtree repeated idea must fail.");
        Check.Equal<PlacementId?>(null, graph.Placements[pd.Id].ParentId, "Failed subtree move mutated the graph.");
        graph.MovePlacement(pe.Id, pb.Id, 2, 2, 2);
        Check.Equal<PlacementId?>(pb.Id, graph.Placements[pe.Id].ParentId, "Valid subtree move failed.");

        Check.Throws<DomainRuleException>(
            () => graph.AddPlacement(TestIds.Placement(15), a.Id, pb.Id, 0, 0, 0),
            "Repeated ancestor idea must fail.");
        Check.Throws<DomainRuleException>(() => graph.RemoveLeafPlacement(pb.Id), "Parent removal must be explicit.");
    }

    private static void CrossMapReferencesAreRejected()
    {
        var first = NewGraph(3);
        var second = NewGraph(4);
        var firstIdea = first.AddIdea(TestIds.Idea(20), "First", "");
        var secondIdea = second.AddIdea(TestIds.Idea(21), "Second", "");
        first.AddPlacement(TestIds.Placement(20), firstIdea.Id, null, 0, 0, 0);
        second.AddPlacement(TestIds.Placement(21), secondIdea.Id, null, 0, 0, 0);

        Check.Throws<DomainRuleException>(
            () => first.AddPlacement(TestIds.Placement(22), secondIdea.Id, null, 0, 0, 0),
            "Cross-map idea reference must fail.");
        Check.Throws<DomainRuleException>(
            () => first.AddRelation(TestIds.Relation(20), firstIdea.Id, secondIdea.Id, "cross", true, ""),
            "Cross-map relation must fail.");

        var foreignPlacement = new Placement(
            TestIds.Placement(23), second.Map.Id, firstIdea.Id, null, 0, 0, 0, false, "");
        Check.Throws<DomainRuleException>(
            () => _ = new MapGraph(first.Map, first.Ideas.Values, [foreignPlacement], []),
            "Loaded cross-map placement must fail.");
    }

    private static void ConceptRelationsMayCycleAndLeafRemovalIsLocal()
    {
        var graph = NewGraph(5);
        var a = graph.AddIdea(TestIds.Idea(30), "A", "");
        var b = graph.AddIdea(TestIds.Idea(31), "B", "");
        var pa = graph.AddPlacement(TestIds.Placement(30), a.Id, null, 0, 0, 0);
        var pb = graph.AddPlacement(TestIds.Placement(31), b.Id, null, 1, 1, 1);
        graph.AddRelation(TestIds.Relation(30), a.Id, b.Id, "related", true, "");
        graph.AddRelation(TestIds.Relation(31), b.Id, a.Id, "related", true, "");

        graph.RemoveLeafPlacement(pb.Id);
        Check.True(graph.Ideas.ContainsKey(b.Id), "Removing a representation deleted its idea.");
        Check.Equal(2, graph.Relations.Count, "Removing a representation deleted conceptual relations.");
        Check.True(graph.Placements.ContainsKey(pa.Id), "Unrelated representation changed.");
    }

    private static void NonFiniteCoordinatesDoNotMutateState()
    {
        var graph = NewGraph(6);
        var idea = graph.AddIdea(TestIds.Idea(40), "Finite", "");
        var placement = graph.AddPlacement(TestIds.Placement(40), idea.Id, null, 1, 2, 3);
        var before = graph.Placements[placement.Id];

        Check.Throws<DomainRuleException>(
            () => graph.MovePlacement(placement.Id, null, double.NaN, 2, 3),
            "NaN coordinate must fail.");
        Check.Equal(before, graph.Placements[placement.Id], "Rejected coordinate mutation changed state.");
        Check.Throws<DomainRuleException>(
            () => graph.AddPlacement(TestIds.Placement(41), idea.Id, null, 0, double.PositiveInfinity, 0),
            "Infinite coordinate must fail.");
        Check.Equal(1, graph.Placements.Count, "Rejected placement was partially added.");
    }
}