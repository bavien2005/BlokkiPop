using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private GameObject panel_;

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
        AudioManager._instance.musicSource.volume = musicSilder.value;
        AudioManager._instance.sfxSource.volume = soundSilder.value;
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
        
        GameFlowManager._instance.RestartGame();
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
