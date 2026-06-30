using NUnit.Framework;
using UnityEngine;

public class DialogueIdentityResolverTests
{
    const string StoryId = "dialogue_identity_story";
    const string ChapterId = "dialogue_identity_chapter";
    const string Alice = "\u0410\u043b\u0438\u0441\u0430";
    const string Alison = "\u042d\u043b\u0438\u0441\u043e\u043d";
    const string Darina = "\u0414\u0430\u0440\u0438\u043d\u0430";

    [SetUp]
    public void SetUp()
    {
        HeroCustomizationStore.DeleteStoredState();
        HeroCustomizationStore.DeletePlayerNameForStory(StoryId);
        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: false);
        StoryManager.Instance = null;
    }

    [TearDown]
    public void TearDown()
    {
        HeroCustomizationStore.DeleteStoredState();
        HeroCustomizationStore.DeletePlayerNameForStory(StoryId);
        PlayerAppearance.ApplyState(new HeroCustomizationState(), save: false, notify: false);
        StoryManager.Instance = null;
    }

    [Test]
    public void PlayerSpeaker_UsesProfileName_InsteadOfCharacterDataLiteral()
    {
        CharacterProfileService.SaveSelectedPlayerName(Darina, StoryId, nameof(DialogueIdentityResolverTests));
        CharacterData hero = ScriptableObject.CreateInstance<CharacterData>();
        hero.name = "JsonCharacter_hero";
        hero.characterName = Alice;

        try
        {
            var line = new DialogueLine
            {
                speakerId = "hero",
                speakerNameHint = "hero",
                speaker = hero,
                richText = "{speakerName} \u043c\u043e\u043b\u0447\u0438\u0442."
            };

            DialogueIdentityResult identity = DialogueIdentity.ResolveSpeaker(new DialogueIdentityRequest
            {
                StoryId = StoryId,
                ChapterId = ChapterId,
                NodeId = "node_1",
                LineIndex = 0,
                Line = line,
                BodyText = line.richText
            });

            string body = DialogueVariableResolver.ResolveText(
                line.richText,
                DialogueVariableContext.StoryUi(nameof(DialogueIdentityResolverTests), null, StoryId, ChapterId, identity));

            Assert.That(identity.DisplayName, Is.EqualTo(Darina));
            Assert.That(identity.Source, Is.EqualTo(DialogueIdentitySource.Profile));
            Assert.That(identity.IsDynamicPlayerName, Is.True);
            Assert.That(body, Is.EqualTo(Darina + " \u043c\u043e\u043b\u0447\u0438\u0442."));
        }
        finally
        {
            Object.DestroyImmediate(hero);
        }
    }

    [Test]
    public void SpeakerNamePlaceholder_UsesSameIdentityAsNameplate()
    {
        CharacterData alice = ScriptableObject.CreateInstance<CharacterData>();
        alice.name = "alice";
        alice.characterName = Alice;

        try
        {
            var line = new DialogueLine
            {
                speakerId = "alice",
                speakerNameHint = Alice,
                speaker = alice,
                richText = "{speakerName}: \u043f\u0440\u0438\u0432\u0435\u0442."
            };

            DialogueIdentityResult identity = DialogueIdentity.ResolveSpeaker(new DialogueIdentityRequest
            {
                StoryId = StoryId,
                ChapterId = ChapterId,
                NodeId = "node_2",
                LineIndex = 0,
                Line = line,
                BodyText = line.richText
            });

            string body = DialogueVariableResolver.ResolveText(
                line.richText,
                DialogueVariableContext.StoryUi(nameof(DialogueIdentityResolverTests), null, StoryId, ChapterId, identity));

            Assert.That(identity.DisplayName, Is.EqualTo(Alice));
            Assert.That(body, Is.EqualTo(Alice + ": \u043f\u0440\u0438\u0432\u0435\u0442."));
        }
        finally
        {
            Object.DestroyImmediate(alice);
        }
    }

    [Test]
    public void Validator_FlagsAlisonLiteral_WhenSpeakerMapsToAlice()
    {
        var document = new StoryJsonDocument
        {
            storyId = StoryId,
            chapterId = ChapterId
        };
        document.characters.Add(new StoryJsonCharacter { id = "alice", name = Alice });
        document.nodes.Add(new StoryJsonNode
        {
            id = "dialogue_1",
            type = "dialogue",
            lines =
            {
                new StoryJsonLine
                {
                    speaker = "alice",
                    text = Alison + " \u0443\u043b\u044b\u0431\u043d\u0443\u043b\u0430\u0441\u044c."
                }
            }
        });

        DialogueIdentityValidationReport report = DialogueIdentityValidator.ValidateJsonDocument(document);

        Assert.That(report.WarningCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(
            HasIssueContaining(report, Alison),
            Is.True,
            "Expected validator warning for Alison literal while speaker maps to Alice.");
    }

    [Test]
    public void Validator_FlagsPlayerActorLiteralName()
    {
        var document = new StoryJsonDocument
        {
            storyId = StoryId,
            chapterId = ChapterId
        };
        document.characters.Add(new StoryJsonCharacter { id = "hero", name = Alice });
        document.nodes.Add(new StoryJsonNode
        {
            id = "dialogue_hero",
            type = "dialogue",
            lines =
            {
                new StoryJsonLine
                {
                    speaker = "hero",
                    text = "\u041e\u043d\u0430 \u043c\u043e\u043b\u0447\u0438\u0442."
                }
            }
        });

        DialogueIdentityValidationReport report = DialogueIdentityValidator.ValidateJsonDocument(document);

        Assert.That(report.WarningCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(
            HasIssueContaining(report, "{playerName}"),
            Is.True,
            "Expected validator warning that hero actor name should use playerName placeholder.");
    }

    static bool HasIssueContaining(DialogueIdentityValidationReport report, string text)
    {
        foreach (DialogueIdentityValidationIssue issue in report.Issues)
        {
            if (issue != null && issue.ToString().Contains(text))
                return true;
        }

        return false;
    }
}
