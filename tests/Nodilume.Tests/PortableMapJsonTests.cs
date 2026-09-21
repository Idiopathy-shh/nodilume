using System.IO;
using System.Text.Json.Nodes;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class PortableMapJsonTests
{
    public static async Task RunAsync()
    {
        var source = DemoMapFactory.Create();
        var shared = source.Ideas.Values.Single(x =>
            source.Placements.Values.Count(p => p.IdeaId == x.Id) == 2);
        source.UpdateIdea(shared.Id, "Università 🌱", "Note condivise <b>solo testo</b>");
        var pinned = source.Placements.Values.First(x => x.ParentId is not null);
        source.SetPlacementDetails(pinned.Id, true, "Nota locale: àèìòù");

        var exported = PortableMapJson.Export(source);
        Check.Equal(exported, PortableMapJson.Export(source),
            "Portable export must have stable ordering.");
        var mapId = MapId.New();
        var imported = PortableMapJson.Import(exported, mapId, 2);
        Check.True(imported.Map.Id != source.Map.Id && imported.Map.Revision == 0,
            "Import reused source map identity or revision.");
        Check.Equal(2, imported.Map.SchemaVersion, "Destination SQLite schema was not selected.");
        Check.Equal(source.Map.Title, imported.Map.Title, "Map title changed.");
        Check.Equal(source.Ideas.Count, imported.Ideas.Count, "Ideas lost.");
        Check.Equal(source.Placements.Count, imported.Placements.Count, "Placements lost.");
        Check.Equal(source.Relations.Count, imported.Relations.Count, "Relations lost.");
        foreach (var idea in source.Ideas.Values)
            Check.Equal(idea with { MapId = mapId }, imported.Ideas[idea.Id],
                "Shared idea identity/content changed.");
        foreach (var placement in source.Placements.Values)
            Check.Equal(placement with { MapId = mapId }, imported.Placements[placement.Id],
                "Parent, local geometry, pin or annotation changed.");
        foreach (var relation in source.Relations.Values)
            Check.Equal(relation with { MapId = mapId }, imported.Relations[relation.Id],
                "Conceptual relation/direction changed.");
        Check.Equal(exported, PortableMapJson.Export(imported),
            "Portable JSON changed after import into another map.");

        await using var temp = new TempDatabase("portable-map");
        await using (var store = new SqliteMapStore(temp.Path))
        {
            await store.InitializeAsync();
            await store.CreateMapAsync(imported);
            var persisted = await store.LoadGraphAsync();
            Check.Equal(mapId, persisted.Map.Id, "Imported SQLite map identity changed.");
            Check.Equal(exported, PortableMapJson.Export(persisted),
                "Portable JSON was not preserved through SQLite.");
            await Check.ThrowsAsync<InvalidOperationException>(
                () => store.CreateMapAsync(PortableMapJson.Import(exported, MapId.New(), 2)),
                "Import must never overwrite an initialized map database.");
            Check.Equal(mapId, (await store.GetMapAsync())!.Id,
                "Rejected import overwrote an existing map.");
        }
        var independent = PortableMapJson.Import(exported, MapId.New(), 2);
        Check.True(independent.Map.Id != mapId,
            "Two imports of the same document must be independent maps.");

        ExpectInvalid("{", mapId);
        ExpectInvalid("null", mapId);
        ExpectInvalid("{}", mapId);
        Check.Throws<InvalidDataException>(() =>
            PortableMapJson.Import("", mapId, 2), "Empty document accepted.");
        var original = JsonNode.Parse(exported)!;
        var future = original.DeepClone();
        future["formatVersion"] = 99;
        ExpectInvalid(future.ToJsonString(), mapId);
        var missing = original.DeepClone();
        missing.AsObject().Remove("ideas");
        ExpectInvalid(missing.ToJsonString(), mapId);
        var duplicate = original.DeepClone();
        duplicate["ideas"]!.AsArray().Add(duplicate["ideas"]![0]!.DeepClone());
        ExpectInvalid(duplicate.ToJsonString(), mapId);
        var dangling = original.DeepClone();
        dangling["placements"]![0]!["ideaId"] = Guid.NewGuid().ToString("D");
        ExpectInvalid(dangling.ToJsonString(), mapId);
        var duplicatePlacement = original.DeepClone();
        duplicatePlacement["placements"]!.AsArray().Add(
            duplicatePlacement["placements"]![0]!.DeepClone());
        ExpectInvalid(duplicatePlacement.ToJsonString(), mapId);
        var danglingRelation = original.DeepClone();
        danglingRelation["relations"]![0]!["targetIdeaId"] = Guid.NewGuid().ToString("D");
        ExpectInvalid(danglingRelation.ToJsonString(), mapId);
        var cycle = original.DeepClone();
        var rootRow = cycle["placements"]!.AsArray().Single(x => x!["parentId"] is null)!;
        rootRow["parentId"] = rootRow["id"]!.GetValue<string>();
        ExpectInvalid(cycle.ToJsonString(), mapId);
        var invalidCoordinate = original.DeepClone();
        invalidCoordinate["placements"]![0]!["x"] = "NaN";
        ExpectInvalid(invalidCoordinate.ToJsonString(), mapId);
        var emptyId = original.DeepClone();
        emptyId["ideas"]![0]!["id"] = Guid.Empty.ToString("D");
        ExpectInvalid(emptyId.ToJsonString(), mapId);
        Check.Throws<ArgumentException>(() =>
            PortableMapJson.Import(exported, new MapId(Guid.Empty), 2),
            "Empty destination map ID accepted.");
    }

    private static void ExpectInvalid(string json, MapId destination)
        => Check.Throws<InvalidDataException>(
            () => PortableMapJson.Import(json, destination, 2),
            "Malformed or incompatible portable map was accepted.");
}
