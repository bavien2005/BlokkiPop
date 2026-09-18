using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager _instance;
    public static AudioManager Instance => _instance;
    [SerializeField] protected Sound[] musicSound, sfxSound;
    [SerializeField] public AudioSource musicSource, sfxSource;



    private void Start()
    {
        PlayMusic("Background");
    }
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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

    public void VolumeMusic(float value)
    {
        this.musicSource.volume = value;
    }
    public void VolumeSound(float value)
    {
        this.sfxSource.volume = value;
    }
}
