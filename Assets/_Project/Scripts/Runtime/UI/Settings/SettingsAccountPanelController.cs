using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/Account Panel Controller")]
public sealed class SettingsAccountPanelController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] TMP_Text _emailText;
    [SerializeField] TMP_Text _playerIdText;
    [SerializeField] string _playerIdPrefix = "ID: ";

    [Header("Guest Button")]
    [SerializeField] Button _authButton;
    [SerializeField] CanvasGroup _authButtonGroup;
    [SerializeField] TMP_Text _authButtonLabel;
    [SerializeField] string _authButtonText = "Войти";

    [Header("Registered Button")]
    [SerializeField] Button _signOutButton;
    [SerializeField] CanvasGroup _signOutButtonGroup;
    [SerializeField] TMP_Text _signOutButtonLabel;
    [SerializeField] string _signOutButtonText = "Выйти из учетной записи";

    [Header("Navigation")]
    [SerializeField] StoryScreenNavigator _screenNavigator;
    [SerializeField] string _loginScreenId = "LoginScreen";

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        Bind(_authButton, OpenLogin);
        Bind(_signOutButton, SignOut);
        AccountLoginState.Changed -= Refresh;
        AccountLoginState.Changed += Refresh;
        NetworkManager.OnProfileUpdated -= Refresh;
        NetworkManager.OnProfileUpdated += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Unbind(_authButton, OpenLogin);
        Unbind(_signOutButton, SignOut);
        AccountLoginState.Changed -= Refresh;
        NetworkManager.OnProfileUpdated -= Refresh;
    }

    void OnValidate()
    {
        _loginScreenId = UIScreenState.NormalizeScreenId(_loginScreenId);
        ResolveReferences();
    }

    public void Refresh()
    {
        bool registered = AccountLoginState.HasAccountIdentity;
        string email = registered ? AccountLoginState.Email : "";
        string id = registered ? ResolvePlayerId() : "";

        ApplyText(_emailText, email, registered);
        ApplyText(_playerIdText, (_playerIdPrefix ?? "") + id, registered);
        if (_authButtonLabel != null)
            _authButtonLabel.text = _authButtonText;
        if (_signOutButtonLabel != null)
            _signOutButtonLabel.text = _signOutButtonText;

        SetGroup(_authButtonGroup, !registered);
        SetGroup(_signOutButtonGroup, registered);
    }

    public void OpenLogin()
    {
        OpenScreen(_loginScreenId);
    }

    public void SignOut()
    {
        AccountLoginState.SignOut();
        OpenLogin();
    }

    string ResolvePlayerId()
    {
        string id = PlayerPublicIdFormatter.FormatServerIdOrEmpty(
            NetworkManager.CurrentProfile?.playerId);
        return id.Length > 0 ? id : "…";
    }

    void OpenScreen(string screenId)
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        string target = UIScreenState.NormalizeScreenId(screenId);
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(target))
            Debug.LogWarning($"Settings account screen '{target}' is unavailable.", this);
    }

    void ResolveReferences()
    {
        if (_emailText == null)
            _emailText = transform.Find("emailText")?.GetComponent<TMP_Text>();
        if (_playerIdText == null)
            _playerIdText = transform.Find("IDText")?.GetComponent<TMP_Text>();
        if (_authButtonGroup == null)
            _authButtonGroup = transform.Find("AuthButton")?.GetComponent<CanvasGroup>();
        if (_signOutButtonGroup == null)
            _signOutButtonGroup = transform.Find("SignOutButton")?.GetComponent<CanvasGroup>();
        ResolveButton(ref _authButton, ref _authButtonGroup, ref _authButtonLabel, "AuthButton");
        ResolveButton(ref _signOutButton, ref _signOutButtonGroup, ref _signOutButtonLabel, "SignOutButton");
    }

    static void ResolveButton(ref Button button, ref CanvasGroup group, ref TMP_Text label, string rootName)
    {
        if (group == null && button != null)
        {
            Transform current = button.transform;
            while (current != null && current.name != rootName)
                current = current.parent;
            if (current != null)
                group = current.GetComponent<CanvasGroup>();
        }
        if (button == null && group != null)
            button = group.GetComponentInChildren<Button>(true);
        if (label == null && button != null)
            label = button.GetComponentInChildren<TMP_Text>(true);
    }

    static void ApplyText(TMP_Text text, string value, bool visible)
    {
        if (text == null)
            return;
        text.text = visible ? value : "";
        text.enabled = visible;
    }

    static void SetGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
