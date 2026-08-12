using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Authentication/Login Feedback View")]
public sealed class LoginFeedbackView : MonoBehaviour
{
    [Header("Outlines Only")]
    [SerializeField] private Graphic _emailOutline;
    [SerializeField] private Graphic _passwordOutline;
    [SerializeField] private Color _invalidOutlineColor = new Color32(190, 70, 65, 255);
    [SerializeField] private LoginErrorLayoutAnimator _layoutAnimator;

    [Header("Errors")]
    [SerializeField] private TMP_Text _emailErrorText;
    [SerializeField] private CanvasGroup _emailErrorGroup;
    [SerializeField] private string _emailErrorMessage = "Неверный e-mail";
    [SerializeField] private TMP_Text _passwordErrorText;
    [SerializeField] private CanvasGroup _passwordErrorGroup;
    [SerializeField] private string _passwordErrorMessage = "Неверный пароль";

    private Color _normalEmailColor;
    private Color _normalPasswordColor;
    private bool _normalColorsCaptured;

    private void OnEnable()
    {
        ResolveLayoutAnimator();
        CaptureNormalColors();
        Clear();
    }

    public void ShowInvalidCredentials()
    {
        ResolveLayoutAnimator();
        CaptureNormalColors();
        if (_emailOutline != null)
            _emailOutline.color = _invalidOutlineColor;
        if (_passwordOutline != null)
            _passwordOutline.color = _invalidOutlineColor;
        SetErrorVisible(_emailErrorText, _emailErrorGroup, _emailErrorMessage, true);
        SetErrorVisible(_passwordErrorText, _passwordErrorGroup, _passwordErrorMessage, true);
        _layoutAnimator?.SetErrorVisible(true);
    }

    public void Clear()
    {
        ResolveLayoutAnimator();
        CaptureNormalColors();
        if (_emailOutline != null)
            _emailOutline.color = _normalEmailColor;
        if (_passwordOutline != null)
            _passwordOutline.color = _normalPasswordColor;
        SetErrorVisible(_emailErrorText, _emailErrorGroup, _emailErrorMessage, false);
        SetErrorVisible(_passwordErrorText, _passwordErrorGroup, _passwordErrorMessage, false);
        _layoutAnimator?.SetErrorVisible(false);
    }

    private void CaptureNormalColors()
    {
        if (_normalColorsCaptured)
            return;
        _normalEmailColor = _emailOutline != null ? _emailOutline.color : Color.white;
        _normalPasswordColor = _passwordOutline != null ? _passwordOutline.color : Color.white;
        _normalColorsCaptured = true;
    }

    private static void SetErrorVisible(
        TMP_Text text,
        CanvasGroup group,
        string message,
        bool visible)
    {
        if (text != null)
        {
            text.text = message ?? "";
            text.enabled = group != null || visible;
        }
        if (group == null)
            return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ResolveLayoutAnimator()
    {
        if (_layoutAnimator == null)
            _layoutAnimator = GetComponent<LoginErrorLayoutAnimator>();
        if (_layoutAnimator == null && transform.parent != null)
            _layoutAnimator = transform.parent.GetComponentInChildren<LoginErrorLayoutAnimator>(true);
        if (_layoutAnimator == null)
            _layoutAnimator = gameObject.AddComponent<LoginErrorLayoutAnimator>();
        _layoutAnimator.ResolveReferences(transform);
    }
}
