using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One-time additive migration, never regenerates an existing designer prefab.</summary>
public static class PopSceneUIAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/PopGameplayUI.prefab";
    private static PopGameplayConfiguration config;
    private static Sprite rounded;
    private static readonly string[] scenes = { "Assets/_Project/Scenes/Home.unity", "Assets/_Project/Scenes/Game.unity" };
    [MenuItem("Tools/Blokki Pop/UI/Install Authored UI (Preserve Existing)")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play/Build before installing authored UI.");
        for (int i = 0; i < SceneManager.sceneCount; ++i)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your open scene changes first. UI migration refuses to overwrite an unsaved scene.");
        PopGameplaySetup.EnsureConfiguration();
        config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EnsurePrefab(); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                PopGameplayUI existing = FindInScene(scene);
                if (existing != null)
                {
                    Validate(existing, config);
                    if (DisableLegacyHudCopy(scene, path)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                    continue;
                }
                string backup = Path.GetFullPath(".utmp/scene-ui-20261015/scene-backups/" + Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(Path.GetFullPath(path), backup);
                foreach (GameObject oldRoot in scene.GetRootGameObjects())
                {
                    PopGameplayUI legacy = oldRoot.GetComponent<PopGameplayUI>();
                    if (legacy == null || legacy.Views != null && legacy.Views.Count > 0) continue;
                    if ((oldRoot.name != "Pop Modes & Progression UI" && oldRoot.name != "Legacy Runtime UI (preserved)") || oldRoot.transform.childCount != 0)
                        throw new InvalidOperationException("Unbound custom UI requires manual binding; refusing to replace " + oldRoot.name);
                    oldRoot.name = "Legacy Runtime UI (preserved)"; oldRoot.SetActive(false);
                }
                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                root.name = "Blokki Pop UI"; PopGameplayUI ui = root.GetComponent<PopGameplayUI>();
                ui.ScenePreview = path.EndsWith("Home.unity") ? PopUIPage.Home : PopUIPage.Hud;
                ui.PreviewScenePage(); PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
                Validate(ui, config); DisableLegacyHudCopy(scene, path); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Scene UI] Installed real prefab instances in Home and Game. Existing prefab/layout edits are preserved: " + PrefabPath);
        }
        finally
        {
            bool hasActive = Array.Exists(setup, item => item.isLoaded && item.isActive);
            if (hasActive) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
        }
    }
    private static PopGameplayUI FindInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        { var ui = root.GetComponentInChildren<PopGameplayUI>(true); if (ui != null && ui.Views != null && ui.Views.Count > 0) return ui; }
        return null;
    }
    private static bool DisableLegacyHudCopy(Scene scene, string path)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "Mode Goals HUD" || !candidate.gameObject.activeSelf || candidate.GetComponentInParent<PopUIView>() != null) continue;
                TMP_Text[] labels = candidate.GetComponentsInChildren<TMP_Text>(true);
                bool legacyCopy = Array.Exists(labels, text => text.text == "CLASSIC ENDLESS") &&
                    Array.Exists(labels, text => text.text == "No move limit. Build your best score.");
                if (!legacyCopy) throw new InvalidOperationException("Unrecognized custom Mode Goals HUD; refusing to modify it automatically.");
                string backup = Path.GetFullPath(".utmp/scene-ui-20261015/scene-backups-before-legacy-hud/" + Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)); if (!File.Exists(backup)) File.Copy(Path.GetFullPath(path), backup);
                candidate.name = "Legacy Mode Goals HUD (preserved)"; candidate.gameObject.SetActive(false); changed = true;
                Debug.Log("[Scene UI] Disabled stale copied Mode Goals HUD outside the authored UI; all objects/styles retained in " + path);
            }
        return changed;
    }
    private static void EnsurePrefab()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            // Repair only the known zero-sized root emitted by the first batch preview scene.
            // This does not regenerate or touch any designer-owned child objects/styles.
            RectTransform existingRect = existingPrefab.GetComponent<RectTransform>();
            if (existingRect.localScale == Vector3.zero && existingRect.sizeDelta == Vector2.zero)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try { NormalizeInitialRoot((RectTransform)contents.transform); PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Scene preview = EditorSceneManager.NewPreviewScene(); GameObject root = null;
        try
        {
            root = new GameObject("Blokki Pop UI", typeof(RectTransform)); SceneManager.MoveGameObjectToScene(root, preview);
            Canvas canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1800;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.ui.referenceResolution; scaler.matchWidthOrHeight = config.ui.screenMatch;
            root.AddComponent<GraphicRaycaster>();
            RectTransform safe = Rect("Safe Area", root.transform); Stretch(safe);
            var views = new List<PopUIView>();
            PopUIView home = Panel(PopUIPage.Home, safe, false, config.ui.homeCardSize, config.ui.homeCardPosition); views.Add(home);
            Transform card = home.transform.Find("Card");
            Label(home, "Title", card, config.text.homeTitle, 0, config.ui.homeTitleY, 780, 70, true);
            Label(home, "Stats", card, "BEST: 1562 | 0 stars", 0, config.ui.homeStatsY, 780, 55, small: true);
            RectTransform stack = Rect("Mode Buttons - Layout", card); stack.anchorMin = stack.anchorMax = stack.pivot = new Vector2(.5f, 1);
            stack.anchoredPosition = new Vector2(0, -190); stack.sizeDelta = new Vector2(config.ui.primaryButtonSize.x, 630);
            // The LayoutGroup is authored and can be adjusted/removed in the Inspector.
            VerticalLayoutGroup layout = stack.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = config.ui.homeButtonSpacing - config.ui.primaryButtonSize.y;
            layout.childAlignment = TextAnchor.UpperCenter; layout.childControlWidth = layout.childControlHeight = false; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Button(home, "Continue", stack, config.text.continueRun + " · 12", 0, 0);
            Button(home, "Classic", stack, config.text.classic, 0, 0); Button(home, "Journey", stack, config.text.journey, 0, 0);
            Button(home, "Daily", stack, config.text.daily, 0, 0); Button(home, "Rewards", stack, config.text.themes, 0, 0);
            Button(home, "Learn", card, config.text.tutorial, -config.ui.homeFooterHalfGap, -412, config.ui.secondaryButtonSize, true);
            Button(home, "Privacy", card, config.text.privacy, config.ui.homeFooterHalfGap, -412, config.ui.secondaryButtonSize, true);
            PopUIView hud = Panel(PopUIPage.Hud, safe, false, config.ui.hudSize, Vector2.zero); views.Add(hud);
            RectTransform h = (RectTransform)hud.transform; h.anchorMin = h.anchorMax = h.pivot = new Vector2(.5f, 1); h.anchoredPosition = new Vector2(0, -config.ui.hudTopInset);
            card = hud.transform.Find("Card");
            Label(hud, "Title", card, "POP JOURNEY · First Pop", -90, 47, 720, 45, true, font: config.ui.buttonFontSize);
            Label(hud, "Stats", card, "MOVES: 6 · 0/6", -90, 7, 720, 40, small: true);
            Label(hud, "Goals", card, "LINES: 0/1", 0, -36, 900, 40, small: true);
            Button(hud, "Pause", card, config.text.pause, 375, 37, config.ui.pauseButtonSize, true);
            PopUIView tutorial = Panel(PopUIPage.Tutorial, safe, false, config.ui.tutorialCardSize, Vector2.zero); views.Add(tutorial);
            RectTransform t = (RectTransform)tutorial.transform; t.anchorMin = t.anchorMax = t.pivot = new Vector2(.5f, 0); t.anchoredPosition = new Vector2(0, config.ui.tutorialBottomInset);
            card = tutorial.transform.Find("Card");
            Label(tutorial, "Instruction", card, config.tutorial.stepInstructions[0], 0, 30, 820, 115);
            Button(tutorial, "Skip", card, config.text.skipTutorial, 0, -63, new Vector2(400, 52), true);
            PopUIView journey = Modal(PopUIPage.Journey, safe, config.text.journey, views); card = journey.transform.Find("Card");
            Label(journey, "Stats", card, "0/60 stars | 0/20 levels cleared", 0, 465, 780, 65, small: true);
            for (int i = 0; i < config.journeyLevels.Count; ++i)
            {
                int offset = i % config.ui.levelsPerPage, col = offset % config.ui.levelColumns, row = offset / config.ui.levelColumns;
                Button(journey, "Level" + (i + 1).ToString("00"), card, (i + 1) + ". " + config.journeyLevels[i].title + "\n0/3 stars",
                    (col - (config.ui.levelColumns - 1) * .5f) * config.ui.levelButtonSpacing.x, 340 - row * config.ui.levelButtonSpacing.y, config.ui.levelButtonSize, true);
                journey.Visible("Level" + (i + 1).ToString("00"), i < config.ui.levelsPerPage);
            }
            Button(journey, "Previous", card, "<", -300, -490, new Vector2(100, 70)); Label(journey, "Page", card, "1 / 2", 0, -490, 250, 60);
            Button(journey, "Next", card, ">", 300, -490, new Vector2(100, 70)); Back(journey, card);
            PopUIView level = Modal(PopUIPage.Level, safe, "LEVEL 1: " + config.journeyLevels[0].title, views); card = level.transform.Find("Card");
            Label(level, "Description", card, config.journeyLevels[0].description, 0, 330, 760, 180);
            Label(level, "Goals", card, "LINES: 1", 0, 130, 760, 220); Label(level, "Moves", card, "MOVES: 6\n3 stars: 1 move | 2 stars: 3 moves", 0, -80, 760, 150, small: true);
            RectTransform actions = Rect("Play Buttons - Layout", card); actions.anchorMin = actions.anchorMax = actions.pivot = new Vector2(.5f, .5f);
            actions.sizeDelta = new Vector2(750, 220); actions.anchoredPosition = new Vector2(0, -320);
            VerticalLayoutGroup actionsLayout = actions.gameObject.AddComponent<VerticalLayoutGroup>(); actionsLayout.spacing = 30; actionsLayout.childControlWidth = actionsLayout.childControlHeight = false; actionsLayout.childForceExpandWidth = actionsLayout.childForceExpandHeight = false;
            Button(level, "Continue", actions, config.text.continueRun, 0, 0); Button(level, "Play", actions, config.text.play, 0, 0); Back(level, card);
            PopUIView daily = Modal(PopUIPage.Daily, safe, config.text.daily, views); card = daily.transform.Find("Card");
            Label(daily, "Date", card, "TODAY · UTC\nA shared puzzle", 0, 420, 760, 100);
            Label(daily, "Goals", card, "BUBBLES: 6 · MOVES: 12", 0, 265, 760, 170, small: true);
            Label(daily, "Status", card, config.text.goalReminder, 0, 125, 760, 100, small: true);
            RectTransform calendar = Rect("Calendar", card); calendar.sizeDelta = new Vector2(770, 330); calendar.anchoredPosition = new Vector2(0, -105);
            for (int i = 0; i < 35; ++i)
            {
                string key = "Day" + (i + 1).ToString("00"); RectTransform day = Image(key, calendar, config.ui.locked, new Vector2(93, 62), new Vector2((i % 7 - 3) * 108, 120 - i / 7 * 72));
                TMP_Text number = RawLabel("Number", day, (i + 1).ToString(), Vector2.zero, day.sizeDelta, config.ui.smallFontSize, false);
                daily.elements.Add(new PopUIElement { key = key, rect = day, text = number, graphic = day.GetComponent<Image>() }); day.gameObject.SetActive(i < config.daily.calendarDaysToShow);
            }
            Label(daily, "Stats", card, "Daily wins: 0 | DAILY TROPHY: 0", 0, -330, 780, 65, small: true);
            Button(daily, "Play", card, config.text.play, 0, -430); Label(daily, "Notice", card, config.text.dailyNotice, 0, -515, 780, 70, small: true); Back(daily, card);
            PopUIView rewards = Modal(PopUIPage.Rewards, safe, config.text.themes, views); card = rewards.transform.Find("Card");
            RectTransform viewport = Image("Scroll View", card, new Color(0, 0, 0, .01f), new Vector2(790, 970), Vector2.zero);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Items", viewport); content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(790, Mathf.Max(970, (config.themes.Count + config.popEffects.Count) * 225 + 80));
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < config.themes.Count; ++i)
                RewardRow(rewards, content, "Theme" + (i + 1).ToString("00"), config.themes[i].title, config.themes[i].description, -35 - i * 225, config.themes[i].accent);
            float effectTop = -config.themes.Count * 225;
            Label(rewards, "EffectsHeading", content, config.text.popEffects, 0, effectTop - 25, 760, 60, true, font: config.ui.buttonFontSize, top: true);
            for (int i = 0; i < config.popEffects.Count; ++i)
                RewardRow(rewards, content, "Effect" + (i + 1).ToString("00"), config.popEffects[i].title, config.popEffects[i].description, effectTop - 105 - i * 225, config.themes[0].accent);
            Back(rewards, card);
            PopUIView pause = Modal(PopUIPage.Pause, safe, config.text.pause, views); card = pause.transform.Find("Card");
            Label(pause, "Body", card, config.text.pauseNotice, 0, 280, 750, 170);
            Button(pause, "Resume", card, config.text.resume, 0, 60); Button(pause, "Home", card, config.text.home, 0, -70);
            Button(pause, "Privacy", card, config.text.privacy, 0, -200); Button(pause, "Rewards", card, config.text.themes, 0, -330);
            PopUIView victory = Modal(PopUIPage.Victory, safe, config.text.victory, views); card = victory.transform.Find("Card");
            for (int i = 0; i < 3; ++i)
            {
                RectTransform star = Rect("Reward Star " + (i + 1), card); star.sizeDelta = config.ui.outcomeStarSize; star.anchoredPosition = new Vector2((i - 1) * config.ui.outcomeStarSpacing, config.ui.outcomeStarY);
                PopStarGraphic graphic = star.gameObject.AddComponent<PopStarGraphic>(); graphic.color = config.ui.earnedStar; graphic.raycastTarget = false;
                victory.elements.Add(new PopUIElement { key = "Star" + (i + 1), rect = star, graphic = graphic });
            }
            Label(victory, "Body", card, "LINES: 1/1\nMOVES: 1 · SCORE: 43", 0, 100, 770, 240);
            Label(victory, "Rewards", card, "New rewards appear here", 0, -55, 770, 70, small: true);
            Button(victory, "Next", card, config.text.next, 0, -160); Button(victory, "Retry", card, config.text.retry, 0, -285); Button(victory, "Home", card, config.text.home, 0, -410);
            PopUIView failure = Modal(PopUIPage.Failure, safe, config.text.failed, views); card = failure.transform.Find("Card");
            Label(failure, "Reason", card, config.text.outOfMoves, 0, 310, 770, 140); Label(failure, "Body", card, "LINES: 0/3\nMOVES: 10 · SCORE: 90", 0, 100, 770, 240);
            Button(failure, "Retry", card, config.text.retry, 0, -160); Button(failure, "Home", card, config.text.home, 0, -290);
            PopUIView replace = Modal(PopUIPage.ReplaceRun, safe, config.text.newRun, views); card = replace.transform.Find("Card");
            Label(replace, "Body", card, config.text.replaceRunWarning, 0, 220, 750, 260); Button(replace, "Play", card, config.text.newRun, 0, -80); Button(replace, "Back", card, config.text.back, 0, -205);
            PopUIView invalid = Modal(PopUIPage.InvalidSave, safe, config.text.continueRun, views); card = invalid.transform.Find("Card");
            Label(invalid, "Body", card, config.text.invalidSaveNotice, 0, 150, 750, 300); Button(invalid, "Close", card, config.text.close, 0, -180);
            PopGameplayUI controller = root.AddComponent<PopGameplayUI>(); SerializedObject serialized = new(controller);
            serialized.FindProperty("safeRoot").objectReferenceValue = safe;
            SerializedProperty array = serialized.FindProperty("views"); array.arraySize = views.Count;
            for (int i = 0; i < views.Count; ++i) array.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            serialized.FindProperty("starRevealSeconds").floatValue = config.ui.starRevealSeconds;
            serialized.FindProperty("earnedStar").colorValue = config.ui.earnedStar; serialized.FindProperty("emptyStar").colorValue = config.ui.emptyStar;
            serialized.ApplyModifiedPropertiesWithoutUndo(); controller.PreviewScenePage();
            NormalizeInitialRoot((RectTransform)root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { if (root != null) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void NormalizeInitialRoot(RectTransform root)
    {
        root.localScale = Vector3.one; root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
        root.sizeDelta = config.ui.referenceResolution; root.anchoredPosition = Vector2.zero;
    }
    private static void RewardRow(PopUIView view, Transform parent, string key, string title, string description, float y, Color accent)
    {
        Label(view, key + "Title", parent, title, 0, y, 760, 65, true, font: config.ui.buttonFontSize, top: true);
        Label(view, key + "Description", parent, description + "\nUnlock requirements update during play", 0, y - 73, 750, 80, small: true, top: true);
        Button(view, key + "Select", parent, "SELECT · " + title, 0, y - 150, new Vector2(700, 65), true, true, accent);
    }
    private static PopUIView Modal(PopUIPage page, Transform parent, string title, List<PopUIView> views)
    {
        PopUIView view = Panel(page, parent, true, config.ui.modalCardSize, Vector2.zero); views.Add(view);
        Label(view, "Title", view.transform.Find("Card"), title, 0, 575, 790, 125, true); return view;
    }
    private static PopUIView Panel(PopUIPage page, Transform parent, bool modal, Vector2 size, Vector2 position)
    {
        RectTransform root = Rect(page.ToString(), parent); PopUIView view = root.gameObject.AddComponent<PopUIView>(); view.page = page;
        if (modal) { Stretch(root); Image dim = root.gameObject.AddComponent<Image>(); dim.color = config.ui.modalDimmer; dim.raycastTarget = true; }
        else { root.sizeDelta = size; root.anchoredPosition = position; }
        Image("Card", root, config.ui.card, size, Vector2.zero); return view;
    }
    private static void Back(PopUIView view, Transform parent) => Button(view, "Back", parent, config.text.back, 0, -595, new Vector2(650, 65), true);
    private static TMP_Text Label(PopUIView view, string key, Transform parent, string text, float x, float y, float width, float height, bool heading = false, bool small = false, float font = 0, bool top = false)
    {
        TMP_Text label = RawLabel(key, parent, text, new Vector2(x, y), new Vector2(width, height), font > 0 ? font : small ? config.ui.smallFontSize : heading ? config.ui.headingFontSize : config.ui.bodyFontSize, heading);
        if (top) label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1);
        view.elements.Add(new PopUIElement { key = key, rect = label.rectTransform, text = label, graphic = label }); if (key == "Title") view.title = label;
        return label;
    }
    private static TMP_Text RawLabel(string name, Transform parent, string text, Vector2 position, Vector2 size, float font, bool heading)
    {
        RectTransform rect = Rect(name, parent); rect.sizeDelta = size; rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = (heading ? config.ui.headingFont : config.ui.bodyFont) ?? TMP_Settings.defaultFontAsset;
        label.text = text; label.color = config.ui.text; label.fontSize = font; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMax = font; label.fontSizeMin = Mathf.Max(12, font * .75f); label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
    private static void Button(PopUIView view, string key, Transform parent, string text, float x, float y, Vector2? size = null, bool small = false, bool top = false, Color? color = null)
    {
        RectTransform rect = Image(key, parent, color ?? config.themes[0].accent, size ?? config.ui.primaryButtonSize, new Vector2(x, y));
        Image image = rect.GetComponent<Image>(); image.raycastTarget = true; image.sprite = config.ui.buttonSprite != null ? config.ui.buttonSprite : rounded;
        if (top) rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        ColorBlock colors = button.colors; colors.disabledColor = new Color(.45f, .5f, .55f, .55f); button.colors = colors;
        TMP_Text label = RawLabel("Label", rect, text, Vector2.zero, rect.sizeDelta - new Vector2(24, 10), small ? config.ui.smallFontSize : config.ui.buttonFontSize, false);
        view.elements.Add(new PopUIElement { key = key, rect = rect, text = label, button = button, graphic = image });
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; return rect;
    }
    private static RectTransform Image(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        RectTransform rect = Rect(name, parent); rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        image.sprite = config.ui.cardSprite != null ? config.ui.cardSprite : rounded; image.type = UnityEngine.UI.Image.Type.Sliced; return rect;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    public static void Validate(PopGameplayUI ui, PopGameplayConfiguration configuration)
    {
        if (ui == null || ui.Views == null) throw new BuildFailedException("Authored UI is missing from the scene. Run Tools > Blokki Pop > UI > Install Authored UI.");
        if (!ui.HasClassicHUD || ui.View(PopUIPage.Pause).GetComponent<PopPausePresentation>() is not { HasPresentation: true })
            throw new BuildFailedException("Classic HUD/Pause bindings are missing. Run Tools > Blokki Pop > UI > Install Classic HUD and Pause Sheet.");
        if (configuration.endless.replaceJourneyHomeButton && (ui.EndlessQueue == null || !ui.EndlessQueue.HasPresentation))
            throw new BuildFailedException("Endless Pop needs its authored future-piece queue. Run Tools > Blokki Pop > UI > Install Endless Pop Preview.");
        foreach (PopUIPage page in Enum.GetValues(typeof(PopUIPage)))
        {
            PopUIView view = ui.View(page); var keys = new HashSet<string>();
            foreach (PopUIElement item in view.elements)
                if (item == null || item.rect == null || !item.rect.IsChildOf(view.transform) || !keys.Add(item.key))
                    throw new BuildFailedException("Broken/duplicate UI binding in " + page);
        }
        var required = new Dictionary<PopUIPage, string>
        {
            [PopUIPage.Home] = "Title,Stats,!Continue,!Classic,!Journey,!Daily,!Rewards,!Learn,!Privacy",
            [PopUIPage.Hud] = "Title,Stats,Goals,!Pause",
            [PopUIPage.Tutorial] = "Instruction,!Skip",
            [PopUIPage.Journey] = "Title,Stats,!Previous,Page,!Next,!Back",
            [PopUIPage.Level] = "Title,Description,Goals,Moves,!Continue,!Play,!Back",
            [PopUIPage.Daily] = "Title,Date,Goals,Status,Stats,!Play,Notice,!Back",
            [PopUIPage.Rewards] = "Title,EffectsHeading,!Back",
            [PopUIPage.Pause] = "Title,Body,!Resume,!Home,!Privacy,!Rewards",
            [PopUIPage.Victory] = "Title,Body,Rewards,!Next,!Retry,!Home",
            [PopUIPage.Failure] = "Title,Reason,Body,!Retry,!Home",
            [PopUIPage.ReplaceRun] = "Title,Body,!Play,!Back",
            [PopUIPage.InvalidSave] = "Title,Body,!Close"
        };
        foreach (var entry in required) foreach (string key in entry.Value.Split(',')) RequireBinding(ui.View(entry.Key), key);
        for (int i = 1; i <= 3; ++i)
            if (ui.View(PopUIPage.Victory).Element("Star" + i).graphic == null) throw new BuildFailedException("Missing result star graphic.");
        for (int i = 0; i < configuration.journeyLevels.Count; ++i) RequireBinding(ui.View(PopUIPage.Journey), "!Level" + (i + 1).ToString("00"));
        for (int i = 0; i < 35; ++i)
        {
            string key = "Day" + (i + 1).ToString("00"); RequireBinding(ui.View(PopUIPage.Daily), key);
            if (ui.View(PopUIPage.Daily).Element(key).graphic == null) throw new BuildFailedException("Missing calendar status graphic: " + key);
        }
        for (int i = 0; i < configuration.themes.Count + configuration.popEffects.Count; ++i)
        {
            string prefix = i < configuration.themes.Count ? "Theme" + (i + 1).ToString("00") : "Effect" + (i - configuration.themes.Count + 1).ToString("00");
            RequireBinding(ui.View(PopUIPage.Rewards), prefix + "Title"); RequireBinding(ui.View(PopUIPage.Rewards), prefix + "Description");
            RequireBinding(ui.View(PopUIPage.Rewards), "!" + prefix + "Select");
        }
    }
    private static void RequireBinding(PopUIView view, string key)
    {
        bool button = key.StartsWith("!"); PopUIElement element = view.Element(button ? key.Substring(1) : key);
        if (element.text == null || button && (element.button == null || element.button.targetGraphic == null))
            throw new BuildFailedException(view.name + ": incomplete text/button binding " + key);
    }
    public static int JourneyPreviewPage(PopGameplayUI ui)
    {
        int width = Mathf.Max(1, AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath).ui.levelsPerPage);
        foreach (PopUIElement element in ui.View(PopUIPage.Journey).elements)
            if (element.key.StartsWith("Level") && int.TryParse(element.key.Substring(5), out int index) && element.rect.gameObject.activeSelf) return (index - 1) / width;
        return 0;
    }
    public static void PreviewJourneyPage(PopGameplayUI ui, int page)
    {
        if (Application.isPlaying) return;
        PopGameplayConfiguration data = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        int width = Mathf.Max(1, data.ui.levelsPerPage), pages = Mathf.Max(1, Mathf.CeilToInt(data.journeyLevels.Count / (float)width));
        page = Mathf.Clamp(page, 0, pages - 1); PopUIView view = ui.View(PopUIPage.Journey);
        foreach (PopUIElement element in view.elements)
        {
            if (!element.key.StartsWith("Level") || !int.TryParse(element.key.Substring(5), out int index)) continue;
            Undo.RecordObject(element.rect.gameObject, "Preview Journey page");
            element.rect.gameObject.SetActive((index - 1) / width == page);
            PrefabUtility.RecordPrefabInstancePropertyModifications(element.rect.gameObject);
        }
        PopUIElement label = view.Element("Page");
        if (label.updateText) { Undo.RecordObject(label.text, "Preview Journey page"); label.text.text = (page + 1) + " / " + pages; PrefabUtility.RecordPrefabInstancePropertyModifications(label.text); }
        if (ui.gameObject.scene.IsValid() && ui.gameObject.scene.isLoaded) EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(PopGameplayUI))]
public sealed class PopGameplayUIInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("UI is saved in the scene/prefab. Choose Scene Preview below to edit any panel without Play. Select its child objects to adjust RectTransform, Image, TMP or Button. Runtime will not overwrite those styles.", MessageType.Info);
        EditorGUILayout.HelpBox("Scene Preview = Hud: turn Preview Endless HUD OFF for Classic Overlay > Gameplay Frame > Header / Stats; ON for Endless. Pause is a separate authored page. All layouts/styles are editable here, never rebuilt in Play. The camera reserves their actual bounds.", MessageType.Info);
        EditorGUI.BeginChangeCheck(); DrawDefaultInspector(); bool changed = EditorGUI.EndChangeCheck();
        PopGameplayUI ui = (PopGameplayUI)target;
        if (ui.Views == null || ui.Views.Count == 0)
        { EditorGUILayout.HelpBox("This is the preserved legacy controller, not the editable UI. Select Blokki Pop UI in the scene.", MessageType.Warning); return; }
        if (!Application.isPlaying && (changed || GUILayout.Button("Show Selected Panel in Scene")))
        {
            foreach (PopUIView view in ui.Views) Undo.RecordObject(view.gameObject, "Preview authored UI");
            if (ui.EndlessQueue != null) Undo.RecordObject(ui.EndlessQueue.gameObject, "Preview authored UI");
            if (ui.ClassicHUD != null) Undo.RecordObject(ui.ClassicHUD.gameObject, "Preview authored UI");
            ui.PreviewScenePage(); SceneView.RepaintAll();
            foreach (PopUIView view in ui.Views) PrefabUtility.RecordPrefabInstancePropertyModifications(view.gameObject);
            if (ui.EndlessQueue != null) PrefabUtility.RecordPrefabInstancePropertyModifications(ui.EndlessQueue.gameObject);
            if (ui.ClassicHUD != null) PrefabUtility.RecordPrefabInstancePropertyModifications(ui.ClassicHUD.gameObject);
        }
        if (!Application.isPlaying && GUILayout.Button("Frame Visible Panel in Scene"))
        { Selection.activeGameObject = ui.ScenePreview == PopUIPage.Hud && ui.ClassicHUD != null && ui.ClassicHUD.gameObject.activeSelf ? ui.ClassicHUD.gameObject : ui.ScenePreview == PopUIPage.Hud && ui.EndlessQueue != null && ui.EndlessQueue.gameObject.activeSelf ? ui.EndlessQueue.gameObject : ui.View(ui.ScenePreview).gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
        if (!Application.isPlaying && ui.ScenePreview == PopUIPage.Journey)
        {
            PopGameplayConfiguration data = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
            int pages = Mathf.Max(1, Mathf.CeilToInt(data.journeyLevels.Count / (float)Mathf.Max(1, data.ui.levelsPerPage)));
            int current = PopSceneUIAuthoring.JourneyPreviewPage(ui);
            int selected = EditorGUILayout.IntSlider("Journey Page Preview", current + 1, 1, pages) - 1;
            if (selected != current) PopSceneUIAuthoring.PreviewJourneyPage(ui, selected);
        }
        if (GUILayout.Button("Select UI Prefab"))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ui.gameObject);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(string.IsNullOrEmpty(path) ? PopSceneUIAuthoring.PrefabPath : path);
        }
    }
}
public sealed class PopSceneUIBuildGuard : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !scene.path.StartsWith("Assets/_Project/Scenes/")) return;
        PopGameplayUI ui = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        { PopGameplayUI found = root.GetComponentInChildren<PopGameplayUI>(true); if (found == null || !root.activeSelf && root.name == "Legacy Runtime UI (preserved)") continue; if (ui != null) throw new BuildFailedException("Duplicate progression UI."); ui = found; }
        PopSceneUIAuthoring.Validate(ui, AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath));
        if (scene.name == "Game" && ui.HasClassicHUD)
        {
            GameOverPanel result = null;
            foreach (var root in scene.GetRootGameObjects()) { result = root.GetComponentInChildren<GameOverPanel>(true); if (result != null) break; }
            if (result == null || !result.gameObject.activeInHierarchy || result.GetComponent<Canvas>() == null || result.GetComponent<GraphicRaycaster>() == null)
                throw new BuildFailedException("Classic Game Over requires its active host and local Canvas/GraphicRaycaster. Keep the UI host active; disable only the retired score/best/settings branches.");
        }
        var ambient = AssetDatabase.LoadAssetAtPath<PopAmbientConfiguration>(PopAmbientConfiguration.AssetPath);
        PopAmbientAuthoring.Validate(ambient);
        if (scene.name == "Game" && ambient != null && ambient.enabled)
        {
            PopAmbientBackground rig = null;
            foreach (var root in scene.GetRootGameObjects()) { rig = root.GetComponentInChildren<PopAmbientBackground>(true); if (rig != null) break; }
            if (rig == null || !rig.HasPresentation) throw new BuildFailedException("Missing authored automatic backgrounds. Run Tools > Blokki Pop > Backgrounds > Install Automatic Score Backgrounds.");
        }
    }
}
