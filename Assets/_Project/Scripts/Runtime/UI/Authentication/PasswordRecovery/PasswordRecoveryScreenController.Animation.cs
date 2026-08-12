using DG.Tweening;
using UnityEngine;

public sealed partial class PasswordRecoveryScreenController
{
    [Header("Transition")]
    [SerializeField, Min(0f)] private float _transitionDuration = 0.3f;
    [SerializeField] private Ease _transitionEase = Ease.OutCubic;

    private Sequence _transition;

    private void ResolveGroups()
    {
        Transform inputTarget = _emailInput != null ? _emailInput.transform : null;
        if (inputTarget != null && inputTarget.parent != null &&
            inputTarget.parent.name.Contains("InputField"))
            inputTarget = inputTarget.parent;

        _emailInputGroup = GetOrAddGroup(_emailInputGroup, inputTarget);
        _successGroup = GetOrAddGroup(
            _successGroup, _successText != null ? _successText.transform : null);
        _actionButtonLabelGroup = GetOrAddGroup(
            _actionButtonLabelGroup,
            _actionButtonLabel != null ? _actionButtonLabel.transform : null);
    }

    private void PlaySuccessTransition()
    {
        KillTransition();
        _transitioning = true;
        if (_emailInput != null)
            _emailInput.interactable = false;
        if (_emailInputGroup != null)
        {
            _emailInputGroup.interactable = false;
            _emailInputGroup.blocksRaycasts = false;
        }

        float duration = Mathf.Max(0f, _transitionDuration);
        if (!Application.isPlaying || duration <= 0f)
        {
            CompleteSuccessTransition();
            return;
        }

        float labelHalf = duration * 0.5f;
        _transition = DOTween.Sequence().SetUpdate(true);
        if (_emailInputGroup != null)
            _transition.Insert(0f, _emailInputGroup.DOFade(0f, duration));
        if (_successGroup != null)
        {
            ActivateGroup(_successGroup);
            _transition.Insert(
                duration * 0.2f,
                _successGroup.DOFade(1f, duration * 0.8f));
        }
        if (_actionButtonLabelGroup != null)
        {
            _transition.Insert(
                0f, _actionButtonLabelGroup.DOFade(0f, labelHalf));
            _transition.InsertCallback(labelHalf, SetReturnButtonText);
            _transition.Insert(
                labelHalf,
                _actionButtonLabelGroup.DOFade(1f, labelHalf));
        }
        else
        {
            _transition.InsertCallback(labelHalf, SetReturnButtonText);
        }
        _transition.SetEase(_transitionEase)
            .OnComplete(CompleteSuccessTransition);
    }

    private void CompleteSuccessTransition()
    {
        _transition = null;
        SetReturnButtonText();
        SetGroup(_emailInputGroup, 0f, false);
        SetGroup(_successGroup, 1f, false);
        SetGroup(_actionButtonLabelGroup, 1f, false);
        _transitioning = false;
        RefreshForm();
    }

    private void SetReturnButtonText()
    {
        if (_actionButtonLabel != null)
            _actionButtonLabel.text = _returnButtonText;
    }

    private void KillTransition()
    {
        if (_transition != null && _transition.IsActive())
            _transition.Kill(false);
        _transition = null;
    }

    private static CanvasGroup GetOrAddGroup(
        CanvasGroup assigned,
        Transform target)
    {
        if (assigned != null)
            return assigned;
        if (target == null)
            return null;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    private static void ActivateGroup(CanvasGroup group)
    {
        if (group != null && !group.gameObject.activeSelf)
            group.gameObject.SetActive(true);
    }

    private static void SetGroup(
        CanvasGroup group,
        float alpha,
        bool interactive)
    {
        if (group == null)
            return;
        ActivateGroup(group);
        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }
}
