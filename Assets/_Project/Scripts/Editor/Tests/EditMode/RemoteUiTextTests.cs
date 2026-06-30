using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RemoteUiTextTests
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
    public void ApiContract_UiTextsQuery_UsesContentRuntimeRoute()
    {
        string query = ApiRoutes.ContentUiTextsQuery("main menu", "story_1", "ru");

        Assert.That(query, Does.StartWith(ApiRoutes.ContentUiTexts + "?"));
        Assert.That(query, Does.Contain("locale=ru"));
        Assert.That(query, Does.Contain("screenId=main%20menu"));
        Assert.That(query, Does.Contain("storyId=story_1"));
        Assert.That(ApiContract.IsRuntimeAllowed("GET", query), Is.True);
        Assert.That(ApiContract.RequiresBearerToken("GET", query), Is.True);
    }

    [Test]
    public void NetworkManager_UiTextParser_SanitizesRichTextAndKeepsSafeTmpTags()
    {
        string json =
            "{\"items\":[" +
            "{\"id\":\"notice\",\"text\":\"<b>Hello</b> <sprite=1> <color=#ff00ff>world</color>\",\"enabled\":true,\"locale\":\"ru\"}" +
            "]}";

        var items = NetworkManager.ParseUiTextResponse(json);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].text, Does.Contain("<b>Hello</b>"));
        Assert.That(items[0].text, Does.Contain("<color=#ff00ff>world</color>"));
        Assert.That(items[0].text, Does.Not.Contain("<sprite"));
        Assert.That(items[0].text, Does.Contain("&lt;sprite"));
    }

    [Test]
    public void NetworkManager_UiTextParser_AcceptsArrayPayload()
    {
        string json = "[{\"id\":\"main_menu_notice\",\"text\":\"Text from backend\",\"enabled\":true,\"locale\":\"ru\"}]";

        var items = NetworkManager.ParseUiTextResponse(json);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].id, Is.EqualTo("main_menu_notice"));
        Assert.That(items[0].text, Is.EqualTo("Text from backend"));
        Assert.That(items[0].enabled, Is.True);
        Assert.That(items[0].locale, Is.EqualTo("ru"));
    }

    [Test]
    public void NetworkManager_TryGetUiText_UsesCaseInsensitiveMostSpecificMatch()
    {
        string json =
            "{\"items\":[" +
            "{\"id\":\"Promo_Notice\",\"text\":\"Global\"}," +
            "{\"id\":\"promo_notice\",\"text\":\"Locale\",\"locale\":\"ru\"}," +
            "{\"id\":\"promo_notice\",\"text\":\"Screen\",\"screenId\":\"main\"}," +
            "{\"id\":\"promo_notice\",\"text\":\"Story\",\"storyId\":\"story_1\"}," +
            "{\"id\":\"promo_notice\",\"text\":\"Exact\",\"locale\":\"ru\",\"screenId\":\"main\",\"storyId\":\"story_1\"}" +
            "]}";

        Assert.That(ApplyUiTextPayload("main", "story_1", "ru", json), Is.True);

        Assert.That(NetworkManager.TryGetUiText("PROMO_NOTICE", "main", "story_1", "ru", out string text), Is.True);
        Assert.That(text, Is.EqualTo("Exact"));
    }

    [Test]
    public void NetworkManager_TryGetUiText_DisabledOrEmptySpecificItemHidesText()
    {
        string json =
            "{\"items\":[" +
            "{\"id\":\"notice\",\"text\":\"Global\"}," +
            "{\"id\":\"notice\",\"text\":\"Hidden\",\"enabled\":false,\"screenId\":\"main\"}," +
            "{\"id\":\"empty_notice\",\"text\":\"   \",\"screenId\":\"main\"}" +
            "]}";

        Assert.That(ApplyUiTextPayload("main", "", "ru", json), Is.True);

        Assert.That(NetworkManager.TryGetUiText("notice", "main", "", "ru", out _), Is.False);
        Assert.That(NetworkManager.TryGetUiText("empty_notice", "main", "", "ru", out _), Is.False);
    }

    [Test]
    public void RemoteUiTextBinder_SelfTarget_DisablesTmpAndCollapsesLayoutWhenMissing()
    {
        string json = "{\"items\":[{\"id\":\"notice\",\"text\":\"From server\",\"locale\":\"ru\",\"screenId\":\"main\"}]}";
        Assert.That(ApplyUiTextPayload("main", "", "ru", json), Is.True);

        GameObject go = new GameObject("RemoteText", typeof(RectTransform));
        go.SetActive(false);
        var text = go.AddComponent<TextMeshProUGUI>();
        var binder = go.AddComponent<RemoteUiTextBinder>();
        SetField(binder, "_textId", "notice");
        SetField(binder, "_screenId", "main");
        SetField(binder, "_targetText", text);
        SetField(binder, "_refreshOnEnable", false);

        try
        {
            go.SetActive(true);

            Assert.That(text.enabled, Is.True);
            Assert.That(text.text, Is.EqualTo("From server"));

            string disabledJson = "{\"items\":[{\"id\":\"notice\",\"text\":\"\",\"enabled\":false,\"locale\":\"ru\",\"screenId\":\"main\"}]}";
            Assert.That(ApplyUiTextPayload("main", "", "ru", disabledJson), Is.True);
            binder.ApplyCachedText();

            Assert.That(go.activeSelf, Is.True);
            Assert.That(text.enabled, Is.False);
            Assert.That(text.text, Is.EqualTo(""));
            Assert.That(go.GetComponent<LayoutElement>(), Is.Not.Null);
            Assert.That(go.GetComponent<LayoutElement>().ignoreLayout, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RemoteUiTextBinder_SeparateRoot_UsesCanvasGroupAndLayoutCollapse()
    {
        string json = "{\"items\":[{\"id\":\"banner\",\"text\":\"Visible\",\"locale\":\"ru\",\"screenId\":\"shop\"}]}";
        Assert.That(ApplyUiTextPayload("shop", "", "ru", json), Is.True);

        GameObject root = new GameObject("BannerRoot", typeof(RectTransform));
        GameObject textObject = new GameObject("BannerText", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        root.SetActive(false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        var binder = textObject.AddComponent<RemoteUiTextBinder>();
        SetField(binder, "_textId", "banner");
        SetField(binder, "_screenId", "shop");
        SetField(binder, "_targetText", text);
        SetField(binder, "_visibilityRoot", root);
        SetField(binder, "_refreshOnEnable", false);

        try
        {
            root.SetActive(true);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            LayoutElement layout = root.GetComponent<LayoutElement>();

            Assert.That(group, Is.Not.Null);
            Assert.That(layout, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(layout.ignoreLayout, Is.False);
            Assert.That(text.text, Is.EqualTo("Visible"));

            string disabledJson = "{\"items\":[{\"id\":\"banner\",\"text\":\"\",\"enabled\":false,\"locale\":\"ru\",\"screenId\":\"shop\"}]}";
            Assert.That(ApplyUiTextPayload("shop", "", "ru", disabledJson), Is.True);
            binder.ApplyCachedText();

            Assert.That(root.activeSelf, Is.True);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(layout.ignoreLayout, Is.True);
            Assert.That(text.enabled, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool ApplyUiTextPayload(string screenId, string storyId, string locale, string json)
    {
        MethodInfo method = typeof(NetworkManager).GetMethod(
            "TryApplyUiTextResponse",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object[] args = { screenId, storyId, locale, json, null };
        return (bool)method.Invoke(null, args);
    }

    private static void ResetUiTextState()
    {
        MethodInfo method = typeof(NetworkManager).GetMethod(
            "ResetUiTextState",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(null, null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
