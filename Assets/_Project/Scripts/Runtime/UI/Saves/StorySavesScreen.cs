using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Saves/Story Saves Screen")]
public sealed partial class StorySavesScreen : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private Transform _content;
    [SerializeField] private StorySaveCardView _saveCardPrefab;
    [SerializeField] private Button _newSaveButton;
    [SerializeField] private GameObject _emptyState;

    [Header("Slots")]
    [SerializeField, Range(1, SavePathResolver.MaxSaveSlot)]
    private int _firstSlot = StorySaveSlotSelection.DefaultSlot;
    [SerializeField, Range(1, SavePathResolver.MaxSaveSlot)]
    private int _lastSlot = SavePathResolver.MaxSaveSlot;
    [SerializeField] private bool _newestFirst = true;
    [SerializeField]
    private string _slotsFullMessage =
        "Свободных сохранений больше нет";

    private readonly List<StorySaveCardView> _spawned =
        new List<StorySaveCardView>();

    private GameData _data;
    private MenuController _menuController;
    private int _freeSlot = -1;
    private string _owningScreenId = "Save";
    private bool _refreshPending;

    private void Awake()
    {
        _newSaveButton?.onClick.AddListener(CreateNewSave);
        BindCreateConfirmationButtons();
    }

    private void OnEnable()
    {
        UIScreenMarker marker = GetComponentInParent<UIScreenMarker>(true);
        if (marker != null && !string.IsNullOrEmpty(marker.ScreenId))
            _owningScreenId = marker.ScreenId;

        SaveManager.OnStorySaveChanged += HandleSaveChanged;
        UIScreenState.CurrentScreenChanged += HandleCurrentScreenChanged;

        if (IsOwningScreenVisible())
            Refresh();
        else
            _refreshPending = true;
    }

    private void OnDisable()
    {
        SaveManager.OnStorySaveChanged -= HandleSaveChanged;
        UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
        CloseCreateConfirmation();
    }

    private void OnDestroy()
    {
        _newSaveButton?.onClick.RemoveListener(CreateNewSave);
        UnbindCreateConfirmationButtons();
    }

    private void OnValidate()
    {
        _firstSlot = Mathf.Clamp(
            _firstSlot, 1, SavePathResolver.MaxSaveSlot);
        _lastSlot = Mathf.Clamp(
            _lastSlot, _firstSlot, SavePathResolver.MaxSaveSlot);
    }

    public void Configure(GameData data, MenuController menuController)
    {
        _data = data;
        _menuController = menuController;

        if (isActiveAndEnabled && IsOwningScreenVisible())
            Refresh();
        else
            _refreshPending = true;
    }

    [ContextMenu("Refresh Saves")]
    public void Refresh()
    {
        _refreshPending = false;
        ClearCards();

        string storyId = ResolveStoryId();
        List<StorySaveSlotEntry> entries =
            StorySaveSlotCatalog.Read(
                storyId,
                _firstSlot,
                _lastSlot,
                _newestFirst,
                out _freeSlot);

        for (int i = 0; i < entries.Count; i++)
            Spawn(entries[i]);

        if (_emptyState != null)
            _emptyState.SetActive(entries.Count == 0);

        if (_newSaveButton != null)
        {
            _newSaveButton.interactable =
                _freeSlot >= 0 && CanCreateManualSave();
        }
    }

    public void OpenSaveSlot(int slot)
    {
        string storyId = ResolveStoryId();
        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null
            ? manager.LoadForStorySlotIfExists(storyId, slot)
            : null;

        if (save == null || !save.HasPosition)
        {
            Refresh();
            return;
        }

        // The currently selected save is not a "load another branch" action.
        // Clicking it means "save the current runtime state into THIS slot" and
        // therefore opens the confirmation screen with this exact save card.
        int selectedSlot = StorySaveSlotSelection.GetSelectedSlot(storyId);
        bool isCurrentSlot = IsCurrentSaveSlot(slot);

        Debug.Log(
            $"[SAVE][CARD_CLICK] storyId='{storyId}', slot={slot}, selectedSlot={selectedSlot}, isCurrent={isCurrentSlot}.",
            this);

        // IMPORTANT: deciding whether this is the CURRENT save must depend only on
        // slot selection. It must NOT depend on whether GameState/currentNode is
        // currently saveable. Otherwise a click on the active slot silently falls
        // through to StartSlot() and reloads the story instead of opening confirm.
        if (isCurrentSlot)
        {
            OpenCurrentSaveConfirmation(slot, save);
            return;
        }

        // Any other existing slot keeps the old semantics: switch/load that branch.
        StartSlot(storyId, slot);
    }

    public void CreateNewSave()
    {
        if (_freeSlot < 0)
        {
            ToastManager.Instance?.ShowSystemMessage(
                _slotsFullMessage);
            return;
        }

        // NEW SAVE must actually create/start a new save immediately.
        // Confirmation belongs to overwriting the CURRENT save, not to creation.
        CreateNewSaveImmediately();
    }

    private void Spawn(StorySaveSlotEntry entry)
    {
        if (_content == null || _saveCardPrefab == null)
            return;

        StorySaveCardView view =
            Instantiate(_saveCardPrefab, _content, false);

        view.gameObject.SetActive(true);
        view.Bind(this, _data, entry.Save, entry.Slot);
        _spawned.Add(view);
    }

    private void StartSlot(string storyId, int slot)
    {
        if (_data == null ||
            _menuController == null ||
            !_data.CanStartStory ||
            string.IsNullOrEmpty(storyId) ||
            !SavePathResolver.IsValidSlot(slot))
        {
            return;
        }

        StorySaveSlotSelection.SelectSlot(storyId, slot);
        _menuController.StartStory(_data);
    }

    private string ResolveStoryId()
    {
        StoryData story = _data != null ? _data.Story : null;
        if (story == null)
            return "";

        string id =
            SaveDataSanitizer.SanitizeIdentifier(story.StoryId);

        return !string.IsNullOrEmpty(id)
            ? id
            : SaveDataSanitizer.SanitizeIdentifier(story.name);
    }

    private void HandleSaveChanged(string storyId)
    {
        string current = ResolveStoryId();
        if (!string.IsNullOrEmpty(storyId) &&
            !string.Equals(storyId, current, StringComparison.Ordinal))
        {
            return;
        }

        if (!IsOwningScreenVisible())
        {
            _refreshPending = true;
            return;
        }

        Refresh();
    }

    private void HandleCurrentScreenChanged(string screenId)
    {
        if (_refreshPending && string.Equals(screenId, _owningScreenId, StringComparison.Ordinal))
            Refresh();
    }

    private bool IsOwningScreenVisible()
    {
        return UIScreenState.IsCurrent(_owningScreenId);
    }

    private void ClearCards()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            StorySaveCardView view = _spawned[i];
            if (view == null)
                continue;

            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        _spawned.Clear();
    }
}
