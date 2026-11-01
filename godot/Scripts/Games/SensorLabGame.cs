using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 3: Sensor Lab & Calibration Bench.
/// Real-time 3D board visualizer and IMU diagnostic bench. Renders a physical 3D
/// balance board mirroring the actual hardware board's roll and pitch angles.
/// </summary>
public partial class SensorLabGame : BaseGameMode
{
    public override string GameId => "SensorLab";
    public override string GameTitle => "Sensor Lab & Diagnostics";
    public override string GameDescription => "Real-time 3D balance board visualizer and IMU calibration bench.";
    public override string GameIcon => "🔬";

    private Node3D? _boardPivot;
    private Label3D? _telemetryLabel;
    private Label3D? _statusLabel;
    private Label3D? _recordLabel;
    private MeshInstance3D? _bubbleIndicator;

    public MovementRecorder Recorder { get; } = new();

    private float _displayRoll;
    private float _displayPitch;
    private bool _lastSpaceKeyState;
    private BoardState _latestBoardState = BoardState.Default;

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Fulcrum Base (Stand)
        var fulcrum = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 1.2f, Height = 1.0f },
            Position = new Vector3(0, -0.5f, 0)
        };
        var baseMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.25f, 0.35f),
            Roughness = 0.5f
        };
        fulcrum.MaterialOverride = baseMat;
        AddChild(fulcrum);

        // 2. Board Pivot (Tilts with roll and pitch)
        _boardPivot = new Node3D { Name = "BoardPivot" };
        AddChild(_boardPivot);

        // Wooden board deck
        var boardMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(6.5f, 0.25f, 2.5f) },
            Position = Vector3.Zero
        };
        var boardMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.55f, 0.25f),
            Roughness = 0.3f
        };
        boardMesh.MaterialOverride = boardMat;
        _boardPivot.AddChild(boardMesh);

        // Grip tape stripes
        for (int i = -1; i <= 1; i += 2)
        {
            var grip = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.0f, 0.26f, 2.2f) },
                Position = new Vector3(i * 2.0f, 0, 0)
            };
            var gripMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.12f, 0.15f) };
            grip.MaterialOverride = gripMat;
            _boardPivot.AddChild(grip);
        }

        // AtomS3 controller model on top of board
        var atomMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.3f, 0.5f) },
            Position = new Vector3(0, 0.25f, 0)
        };
        var atomMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.75f, 0.95f),
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.75f, 0.95f) * 0.5f
        };
        atomMesh.MaterialOverride = atomMat;
        _boardPivot.AddChild(atomMesh);

        // 3. Level bubble on board surface
        _bubbleIndicator = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f },
            Position = new Vector3(0, 0.2f, 0)
        };
        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.9f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.9f, 0.4f) * 0.8f
        };
        _bubbleIndicator.MaterialOverride = bubbleMat;
        _boardPivot.AddChild(_bubbleIndicator);

        // 4. Floating 3D Telemetry Readouts
        _telemetryLabel = new Label3D
        {
            Text = "ROLL: +0.0° | PITCH: +0.0°",
            FontSize = 36,
            Position = new Vector3(0, 3.2f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(_telemetryLabel);

        _statusLabel = new Label3D
        {
            Text = "STATUS: WAITING FOR CONTROLLER",
            FontSize = 24,
            Position = new Vector3(0, 2.4f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.6f, 0.8f, 1.0f)
        };
        AddChild(_statusLabel);

        // 5. Session Recording Status Label
        _recordLabel = new Label3D
        {
            Text = "[Press SPACE / Phone to Record Session]",
            FontSize = 22,
            Position = new Vector3(0, 1.7f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.85f, 0.85f, 0.9f)
        };
        AddChild(_recordLabel);

        // Camera & Lighting
        var camera = new Camera3D
        {
            Position = new Vector3(0, 4.0f, 7.0f),
            RotationDegrees = new Vector3(-20, 0, 0)
        };
        AddChild(camera);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, 30, 0),
            LightEnergy = 1.2f,
            ShadowEnabled = true
        };
        AddChild(light);
    }

    public override void Initialize(GameManager manager)
    {
        base.Initialize(manager);
        if (Manager?.BoardInput != null)
        {
            Manager.BoardInput.OnPacketReceived += HandlePacketReceived;
        }
    }

    public override void _ExitTree()
    {
        if (Manager?.BoardInput != null)
        {
            Manager.BoardInput.OnPacketReceived -= HandlePacketReceived;
        }
        base._ExitTree();
    }

    private void HandlePacketReceived(BoardState snapshot)
    {
        if (Recorder.IsRecording)
        {
            Recorder.RecordSample(snapshot);
        }
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        _latestBoardState = boardState;
        _displayRoll = boardState.FusedRollDeg;
        _displayPitch = boardState.FusedPitchDeg;

        // Toggle recording with Space or R key
        bool spacePressed = Godot.Input.IsKeyPressed(Key.Space) || Godot.Input.IsKeyPressed(Key.R);
        if (spacePressed && !_lastSpaceKeyState)
        {
            ToggleRecording();
        }
        _lastSpaceKeyState = spacePressed;

        // Fallback: capture motion sample in physics loop only if not receiving hardware UDP packets
        if (Recorder.IsRecording && !boardState.IsActive)
        {
            Recorder.RecordSample(boardState);
        }

        // Apply 3D rotation to board pivot
        if (_boardPivot != null)
        {
            float rotZ = -Mathf.DegToRad(_displayRoll);
            float rotX = Mathf.DegToRad(_displayPitch);
            _boardPivot.Rotation = new Vector3(rotX, 0, rotZ);
        }

        // Shift bubble position on board surface
        if (_bubbleIndicator != null)
        {
            float bubbleX = Math.Clamp(boardState.Roll * 2.8f, -2.8f, 2.8f);
            float bubbleZ = Math.Clamp(boardState.Pitch * 0.9f, -0.9f, 0.9f);
            _bubbleIndicator.Position = new Vector3(bubbleX, 0.2f, bubbleZ);
        }

        // Update readouts
        if (_telemetryLabel != null)
        {
            string signRoll = _displayRoll >= 0 ? "+" : "";
            string signPitch = _displayPitch >= 0 ? "+" : "";
            string magText = boardState.HasMag ? $" | COMPASS: {boardState.CompassHeadingDeg:F0}°" : "";
            _telemetryLabel.Text = $"ROLL: {signRoll}{_displayRoll:F1}° | PITCH: {signPitch}{_displayPitch:F1}°{magText}";
        }

        if (_statusLabel != null)
        {
            string src = boardState.Source.ToUpperInvariant();
            string active = boardState.IsActive ? $"ACTIVE ({boardState.Hz} Hz)" : "DISCONNECTED";
            _statusLabel.Text = $"SOURCE: {src} | {active} | IP: {boardState.DeviceIp}";
            _statusLabel.Modulate = boardState.IsActive ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.3f);
        }

        // Update recording label
        if (_recordLabel != null)
        {
            if (Recorder.IsRecording)
            {
                int sec = (int)Recorder.DurationSec;
                int ms = (int)((Recorder.DurationSec - sec) * 10);
                _recordLabel.Text = $"🔴 RECORDING [{sec:D2}.{ms}s] • {Recorder.SampleCount} SAMPLES • 100 Hz";
                _recordLabel.Modulate = new Color(1.0f, 0.25f, 0.25f);
            }
            else if (!string.IsNullOrEmpty(Recorder.LastSavedPath))
            {
                string filename = System.IO.Path.GetFileName(Recorder.LastSavedPath);
                _recordLabel.Text = $"💾 Saved: {filename} ({Recorder.SampleCount} samples)\n[Press SPACE / Phone to Record Again]";
                _recordLabel.Modulate = new Color(0.2f, 0.95f, 0.4f);
            }
            else
            {
                _recordLabel.Text = "[Press SPACE / Phone to Record Session]";
                _recordLabel.Modulate = new Color(0.7f, 0.85f, 1.0f);
            }
        }
    }

    public void ToggleRecording()
    {
        if (Recorder.IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        Recorder.StartRecording();
        GD.Print("[SensorLabGame] Started movement recording session.");
    }

    public void StopRecording()
    {
        Recorder.StopRecording();
        GD.Print($"[SensorLabGame] Stopped movement recording session. Saved {Recorder.SampleCount} samples.");
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        var stats = base.GetLiveStats();
        stats["rollDeg"] = _displayRoll;
        stats["pitchDeg"] = _displayPitch;
        stats["hasMag"] = _latestBoardState.HasMag;
        stats["headingDeg"] = _latestBoardState.CompassHeadingDeg;
        stats["isRecording"] = Recorder.IsRecording;
        stats["recordDuration"] = Recorder.DurationSec;
        stats["sampleCount"] = Recorder.SampleCount;
        stats["lastSavedPath"] = Recorder.LastSavedPath;
        stats["hasRecording"] = !string.IsNullOrEmpty(Recorder.LastSessionCsv);
        return stats;
    }
}
