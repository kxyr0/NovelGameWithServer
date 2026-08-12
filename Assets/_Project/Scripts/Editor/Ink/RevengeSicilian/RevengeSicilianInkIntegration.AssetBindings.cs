#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public static partial class RevengeSicilianInkIntegration
{
    enum AssetReferenceKind
    {
        Character,
        Clothing,
        Sprite,
        Audio,
        Video,
        Text
    }

    sealed class MediaRequest
    {
        public string Id;
        public AssetReferenceKind Kind;
        public string Usage;
    }

    static ExactAssetIndex<Sprite> _spriteIndex;
    static ExactAssetIndex<AudioClip> _audioIndex;
    static ExactAssetIndex<VideoClip> _videoIndex;
    static ExactAssetIndex<TextAsset> _textIndex;

    static bool TryPrepareStoryAssets(
        AuthorInkSharedContext shared,
        List<CompiledEpisode> compiled,
        List<string> report,
        out string error)
    {
        error = "";
        _spriteIndex = new ExactAssetIndex<Sprite>(StoryFolder);
        _audioIndex = new ExactAssetIndex<AudioClip>(StoryFolder);
        _videoIndex = new ExactAssetIndex<VideoClip>(StoryFolder);
        _textIndex = new ExactAssetIndex<TextAsset>(StoryFolder);

        StoryJsonAssetLibrary library = CreateOrLoadAsset<StoryJsonAssetLibrary>(AssetLibraryPath, out bool libraryCreated);
        if (library == null)
        {
            error = "Не удалось создать StoryJsonAssetLibrary: " + AssetLibraryPath;
            return false;
        }

        var references = library.Assets != null
            ? library.Assets.Where(entry => entry != null).ToList()
            : new List<StoryJsonAssetReference>();

        report.Add("[LIBRARY] " + AssetLibraryPath + (libraryCreated ? " (created)" : " (updated without wiping manual bindings)"));
        List<StatDefinition> stats = EnsureStats(shared, report);
        EnsureCharacters(shared, references, report);
        EnsureWardrobeItems(shared, compiled, references, report);
        BindMediaAssets(compiled, references, report);
        ReportAppearanceValues(shared, report);

        library.Configure(references);
        library.ConfigureResolverPolicy(false);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.Add("[SUMMARY] Stats=" + stats.Count +
                   ", Characters=" + CountKind(references, AssetReferenceKind.Character) +
                   ", Clothing=" + CountKind(references, AssetReferenceKind.Clothing) +
                   ", Library entries=" + references.Count + ".");
        return true;
    }

    static void BindMediaAssets(
        List<CompiledEpisode> compiled,
        List<StoryJsonAssetReference> references,
        List<string> report)
    {
        List<MediaRequest> requests = CollectMediaRequests(compiled);
        for (int i = 0; i < requests.Count; i++)
        {
            MediaRequest request = requests[i];
            StoryJsonAssetReference existing = FindReference(references, request.Id);
            if (HasExpectedAsset(existing, request.Kind))
            {
                report.Add("[ASSET:EXISTING] " + request.Kind + " '" + request.Id + "' <- " + AssetPathOf(existing, request.Kind));
                continue;
            }

            UnityEngine.Object resolved = null;
            string resolution = "";
            switch (request.Kind)
            {
                case AssetReferenceKind.Sprite:
                    resolved = _spriteIndex.Resolve(request.Id, out resolution);
                    break;
                case AssetReferenceKind.Audio:
                    resolved = _audioIndex.Resolve(request.Id, out resolution);
                    break;
                case AssetReferenceKind.Video:
                    resolved = _videoIndex.Resolve(request.Id, out resolution);
                    break;
                case AssetReferenceKind.Text:
                    resolved = _textIndex.Resolve(request.Id, out resolution);
                    break;
            }

            if (resolved == null)
            {
                UpsertReference(
                    references,
                    request.Id,
                    StoryJsonAssetReference.CreateEmpty(request.Id, ToKindHint(request.Kind)),
                    request.Kind,
                    report);
                report.Add("[ASSET:UNRESOLVED] " + request.Kind + " '" + request.Id + "' (" + request.Usage + ") — " + resolution +
                           ". Missing slot добавлен в AssetLibrary для ручной привязки.");
                continue;
            }

            StoryJsonAssetReference reference = CreateMediaReference(request.Id, request.Kind, resolved);
            UpsertReference(references, request.Id, reference, request.Kind, report);
            report.Add("[ASSET:AUTO] " + request.Kind + " '" + request.Id + "' <- " + AssetDatabase.GetAssetPath(resolved) + " (exact normalized name)");
        }
    }
}
#endif
