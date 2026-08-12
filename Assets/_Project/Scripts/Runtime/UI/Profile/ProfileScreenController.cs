using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Screen Controller")]
public sealed class ProfileScreenController : MonoBehaviour
{
	private const string DefaultDisplayName = "Гость";
	private const string LoadingPlayerId = "…";

	[Header("Identity")]
	[SerializeField] private TMP_Text _displayNameText;
	[SerializeField] private TMP_Text _playerIdText;
	[SerializeField] private string _fallbackDisplayName = DefaultDisplayName;
	[SerializeField] private string _playerIdPrefix = "ID: ";
	[SerializeField] private string _copiedMessage = "ID скопирован";

	[Header("Buttons")]
	[SerializeField] private Button _copyButton;
	[SerializeField] private Button _editButton;
	[SerializeField] private Button _momentsCollectionButton;
	[SerializeField] private Button _predictionsCollectionButton;
	[SerializeField] private Button[] _backButtons = Array.Empty<Button>();

	[Header("Navigation")]
	[SerializeField] private StoryScreenNavigator _screenNavigator;
	[SerializeField] private string _profileScreenId = "Profile";
	[SerializeField] private string _profileEditScreenId = "EditProfile";
	[SerializeField] private string _momentsCollectionScreenId = "MomentsCollection";
	[SerializeField] private string _predictionsCollectionScreenId = "PredictionsCollection";

	private string _visiblePlayerId = "";

	private void OnEnable()
	{
		BindButtons();
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
		UnbindButtons();
	}

	private void OnValidate()
	{
		_fallbackDisplayName = Fallback(_fallbackDisplayName, DefaultDisplayName);
		_profileScreenId = UIScreenState.NormalizeScreenId(_profileScreenId);
		_profileEditScreenId = UIScreenState.NormalizeScreenId(_profileEditScreenId);
		_momentsCollectionScreenId = UIScreenState.NormalizeScreenId(_momentsCollectionScreenId);
		_predictionsCollectionScreenId = UIScreenState.NormalizeScreenId(_predictionsCollectionScreenId);
		_backButtons ??= Array.Empty<Button>();
	}

	public void RefreshIdentity()
	{
		PlayerProfileState profile = NetworkManager.CurrentProfile;
		string displayName = Fallback(profile != null ? profile.displayName : "", _fallbackDisplayName);
		_visiblePlayerId = PlayerPublicIdFormatter.FormatServerIdOrEmpty(
			profile != null ? profile.playerId : "");

		if (_displayNameText != null)
			_displayNameText.text = displayName;
		if (_playerIdText != null)
			_playerIdText.text = (_playerIdPrefix ?? "") +
				(_visiblePlayerId.Length > 0 ? _visiblePlayerId : LoadingPlayerId);
		if (_copyButton != null)
			_copyButton.interactable = _visiblePlayerId.Length > 0;
	}

	public void CopyPlayerId()
	{
		RefreshIdentity();
		if (_visiblePlayerId.Length == 0)
			return;
		GUIUtility.systemCopyBuffer = _visiblePlayerId;
		ToastManager.Instance?.ShowSystemMessage(_copiedMessage);
	}

	public void OpenProfileEdit() => OpenScreen(_profileEditScreenId);
	public void OpenMomentsCollection() => OpenScreen(_momentsCollectionScreenId);
	public void OpenPredictionsCollection() => OpenScreen(_predictionsCollectionScreenId);
	public void OpenProfile() => OpenScreen(_profileScreenId);

	private void OpenScreen(string screenId)
	{
		StoryScreenNavigator navigator = ResolveNavigator();
		string targetId = UIScreenState.NormalizeScreenId(screenId);
		if (navigator != null && targetId.Length > 0 && navigator.OpenScreen(targetId))
			return;

		Debug.LogWarning($"ProfileScreenController: screen '{targetId}' is not available.", this);
	}

	private StoryScreenNavigator ResolveNavigator()
	{
		if (_screenNavigator == null)
			_screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
		return _screenNavigator;
	}

	private void BindButtons()
	{
		Bind(_copyButton, CopyPlayerId);
		Bind(_editButton, OpenProfileEdit);
		Bind(_momentsCollectionButton, OpenMomentsCollection);
		Bind(_predictionsCollectionButton, OpenPredictionsCollection);
		for (int i = 0; i < _backButtons.Length; i++)
			Bind(_backButtons[i], OpenProfile);
	}

	private void UnbindButtons()
	{
		Unbind(_copyButton, CopyPlayerId);
		Unbind(_editButton, OpenProfileEdit);
		Unbind(_momentsCollectionButton, OpenMomentsCollection);
		Unbind(_predictionsCollectionButton, OpenPredictionsCollection);
		for (int i = 0; i < _backButtons.Length; i++)
			Unbind(_backButtons[i], OpenProfile);
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

	private static string Fallback(string value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}

}
