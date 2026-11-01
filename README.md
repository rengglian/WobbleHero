# WobbleHero ⚖️ 🏃 🌀 🏄 🦹 🛸 🧭 🧱 🏔️ 🪵 🔬

A modular, generic **balancing board game engine & companion platform** designed for Android (and desktop) powered by **Godot 4 (.NET C#)** and controlled physically by an **M5Stack AtomS3** motion controller.

---

## 🎯 Architecture Highlights

1. **Physical Motion Controller (M5Stack AtomS3)**:
   - Taken as-is from EndlessRide with **32-byte binary UDP datagram streaming at 100 Hz**.
   - Dual-axis 6-axis IMU sensing (Bosch BMI270) with host-side complementary sensor fusion for both **Roll** (left/right lateral balance) and **Pitch** (front/back longitudinal balance).
   - Tilt-compensated 9-axis compass heading fusion with support for optional I2C magnetometers (AK09918 / BMM150).
   - Physical jerk stomp detection (> 2200°/s² angular acceleration) for slap-nudging and body slams.
   - Onboard Captive Portal Wi-Fi onboarding (`AtomS3-Setup` AP at `192.168.4.1`) to configure target Android IP.
   - Dynamic **Tare Zero** calibration to calibrate the board's physical resting position as true level.

2. **Modular Multi-Game Architecture (`IGameMode`)**:
   - Clean, decoupled interface enabling instant runtime game switching without restarting the engine.
   - Registry pattern (`GameRegistry`) enabling discoverable mini-games with dynamic scene mounting.
   - **11 Included Games & Diagnostic Modes**:
     - **⚖️ Balance Trainer**: 2D marble on an enlarged tilting circular dish with bouncy rim bumpers; guide the ball to glowing target zones while avoiding rolling off the outer edge.
     - **🏃 Wobble Runner**: High-speed Endless Ride-style 3-lane slalom runner with deterministic procedural seed control (Mulberry32 PRNG), wave obstacles, Apples (burst speed) and Golden Horseshoes (score multipliers), and persistent Top-10 leaderboard.
     - **🌀 Wobble Maze**: 3D wooden labyrinth tilt puzzle with 100% solvable procedural maze generation (Mulberry32 PRNG + DFS recursive backtracker), configurable sizes (Small 9x9, Medium 13x13, Large 17x17) and difficulties (Easy, Medium, Hard), hazard pit traps with vortex pull, golden key and locked barrier gates, bonus emerald gems, and checkpoint respawning.
     - **🏄 Asteroid Surfer**: High-speed cosmic gravity wave runner with continuous momentum carving, pitch dive acceleration and aerodynamic stall braking, zero-G lip jump air-time, clean landing evaluation (|roll| & |pitch| < 4°), glowing neon shaders, asteroids, plasma rings, and black hole vortex gravity wells.
     - **🦹 Wobble Sumo**: Physics brawler in a floating circular dojo ring! Knock AI sumos out of the shrinking ring using board tilt momentum & shockwave body slams (`trigger_shockwave`), customizable difficulty (`novice`, `yokozuna`) and arena size (`compact`, `standard`, `colosseum`).
     - **🛸 Lunar Lander**: Precision vector-thrust lunar excursion module navigation! Softly touch down on subterranean crater landing pads. Board tilt controls RCS thruster vectors while pitch regulates main engine throttle. Multiple celestial gravities (`moon`, `mars`, `europa`, `earth`), emergency RCS (`trigger_emergency_rcs`), refuel pad (`refuel_lander`), and strict touchdown velocity tolerances (< 2.5 m/s horizontal, < 3.0 m/s vertical, < 8° tilt).
     - **🧭 Compass Labyrinth**: Rotate your physical board 360° to shift temple gravity! Monolith puzzle mechanics: slide heavy stone blocks onto pressure plates to open sanctuary doors, retrieve ancient relics, customizable seed (`set_compass_seed`), and manual heading rotation (`rotate_compass` / `set_compass_offset`).
     - **🧱 Gravity Pinball**: Your board is the table! Tilt gravity across multiple active pinballs, pop bumpers (+100 pts), a 4-drop target multiball bank, and rollover lanes (+50 pts, multiplier advances to 5x). Features physical slap-nudge jerk detection, 0.8s debounce, Space-key / Nudge button controls, and authentic arcade 3-strike TILT warning & freeze recovery rules.
     - **🏔️ Downhill Slalom**: High-speed alpine skiing down procedural mountain slopes! Carve tight slalom gates (blue/red), tuck forward (pitch down) for aerodynamic speed runs exceeding 120 km/h, kick off snow terrain jumps for airtime, course types (`slalom`, `giant_slalom`, `downhill`), and snow conditions (`powder`, `hardpack`, `ice`).
     - **🪵 River Log Roll**: White-water rapids lumberjack log rolling! Balance atop a rotating timber log in torrential river currents. Counter river water torque and log spin with precise lateral roll adjustments, dodge oncoming river boulders and drifting obstacles, leap to catch jumping salmon, and select difficulty classes (`rapids_class_1` to `class_4`).
     - **🔬 Sensor Lab & Diagnostics**: Real-time 3D balance board visualizer that mirrors physical board orientation in 3D space, displaying 9-axis sensor vectors (accel, gyro, compass magnetometer) and an integrated 100 Hz CSV movement recorder for tremor and filter analysis.

3. **Modular SPA Companion Remote Web App (`godot/www/`)**:
   - Zero-install smartphone companion web application served directly by Godot via .NET `HttpListener` on port `8080`.
   - **Modern Modular SPA Architecture**:
     - `index.html`: Clean HTML5 shell loading base stylesheets, navigation header, game switcher, universal action deck, and game cockpit scripts.
     - `css/base.css`, `css/components.css`, `css/games.css`: Modularized design system with responsive flex/grid layouts, radar sweeps, and dark cyber arcade theme.
     - `views/*.html`: Individual component view templates (`balance_trainer.html`, `wobble_runner.html`, `wobble_maze.html`, `asteroid_surfer.html`, `wobble_sumo.html`, `lunar_lander.html`, `compass_labyrinth.html`, `gravity_pinball.html`, `downhill_slalom.html`, `river_log_roll.html`, `sensor_lab.html`) loaded asynchronously on demand into `#views-container`.
     - `js/app.js`: Master SPA controller handling WebSocket lifecycle, view mounting, universal start/pause/reset actions, and telemetry dispatch.
     - `js/games/*.js`: Dedicated game cockpit client controllers (`surfer.js`, `sumo.js`, `lander.js`, `compass.js`, `pinball.js`, `slalom.js`, `logroll.js`) with interactive SVG radar displays and custom controls.
   - **Static File Serving & Embedded Fallback**:
     - `CompanionWebServer` resolves static files from Godot's virtual filesystem (`res://www/`) with full MIME type resolution (`.html`, `.css`, `.js`, `.json`, `.svg`, `.png`, etc.).
     - Built-in embedded assembly fallback (`WobbleHero.www.*`) ensures complete standalone operation when packaged into an Android APK or Linux binary without loose asset folders.
   - Real-time bidirectional WebSocket telemetry (`/ws`) at 30 Hz.
   - **Interactive Game Selector**: Switch between all 11 games with a single tap from your phone.
   - **Board Controls**: One-tap Tare Zero calibration, calibration reset, sensitivity slider (0.5x to 2.5x), mounting profile toggles (Top Deck vs. Underneath), and axis inversion toggles.
   - **Zero-Dependency In-Game QR Code**: Godot displays a QR code on screen so players can point their phone camera to instantly connect.

4. **Zero-Hardware Fallback Emulation**:
   - When no AtomS3 hardware is active, `BalanceBoardInput` automatically provides smooth **keyboard emulation** (`A`/`D` or `Left`/`Right` for roll, `W`/`S` or `Up`/`Down` for pitch, `Space` for Pinball nudge / Jump / Shockwave) and **touch emulation** (screen drag/touch deflection).

---

## 🕹️ Gravity Pinball Mechanics (Slap-Nudge & TILT Rules)

Gravity Pinball turns the player's balancing board directly into the tilted playfield plane ($12^\circ$ default incline, $22\text{ m/s}^2$ gravity). Up to 4 chrome pinballs can be in simultaneous play.

```
       ▲  [Rollover Lanes: L • M • R]  (+50 pts / Advance Multiplier to 5x)
      ╱ ╲
     │   │
     │ ◯ │  Pop Bumper Top
   ◯ │   │ ◯  Pop Bumpers Mid-Left & Mid-Right (+100 pts + 14 m/s Impulse)
     │   │
     │===│  4 Drop Targets [1][2][3][4] (All down triggers MULTIBALL!)
     │   │
     │ \ /
      \V/   Outhole Drain (Aperture 4.4m)
```

- **Physical Slap-Nudge Jerk Detection**:
  - The AtomS3 motion stream tracks second-order angular acceleration ($\text{jerk} = \|\vec{\alpha}\|$).
  - A physical stomp or slap exceeding **$2200^\circ/\text{s}^2$** triggers a mechanical nudge impulse, deflecting all active pinballs upward with a randomized horizontal kick.
  - On keyboard or companion remote, pressing `Space` or tapping the **⚠️ Slap Nudge** button sends the nudge command.
- **0.8s Debounce Cooldown**:
  - A mandatory $0.8\text{ s}$ debounce timer prevents a single physical slap or board rebound vibration from triggering cascading false strikes.
- **Arcade 3-Strike TILT Warning System**:
  - Each valid nudge registers a timestamp in a rolling 5-second sliding window.
  - **Strikes 1 & 2**: Visual warning on HUD banner (`⚠️ SLAP NUDGE! (WARNING X/3)`) with camera screen shake ($0.35$).
  - **Strike 3 within 5 seconds**: Triggers **🚨 TILT!** penalty.
- **TILT Freeze & Recovery**:
  - **Controls Frozen**: Active board steering is completely disabled for **$3.5\text{ seconds}$**. The gravity plane reverts strictly to the static incline slope ($\vec{g} = [0, g \sin(\theta_{\text{incline}})]$).
  - **Screen Shake**: Heavy table impact camera shake ($0.7$).
  - **Recovery**: Once the $3.5\text{ s}$ freeze timer elapses, board tilt steering is restored, the warning timestamps are cleared, and a **$1.5\text{ s}$ grace period** is granted before nudges can trigger new strikes.

---

## 📁 Repository Structure

```
WobbleHero/
├── .agents/                          # Agentic engineering context
│   ├── rules/
│   │   ├── 01-game-architecture.md   # Modularity & IGameMode standards
│   │   └── 02-android-performance.md # Android & .NET mobile performance rules
│   ├── skills/
│   │   ├── m5stack-atoms3/           # AtomS3 hardware, UDP protocol & firmware skill (as-is)
│   │   ├── balance-board-input/      # 2D tilt fusion, tare calibration, jerk detection & fallback
│   │   ├── companion-app/            # Modular SPA HTTP/WS companion server & game switching
│   │   └── godot-android/            # Godot Android export, manifest permissions & sockets
│   ├── agents/
│   │   ├── balance-game-architect.md # Mini-game mechanics & physics specialist
│   │   └── companion-web-engineer.md # Web remote & telemetry specialist
│   ├── workflows/
│   │   ├── build-android.md          # Packaging Godot Android APK
│   │   └── test-udp-bridge.md        # Testing UDP bridge and companion remote
│   └── hooks.json                    # Automated validation hooks
├── firmware/
│   └── m5stack_atoms3/
│       ├── m5stack_atoms3.ino        # AtomS3 Arduino firmware (100Hz 32B UDP streaming)
│       └── README.md                 # Flashing instructions
├── scripts/
│   ├── sensor_bridge.py              # Virtual sensor simulator & diagnostic tool
│   └── run_desktop.sh                # Desktop Godot launcher
└── godot/
    ├── project.godot                 # Godot 4 project configuration (Compatibility renderer)
    ├── WobbleHero.csproj             # .NET 10 Godot project (Embedded www resources)
    ├── WobbleHero.sln
    ├── export_presets.cfg            # Android TV & mobile export configuration
    ├── icon.svg & banner.svg         # Game icons and TV banner
    ├── www/                          # Modular Companion Single-Page Web Application
    │   ├── index.html                # App shell, navigation, and view injection container
    │   ├── css/
    │   │   ├── base.css              # Cyberpunk arcade theme, typography & resets
    │   │   ├── components.css        # Buttons, cards, modals, sliders, and badges
    │   │   └── games.css             # Game-specific radar displays & cockpits
    │   ├── js/
    │   │   ├── app.js                # Core SPA lifecycle, WebSocket client & view loader
    │   │   └── games/                # Modular mini-game cockpit controllers
    │   │       ├── surfer.js         # Asteroid Surfer cosmic wave HUD & speed controls
    │   │       ├── sumo.js           # Wobble Sumo ring radar & shockwave slam button
    │   │       ├── lander.js         # Lunar Lander telemetry, pads radar & RCS buttons
    │   │       ├── compass.js        # Compass Labyrinth 360° heading wheel & monoliths
    │   │       ├── pinball.js        # Gravity Pinball live playfield radar & slap nudge
    │   │       ├── slalom.js         # Downhill Slalom ski tuck & course selectors
    │   │       └── logroll.js        # River Log Roll balance bar & rapids radar
    │   └── views/                    # Asynchronously loaded component views
    │       ├── balance_trainer.html  # 2D marble bowl & calibration controls
    │       ├── wobble_runner.html    # Slalom steering bar, seed & leaderboard
    │       ├── wobble_maze.html      # 3D labyrinth timer, gems, size & difficulty
    │       ├── asteroid_surfer.html  # Wave bank visualizer, speed & density
    │       ├── wobble_sumo.html      # Circular dojo radar, shockwave & arena config
    │       ├── lunar_lander.html     # Lander altitude, pad radar, fuel & thrusters
    │       ├── compass_labyrinth.html# Cardinal compass rose & relic tracker
    │       ├── gravity_pinball.html  # Pinball playfield, drop targets & nudge
    │       ├── downhill_slalom.html  # Ski speedometer, tuck gauge & snow weather
    │       ├── river_log_roll.html   # Log spin RPM, rapids radar & salmon counter
    │       └── sensor_lab.html       # 9-axis diagnostic meters & 100 Hz CSV recorder
    ├── Scenes/
    │   ├── Main.tscn                 # Main root scene (GameManager, WebServer, Input)
    │   ├── Games/                    # 11 Mini-Game Scenes
    │   │   ├── BalanceTrainer.tscn   # 1: Marble dish balance
    │   │   ├── WobbleRunner.tscn     # 2: 3-lane slalom runner
    │   │   ├── WobbleMaze.tscn       # 3: Procedural labyrinth marble tilt
    │   │   ├── AsteroidSurfer.tscn   # 4: Cosmic wave momentum carving
    │   │   ├── WobbleSumo.tscn       # 5: Floating dojo ring brawler
    │   │   ├── LunarLander.tscn      # 6: Vector-thrust crater landing
    │   │   ├── CompassLabyrinth.tscn # 7: 360° gravity shifting temple puzzle
    │   │   ├── GravityPinball.tscn   # 8: Tilt gravity arcade pinball
    │   │   ├── DownhillSlalom.tscn   # 9: Alpine ski speed carving
    │   │   ├── RiverLogRoll.tscn     # 10: White-water lumberjack log balance
    │   │   └── SensorLab.tscn        # 11: 3D board visualizer & diagnostics
    │   └── UI/                       # In-Game Heads-Up Displays
    │       ├── AsteroidSurferHud.tscn
    │       ├── BalanceTrainerHud.tscn
    │       ├── BoardTiltIndicator.tscn
    │       ├── CompanionHud.tscn
    │       ├── CompanionQrCard.tscn
    │       ├── CompassLabyrinthHud.tscn
    │       ├── DownhillSlalomHud.tscn
    │       ├── GravityPinballHud.tscn
    │       ├── LunarLanderHud.tscn
    │       ├── RiverLogRollHud.tscn
    │       ├── SensorLabHud.tscn
    │       ├── WobbleMazeHud.tscn
    │       └── WobbleRunnerHud.tscn
    ├── Shaders/
    │   └── cosmic_wave.gdshader      # Pulsating neon UV energy wave shader
    └── Scripts/
        ├── Core/
        │   ├── BoardState.cs         # Immutable board telemetry struct (9-axis)
        │   ├── IGameMode.cs          # Universal game mode interface
        │   ├── GameRegistry.cs       # Available games discovery & metadata (11 games)
        │   ├── LeaderboardService.cs # Persistent Top-10 leaderboard manager
        │   ├── MazeGenerator.cs      # Procedural solvable labyrinth generator (Mulberry32)
        │   └── GameManager.cs        # Master game & input coordinator
        ├── Input/
        │   ├── HardwareUdpReceiver.cs# 32-byte UDP listener, 2D complementary filter & tare
        │   └── BalanceBoardInput.cs  # Unified input with keyboard/touch fallback
        ├── Networking/
        │   └── CompanionWebServer.cs # HTTP & WebSocket server with static file serving & fallback
        ├── UI/
        │   ├── AsteroidSurferHud.cs
        │   ├── BalanceTrainerHud.cs
        │   ├── BaseGameHud.cs        # Shared HUD base class (banners, stats, tilt puck)
        │   ├── BoardTiltIndicator.cs # 2D tilt indicator gauge
        │   ├── CompanionHud.cs       # Master HUD & QR code controller
        │   ├── CompanionQrCard.cs    # In-game QR connection card
        │   ├── CompassLabyrinthHud.cs
        │   ├── DownhillSlalomHud.cs
        │   ├── GravityPinballHud.cs
        │   ├── LunarLanderHud.cs
        │   ├── QrCodeGenerator.cs    # Project Nayuki ISO/IEC 18004 QR generator
        │   ├── RiverLogRollHud.cs
        │   ├── SensorLabHud.cs
        │   ├── WobbleMazeHud.cs
        │   └── WobbleRunnerHud.cs
        └── Games/
            ├── BaseGameMode.cs       # Reusable base class for mini-games
            ├── AsteroidSurferGame.cs # Cosmic wave carving & air-time mechanics
            ├── BalanceTrainerGame.cs # Marble dish balance logic
            ├── CompassLabyrinthGame.cs# 360° temple gravity rotation puzzle
            ├── DownhillSlalomGame.cs # Alpine slalom speed skiing & kickers
            ├── GravityPinballGame.cs # Tilt gravity pinball, bumpers & slap-nudge
            ├── LunarLanderGame.cs    # RCS thrust vector crater excursion
            ├── RiverLogRollGame.cs   # Rapids lumberjack roll & boulder dodging
            ├── SensorLabGame.cs      # 3D board visualizer & movement recorder
            ├── WobbleMazeGame.cs     # 3D labyrinth tilt, key, gate & trap physics
            ├── WobbleRunnerGame.cs   # 3-lane slalom runner logic & waves
            └── WobbleSumoGame.cs     # Physics sumo brawler & shockwave slams
```

---

## 🚀 Quick Start Guide

### 1. Build and Deploy (Android & Desktop)
Use the unified build & deploy script:
```bash
# Full build and deploy to connected Android device / Android TV
./scripts/build_and_deploy.sh

# Build APK only (without deploying)
./scripts/build_and_deploy.sh --build-only

# Deploy, launch, and stream live Godot / C# logcat
./scripts/build_and_deploy.sh --logcat

# Build Linux desktop executable
./scripts/build_and_deploy.sh --desktop
```

### 2. Run on Desktop (Development Mode)
```bash
./scripts/run_desktop.sh
```
*Keyboard & Gamepad Controls*:
- `A` / `D` or `Left` / `Right`: Tilt board roll left / right
- `W` / `S` or `Up` / `Down`: Tilt board pitch forward / backward
- `Space`: Slap-Nudge (Pinball) / Jump (Log Roll & Slalom) / Shockwave Slam (Sumo) / Restart Game
- `R`: Quick restart active game

### 3. Connect Phone Companion Remote
1. Ensure your smartphone is connected to the same Wi-Fi network as your host device.
2. Open your phone camera and scan the **QR code** on the game screen, or navigate to:
   ```text
   http://<YOUR_IP>:8080/remote
   ```
3. The companion remote automatically mounts the active game's view:
   - **Balance Trainer**: Live 2D balance puck, roll/pitch readouts, Tare Zero calibration, sensitivity slider, and invert toggles.
   - **Wobble Runner**: Slalom steering lean bar, player name input, deterministic seed input + `🎲 Random Seed`, and live Top-10 Leaderboard.
   - **Wobble Maze**: Live timer, gems collected, key status, falls counter, deterministic seed input + `🎲 Random Seed`, maze size toggles (Small, Medium, Large), and difficulty selector.
   - **Asteroid Surfer**: Live speed, multiplier, ballistic air-time duration, cosmic dust, 2D interactive wave carve visualizer with bank angle and clean landing indicator, wave speed slider, and cosmic density selector.
   - **Wobble Sumo**: Circular dojo radar displaying player position and relative opponent positions, ring integrity gauge, knockout counter, and instant `⚡ Trigger Shockwave` body slam button.
   - **Lunar Lander**: Flight telemetry (altitude, vertical & horizontal velocity, pitch gimbal), crater pad radar, celestial gravity presets (Moon, Mars, Europa, Earth), `🚨 Emergency RCS` stabilization, and `⛽ Refuel` pad buttons.
   - **Compass Labyrinth**: 360° interactive compass rose, active cardinal gravity plane readout, monolith block & relic radar, seed input, and `↺ 90°` / `↻ 90°` manual rotation buttons.
   - **Gravity Pinball**: Live playfield mini-radar with ball positions & velocities, drop target bank status, bumper hit counter, multiplier gauge, table incline slider ($5^\circ \dots 25^\circ$), restitution slider, and `⚠️ Slap Nudge` button.
   - **Downhill Slalom**: Speedometer (km/h), top speed, distance meter, gates cleared/missed, tuck posture indicator, course presets (`slalom`, `giant_slalom`, `downhill`), and snow weather presets (`powder`, `hardpack`, `ice`).
   - **River Log Roll**: River velocity, log spin RPM, equilibrium deviation meter, sweet-spot status, salmon catch counter, rapids difficulty selector, and live river hazard radar (boulders, driftwood, salmon).
   - **Sensor Lab**: 100 Hz Raw IMU movement recorder, CSV download button, and live 9-axis sensor diagnostics (heading, accel, gyro, mag, packet rate).

### 4. Movement Recording & Filter Evaluation (SensorLab)
To capture real movement data while standing on your balancing board:
1. In the game or companion app, select **🔬 Sensor Lab**.
2. Tap **⏺️ Start Recording Session** on your phone (or press `Space`/`R` on desktop).
3. Perform standing balance, deliberate leans, or holding steady.
4. Tap **⏹️ Stop Recording**; tap **📥 Download CSV Session** right on your phone!
5. Run the analyzer script to evaluate tremor spectra and filter simulations:
   ```bash
   python3 scripts/analyze_session.py
   ```
   *Outputs packet arrival jitter, ankle tremor frequency peaks (FFT), and simulated filter metrics.*

### 5. Flash the M5Stack AtomS3 Controller
1. Open `firmware/m5stack_atoms3/m5stack_atoms3.ino` in Arduino IDE.
2. Install `M5Unified` and `M5GFX` libraries.
3. Flash to the AtomS3 via USB-C.
4. Connect to Wi-Fi `AtomS3-Setup` from your phone to input your Wi-Fi SSID, password, and Android device IP.

---

## 🛠️ Adding New Balancing Board Mini-Games

Creating a new game is straightforward:

1. Create a script in `godot/Scripts/Games/MyGame.cs` inheriting `BaseGameMode`:
   ```csharp
   public partial class MyGame : BaseGameMode
   {
       public override string GameId => "MyGame";
       public override string GameTitle => "My New Game";
       public override string GameDescription => "Balance to win!";
       public override string GameIcon => "🎮";

       public override void UpdateBoardInput(BoardState boardState, double delta)
       {
           // Use boardState.Roll (-1.0 to 1.0) and boardState.Pitch (-1.0 to 1.0)
       }

       public override Dictionary<string, object> GetLiveStats()
       {
           var stats = base.GetLiveStats();
           stats["customMetric"] = 42;
           return stats;
       }
   }
   ```
2. Create a scene `godot/Scenes/Games/MyGame.tscn` with `MyGame` as root node.
3. Add an entry to `GameRegistry.cs`:
   ```csharp
   new("MyGame", "My New Game", "Description", "🎮", "res://Scenes/Games/MyGame.tscn")
   ```
4. Optionally add a component view in `godot/www/views/my_game.html` and companion cockpit module in `godot/www/js/games/my_game.js`.

The game will immediately appear in the smartphone Companion Remote game selector and in-game switcher!
