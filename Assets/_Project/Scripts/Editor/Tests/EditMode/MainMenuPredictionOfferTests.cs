using System.Reflection;
using NUnit.Framework;

public sealed class MainMenuPredictionOfferTests
{
    [SetUp]
    public void SetUp()
    {
        ResetUiTextState();
    }

    [TearDown]
    public void TearDown()
    {
        ResetUiTextState();
    }

    [Test]
    public void NetworkManager_BuildsSharedPredictionOfferFromMainScreenUiTexts()
    {
        string json =
            "{\"items\":[" +
            Item(NetworkManager.MainMenuPredictionEnabledTextId, "true") + "," +
            Item(NetworkManager.MainMenuPredictionCardIdTextId, "veil") + "," +
            Item(NetworkManager.MainMenuPredictionTitleTextId, "Вуаль") + "," +
            Item(NetworkManager.MainMenuPredictionDescriptionTextId, "Общее предсказание") + "," +
            Item(NetworkManager.MainMenuPredictionImageUrlTextId, "/media/veil.png") +
            "]}";

        Assert.That(NetworkManager.ApplyPushedUiTexts("MainScreen", "", "ru", json, out string error), Is.True, error);
        Assert.That(NetworkManager.TryGetMainMenuPredictionOffer("ru", out MainMenuPredictionOfferContent content), Is.True);
        Assert.That(content.CardId, Is.EqualTo("veil"));
        Assert.That(content.Title, Is.EqualTo("Вуаль"));
        Assert.That(content.Description, Is.EqualTo("Общее предсказание"));
        Assert.That(content.ImageUrl, Is.EqualTo("/media/veil.png"));
    }

    [TestCase("false")]
    [TestCase("0")]
    [TestCase("")]
    public void NetworkManager_HidesPredictionOfferUnlessAdminExplicitlyEnablesIt(string enabled)
    {
        string json =
            "{\"items\":[" +
            Item(NetworkManager.MainMenuPredictionEnabledTextId, enabled) + "," +
            Item(NetworkManager.MainMenuPredictionTitleTextId, "Вуаль") + "," +
            Item(NetworkManager.MainMenuPredictionDescriptionTextId, "Описание") +
            "]}";

        Assert.That(NetworkManager.ApplyPushedUiTexts("MainScreen", "", "ru", json, out _), Is.True);
        Assert.That(NetworkManager.TryGetMainMenuPredictionOffer("ru", out _), Is.False);
    }

    [Test]
    public void NetworkManager_DropsUnsupportedPredictionImageUrl()
    {
        string json =
            "{\"items\":[" +
            Item(NetworkManager.MainMenuPredictionEnabledTextId, "true") + "," +
            Item(NetworkManager.MainMenuPredictionTitleTextId, "Вуаль") + "," +
            Item(NetworkManager.MainMenuPredictionDescriptionTextId, "Описание") + "," +
            Item(NetworkManager.MainMenuPredictionImageUrlTextId, "file:///private/card.png") +
            "]}";

        Assert.That(NetworkManager.ApplyPushedUiTexts("MainScreen", "", "ru", json, out _), Is.True);
        Assert.That(NetworkManager.TryGetMainMenuPredictionOffer("ru", out MainMenuPredictionOfferContent content), Is.True);
        Assert.That(content.ImageUrl, Is.Empty);
    }

    private static string Item(string id, string text)
    {
        return "{\"id\":\"" + NetworkJson.Escape(id) +
               "\",\"text\":\"" + NetworkJson.Escape(text) +
               "\",\"enabled\":true,\"locale\":\"ru\",\"screenId\":\"MainScreen\"}";
    }

    private static void ResetUiTextState()
    {
        MethodInfo method = typeof(NetworkManager).GetMethod(
            "ResetUiTextState",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(null, null);
    }
}
