using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Application;

public sealed class MapEditor(IMapStore store)
{
    public async Task<long> CreateIdeaAsync(
        IdeaId ideaId,
        string title,
        string content,
        PlacementId placementId,
        PlacementId? parentId,
        double x,
        double y,
        double z,
        string annotation = "",
        CancellationToken cancellationToken = default)
    {
        var graph = await store.LoadGraphAsync(cancellationToken);
        var idea = graph.AddIdea(ideaId, title, content);
        var placement = graph.AddPlacement(placementId, ideaId, parentId, x, y, z, annotation: annotation);
        return await store.ApplyAsync(
            graph.Map.Id,
            graph.Map.Revision,
            new CreateIdeaWithPlacementChange(idea, placement),
            cancellationToken);
    }

    public async Task<long> UpdateIdeaAsync(
        IdeaId ideaId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        var graph = await store.LoadGraphAsync(cancellationToken);
        var idea = graph.UpdateIdea(ideaId, title, content);
        return await store.ApplyAsync(
            graph.Map.Id,
            graph.Map.Revision,
            new UpdateIdeaChange(idea),
            cancellationToken);
    }

    public async Task<long> MovePlacementAsync(
        PlacementId placementId,
        PlacementId? newParentId,
        double x,
        double y,
        double z,
        CancellationToken cancellationToken = default)
    {
        var graph = await store.LoadGraphAsync(cancellationToken);
        var placement = graph.MovePlacement(placementId, newParentId, x, y, z);
        return await store.ApplyAsync(
            graph.Map.Id,
            graph.Map.Revision,
            new MovePlacementChange(placement),
            cancellationToken);
    }

    public async Task<long> RemoveLeafPlacementAsync(
        PlacementId placementId,
        CancellationToken cancellationToken = default)
    {
        var graph = await store.LoadGraphAsync(cancellationToken);
        graph.RemoveLeafPlacement(placementId);
        return await store.ApplyAsync(
            graph.Map.Id,
            graph.Map.Revision,
            new RemovePlacementChange(placementId),
            cancellationToken);
    }

    public async Task<long> AddRelationAsync(
        RelationId relationId,
        IdeaId sourceIdeaId,
        IdeaId targetIdeaId,
        string kind,
        bool directed,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        var graph = await store.LoadGraphAsync(cancellationToken);
        var relation = graph.AddRelation(relationId, sourceIdeaId, targetIdeaId, kind, directed, explanation);
        return await store.ApplyAsync(
            graph.Map.Id,
            graph.Map.Revision,
            new AddRelationChange(relation),
            cancellationToken);
    }
}