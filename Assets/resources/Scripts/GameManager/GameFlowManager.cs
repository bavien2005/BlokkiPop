using System.Collections;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager _instance;

    private SettingUI settingUI;


    private bool clicked = false;

    [SerializeField] private Button button;

    [Tooltip("Mobile frame-rate target. 60 FPS gives smoother dragging and effects when the device can sustain it.")]
    [SerializeField, Range(30, 120)] private int mobileTargetFrameRate = 60;


    [SerializeField] private int score;

    public UnityEvent<int> _updateScore ;
    public UnityEvent<int> _updateHighScore;

    private int highScore ;

    private int linesCleared;

    private int maxCombo;

    private bool isGameOver;

    private Coroutine gameOverRoutine;

    public int HighScore => highScore;
    public int Score => score;
    public bool IsGameOver => isGameOver;
 
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
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }
    void Start()
    {
        if (_instance != this)
            return;

        settingUI = GetComponent<SettingUI>();
        _updateScore?.Invoke(score);
        
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

   
    public void RestartGame()
    {
        StartNewRun();
        SceneManager.LoadScene(1);
    }
    public void GamePlay()
    {
        StartNewRun();
        SceneManager.LoadScene(1);
    }

    public void ReturnHome()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        SceneManager.LoadScene(0);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void PauseGame()
    {
        Time.timeScale= 0.0f;
    }

    public void GameOver()
    {
        if (isGameOver)
            return;

        isGameOver = true;
        bool isNewBest = score > highScore;
        CheckHighScore();
        Time.timeScale = 0f;

        Board board = FindFirstObjectByType<Board>();
        float collapseDuration = board != null ? board.PlayGameOverSequence() : 0f;
        if (gameOverRoutine != null)
            StopCoroutine(gameOverRoutine);
        gameOverRoutine = StartCoroutine(RevealGameOverAfterBeat(collapseDuration, isNewBest));
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

    }

    //public void ClickButton()
    //{
    //    if (clicked)
    //    {
    //        clicked = false;
    //        button.image.color = Color.white;
    //    }
    //    else
    //    {
    //        clicked = true;
    //        button.image.color = Color.red;
    //    }
    //}

    public void AddCoin(int value)
    {
        this.score += value;
        _updateScore?.Invoke(score);
    }

    public void RegisterClear(int clearedLineCount, int comboCount)
    {
        linesCleared += Mathf.Max(0, clearedLineCount);
        maxCombo = Mathf.Max(maxCombo, comboCount);
    }

    private void StartNewRun()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        score = 0;
        linesCleared = 0;
        maxCombo = 0;
        _updateScore?.Invoke(score);
    }

    public void CheckHighScore()
    {
        if(highScore < score)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore" , highScore);
            PlayerPrefs.Save();
            _updateHighScore?.Invoke(highScore);
        }
    }
    
}
