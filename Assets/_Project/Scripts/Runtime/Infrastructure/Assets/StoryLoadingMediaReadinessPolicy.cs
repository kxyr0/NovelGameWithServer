using System;

public enum StoryLoadingMediaReadinessSeverity
{
    Ok,
    Warning,
    Error
}

public readonly struct StoryLoadingMediaReadinessReport
{
    public StoryLoadingMediaReadinessReport(
        GameData sourceData,
        StoryLoadingMediaReadinessSeverity severity,
        bool overrideEnabled,
        bool usesCustomMedia,
        bool hasAddressableMedia,
        bool hasDirectMedia,
        bool fallbackToMenuMediaWhenEmpty,
        bool isLazyLoadSafe,
        string message)
    {
        SourceData = sourceData;
        Severity = severity;
        OverrideEnabled = overrideEnabled;
        UsesCustomMedia = usesCustomMedia;
        HasAddressableMedia = hasAddressableMedia;
        HasDirectMedia = hasDirectMedia;
        FallbackToMenuMediaWhenEmpty = fallbackToMenuMediaWhenEmpty;
        IsLazyLoadSafe = isLazyLoadSafe;
        Message = message;
    }

    public GameData SourceData { get; }
    public StoryLoadingMediaReadinessSeverity Severity { get; }
    public bool OverrideEnabled { get; }
    public bool UsesCustomMedia { get; }
    public bool HasAddressableMedia { get; }
    public bool HasDirectMedia { get; }
    public bool FallbackToMenuMediaWhenEmpty { get; }
    public bool IsLazyLoadSafe { get; }
    public string Message { get; }
    public bool IsOk => Severity == StoryLoadingMediaReadinessSeverity.Ok;
    public bool BlocksStrictLazyLoading => UsesCustomMedia && !IsLazyLoadSafe;
    public bool ShouldLog => Severity != StoryLoadingMediaReadinessSeverity.Ok &&
        !string.IsNullOrWhiteSpace(Message);
}

public interface IStoryLoadingMediaReadinessPolicy
{
    StoryLoadingMediaReadinessReport Evaluate(GameData data);
}

public sealed class StoryLoadingMediaReadinessPolicy : IStoryLoadingMediaReadinessPolicy
{
    public StoryLoadingMediaReadinessReport Evaluate(GameData data)
    {
        if (data == null)
            return CreateReport(null, StoryLoadingMediaReadinessSeverity.Error, false, false, false, false, true, false, "GameData is missing.");

        GameStoryLoadingMediaSettings settings = data.LoadingMedia;
        if (settings == null)
            return CreateReport(data, StoryLoadingMediaReadinessSeverity.Error, false, false, false, false, true, false, "Loading media settings are missing.");

        bool overrideEnabled = settings.OverrideLoadingMedia;
        bool hasAddressableMedia = settings.HasAddressableMedia;
        bool hasDirectMedia = settings.HasDirectMedia;
        bool usesCustomMedia = settings.ShouldUseCustomMedia;
        bool fallbackToMenuMediaWhenEmpty = settings.FallbackToMenuMediaWhenEmpty;

        if (!overrideEnabled)
        {
            return CreateReport(
                data,
                StoryLoadingMediaReadinessSeverity.Ok,
                overrideEnabled,
                usesCustomMedia,
                hasAddressableMedia,
                hasDirectMedia,
                fallbackToMenuMediaWhenEmpty,
                isLazyLoadSafe: true,
                "Story uses legacy menu media.");
        }

        if (hasDirectMedia)
        {
            string message = hasAddressableMedia && settings.HasAddressableReferenceForEveryDirectFallback
                ? "Custom loading media has Addressables, but direct fallback references are still stored in GameData. Move heavy fallback assets out of menu data to keep loading media lazy."
                : "Custom loading media has direct fallback references without matching Addressables. Move story loading image/video/GIF to Addressables so replacements load only when the story starts.";

            return CreateReport(
                data,
                StoryLoadingMediaReadinessSeverity.Warning,
                overrideEnabled,
                usesCustomMedia,
                hasAddressableMedia,
                hasDirectMedia,
                fallbackToMenuMediaWhenEmpty,
                isLazyLoadSafe: false,
                message);
        }

        if (hasAddressableMedia)
        {
            return CreateReport(
                data,
                StoryLoadingMediaReadinessSeverity.Ok,
                overrideEnabled,
                usesCustomMedia,
                hasAddressableMedia,
                hasDirectMedia,
                fallbackToMenuMediaWhenEmpty,
                isLazyLoadSafe: true,
                "Custom loading media is Addressable and lazy-load safe.");
        }

        StoryLoadingMediaReadinessSeverity severity = fallbackToMenuMediaWhenEmpty
            ? StoryLoadingMediaReadinessSeverity.Warning
            : StoryLoadingMediaReadinessSeverity.Error;
        string emptyMessage = fallbackToMenuMediaWhenEmpty
            ? "Custom loading media override is enabled but no replacement media is assigned; the loading screen will use menu media fallback."
            : "Custom loading media override is enabled but no replacement media or menu fallback is available.";

        return CreateReport(
            data,
            severity,
            overrideEnabled,
            usesCustomMedia,
            hasAddressableMedia,
            hasDirectMedia,
            fallbackToMenuMediaWhenEmpty,
            isLazyLoadSafe: true,
            emptyMessage);
    }

    private static StoryLoadingMediaReadinessReport CreateReport(
        GameData data,
        StoryLoadingMediaReadinessSeverity severity,
        bool overrideEnabled,
        bool usesCustomMedia,
        bool hasAddressableMedia,
        bool hasDirectMedia,
        bool fallbackToMenuMediaWhenEmpty,
        bool isLazyLoadSafe,
        string message)
    {
        return new StoryLoadingMediaReadinessReport(
            data,
            severity,
            overrideEnabled,
            usesCustomMedia,
            hasAddressableMedia,
            hasDirectMedia,
            fallbackToMenuMediaWhenEmpty,
            isLazyLoadSafe,
            message);
    }
}

public static class StoryLoadingMediaReadinessPolicies
{
    private static readonly IStoryLoadingMediaReadinessPolicy SharedPolicy = new StoryLoadingMediaReadinessPolicy();

    public static IStoryLoadingMediaReadinessPolicy Shared => SharedPolicy;
}
