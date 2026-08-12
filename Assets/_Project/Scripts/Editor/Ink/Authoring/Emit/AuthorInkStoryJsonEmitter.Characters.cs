#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal sealed partial class AuthorInkStoryJsonEmitter
{
    void EmitCharacterManifest()
    {
        string heroId = string.IsNullOrWhiteSpace(_options.HeroCharacterId) ? "hero" : _options.HeroCharacterId.Trim();
        _document.characters.Add(new StoryJsonCharacter
        {
            id = heroId,
            name = string.IsNullOrWhiteSpace(_options.DefaultName) ? "Главная героиня" : _options.DefaultName.Trim(),
            asset = heroId
        });

        var speakers = new List<string>(_shared.Speakers);
        speakers.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < speakers.Count; i++)
        {
            string speaker = speakers[i];
            if (string.IsNullOrWhiteSpace(speaker) ||
                string.Equals(speaker, heroId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(speaker, _options.DefaultName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _document.characters.Add(new StoryJsonCharacter
            {
                id = speaker,
                name = speaker,
                asset = speaker
            });
        }
    }
}
#endif
