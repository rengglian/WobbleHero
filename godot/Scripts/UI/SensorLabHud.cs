using System;
using Godot;
using WobbleHero.Core;
using WobbleHero.Games;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game 3: Sensor Lab & Diagnostics Bench.
/// Features large typography for live roll/pitch/heading angles, high-visibility
/// recording status banner with sample counter, and reusable telemetry cards.
/// </summary>
public partial class SensorLabHud : BaseGameHud
{
    [Export] public Label? RollDegLabel { get; set; }
    [Export] public Label? PitchDegLabel { get; set; }
    [Export] public Label? HeadingDegLabel { get; set; }
    [Export] public Label? RecordStatusLabel { get; set; }
    [Export] public PanelContainer? RecordPanel { get; set; }

    public override void _Ready()
    {
        RollDegLabel ??= GetNodeOrNull<Label>("%RollDegLabel");
        PitchDegLabel ??= GetNodeOrNull<Label>("%PitchDegLabel");
        HeadingDegLabel ??= GetNodeOrNull<Label>("%HeadingDegLabel");
        RecordStatusLabel ??= GetNodeOrNull<Label>("%RecordStatusLabel");
        RecordPanel ??= GetNodeOrNull<PanelContainer>("%RecordPanel");
    }

    public override void UpdateState(BoardState board, IGameMode game)
    {
        base.UpdateState(board, game);

        string signRoll = board.FusedRollDeg >= 0 ? "+" : "";
        string signPitch = board.FusedPitchDeg >= 0 ? "+" : "";

        if (RollDegLabel != null)
        {
            RollDegLabel.Text = $"ROLL: {signRoll}{board.FusedRollDeg:F1}°";
        }

        if (PitchDegLabel != null)
        {
            PitchDegLabel.Text = $"PITCH: {signPitch}{board.FusedPitchDeg:F1}°";
        }

        if (HeadingDegLabel != null)
        {
            if (board.HasMag)
            {
                HeadingDegLabel.Visible = true;
                HeadingDegLabel.Text = $"COMPASS: {board.CompassHeadingDeg:F0}°";
            }
            else
            {
                HeadingDegLabel.Visible = false;
            }
        }

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
}
