using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[AddComponentMenu("Nocturne/UI/Main/Prediction Offer Button")]
public sealed class PredictionOfferButtonController : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField, Tooltip("Можно оставить пустым: клик будет принят через дочерний Image.")]
    private Button _button;

    [Header("State")]
    [SerializeField, Tooltip("Пока сервер не прислал поручение, должно быть выключено.")]
    private bool _availableOnStart;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

    [Header("Future card opening")]
    [SerializeField] private UnityEvent _onPredictionRequested = new UnityEvent();

    [Header("Server assignment")]
    [SerializeField] private string _targetScreenId = "CardScreenMainMenu";
    [SerializeField, Min(0.1f)] private float _authenticationWaitSeconds = 10f;

    private bool _isAvailable;
    private MainMenuPredictionOfferContent _content;
    private Coroutine _refreshRoutine;
    private StoryScreenNavigator _screenNavigator;

    public bool IsAvailable => _isAvailable;
    public event Action PredictionRequested;

    private void Awake()
    {
        ResolveReferences();
        _isAvailable = _availableOnStart;
        ApplyImmediate(_isAvailable);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButton();
        NetworkManager.OnUiTextsUpdated += HandleUiTextsUpdated;
        NetworkManager.OnProfileUpdated += HandleProfileUpdated;
        NetworkManager.OnConnectivityChanged += HandleConnectivityChanged;

        if (Application.isPlaying)
            RefreshAssignment();
        else
            ApplyImmediate(_isAvailable);
    }

    private void OnDisable()
    {
        NetworkManager.OnUiTextsUpdated -= HandleUiTextsUpdated;
        NetworkManager.OnProfileUpdated -= HandleProfileUpdated;
        NetworkManager.OnConnectivityChanged -= HandleConnectivityChanged;
        StopRefresh();
        UnbindButton();
        if (_canvasGroup != null)
            DOTween.Kill(_canvasGroup);
    }

    private void OnValidate()
    {
        _fadeDuration = Mathf.Max(0f, _fadeDuration);
        ResolveReferences();
    }

    public void SetAssignmentAvailable(bool available)
    {
        _isAvailable = available;
        ApplyVisibility(available);
    }

    public void HideUntilServerAssignment()
    {
        SetAssignmentAvailable(false);
    }

    public void OnPredictionButtonClicked()
    {
        if (!_isAvailable || _content == null || !_content.IsValid)
            return;

        MainMenuPredictionCardScreenController screen =
            PredictionOfferButtonInstaller.GetOrCreateCardScreenController();
        if (screen == null)
        {
            Debug.LogWarning("[PredictionOffer] CardScreenMainMenu is missing.", this);
            return;
        }

        screen.Show(_content);
        if (!OpenTargetScreen())
            return;

        _onPredictionRequested?.Invoke();
        PredictionRequested?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null && eventData != null && eventData.button == PointerEventData.InputButton.Left)
            OnPredictionButtonClicked();
    }

    [ContextMenu("Debug/Show offer")]
    private void DebugShowOffer() => SetAssignmentAvailable(true);

    [ContextMenu("Debug/Hide offer")]
    private void DebugHideOffer() => SetAssignmentAvailable(false);

    public void RefreshAssignment(bool force = false)
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
            return;

        StopRefresh();
        _content = null;
        SetAssignmentAvailable(false);
        _refreshRoutine = StartCoroutine(RefreshAssignmentRoutine(force));
    }

    private IEnumerator RefreshAssignmentRoutine(bool force)
    {
        float waited = 0f;
        while ((NetworkManager.Instance == null || !NetworkManager.IsAuthenticated) &&
               waited < _authenticationWaitSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
        {
            _refreshRoutine = null;
            yield break;
        }

        MainMenuPredictionOfferContent content = null;
        string error = "";
        yield return NetworkManager.Instance.FetchMainMenuPredictionOffer(
            (loaded, message) =>
            {
                content = loaded;
                error = message ?? "";
            },
            force);

        _content = content;
        SetAssignmentAvailable(content != null && content.IsValid);
        _refreshRoutine = null;

        if (content == null && !string.IsNullOrWhiteSpace(error))
        {
            ThrottledAppLogger.Warn(
                nameof(PredictionOfferButtonController) + ".RefreshFailed",
                AppLogCategory.Network,
                nameof(PredictionOfferButtonController),
                nameof(RefreshAssignmentRoutine),
                "Main menu prediction offer is unavailable.",
                LogMetadata.Of("error", error));
        }
    }

    private void HandleUiTextsUpdated()
    {
        if (!isActiveAndEnabled)
            return;

        if (NetworkManager.TryGetMainMenuPredictionOffer(
                NetworkManager.ResolveUiTextLocale(),
                out MainMenuPredictionOfferContent content))
        {
            _content = content;
            SetAssignmentAvailable(true);
            return;
        }

        _content = null;
        SetAssignmentAvailable(false);
    }

    private void HandleProfileUpdated()
    {
        RefreshAssignment(true);
    }

    private void HandleConnectivityChanged(bool online, string message)
    {
        if (online)
            RefreshAssignment(true);
    }

    private bool OpenTargetScreen()
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        string targetScreenId = UIScreenState.NormalizeScreenId(_targetScreenId);
        if (_screenNavigator != null && _screenNavigator.OpenScreen(targetScreenId))
            return true;

        Debug.LogWarning($"[PredictionOffer] Screen '{targetScreenId}' is unavailable.", this);
        return false;
    }

    private void StopRefresh()
    {
        if (_refreshRoutine == null)
            return;

        StopCoroutine(_refreshRoutine);
        _refreshRoutine = null;
    }

    private void ApplyVisibility(bool visible)
    {
        ResolveReferences();
        if (_canvasGroup == null)
            return;

        DOTween.Kill(_canvasGroup);
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;

        if (!Application.isPlaying || _fadeDuration <= 0f)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            return;
        }

        _canvasGroup.DOFade(visible ? 1f : 0f, _fadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(_canvasGroup);
    }

    private void ApplyImmediate(bool visible)
    {
        if (_canvasGroup == null)
            return;

        DOTween.Kill(_canvasGroup);
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void ResolveReferences()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_button == null)
            _button = GetComponentInChildren<Button>(true);

        _targetScreenId = UIScreenState.NormalizeScreenId(_targetScreenId);
        if (string.IsNullOrEmpty(_targetScreenId))
            _targetScreenId = "CardScreenMainMenu";
        _authenticationWaitSeconds = Mathf.Max(0.1f, _authenticationWaitSeconds);
    }

    private void BindButton()
    {
        if (_button == null)
            return;
        _button.onClick.RemoveListener(OnPredictionButtonClicked);
        _button.onClick.AddListener(OnPredictionButtonClicked);
    }

    private void UnbindButton()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnPredictionButtonClicked);
    }
}
