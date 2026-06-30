using System;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Активный FadeSprite по CanvasGroup страницы")]
public sealed class CanvasGroupPageFadeSpriteActiveState : MonoBehaviour
{
    [Header("Страницы")]
    [SerializeField]
    [InspectorName("CanvasGroup страниц")]
    [Tooltip("CanvasGroup страниц, пока открыта хотя бы одна из них, все FadeSprite ниже будут держаться в active-состоянии.")]
    private CanvasGroup[] _pageCanvasGroups = Array.Empty<CanvasGroup>();

    [SerializeField, Range(0f, 1f)]
    [InspectorName("Порог alpha")]
    [Tooltip("Страница считается открытой, когда alpha её CanvasGroup не ниже этого значения.")]
    private float _activeAlphaThreshold = 0.01f;

    [SerializeField]
    [InspectorName("Требовать activeInHierarchy")]
    [Tooltip("Если включено, страница считается открытой только когда объект CanvasGroup активен в иерархии.")]
    private bool _requireActiveInHierarchy = true;

    [SerializeField]
    [InspectorName("Учитывать родительский alpha")]
    [Tooltip("Если включено, проверяется не только alpha самой страницы, но и alpha всех родительских CanvasGroup. Это спасает случай, когда страница имеет alpha 1, но скрыта родителем.")]
    private bool _useEffectiveAlpha = true;

    [SerializeField]
    [InspectorName("Требовать interactable")]
    [Tooltip("Если включено, страница считается открытой только когда CanvasGroup.interactable = true.")]
    private bool _requireInteractable;

    [SerializeField]
    [InspectorName("Требовать blocksRaycasts")]
    [Tooltip("Если включено, страница считается открытой только когда CanvasGroup.blocksRaycasts = true.")]
    private bool _requireBlocksRaycasts;

    [SerializeField]
    [InspectorName("Проверять UIScreenMarker в Play Mode")]
    [Tooltip("Если включено и у страницы есть UIScreenMarker, в Play Mode страница считается открытой только когда её Screen Id является текущим экраном. В редакторе эта проверка не мешает предпросмотру.")]
    private bool _requireCurrentScreenMarkerInPlayMode = true;

    [Header("FadeSprite")]
    [SerializeField]
    [InspectorName("FadeSprite цели")]
    [Tooltip("UISpriteFade или UISpriteStateFade, которые будут переключаться в active/default. Можно указать сколько угодно.")]
    private UISpriteStateFade[] _spriteFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Автоискать в детях")]
    [Tooltip("Если список FadeSprite пустой, скрипт сам найдёт все UISpriteFade/UISpriteStateFade в дочерних объектах.")]
    private bool _autoCollectSpriteFades = true;

    [SerializeField]
    [InspectorName("Искать неактивные дети")]
    [Tooltip("Учитывать выключенные дочерние объекты при автоиске FadeSprite.")]
    private bool _includeInactiveChildren = true;

    [SerializeField]
    [InspectorName("Отключить hover у FadeSprite")]
    [Tooltip("Если включено, сами FadeSprite не будут переключаться от наведения и будут слушаться только этого компонента.")]
    private bool _disableSpriteFadePointerHover = true;

    [SerializeField]
    [InspectorName("Удерживать каждый кадр")]
    [Tooltip("Если другой скрипт случайно вернёт sprite в default, этот компонент на следующем кадре снова поставит правильное состояние.")]
    private bool _enforceEveryFrame = true;

    [SerializeField]
    [InspectorName("Применять сразу без tween")]
    [Tooltip("Если включено, при удержании active/default состояния FadeSprite применяется без плавной анимации. Это полезно для навигационных кнопок, чтобы состояние не зависало из-за незавершённого fade.")]
    private bool _applyImmediateWhenEnforcing = true;

    [Header("Дополнительно")]
    [SerializeField]
    [InspectorName("Показать когда active")]
    [Tooltip("Дополнительные объекты, которые включаются вместе с active-состоянием.")]
    private GameObject[] _showWhenActive = Array.Empty<GameObject>();

    [SerializeField]
    [InspectorName("Скрыть когда active")]
    [Tooltip("Дополнительные объекты, которые выключаются вместе с active-состоянием.")]
    private GameObject[] _hideWhenActive = Array.Empty<GameObject>();

    [SerializeField]
    [InspectorName("Сбрасывать при Disable")]
    [Tooltip("Если включено, при выключении этого компонента FadeSprite вернутся в default.")]
    private bool _resetWhenDisabled = true;

    [Header("События")]
    [SerializeField]
    [InspectorName("State changed")]
    [Tooltip("Вызывается при смене состояния. True означает, что хотя бы одна указанная страница открыта.")]
    private UnityEvent<bool> _stateChanged = new UnityEvent<bool>();

    [Header("Диагностика")]
    [SerializeField]
    [InspectorName("Сейчас active")]
    [Tooltip("Текущее вычисленное состояние. Обновляется в редакторе и в Play Mode.")]
    private bool _debugIsActive;

    [SerializeField]
    [InspectorName("Открытая страница")]
    [Tooltip("Имя первой страницы, которую скрипт сейчас считает открытой.")]
    private string _debugOpenPage = "";

    [SerializeField]
    [InspectorName("Причина")]
    [Tooltip("Почему скрипт считает страницу открытой или закрытой.")]
    private string _debugReason = "";

    [SerializeField]
    [InspectorName("Последний alpha")]
    [Tooltip("Alpha последней проверенной страницы.")]
    private float _debugLastAlpha;

    [SerializeField]
    [InspectorName("Последний effective alpha")]
    [Tooltip("Alpha страницы с учётом родительских CanvasGroup.")]
    private float _debugLastEffectiveAlpha;

    [SerializeField]
    [InspectorName("Текущий Screen Id")]
    [Tooltip("Текущий экран по UIScreenState. Полезно, если CanvasGroup alpha 1, но подсветка должна выключиться.")]
    private string _debugCurrentScreenId = "";

    private bool _isActive;

    public bool IsActive => _isActive;
    public UnityEvent<bool> StateChanged => _stateChanged;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        Refresh(forceVisuals: true);
    }

    private void OnDisable()
    {
        if (_resetWhenDisabled)
            ApplyState(false, forceVisuals: true);
    }

    private void Update()
    {
        if (!Application.isPlaying || _enforceEveryFrame)
            Refresh(forceVisuals: _enforceEveryFrame);
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && !_enforceEveryFrame)
            Refresh(forceVisuals: false);
    }

    private void OnValidate()
    {
        _activeAlphaThreshold = Mathf.Clamp01(_activeAlphaThreshold);
        EnsureReferences();
        Refresh(forceVisuals: true);
    }

    [ContextMenu("Обновить состояние")]
    public void RefreshNow()
    {
        Refresh(forceVisuals: true);
    }

    [ContextMenu("Собрать FadeSprite из детей")]
    public void CollectSpriteFadesFromChildren()
    {
        _spriteFades = GetComponentsInChildren<UISpriteStateFade>(_includeInactiveChildren);
        Refresh(forceVisuals: true);
    }

    private void Refresh(bool forceVisuals)
    {
        ApplyState(IsAnyPageOpen(), forceVisuals);
    }

    private void ApplyState(bool active, bool forceVisuals)
    {
        bool changed = _isActive != active;
        if (!forceVisuals && !changed)
            return;

        _isActive = active;
        _debugIsActive = active;
        ApplySpriteFades(active, forceVisuals);
        SetObjectsActive(_showWhenActive, active);
        SetObjectsActive(_hideWhenActive, !active);

        if (changed)
            _stateChanged?.Invoke(active);
    }

    private bool IsAnyPageOpen()
    {
        _debugOpenPage = "";
        _debugReason = "Нет назначенных CanvasGroup страниц.";
        _debugLastAlpha = 0f;
        _debugLastEffectiveAlpha = 0f;
        _debugCurrentScreenId = UIScreenState.CurrentScreenId;

        if (_pageCanvasGroups == null)
            return false;

        for (int i = 0; i < _pageCanvasGroups.Length; i++)
        {
            CanvasGroup group = _pageCanvasGroups[i];
            if (IsPageOpen(group, out string reason, out float alpha, out float effectiveAlpha))
            {
                _debugOpenPage = group != null ? group.name : "";
                _debugReason = reason;
                _debugLastAlpha = alpha;
                _debugLastEffectiveAlpha = effectiveAlpha;
                return true;
            }

            if (i == 0)
            {
                _debugOpenPage = group != null ? group.name : "";
                _debugReason = reason;
                _debugLastAlpha = alpha;
                _debugLastEffectiveAlpha = effectiveAlpha;
            }
        }

        return false;
    }

    private bool IsPageOpen(CanvasGroup group, out string reason, out float alpha, out float effectiveAlpha)
    {
        alpha = group != null ? group.alpha : 0f;
        effectiveAlpha = group != null ? CalculateEffectiveAlpha(group) : 0f;

        if (group == null)
        {
            reason = "CanvasGroup страницы пустой.";
            return false;
        }

        if (_requireActiveInHierarchy && !group.gameObject.activeInHierarchy)
        {
            reason = "GameObject страницы не activeInHierarchy.";
            return false;
        }

        float testedAlpha = _useEffectiveAlpha ? effectiveAlpha : alpha;
        if (testedAlpha < _activeAlphaThreshold)
        {
            reason = _useEffectiveAlpha
                ? "Effective alpha ниже порога."
                : "Alpha страницы ниже порога.";
            return false;
        }

        if (_requireInteractable && !group.interactable)
        {
            reason = "CanvasGroup.interactable выключен.";
            return false;
        }

        if (_requireBlocksRaycasts && !group.blocksRaycasts)
        {
            reason = "CanvasGroup.blocksRaycasts выключен.";
            return false;
        }

        if (!MatchesCurrentScreenMarker(group, out string markerReason))
        {
            reason = markerReason;
            return false;
        }

        reason = _useEffectiveAlpha
            ? "Страница открыта по effective alpha."
            : "Страница открыта по alpha.";
        return true;
    }

    private void ApplySpriteFades(bool active, bool forceVisuals)
    {
        if (_spriteFades == null)
            return;

        for (int i = 0; i < _spriteFades.Length; i++)
        {
            UISpriteStateFade spriteFade = _spriteFades[i];
            if (spriteFade == null)
                continue;

            if (_disableSpriteFadePointerHover)
                spriteFade.SetPointerHoverEnabled(false, false);

            spriteFade.SetActiveState(active);

            if (forceVisuals && _applyImmediateWhenEnforcing)
                spriteFade.ApplyImmediate();
        }
    }

    private static float CalculateEffectiveAlpha(CanvasGroup group)
    {
        if (group == null)
            return 0f;

        float alpha = 1f;
        Transform current = group.transform;
        while (current != null)
        {
            CanvasGroup currentGroup = current.GetComponent<CanvasGroup>();
            if (currentGroup != null)
                alpha *= currentGroup.alpha;

            current = current.parent;
        }

        return alpha;
    }

    private bool MatchesCurrentScreenMarker(CanvasGroup group, out string reason)
    {
        reason = "";
        if (!_requireCurrentScreenMarkerInPlayMode || !Application.isPlaying || group == null)
            return true;

        UIScreenMarker marker = group.GetComponent<UIScreenMarker>();
        if (marker == null)
            marker = group.GetComponentInParent<UIScreenMarker>();

        if (marker == null || string.IsNullOrEmpty(marker.ScreenId))
            return true;

        if (UIScreenState.IsCurrent(marker.ScreenId))
            return true;

        reason = "UIScreenMarker не является текущим экраном: page=" + marker.ScreenId
            + ", current=" + UIScreenState.CurrentScreenId;
        return false;
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }

    private void EnsureReferences()
    {
        if (_autoCollectSpriteFades && (_spriteFades == null || _spriteFades.Length == 0))
            _spriteFades = GetComponentsInChildren<UISpriteStateFade>(_includeInactiveChildren);

        if (_pageCanvasGroups == null)
            _pageCanvasGroups = Array.Empty<CanvasGroup>();
        if (_spriteFades == null)
            _spriteFades = Array.Empty<UISpriteStateFade>();
        if (_showWhenActive == null)
            _showWhenActive = Array.Empty<GameObject>();
        if (_hideWhenActive == null)
            _hideWhenActive = Array.Empty<GameObject>();
    }
}
