using System;
using UnityEngine;

[Serializable]
public sealed class AdRewardResult
{
    [SerializeField] private AdRewardStatus _status;
    [SerializeField] private string _placementId;
    [SerializeField] private string _errorMessage;
    [SerializeField] private string _rewardName;
    [SerializeField] private int _rewardAmount;

    public AdRewardStatus Status => _status;
    public string PlacementId => _placementId ?? "";
    public string ErrorMessage => _errorMessage ?? "";
    public string RewardName => _rewardName ?? "";
    public int RewardAmount => _rewardAmount;
    public bool Success => _status == AdRewardStatus.Success;

    public static AdRewardResult Create(
        AdRewardStatus status,
        string placementId,
        string errorMessage = "",
        string rewardName = "",
        int rewardAmount = 0)
    {
        return new AdRewardResult
        {
            _status = status,
            _placementId = placementId ?? "",
            _errorMessage = errorMessage ?? "",
            _rewardName = rewardName ?? "",
            _rewardAmount = Math.Max(0, rewardAmount)
        };
    }
}
