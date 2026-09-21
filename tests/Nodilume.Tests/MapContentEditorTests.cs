using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class MapContentEditorTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("idea-editor");
        var mapId = MapId.New();
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(new MapGraph(new MapInfo(mapId, "Personale", 0, 2)));
        var editor = new MapContentEditor(store);
        var root = await editor.CreateIdeaAsync(mapId, 0, " Radice 🌱 ", "radice", null);
        Check.Equal("Radice 🌱", root.Idea.Title, "Root title was not normalized.");
        Check.Equal(1L, root.Revision, "Root insertion did not advance revision once.");

        var child = await editor.CreateIdeaAsync(mapId, 1, "Concetto", "idea condivisa",
            root.Placement.Id);
        var copy = await editor.CreateRepresentationAsync(mapId, 2, child.Idea.Id,
            root.Placement.Id, "seconda posizione");
        Check.True(copy.Placement.Id != child.Placement.Id
            && copy.Placement.IdeaId == child.Idea.Id
            && copy.Placement.ParentId == root.Placement.Id,
            "Representation copied the Idea or lost its parent.");

        var updated = await editor.UpdateIdeaAsync(mapId, 3, child.Idea.Id,
            "Concetto nuovo", "contenuto 🌳");
        Check.Equal(4L, updated, "Idea update did not advance revision.");
        updated = await editor.UpdateAnnotationAsync(mapId, updated,
            child.Placement.Id, "locale A");
        Check.Equal(5L, updated, "Annotation update did not advance revision.");
        var graph = await store.LoadGraphAsync();
        Check.Equal(2, graph.Ideas.Count, "Additional representation created an extra Idea.");
        Check.Equal(3, graph.Placements.Count, "Expected root and two representations.");
        Check.Equal("contenuto 🌳", graph.Ideas[graph.Placements[copy.Placement.Id].IdeaId].Content,
            "Second representation did not see shared content.");
        Check.Equal("locale A", graph.Placements[child.Placement.Id].Annotation,
            "Local annotation did not persist.");
        Check.Equal("seconda posizione", graph.Placements[copy.Placement.Id].Annotation,
            "Local annotation leaked across representations.");

        Check.Equal(updated, await editor.UpdateIdeaAsync(mapId, updated,
            child.Idea.Id, "Concetto nuovo", "contenuto 🌳"),
            "No-op shared edit increased revision.");
        Check.Equal(updated, await editor.UpdateAnnotationAsync(mapId, updated,
            child.Placement.Id, "locale A"),
            "No-op local edit increased revision.");
        await Check.ThrowsAsync<StaleMapRevisionException>(
            () => editor.UpdateIdeaAsync(mapId, 4, child.Idea.Id,
                "Concetto nuovo", "contenuto 🌳"),
            "Stale no-op was accepted.");
        await Check.ThrowsAsync<StaleMapRevisionException>(
            () => editor.CreateIdeaAsync(mapId, 2, "conflitto", "", null),
            "Stale create was accepted.");
        await Check.ThrowsAsync<DomainRuleException>(
            () => editor.CreateRepresentationAsync(mapId, updated,
                child.Idea.Id, child.Placement.Id),
            "Same idea was inserted beneath its own representation.");
        await Check.ThrowsAsync<DomainRuleException>(
            () => editor.CreateRepresentationAsync(mapId, updated,
                child.Idea.Id, new PlacementId(Guid.NewGuid())),
            "Missing parent was accepted.");
        await Check.ThrowsAsync<ArgumentException>(
            () => editor.CreateIdeaAsync(mapId, updated, " ", "", null),
            "Blank title was accepted.");
        await Check.ThrowsAsync<ArgumentException>(
            () => editor.UpdateAnnotationAsync(mapId, updated, child.Placement.Id,
                new string('x', 4001)),
            "Oversized annotation was accepted.");

        var otherPath = Path.Combine(temp.Root, "other.sqlite");
        var otherId = MapId.New();
        await using var otherStore = new SqliteMapStore(otherPath);
        await otherStore.InitializeAsync();
        await otherStore.CreateMapAsync(new MapGraph(new MapInfo(otherId, "Altra", 0, 2)));
        var otherEditor = new MapContentEditor(otherStore);
        await Check.ThrowsAsync<DomainRuleException>(
            () => otherEditor.CreateIdeaAsync(otherId, 0, "fuori", "",
                root.Placement.Id),
            "Another map's parent was accepted.");
        await Check.ThrowsAsync<DomainRuleException>(
            () => otherEditor.CreateRepresentationAsync(otherId, 0,
                child.Idea.Id, null),
            "Another map's idea was accepted.");
        Check.Equal(0, (await otherStore.LoadGraphAsync()).Ideas.Count,
            "Invalid cross-map edit wrote content.");
        Check.Equal(updated, (await store.GetMapAsync())!.Revision,
            "Rejected write changed map revision.");
    }
}
