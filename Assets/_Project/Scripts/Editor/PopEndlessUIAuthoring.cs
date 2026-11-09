using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time v2 upgrade of ONLY the Endless HUD. Subsequent installs preserve designer edits.</summary>
public static class PopEndlessUIAuthoring
{
    [MenuItem("Tools/Blokki Pop/UI/Install Endless Pop Preview")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Stop Play/Build before authoring UI.");
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        foreach (string path in new[] { PopSceneUIAuthoring.PrefabPath, PopJourneyHomeAuthoring.PrefabPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<PopGameplayUI>();
                var queue = root.GetComponentInChildren<PopEndlessQueueView>(true);
                if (queue == null)
                {
                    // The existing Safe Area has designer offsets. A new canvas-local overlay
                    // avoids inheriting them or compensating/moving any of the existing pages.
                    RectTransform overlay = Rect("Endless Pop Overlay", root.transform, Vector2.zero, Vector2.zero);
                    overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
                    RectTransform rect = Rect("Endless Pop Queue", overlay, new Vector2(920, 150), new Vector2(0, 385));
                    queue = rect.gameObject.AddComponent<PopEndlessQueueView>();
                }
                if (queue.presentationVersion < 2) BuildPresentation(queue, ui, config);
                EnsureLessonCoach(queue, config);
                var bindings = new SerializedObject(ui); bindings.FindProperty("endlessQueue").objectReferenceValue = queue; bindings.ApplyModifiedPropertiesWithoutUndo();
                ui.PreviewScenePage();
                if (config.endless.replaceJourneyHomeButton && ui.View(PopUIPage.Home).TryGetComponent<PopJourneyHomeView>(out var home))
                {
                    // One-time label migration; reruns preserve all later label/style edits.
                    if (home.journeyLabel == config.endless.play && home.journeyHint == "Giải bóng liên tục • Giữ bàn qua mỗi đợt")
                    { home.journeyLabel = "CHƠI POP JOURNEY"; home.journeyHint = "Chinh phục màn tiếp theo"; }
                    if (home.journeyLabel == "CHƠI POP JOURNEY")
                    { ui.View(PopUIPage.Home).Element("Journey").text.text = config.endless.play; home.journeySubtitle.text = "Giải bóng liên tục • Giữ bàn qua mỗi đợt"; }
                    var view=ui.View(PopUIPage.Home);
                    if(!view.elements.Exists(item=>item.key=="Practice"))
                    {
                        Transform panel=home.settings.transform.Find("Panel");
                        RectTransform practice=Rect("Practice",panel,new Vector2(810,80),new Vector2(0,56));
                        var image=practice.gameObject.AddComponent<PopHomeGraphic>();image.color=new Color(.05f,.24f,.32f);image.raycastTarget=true;
                        var button=practice.gameObject.AddComponent<Button>();button.targetGraphic=image;button.targetGraphic.canvasRenderer.cullTransparentMesh=false;
                        TMP_Text label=Text("Label",practice,"Bài luyện • "+config.journeyLevels.Count+" màn",new Vector2(760,70),Vector2.zero,home.soundStatus.font,35);
                        view.elements.Add(new PopUIElement{key="Practice",rect=practice,text=label,graphic=image,button=button});
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); Debug.Log("[Endless Pop] Polished authored HUD installed. Classic HUD transforms, scenes and player saves untouched. Rerunning preserves v2 layout edits.");
    }

    private static readonly Color Ink = new(.045f, .18f, .23f);
    private static void EnsureLessonCoach(PopEndlessQueueView queue, PopGameplayConfiguration config)
    {
        if (queue.lessonCoach != null) return; // Preserve every later designer edit.
        var font = queue.title.font;
        RectTransform card = Card("Lesson Coach", queue.header, new Vector2(960, 132), new Vector2(0, -168));
        queue.lessonCoach = card;
        queue.lessonInstruction = Text("Instruction", card, config.tutorial.endlessInstructions[0], new Vector2(735, 110), new Vector2(-85, 0), font, 32);
        queue.lessonInstruction.textWrappingMode = TextWrappingModes.Normal;
        queue.lessonInstruction.enableAutoSizing = true; queue.lessonInstruction.fontSizeMin = 28; queue.lessonInstruction.fontSizeMax = 32;
        RectTransform skip = Card("Skip", card, new Vector2(142, 66), new Vector2(390, 0));
        var face = skip.Find("Surface").GetComponent<PopHomeGraphic>(); face.raycastTarget = true;
        queue.lessonSkip = skip.gameObject.AddComponent<Button>(); queue.lessonSkip.targetGraphic = face;
        UnityEventTools.AddPersistentListener(queue.lessonSkip.onClick, queue.SkipLesson);
        Text("Label", skip, queue.skipLabel, new Vector2(135, 50), Vector2.zero, font, 22);
        card.gameObject.SetActive(false);
    }
    private static readonly Color Cyan = new(.48f, .91f, .94f);
    private static void BuildPresentation(PopEndlessQueueView queue, PopGameplayUI ui, PopGameplayConfiguration config)
    {
        // Explicit one-time replacement of the old overlapping Endless strip.
        // Never touch Safe Area, the original Hud/Card, scenes, board or tray.
        while (queue.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(queue.transform.GetChild(0).gameObject);
        if (queue.TryGetComponent<PopHomeGraphic>(out var oldBackground)) UnityEngine.Object.DestroyImmediate(oldBackground);
        RectTransform root = (RectTransform)queue.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        if (queue.noMoreText == "ĐÃ CẤP HẾT KHỐI CỦA ĐỢT NÀY") queue.noMoreText = "Đã hết";
        // A visible, standard layout component keeps the gameplay cluster together
        // on tall phones instead of leaving huge gaps inside the HUD. Designers
        // can edit/disable this fitter; no runtime script writes panel positions.
        RectTransform frame = Rect("Gameplay Frame", root, Vector2.zero, Vector2.zero);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
        var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 9f / 16;
        TMP_FontAsset body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopJourneyHomeAuthoring.ArtPath + "/Body.asset") ?? config.ui.bodyFont;
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopJourneyHomeAuthoring.ArtPath + "/Accent Bold.asset") ?? body;
        queue.header = Rect("Header", frame, new Vector2(960, 192), new Vector2(0, -118));
        queue.header.anchorMin = queue.header.anchorMax = new Vector2(.5f, 1);
        queue.header.anchoredPosition = new Vector2(0, -118);
        queue.header.localScale = Vector3.one * .92f;
        queue.title = Text("Mode Title", queue.header, "ENDLESS POP", new Vector2(580, 38), new Vector2(-186, 76), bold, 30);
        queue.title.alignment = TextAlignmentOptions.MidlineLeft; queue.title.color = Color.white; TextShadow(queue.title);
        queue.score = Text("Score", queue.header, "ĐIỂM  0", new Vector2(270, 38), new Vector2(334, 76), bold, 27);
        queue.score.alignment = TextAlignmentOptions.MidlineRight; queue.score.color = Color.white; TextShadow(queue.score);

        RectTransform wave = Card("Wave", queue.header, new Vector2(200, 106), new Vector2(-365, -7));
        Text("Label", wave, "ĐỢT", new Vector2(160, 30), new Vector2(0, 25), bold, 23).color = Cyan;
        queue.heading = Text("Value", wave, "01", new Vector2(160, 57), new Vector2(0, -17), bold, 48);
        RectTransform bubbles = Card("Bubbles", queue.header, new Vector2(284, 106), new Vector2(-107, -7));
        Text("Label", bubbles, "BÓNG CÒN", new Vector2(220, 30), new Vector2(0, 25), bold, 23).color = Cyan;
        Sprite bubbleSprite = ImportBubbleSprite();
        RectTransform icon = Rect("Bubble Icon", bubbles, new Vector2(55, 55), new Vector2(-43, -18));
        icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f); icon.anchoredPosition = new Vector2(-43, -18);
        var image = icon.gameObject.AddComponent<Image>(); image.sprite = bubbleSprite; image.preserveAspect = true; image.raycastTarget = false;
        queue.bubbleCount = Text("Value", bubbles, "3", new Vector2(92, 57), new Vector2(27, -17), bold, 48);
        queue.bubbleCount.color = new Color(1, .86f, .40f);
        RectTransform moves = Card("Moves", queue.header, new Vector2(284, 106), new Vector2(193, -7));
        Text("Label", moves, "LƯỢT CÒN", new Vector2(220, 30), new Vector2(0, 25), bold, 23).color = Cyan;
        queue.movesCount = Text("Value", moves, "6", new Vector2(190, 57), new Vector2(0, -17), bold, 48);
        RectTransform pause = Card("Pause", queue.header, new Vector2(96, 106), new Vector2(407, -7));
        PopHomeGraphic pauseFace = pause.Find("Surface").GetComponent<PopHomeGraphic>();
        pauseFace.color = new Color(1, .84f, .35f); pauseFace.BottomTint = new Color(.98f, .70f, .24f); pauseFace.raycastTarget = true;
        queue.pauseButton = pause.gameObject.AddComponent<Button>(); queue.pauseButton.targetGraphic = pauseFace;
        UnityEventTools.AddPersistentListener(queue.pauseButton.onClick, ui.ShowPause);
        for (int i = 0; i < 2; ++i)
        { RectTransform bar = Rect("Pause Bar " + (i + 1), pause, new Vector2(9, 32), new Vector2(i == 0 ? -10 : 10, 0)); bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f); bar.anchoredPosition = new Vector2(i == 0 ? -10 : 10, 0); var graphic = bar.gameObject.AddComponent<PopHomeGraphic>(); graphic.Radius = 3; graphic.color = Ink; graphic.raycastTarget = false; }

        queue.futurePanel = Rect("Upcoming Trays", frame, new Vector2(960, 174), new Vector2(0, 123));
        queue.futurePanel.localScale = Vector3.one * .92f;
        queue.pieces = new PopPiecePreviewGraphic[6];
        for (int group = 0; group < 2; ++group)
        {
            RectTransform card = Card(group == 0 ? "Next Tray" : "Later Tray", queue.futurePanel, new Vector2(464, 174), new Vector2(group == 0 ? -246 : 246, 0));
            TMP_Text label = Text("Label", card, group == 0 ? "TIẾP THEO" : "SAU NỮA", new Vector2(420, 34), new Vector2(0, 54), bold, 26);
            label.color = group == 0 ? Cyan : new Color(.78f, .87f, .88f);
            TMP_Text empty = Text("Empty", card, "Đã hết", new Vector2(360, 80), new Vector2(0, -19), body, 29);
            empty.color = new Color(.70f, .80f, .82f); empty.gameObject.SetActive(false);
            if (group == 0) queue.nextEmpty = empty; else queue.laterEmpty = empty;
            for (int slot = 0; slot < 3; ++slot)
            {
                int index = group * 3 + slot;
                RectTransform rect = Rect("Future Piece " + (index + 1), card, new Vector2(124, 90), new Vector2((slot - 1) * 142, -19));
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2((slot - 1) * 142, -19);
                var piece = rect.gameObject.AddComponent<PopPiecePreviewGraphic>();
                piece.SetShape(index + 2); piece.raycastTarget = false; piece.color = new Color(.94f, .56f, .37f);
                queue.pieces[index] = piece;
            }
        }
        queue.presentationVersion = 2;
    }
    private static void TextShadow(TMP_Text label)
    { var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.02f, .10f, .14f, .65f); shadow.effectDistance = new Vector2(0, -2); }
    private static RectTransform Card(string name, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform card = Rect(name, parent, size, position); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.anchoredPosition = position;
        RectTransform shadow = Rect("Shadow", card, size, new Vector2(0, -6)); shadow.anchorMin = shadow.anchorMax = new Vector2(.5f, .5f); shadow.anchoredPosition = new Vector2(0, -6);
        var shade = shadow.gameObject.AddComponent<PopHomeGraphic>(); shade.Radius = 27; shade.color = new Color(.02f, .10f, .14f, .24f); shade.raycastTarget = false;
        var edge = card.gameObject.AddComponent<PopHomeGraphic>(); edge.Radius = 27; edge.color = new Color(.35f, .62f, .66f); edge.raycastTarget = false;
        RectTransform surface = Rect("Surface", card, size - Vector2.one * 4, Vector2.zero); surface.anchorMin = surface.anchorMax = new Vector2(.5f, .5f); surface.anchoredPosition = Vector2.zero;
        var face = surface.gameObject.AddComponent<PopHomeGraphic>(); face.Radius = 25; face.color = new Color(.075f, .27f, .33f); face.BottomTint = Ink; face.raycastTarget = false;
        return card;
    }
    private static Sprite ImportBubbleSprite()
    {
        const string path = "Assets/_Project/UI/EndlessPop/bubble-gold.png";
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize != 256))
        { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 256; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing Endless Pop bubble sprite: " + path);
        return sprite;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static TMP_Text Text(string name, Transform parent, string text, Vector2 size, Vector2 position, TMP_FontAsset font, float fontSize)
    {
        RectTransform rect = Rect(name, parent, size, position); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = text; label.font = font;
        rect.anchoredPosition = position;
        label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false; return label;
    }
}

[CustomEditor(typeof(PopEndlessQueueView))]
public sealed class PopEndlessQueueInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Chỉnh Header (thông tin) và Upcoming Trays (hai khay xem trước) ngay trong Hierarchy. Camera chừa chỗ theo bounds thật của chúng, không đưa panel về tọa độ mặc định khi Play. Tắt Reserve Playfield nếu muốn tự căn camera.", MessageType.Info);
        DrawDefaultInspector();
        var queue = (PopEndlessQueueView)target;
        if (queue.header != null && GUILayout.Button("Chọn Header")) Selection.activeGameObject = queue.header.gameObject;
        if (queue.futurePanel != null && GUILayout.Button("Chọn Upcoming Trays")) Selection.activeGameObject = queue.futurePanel.gameObject;
        if (!Application.isPlaying && queue.lessonCoach != null && GUILayout.Button("Hiện / ẩn bài học Pop trong Scene"))
        {
            Undo.RecordObject(queue.lessonCoach.gameObject, "Preview Pop lesson coach");
            queue.lessonCoach.gameObject.SetActive(!queue.lessonCoach.gameObject.activeSelf);
            Selection.activeGameObject = queue.lessonCoach.gameObject;
        }
    }
}
