using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A neutral, local-only age decision before any ad SDK/consent operation.
/// The game remains playable for every choice; age is not consent or a legal
/// determination. Only the coarse category and decision year are persisted.
/// </summary>
public sealed class AdsPrivacyPanel : MonoBehaviour
{
    private const string AgeCategoryPreference = "blokkipop.ads.age-category.v1";
    private const string AgeYearPreference = "blokkipop.ads.age-year.v1";
    private static readonly Color CardColor = new(0.06f, 0.13f, 0.18f, 1f);
    private static readonly Color TextColor = new(0.93f, 0.97f, 0.96f, 1f);
    private static readonly Color MutedColor = new(0.70f, 0.81f, 0.83f, 1f);
    private static readonly Color ButtonColor = new(0.08f, 0.45f, 0.45f, 1f);

    public static AdsPrivacyPanel Instance { get; private set; }
    public static bool IsModalOpen => Instance != null && Instance.modal != null && Instance.modal.activeSelf;

    /// <summary>
    /// The Home screen also reads raw pointer input. A GraphicRaycaster alone
    /// cannot stop that path from treating a PRIVACY-button press as Start.
    /// </summary>
    public static bool ShouldBlockStartInput(Vector2 screenPoint)
    {
        if (IsModalOpen)
            return true;
        return Instance != null && Instance.privacyButton != null &&
            Instance.privacyButton.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)Instance.privacyButton.transform, screenPoint, null);
    }

    private AdsService subscribedService;
    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private RectTransform ageCard;
    private RectTransform privacyCard;
    private GameObject modal;
    private Button privacyButton;
    private Button policyButton;
    private Button consentButton;
    private Button changeAgeButton;
    private Button retryAdsButton;
    private Button inspectorButton;
    private Button continueButton;
    private TMP_InputField ageInput;
    private TextMeshProUGUI validationText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI policyNote;
    private TextMeshProUGUI changeAgeLabel;
    private TMP_FontAsset font;
    private AdsAgeCategory currentCategory;
    private int decisionYear;
    private bool hasDecision;
    private bool restartRequired;
    private string localStatus = "";
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float nextYearCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("Ads Privacy UI", typeof(RectTransform)).AddComponent<AdsPrivacyPanel>();
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
        font = TMP_Settings.defaultFontAsset;
        hasDecision = TryReadDecision(out currentCategory, out decisionYear);
        BuildVisuals();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        RefreshHomeButton();
        if (hasDecision || PopGameplaySession.Instance != null && !PopGameplaySession.Instance.Configuration.runs.promptForAdAgeOnLaunch)
            modal.SetActive(false);
        else
            ShowAgePrompt();
    }

    private IEnumerator Start()
    {
        // AdsService bootstraps BeforeSceneLoad. Waiting also keeps this UI safe
        // if script execution order changes; no fallback initializes an SDK.
        while (AdsService.Instance == null)
            yield return null;
        subscribedService = AdsService.Instance;
        subscribedService.StateChanged += RefreshPrivacyMenu;
        if (hasDecision)
            subscribedService.InitializeForAudience(currentCategory);
        RefreshPrivacyMenu();
    }

    private void Update()
    {
        ApplySafeArea();
        if (Time.unscaledTime < nextYearCheck)
            return;
        nextYearCheck = Time.unscaledTime + 1f;
        if (hasDecision && !restartRequired && decisionYear != DateTime.Now.Year &&
            subscribedService != null && !subscribedService.IsBusy)
            BeginAgeChange(true);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (!ReferenceEquals(subscribedService, null))
            subscribedService.StateChanged -= RefreshPrivacyMenu;
        if (Instance == this)
            Instance = null;
    }

    private static bool TryReadDecision(out AdsAgeCategory category, out int year)
    {
        category = AdsAgeCategory.Unknown;
        year = PlayerPrefs.GetInt(AgeYearPreference, -1);
        if (!PlayerPrefs.HasKey(AgeCategoryPreference) || year != DateTime.Now.Year)
            return false;
        int rawCategory = PlayerPrefs.GetInt(AgeCategoryPreference, -1);
        if (!Enum.IsDefined(typeof(AdsAgeCategory), rawCategory))
            return false;
        category = (AdsAgeCategory)rawCategory;
        return true;
    }

    private void SubmitAge()
    {
        if (subscribedService == null || restartRequired || subscribedService.IsBusy)
            return;
        if (!int.TryParse(ageInput.text, out int age) || age < 0 || age > 120)
        {
            validationText.text = "Enter your age as a whole number, or choose Prefer not to say.";
            return;
        }
        AdsAgeCategory category = age < 13 ? AdsAgeCategory.Under13
            : age < 18 ? AdsAgeCategory.Teen13To17 : AdsAgeCategory.Adult18Plus;
        CompleteAgeDecision(category);
    }

    private void PreferNotToSay()
    {
        if (subscribedService == null || restartRequired || subscribedService.IsBusy)
            return;
        CompleteAgeDecision(AdsAgeCategory.Unknown);
    }

    private void CompleteAgeDecision(AdsAgeCategory category)
    {
        // Discard the exact age immediately. It is never a PlayerPrefs value,
        // analytics event, account field, or SDK request parameter.
        ageInput.DeactivateInputField();
        ageInput.SetTextWithoutNotify("");
        currentCategory = category;
        decisionYear = DateTime.Now.Year;
        hasDecision = true;
        PlayerPrefs.SetInt(AgeCategoryPreference, (int)category);
        PlayerPrefs.SetInt(AgeYearPreference, decisionYear);
        PlayerPrefs.Save();
        modal.SetActive(false);
        subscribedService.InitializeForAudience(category);
    }

    private void ShowAgePrompt()
    {
        localStatus = "";
        ageInput.SetTextWithoutNotify("");
        validationText.text = "";
        ageCard.gameObject.SetActive(true);
        privacyCard.gameObject.SetActive(false);
        modal.SetActive(true);
        ApplySafeArea(true);
        // Do not preselect an age or open the keyboard without a user tap.
    }

    public void OpenPrivacyMenu()
    {
        if (!hasDecision && !restartRequired)
        {
            ShowAgePrompt();
            return;
        }
        ageInput.DeactivateInputField();
        ageInput.SetTextWithoutNotify("");
        ageCard.gameObject.SetActive(false);
        privacyCard.gameObject.SetActive(true);
        modal.SetActive(true);
        RefreshPrivacyMenu();
        ApplySafeArea(true);
    }

    private void ClosePrivacyMenu()
    {
        // Closing our menu does not cancel a native UMP form or ad operation.
        // A slow network check must not trap the player in this local panel.
        modal.SetActive(false);
    }

    private void OpenPolicy()
    {
        AdsConfiguration config = subscribedService != null ? subscribedService.Configuration : null;
        if (config != null && config.HasValidPrivacyPolicyUrl)
            Application.OpenURL(config.PrivacyPolicyUrl);
    }

    private void OpenConsentOptions()
    {
        if (subscribedService == null || !subscribedService.PrivacyOptionsRequired || subscribedService.IsBusy)
            return;
        localStatus = "";
        subscribedService.ShowPrivacyOptions(RefreshPrivacyMenu);
        RefreshPrivacyMenu();
    }

    private void OpenDevelopmentAdTools()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (subscribedService == null || subscribedService.IsBusy) return;
        localStatus = "";
        if (!subscribedService.IsGoogleDemoDevelopmentMode)
        {
            subscribedService.OpenAdInspector();
            return;
        }
        subscribedService.ShowGoogleDemoForDevelopment(result =>
        {
            if (this == null) return;
            localStatus = "Google TEST ad result: " + result + ".\nNo revenue; no board revive."
                + (result == RewardedAdResult.Failed && subscribedService != null
                    ? "\n" + subscribedService.LastAdFailureReason : "");
            RefreshPrivacyMenu();
        }, this);
#endif
    }

    private void BeginAgeChange(bool calendarYearChanged)
    {
        if (subscribedService == null || subscribedService.IsBusy || restartRequired)
            return;
        bool needsRestart = subscribedService.HasStartedSdkInitialization;
        PlayerPrefs.DeleteKey(AgeCategoryPreference);
        PlayerPrefs.DeleteKey(AgeYearPreference);
        PlayerPrefs.Save();
        hasDecision = false;
        currentCategory = AdsAgeCategory.Unknown;
        // This only disables current ads/invalidates callbacks. Once a native
        // SDK has started, it cannot be undone/reconfigured safely in-process.
        subscribedService.InitializeForAudience(AdsAgeCategory.Unknown);
        if (!needsRestart)
        {
            ShowAgePrompt();
            return;
        }
        restartRequired = true;
        localStatus = (calendarYearChanged ? "A new calendar year requires a fresh age decision.\n" : "Saved age range cleared.\n") +
            "Ads are off. Fully close and reopen the app to choose again. You can keep playing now.";
        OpenPrivacyMenu();
    }

    private void RefreshPrivacyMenu()
    {
        if (statusText == null)
            return;
        bool available = subscribedService != null;
        bool busy = available && subscribedService.IsBusy;
        AdsConfiguration config = available ? subscribedService.Configuration : null;
        statusText.text = string.IsNullOrEmpty(localStatus)
            ? $"Saved age range: {CategoryLabel(currentCategory)}\n\n" +
                (available ? subscribedService.StatusText : "Preparing privacy settings...")
            : localStatus + (available ? "\n\n" + subscribedService.StatusText : "");
        policyButton.interactable = config != null && config.HasValidPrivacyPolicyUrl;
        policyNote.text = policyButton.interactable
            ? "Your age range is stored only on this device. Age is not consent."
            : "Privacy policy link is not configured yet. Live ads remain disabled.";
        consentButton.gameObject.SetActive(available && subscribedService.PrivacyOptionsRequired);
        consentButton.interactable = available && !busy;
        changeAgeButton.interactable = available && !busy && !restartRequired;
        changeAgeLabel.text = available && subscribedService.HasStartedSdkInitialization
            ? "CHANGE AGE (RESTART REQUIRED)" : "CHANGE AGE RANGE";
        continueButton.interactable = available && !busy && !restartRequired;
        if (retryAdsButton != null)
        {
            retryAdsButton.gameObject.SetActive(available && currentCategory == AdsAgeCategory.Adult18Plus && !restartRequired);
            retryAdsButton.interactable = available && subscribedService.CanRetryAds;
        }
        if (inspectorButton != null)
        {
            inspectorButton.gameObject.SetActive(available && subscribedService.IsInitialized);
            bool googleDemo = available && subscribedService.IsGoogleDemoDevelopmentMode;
            inspectorButton.GetComponentInChildren<TextMeshProUGUI>().text = googleDemo ? "TEST GOOGLE AD" : "AD INSPECTOR (TEST)";
            inspectorButton.interactable = available && !busy && (!googleDemo || subscribedService.IsRewardedReady);
        }
    }

    private static string CategoryLabel(AdsAgeCategory category)
    {
        switch (category)
        {
            case AdsAgeCategory.Under13: return "Under 13";
            case AdsAgeCategory.Teen13To17: return "13-17";
            case AdsAgeCategory.Adult18Plus: return "18+";
            default: return "Not specified";
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshHomeButton();
    }

    private void RefreshHomeButton()
    {
        if (privacyButton != null)
            privacyButton.gameObject.SetActive(SceneManager.GetActiveScene().buildIndex == 0 && PopGameplaySession.Instance == null);
    }

    private void BuildVisuals()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        safeAreaRoot = MakeRect("Safe Area", transform);
        Stretch(safeAreaRoot);
        privacyButton = MakeButton("Privacy Shortcut", safeAreaRoot, "PRIVACY", Vector2.zero, new Vector2(240f, 76f), 26f);
        RectTransform shortcut = (RectTransform)privacyButton.transform;
        shortcut.anchorMin = shortcut.anchorMax = Vector2.zero;
        shortcut.pivot = Vector2.zero;
        shortcut.anchoredPosition = new Vector2(24f, 24f);
        privacyButton.onClick.AddListener(OpenPrivacyMenu);

        RectTransform overlay = MakeRect("Privacy Modal", transform);
        Stretch(overlay);
        Image dimmer = overlay.gameObject.AddComponent<Image>();
        dimmer.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);
        dimmer.raycastTarget = true;
        modal = overlay.gameObject;
        RectTransform modalSafeArea = MakeRect("Modal Safe Area", overlay);
        Stretch(modalSafeArea);
        // Share the same safe-area anchors for both visible controls and cards.
        ageCard = MakeCard("Age Decision", modalSafeArea, new Vector2(860f, 1060f));
        privacyCard = MakeCard("Privacy Settings", modalSafeArea, new Vector2(860f, 1340f));

        MakeText("Age Title", ageCard, "YOUR AGE", new Vector2(0f, 400f), new Vector2(740f, 80f), 46f);
        MakeText("Age Explanation", ageCard,
            "Please enter your age so we can apply the right privacy settings.\n\nWe save only an age range on this device, not your age or birth date.",
            new Vector2(0f, 240f), new Vector2(740f, 180f), 27f);
        MakeText("Age Label", ageCard, "Age in years", new Vector2(0f, 110f), new Vector2(740f, 50f), 28f);
        ageInput = MakeAgeInput(ageCard, new Vector2(0f, 25f));
        validationText = MakeText("Age Validation", ageCard, "", new Vector2(0f, -70f), new Vector2(740f, 80f), 23f);
        validationText.color = new Color(1f, 0.79f, 0.43f, 1f);
        continueButton = MakeButton("Confirm Age", ageCard, "CONTINUE", new Vector2(0f, -170f), new Vector2(740f, 90f), 30f);
        continueButton.onClick.AddListener(SubmitAge);
        Button skip = MakeButton("Skip Age", ageCard, "PREFER NOT TO SAY", new Vector2(0f, -285f), new Vector2(740f, 90f), 29f);
        skip.onClick.AddListener(PreferNotToSay);
        MakeText("Age Footer", ageCard, "You can play regardless of your choice.\nThis choice does not replace privacy consent.",
            new Vector2(0f, -405f), new Vector2(740f, 100f), 24f).color = MutedColor;

        MakeText("Privacy Title", privacyCard, "PRIVACY", new Vector2(0f, 465f), new Vector2(740f, 80f), 46f);
        MakeText("Privacy Explanation", privacyCard,
            "Manage the age range saved on this device and any available ad privacy choices. Exact age and birth date are not stored.",
            new Vector2(0f, 345f), new Vector2(740f, 135f), 26f).color = MutedColor;
        statusText = MakeText("Ads Status", privacyCard, "", new Vector2(0f, 165f), new Vector2(740f, 220f), 22f);
        policyButton = MakeButton("Privacy Policy", privacyCard, "PRIVACY POLICY", new Vector2(0f, -20f), new Vector2(740f, 90f), 29f);
        policyButton.onClick.AddListener(OpenPolicy);
        consentButton = MakeButton("Ad Privacy Choices", privacyCard, "AD PRIVACY CHOICES", new Vector2(0f, -135f), new Vector2(740f, 90f), 29f);
        consentButton.onClick.AddListener(OpenConsentOptions);
        changeAgeButton = MakeButton("Change Age", privacyCard, "CHANGE AGE RANGE", new Vector2(0f, -245f), new Vector2(740f, 90f), 27f);
        changeAgeLabel = changeAgeButton.GetComponentInChildren<TextMeshProUGUI>();
        changeAgeButton.onClick.AddListener(() => BeginAgeChange(false));
        retryAdsButton = MakeButton("Retry Ad Loading", privacyCard, "RETRY AD LOADING", new Vector2(0f, -350f), new Vector2(740f, 80f), 25f);
        retryAdsButton.onClick.AddListener(() =>
        {
            localStatus = "";
            subscribedService?.RetryRewardedLoad();
            RefreshPrivacyMenu();
        });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ((RectTransform)retryAdsButton.transform).anchoredPosition = new Vector2(-190f, -350f);
        ((RectTransform)retryAdsButton.transform).sizeDelta = new Vector2(360f, 80f);
        retryAdsButton.GetComponentInChildren<TextMeshProUGUI>().rectTransform.sizeDelta = new Vector2(330f, 68f);
        inspectorButton = MakeButton("Development Ad Inspector", privacyCard, "AD INSPECTOR (TEST)", new Vector2(190f, -350f), new Vector2(360f, 80f), 23f);
        inspectorButton.onClick.AddListener(OpenDevelopmentAdTools);
#endif
        Button close = MakeButton("Close Privacy", privacyCard, "CLOSE", new Vector2(0f, -455f), new Vector2(740f, 90f), 29f);
        close.onClick.AddListener(ClosePrivacyMenu);
        policyNote = MakeText("Privacy Footer", privacyCard, "", new Vector2(0f, -585f), new Vector2(740f, 105f), 22f);
        policyNote.color = MutedColor;
        privacyCard.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        ApplySafeArea(true);
    }

    private void ApplySafeArea(bool force = false)
    {
        Vector2Int size = new(Screen.width, Screen.height);
        Rect area = Screen.safeArea;
        if (size.x <= 0 || size.y <= 0 || area.width <= 0f || area.height <= 0f)
            return;
        if (!force && size == lastScreenSize && area == lastSafeArea)
            return;
        lastScreenSize = size;
        lastSafeArea = area;
        Vector2 anchorMin = new(area.xMin / size.x, area.yMin / size.y);
        Vector2 anchorMax = new(area.xMax / size.x, area.yMax / size.y);
        safeAreaRoot.anchorMin = anchorMin;
        safeAreaRoot.anchorMax = anchorMax;
        safeAreaRoot.offsetMin = safeAreaRoot.offsetMax = Vector2.zero;
        RectTransform modalSafeArea = (RectTransform)ageCard.parent;
        modalSafeArea.anchorMin = anchorMin;
        modalSafeArea.anchorMax = anchorMax;
        modalSafeArea.offsetMin = modalSafeArea.offsetMax = Vector2.zero;
        float availableWidth = area.width / Mathf.Max(0.01f, canvas.scaleFactor);
        float availableHeight = area.height / Mathf.Max(0.01f, canvas.scaleFactor);
        ScaleCard(ageCard, availableWidth, availableHeight);
        ScaleCard(privacyCard, availableWidth, availableHeight);
    }

    private static void ScaleCard(RectTransform card, float width, float height)
    {
        float scale = Mathf.Min(1f, Mathf.Min(width / (card.sizeDelta.x + 48f), height / (card.sizeDelta.y + 48f)));
        card.localScale = Vector3.one * Mathf.Max(0.1f, scale);
    }

    private TMP_InputField MakeAgeInput(RectTransform parent, Vector2 position)
    {
        RectTransform root = MakeRect("Age Input", parent);
        SetCentered(root, position, new Vector2(740f, 90f));
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.11f, 0.22f, 0.28f, 1f);
        TMP_InputField field = root.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = background;
        RectTransform viewport = MakeRect("Text Area", root);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(22f, 12f);
        viewport.offsetMax = new Vector2(-22f, -12f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = MakeText("Age Input Text", viewport, "", Vector2.zero, Vector2.zero, 36f);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TextMeshProUGUI placeholder = MakeText("Age Placeholder", viewport, "Enter age", Vector2.zero, Vector2.zero, 30f);
        Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = MutedColor;
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.keyboardType = TouchScreenKeyboardType.NumberPad;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 3;
        field.caretColor = TextColor;
        field.customCaretColor = true;
        field.SetTextWithoutNotify("");
        return field;
    }

    private static RectTransform MakeRect(string objectName, Transform parent)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    private static RectTransform MakeCard(string objectName, RectTransform parent, Vector2 size)
    {
        RectTransform card = MakeRect(objectName, parent);
        SetCentered(card, Vector2.zero, size);
        Image image = card.gameObject.AddComponent<Image>();
        image.color = CardColor;
        image.raycastTarget = true;
        return card;
    }

    private Button MakeButton(string objectName, RectTransform parent, string label, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = MakeRect(objectName, parent);
        SetCentered(rect, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.83f, 1f, 0.96f, 1f);
        colors.pressedColor = new Color(0.58f, 0.79f, 0.75f, 1f);
        colors.disabledColor = new Color(0.45f, 0.51f, 0.55f, 0.65f);
        button.colors = colors;
        TextMeshProUGUI caption = MakeText("Label", rect, label, Vector2.zero, size - new Vector2(30f, 12f), fontSize);
        caption.fontStyle = FontStyles.Bold;
        return button;
    }

    private TextMeshProUGUI MakeText(string objectName, RectTransform parent, string content, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = MakeRect(objectName, parent);
        SetCentered(rect, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = TextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
