using System;
using System.Collections.Generic;
using Godot;
using WobbleHero.Core;

namespace WobbleHero.Games;

/// <summary>
/// Common base class for WobbleHero balancing board mini-games.
/// Provides default state handling, score dispatch, and telemetry hooks.
/// </summary>
public abstract partial class BaseGameMode : Node3D, IGameMode
{
    public abstract string GameId { get; }
    public abstract string GameTitle { get; }
    public abstract string GameDescription { get; }
    public abstract string GameIcon { get; }

    public string GameState { get; protected set; } = "START";
    public int CurrentScore { get; protected set; }

    public event Action<int>? OnScoreChanged;
    public event Action<string>? OnGameOver;

    [Export] public PackedScene? HudScene { get; set; }
    public WobbleHero.UI.BaseGameHud? Hud { get; protected set; }

    protected GameManager? Manager;

    public virtual void Initialize(GameManager manager)
    {
        Manager = manager;

        if (Hud == null && HudScene != null)
        {
            var hudInstance = HudScene.Instantiate<WobbleHero.UI.BaseGameHud>();
            AddChild(hudInstance);
            Hud = hudInstance;
        }

        Hud?.Initialize(manager);
        ResetGame();
    }

    public virtual void StartGame()
    {
        GameState = "RUNNING";
    }

    public virtual void PauseGame()
    {
        if (GameState == "RUNNING")
        {
            GameState = "PAUSED";
        }
    }

    public virtual void ResumeGame()
    {
        if (GameState == "PAUSED")
        {
            GameState = "RUNNING";
        }
    }

    public virtual void ResetGame()
    {
        CurrentScore = 0;
        GameState = "START";
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public abstract void UpdateBoardInput(BoardState boardState, double delta);

    public virtual void UpdateHud(BoardState boardState)
    {
        Hud?.UpdateState(boardState, this);
    }

    public virtual Dictionary<string, object> GetLiveStats()
    {
        return new Dictionary<string, object>
        {
            { "score", CurrentScore },
            { "state", GameState }
        };
    }

    protected void SetScore(int score)
    {
        if (CurrentScore != score)
        {
            CurrentScore = score;
            OnScoreChanged?.Invoke(CurrentScore);
        }
    }

    protected void AddScore(int delta)
    {
        SetScore(CurrentScore + delta);
    }

    protected void TriggerGameOver(string reason)
    {
        GameState = "GAMEOVER";
        OnGameOver?.Invoke(reason);
    }
}
