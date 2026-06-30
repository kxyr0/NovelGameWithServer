using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct StoryCarouselVisibleCard
{
    public GameData Data;
    public int SourceIndex;
    public int SlotOffset;

    public StoryCarouselVisibleCard(GameData data, int sourceIndex, int slotOffset)
    {
        Data = data;
        SourceIndex = sourceIndex;
        SlotOffset = slotOffset;
    }
}

public struct StoryCarouselDrawTarget
{
    public RectTransform Root;
    public int SlotOffset;
    public bool Selected;
    public int SourceIndex;
    public int OriginalOrder;

    public StoryCarouselDrawTarget(RectTransform root, int slotOffset, bool selected, int sourceIndex, int originalOrder)
    {
        Root = root;
        SlotOffset = slotOffset;
        Selected = selected;
        SourceIndex = sourceIndex;
        OriginalOrder = originalOrder;
    }
}

[Serializable]
public sealed class StoryCardCarouselSlot
{
    [SerializeField]
    [InspectorName("Offset")]
    [Tooltip("Позиция относительно выбранной истории: -1 слева, 0 выбранная, 1 справа.")]
    private int _offset;

    [SerializeField]
    [InspectorName("Позиция")]
    [Tooltip("Anchored Position карточки в этом слоте.")]
    private Vector2 _anchoredPosition;

    [SerializeField]
    [InspectorName("Rotation Z")]
    [Tooltip("Поворот карточки в этом слоте. Для выбранного слота код может принудительно ставить 0.")]
    private float _rotationZ;

    [SerializeField]
    [InspectorName("Переопределить размер")]
    [Tooltip("Если включено, слот задает Width/Height root-карточки.")]
    private bool _overrideSize;

    [SerializeField]
    [InspectorName("Размер")]
    [Tooltip("Width/Height root-карточки в этом слоте.")]
    private Vector2 _size = new Vector2(1076.663f, 1716.369f);

    [SerializeField]
    [InspectorName("Scale")]
    [Tooltip("Масштаб карточки в этом слоте.")]
    private Vector2 _scale = Vector2.one;

    [SerializeField]
    [InspectorName("Scale Z")]
    [Tooltip("Z scale карточки в этом слоте. Для UI почти не влияет на вид, но нужен для точного совпадения RectTransform.")]
    private float _scaleZ = 1f;

    public int Offset => _offset;
    public Vector2 AnchoredPosition => _anchoredPosition;
    public float RotationZ => _rotationZ;
    public bool OverrideSize => _overrideSize;
    public Vector2 Size => _size;
    public Vector3 Scale => new Vector3(
        Mathf.Approximately(_scale.x, 0f) ? 1f : _scale.x,
        Mathf.Approximately(_scale.y, 0f) ? 1f : _scale.y,
        Mathf.Approximately(_scaleZ, 0f) ? 1f : _scaleZ);

    public StoryCardCarouselSlot()
    {
    }

    public StoryCardCarouselSlot(int offset, Vector2 anchoredPosition, float rotationZ, Vector2 scale)
        : this(offset, anchoredPosition, rotationZ, scale, 1f, false, Vector2.zero)
    {
    }

    public StoryCardCarouselSlot(
        int offset,
        Vector2 anchoredPosition,
        float rotationZ,
        Vector2 scale,
        float scaleZ,
        bool overrideSize,
        Vector2 size)
    {
        _offset = offset;
        _anchoredPosition = anchoredPosition;
        _rotationZ = rotationZ;
        _scale = scale == Vector2.zero ? Vector2.one : scale;
        _scaleZ = Mathf.Approximately(scaleZ, 0f) ? 1f : scaleZ;
        _overrideSize = overrideSize;
        _size = size == Vector2.zero ? new Vector2(1076.663f, 1716.369f) : size;
    }

    public void Validate()
    {
        _scale = _scale == Vector2.zero ? Vector2.one : _scale;
        _scaleZ = Mathf.Approximately(_scaleZ, 0f) ? 1f : _scaleZ;
        _size = new Vector2(Mathf.Max(0f, _size.x), Mathf.Max(0f, _size.y));
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story Card Carousel Layout")]
public sealed class StoryCardCarouselLayout : MonoBehaviour
{
    [Header("Карусель")]
    [SerializeField]
    [InspectorName("Включить карусель")]
    [Tooltip("Если выключено, MenuController покажет все истории без оконного режима.")]
    private bool _carouselEnabled = true;

    [SerializeField]
    [InspectorName("Зацикливать")]
    [Tooltip("После последней истории переходить к первой, и наоборот.")]
    private bool _wrap = true;

    [SerializeField]
    [InspectorName("Отключать Vertical Layout")]
    [Tooltip("Отключает LayoutGroup и ContentSizeFitter на Content, чтобы они не перетирали позиции карточек.")]
    private bool _disableParentLayoutComponents = true;

    [SerializeField]
    [InspectorName("Выбранная поверх")]
    [Tooltip("Поднимать выбранную карточку поверх боковых.")]
    private bool _selectedAsLastSibling = true;

    [SerializeField]
    [InspectorName("Сортировать глубину")]
    [Tooltip("После раскладки выставлять sibling order: дальние карточки назад, выбранную поверх. Важно для 4+ историй.")]
    private bool _sortSiblingOrder = true;

    [Header("Слоты")]
    [SerializeField]
    [InspectorName("Слоты карточек")]
    [Tooltip("Каждый слот описывает позицию и поворот карточки относительно выбранной истории.")]
    private StoryCardCarouselSlot[] _slots =
    {
        new StoryCardCarouselSlot(-1, new Vector2(-260f, 220.5f), 10f, new Vector2(0.4f, 0.4f), 0.6666667f, true, new Vector2(1076.663f, 1716.369f)),
        new StoryCardCarouselSlot(0, Vector2.zero, 0f, new Vector2(0.6f, 0.6f), 1f, false, Vector2.zero),
        new StoryCardCarouselSlot(1, new Vector2(260f, 220.5f), -10f, new Vector2(0.4f, 0.4f), 0.6666667f, true, new Vector2(1076.663f, 1716.369f))
    };

    [Header("Fallback")]
    [SerializeField]
    [InspectorName("Fallback spacing")]
    [Tooltip("Используется, если для offset нет явного слота.")]
    private float _fallbackHorizontalSpacing = 260f;

    [SerializeField]
    [InspectorName("Fallback rotation")]
    [Tooltip("Используется, если для offset нет явного слота.")]
    private float _fallbackRotationStep = 10f;

    [Header("Анимация")]
    [SerializeField]
    [InspectorName("Анимировать")]
    [Tooltip("Плавно двигать, масштабировать и поворачивать карточки при смене выбранной истории.")]
    private bool _animate = true;

    [SerializeField, Min(0f)]
    [InspectorName("Длительность")]
    [Tooltip("Длительность анимации раскладки карточек.")]
    private float _duration = 0.28f;

    [SerializeField]
    [InspectorName("Ease")]
    [Tooltip("Кривая анимации раскладки.")]
    private Ease _ease = Ease.OutCubic;

    [SerializeField]
    [InspectorName("Unscaled Time")]
    [Tooltip("Использовать unscaled time для UI-анимации.")]
    private bool _useUnscaledTime = true;

    [SerializeField]
    [InspectorName("Selected rotation = 0")]
    [Tooltip("Когда история выбрана, ее поворот всегда анимируется к 0, даже если слот настроен иначе.")]
    private bool _forceSelectedRotationToZero = true;

    public bool CarouselEnabled => _carouselEnabled;
    public bool Wrap => _wrap;
    public int VisibleSlotCount => _carouselEnabled ? Mathf.Max(1, GetSlotCount()) : int.MaxValue;

    private void OnValidate()
    {
        _duration = Mathf.Max(0f, _duration);
        _fallbackHorizontalSpacing = Mathf.Max(0f, _fallbackHorizontalSpacing);
        _fallbackRotationStep = Mathf.Max(0f, _fallbackRotationStep);

        if (_slots == null || _slots.Length == 0)
        {
            _slots = new[]
            {
                new StoryCardCarouselSlot(-1, new Vector2(-260f, 220.5f), 10f, new Vector2(0.4f, 0.4f), 0.6666667f, true, new Vector2(1076.663f, 1716.369f)),
                new StoryCardCarouselSlot(0, Vector2.zero, 0f, new Vector2(0.6f, 0.6f), 1f, false, Vector2.zero),
                new StoryCardCarouselSlot(1, new Vector2(260f, 220.5f), -10f, new Vector2(0.4f, 0.4f), 0.6666667f, true, new Vector2(1076.663f, 1716.369f))
            };
        }

        for (int i = 0; i < _slots.Length; i++)
            _slots[i]?.Validate();
    }

    public void PrepareParent(Transform parent)
    {
        if (!_disableParentLayoutComponents || parent == null)
            return;

        LayoutGroup[] layoutGroups = parent.GetComponents<LayoutGroup>();
        for (int i = 0; i < layoutGroups.Length; i++)
        {
            if (layoutGroups[i] != null)
                layoutGroups[i].enabled = false;
        }

        ContentSizeFitter fitter = parent.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

    public List<StoryCarouselVisibleCard> BuildVisibleCards(IReadOnlyList<GameData> games, int selectedIndex)
    {
        var result = new List<StoryCarouselVisibleCard>();
        if (games == null || games.Count == 0)
            return result;

        int count = games.Count;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);

        if (!_carouselEnabled)
        {
            for (int i = 0; i < count; i++)
                result.Add(new StoryCarouselVisibleCard(games[i], i, i - selectedIndex));

            return result;
        }

        var usedIndexes = new HashSet<int>();
        usedIndexes.Add(selectedIndex);
        result.Add(new StoryCarouselVisibleCard(games[selectedIndex], selectedIndex, 0));

        List<StoryCardCarouselSlot> slots = GetSlotsSortedByDistance();
        for (int i = 0; i < slots.Count; i++)
        {
            int offset = slots[i].Offset;
            if (offset == 0)
                continue;

            int sourceIndex = selectedIndex + offset;

            if (_wrap)
                sourceIndex = WrapIndex(sourceIndex, count);
            else if (sourceIndex < 0 || sourceIndex >= count)
                continue;

            if (!usedIndexes.Add(sourceIndex))
                continue;

            result.Add(new StoryCarouselVisibleCard(games[sourceIndex], sourceIndex, offset));
        }

        return result;
    }

    public void ApplySiblingOrder(IReadOnlyList<StoryCarouselDrawTarget> targets)
    {
        if (!_sortSiblingOrder || targets == null || targets.Count == 0)
            return;

        var sorted = new List<StoryCarouselDrawTarget>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            StoryCarouselDrawTarget target = targets[i];
            if (target.Root != null)
                sorted.Add(target);
        }

        sorted.Sort(CompareDrawTargets);

        for (int i = 0; i < sorted.Count; i++)
        {
            RectTransform root = sorted[i].Root;
            if (root != null)
                root.SetSiblingIndex(i);
        }
    }

    public void ApplyToCard(RectTransform cardRoot, int slotOffset, bool selected, bool instant, GameData data = null)
    {
        if (cardRoot == null)
            return;

        StoryCardCarouselSlot slot = FindSlot(slotOffset);
        Vector2 anchoredPosition = slot != null
            ? slot.AnchoredPosition
            : new Vector2(slotOffset * _fallbackHorizontalSpacing, 0f);

        float rotationZ = slot != null
            ? slot.RotationZ
            : -slotOffset * _fallbackRotationStep;

        Vector3 scale = slot != null ? slot.Scale : Vector3.one;
        bool hasSize = slot != null && slot.OverrideSize;
        Vector2 size = hasSize ? slot.Size : Vector2.zero;

        GameMenuCardOverrideSettings overrides = data != null ? data.MenuCardOverrides : null;
        if (overrides != null)
        {
            if (overrides.OverrideRootPositionOffset)
                anchoredPosition += overrides.RootPositionOffset;

            if (overrides.OverrideRootRotationOffset)
                rotationZ += overrides.RootRotationOffsetZ;

            if (overrides.OverrideRootScaleMultiplier)
                scale = Vector3.Scale(scale, overrides.RootScaleMultiplier);

            if (overrides.OverrideRootSize)
            {
                size = overrides.RootSize;
                hasSize = true;
            }
        }

        if (selected && _forceSelectedRotationToZero)
            rotationZ = 0f;

        Vector3 rotation = new Vector3(0f, 0f, rotationZ);

        cardRoot.DOKill(false);
        bool shouldAnimate = _animate && !instant && _duration > 0f && Application.isPlaying;
        if (!shouldAnimate)
        {
            if (hasSize)
                SetRectSize(cardRoot, size);

            cardRoot.anchoredPosition = anchoredPosition;
            cardRoot.localEulerAngles = rotation;
            cardRoot.localScale = scale;
        }
        else
        {
            if (hasSize)
                cardRoot.DOSizeDelta(size, _duration).SetEase(_ease).SetUpdate(_useUnscaledTime);

            cardRoot.DOAnchorPos(anchoredPosition, _duration).SetEase(_ease).SetUpdate(_useUnscaledTime);
            cardRoot.DOLocalRotate(rotation, _duration).SetEase(_ease).SetUpdate(_useUnscaledTime);
            cardRoot.DOScale(scale, _duration).SetEase(_ease).SetUpdate(_useUnscaledTime);
        }

        if (selected && _selectedAsLastSibling)
            cardRoot.SetAsLastSibling();
    }

    private static void SetRectSize(RectTransform rectTransform, Vector2 size)
    {
        if (rectTransform == null)
            return;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, size.x));
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, size.y));
    }

    private StoryCardCarouselSlot FindSlot(int offset)
    {
        StoryCardCarouselSlot[] slots = GetSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            StoryCardCarouselSlot slot = slots[i];
            if (slot != null && slot.Offset == offset)
                return slot;
        }

        return null;
    }

    private StoryCardCarouselSlot[] GetSlots()
    {
        if (_slots == null || _slots.Length == 0)
            OnValidate();

        return _slots;
    }

    private List<StoryCardCarouselSlot> GetSlotsSortedByDistance()
    {
        StoryCardCarouselSlot[] slots = GetSlots();
        var result = new List<StoryCardCarouselSlot>();
        if (slots == null)
            return result;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                result.Add(slots[i]);
        }

        result.Sort(CompareSlotsForVisibility);
        return result;
    }

    private static int CompareSlotsForVisibility(StoryCardCarouselSlot a, StoryCardCarouselSlot b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int zeroCompare = IsZeroOffset(a.Offset).CompareTo(IsZeroOffset(b.Offset));
        if (zeroCompare != 0)
            return zeroCompare;

        int distanceCompare = Mathf.Abs(a.Offset).CompareTo(Mathf.Abs(b.Offset));
        if (distanceCompare != 0)
            return distanceCompare;

        return a.Offset.CompareTo(b.Offset);
    }

    private int CompareDrawTargets(StoryCarouselDrawTarget a, StoryCarouselDrawTarget b)
    {
        int orderCompare = ResolveDrawOrder(a).CompareTo(ResolveDrawOrder(b));
        if (orderCompare != 0)
            return orderCompare;

        int sourceCompare = a.SourceIndex.CompareTo(b.SourceIndex);
        return sourceCompare != 0 ? sourceCompare : a.OriginalOrder.CompareTo(b.OriginalOrder);
    }

    private int ResolveDrawOrder(StoryCarouselDrawTarget target)
    {
        if (_selectedAsLastSibling && target.Selected)
            return 100000;

        int distance = Mathf.Abs(target.SlotOffset);
        int sideOrder = target.SlotOffset < 0 ? 0 : target.SlotOffset > 0 ? 1 : 2;
        return -distance * 100 + sideOrder;
    }

    private static int IsZeroOffset(int offset)
    {
        return offset == 0 ? 0 : 1;
    }

    private int GetSlotCount()
    {
        StoryCardCarouselSlot[] slots = GetSlots();
        return slots != null ? slots.Length : 0;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        index %= count;
        return index < 0 ? index + count : index;
    }
}
