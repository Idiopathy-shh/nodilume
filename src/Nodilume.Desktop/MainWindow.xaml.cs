using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Nodilume.Desktop;

public partial class MainWindow : Window
{
    private readonly string _profile;
    private bool _closed;
    private const string Origin = "https://nodilume.local";
    public MainWindow() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nodilume", "WebView2")) { }

    public MainWindow(string profile)
    {
        InitializeComponent();
        _profile = profile;
        Loaded += InitializeViewer;
        Closed += (_, _) => { _closed = true; Viewer.Dispose(); };
    }

    private async void InitializeViewer(object sender, RoutedEventArgs e)
    {
        Loaded -= InitializeViewer;
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "viewer");
            if (!File.Exists(Path.Combine(assets, "index.html"))) throw new IOException("Risorse grafiche mancanti. Esegui scripts/build.ps1.");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profile);
            if (_closed) return;
            await Viewer.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            var core = Viewer.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.SetVirtualHostNameToFolderMapping("nodilume.local", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, args) => args.Cancel = !IsLocal(args.Uri);
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += ReceiveMessage;
            core.ProcessFailed += (_, _) => Status.Text = "La vista si è interrotta. Chiudi e riapri Nodilume.";
            core.NavigationCompleted += (_, args) => { if (!args.IsSuccess) Status.Text = $"Caricamento non riuscito: {args.WebErrorStatus}"; };
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            if (!_closed) Status.Text = $"Impossibile avviare la vista: {ex.Message}";
        }
    }

    private static bool IsLocal(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "nodilume.local" && uri.IsDefaultPort;

    private void ReceiveMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsLocal(e.Source) || e.WebMessageAsJson.Length > 16_384) return;
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var v)
                || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version != 1) return;
            if (!root.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) return;
            switch (t.GetString())
            {
                case "ready":
                    Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(DemoScene.Create()));
                    break;
                case "rendered":
                    Status.Text = "Mappa dimostrativa · Dati sintetici · Modifiche e salvataggio disponibili nelle prossime fasi";
                    break;
                case "error":
                    Status.Text = "La scena non può essere visualizzata. Verifica il supporto WebGL e riapri Nodilume.";
                    break;
            }
        }
        catch (JsonException) { Status.Text = "Messaggio della vista non valido."; }
    }
}
