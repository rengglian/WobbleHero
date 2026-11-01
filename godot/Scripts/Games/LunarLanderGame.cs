using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Mini-Game: Lunar Lander (Planetary Hopper).
/// Extreme precision vector-thrust navigation. The player pilots a Lunar Excursion
/// Module (LEM) across cavernous subterranean moon tunnels to touch down on extraction pads.
/// Roll gimbals the main thruster vector (±35°); Pitch controls throttle (dive descent vs boost climb).
/// </summary>
public partial class LunarLanderGame : BaseGameMode
{
    public override string GameId => "LunarLander";
    public override string GameTitle => "Lunar Lander";
    public override string GameDescription => "Pilot the lunar excursion module to soft touchdown on extraction pads! Manage fuel and gimbal thrusters.";
    public override string GameIcon => "🛸";

    // Planet Presets
    [Export] public string PlanetPreset { get; set; } = "moon";

    // Playfield dimensions
    public const float PlayfieldHalfWidth = 45.0f;
    public const float CeilingY = 38.0f;
    public const float LanderHalfSpan = 1.6f;
    public const float GearHeight = 1.35f;

    // Landing Pad definitions
    public record LandingPad(string Id, string Name, float MinX, float MaxX, float SurfaceY, float Multiplier, int BaseScore, Color PadColor);

    private static readonly LandingPad PadAlpha = new("alpha", "Pad Alpha", -32.0f, -22.0f, 5.0f, 1.0f, 500, new Color(0.2f, 0.75f, 1.0f));
    private static readonly LandingPad PadBeta = new("beta", "Pad Beta", -5.0f, 5.0f, 8.5f, 2.0f, 1000, new Color(1.0f, 0.85f, 0.2f));
    private static readonly LandingPad PadGamma = new("gamma", "Pad Gamma", 24.0f, 29.5f, 14.0f, 3.0f, 2000, new Color(1.0f, 0.2f, 0.8f));

    private static readonly LandingPad[] LandingPads = { PadAlpha, PadBeta, PadGamma };

    // Fuel canister entity
    private sealed class FuelCanister
    {
        public Node3D Node { get; }
        public Vector3 Pos;
        public bool Collected;
        public float Radius;

        public FuelCanister(Node3D node, Vector3 pos, float radius)
        {
            Node = node;
            Pos = pos;
            Radius = radius;
            Collected = false;
        }
    }

    // Node hierarchy
    private Node3D? _terrainContainer;
    private Node3D? _landerNode;
    private Node3D? _gimbalNozzle;
    private MeshInstance3D? _thrusterPlume;
    private StandardMaterial3D? _plumeMaterial;
    private Camera3D? _camera;

    // Lander state
    private Vector2 _landerPos = new(0, 32.0f);
    private Vector2 _landerVel = Vector2.Zero;
    private float _gimbalAngleRad = 0f;
    private float _throttlePct = 0f;
    private float _fuel = 100.0f;
    private bool _engineFiring = false;
    private bool _isLanded = false;
    private bool _isCrashed = false;
    private float _roundRestartTimer = 0f;
    private int _landingsCompleted = 0;
    private int _fuelPickupsCollected = 0;
    private int _latestRankAchieved = -1;
    private string _activeTouchdownStatus = "FLYING";

    // Physics parameters
    private float _currentGravity = 1.62f; // m/s^2 (Moon default)
    private const float BaseFuelBurnRate = 7.5f; // % per second at 100% throttle
    private const float MaxGimbalAngleRad = 35.0f * (MathF.PI / 180.0f);

    private readonly List<FuelCanister> _fuelCanisters = new(8);
    private readonly Dictionary<string, object> _liveStatsCache = new(20);
    private readonly List<object> _radarPadsCache = new(4);
    private readonly List<object> _radarCanistersCache = new(8);
    private readonly Random _rng = new();

    public override void _Ready()
    {
        ApplyPlanetGravity(PlanetPreset);
        BuildSceneNodes();
        ResetGame();
    }

    public void SetPlanet(string planet) => SetGravityPreset(planet);

    public void SetGravityPreset(string planet)
    {
        PlanetPreset = planet?.ToLowerInvariant() ?? "moon";
        ApplyPlanetGravity(PlanetPreset);
        ResetGame();
    }

    public void TriggerEmergencyRcs()
    {
        if (GameState != "RUNNING" || _isLanded || _isCrashed) return;
        _landerVel.X *= 0.15f;
        _landerVel.Y = Math.Max(-0.8f, _landerVel.Y * 0.5f);
        _gimbalAngleRad = 0f;
        _fuel = Math.Max(0f, _fuel - 8.0f);
        Hud?.ShowBannerMessage("🛡️ EMERGENCY RCS STABILIZER FIRED! (-8% FUEL)", 1.5f);
    }

    public void Refuel(float amount = 50.0f)
    {
        _fuel = Math.Min(100.0f, _fuel + amount);
        Hud?.ShowBannerMessage($"⛽ REFUELED! (+{amount:F0}%)", 1.2f);
    }

    private void ApplyPlanetGravity(string planet)
    {
        _currentGravity = planet switch
        {
            "mars" => 3.72f,
            "europa" => 1.31f,
            "earth" => 9.81f,
            _ => 1.62f // moon
        };
    }

    private void BuildSceneNodes()
    {
        // 1. Terrain & Crater Mesh
        _terrainContainer = new Node3D { Name = "TerrainContainer" };
        AddChild(_terrainContainer);

        var terrainMesh = GenerateCraterMesh();
        var terrainInstance = new MeshInstance3D { Mesh = terrainMesh };
        var rockMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.20f, 0.25f),
            Roughness = 0.9f,
            Metallic = 0.1f
        };
        terrainInstance.MaterialOverride = rockMat;
        _terrainContainer.AddChild(terrainInstance);

        // Landing Pad Glowing Beacons
        foreach (var pad in LandingPads)
        {
            var padMesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(pad.MaxX - pad.MinX, 0.4f, 8.0f) },
                Position = new Vector3((pad.MinX + pad.MaxX) * 0.5f, pad.SurfaceY - 0.2f, 0)
            };
            var padMat = new StandardMaterial3D
            {
                AlbedoColor = pad.PadColor,
                EmissionEnabled = true,
                Emission = pad.PadColor * 1.5f
            };
            padMesh.MaterialOverride = padMat;
            _terrainContainer.AddChild(padMesh);

            // Left & Right Pad Antennas / Lights
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float px = sign < 0 ? pad.MinX + 0.3f : pad.MaxX - 0.3f;
                var antenna = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.12f, Height = 1.4f },
                    Position = new Vector3(px, pad.SurfaceY + 0.7f, 0)
                };
                var antMat = new StandardMaterial3D
                {
                    AlbedoColor = pad.PadColor,
                    EmissionEnabled = true,
                    Emission = pad.PadColor * 2.0f
                };
                antenna.MaterialOverride = antMat;
                _terrainContainer.AddChild(antenna);
            }
        }

        // 2. Modular Lunar Excursion Module (LEM)
        _landerNode = new Node3D { Name = "Lander" };
        AddChild(_landerNode);

        // Main Ascent Cabin (Octagonal Gold Foil)
        var cabin = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.35f, Height = 1.5f, RadialSegments = 8 },
            Position = new Vector3(0, 0.25f, 0)
        };
        var goldFoilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.78f, 0.2f),
            Metallic = 0.95f,
            Roughness = 0.2f
        };
        cabin.MaterialOverride = goldFoilMat;
        _landerNode.AddChild(cabin);

        // Command Module Viewport
        var viewport = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.4f, 0.25f) },
            Position = new Vector3(0, 0.55f, 1.25f)
        };
        var glassMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.35f, 0.6f),
            Metallic = 0.8f,
            Roughness = 0.1f
        };
        viewport.MaterialOverride = glassMat;
        _landerNode.AddChild(viewport);

        // 4 Articulated Landing Struts with Footpads
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90.0f + 45.0f) * (MathF.PI / 180.0f);
            float dirX = MathF.Cos(angle);
            float dirZ = MathF.Sin(angle);

            var leg = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 1.6f },
                Position = new Vector3(dirX * 1.35f, -0.4f, dirZ * 1.35f),
                RotationDegrees = new Vector3(dirZ * 32.0f, 0, -dirX * 32.0f)
            };
            var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.62f, 0.68f), Metallic = 0.8f, Roughness = 0.3f };
            leg.MaterialOverride = metalMat;
            _landerNode.AddChild(leg);

            var pad = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.08f },
                Position = new Vector3(dirX * 1.85f, -GearHeight, dirZ * 1.85f)
            };
            pad.MaterialOverride = metalMat;
            _landerNode.AddChild(pad);
        }

        // Gimbaled Main Thruster Nozzle
        _gimbalNozzle = new Node3D { Name = "GimbalNozzle", Position = new Vector3(0, -0.6f, 0) };
        _landerNode.AddChild(_gimbalNozzle);

        var nozzleMesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.45f, Height = 0.55f },
            Position = new Vector3(0, -0.25f, 0)
        };
        var nozzleMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.22f, 0.25f), Metallic = 0.9f };
        nozzleMesh.MaterialOverride = nozzleMat;
        _gimbalNozzle.AddChild(nozzleMesh);

        // Exhaust Jet Plume
        _plumeMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.85f, 1.0f, 0.85f),
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.85f, 1.0f) * 2.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _thrusterPlume = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.05f, Height = 2.2f },
            Position = new Vector3(0, -1.45f, 0),
            MaterialOverride = _plumeMaterial,
            Visible = false
        };
        _gimbalNozzle.AddChild(_thrusterPlume);

        // 3. 2.5D Following Camera
        _camera = new Camera3D
        {
            Name = "LanderCamera",
            Fov = 64.0f,
            Position = new Vector3(0, 20.0f, 32.0f),
            Current = true
        };
        AddChild(_camera);

        // Directional cosmic light
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45.0f, 25.0f, 0),
            LightColor = new Color(0.95f, 0.96f, 1.0f),
            LightEnergy = 1.35f
        };
        AddChild(sun);
    }

    private static ArrayMesh GenerateCraterMesh()
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        const int xSteps = 90;
        const float xStep = (PlayfieldHalfWidth * 2.0f) / xSteps;
        const float zBack = -8.0f;
        const float zFront = 8.0f;

        for (int i = 0; i < xSteps; i++)
        {
            float x0 = -PlayfieldHalfWidth + i * xStep;
            float x1 = x0 + xStep;

            float y0 = GetTerrainHeight(x0);
            float y1 = GetTerrainHeight(x1);

            Vector3 v00 = new(x0, y0, zBack);
            Vector3 v01 = new(x0, y0, zFront);
            Vector3 v10 = new(x1, y1, zBack);
            Vector3 v11 = new(x1, y1, zFront);

            Vector3 norm = new Vector3(-(y1 - y0), x1 - x0, 0).Normalized();
            surfaceTool.SetNormal(norm);
            surfaceTool.SetUV(new Vector2((float)i / xSteps, 0));

            surfaceTool.AddVertex(v00);
            surfaceTool.AddVertex(v10);
            surfaceTool.AddVertex(v11);

            surfaceTool.AddVertex(v00);
            surfaceTool.AddVertex(v11);
            surfaceTool.AddVertex(v01);
        }

        return surfaceTool.Commit();
    }

    public static float GetTerrainHeight(float x)
    {
        foreach (var pad in LandingPads)
        {
            if (x >= pad.MinX && x <= pad.MaxX)
            {
                return pad.SurfaceY;
            }
        }

        float baseCurve = 4.0f + 3.0f * MathF.Sin(x * 0.08f) + 2.2f * MathF.Cos(x * 0.18f);
        float crags = 1.2f * MathF.Sin(x * 0.45f) + 0.6f * MathF.Cos(x * 0.9f);
        return Math.Max(1.0f, baseCurve + crags);
    }

    public override void ResetGame()
    {
        base.ResetGame();
        _landerPos = new Vector2(0f, 32.0f);
        _landerVel = new Vector2((float)(_rng.NextDouble() * 1.5f - 0.75f), -0.5f);
        _gimbalAngleRad = 0f;
        _throttlePct = 0f;
        _fuel = 100.0f;
        _engineFiring = false;
        _isLanded = false;
        _isCrashed = false;
        _roundRestartTimer = 0f;
        _latestRankAchieved = -1;
        _activeTouchdownStatus = "FLYING";

        SpawnFuelCanisters();

        if (_landerNode != null)
        {
            _landerNode.Position = new Vector3(_landerPos.X, _landerPos.Y, 0);
            _landerNode.Rotation = Vector3.Zero;
            _landerNode.Visible = true;
        }

        Hud?.ShowBannerMessage($"TARGET EXTRACTION PADS • {PlanetPreset.ToUpperInvariant()} (g={_currentGravity:F2})", 3.0f);
    }

    private void SpawnFuelCanisters()
    {
        foreach (var c in _fuelCanisters)
        {
            c.Node.QueueFree();
        }
        _fuelCanisters.Clear();

        Vector3[] spawnPoints =
        {
            new(-18.0f, 18.0f, 0),
            new(12.0f, 22.0f, 0),
            new(-36.0f, 24.0f, 0),
            new(34.0f, 28.0f, 0)
        };

        foreach (var pt in spawnPoints)
        {
            var node = new Node3D();
            _terrainContainer?.AddChild(node);
            node.Position = pt;

            var mesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.35f, Height = 1.1f },
                RotationDegrees = new Vector3(0, 0, 45)
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.1f, 0.95f, 0.4f),
                EmissionEnabled = true,
                Emission = new Color(0.1f, 0.95f, 0.4f) * 1.6f
            };
            mesh.MaterialOverride = mat;
            node.AddChild(mesh);

            _fuelCanisters.Add(new FuelCanister(node, pt, 1.8f));
        }
    }

    public override void UpdateBoardInput(BoardState boardState, double delta)
    {
        float dt = (float)delta;

        // Auto-start on tilt
        if (GameState == "START" && (MathF.Abs(boardState.Roll) > 0.05f || MathF.Abs(boardState.Pitch) > 0.05f))
        {
            StartGame();
            Hud?.ShowBannerMessage("THRUSTERS ENGAGED!", 1.2f);
        }

        // Post-touchdown or crash countdown to reset
        if (_isLanded || _isCrashed)
        {
            _roundRestartTimer += dt;
            if (_roundRestartTimer > 3.5f && boardState.IsButtonPressed)
            {
                ResetGame();
            }
            return;
        }

        if (GameState != "RUNNING") return;

        // 1. Gimbal Thrust Vectoring from Roll (±35°)
        float rollInput = boardState.Roll;
        _gimbalAngleRad = Math.Clamp(rollInput * MaxGimbalAngleRad, -MaxGimbalAngleRad, MaxGimbalAngleRad);

        // 2. Throttle Control from Pitch
        float pitchVal = boardState.Pitch;
        if (_fuel > 0f)
        {
            if (pitchVal > 0.15f)
            {
                float boostFactor = Math.Min(1.0f, (pitchVal - 0.15f) / 0.85f);
                _throttlePct = Mathf.Lerp(1.0f, 2.2f, boostFactor);
            }
            else if (pitchVal < -0.15f)
            {
                float cutFactor = Math.Min(1.0f, (-pitchVal - 0.15f) / 0.85f);
                _throttlePct = Mathf.Lerp(1.0f, 0.0f, cutFactor);
            }
            else
            {
                _throttlePct = 1.0f;
            }

            // Burn fuel
            float burn = (_throttlePct / 2.2f) * BaseFuelBurnRate * dt;
            _fuel = Math.Max(0f, _fuel - burn);
            _engineFiring = _throttlePct > 0.05f;
        }
        else
        {
            _throttlePct = 0f;
            _engineFiring = false;
        }

        // 3. Accelerations & Velocity Integration
        float totalThrustMag = _throttlePct * _currentGravity;
        float thrustX = -totalThrustMag * MathF.Sin(_gimbalAngleRad);
        float thrustY = totalThrustMag * MathF.Cos(_gimbalAngleRad);

        float accelX = thrustX;
        float accelY = thrustY - _currentGravity;

        _landerVel.X += accelX * dt;
        _landerVel.Y += accelY * dt;

        _landerPos += _landerVel * dt;

        // Boundaries clamping
        _landerPos.X = Math.Clamp(_landerPos.X, -PlayfieldHalfWidth + LanderHalfSpan, PlayfieldHalfWidth - LanderHalfSpan);
        if (_landerPos.Y > CeilingY)
        {
            _landerPos.Y = CeilingY;
            _landerVel.Y = Math.Min(0f, _landerVel.Y);
        }

        // 4. Update Visual Nodes & Articulation
        if (_landerNode != null)
        {
            _landerNode.Position = new Vector3(_landerPos.X, _landerPos.Y, 0);
            _landerNode.Rotation = new Vector3(0, 0, -_gimbalAngleRad * 0.45f);

            if (_gimbalNozzle != null)
            {
                _gimbalNozzle.Rotation = new Vector3(0, 0, -_gimbalAngleRad * 0.65f);
            }

            if (_thrusterPlume != null)
            {
                _thrusterPlume.Visible = _engineFiring;
                if (_engineFiring)
                {
                    float plumeScale = (_throttlePct / 2.2f) * (0.85f + (float)_rng.NextDouble() * 0.3f);
                    _thrusterPlume.Scale = new Vector3(plumeScale, plumeScale, plumeScale);
                }
            }
        }

        // 5. Fuel Canisters Collision
        for (int i = 0; i < _fuelCanisters.Count; i++)
        {
            var can = _fuelCanisters[i];
            if (can.Collected) continue;

            can.Node.RotateY(dt * 3.0f);
            float dist = _landerPos.DistanceTo(new Vector2(can.Pos.X, can.Pos.Y));

            if (dist < can.Radius)
            {
                can.Collected = true;
                can.Node.Visible = false;
                _fuel = Math.Min(100.0f, _fuel + 35.0f);
                _fuelPickupsCollected++;
                AddScore(150);
                Hud?.ShowBannerMessage("⛽ HELIUM-3 FUEL COLLECTED! (+35%)", 1.5f);
            }
        }

        // 6. Camera Follow
        if (_camera != null)
        {
            Vector3 targetCam = new Vector3(_landerPos.X * 0.45f, Math.Max(12.0f, _landerPos.Y * 0.65f + 8.0f), 30.0f);
            _camera.Position = _camera.Position.Lerp(targetCam, 6.0f * dt);
        }

        // 7. Ground / Terrain Collision & Touchdown Evaluation
        EvaluateTouchdown();
    }

    private void EvaluateTouchdown()
    {
        float terrainY = GetTerrainHeight(_landerPos.X);
        float gearBottomY = _landerPos.Y - GearHeight;

        if (gearBottomY <= terrainY)
        {
            _landerPos.Y = terrainY + GearHeight;
            _engineFiring = false;
            if (_thrusterPlume != null) _thrusterPlume.Visible = false;

            float impactVy = MathF.Abs(_landerVel.Y);
            float impactVx = MathF.Abs(_landerVel.X);
            float tiltDeg = MathF.Abs(_gimbalAngleRad * (180.0f / MathF.PI));

            LandingPad? padLandedOn = null;
            foreach (var pad in LandingPads)
            {
                if (_landerPos.X >= pad.MinX && _landerPos.X <= pad.MaxX)
                {
                    padLandedOn = pad;
                    break;
                }
            }

            if (padLandedOn == null)
            {
                TriggerCrash("Crashed on Unstable Lunar Crags!");
                return;
            }

            if (tiltDeg > 5.0f)
            {
                TriggerCrash($"Lander Tipped Over! (|tilt| = {tiltDeg:F1}° > 5°)");
                return;
            }

            if (impactVx > 1.5f)
            {
                TriggerCrash($"Landing Gear Sheared! (|v_x| = {impactVx:F1} m/s > 1.5)");
                return;
            }

            if (impactVy <= 1.8f)
            {
                _isLanded = true;
                _landerVel = Vector2.Zero;
                _landingsCompleted++;
                _activeTouchdownStatus = "SOFT TOUCHDOWN";

                int scoreEarned = (int)(padLandedOn.BaseScore * padLandedOn.Multiplier + _fuel * 10.0f);
                AddScore(scoreEarned);

                Hud?.ShowBannerMessage($"🦅 EAGLE LANDED! {padLandedOn.Name} (+{scoreEarned:N0} PTS)", 4.0f);
            }
            else if (impactVy <= 3.2f)
            {
                _isLanded = true;
                _landerVel = Vector2.Zero;
                _activeTouchdownStatus = "HARD TOUCHDOWN";

                int scoreEarned = (int)((padLandedOn.BaseScore * padLandedOn.Multiplier) * 0.5f);
                AddScore(scoreEarned);

                Hud?.ShowBannerMessage($"⚠️ HARD LANDING! GEAR DAMAGED (+{scoreEarned:N0} PTS)", 4.0f);
            }
            else
            {
                TriggerCrash($"Hull Disintegrated on Impact! (|v_y| = {impactVy:F1} m/s > 3.2)");
            }
        }
    }

    private void TriggerCrash(string reason)
    {
        _isCrashed = true;
        _isLanded = false;
        _landerVel = Vector2.Zero;
        _activeTouchdownStatus = "CRASHED";

        if (_landerNode != null)
        {
            _landerNode.Visible = false;
        }

        TriggerGameOver(reason);

        string playerName = LeaderboardService.Instance.ActivePlayerName;
        _latestRankAchieved = LeaderboardService.Instance.RecordScore(playerName, CurrentScore, _landingsCompleted, (int)(_fuel));
        Hud?.ShowBannerMessage($"💥 {reason.ToUpperInvariant()}", 4.5f);
    }

    public override Dictionary<string, object> GetLiveStats()
    {
        float currentAlt = Math.Max(0f, _landerPos.Y - GetTerrainHeight(_landerPos.X) - GearHeight);

        bool overPad = false;
        _radarPadsCache.Clear();
        foreach (var pad in LandingPads)
        {
            if (_landerPos.X >= pad.MinX && _landerPos.X <= pad.MaxX)
            {
                overPad = true;
            }
            _radarPadsCache.Add(new Dictionary<string, object>
            {
                { "id", pad.Id },
                { "name", pad.Name },
                { "x1", pad.MinX },
                { "x2", pad.MaxX },
                { "y", pad.SurfaceY },
                { "mult", pad.Multiplier }
            });
        }

        _radarCanistersCache.Clear();
        foreach (var c in _fuelCanisters)
        {
            _radarCanistersCache.Add(new Dictionary<string, object>
            {
                { "x", c.Pos.X },
                { "y", c.Pos.Y },
                { "collected", c.Collected }
            });
        }

        _liveStatsCache["score"] = CurrentScore;
        _liveStatsCache["state"] = GameState;
        _liveStatsCache["altitude"] = currentAlt;
        _liveStatsCache["verticalSpeed"] = _landerVel.Y;
        _liveStatsCache["horizontalSpeed"] = MathF.Abs(_landerVel.X);
        _liveStatsCache["fuel"] = _fuel;
        _liveStatsCache["tiltDeg"] = _gimbalAngleRad * (180.0f / MathF.PI);
        _liveStatsCache["tiltRoll"] = _gimbalAngleRad * (180.0f / MathF.PI);
        _liveStatsCache["planet"] = PlanetPreset;
        _liveStatsCache["gravityPreset"] = PlanetPreset;
        _liveStatsCache["gravity"] = _currentGravity;
        _liveStatsCache["gravityValue"] = _currentGravity;
        _liveStatsCache["throttle"] = _throttlePct;
        _liveStatsCache["isLanded"] = _isLanded;
        _liveStatsCache["isThrusting"] = _engineFiring;
        _liveStatsCache["overPad"] = overPad;
        _liveStatsCache["touchdownStatus"] = _activeTouchdownStatus;
        _liveStatsCache["fuelPickups"] = _fuelPickupsCollected;
        _liveStatsCache["latestRank"] = _latestRankAchieved;
        _liveStatsCache["x"] = _landerPos.X;
        _liveStatsCache["y"] = _landerPos.Y;
        _liveStatsCache["landerPos"] = new Dictionary<string, object>
        {
            { "x", _landerPos.X },
            { "y", _landerPos.Y }
        };
        _liveStatsCache["pads"] = _radarPadsCache;
        _liveStatsCache["canisters"] = _radarCanistersCache;

        return _liveStatsCache;
    }
}
