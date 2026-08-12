using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Detail Screen")]
public sealed class PlayerCollectionDetailScreen : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerCollectionKind _expectedKind;

    [Header("View")]
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _storyTitleText;
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private GameObject _emptyRoot;

    private void OnEnable()
    {
        PlayerCollectionSelectionState.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerCollectionSelectionState.Changed -= Refresh;
    }

    public void Refresh()
    {
        PlayerCollectionItemDefinition item =
            PlayerCollectionSelectionState.CurrentItem;
        bool valid = item != null && item.Kind == _expectedKind;

        if (_contentRoot != null)
            _contentRoot.SetActive(valid);
        if (_emptyRoot != null)
            _emptyRoot.SetActive(!valid);

        if (_image != null)
        {
            _image.sprite = valid
                ? PlayerCollectionSelectionState.CurrentImage
                : null;
            _image.enabled = valid && _image.sprite != null;
        }

        if (_titleText != null)
            _titleText.text = valid ? item.Title : "";
        if (_storyTitleText != null)
            _storyTitleText.text = valid ? item.StoryTitle : "";
    }
}
