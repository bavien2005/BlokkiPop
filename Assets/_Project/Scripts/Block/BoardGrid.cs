using UnityEngine;

/// <summary>
/// Draws the 8 x 8 play-area grid over the board background and below blocks.
/// It runs in Edit Mode too, so the generated line objects are visible in
/// Hierarchy while laying out the scene.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BoardGrid : MonoBehaviour
{
    private const string GridRootName = "Board Grid";

    [Header("Board Grid")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = new(0.055f, 0.018f, 0.006f, 0.68f);
    [SerializeField, Range(0.012f, 0.10f)] private float lineThickness = 0.035f;
    [SerializeField, Range(-0.20f, -0.01f)] private float localZ = -0.06f;
    [SerializeField, Tooltip("Keeps the grid behind placed and preview blocks.")]
    private int sortingOrder = 0;

    private Sprite lineSprite;
    private Transform gridRoot;

    private void OnEnable() => Refresh();

    private void OnValidate() => Refresh();

    private void OnDestroy()
    {
        if (lineSprite == null)
            return;

        Texture2D texture = lineSprite.texture;
        if (Application.isPlaying)
        {
            Destroy(lineSprite);
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(lineSprite);
            DestroyImmediate(texture);
        }
        lineSprite = null;
    }

    public void Refresh()
    {
        if (!isActiveAndEnabled)
            return;

        Board board = GetComponent<Board>();
        Transform cellContainer = board != null ? board.CellContainer : null;
        // The grid lives beside the cells so both use the exact same local origin.
        gridRoot = cellContainer != null
            ? cellContainer.Find(GridRootName)
            : transform.Find(GridRootName);
        if (gridRoot == null)
        {
            GameObject root = new(GridRootName);
            root.transform.SetParent(transform, false);
            gridRoot = root.transform;
        }

        // The cells and grid must share one local origin. Previously the grid
        // had a hand-entered offset, which made its lines drift from cell edges
        // whenever the board artwork or its inner padding was adjusted.
        if (cellContainer != null && gridRoot.parent != cellContainer)
            gridRoot.SetParent(cellContainer, false);
        gridRoot.localPosition = Vector3.zero;
        gridRoot.localRotation = Quaternion.identity;
        gridRoot.localScale = Vector3.one;

        EnsureLineCount();
        gridRoot.gameObject.SetActive(showGrid);
        if (!showGrid)
            return;

        Sprite sprite = GetLineSprite();
        int lineIndex = 0;

        // Nine vertical and nine horizontal borders define all 64 cells.
        for (int column = 0; column <= Board.Size; ++column)
            ConfigureLine(gridRoot.GetChild(lineIndex++), sprite,
                new Vector3(column, Board.Size * 0.5f, localZ),
                new Vector2(lineThickness, Board.Size));

        for (int row = 0; row <= Board.Size; ++row)
            ConfigureLine(gridRoot.GetChild(lineIndex++), sprite,
                new Vector3(Board.Size * 0.5f, row, localZ),
                new Vector2(Board.Size, lineThickness));
    }

    private void EnsureLineCount()
    {
        const int expectedCount = (Board.Size + 1) * 2;
        while (gridRoot.childCount > expectedCount)
        {
            GameObject extra = gridRoot.GetChild(gridRoot.childCount - 1).gameObject;
            if (Application.isPlaying)
                Destroy(extra);
            else
                DestroyImmediate(extra);
        }

        while (gridRoot.childCount < expectedCount)
        {
            int index = gridRoot.childCount;
            GameObject line = new($"Grid Line {index + 1:00}");
            line.transform.SetParent(gridRoot, false);
            line.AddComponent<SpriteRenderer>();
        }
    }

    private void ConfigureLine(Transform line, Sprite sprite, Vector3 position, Vector2 size)
    {
        line.localPosition = position;
        SpriteRenderer renderer = line.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = gridColor;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = sortingOrder;
    }

    private Sprite GetLineSprite()
    {
        if (lineSprite != null)
            return lineSprite;

        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false)
        {
            name = "Board Grid Line Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply(false, true);

        lineSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        lineSprite.hideFlags = HideFlags.DontSave;
        return lineSprite;
    }
}
