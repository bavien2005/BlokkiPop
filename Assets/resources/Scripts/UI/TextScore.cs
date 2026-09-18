using TMPro;
using UnityEngine;
[RequireComponent (typeof(TMPro.TextMeshProUGUI))]
public class TextScore : MonoBehaviour
{

    private TextMeshProUGUI textMeshProUGUI;
    void Awake()
    {
        textMeshProUGUI = GetComponent<TextMeshProUGUI>();
    }

    public void ScoreText(int coin)
    {
        textMeshProUGUI.text = coin.ToString();
    }

    private void OnEnable()
    {
        if(GameFlowManager._instance != null)
        {
            GameFlowManager._instance._updateScore.AddListener(ScoreText);
        }
    }

    private void OnDisable()
    {
        if (GameFlowManager._instance != null)
        {
            GameFlowManager._instance._updateScore.RemoveListener(ScoreText);
        }
    }
}
