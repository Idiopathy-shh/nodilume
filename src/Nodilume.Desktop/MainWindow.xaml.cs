using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

public partial class MainWindow : Window
{
    private const string Origin = "https://nodilume.local";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _profile;
    private readonly string _databasePath;
    private CancellationTokenSource? _projectionCancellation;
    private PlacementId? _lastContextPlacementId;
    private string? _activeMapId;
    private long _activeRevision = -1;
    private bool _recoveringViewer;
    private bool _closed;

    public MainWindow() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nodilume", "WebView2"),
        MapDatabasePaths.Demo)
    {
    }

    public MainWindow(string profile, string? databasePath = null)
    {
        InitializeComponent();
        _profile = profile;
        _databasePath = databasePath ?? MapDatabasePaths.Demo;
        Loaded += InitializeViewer;
        Closed += (_, _) =>
        {
            _closed = true;
            Interlocked.Exchange(ref _projectionCancellation, null)?.Cancel();
            Viewer.Dispose();
        };
    }

    private async void InitializeViewer(object sender, RoutedEventArgs e)
    {
        Loaded -= InitializeViewer;
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "viewer");
            if (!File.Exists(Path.Combine(assets, "index.html")))
                throw new IOException("Risorse grafiche mancanti. Esegui scripts/build.ps1.");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profile);
            if (_closed) return;
            await Viewer.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            var core = Viewer.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.SetVirtualHostNameToFolderMapping(
                "nodilume.local",
                assets,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, args) => args.Cancel = !IsLocal(args.Uri);
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += ReceiveMessage;
            core.ProcessFailed += (_, _) => RecoverViewer(core);
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess && !_closed)
                    Status.Text = $"Caricamento non riuscito: {args.WebErrorStatus}";
            };
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            if (!_closed) Status.Text = $"Impossibile avviare la vista: {ex.Message}";
        }
    }

    private static bool IsLocal(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "nodilume.local" && uri.IsDefaultPort;

    private async void ReceiveMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsLocal(e.Source) || e.WebMessageAsJson.Length > 32_768) return;
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            if (!TryReadProtocol(root, out var type, out var requestId)) return;

            switch (type)
            {
                case "ready":
                    _recoveringViewer = false;
                    await SendProjectionAsync(requestId, _lastContextPlacementId, null, null);
                    break;
                case "projectionRequest":
                    await HandleProjectionRequestAsync(root, requestId);
                    break;
                case "rendered":
                    if (!_closed)
                    {
                        var state = ReadOptionalString(root, "state") ?? "ready";
                        Status.Text = state == "partial"
                            ? "Mappa persistente · dati parziali dichiarati · SQLite locale"
                            : "Mappa persistente · zoom semantico contestuale · SQLite locale";
                    }
                    break;
                case "error":
                    if (!_closed)
                        Status.Text = "La vista 3D ha segnalato un errore; la scena precedente resta autorevole.";
                    break;
            }
        }
        catch (JsonException)
        {
            if (!_closed) Status.Text = "Messaggio della vista non valido.";
        }
        catch (Exception ex)
        {
            if (!_closed) Status.Text = $"Messaggio della vista rifiutato: {ex.Message}";
        }
    }

    private async Task HandleProjectionRequestAsync(JsonElement root, string requestId)
    {
        var mapId = ReadOptionalString(root, "mapId");
        var revision = ReadOptionalInt64(root, "revision");
        PlacementId? contextPlacementId = null;
        if (root.TryGetProperty("context", out var context) && context.ValueKind == JsonValueKind.Object)
        {
            var placement = ReadOptionalString(context, "placementId");
            if (!string.IsNullOrWhiteSpace(placement))
                contextPlacementId = PlacementId.Parse(placement);
        }

        await SendProjectionAsync(requestId, contextPlacementId, mapId, revision);
    }
    private async Task SendProjectionAsync(
        string requestId,
        PlacementId? contextPlacementId,
        string? requestedMapId,
        long? requestedRevision)
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _projectionCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await using var store = new SqliteMapStore(_databasePath);
            await DemoMapInitializer.EnsureAsync(store, cancellation.Token);
            var map = await store.GetMapAsync(cancellation.Token)
                ?? throw new InvalidOperationException("Map is not initialized.");
            if (requestedMapId is not null && requestedMapId != map.Id.ToString())
                throw new InvalidOperationException("Viewer requested a different map.");
            if (requestedRevision is not null && requestedRevision > map.Revision)
                throw new InvalidOperationException("Viewer revision is newer than the authoritative map.");

            var projection = await new SceneService(store).LoadProjectionAsync(
                requestId,
                contextPlacementId,
                cancellationToken: cancellation.Token);
            if (_closed || cancellation.IsCancellationRequested
                || !ReferenceEquals(_projectionCancellation, cancellation))
                return;

            _lastContextPlacementId = projection.ContextPlacementId is null
                ? null
                : PlacementId.Parse(projection.ContextPlacementId);
            _activeMapId = projection.MapId;
            _activeRevision = projection.Revision;
            Status.Text = projection.State switch
            {
                "partial" => "Caricamento completato con dati parziali dichiarati.",
                "leaf" => "Foglia caricata: nessun livello interno da espandere.",
                "empty" => "Contesto vuoto caricato.",
                _ => "Contesto caricato."
            };
            Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(projection, JsonOptions));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (_closed || cancellation.IsCancellationRequested
                || !ReferenceEquals(_projectionCancellation, cancellation))
                return;
            var error = new SceneProjectionError(
                2,
                "projectionError",
                requestId,
                _activeMapId,
                _activeRevision < 0 ? null : _activeRevision,
                contextPlacementId?.ToString(),
                ex is InvalidDataException ? "incomplete-context" : "projection-failed",
                ex.Message);
            Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(error, JsonOptions));
            Status.Text = $"Proiezione non caricata: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(
                    ref _projectionCancellation, null, cancellation), cancellation))
                cancellation.Dispose();
        }
    }

    private async void RecoverViewer(CoreWebView2 core)
    {
        if (_closed || _recoveringViewer) return;
        _recoveringViewer = true;
        Interlocked.Exchange(ref _projectionCancellation, null)?.Cancel();
        Status.Text = "Riavvio della vista 3D…";
        try
        {
            await Task.Delay(150);
            if (!_closed) core.Reload();
        }
        catch (Exception ex)
        {
            if (!_closed)
                Status.Text = $"La vista non può essere riavviata automaticamente: {ex.Message}";
            _recoveringViewer = false;
        }
    }

    private static bool TryReadProtocol(
        JsonElement root,
        out string type,
        out string requestId)
    {
        type = "";
        requestId = "";
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version)
            || version != 2
            || !root.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("requestId", out var requestElement)
            || requestElement.ValueKind != JsonValueKind.String)
            return false;
        type = typeElement.GetString() ?? "";
        requestId = requestElement.GetString() ?? "";
        return requestId.Length is > 0 and <= 128 && type.Length > 0;
    }

    private static string? ReadOptionalString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadOptionalInt64(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var result)
            ? result
            : null;
}
