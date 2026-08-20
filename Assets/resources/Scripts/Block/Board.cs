using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{

    public const int Size = 8;

    [SerializeField] private Cell cellPrefabs;

    [SerializeField] private Transform cellsTramsform;

    [SerializeField] private BlockColorPalette colorPalette;

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

  
    void Update()
    {
        
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
            Highlight(point , polyominoColums , polyominoRows , colorId);         
        }
        else if(previousHoverPoints.Count > 0 && Math.Abs(point.x - previousHoverPoint.x) < 2 && Math.Abs(point.y - previousHoverPoint.y) < 2)
        {
            point = previousHoverPoint; 
            hoverPoints.Clear();
            hoverPoints.AddRange(previousHoverPoints);
            Hover(colorId);
            Highlight(point, polyominoColums, polyominoRows , colorId);
        }
        else
        {
            previousHoverPoints.Clear();
        }
    }


    private void UnHighlight()
    {
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

        foreach (var hoverPoint in hoverPoints)
        {
            int r = hoverPoint.y;
            int c = hoverPoint.x;

            data[r, c] = 2;

            colorIds[r, c] = colorId;

            cells[r, c].SetBaseColor(color);

            cells[r, c].Normal();
        }

        ClearFullLines(
            point,
            polyominoColums,
            polyominoRows
        );

        hoverPoints.Clear();
    }

    private void ClearFullLines(Vector2Int point , int polyominoColums , int polyominoRows)
    {
        FullLineColums(point.x, point.x + polyominoColums);
        FullLineRows(point.y, point.y + polyominoRows);

        ClearFullLineColums();
        ClearFullLineRows();
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

                cells[r, c].Hide();
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

                cells[r, c].Hide();
            }
        }
    }


    private void Highlight(Vector2Int point , int polyominoColums , int polyominoRows, int draggedColorId)
    {
        PredictFullLineColums(point.x, point.x + polyominoColums);
        PredictFullLineRows(point.y, point.y + polyominoRows);

        HighlightFullLineColums(draggedColorId);
        HighlightFullLineRows(draggedColorId);
        foreach (var fullLineColumn in fullLineColums)
        {
            int dominantColorId =GetDominantColorInColumn(fullLineColumn, draggedColorId);

            Color color = colorPalette.GetColor(dominantColorId );

            highlightPolyominoColums[fullLineColumn - point.x] = color;
        }

        foreach (var fullLineRow in fullLineRows)
        {
            int dominantColorId = GetDominantColorInRow( fullLineRow,draggedColorId);

            Color color = colorPalette.GetColor(dominantColorId);

            highlightPolyominoRows[fullLineRow - point.y] = color;
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

    private void HighlightFullLineColums(int draggedColorId)
    {
        foreach (var c in fullLineColums)
        {
            int dominantColorId = GetDominantColorInColumn( c, draggedColorId);

            UnityEngine.Color previewColor = colorPalette.GetColor(dominantColorId);

            for (var r = 0; r < Size; ++r)
            {
                if (data[r, c] == 2)
                {
                    cells[r, c].Highlight(previewColor);
                }
            }
        }
    }

    private void HighlightFullLineRows(int draggedColorId)
    {
        foreach (var r in fullLineRows)
        {
            int dominantColorId = GetDominantColorInRow( r, draggedColorId );

            Color previewColor =colorPalette.GetColor(dominantColorId);

            for (var c = 0; c < Size; ++c)
            {
                if (data[r, c] == 2)
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

    private int GetDominantColorInRow( int row,  int preferredColorId)
    {
        int[] counts = new int[colorPalette.Count];

        for (int c = 0; c < Size; ++c)
        {
            if (data[row, c] == 0)
                continue;

            int id = colorIds[row, c];

            if (id >= 0 && id < counts.Length)
            {
                counts[id]++;
            }
        }

        return GetLargestColor(counts,  preferredColorId);
    }

    private int GetDominantColorInColumn(int column, int preferredColorId)
    {
        int[] counts =  new int[colorPalette.Count];

        for (int r = 0; r < Size; ++r)
        {
            if (data[r, column] == 0)
                continue;

            int id = colorIds[r, column];

            if (id >= 0 && id < counts.Length)
            {
                counts[id]++;
            }
        }

        return GetLargestColor( counts,preferredColorId);
    }

    private int GetLargestColor( int[] counts,int preferredColorId)
    {
        int result = -1;
        int largest = -1;

        for (int i = 0; i < counts.Length; ++i)
        {
            if (counts[i] > largest)
            {
                largest = counts[i];
                result = i;
            }
            else if (counts[i] == largest &&  i == preferredColorId)
            {
                // Nếu hòa thì ưu tiên màu block đang kéo
                result = i;
            }
        }

        return result;
    }
    public IReadOnlyDictionary<int, Color> HighlightPolyominoColums => highlightPolyominoColums;

    public IReadOnlyDictionary<int, Color>HighlightPolyominoRows
=> highlightPolyominoRows;
}

