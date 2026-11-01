using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 1: Balance Trainer.
/// Tests 2D balance board coordination (Roll & Pitch). The player tilts a circular
/// dish to guide a rolling marble into target zones while avoiding rolling off the rim.
/// </summary>
public partial class BalanceTrainerGame : BaseGameMode
{
    public override string GameId => "BalanceTrainer";
    public override string GameTitle => "Balance Trainer";
    public override string GameDescription => "Tilt your board in 2D to guide the ball to glowing target zones and build your combo!";
    public override string GameIcon => "⚖️";

    [Export] public float DishRadius { get; set; } = 6.0f;
    [Export] public float Gravity { get; set; } = 6.5f;
    [Export] public float Damping { get; set; } = 0.88f;

    // Node references
    private Node3D? _dishNode;
    private MeshInstance3D? _ballMesh;
    private MeshInstance3D? _targetRing;
    private Label3D? _feedbackLabel;

    private Vector2 _ballPos;
    private Vector2 _ballVel;
    private Vector2 _targetPos;
    private int _ringsCollected;
    private int _fallsCount;
    private float _timeInCurrentGame;
    private float _multiplier = 1.0f;
    private float _feedbackTimer;

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Root dish container that tilts
        _dishNode = new Node3D { Name = "Dish" };
        AddChild(_dishNode);

        // Circular dish surface mesh
        var dishMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = DishRadius,
                BottomRadius = DishRadius * 0.95f,
                Height = 0.4f
            }
        };
        var dishMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.16f, 0.25f),
            Roughness = 0.3f,
            Metallic = 0.2f
        };
        dishMesh.MaterialOverride = dishMat;
        dishMesh.Position = new Vector3(0, -0.2f, 0);
        _dishNode.AddChild(dishMesh);

        // Outer bouncy rim bumper
        var rimMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh
            {
                InnerRadius = DishRadius - 0.45f,
                OuterRadius = DishRadius + 0.15f
            }
        };
        var rimMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.45f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.45f, 0.2f) * 0.4f
        };
        rimMesh.MaterialOverride = rimMat;
        rimMesh.Position = new Vector3(0, 0.05f, 0);
        _dishNode.AddChild(rimMesh);

        // 2. Rolling Ball
        _ballMesh = new MeshInstance3D
        {
            Name = "Ball",
            Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }
        };
        var ballMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.85f, 0.5f),
            Roughness = 0.15f,
            Metallic = 0.8f,
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.85f, 0.5f) * 0.5f
        };
        _ballMesh.MaterialOverride = ballMat;
        _ballMesh.Position = new Vector3(0, 0.45f, 0);
        _dishNode.AddChild(_ballMesh);

        // 3. Target Ring (slightly wider for comfortable standing balance)
        _targetRing = new MeshInstance3D
        {
            Name = "TargetRing",
            Mesh = new TorusMesh { InnerRadius = 0.95f, OuterRadius = 1.25f }
        };
        var targetMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.8f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.8f, 0.1f) * 0.8f
        };
        _targetRing.MaterialOverride = targetMat;
        _targetRing.Position = new Vector3(0, 0.05f, 0);
        _dishNode.AddChild(_targetRing);

        // 4. In-world Feedback label
        _feedbackLabel = new Label3D
        {
            Text = "TILT TO BEGIN",
            FontSize = 36,
            Position = new Vector3(0, 3.5f, -DishRadius),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(_feedbackLabel);

        // Camera & Light setup
        var camera = new Camera3D
        {
            Position = new Vector3(0, 10.5f, 11.0f),
            RotationDegrees = new Vector3(-45, 0, 0)
        };
        AddChild(camera);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, 35, 0),
            LightEnergy = 1.2f,
            ShadowEnabled = true
        };
        AddChild(light);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _ballPos = Vector2.Zero;
        _ballVel = Vector2.Zero;
        _ringsCollected = 0;
        _fallsCount = 0;
        _multiplier = 1.0f;
        _timeInCurrentGame = 0f;
        _feedbackTimer = 0f;
        SpawnNewTarget();
        UpdateVisuals();
        if (_feedbackLabel != null) _feedbackLabel.Text = "TILT TO BEGIN";
    }

    private void SpawnNewTarget()
    {
        // Pick random position within 70% of dish radius
        float angle = (float)(GD.Randf() * Math.PI * 2.0);
        float dist = (float)(GD.Randf() * (DishRadius * 0.65f) + 1.2f);
        _targetPos = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
        if (_targetRing != null)
        {
            _targetRing.Position = new Vector3(_targetPos.X, 0.05f, _targetPos.Y);
        }
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on movement
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.08f || MathF.Abs(boardState.Pitch) > 0.08f))
        {
            StartGame();
            if (_feedbackLabel != null) _feedbackLabel.Text = "";
        }

        if (GameState != "RUNNING") return;

        _timeInCurrentGame += dt;

        // Feedback message auto-fade timer
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= dt;
            if (_feedbackTimer <= 0f && _feedbackLabel != null)
            {
                _feedbackLabel.Text = "";
            }
        }

        // 1. Tilt Dish: Roll rotates Z (sideways tilt), Pitch rotates X (forward/backward tilt)
        float maxTiltRad = Mathf.DegToRad(14.0f);
        float targetTiltX = boardState.Pitch * maxTiltRad;
        float targetTiltZ = -boardState.Roll * maxTiltRad;

        if (_dishNode != null)
        {
            _dishNode.Rotation = new Vector3(targetTiltX, 0, targetTiltZ);
        }

        // 2. Physics:
        // A. Tilt acceleration from board lean
        Vector2 tiltAccel = new(boardState.Roll * Gravity, boardState.Pitch * Gravity);

        // B. Subtle concave dish bowl centering tendency:
        // Creates a restorative gravity well towards the center so the ball stabilizes when level,
        // rather than endlessly sliding across a flat frictionless plane.
        Vector2 bowlForce = -_ballPos * 0.40f;

        _ballVel += (tiltAccel + bowlForce) * dt;

        // C. Viscous rolling damping (smooth exponential decay per frame)
        _ballVel *= MathF.Pow(Damping, dt * 60f);
        _ballPos += _ballVel * dt;

        // 3. Collision with Target Ring (generous capture radius for standing board play)
        float distToTarget = _ballPos.DistanceTo(_targetPos);
        if (distToTarget < 1.35f)
        {
            _ringsCollected++;
            _multiplier = Math.Min(4.0f, _multiplier + 0.2f);
            int points = (int)(100 * _multiplier);
            AddScore(points);
            SpawnNewTarget();

            if (_feedbackLabel != null)
            {
                _feedbackLabel.Text = $"+{points}! (x{_multiplier:F1})";
                _feedbackTimer = 1.5f;
            }
        }

        // 4. Elastic Bumper Rim & Soft Containment:
        // When standing on an elevated round board (Koor Board), rim bouncing keeps the game in flow!
        float rimMargin = DishRadius - 0.45f;
        float currentDist = _ballPos.Length();
        if (currentDist > rimMargin)
        {
            Vector2 normal = _ballPos.Normalized();
            _ballPos = normal * rimMargin;

            // Soft elastic bounce inward
            float radialVel = _ballVel.Dot(normal);
            if (radialVel > 0f)
            {
                _ballVel -= normal * (radialVel * 1.45f);
            }

            // Slight combo decay if pinned against the rim under hard tilt
            if (tiltAccel.Length() > Gravity * 0.75f && _multiplier > 1.0f && _feedbackTimer <= 0f)
            {
                _multiplier = 1.0f;
                if (_feedbackLabel != null)
                {
                    _feedbackLabel.Text = "RIM BUMP! (Combo Reset)";
                    _feedbackTimer = 1.2f;
                }
            }
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_ballMesh != null)
        {
            _ballMesh.Position = new Vector3(_ballPos.X, 0.45f, _ballPos.Y);
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        var stats = base.GetLiveStats();
        stats["ringsCollected"] = _ringsCollected;
        stats["multiplier"] = _multiplier;
        stats["falls"] = _fallsCount;
        stats["timeRemaining"] = Math.Max(0, 60.0f - _timeInCurrentGame);
        stats["ballDist"] = _ballPos.Length();
        return stats;
    }
}
