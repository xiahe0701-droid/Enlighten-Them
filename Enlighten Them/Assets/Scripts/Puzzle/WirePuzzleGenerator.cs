using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WirePuzzleGenerationSettings
{
    [Min(2)] public int gridSize = 4;
    [Min(1)] public int pairCount = 1;
    [Min(0)] public int brokenWireCount = 2;
    [Min(0)] public int extraBranchCount = 1;
    [Min(2)] public int minimumPathLength = 6;
    [Min(1)] public int minimumScrambledTiles = 5;
    [Min(1)] public int generationAttempts = 30;
    [Min(0f)] public float cellSpacing = 10f;
}

public sealed class GeneratedWireTile
{
    public WireTileType Type;
    public int SolvedQuarterTurns;
    public bool Locked;
    public int ChannelId = -1;
}

public sealed class GeneratedWireChannel
{
    public int ChannelId;
    public int SourceIndex;
    public int TargetIndex;
    public readonly List<int> SolutionPath = new List<int>();
}

public sealed class GeneratedWirePuzzle
{
    public int GridSize;
    public int Seed;
    public GeneratedWireTile[] Tiles;
    public readonly List<GeneratedWireChannel> Channels =
        new List<GeneratedWireChannel>();
}

/// <summary>
/// Pure data generator. It is not a MonoBehaviour and is not added to a
/// GameObject. Every generated result contains a known valid solution.
/// </summary>
public sealed class WirePuzzleGenerator
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private readonly HashSet<int> occupied = new HashSet<int>();
    private readonly Dictionary<int, WireDirection> solvedConnections =
        new Dictionary<int, WireDirection>();

    private int gridSize;
    private System.Random random;

    public GeneratedWirePuzzle Generate(
        WirePuzzleGenerationSettings settings,
        int seed)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        gridSize = Mathf.Max(2, settings.gridSize);
        int pairCount = Mathf.Clamp(
            settings.pairCount,
            1,
            gridSize
        );

        int attempts = Mathf.Max(1, settings.generationAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            random = new System.Random(seed + attempt * 7919);
            GeneratedWirePuzzle puzzle = CreateEmptyPuzzle(seed + attempt * 7919);

            occupied.Clear();
            solvedConnections.Clear();

            if (!CreateAllChannels(
                    puzzle,
                    pairCount,
                    settings.minimumPathLength))
            {
                continue;
            }

            AddBranches(puzzle, settings.extraBranchCount);
            BuildPathTiles(puzzle);
            AddBrokenTiles(puzzle, settings.brokenWireCount);
            FillUnusedTiles(puzzle);

            if (ValidateKnownSolution(puzzle))
                return puzzle;
        }

        // A straight path in each row band is the final safety net. It makes
        // generation failure visible as a simpler level, never as a dead one.
        random = new System.Random(seed);
        GeneratedWirePuzzle fallback = CreateEmptyPuzzle(seed);
        occupied.Clear();
        solvedConnections.Clear();
        CreateFallbackChannels(fallback, pairCount);
        BuildPathTiles(fallback);
        AddBrokenTiles(fallback, settings.brokenWireCount);
        FillUnusedTiles(fallback);

        if (!ValidateKnownSolution(fallback))
        {
            throw new InvalidOperationException(
                "The guaranteed wire-puzzle fallback was invalid."
            );
        }

        return fallback;
    }

    public bool ValidateKnownSolution(GeneratedWirePuzzle puzzle)
    {
        if (puzzle == null || puzzle.Tiles == null)
            return false;

        for (int channelIndex = 0;
             channelIndex < puzzle.Channels.Count;
             channelIndex++)
        {
            GeneratedWireChannel channel = puzzle.Channels[channelIndex];

            if (channel.SolutionPath.Count < 2 ||
                channel.SolutionPath[0] != channel.SourceIndex ||
                channel.SolutionPath[channel.SolutionPath.Count - 1] !=
                    channel.TargetIndex)
            {
                return false;
            }

            for (int i = 0; i < channel.SolutionPath.Count - 1; i++)
            {
                int current = channel.SolutionPath[i];
                int next = channel.SolutionPath[i + 1];
                WireDirection direction = DirectionFromTo(current, next);
                WireDirection opposite = Opposite(direction);

                GeneratedWireTile currentTile = puzzle.Tiles[current];
                GeneratedWireTile nextTile = puzzle.Tiles[next];

                WireDirection currentConnections =
                    WireTileView.GetConnectionsFor(
                        currentTile.Type,
                        currentTile.SolvedQuarterTurns
                    );

                WireDirection nextConnections =
                    WireTileView.GetConnectionsFor(
                        nextTile.Type,
                        nextTile.SolvedQuarterTurns
                    );

                if ((currentConnections & direction) == 0 ||
                    (nextConnections & opposite) == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private GeneratedWirePuzzle CreateEmptyPuzzle(int seed)
    {
        GeneratedWirePuzzle puzzle = new GeneratedWirePuzzle
        {
            GridSize = gridSize,
            Seed = seed,
            Tiles = new GeneratedWireTile[gridSize * gridSize]
        };

        for (int i = 0; i < puzzle.Tiles.Length; i++)
            puzzle.Tiles[i] = new GeneratedWireTile();

        return puzzle;
    }

    private bool CreateAllChannels(
        GeneratedWirePuzzle puzzle,
        int pairCount,
        int requestedMinimumLength)
    {
        for (int channelId = 0; channelId < pairCount; channelId++)
        {
            int rowMin = channelId * gridSize / pairCount;
            int rowMaxExclusive =
                (channelId + 1) * gridSize / pairCount;

            int bandCellCount =
                (rowMaxExclusive - rowMin) * gridSize;

            int minimumLength = Mathf.Clamp(
                requestedMinimumLength,
                gridSize,
                bandCellCount
            );

            List<int> path = TryCreatePath(
                rowMin,
                rowMaxExclusive,
                minimumLength
            );

            if (path == null)
                return false;

            GeneratedWireChannel channel = new GeneratedWireChannel
            {
                ChannelId = channelId,
                SourceIndex = path[0],
                TargetIndex = path[path.Count - 1]
            };

            channel.SolutionPath.AddRange(path);
            puzzle.Channels.Add(channel);
            RegisterPath(path);
        }

        return true;
    }

    private List<int> TryCreatePath(
        int rowMin,
        int rowMaxExclusive,
        int minimumLength)
    {
        int bandHeight = rowMaxExclusive - rowMin;
        int localAttempts = Mathf.Max(12, bandHeight * gridSize * 3);

        for (int attempt = 0; attempt < localAttempts; attempt++)
        {
            int sourceRow = random.Next(rowMin, rowMaxExclusive);
            int targetRow = random.Next(rowMin, rowMaxExclusive);
            int source = ToIndex(sourceRow, 0);
            int target = ToIndex(targetRow, gridSize - 1);

            List<int> path = new List<int> { source };
            HashSet<int> visited = new HashSet<int> { source };
            int searchBudget = gridSize * gridSize * 300;

            if (SearchPath(
                    source,
                    target,
                    rowMin,
                    rowMaxExclusive,
                    minimumLength,
                    path,
                    visited,
                    ref searchBudget))
            {
                return path;
            }
        }

        return null;
    }

    private bool SearchPath(
        int current,
        int target,
        int rowMin,
        int rowMaxExclusive,
        int minimumLength,
        List<int> path,
        HashSet<int> visited,
        ref int searchBudget)
    {
        if (--searchBudget <= 0)
            return false;

        if (current == target)
            return path.Count >= minimumLength;

        List<int> neighbors = GetShuffledNeighbors(
            current,
            rowMin,
            rowMaxExclusive
        );

        for (int i = 0; i < neighbors.Count; i++)
        {
            int next = neighbors[i];

            if (occupied.Contains(next) || visited.Contains(next))
                continue;

            // Do not finish too early. The search may approach the target
            // again later after making a longer route through the band.
            if (next == target && path.Count + 1 < minimumLength)
                continue;

            visited.Add(next);
            path.Add(next);

            if (SearchPath(
                    next,
                    target,
                    rowMin,
                    rowMaxExclusive,
                    minimumLength,
                    path,
                    visited,
                    ref searchBudget))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(next);
        }

        return false;
    }

    private void RegisterPath(List<int> path)
    {
        for (int i = 0; i < path.Count; i++)
        {
            occupied.Add(path[i]);

            if (i > 0)
                Connect(path[i], path[i - 1]);

            if (i < path.Count - 1)
                Connect(path[i], path[i + 1]);
        }
    }

    private void AddBranches(
        GeneratedWirePuzzle puzzle,
        int requestedCount)
    {
        int remaining = Mathf.Max(0, requestedCount);
        int attempts = Mathf.Max(20, remaining * 20);

        while (remaining > 0 && attempts-- > 0)
        {
            GeneratedWireChannel channel =
                puzzle.Channels[random.Next(puzzle.Channels.Count)];

            if (channel.SolutionPath.Count <= 2)
                continue;

            int parent = channel.SolutionPath[
                random.Next(1, channel.SolutionPath.Count - 1)
            ];

            if (CountConnections(GetConnections(parent)) >= 3)
                continue;

            List<int> emptyNeighbors = GetShuffledNeighbors(parent, 0, gridSize);
            emptyNeighbors.RemoveAll(index => occupied.Contains(index));

            if (emptyNeighbors.Count == 0)
                continue;

            int branch = emptyNeighbors[0];
            occupied.Add(branch);
            Connect(parent, branch);

            // Give the branch tile a visible dangling end, so it maps to a
            // normal corner/straight piece rather than an unsupported cap.
            AddOneSafeDanglingDirection(branch, parent);

            puzzle.Tiles[branch].ChannelId = channel.ChannelId;
            remaining--;
        }
    }

    private void AddOneSafeDanglingDirection(int index, int parent)
    {
        List<int> neighbors = GetShuffledNeighbors(index, 0, gridSize);

        for (int i = 0; i < neighbors.Count; i++)
        {
            int neighbor = neighbors[i];

            if (neighbor == parent)
                continue;

            WireDirection direction = DirectionFromTo(index, neighbor);
            solvedConnections[index] = GetConnections(index) | direction;
            return;
        }
    }

    private void BuildPathTiles(GeneratedWirePuzzle puzzle)
    {
        for (int channelIndex = 0;
             channelIndex < puzzle.Channels.Count;
             channelIndex++)
        {
            GeneratedWireChannel channel = puzzle.Channels[channelIndex];

            for (int i = 0; i < channel.SolutionPath.Count; i++)
                puzzle.Tiles[channel.SolutionPath[i]].ChannelId =
                    channel.ChannelId;

            ConfigureEndpoint(
                puzzle.Tiles[channel.SourceIndex],
                WireTileType.Source,
                GetConnections(channel.SourceIndex),
                channel.ChannelId
            );

            ConfigureEndpoint(
                puzzle.Tiles[channel.TargetIndex],
                WireTileType.Target,
                GetConnections(channel.TargetIndex),
                channel.ChannelId
            );
        }

        foreach (KeyValuePair<int, WireDirection> pair in solvedConnections)
        {
            GeneratedWireTile tile = puzzle.Tiles[pair.Key];

            if (tile.Type == WireTileType.Source ||
                tile.Type == WireTileType.Target)
            {
                continue;
            }

            tile.Type = GetTileType(pair.Value);
            tile.SolvedQuarterTurns = FindQuarterTurns(tile.Type, pair.Value);
        }
    }

    private static void ConfigureEndpoint(
        GeneratedWireTile tile,
        WireTileType type,
        WireDirection connection,
        int channelId)
    {
        tile.Type = type;
        tile.Locked = true;
        tile.ChannelId = channelId;
        tile.SolvedQuarterTurns = FindQuarterTurns(type, connection);
    }

    private void AddBrokenTiles(
        GeneratedWirePuzzle puzzle,
        int requestedCount)
    {
        List<int> unused = GetUnusedIndices(puzzle.Tiles.Length);
        Shuffle(unused);

        int count = Mathf.Min(Mathf.Max(0, requestedCount), unused.Count);

        for (int i = 0; i < count; i++)
        {
            GeneratedWireTile tile = puzzle.Tiles[unused[i]];
            tile.Type = WireTileType.Broken;
            tile.SolvedQuarterTurns = random.Next(0, 4);
        }
    }

    private void FillUnusedTiles(GeneratedWirePuzzle puzzle)
    {
        WireTileType[] decoyTypes =
        {
            WireTileType.Straight,
            WireTileType.Corner,
            WireTileType.Tee
        };

        for (int i = 0; i < puzzle.Tiles.Length; i++)
        {
            GeneratedWireTile tile = puzzle.Tiles[i];

            if (tile.Type != WireTileType.Empty)
                continue;

            tile.Type = decoyTypes[random.Next(decoyTypes.Length)];
            tile.SolvedQuarterTurns = random.Next(0, 4);
        }
    }

    private void CreateFallbackChannels(
        GeneratedWirePuzzle puzzle,
        int pairCount)
    {
        for (int channelId = 0; channelId < pairCount; channelId++)
        {
            int rowMin = channelId * gridSize / pairCount;
            int rowMaxExclusive =
                (channelId + 1) * gridSize / pairCount;
            int row = (rowMin + rowMaxExclusive - 1) / 2;

            List<int> path = new List<int>();

            for (int column = 0; column < gridSize; column++)
                path.Add(ToIndex(row, column));

            GeneratedWireChannel channel = new GeneratedWireChannel
            {
                ChannelId = channelId,
                SourceIndex = path[0],
                TargetIndex = path[path.Count - 1]
            };

            channel.SolutionPath.AddRange(path);
            puzzle.Channels.Add(channel);
            RegisterPath(path);
        }
    }

    private List<int> GetUnusedIndices(int count)
    {
        List<int> result = new List<int>();

        for (int i = 0; i < count; i++)
        {
            if (!occupied.Contains(i))
                result.Add(i);
        }

        return result;
    }

    private List<int> GetShuffledNeighbors(
        int index,
        int rowMin,
        int rowMaxExclusive)
    {
        int row = index / gridSize;
        int column = index % gridSize;
        List<int> result = new List<int>(4);

        for (int i = 0; i < Directions.Length; i++)
        {
            int nextRow = row + Directions[i].y;
            int nextColumn = column + Directions[i].x;

            if (nextRow < rowMin || nextRow >= rowMaxExclusive ||
                nextColumn < 0 || nextColumn >= gridSize)
            {
                continue;
            }

            result.Add(ToIndex(nextRow, nextColumn));
        }

        Shuffle(result);
        return result;
    }

    private void Connect(int first, int second)
    {
        WireDirection forward = DirectionFromTo(first, second);
        WireDirection backward = Opposite(forward);

        solvedConnections[first] = GetConnections(first) | forward;
        solvedConnections[second] = GetConnections(second) | backward;
    }

    private WireDirection GetConnections(int index)
    {
        WireDirection result;
        return solvedConnections.TryGetValue(index, out result)
            ? result
            : WireDirection.None;
    }

    private WireDirection DirectionFromTo(int from, int to)
    {
        int difference = to - from;

        if (difference == gridSize)
            return WireDirection.Down;
        if (difference == -gridSize)
            return WireDirection.Up;
        if (difference == 1)
            return WireDirection.Right;
        if (difference == -1)
            return WireDirection.Left;

        throw new InvalidOperationException("Wire cells are not adjacent.");
    }

    private static WireDirection Opposite(WireDirection direction)
    {
        switch (direction)
        {
            case WireDirection.Up: return WireDirection.Down;
            case WireDirection.Right: return WireDirection.Left;
            case WireDirection.Down: return WireDirection.Up;
            case WireDirection.Left: return WireDirection.Right;
            default: return WireDirection.None;
        }
    }

    private static WireTileType GetTileType(WireDirection directions)
    {
        int count = CountConnections(directions);

        if (count >= 4)
            return WireTileType.Cross;
        if (count == 3)
            return WireTileType.Tee;
        if (count == 2)
        {
            bool vertical = directions ==
                (WireDirection.Up | WireDirection.Down);
            bool horizontal = directions ==
                (WireDirection.Left | WireDirection.Right);

            return vertical || horizontal
                ? WireTileType.Straight
                : WireTileType.Corner;
        }

        // Only endpoints should have one connection. This fallback keeps a
        // malformed branch visible and harmless during development.
        return WireTileType.Straight;
    }

    private static int FindQuarterTurns(
        WireTileType type,
        WireDirection desired)
    {
        for (int turns = 0; turns < 4; turns++)
        {
            if (WireTileView.GetConnectionsFor(type, turns) == desired)
                return turns;
        }

        return 0;
    }

    private static int CountConnections(WireDirection directions)
    {
        int count = 0;

        if ((directions & WireDirection.Up) != 0) count++;
        if ((directions & WireDirection.Right) != 0) count++;
        if ((directions & WireDirection.Down) != 0) count++;
        if ((directions & WireDirection.Left) != 0) count++;

        return count;
    }

    private int ToIndex(int row, int column)
    {
        return row * gridSize + column;
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temporary = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temporary;
        }
    }
}
