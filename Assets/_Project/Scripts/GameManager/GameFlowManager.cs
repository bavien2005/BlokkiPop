using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    private const string HighScorePreferenceKey = "HighScore";

    public static GameFlowManager _instance;
    public static event System.Action<GameFlowManager> InstanceReady;

    public static int SavedHighScore => Mathf.Max(0, PlayerPrefs.GetInt(HighScorePreferenceKey, 0));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        InstanceReady = null;
        Time.timeScale = 1f;
    }

    [Tooltip("Mobile frame-rate target. 60 FPS gives smoother dragging and effects when the device can sustain it.")]
    [SerializeField, Range(30, 120)] private int mobileTargetFrameRate = 60;


    [SerializeField] private int score;

    public UnityEvent<int> _updateScore = new();
    public UnityEvent<int> _updateHighScore = new();

    private int highScore ;

    private int linesCleared;

    private int maxCombo;

    private bool isGameOver;

    private Coroutine gameOverRoutine;

    private int highScoreAtRunStart;
    private bool highScoreNeedsSave;
    private int runVersion;
    private bool reviveUsed;
    private bool awaitingRevive;
    private bool reviveRequestPending;
    private Blocks reviveBlocks;
    private ReviveOfferPanel revivePanel;

    public int HighScore => highScore;
    public int Score => score;
    public bool IsGameOver => isGameOver;
    public bool HasUsedRevive => reviveUsed;
    public bool IsAwaitingRevive => awaitingRevive;
 
    private void Awake()
    {
        if (Application.isMobilePlatform)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = mobileTargetFrameRate;
        }

        if(_instance != null && _instance != this)
        {
            // Keep the persistent manager. Remove the copy introduced by the
            // newly loaded scene instead of destroying only its component.
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        _updateScore ??= new UnityEvent<int>();
        _updateHighScore ??= new UnityEvent<int>();
        highScore = SavedHighScore;
        highScoreAtRunStart = highScore;
        highScoreNeedsSave = false;
        score = 0;
        linesCleared = 0;
        maxCombo = 0;
        isGameOver = false;
        gameOverRoutine = null;
        runVersion = 0;
        reviveUsed = false;
        awaitingRevive = false;
        reviveRequestPending = false;
        InstanceReady?.Invoke(this);
    }
    void Start()
    {
        if (_instance != this)
            return;

        _updateScore?.Invoke(score);
        _updateHighScore?.Invoke(highScore);
        
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_instance == this)
        {
            CancelReviveOffer();
            CheckHighScore();
            _instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && _instance == this)
            CheckHighScore();
    }

    private void OnApplicationQuit()
    {
        if (_instance == this)
            CheckHighScore();
    }

   
    public void RestartGame()
    {
        PopGameplaySession.Instance?.PrepareRestart();
        StartNewRun();
        SceneManager.LoadScene(1);
    }
    public void GamePlay()
    {
        PopGameplaySession.Instance?.PrepareLegacyPlay();
        StartNewRun();
        SceneManager.LoadScene(1);
    }

    public void ReturnHome()
    {
        PopGameplaySession.Instance?.SaveBeforeNavigation();
        StartNewRun();
        SceneManager.LoadScene(0);
    }

    public void QuitGame()
    {
        CheckHighScore();
        Application.Quit();
    }

    public void PauseGame()
    {
        CheckHighScore();
        Time.timeScale= 0.0f;
    }

    public void GameOver()
    {
        if (isGameOver)
            return;

        isGameOver = true;
        CheckHighScore();
        Time.timeScale = 0f;

        AdsService ads = AdsService.Instance;
        reviveBlocks = FindFirstObjectByType<Blocks>();
        if ((PopGameplaySession.Instance == null || PopGameplaySession.Instance.AllowsRevive) &&
            !reviveUsed && reviveBlocks != null && reviveBlocks.CanReviveBoard() &&
            ads != null && (ads.IsRewardedReady || ads.CanSimulateDevelopmentReward))
        {
            awaitingRevive = true;
            revivePanel = ReviveOfferPanel.Create(this);
            return;
        }

        FinalizeGameOver();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        // Normal navigation already calls StartNewRun. This fallback handles
        // an external/direct scene load while an offer/ad still owns the old board.
        if (_instance == this && awaitingRevive)
            StartNewRun();
    }

    private void FinalizeGameOver()
    {
        awaitingRevive = false;
        reviveRequestPending = false;
        if (revivePanel != null)
            Destroy(revivePanel.gameObject);
        revivePanel = null;
        reviveBlocks = null;
        PopGameplaySession session = PopGameplaySession.Instance;
        session?.FinishFailure();
        if (session != null && session.IsAuthored) return;
        bool isNewBest = score > highScoreAtRunStart;

        Board board = FindFirstObjectByType<Board>();
        float collapseDuration = board != null ? board.PlayGameOverSequence() : 0f;
        if (gameOverRoutine != null)
            StopCoroutine(gameOverRoutine);
        gameOverRoutine = StartCoroutine(RevealGameOverAfterBeat(collapseDuration, isNewBest));
    }

    /// <summary>Opt-in only: loading or closing an ad is never itself a reward.</summary>
    public void RequestRevive(bool developmentSimulation = false)
    {
        if (!awaitingRevive || reviveRequestPending || reviveUsed)
            return;
        AdsService ads = AdsService.Instance;
        if (ads == null)
        {
            revivePanel?.ShowFailure("Ad unavailable. You can finish this run.");
            return;
        }
        reviveRequestPending = true;
        revivePanel?.SetBusy(true);
        int requestedRun = runVersion;
        int requestedScene = SceneManager.GetActiveScene().handle;
        System.Action<RewardedAdResult> completed = result =>
        {
            // Persistent manager can outlive the board. Never reward a new run.
            if (this == null || requestedRun != runVersion ||
                requestedScene != SceneManager.GetActiveScene().handle || !awaitingRevive)
                return;
            reviveRequestPending = false;
            bool earned = result == RewardedAdResult.Earned;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            earned |= developmentSimulation && result == RewardedAdResult.DevelopmentRewardEarned;
#endif
            if (earned && !reviveUsed && reviveBlocks != null && reviveBlocks.TryReviveBoard())
            {
                reviveUsed = true;
                awaitingRevive = false;
                isGameOver = false;
                Time.timeScale = 1f;
                if (revivePanel != null)
                    Destroy(revivePanel.gameObject);
                revivePanel = null;
                reviveBlocks = null;
                PopGameplaySession.Instance?.OnRevived();
                return;
            }
            revivePanel?.SetBusy(false);
            revivePanel?.ShowFailure(earned
                ? "Board changed. Revive cannot be applied. Finish this run."
                : "No reward earned. Retry if an ad is ready, or finish this run.");
        };
        if (developmentSimulation)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ads.ShowDevelopmentReward(completed, this);
#else
            completed(RewardedAdResult.Unavailable);
#endif
        }
        else
            ads.ShowRewarded(completed, this);
    }

    public void DeclineRevive()
    {
        if (awaitingRevive && !reviveRequestPending)
            FinalizeGameOver();
    }

    private void CancelReviveOffer()
    {
        ++runVersion;
        awaitingRevive = false;
        reviveRequestPending = false;
        AdsService.Instance?.CancelRewardRequest(this);
        if (revivePanel != null)
            Destroy(revivePanel.gameObject);
        revivePanel = null;
        reviveBlocks = null;
    }

    private IEnumerator RevealGameOverAfterBeat(float collapseDuration, bool isNewBest)
    {
        yield return new WaitForSecondsRealtime(collapseDuration + 0.22f);
        AudioManager.Instance?.PlayGameOverCue();
        GameOverPanel.Instance?.Show(score, highScore, maxCombo, linesCleared, isNewBest);
        gameOverRoutine = null;
    }

    public void GameWin()
    {
        if (isGameOver) return;
        isGameOver = true;
        CancelReviveOffer();
        Time.timeScale = 0f;
        PopGameplaySession.Instance?.FinishWin();
    }

    public void PreparePreloadedGame()
    {
        PopGameplaySession.Instance?.PrepareLegacyPlay();
        StartNewRun();
    }

    public void RestoreRun(int savedScore, int savedLines, int savedCombo, bool usedRevive)
    {
        score = Mathf.Max(0, savedScore); linesCleared = Mathf.Max(0, savedLines);
        maxCombo = Mathf.Max(0, savedCombo); reviveUsed = usedRevive;
        isGameOver = awaitingRevive = reviveRequestPending = false;
        Time.timeScale = 1f;
        UpdateHighScore();
        _updateScore?.Invoke(score); _updateHighScore?.Invoke(highScore);
    }

    public void AddCoin(int value)
    {
        this.score += value;
        UpdateHighScore();
        _updateScore?.Invoke(score);
    }

    public void RegisterClear(int clearedLineCount, int comboCount)
    {
        linesCleared += Mathf.Max(0, clearedLineCount);
        maxCombo = Mathf.Max(maxCombo, comboCount);
    }

    private void StartNewRun()
    {
        // Restart/scene changes must commit the old run before clearing its score.
        CheckHighScore();
        CancelReviveOffer();
        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
            gameOverRoutine = null;
        }
        Time.timeScale = 1f;
        isGameOver = false;
        reviveUsed = false;
        score = 0;
        linesCleared = 0;
        maxCombo = 0;
        highScoreAtRunStart = highScore;
        _updateScore?.Invoke(score);
        _updateHighScore?.Invoke(highScore);
    }

    public void CheckHighScore()
    {
        UpdateHighScore();
        if (!highScoreNeedsSave)
            return;

        PlayerPrefs.Save();
        highScoreNeedsSave = false;
    }

    private void UpdateHighScore()
    {
        if (PopGameplaySession.Instance != null && !PopGameplaySession.Instance.AllowsClassicScore) return;
        if (score <= highScore)
            return;

        highScore = score;
        // Update the HUD/preferences immediately, but flush to disk at run/lifecycle
        // boundaries instead of doing synchronous disk I/O on every placed block.
        PlayerPrefs.SetInt(HighScorePreferenceKey, highScore);
        highScoreNeedsSave = true;
        _updateHighScore?.Invoke(highScore);
    }
    
}
