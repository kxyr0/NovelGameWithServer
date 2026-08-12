using System.Collections;
using UnityEngine;

public static class MobileDevicePermissionService
{
    const string RequestedKey = "mobile_device_permissions_after_privacy_v1";
    const string Purpose = "setup_flag:mobile_device_permissions";

    public static bool WasRequestedAfterPrivacyConsent =>
        LocalSecurePrefs.GetBool(RequestedKey, Purpose, false);

    public static void RequestAfterPrivacyConsent(MonoBehaviour runner)
    {
        if (runner == null || WasRequestedAfterPrivacyConsent)
            return;

        runner.StartCoroutine(RequestAfterPrivacyConsentRoutine());
    }

    public static IEnumerator RequestAfterPrivacyConsentRoutine()
    {
        if (WasRequestedAfterPrivacyConsent)
            yield break;

        LocalSecurePrefs.SetBool(RequestedKey, Purpose, true);

#if UNITY_ANDROID && !UNITY_EDITOR
        yield return RequestAndroidMediaPermissions();
#elif UNITY_IOS && !UNITY_EDITOR
        yield return null;
#else
        yield return null;
#endif
    }

    public static void ResetRememberedRequest()
    {
        LocalSecurePrefs.Delete(RequestedKey);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    const string ReadExternalStorage = "android.permission.READ_EXTERNAL_STORAGE";
    const string WriteExternalStorage = "android.permission.WRITE_EXTERNAL_STORAGE";
    const string ReadMediaImages = "android.permission.READ_MEDIA_IMAGES";
    const string ReadMediaVideo = "android.permission.READ_MEDIA_VIDEO";

    static IEnumerator RequestAndroidMediaPermissions()
    {
        int sdk = GetAndroidSdkInt();
        if (sdk >= 33)
        {
            yield return RequestAndroidPermission(ReadMediaImages);
            yield return RequestAndroidPermission(ReadMediaVideo);
            yield break;
        }

        yield return RequestAndroidPermission(ReadExternalStorage);

        if (sdk <= 28)
            yield return RequestAndroidPermission(WriteExternalStorage);
    }

    static IEnumerator RequestAndroidPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) ||
            UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
        {
            yield break;
        }

        bool finished = false;
        var callbacks = new UnityEngine.Android.PermissionCallbacks();
        callbacks.PermissionGranted += _ => finished = true;
        callbacks.PermissionDenied += _ => finished = true;
        callbacks.PermissionDeniedAndDontAskAgain += _ => finished = true;

        UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);

        float deadline = Time.unscaledTime + 12f;
        while (!finished && Time.unscaledTime < deadline)
            yield return null;
    }

    static int GetAndroidSdkInt()
    {
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                return version.GetStatic<int>("SDK_INT");
        }
        catch
        {
            return 0;
        }
    }
#endif
}
