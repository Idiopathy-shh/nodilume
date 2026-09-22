using Microsoft.Data.Sqlite;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task<Relation?> ReadRelationAsync(MapId mapId, RelationId relationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, source_idea_id, target_idea_id, kind, is_directed, explanation
FROM relations WHERE map_id=@map AND id=@id;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@id", relationId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRelation(reader, mapId) : null;
    }

    private static async Task UpdateRelationAsync(SqliteConnection connection,
        SqliteTransaction transaction, Relation relation, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE relations SET source_idea_id=@source, target_idea_id=@target,
 kind=@kind, is_directed=@directed, explanation=@explanation
WHERE id=@id AND map_id=@map;
""";
        command.Parameters.AddWithValue("@id", relation.Id.ToString());
        command.Parameters.AddWithValue("@map", relation.MapId.ToString());
        command.Parameters.AddWithValue("@source", relation.SourceIdeaId.ToString());
        command.Parameters.AddWithValue("@target", relation.TargetIdeaId.ToString());
        command.Parameters.AddWithValue("@kind", relation.Kind);
        command.Parameters.AddWithValue("@directed", relation.IsDirected ? 1 : 0);
        command.Parameters.AddWithValue("@explanation", relation.Explanation);
        EnsureAffected(await command.ExecuteNonQueryAsync(cancellationToken), "Relation");
    }

    private static async Task DeleteRelationAsync(SqliteConnection connection,
        SqliteTransaction transaction, MapId mapId, RelationId relationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM relations WHERE id=@id AND map_id=@map;";
        command.Parameters.AddWithValue("@id", relationId.ToString());
        command.Parameters.AddWithValue("@map", mapId.ToString());
        EnsureAffected(await command.ExecuteNonQueryAsync(cancellationToken), "Relation");
    }
}
