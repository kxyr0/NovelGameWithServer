using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public interface IDialogueVariableResolver
{
    string ResolveText(string text, DialogueVariableContext context = null);
    bool TryResolveVariable(string variableName, DialogueVariableContext context, out string value);
}

public sealed class DialogueVariableContext
{
    public readonly string LogCategory;
    public readonly string SourceClass;
    public readonly string StoryId;
    public readonly string ChapterId;
    public readonly GameObject SourceObject;
    public readonly DialogueIdentityResult SpeakerIdentity;

    public DialogueVariableContext(
        string logCategory = AppLogCategory.StoryUi,
        string sourceClass = "",
        GameObject sourceObject = null,
        string storyId = "",
        string chapterId = "",
        DialogueIdentityResult speakerIdentity = null)
    {
        LogCategory = string.IsNullOrWhiteSpace(logCategory) ? AppLogCategory.StoryUi : logCategory.Trim();
        SourceClass = string.IsNullOrWhiteSpace(sourceClass) ? nameof(DialogueVariableResolver) : sourceClass.Trim();
        SourceObject = sourceObject;
        StoryId = storyId ?? "";
        ChapterId = chapterId ?? "";
        SpeakerIdentity = speakerIdentity;
    }

    public static DialogueVariableContext StoryUi(
        string sourceClass = "",
        GameObject sourceObject = null,
        string storyId = "",
        string chapterId = "",
        DialogueIdentityResult speakerIdentity = null)
    {
        return new DialogueVariableContext(AppLogCategory.StoryUi, sourceClass, sourceObject, storyId, chapterId, speakerIdentity);
    }

    public static DialogueVariableContext PhoneDialogue(
        string sourceClass = "",
        GameObject sourceObject = null,
        string storyId = "",
        string chapterId = "",
        DialogueIdentityResult speakerIdentity = null)
    {
        return new DialogueVariableContext(AppLogCategory.PhoneDialogue, sourceClass, sourceObject, storyId, chapterId, speakerIdentity);
    }
}

public static class DialogueVariableResolver
{
    public const string FallbackPlayerName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";

    static readonly IDialogueVariableResolver Resolver = new StoryVariableResolver();

    public static string ResolveText(string text, DialogueVariableContext context = null)
    {
        return Resolver.ResolveText(text, context);
    }

    public static string ResolvePlayerName(DialogueVariableContext context = null)
    {
        string value;
        return Resolver.TryResolveVariable("PlayerName", context, out value) ? value : FallbackPlayerName;
    }

    public static string ResolvePlayerName(DialogueVariableContext context, PlayerNameCase grammaticalCase)
    {
        return StoryVariableResolver.ResolvePlayerName(context, grammaticalCase);
    }

    public static bool IsPlayerNameToken(string value)
    {
        return StoryVariableResolver.IsPlayerNameToken(value);
    }

    public static bool IsPlayerSpeakerName(string value, DialogueVariableContext context = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (IsPlayerNameToken(value))
            return true;

        string normalized = SaveDataSanitizer.SanitizePlayerName(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        string playerName = ResolvePlayerName(context);
        return !string.IsNullOrWhiteSpace(playerName) &&
               string.Equals(normalized, playerName, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class StoryVariableResolver : IDialogueVariableResolver
{
    const string PlayerNameVariable = "PlayerName";
    const string HeroNameVariable = "HeroName";
    const string CharacterNameVariable = "CharacterName";
    const string SpeakerNameVariable = "SpeakerName";
    const string PlayerNameSnakeVariable = "player_name";
    const string HeroNameSnakeVariable = "hero_name";
    const string CharacterNameSnakeVariable = "character_name";
    const string SpeakerNameSnakeVariable = "speaker_name";
    const string RussianNameToken = "\u0418\u041c\u042f";
    const string MojibakeRussianNameToken = "\u0420\u0098\u0420\u045A\u0420\u0407";

    static readonly Regex BracedTokenRegex = new Regex(
        @"\{(?<name>PlayerName|HeroName|CharacterName|SpeakerName|playerName|heroName|characterName|speakerName)(?::(?<case>[^{}\[\]\s:]+))?\}|\[(?<name>player_name|hero_name|character_name|speaker_name|PlayerName|HeroName|CharacterName|SpeakerName)(?::(?<case>[^\[\]{}\s:]+))?\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex BarePlayerNameTokenRegex = new Regex(
        @"(?<![\p{L}\p{Nd}_])(?:NAME|" + RussianNameToken + "|" + MojibakeRussianNameToken + @")(?![\p{L}\p{Nd}_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly HashSet<string> PlayerNameAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PlayerNameVariable,
        HeroNameVariable,
        CharacterNameVariable,
        PlayerNameSnakeVariable,
        HeroNameSnakeVariable,
        CharacterNameSnakeVariable,
        "player",
        "hero",
        "name",
        RussianNameToken,
        MojibakeRussianNameToken
    };

    static readonly HashSet<string> SpeakerNameAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpeakerNameVariable,
        SpeakerNameSnakeVariable,
        "speaker",
        "speakerName",
        "speaker_name",
        "actor",
        "actorName",
        "actor_name"
    };

    static string _lastLogSignature;
    static string _lastCaseWarningSignature;
    static string _lastSpeakerWarningSignature;

    public string ResolveText(string text, DialogueVariableContext context = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (!ContainsDialoguePlaceholder(text))
            return text;

        context ??= DialogueVariableContext.StoryUi();
        string storyId = ResolveStoryId(context);
        string playerName = "";
        string resolved = BracedTokenRegex.Replace(text, match =>
        {
            string variableName = match.Groups["name"].Value;
            PlayerNameCase grammaticalCase = ResolveMatchedPlayerNameCase(match, context, storyId);
            if (IsSpeakerNameVariable(variableName))
                return ResolveSpeakerNameCore(context, grammaticalCase, storyId);

            if (!IsPlayerNameVariable(variableName))
                return match.Value;

            if (string.IsNullOrWhiteSpace(playerName))
                playerName = ResolvePlayerNameCore(context, storyId);
            return ResolvePlayerNameCase(playerName, grammaticalCase, context, storyId);
        });

        if (BarePlayerNameTokenRegex.IsMatch(resolved) && string.IsNullOrWhiteSpace(playerName))
            playerName = ResolvePlayerNameCore(context, storyId);

        return BarePlayerNameTokenRegex.Replace(resolved, playerName);
    }

    public bool TryResolveVariable(string variableName, DialogueVariableContext context, out string value)
    {
        value = "";
        context ??= DialogueVariableContext.StoryUi();
        string storyId = ResolveStoryId(context);

        if (IsSpeakerNameVariable(variableName))
        {
            PlayerNameCase speakerCase = ResolvePlayerNameCaseCode(
                ExtractVariableCaseCode(variableName),
                context,
                storyId);
            value = ResolveSpeakerNameCore(context, speakerCase, storyId);
            return true;
        }

        if (!IsPlayerNameVariable(variableName))
            return false;

        string playerName = ResolvePlayerNameCore(context, storyId);
        PlayerNameCase grammaticalCase = ResolvePlayerNameCaseCode(
            ExtractVariableCaseCode(variableName),
            context,
            storyId);
        value = ResolvePlayerNameCase(playerName, grammaticalCase, context, storyId);
        return true;
    }

    public static bool IsPlayerNameToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return IsPlayerNameVariable(NormalizeVariableName(value));
    }

    static bool ContainsDialoguePlaceholder(string text)
    {
        return BracedTokenRegex.IsMatch(text) || BarePlayerNameTokenRegex.IsMatch(text);
    }

    static bool IsPlayerNameVariable(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;

        return PlayerNameAliases.Contains(NormalizeVariableName(variableName));
    }

    static bool IsSpeakerNameVariable(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;

        return SpeakerNameAliases.Contains(NormalizeVariableName(variableName));
    }

    public static string ResolvePlayerName(DialogueVariableContext context, PlayerNameCase grammaticalCase)
    {
        context ??= DialogueVariableContext.StoryUi();
        string storyId = ResolveStoryId(context);
        string playerName = ResolvePlayerNameCore(context, storyId);
        return ResolvePlayerNameCase(playerName, grammaticalCase, context, storyId);
    }

    static string ResolvePlayerNameCore(DialogueVariableContext context, string storyId = "")
    {
        context ??= DialogueVariableContext.StoryUi();

        if (string.IsNullOrWhiteSpace(storyId))
            storyId = ResolveStoryId(context);
        TryResolveStoryDefaultPlayerName(storyId, out string storyDefaultName);
        CharacterProfileData profile = CharacterProfileService.ResolvePlayerName(
            storyId,
            storyDefaultName,
            nameof(DialogueVariableResolver));
        LogResolvedName(profile.PlayerName, profile.Source, profile.UsedFallback, context, storyId);
        return profile.PlayerName;
    }

    static string ResolveSpeakerNameCore(
        DialogueVariableContext context,
        PlayerNameCase grammaticalCase,
        string storyId)
    {
        context ??= DialogueVariableContext.StoryUi();
        DialogueIdentityResult identity = context.SpeakerIdentity;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return identity.IsDynamicPlayerName
                ? ResolvePlayerNameCase(identity.DisplayName, grammaticalCase, context, storyId)
                : identity.DisplayName;
        }

        LogMissingSpeakerIdentity(context, storyId);
        return DialogueVariableResolver.FallbackPlayerName;
    }

    static string ResolvePlayerNameCase(
        string playerName,
        PlayerNameCase grammaticalCase,
        DialogueVariableContext context,
        string storyId)
    {
        if (grammaticalCase == PlayerNameCase.Nominative)
            return playerName;

        PlayerNameCaseForms overrides = TryResolveStoryDefaultPlayerNameCaseForms(
            playerName,
            storyId,
            out PlayerNameCaseForms forms)
                ? forms
                : null;

        return PlayerNameInflector.Resolve(playerName, grammaticalCase, overrides);
    }

    static PlayerNameCase ResolveMatchedPlayerNameCase(
        Match match,
        DialogueVariableContext context,
        string storyId)
    {
        string caseCode = match != null && match.Groups["case"].Success
            ? match.Groups["case"].Value
            : "";
        return ResolvePlayerNameCaseCode(caseCode, context, storyId);
    }

    static PlayerNameCase ResolvePlayerNameCaseCode(
        string caseCode,
        DialogueVariableContext context,
        string storyId)
    {
        if (PlayerNameInflector.TryParseCaseCode(caseCode, out PlayerNameCase grammaticalCase))
            return grammaticalCase;

        LogUnknownPlayerNameCase(caseCode, context, storyId);
        return PlayerNameCase.Nominative;
    }

    static bool TryResolveStoryPlayerName(string storyId, out string playerName)
    {
        playerName = "";
        if (string.IsNullOrWhiteSpace(storyId))
            return false;

        try
        {
            if (!HeroCustomizationStore.TryLoadPlayerNameForStory(storyId, out string loadedName))
                return false;

            return TrySanitizePlayerName(loadedName, out playerName);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.StoryUi,
                nameof(DialogueVariableResolver),
                nameof(TryResolveStoryPlayerName),
                "Failed to load story-scoped player name. Fallback chain will continue.",
                LogMetadata.Of("storyId", storyId, "exceptionType", exception.GetType().Name),
                recoverable: true);
            return false;
        }
    }

    static bool TryResolveStoryDefaultPlayerName(string storyId, out string playerName)
    {
        playerName = "";
        if (string.IsNullOrWhiteSpace(storyId))
            return false;

        try
        {
            StoryManager manager = StoryManager.Instance ?? FindSceneStoryManager();
            if (manager == null)
                return false;

            string currentStoryId = SaveDataSanitizer.SanitizeIdentifier(manager.CurrentStoryId);
            if (!string.IsNullOrEmpty(currentStoryId) &&
                !string.Equals(storyId, currentStoryId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TrySanitizePlayerName(manager.ResolveStoryDefaultPlayerNameForCurrentStory(), out playerName);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.StoryUi,
                nameof(DialogueVariableResolver),
                nameof(TryResolveStoryDefaultPlayerName),
                "Failed to resolve story default player name. Fallback chain will continue.",
                LogMetadata.Of("storyId", storyId, "exceptionType", exception.GetType().Name),
                recoverable: true);
            return false;
        }
    }

    static bool TryResolveGlobalPlayerName(out string playerName)
    {
        playerName = "";
        try
        {
            if (!HeroCustomizationStore.HasStoredPlayerName())
                return false;

            HeroCustomizationState state = HeroCustomizationStore.Load();
            return state != null && TrySanitizePlayerName(state.playerName, out playerName);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.StoryUi,
                nameof(DialogueVariableResolver),
                nameof(TryResolveGlobalPlayerName),
                "Failed to load global player name. Fallback chain will continue.",
                LogMetadata.Of("exceptionType", exception.GetType().Name),
                recoverable: true);
            return false;
        }
    }

    static bool TryResolveStoryDefaultPlayerNameCaseForms(
        string playerName,
        string storyId,
        out PlayerNameCaseForms forms)
    {
        forms = null;
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(storyId))
            return false;

        try
        {
            StoryManager manager = StoryManager.Instance ?? FindSceneStoryManager();
            if (manager == null)
                return false;

            string currentStoryId = SaveDataSanitizer.SanitizeIdentifier(manager.CurrentStoryId);
            if (!string.IsNullOrEmpty(currentStoryId) &&
                !string.Equals(storyId, currentStoryId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            forms = manager.ResolveStoryDefaultPlayerNameCaseFormsForCurrentStory(playerName);
            return PlayerNameInflector.HasAnyCaseForms(forms);
        }
        catch (Exception exception)
        {
            AppLogger.Warn(
                AppLogCategory.StoryUi,
                nameof(DialogueVariableResolver),
                nameof(TryResolveStoryDefaultPlayerNameCaseForms),
                "Failed to resolve story default player name case forms. Auto inflection will be used.",
                LogMetadata.Of("storyId", storyId, "exceptionType", exception.GetType().Name),
                recoverable: true);
            forms = null;
            return false;
        }
    }

    static bool TrySanitizePlayerName(string rawName, out string playerName)
    {
        playerName = SaveDataSanitizer.SanitizePlayerName(rawName);
        return !string.IsNullOrWhiteSpace(playerName) &&
               !IsPlayerNameToken(playerName) &&
               !string.Equals(playerName, HeroCustomizationStore.DefaultPlayerName, StringComparison.OrdinalIgnoreCase);
    }

    static string ResolveStoryId(DialogueVariableContext context)
    {
        if (context != null && !string.IsNullOrWhiteSpace(context.StoryId))
            return SaveDataSanitizer.SanitizeIdentifier(context.StoryId);

        if (StoryManager.Instance != null && !string.IsNullOrWhiteSpace(StoryManager.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(StoryManager.Instance.CurrentStoryId);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(GameState.Instance.CurrentStoryId);

        StoryManager sceneManager = FindSceneStoryManager();
        return sceneManager != null
            ? SaveDataSanitizer.SanitizeIdentifier(sceneManager.CurrentStoryId)
            : "";
    }

    static StoryManager FindSceneStoryManager()
    {
        StoryManager[] managers = Resources.FindObjectsOfTypeAll<StoryManager>();
        if (managers == null)
            return null;

        for (int i = 0; i < managers.Length; i++)
        {
            StoryManager manager = managers[i];
            if (manager != null && manager.gameObject != null && manager.gameObject.scene.IsValid())
                return manager;
        }

        return null;
    }

    static string NormalizeVariableName(string value)
    {
        string normalized = (value ?? "").Trim();
        normalized = normalized.Trim('{', '}', '[', ']', '<', '>');
        int caseSeparator = normalized.IndexOf(':');
        if (caseSeparator >= 0)
            normalized = normalized.Substring(0, caseSeparator);
        return normalized.Trim();
    }

    static string ExtractVariableCaseCode(string value)
    {
        string normalized = (value ?? "").Trim();
        normalized = normalized.Trim('{', '}', '[', ']', '<', '>');
        int caseSeparator = normalized.IndexOf(':');
        return caseSeparator >= 0 && caseSeparator + 1 < normalized.Length
            ? normalized.Substring(caseSeparator + 1).Trim()
            : "";
    }

    static void LogUnknownPlayerNameCase(
        string caseCode,
        DialogueVariableContext context,
        string storyId)
    {
        context ??= DialogueVariableContext.StoryUi();
        caseCode = (caseCode ?? "").Trim();
        string category = string.IsNullOrWhiteSpace(context.LogCategory) ? AppLogCategory.StoryUi : context.LogCategory;
        string signature = category + "|" + caseCode + "|" + storyId;
        if (string.Equals(signature, _lastCaseWarningSignature, StringComparison.Ordinal))
            return;

        _lastCaseWarningSignature = signature;
        string component = string.IsNullOrWhiteSpace(context.SourceClass)
            ? nameof(DialogueVariableResolver)
            : context.SourceClass;
        AppLogger.Warn(
            category,
            component,
            nameof(ResolvePlayerName),
            "Unknown player name case code. Nominative player name was used.",
            LogMetadata.Of(
                "case",
                caseCode,
                "storyId",
                storyId,
                "chapterId",
                context.ChapterId,
                "object",
                context.SourceObject != null ? context.SourceObject.name : ""),
            recoverable: true);
    }

    static void LogMissingSpeakerIdentity(DialogueVariableContext context, string storyId)
    {
        context ??= DialogueVariableContext.StoryUi();
        string category = string.IsNullOrWhiteSpace(context.LogCategory) ? AppLogCategory.StoryUi : context.LogCategory;
        string signature = category + "|" + storyId + "|" + context.ChapterId + "|" + context.SourceClass;
        if (string.Equals(signature, _lastSpeakerWarningSignature, StringComparison.Ordinal))
            return;

        _lastSpeakerWarningSignature = signature;
        string component = string.IsNullOrWhiteSpace(context.SourceClass)
            ? nameof(DialogueVariableResolver)
            : context.SourceClass;
        AppLogger.Warn(
            category,
            component,
            nameof(ResolveSpeakerNameCore),
            "[DialogueIdentity] Speaker placeholder was used without speaker identity context. Fallback name was used.",
            LogMetadata.Of(
                "storyId",
                storyId,
                "chapterId",
                context.ChapterId,
                "fallback",
                DialogueVariableResolver.FallbackPlayerName,
                "object",
                context.SourceObject != null ? context.SourceObject.name : ""),
            recoverable: true);
    }

    static void LogResolvedName(
        string playerName,
        string source,
        bool usedFallback,
        DialogueVariableContext context,
        string storyId)
    {
        context ??= DialogueVariableContext.StoryUi();
        string category = string.IsNullOrWhiteSpace(context.LogCategory) ? AppLogCategory.StoryUi : context.LogCategory;
        string signature = category + "|" + source + "|" + playerName + "|" + storyId + "|" + usedFallback;
        if (string.Equals(signature, _lastLogSignature, StringComparison.Ordinal))
            return;

        _lastLogSignature = signature;
        var metadata = LogMetadata.Of(
            usedFallback ? "fallback" : "name",
            playerName,
            "source",
            source,
            "storyId",
            storyId,
            "chapterId",
            context.ChapterId,
            "object",
            context.SourceObject != null ? context.SourceObject.name : "");

        string component = string.IsNullOrWhiteSpace(context.SourceClass)
            ? nameof(DialogueVariableResolver)
            : context.SourceClass;

        if (usedFallback)
        {
            AppLogger.Warn(
                category,
                component,
                nameof(ResolvePlayerName),
                "Player name was not found. Fallback name was used. Fallback=" + playerName,
                metadata,
                recoverable: true);
            return;
        }

        AppLogger.DebugLog(
            category,
            component,
            nameof(ResolvePlayerName),
            "Player name resolved successfully. Name=" + playerName,
            metadata);
    }
}
