using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for River Log Roll (White-Water Rapids).
/// Displays River Speed, Log Spin RPM, Equilibrium Balance Gauge with sweet spot brackets,
/// Distance, Multiplier, Golden Salmon count, Lives, and Leaderboard overlay.
/// </summary>
public partial class RiverLogRollHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? RiverSpeedLabel { get; set; }
    [Export] public Label? LogRpmLabel { get; set; }
    [Export] public Label? DistanceLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? SalmonLabel { get; set; }
    [Export] public Label? LivesLabel { get; set; }
    [Export] public Label? BalanceStatusLabel { get; set; }
    [Export] public Control? BalancePointerControl { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        RiverSpeedLabel ??= GetNodeOrNull<Label>("%RiverSpeedLabel");
        LogRpmLabel ??= GetNodeOrNull<Label>("%LogRpmLabel");
        DistanceLabel ??= GetNodeOrNull<Label>("%DistanceLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        SalmonLabel ??= GetNodeOrNull<Label>("%SalmonLabel");
        LivesLabel ??= GetNodeOrNull<Label>("%LivesLabel");
        BalanceStatusLabel ??= GetNodeOrNull<Label>("%BalanceStatusLabel");
        BalancePointerControl ??= GetNodeOrNull<Control>("%BalancePointer");

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
            if (RiverSpeedLabel != null && stats.TryGetValue("riverSpeedKmh", out var spdVal))
            {
                RiverSpeedLabel.Text = $"{Convert.ToInt32(spdVal)} KM/H";
            }

            if (LogRpmLabel != null && stats.TryGetValue("logRpm", out var rpmVal))
            {
                LogRpmLabel.Text = $"{Convert.ToInt32(rpmVal)} RPM";
            }

            if (DistanceLabel != null && stats.TryGetValue("distance", out var distVal))
            {
                DistanceLabel.Text = $"DIST: {Convert.ToInt32(distVal):N0}m";
            }

            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1}";
                MultiplierLabel.Modulate = mult >= 3.0f
                    ? new Color(1.0f, 0.85f, 0.2f) // Gold
                    : (mult > 1.2f ? new Color(0.2f, 0.95f, 0.45f) : new Color(0.22f, 0.74f, 0.97f));
            }

            if (SalmonLabel != null && stats.TryGetValue("salmonCollected", out var fishVal))
            {
                SalmonLabel.Text = $"🐟 {Convert.ToInt32(fishVal)}";
            }

            if (LivesLabel != null && stats.TryGetValue("lives", out var lifeVal))
            {
                int lives = Math.Max(0, Convert.ToInt32(lifeVal));
                LivesLabel.Text = lives switch
                {
                    3 => "❤️❤️❤️",
                    2 => "❤️❤️🖤",
                    1 => "❤️🖤🖤",
                    _ => "🖤🖤🖤"
                };
            }

            if (BalanceStatusLabel != null && stats.TryGetValue("isInSweetSpot", out var sweetVal))
            {
                bool sweet = Convert.ToBoolean(sweetVal);
                if (sweet)
                {
                    BalanceStatusLabel.Text = "✨ SWEET SPOT";
                    BalanceStatusLabel.Modulate = new Color(0.2f, 0.95f, 0.45f);
                }
                else if (stats.TryGetValue("balanceErrorDeg", out var errVal))
                {
                    float err = Convert.ToSingle(errVal);
                    string dir = err > 0 ? "LEAN BACK ▼" : "LEAN FWD ▲";
                    BalanceStatusLabel.Text = $"⚠️ {dir} ({MathF.Abs(err):F1}°)";
                    BalanceStatusLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
                }
            }

            // Move pointer on balance meter bar
            if (BalancePointerControl != null && stats.TryGetValue("balanceErrorDeg", out var bVal))
            {
                float err = Convert.ToSingle(bVal);
                float clamped = Math.Clamp(err, -20.0f, 20.0f);
                // Map [-20, +20] deg to [-50, +50] px offset
                BalancePointerControl.Position = new Vector2(clamped * 2.5f, BalancePointerControl.Position.Y);
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
                GameOverTitleLabel.Text = $"🏆 NEW BIRLING RECORD! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "RUN TERMINATED • TOP LOG ROLLERS";
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance} Fish){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
