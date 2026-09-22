using System.Text;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

public sealed partial class MapCatalog
{
    public async Task ExportPortableAsync(
        MapId id,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var destination = ValidatePortablePath(destinationPath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("Export path has no directory.", nameof(destinationPath));
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("Export directory does not exist.");

        var entry = (await ListAsync(cancellationToken)).SingleOrDefault(x => x.Map.Id == id)
            ?? throw new FileNotFoundException("Map not found in the catalog.");
        await using var store = new SqliteMapStore(entry.DatabasePath);
        var json = PortableMapJson.Export(await store.LoadGraphAsync(cancellationToken));
        var bytes = StrictUtf8.GetBytes(json);
        if (bytes.LongLength > MaxPortableFileBytes)
            throw new InvalidDataException("Portable map file exceeds the 64 MiB limit.");
        var temporary = Path.Combine(directory,
            "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<CatalogMap> ImportPortableAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        var source = ValidatePortablePath(sourcePath);
        string json;
        try
        {
            await using var stream = new FileStream(
                source, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);
            if (stream.Length is <= 0 or > MaxPortableFileBytes)
                throw new InvalidDataException(
                    "Portable map file is empty or exceeds the 64 MiB limit.");
            using var reader = new StreamReader(
                stream, StrictUtf8, detectEncodingFromByteOrderMarks: false,
                bufferSize: 64 * 1024, leaveOpen: true);
            json = await reader.ReadToEndAsync(cancellationToken);
            if (json.Length > 0 && json[0] == '\uFEFF') json = json[1..];
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Portable map file is not valid UTF-8.", ex);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var graph = PortableMapJson.Import(
            json, MapId.New(), SqliteSchema.CurrentVersion);
        return await CreateDatabaseAsync(graph, cancellationToken);
    }

    private async Task<CatalogMap> CreateDatabaseAsync(
        MapGraph graph,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, graph.Map.Id + ".sqlite");
        await using (var reservation = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 1, useAsync: true))
        {
        }
        try
        {
            await using var store = new SqliteMapStore(path);
            await store.InitializeAsync(cancellationToken);
            await store.CreateMapAsync(graph, cancellationToken);
            return new CatalogMap(graph.Map, path);
        }
        catch
        {
            DeleteDatabaseArtifacts(path);
            throw;
        }
    }

    private static string ValidatePortablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Portable map path is required.", nameof(path));
        var full = Path.GetFullPath(path);
        if (!Path.GetExtension(full).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Portable map path must use the .json extension.", nameof(path));
        return full;
    }

    private static void DeleteDatabaseArtifacts(string path)
    {
        foreach (var candidate in new[] { path, path + "-wal", path + "-shm" })
            if (File.Exists(candidate)) File.Delete(candidate);
    }
}
