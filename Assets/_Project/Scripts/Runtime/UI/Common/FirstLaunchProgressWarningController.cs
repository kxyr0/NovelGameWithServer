using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/First Launch Progress Warning")]
public sealed class FirstLaunchProgressWarningController : MonoBehaviour
{
    private const string DefaultSeenKey = "first_launch_progress_warning";

    [Header("Показ")]
    [SerializeField]
    [Tooltip("Автоматически показать плашку в Start, если игрок ещё не видел это предупреждение.")]
    private bool _showOnStart = true;

    [SerializeField]
    [Tooltip("Запоминать выбор игрока. Если выключено, плашка будет показываться при каждом запуске сцены.")]
    private bool _rememberChoice = true;

    [SerializeField]
    [Tooltip("Ключ сохранения. Меняй его только если хочешь полностью сбросить факт просмотра для всех игроков.")]
    private string _seenKey = DefaultSeenKey;

    [SerializeField, Min(1)]
    [Tooltip("Ревизия текста. Увеличь число, если нужно показать обновлённое предупреждение повторно.")]
    private int _revision = 1;

    [SerializeField]
    [Tooltip("В Editor/Development Build показывать каждый запуск, даже если игрок уже видел плашку. Удобно для настройки UI.")]
    private bool _showEveryPlayInDebug = true;

    [SerializeField]
    [Tooltip("Принудительно показать плашку независимо от сохранённого состояния. Можно включить для теста.")]
    private bool _forceShow;

    [Header("Переход на авторизацию")]
    [SerializeField]
    [Tooltip("Навигатор экранов. Если назначен и включён Open Auth Screen On Login, кнопка Войти откроет Auth Screen Id.")]
    private StoryScreenNavigator _screenNavigator;

    [SerializeField]
    [Tooltip("Screen ID of the account sign-in screen. The screen root must have a UIScreenMarker with this ID.")]
    private string _authScreenId = "Auth";

    [SerializeField]
    [Tooltip("Открывать экран авторизации при нажатии Войти. Если выключено, используй событие Login Requested и подключи переход вручную.")]
    private bool _openAuthScreenOnLogin = true;

    [SerializeField]
    [Tooltip("Hide the warning immediately before opening the sign-in screen.")]
    private bool _hideImmediatelyBeforeAuth = true;

    [Header("UI Root")]
    [SerializeField]
    [Tooltip("Корневой объект плашки. Объект со скриптом должен быть активен, а Root можно скрывать через этот контроллер.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня для fade-анимации и блокировки кликов под плашкой.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("Визуальная плашка/панель для мягкой scale-анимации.")]
    private RectTransform _panelRoot;

    [SerializeField]
    [Tooltip("Поднимать Root поверх остальных UI при показе.")]
    private bool _bringToFrontOnShow = true;

    [Header("Тексты")]
    [SerializeField]
    [Tooltip("TMP_Text заголовка. По умолчанию: Внимание!")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("Текст заголовка.")]
    private string _title = "Внимание!";

    [SerializeField]
    [Tooltip("TMP_Text короткого акцента. По умолчанию: Сохрани свой путь ✦")]
    private TMP_Text _headlineText;

    [SerializeField]
    [Tooltip("Короткий акцентный текст над описанием.")]
    private string _headline = "Сохрани свой путь ✦";

    [SerializeField]
    [Tooltip("TMP_Text основного описания. Текст можно менять прямо в инспекторе.")]
    private TMP_Text _bodyText;

    [SerializeField, TextArea(3, 10)]
    [Tooltip("Основной текст предупреждения о прогрессе.")]
    private string _body = "Войди в аккаунт, чтобы не потерять прогресс, продолжай игру с любого устройства в любое время";

    [Header("Кнопки")]
    [SerializeField]
    [Tooltip("Кнопка Войти. Сохраняет факт просмотра и открывает экран авторизации или вызывает Login Requested.")]
    private Button _loginButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки Войти.")]
    private TMP_Text _loginButtonText;

    [SerializeField]
    [Tooltip("Текст кнопки входа.")]
    private string _loginButtonLabel = "Войти";

    [SerializeField]
    [Tooltip("Кнопка Продолжить так. Сохраняет факт просмотра и закрывает плашку.")]
    private Button _continueButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки Продолжить так.")]
    private TMP_Text _continueButtonText;

    [SerializeField]
    [Tooltip("Текст кнопки продолжения без входа.")]
    private string _continueButtonLabel = "Продолжить так";

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций, чтобы плашка работала даже при Time.timeScale = 0.")]
    private bool _useUnscaledTime = true;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность появления плашки.")]
    private float _showDuration = 0.22f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность скрытия плашки.")]
    private float _hideDuration = 0.18f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Стартовый scale панели при появлении.")]
    private float _showStartScale = 0.97f;

    [SerializeField]
    [Tooltip("Ease появления.")]
    private Ease _showEase = Ease.OutQuart;

    [SerializeField]
    [Tooltip("Ease скрытия.")]
    private Ease _hideEase = Ease.InQuart;

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается при показе плашки.")]
    private UnityEvent _shown = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается после скрытия плашки.")]
    private UnityEvent _hidden = new UnityEvent();

    [SerializeField]
    [Tooltip("Invoked when Sign In is pressed. Connect a custom sign-in screen here if StoryScreenNavigator is not used.")]
    private UnityEvent _loginRequested = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается при нажатии Продолжить так.")]
    private UnityEvent _continued = new UnityEvent();

    private Coroutine _routine;
    private Vector3 _panelBaseScale = Vector3.one;
    private bool _visible;

    public bool IsVisible => _visible;
    public bool ShowOnStart
    {
        get => _showOnStart;
        set => _showOnStart = value;
    }

    private void Awake()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolvePanelRoot();

        if (_panelRoot != null)
            _panelBaseScale = _panelRoot.localScale;

        BindButtons();

        if (_showOnStart && ShouldShow())
            HideVisualsButKeepRootActive();
        else
            HideImmediate();
    }

    private void Start()
    {
        BindButtons();

        if (_showOnStart)
            ShowIfNeeded();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void OnDestroy()
    {
        StopRoutine();
        UnbindButtons();
    }

    private void OnValidate()
    {
        _seenKey = SaveDataSanitizer.SafeKeyPart(_seenKey, DefaultSeenKey, 96);
        _revision = Mathf.Max(1, _revision);
        _authScreenId = UIScreenState.NormalizeScreenId(_authScreenId);
        _title = string.IsNullOrWhiteSpace(_title) ? "Внимание!" : _title;
        _headline = string.IsNullOrWhiteSpace(_headline) ? "Сохрани свой путь ✦" : _headline;
        _body = string.IsNullOrWhiteSpace(_body)
            ? "Войди в аккаунт, чтобы не потерять прогресс, продолжай игру с любого устройства в любое время"
            : _body;
        _loginButtonLabel = string.IsNullOrWhiteSpace(_loginButtonLabel) ? "Войти" : _loginButtonLabel;
        _continueButtonLabel = string.IsNullOrWhiteSpace(_continueButtonLabel) ? "Продолжить так" : _continueButtonLabel;
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showStartScale = Mathf.Max(0.01f, _showStartScale);
    }

    [ContextMenu("Показать если нужно")]
    public void ShowIfNeeded()
    {
        if (ShouldShow())
            Show();
        else
            HideImmediate();
    }

    [ContextMenu("Показать сейчас")]
    public void ForceShowNow()
    {
        _forceShow = true;
        Show();
    }

    [ContextMenu("Сбросить просмотр")]
    public void ResetSeen()
    {
        LocalSecurePrefs.Delete(SeenPrefsKey);
    }

    public bool ShouldShowNow()
    {
        return ShouldShow();
    }

    public void SetShowOnStart(bool showOnStart)
    {
        _showOnStart = showOnStart;
    }

    public void Show()
    {
        StopRoutine();
        _routine = StartCoroutine(ShowRoutine());
    }

    public void Hide()
    {
        StopRoutine();
        _routine = StartCoroutine(HideRoutine());
    }

    public void HideImmediate()
    {
        StopRoutine();

        CanvasGroup group = ResolveCanvasGroup();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        GameObject root = ResolveRoot();
        if (root != null)
            root.SetActive(false);

        _visible = false;
    }

    public void Login()
    {
        MarkSeen();
        _loginRequested.Invoke();

        if (_hideImmediatelyBeforeAuth)
            HideImmediate();
        else
            Hide();

        if (_openAuthScreenOnLogin)
            OpenAuthScreen();
    }

    public void ContinueWithoutLogin()
    {
        MarkSeen();
        _continued.Invoke();
        Hide();
    }

    private bool ShouldShow()
    {
        if (_forceShow)
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_showEveryPlayInDebug)
            return true;
#endif

        if (!_rememberChoice)
            return true;

        return !LocalSecurePrefs.GetBool(SeenPrefsKey, SeenPurpose, false);
    }

    private IEnumerator ShowRoutine()
    {
        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform panel = ResolvePanelRoot();

        if (root == null)
            yield break;

        RenderTexts();
        root.SetActive(true);
        if (_bringToFrontOnShow)
            root.transform.SetAsLastSibling();

        if (panel != null)
        {
            _panelBaseScale = panel.localScale;
            panel.DOKill(false);
            panel.localScale = _panelBaseScale * _showStartScale;
        }

        if (group != null)
        {
            group.DOKill(false);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        _visible = true;

        Tween fade = group != null ? group.DOFade(1f, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime) : null;
        Tween scale = panel != null ? panel.DOScale(_panelBaseScale, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime) : null;
        yield return WaitTweens(fade, scale, _showDuration);

        _routine = null;
        _shown.Invoke();
    }

    private IEnumerator HideRoutine()
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
        Tween scale = panel != null ? panel.DOScale(_panelBaseScale * _showStartScale, _hideDuration).SetEase(_hideEase).SetUpdate(_useUnscaledTime) : null;
        yield return WaitTweens(fade, scale, _hideDuration);

        if (panel != null)
            panel.localScale = _panelBaseScale;

        _visible = false;
        _forceShow = false;
        _routine = null;
        _hidden.Invoke();

        GameObject root = ResolveRoot();
        if (root != null)
            root.SetActive(false);
    }

    private void HideVisualsButKeepRootActive()
    {
        StopRoutine();

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

        _visible = false;
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

    private void RenderTexts()
    {
        SetText(_titleText, _title);
        SetText(_headlineText, _headline);
        SetText(_bodyText, _body);
        SetText(_loginButtonText, _loginButtonLabel);
        SetText(_continueButtonText, _continueButtonLabel);
    }

    private void OpenAuthScreen()
    {
        if (string.IsNullOrWhiteSpace(_authScreenId))
            return;

        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        if (_screenNavigator != null)
            _screenNavigator.OpenScreen(_authScreenId);
    }

    private void MarkSeen()
    {
        if (_rememberChoice)
            LocalSecurePrefs.SetBool(SeenPrefsKey, SeenPurpose, true);
    }

    private string SeenPrefsKey => SaveDataSanitizer.SafeKeyPart(_seenKey, DefaultSeenKey, 96) + ":rev_" + Mathf.Max(1, _revision);

    private string SeenPurpose => LocalSaveSecurity.SetupFlagPurpose + ":progress_warning:" + SaveDataSanitizer.SafeKeyPart(_seenKey, DefaultSeenKey, 96);

    private void BindButtons()
    {
        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(Login);
            _loginButton.onClick.AddListener(Login);
        }

        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(ContinueWithoutLogin);
            _continueButton.onClick.AddListener(ContinueWithoutLogin);
        }
    }

    private void UnbindButtons()
    {
        if (_loginButton != null)
            _loginButton.onClick.RemoveListener(Login);

        if (_continueButton != null)
            _continueButton.onClick.RemoveListener(ContinueWithoutLogin);
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

    private void StopRoutine()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? "";
    }
}
