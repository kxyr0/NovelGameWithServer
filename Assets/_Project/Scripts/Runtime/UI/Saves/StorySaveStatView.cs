using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Saves/Story Save Stat View")]
public sealed class StorySaveStatView : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private string _format = "{0} {1}";

    public void SetData(
        GameStoryStatData stat,
        int value,
        string displayName)
    {
        if (stat == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        if (_iconImage != null)
        {
            _iconImage.sprite = stat.Icon;
            _iconImage.enabled = stat.Icon != null;
        }

        if (_valueText != null)
        {
            string format = string.IsNullOrWhiteSpace(_format)
                ? "{0} {1}"
                : _format;

            _valueText.text = string.Format(
                format,
                displayName ?? "",
                value);
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
