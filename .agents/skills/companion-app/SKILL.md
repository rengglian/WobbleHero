---
name: companion-app
description: >-
  Architecture, protocol specification, and REST/WebSocket message schema for the WobbleHero companion web server. Explains dynamic game switching, live 2D bubble level telemetry, calibration commands, and zero-install phone remote interface.
---

# WobbleHero Companion App Skill

This skill documents the embedded HTTP & WebSocket server implementation in Godot C#, protocol formats, message structures, and the responsive smartphone companion remote.

---

## 1. Network Topology & Discovery

```
   ┌─────────────────────────────────────────────────────────────┐
   │                     Android Device                          │
   │  ┌───────────────────────────────────────────────────────┐  │
   │  │                 Godot 4 Game Engine                   │  │
   │  │  ┌───────────────────┐       ┌─────────────────────┐  │  │
   │  │  │HardwareUdpReceiver│       │ CompanionWebServer  │  │  │
   │  │  │  (UDP Port 8080)  │       │(HTTP/WS Port 8080/1)│  │  │
   │  │  └────────▲──────────┘       └──────────▲──────────┘  │  │
   │  └───────────┼─────────────────────────────┼─────────────┘  │
   └──────────────┼─────────────────────────────┼────────────────┘
                  │ 100 Hz Raw 32B UDP          │ HTTP / WebSocket
                  │                             │ (remote.html)
        ┌─────────┴─────────┐         ┌─────────┴─────────┐
        │  M5Stack AtomS3   │         │ Smartphone Remote │
        │ (Balancing Board) │         │ (Browser Client)  │
        └───────────────────┘         └───────────────────┘
```

1. **Embedded Web Server**: Runs directly inside the Godot application using .NET `HttpListener` on port `8080` (or `8082` if UDP shares `8080`).
2. **Local Static Serving**: Serves `remote.html` from embedded assembly resource or Godot `res://` filesystem.
3. **QR Code Discovery**: Godot renders an ISO/IEC 18004 compliant QR code containing `http://<lan-ip>:8080/remote`.

---

## 2. WebSocket Protocol Schema

### 2.1 Server-to-Client Telemetry (`telemetry`)
Broadcasted to all connected smartphones at 25-50 Hz:
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
    "activeGameId": "WobbleRunner",
    "activeGameTitle": "Wobble Runner",
    "state": "RUNNING",
    "score": 1450,
    "stats": {
      "distance": 840,
      "speed": 72,
      "multiplier": 3,
      "seed": 1337,
      "apples": 4,
      "horseshoes": 2
    }
  },
  "availableGames": [
    { "id": "BalanceTrainer", "title": "Balance Trainer", "icon": "⚖️", "description": "Keep the ball centered on the tilting plate" },
    { "id": "WobbleRunner", "title": "Wobble Runner", "icon": "🏃", "description": "3-lane slalom runner with seed control & leaderboard" },
    { "id": "WobbleMaze", "title": "Wobble Maze", "icon": "🌀", "description": "3D procedural labyrinth marble puzzle with traps, keys & gems" },
    { "id": "SensorLab", "title": "Sensor Lab", "icon": "🔬", "description": "3D board diagnostics and 100 Hz CSV recorder" }
  ],
  "leaderboard": [
    { "rank": 1, "playerName": "Hero 1", "score": 4520, "distance": 1820, "seed": 1337, "date": "2026-11-01" }
  ],
  "settings": {
    "sensitivity": 1.0,
    "invertRoll": false,
    "invertPitch": false,
    "playerName": "Hero 1"
  }
}
```

### 2.2 Client-to-Server Commands

| Command `type` | Payload Fields | Purpose |
| :--- | :--- | :--- |
| `switch_game` | `{"gameId": "WobbleRunner"}` | Instantly switches the active game mode in Godot |
| `start_game` | `{}` | Starts or restarts the active game |
| `pause_game` | `{}` | Pauses gameplay physics |
| `resume_game` | `{}` | Resumes gameplay |
| `reset_game` | `{}` | Resets the active game state |
| `tare_board` | `{}` | Calibrates current board tilt as horizontal zero |
| `reset_cal` | `{}` | Resets calibration tare offsets |
| `set_sensitivity` | `{"value": 1.5}` | Adjusts input sensitivity multiplier ($0.5 \dots 2.5$) |
| `toggle_invert` | `{"axis": "roll"}` | Toggles roll or pitch axis inversion |
| `set_seed` | `{"seed": 42}` | Sets deterministic Mulberry32 seed for Wobble Runner |
| `set_maze_config` | `{"seed": 1337, "size": "medium", "difficulty": "hard"}` | Configures Wobble Maze seed, grid size (small/medium/large), and difficulty |
| `set_player_name` | `{"playerName": "Alex"}` | Updates player profile name for leaderboard records |
| `reset_leaderboard` | `{}` | Clears persistent top-10 leaderboard records |
| `start_recording` | `{}` | Begins 100 Hz movement recording session in SensorLab |
| `stop_recording` | `{}` | Finalizes movement recording session |

---

## 3. HTTP REST Endpoints

| Endpoint | Method | Response | Purpose |
| :--- | :--- | :--- | :--- |
| `/` or `/remote` | `GET` | `text/html` | Serves the single-page companion remote web app |
| `/health` | `GET` | `application/json` | Health check (`{"status":"ok","service":"WobbleHero"}`) |
| `/api/leaderboard` | `GET` | `application/json` | Returns Top-10 persistent leaderboard entries |
| `/api/recording/latest.csv` | `GET` | `text/csv` | Downloads latest 100 Hz IMU session as CSV file |

---

## 4. Context-Aware Modular UI Architecture (`remote.html`)

To eliminate clutter on mobile devices, `remote.html` uses a **context-aware modular view architecture**:
- **Universal Shell**: Brand, WebSocket live status, interactive Game Switcher tabs, and Universal Action buttons (`Start`/`Pause`/`Reset`).
- **View Container (`.game-view`)**: Each game has a dedicated container (`view-BalanceTrainer`, `view-WobbleRunner`, `view-WobbleMaze`, `view-SensorLab`) where only the active game's view is rendered (`display: flex`).
- **Dynamic Content**:
  - `BalanceTrainer`: Shows 2D bubble puck level, roll/pitch degrees, Tare Zero, sensitivity slider, and invert toggles.
  - `WobbleRunner`: Shows 3-lane slalom steering bar with real-time lean puck, player name input, deterministic seed input + 🎲 Random button, and the Top-10 leaderboard table.
  - `WobbleMaze`: Shows live timer, gems collected, key status, falls counter, deterministic seed input + 🎲 Random button, maze size selector (Small 9x9, Medium 13x13, Large 17x17), difficulty selector (Easy, Medium, Hard), 2D level puck, and Tare Zero controls.
  - `SensorLab`: Shows 100 Hz recording controls (with duration and sample counters), instant CSV download link, and live 9-axis sensor diagnostics (heading, accel, gyro, mag, packet rate).
