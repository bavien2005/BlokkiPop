using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.iOS;
using UnityEngine;

/// <summary>
/// Applies the authored Blokki Pop artwork to Unity's Android and iOS icon slots.
/// Run from Tools > Blokki Pop > Apply App Icons after Unity imports the images.
/// </summary>
public static class BlokkiPopStoreIconSetup
{
    private const string FullIconPath = "Assets/_Project/StoreAssets/BlokkiPop-Icon-Source.png";
    private const string ForegroundPath = "Assets/_Project/StoreAssets/BlokkiPop-Android-Foreground.png";
    private const string BackgroundPath = "Assets/_Project/StoreAssets/BlokkiPop-Android-Background.png";

    [MenuItem("Tools/Blokki Pop/Apply App Icons")]
    public static void ApplyAppIcons()
    {
        Texture2D fullIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(FullIconPath);
        Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);

        if (fullIcon == null || foreground == null || background == null)
        {
            Debug.LogError("Blokki Pop icon assets are missing. Refresh the Project window and try again.");
            return;
        }

        bool androidLegacySet = ApplySingleLayerIcons(
            NamedBuildTarget.Android,
            AndroidPlatformIconKind.Legacy,
            fullIcon);

        // Unity's Android adaptive slots are ordered Background, then Foreground.
        bool androidAdaptiveSet = ApplyIcons(
            NamedBuildTarget.Android,
            AndroidPlatformIconKind.Adaptive,
            background,
            foreground);

        bool iosSet = ApplySingleLayerIcons(
            NamedBuildTarget.iOS,
            iOSPlatformIconKind.Application,
            fullIcon);

        if (androidLegacySet && androidAdaptiveSet && iosSet)
            Debug.Log("Blokki Pop application icons assigned for Android legacy/adaptive and iOS.");
    }

    private static bool ApplySingleLayerIcons(
        NamedBuildTarget target,
        PlatformIconKind kind,
        Texture2D texture)
    {
        return ApplyIcons(target, kind, texture);
    }

    private static bool ApplyIcons(
        NamedBuildTarget target,
        PlatformIconKind kind,
        params Texture2D[] textures)
    {
        PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(target, kind);
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning($"Unity returned no icon slots for {target} / {kind}. Switch to that build target and retry.");
            return false;
        }

        foreach (PlatformIcon slot in slots)
        {
            if (slot.maxLayerCount != textures.Length)
            {
                Debug.LogError(
                    $"Unexpected layer count for {target} / {kind}: expected {slot.maxLayerCount}, got {textures.Length}.");
                return false;
            }

            slot.SetTextures(textures);
        }

        PlayerSettings.SetPlatformIcons(target, kind, slots);
        return true;
    }
}
