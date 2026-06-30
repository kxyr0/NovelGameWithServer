using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Novel Template/UI/Story Black Screen Transition")]
public sealed class StoryBlackScreenTransition : MonoBehaviour
{
    public static StoryBlackScreenTransition Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject blackScreen;
    [SerializeField] private CanvasGroup blackScreenCanvasGroup;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.45f;
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private Ease fadeInEase = Ease.OutSine;
    [SerializeField] private Ease fadeOutEase = Ease.InSine;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool hideWhenTransparent = true;
    [SerializeField] private bool startTransparent = true;

    Tween fadeTween;

    public bool IsTransitioning => fadeTween != null && fadeTween.IsActive();

    void Reset()
    {
        blackScreen = gameObject;
        blackScreenCanvasGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;

        EnsureReferences();

        if (startTransparent)
            SetTransparentImmediate();
    }

    void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    void OnDestroy()
    {
        fadeTween?.Kill(false);

        if (Instance == this)
            Instance = null;
    }

    public void PrepareStoryEnter()
    {
        SetBlackImmediate();
    }

    public void PlayStoryEnter(Action onComplete = null)
    {
        FadeFromBlack(onComplete);
    }

    public void FadeIn(Action onComplete = null)
    {
        FadeFromBlack(onComplete);
    }

    public void FadeOut(Action onComplete = null)
    {
        FadeToBlack(onComplete);
    }

    public void FadeToBlack(Action onComplete = null)
    {
        FadeTo(1f, fadeOutDuration, fadeOutEase, onComplete);
    }

    public void FadeFromBlack(Action onComplete = null)
    {
        FadeTo(0f, fadeInDuration, fadeInEase, onComplete);
    }

    public void AssignBlackScreen(GameObject target)
    {
        if (target == null)
            return;

        blackScreen = target;
        blackScreenCanvasGroup = blackScreen.GetComponent<CanvasGroup>();
        EnsureReferences();
    }

    public void SetBlackImmediate()
    {
        if (!EnsureReferences())
            return;

        fadeTween?.Kill(false);
        SetAlphaImmediate(1f);
    }

    public void SetTransparentImmediate()
    {
        if (!EnsureReferences())
            return;

        fadeTween?.Kill(false);
        SetAlphaImmediate(0f);
    }

    void FadeTo(float targetAlpha, float duration, Ease ease, Action onComplete)
    {
        if (!EnsureReferences())
        {
            SafeInvoke(onComplete);
            return;
        }

        fadeTween?.Kill(false);
        ActivateBlackScreen();
        SetInteraction(true);

        if (duration <= 0f)
        {
            SetAlphaImmediate(targetAlpha);
            SafeInvoke(onComplete);
            return;
        }

        fadeTween = blackScreenCanvasGroup
            .DOFade(targetAlpha, duration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() =>
            {
                SetAlphaImmediate(targetAlpha);
                SafeInvoke(onComplete);
            })
            .OnKill(() => fadeTween = null);
    }

    bool EnsureReferences()
    {
        if (blackScreen == null)
            blackScreen = gameObject;

        if (blackScreen == null)
        {
            Debug.LogWarning("StoryBlackScreenTransition: blackScreen is not assigned.", this);
            return false;
        }

        if (blackScreenCanvasGroup == null)
            blackScreenCanvasGroup = blackScreen.GetComponent<CanvasGroup>();

        if (blackScreenCanvasGroup == null)
            blackScreenCanvasGroup = blackScreen.AddComponent<CanvasGroup>();

        return blackScreenCanvasGroup != null;
    }

    void ActivateBlackScreen()
    {
        if (blackScreen != null && !blackScreen.activeSelf)
            blackScreen.SetActive(true);
    }

    void SetAlphaImmediate(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        ActivateBlackScreen();

        blackScreenCanvasGroup.alpha = alpha;
        SetInteraction(alpha > 0.001f);

        if (hideWhenTransparent && alpha <= 0.001f && blackScreen != null)
            blackScreen.SetActive(false);
    }

    void SetInteraction(bool blocksInput)
    {
        if (blackScreenCanvasGroup == null)
            return;

        blackScreenCanvasGroup.interactable = blocksInput;
        blackScreenCanvasGroup.blocksRaycasts = blocksInput;
    }

    void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StoryBlackScreenTransition: completion callback failed: {exception.Message}", this);
        }
    }
}
