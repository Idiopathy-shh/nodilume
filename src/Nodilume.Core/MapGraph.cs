namespace Nodilume.Core;

public sealed class MapGraph
{
    private readonly Dictionary<IdeaId, Idea> _ideas;
    private readonly Dictionary<PlacementId, Placement> _placements;
    private readonly Dictionary<RelationId, Relation> _relations;

    public MapInfo Map { get; private set; }
    public IReadOnlyDictionary<IdeaId, Idea> Ideas => _ideas;
    public IReadOnlyDictionary<PlacementId, Placement> Placements => _placements;
    public IReadOnlyDictionary<RelationId, Relation> Relations => _relations;

    public MapGraph(
        MapInfo map,
        IEnumerable<Idea>? ideas = null,
        IEnumerable<Placement>? placements = null,
        IEnumerable<Relation>? relations = null)
    {
        Map = map;
        _ideas = (ideas ?? []).ToDictionary(x => x.Id);
        _placements = (placements ?? []).ToDictionary(x => x.Id);
        _relations = (relations ?? []).ToDictionary(x => x.Id);
        ValidateLoadedState();
    }

    public Idea AddIdea(IdeaId id, string title, string content)
    {
        if (_ideas.ContainsKey(id)) throw new DomainRuleException("Idea already exists.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainRuleException("Idea title is required.");
        var idea = new Idea(id, Map.Id, title.Trim(), content);
        _ideas.Add(id, idea);
        return idea;
    }

    public Idea UpdateIdea(IdeaId id, string title, string content)
    {
        if (!_ideas.TryGetValue(id, out var current)) throw new DomainRuleException("Idea does not exist.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainRuleException("Idea title is required.");
        var updated = current with { Title = title.Trim(), Content = content };
        _ideas[id] = updated;
        return updated;
    }

    public Placement AddPlacement(
        PlacementId id,
        IdeaId ideaId,
        PlacementId? parentId,
        double x,
        double y,
        double z,
        bool isPinned = false,
        string annotation = "")
    {
        ValidateCoordinates(x, y, z);
        if (_placements.ContainsKey(id)) throw new DomainRuleException("Placement already exists.");
        if (!_ideas.ContainsKey(ideaId)) throw new DomainRuleException("Placement idea does not exist.");
        if (parentId is not null)
        {
            if (!_placements.ContainsKey(parentId.Value)) throw new DomainRuleException("Parent placement does not exist.");
            EnsureIdeaAbsentFromAncestors(ideaId, parentId.Value);
        }

        var placement = new Placement(id, Map.Id, ideaId, parentId, x, y, z, isPinned, annotation);
        _placements.Add(id, placement);
        return placement;
    }

    public Placement MovePlacement(PlacementId id, PlacementId? newParentId, double x, double y, double z)
    {
        ValidateCoordinates(x, y, z);
        if (!_placements.TryGetValue(id, out var current)) throw new DomainRuleException("Placement does not exist.");
        if (newParentId == id) throw new DomainRuleException("A placement cannot parent itself.");
        if (newParentId is not null && !_placements.ContainsKey(newParentId.Value))
            throw new DomainRuleException("Parent placement does not exist.");
        if (newParentId is not null && IsDescendant(newParentId.Value, id))
            throw new DomainRuleException("Containment cycles are not allowed.");

        if (newParentId is not null)
        {
            var ancestorIdeas = GetAncestorIdeaIds(newParentId.Value);
            var subtreeIdeas = GetSubtreeIdeaIds(id);
            if (subtreeIdeas.Overlaps(ancestorIdeas))
                throw new DomainRuleException("The same idea cannot repeat along an ancestor-descendant chain.");
        }

        var updated = current with { ParentId = newParentId, X = x, Y = y, Z = z };
        _placements[id] = updated;
        return updated;
    }

    public Placement SetPlacementDetails(PlacementId id, bool isPinned, string annotation)
    {
        if (!_placements.TryGetValue(id, out var current)) throw new DomainRuleException("Placement does not exist.");
        var updated = current with { IsPinned = isPinned, Annotation = annotation };
        _placements[id] = updated;
        return updated;
    }

    public void RemoveLeafPlacement(PlacementId id)
    {
        if (!_placements.ContainsKey(id)) throw new DomainRuleException("Placement does not exist.");
        if (_placements.Values.Any(x => x.ParentId == id))
            throw new DomainRuleException("A placement with children cannot be removed implicitly.");
        _placements.Remove(id);
    }

    public Relation AddRelation(
        RelationId id,
        IdeaId sourceIdeaId,
        IdeaId targetIdeaId,
        string kind,
        bool isDirected,
        string explanation)
    {
        if (_relations.ContainsKey(id)) throw new DomainRuleException("Relation already exists.");
        if (!_ideas.ContainsKey(sourceIdeaId) || !_ideas.ContainsKey(targetIdeaId))
            throw new DomainRuleException("Relation endpoints must exist in the same map.");
        var relation = new Relation(id, Map.Id, sourceIdeaId, targetIdeaId, kind, isDirected, explanation);
        _relations.Add(id, relation);
        return relation;
    }

    public void SetRevision(long revision) => Map = Map with { Revision = revision };

    private void ValidateLoadedState()
    {
        if (Map.Revision < 0) throw new DomainRuleException("Map revision cannot be negative.");
        foreach (var idea in _ideas.Values)
            if (idea.MapId != Map.Id) throw new DomainRuleException("Idea belongs to another map.");

        foreach (var placement in _placements.Values)
        {
            if (placement.MapId != Map.Id) throw new DomainRuleException("Placement belongs to another map.");
            ValidateCoordinates(placement.X, placement.Y, placement.Z);
            if (!_ideas.ContainsKey(placement.IdeaId)) throw new DomainRuleException("Placement references a missing idea.");
            if (placement.ParentId is not null && !_placements.ContainsKey(placement.ParentId.Value))
                throw new DomainRuleException("Placement references a missing parent.");
        }

        foreach (var placement in _placements.Values) ValidatePlacementChain(placement);
        foreach (var relation in _relations.Values)
        {
            if (relation.MapId != Map.Id) throw new DomainRuleException("Relation belongs to another map.");
            if (!_ideas.ContainsKey(relation.SourceIdeaId) || !_ideas.ContainsKey(relation.TargetIdeaId))
                throw new DomainRuleException("Relation references a missing idea.");
        }
    }

    private void ValidatePlacementChain(Placement placement)
    {
        var placementsSeen = new HashSet<PlacementId>();
        var ideasSeen = new HashSet<IdeaId>();
        Placement? current = placement;
        while (current is not null)
        {
            if (!placementsSeen.Add(current.Id)) throw new DomainRuleException("Containment cycle detected.");
            if (!ideasSeen.Add(current.IdeaId))
                throw new DomainRuleException("The same idea cannot repeat along an ancestor-descendant chain.");
            current = current.ParentId is { } parent ? _placements[parent] : null;
        }
    }

    private void EnsureIdeaAbsentFromAncestors(IdeaId ideaId, PlacementId parentId)
    {
        Placement? current = _placements[parentId];
        while (current is not null)
        {
            if (current.IdeaId == ideaId)
                throw new DomainRuleException("The same idea cannot repeat along an ancestor-descendant chain.");
            current = current.ParentId is { } parent ? _placements[parent] : null;
        }
    }

    private bool IsDescendant(PlacementId candidate, PlacementId ancestor)
    {
        Placement? current = _placements[candidate];
        while (current is not null)
        {
            if (current.Id == ancestor) return true;
            current = current.ParentId is { } parent ? _placements[parent] : null;
        }
        return false;
    }

    private HashSet<IdeaId> GetAncestorIdeaIds(PlacementId start)
    {
        var result = new HashSet<IdeaId>();
        Placement? current = _placements[start];
        while (current is not null)
        {
            result.Add(current.IdeaId);
            current = current.ParentId is { } parent ? _placements[parent] : null;
        }
        return result;
    }

    private HashSet<IdeaId> GetSubtreeIdeaIds(PlacementId root)
    {
        var result = new HashSet<IdeaId>();
        var pending = new Stack<PlacementId>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            result.Add(_placements[id].IdeaId);
            foreach (var child in _placements.Values.Where(x => x.ParentId == id))
                pending.Push(child.Id);
        }
        return result;
    }

    private static void ValidateCoordinates(double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            throw new DomainRuleException("Placement coordinates must be finite.");
    }
}