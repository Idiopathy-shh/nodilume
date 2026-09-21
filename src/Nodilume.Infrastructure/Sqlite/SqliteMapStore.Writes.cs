using Microsoft.Data.Sqlite;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task CreateMapAsync(MapGraph graph, CancellationToken cancellationToken = default)
    {
        if (graph.Map.SchemaVersion > SqliteSchema.CurrentVersion)
            throw new UnsupportedSchemaVersionException(graph.Map.SchemaVersion, SqliteSchema.CurrentVersion);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM maps;";
            if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) != 0)
                throw new InvalidOperationException("Map database is already initialized.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO maps(id, title, revision, schema_version)
VALUES (@id, @title, @revision, @schema);
""";
            command.Parameters.AddWithValue("@id", graph.Map.Id.ToString());
            command.Parameters.AddWithValue("@title", graph.Map.Title);
            command.Parameters.AddWithValue("@revision", graph.Map.Revision);
            command.Parameters.AddWithValue("@schema", graph.Map.SchemaVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var idea in graph.Ideas.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal))
            await InsertIdeaAsync(connection, transaction, idea, cancellationToken);
        foreach (var placement in OrderPlacements(graph.Placements.Values))
            await InsertPlacementAsync(connection, transaction, placement, cancellationToken);
        foreach (var relation in graph.Relations.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal))
            await InsertRelationAsync(connection, transaction, relation, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> ApplyAsync(
        MapId mapId,
        long expectedRevision,
        MapChange change,
        CancellationToken cancellationToken = default)
    {
        EnsureChangeMap(mapId, change);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var revision = connection.CreateCommand())
        {
            revision.Transaction = transaction;
            revision.CommandText = """
UPDATE maps
SET revision = revision + 1
WHERE id = @map AND revision = @expected;
""";
            revision.Parameters.AddWithValue("@map", mapId.ToString());
            revision.Parameters.AddWithValue("@expected", expectedRevision);
            if (await revision.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new StaleMapRevisionException(expectedRevision);
        }

        switch (change)
        {
            case CreateIdeaWithPlacementChange create:
                await InsertIdeaAsync(connection, transaction, create.Idea, cancellationToken);
                await InsertPlacementAsync(connection, transaction, create.Placement, cancellationToken);
                break;
            case UpdateIdeaChange update:
                await UpdateIdeaAsync(connection, transaction, update.Idea, cancellationToken);
                break;
            case AddPlacementChange addPlacement:
                await InsertPlacementAsync(connection, transaction, addPlacement.Placement, cancellationToken);
                break;
            case UpdatePlacementDetailsChange details:
                await UpdatePlacementAsync(connection, transaction, details.Placement, cancellationToken);
                break;
            case MovePlacementChange move:
                await UpdatePlacementAsync(connection, transaction, move.Placement, cancellationToken);
                break;
            case RemovePlacementChange remove:
                await DeletePlacementAsync(connection, transaction, mapId, remove.PlacementId, cancellationToken);
                break;
            case AddRelationChange add:
                await InsertRelationAsync(connection, transaction, add.Relation, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }

        await transaction.CommitAsync(cancellationToken);
        return checked(expectedRevision + 1);
    }

    private static async Task InsertIdeaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Idea idea,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO ideas(id, map_id, title, content)
VALUES (@id, @map, @title, @content);
""";
        command.Parameters.AddWithValue("@id", idea.Id.ToString());
        command.Parameters.AddWithValue("@map", idea.MapId.ToString());
        command.Parameters.AddWithValue("@title", idea.Title);
        command.Parameters.AddWithValue("@content", idea.Content);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPlacementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Placement placement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO placements(id, map_id, idea_id, parent_id, x, y, z, is_pinned, annotation)
VALUES (@id, @map, @idea, @parent, @x, @y, @z, @pinned, @annotation);
""";
        command.Parameters.AddWithValue("@id", placement.Id.ToString());
        command.Parameters.AddWithValue("@map", placement.MapId.ToString());
        command.Parameters.AddWithValue("@idea", placement.IdeaId.ToString());
        command.Parameters.AddWithValue("@parent", placement.ParentId is null ? DBNull.Value : placement.ParentId.Value.ToString());
        command.Parameters.AddWithValue("@x", placement.X);
        command.Parameters.AddWithValue("@y", placement.Y);
        command.Parameters.AddWithValue("@z", placement.Z);
        command.Parameters.AddWithValue("@pinned", placement.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("@annotation", placement.Annotation);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRelationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Relation relation,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO relations(id, map_id, source_idea_id, target_idea_id, kind, is_directed, explanation)
VALUES (@id, @map, @source, @target, @kind, @directed, @explanation);
""";
        command.Parameters.AddWithValue("@id", relation.Id.ToString());
        command.Parameters.AddWithValue("@map", relation.MapId.ToString());
        command.Parameters.AddWithValue("@source", relation.SourceIdeaId.ToString());
        command.Parameters.AddWithValue("@target", relation.TargetIdeaId.ToString());
        command.Parameters.AddWithValue("@kind", relation.Kind);
        command.Parameters.AddWithValue("@directed", relation.IsDirected ? 1 : 0);
        command.Parameters.AddWithValue("@explanation", relation.Explanation);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateIdeaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Idea idea,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE ideas SET title=@title, content=@content
WHERE id=@id AND map_id=@map;
""";
        command.Parameters.AddWithValue("@title", idea.Title);
        command.Parameters.AddWithValue("@content", idea.Content);
        command.Parameters.AddWithValue("@id", idea.Id.ToString());
        command.Parameters.AddWithValue("@map", idea.MapId.ToString());
        EnsureAffected(await command.ExecuteNonQueryAsync(cancellationToken), "Idea");
    }

    private static async Task UpdatePlacementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Placement placement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE placements
SET parent_id=@parent, x=@x, y=@y, z=@z, is_pinned=@pinned, annotation=@annotation
WHERE id=@id AND map_id=@map;
""";
        command.Parameters.AddWithValue("@parent", placement.ParentId is null ? DBNull.Value : placement.ParentId.Value.ToString());
        command.Parameters.AddWithValue("@x", placement.X);
        command.Parameters.AddWithValue("@y", placement.Y);
        command.Parameters.AddWithValue("@z", placement.Z);
        command.Parameters.AddWithValue("@pinned", placement.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("@annotation", placement.Annotation);
        command.Parameters.AddWithValue("@id", placement.Id.ToString());
        command.Parameters.AddWithValue("@map", placement.MapId.ToString());
        EnsureAffected(await command.ExecuteNonQueryAsync(cancellationToken), "Placement");
    }

    private static async Task DeletePlacementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MapId mapId,
        PlacementId placementId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM placements WHERE id=@id AND map_id=@map;";
        command.Parameters.AddWithValue("@id", placementId.ToString());
        command.Parameters.AddWithValue("@map", mapId.ToString());
        EnsureAffected(await command.ExecuteNonQueryAsync(cancellationToken), "Placement");
    }

    private static void EnsureAffected(int affected, string entity)
    {
        if (affected != 1) throw new InvalidOperationException($"{entity} does not exist in the map.");
    }

    private static void EnsureChangeMap(MapId mapId, MapChange change)
    {
        var valid = change switch
        {
            CreateIdeaWithPlacementChange x => x.Idea.MapId == mapId
                && x.Placement.MapId == mapId
                && x.Placement.IdeaId == x.Idea.Id,
            UpdateIdeaChange x => x.Idea.MapId == mapId,
            AddPlacementChange x => x.Placement.MapId == mapId,
            UpdatePlacementDetailsChange x => x.Placement.MapId == mapId,
            MovePlacementChange x => x.Placement.MapId == mapId,
            RemovePlacementChange => true,
            AddRelationChange x => x.Relation.MapId == mapId,
            _ => false
        };
        if (!valid) throw new DomainRuleException("Persistence change crosses map boundaries.");
    }

    private static IReadOnlyList<Placement> OrderPlacements(IEnumerable<Placement> placements)
    {
        var remaining = placements.ToDictionary(x => x.Id);
        var ordered = new List<Placement>(remaining.Count);
        var inserted = new HashSet<PlacementId>();
        while (remaining.Count > 0)
        {
            var ready = remaining.Values
                .Where(x => x.ParentId is null || inserted.Contains(x.ParentId.Value))
                .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0) throw new DomainRuleException("Placements do not form a valid containment forest.");
            foreach (var placement in ready)
            {
                ordered.Add(placement);
                inserted.Add(placement.Id);
                remaining.Remove(placement.Id);
            }
        }
        return ordered;
    }
}