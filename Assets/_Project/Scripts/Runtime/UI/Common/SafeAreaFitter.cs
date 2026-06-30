using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransformCache;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        CacheAndApply();
    }

    private void OnEnable()
    {
        CacheAndApply();
    }

    private void Update()
    {
        if (rectTransformCache == null)
            rectTransformCache = GetComponent<RectTransform>();

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (safeArea != lastSafeArea || screenSize != lastScreenSize)
            Apply();
    }

    private void CacheAndApply()
    {
        rectTransformCache = GetComponent<RectTransform>();
        Apply();
    }

    private void Apply()
    {
        if (rectTransformCache == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransformCache.anchorMin = anchorMin;
        rectTransformCache.anchorMax = anchorMax;
        rectTransformCache.offsetMin = Vector2.zero;
        rectTransformCache.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
