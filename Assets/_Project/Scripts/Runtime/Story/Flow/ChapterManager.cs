using UnityEngine;
using System.Collections.Generic;

public class ChapterManager : MonoBehaviour
{
    public List<ChapterData> chapters = new List<ChapterData>();
    public List<string> unlockedChapters = new List<string>();

    public bool IsUnlocked(ChapterData chapter)
    {
        string episodeId = GetChapterKey(chapter);
        bool isPremium = chapter != null && chapter.isPremium;
        isPremium = isPremium || NetworkManager.IsCatalogEpisodePremium(episodeId, false);
        if (!isPremium) return true;
        if (NetworkManager.IsCatalogEpisodeUnlocked(episodeId, false)) return true;
        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled) return false;
        return unlockedChapters.Contains(GetChapterKey(chapter));
    }

    public void Unlock(ChapterData chapter)
    {
        if (!PrototypeFeatureFlags.LocalPremiumSpendEnabled)
        {
            Debug.LogWarning("[ChapterManager] Local premium chapter unlock is disabled. Unlock through API/IAP.");
            return;
        }

        int localCost = chapter != null ? SaveDataSanitizer.ClampCurrencyValue(chapter.unlockCost) : 0;
        int cost = Mathf.Max(localCost, NetworkManager.GetCatalogEpisodeCandleCost(GetChapterKey(chapter), localCost));
        if (cost <= 0)
        {
            Debug.LogWarning("[ChapterManager] Refused chapter unlock with invalid cost: " + cost);
            return;
        }

        string chapterKey = GetChapterKey(chapter);
        if (string.IsNullOrEmpty(chapterKey) || unlockedChapters.Contains(chapterKey))
            return;

        if (NetworkManager.IsCatalogEpisodeUnlocked(chapterKey, false))
        {
            unlockedChapters.Add(chapterKey);
            return;
        }

        if (GameState.Instance != null && GameState.Instance.SpendCurrency(cost))
            unlockedChapters.Add(chapterKey);
    }

    static string GetChapterKey(ChapterData chapter)
    {
        if (chapter == null) return "";
        return SaveDataSanitizer.SanitizeIdentifier(string.IsNullOrEmpty(chapter.chapterId) ? chapter.chapterName : chapter.chapterId);
    }
}
