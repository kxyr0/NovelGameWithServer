using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Video;

[Serializable]
public sealed class AssetReferenceVideoClip : AssetReferenceT<VideoClip>
{
    public AssetReferenceVideoClip(string guid)
        : base(guid)
    {
    }
}

[Serializable]
public sealed class AssetReferenceTextAsset : AssetReferenceT<TextAsset>
{
    public AssetReferenceTextAsset(string guid)
        : base(guid)
    {
    }
}
