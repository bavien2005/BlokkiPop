using System;
using Unity.VisualScripting;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    protected static AudioManager instance;
    public static AudioManager Instance => instance;
    [SerializeField] protected Sound[] musicSound, sfxSound;
    [SerializeField] protected AudioSource musicSource, sfxSource;
    public AudioSource SfxSource => sfxSource;
    private  void Start()
    {
        this.ClockMusic();
    }
    private void Awake()
    {
        if (AudioManager.instance != null) 
            Debug.LogWarning("Only 1 AudioManager allow to exist");
        AudioManager.instance = this;
    }
    public void ClockMusic()
    {
        this.musicSource.gameObject.SetActive(false);
    }
    public void OpenMusic()
    {
        this.musicSource.gameObject.SetActive(true);
    }
    public void PlayMusic(string name)
    {
        Sound s = Array.Find(musicSound, sound => sound.soundName.ToString() == name);
        if (s == null) return;
        this.musicSource.clip = s.clip;
        this.musicSource.Play();
    }
    public void PlayMusic(string name, float volume)
    {
        Sound s = Array.Find(musicSound, sound => sound.soundName.ToString() == name);
        if (s == null) return;
        this.musicSource.clip = s.clip;
        this.musicSource.volume = volume;
        this.musicSource.Play();
    }
    public void StopMusic()
    {
        this.musicSource.Stop();
    }
    public void PlaySFX(string name)
    {
        Sound s = Array.Find(sfxSound, sound => sound.soundName.ToString() == name);
        if (s == null) return;
        this.sfxSource.PlayOneShot(s.clip);
    }
    public void PlaySFX(string name, float volume)
    {
        Sound s = Array.Find(sfxSound, sound => sound.soundName.ToString() == name);
        if (s == null) return;
        this.sfxSource.PlayOneShot(s.clip, volume);
    }
}
