using System.Text;
using Microsoft.Data.Sqlite;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

public sealed record CatalogMap(MapInfo Map, string DatabasePath)
{
    public override string ToString() => $"{Map.Title}  ·  {Map.Id.ToString()[..8]}";
}

/// <summary>Maps stored in a single application-controlled folder; no discovery outside it.</summary>
public sealed partial class MapCatalog
{
    public const long MaxPortableFileBytes = 64L * 1024 * 1024;
    private const string SelectionFile = "active-map.txt";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string _directory;
    private readonly string _legacyPath;

    public MapCatalog(string directory, string legacyDatabasePath)
    {
        _directory = Path.GetFullPath(directory);
        _legacyPath = Path.GetFullPath(legacyDatabasePath);
        if (!Path.GetDirectoryName(_legacyPath)!.Equals(_directory,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Legacy map must belong to the catalog directory.");
    }

    public async Task<IReadOnlyList<CatalogMap>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return [];
        var result = new List<CatalogMap>();
        var ids = new HashSet<MapId>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.sqlite", SearchOption.TopDirectoryOnly)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var legacy = path.Equals(_legacyPath, StringComparison.OrdinalIgnoreCase);
            MapId? fileId = null;
            if (!legacy)
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var guid)
                    || guid == Guid.Empty) continue;
                fileId = new MapId(guid);
            }
            // Read-only: enumeration must never create, migrate or modify a database.
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            };
            try
            {
                await using var connection = new SqliteConnection(builder.ToString());
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT id, title, revision, schema_version FROM maps LIMIT 2;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidDataException("Map database has no map: " + Path.GetFileName(path));
                var map = new MapInfo(MapId.Parse(reader.GetString(0)), reader.GetString(1),
                    reader.GetInt64(2), reader.GetInt32(3));
                if (await reader.ReadAsync(cancellationToken))
                    throw new InvalidDataException("Map database contains multiple maps.");
                if (fileId is not null && fileId != map.Id)
                    throw new InvalidDataException("Map filename and stored identity disagree.");
                if (!ids.Add(map.Id))
                    throw new InvalidDataException("Duplicate map identity in the catalog.");
                result.Add(new CatalogMap(map, path));
            }
            catch (SqliteException ex)
            {
                throw new InvalidDataException(
                    "Map database cannot be read: " + Path.GetFileName(path), ex);
            }
        }
        return result.OrderBy(x => x.Map.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Map.Id.ToString(), StringComparer.Ordinal).ToArray();
    }

    public static string ValidateTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 120
            || trimmed.Any(char.IsControl))
            throw new ArgumentException("Map name must contain 1–120 printable characters.", nameof(title));
        return trimmed;
    }

    public Task<CatalogMap> CreateAsync(
        string title, CancellationToken cancellationToken = default)
    {
        var graph = new MapGraph(new MapInfo(
            MapId.New(), ValidateTitle(title), 0, SqliteSchema.CurrentVersion));
        return CreateDatabaseAsync(graph, cancellationToken);
    }

    public async Task<CatalogMap> RenameAsync(MapId id, long expectedRevision, string title,
        CancellationToken cancellationToken = default)
    {
        var valid = ValidateTitle(title);
        var entry = (await ListAsync(cancellationToken)).SingleOrDefault(x => x.Map.Id == id)
            ?? throw new FileNotFoundException("Map not found in the catalog.");
        await using var store = new SqliteMapStore(entry.DatabasePath);
        var updated = await store.RenameMapAsync(id, expectedRevision, valid, cancellationToken);
        return new CatalogMap(updated, entry.DatabasePath);
    }

    public async Task<CatalogMap?> RestoreSelectionAsync(CancellationToken cancellationToken = default)
    {
        var maps = await ListAsync(cancellationToken);
        if (maps.Count == 0) return null;
        var selectionPath = Path.Combine(_directory, SelectionFile);
        if (File.Exists(selectionPath) && new FileInfo(selectionPath).Length <= 128)
        {
            var selected = (await File.ReadAllTextAsync(selectionPath, cancellationToken)).Trim();
            if (Guid.TryParseExact(selected, "D", out var guid))
            {
                var match = maps.FirstOrDefault(x => x.Map.Id == new MapId(guid));
                if (match is not null) return match;
            }
        }
        return maps.FirstOrDefault(x => x.DatabasePath.Equals(_legacyPath,
                   StringComparison.OrdinalIgnoreCase)) ?? maps[0];
    }

    public async Task<CatalogMap> SelectAsync(MapId id, CancellationToken cancellationToken = default)
    {
        var selected = (await ListAsync(cancellationToken)).SingleOrDefault(x => x.Map.Id == id)
            ?? throw new FileNotFoundException("Map not found in the catalog.");
        Directory.CreateDirectory(_directory);
        var selectionPath = Path.Combine(_directory, SelectionFile);
        var tempPath = Path.Combine(_directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, selected.Map.Id.ToString(), cancellationToken);
            File.Move(tempPath, selectionPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        return selected;
    }
}
