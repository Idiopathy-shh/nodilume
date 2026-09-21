using Nodilume.Core;

namespace Nodilume.Application.Persistence;

public interface IMapStore : IAsyncDisposable
{
    string DatabasePath { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<MapInfo?> GetMapAsync(CancellationToken cancellationToken = default);
    Task<MapGraph> LoadGraphAsync(CancellationToken cancellationToken = default);
    Task CreateMapAsync(MapGraph graph, CancellationToken cancellationToken = default);
    Task<long> ApplyAsync(
        MapId mapId,
        long expectedRevision,
        MapChange change,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Placement>> ReadPlacementPageAsync(
        MapId mapId,
        int limit,
        PlacementId? after = null,
        CancellationToken cancellationToken = default);
    Task<Placement?> ReadPlacementAsync(
        MapId mapId,
        PlacementId placementId,
        CancellationToken cancellationToken = default);
    Task<BoundedResult<Placement>> ReadChildrenAsync(
        MapId mapId,
        PlacementId? parentId,
        int limit,
        CancellationToken cancellationToken = default);
    Task<PlacementPage> ReadChildrenPageAsync(
        MapId mapId,
        PlacementId? parentId,
        int limit,
        PlacementId? after = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Placement>> ReadAncestorPathAsync(
        MapId mapId,
        PlacementId placementId,
        int maxDepth,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<PlacementId, int>> ReadChildCountsAsync(
        MapId mapId,
        IReadOnlyCollection<PlacementId> placementIds,
        CancellationToken cancellationToken = default);
    Task<BoundedResult<Placement>> ReadDescendantsAsync(
        MapId mapId,
        IReadOnlyCollection<PlacementId> rootIds,
        int limit,
        CancellationToken cancellationToken = default);
    Task<BoundedResult<Placement>> ReadPlacementsForIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Idea>> ReadIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ids,
        CancellationToken cancellationToken = default);
    Task<IdeaSearchPage> SearchIdeasByTitlePrefixAsync(
        MapId mapId,
        string prefix,
        int limit,
        IdeaSearchCursor? after = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Relation>> ReadRelationsForIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default);
    Task<BoundedResult<Relation>> ReadRelationsTouchingIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default);
    Task<RelationPage> ReadRelationPageAsync(
        MapId mapId,
        int limit,
        RelationId? after = null,
        CancellationToken cancellationToken = default);
}

public sealed record BoundedResult<T>(IReadOnlyList<T> Items, bool HasMore);
public sealed record PlacementPage(
    IReadOnlyList<Placement> Items,
    bool HasMore,
    PlacementId? NextCursor);
public readonly record struct IdeaSearchCursor(string Title, IdeaId Id);
public sealed record IdeaSearchPage(
    IReadOnlyList<Idea> Items,
    bool HasMore,
    IdeaSearchCursor? NextCursor);
public sealed record RelationPage(
    IReadOnlyList<Relation> Items,
    bool HasMore,
    RelationId? NextCursor);

public abstract record MapChange;
public sealed record CreateIdeaWithPlacementChange(Idea Idea, Placement Placement) : MapChange;
public sealed record UpdateIdeaChange(Idea Idea) : MapChange;
public sealed record AddPlacementChange(Placement Placement) : MapChange;
public sealed record UpdatePlacementDetailsChange(Placement Placement) : MapChange;
public sealed record MovePlacementChange(Placement Placement) : MapChange;
public sealed record RemovePlacementChange(PlacementId PlacementId) : MapChange;
public sealed record AddRelationChange(Relation Relation) : MapChange;

public sealed class StaleMapRevisionException(long expectedRevision)
    : InvalidOperationException($"Map revision {expectedRevision} is stale.");

public sealed class UnsupportedSchemaVersionException(int version, int current)
    : InvalidOperationException($"Database schema version {version} is newer than supported version {current}.");
