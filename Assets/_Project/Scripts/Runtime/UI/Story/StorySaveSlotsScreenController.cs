using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum StorySaveSlotClickAction
{
    None = 0,
    LoadSlot = 1,
    SaveSlot = 2
}

[Serializable]
public sealed class StorySaveSlotInfo
{
    public int SlotNumber;
    public int SaveSlotIndex;
    public bool Locked;
    public bool HasSave;
    public bool IsAutosave;
    public bool IsSelected;
    public bool CanClickSlot;
    public bool CanWrite;
    public bool CanDelete;
    public bool ShowActions;
    public bool CanUseSlotClickWithoutSave;
    public string Title;
    public string Details;
    public string SavedAtIso;
    public SaveData SaveData;
    public Sprite PreviewSprite;
}

[Serializable]
public sealed class StorySaveSlotIntEvent : UnityEvent<int>
{
}

[Serializable]
public sealed class StorySaveSlotMessageEvent : UnityEvent<string>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story Save Slots Screen Controller")]
public sealed class StorySaveSlotsScreenController : MonoBehaviour
{
    [Header("Заголовок")]
    [SerializeField]
    [InspectorName("Title Text")]
    [Tooltip("TMP_Text заголовка экрана. Скрипт запишет сюда текст из поля Title Text Value.")]
    private TMP_Text _titleText;

    [SerializeField]
    [InspectorName("Title Text Value")]
    [Tooltip("Текст заголовка экрана выбора слота.")]
    private string _titleTextValue = "Выберите слот";

    [SerializeField]
    [InspectorName("Записывать заголовок")]
    [Tooltip("Если включено, при Refresh/OnEnable контроллер будет сам выставлять Title Text Value в Title Text.")]
    private bool _writeTitleText = true;

    [Header("Слоты")]
    [SerializeField]
    [InspectorName("Slot Views")]
    [Tooltip("Пять компонентов StorySaveSlotView из твоего Vertical Layout. Порядок массива = Слот 1, 2, 3, 4, 5.")]
    private StorySaveSlotView[] _slotViews = Array.Empty<StorySaveSlotView>();

    [SerializeField]
    [InspectorName("Авто найти Slot Views")]
    [Tooltip("Если Slot Views пустой, найти все StorySaveSlotView внутри этого экрана автоматически.")]
    private bool _autoFindSlotViews = true;

    [SerializeField, Min(1)]
    [InspectorName("Количество UI слотов")]
    [Tooltip("Сколько слотов показывать. Обычно 5.")]
    private int _visibleSlotCount = 5;

    [SerializeField, Min(0)]
    [InspectorName("Первый save slot index")]
    [Tooltip("Внутренний индекс SaveManager для первого UI-слота. Рекомендация: 1, чтобы slot 0 остался под автосейв.")]
    private int _firstSaveSlotIndex = 1;

    [SerializeField]
    [InspectorName("Ограничить числом открытых")]
    [Tooltip("Если включено, первые Unlocked Slot Count будут доступны, остальные станут locked независимо от настройки на Slot View.")]
    private bool _useUnlockedSlotCount = true;

    [SerializeField, Min(0)]
    [InspectorName("Unlocked Slot Count")]
    [Tooltip("Сколько первых слотов открыто. Например 2 = Слот 1 и 2 доступны, Слот 3-5 locked.")]
    private int _unlockedSlotCount = 2;

    [SerializeField]
    [InspectorName("Refresh On Enable")]
    [Tooltip("Обновлять все слоты каждый раз, когда экран включается.")]
    private bool _refreshOnEnable = true;

    [SerializeField]
    [InspectorName("Действие клика по слоту")]
    [Tooltip("Что делает нажатие на саму плашку слота: ничего, загрузить слот или записать слот. Кнопки записи/мусорки работают отдельно.")]
    private StorySaveSlotClickAction _slotClickAction = StorySaveSlotClickAction.LoadSlot;

    [Header("Автосейв")]
    [SerializeField]
    [InspectorName("Первый слот = автосейв")]
    [Tooltip("Если включено, первая плашка читает slot 0 автосейва выбранной истории. Остальные плашки идут как ручные слоты, начиная с Первый save slot index.")]
    private bool _showAutosaveAsFirstSlot;

    [SerializeField]
    [InspectorName("Название автосейва")]
    [Tooltip("Текст первой плашки, когда она работает как автосейв.")]
    private string _autosaveTitleText = "Автосейв";

    [SerializeField]
    [InspectorName("Можно перезаписать автосейв")]
    [Tooltip("Обычно выключено: автосейв пишет сама история. Если включить, кнопка записи сможет вручную записать slot 0.")]
    private bool _autosaveCanBeOverwritten;

    [SerializeField]
    [InspectorName("Можно удалить автосейв")]
    [Tooltip("Обычно выключено: автосейв защищен от мусорки. Если включить, кнопка удаления сможет удалить slot 0 выбранной истории.")]
    private bool _autosaveCanBeDeleted;

    [SerializeField]
    [InspectorName("Сообщение автосейв write")]
    [Tooltip("Сообщение, если игрок пытается вручную перезаписать защищенный автосейв.")]
    private string _autosaveWriteBlockedMessage = "Автосейв перезаписывается автоматически";

    [SerializeField]
    [InspectorName("Сообщение автосейв delete")]
    [Tooltip("Сообщение, если игрок пытается удалить защищенный автосейв.")]
    private string _autosaveDeleteBlockedMessage = "Автосейв нельзя удалить вручную";

    [Header("Менеджеры")]
    [SerializeField]
    [InspectorName("Story Manager")]
    [Tooltip("StoryManager текущей истории. Можно не назначать: контроллер найдет StoryManager.Instance или объект в сцене.")]
    private StoryManager _storyManager;

    [SerializeField]
    [InspectorName("Save Manager")]
    [Tooltip("SaveManager. Можно не назначать: контроллер возьмет SaveManager.Instance или найдет в сцене.")]
    private SaveManager _saveManager;

    [SerializeField]
    [InspectorName("Auto Find Managers")]
    [Tooltip("Автоматически искать StoryManager и SaveManager, если поля пустые.")]
    private bool _autoFindManagers = true;

    [SerializeField]
    [InspectorName("GameData контекст")]
    [Tooltip("Необязательный GameData текущей истории. Нужен в основном для обложки в заполненных слотах, если хочешь показывать GameIcon. Если заполнен, экран будет читать слоты именно этой истории.")]
    private GameData _storyContextData;

    [SerializeField]
    [InspectorName("Выбирать GameData перед загрузкой")]
    [Tooltip("Если экран открыт из меню, перед загрузкой слота вызвать StoryManager.SelectStory(GameData.Story). Внутри самой истории можно оставить включенным, оно не мешает.")]
    private bool _selectStoryContextBeforeLoad = true;

    [Header("Запись слота")]
    [SerializeField]
    [InspectorName("Запись только в активной истории")]
    [Tooltip("Если включено, кнопки карандаша работают только когда уже открыт Story screen, выбрана эта же история и есть текущая нода для сохранения. На History screen запись будет скрыта/заблокирована, чтобы не ловить ошибку SelectStory.")]
    private bool _writeRequiresActiveStory = true;

    [Header("Выбор слота")]
    [SerializeField]
    [InspectorName("Пустой слот выбирает цель сохранения")]
    [Tooltip("Если включено, клик по пустому открытому слоту не показывает ошибку, а запоминает этот слот как цель будущего автосохранения выбранной истории.")]
    private bool _selectEmptySlotAsSaveTarget = true;

    [SerializeField]
    [InspectorName("Показывать текст выбранного пустого")]
    [Tooltip("Если включено, у пустого выбранного слота Details Text заменится на Selected Empty Details Text.")]
    private bool _showSelectedDetailsForEmptySlots = true;

    [SerializeField]
    [InspectorName("Selected Empty Details Text")]
    [Tooltip("Текст Details для пустого слота, который выбран как цель будущего сохранения.")]
    private string _selectedEmptyDetailsText = "Выбран для сохранения";

    [SerializeField]
    [InspectorName("Selected Empty Message")]
    [Tooltip("Сообщение после выбора пустого слота как цели будущего сохранения. {0} = номер UI-слота, {1} = внутренний save slot index.")]
    private string _selectedEmptySlotMessageFormat = "Слот {0} выбран для сохранения";

    [SerializeField]
    [InspectorName("Selected Filled Message")]
    [Tooltip("Сообщение после выбора занятого слота как активного слота. {0} = номер UI-слота, {1} = внутренний save slot index.")]
    private string _selectedFilledSlotMessageFormat = "Слот {0} выбран";

    [SerializeField]
    [InspectorName("Select Failed Message")]
    [Tooltip("Сообщение, если слот нельзя выбрать, потому что экран не знает, к какой истории он относится.")]
    private string _selectSlotFailedMessage = "Сначала выберите историю";
    [Header("Загрузка слота")]

    [SerializeField]
    [InspectorName("Screen Navigator")]
    [Tooltip("StoryScreenNavigator, который должен открыть Story screen после успешной загрузки слота. Можно оставить пустым: контроллер найдет его в родителях или сцене.")]
    private StoryScreenNavigator _screenNavigator;

    [SerializeField]
    [InspectorName("Auto Find Navigator")]
    [Tooltip("Автоматически искать StoryScreenNavigator, если поле Screen Navigator пустое.")]
    private bool _autoFindScreenNavigator = true;

    [SerializeField]
    [InspectorName("Открыть Story после загрузки")]
    [Tooltip("Если включено, после успешной загрузки слота экран сохранений сразу переключит навигатор на Story screen.")]
    private bool _openStoryScreenAfterLoad = true;

    [SerializeField]
    [InspectorName("Story Screen ID")]
    [Tooltip("Screen ID игрового экрана истории. Обычно Story.")]
    private string _storyScreenId = "Story";

    [Header("Тексты слотов")]
    [SerializeField]
    [InspectorName("Filled Title Format")]
    [Tooltip("Формат заголовка занятого слота. {0} = номер UI-слота, {1} = внутренний save slot index.")]
    private string _filledTitleFormat = "Слот {0}";

    [SerializeField]
    [InspectorName("Empty Title Format")]
    [Tooltip("Формат заголовка пустого слота. {0} = номер UI-слота, {1} = внутренний save slot index. По макету можно оставить '(Пустой)'.")]
    private string _emptyTitleFormat = "(Пустой)";

    [SerializeField]
    [InspectorName("Locked Title Format")]
    [Tooltip("Формат заголовка locked-слота. {0} = номер UI-слота, {1} = внутренний save slot index.")]
    private string _lockedTitleFormat = "Слот {0}";

    [SerializeField]
    [InspectorName("Filled Details Format")]
    [Tooltip("Формат дополнительного текста занятого слота. {0} = дата, {1} = episodeId, {2} = chapterId, {3} = nodeGuid. Можно оставить пустым.")]
    private string _filledDetailsFormat = "{0}";

    [SerializeField]
    [InspectorName("Empty Details Text")]
    [Tooltip("Дополнительный текст пустого слота. Можно оставить пустым.")]
    private string _emptyDetailsText = "";

    [SerializeField]
    [InspectorName("Locked Details Text")]
    [Tooltip("Дополнительный текст locked-слота. Можно оставить пустым, если замка достаточно.")]
    private string _lockedDetailsText = "";

    [SerializeField]
    [InspectorName("Date Format")]
    [Tooltip("Формат даты сохранения для дополнительного текста. Например: dd.MM.yyyy HH:mm.")]
    private string _dateFormat = "dd.MM.yyyy HH:mm";

    [SerializeField]
    [InspectorName("No Date Text")]
    [Tooltip("Текст вместо даты, если у старого сохранения нет savedAtIso.")]
    private string _noDateText = "Без даты";

    [Header("Изображения")]
    [SerializeField]
    [InspectorName("Story Cover For Filled")]
    [Tooltip("Если включено, занятые слоты будут получать обложку GameData.GameIcon в Preview Image.")]
    private bool _useStoryCoverForFilledSlots = true;

    [SerializeField]
    [InspectorName("Fallback Cover")]
    [Tooltip("Запасной спрайт для занятого слота, если GameData.GameIcon не назначен.")]
    private Sprite _fallbackFilledSprite;

    [Header("Сообщения")]
    [SerializeField]
    [InspectorName("Show Toast Messages")]
    [Tooltip("Показывать системные сообщения через ToastManager после записи, удаления, загрузки или ошибки.")]
    private bool _showToastMessages = true;

    [SerializeField]
    [InspectorName("Saved Message")]
    [Tooltip("Сообщение после записи. {0} = номер UI-слота.")]
    private string _savedMessageFormat = "Слот {0} перезаписан";

    [SerializeField]
    [InspectorName("Deleted Message")]
    [Tooltip("Сообщение после удаления. {0} = номер UI-слота.")]
    private string _deletedMessageFormat = "Слот {0} удалён";

    [SerializeField]
    [InspectorName("Loaded Message")]
    [Tooltip("Сообщение после успешной загрузки. {0} = номер UI-слота.")]
    private string _loadedMessageFormat = "Слот {0} загружен";

    [SerializeField]
    [InspectorName("Empty Slot Message")]
    [Tooltip("Сообщение при попытке загрузить пустой слот. {0} = номер UI-слота.")]
    private string _emptySlotMessageFormat = "Слот {0} пуст";

    [SerializeField]
    [InspectorName("Locked Slot Message")]
    [Tooltip("Сообщение при клике по locked-слоту. {0} = номер UI-слота.")]
    private string _lockedSlotMessageFormat = "Слот {0} заблокирован";

    [SerializeField]
    [InspectorName("Save Failed Message")]
    [Tooltip("Сообщение, если текущий прогресс нельзя сохранить.")]
    private string _saveFailedMessage = "Сейчас нечего сохранить";

    [Header("События")]
    [SerializeField]
    [InspectorName("Slot Saved")]
    [Tooltip("Вызывается после успешной записи слота. int = номер UI-слота.")]
    private StorySaveSlotIntEvent _slotSaved = new StorySaveSlotIntEvent();

    [SerializeField]
    [InspectorName("Slot Deleted")]
    [Tooltip("Вызывается после удаления слота. int = номер UI-слота.")]
    private StorySaveSlotIntEvent _slotDeleted = new StorySaveSlotIntEvent();

    [SerializeField]
    [InspectorName("Slot Loaded")]
    [Tooltip("Вызывается после успешной загрузки слота. int = номер UI-слота.")]
    private StorySaveSlotIntEvent _slotLoaded = new StorySaveSlotIntEvent();

    [SerializeField]
    [InspectorName("Slot Selected")]
    [Tooltip("Вызывается после выбора слота как активного слота будущего сохранения. int = номер UI-слота.")]
    private StorySaveSlotIntEvent _slotSelected = new StorySaveSlotIntEvent();

    [SerializeField]
    [InspectorName("Message")]
    [Tooltip("Вызывается каждый раз, когда контроллер хочет показать сообщение. string = текст сообщения.")]
    private StorySaveSlotMessageEvent _message = new StorySaveSlotMessageEvent();

    private enum SaveSlotOperation
    {
        Load,
        Save,
        Delete,
        Select
    }

    private void OnEnable()
    {
        BindViews();
        if (_refreshOnEnable)
            RefreshSlots();
    }

    private void OnValidate()
    {
        _visibleSlotCount = Mathf.Max(1, _visibleSlotCount);
        _firstSaveSlotIndex = Mathf.Max(0, _firstSaveSlotIndex);
        _unlockedSlotCount = Mathf.Max(0, _unlockedSlotCount);
        _storyScreenId = UIScreenState.NormalizeScreenId(_storyScreenId);
    }

    [ContextMenu("Обновить слоты")]
    public void RefreshSlots()
    {
        BindViews();
        ApplyTitle();

        StorySaveSlotView[] views = ResolveSlotViews();
        int count = Mathf.Min(_visibleSlotCount, views.Length);
        for (int i = 0; i < count; i++)
        {
            StorySaveSlotView view = views[i];
            if (view == null)
                continue;

            int slotNumber = i + 1;
            view.Bind(this, slotNumber);
            view.Refresh(BuildSlotInfo(view, slotNumber, i));
        }
    }

    public void SaveToSlotNumber(int slotNumber)
    {
        if (!CanUseSlot(slotNumber, SaveSlotOperation.Save, out int saveSlotIndex))
            return;

        StoryManager storyManager = ResolveStoryManager();
        SaveManager saveManager = ResolveSaveManager();
        bool saved = false;

        if (storyManager != null)
            saved = storyManager.TrySaveCurrentToSlot(saveSlotIndex);
        else if (saveManager != null)
        {
            SaveData data = saveManager.SaveCurrentData(saveSlotIndex, storyManager);
            saved = data != null && data.HasPosition;
        }

        if (!saved)
        {
            ShowMessage(_saveFailedMessage);
            return;
        }

        StorySaveSlotSelection.SelectSlot(ResolveStoryId(), saveSlotIndex);
        RefreshSlots();
        ShowMessage(Format(_savedMessageFormat, slotNumber, saveSlotIndex));
        _slotSaved.Invoke(slotNumber);
    }

    public void DeleteSlotNumber(int slotNumber)
    {
        if (!CanUseSlot(slotNumber, SaveSlotOperation.Delete, out int saveSlotIndex))
            return;

        SaveManager saveManager = ResolveSaveManager();
        if (saveManager == null)
            return;

        string storyId = ResolveStoryId();
        if (string.IsNullOrEmpty(storyId))
            saveManager.Delete(saveSlotIndex);
        else
            saveManager.DeleteForStory(storyId, saveSlotIndex);

        if (StorySaveSlotSelection.IsSelectedSlot(storyId, saveSlotIndex))
            StorySaveSlotSelection.ClearSelectedSlot(storyId);

        RefreshSlots();
        ShowMessage(Format(_deletedMessageFormat, slotNumber, saveSlotIndex));
        _slotDeleted.Invoke(slotNumber);
    }

    public void LoadSlotNumber(int slotNumber)
    {
        if (!CanUseSlot(slotNumber, SaveSlotOperation.Load, out int saveSlotIndex))
            return;

        SaveData save = LoadSlotData(saveSlotIndex);
        if (save == null || !save.HasPosition)
        {
            if (CanSaveCurrentProgress())
            {
                SaveToSlotNumber(slotNumber);
                return;
            }

            StartEmptySlotNumber(slotNumber, saveSlotIndex);
            return;
        }

        StoryManager storyManager = ResolveStoryManager();
        bool loaded = EnsureStorySelectedForLoad(storyManager) && storyManager.TryLoadSaveSlot(saveSlotIndex);
        if (!loaded)
        {
            ShowMessage(Format(_emptySlotMessageFormat, slotNumber, saveSlotIndex));
            return;
        }

        StorySaveSlotSelection.SelectSlot(ResolveStoryId(), saveSlotIndex);
        RefreshSlots();
        OpenStoryScreenAfterLoad();
        ShowMessage(Format(_loadedMessageFormat, slotNumber, saveSlotIndex));
        _slotLoaded.Invoke(slotNumber);
    }

    public void SelectSlotNumber(int slotNumber)
    {
        if (!CanUseSlot(slotNumber, SaveSlotOperation.Select, out int saveSlotIndex))
            return;

        string storyId = ResolveStoryId();
        if (string.IsNullOrEmpty(storyId))
        {
            ShowMessage(_selectSlotFailedMessage);
            return;
        }

        SaveData save = LoadSlotData(saveSlotIndex);
        bool hasSave = save != null && save.HasPosition;
        StorySaveSlotSelection.SelectSlot(storyId, saveSlotIndex);
        RefreshSlots();
        ShowMessage(Format(hasSave ? _selectedFilledSlotMessageFormat : _selectedEmptySlotMessageFormat, slotNumber, saveSlotIndex));
        _slotSelected.Invoke(slotNumber);
    }

    public void HandleSlotClicked(StorySaveSlotView view)
    {
        if (view == null)
            return;

        switch (_slotClickAction)
        {
            case StorySaveSlotClickAction.LoadSlot:
                LoadSlotNumber(view.SlotNumber);
                break;
            case StorySaveSlotClickAction.SaveSlot:
                SaveToSlotNumber(view.SlotNumber);
                break;
        }
    }

    public void SetStoryContext(GameData storyContextData)
    {
        _storyContextData = storyContextData;
        RefreshSlots();
    }

    private void BindViews()
    {
        StorySaveSlotView[] views = ResolveSlotViews();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null)
                views[i].Bind(this, i + 1);
        }
    }

    private StorySaveSlotView[] ResolveSlotViews()
    {
        if ((_slotViews == null || _slotViews.Length == 0) && _autoFindSlotViews)
            _slotViews = GetComponentsInChildren<StorySaveSlotView>(true);

        return _slotViews ?? Array.Empty<StorySaveSlotView>();
    }

    private StorySaveSlotInfo BuildSlotInfo(StorySaveSlotView view, int slotNumber, int viewIndex)
    {
        int saveSlotIndex = ToSaveSlotIndex(slotNumber);
        bool locked = IsLocked(view, viewIndex);
        bool isAutosave = IsAutosaveSlot(slotNumber);
        SaveData save = locked ? null : LoadSlotData(saveSlotIndex);
        bool hasSave = save != null && save.HasPosition;
        bool canSaveCurrent = CanSaveCurrentProgress();
        bool canWrite = !locked && canSaveCurrent && (!isAutosave || _autosaveCanBeOverwritten);
        bool canDelete = !locked && hasSave && (!isAutosave || _autosaveCanBeDeleted);
        string storyId = ResolveStoryId();
        bool isSelected = !locked && StorySaveSlotSelection.IsSelectedSlot(storyId, saveSlotIndex);

        return new StorySaveSlotInfo
        {
            SlotNumber = slotNumber,
            SaveSlotIndex = saveSlotIndex,
            Locked = locked,
            HasSave = hasSave,
            IsAutosave = isAutosave,
            IsSelected = isSelected,
            CanClickSlot = CanClickSlot(locked, hasSave, canWrite),
            CanWrite = canWrite,
            CanDelete = canDelete,
            ShowActions = canWrite || canDelete,
            CanUseSlotClickWithoutSave = _slotClickAction == StorySaveSlotClickAction.SaveSlot && canWrite,
            SaveData = save,
            SavedAtIso = save != null ? save.savedAtIso : "",
            Title = BuildTitle(slotNumber, saveSlotIndex, locked, hasSave, isAutosave),
            Details = BuildDetails(save, locked, hasSave, isSelected),
            PreviewSprite = ResolvePreviewSprite(hasSave)
        };
    }

    private bool CanUseSlot(int slotNumber, SaveSlotOperation operation, out int saveSlotIndex)
    {
        saveSlotIndex = ToSaveSlotIndex(slotNumber);
        if (slotNumber <= 0)
            return false;

        StorySaveSlotView view = FindView(slotNumber);
        int viewIndex = Mathf.Max(0, slotNumber - 1);
        if (IsLocked(view, viewIndex))
        {
            ShowMessage(Format(_lockedSlotMessageFormat, slotNumber, saveSlotIndex));
            return false;
        }

        bool isAutosave = IsAutosaveSlot(slotNumber);
        if (operation == SaveSlotOperation.Save && !CanSaveCurrentProgress())
        {
            ShowMessage(_saveFailedMessage);
            return false;
        }

        if (isAutosave && operation == SaveSlotOperation.Save && !_autosaveCanBeOverwritten)
        {
            ShowMessage(_autosaveWriteBlockedMessage);
            return false;
        }

        if (isAutosave && operation == SaveSlotOperation.Delete && !_autosaveCanBeDeleted)
        {
            ShowMessage(_autosaveDeleteBlockedMessage);
            return false;
        }

        return true;
    }

    private StorySaveSlotView FindView(int slotNumber)
    {
        StorySaveSlotView[] views = ResolveSlotViews();
        int index = slotNumber - 1;
        if (index >= 0 && index < views.Length)
            return views[index];

        return null;
    }

    private void StartEmptySlotNumber(int slotNumber, int saveSlotIndex)
    {
        string storyId = ResolveStoryId();
        if (string.IsNullOrEmpty(storyId))
        {
            ShowMessage(_selectSlotFailedMessage);
            return;
        }

        StorySaveSlotSelection.SelectSlot(storyId, saveSlotIndex);

        StoryManager storyManager = ResolveStoryManager();
        bool started = EnsureStorySelectedForLoad(storyManager) && storyManager.StartFreshFromSaveSlot(saveSlotIndex);
        RefreshSlots();

        if (!started)
        {
            ShowMessage(Format(_selectedEmptySlotMessageFormat, slotNumber, saveSlotIndex));
            _slotSelected.Invoke(slotNumber);
            return;
        }

        OpenStoryScreenAfterLoad();
        ShowMessage(Format(_selectedEmptySlotMessageFormat, slotNumber, saveSlotIndex));
        _slotLoaded.Invoke(slotNumber);
    }

    private bool IsLocked(StorySaveSlotView view, int viewIndex)
    {
        if (_useUnlockedSlotCount && viewIndex >= _unlockedSlotCount)
            return true;

        return view != null && view.Locked;
    }

    private bool IsAutosaveSlot(int slotNumber)
    {
        return _showAutosaveAsFirstSlot && slotNumber == 1;
    }

    private bool CanSaveCurrentProgress()
    {
        if (!_writeRequiresActiveStory)
            return true;

        StoryManager storyManager = ResolveStoryManager();
        if (storyManager == null || !storyManager.HasSelectedStory)
            return false;

        if (GameState.Instance == null || GameState.Instance.currentNode == null)
            return false;

        string contextStoryId = ResolveStoryContextId();
        if (!string.IsNullOrEmpty(contextStoryId))
        {
            string activeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyManager.CurrentStoryId);
            if (!string.Equals(activeStoryId, contextStoryId, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private bool CanClickSlot(bool locked, bool hasSave, bool canWrite)
    {
        if (locked)
            return false;

        switch (_slotClickAction)
        {
            case StorySaveSlotClickAction.LoadSlot:
                return hasSave || _selectEmptySlotAsSaveTarget;
            case StorySaveSlotClickAction.SaveSlot:
                return canWrite;
            default:
                return false;
        }
    }

    private int ToSaveSlotIndex(int slotNumber)
    {
        if (IsAutosaveSlot(slotNumber))
            return 0;

        int manualSlotNumber = slotNumber - (_showAutosaveAsFirstSlot ? 1 : 0);
        return _firstSaveSlotIndex + Mathf.Max(0, manualSlotNumber - 1);
    }

    private SaveData LoadSlotData(int saveSlotIndex)
    {
        SaveManager saveManager = ResolveSaveManager();
        if (saveManager == null)
            return null;

        return saveManager.LoadForStorySlotIfExists(ResolveStoryId(), saveSlotIndex);
    }

    private string BuildTitle(int slotNumber, int saveSlotIndex, bool locked, bool hasSave, bool isAutosave)
    {
        if (isAutosave)
            return string.IsNullOrWhiteSpace(_autosaveTitleText) ? "Автосейв" : _autosaveTitleText;

        if (locked)
            return Format(_lockedTitleFormat, slotNumber, saveSlotIndex);

        return hasSave
            ? Format(_filledTitleFormat, slotNumber, saveSlotIndex)
            : Format(_emptyTitleFormat, slotNumber, saveSlotIndex);
    }

    private string BuildDetails(SaveData save, bool locked, bool hasSave, bool isSelected)
    {
        if (locked)
            return _lockedDetailsText ?? "";
        if (!hasSave || save == null)
        {
            if (isSelected && _showSelectedDetailsForEmptySlots)
                return _selectedEmptyDetailsText ?? "";

            return _emptyDetailsText ?? "";
        }

        string savedAt = FormatSavedAt(save.savedAtIso);
        return Format(_filledDetailsFormat, savedAt, save.episodeId ?? "", save.chapterId ?? "", save.currentNodeGuid ?? "");
    }

    private string FormatSavedAt(string savedAtIso)
    {
        if (string.IsNullOrWhiteSpace(savedAtIso))
            return _noDateText;

        if (DateTime.TryParse(savedAtIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime savedAt))
        {
            string format = string.IsNullOrWhiteSpace(_dateFormat) ? "dd.MM.yyyy HH:mm" : _dateFormat;
            return savedAt.ToLocalTime().ToString(format);
        }

        return _noDateText;
    }

    private Sprite ResolvePreviewSprite(bool hasSave)
    {
        if (!hasSave)
            return null;

        if (_useStoryCoverForFilledSlots && _storyContextData != null && _storyContextData.GameIcon != null)
            return _storyContextData.GameIcon;

        return _fallbackFilledSprite;
    }

    private string ResolveStoryId()
    {
        string contextStoryId = ResolveStoryContextId();
        if (!string.IsNullOrEmpty(contextStoryId))
            return contextStoryId;

        StoryManager storyManager = ResolveStoryManager();
        if (storyManager != null && !string.IsNullOrEmpty(storyManager.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(storyManager.CurrentStoryId);

        if (GameState.Instance != null)
            return SaveDataSanitizer.SanitizeIdentifier(GameState.Instance.CurrentStoryId);

        return "";
    }

    private string ResolveStoryContextId()
    {
        if (_storyContextData == null || _storyContextData.Story == null)
            return "";

        string storyId = SaveDataSanitizer.SanitizeIdentifier(_storyContextData.Story.StoryId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        storyId = SaveDataSanitizer.SanitizeIdentifier(_storyContextData.Story.storyId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        return SaveDataSanitizer.SanitizeIdentifier(_storyContextData.Story.name);
    }

    private bool EnsureStorySelectedForLoad(StoryManager storyManager)
    {
        if (storyManager == null)
            return false;

        if (!_selectStoryContextBeforeLoad || _storyContextData == null || _storyContextData.Story == null)
            return storyManager.HasSelectedStory;

        string contextStoryId = ResolveStoryContextId();
        string currentStoryId = SaveDataSanitizer.SanitizeIdentifier(storyManager.CurrentStoryId);
        if (storyManager.HasSelectedStory &&
            (string.IsNullOrEmpty(contextStoryId) || string.Equals(currentStoryId, contextStoryId, StringComparison.Ordinal)))
        {
            return true;
        }

        return storyManager.SelectStory(_storyContextData.Story);
    }

    private void OpenStoryScreenAfterLoad()
    {
        if (!_openStoryScreenAfterLoad)
            return;

        StoryScreenNavigator navigator = ResolveScreenNavigator();
        if (navigator == null)
            return;

        string storyScreenId = UIScreenState.NormalizeScreenId(_storyScreenId);
        if (!string.IsNullOrEmpty(storyScreenId))
            navigator.OpenScreen(storyScreenId);
        else
            navigator.ShowStoryScreen();
    }

    private StoryScreenNavigator ResolveScreenNavigator()
    {
        if (_screenNavigator == null && _autoFindScreenNavigator)
            _screenNavigator = GetComponentInParent<StoryScreenNavigator>(true) ?? FindObjectOfType<StoryScreenNavigator>(true);

        return _screenNavigator;
    }

    private StoryManager ResolveStoryManager()
    {
        if (_storyManager == null && _autoFindManagers)
            _storyManager = StoryManager.Instance != null ? StoryManager.Instance : FindObjectOfType<StoryManager>(true);

        return _storyManager;
    }

    private SaveManager ResolveSaveManager()
    {
        if (_saveManager == null && _autoFindManagers)
            _saveManager = SaveManager.Instance != null ? SaveManager.Instance : FindObjectOfType<SaveManager>(true);

        return _saveManager;
    }

    private void ApplyTitle()
    {
        if (_writeTitleText && _titleText != null)
            _titleText.text = _titleTextValue ?? "";
    }

    private void ShowMessage(string message)
    {
        message ??= "";
        _message.Invoke(message);

        if (_showToastMessages && !string.IsNullOrWhiteSpace(message))
            ToastManager.Instance?.ShowSystemMessage(message);
    }

    private static string Format(string format, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "";

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
