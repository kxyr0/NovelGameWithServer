using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public partial class StoryManager
{
    void Awake()
    {
        Instance = this;
        RegisterStoryAudioScreenEvents();
        AutoWireSceneReferences();
        CaptureDefaultStoryUserInterfaceReferences();
        ApplyStoryUserInterfaceProfile();
        ValidateRequiredSceneReferences();
    }

    void OnValidate()
    {
        cutsceneImageTextDelay = Mathf.Max(0f, cutsceneImageTextDelay);
        cutsceneBackgroundCameraStrength = Mathf.Clamp01(cutsceneBackgroundCameraStrength);
        cutsceneBackgroundPanDuration = Mathf.Max(0f, cutsceneBackgroundPanDuration);
    }

    void AutoWireSceneReferences()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 1) sfxSource = sources[1];
            else if (sources.Length == 1) sfxSource = sources[0];
        }

        if (characterView == null)
            characterView = GetComponent<CharacterViewManager>();
        if (backgroundView == null)
            backgroundView = GetComponent<BackgroundViewManager>();
        if (dialogueUI == null)
            dialogueUI = GetComponent<DialogueUIManager>();
        if (storyHistory == null)
            storyHistory = GetComponent<StoryHistory>();
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>(true);
        if (menuController == null)
            menuController = FindObjectOfType<MenuController>(true);
        if (shopPanel == null)
        {
            var shop = FindObjectOfType<ShopController>(true);
            if (shop != null) shopPanel = shop.gameObject;
        }
        if (tapHandler == null)
            tapHandler = FindObjectOfType<DialogueTapHandler>(true);
        if (imageOverlay == null)
            imageOverlay = ImageOverlayUI.FindOrCreateRuntimeOverlay();
        if (phoneDialogueUI == null)
            phoneDialogueUI = FindObjectOfType<PhoneDialogueUI>(true);
        if (defaultStoryUserInterface == null)
            defaultStoryUserInterface = FindObjectOfType<StoryUserInterface>(true);
        if (_chapterTitleOverlay == null)
            _chapterTitleOverlay = FindObjectOfType<ChapterTitleOverlay>(true);
        if (_statChangeOverlay == null)
            _statChangeOverlay = FindObjectOfType<StatChangeOverlay>(true);
    }

    void CaptureDefaultStoryUserInterfaceReferences()
    {
        if (defaultDialogueUI == null && dialogueUI != null)
            defaultDialogueUI = dialogueUI;

        if (defaultCutsceneUserInterface == null && cutsceneUserInterface != null)
            defaultCutsceneUserInterface = cutsceneUserInterface;

        if (defaultWardrobePanel == null && dialogueUI != null)
            defaultWardrobePanel = dialogueUI.WardrobePanelObject;

        if (defaultStoryUserInterface == null)
            defaultStoryUserInterface = FindObjectOfType<StoryUserInterface>(true);
    }

    void ApplyStoryUserInterfaceProfile()
    {
        CaptureDefaultStoryUserInterfaceReferences();

        StoryUserInterfaceProfile nextProfile = FindBestStoryUserInterfaceProfile(storyData);
        DialogueUIManager nextDialogueUI = nextProfile != null && nextProfile.DialogueUI != null
            ? nextProfile.DialogueUI
            : defaultDialogueUI;
        DialogueUIManager nextCutsceneUserInterface = nextProfile != null && nextProfile.CutsceneUserInterface != null
            ? nextProfile.CutsceneUserInterface
            : defaultCutsceneUserInterface;
        GameObject nextWardrobePanel = nextProfile != null && nextProfile.WardrobePanel != null
            ? nextProfile.WardrobePanel
            : defaultWardrobePanel;
        StoryUserInterface nextStoryUserInterface = nextProfile != null && nextProfile.StoryUserInterface != null
            ? nextProfile.StoryUserInterface
            : defaultStoryUserInterface;

        if (dialogueUI != null && dialogueUI != nextDialogueUI)
            dialogueUI.ResetStoryUi();

        if (cutsceneUserInterface != null &&
            cutsceneUserInterface != nextCutsceneUserInterface &&
            cutsceneUserInterface != dialogueUI)
        {
            cutsceneUserInterface.ResetStoryUi();
        }

        if (activeStoryUserInterfaceProfile != null && activeStoryUserInterfaceProfile != nextProfile)
            activeStoryUserInterfaceProfile.ApplyObjectToggles(false);

        if (nextDialogueUI != null)
            dialogueUI = nextDialogueUI;
        cutsceneUserInterface = nextCutsceneUserInterface;
        if (dialogueUI != null && nextWardrobePanel != null)
            dialogueUI.SetWardrobePanel(nextWardrobePanel);

        if (nextStoryUserInterface != null)
        {
            nextStoryUserInterface.ApplyPhoneConfiguration(nameof(ApplyStoryUserInterfaceProfile));
            nextStoryUserInterface.ApplyEndScreenConfiguration(nameof(ApplyStoryUserInterfaceProfile));
            PhoneDialogueUI nextPhoneDialogueUI = nextStoryUserInterface.ResolvePhoneDialogueUI();
            if (nextPhoneDialogueUI != null)
                phoneDialogueUI = nextPhoneDialogueUI;
        }

        ApplyStoryUiStyles(nextProfile);

        if (nextProfile != null)
            nextProfile.ApplyObjectToggles(true);

        activeStoryUserInterfaceProfile = nextProfile;
    }

    void ApplyStoryUiStyles(StoryUserInterfaceProfile profile)
    {
        StoryUiStyle style = null;
        Sprite backgroundSprite = null;

        TryResolveStoryUiStyle(profile, out style, out backgroundSprite);

        if (dialogueUI != null)
            dialogueUI.ApplyStoryUiStyle(style, backgroundSprite);

        ApplyStatChangeOverlayStyle(style);
        ApplyChapterTitleOverlayStyle(style);
        ApplyPreStorySetupStyle(style);
        ApplyEndScreenStyle(style);

        style = null;
        backgroundSprite = null;

        TryResolveCutsceneStoryUiStyle(profile, out style, out backgroundSprite);

        if (cutsceneUserInterface != null && cutsceneUserInterface != dialogueUI)
            cutsceneUserInterface.ApplyStoryUiStyle(style, backgroundSprite);
    }

    public bool TryResolveCurrentStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        return TryResolveStoryUiStyle(FindBestStoryUserInterfaceProfile(storyData), out style, out backgroundSprite);
    }

    bool TryResolveStoryUiStyle(
        StoryUserInterfaceProfile profile,
        out StoryUiStyle style,
        out Sprite backgroundSprite)
    {
        bool found = TryGetCatalogStoryUiStyle(out style, out backgroundSprite);
        if (!found && profile != null)
            found = profile.TryGetStoryUiStyle(out style, out backgroundSprite);
        if (!found)
        {
            found = storyData != null && storyData.TryGetStoryUiStyle(out style, out backgroundSprite);
            if (!found)
            {
                StoryJsonAssetLibrary library = GetCurrentStoryJsonAssetLibrary();
                if (library != null)
                    found = library.TryGetStoryUiStyle(out style, out backgroundSprite);
            }
        }

        return found;
    }

    bool TryResolveCutsceneStoryUiStyle(
        StoryUserInterfaceProfile profile,
        out StoryUiStyle style,
        out Sprite backgroundSprite)
    {
        bool found = TryGetCatalogCutsceneStoryUiStyle(out style, out backgroundSprite);
        if (!found && profile != null)
            found = profile.TryGetCutsceneStoryUiStyle(out style, out backgroundSprite);
        if (!found)
        {
            found = storyData != null && storyData.TryGetCutsceneStoryUiStyle(out style, out backgroundSprite);
            if (!found)
            {
                StoryJsonAssetLibrary library = GetCurrentStoryJsonAssetLibrary();
                if (library != null)
                    found = library.TryGetCutsceneStoryUiStyle(out style, out backgroundSprite);
            }
        }

        return found;
    }

    void ApplyStatChangeOverlayStyle(StoryUiStyle style)
    {
        if (_statChangeOverlay == null)
            _statChangeOverlay = FindObjectOfType<StatChangeOverlay>(true);

        if (_statChangeOverlay != null)
            _statChangeOverlay.ApplyStoryUiStyle(style);
    }

    void ApplyChapterTitleOverlayStyle(StoryUiStyle style)
    {
        if (_chapterTitleOverlay == null)
            _chapterTitleOverlay = FindObjectOfType<ChapterTitleOverlay>(true);

        if (_chapterTitleOverlay != null)
            _chapterTitleOverlay.ApplyStoryUiStyle(style);
    }

    void ApplyPreStorySetupStyle(StoryUiStyle style)
    {
        PreStorySetupFlow setupFlow = menuController != null
            ? menuController.PreStorySetupFlow
            : null;

        if (setupFlow == null)
            setupFlow = FindObjectOfType<PreStorySetupFlow>(true);

        if (setupFlow != null)
            setupFlow.ApplyStoryUiStyle(style, CurrentStoryId);
    }

    void ApplyEndScreenStyle(StoryUiStyle style)
    {
        StoryEndScreenController endScreen = null;
        if (endStoryPanel != null)
            endScreen = endStoryPanel.GetComponentInChildren<StoryEndScreenController>(true);
        if (endScreen == null)
            endScreen = FindObjectOfType<StoryEndScreenController>(true);

        if (endScreen != null)
            endScreen.ApplyStoryUiStyle(style, CurrentStoryId, preview: false);
    }

    StoryJsonAssetLibrary GetCurrentStoryJsonAssetLibrary()
    {
        ChapterData chapter = GetCurrentChapterOrNull();
        return chapter != null ? chapter.jsonAssetLibrary : null;
    }

    bool TryGetCatalogStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        if (storyInterfaceStyleCatalog != null)
            return storyInterfaceStyleCatalog.TryGetStoryUiStyle(storyData, CurrentStoryId, out style, out backgroundSprite);

        style = null;
        backgroundSprite = null;
        return false;
    }

    bool TryGetCatalogCutsceneStoryUiStyle(out StoryUiStyle style, out Sprite backgroundSprite)
    {
        if (storyInterfaceStyleCatalog != null)
            return storyInterfaceStyleCatalog.TryGetCutsceneStoryUiStyle(storyData, CurrentStoryId, out style, out backgroundSprite);

        style = null;
        backgroundSprite = null;
        return false;
    }

    StoryUserInterfaceProfile FindBestStoryUserInterfaceProfile(StoryData story)
    {
        if (story == null || storyUserInterfaceProfiles == null)
            return null;

        StoryUserInterfaceProfile bestProfile = null;
        int bestScore = 0;

        for (int i = 0; i < storyUserInterfaceProfiles.Count; i++)
        {
            StoryUserInterfaceProfile profile = storyUserInterfaceProfiles[i];
            if (profile == null)
                continue;

            int score = profile.GetMatchScore(story);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestProfile = profile;
        }

        return bestProfile;
    }

    bool ValidateRequiredSceneReferences()
    {
        bool ok = true;

        if (musicSource == null) { Debug.LogError("[StoryManager] musicSource is not assigned", this); ok = false; }
        if (sfxSource == null) { Debug.LogError("[StoryManager] sfxSource is not assigned", this); ok = false; }
        if (characterView == null) { Debug.LogError("[StoryManager] characterView is not assigned", this); ok = false; }
        if (backgroundView == null) { Debug.LogError("[StoryManager] backgroundView is not assigned", this); ok = false; }
        if (dialogueUI == null) { Debug.LogError("[StoryManager] dialogueUI is not assigned", this); ok = false; }
        if (endStoryPanel == null) { Debug.LogError("[StoryManager] endStoryPanel is not assigned", this); ok = false; }

        return ok;
    }

    bool EnsureDialogueUI(string context)
    {
        if (dialogueUI != null)
            return true;

        AutoWireSceneReferences();
        if (dialogueUI != null)
            return true;

        Debug.LogError($"[StoryManager] DialogueUIManager is required for {context}.", this);
        return false;
    }

    bool EnsureCutsceneUserInterface(string context)
    {
        if (cutsceneUserInterface == null)
        {
            Debug.LogError($"[StoryManager] Cutscene user interface is required for {context}. Assign StoryManager Cutscene User Interface in the Inspector. Runtime cutscene UI fallback is disabled.", this);
            return false;
        }

        if (!cutsceneUserInterface.ValidateCutsceneUserInterface())
        {
            Debug.LogError($"[StoryManager] Cutscene user interface is incomplete for {context}.", cutsceneUserInterface);
            return false;
        }

        return true;
    }

    bool EnsureDialogueInterfaceForActiveNode(string context)
    {
        return activeDialogueNode is CutsceneNode
            ? EnsureCutsceneUserInterface(context)
            : EnsureDialogueUI(context);
    }

    DialogueUIManager GetDialogueInterfaceForNode(DialogueNode node)
    {
        return node is CutsceneNode ? cutsceneUserInterface : dialogueUI;
    }

    void HideCutsceneUserInterfaceIfNeeded(DialogueNode node)
    {
        if (node is CutsceneNode && cutsceneUserInterface != null)
            cutsceneUserInterface.HideDialoguePanelForCutsceneIntro();
    }

    void BeginHeroSetupStoryUiSuppression()
    {
        if (heroSetupStoryUiHidden)
            return;

        heroSetupStoryUiHidden = true;
        heroSetupStoryUiStates.Clear();

        if (dialogueUI == null)
            AutoWireSceneReferences();

        if (dialogueUI == null)
            return;

        dialogueUI.ClearChoices();
        dialogueUI.ClearDialogue();
        HideHeroSetupStoryUiObject(dialogueUI.DialoguePanelObject);
        HideHeroSetupStoryUiObject(dialogueUI.ChoiceContainerObject);
    }

    void HideHeroSetupStoryUiObject(GameObject target)
    {
        if (target == null || HasStoredHeroSetupStoryUiState(target))
            return;

        heroSetupStoryUiStates.Add(new StoryUiActiveState
        {
            Target = target,
            WasActiveSelf = target.activeSelf
        });

        if (target.activeSelf)
            target.SetActive(false);
    }

    bool HasStoredHeroSetupStoryUiState(GameObject target)
    {
        for (int i = 0; i < heroSetupStoryUiStates.Count; i++)
        {
            if (heroSetupStoryUiStates[i] != null && heroSetupStoryUiStates[i].Target == target)
                return true;
        }

        return false;
    }

    void RestoreHeroSetupStoryUiForNode(BaseStoryNode node)
    {
        if (!heroSetupStoryUiHidden || !ShouldRestoreHeroSetupStoryUiForNode(node))
            return;

        RestoreHeroSetupStoryUi();
    }

    bool ShouldRestoreHeroSetupStoryUiForNode(BaseStoryNode node)
    {
        return node is DialogueNode || node is ChoiceNode || node is PremiumNode;
    }

    void RestoreHeroSetupStoryUi()
    {
        if (!heroSetupStoryUiHidden)
            return;

        for (int i = 0; i < heroSetupStoryUiStates.Count; i++)
        {
            StoryUiActiveState state = heroSetupStoryUiStates[i];
            if (state == null || state.Target == null)
                continue;

            if (state.Target.activeSelf != state.WasActiveSelf)
                state.Target.SetActive(state.WasActiveSelf);
        }

        heroSetupStoryUiStates.Clear();
        heroSetupStoryUiHidden = false;
    }

    bool EnsureGameState(string context)
    {
        if (GameState.Instance != null)
            return true;

        Debug.LogError($"[StoryManager] GameState is required for {context}.", this);
        return false;
    }

    void SetPurchaseVisible(bool visible)
    {
        if (purchase != null)
            purchase.gameObject.SetActive(visible);
    }

    void WirePurchaseButton(ChapterData chapter)
    {
        if (purchase == null || chapter == null)
            return;

        purchase.onClick.RemoveAllListeners();
        purchase.onClick.AddListener(() => ShowPurchasePopup(chapter));
    }

}
