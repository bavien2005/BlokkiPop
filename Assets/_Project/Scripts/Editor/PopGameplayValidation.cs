using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.EnhancedTouch;
using Object = UnityEngine.Object;

/// <summary>Regression entry point for the isolated test checkout only. Never alters the real player's preferences.</summary>
public static class PopGameplayValidation
{
    private static int stage, cycle, checks, levelIndex, moveIndex, requiredFrame;
    private static double deadline, next;
    private static PopRunSnapshot classicSaved;
    private static PopRunSnapshot journeySaved;
    private static bool journeyResumed;
    private static readonly List<string> errors = new();
    private static int expectedBest;
    private static Mouse testMouse;
    private static Touchscreen testTouchscreen;
    private static Vector2 pointerStart, pointerEnd;
    private static Block pointerBlock;

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/"))
            throw new Exception("Refusing to run progression tests against the user's real project/preferences.");
        PopRuntimeFontSetup.Prepare();
        Check(Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Bangers SDF") != null, "Runtime Bangers load path still works after excluding TMP samples");
        Check(!AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Examples & Extras/Resources"), "Unused TMP sample materials are not runtime Resources");
        PopGameplaySetup.EnsureConfiguration();
        PopGameplayConfiguration config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        Check(PopGameplaySetup.Validate(config, out string result), result);
        foreach (PopLevelDefinition level in config.journeyLevels) Check(PopLevelValidation.Validate(level, config.authoredScoring, out result), level.id + ": " + result);
        PureChecks(config);
        PopSceneUIAuthoring.Install();
        string authoredExport = System.IO.Path.GetFullPath("../scene-ui-20261015/pristine-scenes");
        System.IO.Directory.CreateDirectory(authoredExport);
        foreach (string sceneName in new[] { "Home", "Game" })
            System.IO.File.Copy("Assets/_Project/Scenes/" + sceneName + ".unity", authoredExport + "/" + sceneName + ".unity", true);
        PrepareAuthoredUIEdits(config);
        foreach (string key in new[] { "blokkipop.progression.v1", "blokkipop.run.v1.Classic", "blokkipop.run.v1.Journey", "blokkipop.run.v1.Daily" })
        { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + ".backup"); }
        PlayerPrefs.SetInt("HighScore", 1562); expectedBest = 1562;
        PlayerPrefs.SetInt("blokkipop.ads.age-category.v1", (int)AdsAgeCategory.Unknown);
        PlayerPrefs.SetInt("blokkipop.ads.age-year.v1", DateTime.Now.Year); PlayerPrefs.Save();
        Application.runInBackground = true;
        typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow").GetMethod("SetConsoleErrorPause", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { false });
        EditorApplication.isPaused = false; EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog; EditorApplication.update += Tick;
        deadline = EditorApplication.timeSinceStartup + 420;
        ConfigurePortraitGameView();
        EnterCycle();
    }

    private static void EnterCycle()
    {
        stage = 0; levelIndex = moveIndex = 0;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode(); Arm(0, 1);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at cycle " + cycle + ", stage " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 10)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(ReferenceEquals(PopGameplaySession.Instance, null), "Session singleton cleared on Stop");
                Check(ReferenceEquals(PopGameplayUI.Instance, null), "Progression UI singleton cleared on Stop");
                Check(PlayerPrefs.GetInt("HighScore") == expectedBest, "Legacy best persists on Stop");
                if (++cycle < 3) EnterCycle();
                else
                {
                    Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
                    Debug.Log("PROGRESSION VALIDATION PASS: " + checks + " checks; 20 actual Journey scene wins, daily retry/calendar, tutorial, Classic exact resume, no-ad authored outcomes, 3 Domain-Reload-off Play/Stop cycles.");
                    EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(0);
                }
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < requiredFrame) return;
            PopGameplaySession session = PopGameplaySession.Instance; GameFlowManager manager = GameFlowManager._instance;
            Board board = Object.FindFirstObjectByType<Board>(); Blocks blocks = Object.FindFirstObjectByType<Blocks>();
            bool previewOrOutcome = stage >= 50 || stage == 6 || stage == 9;
            if (session == null || manager == null || board == null && !previewOrOutcome || board != null && !board.IsReady || !session.IsActive && stage != 5 && !previewOrOutcome) return;
            switch (stage)
            {
                case 0:
                    CheckAuthoredRuntimeEdits();
                    int classicGoalLabels = 0;
                    foreach (TMPro.TMP_Text label in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                        if (label.text == session.Configuration.text.classicDescription) ++classicGoalLabels;
                    Check(classicGoalLabels == 1, "Exactly one visible Classic goal HUD; stale scene copy is preserved but disabled");
                    Check(session.Mode == PopGameMode.Classic && !AdsPrivacyPanel.IsModalOpen, "Play-first Classic without mandatory age prompt");
                    Check(!AdsService.Instance.HasStartedSdkInitialization, "Unknown age still cannot initialize ad SDK");
                    Check(Object.FindObjectsByType<PopGameplaySession>(FindObjectsSortMode.None).Length == 1, "Exactly one session");
                    Check(Object.FindObjectsByType<BlockPointerInput>(FindObjectsSortMode.None).Length == 1 && EnhancedTouchSupport.enabled,
                        "Exactly one new-input gesture owner, with enhanced touch enabled across Domain-Reload-off cycles");
                    Check(manager.HighScore == expectedBest, "Legacy high score loaded");
                    SettingUI settings = Object.FindFirstObjectByType<SettingUI>();
                    if (settings != null)
                    {
                        Time.timeScale = .5f; settings.ActivePanelSetting(true); Check(Time.timeScale == 0, "Existing settings gear now pauses gameplay");
                        settings.ActivePanelSetting(false); Check(Mathf.Approximately(Time.timeScale, .5f), "Existing settings restores prior time scale"); Time.timeScale = 1;
                    }
                    if (cycle == 0) Check(!session.EffectUnlocked(session.Configuration.FindEffect("gold-burst")), "Golden Burst is locked for a fresh profile");
                    if (cycle > 0) { session.SaveBeforeNavigation(); EditorApplication.ExitPlaymode(); Arm(10, .5); break; }
                    int score = manager.Score;
                    Check(!blocks.TryPlaceSlot(0, new Vector2Int(-100, -100)), "Illegal placement rejected");
                    Check(manager.Score == score && session.Moves == 0, "Illegal placement does not spend a move or score");
                    int shape = ((int[])Read(blocks, "polyominoIndexes"))[0];
                    var positions = BoardSimulation.GetPlacements(board.CreateOccupancySnapshot(), shape);
                    Check(positions.Count > 0, "Classic tray has a legal landing");
                    pointerBlock = ((Block[])Read(blocks, "blocks"))[0];
                    pointerStart = Camera.main.WorldToScreenPoint(pointerBlock.transform.position);
                    Vector2 desired = new Vector2(positions[0].Column, positions[0].Row) + (Vector2)Read(pointerBlock, "center");
                    Vector2 mouseWorld = (Vector2)pointerBlock.transform.position +
                        (desired - (Vector2)pointerBlock.transform.position - (Vector2)(Vector3)Read(pointerBlock, "inputOffset")) / (float)Read(pointerBlock, "dragSensitivity");
                    pointerEnd = Camera.main.WorldToScreenPoint(mouseWorld);
                    testMouse = InputSystem.AddDevice<Mouse>("BlokkiPop QA Mouse");
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerStart }.WithButton(MouseButton.Left));
                    Arm(30, .12); break;
                case 30:
                    if (!ReferenceEquals(Read(Object.FindFirstObjectByType<BlockPointerInput>(), "dragging"), pointerBlock)) LogPointerDiagnostics();
                    Check(ReferenceEquals(Read(Object.FindFirstObjectByType<BlockPointerInput>(), "dragging"), pointerBlock), "New Input System mouse picks up the actual tray block");
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerEnd }.WithButton(MouseButton.Left));
                    Arm(31, .12); break;
                case 31:
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointerEnd }); Arm(32, .12); break;
                case 32:
                    Check(session.Moves == 1 && !pointerBlock.gameObject.activeSelf, "Mouse drag/release commits exactly one real placement without OnMouse or legacy Input");
                    InputSystem.RemoveDevice(testMouse); testMouse = null;
                    session.SaveBeforeNavigation(); classicSaved = session.SavedRun(PopGameMode.Classic);
                    Check(classicSaved != null && classicSaved.moves == 1 && Count(classicSaved.trayAvailable) == 2, "Exact partial tray snapshot after placement");
                    Time.timeScale = .4f; PopGameplayUI.Instance.ShowPause(); Check(Time.timeScale == 0 && PopGameplayUI.IsModalOpen, "Pause modal freezes board");
                    typeof(PopGameplayUI).GetMethod("CloseModal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(PopGameplayUI.Instance, new object[] { true });
                    Check(Mathf.Approximately(Time.timeScale, .4f), "Pause restores prior time scale"); Time.timeScale = 1;
                    session.PlayClassic(true); Arm(1, .6); break;
                case 1:
                    session.SaveCurrentRun(); PopRunSnapshot resumed = session.SavedRun(PopGameMode.Classic);
                    Check(JsonUtility.ToJson(resumed) == JsonUtility.ToJson(classicSaved), "Classic resume preserves board/colors/partial tray/RNG/counters exactly");
                    Check(manager.Score == classicSaved.score && session.Moves == 1 && !manager.HasUsedRevive, "Classic runtime counters restored");
                    PlayerPrefs.SetString("blokkipop.run.v1.Classic", "corrupt");
                    var recovery = new PopSaveStore();
                    Check(recovery.LoadRun(PopGameMode.Classic) != null && recovery.RecoveredBackup, "Recover valid owned backup from corrupt primary run");
                    recovery.SaveRun(classicSaved); recovery.Flush();
                    pointerBlock = ((Block[])Read(blocks, "blocks"))[1];
                    pointerStart = Camera.main.WorldToScreenPoint(pointerBlock.transform.position);
                    testTouchscreen = InputSystem.AddDevice<Touchscreen>("BlokkiPop QA Touchscreen");
                    InputSystem.QueueStateEvent(testTouchscreen, new TouchState { touchId = 71, position = pointerStart, phase = UnityEngine.InputSystem.TouchPhase.Began });
                    Arm(40, .12); break;
                case 40:
                    Check(ReferenceEquals(Read(Object.FindFirstObjectByType<BlockPointerInput>(), "dragging"), pointerBlock), "Enhanced touch picks up the actual tray block");
                    InputSystem.QueueStateEvent(testTouchscreen, new TouchState { touchId = 72, position = Camera.main.WorldToScreenPoint(((Block[])Read(blocks, "blocks"))[2].transform.position), phase = UnityEngine.InputSystem.TouchPhase.Began });
                    Arm(41, .12); break;
                case 41:
                    Check(ReferenceEquals(Read(Object.FindFirstObjectByType<BlockPointerInput>(), "dragging"), pointerBlock) && session.Moves == 1, "A second finger cannot steal or duplicate the active block gesture");
                    InputSystem.QueueStateEvent(testTouchscreen, new TouchState { touchId = 71, position = pointerStart, phase = UnityEngine.InputSystem.TouchPhase.Canceled });
                    InputSystem.QueueStateEvent(testTouchscreen, new TouchState { touchId = 72, position = pointerStart, phase = UnityEngine.InputSystem.TouchPhase.Ended });
                    Arm(42, .12); break;
                case 42:
                    Check(Read(Object.FindFirstObjectByType<BlockPointerInput>(), "dragging") == null && session.Moves == 1, "Canceled touch releases ownership without placing/spending a move");
                    session.SaveBeforeNavigation();
                    Check(JsonUtility.ToJson(session.SavedRun(PopGameMode.Classic)) == JsonUtility.ToJson(classicSaved), "Canceled touch cannot leak preview cells into the exact saved board/tray");
                    InputSystem.RemoveDevice(testTouchscreen); testTouchscreen = null;
                    session.PlayJourney(0); Arm(2, .5); break;
                case 2:
                    Check(session.Mode == PopGameMode.Journey && session.LevelId == session.Configuration.journeyLevels[levelIndex].id, "Expected Journey level active");
                    PlayWitness(session, blocks, ref moveIndex);
                    if (levelIndex == 19 && moveIndex == 1 && !journeyResumed)
                    {
                        journeyResumed = true; session.SaveBeforeNavigation(); journeySaved = session.SavedRun(PopGameMode.Journey);
                        Check(journeySaved != null && journeySaved.bubblesPopped > 0, "Save partially completed Journey including popped bubbles");
                        session.PlayJourney(19, true); Arm(21, .5); break;
                    }
                    if (!manager.IsGameOver) { Arm(2, .04); break; }
                    Check(session.Stars(session.LevelId) == 3 && session.Lines >= session.Definition.targetLines && session.BubblesPopped >= session.Definition.targetBubbles, "Journey win grants 3 stars and all goals");
                    Check(!manager.IsAwaitingRevive && PopGameplayUI.IsModalOpen, "Authored outcome has no ad revive");
                    Check(GameFlowManager.SavedHighScore == expectedBest, "Journey cannot overwrite Classic best");
                    if (levelIndex == 3)
                    {
                        Check(session.EffectUnlocked(session.Configuration.FindEffect("gold-burst")) && session.NewlyUnlockedEffects.Count == 1, "Actual double clear unlocks Golden Burst once");
                        session.SelectEffect("gold-burst"); Check(session.SelectedEffect.radialBurst && session.SelectedEffect.particleMultiplier > 1, "Selected reward changes the real particle style");
                    }
                    if (levelIndex == 0) { manager.AddCoin(5000); Check(GameFlowManager.SavedHighScore == expectedBest, "Even large authored score is excluded from Classic best"); }
                    if (++levelIndex < session.Configuration.journeyLevels.Count) { moveIndex = 0; session.PlayJourney(levelIndex); Arm(2, .4); }
                    else
                    {
                        Check(session.TotalJourneyStars == 60 && session.ThemeUnlocked(session.Configuration.FindTheme("night-glow")), "60 stars unlock Night Glow");
                        session.SelectTheme("night-glow"); Check(session.SelectedTheme.id == "night-glow", "Selected theme saved");
                        Check(session.SelectedTheme.backgroundOverride != null, "Night Glow background is a real imported Sprite, not a missing reference");
                        ModernBackground backdrop = Object.FindFirstObjectByType<ModernBackground>();
                        Check(backdrop != null && ((SpriteRenderer)Read(backdrop, "backgroundRenderer")).sprite == session.SelectedTheme.backgroundOverride,
                            "Selected theme applies its actual background sprite");
                        moveIndex = 0; session.PlayDaily(); Arm(3, .5);
                    }
                    break;
                case 21:
                    session.SaveBeforeNavigation();
                    Check(JsonUtility.ToJson(journeySaved) == JsonUtility.ToJson(session.SavedRun(PopGameMode.Journey)), "Journey resume preserves deck cursor, bubbles, counters, combo, board and remaining pieces");
                    Arm(2, .05); break;
                case 3:
                case 4:
                    PlayWitness(session, blocks, ref moveIndex);
                    if (!manager.IsGameOver) { Arm(stage, .04); break; }
                    Check(session.DailyWins == 1 && session.DailyComplete(session.UtcToday), "Daily reward/calendar counted once, including retries");
                    if (stage == 3) { moveIndex = 0; session.PlayDaily(); Arm(4, .4); }
                    else { moveIndex = 0; session.PlayTutorial(); Arm(5, .5); }
                    break;
                case 5:
                    if (moveIndex == 0)
                    {
                        Check(Read(PopGameplayUI.Instance, "lesson") != null, "Guided lesson card is visible after mode initialization");
                        Check(!blocks.TryPlaceSlot(0, new Vector2Int(0, 0)) && session.Moves == 0, "Interactive lesson rejects wrong landing without consuming piece");
                        Arm(51, .15); break;
                    }
                    PlayWitness(session, blocks, ref moveIndex);
                    if (!manager.IsGameOver) { Arm(5, .04); break; }
                    Check(session.Progress.tutorialComplete && session.Lines == 4 && session.Moves == 3, "Three real tutorial placements teach 4 clears");
                    Arm(53, .25); break;
                case 51:
                    CapturePreview("tutorial"); Arm(52, .15); break;
                case 52:
                    PlayWitness(session, blocks, ref moveIndex); Arm(5, .04); break;
                case 53:
                    CapturePreview("tutorial-win"); Arm(54, .15); break;
                case 54:
                    session.CompleteTutorial(); Arm(6, .8); break;
                case 6:
                    if (PopGameplayUI.Instance != null && !PopGameplayUI.Instance.View(PopUIPage.Home).gameObject.activeInHierarchy) { Arm(6, .1); break; }
                    Check(SceneManager.GetActiveScene().buildIndex == 0 && PopGameplayUI.Instance != null, "Home menu after tutorial");
                    PopUIElement homeTitle = PopGameplayUI.Instance.View(PopUIPage.Home).Element("Title");
                    Check(homeTitle.rect.anchoredPosition.x == 13 && homeTitle.text.text == "CHOOSE YOUR POP · AUTHORED", "Home scene layout/text override survives scene loading and runtime binding");
                    Check(homeTitle.text.color == new Color(.91f, .79f, .65f, 1), "Authored Home text color is not overwritten by runtime styles");
                    CapturePreview("home"); Arm(60, .15); break;
                case 60:
                    int objectsBefore = PopGameplayUI.Instance.GetComponentsInChildren<RectTransform>(true).Length;
                    PreviewMenu("ShowJourney", 1);
                    Check(PopGameplayUI.Instance.View(PopUIPage.Journey).Element("Level11").rect.gameObject.activeSelf && !PopGameplayUI.Instance.View(PopUIPage.Journey).Element("Level01").rect.gameObject.activeSelf, "Second Journey page uses the saved Level11-20 objects");
                    PreviewMenu("ShowJourney", 0); PreviewMenu("ShowDaily"); PreviewMenu("ShowThemes"); PreviewMenu("ShowThemes");
                    Check(PopGameplayUI.Instance.GetComponentsInChildren<RectTransform>(true).Length == objectsBefore, "Opening/reopening modal pages never creates or destroys designer UI objects");
                    PreviewMenu("ShowJourney", 0); Arm(61, .25); break;
                case 61:
                    CapturePreview("journey-menu"); Arm(62, .15); break;
                case 62:
                    PreviewMenu("ShowLevel", 5); Arm(63, .25); break;
                case 63:
                    CapturePreview("level-goals"); Arm(64, .15); break;
                case 64:
                    PreviewMenu("ShowDaily"); Arm(65, .25); break;
                case 65:
                    CapturePreview("daily"); Arm(66, .15); break;
                case 66:
                    PreviewMenu("ShowThemes"); Arm(67, .25); break;
                case 67:
                    CapturePreview("themes"); Arm(68, .15); break;
                case 68:
                    PopGameplayUI.Instance.ShowHome(); session.PlayJourney(19); moveIndex = 0; Arm(7, .8); break;
                case 7:
                    session.Definition.moveLimit = session.Definition.verifiedSolution.Count;
                    PlayWitness(session, blocks, ref moveIndex);
                    if (!manager.IsGameOver) { Arm(7, .04); break; }
                    Check(session.Stars(session.LevelId) == 3 && session.Moves == session.Definition.moveLimit, "Goal on last move wins before move-limit failure");
                    Arm(70, 1); break;
                case 70:
                    Canvas.ForceUpdateCanvases();
                    PopStarGraphic[] rewardStars = Object.FindObjectsByType<PopStarGraphic>(FindObjectsSortMode.None);
                    Check(rewardStars.Length == 3, "Outcome contains three actual reward stars");
                    foreach (PopStarGraphic rewardStar in rewardStars)
                    {
                        Check(rewardStar.canvasRenderer != null && rewardStar.transform.localScale.x > .9f,
                            "Reward star has its required CanvasRenderer and finishes its unscaled reveal");
                        Mesh starMesh = rewardStar.canvasRenderer.GetMesh();
                        Check(starMesh != null && starMesh.vertexCount == 11, "Reward star submits its actual five-point mesh to the canvas");
                    }
                    CapturePreview("win"); Arm(71, .15); break;
                case 71:
                    session.PlayJourney(15); Arm(8, .5); break;
                case 8:
                    session.Definition.moveLimit = 1;
                    int firstShape = ((int[])Read(blocks, "polyominoIndexes"))[0];
                    var legal = BoardSimulation.GetPlacements(board.CreateOccupancySnapshot(), firstShape);
                    bool placed = false;
                    foreach (var position in legal)
                    {
                        if (position.ClearedLines != 0) continue;
                        placed = blocks.TryPlaceSlot(0, new Vector2Int(position.Column, position.Row)); if (placed) break;
                    }
                    Check(placed && manager.IsGameOver && !session.IsActive && !manager.IsAwaitingRevive, "Move-budget failure is terminal without ads");
                    Check(session.SavedRun(PopGameMode.Journey) == null, "Terminal authored run cleared");
                    session.GoHome(); Arm(9, .8); break;
                case 9:
                    Check(session.TotalJourneyStars == 60 && session.DailyWins == 1 && session.SelectedTheme.id == "night-glow" && session.SelectedEffect.id == "gold-burst", "Progress/theme/effect survive all scene changes");
                    EditorApplication.ExitPlaymode(); Arm(10, .6); break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("PROGRESSION VALIDATION FAILED stage " + stage + ": " + exception);
            EditorApplication.update -= Tick; EditorApplication.Exit(1);
        }
    }

    private static void PlayWitness(PopGameplaySession session, Blocks blocks, ref int step)
    {
        Check(step < session.Definition.verifiedSolution.Count, "Witness still has a move");
        PopSolutionMove move = session.Definition.verifiedSolution[step];
        int[] shapes = (int[])Read(blocks, "polyominoIndexes"); Block[] pieces = (Block[])Read(blocks, "blocks");
        int slot = -1; for (int i = 0; i < 3; ++i) if (pieces[i].gameObject.activeSelf && shapes[i] == move.shape) { slot = i; break; }
        Check(slot >= 0 && blocks.TryPlaceSlot(slot, move.origin), "Actual board accepts witness step " + step);
        Check(session.Moves == ++step, "One move per consumed piece");
    }

    private static void PureChecks(PopGameplayConfiguration config)
    {
        var achievements = new PopSaveStore(); achievements.Progress.largestSingleClear = 0; achievements.Progress.bestCombo = 0;
        Check(!achievements.RecordClearAchievement(PopGameMode.Tutorial, 2, 3) && achievements.Progress.largestSingleClear == 0, "Tutorial cannot farm permanent clear achievements");
        Check(achievements.RecordClearAchievement(PopGameMode.Classic, 2, 2) && achievements.Progress.largestSingleClear == 2, "Classic clear records achievement");
        Check(!achievements.RecordClearAchievement(PopGameMode.Classic, 1, 1), "Achievements never downgrade");
        PopRandom random = new(42), other = new(42);
        for (int i = 0; i < 100; ++i) Check(random.Next() == other.Next(), "Deterministic independent generator " + i);
        other = new PopRandom(random.State); Check(random.Next() == other.Next(), "RNG state resumes exactly");
        PopLevelDefinition invalid = JsonUtility.FromJson<PopLevelDefinition>(JsonUtility.ToJson(config.journeyLevels[0]));
        invalid.startingCells.Add(new PopStartingCell { position = new Vector2Int(99, 0) }); Check(!PopLevelValidation.Validate(invalid, config.authoredScoring, out _), "Reject out-of-board authored cells");
        invalid.startingCells = null; Check(!PopLevelValidation.Validate(invalid, config.authoredScoring, out _), "Reject missing level cells without exception");
        Check(config.DailyFor("2026-10-13").id == config.DailyFor("2026-10-13").id && config.DailyFor("invalid") == null, "Stable valid UTC daily selection");
        var records = new List<PopLevelRecord>(); PopSaveStore.RecordWin(records, "day", 3, 2, 100); PopSaveStore.RecordWin(records, "day", 1, 6, 50);
        Check(records.Count == 1 && records[0].stars == 3 && records[0].bestMoves == 2 && records[0].bestScore == 100, "Repeat wins never downgrade or duplicate rewards");
        Check(!PopSaveStore.ValidRun(new PopRunSnapshot()), "Reject incomplete/corrupt snapshots");
        var progress = new PopProgressData(); progress.journey.Add(null); Check(!PopSaveStore.ValidProgress(progress), "Reject null progress record");
    }

    private static void ConfigurePortraitGameView()
    {
        try
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.BaseType.GetProperty("instance", flags).GetValue(null);
            Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize"), modeType = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, flags, null, new object[] { Enum.Parse(modeType, "FixedResolution"), 720, 1600, "Vivo QA" }, null);
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType); view.position = new Rect(0, 0, 450, 950);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
        }
        catch (Exception exception) { Debug.LogWarning("Portrait test GameView setup: " + exception.Message); }
    }

    private static void PrepareAuthoredUIEdits(PopGameplayConfiguration config)
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
        PopGameplayUI ui = Object.FindFirstObjectByType<PopGameplayUI>();
        PopSceneUIAuthoring.Validate(ui, config);
        Check(ui != null && ui.Views.Count == 12 && ui.View(PopUIPage.Victory).GetComponentsInChildren<PopStarGraphic>(true).Length == 3, "All twelve panels and three stars exist in the Game scene before Play");
        ui.ScenePreview = PopUIPage.Journey; ui.PreviewScenePage(); PopSceneUIAuthoring.PreviewJourneyPage(ui, 1);
        Check(ui.View(PopUIPage.Journey).Element("Level11").rect.gameObject.activeSelf && !ui.View(PopUIPage.Journey).Element("Level01").rect.gameObject.activeSelf, "Inspector can preview/edit Journey levels 11-20 without Play");
        Check(PopSceneUIAuthoring.JourneyPreviewPage(ui) == 1, "Journey edit preview page is detected from real authored objects");
        PopSceneUIAuthoring.PreviewJourneyPage(ui, 0);
        Check(ui.View(PopUIPage.Journey).Element("Level01").rect.gameObject.activeSelf && !ui.View(PopUIPage.Journey).Element("Level11").rect.gameObject.activeSelf, "Inspector can return to Journey levels 1-10 without rebuilding objects");
        PopUIView resultView = ui.View(PopUIPage.Victory); PopUIElement resultBody = resultView.Element("Body"); int bodyIndex = resultView.elements.IndexOf(resultBody);
        resultView.elements.RemoveAt(bodyIndex); bool rejected = false;
        try { PopSceneUIAuthoring.Validate(ui, config); } catch (Exception) { rejected = true; }
        finally { resultView.elements.Insert(bodyIndex, resultBody); }
        Check(rejected, "Build guard rejects a deleted required result binding, not only null references");
        UnityEngine.UI.Button nextButton = resultView.Element("Next").button; resultView.Element("Next").button = null; rejected = false;
        try { PopSceneUIAuthoring.Validate(ui, config); } catch (Exception) { rejected = true; }
        finally { resultView.Element("Next").button = nextButton; }
        Check(rejected, "Build guard rejects a disconnected required action button");
        PopUIElement title = ui.View(PopUIPage.Hud).Element("Title");
        title.rect.anchoredPosition = new Vector2(-79, 44); title.rect.sizeDelta = new Vector2(701, 43);
        title.text.color = new Color(.88f, .69f, .51f, 1); title.text.font = config.ui.bodyFont;
        title.text.enableAutoSizing = false; title.text.fontSize = 31.25f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(title.rect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(title.text);
        ui.ScenePreview = PopUIPage.Victory; ui.PreviewScenePage();
        Check(ui.View(PopUIPage.Victory).gameObject.activeSelf && !ui.View(PopUIPage.Home).gameObject.activeSelf, "Inspector preview displays the real editable Victory panel without Play");
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        ui = Object.FindFirstObjectByType<PopGameplayUI>(); PopSceneUIAuthoring.Validate(ui, config);
        title = ui.View(PopUIPage.Home).Element("Title"); title.rect.anchoredPosition = new Vector2(13, title.rect.anchoredPosition.y);
        title.text.color = new Color(.91f, .79f, .65f, 1); title.updateText = false; title.text.text = "CHOOSE YOUR POP · AUTHORED";
        PrefabUtility.RecordPrefabInstancePropertyModifications(title.rect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(title.text);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui.View(PopUIPage.Home));
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene);
        PopSceneUIAuthoring.Install(); // A second migration must preserve authored prefab and scene overrides.
        Check(Object.FindFirstObjectByType<PopGameplayUI>().View(PopUIPage.Home).Element("Title").rect.anchoredPosition.x == 13, "Re-running installer preserves designer scene overrides");
    }
    private static void CheckAuthoredRuntimeEdits()
    {
        PopGameplayUI ui = PopGameplayUI.Instance;
        Check(ui != null && ui.gameObject.scene == SceneManager.GetActiveScene(), "One scene-local authored UI, not a hidden DontDestroy runtime canvas");
        PopUIElement title = ui.View(PopUIPage.Hud).Element("Title");
        Check(title.rect.anchoredPosition == new Vector2(-79, 44) && title.rect.sizeDelta == new Vector2(701, 43), "Authored HUD position and size survive Play and scene reload");
        Check(title.text.font == PopGameplaySession.Instance.Configuration.ui.bodyFont && Mathf.Approximately(title.text.fontSize, 31.25f) && !title.text.enableAutoSizing, "Authored HUD font, size and auto-size choice survive runtime binding");
        Check(title.text.color == new Color(.88f, .69f, .51f, 1), "Authored HUD color survives runtime binding");
        Check(!ui.View(PopUIPage.Victory).gameObject.activeSelf && ui.View(PopUIPage.Hud).gameObject.activeSelf, "Edit-mode Victory preview cannot leak into actual game startup");
    }
    private static void CapturePreview(string name) => ScreenCapture.CaptureScreenshot(Application.dataPath + "/../../progression-" + name + "-preview.png");
    private static void LogPointerDiagnostics()
    {
        Camera camera = Camera.main;
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null) events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = pointerStart }, hits);
        string names = string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name + "/" + (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()?.name ?? "decorative")));
        string colliders = camera == null ? "no camera" : string.Join(", ", Array.ConvertAll(Physics2D.OverlapPointAll(camera.ScreenToWorldPoint(pointerStart)), hit => hit.name));
        Debug.Log("POINTER DIAGNOSTICS: point=" + pointerStart + "; screen=" + Screen.width + "x" + Screen.height + "; camera=" + camera?.pixelRect + "; block=" + pointerBlock.transform.position + "; colliders=" + colliders + "; UI=" + names + "; modal=" + PopGameplayUI.IsModalOpen + "; privacy=" + AdsPrivacyPanel.IsModalOpen + "; gameover=" + GameFlowManager._instance.IsGameOver + "; scale=" + Time.timeScale + "; mouse=" + Mouse.current?.name + "/" + Mouse.current?.leftButton.isPressed);
    }
    private static void PreviewMenu(string name, params object[] arguments) => typeof(PopGameplayUI).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(PopGameplayUI.Instance, arguments);
    private static object Read(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static int Count(bool[] values) { int count = 0; foreach (bool value in values) if (value) ++count; return count; }
    private static void Check(bool passed, string message) { ++checks; if (!passed) throw new Exception(message); }
    private static void Arm(int newStage, double seconds) { stage = newStage; requiredFrame = Time.frameCount + 2; next = EditorApplication.timeSinceStartup + seconds; }
    private static void OnLog(string text, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) { Debug.LogWarning("Known isolated Editor Search startup defect; excluded from gameplay runtime assertions."); return; }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(text);
    }
}
