using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Asteroid Surfer.
/// Displays speed (km/h), distance, multiplier, air-time duration,
/// plasma rings collected, clean landings, non-blocking top banner,
/// and leaderboard high-score overlay on run completion.
/// </summary>
public partial class AsteroidSurferHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? DistanceLabel { get; set; }
    [Export] public Label? SpeedLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }
    [Export] public Label? MultiplierLabel { get; set; }
    [Export] public Label? AirTimeLabel { get; set; }
    [Export] public Label? PlasmaLabel { get; set; }
    [Export] public Label? LandingsLabel { get; set; }

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
        MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel");
        AirTimeLabel ??= GetNodeOrNull<Label>("%AirTimeLabel");
        PlasmaLabel ??= GetNodeOrNull<Label>("%PlasmaLabel");
        LandingsLabel ??= GetNodeOrNull<Label>("%LandingsLabel");

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
                DistanceLabel.Text = $"DIST: {Convert.ToInt32(distVal):N0}m";
            }

            if (SpeedLabel != null && stats.TryGetValue("speed", out var speedVal))
            {
                SpeedLabel.Text = $"{Convert.ToInt32(speedVal)} KM/H";
            }

            if (MultiplierLabel != null && stats.TryGetValue("multiplier", out var multVal))
            {
                float mult = Convert.ToSingle(multVal);
                MultiplierLabel.Text = $"x{mult:F1}";
                MultiplierLabel.Modulate = mult >= 3.0f
                    ? new Color(1.0f, 0.2f, 0.8f) // Electric Magenta
                    : (mult > 1.5f ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.22f, 0.74f, 0.97f));
            }

            if (AirTimeLabel != null && stats.TryGetValue("isAirborne", out var airVal))
            {
                bool airborne = Convert.ToBoolean(airVal);
                if (airborne && stats.TryGetValue("airTime", out var timeVal))
                {
                    float time = Convert.ToSingle(timeVal);
                    AirTimeLabel.Visible = true;
                    AirTimeLabel.Text = $"🚀 AIR-TIME: {time:F1}s";
                }
                else
                {
                    AirTimeLabel.Visible = false;
                }
            }

            if (PlasmaLabel != null && stats.TryGetValue("plasmaRings", out var ringVal))
            {
                PlasmaLabel.Text = $"⚡ {Convert.ToInt32(ringVal)}";
            }

            if (LandingsLabel != null && stats.TryGetValue("cleanLandings", out var landVal))
            {
                LandingsLabel.Text = $"✨ {Convert.ToInt32(landVal)}";
            }
        }

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
                GameOverTitleLabel.Text = "SURF FINISHED • TOP RUNNERS";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.25f, 0.75f);
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance,4:N0}m){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
