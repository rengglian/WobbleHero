using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Compass Labyrinth (The Rotating Dungeon).
/// Combines 2D board tilting locomotion with physical 360° yaw compass rotation.
/// Physically rotating the balance board shifts the dungeon's gravity quadrant,
/// causing massive monolithic stone blocks to slide along corridors, activate
/// pressure plates, and lower ancient barrier gates to extract the Sun Relics.
/// </summary>
public partial class CompassLabyrinthGame : BaseGameMode
{
    public override string GameId => "CompassLabyrinth";
    public override string GameTitle => "Compass Labyrinth";
    public override string GameDescription => "Rotate your physical board 360° to shift temple gravity! Slide heavy monoliths, activate pressure plates, and retrieve ancient sun relics.";
    public override string GameIcon => "🧭";

    // Labyrinth Grid Constants
    public const int GridWidth = 15;
    public const int GridHeight = 15;
    public const float CellSize = 3.2f;
    public const float WallHeight = 3.6f;
    public const float HalfDungeonSize = (GridWidth * CellSize) * 0.5f;

    public enum CardinalPlane { North, East, South, West }

    // Grid cell types
    private const int CellFloor = 0;
    private const int CellWall = 1;
    private const int CellHazardPit = 2;

    // Puzzle Entities
    private sealed class SlidingMonolith
    {
        public Node3D Node { get; }
        public Vector2 Pos;
        public Vector2 Vel;
        public float Size;
        public bool IsSliding;

        public SlidingMonolith(Node3D node, Vector2 pos, float size)
        {
            Node = node;
            Pos = pos;
            Vel = Vector2.Zero;
            Size = size;
            IsSliding = false;
        }
    }

    private sealed class PressurePlate
    {
        public Node3D Node { get; }
        public Vector2 Pos;
        public int GateId;
        public bool IsPressed;
        public MeshInstance3D PlateMesh;

        public PressurePlate(Node3D node, Vector2 pos, int gateId, MeshInstance3D mesh)
        {
            Node = node;
            Pos = pos;
            GateId = gateId;
            PlateMesh = mesh;
            IsPressed = false;
        }
    }

    private sealed class BarrierGate
    {
        public Node3D Node { get; }
        public int GateId;
        public Vector2 Pos;
        public bool IsOpen;
        public float OpenProgress;

        public BarrierGate(Node3D node, int gateId, Vector2 pos)
        {
            Node = node;
            GateId = gateId;
            Pos = pos;
            IsOpen = false;
            OpenProgress = 0f;
        }
    }

    private sealed class SunRelic
    {
        public Node3D Node { get; }
        public Vector2 Pos;
        public bool Collected;

        public SunRelic(Node3D node, Vector2 pos)
        {
            Node = node;
            Pos = pos;
            Collected = false;
        }
    }

    // Node hierarchy
    private Node3D? _dungeonRoot;
    private Node3D? _marbleNode;
    private MeshInstance3D? _marbleMesh;
    private Camera3D? _camera;
    private Node3D? _altarNode;

    // Explorer marble physics state
    private Vector2 _marblePos = Vector2.Zero;
    private Vector2 _marbleVel = Vector2.Zero;
    private float _marbleY = 0.65f;
    private const float MarbleRadius = 0.65f;
    private const float AccelGain = 26.0f;
    private const float Friction = 2.4f;

    // Compass & Gravity Rotation
    [Export] public float CalibrationOffset { get; set; } = 0f;
    [Export] public int DungeonSeed { get; set; } = 101;

    private float _currentHeadingDeg = 0f;
    private float _visualHeadingDeg = 0f;
    private CardinalPlane _activeCardinalPlane = CardinalPlane.North;
    private Vector2 _currentGravityDir = new(0, -1); // North pulls toward -Z

    // Match state
    private int _relicsCollected = 0;
    private int _platesSolved = 0;
    private bool _altarReached = false;
    private int _lives = 3;
    private float _invulnTimer = 0f;
    private int _latestRankAchieved = -1;

    // Pre-allocated entity lists
    private readonly List<SlidingMonolith> _monoliths = new(6);
    private readonly List<PressurePlate> _plates = new(4);
    private readonly List<BarrierGate> _gates = new(4);
    private readonly List<SunRelic> _relics = new(3);
    private readonly List<Vector2> _wallPositions = new(120);
    private readonly List<Vector2> _pitPositions = new(20);
    private readonly Dictionary<string, object> _liveStatsCache = new(16);
    private readonly List<object> _radarMonolithsCache = new(4);
    private readonly List<object> _radarRelicsCache = new(4);

    // 15x15 Dungeon map definition
    private static readonly int[,] DungeonLayout = new int[15, 15]
    {
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1 },
        { 1, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1 },
        { 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 1 },
        { 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1 },
        { 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1 },
        { 1, 0, 0, 1, 0, 1, 2, 2, 2, 1, 0, 1, 0, 0, 1 },
        { 1, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 1, 1 },
        { 1, 0, 0, 1, 0, 1, 2, 2, 2, 1, 0, 1, 0, 0, 1 },
        { 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1 },
        { 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1 },
        { 1, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 1 },
        { 1, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1 },
        { 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }
    };

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Root Dungeon Container
        _dungeonRoot = new Node3D { Name = "DungeonRoot" };
        AddChild(_dungeonRoot);

        // Floor base plate
        var floorMesh = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(GridWidth * CellSize, GridHeight * CellSize) },
            Position = new Vector3(0, 0, 0)
        };
        var floorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.14f, 0.18f),
            Roughness = 0.7f,
            Metallic = 0.2f
        };
        floorMesh.MaterialOverride = floorMat;
        _dungeonRoot.AddChild(floorMesh);

        // Build Walls & Hazard Pits from layout
        _wallPositions.Clear();
        _pitPositions.Clear();

        var wallMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.24f, 0.26f, 0.32f),
            Roughness = 0.85f,
            Metallic = 0.15f
        };
        var pitMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.15f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.15f, 0.15f) * 0.75f
        };

        for (int r = 0; r < GridHeight; r++)
        {
            for (int c = 0; c < GridWidth; c++)
            {
                int type = DungeonLayout[r, c];
                float wx = (c - GridWidth / 2) * CellSize;
                float wz = (r - GridHeight / 2) * CellSize;
                Vector2 cellCenter = new(wx, wz);

                if (type == CellWall)
                {
                    var wall = new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(CellSize, WallHeight, CellSize) },
                        Position = new Vector3(wx, WallHeight * 0.5f, wz),
                        MaterialOverride = wallMat
                    };
                    _dungeonRoot.AddChild(wall);
                    _wallPositions.Add(cellCenter);
                }
                else if (type == CellHazardPit)
                {
                    var pit = new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.95f, 0.2f, CellSize * 0.95f) },
                        Position = new Vector3(wx, 0.05f, wz),
                        MaterialOverride = pitMat
                    };
                    _dungeonRoot.AddChild(pit);
                    _pitPositions.Add(cellCenter);
                }
            }
        }

        // 2. Build Interactive Puzzle Monoliths, Plates & Gates
        BuildPuzzleEntities();

        // 3. Explorer Marble
        _marbleNode = new Node3D { Name = "ExplorerMarble" };
        _dungeonRoot.AddChild(_marbleNode);

        _marbleMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = MarbleRadius, Height = MarbleRadius * 2.0f, RadialSegments = 24, Rings = 16 },
            Position = new Vector3(0, MarbleRadius, 0)
        };
        var marbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.75f, 1.0f),
            Metallic = 0.85f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.75f, 1.0f) * 0.5f
        };
        _marbleMesh.MaterialOverride = marbleMat;
        _marbleNode.AddChild(_marbleMesh);

        // 4. Extraction Altar
        _altarNode = new Node3D { Name = "SunAltar", Position = new Vector3(0, 0, -16.0f) };
        _dungeonRoot.AddChild(_altarNode);

        var altarBase = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.0f, BottomRadius = 2.4f, Height = 0.6f },
            Position = new Vector3(0, 0.3f, 0)
        };
        var altarMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.85f, 0.2f) * 0.8f
        };
        altarBase.MaterialOverride = altarMat;
        _altarNode.AddChild(altarBase);

        // 5. Overhead Isometric Camera
        _camera = new Camera3D
        {
            Name = "TempleCamera",
            Position = new Vector3(0, 38.0f, 26.0f),
            RotationDegrees = new Vector3(-58.0f, 0, 0),
            Fov = 50.0f,
            Current = true
        };
        AddChild(_camera);

        // Ambient Temple Torchlight
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-65.0f, 35.0f, 0),
            LightColor = new Color(1.0f, 0.92f, 0.8f),
            LightEnergy = 1.25f
        };
        AddChild(light);
    }

    private void BuildPuzzleEntities()
    {
        Vector2[] blockStarts = { new(-12.8f, 0f), new(12.8f, 0f) };
        var stoneMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.48f, 0.52f),
            Metallic = 0.3f,
            Roughness = 0.6f
        };

        for (int i = 0; i < blockStarts.Length; i++)
        {
            var node = new Node3D { Name = $"Monolith_{i}" };
            _dungeonRoot?.AddChild(node);
            node.Position = new Vector3(blockStarts[i].X, 1.2f, blockStarts[i].Y);

            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.4f, 2.4f, 2.4f) },
                MaterialOverride = stoneMat
            };
            node.AddChild(mesh);

            _monoliths.Add(new SlidingMonolith(node, blockStarts[i], 2.4f));
        }

        Vector2[] platePositions = { new(-12.8f, -9.6f), new(12.8f, 9.6f) };
        var plateMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.45f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.45f, 0.15f) * 0.6f
        };

        for (int i = 0; i < platePositions.Length; i++)
        {
            var pNode = new Node3D { Name = $"Plate_{i}" };
            _dungeonRoot?.AddChild(pNode);
            pNode.Position = new Vector3(platePositions[i].X, 0.08f, platePositions[i].Y);

            var pMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.35f, BottomRadius = 1.45f, Height = 0.16f },
                MaterialOverride = plateMat
            };
            pNode.AddChild(pMesh);

            _plates.Add(new PressurePlate(pNode, platePositions[i], i, pMesh));
        }

        Vector2[] gatePositions = { new(-6.4f, -9.6f), new(6.4f, 9.6f) };
        var gateMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.2f, 0.2f) * 0.8f
        };

        for (int i = 0; i < gatePositions.Length; i++)
        {
            var gNode = new Node3D { Name = $"Gate_{i}" };
            _dungeonRoot?.AddChild(gNode);
            gNode.Position = new Vector3(gatePositions[i].X, WallHeight * 0.5f, gatePositions[i].Y);

            var gMesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.95f, WallHeight, 0.4f) },
                MaterialOverride = gateMat
            };
            gNode.AddChild(gMesh);

            _gates.Add(new BarrierGate(gNode, i, gatePositions[i]));
        }

        Vector2[] relicPositions = { new(-16.0f, -9.6f), new(16.0f, 9.6f), new(0f, 16.0f) };
        var relicMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.85f, 0.15f) * 1.8f
        };

        for (int i = 0; i < relicPositions.Length; i++)
        {
            var rNode = new Node3D { Name = $"Relic_{i}" };
            _dungeonRoot?.AddChild(rNode);
            rNode.Position = new Vector3(relicPositions[i].X, 1.4f, relicPositions[i].Y);

            var rMesh = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f },
                MaterialOverride = relicMat
            };
            rNode.AddChild(rMesh);

            _relics.Add(new SunRelic(rNode, relicPositions[i]));
        }
    }

    public void RotateHeading(float deltaDeg)
    {
        _currentHeadingDeg = (_currentHeadingDeg + deltaDeg) % 360.0f;
        if (_currentHeadingDeg < 0) _currentHeadingDeg += 360.0f;
    }

    public void RotateQuadrant(float deltaDeg) => RotateHeading(deltaDeg);

    public void SetHeading(float headingDeg)
    {
        _currentHeadingDeg = (headingDeg % 360.0f + 360.0f) % 360.0f;
    }

    public void SetCalibrationOffset(float offsetDeg)
    {
        CalibrationOffset = offsetDeg;
    }

    public void SetCompassOffset(float offsetDeg) => SetCalibrationOffset(offsetDeg);

    public void SetSeed(int seed)
    {
        DungeonSeed = seed;
        ResetGame();
    }

    public void SetDungeonSeed(int seed) => SetSeed(seed);

    public override void ResetGame()
    {
        base.ResetGame();
        _marblePos = Vector2.Zero;
        _marbleVel = Vector2.Zero;
        _marbleY = MarbleRadius;
        _relicsCollected = 0;
        _platesSolved = 0;
        _altarReached = false;
        _lives = 3;
        _invulnTimer = 0f;
        _currentHeadingDeg = 0f;
        _visualHeadingDeg = 0f;
        _latestRankAchieved = -1;

        Vector2[] blockStarts = { new(-12.8f, 0f), new(12.8f, 0f) };
        for (int i = 0; i < _monoliths.Count; i++)
        {
            _monoliths[i].Pos = blockStarts[i];
            _monoliths[i].Vel = Vector2.Zero;
            _monoliths[i].IsSliding = false;
            _monoliths[i].Node.Position = new Vector3(blockStarts[i].X, 1.2f, blockStarts[i].Y);
        }

        foreach (var p in _plates)
        {
            p.IsPressed = false;
            p.Node.Position = new Vector3(p.Pos.X, 0.08f, p.Pos.Y);
        }
        foreach (var g in _gates)
        {
            g.IsOpen = false;
            g.OpenProgress = 0f;
            g.Node.Position = new Vector3(g.Pos.X, WallHeight * 0.5f, g.Pos.Y);
        }

        foreach (var r in _relics)
        {
            r.Collected = false;
            r.Node.Visible = true;
        }

        if (_marbleNode != null)
        {
            _marbleNode.Position = new Vector3(0, MarbleRadius, 0);
            _marbleNode.Visible = true;
        }

        Hud?.ShowBannerMessage("ROTATE BOARD 360° TO SHIFT TEMPLE GRAVITY!", 3.0f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.05f || MathF.Abs(boardState.Pitch) > 0.05f))
        {
            StartGame();
            Hud?.ShowBannerMessage("TEMPLE AWAKENED!", 1.5f);
        }

        if (GameState != "RUNNING") return;

        UpdateCompassHeading(boardState, dt);
        UpdateMarbleLocomotion(boardState, dt);
        UpdateMonoliths(dt);
        UpdatePlatesAndGates(dt);
        UpdateObjectives(dt);
    }

    private void UpdateCompassHeading(BoardState boardState, float dt)
    {
        if (boardState.HasMag && boardState.CompassHeadingDeg >= 0)
        {
            _currentHeadingDeg = (boardState.CompassHeadingDeg + CalibrationOffset + 360.0f) % 360.0f;
        }
        else
        {
            if (Godot.Input.IsKeyPressed(Key.Q) || Godot.Input.IsKeyPressed(Key.Bracketleft)) RotateHeading(-90.0f * dt);
            if (Godot.Input.IsKeyPressed(Key.E) || Godot.Input.IsKeyPressed(Key.Bracketright)) RotateHeading(90.0f * dt);
        }

        _visualHeadingDeg = Mathf.LerpAngle(_visualHeadingDeg * (MathF.PI / 180.0f), _currentHeadingDeg * (MathF.PI / 180.0f), 6.0f * dt) * (180.0f / MathF.PI);

        if (_dungeonRoot != null)
        {
            _dungeonRoot.Rotation = new Vector3(0, -_visualHeadingDeg * (MathF.PI / 180.0f), 0);
        }

        CardinalPlane newPlane;
        if (_currentHeadingDeg >= 315.0f || _currentHeadingDeg < 45.0f) newPlane = CardinalPlane.North;
        else if (_currentHeadingDeg >= 45.0f && _currentHeadingDeg < 135.0f) newPlane = CardinalPlane.East;
        else if (_currentHeadingDeg >= 135.0f && _currentHeadingDeg < 225.0f) newPlane = CardinalPlane.South;
        else newPlane = CardinalPlane.West;

        if (newPlane != _activeCardinalPlane)
        {
            _activeCardinalPlane = newPlane;
            OnCardinalPlaneChanged(_activeCardinalPlane);
        }
    }

    private void OnCardinalPlaneChanged(CardinalPlane plane)
    {
        _currentGravityDir = plane switch
        {
            CardinalPlane.North => new Vector2(0, -1),
            CardinalPlane.East => new Vector2(1, 0),
            CardinalPlane.South => new Vector2(0, 1),
            CardinalPlane.West => new Vector2(-1, 0),
            _ => new Vector2(0, -1)
        };

        string planeName = plane.ToString().ToUpperInvariant();
        Hud?.ShowBannerMessage($"🧭 TEMPLE ORIENTATION: [{planeName}] • GRAVITY SHIFTED!", 1.8f);

        foreach (var m in _monoliths)
        {
            m.IsSliding = true;
        }
    }

    private void UpdateMarbleLocomotion(BoardState boardState, float dt)
    {
        if (_invulnTimer > 0f) _invulnTimer -= dt;

        float angleRad = -_visualHeadingDeg * (MathF.PI / 180.0f);
        float cosA = MathF.Cos(angleRad);
        float sinA = MathF.Sin(angleRad);

        float inputX = boardState.Roll;
        float inputZ = boardState.Pitch;

        float driveX = (inputX * cosA - inputZ * sinA) * AccelGain;
        float driveZ = (inputX * sinA + inputZ * cosA) * AccelGain;

        _marbleVel.X += driveX * dt;
        _marbleVel.Y += driveZ * dt;

        _marbleVel *= Math.Max(0f, 1.0f - Friction * dt);

        _marblePos += _marbleVel * dt;

        ResolveMarbleWallCollisions();
        ResolveMarbleMonolithCollisions();
        ResolveMarbleGateCollisions();
        CheckHazardPits();

        if (_marbleNode != null)
        {
            _marbleNode.Position = new Vector3(_marblePos.X, _marbleY, _marblePos.Y);
        }
    }

    private void ResolveMarbleWallCollisions()
    {
        float halfCell = CellSize * 0.5f;

        foreach (var wallPos in _wallPositions)
        {
            float dx = _marblePos.X - wallPos.X;
            float dz = _marblePos.Y - wallPos.Y;

            float clampedX = Math.Clamp(dx, -halfCell, halfCell);
            float clampedZ = Math.Clamp(dz, -halfCell, halfCell);

            float diffX = dx - clampedX;
            float diffZ = dz - clampedZ;
            float distSq = diffX * diffX + diffZ * diffZ;

            if (distSq < MarbleRadius * MarbleRadius && distSq > 0.00001f)
            {
                float dist = MathF.Sqrt(distSq);
                float nx = diffX / dist;
                float nz = diffZ / dist;
                float pen = MarbleRadius - dist;

                _marblePos.X += nx * pen;
                _marblePos.Y += nz * pen;

                float dot = _marbleVel.X * nx + _marbleVel.Y * nz;
                if (dot < 0)
                {
                    _marbleVel.X -= (1.0f + 0.35f) * dot * nx;
                    _marbleVel.Y -= (1.0f + 0.35f) * dot * nz;
                }
            }
        }
    }

    private void ResolveMarbleMonolithCollisions()
    {
        foreach (var m in _monoliths)
        {
            float halfBlock = m.Size * 0.5f;
            float dx = _marblePos.X - m.Pos.X;
            float dz = _marblePos.Y - m.Pos.Y;

            float clampedX = Math.Clamp(dx, -halfBlock, halfBlock);
            float clampedZ = Math.Clamp(dz, -halfBlock, halfBlock);

            float diffX = dx - clampedX;
            float diffZ = dz - clampedZ;
            float distSq = diffX * diffX + diffZ * diffZ;

            if (distSq < MarbleRadius * MarbleRadius && distSq > 0.00001f)
            {
                float dist = MathF.Sqrt(distSq);
                float nx = diffX / dist;
                float nz = diffZ / dist;
                float pen = MarbleRadius - dist;

                _marblePos.X += nx * pen;
                _marblePos.Y += nz * pen;

                float dot = _marbleVel.X * nx + _marbleVel.Y * nz;
                if (dot < 0)
                {
                    _marbleVel.X -= (1.0f + 0.45f) * dot * nx;
                    _marbleVel.Y -= (1.0f + 0.45f) * dot * nz;
                }
            }
        }
    }

    private void ResolveMarbleGateCollisions()
    {
        foreach (var g in _gates)
        {
            if (g.IsOpen) continue;

            float halfX = CellSize * 0.48f;
            float halfZ = 0.25f;

            float dx = _marblePos.X - g.Pos.X;
            float dz = _marblePos.Y - g.Pos.Y;

            float clampedX = Math.Clamp(dx, -halfX, halfX);
            float clampedZ = Math.Clamp(dz, -halfZ, halfZ);

            float diffX = dx - clampedX;
            float diffZ = dz - clampedZ;
            float distSq = diffX * diffX + diffZ * diffZ;

            if (distSq < MarbleRadius * MarbleRadius && distSq > 0.00001f)
            {
                float dist = MathF.Sqrt(distSq);
                float nx = diffX / dist;
                float nz = diffZ / dist;
                float pen = MarbleRadius - dist;

                _marblePos.X += nx * pen;
                _marblePos.Y += nz * pen;

                float dot = _marbleVel.X * nx + _marbleVel.Y * nz;
                if (dot < 0)
                {
                    _marbleVel.X -= (1.0f + 0.2f) * dot * nx;
                    _marbleVel.Y -= (1.0f + 0.2f) * dot * nz;
                }
            }
        }
    }

    private void CheckHazardPits()
    {
        if (_invulnTimer > 0f) return;

        float pitRadius = CellSize * 0.45f;
        foreach (var pit in _pitPositions)
        {
            if (_marblePos.DistanceTo(pit) < pitRadius)
            {
                OnMarbleFellIntoPit();
                return;
            }
        }
    }

    private void OnMarbleFellIntoPit()
    {
        _lives--;
        _invulnTimer = 2.0f;
        _marblePos = Vector2.Zero;
        _marbleVel = Vector2.Zero;

        if (_lives > 0)
        {
            Hud?.ShowBannerMessage($"⚠️ FELL INTO SPIKE PIT! {_lives} LIVES REMAINING", 2.2f);
        }
        else
        {
            TriggerGameOver("Perished in the Temple Hazard Pits!");
            string playerName = LeaderboardService.Instance.ActivePlayerName;
            _latestRankAchieved = LeaderboardService.Instance.RecordScore(playerName, CurrentScore, _relicsCollected, (int)_currentHeadingDeg);
            Hud?.ShowBannerMessage($"💀 EXPEDITION FAILED! FINAL SCORE: {CurrentScore:N0}", 3.5f);
        }
    }

    private void UpdateMonoliths(float dt)
    {
        float blockSlideSpeed = 12.5f;

        foreach (var m in _monoliths)
        {
            if (!m.IsSliding) continue;

            Vector2 moveStep = _currentGravityDir * (blockSlideSpeed * dt);
            Vector2 targetPos = m.Pos + moveStep;

            bool collided = false;
            float halfBlock = m.Size * 0.5f;
            float halfCell = CellSize * 0.5f;

            foreach (var wallPos in _wallPositions)
            {
                float dx = MathF.Abs(targetPos.X - wallPos.X);
                float dz = MathF.Abs(targetPos.Y - wallPos.Y);

                if (dx < halfBlock + halfCell && dz < halfBlock + halfCell)
                {
                    collided = true;
                    break;
                }
            }

            if (!collided)
            {
                m.Pos = targetPos;
                m.Node.Position = new Vector3(m.Pos.X, 1.2f, m.Pos.Y);
            }
            else
            {
                m.IsSliding = false;
            }
        }
    }

    private void UpdatePlatesAndGates(float dt)
    {
        int activePlates = 0;

        foreach (var p in _plates)
        {
            bool occupied = false;

            foreach (var m in _monoliths)
            {
                if (m.Pos.DistanceTo(p.Pos) < 1.6f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied && _marblePos.DistanceTo(p.Pos) < 1.4f)
            {
                occupied = true;
            }

            if (occupied && !p.IsPressed)
            {
                p.IsPressed = true;
                AddScore(400);
                Hud?.ShowBannerMessage("🗝️ PRESSURE PLATE ACTIVATED! GATE LOWERING!", 1.8f);
                p.Node.Position = new Vector3(p.Pos.X, -0.05f, p.Pos.Y);
            }

            if (p.IsPressed) activePlates++;
        }

        _platesSolved = activePlates;

        foreach (var g in _gates)
        {
            bool shouldOpen = false;
            foreach (var p in _plates)
            {
                if (p.GateId == g.GateId && p.IsPressed)
                {
                    shouldOpen = true;
                    break;
                }
            }

            g.IsOpen = shouldOpen;
            float targetProgress = shouldOpen ? 1.0f : 0f;
            g.OpenProgress = Mathf.MoveToward(g.OpenProgress, targetProgress, 2.0f * dt);
            float currentY = Mathf.Lerp(WallHeight * 0.5f, -WallHeight * 0.5f, g.OpenProgress);
            g.Node.Position = new Vector3(g.Pos.X, currentY, g.Pos.Y);
        }
    }

    private void UpdateObjectives(float dt)
    {
        for (int i = 0; i < _relics.Count; i++)
        {
            var r = _relics[i];
            if (r.Collected) continue;

            r.Node.RotateY(dt * 3.5f);

            if (_marblePos.DistanceTo(r.Pos) < 1.45f)
            {
                r.Collected = true;
                r.Node.Visible = false;
                _relicsCollected++;
                AddScore(1000);

                if (_relicsCollected >= 3)
                {
                    Hud?.ShowBannerMessage("☀️ ALL 3 SUN RELICS RETRIEVED! HEAD TO EXTRACTION ALTAR! ☀️", 3.0f);
                }
                else
                {
                    Hud?.ShowBannerMessage($"☀️ SUN RELIC RETRIEVED! ({_relicsCollected}/3)", 1.8f);
                }
            }
        }

        if (!_altarReached && _relicsCollected >= 3 && _altarNode != null)
        {
            Vector2 altarPos = new(_altarNode.Position.X, _altarNode.Position.Z);
            if (_marblePos.DistanceTo(altarPos) < 2.2f)
            {
                _altarReached = true;
                int victoryScore = 5000 + (_lives * 1000);
                AddScore(victoryScore);
                TriggerGameOver("Temple Conquered! Ancient Secrets Extracted!");

                string playerName = LeaderboardService.Instance.ActivePlayerName;
                _latestRankAchieved = LeaderboardService.Instance.RecordScore(playerName, CurrentScore, _relicsCollected, (int)_currentHeadingDeg);
                Hud?.ShowBannerMessage($"🏆 VICTORY! TEMPLE MASTER! +{victoryScore:N0} BONUS!", 4.5f);
            }
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        _radarMonolithsCache.Clear();
        foreach (var m in _monoliths)
        {
            _radarMonolithsCache.Add(new Dictionary<string, object>
            {
                { "x", m.Pos.X },
                { "y", m.Pos.Y }
            });
        }

        _radarRelicsCache.Clear();
        foreach (var r in _relics)
        {
            _radarRelicsCache.Add(new Dictionary<string, object>
            {
                { "x", r.Pos.X },
                { "y", r.Pos.Y },
                { "collected", r.Collected }
            });
        }

        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["headingDeg"] = (int)_currentHeadingDeg;
        _liveStatsCache["heading"] = _currentHeadingDeg;
        _liveStatsCache["compassOffset"] = CalibrationOffset;
        _liveStatsCache["cardinalPlane"] = _activeCardinalPlane.ToString().ToUpperInvariant();
        _liveStatsCache["relicsCollected"] = _relicsCollected;
        _liveStatsCache["totalRelics"] = 3;
        _liveStatsCache["platesSolved"] = _platesSolved;
        _liveStatsCache["totalPlates"] = 2;
        _liveStatsCache["lives"] = _lives;
        _liveStatsCache["latestRank"] = _latestRankAchieved;
        _liveStatsCache["explorerPos"] = new Dictionary<string, object>
        {
            { "x", _marblePos.X },
            { "y", _marblePos.Y }
        };
        _liveStatsCache["playerPos"] = new Dictionary<string, object>
        {
            { "x", _marblePos.X },
            { "y", _marblePos.Y }
        };
        _liveStatsCache["blocks"] = _radarMonolithsCache;
        _liveStatsCache["relics"] = _radarRelicsCache;
        _liveStatsCache["seed"] = DungeonSeed;

        return _liveStatsCache;
    }
}
