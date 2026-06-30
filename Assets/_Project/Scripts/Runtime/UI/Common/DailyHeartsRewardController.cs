using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class DailyHeartsRewardClaimedEvent : UnityEvent<int, int>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Daily Hearts Reward Controller")]
public sealed class DailyHeartsRewardController : MonoBehaviour
{
    [Header("Запуск")]
    [SerializeField]
    [Tooltip("Автоматически проверить серверный daily reward после старта сцены.")]
    private bool _checkOnStart = true;

    [SerializeField]
    [Tooltip("Ждать, пока NetworkManager закончит авторизацию. Daily reward работает по серверному времени, поэтому без авторизации не показывается.")]
    private bool _waitForAuth = true;

    [SerializeField, Min(0f)]
    [Tooltip("Максимальное ожидание авторизации перед первой проверкой. Если сервер не готов, плашка не появится.")]
    private float _authWaitTimeout = 12f;

    [SerializeField]
    [Tooltip("Если включено, после успешного claim будет вызван SyncBalance, чтобы UI точно получил актуальные hearts/candles/streak с сервера.")]
    private bool _syncBalanceAfterClaim = true;

    [SerializeField]
    [Tooltip("Если /player/balance не прислал dailyStreak.canClaim, всё равно показать плашку. Реальную доступность тогда проверит серверный POST /player/daily/claim по серверному времени.")]
    private bool _showWhenAvailabilityUnknown = true;

    [SerializeField]
    [Tooltip("Ждать завершения first-launch плашек перед проверкой и показом ежедневного подарка.")]
    private bool _waitForFirstLaunchFlow = true;

    [SerializeField]
    [Tooltip("Контроллер first-launch цепочки. Если оставить пустым и включить авто-поиск, daily reward попробует найти его в сцене сам.")]
    private FirstLaunchOnboardingStateMachineController _firstLaunchController;

    [SerializeField]
    [Tooltip("Если First Launch Controller не назначен вручную, найти его в сцене, включая выключенные объекты.")]
    private bool _autoFindFirstLaunchController = true;

    [SerializeField, Min(0f)]
    [Tooltip("Максимальное ожидание first-launch цепочки. 0 = ждать без лимита.")]
    private float _firstLaunchWaitTimeout = 120f;

    [Header("UI Root")]
    [SerializeField]
    [Tooltip("Корневой объект daily reward плашки. Можно держать выключенным в сцене.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня для fade и блокировки кликов под плашкой.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("RectTransform визуальной плашки для мягкой scale-анимации.")]
    private RectTransform _panelRoot;

    [SerializeField]
    [Tooltip("Поднимать плашку поверх остальных UI при показе.")]
    private bool _bringToFrontOnShow = true;

    [Header("Иконки")]
    [SerializeField]
    [Tooltip("Image подарка на ежедневной плашке. Спрайт назначается в UI, контроллер только включает/выключает объект при показе.")]
    private Image _giftIcon;

    [SerializeField]
    [Tooltip("Image сердечка на ежедневной плашке. Спрайт назначается в UI, контроллер только включает/выключает объект при показе.")]
    private Image _heartIcon;

    [SerializeField]
    [Tooltip("Если включено, сердечко скрывается, когда серверная награда равна 0. Обычно лучше оставить выключенным для стабильного макета.")]
    private bool _hideHeartIconWhenRewardIsZero;

    [Header("Тексты")]
    [SerializeField]
    [Tooltip("TMP_Text заголовка. Можно оставить пустым, если заголовок статичный в UI.")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("Текст заголовка, например Добро пожаловать!.")]
    private string _title = "Добро пожаловать!";

    [SerializeField]
    [Tooltip("Один общий TMP_Text тела плашки для нового макета. {0} = серверный день стрика. Пример: Добро пожаловать!\\nДень {0}.")]
    private TMP_Text _bodyText;

    [SerializeField]
    [TextArea(2, 4)]
    [Tooltip("Формат bodyText для нового макета. {0} = серверный dailyStreakDay.")]
    private string _bodyFormat = "Добро пожаловать!\nДень {0}";

    [SerializeField]
    [Tooltip("TMP_Text дня стрика. Формат задается ниже.")]
    private TMP_Text _dayText;

    [SerializeField]
    [Tooltip("Формат дня. {0} = серверный dailyStreakDay.")]
    private string _dayFormat = "День {0}";

    [SerializeField]
    [Tooltip("TMP_Text награды. Формат задается ниже.")]
    private TMP_Text _rewardText;

    [SerializeField]
    [Tooltip("TMP_Text количества рядом с иконкой сердца для нового макета, например +2. Использует тот же формат награды.")]
    private TMP_Text _countText;

    [SerializeField]
    [Tooltip("Формат награды. {0} = количество сердечек/искр по серверному dailyStreak.reward.")]
    private string _rewardFormat = "+{0}";

    [SerializeField]
    [Tooltip("TMP_Text статуса: проверка, ошибка, получено.")]
    private TMP_Text _statusText;

    [SerializeField]
    [Tooltip("Статус при проверке сервера.")]
    private string _checkingStatus = "Проверяем награду...";

    [SerializeField]
    [Tooltip("Статус, когда награда доступна.")]
    private string _availableStatus = "Награда доступна";

    [SerializeField]
    [Tooltip("Статус после получения награды.")]
    private string _claimedStatus = "Награда получена";

    [SerializeField]
    [Tooltip("Статус, если сервер не разрешил получить награду.")]
    private string _unavailableStatus = "Сегодня награда уже получена";

    [Header("Кнопки")]
    [SerializeField]
    [Tooltip("Кнопка Забрать награду. По клику вызывает серверный POST /player/daily/claim.")]
    private Button _claimButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки получения.")]
    private TMP_Text _claimButtonText;

    [SerializeField]
    [Tooltip("Текст кнопки получения.")]
    private string _claimButtonLabel = "Забрать награду";

    [SerializeField]
    [Tooltip("Кнопка закрытия. Можно оставить пустой, если закрытие не нужно.")]
    private Button _closeButton;

    [SerializeField]
    [Tooltip("Скрывать плашку автоматически после успешного claim.")]
    private bool _hideAfterClaim = true;

    [SerializeField, Min(0f)]
    [Tooltip("Задержка перед автоскрытием после claim.")]
    private float _hideAfterClaimDelay = 0.45f;

    [Header("Тест UI")]
    [SerializeField]
    [Tooltip("Только для предпросмотра UI в Unity: если включено, Context Menu 'Тест: показать плашку' использует эти числа без сервера.")]
    private bool _allowUnityPreview = true;

    [SerializeField, Min(1)]
    [Tooltip("Тестовый день для предпросмотра UI без сервера.")]
    private int _previewDay = 1;

    [SerializeField, Min(0)]
    [Tooltip("Тестовая награда для предпросмотра UI без сервера.")]
    private int _previewRewardHearts = 2;

    [Header("Дебаг: фейковый подарок")]
    [SerializeField]
    [Tooltip("Editor/Development only. Если включено, daily reward полностью работает как фейковый подарок без сервера: покажет плашку и по кнопке добавит сердечки локально.")]
    private bool _debugUseFakeDailyGift;

    [SerializeField, Min(1)]
    [Tooltip("День, который показывается на фейковом daily reward.")]
    private int _debugFakeDay = 1;

    [SerializeField, Min(0)]
    [Tooltip("Сколько сердечек добавить при фейковом получении подарка.")]
    private int _debugFakeRewardHearts = 2;

    [SerializeField]
    [Tooltip("Если включено, фейковый claim реально добавляет сердечки в PlayerData и обновляет верхние счётчики.")]
    private bool _debugFakeAddsHearts = true;

    [SerializeField, Min(0f)]
    [Tooltip("Искусственная задержка перед фейковым получением, чтобы проверить состояние кнопки и текст статуса.")]
    private float _debugFakeClaimDelay = 0.15f;

    [SerializeField]
    [Tooltip("Если серверный claim успешен, но баланс после ответа/SyncBalance не вырос на размер награды, локально дотянуть баланс. Нужен, если сервер отдаёт reward, но не присылает новый hearts сразу.")]
    private bool _applyLocalGrantWhenClaimBalanceDoesNotIncrease = true;

    [SerializeField]
    [Tooltip("Ежедневный подарок считается именно hearts-наградой. После claim/SyncBalance клиент дотянет hearts до ожидаемого значения, если сервер вернул старый или неполный баланс.")]
    private bool _forceDailyRewardIntoHearts = true;

    [SerializeField]
    [Tooltip("Если после daily claim сервер поднял candles на размер награды, откатить этот свечной прирост. Нужно на случай, если backend выдал daily reward не в ту валюту.")]
    private bool _undoAccidentalDailyCandleGrant = true;

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций.")]
    private bool _useUnscaledTime = true;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность появления.")]
    private float _showDuration = 0.25f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность скрытия.")]
    private float _hideDuration = 0.18f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Стартовый scale плашки при появлении.")]
    private float _showStartScale = 0.96f;

    [SerializeField]
    [Tooltip("Ease появления.")]
    private Ease _showEase = Ease.OutBack;

    [SerializeField]
    [Tooltip("Ease скрытия.")]
    private Ease _hideEase = Ease.InQuart;

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается, когда сервер сказал, что награда доступна и плашка показана.")]
    private UnityEvent _shown = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается после скрытия плашки.")]
    private UnityEvent _hidden = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается после успешного claim. Первый int = сколько hearts показано как награда, второй int = текущий баланс hearts.")]
    private DailyHeartsRewardClaimedEvent _claimed = new DailyHeartsRewardClaimedEvent();

    [SerializeField]
    [Tooltip("Вызывается при ошибке проверки или claim. В string передается текст ошибки.")]
    private UnityEvent<string> _failed = new UnityEvent<string>();

    private Coroutine _routine;
    private int _currentRewardHearts;
    private int _currentDay;
    private Vector3 _panelBaseScale = Vector3.one;

    private void Awake()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolvePanelRoot();

        if (_panelRoot != null)
            _panelBaseScale = _panelRoot.localScale;

        if (_checkOnStart)
            HideVisualsButKeepRootActive();
        else
            HideImmediate();
    }

    private void Start()
    {
        BindButtons();

        if (_checkOnStart)
            CheckServerAndShowIfAvailable();
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
        _authWaitTimeout = Mathf.Max(0f, _authWaitTimeout);
        _firstLaunchWaitTimeout = Mathf.Max(0f, _firstLaunchWaitTimeout);
        _bodyFormat = string.IsNullOrWhiteSpace(_bodyFormat) ? "Добро пожаловать!\nДень {0}" : _bodyFormat;
        _dayFormat = string.IsNullOrWhiteSpace(_dayFormat) ? "День {0}" : _dayFormat;
        _rewardFormat = string.IsNullOrWhiteSpace(_rewardFormat) ? "+{0}" : _rewardFormat;
        _claimButtonLabel = string.IsNullOrWhiteSpace(_claimButtonLabel) ? "Забрать награду" : _claimButtonLabel;
        _hideAfterClaimDelay = Mathf.Max(0f, _hideAfterClaimDelay);
        _previewDay = Mathf.Max(1, _previewDay);
        _previewRewardHearts = SaveDataSanitizer.ClampCurrencyValue(_previewRewardHearts);
        _debugFakeDay = Mathf.Max(1, _debugFakeDay);
        _debugFakeRewardHearts = SaveDataSanitizer.ClampCurrencyValue(_debugFakeRewardHearts);
        _debugFakeClaimDelay = Mathf.Max(0f, _debugFakeClaimDelay);
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showStartScale = Mathf.Max(0.01f, _showStartScale);
    }

    [ContextMenu("Проверить сервер и показать если доступно")]
    public void CheckServerAndShowIfAvailable()
    {
        StopRoutine();
        _routine = StartCoroutine(CheckServerRoutine());
    }

    [ContextMenu("Тест: показать плашку")]
    public void ShowUnityPreview()
    {
        if (!_allowUnityPreview)
            return;

        StopRoutine();
        _currentDay = _previewDay;
        _currentRewardHearts = _previewRewardHearts;
        RenderTexts(_currentDay, _currentRewardHearts, _availableStatus);
        SetClaimInteractable(false);
        _routine = StartCoroutine(ShowRoutine());
    }

    [ContextMenu("ДЕБАГ: показать фейковый подарок")]
    public void ShowDebugFakeGiftNow()
    {
        if (!Application.isPlaying)
            return;

        StopRoutine();
        _routine = StartCoroutine(CheckFakeGiftRoutine());
    }

    public void Claim()
    {
        StopRoutine();
        _routine = StartCoroutine(ClaimRoutine());
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
    }

    private IEnumerator CheckServerRoutine()
    {
        if (_waitForFirstLaunchFlow)
            yield return WaitForFirstLaunchFlow();

        if (IsDebugFakeGiftEnabled())
        {
            yield return CheckFakeGiftRoutine();
            yield break;
        }

        ApplyStatus(_checkingStatus);

        if (_waitForAuth)
            yield return WaitForAuth();

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            Fail("Серверная сессия не готова.");
            _routine = null;
            yield break;
        }

        bool synced = false;
        yield return NetworkManager.Instance.SyncBalance(ok => synced = ok);
        if (!synced)
        {
            Fail("Не удалось проверить ежедневную награду на сервере.");
            _routine = null;
            yield break;
        }

        PlayerBalanceState balance = NetworkManager.LastBalance;
        _currentDay = Mathf.Max(1, balance.dailyStreakDay);
        _currentRewardHearts = ResolveDisplayReward(balance.dailyRewardAmount);

        if (balance.dailyRewardAvailabilityKnown && !balance.dailyRewardCanClaim)
        {
            RenderTexts(_currentDay, _currentRewardHearts, _unavailableStatus);
            _routine = null;
            yield break;
        }

        if (!balance.dailyRewardAvailabilityKnown && !_showWhenAvailabilityUnknown)
        {
            RenderTexts(_currentDay, _currentRewardHearts, _unavailableStatus);
            _routine = null;
            yield break;
        }

        RenderTexts(_currentDay, _currentRewardHearts, _availableStatus);
        SetClaimInteractable(true);
        yield return ShowRoutine();
        _routine = null;
    }

    private IEnumerator ClaimRoutine()
    {
        if (IsDebugFakeGiftEnabled())
        {
            yield return ClaimFakeGiftRoutine();
            yield break;
        }

        SetClaimInteractable(false);
        ApplyStatus(_checkingStatus);
        int heartsBeforeClaim = PlayerData.Hearts;
        int candlesBeforeClaim = PlayerData.Candles;

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            Fail("Серверная сессия не готова.");
            SetClaimInteractable(true);
            _routine = null;
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.ClaimDailyReward((ok, json) =>
        {
            if (ok)
                payload = json;
            else
                error = json;
        });

        if (string.IsNullOrWhiteSpace(payload))
        {
            Fail(string.IsNullOrWhiteSpace(error) ? "Сервер не выдал ежедневную награду." : error);
            SetClaimInteractable(true);
            _routine = null;
            yield break;
        }

        int reward = ResolveHeartsRewardForClaim(payload);
        if (_syncBalanceAfterClaim)
            yield return NetworkManager.Instance.SyncBalance();

        if (_applyLocalGrantWhenClaimBalanceDoesNotIncrease || _forceDailyRewardIntoHearts)
            EnsureRewardAppliedLocally(reward, heartsBeforeClaim, candlesBeforeClaim);

        PlayerBalanceState balance = NetworkManager.LastBalance;
        _currentDay = Mathf.Max(1, balance.dailyStreakDay);
        _currentRewardHearts = reward;
        RenderTexts(_currentDay, _currentRewardHearts, _claimedStatus);
        RefreshCurrencyViews();
        _claimed.Invoke(reward, PlayerData.Hearts);

        if (_hideAfterClaim)
        {
            if (_hideAfterClaimDelay > 0f)
                yield return Wait(_hideAfterClaimDelay);

            yield return HideRoutine();
        }

        _routine = null;
    }

    private IEnumerator CheckFakeGiftRoutine()
    {
        _currentDay = Mathf.Max(1, _debugFakeDay);
        _currentRewardHearts = SaveDataSanitizer.ClampCurrencyValue(_debugFakeRewardHearts);
        RenderTexts(_currentDay, _currentRewardHearts, _availableStatus);
        SetClaimInteractable(true);
        yield return ShowRoutine();
        _routine = null;
    }

    private IEnumerator ClaimFakeGiftRoutine()
    {
        SetClaimInteractable(false);
        ApplyStatus(_checkingStatus);

        if (_debugFakeClaimDelay > 0f)
            yield return Wait(_debugFakeClaimDelay);

        int reward = SaveDataSanitizer.ClampCurrencyValue(_currentRewardHearts > 0 ? _currentRewardHearts : _debugFakeRewardHearts);
        int heartsBeforeClaim = PlayerData.Hearts;
        int candlesBeforeClaim = PlayerData.Candles;
        if (_debugFakeAddsHearts)
            EnsureRewardAppliedLocally(reward, heartsBeforeClaim, candlesBeforeClaim);

        _currentDay = Mathf.Max(1, _currentDay > 0 ? _currentDay : _debugFakeDay);
        _currentRewardHearts = reward;
        RenderTexts(_currentDay, _currentRewardHearts, _claimedStatus);
        RefreshCurrencyViews();
        _claimed.Invoke(reward, PlayerData.Hearts);

        if (_hideAfterClaim)
        {
            if (_hideAfterClaimDelay > 0f)
                yield return Wait(_hideAfterClaimDelay);

            yield return HideRoutine();
        }

        _routine = null;
    }

    private IEnumerator ShowRoutine()
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

        Tween fade = group != null ? group.DOFade(1f, _showDuration).SetEase(Ease.OutQuart).SetUpdate(_useUnscaledTime) : null;
        Tween scale = panel != null ? panel.DOScale(_panelBaseScale, _showDuration).SetEase(_showEase).SetUpdate(_useUnscaledTime) : null;
        yield return WaitTweens(fade, scale, _showDuration);

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

        _hidden.Invoke();
        _routine = null;

        GameObject root = ResolveRoot();
        if (root != null)
            root.SetActive(false);
    }

    private IEnumerator WaitForAuth()
    {
        float startedAt = Time.unscaledTime;
        while (!NetworkManager.AuthFlowCompleted && Time.unscaledTime - startedAt < _authWaitTimeout)
            yield return null;
    }

    private IEnumerator WaitForFirstLaunchFlow()
    {
        FirstLaunchOnboardingStateMachineController controller = ResolveFirstLaunchController();
        if (controller == null || !controller.isActiveAndEnabled)
            yield break;

        float startedAt = Time.unscaledTime;
        while (controller != null && controller.isActiveAndEnabled && controller.IsStartupFlowPendingOrRunning)
        {
            if (_firstLaunchWaitTimeout > 0f && Time.unscaledTime - startedAt >= _firstLaunchWaitTimeout)
                yield break;

            yield return null;
        }
    }

    private FirstLaunchOnboardingStateMachineController ResolveFirstLaunchController()
    {
        if (_firstLaunchController != null || !_autoFindFirstLaunchController)
            return _firstLaunchController;

#if UNITY_2023_1_OR_NEWER
        _firstLaunchController = FindFirstObjectByType<FirstLaunchOnboardingStateMachineController>(FindObjectsInactive.Include);
#else
        _firstLaunchController = FindObjectOfType<FirstLaunchOnboardingStateMachineController>(true);
#endif
        return _firstLaunchController;
    }

    private IEnumerator Wait(float seconds)
    {
        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < seconds)
            yield return null;
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

    private bool IsDebugFakeGiftEnabled()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return _debugUseFakeDailyGift;
#else
        return false;
#endif
    }

    private void EnsureRewardAppliedLocally(int reward, int heartsBeforeClaim, int candlesBeforeClaim)
    {
        reward = SaveDataSanitizer.ClampCurrencyValue(reward);
        if (reward <= 0)
            return;

        int expectedHearts = SaveDataSanitizer.ClampCurrencyDelta(heartsBeforeClaim, reward);
        if ((_forceDailyRewardIntoHearts || PlayerData.Hearts < expectedHearts) && PlayerData.Hearts < expectedHearts)
            PlayerData.SetHeartsValue(expectedHearts);

        bool candlesLookLikeDailyReward = PlayerData.Candles == SaveDataSanitizer.ClampCurrencyDelta(candlesBeforeClaim, reward);
        if (_undoAccidentalDailyCandleGrant && candlesLookLikeDailyReward && PlayerData.Hearts >= expectedHearts)
            PlayerData.SetCandlesValue(candlesBeforeClaim);

        PlayerBalanceState balance = NetworkManager.LastBalance;
        if (balance != null)
        {
            balance.hearts = PlayerData.Hearts;
            balance.candles = PlayerData.Candles;
            balance.dailyRewardCanClaim = false;
            balance.dailyRewardAvailabilityKnown = true;
            balance.dailyRewardAmount = reward;
        }
    }

    private int ResolveHeartsRewardForClaim(string payload)
    {
        int fallback = FirstPositive(_currentRewardHearts, _previewRewardHearts, _debugFakeRewardHearts);
        int reward = ResolveClaimedReward(payload, fallback);
        if (reward <= 0)
            reward = fallback;

        return SaveDataSanitizer.ClampCurrencyValue(reward);
    }

    private static int FirstPositive(params int[] values)
    {
        if (values == null)
            return 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > 0)
                return values[i];
        }

        return 0;
    }

    private void RefreshCurrencyViews()
    {
        if (CurrencyBar.Instance != null)
            CurrencyBar.Instance.Refresh(true);

        if (ShopController.Instance != null)
            ShopController.Instance.RefreshDisplayedBalance();
    }

    private int ResolveDisplayReward(int serverReward)
    {
        int normalized = SaveDataSanitizer.ClampCurrencyValue(serverReward);
        if (normalized > 0)
            return normalized;

        return SaveDataSanitizer.ClampCurrencyValue(_previewRewardHearts);
    }

    private int ResolveClaimedReward(string payload, int fallback)
    {
        int reward = NetworkJson.GetInt(payload, "reward", -1);
        if (reward >= 0)
            return SaveDataSanitizer.ClampCurrencyValue(reward);

        reward = NetworkJson.GetInt(payload, "heartsReward", -1);
        if (reward >= 0)
            return SaveDataSanitizer.ClampCurrencyValue(reward);

        string rawReward = NetworkJson.GetRawValue(payload, "reward");
        if (!string.IsNullOrWhiteSpace(rawReward) && NetworkJson.LooksLikeJsonObject(rawReward))
        {
            int hearts = NetworkJson.GetInt(rawReward, "hearts", -1);
            if (hearts >= 0)
                return SaveDataSanitizer.ClampCurrencyValue(hearts);
        }

        string rawDailyStreak = NetworkJson.GetRawValue(payload, "dailyStreak");
        if (!string.IsNullOrWhiteSpace(rawDailyStreak) && NetworkJson.LooksLikeJsonObject(rawDailyStreak))
        {
            int dailyReward = NetworkJson.GetInt(rawDailyStreak, "reward", -1);
            if (dailyReward >= 0)
                return SaveDataSanitizer.ClampCurrencyValue(dailyReward);
        }

        return SaveDataSanitizer.ClampCurrencyValue(fallback);
    }

    private void RenderTexts(int day, int hearts, string status)
    {
        SetIconVisible(_giftIcon, true);
        SetIconVisible(_heartIcon, !_hideHeartIconWhenRewardIsZero || hearts > 0);
        SetText(_titleText, _title);
        SetFormattedText(_bodyText, _bodyFormat, day);
        SetFormattedText(_dayText, _dayFormat, day);
        SetFormattedText(_rewardText, _rewardFormat, hearts);
        SetFormattedText(_countText, _rewardFormat, hearts);
        SetText(_claimButtonText, _claimButtonLabel);
        ApplyStatus(status);
    }

    private void BindButtons()
    {
        if (_claimButton != null)
        {
            _claimButton.onClick.RemoveListener(Claim);
            _claimButton.onClick.AddListener(Claim);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
            _closeButton.onClick.AddListener(Hide);
        }
    }

    private void UnbindButtons()
    {
        if (_claimButton != null)
            _claimButton.onClick.RemoveListener(Claim);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
    }

    private void SetClaimInteractable(bool interactable)
    {
        if (_claimButton != null)
            _claimButton.interactable = interactable;
    }

    private void Fail(string message)
    {
        ApplyStatus(message);
        _failed.Invoke(message ?? "");
    }

    private void ApplyStatus(string status)
    {
        SetText(_statusText, status);
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

    private static void SetIconVisible(Image image, bool visible)
    {
        if (image != null)
            image.gameObject.SetActive(visible);
    }

    private static void SetFormattedText(TMP_Text text, string format, int value)
    {
        if (text == null)
            return;

        try
        {
            text.text = string.Format(format, value);
        }
        catch (FormatException)
        {
            text.text = value.ToString();
        }
    }
}
