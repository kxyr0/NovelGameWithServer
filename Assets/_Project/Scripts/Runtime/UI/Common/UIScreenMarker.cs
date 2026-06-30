using UnityEngine;
using UnityEngine.Serialization;

public class UIScreenMarker : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("screenId")]
    [Tooltip("Стабильный ID экрана, например MainMenu, Story, Settings или Shop. Используется для навигации и отслеживания активного UI.")]
    private string _screenId = "";

    private string _registeredScreenId = "";

    public string ScreenId => UIScreenState.NormalizeScreenId(_screenId);

    private void OnEnable()
    {
        _registeredScreenId = ScreenId;
        UIScreenState.RegisterScreen(_registeredScreenId);
    }

    private void OnDisable()
    {
        UIScreenState.UnregisterScreen(_registeredScreenId);
        _registeredScreenId = "";
    }

    private void OnValidate()
    {
        _screenId = UIScreenState.NormalizeScreenId(_screenId);
    }
}
