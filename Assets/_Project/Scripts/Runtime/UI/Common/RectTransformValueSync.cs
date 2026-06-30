using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RectTransformValueSync : MonoBehaviour, ILayoutElement
{
    private enum SourceMode
    {
        Auto,
        Explicit,
        Self,
        ChildByName,
        FirstChild
    }

    private enum TargetMode
    {
        Auto,
        Explicit,
        Self,
        Parent
    }

    private const float SizeEpsilon = 0.1f;

    [Header("Source")]
    [SerializeField] private SourceMode _sourceMode = SourceMode.Auto;
    [SerializeField] private RectTransform _source;
    [SerializeField] private string _childName = "Container";
    [SerializeField] private bool _searchNestedChildren = true;

    [Header("Target")]
    [SerializeField] private TargetMode _targetMode = TargetMode.Auto;
    [SerializeField] private RectTransform _target;

    [Header("When")]
    [SerializeField] private bool _applyOnEnable = true;
    [SerializeField] private bool _applyOnValidate = true;
    [SerializeField] private bool _applyInEditMode = true;
    [SerializeField] private bool _applyEveryFrame;
    [SerializeField] private bool _forceCanvasUpdateBeforeRead = true;
    [SerializeField] private bool _rebuildTargetParentLayout = true;

    [Header("Size")]
    [SerializeField] private bool _copyWidth = true;
    [SerializeField] private bool _copyHeight = true;
    [SerializeField] private Vector2 _sizeOffset;
    [SerializeField] private Vector2 _minimumSize;

    [Header("Optional Rect Values")]
    [SerializeField] private bool _copyAnchors;
    [SerializeField] private bool _copyPivot;
    [SerializeField] private bool _copyAnchoredPosition;
    [SerializeField] private bool _copyOffsets;
    [SerializeField] private bool _copyRotation;
    [SerializeField] private bool _copyScale;

    [Header("LayoutElement")]
    [SerializeField] private bool _writeLayoutElement;
    [SerializeField] private bool _createTargetLayoutElement;
    [SerializeField] private bool _copySourceLayoutElement = true;
    [SerializeField] private bool _useRectAsPreferredFallback = true;
    [SerializeField] private bool _copyIgnoreLayout;
    [SerializeField] private bool _copyLayoutPriority = true;
    [SerializeField] private bool _zeroFlexibleSizeWhenUsingRectFallback = true;

    private RectTransform _selfRect;
    private bool _isApplying;
    private float _layoutMinWidth = -1f;
    private float _layoutMinHeight = -1f;
    private float _layoutPreferredWidth = -1f;
    private float _layoutPreferredHeight = -1f;
    private float _layoutFlexibleWidth;
    private float _layoutFlexibleHeight;
    private int _layoutPriority = 1;

    public float minWidth
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutMinWidth;
        }
    }

    public float preferredWidth
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutPreferredWidth;
        }
    }

    public float flexibleWidth
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutFlexibleWidth;
        }
    }

    public float minHeight
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutMinHeight;
        }
    }

    public float preferredHeight
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutPreferredHeight;
        }
    }

    public float flexibleHeight
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutFlexibleHeight;
        }
    }

    public int layoutPriority
    {
        get
        {
            RefreshLayoutCache(false);
            return _layoutPriority;
        }
    }

    private void Awake()
    {
        CacheSelf();
    }

    private void OnEnable()
    {
        CacheSelf();

        if (_applyOnEnable)
            Apply();
    }

    private void Start()
    {
        if (_applyOnEnable)
            Apply();
    }

    private void LateUpdate()
    {
        if (_applyEveryFrame)
            Apply();
    }

    private void OnTransformChildrenChanged()
    {
        if (_applyOnEnable)
            Apply();
    }

    private void OnValidate()
    {
        CacheSelf();

        if (_applyOnValidate)
            Apply();
    }

    [ContextMenu("Apply Now")]
    public void ApplyFromContextMenu()
    {
        Apply(true);
    }

    public void Apply()
    {
        Apply(false);
    }

    public void Apply(bool forceInEditMode)
    {
        if (_isApplying)
            return;

        if (!Application.isPlaying && !_applyInEditMode && !forceInEditMode)
            return;

        CacheSelf();

        RectTransform source = ResolveSource();
        RectTransform target = ResolveTarget();
        if (source == null || target == null)
            return;

        _isApplying = true;

        try
        {
            if (_forceCanvasUpdateBeforeRead)
                Canvas.ForceUpdateCanvases();

            LayoutRebuilder.ForceRebuildLayoutImmediate(source);

            Vector2 sourceSize = source.rect.size + _sizeOffset;
            sourceSize.x = Mathf.Max(_minimumSize.x, sourceSize.x);
            sourceSize.y = Mathf.Max(_minimumSize.y, sourceSize.y);

            RefreshLayoutCache(source, sourceSize);
            ApplyRectValues(source, target, sourceSize);
            ApplyLayoutElement(source, target, sourceSize);

            if (_rebuildTargetParentLayout && target.parent is RectTransform parent)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void CalculateLayoutInputHorizontal()
    {
        RefreshLayoutCache(false);
    }

    public void CalculateLayoutInputVertical()
    {
        RefreshLayoutCache(false);
    }

    private void ApplyRectValues(RectTransform source, RectTransform target, Vector2 sourceSize)
    {
        if (_copyAnchors)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
        }

        if (_copyPivot)
            target.pivot = source.pivot;

        if (_copyAnchoredPosition)
            target.anchoredPosition3D = source.anchoredPosition3D;

        if (_copyWidth)
            SetSizeIfChanged(target, RectTransform.Axis.Horizontal, sourceSize.x);

        if (_copyHeight)
            SetSizeIfChanged(target, RectTransform.Axis.Vertical, sourceSize.y);

        if (_copyOffsets)
        {
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }

        if (_copyRotation)
            target.localRotation = source.localRotation;

        if (_copyScale)
            target.localScale = source.localScale;
    }

    private void ApplyLayoutElement(RectTransform source, RectTransform target, Vector2 sourceSize)
    {
        if (!_writeLayoutElement)
            return;

        LayoutElement targetLayout = target.GetComponent<LayoutElement>();
        if (targetLayout == null && _createTargetLayoutElement)
            targetLayout = target.gameObject.AddComponent<LayoutElement>();

        if (targetLayout == null)
            return;

        LayoutElement sourceLayout = source.GetComponent<LayoutElement>();
        if (sourceLayout != null && _copySourceLayoutElement)
            CopyLayoutElement(sourceLayout, targetLayout, sourceSize);
        else
            ApplyPreferredSizeFromRect(targetLayout, sourceSize);
    }

    private void CopyLayoutElement(LayoutElement sourceLayout, LayoutElement targetLayout, Vector2 sourceSize)
    {
        if (_copyIgnoreLayout)
            targetLayout.ignoreLayout = sourceLayout.ignoreLayout;

        targetLayout.minWidth = sourceLayout.minWidth;
        targetLayout.minHeight = sourceLayout.minHeight;
        targetLayout.preferredWidth = ResolvePreferredValue(sourceLayout.preferredWidth, sourceSize.x);
        targetLayout.preferredHeight = ResolvePreferredValue(sourceLayout.preferredHeight, sourceSize.y);
        targetLayout.flexibleWidth = sourceLayout.flexibleWidth;
        targetLayout.flexibleHeight = sourceLayout.flexibleHeight;

        if (_copyLayoutPriority)
            targetLayout.layoutPriority = sourceLayout.layoutPriority;
    }

    private void ApplyPreferredSizeFromRect(LayoutElement targetLayout, Vector2 sourceSize)
    {
        targetLayout.preferredWidth = sourceSize.x;
        targetLayout.preferredHeight = sourceSize.y;

        if (_zeroFlexibleSizeWhenUsingRectFallback)
        {
            targetLayout.flexibleWidth = 0f;
            targetLayout.flexibleHeight = 0f;
        }
    }

    private float ResolvePreferredValue(float layoutValue, float rectValue)
    {
        return _useRectAsPreferredFallback && layoutValue < 0f
            ? rectValue
            : layoutValue;
    }

    private void RefreshLayoutCache(bool forceCanvasUpdate)
    {
        CacheSelf();

        RectTransform source = ResolveSource();
        if (source == null)
        {
            ResetLayoutCache();
            return;
        }

        if (forceCanvasUpdate && _forceCanvasUpdateBeforeRead)
            Canvas.ForceUpdateCanvases();

        Vector2 sourceSize = source.rect.size + _sizeOffset;
        sourceSize.x = Mathf.Max(_minimumSize.x, sourceSize.x);
        sourceSize.y = Mathf.Max(_minimumSize.y, sourceSize.y);
        RefreshLayoutCache(source, sourceSize);
    }

    private void RefreshLayoutCache(RectTransform source, Vector2 sourceSize)
    {
        LayoutElement sourceLayout = source != null ? source.GetComponent<LayoutElement>() : null;
        if (sourceLayout != null && _copySourceLayoutElement)
        {
            _layoutMinWidth = sourceLayout.minWidth;
            _layoutMinHeight = sourceLayout.minHeight;
            _layoutPreferredWidth = ResolvePreferredValue(sourceLayout.preferredWidth, sourceSize.x);
            _layoutPreferredHeight = ResolvePreferredValue(sourceLayout.preferredHeight, sourceSize.y);
            _layoutFlexibleWidth = sourceLayout.flexibleWidth;
            _layoutFlexibleHeight = sourceLayout.flexibleHeight;
            _layoutPriority = _copyLayoutPriority ? sourceLayout.layoutPriority : 1;
            return;
        }

        _layoutMinWidth = -1f;
        _layoutMinHeight = -1f;
        _layoutPreferredWidth = sourceSize.x;
        _layoutPreferredHeight = sourceSize.y;
        _layoutFlexibleWidth = _zeroFlexibleSizeWhenUsingRectFallback ? 0f : -1f;
        _layoutFlexibleHeight = _zeroFlexibleSizeWhenUsingRectFallback ? 0f : -1f;
        _layoutPriority = 1;
    }

    private void ResetLayoutCache()
    {
        _layoutMinWidth = -1f;
        _layoutMinHeight = -1f;
        _layoutPreferredWidth = -1f;
        _layoutPreferredHeight = -1f;
        _layoutFlexibleWidth = 0f;
        _layoutFlexibleHeight = 0f;
        _layoutPriority = 1;
    }

    private RectTransform ResolveSource()
    {
        switch (_sourceMode)
        {
            case SourceMode.Explicit:
                return _source;
            case SourceMode.Self:
                return _selfRect;
            case SourceMode.ChildByName:
                return FindChildRect(_childName);
            case SourceMode.FirstChild:
                return GetFirstChildRect();
            default:
                return ResolveAutoSource();
        }
    }

    private RectTransform ResolveTarget()
    {
        switch (_targetMode)
        {
            case TargetMode.Explicit:
                return _target;
            case TargetMode.Self:
                return _selfRect;
            case TargetMode.Parent:
                return transform.parent as RectTransform;
            default:
                return ResolveAutoTarget();
        }
    }

    private RectTransform ResolveAutoSource()
    {
        if (_source != null)
            return _source;

        if (IsNamedContainer(gameObject.name))
            return _selfRect;

        RectTransform child = FindChildRect(_childName);
        return child != null ? child : GetFirstChildRect();
    }

    private RectTransform ResolveAutoTarget()
    {
        if (_target != null)
            return _target;

        if (IsNamedContainer(gameObject.name) && transform.parent is RectTransform parent)
            return parent;

        return _selfRect;
    }

    private RectTransform FindChildRect(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        foreach (Transform child in transform)
        {
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        }

        if (!_searchNestedChildren)
            return null;

        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform child = children[i];
            if (child != null && child != _selfRect &&
                string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private RectTransform GetFirstChildRect()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i) is RectTransform child)
                return child;
        }

        return null;
    }

    private bool IsNamedContainer(string objectName)
    {
        return !string.IsNullOrWhiteSpace(_childName) &&
               string.Equals(objectName, _childName, StringComparison.OrdinalIgnoreCase);
    }

    private void CacheSelf()
    {
        if (_selfRect == null)
            _selfRect = GetComponent<RectTransform>();
    }

    private static void SetSizeIfChanged(RectTransform rectTransform, RectTransform.Axis axis, float value)
    {
        float current = axis == RectTransform.Axis.Horizontal
            ? rectTransform.rect.width
            : rectTransform.rect.height;

        if (Mathf.Abs(current - value) > SizeEpsilon)
            rectTransform.SetSizeWithCurrentAnchors(axis, value);
    }
}
