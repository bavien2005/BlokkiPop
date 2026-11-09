using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored pause sheet and existing sound/haptics preferences; no UI creation.</summary>
[ExecuteAlways]
public sealed class PopPausePresentation : MonoBehaviour
{
    public RectTransform safeRoot, card;
    public TMP_Text soundStatus, hapticsStatus;
    public Slider volume;
    public bool fitDeviceSafeArea = true;
    [Header("Copy")]
    public string heading = "TẠM DỪNG";
    public string notice = "Ván chơi của bạn đã được lưu.";
    public string resume = "CHƠI TIẾP", home = "VỀ TRANG CHỦ";
    public string privacy = "RIÊNG TƯ", rewards = "GIAO DIỆN";
    public string practice = "BÀI LUYỆN";
    public string onText = "BẬT", offText = "TẮT";
    public bool HasPresentation => safeRoot != null && card != null && soundStatus != null && hapticsStatus != null && volume != null;
    private void OnEnable() { FitSafeArea(); if (Application.isPlaying) RefreshSettings(); }
    private void Update() => FitSafeArea();
    private void FitSafeArea()
    {
        if (!fitDeviceSafeArea || safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        Vector2 min = new(safe.xMin / Screen.width, safe.yMin / Screen.height), max = new(safe.xMax / Screen.width, safe.yMax / Screen.height);
        if (safeRoot.anchorMin != min) safeRoot.anchorMin = min;
        if (safeRoot.anchorMax != max) safeRoot.anchorMax = max;
    }
    public void RefreshSettings()
    {
        if (soundStatus != null) soundStatus.text = PlayerPrefs.GetInt("blokkipop.home.sound.v1", 1) == 1 ? onText : offText;
        if (hapticsStatus != null) hapticsStatus.text = GameHaptics.IsEnabled ? onText : offText;
        if (volume != null && AudioManager.Instance != null && AudioManager.Instance.sfxSource != null) volume.SetValueWithoutNotify(AudioManager.Instance.sfxSource.volume);
    }
    public void ToggleSound()
    {
        bool on = PlayerPrefs.GetInt("blokkipop.home.sound.v1", 1) == 0;
        PlayerPrefs.SetInt("blokkipop.home.sound.v1", on ? 1 : 0); PlayerPrefs.Save(); AudioListener.volume = on ? 1 : 0; RefreshSettings();
    }
    public void ToggleHaptics()
    { PlayerPrefs.SetInt("blokkipop.haptics.enabled", GameHaptics.IsEnabled ? 0 : 1); PlayerPrefs.Save(); RefreshSettings(); }
    public void SetVolume(float value) => AudioManager.Instance?.VolumeSound(Mathf.Clamp01(value));
}
