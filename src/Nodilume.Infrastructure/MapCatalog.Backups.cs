using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

public sealed record MapBackupInfo(
    string FilePath,
    MapId SourceMapId,
    string Title,
    long Revision,
    int MapSchemaVersion,
    int DatabaseSchemaVersion,
    DateTimeOffset CreatedAtUtc,
    long DatabaseBytes,
    string DatabaseSha256,
    string? RetentionWarning);

public sealed partial class MapCatalog
{
    public const int DefaultBackupRetentionCount = 10;
    public const long MaxBackupArchiveBytes = 8L * 1024 * 1024 * 1024;
    public const string BackupExtension = ".nodilume-backup";
    private const int BackupFormatVersion = 1;
    private const int MaxManifestBytes = 64 * 1024;
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string BackupDirectory => Path.Combine(_directory, "Backups");
    public async Task<MapBackupInfo> CreateBackupAsync(
        MapId id,
        int retentionCount = DefaultBackupRetentionCount,
        CancellationToken cancellationToken = default)
    {
        if (retentionCount is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(retentionCount));
        var source = (await ListAsync(cancellationToken))
            .SingleOrDefault(x => x.Map.Id == id)
            ?? throw new FileNotFoundException("Map not found in the catalog.");
        Directory.CreateDirectory(BackupDirectory);
        var operationId = Guid.NewGuid().ToString("N");
        var snapshotPath = Path.Combine(
            BackupDirectory, "." + operationId + ".sqlite.tmp");
        var packagePath = Path.Combine(
            BackupDirectory, "." + operationId + ".backup.tmp");
        var createdAt = DateTimeOffset.UtcNow;
        var fileName = id + "-" + createdAt.ToString("yyyyMMddTHHmmssfffffff'Z'")
            + "-" + operationId[..8] + BackupExtension;
        var destination = Path.Combine(BackupDirectory, fileName);

        try
        {
            await CreateOnlineSnapshotAsync(
                source.DatabasePath, snapshotPath, cancellationToken);
            var snapshot = await ReadSnapshotMetadataAsync(
                snapshotPath, cancellationToken);
            if (snapshot.Map.Id != source.Map.Id)
                throw new InvalidDataException(
                    "Backup snapshot identity differs from the catalog map.");
            var databaseBytes = new FileInfo(snapshotPath).Length;
            if (databaseBytes is <= 0 or > MaxBackupArchiveBytes)
                throw new InvalidDataException(
                    "Backup database is empty or exceeds the 8 GiB limit.");
            var sha256 = await ComputeSha256Async(
                snapshotPath, cancellationToken);
            var manifest = new BackupManifest(
                BackupFormatVersion,
                createdAt,
                snapshot.Map.Id.ToString(),
                snapshot.Map.Title,
                snapshot.Map.Revision,
                snapshot.Map.SchemaVersion,
                snapshot.DatabaseSchemaVersion,
                databaseBytes,
                sha256);
            await WritePackageAsync(
                packagePath, snapshotPath, manifest, cancellationToken);
            File.Move(packagePath, destination);
            var retentionWarning = await ApplyRetentionAsync(
                id, retentionCount, cancellationToken);
            return ToBackupInfo(
                destination, manifest, retentionWarning);
        }
        finally
        {
            DeleteDatabaseArtifacts(snapshotPath);
            if (File.Exists(packagePath)) File.Delete(packagePath);
        }
    }

    public async Task<CatalogMap> RestoreBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        var source = ValidateBackupPath(backupPath);
        Directory.CreateDirectory(_directory);
        var restoreId = MapId.New();
        var destination = Path.Combine(_directory, restoreId + ".sqlite");
        var temporary = Path.Combine(
            _directory, "." + Guid.NewGuid().ToString("N") + ".restore.tmp");
        try
        {
            var manifest = await ExtractVerifiedDatabaseAsync(
                source, temporary, cancellationToken);
            SnapshotMetadata snapshot;
            try
            {
                snapshot = await ReadSnapshotMetadataAsync(
                    temporary, cancellationToken);
            }
            catch (Exception ex) when (
                ex is SqliteException
                    or FormatException
                    or ArgumentException)
            {
                throw new InvalidDataException(
                    "Backup database is not a readable SQLite database.", ex);
            }
            ValidateSnapshotMatchesManifest(snapshot, manifest);
            await ReassignMapIdentityAsync(
                temporary,
                snapshot.Map.Id,
                restoreId,
                snapshot.DatabaseSchemaVersion,
                cancellationToken);
            var restored = await ReadSnapshotMetadataAsync(
                temporary, cancellationToken);
            if (restored.Map.Id != restoreId
                || restored.Map.Title != snapshot.Map.Title
                || restored.Map.Revision != snapshot.Map.Revision)
                throw new InvalidDataException(
                    "Restored database identity or metadata is inconsistent.");
            File.Move(temporary, destination);
            return new CatalogMap(restored.Map, destination);
        }
        finally
        {
            DeleteDatabaseArtifacts(temporary);
        }
    }

    private static string ValidateBackupPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Backup path is required.", nameof(path));
        var full = Path.GetFullPath(path);
        if (!full.EndsWith(
                BackupExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Backup path must use the "
                + BackupExtension + " extension.", nameof(path));
        if (!File.Exists(full))
            throw new FileNotFoundException(
                "Backup file was not found.", full);
        return full;
    }

    private static MapBackupInfo ToBackupInfo(
        string path,
        BackupManifest manifest,
        string? retentionWarning) =>
        new(
            path,
            MapId.Parse(manifest.SourceMapId),
            manifest.Title,
            manifest.Revision,
            manifest.MapSchemaVersion,
            manifest.DatabaseSchemaVersion,
            manifest.CreatedAtUtc,
            manifest.DatabaseBytes,
            manifest.DatabaseSha256,
            retentionWarning);

    private sealed record SnapshotMetadata(
        MapInfo Map,
        int DatabaseSchemaVersion);

    private sealed record BackupManifest(
        int FormatVersion,
        DateTimeOffset CreatedAtUtc,
        string SourceMapId,
        string Title,
        long Revision,
        int MapSchemaVersion,
        int DatabaseSchemaVersion,
        long DatabaseBytes,
        string DatabaseSha256);
}