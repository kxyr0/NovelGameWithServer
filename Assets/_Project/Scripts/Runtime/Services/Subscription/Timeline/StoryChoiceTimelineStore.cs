using System;
using System.IO;
using UnityEngine;
using VContainer;

public interface IStoryChoiceTimelineStore
{
    bool TryLoad(string storyId, out StoryChoiceTimeline timeline);
    bool Save(StoryChoiceTimeline timeline);
    void Delete(string storyId);
}

public sealed class StoryChoiceTimelineStore : IStoryChoiceTimelineStore
{
    readonly string _root;

    [Inject]
    public StoryChoiceTimelineStore()
        : this(null)
    {
    }

    public StoryChoiceTimelineStore(string root = null)
    {
        _root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Application.persistentDataPath, "subscription-timelines")
            : root;
    }

    public bool TryLoad(string storyId, out StoryChoiceTimeline timeline)
    {
        timeline = null;
        string path = GetPath(storyId);
        try
        {
            if (!File.Exists(path))
                return false;
            string protectedText = File.ReadAllText(path);
            if (!LocalSaveSecurity.TryUnprotectJson(protectedText, LocalSaveSecurity.SubscriptionPurpose, out string json, out bool wasProtected) || !wasProtected)
                return false;
            timeline = JsonUtility.FromJson<StoryChoiceTimeline>(json);
            return timeline != null && timeline.schemaVersion >= 1;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[SubscriptionTimeline] Failed to load: " + exception.Message);
            timeline = null;
            return false;
        }
    }

    public bool Save(StoryChoiceTimeline timeline)
    {
        if (timeline == null)
            return false;
        try
        {
            string json = JsonUtility.ToJson(timeline, false);
            if (!SaveDataSanitizer.IsSerializedSizeAllowed(json))
                return false;
            string protectedText = LocalSaveSecurity.ProtectJson(json, LocalSaveSecurity.SubscriptionPurpose);
            if (string.IsNullOrWhiteSpace(protectedText))
                return false;
            Directory.CreateDirectory(_root);
            WriteAtomic(GetPath(timeline.storyId), protectedText);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[SubscriptionTimeline] Failed to save: " + exception.Message);
            return false;
        }
    }

    public void Delete(string storyId)
    {
        string path = GetPath(storyId);
        if (File.Exists(path))
            File.Delete(path);
    }

    string GetPath(string storyId)
    {
        return Path.Combine(_root, SaveDataSanitizer.SafeKeyPart(storyId, "story", 96) + ".timeline");
    }

    static void WriteAtomic(string path, string text)
    {
        string temp = path + ".tmp";
        string backup = path + ".bak";
        File.WriteAllText(temp, text);
        if (File.Exists(path))
            File.Replace(temp, path, backup);
        else
            File.Move(temp, path);
    }
}
