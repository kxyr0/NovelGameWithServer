using System;

public static class ContentReleasePolicy
{
    public static DeploymentEnvironmentValidationResult Validate(ContentReleaseDescriptor descriptor)
    {
        if (descriptor == null)
            return DeploymentEnvironmentValidationResult.Fail("Данные релиза отсутствуют.");

        ContentReleaseDescriptor release = descriptor.CloneNormalized();
        if (string.IsNullOrWhiteSpace(release.storyId))
            return DeploymentEnvironmentValidationResult.Fail("Укажите ID истории.");

        if (string.IsNullOrWhiteSpace(release.episodeId))
            return DeploymentEnvironmentValidationResult.Fail("Укажите ID эпизода.");

        if (string.IsNullOrWhiteSpace(release.contentVersion))
            return DeploymentEnvironmentValidationResult.Fail("Укажите версию контента.");

        if (!ContentReleaseStatus.IsKnown(release.status))
            return DeploymentEnvironmentValidationResult.Fail("Неизвестный статус релиза.");

        if (!ContentReleaseChannel.IsKnown(release.channel))
            return DeploymentEnvironmentValidationResult.Fail("Неизвестный канал релиза.");

        if (release.status == ContentReleaseStatus.Published &&
            release.channel != ContentReleaseChannel.Production)
            return DeploymentEnvironmentValidationResult.Fail("Опубликованный контент можно отправлять только в Прод.");

        if (release.status == ContentReleaseStatus.Staging &&
            release.channel != ContentReleaseChannel.Stage)
            return DeploymentEnvironmentValidationResult.Fail("Тестовый контент можно отправлять только в Stage.");

        if (ContentReleaseStatus.IsLive(release.status) &&
            string.IsNullOrWhiteSpace(release.addressablesRemoteLoadPath))
            return DeploymentEnvironmentValidationResult.Fail("Для живого контента нужен remote load path Addressables.");

        if (!UsesSafeRemotePath(release.addressablesRemoteLoadPath))
            return DeploymentEnvironmentValidationResult.Fail("Путь загрузки Addressables должен использовать HTTPS или локальный HTTP.");

        if (!UsesSafeRemotePath(release.addressablesCatalogUrl))
            return DeploymentEnvironmentValidationResult.Fail("URL каталога Addressables должен использовать HTTPS или локальный HTTP.");

        if (!UsesSafeRemotePath(release.addressablesManifestUrl))
            return DeploymentEnvironmentValidationResult.Fail("URL manifest Addressables должен использовать HTTPS или локальный HTTP.");

        return DeploymentEnvironmentValidationResult.Ok("Данные релиза корректны.");
    }

    public static bool UsesSafeRemotePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string trimmed = value.Trim();
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        return trimmed.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("http://[::1]", StringComparison.OrdinalIgnoreCase);
    }
}
