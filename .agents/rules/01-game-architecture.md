# Rule 01: WobbleHero Architecture & Balancing Board Modularity

This rule governs the architectural standards for WobbleHero, a generic balancing board gaming platform built with Godot 4 (C# .NET) and controlled via an M5Stack AtomS3 motion controller.

---

## 1. Modular Mini-Game Architecture (`IGameMode`)

WobbleHero is designed as an extensible multi-game launcher. Any balancing board mini-game must adhere to the `IGameMode` contract:

1. **Decoupled Game Logic**: Games must never directly instantiate UDP sockets, poll hardware sensors, or hardcode HTTP companion routes.
2. **Lifecycle Contract**:
   - `Initialize(GameManager manager)`: Cache references, initialize resources, and hook into manager events.
   - `StartGame()`: Begin active gameplay loop, spawn obstacles/targets, and start timers.
   - `PauseGame()` / `ResumeGame()`: Suspend/resume physics and game clocks.
   - `ResetGame()`: Reset scores, positions, and entity pools without reloading scenes.
   - `UpdateBoardInput(BoardState boardState, double delta)`: Receive normalized, filtered tilt parameters.
   - `GetLiveStats()`: Return telemetry dictionary for broadcasting to the Companion Web Remote.
3. **Event Hooks**:
   - `OnScoreUpdated(int currentScore)`
   - `OnGameOver(string reason)`
4. **Registration**: Every mini-game must be registered with `GameRegistry.RegisterGame(IGameMode game)` so the Companion Web Remote can discover it and allow players to switch games at runtime.

---

## 2. Input Decoupling & The `BoardState` Paradigm

All games receive a unified `BoardState` struct:
- `Roll`: Normalized $[-1.0, 1.0]$ left/right tilt (negative = tilt left, positive = tilt right).
- `Pitch`: Normalized $[-1.0, 1.0]$ front/back tilt (negative = tilt forward/down, positive = tilt backward/up).
- `FusedRollDeg`: Absolute fused roll in degrees (typically $\pm 28^\circ$).
- `FusedPitchDeg`: Absolute fused pitch in degrees.
- `IsActive`: Connection watchdog flag; true if packet received within last 350 ms.
- `IsButtonPressed`: Tactile button press on controller (e.g. for instant restart or selection).
- `Hz`: Real-time packet throughput (nominal 100 Hz).

### Input Fallbacks
To support desktop debugging and mobile testing without physical hardware, `BalanceBoardInput` must always provide seamless fallback:
- **Keyboard Fallback**: Arrow keys or WASD smoothly simulate roll and pitch.
- **Touch / Virtual Fallback**: Onscreen swipe or screen tilt simulates board roll and pitch.

---

## 3. Companion App & Bidirectional Synchronization

WobbleHero embeds an asynchronous HTTP & WebSocket server (`CompanionWebServer`) on port 8080:
- **Zero-Install Mobile Remote**: Any smartphone connected to the same local Wi-Fi can open `http://<device-ip>:8080/remote` to access full controls.
- **Game Switching**: The companion remote can query registered games via WebSocket and request runtime switching (`switch_game: "BalanceTrainer"`).
- **Board Calibration**: Provides instant **Tare Zero** (`tare`), **Reset Calibration** (`reset_cal`), **Sensitivity Slider**, and **Invert Axis** toggles.
- **HUD QR Code**: Main scene must project a zero-dependency QR code on screen so players can effortlessly scan to connect.

---

## 4. Game-Specific HUDs & Persistent Scoring

1. **Decoupled HUD Scenes**: Each mini-game mounts its own dedicated HUD (`res://Scenes/UI/BalanceTrainerHud.tscn`, `WobbleRunnerHud.tscn`, `SensorLabHud.tscn`) using the arcade **Luckiest Guy** font for distinct visual flair and specialized overlays (e.g., Top-10 Leaderboard overlays, live telemetry gauges).
2. **Persistent Leaderboards (`LeaderboardService`)**: Games record competitive runs to `user://leaderboard.json`. The service tracks Player Name, Score, Distance, Seed, and Date, exposing rankings to both in-game HUDs and the Companion Web Remote via WebSocket and `/api/leaderboard`.
