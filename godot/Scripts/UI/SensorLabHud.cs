using System;
using Godot;
using WobbleHero.Core;
using WobbleHero.Games;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game 3: Sensor Lab & Diagnostics Bench.
/// Features a comprehensive real-time 9-DoF telemetry dashboard displaying:
/// - Fused Roll, Pitch, Tare offsets, and Compass heading
/// - Raw 3-Axis Accelerometer (X, Y, Z, |A| vector magnitude)
/// - Raw 3-Axis Gyroscope angular rates (Gx, Gy, Gz)
/// - Raw 3-Axis Magnetometer (Mx, My, Mz, |M| field strength)
/// - Hardware timing, sequence counter, sender IP, throughput Hz, and button state
/// - High-visibility recording session status banner with sample counter
/// </summary>
public partial class SensorLabHud : BaseGameHud
{
    // Top Bar & Recording
    [Export] public Label? RecordStatusLabel { get; set; }
    [Export] public PanelContainer? RecordPanel { get; set; }

    // Fused Angles & Calibration
    [Export] public Label? FusedAnglesLabel { get; set; }
    [Export] public Label? HeadingLabel { get; set; }
    [Export] public Label? TareOffsetsLabel { get; set; }
    [Export] public Label? NormInputsLabel { get; set; }

    // Raw 3-Axis IMU Sensor Readouts
    [Export] public Label? AccelAxesLabel { get; set; }
    [Export] public Label? AccelMagLabel { get; set; }
    [Export] public Label? GyroAxesLabel { get; set; }
    [Export] public Label? MagAxesLabel { get; set; }
    [Export] public Label? MagStrengthLabel { get; set; }

    // Hardware Telemetry & Link
    [Export] public Label? HardwareLinklabel { get; set; }
    [Export] public Label? HardwarePacketLabel { get; set; }
    [Export] public Label? ButtonStateLabel { get; set; }

    public override void _Ready()
    {
        RecordStatusLabel ??= GetNodeOrNull<Label>("%RecordStatusLabel");
        RecordPanel ??= GetNodeOrNull<PanelContainer>("%RecordPanel");

        FusedAnglesLabel ??= GetNodeOrNull<Label>("%FusedAnglesLabel");
        HeadingLabel ??= GetNodeOrNull<Label>("%HeadingLabel");
        TareOffsetsLabel ??= GetNodeOrNull<Label>("%TareOffsetsLabel");
        NormInputsLabel ??= GetNodeOrNull<Label>("%NormInputsLabel");

        AccelAxesLabel ??= GetNodeOrNull<Label>("%AccelAxesLabel");
        AccelMagLabel ??= GetNodeOrNull<Label>("%AccelMagLabel");
        GyroAxesLabel ??= GetNodeOrNull<Label>("%GyroAxesLabel");
        MagAxesLabel ??= GetNodeOrNull<Label>("%MagAxesLabel");
        MagStrengthLabel ??= GetNodeOrNull<Label>("%MagStrengthLabel");

        HardwareLinklabel ??= GetNodeOrNull<Label>("%HardwareLinklabel");
        HardwarePacketLabel ??= GetNodeOrNull<Label>("%HardwarePacketLabel");
        ButtonStateLabel ??= GetNodeOrNull<Label>("%ButtonStateLabel");
    }

    public override void UpdateState(BoardState board, IGameMode game)
    {
        base.UpdateState(board, game);

        string signRoll = board.FusedRollDeg >= 0 ? "+" : "";
        string signPitch = board.FusedPitchDeg >= 0 ? "+" : "";

        // 1. Fused Angles & Tare
        if (FusedAnglesLabel != null)
        {
            FusedAnglesLabel.Text = $"ROLL: {signRoll}{board.FusedRollDeg:F1}°  |  PITCH: {signPitch}{board.FusedPitchDeg:F1}°";
        }

        if (HeadingLabel != null)
        {
            if (board.HasMag)
            {
                string dir = GetCompassDirection(board.CompassHeadingDeg);
                HeadingLabel.Text = $"COMPASS HEADING: {board.CompassHeadingDeg:F0}° ({dir})";
                HeadingLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                HeadingLabel.Text = "COMPASS HEADING: SENSOR ABSENT";
                HeadingLabel.Modulate = new Color(0.6f, 0.6f, 0.6f);
            }
        }

        if (TareOffsetsLabel != null)
        {
            TareOffsetsLabel.Text = $"TARE ZERO: R: {board.CalOffsetRoll:+0.0;-0.0;0.0}° | P: {board.CalOffsetPitch:+0.0;-0.0;0.0}°";
        }

        if (NormInputsLabel != null)
        {
            NormInputsLabel.Text = $"NORM OUTPUT: ROLL: {board.Roll:+0.00;-0.00;0.00} | PITCH: {board.Pitch:+0.00;-0.00;0.00}";
        }

        // 2. Accelerometer (3-Axis & Magnitude)
        float accelTotal = board.RawAccel.Length();
        if (AccelAxesLabel != null)
        {
            AccelAxesLabel.Text = $"X: {board.RawAccel.X:+0.00;-0.00;0.00}g   Y: {board.RawAccel.Y:+0.00;-0.00;0.00}g   Z: {board.RawAccel.Z:+0.00;-0.00;0.00}g";
        }

        if (AccelMagLabel != null)
        {
            AccelMagLabel.Text = $"TOTAL VECTOR (|A|): {accelTotal:F2} g (1g = REST)";
            AccelMagLabel.Modulate = MathF.Abs(accelTotal - 1.0f) < 0.15f
                ? new Color(0.2f, 0.95f, 0.4f)
                : new Color(1.0f, 0.6f, 0.2f);
        }

        // 3. Gyroscope (3-Axis Rate)
        if (GyroAxesLabel != null)
        {
            GyroAxesLabel.Text = $"Gx: {board.RawGyro.X:+000.0;-000.0;000.0}°/s (P)\nGy: {board.RawGyro.Y:+000.0;-000.0;000.0}°/s (R)\nGz: {board.RawGyro.Z:+000.0;-000.0;000.0}°/s (Y)";
        }

        // 4. Magnetometer (3-Axis Field)
        if (board.HasMag)
        {
            float magTotal = board.RawMag.Length();
            if (MagAxesLabel != null)
            {
                MagAxesLabel.Text = $"Mx: {board.RawMag.X:+000.0;-000.0;000.0} µT   My: {board.RawMag.Y:+000.0;-000.0;000.0} µT   Mz: {board.RawMag.Z:+000.0;-000.0;000.0} µT";
                MagAxesLabel.Modulate = new Color(0.95f, 0.85f, 0.3f);
            }
            if (MagStrengthLabel != null)
            {
                MagStrengthLabel.Text = $"FIELD STRENGTH (|M|): {magTotal:F1} µT";
                MagStrengthLabel.Modulate = new Color(0.2f, 0.95f, 0.4f);
            }
        }
        else
        {
            if (MagAxesLabel != null)
            {
                MagAxesLabel.Text = "Mx: 0.0 µT   My: 0.0 µT   Mz: 0.0 µT";
                MagAxesLabel.Modulate = new Color(0.6f, 0.6f, 0.6f);
            }
            if (MagStrengthLabel != null)
            {
                MagStrengthLabel.Text = "MAGNETOMETER NOT CONNECTED";
                MagStrengthLabel.Modulate = new Color(0.6f, 0.6f, 0.6f);
            }
        }

        // 5. Hardware Link & Packet Telemetry
        if (HardwareLinklabel != null)
        {
            string src = board.Source.ToUpperInvariant();
            string status = board.IsActive ? $"ACTIVE ({board.Hz} HZ)" : "DISCONNECTED";
            string ip = string.IsNullOrEmpty(board.DeviceIp) ? "LOCAL" : board.DeviceIp;
            HardwareLinklabel.Text = $"SOURCE: {src} ({status}) | IP: {ip}";
            HardwareLinklabel.Modulate = board.IsActive ? new Color(0.2f, 0.95f, 0.4f) : new Color(0.9f, 0.4f, 0.3f);
        }

        if (HardwarePacketLabel != null)
        {
            float tsMs = board.TimestampUs * 0.001f;
            HardwarePacketLabel.Text = $"PACKET SEQ: #{board.Seq:D3}  |  TS: {tsMs:N1} ms";
        }

        if (ButtonStateLabel != null)
        {
            ButtonStateLabel.Text = board.IsButtonPressed ? "BUTTON: PRESSED (RESET)" : "BUTTON: RELEASED";
            ButtonStateLabel.Modulate = board.IsButtonPressed ? new Color(1.0f, 0.25f, 0.25f) : new Color(0.7f, 0.85f, 1.0f);
        }

        // 6. Recording Session Banner
        if (game is SensorLabGame lab && RecordStatusLabel != null)
        {
            if (lab.Recorder.IsRecording)
            {
                int sec = (int)lab.Recorder.DurationSec;
                int ms = (int)((lab.Recorder.DurationSec - sec) * 10);
                RecordStatusLabel.Text = $"🔴 RECORDING [{sec:D2}.{ms}s] • {lab.Recorder.SampleCount:N0} SAMPLES • 100 HZ";
                RecordStatusLabel.Modulate = new Color(1.0f, 0.25f, 0.25f);
            }
            else if (!string.IsNullOrEmpty(lab.Recorder.LastSavedPath))
            {
                string filename = System.IO.Path.GetFileName(lab.Recorder.LastSavedPath);
                RecordStatusLabel.Text = $"💾 SAVED: {filename} ({lab.Recorder.SampleCount:N0} SAMPLES)\n[PRESS SPACE / PHONE TO RECORD AGAIN]";
                RecordStatusLabel.Modulate = new Color(0.2f, 0.95f, 0.4f);
            }
            else
            {
                RecordStatusLabel.Text = "[PRESS SPACE / PHONE TO RECORD SESSION]";
                RecordStatusLabel.Modulate = new Color(0.7f, 0.85f, 1.0f);
            }
        }
    }

    private static string GetCompassDirection(float headingDeg)
    {
        string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int idx = (int)MathF.Round(headingDeg / 45.0f) % 8;
        return dirs[idx];
    }
}
