using UnityEngine;

[CreateAssetMenu(fileName = "SubscriptionFeatureConfig", menuName = "Nocturne/Subscription/Feature Config")]
public sealed class SubscriptionFeatureConfig : ScriptableObject
{
    public const string ResourcesPath = "SubscriptionFeatureConfig";

    [SerializeField]
    [InspectorName("Subscription features enabled")]
    [Tooltip("Главный выключатель подписочных возможностей. По умолчанию выключен и не запускает проверки подписки.")]
    private bool _featuresEnabled;

    [SerializeField]
    [InspectorName("Offline window hours")]
    [Tooltip("Сколько часов после последней серверной проверки можно использовать подписку офлайн.")]
    private int _offlineVerificationWindowHours = 72;

    [SerializeField]
    [InspectorName("Clock rollback tolerance minutes")]
    [Tooltip("Допустимый небольшой откат локальных часов до требования повторной проверки.")]
    private int _clockRollbackToleranceMinutes = 5;

    public bool FeaturesEnabled => _featuresEnabled;
    public int OfflineVerificationWindowHours => Mathf.Max(1, _offlineVerificationWindowHours);
    public int ClockRollbackToleranceMinutes => Mathf.Clamp(_clockRollbackToleranceMinutes, 0, 60);

    public static SubscriptionFeatureConfig LoadOrDisabled()
    {
        SubscriptionFeatureConfig config = Resources.Load<SubscriptionFeatureConfig>(ResourcesPath);
        if (config != null)
            return config;

        return CreateInstance<SubscriptionFeatureConfig>();
    }

#if UNITY_EDITOR
    public void SetFeaturesEnabledForEditor(bool enabled)
    {
        _featuresEnabled = enabled;
    }
#endif

    void OnValidate()
    {
        _offlineVerificationWindowHours = Mathf.Max(1, _offlineVerificationWindowHours);
        _clockRollbackToleranceMinutes = Mathf.Clamp(_clockRollbackToleranceMinutes, 0, 60);
    }
}
