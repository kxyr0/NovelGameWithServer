using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class StorySeasonRewardServiceTests
{
    [Test]
    public void FirstCompletion_StoryASeason1_Grants20Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        StorySeasonRewardResult result = Complete(service, "story_a", 1, "run_1");

        Assert.That(result.RewardAmount, Is.EqualTo(20));
        Assert.That(result.Reason, Is.EqualTo(RewardReason.FirstSeasonFirstCompletion));
        Assert.That(gateway.Balance, Is.EqualTo(20));
    }

    [Test]
    public void Replay_StoryASeason1_Grants0Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        Complete(service, "story_a", 1, "run_1");
        StorySeasonRewardResult replay = Complete(service, "story_a", 1, "run_2");

        Assert.That(replay.RewardAmount, Is.EqualTo(0));
        Assert.That(replay.Reason, Is.EqualTo(RewardReason.FirstSeasonReplay));
        Assert.That(gateway.Balance, Is.EqualTo(20));
    }

    [Test]
    public void FirstCompletion_StoryASeason2_Grants20Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        StorySeasonRewardResult result = Complete(service, "story_a", 2, "run_1");

        Assert.That(result.RewardAmount, Is.EqualTo(20));
        Assert.That(result.Reason, Is.EqualTo(RewardReason.LaterSeasonFirstCompletion));
        Assert.That(gateway.Balance, Is.EqualTo(20));
    }

    [Test]
    public void Replay_StoryASeason2_Grants3Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        Complete(service, "story_a", 2, "run_1");
        StorySeasonRewardResult replay = Complete(service, "story_a", 2, "run_2");

        Assert.That(replay.RewardAmount, Is.EqualTo(3));
        Assert.That(replay.Reason, Is.EqualTo(RewardReason.LaterSeasonReplay));
        Assert.That(gateway.Balance, Is.EqualTo(23));
    }

    [Test]
    public void FirstCompletion_StoryBSeason1_IsIndependentFromStoryA()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        Complete(service, "story_a", 1, "run_a_1");
        StorySeasonRewardResult storyB = Complete(service, "story_b", 1, "run_b_1");

        Assert.That(storyB.RewardAmount, Is.EqualTo(20));
        Assert.That(storyB.Reason, Is.EqualTo(RewardReason.FirstSeasonFirstCompletion));
        Assert.That(gateway.Balance, Is.EqualTo(40));
    }

    [Test]
    public void DuplicateCompletionCalls_DoNotGrantDuplicateFirstCompletionReward()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        StorySeasonRewardResult first = Complete(service, "story_a", 2, "same_run");
        StorySeasonRewardResult duplicate = Complete(service, "story_a", 2, "same_run");

        Assert.That(first.RewardAmount, Is.EqualTo(20));
        Assert.That(duplicate.RewardAmount, Is.EqualTo(0));
        Assert.That(duplicate.Reason, Is.EqualTo(RewardReason.DuplicateCompletionEvent));
        Assert.That(gateway.Balance, Is.EqualTo(20));
    }

    [Test]
    public void InvalidStoryId_Grants0Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        StorySeasonRewardResult result = Complete(service, "", 1, "run_1");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.RewardAmount, Is.EqualTo(0));
        Assert.That(result.Reason, Is.EqualTo(RewardReason.InvalidStoryId));
        Assert.That(gateway.Balance, Is.EqualTo(0));
    }

    [Test]
    public void InvalidSeasonNumber_Grants0Hearts()
    {
        var gateway = new FakeHeartGateway();
        var service = CreateService(gateway: gateway);

        StorySeasonRewardResult result = Complete(service, "story_a", 0, "run_1");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.RewardAmount, Is.EqualTo(0));
        Assert.That(result.Reason, Is.EqualTo(RewardReason.InvalidSeasonNumber));
        Assert.That(gateway.Balance, Is.EqualTo(0));
    }

    [Test]
    public void CompletionState_PersistsAfterSaveLoad()
    {
        string prefix = "TEST_STORY_SEASON_" + Guid.NewGuid().ToString("N") + "_";
        var store = new LocalStorySeasonProgressionStore(prefix);
        var key = new StorySeasonKey("persist_story", 2);

        try
        {
            StorySeasonProgressionSaveResult save = store.Save(key, new StorySeasonCompletionState
            {
                completedOnce = true,
                completionCount = 1,
                activeRunId = "run_1",
                lastRewardedRunId = "run_1",
                updatedAtIso = DateTime.UtcNow.ToString("o")
            });

            StorySeasonCompletionState loaded = new LocalStorySeasonProgressionStore(prefix).Load(key);

            Assert.That(save.Success, Is.True, save.Message);
            Assert.That(loaded.completedOnce, Is.True);
            Assert.That(loaded.completionCount, Is.EqualTo(1));
            Assert.That(loaded.lastRewardedRunId, Is.EqualTo("run_1"));
        }
        finally
        {
            DeleteStoreKey(prefix, key);
        }
    }

    [Test]
    public void CompletionState_SaveUsesProtectedPlayerPrefsPayload()
    {
        string prefix = "TEST_STORY_SEASON_PROTECTED_" + Guid.NewGuid().ToString("N") + "_";
        var store = new LocalStorySeasonProgressionStore(prefix);
        var key = new StorySeasonKey("protected_story", 2);
        string prefsKey = GetStorePrefsKey(prefix, key);

        try
        {
            StorySeasonProgressionSaveResult save = store.Save(key, new StorySeasonCompletionState
            {
                completedOnce = true,
                completionCount = 1,
                activeRunId = "run_1",
                lastRewardedRunId = "run_1",
                updatedAtIso = DateTime.UtcNow.ToString("o")
            });

            string stored = PlayerPrefs.GetString(prefsKey, "");
            bool unprotected = LocalSaveSecurity.TryUnprotectText(
                stored,
                LocalSaveSecurity.StoryProgressionPurpose + ":" + key.StoryId + ":season:" + key.SeasonNumber,
                out string json,
                out bool wasProtected);

            Assert.That(save.Success, Is.True, save.Message);
            Assert.That(stored, Is.Not.Empty);
            Assert.That(stored, Does.Contain("nocturne-local-secure-v1"));
            Assert.That(stored.Contains("\"completedOnce\""), Is.False);
            Assert.That(stored.Contains("\"completionCount\""), Is.False);
            Assert.That(LocalSecurePrefs.HasSecureMarker(prefsKey), Is.True);
            Assert.That(unprotected, Is.True);
            Assert.That(wasProtected, Is.True);
            Assert.That(json, Does.Contain("\"completedOnce\""));
        }
        finally
        {
            DeleteStoreKey(prefix, key);
        }
    }

    [Test]
    public void RewardCalculation_IsDeterministicAndIndependentFromUiState()
    {
        var calculator = new StorySeasonRewardCalculator();

        CalculatedSeasonReward first = calculator.Calculate(2, CompletionState.NotCompleted);
        CalculatedSeasonReward replay = calculator.Calculate(2, CompletionState.Completed);
        CalculatedSeasonReward firstAgain = calculator.Calculate(2, CompletionState.NotCompleted);

        Assert.That(first.Amount, Is.EqualTo(20));
        Assert.That(replay.Amount, Is.EqualTo(3));
        Assert.That(firstAgain.Amount, Is.EqualTo(first.Amount));
        Assert.That(firstAgain.Reason, Is.EqualTo(first.Reason));
    }

    static StorySeasonRewardService CreateService(
        InMemoryStorySeasonProgressionStore store = null,
        FakeHeartGateway gateway = null)
    {
        return new StorySeasonRewardService(
            store ?? new InMemoryStorySeasonProgressionStore(),
            gateway ?? new FakeHeartGateway(),
            new StorySeasonRewardCalculator());
    }

    static StorySeasonRewardResult Complete(
        StorySeasonRewardService service,
        string storyId,
        int seasonNumber,
        string runId)
    {
        return service.CompleteSeason(new StorySeasonCompletionRequest
        {
            StoryId = storyId,
            SeasonNumber = seasonNumber,
            SeasonId = storyId + "_season_" + seasonNumber,
            CompletionRunId = runId
        });
    }

    static void DeleteStoreKey(string prefix, StorySeasonKey key)
    {
        string prefsKey = GetStorePrefsKey(prefix, key);
        PlayerPrefs.DeleteKey(prefsKey);
        PlayerPrefs.DeleteKey(SaveDataSanitizer.SafeKeyPart(prefsKey, "pref", 96) + "__SECURE_V1");
        PlayerPrefs.Save();
    }

    static string GetStorePrefsKey(string prefix, StorySeasonKey key)
    {
        return prefix + SaveDataSanitizer.SafeKeyPart(key.StoryId, "story", 80) + "_S" + key.SeasonNumber;
    }

    sealed class InMemoryStorySeasonProgressionStore : IStorySeasonProgressionStore
    {
        readonly Dictionary<string, StorySeasonCompletionState> _states = new Dictionary<string, StorySeasonCompletionState>();

        public StorySeasonCompletionState Load(StorySeasonKey key)
        {
            if (_states.TryGetValue(key.ToString(), out StorySeasonCompletionState state))
                return state.Clone();

            return StorySeasonCompletionState.Empty(key);
        }

        public StorySeasonProgressionSaveResult Save(StorySeasonKey key, StorySeasonCompletionState state)
        {
            _states[key.ToString()] = state != null ? state.Clone() : StorySeasonCompletionState.Empty(key);
            return StorySeasonProgressionSaveResult.Ok();
        }
    }

    sealed class FakeHeartGateway : IHeartCurrencyRewardGateway
    {
        public int Balance { get; private set; }

        public bool TryGrantHearts(int amount, string source, out int balanceBefore, out int balanceAfter, out string error)
        {
            error = "";
            balanceBefore = Balance;
            Balance += amount;
            balanceAfter = Balance;
            return true;
        }
    }
}
