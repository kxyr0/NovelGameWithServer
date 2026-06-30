public static class StoryGraphSpeakerPositionResolver
{
    public static CharacterPosition GetDefaultPosition(string speakerName, CharacterData speaker)
    {
        return IsHeroSpeaker(speakerName, speaker) ? CharacterPosition.Left : CharacterPosition.Right;
    }

    private static bool IsHeroSpeaker(string speakerName, CharacterData speaker)
    {
        if (speaker != null && speaker.inheritAppearanceFromPlayer)
            return true;

        switch (NormalizeSpeakerToken(speakerName))
        {
            case "hero":
            case "gg":
            case "mainhero":
            case "player":
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeSpeakerToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }
}
