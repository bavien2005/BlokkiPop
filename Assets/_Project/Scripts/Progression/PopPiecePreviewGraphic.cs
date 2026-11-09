using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the actual fixed-orientation polyomino, not an approximate icon.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PopPiecePreviewGraphic : MaskableGraphic
{
    [SerializeField] private int shapeId = 1;
    [SerializeField, Range(0, .4f)] private float cellGap = .1f;
    [SerializeField, Min(1)] private float maximumCellSize = 30;
    [SerializeField, Range(0, .25f)] private float bevel = .13f;
    public int ShapeId => shapeId;
    public void SetShape(int shape)
    { if (shapeId == shape) return; shapeId = shape; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); if (shapeId < 0 || shapeId >= Polyominos.Lenght) return;
        int[,] shape = Polyominos.Get(shapeId); Rect rect = rectTransform.rect;
        float size = Mathf.Min(maximumCellSize, rect.width / shape.GetLength(1), rect.height / shape.GetLength(0));
        Vector2 start = rect.center - new Vector2(shape.GetLength(1), shape.GetLength(0)) * size * .5f;
        for (int y = 0; y < shape.GetLength(0); ++y) for (int x = 0; x < shape.GetLength(1); ++x)
        {
            if (shape[y, x] == 0) continue;
            Vector2 p = start + new Vector2(x, y) * size + Vector2.one * size * cellGap * .5f;
            float side = size * (1 - cellGap), edge = side * bevel;
            Color dark = Color.Lerp(color, Color.black, .28f); dark.a = color.a;
            Color light = Color.Lerp(color, Color.white, .32f); light.a = color.a;
            Quad(mesh, p, new Vector2(side, side), dark);
            Quad(mesh, p + new Vector2(0, edge), new Vector2(side, side - edge), light);
            Quad(mesh, p + Vector2.one * edge, Vector2.one * (side - edge * 2), color);
        }
    }
    private static void Quad(VertexHelper mesh, Vector2 p, Vector2 size, Color tint)
    {
        int n = mesh.currentVertCount;
        mesh.AddVert(p, tint, Vector2.zero); mesh.AddVert(p + Vector2.up * size.y, tint, Vector2.zero);
        mesh.AddVert(p + size, tint, Vector2.zero); mesh.AddVert(p + Vector2.right * size.x, tint, Vector2.zero);
        mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
    }
}
