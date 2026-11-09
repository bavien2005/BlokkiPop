using System.Collections;
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

    [Header("Drag feel")]
    [SerializeField] private Vector3 inputOffset = new(0f, 2f, 0f);
    [SerializeField, Min(.1f)] private float dragSensitivity = 1.4f;
    private Coroutine rejectRoutine;

    private Vector2 inputPoint; 

    private Vector3 previouMousePosition = Vector3.positiveInfinity;

    private Vector2Int previousDragPoint; 

    private Vector2Int currentDragPoint;

    private Camera mainCamera;

    private Vector2 center;

    private SortingGroup sortingGroup;

    private BoxCollider2D inputCollider;

    private int colorId;

    private Color blockColor; 

    private void Awake()
    {
        sortingGroup = gameObject.GetComponent<SortingGroup>();
        inputCollider = gameObject.GetComponent<BoxCollider2D>();
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

        var minRow = polyominoRows;
        var maxRow = -1;
        var minColumn = polyminoColums;
        var maxColumn = -1;

        for (var r = 0; r < polyominoRows; ++r)
        {
            for (var c = 0; c < polyminoColums; ++c)
            {
                if (polyomino[r, c] <= 0) continue;

                minRow = Mathf.Min(minRow, r);
                maxRow = Mathf.Max(maxRow, r);
                minColumn = Mathf.Min(minColumn, c);
                maxColumn = Mathf.Max(maxColumn, c);
            }
        }

        if (maxRow < 0 || maxColumn < 0)
        {
            center = new Vector2(polyminoColums * 0.5f, polyominoRows * 0.5f);
        }
        else
        {
            // Center each visible shape inside its slot, including asymmetric
            // shapes such as L pieces and the single-cell piece.
            center = new Vector2(
                (minColumn + maxColumn + 1) * 0.5f,
                (minRow + maxRow + 1) * 0.5f);

            if (inputCollider != null)
            {
                const float touchPadding = 0.65f;
                inputCollider.offset = Vector2.zero;
                inputCollider.size = new Vector2(
                    maxColumn - minColumn + 1 + touchPadding,
                    maxRow - minRow + 1 + touchPadding);
            }
        }

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

    public Vector2Int GetShapeDimensions(int polyominoIndex)
    {
        int[,] shape = Polyominos.Get(polyominoIndex);
        return new Vector2Int(shape.GetLength(1), shape.GetLength(0));
    }

    /// <summary>Sets the idle tray scale and caches it for restoring after a drag.</summary>
    public void SetPreviewScale(float cellScale)
    {
        scale = Vector3.one * cellScale;
        transform.localScale = scale;
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

    internal bool BeginPointerDrag(Vector2 screenPosition)
    {
        if (InputBlocked || mainCamera == null || !gameObject.activeInHierarchy)
            return false;
        if (rejectRoutine != null) { StopCoroutine(rejectRoutine); rejectRoutine = null; }

        AudioManager.Instance?.PlaySFX("PickUp");
        inputPoint = mainCamera.ScreenToWorldPoint(screenPosition);
        transform.localPosition = position + inputOffset;
        transform.localScale = Vector3.one;

        blocks.ResetBlocksSortingOrder();
        SetSortingGroup(1);
        currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);
        board.Hover(currentDragPoint ,polyominusIndex , colorId );
        Highlight(board.HighlightPolyominoColums, board.HighlightPolyominoRows);
        previousDragPoint = currentDragPoint;
        previouMousePosition = screenPosition;
        return true;
    }


    internal void MovePointerDrag(Vector2 screenPosition)
    {
        if (InputBlocked || previouMousePosition == Vector3.positiveInfinity)
            return;
        Vector3 currentMousePosition = screenPosition;
        if(currentMousePosition != previouMousePosition)
        {
            previouMousePosition = currentMousePosition;
            var inputDelta = (Vector2)mainCamera.ScreenToWorldPoint(screenPosition) - inputPoint;

            transform.localPosition = position + inputOffset + (Vector3)inputDelta * dragSensitivity;

            currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);

            if(currentDragPoint != previousDragPoint)
            {
                previousDragPoint = currentDragPoint;
                board.Hover(currentDragPoint, polyominusIndex , colorId);
                Highlight(board.HighlightPolyominoColums, board.HighlightPolyominoRows);
            }
        }
    }

    internal void EndPointerDrag(Vector2 screenPosition)
    {
        MovePointerDrag(screenPosition);
        if (InputBlocked || previouMousePosition == Vector3.positiveInfinity)
        {
            CancelPointerDrag();
            return;
        }
        AudioManager.Instance?.PlaySFX("Drop");
        previouMousePosition = Vector3.positiveInfinity;
        currentDragPoint = Vector2Int.RoundToInt((Vector2)transform.position - center);
        bool placed = board.Place(currentDragPoint , polyominusIndex , colorId);
        if (placed)
        {
            transform.localPosition = position;
            transform.localScale = scale;
            gameObject.SetActive(false);
            blocks.Remove();
        }
        else
        {
            AudioManager.Instance?.PlayInvalidDrop();
            rejectRoutine = StartCoroutine(RejectDrop());
            return;
        }
    }

    private IEnumerator RejectDrop()
    {
        Vector3 dropPosition = transform.localPosition;
        Vector3 reboundPosition = dropPosition + Vector3.up * 0.20f;
        Vector3 dropScale = transform.localScale;
        const float duration = 0.20f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float rebound = t < 0.42f
                ? Mathf.Sin((t / 0.42f) * Mathf.PI * 0.5f)
                : 1f - Mathf.Pow((t - 0.42f) / 0.58f, 2f);
            transform.localPosition = Vector3.Lerp(dropPosition, reboundPosition, rebound);
            transform.localScale = Vector3.Lerp(dropScale, scale * 0.88f, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        transform.localPosition = position;
        transform.localScale = scale;
        blocks.ResetBlocksSortingOrder();
        rejectRoutine = null;
    }

    internal void CancelPointerDrag()
    {
        if (previouMousePosition == Vector3.positiveInfinity) return;
        previouMousePosition = Vector3.positiveInfinity; transform.localPosition = position; transform.localScale = scale;
        if (board != null) board.CancelPlacementPreview();
    }

    private void OnApplicationPause(bool paused) { if (paused) CancelPointerDrag(); }
    private void OnDisable() { CancelPointerDrag(); }

    private static bool InputBlocked => Time.timeScale <= 0f || PopGameplaySession.Instance != null && PopGameplaySession.Instance.IsTransitioning || AdsPrivacyPanel.IsModalOpen || PopGameplayUI.IsModalOpen ||
        (AdsService.Instance != null && AdsService.Instance.IsShowing) ||
        (GameFlowManager._instance != null && GameFlowManager._instance.IsGameOver);

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
