using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public struct RelationshipCharacterInfo
{
    [SerializeField] private string _characterId;
    [SerializeField] private string _characterName;
    [SerializeField] private int _relationship;
    [SerializeField] private Sprite _avatar;

    public RelationshipCharacterInfo(string characterName, int relationship)
        : this(characterName, relationship, null)
    {
    }

    public RelationshipCharacterInfo(string characterName, int relationship, Sprite avatar)
        : this("", characterName, relationship, avatar)
    {
    }

    public RelationshipCharacterInfo(string characterId, string characterName, int relationship, Sprite avatar = null)
    {
        _characterId = SaveDataSanitizer.SanitizeIdentifier(characterId);
        _characterName = SaveDataSanitizer.SanitizePlayerName(characterName);
        _relationship = SaveDataSanitizer.ClampStatValue(relationship);
        _avatar = avatar;
    }

    public string CharacterId => string.IsNullOrEmpty(_characterId)
        ? SaveDataSanitizer.SanitizeIdentifier(_characterName)
        : _characterId;

    public string CharacterName => _characterName;
    public int Relationship => _relationship;
    public Sprite Avatar => _avatar;

    public RelationshipCharacterInfo WithRelationship(int relationship)
    {
        return new RelationshipCharacterInfo(_characterId, _characterName, relationship, _avatar);
    }

    public RelationshipCharacterInfo WithServerState(string characterId, string characterName, int relationship)
    {
        return new RelationshipCharacterInfo(
            string.IsNullOrEmpty(characterId) ? _characterId : characterId,
            string.IsNullOrEmpty(characterName) ? _characterName : characterName,
            relationship,
            _avatar);
    }

    public RelationshipCharacterInfo WithAvatar(Sprite avatar)
    {
        return new RelationshipCharacterInfo(_characterId, _characterName, _relationship, avatar);
    }
}

[DisallowMultipleComponent]
public sealed class RelationshipsWithCharacters : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private const string DefaultRelationshipTextFormat = "{0}";
    private const int MaxServerRelationshipItems = 256;

    [Header("References")]
    [SerializeField] private TMP_Text _characterNameText;
    [SerializeField] private TMP_Text _relationshipText;
    [SerializeField] private Image _avatarTarget;
    [SerializeField] private Image _moonPhaseTarget;

    [Header("Data")]
    [SerializeField] private RelationshipCharacterInfo[] _characters = new RelationshipCharacterInfo[0];
    [SerializeField] private Sprite[] _avatarSprites = new Sprite[0];
    [SerializeField] private string _relationshipTextFormat = DefaultRelationshipTextFormat;
    [SerializeField] private bool _showHighestOnEnable = true;
    [SerializeField] private bool _syncFromServerOnEnable = true;
    [SerializeField] private bool _includeServerOnlyRelationships = true;

    [Header("Moon")]
    [SerializeField] private Sprite[] _moonPhaseSprites = new Sprite[6];
    [SerializeField] private int _minRelationshipForFirstMoonPhase;
    [SerializeField] private int _maxRelationshipForLastMoonPhase = 100;

    [Header("Swipe")]
    [SerializeField] private bool _useStandaloneInput = true;
    [SerializeField] private bool _acceptTouchSwipe = true;
    [SerializeField] private bool _acceptMouseSwipe = true;
    [SerializeField] private RectTransform _swipeArea;
    [SerializeField] private Camera _eventCamera;
    [SerializeField] private float _minSwipeDistance = 80f;
    [SerializeField] private float _horizontalDominance = 1.15f;
    [SerializeField] private bool _loopNavigation = true;
    [SerializeField] private bool _previewDrag = true;
    [SerializeField] private float _dragPreviewFactor = 0.35f;
    [SerializeField] private float _maxDragPreviewDistance = 120f;

    [Header("Animation")]
    [SerializeField] private float _slideDistance = 480f;
    [SerializeField] private float _animationDuration = 0.32f;
    [SerializeField] private Ease _animationEase = Ease.OutCubic;
    [SerializeField] private bool _fadeTextsDuringSwipe = true;
    [SerializeField] private bool _useUnscaledTime = true;

    private readonly List<int> _sortedIndexes = new List<int>();
    private RectTransform _avatarRectTransform;
    private Sequence _activeSequence;
    private Vector2 _avatarHomePosition;
    private Color _avatarHomeColor = Color.white;
    private float _characterNameHomeAlpha = 1f;
    private float _relationshipHomeAlpha = 1f;
    private Vector2 _pointerDownPosition;
    private int _currentSortedIndex;
    private bool _hasHomePose;
    private bool _isPointerDown;
    private bool _isDragging;
    private bool _swipeHandled;
    private bool _serverSyncInFlight;
    private SwipeInputSource _activeSwipeInputSource = SwipeInputSource.None;
    private int _activeStandaloneTouchId = -1;

    public int CharacterCount => _characters != null ? _characters.Length : 0;
    public int CurrentSortedIndex => _currentSortedIndex;
    public int CurrentOriginalIndex => GetCurrentOriginalIndex();
    public bool IsAnimating => _activeSequence != null && _activeSequence.IsActive();

    private void Reset()
    {
        _avatarTarget = GetComponent<Image>();
    }

    private void OnValidate()
    {
        if (_characters == null)
        {
            _characters = new RelationshipCharacterInfo[0];
        }

        if (_avatarSprites == null)
        {
            _avatarSprites = new Sprite[0];
        }

        EnsureMoonPhaseArraySize();

        if (string.IsNullOrEmpty(_relationshipTextFormat))
        {
            _relationshipTextFormat = DefaultRelationshipTextFormat;
        }

        if (_maxRelationshipForLastMoonPhase <= _minRelationshipForFirstMoonPhase)
        {
            _maxRelationshipForLastMoonPhase = _minRelationshipForFirstMoonPhase + 1;
        }

        _minSwipeDistance = Mathf.Max(1f, _minSwipeDistance);
        _horizontalDominance = Mathf.Max(0f, _horizontalDominance);
        _dragPreviewFactor = Mathf.Max(0f, _dragPreviewFactor);
        _maxDragPreviewDistance = Mathf.Max(0f, _maxDragPreviewDistance);
        _slideDistance = Mathf.Max(1f, _slideDistance);
        _animationDuration = Mathf.Max(0f, _animationDuration);
    }

    private void Awake()
    {
        ResolveAvatarRectTransform();
        CaptureHomePose();
        RebuildSortedIndexes(false);

        if (_showHighestOnEnable)
        {
            _currentSortedIndex = 0;
        }

        ApplyCurrentCharacter(true);
        QueueServerSyncIfNeeded();
    }

    private void OnEnable()
    {
        ResolveAvatarRectTransform();
        CaptureHomePose();
        RebuildSortedIndexes(!_showHighestOnEnable);

        if (_showHighestOnEnable)
        {
            _currentSortedIndex = 0;
        }

        ApplyCurrentCharacter(true);
        QueueServerSyncIfNeeded();
    }

    private void OnDisable()
    {
        ResetSwipeTracking();
        KillActiveAnimation(false);
        RestoreHomePose();
    }

    private void OnDestroy()
    {
        KillActiveAnimation(false);
    }

    private void Update()
    {
        if (!_useStandaloneInput)
        {
            return;
        }

        ProcessStandaloneInput();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BeginSwipe(eventData.position, SwipeInputSource.EventSystem, false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isPointerDown && !BeginSwipe(eventData.position, SwipeInputSource.EventSystem, true))
        {
            return;
        }

        if (_activeSwipeInputSource == SwipeInputSource.EventSystem)
        {
            _isDragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_activeSwipeInputSource != SwipeInputSource.EventSystem || !_isDragging || !_previewDrag || IsAnimating)
        {
            return;
        }

        PreviewDrag(eventData.position - _pointerDownPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_activeSwipeInputSource != SwipeInputSource.EventSystem || !_isDragging)
        {
            return;
        }

        FinishSwipe(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_activeSwipeInputSource != SwipeInputSource.EventSystem || !_isPointerDown || _isDragging)
        {
            return;
        }

        FinishSwipe(eventData.position);
    }

    public void Refresh()
    {
        RebuildSortedIndexes(true);
        ApplyCurrentCharacter(true);
    }

    public void RefreshAndShowHighest()
    {
        RebuildSortedIndexes(false);
        _currentSortedIndex = 0;
        ApplyCurrentCharacter(true);
    }

    public void ShowNext()
    {
        TryMove(1);
    }

    public void ShowPrevious()
    {
        TryMove(-1);
    }

    public void SetCharacters(RelationshipCharacterInfo[] characters)
    {
        _characters = CopyCharacters(characters);
        RefreshAndShowHighest();
    }

    public void SetAvatars(Sprite[] avatars)
    {
        _avatarSprites = CopySprites(avatars);
        ApplyCurrentCharacter(true);
    }

    public void SetAvatar(int originalIndex, Sprite avatar)
    {
        if (!IsValidOriginalIndex(originalIndex))
        {
            return;
        }

        _characters[originalIndex] = _characters[originalIndex].WithAvatar(avatar);
        ApplyCurrentCharacter(true);
    }

    public void SetMoonPhases(Sprite[] moonPhaseSprites)
    {
        _moonPhaseSprites = CopySprites(moonPhaseSprites);
        EnsureMoonPhaseArraySize();
        ApplyCurrentCharacter(true);
    }

    public void SetMoonRelationshipRange(int minRelationship, int maxRelationship)
    {
        _minRelationshipForFirstMoonPhase = minRelationship;
        _maxRelationshipForLastMoonPhase = Mathf.Max(minRelationship + 1, maxRelationship);
        ApplyCurrentCharacter(true);
    }

    public void SetRelationship(int originalIndex, int relationship)
    {
        if (!IsValidOriginalIndex(originalIndex))
        {
            return;
        }

        int currentOriginalIndex = GetCurrentOriginalIndex();
        _characters[originalIndex] = _characters[originalIndex].WithRelationship(relationship);
        RebuildSortedIndexes(false);
        _currentSortedIndex = FindSortedIndexByOriginalIndex(currentOriginalIndex);
        ApplyCurrentCharacter(true);
    }

    public void SyncFromServer()
    {
        if (isActiveAndEnabled)
            StartCoroutine(SyncRelationshipsFromServer());
    }

    public bool TryGetCurrentCharacter(out RelationshipCharacterInfo character)
    {
        int originalIndex = GetCurrentOriginalIndex();

        if (!IsValidOriginalIndex(originalIndex))
        {
            character = default;
            return false;
        }

        character = _characters[originalIndex];
        return true;
    }

    public void CaptureCurrentPoseAsHome()
    {
        CaptureHomePose();
    }

    private void QueueServerSyncIfNeeded()
    {
        if (!_syncFromServerOnEnable ||
            NetworkManager.Instance == null ||
            !NetworkManager.IsAuthenticated ||
            !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(SyncRelationshipsFromServer());
    }

    private IEnumerator SyncRelationshipsFromServer()
    {
        if (_serverSyncInFlight ||
            NetworkManager.Instance == null ||
            !NetworkManager.IsAuthenticated)
        {
            yield break;
        }

        _serverSyncInFlight = true;
        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.FetchRelationships((json, err) =>
        {
            payload = json;
            error = err;
        });
        _serverSyncInFlight = false;

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("[Relationships] Failed to load server relationships: " + error);
            yield break;
        }

        if (ApplyServerRelationships(payload))
            RefreshAndShowHighest();
    }

    private bool ApplyServerRelationships(string json)
    {
        var states = ParseServerRelationships(json);
        if (states.Count == 0)
            return false;

        bool changed = false;
        foreach (var state in states)
        {
            if (string.IsNullOrEmpty(state.characterId) && string.IsNullOrEmpty(state.characterName))
                continue;

            int index = FindOriginalIndexByServerState(state);
            if (index >= 0)
            {
                _characters[index] = _characters[index].WithServerState(
                    state.characterId,
                    state.characterName,
                    state.relationship);
                changed = true;
                continue;
            }

            if (!_includeServerOnlyRelationships)
                continue;

            AppendServerRelationship(state);
            changed = true;
        }

        return changed;
    }

    private int FindOriginalIndexByServerState(RelationshipServerState state)
    {
        if (_characters == null || _characters.Length == 0)
            return -1;

        string requestedId = SaveDataSanitizer.SanitizeIdentifier(state.characterId);
        string requestedName = SaveDataSanitizer.SanitizePlayerName(state.characterName);
        string requestedNameId = SaveDataSanitizer.SanitizeIdentifier(requestedName);

        for (int i = 0; i < _characters.Length; i++)
        {
            string characterId = SaveDataSanitizer.SanitizeIdentifier(_characters[i].CharacterId);
            string characterName = SaveDataSanitizer.SanitizePlayerName(_characters[i].CharacterName);
            string characterNameId = SaveDataSanitizer.SanitizeIdentifier(characterName);

            if (!string.IsNullOrEmpty(requestedId) &&
                (StringEquals(characterId, requestedId) || StringEquals(characterNameId, requestedId)))
            {
                return i;
            }

            if (!string.IsNullOrEmpty(requestedName) &&
                (StringEquals(characterName, requestedName) || StringEquals(characterId, requestedNameId)))
            {
                return i;
            }
        }

        return -1;
    }

    private void AppendServerRelationship(RelationshipServerState state)
    {
        var next = new RelationshipCharacterInfo(
            state.characterId,
            string.IsNullOrEmpty(state.characterName) ? state.characterId : state.characterName,
            state.relationship);

        int oldLength = _characters != null ? _characters.Length : 0;
        Array.Resize(ref _characters, oldLength + 1);
        _characters[oldLength] = next;
    }

    private static List<RelationshipServerState> ParseServerRelationships(string json)
    {
        var result = new List<RelationshipServerState>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        string rawRelationships = ResolveRelationshipsArray(json);
        if (!string.IsNullOrWhiteSpace(rawRelationships))
        {
            foreach (string rawItem in NetworkJson.GetArrayItems(rawRelationships))
            {
                if (result.Count >= MaxServerRelationshipItems)
                    break;

                var state = ParseRelationshipObject(rawItem);
                if (state.HasIdentity)
                    result.Add(state);
            }
        }

        var dictionary = NetworkJson.GetIntDictionary(json, "relationships");
        AddRelationshipDictionary(result, dictionary);
        dictionary = NetworkJson.GetIntDictionary(json, "data");
        AddRelationshipDictionary(result, dictionary);

        return result;
    }

    private static void AddRelationshipDictionary(
        List<RelationshipServerState> result,
        Dictionary<string, int> dictionary)
    {
        if (result == null || dictionary == null || dictionary.Count == 0)
            return;

        foreach (var kv in dictionary)
        {
            if (result.Count >= MaxServerRelationshipItems)
                break;

            result.Add(new RelationshipServerState
            {
                characterId = SaveDataSanitizer.SanitizeIdentifier(kv.Key),
                characterName = "",
                relationship = SaveDataSanitizer.ClampStatValue(kv.Value)
            });
        }
    }

    private static RelationshipServerState ParseRelationshipObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !NetworkJson.LooksLikeJsonObject(raw))
            return default;

        string characterId = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetFirstString(
            raw,
            "characterId",
            "id",
            "character",
            "characterKey"));

        string characterName = SaveDataSanitizer.SanitizePlayerName(NetworkJson.GetFirstString(
            raw,
            "characterName",
            "name",
            "displayName",
            "title"));

        int relationship = NetworkJson.GetInt(raw, "relationship", int.MinValue);
        if (relationship == int.MinValue)
            relationship = NetworkJson.GetInt(raw, "level", int.MinValue);
        if (relationship == int.MinValue)
            relationship = NetworkJson.GetInt(raw, "value", int.MinValue);
        if (relationship == int.MinValue)
            relationship = NetworkJson.GetInt(raw, "score", 0);

        return new RelationshipServerState
        {
            characterId = characterId,
            characterName = characterName,
            relationship = SaveDataSanitizer.ClampStatValue(relationship)
        };
    }

    private static string ResolveRelationshipsArray(string json)
    {
        string trimmed = json.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
            return trimmed;

        return FirstRaw(
            NetworkJson.GetRawValue(trimmed, "relationships"),
            NetworkJson.GetRawValue(trimmed, "items"),
            NetworkJson.GetRawValue(trimmed, "characters"),
            NetworkJson.GetRawValue(trimmed, "data"));
    }

    private static string FirstRaw(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (!string.IsNullOrWhiteSpace(value) && value != "null" && value.TrimStart().StartsWith("[", StringComparison.Ordinal))
                return value;
        }

        return "";
    }

    private static bool StringEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void ProcessStandaloneInput()
    {
        if (_activeSwipeInputSource != SwipeInputSource.None &&
            _activeSwipeInputSource != SwipeInputSource.Standalone)
        {
            return;
        }

        if (_acceptTouchSwipe && Input.touchCount > 0)
        {
            ProcessStandaloneTouchInput();
            return;
        }

        if (_acceptMouseSwipe)
        {
            ProcessStandaloneMouseInput();
        }
    }

    private void ProcessStandaloneTouchInput()
    {
        if (_activeStandaloneTouchId < 0)
        {
            TryBeginStandaloneTouch();
            return;
        }

        if (!TryGetActiveTouch(out Touch touch))
        {
            ResetSwipeTracking();
            return;
        }

        if (touch.phase == TouchPhase.Canceled)
        {
            CancelSwipe();
            return;
        }

        if (touch.phase == TouchPhase.Ended)
        {
            FinishSwipe(touch.position);
            return;
        }

        PreviewStandaloneSwipe(touch.position);
    }

    private void TryBeginStandaloneTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase != TouchPhase.Began || !IsScreenPointInSwipeArea(touch.position))
            {
                continue;
            }

            if (BeginSwipe(touch.position, SwipeInputSource.Standalone, true))
            {
                _activeStandaloneTouchId = touch.fingerId;
            }

            return;
        }
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.fingerId == _activeStandaloneTouchId)
            {
                activeTouch = touch;
                return true;
            }
        }

        activeTouch = default;
        return false;
    }

    private void ProcessStandaloneMouseInput()
    {
        Vector2 mousePosition = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsScreenPointInSwipeArea(mousePosition))
            {
                BeginSwipe(mousePosition, SwipeInputSource.Standalone, true);
            }

            return;
        }

        if (_activeSwipeInputSource != SwipeInputSource.Standalone)
        {
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            FinishSwipe(mousePosition);
            return;
        }

        if (Input.GetMouseButton(0))
        {
            PreviewStandaloneSwipe(mousePosition);
        }
    }

    private bool BeginSwipe(Vector2 screenPosition, SwipeInputSource inputSource, bool dragging)
    {
        if (!CanHandleInput())
        {
            return false;
        }

        if (_activeSwipeInputSource != SwipeInputSource.None && _activeSwipeInputSource != inputSource)
        {
            return false;
        }

        _pointerDownPosition = screenPosition;
        _isPointerDown = true;
        _isDragging = dragging;
        _swipeHandled = false;
        _activeSwipeInputSource = inputSource;
        return true;
    }

    private void PreviewStandaloneSwipe(Vector2 screenPosition)
    {
        if (_activeSwipeInputSource != SwipeInputSource.Standalone || !_isDragging || !_previewDrag || IsAnimating)
        {
            return;
        }

        PreviewDrag(screenPosition - _pointerDownPosition);
    }

    private void FinishSwipe(Vector2 screenPosition)
    {
        if (!_isPointerDown)
        {
            ResetSwipeTracking();
            return;
        }

        HandleSwipe(screenPosition - _pointerDownPosition);
        ResetSwipeTracking();
    }

    private void CancelSwipe()
    {
        AnimateBackToHome();
        ResetSwipeTracking();
    }

    private void ResetSwipeTracking()
    {
        _isPointerDown = false;
        _isDragging = false;
        _swipeHandled = false;
        _activeSwipeInputSource = SwipeInputSource.None;
        _activeStandaloneTouchId = -1;
    }

    private void HandleSwipe(Vector2 delta)
    {
        if (_swipeHandled)
        {
            return;
        }

        _swipeHandled = true;

        if (!IsValidHorizontalSwipe(delta))
        {
            AnimateBackToHome();
            return;
        }

        int direction = delta.x < 0f ? 1 : -1;

        if (!TryMove(direction))
        {
            AnimateBackToHome();
        }
    }

    private bool TryMove(int direction)
    {
        if (_sortedIndexes.Count <= 1 || IsAnimating)
        {
            return false;
        }

        int normalizedDirection = direction >= 0 ? 1 : -1;
        int targetSortedIndex = ResolveTargetSortedIndex(normalizedDirection);

        if (targetSortedIndex == _currentSortedIndex)
        {
            return false;
        }

        PlaySwipeAnimation(targetSortedIndex, normalizedDirection);
        return true;
    }

    private int ResolveTargetSortedIndex(int direction)
    {
        int targetIndex = _currentSortedIndex + direction;

        if (_loopNavigation)
        {
            if (targetIndex < 0)
            {
                return _sortedIndexes.Count - 1;
            }

            if (targetIndex >= _sortedIndexes.Count)
            {
                return 0;
            }

            return targetIndex;
        }

        return Mathf.Clamp(targetIndex, 0, _sortedIndexes.Count - 1);
    }

    private void PlaySwipeAnimation(int targetSortedIndex, int direction)
    {
        KillActiveAnimation(false);

        if (_animationDuration <= 0f || !isActiveAndEnabled)
        {
            _currentSortedIndex = targetSortedIndex;
            ApplyCurrentCharacter(true);
            return;
        }

        ResolveAvatarRectTransform();

        if (_avatarRectTransform == null && _avatarTarget == null && !_fadeTextsDuringSwipe)
        {
            _currentSortedIndex = targetSortedIndex;
            ApplyCurrentCharacter(true);
            return;
        }

        float halfDuration = _animationDuration * 0.5f;
        float visualDirection = direction > 0 ? -1f : 1f;
        Vector2 offset = new Vector2(_slideDistance * visualDirection, 0f);
        Vector2 outPosition = _avatarHomePosition + offset;
        Vector2 inPosition = _avatarHomePosition - offset;

        _activeSequence = DOTween.Sequence()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);

        AppendOutAnimation(outPosition, halfDuration);

        _activeSequence.AppendCallback(() =>
        {
            _currentSortedIndex = targetSortedIndex;
            ApplyCurrentCharacter(false);
            PrepareInPose(inPosition);
        });

        AppendInAnimation(halfDuration);

        _activeSequence
            .OnComplete(CompleteAnimation)
            .OnKill(() => _activeSequence = null);
    }

    private void AppendOutAnimation(Vector2 outPosition, float duration)
    {
        if (_avatarRectTransform != null)
        {
            _activeSequence.Join(_avatarRectTransform.DOAnchorPos(outPosition, duration).SetEase(_animationEase));
        }

        if (_avatarTarget != null)
        {
            _activeSequence.Join(_avatarTarget.DOFade(0f, duration).SetEase(_animationEase));
        }

        if (_fadeTextsDuringSwipe)
        {
            AppendTextFade(0f, duration);
        }
    }

    private void AppendInAnimation(float duration)
    {
        if (_avatarRectTransform != null)
        {
            _activeSequence.Join(_avatarRectTransform.DOAnchorPos(_avatarHomePosition, duration).SetEase(_animationEase));
        }

        if (_avatarTarget != null)
        {
            _activeSequence.Join(_avatarTarget.DOFade(_avatarHomeColor.a, duration).SetEase(_animationEase));
        }

        if (_fadeTextsDuringSwipe)
        {
            AppendTextFadeToHome(duration);
        }
    }

    private void AppendTextFade(float alpha, float duration)
    {
        if (_characterNameText != null)
        {
            _activeSequence.Join(_characterNameText.DOFade(alpha, duration).SetEase(_animationEase));
        }

        if (_relationshipText != null)
        {
            _activeSequence.Join(_relationshipText.DOFade(alpha, duration).SetEase(_animationEase));
        }
    }

    private void AppendTextFadeToHome(float duration)
    {
        if (_characterNameText != null)
        {
            _activeSequence.Join(_characterNameText.DOFade(_characterNameHomeAlpha, duration).SetEase(_animationEase));
        }

        if (_relationshipText != null)
        {
            _activeSequence.Join(_relationshipText.DOFade(_relationshipHomeAlpha, duration).SetEase(_animationEase));
        }
    }

    private void PrepareInPose(Vector2 inPosition)
    {
        if (_avatarRectTransform != null)
        {
            _avatarRectTransform.anchoredPosition = inPosition;
        }

        SetAvatarAlpha(0f);

        if (_fadeTextsDuringSwipe)
        {
            SetTextAlpha(0f);
        }
    }

    private void CompleteAnimation()
    {
        RestoreHomePose();
    }

    private void PreviewDrag(Vector2 delta)
    {
        if (_avatarRectTransform == null || !_hasHomePose)
        {
            return;
        }

        float offsetX = Mathf.Clamp(
            delta.x * _dragPreviewFactor,
            -_maxDragPreviewDistance,
            _maxDragPreviewDistance);

        _avatarRectTransform.anchoredPosition = _avatarHomePosition + new Vector2(offsetX, 0f);
    }

    private void AnimateBackToHome()
    {
        if (_avatarRectTransform == null || !_hasHomePose)
        {
            RestoreHomePose();
            return;
        }

        KillActiveAnimation(false);

        if (_animationDuration <= 0f || !isActiveAndEnabled)
        {
            RestoreHomePose();
            return;
        }

        float duration = Mathf.Min(_animationDuration * 0.5f, _animationDuration);
        _activeSequence = DOTween.Sequence()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);

        _activeSequence.Join(_avatarRectTransform.DOAnchorPos(_avatarHomePosition, duration).SetEase(_animationEase));

        if (_avatarTarget != null)
        {
            _activeSequence.Join(_avatarTarget.DOFade(_avatarHomeColor.a, duration).SetEase(_animationEase));
        }

        AppendTextFadeToHome(duration);

        _activeSequence
            .OnComplete(RestoreHomePose)
            .OnKill(() => _activeSequence = null);
    }

    private bool IsValidHorizontalSwipe(Vector2 delta)
    {
        float horizontalDistance = Mathf.Abs(delta.x);
        float verticalDistance = Mathf.Abs(delta.y);

        if (horizontalDistance < _minSwipeDistance)
        {
            return false;
        }

        return horizontalDistance >= verticalDistance * _horizontalDominance;
    }

    private bool IsScreenPointInSwipeArea(Vector2 screenPosition)
    {
        if (_swipeArea == null)
        {
            return true;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            _swipeArea,
            screenPosition,
            ResolveEventCamera());
    }

    private Camera ResolveEventCamera()
    {
        if (_eventCamera != null)
        {
            return _eventCamera;
        }

        Canvas canvas = _swipeArea != null
            ? _swipeArea.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();

        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private bool CanHandleInput()
    {
        return isActiveAndEnabled && !IsAnimating && _sortedIndexes.Count > 0;
    }

    private void RebuildSortedIndexes(bool keepCurrentCharacter)
    {
        int currentOriginalIndex = keepCurrentCharacter ? GetCurrentOriginalIndex() : -1;
        _sortedIndexes.Clear();

        if (_characters == null)
        {
            _currentSortedIndex = 0;
            return;
        }

        for (int i = 0; i < _characters.Length; i++)
        {
            _sortedIndexes.Add(i);
        }

        _sortedIndexes.Sort(CompareByRelationshipDescending);

        if (_sortedIndexes.Count == 0)
        {
            _currentSortedIndex = 0;
            return;
        }

        if (currentOriginalIndex >= 0)
        {
            _currentSortedIndex = FindSortedIndexByOriginalIndex(currentOriginalIndex);
            return;
        }

        _currentSortedIndex = Mathf.Clamp(_currentSortedIndex, 0, _sortedIndexes.Count - 1);
    }

    private int CompareByRelationshipDescending(int leftOriginalIndex, int rightOriginalIndex)
    {
        int relationshipComparison = _characters[rightOriginalIndex].Relationship.CompareTo(_characters[leftOriginalIndex].Relationship);

        if (relationshipComparison != 0)
        {
            return relationshipComparison;
        }

        return leftOriginalIndex.CompareTo(rightOriginalIndex);
    }

    private int FindSortedIndexByOriginalIndex(int originalIndex)
    {
        if (!IsValidOriginalIndex(originalIndex))
        {
            return 0;
        }

        for (int i = 0; i < _sortedIndexes.Count; i++)
        {
            if (_sortedIndexes[i] == originalIndex)
            {
                return i;
            }
        }

        return 0;
    }

    private void ApplyCurrentCharacter(bool restorePose)
    {
        int originalIndex = GetCurrentOriginalIndex();

        if (!IsValidOriginalIndex(originalIndex))
        {
            ClearView();
            return;
        }

        RelationshipCharacterInfo character = _characters[originalIndex];

        if (_characterNameText != null)
        {
            _characterNameText.text = character.CharacterName;
        }

        if (_relationshipText != null)
        {
            _relationshipText.text = FormatRelationshipText(character);
        }

        if (_avatarTarget != null)
        {
            _avatarTarget.sprite = GetAvatar(originalIndex);
        }

        ApplyMoonPhase(character.Relationship);

        if (restorePose)
        {
            RestoreHomePose();
        }
    }

    private void ClearView()
    {
        if (_characterNameText != null)
        {
            _characterNameText.text = string.Empty;
        }

        if (_relationshipText != null)
        {
            _relationshipText.text = string.Empty;
        }

        if (_avatarTarget != null)
        {
            _avatarTarget.sprite = null;
        }

        if (_moonPhaseTarget != null)
        {
            _moonPhaseTarget.sprite = null;
        }

        RestoreHomePose();
    }

    private string FormatRelationshipText(RelationshipCharacterInfo character)
    {
        try
        {
            return string.Format(_relationshipTextFormat, character.Relationship, character.CharacterName);
        }
        catch (FormatException)
        {
            return character.Relationship.ToString();
        }
    }

    private Sprite GetAvatar(int originalIndex)
    {
        if (IsValidOriginalIndex(originalIndex) && _characters[originalIndex].Avatar != null)
        {
            return _characters[originalIndex].Avatar;
        }

        if (_avatarSprites == null || originalIndex < 0 || originalIndex >= _avatarSprites.Length)
        {
            return null;
        }

        return _avatarSprites[originalIndex];
    }

    private void ApplyMoonPhase(int relationship)
    {
        if (_moonPhaseTarget == null)
        {
            return;
        }

        _moonPhaseTarget.sprite = GetMoonPhaseSprite(relationship);
    }

    private Sprite GetMoonPhaseSprite(int relationship)
    {
        if (_moonPhaseSprites == null || _moonPhaseSprites.Length == 0)
        {
            return null;
        }

        int phaseIndex = GetMoonPhaseIndex(relationship);
        return phaseIndex >= 0 ? _moonPhaseSprites[phaseIndex] : null;
    }

    private int GetMoonPhaseIndex(int relationship)
    {
        if (_moonPhaseSprites == null || _moonPhaseSprites.Length == 0)
        {
            return -1;
        }

        if (_moonPhaseSprites.Length == 1)
        {
            return 0;
        }

        float normalized = Mathf.InverseLerp(
            _minRelationshipForFirstMoonPhase,
            _maxRelationshipForLastMoonPhase,
            relationship);

        int index = Mathf.FloorToInt(normalized * _moonPhaseSprites.Length);
        return Mathf.Clamp(index, 0, _moonPhaseSprites.Length - 1);
    }

    private int GetCurrentOriginalIndex()
    {
        if (_sortedIndexes.Count == 0 || _currentSortedIndex < 0 || _currentSortedIndex >= _sortedIndexes.Count)
        {
            return -1;
        }

        return _sortedIndexes[_currentSortedIndex];
    }

    private bool IsValidOriginalIndex(int originalIndex)
    {
        return _characters != null && originalIndex >= 0 && originalIndex < _characters.Length;
    }

    private void ResolveAvatarRectTransform()
    {
        _avatarRectTransform = _avatarTarget != null ? _avatarTarget.rectTransform : null;
    }

    private void CaptureHomePose()
    {
        ResolveAvatarRectTransform();

        if (_avatarRectTransform != null)
        {
            _avatarHomePosition = _avatarRectTransform.anchoredPosition;
        }

        if (_avatarTarget != null)
        {
            _avatarHomeColor = _avatarTarget.color;
        }

        if (_characterNameText != null)
        {
            _characterNameHomeAlpha = _characterNameText.alpha;
        }

        if (_relationshipText != null)
        {
            _relationshipHomeAlpha = _relationshipText.alpha;
        }

        _hasHomePose = true;
    }

    private void RestoreHomePose()
    {
        if (!_hasHomePose)
        {
            return;
        }

        if (_avatarRectTransform != null)
        {
            _avatarRectTransform.anchoredPosition = _avatarHomePosition;
        }

        SetAvatarAlpha(_avatarHomeColor.a);
        SetTextAlphaToHome();
    }

    private void SetAvatarAlpha(float alpha)
    {
        if (_avatarTarget == null)
        {
            return;
        }

        Color color = _avatarTarget.color;
        color.a = alpha;
        _avatarTarget.color = color;
    }

    private void SetTextAlpha(float alpha)
    {
        if (_characterNameText != null)
        {
            _characterNameText.alpha = alpha;
        }

        if (_relationshipText != null)
        {
            _relationshipText.alpha = alpha;
        }
    }

    private void SetTextAlphaToHome()
    {
        if (_characterNameText != null)
        {
            _characterNameText.alpha = _characterNameHomeAlpha;
        }

        if (_relationshipText != null)
        {
            _relationshipText.alpha = _relationshipHomeAlpha;
        }
    }

    private void KillActiveAnimation(bool complete)
    {
        if (_activeSequence == null)
        {
            return;
        }

        Sequence sequence = _activeSequence;
        _activeSequence = null;
        sequence.Kill(complete);
    }

    private RelationshipCharacterInfo[] CopyCharacters(RelationshipCharacterInfo[] characters)
    {
        if (characters == null || characters.Length == 0)
        {
            return new RelationshipCharacterInfo[0];
        }

        RelationshipCharacterInfo[] copy = new RelationshipCharacterInfo[characters.Length];
        Array.Copy(characters, copy, characters.Length);
        return copy;
    }

    private void EnsureMoonPhaseArraySize()
    {
        if (_moonPhaseSprites == null)
        {
            _moonPhaseSprites = new Sprite[6];
            return;
        }

        if (_moonPhaseSprites.Length != 6)
        {
            Array.Resize(ref _moonPhaseSprites, 6);
        }
    }

    private Sprite[] CopySprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return new Sprite[0];
        }

        Sprite[] copy = new Sprite[sprites.Length];
        Array.Copy(sprites, copy, sprites.Length);
        return copy;
    }

    private enum SwipeInputSource
    {
        None,
        EventSystem,
        Standalone
    }

    private struct RelationshipServerState
    {
        public string characterId;
        public string characterName;
        public int relationship;

        public bool HasIdentity => !string.IsNullOrEmpty(characterId) || !string.IsNullOrEmpty(characterName);
    }
}
