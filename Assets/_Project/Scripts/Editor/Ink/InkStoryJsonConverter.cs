#if UNITY_EDITOR
using System.Collections.Generic;
using Ink.Runtime;
using Ink.UnityIntegration;
using UnityEngine;
using static InkStoryJsonUtility;

public static class InkStoryJsonConverter
{
    private const int MaxNodes = 10000;

    public static bool TryConvert(
        InkFile inkFile,
        string storyId,
        string episodeId,
        out string json,
        out string error)
    {
        json = "";
        error = "";
        if (inkFile == null || !inkFile.isCompiled)
            return Fail("Ink-файл не скомпилирован.", out error);

        var context = new InkStoryJsonConvertContext(new Story(inkFile.storyJson));
        var document = new StoryJsonDocument
        {
            version = 2,
            storyId = FirstNonEmpty(storyId, ReadRootTag(context.Story, "storyId"), "ink_story"),
            chapterId = FirstNonEmpty(episodeId, ReadRootTag(context.Story, "episodeId"), inkFile.name),
            episodeId = FirstNonEmpty(episodeId, ReadRootTag(context.Story, "episodeId"), inkFile.name),
            title = FirstNonEmpty(ReadRootTag(context.Story, "title"), inkFile.name),
            defaultName = FirstNonEmpty(ReadRootTag(context.Story, "defaultName"), ReadRootTag(context.Story, "defaultPlayerName")),
            defaultPlayerName = FirstNonEmpty(ReadRootTag(context.Story, "defaultPlayerName"), ReadRootTag(context.Story, "defaultName"))
        };

        string firstNodeId = ProcessCurrentState(context);
        if (!string.IsNullOrEmpty(context.Error))
            return Fail(context.Error, out error);

        document.nodes.Add(new StoryJsonNode
        {
            id = "start",
            guid = "start",
            type = StoryJsonTypes.Start,
            next = firstNodeId,
            title = "Start"
        });
        document.nodes.AddRange(context.Nodes);
        json = JsonUtility.ToJson(document, true);
        return StoryJsonConverter.IsCanonicalJson(json) || Fail("Ink экспорт не создал Story JSON.", out error);
    }

    private static string ProcessCurrentState(InkStoryJsonConvertContext context)
    {
        string firstId = "";
        StoryJsonNode lastNode = null;

        while (context.Story.canContinue)
        {
            string rawLine = context.Story.Continue();
            List<string> tags = context.Story.currentTags;
            StoryJsonNode sceneNode = BuildSceneNode(context, tags);
            if (sceneNode != null)
                Append(context, sceneNode, ref firstId, ref lastNode);

            foreach (StoryJsonNode tagNode in InkStoryJsonTagNodeFactory.Build(context, tags))
                Append(context, tagNode, ref firstId, ref lastNode);

            string line = Clean(rawLine);
            if (!string.IsNullOrWhiteSpace(line))
                Append(context, BuildDialogueNode(context, line), ref firstId, ref lastNode);

            if (!string.IsNullOrEmpty(context.Error))
                return firstId;
        }

        if (context.Story.currentChoices.Count > 0)
            Append(context, BuildChoiceNode(context), ref firstId, ref lastNode);

        return firstId;
    }

    private static StoryJsonNode BuildDialogueNode(InkStoryJsonConvertContext context, string line)
    {
        string id = context.NextId("dialogue");
        SplitSpeaker(line, out string speaker, out string text);
        return new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.Dialogue,
            title = FirstNonEmpty(speaker, "Реплика"),
            lines = new List<StoryJsonLine>
            {
                new StoryJsonLine { speaker = speaker, text = text }
            }
        };
    }

    private static StoryJsonNode BuildChoiceNode(InkStoryJsonConvertContext context)
    {
        string id = context.NextId("choice");
        var node = new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.Choice,
            title = "Выбор",
            choicePrompt = "Выбор"
        };

        string savedState = context.Story.state.ToJson();
        List<Choice> choices = new List<Choice>(context.Story.currentChoices);
        for (int i = 0; i < choices.Count; i++)
        {
            context.Story.state.LoadJson(savedState);
            context.Story.ChooseChoiceIndex(i);
            string next = ProcessCurrentState(context);
            node.choices.Add(new StoryJsonChoice
            {
                text = Clean(choices[i].text),
                next = next,
                isPremium = TryReadPremiumCost(choices[i].tags, out int cost),
                premiumCost = cost
            });
        }

        context.Story.state.LoadJson(savedState);
        return node;
    }

    private static StoryJsonNode BuildSceneNode(InkStoryJsonConvertContext context, List<string> tags)
    {
        if (!TryReadTag(tags, "scene", out string scene) &&
            !TryReadTag(tags, "bg", out string background) &&
            !TryReadTag(tags, "music", out string music))
        {
            return null;
        }

        TryReadTag(tags, "scene", out scene);
        TryReadTag(tags, "bg", out background);
        TryReadTag(tags, "music", out music);
        string id = context.NextId("scene");
        return new StoryJsonNode
        {
            id = id,
            guid = id,
            type = StoryJsonTypes.Scene,
            title = FirstNonEmpty(scene, "Сцена"),
            label = scene,
            background = background,
            music = music
        };
    }

    private static void Append(InkStoryJsonConvertContext context, StoryJsonNode node, ref string firstId, ref StoryJsonNode lastNode)
    {
        if (context.Nodes.Count >= MaxNodes)
        {
            context.Error = "Ink экспорт остановлен: слишком много узлов.";
            return;
        }

        if (string.IsNullOrEmpty(firstId))
            firstId = node.id;
        if (lastNode != null)
            lastNode.next = node.id;
        context.Nodes.Add(node);
        lastNode = node;
    }

}
#endif
