using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_InputField))]
[AddComponentMenu("Nocturne/UI/Input/Placeholder Focus Visibility")]
public sealed class TMPInputPlaceholderFocusVisibility : MonoBehaviour,
    ISelectHandler,
    IDeselectHandler
{
    private TMP_InputField _input;
    private CanvasGroup _placeholderGroup;
    private bool _selected;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (_input != null)
        {
            _input.onValueChanged.RemoveListener(HandleValueChanged);
            _input.onValueChanged.AddListener(HandleValueChanged);
            _selected = _input.isFocused;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (_input != null)
            _input.onValueChanged.RemoveListener(HandleValueChanged);
        _selected = false;
    }

    private void LateUpdate()
    {
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        SetPlaceholderVisible(false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();
        bool hasText = _input != null && !string.IsNullOrEmpty(_input.text);
        bool focused = _selected || (_input != null && _input.isFocused);
        SetPlaceholderVisible(!focused && !hasText);
    }

    private void HandleValueChanged(string value)
    {
        Refresh();
    }

    private void ResolveReferences()
    {
        if (_input == null)
            _input = GetComponent<TMP_InputField>();
        if (_input == null || _input.placeholder == null)
            return;

        GameObject placeholderObject = _input.placeholder.gameObject;
        if (_placeholderGroup == null ||
            _placeholderGroup.gameObject != placeholderObject)
        {
            _placeholderGroup = placeholderObject.GetComponent<CanvasGroup>();
            if (_placeholderGroup == null)
                _placeholderGroup = placeholderObject.AddComponent<CanvasGroup>();
            _placeholderGroup.interactable = false;
            _placeholderGroup.blocksRaycasts = false;
        }
    }

    private void SetPlaceholderVisible(bool visible)
    {
        if (_placeholderGroup != null)
            _placeholderGroup.alpha = visible ? 1f : 0f;
    }
}

public static class TMPInputPlaceholderFocusInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InstallInLoadedScenes();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInLoadedScenes();
    }

    private static void InstallInLoadedScenes()
    {
        TMP_InputField[] inputs = Object.FindObjectsOfType<TMP_InputField>(true);
        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField input = inputs[i];
            if (input != null &&
                input.GetComponent<TMPInputPlaceholderFocusVisibility>() == null)
                input.gameObject.AddComponent<TMPInputPlaceholderFocusVisibility>();
        }
    }
}
