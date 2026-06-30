using UnityEngine;
using DG.Tweening;

/// <summary>
/// Управляет движением "камеры" в 2D визуальной новелле.
/// 
/// Реализуется как сдвиг RectTransform cameraRoot по X.
/// cameraRoot — родительский объект, содержащий фон и персонажей.
/// Фоновое изображение должно быть шире экрана чтобы пан был виден
/// (рекомендуется: ширина фона = ширина экрана × 1.4 или больше).
///
/// Подключение:
///   1. Создай пустой GameObject "CameraRoot" на Canvas.
///   2. Вложи в него Background и Characters.
///   3. Назначь этот объект в поле cameraRoot.
///   4. Добавь CameraController на GameManager или StoryManager GameObject.
///   5. Назначь в StoryManager.cameraController.
///
/// Авто-пан: StoryManager вызывает PanToSpeaker() при смене реплики.
/// Ручной пан: CameraNode в графе → вызывает PanToOffset() или PanToPosition().
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("Ссылки")]
    [Tooltip("RectTransform визуального мира истории. Обычно это общий родитель фона и персонажей, который двигается при панорамировании камеры.")]
    public RectTransform cameraRoot;

    [Tooltip("Дополнительные визуальные корни, которые должны двигаться вместе с Camera Root, сохраняя свои исходные позиции относительно него.")]
    [SerializeField] private RectTransform[] linkedCameraRoots;

    [SerializeField] private bool autoLinkWorldRoots = true;
    [Tooltip("Двигать CharactersRoot вместе с камерой. Выключи, если персонажи должны оставаться в своих слотах, а панорамироваться должен только фон.")]
    [SerializeField] private bool moveCharactersWithCamera = false;
    [Tooltip("Двигать VideoBackground вместе с камерой так же, как обычный фон.")]
    [SerializeField] private bool moveVideoBackgroundWithCamera = true;

    [Header("Смещения по позициям")]
    [Tooltip("Сдвиг камеры влево, когда в фокусе персонаж в слоте Left, в пикселях.")]
    public float leftOffset = 460f;

    [Tooltip("Центральная позиция камеры без горизонтального сдвига.")]
    public float centerOffset = 0f;

    [Tooltip("Сдвиг камеры вправо, когда в фокусе персонаж в слоте Right, в пикселях.")]
    public float rightOffset = -460f;

    [Header("Speaker Focus")]
    [SerializeField] private bool focusSpeakerBySlotCenter = false;
    [SerializeField, Range(0f, 1f)] private float leftFocusViewportX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float centerFocusViewportX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float rightFocusViewportX = 0.5f;
    [SerializeField] private bool ignoreParentBoundsForSpeakerFocus = true;
    [SerializeField] private RectTransform leftSlot;
    [SerializeField] private RectTransform centerSlot;
    [SerializeField] private RectTransform rightSlot;

    [Header("Анимация")]
    [Tooltip("Длительность панорамирования камеры в секундах.")]
    public float panDuration = 0.4f;

    [Tooltip("Кривая движения для анимации панорамирования.")]
    public Ease panEase = Ease.InOutSine;

    [Header("Ограничения")]
    [Tooltip("Максимальный сдвиг по X, чтобы камера не ушла за границы фона.")]
    public float maxOffsetX = 1200f;

    [Header("Безопасная область")]
    [Tooltip("Не даёт Camera Root выйти за границы родительского RectTransform, чтобы UI и персонажи не уезжали за экран.")]
    [SerializeField] private bool clampToParentBounds = true;

    [Tooltip("Разрешить небольшой сдвиг к говорящему персонажу, даже если Camera Root не шире родителя. Max Offset X всё равно ограничивает движение.")]
    [SerializeField] private bool allowFocusPanWhenContentFits = false;

    [Tooltip("Дополнительный горизонтальный отступ от края родителя при ограничении сдвига Camera Root.")]
    [SerializeField] private float parentBoundsPaddingX = 0f;

    private float _currentOffset = 0f;
    private Vector2 _cameraRootBasePosition;
    private Vector2[] _linkedRootBasePositions = System.Array.Empty<Vector2>();
    private Tween _activeTween;

    public float MaxOffsetX => maxOffsetX;
    public float CurrentOffset => _currentOffset;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        AutoLinkWorldRoots();
        AutoWireSpeakerSlots();
        ApplyPanOverflowToBackgroundRoots();
        CaptureRootBasePositions();
    }

    private void OnValidate()
    {
        panDuration = Mathf.Max(0f, panDuration);
        maxOffsetX = Mathf.Max(0f, maxOffsetX);
        parentBoundsPaddingX = Mathf.Max(0f, parentBoundsPaddingX);
        leftFocusViewportX = Mathf.Clamp01(leftFocusViewportX);
        centerFocusViewportX = Mathf.Clamp01(centerFocusViewportX);
        rightFocusViewportX = Mathf.Clamp01(rightFocusViewportX);
        AutoWireSpeakerSlots();
    }

    private void OnDestroy()
    {
        _activeTween?.Kill();

        if (Instance == this)
            Instance = null;
    }

    // ── Публичный API ────────────────────────────────────────

    /// <summary>
    /// Авто-пан по позиции персонажа.
    /// Вызывается из StoryManager при смене спикера.
    /// </summary>
    public void PanToSpeaker(CharacterPosition position)
    {
        float target = ResolveSpeakerOffset(position);
        AnimateTo(target, panDuration, !ignoreParentBoundsForSpeakerFocus);
    }

    public void PanBackgroundOnlyToSpeaker(CharacterPosition position, float strength = 1f, float duration = -1f)
    {
        float target = ResolveSpeakerOffset(position) * Mathf.Max(0f, strength);
        float d = duration >= 0f ? duration : panDuration;
        AnimateBackgroundOnlyTo(target, d, !ignoreParentBoundsForSpeakerFocus);
    }

    public void ResetBackgroundOnly(float duration = 0f)
    {
        AnimateBackgroundOnlyTo(centerOffset, Mathf.Max(0f, duration));
    }

    public void ReapplyCurrentOffset()
    {
        if (cameraRoot == null)
            return;

        EnsureLinkedRootBasePositionsLength();
        ApplyOffset(_currentOffset);
    }

    public void RegisterOrUpdateLinkedCameraRoot(
        RectTransform root,
        bool recaptureBasePosition = false,
        bool rootAlreadyIncludesCurrentOffset = false)
    {
        if (root == null || root == cameraRoot)
            return;

        var roots = new System.Collections.Generic.List<RectTransform>(linkedCameraRoots ?? System.Array.Empty<RectTransform>());
        int index = roots.IndexOf(root);
        if (index < 0)
        {
            roots.Add(root);
            linkedCameraRoots = roots.ToArray();
            EnsureLinkedRootBasePositionsLength();
            index = roots.Count - 1;
            recaptureBasePosition = true;
        }
        else
        {
            EnsureLinkedRootBasePositionsLength();
        }

        if (recaptureBasePosition && index >= 0 && index < _linkedRootBasePositions.Length)
        {
            Vector2 basePosition = root.anchoredPosition;
            if (rootAlreadyIncludesCurrentOffset)
                basePosition.x -= _currentOffset;

            _linkedRootBasePositions[index] = basePosition;
        }

        ApplyPanOverflowToBackgroundRoot(root);

        if (index >= 0 && index < _linkedRootBasePositions.Length && ShouldMoveLinkedRoot(root))
            ApplyRootOffset(root, _linkedRootBasePositions[index], _currentOffset);
    }

    /// <summary>
    /// Ручной пан на конкретное смещение (пиксели).
    /// Используется из CameraNode.
    /// </summary>
    public void PanToOffset(float xOffset, float duration = -1f)
    {
        float d = duration > 0 ? duration : panDuration;
        AnimateTo(xOffset, d);
    }

    /// <summary>
    /// Мгновенный сброс камеры в центр (без анимации).
    /// </summary>
    public void ResetInstant()
    {
        _activeTween?.Kill();
        _currentOffset = ClampOffset(centerOffset);
        ApplyOffset(_currentOffset);
    }

    // ── Внутренние методы ────────────────────────────────────

    private void AnimateTo(float targetOffset, float duration, bool useParentBounds = true)
    {
        if (cameraRoot == null) return;

        targetOffset = ClampOffset(targetOffset, useParentBounds);
        if (Mathf.Approximately(_currentOffset, targetOffset) &&
            Mathf.Approximately(GetCurrentOffset(), targetOffset))
        {
            return;
        }

        _activeTween?.Kill();
        _currentOffset = targetOffset;

        if (duration <= 0f)
        {
            ApplyOffset(targetOffset, useParentBounds);
            return;
        }

        Sequence sequence = DOTween.Sequence().SetEase(panEase);
        JoinRootTween(sequence, cameraRoot, _cameraRootBasePosition, targetOffset, duration);

        EnsureLinkedRootBasePositionsLength();
        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            if (!ShouldMoveLinkedRoot(roots[i]))
                continue;

            Vector2 basePosition = i < _linkedRootBasePositions.Length ? _linkedRootBasePositions[i] : Vector2.zero;
            JoinRootTween(sequence, roots[i], basePosition, targetOffset, duration);
        }

        _activeTween = sequence;
    }

    private void AnimateBackgroundOnlyTo(float targetOffset, float duration, bool useParentBounds = true)
    {
        if (cameraRoot == null) return;

        targetOffset = ClampOffset(targetOffset, useParentBounds);
        _activeTween?.Kill();

        if (duration <= 0f)
        {
            ApplyBackgroundOnlyOffset(targetOffset);
            return;
        }

        Sequence sequence = DOTween.Sequence().SetEase(panEase);
        JoinBackgroundOnlyRootTween(sequence, cameraRoot, _cameraRootBasePosition, targetOffset, duration);

        EnsureLinkedRootBasePositionsLength();
        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            Vector2 basePosition = i < _linkedRootBasePositions.Length ? _linkedRootBasePositions[i] : Vector2.zero;
            JoinBackgroundOnlyRootTween(sequence, roots[i], basePosition, targetOffset, duration);
        }

        _activeTween = sequence;
    }

    private void ApplyBackgroundOnlyOffset(float xOffset)
    {
        ApplyBackgroundOnlyRootOffset(cameraRoot, _cameraRootBasePosition, xOffset);

        EnsureLinkedRootBasePositionsLength();
        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            Vector2 basePosition = i < _linkedRootBasePositions.Length ? _linkedRootBasePositions[i] : Vector2.zero;
            ApplyBackgroundOnlyRootOffset(roots[i], basePosition, xOffset);
        }
    }

    private void ApplyOffset(float xOffset, bool useParentBounds = true)
    {
        if (cameraRoot == null) return;

        xOffset = ClampOffset(xOffset, useParentBounds);
        ApplyRootOffset(cameraRoot, _cameraRootBasePosition, xOffset);

        EnsureLinkedRootBasePositionsLength();
        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            if (!ShouldMoveLinkedRoot(roots[i]))
                continue;

            Vector2 basePosition = i < _linkedRootBasePositions.Length ? _linkedRootBasePositions[i] : Vector2.zero;
            ApplyRootOffset(roots[i], basePosition, xOffset);
        }
    }

    private void CaptureRootBasePositions()
    {
        _cameraRootBasePosition = cameraRoot != null ? cameraRoot.anchoredPosition : Vector2.zero;

        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        _linkedRootBasePositions = new Vector2[roots.Length];
        for (int i = 0; i < roots.Length; i++)
            _linkedRootBasePositions[i] = roots[i] != null ? roots[i].anchoredPosition : Vector2.zero;
    }

    private void EnsureLinkedRootBasePositionsLength()
    {
        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        if (_linkedRootBasePositions != null && _linkedRootBasePositions.Length == roots.Length)
            return;

        Vector2[] oldPositions = _linkedRootBasePositions ?? System.Array.Empty<Vector2>();
        var nextPositions = new Vector2[roots.Length];
        for (int i = 0; i < roots.Length; i++)
        {
            if (i < oldPositions.Length)
                nextPositions[i] = oldPositions[i];
            else
                nextPositions[i] = roots[i] != null ? roots[i].anchoredPosition : Vector2.zero;
        }

        _linkedRootBasePositions = nextPositions;
    }

    private float GetCurrentOffset()
    {
        if (cameraRoot == null)
            return _currentOffset;

        return cameraRoot.anchoredPosition.x - _cameraRootBasePosition.x;
    }

    private float ResolveSpeakerOffset(CharacterPosition position)
    {
        if (focusSpeakerBySlotCenter && TryGetSpeakerFocusOffset(position, out float focusTarget))
            return focusTarget;

        return position switch
        {
            CharacterPosition.Left => leftOffset,
            CharacterPosition.Center => centerOffset,
            CharacterPosition.Right => rightOffset,
            _ => centerOffset
        };
    }

    private static void JoinRootTween(Sequence sequence, RectTransform root, Vector2 basePosition, float xOffset, float duration)
    {
        if (sequence == null || root == null)
            return;

        sequence.Join(root.DOAnchorPos(new Vector2(basePosition.x + xOffset, basePosition.y), duration));
    }

    private static void ApplyRootOffset(RectTransform root, Vector2 basePosition, float xOffset)
    {
        if (root == null)
            return;

        root.anchoredPosition = new Vector2(basePosition.x + xOffset, basePosition.y);
    }

    private void JoinBackgroundOnlyRootTween(Sequence sequence, RectTransform root, Vector2 basePosition, float xOffset, float duration)
    {
        if (sequence == null || root == null || !IsBackgroundCameraRoot(root))
            return;

        sequence.Join(root.DOAnchorPos(new Vector2(basePosition.x + xOffset, basePosition.y), duration));
    }

    private void ApplyBackgroundOnlyRootOffset(RectTransform root, Vector2 basePosition, float xOffset)
    {
        if (root == null || !IsBackgroundCameraRoot(root))
            return;

        root.anchoredPosition = new Vector2(basePosition.x + xOffset, basePosition.y);
    }

    private bool TryGetSpeakerFocusOffset(CharacterPosition position, out float targetOffset)
    {
        targetOffset = centerOffset;

        if (cameraRoot == null)
            return false;

        RectTransform slot = GetSpeakerSlot(position);
        if (slot == null)
        {
            AutoWireSpeakerSlots();
            slot = GetSpeakerSlot(position);
        }

        RectTransform parent = cameraRoot.parent as RectTransform;
        if (slot == null || parent == null)
            return false;

        Bounds slotBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, slot);
        float desiredX = Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, GetFocusViewportX(position));
        float delta = desiredX - slotBounds.center.x;
        targetOffset = GetCurrentOffset() + delta;

        return !float.IsNaN(targetOffset) && !float.IsInfinity(targetOffset);
    }

    private RectTransform GetSpeakerSlot(CharacterPosition position)
    {
        return position switch
        {
            CharacterPosition.Left => leftSlot,
            CharacterPosition.Center => centerSlot,
            CharacterPosition.Right => rightSlot,
            _ => null
        };
    }

    private float GetFocusViewportX(CharacterPosition position)
    {
        return position switch
        {
            CharacterPosition.Left => leftFocusViewportX,
            CharacterPosition.Center => centerFocusViewportX,
            CharacterPosition.Right => rightFocusViewportX,
            _ => centerFocusViewportX
        };
    }

    private void AutoWireSpeakerSlots()
    {
        if (cameraRoot == null)
            return;

        if (leftSlot == null)
            leftSlot = FindDirectSlot("Left");
        if (centerSlot == null)
            centerSlot = FindDirectSlot("Center");
        if (rightSlot == null)
            rightSlot = FindDirectSlot("Right");
    }

    private void AutoLinkWorldRoots()
    {
        if (!autoLinkWorldRoots || cameraRoot == null)
            return;

        var roots = new System.Collections.Generic.List<RectTransform>();
        if (linkedCameraRoots != null)
        {
            for (int i = 0; i < linkedCameraRoots.Length; i++)
            {
                RectTransform root = linkedCameraRoots[i];
                if (root != null && root != cameraRoot && !roots.Contains(root))
                    roots.Add(root);
            }
        }

        RectTransform stableParent = cameraRoot.parent as RectTransform;

        if (moveCharactersWithCamera)
            AddLinkedRootByName(roots, stableParent, "CharactersRoot");

        Canvas canvas = cameraRoot.GetComponentInParent<Canvas>(true);
        Transform canvasRoot = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform
            : stableParent;

        if (moveVideoBackgroundWithCamera)
        {
            AddLinkedRootByName(roots, canvasRoot, "VideoBackground");
            AddLinkedRootByName(roots, canvasRoot, "Video Background");
        }

        linkedCameraRoots = roots.ToArray();
    }

    private void AddLinkedRootByName(System.Collections.Generic.List<RectTransform> roots, Transform searchRoot, string rootName)
    {
        if (roots == null || searchRoot == null || string.IsNullOrEmpty(rootName))
            return;

        foreach (RectTransform rect in searchRoot.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == null ||
                rect == cameraRoot ||
                !string.Equals(rect.name, rootName, System.StringComparison.OrdinalIgnoreCase) ||
                roots.Contains(rect))
            {
                continue;
            }

            roots.Add(rect);
            return;
        }
    }

    public bool MovesRoot(RectTransform root)
    {
        if (root == null)
            return false;

        if (root == cameraRoot)
            return true;

        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == root && ShouldMoveLinkedRoot(root))
                return true;
        }

        return false;
    }

    public bool MovesVideoBackgrounds => moveVideoBackgroundWithCamera;
    public bool MovesCharactersWithCamera => moveCharactersWithCamera;

    private void ApplyPanOverflowToBackgroundRoots()
    {
        ApplyPanOverflowToBackgroundRoot(cameraRoot);

        RectTransform[] roots = linkedCameraRoots ?? System.Array.Empty<RectTransform>();
        for (int i = 0; i < roots.Length; i++)
        {
            if (!ShouldMoveLinkedRoot(roots[i]))
                continue;

            ApplyPanOverflowToBackgroundRoot(roots[i]);
        }
    }

    private void ApplyPanOverflowToBackgroundRoot(RectTransform root)
    {
        if (root == null || !IsBackgroundVisualRoot(root))
            return;

        float horizontalOverflow = Mathf.Max(0f, maxOffsetX);
        if (horizontalOverflow <= 0f)
            return;

        root.offsetMin = new Vector2(-horizontalOverflow, root.offsetMin.y);
        root.offsetMax = new Vector2(horizontalOverflow, root.offsetMax.y);
    }

    private static bool IsBackgroundVisualRoot(RectTransform root)
    {
        if (root == null)
            return false;

        return root.name.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsBackgroundCameraRoot(RectTransform root)
    {
        return IsBackgroundVisualRoot(root) || IsVideoBackgroundRoot(root);
    }

    private bool ShouldMoveLinkedRoot(RectTransform root)
    {
        if (root == null)
            return false;

        if (!moveCharactersWithCamera && IsCharacterVisualRoot(root))
            return false;

        if (!moveVideoBackgroundWithCamera && IsVideoBackgroundRoot(root))
            return false;

        return true;
    }

    private static bool IsCharacterVisualRoot(RectTransform root)
    {
        if (root == null)
            return false;

        return root.name.IndexOf("CharactersRoot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               root.name.IndexOf("Characters Root", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsVideoBackgroundRoot(RectTransform root)
    {
        if (root == null)
            return false;

        if (root.name.IndexOf("VideoBackground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            root.name.IndexOf("Video Background", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return root.GetComponent<VideoBackgroundPlayer>() != null ||
               root.GetComponentInChildren<VideoBackgroundPlayer>(true) != null;
    }

    private RectTransform FindDirectSlot(string slotName)
    {
        if (cameraRoot == null)
            return null;

        Transform direct = cameraRoot.Find(slotName);
        if (direct != null)
            return direct as RectTransform;

        foreach (Transform child in cameraRoot)
        {
            if (string.Equals(child.name, slotName, System.StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        }

        foreach (RectTransform child in cameraRoot.GetComponentsInChildren<RectTransform>(true))
        {
            if (child != cameraRoot && string.Equals(child.name, slotName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    private float ClampOffset(float xOffset, bool useParentBounds = true)
    {
        float limit = maxOffsetX;

        if (useParentBounds && clampToParentBounds && TryGetParentBoundsLimit(out float parentLimit))
            limit = Mathf.Min(limit, parentLimit);

        return Mathf.Clamp(xOffset, -limit, limit);
    }

    private bool TryGetParentBoundsLimit(out float limit)
    {
        limit = 0f;

        if (cameraRoot == null)
            return false;

        RectTransform parent = cameraRoot.parent as RectTransform;
        if (parent == null)
            return false;

        float rootWidth = cameraRoot.rect.width;
        float parentWidth = parent.rect.width;

        if (rootWidth <= 0f || parentWidth <= 0f)
            return false;

        float overflow = Mathf.Max(0f, rootWidth - parentWidth);
        if (overflow <= 0f && allowFocusPanWhenContentFits)
            return false;

        limit = Mathf.Max(0f, overflow * 0.5f - parentBoundsPaddingX);
        return true;
    }
}
