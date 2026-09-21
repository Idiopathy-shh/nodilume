using System.IO;
using System.Text.Json;
using Nodilume.Core;

namespace Nodilume.Application;

/// <summary>
/// Versioned, logical map snapshot. No SQLite schema, edit history or view state.
/// This initial in-memory codec is intentionally limited to small maps.
/// </summary>
public static class PortableMapJson
{
    public const int FormatVersion = 1;
    public const int MaxJsonChars = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Export(MapGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (string.IsNullOrWhiteSpace(graph.Map.Title))
            throw new InvalidDataException("Map title is required.");
        var document = new MapDocument(
            FormatVersion,
            graph.Map.Title,
            graph.Ideas.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .Select(x => new IdeaRow(x.Id.Value, x.Title, x.Content)).ToArray(),
            graph.Placements.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .Select(x => new PlacementRow(x.Id.Value, x.IdeaId.Value,
                    x.ParentId?.Value, x.X, x.Y, x.Z, x.IsPinned, x.Annotation)).ToArray(),
            graph.Relations.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .Select(x => new RelationRow(x.Id.Value, x.SourceIdeaId.Value,
                    x.TargetIdeaId.Value, x.Kind, x.IsDirected, x.Explanation)).ToArray());
        var json = JsonSerializer.Serialize(document, Options);
        if (json.Length > MaxJsonChars)
            throw new InvalidDataException("Portable map exceeds the in-memory codec limit.");
        return json;
    }

    public static MapGraph Import(string json, MapId destinationMapId, int destinationSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length is 0 or > MaxJsonChars)
            throw new InvalidDataException("Portable map is empty or exceeds the codec limit.");
        if (destinationMapId.Value == Guid.Empty)
            throw new ArgumentException("Destination map ID cannot be empty.", nameof(destinationMapId));
        if (destinationSchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(destinationSchemaVersion));

        MapDocument? document;
        try { document = JsonSerializer.Deserialize<MapDocument>(json, Options); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid portable map JSON.", ex); }

        if (document is null || document.FormatVersion != FormatVersion)
            throw new InvalidDataException("Unsupported portable map format version.");
        if (string.IsNullOrWhiteSpace(document.Title)
            || document.Ideas is null || document.Placements is null || document.Relations is null
            || document.Ideas.Any(x => x is null)
            || document.Placements.Any(x => x is null)
            || document.Relations.Any(x => x is null))
            throw new InvalidDataException("Portable map is missing required fields.");
        if (document.Ideas.Any(x => x.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(x.Title) || x.Content is null)
            || document.Placements.Any(x => x.Id == Guid.Empty || x.IdeaId == Guid.Empty
                || x.ParentId == Guid.Empty || x.Annotation is null)
            || document.Relations.Any(x => x.Id == Guid.Empty
                || x.SourceIdeaId == Guid.Empty || x.TargetIdeaId == Guid.Empty
                || x.Kind is null || x.Explanation is null))
            throw new InvalidDataException("Portable map contains missing identities or fields.");

        try
        {
            // A newly created MapId and revision prevent overwriting the source map.
            return new MapGraph(
                new MapInfo(destinationMapId, document.Title, 0, destinationSchemaVersion),
                document.Ideas.Select(x => new Idea(new IdeaId(x.Id), destinationMapId,
                    x.Title, x.Content)),
                document.Placements.Select(x => new Placement(new PlacementId(x.Id),
                    destinationMapId, new IdeaId(x.IdeaId),
                    x.ParentId is { } parent ? new PlacementId(parent) : null,
                    x.X, x.Y, x.Z, x.IsPinned, x.Annotation)),
                document.Relations.Select(x => new Relation(new RelationId(x.Id),
                    destinationMapId, new IdeaId(x.SourceIdeaId), new IdeaId(x.TargetIdeaId),
                    x.Kind, x.IsDirected, x.Explanation)));
        }
        catch (ArgumentException ex) { throw new InvalidDataException("Duplicate identity in portable map.", ex); }
        catch (DomainRuleException ex) { throw new InvalidDataException("Invalid portable map graph.", ex); }
    }

    private sealed record MapDocument(int FormatVersion, string Title,
        IdeaRow[] Ideas, PlacementRow[] Placements, RelationRow[] Relations);
    private sealed record IdeaRow(Guid Id, string Title, string Content);
    private sealed record PlacementRow(Guid Id, Guid IdeaId, Guid? ParentId,
        double X, double Y, double Z, bool IsPinned, string Annotation);
    private sealed record RelationRow(Guid Id, Guid SourceIdeaId, Guid TargetIdeaId,
        string Kind, bool IsDirected, string Explanation);
}
