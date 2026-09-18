using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: generate|measure ...");
    return 2;
}

var options = Parse(args.Skip(1).ToArray());
switch (args[0].ToLowerInvariant())
{
    case "generate":
        await GenerateAsync(
            RequiredInt(options, "size"),
            RequiredInt(options, "seed"),
            Required(options, "out"));
        return 0;
    case "measure":
        await MeasureAsync(
            Required(options, "db"),
            options.TryGetValue("iterations", out var count) ? int.Parse(count) : 9,
            options.GetValueOrDefault("out"));
        return 0;
    default:
        Console.Error.WriteLine("Unknown command.");
        return 2;
}

static Dictionary<string, string> Parse(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (index + 1 >= values.Length || !values[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Options must be --name value pairs.");
        result[values[index][2..]] = values[index + 1];
    }
    return result;
}

static string Required(IReadOnlyDictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing --{name}.");

static int RequiredInt(IReadOnlyDictionary<string, string> options, string name) =>
    int.Parse(Required(options, name));

static Guid Id(byte family, long value)
{
    Span<byte> bytes = stackalloc byte[16];
    bytes[0] = family;
    BitConverter.TryWriteBytes(bytes[8..], value);
    return new Guid(bytes);
}

static async Task GenerateAsync(int size, int seed, string outputPath)
{
    if (size < 1 || size > 1_000_000) throw new ArgumentOutOfRangeException(nameof(size));
    outputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    if (File.Exists(outputPath)) File.Delete(outputPath);

    await using var store = new SqliteMapStore(outputPath);
    await store.InitializeAsync();

    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = outputPath,
        Mode = SqliteOpenMode.ReadWrite
    }.ToString();
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();
    await using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = """
PRAGMA foreign_keys=ON;
PRAGMA journal_mode=OFF;
PRAGMA synchronous=OFF;
PRAGMA temp_store=MEMORY;
DROP INDEX IF EXISTS ix_placements_parent;
DROP INDEX IF EXISTS ix_placements_idea;
DROP INDEX IF EXISTS ix_ideas_title;
DROP INDEX IF EXISTS ix_relations_source;
DROP INDEX IF EXISTS ix_relations_target;
DROP INDEX IF EXISTS ix_relations_map_id;
""";
        await pragma.ExecuteNonQueryAsync();
    }

    using var transaction = connection.BeginTransaction();
    var mapId = Id(0x40, ((long)seed << 32) ^ (uint)size);
    await InsertMapAsync(connection, transaction, mapId, size, seed);

    var wideCount = Math.Min(size - 1, Math.Max(0, Math.Min(5_000, size / 20)));
    var deepCount = Math.Min(Math.Max(0, size - wideCount - 1), 60);
    var parents = new int[size];
    parents[0] = -1;
    for (var index = 1; index < size; index++)
    {
        if (index <= wideCount)
            parents[index] = 0;
        else if (index <= wideCount + deepCount)
            parents[index] = index == wideCount + 1 ? 0 : index - 1;
        else
        {
            var anchorCount = Math.Max(1, Math.Min(index, Math.Max(1, wideCount)));
            parents[index] = 1 + PositiveMod((long)index * 1_103_515_245L + seed, anchorCount);
            if (parents[index] >= index) parents[index] = 0;
        }
    }

    await using var idea = CreateIdeaInsert(connection, transaction);
    await using var placement = CreatePlacementInsert(connection, transaction);
    var depth = new int[size];
    var degree = new int[size];
    for (var index = 0; index < size; index++)
    {
        var ideaId = Id(0x41, index);
        await InsertIdeaAsync(idea, mapId, ideaId, index);
        var parent = parents[index];
        depth[index] = parent < 0 ? 0 : depth[parent] + 1;
        if (parent >= 0) degree[parent]++;

        await InsertPlacementAsync(
            placement,
            mapId,
            Id(0x42, index),
            ideaId,
            parent < 0 ? null : Id(0x42, parent),
            Coordinate(index, seed, 17),
            Coordinate(index, seed, 31),
            Coordinate(index, seed, 47));
    }

    var duplicateCount = size / 2_000;
    for (var index = 1; index <= duplicateCount; index++)
    {
        var ideaIndex = Math.Min(size - 1, index * 1_997);
        await InsertPlacementAsync(
            placement,
            mapId,
            Id(0x44, index),
            Id(0x41, ideaIndex),
            Id(0x42, 0),
            Coordinate(ideaIndex, seed, 61),
            Coordinate(ideaIndex, seed, 73),
            Coordinate(ideaIndex, seed, 89));
        degree[0]++;
    }

    var relationCount = Math.Max(0, size / 5);
    await using var relation = CreateRelationInsert(connection, transaction);

    for (var index = 0; index < relationCount; index++)
    {
        var source = index % 10 == 0 ? 0 : PositiveMod((long)index * 37 + seed, size);
        var target = PositiveMod((long)index * 7_919 + seed * 13L + 1, size);
        if (target == source) target = (target + 1) % size;
        await InsertRelationAsync(relation, mapId, Id(0x43, index), source, target, index);
    }

    transaction.Commit();

    await using (var indexes = connection.CreateCommand())
    {
        indexes.CommandText = """
CREATE INDEX ix_placements_parent ON placements(map_id, parent_id, id);
CREATE INDEX ix_placements_idea ON placements(map_id, idea_id, id);
CREATE INDEX ix_ideas_title ON ideas(map_id, title, id);
CREATE INDEX ix_relations_source ON relations(map_id, source_idea_id, id);
CREATE INDEX ix_relations_target ON relations(map_id, target_idea_id, id);
CREATE INDEX ix_relations_map_id ON relations(map_id, id);
""";
        await indexes.ExecuteNonQueryAsync();
    }

    var file = new FileInfo(outputPath);
    var metadata = new
    {
        seed,
        ideas = size,
        placements = size + duplicateCount,
        relations = relationCount,
        maxDepth = depth.Max(),
        maxDegree = degree.Max(),
        databaseBytes = file.Length,
        profiles = new[] { "wide-hub", "deep-60", "sparse", "multi-placement" }
    };
    Console.WriteLine(JsonSerializer.Serialize(metadata));
}

static int PositiveMod(long value, int modulo)
{
    var result = value % modulo;
    return (int)(result < 0 ? result + modulo : result);
}

static double Coordinate(int index, int seed, int factor) =>
    PositiveMod((long)index * factor + seed * 97L, 2_000) - 1_000;

static async Task InsertMapAsync(
    SqliteConnection connection,
    SqliteTransaction transaction,
    Guid mapId,
    int size,
    int seed)
{
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
INSERT INTO maps(id, title, revision, schema_version)
VALUES (@id, @title, 0, 2);
""";
    command.Parameters.AddWithValue("@id", mapId.ToString("D"));
    command.Parameters.AddWithValue("@title", $"GRAPH.04 {size} seed {seed}");
    await command.ExecuteNonQueryAsync();
}

static SqliteCommand CreateIdeaInsert(SqliteConnection connection, SqliteTransaction transaction)
{
    var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
INSERT INTO ideas(id, map_id, title, content)
VALUES (@id, @map, @title, @content);
""";
    command.Parameters.Add("@id", SqliteType.Text);
    command.Parameters.Add("@map", SqliteType.Text);
    command.Parameters.Add("@title", SqliteType.Text);
    command.Parameters.Add("@content", SqliteType.Text);
    return command;
}

static async Task InsertIdeaAsync(
    SqliteCommand command,
    Guid mapId,
    Guid ideaId,
    int index)
{
    command.Parameters["@id"].Value = ideaId.ToString("D");
    command.Parameters["@map"].Value = mapId.ToString("D");
    command.Parameters["@title"].Value = $"Idea {index:D7}";
    command.Parameters["@content"].Value = $"Synthetic content {index}";
    await command.ExecuteNonQueryAsync();
}

static SqliteCommand CreatePlacementInsert(SqliteConnection connection, SqliteTransaction transaction)
{
    var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
INSERT INTO placements(id, map_id, idea_id, parent_id, x, y, z, is_pinned, annotation)
VALUES (@id, @map, @idea, @parent, @x, @y, @z, 0, '');
""";
    command.Parameters.Add("@id", SqliteType.Text);
    command.Parameters.Add("@map", SqliteType.Text);
    command.Parameters.Add("@idea", SqliteType.Text);
    command.Parameters.Add("@parent", SqliteType.Text);
    command.Parameters.Add("@x", SqliteType.Real);
    command.Parameters.Add("@y", SqliteType.Real);
    command.Parameters.Add("@z", SqliteType.Real);
    return command;
}

static async Task InsertPlacementAsync(
    SqliteCommand command,
    Guid mapId,
    Guid placementId,
    Guid ideaId,
    Guid? parentId,
    double x,
    double y,
    double z)
{
    command.Parameters["@id"].Value = placementId.ToString("D");
    command.Parameters["@map"].Value = mapId.ToString("D");
    command.Parameters["@idea"].Value = ideaId.ToString("D");
    command.Parameters["@parent"].Value = parentId is null ? DBNull.Value : parentId.Value.ToString("D");
    command.Parameters["@x"].Value = x;
    command.Parameters["@y"].Value = y;
    command.Parameters["@z"].Value = z;
    await command.ExecuteNonQueryAsync();
}

static SqliteCommand CreateRelationInsert(SqliteConnection connection, SqliteTransaction transaction)
{
    var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
INSERT INTO relations(id, map_id, source_idea_id, target_idea_id, kind, is_directed, explanation)
VALUES (@id, @map, @source, @target, @kind, @directed, '');
""";
    command.Parameters.Add("@id", SqliteType.Text);
    command.Parameters.Add("@map", SqliteType.Text);
    command.Parameters.Add("@source", SqliteType.Text);

    command.Parameters.Add("@target", SqliteType.Text);
    command.Parameters.Add("@kind", SqliteType.Text);
    command.Parameters.Add("@directed", SqliteType.Integer);
    return command;
}

static async Task InsertRelationAsync(
    SqliteCommand command,
    Guid mapId,
    Guid relationId,
    int sourceIndex,
    int targetIndex,
    int index)
{
    command.Parameters["@id"].Value = relationId.ToString("D");
    command.Parameters["@map"].Value = mapId.ToString("D");
    command.Parameters["@source"].Value = Id(0x41, sourceIndex).ToString("D");
    command.Parameters["@target"].Value = Id(0x41, targetIndex).ToString("D");
    command.Parameters["@kind"].Value = index % 7 == 0 ? "hub" : "cross";
    command.Parameters["@directed"].Value = index % 3 == 0 ? 1 : 0;
    await command.ExecuteNonQueryAsync();
}

static async Task MeasureAsync(string databasePath, int iterations, string? outputPath)
{
    if (iterations is < 3 or > 50) throw new ArgumentOutOfRangeException(nameof(iterations));
    databasePath = Path.GetFullPath(databasePath);
    if (!File.Exists(databasePath)) throw new FileNotFoundException("Benchmark database not found.", databasePath);

    var opening = Stopwatch.StartNew();
    await using var store = new SqliteMapStore(databasePath);
    await store.InitializeAsync();
    var map = await store.GetMapAsync() ?? throw new InvalidOperationException("Benchmark map missing.");
    var service = new SceneService(store);
    var root = new PlacementId(Id(0x42, 0));
    var firstProjection = await service.LoadProjectionAsync("bench-opening", root);
    var firstPayload = JsonSerializer.SerializeToUtf8Bytes(firstProjection);
    opening.Stop();

    await store.ReadChildrenPageAsync(map.Id, root, 128);
    await store.ReadRelationPageAsync(map.Id, 256);
    await store.SearchIdeasByTitlePrefixAsync(map.Id, "Idea 000", 64);
    await service.LoadProjectionAsync("bench-warmup", root);

    var childMs = new double[iterations];
    var relationMs = new double[iterations];
    var searchMs = new double[iterations];
    var projectionMs = new double[iterations];
    var cachedProjectionMs = new double[iterations];
    var serializationMs = new double[iterations];

    var projectionCache = new SceneProjectionCache();
    var cachedService = new SceneService(store, projectionCache);
    await cachedService.LoadProjectionAsync("cache-warmup", root);

    for (var index = 0; index < iterations; index++)
    {
        childMs[index] = await TimedAsync(async () =>
            await store.ReadChildrenPageAsync(map.Id, root, 128));
        relationMs[index] = await TimedAsync(async () =>
            await store.ReadRelationPageAsync(map.Id, 256));
        searchMs[index] = await TimedAsync(async () =>
            await store.SearchIdeasByTitlePrefixAsync(map.Id, "Idea 000", 64));

        SceneProjection? projection = null;
        projectionMs[index] = await TimedAsync(async () =>
            projection = await service.LoadProjectionAsync($"bench-{index}", root));
        cachedProjectionMs[index] = await TimedAsync(async () =>
            await cachedService.LoadProjectionAsync($"cache-{index}", root));
        serializationMs[index] = Timed(() =>
            JsonSerializer.SerializeToUtf8Bytes(projection!).Length);
    }

    var counts = await ReadCountsAsync(databasePath);
    var process = Process.GetCurrentProcess();
    process.Refresh();
    var result = new
    {
        database = databasePath,
        mapId = map.Id.ToString(),
        mapRevision = map.Revision,
        counts.Ideas,
        counts.Placements,
        counts.Relations,
        databaseBytes = new FileInfo(databasePath).Length,
        budgets = new SceneProjectionLimits(),

        processColdBackendMs = opening.Elapsed.TotalMilliseconds,
        firstPayloadBytes = firstPayload.Length,
        firstVisibleNodes = firstProjection.Nodes.Count,
        firstVisibleLinks = firstProjection.Links.Count,
        firstState = firstProjection.State,
        firstPartialReasons = firstProjection.PartialReasons,
        childPage = Summary(childMs),
        relationPage = Summary(relationMs),
        indexedSearch = Summary(searchMs),
        projection = Summary(projectionMs),
        cachedProjection = Summary(cachedProjectionMs),
        cacheStats = projectionCache.Stats,
        serialization = Summary(serializationMs),
        raw = new { childMs, relationMs, searchMs, projectionMs, cachedProjectionMs, serializationMs },
        workingSetBytes = process.WorkingSet64,
        peakWorkingSetBytes = process.PeakWorkingSet64,
        runtime = Environment.Version.ToString(),
        processorCount = Environment.ProcessorCount
    };

    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json);
    }
    Console.WriteLine(json);
}

static async Task<double> TimedAsync(Func<Task> action)
{
    var stopwatch = Stopwatch.StartNew();
    await action();
    stopwatch.Stop();
    return stopwatch.Elapsed.TotalMilliseconds;
}

static double Timed(Func<int> action)
{
    var stopwatch = Stopwatch.StartNew();
    _ = action();
    stopwatch.Stop();
    return stopwatch.Elapsed.TotalMilliseconds;
}

static object Summary(IEnumerable<double> samples)
{
    var ordered = samples.OrderBy(x => x).ToArray();
    return new
    {
        minMs = ordered[0],
        medianMs = ordered[ordered.Length / 2],
        p95Ms = ordered[Math.Min(ordered.Length - 1, (int)Math.Ceiling(ordered.Length * 0.95) - 1)],
        maxMs = ordered[^1]
    };
}

static async Task<(long Ideas, long Placements, long Relations)> ReadCountsAsync(string databasePath)
{
    await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
    await connection.OpenAsync();
    return (
        await CountAsync(connection, "ideas"),
        await CountAsync(connection, "placements"),
        await CountAsync(connection, "relations"));
}

static async Task<long> CountAsync(SqliteConnection connection, string table)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM {table};";
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
