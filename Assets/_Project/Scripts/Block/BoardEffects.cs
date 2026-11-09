using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Danger Feedback")]
    [Tooltip("Board edge begins a gentle warning pulse at this danger score (0-100).")]
    [SerializeField, Range(0f, 100f)] private float dangerPulseThreshold = 58f;
    [Tooltip("Danger glow uses a slower heartbeat so the warning stays readable instead of stressful.")]
    [SerializeField, Min(0.1f)] private float dangerPulseSpeed = 2.4f;

    [Header("Score Reward")]
    [Tooltip("How long the floating score reward takes to rise and curve toward the score HUD.")]
    [SerializeField, Min(0.2f)] private float scoreFlyDuration = 0.82f;
    [Tooltip("Optional exact legacy score target. If unset, use the active authored Classic/Endless HUD, then fall back to TextScore.")]
    [SerializeField] private TextScore scoreHudTarget;
    [Tooltip("World-space TMP font size for each +points popup. Increase this if the numbers are hard to read on your device.")]
    [SerializeField, Min(8f)] private float floatingScoreFontSize = 36f;
    [Tooltip("Initial world-space scale of the floating score text when it pops from a cell.")]
    [SerializeField, Range(0.5f, 2f)] private float floatingScoreStartScale = 1.15f;
    [Tooltip("Final world-space scale as the score text reaches the HUD. Keep this above 0.7 for readability.")]
    [SerializeField, Range(0.3f, 2f)] private float floatingScoreEndScale = 0.9f;
    [Tooltip("Strength of the dark outline around floating numbers, which improves contrast over colorful blocks.")]
    [SerializeField, Range(0f, 0.5f)] private float floatingScoreOutlineWidth = 0.22f;
    [Tooltip("Extra upward arc height, in world units, before each score popup curves toward the HUD.")]
    [SerializeField, Range(0f, 3f)] private float floatingScoreArcHeight = 0.9f;

    private readonly List<SpriteRenderer> lineFlashPool = new();
    private readonly List<SpriteRenderer> previewGlowPool = new();
    private readonly HashSet<Vector2Int> clearPoints = new();

    private ParticleSystem clearParticles;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private Texture2D flashTexture;
    private Sprite flashSprite;
    private SpriteRenderer boardGlowRenderer;
    private SpriteRenderer comboGlowRing2;
    private SpriteRenderer comboGlowRing3;
    private SpriteRenderer feverGlowRenderer;
    private Image feverScreenDimmer;
    private Sprite feverOverlaySprite;
    private Transform cachedScoreTarget;
    private Canvas cachedScoreCanvas;
    private int comboCount;
    private float comboExpiresAt;
    [SerializeField, Tooltip("Configured by Gameplay Configuration. Authored levels always use move-based combos.")]
    private bool comboUsesTimeLimit;
    private float comboPulseEndsAt;
    private float feverEndsAt;
    private float feverCollapseEndsAt;
    private bool feverWasActive;
    private float dangerLevel;
    private float dangerRelaxUntil;
    private readonly List<Vector2Int> placementRipplePoints = new();

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
        PrepareFeverScreenDimmer();
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

        placementRipplePoints.Clear();
        placementRipplePoints.AddRange(placedPoints);
        if (placementRipplePoints.Count == 0)
            return;

        Vector2 center = Vector2.zero;
        foreach (Vector2Int point in placementRipplePoints)
            center += (Vector2)point + Vector2.one * 0.5f;
        center /= placementRipplePoints.Count;
        placementRipplePoints.Sort((a, b) =>
            (((Vector2)a + Vector2.one * 0.5f) - center).sqrMagnitude.CompareTo(
             (((Vector2)b + Vector2.one * 0.5f) - center).sqrMagnitude));

        StartCoroutine(PlayPlacementRipple(
            placementRipplePoints.ToArray(), center, Opaque(blockColor)));
    }

    private IEnumerator PlayPlacementRipple(Vector2Int[] points, Vector2 center, Color blockColor)
    {
        if (clearParticles == null)
            yield break;

        Color particleColor = Color.Lerp(blockColor, Color.white, 0.35f);
        foreach (Vector2Int point in points)
        {
            for (int i = 0; i < placementParticlesPerCell; ++i)
            {
                Vector2 radial = (Vector2)point + Vector2.one * 0.5f - center;
                Vector2 direction = radial.sqrMagnitude > 0.01f
                    ? radial.normalized
                    : Random.insideUnitCircle.normalized;
                EmitParticle(
                    CellCenter(point),
                    direction * Random.Range(0.24f, 0.62f),
                    particleColor,
                    Random.Range(0.07f, 0.12f),
                    Random.Range(0.18f, 0.28f));
            }
            // A short stagger makes the placement feel like a ripple from its center.
            yield return new WaitForSeconds(0.018f);
        }
    }

    public void PlayLineClear(
        IReadOnlyList<int> rows,
        IReadOnlyList<int> columns,
        Color effectColor,
        int clearedLineCount,
        bool feverStarted)
    {
        // The placement owns the clear's hue. Big-clear/Fever intensity still
        // comes from motion/count/size, never a replacement pink/gold/theme hue.
        effectColor = Opaque(effectColor);

        if (clearedLineCount == 2)
        {
            PlayDiagonalSweep(effectColor);
        }
        else
        {
            foreach (int row in rows)
                PlayLineFlash(row, true, effectColor, clearedLineCount);

            foreach (int column in columns)
                PlayLineFlash(column, false, effectColor, clearedLineCount);
        }

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

        Vector2 burstCenter = FindClearIntersectionCenter(rows, columns);
        if (clearedLineCount >= 4)
        {
            // A tiny anticipation beat before the largest burst gives the clear weight.
            StartCoroutine(PlayDelayedClearBurst(
                new List<Vector2Int>(clearPoints), burstCenter, effectColor, feverStarted));
        }
        else
        {
            EmitClearFragments(new List<Vector2Int>(clearPoints), burstCenter, effectColor, clearedLineCount, feverStarted);
        }
    }

    private void PlayDiagonalSweep(Color color)
    {
        SpriteRenderer renderer = GetAvailableFlash();
        if (renderer == null)
            return;

        Vector3 start = new(-0.8f, -0.8f, -0.08f);
        Vector3 end = new(Board.Size + 0.8f, Board.Size + 0.8f, -0.08f);
        renderer.transform.position = start;
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
        renderer.transform.localScale = new Vector3(Board.Size * 1.7f, lineFlashThickness * 1.3f, 1f);
        renderer.color = color;
        renderer.gameObject.SetActive(true);
        StartCoroutine(AnimateLineFlash(renderer, start, end, 2));
    }

    private Vector2 FindClearIntersectionCenter(IReadOnlyList<int> rows, IReadOnlyList<int> columns)
    {
        float x = (Board.Size - 1) * 0.5f;
        float y = (Board.Size - 1) * 0.5f;
        if (columns.Count > 0)
        {
            x = 0f;
            foreach (int column in columns)
                x += column;
            x /= columns.Count;
        }
        if (rows.Count > 0)
        {
            y = 0f;
            foreach (int row in rows)
                y += row;
            y /= rows.Count;
        }
        return new Vector2(x + 0.5f, y + 0.5f);
    }

    private IEnumerator PlayDelayedClearBurst(
        List<Vector2Int> points,
        Vector2 burstCenter,
        Color color,
        bool feverStarted)
    {
        yield return new WaitForSeconds(0.18f);
        EmitClearFragments(points, burstCenter, color, 4, feverStarted);
    }

    private void EmitClearFragments(
        IReadOnlyList<Vector2Int> points,
        Vector2 burstCenter,
        Color color,
        int clearedLineCount,
        bool feverStarted)
    {
        PopEffectDefinition style = PopGameplaySession.Instance?.SelectedEffect;
        int particleCount = Mathf.Clamp(Mathf.RoundToInt(clearParticlesPerCell * (style?.particleMultiplier ?? 1f)), 1, 30);
        float speed = clearParticleSpeed * (style?.speedMultiplier ?? 1f);
        float lifetime = clearParticleLifetime * (style?.lifetimeMultiplier ?? 1f);
        // Cosmetic styles may change fragment shape/speed/lifetime, not the
        // hue shared by preview, dissolving cells and the placed block.
        foreach (Vector2Int point in points)
        {
            for (int i = 0; i < particleCount; ++i)
            {
                Vector2 direction = clearedLineCount >= 3 || style != null && style.radialBurst
                    ? (Vector2)point + Vector2.one * 0.5f - burstCenter
                    : Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Random.insideUnitCircle;
                direction.Normalize();

                EmitParticle(
                    CellCenter(point),
                    direction * Random.Range(speed * 0.45f, speed) *
                        (clearedLineCount >= 4 || feverStarted ? 1.35f : 1f),
                    Color.Lerp(color, Color.white, Random.Range(0f, 0.35f)),
                    Random.Range(particleSizeRange.x, particleSizeRange.y),
                    Random.Range(lifetime * 0.7f, lifetime));
            }
        }

        if (feverStarted)
        {
            GetComponentInParent<Board>()?.PlayFeverPulse(feverColor);
            PlayFeverBurst(color);
        }
    }

    /// <summary>Computes a small scan delay so clear cells dissolve in a readable pattern.</summary>
    public float GetClearWaveDelay(
        Vector2Int point,
        IReadOnlyList<int> rows,
        IReadOnlyList<int> columns)
    {
        int lineCount = rows.Count + columns.Count;
        if (lineCount <= 0)
            return 0f;

        if (lineCount == 1)
        {
            float alongLine = rows.Count == 1 ? point.x : point.y;
            return alongLine * 0.018f;
        }

        if (lineCount == 2)
            return 0.04f + Mathf.Abs(point.x - point.y) * 0.018f;

        Vector2 center = new((Board.Size - 1) * 0.5f, (Board.Size - 1) * 0.5f);
        float radius = Vector2.Distance((Vector2)point, center);
        return lineCount >= 4
            ? 0.28f - radius * 0.014f
            : 0.04f + radius * 0.012f;
    }

    public void PlayScoreReward(int points, Vector3 worldOrigin, Color accent, float startDelay = 0f)
    {
        if (points <= 0)
            return;

        if (Time.time < feverEndsAt)
            accent = feverColor;

        GameObject rewardObject = new("Floating Score Reward", typeof(TextMeshPro));
        rewardObject.transform.position = worldOrigin + Vector3.back * 0.15f;
        TextMeshPro text = rewardObject.GetComponent<TextMeshPro>();
        text.text = $"+{points:N0}";
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = floatingScoreFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.Lerp(accent, Color.white, 0.35f);
        text.outlineWidth = floatingScoreOutlineWidth;
        text.outlineColor = new Color(0.02f, 0.05f, 0.12f, 0.8f);
        text.sortingOrder = 40;
        StartCoroutine(AnimateScoreReward(rewardObject.transform, text, worldOrigin, startDelay));
    }

    private IEnumerator AnimateScoreReward(Transform target, TextMeshPro text, Vector3 origin, float startDelay)
    {
        float wait = Mathf.Max(0f, startDelay);
        while (wait > 0f)
        {
            wait -= Time.unscaledDeltaTime;
            yield return null;
        }

        Vector3 start = origin + Vector3.up * 0.35f;
        Vector3 end = start + Vector3.up * 1.2f;
        Camera camera = Camera.main;
        RectTransform authoredScore = scoreHudTarget == null && PopGameplayUI.Instance != null ? PopGameplayUI.Instance.ScoreRewardTarget : null;
        if (authoredScore != null && cachedScoreTarget != authoredScore)
        { cachedScoreTarget = authoredScore; cachedScoreCanvas = authoredScore.GetComponentInParent<Canvas>(); }
        if (cachedScoreTarget == null)
        {
            TextScore scoreHud = scoreHudTarget != null
                ? scoreHudTarget
                : TextScore.CurrentScoreDisplay != null
                    ? TextScore.CurrentScoreDisplay
                    : FindFirstObjectByType<TextScore>();
            if (scoreHud != null)
            {
                cachedScoreTarget = scoreHud.transform;
                cachedScoreCanvas = scoreHud.GetComponentInParent<Canvas>();
            }
        }

        if (camera != null && cachedScoreTarget != null)
        {
            Camera uiCamera = cachedScoreCanvas != null && cachedScoreCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? cachedScoreCanvas.worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, cachedScoreTarget.position);
            float depth = Mathf.Abs(camera.transform.position.z - origin.z);
            end = camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        }

        float elapsed = 0f;
        Color startColor = text.color;
        while (elapsed < scoreFlyDuration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreFlyDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 control = Vector3.Lerp(start, end, 0.5f) + Vector3.up * floatingScoreArcHeight;
            target.position = (1f - eased) * (1f - eased) * start +
                              2f * (1f - eased) * eased * control + eased * eased * end;
            target.localScale = Vector3.one * Mathf.Lerp(floatingScoreStartScale, floatingScoreEndScale, eased);
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t * 0.45f);
            yield return null;
        }

        if (target != null)
            Destroy(target.gameObject);
    }

    public void SetDangerLevel(float value)
    {
        dangerLevel = Mathf.Clamp(value, 0f, 100f);
    }

    public void NotifyValidMove()
    {
        dangerRelaxUntil = Time.time + 1.1f;
    }

    private void PlayFeverBurst(Color color)
    {
        if (clearParticles == null)
            return;

        Vector2 center = Vector2.one * (Board.Size * 0.5f);
        for (int i = 0; i < 48; ++i)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            EmitParticle(center, direction * Random.Range(1.4f, 2.8f),
                Color.Lerp(color, Color.white, Random.Range(0.25f, 0.75f)),
                Random.Range(0.12f, 0.24f), Random.Range(0.42f, 0.70f));
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
        comboGlowRing2 = CreateBoardGlowRenderer("Combo Border Ring 2", boardRenderer, -4, 2f);
        comboGlowRing3 = CreateBoardGlowRenderer("Combo Border Ring 3", boardRenderer, -5, 3.2f);
        feverGlowRenderer = CreateBoardGlowRenderer("Fever Board Glow", boardRenderer, -3);
        boardGlowRenderer.gameObject.SetActive(false);
        comboGlowRing2.gameObject.SetActive(false);
        comboGlowRing3.gameObject.SetActive(false);
        feverGlowRenderer.gameObject.SetActive(false);
    }

    private void PrepareFeverScreenDimmer()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        feverOverlaySprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        GameObject overlay = new("Fever Screen Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsFirstSibling();
        RectTransform rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        feverScreenDimmer = overlay.GetComponent<Image>();
        feverScreenDimmer.sprite = feverOverlaySprite;
        feverScreenDimmer.color = new Color(0.025f, 0.03f, 0.12f, 0f);
        feverScreenDimmer.raycastTarget = false;
    }

    private SpriteRenderer CreateBoardGlowRenderer(
        string objectName,
        SpriteRenderer source,
        int sortingOrder,
        float paddingScale = 1f)
    {
        GameObject glowObject = new(objectName);
        glowObject.transform.SetParent(boardGlowRoot, false);

        SpriteRenderer renderer = glowObject.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.drawMode = source.drawMode;
        renderer.size = source.size + Vector2.one * boardGlowPadding * 2f * paddingScale;
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
        if (comboUsesTimeLimit && Time.time > comboExpiresAt)
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

    public void ConfigureProgression(PopRunSettings settings, bool authored)
    {
        comboUsesTimeLimit = !authored && settings.timedCombo;
        comboResetDelay = settings.timedComboWindowSeconds;
        feverStartCombo = settings.feverStartCombo;
        feverDuration = settings.feverVisualDurationSeconds;
    }

    public float ComboRemainingSeconds => comboUsesTimeLimit ? Mathf.Max(0f, comboExpiresAt - Time.time) : 0f;
    public void RestoreCombo(int count, float remainingSeconds)
    {
        comboCount = Mathf.Max(0, count);
        if (comboUsesTimeLimit && remainingSeconds <= 0f) comboCount = 0;
        comboExpiresAt = Time.time + Mathf.Max(0f, remainingSeconds);
        comboPulseEndsAt = feverEndsAt = feverCollapseEndsAt = 0f;
    }

    private void UpdateBoardGlow()
    {
        if (boardGlowRenderer == null || feverGlowRenderer == null)
            return;

        bool feverActive = Time.time < feverEndsAt;
        bool comboActive = Time.time < comboPulseEndsAt;
        bool dangerActive = dangerLevel >= dangerPulseThreshold && Time.time >= dangerRelaxUntil;

        if (feverActive && !feverWasActive)
            feverGlowRenderer.transform.localScale = Vector3.one;
        if (feverWasActive && !feverActive)
            feverCollapseEndsAt = Time.time + 0.34f;
        feverWasActive = feverActive;
        bool feverCollapsing = !feverActive && Time.time < feverCollapseEndsAt;

        boardGlowRenderer.gameObject.SetActive(comboActive || feverActive);
        comboGlowRing2.gameObject.SetActive(comboActive || feverActive);
        comboGlowRing3.gameObject.SetActive(comboActive || feverActive);
        feverGlowRenderer.gameObject.SetActive(feverActive || dangerActive || feverCollapsing);

        if (comboActive || feverActive)
        {
            float comboT = Mathf.Clamp01((comboCount - 1f) / Mathf.Max(1f, feverStartCombo - 1f));
            Color color = Color.Lerp(comboStartColor, comboPeakColor, comboT);
            float glowSpeed = feverActive ? 18f : comboCount >= 3 ? 14f : 10f;
            float pulse = 0.70f + Mathf.Sin(Time.time * glowSpeed) * 0.30f;
            float alpha = feverActive ? 0.60f : 0.20f + pulse * 0.38f;
            boardGlowRenderer.color = new Color(color.r, color.g, color.b, alpha);

            float ring2Pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed - 0.8f);
            float ring3Pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed - 1.6f);
            float comboStrength = Mathf.InverseLerp(1f, 5f, comboCount);
            Color ringColor = feverActive ? feverColor : color;
            comboGlowRing2.color = new Color(ringColor.r, ringColor.g, ringColor.b,
                (feverActive ? 0.20f : 0.05f + comboStrength * 0.17f) * ring2Pulse);
            comboGlowRing3.color = new Color(ringColor.r, ringColor.g, ringColor.b,
                (feverActive ? 0.12f : 0.02f + comboStrength * 0.10f) * ring3Pulse);
            comboGlowRing2.transform.localScale = Vector3.one * (1f + ring2Pulse * 0.025f);
            comboGlowRing3.transform.localScale = Vector3.one * (1f + ring3Pulse * 0.045f);
        }
        else
        {
            comboGlowRing2.transform.localScale = Vector3.one;
            comboGlowRing3.transform.localScale = Vector3.one;
        }

        if (feverActive)
        {
            float pulse = 0.60f + Mathf.Sin(Time.time * 8f) * 0.40f;
            feverGlowRenderer.color = new Color(
                feverColor.r,
                feverColor.g,
                feverColor.b,
                0.16f + pulse * 0.32f);
            if (feverScreenDimmer != null)
                feverScreenDimmer.color = new Color(0.025f, 0.03f, 0.12f, 0.10f + pulse * 0.06f);
        }
        else if (dangerActive)
        {
            float amount = Mathf.InverseLerp(dangerPulseThreshold, 100f, dangerLevel);
            float heartbeat = 0.5f + 0.5f * Mathf.Sin(Time.time * dangerPulseSpeed * Mathf.Lerp(1f, 1.8f, amount));
            Color dangerColor = Color.Lerp(new Color(1f, 0.55f, 0.12f), new Color(1f, 0.12f, 0.22f), amount);
            feverGlowRenderer.color = new Color(dangerColor.r, dangerColor.g, dangerColor.b,
                0.08f + heartbeat * Mathf.Lerp(0.16f, 0.32f, amount));
        }
        else if (feverCollapsing)
        {
            float t = 1f - Mathf.Clamp01((feverCollapseEndsAt - Time.time) / 0.34f);
            feverGlowRenderer.transform.localScale = Vector3.one * (1f - t);
            feverGlowRenderer.color = new Color(feverColor.r, feverColor.g, feverColor.b, 0.48f * (1f - t));
            if (feverScreenDimmer != null)
                feverScreenDimmer.color = new Color(0.025f, 0.03f, 0.12f, 0.16f * (1f - t));
        }
        else
        {
            feverGlowRenderer.transform.localScale = Vector3.one;
            if (feverScreenDimmer != null)
                feverScreenDimmer.color = new Color(0.025f, 0.03f, 0.12f, 0f);
        }
    }

    private void PlayLineFlash(int lineIndex, bool horizontal, Color color, int clearedLineCount)
    {
        SpriteRenderer renderer = GetAvailableFlash();
        if (renderer == null)
            return;

        Transform flashTransform = renderer.transform;
        Vector3 start = horizontal
            ? new Vector3(-0.8f, lineIndex + 0.5f, -0.08f)
            : new Vector3(lineIndex + 0.5f, -0.8f, -0.08f);
        Vector3 end = horizontal
            ? new Vector3(Board.Size + 0.8f, lineIndex + 0.5f, -0.08f)
            : new Vector3(lineIndex + 0.5f, Board.Size + 0.8f, -0.08f);

        // Multi-line clears get a longer, brighter sweep; 3+ lines also flash the center more dramatically.
        float sweepLength = clearedLineCount >= 3 ? 2.1f : 1.55f;
        if (clearedLineCount == 2)
            sweepLength *= 1.18f;
        flashTransform.position = start;
        flashTransform.rotation = horizontal
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, 90f);
        flashTransform.localScale = new Vector3(sweepLength, lineFlashThickness, 1f);
        renderer.color = color;
        renderer.gameObject.SetActive(true);

        StartCoroutine(AnimateLineFlash(renderer, start, end, clearedLineCount));
    }

    private IEnumerator AnimateLineFlash(SpriteRenderer renderer, Vector3 start, Vector3 end, int clearedLineCount)
    {
        float duration = Mathf.Max(0.08f, lineFlashDuration) *
            (clearedLineCount >= 3 ? 1.15f : 1f);
        Color color = renderer.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float travel = Mathf.SmoothStep(0f, 1f, t);
            float thicknessPulse = Mathf.Sin(t * Mathf.PI);

            renderer.transform.position = Vector3.Lerp(start, end, travel);
            renderer.transform.localScale = new Vector3(
                renderer.transform.localScale.x,
                lineFlashThickness * (0.8f + thicknessPulse * (clearedLineCount >= 3 ? 1f : 0.65f)),
                1f);
            renderer.color = new Color(color.r, color.g, color.b, (1f - t) * 0.98f);
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
        if (feverOverlaySprite != null)
            Destroy(feverOverlaySprite);
    }
}
