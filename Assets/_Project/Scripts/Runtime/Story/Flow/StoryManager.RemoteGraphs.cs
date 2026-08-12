using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public partial class StoryManager
{
    public string LastResolvedGraphSource { get; private set; } = "";
    public string LastResolvedGraphEpisodeId { get; private set; } = "";
    public string LastResolvedGraphContentVersion { get; private set; } = "";

    IEnumerator SyncRemoteGraphCacheIfNeeded(SaveData snapshot)
    {
        if (snapshot == null)
            yield break;

        yield return SyncRemoteGraphCacheIfNeeded(snapshot.episodeId);
    }

    IEnumerator SyncRemoteGraphCacheIfNeeded(string episodeId)
    {
        if (!PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled ||
            string.IsNullOrEmpty(episodeId) ||
            !NetworkManager.IsAuthenticated ||
            NetworkManager.Instance == null ||
            !NetworkManager.HasCatalogRemoteContent(episodeId))
        {
            yield break;
        }

        EpisodeGraphResponse response = null;
        yield return NetworkManager.Instance.FetchEpisodeGraphResponse(
            episodeId,
            RemoteEpisodeGraphCache.GetLocalVersion(episodeId),
            result => response = result);

        if (response == null || response.notModified)
            yield break;

        RemoteEpisodeGraphCache.Save(
            string.IsNullOrEmpty(response.episodeId) ? episodeId : response.episodeId,
            response.contentVersion,
            response.graphJson,
            response.rawPayloadJson);
    }

    IEnumerator PrepareRemoteGraphCacheForEpisodeIfPossible(string episodeId)
    {
        if (!PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled || string.IsNullOrEmpty(episodeId))
            yield break;

        bool hasCachedGraph = RemoteEpisodeGraphCache.TryLoad(episodeId, out _);
        bool authenticated = NetworkManager.IsAuthenticated;
        if (!authenticated && hasCachedGraph)
            yield break;

        if (!authenticated)
            yield return WaitForAuthentication(ok => authenticated = ok);

        if (!authenticated || NetworkManager.Instance == null)
            yield break;

        if (!NetworkManager.HasCatalogRemoteContent(episodeId))
            yield return NetworkManager.Instance.SyncCatalog();

        yield return SyncRemoteGraphCacheIfNeeded(episodeId);
    }

    StoryGraph ResolveGraphForChapter(ChapterData chapter)
    {
        if (chapter == null)
        {
            Debug.LogError(
                $"[STORY_GRAPH][FAILED] platform={Application.platform} storyId='{CurrentStoryId}' reason=ChapterData_is_null.",
                this);
            return null;
        }

        string episodeId = ResolveChapterEpisodeId(chapter);

        StoryRuntimeAssetRegistryResolver.SetActiveStory(CurrentStoryId);

        // Local JSON has the same priority on every platform.
        // UNITY_EDITOR / DEVELOPMENT_BUILD must not change story source ordering.
        if (TryResolveJsonGraphForChapter(chapter, episodeId, out var localJsonGraph))
        {
            SetLastResolvedGraphSource("локальный JSON", episodeId, "");
            return localJsonGraph;
        }

        if (PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled &&
            !string.IsNullOrEmpty(episodeId) &&
            NetworkManager.HasCatalogRemoteContent(episodeId) &&
            RemoteEpisodeGraphCache.TryLoad(episodeId, out var cacheEntry))
        {
            if (RemoteStoryGraphImporter.TryBuildGraph(cacheEntry, out var remoteGraph, out var reason, CreateJsonAssetResolver(chapter)))
            {
                SetLastResolvedGraphSource("серверный JSON", episodeId, cacheEntry.contentVersion);
                return remoteGraph;
            }

            if (!string.IsNullOrWhiteSpace(reason) && reason != "remote graph has no nodes")
                Debug.LogWarning("[StoryManager] Remote graph fallback to local for " + episodeId + ": " + reason);
        }

        SetLastResolvedGraphSource("локальный StoryGraph", episodeId, "");

        if (chapter.graph == null)
        {
            bool hasJsonAsset = chapter.jsonGraph != null;
            int jsonLength = hasJsonAsset && chapter.jsonGraph.text != null ? chapter.jsonGraph.text.Length : 0;
            Debug.LogError(
                $"[STORY_GRAPH][FAILED] platform={Application.platform} storyId='{CurrentStoryId}' " +
                $"chapterAsset='{chapter.name}' chapterId='{chapter.ChapterId}' episodeId='{episodeId}' " +
                $"reason=No_usable_local_or_remote_graph hasJsonAsset={hasJsonAsset} jsonBytes={jsonLength} " +
                $"hasStoryGraph={chapter.graph != null} remoteEnabled={PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled} " +
                $"catalogHasRemote={NetworkManager.HasCatalogRemoteContent(episodeId)}.",
                this);
        }

        return chapter.graph;
    }

    void SetLastResolvedGraphSource(string source, string episodeId, string contentVersion)
    {
        LastResolvedGraphSource = source ?? "";
        LastResolvedGraphEpisodeId = episodeId ?? "";
        LastResolvedGraphContentVersion = contentVersion ?? "";

        Debug.Log(
            $"[STORY_GRAPH][RESOLVED] platform={Application.platform} storyId='{CurrentStoryId}' " +
            $"chapterId='{CurrentChapterId}' episodeId='{LastResolvedGraphEpisodeId}' " +
            $"source='{LastResolvedGraphSource}' contentVersion='{LastResolvedGraphContentVersion}'.",
            this);
    }

    bool TryResolveJsonGraphForChapter(ChapterData chapter, string episodeId, out StoryGraph graph)
    {
        graph = null;
        if (chapter == null)
            return false;

        StoryRuntimeAssetRegistryResolver.SetActiveStory(CurrentStoryId);

        TextAsset jsonAsset = chapter.jsonGraph;
        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
        {
            jsonAsset = StoryRuntimeAssetRegistryResolver.Resolve<TextAsset>(episodeId);
            if (jsonAsset == null)
                jsonAsset = StoryRuntimeAssetRegistryResolver.Resolve<TextAsset>(chapter.ChapterId);
            if (jsonAsset == null)
                jsonAsset = StoryRuntimeAssetRegistryResolver.Resolve<TextAsset>(chapter.name);

            if (jsonAsset != null && !string.IsNullOrWhiteSpace(jsonAsset.text))
            {
                Debug.Log(
                    $"[STORY_GRAPH][JSON_REGISTRY_FALLBACK] platform={Application.platform} storyId='{CurrentStoryId}' " +
                    $"chapterId='{chapter.ChapterId}' episodeId='{episodeId}' jsonAsset='{jsonAsset.name}'.",
                    this);
            }
        }

        if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
            return false;

        string cacheKey = BuildJsonGraphCacheKey(chapter, episodeId, jsonAsset);
        if (JsonGraphCache.TryGetValue(cacheKey, out graph) && graph != null)
            return true;

        if (StoryJsonConverter.TryBuildGraph(jsonAsset.text, episodeId, out graph, out var reason, CreateJsonAssetResolver(chapter)))
        {
            JsonGraphCache[cacheKey] = graph;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.LogError(
                "[STORY_GRAPH][JSON_FAILED] storyId='" + CurrentStoryId +
                "' episodeId='" + episodeId + "' json='" + jsonAsset.name +
                "' registry=" + StoryRuntimeAssetRegistryResolver.DescribeActiveRegistry() +
                " reason=" + reason,
                this);
        }

        graph = null;
        return false;
    }

    StoryJsonAssetResolver CreateJsonAssetResolver(ChapterData chapter)
    {
        StoryRuntimeAssetRegistryResolver.SetActiveStory(CurrentStoryId);

        // One chapter-scoped resolver keeps runtime JSON and the generated graph
        // in the same asset universe. The generated graph is only a fallback
        // for asset references; story flow still comes from the local JSON.
        return new StoryChapterJsonAssetResolver(
            chapter != null ? chapter.jsonAssetLibrary : null,
            chapter != null ? chapter.graph : null);
    }

    static string BuildJsonGraphCacheKey(ChapterData chapter, string episodeId, TextAsset jsonAsset)
    {
        if (chapter == null || jsonAsset == null)
            return "";

        string libraryKey = chapter.jsonAssetLibrary != null
            ? chapter.jsonAssetLibrary.GetInstanceID().ToString()
            : "no-library";
        string generatedGraphKey = chapter.graph != null
            ? chapter.graph.GetInstanceID().ToString()
            : "no-generated-graph";
        string registryKey = StoryRuntimeAssetRegistryResolver.ActiveStoryId + ":" +
                             StoryRuntimeAssetRegistryResolver.ActiveEntryCount;

        string jsonText = jsonAsset.text ?? "";
        return jsonAsset.GetInstanceID() +
               "::" + ComputeStableHash(jsonText) +
               "::" + jsonText.Length +
               "::" + libraryKey +
               "::" + generatedGraphKey +
               "::" + registryKey +
               "::" + (episodeId ?? "");
    }

    static uint ComputeStableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
            }

            return hash;
        }
    }
}
