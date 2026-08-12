using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Registration Screen Controller")]
public sealed class RegistrationScreenController : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("Consent Text")]
    [SerializeField] private Toggle _termsToggle;
    [SerializeField] private TMP_Text _termsText;
    [SerializeField] private string _termsPrefix = "Я принимаю условия ";
    [SerializeField] private string _privacyPolicyLabel = "Политики обработки персональных данных";
    [SerializeField] private string _termsConnector = " и ";
    [SerializeField] private string _userAgreementLabel = "Пользовательского соглашения";
    [SerializeField] private Color _sandColor = new Color32(179, 143, 111, 255);
    [SerializeField] private bool _underlinePolicyLinks = true;

    [Header("Get Code Button")]
    [SerializeField] private Button _getCodeButton;
    [SerializeField] private UIButtonStateColorMode _buttonColorMode = UIButtonStateColorMode.ButtonColorTint;
    [SerializeField] private Color _readyButtonColor = new Color32(205, 111, 48, 255);
    [SerializeField] private Color _disabledButtonColor = new Color32(105, 105, 105, 255);

    [Header("Navigation")]
    [SerializeField] private Button _exitButton;
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _loginScreenId = "LoginScreen";
    [SerializeField] private string _codeScreenId = "RegistrationCodeScreen";

    [Header("Events")]
    [SerializeField] private UnityEvent _requestCodeReady = new UnityEvent();

    public string Username => RegistrationFormValidator.NormalizeUsername(_usernameInput != null ? _usernameInput.text : "");
    public string Email => RegistrationFormValidator.NormalizeEmail(_emailInput != null ? _emailInput.text : "");
    public string Password => _passwordInput != null ? _passwordInput.text : "";
    public bool IsReady { get; private set; }

    private void OnEnable()
    {
        BindInput(_usernameInput);
        BindInput(_emailInput);
        BindInput(_passwordInput);
        if (_termsToggle != null)
            _termsToggle.onValueChanged.AddListener(HandleTermsChanged);
        BindButton(_exitButton, OpenLoginScreen);
        BindButton(_getCodeButton, RequestCode);
        ConfigureInputFields();
        ApplyTermsText();
        RefreshValidation();
    }

    private void OnDisable()
    {
        UnbindInput(_usernameInput);
        UnbindInput(_emailInput);
        UnbindInput(_passwordInput);
        if (_termsToggle != null)
            _termsToggle.onValueChanged.RemoveListener(HandleTermsChanged);
        UnbindButton(_exitButton, OpenLoginScreen);
        UnbindButton(_getCodeButton, RequestCode);
    }

    private void OnValidate()
    {
        _loginScreenId = UIScreenState.NormalizeScreenId(_loginScreenId);
        _codeScreenId = UIScreenState.NormalizeScreenId(_codeScreenId);
    }

    public void RefreshValidation()
    {
        bool emailValid = RegistrationFormValidator.IsStrictEmail(_emailInput != null ? _emailInput.text : "");
        IsReady = RegistrationFormValidator.IsUsernameReady(Username) &&
                  emailValid && Password.Length > 0 &&
                  _termsToggle != null && _termsToggle.isOn;

        ApplyButtonState(IsReady);
    }

    public void OpenLoginScreen() => OpenScreen(_loginScreenId);

    public void RequestCode()
    {
        RefreshValidation();
        if (!IsReady)
            return;

        _usernameInput?.SetTextWithoutNotify(Username);
        _emailInput?.SetTextWithoutNotify(Email);
        _requestCodeReady.Invoke();
        OpenScreen(_codeScreenId);
    }

    public void ApplyTermsText()
    {
        if (_termsText == null)
            return;

        _termsText.richText = true;
        _termsText.text = (_termsPrefix ?? "") + StylePolicyText(_privacyPolicyLabel) +
                          (_termsConnector ?? "") + StylePolicyText(_userAgreementLabel);
    }

    private void ConfigureInputFields()
    {
        ConfigureField(_usernameInput, TMP_InputField.ContentType.Standard);
        ConfigureField(_emailInput, TMP_InputField.ContentType.EmailAddress);
        ConfigureField(_passwordInput, TMP_InputField.ContentType.Password);
    }

    private static void ConfigureField(TMP_InputField field, TMP_InputField.ContentType type)
    {
        if (field == null)
            return;
        field.transition = Selectable.Transition.None;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.contentType = type;
        field.ForceLabelUpdate();
    }

    private void ApplyButtonState(bool ready)
    {
        UIButtonStateColor.Apply(_getCodeButton, ready, _readyButtonColor,
            _disabledButtonColor, _buttonColorMode);
    }

    private string StylePolicyText(string value)
    {
        string color = ColorUtility.ToHtmlStringRGB(_sandColor);
        string result = $"<color=#{color}>{value ?? ""}</color>";
        return _underlinePolicyLinks ? $"<u>{result}</u>" : result;
    }

    private void OpenScreen(string screenId)
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        string targetId = UIScreenState.NormalizeScreenId(screenId);
        if (_screenNavigator != null && _screenNavigator.OpenScreen(targetId))
            return;
        Debug.LogWarning($"RegistrationScreenController: screen '{targetId}' is unavailable.", this);
    }

    private void HandleInputChanged(string value) => RefreshValidation();
    private void HandleTermsChanged(bool value) => RefreshValidation();
    private void BindInput(TMP_InputField input)
    {
        if (input != null)
            input.onValueChanged.AddListener(HandleInputChanged);
    }

    private void UnbindInput(TMP_InputField input)
    {
        if (input != null)
            input.onValueChanged.RemoveListener(HandleInputChanged);
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
