using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays the Blokki Pop boot animation while the gameplay scene loads in the background.
/// All visuals and tuning values remain editable from the Home scene and Inspector.
/// </summary>
public sealed class BlokkiPopIntroController : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Gameplay scene name. It must be enabled in Build Settings after Home.")]
    [SerializeField] private string gameplaySceneName = "Game";

    [Tooltip("Keep the intro visible for at least this long, even when loading is very fast.")]
    [SerializeField, Min(0f)] private float minimumIntroSeconds = 1.15f;

    [Tooltip("Duration of the visual 0-to-near-full progress animation. No percentage text is shown.")]
    [SerializeField, Min(0.5f)] private float progressAnimationDuration = 2.8f;

    [Tooltip("The bar pauses at this fraction if gameplay is still loading, then finishes once the scene is ready.")]
    [SerializeField, Range(0.5f, 0.99f)] private float progressHoldPoint = 0.92f;

    [Tooltip("Time used to animate the final part of the bar to 100% after gameplay is ready.")]
    [SerializeField, Min(0.05f)] private float progressCompletionDuration = 0.25f;

    [Header("Intro Animation")]
    [Tooltip("Canvas group for the title, tagline, and mini board. Fades in at launch.")]
    [SerializeField] private CanvasGroup brandGroup;

    [Tooltip("Scales the Blokki Pop logo in with a soft overshoot.")]
    [SerializeField] private RectTransform logoRoot;

    [Tooltip("Mini-board cells animated into place in sequence.")]
    [SerializeField] private RectTransform[] animatedCells;

    [Tooltip("Canvas groups paired with Animated Cells to fade each block in as it lands.")]
    [SerializeField] private CanvasGroup[] animatedCellGroups;

    [Tooltip("Duration of the logo's entrance animation.")]
    [SerializeField, Min(0.05f)] private float logoRevealDuration = 0.42f;

    [Tooltip("Pause between each mini-board cell entrance.")]
    [SerializeField, Min(0f)] private float cellStaggerSeconds = 0.09f;

    [Tooltip("Duration of each mini-board cell landing animation.")]
    [SerializeField, Min(0.05f)] private float cellLandingDuration = 0.34f;
    [Tooltip("Use overlapping landings for a short Home entrance instead of six sequential waits.")]
    [SerializeField] private bool overlapCellLandings;
    [SerializeField, Min(0)] private float cellDropDistance = 250f;
    [SerializeField, Min(0)] private float cellDropVariation = 55f;
    [Header("Authored Journey Home")]
    [SerializeField] private bool authoredHomeMenu;
    [Tooltip("Disable when logo/font sizes are owned directly by the authored Home TMP components.")]
    [SerializeField] private bool applyInspectorFontSizes = true;

    [Header("Loading UI / Tap to Start")]
    [Tooltip("Filled image used as the visual loading bar. It animates without printing a numeric percentage.")]
    [SerializeField] private Image progressFill;

    [Tooltip("Status text changes to TAP TO START only after the scene and intro are fully ready.")]
    [SerializeField] private TextMeshProUGUI loadingLabel;

    [Tooltip("Button on the status text. It is enabled only after the bar reaches 100% and loading is complete.")]
    [SerializeField] private Button startButton;

    [Tooltip("Font size applied to the final TAP TO START prompt.")]
    [SerializeField, Range(20f, 120f)] private float readyPromptFontSize = 42f;

    [Tooltip("Speed of the subtle ready-prompt breathing animation.")]
    [SerializeField, Min(0.1f)] private float promptPulseSpeed = 2.6f;

    [Tooltip("How much the ready prompt scales while breathing.")]
    [SerializeField, Range(0f, 0.15f)] private float promptPulseAmount = 0.035f;

    [Tooltip("Subtle logo pulse while a slower device is still loading the gameplay scene.")]
    [SerializeField, Range(0f, 0.12f)] private float waitingPulse = 0.025f;

    [Header("Logo Text Sizes")]
    [Tooltip("Main BLOKKI logo text. The size is applied on startup for quick tuning here.")]
    [SerializeField] private TextMeshProUGUI mainLogoText;

    [SerializeField, Range(24f, 240f), Tooltip("Font size for BLOKKI.")]
    private float mainLogoFontSize = 164f;

    [Tooltip("POP logo text. The size is applied on startup for quick tuning here.")]
    [SerializeField] private TextMeshProUGUI popLogoText;

    [SerializeField, Range(24f, 240f), Tooltip("Font size for POP.")]
    private float popLogoFontSize = 188f;

    [Tooltip("Small slogan below the logo. The size is applied on startup for quick tuning here.")]
    [SerializeField] private TextMeshProUGUI taglineText;

    [SerializeField, Range(12f, 80f), Tooltip("Font size for the slogan.")]
    private float taglineFontSize = 42f;

    [Header("Block Pop Sound")]
    [Tooltip("2D AudioSource used for the short synthesized pop played as each mini-board block lands.")]
    [SerializeField] private AudioSource popAudioSource;

    [Tooltip("Volume of each block pop. This intro sound is separate from gameplay/music sliders.")]
    [SerializeField, Range(0f, 1f)] private float popVolume = 0.22f;

    [Tooltip("Base pitch of the pop sound. Each cell gets a small randomized variation.")]
    [SerializeField, Range(0.5f, 2f)] private float popPitch = 1.05f;

    [Tooltip("Maximum random pitch offset for successive pops.")]
    [SerializeField, Range(0f, 0.4f)] private float popPitchVariation = 0.08f;

    [Tooltip("Length in seconds of the synthesized pop sound.")]
    [SerializeField, Range(0.04f, 0.5f)] private float popDuration = 0.12f;

    [Tooltip("Starting frequency of the soft descending pop, in hertz.")]
    [SerializeField, Range(100f, 1400f)] private float popStartFrequency = 680f;

    [Tooltip("Ending frequency of the pop, in hertz.")]
    [SerializeField, Range(60f, 800f)] private float popEndFrequency = 240f;

    [Header("Intro Music")]
    [Tooltip("Looping intro music clip. The Home scene builder assigns GameLoop-04-42s.wav here.")]
    [SerializeField] private AudioClip introMusicClip;

    [Tooltip("Dedicated looping source. It fades out and stops when the player starts the game.")]
    [SerializeField] private AudioSource introMusicSource;

    [Tooltip("Intro music loudness, independent of the block pop sound.")]
    [SerializeField, Range(0f, 1f)] private float introMusicVolume = 0.55f;

    [Tooltip("How long the intro music takes to fade out after the player taps to start.")]
    [SerializeField, Min(0f)] private float introMusicFadeOutSeconds = 0.25f;

    [Header("Scene Transition")]
    [Tooltip("Persistent screen overlay that fades in before Game activates, then fades away over the gameplay scene.")]
    [SerializeField] private BlokkiPopSceneTransition sceneTransition;

    private const int PopSampleRate = 22050;
    private AsyncOperation loadOperation;
    private Vector2[] cellTargets;
    private Vector3[] cellBaseScales;
    private static bool playedAuthoredIntro;
    private Vector3 logoBaseScale;
    private Vector3 promptBaseScale;
    private AudioClip popClip;
    private float startedAt;
    private float progressElapsed;
    private float displayedProgress;
    private bool introAnimationFinished;
    private bool gameplayReady;
    private bool sceneActivationRequested;
    private bool modeMenuReplacesPrompt;
    public bool IsAnimationFinished => introAnimationFinished;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetIntro() { playedAuthoredIntro=false; }

    private void Awake()
    {
        startedAt = Time.unscaledTime;
        CacheAnimationTargets();
        if(applyInspectorFontSizes)ApplyInspectorFontSizes();
        PrepareStartButton();
        CreatePopClip();
    }

    private void Start()
    {
        PlayIntroMusic();

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            ShowLoadError($"Scene '{gameplaySceneName}' is missing or disabled in Build Settings.");
            return;
        }

        loadOperation = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            ShowLoadError($"Could not start loading scene '{gameplaySceneName}'.");
            return;
        }

        // Keep Home visible until gameplay is ready AND the player explicitly taps the prompt.
        loadOperation.allowSceneActivation = false;
        if (loadingLabel != null)
            loadingLabel.text = "GETTING YOUR BOARD READY...";

        if(authoredHomeMenu&&playedAuthoredIntro)CompleteIntroImmediately();
        else { if(authoredHomeMenu)playedAuthoredIntro=true;StartCoroutine(PlayIntro()); }
    }

    private void Update()
    {
        if (loadOperation == null || sceneActivationRequested)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        progressElapsed += deltaTime;
        float timedProgress = Mathf.Clamp01(progressElapsed / progressAnimationDuration) * progressHoldPoint;
        displayedProgress = Mathf.Max(displayedProgress, timedProgress);

        bool loadFinished = loadOperation.progress >= 0.9f;
        if (loadFinished && progressElapsed >= progressAnimationDuration)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f,
                deltaTime / progressCompletionDuration);
        }

        if (progressFill != null)
            progressFill.fillAmount = displayedProgress;

        if (!gameplayReady && loadFinished && progressElapsed >= progressAnimationDuration &&
            displayedProgress >= 0.999f && introAnimationFinished &&
            Time.unscaledTime - startedAt >= minimumIntroSeconds)
        {
            ShowTapToStart();
        }

        if (!gameplayReady)
        {
            AnimateWaitingPulse(loadFinished);
            return;
        }

        if (modeMenuReplacesPrompt) return;
        AnimateReadyPrompt();
        if (PopGameplaySession.Instance != null) return;
        // Allow tapping anywhere on the ready screen as a fallback to the UI Button. This also
        // keeps the prompt responsive if a device's EventSystem/raycast configuration differs.
        if (TryGetStartPressPosition(out Vector2 pressPosition) &&
            !AdsPrivacyPanel.ShouldBlockStartInput(pressPosition))
        {
            BeginGame();
        }
    }

    /// <summary>Called by the visible TAP TO START button after loading has completed.</summary>
    public void BeginGame()
    {
        if (PopGameplaySession.Instance != null) { PopGameplayUI.Instance?.ShowHome(); return; }
        if (AdsPrivacyPanel.IsModalOpen || (AdsService.Instance != null && AdsService.Instance.IsShowing) ||
            !gameplayReady || loadOperation == null || sceneActivationRequested)
            return;

        sceneActivationRequested = true;
        if (startButton != null)
            startButton.interactable = false;

        if (loadingLabel != null)
            loadingLabel.rectTransform.localScale = promptBaseScale * 1.12f;

        StartCoroutine(FadeMusicAndLeaveIntro());
    }

    public void ActivatePreparedGame()
    {
        if (sceneActivationRequested || loadOperation == null || AdsPrivacyPanel.IsModalOpen) return;
        GameFlowManager._instance?.PreparePreloadedGame();
        sceneActivationRequested = true;
        if (startButton != null) startButton.interactable = false;
        StartCoroutine(WaitForPreparedGame());
    }

    private IEnumerator WaitForPreparedGame()
    {
        while (loadOperation != null && loadOperation.progress < 0.9f) yield return null;
        yield return FadeMusicAndLeaveIntro();
    }

    public void ShowModeMenuInsteadOfPrompt()
    {
        modeMenuReplacesPrompt = true;
        if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);
        if (progressFill != null && progressFill.transform.parent != null) progressFill.transform.parent.gameObject.SetActive(false);
        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.gameObject.SetActive(false);
        }
        // The mode picker owns only navigation/loading presentation, not the intro art.
        // Keep the authored tagline and falling cells active so PlayIntro can animate
        // and land them normally, even when the mode menu opens before it finishes.
    }

    private void PlayIntroMusic()
    {
        if (introMusicSource == null || introMusicClip == null)
            return;

        introMusicSource.playOnAwake = false;
        introMusicSource.loop = true;
        introMusicSource.spatialBlend = 0f;
        introMusicSource.clip = introMusicClip;
        introMusicSource.volume = introMusicVolume;
        introMusicSource.Play();
    }

    private IEnumerator FadeMusicAndLeaveIntro()
    {
        if (introMusicSource != null && introMusicSource.isPlaying && introMusicFadeOutSeconds > 0f)
        {
            float startingVolume = introMusicSource.volume;
            float elapsed = 0f;
            while (elapsed < introMusicFadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                introMusicSource.volume = Mathf.Lerp(startingVolume, 0f,
                    Mathf.Clamp01(elapsed / introMusicFadeOutSeconds));
                yield return null;
            }
        }

        if (introMusicSource != null)
            introMusicSource.Stop();

        if (sceneTransition != null)
            sceneTransition.Play(loadOperation);
        else if (loadOperation != null)
            loadOperation.allowSceneActivation = true;
    }

    private void CacheAnimationTargets()
    {
        if (logoRoot != null)
            logoBaseScale = logoRoot.localScale;

        int count = animatedCells != null ? animatedCells.Length : 0;
        cellTargets = new Vector2[count];
        cellBaseScales = new Vector3[count];
        for (int i = 0; i < count; ++i)
        {
            RectTransform cell = animatedCells[i];
            if (cell == null)
                continue;

            cellTargets[i] = cell.anchoredPosition;
            cellBaseScales[i] = cell.localScale;
            cell.anchoredPosition += Vector2.up * (cellDropDistance + (i % 3) * cellDropVariation);
            cell.localScale = cellBaseScales[i] * 0.55f;
            if (animatedCellGroups != null && i < animatedCellGroups.Length && animatedCellGroups[i] != null)
                animatedCellGroups[i].alpha = 0f;
        }

        if (brandGroup != null)
            brandGroup.alpha = 0f;
        if (logoRoot != null)
            logoRoot.localScale = logoBaseScale * 0.82f;
        if (progressFill != null)
            progressFill.fillAmount = 0f;
        if (loadingLabel != null)
        {
            promptBaseScale = loadingLabel.rectTransform.localScale;
        }
    }

    private void ApplyInspectorFontSizes()
    {
        if (mainLogoText != null)
        {
            mainLogoText.enableAutoSizing = false;
            mainLogoText.fontSize = mainLogoFontSize;
        }
        if (popLogoText != null)
        {
            popLogoText.enableAutoSizing = false;
            popLogoText.fontSize = popLogoFontSize;
        }
        if (taglineText != null)
        {
            taglineText.enableAutoSizing = false;
            taglineText.fontSize = taglineFontSize;
        }
    }

    private void PrepareStartButton()
    {
        if (startButton == null)
            return;

        startButton.interactable = false;
        startButton.transition = Selectable.Transition.None;
        startButton.onClick.RemoveListener(BeginGame);
        startButton.onClick.AddListener(BeginGame);
    }

    private IEnumerator PlayIntro()
    {
        float elapsed = 0f;
        while (elapsed < logoRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / logoRevealDuration);
            float eased = EaseOutBack(t);
            if (brandGroup != null)
                brandGroup.alpha = t;
            if (logoRoot != null)
                logoRoot.localScale = Vector3.LerpUnclamped(logoBaseScale * 0.82f, logoBaseScale, eased);
            yield return null;
        }

        if (brandGroup != null)
            brandGroup.alpha = 1f;
        if (logoRoot != null)
            logoRoot.localScale = logoBaseScale;

        if (animatedCells != null)
        {
            for (int i = 0; i < animatedCells.Length; ++i)
            {
                RectTransform cell = animatedCells[i];
                if (cell == null)
                    continue;

                if (cellStaggerSeconds > 0f)
                    yield return new WaitForSecondsRealtime(cellStaggerSeconds);
                if(overlapCellLandings)StartCoroutine(AnimateCellLanding(i,cell));
                else yield return AnimateCellLanding(i, cell);
            }
            if(overlapCellLandings)yield return new WaitForSecondsRealtime(cellLandingDuration);
        }

        introAnimationFinished = true;
    }

    private IEnumerator AnimateCellLanding(int index, RectTransform cell)
    {
        Vector2 start = cell.anchoredPosition;
        Vector2 target = cellTargets[index];
        float elapsed = 0f;

        while (elapsed < cellLandingDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / cellLandingDuration);
            float eased = EaseOutBack(t);
            cell.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
            cell.localScale = cellBaseScales[index] * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t * 1.15f));
            if (animatedCellGroups != null && index < animatedCellGroups.Length && animatedCellGroups[index] != null)
                animatedCellGroups[index].alpha = t;
            yield return null;
        }

        cell.anchoredPosition = target;
        cell.localScale = cellBaseScales[index];
        if (animatedCellGroups != null && index < animatedCellGroups.Length && animatedCellGroups[index] != null)
            animatedCellGroups[index].alpha = 1f;
        PlayBlockPop();
    }

    private void ShowTapToStart()
    {
        gameplayReady = true;
        if (modeMenuReplacesPrompt || authoredHomeMenu) return;
        if (loadingLabel != null)
        {
            loadingLabel.text = "TAP TO START";
            loadingLabel.enableAutoSizing = false;
            loadingLabel.fontSize = readyPromptFontSize;
            loadingLabel.rectTransform.localScale = promptBaseScale;
        }

        if (startButton != null)
            startButton.interactable = true;
    }

    private void AnimateReadyPrompt()
    {
        if (loadingLabel == null)
            return;

        float wave = (Mathf.Sin(Time.unscaledTime * promptPulseSpeed) + 1f) * 0.5f;
        loadingLabel.rectTransform.localScale = promptBaseScale * (1f + wave * promptPulseAmount);
        Color color = loadingLabel.color;
        color.a = Mathf.Lerp(0.76f, 1f, wave);
        loadingLabel.color = color;
    }
    private void CompleteIntroImmediately()
    {
        if(brandGroup!=null)brandGroup.alpha=1;
        if(logoRoot!=null)logoRoot.localScale=logoBaseScale;
        if(animatedCells!=null)for(int i=0;i<animatedCells.Length;i++)
        { if(animatedCells[i]==null)continue;animatedCells[i].anchoredPosition=cellTargets[i];animatedCells[i].localScale=cellBaseScales[i];if(animatedCellGroups!=null&&i<animatedCellGroups.Length&&animatedCellGroups[i]!=null)animatedCellGroups[i].alpha=1; }
        introAnimationFinished=true;
    }

    private static bool TryGetStartPressPosition(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private void AnimateWaitingPulse(bool loadingFinished)
    {
        if (logoRoot == null || loadingFinished || !introAnimationFinished)
            return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.4f) * waitingPulse;
        logoRoot.localScale = logoBaseScale * pulse;
    }

    private void CreatePopClip()
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(popDuration * PopSampleRate));
        popClip = AudioClip.Create("Blokki Pop Intro Pop", sampleCount, 1, PopSampleRate, false);
        float[] samples = new float[sampleCount];
        float phase = 0f;
        float startFrequency = Mathf.Max(1f, popStartFrequency);
        float endFrequency = Mathf.Max(1f, popEndFrequency);

        for (int i = 0; i < sampleCount; ++i)
        {
            float t = (float)i / sampleCount;
            float frequency = startFrequency * Mathf.Pow(endFrequency / startFrequency, t);
            phase += 2f * Mathf.PI * frequency / PopSampleRate;
            float attack = Mathf.Clamp01(t * 45f);
            float decay = Mathf.Pow(1f - t, 3.2f);
            float tone = Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2f);
            samples[i] = tone * attack * decay * 0.62f;
        }

        popClip.SetData(samples, 0);
        if (popAudioSource != null)
        {
            popAudioSource.playOnAwake = false;
            popAudioSource.loop = false;
            popAudioSource.spatialBlend = 0f;
        }
    }

    private void PlayBlockPop()
    {
        if (popAudioSource == null || popClip == null || popVolume <= 0f)
            return;

        popAudioSource.pitch = popPitch + Random.Range(-popPitchVariation, popPitchVariation);
        popAudioSource.PlayOneShot(popClip, popVolume);
    }

    private void ShowLoadError(string message)
    {
        Debug.LogError($"Blokki Pop intro: {message}", this);
        if (introMusicSource != null)
            introMusicSource.Stop();
        if (loadingLabel != null)
            loadingLabel.text = "OOPS! CHECK THE GAME SCENE IN BUILD SETTINGS.";
        if (startButton != null)
            startButton.interactable = false;
    }

    private void OnDestroy()
    {
        if (popClip != null)
            Destroy(popClip);
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.35f;
        float shifted = t - 1f;
        return 1f + (1f + overshoot) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }
}
