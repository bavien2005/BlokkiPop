using System;
using System.Collections.Generic;
using UnityEngine;

public enum PopAmbientEffect { Snow, Stars, Fireflies }

[Serializable]
public sealed class PopAmbientSceneDefinition
{
    public string id, title;
    public bool enabled = true;
    public PopAmbientEffect effect;
    [Header("Simple colour backdrop — no landscape images")]
    public Color topColor = new(.25f, .25f, .45f);
    public Color bottomColor = new(.45f, .4f, .6f);
    public Color glowColor = new(.6f, .7f, 1);
    [Range(0, .15f)] public float glowStrength = .035f;
    public Color particleColor = Color.white;
    [Range(0, 1)] public float particleAlpha = .7f;
    [Range(0, 120)] public int maxParticles = 65;
    [Range(0, 15)] public float emissionPerSecond = 5;
    [Min(.1f)] public float particleLifetime = 12;
    public Vector2 particleSize = new(.035f, .12f);
    public Vector2 verticalSpeed = new(-.8f, -.35f);
    [Range(0, .5f)] public float horizontalDrift = .08f;
    [Range(0, .5f)] public float turbulence = .06f;
}

[CreateAssetMenu(fileName = "PopAmbientConfiguration", menuName = "Blokki Pop/Automatic Score Backgrounds")]
public sealed class PopAmbientConfiguration : ScriptableObject
{
    public const string ResourcePath = "Gameplay/PopAmbientConfiguration";
    public const string AssetPath = "Assets/_Project/Resources/Gameplay/PopAmbientConfiguration.asset";
    [HideInInspector] public int paletteRevision;
    public bool enabled = true;
    [Header("Chế độ áp dụng")]
    public bool classicEndless = true, endlessPop = true;
    [Header("Mốc điểm của ván hiện tại — không dùng kỷ lục")]
    [Min(1)] public int firstScore = 1000;
    [Min(1)] public int scoreInterval = 1000;
    [Header("Chuyển màu và chuyển động")]
    [InspectorName("Refresh Duration")]
    [Tooltip("Thời gian POP: vòng màu bung ra + màn chơi nảy rồi trở lại đúng vị trí.")]
    [Range(.1f, 4)] public float crossfadeSeconds = .85f;
    [Header("POP refresh — độ nảy và vòng sáng")]
    [Range(0, .15f)] public float screenBounce = .09f;
    [Range(0, .04f)] public float screenLift = .018f;
    [Range(1, 3)] public int bounceCount = 2;
    [Range(.01f, .15f)] public float refreshRingWidth = .045f;
    [Range(0, 2)] public float refreshRingGlow = .9f;
    [Tooltip("Fades back even while game-over timeScale is zero.")]
    [Range(0, 2)] public float returnToDefaultSeconds = .45f;
    [Tooltip("Tắt nảy, vòng sáng và particle; đổi màu tức thời, không có chớp trắng toàn màn hình.")]
    public bool reducedMotion;
    [Range(0, 1.5f)] public float particleDensity = 1;
    [Range(0, 80)] public int transitionBurstCount = 48;
    [Range(1, 15)] public float transitionBurstSpeed = 8;
    [Range(.2f, 2)] public float transitionBurstLifetime = 1.05f;
    public Vector2 transitionBurstSize = new(.12f, .3f);
    [Tooltip("Màu và hiệu ứng được chọn ngẫu nhiên, không lặp ngay kiểu vừa dùng. Giữ ID ổn định để Continue nhận lại đúng kiểu.")]
    public List<PopAmbientSceneDefinition> scenes = new();

#if UNITY_EDITOR
    public int PreviewRevision { get; private set; }
    private void OnValidate()
    {
        unchecked { PreviewRevision++; }
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    public bool AppliesTo(PopGameMode mode) => enabled && (mode == PopGameMode.Classic && classicEndless || mode == PopGameMode.EndlessPop && endlessPop);
    public PopAmbientSceneDefinition Find(string id)
    {
        if (scenes == null) return null;
        for (int i = 0; i < scenes.Count; ++i) { var scene = scenes[i]; if (scene != null && scene.enabled && scene.id == id) return scene; }
        return null;
    }
    public int StageFor(int score) => score < Mathf.Max(1, firstScore) ? 0 : 1 + (score - Mathf.Max(1, firstScore)) / Mathf.Max(1, scoreInterval);
}

/// <summary>Cosmetic RNG is independent of Unity Random and both gameplay generators.</summary>
[Serializable]
public sealed class PopAmbientRunState
{
    public int stage;
    public string sceneId = "";
    public uint randomState;
    public PopAmbientRunState Copy() => new() { stage = stage, sceneId = sceneId, randomState = randomState };

    public bool Advance(PopAmbientConfiguration config, int score)
    {
        int nextStage = config.StageFor(score);
        if (nextStage <= stage) return false;
        int count = 0, alternatives = 0;
        foreach (var scene in config.scenes)
            if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.id)) { ++count; if (scene.id != sceneId) ++alternatives; }
        if (count == 0) return false;
        uint value = randomState == 0 ? 0xA8F321D7u : randomState;
        value ^= value << 13; value ^= value >> 17; value ^= value << 5; randomState = value;
        int chosen = (int)(value % (uint)(alternatives > 0 ? alternatives : count));
        foreach (var scene in config.scenes)
        {
            if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.id) || alternatives > 0 && scene.id == sceneId) continue;
            if (chosen-- != 0) continue;
            sceneId = scene.id; stage = nextStage; return true;
        }
        return false;
    }
}
