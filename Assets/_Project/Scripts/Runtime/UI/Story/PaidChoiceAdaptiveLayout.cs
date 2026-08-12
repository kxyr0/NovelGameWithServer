using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Moves Button/Cost only downward when the real Button height grows.
/// The authored Cost position is the center/base position. Width, size,
/// BodyText and Container are never changed by this component.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PaidChoiceAdaptiveLayout : MonoBehaviour
{
    [Header("Cost only")]
    [SerializeField] private RectTransform _cost;
    [SerializeField] private RectTransform _buttonRect;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private RectTransform _image;
    [SerializeField] private PriceIconPreferredWidthSpacing _priceSpacing;

    [Header("Universal height tracking")]
    [Tooltip("Button height at which Cost remains in its manually authored center. Zero captures the height when the runtime instance is created.")]
    [SerializeField, Min(0f)] private float _referenceButtonHeight;
    [InspectorName("Downward Offset Per Step")]
    [Tooltip("Exact distance Cost moves down for each rendered BodyText line: 0 lines = center, 1 line = one step, 2 lines = two steps.")]
    [FormerlySerializedAs("_downwardMovementPerHeightUnit")]
    [SerializeField, Min(0f)] private float _downwardOffset = 25f;

    [SerializeField, HideInInspector] private Vector2 _centerAnchoredPosition;
    [SerializeField, HideInInspector] private bool _hasCenterAnchoredPosition;

    private float _runtimeReferenceHeight;
    private float _lastButtonHeight = float.NaN;
    private string _lastCostValue;
    private TMP_Text _bodyText;
    private string _lastBodyValue;
    private bool _initialized;
    private bool _subscribed;
    private bool _refreshing;
    private bool _layoutDirty = true;

    public RectTransform Cost => _cost;
    public TMP_Text CostText => _costText;
    public RectTransform Image => _image;
    public float ReferenceButtonHeight => _referenceButtonHeight;
    public float DownwardOffset => _downwardOffset;

    public void Configure(Button choiceButton)
    {
        _buttonRect = choiceButton != null ? choiceButton.transform as RectTransform : null;
        AutoWireExactCostHierarchy();

        if (!_initialized)
            InitializeFromAuthoredCenter();

        RefreshNow();
    }

    public void SetHeightTracking(float referenceButtonHeight, float downwardOffset)
    {
        _referenceButtonHeight = Mathf.Max(0f, referenceButtonHeight);
        _downwardOffset = Mathf.Max(0f, downwardOffset);

        if (_initialized)
            _runtimeReferenceHeight = ResolveReferenceHeight();

        RefreshNow();
    }

    [ContextMenu("Use Current Cost Position As Center")]
    public void CaptureCurrentPositionAsCenter()
    {
        AutoWireExactCostHierarchy();
        if (!HasValidCostHierarchy())
            return;

        // Keep the anchors exactly as authored. This value is serialized so an
        // expanded editor preview cannot become the new center after a script reload.
        _centerAnchoredPosition = _cost.anchoredPosition;
        _hasCenterAnchoredPosition = true;
        _runtimeReferenceHeight = ResolveReferenceHeight();
        _layoutDirty = true;
        _initialized = true;
    }

    [ContextMenu("Refresh Cost Now")]
    public void RefreshNow()
    {
        if (_refreshing)
            return;

        AutoWireExactCostHierarchy();
        if (!HasValidCostHierarchy())
            return;

        if (!_initialized)
            InitializeFromAuthoredCenter();

        _refreshing = true;
        try
        {
            float currentHeight = Mathf.Max(0f, _buttonRect.rect.height);
            int expansionSteps = ResolveExpansionSteps(currentHeight);
            float downwardDistance = expansionSteps * _downwardOffset;

            // Pure vertical movement. Anchors, X and size stay exactly as authored.
            _cost.anchoredPosition = new Vector2(
                _centerAnchoredPosition.x,
                _centerAnchoredPosition.y - downwardDistance);

            if (_costText != null)
                _costText.ForceMeshUpdate(true, true);

            if (_priceSpacing != null)
            {
                _priceSpacing.MarkDirty();
                _priceSpacing.RefreshNow();
            }

            _lastButtonHeight = currentHeight;
            _lastCostValue = _costText != null ? _costText.text : null;
            _lastBodyValue = _bodyText != null ? _bodyText.text : null;
            _layoutDirty = false;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Awake()
    {
        AutoWireExactCostHierarchy();
        InitializeFromAuthoredCenter();
    }

    private void OnEnable()
    {
        AutoWireExactCostHierarchy();
        Subscribe();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;

        if (!_initialized)
            InitializeFromAuthoredCenter();
    }

    private void OnValidate()
    {
        _referenceButtonHeight = Mathf.Max(0f, _referenceButtonHeight);
        _downwardOffset = Mathf.Max(0f, _downwardOffset);
        AutoWireExactCostHierarchy();

        if (!HasValidCostHierarchy())
            return;

        if (!_initialized)
            InitializeFromAuthoredCenter();
        else
            _runtimeReferenceHeight = ResolveReferenceHeight();

        _layoutDirty = true;
        if (isActiveAndEnabled)
            RefreshNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
    }

    private void LateUpdate()
    {
        CaptureManualEditorCenterAtReferenceHeight();

        if (HasRuntimeLayoutChanged())
            RefreshNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_refreshing || !isActiveAndEnabled)
            return;

        _layoutDirty = true;
    }

    private void HandleWillRenderCanvases()
    {
        if (isActiveAndEnabled && HasRuntimeLayoutChanged())
            RefreshNow();
    }

    private void HandleTextChanged(Object changedObject)
    {
        if (changedObject == _costText || changedObject == _bodyText)
        {
            _lastCostValue = null;
            _lastBodyValue = null;
            _layoutDirty = true;
        }
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        _subscribed = false;
    }

    private float ResolveReferenceHeight()
    {
        if (_referenceButtonHeight > 0f)
            return _referenceButtonHeight;

        return _buttonRect != null ? Mathf.Max(0f, _buttonRect.rect.height) : 0f;
    }

    private int ResolveExpansionSteps(float currentButtonHeight)
    {
        if (_bodyText != null)
        {
            if (string.IsNullOrWhiteSpace(_bodyText.text))
                return 0;

            _bodyText.ForceMeshUpdate(true, true);
            return Mathf.Max(1, _bodyText.textInfo.lineCount);
        }

        return currentButtonHeight > _runtimeReferenceHeight + 0.5f ? 1 : 0;
    }

    private void InitializeFromAuthoredCenter()
    {
        AutoWireExactCostHierarchy();
        if (!HasValidCostHierarchy())
            return;

        if (!_hasCenterAnchoredPosition)
        {
            _centerAnchoredPosition = _cost.anchoredPosition;
            _hasCenterAnchoredPosition = true;
        }

        _runtimeReferenceHeight = ResolveReferenceHeight();
        _layoutDirty = true;
        _initialized = true;
    }

    private void CaptureManualEditorCenterAtReferenceHeight()
    {
        if (Application.isPlaying || !_initialized || _cost == null || _buttonRect == null)
            return;

        float currentHeight = Mathf.Max(0f, _buttonRect.rect.height);
        if (currentHeight > _runtimeReferenceHeight + 0.5f)
            return;

        // Do not mistake the old shifted position for a new manual center while
        // the Button is transitioning from expanded back to its reference height.
        if (float.IsNaN(_lastButtonHeight) || _lastButtonHeight > _runtimeReferenceHeight + 0.5f)
            return;

        if ((_cost.anchoredPosition - _centerAnchoredPosition).sqrMagnitude <= 0.0001f)
            return;

        _centerAnchoredPosition = _cost.anchoredPosition;
        _hasCenterAnchoredPosition = true;
        _layoutDirty = true;
    }

    private bool HasRuntimeLayoutChanged()
    {
        if (!_initialized || _buttonRect == null)
            return !_initialized;

        if (_layoutDirty)
            return true;

        bool textChanged = _costText != null &&
                           !string.Equals(_lastCostValue, _costText.text, System.StringComparison.Ordinal);
        bool bodyChanged = _bodyText != null &&
                           !string.Equals(_lastBodyValue, _bodyText.text, System.StringComparison.Ordinal);

        return Mathf.Abs(_buttonRect.rect.height - _lastButtonHeight) > 0.1f || textChanged || bodyChanged;
    }

    private void AutoWireExactCostHierarchy()
    {
        _cost = transform as RectTransform;
        if (_cost == null || !string.Equals(_cost.name, "Cost", System.StringComparison.Ordinal))
        {
            ClearChildReferences();
            return;
        }

        RectTransform directParent = _cost.parent as RectTransform;
        if (directParent == null || !string.Equals(directParent.name, "Button", System.StringComparison.Ordinal))
        {
            _buttonRect = null;
            ClearChildReferences();
            return;
        }

        _buttonRect = directParent;

        Transform container = _buttonRect.parent;
        Transform bodyTextObject = container != null ? container.Find("BodyText") : null;
        _bodyText = bodyTextObject != null
            ? bodyTextObject.GetComponent<TMP_Text>()
            : null;

        Transform costTextChild = _cost.Find("CostText");
        _costText = costTextChild != null && costTextChild.parent == _cost
            ? costTextChild.GetComponent<TMP_Text>()
            : null;

        Transform imageChild = _cost.Find("Image");
        _image = imageChild != null && imageChild.parent == _cost
            ? imageChild as RectTransform
            : null;

        _priceSpacing = _costText != null
            ? _costText.GetComponent<PriceIconPreferredWidthSpacing>()
            : null;
    }

    private bool HasValidCostHierarchy()
    {
        return _cost != null &&
               _buttonRect != null &&
               _cost.parent == _buttonRect;
    }

    private void ClearChildReferences()
    {
        _costText = null;
        _image = null;
        _priceSpacing = null;
        _bodyText = null;
    }
}
