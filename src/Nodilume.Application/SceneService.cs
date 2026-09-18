using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

public sealed record SceneNode(
    string Id,
    string PlacementId,
    string IdeaId,
    string Title,
    double X,
    double Y,
    double Z,
    string Color,
    int Depth);

public sealed record SceneLink(string Source, string Target, string Kind);

public sealed record SceneSnapshot(
    int Version,
    string Type,
    string MapId,
    long Revision,
    IReadOnlyList<SceneNode> Nodes,
    IReadOnlyList<SceneLink> Links);

public static class DemoMapInitializer
{
    public static async Task EnsureAsync(IMapStore store, CancellationToken cancellationToken = default)
    {
        await store.InitializeAsync(cancellationToken);
        if (await store.GetMapAsync(cancellationToken) is null)
            await store.CreateMapAsync(DemoMapFactory.Create(), cancellationToken);
    }
}

public sealed class SceneService(IMapStore store)
{
    private static readonly string[] BranchColors = ["#65d8bf", "#8caafa", "#db9aca", "#7fd3ff", "#efaa7a"];

    public async Task<SceneSnapshot> LoadFirstPageAsync(
        int placementLimit = 128,
        CancellationToken cancellationToken = default)
    {
        if (placementLimit is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(placementLimit));
        var map = await store.GetMapAsync(cancellationToken)
            ?? throw new InvalidOperationException("Map is not initialized.");
        var placements = await store.ReadPlacementPageAsync(map.Id, placementLimit, cancellationToken: cancellationToken);
        var ideaIds = placements.Select(x => x.IdeaId).Distinct().ToArray();
        var ideas = await store.ReadIdeasAsync(map.Id, ideaIds, cancellationToken);
        var relations = await store.ReadRelationsForIdeasAsync(map.Id, ideaIds, placementLimit * 4, cancellationToken);

        var placementById = placements.ToDictionary(x => x.Id);
        var ideaById = ideas.ToDictionary(x => x.Id);
        var childrenOfRoot = placements
            .Where(x => x.ParentId is { } parent && placementById.TryGetValue(parent, out var p) && p.ParentId is null)
            .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .Select(x => x.Id)
            .ToArray();

        var nodes = placements
            .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .Select(x => ProjectNode(x, placementById, ideaById, childrenOfRoot))
            .ToArray();

        var links = new List<SceneLink>();
        foreach (var placement in placements)
            if (placement.ParentId is { } parent && placementById.ContainsKey(parent))
                links.Add(new SceneLink(parent.ToString(), placement.Id.ToString(), "containment"));

        var placementsByIdea = placements
            .GroupBy(x => x.IdeaId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(p => p.Id.ToString(), StringComparer.Ordinal).ToArray());
        foreach (var relation in relations)
        {
            if (!placementsByIdea.TryGetValue(relation.SourceIdeaId, out var sources)
                || !placementsByIdea.TryGetValue(relation.TargetIdeaId, out var targets))
                continue;
            links.Add(new SceneLink(sources[0].Id.ToString(), targets[0].Id.ToString(), "relation"));
        }

        return new SceneSnapshot(1, "scene", map.Id.ToString(), map.Revision, nodes, links);
    }

    private static SceneNode ProjectNode(
        Placement placement,
        IReadOnlyDictionary<PlacementId, Placement> placementById,
        IReadOnlyDictionary<IdeaId, Idea> ideaById,
        IReadOnlyList<PlacementId> rootChildren)
    {
        var (x, y, z, depth, branch) = ResolvePosition(placement, placementById);
        var color = depth == 0
            ? "#f4c676"
            : BranchColors[Math.Max(0, Array.IndexOf(rootChildren.ToArray(), branch)) % BranchColors.Length];
        var title = ideaById.TryGetValue(placement.IdeaId, out var idea) ? idea.Title : "(idea non caricata)";
        return new SceneNode(
            placement.Id.ToString(),
            placement.Id.ToString(),
            placement.IdeaId.ToString(),
            title,
            x,
            y,
            z,
            color,
            depth);
    }

    private static (double X, double Y, double Z, int Depth, PlacementId Branch) ResolvePosition(
        Placement placement,
        IReadOnlyDictionary<PlacementId, Placement> placementById)
    {
        var x = placement.X;
        var y = placement.Y;
        var z = placement.Z;
        var depth = 0;
        var branch = placement.Id;
        var current = placement;
        while (current.ParentId is { } parentId && placementById.TryGetValue(parentId, out var parent))
        {
            depth++;
            branch = current.Id;
            x += parent.X;
            y += parent.Y;
            z += parent.Z;
            current = parent;
        }
        return (x, y, z, depth, branch);
    }
}