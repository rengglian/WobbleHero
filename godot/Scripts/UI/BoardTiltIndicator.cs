using System;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Reusable UI component displaying real-time balance board connection telemetry,
/// active update rate (Hz), roll/pitch tilt bars, and optional compass heading.
/// </summary>
public partial class BoardTiltIndicator : PanelContainer
{
    [Export] public Label? StatusLabel { get; set; }
    [Export] public ProgressBar? RollBar { get; set; }
    [Export] public ProgressBar? PitchBar { get; set; }
    [Export] public Label? HeadingLabel { get; set; }

    public override void _Ready()
    {
        StatusLabel ??= GetNodeOrNull<Label>("VBox/StatusLabel");
        RollBar ??= GetNodeOrNull<ProgressBar>("VBox/RollContainer/RollBar");
        PitchBar ??= GetNodeOrNull<ProgressBar>("VBox/PitchContainer/PitchBar");
        HeadingLabel ??= GetNodeOrNull<Label>("VBox/HeadingLabel");
    }

    public void UpdateState(BoardState state)
    {
        if (StatusLabel != null)
        {
            string src = state.Source.ToUpperInvariant();
            string status = state.IsActive ? $"ACTIVE ({state.Hz} HZ)" : "WAITING";
            StatusLabel.Text = $"BOARD: {src} ({status})";
            StatusLabel.Modulate = state.IsActive ? new Color(0.2f, 0.95f, 0.4f) : new Color(0.85f, 0.5f, 0.3f);
        }

        if (RollBar != null)
        {
            RollBar.Value = state.Roll;
        }

        if (PitchBar != null)
        {
            PitchBar.Value = state.Pitch;
        }

        if (HeadingLabel != null)
        {
            if (state.HasMag)
            {
                HeadingLabel.Visible = true;
                HeadingLabel.Text = $"COMPASS: {state.CompassHeadingDeg:F0}°";
            }
            else
            {
                HeadingLabel.Visible = false;
            }
        }
    }
}
