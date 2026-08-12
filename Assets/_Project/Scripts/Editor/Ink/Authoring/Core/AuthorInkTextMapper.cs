#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

internal sealed class AuthorInkTextMapper
{
    static readonly Regex PlainSpeakerRegex = new Regex(
        @"^(?<speaker>[^:]{1,48}):\s*(?<text>.*)$",
        RegexOptions.Compiled);

    static readonly Regex MissingColonRegex = new Regex(
        @"^(?<speaker>[^:()]{1,48})\s*\((?<emotion>[^)]{1,100})\)\s*(?:[:.…]+\s*)?(?<text>.+)$",
        RegexOptions.Compiled);

    readonly AuthorInkSharedContext _context;

    public AuthorInkTextMapper(AuthorInkSharedContext context)
    {
        _context = context ?? new AuthorInkSharedContext();
    }

    public StoryJsonLine Map(AuthorInkTextLine source, AuthorInkImportReport report)
    {
        string raw = AuthorInkSyntax.Trim(source.Raw);
        if (AuthorInkSyntax.TrySpeaker(raw, out string speaker, out string emotion, out string text))
            return BuildLine(speaker, emotion, text);

        Match missingColon = MissingColonRegex.Match(raw);
        if (missingColon.Success && _context.Speakers.Contains(missingColon.Groups["speaker"].Value.Trim()))
        {
            report.Warn(source.Line, "Исправлена реплика без ':' после эмоции.");
            return BuildLine(
                missingColon.Groups["speaker"].Value.Trim(),
                missingColon.Groups["emotion"].Value.Trim(),
                missingColon.Groups["text"].Value.Trim());
        }

        Match plain = PlainSpeakerRegex.Match(raw);
        if (plain.Success && _context.Speakers.Contains(plain.Groups["speaker"].Value.Trim()))
            return BuildLine(plain.Groups["speaker"].Value.Trim(), "", plain.Groups["text"].Value.Trim());

        return new StoryJsonLine
        {
            speaker = "",
            emotion = "",
            text = StoryJsonConverter.SanitizeDisplayText(raw)
        };
    }

    static StoryJsonLine BuildLine(string speaker, string emotion, string text)
    {
        string mapped = MapEmotion(emotion);
        return new StoryJsonLine
        {
            speaker = StoryJsonConverter.SanitizeDisplayText(speaker),
            emotion = mapped,
            text = StoryJsonConverter.SanitizeDisplayText(text),
            authorComment = string.IsNullOrWhiteSpace(emotion) || IsDirectEmotionMatch(emotion, mapped)
                ? ""
                : "Ink emotion: " + emotion.Trim()
        };
    }

    static bool IsDirectEmotionMatch(string source, string mapped)
    {
        string normalized = Normalize(source);
        return string.Equals(normalized, mapped.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    static string MapEmotion(string source)
    {
        string value = Normalize(source);
        if (string.IsNullOrEmpty(value)) return "";
        if (ContainsAny(value, "ярост", "в бешен", "furious")) return "Furious";
        if (ContainsAny(value, "злост", "злость", "злой", "angry")) return "Angry";
        if (ContainsAny(value, "раздраж", "недоволь", "annoy")) return "Annoyed";
        if (ContainsAny(value, "отвращ", "disgust")) return "Disgust";
        if (ContainsAny(value, "плач", "слез", "cry")) return "Crying";
        if (ContainsAny(value, "испуг", "страх", "scared")) return "Scared";
        if (ContainsAny(value, "шок", "shocked")) return "Shocked";
        if (ContainsAny(value, "удив", "surpris")) return "Surprised";
        if (ContainsAny(value, "смущ", "embarrass")) return "Embarrassed";
        if (ContainsAny(value, "стыд", "shy")) return "Shy";
        if (ContainsAny(value, "груст", "печал", "sad")) return "Sad";
        if (ContainsAny(value, "хмур", "frown")) return "Frown";
        if (ContainsAny(value, "серьез", "serious")) return "Serious";
        if (ContainsAny(value, "задум", "thinking")) return "Thinking";
        if (ContainsAny(value, "прищур", "scull")) return "Scull";
        if (ContainsAny(value, "поднят", "выгнул бров", "бров", "raised eyebrow")) return "RaisedEyebrow";
        if (ContainsAny(value, "закрыт", "глаз", "eyes closed") && !value.Contains("широко")) return "EyesClosed";
        if (ContainsAny(value, "закат", "eye roll")) return "EyeRoll";
        if (ContainsAny(value, "взгляд вправо", "взгляд влево", "взгляд в сторону", "look to side")) return "LookToSide";
        if (ContainsAny(value, "взгляд вниз", "верхний угол", "верхний левый", "отведя взгляд", "виноватый взгляд", "неловкий взгляд", "avert")) return "Averted";
        if (ContainsAny(value, "ехид", "ухмыл", "smirk")) return "Smirk";
        if (ContainsAny(value, "улыбка с зуб", "широк", "wide smile")) return "WideSmile";
        if (ContainsAny(value, "улыб", "smile")) return "Smile";
        if (ContainsAny(value, "счаст", "радост", "happy")) return "Happy";
        if (ContainsAny(value, "вопрос", "растерян", "недоумен", "confus")) return "Confused";
        if (ContainsAny(value, "безразлич", "indifference")) return "Indifference";
        if (ContainsAny(value, "возмущ", "indignant")) return "Indignant";
        if (ContainsAny(value, "отвлеч", "distraction")) return "Distraction";
        if (ContainsAny(value, "нейтрал", "черный силуэт", "neutral")) return "Neutral";
        return "Neutral";
    }

    static string Normalize(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
    }

    static bool ContainsAny(string value, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
            if (value.Contains(needles[i]))
                return true;
        return false;
    }
}
#endif
