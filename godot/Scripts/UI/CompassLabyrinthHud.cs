using System;
using System.Text;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.UI;

/// <summary>
/// Dedicated CanvasLayer HUD for Compass Labyrinth (The Rotating Dungeon).
/// Visualizes Heading degree readout, Cardinal orientation pill badge,
/// 2D mini compass rose needle, Sun Relic indicators, and persistent Leaderboard.
/// </summary>
public partial class CompassLabyrinthHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? HeadingLabel { get; set; }
    [Export] public Label? CardinalPillLabel { get; set; }
    [Export] public Label? RelicsLabel { get; set; }
    [Export] public Label? PlatesLabel { get; set; }
    [Export] public Label? LivesLabel { get; set; }
    [Export] public Control? CompassNeedleControl { get; set; }

    // Leaderboard Overlay
    [Export] public Control? LeaderboardOverlay { get; set; }
    [Export] public Label? LeaderboardListLabel { get; set; }
    [Export] public Label? GameOverTitleLabel { get; set; }

    public override void _Ready()
    {
        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        HeadingLabel ??= GetNodeOrNull<Label>("%HeadingLabel");
        CardinalPillLabel ??= GetNodeOrNull<Label>("%CardinalPillLabel");
        RelicsLabel ??= GetNodeOrNull<Label>("%RelicsLabel");
        PlatesLabel ??= GetNodeOrNull<Label>("%PlatesLabel");
        LivesLabel ??= GetNodeOrNull<Label>("%LivesLabel");
        CompassNeedleControl ??= GetNodeOrNull<Control>("%CompassNeedle");

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
            if (HeadingLabel != null && stats.TryGetValue("headingDeg", out var headVal))
            {
                int deg = Convert.ToInt32(headVal);
                HeadingLabel.Text = $"{deg:D3}°";

                // Rotate mini compass needle indicator
                if (CompassNeedleControl != null)
                {
                    CompassNeedleControl.RotationDegrees = -deg;
                }
            }

            if (CardinalPillLabel != null && stats.TryGetValue("cardinalPlane", out var planeVal))
            {
                string plane = Convert.ToString(planeVal) ?? "NORTH";
                CardinalPillLabel.Text = $"🧭 {plane}";
                CardinalPillLabel.Modulate = plane switch
                {
                    "NORTH" => new Color(0.2f, 0.75f, 1.0f),
                    "EAST" => new Color(1.0f, 0.85f, 0.2f),
                    "SOUTH" => new Color(0.95f, 0.45f, 0.15f),
                    _ => new Color(0.2f, 0.95f, 0.45f) // West
                };
            }

            if (RelicsLabel != null && stats.TryGetValue("relicsCollected", out var rVal))
            {
                int relics = Convert.ToInt32(rVal);
                RelicsLabel.Text = $"☀️ {relics}/3";
            }

            if (PlatesLabel != null && stats.TryGetValue("platesSolved", out var pVal))
            {
                int plates = Convert.ToInt32(pVal);
                PlatesLabel.Text = $"🗝️ {plates}/2";
            }

            if (LivesLabel != null && stats.TryGetValue("lives", out var lVal))
            {
                int lives = Math.Max(0, Convert.ToInt32(lVal));
                LivesLabel.Text = lives switch
                {
                    3 => "❤️❤️❤️",
                    2 => "❤️❤️🖤",
                    1 => "❤️🖤🖤",
                    _ => "🖤🖤🖤"
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
                GameOverTitleLabel.Text = $"🏆 EXPEDITION MASTER! RANK #{rank}";
                GameOverTitleLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
            }
            else
            {
                GameOverTitleLabel.Text = "EXPEDITION FINISHED • TOP EXPLORERS";
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
            sb.AppendLine($"{badge} {e.Name,-12} {e.Score,6:N0} PTS  ({e.Distance} Relics){currentMarker}");
        }

        if (LeaderboardListLabel != null)
        {
            LeaderboardListLabel.Text = sb.ToString();
        }
    }
}
