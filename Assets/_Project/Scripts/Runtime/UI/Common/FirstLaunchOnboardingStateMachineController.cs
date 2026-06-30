using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum FirstLaunchOnboardingState
{
    Idle = 0,
    CatIntro = 1,
    Terms = 2,
    ProgressWarning = 3,
    Complete = 4,
    SystemMessage = 5,
    NoConnection = 6,
    UpdateInfo = 7
}

public enum FirstLaunchOnboardingButtonAction
{
    Next = 0,
    Complete = 1,
    OpenAuthAndComplete = 2
}

public enum FirstLaunchOnboardingPanelMotion
{
    None = 0,
    PopupScale = 1,
    SlideDown = 2
}

[Serializable]
public sealed class FirstLaunchOnboardingStateChangedEvent : UnityEvent<FirstLaunchOnboardingState>
{
}

[Serializable]
public sealed class FirstLaunchOnboardingStep
{
    [SerializeField]
    [Tooltip("Включён ли этот шаг в общей цепочке первого запуска.")]
    private bool _enabled = true;

    [SerializeField]
    [Tooltip("Состояние state-machine для этого шага. Нужно для логов, событий и понятной отладки.")]
    private FirstLaunchOnboardingState _state = FirstLaunchOnboardingState.CatIntro;

    [SerializeField]
    [Tooltip("Стабильный ключ шага для сохранения просмотра. Например: cat_intro, terms, progress_warning.")]
    private string _stepKey = "cat_intro";

    [SerializeField, Min(1)]
    [Tooltip("Ревизия текста. Увеличь число, если нужно показать этот шаг игрокам заново.")]
    private int _revision = 1;

    [SerializeField]
    [Tooltip("Текст маленького заголовка. Если отдельного Title TMP нет, можно оставить пустым и написать всё в Body Text.")]
    private string _title = "Мяу...";

    [SerializeField, TextArea(3, 14)]
    [Tooltip("Основной TMP-текст шага. Можно использовать rich text TMP и переносы строк.")]
    private string _body = "Я — твой мохнатый Проводник\nв Nocturne — в мире, где каждый\nвыбор меняет ход твоей\nистории. Свечи здесь освещают\nпуть, а Рубины зажигают\nмоменты, которые меняют всё...";

    [SerializeField]
    [Tooltip("Текст основной кнопки. Это та же самая кнопка на всех шагах.")]
    private string _primaryButtonText = "Понятно";

    [SerializeField]
    [Tooltip("Что делает основная кнопка на этом шаге: перейти дальше, завершить цепочку или открыть авторизацию.")]
    private FirstLaunchOnboardingButtonAction _primaryAction = FirstLaunchOnboardingButtonAction.Next;

    [SerializeField]
    [Tooltip("Показывать ли вторую кнопку на этом шаге. Для кота и условий обычно выключено, для прогресса включено.")]
    private bool _showSecondaryButton;

    [SerializeField]
    [Tooltip("Текст второй кнопки. Это та же самая вторая кнопка, просто с другим текстом.")]
    private string _secondaryButtonText = "Продолжить так";

    [SerializeField]
    [Tooltip("Что делает вторая кнопка на этом шаге.")]
    private FirstLaunchOnboardingButtonAction _secondaryAction = FirstLaunchOnboardingButtonAction.Complete;

    [Header("Размеры этого шага")]
    [SerializeField]
    [Tooltip("Включи, если на этом шаге нужно задать свою ширину и высоту общей панели Panel Root.")]
    private bool _overridePanelSize;

    [SerializeField]
    [Tooltip("Ширина и высота Panel Root для этого шага. Работает только если включен Override Panel Size.")]
    private Vector2 _panelSize = new Vector2(1000f, 700f);

    [SerializeField]
    [Tooltip("Показывать фон заголовка Title Background на этом шаге.")]
    private bool _showTitleBackground = true;

    [SerializeField]
    [Tooltip("Включи, если на этом шаге нужно задать свою ширину и высоту Title Background.")]
    private bool _overrideTitleBackgroundSize;

    [SerializeField]
    [Tooltip("Ширина и высота Title Background для этого шага. Работает только если включен Override Title Background Size.")]
    private Vector2 _titleBackgroundSize = new Vector2(337f, 184f);

    [Header("Title и Body")]
    [SerializeField]
    [Tooltip("Включи, если на этом шаге нужно отдельно задать размер, позицию и шрифт Title Text.")]
    private bool _overrideTitleTextLayout;

    [SerializeField]
    [Tooltip("Ширина и высота RectTransform у Title Text для этого шага.")]
    private Vector2 _titleTextSize = new Vector2(337f, 96f);

    [SerializeField, Min(1f)]
    [Tooltip("Размер шрифта Title Text для этого шага.")]
    private float _titleFontSize = 42f;

    [SerializeField]
    [Tooltip("Смещение Title Text от базовой позиции. Можно двигать заголовок отдельно от Body.")]
    private Vector2 _titleTextOffset;

    [SerializeField]
    [Tooltip("Включи, если на этом шаге нужно отдельно задать размер, позицию и шрифт Body Text.")]
    private bool _overrideBodyTextLayout;

    [SerializeField]
    [Tooltip("Ширина и высота RectTransform у Body Text для этого шага.")]
    private Vector2 _bodyTextSize = new Vector2(900f, 520f);

    [SerializeField, Min(1f)]
    [Tooltip("Размер шрифта Body Text для этого шага.")]
    private float _bodyFontSize = 32f;

    [SerializeField]
    [Tooltip("Смещение Body Text от базовой позиции. Используй для нормального пространства между Title и Body, например Y = -80.")]
    private Vector2 _bodyTextOffset;

    public FirstLaunchOnboardingStep()
    {
    }

    public FirstLaunchOnboardingStep(
        FirstLaunchOnboardingState state,
        string stepKey,
        string title,
        string body,
        string primaryButtonText,
        FirstLaunchOnboardingButtonAction primaryAction,
        bool showSecondaryButton,
        string secondaryButtonText,
        FirstLaunchOnboardingButtonAction secondaryAction)
    {
        _enabled = true;
        _state = state;
        _stepKey = stepKey;
        _revision = 1;
        _title = title;
        _body = body;
        _primaryButtonText = primaryButtonText;
        _primaryAction = primaryAction;
        _showSecondaryButton = showSecondaryButton;
        _secondaryButtonText = secondaryButtonText;
        _secondaryAction = secondaryAction;
    }

    public void Normalize()
    {
        _revision = Mathf.Max(1, _revision);
        _panelSize = ClampSize(_panelSize);
        _titleBackgroundSize = ClampSize(_titleBackgroundSize);
        _titleTextSize = ClampSize(_titleTextSize);
        _bodyTextSize = ClampSize(_bodyTextSize);
        _titleFontSize = Mathf.Max(1f, _titleFontSize);
        _bodyFontSize = Mathf.Max(1f, _bodyFontSize);

        string defaultStepKey = GetDefaultStepKey(_state);
        if (!string.IsNullOrWhiteSpace(defaultStepKey) &&
            (string.IsNullOrWhiteSpace(_stepKey) || IsKnownDefaultStepKey(_stepKey) && !string.Equals(_stepKey, defaultStepKey, StringComparison.OrdinalIgnoreCase)))
        {
            _stepKey = defaultStepKey;
        }
    }

    public bool Enabled => _enabled;
    public FirstLaunchOnboardingState State => _state;
    public string StepKey => SaveDataSanitizer.SafeKeyPart(_stepKey, "step", 64);
    public int Revision => Mathf.Max(1, _revision);
    public string Title => _title ?? "";
    public string Body => _body ?? "";
    public string PrimaryButtonText => string.IsNullOrWhiteSpace(_primaryButtonText) ? "Далее" : _primaryButtonText;
    public FirstLaunchOnboardingButtonAction PrimaryAction => _primaryAction;
    public bool ShowSecondaryButton => _showSecondaryButton;
    public string SecondaryButtonText => string.IsNullOrWhiteSpace(_secondaryButtonText) ? "Продолжить так" : _secondaryButtonText;
    public FirstLaunchOnboardingButtonAction SecondaryAction => _secondaryAction;
    public bool OverridePanelSize => _overridePanelSize;
    public Vector2 PanelSize => ClampSize(_panelSize);
    public bool ShowTitleBackground => _showTitleBackground;
    public bool OverrideTitleBackgroundSize => _overrideTitleBackgroundSize;
    public Vector2 TitleBackgroundSize => ClampSize(_titleBackgroundSize);
    public bool OverrideTitleTextLayout => _overrideTitleTextLayout;
    public Vector2 TitleTextSize => ClampSize(_titleTextSize);
    public float TitleFontSize => Mathf.Max(1f, _titleFontSize);
    public Vector2 TitleTextOffset => _titleTextOffset;
    public bool OverrideBodyTextLayout => _overrideBodyTextLayout;
    public Vector2 BodyTextSize => ClampSize(_bodyTextSize);
    public float BodyFontSize => Mathf.Max(1f, _bodyFontSize);
    public Vector2 BodyTextOffset => _bodyTextOffset;

    public string SeenKey => "first_launch_onboarding:" + StepKey + ":rev_" + Revision;
    public string SeenPurpose => LocalSaveSecurity.SetupFlagPurpose + ":first_launch_onboarding:" + StepKey;

    private static string GetDefaultStepKey(FirstLaunchOnboardingState state)
    {
        switch (state)
        {
            case FirstLaunchOnboardingState.CatIntro:
                return "cat_intro";
            case FirstLaunchOnboardingState.Terms:
                return "terms";
            case FirstLaunchOnboardingState.ProgressWarning:
                return "progress_warning";
            default:
                return "";
        }
    }

    private static bool IsKnownDefaultStepKey(string stepKey)
    {
        return string.Equals(stepKey, "cat_intro", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stepKey, "terms", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stepKey, "progress_warning", StringComparison.OrdinalIgnoreCase);
    }

    private static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
    }
}

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/First Launch Onboarding State Machine")]
public sealed class FirstLaunchOnboardingStateMachineController : MonoBehaviour
{
    private const string PlatformSignInCompletedKey = "first_launch_platform_sign_in_completed";
    private const string PlatformSignInProviderKey = "first_launch_platform_sign_in_provider";
    private const string PlatformSignInUserIdKey = "first_launch_platform_sign_in_user_id";
    private const string PlatformSignInUserNameKey = "first_launch_platform_sign_in_user_name";

    [Header("Запуск")]
    [SerializeField]
    [Tooltip("Автоматически запустить цепочку первого запуска в Start.")]
    private bool _runOnStart = true;

    [SerializeField]
    [Tooltip("Если цепочка уже идёт, повторный Run остановит её и начнёт сначала.")]
    private bool _restartIfAlreadyRunning;

    [SerializeField]
    [Tooltip("Запоминать прохождение каждого шага. Если выключено, цепочка будет показываться каждый запуск.")]
    private bool _rememberCompletedSteps = true;

    [SerializeField]
    [Tooltip("ДЕБАГ: показывать цепочку при каждом запуске и игнорировать сохраненный просмотр. Удобно для теста в Unity и тестовых билдах.")]
    private bool _debugShowEveryLaunch;

    [SerializeField]
    [Tooltip("В Editor/Development Build показывать шаги каждый запуск, даже если они уже просмотрены.")]
    private bool _showEveryPlayInDebug = true;

    [SerializeField]
    [Tooltip("Принудительно показать все включённые шаги. Удобно для теста всей цепочки в Unity.")]
    private bool _forceShowAllSteps;

    [Header("Один общий UI")]
    [SerializeField]
    [Tooltip("Корневой объект общей плашки. Это твой FirstLaunchLegalRoot/Panel.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня для fade-анимации и блокировки кликов под плашкой.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("Визуальная панель для scale-анимации. Можно указать тот же Panel внутри root.")]
    private RectTransform _panelRoot;

    [SerializeField]
    [Tooltip("Необязательный TMP_Text заголовка. Если у тебя всё в одном тексте, оставь пустым.")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("Фон/рамка заголовка. Перетащи сюда RectTransform объекта TitleBackground, если хочешь менять его размер и видимость по шагам.")]
    private RectTransform _titleBackground;

    [SerializeField]
    [Tooltip("Главный TMP_Text. State-machine просто заменяет его содержимое на каждом шаге.")]
    private TMP_Text _bodyText;

    [SerializeField]
    [Tooltip("Одна и та же основная кнопка для всех шагов: Понятно, Принять, Войти.")]
    private Button _primaryButton;

    [SerializeField]
    [Tooltip("TMP_Text основной кнопки.")]
    private TMP_Text _primaryButtonText;

    [SerializeField]
    [Tooltip("Одна и та же вторая кнопка. Используется только на шагах, где включён Show Secondary Button.")]
    private Button _secondaryButton;

    [SerializeField]
    [Tooltip("TMP_Text второй кнопки.")]
    private TMP_Text _secondaryButtonText;

    [SerializeField]
    [Tooltip("Если включено, вторая кнопка показывается только на последнем активном шаге цепочки. Настройки Show Secondary Button внутри ранних шагов будут проигнорированы.")]
    private bool _secondaryButtonOnlyOnLastStep = true;

    [SerializeField]
    [Tooltip("Поднимать плашку поверх остальных UI при показе.")]
    private bool _bringToFrontOnShow = true;

    [SerializeField]
    [Tooltip("Выключать Root через SetActive(false) после скрытия. Для мониторинга интернета в любой момент лучше оставить выключенным: панель будет невидимой через CanvasGroup, но компонент продолжит работать.")]
    private bool _deactivateRootOnHide;

    [SerializeField]
    [Tooltip("Отключать старые first-launch контроллеры внутри этого Root, чтобы они не переписывали те же TMP_Text поверх state-machine.")]
    private bool _disableLegacyFirstLaunchControllers = true;

    [SerializeField]
    [Tooltip("Перед записью каждого шага очистить все TMP_Text внутри Root, а потом записать нужные тексты заново. Убирает наложение старого текста из сцены или старых компонентов.")]
    private bool _clearAllRootTextsBeforeWrite = true;

    [Header("Шаги")]
    [SerializeField]
    [Tooltip("Шаги показываются сверху вниз. Это не отдельные плашки, а разные тексты для одного и того же UI.")]
    private FirstLaunchOnboardingStep[] _steps =
    {
        new FirstLaunchOnboardingStep(),
        null,
        null
    };

    [Header("Системные сообщения")]
    [SerializeField]
    [Tooltip("Следить за Application.internetReachability и показывать эту же плашку, если соединение пропало.")]
    private bool _monitorInternetConnection = true;

    [SerializeField, Min(0.25f)]
    [Tooltip("Как часто проверять состояние сети, в секундах.")]
    private float _connectionPollInterval = 1f;

    [SerializeField]
    [Tooltip("Автоматически показывать сообщение Нет соединения, когда интернет пропал.")]
    private bool _showNoConnectionWhenDisconnected = true;

    [SerializeField]
    [Tooltip("Автоматически закрывать сообщение Нет соединения, когда интернет вернулся.")]
    private bool _hideNoConnectionWhenRestored = true;

    [SerializeField]
    [Tooltip("Заголовок сообщения, когда нет интернета.")]
    private string _noConnectionTitle = "Нет соединения";

    [SerializeField, TextArea(2, 8)]
    [Tooltip("Текст сообщения, когда нет интернета.")]
    private string _noConnectionBody = "Проверь подключение к интернету и попробуй снова.";

    [SerializeField]
    [Tooltip("Текст основной кнопки в сообщении Нет соединения.")]
    private string _noConnectionButtonText = "Повторить";

    [SerializeField]
    [Tooltip("Заголовок информационной плашки под обновления или новости.")]
    private string _updateInfoTitle = "Обновление";

    [SerializeField, TextArea(2, 8)]
    [Tooltip("Текст информационной плашки под обновления или новости. Его можно менять из инспектора или передать через ShowUpdateInfo.")]
    private string _updateInfoBody = "Скоро здесь появится важная информация.";

    [SerializeField]
    [Tooltip("Текст кнопки информационной плашки под обновления.")]
    private string _updateInfoButtonText = "Понятно";

    [Header("Платформенный вход")]
    [SerializeField]
    [Tooltip("Call platform sign-in when pressing Sign In. Android platform sign-in is disabled; iOS can still use Game Center.")]
    private bool _usePlatformSignIn = true;

    [SerializeField]
    [Tooltip("Если успешный платформенный вход уже сохранен, шаг с кнопкой Войти будет пропущен, чтобы игрок не входил снова и снова.")]
    private bool _skipPlatformSignInStepIfRemembered = true;

    [SerializeField]
    [Tooltip("Сохранять успешный вход в LocalSecurePrefs: провайдер, user id и имя игрока.")]
    private bool _rememberPlatformSignIn = true;

    [SerializeField]
    [Tooltip("Debug in Editor: treat platform sign-in as successful without a real platform provider. Not used in builds.")]
    private bool _debugSimulatePlatformSignInInEditor = true;

    [SerializeField, Min(1f)]
    [Tooltip("How many seconds to wait for a platform sign-in callback before treating it as failed.")]
    private float _platformSignInTimeout = 45f;

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций.")]
    private bool _useUnscaledTime = true;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность появления общей плашки.")]
    private float _showDuration = 0.22f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность скрытия общей плашки.")]
    private float _hideDuration = 0.18f;

    [SerializeField]
    [Tooltip("Легкая анимация движения панели при появлении: без движения, popup-scale или slide down сверху вниз.")]
    private FirstLaunchOnboardingPanelMotion _panelShowMotion = FirstLaunchOnboardingPanelMotion.PopupScale;

    [SerializeField, Min(0.01f)]
    [Tooltip("Стартовый scale панели для режима Popup Scale.")]
    private float _showStartScale = 0.97f;

    [SerializeField]
    [Tooltip("Смещение старта панели для режима Slide Down. Y больше 0 означает старт чуть выше и движение вниз на базовую позицию.")]
    private Vector2 _slideDownOffset = new Vector2(0f, 80f);

    [SerializeField]
    [Tooltip("Использовать выбранное движение и при скрытии панели. Если выключено, при скрытии будет только fade.")]
    private bool _usePanelMotionOnHide = true;

    [SerializeField, Min(0f)]
    [Tooltip("Мягкий fade текста при смене шага. 0 отключает анимацию смены текста.")]
    private float _textSwapFadeDuration = 0.08f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность анимации изменения размеров Panel Root и Title Background при смене шага. 0 меняет размер мгновенно.")]
    private float _resizeDuration = 0.12f;

    [SerializeField]
    [Tooltip("Ease появления.")]
    private Ease _showEase = Ease.OutQuart;

    [SerializeField]
    [Tooltip("Ease скрытия.")]
    private Ease _hideEase = Ease.InQuart;

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается при смене состояния state-machine.")]
    private FirstLaunchOnboardingStateChangedEvent _stateChanged = new FirstLaunchOnboardingStateChangedEvent();

    [SerializeField]
    [Tooltip("Вызывается после завершения всей цепочки.")]
    private UnityEvent _completed = new UnityEvent();

    [SerializeField]
    [Tooltip("Invoked before supported platform sign-in starts.")]
    private UnityEvent _platformSignInRequested = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается после успешного платформенного входа и сохранения локального флага.")]
    private UnityEvent _platformSignInSucceeded = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается, если платформенный вход отменен или завершился ошибкой. Плашка остается на текущем шаге.")]
    private UnityEvent _platformSignInFailed = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается, когда монитор соединения заметил потерю интернета.")]
    private UnityEvent _connectionLost = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается, когда монитор соединения заметил восстановление интернета.")]
    private UnityEvent _connectionRestored = new UnityEvent();

    private Coroutine _routine;
    private Coroutine _connectionMonitorRoutine;
    private Coroutine _systemMessageRoutine;
    private FirstLaunchOnboardingState _state = FirstLaunchOnboardingState.Idle;
    private FirstLaunchOnboardingButtonAction _pendingAction;
    private bool _hasPendingAction;
    private bool _platformSignInInProgress;
    private bool _systemMessageActive;
    private bool _systemMessageRequiresConnection;
    private FirstLaunchOnboardingState _systemMessageState = FirstLaunchOnboardingState.SystemMessage;
    private bool _lastInternetReachable = true;
    private bool _started;
    private int _currentStepIndex = -1;
    private Vector3 _panelBaseScale = Vector3.one;
    private Vector2 _panelBaseAnchoredPosition;
    private Vector2 _panelBaseSize;
    private Vector2 _titleBackgroundBaseSize;
    private Vector2 _titleTextBaseSize;
    private Vector2 _titleTextBaseAnchoredPosition;
    private Vector2 _bodyTextBaseSize;
    private Vector2 _bodyTextBaseAnchoredPosition;
    private float _titleTextBaseFontSize;
    private float _bodyTextBaseFontSize;
    private bool _hasPanelBaseSize;
    private bool _hasTitleBackgroundBaseSize;
    private bool _hasTitleTextBaseLayout;
    private bool _hasBodyTextBaseLayout;
    private bool _forceInstantLayout;

    public FirstLaunchOnboardingState CurrentState => _state;
    public bool IsRunning => _routine != null;
    public bool IsComplete => _state == FirstLaunchOnboardingState.Complete;
    public bool IsStartupFlowPendingOrRunning => IsRunning || (!_started && _runOnStart && HasAnyStepToShow());
    public bool HasRememberedPlatformSignIn => IsPlatformSignInRemembered();

    private void Reset()
    {
        _steps = CreateDefaultSteps();
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyFirstLaunchControllersIfNeeded();
        BindButtons();

        if (_panelRoot != null)
            _panelBaseScale = _panelRoot.localScale;

        if (_runOnStart && HasAnyStepToShow())
        {
            HideVisualsButKeepRootActive();
            PrepareHiddenFirstStep();
            HideVisualsButKeepRootActive();
        }
        else
        {
            HideImmediate();
        }
    }

    private void Start()
    {
        _started = true;
        BindButtons();

        if (_runOnStart)
            RunIfNeeded();

        StartConnectionMonitorIfNeeded();
    }

    private void OnEnable()
    {
        BindButtons();
        if (_started)
            StartConnectionMonitorIfNeeded();
    }

    private void OnDisable()
    {
        StopConnectionMonitor();
        UnbindButtons();
    }

    private void OnDestroy()
    {
        StopStateMachine();
        StopConnectionMonitor();
        UnbindButtons();
    }

    private void OnValidate()
    {
        _steps ??= CreateDefaultSteps();
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showStartScale = Mathf.Max(0.01f, _showStartScale);
        _textSwapFadeDuration = Mathf.Max(0f, _textSwapFadeDuration);
        _resizeDuration = Mathf.Max(0f, _resizeDuration);
        _platformSignInTimeout = Mathf.Max(1f, _platformSignInTimeout);
        _connectionPollInterval = Mathf.Max(0.25f, _connectionPollInterval);

        if (_steps != null)
        {
            for (int i = 0; i < _steps.Length; i++)
                _steps[i]?.Normalize();
        }
    }

    [ContextMenu("Запустить цепочку")]
    public void RunIfNeeded()
    {
        if (_routine != null)
        {
            if (!_restartIfAlreadyRunning)
                return;

            StopCoroutine(_routine);
            _routine = null;
        }

        _routine = StartCoroutine(RunRoutine());
    }

    [ContextMenu("Остановить цепочку")]
    public void StopStateMachine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _hasPendingAction = false;
        _systemMessageActive = false;
        if (_systemMessageRoutine != null)
        {
            StopCoroutine(_systemMessageRoutine);
            _systemMessageRoutine = null;
        }

        SetState(FirstLaunchOnboardingState.Idle);
    }

    [ContextMenu("Сбросить просмотр всех шагов")]
    public void ResetAllSeen()
    {
        if (_steps == null)
            return;

        for (int i = 0; i < _steps.Length; i++)
        {
            FirstLaunchOnboardingStep step = _steps[i];
            if (step != null)
                LocalSecurePrefs.Delete(step.SeenKey);
        }
    }

    [ContextMenu("Сбросить сохраненный платформенный вход")]
    public void ResetRememberedPlatformSignIn()
    {
        LocalSecurePrefs.Delete(PlatformSignInCompletedKey);
        LocalSecurePrefs.Delete(PlatformSignInProviderKey);
        LocalSecurePrefs.Delete(PlatformSignInUserIdKey);
        LocalSecurePrefs.Delete(PlatformSignInUserNameKey);
    }

    [ContextMenu("Показать сообщение: нет соединения")]
    public void ShowNoConnectionMessage()
    {
        ShowSystemMessage(_noConnectionTitle, _noConnectionBody, _noConnectionButtonText, FirstLaunchOnboardingState.NoConnection, true);
    }

    [ContextMenu("Показать сообщение: обновление")]
    public void ShowUpdateInfoFromInspector()
    {
        ShowUpdateInfo(_updateInfoTitle, _updateInfoBody, _updateInfoButtonText);
    }

    public void ShowUpdateInfo(string title, string body)
    {
        ShowUpdateInfo(title, body, _updateInfoButtonText);
    }

    public void ShowUpdateInfo(string title, string body, string buttonText)
    {
        ShowSystemMessage(title, body, buttonText, FirstLaunchOnboardingState.UpdateInfo, false);
    }

    public void ShowSystemMessage(string title, string body, string buttonText, FirstLaunchOnboardingState state = FirstLaunchOnboardingState.SystemMessage, bool requiresConnection = false)
    {
        if (!isActiveAndEnabled)
            return;

        if (_systemMessageRoutine != null)
        {
            StopCoroutine(_systemMessageRoutine);
            _systemMessageRoutine = null;
        }

        _systemMessageRoutine = StartCoroutine(SystemMessageRoutine(title, body, buttonText, state, requiresConnection));
    }

    public void HideSystemMessage()
    {
        _systemMessageActive = false;
    }

    public void HideImmediate()
    {
        CanvasGroup group = ResolveCanvasGroup();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        GameObject root = ResolveRoot();
        if (root != null && _deactivateRootOnHide)
            root.SetActive(false);
    }

    private IEnumerator RunRoutine()
    {
        int stepIndex = FindNextStepIndex(0);
        if (stepIndex < 0)
        {
            CompleteStateMachine();
            _routine = null;
            yield break;
        }

        _currentStepIndex = stepIndex;
        FirstLaunchOnboardingStep firstStep = _steps[stepIndex];
        SetState(firstStep.State);
        ApplyStepInstantly(firstStep);
        SetStepTextsAlpha(1f);
        yield return ShowPanelRoutine();

        bool currentStepAlreadyRendered = true;
        bool shouldContinue = true;
        while (shouldContinue && stepIndex >= 0)
        {
            FirstLaunchOnboardingStep step = _steps[stepIndex];
            _currentStepIndex = stepIndex;
            SetState(step.State);

            if (currentStepAlreadyRendered)
                currentStepAlreadyRendered = false;
            else
                yield return RenderStepRoutine(step);

            _hasPendingAction = false;

            while (!_hasPendingAction)
                yield return null;

            FirstLaunchOnboardingButtonAction action = _pendingAction;

            switch (action)
            {
                case FirstLaunchOnboardingButtonAction.OpenAuthAndComplete:
                    bool signedIn = false;
                    yield return RunPlatformSignInRoutine(result => signedIn = result);
                    if (!signedIn)
                    {
                        _hasPendingAction = false;
                        currentStepAlreadyRendered = true;
                        continue;
                    }

                    MarkStepSeen(step);
                    CompleteStateMachine();
                    yield return HidePanelRoutine();
                    _routine = null;
                    yield break;
                case FirstLaunchOnboardingButtonAction.Complete:
                    MarkStepSeen(step);
                    shouldContinue = false;
                    break;
                case FirstLaunchOnboardingButtonAction.Next:
                default:
                    MarkStepSeen(step);
                    stepIndex = FindNextStepIndex(stepIndex + 1);
                    if (stepIndex < 0)
                        shouldContinue = false;
                    break;
            }
        }

        yield return HidePanelRoutine();
        CompleteStateMachine();
        _routine = null;
    }
    private IEnumerator SystemMessageRoutine(string title, string body, string buttonText, FirstLaunchOnboardingState state, bool requiresConnection)
    {
        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();
        bool wasHidden = root == null || !root.activeInHierarchy || group == null || group.alpha <= 0.001f;
        int restoreStepIndex = _currentStepIndex;

        _systemMessageActive = true;
        _systemMessageRequiresConnection = requiresConnection;
        _systemMessageState = state;

        SetState(state);
        ApplySystemMessage(title, body, buttonText);

        if (wasHidden)
            yield return ShowPanelRoutine();

        while (_systemMessageActive)
            yield return null;

        _systemMessageRequiresConnection = false;

        if (_routine != null && restoreStepIndex >= 0 && _steps != null && restoreStepIndex < _steps.Length && _steps[restoreStepIndex] != null)
        {
            _currentStepIndex = restoreStepIndex;
            SetState(_steps[restoreStepIndex].State);
            yield return RenderStepRoutine(_steps[restoreStepIndex]);
        }
        else
        {
            yield return HidePanelRoutine();
        }

        _systemMessageRoutine = null;
    }

    private void ApplySystemMessage(string title, string body, string buttonText)
    {
        ClearRootTextsBeforeWrite();

        if (_titleText != null)
        {
            SetText(_titleText, title);
            SetText(_bodyText, body);
        }
        else
        {
            SetText(_bodyText, BuildSingleTextBody(title, body));
        }

        SetText(_primaryButtonText, string.IsNullOrWhiteSpace(buttonText) ? "Понятно" : buttonText);
        SetText(_secondaryButtonText, "");

        if (_titleBackground != null)
            _titleBackground.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));

        if (_primaryButton != null)
            _primaryButton.gameObject.SetActive(true);

        if (_secondaryButton != null)
            _secondaryButton.gameObject.SetActive(false);

        SetStepTextsAlpha(1f);
    }

    private IEnumerator ShowPanelRoutine()
    {
        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform panel = ResolvePanelRoot();

        if (root == null)
            yield break;

        root.SetActive(true);
        if (_bringToFrontOnShow)
            root.transform.SetAsLastSibling();

        if (panel != null)
        {
            CapturePanelMotionBase(panel);
            panel.DOKill(false);
            PreparePanelForShow(panel);
        }

        if (group != null)
        {
            group.DOKill(false);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        Tween fade = group != null ? group.DOFade(1f, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime) : null;
        Tween motion = panel != null ? CreatePanelShowMotionTween(panel) : null;
        yield return WaitTweens(fade, motion, _showDuration);

        if (panel != null)
            RestorePanelMotionBase(panel);
    }

    private IEnumerator HidePanelRoutine()
    {
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform panel = ResolvePanelRoot();

        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            group.DOKill(false);
        }

        if (panel != null)
            panel.DOKill(false);

        Tween fade = group != null ? group.DOFade(0f, _hideDuration).SetEase(_hideEase).SetUpdate(_useUnscaledTime) : null;
        Tween motion = panel != null ? CreatePanelHideMotionTween(panel) : null;
        yield return WaitTweens(fade, motion, _hideDuration);

        if (panel != null)
            RestorePanelMotionBase(panel);

        GameObject root = ResolveRoot();
        if (root != null && _deactivateRootOnHide)
            root.SetActive(false);
    }

    private void CapturePanelMotionBase(RectTransform panel)
    {
        if (panel == null)
            return;

        _panelBaseScale = panel.localScale;
        _panelBaseAnchoredPosition = panel.anchoredPosition;
    }

    private void PreparePanelForShow(RectTransform panel)
    {
        if (panel == null)
            return;

        switch (_panelShowMotion)
        {
            case FirstLaunchOnboardingPanelMotion.SlideDown:
                panel.localScale = _panelBaseScale;
                panel.anchoredPosition = _panelBaseAnchoredPosition + _slideDownOffset;
                break;
            case FirstLaunchOnboardingPanelMotion.PopupScale:
                panel.localScale = _panelBaseScale * _showStartScale;
                panel.anchoredPosition = _panelBaseAnchoredPosition;
                break;
            case FirstLaunchOnboardingPanelMotion.None:
            default:
                panel.localScale = _panelBaseScale;
                panel.anchoredPosition = _panelBaseAnchoredPosition;
                break;
        }
    }

    private Tween CreatePanelShowMotionTween(RectTransform panel)
    {
        if (panel == null || _showDuration <= 0f)
            return null;

        switch (_panelShowMotion)
        {
            case FirstLaunchOnboardingPanelMotion.SlideDown:
                return panel.DOAnchorPos(_panelBaseAnchoredPosition, _showDuration)
                    .SetEase(_showEase)
                    .SetUpdate(_useUnscaledTime);
            case FirstLaunchOnboardingPanelMotion.PopupScale:
                return panel.DOScale(_panelBaseScale, _showDuration)
                    .SetEase(_showEase)
                    .SetUpdate(_useUnscaledTime);
            case FirstLaunchOnboardingPanelMotion.None:
            default:
                return null;
        }
    }

    private Tween CreatePanelHideMotionTween(RectTransform panel)
    {
        if (panel == null || !_usePanelMotionOnHide || _hideDuration <= 0f)
            return null;

        switch (_panelShowMotion)
        {
            case FirstLaunchOnboardingPanelMotion.SlideDown:
                return panel.DOAnchorPos(_panelBaseAnchoredPosition + _slideDownOffset, _hideDuration)
                    .SetEase(_hideEase)
                    .SetUpdate(_useUnscaledTime);
            case FirstLaunchOnboardingPanelMotion.PopupScale:
                return panel.DOScale(_panelBaseScale * _showStartScale, _hideDuration)
                    .SetEase(_hideEase)
                    .SetUpdate(_useUnscaledTime);
            case FirstLaunchOnboardingPanelMotion.None:
            default:
                return null;
        }
    }

    private void RestorePanelMotionBase(RectTransform panel)
    {
        if (panel == null)
            return;

        panel.localScale = _panelBaseScale;
        panel.anchoredPosition = _panelBaseAnchoredPosition;
    }

    private IEnumerator RenderStepRoutine(FirstLaunchOnboardingStep step)
    {
        if (_textSwapFadeDuration > 0f && HasAnyStepText())
        {
            yield return FadeStepTextsRoutine(0f);
            ApplyStep(step);
            yield return FadeStepTextsRoutine(1f);
            yield break;
        }

        ApplyStep(step);
        SetStepTextsAlpha(1f);
    }

    private void ApplyStep(FirstLaunchOnboardingStep step)
    {
        if (step == null)
            return;

        ClearRootTextsBeforeWrite();
        ApplyStepLayout(step);

        if (_titleText != null)
        {
            SetText(_titleText, step.Title);
            SetText(_bodyText, step.Body);
        }
        else
        {
            SetText(_bodyText, BuildSingleTextBody(step));
        }

        SetText(_primaryButtonText, step.PrimaryButtonText);
        SetText(_secondaryButtonText, step.SecondaryButtonText);

        if (_primaryButton != null)
            _primaryButton.gameObject.SetActive(true);

        if (_secondaryButton != null)
            _secondaryButton.gameObject.SetActive(ShouldShowSecondaryButton(step));
    }

    private IEnumerator FadeStepTextsRoutine(float targetAlpha)
    {
        Tween titleTween = FadeText(_titleText, targetAlpha);
        Tween bodyTween = FadeText(_bodyText, targetAlpha);
        Tween primaryButtonTween = FadeText(_primaryButtonText, targetAlpha);
        Tween secondaryButtonTween = FadeText(_secondaryButtonText, targetAlpha);

        yield return WaitTweenGroup(
            _textSwapFadeDuration,
            titleTween,
            bodyTween,
            primaryButtonTween,
            secondaryButtonTween);
    }

    private Tween FadeText(TMP_Text text, float targetAlpha)
    {
        if (text == null)
            return null;

        text.DOKill(false);

        if (!text.gameObject.activeInHierarchy || _textSwapFadeDuration <= 0f)
        {
            SetTextAlpha(text, targetAlpha);
            return null;
        }

        return text.DOFade(targetAlpha, _textSwapFadeDuration)
            .SetUpdate(_useUnscaledTime);
    }

    private bool HasAnyStepText()
    {
        return _titleText != null ||
               _bodyText != null ||
               _primaryButtonText != null ||
               _secondaryButtonText != null;
    }

    private void SetStepTextsAlpha(float alpha)
    {
        SetTextAlpha(_titleText, alpha);
        SetTextAlpha(_bodyText, alpha);
        SetTextAlpha(_primaryButtonText, alpha);
        SetTextAlpha(_secondaryButtonText, alpha);
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private bool ShouldShowSecondaryButton(FirstLaunchOnboardingStep step)
    {
        if (step == null)
            return false;

        if (IsCurrentStepLastConfigured())
            return true;

        if (_secondaryButtonOnlyOnLastStep)
            return false;

        return step.ShowSecondaryButton;
    }

    private bool IsCurrentStepLastConfigured()
    {
        if (_currentStepIndex < 0)
            return false;

        return _currentStepIndex == FindLastConfiguredStepIndex();
    }

    private int FindLastConfiguredStepIndex()
    {
        if (_steps == null)
            return -1;

        for (int i = _steps.Length - 1; i >= 0; i--)
        {
            FirstLaunchOnboardingStep step = _steps[i];
            if (step != null && step.Enabled)
                return i;
        }

        return -1;
    }

    private void ApplyStepLayout(FirstLaunchOnboardingStep step)
    {
        RectTransform panel = ResolvePanelRoot();
        ApplyOptionalRectSize(panel, step.OverridePanelSize, step.PanelSize, _hasPanelBaseSize, _panelBaseSize);
        ApplyTextLayout(_titleText, step.OverrideTitleTextLayout, step.TitleTextSize, step.TitleFontSize, step.TitleTextOffset, ref _hasTitleTextBaseLayout, ref _titleTextBaseSize, ref _titleTextBaseAnchoredPosition, ref _titleTextBaseFontSize);
        ApplyTextLayout(_bodyText, step.OverrideBodyTextLayout, step.BodyTextSize, step.BodyFontSize, step.BodyTextOffset, ref _hasBodyTextBaseLayout, ref _bodyTextBaseSize, ref _bodyTextBaseAnchoredPosition, ref _bodyTextBaseFontSize);

        if (_titleBackground == null)
            return;

        bool showTitleBackground = step.ShowTitleBackground && !string.IsNullOrWhiteSpace(step.Title);
        _titleBackground.gameObject.SetActive(showTitleBackground);

        if (showTitleBackground)
        {
            ApplyOptionalRectSize(
                _titleBackground,
                step.OverrideTitleBackgroundSize,
                step.TitleBackgroundSize,
                _hasTitleBackgroundBaseSize,
                _titleBackgroundBaseSize);
        }
    }

    private void ApplyTextLayout(
        TMP_Text text,
        bool overrideLayout,
        Vector2 overrideSize,
        float overrideFontSize,
        Vector2 overrideOffset,
        ref bool hasBaseLayout,
        ref Vector2 baseSize,
        ref Vector2 baseAnchoredPosition,
        ref float baseFontSize)
    {
        if (text == null)
            return;

        CaptureTextBaseLayout(text, ref hasBaseLayout, ref baseSize, ref baseAnchoredPosition, ref baseFontSize);
        if (!hasBaseLayout)
            return;

        RectTransform rect = text.rectTransform;
        if (rect != null)
        {
            Vector2 targetSize = overrideLayout
                ? new Vector2(Mathf.Max(1f, overrideSize.x), Mathf.Max(1f, overrideSize.y))
                : baseSize;
            Vector2 targetPosition = overrideLayout ? baseAnchoredPosition + overrideOffset : baseAnchoredPosition;

            rect.DOKill(false);
            if (!_forceInstantLayout && _resizeDuration > 0f && rect.gameObject.activeInHierarchy)
            {
                rect.DOSizeDelta(targetSize, _resizeDuration)
                    .SetEase(_showEase)
                    .SetUpdate(_useUnscaledTime);
            }
            else
            {
                rect.sizeDelta = targetSize;
            }

            rect.anchoredPosition = targetPosition;
        }

        text.fontSize = overrideLayout ? Mathf.Max(1f, overrideFontSize) : baseFontSize;
    }

    private static void CaptureTextBaseLayout(
        TMP_Text text,
        ref bool hasBaseLayout,
        ref Vector2 baseSize,
        ref Vector2 baseAnchoredPosition,
        ref float baseFontSize)
    {
        if (hasBaseLayout || text == null)
            return;

        RectTransform rect = text.rectTransform;
        if (rect != null)
        {
            baseSize = rect.sizeDelta;
            baseAnchoredPosition = rect.anchoredPosition;
        }

        baseFontSize = text.fontSize;
        hasBaseLayout = true;
    }

    private void ApplyOptionalRectSize(
        RectTransform rect,
        bool overrideSize,
        Vector2 overrideValue,
        bool hasBaseSize,
        Vector2 baseSize)
    {
        if (rect == null)
            return;

        if (!overrideSize && !hasBaseSize)
            return;

        Vector2 targetSize = overrideSize
            ? new Vector2(Mathf.Max(1f, overrideValue.x), Mathf.Max(1f, overrideValue.y))
            : baseSize;

        rect.DOKill(false);
        if (!_forceInstantLayout && _resizeDuration > 0f && rect.gameObject.activeInHierarchy)
        {
            rect.DOSizeDelta(targetSize, _resizeDuration)
                .SetEase(_showEase)
                .SetUpdate(_useUnscaledTime);
            return;
        }

        rect.sizeDelta = targetSize;
    }

    private static string BuildSingleTextBody(FirstLaunchOnboardingStep step)
    {
        if (step == null)
            return "";

        return BuildSingleTextBody(step.Title, step.Body);
    }

    private static string BuildSingleTextBody(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title))
            return body ?? "";

        if (string.IsNullOrWhiteSpace(body))
            return title ?? "";

        return title + "\n\n" + body;
    }

    private int FindNextStepIndex(int startIndex)
    {
        if (_steps == null)
            return -1;

        for (int i = Mathf.Max(0, startIndex); i < _steps.Length; i++)
        {
            FirstLaunchOnboardingStep step = _steps[i];
            if (ShouldShowStep(step))
                return i;
        }

        return -1;
    }

    private bool HasAnyStepToShow()
    {
        return FindNextStepIndex(0) >= 0;
    }

    private bool ShouldShowStep(FirstLaunchOnboardingStep step)
    {
        if (step == null || !step.Enabled)
            return false;

        if (step.PrimaryAction == FirstLaunchOnboardingButtonAction.OpenAuthAndComplete &&
            !IsPlatformSignInSupportedOnCurrentPlatform())
        {
            return false;
        }

        if (_debugShowEveryLaunch)
            return true;

        if (_forceShowAllSteps)
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_showEveryPlayInDebug)
            return true;
#endif

        if (_skipPlatformSignInStepIfRemembered &&
            step.PrimaryAction == FirstLaunchOnboardingButtonAction.OpenAuthAndComplete &&
            IsPlatformSignInRemembered())
        {
            return false;
        }

        if (!_rememberCompletedSteps)
            return true;

        return !LocalSecurePrefs.GetBool(step.SeenKey, step.SeenPurpose, false);
    }

    private void MarkStepSeen(FirstLaunchOnboardingStep step)
    {
        if (!_rememberCompletedSteps || step == null)
            return;

        LocalSecurePrefs.SetBool(step.SeenKey, step.SeenPurpose, true);
    }

    private void HandlePrimaryClicked()
    {
        if (HandleSystemMessagePrimaryClick())
            return;

        FirstLaunchOnboardingStep step = GetCurrentStep();
        ResolveStepAction(step != null ? step.PrimaryAction : FirstLaunchOnboardingButtonAction.Next);
    }

    private void HandleSecondaryClicked()
    {
        if (_systemMessageActive)
        {
            HideSystemMessage();
            return;
        }

        FirstLaunchOnboardingStep step = GetCurrentStep();
        ResolveStepAction(step != null ? step.SecondaryAction : FirstLaunchOnboardingButtonAction.Complete);
    }

    private bool HandleSystemMessagePrimaryClick()
    {
        if (!_systemMessageActive)
            return false;

        if (_systemMessageRequiresConnection && !IsInternetReachable())
            return true;

        HideSystemMessage();
        return true;
    }

    private void ResolveStepAction(FirstLaunchOnboardingButtonAction action)
    {
        if (_platformSignInInProgress)
            return;

        _pendingAction = action;
        _hasPendingAction = true;
    }

    private FirstLaunchOnboardingStep GetCurrentStep()
    {
        if (_steps == null || _currentStepIndex < 0 || _currentStepIndex >= _steps.Length)
            return null;

        return _steps[_currentStepIndex];
    }

    private void CompleteStateMachine()
    {
        _routine = null;
        SetState(FirstLaunchOnboardingState.Complete);
        _completed.Invoke();
    }

    private IEnumerator RunPlatformSignInRoutine(Action<bool> callback)
    {
        if (IsPlatformSignInRemembered())
        {
            callback?.Invoke(true);
            yield break;
        }

        if (!_usePlatformSignIn || !IsPlatformSignInSupportedOnCurrentPlatform())
        {
            callback?.Invoke(false);
            yield break;
        }

        _platformSignInInProgress = true;
        SetButtonsInteractable(false);
        _platformSignInRequested.Invoke();

        bool completed = false;
        bool success = false;

#if UNITY_EDITOR
        if (_debugSimulatePlatformSignInInEditor)
        {
            success = true;
            completed = true;
        }
        else
#endif
        {
            try
            {
                var localUser = Social.localUser;
                if (localUser == null)
                {
                    completed = true;
                    success = false;
                }
                else if (localUser.authenticated)
                {
                    success = true;
                    completed = true;
                }
                else
                {
                    localUser.Authenticate(result =>
                    {
                        success = result;
                        completed = true;
                    });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{nameof(FirstLaunchOnboardingStateMachineController)}: platform sign-in failed. {exception.Message}", this);
                completed = true;
                success = false;
            }
        }

        float startedAt = Time.unscaledTime;
        while (!completed && Time.unscaledTime - startedAt < _platformSignInTimeout)
            yield return null;

        if (!completed)
            success = false;

        _platformSignInInProgress = false;
        SetButtonsInteractable(true);

        if (success)
        {
            RememberPlatformSignIn();
            _platformSignInSucceeded.Invoke();
        }
        else
        {
            _platformSignInFailed.Invoke();
        }

        callback?.Invoke(success);
    }

    private void RememberPlatformSignIn()
    {
        if (!_rememberPlatformSignIn)
            return;

        LocalSecurePrefs.SetBool(PlatformSignInCompletedKey, GetPlatformSignInPurpose("completed"), true);
        LocalSecurePrefs.SetString(PlatformSignInProviderKey, GetPlatformSignInPurpose("provider"), GetRuntimePlatformProvider());

        if (Social.localUser != null)
        {
            LocalSecurePrefs.SetString(PlatformSignInUserIdKey, GetPlatformSignInPurpose("user_id"), Social.localUser.id ?? "");
            LocalSecurePrefs.SetString(PlatformSignInUserNameKey, GetPlatformSignInPurpose("user_name"), Social.localUser.userName ?? "");
        }
    }

    private bool IsPlatformSignInRemembered()
    {
        if (!_rememberPlatformSignIn)
            return false;

        return LocalSecurePrefs.GetBool(PlatformSignInCompletedKey, GetPlatformSignInPurpose("completed"), false);
    }

    private static string GetRuntimePlatformProvider()
    {
#if UNITY_ANDROID
        return "android_platform_sign_in_removed";
#elif UNITY_IOS
        return "apple_game_center";
#else
        return "unity_social";
#endif
    }

    private bool IsPlatformSignInSupportedOnCurrentPlatform()
    {
#if UNITY_EDITOR
        return _debugSimulatePlatformSignInInEditor;
#elif UNITY_IOS
        return true;
#else
        return false;
#endif
    }

    private static string GetPlatformSignInPurpose(string value)
    {
        return LocalSaveSecurity.SetupFlagPurpose + ":first_launch_platform_sign_in:" + value;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_primaryButton != null)
            _primaryButton.interactable = interactable;

        if (_secondaryButton != null)
            _secondaryButton.interactable = interactable;
    }

    private void StartConnectionMonitorIfNeeded()
    {
        if (!_monitorInternetConnection || _connectionMonitorRoutine != null)
            return;

        _connectionMonitorRoutine = StartCoroutine(ConnectionMonitorRoutine());
    }

    private void StopConnectionMonitor()
    {
        if (_connectionMonitorRoutine == null)
            return;

        StopCoroutine(_connectionMonitorRoutine);
        _connectionMonitorRoutine = null;
    }

    private IEnumerator ConnectionMonitorRoutine()
    {
        _lastInternetReachable = IsInternetReachable();
        if (!_lastInternetReachable && _showNoConnectionWhenDisconnected)
            ShowNoConnectionMessage();

        while (true)
        {
            yield return WaitConnectionPollInterval();

            bool reachable = IsInternetReachable();
            if (reachable == _lastInternetReachable)
                continue;

            _lastInternetReachable = reachable;
            if (reachable)
            {
                _connectionRestored.Invoke();
                if (_hideNoConnectionWhenRestored && _systemMessageActive && _systemMessageRequiresConnection)
                    HideSystemMessage();
            }
            else
            {
                _connectionLost.Invoke();
                if (_showNoConnectionWhenDisconnected)
                    ShowNoConnectionMessage();
            }
        }
    }

    private void DisableLegacyFirstLaunchControllersIfNeeded()
    {
        if (!_disableLegacyFirstLaunchControllers)
            return;

        GameObject root = ResolveRoot();
        if (root == null)
            return;

        FirstLaunchInfoFlowController[] infoFlows = root.GetComponentsInChildren<FirstLaunchInfoFlowController>(true);
        for (int i = 0; i < infoFlows.Length; i++)
        {
            if (infoFlows[i] != null)
                infoFlows[i].enabled = false;
        }

        FirstLaunchProgressWarningController[] progressWarnings = root.GetComponentsInChildren<FirstLaunchProgressWarningController>(true);
        for (int i = 0; i < progressWarnings.Length; i++)
        {
            if (progressWarnings[i] != null)
                progressWarnings[i].enabled = false;
        }
    }

    private void ClearRootTextsBeforeWrite()
    {
        if (!_clearAllRootTextsBeforeWrite)
            return;

        GameObject root = ResolveRoot();
        if (root == null)
            return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            text.DOKill(false);
            SetText(text, "");
        }
    }

    private void PrepareHiddenFirstStep()
    {
        int firstStep = FindNextStepIndex(0);
        if (firstStep < 0 || _steps == null || firstStep >= _steps.Length)
            return;

        _currentStepIndex = firstStep;
        ApplyStepInstantly(_steps[firstStep]);
        SetStepTextsAlpha(1f);
    }

    private void ApplyStepInstantly(FirstLaunchOnboardingStep step)
    {
        bool previous = _forceInstantLayout;
        _forceInstantLayout = true;
        ApplyStep(step);
        _forceInstantLayout = previous;
    }

    private IEnumerator WaitConnectionPollInterval()
    {
        float duration = Mathf.Max(0.25f, _connectionPollInterval);
        if (_useUnscaledTime)
            yield return new WaitForSecondsRealtime(duration);
        else
            yield return new WaitForSeconds(duration);
    }

    private static bool IsInternetReachable()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    private void HideVisualsButKeepRootActive()
    {
        GameObject root = ResolveRoot();
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        CanvasGroup group = ResolveCanvasGroup();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private IEnumerator WaitTweens(Tween first, Tween second, float duration)
    {
        if (duration <= 0f)
            yield break;

        while ((first != null && first.IsActive() && first.IsPlaying()) ||
               (second != null && second.IsActive() && second.IsPlaying()))
        {
            yield return null;
        }
    }

    private IEnumerator WaitTweenGroup(float duration, params Tween[] tweens)
    {
        if (duration <= 0f || tweens == null || tweens.Length == 0)
            yield break;

        bool hasPlayingTween;
        do
        {
            hasPlayingTween = false;
            for (int i = 0; i < tweens.Length; i++)
            {
                Tween tween = tweens[i];
                if (tween != null && tween.IsActive() && tween.IsPlaying())
                {
                    hasPlayingTween = true;
                    break;
                }
            }

            if (hasPlayingTween)
                yield return null;
        }
        while (hasPlayingTween);
    }

    private void BindButtons()
    {
        if (_primaryButton != null)
        {
            _primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            _primaryButton.onClick.AddListener(HandlePrimaryClicked);
        }

        if (_secondaryButton != null)
        {
            _secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
            _secondaryButton.onClick.AddListener(HandleSecondaryClicked);
        }
    }

    private void UnbindButtons()
    {
        if (_primaryButton != null)
            _primaryButton.onClick.RemoveListener(HandlePrimaryClicked);

        if (_secondaryButton != null)
            _secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
    }

    private void SetState(FirstLaunchOnboardingState state)
    {
        if (_state == state)
            return;

        _state = state;
        _stateChanged.Invoke(_state);
    }

    private void ResolveReferences()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolvePanelRoot();
        CaptureBaseLayoutSizes();
    }

    private void CaptureBaseLayoutSizes()
    {
        RectTransform panel = ResolvePanelRoot();
        if (panel != null && !_hasPanelBaseSize)
        {
            _panelBaseSize = panel.sizeDelta;
            _hasPanelBaseSize = true;
        }

        if (_titleBackground != null && !_hasTitleBackgroundBaseSize)
        {
            _titleBackgroundBaseSize = _titleBackground.sizeDelta;
            _hasTitleBackgroundBaseSize = true;
        }

        CaptureTextBaseLayout(_titleText, ref _hasTitleTextBaseLayout, ref _titleTextBaseSize, ref _titleTextBaseAnchoredPosition, ref _titleTextBaseFontSize);
        CaptureTextBaseLayout(_bodyText, ref _hasBodyTextBaseLayout, ref _bodyTextBaseSize, ref _bodyTextBaseAnchoredPosition, ref _bodyTextBaseFontSize);
    }

    private GameObject ResolveRoot()
    {
        if (_root == null)
            _root = gameObject;

        return _root;
    }

    private CanvasGroup ResolveCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            GameObject root = ResolveRoot();
            if (root != null)
                _canvasGroup = root.GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null && ResolveRoot() != null)
            _canvasGroup = ResolveRoot().AddComponent<CanvasGroup>();

        return _canvasGroup;
    }

    private RectTransform ResolvePanelRoot()
    {
        if (_panelRoot == null)
            _panelRoot = transform as RectTransform;

        return _panelRoot;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text == null)
            return;

        text.text = value ?? "";
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate(true, true);
    }

    private static FirstLaunchOnboardingStep[] CreateDefaultSteps()
    {
        return new[]
        {
            CreateStep(
                FirstLaunchOnboardingState.CatIntro,
                "cat_intro",
                "Мяу...",
                "Я — твой мохнатый Проводник\nв Nocturne — в мире, где каждый\nвыбор меняет ход твоей\nистории. Свечи здесь освещают\nпуть, а Рубины зажигают\nмоменты, которые меняют всё...",
                "Понятно",
                FirstLaunchOnboardingButtonAction.Next,
                false,
                "",
                FirstLaunchOnboardingButtonAction.Next),
            CreateStep(
                FirstLaunchOnboardingState.Terms,
                "terms",
                "Условия пользования",
                "Нажимая “Принять”, вы подтверждаете согласие с Условиями пользования и Политикой конфиденциальности. Приложение содержит романтический контент. Возрастное ограничение 16+",
                "Принять",
                FirstLaunchOnboardingButtonAction.Next,
                false,
                "",
                FirstLaunchOnboardingButtonAction.Next),
            CreateStep(
                FirstLaunchOnboardingState.ProgressWarning,
                "progress_warning",
                "Внимание!",
                "Сохрани свой путь ✦\n\nВойди в аккаунт, чтобы не потерять прогресс, продолжай игру с любого устройства в любое время",
                "Войти",
                FirstLaunchOnboardingButtonAction.OpenAuthAndComplete,
                true,
                "Продолжить так",
                FirstLaunchOnboardingButtonAction.Complete)
        };
    }

    private static FirstLaunchOnboardingStep CreateStep(
        FirstLaunchOnboardingState state,
        string key,
        string title,
        string body,
        string primaryText,
        FirstLaunchOnboardingButtonAction primaryAction,
        bool showSecondary,
        string secondaryText,
        FirstLaunchOnboardingButtonAction secondaryAction)
    {
        return new FirstLaunchOnboardingStep(
            state,
            key,
            title,
            body,
            primaryText,
            primaryAction,
            showSecondary,
            secondaryText,
            secondaryAction);
    }
}
