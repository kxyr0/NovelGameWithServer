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
    public string description;
    public string imageUrl;
    public int heartsReward;
    public int candlesReward;
    public int subscriptionDaysReward;
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
    [Tooltip("Откуда брать карту: только серверные карты из админки, только локальная тестовая колода или сервер с fallback на Unity-тест.")]
    private DivinationTarotSourceMode _sourceMode = DivinationTarotSourceMode.ServerOnly;

    [SerializeField]
    [Tooltip("Локальная тестовая колода для проверки UI без админки и без опубликованных серверных карт.")]
    private DivinationTarotTestDeckConfig _unityTestDeck;

    [SerializeField]
    [Tooltip("Запасной спрайт, если сервер вернул карту без картинки или загрузка imageUrl не удалась.")]
    private Sprite _fallbackCardSprite;

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
    [Tooltip("Timeout загрузки картинки карты с сервера в секундах.")]
    private int _imageRequestTimeoutSeconds = 12;

    [SerializeField]
    [Tooltip("Если imageUrl начинается с '/', к нему будет добавлен текущий base URL NetworkManager или ApiRoutes.BaseUrl.")]
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
    [Tooltip("Вызывается, когда карту получить не удалось. В string передается текст ошибки.")]
    private UnityEvent<string> _failed = new UnityEvent<string>();

    private Coroutine _drawRoutine;
    private DivinationTarotCardRuntimeData _preparedCard;
    private Sprite _ownedRuntimeSprite;

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

        ApplyPreparedCardToAnimator(_preparedCard);
        StartConfiguredAnimation();
    }

    private IEnumerator DrawRoutine(bool triggerAnimation)
    {
        SetDrawButtonInteractable(false);
        ApplyStatus(_loadingStatus);

        DivinationTarotCardRuntimeData card = null;
        string error = "";

        if (_sourceMode != DivinationTarotSourceMode.UnityTestOnly)
            yield return TryDrawServerCard((result, err) => { card = result; error = err; });

        bool canUseUnityFallback =
            card == null &&
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

        ApplyPreparedCard(card);

        if (triggerAnimation)
            StartConfiguredAnimation();

        SetDrawButtonInteractable(true);
        _drawRoutine = null;
    }

    private IEnumerator PrepareUnityTestCardAndMaybeTrigger(bool triggerAnimation)
    {
        SetDrawButtonInteractable(false);
        ApplyStatus(_loadingStatus);

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

        ApplyPreparedCard(card);
        if (triggerAnimation)
            StartConfiguredAnimation();

        SetDrawButtonInteractable(true);
        _drawRoutine = null;
    }

    private IEnumerator TryDrawServerCard(Action<DivinationTarotCardRuntimeData, string> callback)
    {
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

        DivinationTarotCardRuntimeData card = ParseServerCard(payload);
        if (card == null)
        {
            callback?.Invoke(null, "Сервер вернул карту в неизвестном формате.");
            yield break;
        }

        if (_loadServerImage && !string.IsNullOrWhiteSpace(card.imageUrl))
            yield return LoadCardSprite(card);

        if (card.sprite == null)
            card.sprite = _fallbackCardSprite;

        if (card.sprite == null)
        {
            callback?.Invoke(null, "У карты нет картинки.");
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
            description = testCard.Description,
            heartsReward = testCard.HeartsReward,
            candlesReward = testCard.CandlesReward,
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
        string nestedPayload = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "data"),
            NetworkJson.GetRawValue(json, "result"),
            NetworkJson.GetRawValue(json, "draw"));
        if (!string.IsNullOrWhiteSpace(nestedPayload) && NetworkJson.LooksLikeJsonObject(nestedPayload))
        {
            DivinationTarotCardRuntimeData nestedCard = ParseServerCard(nestedPayload);
            if (nestedCard != null)
            {
                nestedCard.rawJson = json;
                return nestedCard;
            }
        }

        TarotDrawResponse response = NetworkJson.FromJson<TarotDrawResponse>(json);
        TarotCardDto cardDto = response != null ? FirstCard(response.card, response.tarotCard) : null;

        string rawCard = FirstNonEmptyRaw(
            NetworkJson.GetRawValue(json, "card"),
            NetworkJson.GetRawValue(json, "tarotCard"));
        if (cardDto == null && !string.IsNullOrWhiteSpace(rawCard))
            cardDto = NetworkJson.FromJson<TarotCardDto>(rawCard);

        if (cardDto == null)
        {
            cardDto = new TarotCardDto
            {
                id = NetworkJson.GetFirstString(json, "id", "cardId", "tarotCardId"),
                name = NetworkJson.GetFirstString(json, "name", "title"),
                description = NetworkJson.GetString(json, "description"),
                imageUrl = NetworkJson.GetFirstString(json, "imageUrl", "image", "url")
            };
        }

        TarotRewardDto reward = response != null ? response.reward : null;
        if (reward == null && cardDto != null)
            reward = cardDto.reward;

        string rawReward = NetworkJson.GetRawValue(json, "reward");
        if (reward == null && !string.IsNullOrWhiteSpace(rawReward) && NetworkJson.LooksLikeJsonObject(rawReward))
            reward = NetworkJson.FromJson<TarotRewardDto>(rawReward);

        string rawId = cardDto != null ? cardDto.id : "";
        string name = cardDto != null ? FirstNonEmpty(cardDto.name, cardDto.title) : "";
        string description = cardDto != null ? cardDto.description ?? "" : "";
        string imageUrl = cardDto != null ? FirstNonEmpty(cardDto.imageUrl, cardDto.image, cardDto.url) : "";
        if (string.IsNullOrWhiteSpace(rawId) &&
            string.IsNullOrWhiteSpace(name) &&
            string.IsNullOrWhiteSpace(description) &&
            string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        string id = SaveDataSanitizer.SafeKeyPart(rawId, "tarot_card", 96);

        return new DivinationTarotCardRuntimeData
        {
            id = id,
            name = name,
            description = description,
            imageUrl = imageUrl ?? "",
            heartsReward = SaveDataSanitizer.ClampCurrencyValue(reward != null ? reward.hearts : NetworkJson.GetInt(json, "heartsReward", 0)),
            candlesReward = SaveDataSanitizer.ClampCurrencyValue(reward != null ? reward.candles : NetworkJson.GetInt(json, "candlesReward", 0)),
            subscriptionDaysReward = Mathf.Max(0, reward != null ? reward.subscriptionDays : NetworkJson.GetInt(json, "subscriptionDays", 0)),
            fromServer = true,
            rawJson = json
        };
    }

    private void ApplyPreparedCard(DivinationTarotCardRuntimeData card)
    {
        _preparedCard = card;
        ApplyPreparedCardToAnimator(card);

        if (_previewCardImage != null && _applyPreviewImageBeforeFlash)
        {
            _previewCardImage.sprite = card.sprite;
            _previewCardImage.enabled = card.sprite != null;
        }

        SetText(_cardNameText, card.name);
        SetText(_cardDescriptionText, card.description);
        SetText(_rewardText, FormatReward(card));
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

    private string FormatReward(DivinationTarotCardRuntimeData card)
    {
        if (card == null)
            return "";

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

    private static TarotCardDto FirstCard(TarotCardDto first, TarotCardDto second)
    {
        return first != null && (!string.IsNullOrWhiteSpace(first.id) || !string.IsNullOrWhiteSpace(first.imageUrl))
            ? first
            : second;
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

    private static string FirstNonEmptyRaw(params string[] values)
    {
        return FirstNonEmpty(values);
    }

    [Serializable]
    private sealed class TarotDrawResponse
    {
        public TarotCardDto card;
        public TarotCardDto tarotCard;
        public TarotRewardDto reward;
    }

    [Serializable]
    private sealed class TarotCardDto
    {
        public string id;
        public string name;
        public string title;
        public string description;
        public string imageUrl;
        public string image;
        public string url;
        public TarotRewardDto reward;
    }

    [Serializable]
    private sealed class TarotRewardDto
    {
        public int hearts;
        public int candles;
        public int subscriptionDays;
    }
}
