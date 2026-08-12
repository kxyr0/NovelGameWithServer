using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public enum StoryEndScreenStatValueMode
{
    CurrentTotal = 0,
    EpisodeDelta = 1,
    HeartBalance = 2,
    CandleBalance = 3,
    HeartDelta = 4,
    CandleDelta = 5,
    PreviewOnly = 6
}

public interface IStoryEndScreenDataProvider
{
    StoryEndScreenData Build(StoryManager storyManager, IReadOnlyList<StoryEndScreenStatBinding> statBindings, StoryEndScreenPreviewSettings previewSettings, bool preview);
}

public interface IStoryEndScreenRenderer
{
    bool Render(StoryEndScreenController controller, StoryEndScreenData data, string reason);
}

public interface IStoryEndScreenValidator
{
    StoryEndScreenValidationResult Validate(StoryEndScreenController controller, bool requireRuntime);
}

public interface IStoryEndScreenNavigator
{
    bool ReturnToMenu(StoryEndScreenController controller);
    bool ContinueOrReturnToMenu(StoryEndScreenController controller);
    bool ContinueStory(StoryEndScreenController controller);
    bool RestartCompletedEpisode(StoryEndScreenController controller);
    bool OpenScreen(StoryEndScreenController controller, string screenId);
}

[Serializable]
public sealed class StoryEndScreenReferences
{
    [Header("Root")]
    public GameObject root;
    public CanvasGroup canvasGroup;
    public RectTransform safeArea;
    public RectTransform panelRoot;

    [Header("Background")]
    public Image backgroundImage;
    public Sprite backgroundOverride;
    public Sprite defaultBackground;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text storyTitleText;
    public TMP_Text completedEpisodeText;
    public TMP_Text nextEpisodeText;

    [Header("Stats")]
    public RectTransform statsContainer;
    public GameObject statRowTemplate;
    public Image statsBackgroundImage;
    public Sprite statsBackgroundOverride;
    public bool hideStatsBackground;
    public RectTransform legacyCityRow;
    public RectTransform legacyFairytaleRow;
    public RectTransform legacyReputationRow;
    public RectTransform legacySparksRow;
    public RectTransform legacyCandlesRow;
    public Image legacyCityImage;
    public Image legacyFairytaleImage;
    public Image legacyReputationImage;
    public Image legacySparksImage;
    public Image legacyCandlesImage;
    public Image legacyCityIconImage;
    public Image legacyFairytaleIconImage;
    public Image legacyReputationIconImage;
    public Image legacySparksIconImage;
    public Image legacyCandlesIconImage;
    public TMP_Text legacyCityText;
    public TMP_Text legacyFairytaleText;
    public TMP_Text legacyReputationText;
    public TMP_Text legacySparksText;
    public TMP_Text legacyCandlesText;

    [Header("Buttons")]
    public Button continueButton;
    public Image continueButtonPlateImage;
    public Sprite continueButtonPlateSprite;
    public UnityEngine.Object continueButtonPlateSpriteSource;
    public TMP_Text continueButtonText;
    [HideInInspector]
    public Button menuButton;
    [HideInInspector]
    public Button nextEpisodeButton;
    [HideInInspector]
    public Button restartEpisodeButton;
    [HideInInspector]
    public Button closeButton;

    public GameObject ResolveRoot(StoryEndScreenController owner)
    {
        if (root != null)
            return root;
        if (owner != null && owner.StoryManager != null && owner.StoryManager.endStoryPanel != null)
            return owner.StoryManager.endStoryPanel;
        return owner != null ? owner.gameObject : null;
    }

    public RectTransform ResolvePanelRoot(StoryEndScreenController owner)
    {
        if (panelRoot != null)
            return panelRoot;

        GameObject resolvedRoot = ResolveRoot(owner);
        return resolvedRoot != null ? resolvedRoot.GetComponent<RectTransform>() : null;
    }
}

[Serializable]
public sealed class StoryEndScreenLayoutSettings
{
    public bool applyLayoutInEditMode = true;
    public bool keepTemplatesInactive = true;
    public bool clearGeneratedRowsBeforeRender = true;
    public bool forceRebuildLayout = true;
    public bool stretchRootToScreen = true;
    public bool useSafeAreaPadding = true;
    public Vector4 safeAreaPadding = new Vector4(28f, 54f, 28f, 34f);
    public float statsSpacing = 18f;
    public float statRowMinHeight = 86f;
    public float statRowPreferredHeight = 96f;
    public float statRowMaxWidth = 850f;
    public bool centerStatsContainer = true;
}

[Serializable]
public sealed class StoryEndScreenPreviewSettings
{
    public bool useSavedValuesInEditor;
    public bool usePreviewFallbackValues = true;
    public bool hideOtherStoryUiDuringPreview = true;
    public bool showNextEpisodeInPreview = true;
    public Sprite previewBackground;
    public string previewTitle = "Серия завершена";
    public string previewStoryTitle = "История";
    public string previewCompletedEpisodeTitle = "Глава завершена";
    public string previewNextEpisodeTitle = "Следующая глава";
    public int previewCity = 0;
    public int previewFairytale = 0;
    public int previewReputation = 0;
    public int previewSparks = 0;
    public int previewCandles = 0;
}

[Serializable]
public sealed class StoryEndScreenStatBinding
{
    public bool enabled = true;
    public string label = "Город";
    public string statId = "city";
    public string[] statAliases = Array.Empty<string>();
    public StoryEndScreenStatValueMode valueMode = StoryEndScreenStatValueMode.CurrentTotal;
    public int previewValue;
    public RectTransform row;
    public Image backgroundImage;
    public Image plateImage;
    public Image iconImage;
    public TMP_Text lineText;
    public TMP_Text labelText;
    public TMP_Text valueText;
    public Sprite backgroundSprite;
    public UnityEngine.Object backgroundSpriteSource;
    public Sprite plateSprite;
    public UnityEngine.Object plateSpriteSource;
    public Sprite icon;
    public UnityEngine.Object iconSpriteSource;
    public bool hideBackground;
    public bool hidePlate;
    public bool hideIcon;
    public bool overrideIconSize;
    public Vector2 iconSize = new Vector2(96f, 96f);
    public bool overrideRowPosition;
    public Vector2 rowAnchoredPosition;
    public Vector2 rowOffset;
    public Vector2 backgroundOffset;
    public Vector2 plateOffset;
    public Vector2 iconOffset;
    public bool overrideBackgroundRect;
    public Vector2 backgroundAnchoredPosition;
    public Vector2 backgroundSize;
    public bool overridePlateRect;
    public Vector2 plateAnchoredPosition;
    public Vector2 plateSize;
    public bool overrideIconRect;
    public Vector2 iconAnchoredPosition;
    public Vector2 lineTextOffset;
    public Vector2 labelTextOffset;
    public Vector2 valueTextOffset;
    public bool overrideRowSize;
    public Vector2 rowSize;
    public bool ignoreParentLayoutWhenPositioned = true;
    public bool hideWhenZero;
    public string format = "{0}";
    public StoryEndScreenTextStyle lineTextStyle = new StoryEndScreenTextStyle();
    public StoryEndScreenTextStyle labelTextStyle = new StoryEndScreenTextStyle();
    public StoryEndScreenTextStyle valueTextStyle = new StoryEndScreenTextStyle();

    public bool MatchesLabel(string value)
    {
        return string.Equals(Normalize(label), Normalize(value), StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerable<string> AllStatIds()
    {
        if (!string.IsNullOrWhiteSpace(statId))
            yield return statId.Trim();

        if (statAliases == null)
            yield break;

        for (int i = 0; i < statAliases.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(statAliases[i]))
                yield return statAliases[i].Trim();
        }
    }

    public static StoryEndScreenStatBinding[] CreateDefaults()
    {
        return new[]
        {
            new StoryEndScreenStatBinding
            {
                label = "Город",
                statId = "city",
                statAliases = new[] { "town", "gorod" },
                valueMode = StoryEndScreenStatValueMode.EpisodeDelta
            },
            new StoryEndScreenStatBinding
            {
                label = "Сказка",
                statId = "fairytale",
                statAliases = new[] { "story", "tale", "skazka" },
                valueMode = StoryEndScreenStatValueMode.EpisodeDelta
            },
            new StoryEndScreenStatBinding
            {
                label = "Репутация",
                statId = "reputation",
                statAliases = new[] { "respect", "rep" },
                valueMode = StoryEndScreenStatValueMode.EpisodeDelta
            },
            new StoryEndScreenStatBinding
            {
                label = "Искры",
                statId = "hearts",
                valueMode = StoryEndScreenStatValueMode.HeartDelta
            }
        };
    }

    static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}

public sealed class StoryEndScreenStatValue
{
    public string Label;
    public string StatId;
    public int Value;
    public string FormattedValue;
    public RectTransform Row;
    public Image BackgroundImage;
    public Image PlateImage;
    public Image IconImage;
    public TMP_Text LineText;
    public TMP_Text LabelText;
    public TMP_Text ValueText;
    public Sprite BackgroundSprite;
    public Sprite PlateSprite;
    public Sprite Icon;
    public bool HideBackground;
    public bool HidePlate;
    public bool HideIcon;
    public bool OverrideIconSize;
    public Vector2 IconSize;
    public bool OverrideRowPosition;
    public Vector2 RowAnchoredPosition;
    public Vector2 RowOffset;
    public Vector2 BackgroundOffset;
    public Vector2 PlateOffset;
    public Vector2 IconOffset;
    public bool OverrideBackgroundRect;
    public Vector2 BackgroundAnchoredPosition;
    public Vector2 BackgroundSize;
    public bool OverridePlateRect;
    public Vector2 PlateAnchoredPosition;
    public Vector2 PlateSize;
    public bool OverrideIconRect;
    public Vector2 IconAnchoredPosition;
    public Vector2 LineTextOffset;
    public Vector2 LabelTextOffset;
    public Vector2 ValueTextOffset;
    public bool OverrideRowSize;
    public Vector2 RowSize;
    public bool IgnoreParentLayoutWhenPositioned;
    public bool HideWhenZero;
    public StoryEndScreenTextStyle LineTextStyle;
    public StoryEndScreenTextStyle LabelTextStyle;
    public StoryEndScreenTextStyle ValueTextStyle;
}

public sealed class StoryEndScreenData
{
    public bool IsPreview;
    public bool StoryFinished;
    public string StoryId;
    public string StoryTitle;
    public string CompletedEpisodeId;
    public string CompletedEpisodeTitle;
    public int CompletedEpisodeNumber;
    public string NextEpisodeId;
    public string NextEpisodeTitle;
    public int NextEpisodeNumber;
    public string Title;
    public Sprite Background;
    public readonly List<StoryEndScreenStatValue> Stats = new List<StoryEndScreenStatValue>();
}

public sealed class StoryEndScreenValidationResult
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;

    public void Error(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Errors.Add(message);
    }

    public void Warn(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Warnings.Add(message);
    }
}

public sealed class StoryEndScreenDataProvider : IStoryEndScreenDataProvider
{
    public StoryEndScreenData Build(
        StoryManager storyManager,
        IReadOnlyList<StoryEndScreenStatBinding> statBindings,
        StoryEndScreenPreviewSettings previewSettings,
        bool preview)
    {
        previewSettings ??= new StoryEndScreenPreviewSettings();
        var data = new StoryEndScreenData
        {
            IsPreview = preview,
            StoryFinished = storyManager != null && storyManager.EndPanelStoryFinished,
            StoryId = storyManager != null ? storyManager.CurrentStoryId : "",
            StoryTitle = FirstNonEmpty(storyManager != null ? storyManager.CurrentStoryTitle : "", previewSettings.previewStoryTitle, "История"),
            CompletedEpisodeId = storyManager != null ? storyManager.LastCompletedEpisodeId : "",
            CompletedEpisodeTitle = FirstNonEmpty(storyManager != null ? storyManager.LastCompletedChapterTitle : "", previewSettings.previewCompletedEpisodeTitle, "Глава завершена"),
            CompletedEpisodeNumber = storyManager != null ? storyManager.LastCompletedChapterNumber : 0,
            NextEpisodeId = storyManager != null ? storyManager.EndPanelNextChapterId : "",
            NextEpisodeTitle = FirstNonEmpty(storyManager != null ? storyManager.EndPanelNextChapterTitle : "", previewSettings.previewNextEpisodeTitle),
            NextEpisodeNumber = storyManager != null ? storyManager.EndPanelNextChapterNumber : 0,
            Title = FirstNonEmpty(previewSettings.previewTitle, "Серия завершена"),
            Background = previewSettings.previewBackground
        };

        if (!preview && storyManager != null)
            data.Title = storyManager.EndPanelStoryFinished ? "Серия завершена" : "Глава завершена";

        if (preview && !previewSettings.showNextEpisodeInPreview)
        {
            data.NextEpisodeTitle = "";
            data.NextEpisodeId = "";
            data.NextEpisodeNumber = 0;
        }

        AddStats(data, storyManager, statBindings, previewSettings, preview);
        LogDataBuilt(data, storyManager, preview);
        return data;
    }

    void AddStats(
        StoryEndScreenData data,
        StoryManager storyManager,
        IReadOnlyList<StoryEndScreenStatBinding> statBindings,
        StoryEndScreenPreviewSettings previewSettings,
        bool preview)
    {
        if (statBindings == null || statBindings.Count == 0)
            statBindings = StoryEndScreenStatBinding.CreateDefaults();

        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < statBindings.Count; i++)
        {
            StoryEndScreenStatBinding binding = statBindings[i];
            if (binding == null || !binding.enabled)
                continue;

            string label = FirstNonEmpty(binding.label, binding.statId, "Стат");
            if (!seenLabels.Add(label.Trim()))
            {
                ThrottledAppLogger.Warn(
                    "EndScreenDuplicateStat:" + label,
                    AppLogCategory.EndScreen,
                    nameof(StoryEndScreenDataProvider),
                    nameof(AddStats),
                    "Duplicate end-screen stat binding was ignored.",
                    LogMetadata.Of("label", label));
                continue;
            }

            int value = ResolveValue(binding, storyManager, previewSettings, preview);
            if (!preview && (Debug.isDebugBuild || Application.isEditor))
            {
                Debug.Log(
                    $"[END_STATS][BIND] storyId='{(storyManager != null ? storyManager.CurrentStoryId : "")}' " +
                    $"label='{label}' statId='{binding.statId}' mode={binding.valueMode} value={value}.");
            }

            if (binding.hideWhenZero && value == 0)
                continue;

            data.Stats.Add(new StoryEndScreenStatValue
            {
                Label = label,
                StatId = binding.statId,
                Value = value,
                FormattedValue = FormatValue(value, binding.format),
                Row = binding.row,
                BackgroundImage = binding.backgroundImage,
                PlateImage = binding.plateImage,
                IconImage = binding.iconImage,
                LineText = binding.lineText,
                LabelText = binding.labelText,
                ValueText = binding.valueText,
                BackgroundSprite = binding.backgroundSprite,
                PlateSprite = binding.plateSprite,
                Icon = binding.icon,
                HideBackground = binding.hideBackground,
                HidePlate = binding.hidePlate,
                HideIcon = binding.hideIcon,
                OverrideIconSize = binding.overrideIconSize,
                IconSize = binding.iconSize,
                OverrideRowPosition = binding.overrideRowPosition,
                RowAnchoredPosition = binding.rowAnchoredPosition,
                RowOffset = binding.rowOffset,
                BackgroundOffset = binding.backgroundOffset,
                PlateOffset = binding.plateOffset,
                IconOffset = binding.iconOffset,
                OverrideBackgroundRect = binding.overrideBackgroundRect,
                BackgroundAnchoredPosition = binding.backgroundAnchoredPosition,
                BackgroundSize = binding.backgroundSize,
                OverridePlateRect = binding.overridePlateRect,
                PlateAnchoredPosition = binding.plateAnchoredPosition,
                PlateSize = binding.plateSize,
                OverrideIconRect = binding.overrideIconRect,
                IconAnchoredPosition = binding.iconAnchoredPosition,
                LineTextOffset = binding.lineTextOffset,
                LabelTextOffset = binding.labelTextOffset,
                ValueTextOffset = binding.valueTextOffset,
                OverrideRowSize = binding.overrideRowSize,
                RowSize = binding.rowSize,
                IgnoreParentLayoutWhenPositioned = binding.ignoreParentLayoutWhenPositioned,
                HideWhenZero = binding.hideWhenZero,
                LineTextStyle = binding.lineTextStyle,
                LabelTextStyle = binding.labelTextStyle,
                ValueTextStyle = binding.valueTextStyle
            });
        }
    }

    int ResolveValue(
        StoryEndScreenStatBinding binding,
        StoryManager storyManager,
        StoryEndScreenPreviewSettings previewSettings,
        bool preview)
    {
        if (preview && previewSettings != null && !previewSettings.useSavedValuesInEditor)
            return ResolvePreviewValue(binding, previewSettings);

        // The standard completion rows have fixed semantics: they show what was
        // earned during the completed chapter. Do not let a stale scene/style
        // valueMode turn them back into CurrentTotal after StoryManager already
        // calculated the correct chapter summary.
        if (!preview && TryResolveCanonicalCompletionValue(binding, storyManager, out int canonicalValue))
            return canonicalValue;

        switch (binding.valueMode)
        {
            case StoryEndScreenStatValueMode.EpisodeDelta:
                return storyManager != null ? storyManager.GetLastCompletedEpisodeStatDelta(ToArray(binding.AllStatIds())) : ResolvePreviewValue(binding, previewSettings);
            case StoryEndScreenStatValueMode.HeartBalance:
                return PlayerData.Hearts;
            case StoryEndScreenStatValueMode.CandleBalance:
                return PlayerData.Candles;
            case StoryEndScreenStatValueMode.HeartDelta:
                return storyManager != null ? storyManager.LastCompletedEpisodeHeartDelta : ResolvePreviewValue(binding, previewSettings);
            case StoryEndScreenStatValueMode.CandleDelta:
                return storyManager != null ? storyManager.LastCompletedEpisodeCandleDelta : ResolvePreviewValue(binding, previewSettings);
            case StoryEndScreenStatValueMode.PreviewOnly:
                return ResolvePreviewValue(binding, previewSettings);
            case StoryEndScreenStatValueMode.CurrentTotal:
            default:
                return ResolveCurrentStat(binding, previewSettings);
        }
    }

    static bool TryResolveCanonicalCompletionValue(
        StoryEndScreenStatBinding binding,
        StoryManager storyManager,
        out int value)
    {
        value = 0;
        if (binding == null || storyManager == null)
            return false;

        if (BindingMatches(binding, "Город", "city", "town", "gorod"))
        {
            value = storyManager.GetLastCompletedEpisodeStatDelta("city", "town", "gorod");
            return true;
        }

        if (BindingMatches(binding, "Сказка", "fairytale", "story", "tale", "skazka"))
        {
            value = storyManager.GetLastCompletedEpisodeStatDelta("fairytale", "story", "tale", "skazka");
            return true;
        }

        if (BindingMatches(binding, "Репутация", "reputation", "respect", "rep"))
        {
            value = storyManager.GetLastCompletedEpisodeStatDelta("reputation", "respect", "rep");
            return true;
        }

        if (BindingMatches(binding, "Искры", "hearts", "sparks"))
        {
            value = storyManager.LastCompletedEpisodeHeartDelta;
            return true;
        }

        if (BindingMatches(binding, "Свечи", "candles"))
        {
            value = storyManager.LastCompletedEpisodeCandleDelta;
            return true;
        }

        return false;
    }

    static bool BindingMatches(StoryEndScreenStatBinding binding, string label, params string[] statIds)
    {
        if (binding == null)
            return false;

        if (binding.MatchesLabel(label))
            return true;

        foreach (string candidate in binding.AllStatIds())
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            for (int i = 0; i < statIds.Length; i++)
            {
                if (string.Equals(candidate, statIds[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    int ResolveCurrentStat(StoryEndScreenStatBinding binding, StoryEndScreenPreviewSettings previewSettings)
    {
        if (GameState.Instance == null)
        {
            ThrottledAppLogger.Warn(
                "EndScreenMissingGameState:" + (binding != null ? binding.statId : ""),
                AppLogCategory.EndScreen,
                nameof(StoryEndScreenDataProvider),
                nameof(ResolveCurrentStat),
                "GameState was not found. Preview/fallback stat value was used.",
                LogMetadata.Of("statId", binding != null ? binding.statId : ""));
            return ResolvePreviewValue(binding, previewSettings);
        }

        foreach (string statId in binding.AllStatIds())
        {
            int value = GameState.Instance.GetInt(statId);
            if (value != 0)
                return value;
        }

        return GameState.Instance.GetInt(binding.statId);
    }

    int ResolvePreviewValue(StoryEndScreenStatBinding binding, StoryEndScreenPreviewSettings previewSettings)
    {
        if (binding == null)
            return 0;

        if (previewSettings == null)
            return binding.previewValue;

        string label = (binding.label ?? "").Trim().ToLowerInvariant();
        string statId = (binding.statId ?? "").Trim().ToLowerInvariant();

        if (label == "город" || statId == "city" || statId == "town")
            return previewSettings.previewCity;
        if (label == "сказка" || statId == "fairytale" || statId == "story")
            return previewSettings.previewFairytale;
        if (label == "репутация" || statId == "reputation" || statId == "respect")
            return previewSettings.previewReputation;
        if (label == "искры" || statId == "hearts")
            return previewSettings.previewSparks;
        if (label == "свечи" || statId == "candles")
            return previewSettings.previewCandles;

        return binding.previewValue;
    }

    static string FormatValue(int value, string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return value.ToString(CultureInfo.InvariantCulture);

        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }
        catch (FormatException)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
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

    static string[] ToArray(IEnumerable<string> values)
    {
        if (values == null)
            return Array.Empty<string>();

        var result = new List<string>();
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
        }

        return result.ToArray();
    }

    static void LogDataBuilt(StoryEndScreenData data, StoryManager storyManager, bool preview)
    {
        AppLogger.DebugLog(
            AppLogCategory.EndScreen,
            nameof(StoryEndScreenDataProvider),
            nameof(Build),
            "Story end-screen data was built.",
            LogMetadata.Of(
                "preview", preview,
                "storyId", data != null ? data.StoryId : "",
                "completedEpisodeId", data != null ? data.CompletedEpisodeId : "",
                "statCount", data != null ? data.Stats.Count : 0,
                "stats", BuildStatSummary(data),
                "storyFinished", storyManager != null && storyManager.EndPanelStoryFinished));
    }

    static string BuildStatSummary(StoryEndScreenData data)
    {
        if (data == null || data.Stats == null || data.Stats.Count == 0)
            return "";

        var parts = new List<string>(data.Stats.Count);
        for (int i = 0; i < data.Stats.Count; i++)
        {
            StoryEndScreenStatValue stat = data.Stats[i];
            if (stat == null)
                continue;

            parts.Add((stat.StatId ?? stat.Label ?? "stat") + "=" + stat.Value.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(",", parts);
    }
}

public sealed class StoryEndScreenValidator : IStoryEndScreenValidator
{
    public StoryEndScreenValidationResult Validate(StoryEndScreenController controller, bool requireRuntime)
    {
        var result = new StoryEndScreenValidationResult();
        if (controller == null)
        {
            result.Error("Не назначен StoryEndScreenController.");
            return result;
        }

        StoryEndScreenReferences references = controller.References;
        if (references == null)
        {
            result.Error("Не назначен блок ссылок StoryEndScreenReferences.");
            return result;
        }

        if (references.ResolveRoot(controller) == null)
            result.Error("Не назначен root/panel финального экрана.");
        if (references.titleText == null && controller.TitleText == null)
            result.Warn("Не найден текст заголовка финального экрана.");
        if (references.statsContainer == null &&
            references.statRowTemplate == null &&
            references.legacyCityText == null &&
            controller.TownText == null)
        {
            result.Warn("Не найдены контейнер/шаблон/legacy-тексты для статов.");
        }
        if (references.continueButton == null &&
            references.nextEpisodeButton == null &&
            references.menuButton == null &&
            controller.ContinueButton == null &&
            controller.MenuButton == null)
            result.Warn("Кнопка «В меню» не назначена.");
        if (requireRuntime && controller.StoryManager == null)
            result.Warn("StoryManager не найден. Runtime-данные финального экрана будут неполными.");

        ValidateStatBindings(controller.StatBindings, result);
        return result;
    }

    static void ValidateStatBindings(IReadOnlyList<StoryEndScreenStatBinding> bindings, StoryEndScreenValidationResult result)
    {
        if (bindings == null || bindings.Count == 0)
        {
            result.Warn("Нет биндингов статов. Будет использован набор по умолчанию: Город, Сказка, Репутация, Искры.");
            return;
        }

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bindings.Count; i++)
        {
            StoryEndScreenStatBinding binding = bindings[i];
            if (binding == null || !binding.enabled)
                continue;

            if (string.IsNullOrWhiteSpace(binding.label))
                result.Warn("У одного из биндингов статов пустая подпись.");
            else if (!labels.Add(binding.label.Trim()))
                result.Warn("Дублируется стат финального экрана: " + binding.label.Trim());

            if (binding.valueMode == StoryEndScreenStatValueMode.CurrentTotal &&
                string.IsNullOrWhiteSpace(binding.statId))
            {
                result.Warn("У стата «" + binding.label + "» не указан statId.");
            }
        }
    }
}

public static class StoryEndScreenLayoutController
{
    public static void Recalculate(StoryEndScreenController controller, string reason)
    {
        if (controller == null)
            return;

        StoryEndScreenReferences references = controller.References;
        StoryEndScreenLayoutSettings settings = controller.LayoutSettings ?? new StoryEndScreenLayoutSettings();

        if (references != null && settings.keepTemplatesInactive && references.statRowTemplate != null)
            references.statRowTemplate.SetActive(false);

        RectTransform root = references != null ? references.ResolvePanelRoot(controller) : controller.GetComponent<RectTransform>();
        if (root != null && settings.stretchRootToScreen)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        if (references != null && references.safeArea != null && settings.useSafeAreaPadding)
        {
            references.safeArea.anchorMin = Vector2.zero;
            references.safeArea.anchorMax = Vector2.one;
            references.safeArea.offsetMin = new Vector2(settings.safeAreaPadding.x, settings.safeAreaPadding.w);
            references.safeArea.offsetMax = new Vector2(-settings.safeAreaPadding.z, -settings.safeAreaPadding.y);
        }

        if (references != null && references.statsContainer != null)
            ConfigureStatsContainer(references.statsContainer, settings);

        if (settings.forceRebuildLayout)
        {
            Canvas.ForceUpdateCanvases();
            if (root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            if (references != null && references.statsContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(references.statsContainer);
        }

        ThrottledAppLogger.Debug(
            "EndScreenLayout:" + reason,
            AppLogCategory.Layout,
            nameof(StoryEndScreenLayoutController),
            nameof(Recalculate),
            "Story end-screen layout was recalculated.",
            LogMetadata.Of("reason", reason ?? "", "object", controller.name),
            6d);
    }

    static void ConfigureStatsContainer(RectTransform container, StoryEndScreenLayoutSettings settings)
    {
        if (container == null || settings == null)
            return;

        VerticalLayoutGroup vertical = container.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
        {
            vertical.spacing = Mathf.Max(0f, settings.statsSpacing);
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.childAlignment = TextAnchor.MiddleCenter;
        }

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}

public sealed class StoryEndScreenPreviewRenderer : IStoryEndScreenRenderer
{
    public bool Render(StoryEndScreenController controller, StoryEndScreenData data, string reason)
    {
        if (controller == null || data == null)
            return false;

        data.IsPreview = true;
        controller.RenderData(data, animate: false, reason: reason ?? nameof(StoryEndScreenPreviewRenderer));
        return true;
    }
}

public sealed class StoryEndScreenRuntimePresenter
{
    public bool Show(StoryEndScreenController controller, StoryEndScreenData data, string reason)
    {
        if (controller == null || data == null)
            return false;

        data.IsPreview = false;
        controller.RenderData(data, animate: Application.isPlaying, reason: reason ?? nameof(StoryEndScreenRuntimePresenter));
        return true;
    }
}

public sealed class StoryEndScreenNavigationController : IStoryEndScreenNavigator
{
    public bool ReturnToMenu(StoryEndScreenController controller)
    {
        if (controller == null)
            return false;

        StoryManager storyManager = controller.StoryManager;
        if (storyManager != null)
        {
            storyManager.ReturnToMainMenu();
            return true;
        }

        MenuController menu = controller.MenuController;
        if (menu != null)
        {
            menu.ReturnToMenu(controller.CloseEndPanel);
            return true;
        }

        return false;
    }

    public bool ContinueOrReturnToMenu(StoryEndScreenController controller)
    {
        if (controller == null)
            return false;

        StoryManager storyManager = controller.StoryManager;
        if (storyManager != null && storyManager.CanContinueFromEndPanel)
        {
            storyManager.ContinueFromEndPanel();
            return true;
        }

        return ReturnToMenu(controller);
    }

    public bool ContinueStory(StoryEndScreenController controller)
    {
        return controller != null && controller.StoryManager != null && controller.StoryManager.ContinueFromEndPanel();
    }

    public bool RestartCompletedEpisode(StoryEndScreenController controller)
    {
        return controller != null && controller.StoryManager != null && controller.StoryManager.RestartCompletedChapterFromEndPanel();
    }

    public bool OpenScreen(StoryEndScreenController controller, string screenId)
    {
        return controller != null && controller.ScreenNavigator != null && controller.ScreenNavigator.OpenScreen(screenId);
    }
}

public static class StoryEndScreenBackgroundController
{
    public static void Apply(StoryEndScreenController controller, StoryEndScreenData data)
    {
        if (controller == null || data == null || controller.References == null)
            return;

        Image image = controller.References.backgroundImage;
        if (image == null)
            return;

        StoryEndScreenReferences references = controller.References;
        Sprite sprite = references.backgroundOverride != null
            ? references.backgroundOverride
            : data.Background != null
                ? data.Background
                : references.defaultBackground;

        if (sprite == null)
            return;

        image.sprite = sprite;
        image.enabled = true;
        image.preserveAspect = false;
    }
}

public static class StoryEndScreenTweenController
{
    public static void KillHierarchy(GameObject root)
    {
        if (root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
                continue;

            transforms[i].DOKill(false);
            Graphic graphic = transforms[i].GetComponent<Graphic>();
            if (graphic != null)
                graphic.DOKill(false);
            CanvasGroup group = transforms[i].GetComponent<CanvasGroup>();
            if (group != null)
                group.DOKill(false);
        }
    }

    public static void FadeIn(CanvasGroup canvasGroup, bool animate)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.DOKill(false);
        if (!animate)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1f, 0.24f).SetUpdate(true);
    }
}

public sealed class StoryEndScreenGeneratedRowMarker : MonoBehaviour
{
}
