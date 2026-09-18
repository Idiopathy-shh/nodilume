using Microsoft.Data.Sqlite;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task<IReadOnlyList<Placement>> ReadPlacementPageAsync(
        MapId mapId,
        int limit,
        PlacementId? after = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM placements
WHERE map_id = @map AND (@after IS NULL OR id > @after)
ORDER BY id
LIMIT @limit;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@after", after is null ? DBNull.Value : after.Value.ToString());
        command.Parameters.AddWithValue("@limit", limit);
        var result = new List<Placement>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadPlacement(reader, mapId));
        return result;
    }

    public async Task<IReadOnlyList<Idea>> ReadIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = ids.Select((_, index) => $"@id{index}").ToArray();
        command.CommandText = $"""
SELECT id, title, content
FROM ideas
WHERE map_id = @map AND id IN ({string.Join(",", names)})
ORDER BY id;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var index = 0;
        foreach (var id in ids) command.Parameters.AddWithValue($"@id{index++}", id.ToString());
        var result = new List<Idea>(ids.Count);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new Idea(IdeaId.Parse(reader.GetString(0)), mapId, reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<IReadOnlyList<Relation>> ReadRelationsForIdeasAsync(
        MapId mapId,
        IReadOnlyCollection<IdeaId> ideaIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (ideaIds.Count == 0) return [];
        if (limit is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = ideaIds.Select((_, index) => $"@idea{index}").ToArray();
        var inClause = string.Join(",", names);
        command.CommandText = $"""
SELECT id, source_idea_id, target_idea_id, kind, is_directed, explanation
FROM relations
WHERE map_id = @map
  AND source_idea_id IN ({inClause})
  AND target_idea_id IN ({inClause})
ORDER BY id
LIMIT @limit;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@limit", limit);
        var index = 0;
        foreach (var id in ideaIds) command.Parameters.AddWithValue($"@idea{index++}", id.ToString());
        var result = new List<Relation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRelation(reader, mapId));
        return result;
    }

    private static async Task<IReadOnlyList<Idea>> ReadAllIdeasAsync(
        SqliteConnection connection,
        MapId mapId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, content FROM ideas WHERE map_id=@map ORDER BY id;";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var result = new List<Idea>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new Idea(IdeaId.Parse(reader.GetString(0)), mapId, reader.GetString(1), reader.GetString(2)));
        return result;
    }

    private static async Task<IReadOnlyList<Placement>> ReadAllPlacementsAsync(
        SqliteConnection connection,
        MapId mapId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, idea_id, parent_id, x, y, z, is_pinned, annotation
FROM placements WHERE map_id=@map ORDER BY id;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var result = new List<Placement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadPlacement(reader, mapId));
        return result;
    }

    private static async Task<IReadOnlyList<Relation>> ReadAllRelationsAsync(
        SqliteConnection connection,
        MapId mapId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, source_idea_id, target_idea_id, kind, is_directed, explanation
FROM relations WHERE map_id=@map ORDER BY id;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var result = new List<Relation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRelation(reader, mapId));
        return result;
    }

    private static Placement ReadPlacement(SqliteDataReader reader, MapId mapId) => new(
        PlacementId.Parse(reader.GetString(0)),
        mapId,
        IdeaId.Parse(reader.GetString(1)),
        reader.IsDBNull(2) ? null : PlacementId.Parse(reader.GetString(2)),
        reader.GetDouble(3),
        reader.GetDouble(4),
        reader.GetDouble(5),
        reader.GetInt64(6) != 0,
        reader.GetString(7));

    private static Relation ReadRelation(SqliteDataReader reader, MapId mapId) => new(
        RelationId.Parse(reader.GetString(0)),
        mapId,
        IdeaId.Parse(reader.GetString(1)),
        IdeaId.Parse(reader.GetString(2)),
        reader.GetString(3),
        reader.GetInt64(4) != 0,
        reader.GetString(5));
}