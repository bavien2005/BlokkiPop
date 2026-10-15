using TMPro;
using UnityEngine;
[RequireComponent (typeof(TMPro.TextMeshProUGUI))]
public class TextScore : MonoBehaviour
{
    public static TextScore CurrentScoreDisplay { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        CurrentScoreDisplay = null;
    }

    private TextMeshProUGUI textMeshProUGUI;
    private GameFlowManager subscribedManager;

    [SerializeField] private bool isHighScore; 
    void Awake()
    {
        textMeshProUGUI = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        BindManager(GameFlowManager._instance);
    }

    public void ScoreText(int coin)
    {
        if (textMeshProUGUI == null)
            textMeshProUGUI = GetComponent<TextMeshProUGUI>();
        textMeshProUGUI.text = coin.ToString();
    }

    private void OnEnable()
    {
        if (!isHighScore)
            CurrentScoreDisplay = this;

        // OnEnable may run before the manager's Awake. Also bind when it becomes
        // ready, rather than depending on scene object execution order.
        GameFlowManager.InstanceReady -= BindManager;
        GameFlowManager.InstanceReady += BindManager;
        BindManager(GameFlowManager._instance);
    }

    private void OnDisable()
    {
        if (CurrentScoreDisplay == this)
            CurrentScoreDisplay = null;

        GameFlowManager.InstanceReady -= BindManager;
        UnbindManager();
    }

    private void BindManager(GameFlowManager manager)
    {
        UnbindManager();
        subscribedManager = manager;
        if (manager == null)
        {
            ScoreText(isHighScore ? GameFlowManager.SavedHighScore : 0);
            return;
        }

        if (isHighScore)
        {
            manager._updateHighScore.AddListener(ScoreText);
            ScoreText(manager.HighScore);
        }
        else
        {
            manager._updateScore.AddListener(ScoreText);
            ScoreText(manager.Score);
        }
    }

    private void UnbindManager()
    {
        // Keep the actual subscription owner: the global instance can already
        // have been reset when Unity disables this UI during Play Mode exit.
        if (!ReferenceEquals(subscribedManager, null))
        {
            if (isHighScore)
                subscribedManager._updateHighScore?.RemoveListener(ScoreText);
            else
                subscribedManager._updateScore?.RemoveListener(ScoreText);
        }
        subscribedManager = null;
    }
}
