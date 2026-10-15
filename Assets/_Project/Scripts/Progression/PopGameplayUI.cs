using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-local controller. Prefab owns layout/style; code only binds data/actions.</summary>
[ExecuteAlways, RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class PopGameplayUI : MonoBehaviour
{
    public static PopGameplayUI Instance { get; private set; }
    public static bool IsModalOpen => Instance != null && (Instance.modal != null || Instance.homePresentation != null && Instance.homePresentation.IsSettingsOpen);
    [Header("Authored objects (never rebuilt during play)")]
    [SerializeField] private RectTransform safeRoot;
    [SerializeField] private PopUIView[] views;
    [Tooltip("Edit-mode preview only; Play chooses the page from the actual game state.")]
    [SerializeField] private PopUIPage scenePreview = PopUIPage.Home;
    [Tooltip("Fits only Safe Area anchors to device cutouts, in edit preview and Play. Your offsets, size, pivot and child layout are preserved. Disable to control its anchors too.")]
    [SerializeField] private bool fitDeviceSafeArea = true;
    [Header("Home intro")]
    [Tooltip("Let the logo and falling blocks finish before showing the Home menu, so the menu cannot cover their entrance. Disable to show the menu immediately alongside the animation. Does not change any layout.")]
    [SerializeField] private bool waitForHomeIntroAnimation = true;
    [Header("Result animation / data-driven colors")]
    [SerializeField, Min(.01f)] private float starRevealSeconds = .23f;
    [SerializeField] private Color earnedStar = new(1f, .83f, .24f);
    [SerializeField] private Color emptyStar = new(.25f, .36f, .43f);
    [SerializeField] private Color completedDay = new(.14f, .75f, .68f);
    [SerializeField] private Color incompleteDay = new(.19f, .28f, .34f);
    private PopGameplaySession session;
    private PopGameplayConfiguration config;
    private RectTransform home, hud, modal, lesson;
    private TMP_Text hudTitle, lessonText;
    private Rect lastSafe;
    private DrivenRectTransformTracker safeAreaTracker;
    private int screenWidth, screenHeight, journeyPage, noticedEffects;
    private float savedTimeScale, rewardNoticeUntil;
    private bool modalPaused, homeVisible, changingScene, dailyPage;
    private bool runtimeBound;
    private bool waitingForHomeIntro;
    private BlokkiPopIntroController homeIntro;
    private PopJourneyHomeView homePresentation;
    private string displayedDailyDate;
    private Action backAction;
    private readonly Dictionary<Button, UnityAction> listeners = new();
    private readonly List<Coroutine> starRoutines = new();
    private readonly Dictionary<RectTransform, Vector3> starBaseScales = new();
    public IReadOnlyList<PopUIView> Views => views;
    public PopUIPage ScenePreview { get => scenePreview; set => scenePreview = value; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }
    private void Awake()
    {
        if (!Application.isPlaying) return;
        BindRuntime();
    }
    private bool BindRuntime()
    {
        if (runtimeBound) return true;
        // ExecuteAlways can Awake while Unity restores the edit scene during Stop,
        // after the runtime session was already destroyed. That is not a new game.
        session = PopGameplaySession.Instance;
        if (session == null) return false;
        if (views == null || views.Length == 0 || safeRoot == null)
        { Debug.LogError("This is an unbound legacy UI controller. Use the authored Blokki Pop UI prefab in the scene.", this); enabled = false; return false; }
        if (Instance != null && Instance != this)
        { Debug.LogError("Duplicate authored PopGameplayUI in one scene.", this); enabled = false; return false; }
        Instance = this; config = session.Configuration; runtimeBound = true;
        homePresentation = View(PopUIPage.Home).GetComponent<PopJourneyHomeView>();
        foreach (PopUIView view in views) view.gameObject.SetActive(false);
        session.Changed += Refresh; ApplySafeArea();
        return true;
    }
    private void OnEnable()
    {
        screenWidth = screenHeight = 0;
        Canvas.willRenderCanvases += ApplySafeArea;
        ApplySafeArea();
        if (!Application.isPlaying) PreviewScenePage();
    }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= ApplySafeArea;
        safeAreaTracker.Clear();
    }
    private IEnumerator Start()
    {
        if (!Application.isPlaying) yield break;
        while (Application.isPlaying && !BindRuntime()) yield return null;
        if (!Application.isPlaying) yield break;
        yield return null;
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            homeIntro = FindFirstObjectByType<BlokkiPopIntroController>();
            if (homePresentation != null) { ShowHome(); homePresentation.SetNavigationReady(false); }
            waitingForHomeIntro = true; TryShowHomeAfterIntro();
        }
        else BuildHud();
    }
    public PopUIView View(PopUIPage page)
    {
        foreach (PopUIView view in views) if (view != null && view.page == page) return view;
        throw new InvalidOperationException("Missing authored " + page + " panel on " + name);
    }
    public void PreviewScenePage()
    {
        if (Application.isPlaying || views == null) return;
        foreach (PopUIView view in views) if (view != null) view.gameObject.SetActive(view.page == scenePreview || scenePreview == PopUIPage.Tutorial && view.page == PopUIPage.Hud);
    }
    private void Update()
    {
        ApplySafeArea();
        if (!Application.isPlaying || session == null) return;
        if (waitingForHomeIntro) TryShowHomeAfterIntro();
        if (rewardNoticeUntil > 0 && Time.unscaledTime >= rewardNoticeUntil) { rewardNoticeUntil = 0; Refresh(); }
        if (dailyPage && modal != null && displayedDailyDate != session.UtcToday) ShowDaily();
        if (changingScene || AdsPrivacyPanel.IsModalOpen || AdsService.Instance != null && AdsService.Instance.IsShowing) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { if (homePresentation != null && homePresentation.IsSettingsOpen) homePresentation.OpenSettings(false); else if (modal != null) backAction?.Invoke(); else if (session.IsActive) ShowPause(); }
    }
    private void Bind(PopUIView view, string key, string label, Action action, bool interactable = true)
    {
        PopUIElement item = view.Element(key); view.Text(key, label); Button button = item.button;
        if (button == null) throw new InvalidOperationException(view.name + ": " + key + " needs a Button.");
        if (listeners.TryGetValue(button, out UnityAction old)) button.onClick.RemoveListener(old);
        UnityAction listener = () => action?.Invoke(); listeners[button] = listener;
        button.onClick.AddListener(listener); button.interactable = interactable;
        // Do not change layout, sprite, font, colors or persistent designer UnityEvents.
    }
    public void ShowHome()
    {
        if (SceneManager.GetActiveScene().buildIndex != 0 || changingScene) return;
        waitingForHomeIntro = false;
        CloseModal(); homeVisible = true;
        FindFirstObjectByType<BlokkiPopIntroController>()?.ShowModeMenuInsteadOfPrompt();
        PopUIView view = View(PopUIPage.Home); home = (RectTransform)view.transform; home.gameObject.SetActive(true);
        view.Text("Title", homePresentation != null ? homePresentation.heading : config.text.homeTitle);
        view.Text("Stats", homePresentation != null ? "BEST · " + GameFlowManager.SavedHighScore.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) : config.text.best + ": " + GameFlowManager.SavedHighScore + " | " + session.TotalJourneyStars + " " + config.text.stars);
        homePresentation?.OpenSettings(false); homePresentation?.Refresh(session); homePresentation?.SetNavigationReady(true);
        PopRunSnapshot classic = session.SavedRun(PopGameMode.Classic); view.Visible("Continue", classic != null);
        Bind(view, "Continue", homePresentation != null ? homePresentation.continueClassic : config.text.continueRun + " · " + (classic?.score ?? 0), () => Navigate(() => session.PlayClassic(true)));
        Bind(view, "Classic", homePresentation != null ? homePresentation.classicLabel : classic != null ? config.text.newRun : config.text.classic, NewClassic);
        Bind(view, "Journey", homePresentation != null ? homePresentation.journeyLabel : config.text.journey, () => { if(homePresentation!=null)ShowLevel(homePresentation.SuggestedLevel);else ShowJourney(0); });
        view.Visible("Daily", config.daily.enabled); Bind(view, "Daily", homePresentation != null ? homePresentation.dailyLabel : config.text.daily, ShowDaily);
        Bind(view, "Rewards", homePresentation != null ? homePresentation.rewardsLabel : config.text.themes, ShowThemes);
        Bind(view, "Learn", homePresentation != null ? homePresentation.learnLabel : config.text.tutorial, () => Navigate(session.PlayTutorial));
        Bind(view, "Privacy", homePresentation != null ? homePresentation.privacyLabel : config.text.privacy, () => AdsPrivacyPanel.Instance?.OpenPrivacyMenu());
        if(homePresentation!=null)
        {
            Bind(view,"Levels",homePresentation.heading,()=>ShowJourney(homePresentation.SuggestedLevel/Mathf.Max(1,config.ui.levelsPerPage)));
            Bind(view,"Settings","",()=>homePresentation.OpenSettings(true)); Bind(view,"SettingsClose","×",()=>homePresentation.OpenSettings(false));
            Bind(view,"Sound","Âm thanh",homePresentation.ToggleSound); Bind(view,"Haptics","Rung",homePresentation.ToggleHaptics);
            for(int i=0;i<homePresentation.nodes.Length;i++)
            { PopJourneyHomeView.Node node=homePresentation.nodes[i]; Bind(view,"Progress"+(i+1).ToString("00"),"",()=>ShowLevel(node.levelIndex),node.button.interactable); }
        }
    }
    private void TryShowHomeAfterIntro()
    {
        if (waitForHomeIntroAnimation && homeIntro != null && !homeIntro.IsAnimationFinished) return;
        if(homePresentation!=null&&home!=null&&home.gameObject.activeSelf)
        { waitingForHomeIntro=false;homePresentation.SetNavigationReady(true);return; }
        ShowHome();
    }
    private void NewClassic()
    {
        if (session.SavedRun(PopGameMode.Classic) == null) { Navigate(() => session.PlayClassic(false)); return; }
        PopUIView view = OpenModal(PopUIPage.ReplaceRun, config.text.newRun, ShowHome);
        view.Text("Body", config.text.replaceRunWarning);
        Bind(view, "Play", config.text.newRun, () => Navigate(() => session.PlayClassic(false))); Bind(view, "Back", config.text.back, ShowHome);
    }
    private void ShowJourney(int page)
    {
        int count = Mathf.Max(1, config.ui.levelsPerPage), pages = Mathf.Max(1, Mathf.CeilToInt(config.journeyLevels.Count / (float)count));
        journeyPage = Mathf.Clamp(page, 0, pages - 1);
        PopUIView view = OpenModal(PopUIPage.Journey, config.text.journey, ShowHome);
        view.Text("Stats", Format(config.text.progressLabel, session.TotalJourneyStars, config.journeyLevels.Count * 3,
            config.journeyLevels.FindAll(level => session.Stars(level.id) > 0).Count, config.journeyLevels.Count));
        for (int i = 0; i < config.journeyLevels.Count; ++i)
        {
            int index = i; string key = "Level" + (i + 1).ToString("00"); PopLevelDefinition level = config.journeyLevels[i]; bool unlocked = session.LevelUnlocked(i);
            view.Visible(key, i / count == journeyPage);
            Bind(view, key, (i + 1) + ". " + level.title + "\n" + (unlocked ? StarString(session.Stars(level.id)) : config.text.locked), () => ShowLevel(index), unlocked);
        }
        Bind(view, "Previous", "<", () => ShowJourney(journeyPage - 1), journeyPage > 0);
        view.Text("Page", (journeyPage + 1) + " / " + pages);
        Bind(view, "Next", ">", () => ShowJourney(journeyPage + 1), journeyPage + 1 < pages); Bind(view, "Back", config.text.back, ShowHome);
    }
    private void ShowLevel(int index)
    {
        PopLevelDefinition level = config.journeyLevels[index];
        PopUIView view = OpenModal(PopUIPage.Level, Format(config.text.levelLabel, index + 1, level.title), () => ShowJourney(journeyPage));
        view.Text("Description", level.description); view.Text("Goals", GoalText(level, false));
        view.Text("Moves", config.text.moves + ": " + level.moveLimit + "\n" + Format(config.text.threeStarGuide, level.threeStarMoves, level.twoStarMoves));
        PopRunSnapshot saved = session.SavedRun(PopGameMode.Journey); view.Visible("Continue", saved != null && saved.levelId == level.id);
        Bind(view, "Continue", config.text.continueRun, () => Navigate(() => session.PlayJourney(index, true)));
        Bind(view, "Play", config.text.play, () => Navigate(() => session.PlayJourney(index))); Bind(view, "Back", config.text.back, () => ShowJourney(journeyPage));
    }
    private void ShowDaily()
    {
        string today = session.UtcToday; PopLevelDefinition daily = config.DailyFor(today); if (daily == null) return;
        PopUIView view = OpenModal(PopUIPage.Daily, config.text.daily, ShowHome); dailyPage = true; displayedDailyDate = today;
        view.Text("Date", today + " UTC\n" + daily.title); view.Text("Goals", GoalText(daily, false) + "\n" + config.text.moves + ": " + daily.moveLimit);
        view.Text("Status", session.DailyComplete(today) ? config.text.dailyCompleted : config.text.goalReminder);
        int days = Mathf.Clamp(config.daily.calendarDaysToShow, 7, 35);
        for (int i = 0; i < 35; ++i)
        {
            string key = "Day" + (i + 1).ToString("00"); view.Visible(key, i < days); if (i >= days) continue;
            DateTime day = DateTime.UtcNow.Date.AddDays(i - days + 1); bool won = session.DailyComplete(day.ToString("yyyy-MM-dd"));
            view.Text(key, day.Day + (won ? " *" : "")); view.Element(key).graphic.color = won ? completedDay : incompleteDay;
        }
        view.Text("Stats", config.text.dailyWins + ": " + session.DailyWins + " | " + config.text.dailyTrophy + ": " + session.DailyWins / Mathf.Max(1, config.daily.celebrationEveryWins));
        Bind(view, "Play", session.SavedRun(PopGameMode.Daily) != null ? config.text.continueRun : session.DailyComplete(today) ? config.text.retry : config.text.play,
            () => Navigate(() => session.PlayDaily(session.SavedRun(PopGameMode.Daily) != null)));
        view.Text("Notice", config.text.dailyNotice); Bind(view, "Back", config.text.back, ShowHome);
    }
    private void ShowThemes()
    {
        Action back = SceneManager.GetActiveScene().buildIndex == 0 ? ShowHome : ShowPause;
        PopUIView view = OpenModal(PopUIPage.Rewards, config.text.themes, back);
        for (int i = 0; i < config.themes.Count; ++i)
        {
            PopThemeDefinition theme = config.themes[i]; bool unlocked = session.ThemeUnlocked(theme); string key = "Theme" + (i + 1).ToString("00");
            view.Text(key + "Title", theme.title + (session.SelectedTheme.id == theme.id ? " · " + config.text.selected : unlocked ? "" : " · " + config.text.locked));
            view.Text(key + "Description", theme.description + "\n" + Format(config.text.themeRequirements, theme.requiredJourneyStars, theme.requiredDailyWins, theme.requiredClassicBest));
            Bind(view, key + "Select", unlocked ? config.text.play + " · " + theme.title : config.text.locked, () => { session.SelectTheme(theme.id); ShowThemes(); }, unlocked);
        }
        view.Text("EffectsHeading", config.text.popEffects);
        for (int i = 0; i < config.popEffects.Count; ++i)
        {
            PopEffectDefinition effect = config.popEffects[i]; bool unlocked = session.EffectUnlocked(effect); string key = "Effect" + (i + 1).ToString("00");
            view.Text(key + "Title", effect.title + (session.SelectedEffect.id == effect.id ? " · " + config.text.selected : ""));
            view.Text(key + "Description", effect.description + "\n" + Format(config.text.effectRequirements, effect.requiredSingleClearLines, effect.requiredBestCombo));
            Bind(view, key + "Select", unlocked ? config.text.play + " · " + effect.title : config.text.locked, () => { session.SelectEffect(effect.id); ShowThemes(); }, unlocked);
        }
        Bind(view, "Back", config.text.back, back);
    }
    private void BuildHud()
    {
        homeVisible = false; noticedEffects = 0; rewardNoticeUntil = 0;
        PopUIView view = View(PopUIPage.Hud); hud = (RectTransform)view.transform; hud.gameObject.SetActive(true);
        hudTitle = view.Element("Title").text; Bind(view, "Pause", config.text.pause, ShowPause); Refresh();
        if (session.RestoreWasRejected)
        {
            view = OpenModal(PopUIPage.InvalidSave, config.text.continueRun, () => CloseModal());
            view.Text("Body", config.text.invalidSaveNotice); Bind(view, "Close", config.text.close, () => CloseModal());
        }
    }
    private void BuildLesson()
    {
        PopUIView view = View(PopUIPage.Tutorial); lesson = (RectTransform)view.transform; lesson.gameObject.SetActive(true);
        lessonText = view.Element("Instruction").text; view.Visible("Skip", config.tutorial.allowSkip);
        Bind(view, "Skip", config.text.skipTutorial, () => Navigate(() => session.CompleteTutorial(true)));
    }
    public void ShowPause()
    {
        if (!session.IsActive || AdsPrivacyPanel.IsModalOpen || AdsService.Instance != null && AdsService.Instance.IsShowing) return;
        session.SaveBeforeNavigation(); PopUIView view = OpenModal(PopUIPage.Pause, config.text.pause, () => CloseModal());
        view.Text("Body", config.text.pauseNotice); Bind(view, "Resume", config.text.resume, () => CloseModal()); Bind(view, "Home", config.text.home, () => Navigate(session.GoHome));
        Bind(view, "Privacy", config.text.privacy, () => AdsPrivacyPanel.Instance?.OpenPrivacyMenu()); Bind(view, "Rewards", config.text.themes, ShowThemes);
    }
    public void ShowOutcome(bool won, int stars)
    {
        CloseModal(false); string title = session.Mode == PopGameMode.Tutorial && won ? config.text.tutorialComplete : won ? config.text.victory : config.text.failed;
        PopUIView view = OpenModal(won ? PopUIPage.Victory : PopUIPage.Failure, title, () => Navigate(session.GoHome));
        if (won) RevealStars(view, stars); else view.Text("Reason", session.Moves >= session.Definition.moveLimit ? config.text.outOfMoves : config.text.noMoves);
        view.Text("Body", GoalText(session.Definition, true) + "\n" + config.text.moves + ": " + session.Moves + "   " + config.text.score + ": " + GameFlowManager._instance.Score);
        if (won)
        {
            var unlocked = new List<string>(session.NewlyUnlockedThemes); unlocked.AddRange(session.NewlyUnlockedEffects);
            view.Text("Rewards", unlocked.Count > 0 ? Format(config.text.rewardUnlocked, string.Join(", ", unlocked)) : session.DailyTrophyEarned ? config.text.dailyTrophy : "");
            Bind(view, "Next", session.Mode == PopGameMode.Tutorial ? config.text.play : session.Mode == PopGameMode.Journey ? config.text.next : config.text.home, () => Navigate(() =>
            { if (session.Mode == PopGameMode.Tutorial) session.CompleteTutorial(); else if (session.Mode == PopGameMode.Journey) session.NextLevel(); else session.GoHome(); }));
        }
        Bind(view, "Retry", config.text.retry, () => Navigate(session.Retry)); Bind(view, "Home", config.text.home, () => Navigate(session.GoHome));
    }
    public void ShowTutorialHint()
    { if (lessonText != null) View(PopUIPage.Tutorial).Text("Instruction", config.text.lessonHint + "\n" + TutorialInstruction()); }
    private void Refresh()
    {
        if (session == null || changingScene) return;
        if (hud != null && session.IsInitialized && session.Mode == PopGameMode.Tutorial && lesson == null) BuildLesson();
        if (hudTitle != null)
        {
            if (session.NewlyUnlockedEffects.Count > noticedEffects) { noticedEffects = session.NewlyUnlockedEffects.Count; rewardNoticeUntil = Time.unscaledTime + config.ui.rewardNoticeSeconds; }
            PopUIView view = View(PopUIPage.Hud);
            view.Text("Title", session.Mode == PopGameMode.Classic ? config.text.classic : session.Mode == PopGameMode.Tutorial ? config.text.tutorial : session.Definition?.title ?? "");
            view.Text("Stats", session.Mode == PopGameMode.Classic ? config.text.lines + ": " + session.Lines + " · " + config.text.combo + ": " + session.MaxCombo : config.text.moves + ": " + Mathf.Max(0, (session.Definition?.moveLimit ?? 0) - session.Moves) + " · " + session.Moves + "/" + session.Definition?.moveLimit);
            view.Text("Goals", Time.unscaledTime < rewardNoticeUntil ? Format(config.text.rewardUnlocked, string.Join(", ", session.NewlyUnlockedEffects)) : session.Mode == PopGameMode.Classic ? config.text.classicDescription : GoalText(session.Definition, true));
        }
        if (lessonText != null && session.Mode == PopGameMode.Tutorial)
        {
            View(PopUIPage.Tutorial).Text("Instruction", TutorialInstruction());
            FindFirstObjectByType<Board>()?.ShowTutorialPlacement(session.IsActive && session.Moves < session.Definition.verifiedSolution.Count ? session.Definition.verifiedSolution[session.Moves] : null);
        }
    }
    private string TutorialInstruction() => config.tutorial.stepInstructions.Length == 0 ? config.text.lessonHint : config.tutorial.stepInstructions[Mathf.Clamp(session.Moves, 0, config.tutorial.stepInstructions.Length - 1)];
    private string GoalText(PopLevelDefinition level, bool progress)
    {
        if (level == null) return ""; var goals = new List<string>();
        void Add(string label, int target, int value) { if (target > 0) goals.Add(label + ": " + (progress ? Mathf.Min(value, target) + "/" : "") + target); }
        Add(config.text.lines, level.targetLines, session.Lines); Add(config.text.bubbles, level.targetBubbles, session.BubblesPopped);
        Add(config.text.score, level.targetScore, GameFlowManager._instance != null ? GameFlowManager._instance.Score : 0);
        Add(config.text.combo, level.targetCombo, session.MaxCombo); Add(config.text.multiClear, level.targetMultiClear, session.LargestClear); return string.Join(" · ", goals);
    }
    private PopUIView OpenModal(PopUIPage page, string title, Action back)
    {
        CloseModal(); savedTimeScale = Time.timeScale; modalPaused = true; Time.timeScale = 0; backAction = back;
        if (home != null) home.gameObject.SetActive(false);
        PopUIView view = View(page); modal = (RectTransform)view.transform; modal.gameObject.SetActive(true); view.Text("Title", title); return view;
    }
    private void CloseModal(bool restoreTime = true)
    {
        dailyPage = false; foreach (Coroutine routine in starRoutines) if (routine != null) StopCoroutine(routine); starRoutines.Clear();
        foreach (var pair in starBaseScales) if (pair.Key != null) pair.Key.localScale = pair.Value;
        if (modal != null) modal.gameObject.SetActive(false); modal = null; backAction = null;
        if (restoreTime && modalPaused) Time.timeScale = savedTimeScale;
        modalPaused = false; if (homeVisible && home != null) home.gameObject.SetActive(true);
    }
    private void Navigate(Action action)
    {
        if (changingScene || AdsPrivacyPanel.IsModalOpen || AdsService.Instance != null && AdsService.Instance.IsShowing) return;
        changingScene = true; CloseModal(false); if (home != null) home.gameObject.SetActive(false); action();
    }
    private void ApplySafeArea()
    {
        if (!isActiveAndEnabled || !fitDeviceSafeArea || safeRoot == null)
        { safeAreaTracker.Clear(); return; }
        if (Screen.width <= 0 || Screen.height <= 0) return;
        FitSafeAreaAnchors(Screen.safeArea, Screen.width, Screen.height);
    }
    private void FitSafeAreaAnchors(Rect safe, int width, int height)
    {
        if (width <= 0 || height <= 0 || safe.width <= 0 || safe.height <= 0) return;
        Vector2 min = new(safe.xMin / width, safe.yMin / height);
        Vector2 max = new(safe.xMax / width, safe.yMax / height);
        if (safe == lastSafe && screenWidth == width && screenHeight == height && safeRoot.anchorMin == min && safeRoot.anchorMax == max) return;
        lastSafe = safe; screenWidth = width; screenHeight = height;
        safeAreaTracker.Clear();
        safeAreaTracker.Add(this, safeRoot, DrivenTransformProperties.Anchors);
        // Setting anchors alone preserves authored anchoredPosition/sizeDelta.
        // Zeroing offsets here used to erase scene edits to this parent, moving
        // every child even though the selected HUD RectTransform was unchanged.
        safeRoot.anchorMin = min; safeRoot.anchorMax = max;
    }
    private string StarString(int stars) => Mathf.Clamp(stars, 0, 3) + "/3 " + config.text.stars;
    private static string Format(string template, params object[] values)
    { try { return string.Format(template, values); } catch (FormatException) { return template; } }
    private void RevealStars(PopUIView view, int earned)
    {
        for (int i = 0; i < 3; ++i)
        {
            PopUIElement item = view.Element("Star" + (i + 1)); RectTransform star = item.rect; item.graphic.color = i < earned ? earnedStar : emptyStar;
            if (!starBaseScales.ContainsKey(star)) starBaseScales.Add(star, star.localScale);
            starRoutines.Add(StartCoroutine(RevealStar(star, i, starBaseScales[star])));
        }
    }
    private IEnumerator RevealStar(RectTransform star, int index, Vector3 authoredScale)
    {
        star.localScale = Vector3.zero; yield return new WaitForSecondsRealtime(index * starRevealSeconds); float elapsed = 0;
        while (star != null && elapsed < starRevealSeconds)
        { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / starRevealSeconds); star.localScale = authoredScale * (1 - Mathf.Pow(1 - t, 3) + Mathf.Sin(t * Mathf.PI) * .12f); yield return null; }
        if (star != null) star.localScale = authoredScale;
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        if (session != null) session.Changed -= Refresh;
        foreach (var pair in listeners) if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
        if (modalPaused) Time.timeScale = savedTimeScale; Instance = null;
    }
}
