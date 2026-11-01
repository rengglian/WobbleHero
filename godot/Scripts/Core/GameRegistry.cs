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
            "WobbleMaze",
            "Wobble Maze",
            "Guide a rolling marble through tilting labyrinths, collect gems, unlock gates, and avoid hazard pits!",
            "🌀",
            "res://Scenes/Games/WobbleMaze.tscn"
        ),
        new(
            "AsteroidSurfer",
            "Asteroid Surfer",
            "Carve cosmic gravity waves, launch off the lip into zero-G airtime, and dodge black holes!",
            "🏄",
            "res://Scenes/Games/AsteroidSurfer.tscn"
        ),
        new(
            "WobbleSumo",
            "Wobble Sumo",
            "Physics brawler in a floating dojo! Knock AI sumos out of the shrinking ring with board tilts & shockwave body slams!",
            "🦹",
            "res://Scenes/Games/WobbleSumo.tscn"
        ),
        new(
            "LunarLander",
            "Lunar Lander",
            "Precision vector-thrust excursion module navigation! Softly touch down on subterranean crater pads!",
            "🛸",
            "res://Scenes/Games/LunarLander.tscn"
        ),
        new(
            "CompassLabyrinth",
            "Compass Labyrinth",
            "Rotate your physical board 360° to shift temple gravity! Slide heavy monoliths, activate pressure plates, and retrieve ancient relics.",
            "🧭",
            "res://Scenes/Games/CompassLabyrinth.tscn"
        ),
        new(
            "GravityPinball",
            "Gravity Pinball",
            "Your board is the table! Tilt gravity across multiple active pinballs, pop bumpers, drop targets, and avoid the drain.",
            "🧱",
            "res://Scenes/Games/GravityPinball.tscn"
        ),
        new(
            "DownhillSlalom",
            "Downhill Slalom",
            "High-speed alpine skiing! Carve tight slalom gates, tuck forward for 120 km/h speed runs, and hit jump kickers.",
            "🏔️",
            "res://Scenes/Games/DownhillSlalom.tscn"
        ),
        new(
            "RiverLogRoll",
            "River Log Roll",
            "Balance atop a spinning timber log in white-water rapids! Counter river torque to stay in the sweet spot, dodge boulders, and catch salmon.",
            "🪵",
            "res://Scenes/Games/RiverLogRoll.tscn"
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
