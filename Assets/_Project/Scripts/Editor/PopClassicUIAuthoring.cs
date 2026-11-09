using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Additive, repeat-safe prefab upgrade. No scene or player data writes.</summary>
public static class PopClassicUIAuthoring
{
    private static readonly Color Ink = new(.035f, .16f, .21f), Teal = new(.075f, .28f, .34f), Rim = new(.35f, .65f, .69f);
    private static readonly Color Gold = new(1, .84f, .34f), Cyan = new(.54f, .91f, .93f);
    private static TMP_FontAsset body, bold;

    [MenuItem("Tools/Blokki Pop/UI/Install Classic HUD and Pause Sheet")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Stop Play/Build before authoring UI.");
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopJourneyHomeAuthoring.ArtPath + "/Body.asset") ?? config.ui.bodyFont;
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopJourneyHomeAuthoring.ArtPath + "/Accent Bold.asset") ?? body;
        foreach (string path in new[] { PopSceneUIAuthoring.PrefabPath, PopJourneyHomeAuthoring.PrefabPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<PopGameplayUI>();
                var hud = root.GetComponentInChildren<PopClassicHUDView>(true);
                bool added = hud == null;
                if (added) hud = BuildHUD(root.transform, ui);
                var bindings = new SerializedObject(ui); bindings.FindProperty("classicHUD").objectReferenceValue = hud;
                if (added) bindings.FindProperty("previewEndlessHUD").boolValue = false;
                bindings.ApplyModifiedPropertiesWithoutUndo();
                if (ui.View(PopUIPage.Pause).GetComponent<PopPausePresentation>() == null) BuildPause(root.transform, ui);
                ui.PreviewScenePage(); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); Debug.Log("[Classic UI] Authored Classic HUD + Pause sheet installed. Reinstall preserves edits. Scenes, board/tray, saves and ads untouched.");
    }

    private static PopClassicHUDView BuildHUD(Transform parent, PopGameplayUI ui)
    {
        RectTransform root = Rect("Classic Overlay", parent, Vector2.zero, Vector2.zero); Stretch(root);
        var hud = root.gameObject.AddComponent<PopClassicHUDView>();
        RectTransform frame = Frame("Gameplay Frame", root);
        hud.header = Rect("Header", frame, new Vector2(960, 330), new Vector2(0, -190));
        hud.header.anchorMin = hud.header.anchorMax = new Vector2(.5f, 1); hud.header.anchoredPosition = new Vector2(0, -190); hud.header.localScale = Vector3.one * .92f;
        TMP_Text mode = Label("Mode", hud.header, "CLASSIC", new Vector2(340, 36), new Vector2(0, 110), 29); mode.characterSpacing = 6; Shadow(mode);
        RectTransform best = Card("Best", hud.header, new Vector2(284, 86), new Vector2(-314, 106), 43);
        Glyph("Crown", best, PopHomeGraphic.Kind.Crown, new Vector2(44, 44), new Vector2(-96, 0), Gold);
        Label("Label", best, "KỶ LỤC", new Vector2(190, 25), new Vector2(27, 21), 20).color = Cyan;
        hud.best = Label("Value", best, "3,913", new Vector2(190, 43), new Vector2(27, -17), 36);
        Label("Score Label", hud.header, "ĐIỂM", new Vector2(400, 32), new Vector2(0, 64), 24).color = new Color(.87f, .98f, 1);
        hud.score = Label("Score", hud.header, "0", new Vector2(790, 150), new Vector2(0, -25), 108); Shadow(hud.score);
        hud.newRecord = Label("New Record", hud.header, hud.recordText, new Vector2(660, 36), new Vector2(0, -110), 26); hud.newRecord.color = Gold; hud.newRecord.gameObject.SetActive(false);
        RectTransform pause = Card("Pause", hud.header, new Vector2(96, 96), new Vector2(418, 106), 48, true);
        hud.pauseButton = ButtonOn(pause); UnityEventTools.AddPersistentListener(hud.pauseButton.onClick, ui.ShowPause);
        Glyph("Pause Icon", pause, PopHomeGraphic.Kind.Pause, new Vector2(43, 43), Vector2.zero, Ink);

        hud.stats = Rect("Stats", frame, new Vector2(960, 104), new Vector2(0, 96));
        hud.stats.anchorMin = hud.stats.anchorMax = new Vector2(.5f, 0); hud.stats.anchoredPosition = new Vector2(0, 96); hud.stats.localScale = Vector3.one * .92f;
        RectTransform lines = Card("Lines", hud.stats, new Vector2(430, 92), new Vector2(-244, 0), 38);
        Glyph("Rows Icon", lines, PopHomeGraphic.Kind.Rows, new Vector2(38, 38), new Vector2(-175, 0), Cyan);
        Label("Label", lines, "HÀNG ĐÃ XÓA", new Vector2(210, 56), new Vector2(-35, 0), 23).alignment = TextAlignmentOptions.MidlineLeft;
        hud.lines = Label("Value", lines, "0", new Vector2(90, 62), new Vector2(150, 0), 44); hud.lines.color = Gold;
        RectTransform combo = Card("Best Combo", hud.stats, new Vector2(430, 92), new Vector2(244, 0), 38);
        Glyph("Combo Icon", combo, PopHomeGraphic.Kind.Blocks, new Vector2(38, 38), new Vector2(-175, 0), Cyan);
        Label("Label", combo, "COMBO CAO NHẤT", new Vector2(218, 56), new Vector2(-31, 0), 22).alignment = TextAlignmentOptions.MidlineLeft;
        hud.maxCombo = Label("Value", combo, "×0", new Vector2(100, 62), new Vector2(148, 0), 43); hud.maxCombo.color = Gold;
        return hud;
    }

    private static void BuildPause(Transform parent, PopGameplayUI ui)
    {
        // The old page has scene offsets compensating for the old Safe Area.
        // Reparenting that transform would carry those overrides onto the new
        // sheet. Preserve it intact, and bind a fresh authored page instead.
        PopUIView legacy = ui.View(PopUIPage.Pause);
        legacy.name = "Legacy Pause (preserved)"; legacy.gameObject.SetActive(false);
        RectTransform root = Rect("Pause", parent, Vector2.zero, Vector2.zero); Stretch(root);
        var view = root.gameObject.AddComponent<PopUIView>(); view.page = PopUIPage.Pause;
        var bindings = new SerializedObject(ui); var pages = bindings.FindProperty("views");
        for (int i = 0; i < pages.arraySize; ++i)
            if (pages.GetArrayElementAtIndex(i).objectReferenceValue == legacy) pages.GetArrayElementAtIndex(i).objectReferenceValue = view;
        bindings.ApplyModifiedPropertiesWithoutUndo();
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(.015f, .055f, .08f, .78f); dim.raycastTarget = true;
        var presentation = root.gameObject.AddComponent<PopPausePresentation>();
        presentation.safeRoot = Rect("Modal Safe Area", root, Vector2.zero, Vector2.zero); Stretch(presentation.safeRoot);
        RectTransform frame = Frame("Modal Frame", presentation.safeRoot);
        RectTransform card = Rect("Card", frame, new Vector2(820, 1060), Vector2.zero);
        StyleCard(card, 44, false); presentation.card = card;
        view.elements.Clear(); view.title = null;
        RectTransform badge = Card("Pause Emblem", card, new Vector2(86, 86), new Vector2(0, 414), 43, true);
        Glyph("Icon", badge, PopHomeGraphic.Kind.Pause, new Vector2(36, 36), Vector2.zero, Ink);
        AddText(view, "Title", Label("Title", card, presentation.heading, new Vector2(700, 80), new Vector2(0, 314), 60));
        AddText(view, "Body", Label("Body", card, presentation.notice, new Vector2(730, 62), new Vector2(0, 242), 29, false));
        RectTransform resume = AddButton(view, "Resume", card, presentation.resume, new Vector2(700, 104), new Vector2(0, 114), true, 36);
        Glyph("Play Icon", resume, PopHomeGraphic.Kind.Play, new Vector2(30, 30), new Vector2(-240, 0), Ink);
        AddButton(view, "Home", card, presentation.home, new Vector2(700, 88), new Vector2(0, -10), false, 31);
        RectTransform sound = AddButton(view, "Sound", card, "Âm thanh", new Vector2(330, 78), new Vector2(-185, -130), false, 28);
        RectTransform haptics = AddButton(view, "Haptics", card, "Rung", new Vector2(330, 78), new Vector2(185, -130), false, 28);
        foreach (string key in new[] { "Sound", "Haptics" }) { view.Element(key).text.rectTransform.sizeDelta = new Vector2(190, 60); view.Element(key).text.rectTransform.anchoredPosition = new Vector2(-40, 0); }
        presentation.soundStatus = Label("Status", sound, "BẬT", new Vector2(80, 50), new Vector2(105, 0), 24); presentation.soundStatus.color = Gold;
        presentation.hapticsStatus = Label("Status", haptics, "BẬT", new Vector2(80, 50), new Vector2(105, 0), 24); presentation.hapticsStatus.color = Gold;
        UnityEventTools.AddPersistentListener(view.Element("Sound").button.onClick, presentation.ToggleSound);
        UnityEventTools.AddPersistentListener(view.Element("Haptics").button.onClick, presentation.ToggleHaptics);
        Label("Volume Label", card, "Âm lượng", new Vector2(170, 48), new Vector2(-260, -235), 26, false);
        presentation.volume = VolumeSlider(card, new Vector2(65, -235));
        UnityEventTools.AddPersistentListener(presentation.volume.onValueChanged, presentation.SetVolume);
        AddButton(view, "Rewards", card, presentation.rewards, new Vector2(330, 76), new Vector2(-185, -352), false, 27);
        AddButton(view, "Privacy", card, presentation.privacy, new Vector2(330, 76), new Vector2(185, -352), false, 27);
        RectTransform close = AddButton(view, "Close", card, "", new Vector2(64, 64), new Vector2(350, 440), false, 26);
        Glyph("Close Icon", close, PopHomeGraphic.Kind.Close, new Vector2(30, 30), Vector2.zero, Color.white);
    }
    private static Slider VolumeSlider(Transform parent, Vector2 position)
    {
        RectTransform root = Rect("SFX Volume", parent, new Vector2(490, 76), position);
        var hitArea = root.gameObject.AddComponent<Image>(); hitArea.color = new Color(0, 0, 0, .001f); hitArea.raycastTarget = true;
        RectTransform track = Rect("Track", root, new Vector2(490, 14), Vector2.zero); var background = track.gameObject.AddComponent<PopHomeGraphic>(); background.Radius = 7; background.color = new Color(.02f, .10f, .14f); background.raycastTarget = true;
        RectTransform fillArea = Rect("Fill Area", root, new Vector2(466, 14), Vector2.zero);
        RectTransform fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero); Stretch(fill); var fillGraphic = fill.gameObject.AddComponent<PopHomeGraphic>(); fillGraphic.Radius = 7; fillGraphic.color = Cyan; fillGraphic.raycastTarget = false;
        RectTransform handleArea = Rect("Handle Area", root, new Vector2(466, 30), Vector2.zero);
        // Slider drives handle Y anchors to 0..1; zero Y sizeDelta yields an
        // actual 30x30 circle, not an oval stretched over the 76-unit hit area.
        RectTransform handle = Rect("Handle", handleArea, new Vector2(30, 0), Vector2.zero); var knob = handle.gameObject.AddComponent<PopHomeGraphic>(); knob.Shape = PopHomeGraphic.Kind.Circle; knob.color = Gold; knob.raycastTarget = true;
        var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = knob; slider.value = 1; return slider;
    }
    private static void AddText(PopUIView view, string key, TMP_Text text)
    { view.elements.Add(new PopUIElement { key = key, rect = text.rectTransform, text = text, graphic = text }); if (key == "Title") view.title = text; }
    private static RectTransform AddButton(PopUIView view, string key, Transform parent, string label, Vector2 size, Vector2 position, bool primary, float fontSize)
    {
        RectTransform rect = Card(key, parent, size, position, 26, primary); var button = ButtonOn(rect);
        TMP_Text text = Label("Label", rect, label, size - new Vector2(40, 6), Vector2.zero, fontSize); if (primary) text.color = Ink;
        view.elements.Add(new PopUIElement { key = key, rect = rect, button = button, text = text, graphic = button.targetGraphic }); return rect;
    }
    private static Button ButtonOn(RectTransform root)
    { var face = root.Find("Surface").GetComponent<PopHomeGraphic>(); face.raycastTarget = true; var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = face; return button; }
    private static RectTransform Frame(string name, Transform parent)
    { RectTransform frame = Rect(name, parent, Vector2.zero, Vector2.zero); Stretch(frame); var fit = frame.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 9f / 16; return frame; }
    private static RectTransform Card(string name, Transform parent, Vector2 size, Vector2 position, float radius, bool gold = false)
    { RectTransform rect = Rect(name, parent, size, position); StyleCard(rect, radius, gold); return rect; }
    private static void StyleCard(RectTransform rect, float radius, bool gold)
    {
        RectTransform shadow = Rect("Shadow", rect, rect.sizeDelta, new Vector2(0, -7)); var shade = shadow.gameObject.AddComponent<PopHomeGraphic>(); shade.Radius = radius; shade.color = new Color(.015f, .07f, .11f, .30f); shade.raycastTarget = false;
        var edge = rect.gameObject.AddComponent<PopHomeGraphic>(); edge.Radius = radius; edge.color = gold ? new Color(1, .93f, .63f) : Rim; edge.raycastTarget = false;
        RectTransform surface = Rect("Surface", rect, rect.sizeDelta - Vector2.one * 4, Vector2.zero); var face = surface.gameObject.AddComponent<PopHomeGraphic>(); face.Radius = Mathf.Max(0, radius - 2); face.color = gold ? Gold : Teal; face.BottomTint = gold ? new Color(.98f, .70f, .25f) : Ink; face.raycastTarget = false;
    }
    private static PopHomeGraphic Glyph(string name, Transform parent, PopHomeGraphic.Kind kind, Vector2 size, Vector2 position, Color color)
    { var rect = Rect(name, parent, size, position); var graphic = rect.gameObject.AddComponent<PopHomeGraphic>(); graphic.Shape = kind; graphic.color = color; graphic.raycastTarget = false; return graphic; }
    private static TMP_Text Label(string name, Transform parent, string copy, Vector2 size, Vector2 position, float fontSize, bool strong = true)
    { var rect = Rect(name, parent, size, position); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = strong ? bold : body; text.text = copy; text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text; }
    private static void Shadow(TMP_Text text) { var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.015f, .10f, .15f, .65f); shadow.effectDistance = new Vector2(0, -3); }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    { var root = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)root.transform; rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect; }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
