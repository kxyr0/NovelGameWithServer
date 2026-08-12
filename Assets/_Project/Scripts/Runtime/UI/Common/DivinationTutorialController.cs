using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Divination Tutorial Controller")]
public sealed class DivinationTutorialController : MonoBehaviour
{
    private const string DefaultScreenId = "Divination";
    private const string DefaultPrefsKey = "VN_DIVINATION_TUTORIAL_SEEN";

    [Serializable]
    public sealed class BoolEvent : UnityEvent<bool>
    {
    }

    [Header("Экран")]
    [SerializeField]
    [Tooltip("ID экрана, при совпадении с CurrentScreenId будет работать обучение Divination.")]
    private string _screenId = DefaultScreenId;

    [Header("Состояние обучения")]
    [SerializeField]
    [Tooltip("Ключ LocalSecurePrefs, где хранится флаг уже просмотренного обучения.")]
    private string _tutorialSeenPrefsKey = DefaultPrefsKey;

    [SerializeField]
    [Tooltip("Сохранять просмотр обучения между запусками через LocalSecurePrefs.")]
    private bool _persistSeenState = true;

    [SerializeField]
    [Tooltip("В редакторе и Development Build показывать обучение при каждом входе на экран, игнорируя сохраненный флаг.")]
    private bool _showEveryEntryInDebug = true;

    [Header("UI обучения")]
    [SerializeField]
    [Tooltip("Корневой GameObject панели обучения, который можно включать/выключать.")]
    private GameObject _tutorialPanelRoot;

    [SerializeField]
    [Tooltip("CanvasGroup панели обучения для управления видимостью, доступностью взаимодействия и блокировкой raycast.")]
    private CanvasGroup _tutorialPanelCanvasGroup;

    [SerializeField]
    [Tooltip("TMP_Text, в который подставляется текст обучения при показе панели.")]
    private TMP_Text _tutorialMessageText;

    [SerializeField]
    [Tooltip("Текст сообщения обучения, который показывается игроку.")]
    private string _tutorialMessage = "Нажмите на колоду, чтобы узнать предсказания на неделю.";

    [SerializeField]
    [Tooltip("Подставлять текст обучения в TMP_Text каждый раз при показе панели.")]
    private bool _applyMessageOnShow = true;

    [SerializeField]
    [Tooltip("Включать/выключать корневой объект панели вместе с видимостью обучения.")]
    private bool _togglePanelRootActive = true;

    [SerializeField]
    [Tooltip("Кнопка 'Понятно', которая подтверждает обучение.")]
    private Button _understoodButton;

    [Header("Колода")]
    [SerializeField]
    [Tooltip("Кнопка колоды, клик по которой считается попыткой вытянуть карту.")]
    private Button _deckButton;

    [SerializeField]
    [Tooltip("Graphic колоды, у которого можно блокировать raycastTarget до завершения обучения.")]
    private Graphic _deckRaycastGraphic;

    [SerializeField]
    [Tooltip("CanvasGroup колоды, у которого можно блокировать blocksRaycasts до завершения обучения.")]
    private CanvasGroup _deckCanvasGroup;

    [SerializeField]
    [Tooltip("Блокировать клики по колоде, пока игрок не подтвердил обучение.")]
    private bool _blockDeckUntilTutorialAcknowledged = true;

    [SerializeField]
    [Tooltip("Отключать клики по колоде после первого нажатия в текущей сессии экрана.")]
    private bool _disableDeckAfterClick = true;

    [SerializeField]
    [Tooltip("Управлять raycastTarget у графического компонента колоды при блокировке/разблокировке.")]
    private bool _controlDeckGraphicRaycastTarget = true;

    [SerializeField]
    [Tooltip("Управлять blocksRaycasts у CanvasGroup колоды при блокировке/разблокировке.")]
    private bool _controlDeckCanvasGroupRaycasts = true;

    [Header("Автопривязка")]
    [SerializeField]
    [Tooltip("Автоматически подписывать кнопку 'Понятно' на AcknowledgeTutorial.")]
    private bool _bindUnderstoodButton = true;

    [SerializeField]
    [Tooltip("Автоматически подписывать кнопку колоды на HandleDeckClicked.")]
    private bool _bindDeckButton = true;

    [Header("События")]
    [SerializeField]
    [Tooltip("UnityEvent вызывается при показе панели обучения.")]
    private UnityEvent _tutorialShown = new UnityEvent();

    [SerializeField]
    [Tooltip("UnityEvent вызывается при скрытии панели обучения.")]
    private UnityEvent _tutorialHidden = new UnityEvent();

    [SerializeField]
    [Tooltip("UnityEvent вызывается после подтверждения обучения игроком.")]
    private UnityEvent _tutorialAcknowledged = new UnityEvent();

    [SerializeField]
    [Tooltip("UnityEvent вызывается после допустимого клика по колоде.")]
    private UnityEvent _deckClicked = new UnityEvent();

    [SerializeField]
    [Tooltip("UnityEvent<bool> сообщает, доступна ли колода для клика.")]
    private BoolEvent _deckAvailabilityChanged = new BoolEvent();

    private bool _tutorialVisible;
    private bool _tutorialAcknowledgedForEntry;
    private bool _seenTutorialThisSession;
    private bool _hasDeckBeenClickedThisSession;
    private bool _hasCapturedDeckState;
    private bool _homeDeckGraphicRaycastTarget = true;
    private bool _homeDeckCanvasGroupBlocksRaycasts = true;

    public bool IsTutorialVisible => _tutorialVisible;
    public bool IsTutorialAcknowledgedForEntry => _tutorialAcknowledgedForEntry;
    public bool HasDeckBeenClickedThisSession => _hasDeckBeenClickedThisSession;
    public UnityEvent TutorialShown => _tutorialShown;
    public UnityEvent TutorialHidden => _tutorialHidden;
    public UnityEvent TutorialAcknowledged => _tutorialAcknowledged;
    public UnityEvent DeckClicked => _deckClicked;
    public BoolEvent DeckAvailabilityChanged => _deckAvailabilityChanged;

    private void Awake()
    {
        NormalizeSerializedState();
        ResolveDeckTargets();
        CaptureDeckHomeState();
        SetTutorialVisible(false, false);
    }

    private void OnEnable()
    {
        NormalizeSerializedState();
        BindButtons();
        UIScreenState.CurrentScreenChanged += HandleCurrentScreenChanged;
        HandleCurrentScreenChanged(UIScreenState.CurrentScreenId);
    }

    private void OnDisable()
    {
        UnbindButtons();
        UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
        SetTutorialVisible(false, false);
    }

    private void OnValidate()
    {
        _screenId = UIScreenState.NormalizeScreenId(_screenId);
        if (string.IsNullOrEmpty(_screenId))
            _screenId = DefaultScreenId;

        if (string.IsNullOrWhiteSpace(_tutorialSeenPrefsKey))
            _tutorialSeenPrefsKey = DefaultPrefsKey;

        ResolveDeckTargets();
    }

    public void AcknowledgeTutorial()
    {
        _tutorialAcknowledgedForEntry = true;
        _seenTutorialThisSession = true;
        SaveTutorialSeen();
        SetTutorialVisible(false, true);

        if (!_hasDeckBeenClickedThisSession)
            SetDeckAvailable(true);

        InvokeSafe(_tutorialAcknowledged, nameof(_tutorialAcknowledged));
    }

    public void HandleDeckClicked()
    {
        if (_tutorialVisible && !_tutorialAcknowledgedForEntry)
            return;

        _hasDeckBeenClickedThisSession = true;
        InvokeSafe(_deckClicked, nameof(_deckClicked));

        if (_disableDeckAfterClick)
            SetDeckAvailable(false);
    }

    public void ResetTutorialSeen()
    {
        _seenTutorialThisSession = false;
        _tutorialAcknowledgedForEntry = false;

        if (_persistSeenState)
            LocalSecurePrefs.Delete(GetPrefsKey());
    }

    public void MarkTutorialSeen()
    {
        _seenTutorialThisSession = true;
        SaveTutorialSeen();
    }

    public void SetDeckAvailable(bool available)
    {
        ResolveDeckTargets();
        CaptureDeckHomeState();

        if (_deckRaycastGraphic != null && _controlDeckGraphicRaycastTarget)
            _deckRaycastGraphic.raycastTarget = available && _homeDeckGraphicRaycastTarget;

        if (_deckCanvasGroup != null && _controlDeckCanvasGroupRaycasts)
            _deckCanvasGroup.blocksRaycasts = available && _homeDeckCanvasGroupBlocksRaycasts;

        _deckAvailabilityChanged.Invoke(available);
    }

    public void RefreshForCurrentScreen()
    {
        HandleCurrentScreenChanged(UIScreenState.CurrentScreenId);
    }

    private void HandleCurrentScreenChanged(string currentScreenId)
    {
        if (!UIScreenState.IsCurrent(_screenId))
        {
            SetTutorialVisible(false, false);
            return;
        }

        if (ShouldShowTutorial())
        {
            ShowTutorialForEntry();
            return;
        }

        SetTutorialVisible(false, false);

        if (!_hasDeckBeenClickedThisSession)
            SetDeckAvailable(true);
    }

    private void ShowTutorialForEntry()
    {
        _tutorialAcknowledgedForEntry = false;

        if (IsDebugEveryEntryActive())
            _hasDeckBeenClickedThisSession = false;

        SetTutorialVisible(true, true);

        if (_blockDeckUntilTutorialAcknowledged)
            SetDeckAvailable(false);
    }

    private bool ShouldShowTutorial()
    {
        return IsDebugEveryEntryActive() || !HasSeenTutorial();
    }

    private bool HasSeenTutorial()
    {
        if (_seenTutorialThisSession)
            return true;

        if (!_persistSeenState)
            return false;

        return LocalSecurePrefs.GetBool(GetPrefsKey(), LocalSaveSecurity.SetupFlagPurpose, false);
    }

    private void SaveTutorialSeen()
    {
        if (!_persistSeenState)
            return;

        LocalSecurePrefs.SetBool(GetPrefsKey(), LocalSaveSecurity.SetupFlagPurpose, true);
    }

    private string GetPrefsKey()
    {
        string safeKey = SaveDataSanitizer.SafeKeyPart(_tutorialSeenPrefsKey, DefaultPrefsKey, 96);
        return string.IsNullOrEmpty(safeKey) ? DefaultPrefsKey : safeKey;
    }

    private bool IsDebugEveryEntryActive()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return _showEveryEntryInDebug;
#else
        return false;
#endif
    }

    private void SetTutorialVisible(bool visible, bool invokeEvent)
    {
        if (_tutorialVisible == visible && Application.isPlaying)
        {
            ApplyTutorialVisibility(visible);
            return;
        }

        _tutorialVisible = visible;
        ApplyTutorialVisibility(visible);

        if (!invokeEvent)
            return;

        InvokeSafe(visible ? _tutorialShown : _tutorialHidden, visible ? nameof(_tutorialShown) : nameof(_tutorialHidden));
    }

    private void ApplyTutorialVisibility(bool visible)
    {
        if (visible && _applyMessageOnShow && _tutorialMessageText != null)
            _tutorialMessageText.text = _tutorialMessage ?? "";

        GameObject panelRoot = GetPanelRoot();
        if (_togglePanelRootActive && panelRoot != null && panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);

        if (_tutorialPanelCanvasGroup == null)
            return;

        _tutorialPanelCanvasGroup.alpha = visible ? 1f : 0f;
        _tutorialPanelCanvasGroup.interactable = visible;
        _tutorialPanelCanvasGroup.blocksRaycasts = visible;
    }

    private GameObject GetPanelRoot()
    {
        if (_tutorialPanelRoot != null)
            return _tutorialPanelRoot;

        return _tutorialPanelCanvasGroup != null ? _tutorialPanelCanvasGroup.gameObject : null;
    }

    private void BindButtons()
    {
        if (_bindUnderstoodButton && _understoodButton != null)
        {
            _understoodButton.onClick.RemoveListener(AcknowledgeTutorial);
            _understoodButton.onClick.AddListener(AcknowledgeTutorial);
        }

        if (_bindDeckButton && _deckButton != null)
        {
            _deckButton.onClick.RemoveListener(HandleDeckClicked);
            _deckButton.onClick.AddListener(HandleDeckClicked);
        }
    }

    private void UnbindButtons()
    {
        if (_understoodButton != null)
            _understoodButton.onClick.RemoveListener(AcknowledgeTutorial);

        if (_deckButton != null)
            _deckButton.onClick.RemoveListener(HandleDeckClicked);
    }

    private void ResolveDeckTargets()
    {
        if (_deckRaycastGraphic == null && _deckButton != null)
            _deckRaycastGraphic = _deckButton.targetGraphic;

        if (_deckCanvasGroup == null && _deckButton != null)
            _deckCanvasGroup = _deckButton.GetComponent<CanvasGroup>();
    }

    private void CaptureDeckHomeState()
    {
        if (_hasCapturedDeckState)
            return;

        if (_deckRaycastGraphic != null)
            _homeDeckGraphicRaycastTarget = _deckRaycastGraphic.raycastTarget;

        if (_deckCanvasGroup != null)
            _homeDeckCanvasGroupBlocksRaycasts = _deckCanvasGroup.blocksRaycasts;

        _hasCapturedDeckState = _deckButton != null || _deckRaycastGraphic != null || _deckCanvasGroup != null;
    }

    private void NormalizeSerializedState()
    {
        if (string.IsNullOrEmpty(_screenId))
            _screenId = DefaultScreenId;

        if (string.IsNullOrWhiteSpace(_tutorialSeenPrefsKey))
            _tutorialSeenPrefsKey = DefaultPrefsKey;
    }

    private void InvokeSafe(UnityEvent unityEvent, string eventName)
    {
        try
        {
            unityEvent?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"DivinationTutorialController: event '{eventName}' failed: {exception.Message}", this);
        }
    }
}
