using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real mixed-color board clears, isolated from player saves and scenes.</summary>
public static class PopClearColorValidation
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Output => Path.GetFullPath("../clear-color-20261020");
    private static readonly Color[] Colors = { Color.red, Color.yellow, Color.blue, new(1,.2f,.65f), new(1,.5f,0), new(.6f,.2f,1), Color.cyan };
    private static int stage, test, checks;
    private static double next, deadline;
    private static Board board;
    private static BoardEffects effects;
    private static Cell[,] cells;
    private static Color expected;
    private static readonly List<Vector2Int> cleared = new();
    private static readonly List<string> errors = new();

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\','/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Refusing color QA in the real player project.");
        Directory.CreateDirectory(Output);
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        config.themes[0].blockColors = (Color[])Colors.Clone();
        config.themes[0].clearEffect = Color.green;
        config.popEffects[0].tint = Color.green; config.popEffects[0].tintStrength = 1;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        // Deliberately adversarial legacy theme/style tints must not own clear hue.
        var store = new PopSaveStore(); store.Progress.selectedEffect = config.popEffects[0].id; store.Progress.selectedTheme = config.themes[0].id;
        store.SaveProgress(); store.Flush();
        var ambient = AssetDatabase.LoadAssetAtPath<PopAmbientConfiguration>(PopAmbientConfiguration.AssetPath);
        if (ambient != null) { ambient.enabled = false; EditorUtility.SetDirty(ambient); AssetDatabase.SaveAssets(); }
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Application.runInBackground = true;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.EnterPlaymode(); Arm(0, .4);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Color QA timeout stage " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 99)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(errors.Count == 0, string.Join(";", errors));
                string result = $"CLEAR COLOR PASS: {checks} checks; 7 placed colors x row/column/cross/four-line clear; mixed existing colors; preview, delayed cells, actual dissolve RGB, line flashes, particle hue, end occupancy/hide; adversarial green theme/style; no scene/prefab writes.";
                Debug.Log(result); File.WriteAllText(Output + "/results.txt", result);
                EditorApplication.update -= Tick; EditorApplication.Exit(0); return;
            }
            if (!EditorApplication.isPlaying || PopGameplaySession.Instance == null || !PopGameplaySession.Instance.IsActive) return;
            board = Object.FindFirstObjectByType<Board>(); if (board == null || !board.IsReady) return;
            effects = (BoardEffects)typeof(Board).GetField("boardEffects", Flags).GetValue(board);
            cells = (Cell[,])typeof(Board).GetField("cells", Flags).GetValue(board);
            switch (stage)
            {
                case 0:
                    if (test == Colors.Length * 4) { Time.timeScale = 1; EditorApplication.ExitPlaymode(); Arm(99,.2); return; }
                    Setup(); Arm(1, .06); break;
                case 1:
                    CheckVisibleColors();
                    if (test == 0) ScreenCapture.CaptureScreenshot(Output + "/red-row.png");
                    if (test == 1) ScreenCapture.CaptureScreenshot(Output + "/red-column.png");
                    Arm(2, .85); break;
                case 2:
                    foreach (var point in cleared)
                    { Check(!cells[point.y, point.x].gameObject.activeSelf, "Dissolved cell is hidden"); Check(!board.CreateOccupancySnapshot()[point.y, point.x], "Clear still empties logical occupancy"); }
                    ++test; Arm(0,.05); break;
            }
        }
        catch (Exception exception)
        { Time.timeScale = 1; Debug.LogError("CLEAR COLOR FAIL: " + exception); File.WriteAllText(Output + "/results.txt", exception.ToString()); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
    private static void Setup()
    {
        int color = test / 4, kind = test % 4;
        expected = Colors[color]; cleared.Clear();
        var starts = new List<PopStartingCell>();
        for (int y = 0; y < 8; ++y) for (int x = 0; x < 8; ++x)
        {
            bool inClear = kind == 0 ? y == 3 : kind == 1 ? x == 3 : kind == 2 ? x == 3 || y == 3 : x == 2 || x == 3 || y == 2 || y == 3;
            bool gap = kind < 3 ? x == 3 && y == 3 : x >= 2 && x <= 3 && y >= 2 && y <= 3;
            if (!inClear) continue;
            cleared.Add(new Vector2Int(x,y));
            if (!gap) starts.Add(new PopStartingCell { position = new Vector2Int(x,y), colorId = (x + y * 2) % Colors.Length });
        }
        board.ApplyStartingCells(starts);
        var particles = (ParticleSystem)typeof(BoardEffects).GetField("clearParticles", Flags).GetValue(effects);
        particles.Clear();
        Vector2Int origin = kind < 3 ? new(3,3) : new(2,2);
        int shape = kind < 3 ? 0 : PopLevelCatalog.FindShape(new[,] { {1,1}, {1,1} });
        Time.timeScale = 0;
        board.Hover(origin, shape, color);
        foreach (var point in cleared) Check(SameRGB(cells[point.y,point.x].GetComponent<SpriteRenderer>().color, expected), "Preview matches placed color");
        Check(board.Place(origin, shape, color), "Real board placement succeeds");
        PopGameplaySession.Instance.CompletePlacement();
        CheckVisibleColors(); // Includes cells whose scan delay has not started yet.
        var flashes = (List<SpriteRenderer>)typeof(BoardEffects).GetField("lineFlashPool", Flags).GetValue(effects);
        foreach (var flash in flashes) if (flash.gameObject.activeSelf) Check(SameRGB(flash.color, expected), "Line flash preserves placed hue even for multi-clear/Fever");
        var buffer = new ParticleSystem.Particle[particles.main.maxParticles]; int count = particles.GetParticles(buffer);
        for (int i = 0; i < count; ++i)
        {
            Color actual = buffer[i].startColor;
            // White highlights are allowed; arbitrary green/pink/gold replacements are not.
            // Use the darkest channel so Color32 rounding near white is not
            // amplified (Unity's named yellow has a green component near .92).
            int channel = expected.r <= expected.g && expected.r <= expected.b ? 0 : expected.g <= expected.b ? 1 : 2;
            float blend = (actual[channel] - expected[channel]) / Mathf.Max(.001f, 1 - expected[channel]);
            Check(SameRGB(actual, Color.Lerp(expected, Color.white, Mathf.Clamp01(blend)), .02f), "Fragment retains placed hue instead of cosmetic green tint: case=" + test + " actual=" + actual + " expected=" + expected + " blend=" + blend);
        }
        Time.timeScale = 1;
    }
    private static void CheckVisibleColors()
    {
        foreach (var point in cleared)
        { var cell = cells[point.y, point.x]; if (cell.gameObject.activeSelf) Check(SameRGB(cell.GetComponent<SpriteRenderer>().color, expected), "Every dissolving/delayed cell retains placement color, case " + test); }
    }
    private static bool SameRGB(Color a, Color b, float tolerance = .001f) => Mathf.Abs(a.r-b.r) < tolerance && Mathf.Abs(a.g-b.g) < tolerance && Mathf.Abs(a.b-b.b) < tolerance;
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void Arm(int value, double delay) { stage = value; next = EditorApplication.timeSinceStartup + delay; }
    private static void OnLog(string text, string stack, LogType type)
    { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(text); }
}
