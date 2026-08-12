using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Edit Profile Controller")]
public sealed partial class EditProfileController : MonoBehaviour
{
    private const string DefaultName = "Гость";

    [Header("Input")]
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private Image _inputImage;
    [SerializeField] private TMP_Text _nameCharacterText;
    [SerializeField] private TMP_Text _placeholderText;
    [SerializeField] private string _placeholderMessage = "Введите имя пользователя";
    [SerializeField] private string _fallbackName = DefaultName;

    [Header("Buttons")]
    [SerializeField] private Button _editButton;
    [SerializeField] private Button _acceptButton;

    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup _editButtonGroup;
    [SerializeField] private CanvasGroup _acceptButtonGroup;
    [SerializeField] private CanvasGroup _inputRaycastGroup;
    [SerializeField] private CanvasGroup _inputTextGroup;
    [SerializeField] private CanvasGroup _placeholderGroup;
    [SerializeField] private CanvasGroup _nameCharacterGroup;
    [SerializeField] private CanvasGroup _outlineGroup;

    private bool _isEditing;

    private void OnEnable()
    {
        BindButtons();
        if (_nameInputField != null)
            _nameInputField.onValueChanged.AddListener(HandleInputChanged);
        NetworkManager.OnProfileUpdated += RefreshName;
        if (_placeholderText != null)
            _placeholderText.text = _placeholderMessage;
        InitializeAvatarSelection();
        RefreshName();
        ShowViewingMode();
    }

    private void OnDisable()
    {
        NetworkManager.OnProfileUpdated -= RefreshName;
        if (_nameInputField != null)
            _nameInputField.onValueChanged.RemoveListener(HandleInputChanged);
        ShutdownAvatarSelection();
        UnbindButtons();
    }

    public void BeginEditing()
    {
        _isEditing = true;
        if (_nameInputField != null)
        {
            string currentName = ResolveCurrentName();
            _nameInputField.SetTextWithoutNotify(currentName == _fallbackName ? "" : currentName);
            _nameInputField.interactable = true;
            _nameInputField.readOnly = false;
        }
        ApplyMode();
        _nameInputField?.Select();
        _nameInputField?.ActivateInputField();
    }

    public void AcceptName()
    {
        string safeName = SaveDataSanitizer.SanitizePlayerName(
            _nameInputField != null ? _nameInputField.text : "");
        if (!NetworkManager.SetLocalProfileDisplayName(safeName))
        {
            ToastManager.Instance?.ShowSystemMessage(_placeholderMessage);
            HandleInputChanged("");
            _nameInputField?.ActivateInputField();
            return;
        }

        RefreshName();
        ShowViewingMode();
    }

    public void CancelEditing()
    {
        ShowViewingMode();
    }

    public void RefreshName()
    {
        string currentName = ResolveCurrentName();
        if (_nameCharacterText != null)
            _nameCharacterText.text = currentName;
        if (!_isEditing && _nameInputField != null)
            _nameInputField.SetTextWithoutNotify(currentName);
    }

    private void ShowViewingMode()
    {
        _isEditing = false;
        if (_nameInputField != null)
        {
            _nameInputField.DeactivateInputField();
            _nameInputField.interactable = false;
            _nameInputField.readOnly = true;
        }
        RefreshName();
        ApplyMode();
    }

    private void ApplyMode()
    {
        bool hasInput = _nameInputField != null &&
                        !string.IsNullOrWhiteSpace(_nameInputField.text);
        SetGroup(_editButtonGroup, !_isEditing, true);
        SetGroup(_acceptButtonGroup, _isEditing, true);
        SetGroup(_inputRaycastGroup, true, _isEditing);
        SetGroup(_inputTextGroup, _isEditing, false);
        SetGroup(_placeholderGroup, _isEditing && !hasInput, false);
        SetGroup(_nameCharacterGroup, !_isEditing, false);
        SetGroup(_outlineGroup, _isEditing, false);
        SetInputImageAlpha(_isEditing);
        if (_acceptButton != null)
            _acceptButton.interactable = _isEditing && hasInput;
    }

    private void HandleInputChanged(string value)
    {
        if (_isEditing)
            ApplyMode();
    }

    private string ResolveCurrentName()
    {
        string name = NetworkManager.CurrentProfile != null
            ? NetworkManager.CurrentProfile.displayName
            : "";
        name = SaveDataSanitizer.SanitizePlayerName(name);
        return string.IsNullOrWhiteSpace(name) ? _fallbackName : name;
    }

    private void BindButtons()
    {
        Bind(_editButton, BeginEditing);
        Bind(_acceptButton, AcceptName);
    }

    private void UnbindButtons()
    {
        Unbind(_editButton, BeginEditing);
        Unbind(_acceptButton, AcceptName);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void SetGroup(CanvasGroup group, bool visible, bool interactive)
    {
        if (group == null)
            return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && interactive;
        group.blocksRaycasts = visible && interactive;
    }

    private void SetInputImageAlpha(bool visible)
    {
        Image image = _inputImage;
        if (image == null && _nameInputField != null)
            image = _nameInputField.GetComponent<Image>();
        if (image == null)
            return;

        Color color = image.color;
        color.a = visible ? 1f : 0f;
        image.color = color;
    }
}
