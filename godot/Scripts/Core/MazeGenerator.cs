using System;
using System.Collections.Generic;
using Godot;

namespace WobbleHero.Core;

[Flags]
public enum WallFlags : byte
{
    None  = 0,
    North = 1 << 0, // -Z
    East  = 1 << 1, // +X
    South = 1 << 2, // +Z
    West  = 1 << 3  // -X
}

public struct MazeCell
{
    public WallFlags Walls;
    public bool Visited;
}

public struct HazardBarData
{
    public Vector2I Cell;
    public bool IsHorizontal;
    public float Speed;
    public float Range;
}

public class MazeData
{
    public int Cols { get; set; }
    public int Rows { get; set; }
    public int Seed { get; set; }
    public string Size { get; set; } = "medium";
    public string Difficulty { get; set; } = "medium";

    public MazeCell[,] Cells { get; set; } = null!;
    public List<Vector2I> MainPath { get; set; } = new();

    public Vector2I StartCell { get; set; }
    public Vector2I ExitCell { get; set; }
    public Vector2I KeyCell { get; set; }
    public Vector2I GateCellA { get; set; }
    public Vector2I GateCellB { get; set; }
    public Vector2I CheckpointCell { get; set; }

    public List<Vector2I> GemCells { get; set; } = new();
    public List<Vector2I> TrapCells { get; set; } = new();
    public List<HazardBarData> HazardBars { get; set; } = new();
}

/// <summary>
/// Deterministic procedural maze generator using Mulberry32 PRNG and Recursive Backtracker (DFS).
/// Guarantees a 100% solvable path from Start to Exit with configurable gates, keys, and traps.
/// </summary>
public static class MazeGenerator
{
    public struct Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed == 0 ? 1337u : seed;

        public float NextFloat()
        {
            _state += 0x6D2B79F5;
            uint t = _state;
            t = (t ^ (t >> 15)) * (_state | 1);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return ((t ^ (t >> 14)) >> 0) / 4294967296.0f;
        }

        public int NextInt(int min, int max)
        {
            if (min >= max) return min;
            return (int)(min + NextFloat() * (max - min));
        }
    }

    public static MazeData Generate(int seed, string size, string difficulty)
    {
        int cols, rows;
        switch (size.ToLowerInvariant())
        {
            case "small":
                cols = 9;
                rows = 9;
                break;
            case "large":
                cols = 17;
                rows = 17;
                break;
            case "medium":
            default:
                cols = 13;
                rows = 13;
                break;
        }

        var rng = new Mulberry32((uint)seed);
        var cells = new MazeCell[cols, rows];

        // Initialize all walls intact
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                cells[x, y].Walls = WallFlags.North | WallFlags.East | WallFlags.South | WallFlags.West;
                cells[x, y].Visited = false;
            }
        }

        // 1. Recursive Backtracker DFS spanning tree
        var stack = new Stack<Vector2I>();
        var start = new Vector2I(0, 0);
        cells[start.X, start.Y].Visited = true;
        stack.Push(start);

        var neighbors = new List<(Vector2I pos, WallFlags currentWall, WallFlags neighborWall)>(4);

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            neighbors.Clear();

            // North (y - 1)
            if (cur.Y > 0 && !cells[cur.X, cur.Y - 1].Visited)
                neighbors.Add((new Vector2I(cur.X, cur.Y - 1), WallFlags.North, WallFlags.South));
            // East (x + 1)
            if (cur.X < cols - 1 && !cells[cur.X + 1, cur.Y].Visited)
                neighbors.Add((new Vector2I(cur.X + 1, cur.Y), WallFlags.East, WallFlags.West));
            // South (y + 1)
            if (cur.Y < rows - 1 && !cells[cur.X, cur.Y + 1].Visited)
                neighbors.Add((new Vector2I(cur.X, cur.Y + 1), WallFlags.South, WallFlags.North));
            // West (x - 1)
            if (cur.X > 0 && !cells[cur.X - 1, cur.Y].Visited)
                neighbors.Add((new Vector2I(cur.X - 1, cur.Y), WallFlags.West, WallFlags.East));

            if (neighbors.Count > 0)
            {
                int chosenIdx = rng.NextInt(0, neighbors.Count);
                var (next, curWall, nextWall) = neighbors[chosenIdx];

                cells[cur.X, cur.Y].Walls &= ~curWall;
                cells[next.X, next.Y].Walls &= ~nextWall;
                cells[next.X, next.Y].Visited = true;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }

        var exit = new Vector2I(cols - 1, rows - 1);

        // 2. Solve guaranteed main path using BFS
        var mainPath = FindShortestPath(cells, cols, rows, start, exit);

        // 3. Place Locked Gate along main path (~60% along path)
        int gateIndex = Math.Clamp((int)(mainPath.Count * 0.60f), 2, mainPath.Count - 2);
        var gateA = mainPath[gateIndex];
        var gateB = mainPath[gateIndex + 1];

        // 4. Place Key in pre-gate side corridor
        // Find all cells reachable from start WITHOUT passing gateA -> gateB edge
        var preGateReachable = GetReachableCells(cells, cols, rows, start, gateA, gateB);
        var mainPathSet = new HashSet<Vector2I>(mainPath);

        // Dead-ends in pre-gate region not on main path
        var preGateDeadEnds = new List<Vector2I>();
        foreach (var c in preGateReachable)
        {
            if (!mainPathSet.Contains(c) && CountWalls(cells[c.X, c.Y].Walls) == 3)
            {
                preGateDeadEnds.Add(c);
            }
        }

        Vector2I keyCell;
        if (preGateDeadEnds.Count > 0)
        {
            keyCell = preGateDeadEnds[rng.NextInt(0, preGateDeadEnds.Count)];
        }
        else
        {
            // Fallback: any pre-gate cell not on main path
            var nonMain = new List<Vector2I>();
            foreach (var c in preGateReachable)
            {
                if (!mainPathSet.Contains(c)) nonMain.Add(c);
            }
            keyCell = nonMain.Count > 0 ? nonMain[rng.NextInt(0, nonMain.Count)] : mainPath[Math.Max(0, gateIndex - 2)];
        }

        // 5. Place Checkpoint near ~45% of main path (before gate)
        int checkpointIdx = Math.Clamp((int)(mainPath.Count * 0.45f), 1, gateIndex - 1);
        var checkpointCell = mainPath[checkpointIdx];

        // 6. Bonus Gems placement
        int targetGems = difficulty.ToLowerInvariant() switch
        {
            "easy" => 2,
            "hard" => 4,
            _ => 3
        };

        var gemCandidates = new List<Vector2I>();
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var pt = new Vector2I(x, y);
                if (pt == start || pt == exit || pt == keyCell || pt == checkpointCell || pt == gateA || pt == gateB)
                    continue;

                if (CountWalls(cells[x, y].Walls) == 3)
                {
                    gemCandidates.Add(pt);
                }
            }
        }

        Shuffle(gemCandidates, ref rng);
        var gemCells = new List<Vector2I>();
        for (int i = 0; i < Math.Min(targetGems, gemCandidates.Count); i++)
        {
            gemCells.Add(gemCandidates[i]);
        }

        // 7. Trap Holes placement
        int targetTraps = difficulty.ToLowerInvariant() switch
        {
            "easy" => 3,
            "hard" => 10,
            _ => 6
        };

        var trapCells = new List<Vector2I>();
        var reserved = new HashSet<Vector2I>(gemCells)
        {
            start, exit, keyCell, checkpointCell, gateA, gateB
        };

        if (difficulty.ToLowerInvariant() == "easy")
        {
            // Easy: traps strictly in deep dead-ends far from main path
            var deadEnds = new List<Vector2I>();
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    var pt = new Vector2I(x, y);
                    if (!reserved.Contains(pt) && !mainPathSet.Contains(pt) && CountWalls(cells[x, y].Walls) == 3)
                    {
                        deadEnds.Add(pt);
                    }
                }
            }
            Shuffle(deadEnds, ref rng);
            for (int i = 0; i < Math.Min(targetTraps, deadEnds.Count); i++)
            {
                trapCells.Add(deadEnds[i]);
            }
        }
        else
        {
            // Medium / Hard: dead-ends and junctions
            var trapCandidates = new List<Vector2I>();
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    var pt = new Vector2I(x, y);
                    if (reserved.Contains(pt)) continue;

                    // Exclude start area
                    if (Math.Abs(pt.X - start.X) + Math.Abs(pt.Y - start.Y) <= 1) continue;

                    if (difficulty.ToLowerInvariant() == "medium")
                    {
                        // Mostly off main path
                        if (!mainPathSet.Contains(pt) || pt == mainPath[gateIndex / 2])
                            trapCandidates.Add(pt);
                    }
                    else
                    {
                        // Hard: near turns and junctions
                        trapCandidates.Add(pt);
                    }
                }
            }

            Shuffle(trapCandidates, ref rng);
            for (int i = 0; i < Math.Min(targetTraps, trapCandidates.Count); i++)
            {
                trapCells.Add(trapCandidates[i]);
            }
        }

        // 8. Moving hazard bars for Hard difficulty
        var hazardBars = new List<HazardBarData>();
        if (difficulty.ToLowerInvariant() == "hard")
        {
            // Pick 2 corridor segments on main path past the gate
            if (gateIndex + 3 < mainPath.Count)
            {
                hazardBars.Add(new HazardBarData
                {
                    Cell = mainPath[gateIndex + 2],
                    IsHorizontal = (mainPath[gateIndex + 2].Y == mainPath[gateIndex + 1].Y),
                    Speed = 1.8f,
                    Range = 0.7f
                });
            }
            if (gateIndex + 5 < mainPath.Count)
            {
                hazardBars.Add(new HazardBarData
                {
                    Cell = mainPath[gateIndex + 4],
                    IsHorizontal = (mainPath[gateIndex + 4].X == mainPath[gateIndex + 3].X),
                    Speed = 2.2f,
                    Range = 0.7f
                });
            }
        }

        return new MazeData
        {
            Cols = cols,
            Rows = rows,
            Seed = seed,
            Size = size,
            Difficulty = difficulty,
            Cells = cells,
            MainPath = mainPath,
            StartCell = start,
            ExitCell = exit,
            KeyCell = keyCell,
            GateCellA = gateA,
            GateCellB = gateB,
            CheckpointCell = checkpointCell,
            GemCells = gemCells,
            TrapCells = trapCells,
            HazardBars = hazardBars
        };
    }

    private static List<Vector2I> FindShortestPath(MazeCell[,] cells, int cols, int rows, Vector2I start, Vector2I exit)
    {
        var queue = new Queue<Vector2I>();
        var visited = new bool[cols, rows];
        var parent = new Vector2I?[cols, rows];

        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == exit) break;

            var w = cells[cur.X, cur.Y].Walls;

            // North
            if ((w & WallFlags.North) == 0 && cur.Y > 0 && !visited[cur.X, cur.Y - 1])
            {
                visited[cur.X, cur.Y - 1] = true;
                parent[cur.X, cur.Y - 1] = cur;
                queue.Enqueue(new Vector2I(cur.X, cur.Y - 1));
            }
            // East
            if ((w & WallFlags.East) == 0 && cur.X < cols - 1 && !visited[cur.X + 1, cur.Y])
            {
                visited[cur.X + 1, cur.Y] = true;
                parent[cur.X + 1, cur.Y] = cur;
                queue.Enqueue(new Vector2I(cur.X + 1, cur.Y));
            }
            // South
            if ((w & WallFlags.South) == 0 && cur.Y < rows - 1 && !visited[cur.X, cur.Y + 1])
            {
                visited[cur.X, cur.Y + 1] = true;
                parent[cur.X, cur.Y + 1] = cur;
                queue.Enqueue(new Vector2I(cur.X, cur.Y + 1));
            }
            // West
            if ((w & WallFlags.West) == 0 && cur.X > 0 && !visited[cur.X - 1, cur.Y])
            {
                visited[cur.X - 1, cur.Y] = true;
                parent[cur.X - 1, cur.Y] = cur;
                queue.Enqueue(new Vector2I(cur.X - 1, cur.Y));
            }
        }

        var path = new List<Vector2I>();
        Vector2I? curr = exit;
        while (curr.HasValue)
        {
            path.Add(curr.Value);
            curr = parent[curr.Value.X, curr.Value.Y];
        }
        path.Reverse();
        return path;
    }

    private static List<Vector2I> GetReachableCells(MazeCell[,] cells, int cols, int rows, Vector2I start, Vector2I blockedA, Vector2I blockedB)
    {
        var result = new List<Vector2I>();
        var queue = new Queue<Vector2I>();
        var visited = new bool[cols, rows];

        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            result.Add(cur);
            var w = cells[cur.X, cur.Y].Walls;

            void TryNeighbor(Vector2I next, WallFlags flag)
            {
                if ((w & flag) == 0 && !visited[next.X, next.Y])
                {
                    // Check if edge is the blocked gate
                    if ((cur == blockedA && next == blockedB) || (cur == blockedB && next == blockedA))
                        return;

                    visited[next.X, next.Y] = true;
                    queue.Enqueue(next);
                }
            }

            if (cur.Y > 0) TryNeighbor(new Vector2I(cur.X, cur.Y - 1), WallFlags.North);
            if (cur.X < cols - 1) TryNeighbor(new Vector2I(cur.X + 1, cur.Y), WallFlags.East);
            if (cur.Y < rows - 1) TryNeighbor(new Vector2I(cur.X, cur.Y + 1), WallFlags.South);
            if (cur.X > 0) TryNeighbor(new Vector2I(cur.X - 1, cur.Y), WallFlags.West);
        }

        return result;
    }

    private static int CountWalls(WallFlags w)
    {
        int count = 0;
        if ((w & WallFlags.North) != 0) count++;
        if ((w & WallFlags.East) != 0) count++;
        if ((w & WallFlags.South) != 0) count++;
        if ((w & WallFlags.West) != 0) count++;
        return count;
    }

    private static void Shuffle<T>(List<T> list, ref Mulberry32 rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
