---
name: godot-android
description: >-
  Build, configuration, Android manifest permissions, and deployment procedures for Godot 4 C# .NET games targeting Android phones, tablets, and Android TV devices.
---

# Godot 4 Android Deployment Skill

This skill documents Android project configuration, `.NET 10` export template settings, socket permission requirements, and adb deployment commands for WobbleHero.

---

## 1. Android Manifest Permissions

Balancing board communication and companion app hosting require low-level network permissions in `export_presets.cfg`:
```ini
permissions/internet=true
permissions/access_network_state=true
permissions/change_wifi_multicast_state=true
```

## 2. Rendering Backend for Mobile
- Configured in `project.godot`:
```ini
[rendering]
renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"
textures/vram_compression/import_etc2_astc=true
```
- Guarantees compatibility across ARM Mali, Adreno, and PowerVR mobile chipsets without Vulkan validation errors.

---

## 3. Keystore & Export Configuration

Standard debug keystore location:
- Linux: `~/.local/share/godot/keystores/debug.keystore` or `~/.android/debug.keystore`
- Default User: `androiddebugkey`
- Default Password: `android`

### CLI Export Command:
```bash
godot --headless --export-debug "Android TV" builds/android/WobbleHero.apk
```

### Fast ADB Deployment:
```bash
adb install -r builds/android/WobbleHero.apk
adb shell am start -n com.wobblehero.game/com.godot.game.GodotApp
adb logcat -s godot:I DOTNET:I
```
