#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

public static partial class GalleryImageSaver
{
    private static bool TrySaveAndroid(
        byte[] pngBytes,
        string fileName,
        out string savedPath,
        out string error)
    {
        int sdk = GetAndroidSdkInt();
        Debug.Log(
            $"{LogPrefix}[ANDROID][BEGIN] sdk={sdk} file='{fileName}' bytes={pngBytes.Length}");

        if (sdk <= 0)
        {
            savedPath = "";
            error = "Не удалось определить Android SDK_INT.";
            Debug.LogWarning($"{LogPrefix}[ANDROID][FAILED] stage=sdk reason='{error}'");
            return false;
        }

        return sdk >= 29
            ? TrySaveAndroidMediaStore(pngBytes, fileName, out savedPath, out error)
            : TrySaveAndroidLegacy(pngBytes, fileName, out savedPath, out error);
    }

    private static bool TrySaveAndroidMediaStore(
        byte[] pngBytes,
        string fileName,
        out string savedPath,
        out string error)
    {
        savedPath = "";
        error = "";
        string stage = "init";
        AndroidJavaObject resolver = null;
        AndroidJavaObject insertedUri = null;
        bool committed = false;

        try
        {
            stage = "activity";
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null)
            {
                error = "UnityPlayer.currentActivity is null.";
                return false;
            }

            resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            if (resolver == null)
            {
                error = "Activity.getContentResolver returned null.";
                return false;
            }

            stage = "content_values";
            using var values = new AndroidJavaObject("android.content.ContentValues");
            using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");

            PutString(values, "_display_name", fileName);
            PutString(values, "mime_type", "image/png");
            PutString(values, "relative_path", $"Pictures/{AlbumName}");
            PutInt(values, "is_pending", 1);

            stage = "insert";
            using AndroidJavaObject externalUri =
                media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            insertedUri = resolver.Call<AndroidJavaObject>("insert", externalUri, values);
            if (insertedUri == null)
            {
                error = "MediaStore.insert returned null.";
                return false;
            }

            string uriText = SafeUriToString(insertedUri);
            Debug.Log(
                $"{LogPrefix}[ANDROID][MEDIASTORE_INSERT] uri='{uriText}' file='{fileName}'");

            stage = "open_output_stream";
            AndroidJavaObject stream = null;
            try
            {
                stream = resolver.Call<AndroidJavaObject>("openOutputStream", insertedUri, "w");
                if (stream == null)
                {
                    error = "ContentResolver.openOutputStream returned null.";
                    return false;
                }

                stage = "write";
                stream.Call("write", pngBytes, 0, pngBytes.Length);
                stream.Call("flush");
            }
            finally
            {
                if (stream != null)
                {
                    try
                    {
                        stream.Call("close");
                    }
                    catch (Exception closeException)
                    {
                        Debug.LogWarning(
                            $"{LogPrefix}[ANDROID][STREAM_CLOSE_FAILED] " +
                            $"{closeException.GetType().Name}: {closeException.Message}");
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }
            }

            stage = "commit";
            using (var updateValues = new AndroidJavaObject("android.content.ContentValues"))
            {
                PutInt(updateValues, "is_pending", 0);
                int updated = resolver.Call<int>(
                    "update",
                    insertedUri,
                    updateValues,
                    null,
                    null);

                Debug.Log(
                    $"{LogPrefix}[ANDROID][MEDIASTORE_COMMIT] uri='{uriText}' updatedRows={updated}");

                if (updated <= 0)
                {
                    error = "MediaStore.update did not commit the pending image.";
                    return false;
                }
            }

            committed = true;
            savedPath = $"Pictures/{AlbumName}/{fileName}";
            Debug.Log(
                $"{LogPrefix}[ANDROID][SUCCESS] uri='{uriText}' path='{savedPath}' bytes={pngBytes.Length}");
            return true;
        }
        catch (Exception exception)
        {
            error =
                $"stage={stage}; {exception.GetType().Name}: {exception.Message}";
            Debug.LogWarning(
                $"{LogPrefix}[ANDROID][FAILED] {error}");
            return false;
        }
        finally
        {
            if (!committed && resolver != null && insertedUri != null)
                DeleteInsertedUri(resolver, insertedUri);

            insertedUri?.Dispose();
            resolver?.Dispose();
        }
    }

    private static bool TrySaveAndroidLegacy(
        byte[] pngBytes,
        string fileName,
        out string savedPath,
        out string error)
    {
        savedPath = "";
        error = "";

        try
        {
            string directory = Path.Combine(GetAndroidPicturesPath(), AlbumName);
            Directory.CreateDirectory(directory);
            savedPath = MakeUniquePath(directory, fileName);
            File.WriteAllBytes(savedPath, pngBytes);
            ScanAndroidFile(savedPath);
            Debug.Log($"{LogPrefix}[ANDROID][LEGACY_SUCCESS] path='{savedPath}'");
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            Debug.LogWarning(
                $"{LogPrefix}[ANDROID][LEGACY_FAILED] reason='{error}'");
            return false;
        }
    }

    private static void PutString(
        AndroidJavaObject values,
        string key,
        string value)
    {
        values.Call("put", key, value ?? "");
    }

    private static void PutInt(
        AndroidJavaObject values,
        string key,
        int value)
    {
        // ContentValues has put(String, Integer), not put(String, int).
        // Passing a C# int makes JNI search for the wrong overload.
        using var boxed = new AndroidJavaObject("java.lang.Integer", value);
        values.Call("put", key, boxed);
    }

    private static void DeleteInsertedUri(
        AndroidJavaObject resolver,
        AndroidJavaObject uri)
    {
        try
        {
            int deleted = resolver.Call<int>("delete", uri, null, null);
            Debug.Log(
                $"{LogPrefix}[ANDROID][CLEANUP] uri='{SafeUriToString(uri)}' deletedRows={deleted}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"{LogPrefix}[ANDROID][CLEANUP_FAILED] " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string SafeUriToString(AndroidJavaObject uri)
    {
        if (uri == null)
            return "<null>";

        try
        {
            return uri.Call<string>("toString") ?? "<null>";
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static string GetAndroidPicturesPath()
    {
        using var environment = new AndroidJavaClass("android.os.Environment");
        using AndroidJavaObject directory = environment.CallStatic<AndroidJavaObject>(
            "getExternalStoragePublicDirectory",
            environment.GetStatic<string>("DIRECTORY_PICTURES"));
        return directory.Call<string>("getAbsolutePath");
    }

    private static void ScanAndroidFile(string path)
    {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var scanner = new AndroidJavaClass("android.media.MediaScannerConnection");
        scanner.CallStatic(
            "scanFile",
            activity,
            new[] { path },
            new[] { "image/png" },
            null);
    }

    private static int GetAndroidSdkInt()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"{LogPrefix}[ANDROID][SDK_FAILED] " +
                $"{exception.GetType().Name}: {exception.Message}");
            return 0;
        }
    }
}
#endif
