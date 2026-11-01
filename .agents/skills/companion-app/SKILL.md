---
name: companion-app
description: >-
  Architecture, protocol specification, and REST/WebSocket message schema for the WobbleHero companion web server. Explains dynamic game switching, live 2D bubble level telemetry, calibration commands, and zero-install phone remote interface.
---

# WobbleHero Companion App Skill

This skill documents the embedded HTTP & WebSocket server implementation in Godot C#, protocol formats, message structures, static file serving with embedded assembly fallback, and the responsive modular Single-Page Application (SPA) companion remote.

---

## 1. Network Topology & Discovery

```
   ┌─────────────────────────────────────────────────────────────┐
   │                     Host Device (Android/PC)                │
   │  ┌───────────────────────────────────────────────────────┐  │
   │  │                 Godot 4 Game Engine                   │  │
   │  │  ┌───────────────────┐       ┌─────────────────────┐  │  │
   │  │  │HardwareUdpReceiver│       │ CompanionWebServer  │  │  │
   │  │  │  (UDP Port 8080)  │       │ (HTTP/WS Port 8080) │  │  │
   │  │  └────────▲──────────┘       └──────────▲──────────┘  │  │
   │  └───────────┼─────────────────────────────┼─────────────┘  │
   └──────────────┼─────────────────────────────┼────────────────┘
                  │ 100 Hz Raw 32B UDP          │ HTTP / WebSocket
                  │                             │ (SPA: /www/ & /ws)
        ┌─────────┴─────────┐         ┌─────────┴─────────┐
        │  M5Stack AtomS3   │         │ Smartphone Remote │
        │ (Balancing Board) │         │ (Browser Client)  │
        └───────────────────┘         └───────────────────┘
```

1. **Embedded Web Server**: Runs directly inside the Godot application using .NET `HttpListener` on port `8080` (with automatic fallback to alternate port `8082` if UDP port contention occurs).
2. **Static File Serving & Fallback**:
   - Primary: Serves modular web assets from the Godot virtual filesystem (`res://www/` with recursive file routing).
   - Embedded Assembly Fallback: Automatically resolves files packaged inside the `.NET` assembly manifests (`WobbleHero.www.*` or legacy `WobbleHero.remote.html`), ensuring standalone operation on Android APKs without loose files.
3. **QR Code Discovery**: Godot renders an ISO/IEC 18004 compliant QR code containing `http://<lan-ip>:8080/remote`.

---

## 2. WebSocket Protocol Schema

The companion app maintains a persistent WebSocket connection to `/ws` (or `/socket`).

### 2.1 Server-to-Client Telemetry (`telemetry`)
Broadcasted to all connected smartphones at 25–50 Hz:
```json
{
  "type": "telemetry",
  "board": {
    "roll": -0.15,
    "pitch": 0.05,
    "fusedRollDeg": -4.2,
    "fusedPitchDeg": 1.4,
    "calOffsetRoll": 0.5,
    "calOffsetPitch": 0.0,
    "isActive": true,
    "hz": 99,
    "seq": 4812,
    "isButtonPressed": false,
    "hasMag": true,
    "compassHeadingDeg": 142.0,
    "rawMag": { "x": 12.4, "y": -4.1, "z": 45.2 },
    "rawAccel": { "x": -0.07, "y": 0.02, "z": 0.99 },
    "rawGyro": { "x": 1.2, "y": -0.8, "z": 0.1 },
    "source": "atoms3",
    "deviceIp": "192.168.1.145"
  },
  "game": {
    "activeGameId": "GravityPinball",
    "activeGameTitle": "Gravity Pinball",
    "state": "RUNNING",
    "score": 4850,
    "stats": {
      /* Dynamic stats specific to active mini-game (see Section 2.2) */
    }
  },
  "availableGames": [
    { "id": "BalanceTrainer", "title": "Balance Trainer", "icon": "⚖️", "description": "Tilt to keep the marble in scoring zones" },
    { "id": "WobbleRunner", "title": "Wobble Runner", "icon": "🏃", "description": "Slalom left and right through approaching gates" },
    { "id": "WobbleMaze", "title": "Wobble Maze", "icon": "🌀", "description": "Procedural labyrinth tilt puzzle with traps & keys" },
    { "id": "AsteroidSurfer", "title": "Asteroid Surfer", "icon": "🏄", "description": "Cosmic gravity waves & zero-G airtime" },
    { "id": "WobbleSumo", "title": "Wobble Sumo", "icon": "🦹", "description": "Floating dojo brawler with shockwave body slams" },
    { "id": "LunarLander", "title": "Lunar Lander", "icon": "🛸", "description": "Vector-thrust excursion module crater landing" },
    { "id": "CompassLabyrinth", "title": "Compass Labyrinth", "icon": "🧭", "description": "360° gravity shifting temple puzzle" },
    { "id": "GravityPinball", "title": "Gravity Pinball", "icon": "🧱", "description": "Tilt gravity pinball table with pop bumpers & nudge" },
    { "id": "DownhillSlalom", "title": "Downhill Slalom", "icon": "🏔️", "description": "High-speed alpine skiing & speed tucks" },
    { "id": "RiverLogRoll", "title": "River Log Roll", "icon": "🪵", "description": "White-water rapids log balancing & salmon catch" },
    { "id": "SensorLab", "title": "Sensor Lab & Diagnostics", "icon": "🔬", "description": "3D board visualizer and IMU calibration bench" }
  ],
  "leaderboard": [
    { "rank": 1, "playerName": "Hero 1", "score": 4520, "distance": 1820, "seed": 1337, "date": "2026-11-01" }
  ],
  "settings": {
    "sensitivity": 1.0,
    "invertRoll": false,
    "invertPitch": false,
    "playerName": "Hero 1",
    "mountingProfile": "top"
  }
}
```

### 2.2 Game-Specific Telemetry `game.stats` Properties

Each mini-game's `GetLiveStats()` generates a dedicated dictionary streamed in `game.stats`:

| Game ID | Distinct `game.stats` Fields | Description |
| :--- | :--- | :--- |
| **BalanceTrainer** | `timeInZone`, `zoneMultiplier`, `falls`, `difficulty` | Target zone hold timer and fall count |
| **WobbleRunner** | `distance`, `speed`, `multiplier`, `seed`, `apples`, `horseshoes` | Track progression, PRNG seed, and pickups |
| **WobbleMaze** | `time`, `gems`, `hasKey`, `falls`, `seed`, `size`, `difficulty` | Solvability timer, gem count, and gate status |
| **AsteroidSurfer** | `speed`, `multiplier`, `airTime`, `dust`, `bankAngle`, `cleanLanding` | Ballistic flight duration and landing evaluation |
| **WobbleSumo** | `wave`, `knockouts`, `opponentsRemaining`, `lives`, `ringIntegrity`, `ringRadius`, `maxRingRadius`, `shockwaveCooldown`, `shockwaveReady`, `shockwaveActive`, `jerk`, `difficulty`, `arenaSize`, `playerPos` (`x`,`y`), `opponents` (`[{x, y, mass, kind, alive}]`) | Shrinking dojo ring radius, shockwave readiness, opponent radar list |
| **LunarLander** | `altitude`, `verticalSpeed`, `horizontalSpeed`, `fuel`, `tiltDeg`, `planet`, `gravity`, `throttle`, `isLanded`, `isThrusting`, `overPad`, `touchdownStatus`, `fuelPickups`, `landerPos` (`x`,`y`), `pads` (`[{id, name, x1, x2, y, mult}]`), `canisters` (`[{x, y, collected}]`) | Radar pads coordinates, fuel reserves, descent velocity meters |
| **CompassLabyrinth**| `headingDeg`, `heading`, `compassOffset`, `cardinalPlane` (`NORTH`/`EAST`/`SOUTH`/`WEST`), `relicsCollected`, `totalRelics`, `platesSolved`, `totalPlates`, `lives`, `playerPos` (`x`,`y`), `blocks` (`[{x, y}]`), `relics` (`[{x, y, collected}]`), `seed` | 360° gravity plane alignment, sliding monolith coordinates, pressure plates |
| **GravityPinball** | `activeBalls`, `reserveBalls`, `multiplier`, `bumpersHit`, `dropTargetsDown`, `tiltWarnings`, `isTilted`, `isTiltLocked`, `tableIncline`, `restitution`, `balls` (`[{x, y, vx, vy}]`), `targets` (`[{id, active}]`) | Chrome balls radar coordinates & velocities, drop target status, tilt warning counter |
| **DownhillSlalom** | `speedKmh`, `topSpeedKmh`, `distance`, `gatesCleared`, `gatesMissed`, `multiplier`, `courseType`, `snowCondition`, `isTucking`, `isAirborne` | Alpine speedometer, aerodynamic tuck status, gate flags |
| **RiverLogRoll** | `riverSpeedKmh`, `logRpm`, `balanceErrorDeg`, `equilibriumDev`, `targetPitchDeg`, `isInSweetSpot`, `isSlipping`, `multiplier`, `distance`, `salmonCollected`, `lives`, `difficulty`, `obstacles` (`[{type, x, y}]`) | Timber log rotational RPM, sweet-spot status, floating river obstacles |
| **SensorLab** | `isRecording`, `recordingDuration`, `sampleCount`, `sessionCsvAvailable`, `heading`, `accel`, `gyro`, `mag`, `hz`, `source` | 100 Hz CSV recording status and 9-axis vector metrics |

---

### 2.3 Client-to-Server Commands

Clients issue JSON action payloads over WebSocket:

| Command `type` | Payload Fields | Target Mini-Game / System | Purpose |
| :--- | :--- | :--- | :--- |
| `switch_game` | `{"gameId": "GravityPinball"}` | Global | Instantly switches active game mode in Godot |
| `start_game` | `{}` | Global | Starts or restarts current game mode |
| `pause_game` | `{}` | Global | Pauses gameplay physics and timers |
| `resume_game` | `{}` | Global | Resumes gameplay physics |
| `reset_game` | `{}` | Global | Resets score and game entities to initial state |
| `tare_board` | `{}` | Input / Global | Calibrates physical board resting tilt as horizontal zero |
| `reset_cal` | `{}` | Input / Global | Resets tare calibration offsets to 0.0° |
| `set_sensitivity` | `{"value": 1.5}` | Input / Global | Adjusts input sensitivity multiplier ($0.5 \dots 2.5$) |
| `toggle_invert` | `{"axis": "roll"}` | Input / Global | Toggles roll or pitch axis inversion |
| `set_mounting_profile`| `{"profile": "underneath"}` | Input / Global | Sets "top" (uninverted) or "underneath" (invert roll & pitch) |
| `set_player_name` | `{"playerName": "Alex"}` | Leaderboard | Updates active player name for high-score records |
| `reset_leaderboard` | `{}` | Leaderboard | Clears persistent top-10 leaderboard |
| `set_seed` | `{"seed": 1337}` | WobbleRunner | Sets deterministic Mulberry32 procedural seed |
| `set_maze_config` | `{"seed": 42, "size": "medium", "difficulty": "hard"}` | WobbleMaze | Configures labyrinth seed, grid size, and difficulty |
| `set_wave_speed` | `{"speed": 35.0}` | AsteroidSurfer | Sets cosmic gravity wave velocity ($15 \dots 50\text{ km/h}$) |
| `set_wave_density` | `{"density": "high"}` | AsteroidSurfer | Sets asteroid field density (`low`, `medium`, `high`) |
| `set_surfer_config` | `{"speed": 28.0, "density": "medium"}` | AsteroidSurfer | Configures surfer speed and obstacle density together |
| `set_sumo_difficulty`| `{"difficulty": "yokozuna"}` | WobbleSumo | Sets AI aggressiveness (`novice`, `yokozuna`) |
| `set_sumo_arena_size`| `{"arenaSize": "colosseum"}` | WobbleSumo | Sets dojo size (`compact`, `standard`, `colosseum`) |
| `set_sumo_config` | `{"difficulty": "...", "arenaSize": "..."}` | WobbleSumo | Configures sumo difficulty and arena size |
| `trigger_shockwave` | `{}` | WobbleSumo | Triggers radial dojo shockwave body slam |
| `set_lander_gravity` | `{"planet": "mars"}` | LunarLander | Sets celestial gravity (`moon`, `mars`, `europa`, `earth`) |
| `trigger_emergency_rcs` | `{}` | LunarLander | Fires emergency reaction control thrusters to halt spin |
| `refuel_lander` | `{}` | LunarLander | Refuels excursion module tanks |
| `set_compass_offset` | `{"offset": 90.0}` | CompassLabyrinth | Sets manual degree offset for compass heading |
| `rotate_compass` | `{"delta": 90.0}` | CompassLabyrinth | Rotates temple gravity plane by relative angle (e.g., ±90°) |
| `set_compass_seed` | `{"seed": 2048}` | CompassLabyrinth | Sets deterministic dungeon layout seed |
| `launch_pinball` | `{}` | GravityPinball | Launches a reserve ball onto the table |
| `nudge_table` | `{}` | GravityPinball | Delivers slap-nudge impulse (with 0.8s debounce & TILT rules) |
| `set_table_incline` | `{"incline": 14.0}` | GravityPinball | Sets table slope angle ($5^\circ \dots 25^\circ$) |
| `set_pinball_restitution`| `{"restitution": 0.88}` | GravityPinball | Sets bumper bounce restitution ($0.5 \dots 1.0$) |
| `set_slalom_course` | `{"course": "downhill"}` | DownhillSlalom | Sets course (`slalom`, `giant_slalom`, `downhill`) |
| `set_slalom_snow` | `{"snow": "powder"}` | DownhillSlalom | Sets snow physics (`powder`, `hardpack`, `ice`) |
| `set_slalom_config` | `{"course": "...", "snow": "..."}` | DownhillSlalom | Configures slalom course and weather conditions |
| `set_river_diff` | `{"diff": "rapids_class_3"}` | RiverLogRoll | Sets river rapids difficulty (`class_1` to `class_4`) |
| `start_recording` | `{}` | SensorLab | Starts 100 Hz CSV movement recording session |
| `stop_recording` | `{}` | SensorLab | Finalizes movement recording session |

---

## 3. HTTP REST Endpoints

| Endpoint | Method | Response | Purpose |
| :--- | :--- | :--- | :--- |
| `/` or `/remote` | `GET` | `text/html` | Serves modular SPA shell (`index.html`) |
| `/health` | `GET` | `application/json` | Health check (`{"status":"ok","service":"WobbleHero"}`) |
| `/api/leaderboard` | `GET` | `application/json` | Returns Top-10 persistent leaderboard entries |
| `/api/recording/latest.csv` | `GET` | `text/csv` | Downloads latest 100 Hz IMU session as CSV file |
| `/{path}` | `GET` | Mapped MIME | Serves static assets (`/css/*`, `/js/*`, `/views/*`) |

### Static File Serving Resolution Order
1. **Godot Virtual Filesystem (`res://www/{relPath}`)**: Reads directly via `Godot.FileAccess` with automatic MIME detection (`.html`, `.css`, `.js`, `.json`, `.svg`, `.png`, `.jpg`, `.woff2`, etc.).
2. **Embedded Assembly Fallback (`WobbleHero.www.*`)**: Reads manifest resource stream packaged inside the C# binary (e.g. `WobbleHero.www.css.base.css`), guaranteeing zero-configuration execution on Android APKs.
3. **Legacy HTML Fallback (`res://remote.html` or `WobbleHero.remote.html`)**: Fallback for standalone single-file legacy deployments.

---

## 4. Modular SPA Companion Architecture (`godot/www/`)

The companion application is structured as a decoupled Single-Page Application (SPA):

```
godot/www/
├── index.html                # App shell, navigation, and view injection container
├── css/
│   ├── base.css              # Cyberpunk arcade theme, typography & resets
│   ├── components.css        # Buttons, cards, modals, sliders, and badges
│   └── games.css             # Game-specific radar displays & cockpits
├── js/
│   ├── app.js                # Core SPA lifecycle, WebSocket client & view loader
│   └── games/                # Modular mini-game cockpit controllers
│       ├── surfer.js         # Asteroid Surfer cosmic wave HUD & speed controls
│       ├── sumo.js           # Wobble Sumo ring radar & shockwave slam button
│       ├── lander.js         # Lunar Lander telemetry, pads radar & RCS buttons
│       ├── compass.js        # Compass Labyrinth 360° heading wheel & monoliths
│       ├── pinball.js        # Gravity Pinball live playfield radar & slap nudge
│       ├── slalom.js         # Downhill Slalom ski tuck & course selectors
│       └── logroll.js        # River Log Roll balance bar & rapids radar
└── views/                    # Asynchronously loaded component views
    ├── balance_trainer.html  # 2D marble bowl & calibration controls
    ├── wobble_runner.html    # Slalom steering bar, seed & leaderboard
    ├── wobble_maze.html      # 3D labyrinth timer, gems, size & difficulty
    ├── asteroid_surfer.html  # Wave bank visualizer, speed & density
    ├── wobble_sumo.html      # Circular dojo radar, shockwave & arena config
    ├── lunar_lander.html     # Lander altitude, pad radar, fuel & thrusters
    ├── compass_labyrinth.html# Cardinal compass rose & relic tracker
    ├── gravity_pinball.html  # Pinball playfield, drop targets & nudge
    ├── downhill_slalom.html  # Ski speedometer, tuck gauge & snow weather
    ├── river_log_roll.html   # Log spin RPM, rapids radar & salmon counter
    └── sensor_lab.html       # 9-axis diagnostic meters & 100 Hz CSV recorder
```

### Lifecycle & View Injection (`app.js`)
1. On `DOMContentLoaded`, `loadAllViews()` executes concurrent asynchronous `fetch()` requests for each view in `GAME_VIEWS` (`/views/*.html`) and appends them to `#views-container`.
2. When the WebSocket client connects and receives a `telemetry` packet:
   - If `game.activeGameId` has changed, `switchActiveGameView()` activates the matching view (`display: flex`) and hides all inactive views (`display: none`).
   - The shell updates the game title badge and the universal **Start / Pause / Reset** deck.
   - Active cockpit modules (`surfer.js`, `pinball.js`, `sumo.js`, etc.) draw real-time vector radars and update specialized telemetry gauges without DOM thrashing.
