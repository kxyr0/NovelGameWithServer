using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SubscriptionTimelineControlsView : MonoBehaviour
{
    [SerializeField] private Button _rewindBackButton;
    [SerializeField] private Button _rewindForwardButton;
    [SerializeField] private Button _undoChoiceButton;

    public event Action RewindBackClicked;
    public event Action RewindForwardClicked;
    public event Action UndoChoiceClicked;

    public void Assign(Button rewindBackButton, Button rewindForwardButton, Button undoChoiceButton)
    {
        Unbind();
        _rewindBackButton = rewindBackButton;
        _rewindForwardButton = rewindForwardButton;
        _undoChoiceButton = undoChoiceButton;
        if (isActiveAndEnabled)
            Bind();
        Apply(new StoryChoiceTimelineState(false, false, false, false));
    }

    void Awake()
    {
        Apply(new StoryChoiceTimelineState(false, false, false, false));
    }

    void OnEnable()
    {
        Bind();
        Apply(new StoryChoiceTimelineState(false, false, false, false));
    }

    void OnDisable()
    {
        Unbind();
        Apply(new StoryChoiceTimelineState(false, false, false, false));
    }

    public void Apply(StoryChoiceTimelineState state)
    {
        SetInteractable(_rewindBackButton, state.CanRewindBack);
        SetInteractable(_rewindForwardButton, state.CanRewindForward);
        SetInteractable(_undoChoiceButton, state.CanUndoChoice);
    }

    void Bind()
    {
        Unbind();
        if (_rewindBackButton != null)
            _rewindBackButton.onClick.AddListener(OnBack);
        if (_rewindForwardButton != null)
            _rewindForwardButton.onClick.AddListener(OnForward);
        if (_undoChoiceButton != null)
            _undoChoiceButton.onClick.AddListener(OnUndo);
    }

    void Unbind()
    {
        if (_rewindBackButton != null)
            _rewindBackButton.onClick.RemoveListener(OnBack);
        if (_rewindForwardButton != null)
            _rewindForwardButton.onClick.RemoveListener(OnForward);
        if (_undoChoiceButton != null)
            _undoChoiceButton.onClick.RemoveListener(OnUndo);
    }

    void OnBack() => RewindBackClicked?.Invoke();
    void OnForward() => RewindForwardClicked?.Invoke();
    void OnUndo() => UndoChoiceClicked?.Invoke();

    static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
}
