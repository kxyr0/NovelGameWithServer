using System;
using System.Collections;
using UnityEngine;

public sealed partial class AdScreenController
{
    public void WatchRewardedAd()
    {
        if (_busy)
            return;
        if (AdDailyLimitStore.GetRemainingToday(_dailyLimit) <= 0)
        {
            RefreshState();
            return;
        }

        YandexRewardedAdsService service = ResolveAdsService();
        if (service == null)
        {
            Fail("Сервис рекламы не найден.");
            return;
        }
        if (!service.IsRewardedReady(_placementId))
        {
            service.Initialize();
            service.LoadRewarded(_placementId);
            SetStatus(_loadingText);
            RefreshState();
            return;
        }

        _busy = true;
        SetStatus("");
        RefreshState();
        service.ShowRewarded(_placementId, HandleAdCompleted);
    }

    private void HandleAdCompleted(AdRewardResult result)
    {
        if (result == null || !result.Success)
        {
            _busy = false;
            Fail(GetFailureMessage(result));
            ResolveAdsService()?.LoadRewarded(_placementId);
            return;
        }

        if (_useServerAuthoritativeReward)
        {
            if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
            {
                _busy = false;
                Fail("Серверная сессия не готова. Награда не подтверждена.");
                return;
            }
            NetworkManager.Instance.StartCoroutine(ClaimServerReward());
            return;
        }

        GrantLocalReward();
    }

    private void GrantLocalReward()
    {
        if (_rewardCurrency == AdRewardCurrency.Rubies)
            PlayerData.AddHeartValue(_rewardAmount);
        else
            PlayerData.AddCandlesValue(_rewardAmount);
        AdDailyLimitStore.CommitReward(_dailyLimit);
        CurrencyBar.Instance?.Refresh(true);
        CompleteGrant(_rewardAmount);
    }

    private IEnumerator ClaimServerReward()
    {
        int balanceBefore = GetRewardBalance();
        bool success = false;
        string payload = "";
        yield return NetworkManager.Instance.ClaimAdReward((ok, response) =>
        {
            success = ok;
            payload = response ?? "";
        });

        int granted = ResolveServerReward(payload, balanceBefore, GetRewardBalance());
        if (!success || granted < _rewardAmount)
        {
            _busy = false;
            Fail(success
                ? "Сервер не выдал выбранную награду. Проверьте контракт /player/ad/reward."
                : "Сервер не подтвердил награду.");
            yield break;
        }

        AdDailyLimitStore.CommitReward(_dailyLimit);
        CurrencyBar.Instance?.Refresh(true);
        CompleteGrant(granted);
    }

    private void CompleteGrant(int amount)
    {
        _busy = false;
        SetStatus(Format(GetRewardText(), amount));
        _rewardGranted.Invoke(amount);
        ResolveAdsService()?.LoadRewarded(_placementId);
        RefreshState();
    }

    private void Fail(string message)
    {
        SetStatus(message);
        _rewardFailed.Invoke();
        RefreshState();
    }

    private int GetRewardBalance()
    {
        return _rewardCurrency == AdRewardCurrency.Rubies ? PlayerData.Hearts : PlayerData.Candles;
    }

    private int ResolveServerReward(string json, int before, int after)
    {
        int delta = Math.Max(0, after - before);
        string directKey = _rewardCurrency == AdRewardCurrency.Rubies ? "heartsReward" : "candlesReward";
        string nestedKey = _rewardCurrency == AdRewardCurrency.Rubies ? "hearts" : "candles";
        int direct = NetworkJson.GetInt(json, directKey, 0);
        string reward = NetworkJson.GetRawValue(json, "reward");
        int nested = !string.IsNullOrWhiteSpace(reward) && NetworkJson.LooksLikeJsonObject(reward)
            ? NetworkJson.GetInt(reward, nestedKey, 0)
            : 0;
        return Math.Max(delta, Math.Max(direct, nested));
    }

    private static string GetFailureMessage(AdRewardResult result)
    {
        if (result == null)
            return "Реклама завершилась без награды.";
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            return result.ErrorMessage;
        if (result.Status == AdRewardStatus.ClosedWithoutReward)
            return "Ролик закрыт до получения награды.";
        return "Награда за рекламу не получена.";
    }
}
