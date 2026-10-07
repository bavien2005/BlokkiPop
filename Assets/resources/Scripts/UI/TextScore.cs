using TMPro;
using UnityEngine;
[RequireComponent (typeof(TMPro.TextMeshProUGUI))]
public class TextScore : MonoBehaviour
{
    public static TextScore CurrentScoreDisplay { get; private set; }

    private TextMeshProUGUI textMeshProUGUI;

    [SerializeField] private bool isHighScore; 
    void Awake()
    {
        textMeshProUGUI = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (isHighScore)
        {
            OnEnable();
        }
    }

    public void ScoreText(int coin)
    {
        textMeshProUGUI.text = coin.ToString();
    }

    private void OnEnable()
    {
        if (!isHighScore)
            CurrentScoreDisplay = this;

        if(GameFlowManager._instance != null)
        {
            if (isHighScore)
            {
                GameFlowManager._instance._updateHighScore.AddListener(ScoreText);
                ScoreText(GameFlowManager._instance.HighScore);
            }
            else
            {
                GameFlowManager._instance._updateScore.AddListener(ScoreText);
                ScoreText(GameFlowManager._instance.Score);
            }
        }
    }

    private void OnDisable()
    {
        if (CurrentScoreDisplay == this)
            CurrentScoreDisplay = null;

        if (GameFlowManager._instance != null)
        {
            if (isHighScore)
            {
                GameFlowManager._instance._updateHighScore.RemoveListener(ScoreText);
            }
            else
            {
                GameFlowManager._instance._updateScore.RemoveListener(ScoreText);
            }
        }
    }
}
