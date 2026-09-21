using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;

namespace Nodilume.Infrastructure.Sqlite;

public sealed partial class SqliteMapStore
{
    public async Task<LocalEditOutcome> ApplyLocalEditAsync(
        LocalEditCommand request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128)
            throw new ArgumentException("Invalid edit request ID.");
        if (request.Action is not ("move" or "pin" or "undo" or "redo"))
            throw new ArgumentException("Unknown edit action.");
        if (request.Action == "move" &&
            (request.X is not { } x || request.Y is not { } y || request.Z is not { } z
             || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)))
            throw new DomainRuleException("Move requires finite coordinates.");
        if (request.Action == "pin" && request.IsPinned is null)
            throw new DomainRuleException("Pin requires an explicit state.");
        if ((request.Action is "move" or "pin") && request.PlacementId is null)
            throw new DomainRuleException("Placement ID required.");

        var mapId = request.MapId;
        var fingerprint = JsonSerializer.Serialize(request);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        async Task<SqliteCommand> CommandAsync(string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@map", mapId.ToString());
            return await Task.FromResult(cmd);
        }
        await using (var receipt = await CommandAsync(
            """
SELECT fingerprint,(SELECT revision FROM maps WHERE id=@map),
 EXISTS(SELECT 1 FROM graph_edits WHERE map_id=@map AND seq<=COALESCE((SELECT seq FROM edit_cursor WHERE map_id=@map),0)),
 EXISTS(SELECT 1 FROM graph_edits WHERE map_id=@map AND seq>COALESCE((SELECT seq FROM edit_cursor WHERE map_id=@map),0))
 FROM edit_receipts WHERE map_id=@map AND request_id=@request
"""))
        {
            receipt.Parameters.AddWithValue("@request", request.RequestId);
            await using var reader = await receipt.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(0) != fingerprint)
                    throw new DomainRuleException("Request ID reused with different edit payload.");
                return new LocalEditOutcome(reader.GetInt64(1), false, true, null,
                    reader.GetInt64(2) != 0, reader.GetInt64(3) != 0);
            }
        }
        long revision;
        await using (var map = await CommandAsync("SELECT revision FROM maps WHERE id=@map"))
        {
            var value = await map.ExecuteScalarAsync(cancellationToken);
            if (value is null) throw new DomainRuleException("Map not found.");
            revision = Convert.ToInt64(value);
        }
        if (revision != request.ExpectedRevision)
            throw new StaleMapRevisionException(request.ExpectedRevision);
        await using (var initialize = await CommandAsync(
            "INSERT INTO edit_cursor(map_id,seq) VALUES (@map,0) ON CONFLICT(map_id) DO NOTHING"))
            await initialize.ExecuteNonQueryAsync(cancellationToken);
        long cursor;
        await using (var readCursor = await CommandAsync("SELECT seq FROM edit_cursor WHERE map_id=@map"))
            cursor = Convert.ToInt64(await readCursor.ExecuteScalarAsync(cancellationToken));

        Placement? before = null;
        Placement? after = null;
        long historySeq = 0;
        bool isUndo = request.Action == "undo", isRedo = request.Action == "redo";
        if (isUndo || isRedo)
        {
            var sql = isUndo
                ? "SELECT seq,placement_id,before_x,before_y,before_z,before_pin,after_x,after_y,after_z,after_pin FROM graph_edits WHERE map_id=@map AND seq<=@cursor ORDER BY seq DESC LIMIT 1"
                : "SELECT seq,placement_id,before_x,before_y,before_z,before_pin,after_x,after_y,after_z,after_pin FROM graph_edits WHERE map_id=@map AND seq>@cursor ORDER BY seq LIMIT 1";
            await using var history = await CommandAsync(sql);
            history.Parameters.AddWithValue("@cursor", cursor);
            var row = await ReadHistoryRowAsync(history, cancellationToken);
            if (row is not null)
            {
                historySeq = row.Value.Seq;
                var id = PlacementId.Parse(row.Value.PlacementId);
                var current = await ReadPlacementInTransactionAsync(
                    connection, transaction, mapId, id, cancellationToken)
                    ?? throw new DomainRuleException("Edited placement has disappeared.");
                var source = isUndo ? row.Value.After : row.Value.Before;
                var target = isUndo ? row.Value.Before : row.Value.After;
                if (current.X != source.X || current.Y != source.Y ||
                    current.Z != source.Z || current.IsPinned != source.Pin)
                    throw new DomainRuleException("Edited placement no longer matches history.");
                before = current;
                after = current with {
                    X = target.X, Y = target.Y, Z = target.Z, IsPinned = target.Pin
                };
            }
        }
        else
        {
            before = await ReadPlacementInTransactionAsync(connection, transaction, mapId,
                request.PlacementId!.Value, cancellationToken)
                ?? throw new DomainRuleException("Placement not found in this map.");
            if (request.Action == "move")
            {
                if (before.IsPinned) throw new DomainRuleException("Unlock this placement before moving it.");
                after = before with { X = request.X!.Value, Y = request.Y!.Value, Z = request.Z!.Value };
            }
            else
                after = before with { IsPinned = request.IsPinned!.Value };
        }
        var changed = before is not null && after is not null && before != after;
        if (changed)
        {
            await using (var updateMap = await CommandAsync(
                "UPDATE maps SET revision=revision+1 WHERE id=@map AND revision=@expected"))
            {
                updateMap.Parameters.AddWithValue("@expected", revision);
                if (await updateMap.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new StaleMapRevisionException(revision);
            }
            await UpdatePlacementAsync(connection, transaction, after!, cancellationToken);
            if (isUndo || isRedo)
            {
                var next = isRedo ? historySeq : 0L;
                if (isUndo)
                {
                    await using var previous = await CommandAsync(
                        "SELECT COALESCE(MAX(seq),0) FROM graph_edits WHERE map_id=@map AND seq<@seq");
                    previous.Parameters.AddWithValue("@seq", historySeq);
                    next = Convert.ToInt64(await previous.ExecuteScalarAsync(cancellationToken));
                }
                await using var setCursor = await CommandAsync("UPDATE edit_cursor SET seq=@seq WHERE map_id=@map");
                setCursor.Parameters.AddWithValue("@seq", next);
                await setCursor.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using (var pruneRedo = await CommandAsync(
                    "DELETE FROM graph_edits WHERE map_id=@map AND seq>@cursor"))
                {
                    pruneRedo.Parameters.AddWithValue("@cursor", cursor);
                    await pruneRedo.ExecuteNonQueryAsync(cancellationToken);
                }
                await using (var insert = await CommandAsync("""
INSERT INTO graph_edits(map_id,placement_id,before_x,before_y,before_z,before_pin,
 after_x,after_y,after_z,after_pin)
VALUES (@map,@id,@bx,@by,@bz,@bp,@ax,@ay,@az,@ap);
"""))
                {
                    insert.Parameters.AddWithValue("@id", before!.Id.ToString());
                    insert.Parameters.AddWithValue("@bx", before.X);
                    insert.Parameters.AddWithValue("@by", before.Y);
                    insert.Parameters.AddWithValue("@bz", before.Z);
                    insert.Parameters.AddWithValue("@bp", before.IsPinned ? 1 : 0);
                    insert.Parameters.AddWithValue("@ax", after!.X);
                    insert.Parameters.AddWithValue("@ay", after.Y);
                    insert.Parameters.AddWithValue("@az", after.Z);
                    insert.Parameters.AddWithValue("@ap", after.IsPinned ? 1 : 0);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                await using (var setCursor = await CommandAsync(
                    "UPDATE edit_cursor SET seq=last_insert_rowid() WHERE map_id=@map"))
                    await setCursor.ExecuteNonQueryAsync(cancellationToken);
                // Bound history per map without touching other maps or graph rows.
                await using (var bound = await CommandAsync("""
DELETE FROM graph_edits WHERE map_id=@map AND seq NOT IN
 (SELECT seq FROM graph_edits WHERE map_id=@map ORDER BY seq DESC LIMIT 100);
"""))
                    await bound.ExecuteNonQueryAsync(cancellationToken);
            }
            revision++;
        }
        await using (var saveReceipt = await CommandAsync("""
INSERT INTO edit_receipts(map_id,request_id,fingerprint,revision)
VALUES (@map,@request,@fingerprint,@revision);
"""))
        {
            saveReceipt.Parameters.AddWithValue("@request", request.RequestId);
            saveReceipt.Parameters.AddWithValue("@fingerprint", fingerprint);
            saveReceipt.Parameters.AddWithValue("@revision", revision);
            await saveReceipt.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var boundReceipts = await CommandAsync("""
DELETE FROM edit_receipts WHERE map_id=@map AND rowid NOT IN
 (SELECT rowid FROM edit_receipts WHERE map_id=@map ORDER BY rowid DESC LIMIT 1000);
"""))
            await boundReceipts.ExecuteNonQueryAsync(cancellationToken);
        long updatedCursor;
        await using (var readCursor = await CommandAsync("SELECT seq FROM edit_cursor WHERE map_id=@map"))
            updatedCursor = Convert.ToInt64(await readCursor.ExecuteScalarAsync(cancellationToken));
        var canUndo = false;
        var canRedo = false;
        await using (var undo = await CommandAsync(
            "SELECT EXISTS(SELECT 1 FROM graph_edits WHERE map_id=@map AND seq<=@cursor)"))
        {
            undo.Parameters.AddWithValue("@cursor", updatedCursor);
            canUndo = Convert.ToInt64(await undo.ExecuteScalarAsync(cancellationToken)) != 0;
        }
        await using (var redo = await CommandAsync(
            "SELECT EXISTS(SELECT 1 FROM graph_edits WHERE map_id=@map AND seq>@cursor)"))
        {
            redo.Parameters.AddWithValue("@cursor", updatedCursor);
            canRedo = Convert.ToInt64(await redo.ExecuteScalarAsync(cancellationToken)) != 0;
        }
        await transaction.CommitAsync(cancellationToken);
        return new LocalEditOutcome(revision, changed, false, after, canUndo, canRedo);
    }

    private readonly record struct EditCoordinates(double X, double Y, double Z, bool Pin);
    private readonly record struct HistoryRow(
        long Seq, string PlacementId, EditCoordinates Before, EditCoordinates After);

    private static async Task<HistoryRow?> ReadHistoryRowAsync(
        SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new HistoryRow(reader.GetInt64(0), reader.GetString(1),
            new EditCoordinates(reader.GetDouble(2), reader.GetDouble(3),
                reader.GetDouble(4), reader.GetInt64(5) != 0),
            new EditCoordinates(reader.GetDouble(6), reader.GetDouble(7),
                reader.GetDouble(8), reader.GetInt64(9) != 0));
    }

    public async Task<(bool CanUndo, bool CanRedo)> ReadEditHistoryStatusAsync(
        MapId mapId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT EXISTS(SELECT 1 FROM graph_edits e WHERE e.map_id=@map AND e.seq<=
 COALESCE((SELECT seq FROM edit_cursor WHERE map_id=@map),0)),
 EXISTS(SELECT 1 FROM graph_edits e WHERE e.map_id=@map AND e.seq>
 COALESCE((SELECT seq FROM edit_cursor WHERE map_id=@map),0));
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (false,false);
        return (reader.GetInt64(0) != 0, reader.GetInt64(1) != 0);
    }

    private static async Task<Placement?> ReadPlacementInTransactionAsync(
        SqliteConnection connection, SqliteTransaction transaction,
        MapId mapId, PlacementId placementId, CancellationToken cancellationToken)
    {
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = """
SELECT id,idea_id,parent_id,x,y,z,is_pinned,annotation
FROM placements WHERE map_id=@map AND id=@id;
""";
        read.Parameters.AddWithValue("@map", mapId.ToString());
        read.Parameters.AddWithValue("@id", placementId.ToString());
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPlacement(reader, mapId) : null;
    }

    public async Task<PersistedViewState?> ReadViewStateAsync(
        MapId mapId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM view_state WHERE map_id=@map";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        var payload = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (payload is null) return null;
        try { return JsonSerializer.Deserialize<PersistedViewState>(payload); }
        catch (JsonException) { return null; }
    }

    public async Task SaveViewStateAsync(MapId mapId, PersistedViewState state,
        CancellationToken cancellationToken = default)
    {
        if (state.Path is null || state.Path.Length > 256 ||
            state.Camera is null || state.Camera.Length != 3 ||
            state.Target is null || state.Target.Length != 3 ||
            !state.Camera.Concat(state.Target).All(double.IsFinite))
            throw new DomainRuleException("Invalid view state.");
        if (state.Up is not null && (state.Up.Length != 3 ||
            !state.Up.All(double.IsFinite) || state.Up.Sum(x => x*x) < 1e-12))
            throw new DomainRuleException("Invalid camera up vector.");
        var payload = JsonSerializer.Serialize(state);
        if (payload.Length > 16384) throw new DomainRuleException("View state too large.");
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO view_state(map_id,payload) VALUES (@map,@payload)
ON CONFLICT(map_id) DO UPDATE SET payload=excluded.payload;
""";
        command.Parameters.AddWithValue("@map", mapId.ToString());
        command.Parameters.AddWithValue("@payload", payload);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
