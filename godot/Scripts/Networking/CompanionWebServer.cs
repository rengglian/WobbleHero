using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Networking;

/// <summary>
/// Embedded HTTP & WebSocket companion server for WobbleHero.
/// Runs inside Godot on Android and Desktop. Serves remote.html to smartphones
/// and handles real-time bidirectional telemetry and game switching.
/// </summary>
public partial class CompanionWebServer : Node
{
    [Export] public int HttpPort { get; set; } = 8080;

    public event Action<string>? OnSwitchGameRequested;
    public event Action? OnStartGameRequested;
    public event Action? OnPauseGameRequested;
    public event Action? OnResumeGameRequested;
    public event Action? OnResetGameRequested;
    public event Action? OnTareRequested;
    public event Action? OnResetCalRequested;
    public event Action<float>? OnSensitivityChanged;
    public event Action<string>? OnToggleInvertRequested;
    public event Action<string>? OnPlayerNameChanged;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _clientLocks = new();
    private string _cachedRemoteHtml = string.Empty;

    public string PlayerName { get; set; } = "Hero 1";

    public override void _Ready()
    {
        LoadStaticHtml();
        StartServer();
    }

    private void LoadStaticHtml()
    {
        // 1. Try reading from embedded assembly resource
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("WobbleHero.remote.html");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                _cachedRemoteHtml = reader.ReadToEnd();
                GD.Print("[CompanionWebServer] Loaded remote.html from embedded assembly resource.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CompanionWebServer] Assembly resource read error: {ex.Message}");
        }

        // 2. Fallback to Godot virtual filesystem
        if (string.IsNullOrEmpty(_cachedRemoteHtml) && Godot.FileAccess.FileExists("res://remote.html"))
        {
            try
            {
                using var file = Godot.FileAccess.Open("res://remote.html", Godot.FileAccess.ModeFlags.Read);
                _cachedRemoteHtml = file.GetAsText();
                GD.Print("[CompanionWebServer] Loaded remote.html from res://remote.html.");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionWebServer] FileAccess read error: {ex.Message}");
            }
        }
    }

    public string GetCompanionUrl()
    {
        string ip = GetLocalNetworkIp();
        return $"http://{ip}:{HttpPort}/remote";
    }

    public static string GetLocalNetworkIp()
    {
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();
                        if (!ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                        {
                            return ip;
                        }
                    }
                }
            }
        }
        catch { }

        try
        {
            var addrs = Godot.IP.GetLocalAddresses();
            foreach (var a in addrs)
            {
                if (!a.Contains(':') && !a.StartsWith("127.") && !a.StartsWith("169.254."))
                {
                    return a;
                }
            }
        }
        catch { }

        return "localhost";
    }

    public void StartServer()
    {
        StopServer();
        _cts = new CancellationTokenSource();

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://*:{HttpPort}/");
            _httpListener.Start();

            Task.Run(() => ListenLoopAsync(_httpListener, _cts.Token));
            GD.Print($"[CompanionWebServer] Server running at {GetCompanionUrl()}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CompanionWebServer] Failed to start HTTP listener on port {HttpPort}: {ex.Message}");
        }
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleContextAsync(context, ct), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    GD.PrintErr($"[CompanionWebServer] Listener exception: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        string path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

        // WebSocket endpoint
        if (context.Request.IsWebSocketRequest && (path == "/ws" || path == "/socket"))
        {
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                var ws = wsContext.WebSocket;
                var wsLock = new SemaphoreSlim(1, 1);
                _clientLocks[ws] = wsLock;
                GD.Print($"[CompanionWebServer] New WebSocket connected from {request.RemoteEndPoint.Address}");

                await HandleWebSocketClientAsync(ws, wsLock, ct);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionWebServer] WebSocket accept error: {ex.Message}");
            }
            return;
        }

        // Static HTTP serving
        try
        {
            if (path == "/" || path == "/remote" || path == "/index.html")
            {
                byte[] htmlBytes = Encoding.UTF8.GetBytes(_cachedRemoteHtml);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = htmlBytes.Length;
                await response.OutputStream.WriteAsync(htmlBytes, ct);
            }
            else if (path == "/health")
            {
                byte[] okBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"service\":\"WobbleHero\"}");
                response.ContentType = "application/json";
                response.ContentLength64 = okBytes.Length;
                await response.OutputStream.WriteAsync(okBytes, ct);
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch { }
        finally
        {
            try { response.Close(); } catch { }
        }
    }

    private async Task HandleWebSocketClientAsync(WebSocket ws, SemaphoreSlim wsLock, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessClientMessage(msg);
                }
            }
        }
        catch { }
        finally
        {
            _clientLocks.TryRemove(ws, out _);
            wsLock.Dispose();
            try { ws.Dispose(); } catch { }
        }
    }

    private void ProcessClientMessage(string messageJson)
    {
        try
        {
            string cmdType = "";
            string gameId = "";
            float sensitivityVal = 1.0f;
            string axis = "roll";
            string playerName = "";

            using (var doc = JsonDocument.Parse(messageJson))
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) return;
                cmdType = typeProp.GetString() ?? "";

                if (root.TryGetProperty("gameId", out var gId))
                    gameId = gId.GetString() ?? "";

                if (root.TryGetProperty("value", out var sVal))
                    sVal.TryGetSingle(out sensitivityVal);

                if (root.TryGetProperty("axis", out var axProp))
                    axis = axProp.GetString() ?? "roll";

                if (root.TryGetProperty("playerName", out var pName))
                    playerName = pName.GetString() ?? "";
            }

            Callable.From(() =>
            {
                switch (cmdType)
                {
                    case "switch_game":
                        if (!string.IsNullOrEmpty(gameId))
                            OnSwitchGameRequested?.Invoke(gameId);
                        break;
                    case "start_game":
                        OnStartGameRequested?.Invoke();
                        break;
                    case "pause_game":
                        OnPauseGameRequested?.Invoke();
                        break;
                    case "resume_game":
                        OnResumeGameRequested?.Invoke();
                        break;
                    case "reset_game":
                        OnResetGameRequested?.Invoke();
                        break;
                    case "tare_board":
                        OnTareRequested?.Invoke();
                        break;
                    case "reset_cal":
                        OnResetCalRequested?.Invoke();
                        break;
                    case "set_sensitivity":
                        OnSensitivityChanged?.Invoke(sensitivityVal);
                        break;
                    case "toggle_invert":
                        OnToggleInvertRequested?.Invoke(axis);
                        break;
                    case "set_player_name":
                        if (!string.IsNullOrEmpty(playerName))
                        {
                            PlayerName = playerName;
                            OnPlayerNameChanged?.Invoke(PlayerName);
                        }
                        break;
                }
            }).CallDeferred();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CompanionWebServer] Parse message error: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcasts telemetry payload to all connected smartphone remotes.
    /// </summary>
    public void BroadcastTelemetry(
        BoardState boardState,
        string activeGameId,
        string activeGameTitle,
        string gameState,
        int score,
        Dictionary<string, object> gameStats,
        float sensitivity,
        bool invertRoll,
        bool invertPitch)
    {
        if (_clientLocks.IsEmpty) return;

        var availableGames = GameRegistry.GetAvailableGames();
        var gamesList = new List<object>();
        foreach (var g in availableGames)
        {
            gamesList.Add(new
            {
                id = g.Id,
                title = g.Title,
                description = g.Description,
                icon = g.Icon
            });
        }

        var payload = new
        {
            type = "telemetry",
            board = new
            {
                roll = boardState.Roll,
                pitch = boardState.Pitch,
                fusedRollDeg = boardState.FusedRollDeg,
                fusedPitchDeg = boardState.FusedPitchDeg,
                calOffsetRoll = boardState.CalOffsetRoll,
                calOffsetPitch = boardState.CalOffsetPitch,
                isActive = boardState.IsActive,
                isButtonPressed = boardState.IsButtonPressed,
                hz = boardState.Hz,
                seq = boardState.Seq,
                deviceIp = boardState.DeviceIp,
                source = boardState.Source
            },
            game = new
            {
                activeGameId,
                activeGameTitle,
                state = gameState,
                score,
                stats = gameStats
            },
            availableGames = gamesList,
            settings = new
            {
                sensitivity,
                invertRoll,
                invertPitch,
                playerName = PlayerName
            }
        };

        string json = JsonSerializer.Serialize(payload, JsonOpts);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        foreach (var kvp in _clientLocks)
        {
            var ws = kvp.Key;
            var sem = kvp.Value;
            if (ws.State != WebSocketState.Open) continue;

            Task.Run(async () =>
            {
                if (!await sem.WaitAsync(20)) return;
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch { }
                finally
                {
                    sem.Release();
                }
            });
        }
    }

    public void StopServer()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        try { _httpListener?.Stop(); } catch { }
        try { _httpListener?.Close(); } catch { }
        _httpListener = null;

        foreach (var ws in _clientLocks.Keys)
        {
            try { ws.Dispose(); } catch { }
        }
        _clientLocks.Clear();
    }

    public override void _ExitTree()
    {
        StopServer();
    }
}
