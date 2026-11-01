# Rule 02: Android Performance & Godot 4 Optimization

This rule defines the performance, memory, and rendering constraints for WobbleHero running on Android devices (phones, tablets, and Android TV / NVIDIA Shield).

---

## 1. Target Hardware & Frame Budgets
- **Frame Target**: Stable 60 FPS (16.67 ms per frame budget).
- **Renderer**: `gl_compatibility` (OpenGL ES 3.0 / WebGL2 equivalent) to guarantee support across low-cost chips, Android TVs, and Mali/Adreno GPUs.
- **Display Scale**: 1080p target (`1920x1080`) with stretch mode `canvas_items` and aspect `expand`.

---

## 2. Zero GC Overhead in Update Loops
Godot C# runs on .NET 10:
- **No Per-Frame Allocations**: Never allocate heap objects (`new`, LINQ, closures, string concatenations) inside `_Process`, `_PhysicsProcess`, or `UpdateBoardInput`.
- **Pre-Allocated DTOs**: Telemetry payloads and `BoardState` must be structs or reused object instances.
- **Node Pooling**: Collectibles, particles, obstacles, and trail segments must use static node pools (`Queue<T>` or pre-warmed arrays) rather than repeated `Instantiate()` and `QueueFree()`.

---

## 3. Network Threading & Android Socket Safety
- **Asynchronous Sockets**: UDP and HTTP/WebSocket servers run asynchronously using `async`/`await` with `CancellationToken`. Never block the Godot main thread.
- **Thread Safety**: Network threads must dispatch state mutations or scene transitions to the main thread via `Callable.From(...).CallDeferred()`.
- **Permissions**: The Android manifest (`export_presets.cfg`) must declare:
  - `android.permission.INTERNET`
  - `android.permission.ACCESS_NETWORK_STATE`
  - `android.permission.CHANGE_WIFI_MULTICAST_STATE`
