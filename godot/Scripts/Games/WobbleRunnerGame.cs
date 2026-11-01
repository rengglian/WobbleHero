using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 2: Wobble Runner (Endless Ride Approach).
/// High-speed procedural 3-lane slalom runner with deterministic seed control,
/// double-obstacle slalom waves (with guaranteed open escape lanes),
/// collectible Apples and Golden Horseshoes, distance-based speed scaling,
/// and persistent top-10 leaderboard integration.
/// </summary>
public partial class WobbleRunnerGame : BaseGameMode
{
    public override string GameId => "WobbleRunner";
    public override string GameTitle => "Wobble Runner";
    public override string GameDescription => "High-speed 3-lane slalom runner! Dodge obstacles and collect golden horseshoes!";
    public override string GameIcon => "🏃";

    [Export] public float TrackWidth { get; set; } = 12.0f;
    [Export] public float BaseSpeed { get; set; } = 18.0f;
    [Export] public float MaxSpeed { get; set; } = 34.0f;
    [Export] public int Seed { get; set; } = 1337;

    public const float LaneLeft = -3.6f;
    public const float LaneCenter = 0.0f;
    public const float LaneRight = 3.6f;

    // Node hierarchy
    private Node3D? _trackContainer;
    private Node3D? _playerNode;
    private Node3D? _boardMesh;
    private readonly List<MeshInstance3D> _trackSlabs = new();

    // Gameplay state
    private float _playerX;
    private float _distanceRun;
    private float _currentSpeed;
    private float _waveSpawnDistance;
    private float _multiplier = 1.0f;
    private int _applesCollected;
    private int _horseshoesCollected;
    private int _latestRankAchieved = -1;

    // PRNG
    private Mulberry32 _rng;

    // Active track entities
    private readonly List<TrackEntity> _activeEntities = new();

    private enum EntityKind { Rock, Barricade, Hurdle, Horseshoe, Apple }
    private record TrackEntity(Node3D Node, float Z, float CenterX, EntityKind Kind, bool Cleared, float Radius);

    public struct Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed == 0 ? 1337u : seed;
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

    private void BuildSceneNodes()
    {
        // 1. Track container
        _trackContainer = new Node3D { Name = "Track" };
        AddChild(_trackContainer);

        // Infinite rolling ground track slabs (2 alternating slabs of 100m length)
        for (int i = 0; i < 3; i++)
        {
            var slab = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(TrackWidth, 80.0f) },
                Position = new Vector3(0, 0, -40.0f - i * 80.0f)
            };
            var floorMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.07f, 0.10f, 0.16f),
                Roughness = 0.5f
            };
            slab.MaterialOverride = floorMat;
            _trackContainer.AddChild(slab);
            _trackSlabs.Add(slab);
        }

        // 3-lane visual dashes & boundary neon guardrails
        for (int lane = -1; lane <= 1; lane += 2)
        {
            // Rail
            var rail = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.7f, 240.0f) },
                Position = new Vector3(lane * (TrackWidth * 0.5f), 0.35f, -100.0f)
            };
            var railMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.22f, 0.74f, 0.97f),
                EmissionEnabled = true,
                Emission = new Color(0.22f, 0.74f, 0.97f) * 0.6f
            };
            rail.MaterialOverride = railMat;
            _trackContainer.AddChild(rail);

            // Lane dividing stripe
            var stripe = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.02f, 240.0f) },
                Position = new Vector3(lane * 1.8f, 0.02f, -100.0f)
            };
            var stripeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            stripe.MaterialOverride = stripeMat;
            _trackContainer.AddChild(stripe);
        }

        // 2. Player Hoverboard
        _playerNode = new Node3D { Name = "Player" };
        AddChild(_playerNode);

        _boardMesh = new Node3D { Name = "BoardMesh" };
        _playerNode.AddChild(_boardMesh);

        var deck = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.18f, 2.8f) },
            Position = new Vector3(0, 0.25f, 0)
        };
        var deckMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.45f, 0.15f),
            Roughness = 0.25f,
            Metallic = 0.4f
        };
        deck.MaterialOverride = deckMat;
        _boardMesh.AddChild(deck);

        // Neon underglow
        var glow = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.06f, 2.5f) },
            Position = new Vector3(0, 0.12f, 0)
        };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.74f, 0.97f),
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.74f, 0.97f) * 0.9f
        };
        glow.MaterialOverride = glowMat;
        _boardMesh.AddChild(glow);

        // Rider avatar
        var riderMesh = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.35f },
            Position = new Vector3(0, 1.05f, 0)
        };
        var riderMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.85f, 0.55f),
            Roughness = 0.3f
        };
        riderMesh.MaterialOverride = riderMat;
        _boardMesh.AddChild(riderMesh);

        // 3. Camera & Lighting
        var camera = new Camera3D
        {
            Position = new Vector3(0, 4.8f, 7.8f),
            RotationDegrees = new Vector3(-19, 0, 0)
        };
        AddChild(camera);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-42, 28, 0),
            LightEnergy = 1.25f,
            ShadowEnabled = true
        };
        AddChild(light);
    }

    public void SetSeed(int seed)
    {
        Seed = seed;
        GD.Print($"[WobbleRunnerGame] Seed set to #{Seed}");
        ResetGame();
    }

    public void SetRandomSeed()
    {
        SetSeed((int)(GD.Randi() % 99999) + 1);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _rng = new Mulberry32((uint)Math.Abs(Seed));
        _playerX = 0f;
        _distanceRun = 0f;
        _currentSpeed = BaseSpeed;
        _waveSpawnDistance = 35.0f; // first obstacles appear at 35m
        _multiplier = 1.0f;
        _applesCollected = 0;
        _horseshoesCollected = 0;
        _latestRankAchieved = -1;

        // Clear active entities
        foreach (var e in _activeEntities)
        {
            e.Node.QueueFree();
        }
        _activeEntities.Clear();

        if (_playerNode != null) _playerNode.Position = Vector3.Zero;
        Hud?.ShowBannerMessage($"SEED #{Seed} • LEAN TO BEGIN!", 2.5f);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start run on board tilt
        if (GameState == "START" && MathF.Abs(boardState.Roll) > 0.06f)
        {
            StartGame();
            Hud?.ShowBannerMessage("RUN STARTED!", 1.2f);
        }

        if (GameState != "RUNNING") return;

        // 1. Steering Physics (Dynamic lateral lane navigation)
        float steerSpeed = 16.5f;
        _playerX += boardState.Roll * steerSpeed * dt;
        float limitX = (TrackWidth * 0.5f) - 1.2f;
        _playerX = Math.Clamp(_playerX, -limitX, limitX);

        if (_playerNode != null)
        {
            _playerNode.Position = new Vector3(_playerX, 0, 0);
            if (_boardMesh != null)
            {
                // Banking roll rotation
                _boardMesh.Rotation = new Vector3(0, 0, -boardState.Roll * 0.45f);
            }
        }

        // 2. Progression & Distance
        _distanceRun += _currentSpeed * dt;
        AddScore((int)(_currentSpeed * dt * 2.0f * _multiplier));

        // Speed curve scales up to MaxSpeed as player covers distance
        float progress = Math.Min(1.0f, _distanceRun / 2000.0f);
        _currentSpeed = Mathf.Lerp(BaseSpeed, MaxSpeed, progress);

        // 3. Ground Track Scrolling
        for (int i = 0; i < _trackSlabs.Count; i++)
        {
            var slab = _trackSlabs[i];
            float slabZ = slab.Position.Z + _currentSpeed * dt;
            if (slabZ > 40.0f)
            {
                slabZ -= 240.0f; // loop behind
            }
            slab.Position = new Vector3(0, 0, slabZ);
        }

        // 4. Procedural Wave Spawning (Endless Ride Mulberry32 Algorithm)
        if (_distanceRun >= _waveSpawnDistance)
        {
            SpawnProceduralWave();
            // Wave spacing scales down with distance (from 28m down to 17m for intense slalom!)
            float waveSpacing = 28.0f - 11.0f * progress + _rng.NextFloat() * 5.0f;
            _waveSpawnDistance = _distanceRun + waveSpacing;
        }

        // 5. Update, Collide, and Cull Active Entities
        for (int i = _activeEntities.Count - 1; i >= 0; i--)
        {
            var ent = _activeEntities[i];
            float newZ = ent.Z + _currentSpeed * dt;
            ent.Node.Position = new Vector3(ent.CenterX, ent.Node.Position.Y, newZ);

            // Spin animation for collectibles
            if (ent.Kind == EntityKind.Horseshoe || ent.Kind == EntityKind.Apple)
            {
                ent.Node.RotateY(dt * 3.5f);
            }

            // Check collision when passing player (Z ~ 0)
            if (!ent.Cleared && newZ >= -0.8f && newZ <= 1.2f)
            {
                float dx = MathF.Abs(_playerX - ent.CenterX);
                if (dx < ent.Radius)
                {
                    if (ent.Kind == EntityKind.Rock || ent.Kind == EntityKind.Barricade || ent.Kind == EntityKind.Hurdle)
                    {
                        // CRASH!
                        OnPlayerCrash(ent.Kind);
                        return;
                    }
                    else if (ent.Kind == EntityKind.Horseshoe)
                    {
                        // Golden Horseshoe pickup
                        _horseshoesCollected++;
                        _multiplier = Math.Min(4.0f, _multiplier + 0.3f);
                        int bonus = (int)(150 * _multiplier);
                        AddScore(bonus);
                        Hud?.ShowBannerMessage($"🐴 GOLDEN HORSESHOE! +{bonus} (x{_multiplier:F1})", 1.2f);
                        ent.Node.QueueFree();
                        _activeEntities.RemoveAt(i);
                        continue;
                    }
                    else if (ent.Kind == EntityKind.Apple)
                    {
                        // Apple pickup
                        _applesCollected++;
                        AddScore((int)(50 * _multiplier));
                        _currentSpeed = Math.Min(MaxSpeed + 3f, _currentSpeed + 1.5f);
                        Hud?.ShowBannerMessage($"+50 SPEED BOOST! ({_applesCollected} 🍎)", 0.9f);
                        ent.Node.QueueFree();
                        _activeEntities.RemoveAt(i);
                        continue;
                    }
                }
            }

            // Recycle entities past camera
            if (newZ > 12.0f)
            {
                ent.Node.QueueFree();
                _activeEntities.RemoveAt(i);
            }
            else
            {
                _activeEntities[i] = ent with { Z = newZ };
            }
        }
    }

    private void SpawnProceduralWave()
    {
        if (_trackContainer == null) return;

        float[] lanes = { LaneLeft, LaneCenter, LaneRight };
        float spawnZ = -95.0f;
        float progress = Math.Min(1.0f, _distanceRun / 2000.0f);

        // Double obstacle probability scales from 0.35 up to 0.70
        bool isDouble = _rng.NextFloat() < (0.35f + 0.35f * progress);

        if (!isDouble)
        {
            // Single Obstacle in a random lane
            int laneIdx = _rng.NextInt(0, 3);
            SpawnObstacle(lanes[laneIdx], spawnZ);

            // Spawn collectible reward in one of the other lanes!
            int collectibleLane = (laneIdx + 1 + _rng.NextInt(0, 2)) % 3;
            SpawnCollectible(lanes[collectibleLane], spawnZ - 4.0f);
        }
        else
        {
            // Double Obstacle Wave: EXACTLY 1 OPEN ESCAPE LANE GUARANTEED!
            int openLaneIdx = _rng.NextInt(0, 3);
            for (int l = 0; l < 3; l++)
            {
                if (l != openLaneIdx)
                {
                    SpawnObstacle(lanes[l], spawnZ);
                }
            }

            // High-value reward right in the open escape lane (rewarding slalom precision!)
            SpawnCollectible(lanes[openLaneIdx], spawnZ);
        }
    }

    private void SpawnObstacle(float posX, float spawnZ)
    {
        var node = new Node3D();
        _trackContainer?.AddChild(node);
        node.Position = new Vector3(posX, 0, spawnZ);

        EntityKind kind;
        float rand = _rng.NextFloat();

        if (rand < 0.40f)
        {
            // Rock obstacle
            kind = EntityKind.Rock;
            var rock = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.9f, 1.4f) },
                Position = new Vector3(0, 0.45f, 0)
            };
            var mat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.45f, 0.5f), Roughness = 0.8f };
            rock.MaterialOverride = mat;
            node.AddChild(rock);
        }
        else if (rand < 0.75f)
        {
            // Red Barricade / Pylon
            kind = EntityKind.Barricade;
            var pylon = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.65f, Height = 1.6f },
                Position = new Vector3(0, 0.8f, 0)
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.2f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(0.95f, 0.2f, 0.2f) * 0.7f
            };
            pylon.MaterialOverride = mat;
            node.AddChild(pylon);
        }
        else
        {
            // Hurdle Barrier
            kind = EntityKind.Hurdle;
            var hurdle = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.2f, 1.1f, 0.35f) },
                Position = new Vector3(0, 0.55f, 0)
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.5f, 0.1f),
                EmissionEnabled = true,
                Emission = new Color(0.9f, 0.5f, 0.1f) * 0.4f
            };
            hurdle.MaterialOverride = mat;
            node.AddChild(hurdle);
        }

        _activeEntities.Add(new TrackEntity(node, spawnZ, posX, kind, false, 1.35f));
    }

    private void SpawnCollectible(float posX, float spawnZ)
    {
        var node = new Node3D();
        _trackContainer?.AddChild(node);
        node.Position = new Vector3(posX, 0.8f, spawnZ);

        bool isHorseshoe = _rng.NextFloat() < 0.35f;
        EntityKind kind = isHorseshoe ? EntityKind.Horseshoe : EntityKind.Apple;

        if (isHorseshoe)
        {
            // Golden Horseshoe
            var horseshoeMesh = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.45f, OuterRadius = 0.7f },
                RotationDegrees = new Vector3(45, 0, 0)
            };
            var goldMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1.0f, 0.85f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.85f, 0.15f) * 0.8f,
                Metallic = 0.8f,
                Roughness = 0.2f
            };
            horseshoeMesh.MaterialOverride = goldMat;
            node.AddChild(horseshoeMesh);
        }
        else
        {
            // Glowing Apple / Energy Star
            var appleMesh = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f }
            };
            var appleMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.25f, 0.35f),
                EmissionEnabled = true,
                Emission = new Color(0.95f, 0.25f, 0.35f) * 0.7f
            };
            appleMesh.MaterialOverride = appleMat;
            node.AddChild(appleMesh);
        }

        _activeEntities.Add(new TrackEntity(node, spawnZ, posX, kind, false, 1.45f));
    }

    private void OnPlayerCrash(EntityKind obstacleKind)
    {
        string obstacleName = obstacleKind.ToString().ToUpperInvariant();
        TriggerGameOver($"Crashed into {obstacleName}!");

        // Record run to persistent leaderboard
        string playerName = LeaderboardService.Instance.ActivePlayerName;
        int rank = LeaderboardService.Instance.RecordScore(playerName, CurrentScore, (int)_distanceRun, Seed);
        _latestRankAchieved = rank;

        if (rank > 0 && rank <= 10)
        {
            Hud?.ShowBannerMessage($"🏆 NEW RECORD! RANK #{rank} ({CurrentScore:N0} PTS)", 4.0f);
        }
        else
        {
            Hud?.ShowBannerMessage($"CRASH! {CurrentScore:N0} PTS ({_distanceRun:F0}m)", 3.0f);
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        var stats = base.GetLiveStats();
        stats["distance"] = (int)_distanceRun;
        stats["speed"] = (int)(_currentSpeed * 3.6f); // km/h
        stats["multiplier"] = _multiplier;
        stats["seed"] = Seed;
        stats["apples"] = _applesCollected;
        stats["horseshoes"] = _horseshoesCollected;
        stats["latestRank"] = _latestRankAchieved;
        return stats;
    }
}
