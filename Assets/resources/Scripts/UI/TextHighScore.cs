using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMPro.TextMeshProUGUI))]
public class TextHighScore : MonoBehaviour
{
    private TextMeshProUGUI textMeshProUGUI;


    private void Awake()
    {
        textMeshProUGUI = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
       if(GameFlowManager._instance != null)
        {
            UpdateTextHighScore();
        }
    }
    private void UpdateTextHighScore()
    {
        int score = GameFlowManager._instance.HighScore;
        textMeshProUGUI.text = score.ToString();
    }
}
