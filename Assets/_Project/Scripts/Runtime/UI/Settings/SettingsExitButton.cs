using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Settings/Settings Exit Button")]
public sealed class SettingsExitButton : MonoBehaviour
{
	[SerializeField] private Button button;

	[Header("Screens")]
	[SerializeField] private GameObject settingsScreen;
	[SerializeField] private GameObject fallbackScreen;

	[Header("Transition")]
	[SerializeField] private UIScreenTransitionAnimator transitionAnimator;

	private void Awake()
	{
		if (button == null)
		{
			Debug.LogError(
				"Settings exit button is not assigned.",
				this);

			return;
		}

		button.onClick.AddListener(ExitSettings);
	}

	private void ExitSettings()
	{
		if (transitionAnimator.IsTransitioning)
			return;

		GameObject targetScreen =
			SettingsNavigationState.ConsumePreviousScreen();

		if (targetScreen == null)
			targetScreen = fallbackScreen;

		if (targetScreen == null)
		{
			Debug.LogError(
				"Cannot exit settings: target screen is missing.",
				this);

			return;
		}

		UIScreenMarker targetMarker =
			targetScreen.GetComponentInChildren<UIScreenMarker>(true);

		if (targetMarker == null)
		{
			Debug.LogError(
				$"Screen '{targetScreen.name}' has no UIScreenMarker.",
				targetScreen);

			return;
		}

		transitionAnimator.Play(
			settingsScreen,
			targetScreen,
			reverse: true,
			onComplete: () =>
			{
				settingsScreen.SetActive(false);

				UIScreenState.FocusScreen(
					targetMarker.ScreenId);
			});
	}
}