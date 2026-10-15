using UnityEngine;
using UnityEngine.UI;

/// <summary>Small code-native UI star: no downloaded image, shader or asset dependency.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PopStarGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect rect = rectTransform.rect;
        Vector2 center = rect.center; float radius = Mathf.Min(rect.width, rect.height) * .5f;
        mesh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i < 10; ++i)
        {
            float angle = (90 + i * 36) * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1 : .45f);
            mesh.AddVert(point, color, Vector2.zero);
        }
        for (int i = 0; i < 10; ++i) mesh.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
    }
}
