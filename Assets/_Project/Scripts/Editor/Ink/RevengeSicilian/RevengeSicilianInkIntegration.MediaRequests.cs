#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public static partial class RevengeSicilianInkIntegration
{
    static List<MediaRequest> CollectMediaRequests(List<CompiledEpisode> compiled)
    {
        var result = new List<MediaRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < compiled.Count; c++)
        {
            StoryJsonDocument document = compiled[c].Document;
            if (document?.nodes == null)
                continue;

            for (int n = 0; n < document.nodes.Count; n++)
            {
                StoryJsonNode node = document.nodes[n];
                if (node == null)
                    continue;

                AddMedia(result, seen, node.background, AssetReferenceKind.Sprite, "background");
                AddMedia(result, seen, node.backgroundOverlay, AssetReferenceKind.Sprite, "background overlay");
                AddMedia(result, seen, node.image, AssetReferenceKind.Sprite, "image/cutscene");
                AddMedia(result, seen, node.contactAvatar, AssetReferenceKind.Sprite, "phone avatar");
                AddMedia(result, seen, node.music, AssetReferenceKind.Audio, "music");
                AddMedia(result, seen, node.startSfx, AssetReferenceKind.Audio, "sfx/ambience");
                AddMedia(result, seen, node.backgroundVideo, AssetReferenceKind.Video, "background video");
                AddMedia(result, seen, node.video, AssetReferenceKind.Video, "video/cutscene");
                AddMedia(result, seen, node.backgroundGif, AssetReferenceKind.Text, "background gif");
                AddMedia(result, seen, node.gif, AssetReferenceKind.Text, "gif/cutscene");

                if (node.appearanceOptions != null)
                {
                    for (int i = 0; i < node.appearanceOptions.Count; i++)
                        AddMedia(result, seen, node.appearanceOptions[i]?.previewSprite, AssetReferenceKind.Sprite, "appearance preview");
                }

                if (node.heroBuildCutsceneOverrides != null)
                {
                    for (int i = 0; i < node.heroBuildCutsceneOverrides.Count; i++)
                    {
                        StoryJsonHeroBuildCutsceneOverride item = node.heroBuildCutsceneOverrides[i];
                        if (item == null) continue;
                        AddMedia(result, seen, item.image, AssetReferenceKind.Sprite, "hero cutscene image");
                        AddMedia(result, seen, item.video, AssetReferenceKind.Video, "hero cutscene video");
                        AddMedia(result, seen, item.gif, AssetReferenceKind.Text, "hero cutscene gif");
                    }
                }

                if (node.messages != null)
                {
                    for (int i = 0; i < node.messages.Count; i++)
                        AddMedia(result, seen, node.messages[i]?.attachment, AssetReferenceKind.Sprite, "phone attachment");
                }
            }
        }
        return result;
    }

    static void AddMedia(
        List<MediaRequest> output,
        HashSet<string> seen,
        string id,
        AssetReferenceKind kind,
        string usage)
    {
        id = (id ?? "").Trim();
        if (string.IsNullOrWhiteSpace(id))
            return;

        string key = kind + "\n" + id;
        if (!seen.Add(key))
            return;

        output.Add(new MediaRequest { Id = id, Kind = kind, Usage = usage });
    }

    static StoryJsonAssetReference CreateMediaReference(string id, AssetReferenceKind kind, UnityEngine.Object asset)
    {
        switch (kind)
        {
            case AssetReferenceKind.Sprite:
                return StoryJsonAssetReference.CreateSprite(id, asset as Sprite);
            case AssetReferenceKind.Audio:
                return StoryJsonAssetReference.CreateAudio(id, asset as AudioClip);
            case AssetReferenceKind.Video:
                return StoryJsonAssetReference.CreateVideo(id, asset as VideoClip);
            case AssetReferenceKind.Text:
                return StoryJsonAssetReference.CreateText(id, asset as TextAsset);
            default:
                return null;
        }
    }
}
#endif
