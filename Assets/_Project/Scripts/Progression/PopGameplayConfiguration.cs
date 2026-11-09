using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Append modes: existing numeric save identities must never change.
public enum PopGameMode { Classic, Journey, Daily, Tutorial, EndlessPop }

[Serializable]
public sealed class PopStartingCell
{
    public Vector2Int position;
    [Min(0)] public int colorId;
    public bool bubble;
}

[Serializable]
public sealed class PopSolutionMove
{
    [Tooltip("Index in Polyominos. The Inspector editor previews this shape.")]
    public int shape;
    public Vector2Int origin;
}

[Serializable]
public sealed class PopLevelDefinition
{
    [Tooltip("Permanent save identity. Do not rename an ID after publishing it.")]
    public string id;
    public string title;
    [TextArea(2, 4)] public string description;
    [Min(1)] public int moveLimit = 8;
    [Min(0)] public int targetLines = 3;
    [Min(0)] public int targetBubbles;
    [Min(0)] public int targetScore;
    [Min(0)] public int targetCombo;
    [Tooltip("Largest number of rows/columns cleared by ONE placement.")]
    [Min(0)] public int targetMultiClear;
    [Tooltip("Earn 3 stars at or below this number of placements.")]
    [Min(1)] public int threeStarMoves = 3;
    [Tooltip("Earn 2 stars at or below this number; any win earns at least 1.")]
    [Min(1)] public int twoStarMoves = 5;
    public List<PopStartingCell> startingCells = new();
    [Tooltip("Fixed piece deck, dealt three at a time. No adaptive random generator in authored modes.")]
    public List<int> pieceSequence = new();
    [Tooltip("An actual playable solution, used by the editor/build validation and guided tutorial.")]
    public List<PopSolutionMove> verifiedSolution = new();

    public bool GoalsMet(int lines, int bubbles, int score, int combo, int multiClear) =>
        lines >= targetLines && bubbles >= targetBubbles && score >= targetScore &&
        combo >= targetCombo && multiClear >= targetMultiClear;

    public int StarsFor(int moves) => moves <= threeStarMoves ? 3 : moves <= twoStarMoves ? 2 : 1;
}

[Serializable]
public sealed class PopThemeDefinition
{
    public string id;
    public string title;
    [TextArea] public string description;
    [Min(0)] public int requiredJourneyStars;
    [Min(0)] public int requiredDailyWins;
    [Min(0)] public int requiredClassicBest;
    [Tooltip("All non-zero requirements must be met. Sky is always available.")]
    public Color[] blockColors;
    public Color backgroundTint = Color.white;
    public Sprite backgroundOverride;
    public Color accent = new(0.04f, 0.75f, 0.8f);
    public Color clearEffect = new(0.1f, 0.9f, 1f);
    public Color bubbleColor = new(1f, 0.84f, 0.25f);
}

[Serializable]
public sealed class PopEffectDefinition
{
    public string id, title;
    [TextArea] public string description;
    [Tooltip("Largest clear in one placement, earned in Classic/Journey/Daily. Tutorial does not grant achievements.")]
    [Range(0, 16)] public int requiredSingleClearLines;
    [Min(0)] public int requiredBestCombo;
    public bool radialBurst;
    [Range(1f, 3f)] public float particleMultiplier = 1f;
    [Range(0.5f, 2f)] public float speedMultiplier = 1f;
    [Range(0.5f, 2f)] public float lifetimeMultiplier = 1f;
    public Color tint = Color.white;
    [Range(0f, 1f)] public float tintStrength;
}

[Serializable]
public sealed class PopBubbleFeedbackSettings
{
    public bool enabled = true;
    [Range(.1f, .8f)] public float burstSeconds = .38f;
    [Range(.2f, 2f)] public float ringDiameter = 1.25f;
    [Range(0, .15f)] public float staggerSeconds = .035f;
    [Range(0, 1)] public float soundGain = .55f;
    public Color popColor = new(1, .83f, .27f);
    public bool waveClearSound = true;
}

[Serializable]
public sealed class PopDailySettings
{
    public bool enabled = true;
    [Tooltip("Daily resets at 00:00 UTC. Device-clock dates are local/offline, not anti-cheat server rankings.")]
    public string scheduleRevision = "daily-v1";
    [Tooltip("IDs of validated Journey levels used as a rotating offline daily puzzle bank.")]
    public List<string> levelIds = new();
    [Tooltip("Fixed permutation seed. Everyone on this revision/date receives the same bank entry and deck.")]
    public int rotationSeed = 20261013;
    [Min(7)] public int calendarDaysToShow = 28;
    [Min(1)] public int celebrationEveryWins = 7;
}

[Serializable]
public sealed class PopTutorialSettings
{
    [Header("Endless Pop first-play lesson (independent of Classic)")]
    public bool automaticOnFirstEndless = true;
    public PopLevelDefinition endlessLesson = PopLevelCatalog.CreateEndlessLesson();
    [TextArea] public string[] endlessInstructions =
    {
        "Xóa hàng có bóng: kéo khối một ô vào chỗ sáng. Mỗi khối đặt thành công tốn 1 lượt.",
        "Xóa hàng tiếp theo để lấy bóng. Hai khối còn lại sẽ chuẩn bị một lượt xóa kép.",
        "Nhìn TIẾP THEO: còn một thanh dọc. Đặt thanh hiện tại vào cột sáng, giữ chỗ cho thanh đó.",
        "Khay mới đã tới! Đặt thanh dọc vào chỗ sáng để phá hai bóng cùng lúc. Trong ván thật, qua đợt vẫn giữ bàn."
    };
    public bool automaticOnFirstClassic = true;
    public bool skipAutomaticForExistingHighScore = true;
    public bool allowSkip = true;
    public bool highlightExactPlacement = true;
    [TextArea] public string[] stepInstructions =
    {
        "Drag the one-cell block into the glowing gap. Complete a row!",
        "Drag the three-cell block into the next glowing row.",
        "Place the square in the glowing gap. Clear TWO rows at once!"
    };
    public PopLevelDefinition lesson;
}

[Serializable]
public sealed class PopRunSettings
{
    public bool resumeClassic = true;
    public bool resumeJourney = true;
    public bool resumeDaily = true;
    [Min(0.25f)] public float diskFlushIntervalSeconds = 1f;
    [Tooltip("Ads stay optional; authored Journey/Daily/Tutorial never need ad revives.")]
    public bool classicRewardedRevive = true;
    [Tooltip("True restores the previous real-time combo expiry rule; false lets players think without a timer.")]
    public bool timedCombo;
    [Min(0.5f)] public float timedComboWindowSeconds = 3.5f;
    [Min(2)] public int feverStartCombo = 4;
    [Min(0.2f)] public float feverVisualDurationSeconds = 3.2f;
    [Tooltip("Leave false for play-first onboarding. Age/consent is still required before any ad request.")]
    public bool promptForAdAgeOnLaunch;
    [Tooltip("0 uses a fresh seed per Classic run. Non-zero gives reproducible Classic trays for testing.")]
    public int classicFixedSeed;
}

[Serializable]
public sealed class PopScoringSettings
{
    [Min(0)] public int placementPointsPerCell = 3;
    [Min(1)] public int pointsPerLine = 40;
    [Range(0, 0.5f)] public float comboBonusPerChain = 0.12f;
    [Min(1)] public float maximumComboMultiplier = 2.5f;
}

[Serializable]
public sealed class PopUiSettings
{
    [Header("Initial UI authoring defaults (not runtime layout overrides)")]
    [Tooltip("These style/layout defaults seed a new prefab once. Edit the saved Blokki Pop UI prefab/scene objects for subsequent visual changes; the installer never resets designer edits.")]
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;
    public Color card = new(0.055f, 0.14f, 0.20f, 0.97f);
    public Color text = new(0.96f, 0.99f, 1f);
    public Color mutedText = new(0.70f, 0.82f, 0.88f);
    public Color locked = new(0.19f, 0.28f, 0.34f);
    public Color modalDimmer = new(0.01f, 0.025f, 0.055f, 0.84f);
    public Vector2 referenceResolution = new(1080f, 1920f);
    [Range(0, 1)] public float screenMatch = 0.5f;
    public Vector2 homeCardSize = new(850f, 900f);
    public Vector2 homeCardPosition = new(0f, -250f);
    public Vector2 modalCardSize = new(900f, 1340f);
    public Vector2 primaryButtonSize = new(750f, 90f);
    [Min(10)] public float headingFontSize = 44f;
    [Min(10)] public float bodyFontSize = 25f;
    [Min(10)] public float buttonFontSize = 29f;
    [Min(10)] public float smallFontSize = 21f;
    [Min(1)] public int levelsPerPage = 10;
    [Min(1)] public int levelColumns = 2;
    public Vector2 levelButtonSize = new(380f, 116f);
    public Vector2 levelButtonSpacing = new(420f, 145f);
    public Vector2 hudSize = new(950f, 145f);
    public float hudTopInset = 215f;
    public Vector2 pauseButtonSize = new(145f, 62f);
    public Vector2 tutorialCardSize = new(900f, 195f);
    public float tutorialBottomInset = 180f;
    [Range(0.1f, 0.9f)] public float bubbleDiameterInCells = 0.45f;
    [Range(0.1f, 0.9f)] public float tutorialGhostOpacity = 0.40f;
    public Color tutorialGhostColor = new(1f, 0.93f, 0.36f);
    public Sprite buttonSprite;
    public Sprite cardSprite;
    [Header("Menu spacing / result rewards")]
    public float homeTitleY = 370f;
    public float homeStatsY = 310f;
    public float homeFirstButtonY = 215f;
    public float homeButtonSpacing = 108f;
    public float homeFooterGap = 95f;
    public float homeFooterHalfGap = 195f;
    public Vector2 secondaryButtonSize = new(350f, 62f);
    public Vector2 outcomeStarSize = new(88f, 88f);
    public float outcomeStarSpacing = 120f;
    public float outcomeStarY = 325f;
    public Color earnedStar = new(1f, 0.83f, 0.24f);
    public Color emptyStar = new(0.25f, 0.36f, 0.43f);
    [Min(0.05f)] public float starRevealSeconds = 0.23f;
    public float hudTitleY = -25f;
    public float hudStatsY = -65f;
    public float hudGoalsY = -108f;
    [Min(0.1f)] public float rewardNoticeSeconds = 3f;
}

[Serializable]
public sealed class PopUiText
{
    public string homeTitle = "CHOOSE YOUR POP";
    public string classic = "CLASSIC ENDLESS";
    public string journey = "POP JOURNEY";
    public string daily = "DAILY POP";
    public string continueRun = "CONTINUE";
    public string newRun = "NEW CLASSIC RUN";
    public string themes = "THEMES & REWARDS";
    public string tutorial = "LEARN TO PLAY";
    public string privacy = "AD PRIVACY";
    public string home = "HOME";
    public string back = "BACK";
    public string play = "PLAY";
    public string retry = "TRY AGAIN";
    public string next = "NEXT LEVEL";
    public string close = "CLOSE";
    public string resume = "KEEP PLAYING";
    public string pause = "PAUSE";
    public string victory = "YOU POPPED IT!";
    public string failed = "TRY A NEW PLAN";
    public string skipTutorial = "SKIP LESSON";
    public string selected = "SELECTED";
    public string locked = "LOCKED";
    public string stars = "stars";
    public string best = "BEST";
    public string moves = "MOVES";
    public string lines = "LINES";
    public string bubbles = "BUBBLES";
    public string score = "SCORE";
    public string combo = "COMBO";
    public string multiClear = "DOUBLE+";
    public string journeyComplete = "JOURNEY COMPLETE!";
    public string tutorialComplete = "READY TO POP!";
    public string lessonHint = "Use the highlighted piece and landing cells.";
    public string dailyCompleted = "TODAY'S PUZZLE COMPLETE";
    public string dailyTrophy = "DAILY TROPHY";
    public string dailyWins = "Daily wins";
    public string themeUnlocked = "New theme available!";
    public string rewardUnlocked = "UNLOCKED: {0}";
    public string popEffects = "POP EFFECTS";
    public string effectRequirements = "Single clear: {0} lines | Best combo: {1}";
    public string classicDescription = "No move limit. Build your best score.";
    public string pauseNotice = "Your board and remaining pieces are saved when you leave.";
    public string invalidSaveNotice = "This saved run is no longer compatible. A fresh board was started; your records are safe.";
    public string noMoves = "No remaining piece fits. Try a different placement order.";
    public string outOfMoves = "Move limit reached. Look for a double clear or a shorter route.";
    public string threeStarGuide = "3 stars: {0} moves or fewer | 2 stars: {1} or fewer";
    public string levelLabel = "LEVEL {0}: {1}";
    public string progressLabel = "{0}/{1} stars | {2}/{3} levels cleared";
    public string themeRequirements = "{0} Journey stars | {1} Daily wins | {2} Classic best";
    [TextArea] public string dailyNotice = "One shared puzzle each UTC day. Offline device-clock schedule; no online rankings.";
    [TextArea] public string replaceRunWarning = "Start a new run? Your saved Classic board will be replaced. Your best score stays safe.";
    [TextArea] public string goalReminder = "Fill rows or columns to pop their bubbles. Think ahead: pieces do not rotate.";
}

[CreateAssetMenu(fileName = "PopGameplayConfiguration", menuName = "Blokki Pop/Gameplay & Progression Configuration")]
public sealed class PopGameplayConfiguration : ScriptableObject
{
    public const string ResourcePath = "Gameplay/PopGameplayConfiguration";
    public const string AssetPath = "Assets/_Project/Resources/Gameplay/PopGameplayConfiguration.asset";
    [Tooltip("Increment when changing authored decks/layout/rules. Incompatible unfinished runs are not restored.")]
    [Min(1)] public int contentRevision = 1;
    public PopRunSettings runs = new();
    public PopTutorialSettings tutorial = new();
    public PopDailySettings daily = new();
    public PopEndlessSettings endless = new();
    public PopBubbleFeedbackSettings bubbleFeedback = new();
    public PopUiSettings ui = new();
    public PopUiText text = new();
    [Tooltip("Authored modes use these rules so score goals and solution validation agree. Classic keeps its Board Inspector rules.")]
    public PopScoringSettings authoredScoring = new();
    public List<PopLevelDefinition> journeyLevels = new();
    public List<PopThemeDefinition> themes = new();
    public List<PopEffectDefinition> popEffects = new();

    public PopLevelDefinition FindLevel(string id) => journeyLevels.Find(level => level != null && level.id == id);
    public PopThemeDefinition FindTheme(string id) => themes.Find(theme => theme != null && theme.id == id);
    public PopEffectDefinition FindEffect(string id) => popEffects.Find(effect => effect != null && effect.id == id);

    public PopLevelDefinition DailyFor(string utcDate)
    {
        if (!daily.enabled || daily.levelIds.Count == 0 || !DateTime.TryParseExact(utcDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
            return null;
        long day = (long)(date - new DateTime(2020, 1, 1)).TotalDays;
        int index = (int)(((day + daily.rotationSeed) % daily.levelIds.Count + daily.levelIds.Count) % daily.levelIds.Count);
        return FindLevel(daily.levelIds[index]);
    }

    public static PopGameplayConfiguration CreateDefaults()
    {
        var config = CreateInstance<PopGameplayConfiguration>();
        PopLevelCatalog.FillDefaults(config);
        return config;
    }
}
