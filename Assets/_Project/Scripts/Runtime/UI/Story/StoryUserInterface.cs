using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoryUserInterface : MonoBehaviour
{
    [Header("Телефон")]
    [InspectorName("Применять конфигурацию при Enable")]
    [Tooltip("Если включено, StoryUserInterface при Awake/OnEnable передаёт PhoneDialogueUI все ссылки телефона, шаблоны сообщений и layout-настройки.")]
    [SerializeField] private bool _applyPhoneConfigurationOnEnable = true;

    [Header("Ссылки UI телефона")]
    [InspectorName("Ссылки UI телефона")]
    [Tooltip("Все scene-ссылки телефона: root, safe area, header, ScrollRect, Content, input bar и отдельные шаблоны incoming/outgoing/photo.")]
    [SerializeField] private PhoneDialogueUIReferences _phoneReferences = new PhoneDialogueUIReferences();

    [Header("Layout сообщений")]
    [InspectorName("Layout сообщений")]
    [Tooltip("Глобальные и шаблонные настройки расположения сообщений телефона: padding, ширина бабблов, offsets, имена и аватары.")]
    [SerializeField] private PhoneDialogueLayoutSettings _phoneLayoutSettings = new PhoneDialogueLayoutSettings();

    [Header("Предпросмотр телефона")]
    [InspectorName("Предпросмотр телефона")]
    [Tooltip("Настройки editor/runtime preview телефона: лимиты сообщений, очистка preview, скрытие story-персонажей и задержка печати.")]
    [SerializeField] private PhonePreviewSettings _phonePreviewSettings = new PhonePreviewSettings();

    [Header("End Screen")]
    [SerializeField] private bool _applyEndScreenConfigurationOnEnable = true;
    [SerializeField] private StoryEndScreenReferences _endScreenReferences = new StoryEndScreenReferences();
    [SerializeField] private StoryEndScreenLayoutSettings _endScreenLayoutSettings = new StoryEndScreenLayoutSettings();
    [SerializeField] private StoryEndScreenPreviewSettings _endScreenPreviewSettings = new StoryEndScreenPreviewSettings();
    [SerializeField] private StoryEndScreenStatBinding[] _endScreenStatBindings = StoryEndScreenStatBinding.CreateDefaults();

    public PhoneDialogueUIReferences PhoneReferences
    {
        get
        {
            EnsurePhoneSettings();
            return _phoneReferences;
        }
    }

    public PhoneDialogueLayoutSettings PhoneLayoutSettings
    {
        get
        {
            EnsurePhoneSettings();
            return _phoneLayoutSettings;
        }
    }

    public PhonePreviewSettings PhonePreviewSettings
    {
        get
        {
            EnsurePhoneSettings();
            return _phonePreviewSettings;
        }
    }

    public StoryEndScreenReferences EndScreenReferences
    {
        get
        {
            EnsureEndScreenSettings();
            return _endScreenReferences;
        }
    }

    public StoryEndScreenLayoutSettings EndScreenLayoutSettings
    {
        get
        {
            EnsureEndScreenSettings();
            return _endScreenLayoutSettings;
        }
    }

    public StoryEndScreenPreviewSettings EndScreenPreviewSettings
    {
        get
        {
            EnsureEndScreenSettings();
            return _endScreenPreviewSettings;
        }
    }

    public StoryEndScreenStatBinding[] EndScreenStatBindings
    {
        get
        {
            EnsureEndScreenSettings();
            return _endScreenStatBindings;
        }
    }

    void Awake()
    {
        EnsurePhoneSettings();
        EnsureEndScreenSettings();
        if (_applyPhoneConfigurationOnEnable)
            ApplyPhoneConfiguration(nameof(Awake));
        if (_applyEndScreenConfigurationOnEnable)
            ApplyEndScreenConfiguration(nameof(Awake));
    }

    void OnEnable()
    {
        EnsurePhoneSettings();
        EnsureEndScreenSettings();
        if (_applyPhoneConfigurationOnEnable)
            ApplyPhoneConfiguration(nameof(OnEnable));
        if (_applyEndScreenConfigurationOnEnable)
            ApplyEndScreenConfiguration(nameof(OnEnable));
    }

    void OnValidate()
    {
        EnsurePhoneSettings();
        EnsureEndScreenSettings();
    }

    public PhoneDialogueUI ResolvePhoneDialogueUI()
    {
        EnsurePhoneSettings();
        if (_phoneReferences.phoneDialogueUI != null)
            return _phoneReferences.phoneDialogueUI;

        PhoneDialogueUI local = GetComponentInChildren<PhoneDialogueUI>(true);
        if (local != null)
            return local;

        PhoneDialogueUI parent = GetComponentInParent<PhoneDialogueUI>(true);
        if (parent != null)
            return parent;

        return FindObjectOfType<PhoneDialogueUI>(true);
    }

    public StoryEndScreenController ResolveEndScreenController()
    {
        EnsureEndScreenSettings();

        if (_endScreenReferences.root != null)
        {
            StoryEndScreenController rooted = _endScreenReferences.root.GetComponentInChildren<StoryEndScreenController>(true);
            if (rooted != null)
                return rooted;
        }

        StoryEndScreenController local = GetComponentInChildren<StoryEndScreenController>(true);
        if (local != null)
            return local;

        StoryEndScreenController parent = GetComponentInParent<StoryEndScreenController>(true);
        if (parent != null)
            return parent;

        return FindObjectOfType<StoryEndScreenController>(true);
    }

    public bool ApplyPhoneConfiguration(string reason = "Manual")
    {
        EnsurePhoneSettings();
        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        if (phoneUi == null)
        {
            AppLogger.Warn(
                AppLogCategory.PhoneDialogue,
                nameof(StoryUserInterface),
                nameof(ApplyPhoneConfiguration),
                "PhoneDialogueUI не найден. Конфигурация телефона не применена.",
                LogMetadata.Of("owner", name, "reason", reason));
            return false;
        }

        _phoneReferences.phoneDialogueUI = phoneUi;
        phoneUi.ConfigureFromStoryUserInterface(this, reason);
        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(StoryUserInterface),
            nameof(ApplyPhoneConfiguration),
            "Конфигурация телефона применена из StoryUserInterface.",
            LogMetadata.Of("owner", name, "phone", phoneUi.name, "reason", reason));
        return true;
    }

    public bool ApplyEndScreenConfiguration(string reason = "Manual")
    {
        EnsureEndScreenSettings();
        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller == null)
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryUserInterface),
                nameof(ApplyEndScreenConfiguration),
                "StoryEndScreenController was not found. End screen configuration was not applied.",
                LogMetadata.Of("owner", name, "reason", reason));
            return false;
        }

        controller.ConfigureFromStoryUserInterface(this, reason);
        AppLogger.Info(
            AppLogCategory.EndScreen,
            nameof(StoryUserInterface),
            nameof(ApplyEndScreenConfiguration),
            "End screen configuration applied from StoryUserInterface.",
            LogMetadata.Of("owner", name, "controller", controller.name, "reason", reason));
        return true;
    }

    public void AutoFillPhoneReferences(bool overwrite = false)
    {
        EnsurePhoneSettings();
        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        if (phoneUi == null)
        {
            AppLogger.Warn(
                AppLogCategory.PhoneDialogue,
                nameof(StoryUserInterface),
                nameof(AutoFillPhoneReferences),
                "Автозаполнение телефона невозможно: PhoneDialogueUI не найден.",
                LogMetadata.Of("owner", name));
            return;
        }

        _phoneReferences.AutoFillFrom(phoneUi, overwrite);
        ApplyPhoneConfiguration(nameof(AutoFillPhoneReferences));
    }

    public void AutoFillEndScreenReferences(bool overwrite = false)
    {
        EnsureEndScreenSettings();
        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller == null)
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryUserInterface),
                nameof(AutoFillEndScreenReferences),
                "End screen auto-fill skipped because StoryEndScreenController was not found.",
                LogMetadata.Of("owner", name));
            return;
        }

        if (overwrite)
            _endScreenReferences = new StoryEndScreenReferences();

        controller.ConfigureFromStoryUserInterface(this, nameof(AutoFillEndScreenReferences));
        controller.AutoFillEndScreenReferencesFromHierarchy();
    }

    public bool MigratePhoneReferencesFromLegacyPhoneDialogueUI(bool overwrite = false)
    {
        EnsurePhoneSettings();
        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        if (phoneUi == null)
        {
            AppLogger.Warn(
                AppLogCategory.PhoneDialogue,
                nameof(StoryUserInterface),
                nameof(MigratePhoneReferencesFromLegacyPhoneDialogueUI),
                "Миграция телефона невозможна: PhoneDialogueUI не найден.",
                LogMetadata.Of("owner", name));
            return false;
        }

        phoneUi.CopySerializedConfigurationTo(_phoneReferences, _phoneLayoutSettings, _phonePreviewSettings, overwrite);
        _phoneReferences.AutoFillFrom(phoneUi, overwrite);
        ApplyPhoneConfiguration(nameof(MigratePhoneReferencesFromLegacyPhoneDialogueUI));
        AppLogger.Info(
            AppLogCategory.PhoneDialogue,
            nameof(StoryUserInterface),
            nameof(MigratePhoneReferencesFromLegacyPhoneDialogueUI),
            "Ссылки телефона мигрированы из legacy PhoneDialogueUI в StoryUserInterface.",
            LogMetadata.Of("owner", name, "phone", phoneUi.name, "overwrite", overwrite));
        return true;
    }

    public bool MigrateEndScreenReferencesFromLegacyController(bool overwrite = false)
    {
        EnsureEndScreenSettings();
        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller == null)
        {
            AppLogger.Warn(
                AppLogCategory.EndScreen,
                nameof(StoryUserInterface),
                nameof(MigrateEndScreenReferencesFromLegacyController),
                "End screen migration skipped because StoryEndScreenController was not found.",
                LogMetadata.Of("owner", name));
            return false;
        }

        controller.AutoFillEndScreenReferencesFromHierarchy();
        _endScreenStatBindings = controller.CopySerializedConfigurationTo(
            _endScreenReferences,
            _endScreenLayoutSettings,
            _endScreenPreviewSettings,
            _endScreenStatBindings,
            overwrite);
        ApplyEndScreenConfiguration(nameof(MigrateEndScreenReferencesFromLegacyController));
        return true;
    }

    public PhonePreviewValidationResult ValidatePhoneReferences(PhoneDialogueNode node = null, bool requireMessages = false)
    {
        ApplyPhoneConfiguration(nameof(ValidatePhoneReferences));
        return PhonePreviewValidator.Validate(ResolvePhoneDialogueUI(), node, requireMessages);
    }

    public StoryEndScreenValidationResult ValidateEndScreen(bool requireRuntime = false)
    {
        ApplyEndScreenConfiguration(nameof(ValidateEndScreen));
        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller != null)
            return controller.ValidateEndScreen(requireRuntime);

        var result = new StoryEndScreenValidationResult();
        result.Error("StoryEndScreenController was not found.");
        return result;
    }

    public bool ShowPhonePreview(PhoneDialogueNode node, string reason = "StoryUserInterfacePreview")
    {
        if (!ApplyPhoneConfiguration(reason))
            return false;

        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        return Application.isPlaying
            ? new PhoneDialogueRuntimePlayer().Play(phoneUi, node, null)
            : new PhoneDialogueEditorPreviewRenderer().Render(phoneUi, node, reason);
    }

    public bool ShowEndScreenPreview(string reason = "StoryUserInterfacePreview")
    {
        if (!ApplyEndScreenConfiguration(reason))
            return false;

        StoryEndScreenController controller = ResolveEndScreenController();
        return controller != null && controller.ShowStaticPreview(reason);
    }

    public void ClearPhonePreview()
    {
        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        if (phoneUi != null)
            phoneUi.Hide();
    }

    public void ClearEndScreenPreview()
    {
        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller != null)
            controller.Hide();
    }

    public int RecalculatePhoneLayout(string reason = "StoryUserInterface")
    {
        if (!ApplyPhoneConfiguration(reason))
            return -1;

        PhoneDialogueUI phoneUi = ResolvePhoneDialogueUI();
        return phoneUi != null ? phoneUi.RecalculateLayout(reason) : -1;
    }

    public bool RecalculateEndScreenLayout(string reason = "StoryUserInterface")
    {
        if (!ApplyEndScreenConfiguration(reason))
            return false;

        StoryEndScreenController controller = ResolveEndScreenController();
        if (controller == null)
            return false;

        controller.RecalculateLayout(reason);
        return true;
    }

    void EnsurePhoneSettings()
    {
        if (_phoneReferences == null)
            _phoneReferences = new PhoneDialogueUIReferences();
        if (_phoneLayoutSettings == null)
            _phoneLayoutSettings = new PhoneDialogueLayoutSettings();
        if (_phonePreviewSettings == null)
            _phonePreviewSettings = new PhonePreviewSettings();

        _phoneReferences.Ensure();
        _phoneLayoutSettings.Normalize();
        _phonePreviewSettings.Normalize();
    }

    void EnsureEndScreenSettings()
    {
        if (_endScreenReferences == null)
            _endScreenReferences = new StoryEndScreenReferences();
        if (_endScreenLayoutSettings == null)
            _endScreenLayoutSettings = new StoryEndScreenLayoutSettings();
        if (_endScreenPreviewSettings == null)
            _endScreenPreviewSettings = new StoryEndScreenPreviewSettings();
        if (_endScreenStatBindings == null || _endScreenStatBindings.Length == 0)
            _endScreenStatBindings = StoryEndScreenStatBinding.CreateDefaults();
    }
}
