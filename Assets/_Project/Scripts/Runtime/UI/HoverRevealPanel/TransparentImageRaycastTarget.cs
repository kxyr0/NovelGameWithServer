using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[AddComponentMenu("Novel Template/UI/Hover Reveal/Transparent Image Raycast Target")]
public sealed class TransparentImageRaycastTarget : MonoBehaviour
{
    [SerializeField, Tooltip("Применять прозрачность и raycastTarget при каждом включении объекта.")]
    private bool _applyOnEnable = true;
    [SerializeField, Range(0f, 1f), Tooltip("Альфа Image. При 0 объект невидим, но продолжает ловить UI Raycast.")]
    private float _targetAlpha = 0f;

    private Image _image;

    public Image Image
    {
        get
        {
            if (_image == null)
                _image = GetComponent<Image>();

            return _image;
        }
    }

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        if (_applyOnEnable)
            Apply();
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            _image = GetComponent<Image>();

        _targetAlpha = Mathf.Clamp01(_targetAlpha);
    }
    #endif

    public void Apply()
    {
        Image image = Image;
        if (image == null)
            return;

        Color color = image.color;
        color.a = _targetAlpha;
        image.color = color;
        image.raycastTarget = true;
    }
}
