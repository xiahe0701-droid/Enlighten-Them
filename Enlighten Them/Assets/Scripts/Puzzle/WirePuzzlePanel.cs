using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WirePuzzlePanel : RepairPuzzleBase
{
    private static readonly WireDirection[] SearchDirections =
    {
        WireDirection.Up,
        WireDirection.Right,
        WireDirection.Down,
        WireDirection.Left
    };

    [Header("Difficulty Settings")]
    [SerializeField]
    private WirePuzzleGenerationSettings easySettings =
        new WirePuzzleGenerationSettings
        {
            gridSize = 4,
            pairCount = 1,
            brokenWireCount = 2,
            extraBranchCount = 1,
            minimumPathLength = 6,
            minimumScrambledTiles = 5,
            generationAttempts = 30,
            cellSpacing = 10f
        };

    [SerializeField]
    private WirePuzzleGenerationSettings normalSettings =
        new WirePuzzleGenerationSettings
        {
            gridSize = 5,
            pairCount = 1,
            brokenWireCount = 4,
            extraBranchCount = 3,
            minimumPathLength = 9,
            minimumScrambledTiles = 10,
            generationAttempts = 50,
            cellSpacing = 10f
        };

    [SerializeField]
    private WirePuzzleGenerationSettings hardSettings =
        new WirePuzzleGenerationSettings
        {
            gridSize = 6,
            pairCount = 2,
            brokenWireCount = 6,
            extraBranchCount = 5,
            minimumPathLength = 12,
            minimumScrambledTiles = 16,
            generationAttempts = 100,
            cellSpacing = 8f
        };

    [Header("UI References")]
    [SerializeField] private RectTransform board;
    [SerializeField] private GridLayoutGroup boardLayout;
    [SerializeField] private WireTileView tilePrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;

    [Header("Player References")]
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Wire Sprites")]
    [SerializeField] private Sprite straightSprite;
    [SerializeField] private Sprite cornerSprite;
    [SerializeField] private Sprite teeSprite;
    [SerializeField] private Sprite crossSprite;
    [SerializeField] private Sprite sourceSprite;
    [SerializeField] private Sprite targetSprite;
    [SerializeField] private Sprite brokenSprite;

    [Header("Pair Colors")]
    [SerializeField] private Color[] pairColors =
    {
        new Color(1f, 0.82f, 0.15f, 1f),
        new Color(0.2f, 0.85f, 1f, 1f),
        new Color(1f, 0.35f, 0.75f, 1f),
        new Color(0.45f, 1f, 0.45f, 1f)
    };

    [Tooltip("Shown when more than one source powers the same network.")]
    [SerializeField] private Color mixedPowerColor = Color.white;

    [Header("Random Generation")]
    [Tooltip("Enable this during debugging to reproduce the same board.")]
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 12345;

    [Header("Success Animation")]
    [SerializeField, Min(1)] private int flashCount = 3;
    [SerializeField, Min(0.01f)] private float flashOnTime = 0.2f;
    [SerializeField, Min(0.01f)] private float flashOffTime = 0.14f;
    [SerializeField, Min(0f)] private float closeDelay = 0.4f;

    private readonly List<WireTileView> tiles =
        new List<WireTileView>();

    private readonly List<List<int>> traversalOrders =
        new List<List<int>>();

    private readonly List<List<int>> shortestPaths =
        new List<List<int>>();

    private readonly WirePuzzleGenerator puzzleGenerator =
        new WirePuzzleGenerator();

    private GeneratedWirePuzzle currentPuzzle;
    private Action completionCallback;
    private Coroutine completionRoutine;
    private bool isCompleting;
    private int gridSize;
    private int tileCount;

    public override bool IsOpen => gameObject.activeSelf;
    public PuzzleDifficulty CurrentDifficulty { get; private set; }
    public int CurrentSeed { get; private set; }

    public IReadOnlyList<List<int>> TraversalOrders => traversalOrders;
    public IReadOnlyList<List<int>> ShortestPaths => shortestPaths;

    // These two aliases preserve code that inspected the old one-pair puzzle.
    public IReadOnlyList<int> LastTraversalOrder =>
        traversalOrders.Count > 0
            ? traversalOrders[0]
            : Array.Empty<int>();

    public IReadOnlyList<int> ShortestPath =>
        shortestPaths.Count > 0
            ? shortestPaths[0]
            : Array.Empty<int>();

    private void Awake()
    {
        if (boardLayout == null && board != null)
            boardLayout = board.GetComponent<GridLayoutGroup>();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void OnDisable()
    {
        RestorePlayerControl();
    }

    public override void Open(
        PuzzleDifficulty difficulty,
        Action onCompleted)
    {
        CurrentDifficulty = difficulty;
        WirePuzzleGenerationSettings settings =
            GetDifficultySettings(difficulty);

        completionCallback = onCompleted;
        isCompleting = false;

        gameObject.SetActive(true);
        DisablePlayerControl();

        if (closeButton != null)
            closeButton.interactable = true;

        CurrentSeed = useFixedSeed
            ? fixedSeed
            : Guid.NewGuid().GetHashCode();

        currentPuzzle = puzzleGenerator.Generate(settings, CurrentSeed);
        CurrentSeed = currentPuzzle.Seed;
        gridSize = currentPuzzle.GridSize;
        tileCount = gridSize * gridSize;

        ApplyGridLayout(settings.cellSpacing);
        EnsureTilePool();
        ConfigureGeneratedTiles(settings.minimumScrambledTiles);
        RefreshPowerState();
        UpdateProgressText();
    }

    public override void Close()
    {
        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        completionCallback = null;
        isCompleting = false;
        gameObject.SetActive(false);
    }

    private WirePuzzleGenerationSettings GetDifficultySettings(
        PuzzleDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PuzzleDifficulty.Normal:
                return normalSettings;

            case PuzzleDifficulty.Hard:
                return hardSettings;

            default:
                return easySettings;
        }
    }

    private void ApplyGridLayout(float spacing)
    {
        if (board == null || boardLayout == null)
        {
            Debug.LogError(
                "WirePuzzlePanel requires Board and its GridLayoutGroup.",
                this
            );
            return;
        }

        boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        boardLayout.constraintCount = gridSize;
        boardLayout.spacing = new Vector2(spacing, spacing);

        RectOffset padding = boardLayout.padding;
        float availableWidth =
            board.rect.width - padding.left - padding.right -
            spacing * (gridSize - 1);
        float availableHeight =
            board.rect.height - padding.top - padding.bottom -
            spacing * (gridSize - 1);

        float cellSize = Mathf.Floor(
            Mathf.Min(availableWidth, availableHeight) / gridSize
        );

        boardLayout.cellSize = new Vector2(cellSize, cellSize);
    }

    private void EnsureTilePool()
    {
        if (tilePrefab == null || board == null)
        {
            Debug.LogError(
                "WirePuzzlePanel requires Board and WireTile prefab.",
                this
            );
            return;
        }

        while (tiles.Count < tileCount)
        {
            WireTileView newTile = Instantiate(tilePrefab, board);
            newTile.name = $"WireTile_{tiles.Count:00}";
            tiles.Add(newTile);
        }

        for (int i = 0; i < tiles.Count; i++)
            tiles[i].gameObject.SetActive(i < tileCount);
    }

    private void ConfigureGeneratedTiles(int requestedScrambledCount)
    {
        if (currentPuzzle == null || tiles.Count < tileCount)
            return;

        int[] startingTurns = CreateScrambledTurns(
            requestedScrambledCount
        );

        for (int i = 0; i < tileCount; i++)
        {
            GeneratedWireTile definition = currentPuzzle.Tiles[i];

            tiles[i].Configure(
                i,
                definition.Type,
                startingTurns[i],
                GetSprite(definition.Type),
                definition.Locked,
                definition.ChannelId,
                GetChannelColor(definition.ChannelId),
                HandleTileClicked
            );
        }
    }

    private int[] CreateScrambledTurns(int requestedCount)
    {
        int[] turns = new int[tileCount];
        List<int> scrambleCandidates = new List<int>();

        for (int i = 0; i < tileCount; i++)
        {
            GeneratedWireTile tile = currentPuzzle.Tiles[i];
            turns[i] = tile.SolvedQuarterTurns;

            if (CanHaveWrongLogicalRotation(tile))
                scrambleCandidates.Add(i);
        }

        Shuffle(scrambleCandidates);

        int scrambleCount = Mathf.Min(
            Mathf.Max(0, requestedCount),
            scrambleCandidates.Count
        );

        for (int i = 0; i < scrambleCount; i++)
        {
            int tileIndex = scrambleCandidates[i];
            turns[tileIndex] = GetRandomWrongRotation(
                currentPuzzle.Tiles[tileIndex]
            );
        }

        // Each pair receives at least one deliberately incorrect path tile.
        // This prevents another pair's scrambling from leaving it pre-solved.
        for (int channelIndex = 0;
             channelIndex < currentPuzzle.Channels.Count;
             channelIndex++)
        {
            EnsureChannelStartsUnsolved(
                currentPuzzle.Channels[channelIndex],
                turns
            );
        }

        return turns;
    }

    private static bool CanHaveWrongLogicalRotation(
        GeneratedWireTile tile)
    {
        return !tile.Locked &&
               tile.Type != WireTileType.Empty &&
               tile.Type != WireTileType.Broken &&
               tile.Type != WireTileType.Cross;
    }

    private static int GetRandomWrongRotation(
        GeneratedWireTile tile)
    {
        WireDirection solved = WireTileView.GetConnectionsFor(
            tile.Type,
            tile.SolvedQuarterTurns
        );

        List<int> wrongTurns = new List<int>();

        for (int offset = 1; offset < 4; offset++)
        {
            int candidate = tile.SolvedQuarterTurns + offset;

            if (WireTileView.GetConnectionsFor(tile.Type, candidate) != solved)
                wrongTurns.Add(candidate);
        }

        return wrongTurns.Count > 0
            ? wrongTurns[UnityEngine.Random.Range(0, wrongTurns.Count)]
            : tile.SolvedQuarterTurns;
    }

    private void EnsureChannelStartsUnsolved(
        GeneratedWireChannel channel,
        int[] turns)
    {
        for (int i = 1; i < channel.SolutionPath.Count - 1; i++)
        {
            int tileIndex = channel.SolutionPath[i];
            GeneratedWireTile tile = currentPuzzle.Tiles[tileIndex];

            WireDirection shown = WireTileView.GetConnectionsFor(
                tile.Type,
                turns[tileIndex]
            );
            WireDirection solved = WireTileView.GetConnectionsFor(
                tile.Type,
                tile.SolvedQuarterTurns
            );

            if (shown != solved)
                return;
        }

        for (int i = 1; i < channel.SolutionPath.Count - 1; i++)
        {
            int tileIndex = channel.SolutionPath[i];
            GeneratedWireTile tile = currentPuzzle.Tiles[tileIndex];

            if (!CanHaveWrongLogicalRotation(tile))
                continue;

            turns[tileIndex] = GetRandomWrongRotation(tile);
            return;
        }
    }

    private void HandleTileClicked(WireTileView clickedTile)
    {
        if (isCompleting)
            return;

        clickedTile.RotateClockwise();

        if (RefreshPowerState())
        {
            BeginSuccessSequence();
            return;
        }

        UpdateProgressText();
    }

    private bool RefreshPowerState()
    {
        if (currentPuzzle == null)
            return false;

        const int UnpoweredChannel = -1;
        const int MixedChannel = -2;

        int[] poweredByChannel = new int[tileCount];

        for (int i = 0; i < poweredByChannel.Length; i++)
            poweredByChannel[i] = UnpoweredChannel;

        traversalOrders.Clear();
        shortestPaths.Clear();
        bool allPairsSolved = true;

        for (int channelIndex = 0;
             channelIndex < currentPuzzle.Channels.Count;
             channelIndex++)
        {
            GeneratedWireChannel channel =
                currentPuzzle.Channels[channelIndex];

            List<int> traversalOrder;
            List<int> path;
            bool[] visited = RunBreadthFirstSearch(
                channel.SourceIndex,
                channel.TargetIndex,
                out traversalOrder,
                out path
            );

            traversalOrders.Add(traversalOrder);
            shortestPaths.Add(path);

            for (int i = 0; i < tileCount; i++)
            {
                if (!visited[i])
                    continue;

                if (poweredByChannel[i] == UnpoweredChannel)
                {
                    poweredByChannel[i] = channel.ChannelId;
                }
                else if (poweredByChannel[i] != channel.ChannelId)
                {
                    poweredByChannel[i] = MixedChannel;
                }
            }

            if (!visited[channel.TargetIndex])
                allPairsSolved = false;
        }

        for (int i = 0; i < tileCount; i++)
        {
            int powerChannel = poweredByChannel[i];

            if (powerChannel == UnpoweredChannel)
            {
                tiles[i].SetPowered(false);
            }
            else if (powerChannel == MixedChannel)
            {
                tiles[i].SetPowered(true, mixedPowerColor);
            }
            else
            {
                tiles[i].SetPowered(
                    true,
                    GetChannelColor(powerChannel)
                );
            }
        }

        return allPairsSolved;
    }

    private bool[] RunBreadthFirstSearch(
        int sourceIndex,
        int targetIndex,
        out List<int> traversalOrder,
        out List<int> shortestPath)
    {
        bool[] visited = new bool[tileCount];
        int[] parents = new int[tileCount];
        Queue<int> queue = new Queue<int>();

        traversalOrder = new List<int>();
        shortestPath = new List<int>();

        for (int i = 0; i < parents.Length; i++)
            parents[i] = -1;

        visited[sourceIndex] = true;
        queue.Enqueue(sourceIndex);

        while (queue.Count > 0)
        {
            int currentIndex = queue.Dequeue();
            traversalOrder.Add(currentIndex);

            for (int directionIndex = 0;
                 directionIndex < SearchDirections.Length;
                 directionIndex++)
            {
                TryEnqueueConnectedNeighbor(
                    currentIndex,
                    SearchDirections[directionIndex],
                    visited,
                    parents,
                    queue
                );
            }
        }

        if (visited[targetIndex])
            BuildShortestPath(targetIndex, parents, shortestPath);

        return visited;
    }

    private void TryEnqueueConnectedNeighbor(
        int currentIndex,
        WireDirection direction,
        bool[] visited,
        int[] parents,
        Queue<int> queue)
    {
        if ((tiles[currentIndex].Connections & direction) == 0)
            return;

        int currentRow = currentIndex / gridSize;
        int currentColumn = currentIndex % gridSize;
        int neighborRow = currentRow;
        int neighborColumn = currentColumn;

        switch (direction)
        {
            case WireDirection.Up: neighborRow--; break;
            case WireDirection.Right: neighborColumn++; break;
            case WireDirection.Down: neighborRow++; break;
            case WireDirection.Left: neighborColumn--; break;
        }

        if (neighborRow < 0 || neighborRow >= gridSize ||
            neighborColumn < 0 || neighborColumn >= gridSize)
        {
            return;
        }

        int neighborIndex = neighborRow * gridSize + neighborColumn;

        if (visited[neighborIndex])
            return;

        WireDirection required = GetOppositeDirection(direction);

        if ((tiles[neighborIndex].Connections & required) == 0)
            return;

        visited[neighborIndex] = true;
        parents[neighborIndex] = currentIndex;
        queue.Enqueue(neighborIndex);
    }

    private static void BuildShortestPath(
        int targetIndex,
        int[] parents,
        List<int> result)
    {
        int currentIndex = targetIndex;

        while (currentIndex >= 0)
        {
            result.Add(currentIndex);
            currentIndex = parents[currentIndex];
        }

        result.Reverse();
    }

    private void UpdateProgressText()
    {
        if (statusText == null || currentPuzzle == null)
            return;

        int solvedPairs = 0;

        for (int i = 0; i < shortestPaths.Count; i++)
        {
            if (shortestPaths[i].Count > 0)
                solvedPairs++;
        }

        statusText.text =
            $"RESTORE POWER - {CurrentDifficulty}  " +
            $"{solvedPairs}/{currentPuzzle.Channels.Count}";
    }

    private void BeginSuccessSequence()
    {
        if (isCompleting)
            return;

        isCompleting = true;

        if (closeButton != null)
            closeButton.interactable = false;

        for (int i = 0; i < tileCount; i++)
        {
            tiles[i].SetInputEnabled(false);
            tiles[i].SetVisualState(WireTileVisualState.Dimmed);
        }

        if (statusText != null)
            statusText.text = "POWER RESTORED";

        completionRoutine = StartCoroutine(PlaySuccessSequence());
    }

    private IEnumerator PlaySuccessSequence()
    {
        for (int flash = 0; flash < flashCount; flash++)
        {
            SetAllShortestPathsVisual(WireTileVisualState.Success);
            yield return new WaitForSecondsRealtime(flashOnTime);

            SetAllShortestPathsVisual(WireTileVisualState.Dimmed);
            yield return new WaitForSecondsRealtime(flashOffTime);
        }

        SetAllShortestPathsVisual(WireTileVisualState.Success);
        yield return new WaitForSecondsRealtime(0.2f);

        Action callback = completionCallback;
        completionCallback = null;
        callback?.Invoke();

        yield return new WaitForSecondsRealtime(closeDelay);

        completionRoutine = null;
        gameObject.SetActive(false);
    }

    private void SetAllShortestPathsVisual(
        WireTileVisualState visualState)
    {
        for (int pathIndex = 0;
             pathIndex < shortestPaths.Count;
             pathIndex++)
        {
            List<int> path = shortestPaths[pathIndex];
            Color pathColor = GetChannelColor(
                currentPuzzle.Channels[pathIndex].ChannelId
            );

            for (int i = 0; i < path.Count; i++)
            {
                if (visualState == WireTileVisualState.Success)
                {
                    tiles[path[i]].SetVisualState(
                        visualState,
                        pathColor
                    );
                }
                else
                {
                    tiles[path[i]].SetVisualState(visualState);
                }
            }
        }
    }

    private Color GetChannelColor(int channelId)
    {
        if (channelId < 0 || pairColors == null || pairColors.Length == 0)
            return Color.yellow;

        return pairColors[channelId % pairColors.Length];
    }

    private static WireDirection GetOppositeDirection(
        WireDirection direction)
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

    private Sprite GetSprite(WireTileType tileType)
    {
        switch (tileType)
        {
            case WireTileType.Straight: return straightSprite;
            case WireTileType.Corner: return cornerSprite;
            case WireTileType.Tee: return teeSprite;
            case WireTileType.Cross: return crossSprite;
            case WireTileType.Source: return sourceSprite;
            case WireTileType.Target: return targetSprite;
            case WireTileType.Broken: return brokenSprite;
            default: return null;
        }
    }

    private void DisablePlayerControl()
    {
        if (playerInteractor != null)
            playerInteractor.SetInteractionEnabled(false);

        if (playerMovement != null)
            playerMovement.SetMovementEnabled(false);
    }

    private void RestorePlayerControl()
    {
        if (playerInteractor != null)
            playerInteractor.SetInteractionEnabled(true);

        if (playerMovement != null)
            playerMovement.SetMovementEnabled(true);
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            T temporary = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temporary;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Apply Generated Solution")]
    private void DebugApplyGeneratedSolution()
    {
        if (!Application.isPlaying || currentPuzzle == null)
        {
            Debug.LogWarning(
                "Open this puzzle in Play Mode before applying its solution.",
                this
            );
            return;
        }

        for (int i = 0; i < tileCount; i++)
        {
            GeneratedWireTile definition = currentPuzzle.Tiles[i];

            tiles[i].Configure(
                i,
                definition.Type,
                definition.SolvedQuarterTurns,
                GetSprite(definition.Type),
                definition.Locked,
                definition.ChannelId,
                GetChannelColor(definition.ChannelId),
                HandleTileClicked
            );
        }

        if (RefreshPowerState())
        {
            Debug.Log(
                $"Generated solution passed. Seed: {currentPuzzle.Seed}",
                this
            );
            BeginSuccessSequence();
        }
        else
        {
            Debug.LogError(
                $"Generated solution failed. Seed: {currentPuzzle.Seed}",
                this
            );
        }
    }
#endif
}
