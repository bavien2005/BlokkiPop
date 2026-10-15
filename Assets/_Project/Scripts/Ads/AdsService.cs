using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Mediation.UnityAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One persistent owner of consent, SDK initialization and rewarded ads.
/// Gameplay never chooses a winning network: the AdMob dashboard/adapters do.
/// All public calls are made on Unity's main thread; native callbacks are marshalled.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class AdsService : MonoBehaviour
{
    public static AdsService Instance { get; private set; }

    public event Action StateChanged;
    public AdsAgeCategory Audience { get; private set; } = AdsAgeCategory.Unknown;
    public string StatusText { get; private set; } = "Choose an age range before enabling ads.";
    public AdsConfiguration Configuration => configuration;
    public bool IsShowing => activeRequest != null;
    public bool IsBusy => IsShowing || consentBusy || sdkInitializing || privacyFormOpen;
    public bool IsInitialized => sdkInitialized && adsAllowed;
    public bool HasStartedSdkInitialization => sdkInitializationStarted;
    public bool CanChangeAudienceWithoutRestart => !sdkInitializationStarted && !IsBusy;
    public bool PrivacyOptionsRequired { get; private set; }
    public string LastLoadedAdapterClassName { get; private set; } = "";
    public string LastAdFailureReason { get; private set; } = "";
    public bool CanSimulateDevelopmentReward => IsDevelopmentSimulationAvailable;
    public bool CanRetryAds => alive && Audience == AdsAgeCategory.Adult18Plus && !IsBusy && !loading
        && !ageChangeRequiresRestart && rewardedAd == null && (sdkInitialized || !sdkInitializationStarted);
    public bool IsGoogleDemoDevelopmentMode => Debug.isDebugBuild && configuration != null
        && !configuration.UseConfiguredMediationTestUnitInDevelopment;
    public bool IsRewardedReady => adsAllowed && !IsBusy && rewardedAd != null
        && Now - adLoadedAt < configuration.RewardedExpirySeconds && rewardedAd.CanShowAd();

    public bool IsDevelopmentSimulationAvailable
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return alive && configuration != null && configuration.EnableDevelopmentRewardSimulation
                && Audience == AdsAgeCategory.Adult18Plus && !IsBusy;
#else
            return false;
#endif
        }
    }

    private AdsConfiguration configuration;
    private bool ownsConfiguration;
    private bool alive;
    private SynchronizationContext mainThreadContext;
    private int mainThreadId;
    private int consentRevision;
    private int loadRevision;
    private bool consentBusy;
    private bool sdkInitializing;
    private bool sdkInitializationStarted;
    private bool sdkInitialized;
    private bool ageChangeRequiresRestart;
    private bool privacyFormOpen;
    private bool adsAllowed;
    private bool loading;
    private bool applicationPaused;
    private bool applicationFocused = true;
    private string requestedUnitId;
    private double operationDeadline;
    private double loadDeadline;
    private double nextLoadAt = double.PositiveInfinity;
    private double adLoadedAt;
    private int failedLoadCount;
    private RewardedAd rewardedAd;
    private RewardRequest activeRequest;
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
    private readonly List<string> developmentDiagnostics = new();
#endif

    private static double Now => Time.realtimeSinceStartupAsDouble;
    private const double CloseCallbackSettleSeconds = 2d;

    private sealed class RewardRequest
    {
        public RewardedAd Ad;
        public Action<RewardedAdResult> Completion;
        public Object Owner;
        public bool HasOwner;
        public bool Earned;
        public bool Cancelled;
        public bool Completed;
        public bool Opened;
        public bool Closed;
        public double StartedAt;
        public double CompleteAfter;
        public bool SawNativeBackground;
        public double ReturnedToForegroundAt = -1d;
        public bool DevelopmentTest;
        public int SceneHandle;
        public float PreviousTimeScale;
        public bool PreviousAudioPause;
        public Action OpenedHandler;
        public Action ClosedHandler;
        public Action<AdError> FailedHandler;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("AdsService").AddComponent<AdsService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        alive = true;
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        mainThreadContext = SynchronizationContext.Current;
        configuration = Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
        if (configuration == null)
        {
            // Safe defaults allow normal gameplay if the asset is accidentally missing.
            configuration = ScriptableObject.CreateInstance<AdsConfiguration>();
            ownsConfiguration = true;
        }
    }

    private void Update()
    {
        if (!alive) return;
        if (activeRequest != null)
        {
            RewardRequest request = activeRequest;
            if (request.HasOwner && request.Owner == null)
                request.Cancelled = true;
            if (request.SceneHandle != SceneManager.GetActiveScene().handle)
                request.Cancelled = true;
            if (request.Closed && Now >= request.CompleteAfter)
                FinishRequest(request, request.Earned ? RewardedAdResult.Earned : RewardedAdResult.ClosedWithoutReward);
            else if (!request.Closed && request.SawNativeBackground && request.ReturnedToForegroundAt >= 0d
                && !applicationPaused && applicationFocused && Now - request.ReturnedToForegroundAt >= 5d)
            {
                // A missing close callback must not permanently trap the run. Only recover
                // after the Unity app actually returns from the native activity and a grace
                // period elapses. A timeout never invents a close/reward or ends an active video.
                FailRequest(request, "Native ad returned to the game without a close callback.");
            }
            else if (!request.Opened && !request.Closed && !request.SawNativeBackground
                && !applicationPaused && applicationFocused && Now - request.StartedAt >= configuration.RequestTimeoutSeconds)
                FailRequest(request, "Native ad did not open before the show timeout.");
        }
        if ((consentBusy || sdkInitializing || privacyFormOpen) && Now >= operationDeadline)
        {
            ++consentRevision;
            consentBusy = sdkInitializing = privacyFormOpen = false;
            adsAllowed = false;
            DestroyCachedAd();
            SetStatus("Ads unavailable: privacy/initialization timed out. Gameplay remains available.");
        }
        if (loading && Now >= loadDeadline)
        {
            ++loadRevision; // Late native load responses are destroyed, never shown.
            loading = false;
            ScheduleLoadRetry("Ad load timed out.");
        }
        if (rewardedAd != null && !IsShowing &&
            (Now - adLoadedAt >= configuration.RewardedExpirySeconds || !rewardedAd.CanShowAd()))
        {
            DestroyCachedAd();
            nextLoadAt = Now;
        }
        if (adsAllowed && sdkInitialized && !IsBusy && !loading && rewardedAd == null && Now >= nextLoadAt)
            LoadRewarded();
    }

    /// <summary>
    /// Unknown and under-18 players stay ad-free. Age is never substituted for UMP consent.
    /// Once native SDK initialization begins, changing age disables ads until next app launch.
    /// </summary>
    public void InitializeForAudience(AdsAgeCategory category)
    {
        if (!alive || IsShowing || privacyFormOpen) return;
        if (!Enum.IsDefined(typeof(AdsAgeCategory), category))
            category = AdsAgeCategory.Unknown;
        if (Audience == category && (consentBusy || sdkInitializing || adsAllowed)) return;

        if (Audience != category && sdkInitializationStarted)
            ageChangeRequiresRestart = true;
        Audience = category;
        ++consentRevision;
        ++loadRevision;
        consentBusy = sdkInitializing = loading = false;
        adsAllowed = false;
        nextLoadAt = double.PositiveInfinity;
        failedLoadCount = 0;
        LastAdFailureReason = "";
        DestroyCachedAd();
        PrivacyOptionsRequired = false;
        if (category != AdsAgeCategory.Adult18Plus)
        {
            SetStatus(category == AdsAgeCategory.Unknown
                ? "Ads off: age range is unknown."
                : "Ads off for under-18 players. You can play normally.");
            return;
        }
        if (ageChangeRequiresRestart)
        {
            SetStatus("Age settings changed. Restart the app before enabling ads.");
            return;
        }
#if UNITY_EDITOR
        // Google's Editor mock is not a device/mediation test. Never silently award from it.
        SetStatus(IsDevelopmentSimulationAvailable
            ? "Editor: explicit TEST reward available; no ad is shown and no revenue is earned."
            : "Editor: native ads require an Android device. TEST reward simulation is disabled.");
#elif UNITY_ANDROID
        if (!configuration.TryGetRewardedUnit(Debug.isDebugBuild, out requestedUnitId, out string blockReason))
        {
            SetStatus(blockReason);
            return;
        }
        GatherConsent();
#else
        SetStatus("Ads are configured for Android only. You can play normally.");
#endif
    }

    private void GatherConsent()
    {
        int revision = consentRevision;
        consentBusy = true;
        operationDeadline = Now + configuration.RequestTimeoutSeconds * 2d;
        SetStatus("Checking privacy choices...");
        var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
        if (Debug.isDebugBuild)
            parameters.ConsentDebugSettings = new ConsentDebugSettings
            {
                TestDeviceHashedIds = new List<string>(configuration.TestDeviceIds)
            };
        try
        {
            ConsentInformation.Update(parameters, error => Dispatch(() =>
            {
                if (!IsConsentOperationCurrent(revision)) return;
                UpdatePrivacyRequirement();
                if (error != null)
                {
                    // UMP may allow ads using a previously valid decision. Never read our own cache.
                    ConsentFinished(revision, error.Message);
                    return;
                }
                try
                {
                    // The combined API may display a native form. Users must be
                    // allowed to read it for as long as they need; UMP exposes
                    // no safe cancellation API. Only the preceding Update has
                    // a network timeout, not an interactive consent decision.
                    operationDeadline = double.PositiveInfinity;
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError => Dispatch(() =>
                    {
                        if (IsConsentOperationCurrent(revision))
                            ConsentFinished(revision, formError?.Message);
                    }));
                }
                catch (Exception exception)
                {
                    ConsentFinished(revision, exception.Message);
                }
            }));
        }
        catch (Exception exception)
        {
            ConsentFinished(revision, exception.Message);
        }
    }

    private bool IsConsentOperationCurrent(int revision) => this != null && alive
        && consentRevision == revision && Audience == AdsAgeCategory.Adult18Plus && !ageChangeRequiresRestart;

    private void ConsentFinished(int revision, string error)
    {
        if (!IsConsentOperationCurrent(revision)) return;
        consentBusy = false;
        UpdatePrivacyRequirement();
        if (!ConsentInformation.CanRequestAds())
        {
            SetStatus("Ads unavailable: privacy choices do not currently allow ad requests."
                + (Debug.isDebugBuild && !string.IsNullOrEmpty(error) ? "\n" + ShortDiagnostic(error) : ""));
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[Ads] UMP: " + error);
            return;
        }
        // This is a deliberately conservative, non-personalized launch configuration.
        // UMP TCF is passed through by the Unity Ads adapter; do not invent consent=true.
        try
        {
            // Android requires MobileAds.initialize() before its first-party ID API.
            // A null nullable field leaves that API untouched during pre-init setup.
            MobileAds.SetRequestConfiguration(CreateRequestConfiguration(configuration, sdkInitialized, Debug.isDebugBuild));
            // Opt out of sale/sharing for Unity Ads as well. Google's rdp flag is not forwarded
            // to every mediation source. Verify partner configuration before enabling release.
            UnityAds.SetConsentMetaData("privacy.consent", false);
            if (sdkInitialized)
            {
                adsAllowed = true;
                nextLoadAt = Now;
                SetStatus("Privacy choices checked. Loading an ad...");
                return;
            }
            if (sdkInitializationStarted)
            {
                adsAllowed = false;
                SetStatus("SDK initialization did not complete. Restart the app to retry.");
                return;
            }
            sdkInitializationStarted = sdkInitializing = true;
            operationDeadline = Now + configuration.RequestTimeoutSeconds * 2d;
            SetStatus("Initializing AdMob mediation...");
            MobileAds.Initialize(status => Dispatch(() =>
            {
                if (this == null || !alive) return;
                sdkInitialized = status != null;
                if (!IsConsentOperationCurrent(revision)) return;
                sdkInitializing = false;
                if (!sdkInitialized)
                {
                    adsAllowed = false;
                    SetStatus("Ad SDK initialization failed. Restart the app to retry; you can keep playing.");
                    return;
                }
                try
                {
                    // Keep requests blocked until the post-init privacy setting succeeds.
                    MobileAds.SetRequestConfiguration(CreateRequestConfiguration(configuration, true, Debug.isDebugBuild));
                }
                catch (Exception exception)
                {
                    StopAdsForSetupFailure("post-init privacy settings", exception);
                    return;
                }
                adsAllowed = true;
                // The initialization callback has settled; individual adapters
                // may still report NotReady. Do not pretend every source works.
                if (Debug.isDebugBuild)
                {
                    try
                    {
                        var adapterStatuses = status.getAdapterStatusMap();
                        if (adapterStatuses != null)
                            foreach (var entry in adapterStatuses)
                                if (entry.Value != null && entry.Value.InitializationState != AdapterState.Ready)
                                    Debug.LogWarning("[Ads] Adapter not ready: " + entry.Key + " — " + entry.Value.Description);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[Ads] Adapter diagnostics unavailable: " + exception.Message);
                    }
                }
                nextLoadAt = Now;
                if (Debug.isDebugBuild)
                    LogDevelopmentDiagnostic("SDK initialized; first-party ID disabled. Rewarded loading may now start.");
                SetStatus("Ad SDK initialization returned. Loading an ad; verify each source in Ad Inspector.");
            }));
        }
        catch (Exception exception)
        {
            StopAdsForSetupFailure(sdkInitializationStarted ? "SDK initialization" : "pre-init privacy settings", exception);
        }
    }

    internal static RequestConfiguration CreateRequestConfiguration(AdsConfiguration config, bool initialized, bool development)
    {
        return new RequestConfiguration
        {
            AgeRestrictedTreatment = AgeRestrictedTreatment.Unspecified,
            MaxAdContentRating = MaxAdContentRating.T,
            PublisherPrivacyPersonalizationState = PublisherPrivacyPersonalizationState.Disabled,
            PublisherFirstPartyIdEnabled = initialized ? false : (bool?)null,
            TestDeviceIds = development ? new List<string>(config.TestDeviceIds) : new List<string>()
        };
    }

    private void StopAdsForSetupFailure(string stage, Exception exception)
    {
        consentBusy = sdkInitializing = loading = false;
        adsAllowed = false;
        nextLoadAt = double.PositiveInfinity;
        ++loadRevision;
        DestroyCachedAd();
        string recovery = sdkInitializationStarted && !sdkInitialized
            ? "Restart the app to retry."
            : "Retry from PRIVACY. You can keep playing.";
        string detail = Debug.isDebugBuild ? "\n" + ShortDiagnostic(exception.Message) : "";
        SetStatus("Ad setup failed (" + stage + "). " + recovery + detail);
        Debug.LogWarning("[Ads] Setup failed (" + stage + "): " + exception);
    }

    private static string ShortDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "No SDK error detail was provided.";
        string line = message.Split('\r', '\n')[0];
        return line.Length > 220 ? line.Substring(0, 220) + "..." : line;
    }

    private void LoadRewarded()
    {
        if (!adsAllowed || !sdkInitialized || loading || IsBusy || !ConsentInformation.CanRequestAds()) return;
        int revision = ++loadRevision;
        loading = true;
        nextLoadAt = double.PositiveInfinity;
        loadDeadline = Now + configuration.RequestTimeoutSeconds;
        SetStatus("Loading a rewarded ad...");
        var request = new AdRequest();
        request.Extras["npa"] = "1";
        request.Extras["rdp"] = "1";
        try
        {
            RewardedAd.Load(requestedUnitId, request, (ad, error) => Dispatch(() =>
            {
                if (this == null || !alive || revision != loadRevision || !adsAllowed)
                {
                    ad?.Destroy();
                    return;
                }
                loading = false;
                if (error != null || ad == null)
                {
                    ad?.Destroy();
                    ScheduleLoadRetry(error?.GetMessage() ?? "Ad source returned no ad.");
                    return;
                }
                rewardedAd = ad;
                adLoadedAt = Now;
                failedLoadCount = 0;
                LastLoadedAdapterClassName = "";
                if (Debug.isDebugBuild)
                {
                    try
                    {
                        LastLoadedAdapterClassName = ad.GetResponseInfo()?.GetMediationAdapterClassName() ?? "";
                        if (!string.IsNullOrEmpty(LastLoadedAdapterClassName))
                            LogDevelopmentDiagnostic("Loaded adapter: " + LastLoadedAdapterClassName + " (QA; verify test labels). CanShowAd=" + ad.CanShowAd());
                    }
                    catch (Exception exception)
                    {
                        // Optional diagnostics must not discard an otherwise usable ad.
                        Debug.LogWarning("[Ads] Response diagnostics unavailable: " + exception.Message);
                    }
                }
                SetStatus(ReadyStatusText + (string.IsNullOrEmpty(LastLoadedAdapterClassName)
                    ? "" : "\nLoaded adapter: " + LastLoadedAdapterClassName));
            }));
        }
        catch (Exception exception)
        {
            loading = false;
            ScheduleLoadRetry(exception.Message);
        }
    }

    private void ScheduleLoadRetry(string reason)
    {
        ++failedLoadCount;
        if (failedLoadCount <= configuration.MaximumLoadRetries)
        {
            // Unscaled clock, bounded exponential backoff. No recursive failure callback loop.
            double delay = Math.Min(120d, configuration.MinimumLoadRetrySeconds * Math.Pow(2d, failedLoadCount - 1));
            nextLoadAt = Now + delay;
            SetStatus("No ad available yet. Retrying later; gameplay remains available."
                + (Debug.isDebugBuild ? "\n" + ShortDiagnostic(reason) : ""));
        }
        else
        {
            nextLoadAt = double.PositiveInfinity;
            SetStatus("No ad available. Retry later from ad settings; gameplay remains available."
                + (Debug.isDebugBuild ? "\n" + ShortDiagnostic(reason) : ""));
        }
        Debug.LogWarning("[Ads] Rewarded load: " + reason);
    }

    /// <summary>Explicit user retry, still rate-limited; no request is made for blocked ages.</summary>
    public void RetryRewardedLoad()
    {
        if (!CanRetryAds) return;
        if (!sdkInitialized || !adsAllowed)
        {
            InitializeForAudience(Audience);
            return;
        }
        failedLoadCount = 0;
        nextLoadAt = Math.Min(nextLoadAt, Now + configuration.MinimumLoadRetrySeconds);
        SetStatus("Ad retry scheduled.");
    }

    /// <summary>
    /// Completion occurs after the native ad closes/fails, never just on reward earned.
    /// An owner is optional; when supplied, destroyed owners cannot receive a reward.
    /// </summary>
    public void ShowRewarded(Action<RewardedAdResult> completion, Object owner = null)
    {
        if (completion == null) return;
        if (!alive || !IsRewardedReady)
        {
            InvokeCompletion(completion, RewardedAdResult.Unavailable);
            return;
        }
        var request = new RewardRequest
        {
            Ad = rewardedAd,
            Completion = completion,
            Owner = owner,
            HasOwner = !ReferenceEquals(owner, null),
            StartedAt = Now,
            SceneHandle = SceneManager.GetActiveScene().handle,
            PreviousTimeScale = Time.timeScale,
            PreviousAudioPause = AudioListener.pause
        };
        rewardedAd = null; // A native rewarded object is single-use.
        LastAdFailureReason = "";
        activeRequest = request;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        request.OpenedHandler = () => Dispatch(() =>
        {
            if (!IsRequestCurrent(request)) return;
            request.Opened = true;
            LogDevelopmentDiagnostic("Native rewarded ad opened.");
        });
        request.ClosedHandler = () => Dispatch(() =>
        {
            if (!IsRequestCurrent(request) || request.Closed) return;
            request.Closed = true;
            LogDevelopmentDiagnostic("Native rewarded ad closed; earned=" + request.Earned);
            // Let callbacks posted in the same return-to-app batch settle before resolving.
            request.CompleteAfter = Now + (request.Earned ? 0.15d : CloseCallbackSettleSeconds);
        });
        request.FailedHandler = error => Dispatch(() =>
        {
            if (!IsRequestCurrent(request)) return;
            Debug.LogWarning("[Ads] Native show failed: " + error);
            FailRequest(request, "Native show failed: " + (error?.GetMessage() ?? "No SDK error detail."));
        });
        request.Ad.OnAdFullScreenContentOpened += request.OpenedHandler;
        request.Ad.OnAdFullScreenContentClosed += request.ClosedHandler;
        request.Ad.OnAdFullScreenContentFailed += request.FailedHandler;
        SetStatus(Debug.isDebugBuild
            ? configuration.UseConfiguredMediationTestUnitInDevelopment
                ? "Mediation QA: verify test labels and actual device registration."
                : "Showing a Google TEST ad — no revenue."
            : "Showing rewarded ad...");
        try
        {
            request.Ad.Show(reward => Dispatch(() =>
            {
                // Real earned callback is the sole native path to an earned reward.
                if (IsRequestCurrent(request) && !request.Cancelled)
                {
                    request.Earned = true;
                    LogDevelopmentDiagnostic("Native earned-reward callback received.");
                    // Some mediation sources post earned after closed. Keep a
                    // bounded settling window; never treat closed alone as earned.
                    if (request.Closed)
                        request.CompleteAfter = Math.Min(request.CompleteAfter, Now + 0.15d);
                }
            }));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Ads] Show: " + exception.Message);
            FailRequest(request, "Show exception: " + exception.Message);
        }
    }

    private void FailRequest(RewardRequest request, string reason)
    {
        if (!IsRequestCurrent(request)) return;
        LastAdFailureReason = ShortDiagnostic(reason);
        LogDevelopmentDiagnostic(LastAdFailureReason);
        FinishRequest(request, RewardedAdResult.Failed);
    }

    private bool IsRequestCurrent(RewardRequest request) => this != null && alive
        && !request.Completed && ReferenceEquals(activeRequest, request);

    /// <summary>Does not try to forcibly close native UI. A stale run never receives its reward.</summary>
    public void CancelRewardRequest(Object owner)
    {
        if (activeRequest == null || !ReferenceEquals(activeRequest.Owner, owner)) return;
        activeRequest.Cancelled = true;
    }

    private void FinishRequest(RewardRequest request, RewardedAdResult result)
    {
        if (request.Completed || !ReferenceEquals(activeRequest, request)) return;
        request.Completed = true;
        if (request.Cancelled || (request.HasOwner && request.Owner == null))
            result = RewardedAdResult.Cancelled;
#if UNITY_EDITOR
        else if (request.DevelopmentTest && result == RewardedAdResult.Earned)
            result = RewardedAdResult.DevelopmentRewardEarned;
#endif
        activeRequest = null;
        if (request.Ad != null)
        {
            request.Ad.OnAdFullScreenContentOpened -= request.OpenedHandler;
            request.Ad.OnAdFullScreenContentClosed -= request.ClosedHandler;
            request.Ad.OnAdFullScreenContentFailed -= request.FailedHandler;
            request.Ad.Destroy();
        }
        // Restore only the pause we own. Never force a paused game back to timeScale=1,
        // and don't overwrite a new scene's intentional time scale after cancellation.
        if (SceneManager.GetActiveScene().handle == request.SceneHandle && Time.timeScale == 0f)
            Time.timeScale = request.PreviousTimeScale;
        if (AudioListener.pause) AudioListener.pause = request.PreviousAudioPause;
        Action<RewardedAdResult> completion = request.Completion;
        request.Completion = null;
        request.Owner = null;
        request.Ad = null;
        if (alive && adsAllowed) nextLoadAt = Now + 1d;
        if (alive) SetStatus(result == RewardedAdResult.Earned ? "Reward earned. Loading the next ad..." : "Ad finished. You can play normally.");
        LogDevelopmentDiagnostic("Reward request completed: " + result);
        InvokeCompletion(completion, result);
    }

    /// <summary>Explicit development action: no SDK ad, no impression, no revenue.</summary>
    public void ShowDevelopmentReward(Action<RewardedAdResult> completion, Object owner = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (IsDevelopmentSimulationAvailable && (ReferenceEquals(owner, null) || owner != null))
        {
            Debug.Log("[Ads] DEVELOPMENT reward granted by explicit TEST action. NO AD / NO REVENUE.");
            InvokeCompletion(completion, RewardedAdResult.DevelopmentRewardEarned);
            return;
        }
#endif
        InvokeCompletion(completion, RewardedAdResult.Unavailable);
    }

    public void ShowPrivacyOptions(Action completion = null)
    {
        if (!alive || IsBusy || Audience != AdsAgeCategory.Adult18Plus || !PrivacyOptionsRequired)
        {
            completion?.Invoke();
            return;
        }
        int revision = ++consentRevision;
        ++loadRevision;
        loading = false;
        adsAllowed = false;
        nextLoadAt = double.PositiveInfinity;
        DestroyCachedAd(); // Never show an ad loaded using a previous privacy choice.
        privacyFormOpen = true;
        operationDeadline = double.PositiveInfinity;
        SetStatus("Privacy options are open...");
        try
        {
            ConsentForm.ShowPrivacyOptionsForm(error => Dispatch(() =>
            {
                if (!IsConsentOperationCurrent(revision)) return;
                privacyFormOpen = false;
                ConsentFinished(revision, error?.Message);
                completion?.Invoke();
            }));
        }
        catch (Exception exception)
        {
            privacyFormOpen = false;
            SetStatus("Privacy options unavailable. Ads remain disabled.");
            Debug.LogWarning("[Ads] Privacy options: " + exception.Message);
            completion?.Invoke();
        }
    }

    public void OpenAdInspector()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!alive || !sdkInitialized || IsBusy) return;
        try
        {
            MobileAds.OpenAdInspector(error => Dispatch(() =>
            {
                if (this == null || !alive) return;
                SetStatus(error == null ? "Ad inspector closed." : "Ad inspector unavailable: " + error.GetMessage());
            }));
        }
        catch (Exception exception)
        {
            SetStatus("Ad inspector unavailable: " + ShortDiagnostic(exception.Message));
            Debug.LogWarning("[Ads] Inspector: " + exception);
        }
#endif
    }

    // Explicit opt-in QA action. The native path is restricted to Google's demo unit;
    // no simulator, publisher unit or production build can use this shortcut.
    public void ShowGoogleDemoForDevelopment(Action<RewardedAdResult> completion, Object owner = null)
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (IsGoogleDemoDevelopmentMode && requestedUnitId == AdsConfiguration.AndroidTestRewardedId)
        {
            ShowRewarded(completion, owner);
            return;
        }
#endif
        InvokeCompletion(completion, RewardedAdResult.Unavailable);
    }

    private void UpdatePrivacyRequirement()
    {
        PrivacyOptionsRequired = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    }

    private string ReadyStatusText => !Debug.isDebugBuild ? "Rewarded ad ready."
        : configuration.UseConfiguredMediationTestUnitInDevelopment
            ? "Mediation QA ready: verify TEST labels; never click live ads."
            : "Google TEST ad ready — no revenue.";

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        TrackNativeForegroundReturn();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        TrackNativeForegroundReturn();
    }

    private void TrackNativeForegroundReturn()
    {
        if (activeRequest == null) return;
        if (applicationPaused || !applicationFocused)
        {
            activeRequest.SawNativeBackground = true;
            activeRequest.ReturnedToForegroundAt = -1d;
        }
        else if (activeRequest.SawNativeBackground)
            activeRequest.ReturnedToForegroundAt = Now;
    }

#if UNITY_EDITOR
    // Isolated regression seam, never present in an Android production player. Tests use
    // the same completion/cancellation/pause path without a native SDK or ad impression.
    // Even a synthetic earned signal produces DevelopmentRewardEarned, never Earned.
    internal void BeginDevelopmentCallbackTest(Action<RewardedAdResult> completion, Object owner)
    {
        if (!IsDevelopmentSimulationAvailable || completion == null) return;
        activeRequest = new RewardRequest
        {
            Completion = completion,
            Owner = owner,
            HasOwner = !ReferenceEquals(owner, null),
            StartedAt = Now,
            SceneHandle = SceneManager.GetActiveScene().handle,
            PreviousTimeScale = Time.timeScale,
            PreviousAudioPause = AudioListener.pause,
            DevelopmentTest = true
        };
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetStatus("REGRESSION TEST: synthetic callback lifecycle, no SDK / no revenue.");
    }

    internal void ReportDevelopmentCallbackTest(bool earned, bool closed, bool failed)
    {
        if (activeRequest == null || !activeRequest.DevelopmentTest) return;
        if (failed)
        {
            FinishRequest(activeRequest, RewardedAdResult.Failed);
            return;
        }
        activeRequest.Opened = true;
        if (earned && !activeRequest.Cancelled)
        {
            activeRequest.Earned = true;
            if (activeRequest.Closed)
                activeRequest.CompleteAfter = Math.Min(activeRequest.CompleteAfter, Now + 0.15d);
        }
        if (closed)
        {
            activeRequest.Closed = true;
            activeRequest.CompleteAfter = Now + (activeRequest.Earned ? 0.15d : CloseCallbackSettleSeconds);
        }
    }
#endif

    private void DestroyCachedAd()
    {
        rewardedAd?.Destroy();
        rewardedAd = null;
    }

    private void Dispatch(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            action();
        else if (mainThreadContext != null)
            mainThreadContext.Post(_ => action(), null);
        // A Unity player always has a Unity synchronization context in Awake.
        // Never execute Unity API work on an unknown background thread.
    }

    private void SetStatus(string text)
    {
        StatusText = text;
        LogDevelopmentDiagnostic("Status: " + text.Replace('\n', ' '));
        if (StateChanged == null) return;
        foreach (Action listener in StateChanged.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    // Bounded, development-only evidence when a USB profiler connection drops.
    // No credentials, exact age, consent strings or publisher IDs are written.
    private void LogDevelopmentDiagnostic(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Debug.isDebugBuild) return;
        Debug.Log("[Ads] " + message);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            developmentDiagnostics.Add(DateTime.UtcNow.ToString("O") + " " + ShortDiagnostic(message));
            if (developmentDiagnostics.Count > 64) developmentDiagnostics.RemoveAt(0);
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllLines(Path.Combine(Application.persistentDataPath, "ads-development-diagnostics.log"), developmentDiagnostics);
        }
        catch
        {
            // Optional diagnostics must never prevent gameplay or SDK callbacks.
        }
#endif
#endif
    }

    private static void InvokeCompletion(Action<RewardedAdResult> completion, RewardedAdResult result)
    {
        try { completion?.Invoke(result); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        alive = false;
        ++consentRevision;
        ++loadRevision;
        adsAllowed = false;
        if (activeRequest != null)
        {
            activeRequest.Cancelled = true;
            FinishRequest(activeRequest, RewardedAdResult.Cancelled);
        }
        DestroyCachedAd();
        StateChanged = null;
        Instance = null;
        if (ownsConfiguration && configuration != null) Destroy(configuration);
    }
}
