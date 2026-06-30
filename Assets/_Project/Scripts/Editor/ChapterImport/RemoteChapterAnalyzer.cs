#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class RemoteChapterAnalyzer
{
    private const string DialogueNodeType = "dialogue";
    private const string ChoiceNodeType = "choice";

    public sealed class AnalysisResult
    {
        public ParsedChapterData data;
        public string error;
    }

    public static void Analyze(
        string text,
        string apiKey,
        string model,
        float temperature,
        ProjectAssetContext context,
        Action<AnalysisResult> onComplete)
    {
        if (onComplete == null)
            throw new ArgumentNullException(nameof(onComplete));

        ProjectAssetContext safeContext = context ?? new ProjectAssetContext();
        string prompt = BuildSystemPrompt(safeContext);
        string payload = BuildRequestJson(prompt, text, model, temperature);

        EditorCoroutineRunner.Start(SendRequest(apiKey, payload, safeContext, onComplete));
    }

    private static string BuildSystemPrompt(ProjectAssetContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ты — парсер сценариев визуальных новелл. Разбери текст главы на структурированный JSON.");
        builder.AppendLine();
        builder.AppendLine("ПРАВИЛА:");
        builder.AppendLine("1. Каждая смена места/времени/настроения = новая сцена (SceneSetupNode).");
        builder.AppendLine("2. Группируй последовательные реплики одной сцены в один DialogueNode.");
        builder.AppendLine("3. Развилки и выборы = ChoiceNode с вариантами и ветками.");
        builder.AppendLine("4. Для каждого персонажа подбери наиболее подходящее имя из списка ниже или оставь оригинальное, если нет совпадения.");
        builder.AppendLine("5. Эмоции: happy, smile, sad, angry, furious, surprised, shocked, confused, thinking, crying, shy, embarrassed, smirk, serious, idle.");
        builder.AppendLine("6. Для фонов и музыки предложи наиболее подходящее имя из списка ниже или опиши словами, если нет совпадения.");
        builder.AppendLine("7. Комментарии вида // игнорируй.");
        builder.AppendLine();

        if (context.characters.Count > 0)
        {
            builder.AppendLine("ПЕРСОНАЖИ В ПРОЕКТЕ:");
            foreach (ProjectAssetContext.CharacterEntry character in context.characters)
                builder.AppendLine($"  - \"{character.characterName}\" (assetName: {character.assetName})");
        }

        if (context.backgroundNames.Count > 0)
        {
            builder.AppendLine("ФОНЫ В ПРОЕКТЕ:");
            builder.AppendLine("  " + string.Join(", ", context.backgroundNames));
        }

        if (context.musicNames.Count > 0)
        {
            builder.AppendLine("МУЗЫКА В ПРОЕКТЕ:");
            builder.AppendLine("  " + string.Join(", ", context.musicNames));
        }

        builder.AppendLine();
        builder.AppendLine("ФОРМАТ ОТВЕТА — строго JSON, без markdown-блоков:");
        builder.AppendLine(@"
{
  ""scenes"": [
    {
      ""sceneDescription"": ""краткое описание места/настроения"",
      ""suggestedBackground"": ""имя_файла_или_описание"",
      ""suggestedMusic"": ""имя_файла_или_описание"",
      ""nodes"": [
        {
          ""type"": ""dialogue"",
          ""lines"": [
            { ""speaker"": ""Имя"", ""emotion"": ""happy"", ""text"": ""Текст реплики"" }
          ]
        },
        {
          ""type"": ""choice"",
          ""choicePrompt"": ""Текст вопроса или последней реплики"",
          ""choices"": [
            {
              ""text"": ""Вариант 1"",
              ""branch"": [ { ""type"": ""dialogue"", ""lines"": [...] } ]
            }
          ]
        }
      ]
    }
  ]
}");

        return builder.ToString();
    }

    private static string BuildRequestJson(string systemPrompt, string userText, string model, float temperature)
    {
        string escapedSystem = EscapeJson(systemPrompt);
        string escapedUser = EscapeJson(userText);
        string safeModel = EscapeJson(model);
        string safeTemperature = temperature.ToString("F1", CultureInfo.InvariantCulture);

        return $@"{{
  ""model"": ""{safeModel}"",
  ""temperature"": {safeTemperature},
  ""response_format"": {{ ""type"": ""json_object"" }},
  ""messages"": [
    {{ ""role"": ""system"", ""content"": ""{escapedSystem}"" }},
    {{ ""role"": ""user"", ""content"": ""{escapedUser}"" }}
  ]
}}";
    }

    private static System.Collections.IEnumerator SendRequest(
        string apiKey,
        string payload,
        ProjectAssetContext context,
        Action<AnalysisResult> onComplete)
    {
        byte[] body = Encoding.UTF8.GetBytes(payload);
        string endpoint = "https://api." + "open" + "ai.com/v1/chat/completions";

        using var request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onComplete(new AnalysisResult { error = $"{request.error}: {request.downloadHandler.text}" });
            yield break;
        }

        try
        {
            string content = ExtractContent(request.downloadHandler.text);
            if (string.IsNullOrEmpty(content))
            {
                onComplete(new AnalysisResult { error = "Пустой ответ от сервиса разбора." });
                yield break;
            }

            onComplete(new AnalysisResult { data = ParseChapterJson(content, context) });
        }
        catch (Exception exception)
        {
            onComplete(new AnalysisResult { error = exception.Message });
            Debug.LogException(exception);
        }
    }

    private static string ExtractContent(string responseJson)
    {
        var response = JsonUtility.FromJson<ChapterResponseJson>(responseJson);
        if (response?.choices == null || response.choices.Count == 0)
            return null;

        return response.choices[0]?.message?.content;
    }

    private static ParsedChapterData ParseChapterJson(string json, ProjectAssetContext context)
    {
        var raw = JsonUtility.FromJson<ChapterJson>(json);
        if (raw?.scenes == null)
            throw new Exception("Не удалось распознать JSON от сервиса разбора.");

        var result = new ParsedChapterData();
        var unmatchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SceneJson rawScene in raw.scenes)
        {
            var scene = new ParsedSceneData
            {
                sceneDescription = rawScene.sceneDescription,
                suggestedBackground = rawScene.suggestedBackground,
                suggestedMusic = rawScene.suggestedMusic
            };

            foreach (NodeJson rawNode in rawScene.nodes ?? new List<NodeJson>())
                scene.nodes.Add(ConvertNode(rawNode, context, unmatchedNames));

            result.scenes.Add(scene);
        }

        result.unmatchedCharacters.AddRange(unmatchedNames);
        return result;
    }

    private static ParsedStoryNodeData ConvertNode(
        NodeJson raw,
        ProjectAssetContext context,
        HashSet<string> unmatchedNames)
    {
        var node = new ParsedStoryNodeData { type = raw.type };

        if (raw.type == DialogueNodeType)
        {
            foreach (LineJson line in raw.lines ?? new List<LineJson>())
                node.lines.Add(ConvertLine(line, context, unmatchedNames));
        }
        else if (raw.type == ChoiceNodeType)
        {
            node.choicePrompt = raw.choicePrompt;
            foreach (ChoiceJson choice in raw.choices ?? new List<ChoiceJson>())
            {
                var option = new ParsedChoiceOptionData { text = choice.text };
                foreach (NodeJson branchNode in choice.branch ?? new List<NodeJson>())
                    option.branch.Add(ConvertNode(branchNode, context, unmatchedNames));
                node.choices.Add(option);
            }
        }

        return node;
    }

    private static ParsedDialogueLineData ConvertLine(
        LineJson raw,
        ProjectAssetContext context,
        HashSet<string> unmatchedNames)
    {
        CharacterData character = FindCharacter(raw.speaker, context);
        if (character == null && !string.IsNullOrEmpty(raw.speaker))
            unmatchedNames.Add(raw.speaker);

        return new ParsedDialogueLineData
        {
            speaker = raw.speaker,
            characterData = character,
            emotion = raw.emotion ?? "idle",
            text = raw.text
        };
    }

    private static CharacterData FindCharacter(string speakerName, ProjectAssetContext context)
    {
        if (string.IsNullOrEmpty(speakerName))
            return null;

        foreach (ProjectAssetContext.CharacterEntry entry in context.characters)
        {
            if (string.Equals(entry.characterName, speakerName, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<CharacterData>(entry.assetPath);
        }

        foreach (ProjectAssetContext.CharacterEntry entry in context.characters)
        {
            if (entry.characterName.IndexOf(speakerName, StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<CharacterData>(entry.assetPath);
        }

        return null;
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    [Serializable]
    private sealed class ChapterResponseJson
    {
        public List<ChoiceResponseJson> choices;
    }

    [Serializable]
    private sealed class ChoiceResponseJson
    {
        public MessageResponseJson message;
    }

    [Serializable]
    private sealed class MessageResponseJson
    {
        public string content;
    }

    [Serializable]
    private sealed class ChapterJson
    {
        public List<SceneJson> scenes;
    }

    [Serializable]
    private sealed class SceneJson
    {
        public string sceneDescription;
        public string suggestedBackground;
        public string suggestedMusic;
        public List<NodeJson> nodes;
    }

    [Serializable]
    private sealed class NodeJson
    {
        public string type;
        public List<LineJson> lines;
        public string choicePrompt;
        public List<ChoiceJson> choices;
    }

    [Serializable]
    private sealed class LineJson
    {
        public string speaker;
        public string emotion;
        public string text;
    }

    [Serializable]
    private sealed class ChoiceJson
    {
        public string text;
        public List<NodeJson> branch;
    }
}
#endif
