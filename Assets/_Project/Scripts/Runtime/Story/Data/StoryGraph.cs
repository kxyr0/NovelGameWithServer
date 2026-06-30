using System;
using System.Collections.Generic;
using UnityEngine;
using XNode;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "VN/Story Graph")]
public class StoryGraph : NodeGraph
{
    [Header("Имя ГГ")]
    [Tooltip("Разрешить игроку менять имя главной героини в настройках. Если выключено, кнопка смены имени будет заблокирована.")]
    public bool allowNameChange = true;

    [Tooltip("Имя главной героини по умолчанию для этой истории. Показывается в поле ввода при первом запуске.")]
    public string defaultPlayerName = "Героиня";

    [Tooltip("Ручные формы падежей для defaultPlayerName. Используются только пока имя игрока совпадает с defaultPlayerName.")]
    public PlayerNameCaseForms defaultPlayerNameCases = new PlayerNameCaseForms();

    [Tooltip("ID эпизода на сервере, например ep_s1e1. Используется для синхронизации прогресса.")]
    public string episodeId = "";

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureUniqueNodeGuids();
    }

    [ContextMenu("Validate Node GUIDs")]
    void ValidateNodeGuids()
    {
        EnsureUniqueNodeGuids();
    }

    bool EnsureUniqueNodeGuids()
    {
        var seen = new HashSet<string>();
        bool changed = false;

        foreach (var node in nodes)
        {
            var storyNode = node as BaseStoryNode;
            if (storyNode == null) continue;

            if (!string.IsNullOrWhiteSpace(storyNode.guid) && seen.Add(storyNode.guid))
                continue;

            storyNode.guid = Guid.NewGuid().ToString();
            seen.Add(storyNode.guid);
            EditorUtility.SetDirty(storyNode);
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(this);

        return changed;
    }
#endif
}
