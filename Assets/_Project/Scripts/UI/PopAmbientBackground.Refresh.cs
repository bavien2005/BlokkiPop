using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class PopAmbientBackground
{
    private bool projectionApplied, bounceSuppressed, originalAutomaticProjection;
    private Matrix4x4 originalProjection;
    public float TransitionProgress => changing && !returning && duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
    public float PresentationBounce => changing && !returning && configuration != null && !configuration.reducedMotion && !bounceSuppressed &&
        (!Application.isPlaying || Time.timeScale > 0 && !BlockPointerInput.IsDragging)
        ? BounceAt(TransitionProgress, configuration.bounceCount) : 0;

    // Two squash/rebound beats, exactly zero at both ends. No random shaking.
    public static float BounceAt(float progress, int count) => progress <= 0 || progress >= 1 ? 0 :
        Mathf.Sin(progress * Mathf.PI * (Mathf.Clamp(count, 1, 3) * 2 + 1)) * (1 - progress) * (1 - progress);

    private void HookPresentation()
    {
        UnhookPresentation();
        Camera.onPreCull += OnCameraBegin; Camera.onPostRender += OnCameraEnd;
        RenderPipelineManager.beginCameraRendering += OnPipelineBegin; RenderPipelineManager.endCameraRendering += OnPipelineEnd;
    }
    private void UnhookPresentation()
    {
        RestoreProjection();
        Camera.onPreCull -= OnCameraBegin; Camera.onPostRender -= OnCameraEnd;
        RenderPipelineManager.beginCameraRendering -= OnPipelineBegin; RenderPipelineManager.endCameraRendering -= OnPipelineEnd;
    }
    private void OnPipelineBegin(ScriptableRenderContext context, Camera camera) => OnCameraBegin(camera);
    private void OnPipelineEnd(ScriptableRenderContext context, Camera camera) => OnCameraEnd(camera);
    private void OnCameraBegin(Camera camera)
    {
        if (camera != targetCamera || projectionApplied || !HasPresentation) return;
        float bounce = PresentationBounce;
        if (Mathf.Abs(bounce) < .00001f) return;
        originalProjection = camera.projectionMatrix; projectionApplied = true;
        camera.ResetProjectionMatrix();
        originalAutomaticProjection = MatricesMatch(originalProjection, camera.projectionMatrix);
        var pulse = Matrix4x4.identity;
        pulse.m00 = pulse.m11 = 1 + configuration.screenBounce * bounce;
        pulse.m13 = configuration.screenLift * bounce * 2;
        // Render-only: camera position, orthographic size, board and colliders stay put.
        // Restore before pointer input / raycasts on the next frame.
        camera.projectionMatrix = pulse * originalProjection;
    }
    private void OnCameraEnd(Camera camera) { if (camera == targetCamera) RestoreProjection(); }
    private void RestoreProjection()
    {
        if (projectionApplied && targetCamera != null)
        {
            if (originalAutomaticProjection) targetCamera.ResetProjectionMatrix();
            else targetCamera.projectionMatrix = originalProjection;
        }
        projectionApplied = false;
    }
    private static bool MatricesMatch(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > .00001f) return false;
        return true;
    }
    private void SetReveal(SpriteRenderer renderer, float progress)
    {
        layerProperties ??= new MaterialPropertyBlock(); renderer.GetPropertyBlock(layerProperties);
        layerProperties.SetFloat("_Reveal", progress); renderer.SetPropertyBlock(layerProperties);
    }
    private void EmitRefreshBurst(PopAmbientSceneDefinition scene)
    {
        transitionBurst.useAutoRandomSeed = false; transitionBurst.randomSeed = 0xB10CC1u;
        int count = Mathf.Clamp(Mathf.RoundToInt(configuration.transitionBurstCount * configuration.particleDensity), 0, 100);
        var main = transitionBurst.main; main.maxParticles = Mathf.Max(1, count); main.gravityModifier = .45f;
        var velocity = transitionBurst.velocityOverLifetime; velocity.enabled = false;
        float h = targetCamera != null ? targetCamera.orthographicSize * 2 : 18, w = h * (targetCamera != null ? targetCamera.aspect : .5f);
        transitionBurst.Play(false);
        for (int i = 0; i < count; i++)
        {
            float a = ((i * 37 + 11) % 101) / 100f, b = ((i * 61 + 7) % 103) / 102f, side = i % 2 == 0 ? -1 : 1;
            var particle = new ParticleSystem.EmitParams
            {
                position = new Vector3(side * w * (.43f + .025f * a), -h * (.22f + .06f * b), 0),
                velocity = new Vector3(-side * (.12f + .25f * b), .6f + .5f * a, 0) * configuration.transitionBurstSpeed,
                startSize = Mathf.Lerp(configuration.transitionBurstSize.x, configuration.transitionBurstSize.y, b),
                startLifetime = configuration.transitionBurstLifetime * Mathf.Lerp(.75f, 1, a),
                startColor = Color.Lerp(scene.particleColor, new Color(1, .86f, .3f, 1), b),
                rotation = a * 360
            };
            transitionBurst.Emit(particle, 1);
        }
    }

#if UNITY_EDITOR
    private bool previewPlayback, previewWasTransition;
    private float lastRefreshPreview = -1;
    private double refreshPreviewAt;
    public void PlayRefreshPreview()
    {
        if (Application.isPlaying || !HasPresentation) return;
        if (scenePreview <= 0 || scenePreview > configuration.scenes.Count) scenePreview = 2;
        refreshPreview = 0; previewPlayback = true; lastRefreshPreview = -1;
        refreshPreviewAt = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
    private bool HandleRefreshPreview()
    {
        if (previewPlayback)
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            refreshPreview = Mathf.Min(1, refreshPreview + Mathf.Clamp((float)(now - refreshPreviewAt), 0, .05f) / Mathf.Max(.1f, configuration.crossfadeSeconds));
            refreshPreviewAt = now;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); UnityEditor.SceneView.RepaintAll();
            if (refreshPreview >= 1) previewPlayback = false;
        }
        if (refreshPreview >= 1 || scenePreview <= 0 || scenePreview > configuration.scenes.Count)
        {
            if (previewWasTransition) { lastPreview = -1; previewWasTransition = false; }
            return false;
        }
        var scene = configuration.scenes[scenePreview - 1];
        if (scene == null || !scene.enabled) return false;
        if (lastRefreshPreview != refreshPreview || lastPreview != scenePreview || lastPreviewRevision != configuration.PreviewRevision)
        {
            previewWasTransition = true;
            ShowImmediately(""); BeginScene(scene.id); AdvanceTransition(duration * refreshPreview);
            var effect = scene.effect == PopAmbientEffect.Snow ? snow : scene.effect == PopAmbientEffect.Stars ? stars : fireflies;
            if (!configuration.reducedMotion && configuration.particleDensity > 0)
            { effect.Simulate(2 + elapsed, false, true, true); transitionBurst.Simulate(elapsed, false, false, true); }
            lastRefreshPreview = refreshPreview; lastPreview = scenePreview; lastPreviewRevision = configuration.PreviewRevision;
        }
        return true;
    }
#endif
}
