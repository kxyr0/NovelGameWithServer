using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Registration Code Screen Controller")]
public sealed partial class RegistrationCodeScreenController : MonoBehaviour
{
    [Header("Code")]
    [SerializeField] private RegistrationCodeInputGroup _codeInputs;
    [SerializeField] private RegistrationScreenController _registrationScreen;

    [Header("Test Mode")]
    [SerializeField] private bool _useTestCode = true;
    [SerializeField] private string _testCode = "1111";

    [Header("Real Service")]
    [SerializeField] private MonoBehaviour _codeServiceSource;

    [Header("Resend")]
    [SerializeField] private Button _resendButton;
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField, Min(0f)] private float _resendDelay = 32f;
    [SerializeField] private string _countdownPrefix = "Повторная отправка через: ";
    [SerializeField] private string _resendReadyText = "Код можно отправить повторно";
    [SerializeField] private UIButtonStateColorMode _resendColorMode = UIButtonStateColorMode.ButtonColorTint;
    [SerializeField] private Color _resendReadyColor = new Color32(205, 111, 48, 255);
    [SerializeField] private Color _resendDisabledColor = new Color32(105, 105, 105, 255);

    [Header("Success")]
    [SerializeField, Min(0f)] private float _successExitDelay = 1f;
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _codeScreenId = "RegistrationCodeScreen";
    [SerializeField] private string _mainScreenId = "MainScreen";

    [Header("Navigation")]
    [SerializeField] private Button _exitButton;
    [SerializeField] private string _registerScreenId = "RegisterScreen";

    private float _remainingSeconds;
    private bool _sessionActive;
    private bool _verificationInProgress;
    private bool _resendInProgress;
    private Coroutine _successRoutine;

    private IRegistrationCodeService RealService =>
        _codeServiceSource as IRegistrationCodeService;
    private string Email => _registrationScreen != null ? _registrationScreen.Email : "";

    private void OnEnable()
    {
        if (_codeInputs != null)
            _codeInputs.CodeCompleted += VerifyCode;
        BindButton(_resendButton, RequestResend);
        BindButton(_exitButton, OpenRegisterScreen);
        UIScreenState.CurrentScreenChanged += HandleScreenChanged;
        HandleScreenChanged(UIScreenState.CurrentScreenId);
    }

    private void OnDisable()
    {
        if (_codeInputs != null)
            _codeInputs.CodeCompleted -= VerifyCode;
        UnbindButton(_resendButton, RequestResend);
        UnbindButton(_exitButton, OpenRegisterScreen);
        UIScreenState.CurrentScreenChanged -= HandleScreenChanged;
        StopSuccessRoutine();
        _sessionActive = false;
    }

    private void OnValidate()
    {
        _resendDelay = Mathf.Max(0f, _resendDelay);
        _successExitDelay = Mathf.Max(0f, _successExitDelay);
        _testCode ??= "";
        _codeScreenId = UIScreenState.NormalizeScreenId(_codeScreenId);
        _mainScreenId = UIScreenState.NormalizeScreenId(_mainScreenId);
        _registerScreenId = UIScreenState.NormalizeScreenId(_registerScreenId);
    }

    private void Update()
    {
        if (!_sessionActive || _remainingSeconds <= 0f)
            return;
        _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.unscaledDeltaTime);
        RefreshResendUi();
    }

    public void VerifyCode(string code)
    {
        if (!_sessionActive || _verificationInProgress || code.Length != 4)
            return;
        _verificationInProgress = true;
        _codeInputs?.SetInteractable(false);

        if (_useTestCode)
        {
            CompleteVerification(new RegistrationCodeResult(code == _testCode));
            return;
        }

        IRegistrationCodeService service = RealService;
        if (service == null)
        {
            FailUnavailableService();
            return;
        }
        service.VerifyCode(Email, code, CompleteVerification);
    }

    public void VerifyCurrentCode() => VerifyCode(_codeInputs != null ? _codeInputs.Code : "");

    public void OpenRegisterScreen()
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(_registerScreenId))
            Debug.LogWarning($"Register screen '{_registerScreenId}' is unavailable.", this);
    }

    public void RequestResend()
    {
        if (!_sessionActive || _remainingSeconds > 0f || _resendInProgress)
            return;
        _resendInProgress = true;
        RefreshResendUi();

        if (_useTestCode)
        {
            CompleteResend(true, "");
            return;
        }
        IRegistrationCodeService service = RealService;
        if (service == null)
        {
            _resendInProgress = false;
            RefreshResendUi();
            Debug.LogWarning("Registration code service is not assigned.", this);
            return;
        }
        service.ResendCode(Email, CompleteResend);
    }
}
