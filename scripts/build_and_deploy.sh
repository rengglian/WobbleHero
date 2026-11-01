#!/usr/bin/env bash
set -eo pipefail

# ==============================================================================
# WobbleHero Build & Deploy Script
# Automates C# compilation, Godot headless export, and ADB deployment to Android.
# ==============================================================================

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_DIR="$PROJECT_ROOT/godot"
BUILD_DIR="$GODOT_DIR/builds"

# Text formatting
BOLD="\033[1m"
GREEN="\033[1;32m"
BLUE="\033[1;34m"
YELLOW="\033[1;33m"
RED="\033[1;31m"
RESET="\033[0m"

log_info()  { echo -e "${BLUE}[INFO]${RESET} $*"; }
log_ok()    { echo -e "${GREEN}[OK]${RESET} $*"; }
log_warn()  { echo -e "${YELLOW}[WARN]${RESET} $*"; }
log_err()   { echo -e "${RED}[ERROR]${RESET} $*"; }

# Default Options
TARGET="android"       # "android", "desktop", "all"
MODE="debug"           # "debug", "release"
DO_BUILD=true
DO_DEPLOY=true
TAIL_LOGCAT=false
DEVICE_ID=""

usage() {
    echo -e "${BOLD}Usage:${RESET} $(basename "$0") [OPTIONS]"
    echo ""
    echo -e "${BOLD}Targets:${RESET}"
    echo "  --android          Build & deploy for Android TV & Mobile (default)"
    echo "  --desktop, --linux Build for Linux Desktop x86_64"
    echo "  --all              Build both Android and Desktop targets"
    echo ""
    echo -e "${BOLD}Actions:${RESET}"
    echo "  --build-only       Compile and export only; do not attempt ADB deploy"
    echo "  --deploy-only      Deploy existing APK via ADB; skip build step"
    echo "  --logcat           Stream logcat output after launching on Android"
    echo "  --release          Build in Release configuration (default is Debug)"
    echo "  -s, --device <ID>  Specify target ADB device serial if multiple are connected"
    echo "  -h, --help         Show this help message"
    echo ""
    echo -e "${BOLD}Examples:${RESET}"
    echo "  $(basename "$0")                  # Full build and deploy to connected Android device"
    echo "  $(basename "$0") --build-only     # Build Android APK without deploying"
    echo "  $(basename "$0") --desktop        # Build Linux desktop executable"
    echo "  $(basename "$0") --logcat         # Deploy and stream Godot/C# logs"
    exit 0
}

# Parse CLI Arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --android)      TARGET="android"; shift ;;
        --desktop|--linux) TARGET="desktop"; shift ;;
        --all)          TARGET="all"; shift ;;
        --build-only)   DO_BUILD=true; DO_DEPLOY=false; shift ;;
        --deploy-only)  DO_BUILD=false; DO_DEPLOY=true; shift ;;
        --release)      MODE="release"; shift ;;
        --logcat)       TAIL_LOGCAT=true; shift ;;
        -s|--device)    DEVICE_ID="$2"; shift 2 ;;
        -h|--help)      usage ;;
        *) log_err "Unknown option: $1"; usage ;;
    esac
done

# Locate Godot executable
find_godot() {
    if [[ -n "$GODOT_BIN" && -x "$GODOT_BIN" ]]; then
        return 0
    fi
    local candidates=(
        "/opt/godot/Godot_v4.8-beta1_mono_linux_x86_64/Godot_v4.8-beta1_mono_linux.x86_64"
        "/opt/godot/Godot_v4.7.2-stable_mono_linux.x86_64"
        "$(which godot 2>/dev/null || true)"
        "$(which godot4 2>/dev/null || true)"
    )
    for c in "${candidates[@]}"; do
        if [[ -n "$c" && -x "$c" ]]; then
            GODOT_BIN="$c"
            return 0
        fi
    done
    return 1
}

# Ensure debug keystore exists
ensure_keystore() {
    local keystore="$GODOT_DIR/debug.keystore"
    if [[ ! -f "$keystore" ]]; then
        log_info "Creating self-contained debug.keystore for Android export..."
        keytool -genkey -v \
            -keystore "$keystore" \
            -storepass android \
            -alias androiddebugkey \
            -keypass android \
            -keyalg RSA \
            -keysize 2048 \
            -validity 10000 \
            -dname "CN=Android Debug,O=Android,C=US" >/dev/null 2>&1
        log_ok "Generated debug.keystore at $keystore"
    fi
}

# ------------------------------------------------------------------------------
# STEP 1: Build .NET C# Solution
# ------------------------------------------------------------------------------
build_dotnet() {
    local config="Debug"
    if [[ "$MODE" == "release" ]]; then
        config="Release"
    fi

    log_info "Compiling WobbleHero C# solution ($config)..."
    cd "$GODOT_DIR"
    XDG_RUNTIME_DIR=/tmp dotnet build WobbleHero.csproj -c "$config"
    log_ok "C# compilation successful."
}

# ------------------------------------------------------------------------------
# STEP 2: Godot Headless Export
# ------------------------------------------------------------------------------
export_targets() {
    if ! find_godot; then
        log_err "Godot binary not found! Please set GODOT_BIN environment variable."
        exit 1
    fi
    log_info "Using Godot: $GODOT_BIN"

    ensure_keystore
    mkdir -p "$BUILD_DIR/android" "$BUILD_DIR/linux"

    local export_flag="--export-debug"
    if [[ "$MODE" == "release" ]]; then
        export_flag="--export-release"
    fi

    if [[ "$TARGET" == "android" || "$TARGET" == "all" ]]; then
        local apk_dest="$BUILD_DIR/android/WobbleHero.apk"
        log_info "Exporting Android APK ($MODE) to: $apk_dest..."
        "$GODOT_BIN" --headless --path "$GODOT_DIR" $export_flag "Android TV & Mobile" "$apk_dest"
        log_ok "Android APK generated: $apk_dest"
    fi

    if [[ "$TARGET" == "desktop" || "$TARGET" == "all" ]]; then
        local bin_dest="$BUILD_DIR/linux/WobbleHero.x86_64"
        log_info "Exporting Linux Desktop ($MODE) to: $bin_dest..."
        "$GODOT_BIN" --headless --path "$GODOT_DIR" $export_flag "Linux Desktop (x86_64)" "$bin_dest"
        chmod +x "$bin_dest" 2>/dev/null || true
        log_ok "Linux desktop binary generated: $bin_dest"
    fi
}

# ------------------------------------------------------------------------------
# STEP 3: ADB Deployment (Android)
# ------------------------------------------------------------------------------
deploy_android() {
    local apk_path="$BUILD_DIR/android/WobbleHero.apk"
    if [[ ! -f "$apk_path" ]]; then
        log_err "Cannot deploy: APK not found at $apk_path"
        exit 1
    fi

    if ! which adb >/dev/null 2>&1; then
        log_warn "'adb' not found in PATH. Skipping automated device deployment."
        log_info "Install APK manually using: adb install -r $apk_path"
        return 0
    fi

    local adb_cmd="adb"
    if [[ -n "$DEVICE_ID" ]]; then
        adb_cmd="adb -s $DEVICE_ID"
    fi

    # Check for connected devices
    log_info "Checking for connected Android devices via ADB..."
    local devices
    devices=$($adb_cmd devices | grep -v "List of devices" | grep "device$" || true)

    if [[ -z "$devices" ]]; then
        log_warn "No active Android device or emulator detected via ADB."
        log_info "When your device is connected, install with:"
        echo -e "  ${BOLD}adb install -r \"$apk_path\"${RESET}"
        echo -e "  ${BOLD}adb shell am start -n com.wobblehero.game/com.godot.game.GodotApp${RESET}"
        return 0
    fi

    log_info "Connected device(s) found:"
    echo "$devices"

    log_info "Installing $apk_path onto Android device..."
    $adb_cmd install -r "$apk_path"
    log_ok "Installation complete."

    log_info "Launching WobbleHero on Android..."
    $adb_cmd shell am start -n com.wobblehero.game/com.godot.game.GodotApp
    log_ok "WobbleHero launched successfully!"

    if [[ "$TAIL_LOGCAT" == true ]]; then
        log_info "Streaming logcat output (Ctrl+C to stop)..."
        $adb_cmd logcat -c
        $adb_cmd logcat -s godot:I DOTNET:I WobbleHero:I
    fi
}

# ------------------------------------------------------------------------------
# Main Flow
# ------------------------------------------------------------------------------
main() {
    echo -e "${BOLD}=== WobbleHero Build & Deploy ===${RESET}"
    echo "Target:  $TARGET"
    echo "Mode:    $MODE"
    echo "Build:   $DO_BUILD"
    echo "Deploy:  $DO_DEPLOY"
    echo "----------------------------------------"

    if [[ "$DO_BUILD" == true ]]; then
        build_dotnet
        export_targets
    fi

    if [[ "$DO_DEPLOY" == true && ("$TARGET" == "android" || "$TARGET" == "all") ]]; then
        deploy_android
    fi

    echo -e "----------------------------------------"
    log_ok "All requested tasks finished successfully!"
}

main
