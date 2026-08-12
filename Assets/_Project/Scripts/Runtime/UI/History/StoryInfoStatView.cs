using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/History/Story Info Stat View")]
public sealed class StoryInfoStatView : MonoBehaviour
{
	[SerializeField] private Image iconImage;
	[SerializeField] private TMP_Text displayNameText;
	[SerializeField] private TMP_Text valueText;
	[SerializeField] private TMP_Text descriptionText;

	public void SetData(
		GameStoryStatData stat,
		int value,
		string displayName,
		string description)
	{
		if (stat == null)
		{
			Hide();
			return;
		}

		gameObject.SetActive(true);

		if (iconImage != null)
		{
			iconImage.sprite = stat.Icon;
			iconImage.enabled = stat.Icon != null;
		}

		if (displayNameText != null)
			displayNameText.text = displayName ?? "";

		if (valueText != null)
			valueText.text = value.ToString();

		if (descriptionText != null)
			descriptionText.text = description ?? "";
	}

	public void Hide()
	{
		gameObject.SetActive(false);
	}
}
