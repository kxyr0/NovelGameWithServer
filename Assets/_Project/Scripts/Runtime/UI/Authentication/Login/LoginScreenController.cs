using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication/Login Screen Controller")]
public sealed partial class LoginScreenController : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private LoginFeedbackView _feedback;

    [Header("Buttons")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton;
    [SerializeField] private Button _passwordRecoveryButton;
    [SerializeField] private CanvasGroup _registerButtonGroup;

    [Header("Show Password")]
    [SerializeField] private Button _passwordVisibilityButton;
    [SerializeField] private CanvasGroup _passwordVisibilityGroup;
    [SerializeField] private Image _passwordVisibilityIcon;
    [SerializeField] private Sprite _hiddenPasswordSprite;
    [SerializeField] private Sprite _visiblePasswordSprite;

    [Header("Login Button Colors")]
    [SerializeField] private UIButtonStateColorMode _loginColorMode = UIButtonStateColorMode.ButtonColorTint;
    [SerializeField] private Color _readyColor = new Color32(205, 111, 48, 255);
    [SerializeField] private Color _disabledColor = new Color32(105, 105, 105, 255);

    [Header("Service")]
    [SerializeField] private MonoBehaviour _loginServiceSource;

    [Header("Navigation")]
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _loginScreenId = "LoginScreen";
    [SerializeField] private string _registerScreenId = "RegisterScreen";
    [SerializeField] private string _passwordRecoveryScreenId = "PasswordRecoveryScreen";
    [SerializeField] private string _mainScreenId = "MainScreen";

    private bool _busy;
    private bool _passwordVisible;
    private int _requestVersion;
    private ILoginService LoginService => _loginServiceSource as ILoginService;

    private void OnEnable()
    {
        BindUi();
        UIScreenState.CurrentScreenChanged += HandleScreenChanged;
        ConfigureUi();
        _busy = false;
        _requestVersion++;
        _feedback?.Clear();
        RefreshForm();
    }

    private void OnDisable()
    {
        StopRegisterFade();
        UnbindUi();
        UIScreenState.CurrentScreenChanged -= HandleScreenChanged;
        _requestVersion++;
        _busy = false;
    }

    private void OnValidate()
    {
        _loginScreenId = UIScreenState.NormalizeScreenId(_loginScreenId);
        _registerScreenId = UIScreenState.NormalizeScreenId(_registerScreenId);
        _passwordRecoveryScreenId = UIScreenState.NormalizeScreenId(_passwordRecoveryScreenId);
        _mainScreenId = UIScreenState.NormalizeScreenId(_mainScreenId);
    }

    public void SubmitLogin()
    {
        if (_busy || !HasEnteredCredentials())
            return;

        _feedback?.Clear();
        if (!RegistrationFormValidator.IsStrictEmail(_emailInput.text))
        {
            _feedback?.ShowInvalidCredentials();
            return;
        }

        ILoginService service = LoginService;
        if (service == null)
        {
            Debug.LogWarning("LoginScreenController: login service is unavailable.", this);
            RefreshForm();
            return;
        }

        SetBusy(true);
        int version = ++_requestVersion;
        service.Login(
            RegistrationFormValidator.NormalizeEmail(_emailInput.text),
            _passwordInput.text,
            result => CompleteLogin(version, result));
    }

    public void OpenRegister() => OpenScreen(_registerScreenId);
    public void OpenPasswordRecovery() => OpenScreen(_passwordRecoveryScreenId);

    private void CompleteLogin(int version, LoginResult result)
    {
        if (version != _requestVersion)
            return;

        _requestVersion++;
        SetBusy(false);
        if (result.Success)
        {
            _feedback?.Clear();
            string email = RegistrationFormValidator.NormalizeEmail(_emailInput.text);
            AccountLoginState.MarkSignedIn(email, NetworkManager.CurrentProfile?.playerId);
            OpenScreen(_mainScreenId);
            return;
        }

        if (result.FailureKind == LoginFailureKind.InvalidCredentials)
            _feedback?.ShowInvalidCredentials();
        else
            Debug.LogWarning("LoginScreenController: login service is unavailable.", this);
    }

    private void HandleScreenChanged(string screenId)
    {
        if (UIScreenState.NormalizeScreenId(screenId) == _loginScreenId)
        {
            _feedback?.Clear();
            RefreshForm();
            return;
        }
        if (!_busy)
            return;
        _requestVersion++;
        SetBusy(false);
    }

    private void OpenScreen(string screenId)
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        string targetId = UIScreenState.NormalizeScreenId(screenId);
        if (_screenNavigator != null && _screenNavigator.OpenScreen(targetId))
            return;
        Debug.LogWarning($"LoginScreenController: screen '{targetId}' is unavailable.", this);
    }
}
