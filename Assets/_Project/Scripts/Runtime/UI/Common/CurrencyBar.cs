using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyBar : MonoBehaviour
{
    public static CurrencyBar Instance { get; private set; }

    [Header("References")]
    [SerializeField] TMP_Text heartsText;
    [SerializeField] TMP_Text candlesText;
    [SerializeField] Button heartsShopButton;
    [SerializeField] Button candlesShopButton;

    [SerializeField]
    [Tooltip("Автоматически поменять местами Hearts Text и Candles Text, если по имени или пути объекта видно, что TMP назначены наоборот.")]
    bool autoCorrectSwappedTextReferences = true;

    [Header("Icons")]
    [SerializeField] Image heartsIcon;
    [SerializeField] Image candlesIcon;

    [Header("Server Balance")]
    [SerializeField]
    [Tooltip("При включении верхней панели дождаться авторизации и подтянуть баланс с сервера, чтобы не показывать старые локальные тестовые значения.")]
    bool syncServerBalanceOnEnable = true;

    [SerializeField]
    [Tooltip("Показывать плейсхолдер, пока CurrencyBar ждёт серверный баланс.")]
    bool showLoadingWhileSyncing = true;

    [SerializeField]
    [Tooltip("Текст плейсхолдера баланса во время серверной синхронизации.")]
    string loadingText = "...";

    [SerializeField, Min(0f)]
    [Tooltip("Сколько секунд ждать завершения авторизации перед серверной синхронизацией баланса. Если время вышло, останется локальный fallback.")]
    float authWaitTimeout = 8f;

    [Header("Animation")]
    [SerializeField] Color positiveFlashColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] Color negativeFlashColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] float flashDuration = 0.4f;

    public int DisplayedHearts => _prevHearts >= 0 ? _prevHearts : PlayerData.Hearts;
    public int DisplayedCandles => _prevCandles >= 0 ? _prevCandles : PlayerData.Candles;

    int _prevHearts = -1;
    int _prevCandles = -1;
    Tween _heartsValueTween;
    Tween _heartsColorTween;
    Tween _candlesValueTween;
    Tween _candlesColorTween;
    Coroutine _syncRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        AutoCorrectCurrencyTextReferences();

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (heartsShopButton != null)
            heartsShopButton.onClick.AddListener(OpenShop);

        if (candlesShopButton != null)
            candlesShopButton.onClick.AddListener(OpenShop);

        if (_syncRoutine == null)
            Refresh(animate: false);
    }

    void OnEnable()
    {
        PlayerData.BalanceChanged += HandlePlayerBalanceChanged;
        if (syncServerBalanceOnEnable)
            StartServerBalanceSyncIfNeeded();
        else
            Refresh(animate: false);
    }

    void OnDisable()
    {
        PlayerData.BalanceChanged -= HandlePlayerBalanceChanged;
        StopServerBalanceSync();
        KillTweens();
    }

    void OnDestroy()
    {
        if (heartsShopButton != null)
            heartsShopButton.onClick.RemoveListener(OpenShop);

        if (candlesShopButton != null)
            candlesShopButton.onClick.RemoveListener(OpenShop);

        PlayerData.BalanceChanged -= HandlePlayerBalanceChanged;
        StopServerBalanceSync();
        KillTweens();

        if (Instance == this)
            Instance = null;
    }

    public void Refresh(bool animate = true)
    {
        AutoCorrectCurrencyTextReferences();

        int h = PlayerData.Hearts;
        int c = PlayerData.Candles;

        UpdateText(heartsText, _prevHearts, h, animate, ref _heartsValueTween, ref _heartsColorTween);
        UpdateText(candlesText, _prevCandles, c, animate, ref _candlesValueTween, ref _candlesColorTween);

        _prevHearts = h;
        _prevCandles = c;
    }

    void HandlePlayerBalanceChanged()
    {
        Refresh(animate: true);
    }

    void StartServerBalanceSyncIfNeeded()
    {
        if (!syncServerBalanceOnEnable || !isActiveAndEnabled)
            return;

        StopServerBalanceSync();
        _syncRoutine = StartCoroutine(SyncServerBalanceRoutine());
    }

    void StopServerBalanceSync()
    {
        if (_syncRoutine == null)
            return;

        StopCoroutine(_syncRoutine);
        _syncRoutine = null;
    }

    System.Collections.IEnumerator SyncServerBalanceRoutine()
    {
        if (showLoadingWhileSyncing)
            SetTexts(loadingText);

        float startedAt = Time.unscaledTime;
        while (!NetworkManager.AuthFlowCompleted && Time.unscaledTime - startedAt < authWaitTimeout)
            yield return null;

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            Refresh(animate: false);
            _syncRoutine = null;
            yield break;
        }

        bool synced = false;
        yield return NetworkManager.Instance.SyncBalance(ok => synced = ok);
        Refresh(animate: false);
        _syncRoutine = null;
    }

    void SetTexts(string value)
    {
        value ??= "";

        if (heartsText != null)
            heartsText.text = value;

        if (candlesText != null)
            candlesText.text = value;
    }

    void UpdateText(TMP_Text text, int previous, int current, bool animate, ref Tween valueTween, ref Tween colorTween)
    {
        if (text == null)
            return;

        if (animate && previous >= 0)
            AnimateChange(text, previous, current, ref valueTween, ref colorTween);
        else
            text.text = current.ToString();
    }

    void AnimateChange(TMP_Text text, int from, int to, ref Tween valueTween, ref Tween colorTween)
    {
        if (text == null)
            return;

        valueTween?.Kill();
        colorTween?.Kill();

        valueTween = DOTween.To(
                () => from,
                x =>
                {
                    if (text != null)
                        text.text = x.ToString();
                },
                to,
                0.5f)
            .SetEase(Ease.OutCubic);

        Color flash = to >= from ? positiveFlashColor : negativeFlashColor;
        float halfDuration = Mathf.Max(0f, flashDuration * 0.5f);
        colorTween = DOTween.Sequence()
            .Append(text.DOColor(flash, halfDuration))
            .Append(text.DOColor(Color.white, halfDuration));
    }

    void AutoCorrectCurrencyTextReferences()
    {
        if (!autoCorrectSwappedTextReferences || heartsText == null || candlesText == null)
            return;

        bool heartsLooksLikeCandles = TransformPathContainsAny(heartsText.transform, transform, "candle", "candles", "свеч");
        bool candlesLooksLikeHearts = TransformPathContainsAny(candlesText.transform, transform, "heart", "hearts", "серд", "искр");
        if (!heartsLooksLikeCandles || !candlesLooksLikeHearts)
            return;

        TMP_Text tmp = heartsText;
        heartsText = candlesText;
        candlesText = tmp;
    }

    static bool TransformPathContainsAny(Transform target, Transform stopBefore, params string[] needles)
    {
        string path = BuildLocalTransformPath(target, stopBefore);
        if (string.IsNullOrEmpty(path) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (!string.IsNullOrEmpty(needle) && path.Contains(needle.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    static string BuildLocalTransformPath(Transform target, Transform stopBefore)
    {
        if (target == null)
            return "";

        string path = "";
        Transform current = target;
        int guard = 0;
        while (current != null && current != stopBefore && guard++ < 32)
        {
            path = string.IsNullOrEmpty(path) ? current.name : current.name + "/" + path;
            current = current.parent;
        }

        return path.ToLowerInvariant();
    }

    void OpenShop()
    {
        try
        {
            if (ShopController.Instance != null)
                ShopController.Instance.Open();
            else if (StoryManager.Instance != null)
                StoryManager.Instance.OpenShopForCurrency();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"CurrencyBar: failed to open shop: {exception.Message}", this);
        }
    }

    void KillTweens()
    {
        _heartsValueTween?.Kill();
        _heartsColorTween?.Kill();
        _candlesValueTween?.Kill();
        _candlesColorTween?.Kill();

        _heartsValueTween = null;
        _heartsColorTween = null;
        _candlesValueTween = null;
        _candlesColorTween = null;
    }
}
