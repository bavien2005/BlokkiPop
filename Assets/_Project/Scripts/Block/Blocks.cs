using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Blocks : MonoBehaviour
{
    [SerializeField] private Block[] blocks;
    [SerializeField] private Board board;

    [Header("Block Preview Layout")]
    [Tooltip("Size of one preview cell relative to a cell on the board.")]
    [SerializeField, Range(0.45f, 0.75f)] private float previewCellSize = 0.62f;

    [Tooltip("Vertical center of the three block previews in world space.")]
    [SerializeField] private float previewCenterY = -2.05f;

    [Tooltip("Minimum clear space between the edge of the tray and preview blocks.")]
    [SerializeField, Min(0f)] private float trayPreviewEdgePadding = 0.38f;

    [Tooltip("Extra gap between each preview slot, in world units.")]
    [SerializeField, Min(0f)] private float previewSlotGap = 0.18f;

    [Header("Block Tray")]
    [SerializeField] private bool showTray = true;
    [SerializeField] private Vector2 trayCenter = new(4f, -2.05f);
    [SerializeField] private Vector2 traySize = new(8.5f, 2.8f);
    [SerializeField] private Color trayTint = Color.white;

    [Tooltip("Tray object stored in the scene so it is visible outside Play Mode.")]
    [SerializeField] private SpriteRenderer trayRenderer;

    [Tooltip("Tray shadow object stored in the scene so it is visible outside Play Mode.")]
    [SerializeField] private SpriteRenderer trayShadowRenderer;

    [SerializeField] private BlockColorPalette colorPalette;

    [Header("Difficulty Progression")]
    [Tooltip("Five score-driven levels. Keep Score Threshold values in ascending order.")]
    [SerializeField] private BlockDifficultyLevel[] difficultyLevels = CreateDefaultDifficultyLevels();

    [SerializeField, Tooltip("Runtime only: the level selected from current score.")]
    private int currentDifficultyLevel;

    [Tooltip("Extra difficulty applied while a clear combo is active. This is intentionally gentle.")]
    [SerializeField, Range(0f, 1f)] private float comboDifficultyStrength = 0.55f;

    [Header("Smart Tray Generation")]
    [Tooltip("When enabled, the generator simulates the board before showing a tray. Turn this off only to compare with the old random behaviour.")]
    [SerializeField] private bool useSmartTrayGeneration = true;

    [Tooltip("How many possible 3-block trays are sampled before choosing the safest valid one. More attempts improve quality but cost a little more CPU when a tray spawns.")]
    [SerializeField, Range(1, 30)] private int trayCandidateAttempts = 8;

    [Tooltip("Maximum placement branches checked for one shape during tray simulation. Increase toward 64 for a stricter solver; 8-16 is smooth on mobile.")]
    [SerializeField, Range(1, 64)] private int maxSimulationBranchesPerShape = 12;

    [Header("Danger Analysis")]
    [Tooltip("How strongly a crowded or fragmented board shifts generation toward easy shapes. 0 leaves score difficulty unchanged; 1 gives strong assistance.")]
    [SerializeField, Range(0f, 1f)] private float dangerDifficultyRelief = 0.72f;

    [Tooltip("How strongly danger favors shapes with many legal positions on the board.")]
    [SerializeField, Range(0f, 1f)] private float dangerFitBias = 0.82f;

    [SerializeField, Range(0f, 100f), Tooltip("Runtime readout: 0 is a wide-open board; 100 is very crowded, fragmented, and low on legal shapes.")]
    private float currentBoardDanger;

    [Header("Rescue Mode")]
    [Tooltip("Rescue is considered only when the danger score reaches this value. It increases the chance of compact shapes and shapes that can clear now.")]
    [SerializeField, Range(0f, 100f)] private float rescueDangerThreshold = 80f;

    [Tooltip("How many consecutive high-danger trays are needed before rescue may activate. Use 1 to rescue immediately.")]
    [SerializeField, Range(1, 3)] private int rescueDangerStreakRequired = 1;

    [Tooltip("Chance that rescue activates after the danger threshold and streak requirement are reached.")]
    [SerializeField, Range(0f, 1f)] private float rescueActivationChance = 0.78f;

    [Tooltip("Extra preference for shapes that have an immediate row or column clear available while rescue is active.")]
    [SerializeField, Range(0f, 2f)] private float rescueClearPriority = 1.15f;

    [SerializeField, Tooltip("Runtime readout: true when the current tray was generated with rescue assistance.")]
    private bool rescueActive;

    [SerializeField, Tooltip("Runtime readout: consecutive tray generations above the rescue danger threshold.")]
    private int highDangerTrayStreak;

    private int[] polyominoIndexes;
    private int blockCount;

    private readonly List<int> allShapeIndexes = new();
    private readonly List<int> fittingShapeIndexes = new();
    private readonly Dictionary<int, ShapeMetrics> shapeMetrics = new();

    private readonly struct ShapeMetrics
    {
        public readonly int Placements;
        public readonly int ClearingPlacements;
        public readonly bool HasClearingData;

        public ShapeMetrics(int placements, int clearingPlacements, bool hasClearingData)
        {
            Placements = placements;
            ClearingPlacements = clearingPlacements;
            HasClearingData = hasClearingData;
        }
    }

    private IEnumerator Start()
    {
        ApplyTrayLayout();
        ApplyPreviewLayout();

        for (int i = 0; i < blocks.Length; ++i)
        {
            blocks[i].Initialize();
        }

        polyominoIndexes = new int[blocks.Length];
        while (board != null && !board.IsReady) yield return null;
        if (PopGameplaySession.Instance != null) PopGameplaySession.Instance.InitializeBoardAndTray(board, this);
        else { InitializeGenerator(unchecked((uint)System.DateTime.UtcNow.Ticks)); Generate(); }
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyTrayLayoutAfterValidation;
    }

    private void OnValidate()
    {
        // SpriteRenderer.size invokes Unity's internal tiling callback. Calling
        // it directly from OnValidate produces a SendMessage warning, so wait
        // until the current validation pass has completely finished.
        UnityEditor.EditorApplication.delayCall -= ApplyTrayLayoutAfterValidation;
        UnityEditor.EditorApplication.delayCall += ApplyTrayLayoutAfterValidation;
    }

    private void ApplyTrayLayoutAfterValidation()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyTrayLayoutAfterValidation;

        if (this == null || Application.isPlaying)
            return;

        ApplyTrayLayout();
        ApplyPreviewLayout();
    }
#endif

    /// <summary>
    /// Keeps the three preview slots centered in the tray in Edit Mode as well
    /// as Play Mode. Their positions are derived from the board width, rather
    /// than being hand-dragged scene offsets.
    /// </summary>
    private void ApplyPreviewLayout()
    {
        if (blocks == null || blocks.Length == 0)
            return;

        float contentWidth = Mathf.Max(
            0.1f,
            Mathf.Min(Board.Size, traySize.x) - trayPreviewEdgePadding * 2f);
        float slotWidth = contentWidth / blocks.Length;
        float firstSlotCenterX = (Board.Size - contentWidth) * 0.5f + slotWidth * 0.5f;
        for (int i = 0; i < blocks.Length; ++i)
        {
            if (blocks[i] == null)
                continue;

            blocks[i].transform.localPosition = new Vector3(
                firstSlotCenterX + slotWidth * i,
                previewCenterY,
                0f);
            blocks[i].transform.localScale = Vector3.one * previewCellSize;
        }
    }

    private void ApplyTrayLayout()
    {
        if (trayRenderer != null)
            trayRenderer.enabled = showTray;
        if (trayShadowRenderer != null)
            trayShadowRenderer.enabled = showTray;

        if (!showTray || board == null)
            return;

        SpriteRenderer boardRenderer = board.GetComponent<SpriteRenderer>();
        if (boardRenderer == null || boardRenderer.sprite == null)
        {
            Debug.LogWarning("The block tray needs the board SpriteRenderer as its visual source.", this);
            return;
        }

        ApplyTrayLayer(
            trayShadowRenderer,
            boardRenderer,
            trayCenter + new Vector2(0f, -0.12f),
            new Color(0f, 0f, 0f, 0.35f),
            -11);

        ApplyTrayLayer(
            trayRenderer,
            boardRenderer,
            trayCenter,
            trayTint,
            -10);
    }

    private void ApplyTrayLayer(
        SpriteRenderer targetRenderer,
        SpriteRenderer sourceRenderer,
        Vector2 desiredCenter,
        Color tint,
        int sortingOrder)
    {
        if (targetRenderer == null)
            return;

        if (targetRenderer.sprite != sourceRenderer.sprite)
            targetRenderer.sprite = sourceRenderer.sprite;
        if (targetRenderer.sharedMaterial != sourceRenderer.sharedMaterial)
            targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        if (targetRenderer.drawMode != SpriteDrawMode.Sliced)
            targetRenderer.drawMode = SpriteDrawMode.Sliced;
        if (targetRenderer.size != traySize)
            targetRenderer.size = traySize;
        if (targetRenderer.color != tint)
            targetRenderer.color = tint;
        if (targetRenderer.sortingOrder != sortingOrder)
            targetRenderer.sortingOrder = sortingOrder;

        // Keep the requested visual center independent of the source sprite pivot.
        Sprite sprite = targetRenderer.sprite;
        Vector2 normalizedPivot = new(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);
        Vector2 pivotOffset = new(
            (0.5f - normalizedPivot.x) * traySize.x,
            (0.5f - normalizedPivot.y) * traySize.y);

        targetRenderer.transform.localPosition = desiredCenter - pivotOffset;
    }

    private void Generate()
    {
        int[] authored = PopGameplaySession.Instance?.TakeAuthoredTray();
        if (authored != null) { ApplyTray(authored, null, null); return; }
        BlockDifficultyLevel level = ResolveDifficultyLevel();
        shapeMetrics.Clear();
        RefreshShapeCandidates();

        int[] selectedShapes = useSmartTrayGeneration
            ? GenerateSmartTray(level)
            : GenerateBasicTray(level);

        ApplyTray(selectedShapes, null, null);
    }

    private void FitBlockPreviewInTray(int slotIndex, int shapeIndex)
    {
        if (blocks == null || slotIndex < 0 || slotIndex >= blocks.Length || blocks[slotIndex] == null)
            return;

        Vector2Int shapeSize = blocks[slotIndex].GetShapeDimensions(shapeIndex);
        float contentWidth = Mathf.Max(
            0.1f,
            Mathf.Min(Board.Size, traySize.x) - trayPreviewEdgePadding * 2f);
        float slotWidth = contentWidth / Mathf.Max(1, blocks.Length);
        float availableWidth = Mathf.Max(0.1f, slotWidth - previewSlotGap);
        float availableHeight = Mathf.Max(0.1f, traySize.y - trayPreviewEdgePadding * 2f);
        float fitScale = Mathf.Min(
            previewCellSize,
            availableWidth / Mathf.Max(1, shapeSize.x),
            availableHeight / Mathf.Max(1, shapeSize.y));

        blocks[slotIndex].SetPreviewScale(fitScale);
    }

    private int[] GenerateBasicTray(BlockDifficultyLevel level)
    {
        rescueActive = false;
        currentBoardDanger = 0f;
        bool[] mustFit = CreateFittingSlots(level.guaranteedFittingBlocks);
        int[] result = new int[blocks.Length];
        bool[,] state = board != null ? board.CreateOccupancySnapshot() : null;
        for (int i = 0; i < result.Length; ++i)
            result[i] = ChoosePolyomino(level, mustFit[i], state, default, false);
        return result;
    }

    private int[] GenerateSmartTray(BlockDifficultyLevel level)
    {
        if (board == null)
            return GenerateBasicTray(level);

        bool[,] startingState = board.CreateOccupancySnapshot();
        BoardRiskReport risk = BoardSimulation.Analyze(startingState);
        currentBoardDanger = risk.Danger;
        highDangerTrayStreak = risk.Danger >= rescueDangerThreshold
            ? highDangerTrayStreak + 1
            : 0;

        rescueActive = highDangerTrayStreak >= rescueDangerStreakRequired &&
                       generator.Value <= rescueActivationChance;

        int[] bestTray = null;
        float bestQuality = float.NegativeInfinity;
        int attempts = rescueActive ? trayCandidateAttempts + 6 : trayCandidateAttempts;

        for (int attempt = 0; attempt < attempts; ++attempt)
        {
            int[] candidate = new int[blocks.Length];
            for (int slot = 0; slot < candidate.Length; ++slot)
                candidate[slot] = ChoosePolyomino(level, true, startingState, risk, rescueActive);

            // The recursive search tries every A/B/C order and the best legal
            // placements within each order. A tray is kept only if all 3 pieces
            // have at least one simulated route.
            if (!BoardSimulation.HasPlayableOrder(startingState, candidate, maxSimulationBranchesPerShape))
                continue;

            // Score only the most promising routes after the deeper validity
            // search has already proved that the whole tray can be played.
            TraySimulationResult route = BoardSimulation.EvaluateTray(
                startingState, candidate, maxSimulationBranchesPerShape);
            float quality = EvaluateTray(candidate, startingState, rescueActive, level, route);
            if (quality > bestQuality)
            {
                bestQuality = quality;
                bestTray = candidate;
            }
        }

        if (bestTray != null)
            return bestTray;

        // A completely cramped board can have no three-piece route at all. This
        // fallback still gives the player the smallest, most placeable shapes
        // instead of manufacturing an impossible hard tray.
        return GenerateEmergencyFallback(startingState);
    }

    private float EvaluateTray(
        IReadOnlyList<int> tray,
        bool[,] state,
        bool rescue,
        BlockDifficultyLevel level,
        TraySimulationResult route)
    {
        float quality = 0f;
        foreach (int shapeIndex in tray)
        {
            ShapeMetrics metrics = GetShapeMetrics(state, shapeIndex);
            // Fit count is a mild tie-breaker only; it must not make tiny pieces
            // dominate just because they can be placed in many locations.
            quality += metrics.Placements * 0.04f;
            quality += metrics.ClearingPlacements * (rescue ? rescueClearPriority * 1.5f : 0.25f);
        }

        if (route.HasPlayableOrder)
            quality += route.Quality * level.clearOpportunityWeight;
        if (rescue && route.HasPlayableOrder)
            quality += route.ClearedLines * rescueClearPriority * 8f;
        return quality;
    }

    private int[] GenerateEmergencyFallback(bool[,] state)
    {
        int[] fallback = new int[blocks.Length];
        bool[,] simulatedState = state;

        // Build an emergency tray one piece at a time. Each chosen piece is
        // actually placed in the copied board before selecting the next one,
        // so the fallback itself has a real legal route in slot order.
        for (int slot = 0; slot < fallback.Length; ++slot)
        {
            int bestShape = -1;
            SimulatedPlacement bestPlacement = default;
            float bestQuality = float.NegativeInfinity;

            foreach (int shapeIndex in allShapeIndexes)
            {
                List<SimulatedPlacement> placements = BoardSimulation.GetPlacements(simulatedState, shapeIndex);
                if (placements.Count == 0)
                    continue;

                SimulatedPlacement placement = placements[0];
                int cellCount = Polyominos.GetCellCount(shapeIndex);
                float sizePreference = 5f - Mathf.Abs(cellCount - 4) * 1.5f;
                float quality = placement.Quality + sizePreference;
                if (quality > bestQuality)
                {
                    bestShape = shapeIndex;
                    bestPlacement = placement;
                    bestQuality = quality;
                }
            }

            // There is no remaining empty space for any shape. The game-over
            // check will handle this truly terminal board after generation.
            if (bestShape < 0)
            {
                fallback[slot] = 0;
                continue;
            }

            fallback[slot] = bestShape;
            simulatedState = BoardSimulation.ApplyPlacement(
                simulatedState, bestShape, bestPlacement.Column, bestPlacement.Row, out _);
        }

        return fallback;
    }

    private BlockDifficultyLevel ResolveDifficultyLevel()
    {
        if (difficultyLevels == null || difficultyLevels.Length == 0)
        {
            currentDifficultyLevel = 0;
            return CreateDefaultDifficultyLevels()[0];
        }

        int score = GameFlowManager._instance != null ? GameFlowManager._instance.Score : 0;
        int selectedIndex = 0;
        for (int i = 0; i < difficultyLevels.Length; ++i)
        {
            BlockDifficultyLevel level = difficultyLevels[i];
            if (level != null && score >= level.scoreThreshold)
                selectedIndex = i;
        }

        currentDifficultyLevel = selectedIndex;
        return difficultyLevels[selectedIndex] ?? CreateDefaultDifficultyLevels()[0];
    }

    private void RefreshShapeCandidates()
    {
        allShapeIndexes.Clear();
        fittingShapeIndexes.Clear();

        for (int index = 0; index < Polyominos.Lenght; ++index)
        {
            allShapeIndexes.Add(index);
            if (board != null && board.CheckPlace(index))
                fittingShapeIndexes.Add(index);
        }
    }

    private bool[] CreateFittingSlots(int guaranteedCount)
    {
        bool[] slots = new bool[blocks.Length];
        int count = Mathf.Clamp(guaranteedCount, 0, slots.Length);
        for (int i = 0; i < count; ++i)
            slots[i] = true;

        for (int i = slots.Length - 1; i > 0; --i)
        {
            int swapIndex = generator.Range(0, i + 1);
            (slots[i], slots[swapIndex]) = (slots[swapIndex], slots[i]);
        }

        return slots;
    }

    private int ChoosePolyomino(BlockDifficultyLevel level, bool mustFit, bool[,] state,
        BoardRiskReport risk, bool isRescue)
    {
        List<int> source = mustFit && fittingShapeIndexes.Count > 0
            ? fittingShapeIndexes
            : allShapeIndexes;

        if (source.Count == 0)
            return 0;

        float comboBonus = board != null
            ? board.ComboDifficultyBonus * comboDifficultyStrength
            : 0f;

        float dangerRelief = Mathf.Clamp01(risk.Danger / 100f) * dangerDifficultyRelief;
        if (isRescue)
            dangerRelief = Mathf.Min(1.35f, dangerRelief + 0.38f);

        PolyominoDifficulty desiredDifficulty = RollDifficulty(level, comboBonus, dangerRelief);
        List<int> matchingDifficulty = new();
        foreach (int index in source)
        {
            if (Polyominos.GetDifficulty(index) == desiredDifficulty)
                matchingDifficulty.Add(index);
        }

        return SelectByBoardFit(
            matchingDifficulty.Count > 0 ? matchingDifficulty : source,
            state,
            Mathf.Clamp(level.boardFitBias - comboBonus + Mathf.Clamp01(risk.Danger / 100f) * dangerFitBias, -1f, 1f),
            isRescue);
    }

    private PolyominoDifficulty RollDifficulty(BlockDifficultyLevel level, float comboBonus, float dangerRelief)
    {
        float easy = Mathf.Max(0f, level.easyWeight - comboBonus * 0.65f + dangerRelief);
        float standard = Mathf.Max(0f, level.standardWeight + comboBonus * 0.10f + dangerRelief * 0.12f);
        float hard = Mathf.Max(0f, level.hardWeight + comboBonus * 0.75f - dangerRelief * 0.72f);
        float total = easy + standard + hard;
        if (total <= 0.001f)
            return PolyominoDifficulty.Standard;

        float roll = generator.Value * total;
        if (roll < easy)
            return PolyominoDifficulty.Easy;
        if (roll < easy + standard)
            return PolyominoDifficulty.Standard;
        return PolyominoDifficulty.Hard;
    }

    private int SelectByBoardFit(List<int> candidates, bool[,] state, float fitBias, bool isRescue)
    {
        if (candidates.Count == 1 || state == null || Mathf.Abs(fitBias) < 0.01f)
            return candidates[generator.Range(0, candidates.Count)];

        // Use a soft weighted roll instead of selecting only from the top
        // quarter. The old rule repeatedly chose monominoes because they have
        // the most legal placements, especially on an empty board.
        float[] scores = new float[candidates.Count];
        float minScore = float.PositiveInfinity;
        float maxScore = float.NegativeInfinity;
        for (int i = 0; i < candidates.Count; ++i)
        {
            scores[i] = ShapeSafetyScore(state, candidates[i], isRescue);
            minScore = Mathf.Min(minScore, scores[i]);
            maxScore = Mathf.Max(maxScore, scores[i]);
        }

        float totalWeight = 0f;
        float[] weights = new float[candidates.Count];
        float scoreRange = Mathf.Max(0.001f, maxScore - minScore);
        float strength = Mathf.Abs(fitBias);
        for (int i = 0; i < candidates.Count; ++i)
        {
            float normalized = Mathf.Clamp01((scores[i] - minScore) / scoreRange);
            if (fitBias < 0f)
                normalized = 1f - normalized;

            // Even the least board-optimal shape keeps a meaningful chance.
            weights[i] = Mathf.Lerp(1f, 0.45f + normalized * 1.1f, strength);
            totalWeight += weights[i];
        }

        float roll = generator.Value * totalWeight;
        for (int i = 0; i < candidates.Count; ++i)
        {
            roll -= weights[i];
            if (roll <= 0f)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    private float ShapeSafetyScore(bool[,] state, int shapeIndex, bool isRescue)
    {
        ShapeMetrics metrics = GetShapeMetrics(state, shapeIndex, isRescue);
        float placements = metrics.Placements;
        if (!isRescue)
            return placements;

        return placements + metrics.ClearingPlacements * rescueClearPriority * 10f;
    }

    private ShapeMetrics GetShapeMetrics(bool[,] state, int shapeIndex, bool includeClearingData = true)
    {
        if (shapeMetrics.TryGetValue(shapeIndex, out ShapeMetrics metrics))
        {
            if (!includeClearingData || metrics.HasClearingData)
                return metrics;
        }

        int placements = metrics.Placements;
        if (placements == 0 && !shapeMetrics.ContainsKey(shapeIndex))
            placements = BoardSimulation.CountAvailablePlacements(state, shapeIndex);

        int clearingPlacements = includeClearingData && placements > 0
            ? BoardSimulation.CountLineClearingPlacements(state, shapeIndex)
            : 0;
        metrics = new ShapeMetrics(placements, clearingPlacements, includeClearingData);
        shapeMetrics[shapeIndex] = metrics;
        return metrics;
    }

    private static BlockDifficultyLevel[] CreateDefaultDifficultyLevels()
    {
        return new[]
        {
            new BlockDifficultyLevel
            {
                displayName = "Level 1 - Gentle Start",
                scoreThreshold = 0,
                easyWeight = 0.65f,
                standardWeight = 0.30f,
                hardWeight = 0.05f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.25f,
                clearOpportunityWeight = 0.9f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 2 - Building Up",
                scoreThreshold = 600,
                easyWeight = 0.50f,
                standardWeight = 0.38f,
                hardWeight = 0.12f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.20f,
                clearOpportunityWeight = 0.82f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 3 - Balanced",
                scoreThreshold = 1500,
                easyWeight = 0.32f,
                standardWeight = 0.43f,
                hardWeight = 0.25f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.05f,
                clearOpportunityWeight = 0.72f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 4 - Challenging",
                scoreThreshold = 3000,
                easyWeight = 0.18f,
                standardWeight = 0.38f,
                hardWeight = 0.44f,
                guaranteedFittingBlocks = 3,
                boardFitBias = -0.08f,
                clearOpportunityWeight = 0.62f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 5 - Expert",
                scoreThreshold = 5200,
                easyWeight = 0.10f,
                standardWeight = 0.34f,
                hardWeight = 0.56f,
                guaranteedFittingBlocks = 3,
                boardFitBias = -0.18f,
                clearOpportunityWeight = 0.52f
            }
        };
    }

    /// <summary>Checks whether the current tray has a valid revive plan without changing it.</summary>
    public bool CanReviveBoard()
    {
        return TryFindRevivePlan(out _, out _);
    }

    /// <summary>
    /// Makes at least one current tray piece placeable by clearing the minimum
    /// occupied cells. It does not replace pieces or change score/lines/combo.
    /// The game-flow manager enforces the once-per-run reward limit.
    /// </summary>
    public bool TryReviveBoard()
    {
        if (!TryFindRevivePlan(out int shapeIndex, out Vector2Int origin) ||
            !board.TryClearForRevive(shapeIndex, origin))
            return false;

        shapeMetrics.Clear();
        currentBoardDanger = BoardSimulation.Analyze(board.CreateOccupancySnapshot()).Danger;
        return true;
    }

    private bool TryFindRevivePlan(out int shapeIndex, out Vector2Int origin)
    {
        shapeIndex = -1;
        origin = default;
        if (board == null || blocks == null || polyominoIndexes == null ||
            blocks.Length != polyominoIndexes.Length)
            return false;

        int bestCost = int.MaxValue;
        // Slot order is the final tie-break, so the same board and tray always
        // receive the same help. Never generate an unrelated easier tray.
        for (int slot = 0; slot < blocks.Length; ++slot)
        {
            Block block = blocks[slot];
            if (block == null || !block.gameObject.activeInHierarchy ||
                !board.TryFindRevivePlacement(polyominoIndexes[slot], out Vector2Int candidate, out int cost) ||
                cost >= bestCost)
                continue;

            bestCost = cost;
            shapeIndex = polyominoIndexes[slot];
            origin = candidate;
            if (bestCost == 0)
                break;
        }

        return shapeIndex >= 0;
    }

    public void Remove()
    {
        --blockCount;
        if (PopGameplaySession.Instance != null && PopGameplaySession.Instance.CompletePlacement()) return;
        if (blockCount <= 0)
        {
            blockCount = 0;
            Generate();
        }

        PopGameplaySession.Instance?.AfterTrayTransaction();
        if (!HasPlayablePiece)
        {
            GameFlowManager._instance?.GameOver();
        }
    }

    public void ResetBlocksSortingOrder()
    {
        for (int i = 0; i < blocks.Length; ++i)
            blocks[i].SetSortingGroup(0);
    }
}
