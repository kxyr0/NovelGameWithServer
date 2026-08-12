using UnityEngine;

public partial class MenuController
{
    [Header("Story Carousel Swipe")]
    [SerializeField, Tooltip("Переключать истории горизонтальным свайпом по карточкам.")]
    private bool _storyCarouselSwipeEnabled = true;

    [SerializeField, Min(1f), Tooltip("Минимальная длина свайпа в экранных пикселях.")]
    private float _storyCarouselSwipeDistance = 80f;

    [SerializeField, Min(1f), Tooltip("Насколько горизонтальное движение должно преобладать над вертикальным.")]
    private float _storyCarouselSwipeHorizontalDominance = 1.15f;

    [SerializeField, Tooltip("Разрешить проверку свайпа мышью в Unity Editor.")]
    private bool _storyCarouselSwipeWithMouse = true;

    [SerializeField]
    private StoryCardCarouselSwipeInput _storyCarouselSwipeInput;

    private void SetupStoryCarouselSwipe()
    {
        if (_gamesParent == null)
            return;

        if (_storyCarouselSwipeInput == null)
            _storyCarouselSwipeInput = _gamesParent.GetComponent<StoryCardCarouselSwipeInput>();
        if (_storyCarouselSwipeInput == null)
            _storyCarouselSwipeInput = _gamesParent.gameObject.AddComponent<StoryCardCarouselSwipeInput>();

        if (!_storyCarouselSwipeEnabled)
        {
            _storyCarouselSwipeInput.ClearCallbacks();
            _storyCarouselSwipeInput.enabled = false;
            return;
        }

        _storyCarouselSwipeInput.Configure(
            HandleStoryCarouselSwipedLeft,
            HandleStoryCarouselSwipedRight,
            _storyCarouselSwipeDistance,
            _storyCarouselSwipeHorizontalDominance,
            _storyCarouselSwipeWithMouse);
    }

    private void ReleaseStoryCarouselSwipe()
    {
        if (_storyCarouselSwipeInput != null)
            _storyCarouselSwipeInput.ClearCallbacks();
    }

    private void HandleStoryCarouselSwipedLeft()
    {
        if (CanHandleStoryCarouselSwipe())
            SelectNextStory();
    }

    private void HandleStoryCarouselSwipedRight()
    {
        if (CanHandleStoryCarouselSwipe())
            SelectPreviousStory();
    }

    private bool CanHandleStoryCarouselSwipe()
    {
        return _storyCarouselSwipeEnabled &&
               IsStoryCarouselEnabled() &&
               !_isStoryScreenOpen &&
               _storyLaunchState.IsIdle;
    }
}
