#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Fail early instead of accidentally distributing demo ads or mismatched App IDs.</summary>
public sealed class BlokkiPopAdsBuildGuard : IPreprocessBuildWithReport
{
    public const string ConfigurationPath = "Assets/_Project/Resources/Ads/AdsConfiguration.asset";
    private const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;
        bool development = (report.summary.options & BuildOptions.Development) != 0;
        Validate(development);
    }

    [MenuItem("Blokki Pop/Ads/Select Configuration")]
    public static void SelectConfiguration()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AdsConfiguration>(ConfigurationPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Blokki Pop/Ads/Prepare Google Demo Test App ID")]
    public static void PrepareDemo()
    {
        SetSdkAppId(AdsConfiguration.AndroidTestAppId);
        Debug.Log("[Ads] Google demo App ID prepared. Build Android with Development Build enabled. Demo ads do not test Unity Ads bidding or earn revenue.");
    }

    [MenuItem("Blokki Pop/Ads/Prepare Configured Real App ID")]
    public static void PrepareConfigured()
    {
        AdsConfiguration configuration = LoadConfiguration();
        if (!AdsConfiguration.IsLiveAppId(configuration.AndroidAppId))
            throw new InvalidOperationException("Enter your own Android App ID in AdsConfiguration first (App ID contains ~, not /).");
        SetSdkAppId(configuration.AndroidAppId);
        Debug.Log("[Ads] Configured App ID prepared. This does not approve consent, store policies, mediation accounts, or release readiness.");
    }

    [MenuItem("Blokki Pop/Ads/Validate Android Development Settings")]
    public static void ValidateDevelopment() => Validate(true);

    [MenuItem("Blokki Pop/Ads/Validate Android Release Settings")]
    public static void ValidateRelease() => Validate(false);

    private static void Validate(bool development)
    {
        AdsConfiguration configuration = LoadConfiguration();
        if (!configuration.TryGetRewardedUnit(development, out string unitId, out string reason))
            throw new BuildFailedException(reason + " See Docs/AdMob-Integration.md.");
        // A configured-unit test uses the publisher's App ID; a Google demo unit uses its sample App ID.
        string expectedAppId = development && unitId == AdsConfiguration.AndroidTestRewardedId
            ? AdsConfiguration.AndroidTestAppId : configuration.AndroidAppId;
        UnityEngine.Object settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsPath);
        if (settings == null)
            throw new BuildFailedException("Google Mobile Ads settings asset is missing. Wait for Package Manager to install the pinned SDK.");
        SerializedProperty appId = new SerializedObject(settings).FindProperty("adMobAndroidAppId");
        if (appId == null || appId.stringValue?.Trim() != expectedAppId)
            throw new BuildFailedException("AdMob App ID mismatch. Use Blokki Pop > Ads > Prepare Google Demo Test App ID or Prepare Configured Real App ID, matching your chosen test/release mode.");
        if ((int)PlayerSettings.Android.minSdkVersion < 24)
            throw new BuildFailedException("The pinned AdMob SDK requires Android minimum API 24 or higher.");
        if ((int)PlayerSettings.Android.targetSdkVersion != 0 && (int)PlayerSettings.Android.targetSdkVersion < 35)
            throw new BuildFailedException("The pinned AdMob SDK requires Android target API 35 or higher.");
        Debug.Log(development
            ? "[Ads] Development settings validated. Test ads only; no revenue. Device/dashboard testing is still required."
            : "[Ads] Release configuration gates passed. This is not a legal certification or proof of live mediation bidding.");
    }

    private static AdsConfiguration LoadConfiguration()
    {
        AdsConfiguration asset = AssetDatabase.LoadAssetAtPath<AdsConfiguration>(ConfigurationPath);
        if (asset == null)
            throw new BuildFailedException("AdsConfiguration.asset is missing or has not imported yet.");
        return asset;
    }

    private static void SetSdkAppId(string value)
    {
        UnityEngine.Object settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsPath);
        if (settings == null)
            throw new InvalidOperationException("Google Mobile Ads settings have not imported yet. Let Package Manager finish first.");
        SerializedObject serialized = new(settings);
        SerializedProperty property = serialized.FindProperty("adMobAndroidAppId");
        if (property == null)
            throw new InvalidOperationException("The SDK settings schema has changed. Inspect the installed Google Mobile Ads settings before continuing.");
        Undo.RecordObject(settings, "Change AdMob Android App ID");
        property.stringValue = value;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
#endif
