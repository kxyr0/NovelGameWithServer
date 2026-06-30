using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-8500)]
public sealed class RuntimeFpsOverlay : MonoBehaviour
{
    readonly StringBuilder _builder = new StringBuilder(256);

    Canvas _canvas;
    RectTransform _panel;
    TextMeshProUGUI _label;
    float _nextUpdateAt;
    const float UpdateInterval = 0.25f;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    void OnEnable()
    {
        BuildUi();
        _nextUpdateAt = 0f;
    }

    void Update()
    {
        if (Time.unscaledTime < _nextUpdateAt)
            return;

        _nextUpdateAt = Time.unscaledTime + UpdateInterval;
        RuntimePerformanceSnapshot snapshot = RuntimePerformanceDiagnostics.Snapshot;
        _builder.Length = 0;
        _builder.Append("FPS ");
        _builder.Append(snapshot.CurrentFps.ToString("0"));
        _builder.Append(" avg ");
        _builder.Append(snapshot.AverageFps.ToString("0"));
        _builder.Append(" min ");
        _builder.Append(snapshot.MinFps.ToString("0"));
        _builder.Append('\n');
        _builder.Append("Frame ");
        _builder.Append(snapshot.FrameMilliseconds.ToString("0.0"));
        _builder.Append(" ms");
        _builder.Append('\n');
        _builder.Append("Mem ");
        _builder.Append(FormatMegabytes(snapshot.TotalAllocatedMemoryBytes));
        _builder.Append(" MB  GC ");
        _builder.Append(FormatMegabytes(snapshot.GcAllocatedInFrameBytes));
        _builder.Append(" MB");
        _builder.Append('\n');
        _builder.Append(snapshot.SceneName);
        _builder.Append("  ");
        _builder.Append(snapshot.DeviceModel);

        if (_label != null)
            _label.text = _builder.ToString();
    }

    void BuildUi()
    {
        if (_canvas != null && _label != null)
            return;

        _canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (_panel == null)
        {
            GameObject panelObject = new GameObject("FPS Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.GetComponent<RectTransform>();
            Image background = panelObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0f, 0f, 0f, 0.62f);
        }

        RectTransform rect = _panel;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(460f, 150f);

        if (_label == null)
        {
            GameObject labelObject = new GameObject("FPS Label", typeof(RectTransform));
            labelObject.transform.SetParent(_panel, false);
            _label = labelObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform labelRect = _label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        _label.raycastTarget = false;
        _label.fontSize = 22f;
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.color = Color.white;
        _label.enableWordWrapping = false;
    }

    static string FormatMegabytes(long bytes)
    {
        if (bytes <= 0L)
            return "0";

        return (bytes / (1024f * 1024f)).ToString("0.0");
    }
}
