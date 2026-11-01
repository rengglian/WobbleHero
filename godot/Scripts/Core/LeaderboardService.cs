using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace WobbleHero.Core;

public record LeaderboardEntry
{
    public string Name { get; init; } = "Wobbler";
    public int Score { get; init; }
    public int Distance { get; init; }
    public int Seed { get; init; }
    public string Date { get; init; } = string.Empty;
}

/// <summary>
/// Persistent leaderboard manager tracking top runs across mini-games.
/// Saves to user://leaderboard.json with auto-sorting and companion synchronization.
/// </summary>
public class LeaderboardService
{
    public static LeaderboardService Instance { get; } = new();

    public string ActivePlayerName { get; set; } = "Rider 1";
    public event Action? OnLeaderboardChanged;

    private readonly List<LeaderboardEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 10;
    private const string SaveFileName = "user://leaderboard.json";

    public LeaderboardService()
    {
        Load();
    }

    public IReadOnlyList<LeaderboardEntry> GetTopEntries()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }

    public bool IsHighScore(int score)
    {
        lock (_lock)
        {
            if (_entries.Count < MaxEntries) return true;
            return score > _entries[^1].Score;
        }
    }

    /// <summary>
    /// Submits a run score. Returns the 1-based rank achieved (1..10), or -1 if not qualified.
    /// </summary>
    public int RecordScore(string playerName, int score, int distance, int seed)
    {
        string safeName = string.IsNullOrWhiteSpace(playerName) ? ActivePlayerName : playerName.Trim();
        if (safeName.Length > 16) safeName = safeName[..16];

        var entry = new LeaderboardEntry
        {
            Name = safeName,
            Score = score,
            Distance = distance,
            Seed = seed,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        int rank = -1;
        lock (_lock)
        {
            _entries.Add(entry);
            _entries.Sort((a, b) => b.Score.CompareTo(a.Score));

            int idx = _entries.IndexOf(entry);
            if (idx < MaxEntries)
            {
                rank = idx + 1;
            }

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }

            Save();
        }

        OnLeaderboardChanged?.Invoke();
        GD.Print($"[LeaderboardService] Recorded score for '{safeName}': {score} pts ({distance}m) -> Rank #{rank}");
        return rank;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            Save();
        }
        OnLeaderboardChanged?.Invoke();
    }

    private void Load()
    {
        lock (_lock)
        {
            _entries.Clear();
            if (Godot.FileAccess.FileExists(SaveFileName))
            {
                try
                {
                    using var file = Godot.FileAccess.Open(SaveFileName, Godot.FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        string json = file.GetAsText();
                        var loaded = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json);
                        if (loaded != null)
                        {
                            _entries.AddRange(loaded);
                            _entries.Sort((a, b) => b.Score.CompareTo(a.Score));
                            if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
                            GD.Print($"[LeaderboardService] Loaded {_entries.Count} leaderboard entries.");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LeaderboardService] Load error: {ex.Message}");
                }
            }

            // Defaults if file does not exist
            _entries.Add(new LeaderboardEntry { Name = "WobblePro", Score = 2500, Distance = 850, Seed = 1337, Date = "Default" });
            _entries.Add(new LeaderboardEntry { Name = "BalanceHero", Score = 1800, Distance = 620, Seed = 1337, Date = "Default" });
            _entries.Add(new LeaderboardEntry { Name = "Rider 1", Score = 1200, Distance = 450, Seed = 1337, Date = "Default" });
            Save();
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            using (var file = Godot.FileAccess.Open(SaveFileName, Godot.FileAccess.ModeFlags.Write))
            {
                file?.StoreString(json);
            }

            // Also write a copy to local project directory for easy inspection on desktop
            try
            {
                string localPath = Path.Combine(Directory.GetCurrentDirectory(), "recordings", "leaderboard.json");
                File.WriteAllText(localPath, json);
            }
            catch { }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LeaderboardService] Save error: {ex.Message}");
        }
    }
}
