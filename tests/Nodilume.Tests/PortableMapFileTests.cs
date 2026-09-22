using System.IO;
using System.Text;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class PortableMapFileTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("portable-files");
        await using (var seed = new SqliteMapStore(temp.Path))
            await DemoMapInitializer.EnsureAsync(seed);
        var catalog = new MapCatalog(temp.Root, temp.Path);
        var source = (await catalog.ListAsync()).Single();
        string sourceJson;
        await using (var store = new SqliteMapStore(source.DatabasePath))
            sourceJson = PortableMapJson.Export(await store.LoadGraphAsync());

        var exportedPath = Path.Combine(temp.Root, "backup.nodilume.json");
        await File.WriteAllTextAsync(exportedPath, "old destination");
        await catalog.ExportPortableAsync(source.Map.Id, exportedPath);
        var bytes = await File.ReadAllBytesAsync(exportedPath);
        Check.True(bytes.Length > 3
            && !(bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf),
            "Export unexpectedly wrote a UTF-8 BOM.");
        Check.Equal(sourceJson, await File.ReadAllTextAsync(exportedPath),
            "File export changed the portable document.");
        var imported = await catalog.ImportPortableAsync(exportedPath);
        Check.True(imported.Map.Id != source.Map.Id && imported.Map.Revision == 0,
            "File import reused the source map identity or revision.");
        Check.Equal(imported.Map.Id + ".sqlite", Path.GetFileName(imported.DatabasePath),
            "Imported database filename does not match its fresh MapId.");
        await using (var store = new SqliteMapStore(imported.DatabasePath))
            Check.Equal(sourceJson, PortableMapJson.Export(await store.LoadGraphAsync()),
                "Imported SQLite graph differs from the exported document.");

        var second = await catalog.ImportPortableAsync(exportedPath);
        Check.True(second.Map.Id != imported.Map.Id && second.Map.Id != source.Map.Id,
            "Repeated import did not create an independent map.");
        Check.Equal(3, (await catalog.ListAsync()).Count,
            "Imported maps were not both added to the catalog.");

        var unknownDestination = Path.Combine(temp.Root, "unknown.json");
        await File.WriteAllTextAsync(unknownDestination, "keep");
        await Check.ThrowsAsync<FileNotFoundException>(
            () => catalog.ExportPortableAsync(MapId.New(), unknownDestination),
            "Unknown map was exported.");
        Check.Equal("keep", await File.ReadAllTextAsync(unknownDestination),
            "Rejected export changed its destination.");
        var wrongExtension = Path.Combine(temp.Root, "protected.sqlite");
        await File.WriteAllTextAsync(wrongExtension, "keep");
        await Check.ThrowsAsync<ArgumentException>(
            () => catalog.ExportPortableAsync(source.Map.Id, wrongExtension),
            "Export accepted a non-JSON destination.");
        Check.Equal("keep", await File.ReadAllTextAsync(wrongExtension),
            "Rejected extension overwrote a non-JSON file.");

        await AssertRejectedImportAsync(catalog, temp.Root, "broken.json",
            async path => await File.WriteAllTextAsync(path, "{"));
        await AssertRejectedImportAsync(catalog, temp.Root, "invalid-utf8.json",
            async path => await File.WriteAllBytesAsync(path, [0xc3, 0x28]));
        await AssertRejectedImportAsync(catalog, temp.Root, "utf16.json",
            async path => await File.WriteAllTextAsync(path, sourceJson, Encoding.Unicode));
        await AssertRejectedImportAsync(catalog, temp.Root, "empty.json",
            async path => await File.WriteAllBytesAsync(path, []));
        await AssertRejectedImportAsync(catalog, temp.Root, "oversized.json",
            async path =>
            {
                await using var stream = new FileStream(path, FileMode.CreateNew);
                stream.SetLength(MapCatalog.MaxPortableFileBytes + 1);
            });

        var cancelledPath = Path.Combine(temp.Root, "cancelled.json");
        await File.WriteAllTextAsync(cancelledPath, "keep");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(
            () => catalog.ExportPortableAsync(source.Map.Id, cancelledPath, cancelled.Token),
            "Cancelled export was accepted.");
        Check.Equal("keep", await File.ReadAllTextAsync(cancelledPath),
            "Cancelled export changed its destination.");

        await using var sourceStore = new SqliteMapStore(source.DatabasePath);
        Check.Equal(sourceJson, PortableMapJson.Export(await sourceStore.LoadGraphAsync()),
            "Import/export changed the source map.");
        Check.True(!Directory.EnumerateFiles(temp.Root, "*.tmp").Any()
            && !Directory.EnumerateFiles(temp.Root, "*.sqlite-wal").Any()
            && !Directory.EnumerateFiles(temp.Root, "*.sqlite-shm").Any(),
            "Portable file operations left temporary database artifacts.");
    }

    private static async Task AssertRejectedImportAsync(
        MapCatalog catalog,
        string root,
        string name,
        Func<string, Task> create)
    {
        var path = Path.Combine(root, name);
        await create(path);
        var before = GuidDatabaseCount(root);
        await Check.ThrowsAsync<InvalidDataException>(
            () => catalog.ImportPortableAsync(path),
            "Invalid portable file was imported: " + name);
        Check.Equal(before, GuidDatabaseCount(root),
            "Rejected import left a catalog database: " + name);
    }

    private static int GuidDatabaseCount(string root) =>
        Directory.EnumerateFiles(root, "*.sqlite")
            .Count(path => Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(path), "D", out _));
}
