using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication/Password Recovery Screen Controller")]
public sealed partial class PasswordRecoveryScreenController : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private CanvasGroup _emailInputGroup;
    [SerializeField] private TMP_Text _successText;
    [SerializeField] private CanvasGroup _successGroup;
    [TextArea]
    [SerializeField] private string _successMessage =
        "Мы отправили новый пароль на Ваш e-mail";

    [Header("Action Button")]
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionButtonLabel;
    [SerializeField] private CanvasGroup _actionButtonLabelGroup;
    [SerializeField] private string _resetButtonText = "Сбросить пароль";
    [SerializeField] private string _returnButtonText = "На главную";
    [SerializeField] private UIButtonStateColorMode _buttonColorMode =
        UIButtonStateColorMode.ButtonColorTint;
    [SerializeField] private Color _readyColor = new Color32(190, 154, 123, 255);
    [SerializeField] private Color _disabledColor = new Color32(70, 70, 70, 255);

    [Header("Navigation")]
    [SerializeField] private Button _exitButton;
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _screenId = "PasswordRecoveryScreen";
    [SerializeField] private string _loginScreenId = "LoginScreen";

    private bool _completed;
    private bool _transitioning;

    private void OnEnable()
    {
        ResolveGroups();
        ConfigureInput();
        BindInput(true);
        BindButton(true);
        BindExitButton(true);
        UIScreenState.CurrentScreenChanged += HandleScreenChanged;
        ResetView();
    }

    private void OnDisable()
    {
        KillTransition();
        BindInput(false);
        BindButton(false);
        BindExitButton(false);
        UIScreenState.CurrentScreenChanged -= HandleScreenChanged;
    }

    private void OnValidate()
    {
        _screenId = UIScreenState.NormalizeScreenId(_screenId);
        _loginScreenId = UIScreenState.NormalizeScreenId(_loginScreenId);
        _transitionDuration = Mathf.Max(0f, _transitionDuration);
    }

    public void SubmitOrReturn()
    {
        if (_transitioning)
            return;
        if (_completed)
        {
            OpenLogin();
            return;
        }
        if (!RegistrationFormValidator.IsStrictEmail(_emailInput?.text))
            return;

        _emailInput.SetTextWithoutNotify(
            RegistrationFormValidator.NormalizeEmail(_emailInput.text));
        _completed = true;
        PlaySuccessTransition();
    }

    public void OpenLogin()
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(_loginScreenId))
            Debug.LogWarning($"Login screen '{_loginScreenId}' is unavailable.", this);
    }

    public void RefreshForm()
    {
        bool ready = _completed ||
            RegistrationFormValidator.IsStrictEmail(_emailInput?.text);
        UIButtonStateColor.Apply(
            _actionButton, ready, _readyColor, _disabledColor, _buttonColorMode);
    }

    public void ResetView()
    {
        KillTransition();
        ResolveGroups();
        _completed = false;
        _transitioning = false;
        if (_emailInput != null)
        {
            _emailInput.SetTextWithoutNotify("");
            _emailInput.interactable = true;
        }
        if (_successText != null)
            _successText.text = _successMessage;
        if (_actionButtonLabel != null)
            _actionButtonLabel.text = _resetButtonText;
        SetGroup(_emailInputGroup, 1f, true);
        SetGroup(_successGroup, 0f, false);
        SetGroup(_actionButtonLabelGroup, 1f, false);
        RefreshForm();
    }

    private void HandleScreenChanged(string screenId)
    {
        if (UIScreenState.NormalizeScreenId(screenId) == _screenId)
            ResetView();
    }

    private void ConfigureInput()
    {
        if (_emailInput == null)
            return;
        _emailInput.transition = Selectable.Transition.None;
        _emailInput.lineType = TMP_InputField.LineType.SingleLine;
        _emailInput.contentType = TMP_InputField.ContentType.EmailAddress;
        _emailInput.ForceLabelUpdate();
    }

    private void HandleEmailChanged(string value) => RefreshForm();

    private void BindInput(bool bind)
    {
        if (_emailInput == null)
            return;
        _emailInput.onValueChanged.RemoveListener(HandleEmailChanged);
        if (bind)
            _emailInput.onValueChanged.AddListener(HandleEmailChanged);
    }

    private void BindButton(bool bind)
    {
        if (_actionButton == null)
            return;
        UnityAction action = SubmitOrReturn;
        _actionButton.onClick.RemoveListener(action);
        if (bind)
            _actionButton.onClick.AddListener(action);
    }

    private void BindExitButton(bool bind)
    {
        if (_exitButton == null)
            return;
        UnityAction action = OpenLogin;
        _exitButton.onClick.RemoveListener(action);
        if (bind)
            _exitButton.onClick.AddListener(action);
    }
}
