#!/usr/bin/env python3
"""
WobbleHero Sensor Bridge & Virtual Board Simulator
Can simulate an M5Stack AtomS3 balancing board sending 32-byte binary UDP datagrams
at 100 Hz to Godot for testing without physical hardware, or diagnose real incoming packets.
"""

import socket
import struct
import time
import math
import argparse
import sys

PACKET_MAGIC = 0x4553  # 'ES' in hex
PACKET_FORMAT = "<HBBI6f"  # uint16 magic, uint8 seq, uint8 button, uint32 timestamp_us, 6 floats (ax, ay, az, gx, gy, gz)
PACKET_SIZE = struct.calcsize(PACKET_FORMAT)

def run_simulator(host="127.0.0.1", port=8080, rate_hz=100):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[Sim] Streaming simulated AtomS3 packets to {host}:{port} at {rate_hz} Hz...")
    print("[Sim] Simulating gentle wobble roll and pitch oscillations. Press Ctrl+C to stop.")

    seq = 0
    start_time = time.time()
    period = 1.0 / rate_hz

    try:
        while True:
            t = time.time() - start_time
            timestamp_us = int((time.time() * 1_000_000) % 0xFFFFFFFF)

            # Simulated gentle balancing wobble: roll frequency ~0.4 Hz, pitch frequency ~0.3 Hz
            roll_rad = math.sin(t * 2.5) * 0.25      # ~14 degrees max
            pitch_rad = math.cos(t * 1.8) * 0.15     # ~8 degrees max

            # Acceleration components (in g units, with 1g gravity)
            ax = math.sin(roll_rad)
            ay = math.sin(pitch_rad)
            az = math.sqrt(max(0.01, 1.0 - ax*ax - ay*ay))

            # Gyroscope components (in deg/s)
            gx = -math.sin(t * 1.8) * 0.15 * 1.8 * (180.0 / math.pi)
            gy = math.cos(t * 2.5) * 0.25 * 2.5 * (180.0 / math.pi)
            gz = 0.0

            button = 0
            packet = struct.pack(PACKET_FORMAT, PACKET_MAGIC, seq, button, timestamp_us, ax, ay, az, gx, gy, gz)
            sock.sendto(packet, (host, port))

            seq = (seq + 1) & 0xFF
            time.sleep(period)
    except KeyboardInterrupt:
        print("\n[Sim] Stopped.")
    finally:
        sock.close()

def run_listener(port=8080):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", port))
    print(f"[Listener] Listening on UDP port {port} for AtomS3 binary packets...")

    count = 0
    t0 = time.time()

    try:
        while True:
            data, addr = sock.recvfrom(128)
            if len(data) == PACKET_SIZE:
                magic, seq, button, ts_us, ax, ay, az, gx, gy, gz = struct.unpack(PACKET_FORMAT, data)
                if magic == PACKET_MAGIC:
                    count += 1
                    if count % 50 == 0:
                        elapsed = time.time() - t0
                        hz = count / elapsed if elapsed > 0 else 0
                        roll_deg = math.atan2(ax, math.sqrt(ay*ay + az*az)) * (180.0 / math.pi)
                        pitch_deg = math.atan2(ay, math.sqrt(ax*ax + az*az)) * (180.0 / math.pi)
                        print(f"[{addr[0]}] Seq={seq:3d} | Btn={button} | Roll={roll_deg:+5.1f}° | Pitch={pitch_deg:+5.1f}° | Rate={hz:4.1f} Hz")
    except KeyboardInterrupt:
        print("\n[Listener] Stopped.")
    finally:
        sock.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WobbleHero Sensor Bridge")
    parser.add_argument("--simulate", action="store_true", help="Send simulated AtomS3 packets")
    parser.add_argument("--listen", action="store_true", help="Listen and decode incoming AtomS3 packets")
    parser.add_argument("--host", default="127.0.0.1", help="Target host IP for simulator")
    parser.add_argument("--port", type=int, default=8080, help="Target UDP port (default: 8080)")
    parser.add_argument("--rate", type=int, default=100, help="Simulation frequency in Hz (default: 100)")
    args = parser.parse_args()

    if args.listen:
        run_listener(args.port)
    else:
        run_simulator(args.host, args.port, args.rate)
