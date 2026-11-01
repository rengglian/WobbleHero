using System;
using Godot;
using WobbleHero.Core;
using WobbleHero.Games;

namespace WobbleHero.UI;

/// <summary>
/// Custom HUD for Mini-Game: Wobble Maze.
/// Displays Level Timer, Gems collected, Key indicator, Falls counter,
/// difficulty badge, in-game banners, and Game Over / Victory Overlay.
/// </summary>
public partial class WobbleMazeHud : BaseGameHud
{
    [Export] public Label? ScoreLabel { get; set; }
    [Export] public Label? TimerLabel { get; set; }
    [Export] public Label? GemsLabel { get; set; }
    [Export] public Label? KeyLabel { get; set; }
    [Export] public Label? FallsLabel { get; set; }
    [Export] public Label? StateLabel { get; set; }
    [Export] public Label? ConfigBadgeLabel { get; set; }

    // Game Over / Victory Overlay
    [Export] public Control? VictoryOverlay { get; set; }
    [Export] public Label? VictoryTitleLabel { get; set; }
    [Export] public Label? VictoryStatsLabel { get; set; }

    public override void Initialize(GameManager manager)
    {
        base.Initialize(manager);

        ScoreLabel ??= GetNodeOrNull<Label>("%ScoreLabel");
        TimerLabel ??= GetNodeOrNull<Label>("%TimerLabel");
        GemsLabel ??= GetNodeOrNull<Label>("%GemsLabel");
        KeyLabel ??= GetNodeOrNull<Label>("%KeyLabel");
        FallsLabel ??= GetNodeOrNull<Label>("%FallsLabel");
        StateLabel ??= GetNodeOrNull<Label>("%StateLabel");
        ConfigBadgeLabel ??= GetNodeOrNull<Label>("%ConfigBadgeLabel");

        VictoryOverlay ??= GetNodeOrNull<Control>("%VictoryOverlay");
        VictoryTitleLabel ??= GetNodeOrNull<Label>("%VictoryTitleLabel");
        VictoryStatsLabel ??= GetNodeOrNull<Label>("%VictoryStatsLabel");
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
            if (TimerLabel != null && stats.TryGetValue("timeElapsed", out var timeVal))
            {
                float time = Convert.ToSingle(timeVal);
                int mins = (int)(time / 60f);
                float secs = time % 60f;
                TimerLabel.Text = $"TIME: {mins:D2}:{secs:04.1f}";
            }

            if (GemsLabel != null)
            {
                int collected = stats.TryGetValue("gemsCollected", out var cVal) ? Convert.ToInt32(cVal) : 0;
                int total = stats.TryGetValue("totalGems", out var tVal) ? Convert.ToInt32(tVal) : 3;
                GemsLabel.Text = $"💎 {collected}/{total}";
            }

            if (KeyLabel != null && stats.TryGetValue("hasKey", out var keyVal))
            {
                bool hasKey = Convert.ToBoolean(keyVal);
                bool gateUnlocked = stats.TryGetValue("gateUnlocked", out var gVal) && Convert.ToBoolean(gVal);

                if (gateUnlocked)
                {
                    KeyLabel.Text = "🗝️ UNLOCKED";
                    KeyLabel.Modulate = new Color(0.2f, 0.95f, 0.4f);
                }
                else if (hasKey)
                {
                    KeyLabel.Text = "🗝️ OBTAINED";
                    KeyLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
                }
                else
                {
                    KeyLabel.Text = "🗝️ LOCKED";
                    KeyLabel.Modulate = new Color(0.6f, 0.65f, 0.75f);
                }
            }

            if (FallsLabel != null && stats.TryGetValue("falls", out var fVal))
            {
                int falls = Convert.ToInt32(fVal);
                FallsLabel.Text = $"FALLS: {falls}";
                FallsLabel.Modulate = falls > 0 ? new Color(1.0f, 0.35f, 0.35f) : new Color(0.7f, 0.85f, 1.0f);
            }

            if (ConfigBadgeLabel != null)
            {
                string size = stats.TryGetValue("size", out var sVal) ? sVal.ToString()!.ToUpperInvariant() : "MED";
                string diff = stats.TryGetValue("difficulty", out var dVal) ? dVal.ToString()!.ToUpperInvariant() : "EASY";
                int seed = stats.TryGetValue("seed", out var sdVal) ? Convert.ToInt32(sdVal) : 1337;
                ConfigBadgeLabel.Text = $"{size} • {diff} • #{seed}";
            }
        }

        // Victory Overlay
        if (VictoryOverlay != null)
        {
            bool isOver = game.GameState == "GAMEOVER";
            VictoryOverlay.Visible = isOver;

            if (isOver && VictoryStatsLabel != null && stats != null)
            {
                float time = stats.TryGetValue("timeElapsed", out var tVal) ? Convert.ToSingle(tVal) : 0f;
                int gems = stats.TryGetValue("gemsCollected", out var gVal) ? Convert.ToInt32(gVal) : 0;
                int falls = stats.TryGetValue("falls", out var fVal) ? Convert.ToInt32(fVal) : 0;

                VictoryStatsLabel.Text = $"TIME: {time:F1}s\nGEMS: {gems}\nFALLS: {falls}\nFINAL SCORE: {game.CurrentScore:N0}";
            }
        }
    }
}
