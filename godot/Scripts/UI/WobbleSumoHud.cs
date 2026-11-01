using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for Wobble Sumo (Ring King).
/// Displays Score, Knockouts, Opponents Remaining, Ring Integrity %,
/// Wave/Round, Lives, Shockwave Ready Indicator, and Top-10 Leaderboard.
/// </summary>
public partial class WobbleSumoHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? WaveLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }
    [Export] public Label? KnockoutsLabel { get; set; }
    [Export] public Label? FoesRemainingLabel { get; set; }
    [Export] public Label? RingIntegrityLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? LivesLabel { get; set; }
    [Export] public Label? ShockwaveStatusLabel { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        WaveLabel ??= GetNodeOrNull<Label>("%WaveLabel");
        StateLabel ??= GetNodeOrNull<Label>("%StateLabel");
        KnockoutsLabel ??= GetNodeOrNull<Label>("%KnockoutsLabel");
        FoesRemainingLabel ??= GetNodeOrNull<Label>("%FoesRemainingLabel");
        RingIntegrityLabel ??= GetNodeOrNull<Label>("%RingIntegrityLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        LivesLabel ??= GetNodeOrNull<Label>("%LivesLabel");
        ShockwaveStatusLabel ??= GetNodeOrNull<Label>("%ShockwaveStatusLabel");

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

        if (StateLabel != null)
        {
            StateLabel.Text = game.GameState;
        }

        var stats = game.GetLiveStats();
        if (stats != null)
        {
            if (WaveLabel != null && stats.TryGetValue("wave", out var waveVal))
            {
                WaveLabel.Text = $"WAVE {Convert.ToInt32(waveVal)}";
            }

            if (KnockoutsLabel != null && stats.TryGetValue("knockouts", out var koVal))
            {
                KnockoutsLabel.Text = $"KO: {Convert.ToInt32(koVal)}";
            }

            if (FoesRemainingLabel != null && stats.TryGetValue("opponentsRemaining", out var foeVal))
            {
                FoesRemainingLabel.Text = $"FOES: {Convert.ToInt32(foeVal)}";
            }

            if (RingIntegrityLabel != null && stats.TryGetValue("ringIntegrity", out var ringVal))
            {
                int ring = Convert.ToInt32(ringVal);
                RingIntegrityLabel.Text = $"RING: {ring}%";
                RingIntegrityLabel.Modulate = ring > 75
                    ? new Color(0.2f, 0.95f, 0.45f)
                    : (ring > 50 ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.95f, 0.25f, 0.25f));
            }

            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1}";
                MultiplierLabel.Modulate = mult >= 2.5f
                    ? new Color(1.0f, 0.25f, 0.2f)
                    : (mult > 1.2f ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.22f, 0.74f, 0.97f));
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

            if (ShockwaveStatusLabel != null && stats.TryGetValue("shockwaveReady", out var readyVal))
            {
                bool isReady = Convert.ToBoolean(readyVal);
                if (isReady)
                {
                    ShockwaveStatusLabel.Text = "💥 SLAM READY!";
                    ShockwaveStatusLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
                }
                else if (stats.TryGetValue("shockwaveCooldown", out var cdVal))
                {
                    float cd = Convert.ToSingle(cdVal);
                    ShockwaveStatusLabel.Text = $"⏳ CHARGING ({cd:F1}s)";
                    ShockwaveStatusLabel.Modulate = new Color(0.6f, 0.75f, 0.9f);
                }
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
                GameOverTitleLabel.Text = $"🏆 NEW RECORD! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "MATCH FINISHED • TOP SUMO RINGS";
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  (Wave {e.Seed}){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
