using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EditProfileControllerTests
{
    private const string LocalNameKey = "VN_PROFILE_DISPLAY_NAME";

    [SetUp]
    public void SetUp() => ResetState();

    [TearDown]
    public void TearDown() => ResetState();

    [Test]
    public void EditAndAccept_TogglesGroupsAndSavesNameLocally()
    {
        GameObject root = new GameObject("EditProfileTest", typeof(RectTransform));
        root.SetActive(false);
        EditProfileController controller = root.AddComponent<EditProfileController>();
        Button editButton = CreateButton(root.transform, "Edit", out CanvasGroup editGroup);
        Button acceptButton = CreateButton(root.transform, "Accept", out CanvasGroup acceptGroup);
        TMP_InputField input = CreateInput(root.transform, out CanvasGroup inputGroup,
            out TMP_Text inputText, out TMP_Text placeholder);
        Image inputImage = input.GetComponent<Image>();
        CanvasGroup inputTextGroup = inputText.gameObject.AddComponent<CanvasGroup>();
        CanvasGroup placeholderGroup = placeholder.gameObject.AddComponent<CanvasGroup>();
        TMP_Text nameText = CreateText(root.transform, "NameCharacter");
        CanvasGroup nameGroup = nameText.gameObject.AddComponent<CanvasGroup>();
        CanvasGroup outlineGroup = CreateGroup(root.transform, "Outline");

        SetPrivate(controller, "_editButton", editButton);
        SetPrivate(controller, "_acceptButton", acceptButton);
        SetPrivate(controller, "_nameInputField", input);
        SetPrivate(controller, "_inputImage", inputImage);
        SetPrivate(controller, "_nameCharacterText", nameText);
        SetPrivate(controller, "_placeholderText", placeholder);
        SetPrivate(controller, "_editButtonGroup", editGroup);
        SetPrivate(controller, "_acceptButtonGroup", acceptGroup);
        SetPrivate(controller, "_inputRaycastGroup", inputGroup);
        SetPrivate(controller, "_inputTextGroup", inputTextGroup);
        SetPrivate(controller, "_placeholderGroup", placeholderGroup);
        SetPrivate(controller, "_nameCharacterGroup", nameGroup);
        SetPrivate(controller, "_outlineGroup", outlineGroup);
        root.SetActive(true);
        InvokePrivate(controller, "OnEnable");

        try
        {
            Assert.That(nameText.text, Is.EqualTo("Гость"));
            AssertGroup(editGroup, true, true);
            AssertGroup(nameGroup, true, false);
            AssertGroup(inputTextGroup, false, false);
            Assert.That(inputImage.color.a, Is.Zero);

            controller.BeginEditing();
            AssertGroup(editGroup, false, false);
            AssertGroup(acceptGroup, true, true);
            AssertGroup(inputGroup, true, true);
            AssertGroup(placeholderGroup, true, false);
            AssertGroup(nameGroup, false, false);
            AssertGroup(outlineGroup, true, false);
            Assert.That(inputImage.color.a, Is.EqualTo(1f));
            Assert.That(placeholder.text, Is.EqualTo("Введите имя пользователя"));
            Assert.That(acceptButton.interactable, Is.False);

            controller.AcceptName();
            Assert.That(PlayerPrefs.HasKey(LocalNameKey), Is.False);
            input.text = "  Алиса  ";
            Assert.That(acceptButton.interactable, Is.True);
            controller.AcceptName();

            Assert.That(PlayerPrefs.GetString(LocalNameKey), Is.EqualTo("Алиса"));
            Assert.That(NetworkManager.CurrentProfile.displayName, Is.EqualTo("Алиса"));
            Assert.That(nameText.text, Is.EqualTo("Алиса"));
            AssertGroup(editGroup, true, true);
            AssertGroup(acceptGroup, false, false);
            AssertGroup(inputGroup, true, false);
            AssertGroup(inputTextGroup, false, false);
            AssertGroup(placeholderGroup, false, false);
            AssertGroup(nameGroup, true, false);
            AssertGroup(outlineGroup, false, false);
            Assert.That(inputImage.color.a, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static TMP_InputField CreateInput(Transform parent, out CanvasGroup group,
        out TMP_Text text, out TMP_Text placeholder)
    {
        var go = new GameObject("InputField", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(TMP_InputField), typeof(CanvasGroup));
        go.transform.SetParent(parent);
        var field = go.GetComponent<TMP_InputField>();
        group = go.GetComponent<CanvasGroup>();
        text = CreateText(go.transform, "Text");
        placeholder = CreateText(go.transform, "Placeholder");
        field.textViewport = go.GetComponent<RectTransform>();
        field.textComponent = text as TextMeshProUGUI;
        field.placeholder = placeholder;
        return field;
    }

    private static TMP_Text CreateText(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent);
        return go.GetComponent<TMP_Text>();
    }

    private static Button CreateButton(Transform parent, string name, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent);
        group = go.GetComponent<CanvasGroup>();
        return go.GetComponent<Button>();
    }

    private static CanvasGroup CreateGroup(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent);
        return go.GetComponent<CanvasGroup>();
    }

    private static void AssertGroup(CanvasGroup group, bool visible, bool interactive)
    {
        Assert.That(group.alpha, Is.EqualTo(visible ? 1f : 0f));
        Assert.That(group.interactable, Is.EqualTo(visible && interactive));
        Assert.That(group.blocksRaycasts, Is.EqualTo(visible && interactive));
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }

    private static void ResetState()
    {
        PlayerPrefs.DeleteKey(LocalNameKey);
        MethodInfo reset = typeof(NetworkManager).GetMethod("ResetStaticState",
            BindingFlags.Static | BindingFlags.NonPublic);
        reset?.Invoke(null, null);
    }
}
