using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored Classic HUD. Only data/visibility change in Play, never child layout.</summary>
[ExecuteAlways]
public sealed class PopClassicHUDView : MonoBehaviour
{
    public RectTransform header, stats;
    public TMP_Text score, best, lines, maxCombo, newRecord;
    public Button pauseButton;
    [Header("Responsive layout")]
    [Tooltip("Only root anchors follow device cutouts. Child positions, sizes and styles stay authored.")]
    public bool fitDeviceSafeArea = true;
    public bool reservePlayfield = true;
    [Min(0)] public float playfieldGap = 24;
    [Header("Dynamic copy")]
    public string recordText = "KỶ LỤC MỚI!";
    public string comboPrefix = "×";
    public bool HasPresentation => header != null && stats != null && score != null && best != null && lines != null && maxCombo != null && newRecord != null && pauseButton != null;
    private int lastScore = -1, lastBest = -1, lastLines = -1, lastCombo = -1;

    private void OnEnable() => ApplySafeArea();
    private void Update() => ApplySafeArea();
    private void ApplySafeArea()
    {
        if (!fitDeviceSafeArea) return;
        FitSafeArea(Screen.safeArea, Screen.width, Screen.height);
    }
    private void FitSafeArea(Rect safe, int width, int height)
    {
        if (width <= 0 || height <= 0 || safe.width <= 0 || safe.height <= 0) return;
        var rect = (RectTransform)transform;
        Vector2 min = new(safe.xMin / width, safe.yMin / height), max = new(safe.xMax / width, safe.yMax / height);
        if (rect.anchorMin != min) rect.anchorMin = min;
        if (rect.anchorMax != max) rect.anchorMax = max;
    }
    public bool TryGetPlayfieldRect(Rect safe, out Rect playfield)
    {
        playfield = safe;
        if (!reservePlayfield || header == null || stats == null) return false;
        float scale = GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        float bottom = Mathf.Max(safe.yMin, PopEndlessQueueView.ScreenBounds(stats).yMax + playfieldGap * scale);
        float top = Mathf.Min(safe.yMax, PopEndlessQueueView.ScreenBounds(header).yMin - playfieldGap * scale);
        if (top - bottom < safe.height * .25f) return false;
        playfield = Rect.MinMaxRect(safe.xMin, bottom, safe.xMax, top); return true;
    }
    public void Refresh(PopGameplaySession session)
    {
        var manager = GameFlowManager._instance;
        // Finished runs still own the board behind the result/revive overlay.
        // Keep the same HUD and final values; only navigation/new scenes replace it.
        bool visible = session != null && session.Mode == PopGameMode.Classic && session.IsInitialized && manager != null &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 1 && !PopGameplayUI.IsModalOpen && !AdsPrivacyPanel.IsModalOpen;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (!visible || !HasPresentation) return;
        pauseButton.interactable = session.IsActive && !manager.IsGameOver;
        if (lastScore != manager.Score) { lastScore = manager.Score; score.text = lastScore.ToString("N0"); }
        if (lastBest != manager.HighScore) { lastBest = manager.HighScore; best.text = lastBest.ToString("N0"); }
        if (lastLines != session.Lines) { lastLines = session.Lines; lines.text = lastLines.ToString(); }
        if (lastCombo != session.MaxCombo) { lastCombo = session.MaxCombo; maxCombo.text = comboPrefix + lastCombo; }
        bool record = manager.Score > manager.HighScoreAtRunStart;
        if (newRecord.gameObject.activeSelf != record) newRecord.gameObject.SetActive(record);
        if (record && newRecord.text != recordText) newRecord.text = recordText;
    }
}
