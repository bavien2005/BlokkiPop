using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class GameOverPanel : MonoBehaviour
{
    public static GameOverPanel Instance { get; private set; }

    [Header("Hyper Casual GUI Sprites")]
    [SerializeField] private Sprite popupFrameSprite;
    [SerializeField] private Sprite playAgainButtonSprite;
    [SerializeField] private Sprite homeButtonSprite;

    [Header("Animation")]
    [SerializeField, Min(0.15f)] private float popupDuration = 0.46f;
    [SerializeField, Min(0.1f)] private float scoreCountDuration = 0.50f;

    private CanvasGroup overlayGroup;
    private RectTransform panelTransform;
    private TextMeshProUGUI headlineText;
    private TextMeshProUGUI subheadText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI bestText;
    private TextMeshProUGUI comboText;
    private TextMeshProUGUI linesText;
    private TextMeshProUGUI playAgainText;
    private Coroutine showRoutine;
    private TMP_FontAsset displayFont;

    private void Awake()
    {
        Instance = this;
        BuildVisuals();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(int score, int bestScore, int maxCombo, int linesCleared, bool isNewBest)
    {
        gameObject.SetActive(true);
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        headlineText.text = isNewBest ? "YOU ATE THAT!" : "NO MOVES!";
        headlineText.color = isNewBest
            ? new Color(1f, 0.78f, 0.12f, 1f)
            : new Color(0.05f, 0.56f, 0.58f, 1f);
        subheadText.text = isNewBest ? "NEW BEST. BIG FLEX!" : "SO CLOSE. RUN IT BACK?";
        bestText.text = $"BEST\n{bestScore:N0}";
        comboText.text = $"MAX COMBO\nx{Mathf.Max(0, maxCombo)}";
        linesText.text = $"LINES\n{linesCleared:N0}";
        playAgainText.text = "RUN IT BACK";

        scoreText.text = "0";
        showRoutine = StartCoroutine(AnimateShow(score));
    }

    private void BuildVisuals()
    {
        displayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF")
            ?? TMP_Settings.defaultFontAsset;

        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image dimmer = CreateImage("Dim Background", CreateSolidSprite(), root);
        Stretch(dimmer.rectTransform);
        dimmer.color = new Color(0.01f, 0.02f, 0.07f, 0.72f);
        dimmer.raycastTarget = true;

        overlayGroup = GetComponent<CanvasGroup>();
        if (overlayGroup == null)
            overlayGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject panel = new("Game Over Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root, false);
        panelTransform = (RectTransform)panel.transform;
        panelTransform.anchorMin = panelTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panelTransform.sizeDelta = new Vector2(600f, 1020f);
        Image frame = panel.GetComponent<Image>();
        frame.sprite = popupFrameSprite;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        if (frame.sprite == null)
            frame.color = new Color(0.93f, 0.88f, 0.69f, 1f);

        headlineText = CreateText("Headline", panelTransform, new Vector2(0f, 350f), new Vector2(520f, 100f), 76f);
        headlineText.alignment = TextAlignmentOptions.Center;

        subheadText = CreateText("Subhead", panelTransform, new Vector2(0f, 272f), new Vector2(520f, 52f), 30f);
        subheadText.alignment = TextAlignmentOptions.Center;
        subheadText.color = new Color(0.15f, 0.32f, 0.32f, 1f);

        TextMeshProUGUI scoreLabel = CreateText("Score Label", panelTransform, new Vector2(0f, 160f), new Vector2(440f, 44f), 30f);
        scoreLabel.text = "THIS RUN";
        scoreLabel.alignment = TextAlignmentOptions.Center;
        scoreLabel.color = new Color(0.18f, 0.40f, 0.40f, 1f);

        scoreText = CreateText("Score", panelTransform, new Vector2(0f, 82f), new Vector2(500f, 112f), 88f);
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.color = new Color(0.08f, 0.48f, 0.50f, 1f);

        bestText = CreateStat("Best Stat", panelTransform, new Vector2(-175f, -68f));
        comboText = CreateStat("Combo Stat", panelTransform, Vector2.zero);
        linesText = CreateStat("Lines Stat", panelTransform, new Vector2(175f, -68f));

        Button playAgainButton = CreateButton("Play Again", panelTransform, playAgainButtonSprite,
            new Vector2(0f, -255f), new Vector2(470f, 120f));
        playAgainButton.onClick.AddListener(PlayAgain);
        playAgainText = CreateText("Play Again Text", (RectTransform)playAgainButton.transform, Vector2.zero,
            new Vector2(430f, 82f), 46f);
        playAgainText.alignment = TextAlignmentOptions.Center;
        playAgainText.color = Color.white;

        Button homeButton = CreateButton("Home", panelTransform, homeButtonSprite,
            new Vector2(0f, -365f), new Vector2(270f, 80f));
        homeButton.onClick.AddListener(GoHome);
        TextMeshProUGUI homeText = CreateText("Home Text", (RectTransform)homeButton.transform, Vector2.zero,
            new Vector2(230f, 58f), 30f);
        homeText.text = "HOME";
        homeText.alignment = TextAlignmentOptions.Center;
        homeText.color = new Color(0.05f, 0.34f, 0.36f, 1f);
    }

    private TextMeshProUGUI CreateStat(string name, RectTransform parent, Vector2 position)
    {
        TextMeshProUGUI stat = CreateText(name, parent, position, new Vector2(158f, 110f), 26f);
        stat.alignment = TextAlignmentOptions.Center;
        stat.color = new Color(0.10f, 0.42f, 0.43f, 1f);
        return stat;
    }

    private Button CreateButton(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite ?? CreateSolidSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = sprite != null ? Color.white : new Color(0.08f, 0.76f, 0.71f, 1f);
        return buttonObject.GetComponent<Button>();
    }

    private Image CreateImage(string name, Sprite sprite, RectTransform parent)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = displayFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = new Color(0f, 0.12f, 0.14f, 0.62f);
        text.outlineWidth = 0.12f;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private IEnumerator AnimateShow(int finalScore)
    {
        overlayGroup.alpha = 0f;
        panelTransform.localScale = Vector3.one * 0.45f;
        panelTransform.anchoredPosition = new Vector2(0f, -95f);

        float elapsed = 0f;
        while (elapsed < popupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popupDuration);
            float ease = 1f - Mathf.Pow(1f - t, 4f);
            float bounce = Mathf.Sin(t * Mathf.PI) * 0.16f;
            overlayGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            panelTransform.localScale = Vector3.one * (Mathf.Lerp(0.45f, 1f, ease) + bounce);
            panelTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-95f, 0f, ease));
            yield return null;
        }

        panelTransform.localScale = Vector3.one;
        panelTransform.anchoredPosition = Vector2.zero;

        elapsed = 0f;
        while (elapsed < scoreCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / scoreCountDuration));
            scoreText.text = Mathf.RoundToInt(Mathf.Lerp(0f, finalScore, t)).ToString("N0");
            yield return null;
        }

        scoreText.text = finalScore.ToString("N0");
        showRoutine = null;
    }

    private void PlayAgain() => GameFlowManager._instance?.RestartGame();

    private void GoHome() => GameFlowManager._instance?.ReturnHome();

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
