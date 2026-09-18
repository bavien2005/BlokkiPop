using System.Collections;
using UnityEngine;

public class Cell : MonoBehaviour
{
    [SerializeField] private Sprite normal;

    [SerializeField] private Sprite highlight;


    private SpriteRenderer spriteRenderer;

    private SpriteRenderer depthRenderer;

    private SpriteRenderer previewGlowRenderer;


    private Color baseColor = Color.white;

    private Vector3 restingScale;

    private Coroutine visualAnimation;

    private Color previewGlowColor;

    private float previewGlowStartedAt;

    private void Awake()
    {
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        restingScale = transform.localScale;
        CreateDepthLayers();
    }

    private void Update()
    {
        if (previewGlowRenderer == null || !previewGlowRenderer.gameObject.activeSelf)
            return;

        float pulse = 0.62f + Mathf.Sin((Time.time - previewGlowStartedAt) * 7f) * 0.38f;
        previewGlowRenderer.transform.localScale = Vector3.one * (1.13f + pulse * 0.06f);
        previewGlowRenderer.color = new Color(
            previewGlowColor.r,
            previewGlowColor.g,
            previewGlowColor.b,
            0.18f + pulse * 0.27f);
    }

    public void SetBaseColor(Color color)
    {
        this.baseColor = color;
    }



    public void Normal()
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        ApplyNormalVisual();
    }


    public void Highlight(Color highlightColor)
    {
        StopVisualAnimation();
        gameObject.SetActive(true);
        spriteRenderer.color = new Color(
            highlightColor.r,
            highlightColor.g,
            highlightColor.b,
            1f);
        spriteRenderer.sprite = highlight;
        SetPreviewGlow(true, highlightColor);
    }

    public void Hover()
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        spriteRenderer.color = new Color(
             baseColor.r,
             baseColor.g,
             baseColor.b,
             0.5f);
        spriteRenderer.sprite = normal;
    }

    public void Hide()
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(false);
    }

    public void PlayPlacementPulse(float duration = 0.16f)
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        visualAnimation = StartCoroutine(PlacementPulse(duration));
    }

    public void PlayClearPulse(Color effectColor, float duration = 0.22f)
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        visualAnimation = StartCoroutine(ClearPulse(effectColor, duration));
    }

    private IEnumerator PlacementPulse(float duration)
    {
        duration = Mathf.Max(0.05f, duration);
        spriteRenderer.sprite = normal;

        Color normalColor = Opaque(baseColor);
        Color flashColor = Color.Lerp(normalColor, Color.white, 0.42f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float wave = Mathf.Sin(t * Mathf.PI);
            float scale = t < 0.28f
                ? Mathf.Lerp(0.88f, 1.12f, t / 0.28f)
                : Mathf.Lerp(1.12f, 1f, (t - 0.28f) / 0.72f);

            transform.localScale = restingScale * scale;
            spriteRenderer.color = Color.Lerp(normalColor, flashColor, wave);
            yield return null;
        }

        transform.localScale = restingScale;
        spriteRenderer.color = normalColor;
        visualAnimation = null;
    }

    private IEnumerator ClearPulse(Color effectColor, float duration)
    {
        duration = Mathf.Max(0.08f, duration);
        spriteRenderer.sprite = highlight;

        Color startColor = Opaque(effectColor);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.35f
                ? Mathf.Lerp(1f, 1.15f, t / 0.35f)
                : Mathf.Lerp(1.15f, 0f, (t - 0.35f) / 0.65f);

            transform.localScale = restingScale * scale;
            spriteRenderer.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                1f - t);
            yield return null;
        }

        visualAnimation = null;
        transform.localScale = restingScale;
        gameObject.SetActive(false);
    }

    private void StopVisualAnimation()
    {
        if (visualAnimation != null)
        {
            StopCoroutine(visualAnimation);
            visualAnimation = null;
        }

        transform.localScale = restingScale;
    }

    private void ApplyNormalVisual()
    {
        spriteRenderer.sprite = normal;
        spriteRenderer.color = Opaque(baseColor);
        if (depthRenderer != null)
            depthRenderer.sprite = normal;
    }

    private void CreateDepthLayers()
    {
        if (spriteRenderer == null || normal == null)
            return;

        depthRenderer = CreateLayer("Cell Depth", -1, new Vector3(0.055f, -0.07f, 0.01f));
        depthRenderer.sprite = normal;
        depthRenderer.color = new Color(0f, 0f, 0f, 0.30f);

        previewGlowRenderer = CreateLayer("Clear Preview Glow", -2, Vector3.zero);
        previewGlowRenderer.sprite = normal;
        previewGlowRenderer.transform.localScale = Vector3.one * 1.16f;
        previewGlowRenderer.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateLayer(string layerName, int sortingOffset, Vector3 localPosition)
    {
        GameObject layer = new(layerName);
        layer.transform.SetParent(transform, false);
        layer.transform.localPosition = localPosition;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = normal;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + sortingOffset;
        return renderer;
    }

    private void SetPreviewGlow(bool visible, Color color)
    {
        if (previewGlowRenderer == null)
            return;

        previewGlowRenderer.gameObject.SetActive(visible);
        if (!visible)
            return;

        previewGlowColor = Opaque(color);
        previewGlowStartedAt = Time.time;
    }

    private static Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private void OnDisable()
    {
        visualAnimation = null;
        transform.localScale = restingScale;
    }
}
