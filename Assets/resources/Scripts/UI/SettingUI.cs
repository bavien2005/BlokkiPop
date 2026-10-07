using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private GameObject panel_;

    private AudioManager audioManager;

    private bool isOpen = false;

    public  Slider musicSilder, soundSilder;

    public Image musicImg, soundImg;

    public Sprite musicImgMuted, soundImgMuted , musicImgNormal , soundImgNormal;


    private void Awake()
    {
     
        panel_.SetActive(isOpen);
    }
    private void Start()
    {
        audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogError("SettingUI could not find the persistent AudioManager.", this);
            return;
        }

        // The scene's serialized slider events point at its scene AudioManager,
        // which is discarded when the persistent singleton survives a replay.
        // Rebind at runtime to the live manager and preserve its current volume.
        if (musicSilder != null)
        {
            if (audioManager.musicSource != null)
                musicSilder.SetValueWithoutNotify(audioManager.musicSource.volume);
            musicSilder.onValueChanged.AddListener(audioManager.VolumeMusic);
        }

        if (soundSilder != null)
        {
            if (audioManager.sfxSource != null)
                soundSilder.SetValueWithoutNotify(audioManager.sfxSource.volume);
            soundSilder.onValueChanged.AddListener(audioManager.VolumeSound);
        }
    }

    private void OnDestroy()
    {
        if (audioManager == null)
            return;

        if (musicSilder != null)
            musicSilder.onValueChanged.RemoveListener(audioManager.VolumeMusic);
        if (soundSilder != null)
            soundSilder.onValueChanged.RemoveListener(audioManager.VolumeSound);
    }
    public void ActivePanelSetting()
    {
        Debug.Log("cmkais");
        isOpen = !isOpen;
        panel_?.SetActive(isOpen);
    }

    public void ActivePanelSetting(bool check)
    {
        Debug.Log("fser4r");
        panel_?.SetActive(check);
    }

    public void RestartGame()
    {
        
        GameFlowManager._instance?.RestartGame();
        panel_?.SetActive(false);

    }

    public void GamePlay()
    {
        GameFlowManager._instance.GamePlay();
    }

    public void Quit()
    {
        GameFlowManager._instance.QuitGame();
    }

    public void ChangeSpriteMusic(float value)
    {
       if(value == 0)
       {
            musicImg.sprite = musicImgMuted;
       }
       else
       {
            musicImg.sprite = musicImgNormal;
       }
    }

    public void ChangeSpriteSound(float value)
    {
        if (value == 0)
        {
            soundImg.sprite = soundImgMuted;
        }
        else
        {
            soundImg.sprite = soundImgNormal;
        }
    }
}
