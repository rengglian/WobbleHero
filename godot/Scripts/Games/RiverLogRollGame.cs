using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: River Log Roll (White-Water Rapids).
/// The player balances atop a cylindrical timber log spinning downstream in a rushing
/// mountain river canyon. Pitch tilt counters water drag torque to stay in the
/// rotational "sweet spot" (±4°), while Roll steers laterally across the 18m rapids
/// to dodge granite boulders, escape whirlpool eddies, and catch Golden Salmon.
/// </summary>
public partial class RiverLogRollGame : BaseGameMode
{
    public override string GameId => "RiverLogRoll";
    public override string GameTitle => "River Log Roll";
    public override string GameDescription => "Balance atop a spinning timber log in rushing white-water rapids! Match river torque to birle in the sweet spot and dodge river boulders.";
    public override string GameIcon => "🪵";

    // River Dimensions
    public const float RiverHalfWidth = 9.0f; // 18m canyon width
    public const float SegmentLength = 90.0f;
    public const int SlabsCount = 3;

    // Log & Birling Constants
    public const float LogRadius = 0.65f;
    public const float LogLength = 4.2f;
    public const float LogInertia = 52.8f; // kg * m^2
    public const float RiderMass = 75.0f; // kg
    public const float Gravity = 9.81f;

    // Difficulty Presets
    [Export] public string DifficultyPreset { get; set; } = "rapids_class_3";
    [Export] public int Seed { get; set; } = 8192;

    // River Hazard / Collectible Entity
    public enum RiverEntityKind { GraniteBoulder, Whirlpool, GoldenSalmon, Driftwood }
    private sealed class RiverEntity
    {
        public Node3D Node { get; }
        public float X;
        public float Z;
        public float Radius;
        public RiverEntityKind Kind;
        public bool Cleared;

        public RiverEntity(Node3D node, float x, float z, float radius, RiverEntityKind kind)
        {
            Node = node;
            X = x;
            Z = z;
            Radius = radius;
            Kind = kind;
            Cleared = false;
        }
    }

    // Node hierarchy
    private Node3D? _riverContainer;
    private Node3D? _logRiderNode;
    private MeshInstance3D? _timberLogMesh;
    private Node3D? _athleteMesh;
    private Camera3D? _camera;
    private readonly List<MeshInstance3D> _waterSlabs = new(SlabsCount);

    // Physics & Birling State
    private float _logX = 0f;
    private float _logY = 0f;
    private float _logSpinRpm = 0f; // Revolutions per minute
    private float _logRotAngleRad = 0f;
    private float _riverCurrentSpeed = 6.67f; // m/s (24 km/h default)
    private float _targetPitchDeg = 0f;
    private float _balanceErrorDeg = 0f;
    private bool _isInSweetSpot = false;
    private float _distanceRun = 0f;
    private int _playerLives = 3;
    private float _invulnTimer = 0f;
    private bool _isSplashedDown = false;

    // Match Progression
    private float _multiplier = 1.0f;
    private int _salmonCollected = 0;
    private int _latestRankAchieved = -1;
    private float _obstacleSpawnDistance = 35.0f;

    // Pre-allocated entity lists & telemetry
    private readonly List<RiverEntity> _activeEntities = new(24);
    private readonly Dictionary<string, object> _liveStatsCache = new(16);
    private readonly List<object> _cachedObstaclesList = new(24);
    private Mulberry32 _rng;

    public struct Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed == 0 ? 8192u : seed;
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
        ApplyDifficulty(DifficultyPreset);
        ResetGame();
    }

    public void SetDifficulty(string diff)
    {
        DifficultyPreset = diff?.ToLowerInvariant() ?? "rapids_class_3";
        ApplyDifficulty(DifficultyPreset);
        ResetGame();
    }

    private void ApplyDifficulty(string diff)
    {
        _riverCurrentSpeed = diff switch
        {
            "calm_creek" => 4.17f,          // 15 km/h
            "white_water_class_5" => 9.72f, // 35 km/h
            _ => 6.67f                      // 24 km/h (rapids_class_3)
        };
    }

    private void BuildSceneNodes()
    {
        // 1. Riverbed & White-Water Slabs
        _riverContainer = new Node3D { Name = "RiverContainer" };
        AddChild(_riverContainer);

        var waterMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.45f, 0.65f, 0.92f),
            Roughness = 0.1f,
            Metallic = 0.3f,
            EmissionEnabled = true,
            Emission = new Color(0.12f, 0.55f, 0.75f) * 0.45f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        for (int i = 0; i < SlabsCount; i++)
        {
            var slab = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(RiverHalfWidth * 2.0f, SegmentLength) },
                MaterialOverride = waterMat,
                Position = new Vector3(0, 0, -i * SegmentLength)
            };
            _riverContainer.AddChild(slab);
            _waterSlabs.Add(slab);
        }

        // Canyon Rock Walls
        BuildCanyonWalls();

        // 2. Timber Log & Athlete Node
        _logRiderNode = new Node3D { Name = "LogRider" };
        AddChild(_logRiderNode);

        // Cylindrical Timber Log
        _timberLogMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = LogRadius, BottomRadius = LogRadius, Height = LogLength, RadialSegments = 24 },
            RotationDegrees = new Vector3(0, 0, 90.0f),
            Position = new Vector3(0, LogRadius * 0.5f, 0)
        };
        var barkMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.38f, 0.24f, 0.14f),
            Roughness = 0.85f,
            Metallic = 0.1f
        };
        _timberLogMesh.MaterialOverride = barkMat;
        _logRiderNode.AddChild(_timberLogMesh);

        // Athlete Character on top of log
        _athleteMesh = new Node3D { Name = "Athlete", Position = new Vector3(0, LogRadius * 1.5f, 0) };
        _logRiderNode.AddChild(_athleteMesh);

        // Athlete Torso (Flannel shirt)
        var torso = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.9f, 0.4f) },
            Position = new Vector3(0, 0.45f, 0)
        };
        var flannelMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.15f, 0.15f) };
        torso.MaterialOverride = flannelMat;
        _athleteMesh.AddChild(torso);

        // Athlete Head & Toque Cap
        var head = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.28f, Height = 0.56f },
            Position = new Vector3(0, 1.15f, 0)
        };
        var toqueMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.12f, 0.15f) };
        head.MaterialOverride = toqueMat;
        _athleteMesh.AddChild(head);

        // Outstretched Balance Arms / Pole
        var balancePole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 3.2f },
            RotationDegrees = new Vector3(0, 0, 90.0f),
            Position = new Vector3(0, 0.7f, 0)
        };
        var poleMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.75f, 0.35f) };
        balancePole.MaterialOverride = poleMat;
        _athleteMesh.AddChild(balancePole);

        // 3. Dynamic Third-Person Chase Camera
        _camera = new Camera3D
        {
            Name = "ChaseCamera",
            Fov = 66.0f,
            Position = new Vector3(0, 4.2f, 8.5f),
            RotationDegrees = new Vector3(-18.0f, 0, 0),
            Current = true
        };
        AddChild(_camera);

        // River Canyon Sunlight
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55.0f, 40.0f, 0),
            LightColor = new Color(0.98f, 0.96f, 0.92f),
            LightEnergy = 1.3f
        };
        AddChild(sun);
    }

    private void BuildCanyonWalls()
    {
        var rockMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.24f, 0.22f),
            Roughness = 0.95f
        };

        for (int side = -1; side <= 1; side += 2)
        {
            var wall = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(4.0f, 6.0f, SegmentLength * SlabsCount) },
                Position = new Vector3(side * (RiverHalfWidth + 2.0f), 2.5f, -SegmentLength),
                MaterialOverride = rockMat
            };
            _riverContainer?.AddChild(wall);
        }
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _rng = new Mulberry32((uint)Math.Abs(Seed));
        _logX = 0f;
        _logY = 0f;
        _logSpinRpm = 45.0f;
        _logRotAngleRad = 0f;
        _distanceRun = 0f;
        _playerLives = 3;
        _invulnTimer = 0f;
        _isSplashedDown = false;
        _multiplier = 1.0f;
        _salmonCollected = 0;
        _latestRankAchieved = -1;
        _obstacleSpawnDistance = 35.0f;

        // Clear active entities
        foreach (var e in _activeEntities) e.Node.QueueFree();
        _activeEntities.Clear();

        if (_logRiderNode != null)
        {
            _logRiderNode.Position = Vector3.Zero;
            _logRiderNode.Visible = true;
        }

        string diffName = DifficultyPreset.Replace('_', ' ').ToUpperInvariant();
        Hud?.ShowBannerMessage($"RIVER RAPIDS: {diffName} • HOLD BALANCE IN SWEET SPOT!", 2.8f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.05f || MathF.Abs(boardState.Pitch) > 0.05f))
        {
            StartGame();
            Hud?.ShowBannerMessage("RAPIDS ENGAGED! KEEP THE LOG BALANCED!", 1.5f);
        }

        if (GameState != "RUNNING") return;

        // 1. Birling Rotational Dynamics & Equilibrium Equation
        // tau_water = K_water * (v_river - omega * R)
        // tau_rider = m * g * R * sin(pitch)
        float pitchDeg = boardState.FusedPitchDeg;
        if (boardState.Source != "atoms3") pitchDeg = boardState.Pitch * 28.0f;

        float omegaLog = (_logSpinRpm * (MathF.Tau / 60.0f));
        float tauWater = 28.0f * (_riverCurrentSpeed - omegaLog * LogRadius);

        float pitchRad = pitchDeg * (MathF.PI / 180.0f);
        float tauRider = RiderMass * Gravity * LogRadius * MathF.Sin(pitchRad);

        float netTorque = tauWater - tauRider;
        float dOmega = (netTorque / LogInertia) * dt;
        omegaLog = Math.Max(0.5f, omegaLog + dOmega);
        _logSpinRpm = omegaLog * (60.0f / MathF.Tau);

        // Continuous spin rotation
        _logRotAngleRad += omegaLog * dt;
        if (_timberLogMesh != null)
        {
            _timberLogMesh.Rotation = new Vector3(_logRotAngleRad, 0, (MathF.PI / 2.0f));
        }

        // Target pitch needed to perfectly balance current river torque:
        // sin(targetPitch) = tau_water / (m * g * R)
        float sinTarget = Math.Clamp(tauWater / (RiderMass * Gravity * LogRadius), -0.7f, 0.7f);
        _targetPitchDeg = MathF.Asin(sinTarget) * (180.0f / MathF.PI);

        _balanceErrorDeg = pitchDeg - _targetPitchDeg;

        // Sweet Spot Evaluation: |error| <= sweetSpotLimit
        float sweetSpotLimit = DifficultyPreset switch
        {
            "calm_creek" => 5.5f,
            "white_water_class_5" => 2.8f,
            _ => 4.0f // rapids_class_3
        };

        _isInSweetSpot = MathF.Abs(_balanceErrorDeg) <= sweetSpotLimit;

        if (_isInSweetSpot)
        {
            // Building multiplier
            _multiplier = MathF.Min(5.0f, _multiplier + 0.15f * dt);
            AddScore((int)(80.0f * _multiplier * dt));
        }
        else
        {
            // Slipping: multiplier decays
            _multiplier = Mathf.MoveToward(_multiplier, 1.0f, 0.6f * dt);

            // Over-lean check: > 18 deg error causes splash-down slip!
            if (MathF.Abs(_balanceErrorDeg) > 18.0f && _invulnTimer <= 0f)
            {
                OnPlayerSplashDown("Lost Footing on Spinning Log! Slid into Rapids!");
                return;
            }
        }

        // 2. Lateral Roll Steering across River
        float steerSpeed = 16.0f;
        float rollInput = boardState.Roll;
        _logX += rollInput * steerSpeed * dt;

        // Piste boundary limit
        float riverLimit = RiverHalfWidth - (LogLength * 0.5f);
        _logX = Math.Clamp(_logX, -riverLimit, riverLimit);

        // Apply Log Rider Transform & Athlete Animation
        if (_logRiderNode != null)
        {
            _logRiderNode.Position = new Vector3(_logX, _logY, 0);

            if (_athleteMesh != null)
            {
                // Wobble tilt based on balance error
                float athleteTilt = -(_balanceErrorDeg * (MathF.PI / 180.0f)) * 0.65f;
                _athleteMesh.Rotation = new Vector3(athleteTilt, 0, -rollInput * 0.35f);
            }
        }

        // Camera Follow
        if (_camera != null)
        {
            Vector3 targetCam = new Vector3(_logX * 0.35f, 4.2f, 8.5f);
            _camera.Position = _camera.Position.Lerp(targetCam, 7.0f * dt);
        }

        // 3. Track Progression & Slab Scrolling
        _distanceRun += _riverCurrentSpeed * dt;
        AddScore((int)(_riverCurrentSpeed * dt * 2.0f * _multiplier));

        float totalSpan = SlabsCount * SegmentLength;
        for (int i = 0; i < _waterSlabs.Count; i++)
        {
            var slab = _waterSlabs[i];
            float newZ = slab.Position.Z + _riverCurrentSpeed * dt;
            if (newZ > SegmentLength) newZ -= totalSpan;
            slab.Position = new Vector3(0, 0, newZ);
        }

        // 4. Procedural River Hazards & Fish Spawning
        if (_distanceRun >= _obstacleSpawnDistance)
        {
            SpawnRiverWave();
            float spacing = DifficultyPreset switch
            {
                "white_water_class_5" => 28.0f + _rng.NextFloat() * 10.0f,
                "calm_creek" => 48.0f + _rng.NextFloat() * 16.0f,
                _ => 36.0f + _rng.NextFloat() * 12.0f
            };
            _obstacleSpawnDistance = _distanceRun + spacing;
        }

        // 5. Update Hazards & Pickups
        UpdateRiverEntities(dt);

        if (_invulnTimer > 0f) _invulnTimer -= dt;
    }

    private void SpawnRiverWave()
    {
        float spawnZ = -SegmentLength * 1.3f;
        float roll = _rng.NextFloat();

        if (roll < 0.45f)
        {
            // Granite River Boulder
            float boulderX = (_rng.NextFloat() * 2.0f - 1.0f) * (RiverHalfWidth - 3.0f);
            SpawnGraniteBoulder(boulderX, spawnZ);
        }
        else if (roll < 0.70f)
        {
            // Swirling Whirlpool Eddy
            float poolX = (_rng.NextFloat() * 2.0f - 1.0f) * (RiverHalfWidth - 4.5f);
            SpawnWhirlpool(poolX, spawnZ);
        }
        else
        {
            // Jumping Golden Salmon
            float salmonX = (_rng.NextFloat() * 2.0f - 1.0f) * (RiverHalfWidth - 3.5f);
            SpawnGoldenSalmon(salmonX, spawnZ);
        }
    }

    private void SpawnGraniteBoulder(float x, float z)
    {
        var node = new Node3D();
        _riverContainer?.AddChild(node);
        node.Position = new Vector3(x, 0.4f, z);

        var rockMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.45f, Height = 2.4f, RadialSegments = 10, Rings = 8 }
        };
        var rockMat = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.33f, 0.36f), Roughness = 0.9f };
        rockMesh.MaterialOverride = rockMat;
        node.AddChild(rockMesh);

        _activeEntities.Add(new RiverEntity(node, x, z, 1.8f, RiverEntityKind.GraniteBoulder));
    }

    private void SpawnWhirlpool(float x, float z)
    {
        var node = new Node3D();
        _riverContainer?.AddChild(node);
        node.Position = new Vector3(x, 0.05f, z);

        var poolMesh = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 1.8f, OuterRadius = 3.2f }
        };
        var foamMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.95f, 1.0f, 0.7f),
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.8f, 1.0f) * 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        poolMesh.MaterialOverride = foamMat;
        node.AddChild(poolMesh);

        _activeEntities.Add(new RiverEntity(node, x, z, 3.4f, RiverEntityKind.Whirlpool));
    }

    private void SpawnGoldenSalmon(float x, float z)
    {
        var node = new Node3D();
        _riverContainer?.AddChild(node);
        node.Position = new Vector3(x, 0.8f, z);

        var fishMesh = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.1f },
            RotationDegrees = new Vector3(0, 0, 75.0f)
        };
        var goldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.82f, 0.15f) * 1.5f,
            Metallic = 0.85f,
            Roughness = 0.2f
        };
        fishMesh.MaterialOverride = goldMat;
        node.AddChild(fishMesh);

        _activeEntities.Add(new RiverEntity(node, x, z, 1.6f, RiverEntityKind.GoldenSalmon));
    }

    private void UpdateRiverEntities(float dt)
    {
        for (int i = _activeEntities.Count - 1; i >= 0; i--)
        {
            var ent = _activeEntities[i];
            ent.Z += _riverCurrentSpeed * dt;
            ent.Node.Position = new Vector3(ent.X, ent.Node.Position.Y, ent.Z);

            if (ent.Kind == RiverEntityKind.Whirlpool)
            {
                ent.Node.RotateY(dt * 4.5f);

                // Whirlpool suction pull
                float dx = ent.X - _logX;
                float dz = ent.Z;
                float dist = MathF.Sqrt(dx * dx + dz * dz);

                if (dist < ent.Radius && dist > 0.01f)
                {
                    float pull = (1.0f - dist / ent.Radius) * 6.5f;
                    _logX += (dx / dist) * pull * dt;
                }
            }
            else if (ent.Kind == RiverEntityKind.GoldenSalmon)
            {
                ent.Node.RotateY(dt * 3.5f);
            }

            // Collision check near Z ~ 0
            if (!ent.Cleared && ent.Z >= -1.2f && ent.Z <= 1.2f)
            {
                float dx = MathF.Abs(_logX - ent.X);
                if (dx < ent.Radius)
                {
                    if (ent.Kind == RiverEntityKind.GraniteBoulder && _invulnTimer <= 0f)
                    {
                        OnPlayerSplashDown("Crushed by Granite Boulder!");
                        return;
                    }
                    else if (ent.Kind == RiverEntityKind.GoldenSalmon)
                    {
                        ent.Cleared = true;
                        _salmonCollected++;
                        _multiplier = MathF.Min(5.0f, _multiplier + 0.3f);
                        int pts = (int)(1000 * _multiplier);
                        AddScore(pts);

                        Hud?.ShowBannerMessage($"🐟 GOLDEN SALMON CAUGHT! +{pts} (x{_multiplier:F1})", 1.2f);
                        ent.Node.QueueFree();
                        _activeEntities.RemoveAt(i);
                        continue;
                    }
                }
            }

            // Recycle offscreen
            if (ent.Z > 15.0f)
            {
                ent.Node.QueueFree();
                _activeEntities.RemoveAt(i);
            }
        }
    }

    private void OnPlayerSplashDown(string reason)
    {
        _playerLives--;
        _multiplier = 1.0f;
        _invulnTimer = 2.2f;

        if (_playerLives > 0)
        {
            _logX = 0f;
            Hud?.ShowBannerMessage($"🌊 SPLASH DOWN! {_playerLives} LIVES REMAINING", 2.2f);
        }
        else
        {
            _isSplashedDown = true;
            TriggerGameOver(reason);

            string player = LeaderboardService.Instance.ActivePlayerName;
            _latestRankAchieved = LeaderboardService.Instance.RecordScore(player, CurrentScore, _salmonCollected, (int)_distanceRun);
            Hud?.ShowBannerMessage($"💀 SWEPT AWAY BY RAPIDS! ({CurrentScore:N0} PTS)", 3.5f);
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["riverSpeedKmh"] = (int)(_riverCurrentSpeed * 3.6f);
        _liveStatsCache["riverSpeed"] = (int)(_riverCurrentSpeed * 3.6f);
        _liveStatsCache["logRpm"] = (int)_logSpinRpm;
        _liveStatsCache["balanceErrorDeg"] = _balanceErrorDeg;
        _liveStatsCache["equilibriumDev"] = _balanceErrorDeg;
        _liveStatsCache["targetPitchDeg"] = _targetPitchDeg;
        _liveStatsCache["isInSweetSpot"] = _isInSweetSpot;
        _liveStatsCache["isSlipping"] = !_isInSweetSpot;
        _liveStatsCache["multiplier"] = _multiplier;
        _liveStatsCache["distance"] = (int)_distanceRun;
        _liveStatsCache["salmonCollected"] = _salmonCollected;
        _liveStatsCache["lives"] = _playerLives;
        _liveStatsCache["difficulty"] = DifficultyPreset;
        _liveStatsCache["latestRank"] = _latestRankAchieved;

        // Obstacles for 2D companion radar
        _cachedObstaclesList.Clear();
        for (int i = 0; i < _activeEntities.Count; i++)
        {
            var e = _activeEntities[i];
            string typeStr = e.Kind switch
            {
                RiverEntityKind.GraniteBoulder => "boulder",
                RiverEntityKind.Whirlpool => "whirlpool",
                RiverEntityKind.GoldenSalmon => "salmon",
                _ => "driftwood"
            };
            _cachedObstaclesList.Add(new { type = typeStr, x = e.X / RiverHalfWidth, y = -e.Z / SegmentLength });
        }
        _liveStatsCache["obstacles"] = _cachedObstaclesList;

        return _liveStatsCache;
    }
}
