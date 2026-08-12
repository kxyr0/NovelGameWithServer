using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Item View")]
public sealed class PlayerCollectionItemView : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _storyTitleText;

    public void Bind(PlayerCollectionItemDefinition item)
    {
        if (item == null) return;
        ResolveReferences();

        if (_image != null)
            _image.sprite = item.ResolveImage(
                PlayerCollectionState.GetCollectedImageId(item));
        if (_titleText != null)
            _titleText.text = item.Title;
        if (_storyTitleText != null)
            _storyTitleText.text = item.StoryTitle;
    }

    [ContextMenu("Auto Assign References")]
    private void ResolveReferences()
    {
        if (_image == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].transform != transform && images[i].name == "Image")
                {
                    _image = images[i];
                    break;
                }
            }

            if (_image == null && TryGetComponent(out Image rootImage))
                _image = rootImage;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (_titleText == null && texts.Length > 0)
            _titleText = texts[0];
        if (_storyTitleText == null && texts.Length > 1)
            _storyTitleText = texts[1];
    }

    private void Reset()
    {
        ResolveReferences();
    }
}
