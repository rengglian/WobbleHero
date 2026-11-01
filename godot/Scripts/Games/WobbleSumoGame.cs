using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game 2: Wobble Sumo (Ring King).
/// High-impact physical brawler inside a circular floating dojo.
/// Tilting the balance board drives 2D planar propulsion.
/// Rapid angular jerk snaps (> 180°/s²) trigger kinetic shockwave slams
/// that blast adjacent opponents over the shrinking ring perimeter.
/// </summary>
public partial class WobbleSumoGame : BaseGameMode
{
    public override string GameId => "WobbleSumo";
    public override string GameTitle => "Wobble Sumo";
    public override string GameDescription => "Tilt to shove sumo spheres off the shrinking dojo ring! Snap your board to unleash kinetic shockwave slams!";
    public override string GameIcon => "🦹";

    // Arena configuration
    [Export] public float BaseArenaRadius { get; set; } = 14.0f;
    [Export] public string ArenaSizeConfig { get; set; } = "standard"; // "compact", "standard", "colosseum"
    [Export] public string DifficultyConfig { get; set; } = "standard"; // "novice", "standard", "yokozuna"

    // Physics parameters
    private const float PlayerBaseMass = 100.0f;
    private const float PlayerRadius = 1.25f;
    private const float AccelGain = 28.0f;
    private const float PlanarDamping = 2.4f;
    private const float Restitution = 0.65f;
    private const float GravityY = 28.0f;
    private const float ShockwaveCooldownDuration = 1.2f;
    private const float ShockwaveBurstRadius = 4.2f;
    private const float ShockwaveImpulseForce = 45.0f;
    private const float JerkThreshold = 180.0f; // deg/s^2

    // Fighter Archetypes
    public enum FighterArchetype { Scout, Brawler, Yokozuna }

    private sealed class SumoFighter
    {
        public Node3D Node { get; }
        public MeshInstance3D MeshInstance { get; }
        public Vector3 Pos;
        public Vector3 Vel;
        public float Radius;
        public float Mass;
        public float MaxSpeed;
        public float ForceAccel;
        public bool IsAlive;
        public bool IsFalling;
        public FighterArchetype Archetype;
        public int ScoreValue;
        public float DecisionTimer;
        public float ChargeTimer;
        public bool IsCharging;

        public SumoFighter(Node3D node, MeshInstance3D mesh, FighterArchetype archetype, float radius, float mass, float maxSpeed, int scoreVal)
        {
            Node = node;
            MeshInstance = mesh;
            Archetype = archetype;
            Radius = radius;
            Mass = mass;
            MaxSpeed = maxSpeed;
            ForceAccel = 20.0f;
            ScoreValue = scoreVal;
            IsAlive = true;
            IsFalling = false;
        }
    }

    // Node hierarchy
    private Node3D? _arenaContainer;
    private MeshInstance3D? _dojoPlatformMesh;
    private MeshInstance3D? _ringEdgeBorderMesh;
    private MeshInstance3D? _shockwaveRingMesh;
    private Node3D? _playerNode;
    private MeshInstance3D? _playerMesh;
    private Camera3D? _camera;
    private StandardMaterial3D? _shockwaveMat;

    // Player state
    private Vector3 _playerPos;
    private Vector3 _playerVel;
    private int _playerLives = 3;
    private float _invulnerabilityTimer;
    private bool _playerFalling;

    // Jerk detection state
    private Vector2 _lastBoardAngle;
    private Vector2 _lastAngularVel;
    private float _currentJerk;
    private float _shockwaveCooldown;
    private float _shockwaveAnimTimer;
    private bool _shockwaveActive;
    private float _screenshakeIntensity;

    // Match progression
    private int _currentWave = 1;
    private int _knockoutsCount;
    private float _ringIntegrityPct = 1.0f;
    private float _currentArenaRadius = 14.0f;
    private float _waveTransitionTimer;
    private bool _waveTransitionActive;
    private float _multiplier = 1.0f;
    private int _latestRankAchieved = -1;

    // Fighters pool
    private readonly List<SumoFighter> _opponents = new(12);
    private readonly Dictionary<string, object> _liveStatsCache = new(16);
    private readonly List<object> _radarOpponentsCache = new(12);
    private readonly Random _rng = new();

    public override void _Ready()
    {
        BuildSceneNodes();
        ResetGame();
    }

    private void BuildSceneNodes()
    {
        // 1. Arena Root
        _arenaContainer = new Node3D { Name = "DojoArena" };
        AddChild(_arenaContainer);

        // Circular platform
        _dojoPlatformMesh = new MeshInstance3D
        {
            Name = "Platform",
            Mesh = new CylinderMesh
            {
                TopRadius = BaseArenaRadius,
                BottomRadius = BaseArenaRadius * 0.96f,
                Height = 0.8f,
                RadialSegments = 48
            },
            Position = new Vector3(0, -0.4f, 0)
        };
        var platformMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.16f, 0.24f),
            Roughness = 0.65f,
            Metallic = 0.2f
        };
        _dojoPlatformMesh.MaterialOverride = platformMat;
        _arenaContainer.AddChild(_dojoPlatformMesh);

        // Outer Ring Edge Warning Glow
        _ringEdgeBorderMesh = new MeshInstance3D
        {
            Name = "RingBorder",
            Mesh = new TorusMesh
            {
                InnerRadius = BaseArenaRadius - 0.25f,
                OuterRadius = BaseArenaRadius + 0.15f,
                Rings = 48,
                RingSegments = 16
            },
            Position = new Vector3(0, 0.02f, 0)
        };
        var borderMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.35f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.35f, 0.15f) * 1.5f
        };
        _ringEdgeBorderMesh.MaterialOverride = borderMat;
        _arenaContainer.AddChild(_ringEdgeBorderMesh);

        // Visual Shockwave Blast Ring
        _shockwaveMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.2f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.85f, 0.2f) * 2.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _shockwaveRingMesh = new MeshInstance3D
        {
            Name = "ShockwaveRing",
            Mesh = new TorusMesh { InnerRadius = 0.8f, OuterRadius = 1.15f, Rings = 32 },
            MaterialOverride = _shockwaveMat,
            Visible = false
        };
        AddChild(_shockwaveRingMesh);

        // 2. Player Sumo Hero
        _playerNode = new Node3D { Name = "PlayerSumo" };
        AddChild(_playerNode);

        _playerMesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = PlayerRadius, Height = PlayerRadius * 2.0f, RadialSegments = 24, Rings = 16 },
            Position = new Vector3(0, PlayerRadius, 0)
        };
        var playerMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.74f, 0.97f), // Hero Cyan
            Roughness = 0.25f,
            Metallic = 0.6f,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.74f, 0.97f) * 0.45f
        };
        _playerMesh.MaterialOverride = playerMat;
        _playerNode.AddChild(_playerMesh);

        // Player belt/topknot accent
        var belt = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = PlayerRadius * 1.02f, BottomRadius = PlayerRadius * 1.02f, Height = 0.35f },
            Position = new Vector3(0, PlayerRadius * 0.9f, 0)
        };
        var beltMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.85f, 0.2f) * 0.7f
        };
        belt.MaterialOverride = beltMat;
        _playerNode.AddChild(belt);

        // 3. Overhead 3D Arena Camera
        _camera = new Camera3D
        {
            Name = "ArenaCamera",
            Position = new Vector3(0, 22.0f, 17.5f),
            RotationDegrees = new Vector3(-55.0f, 0, 0),
            Fov = 60.0f,
            Current = true
        };
        AddChild(_camera);

        // Overhead Spotlight
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-65, 30, 0),
            LightColor = new Color(0.95f, 0.96f, 1.0f),
            LightEnergy = 1.3f
        };
        AddChild(light);
    }

    public void SetSumoConfig(string difficulty, string arenaSize)
    {
        if (!string.IsNullOrEmpty(difficulty)) SetDifficulty(difficulty);
        if (!string.IsNullOrEmpty(arenaSize)) SetArenaSize(arenaSize);
    }

    public void SetArenaSize(string sizeConfig)
    {
        ArenaSizeConfig = sizeConfig.ToLowerInvariant();
        BaseArenaRadius = ArenaSizeConfig switch
        {
            "compact" => 10.5f,
            "colosseum" => 18.0f,
            _ => 14.0f
        };
        ResetGame();
    }

    public void SetDifficulty(string diffConfig)
    {
        DifficultyConfig = diffConfig.ToLowerInvariant();
        ResetGame();
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _currentWave = 1;
        _knockoutsCount = 0;
        _multiplier = 1.0f;
        _playerLives = 3;
        _latestRankAchieved = -1;
        _shockwaveCooldown = 0f;
        _shockwaveActive = false;
        _screenshakeIntensity = 0f;
        _waveTransitionActive = false;
        _currentJerk = 0f;

        ApplyArenaDimensions(1.0f);
        RespawnPlayerAtCenter();
        ClearOpponents();
        SpawnWaveOpponents(_currentWave);

        Hud?.ShowBannerMessage("WOBBLE SUMO • WAVE 1 • RING KING!", 2.5f);
    }

    private void ApplyArenaDimensions(float integrity)
    {
        _ringIntegrityPct = integrity;
        _currentArenaRadius = BaseArenaRadius * _ringIntegrityPct;

        if (_dojoPlatformMesh != null)
        {
            _dojoPlatformMesh.Scale = new Vector3(_ringIntegrityPct, 1.0f, _ringIntegrityPct);
        }
        if (_ringEdgeBorderMesh != null)
        {
            _ringEdgeBorderMesh.Scale = new Vector3(_ringIntegrityPct, 1.0f, _ringIntegrityPct);
        }
    }

    private void RespawnPlayerAtCenter()
    {
        _playerPos = Vector3.Zero;
        _playerVel = Vector3.Zero;
        _playerFalling = false;
        _invulnerabilityTimer = 1.8f;

        if (_playerNode != null)
        {
            _playerNode.Position = new Vector3(0, 0, 0);
            _playerNode.Visible = true;
        }
    }

    private void ClearOpponents()
    {
        for (int i = 0; i < _opponents.Count; i++)
        {
            _opponents[i].Node.QueueFree();
        }
        _opponents.Clear();
    }

    private void SpawnWaveOpponents(int wave)
    {
        ClearOpponents();

        // Calculate composition based on wave and difficulty
        float diffMult = DifficultyConfig switch
        {
            "novice" => 0.8f,
            "yokozuna" => 1.35f,
            _ => 1.0f
        };

        int scoutCount = 2;
        int brawlerCount = 0;
        int yokozunaCount = 0;

        if (wave == 1)
        {
            scoutCount = 3;
            ApplyArenaDimensions(1.0f);
        }
        else if (wave == 2)
        {
            scoutCount = 2;
            brawlerCount = 2;
            ApplyArenaDimensions(0.92f);
        }
        else if (wave == 3)
        {
            scoutCount = 1;
            brawlerCount = 2;
            yokozunaCount = 1;
            ApplyArenaDimensions(0.84f);
        }
        else
        {
            // Wave 4+
            scoutCount = Math.Min(4, 2 + wave / 2);
            brawlerCount = Math.Min(3, 1 + wave / 2);
            yokozunaCount = Math.Min(2, wave / 3);
            float minIntegrity = 0.55f;
            float integrity = Math.Max(minIntegrity, 1.0f - (wave - 1) * 0.08f);
            ApplyArenaDimensions(integrity);
        }

        float spawnRingDist = _currentArenaRadius * 0.68f;
        int totalFighters = scoutCount + brawlerCount + yokozunaCount;
        float angleStep = Mathf.Tau / totalFighters;
        float currentAngle = (float)_rng.NextDouble() * Mathf.Tau;

        for (int i = 0; i < scoutCount; i++)
        {
            CreateOpponent(FighterArchetype.Scout, currentAngle, spawnRingDist, diffMult);
            currentAngle += angleStep;
        }
        for (int i = 0; i < brawlerCount; i++)
        {
            CreateOpponent(FighterArchetype.Brawler, currentAngle, spawnRingDist, diffMult);
            currentAngle += angleStep;
        }
        for (int i = 0; i < yokozunaCount; i++)
        {
            CreateOpponent(FighterArchetype.Yokozuna, currentAngle, spawnRingDist, diffMult);
            currentAngle += angleStep;
        }
    }

    private void CreateOpponent(FighterArchetype archetype, float angle, float dist, float diffMult)
    {
        var node = new Node3D();
        AddChild(node);

        float radius;
        float mass;
        float maxSpeed;
        int scoreVal;
        Color color;

        switch (archetype)
        {
            case FighterArchetype.Scout:
                radius = 0.95f;
                mass = 65.0f * diffMult;
                maxSpeed = 9.5f * diffMult;
                scoreVal = 300;
                color = new Color(0.1f, 0.85f, 0.45f); // Emerald
                break;
            case FighterArchetype.Brawler:
                radius = 1.35f;
                mass = 125.0f * diffMult;
                maxSpeed = 7.5f * diffMult;
                scoreVal = 600;
                color = new Color(0.95f, 0.55f, 0.1f); // Amber
                break;
            default: // Yokozuna
                radius = 1.85f;
                mass = 230.0f * diffMult;
                maxSpeed = 5.2f * diffMult;
                scoreVal = 1200;
                color = new Color(0.85f, 0.18f, 0.25f); // Crimson
                break;
        }

        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2.0f, RadialSegments = 20, Rings = 14 },
            Position = new Vector3(0, radius, 0)
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.35f,
            Metallic = 0.45f,
            EmissionEnabled = true,
            Emission = color * 0.35f
        };
        mesh.MaterialOverride = mat;
        node.AddChild(mesh);

        // Position on dojo ring
        float px = MathF.Cos(angle) * dist;
        float pz = MathF.Sin(angle) * dist;
        node.Position = new Vector3(px, 0, pz);

        var fighter = new SumoFighter(node, mesh, archetype, radius, mass, maxSpeed, scoreVal)
        {
            Pos = new Vector3(px, 0, pz)
        };
        _opponents.Add(fighter);
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start game on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.06f || MathF.Abs(boardState.Pitch) > 0.06f))
        {
            StartGame();
            Hud?.ShowBannerMessage("MATCH COMMENCED! PUSH THEM OFF!", 1.5f);
        }

        if (GameState != "RUNNING") return;

        // 1. Angular Jerk / Snap Detection for Shockwave Body Slam
        UpdateJerkDetection(boardState, dt);

        // 2. Player 2D Tilt Driving Force Integration
        if (!_playerFalling)
        {
            float driveX = boardState.Roll;
            float driveZ = boardState.Pitch;

            Vector3 forceDrive = new Vector3(driveX, 0, driveZ) * (PlayerBaseMass * AccelGain);
            Vector3 accel = forceDrive / PlayerBaseMass;

            _playerVel += accel * dt;
            _playerVel *= Math.Max(0f, 1.0f - PlanarDamping * dt);

            // Cap player max velocity
            float currentSpeed = _playerVel.Length();
            float maxPlayerSpeed = 15.0f;
            if (currentSpeed > maxPlayerSpeed)
            {
                _playerVel = _playerVel.Normalized() * maxPlayerSpeed;
            }

            _playerPos.X += _playerVel.X * dt;
            _playerPos.Z += _playerVel.Z * dt;

            // Check if player crossed ring perimeter
            float distFromCenter = MathF.Sqrt(_playerPos.X * _playerPos.X + _playerPos.Z * _playerPos.Z);
            if (distFromCenter > _currentArenaRadius)
            {
                _playerFalling = true;
            }
        }
        else
        {
            // Falling to void
            _playerVel.Y -= GravityY * dt;
            _playerPos += _playerVel * dt;

            if (_playerPos.Y < -6.0f)
            {
                OnPlayerFellOut();
                return;
            }
        }

        // Apply Player Node Transform
        if (_playerNode != null)
        {
            _playerNode.Position = _playerPos;

            // Invulnerability blink effect
            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer -= dt;
                _playerNode.Visible = (MathF.Sin(_invulnerabilityTimer * 28.0f) > 0f);
            }
            else
            {
                _playerNode.Visible = true;
            }
        }

        // 3. Update AI Sumo Opponents (Steering & Physics)
        UpdateOpponentsAI(dt);

        // 4. Analytical Sphere-Sphere Collisions
        ResolveAllSphereCollisions(dt);

        // 5. Update Shockwave Slam Animation & Cooldown
        UpdateShockwaveEffects(dt);

        // 6. Camera Follow & Screenshake
        UpdateCamera(dt);

        // 7. Check Wave Cleared Condition
        CheckWaveCompletion(dt);
    }

    private void UpdateJerkDetection(BoardState boardState, float dt)
    {
        if (_shockwaveCooldown > 0f)
        {
            _shockwaveCooldown -= dt;
        }

        Vector2 currentAngle = new Vector2(boardState.FusedRollDeg, boardState.FusedPitchDeg);
        if (boardState.Source != "atoms3")
        {
            currentAngle = new Vector2(boardState.Roll * 28.0f, boardState.Pitch * 28.0f);
        }

        Vector2 angularVel = (currentAngle - _lastBoardAngle) / Math.Max(0.0001f, dt);
        Vector2 angularAccel = (angularVel - _lastAngularVel) / Math.Max(0.0001f, dt);
        _currentJerk = angularAccel.Length();

        _lastBoardAngle = currentAngle;
        _lastAngularVel = angularVel;

        // Trigger on Jerk threshold or Button Press fallback
        if ((_currentJerk >= JerkThreshold || boardState.IsButtonPressed) && _shockwaveCooldown <= 0f && !_playerFalling)
        {
            TriggerShockwaveSlam();
        }
    }

    public void TriggerShockwave() => TriggerShockwaveSlam();

    public void TriggerShockwaveSlam()
    {
        if (_shockwaveCooldown > 0f || _playerFalling) return;

        _shockwaveCooldown = ShockwaveCooldownDuration;
        _shockwaveActive = true;
        _shockwaveAnimTimer = 0f;
        _screenshakeIntensity = 0.65f;

        if (_shockwaveRingMesh != null)
        {
            _shockwaveRingMesh.Visible = true;
            _shockwaveRingMesh.Position = new Vector3(_playerPos.X, 0.1f, _playerPos.Z);
            _shockwaveRingMesh.Scale = Vector3.One * 0.4f;
        }

        Hud?.ShowBannerMessage("💥 KINETIC SHOCKWAVE SLAM! 💥", 1.2f);

        // Apply radial blast impulse to adjacent opponents
        for (int i = 0; i < _opponents.Count; i++)
        {
            var foe = _opponents[i];
            if (!foe.IsAlive || foe.IsFalling) continue;

            Vector3 delta = foe.Pos - _playerPos;
            delta.Y = 0f;
            float dist = delta.Length();

            if (dist < ShockwaveBurstRadius && dist > 0.001f)
            {
                Vector3 blastDir = delta / dist;
                float falloff = Math.Max(0.2f, 1.0f - (dist / ShockwaveBurstRadius));
                float impulse = ShockwaveImpulseForce * falloff;

                foe.Vel += blastDir * (impulse / foe.Mass * 2.2f);
            }
        }
    }

    private void UpdateShockwaveEffects(float dt)
    {
        if (!_shockwaveActive) return;

        _shockwaveAnimTimer += dt;
        float progress = _shockwaveAnimTimer / 0.38f;

        if (progress >= 1.0f)
        {
            _shockwaveActive = false;
            if (_shockwaveRingMesh != null) _shockwaveRingMesh.Visible = false;
        }
        else
        {
            if (_shockwaveRingMesh != null)
            {
                float scale = Mathf.Lerp(0.5f, ShockwaveBurstRadius * 1.1f, progress);
                _shockwaveRingMesh.Scale = new Vector3(scale, 1.0f, scale);

                if (_shockwaveMat != null)
                {
                    Color c = _shockwaveMat.AlbedoColor;
                    c.A = 1.0f - progress;
                    _shockwaveMat.AlbedoColor = c;
                }
            }
        }
    }

    private void UpdateOpponentsAI(float dt)
    {
        for (int i = 0; i < _opponents.Count; i++)
        {
            var foe = _opponents[i];
            if (!foe.IsAlive) continue;

            if (foe.IsFalling)
            {
                foe.Vel.Y -= GravityY * dt;
                foe.Pos += foe.Vel * dt;
                foe.Node.Position = foe.Pos;

                if (foe.Pos.Y < -6.0f)
                {
                    OnOpponentEliminated(foe);
                }
                continue;
            }

            // AI Steering towards player or edge recovery
            foe.DecisionTimer -= dt;
            if (foe.DecisionTimer <= 0f)
            {
                foe.DecisionTimer = 0.25f + (float)_rng.NextDouble() * 0.25f;

                // Random charge burst for Brawlers & Yokozuna
                if (foe.Archetype != FighterArchetype.Scout && _rng.NextDouble() < 0.2)
                {
                    foe.IsCharging = true;
                    foe.ChargeTimer = 0.75f;
                }
            }

            if (foe.IsCharging)
            {
                foe.ChargeTimer -= dt;
                if (foe.ChargeTimer <= 0f) foe.IsCharging = false;
            }

            Vector3 toPlayer = _playerPos - foe.Pos;
            toPlayer.Y = 0f;
            float distToPlayer = toPlayer.Length();
            Vector3 steerDir = distToPlayer > 0.001f ? (toPlayer / distToPlayer) : Vector3.Forward;

            // Check if near ring edge: steer inward to avoid suicide
            float distFromCenter = MathF.Sqrt(foe.Pos.X * foe.Pos.X + foe.Pos.Z * foe.Pos.Z);
            if (distFromCenter > _currentArenaRadius * 0.88f)
            {
                Vector3 toCenter = -foe.Pos;
                toCenter.Y = 0f;
                if (toCenter.Length() > 0.01f)
                {
                    steerDir = (steerDir + toCenter.Normalized() * 1.8f).Normalized();
                }
            }

            float currentFoeSpeed = foe.MaxSpeed * (foe.IsCharging ? 1.55f : 1.0f);
            Vector3 targetVel = steerDir * currentFoeSpeed;

            foe.Vel = foe.Vel.Lerp(targetVel, Math.Min(1.0f, 4.0f * dt));
            foe.Pos.X += foe.Vel.X * dt;
            foe.Pos.Z += foe.Vel.Z * dt;
            foe.Node.Position = foe.Pos;

            // Check if opponent crossed ring edge
            if (distFromCenter > _currentArenaRadius)
            {
                foe.IsFalling = true;
            }
        }
    }

    private void ResolveAllSphereCollisions(float dt)
    {
        // 1. Player vs All Opponents
        if (!_playerFalling && _playerNode != null)
        {
            for (int i = 0; i < _opponents.Count; i++)
            {
                var foe = _opponents[i];
                if (!foe.IsAlive || foe.IsFalling) continue;

                ResolveSpherePair(ref _playerPos, ref _playerVel, PlayerRadius, PlayerBaseMass,
                                  ref foe.Pos, ref foe.Vel, foe.Radius, foe.Mass, isPlayerCollision: true);
            }
        }

        // 2. Opponent vs Opponent
        for (int i = 0; i < _opponents.Count; i++)
        {
            var a = _opponents[i];
            if (!a.IsAlive || a.IsFalling) continue;

            for (int j = i + 1; j < _opponents.Count; j++)
            {
                var b = _opponents[j];
                if (!b.IsAlive || b.IsFalling) continue;

                ResolveSpherePair(ref a.Pos, ref a.Vel, a.Radius, a.Mass,
                                  ref b.Pos, ref b.Vel, b.Radius, b.Mass, isPlayerCollision: false);
            }
        }
    }

    private void ResolveSpherePair(ref Vector3 posA, ref Vector3 velA, float radiusA, float massA,
                                   ref Vector3 posB, ref Vector3 velB, float radiusB, float massB, bool isPlayerCollision)
    {
        Vector3 delta = posB - posA;
        delta.Y = 0f;
        float dist = delta.Length();
        float minRadius = radiusA + radiusB;

        if (dist < minRadius && dist > 0.0001f)
        {
            Vector3 normal = delta / dist;
            float penetration = minRadius - dist;
            float totalMass = massA + massB;

            // 1. Positional Separation
            posA -= normal * penetration * (massB / totalMass);
            posB += normal * penetration * (massA / totalMass);

            // 2. Velocity Impulse Exchange
            Vector3 relVel = velB - velA;
            float normalRelVel = relVel.Dot(normal);

            if (normalRelVel < 0f)
            {
                float impulseMagnitude = -((1.0f + Restitution) * normalRelVel) / ((1.0f / massA) + (1.0f / massB));
                Vector3 impulseVector = normal * impulseMagnitude;

                velA -= impulseVector / massA;
                velB += impulseVector / massB;

                if (isPlayerCollision && impulseMagnitude > 22.0f)
                {
                    _screenshakeIntensity = MathF.Max(_screenshakeIntensity, 0.35f);
                }
            }
        }
    }

    private void UpdateCamera(float dt)
    {
        if (_camera == null) return;

        Vector3 shakeOffset = Vector3.Zero;
        if (_screenshakeIntensity > 0f)
        {
            _screenshakeIntensity = Mathf.MoveToward(_screenshakeIntensity, 0f, 2.5f * dt);
            shakeOffset = new Vector3(
                ((float)_rng.NextDouble() * 2f - 1f) * _screenshakeIntensity,
                0,
                ((float)_rng.NextDouble() * 2f - 1f) * _screenshakeIntensity
            );
        }

        Vector3 targetCamPos = new Vector3(
            _playerPos.X * 0.35f,
            22.0f,
            17.5f + _playerPos.Z * 0.25f
        ) + shakeOffset;

        _camera.Position = _camera.Position.Lerp(targetCamPos, 8.0f * dt);
    }

    private void OnOpponentEliminated(SumoFighter foe)
    {
        foe.IsAlive = false;
        foe.Node.Visible = false;
        _knockoutsCount++;

        _multiplier = MathF.Min(5.0f, _multiplier + 0.25f);
        int pts = (int)(foe.ScoreValue * _multiplier);
        AddScore(pts);

        Hud?.ShowBannerMessage($"🤼 RING OUT! +{pts} PTS (x{_multiplier:F1})", 1.2f);
    }

    private void OnPlayerFellOut()
    {
        _playerLives--;
        _multiplier = 1.0f;

        if (_playerLives > 0)
        {
            Hud?.ShowBannerMessage($"⚠️ RING OUT! {_playerLives} LIVES REMAINING", 2.0f);
            RespawnPlayerAtCenter();
        }
        else
        {
            TriggerGameOver("Knocked Out of the Dojo Ring!");
            string playerName = LeaderboardService.Instance.ActivePlayerName;
            _latestRankAchieved = LeaderboardService.Instance.RecordScore(playerName, CurrentScore, _knockoutsCount, _currentWave);
            Hud?.ShowBannerMessage($"💀 MATCH OVER! FINAL SCORE: {CurrentScore:N0}", 3.5f);
        }
    }

    private void CheckWaveCompletion(float dt)
    {
        if (_waveTransitionActive)
        {
            _waveTransitionTimer -= dt;
            if (_waveTransitionTimer <= 0f)
            {
                _waveTransitionActive = false;
                _currentWave++;
                SpawnWaveOpponents(_currentWave);
                Hud?.ShowBannerMessage($"🔥 WAVE {_currentWave} COMMENCING!", 2.0f);
            }
            return;
        }

        int aliveFoes = 0;
        for (int i = 0; i < _opponents.Count; i++)
        {
            if (_opponents[i].IsAlive) aliveFoes++;
        }

        if (aliveFoes == 0 && !_playerFalling)
        {
            _waveTransitionActive = true;
            _waveTransitionTimer = 2.2f;
            int waveBonus = 1000 * _currentWave;
            AddScore(waveBonus);
            Hud?.ShowBannerMessage($"🏆 WAVE {_currentWave} CLEARED! +{waveBonus:N0} BONUS!", 2.0f);
        }
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        int aliveFoes = 0;
        _radarOpponentsCache.Clear();

        for (int i = 0; i < _opponents.Count; i++)
        {
            var foe = _opponents[i];
            if (foe.IsAlive)
            {
                aliveFoes++;
                _radarOpponentsCache.Add(new Dictionary<string, object>
                {
                    { "x", foe.Pos.X },
                    { "y", foe.Pos.Z },
                    { "mass", foe.Mass / 100.0f },
                    { "kind", foe.Archetype == FighterArchetype.Yokozuna ? "heavy" : "grunt" },
                    { "alive", foe.IsAlive && !foe.IsFalling }
                });
            }
        }

        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["wave"] = _currentWave;
        _liveStatsCache["knockouts"] = _knockoutsCount;
        _liveStatsCache["opponentsRemaining"] = aliveFoes;
        _liveStatsCache["lives"] = _playerLives;
        _liveStatsCache["ringIntegrity"] = (int)(_ringIntegrityPct * 100f);
        _liveStatsCache["ringRadius"] = _currentArenaRadius;
        _liveStatsCache["maxRingRadius"] = BaseArenaRadius;
        _liveStatsCache["multiplier"] = _multiplier;
        _liveStatsCache["shockwaveCooldown"] = Math.Max(0f, _shockwaveCooldown);
        _liveStatsCache["shockwaveReady"] = (_shockwaveCooldown <= 0f);
        _liveStatsCache["shockwaveActive"] = _shockwaveActive;
        _liveStatsCache["jerk"] = _currentJerk;
        _liveStatsCache["latestRank"] = _latestRankAchieved;
        _liveStatsCache["difficulty"] = DifficultyConfig;
        _liveStatsCache["arenaSize"] = ArenaSizeConfig;
        _liveStatsCache["playerPos"] = new Dictionary<string, object>
        {
            { "x", _playerPos.X },
            { "y", _playerPos.Z }
        };
        _liveStatsCache["opponents"] = _radarOpponentsCache;

        return _liveStatsCache;
    }
}
