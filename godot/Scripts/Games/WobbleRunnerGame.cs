using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 2: Wobble Runner.
/// Slalom runner where the balance board steers a hoverboarder left and right
/// through approaching score gates while dodging obstacles.
/// </summary>
public partial class WobbleRunnerGame : BaseGameMode
{
    public override string GameId => "WobbleRunner";
    public override string GameTitle => "Wobble Runner";
    public override string GameDescription => "Balance left and right to weave through slalom gates and avoid obstacles!";
    public override string GameIcon => "🏃";

    [Export] public float TrackWidth { get; set; } = 12.0f;
    [Export] public float BaseSpeed { get; set; } = 16.0f;

    private Node3D? _playerNode;
    private Node3D? _trackContainer;
    private Label3D? _feedbackLabel;

    private float _playerX;
    private float _distanceRun;
    private float _currentSpeed;
    private float _gateSpawnTimer;
    private readonly List<GateInstance> _activeGates = new();

    private record struct GateInstance(Node3D Node, float Z, float CenterX, bool IsObstacle, bool Cleared);

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Track container
        _trackContainer = new Node3D { Name = "Track" };
        AddChild(_trackContainer);

        // Ground track mesh
        var floorMesh = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(TrackWidth, 120.0f) },
            Position = new Vector3(0, 0, -50.0f)
        };
        var floorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.12f, 0.2f),
            Roughness = 0.4f
        };
        floorMesh.MaterialOverride = floorMat;
        _trackContainer.AddChild(floorMesh);

        // Track side rails
        for (int i = -1; i <= 1; i += 2)
        {
            var rail = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.8f, 120.0f) },
                Position = new Vector3(i * (TrackWidth * 0.5f), 0.4f, -50.0f)
            };
            var railMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.6f, 0.95f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.6f, 0.95f) * 0.5f
            };
            rail.MaterialOverride = railMat;
            _trackContainer.AddChild(rail);
        }

        // 2. Player Hoverboard
        _playerNode = new Node3D { Name = "Player" };
        AddChild(_playerNode);

        var boardMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.2f, 3.0f) },
            Position = new Vector3(0, 0.2f, 0)
        };
        var boardMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.4f, 0.1f),
            Metallic = 0.5f
        };
        boardMesh.MaterialOverride = boardMat;
        _playerNode.AddChild(boardMesh);

        var riderMesh = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.4f },
            Position = new Vector3(0, 1.0f, 0)
        };
        var riderMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.85f, 0.5f)
        };
        riderMesh.MaterialOverride = riderMat;
        _playerNode.AddChild(riderMesh);

        // 3. Feedback label
        _feedbackLabel = new Label3D
        {
            Text = "LEAN LEFT & RIGHT TO WEAVE",
            FontSize = 32,
            Position = new Vector3(0, 4.0f, -6.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(_feedbackLabel);

        // 4. Follow Camera & Light
        var camera = new Camera3D
        {
            Position = new Vector3(0, 4.5f, 7.5f),
            RotationDegrees = new Vector3(-18, 0, 0)
        };
        AddChild(camera);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-40, 25, 0),
            LightEnergy = 1.1f,
            ShadowEnabled = true
        };
        AddChild(light);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _playerX = 0f;
        _distanceRun = 0f;
        _currentSpeed = BaseSpeed;
        _gateSpawnTimer = 0f;

        // Clear existing gate meshes
        foreach (var gate in _activeGates)
        {
            gate.Node.QueueFree();
        }
        _activeGates.Clear();

        if (_playerNode != null) _playerNode.Position = Vector3.Zero;
        if (_feedbackLabel != null) _feedbackLabel.Text = "LEAN LEFT & RIGHT TO WEAVE";
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        if (GameState == "START" && MathF.Abs(boardState.Roll) > 0.05f)
        {
            StartGame();
            if (_feedbackLabel != null) _feedbackLabel.Text = "";
        }

        if (GameState != "RUNNING") return;

        // 1. Steering: boardState.Roll shifts player laterally
        float steerSpeed = 14.0f;
        _playerX += boardState.Roll * steerSpeed * dt;
        float limitX = (TrackWidth * 0.5f) - 1.2f;
        _playerX = Math.Clamp(_playerX, -limitX, limitX);

        if (_playerNode != null)
        {
            _playerNode.Position = new Vector3(_playerX, 0, 0);
            _playerNode.Rotation = new Vector3(0, 0, -boardState.Roll * 0.35f);
        }

        // 2. Progression & Distance
        _distanceRun += _currentSpeed * dt;
        _currentSpeed = BaseSpeed + (_distanceRun * 0.015f);

        // 3. Spawning gates / obstacles
        _gateSpawnTimer -= dt;
        if (_gateSpawnTimer <= 0f)
        {
            SpawnGate();
            _gateSpawnTimer = Math.Max(0.6f, 1.4f - (_distanceRun * 0.001f));
        }

        // 4. Move and check collision for active gates
        for (int i = _activeGates.Count - 1; i >= 0; i--)
        {
            var gate = _activeGates[i];
            float newZ = gate.Z + _currentSpeed * dt;

            gate.Node.Position = new Vector3(gate.CenterX, 0, newZ);

            // Check if player passed the gate
            if (!gate.Cleared && newZ >= -0.5f && newZ <= 1.0f)
            {
                float dx = MathF.Abs(_playerX - gate.CenterX);
                if (gate.IsObstacle)
                {
                    if (dx < 1.4f)
                    {
                        TriggerGameOver("Crashed into an obstacle!");
                        if (_feedbackLabel != null) _feedbackLabel.Text = "CRASH! Reset to try again";
                        return;
                    }
                }
                else
                {
                    // Scoring Gate (width ~ 3.5m)
                    if (dx < 1.8f)
                    {
                        AddScore(50);
                        if (_feedbackLabel != null) _feedbackLabel.Text = "+50 GATE PASS!";
                    }
                }
                gate = gate with { Cleared = true };
            }

            if (newZ > 10.0f)
            {
                gate.Node.QueueFree();
                _activeGates.RemoveAt(i);
            }
            else
            {
                _activeGates[i] = gate with { Z = newZ };
            }
        }
    }

    private void SpawnGate()
    {
        if (_trackContainer == null) return;

        bool isObstacle = GD.Randf() < 0.35f;
        float halfW = (TrackWidth * 0.5f) - 2.5f;
        float posX = (float)(GD.Randf() * (halfW * 2f) - halfW);

        var gateNode = new Node3D();
        _trackContainer.AddChild(gateNode);
        gateNode.Position = new Vector3(posX, 0, -90.0f);

        if (isObstacle)
        {
            // Red hazard barrier
            var pylon = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.6f, Height = 1.6f },
                Position = new Vector3(0, 0.8f, 0)
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.15f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(0.9f, 0.15f, 0.15f) * 0.6f
            };
            pylon.MaterialOverride = mat;
            gateNode.AddChild(pylon);
        }
        else
        {
            // Golden score arch
            var arch = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 1.6f, OuterRadius = 1.9f },
                Position = new Vector3(0, 1.8f, 0),
                RotationDegrees = new Vector3(90, 0, 0)
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.8f, 0.1f),
                EmissionEnabled = true,
                Emission = new Color(0.95f, 0.8f, 0.1f) * 0.8f
            };
            arch.MaterialOverride = mat;
            gateNode.AddChild(arch);
        }

        _activeGates.Add(new GateInstance(gateNode, -90.0f, posX, isObstacle, false));
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        var stats = base.GetLiveStats();
        stats["distance"] = (int)_distanceRun;
        stats["speed"] = (int)_currentSpeed;
        return stats;
    }
}
