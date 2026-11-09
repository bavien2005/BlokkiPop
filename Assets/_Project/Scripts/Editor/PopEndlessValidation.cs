using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated QA only; never deletes or modifies real player preferences.</summary>
public static class PopEndlessValidation
{
    private static int stage, checks, move, completed;
    private static double next, deadline;
    private static PopRunSnapshot saved;
    private static int[] leftovers;
    private static string classicBefore;
    private static bool lastMoveWin;
    private static readonly List<string> errors = new();
    private static readonly string output = Path.GetFullPath("../endless-pop-20261016");
    public static void Run()
    {
        Guard(); Directory.CreateDirectory(output); Pure(); PopEndlessUIAuthoring.Install();
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        config.tutorial.automaticOnFirstEndless = false; // First-play lesson has its own product-flow QA.
        config.endless.fixedSeed = 293; config.endless.transitionSeconds = 1.5f; config.endless.replaceJourneyHomeButton = true;
        config.endless.wavesPerDifficultyStep = 1; config.endless.startingSlack = 1; config.endless.minimumSlack = 0; config.endless.breathingWaveInterval = 0;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        new PopSaveStore().ClearRun(PopGameMode.EndlessPop); PlayerPrefs.Save();
        classicBefore = PlayerPrefs.GetString("blokkipop.run.v1.Classic", "");
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.runInBackground = true; Application.logMessageReceived += OnLog;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.EnterPlaymode(); next = EditorApplication.timeSinceStartup + .5;
    }
    public static void RunPure() { Guard(); Directory.CreateDirectory(output); Pure(); }
    private static void Guard()
    { if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/")) throw new Exception("QA project only."); }
    private static void Pure()
    {
        var settings = new PopEndlessSettings(); int early = 0, late = 0, fallbacks = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (uint seed = 1; seed <= 8; ++seed)
        {
            var rng = new PopRandom(seed); int[] colors = Empty();
            for (int wave = 1; wave <= 24; ++wave)
            {
                uint before = rng.State; var level = PopEndlessGenerator.Generate(colors, wave, 8, settings, ref rng);
                Check(PopEndlessGenerator.Validate(level, out string reason), "Generated witness: " + reason);
                var replay = new PopRandom(before);
                Check(JsonUtility.ToJson(level) == JsonUtility.ToJson(PopEndlessGenerator.Generate(colors, wave, 8, settings, ref replay)) && rng.State == replay.State, "Generation is deterministic independent of global visual Random");
                int[] start = Empty(); var occupied = new bool[64]; var bubbles = new bool[64];
                foreach (var cell in level.startingCells) { int i = cell.position.y * 8 + cell.position.x; start[i] = cell.colorId; occupied[i] = true; bubbles[i] = cell.bubble; }
                for (int i = 0; i < 64; ++i) if (colors[i] >= 0) Check(start[i] == colors[i], "Prior board occupancy/color is preserved");
                int lines = 0, popped = 0, score = 0, combo = 0, best = 0, multi = 0;
                foreach (var step in level.verifiedSolution)
                { Check(PopLevelValidation.ResolveMove(occupied, bubbles, step.shape, step.origin, new PopScoringSettings(), ref lines, ref popped, ref score, ref combo, ref best, ref multi), "Real witness placement is legal"); if (popped >= level.targetBubbles) break; }
                colors = Empty(); for (int i = 0; i < 64; ++i) if (occupied[i]) colors[i] = start[i] >= 0 ? start[i] : wave % 8;
                if (wave <= 4) early += level.verifiedSolution.Count;
                if (wave >= 21) late += level.verifiedSolution.Count;
                if (level.verifiedSolution.Exists(m => Polyominos.GetCellCount(m.shape) == 1)) ++fallbacks;
            }
        }
        string report = "192 carried-board waves, each independently replayed twice; " + checks + " checks. Early witness avg=" + early / 32f + "; late=" + late / 32f + "; dot fallback waves=" + fallbacks + "; " + watch.ElapsedMilliseconds + "ms";
        File.WriteAllText(output + "/generation-report.txt", report); Debug.Log("ENDLESS GENERATION PASS: " + report);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 8)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(ReferenceEquals(PopGameplaySession.Instance, null), "Session resets on Stop with Domain Reload off");
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity"); EditorApplication.EnterPlaymode(); Arm(9, .4); return;
            }
            if (stage == 30)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(errors.Count == 0, string.Join(";", errors));
                Debug.Log("ENDLESS POP PASS: " + checks + " checks; generation, carried board, fixed visible deck, pending-transition resume, isolated Classic save, new-run confirmation and real scene play.");
                File.WriteAllText(output + "/results.txt", "ENDLESS POP PASS: " + checks + " checks");
                EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(0); return;
            }
            if (!EditorApplication.isPlaying || PopGameplaySession.Instance == null || PopGameplayUI.Instance == null) return;
            var session = PopGameplaySession.Instance; var ui = PopGameplayUI.Instance;
            var tray = Object.FindFirstObjectByType<Blocks>();
            switch (stage)
            {
                case 0:
                    if (!ui.View(PopUIPage.Home).gameObject.activeInHierarchy || ui.View(PopUIPage.Home).GetComponent<PopJourneyHomeView>().actions.alpha < .99f) return;
                    ui.View(PopUIPage.Home).Element("Journey").button.onClick.Invoke();
                    Check(ui.View(PopUIPage.Level).title.text == session.Configuration.endless.title, "Home primary opens Endless entry, not level 1");
                    ui.View(PopUIPage.Level).Element("Play").button.onClick.Invoke(); Arm(1, .3); break;
                case 1:
                    if (!session.IsActive || !session.IsEndless || tray == null) return;
                    Check(session.EndlessWave == 1 && !session.AllowsClassicScore && !session.AllowsRevive, "Endless has independent counters and no ad requirement");
                    var queue = Object.FindFirstObjectByType<PopEndlessQueueView>();
                    Check(queue != null && queue.pieces.Length == 6, "Future queue exists as real authored objects");
                    for (int i = 0; i < queue.pieces.Length; ++i) Check(queue.pieces[i].ShapeId == (session.DeckCursor + i < session.Definition.pieceSequence.Count ? session.Definition.pieceSequence[session.DeckCursor + i] : -1), "Preview matches exact upcoming deck orientation");
                    move = 0; Arm(12, .15); break;
                case 12:
                    Canvas.ForceUpdateCanvases();
                    var q=ui.EndlessQueue; var corners=new Vector3[4];((RectTransform)q.transform).GetWorldCorners(corners);
                    Debug.Log("ENDLESS QUEUE BOUNDS: "+corners[0]+" -> "+corners[2]+"; active="+q.gameObject.activeInHierarchy+"; heading="+q.heading.canvasRenderer.cull+"; parent="+q.transform.parent.name);
                    Check(corners[0].x>=-1 && corners[2].x<=Screen.width+1 && corners[0].y>=0 && corners[2].y<=Screen.height,"Authored preview is on-screen");
                    ScreenCapture.CaptureScreenshot(output + "/endless-wave.png"); Arm(13, .2); break;
                case 13:
                    PlayWitness(session, tray); session.SaveBeforeNavigation(); saved = session.SavedRun(PopGameMode.EndlessPop);
                    Check(saved != null, "Partial tray/pending wave can be saved"); session.GoHome(); Arm(2, .4); break;
                case 2:
                    if (SceneManager.GetActiveScene().buildIndex != 0 || !ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    Check(!ui.EndlessQueue.gameObject.activeInHierarchy,"Gameplay queue never leaks onto Home after leaving an Endless run");
                    ui.ShowEndless(); ui.View(PopUIPage.Level).Element("Play").button.onClick.Invoke();
                    Check(ui.View(PopUIPage.ReplaceRun).gameObject.activeInHierarchy, "Starting new asks before replacing an existing endless run");
                    Check(JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)) == JsonUtility.ToJson(saved), "Opening confirmation does not erase save");
                    ui.View(PopUIPage.ReplaceRun).Element("Back").button.onClick.Invoke(); ui.View(PopUIPage.Level).Element("Continue").button.onClick.Invoke(); Arm(3, .25); break;
                case 3:
                    if (!session.IsActive || !session.IsEndless || tray == null) return;
                    Check(session.LevelId == saved.levelId, "Dynamic puzzle identity survives resume");
                    session.SaveCurrentRun(); Check(JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)) == JsonUtility.ToJson(saved), "Resume preserves exact board/targets/partial tray/preview/RNG/counters");
                    Arm(4, .05); break;
                case 4:
                    if (!session.IsActive || !session.IsEndless || tray == null) return;
                    if (!session.IsTransitioning) { PlayWitness(session, tray); Arm(4, .05); break; }
                    session.SaveBeforeNavigation(); saved = session.SavedRun(PopGameMode.EndlessPop); leftovers = (int[])saved.occupiedColors.Clone();
                    Check(saved != null && saved.endlessPendingWave && PopSaveStore.ValidRun(saved), "Goal reached on last move/empty tray is a resumable transition, not loss");
                    Check(session.Progress.endlessBestWave >= 1, "Wave clear records permanent best");
                    ui.ShowPause(); Check(!tray.TryPlaceSlot(0, Vector2Int.zero), "Cannot place during wave transition/pause");
                    Check(!ui.EndlessQueue.gameObject.activeInHierarchy,"Queue cannot cover the pause modal");
                    session.GoHome(); Arm(5, .35); break;
                case 5:
                    if (SceneManager.GetActiveScene().buildIndex != 0 || !ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    ui.ShowEndless(); ui.View(PopUIPage.Level).Element("Continue").button.onClick.Invoke(); Arm(6, .2); break;
                case 6:
                    if (!session.IsActive || !session.IsEndless || tray == null) return;
                    if (session.EndlessWave == 1) { Check(session.IsTransitioning, "Pending transition restored without replaying a reward"); return; }
                    Check(session.EndlessWave == 2, "Resume advances exactly once to next wave");
                    session.SaveCurrentRun(); var current = session.SavedRun(PopGameMode.EndlessPop);
                    for (int i = 0; i < 64; ++i) if (leftovers[i] >= 0) Check(current.occupiedColors[i] == leftovers[i], "Actual scene transition keeps every leftover cell/color");
                    Check(current.moves == current.waveStartMoves && current.bubblesPopped == current.waveStartBubbles, "Only wave budget/target counters reset");
                    completed = 1; move = 0; Arm(7, .05); break;
                case 7:
                    if (!session.IsActive) throw new Exception("Verified run unexpectedly lost.");
                    if (session.IsTransitioning) { if(session.WaveMovesRemaining==0)lastMoveWin=true; move = 0; Arm(7, .1); break; }
                    if (session.EndlessWave > completed + 1) { completed = session.EndlessWave - 1; move = 0; }
                    if (session.EndlessWave < 8) { PlayWitness(session, tray); Arm(7, .05); break; }
                    Check(session.Progress.endlessBestWave == 7, "Seven waves progress continuously without a win-modal or scene reload");
                    Check(lastMoveWin,"Clearing the final bubble on the final allowed move wins, not loses");
                    Check(PlayerPrefs.GetString("blokkipop.run.v1.Classic", "") == classicBefore, "Endless never overwrites the saved Classic run");
                    session.SaveBeforeNavigation(); saved = session.SavedRun(PopGameMode.EndlessPop);
                    EditorApplication.ExitPlaymode(); Arm(8, .25); break;
                case 9:
                    if (!ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    ui.ShowEndless(); ui.View(PopUIPage.Level).Element("Continue").button.onClick.Invoke(); Arm(10, .25); break;
                case 10:
                    if (!session.IsActive || !session.IsEndless || tray == null) return;
                    session.SaveCurrentRun(); Check(JsonUtility.ToJson(session.SavedRun(PopGameMode.EndlessPop)) == JsonUtility.ToJson(saved), "Exact resume across Play/Stop, not only scene navigation");
                    Arm(14,.05);break;
                case 14:
                    if(session.IsActive){PlayLosingMove(session,tray);Arm(14,.05);break;}
                    Check(session.WaveMovesRemaining==0||!tray.HasPlayablePiece,"Loss is triggered by the real move/placement rules, not a manually invoked GameOver");
                    Check(session.SavedRun(PopGameMode.EndlessPop) == null && session.Progress.endlessBestWave == 7, "Loss ends run but preserves permanent record");
                    Check(ui.View(PopUIPage.Failure).gameObject.activeInHierarchy, "Failure UI has real New Run/Home actions");
                    Check(!ui.EndlessQueue.gameObject.activeInHierarchy,"Queue cannot cover the failure modal");
                    ui.View(PopUIPage.Failure).Element("Retry").button.onClick.Invoke(); Arm(11, .25); break;
                case 11:
                    if (!session.IsActive || !session.IsEndless) return;
                    Check(session.EndlessWave == 1 && session.Moves == 0 && session.Progress.endlessBestWave == 7, "New run resets board/run counters, not records");
                    Check(PlayerPrefs.GetString("blokkipop.run.v1.Classic", "") == classicBefore, "New endless run still preserves Classic");
                    EditorApplication.ExitPlaymode(); Arm(30, .25); break;
            }
        }
        catch (Exception e) { Debug.LogError("ENDLESS POP FAIL stage " + stage + ": " + e); File.WriteAllText(output + "/results.txt", e.ToString()); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
    private static void PlayWitness(PopGameplaySession session, Blocks tray)
    {
        var step = session.Definition.verifiedSolution[move++]; session.SaveCurrentRun(); var snapshot = session.SavedRun(PopGameMode.EndlessPop);
        int slot = -1; for (int i = 0; i < 3; ++i) if (snapshot.trayAvailable[i] && snapshot.trayShapes[i] == step.shape) { slot = i; break; }
        Check(slot >= 0 && tray.TryPlaceSlot(slot, step.origin), "Actual tray accepts the verified solution move");
    }
    private static void PlayLosingMove(PopGameplaySession session,Blocks tray)
    {
        session.SaveCurrentRun();var snapshot=session.SavedRun(PopGameMode.EndlessPop);
        var board=Object.FindFirstObjectByType<Board>();
        for(int slot=0;slot<3;slot++)if(snapshot.trayAvailable[slot])
        {
            var placements=BoardSimulation.GetPlacements(board.CreateOccupancySnapshot(),snapshot.trayShapes[slot]);
            for(int j=placements.Count-1;j>=0;j--)
            {
                var occupied=new bool[64];for(int i=0;i<64;i++)occupied[i]=snapshot.occupiedColors[i]>=0;
                var bubbles=(bool[])snapshot.bubbleTargets.Clone();int lines=0,popped=0,score=0,combo=0,best=0,multi=0;
                var origin=new Vector2Int(placements[j].Column,placements[j].Row);
                if(PopLevelValidation.ResolveMove(occupied,bubbles,snapshot.trayShapes[slot],origin,new PopScoringSettings(),ref lines,ref popped,ref score,ref combo,ref best,ref multi)&&popped<session.WaveBubblesRemaining)
                {Check(tray.TryPlaceSlot(slot,origin),"Legal but unhelpful moves spend the actual wave budget");return;}
            }
        }
        throw new Exception("QA could not find an intentionally losing move.");
    }
    private static int[] Empty() { var colors = new int[64]; for (int i = 0; i < 64; ++i) colors[i] = -1; return colors; }
    private static void Arm(int value, double delay) { stage = value; next = EditorApplication.timeSinceStartup + delay; }
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void OnLog(string text, string stack, LogType type) { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) errors.Add(text); }
}
