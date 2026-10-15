using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ResponsiveGameLayout : MonoBehaviour
{
    [Tooltip("World-space center of the complete board and block tray area.")]
    [SerializeField] private Vector2 contentCenter = new Vector2(4f, 2.65f);

    [Tooltip("World-space size containing the board and all three draggable blocks.")]
    [SerializeField] private Vector2 contentSize = new Vector2(9.2f, 12.2f);

    [Tooltip("Extra world-space breathing room inside the device safe area.")]
    [SerializeField] private Vector2 safePadding = new Vector2(0.35f, 0.35f);

    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 4.5f;

    private Camera targetCamera;
    private int previousScreenWidth = -1;
    private int previousScreenHeight = -1;
    private float previousAspect = -1f;
    private Rect previousSafeArea;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyLayout(true);
    }

    private void LateUpdate()
    {
        ApplyLayout(false);
    }

    public void Refresh()
    {
        ApplyLayout(true);
    }

    private void ApplyLayout(bool force)
    {
        if (targetCamera == null || !targetCamera.orthographic) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var safeArea = Screen.safeArea;
        var aspect = targetCamera.aspect;

        if (!force &&
            Screen.width == previousScreenWidth &&
            Screen.height == previousScreenHeight &&
            Mathf.Abs(aspect - previousAspect) < 0.0001f &&
            Approximately(safeArea, previousSafeArea))
            return;

        previousScreenWidth = Screen.width;
        previousScreenHeight = Screen.height;
        previousAspect = aspect;
        previousSafeArea = safeArea;

        var safeWidthRatio = Mathf.Max(0.01f, safeArea.width / Screen.width);
        var safeHeightRatio = Mathf.Max(0.01f, safeArea.height / Screen.height);
        var requiredWidth = contentSize.x + safePadding.x * 2f;
        var requiredHeight = contentSize.y + safePadding.y * 2f;

        var sizeRequiredByHeight = requiredHeight / (2f * safeHeightRatio);
        var sizeRequiredByWidth = requiredWidth / (2f * aspect * safeWidthRatio);

        targetCamera.orthographicSize = Mathf.Max(
            minimumOrthographicSize,
            sizeRequiredByHeight,
            sizeRequiredByWidth);

        var fullViewHeight = targetCamera.orthographicSize * 2f;
        var fullViewWidth = fullViewHeight * aspect;
        var safeCenter = new Vector2(
            safeArea.center.x / Screen.width,
            safeArea.center.y / Screen.height);
        var safeCenterOffset = new Vector2(
            (safeCenter.x - 0.5f) * fullViewWidth,
            (safeCenter.y - 0.5f) * fullViewHeight);

        var cameraPosition = targetCamera.transform.position;
        cameraPosition.x = contentCenter.x - safeCenterOffset.x;
        cameraPosition.y = contentCenter.y - safeCenterOffset.y;
        targetCamera.transform.position = cameraPosition;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.5f &&
               Mathf.Abs(a.y - b.y) < 0.5f &&
               Mathf.Abs(a.width - b.width) < 0.5f &&
               Mathf.Abs(a.height - b.height) < 0.5f;
    }
}
