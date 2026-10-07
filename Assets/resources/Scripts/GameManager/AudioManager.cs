using System.Collections;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager _instance;
    public static AudioManager Instance => _instance;
    [SerializeField] protected Sound[] musicSound, sfxSound;
    [SerializeField] public AudioSource musicSource, sfxSource;

    [Header("Arcade Game Cues")]
    [Tooltip("Minimum time between spoken callouts so clear chains do not become noisy.")]
    [SerializeField, Min(0f)] private float voiceCalloutCooldown = 0.9f;
    [Tooltip("Extra gain for normalized voice clips, still multiplied by the SFX slider.")]
    [SerializeField, Range(0.5f, 1.5f)] private float voiceGain = 1.1f;

    private readonly Dictionary<string, AudioClip> voiceClips = new();
    private AudioClip invalidDropTone;
    private AudioClip clearTone;
    private AudioClip bigClearTone;
    private AudioSource voiceSource;
    private float nextVoiceAt;



    private void Start()
    {
        if (_instance != this)
            return;

        PlayMusic("Background");
    }
    private void Awake()
    {
       if(_instance != null && _instance != this)
        {
            // Destroy the newly loaded duplicate as a whole. Destroying only
            // the old component leaves its AudioSources alive and playing.
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        PrepareVoiceSource();
        BuildArcadeTones();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        if (invalidDropTone != null) Destroy(invalidDropTone);
        if (clearTone != null) Destroy(clearTone);
        if (bigClearTone != null) Destroy(bigClearTone);
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
        if (s == null || musicSource == null) return;
        if (musicSource.clip == s.clip && musicSource.isPlaying) return;
        this.musicSource.clip = s.clip;
        this.musicSource.Play();
    }
    public void PlayMusic(string name, float volume)
    {
        Sound s = Array.Find(musicSound, sound => sound.soundName.ToString() == name);
        if (s == null || musicSource == null) return;
        if (musicSource.clip == s.clip && musicSource.isPlaying)
        {
            musicSource.volume = volume;
            return;
        }
        this.musicSource.clip = s.clip;
        this.musicSource.volume = volume;
        this.musicSource.Play();
    }
    public void StopMusic()
    {
        if (musicSource != null)
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

    /// <summary>Short low thud for an invalid drop, with no voice callout.</summary>
    public void PlayInvalidDrop()
    {
        PlayGeneratedTone(invalidDropTone, 0.65f);
    }

    /// <summary>Layers a rising arcade chime and an English male callout onto a successful clear.</summary>
    public void PlayClearCue(int clearedLines, int combo, bool feverStarted)
    {
        bool bigClear = clearedLines >= 3 || combo >= 4 || feverStarted;
        PlayGeneratedTone(bigClear ? bigClearTone : clearTone, bigClear ? 0.85f : 0.62f);

        string voice = feverStarted ? "Voice_Fever"
            : clearedLines >= 4 ? "Voice_MegaClear"
            : clearedLines == 3 ? "Voice_TripleClear"
            : clearedLines == 2 ? "Voice_DoubleClear"
            : combo >= 3 ? "Voice_Nice"
            : UnityEngine.Random.value < 0.5f ? "Voice_Good" : "Voice_Nice";
        StartCoroutine(PlayVoiceAfterChime(voice));
    }

    private IEnumerator PlayVoiceAfterChime(string voice)
    {
        yield return new WaitForSecondsRealtime(0.12f);
        PlayVoice(voice);
    }

    /// <summary>Plays a short male English game-over sign-off.</summary>
    public void PlayGameOverCue()
    {
        PlayVoice("Voice_GameOver", true);
    }

    private void PlayVoice(string resourceName, bool ignoreCooldown = false)
    {
        if (voiceSource == null || (!ignoreCooldown && Time.unscaledTime < nextVoiceAt))
            return;

        if (!voiceClips.TryGetValue(resourceName, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>($"Voice/{resourceName}");
            voiceClips[resourceName] = clip;
        }

        if (clip == null)
        {
            Debug.LogWarning($"Voice cue '{resourceName}' was not found under Assets/Resources/Voice.", this);
            return;
        }

        nextVoiceAt = Time.unscaledTime + voiceCalloutCooldown;
        // The source slider remains the master SFX control; clips are normalized in the project.
        voiceSource.PlayOneShot(clip, voiceGain);
    }

    private void PrepareVoiceSource()
    {
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        if (sfxSource != null)
        {
            voiceSource.volume = sfxSource.volume;
            voiceSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        }
    }

    private void PlayGeneratedTone(AudioClip clip, float volume)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    private void BuildArcadeTones()
    {
        // Tiny procedural cues keep the interaction sounds self-contained and avoid extra asset wiring.
        invalidDropTone = CreateTone("Invalid Drop", new[] { 135f, 92f }, 0.16f, 0.28f);
        clearTone = CreateTone("Line Clear", new[] { 740f, 990f, 1320f }, 0.24f, 0.20f);
        bigClearTone = CreateTone("Big Clear", new[] { 523f, 784f, 1047f, 1568f }, 0.42f, 0.23f);
    }

    private static AudioClip CreateTone(string clipName, float[] notes, float duration, float amplitude)
    {
        const int sampleRate = 24000;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        float[] data = new float[samples];
        for (int i = 0; i < samples; ++i)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;
            float notePosition = progress * notes.Length;
            int noteIndex = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(notePosition));
            float withinNote = notePosition - noteIndex;
            float frequency = Mathf.Lerp(notes[noteIndex], notes[Mathf.Min(noteIndex + 1, notes.Length - 1)], withinNote);
            float envelope = Mathf.Clamp01(Mathf.Min(progress * 18f, (1f - progress) * 7f));
            float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * t);
            float overtone = Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * t) * 0.22f;
            data[i] = (fundamental + overtone) * envelope * amplitude;
        }
        clip.SetData(data, 0);
        return clip;
    }

    public void VolumeMusic(float value)
    {
        if (musicSource != null)
            this.musicSource.volume = value;
    }
    public void VolumeSound(float value)
    {
        if (sfxSource != null)
            this.sfxSource.volume = value;
        if (voiceSource != null)
            voiceSource.volume = value;
    }
}
