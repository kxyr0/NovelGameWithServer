#if UNITY_EDITOR
using System;

internal static class AuthorInkDirectiveMapper
{
    public static StoryJsonNode Map(
        AuthorInkDirectiveStatement directive,
        Func<string, string> idFactory,
        AuthorInkImportReport report)
    {
        string key = AuthorInkSyntax.NormalizeKey(directive.Key);
        string value = StoryJsonConverter.SanitizeDisplayText(AuthorInkSyntax.StripInlineComment(directive.Value));
        report.Directives++;

        if (key == "локация")
            return Scene(idFactory, "Локация " + value, value, "", false, "", false);

        if (key == "музыка")
        {
            bool stop = IsSilence(value);
            return Scene(idFactory, stop ? "Музыка: тишина" : "Музыка " + value, "", stop ? "" : value, stop, "", false);
        }

        if (key == "звук" || key == "звуки окружения")
        {
            bool stop = IsSilence(value);
            return Scene(idFactory, key + " " + value, "", "", false, stop ? "" : value, stop);
        }

        if (key == "уведомление" || key == "подсказка")
        {
            if (LooksLikeDuplicateStatToast(value))
            {
                report.Info(directive.Line, "Не создан дублирующий баннер статистики: " + value);
                return null;
            }

            string id = idFactory("banner");
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.Banner,
                title = key == "подсказка" ? "Подсказка" : "Уведомление",
                systemMessage = value,
                duration = 2.5f
            };
        }

        if (key == "арт")
        {
            string id = idFactory("image");
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.Image,
                title = value,
                image = value,
                caption = "Продолжить"
            };
        }

        if (key == "кат-сцена" || key == "кат сцена")
        {
            string id = idFactory("cutscene");
            report.Warn(directive.Line, "Кат-сцена импортирована по текстовому идентификатору. Проверь asset id: " + value);
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.Cutscene,
                title = value,
                image = value,
                showCharacters = false
            };
        }

        if (key == "камера")
            return MapCamera(directive, value, idFactory, report);

        if (key == "название" || key == "жанры" || key == "аннотация" || key == "статы" || key == "описание" || key.StartsWith("серия", StringComparison.Ordinal))
        {
            report.Info(directive.Line, "Служебная мета-директива пропущена: " + directive.Key);
            return null;
        }

        report.Warn(directive.Line, "Неподдержанная директива: " + directive.Key + ": " + directive.Value);
        return null;
    }

    static StoryJsonNode MapCamera(
        AuthorInkDirectiveStatement directive,
        string value,
        Func<string, string> idFactory,
        AuthorInkImportReport report)
    {
        string normalized = value.ToLowerInvariant().Replace('ё', 'е');
        if (normalized.Contains("сдвиг"))
        {
            float offset = normalized.Contains("лев") ? -250f : 250f;
            string id = idFactory("camera");
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.Camera,
                title = value,
                mode = "Offset",
                xOffset = offset,
                duration = normalized.Contains("резк") ? 0.15f : 0.35f
            };
        }

        if (normalized.Contains("ранение"))
        {
            string id = idFactory("effect");
            return new StoryJsonNode
            {
                id = id,
                guid = id,
                type = StoryJsonTypes.Effect,
                title = value,
                effect = "Shake",
                duration = 0.35f,
                intensity = 8f
            };
        }

        report.Warn(directive.Line,
            "Камера '" + value + "' не имеет эквивалента в текущих StoryJson node types; команда не превращена в фальшивую реплику.");
        return null;
    }

    static StoryJsonNode Scene(
        Func<string, string> idFactory,
        string title,
        string background,
        string music,
        bool stopMusic,
        string sfx,
        bool stopSfx)
    {
        string id = idFactory("scene");
        return new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.Scene,
            title = title,
            background = background,
            suggestedBackground = background,
            music = music,
            suggestedMusic = music,
            stopMusic = stopMusic,
            startSfx = sfx,
            stopSfx = stopSfx
        };
    }

    static bool IsSilence(string value)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
        return normalized == "тишина" || normalized == "нет" || normalized == "стоп" || normalized == "stop" || normalized == "none";
    }

    static bool LooksLikeDuplicateStatToast(string value)
    {
        string normalized = (value ?? "").Trim();
        return normalized.StartsWith("+1 Остроумие", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("+1 Решительность", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("+1 Состояние", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
