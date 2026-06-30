using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameInputUI : MonoBehaviour
{
    public static PlayerNameInputUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text placeholderText;

    [Header("Settings")]
    [SerializeField] private int maxNameLength = 20;
    [SerializeField] private string defaultName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";
    [SerializeField] private string placeholder = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0438\u043c\u044f \u043f\u0435\u0440\u0441\u043e\u043d\u0430\u0436\u0430";

    [Header("Visibility")]
    [SerializeField] private List<GameObject> hideWhileOpen = new List<GameObject>();

    public bool IsVisible => panel != null && panel.activeSelf;
    public int MaxNameLength => maxNameLength;
    public string DefaultName => defaultName;

    System.Action _onConfirm;
    readonly List<HiddenObjectState> hiddenObjectStates = new List<HiddenObjectState>();

    sealed class HiddenObjectState
    {
        public GameObject Target;
        public bool WasActiveSelf;
        public CanvasGroup CanvasGroup;
        public bool HadCanvasGroup;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
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

        if (panel != null)
            panel.SetActive(false);
    }

    private void OnValidate()
    {
        maxNameLength = Mathf.Clamp(maxNameLength, 1, 64);
        if (string.IsNullOrWhiteSpace(defaultName))
            defaultName = "\u0413\u0435\u0440\u043e\u0438\u043d\u044f";
        if (string.IsNullOrWhiteSpace(placeholder))
            placeholder = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0438\u043c\u044f \u043f\u0435\u0440\u0441\u043e\u043d\u0430\u0436\u0430";
        if (hideWhileOpen == null)
            hideWhileOpen = new List<GameObject>();
    }

    private void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxNameLength;
            if (placeholderText != null)
                placeholderText.text = placeholder;
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirm);

        if (Instance == this)
            Instance = null;
    }

    public void Show(System.Action onConfirm = null, bool forceShow = false, string suggestedName = null)
    {
        if (!forceShow && SafeHasPlayerName())
        {
            SafeInvoke(onConfirm);
            return;
        }

        _onConfirm = onConfirm;

        if (nameInputField != null)
        {
            string suggested = !string.IsNullOrWhiteSpace(suggestedName) ? NormalizeName(suggestedName) : "";
            nameInputField.characterLimit = maxNameLength;
            if (placeholderText != null)
                placeholderText.text = placeholder;
            nameInputField.SetTextWithoutNotify(suggested);
            nameInputField.selectionAnchorPosition = 0;
            nameInputField.selectionFocusPosition = suggested.Length;
        }

        if (panel != null)
        {
            panel.SetActive(true);
            HideObjectsWhileOpen();
        }
        else
        {
            OnConfirm();
        }
    }

    private void OnConfirm()
    {
        string input = nameInputField != null ? nameInputField.text : null;
        input = NormalizeName(input);

        try
        {
            string storyId = ResolveActiveStoryId();
            input = CharacterProfileService.SaveSelectedPlayerName(
                input,
                storyId,
                nameof(PlayerNameInputUI));

            if (NetworkManager.Instance != null)
                NetworkManager.Instance.SetHeroNameAsync(input, storyId: storyId);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"PlayerNameInputUI: failed to save player name: {exception.Message}", this);
            CharacterProfileService.SaveSelectedPlayerName(input, ResolveActiveStoryId(), nameof(PlayerNameInputUI) + ".fallback");
        }

        if (panel != null)
            panel.SetActive(false);
        RestoreObjectsHiddenWhileOpen();

        var callback = _onConfirm;
        _onConfirm = null;
        SafeInvoke(callback);
    }

    void HideObjectsWhileOpen()
    {
        if (hideWhileOpen == null || hideWhileOpen.Count == 0 || hiddenObjectStates.Count > 0)
            return;

        foreach (GameObject target in hideWhileOpen)
        {
            if (target == null)
                continue;

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            bool hadCanvasGroup = canvasGroup != null;
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();

            hiddenObjectStates.Add(new HiddenObjectState
            {
                Target = target,
                WasActiveSelf = target.activeSelf,
                CanvasGroup = canvasGroup,
                HadCanvasGroup = hadCanvasGroup,
                Alpha = canvasGroup.alpha,
                Interactable = canvasGroup.interactable,
                BlocksRaycasts = canvasGroup.blocksRaycasts
            });

            if (!target.activeSelf)
                target.SetActive(true);

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void RestoreObjectsHiddenWhileOpen()
    {
        if (hiddenObjectStates.Count == 0)
            return;

        for (int i = 0; i < hiddenObjectStates.Count; i++)
        {
            HiddenObjectState state = hiddenObjectStates[i];
            if (state == null || state.Target == null)
                continue;

            if (state.CanvasGroup != null)
            {
                state.CanvasGroup.alpha = state.Alpha;
                state.CanvasGroup.interactable = state.Interactable;
                state.CanvasGroup.blocksRaycasts = state.BlocksRaycasts;

                if (!state.HadCanvasGroup)
                    Destroy(state.CanvasGroup);
            }

            if (state.Target.activeSelf != state.WasActiveSelf)
                state.Target.SetActive(state.WasActiveSelf);
        }

        hiddenObjectStates.Clear();
    }

    string NormalizeName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? defaultName : value.Trim();
        int limit = Mathf.Clamp(maxNameLength, 1, 64);

        if (normalized.Length > limit)
            normalized = normalized.Substring(0, limit);

        return normalized;
    }

    static string ResolveActiveStoryId()
    {
        if (StoryManager.Instance != null && !string.IsNullOrWhiteSpace(StoryManager.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(StoryManager.Instance.CurrentStoryId);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(GameState.Instance.CurrentStoryId))
            return SaveDataSanitizer.SanitizeIdentifier(GameState.Instance.CurrentStoryId);

        return "";
    }

    static bool SafeHasPlayerName()
    {
        try
        {
            return HeroCustomizationStore.HasStoredPlayerName();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("PlayerNameInputUI: failed to read name flag: " + exception.Message);
            return false;
        }
    }

    static void SafeInvoke(System.Action callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("PlayerNameInputUI: confirm callback failed: " + exception.Message);
        }
    }
}
