using System;
using Godot;

namespace WobbleHero.Core;

/// <summary>
/// Immutable snapshot representing instantaneous balance board orientation,
/// connection metrics, and controller button states.
/// </summary>
public readonly struct BoardState
{
    /// <summary>Normalized steering / roll: [-1.0 (hard left) .. +1.0 (hard right)]</summary>
    public float Roll { get; init; }

    /// <summary>Normalized pitch: [-1.0 (tilt forward) .. +1.0 (tilt back)]</summary>
    public float Pitch { get; init; }

    /// <summary>Absolute fused roll in degrees (typically -28° .. +28°)</summary>
    public float FusedRollDeg { get; init; }

    /// <summary>Absolute fused pitch in degrees (typically -28° .. +28°)</summary>
    public float FusedPitchDeg { get; init; }

    /// <summary>Tare zero calibration offset applied to roll axis (in degrees)</summary>
    public float CalOffsetRoll { get; init; }

    /// <summary>Tare zero calibration offset applied to pitch axis (in degrees)</summary>
    public float CalOffsetPitch { get; init; }

    /// <summary>True if receiving valid UDP packets within 350 ms timeout</summary>
    public bool IsActive { get; init; }

    /// <summary>True on rising edge / held state of hardware button on AtomS3</summary>
    public bool IsButtonPressed { get; init; }

    /// <summary>Rolling packet throughput in Hz</summary>
    public int Hz { get; init; }

    /// <summary>Rolling sequence counter (0..255)</summary>
    public byte Seq { get; init; }

    /// <summary>IP address of physical AtomS3 transmitter</summary>
    public string DeviceIp { get; init; }

    /// <summary>Input source identifier ("atoms3", "keyboard", "touch", "sim")</summary>
    public string Source { get; init; }

    public static BoardState Default => new()
    {
        Roll = 0f,
        Pitch = 0f,
        FusedRollDeg = 0f,
        FusedPitchDeg = 0f,
        CalOffsetRoll = 0f,
        CalOffsetPitch = 0f,
        IsActive = false,
        IsButtonPressed = false,
        Hz = 0,
        Seq = 0,
        DeviceIp = string.Empty,
        Source = "idle"
    };
}
