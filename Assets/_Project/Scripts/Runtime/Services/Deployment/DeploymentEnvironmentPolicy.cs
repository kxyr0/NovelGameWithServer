using System;
using System.Collections.Generic;

public static class DeploymentEnvironmentPolicy
{
    public const string StageContentChannel = "stage";
    public const string ProductionContentChannel = "prod";

    public static DeploymentEnvironmentValidationResult Validate(NetworkRuntimeConfigData config)
    {
        if (config == null)
            return DeploymentEnvironmentValidationResult.Fail("Runtime-конфиг сети отсутствует.");

        NetworkEnvironmentEntry stage = Find(config.environments, DeploymentEnvironmentIds.Stage);
        NetworkEnvironmentEntry prod = Find(config.environments, DeploymentEnvironmentIds.Production);
        if (stage == null || prod == null)
            return DeploymentEnvironmentValidationResult.Fail("Должны существовать обе среды: Тест и Прод.");

        if (string.IsNullOrWhiteSpace(stage.baseUrl) || string.IsNullOrWhiteSpace(prod.baseUrl))
            return DeploymentEnvironmentValidationResult.Fail("Адреса серверов Тест и Прод не должны быть пустыми.");

        if (Same(stage.baseUrl, prod.baseUrl))
            return DeploymentEnvironmentValidationResult.Fail("Адреса серверов Тест и Прод должны отличаться.");

        if (!Same(stage.contentChannel, StageContentChannel))
            return DeploymentEnvironmentValidationResult.Fail("У тестовой среды contentChannel должен быть 'stage'.");

        if (!Same(prod.contentChannel, ProductionContentChannel))
            return DeploymentEnvironmentValidationResult.Fail("У Прод contentChannel должен быть 'prod'.");

        if (string.IsNullOrWhiteSpace(stage.addressablesRemoteLoadPath) ||
            string.IsNullOrWhiteSpace(prod.addressablesRemoteLoadPath))
            return DeploymentEnvironmentValidationResult.Fail("Пути загрузки Addressables для Тест и Прод не должны быть пустыми.");

        return DeploymentEnvironmentValidationResult.Ok("Среды выкладки разделены.");
    }

    public static NetworkEnvironmentEntry Find(IReadOnlyList<NetworkEnvironmentEntry> environments, string id)
    {
        if (environments == null)
            return null;

        string normalizedId = DeploymentEnvironmentIds.Normalize(id);
        for (int i = 0; i < environments.Count; i++)
        {
            NetworkEnvironmentEntry environment = environments[i];
            if (environment != null && DeploymentEnvironmentIds.Normalize(environment.id) == normalizedId)
                return environment;
        }

        return null;
    }

    public static string NormalizeUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().TrimEnd('/');
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(
            NormalizeUrl(left),
            NormalizeUrl(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
