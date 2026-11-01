---
name: balance-board-input
description: >-
  Provides end-to-end guidance for the WobbleHero balancing board input abstraction layer: 2D roll and pitch complementary sensor fusion, tare zeroing, deadzones, sensitivity scaling, and keyboard/touch emulation fallbacks.
---

# Balance Board Input Skill

This skill documents the mathematics, filtering pipeline, calibration mechanics, and fallback emulation for balancing board input in WobbleHero.

---

## 1. 2D Board Kinematics: Roll and Pitch

Unlike 1D steering runners, a physical balancing board exhibits motion along two primary axes:
1. **Roll Axis ($X$-axis / Lateral Tilt)**:
   - Tilt left or right.
   - Primary steering and lateral balance mechanic.
   - Accelerometer: $\theta_{\text{accel, roll}} = \text{atan2}\left(a_x, \sqrt{a_y^2 + a_z^2}\right) \cdot \frac{180^\circ}{\pi}$
2. **Pitch Axis ($Y$-axis / Longitudinal Tilt)**:
   - Tilt front or back.
   - Used for forward/backward marble acceleration, braking, ducking, or 2D marble tilt tables.
   - Accelerometer: $\theta_{\text{accel, pitch}} = \text{atan2}\left(a_y, \sqrt{a_x^2 + a_z^2}\right) \cdot \frac{180^\circ}{\pi}$

---

## 2. Complementary Filter Pipeline

For both Roll and Pitch:
$$\theta_{\text{gyro}}(t) = \theta_{\text{fused}}(t - 1) + \omega \cdot \Delta t$$
$$\theta_{\text{fused}}(t) = 0.96 \cdot \theta_{\text{gyro}}(t) + 0.04 \cdot \theta_{\text{accel}}(t)$$

Where:
- $\omega$ is the corresponding angular velocity ($g_y$ for roll, $g_x$ for pitch) in $^\circ/\text{s}$.
- $\Delta t$ is computed from microsecond hardware timestamps (`timestamp_us`).
- Normalization: $\text{steer} = \text{clamp}\left(\frac{\theta_{\text{fused}} - \text{TareOffset}}{\theta_{\max}}, -1.0, 1.0\right)$ with $\theta_{\max} = 28.0^\circ$.

---

## 3. Tare Calibration & Deadzone
- **Tare Calibration**: Pressing the Tare button stores $\text{TareOffset} = \theta_{\text{fused}}$, defining the board's resting angle as true zero.
- **Center Deadzone**: Suppresses natural human standing tremor ($|\text{steer}| < 0.05 \implies 0.0$).
- **Exponential Smoothing (EMA)**:
  $$u(t) = u(t - 1) + 0.35 \cdot (u_{\text{raw}} - u(t - 1))$$

---

## 4. Fallback Emulation (Zero Hardware Required)
When no AtomS3 hardware packet is received within 350 ms:
- **Keyboard Emulation**:
  - `A` / `Left Arrow`: Smoothly ramps Roll left to $-1.0$.
  - `D` / `Right Arrow`: Smoothly ramps Roll right to $+1.0$.
  - `W` / `Up Arrow`: Smoothly ramps Pitch forward to $-1.0$.
  - `S` / `Down Arrow`: Smoothly ramps Pitch backward to $+1.0$.
- **Touch Emulation**:
  - Touching left/right screen halves or dragging across the screen produces proportional Roll and Pitch deflection.
