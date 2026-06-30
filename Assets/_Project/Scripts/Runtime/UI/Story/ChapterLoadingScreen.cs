using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChapterLoadingScreen : MonoBehaviour
{
    public static ChapterLoadingScreen Instance { get; private set; }

    [Header("References")]
    [SerializeField] GameObject loadingPanel;
    [SerializeField] TMP_Text progressText;
    [SerializeField] Image progressBar;
    [SerializeField] TMP_Text chapterTitleText;

    [Header("Timing")]
    [SerializeField] float minDuration = 1.5f;
    [SerializeField] float maxDuration = 3.0f;
    [SerializeField] bool useUnscaledTime = true;

    public bool IsVisible => loadingPanel != null && loadingPanel.activeSelf;

    Coroutine _loadRoutine;

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

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    void OnValidate()
    {
        minDuration = Mathf.Max(0f, minDuration);
        maxDuration = Mathf.Max(minDuration, maxDuration);
    }

    void OnDestroy()
    {
        StopLoadRoutine();

        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        HideImmediate();
    }

    public void Show(string chapterName, Action onComplete)
    {
        if (loadingPanel == null || !isActiveAndEnabled)
        {
            SafeInvoke(onComplete);
            return;
        }

        StopLoadRoutine();

        _loadRoutine = StartCoroutine(LoadRoutine(chapterName, onComplete));
    }

    public void HideImmediate()
    {
        StopLoadRoutine();

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    IEnumerator LoadRoutine(string chapterName, Action onComplete)
    {
        if (progressBar != null)
            progressBar.fillAmount = 0f;

        if (progressText != null)
            progressText.text = "0%";

        if (chapterTitleText != null)
            chapterTitleText.text = string.IsNullOrEmpty(chapterName) ? "" : chapterName;

        loadingPanel.SetActive(true);

        float duration = UnityEngine.Random.Range(minDuration, maxDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(duration <= 0f ? 1f : elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            int percent = Mathf.RoundToInt(eased * 100f);

            if (progressText != null)
                progressText.text = $"{percent}%";

            if (progressBar != null)
                progressBar.fillAmount = eased;

            yield return null;
        }

        if (progressText != null)
            progressText.text = "100%";

        if (progressBar != null)
            progressBar.fillAmount = 1f;

        yield return Wait(0.2f);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        _loadRoutine = null;
        SafeInvoke(onComplete);
    }

    object Wait(float seconds)
    {
        return useUnscaledTime
            ? new WaitForSecondsRealtime(seconds)
            : new WaitForSeconds(seconds);
    }

    void StopLoadRoutine()
    {
        if (_loadRoutine == null)
            return;

        StopCoroutine(_loadRoutine);
        _loadRoutine = null;
    }

    void SafeInvoke(Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ChapterLoadingScreen: completion callback failed: {exception.Message}", this);
        }
    }
}
