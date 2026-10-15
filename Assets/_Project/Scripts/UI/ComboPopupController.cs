using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ComboPopupController : MonoBehaviour
{
    private Sprite softCircleSprite;

    [Header("Popup Layout")]
    [SerializeField] private Vector2 popupPosition = new(0f, 230f);
    [SerializeField, Min(0.3f)] private float popupDuration = 1.15f;
    [SerializeField, Min(20f)] private float travelDistance = 110f;

    [Header("Style")]
    [SerializeField] private Color titleColor = new(0.25f, 0.9f, 1f, 1f);
    [SerializeField] private Color outlineColor = new(0.04f, 0.1f, 0.34f, 1f);
    [SerializeField, Range(4, 18)] private int sparkleCount = 10;

    private RectTransform popupTransform;
    private CanvasGroup canvasGroup;
    private Image glowImage;
    private TextMeshProUGUI comboText;
    private TextMeshProUGUI multiplierText;
    private TextMeshProUGUI clearText;
    private TextMeshProUGUI rewardText;
    private TMP_FontAsset popupFont;
    private Coroutine animationRoutine;
    private readonly List<Sparkle> sparkles = new();

    private struct Sparkle
    {
        public Image image;
        public Vector2 start;
        public Vector2 velocity;
        public float rotation;
    }

    private void Awake()
    {
        popupTransform = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        BuildVisuals();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (softCircleSprite == null)
            return;

        Texture2D texture = softCircleSprite.texture;
        Destroy(softCircleSprite);
        Destroy(texture);
        softCircleSprite = null;
    }

    public void Show(ComboClearResult result)
    {
        gameObject.SetActive(true);
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        Color accent = GetAccentColor(result.ComboCount, result.ClearedLineCount, result.FeverStarted);
        comboText.text = "COMBO";
        multiplierText.text = $"x{result.ComboCount}";
        clearText.text = GetClearLabel(result.ClearedLineCount);
        rewardText.text = result.FeverStarted
            ? "FEVER!"
            : $"CHAIN x{result.ComboCount}";

        comboText.color = titleColor;
        multiplierText.color = accent;
        clearText.color = Color.Lerp(accent, Color.white, 0.42f);
        rewardText.color = Color.Lerp(accent, Color.white, 0.18f);
        glowImage.color = new Color(accent.r, accent.g, accent.b, 0.22f);

        PrepareSparkles(accent);
        animationRoutine = StartCoroutine(AnimatePopup());
    }

    private void BuildVisuals()
    {
        // Some Unity versions do not include the old UI/Skin/UISprite.psd built-in
        // resource. Build this small soft sprite ourselves, so the popup works in
        // every version without needing a separately imported UI image.
        Sprite uiSprite = GetSoftCircleSprite();
        popupFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF")
            ?? TMP_Settings.defaultFontAsset;

        glowImage = CreateImage("Glow", uiSprite);
        RectTransform glowRect = glowImage.rectTransform;
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = new Vector2(670f, 220f);
        glowRect.anchoredPosition = new Vector2(0f, 10f);
        glowImage.raycastTarget = false;

        comboText = CreateText("Combo Text", new Vector2(-155f, 28f), new Vector2(350f, 126f), 86f);
        comboText.alignment = TextAlignmentOptions.Right;

        multiplierText = CreateText("Multiplier Text", new Vector2(115f, 28f), new Vector2(180f, 130f), 104f);
        multiplierText.alignment = TextAlignmentOptions.Left;

        clearText = CreateText("Clear Label", new Vector2(0f, -66f), new Vector2(520f, 52f), 38f);
        clearText.alignment = TextAlignmentOptions.Center;

        rewardText = CreateText("Reward Label", new Vector2(0f, -112f), new Vector2(420f, 42f), 28f);
        rewardText.alignment = TextAlignmentOptions.Center;

        for (int i = 0; i < sparkleCount; ++i)
        {
            Image sparkle = CreateImage($"Sparkle {i + 1:00}", uiSprite);
            sparkle.raycastTarget = false;
            RectTransform sparkleRect = sparkle.rectTransform;
            sparkleRect.anchorMin = sparkleRect.anchorMax = new Vector2(0.5f, 0.5f);
            sparkleRect.sizeDelta = new Vector2(12f, 12f);
            sparkles.Add(new Sparkle { image = sparkle });
        }
    }

    private Image CreateImage(string objectName, Sprite sprite)
    {
        GameObject child = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(transform, false);
        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject child = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(transform, false);

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.font = popupFont ?? TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = outlineColor;
        text.outlineWidth = 0.20f;
        text.raycastTarget = false;
        child.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.55f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private Sprite GetSoftCircleSprite()
    {
        if (softCircleSprite != null)
            return softCircleSprite;

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Combo Popup Soft Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                float normalizedX = (x + 0.5f) / size * 2f - 1f;
                float normalizedY = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.Clamp01(1f - radius);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        softCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        softCircleSprite.hideFlags = HideFlags.DontSave;
        return softCircleSprite;
    }

    private void PrepareSparkles(Color accent)
    {
        for (int i = 0; i < sparkles.Count; ++i)
        {
            Sparkle sparkle = sparkles[i];
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(150f, 310f);
            sparkle.start = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            sparkle.velocity = sparkle.start.normalized * Random.Range(48f, 125f);
            sparkle.rotation = Random.Range(-180f, 180f);
            sparkle.image.rectTransform.anchoredPosition = sparkle.start;
            sparkle.image.rectTransform.localScale = Vector3.one * Random.Range(0.45f, 1.15f);
            sparkle.image.color = Color.Lerp(accent, Color.white, Random.Range(0.25f, 0.85f));
            sparkles[i] = sparkle;
        }
    }

    private IEnumerator AnimatePopup()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.3f, popupDuration);
        Vector2 startPosition = popupPosition - new Vector2(0f, 38f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float inT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
            float outT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 1f, t));
            float pop = t < 0.22f
                ? Mathf.Lerp(0.50f, 1.15f, t / 0.22f)
                : Mathf.Lerp(1.15f, 1f, Mathf.InverseLerp(0.22f, 0.48f, t));

            canvasGroup.alpha = inT * outT;
            popupTransform.localScale = Vector3.one * pop;
            popupTransform.anchoredPosition = startPosition + new Vector2(0f, travelDistance * t);
            multiplierText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 16f) * 3f * outT);

            UpdateSparkles(t, outT);
            yield return null;
        }

        animationRoutine = null;
        gameObject.SetActive(false);
    }

    private void UpdateSparkles(float t, float alpha)
    {
        foreach (Sparkle sparkle in sparkles)
        {
            RectTransform rect = sparkle.image.rectTransform;
            rect.anchoredPosition = sparkle.start + sparkle.velocity * t + Vector2.up * (45f * t * t);
            rect.localRotation = Quaternion.Euler(0f, 0f, sparkle.rotation + t * 280f);
            Color color = sparkle.image.color;
            color.a = alpha * Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI);
            sparkle.image.color = color;
        }
    }

    private static string GetClearLabel(int clearedLineCount)
    {
        return clearedLineCount switch
        {
            2 => "DOUBLE CLEAR!",
            3 => "TRIPLE CLEAR!",
            4 => "QUAD CLEAR!",
            >= 5 => "MEGA CLEAR!",
            _ => "LINE CLEAR!"
        };
    }

    private static Color GetAccentColor(int comboCount, int clearedLineCount, bool feverStarted)
    {
        if (feverStarted)
            return new Color(1f, 0.70f, 0.12f, 1f);
        if (clearedLineCount >= 3)
            return new Color(1f, 0.34f, 0.82f, 1f);
        if (clearedLineCount == 2)
            return new Color(1f, 0.82f, 0.16f, 1f);

        float hue = Mathf.Repeat(0.53f + (comboCount - 1) * 0.075f, 1f);
        return Color.HSVToRGB(hue, 0.72f, 1f);
    }
}
