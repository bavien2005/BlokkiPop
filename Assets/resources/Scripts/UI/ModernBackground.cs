using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ModernBackground : MonoBehaviour
{
    [SerializeField] private Sprite backgroundSprite;

    [SerializeField, Min(0f)] private float driftAmount = 0.12f;

    [SerializeField, Min(0f)] private float driftSpeed = 0.08f;

    private readonly List<Object> runtimeResources = new();

    private Camera targetCamera;
    private Transform backgroundTransform;
    private Vector3 backgroundBaseScale = Vector3.one;
    private ParticleSystem fallingLights;
    private ParticleSystem floatingDiamonds;
    private float previousViewWidth = -1f;
    private float previousViewHeight = -1f;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.backgroundColor = new Color(0.008f, 0.016f, 0.07f, 1f);

        CreateBackground();
        fallingLights = CreateFallingLights();
        floatingDiamonds = CreateFloatingDiamonds();
        UpdateLayout(true);
    }

    private void LateUpdate()
    {
        UpdateLayout(false);

        if (backgroundTransform == null) return;

        var time = Time.unscaledTime * driftSpeed;
        var pulse = 1f + Mathf.Sin(time * 0.73f) * 0.004f;

        backgroundTransform.localPosition = new Vector3(
            Mathf.Sin(time) * driftAmount,
            Mathf.Cos(time * 0.67f) * driftAmount * 0.55f,
            20f);
        backgroundTransform.localScale = backgroundBaseScale * pulse;
    }

    private void OnDestroy()
    {
        foreach (var resource in runtimeResources)
        {
            if (resource == null) continue;

            if (Application.isPlaying)
                Destroy(resource);
            else
                DestroyImmediate(resource);
        }
    }

    private void CreateBackground()
    {
        if (backgroundSprite == null)
        {
            Debug.LogWarning("ModernBackground requires a background sprite.", this);
            return;
        }

        var backgroundObject = new GameObject("Modern Background Art");
        backgroundObject.transform.SetParent(transform, false);
        backgroundTransform = backgroundObject.transform;

        var spriteRenderer = backgroundObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = backgroundSprite;
        spriteRenderer.sortingOrder = -1000;
    }

    private ParticleSystem CreateFallingLights()
    {
        var particleObject = new GameObject("Falling Light Dots");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 0f, 15f);

        var particleSystem = particleObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.96f, 1f, 0.85f),
            new Color(1f, 0.86f, 0.45f, 0.78f));
        main.maxParticles = 35;

        var emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;

        var velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.55f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ApplySoftFade(particleSystem);

        var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = -900;
        renderer.material = CreateParticleMaterial("Falling Light Material", false);

        return particleSystem;
    }

    private ParticleSystem CreateFloatingDiamonds()
    {
        var particleObject = new GameObject("Floating Puzzle Sparkles");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 0f, 14f);

        var particleSystem = particleObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.96f, 1f, 0.95f),
            new Color(1f, 0.72f, 0.28f, 0.9f));
        main.maxParticles = 12;

        var emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;

        var velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.16f, -0.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var rotation = particleSystem.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ApplySoftFade(particleSystem);

        var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = -890;
        renderer.material = CreateParticleMaterial("Puzzle Glint Material", true);

        return particleSystem;
    }

    private static void ApplySoftFade(ParticleSystem particleSystem)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.16f),
                new GradientAlphaKey(0.7f, 0.78f),
                new GradientAlphaKey(0f, 1f)
            });

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = gradient;
    }

    private Material CreateParticleMaterial(string materialName, bool sparkle)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            Debug.LogWarning("No compatible particle shader was found.", this);
            return null;
        }

        var texture = CreateParticleTexture(sparkle);
        texture.name = sparkle ? "Four Point Sparkle Particle" : "Soft Dot Particle";
        runtimeResources.Add(texture);

        var material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        runtimeResources.Add(material);
        return material;
    }

    private static Texture2D CreateParticleTexture(bool sparkle)
    {
        const int textureSize = 64;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        var pixels = new Color[textureSize * textureSize];

        for (var y = 0; y < textureSize; ++y)
        {
            for (var x = 0; x < textureSize; ++x)
            {
                var nx = (x + 0.5f) / textureSize * 2f - 1f;
                var ny = (y + 0.5f) / textureSize * 2f - 1f;
                float alpha;

                if (sparkle)
                {
                    var coreDistance = Mathf.Sqrt(nx * nx + ny * ny);
                    var core = Mathf.Clamp01((0.34f - coreDistance) / 0.2f);
                    var horizontalRay = Mathf.Clamp01((0.075f - Mathf.Abs(ny)) / 0.075f) *
                                        Mathf.Clamp01(1f - Mathf.Abs(nx));
                    var verticalRay = Mathf.Clamp01((0.075f - Mathf.Abs(nx)) / 0.075f) *
                                      Mathf.Clamp01(1f - Mathf.Abs(ny));
                    alpha = Mathf.Max(core, Mathf.Max(horizontalRay, verticalRay) * 0.9f);
                }
                else
                {
                    var distance = Mathf.Sqrt(nx * nx + ny * ny);
                    alpha = Mathf.Clamp01((1f - distance) / 0.42f);
                }

                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void UpdateLayout(bool force)
    {
        if (targetCamera == null || !targetCamera.orthographic) return;

        var viewHeight = targetCamera.orthographicSize * 2f;
        var viewWidth = viewHeight * targetCamera.aspect;

        if (!force &&
            Mathf.Abs(viewWidth - previousViewWidth) < 0.01f &&
            Mathf.Abs(viewHeight - previousViewHeight) < 0.01f)
            return;

        previousViewWidth = viewWidth;
        previousViewHeight = viewHeight;

        if (backgroundTransform != null && backgroundSprite != null)
        {
            var spriteSize = backgroundSprite.bounds.size;
            var scale = Mathf.Max(
                (viewWidth + driftAmount * 4f) / spriteSize.x,
                (viewHeight + driftAmount * 4f) / spriteSize.y);
            backgroundBaseScale = Vector3.one * scale * 1.02f;
        }

        if (fallingLights != null)
        {
            var shape = fallingLights.shape;
            shape.position = new Vector3(0f, viewHeight * 0.5f + 0.5f, 0f);
            shape.scale = new Vector3(viewWidth + 2f, 0.15f, 0f);
        }

        if (floatingDiamonds != null)
        {
            var shape = floatingDiamonds.shape;
            shape.position = Vector3.zero;
            shape.scale = new Vector3(viewWidth, viewHeight, 0f);
        }
    }
}
