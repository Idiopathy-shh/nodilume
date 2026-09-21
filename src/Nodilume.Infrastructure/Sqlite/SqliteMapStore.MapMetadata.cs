using Microsoft.Data.Sqlite;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task<MapInfo> RenameMapAsync(MapId mapId, long expectedRevision,
        string title, CancellationToken cancellationToken = default)
    {
        var validTitle = Nodilume.Infrastructure.MapCatalog.ValidateTitle(title);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE maps SET title = @title, revision = revision + 1
WHERE id = @id AND revision = @revision AND title <> @title;
""";
        command.Parameters.AddWithValue("@title", validTitle);
        command.Parameters.AddWithValue("@id", mapId.ToString());
        command.Parameters.AddWithValue("@revision", expectedRevision);
        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT title, revision, schema_version FROM maps WHERE id = @id;";
        read.Parameters.AddWithValue("@id", mapId.ToString());
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new FileNotFoundException("Map not found.");
        var current = new MapInfo(mapId, reader.GetString(0), reader.GetInt64(1), reader.GetInt32(2));
        if (updated == 0 && current.Revision != expectedRevision)
            throw new StaleMapRevisionException(expectedRevision);
        if (updated == 0 && current.Title != validTitle)
            throw new InvalidOperationException("Map rename did not update the title.");
        if (updated == 1 && current.Revision != checked(expectedRevision + 1))
            throw new InvalidDataException("Map revision did not advance exactly once.");
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return current;
    }
}
