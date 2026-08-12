using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/History/Story History Utility Navigation")]
public sealed class StoryHistoryUtilityNavigation : MonoBehaviour
{
    [SerializeField]
    private StoryRetryScreen retryScreen;

    [SerializeField]
    private StoryInfoScreen infoScreen;

    [SerializeField]
    private StorySavesScreen savesScreen;

    [Header("Buttons")]
    [SerializeField] private Button savesButton;
    [SerializeField] private Button wardrobeButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button infoButton;

    [Header("Screen IDs")]
    [SerializeField] private string savesScreenId = "Saves";
    [SerializeField] private string retryScreenId = "Retry";
    [SerializeField] private string infoScreenId = "Info";
    [SerializeField] private string wardrobeScreenId = "Wardrobe";

    [SerializeField] private MenuController _menuController;
    private GameData _data;

    private void Awake()
    {
        savesButton?.onClick.AddListener(OpenSaves);
        wardrobeButton?.onClick.AddListener(OpenWardrobe);
        retryButton?.onClick.AddListener(OpenRetry);
        infoButton?.onClick.AddListener(OpenInfo);
        RefreshButtonAvailability();
    }

    private void OnDestroy()
    {
        savesButton?.onClick.RemoveListener(OpenSaves);
        wardrobeButton?.onClick.RemoveListener(OpenWardrobe);
        retryButton?.onClick.RemoveListener(OpenRetry);
        infoButton?.onClick.RemoveListener(OpenInfo);
    }

    public void Configure(GameData data, MenuController menuController)
    {
        _data = data;
        _menuController = menuController;
        RefreshButtonAvailability();

        if (!HasUsableStoryContext())
        {
            Debug.LogWarning(
                $"[HISTORY][UTILITY_NAV] Disabled story utility navigation. " +
                $"gameData='{(_data != null ? _data.name : "<null>")}' " +
                $"reason='{GetUnavailableReason()}'.",
                this);
        }
    }

    private void OpenSaves()
    {
        if (!CanUseStoryUtilities(nameof(OpenSaves)))
            return;

        if (savesScreen == null)
            savesScreen = FindObjectOfType<StorySavesScreen>(true);

        if (savesScreen == null)
        {
            Debug.LogError("Cannot open Saves: StorySavesScreen is missing.", this);
            return;
        }

        savesScreen.Configure(_data, _menuController);
        OpenScreen(savesScreenId);
    }

    private void OpenWardrobe()
    {
        if (!CanUseStoryUtilities(nameof(OpenWardrobe)))
            return;

        _menuController.OpenWardrobeScreenFor(_data);
        OpenScreen(wardrobeScreenId);
    }

    private void OpenRetry()
    {
        if (!CanUseStoryUtilities(nameof(OpenRetry)))
            return;

        if (retryScreen == null)
        {
            Debug.LogError("Cannot open Retry: StoryRetryScreen is missing.", this);
            return;
        }

        retryScreen.Configure(_data, _menuController);

        CanvasGroup group = retryScreen.gameObject.GetComponent<CanvasGroup>();
        if (group == null)
        {
            Debug.LogError("Cannot open Retry: CanvasGroup is missing.", retryScreen);
            return;
        }

        group.DOFade(1, 0.5f).Complete();
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    private void OpenInfo()
    {
        if (!CanUseStoryUtilities(nameof(OpenInfo)))
            return;

        if (infoScreen == null)
        {
            Debug.LogError("Cannot open Info: StoryInfoScreen is missing.", this);
            return;
        }

        infoScreen.Configure(_data, _menuController);
        OpenScreen(infoScreenId);
    }

    private void OpenScreen(string screenId)
    {
        if (_menuController == null)
            return;

        StoryScreenNavigator navigator = _menuController.ScreenNavigator;
        if (navigator == null)
        {
            Debug.LogError("Cannot open utility screen: navigator is missing.", this);
            return;
        }

        screenId = UIScreenState.NormalizeScreenId(screenId);
        if (screenId.Length == 0)
            return;

        if (!navigator.OpenScreen(screenId))
            Debug.LogWarning($"Cannot open screen '{screenId}'.", this);
    }

    private bool CanUseStoryUtilities(string action)
    {
        if (_menuController != null && HasUsableStoryContext())
            return true;

        Debug.LogWarning(
            $"[HISTORY][UTILITY_NAV_BLOCKED] action='{action}' " +
            $"gameData='{(_data != null ? _data.name : "<null>")}' " +
            $"hasMenuController={_menuController != null} " +
            $"hasStory={_data != null && _data.Story != null} " +
            $"forceComingSoon={_data != null && _data.ForceComingSoon} " +
            $"canStartStory={_data != null && _data.CanStartStory} " +
            $"reason='{GetUnavailableReason()}'.",
            this);

        RefreshButtonAvailability();
        return false;
    }

    /// <summary>
    /// History utility screens are only valid for a real, playable story context.
    /// A card may still be visible in History while its story is missing or explicitly
    /// marked Coming Soon; in that state Saves/Wardrobe/Retry/Info must not be reachable.
    /// </summary>
    private bool HasUsableStoryContext()
    {
        if (_data == null || _data.Story == null)
            return false;

        if (_data.ForceComingSoon)
            return false;

        return _data.CanStartStory;
    }

    private string GetUnavailableReason()
    {
        if (_data == null)
            return "GameData is null";

        if (_data.Story == null)
            return "GameData.Story is null";

        if (_data.ForceComingSoon)
            return "ForceComingSoon is enabled";

        if (!_data.CanStartStory)
            return StoryCatalogRuntimeDiagnostics.DescribeAvailability(_data);

        if (_menuController == null)
            return "MenuController is null";

        return "OK";
    }

    private void RefreshButtonAvailability()
    {
        bool enabled = _menuController != null && HasUsableStoryContext();
        SetInteractable(savesButton, enabled);
        SetInteractable(wardrobeButton, enabled);
        SetInteractable(retryButton, enabled);
        SetInteractable(infoButton, enabled);
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
}
