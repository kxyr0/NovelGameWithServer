// Dev Mode Panel — только в редакторе и dev-билдах.
// В релизе весь этот код не компилируется.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель разработчика — открывается тройным тапом в левом верхнем углу.
///
/// Возможности:
///   - Прыжок к конкретной сцене (SceneSetupNode) или ноде
///   - Показ/редактирование статов (affection и т.д.)
///   - Добавление свечей/искр
///   - Сброс прогресса
///   - Показ текущей ноды и её guid
///
/// Подключение:
///   1. Создай Panel "DevModePanel" в Canvas (поверх всего, изначально выключен).
///   2. Прикрепи этот скрипт к нему.
///   3. Назначь ссылки в инспекторе.
///   4. Создай пустой прозрачный Button "DevTapZone" в левом верхнем углу (~80×80px).
///      Назначь его в поле tapZone.
///
/// Открытие: тройной тап по tapZone за 1 секунду.
/// </summary>
public class DevModePanel : MonoBehaviour
{
    public static DevModePanel Instance;

    [Header("Ссылки")]
    public GameObject panel;                   // Root панели
    public Button tapZone;                     // Невидимая зона для открытия

    [Header("Статус")]
    public TMP_Text currentNodeText;           // "Нода: DialogueNode (guid...)"
    public TMP_Text onlineStatusText;          // "Онлайн / Оффлайн"

    [Header("Прыжок к сцене")]
    public TMP_Dropdown sceneDropdown;         // Список SceneSetupNode из текущего графа
    public Button jumpToSceneButton;

    [Header("Прыжок к ноде по индексу")]
    public TMP_InputField nodeIndexInput;      // Порядковый номер ноды
    public Button jumpToNodeButton;

    [Header("Валюта")]
    public TMP_InputField addCandlesInput;     // Сколько свечей добавить
    public Button addCandlesButton;
    public TMP_InputField addHeartsInput;      // Сколько искр добавить
    public Button addHeartsButton;
    public TMP_Text balanceText;               // "🕯 12  ❤ 5"

    [Header("Статы")]
    public TMP_Text statsText;                 // Список всех статов
    public TMP_InputField statKeyInput;        // Название стата
    public TMP_InputField statValueInput;      // Значение
    public Button setStatButton;

    [Header("Сброс")]
    public Button resetProgressButton;
    public Button resetAllButton;              // Полный сброс PlayerPrefs

    [Header("Закрыть")]
    public Button closeButton;

    // ── Внутреннее состояние ──────────────────────────────────

    int _tapCount = 0;
    float _lastTapTime = 0f;
    const float TAP_WINDOW = 1f;
    const int TAPS_TO_OPEN = 3;

    List<BaseStoryNode> _allNodes = new List<BaseStoryNode>();
    List<SceneSetupNode> _sceneNodes = new List<SceneSetupNode>();

    // ── Unity lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Тройной тап для открытия
        if (tapZone != null)
            tapZone.onClick.AddListener(OnTapZone);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (jumpToSceneButton != null)
            jumpToSceneButton.onClick.AddListener(JumpToSelectedScene);

        if (jumpToNodeButton != null)
            jumpToNodeButton.onClick.AddListener(JumpToNodeByIndex);

        if (addCandlesButton != null)
            addCandlesButton.onClick.AddListener(AddCandles);

        if (addHeartsButton != null)
            addHeartsButton.onClick.AddListener(AddHearts);

        if (setStatButton != null)
            setStatButton.onClick.AddListener(SetStat);

        if (resetProgressButton != null)
            resetProgressButton.onClick.AddListener(ResetProgress);

        if (resetAllButton != null)
            resetAllButton.onClick.AddListener(ResetAll);

        Hide();
    }

    void Update()
    {
        // Обновляем статус каждый кадр когда панель открыта
        if (panel != null && panel.activeSelf)
            RefreshStatus();
    }

    // ── Открытие/закрытие ──────────────────────────────────────

    void OnTapZone()
    {
        if (Time.unscaledTime - _lastTapTime > TAP_WINDOW)
            _tapCount = 0;

        _tapCount++;
        _lastTapTime = Time.unscaledTime;

        if (_tapCount >= TAPS_TO_OPEN)
        {
            _tapCount = 0;
            Show();
        }
    }

    public void Show()
    {
        BuildNodeList();
        RefreshStatus();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ── Построение списка нод ──────────────────────────────────

    void BuildNodeList()
    {
        _allNodes.Clear();
        _sceneNodes.Clear();

        var sm = StoryManager.Instance;
        if (sm == null || sm.storyGraph == null) return;

        // Собираем все ноды из текущего графа
        foreach (var node in sm.storyGraph.nodes)
        {
            if (node is BaseStoryNode bsn)
                _allNodes.Add(bsn);
            if (node is SceneSetupNode ssn)
                _sceneNodes.Add(ssn);
        }

        // Заполняем дропдаун сценами
        if (sceneDropdown != null)
        {
            sceneDropdown.ClearOptions();
            var options = _sceneNodes.Select((s, i) =>
            {
                string label = s.name ?? $"Сцена {i + 1}";
                return new TMP_Dropdown.OptionData(label);
            }).ToList();
            sceneDropdown.AddOptions(options);
        }
    }

    // ── Статус ────────────────────────────────────────────────

    void RefreshStatus()
    {
        // Текущая нода
        if (currentNodeText != null)
        {
            var sm = StoryManager.Instance;
            var node = GameState.Instance?.currentNode;
            if (node != null)
                currentNodeText.text = $"Нода: {node.GetType().Name}\n#{_allNodes.IndexOf(node as BaseStoryNode)} из {_allNodes.Count}";
            else
                currentNodeText.text = "Нода: —";
        }

        // Онлайн-статус
        if (onlineStatusText != null)
        {
            bool online = NetworkManager.IsOnline;
            bool auth   = NetworkManager.IsAuthenticated;
            onlineStatusText.text = online && auth ? "🟢 Онлайн" :
                                    auth            ? "🟡 Auth OK, сеть нет" : "🔴 Оффлайн";
        }

        // Баланс
        if (balanceText != null)
            balanceText.text = $"🕯 {PlayerData.Candles}   ❤ {PlayerData.Hearts}";

        // Статы
        if (statsText != null)
        {
            var stats = GameState.Instance?.stats;
            if (stats != null && stats.Count > 0)
                statsText.text = string.Join("\n", stats.Select(kv => $"{kv.Key}: {kv.Value}"));
            else
                statsText.text = "Статов нет";
        }
    }

    // ── Прыжок к сцене ────────────────────────────────────────

    void JumpToSelectedScene()
    {
        if (sceneDropdown == null || _sceneNodes.Count == 0) return;

        int idx = sceneDropdown.value;
        if (idx < 0 || idx >= _sceneNodes.Count) return;

        var scene = _sceneNodes[idx];
        JumpToNode(scene);
    }

    void JumpToNodeByIndex()
    {
        if (nodeIndexInput == null) return;
        if (!int.TryParse(nodeIndexInput.text, out int idx)) return;
        if (idx < 0 || idx >= _allNodes.Count) return;

        JumpToNode(_allNodes[idx]);
    }

    void JumpToNode(BaseStoryNode node)
    {
        var sm = StoryManager.Instance;
        if (sm == null) return;

        Hide();
        sm.ProcessNode(node);
        Debug.Log($"[DevMode] Прыжок → {node.GetType().Name}");
    }

    // ── Валюта ────────────────────────────────────────────────

    void AddCandles()
    {
        if (!PrototypeFeatureFlags.DevCurrencyToolsEnabled)
        {
            Debug.LogWarning("[DevMode] Prototype currency tools are disabled.");
            return;
        }

        if (addCandlesInput == null) return;
        if (!int.TryParse(addCandlesInput.text, out int amount)) amount = 10;
        PlayerData.AddCandlesValue(amount);
        Debug.Log($"[DevMode] +{amount} свечей → {PlayerData.Candles}");
        RefreshStatus();
    }

    void AddHearts()
    {
        if (!PrototypeFeatureFlags.DevCurrencyToolsEnabled)
        {
            Debug.LogWarning("[DevMode] Prototype currency tools are disabled.");
            return;
        }

        if (addHeartsInput == null) return;
        if (!int.TryParse(addHeartsInput.text, out int amount)) amount = 5;
        PlayerData.AddHeartValue(amount);
        Debug.Log($"[DevMode] +{amount} искр → {PlayerData.Hearts}");
        RefreshStatus();
    }

    // ── Статы ─────────────────────────────────────────────────

    void SetStat()
    {
        if (statKeyInput == null || statValueInput == null) return;

        string key = statKeyInput.text.Trim();
        if (string.IsNullOrEmpty(key)) return;

        if (!int.TryParse(statValueInput.text, out int value)) value = 0;

        GameState.Instance?.SetInt(key, value);
        Debug.Log($"[DevMode] Стат {key} = {value}");
        RefreshStatus();
    }

    // ── Сброс ─────────────────────────────────────────────────

    void ResetProgress()
    {
        // Сбрасываем только прогресс текущей истории
        var sm = StoryManager.Instance;
        if (sm != null)
        {
            sm.StopAllCoroutines();
            sm.CloseEndPanel();
        }

        StoryProgressResetUtility.ResetLocalProgress(
            sm != null ? sm.storyData : null,
            sm != null ? sm.CurrentStoryId : "");

        Debug.Log("[DevMode] Прогресс сброшен");
        Hide();

        // Перезапустить историю с начала
        if (sm != null)
            sm.StartStory();
    }

    void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[DevMode] Все данные удалены. Перезапустите приложение.");
        Hide();

        // Перезагружаем сцену 0
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}

#endif
