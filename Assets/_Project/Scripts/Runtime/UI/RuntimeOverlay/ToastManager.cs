using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] CanvasGroup toastPanel;
    [SerializeField] TMP_Text toastText;

    [Header("Timing")]
    [SerializeField] float displayDuration = 2.5f;
    [SerializeField] float fadeDuration = 0.3f;

    public bool IsVisible => toastPanel != null && toastPanel.gameObject.activeSelf;

    Coroutine _currentToast;
    Tween _fadeTween;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (toastPanel != null)
        {
            toastPanel.alpha = 0f;
            toastPanel.gameObject.SetActive(false);
        }
    }

    void OnValidate()
    {
        displayDuration = Mathf.Max(0f, displayDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
    }

    void OnDisable()
    {
        StopActiveToast();
    }

    void OnDestroy()
    {
        StopActiveToast();

        if (Instance == this)
            Instance = null;
    }

    public void Show(string message)
    {
        if (toastPanel == null)
        {
            Debug.Log(message ?? "");
            return;
        }

        StopActiveToast();
        _currentToast = StartCoroutine(ShowRoutine(message ?? ""));
    }

    public void ShowStat(string statName, int delta)
    {
        string sign = delta >= 0 ? "+" : "";
        Show($"{statName} {sign}{delta}");
    }

    public void ShowSystemMessage(string message)
    {
        Show(message);
    }

    IEnumerator ShowRoutine(string message)
    {
        if (toastText != null)
            toastText.text = message;

        toastPanel.alpha = 0f;
        toastPanel.gameObject.SetActive(true);

        _fadeTween = toastPanel.DOFade(1f, fadeDuration);
        yield return _fadeTween.WaitForCompletion();

        yield return new WaitForSeconds(displayDuration);

        _fadeTween = toastPanel.DOFade(0f, fadeDuration)
            .OnComplete(() =>
            {
                if (toastPanel != null)
                    toastPanel.gameObject.SetActive(false);
            });
        yield return _fadeTween.WaitForCompletion();

        _fadeTween = null;
        _currentToast = null;
    }

    void StopActiveToast()
    {
        if (_currentToast != null)
        {
            StopCoroutine(_currentToast);
            _currentToast = null;
        }

        _fadeTween?.Kill();
        _fadeTween = null;

        if (toastPanel != null)
        {
            toastPanel.alpha = 0f;
            toastPanel.gameObject.SetActive(false);
        }
    }
}
