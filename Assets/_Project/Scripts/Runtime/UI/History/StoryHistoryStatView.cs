using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/History/Story History Stat View")]
public sealed class StoryHistoryStatView : MonoBehaviour
{
	[SerializeField] private Image iconImage;
	[SerializeField] private TMP_Text valueText;

	public void SetData(
	GameStoryStatData stat,
	int value,
	string displayName)
	{
		if (stat == null)
		{
			gameObject.SetActive(false);
			return;
		}

		gameObject.SetActive(true);

		if (iconImage != null)
		{
			iconImage.sprite = stat.Icon;
			iconImage.enabled = stat.Icon != null;
		}

		if (valueText != null)
			valueText.text = $"{displayName}:{value}";
	}

	public void Hide()
	{
		gameObject.SetActive(false);
	}
}