using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Wpf;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

internal static class EditingSmoke
{
    public static async Task VerifyAsync(WebView2 web, string databasePath, bool reopen)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        Task<string> Script(string s) => web.ExecuteScriptAsync(s).WaitAsync(timeout.Token);
        async Task Wait(string expression)
        {
            var started = DateTime.UtcNow;
            while (await Script(expression) != "true")
            {
                if (DateTime.UtcNow - started > TimeSpan.FromSeconds(12))
                {
                    var diagnostic = await Script(
                        "(()=>{const l=[...document.querySelectorAll('.node-label')]"
                        + ".find(x=>x.textContent==='Sottogruppo A1');"
                        + "const r=l?.getBoundingClientRect();"
                        + "const h=r?document.elementFromPoint(r.x+r.width/2,r.y+r.height/2):null;"
                        + "return JSON.stringify({state:document.body.dataset.state,"
                        + "dragging:document.body.dataset.dragging,"
                        + "editPending:document.body.dataset.editPending,"
                        + "undoDisabled:document.getElementById('undo').disabled,"
                        + "redoDisabled:document.getElementById('redo').disabled,"
                        + "pinDisabled:document.getElementById('pin').disabled,"
                        + "pinText:document.getElementById('pin').textContent,"
                        + "status:document.getElementById('edit-status').textContent,"
                        + "projectionStatus:document.getElementById('projection-status').textContent,"
                        + "loadState:document.body.dataset.loadState,"
                        + "labelRect:r?[r.x,r.y,r.width,r.height]:null,"
                        + "hitTag:h?.tagName,hitClass:h?.className,hitText:h?.textContent})})()");
                    throw new Exception("Editing wait timed out: " + expression + " DOM=" + diagnostic);
                }
                await Task.Delay(50, timeout.Token);
            }
        }
        await using var store = new SqliteMapStore(databasePath);
        await store.InitializeAsync();
        var map = (await store.GetMapAsync())!;
        var id = new PlacementId(Guid.Parse("20000000-0000-0000-0000-000000000203"));
        if (reopen)
        {
            var saved = (await store.ReadViewStateAsync(map.Id))!;
            await Wait("document.getElementById('selection-title').textContent === 'Sottogruppo A1'");
            await Wait("document.querySelector('#breadcrumbs .current')?.textContent === 'Gruppo A'");
            var poseText = JsonSerializer.Deserialize<string>(await Script("document.body.dataset.cameraPosition"))!;
            var pose = JsonSerializer.Deserialize<double[]>(poseText)!;
            if (pose.Zip(saved.Camera, (a,b) => Math.Abs(a-b)).Max() > 0.01)
                throw new Exception("Camera was not restored after actual window close/reopen.");
            if (!(await store.ReadPlacementAsync(map.Id, id))!.IsPinned)
                throw new Exception("Pin was lost after reopen.");
            Console.WriteLine("PASS: GRAPH.06.08 real window reopen restores context, selection, camera and pin.");
            return;
        }
        await Script("[...document.querySelectorAll('.node-label')].find(x=>x.textContent==='Gruppo A').click()");
        await Script("document.getElementById('enter').click()");
        await Wait("document.body.dataset.state==='ready' && document.querySelector('#breadcrumbs .current')?.textContent==='Gruppo A'");
        await Script("[...document.querySelectorAll('.node-label')].find(x=>x.textContent==='Sottogruppo A1').click()");
        await Script("document.getElementById('focus').click()");
        await Task.Delay(800, timeout.Token);
        // A close view of a group can open it semantically; restore the parent if needed.
        if (await Script("document.querySelector('#breadcrumbs .current')?.textContent==='Gruppo A'") != "true")
            throw new Exception("Unexpected semantic navigation during editing setup.");
        var before = (await store.ReadPlacementAsync(map.Id,id))!;
        async Task Mouse(string type, double x, double y, string button, int buttons)
        {
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                JsonSerializer.Serialize(new { type, x, y, button, buttons, modifiers=8, clickCount=1 }));
        }
        async Task Drag(bool cancel = false)
        {
            var raw = await Script("(()=>{const r=[...document.querySelectorAll('.node-label')].find(x=>x.textContent==='Sottogruppo A1').getBoundingClientRect(); return [r.x+r.width/2,r.y+r.height/2]})()");
            var point = JsonSerializer.Deserialize<double[]>(raw)!;
            await Mouse("mousePressed",point[0],point[1],"left",1);
            await Wait("document.body.dataset.dragging==='true'");
            await Mouse("mouseMoved",point[0]+55,point[1]+25,"left",1);
            if (cancel)
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                    "{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
            await Mouse("mouseReleased",point[0]+55,point[1]+25,"left",0);
            await Wait("document.body.dataset.dragging==='false' && document.body.dataset.editPending==='false' && document.body.dataset.state==='ready'");
            await Task.Delay(150, timeout.Token);
        }
        await Drag(cancel:true);
        if ((await store.ReadPlacementAsync(map.Id,id))! != before)
            throw new Exception("Esc cancellation persisted a drag.");
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_smoke BEFORE UPDATE ON placements BEGIN SELECT RAISE(ABORT,'smoke write failure'); END;";
            await command.ExecuteNonQueryAsync();
            await Drag();
            await Wait("document.getElementById('edit-status').textContent.includes('non salvata')");
            if ((await store.ReadPlacementAsync(map.Id,id))! != before)
                throw new Exception("Failed drag changed the database.");
            command.CommandText = "DROP TRIGGER fail_smoke;";
            await command.ExecuteNonQueryAsync();
        }

        await Drag();
        var moved = (await store.ReadPlacementAsync(map.Id,id))!;
        if (moved == before || moved.ParentId != before.ParentId)
            throw new Exception("Real pointer drag did not persist a local move.");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && !document.getElementById('undo').disabled");
        await Script("document.getElementById('undo').click()");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && !document.getElementById('redo').disabled");
        if ((await store.ReadPlacementAsync(map.Id,id))! != before)
            throw new Exception("UI undo did not restore the exact placement.");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && !document.getElementById('redo').disabled");
        await Script("document.getElementById('redo').click()");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && document.getElementById('redo').disabled");
        if ((await store.ReadPlacementAsync(map.Id,id))! != moved)
            throw new Exception("UI redo did not restore the move.");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && !document.getElementById('pin').disabled");
        await Script("document.getElementById('pin').click()");
        await Wait("document.body.dataset.editPending==='false' && document.body.dataset.state==='ready' && document.getElementById('pin').textContent==='Sblocca'");
        if (!(await store.ReadPlacementAsync(map.Id,id))!.IsPinned)
            throw new Exception("UI pin was not persisted.");
        await Script("window.nodilumeSaveView()");
        await Task.Delay(650, timeout.Token);
        Console.WriteLine("PASS: GRAPH.06.08 trusted pointer drag, Esc, UI undo/redo, write rollback and pin.");
    }
}
