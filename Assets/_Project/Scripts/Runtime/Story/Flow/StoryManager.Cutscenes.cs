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
    void ProcessCutsceneImage(ImageNode node)
    {
        if (node == null)
            return;

        HideImageOverlayIfVisible();
        HideBackgroundBeforeCutsceneLayoutSwitch();
        ClearCutsceneRuntimeState();

        GameState.Instance.currentNode = node;
        activeDialogueNode = null;
        activeCutsceneImageNode = node;
        activeCutsceneImageLine = BuildCutsceneImageLine(node);
        activeCutsceneImageEnterFrame = Time.frameCount;
        cutsceneTextRevealed = false;
        ResetDialogueLinePages();

        BeginCutsceneBackgroundFraming();
        ApplyCutsceneMedia(node);
        BeginCutsceneBackgroundFraming();
        characterView?.HideAll(0f);
        ResetCameraPosition();
        HideImageCutsceneDialogueInterface();

        if (activeCutsceneImageLine == null)
        {
            PersistProgress(node);
            return;
        }

        cutsceneTextRevealRoutine = StartCoroutine(RevealCutsceneTextAfterDelay(cutsceneImageTextDelay));
        PersistProgress(node);
    }

    void HideImageCutsceneDialogueInterface()
    {
        if (dialogueUI == null)
            AutoWireSceneReferences();

        dialogueUI?.HideDialoguePanelForCutsceneIntro();

        if (cutsceneUserInterface != null && cutsceneUserInterface != dialogueUI)
            cutsceneUserInterface.HideDialoguePanelForCutsceneIntro();
    }

    DialogueUIManager GetImageCutsceneDialogueInterface()
    {
        if (dialogueUI == null)
            AutoWireSceneReferences();

        return dialogueUI;
    }

    void ProcessCutscene(CutsceneNode node)
    {
        if (node == null || !EnsureCutsceneUserInterface("showing cutscene"))
            return;

        if (isSkippingToNextChoice)
        {
            ClearCutsceneRuntimeState();
            GoNext(node, "exit");
            return;
        }

        HideBackgroundBeforeCutsceneLayoutSwitch();
        ClearCutsceneRuntimeState();

        GameState.Instance.currentNode = node;
        activeDialogueNode = node;
        currentLineIndex = 0;
        ResetDialogueLinePages();
        cutsceneTextRevealed = false;

        BeginCutsceneBackgroundFraming();
        ApplyCutsceneMedia(node);
        BeginCutsceneBackgroundFraming();

        if (node.HideCharacters)
            characterView?.HideAll(0f);

        ResetCameraPosition();
        cutsceneUserInterface.HideDialoguePanelForCutsceneIntro();

        if (node.lines == null || node.lines.Count == 0)
        {
            PersistProgress(node);
            return;
        }

        cutsceneTextRevealRoutine = StartCoroutine(RevealCutsceneTextAfterDelay(node.TextDelay));
        PersistProgress(node);
    }

    void BeginCutsceneBackgroundFraming()
    {
        if (backgroundView == null)
            backgroundView = FindObjectOfType<BackgroundViewManager>(true);

        cutsceneBackgroundSceneActive = true;
        backgroundView?.BeginCutsceneHorizontalFraming();
    }

    void HideBackgroundBeforeCutsceneLayoutSwitch()
    {
        if (backgroundView == null)
            backgroundView = FindObjectOfType<BackgroundViewManager>(true);

        backgroundView?.HideCurrentMediaBeforeLayoutSwitch();
    }

    void EndCutsceneBackgroundFraming()
    {
        backgroundView?.EndCutsceneHorizontalFraming();
    }

    void ApplyCutsceneMedia(CutsceneNode node)
    {
        if (node == null)
            return;

        if (backgroundView == null)
            backgroundView = FindObjectOfType<BackgroundViewManager>(true);

        if (backgroundView == null)
        {
            Debug.LogWarning("[StoryManager] backgroundView is required for cutscene media.", this);
            return;
        }

        if (node.video != null)
        {
            backgroundView.SetBackgroundVideo(node.video);
            return;
        }

        if (node.gif != null)
        {
            backgroundView.SetBackgroundGif(node.gif);
            return;
        }

        if (node.image != null)
            backgroundView.SetBackground(node.image);
    }

    void ApplyCutsceneMedia(ImageNode node)
    {
        if (node == null)
            return;

        if (backgroundView == null)
            backgroundView = FindObjectOfType<BackgroundViewManager>(true);

        if (backgroundView == null)
        {
            Debug.LogWarning("[StoryManager] backgroundView is required for image cutscene media.", this);
            return;
        }

        if (node.video != null)
        {
            backgroundView.SetBackgroundVideo(node.video);
            return;
        }

        if (node.gif != null)
        {
            backgroundView.SetBackgroundGif(node.gif);
            return;
        }

        if (node.image != null)
            backgroundView.SetBackground(node.image);
    }

    DialogueLine BuildCutsceneImageLine(ImageNode node)
    {
        string text = GetCutsceneImageDialogueText(node);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new DialogueLine
        {
            richText = text
        };
    }

    string GetCutsceneImageDialogueText(ImageNode node)
    {
        if (node == null)
            return "";

        string text = StripCutsceneLabelPrefix(node.description);
        string caption = StripCutsceneLabelPrefix(node.caption);

        if (string.IsNullOrWhiteSpace(text))
            return "";

        if (!string.IsNullOrWhiteSpace(caption) &&
            string.Equals(text.Trim(), caption.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        string imageName = node.image != null ? StripCutsceneLabelPrefix(node.image.name) : "";
        if (!string.IsNullOrWhiteSpace(imageName) &&
            string.Equals(text.Trim(), imageName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return text.Trim();
    }

    static string StripCutsceneLabelPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string text = PlayerAppearance.ReplacePlaceholders(value).Trim();
        string prepared = text.ToLowerInvariant();
        string[] unicodeMarkers =
        {
            "\u043a\u0430\u0442-\u0441\u0446\u0435\u043d\u0430",
            "\u043a\u0430\u0442 \u0441\u0446\u0435\u043d\u0430",
            "\u043a\u0430\u0442\u0441\u0446\u0435\u043d\u0430"
        };

        for (int i = 0; i < unicodeMarkers.Length; i++)
        {
            string marker = unicodeMarkers[i];
            if (!prepared.StartsWith(marker, StringComparison.Ordinal))
                continue;

            text = text.Substring(marker.Length).TrimStart(' ', ':', '-', '\u2013', '\u2014');
            return text.Trim();
        }
        string[] markers = { "кат-сцена", "катсцена", "cutscene" };

        for (int i = 0; i < markers.Length; i++)
        {
            string marker = markers[i];
            if (!prepared.StartsWith(marker, StringComparison.Ordinal))
                continue;

            text = text.Substring(marker.Length).TrimStart(' ', ':', '-', '\u2013', '\u2014');
            return text.Trim();
        }

        return text;
    }

    IEnumerator RevealCutsceneTextAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        else
            yield return null;

        cutsceneTextRevealRoutine = null;
        RevealCutsceneText();
    }

    bool TryRevealCutsceneTextFromInput()
    {
        if (activeCutsceneImageNode != null)
        {
            if (cutsceneTextRevealed || activeCutsceneImageLine == null)
                return false;

            StopCutsceneTextRevealRoutine();
            RevealCutsceneText();
            return true;
        }

        if (!(activeDialogueNode is CutsceneNode) || cutsceneTextRevealed)
            return false;

        if (activeDialogueNode.lines == null || activeDialogueNode.lines.Count == 0)
            return false;

        StopCutsceneTextRevealRoutine();
        RevealCutsceneText();
        return true;
    }

    void RevealCutsceneText()
    {
        if (activeCutsceneImageNode != null)
        {
            RevealCutsceneImageText();
            return;
        }

        if (!(activeDialogueNode is CutsceneNode node) ||
            cutsceneTextRevealed ||
            node.lines == null ||
            node.lines.Count == 0)
        {
            return;
        }

        if (!EnsureCutsceneUserInterface("revealing cutscene text"))
            return;

        cutsceneTextRevealed = true;
        currentLineIndex = Mathf.Clamp(currentLineIndex, 0, node.lines.Count - 1);

        DialogueLine line = node.lines[currentLineIndex];
        ShowDialogueLinePage(line);
        TryPanCutsceneBackground(line);
        RecordDialogueHistory(line);
        PersistProgress(node);
    }

    void RevealCutsceneImageText()
    {
        if (activeCutsceneImageNode == null ||
            cutsceneTextRevealed ||
            activeCutsceneImageLine == null)
        {
            return;
        }

        DialogueUIManager targetUi = GetImageCutsceneDialogueInterface();
        if (targetUi == null)
        {
            activeCutsceneImageLine = null;
            cutsceneTextRevealed = true;
            return;
        }

        cutsceneTextRevealed = true;
        targetUi.ShowLineText(activeCutsceneImageLine, ReplaceStoryPlaceholders(activeCutsceneImageLine.richText));
        RecordDialogueHistory(activeCutsceneImageLine);
        PersistProgress(activeCutsceneImageNode);
    }

    void StopCutsceneTextRevealRoutine()
    {
        if (cutsceneTextRevealRoutine == null)
            return;

        StopCoroutine(cutsceneTextRevealRoutine);
        cutsceneTextRevealRoutine = null;
    }

    void FinishCutsceneImage()
    {
        ImageNode node = activeCutsceneImageNode;
        if (node == null)
            return;

        HideImageCutsceneDialogueInterface();
        bool exitOpensScene = DoesCutsceneExitOpenScene(node);
        PrepareCutsceneBackgroundForExit(node);
        GoNext(node, "exit");
        if (exitOpensScene)
            CompleteCutsceneBackgroundExit();
    }

    void RestoreSceneBackgroundAfterCutscene(BaseStoryNode cutsceneNode)
    {
        if (cutsceneNode == null || DoesCutsceneExitOpenScene(cutsceneNode))
            return;

        var graph = storyGraph ?? GetCurrentGraphOrNull();
        SceneSetupNode scene = FindSceneBeforeNode(graph, cutsceneNode);
        if (scene == null)
        {
            Debug.LogWarning($"[StoryManager] Could not find a previous scene to restore after cutscene '{cutsceneNode.guid}'.", cutsceneNode);
            return;
        }

        ProcessScene(scene, false);
    }

    bool DoesCutsceneExitOpenScene(BaseStoryNode cutsceneNode)
    {
        var port = cutsceneNode != null ? cutsceneNode.GetOutputPort("exit") : null;
        if (port == null || port.Connection == null)
            return false;

        return port.Connection.node is SceneSetupNode;
    }

    void PrepareCutsceneBackgroundForExit(BaseStoryNode cutsceneNode)
    {
        bool exitOpensScene = DoesCutsceneExitOpenScene(cutsceneNode);
        ClearCutsceneRuntimeState(false);

        if (exitOpensScene)
            return;

        RestoreSceneBackgroundAfterCutscene(cutsceneNode);
        CompleteCutsceneBackgroundExit();
    }

    void CompleteCutsceneBackgroundExit()
    {
        cutsceneBackgroundSceneActive = false;
        EndCutsceneBackgroundFraming();
        ResetCutsceneBackgroundCamera();
    }

    void ClearCutsceneRuntimeState(bool endBackgroundFraming = true)
    {
        StopCutsceneTextRevealRoutine();
        if (endBackgroundFraming)
        {
            cutsceneBackgroundSceneActive = false;
            EndCutsceneBackgroundFraming();
            ResetCutsceneBackgroundCamera();
        }
        cutsceneTextRevealed = false;
        activeCutsceneImageNode = null;
        activeCutsceneImageLine = null;
        activeCutsceneImageEnterFrame = -1;
    }

}
