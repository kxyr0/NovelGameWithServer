using System;

public static class PlayerNameInflector
{
    public static string Resolve(
        string playerName,
        PlayerNameCase grammaticalCase,
        PlayerNameCaseForms overrides = null)
    {
        string safeName = SaveDataSanitizer.SanitizePlayerName(playerName);
        if (string.IsNullOrWhiteSpace(safeName))
            return "";

        if (overrides != null)
        {
            string overrideForm = SaveDataSanitizer.SanitizePlayerName(overrides.Get(grammaticalCase));
            if (!string.IsNullOrWhiteSpace(overrideForm))
                return overrideForm;
        }

        if (grammaticalCase == PlayerNameCase.Nominative)
            return safeName;

        return TryInflectLastNamePart(safeName, grammaticalCase, out string inflectedName)
            ? inflectedName
            : safeName;
    }

    public static bool TryParseCaseCode(string rawCode, out PlayerNameCase grammaticalCase)
    {
        grammaticalCase = PlayerNameCase.Nominative;
        string code = NormalizeCaseCode(rawCode);
        if (string.IsNullOrEmpty(code))
            return true;

        switch (code)
        {
            case "nom":
            case "nominative":
            case "im":
            case "imen":
            case "imenitelny":
            case "imenitelnyy":
            case "imenitelnij":
            case "imenitelnyi":
            case "\u0438\u043c":
            case "\u0438\u043c\u0435\u043d":
            case "\u0438\u043c\u0435\u043d\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Nominative;
                return true;
            case "gen":
            case "genitive":
            case "rod":
            case "roditelny":
            case "roditelnyy":
            case "\u0440\u043e\u0434":
            case "\u0440\u043e\u0434\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Genitive;
                return true;
            case "dat":
            case "dative":
            case "datelny":
            case "datelnyy":
            case "\u0434\u0430\u0442":
            case "\u0434\u0430\u0442\u0435\u043b\u044c\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Dative;
                return true;
            case "acc":
            case "accusative":
            case "vin":
            case "vinitelny":
            case "vinitelnyy":
            case "\u0432\u0438\u043d":
            case "\u0432\u0438\u043d\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Accusative;
                return true;
            case "ins":
            case "instr":
            case "instrumental":
            case "tvor":
            case "tvoritelny":
            case "tvoritelnyy":
            case "\u0442\u0432\u043e\u0440":
            case "\u0442\u0432\u043e\u0440\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Instrumental;
                return true;
            case "prep":
            case "pre":
            case "loc":
            case "prepositional":
            case "predl":
            case "predlozhny":
            case "predlozhnyy":
            case "\u043f\u0440\u0435\u0434\u043b":
            case "\u043f\u0440\u0435\u0434\u043b\u043e\u0436\u043d\u044b\u0439":
                grammaticalCase = PlayerNameCase.Prepositional;
                return true;
            default:
                return false;
        }
    }

    public static bool HasAnyCaseForms(PlayerNameCaseForms forms)
    {
        return forms != null && forms.HasAny();
    }

    static string NormalizeCaseCode(string rawCode)
    {
        string code = (rawCode ?? "").Trim();
        if (string.IsNullOrEmpty(code))
            return "";

        code = code.Trim('{', '}', '[', ']', '<', '>', ':', '.', '_', '-');
        return code.Trim().ToLowerInvariant().Replace("\u0451", "\u0435");
    }

    static bool TryInflectLastNamePart(string playerName, PlayerNameCase grammaticalCase, out string inflectedName)
    {
        inflectedName = playerName;
        int partStart = FindLastNamePartStart(playerName);
        string prefix = playerName.Substring(0, partStart);
        string part = playerName.Substring(partStart);

        if (!TryInflectSimpleRussianFemaleName(part, grammaticalCase, out string inflectedPart))
            return false;

        inflectedName = prefix + inflectedPart;
        return true;
    }

    static int FindLastNamePartStart(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int lastSeparator = -1;
        char[] separators = { ' ', '-', '\u2011', '\u2013' };
        for (int i = 0; i < separators.Length; i++)
        {
            int index = value.LastIndexOf(separators[i]);
            if (index > lastSeparator)
                lastSeparator = index;
        }

        return lastSeparator >= 0 && lastSeparator + 1 < value.Length
            ? lastSeparator + 1
            : 0;
    }

    static bool TryInflectSimpleRussianFemaleName(
        string name,
        PlayerNameCase grammaticalCase,
        out string inflectedName)
    {
        inflectedName = name;
        if (string.IsNullOrWhiteSpace(name) || !IsCyrillicWord(name))
            return false;

        string lower = name.ToLowerInvariant();
        if (lower.EndsWith("\u0438\u044f", StringComparison.Ordinal))
            return InflectIyaName(name, grammaticalCase, out inflectedName);
        if (lower.EndsWith("\u0430", StringComparison.Ordinal))
            return InflectAName(name, grammaticalCase, out inflectedName);
        if (lower.EndsWith("\u044f", StringComparison.Ordinal))
            return InflectYaName(name, grammaticalCase, out inflectedName);

        return false;
    }

    static bool InflectIyaName(string name, PlayerNameCase grammaticalCase, out string inflectedName)
    {
        string stem = name.Substring(0, name.Length - 1);
        switch (grammaticalCase)
        {
            case PlayerNameCase.Genitive:
            case PlayerNameCase.Dative:
            case PlayerNameCase.Prepositional:
                inflectedName = stem + MatchSuffixCase(name, "\u0438");
                return true;
            case PlayerNameCase.Accusative:
                inflectedName = stem + MatchSuffixCase(name, "\u044e");
                return true;
            case PlayerNameCase.Instrumental:
                inflectedName = stem + MatchSuffixCase(name, "\u0435\u0439");
                return true;
            default:
                inflectedName = name;
                return true;
        }
    }

    static bool InflectAName(string name, PlayerNameCase grammaticalCase, out string inflectedName)
    {
        string stem = name.Substring(0, name.Length - 1);
        switch (grammaticalCase)
        {
            case PlayerNameCase.Genitive:
                inflectedName = stem + MatchSuffixCase(name, NeedsIAfterAStem(stem) ? "\u0438" : "\u044b");
                return true;
            case PlayerNameCase.Dative:
            case PlayerNameCase.Prepositional:
                inflectedName = stem + MatchSuffixCase(name, "\u0435");
                return true;
            case PlayerNameCase.Accusative:
                inflectedName = stem + MatchSuffixCase(name, "\u0443");
                return true;
            case PlayerNameCase.Instrumental:
                inflectedName = stem + MatchSuffixCase(name, NeedsSoftInstrumentalAfterAStem(stem) ? "\u0435\u0439" : "\u043e\u0439");
                return true;
            default:
                inflectedName = name;
                return true;
        }
    }

    static bool InflectYaName(string name, PlayerNameCase grammaticalCase, out string inflectedName)
    {
        string stem = name.Substring(0, name.Length - 1);
        switch (grammaticalCase)
        {
            case PlayerNameCase.Genitive:
            case PlayerNameCase.Dative:
            case PlayerNameCase.Prepositional:
                inflectedName = stem + MatchSuffixCase(name, "\u0438");
                return true;
            case PlayerNameCase.Accusative:
                inflectedName = stem + MatchSuffixCase(name, "\u044e");
                return true;
            case PlayerNameCase.Instrumental:
                inflectedName = stem + MatchSuffixCase(name, "\u0435\u0439");
                return true;
            default:
                inflectedName = name;
                return true;
        }
    }

    static bool NeedsIAfterAStem(string stem)
    {
        if (string.IsNullOrEmpty(stem))
            return false;

        char last = char.ToLowerInvariant(stem[stem.Length - 1]);
        return last == '\u0433' || last == '\u043a' || last == '\u0445' ||
               last == '\u0436' || last == '\u0447' || last == '\u0448' ||
               last == '\u0449';
    }

    static bool NeedsSoftInstrumentalAfterAStem(string stem)
    {
        if (string.IsNullOrEmpty(stem))
            return false;

        char last = char.ToLowerInvariant(stem[stem.Length - 1]);
        return last == '\u0436' || last == '\u0447' || last == '\u0448' ||
               last == '\u0449';
    }

    static string MatchSuffixCase(string name, string suffix)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(suffix))
            return suffix;

        bool isUpper = true;
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsLetter(name[i]) && !char.IsUpper(name[i]))
            {
                isUpper = false;
                break;
            }
        }

        return isUpper ? suffix.ToUpperInvariant() : suffix;
    }

    static bool IsCyrillicWord(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!IsCyrillicLetter(c))
                return false;
        }

        return true;
    }

    static bool IsCyrillicLetter(char c)
    {
        return c == '\u0401' ||
               c == '\u0451' ||
               (c >= '\u0410' && c <= '\u044f');
    }
}
