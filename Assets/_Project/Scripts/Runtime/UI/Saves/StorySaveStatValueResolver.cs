using System;
using UnityEngine;

public static class StorySaveStatValueResolver
{
    public static int Resolve(SaveData save, GameStoryStatData stat)
    {
        if (stat == null)
            return 0;

        string requested = StoryStatId.Canonical(stat.StatId);
        if (string.IsNullOrEmpty(requested))
            return stat.Value;

        if (save == null || save.statKeys == null || save.statValues == null)
            return stat.Value;

        int count = Mathf.Min(save.statKeys.Count, save.statValues.Count);
        for (int i = 0; i < count; i++)
        {
            if (!string.Equals(
                StoryStatId.Canonical(save.statKeys[i]),
                requested,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return SaveDataSanitizer.ClampStatValue(
                save.statValues[i]);
        }

        return stat.Value;
    }
}
