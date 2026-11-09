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

/// <summary>Runs only in the isolated QA project. Never writes the real player's prefs/scenes.</summary>
public static class PopProductValidation
{
    private static string Output => Path.GetFullPath("../endless-product-20261020");
    private static int stage, checks, move, ratio;
    private static double next, deadline;
    private static string savedRun;
    private static readonly Vector2Int[] Sizes = { new(720,1280), new(720,1440), new(720,1600), new(1080,2400), new(768,1024) };
    private static readonly List<string> errors = new();
    private static void Guard()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\','/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Isolated product QA only.");
        Directory.CreateDirectory(Output);
    }
    public static void PrepareAssets()
    {
        Guard();
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        Check(PopGameplaySetup.Validate(config, out string reason), reason);
        PopEndlessValidation.RunPure();
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        PopEndlessUIAuthoring.Install();
        Directory.CreateDirectory(Output + "/authored");
        File.Copy(PopGameplayConfiguration.AssetPath, Output + "/authored/PopGameplayConfiguration.asset", true);
        File.Copy(PopSceneUIAuthoring.PrefabPath, Output + "/authored/PopGameplayUI.prefab", true);
        File.Copy(PopJourneyHomeAuthoring.PrefabPath, Output + "/authored/PopJourneyFocusUI.prefab", true);
        Debug.Log("PRODUCT AUTHORING PASS: exported config and additive lesson-coach objects; no scene writes.");
    }
    public static void Run()
    {
        Guard();
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        Check(PopGameplaySetup.Validate(config, out string reason), reason);
        PopEndlessValidation.RunPure();
        VerifyPuzzleRhythm(config);
        var metrics = PopEndlessGenerator.Evaluate(config.tutorial.endlessLesson, config.endless);
        Check(config.tutorial.endlessLesson.pieceSequence.Count == 4 && metrics.multiClears == 1, "Lesson teaches the next tray and one real double clear");
        var legacy = JsonUtility.FromJson<PopProgressData>("{\"schema\":1,\"tutorialComplete\":true,\"selectedTheme\":\"sky\",\"journey\":[],\"daily\":[]}");
        Check(PopSaveStore.ValidProgress(legacy) && legacy.tutorialComplete && !legacy.endlessTutorialComplete, "Additive onboarding flag preserves old progression");
        PlayerPrefs.DeleteKey("blokkipop.progression.v1"); PlayerPrefs.DeleteKey("blokkipop.progression.v1.backup");
        var store = new PopSaveStore(); store.ClearRun(PopGameMode.EndlessPop); store.Flush();
        config.tutorial.automaticOnFirstEndless = true; config.endless.fixedSeed = 293;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Application.runInBackground = true;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog; EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.EnterPlaymode(); Arm(0, .5);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Product QA timeout at " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 99)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(errors.Count == 0, string.Join(";", errors));
                string report = "PRODUCT FLOW PASS: " + checks + " checks; first-play lesson, actual future tray, invalid drop, four real placements/double pop, five screen ratios, pause/resume, exact save/Continue, no repeat onboarding, replay and skip.";
                File.WriteAllText(Output + "/results.txt", report); Debug.Log(report);
                EditorApplication.update -= Tick; EditorApplication.Exit(0); return;
            }
            if (!EditorApplication.isPlaying || PopGameplaySession.Instance == null || PopGameplayUI.Instance == null) return;
            var session = PopGameplaySession.Instance; var ui = PopGameplayUI.Instance;
            var tray = Object.FindFirstObjectByType<Blocks>();
            switch (stage)
            {
                case 0:
                    if (!ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    session.PlayEndless(false); Arm(1, .4); break;
                case 1:
                    if (!session.IsActive || !session.IsEndlessLesson || tray == null) return;
                    Check(session.Mode == PopGameMode.Tutorial && !session.AllowsRevive && session.SavedRun(PopGameMode.EndlessPop) == null, "First entry is a separate ad-free lesson, not a fake Endless wave");
                    Check(ui.EndlessQueue.lessonCoach.gameObject.activeInHierarchy, "Coach is authored and visible");
                    Check(ui.ScoreRewardTarget == ui.EndlessQueue.score.rectTransform, "Lesson score rewards target the new HUD, not retired artwork");
                    Check(ui.EndlessQueue.pieces[0].ShapeId == session.Definition.pieceSequence[3], "Real next-tray piece is visible in the lesson");
                    Check(!ui.View(PopUIPage.Tutorial).gameObject.activeInHierarchy, "Old bottom tutorial card cannot cover the queue/tray");
                    Check(!tray.TryPlaceSlot(0, Vector2Int.zero) && session.Moves == 0, "Wrong guided placement does not spend a move");
                    Arm(2, .1); break;
                case 2:
                    if (ratio < Sizes.Length)
                    {
                        ConfigureSize(Sizes[ratio]); Arm(3, .3); break;
                    }
                    ui.ShowPause(); Check(!ui.EndlessQueue.gameObject.activeInHierarchy, "Coach/queue cannot cover Pause");
                    ui.View(PopUIPage.Pause).Element("Resume").button.onClick.Invoke();
                    Check(ui.EndlessQueue.lessonCoach.gameObject.activeInHierarchy, "Coach restored on Resume");
                    move = 0; Arm(4, .1); break;
                case 3:
                    Canvas.ForceUpdateCanvases();
                    Rect coach = PopEndlessQueueView.ScreenBounds(ui.EndlessQueue.lessonCoach);
                    var board = Object.FindFirstObjectByType<Board>(); var camera = Camera.main;
                    float boardTop = camera.WorldToScreenPoint(new Vector3(4, 8, 0)).y;
                    Check(coach.xMin >= -1 && coach.xMax <= Screen.width + 1 && coach.yMax <= Screen.height + 1, "Lesson coach stays on-screen " + Sizes[ratio]);
                    Check(coach.yMin > boardTop, "Lesson coach does not cover board " + Sizes[ratio]);
                    Check(!ui.EndlessQueue.lessonInstruction.isTextOverflowing, "Instruction fits " + Sizes[ratio]);
                    foreach (string instruction in session.Configuration.tutorial.endlessInstructions)
                    {
                        ui.EndlessQueue.lessonInstruction.text = instruction;
                        ui.EndlessQueue.lessonInstruction.ForceMeshUpdate();
                        Check(!ui.EndlessQueue.lessonInstruction.isTextOverflowing, "Every lesson step fits " + Sizes[ratio]);
                    }
                    ui.EndlessQueue.Refresh(session);
                    ScreenCapture.CaptureScreenshot(Output + "/lesson-" + Sizes[ratio].x + "x" + Sizes[ratio].y + ".png");
                    ++ratio; Arm(2, .15); break;
                case 4:
                    if (move < session.Definition.verifiedSolution.Count)
                    {
                        var step = session.Definition.verifiedSolution[move++]; Place(tray, step);
                        if (move == 1)
                            Check(Array.Exists(Object.FindFirstObjectByType<Board>().GetComponentsInChildren<SpriteRenderer>(),
                                item => item.name == "Bubble pop VFX (pooled)" && item.gameObject.activeInHierarchy), "A real bubble clear emits the pooled visual burst");
                        if (move == 3) Check(ui.EndlessQueue.pieces[0].ShapeId == -1 && tray.HasPlayablePiece, "Preview is consumed only when the real next tray arrives");
                        Arm(4, .16); break;
                    }
                    Check(!session.IsActive && session.Progress.endlessTutorialComplete && !session.Progress.tutorialComplete && session.LargestClear == 2 && session.BubblesPopped == 4, "Lesson completes real bubbles/double clear, independent of Classic tutorial");
                    ui.View(PopUIPage.Victory).Element("Next").button.onClick.Invoke(); Arm(5, .4); break;
                case 5:
                    if (!session.IsActive || !session.IsEndless) return;
                    Check(session.EndlessWave == 1 && session.Moves == 0 && session.BubblesPopped == 0, "Lesson starts a clean real run with no tutorial score/counters");
                    Check(!ui.EndlessQueue.lessonCoach.gameObject.activeInHierarchy, "Lesson coach hidden in normal play");
                    Place(tray, session.Definition.verifiedSolution[0]); session.SaveBeforeNavigation();
                    savedRun = JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)); session.GoHome(); Arm(6, .3); break;
                case 6:
                    if (SceneManager.GetActiveScene().buildIndex != 0) return;
                    session.PlayEndless(true); Arm(7, .3); break;
                case 7:
                    if (!session.IsActive || !session.IsEndless) return;
                    session.SaveCurrentRun(); Check(savedRun == JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)), "Continue restores exact board/deck/random/counters without a lesson");
                    session.PlayEndlessLesson(); Arm(8, .3); break;
                case 8:
                    if (!session.IsActive || !session.IsEndlessLesson) return;
                    Check(savedRun == JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)), "Manual lesson never erases the existing run");
                    var events = EventSystem.current; var skip = ui.EndlessQueue.lessonSkip;
                    var pointer = new PointerEventData(events) { position = PopEndlessQueueView.ScreenBounds((RectTransform)skip.transform).center };
                    var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                    Check(hits.Exists(hit => hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == skip), "Skip is actually raycast-reachable");
                    ExecuteEvents.Execute(skip.gameObject, pointer, ExecuteEvents.pointerClickHandler); Arm(9, .3); break;
                case 9:
                    if (SceneManager.GetActiveScene().buildIndex != 0) return;
                    Check(savedRun == JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)), "Skipping manual lesson leaves save untouched");
                    session.PlayEndless(false); Arm(10, .3); break;
                case 10:
                    if (!session.IsActive || !session.IsEndless) return;
                    Check(session.EndlessWave == 1 && session.Moves == 0, "Subsequent New Run does not repeat onboarding");
                    EditorApplication.ExitPlaymode(); Arm(99, .2); break;
            }
        }
        catch (Exception exception)
        { Debug.LogError("PRODUCT FLOW FAIL: " + exception); File.WriteAllText(Output + "/results.txt", exception.ToString()); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
    private static void Place(Blocks tray, PopSolutionMove move)
    {
        var snapshot = new PopRunSnapshot { trayShapes = new int[3], trayColors = new int[3], trayAvailable = new bool[3] };
        tray.Export(snapshot); int slot = -1;
        for (int i = 0; i < 3; ++i) if (snapshot.trayAvailable[i] && snapshot.trayShapes[i] == move.shape) { slot = i; break; }
        Check(slot >= 0 && tray.TryPlaceSlot(slot, move.origin), "Actual tray accepts the lesson/witness move");
    }
    private static void VerifyPuzzleRhythm(PopGameplayConfiguration config)
    {
        int setup = 0, doubles = 0, pockets = 0, count = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (uint seed = 1; seed <= 4; ++seed)
        {
            var rng = new PopRandom(seed); var colors = new int[64]; Array.Fill(colors, -1);
            for (int wave = 1; wave <= 24; ++wave)
            {
                var level = PopEndlessGenerator.Generate(colors, wave, 8, config.endless, ref rng);
                var metrics = PopEndlessGenerator.Evaluate(level, config.endless);
                Check(!float.IsInfinity(metrics.quality) && metrics.remainingCells >= 0 && metrics.pocketCells >= 0, "Finite structural score");
                setup += metrics.setupMoves; doubles += metrics.multiClears; pockets += metrics.pocketCells; ++count;
                var occupied = new bool[64]; var bubbles = new bool[64];
                foreach (var cell in level.startingCells)
                { int i = cell.position.y * 8 + cell.position.x; occupied[i] = true; bubbles[i] = cell.bubble; }
                int lines = 0, popped = 0, score = 0, combo = 0, best = 0, multi = 0;
                foreach (var move in level.verifiedSolution)
                    PopLevelValidation.ResolveMove(occupied, bubbles, move.shape, move.origin, config.authoredScoring, ref lines, ref popped, ref score, ref combo, ref best, ref multi);
                for (int i = 0; i < 64; ++i) colors[i] = occupied[i] ? 0 : -1;
            }
        }
        Check(doubles > 0 && setup > 0, "Generated bank actually contains both setup moves and multiple-line clears");
        string report = $"{count} carried-board waves; setup moves={setup}; multi-line clear moves={doubles}; final pocket cells={pockets}; {watch.ElapsedMilliseconds}ms. Structural observations only; not human difficulty/retention evidence.";
        File.WriteAllText(Output + "/puzzle-rhythm.txt", report); Debug.Log("PUZZLE RHYTHM PASS: " + report);
    }
    private static void ConfigureSize(Vector2Int size)
    {
        var game = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(game);
        // Reuse the existing well-tested arbitrary-resolution setup.
        typeof(PopEndlessPresentationValidation).GetMethod("ConfigureSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size });
        view.Repaint();
    }
    private static void Arm(int value, double delay) { stage = value; next = EditorApplication.timeSinceStartup + delay; }
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void OnLog(string text, string stack, LogType type)
    { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) errors.Add(text); }
}
