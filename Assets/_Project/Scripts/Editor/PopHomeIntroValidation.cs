using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated Home intro/menu regression: actual falling cells, toggle lifecycle and preloaded navigation.</summary>
public static class PopHomeIntroValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int scenario, stage, checks;
    private static double next, deadline;
    private static Vector2[] authoredTargets, previousPositions;
    private static bool sawDescent, sawUncoveredDescent, toggledOff, toggledOn, capturedFalling;
    private static readonly List<string> results = new();
    private static readonly List<string> errors = new();
    private static string Output => Path.GetFullPath("../scene-ui-20261015/home-intro-fix");

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Home intro tests may only change the isolated QA project.");
        Directory.CreateDirectory(Output);
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Application.runInBackground = true;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow").GetMethod("SetConsoleErrorPause", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { false });
        EditorApplication.isPaused = false;
        EditorApplication.update += Tick; Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 150;
        OpenScenario();
    }

    private static void OpenScenario()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        BlokkiPopIntroController intro = Object.FindFirstObjectByType<BlokkiPopIntroController>();
        RectTransform[] cells = Read<RectTransform[]>(intro, "animatedCells");
        authoredTargets = new Vector2[cells.Length]; previousPositions = new Vector2[cells.Length];
        for (int i = 0; i < cells.Length; ++i) authoredTargets[i] = cells[i].anchoredPosition;
        Check(cells.Length == 6, "Home scene has six authored falling colored blocks");
        PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>(FindObjectsInactive.Include);
        var serialized = new SerializedObject(ui);
        serialized.FindProperty("waitForHomeIntroAnimation").boolValue = scenario != 3;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
        ui.gameObject.SetActive(scenario != 1);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui.gameObject);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene);
        sawDescent = sawUncoveredDescent = toggledOff = toggledOn = capturedFalling = false;
        EditorApplication.EnterPlaymode(); stage = 0; next = EditorApplication.timeSinceStartup + .02;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Home intro timeout at scenario " + scenario + ", stage " + stage);
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .02;
            if (stage == 4)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(ReferenceEquals(PopGameplayUI.Instance, null), "UI singleton clears on Stop with Domain Reload disabled");
                if (++scenario < 4) OpenScenario();
                else
                {
                    Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
                    results.Add("HOME INTRO VALIDATION PASS: " + checks + " checks; UI on/off/reenabled during intro, deferred/immediate menu options, all six real cell landings, unscaled animation, menu revisits and preloaded Journey navigation; 4 Domain-Reload-off Play/Stop cycles.");
                    File.WriteAllLines(Output + "/intro-results.txt", results); Debug.Log(results[^1]); Finish(0);
                }
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (stage == 3)
            {
                Board board = Object.FindFirstObjectByType<Board>();
                if (SceneManager.GetActiveScene().buildIndex != 1 || board == null || !board.IsReady || !PopGameplaySession.Instance.IsActive) return;
                Check(PopGameplaySession.Instance.Mode == PopGameMode.Journey && PopGameplayUI.Instance.View(PopUIPage.Hud).gameObject.activeInHierarchy, "Menu Play still activates preloaded Journey and its authored HUD");
                Check(Object.FindObjectsByType<PopGameplayUI>(FindObjectsSortMode.None).Length == 1 && Object.FindFirstObjectByType<BlokkiPopIntroController>() == null, "Scene transition does not duplicate UI or leave the intro behind");
                EditorApplication.ExitPlaymode(); stage = 4; return;
            }
            BlokkiPopIntroController intro = Object.FindFirstObjectByType<BlokkiPopIntroController>();
            if (intro == null || Read<AsyncOperation>(intro, "loadOperation") == null) return;
            RectTransform[] cells = Read<RectTransform[]>(intro, "animatedCells");
            CanvasGroup[] groups = Read<CanvasGroup[]>(intro, "animatedCellGroups");
            PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>(FindObjectsInactive.Include);
            if (stage == 0)
            {
                Check(Time.timeScale == 1f, "Opening Home does not pause the animation/game globally");
                for (int i = 0; i < cells.Length; ++i) previousPositions[i] = cells[i].anchoredPosition;
                // Deliberately pause scaled time in one scenario: the intro must still use unscaled time.
                if (scenario == 2) Time.timeScale = 0;
                stage = 1;
            }
            if (stage == 1)
            {
                float elapsed = Time.unscaledTime - Read<float>(intro, "startedAt");
                if (scenario == 2 && elapsed >= .65f && !toggledOff) { ui.gameObject.SetActive(false); toggledOff = true; }
                if (scenario == 2 && elapsed >= 1.1f && !toggledOn) { ui.gameObject.SetActive(true); toggledOn = true; }
                int landed = 0;
                for (int i = 0; i < cells.Length; ++i)
                {
                    if (!cells[i].gameObject.activeInHierarchy) throw new Exception("Menu hid intro cell " + i + " while its landing coroutine was running");
                    if (cells[i].anchoredPosition.y < previousPositions[i].y - .05f && groups[i].alpha > .01f && groups[i].alpha < .99f) sawDescent = true;
                    if (sawDescent && !ui.View(PopUIPage.Home).gameObject.activeInHierarchy) sawUncoveredDescent = true;
                    previousPositions[i] = cells[i].anchoredPosition;
                    if (Vector2.Distance(cells[i].anchoredPosition, authoredTargets[i]) < .01f && groups[i].alpha > .99f) ++landed;
                }
                if (scenario == 0 && landed >= 2 && !capturedFalling && !Read<bool>(intro, "introAnimationFinished"))
                { ScreenCapture.CaptureScreenshot(Output + "/home-cells-falling.png"); capturedFalling = true; }
                if (!Read<bool>(intro, "introAnimationFinished") || !Read<bool>(intro, "gameplayReady")) return;
                if (scenario != 1 && !ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                Check(sawDescent, "Observed visible downward motion and fading during an actual cell landing, not only an end pose");
                if (scenario != 3) Check(sawUncoveredDescent, "The default deferred menu leaves the falling mini-board uncovered during its entrance");
                Check(landed == cells.Length, "All six cells reach their original authored landing positions");
                for (int i = 0; i < cells.Length; ++i)
                    Check(Vector3.Distance(cells[i].localScale, Vector3.one) < .001f && Mathf.Approximately(groups[i].alpha, 1f), "Cell " + i + " finishes at full scale and opacity");
                Check(Read<TMPro.TMP_Text>(intro, "taglineText").gameObject.activeInHierarchy, "The authored DROP MATCH POP slogan stays visible with the mode menu");
                if (scenario == 1)
                {
                    Check(Read<TMPro.TMP_Text>(intro, "loadingLabel").gameObject.activeInHierarchy && Read<TMPro.TMP_Text>(intro, "loadingLabel").text == "TAP TO START", "Without mode UI, the original loading/Tap to Start presentation still completes");
                    ui.gameObject.SetActive(true); // Enabling after completion must not hide or replay the landed blocks.
                    stage = 2; next = EditorApplication.timeSinceStartup + .15; return;
                }
                Time.timeScale = 1;
                CheckMenu(intro, ui); stage = 2; next = EditorApplication.timeSinceStartup + .15;
            }
            else if (stage == 2)
            {
                CheckMenu(intro, ui);
                ui.View(PopUIPage.Home).Element("Journey").button.onClick.Invoke();
                Check(ui.View(PopUIPage.Journey).gameObject.activeInHierarchy, "Journey button still opens the authored level picker");
                ui.View(PopUIPage.Journey).Element("Back").button.onClick.Invoke();
                Check(ui.View(PopUIPage.Home).gameObject.activeInHierarchy, "Back returns to the authored Home menu");
                for (int i = 0; i < cells.Length; ++i)
                    Check(cells[i].gameObject.activeInHierarchy && Vector2.Distance(cells[i].anchoredPosition, authoredTargets[i]) < .01f, "Reopening Home does not hide or rewind landed cell " + i);
                results.Add($"scenario {scenario}: all six cells visibly fell and landed, slogan preserved, menu works, UI toggle/unscaled check passed");
                if (scenario == 0) ScreenCapture.CaptureScreenshot(Output + "/home-cells-landed-menu.png");
                if (scenario == 2) Check(toggledOff && toggledOn, "Actually toggled the mode UI off and back on while the intro was in progress");
                if (scenario == 3)
                {
                    ui.View(PopUIPage.Home).Element("Journey").button.onClick.Invoke();
                    ui.View(PopUIPage.Journey).Element("Level01").button.onClick.Invoke();
                    ui.View(PopUIPage.Level).Element("Play").button.onClick.Invoke(); stage = 3;
                }
                else { stage = 5; next = EditorApplication.timeSinceStartup + .25; }
            }
            else if (stage == 5) { EditorApplication.ExitPlaymode(); stage = 4; }
        }
        catch (Exception exception)
        {
            results.Add("HOME INTRO VALIDATION FAILED: " + exception); File.WriteAllLines(Output + "/intro-results.txt", results);
            Debug.LogError(results[^1]); Finish(1);
        }
    }

    private static void CheckMenu(BlokkiPopIntroController intro, PopGameplayUI ui)
    {
        Check(ui.gameObject.activeInHierarchy && ui.View(PopUIPage.Home).gameObject.activeInHierarchy, "Mode menu remains enabled alongside the animated mini-board");
        Check(!Read<TMPro.TMP_Text>(intro, "loadingLabel").gameObject.activeSelf && !Read<Button>(intro, "startButton").gameObject.activeSelf, "Mode menu replaces only the legacy loading/Tap prompt");
        Check(!Read<Button>(intro, "startButton").interactable, "Loading completion does not reactivate the obsolete Tap button behind the menu");
    }
    private static T Read<T>(object instance, string field) => (T)instance.GetType().GetField(field, Private).GetValue(instance);
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void OnLog(string text, string stack, LogType type)
    { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(text); }
    private static void Finish(int exit)
    { EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(exit); }
}
