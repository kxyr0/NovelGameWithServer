using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    [Header("References")]
    public Image logoImage;
    public TMP_Text titleText;
    public Image loadingBar;
    public TMP_Text loadingText;
    public Image backgroundImage;
    public TMP_Text versionText;

    [Header("Settings")]
    public string menuSceneName = "Game";
    public int menuSceneIndex = 1;
    public float minDuration = 2.0f;
    public bool showAlways = true;

    const string SplashShownKey = "SplashShown";
    const string LoadingPrefix = "\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430";
    const string ReadyText = "\u0413\u043e\u0442\u043e\u0432\u043e!";

    Coroutine _splashRoutine;
    Tween _logoTween;
    Tween _titleTween;

    void OnValidate()
    {
        minDuration = Mathf.Max(0f, minDuration);
        menuSceneIndex = Mathf.Max(0, menuSceneIndex);
    }

    void Start()
    {
        if (versionText != null)
            versionText.text = $"v{Application.version}";

        if (!showAlways && SafeGetSplashShown())
        {
            LoadMenu();
            return;
        }

        _splashRoutine = StartCoroutine(RunSplash());
    }

    void OnDestroy()
    {
        if (_splashRoutine != null)
        {
            StopCoroutine(_splashRoutine);
            _splashRoutine = null;
        }

        _logoTween?.Kill();
        _titleTween?.Kill();
    }

    IEnumerator RunSplash()
    {
        if (logoImage != null)
            logoImage.color = new Color(1f, 1f, 1f, 0f);

        if (titleText != null)
            titleText.alpha = 0f;

        if (loadingBar != null)
            loadingBar.fillAmount = 0f;

        yield return new WaitForSeconds(0.3f);

        if (logoImage != null)
            _logoTween = logoImage.DOFade(1f, 0.6f);

        if (titleText != null)
            _titleTween = titleText.DOFade(1f, 0.8f).SetDelay(0.3f);

        yield return new WaitForSeconds(0.8f);

        float progressDuration = Mathf.Max(0.01f, minDuration - 0.8f);
        float elapsed = 0f;

        while (elapsed < progressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / progressDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            if (loadingBar != null)
                loadingBar.fillAmount = eased;

            if (loadingText != null)
                loadingText.text = $"{LoadingPrefix}... {Mathf.RoundToInt(eased * 100f)}%";

            yield return null;
        }

        if (loadingBar != null)
            loadingBar.fillAmount = 1f;

        if (loadingText != null)
            loadingText.text = ReadyText;

        yield return new WaitForSeconds(0.4f);

        SafeSetSplashShown();
        _splashRoutine = null;
        LoadMenu();
    }

    void LoadMenu()
    {
        try
        {
            if (!string.IsNullOrEmpty(menuSceneName))
                SceneManager.LoadScene(menuSceneName);
            else
                SceneManager.LoadScene(menuSceneIndex);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SplashScreen: failed to load menu scene: {exception.Message}", this);
        }
    }

    static bool SafeGetSplashShown()
    {
        try
        {
            return PlayerPrefs.GetInt(SplashShownKey, 0) == 1;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("SplashScreen: failed to read SplashShown: " + exception.Message);
            return false;
        }
    }

    static void SafeSetSplashShown()
    {
        try
        {
            PlayerPrefs.SetInt(SplashShownKey, 1);
            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("SplashScreen: failed to save SplashShown: " + exception.Message);
        }
    }
}
