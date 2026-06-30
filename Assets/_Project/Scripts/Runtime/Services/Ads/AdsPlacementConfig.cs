using System;
using UnityEngine;

[Serializable]
public sealed class AdsPlacementConfig
{
    [SerializeField] private string _placementId = "";
    [SerializeField] private string _androidAdUnitId = "";
    [SerializeField] private string _iosAdUnitId = "";
    [SerializeField] private string _placementName = "";

    public AdsPlacementConfig()
    {
    }

    public AdsPlacementConfig(string placementId, string androidAdUnitId, string iosAdUnitId, string placementName)
    {
        _placementId = placementId;
        _androidAdUnitId = androidAdUnitId;
        _iosAdUnitId = iosAdUnitId;
        _placementName = placementName;
    }

    public string PlacementId => Clean(_placementId);
    public string AndroidAdUnitId => Clean(_androidAdUnitId);
    public string IosAdUnitId => Clean(_iosAdUnitId);
    public string PlacementName => FirstNonEmpty(_placementName, _placementId);
    public string ConfigKey => FirstNonEmpty(_placementId, _placementName, GetAdUnitId());

    public string GetAdUnitId()
    {
#if UNITY_IOS
        return IosAdUnitId;
#else
        return AndroidAdUnitId;
#endif
    }

    public bool HasAdUnitForCurrentPlatform => !string.IsNullOrWhiteSpace(GetAdUnitId());

    public bool Matches(string placementId)
    {
        placementId = Clean(placementId);
        if (string.IsNullOrEmpty(placementId))
            return false;

        return EqualsToken(placementId, PlacementId) ||
               EqualsToken(placementId, PlacementName) ||
               EqualsToken(placementId, AndroidAdUnitId) ||
               EqualsToken(placementId, IosAdUnitId);
    }

    private static bool EqualsToken(string left, string right)
    {
        return string.Equals(Clean(left), Clean(right), StringComparison.Ordinal);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            string clean = Clean(value);
            if (!string.IsNullOrEmpty(clean))
                return clean;
        }

        return "";
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
