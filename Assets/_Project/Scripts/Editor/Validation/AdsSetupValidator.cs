using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AdsSetupValidator
{
    private const string ValidateMenuPath = "Tools/Novel Template/Ads/Validate Setup";
    private const string AdsConfigPath = "Assets/_Project/Resources/Ads/AdsConfig.asset";
    private const string RewardedPlacementId = "rewarded_bonus";

    [MenuItem(ValidateMenuPath)]
    public static void ValidateSetupMenu()
    {
        AdsSetupReport report = Validate();
        report.LogToConsole();
    }

    public static AdsSetupReport Validate()
    {
        var report = new AdsSetupReport();
        string config = ReadProjectFile(AdsConfigPath);
        if (string.IsNullOrEmpty(config))
        {
            report.AddError("AdsConfig.asset is missing: " + AdsConfigPath);
            return report;
        }

        bool adsEnabled = ExtractBool(config, "_adsEnabled");
        bool autoCreate = ExtractBool(config, "_autoCreateRuntimeService");
        string androidAppKey = ExtractYamlValue(config, "_androidAppKey");
        string rewardedAdUnitId = ExtractRewardedAndroidAdUnitId(config, RewardedPlacementId);

        if (!adsEnabled)
            report.AddError("AdsConfig has _adsEnabled disabled. Rewarded button will not show ads.");

        if (!autoCreate)
            report.AddError("AdsConfig has _autoCreateRuntimeService disabled. Add an ads service to the scene or enable auto creation.");

        if (string.IsNullOrWhiteSpace(rewardedAdUnitId))
            report.AddError("AdsConfig has no Android ad unit for rewarded_bonus.");
        else if (IsPlaceholder(rewardedAdUnitId))
            report.AddError("AdsConfig rewarded_bonus Android ad unit is still a placeholder. Set a real LevelPlay rewarded ad unit before Android build.");

        if (string.IsNullOrWhiteSpace(androidAppKey))
            report.AddError("AdsConfig Android LevelPlay app key is empty. Set a real LevelPlay app key before Android build.");
        else if (IsPlaceholder(androidAppKey))
            report.AddError("AdsConfig Android LevelPlay app key is still a placeholder. Set a real LevelPlay app key before Android build.");

        return report;
    }

    private static string ReadProjectFile(string relativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static bool ExtractBool(string yaml, string key)
    {
        string value = ExtractYamlValue(yaml, key);
        return value == "1" || value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractYamlValue(string yaml, string key)
    {
        Match match = Regex.Match(
            yaml,
            @"^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string ExtractRewardedAndroidAdUnitId(string yaml, string placementId)
    {
        Match match = Regex.Match(
            yaml,
            @"-\s*_placementId:\s*" + Regex.Escape(placementId) + @"\s*[\s\S]*?^\s*_androidAdUnitId:\s*(.*?)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.Trim();
        return normalized.StartsWith("YOUR_", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("ANDROID_", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("_AD_UNIT_ID", System.StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AdsSetupReport
{
    private readonly List<string> _errors = new List<string>();
    private readonly List<string> _warnings = new List<string>();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool HasErrors => _errors.Count > 0;

    public void AddError(string message)
    {
        _errors.Add(message);
    }

    public void AddWarning(string message)
    {
        _warnings.Add(message);
    }

    public string BuildFailureMessage()
    {
        if (!HasErrors)
            return "";

        return "Ads setup is incomplete:\n- " + string.Join("\n- ", _errors);
    }

    public void LogToConsole()
    {
        foreach (string warning in _warnings)
            Debug.LogWarning("[Ads] " + warning);

        foreach (string error in _errors)
            Debug.LogError("[Ads] " + error);

        if (HasErrors)
            Debug.LogError("[Ads] Ads setup is incomplete.");
        else if (_warnings.Count > 0)
            Debug.LogWarning("[Ads] Ads setup works in Editor mock, but real Android values still need attention.");
        else
            Debug.Log("[Ads] Ads setup is valid.");
    }
}

public sealed class AdsBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.Android)
            return;

        AdsSetupReport validation = AdsSetupValidator.Validate();
        if (validation.HasErrors)
            throw new BuildFailedException(validation.BuildFailureMessage());
    }
}
