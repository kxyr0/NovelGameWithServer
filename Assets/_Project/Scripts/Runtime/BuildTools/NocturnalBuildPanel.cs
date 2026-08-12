using UnityEngine;

public enum NocturnalBuildEnvironment
{
    Stage,
    Production
}

public enum NocturnalAndroidPackageFormat
{
    Apk,
    AppBundle
}

public enum NocturnalDesktopBuildTarget
{
    Windows64,
    Linux64,
    MacOS
}

public enum NocturnalManagedStrippingLevel
{
    Minimal,
    Low,
    Medium,
    High
}

public enum NocturnalIl2CppCodeGeneration
{
    FasterRuntime,
    FasterSmallerBuilds
}

public enum NocturnalIl2CppCompilerConfiguration
{
    Release,
    Master
}

[AddComponentMenu("Nocturnal/Сборщик билдов")]
public sealed class NocturnalBuildPanel : MonoBehaviour
{
    [SerializeField] private NocturnalBuildEnvironment _environment = NocturnalBuildEnvironment.Stage;
    [SerializeField] private string _outputRoot = "Builds";
    [SerializeField] private string _versionName = "1.0";
    [SerializeField] private int _androidVersionCode = 1;
    [SerializeField] private int _iosBuildNumber = 1;

    [SerializeField] private bool _applyEnvironmentBeforeBuild = true;
    [SerializeField] private bool _buildAddressablesBeforePlayer = true;
    [SerializeField] private bool _developmentBuild;
    [SerializeField] private bool _strictMode = true;
    [SerializeField] private bool _autoRunAfterBuild;
    [SerializeField] private bool _showFolderAfterBuild = true;

    [SerializeField] private NocturnalManagedStrippingLevel _managedStrippingLevel = NocturnalManagedStrippingLevel.Low;
    [SerializeField] private NocturnalIl2CppCodeGeneration _il2CppCodeGeneration = NocturnalIl2CppCodeGeneration.FasterRuntime;
    [SerializeField] private NocturnalIl2CppCompilerConfiguration _il2CppCompilerConfiguration = NocturnalIl2CppCompilerConfiguration.Release;

    [SerializeField] private NocturnalAndroidPackageFormat _androidPackageFormat = NocturnalAndroidPackageFormat.Apk;
    [SerializeField] private bool _androidIncludeArmv7;
    [SerializeField] private bool _androidMinifyRelease;
    [SerializeField] private bool _androidCreateSymbols = true;
    [SerializeField] private bool _androidOptimizedFramePacing = true;

    [SerializeField] private string _iosMinimumVersion = "13.0";
    [SerializeField] private bool _iosSupportsIPad = true;
    [SerializeField] private bool _iosAutomaticSigning = true;
    [SerializeField] private string _iosAppleDeveloperTeamId = "";

    [SerializeField] private NocturnalDesktopBuildTarget _desktopBuildTarget = NocturnalDesktopBuildTarget.Windows64;

    public NocturnalBuildEnvironment Environment => _environment;
    public string OutputRoot => string.IsNullOrWhiteSpace(_outputRoot) ? "Builds" : _outputRoot.Trim();
    public string VersionName => string.IsNullOrWhiteSpace(_versionName) ? Application.version : _versionName.Trim();
    public int AndroidVersionCode => Mathf.Max(1, _androidVersionCode);
    public int IosBuildNumber => Mathf.Max(1, _iosBuildNumber);
    public bool ApplyEnvironmentBeforeBuild => _applyEnvironmentBeforeBuild;
    public bool BuildAddressablesBeforePlayer => _buildAddressablesBeforePlayer;
    public bool DevelopmentBuild => _environment != NocturnalBuildEnvironment.Production &&
                                    _developmentBuild;
    public bool StrictMode => _strictMode;
    public bool AutoRunAfterBuild => _autoRunAfterBuild;
    public bool ShowFolderAfterBuild => _showFolderAfterBuild;
    public NocturnalManagedStrippingLevel ManagedStrippingLevel => _managedStrippingLevel;
    public NocturnalIl2CppCodeGeneration Il2CppCodeGeneration => _il2CppCodeGeneration;
    public NocturnalIl2CppCompilerConfiguration Il2CppCompilerConfiguration => _il2CppCompilerConfiguration;
    public bool AndroidBuildsAppBundle => _androidPackageFormat == NocturnalAndroidPackageFormat.AppBundle;
    public NocturnalAndroidPackageFormat AndroidPackageFormat => _androidPackageFormat;
    public bool AndroidIncludeArmv7 => _androidIncludeArmv7;
    public bool AndroidMinifyRelease => _androidMinifyRelease;
    public bool AndroidCreateSymbols => _androidCreateSymbols;
    public bool AndroidOptimizedFramePacing => _androidOptimizedFramePacing;
    public string IosMinimumVersion => NormalizeIosVersion(_iosMinimumVersion);
    public bool IosSupportsIPad => _iosSupportsIPad;
    public bool IosAutomaticSigning => _iosAutomaticSigning;
    public string IosAppleDeveloperTeamId => (_iosAppleDeveloperTeamId ?? "").Trim();
    public NocturnalDesktopBuildTarget DesktopBuildTarget => _desktopBuildTarget;

    private void Reset()
    {
        _versionName = Application.version;
        _androidVersionCode = 1;
        _iosBuildNumber = 1;
        _outputRoot = "Builds";
        _applyEnvironmentBeforeBuild = true;
        _buildAddressablesBeforePlayer = true;
        _strictMode = true;
        _managedStrippingLevel = NocturnalManagedStrippingLevel.Low;
        _il2CppCodeGeneration = NocturnalIl2CppCodeGeneration.FasterRuntime;
        _il2CppCompilerConfiguration = NocturnalIl2CppCompilerConfiguration.Release;
        _androidCreateSymbols = true;
        _androidOptimizedFramePacing = true;
        _iosMinimumVersion = "13.0";
        _iosSupportsIPad = true;
        _iosAutomaticSigning = true;
    }

    private static string NormalizeIosVersion(string value)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? "13.0" : value.Trim();
        return System.Version.TryParse(candidate, out _) ? candidate : "13.0";
    }
}
