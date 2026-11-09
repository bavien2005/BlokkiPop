using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real Editor renders and pixel-overlap checks. Isolated QA project ONLY.</summary>
public static class PopEndlessPresentationValidation
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Vector2Int[] Sizes = { new(720, 1280), new(720, 1440), new(720, 1600), new(1080, 2400), new(768, 1024) };
    private static readonly List<string> results = new(), errors = new();
    private static readonly Dictionary<string, string> authored = new();
    private static string Output => Path.GetFullPath("../endless-ui-polish-20261016");
    private static int stage, sizeIndex, checks;
    private static double next, deadline;
    private static Rect safe;
    private static int objectsBefore;
    private static bool editProbe = true;
    private static float authoredTitleSize;
    private static Color authoredTitleColor;

    public static void RunRelease() { editProbe = false; Run(); }

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Presentation QA may not change the real project or player saves.");
        Directory.CreateDirectory(Output);
        PopEndlessUIAuthoring.Install(); PopClassicUIAuthoring.Install();
        // Prove a repeated installer preserves designer transforms/styles.
        GameObject root = PrefabUtility.LoadPrefabContents(PopSceneUIAuthoring.PrefabPath);
        try
        {
            var queue = root.GetComponentInChildren<PopEndlessQueueView>(true);
            if (editProbe)
            {
                queue.header.anchoredPosition += new Vector2(0, -4); queue.futurePanel.anchoredPosition += new Vector2(0, 8);
                queue.title.fontSize = 31; queue.title.color = new Color(.94f, 1, 1);
            }
            authoredTitleSize = queue.title.fontSize; authoredTitleColor = queue.title.color;
            PrefabUtility.SaveAsPrefabAsset(root, PopSceneUIAuthoring.PrefabPath);
            RecordTransforms(queue, authored);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        PopEndlessUIAuthoring.Install();
        root = PrefabUtility.LoadPrefabContents(PopSceneUIAuthoring.PrefabPath);
        try
        {
            var queue = root.GetComponentInChildren<PopEndlessQueueView>(true);
            CompareTransforms(queue);
            Check(Mathf.Approximately(queue.title.fontSize, authoredTitleSize), "Reinstall preserves authored label typography");
            Check(queue.HasPresentation, "All new HUD references are prefab-owned and bound");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        config.tutorial.automaticOnFirstEndless = false; // Dedicated onboarding QA covers the lesson layout.
        config.endless.fixedSeed = 293; config.endless.replaceJourneyHomeButton = true;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        ConfigureSize(Sizes[0]);
        Application.runInBackground = true;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 210;
        EditorApplication.EnterPlaymode(); Arm(0, .8);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at stage " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 9)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
                results.Add("ENDLESS PRESENTATION PASS: " + checks + " checks; five aspect ratios, notch insets, authored offsets, no tray/board overlap, Pause, Classic isolation, raycasts, exact previews.");
                File.WriteAllLines(Output + "/presentation-results.txt", results); Debug.Log(results[^1]); Finish(0); return;
            }
            var session = PopGameplaySession.Instance; var ui = PopGameplayUI.Instance;
            if (!EditorApplication.isPlaying || session == null || ui == null) return;
            var queue = ui.EndlessQueue;
            switch (stage)
            {
                case 0:
                    if (!ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    session.SelectTheme(session.Configuration.themes[0].id);
                    session.PlayEndless(false); Arm(1, .6); break;
                case 1:
                    if (SceneManager.GetActiveScene().buildIndex != 1 || !session.IsActive || !session.IsEndless) return;
                    objectsBefore = queue.GetComponentsInChildren<RectTransform>(true).Length;
                    Arm(2, .3); break;
                case 2:
                    if (Screen.width != Sizes[sizeIndex].x || Screen.height != Sizes[sizeIndex].y) return;
                    Canvas.ForceUpdateCanvases(); Object.FindFirstObjectByType<ResponsiveGameLayout>().Refresh();
                    safe = Screen.safeArea; CheckLayout(ui, safe);
                    ScreenCapture.CaptureScreenshot(Output + "/endless-" + Screen.width + "x" + Screen.height + ".png");
                    Arm(3, .3); break;
                case 3:
                    if (++sizeIndex < Sizes.Length) { ConfigureSize(Sizes[sizeIndex]); Arm(2, .5); }
                    else { ConfigureSize(new Vector2Int(720, 1600)); Arm(4, .5); }
                    break;
                case 4:
                    if (Screen.width != 720 || Screen.height != 1600) return;
                    safe = new Rect(0, 40, Screen.width, Screen.height - 124);
                    Vector2 position = ((RectTransform)queue.transform).anchoredPosition, size = ((RectTransform)queue.transform).sizeDelta;
                    queue.fitDeviceSafeArea = false;
                    typeof(PopEndlessQueueView).GetMethod("FitSafeArea", Flags).Invoke(queue, new object[] { safe, Screen.width, Screen.height });
                    Check(((RectTransform)queue.transform).anchoredPosition == position && ((RectTransform)queue.transform).sizeDelta == size, "Notch fitting preserves authored root offsets/size");
                    Arm(5, .3); break;
                case 5:
                    Canvas.ForceUpdateCanvases(); Object.FindFirstObjectByType<ResponsiveGameLayout>().Refresh(); CheckLayout(ui, safe);
                    ScreenCapture.CaptureScreenshot(Output + "/endless-safe-area.png");
                    queue.pauseButton.onClick.Invoke();
                    Check(ui.View(PopUIPage.Pause).gameObject.activeInHierarchy && Time.timeScale == 0 && !queue.gameObject.activeInHierarchy, "New authored Pause button pauses and hides whole Endless HUD");
                    ui.View(PopUIPage.Pause).Element("Resume").button.onClick.Invoke();
                    Check(queue.gameObject.activeInHierarchy && Time.timeScale > 0, "Resume restores dedicated HUD");
                    queue.fitDeviceSafeArea = true;
                    // Read exact future queue after a real consumed tray piece.
                    var blocks = Object.FindFirstObjectByType<Blocks>(); var step = session.Definition.verifiedSolution[0];
                    session.SaveCurrentRun(); var saved = session.SavedRun(PopGameMode.EndlessPop);
                    int slot = -1; for (int i = 0; i < 3; ++i) if (saved.trayAvailable[i] && saved.trayShapes[i] == step.shape) { slot = i; break; }
                    Check(slot >= 0 && blocks.TryPlaceSlot(slot, step.origin), "Real placement still works with reserved playfield and new overlay");
                    Check(queue.GetComponentsInChildren<RectTransform>(true).Length == objectsBefore, "Refresh/Pause never creates or replaces authored UI");
                    CompareTransforms(queue);
                    session.PlayClassic(false); Arm(6, .7); break;
                case 6:
                    if (session.Mode != PopGameMode.Classic || !session.IsActive || SceneManager.GetActiveScene().buildIndex != 1) return;
                    Check(!queue.gameObject.activeInHierarchy && ui.ClassicHUD.gameObject.activeInHierarchy && !ui.View(PopUIPage.Hud).gameObject.activeInHierarchy, "Classic uses its authored HUD, never Endless overlays or the old footer");
                    foreach (TextScore text in Object.FindObjectsByType<TextScore>(FindObjectsSortMode.None)) Check(!text.GetComponent<TMPro.TMP_Text>().enabled && text.GetComponentInParent<Canvas>().rootCanvas.enabled, "Classic hides old score artwork, not the combo/fever canvas");
                    Canvas.ForceUpdateCanvases(); Object.FindFirstObjectByType<ResponsiveGameLayout>().Refresh();
                    Check(ui.ClassicHUD.TryGetPlayfieldRect(Screen.safeArea, out Rect classicArea), "Classic reserves its own authored panels instead of the Endless preview");
                    Check(Inside(classicArea, WorldBounds(new Bounds(new Vector3(4, 4, 0), new Vector3(8.5f, 8.5f, 0)))), "Classic board fits its own playfield");
                    EditorApplication.ExitPlaymode(); Arm(9, .3); break;
            }
        }
        catch (Exception e) { results.Add("ENDLESS PRESENTATION FAIL: " + e); File.WriteAllLines(Output + "/presentation-results.txt", results); Debug.LogError(results[^1]); Finish(1); }
    }
    private static void CheckLayout(PopGameplayUI ui, Rect safeArea)
    {
        var queue = ui.EndlessQueue; CompareTransforms(queue);
        Rect header = PopEndlessQueueView.ScreenBounds(queue.header), future = PopEndlessQueueView.ScreenBounds(queue.futurePanel);
        Check(Inside(safeArea, header) && Inside(safeArea, future), "Header/preview inside safe area at " + Screen.width + "x" + Screen.height);
        var blocks = Object.FindFirstObjectByType<Blocks>();
        var renderer = (SpriteRenderer)typeof(Blocks).GetField("trayRenderer", Flags).GetValue(blocks);
        Rect tray = WorldBounds(renderer.bounds), board = WorldBounds(new Bounds(new Vector3(4, 4, 0), new Vector3(8.5f, 8.5f, 0)));
        Check(!header.Overlaps(tray) && !future.Overlaps(tray) && !header.Overlaps(board) && !future.Overlaps(board), "No authored HUD/preview overlaps the actual world board/tray");
        Check(tray.yMin > future.yMax && board.yMax < header.yMin, "Playfield has visible space above and below");
        Check(Inside(safeArea, tray) && Inside(safeArea, board), "Complete board and tray remain visible");
        Check(!ui.View(PopUIPage.Hud).gameObject.activeInHierarchy, "No duplicate bottom HUD");
        foreach (TextScore text in Object.FindObjectsByType<TextScore>(FindObjectsSortMode.None)) Check(!text.GetComponentInParent<Canvas>().rootCanvas.enabled, "No unrelated Classic score over the new HUD");
        for (int i = 0; i < 6; ++i)
        { int index = PopGameplaySession.Instance.DeckCursor + i; var deck = PopGameplaySession.Instance.Definition.pieceSequence; Check(queue.pieces[i].ShapeId == (index < deck.Count ? deck[index] : -1), "Exact queued shape/orientation " + i); }
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = tray.center }, hits);
        Check(!hits.Exists(hit => hit.gameObject.transform.IsChildOf(queue.transform)), "Decorative HUD/preview does not steal tray touches");
        foreach (TMPro.TMP_Text label in queue.GetComponentsInChildren<TMPro.TMP_Text>())
        { label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "No overflowing HUD text: " + label.name); Check(!label.text.Contains("Xanh:") && !label.text.Contains("Chưa kéo"), "No confusing color legend remains"); }
        Check(queue.title.fontSize == authoredTitleSize && queue.title.color == authoredTitleColor, "Authored text font size/color survives Play");
        results.Add(Screen.width + "x" + Screen.height + ": header=" + header + "; board=" + board + "; tray=" + tray + "; future=" + future);
    }
    private static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
    private static Rect WorldBounds(Bounds bounds)
    { Vector3 min = Camera.main.WorldToScreenPoint(bounds.min), max = Camera.main.WorldToScreenPoint(bounds.max); return Rect.MinMaxRect(min.x, min.y, max.x, max.y); }
    private static string Pose(RectTransform rect) => rect.anchorMin + "|" + rect.anchorMax + "|" + rect.anchoredPosition + "|" + rect.sizeDelta + "|" + rect.pivot + "|" + rect.localScale;
    private static void RecordTransforms(PopEndlessQueueView queue, Dictionary<string, string> values)
    { foreach (RectTransform rect in queue.GetComponentsInChildren<RectTransform>(true)) if (rect != queue.transform && rect.GetComponent<UnityEngine.UI.AspectRatioFitter>() == null) values[AnimationUtility.CalculateTransformPath(rect, queue.transform)] = Pose(rect); }
    private static void CompareTransforms(PopEndlessQueueView queue)
    { foreach (RectTransform rect in queue.GetComponentsInChildren<RectTransform>(true)) if (rect != queue.transform && rect.GetComponent<UnityEngine.UI.AspectRatioFitter>() == null) Check(authored[AnimationUtility.CalculateTransformPath(rect, queue.transform)] == Pose(rect), "Authored transform preserved: " + rect.name); }
    private static void ConfigureSize(Vector2Int dimensions)
    {
        Assembly editor = typeof(EditorWindow).Assembly; Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        object sizes = sizesType.BaseType.GetProperty("instance", Flags).GetValue(null);
        object group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), Flags, null, new object[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), dimensions.x, dimensions.y, "Endless UI " + dimensions }, null);
        group.GetType().GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null) - 1;
        Type viewType = editor.GetType("UnityEditor.GameView"); EditorWindow view = EditorWindow.GetWindow(viewType); view.position = new Rect(0, 0, 450, 950);
        viewType.GetProperty("selectedSizeIndex", Flags).SetValue(view, index);
    }
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void Arm(int value, double delay) { stage = value; next = EditorApplication.timeSinceStartup + delay; }
    private static void OnLog(string text, string stack, LogType type) { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(text); }
    private static void Finish(int code) { EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(code); }
}
