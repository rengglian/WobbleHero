using System;
using Godot;
using WobbleHero.Core;
using WobbleHero.Games;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game 1: Balance Trainer.
/// Displays styled score, dynamic combo multiplier, target ring collection count,
/// in-game event banners, and embedded reusable telemetry indicators.
/// </summary>
public partial class BalanceTrainerHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? RingsLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        RingsLabel ??= GetNodeOrNull<Label>("%RingsLabel");
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
            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1} COMBO";
                MultiplierLabel.Modulate = mult > 1.5f ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.85f, 0.2f);
            }

            if (RingsLabel != null && stats.TryGetValue("ringsCollected", out var ringsVal))
            {
                RingsLabel.Text = $"RINGS: {ringsVal}";
            }
        }
    }
}
