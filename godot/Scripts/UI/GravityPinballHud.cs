using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for Gravity Pinball (Pachinko Defense).
/// Displays Score, Active Balls, Reserve Balls, Combo Multiplier,
/// Tilt Warning indicator, Bumpers Hit, and Leaderboard overlay.
/// </summary>
public partial class GravityPinballHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? ActiveBallsLabel { get; set; }
    [Export] public Label? ReserveBallsLabel { get; set; }
    [Export] public Label? TiltWarningLabel { get; set; }
    [Export] public Label? BumpersLabel { get; set; }
    [Export] public Label? TargetsLabel { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        ActiveBallsLabel ??= GetNodeOrNull<Label>("%ActiveBallsLabel");
        ReserveBallsLabel ??= GetNodeOrNull<Label>("%ReserveBallsLabel");
        TiltWarningLabel ??= GetNodeOrNull<Label>("%TiltWarningLabel");
        BumpersLabel ??= GetNodeOrNull<Label>("%BumpersLabel");
        TargetsLabel ??= GetNodeOrNull<Label>("%TargetsLabel");

        LeaderboardOverlay ??= GetNodeOrNull<Control>("%LeaderboardOverlay");
        LeaderboardListLabel ??= GetNodeOrNull<Label>("%LeaderboardListLabel");
        GameOverTitleLabel ??= GetNodeOrNull<Label>("%GameOverTitleLabel");
    }

    public override void UpdateState(BoardState board, IGameMode game)
    {
        base.UpdateState(board, game);

        if (ScoreLabel != null)
        {
            ScoreLabel.Text = $"SCORE: {game.CurrentScore:N0}";
        }

        var stats = game.GetLiveStats();
        if (stats != null)
        {
            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1}";
                MultiplierLabel.Modulate = mult >= 3.0f
                    ? new Color(1.0f, 0.2f, 0.75f) // Magenta
                    : (mult > 1.2f ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.22f, 0.74f, 0.97f));
            }

            if (ActiveBallsLabel != null && stats.TryGetValue("activeBalls", out var actVal))
            {
                int active = Convert.ToInt32(actVal);
                ActiveBallsLabel.Text = active switch
                {
                    4 => "⚪⚪⚪⚪",
                    3 => "⚪⚪⚪",
                    2 => "⚪⚪",
                    1 => "⚪",
                    _ => "⚫"
                };
            }

            if (ReserveBallsLabel != null && stats.TryGetValue("reserveBalls", out var resVal))
            {
                int reserve = Convert.ToInt32(resVal);
                ReserveBallsLabel.Text = $"BALLS: {reserve}";
            }

            if (TiltWarningLabel != null)
            {
                bool isTilted = stats.TryGetValue("isTilted", out var tVal) && Convert.ToBoolean(tVal);
                if (isTilted)
                {
                    TiltWarningLabel.Text = "🚨 TILT! 🚨";
                    TiltWarningLabel.Modulate = new Color(0.95f, 0.2f, 0.2f);
                }
                else if (stats.TryGetValue("tiltWarnings", out var warnVal))
                {
                    int warns = Convert.ToInt32(warnVal);
                    TiltWarningLabel.Text = warns switch
                    {
                        2 => "⚠️ TILT [!!_]",
                        1 => "⚠️ TILT [!__]",
                        _ => "TILT [___]"
                    };
                    TiltWarningLabel.Modulate = warns > 0 ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.5f, 0.6f, 0.75f);
                }
            }

            if (BumpersLabel != null && stats.TryGetValue("bumpersHit", out var bVal))
            {
                BumpersLabel.Text = $"💥 {Convert.ToInt32(bVal)}";
            }

            if (TargetsLabel != null && stats.TryGetValue("dropTargetsDown", out var tgtVal))
            {
                TargetsLabel.Text = $"🎯 {Convert.ToInt32(tgtVal)}/4";
            }
        }

        // Leaderboard Overlay visibility
        if (LeaderboardOverlay != null)
        {
            bool isGameOver = game.GameState == "GAMEOVER";
            LeaderboardOverlay.Visible = isGameOver;

            if (isGameOver && LeaderboardListLabel != null)
            {
                UpdateLeaderboard(game);
            }
        }
    }

    private void UpdateLeaderboard(IGameMode game)
    {
        var entries = LeaderboardService.Instance.GetTopEntries();
        var sb = new StringBuilder();

        var stats = game.GetLiveStats();
        int rank = stats != null && stats.TryGetValue("latestRank", out var rVal) ? Convert.ToInt32(rVal) : -1;

        if (GameOverTitleLabel != null)
        {
            if (rank > 0 && rank <= 10)
            {
                GameOverTitleLabel.Text = $"🏆 NEW HIGH SCORE! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "PINBALL GAME OVER • TOP PLAYERS";
                GameOverTitleLabel.Modulate = new Color(0.95f, 0.45f, 0.15f);
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string badge = (i + 1) switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"#{i + 1:D2}"
            };
            string currentMarker = (i + 1 == rank) ? " ⭐" : "";
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance} Bumpers){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
