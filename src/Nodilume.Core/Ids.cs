namespace Nodilume.Core;

public readonly record struct MapId(Guid Value)
{
    public static MapId New() => new(Guid.NewGuid());
    public static MapId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString("D");
}

public readonly record struct IdeaId(Guid Value)
{
    public static IdeaId New() => new(Guid.NewGuid());
    public static IdeaId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString("D");
}

public readonly record struct PlacementId(Guid Value)
{
    public static PlacementId New() => new(Guid.NewGuid());
    public static PlacementId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString("D");
}

public readonly record struct RelationId(Guid Value)
{
    public static RelationId New() => new(Guid.NewGuid());
    public static RelationId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString("D");
}