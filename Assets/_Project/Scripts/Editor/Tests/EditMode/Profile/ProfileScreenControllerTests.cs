using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProfileScreenControllerTests
{
    [SetUp]
    public void SetUp()
    {
        ResetRuntimeState();
    }

    [TearDown]
    public void TearDown()
    {
        ResetRuntimeState();
    }

    [Test]
    public void AuthProfile_AppliesDisplayNameAndRaisesUpdate()
    {
        GameObject go = new GameObject("ProfileAuthTest");
        NetworkManager network = go.AddComponent<NetworkManager>();
        network.enabled = false;
        int updateCount = 0;
        Action handler = () => updateCount++;
        NetworkManager.OnProfileUpdated += handler;

        try
        {
            bool applied = (bool)InvokePrivate(
                network,
                "ApplyAuthResponse",
                "{\"authToken\":\"profile-token\",\"playerId\":\"server-42\"," +
                "\"profile\":{\"displayName\":\"Алиса\",\"locale\":\"ru\"}}");

            Assert.That(applied, Is.True);
            Assert.That(NetworkManager.CurrentProfile.displayName, Is.EqualTo("Алиса"));
            Assert.That(NetworkManager.CurrentProfile.playerId, Is.EqualTo("server-42"));
            Assert.That(updateCount, Is.EqualTo(1));
        }
        finally
        {
            NetworkManager.OnProfileUpdated -= handler;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AuthProfile_UsesFallbacksForEmptyIdentity()
    {
        GameObject go = new GameObject("ProfileFallbackTest");
        NetworkManager network = go.AddComponent<NetworkManager>();
        network.enabled = false;
        try
        {
            bool applied = (bool)InvokePrivate(network, "ApplyAuthResponse", "{\"authToken\":\"profile-token\",\"profile\":{\"displayName\":\" \"}}");
            Assert.That(applied, Is.True);
            Assert.That(NetworkManager.CurrentProfile.displayName, Is.EqualTo("Гость"));
            Assert.That(NetworkManager.CurrentProfile.playerId, Is.EqualTo("999-999"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void Identity_HidesUnconfirmedIdAndCopiesServerId()
    {
        GameObject controllerGo = new GameObject("ProfileControllerTest");
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject idGo = new GameObject("Id", typeof(RectTransform), typeof(TextMeshProUGUI));
        ProfileScreenController controller = controllerGo.AddComponent<ProfileScreenController>();
        TMP_Text nameText = nameGo.GetComponent<TMP_Text>();
        TMP_Text idText = idGo.GetComponent<TMP_Text>();
        SetPrivate(controller, "_displayNameText", nameText);
        SetPrivate(controller, "_playerIdText", idText);

        try
        {
            AccountLoginState.MarkSignedIn("player@example.com");
            controller.RefreshIdentity();
            Assert.That(nameText.text, Is.EqualTo("Гость"));
            Assert.That(idText.text, Is.EqualTo("ID: …"));

            NetworkManager.CurrentProfile.playerId = "player_BEFDCFBA";
            controller.RefreshIdentity();
            Assert.That(idText.text, Is.EqualTo(
                "ID: " + PlayerPublicIdFormatter.FormatServerIdOrEmpty("player_BEFDCFBA")));
            NetworkManager.CurrentProfile.displayName = "Серверный игрок";
            NetworkManager.CurrentProfile.playerId = "123456";
            controller.RefreshIdentity();
            controller.CopyPlayerId();
            Assert.That(nameText.text, Is.EqualTo("Серверный игрок"));
            Assert.That(idText.text, Is.EqualTo("ID: 123-456"));
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo("123-456"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerGo);
            UnityEngine.Object.DestroyImmediate(nameGo);
            UnityEngine.Object.DestroyImmediate(idGo);
        }
    }

    [Test]
    public void CachedServerId_IsAvailableDuringNetworkAwake()
    {
        PlayerPrefs.SetString("VN_PLAYER_ID", "cached-server-player");
        PlayerPrefs.Save();
        GameObject go = new GameObject("CachedProfileIdTest");
        NetworkManager network = go.AddComponent<NetworkManager>();
        network.enabled = false;

        try
        {
            Assert.That(NetworkManager.CurrentProfile.playerId,
                Is.EqualTo("cached-server-player"));
            Assert.That(PlayerPublicIdFormatter.FormatServerIdOrEmpty(
                NetworkManager.CurrentProfile.playerId), Is.Not.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Navigation_OpensEveryProfileTargetAndReturns()
    {
        CanvasGroup profile = CreateGroup("Profile");
        CanvasGroup edit = CreateGroup("EditProfile");
        CanvasGroup moments = CreateGroup("MomentsCollection");
        CanvasGroup predictions = CreateGroup("PredictionsCollection");
        GameObject navigatorGo = new GameObject("ProfileNavigatorTest");
        StoryScreenNavigator navigator = navigatorGo.AddComponent<StoryScreenNavigator>();
        navigator.ScreenTransition = UIScreenTransitionType.None;
        SetPrivate(navigator, "_includeScreenMarkers", false);
        SetPrivate(navigator, "_initialScreenId", "Profile");
        SetPrivate(navigator, "_screens", new[]
        {
            new StoryScreenNavigator.ScreenBinding("Profile", profile, null, null),
            new StoryScreenNavigator.ScreenBinding("EditProfile", edit, null, null),
            new StoryScreenNavigator.ScreenBinding("MomentsCollection", moments, null, null),
            new StoryScreenNavigator.ScreenBinding("PredictionsCollection", predictions, null, null)
        });
        navigator.PrepareInitialState();

        GameObject controllerGo = new GameObject("ProfileNavigationControllerTest");
        ProfileScreenController controller = controllerGo.AddComponent<ProfileScreenController>();
        SetPrivate(controller, "_screenNavigator", navigator);

        try
        {
            AssertVisible(profile);
            controller.OpenProfileEdit();
            AssertVisible(edit);
            controller.OpenProfile();
            AssertVisible(profile);
            controller.OpenMomentsCollection();
            AssertVisible(moments);
            controller.OpenProfile();
            controller.OpenPredictionsCollection();
            AssertVisible(predictions);
            controller.OpenProfile();

            SetPrivate(controller, "_profileEditScreenId", "MissingProfileScreen");
            controller.OpenProfileEdit();
            AssertVisible(profile);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerGo);
            UnityEngine.Object.DestroyImmediate(navigatorGo);
            UnityEngine.Object.DestroyImmediate(profile.gameObject);
            UnityEngine.Object.DestroyImmediate(edit.gameObject);
            UnityEngine.Object.DestroyImmediate(moments.gameObject);
            UnityEngine.Object.DestroyImmediate(predictions.gameObject);
        }
    }

    private static CanvasGroup CreateGroup(string name)
    {
        return new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
    }

    private static void AssertVisible(CanvasGroup expected)
    {
        Assert.That(expected.alpha, Is.EqualTo(1f));
        Assert.That(expected.interactable, Is.True);
        Assert.That(expected.blocksRaycasts, Is.True);
    }

    private static object InvokePrivate(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null);
        return info.Invoke(target, args);
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null);
        info.SetValue(target, value);
    }

    private static void ResetRuntimeState()
    {
        PlayerPrefs.DeleteKey("VN_PROFILE_DISPLAY_NAME");
        MethodInfo reset = typeof(NetworkManager).GetMethod(
            "ResetStaticState",
            BindingFlags.Static | BindingFlags.NonPublic);
        reset?.Invoke(null, null);
        UIScreenState.SetCurrentScreen("");
        UIScreenState.ClearSelectedScreen();
        GUIUtility.systemCopyBuffer = "";
        PlayerPrefs.DeleteKey("VN_AUTH_TOKEN");
        PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN");
        PlayerPrefs.DeleteKey("VN_REFRESH_TOKEN_V2");
        PlayerPrefs.DeleteKey("VN_PLAYER_ID");
        PlayerPrefs.DeleteKey("Nocturne.Account.SignedIn");
        PlayerPrefs.DeleteKey("Nocturne.Account.Email");
        PlayerPrefs.DeleteKey("Nocturne.Account.PublicPlayerId");
    }
}
