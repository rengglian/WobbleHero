using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

namespace WobbleHero.Core;

public record struct MovementSample(
    float ElapsedSec,
    uint TimestampUs,
    byte Seq,
    float Ax, float Ay, float Az,
    float Gx, float Gy, float Gz,
    float Mx, float My, float Mz,
    float FusedRollDeg, float FusedPitchDeg,
    float Roll, float Pitch,
    float CalOffsetRoll, float CalOffsetPitch,
    byte Button
);

/// <summary>
/// High-resolution motion recorder that captures raw 6-axis IMU data,
/// fused orientations, and timestamps for offline filter analysis and tuning.
/// </summary>
public class MovementRecorder
{
    public bool IsRecording { get; private set; }
    public int SampleCount
    {
        get
        {
            lock (_lock) return _samples.Count;
        }
    }
    public float DurationSec => IsRecording ? (Time.GetTicksMsec() - _startTimeMs) * 0.001f : _finalDurationSec;
    public string LastSavedPath { get; private set; } = string.Empty;
    public string LastSessionCsv { get; private set; } = string.Empty;

    public event Action? OnRecordingStarted;
    public event Action<string, int>? OnRecordingStopped; // path, sample count

    private readonly object _lock = new();
    private readonly List<MovementSample> _samples = new(6000); // 1 minute @ 100 Hz pre-allocated
    private ulong _startTimeMs;
    private float _finalDurationSec;

    public void StartRecording()
    {
        lock (_lock)
        {
            _samples.Clear();
            _startTimeMs = Time.GetTicksMsec();
            _finalDurationSec = 0f;
            IsRecording = true;
            LastSavedPath = string.Empty;
            LastSessionCsv = string.Empty;
        }
        GD.Print("[MovementRecorder] Session recording started.");
        OnRecordingStarted?.Invoke();
    }

    public void RecordSample(BoardState board)
    {
        if (!IsRecording) return;

        float elapsed = (Time.GetTicksMsec() - _startTimeMs) * 0.001f;
        var sample = new MovementSample(
            elapsed,
            board.TimestampUs,
            board.Seq,
            board.RawAccel.X, board.RawAccel.Y, board.RawAccel.Z,
            board.RawGyro.X, board.RawGyro.Y, board.RawGyro.Z,
            board.RawMag.X, board.RawMag.Y, board.RawMag.Z,
            board.FusedRollDeg, board.FusedPitchDeg,
            board.Roll, board.Pitch,
            board.CalOffsetRoll, board.CalOffsetPitch,
            board.IsButtonPressed ? (byte)1 : (byte)0
        );

        lock (_lock)
        {
            if (IsRecording)
            {
                _samples.Add(sample);
            }
        }
    }

    public string StopRecording()
    {
        List<MovementSample> samplesCopy;
        lock (_lock)
        {
            if (!IsRecording) return LastSavedPath;

            IsRecording = false;
            _finalDurationSec = (Time.GetTicksMsec() - _startTimeMs) * 0.001f;
            samplesCopy = new List<MovementSample>(_samples);
        }

        // Generate CSV content
        var sb = new StringBuilder();
        sb.AppendLine("elapsed_sec,timestamp_us,seq,ax_g,ay_g,az_g,gx_degs,gy_degs,gz_degs,mx_ut,my_ut,mz_ut,fused_roll_deg,fused_pitch_deg,norm_roll,norm_pitch,cal_offset_roll,cal_offset_pitch,button");

        var inv = CultureInfo.InvariantCulture;
        foreach (var s in samplesCopy)
        {
            sb.Append(s.ElapsedSec.ToString("F4", inv)).Append(',')
              .Append(s.TimestampUs).Append(',')
              .Append(s.Seq).Append(',')
              .Append(s.Ax.ToString("F4", inv)).Append(',')
              .Append(s.Ay.ToString("F4", inv)).Append(',')
              .Append(s.Az.ToString("F4", inv)).Append(',')
              .Append(s.Gx.ToString("F3", inv)).Append(',')
              .Append(s.Gy.ToString("F3", inv)).Append(',')
              .Append(s.Gz.ToString("F3", inv)).Append(',')
              .Append(s.Mx.ToString("F2", inv)).Append(',')
              .Append(s.My.ToString("F2", inv)).Append(',')
              .Append(s.Mz.ToString("F2", inv)).Append(',')
              .Append(s.FusedRollDeg.ToString("F2", inv)).Append(',')
              .Append(s.FusedPitchDeg.ToString("F2", inv)).Append(',')
              .Append(s.Roll.ToString("F4", inv)).Append(',')
              .Append(s.Pitch.ToString("F4", inv)).Append(',')
              .Append(s.CalOffsetRoll.ToString("F2", inv)).Append(',')
              .Append(s.CalOffsetPitch.ToString("F2", inv)).Append(',')
              .Append(s.Button)
              .AppendLine();
        }

        LastSessionCsv = sb.ToString();

        // 1. Save to user://recordings/
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"session_{timestamp}.csv";

        DirAccess.MakeDirAbsolute("user://recordings");
        string godotPath = $"user://recordings/{filename}";
        using (var file = Godot.FileAccess.Open(godotPath, Godot.FileAccess.ModeFlags.Write))
        {
            if (file != null)
            {
                file.StoreString(LastSessionCsv);
            }
        }

        // 2. Also try writing directly to local project recordings directory if accessible (for desktop dev)
        string globalPath = ProjectSettings.GlobalizePath(godotPath);
        LastSavedPath = globalPath;

        try
        {
            string projectRecordingsDir = Path.Combine(Directory.GetCurrentDirectory(), "recordings");
            if (!Directory.Exists(projectRecordingsDir))
            {
                Directory.CreateDirectory(projectRecordingsDir);
            }
            string localFilePath = Path.Combine(projectRecordingsDir, filename);
            File.WriteAllText(localFilePath, LastSessionCsv);
            LastSavedPath = localFilePath;
            GD.Print($"[MovementRecorder] Exported local copy to {localFilePath}");
        }
        catch { }

        GD.Print($"[MovementRecorder] Recording stopped. {_samples.Count} samples captured ({_finalDurationSec:F1}s). Saved to: {LastSavedPath}");
        OnRecordingStopped?.Invoke(LastSavedPath, _samples.Count);
        return LastSavedPath;
    }
}
