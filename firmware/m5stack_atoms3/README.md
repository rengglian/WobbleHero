# M5Stack AtomS3 Balancing Board Firmware

Firmware for the **M5Stack AtomS3 (SKU C126)** mounted on a balancing board (wobble board / balance board).

## Architecture
- **Hardware**: M5Stack AtomS3 (ESP32-S3 + Bosch BMI270 6-axis IMU + 128x128 color IPS LCD + push-button).
- **Streaming Rate**: 100 Hz (10 ms period).
- **Protocol**: 32-byte raw binary UDP datagram (`magic: 0x4553`, sequence, button, `timestamp_us`, `ax, ay, az, gx, gy, gz`).
- **Target Port**: UDP `8080` (or `8081`).
- **Configuration**:
  - Wi-Fi AP Onboarding: If not connected to Wi-Fi, the AtomS3 launches a captive portal AP named `AtomS3-Setup` (`192.168.4.1`).
  - Enter your local Wi-Fi SSID, Password, and the IP address of your Android TV/Tablet/Phone running WobbleHero.
  - Settings are saved to non-volatile flash (NVS).

## Flashing Instructions
1. Install **Arduino IDE 2.x** with the ESP32 board package (`esp32` by Espressif Systems).
2. Install the **M5Unified** and **M5GFX** libraries via the Arduino Library Manager.
3. Select board **M5Stack-AtomS3** (`Upload Speed: 921600`, `USB CDC On Boot: Enabled`).
4. Connect the AtomS3 via USB-C and flash `m5stack_atoms3.ino`.
5. On boot, connect to Wi-Fi `AtomS3-Setup` from your phone to enter the WobbleHero Android device IP.
