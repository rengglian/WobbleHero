using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Downhill Slalom (Alpine Speed Carver).
/// High-speed alpine winter sports runner. The player carves edge turns
/// through alternating Red/Blue slalom gate flags while balancing edge hold
/// across ice sheets, powder snow, and jump kickers.
/// Leaning forward executes an aerodynamic speed-tuck up to 120 km/h;
/// leaning back executes a hockey-stop drift to shed speed for tight hairpins.
/// </summary>
public partial class DownhillSlalomGame : BaseGameMode
{
    public override string GameId => "DownhillSlalom";
    public override string GameTitle => "Downhill Slalom";
    public override string GameDescription => "Carve down alpine mountain pistes! Tuck for 120 km/h speed runs, weave through slalom gates, and nail jump kickers.";
    public override string GameIcon => "🏔️";

    // Track Dimensions
    public const float PisteHalfWidth = 12.0f; // 24m wide piste
    public const float SegmentLength = 90.0f;
    public const int SlabsCount = 3;

    // Speeds (m/s)
    public const float CruiseSpeed = 22.0f; // ~79 km/h
    public const float MaxTuckSpeed = 33.5f; // ~120.6 km/h
    public const float MinBrakeSpeed = 10.0f; // ~36 km/h

    // Course & Snow Configuration
    [Export] public string CoursePreset { get; set; } = "giant_slalom"; // "slalom", "giant_slalom", "downhill"
    [Export] public string SnowCondition { get; set; } = "powder"; // "powder", "ice"
    [Export] public int Seed { get; set; } = 4096;

    // Slalom Gate Entity
    private sealed class SlalomGate
    {
        public Node3D Node { get; }
        public MeshInstance3D LeftPole { get; }
        public MeshInstance3D RightPole { get; }
        public MeshInstance3D BannerMesh { get; }
        public float CenterX;
        public float Z;
        public float HalfWidth;
        public bool IsRed;
        public bool Cleared;
        public bool Missed;

        public SlalomGate(Node3D node, MeshInstance3D left, MeshInstance3D right, MeshInstance3D banner, float x, float z, float halfW, bool isRed)
        {
            Node = node;
            LeftPole = left;
            RightPole = right;
            BannerMesh = banner;
            CenterX = x;
            Z = z;
            HalfWidth = halfW;
            IsRed = isRed;
            Cleared = false;
            Missed = false;
        }
    }

    // Hazard / Feature Entity
    public enum FeatureKind { PineTree, IcePatch, JumpKicker }
    private sealed class PisteFeature
    {
        public Node3D Node { get; }
        public float X;
        public float Z;
        public float Radius;
        public FeatureKind Kind;

        public PisteFeature(Node3D node, float x, float z, float radius, FeatureKind kind)
        {
            Node = node;
            X = x;
            Z = z;
            Radius = radius;
            Kind = kind;
        }
    }

    // Node hierarchy
    private Node3D? _pisteContainer;
    private Node3D? _skierNode;
    private Node3D? _skierMeshContainer;
    private Camera3D? _camera;
    private readonly List<MeshInstance3D> _pisteSlabs = new(SlabsCount);

    // Skier Physics State
    private float _skierX = 0f;
    private float _skierY = 0f;
    private float _verticalVel = 0f;
    private bool _isAirborne = false;
    private float _currentSpeed = CruiseSpeed;
    private float _topSpeed = CruiseSpeed;
    private float _distanceRun = 0f;
    private bool _isTucking = false;
    private bool _isDrifting = false;

    // Match Progression
    private int _gatesCleared = 0;
    private int _gatesMissed = 0;
    private float _multiplier = 1.0f;
    private int _latestRankAchieved = -1;
    private float _gateSpawnDistance = 35.0f;

    // Pre-allocated entity pools
    private readonly List<SlalomGate> _activeGates = new(16);
    private readonly List<PisteFeature> _activeFeatures = new(32);
    private readonly Dictionary<string, object> _liveStatsCache = new(14);
    private Mulberry32 _rng;

    public struct Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed == 0 ? 4096u : seed;
        public float NextFloat()
        {
            _state += 0x6D2B79F5;
            uint t = _state;
            t = (t ^ (t >> 15)) * (_state | 1);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return ((t ^ (t >> 14)) >> 0) / 4294967296.0f;
        }
    }

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Piste Track Container
        _pisteContainer = new Node3D { Name = "PisteContainer" };
        AddChild(_pisteContainer);

        // Snow surface slabs
        var snowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.95f, 1.0f),
            Roughness = 0.45f,
            Metallic = 0.05f
        };
        for (int i = 0; i < SlabsCount; i++)
        {
            var slab = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(PisteHalfWidth * 2.0f, SegmentLength) },
                MaterialOverride = snowMat,
                Position = new Vector3(0, 0, -i * SegmentLength)
            };
            _pisteContainer.AddChild(slab);
            _pisteSlabs.Add(slab);
        }

        // 2. Skier Character Model
        _skierNode = new Node3D { Name = "Skier" };
        AddChild(_skierNode);

        _skierMeshContainer = new Node3D { Name = "MeshContainer" };
        _skierNode.AddChild(_skierMeshContainer);

        // Twin Carve Skis
        for (int side = -1; side <= 1; side += 2)
        {
            var ski = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.08f, 2.4f) },
                Position = new Vector3(side * 0.42f, 0.05f, 0)
            };
            var skiMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.2f, 0.25f), Metallic = 0.6f, Roughness = 0.2f };
            ski.MaterialOverride = skiMat;
            _skierMeshContainer.AddChild(ski);
        }

        // Skier Torso (Racing suit)
        var torso = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.32f, Height = 1.1f },
            Position = new Vector3(0, 0.95f, 0)
        };
        var suitMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.45f, 0.95f), Roughness = 0.5f };
        torso.MaterialOverride = suitMat;
        _skierMeshContainer.AddChild(torso);

        // Aerodynamic Visor Helmet
        var helmet = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.32f, Height = 0.64f },
            Position = new Vector3(0, 1.65f, 0.05f)
        };
        var helmetMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.2f),
            Metallic = 0.9f,
            Roughness = 0.15f
        };
        helmet.MaterialOverride = helmetMat;
        _skierMeshContainer.AddChild(helmet);

        // 3. Dynamic Third-Person Chase Camera
        _camera = new Camera3D
        {
            Name = "ChaseCamera",
            Fov = 68.0f,
            Position = new Vector3(0, 3.2f, 7.2f),
            RotationDegrees = new Vector3(-14.0f, 0, 0),
            Current = true
        };
        AddChild(_camera);

        // Alpine Sun Light
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-42.0f, 35.0f, 0),
            LightColor = new Color(0.98f, 0.98f, 1.0f),
            LightEnergy = 1.4f
        };
        AddChild(sun);
    }

    public void SetCourseType(string course)
    {
        CoursePreset = course.ToLowerInvariant();
        ResetGame();
    }

    public void SetSnowCondition(string condition)
    {
        SnowCondition = condition.ToLowerInvariant();
    }

    public void SetSlalomConfig(string? course, string? snow)
    {
        if (!string.IsNullOrEmpty(course)) CoursePreset = course.ToLowerInvariant();
        if (!string.IsNullOrEmpty(snow)) SnowCondition = snow.ToLowerInvariant();
        ResetGame();
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _rng = new Mulberry32((uint)Math.Abs(Seed));
        _skierX = 0f;
        _skierY = 0f;
        _verticalVel = 0f;
        _isAirborne = false;
        _currentSpeed = CruiseSpeed;
        _topSpeed = CruiseSpeed;
        _distanceRun = 0f;
        _isTucking = false;
        _isDrifting = false;
        _gatesCleared = 0;
        _gatesMissed = 0;
        _multiplier = 1.0f;
        _latestRankAchieved = -1;
        _gateSpawnDistance = 40.0f;

        // Clear active gates and features
        foreach (var g in _activeGates) g.Node.QueueFree();
        _activeGates.Clear();

        foreach (var f in _activeFeatures) f.Node.QueueFree();
        _activeFeatures.Clear();

        if (_skierNode != null)
        {
            _skierNode.Position = Vector3.Zero;
            if (_skierMeshContainer != null) _skierMeshContainer.Rotation = Vector3.Zero;
        }

        // Spawn initial trees along snowbanks
        for (int i = 0; i < 12; i++)
        {
            float side = (i % 2 == 0) ? -1 : 1;
            float px = side * (PisteHalfWidth + 2.5f + _rng.NextFloat() * 4.0f);
            float pz = -i * 20.0f;
            SpawnPineTree(px, pz);
        }

        string courseName = CoursePreset.Replace('_', ' ').ToUpperInvariant();
        Hud?.ShowBannerMessage($"ALPINE COURSE: {courseName} • LEAN TO CARVE!", 2.5f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.05f || MathF.Abs(boardState.Pitch) > 0.05f))
        {
            StartGame();
            Hud?.ShowBannerMessage("RACE COMMENCED! WEAVE THROUGH GATES!", 1.2f);
        }

        if (GameState != "RUNNING") return;

        // 1. Pitch Aerodynamics: Speed-Tuck vs Hockey-Stop Drift
        // boardState.Pitch: forward lean (tilt forward < -0.25) => tuck
        // back lean (tilt backward > 0.20) => drift brake
        float pitchNorm = boardState.Pitch;
        if (pitchNorm < -0.25f)
        {
            // Aerodynamic Speed-Tuck
            _isTucking = true;
            _isDrifting = false;
            float tuckAccel = 5.8f;
            _currentSpeed = MathF.Min(MaxTuckSpeed, _currentSpeed + tuckAccel * dt);
        }
        else if (pitchNorm > 0.20f)
        {
            // Hockey-Stop Snow Drift Braking
            _isTucking = false;
            _isDrifting = true;
            float brakeDecel = 14.5f;
            _currentSpeed = MathF.Max(MinBrakeSpeed, _currentSpeed - brakeDecel * dt);
        }
        else
        {
            _isTucking = false;
            _isDrifting = false;
            _currentSpeed = Mathf.MoveToward(_currentSpeed, CruiseSpeed, 2.2f * dt);
        }

        // 2. Roll Edge Carving Physics
        float gripFactor = (SnowCondition == "ice") ? 0.45f : 1.0f;
        float vCarve = 22.0f * gripFactor;
        float rollInput = boardState.Roll;

        // Edge carving creates lateral velocity
        _skierX += rollInput * vCarve * dt;

        // Carve edge-bite friction drag
        float carveDrag = 3.5f * (rollInput * rollInput);
        _currentSpeed = MathF.Max(MinBrakeSpeed, _currentSpeed - carveDrag * dt);

        // Clamp to piste width
        float pisteLimit = PisteHalfWidth - 1.2f;
        _skierX = Math.Clamp(_skierX, -pisteLimit, pisteLimit);

        if (_currentSpeed > _topSpeed) _topSpeed = _currentSpeed;

        // 3. Airborne Jump State
        if (_isAirborne)
        {
            _verticalVel -= 24.0f * dt; // gravity
            _skierY += _verticalVel * dt;

            if (_skierY <= 0f)
            {
                // Landed!
                _skierY = 0f;
                _isAirborne = false;
                _verticalVel = 0f;

                // Clean landing check: |roll| < 5 deg
                float rollDeg = MathF.Abs(boardState.FusedRollDeg);
                if (boardState.Source != "atoms3") rollDeg = MathF.Abs(boardState.Roll * 28.0f);

                if (rollDeg < 5.0f)
                {
                    int jumpBonus = (int)(500 * _multiplier);
                    AddScore(jumpBonus);
                    Hud?.ShowBannerMessage($"✨ PERFECT JUMP LANDING! +{jumpBonus} PTS", 1.5f);
                }
            }
        }

        // Apply Skier Model Position & Banking
        if (_skierNode != null)
        {
            _skierNode.Position = new Vector3(_skierX, _skierY, 0);

            if (_skierMeshContainer != null)
            {
                float bankRoll = -rollInput * 0.45f;
                float tuckPitch = _isTucking ? -0.35f : (_isDrifting ? 0.25f : 0f);
                float driftYaw = _isDrifting ? (rollInput >= 0 ? 0.45f : -0.45f) : 0f;

                _skierMeshContainer.Rotation = new Vector3(tuckPitch, driftYaw, bankRoll);
            }
        }

        // Camera Follow
        if (_camera != null)
        {
            Vector3 targetCam = new Vector3(_skierX * 0.4f, 3.2f + _skierY * 0.5f, 7.2f);
            _camera.Position = _camera.Position.Lerp(targetCam, 8.0f * dt);
        }

        // 4. Track Progression & Slab Scrolling
        _distanceRun += _currentSpeed * dt;
        AddScore((int)(_currentSpeed * dt * 2.0f * _multiplier));

        float totalSpan = SlabsCount * SegmentLength;
        for (int i = 0; i < _pisteSlabs.Count; i++)
        {
            var slab = _pisteSlabs[i];
            float newZ = slab.Position.Z + _currentSpeed * dt;
            if (newZ > SegmentLength) newZ -= totalSpan;
            slab.Position = new Vector3(0, 0, newZ);
        }

        // 5. Procedural Gates & Features Spawning
        if (_distanceRun >= _gateSpawnDistance)
        {
            SpawnCourseWave();
            float gateSpacing = CoursePreset switch
            {
                "slalom" => 26.0f + _rng.NextFloat() * 6.0f,
                "downhill" => 55.0f + _rng.NextFloat() * 15.0f,
                _ => 38.0f + _rng.NextFloat() * 8.0f // giant_slalom
            };
            _gateSpawnDistance = _distanceRun + gateSpacing;
        }

        // 6. Update Slalom Gates (Crossing & Penalties)
        UpdateGates(dt);

        // 7. Update Features (Ice Sheets, Kickers, Trees)
        UpdateFeatures(dt);
    }

    private void SpawnCourseWave()
    {
        float spawnZ = -SegmentLength * 1.3f;
        bool isRed = (_gatesCleared + _gatesMissed) % 2 == 0;

        float maxOffset = CoursePreset switch
        {
            "slalom" => 6.5f,
            "downhill" => 3.5f,
            _ => 8.2f // giant_slalom
        };

        float gateX = ((_rng.NextFloat() * 2.0f - 1.0f) * maxOffset);
        float gateHalfW = CoursePreset == "slalom" ? 1.8f : 2.2f;

        SpawnSlalomGate(gateX, spawnZ, gateHalfW, isRed);

        // Occasional Ice Patch or Jump Kicker
        float featureRoll = _rng.NextFloat();
        if (featureRoll < 0.25f && CoursePreset != "downhill")
        {
            SpawnIcePatch((_rng.NextFloat() * 2.0f - 1.0f) * 6.0f, spawnZ - 18.0f);
        }
        else if (featureRoll > 0.75f || CoursePreset == "downhill")
        {
            SpawnJumpKicker((_rng.NextFloat() * 2.0f - 1.0f) * 5.0f, spawnZ - 22.0f);
        }

        // Roadside Pine Trees
        for (int side = -1; side <= 1; side += 2)
        {
            float treeX = side * (PisteHalfWidth + 2.5f + _rng.NextFloat() * 4.0f);
            SpawnPineTree(treeX, spawnZ + _rng.NextFloat() * 10.0f);
        }
    }

    private void SpawnSlalomGate(float x, float z, float halfW, bool isRed)
    {
        var node = new Node3D();
        _pisteContainer?.AddChild(node);
        node.Position = new Vector3(x, 0, z);

        Color flagCol = isRed ? new Color(0.95f, 0.15f, 0.2f) : new Color(0.15f, 0.45f, 0.95f);
        var poleMat = new StandardMaterial3D { AlbedoColor = flagCol, Metallic = 0.5f };

        // Left pole
        var leftPole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 2.4f },
            Position = new Vector3(-halfW, 1.2f, 0),
            MaterialOverride = poleMat
        };
        node.AddChild(leftPole);

        // Right pole
        var rightPole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 2.4f },
            Position = new Vector3(halfW, 1.2f, 0),
            MaterialOverride = poleMat
        };
        node.AddChild(rightPole);

        // Connecting cloth flag banner
        var bannerMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(halfW * 2.0f, 0.55f, 0.05f) },
            Position = new Vector3(0, 1.8f, 0)
        };
        var bannerMat = new StandardMaterial3D
        {
            AlbedoColor = flagCol,
            EmissionEnabled = true,
            Emission = flagCol * 0.8f
        };
        bannerMesh.MaterialOverride = bannerMat;
        node.AddChild(bannerMesh);

        _activeGates.Add(new SlalomGate(node, leftPole, rightPole, bannerMesh, x, z, halfW, isRed));
    }

    private void SpawnIcePatch(float x, float z)
    {
        var node = new Node3D();
        _pisteContainer?.AddChild(node);
        node.Position = new Vector3(x, 0.02f, z);

        var iceMesh = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(4.5f, 10.0f) }
        };
        var iceMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.88f, 1.0f),
            Roughness = 0.05f,
            Metallic = 0.6f
        };
        iceMesh.MaterialOverride = iceMat;
        node.AddChild(iceMesh);

        _activeFeatures.Add(new PisteFeature(node, x, z, 3.2f, FeatureKind.IcePatch));
    }

    private void SpawnJumpKicker(float x, float z)
    {
        var node = new Node3D();
        _pisteContainer?.AddChild(node);
        node.Position = new Vector3(x, 0.35f, z);

        var kickerMesh = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(3.5f, 0.7f, 3.0f) },
            RotationDegrees = new Vector3(0, 180, 0)
        };
        var kickerMat = new StandardMaterial3D { AlbedoColor = new Color(1.0f, 0.85f, 0.2f) };
        kickerMesh.MaterialOverride = kickerMat;
        node.AddChild(kickerMesh);

        _activeFeatures.Add(new PisteFeature(node, x, z, 2.2f, FeatureKind.JumpKicker));
    }

    private void SpawnPineTree(float x, float z)
    {
        var node = new Node3D();
        _pisteContainer?.AddChild(node);
        node.Position = new Vector3(x, 0, z);

        // Canopy Cone
        var cone = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 1.4f, Height = 3.5f },
            Position = new Vector3(0, 2.4f, 0)
        };
        var pineMat = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.32f, 0.18f), Roughness = 0.8f };
        cone.MaterialOverride = pineMat;
        node.AddChild(cone);

        // Trunk
        var trunk = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 1.2f },
            Position = new Vector3(0, 0.6f, 0)
        };
        var woodMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.22f, 0.15f) };
        trunk.MaterialOverride = woodMat;
        node.AddChild(trunk);

        _activeFeatures.Add(new PisteFeature(node, x, z, 1.5f, FeatureKind.PineTree));
    }

    private void UpdateGates(float dt)
    {
        for (int i = _activeGates.Count - 1; i >= 0; i--)
        {
            var gate = _activeGates[i];
            gate.Z += _currentSpeed * dt;
            gate.Node.Position = new Vector3(gate.CenterX, 0, gate.Z);

            // Gate Passage Check near Z ~ 0
            if (!gate.Cleared && !gate.Missed && gate.Z >= -0.6f && gate.Z <= 1.2f)
            {
                float dx = MathF.Abs(_skierX - gate.CenterX);
                if (dx <= gate.HalfWidth)
                {
                    // Gate Cleared!
                    gate.Cleared = true;
                    _gatesCleared++;
                    _multiplier = MathF.Min(5.0f, _multiplier + 0.2f);
                    int pts = (int)(500 * _multiplier);
                    AddScore(pts);

                    // Flex poles outward
                    gate.LeftPole.RotationDegrees = new Vector3(0, 0, 18.0f);
                    gate.RightPole.RotationDegrees = new Vector3(0, 0, -18.0f);

                    Hud?.ShowBannerMessage($"🚩 GATE CLEARED! +{pts} (x{_multiplier:F1})", 0.8f);
                }
            }

            // Missed gate check
            if (!gate.Cleared && !gate.Missed && gate.Z > 1.2f)
            {
                gate.Missed = true;
                _gatesMissed++;
                _multiplier = 1.0f;
                _currentSpeed = MathF.Max(MinBrakeSpeed, _currentSpeed - 4.5f);
                Hud?.ShowBannerMessage("❌ GATE MISSED! PENALTY (-15 KM/H)", 1.2f);
            }

            // Recycle offscreen
            if (gate.Z > 15.0f)
            {
                gate.Node.QueueFree();
                _activeGates.RemoveAt(i);
            }
        }
    }

    private void UpdateFeatures(float dt)
    {
        for (int i = _activeFeatures.Count - 1; i >= 0; i--)
        {
            var feat = _activeFeatures[i];
            feat.Z += _currentSpeed * dt;
            feat.Node.Position = new Vector3(feat.X, feat.Node.Position.Y, feat.Z);

            // Collision / Trigger near Z ~ 0
            if (feat.Z >= -1.0f && feat.Z <= 1.0f)
            {
                float dx = MathF.Abs(_skierX - feat.X);
                if (dx < feat.Radius)
                {
                    if (feat.Kind == FeatureKind.JumpKicker && !_isAirborne)
                    {
                        // Launch airborne jump
                        _isAirborne = true;
                        _verticalVel = 7.5f;
                        Hud?.ShowBannerMessage("🚀 KICKER LAUNCH! HOLD LEVEL FOR LANDING!", 1.5f);
                    }
                    else if (feat.Kind == FeatureKind.PineTree)
                    {
                        // Crash into tree
                        OnPlayerCrash("Smashed into Alpine Pine Tree!");
                        return;
                    }
                }
            }

            // Recycle offscreen
            if (feat.Z > 15.0f)
            {
                feat.Node.QueueFree();
                _activeFeatures.RemoveAt(i);
            }
        }
    }

    private void OnPlayerCrash(string reason)
    {
        TriggerGameOver(reason);
        string player = LeaderboardService.Instance.ActivePlayerName;
        _latestRankAchieved = LeaderboardService.Instance.RecordScore(player, CurrentScore, _gatesCleared, (int)_topSpeed);
        Hud?.ShowBannerMessage($"💥 {reason.ToUpperInvariant()} ({CurrentScore:N0} PTS)", 3.5f);
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["speedKmh"] = (int)(_currentSpeed * 3.6f);
        _liveStatsCache["topSpeedKmh"] = (int)(_topSpeed * 3.6f);
        _liveStatsCache["distance"] = (int)_distanceRun;
        _liveStatsCache["gatesCleared"] = _gatesCleared;
        _liveStatsCache["gatesMissed"] = _gatesMissed;
        _liveStatsCache["multiplier"] = _multiplier;
        _liveStatsCache["courseType"] = CoursePreset;
        _liveStatsCache["snowCondition"] = SnowCondition;
        _liveStatsCache["isTucking"] = _isTucking;
        _liveStatsCache["isAirborne"] = _isAirborne;
        _liveStatsCache["latestRank"] = _latestRankAchieved;

        return _liveStatsCache;
    }
}
