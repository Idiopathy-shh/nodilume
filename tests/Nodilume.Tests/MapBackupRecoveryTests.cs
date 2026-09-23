using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class MapBackupRecoveryTests
{
    private const int LargePayloadBytes = 65 * 1024 * 1024;

    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("backup-recovery");
        await using (var seed = new SqliteMapStore(temp.Path))
            await DemoMapInitializer.EnsureAsync(seed);
        var catalog = new MapCatalog(temp.Root, temp.Path);
        var initial = (await catalog.ListAsync()).Single();
        var source = await catalog.RenameAsync(
            initial.Map.Id,
            initial.Map.Revision,
            "Backup source");
        await AddLargePayloadAsync(source.DatabasePath);
        Check.True(new FileInfo(source.DatabasePath).Length
            > MapCatalog.MaxPortableFileBytes,
            "Backup fixture did not exceed the portable JSON limit.");
        string sourceJson;
        await using (var store =
                     new SqliteMapStore(source.DatabasePath))
            sourceJson = PortableMapJson.Export(
                await store.LoadGraphAsync());

        var backup = await catalog.CreateBackupAsync(
            source.Map.Id, retentionCount: 100);
        Check.True(File.Exists(backup.FilePath),
            "Backup package was not created.");
        Check.Equal(catalog.BackupDirectory,
            Path.GetDirectoryName(backup.FilePath)!,
            "Backup was written outside the managed directory.");
        Check.Equal(source.Map.Id, backup.SourceMapId,
            "Backup metadata has the wrong source MapId.");
        Check.Equal(source.Map.Revision, backup.Revision,
            "Backup did not preserve the source revision.");
        var package = await ReadPackageAsync(backup.FilePath);
        Check.Equal(2, package.EntryCount,
            "Backup package has unexpected entries.");
        Check.Equal(source.Map.Id.ToString(),
            package.Manifest["sourceMapId"]!.GetValue<string>(),
            "Manifest source MapId mismatch.");
        Check.Equal(source.Map.Revision,
            package.Manifest["revision"]!.GetValue<long>(),
            "Manifest revision mismatch.");
        Check.Equal(package.Database.LongLength,
            package.Manifest["databaseBytes"]!.GetValue<long>(),
            "Manifest database length mismatch.");
        Check.Equal(
            Convert.ToHexString(SHA256.HashData(package.Database))
                .ToLowerInvariant(),
            package.Manifest["databaseSha256"]!.GetValue<string>(),
            "Manifest checksum mismatch.");

        var restored = await catalog.RestoreBackupAsync(
            backup.FilePath);
        Check.True(restored.Map.Id != source.Map.Id,
            "Restore reused the source MapId.");
        Check.Equal(source.Map.Revision, restored.Map.Revision,
            "Restore reset or changed the source revision.");
        Check.Equal(restored.Map.Id + ".sqlite",
            Path.GetFileName(restored.DatabasePath),
            "Restored filename does not match its fresh MapId.");
        await AssertGraphEqualsAsync(
            sourceJson, restored.DatabasePath);
        await AssertLargePayloadAsync(restored.DatabasePath);

        var second = await catalog.RestoreBackupAsync(
            backup.FilePath);
        Check.True(second.Map.Id != restored.Map.Id
            && second.Map.Id != source.Map.Id,
            "Repeated restore did not create an independent map.");
        await AssertGraphEqualsAsync(sourceJson, second.DatabasePath);
        await AssertLargePayloadAsync(second.DatabasePath);
        var catalogCount = (await catalog.ListAsync()).Count;
        var badChecksum = Path.Combine(
            temp.Root, "bad-checksum" + MapCatalog.BackupExtension);
        var checksumBytes = package.Database.ToArray();
        checksumBytes[^1] ^= 0x5a;
        await WritePackageAsync(
            badChecksum, package.Manifest, checksumBytes);
        await AssertRejectedAsync(
            catalog, temp.Root, badChecksum, catalogCount,
            "Checksum mismatch was accepted.");

        var badManifest = Path.Combine(
            temp.Root, "bad-manifest" + MapCatalog.BackupExtension);
        var changedManifest =
            (JsonObject)package.Manifest.DeepClone();
        changedManifest["revision"] = source.Map.Revision + 50;
        await WritePackageAsync(
            badManifest, changedManifest, package.Database);
        await AssertRejectedAsync(
            catalog, temp.Root, badManifest, catalogCount,
            "Manifest/database disagreement was accepted.");

        var corruptDatabase = Path.Combine(
            temp.Root, "corrupt-db" + MapCatalog.BackupExtension);
        var corruptBytes = package.Database.ToArray();
        corruptBytes[0] ^= 0xff;
        var corruptManifest =
            (JsonObject)package.Manifest.DeepClone();
        corruptManifest["databaseSha256"] =
            Convert.ToHexString(SHA256.HashData(corruptBytes))
                .ToLowerInvariant();
        await WritePackageAsync(
            corruptDatabase, corruptManifest, corruptBytes);
        await AssertRejectedAsync(
            catalog, temp.Root, corruptDatabase, catalogCount,
            "Corrupt SQLite snapshot was accepted.");
        var extraEntry = Path.Combine(
            temp.Root, "extra-entry" + MapCatalog.BackupExtension);
        await WritePackageAsync(
            extraEntry,
            package.Manifest,
            package.Database,
            includeExtraEntry: true);
        await AssertRejectedAsync(
            catalog, temp.Root, extraEntry, catalogCount,
            "Backup with an extra entry was accepted.");

        await Check.ThrowsAsync<ArgumentException>(
            () => catalog.RestoreBackupAsync(
                backup.FilePath + ".zip"),
            "Restore accepted the wrong extension.");
        Check.Equal(catalogCount, (await catalog.ListAsync()).Count,
            "Rejected extension changed the catalog.");

        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await Check.ThrowsAsync<OperationCanceledException>(
                () => catalog.CreateBackupAsync(
                    source.Map.Id, 100, cancelled.Token),
                "Cancelled backup was accepted.");
            await Check.ThrowsAsync<OperationCanceledException>(
                () => catalog.RestoreBackupAsync(
                    backup.FilePath, cancelled.Token),
                "Cancelled restore was accepted.");
        }
        Check.Equal(catalogCount, (await catalog.ListAsync()).Count,
            "Cancelled recovery changed the catalog.");

        var foreign = Path.Combine(
            catalog.BackupDirectory,
            source.Map.Id + "-foreign"
                + MapCatalog.BackupExtension);
        await File.WriteAllTextAsync(foreign, "not our package");
        for (var index = 0; index < 5; index++)
        {
            await catalog.CreateBackupAsync(
                source.Map.Id, retentionCount: 3);
            await Task.Delay(2);
        }
        var retained = Directory.EnumerateFiles(
                catalog.BackupDirectory,
                source.Map.Id + "-*" + MapCatalog.BackupExtension)
            .Where(path => path != foreign)
            .ToArray();
        Check.Equal(3, retained.Length,
            "Explicit retention did not keep exactly three backups.");
        Check.True(File.Exists(foreign),
            "Retention deleted an unverified matching file.");

        await using (var sourceStore =
                     new SqliteMapStore(source.DatabasePath))
            Check.Equal(sourceJson, PortableMapJson.Export(
                    await sourceStore.LoadGraphAsync()),
                "Backup or restore modified the source map.");
        Check.True(
            !Directory.EnumerateFiles(
                    temp.Root, "*.restore.tmp")
                .Any()
            && !Directory.EnumerateFiles(
                    catalog.BackupDirectory, "*.tmp")
                .Any(),
            "Backup/recovery left temporary artifacts.");
    }

    private static async Task AddLargePayloadAsync(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        await using var connection =
            new SqliteConnection(builder.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE backup_probe (
    id INTEGER PRIMARY KEY,
    payload BLOB NOT NULL
);
INSERT INTO backup_probe(id, payload) VALUES (1, zeroblob(@bytes));
""";
        command.Parameters.AddWithValue("@bytes", LargePayloadBytes);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertLargePayloadAsync(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using var connection =
            new SqliteConnection(builder.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT length(payload) FROM backup_probe WHERE id = 1;";
        Check.Equal(LargePayloadBytes,
            Convert.ToInt32(await command.ExecuteScalarAsync()),
            "Restore did not preserve the complete SQLite database.");
    }

    private static async Task AssertGraphEqualsAsync(
        string expected,
        string databasePath)
    {
        await using var store =
            new SqliteMapStore(databasePath);
        Check.Equal(expected,
            PortableMapJson.Export(await store.LoadGraphAsync()),
            "Restored graph differs from the backup source.");
    }

    private static async Task AssertRejectedAsync(
        MapCatalog catalog,
        string root,
        string backupPath,
        int expectedCatalogCount,
        string message)
    {
        await Check.ThrowsAsync<InvalidDataException>(
            () => catalog.RestoreBackupAsync(backupPath),
            message);
        Check.Equal(expectedCatalogCount,
            (await catalog.ListAsync()).Count,
            "Rejected restore changed the catalog.");
        Check.True(!Directory.EnumerateFiles(
                root, "*.restore.tmp").Any(),
            "Rejected restore left a temporary database.");
    }

    private static async Task<PackageData> ReadPackageAsync(
        string path)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(
            stream, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException(
                "Manifest entry missing.");
        var databaseEntry = archive.GetEntry("map.sqlite")
            ?? throw new InvalidOperationException(
                "Database entry missing.");
        JsonObject manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = (JsonNode.Parse(manifestStream)
                as JsonObject)
                ?? throw new InvalidOperationException(
                    "Manifest is not an object.");
        }
        byte[] database;
        await using (var databaseStream = databaseEntry.Open())
        using (var memory = new MemoryStream())
        {
            await databaseStream.CopyToAsync(memory);
            database = memory.ToArray();
        }
        return new PackageData(
            manifest, database, archive.Entries.Count);
    }

    private static async Task WritePackageAsync(
        string path,
        JsonObject manifest,
        byte[] database,
        bool includeExtraEntry = false)
    {
        await using var output = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(
            output, ZipArchiveMode.Create, leaveOpen: true);
        var manifestEntry = archive.CreateEntry(
            "manifest.json", CompressionLevel.NoCompression);
        await using (var manifestStream = manifestEntry.Open())
            await JsonSerializer.SerializeAsync(
                manifestStream, manifest);
        var databaseEntry = archive.CreateEntry(
            "map.sqlite", CompressionLevel.NoCompression);
        await using (var databaseStream = databaseEntry.Open())
            await databaseStream.WriteAsync(database);
        if (includeExtraEntry)
        {
            var extra = archive.CreateEntry("extra.txt");
            await using var extraStream = extra.Open();
            await extraStream.WriteAsync(new byte[] { 1 });
        }
    }

    private sealed record PackageData(
        JsonObject Manifest,
        byte[] Database,
        int EntryCount);
}