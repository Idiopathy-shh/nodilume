using System.IO;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

public sealed record IdeaSearchRepresentation(
    PlacementId PlacementId,
    PlacementId? OpenContextPlacementId,
    string Path,
    bool IsPathPartial);

public sealed record IdeaSearchResult(
    IdeaId IdeaId,
    string Title,
    string Content,
    IReadOnlyList<IdeaSearchRepresentation> Representations);

public sealed record MapIdeaSearchPage(
    IReadOnlyList<IdeaSearchResult> Items,
    bool HasMoreIdeas,
    IdeaSearchCursor? NextCursor,
    bool RepresentationsPartial);

/// <summary>Bounded indexed Idea search with explicit Placement destinations.</summary>
public sealed class MapSearchService(IMapStore store)
{
    public static string Prefix(string? prefix)
    {
        var normalized = prefix?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160
            || normalized.Any(char.IsControl))
            throw new ArgumentException(
                "Search prefix must contain 1–160 printable characters.", nameof(prefix));
        return normalized;
    }

    public async Task<MapIdeaSearchPage> SearchAsync(
        MapId mapId,
        string prefix,
        int ideaLimit = 20,
        int placementLimit = 64,
        int ancestorDepthLimit = 128,
        IdeaSearchCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (ideaLimit is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(ideaLimit));
        if (placementLimit is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(placementLimit));
        if (ancestorDepthLimit is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(ancestorDepthLimit));

        var normalized = Prefix(prefix);
        var ideas = await store.SearchIdeasByTitlePrefixAsync(
            mapId, normalized, ideaLimit, after, cancellationToken);
        if (ideas.Items.Count == 0)
            return new MapIdeaSearchPage([], ideas.HasMore, ideas.NextCursor, false);

        var placements = await store.ReadPlacementsForIdeasAsync(
            mapId, ideas.Items.Select(x => x.Id).ToArray(), placementLimit, cancellationToken);
        var paths = new Dictionary<PlacementId, IReadOnlyList<Placement>>();
        var pathIdeaIds = new HashSet<IdeaId>(ideas.Items.Select(x => x.Id));
        var pathPartial = false;
        foreach (var placement in placements.Items)
        {
            var path = await store.ReadAncestorPathAsync(
                mapId, placement.Id, ancestorDepthLimit, cancellationToken);
            if (path.Count == 0)
                throw new InvalidDataException("A search representation disappeared.");
            paths[placement.Id] = path;
            pathPartial |= path[0].ParentId is not null;
            foreach (var node in path) pathIdeaIds.Add(node.IdeaId);
        }

        var titles = (await store.ReadIdeasAsync(mapId, pathIdeaIds.ToArray(), cancellationToken))
            .ToDictionary(x => x.Id, x => x.Title);
        var byIdea = placements.Items.GroupBy(x => x.IdeaId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var results = ideas.Items.Select(idea =>
        {
            var representations = byIdea.GetValueOrDefault(idea.Id, [])
                .Select(placement =>
                {
                    var path = paths[placement.Id];
                    var label = string.Join(" / ", path.Select(x =>
                        titles.GetValueOrDefault(x.IdeaId) ?? "(idea non caricata)"));
                    var partial = path[0].ParentId is not null;
                    return new IdeaSearchRepresentation(
                        placement.Id, placement.ParentId,
                        (partial ? "… / " : "") + label, partial);
                })
                .OrderBy(x => x.Path, StringComparer.Ordinal)
                .ThenBy(x => x.PlacementId.ToString(), StringComparer.Ordinal)
                .ToArray();
            return new IdeaSearchResult(
                idea.Id, idea.Title, idea.Content, representations);
        }).ToArray();

        return new MapIdeaSearchPage(
            results, ideas.HasMore, ideas.NextCursor,
            placements.HasMore || pathPartial);
    }
}