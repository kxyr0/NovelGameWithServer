using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PriceIconPreferredWidthSpacing : MonoBehaviour
{
    public enum IconSide
    {
        RightOfText,
        LeftOfText
    }

    [Header("Ссылки")]
    [Tooltip("TMP_Text с ценой. Ширина берется из preferredWidth.")]
    [SerializeField] private TMP_Text _costText;

    [Tooltip("RectTransform иконки сердца или валюты, которую нужно поставить рядом с ценой.")]
    [SerializeField] private RectTransform _iconRect;

    [Header("Отступ")]
    [Tooltip("С какой стороны от текста ставить иконку.")]
    [SerializeField] private IconSide _side = IconSide.RightOfText;

    [Tooltip("Расстояние между видимой шириной текста цены и иконкой.")]
    [SerializeField] private float _spacing = 8f;

    [Tooltip("Вертикальная поправка иконки относительно центра RectTransform текста.")]
    [SerializeField] private float _verticalOffset;

    [Header("Поведение")]
    [Tooltip("Если включено, скрипт ставит anchors и pivot иконки в центр, чтобы anchoredPosition работал предсказуемо.")]
    [SerializeField] private bool _driveIconAnchors = true;

    [Tooltip("Скрывать иконку, если цена пустая.")]
    [SerializeField] private bool _hideIconWhenTextEmpty;

    [Tooltip("Обновлять позицию перед рендером Canvas.")]
    [SerializeField] private bool _refreshBeforeCanvasRender = true;

    [Tooltip("Обновлять позицию в LateUpdate, если текст изменился.")]
    [SerializeField] private bool _refreshInLateUpdate = true;

    bool _dirty = true;
    bool _subscribed;

    public TMP_Text CostText => _costText;
    public RectTransform IconRect => _iconRect;

    public void SetSpacing(float spacing)
    {
        _spacing = spacing;
        MarkDirty();
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        TryAutoWire();

        if (_costText == null || _iconRect == null)
            return;

        bool hasText = !string.IsNullOrWhiteSpace(_costText.text);
        if (_hideIconWhenTextEmpty && !hasText)
        {
            SetIconVisible(false);
            _dirty = false;
            return;
        }

        SetIconVisible(true);

        RectTransform textRect = _costText.rectTransform;
        if (textRect == null)
            return;

        if (_driveIconAnchors)
        {
            _iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            _iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            _iconRect.pivot = new Vector2(0.5f, 0.5f);
        }

        float preferredWidth = ResolvePreferredWidth();
        float rectWidth = Mathf.Max(0f, textRect.rect.width);
        float visualLeft = ResolveTextVisualLeft(rectWidth, preferredWidth);
        float visualRight = visualLeft + preferredWidth;

        float iconWidth = Mathf.Max(0f, _iconRect.rect.width);
        float targetX = _side == IconSide.RightOfText
            ? visualRight + _spacing + iconWidth * _iconRect.pivot.x
            : visualLeft - _spacing - iconWidth * (1f - _iconRect.pivot.x);

        float targetY = (0.5f - textRect.pivot.y) * textRect.rect.height + _verticalOffset;
        Vector3 world = textRect.TransformPoint(new Vector3(targetX, targetY, 0f));
        Vector3 local = _iconRect.parent != null
            ? _iconRect.parent.InverseTransformPoint(world)
            : world;

        _iconRect.anchoredPosition = new Vector2(local.x, local.y);
        _dirty = false;
    }

    void Reset()
    {
        TryAutoWire();
        MarkDirty();
    }

    void OnEnable()
    {
        TryAutoWire();
        Subscribe();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
        MarkDirty();
        RefreshNow();
    }

    void OnDisable()
    {
        Unsubscribe();
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
    }

    void OnValidate()
    {
        TryAutoWire();
        MarkDirty();
    }

    void LateUpdate()
    {
        if (_refreshInLateUpdate && _dirty)
            RefreshNow();
    }

    void HandleWillRenderCanvases()
    {
        if (_refreshBeforeCanvasRender && _dirty && isActiveAndEnabled)
            RefreshNow();
    }

    void HandleTextChanged(Object changedObject)
    {
        if (changedObject == _costText)
            MarkDirty();
    }

    void Subscribe()
    {
        if (_subscribed)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTextChanged);
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed)
            return;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTextChanged);
        _subscribed = false;
    }

    void TryAutoWire()
    {
        if (_costText == null)
            _costText = FindCostText();

        if (_iconRect == null)
            _iconRect = FindIconRect();
    }

    TMP_Text FindCostText()
    {
        TMP_Text ownText = GetComponent<TMP_Text>();
        if (ownText != null)
            return ownText;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name.IndexOf("Cost", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return text;
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    RectTransform FindIconRect()
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || (_costText != null && rect == _costText.rectTransform))
                continue;

            string name = rect.name;
            if (name.IndexOf("Heart", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Price", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Currency", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return rect;
            }
        }

        return null;
    }

    float ResolvePreferredWidth()
    {
        _costText.ForceMeshUpdate(true, true);
        Vector2 preferred = _costText.GetPreferredValues(_costText.text ?? "", Mathf.Infinity, Mathf.Infinity);
        return Mathf.Max(0f, preferred.x);
    }

    float ResolveTextVisualLeft(float rectWidth, float preferredWidth)
    {
        float rectLeft = -_costText.rectTransform.pivot.x * rectWidth;
        float freeWidth = rectWidth - preferredWidth;
        int alignment = (int)_costText.alignment;

        if ((alignment & (int)TextAlignmentOptions.Right) != 0)
            return rectLeft + freeWidth;

        if ((alignment & (int)TextAlignmentOptions.Center) != 0)
            return rectLeft + freeWidth * 0.5f;

        return rectLeft;
    }

    void SetIconVisible(bool visible)
    {
        if (_iconRect == null)
            return;

        _iconRect.gameObject.SetActive(visible);
    }
}
