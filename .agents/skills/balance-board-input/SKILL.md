---
name: balance-board-input
description: >-
  Provides end-to-end guidance for the WobbleHero balancing board input abstraction layer: 2D roll and pitch complementary sensor fusion, tare zeroing, deadzones, physical slap-nudge jerk detection, sensitivity scaling, and keyboard/touch emulation fallbacks.
---

# Balance Board Input Skill

This skill documents the kinematics, complementary filtering pipeline, tare calibration, physical jerk detection for slap-nudges, and zero-hardware fallback emulation for balancing boards in WobbleHero.

---

## 1. 2D Board Kinematics: Roll and Pitch

Unlike 1D steering runners, a physical balancing board exhibits motion along two primary axes:
1. **Roll Axis ($X$-axis / Lateral Tilt)**:
   - Tilt left or right.
   - Primary steering and lateral balance mechanic.
   - Accelerometer: $\theta_{\text{accel, roll}} = \text{atan2}\left(a_x, \sqrt{a_y^2 + a_z^2}\right) \cdot \frac{180^\circ}{\pi}$
2. **Pitch Axis ($Y$-axis / Longitudinal Tilt)**:
   - Tilt front or back.
   - Used for forward/backward marble acceleration, downhill ski tuck, log roll torque resistance, or table incline.
   - Accelerometer: $\theta_{\text{accel, pitch}} = \text{atan2}\left(a_y, \sqrt{a_x^2 + a_z^2}\right) \cdot \frac{180^\circ}{\pi}$

---

## 2. Complementary Filter Pipeline

For both Roll and Pitch:
$$\theta_{\text{gyro}}(t) = \theta_{\text{fused}}(t - 1) + \omega \cdot \Delta t$$
$$\theta_{\text{fused}}(t) = 0.96 \cdot \theta_{\text{gyro}}(t) + 0.04 \cdot \theta_{\text{accel}}(t)$$

Where:
- $\omega$ is the corresponding angular velocity ($g_y$ for roll, $g_x$ for pitch) in $^\circ/\text{s}$.
- $\Delta t$ is computed from microsecond hardware timestamps (`timestamp_us`) and clamped between $0.005\text{ s}$ and $0.05\text{ s}$.

---

## 3. Tare Calibration, Deadzone & Progressive Curve

On an **elevated round board** (such as a round Koor Board with a raised dome/pedestal to fit electronics and power banks), the center of mass is elevated. This creates an inverted pendulum effect where natural standing ankle micro-tremors (~1.5°–2.0°) occur continuously.

The filtering pipeline applies:
1. **Dynamic Tare Calibration**:
   $$\theta_{\text{cal}} = \theta_{\text{fused}} - \text{TareOffset}$$
2. **Comfort Normalization Limit**:
   $$\theta_{\max} = 20.0^\circ \quad (\text{comfort tilt limit for round board})$$
   $$u_{\text{raw}} = \text{clamp}\left(\frac{\theta_{\text{cal}}}{\theta_{\max}}, -1.0, 1.0\right)$$
3. **Progressive Deadzone & Curved Remapping**:
   Suppresses standing tremor while giving fine, gentle micro-control near level and full deflection on deep leans:
   $$u_{\text{remapped}} = \begin{cases} 0.0 & \text{if } |u_{\text{raw}}| \le 0.07 \\ \text{sign}(u_{\text{raw}}) \cdot \left(\frac{|u_{\text{raw}}| - 0.07}{1.0 - 0.07}\right)^{1.35} & \text{otherwise} \end{cases}$$
4. **Exponential Smoothing (EMA)**:
   Low-pass filter coefficient $\alpha = 0.18$ at 100 Hz suppresses standing ankle tremors without noticeable control latency:
   $$u(t) = u(t - 1) + 0.18 \cdot (u_{\text{remapped}}(t) - u(t - 1))$$
5. **Inversion & Sensitivity**:
   $$u_{\text{final}} = \text{clamp}\left(( \text{invert} \ ?\ -u(t) : u(t) ) \cdot \text{Sensitivity}, -1.0, 1.0\right)$$

---

## 4. Physical Slap-Nudge & Jerk Detection

To support authentic physical interaction (e.g. Pinball table slap-nudges and Sumo shockwave body slams), the engine calculates second-order angular acceleration ($\text{jerk}$):

$$\vec{\omega}(t) = \frac{\vec{\theta}_{\text{fused}}(t) - \vec{\theta}_{\text{fused}}(t - 1)}{\Delta t}$$
$$\vec{\alpha}(t) = \frac{\vec{\omega}(t) - \vec{\omega}(t - 1)}{\Delta t}$$
$$\text{jerk} = \|\vec{\alpha}(t)\|$$

### Slap-Nudge Evaluation Rules
- **Physical Jerk Threshold**: An angular jerk exceeding **$2200^\circ/\text{s}^2$** on physical AtomS3 hardware triggers a slap-nudge or shockwave slam.
- **Hardware vs. Emulation Segregation**:
  - `boardState.Source == "atoms3"`: Jerk threshold is evaluated automatically from raw IMU motion.
  - Keyboard / Touch / Companion: Explicit `Space` key, controller button, or companion UI button (`nudge_table` / `trigger_shockwave`) triggers the action.
- **0.8s Debounce Cooldown**:
  - When triggered, `_nudgeCooldownTimer` is set to $0.8\text{ s}$.
  - Any subsequent physical vibrations, board bounces, or rapid stomps within this window are discarded.
- **Arcade TILT Rules (Gravity Pinball)**:
  - Valid nudges are timestamped in a 5-second sliding window.
  - Warnings 1 and 2 deliver camera shake ($0.35$) and impulse.
  - A 3rd strike triggers a **3.5s TILT freeze** (board steering locked to base incline, shake $0.7$), followed by a **1.5s recovery grace period**.

---

## 5. Zero-Hardware Fallback Emulation

When no AtomS3 hardware packet is received within 350 ms (`HardwareReceiver.IsActive == false`), `BalanceBoardInput` automatically engages smooth fallback emulation:

### 5.1 Keyboard Emulation
- `A` / `Left Arrow`: Smoothly ramps Roll left to $-1.0$.
- `D` / `Right Arrow`: Smoothly ramps Roll right to $+1.0$.
- `W` / `Up Arrow`: Smoothly ramps Pitch forward to $-1.0$.
- `S` / `Down Arrow`: Smoothly ramps Pitch backward to $+1.0$.
- `Space` / `R`: Sets `IsButtonPressed = true` (triggers slap-nudge, ski jump, shockwave slam, or game restart).
- Smoothing speed: `8.0` units/s when keys are active; `12.0` units/s spring centering when released.

### 5.2 Touch Emulation
- Touch deflection is calculated relative to the viewport center:
  $$\text{targetRoll} = \text{clamp}\left(\frac{x_{\text{touch}} - 0.5 \cdot W}{0.4 \cdot W}, -1.0, 1.0\right)$$
  $$\text{targetPitch} = \text{clamp}\left(\frac{y_{\text{touch}} - 0.5 \cdot H}{0.4 \cdot H}, -1.0, 1.0\right)$$
- Screen drags dynamically update roll and pitch with smooth lerp interpolation.

---

## 6. 9-Axis Sensing & Magnetometer Compass Fusion

When an external or integrated magnetometer (e.g. AK09918, BMM150) is connected to the AtomS3:
- **Binary Telemetry**: The packet format extends from 32 bytes to 44 bytes, transmitting 3-axis magnetic flux density ($B_x, B_y, B_z$) in microteslas ($\mu\text{T}$).
- **Tilt-Compensated Compass Heading**:
  Computed from fused roll ($\theta_{\text{roll}}$), fused pitch ($\theta_{\text{pitch}}$), and normalized magnetic field:
  $$B_{xh} = B_x \cos(\theta_{\text{pitch}}) + B_z \sin(\theta_{\text{pitch}})$$
  $$B_{yh} = B_x \sin(\theta_{\text{roll}}) \sin(\theta_{\text{pitch}}) + B_y \cos(\theta_{\text{roll}}) - B_z \sin(\theta_{\text{roll}}) \cos(\theta_{\text{pitch}})$$
  $$\text{Heading} = \text{atan2}(-B_{yh}, B_{xh}) \cdot \frac{180^\circ}{\pi} \pmod{360^\circ}$$
- **Compass Labyrinth Mechanics**:
  - The 360° heading rotates temple gravity planes dynamically between NORTH, EAST, SOUTH, and WEST.
  - `BoardState.CompassHeadingDeg` and `BoardState.HasMag` are streamed to companion clients at 30 Hz.
