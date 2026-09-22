using Nodilume.Application;
using Nodilume.Application.Persistence;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class MapRelationEditorTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("relation-editor");
        var mapId = MapId.New();
        await using var store = new SqliteMapStore(temp.Path);
        await store.InitializeAsync();
        await store.CreateMapAsync(new MapGraph(new MapInfo(mapId, "Relazioni", 0, 2)));
        var ideas = new MapContentEditor(store);
        var a = await ideas.CreateIdeaAsync(mapId, 0, "Sorgente", "", null);
        var b = await ideas.CreateIdeaAsync(mapId, 1, "Destinazione", "", null);
        var alt = await ideas.CreateRepresentationAsync(mapId, 2, a.Idea.Id, null);
        var editor = new MapRelationEditor(store);
        var relation = await editor.CreateAsync(mapId, 3, a.Idea.Id, b.Idea.Id,
            "  sostiene  ", true, "Prima spiegazione 🪴");
        Check.Equal("sostiene", relation.Relation.Kind, "Relation kind not normalized.");
        Check.Equal(4L, relation.Revision, "Create did not advance revision once.");
        Check.Equal(relation.Relation, await store.ReadRelationAsync(mapId, relation.Relation.Id),
            "New relation failed targeted SQLite roundtrip.");

        var changed = await editor.UpdateAsync(mapId, 4, relation.Relation.Id,
            b.Idea.Id, a.Idea.Id, "contraddice", false, "Spiegazione modificata");
        Check.Equal(5L, changed.Revision, "Relation update did not advance revision.");
        Check.True(changed.Relation.SourceIdeaId == b.Idea.Id
            && changed.Relation.TargetIdeaId == a.Idea.Id && !changed.Relation.IsDirected,
            "Relation orientation/direction was not changed.");
        Check.Equal(changed.Relation, await store.ReadRelationAsync(mapId, relation.Relation.Id),
            "Updated relation was not persisted.");

        var noOp = await editor.UpdateAsync(mapId, 5, relation.Relation.Id,
            b.Idea.Id, a.Idea.Id, "contraddice", false, "Spiegazione modificata");
        Check.Equal(5L, noOp.Revision, "No-op relation changed revision.");
        await Check.ThrowsAsync<StaleMapRevisionException>(() =>
            editor.UpdateAsync(mapId, 4, relation.Relation.Id, b.Idea.Id, a.Idea.Id,
                "contraddice", false, "Spiegazione modificata"),
            "Stale no-op relation update was accepted.");
        await Check.ThrowsAsync<StaleMapRevisionException>(() =>
            editor.CreateAsync(mapId, 4, a.Idea.Id, b.Idea.Id,
                "stale", true, ""), "Stale relation creation was accepted.");
        await Check.ThrowsAsync<DomainRuleException>(() =>
            editor.CreateAsync(mapId, 5, a.Idea.Id, a.Idea.Id,
                "self", true, ""), "Conceptual self-relation was accepted.");
        await Check.ThrowsAsync<ArgumentException>(() =>
            editor.CreateAsync(mapId, 5, a.Idea.Id, b.Idea.Id,
                "", false, ""), "Empty relation kind accepted.");
        await Check.ThrowsAsync<ArgumentException>(() =>
            editor.UpdateAsync(mapId, 5, relation.Relation.Id, a.Idea.Id, b.Idea.Id,
                "kind", true, new string('e', 4001)),
            "Oversized explanation was accepted.");

        var otherPath = Path.Combine(temp.Root, "other.sqlite");
        var otherId = MapId.New();
        await using var otherStore = new SqliteMapStore(otherPath);
        await otherStore.InitializeAsync();
        await otherStore.CreateMapAsync(new MapGraph(new MapInfo(otherId, "Altra", 0, 2)));
        var otherEditor = new MapRelationEditor(otherStore);
        await Check.ThrowsAsync<DomainRuleException>(() =>
            otherEditor.CreateAsync(otherId, 0, a.Idea.Id, b.Idea.Id,
                "cross-map", true, ""), "Cross-map endpoints accepted.");
        await Check.ThrowsAsync<DomainRuleException>(() =>
            otherEditor.UpdateAsync(otherId, 0, relation.Relation.Id,
                a.Idea.Id, b.Idea.Id, "cross-map", true, ""),
            "Relation from another map updated.");
        await Check.ThrowsAsync<DomainRuleException>(() =>
            otherEditor.RemoveAsync(otherId, 0, relation.Relation.Id),
            "Relation from another map deleted.");
        Check.Equal(0L, (await otherStore.GetMapAsync())!.Revision,
            "Rejected cross-map write modified other map revision.");
        Check.Equal(5L, (await store.GetMapAsync())!.Revision,
            "Rejected write modified source map revision.");
        Check.Equal(2, (await store.LoadGraphAsync()).Ideas.Count,
            "Relation operation changed idea identities.");

        var removed = await editor.RemoveAsync(mapId, 5, relation.Relation.Id);
        Check.Equal(6L, removed, "Delete did not advance revision once.");
        Check.True(await store.ReadRelationAsync(mapId, relation.Relation.Id) is null,
            "Deletion left conceptual relation.");
        Check.Equal(3, (await store.LoadGraphAsync()).Placements.Count,
            "Deleting relation deleted an Idea or Placement.");
        await Check.ThrowsAsync<DomainRuleException>(() =>
            editor.RemoveAsync(mapId, 6, relation.Relation.Id),
            "Deleting absent relation was accepted.");
    }
}
