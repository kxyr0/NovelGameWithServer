using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public interface IStoryStartPreloadAssetCollector
{
    StoryStartPreloadAssetSet Collect(
        GameData data,
        StoryStartLoadingAssetScope assetScope,
        StoryLoadingMediaLease loadingMedia);
}

public sealed class StoryStartPreloadAssetCollector : IStoryStartPreloadAssetCollector
{
    private readonly IStoryLoadingMediaPolicy _loadingMediaPolicy;
    private readonly IStoryStartChapterSelector _chapterSelector;

    public StoryStartPreloadAssetCollector()
        : this(StoryLoadingMediaPolicies.Shared, StoryStartChapterSelectors.Shared)
    {
    }

    public StoryStartPreloadAssetCollector(IStoryLoadingMediaPolicy loadingMediaPolicy)
        : this(loadingMediaPolicy, StoryStartChapterSelectors.Shared)
    {
    }

    public StoryStartPreloadAssetCollector(
        IStoryLoadingMediaPolicy loadingMediaPolicy,
        IStoryStartChapterSelector chapterSelector)
    {
        _loadingMediaPolicy = loadingMediaPolicy ?? StoryLoadingMediaPolicies.Shared;
        _chapterSelector = chapterSelector ?? StoryStartChapterSelectors.Shared;
    }

    public StoryStartPreloadAssetSet Collect(
        GameData data,
        StoryStartLoadingAssetScope assetScope,
        StoryLoadingMediaLease loadingMedia)
    {
        var assets = new StoryStartPreloadAssetSet();
        if (data == null)
            return assets;

        CollectLoadingMediaAssets(loadingMedia, assets);

        if (_loadingMediaPolicy.ShouldPreloadLegacyMenuMedia(data, loadingMedia))
        {
            assets.Add(data.GameIcon);
            assets.Add(data.GameIconVideo);
            assets.AddGif(data.GameIconGif);
        }

        CollectWardrobeAssets(data.WardrobeSetup, assets);

        StoryData story = data.Story;
        if (story == null)
            return assets;

        if (story.TryGetStoryUiStyle(out StoryUiStyle storyStyle, out Sprite storyBackground))
        {
            assets.Add(storyBackground);
            CollectStoryUiStyle(storyStyle, assets);
        }

        if (story.TryGetCutsceneStoryUiStyle(out StoryUiStyle cutsceneStyle, out Sprite cutsceneBackground))
        {
            assets.Add(cutsceneBackground);
            CollectStoryUiStyle(cutsceneStyle, assets);
        }

        if (assetScope == StoryStartLoadingAssetScope.CoverOnly)
            return assets;

        IReadOnlyList<ChapterData> chapters = story.Chapters;
        if (chapters == null || chapters.Count == 0)
            return assets;

        if (assetScope == StoryStartLoadingAssetScope.AllChapters)
        {
            for (int i = 0; i < chapters.Count; i++)
                CollectChapterAssets(chapters[i], assets);

            return assets;
        }

        ChapterData selectedChapter = _chapterSelector.SelectSavedOrFirstChapter(story, chapters);
        CollectChapterAssets(selectedChapter, assets);
        return assets;
    }

    private static void CollectLoadingMediaAssets(StoryLoadingMediaLease loadingMedia, StoryStartPreloadAssetSet assets)
    {
        if (loadingMedia == null || assets == null)
            return;

        assets.Add(loadingMedia.CoverSprite);
        assets.Add(loadingMedia.CoverVideo);
        assets.AddGif(loadingMedia.CoverGif);
    }

    private static void CollectChapterAssets(ChapterData chapter, StoryStartPreloadAssetSet assets)
    {
        if (chapter == null || assets == null)
            return;

        assets.Add(chapter.JsonGraph);
        CollectJsonAssetLibrary(chapter.JsonAssetLibrary, assets);

        StoryGraph graph = chapter.Graph;
        if (graph == null || graph.nodes == null)
            return;

        for (int i = 0; i < graph.nodes.Count; i++)
            CollectNodeAssets(graph.nodes[i] as BaseStoryNode, assets);
    }

    private static void CollectNodeAssets(BaseStoryNode node, StoryStartPreloadAssetSet assets)
    {
        if (node == null)
            return;

        if (node is SceneSetupNode sceneNode)
            CollectSceneSetup(sceneNode.sceneData, assets);

        if (node is ImageNode imageNode)
        {
            assets.Add(imageNode.defaultImage);
            assets.Add(imageNode.defaultVideo);
            assets.AddGif(imageNode.defaultGif);
            CollectHeroBuildCutsceneOverrides(imageNode.heroBuildCutsceneOverrides, assets);
        }

        if (node is CutsceneNode cutsceneNode)
        {
            assets.Add(cutsceneNode.defaultImage);
            assets.Add(cutsceneNode.defaultVideo);
            assets.AddGif(cutsceneNode.defaultGif);
            CollectHeroBuildCutsceneOverrides(cutsceneNode.heroBuildCutsceneOverrides, assets);
        }

        if (node is DialogueNode dialogueNode)
            CollectDialogueNode(dialogueNode, assets);

        if (node is PhoneDialogueNode phoneNode)
            CollectPhoneDialogueNode(phoneNode, assets);

        if (node is AppearanceChoiceNode appearanceNode && appearanceNode.options != null)
        {
            for (int i = 0; i < appearanceNode.options.Count; i++)
                assets.Add(appearanceNode.options[i]?.previewSprite);
        }

        if (node is WardrobeChoiceNode wardrobeNode)
        {
            CollectCharacter(wardrobeNode.character, assets);
            if (wardrobeNode.availableClothes != null)
            {
                for (int i = 0; i < wardrobeNode.availableClothes.Count; i++)
                    CollectClothing(wardrobeNode.availableClothes[i], assets);
            }
        }

        if (node is AddClothingNode addClothingNode)
            CollectClothing(addClothingNode.clothing, assets);
    }

    private static void CollectDialogueNode(DialogueNode node, StoryStartPreloadAssetSet assets)
    {
        if (node == null)
            return;

        if (node.activeCharacters != null)
        {
            for (int i = 0; i < node.activeCharacters.Count; i++)
                CollectCharacter(node.activeCharacters[i]?.character, assets);
        }

        if (node.lines == null)
            return;

        for (int i = 0; i < node.lines.Count; i++)
        {
            DialogueLine line = node.lines[i];
            if (line == null)
                continue;

            CollectCharacter(line.speaker, assets);
            CollectDialogueStyle(line.style, assets);
        }
    }

    private static void CollectPhoneDialogueNode(PhoneDialogueNode node, StoryStartPreloadAssetSet assets)
    {
        if (node == null)
            return;

        assets.Add(node.contactAvatar);

        if (node.messages == null)
            return;

        for (int i = 0; i < node.messages.Count; i++)
            assets.Add(node.messages[i]?.attachment);
    }

    private static void CollectSceneSetup(SceneSetupData sceneData, StoryStartPreloadAssetSet assets)
    {
        if (sceneData == null)
            return;

        assets.Add(sceneData.background);
        assets.Add(sceneData.backgroundOverlay);
        assets.Add(sceneData.backgroundVideo);
        assets.AddGif(sceneData.backgroundGif);
        assets.Add(sceneData.music);
        assets.Add(sceneData.startSfx);
    }

    private static void CollectHeroBuildCutsceneOverrides(
        IReadOnlyList<HeroBuildCutsceneOverride> overrides,
        StoryStartPreloadAssetSet assets)
    {
        if (overrides == null || assets == null)
            return;

        for (int i = 0; i < overrides.Count; i++)
        {
            HeroBuildCutsceneOverride rule = overrides[i];
            if (rule == null || !rule.Enabled)
                continue;

            assets.Add(rule.DefaultImage);
            assets.Add(rule.DefaultVideo);
            assets.AddGif(rule.DefaultGif);
        }
    }

    private static void CollectJsonAssetLibrary(StoryJsonAssetLibrary library, StoryStartPreloadAssetSet assets)
    {
        if (library == null)
            return;

        if (library.TryGetStoryUiStyle(out StoryUiStyle style, out Sprite background))
        {
            assets.Add(background);
            CollectStoryUiStyle(style, assets);
        }

        if (library.TryGetCutsceneStoryUiStyle(out StoryUiStyle cutsceneStyle, out Sprite cutsceneBackground))
        {
            assets.Add(cutsceneBackground);
            CollectStoryUiStyle(cutsceneStyle, assets);
        }

        IReadOnlyList<StoryJsonAssetReference> refs = library.Assets;
        if (refs == null)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            StoryJsonAssetReference reference = refs[i];
            if (reference == null)
                continue;

            CollectCharacter(reference.Character, assets);
            CollectClothing(reference.Clothing, assets);
            assets.Add(reference.Sprite);
            assets.Add(reference.Video);
            assets.Add(reference.TextAsset);
            assets.Add(reference.Audio);
            CollectDialogueStyle(reference.DialogueStyle, assets);
        }
    }

    private static void CollectWardrobeAssets(GameWardrobeSetupSettings wardrobe, StoryStartPreloadAssetSet assets)
    {
        if (wardrobe == null)
            return;

        CollectCharacter(wardrobe.TargetCharacter, assets);
        CollectClothing(wardrobe.DefaultOutfitItem, assets);
        CollectClothing(wardrobe.DefaultHairItem, assets);
        CollectClothing(wardrobe.DefaultAccessoryItem, assets);

        IReadOnlyList<WardrobeHeroAppearanceOption> appearances = wardrobe.AppearanceOptions;
        if (appearances != null)
        {
            for (int i = 0; i < appearances.Count; i++)
                assets.Add(appearances[i]?.previewSprite);
        }

        CollectClothingList(wardrobe.OutfitItems, assets);
        CollectClothingList(wardrobe.HairItems, assets);
        CollectClothingList(wardrobe.AccessoryItems, assets);
    }

    private static void CollectClothingList(IReadOnlyList<ClothingItem> items, StoryStartPreloadAssetSet assets)
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
            CollectClothing(items[i], assets);
    }

    private static void CollectCharacter(CharacterData character, StoryStartPreloadAssetSet assets)
    {
        if (character == null || assets == null)
            return;

        assets.Add(character.defaultSprite);
        assets.Add(character.hairSprite);
        assets.Add(character.bodySprite);
        CollectClothing(character.permanentOutfit, assets);
        CollectClothing(character.permanentHair, assets);
        CollectClothing(character.permanentAccessory, assets);

        if (character.emotions != null)
        {
            for (int i = 0; i < character.emotions.Count; i++)
                assets.Add(character.emotions[i]?.sprite);
        }

        if (character.emotionLayers != null)
        {
            for (int i = 0; i < character.emotionLayers.Count; i++)
                assets.Add(character.emotionLayers[i]?.faceSprite);
        }

        if (character.appearanceVariants == null)
            return;

        for (int i = 0; i < character.appearanceVariants.Count; i++)
        {
            AppearanceVariant variant = character.appearanceVariants[i];
            if (variant == null)
                continue;

            assets.Add(variant.defaultSprite);
            if (variant.emotions == null)
                continue;

            for (int j = 0; j < variant.emotions.Count; j++)
                assets.Add(variant.emotions[j]?.sprite);
        }
    }

    private static void CollectClothing(ClothingItem item, StoryStartPreloadAssetSet assets)
    {
        if (item == null || assets == null)
            return;

        assets.Add(item.sprite);
    }

    private static void CollectDialogueStyle(DialogueStyle style, StoryStartPreloadAssetSet assets)
    {
        if (style == null || assets == null)
            return;

        assets.Add(style.backgroundSprite);
    }

    private static void CollectStoryUiStyle(StoryUiStyle style, StoryStartPreloadAssetSet assets)
    {
        if (style == null || assets == null)
            return;

        style.CollectPreloadAssets(assets);
    }
}

public static class StoryStartPreloadAssetCollectors
{
    private static readonly IStoryStartPreloadAssetCollector SharedCollector =
        new StoryStartPreloadAssetCollector(
            StoryLoadingMediaPolicies.Shared,
            StoryStartChapterSelectors.Shared);

    public static IStoryStartPreloadAssetCollector Shared => SharedCollector;
}
