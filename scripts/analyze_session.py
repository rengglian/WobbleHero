#!/usr/bin/env python3
"""
WobbleHero Movement Session Analyzer & Filter Evaluator
Analyzes CSV movement sessions recorded in SensorLab.
Evaluates packet arrival jitter, standing tremor spectra (FFT),
and simulates different filter configurations.
"""

import sys
import os
import csv
import math
import argparse
from typing import List, Dict, Any

def analyze_csv(filepath: str, save_plot: bool = True):
    if not os.path.isfile(filepath):
        print(f"[Error] File not found: {filepath}")
        sys.exit(1)

    print(f"============================================================")
    print(f" Analyzing Movement Session: {os.path.basename(filepath)}")
    print(f"============================================================")

    # 1. Parse CSV
    rows: List[Dict[str, float]] = []
    with open(filepath, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for r in reader:
            try:
                rows.append({
                    "elapsed": float(r["elapsed_sec"]),
                    "timestamp_us": float(r["timestamp_us"]),
                    "seq": int(r["seq"]),
                    "ax": float(r["ax_g"]),
                    "ay": float(r["ay_g"]),
                    "az": float(r["az_g"]),
                    "gx": float(r["gx_degs"]),
                    "gy": float(r["gy_degs"]),
                    "gz": float(r["gz_degs"]),
                    "fused_roll": float(r["fused_roll_deg"]),
                    "fused_pitch": float(r["fused_pitch_deg"]),
                    "norm_roll": float(r["norm_roll"]),
                    "norm_pitch": float(r["norm_pitch"]),
                    "mx": float(r.get("mx_ut", 0.0)),
                    "my": float(r.get("my_ut", 0.0)),
                    "mz": float(r.get("mz_ut", 0.0)),
                })
            except (ValueError, KeyError):
                continue

    n = len(rows)
    if n < 10:
        print(f"[Error] Session has insufficient samples ({n}). Record for at least 5 seconds.")
        return

    duration = rows[-1]["elapsed"] - rows[0]["elapsed"]
    hz = (n - 1) / duration if duration > 0 else 0

    # 2. Timing and Packet Jitter
    dt_us_list = []
    unique_packets = 0
    dropped_packets = 0
    prev_seq = None

    for i in range(n):
        seq = rows[i]["seq"]
        if i > 0:
            diff_us = (rows[i]["timestamp_us"] - rows[i-1]["timestamp_us"]) % 0xFFFFFFFF
            if 0 < diff_us < 500_000:
                dt_us_list.append(diff_us)

        if prev_seq is not None:
            if seq != prev_seq:
                unique_packets += 1
                expected_seq = (prev_seq + 1) & 0xFF
                if seq != expected_seq:
                    gap = (seq - expected_seq) & 0xFF
                    dropped_packets += gap
        else:
            unique_packets = 1
        prev_seq = seq

    total_sent = unique_packets + dropped_packets
    loss_rate = (dropped_packets / total_sent * 100.0) if total_sent > 0 else 0.0

    avg_dt_ms = (sum(dt_us_list) / len(dt_us_list) / 1000.0) if dt_us_list else 10.0
    jitter_ms = 0.0
    if len(dt_us_list) > 1:
        variance = sum(((x / 1000.0) - avg_dt_ms) ** 2 for x in dt_us_list) / len(dt_us_list)
        jitter_ms = math.sqrt(variance)

    print(f"\n📊 [1. Session Overview & Network Telemetry]")
    print(f"  • Total Frames Sampled: {n:,} frames")
    print(f"  • Unique UDP Packets:   {unique_packets:,} packets")
    print(f"  • Session Duration:     {duration:.2f} seconds")
    print(f"  • Effective Packet Rate:{unique_packets / duration:.1f} Hz (Hardware target: 100 Hz)")
    print(f"  • Average Packet Delta: {avg_dt_ms:.2f} ms")
    print(f"  • Packet Arrival Jitter:±{jitter_ms:.2f} ms")
    print(f"  • Dropped Packets:      {dropped_packets} (Loss rate: {loss_rate:.1f}%)")

    # 3. Motion Amplitude & Standing Tremor
    roll_vals = [r["fused_roll"] for r in rows]
    pitch_vals = [r["fused_pitch"] for r in rows]
    gy_vals = [r["gy"] for r in rows]
    gx_vals = [r["gx"] for r in rows]

    mean_roll = sum(roll_vals) / n
    std_roll = math.sqrt(sum((x - mean_roll)**2 for x in roll_vals) / n)
    min_roll, max_roll = min(roll_vals), max(roll_vals)

    mean_pitch = sum(pitch_vals) / n
    std_pitch = math.sqrt(sum((x - mean_pitch)**2 for x in pitch_vals) / n)
    min_pitch, max_pitch = min(pitch_vals), max(pitch_vals)

    rms_gy = math.sqrt(sum(g**2 for g in gy_vals) / n)
    rms_gx = math.sqrt(sum(g**2 for g in gx_vals) / n)

    print(f"\n⚖️ [2. Kinematic & Standing Stability Analysis]")
    print(f"  • Roll Range (Left/Right): {min_roll:+5.1f}° to {max_roll:+5.1f}° (Mean: {mean_roll:+5.1f}°, StdDev: {std_roll:4.1f}°)")
    print(f"  • Pitch Range (Front/Back):{min_pitch:+5.1f}° to {max_pitch:+5.1f}° (Mean: {mean_pitch:+5.1f}°, StdDev: {std_pitch:4.1f}°)")
    print(f"  • Ankle Tremor RMS Rate:   Roll: {rms_gy:5.1f} deg/s | Pitch: {rms_gx:5.1f} deg/s")

    # Magnetometer check
    mag_magnitudes = [math.sqrt(r["mx"]**2 + r["my"]**2 + r["mz"]**2) for r in rows]
    has_mag = any(m > 0.01 for m in mag_magnitudes)
    if has_mag:
        avg_mag = sum(mag_magnitudes) / n
        print(f"  • Magnetometer (Compass):  Active (Mean Field: {avg_mag:.1f} µT, Earth nominal ~30-60 µT)")
    else:
        print(f"  • Magnetometer (Compass):  Not Detected (0.0 µT - standard 6-axis mode)")

    # 4. Simple Discrete Fourier Transform (FFT) for Dominant Tremor Frequencies
    print(f"\n🔍 [3. Tremor Frequency Spectrum (FFT)]")
    num_bins = min(256, n // 2)
    freqs = []
    powers = []
    for k in range(1, num_bins):
        freq = (k * hz) / n
        if freq > 15.0: # Interested in balance frequencies (0 - 15 Hz)
            break
        re = sum(gy_vals[i] * math.cos(2 * math.pi * k * i / n) for i in range(n))
        im = sum(-gy_vals[i] * math.sin(2 * math.pi * k * i / n) for i in range(n))
        pwr = math.sqrt(re*re + im*im) / n
        freqs.append(freq)
        powers.append(pwr)

    if powers:
        top_indices = sorted(range(len(powers)), key=lambda i: powers[i], reverse=True)[:3]
        for rank, idx in enumerate(top_indices, 1):
            f = freqs[idx]
            p = powers[idx]
            category = "Intentional Lean / Shift" if f < 1.0 else ("Ankle Sway / Wobble" if f < 4.5 else "Tremor / Jitter")
            print(f"  • Peak #{rank}: {f:4.2f} Hz (Amplitude: {p:5.2f}) → {category}")

    # 5. Filter Evaluation Simulator
    print(f"\n🧪 [4. Offline Filter Simulation & Comparison]")
    print("  Simulating different filter configurations on this raw IMU dataset:")

    configs = [
        {"name": "Comp 0.90 (Fast / Raw)", "alpha": 0.90, "ema": 0.35},
        {"name": "Comp 0.96 (Standard)",    "alpha": 0.96, "ema": 0.28},
        {"name": "Comp 0.96 + Heavy EMA",   "alpha": 0.96, "ema": 0.18}, # Our tuned elevated board profile
        {"name": "Comp 0.98 + Heavy EMA",   "alpha": 0.98, "ema": 0.15},
    ]

    for cfg in configs:
        alpha = cfg["alpha"]
        ema_k = cfg["ema"]

        sim_roll = rows[0]["fused_roll"]
        filtered_out = 0.0
        accel_rolls = []
        sim_rolls = []

        for i in range(n):
            ax = rows[i]["ax"]
            ay = rows[i]["ay"]
            az = rows[i]["az"]
            gy = rows[i]["gy"]

            acc_deg = math.atan2(ax, math.sqrt(ay*ay + az*az)) * (180.0 / math.pi)
            accel_rolls.append(acc_deg)

            dt = 0.01
            if i > 0 and i < len(dt_us_list):
                dt = max(0.005, min(0.05, dt_us_list[i-1] * 1e-6))

            sim_roll = alpha * (sim_roll + gy * dt) + (1.0 - alpha) * acc_deg
            norm = max(-1.0, min(1.0, sim_roll / 20.0))
            filtered_out += (norm - filtered_out) * ema_k
            sim_rolls.append(filtered_out)

        # Smoothness: mean second derivative / jitter
        diffs = [abs(sim_rolls[j] - sim_rolls[j-1]) for j in range(1, len(sim_rolls))]
        mean_jitter = (sum(diffs) / len(diffs)) * 100.0 if diffs else 0
        print(f"  • {cfg['name']:<28} → Jitter Metric: {mean_jitter:4.2f} (lower is smoother)")

    # 6. Plotting (Optional if matplotlib is available)
    if save_plot:
        try:
            import matplotlib.pyplot as plt
            plot_path = os.path.splitext(filepath)[0] + "_analysis.png"

            fig, axes = plt.subplots(3, 1, figsize=(11, 8), sharex=False)
            t = [r["elapsed"] for r in rows]

            # Subplot 1: Raw Accel Tilt vs Fused Roll
            axes[0].plot(t, [math.atan2(r["ax"], math.sqrt(r["ay"]**2 + r["az"]**2))*(180/math.pi) for r in rows],
                         color="#94a3b8", alpha=0.5, label="Raw Accel Tilt (Deg)")
            axes[0].plot(t, roll_vals, color="#38bdf8", linewidth=1.5, label="Fused Roll (Host Filter)")
            axes[0].set_ylabel("Degrees")
            axes[0].set_title("Orientation: Raw Accelerometer Tilt vs Complementary Fused Roll")
            axes[0].grid(True, alpha=0.25)
            axes[0].legend(loc="upper right")

            # Subplot 2: Gyroscope Angular Rate (Ankle Tremor)
            axes[1].plot(t, gy_vals, color="#f59e0b", linewidth=1.0, label="Gyro Y (Roll rate deg/s)")
            axes[1].plot(t, gx_vals, color="#a855f7", linewidth=1.0, alpha=0.7, label="Gyro X (Pitch rate deg/s)")
            axes[1].set_ylabel("deg/s")
            axes[1].set_title("Angular Velocity (Standing Tremor Rates)")
            axes[1].grid(True, alpha=0.25)
            axes[1].legend(loc="upper right")

            # Subplot 3: FFT Frequency Spectrum
            axes[2].plot(freqs, powers, color="#10b981", linewidth=1.5)
            axes[2].set_xlabel("Frequency (Hz)")
            axes[2].set_ylabel("Power Amplitude")
            axes[2].set_title("Frequency Spectrum of Standing Tremor")
            axes[2].set_xlim(0, 12)
            axes[2].grid(True, alpha=0.25)

            plt.tight_layout()
            plt.savefig(plot_path, dpi=120)
            print(f"\n📈 Saved analysis plot visualization: {plot_path}")
        except ImportError:
            print("\n(Tip: Install matplotlib `pip3 install matplotlib` to generate graphical plots)")

    print(f"\n============================================================")
    print(" Analysis Complete.")
    print(f"============================================================")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WobbleHero Session Movement Analyzer")
    parser.add_argument("csv_path", nargs="?", default="", help="Path to recorded session CSV file")
    parser.add_argument("--no-plot", action="store_true", help="Do not attempt to save visual plots")
    args = parser.parse_args()

    target_csv = args.csv_path
    if not target_csv:
        # Search for latest session in recordings directory
        recordings_dirs = [
            os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "recordings"),
            os.path.expanduser("~/.local/share/godot/app_userdata/WobbleHero/recordings"),
            "/tmp"
        ]
        found_files = []
        for d in recordings_dirs:
            if os.path.isdir(d):
                for f in os.listdir(d):
                    if f.endswith(".csv"):
                        found_files.append(os.path.join(d, f))

        if found_files:
            found_files.sort(key=lambda p: os.path.getmtime(p), reverse=True)
            target_csv = found_files[0]
            print(f"[Auto-Detect] Using most recent recording session: {target_csv}")
        else:
            print("Usage: python3 scripts/analyze_session.py <path/to/session.csv>")
            sys.exit(1)

    analyze_csv(target_csv, save_plot=not args.no_plot)
