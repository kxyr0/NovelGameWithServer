using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static int Hearts { get; private set; }
    public static int Candles { get; private set; }

    public static event Action<int> HeartsChanged;
    public static event Action<int> CandlesChanged;
    public static event Action BalanceChanged;

    const string HeartsKey = "Hearts";
    const string CandlesKey = "Candles";

    static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Hearts = 0;
        Candles = 0;
        _loaded = false;
        HeartsChanged = null;
        CandlesChanged = null;
        BalanceChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnStartup()
    {
        EnsureLoaded();
    }

    public static void AddHeartValue(int delta)
    {
        EnsureLoaded();
        Hearts = SaveDataSanitizer.ClampCurrencyDelta(Hearts, delta);
        SaveAndNotifyHearts();
    }

    /// <summary>
    /// Установить абсолютное значение искр (Hearts).
    /// Используется при синхронизации с сервером.
    /// </summary>
    public static void SetHeartsValue(int absoluteValue)
    {
        EnsureLoaded();
        Hearts = SaveDataSanitizer.ClampCurrencyValue(absoluteValue);
        SaveAndNotifyHearts();
    }


    /// <summary>
    /// Добавить/вычесть свечи (дельта).
    /// Используй для изменений: +10, -50 и т.д.
    /// </summary>
    public static void AddCandlesValue(int delta)
    {
        EnsureLoaded();
        Candles = SaveDataSanitizer.ClampCurrencyDelta(Candles, delta);
        SaveAndNotifyCandles();
    }

    /// <summary>
    /// Установить абсолютное значение свечей.
    /// Используется из GameState.currency = X.
    /// </summary>
    public static void SetCandlesValue(int absoluteValue)
    {
        EnsureLoaded();
        Candles = SaveDataSanitizer.ClampCurrencyValue(absoluteValue);
        SaveAndNotifyCandles();
    }

    public static void SetBalanceValues(int hearts, int candles)
    {
        EnsureLoaded();
        Hearts = SaveDataSanitizer.ClampCurrencyValue(hearts);
        Candles = SaveDataSanitizer.ClampCurrencyValue(candles);
        SaveAndNotifyBalance();
    }

    static void SaveAndNotifyBalance()
    {
        TrySaveProtectedInt(HeartsKey, Hearts);
        TrySaveProtectedInt(CandlesKey, Candles);

        if (ItemsController.Instance != null)
        {
            ItemsController.Instance.SetHearts(Hearts);
            ItemsController.Instance.SetCandles(Candles);
        }

        HeartsChanged?.Invoke(Hearts);
        CandlesChanged?.Invoke(Candles);
        BalanceChanged?.Invoke();
    }
    static void SaveAndNotifyCandles()
    {
        TrySaveProtectedInt(CandlesKey, Candles);

        if (ItemsController.Instance != null)
            ItemsController.Instance.SetCandles(Candles);

        CandlesChanged?.Invoke(Candles);
        BalanceChanged?.Invoke();
    }

    static void SaveAndNotifyHearts()
    {
        TrySaveProtectedInt(HeartsKey, Hearts);

        if (ItemsController.Instance != null)
            ItemsController.Instance.SetHearts(Hearts);

        HeartsChanged?.Invoke(Hearts);
        BalanceChanged?.Invoke();
    }

    private void Start()
    {
        EnsureLoaded();

        if (ItemsController.Instance != null)
        {
            ItemsController.Instance.SetHearts(Hearts);
            ItemsController.Instance.SetCandles(Candles);
        }
    }

    static void EnsureLoaded()
    {
        if (!_loaded)
            LoadFromPrefs();
    }

    static void LoadFromPrefs()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Hearts = SaveDataSanitizer.ClampCurrencyValue(TryGetProtectedInt(HeartsKey, 0));
        Candles = SaveDataSanitizer.ClampCurrencyValue(TryGetProtectedInt(CandlesKey, 0));
#else
        Hearts = 0;
        Candles = 0;
#endif
        _loaded = true;
    }

    static int TryGetProtectedInt(string key, int defaultValue)
    {
        try
        {
            string purpose = GetCurrencyPurpose(key);
            AppLogger.Info(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TryGetProtectedInt),
                "[CurrencyReward] Loading protected player currency.",
                LogMetadata.Of(
                    "currencyKey", SaveDataSanitizer.SafeKeyPart(key),
                    "purpose", purpose,
                    "defaultValue", defaultValue,
                    "secureMarker", LocalSecurePrefs.HasSecureMarker(key)));

            int value = SaveDataSanitizer.ClampCurrencyValue(LocalSecurePrefs.GetInt(key, purpose, defaultValue));

            AppLogger.Info(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TryGetProtectedInt),
                "[CurrencyReward] Protected player currency loaded.",
                LogMetadata.Of(
                    "currencyKey", SaveDataSanitizer.SafeKeyPart(key),
                    "purpose", purpose,
                    "value", value,
                    "secureMarker", LocalSecurePrefs.HasSecureMarker(key)));

            return value;
        }
        catch (System.Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TryGetProtectedInt),
                "[CurrencyReward] Failed to load protected player currency.",
                exception,
                LogMetadata.Of("currencyKey", SaveDataSanitizer.SafeKeyPart(key)),
                recoverable: true);
            Debug.LogWarning($"PlayerData: failed to load '{key}': {exception.Message}");
            return defaultValue;
        }
    }

    static void TrySaveProtectedInt(string key, int value)
    {
        try
        {
            value = SaveDataSanitizer.ClampCurrencyValue(value);
            string purpose = GetCurrencyPurpose(key);

            AppLogger.Info(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TrySaveProtectedInt),
                "[CurrencyReward] Saving protected player currency.",
                LogMetadata.Of(
                    "currencyKey", SaveDataSanitizer.SafeKeyPart(key),
                    "purpose", purpose,
                    "value", value,
                    "secureMarkerBefore", LocalSecurePrefs.HasSecureMarker(key)));

            LocalSecurePrefs.SetInt(key, purpose, value);

            AppLogger.Info(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TrySaveProtectedInt),
                "[CurrencyReward] Protected player currency saved.",
                LogMetadata.Of(
                    "currencyKey", SaveDataSanitizer.SafeKeyPart(key),
                    "purpose", purpose,
                    "value", value,
                    "secureMarkerAfter", LocalSecurePrefs.HasSecureMarker(key)));
        }
        catch (System.Exception exception)
        {
            AppLogger.Error(
                AppLogCategory.CurrencyReward,
                nameof(PlayerData),
                nameof(TrySaveProtectedInt),
                "[CurrencyReward] Failed to save protected player currency.",
                exception,
                LogMetadata.Of(
                    "currencyKey", SaveDataSanitizer.SafeKeyPart(key),
                    "value", value),
                recoverable: true);
            Debug.LogWarning($"PlayerData: failed to save '{key}': {exception.Message}");
        }
    }

    static string GetCurrencyPurpose(string key)
    {
        return LocalSaveSecurity.PlayerCurrencyPurpose + ":" + SaveDataSanitizer.SanitizeIdentifier(key);
    }
}
