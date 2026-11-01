using System;
using System.Text;
using Godot;
using WobbleHero.Core;
using WobbleHero.Games;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game 2: Wobble Runner (Endless Ride Approach).
/// Displays distance survived, speed in km/h, active seed, combo multiplier,
/// collectible badges, and persistent top-10 leaderboard overlay on game over.
/// </summary>
public partial class WobbleRunnerHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? DistanceLabel { get; set; }
    [Export] public Label? SpeedLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }
    [Export] public Label? SeedLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? CollectiblesLabel { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        DistanceLabel ??= GetNodeOrNull<Label>("%DistanceLabel");
        SpeedLabel ??= GetNodeOrNull<Label>("%SpeedLabel");
        StateLabel ??= GetNodeOrNull<Label>("%StateLabel");
        SeedLabel ??= GetNodeOrNull<Label>("%SeedLabel");
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        CollectiblesLabel ??= GetNodeOrNull<Label>("%CollectiblesLabel");

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
            if (DistanceLabel != null && stats.TryGetValue("distance", out var distVal))
            {
                float dist = Convert.ToSingle(distVal);
                DistanceLabel.Text = $"DIST: {dist:F0}m";
            }

            if (SpeedLabel != null && stats.TryGetValue("speed", out var speedVal))
            {
                float speed = Convert.ToSingle(speedVal);
                SpeedLabel.Text = $"{speed:F0} KM/H";
            }

            if (SeedLabel != null && stats.TryGetValue("seed", out var seedVal))
            {
                int seed = Convert.ToInt32(seedVal);
                SeedLabel.Text = $"SEED #{seed}";
            }

            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1}";
                MultiplierLabel.Modulate = mult > 1.5f ? new Color(1.0f, 0.45f, 0.15f) : new Color(1.0f, 0.85f, 0.2f);
            }

            if (CollectiblesLabel != null)
            {
                int apples = stats.TryGetValue("apples", out var aVal) ? Convert.ToInt32(aVal) : 0;
                int horseshoes = stats.TryGetValue("horseshoes", out var hVal) ? Convert.ToInt32(hVal) : 0;
                CollectiblesLabel.Text = $"🍎 {apples}  🐴 {horseshoes}";
            }
        }

        // Leaderboard Overlay visibility
        if (LeaderboardOverlay != null)
        {
            bool isGameOver = game.GameState == "GAMEOVER";
            LeaderboardOverlay.Visible = isGameOver;

            if (isGameOver && LeaderboardListLabel != null)
            {
                UpdateLeaderboardDisplay(game);
            }
        }
    }

    private void UpdateLeaderboardDisplay(IGameMode game)
    {
        var entries = LeaderboardService.Instance.GetTopEntries();
        var sb = new StringBuilder();

        var stats = game.GetLiveStats();
        int latestRank = stats != null && stats.TryGetValue("latestRank", out var rVal) ? Convert.ToInt32(rVal) : -1;

        if (GameOverTitleLabel != null)
        {
            if (latestRank > 0 && latestRank <= 10)
            {
                GameOverTitleLabel.Text = $"🏆 NEW HIGH SCORE! RANK #{latestRank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "RUN FINISHED • TOP RUNNERS";
                GameOverTitleLabel.Modulate = new Color(0.95f, 0.45f, 0.2f);
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string prefix = (i + 1) switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"#{i + 1:D2}"
            };

            string isCurrentMarker = (i + 1 == latestRank) ? " ⭐" : "";
            sb.AppendLine($"{prefix} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance,4:N0}m  #{e.Seed}){isCurrentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
