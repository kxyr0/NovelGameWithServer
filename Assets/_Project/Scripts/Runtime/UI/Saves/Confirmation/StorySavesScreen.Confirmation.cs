using UnityEngine;
using UnityEngine.UI;

public enum StoryNewSaveCreationMode
{
    [InspectorName("От текущего момента")]
    BranchFromCurrent = 0,

    [InspectorName("Полностью новое прохождение")]
    FreshPlaythrough = 1
}

public sealed partial class StorySavesScreen
{
    [Header("Create save behavior")]
    [SerializeField]
    [Tooltip("От текущего момента = новый независимый слот-клон текущего прохождения. Полностью новое прохождение = новый пустой слот и запуск истории с самого начала.")]
    private StoryNewSaveCreationMode _newSaveCreationMode =
        StoryNewSaveCreationMode.BranchFromCurrent;

    // Kept under the old serialized names so the existing scene/prefab references
    // are preserved. The screen is now used to CONFIRM OVERWRITE OF CURRENT SAVE.
    [Header("Current save confirmation")]
    [SerializeField] private string _createConfirmationScreenId =
        "SaveCreateConfirmation";
    [SerializeField] private GameObject _createConfirmationRoot;
    [SerializeField] private Button _confirmCreateSaveButton;
    [SerializeField] private Button _cancelCreateSaveButton;
    [SerializeField] private StorySaveCardView _createSavePreview;
    [SerializeField] private bool _deactivateConfirmationWhenClosed = true;

    [Header("Save messages")]
    [SerializeField] private string _saveUnavailableMessage =
        "Нет активного прохождения для сохранения";
    [SerializeField] private string _freshStartUnavailableMessage =
        "Не удалось начать новое прохождение";
    [SerializeField] private string _saveFailedMessage =
        "Не удалось сохранить прохождение";

    private int _pendingOverwriteSlot = -1;
    private SaveData _pendingOverwriteSave;

    public StoryNewSaveCreationMode NewSaveCreationMode =>
        _newSaveCreationMode;

    private void BindCreateConfirmationButtons()
    {
        _confirmCreateSaveButton?.onClick.AddListener(ConfirmCreateSave);
        _cancelCreateSaveButton?.onClick.AddListener(CloseCreateConfirmation);
        CloseCreateConfirmation();
    }

    private void UnbindCreateConfirmationButtons()
    {
        _confirmCreateSaveButton?.onClick.RemoveListener(ConfirmCreateSave);
        _cancelCreateSaveButton?.onClick.RemoveListener(CloseCreateConfirmation);
    }

    private bool CanCreateManualSave()
    {
        return _newSaveCreationMode == StoryNewSaveCreationMode.FreshPlaythrough
            ? CanCreateFreshPlaythrough()
            : CanCreateBranchFromCurrent();
    }

    private bool CanCreateBranchFromCurrent()
    {
        StoryManager storyManager = StoryManager.Instance;
        if (_data == null || storyManager == null ||
            !storyManager.HasSelectedStory || SaveManager.Instance == null)
        {
            return false;
        }

        string targetStoryId = ResolveStoryId();
        string runtimeStoryId = SaveDataSanitizer.SanitizeIdentifier(
            storyManager.CurrentStoryId);

        return !string.IsNullOrEmpty(targetStoryId) &&
               targetStoryId == runtimeStoryId &&
               GameState.Instance != null &&
               GameState.Instance.currentNode != null;
    }

    private bool CanCreateFreshPlaythrough()
    {
        return _data != null &&
               _data.Story != null &&
               _data.CanStartStory &&
               _menuController != null &&
               SaveManager.Instance != null &&
               !string.IsNullOrEmpty(ResolveStoryId());
    }

    /// <summary>
    /// Identifies the CURRENT save slot. This check deliberately does not inspect
    /// GameState/currentNode: slot identity and runtime saveability are different
    /// concerns. Mixing them caused current-slot clicks to fall through into LOAD.
    /// </summary>
    private bool IsCurrentSaveSlot(int slot)
    {
        if (!SavePathResolver.IsValidSlot(slot))
            return false;

        string storyId = ResolveStoryId();
        return !string.IsNullOrEmpty(storyId) &&
               StorySaveSlotSelection.IsSelectedSlot(storyId, slot);
    }

    /// <summary>
    /// New-save button has no confirmation anymore. It performs the selected enum
    /// mode immediately.
    /// </summary>
    private void CreateNewSaveImmediately()
    {
        if (_freeSlot < 0 || !CanCreateManualSave())
        {
            ToastManager.Instance?.ShowSystemMessage(
                _freeSlot < 0
                    ? _slotsFullMessage
                    : (_newSaveCreationMode == StoryNewSaveCreationMode.FreshPlaythrough
                        ? _freshStartUnavailableMessage
                        : _saveUnavailableMessage));
            return;
        }

        int newSlot = _freeSlot;

        if (_newSaveCreationMode == StoryNewSaveCreationMode.FreshPlaythrough)
        {
            StartFreshPlaythrough(newSlot);
            return;
        }

        CreateBranchFromCurrent(newSlot);
    }

    /// <summary>
    /// Opens the existing confirmation panel for the ACTIVE save slot and shows the
    /// exact save that is about to be overwritten.
    /// </summary>
    private void OpenCurrentSaveConfirmation(int slot, SaveData existingSave)
    {
        if (!IsCurrentSaveSlot(slot) || existingSave == null || !existingSave.HasPosition)
        {
            ToastManager.Instance?.ShowSystemMessage(_saveUnavailableMessage);
            return;
        }

        GameObject root = ResolveCreateConfirmationRoot();
        if (root == null)
        {
            Debug.LogWarning(
                $"[StorySavesScreen] Confirmation screen '{_createConfirmationScreenId}' was not found.",
                this);
            return;
        }

        _pendingOverwriteSlot = slot;
        _pendingOverwriteSave = existingSave;

        RefreshCurrentSavePreview(existingSave, slot);
        SetConfirmationVisible(root, true);

        Debug.Log(
            $"[SAVE][OVERWRITE_CONFIRM_OPEN] storyId='{ResolveStoryId()}', slot={slot}.",
            this);
    }

    /// <summary>
    /// Kept with the old public name so existing serialized UnityEvents/listeners do
    /// not break. It now confirms overwrite of the current save; it NEVER creates a
    /// new slot.
    /// </summary>
    public void ConfirmCreateSave()
    {
        int slot = _pendingOverwriteSlot;
        if (!IsCurrentSaveSlot(slot))
        {
            ToastManager.Instance?.ShowSystemMessage(_saveUnavailableMessage);
            CloseCreateConfirmation();
            return;
        }

        SaveManager manager = SaveManager.Instance;
        SaveData saved = manager != null
            ? manager.BuildCurrentSaveData(StoryManager.Instance)
            : null;

        string targetStoryId = ResolveStoryId();
        string runtimeStoryId = saved != null
            ? SaveDataSanitizer.SanitizeIdentifier(saved.storyId)
            : "";

        // Never overwrite a save card with runtime data from another story.
        if (saved == null || !saved.HasPosition ||
            string.IsNullOrEmpty(targetStoryId) ||
            runtimeStoryId != targetStoryId)
        {
            Debug.LogWarning(
                $"[SAVE][OVERWRITE_REJECTED] targetStoryId='{targetStoryId}', runtimeStoryId='{runtimeStoryId}', slot={slot}, hasPosition={saved != null && saved.HasPosition}.",
                this);
            ToastManager.Instance?.ShowSystemMessage(_saveFailedMessage);
            return;
        }

        manager.Save(saved, slot);
        StorySaveSlotSelection.SelectSlot(targetStoryId, slot);

        Debug.Log(
            $"[SAVE][OVERWRITE_SUCCESS] storyId='{ResolveStoryId()}', slot={slot}.",
            this);

        CloseCreateConfirmation();
        Refresh();
    }

    private void CreateBranchFromCurrent(int newSlot)
    {
        SaveData saved = SaveManager.Instance.CreateNewSaveFromCurrent(
            newSlot,
            StoryManager.Instance);

        if (saved == null || !saved.HasPosition)
        {
            ToastManager.Instance?.ShowSystemMessage(_saveFailedMessage);
            Refresh();
            return;
        }

        Refresh();
    }

    private void StartFreshPlaythrough(int newSlot)
    {
        bool prepared = SaveManager.Instance.PrepareFreshPlaythroughSlot(
            _data.Story,
            newSlot);

        if (!prepared)
        {
            ToastManager.Instance?.ShowSystemMessage(_freshStartUnavailableMessage);
            Refresh();
            return;
        }

        CloseCreateConfirmation();

        // Launch through MenuController rather than StoryManager.StartStory() directly.
        // This preserves the normal Story screen transition, preload and pre-story setup.
        _menuController.StartStory(_data);
    }

    public void CloseCreateConfirmation()
    {
        _pendingOverwriteSlot = -1;
        _pendingOverwriteSave = null;

        GameObject root = ResolveCreateConfirmationRoot();
        if (root != null)
            SetConfirmationVisible(root, false);
    }

    private void RefreshCurrentSavePreview(SaveData save, int slot)
    {
        if (_createSavePreview == null)
            return;

        _createSavePreview.gameObject.SetActive(true);
        _createSavePreview.Bind(null, _data, save, slot);
    }

    // Kept for source compatibility with older partials/calls. Confirmation is no
    // longer opened by New Save, so this method intentionally does nothing except
    // route to the active save when possible.
    private void OpenCreateConfirmation()
    {
        string storyId = ResolveStoryId();
        int slot = StorySaveSlotSelection.GetSelectedSlot(storyId);
        SaveManager manager = SaveManager.Instance;
        SaveData save = manager != null && SavePathResolver.IsValidSlot(slot)
            ? manager.LoadForStorySlotIfExists(storyId, slot)
            : null;

        if (save != null && save.HasPosition)
            OpenCurrentSaveConfirmation(slot, save);
    }

    private void RefreshCreateSavePreview()
    {
        if (_pendingOverwriteSave != null && _pendingOverwriteSlot >= 0)
            RefreshCurrentSavePreview(_pendingOverwriteSave, _pendingOverwriteSlot);
    }

    private GameObject ResolveCreateConfirmationRoot()
    {
        if (_createConfirmationRoot != null)
            return _createConfirmationRoot;

        string screenId = UIScreenState.NormalizeScreenId(
            _createConfirmationScreenId);
        if (screenId.Length == 0)
            return null;

        UIScreenMarker[] markers = FindObjectsOfType<UIScreenMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            UIScreenMarker marker = markers[i];
            if (marker != null && marker.ScreenId == screenId)
                return marker.gameObject;
        }

        return null;
    }

    private void SetConfirmationVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        if (visible && !root.activeSelf)
            root.SetActive(true);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (visible)
            root.transform.SetAsLastSibling();
        else if (_deactivateConfirmationWhenClosed && root.activeSelf)
            root.SetActive(false);
    }
}
