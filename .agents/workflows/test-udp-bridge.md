# Workflow: Test UDP Balance Board Bridge & Companion Remote

This workflow tests AtomS3 UDP packet delivery and the companion web app without needing physical hardware.

## Steps

1. **Launch Desktop Godot**:
   ```bash
   ./scripts/run_desktop.sh
   ```

2. **Simulate Balance Board Packets (Optional)**:
   In a separate terminal, launch the Python virtual sensor simulator:
   ```bash
   python3 scripts/sensor_bridge.py --simulate --host 127.0.0.1 --port 8080
   ```

3. **Open Companion Web Remote**:
   Open a browser to:
   ```text
   http://localhost:8080/remote
   ```
   Or scan the QR code displayed on the Godot game screen with your mobile phone!

4. **Verify Functionality**:
   - Verify the 2D bubble level responds to simulated tilt or keyboard input (A/D or W/S).
   - Test switching games using the game switcher cards.
   - Test tare calibration and sensitivity slider.
