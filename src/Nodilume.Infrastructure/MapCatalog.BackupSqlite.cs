using Microsoft.Data.Sqlite;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

public sealed partial class MapCatalog
{
    private static async Task CreateOnlineSnapshotAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            var destinationBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            using var source = new SqliteConnection(
                sourceBuilder.ToString());
            using var destination = new SqliteConnection(
                destinationBuilder.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    private static async Task<SnapshotMetadata> ReadSnapshotMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using var connection =
            new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(
                await integrity.ExecuteScalarAsync(cancellationToken));
            if (result != "ok")
                throw new InvalidDataException(
                    "Backup database failed SQLite integrity_check.");
        }

        int databaseSchemaVersion;
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText =
                "SELECT version FROM schema_info WHERE id = 1;";
            var value = await schema.ExecuteScalarAsync(cancellationToken);
            if (value is null or DBNull)
                throw new InvalidDataException(
                    "Backup database has no schema version.");
            databaseSchemaVersion = Convert.ToInt32(value);
        }
        if (databaseSchemaVersion is < 1
            or > SqliteSchema.CurrentVersion)
            throw new InvalidDataException(
                "Backup database schema is not supported.");

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, title, revision, schema_version FROM maps LIMIT 2;";
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException(
                "Backup database contains no map.");
        var map = new MapInfo(
            MapId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt32(3));
        if (await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException(
                "Backup database contains multiple maps.");
        return new SnapshotMetadata(
            map, databaseSchemaVersion);
    }

    private static async Task ReassignMapIdentityAsync(
        string path,
        MapId sourceId,
        MapId destinationId,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        await using var connection =
            new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText =
                "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await foreignKeys.ExecuteNonQueryAsync(cancellationToken);
        }
        using var transaction = connection.BeginTransaction();
        await using (var defer = connection.CreateCommand())
        {
            defer.Transaction = transaction;
            defer.CommandText = "PRAGMA defer_foreign_keys = ON;";
            await defer.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var sql in new[]
                 {
                     "UPDATE ideas SET map_id = @new WHERE map_id = @old;",
                     "UPDATE placements SET map_id = @new WHERE map_id = @old;",
                     "UPDATE relations SET map_id = @new WHERE map_id = @old;"
                 })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue(
                "@new", destinationId.ToString());
            command.Parameters.AddWithValue(
                "@old", sourceId.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var map = connection.CreateCommand())
        {            map.Transaction = transaction;
            map.CommandText =
                "UPDATE maps SET id = @new WHERE id = @old;";
            map.Parameters.AddWithValue(
                "@new", destinationId.ToString());
            map.Parameters.AddWithValue(
                "@old", sourceId.ToString());
            if (await map.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidDataException(
                    "Backup map identity could not be reassigned.");
        }
        await transaction.CommitAsync(cancellationToken);

        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA foreign_key_check;";
        await using var reader =
            await check.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException(
                "Restored database failed foreign_key_check.");
    }

    private static void ValidateSnapshotMatchesManifest(
        SnapshotMetadata snapshot,
        BackupManifest manifest)
    {
        if (snapshot.Map.Id.ToString() != manifest.SourceMapId
            || snapshot.Map.Title != manifest.Title
            || snapshot.Map.Revision != manifest.Revision
            || snapshot.Map.SchemaVersion != manifest.MapSchemaVersion
            || snapshot.DatabaseSchemaVersion
                != manifest.DatabaseSchemaVersion)
            throw new InvalidDataException(
                "Backup manifest does not match the SQLite snapshot.");
    }
}