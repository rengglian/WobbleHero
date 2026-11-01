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

## 3. Tare Calibration, Deadzone & Progressive Curve

On an **elevated round board** (such as a round Koor Board with a raised dome/pedestal to fit electronics and power banks), the center of mass is elevated. This creates an inverted pendulum effect where natural standing ankle micro-tremors (~1.5°–2.0°) occur continuously.

The filtering pipeline applies:
1. **Dynamic Tare Calibration**:
   $$\theta_{\text{cal}} = \theta_{\text{fused}} - \text{TareOffset}$$
2. **Comfort Normalization Limit**:
   $$\theta_{\max} = 20.0^\circ \quad (\text{comfort tilt limit for round board})$$
3. **Progressive Deadzone & Curved Remapping**:
   Suppresses standing tremor while giving fine, gentle control near level and full deflection on deep leans:
   $$u_{\text{remapped}} = \begin{cases} 0.0 & \text{if } |u_{\text{raw}}| \le 0.07 \\ \text{sign}(u_{\text{raw}}) \cdot \left(\frac{|u_{\text{raw}}| - 0.07}{1.0 - 0.07}\right)^{1.35} & \text{otherwise} \end{cases}$$
4. **Exponential Smoothing (EMA)**:
   Low-pass filter coefficient $\alpha = 0.18$ at 100 Hz suppresses standing ankle tremors without noticeable latency:
   $$u(t) = u(t - 1) + 0.18 \cdot (u_{\text{curved}}(t) - u(t - 1))$$

---

## 4. Ball & Plate Game Physics Tuning (Elevated Boards)

To make balance mini-games playable and satisfying when standing:
1. **Gentle Gravity**: $g = 6.5\text{ m/s}^2$ (rather than extreme $18\text{ m/s}^2$).
2. **Viscous Rolling Damping**: $v(t) = v(t - 1) \cdot (0.88)^{60 \cdot \Delta t}$ prevents runaway speed.
3. **Concave Dish Bowl Centering**: A subtle restorative force towards the center ($F_{\text{bowl}} = -0.40 \cdot \vec{r}$) creates a natural stability well when the board is level.
4. **Elastic Bumper Rim**: The outer perimeter acts as a soft bouncy cushion that keeps the marble in play rather than repeatedly dropping off.

---

## 5. Fallback Emulation (Zero Hardware Required)
When no AtomS3 hardware packet is received within 350 ms:
- **Keyboard Emulation**:
  - `A` / `Left Arrow`: Smoothly ramps Roll left to $-1.0$.
  - `D` / `Right Arrow`: Smoothly ramps Roll right to $+1.0$.
  - `W` / `Up Arrow`: Smoothly ramps Pitch forward to $-1.0$.
  - `S` / `Down Arrow`: Smoothly ramps Pitch backward to $+1.0$.
- **Touch Emulation**:
  - Touching left/right screen halves or dragging across the screen produces proportional Roll and Pitch deflection.

---

## 6. 9-Axis Sensing & Magnetometer Compass Fusion
When an external or integrated magnetometer (e.g., AK09918, BMM150) is connected to the AtomS3:
- **Binary Telemetry**: Extends the raw IMU stream with 3-axis magnetic flux density ($B_x, B_y, B_z$) in microteslas ($\mu\text{T}$).
- **Tilt-Compensated Compass Heading**:
  Computed from fused roll ($\theta_{\text{roll}}$), fused pitch ($\theta_{\text{pitch}}$), and normalized magnetic field:
  $$B_{xh} = B_x \cos(\theta_{\text{pitch}}) + B_z \sin(\theta_{\text{pitch}})$$
  $$B_{yh} = B_x \sin(\theta_{\text{roll}}) \sin(\theta_{\text{pitch}}) + B_y \cos(\theta_{\text{roll}}) - B_z \sin(\theta_{\text{roll}}) \cos(\theta_{\text{pitch}})$$
  $$\text{Heading} = \text{atan2}(-B_{yh}, B_{xh}) \cdot \frac{180^\circ}{\pi} \pmod{360^\circ}$$
- **Forwarding in `BoardState`**: Exposed via `RawMag`, `HasMag`, and `CompassHeadingDeg` for display in SensorLab and broadcast over telemetry.
