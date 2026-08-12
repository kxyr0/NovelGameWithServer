using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IDPlayerText : MonoBehaviour
{
	private const string LoadingPlayerId = "…";

	[SerializeField] private TMP_Text _playerIdText;
	[SerializeField] private string _playerIdPrefix = "ID: ";

	private string _visiblePlayerId = "";

	private void OnEnable()
	{
		NetworkManager.OnProfileUpdated -= RefreshIdentity;
		NetworkManager.OnProfileUpdated += RefreshIdentity;
		AccountLoginState.Changed -= RefreshIdentity;
		AccountLoginState.Changed += RefreshIdentity;
		RefreshIdentity();
	}

	private void OnDisable()
	{
		NetworkManager.OnProfileUpdated -= RefreshIdentity;
		AccountLoginState.Changed -= RefreshIdentity;
	}

	private void RefreshIdentity()
	{
		PlayerProfileState profile = NetworkManager.CurrentProfile;

		_visiblePlayerId = PlayerPublicIdFormatter.FormatServerIdOrEmpty(
			profile != null ? profile.playerId : "");

		if (_playerIdText != null)
			_playerIdText.text = (_playerIdPrefix ?? "") +
				(_visiblePlayerId.Length > 0 ? _visiblePlayerId : LoadingPlayerId);
	}
}
