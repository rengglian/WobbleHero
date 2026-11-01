using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 3: Sensor Lab & Calibration Bench.
/// Real-time 3D board visualizer and IMU diagnostic bench. Renders a physical round
/// balance board mirroring the actual hardware board's Roll, Pitch, and Magnetometer Yaw.
/// </summary>
public partial class SensorLabGame : BaseGameMode
{
    public override string GameId => "SensorLab";
    public override string GameTitle => "Sensor Lab & Diagnostics";
    public override string GameDescription => "Real-time 3D round balance board visualizer with full 9-DoF Roll, Pitch, and Compass Yaw.";
    public override string GameIcon => "🔬";

    // 2-Stage Rotational Hierarchy:
    // _yawPivot rotates around world Y axis (driven by Magnetometer Compass)
    // _boardPivot tilts roll & pitch in the board's own local reference frame
    private Node3D? _yawPivot;
    private Node3D? _boardPivot;
    private MeshInstance3D? _bubbleIndicator;

    public MovementRecorder Recorder { get; } = new();

    private float _displayRoll;
    private float _displayPitch;
    private float _displayYaw;
    private float _headingOffset;
    private bool _hasInitialHeading;
    private bool _lastSpaceKeyState;
    private BoardState _latestBoardState = BoardState.Default;

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Fulcrum Stand on Ground
        var fulcrum = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 1.3f, Height = 0.9f },
            Position = new Vector3(0, -0.65f, 0)
        };
        var baseMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.22f, 0.32f),
            Roughness = 0.6f
        };
        fulcrum.MaterialOverride = baseMat;
        AddChild(fulcrum);

        // 2. Yaw Pivot (Rotates around Y with Magnetometer)
        _yawPivot = new Node3D { Name = "YawPivot" };
        AddChild(_yawPivot);

        // 3. Board Pivot (Tilts Roll & Pitch relative to the board)
        _boardPivot = new Node3D { Name = "BoardPivot" };
        _yawPivot.AddChild(_boardPivot);

        // --- ROUND BALANCE BOARD DECK ---
        const float boardRadius = 2.6f;
        const float boardHeight = 0.22f;

        // Circular wooden board body
        var boardMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = boardRadius,
                BottomRadius = boardRadius * 0.97f,
                Height = boardHeight,
                RadialSegments = 48
            },
            Position = Vector3.Zero
        };
        var boardMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.58f, 0.32f), // Warm birch / maple wood
            Roughness = 0.35f,
            Metallic = 0.05f
        };
        boardMesh.MaterialOverride = boardMat;
        _boardPivot.AddChild(boardMesh);

        // Outer beveled rim accent
        var rimMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh
            {
                InnerRadius = boardRadius - 0.14f,
                OuterRadius = boardRadius + 0.04f,
                Rings = 32,
                RingSegments = 16
            },
            Position = new Vector3(0, boardHeight * 0.4f, 0)
        };
        var rimMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.22f, 0.12f),
            Roughness = 0.4f
        };
        rimMesh.MaterialOverride = rimMat;
        _boardPivot.AddChild(rimMesh);

        // Concentric non-slip rubber grip ring
        var gripOuter = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 2.25f,
                BottomRadius = 2.25f,
                Height = 0.02f,
                RadialSegments = 48
            },
            Position = new Vector3(0, boardHeight * 0.5f + 0.01f, 0)
        };
        var gripMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.15f, 0.20f), // Dark graphite grip
            Roughness = 0.85f
        };
        gripOuter.MaterialOverride = gripMat;
        _boardPivot.AddChild(gripOuter);

        // Inner circular wooden inlay
        var woodInlay = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 1.6f,
                BottomRadius = 1.6f,
                Height = 0.024f,
                RadialSegments = 36
            },
            Position = new Vector3(0, boardHeight * 0.5f + 0.012f, 0)
        };
        woodInlay.MaterialOverride = boardMat;
        _boardPivot.AddChild(woodInlay);

        // Center circular grip bullseye
        var gripCenter = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.65f,
                BottomRadius = 0.65f,
                Height = 0.026f,
                RadialSegments = 32
            },
            Position = new Vector3(0, boardHeight * 0.5f + 0.014f, 0)
        };
        gripCenter.MaterialOverride = gripMat;
        _boardPivot.AddChild(gripCenter);

        // Forward / North Directional Arrow (Points towards -Z)
        var arrowMesh = new MeshInstance3D
        {
            Mesh = new PrismMesh
            {
                Size = new Vector3(0.45f, 0.65f, 0.04f)
            },
            Position = new Vector3(0, boardHeight * 0.5f + 0.025f, -1.95f),
            RotationDegrees = new Vector3(90, 0, 0)
        };
        var arrowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.85f, 1.0f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.85f, 1.0f) * 0.7f
        };
        arrowMesh.MaterialOverride = arrowMat;
        _boardPivot.AddChild(arrowMesh);

        // Fulcrum Dome (Underneath Board Center)
        var dome = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.65f,
                Height = 1.3f
            },
            Position = new Vector3(0, -0.42f, 0)
        };
        var domeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.18f, 0.25f),
            Roughness = 0.4f,
            Metallic = 0.3f
        };
        dome.MaterialOverride = domeMat;
        _boardPivot.AddChild(dome);

        // AtomS3 Controller Representation (Mounted upside down UNDERNEATH the board)
        var atomNode = new Node3D
        {
            Name = "AtomS3Underneath",
            Position = new Vector3(0, -boardHeight * 0.5f - 0.06f, 0),
            RotationDegrees = new Vector3(180, 0, 0) // Upside-down mounting
        };
        _boardPivot.AddChild(atomNode);

        var atomBody = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.22f, 0.55f) }
        };
        var atomMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.75f, 0.95f),
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.75f, 0.95f) * 0.4f
        };
        atomBody.MaterialOverride = atomMat;
        atomNode.AddChild(atomBody);

        // 4. Level Bubble on Board Top Surface
        _bubbleIndicator = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f },
            Position = new Vector3(0, boardHeight * 0.5f + 0.15f, 0)
        };
        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.95f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.95f, 0.4f) * 0.9f
        };
        _bubbleIndicator.MaterialOverride = bubbleMat;
        _boardPivot.AddChild(_bubbleIndicator);

        // 5. Camera & Dynamic Lighting
        var camera = new Camera3D
        {
            Position = new Vector3(0, 4.2f, 6.6f),
            RotationDegrees = new Vector3(-26, 0, 0)
        };
        AddChild(camera);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, 35, 0),
            LightEnergy = 1.3f,
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
        if (Manager?.WebServer != null)
        {
            Manager.WebServer.OnTareRequested += ResetHeadingOffset;
        }
    }

    public override void _ExitTree()
    {
        if (Manager?.BoardInput != null)
        {
            Manager.BoardInput.OnPacketReceived -= HandlePacketReceived;
        }
        if (Manager?.WebServer != null)
        {
            Manager.WebServer.OnTareRequested -= ResetHeadingOffset;
        }
        base._ExitTree();
    }

    public void ResetHeadingOffset()
    {
        _headingOffset = _displayYaw;
        GD.Print($"[SensorLabGame] Centered compass heading zero: offset={_headingOffset:F1}°");
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

        // Magnetometer Compass Yaw Tracking
        if (boardState.HasMag)
        {
            float targetHeading = boardState.CompassHeadingDeg;

            // If mounted upside down underneath board, clockwise rotation in Earth coordinates
            // is read with opposite sign by the inverted sensor.
            bool isUpsideDown = Manager?.BoardInput?.InvertRoll ?? true;
            if (isUpsideDown)
            {
                targetHeading = Mathf.PosMod(-targetHeading, 360.0f);
            }

            if (!_hasInitialHeading)
            {
                _headingOffset = targetHeading;
                _displayYaw = targetHeading;
                _hasInitialHeading = true;
            }

            // Smooth shortest-path angle interpolation across 0°/360° boundary
            float diff = Mathf.PosMod(targetHeading - _displayYaw + 180.0f, 360.0f) - 180.0f;
            _displayYaw += diff * (float)Math.Clamp(delta * 8.0, 0.0, 1.0);
            _displayYaw = Mathf.PosMod(_displayYaw, 360.0f);
        }
        else
        {
            // Keyboard fallback: Q (turn left) and E (turn right)
            if (Godot.Input.IsKeyPressed(Key.Q)) _displayYaw -= (float)(90.0 * delta);
            if (Godot.Input.IsKeyPressed(Key.E)) _displayYaw += (float)(90.0 * delta);
            _displayYaw = Mathf.PosMod(_displayYaw, 360.0f);
        }

        // Space / R key: Toggle recording
        bool spacePressed = Godot.Input.IsKeyPressed(Key.Space) || Godot.Input.IsKeyPressed(Key.R);
        if (spacePressed && !_lastSpaceKeyState)
        {
            ToggleRecording();
        }
        _lastSpaceKeyState = spacePressed;

        // T key: Tare / Zero Heading
        if (Godot.Input.IsKeyPressed(Key.T))
        {
            ResetHeadingOffset();
        }

        // Fallback: capture motion sample in physics loop if not receiving hardware UDP packets
        if (Recorder.IsRecording && !boardState.IsActive)
        {
            Recorder.RecordSample(boardState);
        }

        // 1. Yaw rotation around vertical Y axis driven by Magnetometer Compass
        if (_yawPivot != null)
        {
            float rotY = -Mathf.DegToRad(_displayYaw - _headingOffset);
            _yawPivot.Rotation = new Vector3(0, rotY, 0);
        }

        // 2. Roll & Pitch tilt in the round board's local coordinate frame
        if (_boardPivot != null)
        {
            float rotZ = -Mathf.DegToRad(_displayRoll);
            float rotX = Mathf.DegToRad(_displayPitch);
            _boardPivot.Rotation = new Vector3(rotX, 0, rotZ);
        }

        // 3. Shift level bubble across round board surface
        if (_bubbleIndicator != null)
        {
            const float maxBubbleRadius = 2.1f;
            var tiltVector = new Vector2(boardState.Roll, boardState.Pitch);
            if (tiltVector.Length() > 1.0f)
            {
                tiltVector = tiltVector.Normalized();
            }

            float bubbleX = tiltVector.X * maxBubbleRadius;
            float bubbleZ = tiltVector.Y * maxBubbleRadius;
            _bubbleIndicator.Position = new Vector3(bubbleX, 0.15f, bubbleZ);
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
        stats["yawDeg"] = Mathf.PosMod(_displayYaw - _headingOffset, 360.0f);
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
