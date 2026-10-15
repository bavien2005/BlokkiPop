#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Creates the editable intro hierarchy in the existing Home scene.</summary>
[InitializeOnLoad]
public static class BlokkiPopHomeSceneBuilder
{
    private const string HomeScenePath = "Assets/_Project/Scenes/Home.unity";
    private const string BackgroundPath = "Assets/_Project/Sprites/intro-background.png";
    private const string IntroMusicPath = "Assets/_Project/Audio/GameLoop-04-42s.wav";
    private const string LiberationFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string BangersFontPath = PopRuntimeFontSetup.RuntimeFontPath;
    private const string RootName = "Blokki Pop Intro";
    private const string AutoBuildPreference = "BlokkiPop.HomeIntroSceneBuilt.v4";

    static BlokkiPopHomeSceneBuilder()
    {
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.delayCall += TryBuildForActiveHomeScene;
    }

    private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
    {
        TryBuildForScene(next);
    }

    private static void TryBuildForActiveHomeScene()
    {
        TryBuildForScene(EditorSceneManager.GetActiveScene());
    }

    private static void TryBuildForScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HomeScenePath ||
            EditorPrefs.GetBool(AutoBuildPreference, false))
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != RootName)
                continue;

            UpgradeExistingIntro(root, scene);
            EditorPrefs.SetBool(AutoBuildPreference, true);
            return;
        }

        BuildHomeIntroScene();
    }

    [MenuItem("Blokki Pop/Build Home Intro Scene")]
    public static void BuildHomeIntroScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName)
                Object.DestroyImmediate(root);
        }

        Texture2D backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (backgroundTexture == null)
        {
            Debug.LogError($"Blokki Pop intro background not found at '{BackgroundPath}'.");
            return;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BangersFontPath);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontPath);
        if (font == null)
        {
            Debug.LogError("Could not find a TextMesh Pro font for the Blokki Pop intro.");
            return;
        }

        var rootObject = new GameObject(RootName);
        var controller = rootObject.AddComponent<BlokkiPopIntroController>();
        var canvasObject = CreateObject("Intro Canvas", rootObject.transform,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.dynamicPixelsPerUnit = 1f;

        GameObject backgroundObject = CreateUIObject("Intro Background", canvasObject.transform);
        RawImage background = backgroundObject.AddComponent<RawImage>();
        background.texture = backgroundTexture;
        background.color = Color.white;
        background.raycastTarget = false;
        SetFullStretch(background.rectTransform);
        AspectRatioFitter aspectFitter = backgroundObject.AddComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectFitter.aspectRatio = (float)backgroundTexture.width / backgroundTexture.height;

        GameObject brandObject = CreateUIObject("Brand and Mini Board", canvasObject.transform);
        CanvasGroup brandGroup = brandObject.AddComponent<CanvasGroup>();
        RectTransform brandRect = brandObject.GetComponent<RectTransform>();
        SetCentered(brandRect, Vector2.zero, new Vector2(1080f, 1920f));

        GameObject logoObject = CreateUIObject("Blokki Pop Logo", brandObject.transform);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        SetCentered(logoRect, new Vector2(0f, 330f), new Vector2(960f, 360f));

        TextMeshProUGUI logoShadow = CreateText("Logo Shadow", logoObject.transform, font,
            "BLOKKI", new Vector2(0f, 95f), new Vector2(930f, 190f), 164f, Hex("#145278"));
        logoShadow.rectTransform.anchoredPosition += new Vector2(0f, -9f);
        AddOutline(logoShadow.gameObject, Hex("#145278"), new Vector2(5f, -8f));
        TextMeshProUGUI logoTop = CreateText("Logo BLOKKI", logoObject.transform, font,
            "BLOKKI", new Vector2(0f, 104f), new Vector2(930f, 190f), 164f, Color.white);
        AddOutline(logoTop.gameObject, Hex("#16759A"), new Vector2(3f, -4f));
        TextMeshProUGUI logoPop = CreateText("Logo POP", logoObject.transform, font,
            "POP", new Vector2(0f, -73f), new Vector2(700f, 190f), 188f, Hex("#FFF06A"));
        AddOutline(logoPop.gameObject, Hex("#F08A36"), new Vector2(3f, -5f));

        TextMeshProUGUI tagline = CreateText("Tagline", brandObject.transform, font,
            "DROP  •  MATCH  •  POP", new Vector2(0f, 110f), new Vector2(900f, 70f), 42f, Hex("#176486"));
        tagline.characterSpacing = 2f;

        GameObject gridObject = CreateUIObject("Mini Board Grid", brandObject.transform);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        SetCentered(gridRect, new Vector2(0f, -175f), new Vector2(424f, 424f));

        const int gridSize = 4;
        const float pitch = 100f;
        const float slotSize = 84f;
        for (int row = 0; row < gridSize; ++row)
        {
            for (int column = 0; column < gridSize; ++column)
            {
                GameObject slot = CreateUIObject($"Empty Cell {row + 1}-{column + 1}", gridObject.transform);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                float x = (column - (gridSize - 1) * 0.5f) * pitch;
                float y = ((gridSize - 1) * 0.5f - row) * pitch;
                SetCentered(slotRect, new Vector2(x, y), Vector2.one * slotSize);
                Image slotImage = slot.AddComponent<Image>();
                slotImage.color = new Color(0.12f, 0.40f, 0.58f, 0.19f);
                slotImage.raycastTarget = false;
            }
        }

        List<RectTransform> animatedCells = new();
        List<CanvasGroup> animatedCellGroups = new();
        AddAnimatedCell(gridObject.transform, "Block Cell 01", -150f, 50f, Hex("#30D5DF"), animatedCells, animatedCellGroups);
        AddAnimatedCell(gridObject.transform, "Block Cell 02", -50f, 50f, Hex("#30D5DF"), animatedCells, animatedCellGroups);
        AddAnimatedCell(gridObject.transform, "Block Cell 03", -50f, -50f, Hex("#30D5DF"), animatedCells, animatedCellGroups);
        AddAnimatedCell(gridObject.transform, "Block Cell 04", 50f, 150f, Hex("#FFD45C"), animatedCells, animatedCellGroups);
        AddAnimatedCell(gridObject.transform, "Block Cell 05", 150f, 150f, Hex("#FFD45C"), animatedCells, animatedCellGroups);
        AddAnimatedCell(gridObject.transform, "Block Cell 06", 150f, 50f, Hex("#FFD45C"), animatedCells, animatedCellGroups);

        TextMeshProUGUI loadingLabel = CreateText("Loading Status", canvasObject.transform,
            font, "GETTING YOUR BOARD READY...", new Vector2(0f, -650f), new Vector2(940f, 65f), 33f,
            new Color(0.08f, 0.32f, 0.47f, 0.88f));
        loadingLabel.characterSpacing = 1.5f;
        loadingLabel.raycastTarget = true;
        CanvasGroup loadingGroup = loadingLabel.gameObject.AddComponent<CanvasGroup>();
        Button startButton = loadingLabel.gameObject.AddComponent<Button>();
        startButton.targetGraphic = loadingLabel;
        startButton.transition = Selectable.Transition.None;
        startButton.interactable = false;

        GameObject progressTrackObject = CreateUIObject("Loading Progress Track", canvasObject.transform);
        RectTransform trackRect = progressTrackObject.GetComponent<RectTransform>();
        SetCentered(trackRect, new Vector2(0f, -710f), new Vector2(560f, 20f));
        Image progressTrack = progressTrackObject.AddComponent<Image>();
        progressTrack.color = new Color(1f, 1f, 1f, 0.50f);
        progressTrack.raycastTarget = false;

        GameObject progressFillObject = CreateUIObject("Loading Progress Fill", progressTrackObject.transform);
        RectTransform fillRect = progressFillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image progressFill = progressFillObject.AddComponent<Image>();
        progressFill.color = Hex("#20BBD5");
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;

        AudioSource popAudioSource = rootObject.AddComponent<AudioSource>();
        popAudioSource.playOnAwake = false;
        popAudioSource.spatialBlend = 0f;
        AudioSource introMusicSource = rootObject.AddComponent<AudioSource>();
        introMusicSource.playOnAwake = false;
        introMusicSource.loop = true;
        introMusicSource.spatialBlend = 0f;
        AudioClip introMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroMusicPath);
        BlokkiPopSceneTransition sceneTransition = EnsureTransitionCanvas(scene, backgroundTexture);
        EnsureEventSystem(scene);
        AssignControllerReferences(controller, brandGroup, logoRect, animatedCells, animatedCellGroups,
            progressFill, loadingLabel, loadingGroup, startButton, popAudioSource, logoTop, logoPop, tagline,
            introMusicClip, introMusicSource, sceneTransition);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetBool(AutoBuildPreference, true);
        Selection.activeGameObject = rootObject;
        Debug.Log("Built the editable Blokki Pop intro hierarchy in Home.unity.");
    }

    [MenuItem("Blokki Pop/Upgrade Home Intro Scene")]
    public static void UpgradeHomeIntroScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName)
            {
                UpgradeExistingIntro(root, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Upgraded the existing Home intro with progress animation, intro music, and scene transition.");
                return;
            }
        }

        BuildHomeIntroScene();
    }

    private static void UpgradeExistingIntro(GameObject root, UnityEngine.SceneManagement.Scene scene)
    {
        BlokkiPopIntroController controller = root.GetComponent<BlokkiPopIntroController>();
        Transform canvas = root.transform.Find("Intro Canvas");
        if (controller == null || canvas == null)
            return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        TextMeshProUGUI loadingLabel = canvas.Find("Loading Status")?.GetComponent<TextMeshProUGUI>();
        Image progressFill = canvas.Find("Loading Progress Track/Loading Progress Fill")?.GetComponent<Image>();
        if (loadingLabel == null || progressFill == null)
            return;

        CanvasGroup loadingGroup = loadingLabel.GetComponent<CanvasGroup>();
        if (loadingGroup == null)
            loadingGroup = loadingLabel.gameObject.AddComponent<CanvasGroup>();

        Button startButton = loadingLabel.GetComponent<Button>();
        if (startButton == null)
            startButton = loadingLabel.gameObject.AddComponent<Button>();
        startButton.targetGraphic = loadingLabel;
        startButton.transition = Selectable.Transition.None;
        startButton.interactable = false;
        loadingLabel.raycastTarget = true;

        AudioSource popAudioSource = root.GetComponent<AudioSource>();
        if (popAudioSource == null)
            popAudioSource = root.AddComponent<AudioSource>();
        popAudioSource.playOnAwake = false;
        popAudioSource.loop = false;
        popAudioSource.spatialBlend = 0f;

        AudioSource[] sources = root.GetComponents<AudioSource>();
        AudioSource introMusicSource = sources.Length > 1 ? sources[1] : root.AddComponent<AudioSource>();
        introMusicSource.playOnAwake = false;
        introMusicSource.loop = true;
        introMusicSource.spatialBlend = 0f;
        AudioClip introMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroMusicPath);
        BlokkiPopSceneTransition sceneTransition = EnsureTransitionCanvas(scene, AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath));

        Transform brand = canvas.Find("Brand and Mini Board");
        Transform logo = brand != null ? brand.Find("Blokki Pop Logo") : null;
        Transform grid = brand != null ? brand.Find("Mini Board Grid") : null;
        var animatedCells = new List<RectTransform>();
        var animatedCellGroups = new List<CanvasGroup>();
        if (grid != null)
        {
            for (int i = 0; i < grid.childCount; ++i)
            {
                Transform child = grid.GetChild(i);
                if (!child.name.StartsWith("Block Cell"))
                    continue;

                RectTransform cell = child as RectTransform;
                CanvasGroup cellGroup = child.GetComponent<CanvasGroup>();
                if (cell != null && cellGroup != null)
                {
                    animatedCells.Add(cell);
                    animatedCellGroups.Add(cellGroup);
                }
            }
        }

        EnsureEventSystem(scene);
        AssignControllerReferences(controller,
            brand != null ? brand.GetComponent<CanvasGroup>() : null,
            logo as RectTransform,
            animatedCells,
            animatedCellGroups,
            progressFill,
            loadingLabel,
            loadingGroup,
            startButton,
            popAudioSource,
            brand != null ? brand.Find("Blokki Pop Logo/Logo BLOKKI")?.GetComponent<TextMeshProUGUI>() : null,
            brand != null ? brand.Find("Blokki Pop Logo/Logo POP")?.GetComponent<TextMeshProUGUI>() : null,
            brand != null ? brand.Find("Tagline")?.GetComponent<TextMeshProUGUI>() : null,
            introMusicClip,
            introMusicSource,
            sceneTransition);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
    {
        EventSystem eventSystem = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            eventSystem = root.GetComponentInChildren<EventSystem>(true);
            if (eventSystem != null)
                break;
        }

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new("Blokki Pop Event System", typeof(EventSystem));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (inputModule.actionsAsset == null)
            inputModule.AssignDefaultActions();
    }

    private static BlokkiPopSceneTransition EnsureTransitionCanvas(
        UnityEngine.SceneManagement.Scene scene, Texture2D transitionTexture)
    {
        const string transitionCanvasName = "Scene Transition Canvas";
        GameObject transitionObject = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == transitionCanvasName)
            {
                transitionObject = root;
                break;
            }
        }

        if (transitionObject == null)
        {
            transitionObject = new GameObject(transitionCanvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup),
                typeof(RawImage), typeof(BlokkiPopSceneTransition));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(transitionObject, scene);
        }

        RectTransform rect = transitionObject.GetComponent<RectTransform>();
        SetFullStretch(rect);
        Canvas canvas = transitionObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = transitionObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup coverGroup = transitionObject.GetComponent<CanvasGroup>();
        coverGroup.alpha = 0f;
        coverGroup.interactable = false;
        coverGroup.blocksRaycasts = false;

        RawImage cover = transitionObject.GetComponent<RawImage>();
        cover.texture = transitionTexture;
        cover.color = Color.white;
        cover.raycastTarget = false;
        SetFullStretch(cover.rectTransform);

        BlokkiPopSceneTransition transition = transitionObject.GetComponent<BlokkiPopSceneTransition>();
        SerializedObject serialized = new(transition);
        serialized.FindProperty("coverGroup").objectReferenceValue = coverGroup;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(transition);
        return transition;
    }

    private static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static TextMeshProUGUI CreateText(
        string name, Transform parent, TMP_FontAsset font, string value,
        Vector2 position, Vector2 size, float fontSize, Color color)
    {
        GameObject textObject = CreateUIObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetCentered(rect, position, size);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.72f;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void AddAnimatedCell(
        Transform parent, string name, float x, float y, Color color,
        List<RectTransform> cells, List<CanvasGroup> groups)
    {
        GameObject cell = CreateUIObject(name, parent);
        RectTransform rect = cell.GetComponent<RectTransform>();
        SetCentered(rect, new Vector2(x, y), new Vector2(88f, 88f));
        Image image = cell.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        Shadow shadow = cell.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.04f, 0.22f, 0.34f, 0.38f);
        shadow.effectDistance = new Vector2(0f, -7f);
        shadow.useGraphicAlpha = true;
        Outline outline = cell.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.56f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        cells.Add(rect);
        groups.Add(cell.AddComponent<CanvasGroup>());
    }

    private static void AssignControllerReferences(
        BlokkiPopIntroController controller,
        CanvasGroup brandGroup,
        RectTransform logoRoot,
        List<RectTransform> cells,
        List<CanvasGroup> cellGroups,
        Image progressFill,
        TextMeshProUGUI loadingLabel,
        CanvasGroup loadingGroup,
        Button startButton,
        AudioSource popAudioSource,
        TextMeshProUGUI mainLogoText,
        TextMeshProUGUI popLogoText,
        TextMeshProUGUI taglineText,
        AudioClip introMusicClip,
        AudioSource introMusicSource,
        BlokkiPopSceneTransition sceneTransition)
    {
        SerializedObject serialized = new(controller);
        serialized.FindProperty("brandGroup").objectReferenceValue = brandGroup;
        serialized.FindProperty("logoRoot").objectReferenceValue = logoRoot;
        serialized.FindProperty("progressFill").objectReferenceValue = progressFill;
        serialized.FindProperty("loadingLabel").objectReferenceValue = loadingLabel;
        serialized.FindProperty("startButton").objectReferenceValue = startButton;
        serialized.FindProperty("popAudioSource").objectReferenceValue = popAudioSource;
        serialized.FindProperty("mainLogoText").objectReferenceValue = mainLogoText;
        serialized.FindProperty("popLogoText").objectReferenceValue = popLogoText;
        serialized.FindProperty("taglineText").objectReferenceValue = taglineText;
        serialized.FindProperty("introMusicClip").objectReferenceValue = introMusicClip;
        serialized.FindProperty("introMusicSource").objectReferenceValue = introMusicSource;
        serialized.FindProperty("sceneTransition").objectReferenceValue = sceneTransition;

        // The status label itself becomes the large, easy-to-tap start button when loading finishes.
        if (loadingGroup != null)
            loadingGroup.alpha = 1f;

        SerializedProperty cellArray = serialized.FindProperty("animatedCells");
        cellArray.arraySize = cells.Count;
        SerializedProperty groupArray = serialized.FindProperty("animatedCellGroups");
        groupArray.arraySize = cellGroups.Count;
        for (int i = 0; i < cells.Count; ++i)
        {
            cellArray.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            groupArray.GetArrayElementAtIndex(i).objectReferenceValue = cellGroups[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}
#endif
