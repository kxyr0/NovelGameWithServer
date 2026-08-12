using System.Collections;
using UnityEngine;

public static class CutsceneGalleryPermission
{
    private const string LogPrefix = "[IMAGE_EXPORT][PERMISSION]";

    public static bool HasSaveAccess
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int sdk = GetAndroidSdkInt();
            if (sdk >= 29)
                return true;

            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                WriteExternalStorage);
#else
            return true;
#endif
        }
    }

    public static IEnumerator RequestSaveAccessIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int sdk = GetAndroidSdkInt();
        Debug.Log($"{LogPrefix}[CHECK] sdk={sdk} currentAccess={HasSaveAccess}");

        // Android 10+ writes app-created images through MediaStore and does not
        // need storage/read-media permission for this operation.
        if (sdk >= 29)
        {
            Debug.Log($"{LogPrefix}[SKIP] sdk={sdk} reason=MediaStore_does_not_require_storage_permission");
            yield break;
        }

        if (HasSaveAccess)
        {
            Debug.Log($"{LogPrefix}[SKIP] sdk={sdk} reason=Already_granted");
            yield break;
        }

        bool finished = false;
        string result = "timeout";
        var callbacks = new UnityEngine.Android.PermissionCallbacks();
        callbacks.PermissionGranted += _ =>
        {
            result = "granted";
            finished = true;
        };
        callbacks.PermissionDenied += _ =>
        {
            result = "denied";
            finished = true;
        };
        callbacks.PermissionDeniedAndDontAskAgain += _ =>
        {
            result = "denied_dont_ask_again";
            finished = true;
        };

        Debug.Log($"{LogPrefix}[REQUEST] sdk={sdk} permission='{WriteExternalStorage}'");
        UnityEngine.Android.Permission.RequestUserPermission(
            WriteExternalStorage,
            callbacks);

        float deadline = Time.unscaledTime + 12f;
        while (!finished && Time.unscaledTime < deadline)
            yield return null;

        Debug.Log(
            $"{LogPrefix}[RESULT] sdk={sdk} result={result} finalAccess={HasSaveAccess}");
#else
        yield return null;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string WriteExternalStorage =
        "android.permission.WRITE_EXTERNAL_STORAGE";

    private static int GetAndroidSdkInt()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"{LogPrefix}[SDK_FAILED] {exception.GetType().Name}: {exception.Message}");
            return 0;
        }
    }
#endif
}
