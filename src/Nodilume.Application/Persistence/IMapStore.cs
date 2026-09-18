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
    Task<IReadOnlyList<Idea>> ReadIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ids,
        CancellationToken cancellationToken = default);    Task<IReadOnlyList<Relation>> ReadRelationsForIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default);
}

public abstract record MapChange;
public sealed record CreateIdeaWithPlacementChange(Idea Idea, Placement Placement) : MapChange;
public sealed record UpdateIdeaChange(Idea Idea) : MapChange;
public sealed record MovePlacementChange(Placement Placement) : MapChange;
public sealed record RemovePlacementChange(PlacementId PlacementId) : MapChange;
public sealed record AddRelationChange(Relation Relation) : MapChange;

public sealed class StaleMapRevisionException(long expectedRevision)
    : InvalidOperationException($"Map revision {expectedRevision} is stale.");

public sealed class UnsupportedSchemaVersionException(int version, int current)
    : InvalidOperationException($"Database schema version {version} is newer than supported version {current}.");