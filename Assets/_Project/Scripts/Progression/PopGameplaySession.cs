using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One mode/progression owner. Counters and saves are committed after a whole placement/tray transaction.</summary>
[DefaultExecutionOrder(-950)]
public sealed partial class PopGameplaySession : MonoBehaviour
{
    public static PopGameplaySession Instance { get; private set; }
    public event Action Changed;
    [SerializeField] private PopGameplayConfiguration configuration;
    [Header("Runtime state")]
    [SerializeField] private PopGameMode mode;
    [SerializeField] private string levelId;
    [SerializeField] private string date;
    [SerializeField] private int moves, lines, bubblesPopped, maxCombo, largestClear;
    [SerializeField] private int deckCursor;
    [SerializeField] private bool active, finished;

    private PopSaveStore store;
    private Board board;
    private Blocks tray;
    private PopLevelDefinition definition;
    private bool ownsConfiguration, hasPending, pendingResume, pendingRestoreFailed;
    private PopGameMode pendingMode;
    private string pendingLevel, pendingDate;
    private PopRunSnapshot pendingSnapshot;
    private float nextFlush;
    private PopGameMode afterTutorialMode;
    private string afterTutorialLevel, afterTutorialDate;
    private bool hasAfterTutorial;
    private bool placementPending;
    private readonly List<string> newlyUnlockedThemes = new();
    private readonly List<string> newlyUnlockedEffects = new();
    public IReadOnlyList<string> NewlyUnlockedThemes => newlyUnlockedThemes;
    public IReadOnlyList<string> NewlyUnlockedEffects => newlyUnlockedEffects;
    public bool DailyTrophyEarned { get; private set; }
    public bool IsInitialized => active;

    public PopGameplayConfiguration Configuration => configuration;
    public PopProgressData Progress => store.Progress;
    public PopGameMode Mode => mode;
    public PopLevelDefinition Definition => definition;
    public bool IsActive => active && !finished;
    public bool IsAuthored => mode != PopGameMode.Classic;
    public bool AllowsClassicScore => mode == PopGameMode.Classic;
    public bool AllowsRevive => mode == PopGameMode.Classic && configuration.runs.classicRewardedRevive;
    public int Moves => moves;
    public int Lines => lines;
    public int BubblesPopped => bubblesPopped;
    public int MaxCombo => maxCombo;
    public int LargestClear => largestClear;
    public string LevelId => levelId;
    public string RunDate => date;
    public string UtcToday => DateTime.UtcNow.ToString("yyyy-MM-dd");
    public bool RestoreWasRejected => pendingRestoreFailed;
    public int DailyWins => store.Progress.daily.Count;
    public int TotalJourneyStars
    {
        get
        {
            int sum = 0;
            foreach (PopLevelDefinition level in configuration.journeyLevels) sum += Stars(level.id);
            return sum;
        }
    }
    public PopThemeDefinition SelectedTheme => UsesAutomaticBackgrounds ? configuration.themes[0] : configuration.FindTheme(store.Progress.selectedTheme) ?? configuration.themes[0];
    public PopEffectDefinition SelectedEffect => configuration.FindEffect(store.Progress.selectedEffect) ?? configuration.popEffects[0];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("Pop Gameplay & Progression").AddComponent<PopGameplaySession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (configuration == null) configuration = Resources.Load<PopGameplayConfiguration>(PopGameplayConfiguration.ResourcePath);
        if (configuration == null) { configuration = PopGameplayConfiguration.CreateDefaults(); ownsConfiguration = true; }
        store = new PopSaveStore();
        if (!ThemeUnlocked(SelectedTheme)) store.Progress.selectedTheme = configuration.themes[0].id;
        if (!EffectUnlocked(SelectedEffect)) store.Progress.selectedEffect = configuration.popEffects[0].id;
        SceneManager.activeSceneChanged += SceneChanged;
    }

    private void Update()
    {
        TickEndlessTransition();
        if (Time.unscaledTime < nextFlush) return;
        nextFlush = Time.unscaledTime + Mathf.Max(0.25f, configuration.runs.diskFlushIntervalSeconds);
        store.Flush();
    }

    private void SceneChanged(Scene oldScene, Scene next)
    {
        active = false;
        placementPending = false;
        board = null; tray = null;
        Notify();
    }

    public int Stars(string id) => PopSaveStore.Find(store.Progress.journey, id)?.stars ?? 0;
    public bool LevelUnlocked(int index) => index >= 0 && index < configuration.journeyLevels.Count &&
        (index == 0 || Stars(configuration.journeyLevels[index - 1].id) > 0);
    public bool ThemeUnlocked(PopThemeDefinition theme) => theme != null &&
        TotalJourneyStars >= theme.requiredJourneyStars && DailyWins >= theme.requiredDailyWins &&
        GameFlowManager.SavedHighScore >= theme.requiredClassicBest;

    public void SelectTheme(string id)
    {
        if (UsesAutomaticBackgrounds) return; // Old selection remains saved, but no longer owns the run's background.
        PopThemeDefinition theme = configuration.FindTheme(id);
        if (!ThemeUnlocked(theme)) return;
        store.Progress.selectedTheme = id;
        store.SaveProgress(); store.Flush();
        ApplyTheme(); Notify();
    }

    public bool EffectUnlocked(PopEffectDefinition effect) => effect != null &&
        store.Progress.largestSingleClear >= effect.requiredSingleClearLines && store.Progress.bestCombo >= effect.requiredBestCombo;

    public void SelectEffect(string id)
    {
        PopEffectDefinition effect = configuration.FindEffect(id);
        if (!EffectUnlocked(effect)) return;
        store.Progress.selectedEffect = id; store.SaveProgress(); store.Flush(); Notify();
    }

    public PopRunSnapshot SavedRun(PopGameMode wantedMode)
    {
        if (wantedMode == PopGameMode.Tutorial || wantedMode == PopGameMode.Classic && !configuration.runs.resumeClassic ||
            wantedMode == PopGameMode.Journey && !configuration.runs.resumeJourney || wantedMode == PopGameMode.Daily && !configuration.runs.resumeDaily ||
            wantedMode == PopGameMode.EndlessPop && !configuration.endless.resumeRun)
            return null;
        PopRunSnapshot run = store.LoadRun(wantedMode);
        if (run == null || run.contentRevision != configuration.contentRevision) return null;
        if (wantedMode == PopGameMode.Journey && configuration.FindLevel(run.levelId) == null) return null;
        if (wantedMode == PopGameMode.EndlessPop && run.endlessRevision != configuration.endless.generatorRevision) return null;
        if (wantedMode == PopGameMode.Daily && (run.utcDate != UtcToday || run.dailyRevision != configuration.daily.scheduleRevision ||
            configuration.DailyFor(run.utcDate)?.id != run.levelId)) return null;
        return run;
    }

    public void PlayClassic(bool resume)
    {
        if (!resume && configuration.tutorial.automaticOnFirstClassic && !store.Progress.tutorialComplete &&
            !(configuration.tutorial.skipAutomaticForExistingHighScore && GameFlowManager.SavedHighScore > 0))
        {
            afterTutorialMode = PopGameMode.Classic; hasAfterTutorial = true;
            StartMode(PopGameMode.Tutorial, configuration.tutorial.lesson.id, "", false); return;
        }
        StartMode(PopGameMode.Classic, "", "", resume);
    }

    public void PlayJourney(int index, bool resume = false)
    {
        if (!LevelUnlocked(index)) return;
        StartMode(PopGameMode.Journey, configuration.journeyLevels[index].id, "", resume);
    }

    public void PlayDaily(bool resume = false)
    {
        PopLevelDefinition level = configuration.DailyFor(UtcToday);
        if (level != null) StartMode(PopGameMode.Daily, level.id, UtcToday, resume);
    }

    public void PlayTutorial()
    {
        hasAfterTutorial = false;
        StartMode(PopGameMode.Tutorial, configuration.tutorial.lesson.id, "", false);
    }

    private void StartMode(PopGameMode wantedMode, string wantedLevel, string wantedDate, bool resume)
    {
        SaveBeforeNavigation();
        pendingMode = wantedMode; pendingLevel = wantedLevel; pendingDate = wantedDate;
        pendingSnapshot = resume ? SavedRun(wantedMode) : null;
        if (pendingSnapshot != null && wantedMode != PopGameMode.Classic && wantedMode != PopGameMode.EndlessPop && pendingSnapshot.levelId != wantedLevel) pendingSnapshot = null;
        pendingResume = pendingSnapshot != null;
        pendingRestoreFailed = resume && !pendingResume;
        hasPending = true;
        // Home has a preloaded scene. Use its transition instead of queuing a second load behind it.
        BlokkiPopIntroController intro = FindFirstObjectByType<BlokkiPopIntroController>();
        if (intro != null) intro.ActivatePreparedGame();
        else if (GameFlowManager._instance != null) GameFlowManager._instance.GamePlay();
        else { Time.timeScale = 1f; SceneManager.LoadScene(1); }
    }

    public void PrepareLegacyPlay()
    {
        SaveBeforeNavigation();
        if (hasPending) return;
        pendingMode = PopGameMode.Classic; pendingLevel = pendingDate = ""; pendingSnapshot = null; pendingResume = false; hasPending = true;
    }

    public void PrepareRestart()
    {
        SaveBeforeNavigation();
        if (hasPending) return;
        pendingMode = mode; pendingLevel = levelId; pendingDate = date;
        pendingSnapshot = null; pendingResume = false; hasPending = true;
    }

    public void InitializeBoardAndTray(Board newBoard, Blocks newTray)
    {
        board = newBoard; tray = newTray;
        mode = hasPending ? pendingMode : PopGameMode.Classic;
        levelId = hasPending ? pendingLevel : "";
        date = hasPending ? pendingDate : "";
        definition = mode == PopGameMode.EndlessPop ? pendingSnapshot?.endlessDefinition : mode == PopGameMode.Tutorial
            ? levelId == configuration.tutorial.endlessLesson.id ? configuration.tutorial.endlessLesson : configuration.tutorial.lesson : configuration.FindLevel(levelId);
        if (IsAuthored && definition == null && mode != PopGameMode.EndlessPop) { mode = PopGameMode.Classic; levelId = date = ""; }
        moves = lines = bubblesPopped = maxCombo = largestClear = deckCursor = 0;
        finished = placementPending = false;
        endlessPendingWave = false;
        if (mode != PopGameMode.EndlessPop) waveStartMoves = waveStartBubbles = 0;
        newlyUnlockedThemes.Clear(); newlyUnlockedEffects.Clear(); DailyTrophyEarned = false;
        board.ApplyRunConfiguration(configuration, IsAuthored);
        tray.InitializeGenerator(configuration.runs.classicFixedSeed != 0
            ? (uint)configuration.runs.classicFixedSeed : unchecked((uint)DateTime.UtcNow.Ticks));
        active = true;
        ApplyTheme();
        bool restored = false;
        PopAmbientRunState restoredAmbience = null;
        if (pendingResume && pendingSnapshot != null)
        {
            PopRunSnapshot saved = pendingSnapshot;
            if (saved.mode == mode && saved.rulesSignature == RulesSignature() && AuthoredSnapshotIsConsistent(saved) && board.CanImport(saved) && tray.CanImport(saved))
            {
                moves = saved.moves; lines = saved.lines; bubblesPopped = saved.bubblesPopped;
                maxCombo = saved.maxCombo; largestClear = saved.largestClear; deckCursor = saved.deckCursor;
                board.Import(saved); tray.Import(saved);
                GameFlowManager._instance?.RestoreRun(saved.score, saved.lines, saved.maxCombo, saved.reviveUsed);
                restoredAmbience = saved.ambience;
                if (mode == PopGameMode.EndlessPop) RestoreEndless(saved);
                restored = true;
            }
            else pendingRestoreFailed = true;
        }
        hasPending = pendingResume = false; pendingSnapshot = null;
        if (!restored)
        {
            GameFlowManager._instance?.RestoreRun(0, 0, 0, false);
            if (mode == PopGameMode.EndlessPop) StartFreshEndless();
            else if (IsAuthored) board.ApplyStartingCells(definition.startingCells);
            tray.DealNextTray();
        }
        InitializeAmbience(restoredAmbience);
        Notify();
        SaveCurrentRun();
        if (!IsTransitioning && !tray.HasPlayablePiece) GameFlowManager._instance?.GameOver();
    }

    public int[] TakeAuthoredTray()
    {
        if (!IsAuthored || definition == null) return null;
        var result = new[] { -1, -1, -1 };
        for (int i = 0; i < result.Length && deckCursor < definition.pieceSequence.Count; ++i)
            result[i] = definition.pieceSequence[deckCursor++];
        return result;
    }

    public bool CanPlace(int shape, Vector2Int origin)
    {
        if (active && finished || IsTransitioning) return false;
        if (!IsActive || mode != PopGameMode.Tutorial) return true;
        if (moves >= definition.verifiedSolution.Count) return false;
        PopSolutionMove step = definition.verifiedSolution[moves];
        bool correct = shape == step.shape && origin == step.origin;
        if (!correct) PopGameplayUI.Instance?.ShowTutorialHint();
        return correct;
    }

    public void RecordBoardPlacement(int clearedLines, int popped, int combo)
    {
        if (!IsActive) return;
        ++moves; lines += clearedLines; bubblesPopped += popped;
        maxCombo = Mathf.Max(maxCombo, combo); largestClear = Mathf.Max(largestClear, clearedLines);
        if (mode != PopGameMode.Tutorial && (largestClear > store.Progress.largestSingleClear || maxCombo > store.Progress.bestCombo))
        {
            var locked = configuration.popEffects.FindAll(effect => !EffectUnlocked(effect));
            store.RecordClearAchievement(mode, largestClear, maxCombo);
            foreach (PopEffectDefinition effect in locked) if (EffectUnlocked(effect)) newlyUnlockedEffects.Add(effect.title);
        }
        placementPending = true;
    }

    // Called after the dragged block is removed, but BEFORE a new tray is generated.
    public bool CompletePlacement()
    {
        if (!IsActive || !placementPending) return false;
        placementPending = false;
        Notify();
        if (mode == PopGameMode.EndlessPop) return CompleteEndlessPlacement();
        if (IsAuthored)
        {
            int score = GameFlowManager._instance != null ? GameFlowManager._instance.Score : 0;
            if (definition.GoalsMet(lines, bubblesPopped, score, maxCombo, largestClear))
            { GameFlowManager._instance?.GameWin(); return true; }
            if (moves >= definition.moveLimit)
            { GameFlowManager._instance?.GameOver(); return true; }
        }
        return false;
    }

    public void AfterTrayTransaction()
    {
        SaveCurrentRun();
        if (IsEndless) store.Flush();
        Notify();
    }

    public void FinishWin()
    {
        if (!IsActive || !IsAuthored || IsEndless) return;
        finished = true;
        EndAmbience();
        int score = GameFlowManager._instance != null ? GameFlowManager._instance.Score : 0;
        int stars = definition.StarsFor(moves);
        var previouslyLocked = configuration.themes.FindAll(theme => !UsesAutomaticBackgrounds && !ThemeUnlocked(theme));
        int previousDailyWins = DailyWins;
        if (mode == PopGameMode.Journey) PopSaveStore.RecordWin(store.Progress.journey, levelId, stars, moves, score);
        else if (mode == PopGameMode.Daily) PopSaveStore.RecordWin(store.Progress.daily, DailyRecordId(date), stars, moves, score);
        else if (IsEndlessLesson) store.Progress.endlessTutorialComplete = true;
        else store.Progress.tutorialComplete = true;
        foreach (PopThemeDefinition theme in previouslyLocked) if (ThemeUnlocked(theme)) newlyUnlockedThemes.Add(theme.title);
        DailyTrophyEarned = DailyWins > previousDailyWins && DailyWins % Mathf.Max(1, configuration.daily.celebrationEveryWins) == 0;
        store.ClearRun(mode); store.SaveProgress(); store.Flush();
        Notify(); PopGameplayUI.Instance?.ShowOutcome(true, stars);
    }

    public void FinishFailure()
    {
        if (!IsActive) return;
        finished = true;
        EndAmbience();
        if (mode == PopGameMode.EndlessPop) RecordEndlessBest();
        store.ClearRun(mode); store.Flush(); Notify();
        if (IsAuthored) PopGameplayUI.Instance?.ShowOutcome(false, 0);
    }

    public void Retry()
    {
        if (mode == PopGameMode.EndlessPop) { PlayEndless(false); return; }
        pendingMode = mode; pendingLevel = levelId; pendingDate = date;
        hasPending = true; pendingResume = pendingRestoreFailed = false; pendingSnapshot = null;
        GameFlowManager._instance?.RestartGame();
    }

    public void NextLevel()
    {
        int index = configuration.journeyLevels.FindIndex(level => level.id == levelId);
        if (index >= 0 && index + 1 < configuration.journeyLevels.Count) PlayJourney(index + 1);
        else GoHome();
    }

    public void CompleteTutorial(bool skipped = false)
    {
        if (mode != PopGameMode.Tutorial) return;
        if (skipped && !configuration.tutorial.allowSkip) return;
        if (IsEndlessLesson) store.Progress.endlessTutorialComplete = true;
        else store.Progress.tutorialComplete = true;
        store.SaveProgress(); store.Flush();
        if (hasAfterTutorial)
        {
            hasAfterTutorial = false;
            StartMode(afterTutorialMode, afterTutorialLevel ?? "", afterTutorialDate ?? "", false);
        }
        else GoHome();
    }

    public void GoHome()
    {
        hasAfterTutorial = false;
        SaveBeforeNavigation();
        GameFlowManager._instance?.ReturnHome();
    }

    public void SaveBeforeNavigation() { SaveCurrentRun(); store?.Flush(); }
    public void OnRevived() { SaveCurrentRun(); store.Flush(); Notify(); }
    public void RefreshUi() => Notify();
    public string DailyRecordId(string utcDate) => configuration.daily.scheduleRevision + "/" + utcDate;
    public bool DailyComplete(string utcDate) => PopSaveStore.Find(store.Progress.daily, DailyRecordId(utcDate)) != null;

    public string RulesSignature()
    {
        string content = FormattableString.Invariant($"{configuration.contentRevision}/{mode}/{board.ScoringSignature}/{configuration.runs.timedCombo}/{configuration.runs.timedComboWindowSeconds:R}/{Polyominos.Lenght}");
        if (!IsAuthored) content += "/" + tray.GenerationSignature;
        if (IsAuthored) content += "/" + JsonUtility.ToJson(definition);
        if (mode == PopGameMode.EndlessPop) content += "/" + configuration.endless.generatorRevision;
        return Hash128.Compute(content).ToString();
    }

    private bool AuthoredSnapshotIsConsistent(PopRunSnapshot saved)
    {
        if (!IsAuthored) return true;
        if (mode == PopGameMode.EndlessPop) return PopEndlessGenerator.ValidSnapshot(saved);
        if (saved.moves >= definition.moveLimit || saved.deckCursor > definition.pieceSequence.Count || saved.reviveUsed ||
            saved.maxCombo > saved.moves || saved.combo > saved.moves || saved.largestClear > 16 || saved.lines > (long)saved.moves * 16) return false;
        int initialBubbles = definition.startingCells.FindAll(cell => cell.bubble).Count, remaining = 0;
        foreach (bool bubble in saved.bubbleTargets) if (bubble) ++remaining;
        return saved.bubblesPopped + remaining == initialBubbles;
    }

    public void SaveCurrentRun()
    {
        if (!IsActive || placementPending || board == null || tray == null || mode == PopGameMode.Tutorial) return;
        if (mode == PopGameMode.Classic && !configuration.runs.resumeClassic || mode == PopGameMode.Journey && !configuration.runs.resumeJourney ||
            mode == PopGameMode.Daily && !configuration.runs.resumeDaily || mode == PopGameMode.EndlessPop && !configuration.endless.resumeRun) return;
        GameFlowManager manager = GameFlowManager._instance;
        if (manager == null) return;
        var snapshot = new PopRunSnapshot
        {
            contentRevision = configuration.contentRevision, rulesSignature = RulesSignature(), mode = mode,
            levelId = levelId, utcDate = date, dailyRevision = configuration.daily.scheduleRevision,
            score = manager.Score, moves = moves, lines = lines, bubblesPopped = bubblesPopped, maxCombo = maxCombo,
            largestClear = largestClear, deckCursor = deckCursor, reviveUsed = manager.HasUsedRevive,
            occupiedColors = new int[64], bubbleTargets = new bool[64],
            trayShapes = new int[3], trayColors = new int[3], trayAvailable = new bool[3]
        };
        board.Export(snapshot); tray.Export(snapshot);
        if (mode == PopGameMode.EndlessPop) ExportEndless(snapshot);
        snapshot.ambience = UsesAutomaticBackgrounds ? ambience.Copy() : null;
        store.SaveRun(snapshot);
    }

    private void ApplyTheme()
    {
        board?.RefreshTheme();
        tray?.RefreshTheme();
        foreach (ModernBackground background in FindObjectsByType<ModernBackground>(FindObjectsSortMode.None))
            background.ApplyTheme(SelectedTheme);
    }

    private void Notify()
    {
        if (Changed == null) return;
        foreach (Action callback in Changed.GetInvocationList())
            try { callback(); } catch (Exception exception) { Debug.LogException(exception); }
    }

    private void OnApplicationPause(bool paused) { if (paused) SaveBeforeNavigation(); }
    private void OnApplicationQuit() { SaveBeforeNavigation(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        SaveBeforeNavigation();
        SceneManager.activeSceneChanged -= SceneChanged;
        Changed = null; Instance = null;
        if (ownsConfiguration && configuration != null) Destroy(configuration);
    }
}
