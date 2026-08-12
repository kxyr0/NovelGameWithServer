using System;
using UnityEngine;

public sealed partial class NetworkManager
{
    private const string DefaultProfileDisplayName = "Гость";
    private const string DefaultProfilePlayerId = "999-999";
    private const string LocalProfileDisplayNameKey = "VN_PROFILE_DISPLAY_NAME";

    public static event Action OnProfileUpdated;

    public static bool SetLocalProfileDisplayName(string displayName)
    {
        string safeName = SaveDataSanitizer.SanitizePlayerName(displayName);
        if (string.IsNullOrWhiteSpace(safeName))
            return false;

        _currentProfile.displayName = safeName;
        SaveLocalProfileDisplayName(safeName);
        NotifyProfileUpdated();
        return true;
    }

    private static void ApplyProfileIdentity(AuthResponse response, string rawJson)
    {
        string displayName = response != null && response.profile != null
            ? response.profile.displayName
            : NetworkJson.GetString(rawJson, "displayName");

        string safeName = SaveDataSanitizer.SanitizePlayerName(displayName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = LoadLocalProfileDisplayName();
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = DefaultProfileDisplayName;
        else
            SaveLocalProfileDisplayName(safeName);

        string safeId = SaveDataSanitizer.SanitizeIdentifier(_playerId);
        _currentProfile.displayName = safeName;
        _currentProfile.playerId = string.IsNullOrWhiteSpace(safeId)
            ? DefaultProfilePlayerId
            : safeId;
    }

    private static string LoadLocalProfileDisplayName()
    {
        return SaveDataSanitizer.SanitizePlayerName(
            PlayerPrefs.GetString(LocalProfileDisplayNameKey, ""));
    }

    private static void SaveLocalProfileDisplayName(string displayName)
    {
        PlayerPrefs.SetString(LocalProfileDisplayNameKey, displayName);
        PlayerPrefs.Save();
    }

    private static void NotifyProfileUpdated()
    {
        try
        {
            OnProfileUpdated?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

internal sealed partial class AuthProfile
{
    public string displayName;
}

public partial class PlayerProfileState
{
    public string displayName;
}
