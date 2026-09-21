using System.Text.Json;
using System.Windows;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private readonly SemaphoreSlim _editGate = new(1, 1);
    private bool _mayClose;
    private bool _closingForSave;
    private TaskCompletionSource<bool>? _closingSave;
    private string? _closingSaveRequestId;
    private readonly SemaphoreSlim _viewSaveGate = new(1, 1);
    private long _viewSaveSequence;

    private void ConfigureViewSaveOnClose()
    {
        Closing += async (_, args) =>
        {
            if (_mayClose) return;
            args.Cancel = true;
            if (_closingForSave) return;
            _closingForSave = true;
            _closingSave = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                await _editGate.WaitAsync();
                _editGate.Release();
                if (Viewer.CoreWebView2 is not null && _activeMapId is not null)
                {
                    _closingSaveRequestId = Guid.NewGuid().ToString("N");
                    await Viewer.ExecuteScriptAsync("window.nodilumeSaveView?.(" +
                        JsonSerializer.Serialize(_closingSaveRequestId) + ")");
                    var completed = await Task.WhenAny(_closingSave.Task, Task.Delay(4000));
                    if (completed != _closingSave.Task || !await _closingSave.Task)
                    {
                        Status.Text = "Vista non salvata.";
                        _mayClose = MessageBox.Show(this,
                            "Non è stato possibile salvare la vista. Chiudere mantenendo l’ultima vista salvata?",
                            "Nodilume", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
                        return;
                    }
                }
                _mayClose = true;
            }
            catch (Exception exception)
            {
                Status.Text = $"Vista non salvata: {exception.Message}";
                _mayClose = MessageBox.Show(this,
                    "La vista non risponde. Chiudere mantenendo l’ultima vista salvata?",
                    "Nodilume", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }
            finally
            {
                _closingForSave = false;
                if (_mayClose) Close();
            }
        };
    }

    private async Task SendInitialProjectionAsync(string requestId)
    {
        await using var store = new SqliteMapStore(_databasePath);
        await DemoMapInitializer.EnsureAsync(store);
        var map = await store.GetMapAsync()
            ?? throw new InvalidOperationException("Map missing.");
        var restored = await ViewStateResolver.ResolveAsync(store, map.Id);
        _activeMapId = map.Id.ToString();
        _activeRevision = map.Revision;
        var history = await store.ReadEditHistoryStatusAsync(map.Id);
        Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
            version = 2, type = "editStatus", requestId,
            mapId = _activeMapId, revision = map.Revision,
            canUndo = history.CanUndo, canRedo = history.CanRedo
        }, JsonOptions));
        Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
            version = 2, type = "restoreView", requestId,
            mapId = _activeMapId, state = restored.CameraState,
            selectedPlacementId = restored.Selection?.ToString()
        }, JsonOptions));
        await SendProjectionAsync(requestId, restored.Context, _activeMapId, null,
            restored.PageAfter, restored.Selection);
    }

    private async Task HandleEditCommandAsync(JsonElement root, string requestId)
    {
        await _editGate.WaitAsync();
        var mapId = ReadOptionalString(root, "mapId");
        var action = ReadOptionalString(root, "action") ?? "";
        try
        {
            if (_closed || _closingForSave || mapId is null || mapId != _activeMapId)
                throw new DomainRuleException("The edit belongs to a different or closing map.");
            var expected = ReadOptionalInt64(root, "revision")
                ?? throw new DomainRuleException("Edit revision missing.");
            var placementText = ReadOptionalString(root, "placementId");
            var placement = placementText is null ? (PlacementId?)null : PlacementId.Parse(placementText);
            double? Numeric(string key) => root.TryGetProperty(key, out var prop)
                && prop.ValueKind == JsonValueKind.Number ? prop.GetDouble() : null;
            bool? pin = root.TryGetProperty("isPinned", out var pinValue)
                ? pinValue.ValueKind == JsonValueKind.True ? true
                : pinValue.ValueKind == JsonValueKind.False ? false : null : null;
            var command = new LocalEditCommand(
                requestId, MapId.Parse(mapId), expected, action, placement,
                Numeric("x"), Numeric("y"), Numeric("z"), pin);
            await using var store = new SqliteMapStore(_databasePath);
            _projectionCancellation?.Cancel();
            var result = await store.ApplyLocalEditAsync(command);
            _projectionCache.RetainMapRevision(mapId, result.Revision);
            _activeRevision = Math.Max(_activeRevision, result.Revision);
            Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                version = 2, type = "editResult", requestId, mapId,
                revision = result.Revision, success = true, changed = result.Changed,
                replay = result.Replay, canUndo = result.CanUndo, canRedo = result.CanRedo
            }, JsonOptions));
        }
        catch (Exception exception)
        {
            if (_closed) return;
            Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                version = 2, type = "editResult", requestId, mapId,
                success = false, message = exception.Message
            }, JsonOptions));
            Status.Text = $"Modifica non salvata: {exception.Message}";
        }
        finally { _editGate.Release(); }
    }

    private async Task HandleViewStateAsync(JsonElement root)
    {
        var mapId = ReadOptionalString(root, "mapId");
        var sequence = Interlocked.Increment(ref _viewSaveSequence);
        var committed = false;
        try
        {
            if (_closed || mapId is null || mapId != _activeMapId ||
                !root.TryGetProperty("state", out var stateElement))
                return;
            var state = JsonSerializer.Deserialize<PersistedViewState>(
                stateElement.GetRawText(), JsonOptions);
            if (state is null) return;
            await _viewSaveGate.WaitAsync();
            try
            {
                if (sequence != Interlocked.Read(ref _viewSaveSequence) || _closed) return;
                await using var store = new SqliteMapStore(_databasePath);
                var map = await store.GetMapAsync();
                if (map is null || map.Id.ToString() != mapId) return;
                await store.SaveViewStateAsync(map.Id, state);
                committed = true;
            }
            finally { _viewSaveGate.Release(); }
        }
        catch (Exception exception)
        {
            if (!_closed) Status.Text = $"Vista non salvata: {exception.Message}";
            if (ReadOptionalString(root, "requestId") == _closingSaveRequestId)
                _closingSave?.TrySetResult(false);
        }
        finally
        {
            if (ReadOptionalString(root, "requestId") == _closingSaveRequestId)
                _closingSave?.TrySetResult(committed);
        }
    }
}
