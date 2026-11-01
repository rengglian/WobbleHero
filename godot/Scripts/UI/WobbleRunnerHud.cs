using System;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game 2: Wobble Runner.
/// Displays distance survived, current speed, score, obstacle alerts,
/// and embedded reusable telemetry indicators.
/// </summary>
public partial class WobbleRunnerHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? DistanceLabel { get; set; }
    [Export] public Label? SpeedLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        DistanceLabel ??= GetNodeOrNull<Label>("%DistanceLabel");
        SpeedLabel ??= GetNodeOrNull<Label>("%SpeedLabel");
        StateLabel ??= GetNodeOrNull<Label>("%StateLabel");
    }

    public override void UpdateState(BoardState board, IGameMode game)
    {
        base.UpdateState(board, game);

        if (ScoreLabel != null)
        {
            ScoreLabel.Text = $"SCORE: {game.CurrentScore:N0}";
        }

        if (StateLabel != null)
        {
            StateLabel.Text = game.GameState;
        }

        var stats = game.GetLiveStats();
        if (stats != null)
        {
            if (DistanceLabel != null && stats.TryGetValue("distance", out var distVal))
            {
                float dist = Convert.ToSingle(distVal);
                DistanceLabel.Text = $"DISTANCE: {dist:F0}m";
            }

            if (SpeedLabel != null && stats.TryGetValue("speed", out var speedVal))
            {
                float speed = Convert.ToSingle(speedVal);
                SpeedLabel.Text = $"SPEED: {speed:F0} KM/H";
            }
        }
    }
}
