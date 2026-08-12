using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/External Links")]
public sealed class SettingsExternalLinks : MonoBehaviour
{
	[SerializeField] private LinkConfig linkConfig;

	[Header("Buttons")]
	[SerializeField] private Button vkButton;
	[SerializeField] private Button telegramButton;
	[SerializeField] private Button userAgreementButton;
	[SerializeField] private Button personalDataButton;

	private void Awake()
	{
		if (vkButton != null)
		{
			vkButton.onClick.AddListener(
				() => OpenLink(linkConfig.VKUrl));
		}
		if (telegramButton != null)
		{
			telegramButton.onClick.AddListener(
				() => OpenLink(linkConfig.TelegramUrl));
		}
		if (userAgreementButton != null)
		{
			userAgreementButton.onClick.AddListener(
				() => OpenLink(linkConfig.UserAgreementUrl));
		}
		if (personalDataButton != null)
		{
			personalDataButton.onClick.AddListener(
				() => OpenLink(linkConfig.PersonalDataPolicyUrl));
		}
	}

	private static void OpenLink(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			Debug.LogError("Cannot open link: URL is empty.");
			return;
		}

		Application.OpenURL(url);
	}
}