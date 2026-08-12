using System;
using UnityEngine;

public struct CutsceneImageDownloadInfo
{
    public CutsceneImageDownloadInfo(Sprite sprite, string suggestedFileName)
    {
        Sprite = sprite;
        SuggestedFileName = suggestedFileName;
    }

    public Sprite Sprite { get; }
    public string SuggestedFileName { get; }
    public bool HasImage => Sprite != null;
}

public static class CutsceneImageDownloadState
{
    private const string LogPrefix = "[IMAGE_EXPORT][CUTSCENE_STATE]";

    public static event Action<CutsceneImageDownloadInfo> Changed;

    private static CutsceneImageDownloadInfo _current;

    public static CutsceneImageDownloadInfo Current => _current;

    public static void Show(Sprite sprite, string suggestedFileName)
    {
        if (sprite == null)
        {
            Debug.LogWarning(
                $"{LogPrefix}[SHOW_FAILED] reason=Sprite_is_null file='{suggestedFileName}'");
            Hide();
            return;
        }

        _current = new CutsceneImageDownloadInfo(sprite, suggestedFileName);
        Debug.Log(
            $"{LogPrefix}[SHOW] sprite='{sprite.name}' texture='{(sprite.texture != null ? sprite.texture.name : "<null>")}' " +
            $"file='{suggestedFileName}'");
        Changed?.Invoke(_current);
    }

    public static void Hide()
    {
        if (!_current.HasImage)
            return;

        Debug.Log(
            $"{LogPrefix}[HIDE] sprite='{(_current.Sprite != null ? _current.Sprite.name : "<null>")}' " +
            $"file='{_current.SuggestedFileName}'");
        _current = default;
        Changed?.Invoke(_current);
    }
}
