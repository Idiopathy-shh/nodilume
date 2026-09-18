using Microsoft.Data.Sqlite;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task<Placement?> ReadPlacementAsync(
        MapId mapId,
        PlacementId placementId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM placements
WHERE map_id=@map AND id=@id
LIMIT 1;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@id", placementId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPlacement(reader, mapId) : null;
    }

    public async Task<BoundedResult<Placement>> ReadChildrenAsync(
        MapId mapId,
        PlacementId? parentId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await ReadChildrenPageAsync(mapId, parentId, limit, null, cancellationToken);
        return new BoundedResult<Placement>(page.Items, page.HasMore);
    }

    public async Task<PlacementPage> ReadChildrenPageAsync(
        MapId mapId,
        PlacementId? parentId,
        int limit,
        PlacementId? after = null,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundedLimit(limit, 512);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var parentPredicate = parentId is null ? "parent_id IS NULL" : "parent_id=@parent";
        var afterPredicate = after is null ? "" : "AND id>@after";
        command.CommandText = $"""
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM placements
WHERE map_id=@map
  AND {parentPredicate}
  {afterPredicate}
ORDER BY id
LIMIT @take;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        if (parentId is not null) command.Parameters.AddWithValue("@parent", parentId.Value.ToString());
        if (after is not null) command.Parameters.AddWithValue("@after", after.Value.ToString());
        command.Parameters.AddWithValue("@take", limit + 1);
        var rows = new List<Placement>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadPlacement(reader, mapId));
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveRange(limit, rows.Count - limit);
        return new PlacementPage(rows, hasMore, hasMore && rows.Count > 0 ? rows[^1].Id : null);
    }

    public async Task<IReadOnlyList<Placement>> ReadAncestorPathAsync(
        MapId mapId,
        PlacementId placementId,
        int maxDepth,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
WITH RECURSIVE ancestry(
    id, idea_id, parent_id, x, y, z, is_pinned, annotation, depth
) AS (
    SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation, 0
    FROM placements
    WHERE map_id=@map AND id=@id
    UNION ALL
    SELECT p.id, p.idea_id, p.parent_id, p.x, p.y, p.z, p.is_pinned, p.annotation, a.depth + 1
    FROM placements p
    JOIN ancestry a ON p.id = a.parent_id
    WHERE p.map_id=@map AND a.depth + 1 < @maxDepth
)
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM ancestry
ORDER BY depth DESC;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@id", placementId.ToString());
        command.Parameters.AddWithValue("@maxDepth", maxDepth);
        var rows = new List<Placement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadPlacement(reader, mapId));
        return rows;
    }

    public async Task<IReadOnlyDictionary<PlacementId, int>> ReadChildCountsAsync(
        MapId mapId,
        IReadOnlyCollection<PlacementId> placementIds,
        CancellationToken cancellationToken = default)
    {
        if (placementIds.Count == 0) return new Dictionary<PlacementId, int>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = placementIds.Select((_, index) => $"@p{index}").ToArray();
        command.CommandText = $"""
SELECT parent_id, COUNT(*)
FROM placements
WHERE map_id=@map AND parent_id IN ({string.Join(",", names)})
GROUP BY parent_id;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var index = 0;
        foreach (var id in placementIds)
            command.Parameters.AddWithValue($"@p{index++}", id.ToString());

        var result = new Dictionary<PlacementId, int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[PlacementId.Parse(reader.GetString(0))] = reader.GetInt32(1);
        return result;
    }

    public async Task<BoundedResult<Placement>> ReadDescendantsAsync(
        MapId mapId,
        IReadOnlyCollection<PlacementId> rootIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundedLimit(limit, 4096);
        if (rootIds.Count == 0) return new BoundedResult<Placement>([], false);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = rootIds.Select((_, index) => $"@root{index}").ToArray();
        command.CommandText = $"""
WITH RECURSIVE subtree(
    id, idea_id, parent_id, x, y, z, is_pinned, annotation, depth
) AS (
    SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation, 0
    FROM placements
    WHERE map_id=@map AND id IN ({string.Join(",", names)})
    UNION ALL
    SELECT p.id, p.idea_id, p.parent_id, p.x, p.y, p.z, p.is_pinned, p.annotation, s.depth + 1
    FROM placements p
    JOIN subtree s ON p.parent_id = s.id
    WHERE p.map_id=@map
)
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM subtree
ORDER BY depth, id
LIMIT @take;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@take", limit + 1);
        var index = 0;
        foreach (var id in rootIds)
            command.Parameters.AddWithValue($"@root{index++}", id.ToString());

        var rows = new List<Placement>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadPlacement(reader, mapId));
        return ToBounded(rows, limit);
    }

    public async Task<BoundedResult<Placement>> ReadPlacementsForIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundedLimit(limit, 4096);
        if (ideaIds.Count == 0) return new BoundedResult<Placement>([], false);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = ideaIds.Select((_, index) => $"@idea{index}").ToArray();
        command.CommandText = $"""
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM placements
WHERE map_id=@map AND idea_id IN ({string.Join(",", names)})
ORDER BY idea_id, id
LIMIT @take;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@take", limit + 1);
        var index = 0;
        foreach (var id in ideaIds)
            command.Parameters.AddWithValue($"@idea{index++}", id.ToString());

        var rows = new List<Placement>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadPlacement(reader, mapId));
        return ToBounded(rows, limit);
    }

    public async Task<BoundedResult<Relation>> ReadRelationsTouchingIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundedLimit(limit, 4096);
        if (ideaIds.Count == 0) return new BoundedResult<Relation>([], false);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = ideaIds.Select((_, index) => $"@idea{index}").ToArray();
        var inClause = string.Join(",", names);
        command.CommandText = $"""
SELECT id, source_idea_id, target_idea_id, kind, is_directed, explanation
FROM relations
WHERE map_id=@map
  AND (source_idea_id IN ({inClause}) OR target_idea_id IN ({inClause}))
ORDER BY id
LIMIT @take;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@take", limit + 1);
        var index = 0;
        foreach (var id in ideaIds)
            command.Parameters.AddWithValue($"@idea{index++}", id.ToString());

        var rows = new List<Relation>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadRelation(reader, mapId));
        return ToBounded(rows, limit);
    }

    private static BoundedResult<T> ToBounded<T>(List<T> rows, int limit)
    {
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveRange(limit, rows.Count - limit);
        return new BoundedResult<T>(rows, hasMore);
    }

    private static void ValidateBoundedLimit(int limit, int maximum)
    {
        if (limit < 1 || limit > maximum)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }
}
