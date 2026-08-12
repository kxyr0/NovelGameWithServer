using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Divination Card Display Controller")]
public sealed class DivinationCardDisplayController : MonoBehaviour
{
    private const string LogPrefix = "[Divination]";

    [Header("Картинка карты / спрайты")]
    [SerializeField]
    [Tooltip("Компонент Image, в который будет подставлен локальный спрайт лицевой стороны карты.")]
    private Image _cardFrontImage;

    [SerializeField]
    [Tooltip("Необязательный компонент Image для рубашки/колоды. Скрипт сам его не меняет, если не вызвать методы из событий.")]
    private Image _optionalCardBackImage;

    [SerializeField]
    [Tooltip("Сопоставление серверного ID/ключа карты с локальным спрайтом и запасными текстами.")]
    private List<DivinationCardViewConfig> _cardViewConfigs = new List<DivinationCardViewConfig>();

    [SerializeField]
    [Tooltip("Запасной спрайт, если сервер вернул неизвестный ID карты или у настройки нет спрайта.")]
    private Sprite _fallbackSprite;

    [SerializeField]
    [Tooltip("Если ID карты неизвестен и запасного спрайта нет, оставить предыдущий безопасный спрайт вместо очистки картинки.")]
    private bool _keepPreviousSpriteWhenCardIdUnknown = true;

    [SerializeField]
    [Tooltip("Включать компонент Image карты после успешной подстановки спрайта.")]
    private bool _enableCardImageWhenSpriteApplied = true;

    [Header("Текстовые поля")]
    [SerializeField]
    [Tooltip("TMP_Text для названия карты с сервера или запасного текста.")]
    private TMP_Text _cardTitleText;

    [SerializeField]
    [Tooltip("TMP_Text для описания карты с сервера или запасного текста.")]
    private TMP_Text _cardDescriptionText;

    [SerializeField]
    [Tooltip("TMP_Text для отображения наград, которые вернул сервер.")]
    private TMP_Text _rewardText;

    [SerializeField]
    [Tooltip("TMP_Text для статуса кулдауна/доступности вытягивания карты.")]
    private TMP_Text _cooldownText;

    [SerializeField]
    [Tooltip("Держать alpha=0 у текстов карты, пока карта не показана.")]
    private bool _hideTextsUntilCardShown = true;

    [SerializeField]
    [Tooltip("Запасное название. Используется только если сервер и настройка карты не дали название.")]
    private string _fallbackTitle;

    [SerializeField, TextArea(2, 6)]
    [Tooltip("Запасное описание. Используется только если сервер и настройка карты не дали описание.")]
    private string _fallbackDescription;

    [SerializeField]
    [Tooltip("Текст в поле награды, если наград нет. Оставьте пустым, чтобы ничего не показывать.")]
    private string _noRewardText = "";

    [SerializeField]
    [Tooltip("Текст в поле кулдауна, когда карту можно вытянуть. Оставьте пустым, чтобы ничего не показывать.")]
    private string _availableCooldownText = "";

    [Header("UI-трансформ карты")]
    [SerializeField]
    [Tooltip("RectTransform UI-карты, к которому применяются размеры, позиция, масштаб и поворот.")]
    private RectTransform _cardRectTransform;

    [SerializeField]
    [Tooltip("Применять настройки RectTransform при вызове ApplyCardTransformSettings.")]
    private bool _applyUiCardTransform;

    [SerializeField, Min(1f)]
    [Tooltip("Ширина UI-карты.")]
    private float _uiWidth = 699f;

    [SerializeField, Min(1f)]
    [Tooltip("Высота UI-карты.")]
    private float _uiHeight = 907f;

    [SerializeField]
    [Tooltip("Якорная позиция UI-карты по X/Y/Z.")]
    private Vector3 _uiAnchoredPosition3D;

    [SerializeField]
    [Tooltip("Локальный масштаб UI-карты по X/Y/Z.")]
    private Vector3 _uiLocalScale = Vector3.one;

    [SerializeField]
    [Tooltip("Локальный поворот UI-карты по X/Y/Z в градусах.")]
    private Vector3 _uiLocalEulerAngles;

    [Header("Мировой/3D-трансформ карты")]
    [SerializeField]
    [Tooltip("Transform карты в мировом пространстве/3D, к которому применяются позиция, масштаб и поворот.")]
    private Transform _worldCardTransform;

    [SerializeField]
    [Tooltip("Применять настройки Transform при вызове ApplyCardTransformSettings.")]
    private bool _applyWorldCardTransform;

    [SerializeField]
    [Tooltip("Локальная позиция 3D-карты по X/Y/Z.")]
    private Vector3 _worldLocalPosition;

    [SerializeField]
    [Tooltip("Локальный масштаб 3D-карты по X/Y/Z.")]
    private Vector3 _worldLocalScale = Vector3.one;

    [SerializeField]
    [Tooltip("Локальный поворот 3D-карты по X/Y/Z в градусах.")]
    private Vector3 _worldLocalEulerAngles;

    [Header("Визуальные эффекты")]
    [SerializeField]
    [Tooltip("Необязательный контроллер визуальных эффектов карты.")]
    private DivinationCardVisualEffectController _visualEffectController;

    [SerializeField]
    [Tooltip("Стандартный режим визуального эффекта, если карта не переопределяет его в конфиге.")]
    private DivinationCardVisualEffectMode _defaultVisualEffectMode = DivinationCardVisualEffectMode.None;

    [SerializeField]
    [Tooltip("Запускать визуальный эффект после применения данных карты.")]
    private bool _playVisualEffectOnApply;

    [Header("Валидация")]
    [SerializeField]
    [Tooltip("Запускать легкую проверку настроек в OnValidate.")]
    private bool _validateOnEdit = true;

    private Sprite _lastSafeSprite;
    private readonly Dictionary<TMP_Text, float> _textOriginalAlpha = new Dictionary<TMP_Text, float>();

    private void Awake()
    {
        CaptureSafeSprite();
        CaptureTextOriginalAlphas();
        if (_hideTextsUntilCardShown)
            HideCardTexts();
    }

    private void OnEnable()
    {
        if (_hideTextsUntilCardShown)
            HideCardTexts();
    }

    private void OnValidate()
    {
        _uiWidth = Mathf.Max(1f, _uiWidth);
        _uiHeight = Mathf.Max(1f, _uiHeight);
        if (_uiLocalScale == Vector3.zero)
            _uiLocalScale = Vector3.one;
        if (_worldLocalScale == Vector3.zero)
            _worldLocalScale = Vector3.one;

        if (_validateOnEdit)
            ValidateConfiguration(false);
    }

    [ContextMenu("Apply Card Transform Settings")]
    public void ApplyCardTransformSettings()
    {
        if (_applyUiCardTransform)
            ApplyUiTransformSettings();

        if (_applyWorldCardTransform)
            ApplyWorldTransformSettings();
    }

    [ContextMenu("Validate Divination Card Display")]
    public void ValidateConfiguration()
    {
        ValidateConfiguration(true);
    }

    public void ApplyCard(DivinationTarotCardRuntimeData runtimeData)
    {
        ApplyCard(runtimeData, true);
    }

    public void ApplyCard(DivinationTarotCardRuntimeData runtimeData, bool showTextsImmediately)
    {
        if (runtimeData == null)
        {
            Debug.LogWarning(LogPrefix + " card display skipped: runtime data is null.", this);
            return;
        }

        var card = new DivinationCardBackendDto
        {
            id = runtimeData.id,
            name = runtimeData.name,
            title = runtimeData.title,
            description = runtimeData.description,
            imageUrl = runtimeData.imageUrl,
            rawJson = runtimeData.rawJson
        };

        ApplyCard(card, runtimeData.rewards, runtimeData.cooldown, runtimeData.sprite, showTextsImmediately);
    }

    public void ApplyCard(
        DivinationCardBackendDto backendCard,
        IEnumerable<DivinationRewardDto> rewards,
        DivinationCooldownDto cooldown,
        Sprite spriteOverride = null)
    {
        ApplyCard(backendCard, rewards, cooldown, spriteOverride, true);
    }

    public void ApplyCard(
        DivinationCardBackendDto backendCard,
        IEnumerable<DivinationRewardDto> rewards,
        DivinationCooldownDto cooldown,
        Sprite spriteOverride,
        bool showTextsImmediately)
    {
        if (backendCard == null)
        {
            Debug.LogWarning(LogPrefix + " card display skipped: backend card is null.", this);
            return;
        }

        string cardId = backendCard.EffectiveId;
        bool hasConfig = TryGetCardConfig(cardId, out DivinationCardViewConfig config);
        Sprite sprite = spriteOverride != null ? spriteOverride : null;
        if (sprite == null && hasConfig)
            sprite = config.FrontSprite;

        ApplySprite(cardId, sprite, hasConfig);
        ApplyText(backendCard, hasConfig, config);
        ApplyRewardText(rewards);
        ApplyCooldown(cooldown);
        ApplyCardTransformSettings();
        SetCardTextsVisible(showTextsImmediately);

        if (_playVisualEffectOnApply && _visualEffectController != null)
        {
            DivinationCardVisualEffectMode mode =
                hasConfig && config.OverrideVisualEffectMode
                    ? config.VisualEffectMode
                    : _defaultVisualEffectMode;
            _visualEffectController.Play(mode, config.OverrideMaterial, config.OverrideShader);
        }

        Debug.Log(LogPrefix + " card text applied: " + FirstNonEmpty(backendCard.EffectiveTitle, cardId) + ".", this);
    }

    public void ApplyCooldown(DivinationCooldownDto cooldown)
    {
        string text = DivinationCooldownFormatter.Format(cooldown, _availableCooldownText);
        SetText(_cooldownText, text);

        if (cooldown != null)
            Debug.Log(LogPrefix + " cooldown state: available=" + cooldown.IsAvailable(true) + ".", this);
    }

    public void HideCardTexts()
    {
        SetCardTextsVisible(false);
    }

    public void ShowCardTexts()
    {
        SetCardTextsVisible(true);
    }

    public void SetCardTextsVisible(bool visible)
    {
        CaptureTextOriginalAlphas();
        SetTextAlpha(_cardTitleText, visible);
        SetTextAlpha(_cardDescriptionText, visible);
        SetTextAlpha(_rewardText, visible);
        SetTextAlpha(_cooldownText, visible);
    }

    public bool TryResolveSprite(string cardId, out Sprite sprite)
    {
        sprite = null;
        if (TryGetCardConfig(cardId, out DivinationCardViewConfig config) && config.FrontSprite != null)
        {
            sprite = config.FrontSprite;
            return true;
        }

        if (_fallbackSprite != null)
        {
            sprite = _fallbackSprite;
            return true;
        }

        return false;
    }

    public bool TryGetCardConfig(string cardId, out DivinationCardViewConfig config)
    {
        string normalized = DivinationCardIdUtility.Normalize(cardId);
        if (!string.IsNullOrEmpty(normalized) && _cardViewConfigs != null)
        {
            for (int i = 0; i < _cardViewConfigs.Count; i++)
            {
                DivinationCardViewConfig item = _cardViewConfigs[i];
                if (item.NormalizedCardId == normalized)
                {
                    config = item;
                    return true;
                }
            }
        }

        config = default(DivinationCardViewConfig);
        return false;
    }

    public bool TryGetRandomConfiguredCard(out DivinationCardViewConfig config)
    {
        config = default(DivinationCardViewConfig);
        if (_cardViewConfigs == null || _cardViewConfigs.Count == 0)
            return false;

        int validCount = 0;
        for (int i = 0; i < _cardViewConfigs.Count; i++)
        {
            if (IsUsableCardConfig(_cardViewConfigs[i]))
                validCount++;
        }

        if (validCount <= 0)
            return false;

        int targetIndex = Random.Range(0, validCount);
        for (int i = 0; i < _cardViewConfigs.Count; i++)
        {
            DivinationCardViewConfig item = _cardViewConfigs[i];
            if (!IsUsableCardConfig(item))
                continue;

            if (targetIndex == 0)
            {
                config = item;
                return true;
            }

            targetIndex--;
        }

        return false;
    }

    private void ApplySprite(string cardId, Sprite sprite, bool hasConfig)
    {
        if (_cardFrontImage == null)
        {
            Debug.LogWarning(LogPrefix + " missing inspector reference: Card Front Image.", this);
            return;
        }

        if (sprite == null && _fallbackSprite != null)
        {
            sprite = _fallbackSprite;
            Debug.LogWarning(LogPrefix + " safe fallback usage: fallback sprite used for card id '" + cardId + "'.", this);
        }

        if (sprite == null)
        {
            if (!hasConfig)
                Debug.LogWarning(LogPrefix + " unknown card id '" + cardId + "'.", this);
            else
                Debug.LogWarning(LogPrefix + " missing sprite for card id '" + cardId + "'.", this);

            if (_keepPreviousSpriteWhenCardIdUnknown && _lastSafeSprite != null)
            {
                _cardFrontImage.sprite = _lastSafeSprite;
                Debug.LogWarning(LogPrefix + " safe fallback usage: previous sprite kept.", this);
            }

            return;
        }

        _cardFrontImage.sprite = sprite;
        if (_enableCardImageWhenSpriteApplied)
            _cardFrontImage.enabled = true;
        _lastSafeSprite = sprite;
        Debug.Log(LogPrefix + " card sprite applied for id '" + cardId + "'.", this);
    }

    private void ApplyText(DivinationCardBackendDto backendCard, bool hasConfig, DivinationCardViewConfig config)
    {
        string title = FirstNonEmpty(
            backendCard.EffectiveTitle,
            hasConfig ? config.FallbackTitle : "",
            _fallbackTitle);
        string description = FirstNonEmpty(
            backendCard.EffectiveDescription,
            hasConfig ? config.FallbackDescription : "",
            _fallbackDescription);

        SetText(_cardTitleText, title);
        SetText(_cardDescriptionText, description);
    }

    private void ApplyRewardText(IEnumerable<DivinationRewardDto> rewards)
    {
        string rewardText = DivinationRewardDisplayFormatter.FormatRewards(rewards);
        SetText(_rewardText, string.IsNullOrWhiteSpace(rewardText) ? _noRewardText : rewardText);
    }

    private void ApplyUiTransformSettings()
    {
        if (_cardRectTransform == null)
        {
            Debug.LogWarning(LogPrefix + " missing inspector reference: UI Card RectTransform.", this);
            return;
        }

        _cardRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _uiWidth);
        _cardRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _uiHeight);
        _cardRectTransform.anchoredPosition3D = _uiAnchoredPosition3D;
        _cardRectTransform.localScale = _uiLocalScale;
        _cardRectTransform.localEulerAngles = _uiLocalEulerAngles;
    }

    private void ApplyWorldTransformSettings()
    {
        if (_worldCardTransform == null)
        {
            Debug.LogWarning(LogPrefix + " missing inspector reference: World Card Transform.", this);
            return;
        }

        _worldCardTransform.localPosition = _worldLocalPosition;
        _worldCardTransform.localScale = _worldLocalScale;
        _worldCardTransform.localEulerAngles = _worldLocalEulerAngles;
    }

    private void ValidateConfiguration(bool verbose)
    {
        var ids = new HashSet<string>();
        if (_cardViewConfigs != null)
        {
            for (int i = 0; i < _cardViewConfigs.Count; i++)
            {
                DivinationCardViewConfig config = _cardViewConfigs[i];
                string id = config.NormalizedCardId;
                if (string.IsNullOrEmpty(id))
                {
                    if (verbose)
                        Debug.LogWarning(LogPrefix + " validation: card config at index " + i + " has empty CardId.", this);
                    continue;
                }

                if (!ids.Add(id))
                    Debug.LogWarning(LogPrefix + " validation: duplicate card id '" + id + "'.", this);

                if (config.FrontSprite == null && verbose)
                    Debug.LogWarning(LogPrefix + " validation: card id '" + id + "' has no Front Sprite.", this);
            }
        }

        if (_cardFrontImage == null && verbose)
            Debug.LogWarning(LogPrefix + " validation: Card Front Image is not assigned.", this);

        if (_cardTitleText == null && verbose)
            Debug.LogWarning(LogPrefix + " validation: Card Title TMP_Text is not assigned.", this);

        if (_cardDescriptionText == null && verbose)
            Debug.LogWarning(LogPrefix + " validation: Card Description TMP_Text is not assigned.", this);

        if (_rewardText == null && verbose)
            Debug.LogWarning(LogPrefix + " validation: Reward TMP_Text is not assigned.", this);

        if (_cooldownText == null && verbose)
            Debug.LogWarning(LogPrefix + " validation: Cooldown TMP_Text is not assigned.", this);
    }

    private void CaptureSafeSprite()
    {
        if (_cardFrontImage != null)
            _lastSafeSprite = _cardFrontImage.sprite;
    }

    private void CaptureTextOriginalAlphas()
    {
        CaptureTextOriginalAlpha(_cardTitleText);
        CaptureTextOriginalAlpha(_cardDescriptionText);
        CaptureTextOriginalAlpha(_rewardText);
        CaptureTextOriginalAlpha(_cooldownText);
    }

    private void CaptureTextOriginalAlpha(TMP_Text target)
    {
        if (target == null || _textOriginalAlpha.ContainsKey(target))
            return;

        _textOriginalAlpha[target] = target.color.a;
    }

    private void SetTextAlpha(TMP_Text target, bool visible)
    {
        if (target == null)
            return;

        Color color = target.color;
        color.a = visible && _textOriginalAlpha.TryGetValue(target, out float alpha) ? alpha : 0f;
        target.color = color;
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null)
            target.text = text ?? "";
    }

    private static bool IsUsableCardConfig(DivinationCardViewConfig config)
    {
        return !string.IsNullOrEmpty(config.NormalizedCardId) && config.FrontSprite != null;
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
