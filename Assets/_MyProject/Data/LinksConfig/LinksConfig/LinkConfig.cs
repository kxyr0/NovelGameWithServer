using UnityEngine;

[CreateAssetMenu(
	fileName = "LinkConfig",
	menuName = "Configs/Link Config"
)]
public class LinkConfig : ScriptableObject
{
	[SerializeField] private string vkUrl;
	public string VKUrl => vkUrl;
	[SerializeField] private string telegramUrl;
	public string TelegramUrl => telegramUrl;
	[SerializeField] private string userAgreementUrl;
	public string UserAgreementUrl => userAgreementUrl;
	[SerializeField] private string personalDataPolicyUrl;
	public string PersonalDataPolicyUrl => personalDataPolicyUrl;
}