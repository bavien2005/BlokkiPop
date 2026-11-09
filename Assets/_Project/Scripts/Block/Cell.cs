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
        PlayPlacementPulse(duration, 0f);
    }

    public void PlayPlacementPulse(float duration, float delay)
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        visualAnimation = StartCoroutine(PlacementPulse(duration, delay));
    }

    /// <summary>Glows an already occupied neighboring cell as the placement ripple passes it.</summary>
    public void PlayRippleFlash(Color rippleColor, float delay)
    {
        StopVisualAnimation();
        gameObject.SetActive(true);
        visualAnimation = StartCoroutine(RippleFlash(rippleColor, delay));
    }

    public void PlayClearPulse(Color effectColor, float duration = 0.22f)
    {
        PlayClearPulse(effectColor, duration, 0f);
    }

    public void PlayClearPulse(Color effectColor, float duration, float delay)
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        gameObject.SetActive(true);
        // Even cells waiting for their scan delay must hold the placed block's
        // color, not briefly revert to the old mixed colors underneath.
        spriteRenderer.sprite = highlight;
        spriteRenderer.color = Opaque(effectColor);
        visualAnimation = StartCoroutine(ClearPulse(effectColor, duration, delay));
    }

    /// <summary>Staggers the end-of-run fall using unscaled time so it still animates while gameplay is paused.</summary>
    public void PlayGameOverFall(float delay)
    {
        StopVisualAnimation();
        SetPreviewGlow(false, Color.clear);
        visualAnimation = StartCoroutine(GameOverFall(delay));
    }

    /// <summary>Brief hot-color flash used to announce Fever across occupied cells.</summary>
    public void PlayFeverPulse(Color feverColor, float duration = 0.36f)
    {
        StopVisualAnimation();
        gameObject.SetActive(true);
        visualAnimation = StartCoroutine(FeverPulse(feverColor, duration));
    }

    private IEnumerator PlacementPulse(float duration, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

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

    private IEnumerator ClearPulse(Color effectColor, float duration, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

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

    private IEnumerator GameOverFall(float delay)
    {
        float wait = Mathf.Max(0f, delay);
        while (wait > 0f)
        {
            wait -= Time.unscaledDeltaTime;
            yield return null;
        }

        Color startColor = spriteRenderer.color;
        Vector3 startPosition = transform.localPosition;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            transform.localPosition = startPosition + Vector3.down * (0.22f * eased);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.72f, eased);
            spriteRenderer.color = Color.Lerp(startColor, new Color(0.12f, 0.14f, 0.2f, 0.4f), eased);
            if (depthRenderer != null)
                depthRenderer.color = Color.Lerp(depthRenderer.color, new Color(0.05f, 0.06f, 0.1f, 0.2f), eased);
            yield return null;
        }

        visualAnimation = null;
    }

    private IEnumerator RippleFlash(Color rippleColor, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SetPreviewGlow(true, rippleColor);
        yield return new WaitForSeconds(0.18f);
        SetPreviewGlow(false, Color.clear);
        visualAnimation = null;
    }

    private IEnumerator FeverPulse(Color feverColor, float duration)
    {
        Color normalColor = Opaque(baseColor);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            spriteRenderer.color = Color.Lerp(normalColor, feverColor, pulse * 0.82f);
            transform.localScale = restingScale * (1f + pulse * 0.09f);
            yield return null;
        }

        transform.localScale = restingScale;
        spriteRenderer.color = normalColor;
        visualAnimation = null;
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
