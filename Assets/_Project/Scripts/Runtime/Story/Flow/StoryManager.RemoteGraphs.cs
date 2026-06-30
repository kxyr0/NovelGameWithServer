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

    StoryGraph ResolveGraphForChapter(ChapterData chapter)
    {
        if (chapter == null)
            return null;

        string episodeId = ResolveChapterEpisodeId(chapter);
#if UNITY_EDITOR
        if (TryResolveJsonGraphForChapter(chapter, episodeId, out var editorJsonGraph))
            return editorJsonGraph;
#endif

        if (PrototypeFeatureFlags.RemoteEpisodeGraphsEnabled &&
            !string.IsNullOrEmpty(episodeId) &&
            NetworkManager.HasCatalogRemoteContent(episodeId) &&
            RemoteEpisodeGraphCache.TryLoad(episodeId, out var cacheEntry))
        {
            if (RemoteStoryGraphImporter.TryBuildGraph(cacheEntry, out var remoteGraph, out var reason, CreateJsonAssetResolver(chapter)))
                return remoteGraph;

            if (!string.IsNullOrWhiteSpace(reason) && reason != "remote graph has no nodes")
                Debug.LogWarning("[StoryManager] Remote graph fallback to local for " + episodeId + ": " + reason);
        }

#if !UNITY_EDITOR
        if (TryResolveJsonGraphForChapter(chapter, episodeId, out var jsonGraph))
            return jsonGraph;
#endif

        return chapter.graph;
    }

    bool TryResolveJsonGraphForChapter(ChapterData chapter, string episodeId, out StoryGraph graph)
    {
        graph = null;

        if (chapter == null || chapter.jsonGraph == null || string.IsNullOrWhiteSpace(chapter.jsonGraph.text))
            return false;

        string cacheKey = BuildJsonGraphCacheKey(chapter, episodeId);
        if (JsonGraphCache.TryGetValue(cacheKey, out graph) && graph != null)
            return true;

        if (StoryJsonConverter.TryBuildGraph(chapter.jsonGraph.text, episodeId, out graph, out var reason, CreateJsonAssetResolver(chapter)))
        {
            JsonGraphCache[cacheKey] = graph;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(reason))
            Debug.LogError("[StoryManager] JSON graph fallback to local StoryGraph for " + ResolveChapterEpisodeId(chapter) + ": " + reason);

        graph = null;
        return false;
    }

    StoryJsonAssetResolver CreateJsonAssetResolver(ChapterData chapter)
    {
        return chapter != null && chapter.jsonAssetLibrary != null
            ? new StoryJsonAssetLibraryResolver(chapter.jsonAssetLibrary)
            : new StoryJsonAssetResolver();
    }

    static string BuildJsonGraphCacheKey(ChapterData chapter, string episodeId)
    {
        if (chapter == null || chapter.jsonGraph == null)
            return "";

        string libraryKey = chapter.jsonAssetLibrary != null
            ? chapter.jsonAssetLibrary.GetInstanceID().ToString()
            : "no-library";

        string jsonText = chapter.jsonGraph.text ?? "";
        return chapter.jsonGraph.GetInstanceID() +
               "::" + ComputeStableHash(jsonText) +
               "::" + jsonText.Length +
               "::" + libraryKey +
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
