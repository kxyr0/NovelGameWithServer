using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StoryJsonDocument
{
    public int version = 1;
    public string storyId;
    public string chapterId;
    public string episodeId;
    public string title;
    public string defaultName;
    public string defaultPlayerName;
    public PlayerNameCaseForms defaultNameCases;
    public PlayerNameCaseForms defaultPlayerNameCases;
    public List<StoryJsonCharacter> characters = new List<StoryJsonCharacter>();
    public List<StoryJsonNode> nodes = new List<StoryJsonNode>();
}

[Serializable]
public sealed class StoryJsonCharacter
{
    public string id;
    public string name;
    public string asset;
    public string guid;
}

[Serializable]
public sealed class StoryJsonNode
{
    public string id;
    public string guid;
    public string type;
    public string next;
    public Vector2 position;

    public string title;
    public string label;
    public string background;
    public string backgroundVideo;
    public string backgroundGif;
    public string backgroundOverlay;
    public string music;
    public bool stopMusic;
    public string startSfx;
    public bool stopSfx;
    public string suggestedBackground;
    public string suggestedMusic;

    public List<StoryJsonActiveCharacter> activeCharacters = new List<StoryJsonActiveCharacter>();
    public List<StoryJsonLine> lines = new List<StoryJsonLine>();
    public string choicePrompt;
    public List<StoryJsonChoice> choices = new List<StoryJsonChoice>();

    public string statId;
    public int statDelta;
    public string statDisplayName;
    public string systemMessage;
    public string defaultName;
    public bool forceShow;

    public string variableKey;
    public int deltaValue;
    public bool add;
    public int requiredValue;
    public string comparison;
    public string compareVariableKey;
    public string leftVariableKey;
    public string rightVariableKey;
    public string trueNext;
    public string falseNext;

    public int cost;
    public string successNext;
    public string failNext;

    public string mode;
    public string targetPosition;
    public float xOffset;
    public float duration;

    public string image;
    public string video;
    public string gif;
    public string caption;
    public string description;
    public bool zoomable;
    public List<StoryJsonHeroBuildCutsceneOverride> heroBuildCutsceneOverrides = new List<StoryJsonHeroBuildCutsceneOverride>();
    public float textDelay;
    public bool showCharacters;

    public string contactName;
    public string headerContactMode;
    public string contactAvatar;
    public float typingDelay;
    public List<StoryJsonPhoneMessage> messages = new List<StoryJsonPhoneMessage>();

    public string effect;
    public float intensity;

    public string promptText;
    public bool singleExit;
    public List<StoryJsonAppearanceOption> appearanceOptions = new List<StoryJsonAppearanceOption>();

    public string characterId;
    public List<string> clothes = new List<string>();
    public List<int> premiumCosts = new List<int>();
    public List<int> clothingCosts = new List<int>();
    public List<int> clothesCosts = new List<int>();
    public List<StoryJsonWardrobeOptionRule> optionRules = new List<StoryJsonWardrobeOptionRule>();
    public List<string> exits = new List<string>();
    public string clothing;
    public string itemId;
    public string hasItemNext;
    public string noItemNext;
}

[Serializable]
public sealed class StoryJsonHeroBuildCutsceneOverride
{
    public bool enabled = true;
    public string ruleName;
    public bool matchAppearance;
    public string appearance;
    public string outfitId;
    public string hairId;
    public List<string> hairIds = new List<string>();
    public string accessoryId;
    public string image;
    public string video;
    public string gif;
}

[Serializable]
public sealed class StoryJsonWardrobeOptionRule
{
    public string label;
    public string clearSlot;
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
    public string requiredItemId;
    public bool hideInRestrictedRegions;
    public List<string> hiddenRegionCodes = new List<string>();
    public string purchaseKey;
    public string unavailableMessage;
}

[Serializable]
public sealed class StoryJsonActiveCharacter
{
    public string character;
    public string emotion;
    public string position;
}

[Serializable]
public sealed class StoryJsonLine
{
    public string speaker;
    public string emotion;
    public string text;
    public string style;
    public string authorComment;
}

[Serializable]
public sealed class StoryJsonChoice
{
    public string text;
    public string next;
    public bool isPremium;
    public int premiumCost;
    public string requiredVariable;
    public int requiredValue;
    public bool hideWhenRequirementNotMet;
    public bool hideInRestrictedRegions;
    public List<string> hiddenRegionCodes = new List<string>();
}

[Serializable]
public sealed class StoryJsonPhoneMessage
{
    public string senderName;
    public string speaker;
    public string text;
    public string timeText;
    public string time;
    public string side;
    public string attachment;
    public bool usePhotoLayout;
    public bool photoLayout;
}

[Serializable]
public sealed class StoryJsonAppearanceOption
{
    public string label;
    public string type;
    public string previewSprite;
    public string next;
}

public static class StoryJsonTypes
{
    public const string Start = "start";
    public const string Scene = "scene";
    public const string Dialogue = "dialogue";
    public const string Cutscene = "cutscene";
    public const string Choice = "choice";
    public const string StatChange = "statChange";
    public const string VariableChange = "variableChange";
    public const string Condition = "condition";
    public const string Premium = "premium";
    public const string Camera = "camera";
    public const string Image = "image";
    public const string PhoneDialogue = "phoneDialogue";
    public const string Effect = "effect";
    public const string Banner = "banner";
    public const string NameChoice = "nameChoice";
    public const string AppearanceChoice = "appearanceChoice";
    public const string WardrobeChoice = "wardrobeChoice";
    public const string AddClothing = "addClothing";
    public const string OpenWardrobe = "openWardrobe";
    public const string WardrobeCheck = "wardrobeCheck";
}
