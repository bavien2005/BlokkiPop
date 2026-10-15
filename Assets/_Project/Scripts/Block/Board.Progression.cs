using System.Collections.Generic;
using UnityEngine;

public partial class Board
{
    private readonly bool[,] bubbleTargets = new bool[Size, Size];
    private readonly SpriteRenderer[,] bubbleMarkers = new SpriteRenderer[Size, Size];
    private readonly List<SpriteRenderer> lessonMarkers = new();
    private Sprite markerSprite;
    private Texture2D markerTexture;
    public bool IsReady => HasInitializedCells();
    public void CancelPlacementPreview()
    { UnHover(); UnHighlight(); previousHoverPoints.Clear(); }
    public string ScoringSignature => System.FormattableString.Invariant($"{placementScorePerCell}/{valueScore}/{comboScoreBonusPerChain:R}/{maxComboScoreMultiplier:R}");

    public void ApplyRunConfiguration(PopGameplayConfiguration config, bool authored)
    {
        if (authored)
        {
            placementScorePerCell = config.authoredScoring.placementPointsPerCell;
            valueScore = config.authoredScoring.pointsPerLine;
            comboScoreBonusPerChain = config.authoredScoring.comboBonusPerChain;
            maxComboScoreMultiplier = config.authoredScoring.maximumComboMultiplier;
        }
        boardEffects?.ConfigureProgression(config.runs, authored);
    }

    public void ApplyStartingCells(IReadOnlyList<PopStartingCell> start)
    {
        UnHover(); UnHighlight(); previousHoverPoints.Clear();
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
            { data[row, col] = 0; colorIds[row, col] = -1; bubbleTargets[row, col] = false; cells[row, col].Hide(); }
        if (start != null)
            foreach (PopStartingCell cell in start)
            {
                if (cell == null || cell.position.x < 0 || cell.position.x >= Size || cell.position.y < 0 || cell.position.y >= Size) continue;
                int row = cell.position.y, col = cell.position.x;
                data[row, col] = 2; colorIds[row, col] = cell.colorId; bubbleTargets[row, col] = cell.bubble;
            }
        RefreshTheme();
    }

    public bool CanImport(PopRunSnapshot run)
    {
        if (!IsReady || !PopSaveStore.ValidRun(run)) return false;
        foreach (int color in run.occupiedColors) if (color >= colorPalette.Count) return false;
        return true;
    }

    public void Import(PopRunSnapshot run)
    {
        UnHover(); UnHighlight(); previousHoverPoints.Clear();
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
            {
                int index = row * Size + col;
                colorIds[row, col] = run.occupiedColors[index];
                data[row, col] = colorIds[row, col] >= 0 ? 2 : 0;
                bubbleTargets[row, col] = run.bubbleTargets[index];
            }
        boardEffects?.RestoreCombo(run.combo, run.comboSecondsRemaining);
        RefreshTheme();
    }

    public void Export(PopRunSnapshot run)
    {
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
            {
                int index = row * Size + col;
                // Drag previews are not occupied cells and must never be saved.
                run.occupiedColors[index] = data[row, col] == 2 ? colorIds[row, col] : -1;
                run.bubbleTargets[index] = data[row, col] == 2 && bubbleTargets[row, col];
            }
        run.combo = boardEffects != null ? boardEffects.CurrentCombo : 0;
        run.comboSecondsRemaining = boardEffects != null ? boardEffects.ComboRemainingSeconds : 0f;
    }

    public void RefreshTheme()
    {
        if (!IsReady) return;
        PopThemeDefinition theme = PopGameplaySession.Instance?.SelectedTheme;
        if (theme != null) lineClearEffectColor = theme.clearEffect;
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
            {
                if (data[row, col] == 2) { cells[row, col].SetBaseColor(colorPalette.GetColor(colorIds[row, col])); cells[row, col].Normal(); }
                else if (data[row, col] == 0) cells[row, col].Hide();
            }
        RefreshMarkers();
    }

    private int PopClearedBubbles()
    {
        int popped = 0;
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
                if (bubbleTargets[row, col] && (fullLineRows.Contains(row) || fullLineColums.Contains(col)))
                { ++popped; bubbleTargets[row, col] = false; }
        return popped;
    }

    private void RefreshMarkers()
    {
        PopGameplaySession session = PopGameplaySession.Instance;
        if (session == null) return;
        for (int row = 0; row < Size; ++row)
            for (int col = 0; col < Size; ++col)
            {
                bool visible = bubbleTargets[row, col] && data[row, col] == 2;
                if (visible && bubbleMarkers[row, col] == null) bubbleMarkers[row, col] = CreateMarker("Bubble target");
                SpriteRenderer marker = bubbleMarkers[row, col];
                if (marker == null) continue;
                marker.gameObject.SetActive(visible);
                marker.transform.position = new Vector3(col + 0.5f, row + 0.5f, -0.25f);
                marker.transform.localScale = Vector3.one * session.Configuration.ui.bubbleDiameterInCells;
                marker.color = session.SelectedTheme.bubbleColor;
            }
    }

    public void ShowTutorialPlacement(PopSolutionMove move)
    {
        foreach (SpriteRenderer marker in lessonMarkers) marker.gameObject.SetActive(false);
        if (move == null || !PopGameplaySession.Instance.Configuration.tutorial.highlightExactPlacement) return;
        int[,] shape = Polyominos.Get(move.shape); int count = 0;
        for (int row = 0; row < shape.GetLength(0); ++row)
            for (int col = 0; col < shape.GetLength(1); ++col)
            {
                if (shape[row, col] <= 0) continue;
                if (count == lessonMarkers.Count) lessonMarkers.Add(CreateMarker("Lesson landing guide"));
                SpriteRenderer marker = lessonMarkers[count++];
                marker.gameObject.SetActive(true);
                marker.transform.position = new Vector3(move.origin.x + col + 0.5f, move.origin.y + row + 0.5f, -0.3f);
                marker.transform.localScale = Vector3.one * 0.8f;
                Color color = PopGameplaySession.Instance.Configuration.ui.tutorialGhostColor;
                color.a = PopGameplaySession.Instance.Configuration.ui.tutorialGhostOpacity;
                marker.color = color;
            }
    }

    private SpriteRenderer CreateMarker(string label)
    {
        if (markerSprite == null)
        {
            const int size = 64;
            markerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float radius = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                    float alpha = Mathf.Clamp01((0.97f - radius) * 32f) * Mathf.Clamp01((radius - 0.67f) * 32f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            markerTexture.SetPixels(pixels); markerTexture.Apply(false, true);
            markerSprite = Sprite.Create(markerTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            markerSprite.hideFlags = HideFlags.DontSave;
        }
        GameObject obj = new(label); obj.transform.SetParent(transform, false);
        SpriteRenderer result = obj.AddComponent<SpriteRenderer>(); result.sprite = markerSprite;
        result.sortingOrder = 120; return result;
    }

    private void OnDestroy()
    {
        if (markerSprite != null) Destroy(markerSprite);
        if (markerTexture != null) Destroy(markerTexture);
    }
}
