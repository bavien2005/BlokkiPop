using System.Collections.Generic;
using UnityEngine;

public class Blocks : MonoBehaviour
{
    [SerializeField] private Block[] blocks;
    [SerializeField] private Board board;

    [Header("Block Preview Layout")]
    [Tooltip("Size of one preview cell relative to a cell on the board.")]
    [SerializeField, Range(0.45f, 0.75f)] private float previewCellSize = 0.62f;

    [Tooltip("Vertical center of the three block previews in world space.")]
    [SerializeField] private float previewCenterY = -2.05f;

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

    private int[] polyominoIndexes;
    private int blockCount;

    private readonly List<int> allShapeIndexes = new();
    private readonly List<int> fittingShapeIndexes = new();

    private void Start()
    {
        ApplyTrayLayout();

        float blockWidth = (float)Board.Size / blocks.Length;
        for (int i = 0; i < blocks.Length; ++i)
        {
            blocks[i].transform.localPosition = new Vector3(
                blockWidth * (i + 0.5f),
                previewCenterY,
                0f);
            blocks[i].transform.localScale = new Vector3(
                previewCellSize,
                previewCellSize,
                previewCellSize);
            blocks[i].Initialize();
        }

        polyominoIndexes = new int[blocks.Length];
        Generate();
    }

#if UNITY_EDITOR
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
    }
#endif

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

        // The board sprite uses a bottom-left pivot. Offset it so the Inspector
        // value remains the intuitive visual center of the tray.
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
        BlockDifficultyLevel level = ResolveDifficultyLevel();
        RefreshShapeCandidates();

        bool[] mustFit = CreateFittingSlots(level.guaranteedFittingBlocks);
        for (int i = 0; i < blocks.Length; ++i)
        {
            polyominoIndexes[i] = ChoosePolyomino(level, mustFit[i]);

            int colorId = colorPalette.GetRandomColorId();
            Color color = colorPalette.GetColor(colorId);

            blocks[i].SetColor(colorId, color);
            blocks[i].gameObject.SetActive(true);
            blocks[i].Show(polyominoIndexes[i]);
            ++blockCount;
        }
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
            int swapIndex = Random.Range(0, i + 1);
            (slots[i], slots[swapIndex]) = (slots[swapIndex], slots[i]);
        }

        return slots;
    }

    private int ChoosePolyomino(BlockDifficultyLevel level, bool mustFit)
    {
        List<int> source = mustFit && fittingShapeIndexes.Count > 0
            ? fittingShapeIndexes
            : allShapeIndexes;

        if (source.Count == 0)
            return 0;

        float comboBonus = board != null
            ? board.ComboDifficultyBonus * comboDifficultyStrength
            : 0f;

        PolyominoDifficulty desiredDifficulty = RollDifficulty(level, comboBonus);
        List<int> matchingDifficulty = new();
        foreach (int index in source)
        {
            if (Polyominos.GetDifficulty(index) == desiredDifficulty)
                matchingDifficulty.Add(index);
        }

        return SelectByBoardFit(
            matchingDifficulty.Count > 0 ? matchingDifficulty : source,
            Mathf.Clamp(level.boardFitBias - comboBonus, -1f, 1f));
    }

    private static PolyominoDifficulty RollDifficulty(BlockDifficultyLevel level, float comboBonus)
    {
        float easy = Mathf.Max(0f, level.easyWeight - comboBonus * 0.65f);
        float standard = Mathf.Max(0f, level.standardWeight + comboBonus * 0.10f);
        float hard = Mathf.Max(0f, level.hardWeight + comboBonus * 0.75f);
        float total = easy + standard + hard;
        if (total <= 0.001f)
            return PolyominoDifficulty.Standard;

        float roll = Random.value * total;
        if (roll < easy)
            return PolyominoDifficulty.Easy;
        if (roll < easy + standard)
            return PolyominoDifficulty.Standard;
        return PolyominoDifficulty.Hard;
    }

    private int SelectByBoardFit(List<int> candidates, float fitBias)
    {
        if (candidates.Count == 1 || board == null || Mathf.Abs(fitBias) < 0.01f)
            return candidates[Random.Range(0, candidates.Count)];

        List<int> sorted = new(candidates);
        sorted.Sort((left, right) => board.CountAvailablePlacements(left)
            .CompareTo(board.CountAvailablePlacements(right)));

        if (fitBias > 0f)
            sorted.Reverse();

        float strength = Mathf.Abs(fitBias);
        int topRange = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Lerp(sorted.Count, Mathf.Max(1, sorted.Count / 4), strength)),
            1,
            sorted.Count);
        return sorted[Random.Range(0, topRange)];
    }

    private static BlockDifficultyLevel[] CreateDefaultDifficultyLevels()
    {
        return new[]
        {
            new BlockDifficultyLevel
            {
                displayName = "Level 1 - Gentle Start",
                scoreThreshold = 0,
                easyWeight = 0.90f,
                standardWeight = 0.10f,
                hardWeight = 0f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.90f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 2 - Building Up",
                scoreThreshold = 300,
                easyWeight = 0.60f,
                standardWeight = 0.35f,
                hardWeight = 0.05f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.55f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 3 - Balanced",
                scoreThreshold = 800,
                easyWeight = 0.35f,
                standardWeight = 0.45f,
                hardWeight = 0.20f,
                guaranteedFittingBlocks = 3,
                boardFitBias = 0.20f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 4 - Challenging",
                scoreThreshold = 1600,
                easyWeight = 0.15f,
                standardWeight = 0.45f,
                hardWeight = 0.40f,
                guaranteedFittingBlocks = 3,
                boardFitBias = -0.10f
            },
            new BlockDifficultyLevel
            {
                displayName = "Level 5 - Expert",
                scoreThreshold = 3000,
                easyWeight = 0.05f,
                standardWeight = 0.30f,
                hardWeight = 0.65f,
                guaranteedFittingBlocks = 3,
                boardFitBias = -0.45f
            }
        };
    }

    public void Remove()
    {
        --blockCount;
        if (blockCount <= 0)
        {
            blockCount = 0;
            Generate();
        }

        bool lose = true;
        for (int i = 0; i < blocks.Length; ++i)
        {
            if (blocks[i].gameObject.activeSelf && board.CheckPlace(polyominoIndexes[i]))
            {
                lose = false;
                break;
            }
        }

        if (lose)
        {
            GameFlowManager._instance.CheckHighScore();
            Debug.Log("Thua cuộc ");
        }
    }

    public void ResetBlocksSortingOrder()
    {
        for (int i = 0; i < blocks.Length; ++i)
            blocks[i].SetSortingGroup(0);
    }
}
