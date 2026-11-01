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
    public event Action<string>? OnMountingProfileChanged;
    public event Action<string>? OnPlayerNameChanged;
    public event Action<int>? OnSeedChanged;
    public event Action<int, string, string>? OnMazeConfigChanged;
    public event Action<float, string>? OnSurferConfigChanged;
    public event Action<string, string>? OnSumoConfigChanged;
    public event Action? OnSumoShockwaveTriggered;
    public event Action<string>? OnLanderGravityChanged;
    public event Action? OnEmergencyRcsTriggered;
    public event Action? OnLanderRefuelRequested;
    public event Action<float>? OnCompassOffsetChanged;
    public event Action<float>? OnCompassRotateRequested;
    public event Action<int>? OnCompassSeedChanged;
    public event Action? OnPinballLaunchRequested;
    public event Action? OnTableNudgeRequested;
    public event Action<float>? OnTableInclineChanged;
    public event Action<float>? OnPinballRestitutionChanged;
    public event Action<string, string>? OnSlalomConfigChanged;
    public event Action<string>? OnRiverDifficultyChanged;
    public event Action? OnResetLeaderboardRequested;
    public event Action? OnStartRecordingRequested;
    public event Action? OnStopRecordingRequested;
    public Func<string>? OnGetRecordingCsv;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> _clientLocks = new();
    private string _cachedRemoteHtml = string.Empty;
    private float _surferWaveSpeed = 28.0f;
    private string _surferCosmicDensity = "medium";
    private string _sumoDifficulty = "novice";
    private string _sumoArenaSize = "standard";
    private string _landerGravity = "moon";
    private float _compassOffsetDeg = 0.0f;
    private int _compassSeed = 4096;
    private float _tableInclineDeg = 12.0f;
    private float _pinballRestitution = 0.85f;
    private string _slalomCourse = "giant_slalom";
    private string _slalomWeather = "powder";
    private string _riverDifficulty = "rapids_class_3";

    public string PlayerName { get; set; } = "Hero 1";

    public override void _Ready()
    {
        LoadStaticHtml();
        StartServer();
    }

    private void LoadStaticHtml()
    {
        // 1. Prefer reading modular index.html from Godot virtual filesystem (res://www/index.html)
        if (Godot.FileAccess.FileExists("res://www/index.html"))
        {
            try
            {
                using var file = Godot.FileAccess.Open("res://www/index.html", Godot.FileAccess.ModeFlags.Read);
                _cachedRemoteHtml = file.GetAsText();
                GD.Print("[CompanionWebServer] Loaded companion app from res://www/index.html (Modular SPA).");
                return;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionWebServer] FileAccess read error on res://www/index.html: {ex.Message}");
            }
        }

        // 2. Legacy fallback to res://remote.html
        if (Godot.FileAccess.FileExists("res://remote.html"))
        {
            try
            {
                using var file = Godot.FileAccess.Open("res://remote.html", Godot.FileAccess.ModeFlags.Read);
                _cachedRemoteHtml = file.GetAsText();
                GD.Print("[CompanionWebServer] Loaded remote.html from res://remote.html.");
                return;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionWebServer] FileAccess read error: {ex.Message}");
            }
        }

        // 3. Fallback to embedded assembly resource (e.g. standalone/Android export)
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("WobbleHero.www.index.html")
                                ?? asm.GetManifestResourceStream("WobbleHero.remote.html");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                _cachedRemoteHtml = reader.ReadToEnd();
                GD.Print("[CompanionWebServer] Loaded remote HTML from embedded assembly resource.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CompanionWebServer] Assembly resource read error: {ex.Message}");
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
            if (path == "/health")
            {
                byte[] okBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"service\":\"WobbleHero\"}");
                response.ContentType = "application/json";
                response.ContentLength64 = okBytes.Length;
                await response.OutputStream.WriteAsync(okBytes, ct);
            }
            else if (path == "/api/leaderboard")
            {
                var topEntries = LeaderboardService.Instance.GetTopEntries();
                string json = JsonSerializer.Serialize(topEntries, JsonOpts);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = jsonBytes.Length;
                await response.OutputStream.WriteAsync(jsonBytes, ct);
            }
            else if (path == "/api/recording/latest.csv" || path == "/api/recordings/latest.csv")
            {
                string csv = OnGetRecordingCsv?.Invoke() ?? string.Empty;
                if (!string.IsNullOrEmpty(csv))
                {
                    byte[] csvBytes = Encoding.UTF8.GetBytes(csv);
                    response.ContentType = "text/csv; charset=utf-8";
                    response.Headers.Add("Content-Disposition", "attachment; filename=\"sensor_session.csv\"");
                    response.ContentLength64 = csvBytes.Length;
                    await response.OutputStream.WriteAsync(csvBytes, ct);
                }
                else
                {
                    byte[] notFoundBytes = Encoding.UTF8.GetBytes("No recording session available yet. Start and stop a recording in SensorLab first.");
                    response.StatusCode = 404;
                    response.ContentType = "text/plain";
                    response.ContentLength64 = notFoundBytes.Length;
                    await response.OutputStream.WriteAsync(notFoundBytes, ct);
                }
            }
            else if (await TryServeStaticFileAsync(response, path, ct))
            {
                // Successfully served static file from res://www or embedded resource
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

    private static string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".ico" => "image/x-icon",
            ".woff2" => "font/woff2",
            ".woff" => "font/woff",
            ".ttf" => "font/ttf",
            _ => "application/octet-stream"
        };
    }

    private async Task<bool> TryServeStaticFileAsync(HttpListenerResponse response, string requestPath, CancellationToken ct)
    {
        string relPath = requestPath.TrimStart('/');
        if (string.IsNullOrEmpty(relPath) || relPath == "remote" || relPath == "remote.html") relPath = "index.html";

        // 1. Try reading directly from Godot virtual filesystem (res://www/...)
        string godotPath = $"res://www/{relPath}";
        if (Godot.FileAccess.FileExists(godotPath))
        {
            try
            {
                using var f = Godot.FileAccess.Open(godotPath, Godot.FileAccess.ModeFlags.Read);
                if (f != null)
                {
                    byte[] buffer = f.GetBuffer((long)f.GetLength());
                    response.ContentType = GetMimeType(godotPath);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, ct);
                    return true;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CompanionWebServer] Static file read error '{godotPath}': {ex.Message}");
            }
        }

        // 2. Fallback to embedded assembly resource (e.g. standalone/Android export)
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = "WobbleHero.www." + relPath.Replace('/', '.').Replace('\\', '.');
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                byte[] buffer = ms.ToArray();
                response.ContentType = GetMimeType(relPath);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CompanionWebServer] Embedded resource read error for '{relPath}': {ex.Message}");
        }

        // 3. Fallback to legacy _cachedRemoteHtml if requesting main entry
        if ((requestPath == "/" || requestPath == "/remote" || requestPath == "/remote.html" || requestPath == "/index.html") && !string.IsNullOrEmpty(_cachedRemoteHtml))
        {
            byte[] htmlBytes = Encoding.UTF8.GetBytes(_cachedRemoteHtml);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = htmlBytes.Length;
            await response.OutputStream.WriteAsync(htmlBytes, ct);
            return true;
        }

        return false;
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
            string mountingProfile = "";
            int seedVal = 1337;
            bool hasSeed = false;
            string mazeSize = "medium";
            string mazeDifficulty = "medium";
            float waveSpeedVal = 28.0f;
            bool hasWaveSpeed = false;
            string cosmicDensityVal = "medium";
            bool hasCosmicDensity = false;
            string sumoDiffVal = "";
            bool hasSumoDiff = false;
            string sumoSizeVal = "";
            bool hasSumoSize = false;
            string landerPlanetVal = "";
            bool hasLanderPlanet = false;
            float compassOffsetVal = 0.0f;
            bool hasCompassOffset = false;
            float compassDeltaVal = 0.0f;
            bool hasCompassDelta = false;
            float pinballInclineVal = 12.0f;
            bool hasPinballIncline = false;
            float pinballRestVal = 0.85f;
            bool hasPinballRest = false;
            string slalomCourseVal = string.Empty;
            string slalomWeatherVal = string.Empty;
            string riverDiffVal = string.Empty;

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

                if (root.TryGetProperty("profile", out var profProp))
                    mountingProfile = profProp.GetString() ?? "";

                if (root.TryGetProperty("seed", out var seedProp))
                {
                    if (seedProp.ValueKind == JsonValueKind.Number && seedProp.TryGetInt32(out var sInt))
                    {
                        seedVal = sInt;
                        hasSeed = true;
                    }
                    else if (seedProp.ValueKind == JsonValueKind.String && int.TryParse(seedProp.GetString(), out var sParsed))
                    {
                        seedVal = sParsed;
                        hasSeed = true;
                    }
                }

                if (root.TryGetProperty("size", out var sizeProp))
                    mazeSize = sizeProp.GetString() ?? "medium";

                if (root.TryGetProperty("difficulty", out var diffProp))
                {
                    mazeDifficulty = diffProp.GetString() ?? "medium";
                    sumoDiffVal = diffProp.GetString() ?? "";
                    hasSumoDiff = !string.IsNullOrEmpty(sumoDiffVal);
                }

                if (root.TryGetProperty("speed", out var spdProp))
                {
                    if (spdProp.ValueKind == JsonValueKind.Number && spdProp.TryGetSingle(out var sValF))
                    {
                        waveSpeedVal = sValF;
                        hasWaveSpeed = true;
                    }
                    else if (spdProp.ValueKind == JsonValueKind.String && float.TryParse(spdProp.GetString(), out var sParsedF))
                    {
                        waveSpeedVal = sParsedF;
                        hasWaveSpeed = true;
                    }
                }

                if (root.TryGetProperty("density", out var denProp))
                {
                    cosmicDensityVal = denProp.GetString() ?? "medium";
                    hasCosmicDensity = !string.IsNullOrEmpty(cosmicDensityVal);
                }

                if (root.TryGetProperty("arenaSize", out var aSizeProp))
                {
                    sumoSizeVal = aSizeProp.GetString() ?? "";
                    hasSumoSize = !string.IsNullOrEmpty(sumoSizeVal);
                }
                else if (root.TryGetProperty("size", out var aSizeProp2))
                {
                    string sValStr = aSizeProp2.GetString() ?? "";
                    if (!string.IsNullOrEmpty(sValStr) && (sValStr == "compact" || sValStr == "standard" || sValStr == "colosseum"))
                    {
                        sumoSizeVal = sValStr;
                        hasSumoSize = true;
                    }
                }

                if (root.TryGetProperty("planet", out var pProp))
                {
                    landerPlanetVal = pProp.GetString() ?? "";
                    hasLanderPlanet = !string.IsNullOrEmpty(landerPlanetVal);
                }

                if (root.TryGetProperty("value", out var vProp) && vProp.ValueKind == JsonValueKind.String)
                {
                    string vStr = vProp.GetString() ?? "";
                    if (!hasSumoDiff && (vStr == "novice" || vStr == "yokozuna"))
                    {
                        sumoDiffVal = vStr;
                        hasSumoDiff = true;
                    }
                    if (!hasSumoSize && (vStr == "compact" || vStr == "standard" || vStr == "colosseum"))
                    {
                        sumoSizeVal = vStr;
                        hasSumoSize = true;
                    }
                    if (!hasLanderPlanet && (vStr == "moon" || vStr == "mars" || vStr == "europa" || vStr == "earth"))
                    {
                        landerPlanetVal = vStr;
                        hasLanderPlanet = true;
                    }
                }

                if (root.TryGetProperty("offset", out var offProp) && offProp.TryGetSingle(out var oF))
                {
                    compassOffsetVal = oF;
                    hasCompassOffset = true;
                }
                else if (root.TryGetProperty("value", out var vFProp) && vFProp.TryGetSingle(out var vF))
                {
                    compassOffsetVal = vF;
                    compassDeltaVal = vF;
                }

                if (root.TryGetProperty("delta", out var dProp) && dProp.TryGetSingle(out var dF))
                {
                    compassDeltaVal = dF;
                    hasCompassDelta = true;
                }
                else if (root.TryGetProperty("deltaDeg", out var dDegProp) && dDegProp.TryGetSingle(out var ddF))
                {
                    compassDeltaVal = ddF;
                    hasCompassDelta = true;
                }

                if (root.TryGetProperty("incline", out var incProp) && incProp.TryGetSingle(out var incF))
                {
                    pinballInclineVal = incF;
                    hasPinballIncline = true;
                }
                else if (root.TryGetProperty("deg", out var degProp) && degProp.TryGetSingle(out var degF))
                {
                    pinballInclineVal = degF;
                    hasPinballIncline = true;
                }

                if (root.TryGetProperty("restitution", out var restProp) && restProp.TryGetSingle(out var rF))
                {
                    pinballRestVal = rF;
                    hasPinballRest = true;
                }

                if (root.TryGetProperty("course", out var cProp) && cProp.GetString() is { } cStr)
                {
                    slalomCourseVal = cStr;
                }
                if (root.TryGetProperty("snow", out var sProp) && sProp.GetString() is { } sStr)
                {
                    slalomWeatherVal = sStr;
                }
                else if (root.TryGetProperty("weather", out var wProp) && wProp.GetString() is { } wStr)
                {
                    slalomWeatherVal = wStr;
                }

                if (root.TryGetProperty("diff", out var rDiffProp) && rDiffProp.GetString() is { } rdStr)
                {
                    riverDiffVal = rdStr;
                }
                else if (root.TryGetProperty("difficulty", out var rDiffProp2) && rDiffProp2.GetString() is { } rdStr2)
                {
                    riverDiffVal = rdStr2;
                }
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
                    case "set_mounting_profile":
                        if (!string.IsNullOrEmpty(mountingProfile))
                        {
                            OnMountingProfileChanged?.Invoke(mountingProfile);
                        }
                        break;
                    case "set_seed":
                        if (hasSeed)
                        {
                            OnSeedChanged?.Invoke(seedVal);
                        }
                        break;
                    case "set_maze_config":
                        OnMazeConfigChanged?.Invoke(seedVal, mazeSize, mazeDifficulty);
                        break;
                    case "set_wave_speed":
                        if (hasWaveSpeed) _surferWaveSpeed = waveSpeedVal;
                        else if (sensitivityVal > 0) _surferWaveSpeed = sensitivityVal;
                        OnSurferConfigChanged?.Invoke(_surferWaveSpeed, _surferCosmicDensity);
                        break;
                    case "set_wave_density":
                        if (hasCosmicDensity) _surferCosmicDensity = cosmicDensityVal;
                        OnSurferConfigChanged?.Invoke(_surferWaveSpeed, _surferCosmicDensity);
                        break;
                    case "set_surfer_config":
                        if (hasWaveSpeed) _surferWaveSpeed = waveSpeedVal;
                        if (hasCosmicDensity) _surferCosmicDensity = cosmicDensityVal;
                        OnSurferConfigChanged?.Invoke(_surferWaveSpeed, _surferCosmicDensity);
                        break;
                    case "set_sumo_difficulty":
                        if (hasSumoDiff) _sumoDifficulty = sumoDiffVal;
                        OnSumoConfigChanged?.Invoke(_sumoDifficulty, _sumoArenaSize);
                        break;
                    case "set_sumo_arena_size":
                        if (hasSumoSize) _sumoArenaSize = sumoSizeVal;
                        OnSumoConfigChanged?.Invoke(_sumoDifficulty, _sumoArenaSize);
                        break;
                    case "set_sumo_config":
                        if (hasSumoDiff) _sumoDifficulty = sumoDiffVal;
                        if (hasSumoSize) _sumoArenaSize = sumoSizeVal;
                        OnSumoConfigChanged?.Invoke(_sumoDifficulty, _sumoArenaSize);
                        break;
                    case "trigger_shockwave":
                        OnSumoShockwaveTriggered?.Invoke();
                        break;
                    case "set_lander_gravity":
                        if (hasLanderPlanet) _landerGravity = landerPlanetVal;
                        OnLanderGravityChanged?.Invoke(_landerGravity);
                        break;
                    case "trigger_emergency_rcs":
                        OnEmergencyRcsTriggered?.Invoke();
                        break;
                    case "refuel_lander":
                        OnLanderRefuelRequested?.Invoke();
                        break;
                    case "set_compass_offset":
                        if (hasCompassOffset || sensitivityVal != 1.0f)
                        {
                            _compassOffsetDeg = hasCompassOffset ? compassOffsetVal : sensitivityVal;
                        }
                        OnCompassOffsetChanged?.Invoke(_compassOffsetDeg);
                        break;
                    case "rotate_compass":
                    case "rotate_heading":
                        OnCompassRotateRequested?.Invoke(hasCompassDelta ? compassDeltaVal : sensitivityVal);
                        break;
                    case "set_compass_seed":
                        if (hasSeed) _compassSeed = seedVal;
                        OnCompassSeedChanged?.Invoke(_compassSeed);
                        break;
                    case "launch_pinball":
                    case "launch_ball":
                        OnPinballLaunchRequested?.Invoke();
                        break;
                    case "nudge_table":
                        OnTableNudgeRequested?.Invoke();
                        break;
                    case "set_table_incline":
                        if (hasPinballIncline || sensitivityVal != 1.0f)
                        {
                            _tableInclineDeg = hasPinballIncline ? pinballInclineVal : sensitivityVal;
                        }
                        OnTableInclineChanged?.Invoke(_tableInclineDeg);
                        break;
                    case "set_pinball_restitution":
                        if (hasPinballRest || sensitivityVal != 1.0f)
                        {
                            _pinballRestitution = hasPinballRest ? pinballRestVal : sensitivityVal;
                        }
                        OnPinballRestitutionChanged?.Invoke(_pinballRestitution);
                        break;
                    case "set_slalom_course":
                    case "set_course":
                        if (!string.IsNullOrEmpty(slalomCourseVal)) _slalomCourse = slalomCourseVal;
                        OnSlalomConfigChanged?.Invoke(_slalomCourse, _slalomWeather);
                        break;
                    case "set_slalom_weather":
                    case "set_snow":
                        if (!string.IsNullOrEmpty(slalomWeatherVal)) _slalomWeather = slalomWeatherVal;
                        OnSlalomConfigChanged?.Invoke(_slalomCourse, _slalomWeather);
                        break;
                    case "set_slalom_config":
                        if (!string.IsNullOrEmpty(slalomCourseVal)) _slalomCourse = slalomCourseVal;
                        if (!string.IsNullOrEmpty(slalomWeatherVal)) _slalomWeather = slalomWeatherVal;
                        OnSlalomConfigChanged?.Invoke(_slalomCourse, _slalomWeather);
                        break;
                    case "set_river_diff":
                    case "set_river_difficulty":
                    case "set_difficulty":
                        if (!string.IsNullOrEmpty(riverDiffVal)) _riverDifficulty = riverDiffVal;
                        OnRiverDifficultyChanged?.Invoke(_riverDifficulty);
                        break;
                    case "set_player_name":
                        if (!string.IsNullOrEmpty(playerName))
                        {
                            PlayerName = playerName;
                            LeaderboardService.Instance.ActivePlayerName = playerName;
                            OnPlayerNameChanged?.Invoke(PlayerName);
                        }
                        break;
                    case "reset_leaderboard":
                        LeaderboardService.Instance.Clear();
                        OnResetLeaderboardRequested?.Invoke();
                        break;
                    case "start_recording":
                        OnStartRecordingRequested?.Invoke();
                        break;
                    case "stop_recording":
                        OnStopRecordingRequested?.Invoke();
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
                hasMag = boardState.HasMag,
                compassHeadingDeg = boardState.CompassHeadingDeg,
                rawMag = new { x = boardState.RawMag.X, y = boardState.RawMag.Y, z = boardState.RawMag.Z },
                rawAccel = new { x = boardState.RawAccel.X, y = boardState.RawAccel.Y, z = boardState.RawAccel.Z },
                rawGyro = new { x = boardState.RawGyro.X, y = boardState.RawGyro.Y, z = boardState.RawGyro.Z },
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
            leaderboard = LeaderboardService.Instance.GetTopEntries(),
            settings = new
            {
                sensitivity,
                invertRoll,
                invertPitch,
                playerName = PlayerName,
                mountingProfile = (invertRoll && invertPitch) ? "underneath" : (!invertRoll && !invertPitch ? "top" : "custom")
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
