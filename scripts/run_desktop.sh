#!/usr/bin/env bash
set -e

# WobbleHero Desktop Runner
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -x "/opt/godot/Godot_v4.8-beta1_mono_linux_x86_64/Godot_v4.8-beta1_mono_linux.x86_64" ]; then
    GODOT_BIN="/opt/godot/Godot_v4.8-beta1_mono_linux_x86_64/Godot_v4.8-beta1_mono_linux.x86_64"
elif which godot >/dev/null 2>&1; then
    GODOT_BIN="godot"
else
    GODOT_BIN="godot"
fi

echo "[WobbleHero] Launching Godot project from $PROJECT_DIR/godot using $GODOT_BIN..."
exec "$GODOT_BIN" --path "$PROJECT_DIR/godot" "$@"
