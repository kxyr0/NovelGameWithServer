using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Панель «Сообщить об ошибке».
///
/// Подключение:
/// 1. Создай Canvas-панель "BugReportPanel".
/// 2. Прикрепи скрипт BugReportPanel.
/// 3. Назначь поля:
///    - panel              — корневой GameObject панели
///    - descriptionInput   — TMP_InputField для описания ошибки
///    - sendButton         — Button отправки
///    - cancelButton       — Button отмены / закрытия
///    - successLabel       — TMP_Text "Спасибо! Мы рассмотрим обращение" (скрыт по умолчанию)
///    - charCountText      — TMP_Text счётчика символов (необязательно)
///
/// Настройки отправки (заполни в инспекторе):
///    - reportMethod: Email / TelegramBot / CustomUrl
///    - emailAddress    — для метода Email
///    - telegramBotUrl  — для TelegramBot (webhook-url)
///    - customReportUrl — для CustomUrl (открывается браузер)
/// </summary>
public class BugReportPanel : MonoBehaviour
{
    public static BugReportPanel Instance { get; private set; }

    // ── Метод отправки ───────────────────────────────────────
    public enum ReportMethod
    {
        /// <summary>Открывает почтовый клиент с предзаполненным письмом</summary>
        Email,
        /// <summary>Открывает URL в браузере (Google Form, Typeform, etc.)</summary>
        CustomUrl,
        /// <summary>Локальный лог — сохраняет в PlayerPrefs и показывает "Спасибо"</summary>
        LocalOnly
    }

    [Header("UI")]
    [Tooltip("Корневой GameObject панели. Скрипт включает и выключает его при Show/Hide.")]
    public GameObject panel;
    [Tooltip("Поле, куда игрок вводит описание ошибки.")]
    public TMP_InputField descriptionInput;
    [Tooltip("Кнопка отправки отчёта об ошибке.")]
    public Button sendButton;
    [Tooltip("Кнопка закрытия панели без отправки отчёта.")]
    public Button cancelButton;
    [Tooltip("Текст успешной отправки. Обычно его нужно скрыть в инспекторе по умолчанию.")]
    public TMP_Text successLabel;
    [Tooltip("Необязательный счётчик введённых символов.")]
    public TMP_Text charCountText;
    [Tooltip("TMP_Text внутри кнопки отправки.")]
    public TMP_Text sendButtonText;

    [Header("CanvasGroup для fade")]
    public CanvasGroup canvasGroup;

    [Header("Метод отправки")]
    public ReportMethod reportMethod = ReportMethod.Email;

    [Tooltip("Email-адрес, на который отправляется отчёт при методе Email.")]
    public string emailAddress = "support@yourgame.com";

    [Tooltip("Тема письма для отчёта об ошибке.")]
    public string emailSubject = "Ошибка в приложении";

    [Tooltip("URL формы или сервера, куда отправляется отчёт при методе CustomUrl.")]
    public string customReportUrl = "https://forms.gle/yourform";

    [Header("Ограничения")]
    [Tooltip("Минимальное число символов, после которого кнопка отправки становится активной.")]
    public int minChars = 10;
    [Tooltip("Максимальное число символов в описании ошибки.")]
    public int maxChars = 500;

    // ── Состояние ────────────────────────────────────────────
    bool _sending = false;
    Tween _fadeTween;
    Tween _closeDelayTween;
    string _defaultSendButtonLabel = "Отправить";

    public bool IsVisible => panel != null && panel.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (panel != null) panel.SetActive(false);
    }

    void OnValidate()
    {
        maxChars = Mathf.Clamp(maxChars, 1, 5000);
        minChars = Mathf.Clamp(minChars, 0, maxChars);
    }

    void OnDestroy()
    {
        if (descriptionInput != null)
            descriptionInput.onValueChanged.RemoveListener(OnInputChanged);

        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnSend);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Hide);

        StopUiEffects();

        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        StopUiEffects();

        if (panel != null)
            panel.SetActive(false);
    }

    void Start()
    {
        if (sendButtonText != null && !string.IsNullOrWhiteSpace(sendButtonText.text))
            _defaultSendButtonLabel = sendButtonText.text;

        if (descriptionInput != null)
        {
            descriptionInput.characterLimit = maxChars;
            descriptionInput.onValueChanged.RemoveListener(OnInputChanged);
            descriptionInput.onValueChanged.AddListener(OnInputChanged);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSend);
            sendButton.onClick.AddListener(OnSend);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
            cancelButton.onClick.AddListener(Hide);
        }

        UpdateSendButton("");
    }

    // ── Публичный API ────────────────────────────────────────

    public void Show()
    {
        if (panel == null) return;

        StopUiEffects();
        _sending = false;
        panel.SetActive(true);

        if (descriptionInput != null) descriptionInput.text = "";
        if (successLabel != null) successLabel.gameObject.SetActive(false);
        if (descriptionInput != null) descriptionInput.gameObject.SetActive(true);
        if (sendButton != null) sendButton.gameObject.SetActive(true);
        if (sendButtonText != null) sendButtonText.text = _defaultSendButtonLabel;
        if (charCountText != null) charCountText.text = $"0/{maxChars}";

        UpdateSendButton("");

        if (canvasGroup != null)
        {
            _fadeTween?.Kill();
            canvasGroup.alpha = 0f;
            _fadeTween = canvasGroup.DOFade(1f, 0.2f);
        }

        // Фокус на поле ввода
        if (descriptionInput != null)
            descriptionInput.Select();
    }

    public void Hide()
    {
        if (panel == null) return;

        _closeDelayTween?.Kill();
        _closeDelayTween = null;

        if (canvasGroup != null)
        {
            _fadeTween?.Kill();
            _fadeTween = canvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
            {
                if (panel != null)
                    panel.SetActive(false);
            });
        }
        else
        {
            panel.SetActive(false);
        }
    }

    // ── Внутренние методы ────────────────────────────────────

    void OnInputChanged(string text)
    {
        text ??= "";
        UpdateSendButton(text);

        if (charCountText != null)
            charCountText.text = $"{text.Length}/{maxChars}";
    }

    void UpdateSendButton(string text)
    {
        text ??= "";
        if (sendButton != null)
            sendButton.interactable = !_sending && text.Length >= minChars;
    }

    void OnSend()
    {
        if (_sending) return;

        string description = descriptionInput != null ? descriptionInput.text.Trim() : "";
        if (description.Length < minChars) return;

        _sending = true;
        if (sendButton != null) sendButton.interactable = false;
        if (sendButtonText != null) sendButtonText.text = "Отправка...";

        try
        {
            switch (reportMethod)
            {
                case ReportMethod.Email:
                    if (string.IsNullOrWhiteSpace(emailAddress))
                    {
                        FailSend("BugReportPanel: emailAddress is empty.");
                        return;
                    }

                    SendViaEmail(description);
                    break;
                case ReportMethod.CustomUrl:
                    if (string.IsNullOrWhiteSpace(customReportUrl))
                    {
                        FailSend("BugReportPanel: customReportUrl is empty.");
                        return;
                    }

                    Application.OpenURL(customReportUrl);
                    ShowSuccess();
                    break;
                case ReportMethod.LocalOnly:
                    SaveLocalReport(description);
                    ShowSuccess();
                    break;
            }
        }
        catch (Exception exception)
        {
            FailSend($"BugReportPanel: failed to send report: {exception.Message}");
        }
    }

    void SendViaEmail(string description)
    {
        string body = BuildEmailBody(description);

        // URL-encode основные символы
        string encodedSubject = Uri.EscapeUriString(emailSubject);
        string encodedBody    = Uri.EscapeUriString(body);

        string mailto = $"mailto:{emailAddress}?subject={encodedSubject}&body={encodedBody}";

        Application.OpenURL(mailto);
        ShowSuccess();
    }

    string BuildEmailBody(string userDescription)
    {
        var sb = new StringBuilder();
        sb.AppendLine(userDescription);
        sb.AppendLine();
        sb.AppendLine("--- Системная информация ---");
        sb.AppendLine($"Устройство: {SystemInfo.deviceModel}");
        sb.AppendLine($"ОС: {SystemInfo.operatingSystem}");
        sb.AppendLine($"Unity: {Application.unityVersion}");
        sb.AppendLine($"Версия приложения: {Application.version}");

        var storyData = StoryManager.Instance?.storyData;
        if (storyData != null)
            sb.AppendLine($"История: {storyData.storyName} ({storyData.storyId})");

        var node = GameState.Instance?.currentNode;
        if (node != null)
            sb.AppendLine($"Нода: {node.guid}");

        return sb.ToString();
    }

    void SaveLocalReport(string description)
    {
        // Сохраняем в PlayerPrefs как лог (для отладки)
        string key = $"BugReport_{DateTime.Now:yyyyMMdd_HHmmss}";
        PlayerPrefs.SetString(key, description);
        PlayerPrefs.Save();
        Debug.Log($"[BugReport] Сохранён локально: {key}\n{description}");
    }

    void ShowSuccess()
    {
        _sending = false;

        if (descriptionInput != null) descriptionInput.gameObject.SetActive(false);
        if (sendButton != null) sendButton.gameObject.SetActive(false);
        if (sendButtonText != null) sendButtonText.text = _defaultSendButtonLabel;

        if (successLabel != null)
        {
            successLabel.gameObject.SetActive(true);
            successLabel.text = "Спасибо! Мы рассмотрим обращение.";
        }

        // Через 2 секунды закрываем
        _closeDelayTween?.Kill();
        _closeDelayTween = DOVirtual.DelayedCall(2f, Hide);
    }

    void FailSend(string message)
    {
        Debug.LogWarning(message, this);
        _sending = false;

        if (sendButtonText != null)
            sendButtonText.text = _defaultSendButtonLabel;

        UpdateSendButton(descriptionInput != null ? descriptionInput.text : "");
    }

    void StopUiEffects()
    {
        _fadeTween?.Kill();
        _fadeTween = null;

        _closeDelayTween?.Kill();
        _closeDelayTween = null;
    }
}
