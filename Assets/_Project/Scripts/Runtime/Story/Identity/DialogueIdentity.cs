using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public enum DialogueIdentitySource
{
    Profile,
    CharacterData,
    StoryActorMap,
    Localization,
    Fallback,
    LiteralText,
    SerializedPrefab,
    Unknown
}

public sealed class DialogueIdentityRequest
{
    public string StoryId = "";
    public string ChapterId = "";
    public string NodeId = "";
    public int LineIndex = -1;
    public int PageIndex = -1;
    public DialogueLine Line;
    public CharacterData Speaker;
    public string SpeakerId = "";
    public string SpeakerNameHint = "";
    public string BodyText = "";
    public string FallbackDisplayName = "";
    public DialogueVariableContext VariableContext;
    public GameObject SourceObject;
}

public sealed class DialogueIdentityResult
{
    public readonly string StoryId;
    public readonly string ChapterId;
    public readonly string NodeId;
    public readonly int LineIndex;
    public readonly int PageIndex;
    public readonly string SpeakerId;
    public readonly string ActorId;
    public readonly string CharacterId;
    public readonly string DisplayName;
    public readonly DialogueIdentitySource Source;
    public readonly bool IsFallback;
    public readonly bool IsDynamicPlayerName;
    public readonly string Warning;

    public DialogueIdentityResult(
        string storyId,
        string chapterId,
        string nodeId,
        int lineIndex,
        int pageIndex,
        string speakerId,
        string actorId,
        string characterId,
        string displayName,
        DialogueIdentitySource source,
        bool isFallback,
        bool isDynamicPlayerName,
        string warning)
    {
        StoryId = storyId ?? "";
        ChapterId = chapterId ?? "";
        NodeId = nodeId ?? "";
        LineIndex = lineIndex;
        PageIndex = pageIndex;
        SpeakerId = speakerId ?? "";
        ActorId = actorId ?? "";
        CharacterId = characterId ?? "";
        DisplayName = displayName ?? "";
        Source = source;
        IsFallback = isFallback;
        IsDynamicPlayerName = isDynamicPlayerName;
        Warning = warning ?? "";
    }
}

public interface ICharacterIdentityService
{
    DialogueIdentityResult ResolveSpeaker(DialogueIdentityRequest request);
}

public static class DialogueIdentity
{
    static ICharacterIdentityService _service = new CharacterIdentityService();

    public static ICharacterIdentityService Service
    {
        get => _service;
        set => _service = value ?? new CharacterIdentityService();
    }

    public static DialogueIdentityResult ResolveSpeaker(DialogueIdentityRequest request)
    {
        return Service.ResolveSpeaker(request ?? new DialogueIdentityRequest());
    }

    public static bool IsPlayerSpeaker(string speakerId, CharacterData speaker, string displayName = "")
    {
        return CharacterIdentityService.IsPlayerSpeaker(speakerId, speaker, displayName);
    }
}

public sealed class CharacterIdentityService : ICharacterIdentityService
{
    const string HeroToken = "hero";

    public DialogueIdentityResult ResolveSpeaker(DialogueIdentityRequest request)
    {
        request ??= new DialogueIdentityRequest();

        DialogueLine line = request.Line;
        CharacterData speaker = request.Speaker != null ? request.Speaker : line != null ? line.speaker : null;
        string speakerId = FirstNonEmpty(
            request.SpeakerId,
            line != null ? line.speakerId : "",
            request.SpeakerNameHint,
            line != null ? line.speakerNameHint : "",
            ResolveCharacterIdFromSpeaker(speaker));
        string rawDisplayName = speaker != null ? speaker.characterName : "";
        string characterId = FirstNonEmpty(speakerId, ResolveCharacterIdFromSpeaker(speaker));

        DialogueVariableContext variableContext = request.VariableContext ??
            DialogueVariableContext.StoryUi(
                nameof(CharacterIdentityService),
                request.SourceObject,
                request.StoryId,
                request.ChapterId);

        DialogueIdentityResult result = IsPlayerSpeaker(speakerId, speaker, rawDisplayName)
            ? ResolvePlayerIdentity(request, variableContext, speakerId, characterId, rawDisplayName)
            : ResolveCharacterIdentity(request, variableContext, speaker, speakerId, characterId, rawDisplayName);

        DialogueIdentityLogger.Log(request, result, rawDisplayName);
        return result;
    }

    static DialogueIdentityResult ResolvePlayerIdentity(
        DialogueIdentityRequest request,
        DialogueVariableContext variableContext,
        string speakerId,
        string characterId,
        string rawDisplayName)
    {
        string storyDefaultName = ResolveStoryDefaultPlayerName(request.StoryId);
        CharacterProfileData profile = CharacterProfileService.ResolvePlayerName(
            request.StoryId,
            storyDefaultName,
            nameof(CharacterIdentityService));

        DialogueIdentitySource source = MapProfileSource(profile);
        string displayName = profile != null ? profile.PlayerName : "";
        bool fallback = profile == null || profile.UsedFallback || string.IsNullOrWhiteSpace(displayName);
        if (fallback && string.IsNullOrWhiteSpace(displayName))
            displayName = FirstNonEmpty(request.FallbackDisplayName, HeroCustomizationStore.DefaultPlayerName);

        string warning = "";
        if (fallback)
            warning = "Player speaker used fallback display name.";
        else if (!string.IsNullOrWhiteSpace(rawDisplayName) &&
                 !LooksLikePlayerNameReference(rawDisplayName) &&
                 !NamesEqual(rawDisplayName, displayName))
        {
            warning = "Player speaker CharacterData name differs from resolved profile name.";
        }

        return new DialogueIdentityResult(
            request.StoryId,
            request.ChapterId,
            request.NodeId,
            request.LineIndex,
            request.PageIndex,
            speakerId,
            speakerId,
            FirstNonEmpty(characterId, HeroToken),
            displayName,
            source,
            fallback,
            true,
            warning);
    }

    static DialogueIdentityResult ResolveCharacterIdentity(
        DialogueIdentityRequest request,
        DialogueVariableContext variableContext,
        CharacterData speaker,
        string speakerId,
        string characterId,
        string rawDisplayName)
    {
        string displayName = ResolveDisplayName(rawDisplayName, variableContext);
        DialogueIdentitySource source = ResolveCharacterSource(speaker, rawDisplayName, displayName);
        bool fallback = false;
        string warning = "";

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = FirstNonEmpty(request.FallbackDisplayName, speakerId, characterId);
            fallback = true;
            source = DialogueIdentitySource.Fallback;
            warning = speaker == null
                ? "Dialogue line has speaker id but no CharacterData."
                : "CharacterData has no display name.";
        }

        return new DialogueIdentityResult(
            request.StoryId,
            request.ChapterId,
            request.NodeId,
            request.LineIndex,
            request.PageIndex,
            speakerId,
            speakerId,
            characterId,
            displayName,
            source,
            fallback,
            false,
            warning);
    }

    public static bool IsPlayerSpeaker(string speakerId, CharacterData speaker, string displayName = "")
    {
        if (speaker != null && speaker.inheritAppearanceFromPlayer)
            return true;

        if (IsHeroTokenValue(speakerId) || IsHeroTokenValue(ResolveCharacterIdFromSpeaker(speaker)))
            return true;

        return LooksLikePlayerNameReference(displayName) ||
               LooksLikePlayerNameReference(speaker != null ? speaker.characterName : "");
    }

    static DialogueIdentitySource MapProfileSource(CharacterProfileData profile)
    {
        if (profile == null || profile.UsedFallback)
            return DialogueIdentitySource.Fallback;

        return string.Equals(profile.Source, "story-default", StringComparison.OrdinalIgnoreCase)
            ? DialogueIdentitySource.StoryActorMap
            : DialogueIdentitySource.Profile;
    }

    static DialogueIdentitySource ResolveCharacterSource(CharacterData speaker, string rawDisplayName, string displayName)
    {
        if (speaker == null)
            return DialogueIdentitySource.Fallback;

        if ((speaker.hideFlags & HideFlags.DontSave) != 0 ||
            (speaker.name != null && speaker.name.StartsWith("JsonCharacter_", StringComparison.OrdinalIgnoreCase)))
        {
            return DialogueIdentitySource.StoryActorMap;
        }

        if (!string.IsNullOrWhiteSpace(rawDisplayName))
            return DialogueIdentitySource.CharacterData;

        return !string.IsNullOrWhiteSpace(displayName)
            ? DialogueIdentitySource.SerializedPrefab
            : DialogueIdentitySource.Fallback;
    }

    static string ResolveDisplayName(string rawDisplayName, DialogueVariableContext context)
    {
        if (string.IsNullOrWhiteSpace(rawDisplayName))
            return "";

        return DialogueVariableResolver.ResolveText(rawDisplayName, context).Trim();
    }

    static string ResolveStoryDefaultPlayerName(string storyId)
    {
        StoryManager manager = StoryManager.Instance ?? FindSceneStoryManager();
        if (manager == null)
            return "";

        string managerStoryId = SaveDataSanitizer.SanitizeIdentifier(manager.CurrentStoryId);
        string requestStoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        if (!string.IsNullOrEmpty(requestStoryId) &&
            !string.IsNullOrEmpty(managerStoryId) &&
            !string.Equals(requestStoryId, managerStoryId, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return manager.ResolveStoryDefaultPlayerNameForCurrentStory();
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

    static string ResolveCharacterIdFromSpeaker(CharacterData speaker)
    {
        if (speaker == null)
            return "";

        string name = speaker.name ?? "";
        const string jsonPrefix = "JsonCharacter_";
        if (name.StartsWith(jsonPrefix, StringComparison.OrdinalIgnoreCase) && name.Length > jsonPrefix.Length)
            return name.Substring(jsonPrefix.Length);

        return name;
    }

    static bool IsHeroTokenValue(string value)
    {
        switch (NormalizeSpeakerToken(value))
        {
            case "hero":
            case "gg":
            case "mainhero":
            case "player":
            case "protagonist":
            case "heroine":
            case "jsoncharacterhero":
            case "jsoncharactergg":
            case "jsoncharactermainhero":
            case "jsoncharacterplayer":
                return true;
            default:
                return false;
        }
    }

    static bool LooksLikePlayerNameReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DialogueVariableResolver.IsPlayerNameToken(value))
            return true;

        string normalized = value.Trim();
        return normalized.IndexOf("playerName", StringComparison.OrdinalIgnoreCase) >= 0 ||
               normalized.IndexOf("player_name", StringComparison.OrdinalIgnoreCase) >= 0 ||
               normalized.IndexOf("heroName", StringComparison.OrdinalIgnoreCase) >= 0 ||
               normalized.IndexOf("hero_name", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string NormalizeSpeakerToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = PlayerAppearance.ReplacePlaceholders(value);
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    static bool NamesEqual(string left, string right)
    {
        left = SaveDataSanitizer.SanitizePlayerName(left);
        right = SaveDataSanitizer.SanitizePlayerName(right);
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return "";
    }
}

public static class DialogueIdentityLiteralScanner
{
    static readonly string[] DefaultWatchedNames =
    {
        "\u0410\u043b\u0438\u0441\u0430",
        "\u042d\u043b\u0438\u0441\u043e\u043d",
        "\u0414\u0430\u0440\u0438\u043d\u0430",
        "Alice",
        "Alison",
        "Darina"
    };

    public static List<string> FindUnexpectedLiterals(
        string bodyText,
        string expectedDisplayName,
        string rawSpeakerName = "",
        IEnumerable<string> additionalNames = null)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(bodyText))
            return result;

        var candidates = new List<string>(DefaultWatchedNames);
        if (!string.IsNullOrWhiteSpace(rawSpeakerName))
            candidates.Add(rawSpeakerName);

        if (additionalNames != null)
        {
            foreach (string name in additionalNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    candidates.Add(name);
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate.Trim()))
                continue;

            if (NamesMatch(candidate, expectedDisplayName))
                continue;

            if (ContainsName(bodyText, candidate))
                result.Add(candidate.Trim());
        }

        return result;
    }

    public static bool ContainsName(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(name))
            return false;

        string pattern = @"(?<![\p{L}\p{Nd}_])" + Regex.Escape(name.Trim()) + @"(?![\p{L}\p{Nd}_])";
        return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    static bool NamesMatch(string left, string right)
    {
        left = SaveDataSanitizer.SanitizePlayerName(left);
        right = SaveDataSanitizer.SanitizePlayerName(right);
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

static class DialogueIdentityLogger
{
    static readonly HashSet<string> SeenSignatures = new HashSet<string>(StringComparer.Ordinal);

    public static void Log(DialogueIdentityRequest request, DialogueIdentityResult result, string rawSpeakerName)
    {
        if (request == null || result == null)
            return;

        List<string> literals = DialogueIdentityLiteralScanner.FindUnexpectedLiterals(
            request.BodyText,
            result.DisplayName,
            rawSpeakerName);

        bool warn = result.IsFallback ||
                    !string.IsNullOrWhiteSpace(result.Warning) ||
                    literals.Count > 0;

        bool shouldDebug = result.IsDynamicPlayerName || result.Source != DialogueIdentitySource.CharacterData;
        if (!warn && !shouldDebug)
            return;

        string literalText = literals.Count > 0 ? string.Join(", ", literals.ToArray()) : "";
        string signature = result.StoryId + "|" +
                           result.ChapterId + "|" +
                           result.NodeId + "|" +
                           result.LineIndex + "|" +
                           result.SpeakerId + "|" +
                           result.DisplayName + "|" +
                           result.Source + "|" +
                           result.Warning + "|" +
                           literalText;
        if (SeenSignatures.Contains(signature))
            return;

        if (SeenSignatures.Count > 512)
            SeenSignatures.Clear();
        SeenSignatures.Add(signature);

        string warning = result.Warning;
        if (literals.Count > 0)
        {
            warning = string.IsNullOrWhiteSpace(warning)
                ? "Body text contains literal names that differ from resolved speaker display name."
                : warning + " Body text contains literal names that differ from resolved speaker display name.";
        }

        var metadata = LogMetadata.Of(
            "story", result.StoryId,
            "chapter", result.ChapterId,
            "node", result.NodeId,
            "line", result.LineIndex,
            "page", result.PageIndex,
            "speaker", result.SpeakerId,
            "character", result.CharacterId,
            "nameplate", result.DisplayName,
            "body", TrimForLog(request.BodyText, 240),
            "rawSpeakerName", rawSpeakerName,
            "source", result.Source.ToString(),
            "profile", result.IsDynamicPlayerName,
            "fallback", result.IsFallback,
            "literals", literalText,
            "warning", warning);

        if (warn)
        {
            AppLogger.Warn(
                AppLogCategory.StoryUi,
                nameof(DialogueIdentity),
                nameof(ICharacterIdentityService.ResolveSpeaker),
                "[DialogueIdentity] Identity warning.",
                metadata,
                recoverable: true);
            return;
        }

        AppLogger.DebugLog(
            AppLogCategory.StoryUi,
            nameof(DialogueIdentity),
            nameof(ICharacterIdentityService.ResolveSpeaker),
            "[DialogueIdentity] Identity resolved.",
            metadata);
    }

    static string TrimForLog(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? "";

        return value.Substring(0, maxLength) + "...";
    }
}
