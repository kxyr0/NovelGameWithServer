using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Item Navigation")]
public sealed class PlayerCollectionItemNavigation : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] private Button _button;

    [Header("Navigation")]
    [SerializeField] private StoryScreenNavigator _screenNavigator;
    [SerializeField] private string _momentScreenId = "CutsceneScreen";
    [SerializeField] private string _cardScreenId = "CardScreen";

    private PlayerCollectionItemDefinition _item;

    private void Awake()
    {
        ResolveButton();
    }

    private void OnEnable()
    {
        BindButton();
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    private void OnValidate()
    {
        ResolveButton();
        _momentScreenId = UIScreenState.NormalizeScreenId(_momentScreenId);
        _cardScreenId = UIScreenState.NormalizeScreenId(_cardScreenId);
    }

    public void Bind(PlayerCollectionItemDefinition item)
    {
        _item = item;
        Button button = ResolveButton();
        if (button != null)
            button.interactable = item != null;
    }

    public void OpenSelectedItem()
    {
        if (_item == null)
        {
            Debug.LogWarning(
                "PlayerCollectionItemNavigation: collection item is not bound.",
                this);
            return;
        }

        StoryScreenNavigator navigator = ResolveNavigator();
        string targetScreenId = ResolveTargetScreenId(_item.Kind);
        if (navigator == null || targetScreenId.Length == 0)
        {
            Debug.LogWarning(
                "PlayerCollectionItemNavigation: navigation target is not configured.",
                this);
            return;
        }

        PlayerCollectionSelectionState.Select(_item);

        if (navigator.OpenScreen(targetScreenId))
            return;

        PlayerCollectionSelectionState.Clear();
        Debug.LogWarning(
            $"PlayerCollectionItemNavigation: screen '{targetScreenId}' is not available. " +
            "Add a UIScreenMarker with this Screen Id.",
            this);
    }

    private string ResolveTargetScreenId(PlayerCollectionKind kind)
    {
        string screenId = kind == PlayerCollectionKind.Moment
            ? _momentScreenId
            : _cardScreenId;
        return UIScreenState.NormalizeScreenId(screenId);
    }

    private StoryScreenNavigator ResolveNavigator()
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
        return _screenNavigator;
    }

    private Button ResolveButton()
    {
        if (_button != null)
            return _button;

        _button = GetComponent<Button>();
        if (_button == null)
            _button = GetComponentInChildren<Button>(true);
        return _button;
    }

    private void BindButton()
    {
        Button button = ResolveButton();
        if (button == null)
            return;

        button.onClick.RemoveListener(OpenSelectedItem);
        button.onClick.AddListener(OpenSelectedItem);
    }

    private void UnbindButton()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OpenSelectedItem);
    }
}
