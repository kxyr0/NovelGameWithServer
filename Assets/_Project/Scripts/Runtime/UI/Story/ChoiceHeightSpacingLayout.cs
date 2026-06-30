using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ChoiceHeightSpacingLayout : MonoBehaviour
{
    private const float SizeEpsilon = 0.1f;

    [Header("Источник")]
    [Tooltip("Контейнер, внутри которого лежат варианты выбора. Если поле пустое, используется RectTransform объекта с этим скриптом.")]
    [SerializeField] private RectTransform _container;

    [Tooltip("Если включено, раскладываются только прямые дети контейнера. Для ChoicePanel обычно нужно оставить включённым.")]
    [SerializeField] private bool _directChildrenOnly = true;

    [Tooltip("Учитывать неактивные дочерние объекты при расчёте высоты. Обычно выключено, чтобы скрытые варианты не занимали место.")]
    [SerializeField] private bool _includeInactiveChildren;

    [Header("Отступы")]
    [Tooltip("Отступ от верхнего края ChoicePanel до первой кнопки.")]
    [SerializeField] private float _topPadding;

    [Tooltip("Отступ после последней кнопки. Используется при автоматической высоте ChoicePanel.")]
    [SerializeField] private float _bottomPadding;

    [Tooltip("Базовый промежуток между кнопками выбора.")]
    [SerializeField] private float _spacing = 18f;

    [Tooltip("Если вариантов больше порога, использовать компактный промежуток.")]
    [SerializeField] private bool _useCompactSpacing = true;

    [Tooltip("Сколько вариантов считается обычным количеством. Если вариантов больше, включается компактный промежуток.")]
    [SerializeField, Min(0)] private int _compactThreshold = 3;

    [Tooltip("Промежуток между кнопками, когда вариантов много.")]
    [SerializeField] private float _compactSpacing = 8f;

    [Tooltip("Дополнительный промежуток как доля высоты предыдущей кнопки. 0 = только обычный spacing, 0.1 = плюс 10% высоты кнопки.")]
    [SerializeField] private float _extraSpacingFromPreviousHeight;

    [Header("Позиция кнопок")]
    [Tooltip("Ставить якоря и pivot каждой кнопки в верхний центр. Это делает расчёт стабильным и не зависит от старых настроек prefab.")]
    [SerializeField] private bool _forceChildTopCenterAnchors = true;

    [Tooltip("Центрировать кнопки по X внутри ChoicePanel.")]
    [SerializeField] private bool _centerChildrenHorizontally = true;

    [Tooltip("Ручной X для кнопок, если центрирование выключено.")]
    [SerializeField] private float _childAnchoredX;

    [Tooltip("Записывать измеренную высоту обратно в RectTransform кнопки. Нужно, если root prefab имеет высоту 0, а реальная высота находится внутри Container.")]
    [SerializeField] private bool _writeMeasuredHeightToChild = true;

    [Tooltip("Если ширина кнопки меньше этого значения, скрипт берёт ширину по визуальным дочерним объектам.")]
    [SerializeField] private float _minReadableChildWidth = 1f;

    [Header("Высота ChoicePanel")]
    [Tooltip("Автоматически менять высоту ChoicePanel под сумму высот кнопок и отступов.")]
    [SerializeField] private bool _resizeContainerHeight = true;

    [Tooltip("Минимальная высота ChoicePanel после расчёта.")]
    [SerializeField] private float _minContainerHeight;

    [Tooltip("Сохранять верхнюю границу ChoicePanel на месте при изменении высоты. Обычно включено: выборы будут расти вниз.")]
    [SerializeField] private bool _keepContainerTopInPlace = true;

    [Tooltip("Дополнительно опускать весь ChoicePanel, когда общий блок стал выше указанного порога.")]
    [SerializeField] private bool _pushContainerDownWhenTall;

    [Tooltip("Высота, после которой начинает работать опускание ChoicePanel. 0 = использовать исходную высоту контейнера.")]
    [SerializeField] private float _pushDownAfterHeight;

    [Tooltip("Сила опускания ChoicePanel от лишней высоты. 1 = опустить на всю лишнюю высоту, 0.5 = на половину.")]
    [SerializeField] private float _pushDownMultiplier = 0.5f;

    [Tooltip("Максимальное опускание ChoicePanel. 0 = без ограничения.")]
    [SerializeField] private float _maxPushDown;

    [Header("Обновление")]
    [Tooltip("Обновлять раскладку при включении ChoicePanel.")]
    [SerializeField] private bool _refreshOnEnable = true;

    [Tooltip("Обновлять раскладку в LateUpdate. Полезно, если текст или цена меняются уже после создания кнопки.")]
    [SerializeField] private bool _refreshInLateUpdate = true;

    [Tooltip("Обновлять раскладку перед рендером Canvas. Это ловит поздние изменения TMP и ContentSizeFitter.")]
    [SerializeField] private bool _refreshBeforeCanvasRender = true;

    [Tooltip("Принудительно обновлять Canvas перед измерением высоты кнопок.")]
    [SerializeField] private bool _forceCanvasUpdateBeforeMeasure = true;

    [Tooltip("Перед измерением вызывать ButtonTextAutoSize и RectTransformValueSync на дочерних кнопках.")]
    [SerializeField] private bool _refreshChildHelpers = true;

    [Header("Конфликтующие layout-компоненты")]
    [Tooltip("Отключать VerticalLayoutGroup на ChoicePanel во время игры, чтобы он не перетирал ручную раскладку.")]
    [SerializeField] private bool _disableVerticalLayoutGroupAtRuntime = true;

    [Tooltip("Если VerticalLayoutGroup был отключён этим скриптом, вернуть его состояние при выключении компонента.")]
    [SerializeField] private bool _restoreVerticalLayoutGroupOnDisable = true;

    private readonly List<RectTransform> _children = new List<RectTransform>();
    private RectTransform _rectTransform;
    private VerticalLayoutGroup _verticalLayoutGroup;
    private bool _capturedBase;
    private Vector2 _baseAnchoredPosition;
    private float _baseHeight;
    private bool _capturedVerticalLayoutGroupState;
    private bool _verticalLayoutGroupWasEnabled;
    private bool _refreshing;
    private int _lastVisibleChoiceCount = -1;

    private void Awake()
    {
        CacheReferences();
        CaptureBaseLayout();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureBaseLayout();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;

        if (_refreshOnEnable)
            RefreshNow();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        RestoreVerticalLayoutGroupIfNeeded();
    }

    private void OnValidate()
    {
        _compactThreshold = Mathf.Max(0, _compactThreshold);
        _minReadableChildWidth = Mathf.Max(0f, _minReadableChildWidth);
        _minContainerHeight = Mathf.Max(0f, _minContainerHeight);
        _pushDownAfterHeight = Mathf.Max(0f, _pushDownAfterHeight);
        _pushDownMultiplier = Mathf.Max(0f, _pushDownMultiplier);
        _maxPushDown = Mathf.Max(0f, _maxPushDown);

        CacheReferences();
        if (isActiveAndEnabled)
            RefreshNow();
    }

    private void LateUpdate()
    {
        if (_refreshInLateUpdate)
            RefreshNow(_lastVisibleChoiceCount);
    }

    private void OnTransformChildrenChanged()
    {
        if (isActiveAndEnabled)
            RefreshNow();
    }

    [ContextMenu("Обновить раскладку")]
    public void RefreshNowFromContextMenu()
    {
        RefreshNow();
    }

    public void RefreshNow(int visibleChoiceCount = -1)
    {
        if (_refreshing)
            return;

        CacheReferences();
        RectTransform container = ResolveContainer();
        if (container == null)
            return;

        _lastVisibleChoiceCount = visibleChoiceCount;
        _refreshing = true;

        try
        {
            DisableVerticalLayoutGroupIfNeeded();

            if (_forceCanvasUpdateBeforeMeasure)
                Canvas.ForceUpdateCanvases();

            CollectChildren(container);
            if (_refreshChildHelpers)
                RefreshChildHelpers();

            if (_forceCanvasUpdateBeforeMeasure)
                Canvas.ForceUpdateCanvases();

            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            ApplyManualLayout(container, visibleChoiceCount >= 0 ? visibleChoiceCount : _children.Count);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void HandleWillRenderCanvases()
    {
        if (_refreshBeforeCanvasRender && isActiveAndEnabled)
            RefreshNow(_lastVisibleChoiceCount);
    }

    private void ApplyManualLayout(RectTransform container, int visibleChoiceCount)
    {
        float y = Mathf.Max(0f, _topPadding);
        float spacing = ResolveSpacing(visibleChoiceCount);
        float totalWidth = 0f;

        for (int i = 0; i < _children.Count; i++)
        {
            RectTransform child = _children[i];
            if (child == null)
                continue;

            Vector2 measuredSize = MeasureChild(child);
            float width = Mathf.Max(measuredSize.x, child.rect.width);
            float height = Mathf.Max(1f, measuredSize.y);
            totalWidth = Mathf.Max(totalWidth, width);

            if (_forceChildTopCenterAnchors)
            {
                child.anchorMin = new Vector2(0.5f, 1f);
                child.anchorMax = new Vector2(0.5f, 1f);
                child.pivot = new Vector2(0.5f, 1f);
            }

            if (_writeMeasuredHeightToChild)
            {
                if (width > _minReadableChildWidth)
                    SetSizeIfChanged(child, RectTransform.Axis.Horizontal, width);

                SetSizeIfChanged(child, RectTransform.Axis.Vertical, height);
            }

            float x = _centerChildrenHorizontally ? 0f : _childAnchoredX;
            Vector2 targetPosition = new Vector2(x, -y);
            if ((child.anchoredPosition - targetPosition).sqrMagnitude > SizeEpsilon * SizeEpsilon)
                child.anchoredPosition = targetPosition;

            if (i < _children.Count - 1)
                y += height + ResolveGapAfterChild(spacing, height);
            else
                y += height;
        }

        float totalHeight = Mathf.Max(_minContainerHeight, y + Mathf.Max(0f, _bottomPadding));
        if (_resizeContainerHeight)
            ResizeContainer(container, totalHeight);

        ApplyContainerDownOffset(container, totalHeight);
        LayoutRebuilder.MarkLayoutForRebuild(container);
    }

    private float ResolveSpacing(int visibleChoiceCount)
    {
        if (_useCompactSpacing && visibleChoiceCount > Mathf.Max(0, _compactThreshold))
            return _compactSpacing;

        return _spacing;
    }

    private float ResolveGapAfterChild(float spacing, float childHeight)
    {
        return spacing + Mathf.Max(0f, _extraSpacingFromPreviousHeight) * Mathf.Max(0f, childHeight);
    }

    private Vector2 MeasureChild(RectTransform child)
    {
        if (child == null)
            return Vector2.zero;

        float preferredWidth = LayoutUtility.GetPreferredWidth(child);
        float preferredHeight = LayoutUtility.GetPreferredHeight(child);
        Vector2 rectSize = child.rect.size;
        Vector2 boundsSize = MeasureChildBounds(child);

        return new Vector2(
            Mathf.Max(preferredWidth, rectSize.x, boundsSize.x),
            Mathf.Max(preferredHeight, rectSize.y, boundsSize.y));
    }

    private Vector2 MeasureChildBounds(RectTransform child)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(child, child);
        Vector3 size = bounds.size;
        return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
    }

    private void ResizeContainer(RectTransform container, float targetHeight)
    {
        if (container == null || Mathf.Abs(container.rect.height - targetHeight) <= SizeEpsilon)
            return;

        Vector3 topBefore = _keepContainerTopInPlace ? GetWorldTopCenter(container) : Vector3.zero;
        container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, targetHeight));

        if (_keepContainerTopInPlace)
            container.position += topBefore - GetWorldTopCenter(container);
    }

    private void ApplyContainerDownOffset(RectTransform container, float totalHeight)
    {
        if (!_pushContainerDownWhenTall || container == null)
            return;

        CaptureBaseLayout();

        float threshold = _pushDownAfterHeight > 0f ? _pushDownAfterHeight : _baseHeight;
        float extraHeight = Mathf.Max(0f, totalHeight - threshold);
        float pushDown = extraHeight * _pushDownMultiplier;
        if (_maxPushDown > 0f)
            pushDown = Mathf.Min(pushDown, _maxPushDown);

        Vector2 targetPosition = _baseAnchoredPosition + new Vector2(0f, -pushDown);
        if ((container.anchoredPosition - targetPosition).sqrMagnitude > SizeEpsilon * SizeEpsilon)
            container.anchoredPosition = targetPosition;
    }

    private void RefreshChildHelpers()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            RectTransform child = _children[i];
            if (child == null)
                continue;

            ButtonTextAutoSize[] autoSizes = child.GetComponentsInChildren<ButtonTextAutoSize>(true);
            for (int j = 0; j < autoSizes.Length; j++)
                autoSizes[j]?.RefreshNow();

            RectTransformValueSync[] valueSyncs = child.GetComponentsInChildren<RectTransformValueSync>(true);
            for (int j = 0; j < valueSyncs.Length; j++)
                valueSyncs[j]?.Apply();
        }
    }

    private void CollectChildren(RectTransform container)
    {
        _children.Clear();

        if (container == null)
            return;

        if (_directChildrenOnly)
        {
            for (int i = 0; i < container.childCount; i++)
                AddChild(container.GetChild(i) as RectTransform);

            return;
        }

        RectTransform[] rects = container.GetComponentsInChildren<RectTransform>(_includeInactiveChildren);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform child = rects[i];
            if (child != null && child != container)
                AddChild(child);
        }
    }

    private void AddChild(RectTransform child)
    {
        if (child == null)
            return;

        if (!_includeInactiveChildren && !child.gameObject.activeInHierarchy)
            return;

        _children.Add(child);
    }

    private void DisableVerticalLayoutGroupIfNeeded()
    {
        if (!_disableVerticalLayoutGroupAtRuntime || !Application.isPlaying)
            return;

        if (_verticalLayoutGroup == null)
            return;

        if (!_capturedVerticalLayoutGroupState)
        {
            _verticalLayoutGroupWasEnabled = _verticalLayoutGroup.enabled;
            _capturedVerticalLayoutGroupState = true;
        }

        if (_verticalLayoutGroup.enabled)
            _verticalLayoutGroup.enabled = false;
    }

    private void RestoreVerticalLayoutGroupIfNeeded()
    {
        if (!_restoreVerticalLayoutGroupOnDisable || !_capturedVerticalLayoutGroupState || _verticalLayoutGroup == null)
            return;

        _verticalLayoutGroup.enabled = _verticalLayoutGroupWasEnabled;
        _capturedVerticalLayoutGroupState = false;
    }

    private RectTransform ResolveContainer()
    {
        if (_container != null)
            return _container;

        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        return _rectTransform;
    }

    private void CacheReferences()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_container == null)
            _container = _rectTransform;

        if (_verticalLayoutGroup == null)
            _verticalLayoutGroup = ResolveContainer() != null ? ResolveContainer().GetComponent<VerticalLayoutGroup>() : null;
    }

    private void CaptureBaseLayout()
    {
        if (_capturedBase)
            return;

        RectTransform container = ResolveContainer();
        if (container == null)
            return;

        _baseAnchoredPosition = container.anchoredPosition;
        _baseHeight = Mathf.Max(1f, container.rect.height);
        _capturedBase = true;
    }

    private static Vector3 GetWorldTopCenter(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return (corners[1] + corners[2]) * 0.5f;
    }

    private static void SetSizeIfChanged(RectTransform rect, RectTransform.Axis axis, float value)
    {
        if (rect == null)
            return;

        float current = axis == RectTransform.Axis.Horizontal ? rect.rect.width : rect.rect.height;
        if (Mathf.Abs(current - value) <= SizeEpsilon)
            return;

        rect.SetSizeWithCurrentAnchors(axis, Mathf.Max(1f, value));
    }
}
