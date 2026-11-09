using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored future-deck display. References and layout are scene/prefab-owned.</summary>
[ExecuteAlways]
public sealed class PopEndlessQueueView : MonoBehaviour
{
    [HideInInspector] public int presentationVersion;
    [Header("Authored HUD (layout and styles are never rewritten in Play)")]
    public RectTransform header;
    public RectTransform futurePanel;
    public TMP_Text heading;
    public TMP_Text title, score, bubbleCount, movesCount;
    public TMP_Text nextEmpty, laterEmpty;
    public Button pauseButton;
    [Header("Authored Endless lesson coach (hidden outside the lesson)")]
    public RectTransform lessonCoach;
    public TMP_Text lessonInstruction;
    public Button lessonSkip;
    public string lessonTitle = "HỌC PHÁ BÓNG";
    public string skipLabel = "BỎ QUA";
    public PopPiecePreviewGraphic[] pieces;
    [Header("Responsive playfield")]
    [Tooltip("Fit only this root's anchors to device cutouts. Position/size offsets and every child RectTransform remain yours.")]
    public bool fitDeviceSafeArea = true;
    [Tooltip("ResponsiveGameLayout fits the board/tray BETWEEN the authored Header and Upcoming Trays. It never moves these panels.")]
    public bool reservePlayfield = true;
    [Min(0), Tooltip("Extra space, in Canvas units, between the world-space game and these panels.")]
    public float playfieldGap = 24;
    [Header("Copy and data-driven colors")]
    public string titleText = "ENDLESS POP";
    public string completionText = "ĐÃ XÓA HẾT BÓNG!";
    public string noMoreText = "Đã hết";
    public string scoreLabel = "ĐIỂM";
    [Min(0)] public int lowMovesThreshold = 2;
    public Color normalMovesColor = Color.white;
    public Color lowMovesColor = new(1, .69f, .40f);
    public bool HasPresentation => header != null && futurePanel != null && heading != null && title != null && score != null &&
        bubbleCount != null && movesCount != null && nextEmpty != null && laterEmpty != null && pauseButton != null && pieces != null && pieces.Length == 6 &&
        System.Array.TrueForAll(pieces, piece => piece != null);
    private static readonly Vector3[] corners = new Vector3[4];

    private void OnEnable() => ApplySafeArea();
    private void Update() => ApplySafeArea();
    private void ApplySafeArea()
    {
        if (!fitDeviceSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
        FitSafeArea(Screen.safeArea, Screen.width, Screen.height);
    }
    private void FitSafeArea(Rect safe, int width, int height)
    {
        if (width <= 0 || height <= 0 || safe.width <= 0 || safe.height <= 0) return;
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
        rect.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
    }

    /// <summary>Read authored panel bounds, including designer offsets. Only the CAMERA adapts.</summary>
    public bool TryGetPlayfieldRect(Rect safe, out Rect playfield)
    {
        playfield = safe;
        if (!reservePlayfield || header == null || futurePanel == null) return false;
        float scale = GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        float bottom = Mathf.Max(safe.yMin, ScreenBounds(futurePanel).yMax + playfieldGap * scale);
        float top = Mathf.Min(safe.yMax, ScreenBounds(header).yMin - playfieldGap * scale);
        if (lessonCoach != null && lessonCoach.gameObject.activeInHierarchy)
            top = Mathf.Min(top, ScreenBounds(lessonCoach).yMin - playfieldGap * scale);
        if (top - bottom < safe.height * .25f) return false;
        playfield = Rect.MinMaxRect(safe.xMin, bottom, safe.xMax, top); return true;
    }
    public static Rect ScreenBounds(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        // These panels are authored under the Screen Space Overlay canvas.
        Vector2 min = corners[0], max = corners[0];
        for (int i = 1; i < 4; ++i) { min = Vector2.Min(min, corners[i]); max = Vector2.Max(max, corners[i]); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    public void Refresh(PopGameplaySession session)
    {
        bool visible = session != null && session.UsesEndlessPresentation && session.IsActive &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 1 && !PopGameplayUI.IsModalOpen;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (!visible) return;
        var deck = session.Definition.pieceSequence;
        if (lessonCoach != null) lessonCoach.gameObject.SetActive(session.IsEndlessLesson);
        if (session.IsEndlessLesson && lessonInstruction != null)
        {
            var instructions = session.Configuration.tutorial.endlessInstructions;
            lessonInstruction.text = instructions.Length == 0 ? "" : instructions[Mathf.Clamp(session.Moves, 0, instructions.Length - 1)];
            if (lessonSkip != null) lessonSkip.gameObject.SetActive(session.Configuration.tutorial.allowSkip);
        }
        if (heading != null) heading.text = session.IsEndlessLesson ? (session.Moves + 1).ToString() + "/" + session.Definition.moveLimit : session.EndlessWave.ToString("00");
        if (title != null) title.text = session.IsEndlessLesson ? lessonTitle : session.IsTransitioning ? completionText : titleText;
        if (score != null) score.text = scoreLabel + "  " + (GameFlowManager._instance != null ? GameFlowManager._instance.Score : 0).ToString("N0");
        if (bubbleCount != null) bubbleCount.text = session.WaveBubblesRemaining.ToString();
        if (movesCount != null)
        { movesCount.text = session.WaveMovesRemaining.ToString(); movesCount.color = session.WaveMovesRemaining <= lowMovesThreshold ? lowMovesColor : normalMovesColor; }
        if (nextEmpty != null) { nextEmpty.text = noMoreText; nextEmpty.gameObject.SetActive(session.DeckCursor >= deck.Count); }
        if (laterEmpty != null) { laterEmpty.text = noMoreText; laterEmpty.gameObject.SetActive(session.DeckCursor + 3 >= deck.Count); }
        for (int i = 0; i < pieces.Length; ++i)
        {
            int index = session.DeckCursor + i;
            pieces[i].SetShape(index < deck.Count ? deck[index] : -1);
        }
    }
    public void SkipLesson() => PopGameplaySession.Instance?.CompleteTutorial(true);
}

