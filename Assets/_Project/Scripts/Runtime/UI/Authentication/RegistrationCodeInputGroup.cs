using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Registration Code Input Group")]
public sealed class RegistrationCodeInputGroup : MonoBehaviour
{
    private const int RequiredDigits = 4;

    [Header("Digits")]
    [SerializeField] private TMP_InputField[] _digitInputs = new TMP_InputField[RequiredDigits];
    [SerializeField] private Graphic[] _outlines = new Graphic[RequiredDigits];

    [Header("Result Colors")]
    [SerializeField] private Color _invalidColor = new Color32(190, 70, 65, 255);
    [SerializeField] private Color _validColor = new Color32(70, 170, 100, 255);

    private UnityAction<string>[] _listeners;
    private Color[] _normalColors;
    private bool _normalColorsCaptured;
    public event Action<string> CodeCompleted;

    public string Code
    {
        get
        {
            string code = "";
            for (int i = 0; i < RequiredDigits; i++)
                code += GetDigit(i);
            return code;
        }
    }

    public bool IsComplete => Code.Length == RequiredDigits;
    private void OnEnable()
    {
        ConfigureInputs();
        CaptureNormalColors();
        BindInputs();
        SetResultState(null);
    }

    private void OnDisable()
    {
        UnbindInputs();
    }

    public void ClearAndFocus()
    {
        for (int i = 0; i < RequiredDigits; i++)
            GetInput(i)?.SetTextWithoutNotify("");
        SetInteractable(true);
        SetResultState(null);
        FocusInput(0);
    }

    public void SetInteractable(bool interactable)
    {
        for (int i = 0; i < RequiredDigits; i++)
        {
            TMP_InputField input = GetInput(i);
            if (input != null)
                input.interactable = interactable;
        }
    }

    public void SetResultState(bool? valid)
    {
        CaptureNormalColors();
        for (int i = 0; i < RequiredDigits; i++)
        {
            Graphic outline = GetOutline(i);
            if (outline == null)
                continue;
            outline.color = valid.HasValue
                ? valid.Value ? _validColor : _invalidColor
                : _normalColors[i];
        }
    }

    private void HandleDigitChanged(int index, string value)
    {
        SetResultState(null);
        TMP_InputField input = GetInput(index);
        string digit = LastDigit(value);
        if (input != null && input.text != digit)
            input.SetTextWithoutNotify(digit);

        if (digit.Length == 0)
        {
            if (index > 0)
                FocusInput(index - 1);
            return;
        }

        if (index < RequiredDigits - 1)
            FocusInput(index + 1);
        else if (IsComplete)
            CodeCompleted?.Invoke(Code);
    }

    private void ConfigureInputs()
    {
        for (int i = 0; i < RequiredDigits; i++)
        {
            TMP_InputField input = GetInput(i);
            if (input == null)
                continue;
            input.transition = Selectable.Transition.None;
            input.characterLimit = 1;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterValidation = TMP_InputField.CharacterValidation.Integer;
            input.keyboardType = TouchScreenKeyboardType.NumberPad;
            input.lineType = TMP_InputField.LineType.SingleLine;
        }
    }

    private void BindInputs()
    {
        UnbindInputs();
        _listeners = new UnityAction<string>[RequiredDigits];
        for (int i = 0; i < RequiredDigits; i++)
        {
            TMP_InputField input = GetInput(i);
            if (input == null)
                continue;
            int capturedIndex = i;
            _listeners[i] = value => HandleDigitChanged(capturedIndex, value);
            input.onValueChanged.AddListener(_listeners[i]);
        }
    }

    private void UnbindInputs()
    {
        if (_listeners == null)
            return;
        for (int i = 0; i < RequiredDigits; i++)
        {
            TMP_InputField input = GetInput(i);
            if (input != null && _listeners[i] != null)
                input.onValueChanged.RemoveListener(_listeners[i]);
        }
        _listeners = null;
    }

    private void CaptureNormalColors()
    {
        if (_normalColorsCaptured)
            return;
        _normalColors = new Color[RequiredDigits];
        for (int i = 0; i < RequiredDigits; i++)
            _normalColors[i] = GetOutline(i) != null ? GetOutline(i).color : Color.white;
        _normalColorsCaptured = true;
    }

    private void FocusInput(int index)
    {
        TMP_InputField input = GetInput(index);
        if (input == null || !input.interactable)
            return;
        input.Select();
        input.ActivateInputField();
    }

    private TMP_InputField GetInput(int index) =>
        _digitInputs != null && index < _digitInputs.Length ? _digitInputs[index] : null;
    private Graphic GetOutline(int index) =>
        _outlines != null && index < _outlines.Length ? _outlines[index] : null;
    private string GetDigit(int index) => GetInput(index) != null ? LastDigit(GetInput(index).text) : "";

    private static string LastDigit(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        for (int i = value.Length - 1; i >= 0; i--)
        {
            if (value[i] >= '0' && value[i] <= '9')
                return value[i].ToString();
        }
        return "";
    }
}
