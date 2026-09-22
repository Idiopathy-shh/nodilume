using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

/// <summary>Edits conceptual Idea-to-Idea relations within one authoritative map.
/// Changes are atomic and revision-guarded; no global graph materialization.</summary>
public sealed class MapRelationEditor(IMapStore store)
{
    public static string Kind(string? kind)
    {
        var normalized = kind?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 80
            || normalized.Any(char.IsControl))
            throw new ArgumentException("Relation kind must contain 1–80 printable characters.",
                nameof(kind));
        return normalized;
    }

    public static string Explanation(string? explanation)
    {
        if (explanation is null || explanation.Length > 4000)
            throw new ArgumentException("Relation explanation exceeds the 4000-character limit.",
                nameof(explanation));
        return explanation;
    }

    public async Task<(Relation Relation, long Revision)> CreateAsync(
        MapId mapId, long expectedRevision, IdeaId sourceIdeaId, IdeaId targetIdeaId,
        string kind, bool isDirected, string explanation,
        CancellationToken cancellationToken = default)
    {
        var validKind = Kind(kind);
        var validExplanation = Explanation(explanation);
        await VerifyEndpointsAsync(mapId, sourceIdeaId, targetIdeaId, cancellationToken);
        var relation = new Relation(RelationId.New(), mapId, sourceIdeaId,
            targetIdeaId, validKind, isDirected, validExplanation);
        var revision = await store.ApplyAsync(mapId, expectedRevision,
            new AddRelationChange(relation), cancellationToken);
        return (relation, revision);
    }

    public async Task<(Relation Relation, long Revision)> UpdateAsync(
        MapId mapId, long expectedRevision, RelationId relationId,
        IdeaId sourceIdeaId, IdeaId targetIdeaId, string kind, bool isDirected,
        string explanation, CancellationToken cancellationToken = default)
    {
        var validKind = Kind(kind);
        var validExplanation = Explanation(explanation);
        var previous = await store.ReadRelationAsync(mapId, relationId, cancellationToken)
            ?? throw new DomainRuleException("Relation does not exist in this map.");
        await VerifyEndpointsAsync(mapId, sourceIdeaId, targetIdeaId, cancellationToken);
        var relation = previous with { SourceIdeaId = sourceIdeaId, TargetIdeaId = targetIdeaId,
            Kind = validKind, IsDirected = isDirected, Explanation = validExplanation };
        if (relation == previous)
        {
            await AssertRevisionAsync(mapId, expectedRevision, cancellationToken);
            return (previous, expectedRevision);
        }
        var revision = await store.ApplyAsync(mapId, expectedRevision,
            new UpdateRelationChange(relation), cancellationToken);
        return (relation, revision);
    }

    public async Task<long> RemoveAsync(MapId mapId, long expectedRevision, RelationId relationId,
        CancellationToken cancellationToken = default)
    {
        if (await store.ReadRelationAsync(mapId, relationId, cancellationToken) is null)
            throw new DomainRuleException("Relation does not exist in this map.");
        return await store.ApplyAsync(mapId, expectedRevision,
            new RemoveRelationChange(relationId), cancellationToken);
    }

    private async Task VerifyEndpointsAsync(MapId mapId, IdeaId sourceIdeaId,
        IdeaId targetIdeaId, CancellationToken cancellationToken)
    {
        if (sourceIdeaId == targetIdeaId)
            throw new DomainRuleException("Select two different ideas for a conceptual relation.");
        var ideas = await store.ReadIdeasAsync(mapId, [sourceIdeaId, targetIdeaId],
            cancellationToken);
        if (ideas.Count != 2)
            throw new DomainRuleException("Both relation endpoints must belong to this map.");
    }

    private async Task AssertRevisionAsync(MapId mapId, long expectedRevision,
        CancellationToken cancellationToken)
    {
        var map = await store.GetMapAsync(cancellationToken);
        if (map is null || map.Id != mapId || map.Revision != expectedRevision)
            throw new StaleMapRevisionException(expectedRevision);
    }
}
