using System;
using System.Collections.Generic;
using UnityEngine;

public enum FirstLaunchInfoDeclineAction
{
    HideOnly = 0,
    QuitApplication = 1,
    OpenUrl = 2
}

[Serializable]
public sealed class FirstLaunchInfoLinkConfig
{
    [SerializeField]
    [Tooltip("Текст на кнопке ссылки. Например: Политика конфиденциальности, Условия использования, Поддержка.")]
    private string _label = "Открыть";

    [SerializeField]
    [Tooltip("URL, который откроется через Application.OpenURL. Можно оставить пустым, если кнопка ссылки на этой странице не нужна.")]
    private string _url;

    public string Label => string.IsNullOrWhiteSpace(_label) ? "Открыть" : _label;
    public string Url => _url ?? "";
    public bool IsValid => !string.IsNullOrWhiteSpace(_url);
}

[Serializable]
public sealed class FirstLaunchInfoPageConfig
{
    [SerializeField]
    [Tooltip("Включена ли эта плашка в цепочке. Удобно временно выключить страницу без удаления текста.")]
    private bool _enabled = true;

    [SerializeField]
    [Tooltip("Стабильный ID плашки для логов и событий. Например: terms, privacy, age, consent.")]
    private string _pageId = "terms";

    [SerializeField]
    [Tooltip("Заголовок плашки. Скрипт подставит его в TMP_Text заголовка.")]
    private string _title = "Условия использования";

    [SerializeField, TextArea(8, 30)]
    [Tooltip("Основной текст плашки. Сюда можно вставлять юридический текст, правила, предупреждения и любую служебную информацию. Поддерживает TMP rich text.")]
    private string _body = "Нажимая “Принять”, вы подтверждаете согласие с Условиями пользования и Политикой конфиденциальности. Приложение содержит романтический контент. Возрастное ограничение 16+\n\nПосле принятия приложение может попросить доступ к файлам и медиа на телефоне. Это нужно только для функций, где вы сами выбираете файл или отправляете диагностику.";

    [SerializeField]
    [Tooltip("Текст основной кнопки именно на этой странице. Если пусто, контроллер возьмет общий текст: Далее или Принять.")]
    private string _primaryButtonText = "Принять";

    [SerializeField]
    [Tooltip("Если включено, перед переходом дальше игрок должен включить Toggle подтверждения.")]
    private bool _requireToggle;

    [SerializeField]
    [Tooltip("Текст рядом с Toggle подтверждения. Например: Я прочитал(а) и принимаю условия.")]
    private string _toggleText = "Я прочитал(а) и принимаю условия.";

    [SerializeField]
    [Tooltip("Если включено, основная кнопка станет доступна только после прокрутки текста до конца.")]
    private bool _requireScrollToBottom;

    [SerializeField, Range(0f, 0.25f)]
    [Tooltip("Насколько близко ScrollRect должен быть к низу, чтобы страница считалась прочитанной. 0.03 обычно достаточно.")]
    private float _scrollBottomThreshold = 0.03f;

    [SerializeField]
    [Tooltip("Дополнительные ссылки для этой плашки. Контроллер разложит их по назначенным Link Buttons.")]
    private List<FirstLaunchInfoLinkConfig> _links = new List<FirstLaunchInfoLinkConfig>();

    public bool Enabled => _enabled;
    public string PageId => SaveDataSanitizer.SafeKeyPart(_pageId, "page", 64);
    public string Title => _title ?? "";
    public string Body => _body ?? "";
    public string PrimaryButtonText => _primaryButtonText ?? "";
    public bool RequireToggle => _requireToggle;
    public string ToggleText => string.IsNullOrWhiteSpace(_toggleText) ? "Я прочитал(а) и принимаю условия." : _toggleText;
    public bool RequireScrollToBottom => _requireScrollToBottom;
    public float ScrollBottomThreshold => Mathf.Clamp(_scrollBottomThreshold, 0f, 0.25f);
    public IReadOnlyList<FirstLaunchInfoLinkConfig> Links => _links ?? EmptyLinks;

    private static readonly IReadOnlyList<FirstLaunchInfoLinkConfig> EmptyLinks = Array.Empty<FirstLaunchInfoLinkConfig>();
}

[CreateAssetMenu(fileName = "First Launch Info Flow", menuName = "Nocturne/UI/First Launch Info Flow")]
public sealed class FirstLaunchInfoFlowConfig : ScriptableObject
{
    private const string DefaultFlowId = "legal";

    [Header("Ключ сохранения")]
    [SerializeField]
    [Tooltip("Стабильный ID этого набора плашек. Входит в ключ сохранения принятия. Например: legal, beta_notice, age_gate.")]
    private string _flowId = DefaultFlowId;

    [SerializeField, Min(1)]
    [Tooltip("Ревизия текста/условий. Увеличь число, если пользователь должен увидеть и принять обновленные условия заново.")]
    private int _revision = 1;

    [SerializeField]
    [Tooltip("Если включено, принятие сохраняется в LocalSecurePrefs. Если выключено, flow будет показываться каждый запуск.")]
    private bool _rememberAcceptance = true;

    [SerializeField]
    [Tooltip("В Editor и Development Build показывать плашки при каждом входе, даже если они уже приняты. Удобно для настройки UI.")]
    private bool _showEveryPlayInDebug = true;

    [Header("Отказ")]
    [SerializeField]
    [Tooltip("Показывать кнопку отказа. Для юридических условий обычно включают и ставят действие QuitApplication.")]
    private bool _allowDecline;

    [SerializeField]
    [Tooltip("Текст кнопки отказа.")]
    private string _declineButtonText = "Отказаться";

    [SerializeField]
    [Tooltip("Что делать при отказе: просто скрыть плашки, выйти из приложения или открыть ссылку.")]
    private FirstLaunchInfoDeclineAction _declineAction = FirstLaunchInfoDeclineAction.QuitApplication;

    [SerializeField]
    [Tooltip("URL для действия OpenUrl при отказе. Можно оставить пустым, если действие не OpenUrl.")]
    private string _declineUrl;

    [Header("Плашки")]
    [SerializeField]
    [Tooltip("Список плашек, которые игрок увидит по порядку. Текст и требования задаются отдельно для каждой страницы.")]
    private List<FirstLaunchInfoPageConfig> _pages = new List<FirstLaunchInfoPageConfig>();

    public string FlowId => SaveDataSanitizer.SafeKeyPart(_flowId, DefaultFlowId, 96);
    public int Revision => Mathf.Max(1, _revision);
    public bool RememberAcceptance => _rememberAcceptance;
    public bool ShowEveryPlayInDebug => _showEveryPlayInDebug;
    public bool AllowDecline => _allowDecline;
    public string DeclineButtonText => string.IsNullOrWhiteSpace(_declineButtonText) ? "Отказаться" : _declineButtonText;
    public FirstLaunchInfoDeclineAction DeclineAction => _declineAction;
    public string DeclineUrl => _declineUrl ?? "";
    public IReadOnlyList<FirstLaunchInfoPageConfig> Pages => _pages ?? EmptyPages;

    private static readonly IReadOnlyList<FirstLaunchInfoPageConfig> EmptyPages = Array.Empty<FirstLaunchInfoPageConfig>();

    public string AcceptanceKey => "first_launch_info:" + FlowId + ":rev_" + Revision;
    public string AcceptancePurpose => LocalSaveSecurity.SetupFlagPurpose + ":first_launch_info:" + FlowId;

    public int CountEnabledPages()
    {
        int count = 0;
        IReadOnlyList<FirstLaunchInfoPageConfig> pages = Pages;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] != null && pages[i].Enabled)
                count++;
        }

        return count;
    }

    private void OnValidate()
    {
        _flowId = SaveDataSanitizer.SafeKeyPart(_flowId, DefaultFlowId, 96);
        _revision = Mathf.Max(1, _revision);
        _pages ??= new List<FirstLaunchInfoPageConfig>();
    }
}
