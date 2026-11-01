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
    "hz": 98,
    "button": 0,
    "source": "atoms3",
    "deviceIp": "192.168.1.145"
  },
  "game": {
    "activeGameId": "BalanceTrainer",
    "activeGameTitle": "Balance Trainer",
    "state": "RUNNING",
    "score": 420,
    "stats": {
      "timeRemaining": 45.2,
      "ringsCollected": 8,
      "multiplier": 2.0
    }
  },
  "availableGames": [
    { "id": "BalanceTrainer", "title": "Balance Trainer", "icon": "⚖️", "description": "Keep the ball centered on the tilting plate" },
    { "id": "WobbleRunner", "title": "Wobble Runner", "icon": "🏃", "description": "Slalom through gates and dodge obstacles" },
    { "id": "SensorLab", "title": "Sensor Lab", "icon": "🔬", "description": "3D board diagnostics and calibration" }
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
| `tare_board` | `{}` | Calibrates current board tilt as horizontal zero |
| `reset_cal` | `{}` | Resets calibration tare offsets |
| `set_sensitivity` | `{"value": 1.5}` | Adjusts input sensitivity multiplier ($0.5 \dots 2.5$) |
| `toggle_invert` | `{"axis": "roll"}` | Toggles roll or pitch axis inversion |
| `set_player_name` | `{"playerName": "Alex"}` | Updates player profile name |
