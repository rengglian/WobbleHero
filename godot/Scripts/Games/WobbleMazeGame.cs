using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Wobble Maze.
/// Tests 2D balance board precision tilt control. The player balances and rolls a marble
/// through a deterministic procedural maze with pits, gems, locked gates, and exit portals.
/// </summary>
public partial class WobbleMazeGame : BaseGameMode
{
    public override string GameId => "WobbleMaze";
    public override string GameTitle => "Wobble Maze";
    public override string GameDescription => "Tilt to navigate the marble through a hazardous maze with traps, keys, and gems!";
    public override string GameIcon => "🌀";

    [Export] public int Seed { get; set; } = 1337;
    [Export] public string Size { get; set; } = "medium";
    [Export] public string Difficulty { get; set; } = "medium";
    [Export] public float CellSize { get; set; } = 2.4f;
    [Export] public float BallRadius { get; set; } = 0.28f;
    [Export] public float Gravity { get; set; } = 15.0f;
    [Export] public float Damping { get; set; } = 0.965f;
    [Export] public float Restitution { get; set; } = 0.32f;

    public string CurrentSize => Size;
    public string CurrentDifficulty => Difficulty;

    // Node hierarchy
    private Node3D? _boardNode;
    private MeshInstance3D? _ballMesh;
    private Node3D? _gateNode;
    private Node3D? _keyNode;
    private Node3D? _portalNode;
    private Node3D? _checkpointNode;
    private Camera3D? _camera;
    private readonly List<MeshInstance3D> _gemNodes = new();
    private readonly List<Node3D> _hazardBarNodes = new();

    // Maze Data
    private MazeData? _mazeData;
    private readonly List<Rect2> _wallColliders = new();

    // Gameplay runtime state
    private Vector2 _ballPos;
    private Vector2 _ballVel;
    private Vector2 _checkpointPos;
    private bool _checkpointActive;
    private bool _hasKey;
    private bool _gateUnlocked;
    private int _gemsCollected;
    private int _totalGems;
    private int _falls;
    private float _timeElapsed;
    private float _fallAnimationTimer;
    private bool _isFalling;
    private float _spinAngleX;
    private float _spinAngleZ;

    public override void _Ready()
    {
        BuildScene();
        ResetGame();
    }

    public void SetMazeConfig(int seed, string size, string difficulty)
    {
        Seed = seed;
        Size = size;
        Difficulty = difficulty;
        BuildScene();
        ResetGame();
    }

    private void BuildScene()
    {
        // 1. Clear existing dynamic nodes
        if (_boardNode != null)
        {
            RemoveChild(_boardNode);
            _boardNode.QueueFree();
            _boardNode = null;
        }

        _gemNodes.Clear();
        _hazardBarNodes.Clear();
        _wallColliders.Clear();

        // 2. Generate procedural maze data
        _mazeData = MazeGenerator.Generate(Seed, Size, Difficulty);
        _totalGems = _mazeData.GemCells.Count;

        // 3. Root Tilting Board Node
        _boardNode = new Node3D { Name = "MazeBoard" };
        AddChild(_boardNode);

        float mazeWidth = _mazeData.Cols * CellSize;
        float mazeHeight = _mazeData.Rows * CellSize;
        float halfW = mazeWidth * 0.5f;
        float halfH = mazeHeight * 0.5f;

        // Base Floor Slab
        var floor = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(mazeWidth + 0.6f, 0.4f, mazeHeight + 0.6f) },
            Position = new Vector3(0, -0.2f, 0)
        };
        var floorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.10f, 0.13f, 0.20f),
            Roughness = 0.35f,
            Metallic = 0.25f
        };
        floor.MaterialOverride = floorMat;
        _boardNode.AddChild(floor);

        // Outer Perimeter Walls
        float wallThickness = 0.28f;
        float wallHeight = 0.85f;
        var wallMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.24f, 0.35f),
            Roughness = 0.2f,
            Metallic = 0.5f
        };

        // North perimeter
        AddWallSegment(new Vector2(0, -halfH - wallThickness * 0.5f), new Vector2(mazeWidth + wallThickness * 2, wallThickness), wallHeight, wallMat);
        // South perimeter
        AddWallSegment(new Vector2(0, halfH + wallThickness * 0.5f), new Vector2(mazeWidth + wallThickness * 2, wallThickness), wallHeight, wallMat);
        // West perimeter
        AddWallSegment(new Vector2(-halfW - wallThickness * 0.5f, 0), new Vector2(wallThickness, mazeHeight), wallHeight, wallMat);
        // East perimeter
        AddWallSegment(new Vector2(halfW + wallThickness * 0.5f, 0), new Vector2(wallThickness, mazeHeight), wallHeight, wallMat);

        // Internal Cell Walls
        for (int x = 0; x < _mazeData.Cols; x++)
        {
            for (int y = 0; y < _mazeData.Rows; y++)
            {
                var cell = _mazeData.Cells[x, y];
                Vector2 cellCenter = CellToWorld2D(new Vector2I(x, y));

                // East wall
                if ((cell.Walls & WallFlags.East) != 0)
                {
                    Vector2 wallPos = cellCenter + new Vector2(CellSize * 0.5f, 0);
                    AddWallSegment(wallPos, new Vector2(wallThickness, CellSize), wallHeight, wallMat);
                }

                // South wall
                if ((cell.Walls & WallFlags.South) != 0)
                {
                    Vector2 wallPos = cellCenter + new Vector2(0, CellSize * 0.5f);
                    AddWallSegment(wallPos, new Vector2(CellSize, wallThickness), wallHeight, wallMat);
                }
            }
        }

        // 4. Trap Holes
        var holeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.04f, 0.04f, 0.06f),
            Roughness = 0.9f
        };
        var rimMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.2f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.15f, 0.15f) * 1.2f
        };

        foreach (var trap in _mazeData.TrapCells)
        {
            Vector2 pos2 = CellToWorld2D(trap);

            // Sunken dark cylinder
            var pit = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.38f, Height = 0.45f },
                Position = new Vector3(pos2.X, -0.1f, pos2.Y)
            };
            pit.MaterialOverride = holeMat;
            _boardNode.AddChild(pit);

            // Glowing red hazard ring
            var rim = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.38f, OuterRadius = 0.45f },
                Position = new Vector3(pos2.X, 0.02f, pos2.Y)
            };
            rim.MaterialOverride = rimMat;
            _boardNode.AddChild(rim);
        }

        // 5. Golden Key
        Vector2 keyPos2 = CellToWorld2D(_mazeData.KeyCell);
        _keyNode = new Node3D { Name = "GoldenKey", Position = new Vector3(keyPos2.X, 0.35f, keyPos2.Y) };
        var keyMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.14f, OuterRadius = 0.28f }
        };
        var keyMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.82f, 0.15f) * 0.9f,
            Metallic = 0.8f,
            Roughness = 0.15f
        };
        keyMesh.MaterialOverride = keyMat;
        _keyNode.AddChild(keyMesh);
        _boardNode.AddChild(_keyNode);

        // 6. Locked Barrier Gate
        Vector2 gatePosA = CellToWorld2D(_mazeData.GateCellA);
        Vector2 gatePosB = CellToWorld2D(_mazeData.GateCellB);
        Vector2 gateCenter2 = (gatePosA + gatePosB) * 0.5f;
        bool isGateHorizontal = MathF.Abs(gatePosA.Y - gatePosB.Y) > MathF.Abs(gatePosA.X - gatePosB.X);
        Vector2 gateSize2 = isGateHorizontal ? new Vector2(CellSize * 0.95f, wallThickness * 1.5f) : new Vector2(wallThickness * 1.5f, CellSize * 0.95f);

        _gateNode = new Node3D { Name = "LockedGate", Position = new Vector3(gateCenter2.X, 0.42f, gateCenter2.Y) };
        var gateMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(gateSize2.X, wallHeight * 0.95f, gateSize2.Y) }
        };
        var gateMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.4f, 0.05f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.4f, 0.05f) * 1.5f
        };
        gateMesh.MaterialOverride = gateMat;
        _gateNode.AddChild(gateMesh);
        _boardNode.AddChild(_gateNode);

        // 7. Bonus Gems
        var gemMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.95f, 0.45f),
            EmissionEnabled = true,
            Emission = new Color(0.12f, 0.95f, 0.45f) * 1.1f,
            Metallic = 0.4f,
            Roughness = 0.1f
        };
        foreach (var gemCell in _mazeData.GemCells)
        {
            Vector2 gemPos2 = CellToWorld2D(gemCell);
            var gemMesh = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(0.38f, 0.48f, 0.38f) },
                Position = new Vector3(gemPos2.X, 0.32f, gemPos2.Y)
            };
            gemMesh.MaterialOverride = gemMat;
            _boardNode.AddChild(gemMesh);
            _gemNodes.Add(gemMesh);
        }

        // 8. Checkpoint Pad
        Vector2 cpPos2 = CellToWorld2D(_mazeData.CheckpointCell);
        _checkpointNode = new Node3D { Name = "Checkpoint", Position = new Vector3(cpPos2.X, 0.02f, cpPos2.Y) };
        var cpPad = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.04f }
        };
        var cpMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.74f, 0.97f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.74f, 0.97f) * 0.5f
        };
        cpPad.MaterialOverride = cpMat;
        _checkpointNode.AddChild(cpPad);
        _boardNode.AddChild(_checkpointNode);

        // 9. Exit Goal Portal
        Vector2 exitPos2 = CellToWorld2D(_mazeData.ExitCell);
        _portalNode = new Node3D { Name = "ExitPortal", Position = new Vector3(exitPos2.X, 0.05f, exitPos2.Y) };
        var portalRing = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.50f, OuterRadius = 0.75f }
        };
        var portalMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.2f, 0.95f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.2f, 0.95f) * 1.5f
        };
        portalRing.MaterialOverride = portalMat;
        _portalNode.AddChild(portalRing);

        var portalBeam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.35f, Height = 2.4f },
            Position = new Vector3(0, 1.2f, 0)
        };
        var beamMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.85f, 1.0f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.85f, 1.0f) * 1.8f
        };
        portalBeam.MaterialOverride = beamMat;
        _portalNode.AddChild(portalBeam);
        _boardNode.AddChild(_portalNode);

        // 10. The Rolling Marble
        _ballMesh = new MeshInstance3D
        {
            Name = "Ball",
            Mesh = new SphereMesh { Radius = BallRadius, Height = BallRadius * 2.0f }
        };
        var ballMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.95f, 1.0f),
            Metallic = 0.95f,
            Roughness = 0.10f,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.74f, 0.97f) * 0.35f
        };
        _ballMesh.MaterialOverride = ballMat;
        _boardNode.AddChild(_ballMesh);

        // 11. Camera & Lighting (Mounted to game root, following marble)
        if (_camera == null)
        {
            _camera = new Camera3D
            {
                Position = new Vector3(0, 12.0f, 9.5f),
                RotationDegrees = new Vector3(-52, 0, 0)
            };
            AddChild(_camera);

            var light = new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-55, 30, 0),
                LightEnergy = 1.3f,
                ShadowEnabled = true
            };
            AddChild(light);
        }
    }

    private void AddWallSegment(Vector2 center, Vector2 size, float height, StandardMaterial3D mat)
    {
        var wallMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(size.X, height, size.Y) },
            Position = new Vector3(center.X, height * 0.5f, center.Y)
        };
        wallMesh.MaterialOverride = mat;
        _boardNode?.AddChild(wallMesh);

        // Register AABB collider
        _wallColliders.Add(new Rect2(center - size * 0.5f, size));
    }

    public Vector2 CellToWorld2D(Vector2I cell)
    {
        if (_mazeData == null) return Vector2.Zero;
        float startX = -_mazeData.Cols * CellSize * 0.5f;
        float startZ = -_mazeData.Rows * CellSize * 0.5f;
        return new Vector2(
            startX + (cell.X + 0.5f) * CellSize,
            startZ + (cell.Y + 0.5f) * CellSize
        );
    }

    public override void ResetGame()
    {
        base.ResetGame();
        if (_mazeData == null) return;

        _ballPos = CellToWorld2D(_mazeData.StartCell);
        _ballVel = Vector2.Zero;
        _checkpointPos = _ballPos;
        _checkpointActive = false;
        _hasKey = false;
        _gateUnlocked = false;
        _gemsCollected = 0;
        _falls = 0;
        _timeElapsed = 0f;
        _isFalling = false;
        _fallAnimationTimer = 0f;
        _spinAngleX = 0f;
        _spinAngleZ = 0f;

        if (_keyNode != null) _keyNode.Visible = true;
        if (_gateNode != null) _gateNode.Visible = true;
        foreach (var gem in _gemNodes) gem.Visible = true;

        UpdateVisuals();
        Hud?.ShowBannerMessage("TILT TO NAVIGATE", 2.0f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Keyboard tilt fallback (WASD / Arrows)
        float kbRoll = 0.0f;
        float kbPitch = 0.0f;
        if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) kbRoll -= 1.0f;
        if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) kbRoll += 1.0f;
        if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) kbPitch -= 1.0f;
        if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) kbPitch += 1.0f;

        float effectiveRoll = MathF.Abs(boardState.Roll) > 0.05f ? boardState.Roll : kbRoll;
        float effectivePitch = MathF.Abs(boardState.Pitch) > 0.05f ? boardState.Pitch : kbPitch;

        // Auto-start on movement
        if (GameState == "START" && (MathF.Abs(effectiveRoll) > 0.08f || MathF.Abs(effectivePitch) > 0.08f))
        {
            StartGame();
        }

        // Falling / Respawn sequence
        if (_isFalling)
        {
            _fallAnimationTimer -= dt;
            if (_fallAnimationTimer <= 0f)
            {
                _isFalling = false;
                _ballPos = _checkpointActive ? _checkpointPos : CellToWorld2D(_mazeData!.StartCell);
                _ballVel = Vector2.Zero;
            }
            UpdateVisuals();
            return;
        }

        if (GameState != "RUNNING" || _mazeData == null) return;

        _timeElapsed += dt;

        // 1. Tilt Board in 3D
        float maxTiltRad = Mathf.DegToRad(12.0f);
        float tiltX = effectivePitch * maxTiltRad;
        float tiltZ = -effectiveRoll * maxTiltRad;
        if (_boardNode != null)
        {
            _boardNode.Rotation = new Vector3(tiltX, 0, tiltZ);
        }

        // 2. Physics: Tilt acceleration + friction
        Vector2 tiltAccel = new(effectiveRoll * Gravity, effectivePitch * Gravity);
        _ballVel += tiltAccel * dt;
        _ballVel *= MathF.Pow(Damping, dt * 60f);

        // Velocity clamp
        float maxSpeed = 16.0f;
        if (_ballVel.Length() > maxSpeed)
        {
            _ballVel = _ballVel.Normalized() * maxSpeed;
        }

        _ballPos += _ballVel * dt;

        // Rolling mesh spin
        _spinAngleX += (_ballVel.Y / BallRadius) * dt;
        _spinAngleZ -= (_ballVel.X / BallRadius) * dt;

        // 3. Wall Collisions (Circle vs AABB)
        for (int i = 0; i < _wallColliders.Count; i++)
        {
            ResolveCircleAabbCollision(_wallColliders[i]);
        }

        // Gate collision if locked
        if (!_gateUnlocked && _gateNode != null)
        {
            Vector2 gatePosA = CellToWorld2D(_mazeData.GateCellA);
            Vector2 gatePosB = CellToWorld2D(_mazeData.GateCellB);
            Vector2 gateCenter = (gatePosA + gatePosB) * 0.5f;
            bool isHorizontal = MathF.Abs(gatePosA.Y - gatePosB.Y) > MathF.Abs(gatePosA.X - gatePosB.X);
            Vector2 gateSize = isHorizontal ? new Vector2(CellSize, 0.4f) : new Vector2(0.4f, CellSize);
            var gateRect = new Rect2(gateCenter - gateSize * 0.5f, gateSize);
            ResolveCircleAabbCollision(gateRect);
        }

        // 4. Trap Hole Suction & Fall Trigger
        float holeRadius = 0.40f;
        float suctionRadius = 0.70f;
        foreach (var trap in _mazeData.TrapCells)
        {
            Vector2 trapPos = CellToWorld2D(trap);
            float dist = _ballPos.DistanceTo(trapPos);

            if (dist < suctionRadius)
            {
                // Vortex pull towards hole center
                Vector2 dir = (trapPos - _ballPos).Normalized();
                float pull = (1.0f - dist / suctionRadius) * 22.0f;
                _ballVel += dir * (pull * dt);

                if (dist < holeRadius * 0.85f)
                {
                    TriggerTrapFall();
                    break;
                }
            }
        }

        // 5. Checkpoint Trigger
        if (!_checkpointActive)
        {
            Vector2 cpPos = CellToWorld2D(_mazeData.CheckpointCell);
            if (_ballPos.DistanceTo(cpPos) < 0.60f)
            {
                _checkpointActive = true;
                _checkpointPos = cpPos;
                AddScore(150);
                Hud?.ShowBannerMessage("📍 CHECKPOINT REACHED!", 2.0f);
            }
        }

        // 6. Golden Key Trigger
        if (!_hasKey && _keyNode != null && _keyNode.Visible)
        {
            Vector2 keyPos = CellToWorld2D(_mazeData.KeyCell);
            if (_ballPos.DistanceTo(keyPos) < 0.55f)
            {
                _hasKey = true;
                _keyNode.Visible = false;
                AddScore(300);
                Hud?.ShowBannerMessage("🗝️ KEY COLLECTED! UNLOCK THE GATE", 2.5f);
            }
        }

        // 7. Gate Unlock Trigger (if near gate with key)
        if (_hasKey && !_gateUnlocked && _gateNode != null)
        {
            Vector2 gatePosA = CellToWorld2D(_mazeData.GateCellA);
            Vector2 gatePosB = CellToWorld2D(_mazeData.GateCellB);
            Vector2 gateCenter = (gatePosA + gatePosB) * 0.5f;

            if (_ballPos.DistanceTo(gateCenter) < 1.10f)
            {
                _gateUnlocked = true;
                _gateNode.Visible = false;
                AddScore(250);
                Hud?.ShowBannerMessage("⚡ GATE UNLOCKED! ESCAPE ROUTE OPEN", 2.0f);
            }
        }

        // 8. Gem Collectibles Trigger
        for (int i = 0; i < _mazeData.GemCells.Count; i++)
        {
            if (i < _gemNodes.Count && _gemNodes[i].Visible)
            {
                Vector2 gemPos = CellToWorld2D(_mazeData.GemCells[i]);
                if (_ballPos.DistanceTo(gemPos) < 0.55f)
                {
                    _gemNodes[i].Visible = false;
                    _gemsCollected++;
                    AddScore(200);
                    Hud?.ShowBannerMessage($"+200 GEM ({_gemsCollected}/{_totalGems})", 1.5f);
                }
            }
        }

        // 9. Exit Portal Victory Trigger
        Vector2 exitPos = CellToWorld2D(_mazeData.ExitCell);
        if (_ballPos.DistanceTo(exitPos) < 0.65f)
        {
            CompleteMaze();
        }

        UpdateVisuals();
    }

    private void ResolveCircleAabbCollision(Rect2 box)
    {
        float closestX = Math.Clamp(_ballPos.X, box.Position.X, box.End.X);
        float closestY = Math.Clamp(_ballPos.Y, box.Position.Y, box.End.Y);
        Vector2 diff = _ballPos - new Vector2(closestX, closestY);
        float distSq = diff.LengthSquared();

        if (distSq < BallRadius * BallRadius)
        {
            float dist = MathF.Sqrt(distSq);
            Vector2 normal = dist > 0.0001f ? diff / dist : Vector2.Right;
            float penetration = BallRadius - dist;

            _ballPos += normal * penetration;

            // Elastic bounce
            float velDot = _ballVel.Dot(normal);
            if (velDot < 0f)
            {
                _ballVel -= normal * (velDot * (1.0f + Restitution));
                _ballVel *= 0.95f; // Friction
            }
        }
    }

    private void TriggerTrapFall()
    {
        _falls++;
        _isFalling = true;
        _fallAnimationTimer = 0.5f;
        int penalty = 50;
        SetScore(Math.Max(0, CurrentScore - penalty));
        Hud?.ShowBannerMessage("⚠️ PIT TRAP! RESPAWN AT CHECKPOINT (-50 PTS)", 1.8f);
    }

    private void CompleteMaze()
    {
        int timeBonus = Math.Max(0, (int)((90.0f - _timeElapsed) * 15f));
        int gemsBonus = _gemsCollected * 300;
        AddScore(timeBonus + gemsBonus + 500);
        TriggerGameOver("VICTORY");
        Hud?.ShowBannerMessage("🏆 MAZE COMPLETED!", 3.0f);
    }

    private void UpdateVisuals()
    {
        // Marble position and rotation
        if (_ballMesh != null)
        {
            float posY = _isFalling ? -0.3f : BallRadius;
            _ballMesh.Position = new Vector3(_ballPos.X, posY, _ballPos.Y);
            _ballMesh.Rotation = new Vector3(_spinAngleX, 0, _spinAngleZ);
        }

        // Collectibles idle spin
        float rotDelta = 2.0f * (float)GetProcessDeltaTime();
        if (_keyNode != null && _keyNode.Visible) _keyNode.RotateY(rotDelta * 1.5f);
        foreach (var gem in _gemNodes)
        {
            if (gem.Visible) gem.RotateY(rotDelta);
        }
        if (_portalNode != null) _portalNode.RotateY(rotDelta * 0.8f);

        // Camera follow
        if (_camera != null && _boardNode != null)
        {
            Vector3 marbleWorld = _boardNode.ToGlobal(new Vector3(_ballPos.X, BallRadius, _ballPos.Y));
            Vector3 desiredPos = marbleWorld + new Vector3(0, 11.5f, 9.0f);
            _camera.Position = _camera.Position.Lerp(desiredPos, 0.12f);
            _camera.LookAt(marbleWorld + new Vector3(0, 0, -0.6f), Vector3.Up);
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        var stats = base.GetLiveStats();
        stats["seed"] = Seed;
        stats["size"] = Size;
        stats["difficulty"] = Difficulty;
        stats["gemsCollected"] = _gemsCollected;
        stats["totalGems"] = _totalGems;
        stats["hasKey"] = _hasKey;
        stats["gateUnlocked"] = _gateUnlocked;
        stats["timeElapsed"] = _timeElapsed;
        stats["falls"] = _falls;
        stats["checkpointActive"] = _checkpointActive;
        return stats;
    }
}
