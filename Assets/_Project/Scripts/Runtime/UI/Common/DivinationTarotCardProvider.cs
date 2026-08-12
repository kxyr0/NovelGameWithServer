using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public enum DivinationTarotSourceMode
{
    ServerOnly = 0,
    UnityTestOnly = 1,
    ServerThenUnityTestFallback = 2
}

public enum DivinationTarotAnimationStartMode
{
    None = 0,
    TriggerAnimation = 1,
    PlayFromStart = 2
}

[Serializable]
public sealed class DivinationTarotCardEvent : UnityEvent<DivinationTarotCardRuntimeData>
{
}

[Serializable]
public sealed class DivinationTarotCardRuntimeData
{
    public string id;
    public string name;
    public string title;
    public string description;
    public string imageUrl;
    public int heartsReward;
    public int candlesReward;
    public int subscriptionDaysReward;
    public float weight;
    public bool active = true;
    public DivinationRewardDto[] rewards;
    public DivinationCooldownDto cooldown;
    public DivinationCardBackendDto backendCard;
    public Sprite sprite;
    public bool fromServer;
    public string rawJson;
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Divination Tarot Card Provider")]
public sealed class DivinationTarotCardProvider : MonoBehaviour
{
    [Header("Источник карты")]
    [SerializeField]
    [Tooltip("Откуда брать карту: только серверные карты из админки, только локальная тестовая колода или сервер с запасным переходом на Unity-тест.")]
    private DivinationTarotSourceMode _sourceMode = DivinationTarotSourceMode.ServerOnly;

    [SerializeField]
    [Tooltip("Локальная тестовая колода для проверки UI без админки и без опубликованных серверных карт.")]
    private DivinationTarotTestDeckConfig _unityTestDeck;

    [SerializeField]
    [Tooltip("Запасной спрайт, если сервер вернул карту без картинки или загрузка imageUrl не удалась.")]
    private Sprite _fallbackCardSprite;

    [Header("Интеграция отображения карты")]
    [SerializeField]
    [Tooltip("Необязательный API-клиент для документированных запросов /player/tarot/status и /player/tarot/draw.")]
    private DivinationApiClient _apiClient;

    [SerializeField]
    [Tooltip("Необязательный контроллер отображения: сопоставляет серверный ID карты с локальным спрайтом из инспектора Unity и применяет текст сервера.")]
    private DivinationCardDisplayController _cardDisplayController;

    [SerializeField]
    [Tooltip("Перед вытягиванием проверять /player/tarot/status и показывать серверный кулдаун, если карта недоступна.")]
    private bool _fetchStatusBeforeDraw = true;

    [SerializeField]
    [Tooltip("Только для Editor/Development Build: не блокировать вытягивание по серверному кулдауну на стороне клиента. Backend всё равно может отказать в /player/tarot/draw.")]
    private bool _ignoreCooldownInDebug = false;

    [SerializeField]
    [Tooltip("Только для Editor/Development Build: если backend отклонил draw по кулдауну, показать локальную карту из Card Display Controller для проверки UI.")]
    private bool _useLocalCardWhenDebugCooldownRequestFails = true;

    [SerializeField]
    [Tooltip("Передавать карту без спрайта в контроллер отображения. Контроллер сможет безопасно оставить предыдущий или запасной спрайт.")]
    private bool _allowCardWithoutSpriteWhenDisplayControllerAssigned = true;

    [Header("Анимация")]
    [SerializeField]
    [Tooltip("SpriteFrameAnimator с флешкой и финальной картой. Скрипт подставит карту в SetFinalCardSprites перед запуском анимации.")]
    private SpriteFrameAnimator _spriteFrameAnimator;

    [SerializeField]
    [Tooltip("Как запускать анимацию после подготовки карты. TriggerAnimation подходит, если в SpriteFrameAnimator включен Requires Trigger.")]
    private DivinationTarotAnimationStartMode _animationStartMode = DivinationTarotAnimationStartMode.TriggerAnimation;

    [SerializeField]
    [Tooltip("Если включено, перед запуском новой карты снимается флаг остановки на финальной карте у SpriteFrameAnimator.")]
    private bool _clearFinalCardStopBeforePlay = true;

    [Header("UI")]
    [SerializeField]
    [Tooltip("Кнопка колоды/получения карты. Если включен Auto Bind Button, клик сам вызовет DrawServerOrTestCardAndTriggerAnimation.")]
    private Button _drawButton;

    [SerializeField]
    [Tooltip("Автоматически подписать Draw Button на получение карты.")]
    private bool _autoBindButton = true;

    [SerializeField]
    [Tooltip("Image для предпросмотра/прямой подстановки карты. Можно оставить пустым, если карту должен менять только SpriteFrameAnimator после флешки.")]
    private Image _previewCardImage;

    [SerializeField]
    [Tooltip("Если включено, Preview Card Image обновится сразу после подготовки карты, ещё до флешки.")]
    private bool _applyPreviewImageBeforeFlash;

    [SerializeField]
    [Tooltip("TMP_Text для названия карты.")]
    private TMP_Text _cardNameText;

    [SerializeField]
    [Tooltip("TMP_Text для описания карты.")]
    private TMP_Text _cardDescriptionText;

    [SerializeField]
    [Tooltip("TMP_Text для награды карты. Формат задается ниже.")]
    private TMP_Text _rewardText;

    [SerializeField]
    [Tooltip("Формат текста награды. {0} = hearts, {1} = candles, {2} = subscriptionDays.")]
    private string _rewardFormat = "+{0}";

    [SerializeField]
    [Tooltip("TMP_Text статуса: загрузка, ошибка, кулдаун, готово.")]
    private TMP_Text _statusText;

    [Header("Серверная картинка")]
    [SerializeField]
    [Tooltip("Загружать imageUrl серверной карты как Texture2D и создавать Sprite на лету.")]
    private bool _loadServerImage = true;

    [SerializeField, Min(1)]
    [Tooltip("Таймаут загрузки картинки карты с сервера в секундах.")]
    private int _imageRequestTimeoutSeconds = 12;

    [SerializeField]
    [Tooltip("Если imageUrl начинается с '/', к нему будет добавлен текущий базовый URL из NetworkManager или ApiRoutes.BaseUrl.")]
    private bool _resolveRelativeImageUrl = true;

    [Header("Тексты статуса")]
    [SerializeField]
    [Tooltip("Статус во время запроса карты.")]
    private string _loadingStatus = "Получаем карту...";

    [SerializeField]
    [Tooltip("Статус после успешной подготовки карты.")]
    private string _readyStatus = "Карта готова";

    [SerializeField]
    [Tooltip("Статус, если сервер запретил вытянуть карту или случилась ошибка.")]
    private string _errorStatus = "Карта пока недоступна";

    [Header("События")]
    [SerializeField]
    [Tooltip("Вызывается, когда карта успешно подготовлена.")]
    private DivinationTarotCardEvent _cardPrepared = new DivinationTarotCardEvent();

    [SerializeField]
    [Tooltip("Вызывается, когда карту получить не удалось. В строку передается текст ошибки.")]
    private UnityEvent<string> _failed = new UnityEvent<string>();

    private Coroutine _drawRoutine;
    private DivinationTarotCardRuntimeData _preparedCard;
    private Sprite _ownedRuntimeSprite;
    private bool _waitingForTextRevealAfterAnimation;

    public DivinationTarotCardRuntimeData PreparedCard => _preparedCard;

    private void Awake()
    {
        ResolveAnimator();
    }

    private void OnEnable()
    {
        BindButton();
    }

    private void OnDisable()
    {
        StopWaitingForTextRevealAfterAnimation();
        UnbindButton();
    }

    private void OnDestroy()
    {
        StopRunningRoutine();
        DestroyOwnedRuntimeSprite();
    }

    private void OnValidate()
    {
        _imageRequestTimeoutSeconds = Mathf.Max(1, _imageRequestTimeoutSeconds);
        _rewardFormat = string.IsNullOrWhiteSpace(_rewardFormat) ? "+{0}" : _rewardFormat;
    }

    [ContextMenu("Тест: взять карту из Unity")]
    public void UseRandomUnityTestCardAndTriggerAnimation()
    {
        StopRunningRoutine();
        _drawRoutine = StartCoroutine(PrepareUnityTestCardAndMaybeTrigger(true));
    }

    [ContextMenu("Получить карту и запустить анимацию")]
    public void DrawServerOrTestCardAndTriggerAnimation()
    {
        StopRunningRoutine();
        _drawRoutine = StartCoroutine(DrawRoutine(triggerAnimation: true));
    }

    [ContextMenu("Только подготовить карту")]
    public void PrepareCardOnly()
    {
        StopRunningRoutine();
        _drawRoutine = StartCoroutine(DrawRoutine(triggerAnimation: false));
    }

    public void TriggerAnimationWithPreparedCard()
    {
        if (_preparedCard == null)
        {
            ApplyStatus(_errorStatus);
            return;
        }

        HideCardTexts();
        ApplyPreparedCardToAnimator(_preparedCard);
        bool revealTextsAfterAnimation = ShouldRevealCardTextsAfterAnimation();
        if (revealTextsAfterAnimation)
            WaitForTextRevealAfterAnimation();
        else
            ShowCardTexts();

        StartConfiguredAnimation();

        if (revealTextsAfterAnimation && (_spriteFrameAnimator == null || !_spriteFrameAnimator.IsPlaying))
            RevealCardTextsAfterAnimation();
    }

    private IEnumerator DrawRoutine(bool triggerAnimation)
    {
        SetDrawButtonInteractable(false);
        ApplyStatus(_loadingStatus);
        HideCardTexts();

        DivinationTarotCardRuntimeData card = null;
        string error = "";
        bool serverCooldownBlocked = false;

        if (_sourceMode != DivinationTarotSourceMode.UnityTestOnly && _fetchStatusBeforeDraw)
        {
            DivinationTarotStatusResponseDto status = null;
            string statusError = "";
            yield return TryFetchServerStatus((result, err) => { status = result; statusError = err; });

            if (status != null)
            {
                ApplyCooldownToDisplay(status.cooldown);
                if (!status.IsDrawAvailable(true))
                {
                    if (ShouldIgnoreCooldownInDebug())
                    {
                        Debug.LogWarning("[Divination] debug cooldown bypass: status says draw is unavailable, draw request will still be attempted.", this);
                    }
                    else
                    {
                        serverCooldownBlocked = true;
                        error = DivinationCooldownFormatter.Format(status.cooldown, _errorStatus);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(statusError))
            {
                Debug.LogWarning("[Divination] tarot status request skipped/failed: " + statusError, this);
            }
        }

        if (_sourceMode != DivinationTarotSourceMode.UnityTestOnly && !serverCooldownBlocked)
            yield return TryDrawServerCard((result, err) => { card = result; error = err; });

        if (card == null && ShouldUseLocalDebugCooldownFallback(error))
            TryPrepareDebugCooldownFallbackCard(out card);

        bool canUseUnityFallback =
            card == null &&
            !serverCooldownBlocked &&
            (_sourceMode == DivinationTarotSourceMode.UnityTestOnly ||
             _sourceMode == DivinationTarotSourceMode.ServerThenUnityTestFallback);

        if (canUseUnityFallback)
            yield return TryPrepareUnityTestCard((result, err) => { card = result; error = err; });

        if (card == null)
        {
            Fail(string.IsNullOrWhiteSpace(error) ? _errorStatus : error);
            SetDrawButtonInteractable(true);
            _drawRoutine = null;
            yield break;
        }

        bool revealTextsAfterAnimation = triggerAnimation && ShouldRevealCardTextsAfterAnimation();
        ApplyPreparedCard(card, !revealTextsAfterAnimation);

        if (triggerAnimation)
        {
            if (revealTextsAfterAnimation)
                WaitForTextRevealAfterAnimation();

            StartConfiguredAnimation();

            if (revealTextsAfterAnimation && (_spriteFrameAnimator == null || !_spriteFrameAnimator.IsPlaying))
                RevealCardTextsAfterAnimation();
        }

        SetDrawButtonInteractable(true);
        _drawRoutine = null;
    }

    private IEnumerator PrepareUnityTestCardAndMaybeTrigger(bool triggerAnimation)
    {
        SetDrawButtonInteractable(false);
        ApplyStatus(_loadingStatus);
        HideCardTexts();

        DivinationTarotCardRuntimeData card = null;
        string error = "";
        yield return TryPrepareUnityTestCard((result, err) => { card = result; error = err; });

        if (card == null)
        {
            Fail(string.IsNullOrWhiteSpace(error) ? _errorStatus : error);
            SetDrawButtonInteractable(true);
            _drawRoutine = null;
            yield break;
        }

        bool revealTextsAfterAnimation = triggerAnimation && ShouldRevealCardTextsAfterAnimation();
        ApplyPreparedCard(card, !revealTextsAfterAnimation);
        if (triggerAnimation)
        {
            if (revealTextsAfterAnimation)
                WaitForTextRevealAfterAnimation();

            StartConfiguredAnimation();

            if (revealTextsAfterAnimation && (_spriteFrameAnimator == null || !_spriteFrameAnimator.IsPlaying))
                RevealCardTextsAfterAnimation();
        }

        SetDrawButtonInteractable(true);
        _drawRoutine = null;
    }

    private IEnumerator TryFetchServerStatus(Action<DivinationTarotStatusResponseDto, string> callback)
    {
        if (_apiClient != null)
        {
            yield return _apiClient.FetchStatus(callback);
            yield break;
        }

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            callback?.Invoke(null, "Network session is not authenticated.");
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.FetchTarotStatus((json, err) =>
        {
            payload = json;
            error = err;
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            callback?.Invoke(null, error);
            yield break;
        }

        DivinationTarotStatusResponseDto status = DivinationBackendJsonParser.ParseStatusResponse(payload);
        callback?.Invoke(status, status == null ? "Tarot status parse failed." : "");
    }

    private IEnumerator TryDrawServerCard(Action<DivinationTarotCardRuntimeData, string> callback)
    {
        if (_apiClient != null)
        {
            yield return TryDrawServerCardWithApiClient(callback);
            yield break;
        }

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            callback?.Invoke(null, "Серверная сессия ещё не готова.");
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.DrawTarot((ok, json) =>
        {
            if (ok)
                payload = json;
            else
                error = json;
        });

        if (string.IsNullOrWhiteSpace(payload))
        {
            callback?.Invoke(null, string.IsNullOrWhiteSpace(error) ? _errorStatus : error);
            yield break;
        }

        DivinationTarotDrawResponseDto drawResponse = DivinationBackendJsonParser.ParseDrawResponse(payload);
        if (ShouldBlockDrawResponseByCooldown(drawResponse))
        {
            ApplyCooldownToDisplay(drawResponse.cooldown);
            callback?.Invoke(null, DivinationCooldownFormatter.Format(drawResponse.cooldown, _errorStatus));
            yield break;
        }

        DivinationTarotCardRuntimeData card = BuildRuntimeCard(drawResponse);
        if (card == null)
        {
            callback?.Invoke(null, "Сервер вернул карту в неизвестном формате.");
            yield break;
        }

        TryApplyConfiguredFrontendSprite(card);

        if (card.sprite == null && _loadServerImage && !string.IsNullOrWhiteSpace(card.imageUrl))
            yield return LoadCardSprite(card);

        if (card.sprite == null)
            TryApplyDisplayFallbackSprite(card);

        if (card.sprite == null)
            card.sprite = _fallbackCardSprite;

        if (card.sprite == null && CanContinueWithoutSprite())
        {
            callback?.Invoke(card, "");
            yield break;
        }

        if (card.sprite == null)
        {
            callback?.Invoke(null, "У карты нет картинки.");
            yield break;
        }

        callback?.Invoke(card, "");
    }

    private IEnumerator TryDrawServerCardWithApiClient(Action<DivinationTarotCardRuntimeData, string> callback)
    {
        bool ok = false;
        DivinationTarotDrawResponseDto drawResponse = null;
        string error = null;
        yield return _apiClient.DrawCard((success, response, err) =>
        {
            ok = success;
            drawResponse = response;
            error = err;
        });

        if (!ok && drawResponse == null)
        {
            callback?.Invoke(null, string.IsNullOrWhiteSpace(error) ? _errorStatus : error);
            yield break;
        }

        if (ShouldBlockDrawResponseByCooldown(drawResponse))
        {
            ApplyCooldownToDisplay(drawResponse.cooldown);
            callback?.Invoke(null, DivinationCooldownFormatter.Format(drawResponse.cooldown, _errorStatus));
            yield break;
        }

        DivinationTarotCardRuntimeData card = BuildRuntimeCard(drawResponse);
        if (card == null)
        {
            callback?.Invoke(null, "Server returned card in an unknown format.");
            yield break;
        }

        TryApplyConfiguredFrontendSprite(card);

        if (card.sprite == null && _loadServerImage && !string.IsNullOrWhiteSpace(card.imageUrl))
            yield return LoadCardSprite(card);

        if (card.sprite == null)
            TryApplyDisplayFallbackSprite(card);

        if (card.sprite == null)
            card.sprite = _fallbackCardSprite;

        if (card.sprite == null && !CanContinueWithoutSprite())
        {
            callback?.Invoke(null, "Card has no sprite.");
            yield break;
        }

        callback?.Invoke(card, "");
    }

    private IEnumerator TryPrepareUnityTestCard(Action<DivinationTarotCardRuntimeData, string> callback)
    {
        DivinationTarotTestCard testCard = _unityTestDeck != null ? _unityTestDeck.PickRandom() : null;
        if (testCard == null)
        {
            callback?.Invoke(null, "В Unity Test Deck нет валидных карт.");
            yield break;
        }

        var card = new DivinationTarotCardRuntimeData
        {
            id = testCard.Id,
            name = testCard.Name,
            title = testCard.Name,
            description = testCard.Description,
            heartsReward = testCard.HeartsReward,
            candlesReward = testCard.CandlesReward,
            rewards = BuildLegacyRewards(testCard.HeartsReward, testCard.CandlesReward, 0),
            sprite = testCard.Sprite,
            fromServer = false
        };

        yield return null;
        callback?.Invoke(card, "");
    }

    private IEnumerator LoadCardSprite(DivinationTarotCardRuntimeData card)
    {
        string url = ResolveImageUrl(card.imageUrl);
        if (string.IsNullOrWhiteSpace(url))
            yield break;

        RuntimeTextureLoadScope loadScope = RuntimePerformanceDiagnostics.BeginTextureLoad("RemoteTexture:" + url);
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = _imageRequestTimeoutSeconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            RuntimePerformanceDiagnostics.TrackAsyncOperation("RemoteTexture:" + url, operation);
            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                loadScope.Complete(false, request.error);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                loadScope.Complete(false, "empty-texture");
                yield break;
            }

            DestroyOwnedRuntimeSprite();
            _ownedRuntimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _ownedRuntimeSprite.name = "ServerTarot_" + SaveDataSanitizer.SafeKeyPart(card.id, "card", 64);
            card.sprite = _ownedRuntimeSprite;
        }

        loadScope.Complete(true, "remote");
    }

    private string ResolveImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "";

        imageUrl = imageUrl.Trim();
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttps || absoluteUri.Scheme == Uri.UriSchemeHttp))
        {
            return imageUrl;
        }

        if (!_resolveRelativeImageUrl || !imageUrl.StartsWith("/", StringComparison.Ordinal))
            return "";

        string baseUrl = !string.IsNullOrWhiteSpace(NetworkManager.ActiveBaseUrl)
            ? NetworkManager.ActiveBaseUrl
            : ApiRoutes.BaseUrl;

        return baseUrl.TrimEnd('/') + imageUrl;
    }

    private DivinationTarotCardRuntimeData ParseServerCard(string json)
    {
        return BuildRuntimeCard(DivinationBackendJsonParser.ParseDrawResponse(json));
    }

    private DivinationTarotCardRuntimeData BuildRuntimeCard(DivinationTarotDrawResponseDto response)
    {
        if (response == null)
            return null;

        DivinationCardBackendDto cardDto = response.SelectedCard;
        if (cardDto == null)
            return null;

        string rawId = cardDto.EffectiveId;
        string id = string.IsNullOrWhiteSpace(rawId) ? "tarot_card" : rawId.Trim();
        if (string.IsNullOrWhiteSpace(id))
            id = DivinationCardIdUtility.Normalize(rawId);

        DivinationRewardDto[] rewards = response.rewards ?? cardDto.rewards ?? new DivinationRewardDto[0];
        int hearts = SumRewardValue(rewards, reward => reward.hearts);
        int candles = SumRewardValue(rewards, reward => reward.candles);
        int subscriptionDays = SumRewardValue(rewards, reward => reward.subscriptionDays);

        return new DivinationTarotCardRuntimeData
        {
            id = id,
            name = cardDto.EffectiveTitle,
            title = cardDto.EffectiveTitle,
            description = cardDto.EffectiveDescription,
            imageUrl = cardDto.EffectiveImageUrl ?? "",
            heartsReward = SaveDataSanitizer.ClampCurrencyValue(hearts),
            candlesReward = SaveDataSanitizer.ClampCurrencyValue(candles),
            subscriptionDaysReward = Mathf.Max(0, subscriptionDays),
            weight = Mathf.Max(cardDto.weight, cardDto.probability),
            active = !cardDto.hasActiveValue || cardDto.active || cardDto.isActive,
            rewards = rewards,
            cooldown = response.cooldown,
            backendCard = cardDto,
            fromServer = true,
            rawJson = response.rawJson
        };
    }

    private void TryApplyConfiguredFrontendSprite(DivinationTarotCardRuntimeData card)
    {
        if (card == null || _cardDisplayController == null)
            return;

        if (_cardDisplayController.TryGetCardConfig(card.id, out DivinationCardViewConfig config) &&
            config.FrontSprite != null)
        {
            card.sprite = config.FrontSprite;
            Debug.Log("[Divination] frontend sprite resolved for card id '" + card.id + "'.", this);
        }
    }

    private void TryApplyDisplayFallbackSprite(DivinationTarotCardRuntimeData card)
    {
        if (card == null || _cardDisplayController == null || card.sprite != null)
            return;

        if (_cardDisplayController.TryResolveSprite(card.id, out Sprite sprite))
            card.sprite = sprite;
    }

    private bool CanContinueWithoutSprite()
    {
        return _allowCardWithoutSpriteWhenDisplayControllerAssigned && _cardDisplayController != null;
    }

    private bool ShouldBlockDrawResponseByCooldown(DivinationTarotDrawResponseDto response)
    {
        if (response == null || response.IsDrawAvailable(true))
            return false;

        if (ShouldIgnoreCooldownInDebug() && response.SelectedCard != null)
        {
            Debug.LogWarning("[Divination] debug cooldown bypass: draw response is marked unavailable, but a card was returned.", this);
            return false;
        }

        return true;
    }

    private bool ShouldUseLocalDebugCooldownFallback(string error)
    {
        if (!ShouldIgnoreCooldownInDebug() || !_useLocalCardWhenDebugCooldownRequestFails)
            return false;

        return LooksLikeCooldownConflict(error);
    }

    private bool TryPrepareDebugCooldownFallbackCard(out DivinationTarotCardRuntimeData card)
    {
        card = null;
        if (_cardDisplayController == null ||
            !_cardDisplayController.TryGetRandomConfiguredCard(out DivinationCardViewConfig config))
        {
            Debug.LogWarning("[Divination] debug cooldown fallback skipped: Card Display Controller has no configured local cards.", this);
            return false;
        }

        string id = string.IsNullOrWhiteSpace(config.CardId) ? config.NormalizedCardId : config.CardId.Trim();
        card = new DivinationTarotCardRuntimeData
        {
            id = string.IsNullOrWhiteSpace(id) ? "debug_card" : id,
            name = config.FallbackTitle ?? "",
            title = config.FallbackTitle ?? "",
            description = config.FallbackDescription ?? "",
            imageUrl = "",
            rewards = new DivinationRewardDto[0],
            cooldown = null,
            sprite = config.FrontSprite,
            fromServer = false,
            rawJson = "debug-cooldown-local-card"
        };

        Debug.LogWarning("[Divination] debug cooldown fallback: using local card config '" + card.id + "'.", this);
        return true;
    }

    private bool ShouldIgnoreCooldownInDebug()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return _ignoreCooldownInDebug;
#else
        _ = _ignoreCooldownInDebug;
        _ = _useLocalCardWhenDebugCooldownRequestFails;
        return false;
#endif
    }

    private static bool LooksLikeCooldownConflict(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return false;

        string value = error.ToLowerInvariant();
        return value.Contains("409") ||
               value.Contains("conflict") ||
               value.Contains("cooldown") ||
               value.Contains("available") ||
               value.Contains("too early");
    }

    private void ApplyCooldownToDisplay(DivinationCooldownDto cooldown)
    {
        if (_cardDisplayController != null)
            _cardDisplayController.ApplyCooldown(cooldown);

        string status = DivinationCooldownFormatter.Format(cooldown, "");
        if (!string.IsNullOrWhiteSpace(status))
            ApplyStatus(status);
    }

    private static DivinationRewardDto[] BuildLegacyRewards(int hearts, int candles, int subscriptionDays)
    {
        hearts = SaveDataSanitizer.ClampCurrencyValue(hearts);
        candles = SaveDataSanitizer.ClampCurrencyValue(candles);
        subscriptionDays = Mathf.Max(0, subscriptionDays);
        if (hearts <= 0 && candles <= 0 && subscriptionDays <= 0)
            return new DivinationRewardDto[0];

        return new[]
        {
            new DivinationRewardDto
            {
                type = "legacy",
                hearts = hearts,
                candles = candles,
                subscriptionDays = subscriptionDays
            }
        };
    }

    private static int SumRewardValue(DivinationRewardDto[] rewards, Func<DivinationRewardDto, int> selector)
    {
        if (rewards == null || selector == null)
            return 0;

        int total = 0;
        for (int i = 0; i < rewards.Length; i++)
        {
            if (rewards[i] != null)
                total += Mathf.Max(0, selector(rewards[i]));
        }

        return total;
    }

    private void ApplyPreparedCard(DivinationTarotCardRuntimeData card, bool showTextsImmediately)
    {
        _preparedCard = card;
        ApplyPreparedCardToAnimator(card);
        if (_cardDisplayController != null)
            _cardDisplayController.ApplyCard(card, showTextsImmediately);

        if (_previewCardImage != null && _applyPreviewImageBeforeFlash)
        {
            _previewCardImage.sprite = card.sprite;
            _previewCardImage.enabled = card.sprite != null;
        }

        SetText(_cardNameText, FirstNonEmpty(card.title, card.name));
        SetText(_cardDescriptionText, card.description);
        SetText(_rewardText, FormatReward(card));
        if (showTextsImmediately)
            ShowCardTexts();
        else
            HideCardTexts();
        ApplyStatus(_readyStatus);
        _cardPrepared.Invoke(card);
    }

    private void ApplyPreparedCardToAnimator(DivinationTarotCardRuntimeData card)
    {
        ResolveAnimator();
        if (_spriteFrameAnimator == null || card == null || card.sprite == null)
            return;

        _spriteFrameAnimator.SetFinalCardSprites(new[] { card.sprite });
        _spriteFrameAnimator.SetReplaceLastCardSpriteWithRandom(true);
        if (_clearFinalCardStopBeforePlay)
            _spriteFrameAnimator.ClearStoppedOnFinalCard();
    }

    private void StartConfiguredAnimation()
    {
        ResolveAnimator();
        if (_spriteFrameAnimator == null)
            return;

        if (_clearFinalCardStopBeforePlay)
            _spriteFrameAnimator.ClearStoppedOnFinalCard();

        switch (_animationStartMode)
        {
            case DivinationTarotAnimationStartMode.PlayFromStart:
                _spriteFrameAnimator.PlayFromStart();
                break;
            case DivinationTarotAnimationStartMode.TriggerAnimation:
                _spriteFrameAnimator.TriggerAnimation();
                break;
            case DivinationTarotAnimationStartMode.None:
            default:
                break;
        }
    }

    private bool ShouldRevealCardTextsAfterAnimation()
    {
        ResolveAnimator();
        return _cardDisplayController != null &&
               _spriteFrameAnimator != null &&
               _animationStartMode != DivinationTarotAnimationStartMode.None;
    }

    private void WaitForTextRevealAfterAnimation()
    {
        ResolveAnimator();
        if (_spriteFrameAnimator == null)
            return;

        StopWaitingForTextRevealAfterAnimation();
        _waitingForTextRevealAfterAnimation = true;
        _spriteFrameAnimator.AnimationCompleted += RevealCardTextsAfterAnimation;
    }

    private void StopWaitingForTextRevealAfterAnimation()
    {
        if (_spriteFrameAnimator != null)
            _spriteFrameAnimator.AnimationCompleted -= RevealCardTextsAfterAnimation;

        _waitingForTextRevealAfterAnimation = false;
    }

    private void RevealCardTextsAfterAnimation()
    {
        if (!_waitingForTextRevealAfterAnimation)
            return;

        StopWaitingForTextRevealAfterAnimation();
        ShowCardTexts();
    }

    private void HideCardTexts()
    {
        if (_cardDisplayController != null)
            _cardDisplayController.HideCardTexts();
    }

    private void ShowCardTexts()
    {
        if (_cardDisplayController != null)
            _cardDisplayController.ShowCardTexts();
    }

    private string FormatReward(DivinationTarotCardRuntimeData card)
    {
        if (card == null)
            return "";

        string formattedRewards = DivinationRewardDisplayFormatter.FormatRewards(card.rewards);
        if (!string.IsNullOrWhiteSpace(formattedRewards))
            return formattedRewards;

        try
        {
            return string.Format(_rewardFormat, card.heartsReward, card.candlesReward, card.subscriptionDaysReward);
        }
        catch (FormatException)
        {
            return "+" + card.heartsReward;
        }
    }

    private void BindButton()
    {
        if (!_autoBindButton || _drawButton == null)
            return;

        _drawButton.onClick.RemoveListener(DrawServerOrTestCardAndTriggerAnimation);
        _drawButton.onClick.AddListener(DrawServerOrTestCardAndTriggerAnimation);
    }

    private void UnbindButton()
    {
        if (_drawButton != null)
            _drawButton.onClick.RemoveListener(DrawServerOrTestCardAndTriggerAnimation);
    }

    private void SetDrawButtonInteractable(bool interactable)
    {
        if (_drawButton != null)
            _drawButton.interactable = interactable;
    }

    private void ResolveAnimator()
    {
        if (_spriteFrameAnimator == null)
            _spriteFrameAnimator = GetComponent<SpriteFrameAnimator>();
    }

    private void StopRunningRoutine()
    {
        if (_drawRoutine == null)
            return;

        StopCoroutine(_drawRoutine);
        _drawRoutine = null;
    }

    private void Fail(string message)
    {
        ApplyStatus(string.IsNullOrWhiteSpace(message) ? _errorStatus : message);
        _failed.Invoke(string.IsNullOrWhiteSpace(message) ? _errorStatus : message);
    }

    private void ApplyStatus(string text)
    {
        SetText(_statusText, text);
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null)
            target.text = text ?? "";
    }

    private void DestroyOwnedRuntimeSprite()
    {
        if (_ownedRuntimeSprite == null)
            return;

        Texture texture = _ownedRuntimeSprite.texture;
        Destroy(_ownedRuntimeSprite);
        if (texture != null)
            Destroy(texture);

        _ownedRuntimeSprite = null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return "";
    }
}
