using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public readonly struct ComboClearResult
{
    public readonly int ComboCount;
    public readonly int ClearedLineCount;
    public readonly bool FeverStarted;

    public ComboClearResult(int comboCount, int clearedLineCount, bool feverStarted)
    {
        ComboCount = comboCount;
        ClearedLineCount = clearedLineCount;
        FeverStarted = feverStarted;
    }
}

public sealed class BoardEffects : MonoBehaviour
{
    [Header("Hierarchy References")]
    [SerializeField] private GameObject clearParticlesObject;
    [SerializeField] private Transform lineFlashRoot;
    [SerializeField] private Transform previewGlowRoot;
    [SerializeField] private Transform boardGlowRoot;

    [Header("Placement Pop")]
    [SerializeField, Min(0.05f)] private float placementPulseDuration = 0.16f;
    [SerializeField, Range(0, 4)] private int placementParticlesPerCell = 1;

    [Header("Line Clear")]
    [SerializeField, Min(0.08f)] private float cellClearDuration = 0.22f;
    [SerializeField, Min(0.08f)] private float lineFlashDuration = 0.30f;
    [SerializeField, Range(1, 10)] private int clearParticlesPerCell = 5;
    [SerializeField, Min(0.01f)] private float lineFlashThickness = 0.22f;

    [Header("Particle Tuning")]
    [SerializeField, Min(0.1f)] private float clearParticleSpeed = 1.65f;
    [SerializeField, Min(0.05f)] private float clearParticleLifetime = 0.46f;
    [SerializeField] private Vector2 particleSizeRange = new(0.08f, 0.20f);
    [SerializeField, Range(1, 24)] private int lineFlashPoolSize = 16;

    [Header("Clear Preview Glow")]
    [SerializeField, Range(0.08f, 0.6f)] private float previewGlowThickness = 0.34f;
    [SerializeField, Range(1f, 14f)] private float previewGlowPulseSpeed = 6f;
    [SerializeField, Range(0.05f, 1f)] private float previewGlowOpacity = 0.52f;

    [Header("Combo Board Border")]
    [SerializeField, Min(0.5f)] private float comboResetDelay = 3.5f;
    [SerializeField, Min(0.1f)] private float comboGlowDuration = 1.1f;
    [SerializeField, Min(0.05f)] private float boardGlowPadding = 0.18f;
    [SerializeField] private Color comboStartColor = new(0.12f, 0.9f, 1f, 1f);
    [SerializeField] private Color comboPeakColor = new(1f, 0.35f, 0.86f, 1f);

    [Header("Fever")]
    [SerializeField, Range(2, 10)] private int feverStartCombo = 4;
    [SerializeField, Min(0.2f)] private float feverDuration = 3.2f;
    [SerializeField] private Color feverColor = new(1f, 0.77f, 0.15f, 1f);

    private readonly List<SpriteRenderer> lineFlashPool = new();
    private readonly List<SpriteRenderer> previewGlowPool = new();
    private readonly HashSet<Vector2Int> clearPoints = new();

    private ParticleSystem clearParticles;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private Texture2D flashTexture;
    private Sprite flashSprite;
    private SpriteRenderer boardGlowRenderer;
    private SpriteRenderer feverGlowRenderer;
    private int comboCount;
    private float comboExpiresAt;
    private float comboPulseEndsAt;
    private float feverEndsAt;

    public float PlacementPulseDuration => placementPulseDuration;
    public float CellClearDuration => cellClearDuration;
    public int CurrentCombo => comboCount;
    public float GenerationDifficultyBonus => Mathf.Clamp01((comboCount - 1) / 8f) * 0.28f;

    private void Awake()
    {
        PrepareParticleSystem();
        PrepareLineFlashPool();
        PreparePreviewGlowPool();
        PrepareBoardGlow();
    }

    private void Update()
    {
        UpdatePreviewGlow();
        UpdateBoardGlow();
    }

    public void SetClearPreview(
        IReadOnlyList<int> rows,
        IReadOnlyList<int> columns,
        Color color)
    {
        ClearClearPreview();

        foreach (int row in rows)
            ActivatePreviewGlow(row, true, color);

        foreach (int column in columns)
            ActivatePreviewGlow(column, false, color);
    }

    public void ClearClearPreview()
    {
        foreach (SpriteRenderer renderer in previewGlowPool)
        {
            if (renderer != null)
                renderer.gameObject.SetActive(false);
        }
    }

    public void PlayPlacement(IReadOnlyList<Vector2Int> placedPoints, Color blockColor)
    {
        if (clearParticles == null || placementParticlesPerCell <= 0)
            return;

        Color particleColor = Color.Lerp(Opaque(blockColor), Color.white, 0.28f);
        foreach (Vector2Int point in placedPoints)
        {
            for (int i = 0; i < placementParticlesPerCell; ++i)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                EmitParticle(
                    CellCenter(point),
                    direction * Random.Range(0.18f, 0.55f),
                    particleColor,
                    Random.Range(0.07f, 0.12f),
                    Random.Range(0.18f, 0.28f));
            }
        }
    }

    public void PlayLineClear(
        IReadOnlyList<int> rows,
        IReadOnlyList<int> columns,
        Color effectColor)
    {
        effectColor = Opaque(effectColor);

        foreach (int row in rows)
            PlayLineFlash(row, true, effectColor);

        foreach (int column in columns)
            PlayLineFlash(column, false, effectColor);

        if (clearParticles == null || clearParticlesPerCell <= 0)
            return;

        clearPoints.Clear();
        foreach (int row in rows)
        {
            for (int column = 0; column < Board.Size; ++column)
                clearPoints.Add(new Vector2Int(column, row));
        }

        foreach (int column in columns)
        {
            for (int row = 0; row < Board.Size; ++row)
                clearPoints.Add(new Vector2Int(column, row));
        }

        foreach (Vector2Int point in clearPoints)
        {
            for (int i = 0; i < clearParticlesPerCell; ++i)
            {
                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.up;

                direction.Normalize();
                EmitParticle(
                    CellCenter(point),
                    direction * Random.Range(clearParticleSpeed * 0.45f, clearParticleSpeed),
                    Color.Lerp(effectColor, Color.white, Random.Range(0f, 0.35f)),
                    Random.Range(particleSizeRange.x, particleSizeRange.y),
                    Random.Range(clearParticleLifetime * 0.7f, clearParticleLifetime));
            }
        }
    }

    private void PrepareParticleSystem()
    {
        if (clearParticlesObject == null)
            return;

        clearParticles = clearParticlesObject.GetComponent<ParticleSystem>();
        if (clearParticles == null)
            clearParticles = clearParticlesObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = clearParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 420;
        main.startSpeed = 0f;
        main.startLifetime = clearParticleLifetime;
        main.startSize = particleSizeRange.y;
        main.gravityModifier = 0.08f;

        ParticleSystem.EmissionModule emission = clearParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = clearParticles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = clearParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGradient = new();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = alphaGradient;

        ParticleSystem.RotationOverLifetimeModule rotation = clearParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-3.2f, 3.2f);

        ParticleSystemRenderer particleRenderer = clearParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 30;

        particleTexture = CreateRoundedSquareTexture(24);
        Shader particleShader = Shader.Find("Sprites/Default");
        if (particleShader == null)
            particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (particleShader != null)
        {
            particleMaterial = new Material(particleShader)
            {
                name = "Board Clear Particles (Runtime)",
                mainTexture = particleTexture
            };
            particleRenderer.material = particleMaterial;
        }

        clearParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PrepareLineFlashPool()
    {
        if (lineFlashRoot == null)
            return;

        flashTexture = CreateSoftFlashTexture(64, 16);
        flashSprite = Sprite.Create(
            flashTexture,
            new Rect(0f, 0f, flashTexture.width, flashTexture.height),
            new Vector2(0.5f, 0.5f),
            16f,
            0,
            SpriteMeshType.FullRect);
        flashSprite.name = "Board Line Flash (Runtime)";

        int poolCount = Mathf.Clamp(lineFlashPoolSize, 1, 24);
        for (int i = 0; i < poolCount; ++i)
        {
            GameObject flashObject = new($"Line Flash {i + 1:00}");
            flashObject.transform.SetParent(lineFlashRoot, false);

            SpriteRenderer renderer = flashObject.AddComponent<SpriteRenderer>();
            renderer.sprite = flashSprite;
            renderer.sortingOrder = 25;

            flashObject.SetActive(false);
            lineFlashPool.Add(renderer);
        }
    }

    private void PreparePreviewGlowPool()
    {
        if (previewGlowRoot == null || flashSprite == null)
            return;

        int poolCount = Mathf.Clamp(lineFlashPoolSize, 1, 24);
        for (int i = 0; i < poolCount; ++i)
        {
            GameObject glowObject = new($"Clear Preview Glow {i + 1:00}");
            glowObject.transform.SetParent(previewGlowRoot, false);

            SpriteRenderer renderer = glowObject.AddComponent<SpriteRenderer>();
            renderer.sprite = flashSprite;
            renderer.sortingOrder = 1;
            glowObject.SetActive(false);
            previewGlowPool.Add(renderer);
        }
    }

    private void PrepareBoardGlow()
    {
        if (boardGlowRoot == null)
            return;

        Board board = GetComponentInParent<Board>();
        SpriteRenderer boardRenderer = board != null ? board.GetComponent<SpriteRenderer>() : null;
        if (boardRenderer == null || boardRenderer.sprite == null)
            return;

        boardGlowRenderer = CreateBoardGlowRenderer("Combo Border Glow", boardRenderer, -2);
        feverGlowRenderer = CreateBoardGlowRenderer("Fever Board Glow", boardRenderer, -3);
        boardGlowRenderer.gameObject.SetActive(false);
        feverGlowRenderer.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateBoardGlowRenderer(
        string objectName,
        SpriteRenderer source,
        int sortingOrder)
    {
        GameObject glowObject = new(objectName);
        glowObject.transform.SetParent(boardGlowRoot, false);

        SpriteRenderer renderer = glowObject.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.drawMode = source.drawMode;
        renderer.size = source.size + Vector2.one * boardGlowPadding * 2f;
        renderer.sortingOrder = sortingOrder;

        Vector2 sourcePivot = GetPivotOffset(source.sprite, source.size);
        Vector2 glowPivot = GetPivotOffset(renderer.sprite, renderer.size);
        renderer.transform.position = source.transform.position + (Vector3)(sourcePivot - glowPivot);
        renderer.transform.rotation = source.transform.rotation;
        renderer.transform.localScale = source.transform.lossyScale;
        return renderer;
    }

    private static Vector2 GetPivotOffset(Sprite sprite, Vector2 size)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            return Vector2.zero;

        Vector2 normalizedPivot = new(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);
        return new Vector2(
            (0.5f - normalizedPivot.x) * size.x,
            (0.5f - normalizedPivot.y) * size.y);
    }

    private void ActivatePreviewGlow(int lineIndex, bool horizontal, Color color)
    {
        SpriteRenderer renderer = GetAvailablePreviewGlow();
        if (renderer == null)
            return;

        Transform glowTransform = renderer.transform;
        glowTransform.position = horizontal
            ? new Vector3(Board.Size * 0.5f, lineIndex + 0.5f, -0.04f)
            : new Vector3(lineIndex + 0.5f, Board.Size * 0.5f, -0.04f);
        glowTransform.rotation = horizontal
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, 90f);
        glowTransform.localScale = new Vector3(
            Board.Size * 0.26f,
            previewGlowThickness,
            1f);
        renderer.color = Opaque(color);
        renderer.gameObject.SetActive(true);
    }

    private void UpdatePreviewGlow()
    {
        float pulse = 0.68f + Mathf.Sin(Time.time * previewGlowPulseSpeed) * 0.32f;
        foreach (SpriteRenderer renderer in previewGlowPool)
        {
            if (renderer == null || !renderer.gameObject.activeSelf)
                continue;

            Color color = renderer.color;
            color.a = previewGlowOpacity * pulse;
            renderer.color = color;
        }
    }

    private SpriteRenderer GetAvailablePreviewGlow()
    {
        foreach (SpriteRenderer renderer in previewGlowPool)
        {
            if (!renderer.gameObject.activeSelf)
                return renderer;
        }

        return null;
    }

    public ComboClearResult RegisterClear(int clearedLineCount)
    {
        bool feverWasActive = Time.time < feverEndsAt;
        if (Time.time > comboExpiresAt)
            comboCount = 0;

        // A combo is a streak of successful moves. Clearing Double/Triple in
        // one move makes the popup stronger, but does not skip combo numbers.
        comboCount++;
        comboExpiresAt = Time.time + comboResetDelay;
        comboPulseEndsAt = Time.time + comboGlowDuration;

        if (comboCount >= feverStartCombo)
            feverEndsAt = Time.time + feverDuration;

        return new ComboClearResult(
            comboCount,
            Mathf.Max(1, clearedLineCount),
            !feverWasActive && Time.time < feverEndsAt);
    }

    public void BreakCombo()
    {
        comboCount = 0;
        comboExpiresAt = 0f;
        comboPulseEndsAt = 0f;
    }

    private void UpdateBoardGlow()
    {
        if (boardGlowRenderer == null || feverGlowRenderer == null)
            return;

        bool feverActive = Time.time < feverEndsAt;
        bool comboActive = Time.time < comboPulseEndsAt;

        boardGlowRenderer.gameObject.SetActive(comboActive || feverActive);
        feverGlowRenderer.gameObject.SetActive(feverActive);

        if (comboActive || feverActive)
        {
            float comboT = Mathf.Clamp01((comboCount - 1f) / Mathf.Max(1f, feverStartCombo - 1f));
            Color color = Color.Lerp(comboStartColor, comboPeakColor, comboT);
            float pulse = 0.70f + Mathf.Sin(Time.time * 10f) * 0.30f;
            float alpha = feverActive ? 0.60f : 0.20f + pulse * 0.38f;
            boardGlowRenderer.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (feverActive)
        {
            float pulse = 0.60f + Mathf.Sin(Time.time * 8f) * 0.40f;
            feverGlowRenderer.color = new Color(
                feverColor.r,
                feverColor.g,
                feverColor.b,
                0.16f + pulse * 0.32f);
        }
    }

    private void PlayLineFlash(int lineIndex, bool horizontal, Color color)
    {
        SpriteRenderer renderer = GetAvailableFlash();
        if (renderer == null)
            return;

        Transform flashTransform = renderer.transform;
        flashTransform.position = horizontal
            ? new Vector3(Board.Size * 0.5f, lineIndex + 0.5f, -0.08f)
            : new Vector3(lineIndex + 0.5f, Board.Size * 0.5f, -0.08f);
        flashTransform.rotation = horizontal
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, 90f);
        flashTransform.localScale = Vector3.zero;
        renderer.color = color;
        renderer.gameObject.SetActive(true);

        StartCoroutine(AnimateLineFlash(renderer));
    }

    private IEnumerator AnimateLineFlash(SpriteRenderer renderer)
    {
        float duration = Mathf.Max(0.08f, lineFlashDuration);
        Color color = renderer.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 4f));
            float thicknessPulse = Mathf.Sin(t * Mathf.PI);

            renderer.transform.localScale = new Vector3(
                Board.Size * 0.26f * grow,
                lineFlashThickness * (0.75f + thicknessPulse * 0.7f),
                1f);
            renderer.color = new Color(color.r, color.g, color.b, (1f - t) * 0.92f);
            yield return null;
        }

        renderer.gameObject.SetActive(false);
    }

    private SpriteRenderer GetAvailableFlash()
    {
        foreach (SpriteRenderer renderer in lineFlashPool)
        {
            if (!renderer.gameObject.activeSelf)
                return renderer;
        }

        return null;
    }

    private void EmitParticle(
        Vector3 position,
        Vector3 velocity,
        Color color,
        float size,
        float lifetime)
    {
        ParticleSystem.EmitParams emit = new()
        {
            position = position,
            velocity = velocity,
            startColor = Opaque(color),
            startSize = size,
            startLifetime = lifetime,
            rotation = Random.Range(0f, Mathf.PI * 2f)
        };
        clearParticles.Emit(emit, 1);
    }

    private static Vector3 CellCenter(Vector2Int point)
    {
        return new Vector3(point.x + 0.5f, point.y + 0.5f, -0.1f);
    }

    private static Texture2D CreateRoundedSquareTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Board Particle Square (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float radius = size * 0.18f;
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - (size * 0.5f - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.SmoothStep(radius - 1.5f, radius, distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateSoftFlashTexture(int width, int height)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            name = "Board Line Flash (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; ++y)
        {
            float vertical = 1f - Mathf.Abs((y + 0.5f) / height * 2f - 1f);
            vertical = Mathf.SmoothStep(0f, 1f, vertical);

            for (int x = 0; x < width; ++x)
            {
                float horizontal = 1f - Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                horizontal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(horizontal * 5f));
                pixels[y * width + x] = new Color(1f, 1f, 1f, vertical * horizontal);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Color Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private void OnDestroy()
    {
        if (particleMaterial != null)
            Destroy(particleMaterial);
        if (flashSprite != null)
            Destroy(flashSprite);
        if (particleTexture != null)
            Destroy(particleTexture);
        if (flashTexture != null)
            Destroy(flashTexture);
    }
}
