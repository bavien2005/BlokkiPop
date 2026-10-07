using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{

    public const int Size = 8;

    [SerializeField] private Cell cellPrefabs;

    [SerializeField] private Transform cellsTramsform;

    [SerializeField] private BlockColorPalette colorPalette;

    [Header("Effects")]
    [SerializeField] private Color lineClearEffectColor = new(0.05f, 0.88f, 1f, 1f);
    [SerializeField] private BoardEffects boardEffects;
    [SerializeField] private ComboPopupController comboPopup;

    private readonly Cell[,] cells = new Cell[Size , Size];
   
    private readonly int[,] data = new int[Size , Size]; //0 : empty , 1 : hover , 2 : normal 

    // quy uoc
    // -1 : khong có màu , 0 : màu số 0 trong palatete , 1: tương tự ..... 
    private readonly int[,]  colorIds = new int[Size , Size];

    private readonly List<Vector2Int> hoverPoints = new();

    private readonly Dictionary<int , Color> highlightPolyominoColums = new();

    private readonly Dictionary<int, Color> highlightPolyominoRows = new();

    private readonly List<int> fullLineColums = new();

    private readonly List<int> fullLineRows = new();

    private Vector2Int previousHoverPoint;


    private readonly List<Vector2Int> previousHoverPoints = new();


    [Header("Scoring")]
    [Tooltip("Points awarded for each placed cell. This rewards useful large pieces even when they do not clear a line immediately.")]
    [SerializeField, Min(0)] private int placementScorePerCell = 3;

    [Tooltip("Base points per cleared line. Multi-line clears scale by the square of the number of lines cleared in one move (for example, 2 lines = 4x this value).")]
    [SerializeField, Min(1)] private int valueScore = 40;

    [Tooltip("Extra score multiplier added for each consecutive clear move in the combo chain.")]
    [SerializeField, Range(0f, 0.5f)] private float comboScoreBonusPerChain = 0.12f;

    [Tooltip("Upper limit for the score multiplier from a long combo chain.")]
    [SerializeField, Min(1f)] private float maxComboScoreMultiplier = 2.5f;


    [Header("Effects Shake Camera")]
    [SerializeField] private float duration, magnitude;
    void Start()
    {
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                colorIds[r, c] = -1; 

                cells[r, c] = Instantiate(cellPrefabs, cellsTramsform);

                cells[r, c].transform.position = new(
                    c + 0.5f,
                    r + 0.5f,
                    0.0f);

                cells[r, c].Hide();
            }
        }
    }

    public void Hover(Vector2Int point , int polyominoIndex , int colorId)
    {
        var polyomino = Polyominos.Get(polyominoIndex);
        var polyominoRows = polyomino.GetLength(0);
        var polyominoColums = polyomino.GetLength(1);

        UnHover();
        UnHighlight();

        highlightPolyominoColums.Clear();
        highlightPolyominoRows.Clear();
        HoverPoints(point, polyominoRows, polyominoColums, polyomino);

        if(hoverPoints.Count > 0)
        {
            previousHoverPoint = point;
            previousHoverPoints.Clear();
            previousHoverPoints.AddRange(hoverPoints);
            Hover(colorId);
            Highlight(point , polyominoColums , polyominoRows, colorId);         
        }
        else if(previousHoverPoints.Count > 0 && Math.Abs(point.x - previousHoverPoint.x) < 2 && Math.Abs(point.y - previousHoverPoint.y) < 2)
        {
            point = previousHoverPoint; 
            hoverPoints.Clear();
            hoverPoints.AddRange(previousHoverPoints);
            Hover(colorId);
            Highlight(point, polyominoColums, polyominoRows, colorId);
        }
        else
        {
            previousHoverPoints.Clear();
        }
    }

    /// <summary>Darkens occupied cells in a staggered collapse; returned time is the panel delay.</summary>
    public float PlayGameOverSequence()
    {
        const float rowStagger = 0.055f;
        const float fallDuration = 0.28f;
        for (int row = 0; row < Size; ++row)
        {
            for (int column = 0; column < Size; ++column)
            {
                if (data[row, column] != 2 || cells[row, column] == null)
                    continue;

                // The collapse rolls upward across the board, then the result card appears.
                float delay = (Size - 1 - row) * rowStagger + column * 0.008f;
                cells[row, column].PlayGameOverFall(delay);
            }
        }

        return (Size - 1) * rowStagger + (Size - 1) * 0.008f + fallDuration;
    }

    public void PlayFeverPulse(Color feverColor)
    {
        for (int row = 0; row < Size; ++row)
        {
            for (int column = 0; column < Size; ++column)
            {
                if (data[row, column] == 2 && cells[row, column] != null)
                    cells[row, column].PlayFeverPulse(feverColor);
            }
        }
    }


    private void UnHighlight()
    {
        boardEffects?.ClearClearPreview();
        UnHighlightFullLineColums();
        UnHighlightFullLineRows();
    }
    private void HoverPoints(Vector2Int point , int polyominoRows 
        , int polyominoColums , int[,] polyomino)
    {
        for (var r = 0; r < polyominoRows;  ++r)
        {
            for (var c = 0; c < polyominoColums; ++c)
            {
                if (polyomino[r, c ]> 0 )
                {
                    var hoverPoint = point + new Vector2Int(c, r);
                    if (!IsValidPoint(hoverPoint))
                    {
                        hoverPoints.Clear();
                        return;
                    }
                    hoverPoints.Add(hoverPoint);
                }
               
            }
        }
    }

    private bool IsValidPoint(Vector2Int point)
    {
        if(point.x < 0 || Size <= point.x) return false;
        if (point.y < 0 || Size <= point.y) return false;
        if (data[point.y , point.x] > 0) return false;
        return true;
    }

    private void Hover(int colorId)
    {
        Color color = colorPalette.GetColor(colorId);

        foreach (var hoverPoint in hoverPoints)
        {
            int r = hoverPoint.y;
            int c = hoverPoint.x;

            // Đánh dấu ô đang hover
            data[r, c] = 1;

            // Ghi nhớ màu của block đang hover
            colorIds[r, c] = colorId;

            // Cho Cell biết màu gốc
            cells[r, c].SetBaseColor(color);

            // Hiển thị trạng thái Hover
            cells[r, c].Hover();
        }
    }


    private void UnHover()
    {
        foreach (var hoverPoint in hoverPoints)
        {
            int r = hoverPoint.y;
            int c = hoverPoint.x;

            data[r, c] = 0;

            colorIds[r, c] = -1;

            cells[r, c].Hide();
        }

        hoverPoints.Clear();
    }


    public bool Place(Vector2Int point, int polyominoIndex , int colorId)
    {
        var polyomino = Polyominos.Get(polyominoIndex);
        var polyominoRows = polyomino.GetLength(0);
        var polyominoColums = polyomino.GetLength(1);

        UnHover();
        UnHighlight();
        HoverPoints(point, polyominoRows, polyominoColums, polyomino);

        if (hoverPoints.Count > 0)
        {
            Place(point , polyominoColums , polyominoRows , colorId);
            previousHoverPoints.Clear();
            return true;
        }
        else if (previousHoverPoints.Count > 0 && Math.Abs(point.x - previousHoverPoint.x) < 2 && Math.Abs(point.y - previousHoverPoint.y) < 2)
        {
            point = previousHoverPoint;
            hoverPoints.Clear();
            hoverPoints.AddRange(previousHoverPoints);
            Place(point, polyominoColums, polyominoRows , colorId);
            previousHoverPoints.Clear();
            return true;
        }
        previousHoverPoints.Clear();
        return false;
    }

    


    private void Place(Vector2Int point , int polyominoColums, int polyominoRows, int colorId)
    {
        Color color = colorPalette.GetColor(colorId);
        int placedCellCount = hoverPoints.Count;
        List<Vector2Int> placedPoints = new(hoverPoints);
        Vector2 placedCenter = Vector2.zero;
        foreach (Vector2Int pointOnBoard in placedPoints)
            placedCenter += (Vector2)pointOnBoard + Vector2.one * 0.5f;
        if (placedPoints.Count > 0)
            placedCenter /= placedPoints.Count;

        foreach (var hoverPoint in placedPoints)
        {
            int r = hoverPoint.y;
            int c = hoverPoint.x;

            data[r, c] = 2;

            colorIds[r, c] = colorId;

            cells[r, c].SetBaseColor(color);

            cells[r, c].Normal();
            cells[r, c].PlayPlacementPulse(
                boardEffects != null ? boardEffects.PlacementPulseDuration : 0.16f,
                Vector2.Distance((Vector2)hoverPoint + Vector2.one * 0.5f, placedCenter) * 0.024f);
        }

        boardEffects?.PlayPlacement(placedPoints, color);
        PlayNeighboringCellRipple(placedPoints, color);

        if (placedCellCount > 0)
        {
            GameFlowManager._instance?.AddCoin(placementScorePerCell * placedCellCount);
            for (int i = 0; i < placedPoints.Count; ++i)
            {
                Vector2Int pointOnBoard = placedPoints[i];
                boardEffects?.PlayScoreReward(
                    placementScorePerCell,
                    new Vector3(pointOnBoard.x + 0.5f, pointOnBoard.y + 0.5f, transform.position.z - 0.2f),
                    color,
                    i * 0.035f);
            }
        }

        ClearFullLines(
            point,
            polyominoColums,
            polyominoRows
        );

        hoverPoints.Clear();
    }

    private void PlayNeighboringCellRipple(IReadOnlyList<Vector2Int> placedPoints, Color blockColor)
    {
        HashSet<Vector2Int> placedSet = new(placedPoints);
        HashSet<Vector2Int> pulsed = new();
        for (int ring = 1; ring <= 2; ++ring)
        {
            foreach (Vector2Int origin in placedPoints)
            {
                for (int dx = -ring; dx <= ring; ++dx)
                {
                    int dyMagnitude = ring - Mathf.Abs(dx);
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector2Int neighbor = origin + new Vector2Int(dx, dyMagnitude * sign);
                        if (neighbor.x < 0 || neighbor.x >= Size || neighbor.y < 0 || neighbor.y >= Size ||
                            placedSet.Contains(neighbor) || !pulsed.Add(neighbor) || data[neighbor.y, neighbor.x] != 2)
                            continue;

                        cells[neighbor.y, neighbor.x].PlayRippleFlash(
                            Color.Lerp(blockColor, Color.white, 0.45f),
                            ring * 0.045f);
                    }
                }
            }
        }
    }

    private void ClearFullLines(Vector2Int point , int polyominoColums , int polyominoRows)
    {
        FullLineColums(point.x, point.x + polyominoColums);
        FullLineRows(point.y, point.y + polyominoRows);

       

        if (fullLineColums.Count > 0 || fullLineRows.Count > 0)
        {
            int clearedLineCount = fullLineColums.Count + fullLineRows.Count;
            ComboClearResult comboResult = boardEffects != null
                ? boardEffects.RegisterClear(clearedLineCount)
                : new ComboClearResult(1, clearedLineCount, false);

            GameFlowManager._instance?.RegisterClear(clearedLineCount, comboResult.ComboCount);

            int comboExtra = Mathf.Max(0, comboResult.ComboCount - 1);
            float comboMultiplier = Mathf.Min(
                maxComboScoreMultiplier,
                1f + comboExtra * comboScoreBonusPerChain);
            int clearScore = Mathf.RoundToInt(
                valueScore * clearedLineCount * clearedLineCount * comboMultiplier);
            GameFlowManager._instance?.AddCoin(clearScore);

            Vector3 rewardOrigin = GetClearRewardOrigin();
            boardEffects?.PlayScoreReward(clearScore, rewardOrigin, lineClearEffectColor);

            comboPopup?.Show(comboResult);
            
            boardEffects?.PlayLineClear(
                fullLineRows,
                fullLineColums,
                lineClearEffectColor,
                clearedLineCount,
                comboResult.FeverStarted);

            AudioManager.Instance?.PlayClearCue(
                clearedLineCount,
                comboResult.ComboCount,
                comboResult.FeverStarted);

            CameraShake.instance.ShackCamera(duration, magnitude);
        }
        else
        {
            boardEffects?.BreakCombo();
        }

        ClearFullLineColums();
        ClearFullLineRows();

        boardEffects?.NotifyValidMove();
        boardEffects?.SetDangerLevel(BoardSimulation.Analyze(CreateOccupancySnapshot()).Danger);
    }

    private Vector3 GetClearRewardOrigin()
    {
        Vector2 center = Vector2.zero;
        int pointCount = 0;
        foreach (int row in fullLineRows)
        {
            center += new Vector2((Size - 1) * 0.5f, row);
            pointCount++;
        }
        foreach (int column in fullLineColums)
        {
            center += new Vector2(column, (Size - 1) * 0.5f);
            pointCount++;
        }

        if (pointCount > 0)
            center /= pointCount;
        return new Vector3(center.x + 0.5f, center.y + 0.5f, transform.position.z - 0.2f);
    }

    private void FullLineColums(int fromColumn , int toColumnExclusive) 
    {
        fullLineColums.Clear();
        fromColumn = Mathf.Clamp(fromColumn, 0, Size);
        toColumnExclusive = Mathf.Clamp(toColumnExclusive, 0, Size);
        for (var c = fromColumn; c < toColumnExclusive; ++c)
        {
            var isFullLine = true;

            for (var r = 0; r< Size ; ++r)
            {
                if (data[r , c ] != 2)
                {
                    isFullLine = false; 
                    break;
                }
            }
            if (isFullLine)
            {
                fullLineColums.Add(c);
            }
        }
    }

    private void FullLineRows(int fromRow, int toRowExclusive)
    {

        fullLineRows.Clear();

        fromRow = Mathf.Clamp(fromRow, 0, Size);
        toRowExclusive = Mathf.Clamp(toRowExclusive, 0, Size);

        for (var r = fromRow; r < toRowExclusive; ++r)
        {
            var isFullLine = true;

            for (var c = 0; c < Size; ++c)
            {
                if (data[r, c] != 2)
                {
                    isFullLine = false;
                    break;
                }
            }
            if (isFullLine)
            {
                fullLineRows.Add(r);
            }
        }
    }

    private void ClearFullLineColums()
    {
        foreach (var c in fullLineColums)
        {
            for (var r = 0; r < Size; ++r)
            {
                data[r, c] = 0;

                colorIds[r, c] = -1;

                cells[r, c].PlayClearPulse(
                    lineClearEffectColor,
                    boardEffects != null ? boardEffects.CellClearDuration : 0.22f,
                    boardEffects != null
                        ? boardEffects.GetClearWaveDelay(new Vector2Int(c, r), fullLineRows, fullLineColums)
                        : 0f);
            }
        }
    }

    private void ClearFullLineRows()
    {
        foreach (var r in fullLineRows)
        {
            for (var c = 0; c < Size; ++c)
            {
                data[r, c] = 0;

                colorIds[r, c] = -1;

                cells[r, c].PlayClearPulse(
                    lineClearEffectColor,
                    boardEffects != null ? boardEffects.CellClearDuration : 0.22f,
                    boardEffects != null
                        ? boardEffects.GetClearWaveDelay(new Vector2Int(c, r), fullLineRows, fullLineColums)
                        : 0f);
            }
        }
    }


    private void Highlight(
        Vector2Int point,
        int polyominoColums,
        int polyominoRows,
        int draggedColorId)
    {
        PredictFullLineColums(point.x, point.x + polyominoColums);
        PredictFullLineRows(point.y, point.y + polyominoRows);

        Color draggedColor = colorPalette.GetColor(draggedColorId);

        boardEffects?.SetClearPreview(fullLineRows, fullLineColums, draggedColor);
        HighlightFullLineColums(draggedColor);
        HighlightFullLineRows(draggedColor);
        foreach (var fullLineColumn in fullLineColums)
        {
            highlightPolyominoColums[fullLineColumn - point.x] = draggedColor;
        }

        foreach (var fullLineRow in fullLineRows)
        {
            highlightPolyominoRows[fullLineRow - point.y] = draggedColor;
        }
    }


    private void PredictFullLineColums(int fromColumn, int toColumnExclusive)
    {
        fullLineColums.Clear();
        fromColumn = Mathf.Clamp(fromColumn, 0, Size);
        toColumnExclusive = Mathf.Clamp(toColumnExclusive, 0, Size);

        for (var c = fromColumn; c < toColumnExclusive; ++c)
        {
            var isFullLine = true;

            for (var r = 0; r < Size; ++r)
            {
                if (data[r, c] != 2 && data[r , c] != 1) 
                {
                    isFullLine = false;
                    break;
                }
            }
            if (isFullLine)
            {
                fullLineColums.Add(c);
            }
        }
    }

    private void PredictFullLineRows(int fromRow, int toRowExclusive)
    {
        fullLineRows.Clear();

        fromRow = Mathf.Clamp(fromRow, 0, Size);
        toRowExclusive = Mathf.Clamp(toRowExclusive, 0, Size);

        for (var r = fromRow; r < toRowExclusive; ++r)
        {
            var isFullLine = true;

            for (var c = 0; c < Size; ++c)
            {
                if (data[r, c] != 2 && data[r, c] != 1)
                {
                    isFullLine = false;
                    break;
                }
            }
            if (isFullLine)
            {
                fullLineRows.Add(r);
            }
        }
    }

    private void HighlightFullLineColums(Color previewColor)
    {
        foreach (var c in fullLineColums)
        {
            for (var r = 0; r < Size; ++r)
            {
                if (data[r, c] == 2 || data[r, c] == 1)
                {
                    cells[r, c].Highlight(previewColor);
                }
            }
        }
    }

    private void HighlightFullLineRows(Color previewColor)
    {
        foreach (var r in fullLineRows)
        {
            for (var c = 0; c < Size; ++c)
            {
                if (data[r, c] == 2 || data[r, c] == 1)
                {
                    cells[r, c].Highlight(previewColor);
                }
            }
        }
    }

    private void UnHighlightFullLineColums()
    {
        foreach (var c in fullLineColums)
        {
            for (var r = 0; r < Size; ++r)
            {
                if (data[r, c] == 2)
                {
                    cells[r, c].Normal();
                }
            }
        }
    }

    private void UnHighlightFullLineRows()
    {
        foreach (var r in fullLineRows)
        {
            for (var c = 0; c < Size; ++c)
            {
                if (data[r, c] == 2)
                {
                    cells[r, c].Normal();
                }
            }
        }
    }

    public bool CheckPlace(int polyominoIndex)
    {
        var polyomino = Polyominos.Get(polyominoIndex);
        var polyominoRows = polyomino.GetLength(0);
        var polyominoColums = polyomino.GetLength(1);

        var minRow = polyominoRows;
        var maxRow = -1;
        var minColumn = polyominoColums;
        var maxColumn = -1;

        for (var r = 0; r < polyominoRows; ++r)
        {
            for (var c = 0; c < polyominoColums; ++c)
            {
                if (polyomino[r, c] <= 0) continue;

                minRow = Mathf.Min(minRow, r);
                maxRow = Mathf.Max(maxRow, r);
                minColumn = Mathf.Min(minColumn, c);
                maxColumn = Mathf.Max(maxColumn, c);
            }
        }

        if (maxRow < 0 || maxColumn < 0) return false;

        for (var r = -minRow; r <= Size - 1 - maxRow; ++r)
        {
            for (var c = -minColumn; c <= Size - 1 - maxColumn; ++c)
            {
                if (CheckPlace(c, r, polyominoColums, polyominoRows, polyomino))
                    return true;
            }
        }

        return false;
    }

    public int CountAvailablePlacements(int polyominoIndex)
    {
        var polyomino = Polyominos.Get(polyominoIndex);
        var polyominoRows = polyomino.GetLength(0);
        var polyominoColumns = polyomino.GetLength(1);
        int placementCount = 0;

        for (int row = 0; row <= Size - polyominoRows; ++row)
        {
            for (int column = 0; column <= Size - polyominoColumns; ++column)
            {
                if (CheckPlace(column, row, polyominoColumns, polyominoRows, polyomino))
                    placementCount++;
            }
        }

        return placementCount;
    }

    public float ComboDifficultyBonus => boardEffects != null
        ? boardEffects.GenerationDifficultyBonus
        : 0f;

    /// <summary>Local origin shared by cells and the visual 8 x 8 grid.</summary>
    public Transform CellContainer => cellsTramsform;

    /// <summary>
    /// Returns only the occupied cells. The smart tray generator uses this copy
    /// for simulations, so it never changes the real board while thinking.
    /// </summary>
    public bool[,] CreateOccupancySnapshot()
    {
        bool[,] snapshot = new bool[Size, Size];
        for (int row = 0; row < Size; ++row)
        {
            for (int column = 0; column < Size; ++column)
                snapshot[row, column] = data[row, column] == 2;
        }

        return snapshot;
    }

    private bool CheckPlace(int co , int ro , int polyominoColums , int polyominoRows , int[,] polyomino)
    {
        for (var r = 0; r < polyominoRows; ++r)
        {
            for (var c = 0; c <  polyominoColums; ++c)
            {
                if (polyomino[r, c] <= 0) continue;

                var boardRow = ro + r;
                var boardColumn = co + c;

                if (boardRow < 0 || boardRow >= Size ||
                    boardColumn < 0 || boardColumn >= Size)
                    return false;

                if (data[boardRow, boardColumn] == 2) return false;
            }
        }
        return true;
    }

    public IReadOnlyDictionary<int, Color> HighlightPolyominoColums => highlightPolyominoColums;

    public IReadOnlyDictionary<int, Color>HighlightPolyominoRows
=> highlightPolyominoRows;
}

