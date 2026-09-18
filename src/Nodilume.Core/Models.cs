namespace Nodilume.Core;

public sealed record MapInfo(
    MapId Id,
    string Title,
    long Revision,
    int SchemaVersion);

public sealed record Idea(
    IdeaId Id,
    MapId MapId,
    string Title,
    string Content);

public sealed record Placement(
    PlacementId Id,
    MapId MapId,
    IdeaId IdeaId,
    PlacementId? ParentId,
    double X,
    double Y,
    double Z,
    bool IsPinned,
    string Annotation);

public sealed record Relation(
    RelationId Id,
    MapId MapId,
    IdeaId SourceIdeaId,
    IdeaId TargetIdeaId,
    string Kind,
    bool IsDirected,
    string Explanation);

public sealed class DomainRuleException(string message) : InvalidOperationException(message);