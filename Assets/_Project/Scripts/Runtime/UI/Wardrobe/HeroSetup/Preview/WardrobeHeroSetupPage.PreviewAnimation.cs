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
    void SetStepPreviewAnimated(RuntimeOption option, int direction)
    {
        if (!_animateOptionChanges || direction == 0 || option == null || !Application.isPlaying)
        {
            KillOptionPreviewTween(true);
            SetStepPreview(option);
            return;
        }

        Image target = GetPreviewImageForStep(option.Step);
        if (target == null && option.Step != WardrobeHeroSetupStep.Accessories)
            target = _previewImage;

        RectTransform rect = GetOptionAnimationRect(option.Step, target);
        if (target == null || rect == null)
        {
            SetStepPreview(option);
            return;
        }

        KillOptionPreviewTween(true);

        CanvasGroup group = GetOrAddPreviewCanvasGroup(rect);
        Vector2 shownPosition = rect.anchoredPosition;
        Vector2 outOffset = Vector2.left * Mathf.Sign(direction) * _optionSwipeDistance;
        Vector2 inOffset = -outOffset;
        float halfDuration = Mathf.Max(0.01f, _optionSwipeDuration * 0.5f);
        bool animatePosition = option.Step == WardrobeHeroSetupStep.Appearance;

        _optionAnimatedRect = rect;
        _optionAnimatedCanvasGroup = group;
        _optionAnimatedBasePosition = shownPosition;

        SetOptionCycleButtonsInteractable(false);

        _optionPreviewTween = DOTween.Sequence().SetUpdate(_useUnscaledOptionAnimation);
        _optionPreviewTween.Append(group.DOFade(0f, halfDuration).SetEase(_optionSwipeOutEase));
        if (animatePosition)
            _optionPreviewTween.Join(rect.DOAnchorPos(shownPosition + outOffset, halfDuration).SetEase(_optionSwipeOutEase));
        _optionPreviewTween.AppendCallback(() =>
        {
            SetStepPreview(option);
            shownPosition = rect.anchoredPosition;
            _optionAnimatedBasePosition = shownPosition;
            if (animatePosition)
                rect.anchoredPosition = shownPosition + inOffset;
            group.alpha = 0f;
        });
        _optionPreviewTween.Append(group.DOFade(1f, halfDuration).SetEase(_optionSwipeInEase));
        if (animatePosition)
            _optionPreviewTween.Join(rect.DOAnchorPos(shownPosition, halfDuration).SetEase(_optionSwipeInEase));
        _optionPreviewTween.OnComplete(() =>
        {
            rect.anchoredPosition = shownPosition;
            group.alpha = 1f;
            _optionPreviewTween = null;
            _optionAnimatedRect = null;
            _optionAnimatedCanvasGroup = null;
            RefreshNavigationButtons();
        });
    }

    void KillOptionPreviewTween(bool restore)
    {
        if (_optionPreviewTween != null)
        {
            _optionPreviewTween.Kill();
            _optionPreviewTween = null;
        }

        if (restore)
        {
            if (_optionAnimatedRect != null)
                _optionAnimatedRect.anchoredPosition = _optionAnimatedBasePosition;
            if (_optionAnimatedCanvasGroup != null)
                _optionAnimatedCanvasGroup.alpha = 1f;
            SetOptionCycleButtonsInteractable(true);
        }

        _optionAnimatedRect = null;
        _optionAnimatedCanvasGroup = null;
    }

    RectTransform GetOptionAnimationRect(WardrobeHeroSetupStep step, Image previewTarget)
    {
        if (_animateWholeLayeredCharacterForClothingChanges && step != WardrobeHeroSetupStep.Appearance)
        {
            RectTransform layeredRoot = GetLayeredCharacterAnimationRoot(step);
            if (layeredRoot != null)
                return layeredRoot;
        }

        return previewTarget != null ? previewTarget.rectTransform : null;
    }

    RectTransform GetLayeredCharacterAnimationRoot(WardrobeHeroSetupStep step)
    {
        Image requiredLayer;
        switch (step)
        {
            case WardrobeHeroSetupStep.Hair:
                requiredLayer = _hairPreviewImage;
                break;
            case WardrobeHeroSetupStep.Accessories:
                requiredLayer = _accessoryPreviewImage;
                break;
            default:
                requiredLayer = _outfitPreviewImage;
                break;
        }

        if (_bodyPreviewImage == null || requiredLayer == null || requiredLayer == _bodyPreviewImage)
            return null;

        Transform parent = _bodyPreviewImage.transform.parent;
        if (parent == null || requiredLayer.transform.parent != parent)
            return null;

        return parent as RectTransform;
    }

    CanvasGroup GetOrAddPreviewCanvasGroup(RectTransform rectTransform)
    {
        CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        return canvasGroup;
    }

    void SetOptionCycleButtonsInteractable(bool interactable)
    {
        if (interactable)
        {
            RefreshNavigationButtons();
            return;
        }

        if (_previousOptionButton != null)
            _previousOptionButton.interactable = false;
        if (_nextOptionButton != null)
            _nextOptionButton.interactable = false;

        ApplyOptionArrowDisabledFades(_previousOptionDisabledFades, true);
        ApplyOptionArrowDisabledFades(_nextOptionDisabledFades, true);
    }

    Image GetPreviewImageForStep(WardrobeHeroSetupStep step)
    {
        switch (step)
        {
            case WardrobeHeroSetupStep.Appearance:
                return _bodyPreviewImage != null ? _bodyPreviewImage : _previewImage;
            case WardrobeHeroSetupStep.Outfit:
                return _outfitPreviewImage != null ? _outfitPreviewImage : _previewImage;
            case WardrobeHeroSetupStep.Hair:
                return _hairPreviewImage != null ? _hairPreviewImage : _previewImage;
            case WardrobeHeroSetupStep.Accessories:
                return _accessoryPreviewImage;
            default:
                return _previewImage;
        }
    }
}
