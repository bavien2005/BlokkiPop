using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-owned, optional offer shown before the board collapses.</summary>
public sealed class ReviveOfferPanel : MonoBehaviour
{
    private GameFlowManager owner;
    private Button watchButton;
    private Button finishButton;
    private Button simulationButton;
    private TextMeshProUGUI message;
    private bool busy;
    private RectTransform safeArea;
    private RectTransform cardRect;
    private Canvas canvas;

    public static ReviveOfferPanel Create(GameFlowManager manager)
    {
        GameObject root = new("Optional Rewarded Revive", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ReviveOfferPanel));
        Canvas rootCanvas = root.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        ReviveOfferPanel panel = root.GetComponent<ReviveOfferPanel>();
        panel.canvas = rootCanvas;
        panel.owner = manager;
        panel.Build();
        return panel;
    }

    private void Build()
    {
        Image dim = Image("Dimmer", transform, new Color(0.01f, 0.025f, 0.065f, 0.86f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(transform, false);
        UpdateSafeArea();
        Image card = Image("Revive Card", safeArea, new Color(0.035f, 0.12f, 0.17f, 1f));
        RectTransform rect = card.rectTransform;
        cardRect = rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(880, 900);
        Text("Title", rect, 310, 64, "ONE MORE CHANCE?");
        Text("Reward", rect, 205, 34, "Watch an ad to clear just enough space\nfor one of your current blocks.\nOne revive per run. Your score stays.");
        message = Text("Status", rect, 72, 26, "Ads are optional. You can finish now.");
        watchButton = Button("Watch Ad", rect, -55, "WATCH AD - REVIVE", () => owner?.RequestRevive());
        finishButton = Button("Finish Run", rect, -200, "NO THANKS - FINISH", () => owner?.DeclineRevive());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (AdsService.Instance != null && AdsService.Instance.CanSimulateDevelopmentReward)
            simulationButton = Button("Explicit Simulation", rect, -345, "TEST REVIVE - NO AD / NO REVENUE",
                () => owner?.RequestRevive(true), 25);
#endif
    }

    private void Update()
    {
        UpdateSafeArea();
        if (watchButton != null)
            watchButton.interactable = !busy && AdsService.Instance != null && AdsService.Instance.IsRewardedReady;
        if (simulationButton != null)
            simulationButton.interactable = !busy && AdsService.Instance != null && AdsService.Instance.CanSimulateDevelopmentReward;
        if (finishButton != null)
            finishButton.interactable = !busy;
    }

    public void SetBusy(bool value)
    {
        busy = value;
        if (value)
            message.text = "Opening ad... The reward is applied after it closes.";
    }

    public void ShowFailure(string text)
    {
        busy = false;
        message.text = text;
    }

    private void UpdateSafeArea()
    {
        Rect area = Screen.safeArea;
        safeArea.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
        safeArea.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        if (cardRect != null && canvas != null)
        {
            float scaleFactor = Mathf.Max(0.01f, canvas.scaleFactor);
            float fit = Mathf.Min(1f, area.width / scaleFactor / 912f, area.height / scaleFactor / 932f);
            cardRect.localScale = Vector3.one * Mathf.Max(0.01f, fit);
        }
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, float y, float fontSize, string value)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, y);
        rect.sizeDelta = new Vector2(790, 145);
        return text;
    }

    private static Button Button(string name, Transform parent, float y, string label, UnityEngine.Events.UnityAction action, float fontSize = 32)
    {
        Image image = Image(name, parent, new Color(0.05f, 0.52f, 0.55f, 1f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, y);
        rect.sizeDelta = new Vector2(780, 110);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        TextMeshProUGUI text = Text("Label", rect, 0, fontSize, label);
        text.rectTransform.sizeDelta = new Vector2(735, 100);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
