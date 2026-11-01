# Workflow: Build & Deploy Android APK

This workflow guides compiling and deploying the WobbleHero Android package.

## Quick One-Command Build & Deploy
Run the automated build and deploy script from the project root:
```bash
./scripts/build_and_deploy.sh
```

### Script Options
- `./scripts/build_and_deploy.sh --build-only` — Compiles C# and exports APK without deploying to device.
- `./scripts/build_and_deploy.sh --deploy-only` — Deploys existing APK to connected ADB device.
- `./scripts/build_and_deploy.sh --logcat` — Deploys, launches app, and streams live Godot/C# logcat.
- `./scripts/build_and_deploy.sh --desktop` — Exports Linux desktop executable.
- `./scripts/build_and_deploy.sh --release` — Exports release build.

---

## Manual Step-by-Step Flow

1. **Verify .NET Solution**:
   ```bash
   cd godot
   XDG_RUNTIME_DIR=/tmp dotnet build WobbleHero.csproj -c Release
   ```

2. **Generate Export Templates**:
   Ensure `export_presets.cfg` is present with `permissions/internet=true` and points to `debug.keystore`.

3. **Package APK**:
   ```bash
   mkdir -p builds/android
   godot --headless --path godot --export-debug "Android TV & Mobile" builds/android/WobbleHero.apk
   ```

4. **Install & Launch via ADB**:
   ```bash
   adb devices
   adb install -r builds/android/WobbleHero.apk
   adb shell am start -n com.wobblehero.game/com.godot.game.GodotApp
   ```

5. **Monitor Logs**:
   ```bash
   adb logcat -s godot:I DOTNET:I WobbleHero:I
   ```
