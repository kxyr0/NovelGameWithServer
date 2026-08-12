using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressablesCatalogUpdateService
{
    private const int MaxCatalogUpdates = 16;

    public async UniTask<int> UpdateCatalogsAsync(CancellationToken cancellationToken)
    {
        NetworkRuntimeConfigData config = NetworkRuntimeConfigLoader.Load();
        string loadPath = config != null ? config.ResolveAddressablesRemoteLoadPath() : "";
        if (string.IsNullOrWhiteSpace(loadPath))
            return 0;

        if (!ContentReleasePolicy.UsesSafeRemotePath(loadPath))
        {
            Debug.LogWarning("[AddressablesCatalogUpdateService] Remote load path is not safe: " + loadPath);
            return 0;
        }

        AsyncOperationHandle<IResourceLocator> initHandle = Addressables.InitializeAsync();
        await initHandle.ToUniTask(cancellationToken: cancellationToken);

        AsyncOperationHandle<List<string>> checkHandle = Addressables.CheckForCatalogUpdates(false);
        List<string> catalogs = null;
        try
        {
            catalogs = await checkHandle.ToUniTask(cancellationToken: cancellationToken);
        }
        finally
        {
            if (checkHandle.IsValid())
                Addressables.Release(checkHandle);
        }

        if (catalogs == null || catalogs.Count == 0)
            return 0;

        if (catalogs.Count > MaxCatalogUpdates)
            catalogs = catalogs.GetRange(0, MaxCatalogUpdates);

        AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(true, catalogs, false);
        try
        {
            List<IResourceLocator> locators = await updateHandle.ToUniTask(cancellationToken: cancellationToken);
            int count = locators != null ? locators.Count : 0;
            Debug.Log("[AddressablesCatalogUpdateService] Updated remote catalogs: " + count);
            return count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[AddressablesCatalogUpdateService] Catalog update failed: " + exception.Message);
            return 0;
        }
        finally
        {
            if (updateHandle.IsValid())
                Addressables.Release(updateHandle);
        }
    }
}
