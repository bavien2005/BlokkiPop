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


    [SerializeField] private int score;

    public UnityEvent<int> _updateScore ;

    private int highScore ;

    public int HighScore => highScore;
    public int Score => score;
 
    private void Awake()
    {
        if(_instance == null)
        {
            _instance = this;
            highScore = PlayerPrefs.GetInt("HighScore", 0);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);    
        }
    }
    void Start()
    {
        settingUI = GetComponent<SettingUI>();
        _updateScore?.Invoke(score);
        
    }

   
    void Update()
    {
        
    }


    public void RestartGame()
    {
        CheckHighScore();
        this.score = 0;
        SceneManager.LoadScene(1);
    }
    public void GamePlay()
    {
        SceneManager.LoadScene(1);
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
        CheckHighScore();
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

    public void CheckHighScore()
    {
        if(highScore < score)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore" , highScore);
            PlayerPrefs.Save();
        }
    }
    
}
