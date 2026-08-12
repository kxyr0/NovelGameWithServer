using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using XNode;

public partial class StoryManager
{
	DialogueIdentityResult currentLineIdentity;

	void ProcessDialogue(DialogueNode node)
	{
		if (node == null || !EnsureDialogueUI("showing dialogue"))
			return;

		if (ShouldSkipChapterTitleDialogue(node))
		{
			dialogueUI.ClearChoices();
			dialogueUI.ClearDialogue();
			activeDialogueNode = null;
			ResetDialogueLinePages();
			GoNext(node, "exit");
			return;
		}

		GameState.Instance.currentNode = node;
		dialogueUI.ClearChoices();

		activeDialogueNode = node;
		ResetDialogueLinePages();
		EnsureHeroCustomizationReadyForDisplay();
		RepairRuntimeDialogueCharacterBindings(node);
		EnsureRuntimeActiveCharacters(node);

		currentLineIndex = FindNextDisplayableDialogueLineIndex(node.lines, 0);
		DialogueLine firstLine = currentLineIndex >= 0 ? node.lines[currentLineIndex] : null;
		bool firstLineIsNarration = IsNarrationLine(firstLine);
		bool leftUsed = false;
		bool centerUsed = false;
		bool rightUsed = false;
		bool hasExplicitCharacters = false;

		if (!firstLineIsNarration && node.activeCharacters != null && characterView != null)
		{
			foreach (var entry in node.activeCharacters)
			{
				if (entry == null)
					continue;

				if (entry.character != null)
					hasExplicitCharacters = true;

				if (!IsRenderableStorySpeaker(entry.character))
					continue;

				characterView.SetupCharacter(entry.character, entry.emotion, entry.position);

				MarkPositionUsed(entry.position, ref leftUsed, ref centerUsed, ref rightUsed);
			}
		}

		if (firstLine != null)
		{
			if (firstLineIsNarration)
			{
				HandleNarrationLine(firstLine);
			}
			else if (TryShowDialogueSpeaker(firstLine, !hasExplicitCharacters, out var speakerPosition))
			{
				MarkPositionUsed(speakerPosition, ref leftUsed, ref centerUsed, ref rightUsed);
			}

			ShowDialogueLinePage(firstLine);
			RecordDialogueHistory(firstLine);
			TryAutoPan(firstLine);
		}
		else
		{
			activeDialogueNode = null;
			ResetDialogueLinePages();
			GoNext(node, "exit");
			return;
		}

		if (!firstLineIsNarration)
			characterView?.DisableUnused(leftUsed, centerUsed, rightUsed);

		PersistProgress(node);
	}

	bool ShouldSkipChapterTitleDialogue(DialogueNode node)
	{
		if (node == null || node.lines == null || node.lines.Count != 1)
			return false;

		DialogueLine line = node.lines[0];
		if (line == null || line.speaker != null || !string.IsNullOrWhiteSpace(line.speakerId))
			return false;

		string lineTitle = CompactChapterTitle(line.richText);
		if (string.IsNullOrEmpty(lineTitle))
			return false;

		string chapterTitle = CompactChapterTitle(GetChapterDisplayName(GetCurrentChapterOrNull()));
		if (!string.IsNullOrEmpty(chapterTitle) && lineTitle == chapterTitle)
			return true;

		string nodeTitle = CompactChapterTitle(node.nodeTitle);
		if (!string.IsNullOrEmpty(nodeTitle) && lineTitle == nodeTitle && LooksLikeChapterTitleText(node.nodeTitle))
			return true;

		return LooksLikeChapterTitleText(line.richText) &&
			   LooksLikeChapterTitleText(node.nodeTitle) &&
			   lineTitle == nodeTitle;
	}

	static bool LooksLikeChapterTitleText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		string prepared = PlayerAppearance.ReplacePlaceholders(value).Trim().ToUpperInvariant();
		return prepared.StartsWith("ГЛАВА", StringComparison.Ordinal) ||
			   prepared.StartsWith("CHAPTER", StringComparison.Ordinal);
	}

	static string CompactChapterTitle(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		value = PlayerAppearance.ReplacePlaceholders(value);
		var builder = new System.Text.StringBuilder(value.Length);

		foreach (char character in value)
		{
			if (char.IsLetterOrDigit(character))
				builder.Append(char.ToUpperInvariant(character));
		}

		return builder.ToString();
	}

	void ShowDialogueLinePage(DialogueLine line)
	{
		PrepareDialogueLinePages(line);
		ShowCurrentDialogueLinePage(line);
	}

	void PrepareDialogueLinePages(DialogueLine line)
	{
		currentLinePages.Clear();
		currentLinePageIndex = 0;

		currentLineIdentity = ResolveDialogueIdentity(line, line != null ? line.richText ?? "" : "");
		string resolvedText = ReplaceStoryPlaceholders(line != null ? line.richText ?? "" : "", line, currentLineIdentity);
		if (!splitLongDialogueLines || maxDialogueCharsPerTap <= 0 ||
			CountVisibleDialogueChars(resolvedText) <= maxDialogueCharsPerTap)
		{
			currentLinePages.Add(resolvedText);
			return;
		}

		currentLinePages.AddRange(BuildDialogueSentencePages(resolvedText, maxDialogueCharsPerTap));

		if (currentLinePages.Count == 0)
			currentLinePages.Add("");
	}

	bool TryShowNextDialogueLinePage()
	{
		if (activeDialogueNode == null ||
			activeDialogueNode.lines == null ||
			currentLineIndex < 0 ||
			currentLineIndex >= activeDialogueNode.lines.Count ||
			currentLinePages.Count <= 1 ||
			currentLinePageIndex >= currentLinePages.Count - 1)
		{
			return false;
		}

		currentLinePageIndex++;
		if (!EnsureDialogueInterfaceForActiveNode("advancing long dialogue line"))
			return true;

		ShowCurrentDialogueLinePage(activeDialogueNode.lines[currentLineIndex]);
		return true;
	}

	void ShowCurrentDialogueLinePage(
		DialogueLine line,
		bool animate = true)
	{
		DialogueUIManager targetUi =
			GetDialogueInterfaceForNode(activeDialogueNode);

		if (targetUi == null)
			return;

		if (currentLinePages.Count == 0)
			currentLinePages.Add("");

		int pageIndex =
			Mathf.Clamp(
				currentLinePageIndex,
				0,
				currentLinePages.Count - 1);

		targetUi.ShowLineText(
			line,
			currentLinePages[pageIndex],
			currentLineIdentity,
			animate);
	}

	void ResetDialogueLinePages()
	{
		currentLinePages.Clear();
		currentLinePageIndex = 0;
		currentLineIdentity = null;
	}

	string ReplaceStoryPlaceholders(string value)
	{
		return ReplaceStoryPlaceholders(value, null, null);
	}

	string ReplaceStoryPlaceholders(string value, DialogueLine line, DialogueIdentityResult identity)
	{
		EnsurePlayerNameResolvedForActiveStory();
		DialogueIdentityResult speakerIdentity = identity ?? ResolveDialogueIdentity(line, value);
		return DialogueVariableResolver.ResolveText(
			value,
			DialogueVariableContext.StoryUi(
				nameof(StoryManager),
				gameObject,
				CurrentStoryId,
				CurrentChapterId,
				speakerIdentity));
	}

	DialogueIdentityResult ResolveDialogueIdentity(DialogueLine line, string bodyText = "")
	{
		if (line == null || (line.speaker == null && string.IsNullOrWhiteSpace(line.speakerId)))
			return null;

		return DialogueIdentity.ResolveSpeaker(new DialogueIdentityRequest
		{
			StoryId = CurrentStoryId,
			ChapterId = CurrentChapterId,
			NodeId = activeDialogueNode != null ? activeDialogueNode.guid : "",
			LineIndex = currentLineIndex,
			PageIndex = currentLinePageIndex,
			Line = line,
			BodyText = bodyText ?? "",
			SourceObject = gameObject
		});
	}

	void EnsurePlayerNameResolvedForActiveStory()
	{
		string resolvedName = ResolveRestoredPlayerName(null, storyGraph, PlayerAppearance.PlayerName);
		if (string.IsNullOrWhiteSpace(resolvedName) ||
			string.Equals(resolvedName, PlayerAppearance.PlayerName, StringComparison.Ordinal))
		{
			return;
		}

		HeroCustomizationState state = PlayerAppearance.CaptureState();
		state.playerName = resolvedName;
		PlayerAppearance.ApplyState(state, save: false, notify: false);
	}

	int GetCurrentDialoguePageVisibleCharCount()
	{
		if (currentLinePages.Count > 0)
		{
			int pageIndex = Mathf.Clamp(currentLinePageIndex, 0, currentLinePages.Count - 1);
			return CountVisibleDialogueChars(currentLinePages[pageIndex]);
		}

		DialogueUIManager targetUi = GetDialogueInterfaceForNode(activeDialogueNode);
		return targetUi != null && targetUi.dialogueText != null
			? CountVisibleDialogueChars(targetUi.dialogueText.text)
			: 0;
	}

	int GetCurrentDialogueLineVisibleCharCount()
	{
		DialogueLine line = GetCurrentDialogueLine();
		if (line != null)
			return CountVisibleDialogueChars(ReplaceStoryPlaceholders(line.richText ?? ""));

		DialogueUIManager targetUi = GetDialogueInterfaceForNode(activeDialogueNode);
		return targetUi != null && targetUi.dialogueText != null
			? CountVisibleDialogueChars(targetUi.dialogueText.text)
			: 0;
	}

	DialogueLine GetCurrentDialogueLine()
	{
		if (activeDialogueNode == null ||
			activeDialogueNode.lines == null ||
			currentLineIndex < 0 ||
			currentLineIndex >= activeDialogueNode.lines.Count)
		{
			return null;
		}

		return activeDialogueNode.lines[currentLineIndex];
	}

	public static int CountVisibleDialogueChars(string value)
	{
		if (string.IsNullOrEmpty(value))
			return 0;

		int count = 0;
		bool insideRichTextTag = false;
		foreach (char character in value)
		{
			if (character == '<')
			{
				insideRichTextTag = true;
				continue;
			}

			if (insideRichTextTag)
			{
				if (character == '>')
					insideRichTextTag = false;

				continue;
			}

			count++;
		}

		return count;
	}

	static List<string> BuildDialogueSentencePages(string value, int maxVisibleChars)
	{
		var pages = new List<string>();
		if (string.IsNullOrEmpty(value))
			return pages;

		int sentenceStart = SkipLeadingDialoguePageWhitespace(value, 0);
		int pageStart = -1;
		int pageEnd = -1;

		while (sentenceStart < value.Length)
		{
			int sentenceEnd = FindDialogueSentenceEnd(value, sentenceStart);
			if (sentenceEnd <= sentenceStart)
				sentenceEnd = value.Length;

			if (pageStart < 0)
			{
				pageStart = sentenceStart;
				pageEnd = sentenceEnd;
			}
			else
			{
				string candidate = value.Substring(pageStart, sentenceEnd - pageStart).TrimEnd();
				if (CountVisibleDialogueChars(candidate) <= maxVisibleChars)
				{
					pageEnd = sentenceEnd;
				}
				else
				{
					AddDialoguePage(pages, value, pageStart, pageEnd);
					pageStart = sentenceStart;
					pageEnd = sentenceEnd;
				}
			}

			int nextSentenceStart = SkipLeadingDialoguePageWhitespace(value, sentenceEnd);
			if (nextSentenceStart <= sentenceStart)
				break;

			sentenceStart = nextSentenceStart;
		}

		AddDialoguePage(pages, value, pageStart, pageEnd);
		return pages;
	}

	static void AddDialoguePage(List<string> pages, string value, int start, int end)
	{
		if (pages == null || string.IsNullOrEmpty(value) || start < 0 || end <= start)
			return;

		string page = value.Substring(start, end - start).TrimEnd();
		if (!string.IsNullOrEmpty(page))
			pages.Add(page);
	}

	static int FindDialogueSentenceEnd(string value, int start)
	{
		bool insideRichTextTag = false;

		for (int i = start; i < value.Length; i++)
		{
			char character = value[i];
			if (character == '<')
			{
				insideRichTextTag = true;
				continue;
			}

			if (insideRichTextTag)
			{
				if (character == '>')
					insideRichTextTag = false;

				continue;
			}

			if (!IsDialogueSentenceTerminator(character))
				continue;

			int end = i + 1;
			while (end < value.Length)
			{
				char trailing = value[end];
				if (IsDialogueSentenceTerminator(trailing) ||
					IsDialogueSentenceTrailingCharacter(trailing))
				{
					end++;
					continue;
				}

				if (IsClosingRichTextTagStart(value, end))
				{
					int tagEnd = value.IndexOf('>', end);
					if (tagEnd < 0)
						return end;

					end = tagEnd + 1;
					continue;
				}

				break;
			}

			return end;
		}

		return value.Length;
	}

	static int SkipLeadingDialoguePageWhitespace(string value, int start)
	{
		if (string.IsNullOrEmpty(value))
			return 0;

		int index = Mathf.Clamp(start, 0, value.Length);
		while (index < value.Length && char.IsWhiteSpace(value[index]))
			index++;

		return index;
	}

	static bool IsDialogueSentenceTerminator(char character)
	{
		return character == '.' ||
			   character == '!' ||
			   character == '?' ||
			   character == '\u2026';
	}

	static bool IsDialogueSentenceTrailingCharacter(char character)
	{
		return character == ')' ||
			   character == ']' ||
			   character == '}' ||
			   character == '\u00bb' ||
			   character == '\u201d' ||
			   character == '\u2019' ||
			   character == '"' ||
			   character == '\'';
	}

	static bool IsClosingRichTextTagStart(string value, int index)
	{
		return !string.IsNullOrEmpty(value) &&
			   index >= 0 &&
			   index + 1 < value.Length &&
			   value[index] == '<' &&
			   value[index + 1] == '/';
	}

	public void OnDialogueClick()
	{
		if (activeDialogueNode == null && activeCutsceneImageNode == null)
			return;

		if (activeCutsceneImageNode != null && activeCutsceneImageEnterFrame == Time.frameCount)
			return;

		if (TryRevealCutsceneTextFromInput())
			return;

		if (activeCutsceneImageNode != null)
		{
			FinishCutsceneImage();
			return;
		}

		if (activeDialogueNode.lines == null || activeDialogueNode.lines.Count == 0)
		{
			var emptyNode = activeDialogueNode;
			bool exitOpensScene = emptyNode is CutsceneNode && DoesCutsceneExitOpenScene(emptyNode);
			activeDialogueNode = null;
			ResetDialogueLinePages();
			if (emptyNode is CutsceneNode)
				PrepareCutsceneBackgroundForExit(emptyNode);
			else
				ClearCutsceneRuntimeState();
			GoNext(emptyNode, "exit");
			if (exitOpensScene)
				CompleteCutsceneBackgroundExit();
			return;
		}

		if (TryShowNextDialogueLinePage())
			return;

		currentLineIndex = FindNextDisplayableDialogueLineIndex(activeDialogueNode.lines, currentLineIndex + 1);

		if (currentLineIndex < 0 || currentLineIndex >= activeDialogueNode.lines.Count)
		{
			var node = activeDialogueNode;
			bool exitOpensScene = node is CutsceneNode && DoesCutsceneExitOpenScene(node);
			activeDialogueNode = null;
			ResetDialogueLinePages();
			if (node is CutsceneNode)
				PrepareCutsceneBackgroundForExit(node);
			else
				ClearCutsceneRuntimeState();

			GoNext(node, "exit");
			if (exitOpensScene)
				CompleteCutsceneBackgroundExit();
			return;
		}

		var currentLine = activeDialogueNode.lines[currentLineIndex];

		if (activeDialogueNode is CutsceneNode)
		{
			if (!EnsureCutsceneUserInterface("advancing cutscene dialogue"))
				return;

			ShowDialogueLinePage(currentLine);
			TryPanCutsceneBackground(currentLine);
			RecordDialogueHistory(currentLine);
			PersistProgress(activeDialogueNode);
			return;
		}

		if (!EnsureDialogueUI("advancing dialogue"))
			return;

		if (IsNarrationLine(currentLine))
			HandleNarrationLine(currentLine);
		else
			TryShowDialogueSpeaker(currentLine, !HasExplicitCharacters(activeDialogueNode), out _);

		ShowDialogueLinePage(currentLine);
		RecordDialogueHistory(currentLine);
		TryAutoPan(currentLine);
		PersistProgress(activeDialogueNode);
	}

	static int FindNextDisplayableDialogueLineIndex(List<DialogueLine> lines, int startIndex)
	{
		if (lines == null || lines.Count == 0)
			return -1;

		int index = Mathf.Max(0, startIndex);
		while (index < lines.Count)
		{
			if (!IsSystemInstructionDialogueLine(lines[index]))
				return index;

			index++;
		}

		return -1;
	}

	static bool IsSystemInstructionDialogueLine(DialogueLine line)
	{
		if (line == null || line.speaker != null || !string.IsNullOrWhiteSpace(line.speakerId))
			return false;

		string text = StripRichTextTags(PlayerAppearance.ReplacePlaceholders(line.richText ?? "")).Trim();
		return StoryJsonConverter.IsSystemInstructionText(text);
	}

	static string StripRichTextTags(string value)
	{
		if (string.IsNullOrEmpty(value))
			return "";

		var builder = new System.Text.StringBuilder(value.Length);
		bool insideTag = false;
		for (int i = 0; i < value.Length; i++)
		{
			char character = value[i];
			if (character == '<')
			{
				insideTag = true;
				continue;
			}

			if (insideTag)
			{
				if (character == '>')
					insideTag = false;

				continue;
			}

			builder.Append(character);
		}

		return builder.ToString();
	}

	bool IsNarrationLine(DialogueLine line)
	{
		return line != null && line.speaker == null && string.IsNullOrWhiteSpace(line.speakerId);
	}

	void HandleNarrationLine(DialogueLine line)
	{
		if (!IsNarrationLine(line))
			return;

		CharacterPosition heroPosition = heroCharacterPosition;
		TryGetHeroActivePosition(activeDialogueNode, out heroPosition);

		if (panToHeroOnNarrationLines && autoPanToSpeaker)
		{
			var cam = cameraController ?? CameraController.Instance;
			cam?.PanToSpeaker(heroPosition);
		}

		switch (narrationHeroHideMode)
		{
			case NarrationHeroHideMode.Instant:
				characterView?.HideAll(0f);
				break;
			case NarrationHeroHideMode.Fade:
				characterView?.HideAll(narrationHeroFadeDuration);
				break;
		}
	}

	bool TryShowDialogueSpeaker(DialogueLine line, bool autoCenterWhenMissing, out CharacterPosition position)
	{
		position = CharacterPosition.Center;

		if (line == null || line.speaker == null || characterView == null || !IsRenderableStorySpeaker(line.speaker))
			return false;

		if (TryGetActiveSpeakerPosition(activeDialogueNode, line.speaker, out position))
		{
			characterView.SetupCharacter(line.speaker, line.emotion, position);
			HideInactiveSpeakers(position);
			return true;
		}

		if (!autoCenterWhenMissing)
			return false;

		position = autoBuildActiveCharacters
			? GetDefaultSpeakerPosition(line.speaker)
			: CharacterPosition.Center;
		characterView.SetupCharacter(line.speaker, line.emotion, position);
		HideInactiveSpeakers(position);
		return true;
	}

	void HideInactiveSpeakers(CharacterPosition speakerPosition)
	{
		if (characterView == null)
			return;

		switch (inactiveSpeakerHideMode)
		{
			case InactiveSpeakerHideMode.Instant:
				characterView.HideAllExcept(speakerPosition, 0f);
				break;

			case InactiveSpeakerHideMode.Fade:
				characterView.HideAllExcept(speakerPosition, inactiveSpeakerFadeDuration);
				break;
		}
	}


	void RepairRuntimeDialogueCharacterBindings(DialogueNode node)
	{
		if (node == null)
			return;

		bool hasBrokenBinding = false;
		if (node.lines != null)
		{
			foreach (DialogueLine line in node.lines)
			{
				if (line != null &&
					!string.IsNullOrWhiteSpace(line.speakerId) &&
					!IsRenderableStorySpeaker(line.speaker))
				{
					hasBrokenBinding = true;
					break;
				}
			}
		}

		if (!hasBrokenBinding && node.activeCharacters != null)
		{
			foreach (DialogueCharacterEntry entry in node.activeCharacters)
			{
				if (entry != null &&
					!string.IsNullOrWhiteSpace(entry.speakerNameHint) &&
					!IsRenderableStorySpeaker(entry.character))
				{
					hasBrokenBinding = true;
					break;
				}
			}
		}

		if (!hasBrokenBinding)
			return;

		ChapterData chapter = GetCurrentChapterOrNull();
		StoryRuntimeAssetRegistryResolver.SetActiveStory(CurrentStoryId);
		var resolver = new StoryChapterJsonAssetResolver(
			chapter != null ? chapter.jsonAssetLibrary : null,
			chapter != null ? chapter.graph : null);

		if (node.lines != null)
		{
			foreach (DialogueLine line in node.lines)
			{
				if (line == null ||
					string.IsNullOrWhiteSpace(line.speakerId) ||
					IsRenderableStorySpeaker(line.speaker))
				{
					continue;
				}

				TryRepairDialogueCharacter(
					resolver,
					node.guid,
					line.speakerId,
					line.speakerNameHint,
					line.speaker,
					recovered => line.speaker = recovered);
			}
		}

		if (node.activeCharacters == null)
			return;

		foreach (DialogueCharacterEntry entry in node.activeCharacters)
		{
			if (entry == null || IsRenderableStorySpeaker(entry.character))
				continue;

			string id = !string.IsNullOrWhiteSpace(entry.speakerNameHint)
				? entry.speakerNameHint
				: entry.character != null ? entry.character.name : "";
			if (string.IsNullOrWhiteSpace(id))
				continue;

			TryRepairDialogueCharacter(
				resolver,
				node.guid,
				id,
				entry.speakerNameHint,
				entry.character,
				recovered => entry.character = recovered);
		}
	}

	void TryRepairDialogueCharacter(
		StoryChapterJsonAssetResolver resolver,
		string nodeId,
		string speakerId,
		string displayName,
		CharacterData previous,
		Action<CharacterData> apply)
	{
		if (resolver == null || apply == null)
			return;

		if (resolver.TryResolveKnownCharacter(
			speakerId,
			displayName,
			out CharacterData recovered,
			out string source) &&
			IsRenderableStorySpeaker(recovered))
		{
			apply(recovered);
			Debug.Log(
				$"[STORY_CHARACTER][RECOVERED] platform={Application.platform} " +
				$"storyId='{CurrentStoryId}' chapterId='{CurrentChapterId}' nodeId='{nodeId}' " +
				$"speakerId='{speakerId}' previous='{(previous != null ? previous.name : "<null>")}' " +
				$"asset='{recovered.name}' source='{source}'.",
				this);
			return;
		}

		Debug.LogError(
			$"[STORY_CHARACTER][UNRESOLVED] platform={Application.platform} " +
			$"storyId='{CurrentStoryId}' chapterId='{CurrentChapterId}' nodeId='{nodeId}' " +
			$"speakerId='{speakerId}' displayName='{displayName}' " +
			$"previous='{(previous != null ? previous.name : "<null>")}' " +
			$"previousRenderable={IsRenderableStorySpeaker(previous)} " +
			$"hasLibrary={GetCurrentChapterOrNull()?.jsonAssetLibrary != null} " +
			$"hasGeneratedGraph={GetCurrentChapterOrNull()?.graph != null} " +
			$"registry={StoryRuntimeAssetRegistryResolver.DescribeActiveRegistry()}.",
			this);
	}

	void EnsureRuntimeActiveCharacters(DialogueNode node)
	{
		if (!autoBuildActiveCharacters || node == null || HasExplicitCharacters(node))
			return;

		var generated = BuildRuntimeActiveCharacters(node.lines);
		if (generated.Count > 0)
			node.activeCharacters = generated;
	}

	List<DialogueCharacterEntry> BuildRuntimeActiveCharacters(List<DialogueLine> lines)
	{
		var result = new List<DialogueCharacterEntry>();
		if (lines == null)
			return result;

		var seen = new HashSet<CharacterData>();
		foreach (var line in lines)
		{
			CharacterData speaker = line?.speaker;
			if (speaker == null || !IsRenderableStorySpeaker(speaker) || !seen.Add(speaker))
				continue;

			result.Add(new DialogueCharacterEntry
			{
				character = speaker,
				emotion = CharacterEmotionType.Idle,
				position = GetDefaultSpeakerPosition(line, speaker),
				speakerNameHint = !string.IsNullOrWhiteSpace(line.speakerId) ? line.speakerId : speaker.name
			});
		}

		return result;
	}

	CharacterPosition GetDefaultSpeakerPosition(CharacterData speaker)
	{
		return IsHeroSpeaker(speaker) ? heroCharacterPosition : otherCharacterPosition;
	}

	CharacterPosition GetDefaultSpeakerPosition(DialogueLine line, CharacterData speaker)
	{
		string speakerToken = NormalizeSpeakerToken(line != null ? line.speakerId : "");
		string heroToken = NormalizeSpeakerToken(heroCharacterId);
		if (string.IsNullOrEmpty(heroToken))
			heroToken = "hero";

		return speakerToken == heroToken || speakerToken == "jsoncharacter" + heroToken
			? heroCharacterPosition
			: GetDefaultSpeakerPosition(speaker);
	}

	bool TryGetHeroActivePosition(DialogueNode node, out CharacterPosition position)
	{
		position = heroCharacterPosition;

		if (node == null || node.activeCharacters == null)
			return false;

		foreach (var entry in node.activeCharacters)
		{
			if (!IsHeroActiveEntry(entry))
				continue;

			position = entry.position;
			return true;
		}

		return false;
	}

	bool IsHeroActiveEntry(DialogueCharacterEntry entry)
	{
		if (entry == null)
			return false;

		if (IsHeroSpeaker(entry.character))
			return true;

		string heroToken = NormalizeSpeakerToken(heroCharacterId);
		if (string.IsNullOrEmpty(heroToken))
			heroToken = "hero";

		string hint = NormalizeSpeakerToken(entry.speakerNameHint);
		return hint == heroToken || hint == "jsoncharacter" + heroToken;
	}

	bool IsHeroSpeaker(CharacterData speaker)
	{
		if (speaker == null)
			return false;

		if (speaker.inheritAppearanceFromPlayer)
			return true;

		string heroToken = NormalizeSpeakerToken(heroCharacterId);
		if (string.IsNullOrEmpty(heroToken))
			heroToken = "hero";

		return NormalizeSpeakerToken(speaker.name) == heroToken ||
			   NormalizeSpeakerToken(speaker.characterName) == heroToken ||
			   NormalizeSpeakerToken(speaker.name) == "jsoncharacter" + heroToken;
	}

	bool IsRenderableStorySpeaker(CharacterData speaker)
	{
		if (speaker == null)
			return false;

		if (speaker.defaultSprite != null ||
			speaker.bodySprite != null ||
			speaker.GetBaseSprite() != null ||
			speaker.GetBodySprite() != null)
		{
			return true;
		}

		if (speaker.emotions != null)
		{
			foreach (CharacterEmotion emotion in speaker.emotions)
			{
				if (emotion != null && emotion.sprite != null)
					return true;
			}
		}

		if (speaker.emotionLayers != null)
		{
			foreach (CharacterEmotionLayer layer in speaker.emotionLayers)
			{
				if (layer != null && layer.faceSprite != null)
					return true;
			}
		}

		return false;
	}

	static string NormalizeSpeakerToken(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		return value.Trim()
			.Replace(" ", "")
			.Replace("_", "")
			.Replace("-", "")
			.ToLowerInvariant();
	}

	bool TryGetActiveSpeakerPosition(DialogueNode node, CharacterData speaker, out CharacterPosition position)
	{
		position = CharacterPosition.Center;

		if (node == null || speaker == null || node.activeCharacters == null)
			return false;

		foreach (var entry in node.activeCharacters)
		{
			if (entry == null || entry.character != speaker)
				continue;

			position = entry.position;
			return true;
		}

		return false;
	}

	static bool HasExplicitCharacters(DialogueNode node)
	{
		if (node == null || node.activeCharacters == null)
			return false;

		foreach (var entry in node.activeCharacters)
		{
			if (entry != null && entry.character != null)
				return true;
		}

		return false;
	}

	static void MarkPositionUsed(CharacterPosition position, ref bool leftUsed, ref bool centerUsed, ref bool rightUsed)
	{
		if (position == CharacterPosition.Left) leftUsed = true;
		if (position == CharacterPosition.Center) centerUsed = true;
		if (position == CharacterPosition.Right) rightUsed = true;
	}

}
