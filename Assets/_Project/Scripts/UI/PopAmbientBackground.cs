using UnityEngine;

/// <summary>Authored colour/particle layers with render-only POP. Gameplay transforms, input coordinates and RNG stay intact.</summary>
[ExecuteAlways, DefaultExecutionOrder(50)]
public sealed partial class PopAmbientBackground : MonoBehaviour
{
    [Header("Authored scene objects — no runtime hierarchy generation")]
    public PopAmbientConfiguration configuration;
    public SpriteRenderer layerA, layerB;
    [Tooltip("Existing rectangular sprite is used only as geometry. Its image is NOT drawn by the colour shader.")]
    public Sprite colorSurface;
    public ParticleSystem snow, stars, fireflies, transitionBurst;
    [Header("Camera-fit colour surface")]
    public Vector2 artScale = Vector2.one;
    public Vector2 artOffset;
    [Header("Edit-mode preview only (0 = default; 1..N = scenes)")]
    [Min(0)] public int scenePreview;
    public bool animatePreview;
    [Tooltip("Scrub the POP transition before Play: 0 = beginning, 1 = settled. Use the preview button to play it.")]
    [Range(0, 1)] public float refreshPreview = 1;

    private Camera targetCamera;
    private ModernBackground defaultBackground;
    private bool initialized, changing, returning;
    private int activeLayer;
    private string currentId = "", targetId = "";
    private float elapsed, duration, returnAlphaA, returnAlphaB;
#if UNITY_EDITOR
    private int lastPreview = -1;
    private int lastPreviewRevision = -1;
#endif
    private bool lastReducedMotion;
    private float lastDensity = -1;
    private double previewAt;
    private readonly Vector2[] effectViewSizes = new Vector2[4];
    private MaterialPropertyBlock layerProperties;
    private static readonly int Top = Shader.PropertyToID("_Top"), Bottom = Shader.PropertyToID("_Bottom"), Glow = Shader.PropertyToID("_Glow"), GlowStrength = Shader.PropertyToID("_GlowStrength"), AmbientTime = Shader.PropertyToID("_AmbientTime");
    public string CurrentSceneId => currentId;
    public bool IsTransitioning => changing;
    public bool IsReady => initialized;
    public bool HasPresentation => configuration != null && colorSurface != null && layerA != null && layerB != null && snow != null && stars != null && fireflies != null && transitionBurst != null;

    private void OnEnable()
    {
        targetCamera = GetComponentInParent<Camera>();
        defaultBackground = targetCamera != null ? targetCamera.GetComponent<ModernBackground>() : null;
        initialized = changing = returning = false; activeLayer = 0; currentId = targetId = "";
#if UNITY_EDITOR
        lastPreview = -1;
        lastPreviewRevision = -1;
        previewPlayback = previewWasTransition = false; lastRefreshPreview = -1;
#endif
        if (Application.isPlaying) { ClearLayers(); StopEffects(); }
        HookPresentation();
    }
    private void OnDisable()
    {
        StopEffects(); ClearLayers();
        UnhookPresentation();
        if (defaultBackground != null) defaultBackground.SetDefaultEffectsVisible(true);
    }
    private void Update()
    {
        if (!HasPresentation || targetCamera == null) return;
        if (!Application.isPlaying) { Preview(); return; }
        if (changing && BlockPointerInput.IsDragging) bounceSuppressed = true;
        if (changing && configuration.reducedMotion) duration = 0;
        if (initialized && (lastReducedMotion != configuration.reducedMotion || !Mathf.Approximately(lastDensity, configuration.particleDensity)))
        {
            var scene = configuration.Find(changing && !returning ? targetId : currentId);
            if (scene != null) StartEffect(scene, false); else { StopEffects(); DefaultEffects(true); }
        }
        lastReducedMotion = configuration.reducedMotion; lastDensity = configuration.particleDensity;
        var session = PopGameplaySession.Instance;
        var manager = GameFlowManager._instance;
        // Board initialization restores the save after scene activation. Do not
        // mistake that first empty frame for a new default-to-theme milestone.
        if (!initialized && (session == null || !session.IsInitialized)) return;
        string desired = session != null && session.HasScoreAmbience && (manager == null || !manager.IsGameOver || manager.IsAwaitingRevive) ? session.Ambience.sceneId : "";
        if (configuration.Find(desired) == null) desired = "";
        if (!initialized)
        {
            initialized = true; ShowImmediately(desired); // Resume never replays milestone confetti or rerolls.
        }
        else if (desired.Length == 0 && (currentId.Length > 0 || changing && targetId.Length > 0)) BeginDefault();
        else if (!changing && desired != currentId) BeginScene(desired);
        AdvanceTransition(returning ? Time.unscaledDeltaTime : Time.deltaTime);
    }
    private void AdvanceTransition(float delta)
    {
        if (changing)
        {
            elapsed += delta;
            float t = duration <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            if (returning)
            {
                SetAlpha(layerA, returnAlphaA * (1 - t)); SetAlpha(layerB, returnAlphaB * (1 - t));
            }
            else SetReveal(Incoming, 1 - Mathf.Pow(1 - (duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration)), 3));
            if (elapsed >= duration)
            {
                if (returning) { ClearLayers(); DefaultEffects(true); }
                else { SetAlpha(Outgoing, 0); Outgoing.sprite = null; activeLayer = 1 - activeLayer; }
                currentId = targetId; changing = returning = false;
            }
        }
    }
    private SpriteRenderer Outgoing => activeLayer == 0 ? layerA : layerB;
    private SpriteRenderer Incoming => activeLayer == 0 ? layerB : layerA;
    private void BeginScene(string id)
    {
        var scene = configuration.Find(id); if (scene == null) return;
        changing = true; returning = false; targetId = id; elapsed = 0; duration = configuration.reducedMotion ? 0 : configuration.crossfadeSeconds;
        bounceSuppressed = false;
        Paint(Incoming, scene, 1); SetReveal(Incoming, 0);
        // Sorting, not hierarchy order, decides which of the two crossfade layers is on top.
        Outgoing.sortingOrder = -995; Incoming.sortingOrder = -994;
        DefaultEffects(false); StartEffect(scene, true);
    }
    private void BeginDefault()
    {
        if (returning) return;
        changing = returning = true; targetId = ""; elapsed = 0; duration = configuration.reducedMotion ? 0 : configuration.returnToDefaultSeconds;
        returnAlphaA = layerA.color.a; returnAlphaB = layerB.color.a; StopEffects();
    }
    private void ShowImmediately(string id)
    {
        ClearLayers(); StopEffects(); changing = returning = false; activeLayer = 0; currentId = targetId = id;
        var scene = configuration.Find(id);
        if (scene != null) { Paint(layerA, scene, 1); StartEffect(scene, false); }
        DefaultEffects(scene == null);
    }
    private void DefaultEffects(bool visible)
    {
        if (defaultBackground != null) defaultBackground.SetDefaultEffectsVisible(visible && !configuration.reducedMotion && configuration.particleDensity > 0);
    }
    private void Paint(SpriteRenderer renderer, PopAmbientSceneDefinition scene, float alpha)
    {
        renderer.sprite = colorSurface; renderer.color = new Color(1, 1, 1, alpha);
        layerProperties ??= new MaterialPropertyBlock(); renderer.GetPropertyBlock(layerProperties);
        layerProperties.SetColor(Top, scene.topColor); layerProperties.SetColor(Bottom, scene.bottomColor);
        layerProperties.SetColor(Glow, scene.glowColor); layerProperties.SetFloat(GlowStrength, scene.glowStrength);
        layerProperties.SetFloat("_Reveal", 1); layerProperties.SetFloat("_RingWidth", configuration.refreshRingWidth);
        layerProperties.SetFloat("_RingGlow", configuration.reducedMotion ? 0 : configuration.refreshRingGlow);
        renderer.SetPropertyBlock(layerProperties);
    }
    private void StartEffect(PopAmbientSceneDefinition scene, bool burst)
    {
        StopEffects(); if (configuration.reducedMotion || configuration.particleDensity <= 0) return;
        ParticleSystem effect = scene.effect == PopAmbientEffect.Snow ? snow : scene.effect == PopAmbientEffect.Stars ? stars : fireflies;
        var main = effect.main;
        main.startColor = new Color(scene.particleColor.r, scene.particleColor.g, scene.particleColor.b, scene.particleAlpha);
        main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(scene.maxParticles * configuration.particleDensity), 0, 150);
        main.startLifetime = Mathf.Max(.1f, scene.particleLifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(.01f, scene.particleSize.x), Mathf.Max(.01f, scene.particleSize.y));
        var emission = effect.emission; emission.rateOverTime = scene.emissionPerSecond * configuration.particleDensity;
        var velocity = effect.velocityOverLifetime; velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-scene.horizontalDrift, scene.horizontalDrift); velocity.y = new ParticleSystem.MinMaxCurve(scene.verticalSpeed.x, scene.verticalSpeed.y); velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
        var noise = effect.noise; noise.enabled = scene.turbulence > 0; noise.strength = scene.turbulence;
        // Explicit seeds prevent ParticleSystem auto-seeding from sharing Unity Random with piece colors.
        effect.useAutoRandomSeed = false; effect.randomSeed = 0x46A71u + (uint)scene.effect * 137u;
        if (Application.isPlaying) { effect.Play(); effect.Simulate(2, false, false, true); effect.Play(); }
        if (burst && configuration.transitionBurstCount > 0) EmitRefreshBurst(scene);
    }
    private void StopEffects()
    {
        Stop(snow); Stop(stars); Stop(fireflies); Stop(transitionBurst);
    }
    private static void Stop(ParticleSystem effect) { if (effect != null) effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
    private static void SetAlpha(SpriteRenderer renderer, float alpha) { Color c = renderer.color; c.a = alpha; renderer.color = c; }
    private void ClearLayers() { if (layerA != null) { SetAlpha(layerA, 0); layerA.sprite = null; } if (layerB != null) { SetAlpha(layerB, 0); layerB.sprite = null; } }
    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.orthographic || !HasPresentation) return;
        float h = targetCamera.orthographicSize * 2, w = h * targetCamera.aspect;
        FitArt(layerA, w, h); FitArt(layerB, w, h);
        FitEffect(snow, 0, w, h, true); FitEffect(stars, 1, w, h, false); FitEffect(fireflies, 2, w, h, false);
        FitEffect(transitionBurst, 3, w, h, false);
    }
    private void FitArt(SpriteRenderer renderer, float width, float height)
    {
        if (renderer.sprite == null) return;
        Vector3 size = renderer.sprite.bounds.size;
        // Overscan covers the zoom-out rebound too, so the old blue cannot peek
        // through as thin strips at the screen edges during the second beat.
        float fit = Mathf.Max(width / size.x, height / size.y) * (1.03f + configuration.screenBounce);
        renderer.transform.localScale = new Vector3(fit * Mathf.Max(.1f, artScale.x), fit * Mathf.Max(.1f, artScale.y), 1);
        renderer.transform.localPosition = new Vector3(artOffset.x, artOffset.y, 20);
        layerProperties ??= new MaterialPropertyBlock(); renderer.GetPropertyBlock(layerProperties);
        layerProperties.SetFloat(AmbientTime, configuration.reducedMotion ? 0 : Application.isPlaying ? Time.time : (float)previewAt);
        layerProperties.SetFloat("_Aspect", size.x / Mathf.Max(.001f, size.y));
        renderer.SetPropertyBlock(layerProperties);
    }
    private void FitEffect(ParticleSystem effect, int index, float width, float height, bool top)
    {
        var size = new Vector2(width, height); if ((effectViewSizes[index] - size).sqrMagnitude < .001f) return;
        effectViewSizes[index] = size; var shape = effect.shape;
        shape.position = top ? new Vector3(0, height * .5f + .3f, 0) : Vector3.zero;
        shape.scale = top ? new Vector3(width + 1, .1f, 0) : new Vector3(width, height, 0);
    }
    private void Preview()
    {
#if UNITY_EDITOR
        if (HandleRefreshPreview()) return;
        if (lastPreview != scenePreview || lastPreviewRevision != configuration.PreviewRevision)
        {
            lastPreview = scenePreview; lastPreviewRevision = configuration.PreviewRevision; int index = scenePreview - 1;
            var scene = index >= 0 && index < configuration.scenes.Count ? configuration.scenes[index] : null;
            ShowImmediately(scene != null && scene.enabled ? scene.id : "");
            ParticleSystem effect = scene?.effect == PopAmbientEffect.Snow ? snow : scene?.effect == PopAmbientEffect.Stars ? stars : fireflies;
            if (scene != null && scene.enabled && !configuration.reducedMotion && configuration.particleDensity > 0) effect.Simulate(5, false, true, true);
            previewAt = UnityEditor.EditorApplication.timeSinceStartup;
        }
        if (animatePreview && scenePreview > 0)
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup; float dt = Mathf.Clamp((float)(now - previewAt), 0, .05f); previewAt = now;
            int index = scenePreview - 1;
            if (index >= 0 && index < configuration.scenes.Count && configuration.scenes[index] != null && configuration.scenes[index].enabled && !configuration.reducedMotion && configuration.particleDensity > 0)
            {
                var scene = configuration.scenes[index]; var ps = scene.effect == PopAmbientEffect.Snow ? snow : scene.effect == PopAmbientEffect.Stars ? stars : fireflies;
                ps.Simulate(dt, false, false, true);
            }
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); UnityEditor.SceneView.RepaintAll();
        }
        if (scenePreview > 0 && scenePreview <= configuration.scenes.Count)
        {
            var scene = configuration.scenes[scenePreview - 1];
            if (scene != null && scene.enabled) Paint(layerA, scene, 1);
        }
#endif
    }
}
