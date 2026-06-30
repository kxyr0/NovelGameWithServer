using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public sealed class AdRewardClaimedEvent : UnityEvent<int>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Ad Reward Popup Controller")]
public sealed class AdRewardPopupController : MonoBehaviour
{
    [Header("Серверная награда")]
    [SerializeField]
    [Tooltip("Сколько сердечек/искр показывать, если сервер не вернул точное число награды. Для твоего макета: 2.")]
    private int _fallbackRewardHearts = 2;

    [SerializeField]
    [Tooltip("После успешного серверного claim вызвать SyncBalance, чтобы UI точно получил актуальный баланс.")]
    private bool _syncBalanceAfterClaim = true;

    [SerializeField]
    [Tooltip("Обновить CurrencyBar после успешного claim награды за рекламу.")]
    private bool _refreshCurrencyBarAfterClaim = true;

    [Header("UI Root")]
    [SerializeField]
    [Tooltip("Корневой объект плашки награды после рекламы. Объект со скриптом должен быть активен, Root можно скрывать этим контроллером.")]
    private GameObject _root;

    [SerializeField]
    [Tooltip("CanvasGroup корня для fade-анимации и блокировки кликов под плашкой.")]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    [Tooltip("Визуальная панель плашки для scale-анимации.")]
    private RectTransform _panelRoot;

    [SerializeField]
    [Tooltip("Поднимать Root поверх остальных UI при показе.")]
    private bool _bringToFrontOnShow = true;

    [Header("Тексты")]
    [SerializeField]
    [Tooltip("TMP_Text заголовка. В него будет подставлен формат: Награда получена! +{0}♥")]
    private TMP_Text _titleText;

    [SerializeField]
    [Tooltip("Формат заголовка. {0} = количество сердечек/искр.")]
    private string _titleFormat = "Награда получена! +{0}♥";

    [SerializeField]
    [Tooltip("TMP_Text статуса/ошибки. Можно оставить пустым, если статус не нужен.")]
    private TMP_Text _statusText;

    [SerializeField]
    [Tooltip("Статус во время серверной выдачи награды.")]
    private string _claimingStatus = "Выдаём награду...";

    [SerializeField]
    [Tooltip("Статус, когда награда успешно получена.")]
    private string _readyStatus = "Награда получена";

    [Header("Кнопки")]
    [SerializeField]
    [Tooltip("Кнопка Забрать. Награда уже выдана сервером, кнопка просто закрывает плашку и вызывает событие Collected.")]
    private Button _collectButton;

    [SerializeField]
    [Tooltip("TMP_Text кнопки Забрать.")]
    private TMP_Text _collectButtonText;

    [SerializeField]
    [Tooltip("Текст кнопки закрытия плашки после награды.")]
    private string _collectButtonLabel = "Забрать";

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("Использовать unscaled time для анимаций.")]
    private bool _useUnscaledTime = true;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность появления.")]
    private float _showDuration = 0.22f;

    [SerializeField, Min(0f)]
    [Tooltip("Длительность скрытия.")]
    private float _hideDuration = 0.16f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Стартовый scale плашки при появлении.")]
    private float _showStartScale = 0.94f;

    [SerializeField]
    [Tooltip("Ease появления.")]
    private Ease _showEase = Ease.OutBack;

    [SerializeField]
    [Tooltip("Ease скрытия.")]
    private Ease _hideEase = Ease.InQuart;

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается после успешного серверного claim и показа плашки. int = количество показанной награды.")]
    private AdRewardClaimedEvent _claimed = new AdRewardClaimedEvent();

    [SerializeField]
    [Tooltip("Вызывается при нажатии Забрать.")]
    private UnityEvent _collected = new UnityEvent();

    [SerializeField]
    [Tooltip("Вызывается при ошибке серверной выдачи. string = текст ошибки.")]
    private UnityEvent<string> _failed = new UnityEvent<string>();

    private Coroutine _routine;
    private Vector3 _panelBaseScale = Vector3.one;
    private int _currentRewardHearts;

    private void Awake()
    {
        ResolveRoot();
        ResolveCanvasGroup();
        ResolvePanelRoot();

        if (_panelRoot != null)
            _panelBaseScale = _panelRoot.localScale;

        BindButtons();
        HideImmediate();
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
        _fallbackRewardHearts = SaveDataSanitizer.ClampCurrencyValue(_fallbackRewardHearts);
        _titleFormat = string.IsNullOrWhiteSpace(_titleFormat) ? "Награда получена! +{0}♥" : _titleFormat;
        _collectButtonLabel = string.IsNullOrWhiteSpace(_collectButtonLabel) ? "Забрать" : _collectButtonLabel;
        _showDuration = Mathf.Max(0f, _showDuration);
        _hideDuration = Mathf.Max(0f, _hideDuration);
        _showStartScale = Mathf.Max(0.01f, _showStartScale);
    }

    [ContextMenu("Тест: показать +2")]
    public void ShowPreview()
    {
        ShowReward(_fallbackRewardHearts);
    }

    [ContextMenu("Сервер: забрать награду за рекламу и показать")]
    public void ClaimServerAdRewardAndShow()
    {
        StopRoutine();
        _routine = StartCoroutine(ClaimServerAdRewardRoutine());
    }

    public void ShowReward(int hearts)
    {
        StopRoutine();
        _currentRewardHearts = SaveDataSanitizer.ClampCurrencyValue(hearts <= 0 ? _fallbackRewardHearts : hearts);
        RenderTexts(_currentRewardHearts, _readyStatus);
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
    }

    public void CollectAndHide()
    {
        _collected.Invoke();
        Hide();
    }

    private IEnumerator ClaimServerAdRewardRoutine()
    {
        RenderTexts(_fallbackRewardHearts, _claimingStatus);

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            Fail("Серверная сессия не готова.");
            _routine = null;
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.ClaimAdReward((ok, json) =>
        {
            if (ok)
                payload = json;
            else
                error = json;
        });

        if (string.IsNullOrWhiteSpace(payload))
        {
            Fail(string.IsNullOrWhiteSpace(error) ? "Сервер не выдал награду за рекламу." : error);
            _routine = null;
            yield break;
        }

        int reward = ResolveReward(payload, _fallbackRewardHearts);
        if (_syncBalanceAfterClaim)
            yield return NetworkManager.Instance.SyncBalance();

        if (_refreshCurrencyBarAfterClaim && CurrencyBar.Instance != null)
            CurrencyBar.Instance.Refresh(true);

        _currentRewardHearts = reward;
        RenderTexts(_currentRewardHearts, _readyStatus);
        _claimed.Invoke(_currentRewardHearts);
        yield return ShowRoutine();
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

        _routine = null;

        GameObject root = ResolveRoot();
        if (root != null)
            root.SetActive(false);
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

    private int ResolveReward(string payload, int fallback)
    {
        int reward = NetworkJson.GetInt(payload, "reward", -1);
        if (reward >= 0)
            return SaveDataSanitizer.ClampCurrencyValue(reward);

        reward = NetworkJson.GetInt(payload, "heartsReward", -1);
        if (reward >= 0)
            return SaveDataSanitizer.ClampCurrencyValue(reward);

        reward = NetworkJson.GetInt(payload, "adReward", -1);
        if (reward >= 0)
            return SaveDataSanitizer.ClampCurrencyValue(reward);

        string rawReward = NetworkJson.GetRawValue(payload, "reward");
        if (!string.IsNullOrWhiteSpace(rawReward) && NetworkJson.LooksLikeJsonObject(rawReward))
        {
            int hearts = NetworkJson.GetInt(rawReward, "hearts", -1);
            if (hearts >= 0)
                return SaveDataSanitizer.ClampCurrencyValue(hearts);
        }

        return SaveDataSanitizer.ClampCurrencyValue(fallback);
    }

    private void RenderTexts(int hearts, string status)
    {
        if (_titleText != null)
        {
            try
            {
                _titleText.text = string.Format(_titleFormat, hearts);
            }
            catch (FormatException)
            {
                _titleText.text = "Награда получена! +" + hearts + "♥";
            }
        }

        SetText(_collectButtonText, _collectButtonLabel);
        SetText(_statusText, status);
    }

    private void BindButtons()
    {
        if (_collectButton == null)
            return;

        _collectButton.onClick.RemoveListener(CollectAndHide);
        _collectButton.onClick.AddListener(CollectAndHide);
    }

    private void UnbindButtons()
    {
        if (_collectButton != null)
            _collectButton.onClick.RemoveListener(CollectAndHide);
    }

    private void Fail(string message)
    {
        SetText(_statusText, message);
        _failed.Invoke(message ?? "");
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
