using UnityEngine;

public static class SettingsNavigationState
{
	private static GameObject _previousScreen;

	public static void SetPreviousScreen(GameObject screen)
	{
		_previousScreen = screen;
	}

	public static GameObject ConsumePreviousScreen()
	{
		GameObject screen = _previousScreen;
		_previousScreen = null;

		return screen;
	}

	public static void Clear()
	{
		_previousScreen = null;
	}
}