namespace Nodilume.Application;

public sealed record SceneVector(double X, double Y, double Z);

public sealed record SceneContextItem(
    string PlacementId,
    string IdeaId,
    string Title,
    int Depth);

public sealed record SceneNode(
    string Id,
    string PlacementId,
    string IdeaId,
    string? ParentPlacementId,
    string Title,
    double X,
    double Y,
    double Z,
    string Color,
    int Depth,
    string Role,
    bool ShowLabel,
    bool HasChildren,
    int DirectChildCount,
    bool IsPinned,
    double LocalX, double LocalY, double LocalZ);

public sealed record SceneLink(
    string Id,
    string Category,
    string Source,
    string Target,
    string Kind,
    bool IsDirected,
    int Count,
    IReadOnlyList<string> RelationIds);

public sealed record SceneDestinationCandidate(
    string PlacementId,
    string? OpenContextPlacementId,
    string IdeaId,
    string Title,
    string Path);

public sealed record SceneEndpointResolution(
    string IdeaId,
    string Title,
    string Status,
    string? VisiblePlacementId,
    IReadOnlyList<SceneDestinationCandidate> Candidates);

public sealed record SceneRelationNavigation(
    string RelationId,
    string Kind,
    bool IsDirected,
    string Explanation,
    SceneEndpointResolution Source,
    SceneEndpointResolution Target);

public sealed record ScenePageInfo(
    string? AfterPlacementId,
    string? NextPlacementId,
    bool HasPrevious,
    bool HasMore,
    int PageItemCount);

public sealed record SceneProjection(
    int Version,
    string Type,
    string RequestId,
    string MapId,
    long Revision,
    long TransferSentUnixMs,
    string State,
    string? ContextPlacementId,
    string? ParentContextPlacementId,
    SceneVector FrameOrigin,
    IReadOnlyList<SceneContextItem> Path,
    ScenePageInfo Page,
    IReadOnlyList<SceneNode> Nodes,
    IReadOnlyList<SceneLink> Links,
    IReadOnlyList<SceneRelationNavigation> Relations,
    int HiddenInternalRelationCount,
    IReadOnlyList<string> PartialReasons);

public sealed record SceneProjectionError(
    int Version,
    string Type,
    string RequestId,
    string? MapId,
    long? Revision,
    string? ContextPlacementId,
    string Code,
    string Message);

public sealed record SceneProjectionLimits(
    int RootLimit = 32,
    int ChildLimit = 128,
    int AncestorDepthLimit = 64,
    int RelationPlacementLimit = 1024,
    int RelationLimit = 256,
    int DestinationPlacementLimit = 512,
    int NodeBudget = 160,
    int LinkBudget = 256,
    int LabelBudget = 64)
{
    public void Validate()
    {
        if (RootLimit is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(RootLimit));
        if (ChildLimit is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(ChildLimit));
        if (AncestorDepthLimit is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(AncestorDepthLimit));
        if (RelationPlacementLimit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(RelationPlacementLimit));
        if (RelationLimit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(RelationLimit));
        if (DestinationPlacementLimit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(DestinationPlacementLimit));
        if (NodeBudget is < 4 or > 1024) throw new ArgumentOutOfRangeException(nameof(NodeBudget));
        if (LinkBudget is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(LinkBudget));
        if (LabelBudget is < 0 or > 1024) throw new ArgumentOutOfRangeException(nameof(LabelBudget));
        if (LabelBudget > NodeBudget) throw new ArgumentOutOfRangeException(nameof(LabelBudget));
    }
}
