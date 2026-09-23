using Nodilume.Core;

namespace Nodilume.Application;

public sealed record LocalEditCommand(
    string RequestId, MapId MapId, long ExpectedRevision,
    string Action, PlacementId? PlacementId = null,
    double? X = null, double? Y = null, double? Z = null, bool? IsPinned = null);

public sealed record LocalEditOutcome(
    long Revision, bool Changed, bool Replay, Placement? Placement,
    bool CanUndo, bool CanRedo);

public sealed record PersistedViewState(
    string[] Path, double[] Camera, double[] Target,
    string? SelectedPlacementId, string? PageAfterPlacementId, double[]? Up = null);

public interface ILocalEditStore
{
    Task<LocalEditOutcome> ApplyLocalEditAsync(
        LocalEditCommand command, CancellationToken cancellationToken = default);
    Task<PersistedViewState?> ReadViewStateAsync(
        MapId mapId, CancellationToken cancellationToken = default);
    Task SaveViewStateAsync(
        MapId mapId, PersistedViewState state,
        CancellationToken cancellationToken = default);
}
