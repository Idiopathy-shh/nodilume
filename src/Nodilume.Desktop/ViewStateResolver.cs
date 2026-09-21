using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

internal sealed record ResolvedViewState(
    PlacementId? Context, PlacementId? Selection, PlacementId? PageAfter,
    PersistedViewState? CameraState);

internal static class ViewStateResolver
{
    public static async Task<ResolvedViewState> ResolveAsync(
        SqliteMapStore store, MapId mapId, CancellationToken cancellationToken = default)
    {
        var saved = await store.ReadViewStateAsync(mapId, cancellationToken);
        if (saved is null) return new(null, null, null, null);
        if (saved.Up is not null && (saved.Up.Length != 3 ||
            !saved.Up.All(double.IsFinite) || saved.Up.Sum(x => x*x) < 1e-12))
            saved = saved with { Up = null };
        if (saved.Camera is not { Length: 3 } || saved.Target is not { Length: 3 } ||
            !saved.Camera.Concat(saved.Target).All(double.IsFinite) ||
            saved.Path is null || saved.Path.Length > 256)
            return new(null, null, null, null);

        PlacementId? context = null;
        var resolvedPath = new List<string>();
        foreach (var raw in saved.Path)
        {
            if (!Guid.TryParse(raw, out var guid)) break;
            var id = new PlacementId(guid);
            var placement = await store.ReadPlacementAsync(mapId, id, cancellationToken);
            if (placement is null || placement.ParentId != context) break;
            resolvedPath.Add(raw);
            context = id;
        }
        var exact = resolvedPath.Count == saved.Path.Length;
        PlacementId? selection = null;
        if (Guid.TryParse(saved.SelectedPlacementId, out var selectedId))
        {
            var candidate = await store.ReadPlacementAsync(
                mapId, new PlacementId(selectedId), cancellationToken);
            if (candidate?.ParentId == context && candidate is not null)
                selection = candidate.Id;
        }
        PlacementId? pageAfter = null;
        if (Guid.TryParse(saved.PageAfterPlacementId, out var pageId))
        {
            var candidate = await store.ReadPlacementAsync(
                mapId, new PlacementId(pageId), cancellationToken);
            if (candidate is not null && candidate.ParentId == context)
                pageAfter = candidate.Id;
        }
        // Camera is relative to its original context and cannot be applied to a fallback.
        var cameraDistance = Math.Sqrt(saved.Camera.Zip(saved.Target,
            (a, b) => (a - b) * (a - b)).Sum());
        var safeCamera = exact && cameraDistance is >= 3 and <= 1800
            ? saved with { Path = resolvedPath.ToArray() }
            : null;
        return new(context, selection, pageAfter, safeCamera);
    }
}
