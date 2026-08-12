using UnityEngine;

public static class PredictionOfferButtonInstaller
{
    private const string MainScreenName = "MainScreen";
    private const string RelativeButtonPath = "BackGround/RedButton";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForCurrentMainScreen()
    {
        GameObject mainScreen = FindMainScreen();
        if (mainScreen == null)
            return;

        Transform buttonRoot = mainScreen.transform.Find(RelativeButtonPath);
        if (buttonRoot == null)
        {
            Debug.LogWarning(
                $"[PredictionOffer] Не найден '{MainScreenName}/{RelativeButtonPath}'. " +
                "Добавьте PredictionOfferButtonController на root кнопки вручную.");
            return;
        }

        PredictionOfferButtonController controller =
            buttonRoot.GetComponent<PredictionOfferButtonController>();
        if (controller == null)
            controller = buttonRoot.gameObject.AddComponent<PredictionOfferButtonController>();

        GetOrCreateCardScreenController();
        controller.RefreshAssignment();
    }

    public static MainMenuPredictionCardScreenController GetOrCreateCardScreenController()
    {
        UIScreenMarker[] screens = Object.FindObjectsOfType<UIScreenMarker>(true);
        for (int i = 0; i < screens.Length; i++)
        {
            UIScreenMarker marker = screens[i];
            if (marker == null || marker.ScreenId != "CardScreenMainMenu")
                continue;

            MainMenuPredictionCardScreenController controller =
                marker.GetComponent<MainMenuPredictionCardScreenController>();
            return controller != null
                ? controller
                : marker.gameObject.AddComponent<MainMenuPredictionCardScreenController>();
        }

        return null;
    }

    private static GameObject FindMainScreen()
    {
        UIScreenMarker[] screens = Object.FindObjectsOfType<UIScreenMarker>(true);
        for (int i = 0; i < screens.Length; i++)
        {
            UIScreenMarker screen = screens[i];
            if (screen != null && screen.ScreenId == MainScreenName)
                return screen.gameObject;
        }

        return GameObject.Find(MainScreenName);
    }
}
