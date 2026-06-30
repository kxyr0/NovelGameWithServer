using System;
using System.Collections.Generic;
using UnityEngine;

public enum CompletionState
{
    NotCompleted = 0,
    Completed = 1
}

public enum RewardReason
{
    None = 0,
    InvalidStoryId = 1,
    InvalidSeasonNumber = 2,
    DuplicateCompletionEvent = 3,
    FirstSeasonFirstCompletion = 10,
    FirstSeasonReplay = 11,
    LaterSeasonFirstCompletion = 20,
    LaterSeasonReplay = 21,
    SaveFailed = 90,
    CurrencyApplyFailed = 91
}

public sealed class StorySeasonRewardSettings
{
    public int FirstCompletionHearts = 20;
    public int FirstSeasonReplayHearts = 0;
    public int LaterSeasonReplayHearts = 3;

    public static StorySeasonRewardSettings Default => new StorySeasonRewardSettings();
}

public struct StorySeasonKey
{
    public readonly string StoryId;
    public readonly int SeasonNumber;

    public StorySeasonKey(string storyId, int seasonNumber)
    {
        StoryId = SaveDataSanitizer.SanitizeIdentifier(storyId);
        SeasonNumber = seasonNumber;
    }

    public bool IsValid => !string.IsNullOrEmpty(StoryId) && SeasonNumber > 0;

    public override string ToString()
    {
        return StoryId + ":season:" + SeasonNumber;
    }
}

[Serializable]
public sealed class StorySeasonCompletionState
{
    public string storyId;
    public int seasonNumber;
    public bool completedOnce;
    public int completionCount;
    public string activeRunId;
    public string lastRewardedRunId;
    public string updatedAtIso;

    public CompletionState CompletionState => completedOnce ? CompletionState.Completed : CompletionState.NotCompleted;

    public StorySeasonCompletionState Clone()
    {
        return new StorySeasonCompletionState
        {
            storyId = storyId,
            seasonNumber = seasonNumber,
            completedOnce = completedOnce,
            completionCount = completionCount,
            activeRunId = activeRunId,
            lastRewardedRunId = lastRewardedRunId,
            updatedAtIso = updatedAtIso
        };
    }

    public void Normalize(StorySeasonKey key)
    {
        storyId = key.StoryId;
        seasonNumber = key.SeasonNumber;
        completedOnce = completedOnce || completionCount > 0;
        completionCount = Mathf.Max(0, completionCount);
        activeRunId = SaveDataSanitizer.SanitizeIdentifier(activeRunId);
        lastRewardedRunId = SaveDataSanitizer.SanitizeIdentifier(lastRewardedRunId);
        updatedAtIso = SaveDataSanitizer.SanitizeSavedAtIso(updatedAtIso);
    }

    public static StorySeasonCompletionState Empty(StorySeasonKey key)
    {
        var state = new StorySeasonCompletionState();
        state.Normalize(key);
        return state;
    }
}

public sealed class StorySeasonProgressionSaveResult
{
    public bool Success;
    public string ErrorType;
    public string Message;

    public static StorySeasonProgressionSaveResult Ok()
    {
        return new StorySeasonProgressionSaveResult { Success = true };
    }

    public static StorySeasonProgressionSaveResult Fail(string errorType, string message)
    {
        return new StorySeasonProgressionSaveResult
        {
            Success = false,
            ErrorType = errorType ?? "",
            Message = message ?? ""
        };
    }
}

public interface IStorySeasonProgressionStore
{
    StorySeasonCompletionState Load(StorySeasonKey key);
    StorySeasonProgressionSaveResult Save(StorySeasonKey key, StorySeasonCompletionState state);
}

public interface IHeartCurrencyRewardGateway
{
    int Balance { get; }
    bool TryGrantHearts(int amount, string source, out int balanceBefore, out int balanceAfter, out string error);
}

public sealed class PlayerDataHeartCurrencyRewardGateway : IHeartCurrencyRewardGateway
{
    public int Balance => PlayerData.Hearts;

    public bool TryGrantHearts(int amount, string source, out int balanceBefore, out int balanceAfter, out string error)
    {
        error = "";
        balanceBefore = Balance;

        if (amount <= 0)
        {
            balanceAfter = balanceBefore;
            return true;
        }

        try
        {
            PlayerData.AddHeartValue(amount);
            balanceAfter = Balance;
            return balanceAfter >= balanceBefore;
        }
        catch (Exception exception)
        {
            balanceAfter = Balance;
            error = exception.Message;
            return false;
        }
    }
}

public sealed class CalculatedSeasonReward
{
    public int Amount;
    public RewardReason Reason;
    public bool FirstCompletion;
    public bool Replay;
}

public sealed class StorySeasonRewardCalculator
{
    readonly StorySeasonRewardSettings _settings;

    public StorySeasonRewardCalculator(StorySeasonRewardSettings settings = null)
    {
        _settings = settings ?? StorySeasonRewardSettings.Default;
    }

    public CalculatedSeasonReward Calculate(int seasonNumber, CompletionState previousState)
    {
        bool firstCompletion = previousState != CompletionState.Completed;
        bool firstSeason = seasonNumber == 1;

        if (firstCompletion)
        {
            return new CalculatedSeasonReward
            {
                Amount = _settings.FirstCompletionHearts,
                Reason = firstSeason
                    ? RewardReason.FirstSeasonFirstCompletion
                    : RewardReason.LaterSeasonFirstCompletion,
                FirstCompletion = true,
                Replay = false
            };
        }

        return new CalculatedSeasonReward
        {
            Amount = firstSeason ? _settings.FirstSeasonReplayHearts : _settings.LaterSeasonReplayHearts,
            Reason = firstSeason ? RewardReason.FirstSeasonReplay : RewardReason.LaterSeasonReplay,
            FirstCompletion = false,
            Replay = true
        };
    }
}

public sealed class StorySeasonCompletionRequest
{
    public string StoryId;
    public int SeasonNumber;
    public string CompletionRunId;
    public string SeasonId;
}

public sealed class StorySeasonRunResult
{
    public bool Success;
    public string StoryId;
    public int SeasonNumber;
    public string CompletionRunId;
    public StorySeasonProgressionSaveResult SaveResult;
}

public sealed class StorySeasonRewardResult
{
    public bool IsValid;
    public string StoryId;
    public int SeasonNumber;
    public string SeasonId;
    public string CompletionRunId;
    public int CalculatedAmount;
    public int RewardAmount;
    public RewardReason Reason;
    public bool FirstCompletion;
    public bool Replay;
    public CompletionState PreviousCompletionState;
    public CompletionState NewCompletionState;
    public int PreviousCompletionCount;
    public int NewCompletionCount;
    public int CurrencyBalanceBefore;
    public int CurrencyBalanceAfter;
    public bool SaveSucceeded;
    public string SaveError;
    public bool CurrencyApplied;
    public string CurrencyError;
}

public sealed class StorySeasonRewardService
{
    const string RewardSource = "StorySeasonCompletion";

    readonly IStorySeasonProgressionStore _progressionStore;
    readonly IHeartCurrencyRewardGateway _currencyGateway;
    readonly StorySeasonRewardCalculator _calculator;

    public StorySeasonRewardService(
        IStorySeasonProgressionStore progressionStore = null,
        IHeartCurrencyRewardGateway currencyGateway = null,
        StorySeasonRewardCalculator calculator = null)
    {
        _progressionStore = progressionStore ?? new LocalStorySeasonProgressionStore();
        _currencyGateway = currencyGateway ?? new PlayerDataHeartCurrencyRewardGateway();
        _calculator = calculator ?? new StorySeasonRewardCalculator();
    }

    public StorySeasonRunResult StartNewCompletionRun(string storyId, int seasonNumber, string source)
    {
        return BeginCompletionRun(storyId, seasonNumber, source, reuseExisting: false);
    }

    public StorySeasonRunResult RestoreOrStartCompletionRun(string storyId, int seasonNumber, string source)
    {
        return BeginCompletionRun(storyId, seasonNumber, source, reuseExisting: true);
    }

    public StorySeasonRewardResult CompleteSeason(StorySeasonCompletionRequest request)
    {
        StorySeasonKey key = CreateKey(request != null ? request.StoryId : "", request != null ? request.SeasonNumber : 0);
        string runId = SaveDataSanitizer.SanitizeIdentifier(request != null ? request.CompletionRunId : "");
        string seasonId = SaveDataSanitizer.SanitizeIdentifier(request != null ? request.SeasonId : "");

        var result = CreateBaseResult(key, runId, seasonId);
        LogInfo(
            AppLogCategory.StorySeasonReward,
            nameof(CompleteSeason),
            "[StorySeasonReward] Start reward evaluation.",
            LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "runId", runId, "seasonId", seasonId));

        if (!ValidateKey(key, result))
            return result;

        result.IsValid = true;

        StorySeasonCompletionState state = LoadState(key);
        state.activeRunId = string.IsNullOrEmpty(runId) ? state.activeRunId : runId;
        result.PreviousCompletionState = state.CompletionState;
        result.PreviousCompletionCount = state.completionCount;

        if (!string.IsNullOrEmpty(runId) &&
            string.Equals(state.lastRewardedRunId, runId, StringComparison.Ordinal))
        {
            result.Reason = RewardReason.DuplicateCompletionEvent;
            result.NewCompletionState = state.CompletionState;
            result.NewCompletionCount = state.completionCount;
            result.SaveSucceeded = true;
            result.CurrencyApplied = true;
            result.CurrencyBalanceBefore = _currencyGateway.Balance;
            result.CurrencyBalanceAfter = result.CurrencyBalanceBefore;
            LogInfo(
                AppLogCategory.StorySeasonReward,
                nameof(CompleteSeason),
                "[StorySeasonReward] Duplicate completion event ignored.",
                BuildResultMetadata(result, state, state));
            return result;
        }

        CalculatedSeasonReward reward = _calculator.Calculate(key.SeasonNumber, state.CompletionState);
        result.CalculatedAmount = reward.Amount;
        result.Reason = reward.Reason;
        result.FirstCompletion = reward.FirstCompletion;
        result.Replay = reward.Replay;

        StorySeasonCompletionState newState = state.Clone();
        newState.completedOnce = true;
        newState.completionCount = SaveDataSanitizer.ClampStatDelta(newState.completionCount, 1);
        newState.lastRewardedRunId = runId;
        newState.activeRunId = runId;
        newState.updatedAtIso = DateTime.UtcNow.ToString("o");
        newState.Normalize(key);

        LogInfo(
            AppLogCategory.StorySeasonReward,
            nameof(CompleteSeason),
            "[StorySeasonReward] Completion state evaluated.",
            LogMetadata.Of(
                "storyId", key.StoryId,
                "season", key.SeasonNumber,
                "firstCompletion", result.FirstCompletion,
                "replay", result.Replay,
                "previousCompletionState", result.PreviousCompletionState.ToString(),
                "newCompletionState", newState.CompletionState.ToString()));

        LogInfo(
            AppLogCategory.StorySeasonReward,
            nameof(CompleteSeason),
            "[StorySeasonReward] Reward calculated.",
            LogMetadata.Of(
                "storyId", key.StoryId,
                "season", key.SeasonNumber,
                "amount", reward.Amount,
                "currency", "hearts",
                "reason", reward.Reason.ToString()));

        StorySeasonProgressionSaveResult save = _progressionStore.Save(key, newState);
        result.SaveSucceeded = save != null && save.Success;
        result.SaveError = save != null ? save.Message : "Save result is missing.";
        if (!result.SaveSucceeded)
        {
            result.Reason = RewardReason.SaveFailed;
            result.NewCompletionState = state.CompletionState;
            result.NewCompletionCount = state.completionCount;
            result.CurrencyBalanceBefore = _currencyGateway.Balance;
            result.CurrencyBalanceAfter = result.CurrencyBalanceBefore;
            LogError(
                AppLogCategory.RewardSave,
                nameof(CompleteSeason),
                "[RewardSave] Progression state save failed; reward was not granted.",
                null,
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "errorType", save != null ? save.ErrorType : "missing_result",
                    "error", result.SaveError));
            return result;
        }

        LogInfo(
            AppLogCategory.RewardSave,
            nameof(CompleteSeason),
            "[RewardSave] Progression state saved successfully.",
            LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "saveResult", "success"));

        result.NewCompletionState = newState.CompletionState;
        result.NewCompletionCount = newState.completionCount;

        if (!_currencyGateway.TryGrantHearts(reward.Amount, RewardSource, out int before, out int after, out string currencyError))
        {
            result.Reason = RewardReason.CurrencyApplyFailed;
            result.CurrencyApplied = false;
            result.CurrencyError = currencyError;
            result.CurrencyBalanceBefore = before;
            result.CurrencyBalanceAfter = after;
            LogError(
                AppLogCategory.CurrencyReward,
                nameof(CompleteSeason),
                "[CurrencyReward] Hearts grant failed.",
                null,
                LogMetadata.Of(
                    "storyId", key.StoryId,
                    "season", key.SeasonNumber,
                    "amount", reward.Amount,
                    "currency", "hearts",
                    "source", RewardSource,
                    "balanceBefore", before,
                    "balanceAfter", after,
                    "error", currencyError));
            return result;
        }

        result.CurrencyApplied = true;
        result.CurrencyBalanceBefore = before;
        result.CurrencyBalanceAfter = after;
        result.RewardAmount = reward.Amount;

        LogInfo(
            AppLogCategory.CurrencyReward,
            nameof(CompleteSeason),
            "[CurrencyReward] Hearts granted.",
            LogMetadata.Of(
                "storyId", key.StoryId,
                "season", key.SeasonNumber,
                "amount", reward.Amount,
                "currency", "hearts",
                "source", RewardSource,
                "balanceBefore", before,
                "balanceAfter", after));

        LogInfo(
            AppLogCategory.StorySeasonReward,
            nameof(CompleteSeason),
            "[StorySeasonReward] Completed reward flow.",
            BuildResultMetadata(result, state, newState));

        return result;
    }

    StorySeasonRunResult BeginCompletionRun(string storyId, int seasonNumber, string source, bool reuseExisting)
    {
        StorySeasonKey key = CreateKey(storyId, seasonNumber);
        var result = new StorySeasonRunResult
        {
            Success = false,
            StoryId = key.StoryId,
            SeasonNumber = key.SeasonNumber,
            SaveResult = StorySeasonProgressionSaveResult.Fail("not_started", "Run was not started.")
        };

        if (!key.IsValid)
        {
            LogValidationFailure(key, key.IsValid ? RewardReason.None : ResolveValidationReason(key));
            return result;
        }

        StorySeasonCompletionState state = LoadState(key);
        if (reuseExisting && !string.IsNullOrEmpty(state.activeRunId))
        {
            result.Success = true;
            result.CompletionRunId = state.activeRunId;
            result.SaveResult = StorySeasonProgressionSaveResult.Ok();
            return result;
        }

        state.activeRunId = Guid.NewGuid().ToString("N");
        state.updatedAtIso = DateTime.UtcNow.ToString("o");
        state.Normalize(key);

        StorySeasonProgressionSaveResult save = _progressionStore.Save(key, state);
        result.Success = save != null && save.Success;
        result.CompletionRunId = state.activeRunId;
        result.SaveResult = save;

        LogInfo(
            AppLogCategory.StoryProgression,
            nameof(BeginCompletionRun),
            "[StoryProgression] Season completion run prepared.",
            LogMetadata.Of(
                "storyId", key.StoryId,
                "season", key.SeasonNumber,
                "runId", state.activeRunId,
                "source", SaveDataSanitizer.SanitizeIdentifier(source),
                "reused", false,
                "saveResult", result.Success ? "success" : "failed"));

        return result;
    }

    StorySeasonCompletionState LoadState(StorySeasonKey key)
    {
        StorySeasonCompletionState state = _progressionStore.Load(key) ?? StorySeasonCompletionState.Empty(key);
        state.Normalize(key);
        return state;
    }

    static StorySeasonKey CreateKey(string storyId, int seasonNumber)
    {
        return new StorySeasonKey(storyId, seasonNumber);
    }

    bool ValidateKey(StorySeasonKey key, StorySeasonRewardResult result)
    {
        if (key.IsValid)
            return true;

        RewardReason reason = ResolveValidationReason(key);
        result.Reason = reason;
        result.CurrencyBalanceBefore = _currencyGateway.Balance;
        result.CurrencyBalanceAfter = result.CurrencyBalanceBefore;
        LogValidationFailure(key, reason);
        return false;
    }

    static RewardReason ResolveValidationReason(StorySeasonKey key)
    {
        return string.IsNullOrEmpty(key.StoryId)
            ? RewardReason.InvalidStoryId
            : RewardReason.InvalidSeasonNumber;
    }

    static StorySeasonRewardResult CreateBaseResult(StorySeasonKey key, string runId, string seasonId)
    {
        return new StorySeasonRewardResult
        {
            StoryId = key.StoryId,
            SeasonNumber = key.SeasonNumber,
            SeasonId = seasonId,
            CompletionRunId = runId,
            Reason = RewardReason.None,
            PreviousCompletionState = CompletionState.NotCompleted,
            NewCompletionState = CompletionState.NotCompleted
        };
    }

    static IDictionary<string, object> BuildResultMetadata(
        StorySeasonRewardResult result,
        StorySeasonCompletionState previousState,
        StorySeasonCompletionState newState)
    {
        return LogMetadata.Of(
            "storyId", result.StoryId,
            "season", result.SeasonNumber,
            "runId", result.CompletionRunId,
            "firstCompletion", result.FirstCompletion,
            "replay", result.Replay,
            "amount", result.RewardAmount,
            "calculatedAmount", result.CalculatedAmount,
            "currency", "hearts",
            "reason", result.Reason.ToString(),
            "previousCompletionState", previousState != null ? previousState.CompletionState.ToString() : CompletionState.NotCompleted.ToString(),
            "newCompletionState", newState != null ? newState.CompletionState.ToString() : CompletionState.NotCompleted.ToString(),
            "previousCompletionCount", previousState != null ? previousState.completionCount : 0,
            "newCompletionCount", newState != null ? newState.completionCount : 0,
            "balanceBefore", result.CurrencyBalanceBefore,
            "balanceAfter", result.CurrencyBalanceAfter,
            "saveResult", result.SaveSucceeded ? "success" : "failed");
    }

    static void LogValidationFailure(StorySeasonKey key, RewardReason reason)
    {
        LogWarn(
            AppLogCategory.StorySeasonReward,
            nameof(CompleteSeason),
            "[StorySeasonReward] Validation failed; reward was not granted.",
            LogMetadata.Of("storyId", key.StoryId, "season", key.SeasonNumber, "reason", reason.ToString()));
    }

    static void LogInfo(string category, string operation, string message, IDictionary<string, object> metadata)
    {
        AppLogger.Info(category, nameof(StorySeasonRewardService), operation, message, metadata);
    }

    static void LogWarn(string category, string operation, string message, IDictionary<string, object> metadata)
    {
        AppLogger.Warn(category, nameof(StorySeasonRewardService), operation, message, metadata, recoverable: true);
    }

    static void LogError(
        string category,
        string operation,
        string message,
        Exception exception,
        IDictionary<string, object> metadata)
    {
        AppLogger.Error(category, nameof(StorySeasonRewardService), operation, message, exception, metadata, recoverable: true);
    }
}
