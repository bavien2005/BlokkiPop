using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edit/Play screen-space layout regression. Only runs in the isolated QA project.</summary>
public static class PopUILayoutValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int stage, scenario, checks;
    private static double next, deadline;
    private static Rect hudBefore, pauseBefore;
    private static Vector2 parentPosition, parentSize, hudPosition, hudSize, hudMin, hudMax;
    private static readonly List<string> results = new();
    private static readonly List<string> errors = new();
    private static string Output => Path.GetFullPath("../scene-ui-20261015/layout-fix");

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Layout tests may only modify the isolated QA project, never the user's scene/preferences.");
        Directory.CreateDirectory(Output);
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Application.runInBackground = true;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.isPaused = false;
        typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow").GetMethod("SetConsoleErrorPause", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { false });
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        deadline = EditorApplication.timeSinceStartup + 180;
        OpenScenario();
    }

    private static void OpenScenario()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
        PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>();
        RectTransform parent = Parent(ui), hud = (RectTransform)ui.View(PopUIPage.Hud).transform;
        if (scenario == 1)
        {
            // Reproduce both translated parent and stretched HUD from the user's screenshots.
            parent.anchoredPosition = new Vector2(4, -1493); parent.sizeDelta = new Vector2(11, -17);
            hud.anchorMin = new Vector2(.5f, 0); hud.anchorMax = new Vector2(.5f, 1); hud.pivot = new Vector2(.5f, .5f);
            hud.sizeDelta = new Vector2(950, -1973); hud.anchoredPosition = new Vector2(0, 641.5f);
        }
        else if (scenario == 2)
        {
            // Independently moving only the HUD must not require moving its shared parent.
            parent.anchoredPosition = Vector2.zero; parent.sizeDelta = Vector2.zero;
            hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(.5f, 0);
            hud.sizeDelta = new Vector2(950, 145); hud.anchoredPosition = new Vector2(0, 190);
        }
        TestInsets(ui);
        PrefabUtility.RecordPrefabInstancePropertyModifications(parent);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        ui.ScenePreview = PopUIPage.Hud; ui.PreviewScenePage();
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveScene(ui.gameObject.scene);
        // Reopen to prove edits were actually serialized, not just kept in a test object.
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
        stage = 0; next = EditorApplication.timeSinceStartup + .75;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Layout validation timeout at " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (stage == 0)
            {
                PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>();
                ui.PreviewScenePage(); Canvas.ForceUpdateCanvases();
                RectTransform parent = Parent(ui), hud = (RectTransform)ui.View(PopUIPage.Hud).transform;
                parentPosition = parent.anchoredPosition; parentSize = parent.sizeDelta;
                hudPosition = hud.anchoredPosition; hudSize = hud.sizeDelta; hudMin = hud.anchorMin; hudMax = hud.anchorMax;
                hudBefore = Pixels(hud); pauseBefore = Pixels(ui.View(PopUIPage.Hud).Element("Pause").rect);
                results.Add($"scenario {scenario} EDIT screen={Screen.width}x{Screen.height}, safe={Screen.safeArea}, parent={parentPosition}, HUD={hudBefore}, Pause={pauseBefore}");
                Check(hudBefore.width > 0 && pauseBefore.width > 0, "Measured actual rendered rectangles, not only Inspector values");
                EditorApplication.EnterPlaymode(); stage = 1; next = EditorApplication.timeSinceStartup + 1.2;
            }
            else if (stage == 1)
            {
                if (!EditorApplication.isPlaying || PopGameplayUI.Instance == null || !PopGameplayUI.Instance.View(PopUIPage.Hud).gameObject.activeInHierarchy) return;
                PopGameplayUI ui = PopGameplayUI.Instance; Canvas.ForceUpdateCanvases();
                RectTransform parent = Parent(ui), hud = (RectTransform)ui.View(PopUIPage.Hud).transform;
                Check(parent.anchoredPosition == parentPosition && parent.sizeDelta == parentSize, "Play preserves designer offsets/size on shared Safe Area parent");
                Check(hud.anchoredPosition == hudPosition && hud.sizeDelta == hudSize && hud.anchorMin == hudMin && hud.anchorMax == hudMax, "Play preserves designer HUD anchors/offsets/size");
                Rect hudAfter = Pixels(hud), pauseAfter = Pixels(ui.View(PopUIPage.Hud).Element("Pause").rect);
                Check(Near(hudBefore, hudAfter), "HUD screen-space position and size match edit preview within 0.6 pixels");
                Check(Near(pauseBefore, pauseAfter), "Pause button screen-space position and size match edit preview within 0.6 pixels");
                results.Add($"scenario {scenario} PLAY HUD={hudAfter}, Pause={pauseAfter}");
                if (scenario == 2) Check(hudAfter.center.y < Screen.height * .25f, "A HUD authored below the board really remains at the bottom in Play");
                ScreenCapture.CaptureScreenshot(Output + "/hud-play-" + scenario + ".png");
                stage = 2; next = EditorApplication.timeSinceStartup + .6;
            }
            else if (stage == 2)
            {
                EditorApplication.ExitPlaymode(); stage = 3; next = EditorApplication.timeSinceStartup + .7;
            }
            else if (stage == 3)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>(); ui.PreviewScenePage(); Canvas.ForceUpdateCanvases();
                Check(Parent(ui).anchoredPosition == parentPosition && Parent(ui).sizeDelta == parentSize, "Stop restores the authored parent without zeroing edits");
                Check(Near(Pixels((RectTransform)ui.View(PopUIPage.Hud).transform), hudBefore), "HUD returns to the same edit-preview pixels after Stop");
                if (++scenario < 3) OpenScenario();
                else
                {
                    Check(errors.Count == 0, "No layout/runtime errors: " + string.Join("; ", errors));
                    results.Add("LAYOUT VALIDATION PASS: " + checks + " checks; 3 saved/reopened Edit-Play-Stop scenarios; actual HUD/Pause pixel rectangles; full-screen/notch/rotated insets.");
                    File.WriteAllLines(Output + "/layout-results.txt", results);
                    Debug.Log(results[^1]); Finish(0);
                }
            }
        }
        catch (Exception exception)
        {
            results.Add("LAYOUT VALIDATION FAILED: " + exception); File.WriteAllLines(Output + "/layout-results.txt", results);
            Debug.LogError(results[^1]); Finish(1);
        }
    }

    private static void TestInsets(PopGameplayUI ui)
    {
        RectTransform parent = Parent(ui), hud = (RectTransform)ui.View(PopUIPage.Hud).transform;
        Vector2 position = parent.anchoredPosition, size = parent.sizeDelta, pivot = parent.pivot;
        Vector2 childPosition = hud.anchoredPosition, childSize = hud.sizeDelta;
        ui.enabled = false; // Do not let the render callback replace injected cutouts with the current Game View.
        if (position.sqrMagnitude > 1)
        {
            Canvas.ForceUpdateCanvases(); Rect before = Pixels(hud);
            parent.offsetMin = parent.offsetMax = Vector2.zero; Canvas.ForceUpdateCanvases();
            Check(hud.anchoredPosition == childPosition && !Near(before, Pixels(hud)), "Negative control reproduces the old bug: zeroing the parent moves visible HUD despite unchanged child Inspector values");
            results.Add($"scenario {scenario} OLD RESET reproduced: HUD pixels {before} -> {Pixels(hud)} while child position stays {childPosition}");
            parent.anchoredPosition = position; parent.sizeDelta = size;
        }
        MethodInfo fit = typeof(PopGameplayUI).GetMethod("FitSafeAreaAnchors", Private);
        var areas = new[] { new Rect(0, 0, 720, 1600), new Rect(0, 102, 1170, 2289), new Rect(141, 0, 2289, 1170), new Rect(0, 0, 720, 1600) };
        var sizes = new[] { new Vector2Int(720, 1600), new Vector2Int(1170, 2532), new Vector2Int(2532, 1170), new Vector2Int(720, 1600) };
        for (int i = 0; i < areas.Length; ++i)
        {
            fit.Invoke(ui, new object[] { areas[i], sizes[i].x, sizes[i].y });
            Check(parent.anchoredPosition == position && parent.sizeDelta == size && parent.pivot == pivot, "Safe-area/rotation changes never zero designer parent offsets, size or pivot");
            Check(hud.anchoredPosition == childPosition && hud.sizeDelta == childSize, "Safe-area changes do not rewrite child layout");
            Check(parent.anchorMin == new Vector2(areas[i].xMin / sizes[i].x, areas[i].yMin / sizes[i].y) && parent.anchorMax == new Vector2(areas[i].xMax / sizes[i].x, areas[i].yMax / sizes[i].y), "Safe-area anchors still avoid simulated cutouts");
        }
        ui.enabled = true; Canvas.ForceUpdateCanvases();
        var serialized = new SerializedObject(ui); serialized.FindProperty("fitDeviceSafeArea").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
        parent.anchorMin = new Vector2(.1f, .2f); parent.anchorMax = new Vector2(.8f, .9f); Canvas.ForceUpdateCanvases();
        Check(parent.anchorMin == new Vector2(.1f, .2f) && parent.anchorMax == new Vector2(.8f, .9f), "Turning Fit Device Safe Area off allows manual anchors too");
        serialized.Update(); serialized.FindProperty("fitDeviceSafeArea").boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo(); Canvas.ForceUpdateCanvases();
    }

    private static RectTransform Parent(PopGameplayUI ui) => (RectTransform)typeof(PopGameplayUI).GetField("safeRoot", Private).GetValue(ui);
    private static Rect Pixels(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]), max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private static bool Near(Rect a, Rect b) => Mathf.Abs(a.x - b.x) < .6f && Mathf.Abs(a.y - b.y) < .6f && Mathf.Abs(a.width - b.width) < .6f && Mathf.Abs(a.height - b.height) < .6f;
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void OnLog(string text, string stack, LogType type)
    { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(text); }
    private static void Finish(int exit)
    { EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(exit); }
}
