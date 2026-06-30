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
    void ClearOptions()
    {
        if (_optionsContainer == null)
            return;

        for (int i = _optionsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _optionsContainer.GetChild(i);
            if (child == null)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!child.name.StartsWith(EditorPreviewOptionNamePrefix, StringComparison.Ordinal))
                    continue;

                DestroyImmediate(child.gameObject);
                continue;
            }
#endif

            Destroy(child.gameObject);
        }
    }

    void SetSetupObjectsVisible(bool visible)
    {
        if (_setupContentRoot != null)
            SetCanvasGroupVisible(_setupContentRoot, visible);

        foreach (var target in _hideWhileSetupOpen)
        {
            if (target == null)
                continue;

            if (IsLayeredPreviewObject(target))
            {
                SetCanvasGroupVisible(target, true);
                continue;
            }

            SetCanvasGroupVisible(target, !visible);
        }
    }

    void HideStoryObjects()
    {
        foreach (var target in _hideStoryObjectsWhileOpen)
        {
            if (target != null)
                SetCanvasGroupVisible(target, false);
        }
    }

    void RestoreStoryObjects()
    {
        foreach (var target in _hideStoryObjectsWhileOpen)
        {
            if (target != null)
                SetCanvasGroupVisible(target, true);
        }
    }

    void SetCanvasGroupVisible(GameObject target, bool visible)
    {
        if (target == null)
            return;

        if (!target.activeSelf)
            target.SetActive(true);

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    void BindButtons()
    {
        BindOptionCycleButtons();

        if (_buttonsBound)
            return;

        if (CanUseAsBackButton(_backButton))
            _backButton.onClick.AddListener(MoveToPreviousFullSetupStep);

        if (CanUseAsContinueButton(_continueButton))
            _continueButton.onClick.AddListener(ConfirmCurrentStep);

        if (_selectedOptionLabelConfirmButton != null)
            _selectedOptionLabelConfirmButton.onClick.AddListener(ConfirmCurrentStep);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseButtonClicked);

        _buttonsBound = true;
    }

    void UnbindButtons()
    {
        if (!_buttonsBound)
            return;

        if (CanUseAsBackButton(_backButton))
            _backButton.onClick.RemoveListener(MoveToPreviousFullSetupStep);

        if (CanUseAsContinueButton(_continueButton))
            _continueButton.onClick.RemoveListener(ConfirmCurrentStep);

        if (_selectedOptionLabelConfirmButton != null)
            _selectedOptionLabelConfirmButton.onClick.RemoveListener(ConfirmCurrentStep);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(HandleCloseButtonClicked);

        _buttonsBound = false;
    }

    void BindOptionCycleButtons()
    {
        if (_previousOptionButton != null)
        {
            _previousOptionButton.onClick = new Button.ButtonClickedEvent();
            _previousOptionButton.onClick.AddListener(SelectPreviousOption);
        }

        if (_nextOptionButton != null)
        {
            _nextOptionButton.onClick = new Button.ButtonClickedEvent();
            _nextOptionButton.onClick.AddListener(SelectNextOption);
        }
    }

    void AutoWire()
    {
        if (_pageRoot == null)
            _pageRoot = gameObject;

        if (_pageCanvasGroup == null)
            _pageCanvasGroup = GetComponent<CanvasGroup>();

        if (_bodyPreviewImage == null)
            _bodyPreviewImage = FindLayerImage("Body");

        if (_outfitPreviewImage == null)
            _outfitPreviewImage = FindLayerImage("Outfit");

        if (_hairPreviewImage == null)
            _hairPreviewImage = FindLayerImage("Hair");

        if (_accessoryPreviewImage == null)
            _accessoryPreviewImage = FindLayerImage("Accessories");

        if (_accessoryPreviewImage == null)
            _accessoryPreviewImage = FindLayerImage("Accessory");

        if (_previewImage == null)
            _previewImage = _bodyPreviewImage;

        if (_bodyPreviewImage == null)
            _bodyPreviewImage = _previewImage;

        EnsureAccessoryPreviewLayer();

        if (_previousOptionButton == null)
            _previousOptionButton = FindButtonByName("ArrowLeft");

        if (_nextOptionButton == null)
            _nextOptionButton = FindButtonByName("ArrowRight");

        Button detectedConfirmButton = FindConfirmButton();
        if (_continueButton == null || ShouldPreferDetectedConfirmButton(_continueButton, detectedConfirmButton))
            _continueButton = detectedConfirmButton;

        if (_continueButtonLabel == null && _continueButton != null)
            _continueButtonLabel = _continueButton.GetComponentInChildren<TMP_Text>(true);

        Button detectedCloseButton = FindCloseButton();
        if (_closeButton == null || ShouldPreferDetectedCloseButton(_closeButton, detectedCloseButton))
            _closeButton = detectedCloseButton;

        if (_closeScreenNavigator == null)
            _closeScreenNavigator = GetComponentInParent<StoryScreenNavigator>(true);

        if (_closeScreenNavigator == null)
            _closeScreenNavigator = FindObjectOfType<StoryScreenNavigator>(true);

        if (_selectedOptionLabel == null)
            _selectedOptionLabel = FindTextByName("NameCloth");

        if (_selectedOptionPriceIcon == null && _selectedOptionLabel != null)
            _selectedOptionPriceIcon = _selectedOptionLabel.GetComponentInParent<InlinePriceIconLayout>(true);

        EnsureSelectedOptionLabelConfirmButton();
    }

    void EnsureAccessoryPreviewLayer()
    {
        if (_accessoryPreviewImage != null || !Application.isPlaying)
            return;

        Image reference = _bodyPreviewImage != null ? _bodyPreviewImage : _previewImage;
        Transform parent = reference != null
            ? reference.transform.parent
            : _outfitPreviewImage != null ? _outfitPreviewImage.transform.parent
            : _hairPreviewImage != null ? _hairPreviewImage.transform.parent
            : null;

        if (parent == null)
            return;

        var layerObject = new GameObject("Accessory", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        layerObject.transform.SetParent(parent, false);

        RectTransform rect = layerObject.GetComponent<RectTransform>();
        if (reference != null)
        {
            RectTransform sourceRect = reference.rectTransform;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.localScale = sourceRect.localScale;
        }

        _accessoryPreviewImage = layerObject.GetComponent<Image>();
        _accessoryPreviewImage.raycastTarget = false;
        _accessoryPreviewImage.preserveAspect = true;
        _accessoryPreviewImage.enabled = false;

        CanvasGroup canvasGroup = layerObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        EnsureLayerOrder();
    }

    Image FindLayerImage(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image != null &&
                (image.name == objectName || image.name.StartsWith(objectName + " (", StringComparison.Ordinal)))
                return image;
        }

        return null;
    }

    Button FindButtonByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && string.Equals(button.name, objectName, StringComparison.Ordinal))
                return button;
        }

        return null;
    }

    Button FindConfirmButton()
    {
        Button namedButton = FindButtonByNames(
            "ButtonStart",
            "StartButton",
            "SelectButton",
            "ChooseButton",
            "ConfirmButton",
            "ButtonSelect",
            "ButtonChoose",
            "ButtonConfirm");

        if (CanUseAsContinueButton(namedButton) && namedButton != _closeButton)
            return namedButton;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (!CanUseAsContinueButton(button) || button == _closeButton || button == _backButton)
                continue;

            if (button.GetComponent<WardrobeOptionButtonView>() != null)
                return button;
        }

        return null;
    }

    bool ShouldPreferDetectedConfirmButton(Button currentButton, Button detectedButton)
    {
        if (detectedButton == null || currentButton == detectedButton)
            return false;

        if (!CanUseAsContinueButton(currentButton))
            return true;

        bool detectedLooksLikeNewWardrobeButton = detectedButton.GetComponent<WardrobeOptionButtonView>() != null ||
                                                  string.Equals(detectedButton.name, "ButtonStart", StringComparison.OrdinalIgnoreCase);
        bool currentLooksLikeNewWardrobeButton = currentButton.GetComponent<WardrobeOptionButtonView>() != null ||
                                                 string.Equals(currentButton.name, "ButtonStart", StringComparison.OrdinalIgnoreCase);

        return detectedLooksLikeNewWardrobeButton && !currentLooksLikeNewWardrobeButton;
    }

    Button FindCloseButton()
    {
        Button button = FindButtonByNames(
            "ExitButton",
            "CloseButton",
            "ButtonClose",
            "Close",
            "Exit",
            "BackToHistoryButton");

        if (button != null && button != _continueButton && button != _previousOptionButton && button != _nextOptionButton)
            return button;

        return null;
    }

    bool ShouldPreferDetectedCloseButton(Button currentButton, Button detectedButton)
    {
        if (detectedButton == null || currentButton == detectedButton)
            return false;

        if (currentButton == _continueButton || currentButton == _previousOptionButton || currentButton == _nextOptionButton)
            return true;

        bool detectedLooksLikeClose = IsNamedCloseButton(detectedButton);
        bool currentLooksLikeClose = IsNamedCloseButton(currentButton);
        return detectedLooksLikeClose && !currentLooksLikeClose;
    }

    static bool IsNamedCloseButton(Button button)
    {
        if (button == null)
            return false;

        return string.Equals(button.name, "ExitButton", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(button.name, "CloseButton", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(button.name, "ButtonClose", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(button.name, "Close", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(button.name, "Exit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(button.name, "BackToHistoryButton", StringComparison.OrdinalIgnoreCase);
    }

    Button FindButtonByNames(params string[] objectNames)
    {
        if (objectNames == null || objectNames.Length == 0)
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            for (int i = 0; i < objectNames.Length; i++)
            {
                string objectName = objectNames[i];
                if (!string.IsNullOrWhiteSpace(objectName) &&
                    string.Equals(button.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return button;
            }
        }

        return null;
    }

    TMP_Text FindTextByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && string.Equals(text.name, objectName, StringComparison.Ordinal))
                return text;
        }

        return null;
    }

    void EnsureSelectedOptionLabelConfirmButton()
    {
        if (!Application.isPlaying || !_useSelectedOptionLabelAsConfirmButton || _selectedOptionLabel == null)
            return;

        if (CanUseAsContinueButton(_continueButton))
            return;

        _selectedOptionLabelConfirmButton = _selectedOptionLabel.GetComponent<Button>();
        if (_selectedOptionLabelConfirmButton == null)
            _selectedOptionLabelConfirmButton = _selectedOptionLabel.gameObject.AddComponent<Button>();

        _selectedOptionLabelConfirmButton.targetGraphic = _selectedOptionLabel;
    }

    bool CanUseAsBackButton(Button button)
    {
        return button != null &&
               button != _previousOptionButton &&
               button != _nextOptionButton &&
               !IsLegacyOptionArrow(button);
    }

    bool CanUseAsContinueButton(Button button)
    {
        return button != null &&
               button != _previousOptionButton &&
               button != _nextOptionButton &&
               !IsLegacyOptionArrow(button);
    }

    bool IsLegacyOptionArrow(Button button)
    {
        if (button == null)
            return false;

        return string.Equals(button.name, "ArrowLeft", StringComparison.Ordinal) ||
               string.Equals(button.name, "ArrowRight", StringComparison.Ordinal);
    }
}
