using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for Downhill Slalom (Alpine Speed Carver).
/// Displays Speed in km/h (with speed-tuck glow indicator), Distance,
/// Gates Cleared/Missed, Course Preset badge, and Leaderboard overlay.
/// </summary>
public partial class DownhillSlalomHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? SpeedLabel { get; set; }
    [Export] public Label? DistanceLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? GatesRatioLabel { get; set; }
    [Export] public Label? CourseBadgeLabel { get; set; }
    [Export] public Label? TuckStatusLabel { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        SpeedLabel ??= GetNodeOrNull<Label>("%SpeedLabel");
        DistanceLabel ??= GetNodeOrNull<Label>("%DistanceLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        GatesRatioLabel ??= GetNodeOrNull<Label>("%GatesRatioLabel");
        CourseBadgeLabel ??= GetNodeOrNull<Label>("%CourseBadgeLabel");
        TuckStatusLabel ??= GetNodeOrNull<Label>("%TuckStatusLabel");

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
            if (SpeedLabel != null && stats.TryGetValue("speedKmh", out var spdVal))
            {
                int spd = Convert.ToInt32(spdVal);
                SpeedLabel.Text = $"{spd} KM/H";

                bool isTuck = stats.TryGetValue("isTucking", out var tuckVal) && Convert.ToBoolean(tuckVal);
                if (isTuck)
                {
                    SpeedLabel.Modulate = new Color(0.2f, 0.95f, 0.45f); // Tuck Green
                    if (TuckStatusLabel != null)
                    {
                        TuckStatusLabel.Visible = true;
                        TuckStatusLabel.Text = "⚡ SPEED-TUCK";
                    }
                }
                else
                {
                    SpeedLabel.Modulate = spd > 90 ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.22f, 0.74f, 0.97f);
                    if (TuckStatusLabel != null) TuckStatusLabel.Visible = false;
                }
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
                    ? new Color(0.95f, 0.2f, 0.25f)
                    : (mult > 1.2f ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.22f, 0.74f, 0.97f));
            }

            if (GatesRatioLabel != null && stats.TryGetValue("gatesCleared", out var clrVal) && stats.TryGetValue("gatesMissed", out var misVal))
            {
                int clr = Convert.ToInt32(clrVal);
                int mis = Convert.ToInt32(misVal);
                GatesRatioLabel.Text = $"🚩 {clr} / {clr + mis}";
            }

            if (CourseBadgeLabel != null && stats.TryGetValue("courseType", out var cVal))
            {
                string course = Convert.ToString(cVal)?.Replace('_', ' ').ToUpperInvariant() ?? "GIANT SLALOM";
                CourseBadgeLabel.Text = course;
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
                GameOverTitleLabel.Text = $"🏆 NEW PODIUM RECORD! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "RUN FINISHED • TOP SKIERS";
                GameOverTitleLabel.Modulate = new Color(0.95f, 0.35f, 0.2f);
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance} Gates){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
