using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

public static class DemoMapInitializer
{
    public static async Task EnsureAsync(IMapStore store, CancellationToken cancellationToken = default)
    {
        await store.InitializeAsync(cancellationToken);
        if (await store.GetMapAsync(cancellationToken) is null)
            await store.CreateMapAsync(DemoMapFactory.Create(), cancellationToken);
    }
}

public sealed class SceneService(IMapStore store, SceneProjectionCache? cache = null)
{
    private static readonly string[] BranchColors =
        ["#65d8bf", "#8caafa", "#db9aca", "#7fd3ff", "#efaa7a"];

    public async Task<SceneProjection> LoadProjectionAsync(
        string requestId,
        PlacementId? contextPlacementId = null,
        SceneProjectionLimits? limits = null,
        PlacementId? afterPlacementId = null,
        PlacementId? preserveSelectionPlacementId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128)
            throw new ArgumentException("A bounded requestId is required.", nameof(requestId));
        limits ??= new SceneProjectionLimits();
        limits.Validate();

        var map = await store.GetMapAsync(cancellationToken)
            ?? throw new InvalidOperationException("Map is not initialized.");
        var cacheKey = new SceneProjectionCacheKey(
            map.Id.ToString(),
            map.Revision,
            contextPlacementId?.ToString(),
            afterPlacementId?.ToString(),
            preserveSelectionPlacementId?.ToString(),
            limits);
        if (cache?.TryGet(cacheKey, out var cachedProjection) == true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return cachedProjection with
            {
                RequestId = requestId,
                TransferSentUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        SceneProjection CacheResult(SceneProjection projection)
        {
            cache?.Set(cacheKey, projection);
            return projection;
        }

        var partialReasons = new HashSet<string>(StringComparer.Ordinal);
        var reserveSelection = preserveSelectionPlacementId is null ? 0 : 1;
        var rootLimit = Math.Max(1, Math.Min(limits.RootLimit, limits.NodeBudget - reserveSelection));
        var rootCursor = contextPlacementId is null ? afterPlacementId : null;
        var roots = await store.ReadChildrenPageAsync(
            map.Id, null, rootLimit, rootCursor, cancellationToken);
        if (roots.HasMore) partialReasons.Add("root-limit");
        if (rootCursor is not null) partialReasons.Add("root-page");

        if (roots.Items.Count == 0)
        {
            if (rootCursor is not null)
                throw new InvalidOperationException("Requested root page cursor has no data.");
            return CacheResult(new SceneProjection(
                2, "projection", requestId, map.Id.ToString(), map.Revision,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "empty",
                null, null, new SceneVector(0, 0, 0), [],
                new ScenePageInfo(null, null, false, false, 0),
                [], [], [], 0,
                partialReasons.OrderBy(x => x, StringComparer.Ordinal).ToArray()));
        }

        IReadOnlyList<Placement> path;
        if (contextPlacementId is null)
        {
            path = roots.Items.Count == 1 && !roots.HasMore && rootCursor is null
                ? [roots.Items[0]]
                : [];
        }
        else
        {
            path = await store.ReadAncestorPathAsync(
                map.Id, contextPlacementId.Value, limits.AncestorDepthLimit, cancellationToken);
            if (path.Count == 0 || path[^1].Id != contextPlacementId.Value)
                throw new InvalidOperationException("Requested context placement does not exist.");
            if (path[0].ParentId is not null)
                throw new InvalidDataException("Ancestor depth limit reached before the root.");
        }

        var visible = new Dictionary<PlacementId, Placement>();
        foreach (var root in roots.Items) visible[root.Id] = root;
        foreach (var item in path) visible[item.Id] = item;
        var context = path.Count == 0 ? null : path[^1];

        Placement? preservedSelection = null;
        if (preserveSelectionPlacementId is { } selectionId && !visible.ContainsKey(selectionId))
        {
            var candidate = await store.ReadPlacementAsync(map.Id, selectionId, cancellationToken);
            var belongsToActivePage = candidate is not null
                && (context is null ? candidate.ParentId is null : candidate.ParentId == context.Id);
            if (belongsToActivePage) preservedSelection = candidate;
        }

        PlacementPage? activePage = context is null ? roots : null;
        var childrenByParent = new Dictionary<PlacementId, IReadOnlyList<Placement>>();
        if (context is not null)
        {
            var available = Math.Max(1, limits.NodeBudget - visible.Count - (preservedSelection is null ? 0 : 1));
            var take = Math.Min(limits.ChildLimit, available);
            activePage = await store.ReadChildrenPageAsync(
                map.Id, context.Id, take, afterPlacementId, cancellationToken);
            childrenByParent[context.Id] = activePage.Items;
            if (activePage.HasMore) partialReasons.Add($"child-limit:{context.Id}");
            if (afterPlacementId is not null) partialReasons.Add($"child-page:{context.Id}");
            if (take < limits.ChildLimit && activePage.HasMore) partialReasons.Add("node-budget");
            foreach (var child in activePage.Items) visible[child.Id] = child;
        }

        if (preservedSelection is not null && visible.Count < limits.NodeBudget)
            visible[preservedSelection.Id] = preservedSelection;

        foreach (var item in path.Where(x => context is null || x.Id != context.Id).Reverse())
        {
            var remaining = limits.NodeBudget - visible.Count;
            if (remaining <= 0)
            {
                partialReasons.Add("node-budget");
                break;
            }
            var take = Math.Min(limits.ChildLimit, remaining);
            var children = await store.ReadChildrenPageAsync(
                map.Id, item.Id, take, null, cancellationToken);
            childrenByParent[item.Id] = children.Items;
            if (children.HasMore) partialReasons.Add($"child-limit:{item.Id}");
            foreach (var child in children.Items) visible[child.Id] = child;
        }

        if (visible.Count > limits.NodeBudget)
        {
            partialReasons.Add("node-budget");
            foreach (var id in visible.Keys
                .Where(id => !path.Any(p => p.Id == id))
                .Skip(limits.NodeBudget - path.Count)
                .ToArray())
                visible.Remove(id);
        }

        activePage ??= new PlacementPage([], false, null);
        var activeChildren = activePage.Items;
        var pageInfo = new ScenePageInfo(
            afterPlacementId?.ToString(),
            activePage.NextCursor?.ToString(),
            afterPlacementId is not null,
            activePage.HasMore,
            activePage.Items.Count);

        var childCounts = await store.ReadChildCountsAsync(
            map.Id, visible.Keys.ToArray(), cancellationToken);
        var visibleIdeaIds = visible.Values.Select(x => x.IdeaId).Distinct().ToArray();
        var visibleIdeas = (await store.ReadIdeasAsync(map.Id, visibleIdeaIds, cancellationToken))
            .ToDictionary(x => x.Id);
        if (visibleIdeas.Count != visibleIdeaIds.Length)
            throw new InvalidDataException("Projection references an idea that could not be loaded.");

        var globalPositions = new Dictionary<PlacementId, Vector3Value>();
        Vector3Value ResolveGlobal(Placement placement)
        {
            if (globalPositions.TryGetValue(placement.Id, out var cached)) return cached;
            var local = new Vector3Value(placement.X, placement.Y, placement.Z);
            if (placement.ParentId is null)
                return globalPositions[placement.Id] = local;
            if (!visible.TryGetValue(placement.ParentId.Value, out var parent))
                throw new InvalidDataException("Visible projection is missing a required ancestor.");
            return globalPositions[placement.Id] = ResolveGlobal(parent) + local;
        }
        var depths = new Dictionary<PlacementId, int>();
        int ResolveDepth(Placement placement)
        {
            if (depths.TryGetValue(placement.Id, out var cached)) return cached;
            if (placement.ParentId is null) return depths[placement.Id] = 0;
            if (!visible.TryGetValue(placement.ParentId.Value, out var parent))
                throw new InvalidDataException("Visible projection is missing a required parent.");
            return depths[placement.Id] = ResolveDepth(parent) + 1;
        }

        var frame = context is null ? Vector3Value.Zero : ResolveGlobal(context);
        var pathIds = path.Select(x => x.Id).ToHashSet();
        if (visible.Count > limits.LabelBudget) partialReasons.Add("label-budget");
        var nodes = visible.Values
            .OrderBy(ResolveDepth)
            .ThenBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .Select((placement, index) =>
            {
                var position = ResolveGlobal(placement) - frame;
                return new SceneNode(
                    placement.Id.ToString(),
                    placement.Id.ToString(),
                    placement.IdeaId.ToString(),
                    placement.ParentId?.ToString(),
                    visibleIdeas[placement.IdeaId].Title,
                    position.X, position.Y, position.Z,
                    ResolveColor(placement, visible),
                    ResolveDepth(placement),
                    ResolveRole(placement, context, pathIds),
                    index < limits.LabelBudget,
                    childCounts.TryGetValue(placement.Id, out var count) && count > 0,
                    childCounts.GetValueOrDefault(placement.Id, 0));
            })
            .ToArray();

        var links = new List<SceneLink>();
        foreach (var placement in visible.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal))
        {
            if (placement.ParentId is not { } parentId || !visible.ContainsKey(parentId)) continue;
            links.Add(new SceneLink(
                $"containment:{parentId}:{placement.Id}",
                "containment",
                parentId.ToString(),
                placement.Id.ToString(),
                "containment",
                false,
                1,
                []));
        }

        var relationProjection = await BuildRelationProjectionAsync(
            map.Id,
            visible,
            limits,
            partialReasons,
            cancellationToken);
        links.AddRange(relationProjection.Links);
        if (links.Count > limits.LinkBudget)
        {
            partialReasons.Add("link-budget");
            links.RemoveRange(limits.LinkBudget, links.Count - limits.LinkBudget);
        }

        var pathItems = path
            .Select((placement, depth) => new SceneContextItem(
                placement.Id.ToString(),
                placement.IdeaId.ToString(),
                visibleIdeas[placement.IdeaId].Title,
                depth))
            .ToArray();

        var state = activeChildren.Count == 0
            ? context is null ? "empty" : "leaf"
            : partialReasons.Count > 0 ? "partial" : "ready";
        return CacheResult(new SceneProjection(
            2,
            "projection",
            requestId,
            map.Id.ToString(),
            map.Revision,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            state,
            context?.Id.ToString(),
            context?.ParentId?.ToString(),
            new SceneVector(frame.X, frame.Y, frame.Z),
            pathItems,
            pageInfo,
            nodes,
            links,
            relationProjection.Relations,
            relationProjection.HiddenInternalCount,
            partialReasons.OrderBy(x => x, StringComparer.Ordinal).ToArray()));
    }

    private async Task<RelationProjectionResult> BuildRelationProjectionAsync(
        MapId mapId,
        IReadOnlyDictionary<PlacementId, Placement> visible,
        SceneProjectionLimits limits,
        HashSet<string> partialReasons,
        CancellationToken cancellationToken)
    {
        var relationResult = await store.ReadRelationPageAsync(
            mapId, limits.RelationLimit, null, cancellationToken);
        if (relationResult.HasMore) partialReasons.Add("relation-page");
        if (relationResult.Items.Count == 0)
            return new RelationProjectionResult([], [], 0);

        var endpointIdeaIds = relationResult.Items
            .SelectMany(x => new[] { x.SourceIdeaId, x.TargetIdeaId })
            .Distinct()
            .ToArray();
        var ideaById = (await store.ReadIdeasAsync(mapId, endpointIdeaIds, cancellationToken))
            .ToDictionary(x => x.Id);

        var destinationResult = await store.ReadPlacementsForIdeasAsync(
            mapId, endpointIdeaIds, limits.DestinationPlacementLimit, cancellationToken);
        if (destinationResult.HasMore) partialReasons.Add("destination-placement-limit");
        var placementsByIdea = destinationResult.Items
            .GroupBy(x => x.IdeaId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<Placement>)x.OrderBy(p => p.Id.ToString(), StringComparer.Ordinal).ToArray());

        var candidatePaths = new Dictionary<PlacementId, IReadOnlyList<Placement>>();
        var pathIdeaIds = new HashSet<IdeaId>(endpointIdeaIds);
        foreach (var placement in destinationResult.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidatePath = TryBuildPathFromScope(placement, visible);
            if (candidatePath is null)
            {
                candidatePath = await store.ReadAncestorPathAsync(
                    mapId, placement.Id, limits.AncestorDepthLimit, cancellationToken);
            }
            if (candidatePath.Count == 0)
                throw new InvalidDataException("A relation destination disappeared during projection.");
            if (candidatePath[0].ParentId is not null)
                partialReasons.Add("destination-ancestor-depth-limit");
            candidatePaths[placement.Id] = candidatePath;
            foreach (var item in candidatePath) pathIdeaIds.Add(item.IdeaId);
        }

        var pathIdeas = await store.ReadIdeasAsync(mapId, pathIdeaIds.ToArray(), cancellationToken);
        foreach (var idea in pathIdeas) ideaById[idea.Id] = idea;

        var resolutions = new Dictionary<IdeaId, SceneEndpointResolution>();
        SceneEndpointResolution ResolveEndpoint(IdeaId ideaId)
        {
            if (resolutions.TryGetValue(ideaId, out var cached)) return cached;
            var title = ideaById.TryGetValue(ideaId, out var idea) ? idea.Title : "(idea non caricata)";
            var placements = placementsByIdea.GetValueOrDefault(ideaId, []);
            if (placements.Count == 0)
            {
                return resolutions[ideaId] = new SceneEndpointResolution(
                    ideaId.ToString(), title, "unplaced", null, []);
            }

            var candidates = new List<SceneDestinationCandidate>(placements.Count);
            var visibleRepresentatives = new HashSet<PlacementId>();
            foreach (var placement in placements)
            {
                if (!candidatePaths.TryGetValue(placement.Id, out var candidatePath)) continue;
                var visibleRepresentative = FindDeepestVisible(candidatePath, visible);
                if (visibleRepresentative is { } rep) visibleRepresentatives.Add(rep);
                var pathLabel = string.Join(
                    " / ",
                    candidatePath.Select(x => ideaById.TryGetValue(x.IdeaId, out var pathIdea)
                        ? pathIdea.Title
                        : "(idea non caricata)"));
                candidates.Add(new SceneDestinationCandidate(
                    placement.Id.ToString(),
                    (placement.ParentId ?? placement.Id).ToString(),
                    placement.IdeaId.ToString(),
                    title,
                    pathLabel));
            }

            var status = placements.Count > 1
                ? "ambiguous"
                : visibleRepresentatives.Count == 1 ? "visible" : "external";
            var visibleId = visibleRepresentatives.Count == 1
                ? visibleRepresentatives.Single().ToString()
                : null;
            return resolutions[ideaId] = new SceneEndpointResolution(
                ideaId.ToString(), title, status, visibleId, candidates);
        }

        var aggregation = new Dictionary<RelationKey, List<string>>();
        var navigations = new List<SceneRelationNavigation>(relationResult.Items.Count);
        var hiddenInternal = 0;
        foreach (var relation in relationResult.Items)
        {
            var source = ResolveEndpoint(relation.SourceIdeaId);
            var target = ResolveEndpoint(relation.TargetIdeaId);
            navigations.Add(new SceneRelationNavigation(
                relation.Id.ToString(),
                relation.Kind,
                relation.IsDirected,
                relation.Explanation,
                source,
                target));

            if (source.VisiblePlacementId is null || target.VisiblePlacementId is null) continue;
            var sourceId = source.VisiblePlacementId;
            var targetId = target.VisiblePlacementId;
            if (sourceId == targetId)
            {
                hiddenInternal++;
                continue;
            }

            if (!relation.IsDirected && string.CompareOrdinal(sourceId, targetId) > 0)
                (sourceId, targetId) = (targetId, sourceId);
            var key = new RelationKey(sourceId, targetId, relation.Kind, relation.IsDirected);
            if (!aggregation.TryGetValue(key, out var relationIds))
                aggregation[key] = relationIds = [];
            relationIds.Add(relation.Id.ToString());
        }

        var links = aggregation
            .OrderBy(x => x.Key.Source, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Target, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Kind, StringComparer.Ordinal)
            .ThenBy(x => x.Key.IsDirected)
            .Select(x => new SceneLink(
                $"relation:{x.Key.Source}:{x.Key.Target}:{x.Key.Kind}:{(x.Key.IsDirected ? "d" : "u")}",
                "relation",
                x.Key.Source,
                x.Key.Target,
                x.Key.Kind,
                x.Key.IsDirected,
                x.Value.Count,
                x.Value.OrderBy(id => id, StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new RelationProjectionResult(
            links,
            navigations.OrderBy(x => x.RelationId, StringComparer.Ordinal).ToArray(),
            hiddenInternal);
    }
    private static IReadOnlyList<Placement>? TryBuildPathFromScope(
        Placement placement,
        IReadOnlyDictionary<PlacementId, Placement> scope)
    {
        var reverse = new List<Placement> { placement };
        var current = placement;
        var guard = 0;
        while (current.ParentId is { } parentId)
        {
            if (++guard > 256) throw new InvalidDataException("Placement ancestry is cyclic.");
            if (!scope.TryGetValue(parentId, out var parent)) return null;
            reverse.Add(parent);
            current = parent;
        }
        reverse.Reverse();
        return reverse;
    }

    private static PlacementId? FindDeepestVisible(
        IReadOnlyList<Placement> path,
        IReadOnlyDictionary<PlacementId, Placement> visible)
    {
        for (var index = path.Count - 1; index >= 0; index--)
            if (visible.ContainsKey(path[index].Id))
                return path[index].Id;
        return null;
    }

    private static string ResolveRole(
        Placement placement,
        Placement? context,
        IReadOnlySet<PlacementId> pathIds)
    {
        if (context is not null && placement.Id == context.Id) return "context";
        if (pathIds.Contains(placement.Id)) return "ancestor";
        if (context is not null && placement.ParentId == context.Id) return "child";
        if (placement.ParentId is null) return "root";
        return "sibling";
    }
    private static string ResolveColor(
        Placement placement,
        IReadOnlyDictionary<PlacementId, Placement> visible)
    {
        if (placement.ParentId is null) return "#f4c676";
        var branch = placement;
        while (branch.ParentId is { } parentId
            && visible.TryGetValue(parentId, out var parent)
            && parent.ParentId is not null)
        {
            branch = parent;
        }

        var bytes = branch.Id.Value.ToByteArray();
        var hash = 17;
        foreach (var value in bytes) hash = unchecked(hash * 31 + value);
        return BranchColors[(hash & int.MaxValue) % BranchColors.Length];
    }

    private readonly record struct RelationKey(
        string Source,
        string Target,
        string Kind,
        bool IsDirected);

    private sealed record RelationProjectionResult(
        IReadOnlyList<SceneLink> Links,
        IReadOnlyList<SceneRelationNavigation> Relations,
        int HiddenInternalCount);

    private readonly record struct Vector3Value(double X, double Y, double Z)
    {
        public static Vector3Value Zero => new(0, 0, 0);
        public static Vector3Value operator +(Vector3Value left, Vector3Value right) =>
            new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        public static Vector3Value operator -(Vector3Value left, Vector3Value right) =>
            new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}
