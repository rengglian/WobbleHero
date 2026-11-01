using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Input;
using WobbleHero.Networking;
using WobbleHero.UI;
using WobbleHero.Games;

namespace WobbleHero.Core;

/// <summary>
/// Central coordinator for WobbleHero. Connects balance board input,
/// embedded companion server, in-game HUD, and active mini-game mode.
/// </summary>
public partial class GameManager : Node
{
    [Export] public NodePath? GameContainerPath { get; set; }
    [Export] public NodePath? BoardInputPath { get; set; }
    [Export] public NodePath? WebServerPath { get; set; }
    [Export] public NodePath? HudPath { get; set; }
    [Export] public string DefaultGameId { get; set; } = "BalanceTrainer";

    public BalanceBoardInput? BoardInput { get; private set; }
    public CompanionWebServer? WebServer { get; private set; }
    public CompanionHud? Hud { get; private set; }
    public Node3D? GameContainer { get; private set; }

    public IGameMode? CurrentGame { get; private set; }

    private double _telemetryTimer;
    private const double TelemetryInterval = 1.0 / 30.0; // 30 Hz

    public override void _Ready()
    {
        // Resolve nodes
        BoardInput = GetNodeOrNull<BalanceBoardInput>(BoardInputPath ?? "BalanceBoardInput");
        WebServer = GetNodeOrNull<CompanionWebServer>(WebServerPath ?? "CompanionWebServer");
        Hud = GetNodeOrNull<CompanionHud>(HudPath ?? "CompanionHud");
        GameContainer = GetNodeOrNull<Node3D>(GameContainerPath ?? "GameContainer");

        if (Hud != null)
        {
            Hud.Initialize(this);
            if (WebServer != null)
            {
                Hud.UpdateCompanionUrl(WebServer.GetCompanionUrl());
            }
        }

        // Wire companion server events
        if (WebServer != null)
        {
            WebServer.OnSwitchGameRequested += SwitchGame;
            WebServer.OnStartGameRequested += () => CurrentGame?.StartGame();
            WebServer.OnPauseGameRequested += () => CurrentGame?.PauseGame();
            WebServer.OnResumeGameRequested += () => CurrentGame?.ResumeGame();
            WebServer.OnResetGameRequested += () => CurrentGame?.ResetGame();
            WebServer.OnTareRequested += () => BoardInput?.TareZero();
            WebServer.OnResetCalRequested += () => BoardInput?.ResetCalibration();
            WebServer.OnSensitivityChanged += (val) =>
            {
                if (BoardInput != null) BoardInput.Sensitivity = val;
                SaveSettings();
            };
            WebServer.OnToggleInvertRequested += (axis) =>
            {
                if (BoardInput == null) return;
                if (axis == "pitch") BoardInput.InvertPitch = !BoardInput.InvertPitch;
                else BoardInput.InvertRoll = !BoardInput.InvertRoll;
                SaveSettings();
            };
            WebServer.OnMountingProfileChanged += (profile) =>
            {
                if (BoardInput == null) return;
                if (profile == "underneath")
                {
                    BoardInput.InvertRoll = true;
                    BoardInput.InvertPitch = true;
                }
                else if (profile == "top")
                {
                    BoardInput.InvertRoll = false;
                    BoardInput.InvertPitch = false;
                }
                SaveSettings();
            };
            WebServer.OnPlayerNameChanged += (name) => SaveSettings();
            WebServer.OnSeedChanged += (seed) =>
            {
                if (CurrentGame is WobbleRunnerGame runner)
                {
                    runner.SetSeed(seed);
                }
            };
            WebServer.OnMazeConfigChanged += (seed, size, diff) =>
            {
                if (CurrentGame is WobbleMazeGame maze)
                {
                    maze.SetMazeConfig(seed, size, diff);
                }
            };
            WebServer.OnSurferConfigChanged += (speed, density) =>
            {
                if (CurrentGame is AsteroidSurferGame surfer)
                {
                    surfer.SetSurferConfig(speed, density);
                }
            };
            WebServer.OnSumoConfigChanged += (difficulty, arenaSize) =>
            {
                if (CurrentGame is WobbleSumoGame sumo)
                {
                    sumo.SetSumoConfig(difficulty, arenaSize);
                }
            };
            WebServer.OnSumoShockwaveTriggered += () =>
            {
                if (CurrentGame is WobbleSumoGame sumo)
                {
                    sumo.TriggerShockwave();
                }
            };
            WebServer.OnLanderGravityChanged += (planet) =>
            {
                if (CurrentGame is LunarLanderGame lander)
                {
                    lander.SetGravityPreset(planet);
                }
            };
            WebServer.OnEmergencyRcsTriggered += () =>
            {
                if (CurrentGame is LunarLanderGame lander)
                {
                    lander.TriggerEmergencyRcs();
                }
            };
            WebServer.OnLanderRefuelRequested += () =>
            {
                if (CurrentGame is LunarLanderGame lander)
                {
                    lander.Refuel();
                }
            };
            WebServer.OnCompassOffsetChanged += (offset) =>
            {
                if (CurrentGame is CompassLabyrinthGame compass)
                {
                    compass.SetCompassOffset(offset);
                }
            };
            WebServer.OnCompassRotateRequested += (deltaDeg) =>
            {
                if (CurrentGame is CompassLabyrinthGame compass)
                {
                    compass.RotateQuadrant(deltaDeg);
                }
            };
            WebServer.OnCompassSeedChanged += (seed) =>
            {
                if (CurrentGame is CompassLabyrinthGame compass)
                {
                    compass.SetDungeonSeed(seed);
                }
            };
            WebServer.OnStartRecordingRequested += () =>
            {
                if (CurrentGame is SensorLabGame lab) lab.StartRecording();
            };
            WebServer.OnStopRecordingRequested += () =>
            {
                if (CurrentGame is SensorLabGame lab) lab.StopRecording();
            };
            WebServer.OnGetRecordingCsv = () =>
            {
                if (CurrentGame is SensorLabGame lab) return lab.Recorder.LastSessionCsv;
                return string.Empty;
            };
        }

        // Load persistent settings (Defaults to AtomS3 mounted upside down underneath board)
        LoadSettings();

        // Wire controller restart button
        if (BoardInput != null)
        {
            BoardInput.OnRestartRequested += () =>
            {
                GD.Print("[GameManager] Controller button pressed -> Reset/Start Game");
                if (CurrentGame?.GameState == "RUNNING")
                {
                    CurrentGame.ResetGame();
                }
                else
                {
                    CurrentGame?.StartGame();
                }
            };
        }

        // Load default game mode
        SwitchGame(DefaultGameId);
    }

    /// <summary>
    /// Dynamically switches the active mini-game mode.
    /// </summary>
    public void SwitchGame(string gameId)
    {
        var entry = GameRegistry.FindGame(gameId);
        if (entry == null)
        {
            GD.PrintErr($"[GameManager] Game '{gameId}' not found in GameRegistry!");
            return;
        }

        GD.Print($"[GameManager] Switching to game: {entry.Title} ({entry.Id})");

        // 1. Teardown previous game
        if (CurrentGame is Node prevNode)
        {
            if (prevNode.GetParent() != null)
            {
                prevNode.GetParent().RemoveChild(prevNode);
            }
            prevNode.QueueFree();
            CurrentGame = null;
        }

        // 2. Instantiate and mount new game
        if (GameContainer != null)
        {
            var scene = GD.Load<PackedScene>(entry.ScenePath);
            if (scene != null)
            {
                var instance = scene.Instantiate();
                GameContainer.AddChild(instance);

                if (instance is IGameMode newGame)
                {
                    CurrentGame = newGame;
                    newGame.Initialize(this);
                    GD.Print($"[GameManager] Initialized {newGame.GameTitle}");
                }
                else
                {
                    GD.PrintErr($"[GameManager] Root node in '{entry.ScenePath}' does not implement IGameMode!");
                }
            }
            else
            {
                GD.PrintErr($"[GameManager] Could not load scene at {entry.ScenePath}");
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var state = BoardInput?.CurrentState ?? BoardState.Default;

        // Feed balance board input into current game mode
        CurrentGame?.UpdateBoardInput(state, delta);

        // Update active mini-game HUD
        CurrentGame?.UpdateHud(state);

        // Update legacy global HUD if present
        if (Hud != null && CurrentGame != null)
        {
            Hud.UpdateTelemetry(state, CurrentGame.GameTitle, CurrentGame.GameState, CurrentGame.CurrentScore);
        }

        // Broadcast WebSocket telemetry to companion remotes
        _telemetryTimer += delta;
        if (_telemetryTimer >= TelemetryInterval)
        {
            _telemetryTimer = 0;
            if (WebServer != null && CurrentGame != null && BoardInput != null)
            {
                var liveStats = CurrentGame.GetLiveStats() ?? new Dictionary<string, object>();
                WebServer.BroadcastTelemetry(
                    state,
                    CurrentGame.GameId,
                    CurrentGame.GameTitle,
                    CurrentGame.GameState,
                    CurrentGame.CurrentScore,
                    liveStats,
                    BoardInput.Sensitivity,
                    BoardInput.InvertRoll,
                    BoardInput.InvertPitch
                );
            }
        }
    }

    private const string SettingsFile = "user://settings.json";

    public void LoadSettings()
    {
        if (Godot.FileAccess.FileExists(SettingsFile))
        {
            try
            {
                using var file = Godot.FileAccess.Open(SettingsFile, Godot.FileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (BoardInput != null)
                {
                    if (root.TryGetProperty("invertRoll", out var ir)) BoardInput.InvertRoll = ir.GetBoolean();
                    if (root.TryGetProperty("invertPitch", out var ip)) BoardInput.InvertPitch = ip.GetBoolean();
                    if (root.TryGetProperty("sensitivity", out var sens)) BoardInput.Sensitivity = (float)sens.GetDouble();
                }
                if (WebServer != null && root.TryGetProperty("playerName", out var pn))
                {
                    string name = pn.GetString() ?? "Hero 1";
                    WebServer.PlayerName = name;
                    LeaderboardService.Instance.ActivePlayerName = name;
                }
                GD.Print($"[GameManager] Loaded user settings: InvertRoll={BoardInput?.InvertRoll}, InvertPitch={BoardInput?.InvertPitch}");
                return;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameManager] Error loading settings: {ex.Message}");
            }
        }

        // Default: AtomS3 is mounted upside down underneath the board -> InvertRoll=true, InvertPitch=true
        if (BoardInput != null)
        {
            BoardInput.InvertRoll = true;
            BoardInput.InvertPitch = true;
            BoardInput.Sensitivity = 1.0f;
        }
        SaveSettings();
    }

    public void SaveSettings()
    {
        try
        {
            var data = new
            {
                invertRoll = BoardInput?.InvertRoll ?? true,
                invertPitch = BoardInput?.InvertPitch ?? true,
                sensitivity = BoardInput?.Sensitivity ?? 1.0f,
                playerName = WebServer?.PlayerName ?? "Hero 1"
            };
            string json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            using var file = Godot.FileAccess.Open(SettingsFile, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameManager] Error saving settings: {ex.Message}");
        }
    }
}
