using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Input;

/// <summary>
/// Asynchronous UDP listener for M5Stack AtomS3 hardware balancing boards.
/// Listens on port 8080 / 8081 for 32-byte binary datagrams:
/// [magic: uint16 (0x4553), seq: uint8, button: uint8, timestamp_us: uint32, ax, ay, az, gx, gy, gz: float32 x 6]
/// Performs 2-axis (Roll and Pitch) complementary sensor fusion, tare offsets, and deadzones.
/// </summary>
public partial class HardwareUdpReceiver : Node
{
    [Export] public int ListenPort { get; set; } = 8080;
    [Export] public int AlternatePort { get; set; } = 8081;
    [Export] public float Sensitivity { get; set; } = 1.0f;
    [Export] public bool InvertRoll { get; set; } = true;
    [Export] public bool InvertPitch { get; set; } = true;

    public float Roll { get; private set; }
    public float Pitch { get; private set; }
    public float FusedRollDeg => InvertRoll ? -(_fusedRollDeg - CalOffsetRoll) : (_fusedRollDeg - CalOffsetRoll);
    public float FusedPitchDeg => InvertPitch ? -(_fusedPitchDeg - CalOffsetPitch) : (_fusedPitchDeg - CalOffsetPitch);
    public float CalOffsetRoll { get; set; } = 0.0f;
    public float CalOffsetPitch { get; set; } = 0.0f;

    public bool IsActive => _lastPacketTimeMs > 0 && (Time.GetTicksMsec() - _lastPacketTimeMs) < 350;
    public bool IsRestartPressed { get; private set; }
    public int PacketCount { get; private set; }
    public string DeviceIp { get; private set; } = string.Empty;
    public byte Seq { get; private set; }
    public byte Button { get; private set; }
    public int Hz { get; private set; }

    public Vector3 RawAccel { get; private set; }
    public Vector3 RawGyro { get; private set; }
    public Vector3 RawMag { get; private set; }
    public bool HasMag { get; private set; }
    public float CompassHeadingDeg { get; private set; }
    public uint TimestampUs { get; private set; }

    public event Action? OnRestartRequested;
    public event Action<BoardState>? OnPacketReceived;

    private UdpClient? _udpClientPrimary;
    private UdpClient? _udpClientSecondary;
    private CancellationTokenSource? _cts;
    private ulong _lastPacketTimeMs;
    private bool _lastRestartState;
    private int _hzPacketCounter;
    private ulong _hzWindowStartTimeMs;

    private float _fusedRollDeg;
    private float _fusedPitchDeg;
    private bool _hasFusedAngles;
    private uint _lastTimestampUs;
    private bool _hasLastTimestamp;

    private float _filteredRoll;
    private float _filteredPitch;

    private const ushort PacketMagic = 0x4553; // 'ES' in hex

    public override void _Ready()
    {
        StartListening();
    }

    public void StartListening()
    {
        StopListening();
        _cts = new CancellationTokenSource();
        _hzWindowStartTimeMs = Time.GetTicksMsec();

        // 1. Primary UDP listener
        try
        {
            _udpClientPrimary = new UdpClient(ListenPort);
            _udpClientPrimary.Client.ReceiveBufferSize = 1024 * 1024; // 1MB buffer to prevent OS packet drops
            Task.Run(() => ReceiveLoopAsync(_udpClientPrimary, ListenPort, _cts.Token));
            GD.Print($"[HardwareUdpReceiver] Listening on primary UDP port {ListenPort} for AtomS3 binary packets.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[HardwareUdpReceiver] Could not bind UDP port {ListenPort}: {ex.Message}");
        }

        // 2. Secondary UDP listener
        if (AlternatePort != ListenPort)
        {
            try
            {
                _udpClientSecondary = new UdpClient(AlternatePort);
                _udpClientSecondary.Client.ReceiveBufferSize = 1024 * 1024;
                Task.Run(() => ReceiveLoopAsync(_udpClientSecondary, AlternatePort, _cts.Token));
                GD.Print($"[HardwareUdpReceiver] Listening on secondary UDP port {AlternatePort} for AtomS3 binary packets.");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[HardwareUdpReceiver] Could not bind secondary UDP port {AlternatePort}: {ex.Message}");
            }
        }
    }

    private async Task ReceiveLoopAsync(UdpClient client, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && client != null)
        {
            try
            {
                var result = await client.ReceiveAsync(ct);
                ProcessPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    GD.PrintErr($"[HardwareUdpReceiver:{port}] Receive error: {ex.Message}");
                    await Task.Delay(100, ct);
                }
            }
        }
    }

    private void ProcessPacket(byte[] data, IPEndPoint remoteEndPoint)
    {
        if (data.Length < 32) return;

        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        if (magic != PacketMagic) return;

        byte seq = data[2];
        byte button = data[3];

        uint timestampUs = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        float ax = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8, 4));
        float ay = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(12, 4));
        float az = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(16, 4));
        float gx = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(20, 4));
        float gy = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(24, 4));
        float gz = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(28, 4));

        if (!float.IsFinite(ax) || !float.IsFinite(ay) || !float.IsFinite(az) ||
            !float.IsFinite(gx) || !float.IsFinite(gy) || !float.IsFinite(gz))
        {
            return;
        }

        RawAccel = new Vector3(ax, ay, az);
        RawGyro = new Vector3(gx, gy, gz);

        // Optional 3-DoF Magnetometer (Bytes 32-43 if 44-byte packet format)
        if (data.Length >= 44)
        {
            float mx = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(32, 4));
            float my = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(36, 4));
            float mz = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(40, 4));

            if (float.IsFinite(mx) && float.IsFinite(my) && float.IsFinite(mz))
            {
                RawMag = new Vector3(mx, my, mz);
                HasMag = RawMag.LengthSquared() > 0.01f;
                if (HasMag)
                {
                    float heading = Mathf.RadToDeg(MathF.Atan2(my, mx));
                    if (heading < 0f) heading += 360f;
                    CompassHeadingDeg = heading;
                }
                else
                {
                    CompassHeadingDeg = 0f;
                }
            }
        }
        else
        {
            RawMag = Vector3.Zero;
            HasMag = false;
            CompassHeadingDeg = 0f;
        }

        TimestampUs = timestampUs;
        Seq = seq;
        Button = button;

        ulong now = Time.GetTicksMsec();

        // 1. Calculate dt
        float dt = 0.01f;
        if (_hasLastTimestamp)
        {
            uint diffUs = timestampUs - _lastTimestampUs;
            if (diffUs > 0 && diffUs < 1_000_000)
            {
                dt = diffUs * 1e-6f;
            }
        }
        _lastTimestampUs = timestampUs;
        _hasLastTimestamp = true;
        dt = Math.Clamp(dt, 0.005f, 0.05f);

        // 2. Accelerometer angles (Roll around X/Y, Pitch around Y/X)
        float accelRollDeg = MathF.Atan2(ax, MathF.Sqrt(ay * ay + az * az)) * (180.0f / MathF.PI);
        float accelPitchDeg = MathF.Atan2(ay, MathF.Sqrt(ax * ax + az * az)) * (180.0f / MathF.PI);

        // 3. 2D Complementary filter
        if (!_hasFusedAngles || (now - _lastPacketTimeMs) > 500)
        {
            _fusedRollDeg = accelRollDeg;
            _fusedPitchDeg = accelPitchDeg;
            _hasFusedAngles = true;
        }
        else
        {
            // Gyroscope integration (gy = roll rate, gx = pitch rate)
            _fusedRollDeg = 0.96f * (_fusedRollDeg + gy * dt) + 0.04f * accelRollDeg;
            _fusedPitchDeg = 0.96f * (_fusedPitchDeg + gx * dt) + 0.04f * accelPitchDeg;
        }

        // 4. Tare calibration offset
        float calRoll = _fusedRollDeg - CalOffsetRoll;
        float calPitch = _fusedPitchDeg - CalOffsetPitch;

        // 5. Normalization with ergonomic comfort limit
        // Elevated round boards (like Koor Board) operate comfortably within ~18° - 20°
        const float maxComfortDeg = 20.0f;
        float rawNormRoll = Math.Clamp(calRoll / maxComfortDeg, -1.0f, 1.0f);
        float rawNormPitch = Math.Clamp(calPitch / maxComfortDeg, -1.0f, 1.0f);

        // 6. Progressive Deadzone & Curve:
        // Standing on an elevated dome pivot creates continuous foot micro-tremors (~1.5°).
        // A 0.07 deadzone with a 1.35x power curve allows stable standing near zero while keeping full lean range.
        const float deadzone = 0.07f;
        rawNormRoll = ApplyDeadzoneAndCurve(rawNormRoll, deadzone);
        rawNormPitch = ApplyDeadzoneAndCurve(rawNormPitch, deadzone);

        // 7. Exponential smoothing (0.18 at 100 Hz filters high-frequency standing ankle wobble)
        _filteredRoll += (rawNormRoll - _filteredRoll) * 0.18f;
        _filteredPitch += (rawNormPitch - _filteredPitch) * 0.18f;

        // 8. Inversion and Sensitivity
        float finalRoll = InvertRoll ? -_filteredRoll : _filteredRoll;
        float finalPitch = InvertPitch ? -_filteredPitch : _filteredPitch;

        Roll = Math.Clamp(finalRoll * Sensitivity, -1.0f, 1.0f);
        Pitch = Math.Clamp(finalPitch * Sensitivity, -1.0f, 1.0f);

        _lastPacketTimeMs = now;
        PacketCount++;
        DeviceIp = remoteEndPoint.Address.ToString();

        // Hz metric
        _hzPacketCounter++;
        ulong dtHz = now - _hzWindowStartTimeMs;
        if (dtHz >= 1000)
        {
            Hz = (int)Math.Round(_hzPacketCounter * 1000.0 / dtHz);
            _hzPacketCounter = 0;
            _hzWindowStartTimeMs = now;
        }
        else if (Hz == 0)
        {
            Hz = 100;
        }

        // Button edge-detection
        bool currentRestart = button > 0;
        if (currentRestart && !_lastRestartState)
        {
            IsRestartPressed = true;
            OnRestartRequested?.Invoke();
        }
        else if (!currentRestart)
        {
            IsRestartPressed = false;
        }
        _lastRestartState = currentRestart;

        // Dispatch raw packet arrival for telemetry and high-frequency recording
        var snapshot = new BoardState
        {
            Roll = Roll,
            Pitch = Pitch,
            FusedRollDeg = FusedRollDeg,
            FusedPitchDeg = FusedPitchDeg,
            CalOffsetRoll = CalOffsetRoll,
            CalOffsetPitch = CalOffsetPitch,
            IsActive = true,
            IsButtonPressed = IsRestartPressed,
            Hz = Hz,
            Seq = Seq,
            RawAccel = RawAccel,
            RawGyro = RawGyro,
            RawMag = RawMag,
            HasMag = HasMag,
            CompassHeadingDeg = CompassHeadingDeg,
            TimestampUs = TimestampUs,
            DeviceIp = DeviceIp,
            Source = "atoms3"
        };
        OnPacketReceived?.Invoke(snapshot);
    }

    private static float ApplyDeadzoneAndCurve(float val, float deadzone)
    {
        float abs = MathF.Abs(val);
        if (abs <= deadzone) return 0f;

        // Smoothly ramp from 0 at the deadzone edge up to 1.0 at maximum tilt
        float remapped = (abs - deadzone) / (1.0f - deadzone);
        // Progressive curve (1.35x power) gives fine micro-control near level, but full range on deep lean
        float curved = MathF.Pow(remapped, 1.35f);
        return MathF.Sign(val) * curved;
    }

    public void TareZero()
    {
        CalOffsetRoll = _fusedRollDeg;
        CalOffsetPitch = _fusedPitchDeg;
        _filteredRoll = 0f;
        _filteredPitch = 0f;
        GD.Print($"[HardwareUdpReceiver] Tare zero set: Roll={CalOffsetRoll:F2}°, Pitch={CalOffsetPitch:F2}°");
    }

    public void ResetCalibration()
    {
        CalOffsetRoll = 0.0f;
        CalOffsetPitch = 0.0f;
        GD.Print("[HardwareUdpReceiver] Calibration reset to 0.0°");
    }

    public void StopListening()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _udpClientPrimary?.Close();
        _udpClientPrimary?.Dispose();
        _udpClientPrimary = null;

        _udpClientSecondary?.Close();
        _udpClientSecondary?.Dispose();
        _udpClientSecondary = null;
    }

    public override void _ExitTree()
    {
        StopListening();
    }
}
