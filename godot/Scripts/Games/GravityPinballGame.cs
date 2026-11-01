using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Gravity Pinball (Pachinko Defense).
/// The physical balance board tilts the gravity plane of an arcade pinball table.
/// Directs up to 4 simultaneous chrome pinballs through glowing pop bumpers,
/// reset drop target banks, and rollover multiplier lanes. Includes physical jerk
/// slap-nudging with an authentic arcade TILT penalty system.
/// </summary>
public partial class GravityPinballGame : BaseGameMode
{
    public override string GameId => "GravityPinball";
    public override string GameTitle => "Gravity Pinball";
    public override string GameDescription => "Tilt your board to steer gravity across the pinball table! Hit bumpers, drop targets, and trigger multiball!";
    public override string GameIcon => "🧱";

    // Table Dimensions
    public const float TableHalfWidth = 6.0f; // 12m wide
    public const float TableHalfLength = 11.0f; // 22m long
    public const float DrainHalfWidth = 2.2f;
    public const float BallRadius = 0.45f;
    public const float BallMass = 10.0f;
    public const int MaxBalls = 4;

    // Physics parameters
    [Export] public float BaseTableInclineDeg { get; set; } = 12.0f;
    [Export] public float GravityMagnitude { get; set; } = 22.0f;
    [Export] public float WallRestitution { get; set; } = 0.82f;
    [Export] public float BallRestitution { get; set; } = 0.88f;

    // Entities
    private sealed class Pinball
    {
        public Node3D Node { get; }
        public MeshInstance3D Mesh { get; }
        public Vector2 Pos;
        public Vector2 Vel;
        public bool IsActive;

        public Pinball(Node3D node, MeshInstance3D mesh)
        {
            Node = node;
            Mesh = mesh;
            Pos = Vector2.Zero;
            Vel = Vector2.Zero;
            IsActive = false;
        }
    }

    private sealed class PopBumper
    {
        public Node3D Node { get; }
        public MeshInstance3D Mesh { get; }
        public StandardMaterial3D Material { get; }
        public Vector2 Pos;
        public float Radius;
        public float FlashTimer;

        public PopBumper(Node3D node, MeshInstance3D mesh, StandardMaterial3D mat, Vector2 pos, float radius)
        {
            Node = node;
            Mesh = mesh;
            Material = mat;
            Pos = pos;
            Radius = radius;
            FlashTimer = 0f;
        }
    }

    private sealed class DropTarget
    {
        public Node3D Node { get; }
        public MeshInstance3D Mesh { get; }
        public Vector2 Pos;
        public Vector2 Size;
        public bool IsDown;

        public DropTarget(Node3D node, MeshInstance3D mesh, Vector2 pos, Vector2 size)
        {
            Node = node;
            Mesh = mesh;
            Pos = pos;
            Size = size;
            IsDown = false;
        }
    }

    private sealed class RolloverLane
    {
        public Node3D Node { get; }
        public MeshInstance3D Mesh { get; }
        public StandardMaterial3D Material { get; }
        public Vector2 Pos;
        public float Radius;
        public bool IsLit;

        public RolloverLane(Node3D node, MeshInstance3D mesh, StandardMaterial3D mat, Vector2 pos, float radius)
        {
            Node = node;
            Mesh = mesh;
            Material = mat;
            Pos = pos;
            Radius = radius;
            IsLit = false;
        }
    }

    // Node hierarchy
    private Node3D? _tableRoot;
    private Camera3D? _camera;

    // Pre-allocated Pools
    private readonly Pinball[] _balls = new Pinball[MaxBalls];
    private readonly List<PopBumper> _bumpers = new(3);
    private readonly List<DropTarget> _dropTargets = new(4);
    private readonly List<RolloverLane> _rolloverLanes = new(3);
    private readonly Dictionary<string, object> _liveStatsCache = new(16);
    private readonly List<object> _radarBallsCache = new(4);
    private readonly List<object> _radarTargetsCache = new(4);
    private readonly Random _rng = new();

    // Match Progression
    private int _reserveBalls = 3;
    private float _multiplier = 1.0f;
    private int _bumpersHitTotal = 0;
    private float _dropTargetResetTimer = 0f;
    private int _latestRankAchieved = -1;

    // Jerk Nudge & Tilt Penalty
    private Vector2 _lastBoardAngle;
    private Vector2 _lastAngularVel;
    private readonly List<float> _tiltWarningTimestamps = new(4);
    private bool _isTilted = false;
    private float _tiltFreezeTimer = 0f;
    private float _screenshakeIntensity = 0f;
    private float _nudgeCooldownTimer = 0.5f;

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Slanted Table Root
        _tableRoot = new Node3D { Name = "PinballTableRoot" };
        AddChild(_tableRoot);

        // Slanted angle orientation
        _tableRoot.RotationDegrees = new Vector3(-BaseTableInclineDeg, 0, 0);

        // Table Playfield Surface Mesh
        var playfieldMesh = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(TableHalfWidth * 2.0f, TableHalfLength * 2.0f) },
            Position = new Vector3(0, 0, 0)
        };
        var fieldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.06f, 0.08f, 0.14f),
            Roughness = 0.2f,
            Metallic = 0.4f
        };
        playfieldMesh.MaterialOverride = fieldMat;
        _tableRoot.AddChild(playfieldMesh);

        // Neon Boundary Walls
        BuildTableWalls();

        // 2. Pop Bumpers (Triangular Formation)
        Vector2[] bumperPositions =
        {
            new(0f, -5.5f),      // Top Center
            new(-2.6f, -2.8f),   // Mid Left
            new(2.6f, -2.8f)     // Mid Right
        };
        for (int i = 0; i < bumperPositions.Length; i++)
        {
            var bNode = new Node3D { Name = $"Bumper_{i}" };
            _tableRoot.AddChild(bNode);
            bNode.Position = new Vector3(bumperPositions[i].X, 0.4f, bumperPositions[i].Y);

            var bMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.0f, 0.95f, 1.0f),
                EmissionEnabled = true,
                Emission = new Color(0.0f, 0.95f, 1.0f) * 1.6f
            };
            var bMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.25f, Height = 0.8f },
                MaterialOverride = bMat
            };
            bNode.AddChild(bMesh);

            _bumpers.Add(new PopBumper(bNode, bMesh, bMat, bumperPositions[i], 1.15f));
        }

        // 3. Bank of 4 Reset Drop Targets
        float[] targetX = { -3.3f, -1.1f, 1.1f, 3.3f };
        var targetMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.2f, 0.75f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.2f, 0.75f) * 1.4f
        };
        for (int i = 0; i < targetX.Length; i++)
        {
            var tNode = new Node3D { Name = $"DropTarget_{i}" };
            _tableRoot.AddChild(tNode);
            tNode.Position = new Vector3(targetX[i], 0.35f, -0.6f);

            var tMesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.7f, 0.25f) },
                MaterialOverride = targetMat
            };
            tNode.AddChild(tMesh);

            _dropTargets.Add(new DropTarget(tNode, tMesh, new Vector2(targetX[i], -0.6f), new Vector2(1.4f, 0.25f)));
        }

        // 4. Rollover Multiplier Lanes
        float[] laneX = { -3.2f, 0f, 3.2f };
        for (int i = 0; i < laneX.Length; i++)
        {
            var lNode = new Node3D { Name = $"Rollover_{i}" };
            _tableRoot.AddChild(lNode);
            lNode.Position = new Vector3(laneX[i], 0.05f, -9.2f);

            var lMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.3f, 0.35f, 0.45f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.2f, 0.3f)
            };
            var lMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.65f, BottomRadius = 0.65f, Height = 0.1f },
                MaterialOverride = lMat
            };
            lNode.AddChild(lMesh);

            _rolloverLanes.Add(new RolloverLane(lNode, lMesh, lMat, new Vector2(laneX[i], -9.2f), 0.65f));
        }

        // 5. Pre-allocate 4 Chrome Pinballs
        var chromeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.92f, 0.98f),
            Metallic = 0.98f,
            Roughness = 0.05f
        };
        for (int i = 0; i < MaxBalls; i++)
        {
            var pNode = new Node3D { Name = $"Pinball_{i}", Visible = false };
            _tableRoot.AddChild(pNode);

            var pMesh = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = BallRadius, Height = BallRadius * 2.0f, RadialSegments = 20, Rings = 14 },
                MaterialOverride = chromeMat,
                Position = new Vector3(0, BallRadius, 0)
            };
            pNode.AddChild(pMesh);

            _balls[i] = new Pinball(pNode, pMesh);
        }

        // 6. Overhead Isometric Pinball Camera
        _camera = new Camera3D
        {
            Name = "PinballCamera",
            Position = new Vector3(0, 24.0f, 16.0f),
            RotationDegrees = new Vector3(-54.0f, 0, 0),
            Fov = 55.0f,
            Current = true
        };
        AddChild(_camera);

        // Neon table directional light
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-60.0f, 25.0f, 0),
            LightColor = new Color(0.95f, 0.96f, 1.0f),
            LightEnergy = 1.35f
        };
        AddChild(light);
    }

    private void BuildTableWalls()
    {
        var wallMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.74f, 0.97f),
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.74f, 0.97f) * 0.7f
        };

        // Left boundary
        var leftWall = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.4f, 1.2f, TableHalfLength * 2.0f) },
            Position = new Vector3(-TableHalfWidth, 0.6f, 0),
            MaterialOverride = wallMat
        };
        _tableRoot?.AddChild(leftWall);

        // Right boundary
        var rightWall = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.4f, 1.2f, TableHalfLength * 2.0f) },
            Position = new Vector3(TableHalfWidth, 0.6f, 0),
            MaterialOverride = wallMat
        };
        _tableRoot?.AddChild(rightWall);

        // Top arch wall
        var topWall = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(TableHalfWidth * 2.0f, 1.2f, 0.4f) },
            Position = new Vector3(0, 0.6f, -TableHalfLength),
            MaterialOverride = wallMat
        };
        _tableRoot?.AddChild(topWall);

        // Bottom Left Angled Guide
        var bLeft = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(4.2f, 1.2f, 0.4f) },
            Position = new Vector3(-4.0f, 0.6f, 10.0f),
            RotationDegrees = new Vector3(0, 28.0f, 0),
            MaterialOverride = wallMat
        };
        _tableRoot?.AddChild(bLeft);

        // Bottom Right Angled Guide
        var bRight = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(4.2f, 1.2f, 0.4f) },
            Position = new Vector3(4.0f, 0.6f, 10.0f),
            RotationDegrees = new Vector3(0, -28.0f, 0),
            MaterialOverride = wallMat
        };
        _tableRoot?.AddChild(bRight);
    }

    public void SetTableIncline(float deg)
    {
        BaseTableInclineDeg = Math.Clamp(deg, 5.0f, 25.0f);
        if (_tableRoot != null)
        {
            _tableRoot.RotationDegrees = new Vector3(-BaseTableInclineDeg, 0, 0);
        }
    }

    public void SetBumperRestitution(float rest)
    {
        WallRestitution = Math.Clamp(rest, 0.5f, 1.0f);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _reserveBalls = 3;
        _multiplier = 1.0f;
        _bumpersHitTotal = 0;
        _dropTargetResetTimer = 0f;
        _latestRankAchieved = -1;
        _isTilted = false;
        _tiltFreezeTimer = 0f;
        _screenshakeIntensity = 0f;
        _nudgeCooldownTimer = 0.8f;
        _lastBoardAngle = Vector2.Zero;
        _lastAngularVel = Vector2.Zero;
        _tiltWarningTimestamps.Clear();

        // Deactivate all balls
        for (int i = 0; i < MaxBalls; i++)
        {
            _balls[i].IsActive = false;
            _balls[i].Node.Visible = false;
            _balls[i].Vel = Vector2.Zero;
        }

        // Reset Drop Targets
        foreach (var t in _dropTargets)
        {
            t.IsDown = false;
            t.Node.Position = new Vector3(t.Pos.X, 0.35f, t.Pos.Y);
        }

        // Reset Rollover Lanes
        foreach (var r in _rolloverLanes)
        {
            r.IsLit = false;
            r.Material.Emission = new Color(0.2f, 0.2f, 0.3f);
        }

        // Launch initial ball
        LaunchBall();

        Hud?.ShowBannerMessage("GRAVITY PINBALL • TILT BOARD TO STEER GRAVITY!", 2.5f);
    }

    public void LaunchBall()
    {
        for (int i = 0; i < MaxBalls; i++)
        {
            if (!_balls[i].IsActive)
            {
                var b = _balls[i];
                b.IsActive = true;
                b.Pos = new Vector2(TableHalfWidth - 0.9f, TableHalfLength - 1.5f); // Plunger lane
                b.Vel = new Vector2(-1.5f, -22.0f); // Launch upward
                b.Node.Position = new Vector3(b.Pos.X, BallRadius, b.Pos.Y);
                b.Node.Visible = true;
                return;
            }
        }
    }

    public void NudgeTable()
    {
        if (_isTilted || _nudgeCooldownTimer > 0f) return;

        _nudgeCooldownTimer = 0.8f; // Cooldown so a single physical slap doesn't register multiple strikes

        float now = (float)Time.GetTicksMsec() / 1000.0f;
        _tiltWarningTimestamps.Add(now);

        // Cull timestamps older than 5s
        for (int i = _tiltWarningTimestamps.Count - 1; i >= 0; i--)
        {
            if (now - _tiltWarningTimestamps[i] > 5.0f)
            {
                _tiltWarningTimestamps.RemoveAt(i);
            }
        }

        if (_tiltWarningTimestamps.Count >= 3)
        {
            // TILT PENALTY TRIGGERED!
            _isTilted = true;
            _tiltFreezeTimer = 3.5f;
            _screenshakeIntensity = 0.7f;
            Hud?.ShowBannerMessage("🚨 TILT! CONTROLS FROZEN! 🚨", 3.0f);
            return;
        }

        // Nudge impulse applied to all active balls
        _screenshakeIntensity = 0.35f;
        Hud?.ShowBannerMessage($"⚠️ SLAP NUDGE! (WARNING {_tiltWarningTimestamps.Count}/3)", 1.2f);

        for (int i = 0; i < MaxBalls; i++)
        {
            if (_balls[i].IsActive)
            {
                _balls[i].Vel += new Vector2((float)(_rng.NextDouble() * 5.0f - 2.5f), -3.5f);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Space)
            {
                NudgeTable();
            }
        }
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.05f || MathF.Abs(boardState.Pitch) > 0.05f))
        {
            StartGame();
            Hud?.ShowBannerMessage("BALL IN PLAY!", 1.2f);
        }

        if (GameState != "RUNNING") return;

        // 1. Jerk Nudge Detection (> 160 deg/s^2 or Button tap)
        UpdateJerkDetection(boardState, dt);

        // 2. Compute 2D Table Gravity Vector
        Vector2 gravityTable;
        if (!_isTilted)
        {
            float rollRad = boardState.Roll * (25.0f * (MathF.PI / 180.0f));
            float pitchRad = boardState.Pitch * (20.0f * (MathF.PI / 180.0f));
            float inclineRad = BaseTableInclineDeg * (MathF.PI / 180.0f);

            float gx = GravityMagnitude * MathF.Sin(rollRad);
            float gz = GravityMagnitude * MathF.Sin(pitchRad + inclineRad);
            gravityTable = new Vector2(gx, gz);
        }
        else
        {
            // Tilted penalty: standard incline only, no player steering
            float inclineRad = BaseTableInclineDeg * (MathF.PI / 180.0f);
            gravityTable = new Vector2(0f, GravityMagnitude * MathF.Sin(inclineRad));
        }

        // 3. Update Active Balls Physics & Trajectories
        for (int i = 0; i < MaxBalls; i++)
        {
            var ball = _balls[i];
            if (!ball.IsActive) continue;

            // Acceleration & velocity integration
            ball.Vel += gravityTable * dt;
            ball.Vel *= Math.Max(0f, 1.0f - 0.35f * dt); // table rolling friction

            // Speed cap
            float speed = ball.Vel.Length();
            if (speed > 30.0f) ball.Vel = ball.Vel.Normalized() * 30.0f;

            ball.Pos += ball.Vel * dt;

            // Collisions
            ResolveBallWallCollisions(ball);
            ResolveBallBumperCollisions(ball);
            ResolveBallTargetCollisions(ball);
            CheckRolloverLanes(ball);

            // Bottom Drain Check
            if (ball.Pos.Y > TableHalfLength)
            {
                if (MathF.Abs(ball.Pos.X) < DrainHalfWidth)
                {
                    // Drained!
                    ball.IsActive = false;
                    ball.Node.Visible = false;
                    OnBallDrained();
                    continue;
                }
            }

            ball.Node.Position = new Vector3(ball.Pos.X, BallRadius, ball.Pos.Y);
        }

        // 4. Ball-to-Ball Collisions
        ResolveBallToBallCollisions();

        // 5. Update Bumper Flashes & Drop Targets Resets
        UpdateTableFeatures(dt);

        // 6. Camera Shake Update
        if (_camera != null && _screenshakeIntensity > 0f)
        {
            _screenshakeIntensity = Mathf.MoveToward(_screenshakeIntensity, 0f, 2.0f * dt);
            Vector3 shake = new Vector3(
                ((float)_rng.NextDouble() * 2f - 1f) * _screenshakeIntensity,
                0,
                ((float)_rng.NextDouble() * 2f - 1f) * _screenshakeIntensity
            );
            _camera.Position = new Vector3(0, 24.0f, 16.0f) + shake;
        }
    }

    private void UpdateJerkDetection(BoardState boardState, float dt)
    {
        if (_nudgeCooldownTimer > 0f)
        {
            _nudgeCooldownTimer -= dt;
        }

        if (_isTilted)
        {
            _tiltFreezeTimer -= dt;
            if (_tiltFreezeTimer <= 0f)
            {
                _isTilted = false;
                _tiltWarningTimestamps.Clear();
                _nudgeCooldownTimer = 1.5f; // Grace period after recovery
                Vector2 resetAngle = new(boardState.FusedRollDeg, boardState.FusedPitchDeg);
                _lastBoardAngle = resetAngle;
                _lastAngularVel = Vector2.Zero;
                Hud?.ShowBannerMessage("CONTROLS RESTORED!", 1.5f);
            }
            return;
        }

        // Physical jerk detection is strictly for AtomS3 hardware sensor slap-nudges.
        // Keyboard, gamepad, and touch use Space key or the companion Nudge button.
        if (boardState.Source == "atoms3")
        {
            Vector2 currentAngle = new(boardState.FusedRollDeg, boardState.FusedPitchDeg);
            Vector2 angularVel = (currentAngle - _lastBoardAngle) / Math.Max(0.0001f, dt);
            Vector2 angularAccel = (angularVel - _lastAngularVel) / Math.Max(0.0001f, dt);
            float jerk = angularAccel.Length();

            _lastBoardAngle = currentAngle;
            _lastAngularVel = angularVel;

            // Only trigger a physical slap-nudge on genuine hard stomps (> 2200 deg/s^2)
            if (jerk > 2200.0f && _nudgeCooldownTimer <= 0f)
            {
                NudgeTable();
            }
        }
        else
        {
            // For keyboard/controller, only explicit button press triggers nudge
            if (boardState.IsButtonPressed && _nudgeCooldownTimer <= 0f)
            {
                NudgeTable();
            }
        }
    }

    private void ResolveBallWallCollisions(Pinball b)
    {
        float minX = -TableHalfWidth + BallRadius;
        float maxX = TableHalfWidth - BallRadius;
        float minZ = -TableHalfLength + BallRadius;

        // Left / Right Walls
        if (b.Pos.X < minX)
        {
            b.Pos.X = minX;
            b.Vel.X = -b.Vel.X * WallRestitution;
        }
        else if (b.Pos.X > maxX)
        {
            b.Pos.X = maxX;
            b.Vel.X = -b.Vel.X * WallRestitution;
        }

        // Top Arch Wall
        if (b.Pos.Y < minZ)
        {
            b.Pos.Y = minZ;
            b.Vel.Y = -b.Vel.Y * WallRestitution;
        }

        // Bottom Angled Gutters (deflect away from drain aperture)
        if (b.Pos.Y > 8.0f && b.Pos.Y < TableHalfLength)
        {
            if (b.Pos.X < -DrainHalfWidth && b.Pos.Y > 10.0f)
            {
                b.Pos.Y = 10.0f;
                b.Vel.Y = -MathF.Abs(b.Vel.Y) * WallRestitution;
            }
            else if (b.Pos.X > DrainHalfWidth && b.Pos.Y > 10.0f)
            {
                b.Pos.Y = 10.0f;
                b.Vel.Y = -MathF.Abs(b.Vel.Y) * WallRestitution;
            }
        }
    }

    private void ResolveBallBumperCollisions(Pinball b)
    {
        for (int i = 0; i < _bumpers.Count; i++)
        {
            var bumper = _bumpers[i];
            float dist = b.Pos.DistanceTo(bumper.Pos);
            float totalR = bumper.Radius + BallRadius;

            if (dist < totalR && dist > 0.001f)
            {
                Vector2 normal = (b.Pos - bumper.Pos) / dist;
                b.Pos = bumper.Pos + normal * (totalR + 0.05f);

                // Explosive outward pop impulse (18 m/s)
                b.Vel = normal * Math.Max(18.0f, b.Vel.Length() * 1.25f);

                // Flash Bumper
                bumper.FlashTimer = 0.18f;
                bumper.Material.Emission = new Color(1.0f, 1.0f, 1.0f) * 3.5f;

                _bumpersHitTotal++;
                int pts = (int)(250 * _multiplier);
                AddScore(pts);

                _screenshakeIntensity = Math.Max(_screenshakeIntensity, 0.25f);
            }
        }
    }

    private void ResolveBallTargetCollisions(Pinball b)
    {
        for (int i = 0; i < _dropTargets.Count; i++)
        {
            var target = _dropTargets[i];
            if (target.IsDown) continue;

            float halfW = target.Size.X * 0.5f;
            float halfH = target.Size.Y * 0.5f;

            if (MathF.Abs(b.Pos.X - target.Pos.X) < halfW + BallRadius &&
                MathF.Abs(b.Pos.Y - target.Pos.Y) < halfH + BallRadius)
            {
                target.IsDown = true;
                target.Node.Position = new Vector3(target.Pos.X, -0.4f, target.Pos.Y); // drop below table
                b.Vel.Y = -b.Vel.Y * WallRestitution;

                int pts = (int)(500 * _multiplier);
                AddScore(pts);

                CheckDropTargetBank();
            }
        }
    }

    private void CheckDropTargetBank()
    {
        int downCount = 0;
        foreach (var t in _dropTargets)
        {
            if (t.IsDown) downCount++;
        }

        if (downCount == _dropTargets.Count)
        {
            // All 4 down: Jackpot & Multiball!
            _multiplier = Math.Min(5.0f, _multiplier + 1.0f);
            int jackpot = (int)(2000 * _multiplier);
            AddScore(jackpot);

            // Multiball release
            LaunchBall();
            LaunchBall();

            _dropTargetResetTimer = 3.5f;
            Hud?.ShowBannerMessage($"💎 DROP TARGETS DOWN! MULTIBALL JACKPOT! (+{jackpot:N0}) 💎", 2.5f);
        }
    }

    private void CheckRolloverLanes(Pinball b)
    {
        int litCount = 0;
        foreach (var lane in _rolloverLanes)
        {
            if (b.Pos.DistanceTo(lane.Pos) < lane.Radius)
            {
                if (!lane.IsLit)
                {
                    lane.IsLit = true;
                    lane.Material.Emission = new Color(0.2f, 0.95f, 0.45f) * 2.0f;
                    AddScore((int)(150 * _multiplier));
                }
            }
            if (lane.IsLit) litCount++;
        }

        if (litCount == _rolloverLanes.Count)
        {
            _multiplier = Math.Min(5.0f, _multiplier + 1.0f);
            AddScore(1000);
            Hud?.ShowBannerMessage($"🌟 LANES LIT! MULTIPLIER ADVANCED! (x{_multiplier:F1}) 🌟", 2.0f);

            // Reset lanes
            foreach (var lane in _rolloverLanes)
            {
                lane.IsLit = false;
                lane.Material.Emission = new Color(0.2f, 0.2f, 0.3f);
            }
        }
    }

    private void ResolveBallToBallCollisions()
    {
        for (int i = 0; i < MaxBalls; i++)
        {
            var b1 = _balls[i];
            if (!b1.IsActive) continue;

            for (int j = i + 1; j < MaxBalls; j++)
            {
                var b2 = _balls[j];
                if (!b2.IsActive) continue;

                float dist = b1.Pos.DistanceTo(b2.Pos);
                float minR = BallRadius * 2.0f;

                if (dist < minR && dist > 0.001f)
                {
                    Vector2 normal = (b2.Pos - b1.Pos) / dist;
                    float pen = minR - dist;

                    b1.Pos -= normal * (pen * 0.5f);
                    b2.Pos += normal * (pen * 0.5f);

                    Vector2 relVel = b2.Vel - b1.Vel;
                    float normRelVel = relVel.Dot(normal);

                    if (normRelVel < 0f)
                    {
                        float impulse = -((1.0f + BallRestitution) * normRelVel) * 0.5f;
                        b1.Vel -= normal * impulse;
                        b2.Vel += normal * impulse;
                    }
                }
            }
        }
    }

    private void UpdateTableFeatures(float dt)
    {
        // Bumpers flash decay
        foreach (var bumper in _bumpers)
        {
            if (bumper.FlashTimer > 0f)
            {
                bumper.FlashTimer -= dt;
                if (bumper.FlashTimer <= 0f)
                {
                    bumper.Material.Emission = new Color(0.0f, 0.95f, 1.0f) * 1.6f;
                }
            }
        }

        // Drop target bank reset timer
        if (_dropTargetResetTimer > 0f)
        {
            _dropTargetResetTimer -= dt;
            if (_dropTargetResetTimer <= 0f)
            {
                foreach (var t in _dropTargets)
                {
                    t.IsDown = false;
                    t.Node.Position = new Vector3(t.Pos.X, 0.35f, t.Pos.Y);
                }
            }
        }
    }

    private void OnBallDrained()
    {
        int activeCount = 0;
        for (int i = 0; i < MaxBalls; i++)
        {
            if (_balls[i].IsActive) activeCount++;
        }

        if (activeCount == 0)
        {
            _reserveBalls--;

            if (_reserveBalls > 0)
            {
                Hud?.ShowBannerMessage($"BALL DRAINED! {_reserveBalls} BALLS REMAINING", 2.0f);
                LaunchBall();
            }
            else
            {
                TriggerGameOver("Game Over! All Reserve Balls Drained!");
                string player = LeaderboardService.Instance.ActivePlayerName;
                _latestRankAchieved = LeaderboardService.Instance.RecordScore(player, CurrentScore, _bumpersHitTotal, (int)_multiplier);
                Hud?.ShowBannerMessage($"💀 GAME OVER! FINAL SCORE: {CurrentScore:N0}", 3.5f);
            }
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        int activeCount = 0;
        _radarBallsCache.Clear();
        for (int i = 0; i < MaxBalls; i++)
        {
            if (_balls[i].IsActive)
            {
                activeCount++;
                float normX = Math.Clamp(_balls[i].Pos.X / TableHalfWidth, -1.0f, 1.0f);
                float normY = Math.Clamp(_balls[i].Pos.Y / TableHalfLength, -1.0f, 1.0f);
                _radarBallsCache.Add(new Dictionary<string, object>
                {
                    { "x", normX },
                    { "y", normY },
                    { "vx", _balls[i].Vel.X / TableHalfWidth },
                    { "vy", _balls[i].Vel.Y / TableHalfLength }
                });
            }
        }

        int targetsDown = 0;
        _radarTargetsCache.Clear();
        for (int i = 0; i < _dropTargets.Count; i++)
        {
            if (_dropTargets[i].IsDown) targetsDown++;
            _radarTargetsCache.Add(new Dictionary<string, object>
            {
                { "id", i + 1 },
                { "active", !_dropTargets[i].IsDown }
            });
        }

        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["activeBalls"] = activeCount;
        _liveStatsCache["reserveBalls"] = Math.Max(0, _reserveBalls);
        _liveStatsCache["multiplier"] = _multiplier;
        _liveStatsCache["bumpersHit"] = _bumpersHitTotal;
        _liveStatsCache["dropTargetsDown"] = targetsDown;
        _liveStatsCache["tiltWarnings"] = _tiltWarningTimestamps.Count;
        _liveStatsCache["isTilted"] = _isTilted;
        _liveStatsCache["isTiltLocked"] = _isTilted;
        _liveStatsCache["tableIncline"] = BaseTableInclineDeg;
        _liveStatsCache["restitution"] = WallRestitution;
        _liveStatsCache["balls"] = _radarBallsCache;
        _liveStatsCache["targets"] = _radarTargetsCache;
        _liveStatsCache["latestRank"] = _latestRankAchieved;

        return _liveStatsCache;
    }
}
