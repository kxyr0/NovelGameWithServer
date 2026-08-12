using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/CopyButtonID")]
public sealed class CopyButtonID : MonoBehaviour
{
	[SerializeField] private string _copiedMessage = "ID скопирован";

	private string _visiblePlayerId = "";

	[Header("Buttons")]
	[SerializeField] private Button _copyButton;

	private void OnEnable()
	{
		NetworkManager.OnProfileUpdated -= RefreshIdentity;
		NetworkManager.OnProfileUpdated += RefreshIdentity;
		AccountLoginState.Changed -= RefreshIdentity;
		AccountLoginState.Changed += RefreshIdentity;
		Bind(_copyButton, CopyPlayerId);
		RefreshIdentity();
	}

	private void OnDisable()
	{
		NetworkManager.OnProfileUpdated -= RefreshIdentity;
		AccountLoginState.Changed -= RefreshIdentity;
		Unbind(_copyButton, CopyPlayerId);
	}

	public void CopyPlayerId()
	{
		RefreshIdentity();
		if (_visiblePlayerId.Length == 0)
			return;
		GUIUtility.systemCopyBuffer = _visiblePlayerId;
		ToastManager.Instance?.ShowSystemMessage(_copiedMessage);
	}

	private void RefreshIdentity()
	{
		PlayerProfileState profile = NetworkManager.CurrentProfile;
		_visiblePlayerId = PlayerPublicIdFormatter.FormatServerIdOrEmpty(
			profile != null ? profile.playerId : "");
		if (_copyButton != null)
			_copyButton.interactable = _visiblePlayerId.Length > 0;
	}

	private static void Bind(Button button, UnityEngine.Events.UnityAction action)
	{
		if (button == null)
			return;
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
	}

	private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
	{
		if (button != null)
			button.onClick.RemoveListener(action);
	}
}
