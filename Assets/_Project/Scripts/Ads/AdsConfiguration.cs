using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[CreateAssetMenu(fileName = "AdsConfiguration", menuName = "Blokki Pop/Ads Configuration")]
public sealed class AdsConfiguration : ScriptableObject
{
  
    public const string ResourcePath = "Ads/AdsConfiguration";
    public const string AndroidTestAppId = "ca-app-pub-3940256099942544~3347511713";
    // Official v11.5.0 Google Mobile Ads Unity sample rewarded unit.
    public const string AndroidTestRewardedId = "ca-app-pub-3940256099942544/5224354917";

    [Header("Safety gates — leave these defaults for development")]
    [SerializeField] private bool useTestAds = true;
    [Tooltip("Enable only after real Android device, consent, every mediation partner, age targeting, privacy policy, app-ads.txt and store declarations have been audited. Not a legal certification.")]
    [SerializeField] private bool liveAdsComplianceApproved;
    [Tooltip("Explicit TEST / NO REVENUE button only. Never enables simulated ads in a production build.")]
    [SerializeField] private bool enableDevelopmentRewardSimulation;
    [Tooltip("OFF by default. Device-only mediation QA with your own rewarded unit: every QA device must be explicitly registered as a Google test device and Unity Ads test mode must be enabled in its dashboard. This cannot prove a test ID belongs to the current phone.")]
    [SerializeField] private bool useConfiguredMediationTestUnitInDevelopment;

    [Header("Live Android identifiers — never enter account passwords here")]
    [SerializeField] private string androidAppId = "";
    [SerializeField] private string androidRewardedAdUnitId = "";
    [SerializeField] private string privacyPolicyUrl = "";
    [Tooltip("Hashed Google test device IDs. Used only by development builds; Unity Ads also needs dashboard test mode for mediated-source testing.")]
    [SerializeField] private List<string> testDeviceIds = new();

    [Header("Reliability")]
    [SerializeField, Min(5f)] private float minimumLoadRetrySeconds = 15f;
    [SerializeField, Range(0, 6)] private int maximumLoadRetries = 4;
    [SerializeField, Range(1f, 55f)] private float rewardedExpiryMinutes = 50f;
    [SerializeField, Range(15f, 120f)] private float requestTimeoutSeconds = 45f;
  
    public bool UseTestAds => useTestAds;
    public bool LiveAdsComplianceApproved => liveAdsComplianceApproved;
    public bool EnableDevelopmentRewardSimulation => enableDevelopmentRewardSimulation;
    public bool UseConfiguredMediationTestUnitInDevelopment => useConfiguredMediationTestUnitInDevelopment;
    public string AndroidAppId => androidAppId?.Trim() ?? "";
    public string AndroidRewardedAdUnitId => androidRewardedAdUnitId?.Trim() ?? "";
    public string PrivacyPolicyUrl => privacyPolicyUrl?.Trim() ?? "";
    public IReadOnlyList<string> TestDeviceIds => testDeviceIds ?? (IReadOnlyList<string>)Array.Empty<string>();
    public float MinimumLoadRetrySeconds => Mathf.Max(5f, minimumLoadRetrySeconds);
    public int MaximumLoadRetries => Mathf.Clamp(maximumLoadRetries, 0, 6);
    public double RewardedExpirySeconds => Mathf.Clamp(rewardedExpiryMinutes, 1f, 55f) * 60d;
    public float RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 15f, 120f);

    /// <summary>Release ads are opt-in. Development defaults to demo IDs; configured-unit QA is explicit.</summary>
    public bool TryGetRewardedUnit(bool developmentBuild, out string unitId, out string reason)
    {
        unitId = "";
        reason = "";
        if (developmentBuild)
        {
            if (UseConfiguredMediationTestUnitInDevelopment)
            {
                if (!IsLiveAppId(AndroidAppId) || !IsLiveAdUnitId(AndroidRewardedAdUnitId))
                {
                    reason = "Mediation QA disabled: real Android app/unit IDs are missing.";
                    return false;
                }
                bool hasTestDevice = false;
                foreach (string testDevice in TestDeviceIds)
                    hasTestDevice |= !string.IsNullOrWhiteSpace(testDevice);
                if (!hasTestDevice)
                {
                    reason = "Mediation QA disabled: register the actual QA phone as a test device first.";
                    return false;
                }
                unitId = AndroidRewardedAdUnitId;
                return true;
            }
            unitId = AndroidTestRewardedId;
            return true;
        }
        if (UseTestAds)
            reason = "Release ads disabled: test mode is still enabled.";
        else if (!LiveAdsComplianceApproved)
            reason = "Release ads disabled: compliance/device audit is not approved.";
        else if (!IsLiveAppId(AndroidAppId) || !IsLiveAdUnitId(AndroidRewardedAdUnitId))
            reason = "Release ads disabled: real AdMob Android IDs are missing.";
        else if (!HasValidPrivacyPolicyUrl)
            reason = "Release ads disabled: HTTPS privacy policy URL is missing.";
        else
        {
            unitId = AndroidRewardedAdUnitId;
            return true;
        }
        return false;
    }

    public bool HasValidPrivacyPolicyUrl => Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out Uri uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);

    public static bool IsLiveAppId(string value) => IsLiveId(value, '~');
    public static bool IsLiveAdUnitId(string value) => IsLiveId(value, '/');

    private static bool IsLiveId(string value, char separator)
    {
        if (string.IsNullOrEmpty(value) || value.Contains("3940256099942544"))
            return false;
        return Regex.IsMatch(value, @"^ca-app-pub-\d{16}" + Regex.Escape(separator.ToString()) + @"\d{10}$");
    }
}
