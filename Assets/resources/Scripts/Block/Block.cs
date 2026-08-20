using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Block : MonoBehaviour
{
    public const int Size = 5;

    [SerializeField] private Cell cellPrefabs;

    [SerializeField] private Board board;

    [SerializeField] private Blocks blocks;

    private int polyominusIndex;

    private readonly Cell[,] cells = new Cell[Size , Size];

    private Vector3 position;

    private Vector3 scale;

    private readonly Vector3 inputOffset = new Vector3(0.0f, 2.0f, 0.0f);

    private Vector2 inputPoint; 

    private Vector3 previouMousePosition = Vector3.positiveInfinity;

    private Vector2Int previousDragPoint; 

    private Vector2Int currentDragPoint;

    private Camera mainCamera;

    private Vector2 center;

    private SortingGroup sortingGroup;

    private int colorId;

    private Color blockColor; 

    private void Awake()
    {
        sortingGroup = gameObject.GetComponent<SortingGroup>();
        mainCamera = Camera.main;
    }
    public void Initialize()
    {
        for (var r  =0  ; r < Size ; ++r)
        {
            for (var c = 0 ; c < Size ; ++c)
            {
                cells[r, c] = Instantiate(cellPrefabs, transform);
            }
        }

        position = transform.position;

        scale = transform.localScale;

    }

    public void Show(int polyominusIndex)
    {
        this.polyominusIndex = polyominusIndex;
        Hide();
        var polyomino = Polyominos.Get(polyominusIndex);
        var polyominoRows = polyomino.GetLength(0);
        var polyminoColums = polyomino.GetLength(1);

        center = new Vector2(polyminoColums * 0.5f, polyominoRows * 0.5f);
        for (var r =0; r< polyominoRows; ++r)
        {
            for (var c = 0; c < polyminoColums; ++c)
            {
                if(polyomino[r, c] > 0)  
                {
                    cells[r, c].transform.localPosition = new(
                        c - center.x + 0.5f,
                        r - center.y + 0.5f,
                        0.0f);

                    cells[r, c].SetBaseColor(blockColor);
                    cells[r, c].Normal();
                }
            }
        }
    }

    private void Hide()
    {
        for (var r = 0; r < Size; ++r)
        {
            for (var c = 0; c < Size; ++c)
            {
                cells[r,c].Hide();
            }
        }
    }

    private void OnMouseDown()
    {
        AudioManager.Instance.PlaySFX("PickUp");
        inputPoint = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        transform.localPosition = position + inputOffset;
        transform.localScale = Vector3.one;

        blocks.ResetBlocksSortingOrder();
        SetSortingGroup(1);
        currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);
        board.Hover(currentDragPoint ,polyominusIndex , colorId );
        Highlight(board.HighlightPolyominoColums, board.HighlightPolyominoRows);
        previousDragPoint = currentDragPoint;
        previouMousePosition = Input.mousePosition;
    }


    private void OnMouseDrag()
    {
       
        var currentMousePosition = Input.mousePosition;
        if(currentMousePosition != previouMousePosition)
        {
            previouMousePosition = currentMousePosition;
            var inputDelta = (Vector2)mainCamera.ScreenToWorldPoint(Input.mousePosition) - inputPoint;

            transform.localPosition = position + inputOffset + (Vector3)inputDelta * 1.4f ;

            currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);

            if(currentDragPoint != previousDragPoint)
            {
                previousDragPoint = currentDragPoint;
                board.Hover(currentDragPoint, polyominusIndex , colorId);
                Highlight(board.HighlightPolyominoColums, board.HighlightPolyominoRows);
            }
        }
    }

    private void OnMouseUp()
    {
        AudioManager.Instance.PlaySFX("Drop");
        previouMousePosition = Vector3.positiveInfinity;
        currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);
        if(board.Place(currentDragPoint , polyominusIndex , colorId))
        {
            gameObject.SetActive(false);
            blocks.Remove();
        }
        transform.localPosition = position;
        transform.localScale = scale;
       
       
    }

    private void Highlight( IReadOnlyDictionary<int, Color> colums,
     IReadOnlyDictionary<int, Color> rows)
    {
        var polyomino = Polyominos.Get(polyominusIndex);
        int polyominoRows = polyomino.GetLength(0);
        int polyominoColumns = polyomino.GetLength(1);

        Unhightlight( polyominoColumns,polyominoRows, polyomino);

        HighlightColumns( polyominoRows, polyomino, colums);

        HighlightRows(polyominoColumns,polyomino,  rows);
    }
    private void Unhightlight(int polyominoColumns, int polyominoRows, int[,] polyomino)
    {
        for (var r = 0 ; r < polyominoRows; ++r)
        {
            for (var c = 0 ; c < polyominoColumns; ++c)
            {
                if (polyomino[r,c] > 0)
                {
                    cells[r, c].Normal();
                }
            }
        }
    }

    private void HighlightColumns( int polyominoRows, int[,] polyomino, 
        IReadOnlyDictionary<int, Color> colums)
    {
        foreach (var item in colums)
        {
            int c = item.Key;
            Color color = item.Value;

            if (c < 0 ||c >= polyomino.GetLength(1))
                continue;

            for (var r = 0; r < polyominoRows; ++r)
            {
                if (polyomino[r, c] > 0)
                {
                    cells[r, c].Highlight(color);
                }
            }
        }
    }

    private void HighlightRows(int polyominoColums, int[,] polyomino,
        IReadOnlyDictionary<int, Color> rows)
    {
        foreach (var item in rows)
        {
            int r = item.Key;
            Color color = item.Value;

            if (r < 0 || r >= polyomino.GetLength(0))
                continue;

            for (var c = 0; c < polyominoColums; ++c)
            {
                if (polyomino[r, c] > 0)
                {
                    cells[r, c].Highlight(color);
                }
            }
        }
    }
    public void SetSortingGroup(int sortingOrder)
    {
        sortingGroup.sortingOrder = sortingOrder;
    }

    public void SetColor(int colorId , Color color)
    {
        this.colorId = colorId;
        blockColor = color;
    }
}
