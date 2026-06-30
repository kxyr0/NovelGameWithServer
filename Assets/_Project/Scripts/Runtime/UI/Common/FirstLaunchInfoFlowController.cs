using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public sealed class FirstLaunchInfoPageChangedEvent : UnityEvent<int, string>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/First Launch Info Flow Controller")]
public sealed class FirstLaunchInfoFlowController : MonoBehaviour
{
    [Header("Конфиг")]
    [SerializeField]
    [Tooltip("ScriptableObject с текстами, ревизией, страницами, ссылками и правилами подтверждения.")]
    private FirstLaunchInfoFlowConfig _config;

    [SerializeField]
    [Tooltip("Автоматически проверить условия и показать плашки в Start. Объект с этим компонентом должен быть активен в сцене.")]
    private bool _showOnStart = true;

    [SerializeField]
    [Tooltip("Если включено, корневой UI будет скрыт в Awake, пока flow не решит, что его нужно показать.")]
    private bool _hideRootOnAwake = true;

    [SerializeField]
    [Tooltip("Принудительно показывать flow даже если он уже принят. Удобно для кнопки в настройках или проверки в инспекторе.")]
    private bool _forceShow;

    [Header("Корень UI")]
    [SerializeField]
    [Tooltip("Корневой объект всей плашки/модалки. Можно назначить любой объект панели. Он будет включаться на показ и выключаться после скрытия.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня. Нужен для блокировки кликов под плашкой и плавного fade in/out. Если пусто, скрипт попробует создать его на Root.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("RectTransform визуального контейнера плашки. Если назначен, на нем будет мягкая scale-анимация появления.")]
    private RectTransform _animatedPanelRoot;

    [SerializeField]
    [Tooltip("Поднимать Root последним sibling при показе, чтобы плашка была поверх меню.")]
    private bool _bringToFrontOnShow = true;

    [Header("Тексты")]
    [SerializeField]
    [Tooltip("TMP_Text заголовка текущей плашки.")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("TMP_Text основного текста. В него подставляется Body из текущей страницы конфига.")]
    private TMP_Text _bodyText;

    [SerializeField]
    [Tooltip("TMP_Text счетчика страниц. Например: 1/3. Можно оставить пустым.")]
    private TMP_Text _pageCounterText;

    [SerializeField]
    [Tooltip("Формат счетчика страниц. {0} = текущая страница, {1} = всего страниц.")]
    private string _pageCounterFormat = "{0}/{1}";

    [Header("Прокрутка и подтверждение")]
    [SerializeField]
    [Tooltip("ScrollRect с юридическим текстом. Нужен, если страница требует прокрутку до конца.")]
    private ScrollRect _bodyScrollRect;

    [SerializeField]
    [Tooltip("Сбрасывать ScrollRect наверх при переходе на новую страницу.")]
    private bool _resetScrollOnPageChange = true;

    [SerializeField]
    [Tooltip("Toggle подтверждения. Показывается только на страницах, где включен Require Toggle.")]
    private Toggle _acceptToggle;

    [SerializeField]
    [Tooltip("Корень Toggle-подтверждения. Если пусто, будет использован gameObject самого Toggle.")]
    private GameObject _acceptToggleRoot;

    [SerializeField]
    [Tooltip("TMP_Text рядом с Toggle. В него подставляется Toggle Text из текущей страницы.")]
    private TMP_Text _acceptToggleLabel;

    [SerializeField]
    [Tooltip("Сбрасывать Toggle в выключенное состояние при переходе на новую страницу.")]
    private bool _resetToggleOnPageChange = true;

    [SerializeField]
    [Tooltip("Если включено, основная кнопка будет неактивна, пока не выполнены требования страницы: галочка и/или скролл до конца.")]
    private bool _disablePrimaryUntilRequirementsMet = true;

    [Header("Кнопки")]
    [SerializeField]
    [Tooltip("Основная кнопка: Далее или Принять.")]
    private Button _primaryButton;

    [SerializeField]
    [Tooltip("TMP_Text основной кнопки. Скрипт подставит текст из страницы или общий fallback.")]
    private TMP_Text _primaryButtonText;

    [SerializeField]
    [Tooltip("Текст основной кнопки на не последней странице, если в конфиге страницы поле пустое.")]
    private string _nextButtonText = "Далее";

    [SerializeField]
    [Tooltip("Текст основной кнопки на последней странице, если в конфиге страницы поле пустое.")]
    private string _acceptButtonText = "Принять";

    [SerializeField]
    [Tooltip("Кнопка назад. Можно оставить пустой, если назад не нужен.")]
    private Button _backButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки назад.")]
    private TMP_Text _backButtonText;

    [SerializeField]
    [Tooltip("Текст кнопки назад.")]
    private string _backButtonLabel = "Назад";

    [SerializeField]
    [Tooltip("Кнопка отказа. Показывается только если Allow Decline включен в конфиге.")]
    private Button _declineButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки отказа.")]
    private TMP_Text _declineButtonText;

    [Header("Ссылки")]
    [SerializeField]
    [Tooltip("Кнопки ссылок. Контроллер использует их по порядку для Links текущей страницы и скрывает лишние.")]
    private Button[] _linkButtons = Array.Empty<Button>();

    [SerializeField]
    [Tooltip("TMP_Text на кнопках ссылок. Размер массива должен соответствовать Link Buttons, но можно оставить элементы пустыми.")]
    private TMP_Text[] _linkButtonTexts = Array.Empty<TMP_Text>();

    [Header("Поведение во время показа")]
    [SerializeField]
    [Tooltip("Остановить Time.timeScale на время показа плашек. Анимации этого контроллера используют unscaled time, поэтому продолжат работать.")]
    private bool _pauseTimeWhileVisible;

    [SerializeField]
    [Tooltip("Объекты, которые нужно временно скрыть, пока открыты first-launch плашки. Скрипт восстановит их исходное activeSelf.")]
    private GameObject[] _hideWhileVisible = Array.Empty<GameObject>();

    [SerializeField]
    [Tooltip("CanvasGroup, которым нужно временно выключить interactable/blocksRaycasts, пока открыты плашки. Скрипт восстановит исходное состояние.")]
    private CanvasGroup[] _blockWhileVisible = Array.Empty<CanvasGroup>();

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций, чтобы fade работал даже при Time.timeScale = 0.")]
    private bool _useUnscaledTime = true;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность появления плашки.")]
    private float _showDuration = 0.22f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность скрытия плашки.")]
    private float _hideDuration = 0.18f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Стартовый scale плашки при появлении. Например 0.98 дает мягкое раскрытие.")]
    private float _showStartScale = 0.98f;

    [SerializeField]
    [Tooltip("Ease появления.")]
    private Ease _showEase = Ease.OutQuart;

    [SerializeField]
    [Tooltip("Ease скрытия.")]
    private Ease _hideEase = Ease.InQuart;

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается, когда flow показан.")]
    private UnityEvent _shown = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается, когда flow скрыт.")]
    private UnityEvent _hidden = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается после принятия последней плашки.")]
    private UnityEvent _accepted = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается при отказе.")]
    private UnityEvent _declined = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается при смене страницы: index и pageId.")]
    private FirstLaunchInfoPageChangedEvent _pageChanged = new FirstLaunchInfoPageChangedEvent();

    private readonly List<FirstLaunchInfoPageConfig> _runtimePages = new List<FirstLaunchInfoPageConfig>();
    private readonly Dictionary<GameObject, bool> _hiddenObjectStates = new Dictionary<GameObject, bool>();
    private readonly Dictionary<CanvasGroup, CanvasGroupState> _blockedCanvasGroupStates = new Dictionary<CanvasGroup, CanvasGroupState>();
    private readonly Dictionary<Button, UnityAction> _boundLinkHandlers = new Dictionary<Button, UnityAction>();

    private Coroutine _showRoutine;
    private int _pageIndex;
    private bool _visible;
    private bool _timeScaleCaptured;
    private float _capturedTimeScale = 1f;
    private Vector3 _animatedPanelBaseScale = Vector3.one;

    public bool IsVisible => _visible;
    public FirstLaunchInfoFlowConfig Config => _config;
    public bool ShowOnStart
    {
        get => _showOnStart;
        set => _showOnStart = value;
    }

    private void Awake()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolveAnimatedPanelRoot();

        if (_animatedPanelRoot != null)
            _animatedPanelBaseScale = _animatedPanelRoot.localScale;

        BindStaticButtons();

        if (_hideRootOnAwake)
        {
            if (_showOnStart && ShouldShow())
                HideVisualsButKeepRootActive();
            else
                HideImmediate();
        }
    }

    private void Start()
    {
        if (_showOnStart)
            ShowIfNeeded();
    }

    private void OnEnable()
    {
        BindStaticButtons();
    }

    private void OnDisable()
    {
        UnbindStaticButtons();
        UnbindLinkButtons();
    }

    private void OnDestroy()
    {
        StopRunningRoutine();
        RestoreBlockedObjects();
        RestoreTimeScale();
        UnbindStaticButtons();
        UnbindLinkButtons();
    }

    private void OnValidate()
    {
        _pageCounterFormat = string.IsNullOrWhiteSpace(_pageCounterFormat) ? "{0}/{1}" : _pageCounterFormat;
        _nextButtonText = string.IsNullOrWhiteSpace(_nextButtonText) ? "Далее" : _nextButtonText;
        _acceptButtonText = string.IsNullOrWhiteSpace(_acceptButtonText) ? "Принять" : _acceptButtonText;
        _backButtonLabel = string.IsNullOrWhiteSpace(_backButtonLabel) ? "Назад" : _backButtonLabel;
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showStartScale = Mathf.Max(0.01f, _showStartScale);
        _linkButtons ??= Array.Empty<Button>();
        _linkButtonTexts ??= Array.Empty<TMP_Text>();
        _hideWhileVisible ??= Array.Empty<GameObject>();
        _blockWhileVisible ??= Array.Empty<CanvasGroup>();
    }

    [ContextMenu("Показать сейчас")]
    public void ForceShowNow()
    {
        _forceShow = true;
        Show();
    }

    [ContextMenu("Показать если нужно")]
    public void ShowIfNeeded()
    {
        if (ShouldShow())
            Show();
        else
            HideImmediate();
    }

    [ContextMenu("Сбросить принятие")]
    public void ResetAcceptance()
    {
        if (_config == null)
            return;

        LocalSecurePrefs.Delete(_config.AcceptanceKey);
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
        if (!BuildRuntimePages())
        {
            HideImmediate();
            return;
        }

        StopRunningRoutine();
        _showRoutine = StartCoroutine(ShowRoutine());
    }

    public void Hide()
    {
        StopRunningRoutine();
        _showRoutine = StartCoroutine(HideRoutine(true));
    }

    public void HideImmediate()
    {
        StopRunningRoutine();
        UnbindLinkButtons();
        RestoreBlockedObjects();
        RestoreTimeScale();

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

    private bool ShouldShow()
    {
        if (_forceShow)
            return true;

        if (_config == null || _config.CountEnabledPages() == 0)
            return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_config.ShowEveryPlayInDebug)
            return true;
#endif

        if (!_config.RememberAcceptance)
            return true;

        return !LocalSecurePrefs.GetBool(_config.AcceptanceKey, _config.AcceptancePurpose, false);
    }

    private IEnumerator ShowRoutine()
    {
        GameObject root = ResolveRoot();
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform panelRoot = ResolveAnimatedPanelRoot();

        if (root == null)
            yield break;

        _pageIndex = 0;
        _visible = true;

        root.SetActive(true);
        if (_bringToFrontOnShow)
            root.transform.SetAsLastSibling();

        CaptureAndApplyBlockedObjects();
        CaptureAndApplyTimeScale();

        if (panelRoot != null)
        {
            _animatedPanelBaseScale = panelRoot.localScale;
            panelRoot.DOKill(false);
            panelRoot.localScale = _animatedPanelBaseScale * _showStartScale;
        }

        if (group != null)
        {
            group.DOKill(false);
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        RenderCurrentPage();

        Tween fadeTween = null;
        Tween scaleTween = null;
        if (group != null)
            fadeTween = group.DOFade(1f, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime);

        if (panelRoot != null)
            scaleTween = panelRoot.DOScale(_animatedPanelBaseScale, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime);

        yield return WaitForTweens(fadeTween, scaleTween, _showDuration);

        _showRoutine = null;
        InvokeSafe(_shown, nameof(_shown));
    }

    private IEnumerator HideRoutine(bool invokeEvent)
    {
        CanvasGroup group = ResolveCanvasGroup();
        RectTransform panelRoot = ResolveAnimatedPanelRoot();

        UnbindLinkButtons();

        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            group.DOKill(false);
        }

        if (panelRoot != null)
            panelRoot.DOKill(false);

        Tween fadeTween = null;
        Tween scaleTween = null;
        if (group != null)
            fadeTween = group.DOFade(0f, _hideDuration).SetEase(_hideEase).SetUpdate(_useUnscaledTime);

        if (panelRoot != null)
            scaleTween = panelRoot.DOScale(_animatedPanelBaseScale * _showStartScale, _hideDuration).SetEase(_hideEase).SetUpdate(_useUnscaledTime);

        yield return WaitForTweens(fadeTween, scaleTween, _hideDuration);

        if (panelRoot != null)
            panelRoot.localScale = _animatedPanelBaseScale;

        RestoreBlockedObjects();
        RestoreTimeScale();

        _visible = false;
        _forceShow = false;
        _showRoutine = null;

        if (invokeEvent)
            InvokeSafe(_hidden, nameof(_hidden));

        GameObject root = ResolveRoot();
        if (root != null)
            root.SetActive(false);
    }

    private void HideVisualsButKeepRootActive()
    {
        StopRunningRoutine();
        UnbindLinkButtons();
        RestoreBlockedObjects();
        RestoreTimeScale();

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

    private IEnumerator WaitForTweens(Tween first, Tween second, float fallbackDuration)
    {
        if (fallbackDuration <= 0f)
            yield break;

        while ((first != null && first.IsActive() && first.IsPlaying()) ||
               (second != null && second.IsActive() && second.IsPlaying()))
        {
            yield return null;
        }
    }

    private void HandlePrimaryClicked()
    {
        if (!AreCurrentPageRequirementsMet())
        {
            UpdatePrimaryButtonState();
            return;
        }

        if (_pageIndex >= _runtimePages.Count - 1)
        {
            AcceptFlow();
            return;
        }

        _pageIndex++;
        RenderCurrentPage();
    }

    private void HandleBackClicked()
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;
        RenderCurrentPage();
    }

    private void HandleDeclineClicked()
    {
        InvokeSafe(_declined, nameof(_declined));

        if (_config == null)
        {
            Hide();
            return;
        }

        switch (_config.DeclineAction)
        {
            case FirstLaunchInfoDeclineAction.OpenUrl:
                OpenUrl(_config.DeclineUrl);
                Hide();
                break;
            case FirstLaunchInfoDeclineAction.QuitApplication:
                QuitApplication();
                break;
            case FirstLaunchInfoDeclineAction.HideOnly:
            default:
                Hide();
                break;
        }
    }

    private void AcceptFlow()
    {
        if (_config != null && _config.RememberAcceptance)
            LocalSecurePrefs.SetBool(_config.AcceptanceKey, _config.AcceptancePurpose, true);

        InvokeSafe(_accepted, nameof(_accepted));
        Hide();
    }

    private void RenderCurrentPage()
    {
        if (_runtimePages.Count == 0)
            return;

        _pageIndex = Mathf.Clamp(_pageIndex, 0, _runtimePages.Count - 1);
        FirstLaunchInfoPageConfig page = _runtimePages[_pageIndex];
        bool isLastPage = _pageIndex >= _runtimePages.Count - 1;

        if (_titleText != null)
            _titleText.text = page.Title;

        if (_bodyText != null)
            _bodyText.text = page.Body;

        if (_pageCounterText != null)
            _pageCounterText.text = string.Format(_pageCounterFormat, _pageIndex + 1, _runtimePages.Count);

        if (_primaryButtonText != null)
        {
            string buttonText = page.PrimaryButtonText;
            _primaryButtonText.text = string.IsNullOrWhiteSpace(buttonText)
                ? (isLastPage ? _acceptButtonText : _nextButtonText)
                : buttonText;
        }

        if (_backButtonText != null)
            _backButtonText.text = _backButtonLabel;

        if (_backButton != null)
            _backButton.gameObject.SetActive(_pageIndex > 0);

        if (_declineButton != null)
            _declineButton.gameObject.SetActive(_config != null && _config.AllowDecline);

        if (_declineButtonText != null && _config != null)
            _declineButtonText.text = _config.DeclineButtonText;

        ApplyToggleForPage(page);
        ApplyScrollForPage(page);
        ApplyLinksForPage(page);
        UpdatePrimaryButtonState();

        _pageChanged.Invoke(_pageIndex, page.PageId);
    }

    private void ApplyToggleForPage(FirstLaunchInfoPageConfig page)
    {
        GameObject toggleRoot = _acceptToggleRoot != null
            ? _acceptToggleRoot
            : _acceptToggle != null ? _acceptToggle.gameObject : null;

        bool showToggle = page != null && page.RequireToggle;
        if (toggleRoot != null)
            toggleRoot.SetActive(showToggle);

        if (_acceptToggle != null)
        {
            _acceptToggle.onValueChanged.RemoveListener(HandleRequirementChanged);
            if (_resetToggleOnPageChange)
                _acceptToggle.isOn = false;

            _acceptToggle.onValueChanged.AddListener(HandleRequirementChanged);
        }

        if (_acceptToggleLabel != null && page != null)
            _acceptToggleLabel.text = page.ToggleText;
    }

    private void ApplyScrollForPage(FirstLaunchInfoPageConfig page)
    {
        if (_bodyScrollRect == null)
            return;

        _bodyScrollRect.onValueChanged.RemoveListener(HandleScrollChanged);

        if (_resetScrollOnPageChange)
        {
            Canvas.ForceUpdateCanvases();
            _bodyScrollRect.verticalNormalizedPosition = 1f;
        }

        _bodyScrollRect.onValueChanged.AddListener(HandleScrollChanged);
    }

    private void ApplyLinksForPage(FirstLaunchInfoPageConfig page)
    {
        UnbindLinkButtons();

        IReadOnlyList<FirstLaunchInfoLinkConfig> links = page != null ? page.Links : Array.Empty<FirstLaunchInfoLinkConfig>();
        int buttonCount = _linkButtons != null ? _linkButtons.Length : 0;

        for (int i = 0; i < buttonCount; i++)
        {
            Button button = _linkButtons[i];
            if (button == null)
                continue;

            FirstLaunchInfoLinkConfig link = i < links.Count ? links[i] : null;
            bool visible = link != null && link.IsValid;
            button.gameObject.SetActive(visible);

            if (!visible)
                continue;

            if (_linkButtonTexts != null && i < _linkButtonTexts.Length && _linkButtonTexts[i] != null)
                _linkButtonTexts[i].text = link.Label;

            string url = link.Url;
            UnityAction handler = () => OpenUrl(url);
            button.onClick.AddListener(handler);
            _boundLinkHandlers[button] = handler;
        }
    }

    private void HandleRequirementChanged(bool _)
    {
        UpdatePrimaryButtonState();
    }

    private void HandleScrollChanged(Vector2 _)
    {
        UpdatePrimaryButtonState();
    }

    private void UpdatePrimaryButtonState()
    {
        if (_primaryButton == null)
            return;

        _primaryButton.interactable = !_disablePrimaryUntilRequirementsMet || AreCurrentPageRequirementsMet();
    }

    private bool AreCurrentPageRequirementsMet()
    {
        if (_runtimePages.Count == 0)
            return true;

        FirstLaunchInfoPageConfig page = _runtimePages[Mathf.Clamp(_pageIndex, 0, _runtimePages.Count - 1)];
        if (page == null)
            return true;

        if (page.RequireToggle && _acceptToggle != null && !_acceptToggle.isOn)
            return false;

        if (page.RequireScrollToBottom && !IsScrollAtBottom(page.ScrollBottomThreshold))
            return false;

        return true;
    }

    private bool IsScrollAtBottom(float threshold)
    {
        if (_bodyScrollRect == null || !_bodyScrollRect.vertical)
            return true;

        RectTransform content = _bodyScrollRect.content;
        RectTransform viewport = _bodyScrollRect.viewport != null
            ? _bodyScrollRect.viewport
            : _bodyScrollRect.transform as RectTransform;

        if (content == null || viewport == null)
            return true;

        if (content.rect.height <= viewport.rect.height + 1f)
            return true;

        return _bodyScrollRect.verticalNormalizedPosition <= threshold;
    }

    private bool BuildRuntimePages()
    {
        _runtimePages.Clear();

        if (_config == null)
            return false;

        IReadOnlyList<FirstLaunchInfoPageConfig> pages = _config.Pages;
        for (int i = 0; i < pages.Count; i++)
        {
            FirstLaunchInfoPageConfig page = pages[i];
            if (page != null && page.Enabled)
                _runtimePages.Add(page);
        }

        return _runtimePages.Count > 0;
    }

    private void BindStaticButtons()
    {
        if (_primaryButton != null)
        {
            _primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            _primaryButton.onClick.AddListener(HandlePrimaryClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(HandleBackClicked);
            _backButton.onClick.AddListener(HandleBackClicked);
        }

        if (_declineButton != null)
        {
            _declineButton.onClick.RemoveListener(HandleDeclineClicked);
            _declineButton.onClick.AddListener(HandleDeclineClicked);
        }

        if (_acceptToggle != null)
        {
            _acceptToggle.onValueChanged.RemoveListener(HandleRequirementChanged);
            _acceptToggle.onValueChanged.AddListener(HandleRequirementChanged);
        }

        if (_bodyScrollRect != null)
        {
            _bodyScrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
            _bodyScrollRect.onValueChanged.AddListener(HandleScrollChanged);
        }
    }

    private void UnbindStaticButtons()
    {
        if (_primaryButton != null)
            _primaryButton.onClick.RemoveListener(HandlePrimaryClicked);

        if (_backButton != null)
            _backButton.onClick.RemoveListener(HandleBackClicked);

        if (_declineButton != null)
            _declineButton.onClick.RemoveListener(HandleDeclineClicked);

        if (_acceptToggle != null)
            _acceptToggle.onValueChanged.RemoveListener(HandleRequirementChanged);

        if (_bodyScrollRect != null)
            _bodyScrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
    }

    private void UnbindLinkButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in _boundLinkHandlers)
        {
            if (pair.Key != null)
                pair.Key.onClick.RemoveListener(pair.Value);
        }

        _boundLinkHandlers.Clear();
    }

    private void CaptureAndApplyBlockedObjects()
    {
        _hiddenObjectStates.Clear();
        if (_hideWhileVisible != null)
        {
            for (int i = 0; i < _hideWhileVisible.Length; i++)
            {
                GameObject target = _hideWhileVisible[i];
                if (target == null || _hiddenObjectStates.ContainsKey(target))
                    continue;

                _hiddenObjectStates.Add(target, target.activeSelf);
                target.SetActive(false);
            }
        }

        _blockedCanvasGroupStates.Clear();
        if (_blockWhileVisible == null)
            return;

        for (int i = 0; i < _blockWhileVisible.Length; i++)
        {
            CanvasGroup group = _blockWhileVisible[i];
            if (group == null || _blockedCanvasGroupStates.ContainsKey(group))
                continue;

            _blockedCanvasGroupStates.Add(group, new CanvasGroupState(group));
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private void RestoreBlockedObjects()
    {
        foreach (KeyValuePair<GameObject, bool> pair in _hiddenObjectStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        _hiddenObjectStates.Clear();

        foreach (KeyValuePair<CanvasGroup, CanvasGroupState> pair in _blockedCanvasGroupStates)
        {
            if (pair.Key != null)
                pair.Value.ApplyTo(pair.Key);
        }

        _blockedCanvasGroupStates.Clear();
    }

    private void CaptureAndApplyTimeScale()
    {
        if (!_pauseTimeWhileVisible || _timeScaleCaptured)
            return;

        _capturedTimeScale = Time.timeScale;
        _timeScaleCaptured = true;
        Time.timeScale = 0f;
    }

    private void RestoreTimeScale()
    {
        if (!_timeScaleCaptured)
            return;

        Time.timeScale = _capturedTimeScale;
        _timeScaleCaptured = false;
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

    private RectTransform ResolveAnimatedPanelRoot()
    {
        if (_animatedPanelRoot == null)
            _animatedPanelRoot = transform as RectTransform;

        return _animatedPanelRoot;
    }

    private void StopRunningRoutine()
    {
        if (_showRoutine == null)
            return;

        StopCoroutine(_showRoutine);
        _showRoutine = null;
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        Application.OpenURL(url.Trim());
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void InvokeSafe(UnityEvent unityEvent, string eventName)
    {
        try
        {
            unityEvent?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"FirstLaunchInfoFlowController: event '{eventName}' failed: {exception.Message}", this);
        }
    }

    private readonly struct CanvasGroupState
    {
        private readonly float _alpha;
        private readonly bool _interactable;
        private readonly bool _blocksRaycasts;

        public CanvasGroupState(CanvasGroup group)
        {
            _alpha = group != null ? group.alpha : 1f;
            _interactable = group != null && group.interactable;
            _blocksRaycasts = group != null && group.blocksRaycasts;
        }

        public void ApplyTo(CanvasGroup group)
        {
            if (group == null)
                return;

            group.alpha = _alpha;
            group.interactable = _interactable;
            group.blocksRaycasts = _blocksRaycasts;
        }
    }
}
