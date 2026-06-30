using System;
using UnityEngine;

[Serializable]
public class DialogueCharacterEntry
{
    public CharacterData character;
    public CharacterEmotionType emotion;
    public CharacterPosition position;

    /// <summary>
    /// Имя персонажа из исходного текста, сохранённое при импорте главы.
    /// Используется StoryGraphAssetMatcher для поиска CharacterData если character == null.
    /// </summary>
    [HideInInspector] public string speakerNameHint;
}
