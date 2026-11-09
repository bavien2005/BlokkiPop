using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public sealed class ResponsiveGameLayout : MonoBehaviour
{
    [Tooltip("World-space center of the complete board and block tray area.")]
    [SerializeField] private Vector2 contentCenter = new Vector2(4f, 2.65f);

    [Tooltip("World-space size containing the board and all three draggable blocks.")]
    [SerializeField] private Vector2 contentSize = new Vector2(9.2f, 12.2f);

    [Tooltip("Extra world-space breathing room inside the device safe area.")]
    [SerializeField] private Vector2 safePadding = new Vector2(0.35f, 0.35f);

    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 4.5f;
    [Tooltip("In edit mode, fit around the visible authored Classic/Endless HUD. Only the camera adapts; no child UI positions are overwritten.")]
    [SerializeField] private bool previewEndlessLayout = true;

    private Camera targetCamera;
    private PopGameplayUI authoredUI;
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
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null || !targetCamera.orthographic) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var safeArea = Screen.safeArea;
        // The new HUD is screen-space; the board/tray are world-space. Reserve
        // the actual authored panels, not a guessed Y position for one phone.
        // Journey/Daily keep their existing layout. Classic/Endless reserve only
        // their own authored HUD; Pause does not change the reserved playfield.
        var session = PopGameplaySession.Instance;
        var ui = PopGameplayUI.Instance;
        if (!Application.isPlaying)
        {
            if (authoredUI == null)
                foreach (var candidate in FindObjectsByType<PopGameplayUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene && candidate.Views != null && candidate.Views.Count > 0) { authoredUI = candidate; break; }
            ui = authoredUI;
        }
        bool endlessLayout = Application.isPlaying ? session != null && session.UsesEndlessPresentation : previewEndlessLayout && ui != null &&
            ui.ScenePreview == PopUIPage.Hud && ui.EndlessQueue != null && ui.EndlessQueue.gameObject.activeInHierarchy;
        if (endlessLayout && ui != null && ui.EndlessQueue != null &&
            ui.EndlessQueue.TryGetPlayfieldRect(safeArea, out Rect playfield)) safeArea = playfield;
        bool classicLayout = Application.isPlaying ? session != null && session.Mode == PopGameMode.Classic : previewEndlessLayout && ui != null &&
            ui.ScenePreview == PopUIPage.Hud && ui.ClassicHUD != null && ui.ClassicHUD.gameObject.activeInHierarchy;
        if (classicLayout && ui != null && ui.HasClassicHUD && ui.ClassicHUD.TryGetPlayfieldRect(safeArea, out Rect classicPlayfield)) safeArea = classicPlayfield;
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
