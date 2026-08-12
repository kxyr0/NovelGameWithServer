using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/Settings Open Button")]
public sealed class SettingsOpenButton : MonoBehaviour
{
	[SerializeField] private Button button;

	[Header("Screens")]
	[SerializeField] private GameObject currentScreen;
	[SerializeField] private GameObject settingsScreen;

	[Header("Transition")]
	[SerializeField] private UIScreenTransitionAnimator transitionAnimator;

	private void Awake()
	{
		if (button == null)
		{
			Debug.LogError(
				"Settings open button is not assigned.",
				this);

			return;
		}

		button.onClick.AddListener(OpenSettings);
	}

	private void OpenSettings()
	{
		if (transitionAnimator.IsTransitioning)
			return;

		if (currentScreen == null)
		{
			Debug.LogError(
				"Cannot open settings: current screen is missing.",
				this);

			return;
		}

		if (settingsScreen == null)
		{
			Debug.LogError(
				"Cannot open settings: settings screen is missing.",
				this);

			return;
		}

		if (transitionAnimator == null)
		{
			Debug.LogError(
				"Cannot open settings: transition animator is missing.",
				this);

			return;
		}

		if (transitionAnimator.IsTransitioning)
			return;

		SettingsNavigationState.SetPreviousScreen(currentScreen);

		transitionAnimator.Play(
			currentScreen,
			settingsScreen,
			reverse: false,
			onComplete: () =>
			{
				currentScreen.SetActive(false);

				UIScreenState.FocusScreen("Settings");
			});
	}
}