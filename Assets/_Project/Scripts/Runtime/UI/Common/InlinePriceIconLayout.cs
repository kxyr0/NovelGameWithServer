using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class InlinePriceIconLayout : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _defaultIcon;

    [Header("Text")]
    [SerializeField] private string _pricePrefix = " (";
    [SerializeField] private string _priceSuffix = ")";

    [Header("Icon Layout")]
    [SerializeField, FormerlySerializedAs("_iconSize"), Min(1f)] private float _iconWidth = 24f;
    [SerializeField, Min(1f)] private float _iconHeight = 24f;
    [SerializeField] private float _spacing = 3f;
    [SerializeField] private float _verticalOffset;
    [SerializeField] private bool _driveIconAnchors = true;
    [SerializeField] private bool _refreshInLateUpdate = true;
    [SerializeField] private bool _refreshBeforeCanvasRender = true;

    int _price;
    string _baseText = "";
    bool _dirty = true;
    bool _subscribed;

    public Image Icon => _icon;

    public void SetContent(string text, int price, Sprite iconOverride = null)
    {
        TryAutoWire();

        _baseText = text ?? "";
        _price = Mathf.Max(0, price);

        if (_text != null)
            _text.text = _price > 0 ? _baseText + _pricePrefix + _price + _priceSuffix : _baseText;

        Sprite icon = iconOverride != null ? iconOverride : _defaultIcon;
        if (_icon != null && icon != null)
            _icon.sprite = icon;

        MarkDirty();
        RefreshNow();
    }

    public void SetIcon(Sprite icon)
    {
        _defaultIcon = icon;
        if (_icon != null && icon != null)
            _icon.sprite = icon;

        MarkDirty();
    }

    public void Clear()
    {
        SetContent("", 0, null);
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        TryAutoWire();

        bool showIcon = _price > 0 && _text != null && _icon != null && (_icon.sprite != null || _defaultIcon != null);
        if (!showIcon)
        {
            SetIconVisible(false);
            _dirty = false;
            return;
        }

        if (_icon.sprite == null)
            _icon.sprite = _defaultIcon;

        _text.ForceMeshUpdate(true, true);
        TMP_TextInfo textInfo = _text.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
        {
            SetIconVisible(false);
            _dirty = false;
            return;
        }

        int lastVisible = FindLastVisibleCharacter(textInfo);
        if (lastVisible < 0)
        {
            SetIconVisible(false);
            _dirty = false;
            return;
        }

        TMP_CharacterInfo character = textInfo.characterInfo[lastVisible];
        RectTransform iconRect = _icon.rectTransform;
        if (iconRect == null)
            return;

        if (_driveIconAnchors)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
        }

        iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _iconWidth);
        iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _iconHeight);

        float y = (character.ascender + character.descender) * 0.5f + _verticalOffset;
        float x = character.topRight.x + _spacing + _iconWidth * 0.5f;
        Vector3 world = _text.transform.TransformPoint(new Vector3(x, y, 0f));
        Vector3 targetLocal = iconRect.parent != null ? iconRect.parent.InverseTransformPoint(world) : world;
        iconRect.anchoredPosition = new Vector2(targetLocal.x, targetLocal.y);

        SetIconVisible(true);
        _dirty = false;
    }

    void Reset()
    {
        TryAutoWire();
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
        _iconWidth = Mathf.Max(1f, _iconWidth);
        _iconHeight = Mathf.Max(1f, _iconHeight);
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
        if (changedObject == _text)
            MarkDirty();
    }

    void TryAutoWire()
    {
        if (_text == null)
            _text = GetComponentInChildren<TMP_Text>(true);

        if (_icon == null)
            _icon = FindIconImage();
    }

    Image FindIconImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            string imageName = image.name;
            if (imageName.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("Price", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
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

    int FindLastVisibleCharacter(TMP_TextInfo textInfo)
    {
        for (int i = textInfo.characterCount - 1; i >= 0; i--)
        {
            if (textInfo.characterInfo[i].isVisible)
                return i;
        }

        return -1;
    }

    void SetIconVisible(bool visible)
    {
        if (_icon == null)
            return;

        _icon.enabled = visible;
        _icon.gameObject.SetActive(visible);
    }
}
