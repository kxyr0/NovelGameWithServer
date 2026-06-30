using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterProfileData
{
    public string PlayerName = HeroCustomizationStore.DefaultPlayerName;
    public string StoryId = "";
    public string Source = "fallback";
    public bool UsedFallback;
    public bool IsCustomName;
}

public static class CharacterProfileService
{
    static string _lastLogSignature;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _lastLogSignature = "";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadProfileBeforeScene()
    {
        try
        {
            HeroCustomizationState state = HeroCustomizationStore.Load();
            if (state != null)
                PlayerAppearance.ApplyState(state, save: false, notify: false);

            LogProfileSnapshot("before-scene-load", "", state != null ? state.playerName : "");
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.Diagnostics,
                nameof(CharacterProfileService),
                nameof(LoadProfileBeforeScene),
                "Failed to load character profile before scene load.",
                LogMetadata.Of("exceptionType", exception.GetType().Name),
                recoverable: true);
        }
    }

    public static string SaveSelectedPlayerName(string name, string storyId = "", string source = "")
    {
        string safeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string safeName = HeroCustomizationState.NormalizePlayerName(name);

        HeroCustomizationState state = PlayerAppearance.CaptureState();
        state.playerName = safeName;
        HeroCustomizationStore.Save(state);

        if (!string.IsNullOrEmpty(safeStoryId))
            HeroCustomizationStore.SavePlayerNameForStory(safeStoryId, safeName);

        PlayerAppearance.SetPlayerName(safeName);
        LogProfileSnapshot(string.IsNullOrWhiteSpace(source) ? "save-selected-name" : source, safeStoryId, safeName);
        return safeName;
    }

    public static CharacterProfileData ResolvePlayerName(
        string storyId,
        string storyDefaultName = "",
        string source = "")
    {
        string safeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string safeStoryDefault = SaveDataSanitizer.SanitizePlayerName(storyDefaultName);

        if (TryResolveSavedOrActivePlayerName(safeStoryId, safeStoryDefault, out string playerName, out string resolvedSource))
            return CreateProfile(playerName, safeStoryId, resolvedSource, false);

        if (IsDisplayPlayerName(safeStoryDefault))
            return CreateProfile(HeroCustomizationState.NormalizePlayerName(safeStoryDefault), safeStoryId, "story-default", false);

        CharacterProfileData profile = CreateProfile(HeroCustomizationStore.DefaultPlayerName, safeStoryId, "fallback", true);
        LogProfileSnapshot(string.IsNullOrWhiteSpace(source) ? "resolve-fallback" : source, safeStoryId, profile.PlayerName);
        return profile;
    }

    public static bool TryResolveSavedOrActivePlayerName(
        string storyId,
        string storyDefaultName,
        out string playerName,
        out string source)
    {
        playerName = "";
        source = "";
        string safeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string safeStoryDefault = SaveDataSanitizer.SanitizePlayerName(storyDefaultName);

        if (!string.IsNullOrEmpty(safeStoryId) &&
            TryLoadStoryName(safeStoryId, out playerName))
        {
            source = "story-save";
            return true;
        }

        HeroCustomizationState state = null;
        try
        {
            state = HeroCustomizationStore.Load();
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.Diagnostics,
                nameof(CharacterProfileService),
                nameof(TryResolveSavedOrActivePlayerName),
                "Failed to load global character profile. Active profile will be checked.",
                LogMetadata.Of("storyId", safeStoryId, "exceptionType", exception.GetType().Name),
                recoverable: true);
        }

        string globalName = state != null ? SaveDataSanitizer.SanitizePlayerName(state.playerName) : "";
        if (IsSelectableName(globalName) && !NamesEqual(globalName, safeStoryDefault))
        {
            playerName = HeroCustomizationState.NormalizePlayerName(globalName);
            source = "global-save";
            return true;
        }

        string activeName = SaveDataSanitizer.SanitizePlayerName(PlayerAppearance.PlayerName);
        if (IsSelectableName(activeName) && !NamesEqual(activeName, safeStoryDefault))
        {
            playerName = HeroCustomizationState.NormalizePlayerName(activeName);
            source = "active-profile";
            return true;
        }

        if (IsSelectableName(globalName))
        {
            playerName = HeroCustomizationState.NormalizePlayerName(globalName);
            source = "global-save";
            return true;
        }

        if (IsSelectableName(activeName))
        {
            playerName = HeroCustomizationState.NormalizePlayerName(activeName);
            source = "active-profile";
            return true;
        }

        return false;
    }

    public static string BuildDebugSummary(string storyId = "", string storyDefaultName = "")
    {
        CharacterProfileData profile = ResolvePlayerName(storyId, storyDefaultName, "debug-summary");
        HeroCustomizationState state = null;
        try
        {
            state = HeroCustomizationStore.Load();
        }
        catch (Exception)
        {
        }

        string safeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string storyName = "";
        if (!string.IsNullOrEmpty(safeStoryId))
            HeroCustomizationStore.TryLoadPlayerNameForStory(safeStoryId, out storyName);

        return "profileName=" + profile.PlayerName +
               " source=" + profile.Source +
               " storyId=" + safeStoryId +
               " storyStored=" + storyName +
               " globalStored=" + (state != null ? state.playerName : "") +
               " active=" + PlayerAppearance.PlayerName;
    }

    static bool TryLoadStoryName(string storyId, out string playerName)
    {
        playerName = "";
        try
        {
            if (!HeroCustomizationStore.TryLoadPlayerNameForStory(storyId, out string loadedName))
                return false;

            playerName = HeroCustomizationState.NormalizePlayerName(loadedName);
            return IsSelectableName(playerName);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.Diagnostics,
                nameof(CharacterProfileService),
                nameof(TryLoadStoryName),
                "Failed to load story-scoped character profile name.",
                LogMetadata.Of("storyId", storyId, "exceptionType", exception.GetType().Name),
                recoverable: true);
            return false;
        }
    }

    static CharacterProfileData CreateProfile(string playerName, string storyId, string source, bool usedFallback)
    {
        string safeName = HeroCustomizationState.NormalizePlayerName(playerName);
        return new CharacterProfileData
        {
            PlayerName = safeName,
            StoryId = SaveDataSanitizer.SanitizeIdentifier(storyId),
            Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source,
            UsedFallback = usedFallback,
            IsCustomName = IsSelectableName(safeName)
        };
    }

    static bool IsDisplayPlayerName(string value)
    {
        string name = SaveDataSanitizer.SanitizePlayerName(value);
        return !string.IsNullOrWhiteSpace(name) &&
               !DialogueVariableResolver.IsPlayerNameToken(name);
    }

    static bool IsSelectableName(string value)
    {
        return IsDisplayPlayerName(value) &&
               HeroCustomizationStore.IsCustomPlayerName(value);
    }

    static bool NamesEqual(string left, string right)
    {
        left = SaveDataSanitizer.SanitizePlayerName(left);
        right = SaveDataSanitizer.SanitizePlayerName(right);
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static void LogProfileSnapshot(string source, string storyId, string playerName)
    {
        string safeStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        string safeName = HeroCustomizationState.NormalizePlayerName(playerName);
        string signature = source + "|" + safeStoryId + "|" + safeName + "|" + PlayerAppearance.PlayerName;
        if (string.Equals(signature, _lastLogSignature, StringComparison.Ordinal))
            return;

        _lastLogSignature = signature;

        IDictionary<string, object> metadata = LogMetadata.Of(
            "storyId", safeStoryId,
            "playerName", safeName,
            "activeName", PlayerAppearance.PlayerName,
            "hasGlobalName", HeroCustomizationStore.HasStoredPlayerName(),
            "hasStoryName", !string.IsNullOrEmpty(safeStoryId) && HeroCustomizationStore.HasStoredPlayerNameForStory(safeStoryId));

        AppLogger.DebugLog(
            AppLogCategory.Diagnostics,
            nameof(CharacterProfileService),
            source,
            "Character profile snapshot.",
            metadata);
    }
}
