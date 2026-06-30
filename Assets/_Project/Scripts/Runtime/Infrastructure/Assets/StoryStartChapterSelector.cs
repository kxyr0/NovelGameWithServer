using System.Collections.Generic;
using UnityEngine;

public interface IStoryStartChapterSelector
{
    ChapterData SelectSavedOrFirstChapter(StoryData story, IReadOnlyList<ChapterData> chapters);
}

public sealed class SavedOrFirstStoryStartChapterSelector : IStoryStartChapterSelector
{
    public ChapterData SelectSavedOrFirstChapter(StoryData story, IReadOnlyList<ChapterData> chapters)
    {
        if (chapters == null || chapters.Count == 0)
            return null;

        if (story != null && SaveManager.Instance != null)
        {
            string storyId = ResolveStoryId(story);
            int saveSlot = StorySaveSlotSelection.GetSelectedSlot(storyId);
            SaveData save = SaveManager.Instance.LoadForStory(storyId, saveSlot);
            if (save != null)
            {
                int chapterIndex = Mathf.Clamp(save.currentChapterIndex, 0, chapters.Count - 1);
                if (chapterIndex >= 0 && chapterIndex < chapters.Count && chapters[chapterIndex] != null)
                    return chapters[chapterIndex];
            }
        }

        return chapters[0];
    }

    private static string ResolveStoryId(StoryData story)
    {
        if (story == null)
            return "";

        string storyId = SaveDataSanitizer.SanitizeIdentifier(story.StoryId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        storyId = SaveDataSanitizer.SanitizeIdentifier(story.storyId);
        if (!string.IsNullOrEmpty(storyId))
            return storyId;

        return SaveDataSanitizer.SanitizeIdentifier(story.name);
    }
}

public static class StoryStartChapterSelectors
{
    private static readonly IStoryStartChapterSelector SharedSelector = new SavedOrFirstStoryStartChapterSelector();

    public static IStoryStartChapterSelector Shared => SharedSelector;
}
