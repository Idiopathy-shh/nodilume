using Microsoft.Data.Sqlite;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore : IMapStore
{
    private readonly string _connectionString;

    public SqliteMapStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.", nameof(databasePath));
        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public string DatabasePath { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var exists = await TableExistsAsync(connection, transaction, "schema_info", cancellationToken);
        var version = 0;
        if (exists)
        {
            await using var versionCommand = connection.CreateCommand();
            versionCommand.Transaction = transaction;
            versionCommand.CommandText = "SELECT version FROM schema_info WHERE id = 1;";
            var value = await versionCommand.ExecuteScalarAsync(cancellationToken);
            version = value is null or DBNull ? 0 : Convert.ToInt32(value);
        }

        if (version > SqliteSchema.CurrentVersion)
            throw new UnsupportedSchemaVersionException(version, SqliteSchema.CurrentVersion);

        if (version < 1)
        {
            await using var migration = connection.CreateCommand();
            migration.Transaction = transaction;
            migration.CommandText = SqliteSchema.MigrationV1;
            await migration.ExecuteNonQueryAsync(cancellationToken);

            await using var stamp = connection.CreateCommand();
            stamp.Transaction = transaction;
            stamp.CommandText = """
INSERT INTO schema_info(id, version) VALUES (1, @version)
ON CONFLICT(id) DO UPDATE SET version = excluded.version;
""";
            stamp.Parameters.AddWithValue("@version", SqliteSchema.CurrentVersion);
            await stamp.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<MapInfo?> GetMapAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, revision, schema_version FROM maps ORDER BY id LIMIT 2;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var maps = new List<MapInfo>(2);
        while (await reader.ReadAsync(cancellationToken))
        {
            maps.Add(new MapInfo(
                MapId.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt32(3)));
        }

        return maps.Count switch
        {
            0 => null,
            1 => maps[0],
            _ => throw new InvalidDataException("A map database contains more than one map.")
        };
    }

    public async Task<MapGraph> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        var map = await GetMapAsync(cancellationToken)
            ?? throw new InvalidOperationException("Map database is empty.");
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var ideas = await ReadAllIdeasAsync(connection, map.Id, cancellationToken);
        var placements = await ReadAllPlacementsAsync(connection, map.Id, cancellationToken);
        var relations = await ReadAllRelationsAsync(connection, map.Id, cancellationToken);
        return new MapGraph(map, ideas, placements, relations);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
        command.Parameters.AddWithValue("@name", tableName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }
}