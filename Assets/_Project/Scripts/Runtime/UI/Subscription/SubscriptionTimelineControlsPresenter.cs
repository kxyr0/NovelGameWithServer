using UnityEngine;

[DisallowMultipleComponent]
public sealed class SubscriptionTimelineControlsPresenter : MonoBehaviour
{
    [SerializeField] private SubscriptionTimelineControlsView _view;
    [SerializeField] private StoryManager _storyManager;
    ISubscriptionEntitlementService _entitlements;
    IStoryChoiceTimelineService _timeline;

    void Awake()
    {
        ResolveReferences();
        Apply();
    }

    void OnEnable()
    {
        ResolveReferences();
        Bind();
        Apply();
    }

    void OnDisable()
    {
        Unbind();
    }

    public void Assign(
        SubscriptionTimelineControlsView view,
        StoryManager storyManager,
        ISubscriptionEntitlementService entitlements,
        IStoryChoiceTimelineService timeline)
    {
        Unbind();
        _view = view;
        _storyManager = storyManager;
        _entitlements = entitlements;
        _timeline = timeline;
        if (isActiveAndEnabled)
            Bind();
        Apply();
    }

    void Bind()
    {
        if (_view != null)
        {
            _view.RewindBackClicked += RewindBack;
            _view.RewindForwardClicked += RewindForward;
            _view.UndoChoiceClicked += UndoChoice;
        }
        if (_storyManager != null)
            _storyManager.SubscriptionTimelineStateChanged += Apply;
        if (_entitlements != null)
            _entitlements.StateChanged += ApplySubscriptionState;
        if (_timeline != null)
            _timeline.Changed += Apply;
    }

    void Unbind()
    {
        if (_view != null)
        {
            _view.RewindBackClicked -= RewindBack;
            _view.RewindForwardClicked -= RewindForward;
            _view.UndoChoiceClicked -= UndoChoice;
        }
        if (_storyManager != null)
            _storyManager.SubscriptionTimelineStateChanged -= Apply;
        if (_entitlements != null)
            _entitlements.StateChanged -= ApplySubscriptionState;
        if (_timeline != null)
            _timeline.Changed -= Apply;
    }

    void RewindBack()
    {
        _storyManager?.TrySubscriptionRewindBack();
        Apply();
    }

    void RewindForward()
    {
        _storyManager?.TrySubscriptionRewindForward();
        Apply();
    }

    void UndoChoice()
    {
        _storyManager?.TrySubscriptionUndoLastChoice();
        Apply();
    }

    void Apply()
    {
        if (_view == null || _storyManager == null)
            return;
        _view.Apply(_storyManager.GetSubscriptionTimelineState());
    }

    void ApplySubscriptionState(SubscriptionFeatureState state)
    {
        Apply();
    }

    void ResolveReferences()
    {
        if (_view == null)
            _view = GetComponent<SubscriptionTimelineControlsView>();
        if (_storyManager == null)
            _storyManager = FindObjectOfType<StoryManager>(true);
    }
}
