using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Story Favorite Toggle Button")]
public sealed class StoryFavoriteToggleButton : MonoBehaviour, IPointerClickHandler
{
    [Header("История")]
    [SerializeField]
    [InspectorName("Game Data")]
    [Tooltip("Данные истории. Обычно заполнять не нужно: GameButtonView передаст их автоматически.")]
    private GameData _gameData;

    [Header("Визуал")]
    [SerializeField]
    [InspectorName("State fades")]
    [Tooltip("Sprite Fade компоненты, которые показывают active/default состояние избранного.")]
    private UISpriteStateFade[] _stateFades = Array.Empty<UISpriteStateFade>();

    [SerializeField]
    [InspectorName("Отключать hover у fades")]
    [Tooltip("Отключить hover-логику у указанных Sprite Fade, чтобы active держался до второго клика.")]
    private bool _disableHoverOnStateFades = true;

    [SerializeField]
    [InspectorName("Обновлять при OnEnable")]
    [Tooltip("При включении кнопки сразу подтянуть состояние из FavoritesManager.")]
    private bool _refreshOnEnable = true;

    [Header("Input")]
    [SerializeField]
    [InspectorName("Поглощать клик")]
    [Tooltip("Остановить распространение клика после toggle избранного.")]
    private bool _consumeClick = true;

    private GameButtonView _gameButtonView;

    private void Awake()
    {
        ResolveGameButtonView();
        PrepareStateFades();
    }

    private void OnEnable()
    {
        FavoritesManager.OnChanged += RefreshVisual;
        ResolveGameButtonView();
        PrepareStateFades();
        EnsureFavoritesManager(createIfMissing: true);

        if (_refreshOnEnable)
            RefreshVisual();
    }

    private void OnDisable()
    {
        FavoritesManager.OnChanged -= RefreshVisual;
    }

    private void OnValidate()
    {
        _stateFades ??= Array.Empty<UISpriteStateFade>();
    }

    public void Configure(GameData gameData)
    {
        _gameData = gameData;
        RefreshVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        ToggleFavorite();

        if (_consumeClick)
            eventData?.Use();
    }

    public void ToggleFavorite()
    {
        string storyId = ResolveStoryId();
        if (string.IsNullOrEmpty(storyId))
        {
            Debug.LogWarning("[StoryFavoriteToggleButton] Story id is empty.", this);
            return;
        }

        FavoritesManager manager = EnsureFavoritesManager(createIfMissing: true);
        if (manager == null)
        {
            Debug.LogWarning("[StoryFavoriteToggleButton] FavoritesManager is not available.", this);
            return;
        }

        if (manager.IsFavorite(storyId))
            manager.Remove(storyId);
        else
            manager.Add(storyId, ResolveStoryLabel());

        RefreshVisual();
    }

    public void RefreshVisual()
    {
        string storyId = ResolveStoryId();
        FavoritesManager manager = EnsureFavoritesManager(createIfMissing: false);
        bool isFavorite = manager != null && !string.IsNullOrEmpty(storyId) && manager.IsFavorite(storyId);
        ApplyStateFades(isFavorite);
    }

    private void PrepareStateFades()
    {
        if (_stateFades == null)
            return;

        if (!_disableHoverOnStateFades)
            return;

        for (int i = 0; i < _stateFades.Length; i++)
        {
            UISpriteStateFade stateFade = _stateFades[i];
            if (stateFade != null)
                stateFade.SetPointerHoverEnabled(false, false);
        }
    }

    private void ApplyStateFades(bool active)
    {
        if (_stateFades == null)
            return;

        for (int i = 0; i < _stateFades.Length; i++)
        {
            UISpriteStateFade stateFade = _stateFades[i];
            if (stateFade != null)
                stateFade.SetActiveState(active);
        }
    }

    private string ResolveStoryId()
    {
        GameData gameData = ResolveGameData();
        if (gameData != null && gameData.Story != null)
            return SaveDataSanitizer.SanitizeIdentifier(gameData.Story.StoryId);

        return "";
    }

    private string ResolveStoryLabel()
    {
        GameData gameData = ResolveGameData();
        return gameData != null ? gameData.GameName : "";
    }

    private GameData ResolveGameData()
    {
        if (_gameData != null)
            return _gameData;

        GameButtonView gameButtonView = ResolveGameButtonView();
        return gameButtonView != null ? gameButtonView.Data : null;
    }

    private GameButtonView ResolveGameButtonView()
    {
        if (_gameButtonView == null)
            _gameButtonView = GetComponentInParent<GameButtonView>(true);

        return _gameButtonView;
    }

    private static FavoritesManager EnsureFavoritesManager(bool createIfMissing)
    {
        if (FavoritesManager.Instance != null)
            return FavoritesManager.Instance;

        FavoritesManager manager = UnityEngine.Object.FindObjectOfType<FavoritesManager>(true);
        if (manager != null)
            return manager;

        if (!createIfMissing)
            return null;

        var managerObject = new GameObject("FavoritesManager");
        return managerObject.AddComponent<FavoritesManager>();
    }
}
