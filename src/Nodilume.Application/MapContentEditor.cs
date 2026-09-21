using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

/// <summary>Bounded interactive edits against the authoritative map revision.
/// No global LoadGraph: each edit touches only its referenced Idea/Placement.</summary>
public sealed class MapContentEditor(IMapStore store)
{
    public static string Title(string? title)
    {
        var value = title?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160
            || value.Any(char.IsControl))
            throw new ArgumentException("Idea title must contain 1–160 printable characters.", nameof(title));
        return value;
    }

    public static string Content(string? content)
    {
        if (content is null || content.Length > 200_000)
            throw new ArgumentException("Idea content exceeds the 200000-character limit.", nameof(content));
        return content;
    }

    public static string Annotation(string? annotation)
    {
        if (annotation is null || annotation.Length > 4000)
            throw new ArgumentException("Local annotation exceeds the 4000-character limit.", nameof(annotation));
        return annotation;
    }

    public async Task<(Idea Idea, Placement Placement, long Revision)> CreateIdeaAsync(
        MapId mapId, long expectedRevision, string title, string content,
        PlacementId? parentId, CancellationToken cancellationToken = default)
    {
        var validTitle = Title(title);
        var validContent = Content(content);
        await AssertParentAsync(mapId, parentId, null, cancellationToken);
        var idea = new Idea(IdeaId.New(), mapId, validTitle, validContent);
        var placementId = PlacementId.New();
        var (x, y, z) = InitialCoordinates(placementId);
        var placement = new Placement(placementId, mapId, idea.Id, parentId,
            x, y, z, false, "");
        var revision = await store.ApplyAsync(mapId, expectedRevision,
            new CreateIdeaWithPlacementChange(idea, placement), cancellationToken);
        return (idea, placement, revision);
    }

    public async Task<(Placement Placement, long Revision)> CreateRepresentationAsync(
        MapId mapId, long expectedRevision, IdeaId ideaId, PlacementId? parentId,
        string annotation = "", CancellationToken cancellationToken = default)
    {
        var validAnnotation = Annotation(annotation);
        var exists = await store.ReadIdeasAsync(mapId, [ideaId], cancellationToken);
        if (exists.Count != 1) throw new DomainRuleException("Idea does not exist in this map.");
        await AssertParentAsync(mapId, parentId, ideaId, cancellationToken);
        var placementId = PlacementId.New();
        var (x, y, z) = InitialCoordinates(placementId);
        var placement = new Placement(placementId, mapId, ideaId, parentId,
            x, y, z, false, validAnnotation);
        var revision = await store.ApplyAsync(mapId, expectedRevision,
            new AddPlacementChange(placement), cancellationToken);
        return (placement, revision);
    }

    public async Task<long> UpdateIdeaAsync(MapId mapId, long expectedRevision,
        IdeaId ideaId, string title, string content, CancellationToken cancellationToken = default)
    {
        var validTitle = Title(title);
        var validContent = Content(content);
        var idea = (await store.ReadIdeasAsync(mapId, [ideaId], cancellationToken))
            .SingleOrDefault() ?? throw new DomainRuleException("Idea not found in this map.");
        if (idea.Title == validTitle && idea.Content == validContent)
        {
            await AssertRevisionAsync(mapId, expectedRevision, cancellationToken);
            return expectedRevision;
        }
        return await store.ApplyAsync(mapId, expectedRevision,
            new UpdateIdeaChange(idea with { Title = validTitle, Content = validContent }), cancellationToken);
    }

    public async Task<long> UpdateAnnotationAsync(MapId mapId, long expectedRevision,
        PlacementId placementId, string annotation, CancellationToken cancellationToken = default)
    {
        var valid = Annotation(annotation);
        var current = await store.ReadPlacementAsync(mapId, placementId, cancellationToken)
            ?? throw new DomainRuleException("Placement not found in this map.");
        if (current.Annotation == valid)
        {
            await AssertRevisionAsync(mapId, expectedRevision, cancellationToken);
            return expectedRevision;
        }
        return await store.ApplyAsync(mapId, expectedRevision,
            new UpdatePlacementDetailsChange(current with { Annotation = valid }), cancellationToken);
    }

    private async Task AssertRevisionAsync(MapId mapId, long expectedRevision,
        CancellationToken cancellationToken)
    {
        var map = await store.GetMapAsync(cancellationToken);
        if (map is null || map.Id != mapId || map.Revision != expectedRevision)
            throw new StaleMapRevisionException(expectedRevision);
    }

    private static (double X, double Y, double Z) InitialCoordinates(PlacementId id)
    {
        var bytes = id.Value.ToByteArray();
        return ((bytes[0] % 11 - 5) * 12.0, (bytes[1] % 11 - 5) * 10.0,
            (bytes[2] % 7 - 3) * 8.0);
    }

    private async Task AssertParentAsync(MapId mapId, PlacementId? parentId,
        IdeaId? childIdeaId, CancellationToken cancellationToken)
    {
        if (parentId is null) return;
        var parent = await store.ReadPlacementAsync(mapId, parentId.Value, cancellationToken)
            ?? throw new DomainRuleException("Parent placement not found in this map.");
        if (childIdeaId is null) return;
        var path = await store.ReadAncestorPathAsync(mapId, parent.Id, 64, cancellationToken);
        if (path.Count == 0 || path[^1].Id != parent.Id || path[0].ParentId is not null)
            throw new DomainRuleException("Parent hierarchy exceeds the supported depth.");
        if (path.Any(x => x.IdeaId == childIdeaId))
            throw new DomainRuleException("The same idea cannot repeat along its ancestor chain.");
    }
}
