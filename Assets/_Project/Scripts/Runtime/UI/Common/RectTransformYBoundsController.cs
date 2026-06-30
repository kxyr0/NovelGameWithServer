using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RectTransformYBoundsController : MonoBehaviour, ILayoutSelfController
{
    private const float Epsilon = 0.01f;

    [Header("Цель")]
    [InspectorName("RectTransform")]
    [Tooltip("RectTransform, который нужно ограничивать. Если пусто, используется объект с этим скриптом.")]
    [SerializeField] private RectTransform _target;

    [Header("Позиция Y")]
    [InspectorName("Ограничивать Pos Y")]
    [Tooltip("Если включено, anchoredPosition.y не сможет выйти ниже Минимум Y или выше Максимум Y.")]
    [SerializeField] private bool _clampAnchoredY = true;

    [InspectorName("Минимум Y")]
    [Tooltip("Нижняя граница Pos Y. Значение меньше этого будет сразу возвращено к минимуму.")]
    [SerializeField] private float _minY = -100000f;

    [InspectorName("Максимум Y")]
    [Tooltip("Верхняя граница Pos Y. Значение больше этого будет сразу возвращено к максимуму.")]
    [SerializeField] private float _maxY = 100000f;

    [Header("Высота")]
    [InspectorName("Ограничивать высоту")]
    [Tooltip("Если включено, Height не сможет стать меньше Минимальной высоты или больше Максимальной высоты.")]
    [SerializeField] private bool _clampHeight = true;

    [InspectorName("Минимальная высота")]
    [Tooltip("Минимально разрешённая высота RectTransform.")]
    [SerializeField] private float _minHeight = 0f;

    [InspectorName("Максимальная высота")]
    [Tooltip("Максимально разрешённая высота RectTransform. Если меньше минимума, будет автоматически поднята до минимума.")]
    [SerializeField] private float _maxHeight = 100000f;

    [Header("LayoutElement")]
    [InspectorName("Писать LayoutElement")]
    [Tooltip("Если включено, скрипт будет записывать minHeight/preferredHeight/flexibleHeight в LayoutElement, чтобы LayoutGroup тоже видел лимиты.")]
    [SerializeField] private bool _writeLayoutElement = true;

    [InspectorName("Создать LayoutElement")]
    [Tooltip("Если LayoutElement отсутствует, создать его автоматически во время применения.")]
    [SerializeField] private bool _createLayoutElement = true;

    [InspectorName("Flexible Height = 0")]
    [Tooltip("Записывать flexibleHeight = 0, чтобы LayoutGroup не растягивал объект за пределы лимитов.")]
    [SerializeField] private bool _zeroFlexibleHeight = true;

    [Header("Когда применять")]
    [InspectorName("При включении")]
    [Tooltip("Применить ограничения в OnEnable/Start.")]
    [SerializeField] private bool _applyOnEnable = true;

    [InspectorName("При изменениях в инспекторе")]
    [Tooltip("Применять ограничения прямо в редакторе при изменении полей компонента.")]
    [SerializeField] private bool _applyOnValidate = true;

    [InspectorName("В редакторе")]
    [Tooltip("Разрешить скрипту менять RectTransform в edit mode. Если выключено, в редакторе работает только контекстное меню.")]
    [SerializeField] private bool _applyInEditMode = true;

    [InspectorName("Каждый кадр")]
    [Tooltip("Жёсткая защита в Update/LateUpdate. Полезно, если другие скрипты постоянно пытаются менять Pos Y или Height.")]
    [SerializeField] private bool _applyEveryFrame = true;

    [InspectorName("Перед рендером Canvas")]
    [Tooltip("Финальная проверка прямо перед отрисовкой Canvas. Помогает победить LayoutGroup, анимации и поздние изменения UI.")]
    [SerializeField] private bool _applyBeforeCanvasRender = true;

    [InspectorName("При изменении размеров")]
    [Tooltip("Применять ограничения, когда Unity сообщает об изменении размеров RectTransform.")]
    [SerializeField] private bool _applyOnDimensionsChange = true;

    [InspectorName("Перестраивать layout родителя")]
    [Tooltip("После ограничения высоты перестроить родительский layout, чтобы соседние элементы сразу встали корректно.")]
    [SerializeField] private bool _rebuildParentLayout = true;

    private bool _isApplying;
    private LayoutElement _layoutElement;

    public RectTransform Target => ResolveTarget();
    public float MinY => _minY;
    public float MaxY => _maxY;
    public float MinHeight => _minHeight;
    public float MaxHeight => _maxHeight;

    private void Awake()
    {
        CacheTarget();
    }

    private void OnEnable()
    {
        CacheTarget();
        Canvas.willRenderCanvases += HandleWillRenderCanvases;

        if (_applyOnEnable)
            ApplyNow();
    }

    private void Start()
    {
        if (_applyOnEnable)
            ApplyNow();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
    }

    private void Update()
    {
        if (_applyEveryFrame)
            ApplyNow();
    }

    private void LateUpdate()
    {
        if (_applyEveryFrame)
            ApplyNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_applyOnDimensionsChange)
            ApplyNow();
    }

    private void OnDidApplyAnimationProperties()
    {
        ApplyNow();
    }

    private void OnValidate()
    {
        NormalizeLimits();
        CacheTarget();

        if (_applyOnValidate)
            ApplyNow();
    }

    public void SetLayoutHorizontal()
    {
    }

    public void SetLayoutVertical()
    {
        ApplyNow();
    }

    [ContextMenu("Y Bounds/Применить сейчас")]
    public void ApplyNowFromContextMenu()
    {
        ApplyNow(true);
    }

    public void ApplyNow()
    {
        ApplyNow(false);
    }

    public void ApplyNow(bool forceInEditMode)
    {
        if (_isApplying)
            return;

        if (!Application.isPlaying && !_applyInEditMode && !forceInEditMode)
            return;

        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        NormalizeLimits();

        _isApplying = true;
        try
        {
            bool changed = false;

            if (_clampHeight)
                changed |= ClampHeight(target);

            if (_clampAnchoredY)
                changed |= ClampAnchoredY(target);

            if (_writeLayoutElement)
                ApplyLayoutElement(target);

            if (changed && _rebuildParentLayout && target.parent is RectTransform parent)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void SetYLimits(float minY, float maxY, bool applyImmediately = true)
    {
        _minY = minY;
        _maxY = maxY;
        NormalizeLimits();

        if (applyImmediately)
            ApplyNow(true);
    }

    public void SetHeightLimits(float minHeight, float maxHeight, bool applyImmediately = true)
    {
        _minHeight = minHeight;
        _maxHeight = maxHeight;
        NormalizeLimits();

        if (applyImmediately)
            ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Текущий Y как минимум")]
    private void CaptureCurrentYAsMin()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _minY = target.anchoredPosition.y;
        NormalizeLimits();
        ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Текущий Y как максимум")]
    private void CaptureCurrentYAsMax()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _maxY = target.anchoredPosition.y;
        NormalizeLimits();
        ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Текущая высота как минимум")]
    private void CaptureCurrentHeightAsMin()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _minHeight = Mathf.Max(0f, target.rect.height);
        NormalizeLimits();
        ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Текущая высота как максимум")]
    private void CaptureCurrentHeightAsMax()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _maxHeight = Mathf.Max(0f, target.rect.height);
        NormalizeLimits();
        ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Взять текущий Y как диапазон")]
    private void CaptureCurrentYAsLockedRange()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _minY = target.anchoredPosition.y;
        _maxY = _minY;
        ApplyNow(true);
    }

    [ContextMenu("Y Bounds/Взять текущую высоту как диапазон")]
    private void CaptureCurrentHeightAsLockedRange()
    {
        RectTransform target = ResolveTarget();
        if (target == null)
            return;

        _minHeight = Mathf.Max(0f, target.rect.height);
        _maxHeight = _minHeight;
        ApplyNow(true);
    }

    private void HandleWillRenderCanvases()
    {
        if (_applyBeforeCanvasRender)
            ApplyNow();
    }

    private bool ClampAnchoredY(RectTransform target)
    {
        Vector3 position = target.anchoredPosition3D;
        float clampedY = Mathf.Clamp(position.y, _minY, _maxY);
        if (Mathf.Abs(position.y - clampedY) <= Epsilon)
            return false;

        position.y = clampedY;
        target.anchoredPosition3D = position;
        return true;
    }

    private bool ClampHeight(RectTransform target)
    {
        float currentHeight = Mathf.Max(0f, target.rect.height);
        float clampedHeight = Mathf.Clamp(currentHeight, _minHeight, _maxHeight);
        if (Mathf.Abs(currentHeight - clampedHeight) <= Epsilon)
            return false;

        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, clampedHeight);
        return true;
    }

    private void ApplyLayoutElement(RectTransform target)
    {
        if (_layoutElement == null || _layoutElement.gameObject != target.gameObject)
            _layoutElement = target.GetComponent<LayoutElement>();

        if (_layoutElement == null && _createLayoutElement)
            _layoutElement = target.gameObject.AddComponent<LayoutElement>();

        if (_layoutElement == null)
            return;

        if (_clampHeight)
        {
            float height = Mathf.Clamp(Mathf.Max(0f, target.rect.height), _minHeight, _maxHeight);
            _layoutElement.minHeight = _minHeight;
            _layoutElement.preferredHeight = height;
            if (_zeroFlexibleHeight)
                _layoutElement.flexibleHeight = 0f;
        }
    }

    private RectTransform ResolveTarget()
    {
        CacheTarget();
        return _target;
    }

    private void CacheTarget()
    {
        if (_target == null)
            _target = transform as RectTransform;
    }

    private void NormalizeLimits()
    {
        if (_maxY < _minY)
            _maxY = _minY;

        _minHeight = Mathf.Max(0f, _minHeight);
        _maxHeight = Mathf.Max(0f, _maxHeight);
        if (_maxHeight < _minHeight)
            _maxHeight = _minHeight;
    }
}
