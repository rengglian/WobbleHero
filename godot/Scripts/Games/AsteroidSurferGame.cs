using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Asteroid Surfer.
/// High-speed cosmic gravity wave runner with momentum carving,
/// pitch dive acceleration/aerodynamic braking, lip jump air-time,
/// clean landing evaluation (|roll| & |pitch| < 4 deg), asteroids,
/// plasma score rings, and black hole gravity well hazards.
/// </summary>
public partial class AsteroidSurferGame : BaseGameMode
{
    public override string GameId => "AsteroidSurfer";
    public override string GameTitle => "Asteroid Surfer";
    public override string GameDescription => "Ride cosmic gravity waves, carve the lip for massive ballistic air-time, and dodge black holes!";
    public override string GameIcon => "🏄";

    [Export] public float BaseSpeed { get; set; } = 20.0f;
    [Export] public float MaxSpeed { get; set; } = 38.0f;
    [Export] public float MinSpeed { get; set; } = 14.0f;
    [Export] public int Seed { get; set; } = 2049;
    [Export] public string CosmicDensity { get; set; } = "medium";

    public const float HalfWidth = 16.0f;
    public const float SegmentLength = 90.0f;
    public const int SlabsCount = 3;

    // Node references
    private Node3D? _trackContainer;
    private Node3D? _playerNode;
    private Node3D? _boardMesh;
    private Camera3D? _camera;
    private readonly List<MeshInstance3D> _waveSlabs = new(SlabsCount);

    // Surfer physics state
    private float _playerX = 0f;
    private float _playerY = 0f;
    private float _verticalVel = 0f;
    private float _horizontalAirVel = 0f;
    private bool _isAirborne = false;
    private bool _isCarvingLip = false;
    private float _airTime = 0f;
    private float _distanceRun = 0f;
    private float _currentSpeed = 20.0f;
    private float _multiplier = 1.0f;
    private int _plasmaRingsCollected = 0;
    private int _cleanLandingsCount = 0;
    private int _latestRankAchieved = -1;
    private float _bankAngle = 0f;

    // Procedural wave generator & spawning
    private float _obstacleSpawnZ = -60.0f;
    private Mulberry32 _rng;

    // Obstacle types
    public enum EntityKind { Asteroid, PlasmaRingLow, PlasmaRingHigh, BlackHole }
    private sealed class TrackEntity
    {
        public Node3D Node { get; }
        public float X;
        public float Y;
        public float Z;
        public EntityKind Kind;
        public bool Cleared;
        public float Radius;
        public float RotSpeedX;
        public float RotSpeedY;

        public TrackEntity(Node3D node, float x, float y, float z, EntityKind kind, float radius)
        {
            Node = node;
            X = x;
            Y = y;
            Z = z;
            Kind = kind;
            Radius = radius;
            Cleared = false;
        }
    }

    private readonly List<TrackEntity> _activeEntities = new(32);
    private readonly Dictionary<string, object> _liveStatsCache = new(16);

    public struct Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed == 0 ? 2049u : seed;
        public float NextFloat()
        {
            _state += 0x6D2B79F5;
            uint t = _state;
            t = (t ^ (t >> 15)) * (_state | 1);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return ((t ^ (t >> 14)) >> 0) / 4294967296.0f;
        }
        public int NextInt(int min, int max) => (int)(min + NextFloat() * (max - min));
    }

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    public void SetSurferConfig(float speed, string density)
    {
        BaseSpeed = Mathf.Clamp(speed / 3.6f, 5.0f, 25.0f); // Convert km/h to m/s
        CosmicDensity = density?.ToLowerInvariant() ?? "medium";
        _currentSpeed = BaseSpeed;
        GD.Print($"[AsteroidSurfer] Config Updated -> BaseSpeed={BaseSpeed * 3.6f:F0} km/h, Density={CosmicDensity}");
    }

    private void BuildSceneNodes()
    {
        // 1. Track container
        _trackContainer = new Node3D { Name = "Track" };
        AddChild(_trackContainer);

        // Generate procedural wave ribbon mesh
        var waveMesh = GenerateWaveMesh(HalfWidth * 2.0f, SegmentLength, 32, 24);
        var waveShader = GD.Load<Shader>("res://Shaders/cosmic_wave.gdshader");
        var waveMat = new ShaderMaterial { Shader = waveShader };

        for (int i = 0; i < SlabsCount; i++)
        {
            var slab = new MeshInstance3D
            {
                Mesh = waveMesh,
                MaterialOverride = waveMat,
                Position = new Vector3(0, 0, -i * SegmentLength)
            };
            _trackContainer.AddChild(slab);
            _waveSlabs.Add(slab);
        }

        // 2. Player Hoverboard
        _playerNode = new Node3D { Name = "Surfer" };
        AddChild(_playerNode);

        _boardMesh = new Node3D { Name = "Hoverboard" };
        _playerNode.AddChild(_boardMesh);

        // Surfer Deck
        var deck = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.3f, 0.14f, 2.7f) },
            Position = new Vector3(0, 0.12f, 0)
        };
        var deckMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.88f, 0.95f),
            Metallic = 0.7f,
            Roughness = 0.25f,
            EmissionEnabled = true,
            Emission = new Color(0.0f, 0.65f, 0.85f) * 0.4f
        };
        deck.MaterialOverride = deckMat;
        _boardMesh.AddChild(deck);

        // Deck central accent stripe
        var stripe = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.16f, 2.5f) },
            Position = new Vector3(0, 0.13f, 0)
        };
        var stripeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.15f, 0.75f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.15f, 0.75f) * 0.8f
        };
        stripe.MaterialOverride = stripeMat;
        _boardMesh.AddChild(stripe);

        // Dual Ion Thruster Nozzles
        for (int side = -1; side <= 1; side += 2)
        {
            var nozzle = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.22f, Height = 0.45f },
                RotationDegrees = new Vector3(90, 0, 0),
                Position = new Vector3(side * 0.42f, 0.12f, 1.35f)
            };
            var nozzleMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.0f, 1.0f, 0.9f),
                EmissionEnabled = true,
                Emission = new Color(0.0f, 1.0f, 0.9f) * 1.5f
            };
            nozzle.MaterialOverride = nozzleMat;
            _boardMesh.AddChild(nozzle);
        }

        // 3. Dynamic Follow Camera
        _camera = new Camera3D
        {
            Name = "ChaseCamera",
            Fov = 72.0f,
            Position = new Vector3(0, 3.2f, 7.0f),
            Current = true
        };
        AddChild(_camera);

        // Directional cosmic light
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, 45, 0),
            LightColor = new Color(0.85f, 0.92f, 1.0f),
            LightEnergy = 1.2f
        };
        AddChild(light);
    }

    private static ArrayMesh GenerateWaveMesh(float totalWidth, float length, int xRes, int zRes)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float dx = totalWidth / xRes;
        float dz = length / zRes;
        float startX = -totalWidth * 0.5f;

        for (int z = 0; z <= zRes; z++)
        {
            float posZ = -z * dz;
            float uvV = (float)z / zRes;

            for (int x = 0; x <= xRes; x++)
            {
                float posX = startX + x * dx;
                float posY = GetWaveHeight(posX);
                float uvU = (float)x / xRes;

                Vector3 normal = GetWaveNormal(posX);
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2(uvU, uvV));
                surfaceTool.AddVertex(new Vector3(posX, posY, posZ));
            }
        }

        int stride = xRes + 1;
        for (int z = 0; z < zRes; z++)
        {
            for (int x = 0; x < xRes; x++)
            {
                int i0 = z * stride + x;
                int i1 = z * stride + (x + 1);
                int i2 = (z + 1) * stride + (x + 1);
                int i3 = (z + 1) * stride + x;

                surfaceTool.AddIndex(i0);
                surfaceTool.AddIndex(i1);
                surfaceTool.AddIndex(i2);

                surfaceTool.AddIndex(i0);
                surfaceTool.AddIndex(i2);
                surfaceTool.AddIndex(i3);
            }
        }

        return surfaceTool.Commit();
    }

    public static float GetWaveHeight(float x)
    {
        if (x <= -6.0f) return 0f;
        if (x <= 11.5f)
        {
            float t = (x + 6.0f) / 17.5f;
            return 8.5f * MathF.Pow(t, 2.2f);
        }
        else
        {
            float tLip = Math.Clamp((x - 11.5f) / 4.5f, 0f, 1f);
            return 8.5f + MathF.Sin(tLip * MathF.PI) * 0.8f;
        }
    }

    public static float GetWaveSlope(float x)
    {
        if (x <= -6.0f) return 0f;
        if (x <= 11.5f)
        {
            float t = (x + 6.0f) / 17.5f;
            return (8.5f * 2.2f / 17.5f) * MathF.Pow(t, 1.2f);
        }
        else
        {
            float tLip = Math.Clamp((x - 11.5f) / 4.5f, 0f, 1f);
            return (MathF.PI * 0.8f / 4.5f) * MathF.Cos(tLip * MathF.PI);
        }
    }

    public static Vector3 GetWaveNormal(float x)
    {
        float slope = GetWaveSlope(x);
        float len = MathF.Sqrt(slope * slope + 1.0f);
        return new Vector3(-slope / len, 1.0f / len, 0f);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _rng = new Mulberry32((uint)Math.Abs(Seed));
        _playerX = -2.0f;
        _playerY = GetWaveHeight(_playerX);
        _verticalVel = 0f;
        _horizontalAirVel = 0f;
        _isAirborne = false;
        _isCarvingLip = false;
        _airTime = 0f;
        _distanceRun = 0f;
        _currentSpeed = BaseSpeed;
        _multiplier = 1.0f;
        _plasmaRingsCollected = 0;
        _cleanLandingsCount = 0;
        _latestRankAchieved = -1;
        _obstacleSpawnZ = -55.0f;

        foreach (var ent in _activeEntities)
        {
            ent.Node.QueueFree();
        }
        _activeEntities.Clear();

        if (_playerNode != null)
        {
            _playerNode.Position = new Vector3(_playerX, _playerY, 0f);
            if (_boardMesh != null) _boardMesh.Rotation = Vector3.Zero;
        }

        Hud?.ShowBannerMessage($"COSMIC WAVE #{Seed} • LEAN BOARD TO SURF!", 2.5f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.06f || MathF.Abs(boardState.Pitch) > 0.06f))
        {
            StartGame();
            Hud?.ShowBannerMessage("SURF RUN INITIATED!", 1.2f);
        }

        if (GameState != "RUNNING") return;

        _bankAngle = boardState.FusedRollDeg;

        // 1. Pitch-Based Dive Acceleration & Aerodynamic Stall Braking
        float pitchVal = boardState.Pitch;
        if (pitchVal > 0.05f)
        {
            // Forward dive down wave face
            float aDive = 14.0f * pitchVal;
            _currentSpeed = MathF.Min(MaxSpeed, _currentSpeed + aDive * dt);
        }
        else if (pitchVal < -0.05f)
        {
            // Aerodynamic stall braking
            float aStall = 12.0f * pitchVal; // negative
            _currentSpeed = MathF.Max(MinSpeed, _currentSpeed + aStall * dt);
        }
        else
        {
            // Return towards BaseSpeed
            _currentSpeed = Mathf.MoveToward(_currentSpeed, BaseSpeed, 1.8f * dt);
        }

        // 2. Surfer Physics & Ballistic Airtime State
        float waveSurfaceY = GetWaveHeight(_playerX);
        _isCarvingLip = _playerX > 10.5f;

        if (_isAirborne)
        {
            _airTime += dt;
            _verticalVel -= 16.0f * dt; // Gravity
            _playerY += _verticalVel * dt;
            _playerX += _horizontalAirVel * dt;
            _playerX = Math.Clamp(_playerX, -15.0f, 14.5f);

            // In-flight board free tilt
            if (_boardMesh != null)
            {
                _boardMesh.Rotation = new Vector3(
                    -boardState.Pitch * 0.35f,
                    0,
                    -boardState.Roll * 0.45f
                );
            }

            // Check touchdown
            if (_playerY <= waveSurfaceY && _verticalVel < 0f)
            {
                _playerY = waveSurfaceY;
                _isAirborne = false;

                // Evaluate Clean Landing criteria: |roll| < 4 deg, |pitch| < 4 deg
                float rollDeg = MathF.Abs(boardState.FusedRollDeg);
                float pitchDeg = MathF.Abs(boardState.FusedPitchDeg);

                // Fallback check for simulated keyboard/touch
                if (boardState.Source != "atoms3")
                {
                    rollDeg = MathF.Abs(boardState.Roll * 28.0f);
                    pitchDeg = MathF.Abs(boardState.Pitch * 28.0f);
                }

                if (rollDeg < 4.0f && pitchDeg < 4.0f)
                {
                    // Clean Landing!
                    _cleanLandingsCount++;
                    _multiplier = MathF.Min(5.0f, _multiplier + 0.5f);
                    int bonus = (int)(500 * _multiplier);
                    AddScore(bonus);
                    _currentSpeed = MathF.Min(MaxSpeed, _currentSpeed + 5.0f);
                    Hud?.ShowBannerMessage($"✨ CLEAN COSMIC LANDING! +{bonus} (x{_multiplier:F1}) ✨", 2.0f);
                }
                else
                {
                    // Rough landing - loss of multiplier
                    _multiplier = 1.0f;
                    _currentSpeed = MathF.Max(MinSpeed, _currentSpeed - 6.0f);
                    Hud?.ShowBannerMessage("💥 WOBBLE LANDING! MULTIPLIER RESET", 2.0f);
                }
            }
        }
        else
        {
            // Grounded Surfing Physics
            float vCarve = 18.0f;
            float slopeAngle = MathF.Atan(GetWaveSlope(_playerX));
            float gravitySlide = -6.5f * MathF.Sin(slopeAngle);

            _playerX += (boardState.Roll * vCarve + gravitySlide) * dt;
            _playerX = Math.Clamp(_playerX, -15.0f, 13.5f);
            _playerY = GetWaveHeight(_playerX);

            // Lip Jump Detection: x >= 11.2m and Speed >= 24 m/s
            if (_playerX >= 11.2f && _currentSpeed >= 24.0f)
            {
                _isAirborne = true;
                _airTime = 0f;
                _verticalVel = 9.5f + (_currentSpeed - 24.0f) * 0.35f;
                _horizontalAirVel = -3.8f; // arc inwards
                Hud?.ShowBannerMessage("🚀 LIP JUMP! ZERO-G AIR-TIME!", 1.5f);
            }

            // Board banking alignment along wave slope
            if (_boardMesh != null)
            {
                float bankRoll = -slopeAngle - (boardState.Roll * 0.4f);
                float bankPitch = -boardState.Pitch * 0.25f;
                _boardMesh.Rotation = new Vector3(bankPitch, 0, bankRoll);
            }
        }

        // Apply Player Node Transform
        if (_playerNode != null)
        {
            _playerNode.Position = new Vector3(_playerX, _playerY, 0);
        }

        // Camera chase
        if (_camera != null)
        {
            float targetCamX = _playerX * 0.55f;
            float targetCamY = _playerY + 2.8f;
            _camera.Position = _camera.Position.Lerp(new Vector3(targetCamX, targetCamY, 6.8f), 8.0f * dt);
        }

        // 3. Track Progression
        _distanceRun += _currentSpeed * dt;
        AddScore((int)(_currentSpeed * dt * 2.5f * _multiplier));

        // 4. Wave Slabs Infinite Recycling
        float totalTrackSpan = SlabsCount * SegmentLength;
        for (int i = 0; i < _waveSlabs.Count; i++)
        {
            var slab = _waveSlabs[i];
            float newZ = slab.Position.Z + _currentSpeed * dt;
            if (newZ > SegmentLength)
            {
                newZ -= totalTrackSpan;
            }
            slab.Position = new Vector3(0, 0, newZ);
        }

        // 5. Procedural Obstacle Spawning
        if (_distanceRun >= _obstacleSpawnZ)
        {
            SpawnObstacleWave();
            float spacing = (CosmicDensity == "high" ? 32.0f : (CosmicDensity == "low" ? 58.0f : 45.0f)) + _rng.NextFloat() * 18.0f;
            _obstacleSpawnZ = _distanceRun + spacing;
        }

        // 6. Update Entities, Collision & Black Hole Vortex Pull
        for (int i = _activeEntities.Count - 1; i >= 0; i--)
        {
            var ent = _activeEntities[i];
            ent.Z += _currentSpeed * dt;
            ent.Node.Position = new Vector3(ent.X, ent.Y, ent.Z);

            // Rotate asteroids and rings
            if (ent.Kind == EntityKind.Asteroid)
            {
                ent.Node.RotateX(dt * ent.RotSpeedX);
                ent.Node.RotateY(dt * ent.RotSpeedY);
            }
            else if (ent.Kind == EntityKind.PlasmaRingLow || ent.Kind == EntityKind.PlasmaRingHigh)
            {
                ent.Node.RotateZ(dt * 3.0f);
            }
            else if (ent.Kind == EntityKind.BlackHole)
            {
                ent.Node.RotateY(dt * 4.5f);

                // Black Hole Gravity Well Pull
                float dxHole = ent.X - _playerX;
                float dyHole = ent.Y - _playerY;
                float dzHole = ent.Z;
                float distHole = MathF.Sqrt(dxHole * dxHole + dyHole * dyHole + dzHole * dzHole);

                if (distHole < 7.5f)
                {
                    float pullForce = 35.0f / (distHole + 1.0f);
                    _playerX += (dxHole / distHole) * pullForce * dt;

                    if (distHole < 1.35f)
                    {
                        OnBlackHoleSwallowed();
                        return;
                    }
                }
            }

            // Player Collision Check near Z ~ 0
            if (!ent.Cleared && ent.Z >= -1.2f && ent.Z <= 1.2f)
            {
                float dx = ent.X - _playerX;
                float dy = ent.Y - _playerY;
                float dist2D = MathF.Sqrt(dx * dx + dy * dy);

                if (dist2D < ent.Radius)
                {
                    if (ent.Kind == EntityKind.Asteroid)
                    {
                        OnAsteroidCrash();
                        return;
                    }
                    else if (ent.Kind == EntityKind.PlasmaRingLow || ent.Kind == EntityKind.PlasmaRingHigh)
                    {
                        ent.Cleared = true;
                        _plasmaRingsCollected++;
                        _multiplier = MathF.Min(5.0f, _multiplier + 0.2f);
                        int ringBonus = (int)(200 * _multiplier);
                        AddScore(ringBonus);
                        Hud?.ShowBannerMessage($"⚡ PLASMA RING +{ringBonus} (x{_multiplier:F1})", 0.7f);
                        ent.Node.QueueFree();
                        _activeEntities.RemoveAt(i);
                        continue;
                    }
                }
            }

            // Recycle off-screen
            if (ent.Z > 15.0f)
            {
                ent.Node.QueueFree();
                _activeEntities.RemoveAt(i);
            }
        }
    }

    private void SpawnObstacleWave()
    {
        float spawnZ = -SegmentLength * 1.2f;
        float roll = _rng.NextFloat();

        if (roll < 0.45f)
        {
            // Asteroid Cluster
            int count = _rng.NextInt(1, 3);
            for (int k = 0; k < count; k++)
            {
                float posX = -12.0f + _rng.NextFloat() * 21.0f;
                float posY = GetWaveHeight(posX) + 0.9f;
                SpawnAsteroid(posX, posY, spawnZ - k * 14.0f);
            }
        }
        else if (roll < 0.75f)
        {
            // Plasma Score Rings (Wave face or High Lip Jump)
            bool isHighLip = _rng.NextFloat() < 0.4f;
            if (isHighLip)
            {
                // Air ring high above lip
                float posX = 9.5f + _rng.NextFloat() * 2.5f;
                float posY = 12.0f + _rng.NextFloat() * 2.5f;
                SpawnPlasmaRing(posX, posY, spawnZ, EntityKind.PlasmaRingHigh);
            }
            else
            {
                // Surface ring
                float posX = -8.0f + _rng.NextFloat() * 16.0f;
                float posY = GetWaveHeight(posX) + 1.2f;
                SpawnPlasmaRing(posX, posY, spawnZ, EntityKind.PlasmaRingLow);
            }
        }
        else
        {
            // Black Hole Vortex
            float posX = -6.0f + _rng.NextFloat() * 12.0f;
            float posY = GetWaveHeight(posX) + 0.5f;
            SpawnBlackHole(posX, posY, spawnZ);
        }
    }

    private void SpawnAsteroid(float x, float y, float z)
    {
        var node = new Node3D();
        _trackContainer?.AddChild(node);
        node.Position = new Vector3(x, y, z);

        var rockMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.15f, Height = 2.3f, RadialSegments = 8, Rings = 6 }
        };
        var rockMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.32f, 0.40f),
            Roughness = 0.9f,
            Metallic = 0.5f,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.3f, 0.1f) * 0.25f // molten fissure
        };
        rockMesh.MaterialOverride = rockMat;
        node.AddChild(rockMesh);

        var ent = new TrackEntity(node, x, y, z, EntityKind.Asteroid, 1.45f)
        {
            RotSpeedX = 1.2f + _rng.NextFloat() * 2.0f,
            RotSpeedY = 1.0f + _rng.NextFloat() * 2.0f
        };
        _activeEntities.Add(ent);
    }

    private void SpawnPlasmaRing(float x, float y, float z, EntityKind kind)
    {
        var node = new Node3D();
        _trackContainer?.AddChild(node);
        node.Position = new Vector3(x, y, z);

        var ringMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1.25f }
        };
        Color neonColor = kind == EntityKind.PlasmaRingHigh
            ? new Color(1.0f, 0.2f, 0.8f) // High Jump: Radiant Magenta
            : new Color(0.1f, 0.95f, 1.0f); // Surface: Electric Cyan

        var ringMat = new StandardMaterial3D
        {
            AlbedoColor = neonColor,
            EmissionEnabled = true,
            Emission = neonColor * 1.6f
        };
        ringMesh.MaterialOverride = ringMat;
        node.AddChild(ringMesh);

        var ent = new TrackEntity(node, x, y, z, kind, 1.6f);
        _activeEntities.Add(ent);
    }

    private void SpawnBlackHole(float x, float y, float z)
    {
        var node = new Node3D();
        _trackContainer?.AddChild(node);
        node.Position = new Vector3(x, y, z);

        // Core singularity
        var coreMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.85f, Height = 1.7f }
        };
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.01f, 0.01f, 0.02f),
            Roughness = 1.0f
        };
        coreMesh.MaterialOverride = coreMat;
        node.AddChild(coreMesh);

        // Accretion Disk
        var diskMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 1.1f, OuterRadius = 2.4f },
            RotationDegrees = new Vector3(65, 0, 0)
        };
        var diskMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.45f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.35f, 0.05f) * 2.0f
        };
        diskMesh.MaterialOverride = diskMat;
        node.AddChild(diskMesh);

        var ent = new TrackEntity(node, x, y, z, EntityKind.BlackHole, 1.35f);
        _activeEntities.Add(ent);
    }

    private void OnAsteroidCrash()
    {
        TriggerGameOver("Smashed into Deep Space Asteroid!");
        FinalizeScoreSubmission();
        Hud?.ShowBannerMessage($"💥 CRASH! {CurrentScore:N0} PTS ({_distanceRun:F0}m)", 3.5f);
    }

    private void OnBlackHoleSwallowed()
    {
        TriggerGameOver("Consumed by Black Hole Singularity!");
        FinalizeScoreSubmission();
        Hud?.ShowBannerMessage($"🌌 EVENT HORIZON COLLAPSE! ({CurrentScore:N0} PTS)", 3.5f);
    }

    private void FinalizeScoreSubmission()
    {
        string player = LeaderboardService.Instance.ActivePlayerName;
        int rank = LeaderboardService.Instance.RecordScore(player, CurrentScore, (int)_distanceRun, Seed);
        _latestRankAchieved = rank;
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["speed"] = (int)(_currentSpeed * 3.6f); // km/h
        _liveStatsCache["distance"] = (int)_distanceRun;
        _liveStatsCache["multiplier"] = _multiplier;
        _liveStatsCache["airTime"] = _airTime;
        _liveStatsCache["isAirborne"] = _isAirborne;
        _liveStatsCache["isCarvingLip"] = _isCarvingLip;
        _liveStatsCache["plasmaRings"] = _plasmaRingsCollected;
        _liveStatsCache["cleanLandings"] = _cleanLandingsCount;
        _liveStatsCache["seed"] = Seed;
        _liveStatsCache["latestRank"] = _latestRankAchieved;
        _liveStatsCache["waveSpeed"] = BaseSpeed * 3.6f;
        _liveStatsCache["cosmicDensity"] = CosmicDensity;
        _liveStatsCache["wavePosition"] = _playerX / HalfWidth;
        _liveStatsCache["bankAngle"] = _bankAngle;
        _liveStatsCache["lipHeight"] = _playerY;
        return _liveStatsCache;
    }
}
