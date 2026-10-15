using UnityEngine;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour
{
    private const string HapticsPreference = "blokkipop.haptics.enabled";

    [SerializeField] private GameObject panel_;
    [SerializeField] private Slider musicSilder;
    [SerializeField] private Slider soundSilder;
    [SerializeField] private Image musicImg;
    [SerializeField] private Image soundImg;
    [SerializeField] private Sprite musicImgMuted;
    [SerializeField] private Sprite soundImgMuted;
    [SerializeField] private Sprite musicImgNormal;
    [SerializeField] private Sprite soundImgNormal;

    [Header("Haptics Toggle (Inspector-wired scene UI)")]
    [SerializeField] private Toggle hapticsToggle;
    [SerializeField] private Image hapticsImage;
    [SerializeField] private Sprite hapticsOnSprite;
    [SerializeField] private Sprite hapticsOffSprite;

    private AudioManager audioManager;
    private bool isOpen;
    private float timeScaleBeforeOpening;

    private void Awake()
    {
        panel_?.SetActive(false);
    }

    private void Start()
    {
        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("SettingUI could not find the persistent AudioManager.", this);
        }
        else
        {
            // Background music has been removed from gameplay.
            if (musicSilder != null)
                musicSilder.gameObject.SetActive(false);

            if (soundSilder != null)
            {
                if (audioManager.sfxSource != null)
                    soundSilder.SetValueWithoutNotify(audioManager.sfxSource.volume);
                soundSilder.onValueChanged.AddListener(audioManager.VolumeSound);
            }
        }

        // The Toggle itself, its image, and its persistent On Value Changed event
        // are assigned on the Game scene object through the Inspector.
        if (hapticsToggle != null)
            hapticsToggle.SetIsOnWithoutNotify(GameHaptics.IsEnabled);
        RefreshHapticsImage(GameHaptics.IsEnabled);
    }

    private void OnDestroy()
    {
        if (audioManager != null && soundSilder != null)
            soundSilder.onValueChanged.RemoveListener(audioManager.VolumeSound);
    }

    public void SetHapticsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(HapticsPreference, enabled ? 1 : 0);
        PlayerPrefs.Save();
        RefreshHapticsImage(enabled);
    }

    private void RefreshHapticsImage(bool enabled)
    {
        if (hapticsImage != null)
            hapticsImage.sprite = enabled ? hapticsOnSprite : hapticsOffSprite;
    }

    public void ActivePanelSetting()
    {
        ActivePanelSetting(!isOpen);
    }

    public void ActivePanelSetting(bool check)
    {
        if (check == isOpen) return;
        if (check)
        {
            timeScaleBeforeOpening = Time.timeScale;
            PopGameplaySession.Instance?.SaveBeforeNavigation();
            Time.timeScale = 0f;
        }
        else if (GameFlowManager._instance == null || !GameFlowManager._instance.IsGameOver)
            Time.timeScale = timeScaleBeforeOpening;
        isOpen = check;
        panel_?.SetActive(check);
    }

    public void RestartGame()
    {
        ActivePanelSetting(false);
        GameFlowManager._instance?.RestartGame();
        panel_?.SetActive(false);
    }

    public void GamePlay()
    {
        GameFlowManager._instance?.GamePlay();
    }

    public void Quit()
    {
        GameFlowManager._instance?.QuitGame();
    }

    public void ChangeSpriteMusic(float value)
    {
        if (musicImg != null)
            musicImg.sprite = value == 0 ? musicImgMuted : musicImgNormal;
    }

    public void ChangeSpriteSound(float value)
    {
        if (soundImg != null)
            soundImg.sprite = value == 0 ? soundImgMuted : soundImgNormal;
    }
}

/// <summary>Small, opt-out haptic feedback for successful gameplay moves.</summary>
public static class GameHaptics
{
    private const string PreferenceKey = "blokkipop.haptics.enabled";

    public static bool IsEnabled => PlayerPrefs.GetInt(PreferenceKey, 1) == 1;

    public static void Vibrate()
    {
        if (!IsEnabled || !Application.isMobilePlatform)
            return;

        Handheld.Vibrate();
    }
}
