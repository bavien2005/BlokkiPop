using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Additive scene/prefab authoring. Reinstall never rebuilds designer-owned colour/FX.</summary>
public static class PopAmbientAuthoring
{
    public const string ArtPath = "Assets/_Project/UI/Ambience";
    public const string RigPath = "Assets/_Project/Prefabs/Ambience/PopAmbientBackground.prefab";
    [MenuItem("Tools/Blokki Pop/Backgrounds/Install Automatic Score Backgrounds")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Stop Play/Build before authoring backgrounds.");
        for (int i = 0; i < SceneManager.sceneCount; ++i) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first; background installer never overwrites an unsaved scene.");
        var config = AssetDatabase.LoadAssetAtPath<PopAmbientConfiguration>(PopAmbientConfiguration.AssetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PopAmbientConfiguration>();
            AssetDatabase.CreateAsset(config, PopAmbientConfiguration.AssetPath);
        }
        // One-time migration of the unpublished illustration prototype. Later installs
        // preserve all designer-owned colours, thresholds and particle tuning.
        if (config.paletteRevision == 0)
        {
            config.scenes.Clear();
            config.scenes.Add(Scene("snow", "XANH BĂNG · TUYẾT RƠI", PopAmbientEffect.Snow, new(.36f,.55f,.65f), new(.62f,.73f,.78f), new(.9f,.97f,1), 65, 5, 18, new(-.95f,-.45f), new(.045f,.14f)));
            config.scenes.Add(Scene("stars", "TÍM DỊU · SAO LẤP LÁNH", PopAmbientEffect.Stars, new(.22f,.22f,.39f), new(.43f,.38f,.55f), new(.85f,.9f,1), 40, 4, 7, new(-.018f,.025f), new(.09f,.22f)));
            config.scenes.Add(Scene("sunset", "XANH NGỌC · ĐOM ĐÓM", PopAmbientEffect.Fireflies, new(.13f,.36f,.38f), new(.34f,.58f,.53f), new(1,.92f,.45f), 35, 3, 10, new(.02f,.11f), new(.06f,.16f)));
            config.paletteRevision = 1; EditorUtility.SetDirty(config);
        }
        Validate(config);
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Ambience")) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Ambience");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null) BuildRig(config);
        else UpgradeColourSurface();
        UpgradeNavigation();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
            ModernBackground background = null;
            foreach (var root in scene.GetRootGameObjects()) { background = root.GetComponentInChildren<ModernBackground>(true); if (background != null) break; }
            if (background == null) throw new InvalidOperationException("Game scene is missing its authored ModernBackground camera.");
            var existing = background.GetComponentInChildren<PopAmbientBackground>(true);
            if (existing == null)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath), background.transform);
                root.name = "Automatic Score Backgrounds"; root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            else if (!existing.HasPresentation) throw new InvalidOperationException("Existing background rig has incomplete references; refusing to rebuild custom edits.");
        }
        finally { if (Array.Exists(setup, s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets(); Debug.Log("[Ambient] Authored backgrounds installed. No save/high-score resets. Repeat install preserves Inspector edits.");
    }
    private static PopAmbientSceneDefinition Scene(string id, string title, PopAmbientEffect effect, Color top, Color bottom, Color color, int max, float rate, float lifetime, Vector2 velocity, Vector2 size) =>
        new() { id=id,title=title,topColor=top,bottomColor=bottom,glowColor=color,effect=effect,particleColor=color,maxParticles=max,emissionPerSecond=rate,particleLifetime=lifetime,verticalSpeed=velocity,particleSize=size,particleAlpha=effect==PopAmbientEffect.Snow?.65f:.9f,turbulence=effect==PopAmbientEffect.Stars?0:.07f,horizontalDrift=effect==PopAmbientEffect.Snow?.07f:.025f };
    private static void ConfigureColourSurface(PopAmbientBackground rig)
    {
        rig.colorSurface = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/UI/background.png");
        string path = ArtPath + "/Colour.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ArtPath+"/AmbientColour.shader")); AssetDatabase.CreateAsset(material,path); }
        rig.layerA.sharedMaterial = rig.layerB.sharedMaterial = material;
    }
    private static void UpgradeColourSurface()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath).GetComponent<PopAmbientBackground>().colorSurface != null) return;
        var root = PrefabUtility.LoadPrefabContents(RigPath);
        try { ConfigureColourSurface(root.GetComponent<PopAmbientBackground>()); PrefabUtility.SaveAsPrefabAsset(root,RigPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void BuildRig(PopAmbientConfiguration config)
    {
        var root = new GameObject("Automatic Score Backgrounds");
        try
        {
            var rig = root.AddComponent<PopAmbientBackground>(); rig.configuration = config;
            rig.layerA = Art("Background Layer A", root.transform); rig.layerB = Art("Background Layer B", root.transform);
            ConfigureColourSurface(rig);
            rig.snow = Effect("Snowfall", root.transform, PopAmbientEffect.Snow, 0);
            rig.stars = Effect("Twinkling Stars", root.transform, PopAmbientEffect.Stars, 1);
            rig.fireflies = Effect("Fireflies", root.transform, PopAmbientEffect.Fireflies, 2);
            rig.transitionBurst = Effect("Milestone Stardust", root.transform, PopAmbientEffect.Stars, 3);
            var main = rig.transitionBurst.main; main.loop = false; main.startLifetime = 1.3f; main.maxParticles = 40; main.startColor = new Color(1,.88f,.45f,.75f); main.startSize = new ParticleSystem.MinMaxCurve(.08f,.2f);
            var emission = rig.transitionBurst.emission; emission.rateOverTime = 0;
            var velocity = rig.transitionBurst.velocityOverLifetime; velocity.enabled = true; velocity.x = new ParticleSystem.MinMaxCurve(-1,1); velocity.y = new ParticleSystem.MinMaxCurve(-1,1); velocity.z = new ParticleSystem.MinMaxCurve(0,0);
            PrefabUtility.SaveAsPrefabAsset(root, RigPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static SpriteRenderer Art(string name, Transform parent)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = new Vector3(0,0,20);
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sortingOrder = -995; renderer.color = new Color(1,1,1,0); return renderer;
    }
    private static ParticleSystem Effect(string name, Transform parent, PopAmbientEffect kind, int index)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = new Vector3(0,0,15);
        var ps = obj.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = false; main.useUnscaledTime = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0; main.startLifetime = 8; main.maxParticles = 80; main.startSize = new ParticleSystem.MinMaxCurve(.04f,.14f);
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(10,18,0);
        var emission = ps.emission; emission.rateOverTime = 4;
        var noise = ps.noise; noise.frequency = .25f; noise.quality = ParticleSystemNoiseQuality.Low; noise.scrollSpeed = .1f;
        var gradient = new Gradient(); gradient.SetKeys(new[]{ new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            kind==PopAmbientEffect.Stars ? new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.2f),new GradientAlphaKey(.15f,.42f),new GradientAlphaKey(.85f,.67f),new GradientAlphaKey(0,1)} : new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(.65f,.8f),new GradientAlphaKey(0,1)});
        var fade = ps.colorOverLifetime; fade.enabled = true; fade.color = gradient;
        ps.useAutoRandomSeed = false; ps.randomSeed = 0x46A71u + (uint)index * 137;
        var renderer = obj.GetComponent<ParticleSystemRenderer>(); renderer.sortingOrder = -970 + index; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        string materialPath = ArtPath+"/"+kind+".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ArtPath+"/AmbientParticles.shader")); material.SetFloat("_Shape",(float)kind); AssetDatabase.CreateAsset(material,materialPath); }
        renderer.sharedMaterial = material; return ps;
    }
    private static void UpgradeNavigation()
    {
        foreach (string path in new[] { PopSceneUIAuthoring.PrefabPath, PopJourneyHomeAuthoring.PrefabPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<PopGameplayUI>(); var home = ui.View(PopUIPage.Home);
                var presentation = home.GetComponent<PopJourneyHomeView>(); home.Text("Rewards", presentation != null ? presentation.practiceLabel : "BÀI LUYỆN");
                var icon = home.Element("Rewards").rect.Find("Icon")?.GetComponent<PopHomeGraphic>();
                if (icon != null && icon.Shape == PopHomeGraphic.Kind.Palette) icon.Shape = PopHomeGraphic.Kind.Blocks;
                var pause = ui.View(PopUIPage.Pause); pause.Text("Rewards", pause.GetComponent<PopPausePresentation>()?.practice ?? "BÀI LUYỆN");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    public static void Validate(PopAmbientConfiguration config)
    {
        if (config == null || !config.enabled) return;
        if (config.firstScore < 1 || config.scoreInterval < 1 || config.scenes == null || config.scenes.Count < 2) throw new InvalidOperationException("Automatic backgrounds need positive score milestones and at least two scenes.");
        var ids = new System.Collections.Generic.HashSet<string>(); int active = 0;
        foreach (var scene in config.scenes)
        {
            if (scene == null || !scene.enabled) continue;
            if (string.IsNullOrWhiteSpace(scene.id) || !ids.Add(scene.id)) throw new InvalidOperationException("Background palettes need unique IDs.");
            ++active;
        }
        if (active < 2) throw new InvalidOperationException("Enable at least two backgrounds to avoid repeating the same scene.");
    }
}

[CustomEditor(typeof(PopAmbientBackground))]
public sealed class PopAmbientBackgroundInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("POP refresh: radial colour wave + two screen rebounds + star cannons. Scene Preview: 0 = original blue, 1 = snow, 2 = stars, 3 = fireflies. Refresh Preview scrubs the transition; the button plays it without Play Mode. Bounce is render-only and stops when dragging; layout/colliders remain untouched.",MessageType.Info);
        DrawDefaultInspector();
        var rig=(PopAmbientBackground)target;
        if (!Application.isPlaying && GUILayout.Button("▶ Preview POP refresh")) rig.PlayRefreshPreview();
        if(rig.configuration!=null && GUILayout.Button("Select score milestones / scenes / particle settings")) Selection.activeObject=rig.configuration;
    }
}
