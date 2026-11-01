using System;
using System.Collections.Generic;

namespace WobbleHero.Core;

/// <summary>
/// Universal interface implemented by all balancing board mini-games in WobbleHero.
/// Enables seamless runtime game switching via companion app or UI.
/// </summary>
public interface IGameMode
{
    /// <summary>Unique machine identifier (e.g. "BalanceTrainer", "WobbleRunner")</summary>
    string GameId { get; }

    /// <summary>Human-readable display title (e.g. "Balance Trainer")</summary>
    string GameTitle { get; }

    /// <summary>Brief summary of gameplay objective</summary>
    string GameDescription { get; }

    /// <summary>Emoji or icon identifier for UI</summary>
    string GameIcon { get; }

    /// <summary>Current lifecycle state ("START", "RUNNING", "PAUSED", "GAMEOVER")</summary>
    string GameState { get; }

    /// <summary>Current primary score</summary>
    int CurrentScore { get; }

    /// <summary>Called when game mode is loaded and mounted into GameContainer</summary>
    void Initialize(GameManager manager);

    /// <summary>Starts active gameplay loop</summary>
    void StartGame();

    /// <summary>Pauses gameplay physics and timers</summary>
    void PauseGame();

    /// <summary>Resumes from paused state</summary>
    void ResumeGame();

    /// <summary>Resets entities and scores to starting state</summary>
    void ResetGame();

    /// <summary>Per-frame physics update receiving balance board tilt</summary>
    void UpdateBoardInput(BoardState boardState, double delta);

    /// <summary>Returns arbitrary telemetry dictionary for companion remote streaming</summary>
    Dictionary<string, object> GetLiveStats();

    /// <summary>Fired when score changes</summary>
    event Action<int>? OnScoreChanged;

    /// <summary>Fired when game ends (e.g. fell off board, time up)</summary>
    event Action<string>? OnGameOver;
}
