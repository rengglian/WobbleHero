using System;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Input;

/// <summary>
/// Unified input manager providing high-level balance board telemetry.
/// Seamlessly blends physical AtomS3 UDP hardware with keyboard and touch fallback.
/// </summary>
public partial class BalanceBoardInput : Node
{
    [Export] public HardwareUdpReceiver? HardwareReceiver { get; set; }

    public BoardState CurrentState { get; private set; } = BoardState.Default;

    public float Sensitivity
    {
        get => HardwareReceiver?.Sensitivity ?? _localSensitivity;
        set
        {
            _localSensitivity = value;
            if (HardwareReceiver != null) HardwareReceiver.Sensitivity = value;
        }
    }

    public bool InvertRoll
    {
        get => HardwareReceiver?.InvertRoll ?? _localInvertRoll;
        set
        {
            _localInvertRoll = value;
            if (HardwareReceiver != null) HardwareReceiver.InvertRoll = value;
        }
    }

    public bool InvertPitch
    {
        get => HardwareReceiver?.InvertPitch ?? _localInvertPitch;
        set
        {
            _localInvertPitch = value;
            if (HardwareReceiver != null) HardwareReceiver.InvertPitch = value;
        }
    }

    public event Action? OnRestartRequested;
    public event Action<BoardState>? OnPacketReceived;

    private float _localSensitivity = 1.0f;
    private bool _localInvertRoll;
    private bool _localInvertPitch;

    private float _simRoll;
    private float _simPitch;
    private bool _touchPressed;
    private Vector2 _touchPos;

    public override void _Ready()
    {
        if (HardwareReceiver == null)
        {
            HardwareReceiver = GetNodeOrNull<HardwareUdpReceiver>("HardwareUdpReceiver");
        }

        if (HardwareReceiver != null)
        {
            HardwareReceiver.OnRestartRequested += () => OnRestartRequested?.Invoke();
            HardwareReceiver.OnPacketReceived += (snapshot) => OnPacketReceived?.Invoke(snapshot);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch)
        {
            _touchPressed = touch.Pressed;
            _touchPos = touch.Position;
        }
        else if (@event is InputEventScreenDrag drag)
        {
            _touchPos = drag.Position;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 1. Check if hardware AtomS3 board is transmitting
        if (HardwareReceiver != null && HardwareReceiver.IsActive)
        {
            CurrentState = new BoardState
            {
                Roll = HardwareReceiver.Roll,
                Pitch = HardwareReceiver.Pitch,
                FusedRollDeg = HardwareReceiver.FusedRollDeg,
                FusedPitchDeg = HardwareReceiver.FusedPitchDeg,
                CalOffsetRoll = HardwareReceiver.CalOffsetRoll,
                CalOffsetPitch = HardwareReceiver.CalOffsetPitch,
                IsActive = true,
                IsButtonPressed = HardwareReceiver.IsRestartPressed,
                Hz = HardwareReceiver.Hz,
                Seq = HardwareReceiver.Seq,
                RawAccel = HardwareReceiver.RawAccel,
                RawGyro = HardwareReceiver.RawGyro,
                TimestampUs = HardwareReceiver.TimestampUs,
                DeviceIp = HardwareReceiver.DeviceIp,
                Source = "atoms3"
            };
            return;
        }

        // 2. Fallback: Keyboard Emulation
        float targetRoll = 0.0f;
        float targetPitch = 0.0f;

        if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) targetRoll -= 1.0f;
        if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) targetRoll += 1.0f;
        if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) targetPitch -= 1.0f;
        if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) targetPitch += 1.0f;

        bool hasKeyboard = targetRoll != 0f || targetPitch != 0f;

        // 3. Fallback: Touch Emulation
        bool hasTouch = false;
        if (!hasKeyboard && _touchPressed)
        {
            Vector2 viewportSize = GetviewportRect().Size;
            if (viewportSize.X > 0 && viewportSize.Y > 0)
            {
                // Map screen touch relative to center (-1.0 .. +1.0)
                targetRoll = Math.Clamp((_touchPos.X - viewportSize.X * 0.5f) / (viewportSize.X * 0.4f), -1.0f, 1.0f);
                targetPitch = Math.Clamp((_touchPos.Y - viewportSize.Y * 0.5f) / (viewportSize.Y * 0.4f), -1.0f, 1.0f);
                hasTouch = true;
            }
        }

        // Smooth simulated input
        float smoothSpeed = (hasKeyboard || hasTouch) ? 8.0f : 12.0f;
        _simRoll = Mathf.MoveToward(_simRoll, targetRoll, smoothSpeed * dt);
        _simPitch = Mathf.MoveToward(_simPitch, targetPitch, smoothSpeed * dt);

        float finalRoll = (InvertRoll ? -_simRoll : _simRoll) * Sensitivity;
        float finalPitch = (InvertPitch ? -_simPitch : _simPitch) * Sensitivity;

        CurrentState = new BoardState
        {
            Roll = Math.Clamp(finalRoll, -1.0f, 1.0f),
            Pitch = Math.Clamp(finalPitch, -1.0f, 1.0f),
            FusedRollDeg = finalRoll * 28.0f,
            FusedPitchDeg = finalPitch * 28.0f,
            CalOffsetRoll = 0f,
            CalOffsetPitch = 0f,
            IsActive = hasKeyboard || hasTouch,
            IsButtonPressed = Godot.Input.IsKeyPressed(Key.Space) || Godot.Input.IsKeyPressed(Key.R),
            Hz = 60,
            Seq = 0,
            DeviceIp = "local",
            Source = hasKeyboard ? "keyboard" : (hasTouch ? "touch" : "idle")
        };
    }

    private Rect2 GetviewportRect() => GetViewport().GetVisibleRect();

    public void TareZero()
    {
        HardwareReceiver?.TareZero();
    }

    public void ResetCalibration()
    {
        HardwareReceiver?.ResetCalibration();
    }
}
