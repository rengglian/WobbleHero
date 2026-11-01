using System;
using System.Collections.Generic;
using Godot;

namespace WobbleHero.Core;

public record GameEntry(
    string Id,
    string Title,
    string Description,
    string Icon,
    string ScenePath
);

/// <summary>
/// Registry containing available mini-games. Supports runtime dynamic switching.
/// </summary>
public static class GameRegistry
{
    private static readonly List<GameEntry> Entries = new()
    {
        new(
            "BalanceTrainer",
            "Balance Trainer",
            "Tilt to keep the marble in the scoring zones without falling off",
            "⚖️",
            "res://Scenes/Games/BalanceTrainer.tscn"
        ),
        new(
            "WobbleRunner",
            "Wobble Runner",
            "Slalom left and right through approaching gates to build multipliers",
            "🏃",
            "res://Scenes/Games/WobbleRunner.tscn"
        ),
        new(
            "SensorLab",
            "Sensor Lab & Diagnostics",
            "Real-time 3D balance board visualizer and IMU calibration bench",
            "🔬",
            "res://Scenes/Games/SensorLab.tscn"
        )
    };

    public static IReadOnlyList<GameEntry> GetAvailableGames() => Entries;

    public static GameEntry? FindGame(string id)
    {
        return Entries.Find(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
