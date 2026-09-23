using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

public sealed partial class MapCatalog
{
    private static async Task WritePackageAsync(
        string packagePath,
        string databasePath,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestBytes =
            JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson);
        if (manifestBytes.Length > MaxManifestBytes)
            throw new InvalidDataException("Backup manifest is too large.");
        await using var output = new FileStream(
            packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 128 * 1024, useAsync: true);
        using (var archive = new ZipArchive(
                   output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry(
                "manifest.json", CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
                await manifestStream.WriteAsync(
                    manifestBytes, cancellationToken);

            var databaseEntry = archive.CreateEntry(
                "map.sqlite", CompressionLevel.Fastest);
            await using var databaseStream = databaseEntry.Open();
            await using var database = new FileStream(
                databasePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, useAsync: true);
            await database.CopyToAsync(
                databaseStream, 128 * 1024, cancellationToken);
        }
        output.Flush(flushToDisk: true);
    }

    private async Task<string?> ApplyRetentionAsync(
        MapId mapId,
        int retentionCount,
        CancellationToken cancellationToken)
    {
        var managed = new List<(string Path, DateTimeOffset CreatedAt)>();
        foreach (var path in Directory.EnumerateFiles(
                     BackupDirectory,
                     mapId + "-*" + BackupExtension,
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await TryReadManifestAsync(path, cancellationToken);
            if (manifest is not null
                && manifest.SourceMapId == mapId.ToString())
                managed.Add((path, manifest.CreatedAtUtc));
        }

        foreach (var obsolete in managed
                     .OrderByDescending(item => item.CreatedAt)
                     .ThenByDescending(
                         item => Path.GetFileName(item.Path),
                         StringComparer.Ordinal)
                     .Skip(retentionCount))
        {            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Delete(obsolete.Path);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }
        return null;
    }

    private static async Task<BackupManifest?> TryReadManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);
            using var archive = new ZipArchive(
                stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null
                || entry.Length is <= 0 or > MaxManifestBytes)
                return null;
            await using var entryStream = entry.Open();
            var bytes = await ReadLimitedAsync(
                entryStream, MaxManifestBytes, cancellationToken);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(
                bytes, ManifestJson);
            ValidateManifest(manifest);
            return manifest;
        }
        catch (Exception exception) when (
            exception is InvalidDataException                or IOException
                or JsonException
                or ArgumentException)
        {
            return null;
        }
    }

    private static async Task<BackupManifest> ExtractVerifiedDatabaseAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024, useAsync: true);
        if (input.Length is <= 0 or > MaxBackupArchiveBytes)
            throw new InvalidDataException(
                "Backup archive is empty or exceeds the 8 GiB limit.");
        using var archive = new ZipArchive(
            input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count != 2
            || archive.Entries.Any(entry => entry.FullName != entry.Name))
            throw new InvalidDataException(
                "Backup must contain exactly two top-level entries.");

        var manifests = archive.Entries
            .Where(entry => entry.Name == "manifest.json")
            .ToArray();
        var databases = archive.Entries
            .Where(entry => entry.Name == "map.sqlite")
            .ToArray();
        if (manifests.Length != 1 || databases.Length != 1)
            throw new InvalidDataException(
                "Backup manifest or database entry "
                + "is missing or duplicated.");
        if (manifests[0].Length is <= 0 or > MaxManifestBytes)
            throw new InvalidDataException(
                "Backup manifest has invalid size.");
        await using var manifestStream = manifests[0].Open();
        var manifestBytes = await ReadLimitedAsync(
            manifestStream, MaxManifestBytes, cancellationToken);
        BackupManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                manifestBytes, ManifestJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Backup manifest is not valid JSON.", ex);
        }
        ValidateManifest(manifest);
        if (databases[0].Length != manifest!.DatabaseBytes)
            throw new InvalidDataException(
                "Backup database length differs from the manifest.");

        await using var databaseEntry = databases[0].Open();
        await using var output = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 128 * 1024, useAsync: true);
        using var hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            var read = await databaseEntry.ReadAsync(
                buffer, cancellationToken);
            if (read == 0) break;
            total = checked(total + read);            if (total > manifest.DatabaseBytes)
                throw new InvalidDataException(
                    "Backup database exceeds the manifest length.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(
                buffer.AsMemory(0, read), cancellationToken);
        }
        output.Flush(flushToDisk: true);
        if (total != manifest.DatabaseBytes)
            throw new InvalidDataException(
                "Backup database is truncated.");
        var expectedHash = Convert.FromHexString(
            manifest.DatabaseSha256);
        var actualHash = hash.GetHashAndReset();
        if (!CryptographicOperations.FixedTimeEquals(
                expectedHash, actualHash))
            throw new InvalidDataException(
                "Backup database checksum does not match the manifest.");
        return manifest;
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        int limit,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(
                buffer, cancellationToken);
            if (read == 0) break;
            if (memory.Length + read > limit)
                throw new InvalidDataException(
                    "Backup manifest exceeds its size limit.");
            await memory.WriteAsync(
                buffer.AsMemory(0, read), cancellationToken);        }
        return memory.ToArray();
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(
            stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateManifest(BackupManifest? manifest)
    {
        if (manifest is null
            || manifest.FormatVersion != BackupFormatVersion)
            throw new InvalidDataException(
                "Backup format version is not supported.");
        if (!Guid.TryParseExact(
                manifest.SourceMapId, "D", out var id)
            || id == Guid.Empty)
            throw new InvalidDataException(
                "Backup source MapId is invalid.");
        try
        {
            ValidateTitle(manifest.Title);
            _ = Convert.FromHexString(
                manifest.DatabaseSha256);
        }
        catch (Exception ex) when (
            ex is ArgumentException or FormatException)
        {
            throw new InvalidDataException(
                "Backup manifest contains invalid metadata.", ex);
        }
        if (manifest.Revision < 0
            || manifest.MapSchemaVersion is < 1
                or > SqliteSchema.CurrentVersion
            || manifest.DatabaseSchemaVersion is < 1
                or > SqliteSchema.CurrentVersion
            || manifest.DatabaseBytes is <= 0
                or > MaxBackupArchiveBytes
            || manifest.DatabaseSha256.Length != 64
            || manifest.CreatedAtUtc == default)
            throw new InvalidDataException(
                "Backup manifest contains unsupported values.");
    }
}