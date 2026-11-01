using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for Lunar Lander (Planetary Hopper).
/// Visualizes Altitude, Vertical Descent Speed (with safe/danger colors),
/// Horizontal Drift, Fuel Reserve Bar, Planet Gravity, and Touchdown Leaderboard.
/// </summary>
public partial class LunarLanderHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? AltitudeLabel { get; set; }
    [Export] public Label? VerticalSpeedLabel { get; set; }
    [Export] public Label? HorizontalSpeedLabel { get; set; }
    [Export] public Label? FuelLabel { get; set; }
    [Export] public ProgressBar? FuelBar { get; set; }
    [Export] public Label? PlanetLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }
    [Export] public Label? StatusLabel { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        AltitudeLabel ??= GetNodeOrNull<Label>("%AltitudeLabel");
        VerticalSpeedLabel ??= GetNodeOrNull<Label>("%VerticalSpeedLabel");
        HorizontalSpeedLabel ??= GetNodeOrNull<Label>("%HorizontalSpeedLabel");
        FuelLabel ??= GetNodeOrNull<Label>("%FuelLabel");
        FuelBar ??= GetNodeOrNull<ProgressBar>("%FuelBar");
        PlanetLabel ??= GetNodeOrNull<Label>("%PlanetLabel");
        StateLabel ??= GetNodeOrNull<Label>("%StateLabel");
        StatusLabel ??= GetNodeOrNull<Label>("%StatusLabel");

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
            if (AltitudeLabel != null && stats.TryGetValue("altitude", out var altVal))
            {
                float alt = Convert.ToSingle(altVal);
                AltitudeLabel.Text = $"ALT: {alt:F1}m";
            }

            if (VerticalSpeedLabel != null && stats.TryGetValue("verticalSpeed", out var vyVal))
            {
                float vy = Convert.ToSingle(vyVal);
                float speed = MathF.Abs(vy);
                string sign = vy >= 0 ? "▲" : "▼";
                VerticalSpeedLabel.Text = $"{sign} V/S: {speed:F1} m/s";

                // Safe touchdown limit: <= 1.8 m/s (Green), <= 3.2 m/s (Amber), > 3.2 m/s (Red)
                if (speed <= 1.8f)
                {
                    VerticalSpeedLabel.Modulate = new Color(0.2f, 0.95f, 0.45f); // Safe Green
                }
                else if (speed <= 3.2f)
                {
                    VerticalSpeedLabel.Modulate = new Color(1.0f, 0.85f, 0.2f); // Warning Amber
                }
                else
                {
                    VerticalSpeedLabel.Modulate = new Color(0.95f, 0.25f, 0.25f); // Danger Red
                }
            }

            if (HorizontalSpeedLabel != null && stats.TryGetValue("horizontalSpeed", out var vxVal))
            {
                float vx = Convert.ToSingle(vxVal);
                HorizontalSpeedLabel.Text = $"H/S: {vx:F1} m/s";
                HorizontalSpeedLabel.Modulate = vx <= 1.5f ? new Color(0.22f, 0.74f, 0.97f) : new Color(0.95f, 0.35f, 0.2f);
            }

            if (FuelLabel != null && stats.TryGetValue("fuel", out var fuelVal))
            {
                float fuel = Convert.ToSingle(fuelVal);
                FuelLabel.Text = $"FUEL: {fuel:F0}%";
                if (FuelBar != null) FuelBar.Value = fuel;
            }

            if (PlanetLabel != null && stats.TryGetValue("planet", out var pVal) && stats.TryGetValue("gravity", out var gVal))
            {
                string planet = Convert.ToString(pVal)?.ToUpperInvariant() ?? "MOON";
                float gravity = Convert.ToSingle(gVal);
                PlanetLabel.Text = $"{planet} (g={gravity:F2})";
            }

            if (StatusLabel != null && stats.TryGetValue("touchdownStatus", out var statusVal))
            {
                string status = Convert.ToString(statusVal) ?? "FLYING";
                StatusLabel.Text = status;
                StatusLabel.Modulate = status switch
                {
                    "SOFT TOUCHDOWN" => new Color(0.2f, 0.95f, 0.45f),
                    "HARD TOUCHDOWN" => new Color(1.0f, 0.85f, 0.2f),
                    "CRASHED" => new Color(0.95f, 0.25f, 0.25f),
                    _ => new Color(0.7f, 0.85f, 1.0f)
                };
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
                GameOverTitleLabel.Text = $"🏆 EXTRACTION SUCCESS! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "MISSION TERMINATED • TOP PILOTS";
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance} Landings){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
