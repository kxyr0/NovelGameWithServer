using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed partial class WardrobeHeroSetupPage
{
#if UNITY_EDITOR
    public static void EditorNotifyClothingItemChanged(ClothingItem item)
    {
        if (item == null || Application.isPlaying)
            return;

        WardrobeHeroSetupPage[] pages = Resources.FindObjectsOfTypeAll<WardrobeHeroSetupPage>();
        foreach (WardrobeHeroSetupPage page in pages)
        {
            if (page == null || EditorUtility.IsPersistent(page))
                continue;

            if (page._editorPreviewEnabled)
            {
                page.ApplyEditorPreview();
                continue;
            }

            page.RefreshClothingItemPreviewAfterSourceChange(item);
        }
    }

    public static void EditorNotifyCharacterDataChanged(CharacterData character)
    {
        if (character == null || Application.isPlaying)
            return;

        WardrobeHeroSetupPage[] pages = Resources.FindObjectsOfTypeAll<WardrobeHeroSetupPage>();
        foreach (WardrobeHeroSetupPage page in pages)
        {
            if (page == null || EditorUtility.IsPersistent(page) || !page.UsesCharacterForPreview(character))
                continue;

            if (page._editorPreviewEnabled)
            {
                page.ApplyEditorPreview();
                continue;
            }

            if (page.gameObject.activeInHierarchy)
                page.RefreshPreviewAfterSourceChange();
        }
    }

    public void EditorSetPreviewCharacter(CharacterData character)
    {
        if (character == null)
            return;

        _editorPreviewCharacterOverride = character;
        EditorRefreshPreview();
    }

    public void EditorClearPreviewCharacterOverride()
    {
        _editorPreviewCharacterOverride = null;
        EditorRefreshPreview();
    }

    public void EditorShowPreview()
    {
        _editorPreviewEnabled = true;
        ApplyEditorPreview();
        EditorUtility.SetDirty(this);
    }

    public void EditorRefreshPreview()
    {
        _editorPreviewEnabled = true;
        ApplyEditorPreview();
        EditorUtility.SetDirty(this);
    }

    public void EditorHidePreview()
    {
        _editorPreviewEnabled = false;
        ClearEditorPreview();
        EditorUtility.SetDirty(this);
    }

    public void EditorMovePreviewStep(int direction)
    {
        int stepCount = Enum.GetValues(typeof(WardrobeHeroSetupStep)).Length;
        int next = ((int)_editorPreviewStep + direction) % stepCount;
        if (next < 0)
            next += stepCount;

        _editorPreviewStep = (WardrobeHeroSetupStep)next;
        _editorPreviewSelectedIndex = 0;
        EditorRefreshPreview();
    }

    public void EditorSelectPreviousOption()
    {
        EditorMovePreviewOption(-1);
    }

    public void EditorSelectNextOption()
    {
        EditorMovePreviewOption(1);
    }

    public void EditorMovePreviewOption(int direction)
    {
        if (Application.isPlaying)
        {
            SelectRelativeOption(direction);
            return;
        }

        _editorPreviewEnabled = true;
        BuildOptionsForStep(_editorPreviewStep, _currentOptions);

        if (_currentOptions.Count == 0)
        {
            ApplyEditorPreview();
            EditorUtility.SetDirty(this);
            return;
        }

        int next = _editorPreviewSelectedIndex + direction;
        if (_wrapOptionNavigation)
        {
            next %= _currentOptions.Count;
            if (next < 0)
                next += _currentOptions.Count;
        }
        else
        {
            next = Mathf.Clamp(next, 0, _currentOptions.Count - 1);
        }

        _editorPreviewSelectedIndex = next;
        ApplyEditorPreview();
        EditorUtility.SetDirty(this);
    }

    void QueueEditorPreviewRefresh()
    {
        if (Application.isPlaying || !_editorPreviewAutoRefresh)
            return;

        if (!_editorPreviewEnabled && !HasEditorPreviewGeneratedOptions())
            return;

        if (_editorPreviewQueued)
            return;

        _editorPreviewQueued = true;
        EditorApplication.delayCall += ApplyQueuedEditorPreview;
    }

    bool HasEditorPreviewGeneratedOptions()
    {
        if (_optionsContainer == null)
            return false;

        for (int i = 0; i < _optionsContainer.childCount; i++)
        {
            Transform child = _optionsContainer.GetChild(i);
            if (child != null && child.name.StartsWith(EditorPreviewOptionNamePrefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    void ApplyQueuedEditorPreview()
    {
        _editorPreviewQueued = false;

        if (this == null || Application.isPlaying)
            return;

        if (_editorPreviewEnabled)
            ApplyEditorPreview();
        else
            ClearEditorPreview();
    }

    void ApplyEditorPreview()
    {
        if (Application.isPlaying)
            return;

        AutoWire();
        _mode = OpenMode.FullSetup;
        _isOpen = false;

        BuildFullSetupSteps();
        PrepareLayeredPreviewForOpen();
        int setupIndex = _fullSetupSteps.IndexOf(_editorPreviewStep);
        if (setupIndex < 0)
        {
            _fullSetupSteps.Clear();
            _fullSetupSteps.Add(_editorPreviewStep);
            setupIndex = 0;
        }

        _stepIndex = setupIndex;
        _selectedOptionIndex = _editorPreviewSelectedIndex;

        BuildOptionsForStep(_editorPreviewStep, _currentOptions);
        SetEditorPreviewVisible(true);
        ShowOptions(GetStepTitle(_editorPreviewStep), GetStepDescription(_editorPreviewStep), _currentOptions);
        _editorPreviewSelectedIndex = Mathf.Max(0, _selectedOptionIndex);

        EditorUtility.SetDirty(this);
    }

    void ClearEditorPreview()
    {
        if (Application.isPlaying)
            return;

        ClearOptions();

        if (_setupContentRoot != null)
            _setupContentRoot.SetActive(false);

        if (_editorPreviewHideOldWardrobeObjects)
        {
            foreach (var target in _hideWhileSetupOpen)
            {
                if (target != null)
                    target.SetActive(true);
            }
        }

        if (_editorPreviewHideStoryObjects)
        {
            foreach (var target in _hideStoryObjectsWhileOpen)
            {
                if (target != null)
                    target.SetActive(true);
            }
        }
    }

    void SetEditorPreviewVisible(bool visible)
    {
        if (_pageRoot != null && _editorPreviewActivatePageRoot)
            _pageRoot.SetActive(true);

        if (_pageCanvasGroup != null)
        {
            _pageCanvasGroup.alpha = visible ? 1f : 0f;
            _pageCanvasGroup.interactable = false;
            _pageCanvasGroup.blocksRaycasts = false;
        }

        if (_setupContentRoot != null)
            _setupContentRoot.SetActive(visible);

        if (_editorPreviewHideOldWardrobeObjects)
        {
            foreach (var target in _hideWhileSetupOpen)
            {
                if (target == null)
                    continue;

                if (IsLayeredPreviewObject(target))
                {
                    target.SetActive(true);
                    continue;
                }

                target.SetActive(!visible);
            }
        }

        if (_editorPreviewHideStoryObjects)
        {
            foreach (var target in _hideStoryObjectsWhileOpen)
            {
                if (target != null)
                    target.SetActive(!visible);
            }
        }
    }
#endif

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    static bool HasBindingEntries(List<string> values)
    {
        if (values == null)
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return true;
        }

        return false;
    }

    static bool MatchesAnyBinding(List<string> values, string candidate)
    {
        if (values == null || values.Count == 0 || string.IsNullOrWhiteSpace(candidate))
            return false;

        string normalizedCandidate = NormalizeBindingId(candidate);
        for (int i = 0; i < values.Count; i++)
        {
            string normalizedValue = NormalizeBindingId(values[i]);
            if (normalizedValue.Length == 0)
                continue;

            if (string.Equals(normalizedValue, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static string NormalizeBindingId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string sanitized = SaveDataSanitizer.SanitizeIdentifier(value);
        return string.IsNullOrWhiteSpace(sanitized) ? value.Trim() : sanitized.Trim();
    }
}
