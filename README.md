# WobbleHero ⚖️ 🏃 🔬

A modular, generic **balancing board game engine & companion platform** designed for Android (and desktop) powered by **Godot 4 (.NET C#)** and controlled physically by an **M5Stack AtomS3** motion controller.

---

## 🎯 Architecture Highlights

1. **Physical Motion Controller (M5Stack AtomS3)**:
   - Taken as-is from EndlessRide with **32-byte binary UDP datagram streaming at 100 Hz**.
   - Dual-axis 6-axis IMU sensing (Bosch BMI270) with host-side complementary sensor fusion for both **Roll** (left/right lateral balance) and **Pitch** (front/back longitudinal balance).
   - Onboard Captive Portal Wi-Fi onboarding (`AtomS3-Setup` AP at `192.168.4.1`) to configure target Android IP.
   - Dynamic **Tare Zero** calibration to calibrate the board's physical resting position as true level.

2. **Modular Multi-Game Architecture (`IGameMode`)**:
   - Clean, decoupled interface enabling instant runtime game switching without restarting the engine.
   - Registry pattern (`GameRegistry`) enabling discoverable mini-games with dynamic scene mounting.
   - **3 Included Starter Games**:
     - **⚖️ Balance Trainer**: 2D marble on a tilting circular dish; guide the ball to glowing target zones while avoiding rolling off the outer edge.
     - **🏃 Wobble Runner**: Fast slalom runner; weave left and right through approaching gates while dodging hazardous obstacle pylons.
     - **🔬 Sensor Lab & Diagnostics**: Real-time 3D balance board visualizer that mirrors physical board orientation in 3D space, displaying raw vectors and packet latency.

3. **Embedded Companion Remote Web App**:
   - Zero-install smartphone companion web application (`remote.html`) served directly by Godot via .NET `HttpListener` on port `8080`.
   - Real-time bidirectional WebSocket telemetry (`/ws`) at 30 Hz.
   - **Interactive Game Selector**: Switch between installed games with a single tap from your phone.
   - **2D Bubble Level Indicator**: Live 2D target visualizer with instantaneous roll & pitch readouts.
   - **Board Controls**: One-tap Tare Zero calibration, calibration reset, sensitivity slider (0.5x to 2.5x), and axis inversion toggles.
   - **Zero-Dependency In-Game QR Code**: Godot displays a QR code on screen so players can point their phone camera to instantly connect.

4. **Zero-Hardware Fallback Emulation**:
   - When no AtomS3 hardware is active, `BalanceBoardInput` automatically provides smooth **keyboard emulation** (`A`/`D` or `Left`/`Right` for roll, `W`/`S` or `Up`/`Down` for pitch) and **touch emulation** (screen drag/touch deflection).

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
│   │   ├── balance-board-input/      # 2D tilt fusion, tare calibration & fallback input
│   │   ├── companion-app/            # Embedded HTTP/WS companion server & game switching
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
    ├── WobbleHero.csproj             # .NET 10 Godot project (remote.html embedded)
    ├── WobbleHero.sln
    ├── export_presets.cfg            # Android TV & mobile export configuration
    ├── remote.html                   # Mobile companion remote single-page web app
    ├── icon.svg & banner.svg         # Game icons and TV banner
    ├── Scenes/
    │   ├── Main.tscn                 # Main root scene (GameManager, WebServer, HUD, Input)
    │   └── Games/
    │       ├── BalanceTrainer.tscn   # Mini-game 1: Marble dish balance
    │       ├── WobbleRunner.tscn     # Mini-game 2: Slalom lane runner
    │       └── SensorLab.tscn        # Mini-game 3: 3D board visualizer & diagnostics
    └── Scripts/
        ├── Core/
        │   ├── BoardState.cs         # Immutable board telemetry struct
        │   ├── IGameMode.cs          # Universal game mode interface
        │   ├── GameRegistry.cs       # Available games discovery & metadata
        │   └── GameManager.cs        # Master game & input coordinator
        ├── Input/
        │   ├── HardwareUdpReceiver.cs# 32-byte UDP listener & 2D complementary filter
        │   └── BalanceBoardInput.cs  # Unified input with keyboard/touch fallback
        ├── Networking/
        │   └── CompanionWebServer.cs # Embedded HTTP & WebSocket server
        ├── UI/
        │   ├── CompanionHud.cs       # In-game HUD overlay with QR code & tilt bars
        │   └── QrCodeGenerator.cs    # Project Nayuki ISO/IEC 18004 QR generator
        └── Games/
            ├── BaseGameMode.cs       # Reusable base class for mini-games
            ├── BalanceTrainerGame.cs # Marble balance logic
            ├── WobbleRunnerGame.cs   # Slalom runner logic
            └── SensorLabGame.cs      # 3D board visualizer logic
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
*Controls*:
- `A` / `D` or `Left` / `Right`: Tilt board roll left / right
- `W` / `S` or `Up` / `Down`: Tilt board pitch forward / backward
- `Space` or `R`: Restart active game

### 3. Test with Virtual AtomS3 Sensor Simulator
If you do not have the physical AtomS3 controller powered on, simulate 100 Hz UDP datagrams:
```bash
python3 scripts/sensor_bridge.py --simulate --port 8080
```

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

### 4. Connect Phone Companion Remote
1. Ensure your smartphone is connected to the same Wi-Fi network.
2. Open your phone camera and scan the **QR code** on the game screen, or navigate to:
   ```text
   http://<YOUR_IP>:8080/remote
   ```
3. Use the companion app to:
   - Select and switch between **Balance Trainer**, **Wobble Runner**, or **Sensor Lab**.
   - Tap **Tare Zero** to calibrate the board horizontal level.
   - Adjust sensitivity slider.
   - Start, pause, or reset the game.

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
   }
   ```
2. Create a scene `godot/Scenes/Games/MyGame.tscn` with `MyGame` as root node.
3. Add an entry to `GameRegistry.cs`:
   ```csharp
   new("MyGame", "My New Game", "Description", "🎮", "res://Scenes/Games/MyGame.tscn")
   ```
The game will immediately appear in the smartphone Companion Remote game selector and in-game switcher!
