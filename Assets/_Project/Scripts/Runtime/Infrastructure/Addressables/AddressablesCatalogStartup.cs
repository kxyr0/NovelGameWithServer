using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AddressablesCatalogStartup
{
    private static bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Start()
    {
        if (_started)
            return;

        _started = true;
        UpdateCatalogsAsync().Forget();
    }

    private static async UniTaskVoid UpdateCatalogsAsync()
    {
        try
        {
            await new AddressablesCatalogUpdateService().UpdateCatalogsAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[AddressablesCatalogStartup] Remote catalog update failed: " + exception.Message);
        }
    }
}
