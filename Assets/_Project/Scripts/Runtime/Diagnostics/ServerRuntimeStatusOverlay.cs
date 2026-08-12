using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturnal/Диагностика сервера")]
public sealed class ServerRuntimeStatusOverlay : MonoBehaviour
{
    const int OverlaySortingOrder = 30000;

    [Header("Показ")]
    [SerializeField, Tooltip("Показывать панель сразу после запуска сцены.")]
    bool showOnStart = true;
    [SerializeField, Tooltip("Не удалять объект при переходе между сценами.")]
    bool keepBetweenScenes = true;
    [SerializeField, Tooltip("Разрешить панель в обычной релизной сборке. По умолчанию работает только в Editor и Development Build.")]
    bool allowInReleaseBuild = false;
    [SerializeField, Tooltip("Клавиша скрытия и показа панели. None отключает горячую клавишу.")]
    KeyCode toggleKey = KeyCode.F9;

    [Header("Размер")]
    [SerializeField, Min(260f), Tooltip("Ширина панели в пикселях Canvas.")]
    float panelWidth = 620f;
    [SerializeField, Min(160f), Tooltip("Высота панели в пикселях Canvas.")]
    float panelHeight = 360f;
    [SerializeField, Min(12f), Tooltip("Размер текста внутри панели.")]
    float fontSize = 20f;
    [SerializeField, Min(0.1f), Tooltip("Как часто обновлять показанные статусы.")]
    float refreshIntervalSeconds = 0.5f;

    GameObject _root;
    TextMeshProUGUI _text;
    ScrollRect _scrollRect;
    float _nextRefreshTime;

    void Awake()
    {
        if (!CanRunInThisBuild())
        {
            enabled = false;
            return;
        }

        if (keepBetweenScenes)
            DontDestroyOnLoad(gameObject);

        CreateUiIfNeeded();
        SetVisible(showOnStart);
    }

    void OnEnable()
    {
        if (_root != null)
            _root.SetActive(showOnStart);
    }

    void OnDisable()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_root != null)
            Destroy(_root);
    }

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            ToggleVisible();

        if (_root == null || !_root.activeSelf || Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        RefreshText();
    }

    public void ToggleVisible()
    {
        SetVisible(_root == null || !_root.activeSelf);
    }

    public void SetVisible(bool visible)
    {
        CreateUiIfNeeded();
        if (_root == null)
            return;

        _root.SetActive(visible);
        if (visible)
            RefreshText();
    }

    void CreateUiIfNeeded()
    {
        if (_root != null)
            return;

        _root = new GameObject("Диагностика сервера", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);

        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelObject = new GameObject("Панель статуса", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        panelObject.transform.SetParent(_root.transform, false);
        var panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(18f, -18f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelObject.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.05f, 0.88f);

        var viewportObject = new GameObject("Область прокрутки", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panelObject.transform, false);
        var viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 14f);
        viewportRect.offsetMax = new Vector2(-18f, -14f);
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        var textObject = new GameObject("Текст статуса", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        textObject.transform.SetParent(viewportObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        _text = textObject.GetComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.color = new Color(0.94f, 0.96f, 1f, 1f);
        _text.fontSize = fontSize;
        _text.enableWordWrapping = true;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.raycastTarget = false;

        var fitter = textObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect = panelObject.GetComponent<ScrollRect>();
        _scrollRect.viewport = viewportRect;
        _scrollRect.content = textRect;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 32f;
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    void RefreshText()
    {
        if (_text != null)
            _text.text = ServerRuntimeStatusFormatter.Build(toggleKey);
    }

    bool CanRunInThisBuild()
    {
        if (allowInReleaseBuild)
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }
}
