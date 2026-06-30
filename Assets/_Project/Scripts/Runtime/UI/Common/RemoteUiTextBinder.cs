using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Nocturne/UI/Remote UI Text Binder")]
public sealed class RemoteUiTextBinder : MonoBehaviour
{
    [Header("Remote text")]
    [SerializeField] private string _textId = "";
    [SerializeField] private TMP_Text _targetText;
    [SerializeField] private GameObject _visibilityRoot;

    [Header("Context")]
    [SerializeField] private string _screenId = "";
    [SerializeField] private string _storyId = "";
    [SerializeField] private bool _useActiveStoryId = true;
    [SerializeField] private string _localeOverride = "";

    [Header("Behavior")]
    [SerializeField] private bool _refreshOnEnable = true;
    [SerializeField] private bool _collapseLayoutWhenHidden = true;
    [SerializeField] private bool _autoAddVisibilityComponents = true;

    private Coroutine _refreshRoutine;
    private CanvasGroup _visibilityCanvasGroup;
    private LayoutElement _layoutElement;

    public string TextId => _textId;
    public TMP_Text TargetText => _targetText;
    public GameObject VisibilityRoot => _visibilityRoot;

    private void Awake()
    {
        ResolveReferences(false);
    }

    private void OnEnable()
    {
        NetworkManager.OnUiTextsUpdated += HandleUiTextsUpdated;
        HideImmediate();

        if (_refreshOnEnable && Application.isPlaying)
            StartRefresh();
        else
            ApplyCachedText();
    }

    private void OnDisable()
    {
        NetworkManager.OnUiTextsUpdated -= HandleUiTextsUpdated;
        StopRefresh();
    }

    private void OnDestroy()
    {
        NetworkManager.OnUiTextsUpdated -= HandleUiTextsUpdated;
        StopRefresh();
    }

    private void OnValidate()
    {
        _textId = SaveDataSanitizer.SanitizeIdentifier(_textId);
        _screenId = SaveDataSanitizer.SanitizeIdentifier(_screenId);
        _storyId = SaveDataSanitizer.SanitizeIdentifier(_storyId);
        _localeOverride = SaveDataSanitizer.SanitizeIdentifier(_localeOverride);
        ResolveReferences(false);
    }

    [ContextMenu("Prepare Visibility Components")]
    public void PrepareVisibilityComponents()
    {
        ResolveReferences(true);
    }

    [ContextMenu("Refresh Remote Text")]
    public void RefreshRemoteText()
    {
        if (!isActiveAndEnabled)
            return;

        StopRefresh();
        HideImmediate();
        _refreshRoutine = StartCoroutine(RefreshAndApplyRoutine(force: true));
    }

    [ContextMenu("Apply Cached Text")]
    public void ApplyCachedText()
    {
        ResolveReferences(false);
        if (_targetText == null)
            return;

        string text;
        if (NetworkManager.TryGetUiText(
                _textId,
                ResolveScreenId(),
                ResolveStoryId(),
                ResolveLocale(),
                out text))
        {
            ShowText(text);
            return;
        }

        HideImmediate();
    }

    private void StartRefresh()
    {
        StopRefresh();
        _refreshRoutine = StartCoroutine(RefreshAndApplyRoutine(force: false));
    }

    private void StopRefresh()
    {
        if (_refreshRoutine == null)
            return;

        StopCoroutine(_refreshRoutine);
        _refreshRoutine = null;
    }

    private IEnumerator RefreshAndApplyRoutine(bool force)
    {
        ResolveReferences(false);
        if (_targetText == null || string.IsNullOrWhiteSpace(_textId))
        {
            HideImmediate();
            _refreshRoutine = null;
            yield break;
        }

        while (NetworkManager.Instance == null || !NetworkManager.AuthFlowCompleted)
            yield return null;

        if (!NetworkManager.IsAuthenticated)
        {
            HideImmediate();
            _refreshRoutine = null;
            yield break;
        }

        bool ok = false;
        string error = "";
        yield return NetworkManager.Instance.RefreshUiTexts(
            ResolveScreenId(),
            ResolveStoryId(),
            ResolveLocale(),
            (success, message) =>
            {
                ok = success;
                error = message;
            },
            force);

        if (ok)
            ApplyCachedText();
        else
            HideImmediate();

        if (!ok && !string.IsNullOrWhiteSpace(error))
        {
            ThrottledAppLogger.Warn(
                nameof(RemoteUiTextBinder) + ".RefreshFailed:" + _textId,
                AppLogCategory.Network,
                nameof(RemoteUiTextBinder),
                nameof(RefreshAndApplyRoutine),
                "Remote UI text refresh failed.",
                LogMetadata.Of(
                    "textId", SaveDataSanitizer.SanitizeIdentifier(_textId),
                    "screenId", ResolveScreenId(),
                    "storyId", ResolveStoryId(),
                    "error", error));
        }

        _refreshRoutine = null;
    }

    private void HandleUiTextsUpdated()
    {
        if (isActiveAndEnabled && _refreshRoutine == null)
            ApplyCachedText();
    }

    private void ShowText(string value)
    {
        ResolveReferences(false);
        if (_targetText == null)
            return;

        _targetText.text = value ?? "";
        SetVisible(true);
    }

    private void HideImmediate()
    {
        ResolveReferences(false);
        if (_targetText != null)
            _targetText.text = "";

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        ResolveReferences(_autoAddVisibilityComponents);
        if (_targetText == null)
            return;

        bool hasSeparateRoot = _visibilityRoot != null && _visibilityRoot != gameObject;
        if (hasSeparateRoot)
        {
            _targetText.enabled = true;
            CanvasGroup group = ResolveCanvasGroup(_autoAddVisibilityComponents);
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
        }
        else
        {
            _targetText.enabled = visible;
        }

        ApplyLayoutCollapse(!visible);
    }

    private void ApplyLayoutCollapse(bool hidden)
    {
        if (!_collapseLayoutWhenHidden)
            return;

        LayoutElement element = ResolveLayoutElement(_autoAddVisibilityComponents);
        if (element != null)
            element.ignoreLayout = hidden;
    }

    private void ResolveReferences(bool createVisibilityComponents)
    {
        if (_targetText == null)
        {
            _targetText = GetComponent<TMP_Text>();
            if (_targetText == null)
                _targetText = GetComponentInChildren<TMP_Text>(true);
        }

        if (createVisibilityComponents)
        {
            ResolveCanvasGroup(true);
            ResolveLayoutElement(true);
        }
    }

    private CanvasGroup ResolveCanvasGroup(bool createIfMissing)
    {
        if (_visibilityRoot == null || _visibilityRoot == gameObject)
            return null;

        if (_visibilityCanvasGroup != null && _visibilityCanvasGroup.gameObject == _visibilityRoot)
            return _visibilityCanvasGroup;

        _visibilityCanvasGroup = _visibilityRoot.GetComponent<CanvasGroup>();
        if (_visibilityCanvasGroup == null && createIfMissing)
            _visibilityCanvasGroup = _visibilityRoot.AddComponent<CanvasGroup>();

        return _visibilityCanvasGroup;
    }

    private LayoutElement ResolveLayoutElement(bool createIfMissing)
    {
        GameObject target = _visibilityRoot != null ? _visibilityRoot : (_targetText != null ? _targetText.gameObject : gameObject);
        if (target == null)
            return null;

        if (_layoutElement != null && _layoutElement.gameObject == target)
            return _layoutElement;

        _layoutElement = target.GetComponent<LayoutElement>();
        if (_layoutElement == null && createIfMissing)
            _layoutElement = target.AddComponent<LayoutElement>();

        return _layoutElement;
    }

    private string ResolveScreenId()
    {
        return SaveDataSanitizer.SanitizeIdentifier(_screenId);
    }

    private string ResolveStoryId()
    {
        string explicitStoryId = SaveDataSanitizer.SanitizeIdentifier(_storyId);
        if (!string.IsNullOrEmpty(explicitStoryId) || !_useActiveStoryId)
            return explicitStoryId;

        return NetworkManager.ResolveActiveStoryIdForUiTexts();
    }

    private string ResolveLocale()
    {
        return NetworkManager.ResolveUiTextLocale(_localeOverride);
    }
}
