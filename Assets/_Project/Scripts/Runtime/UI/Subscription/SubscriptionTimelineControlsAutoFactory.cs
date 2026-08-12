using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public static class SubscriptionTimelineControlsAutoFactory
{
    const float ButtonWidth = 92f;
    const float ButtonHeight = 38f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreate();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    static void TryCreate()
    {
        SubscriptionFeatureConfig config = SubscriptionFeatureConfig.LoadOrDisabled();
        if (config == null || !config.FeaturesEnabled)
            return;
        if (UnityEngine.Object.FindObjectOfType<SubscriptionTimelineControlsView>(true) != null)
            return;

        Canvas canvas = FindCanvas();
        if (canvas == null)
            return;

        GameObject root = CreateRoot(canvas.transform);
        var view = root.GetComponent<SubscriptionTimelineControlsView>();
        var presenter = root.GetComponent<SubscriptionTimelineControlsPresenter>();
        Button back = CreateButton(root.transform, "SubscriptionRewindBackButton", "Назад");
        Button forward = CreateButton(root.transform, "SubscriptionRewindForwardButton", "Вперёд");
        Button undo = CreateButton(root.transform, "SubscriptionUndoChoiceButton", "Отмена");
        view.Assign(back, forward, undo);
        presenter.Assign(view, FindStoryManager(), ResolveService<ISubscriptionEntitlementService>(), ResolveService<IStoryChoiceTimelineService>());
        root.SetActive(true);
    }

    static GameObject CreateRoot(Transform parent)
    {
        var root = new GameObject("SubscriptionTimelineControls");
        root.SetActive(false);
        root.AddComponent<RectTransform>();
        root.AddComponent<CanvasGroup>();
        var layout = root.AddComponent<HorizontalLayoutGroup>();
        root.AddComponent<SubscriptionTimelineControlsView>();
        root.AddComponent<SubscriptionTimelineControlsPresenter>();
        root.transform.SetParent(parent, false);
        ConfigureRoot(root.GetComponent<RectTransform>());
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return root;
    }

    static void ConfigureRoot(RectTransform rect)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(ButtonWidth * 3f + 16f, ButtonHeight);
    }

    static Button CreateButton(Transform parent, string name, string label)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.13f, 0.15f, 0.9f);
        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = ButtonWidth;
        layout.preferredHeight = ButtonHeight;
        Button button = buttonObject.GetComponent<Button>();
        button.interactable = false;
        CreateLabel(buttonObject.transform, label);
        return button;
    }

    static void CreateLabel(Transform parent, string label)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 16;
        text.color = Color.white;
    }

    static Canvas FindCanvas()
    {
        GameObject named = GameObject.Find("NovelCanvas");
        if (named != null && named.TryGetComponent(out Canvas canvas))
            return canvas;
        return UnityEngine.Object.FindObjectOfType<Canvas>(true);
    }

    static StoryManager FindStoryManager()
    {
        return UnityEngine.Object.FindObjectOfType<StoryManager>(true);
    }

    static T ResolveService<T>() where T : class
    {
        try
        {
            LifetimeScope scope = LifetimeScope.Find<NovelTemplateLifetimeScope>();
            return scope != null && scope.Container != null ? scope.Container.Resolve<T>() : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SubscriptionTimelineControls] Не удалось получить сервис {typeof(T).Name}: {exception.Message}");
            return null;
        }
    }
}
