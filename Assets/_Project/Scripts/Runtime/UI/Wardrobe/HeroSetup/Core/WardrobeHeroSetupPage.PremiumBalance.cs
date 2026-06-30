using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class WardrobeHeroSetupPage
{
    [Header("Premium choice balance panel")]
    [SerializeField] private bool _showPremiumChoiceBalancePanel = true;
    [SerializeField] private GameObject _premiumChoiceBalancePanelPrefab;
    [SerializeField] private Transform _premiumChoiceBalancePanelParent;
    [SerializeField] private GameObject _premiumChoiceBalancePanel;
    [SerializeField] private TMP_Text _premiumChoiceBalanceText;
    [SerializeField] private Image _premiumChoiceHeartIcon;
    [SerializeField] private string _premiumChoiceBalanceTextFormat = "{0}";
    [SerializeField] private Vector2 _premiumChoiceBalancePanelOffset;
    [SerializeField] private bool _useStoryUiStylePremiumBalancePanel = true;

    GameObject _activePremiumChoiceBalancePanelInstance;
    GameObject _activePremiumChoiceBalancePanelPrefab;
    PremiumChoiceBalancePanelView _activePremiumChoiceBalancePanelView;
    Vector2 _activePremiumChoiceBalancePanelOffset;
    StoryUiStyle _runtimePremiumChoiceStoryUiStyle;
    bool _premiumChoiceBalancePanelVisible;

    void HandleWardrobePremiumChoiceHeartsChanged(int hearts)
    {
        SetWardrobePremiumChoiceBalanceText(hearts);
    }

    void RefreshWardrobePremiumChoiceBalancePanel()
    {
        SetWardrobePremiumChoiceBalancePanelVisible(_isOpen && HasVisibleWardrobePremiumChoice());
    }

    bool HasVisibleWardrobePremiumChoice()
    {
        if (_currentOptions == null)
            return false;

        for (int i = 0; i < _currentOptions.Count; i++)
        {
            if (GetVisiblePremiumCost(_currentOptions[i]) > 0)
                return true;
        }

        return false;
    }

    void RefreshWardrobePremiumChoiceBalanceText()
    {
        SetWardrobePremiumChoiceBalanceText(PlayerData.Hearts);
    }

    void SetWardrobePremiumChoiceBalanceText(int hearts)
    {
        hearts = SaveDataSanitizer.ClampCurrencyValue(hearts);

        if (_activePremiumChoiceBalancePanelView != null)
            _activePremiumChoiceBalancePanelView.SetBalance(hearts);

        if (_premiumChoiceBalanceText == null)
            return;

        string format = string.IsNullOrWhiteSpace(_premiumChoiceBalanceTextFormat)
            ? "{0}"
            : _premiumChoiceBalanceTextFormat;

        try
        {
            _premiumChoiceBalanceText.text = string.Format(format, hearts);
        }
        catch (System.FormatException)
        {
            _premiumChoiceBalanceText.text = hearts.ToString();
        }
    }

    void SetWardrobePremiumChoiceBalancePanelVisible(bool visible)
    {
        bool shouldShow = _showPremiumChoiceBalancePanel && visible;
        _premiumChoiceBalancePanelVisible = shouldShow;

        if (shouldShow)
        {
            GameObject panel = ResolveWardrobePremiumChoiceBalancePanelObject();
            if (panel != null)
            {
                bool usesSceneFallbackPanel = panel == _premiumChoiceBalancePanel;
                PremiumChoiceBalancePanelView view = ResolveWardrobePremiumChoiceBalancePanelView(panel);
                if (view != null)
                {
                    view.SetVisible(true);
                }
                else
                {
                    SetActiveIfDifferent(panel, true);
                    if (usesSceneFallbackPanel)
                        SetWardrobePremiumChoiceFallbackElementsVisible(true);
                }
            }

            RefreshWardrobePremiumChoiceBalanceText();
            return;
        }

        HideWardrobePremiumChoiceBalancePanel();
    }

    void RefreshVisibleWardrobePremiumChoiceBalancePanel()
    {
        if (!_premiumChoiceBalancePanelVisible)
            return;

        HideWardrobePremiumChoiceBalancePanel();
        SetWardrobePremiumChoiceBalancePanelVisible(true);
    }

    GameObject ResolveWardrobePremiumChoiceBalancePanelObject()
    {
        GameObject prefab = ResolveWardrobePremiumChoiceBalancePanelPrefab(out Vector2 offset);
        if (prefab != null)
            return EnsureWardrobePremiumChoiceBalancePanelInstance(prefab, offset);

        DestroyWardrobePremiumChoiceBalancePanelInstance();
        _activePremiumChoiceBalancePanelView = ResolveWardrobePremiumChoiceBalancePanelView(_premiumChoiceBalancePanel);
        return _premiumChoiceBalancePanel;
    }

    GameObject ResolveWardrobePremiumChoiceBalancePanelPrefab(out Vector2 offset)
    {
        StoryUiStyle style = ResolveWardrobePremiumChoiceStoryUiStyle();
        if (style != null && style.PremiumChoiceBalancePanelPrefabOverride != null)
        {
            offset = style.PremiumChoiceBalancePanelOffset;
            return style.PremiumChoiceBalancePanelPrefabOverride;
        }

        offset = _premiumChoiceBalancePanelOffset;
        return _premiumChoiceBalancePanelPrefab;
    }

    StoryUiStyle ResolveWardrobePremiumChoiceStoryUiStyle()
    {
        if (!_useStoryUiStylePremiumBalancePanel || !Application.isPlaying)
            return null;

        StoryManager manager = StoryManager.Instance;
        if (manager != null && manager.HasSelectedStory)
        {
            manager.TryResolveCurrentStoryUiStyle(out StoryUiStyle style, out _);
            if (style != null)
                return style;
        }

        return _runtimePremiumChoiceStoryUiStyle;
    }

    void CacheRuntimePremiumChoiceStoryUiStyle(GameData data)
    {
        _runtimePremiumChoiceStoryUiStyle = null;

        StoryData story = data != null ? data.Story : null;
        if (story != null && story.TryGetStoryUiStyle(out StoryUiStyle style, out _))
            _runtimePremiumChoiceStoryUiStyle = style;

        RefreshVisibleWardrobePremiumChoiceBalancePanel();
    }

    GameObject EnsureWardrobePremiumChoiceBalancePanelInstance(GameObject prefab, Vector2 offset)
    {
        if (prefab == null)
            return null;

        if (_activePremiumChoiceBalancePanelInstance != null &&
            (_activePremiumChoiceBalancePanelPrefab != prefab || _activePremiumChoiceBalancePanelOffset != offset))
        {
            DestroyWardrobePremiumChoiceBalancePanelInstance();
        }

        if (_activePremiumChoiceBalancePanelInstance == null)
        {
            Transform parent = ResolveWardrobePremiumChoiceBalancePanelParent();
            _activePremiumChoiceBalancePanelInstance = Instantiate(prefab, parent, false);
            _activePremiumChoiceBalancePanelInstance.name = prefab.name + " (Wardrobe Premium Choice Balance)";
            _activePremiumChoiceBalancePanelPrefab = prefab;
            _activePremiumChoiceBalancePanelOffset = offset;
            PlaceWardrobePremiumChoiceBalancePanel(_activePremiumChoiceBalancePanelInstance.transform, offset);
        }

        _activePremiumChoiceBalancePanelView = ResolveWardrobePremiumChoiceBalancePanelView(_activePremiumChoiceBalancePanelInstance);
        SetActiveIfDifferent(_activePremiumChoiceBalancePanelInstance, true);
        return _activePremiumChoiceBalancePanelInstance;
    }

    Transform ResolveWardrobePremiumChoiceBalancePanelParent()
    {
        if (_premiumChoiceBalancePanelParent != null)
            return _premiumChoiceBalancePanelParent;

        if (_optionsContainer != null && _optionsContainer.parent != null)
            return _optionsContainer.parent;

        if (_setupContentRoot != null)
            return _setupContentRoot.transform;

        return _pageRoot != null ? _pageRoot.transform : transform;
    }

    void PlaceWardrobePremiumChoiceBalancePanel(Transform panel, Vector2 offset)
    {
        if (panel == null)
            return;

        if (_optionsContainer != null && panel.parent == _optionsContainer.parent)
            panel.SetSiblingIndex(_optionsContainer.GetSiblingIndex());

        if (panel is RectTransform rect)
            rect.anchoredPosition += offset;
    }

    PremiumChoiceBalancePanelView ResolveWardrobePremiumChoiceBalancePanelView(GameObject panel)
    {
        return panel != null ? panel.GetComponent<PremiumChoiceBalancePanelView>() : null;
    }

    void HideWardrobePremiumChoiceBalancePanel()
    {
        _premiumChoiceBalancePanelVisible = false;

        if (_activePremiumChoiceBalancePanelView != null)
            _activePremiumChoiceBalancePanelView.SetVisible(false);

        DestroyWardrobePremiumChoiceBalancePanelInstance();

        SetActiveIfDifferent(_premiumChoiceBalancePanel, false);

        if (_premiumChoiceBalanceText != null)
            SetActiveIfDifferent(_premiumChoiceBalanceText.gameObject, false);

        if (_premiumChoiceHeartIcon != null)
            SetActiveIfDifferent(_premiumChoiceHeartIcon.gameObject, false);
    }

    void SetWardrobePremiumChoiceFallbackElementsVisible(bool visible)
    {
        if (_premiumChoiceBalanceText != null)
            SetActiveIfDifferent(_premiumChoiceBalanceText.gameObject, visible);

        if (_premiumChoiceHeartIcon != null)
            SetActiveIfDifferent(_premiumChoiceHeartIcon.gameObject, visible);
    }

    void DestroyWardrobePremiumChoiceBalancePanelInstance()
    {
        if (_activePremiumChoiceBalancePanelInstance != null)
        {
            if (Application.isPlaying)
                Destroy(_activePremiumChoiceBalancePanelInstance);
            else
                DestroyImmediate(_activePremiumChoiceBalancePanelInstance);
        }

        _activePremiumChoiceBalancePanelInstance = null;
        _activePremiumChoiceBalancePanelPrefab = null;
        _activePremiumChoiceBalancePanelView = null;
        _activePremiumChoiceBalancePanelOffset = Vector2.zero;
    }

    static void SetActiveIfDifferent(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
