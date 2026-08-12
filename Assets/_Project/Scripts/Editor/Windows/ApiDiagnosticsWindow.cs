using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ApiDiagnosticsWindow : EditorWindow
{
    private static readonly Regex SecretPattern = new Regex(
        "(?i)([\"']?\\b(password|token|accessToken|authToken|refreshToken|idToken|restoreCode|secret|apiKey|adminKey|x-admin-key|authorization|cookie|privateKey|session|jwt|purchaseToken|restoreToken|receipt|signature)\\b[\"']?\\s*[:=]\\s*)(\"[^\"]*\"|'[^']*'|[^\\s,;}\\]]+)",
        RegexOptions.Compiled);
    private static readonly Regex BearerPattern = new Regex("(?i)Bearer\\s+[A-Za-z0-9\\-._~+/]+=*", RegexOptions.Compiled);

    private const int MaxResponsePreviewChars = 700;
    private const int MaxUiTextDraftChars = 4000;
    private const string AdminKeyEnvironmentVariable = "NOCTURNEDC_ADMIN_KEY";

    private string _baseUrl = ApiRoutes.BaseUrl;
    private string _adminKey = "";
    private string _manualBearerToken = "";
    private string _deviceId = "unity-api-check";
    private string _episodeId = "";
    private string _uiTextId = "cat_phase";
    private string _uiTextScreenId = "MainScreen";
    private string _uiTextStoryId = "";
    private string _uiTextLocale = "ru";
    private string _uiTextDraftText = "Тестовый текст пришел с сервера.";
    private bool _uiTextDraftEnabled = true;
    private int _uiTextScreenPresetIndex;
    private string _manualPath = ApiRoutes.ContentCatalog;
    private string _manualBody = "{}";
    private int _manualMethodIndex;
    private bool _allowWriteProbes;
    private bool _isBusy;
    private Vector2 _scroll;
    private string _lastResult = "Проверка еще не запускалась.";

    private static readonly string[] ManualMethods = { "GET", "POST", "DELETE" };
    private static readonly string[] UiTextScreenPresets = { "MainScreen", "StoryScreen", "ShopScreen", "WardrobeScreen", "EndScreen" };

    [MenuItem("Инструменты/Nocturnal/Проверка API", priority = 21)]
    [MenuItem("VN/Network/API Diagnostics")]
    public static void Open()
    {
        GetWindow<ApiDiagnosticsWindow>("Проверка API");
    }

    private void OnGUI()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;

            EditorGUILayout.LabelField("Проверка API Nocturnal", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Это окно проверяет, что backend отвечает по документированным маршрутам и что Unity может разобрать каталог. Безопасная проверка не публикует контент и не меняет каталог.",
                MessageType.Info);

            EditorGUIUtility.labelWidth = 190f;
            using (new EditorGUI.DisabledScope(_isBusy))
            {
                DrawConnectionFields();
                DrawMainActions();
                DrawManualProbe();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(_isBusy ? "Проверка выполняется..." : "Последний отчет", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_lastResult, GUILayout.MinHeight(260f));
        }
    }

    private void DrawConnectionFields()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Подключение", EditorStyles.boldLabel);
        _baseUrl = EditorGUILayout.TextField(new GUIContent("Адрес API", "Например https://nocturnedc.ru или локальный http://127.0.0.1:52206."), CleanDefault(_baseUrl, ApiRoutes.BaseUrl));
        _adminKey = EditorGUILayout.PasswordField(new GUIContent("X-Admin-Key", "Нужен только для admin/Unity publisher read-only проверки. Можно не вводить, если задан NOCTURNEDC_ADMIN_KEY."), _adminKey);
        _deviceId = EditorGUILayout.TextField(new GUIContent("Device ID теста", "Техническое имя тестового гостевого устройства. По нему backend выдаст JWT."), CleanDefault(_deviceId, "unity-api-check"));
        _episodeId = EditorGUILayout.TextField(new GUIContent("ID эпизода", "Необязательно. Если пусто, диагностика возьмет первый эпизод из каталога."), _episodeId);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Тексты UI", EditorStyles.boldLabel);
        int nextScreenPreset = GUILayout.Toolbar(_uiTextScreenPresetIndex, UiTextScreenPresets, GUILayout.MaxWidth(700f));
        if (nextScreenPreset != _uiTextScreenPresetIndex)
        {
            _uiTextScreenPresetIndex = nextScreenPreset;
            _uiTextScreenId = UiTextScreenPresets[_uiTextScreenPresetIndex];
        }

        _uiTextId = EditorGUILayout.TextField(new GUIContent("ID текста", "Например cat_phase. Именно этот id будет проверен на визуальное отображение."), CleanDefault(_uiTextId, "cat_phase"));
        _uiTextScreenId = EditorGUILayout.TextField(new GUIContent("Screen ID", "Экран, для которого запрашивается текст. Например MainScreen."), CleanDefault(_uiTextScreenId, "MainScreen"));
        _uiTextStoryId = EditorGUILayout.TextField(new GUIContent("Story ID", "Можно оставить пустым, если текст общий для всех историй."), _uiTextStoryId ?? "");
        _uiTextLocale = EditorGUILayout.TextField(new GUIContent("Локаль", "Обычно ru."), CleanDefault(_uiTextLocale, "ru"));
        _uiTextDraftEnabled = EditorGUILayout.ToggleLeft(new GUIContent("Текст включен", "Если выключено, сервер вернет enabled=false, а Unity должна скрыть этот текст."), _uiTextDraftEnabled);
        EditorGUILayout.LabelField(new GUIContent("Текст для записи", "Этот текст будет отправлен на тестовый сервер через /admin/ui-texts, потом прочитан через /content/ui-texts."));
        _uiTextDraftText = EditorGUILayout.TextArea(_uiTextDraftText ?? "", GUILayout.MinHeight(52f));
    }

    private void DrawMainActions()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Основные проверки", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("Проверить контракт в коде", "Проверяет локальную таблицу API: runtime не может ходить в admin/Unity publisher, защищенные маршруты требуют JWT."))
                ValidateLocalContract();

            if (Button("Проверить player/content API", "Создает гостевой JWT и прогоняет безопасные маршруты чтения: каталог, профиль, баланс, магазин, graph/version эпизода."))
                StartProbe(RunRuntimeReadOnlyProbe());
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("Проверить admin API", "Проверяет только чтение: /admin/me, /admin/catalog, /unity/choice-costs, /unity/wardrobe-costs. Ничего не публикует."))
                StartProbe(RunAdminReadOnlyProbe());

            if (Button("Проверить API карточек", "Создает гостевой JWT, проверяет /player/tarot/status и /player/tarot/draw, показывает какой текст карты Unity возьмет из description."))
                StartProbe(RunTarotCardApiProbe());
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("Проверить API текстов", "Создает гостевой JWT, проверяет /content/ui-texts и показывает, будет ли выбранный текст виден в UI."))
                StartProbe(RunUiTextsApiProbe());

            if (Button("Записать и проверить текст", "Отправляет текст на тестовый backend через /admin/ui-texts, затем читает его обычным runtime-запросом /content/ui-texts."))
                StartProbe(RunUiTextUpsertProbe(true));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("Только записать текст", "Отправляет текст на backend через /admin/ui-texts без последующей проверки чтения. Для локального mock X-Admin-Key не нужен."))
                StartProbe(RunUiTextUpsertProbe(false));

            if (Button("Скопировать отчет", "Копирует последний отчет без секретов."))
                EditorGUIUtility.systemCopyBuffer = _lastResult;
        }
    }

    private void DrawManualProbe()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Разовый запрос", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Нужен для ручной проверки одного маршрута. Токены и ключи в отчете скрываются.",
            MessageType.None);

        _manualMethodIndex = GUILayout.Toolbar(_manualMethodIndex, ManualMethods, GUILayout.MaxWidth(360f));
        _manualPath = EditorGUILayout.TextField(new GUIContent("Путь", "Например /content/catalog или /player/balance."), CleanDefault(_manualPath, ApiRoutes.ContentCatalog));
        _manualBearerToken = EditorGUILayout.PasswordField(new GUIContent("Bearer JWT", "Не нужен, если используете кнопку полной проверки: она сама получает гостевой JWT."), _manualBearerToken);

        using (new EditorGUI.DisabledScope(ManualMethods[_manualMethodIndex] == "GET" || ManualMethods[_manualMethodIndex] == "DELETE"))
        {
            _manualBody = EditorGUILayout.TextField(new GUIContent("JSON body", "Тело POST-запроса."), CleanDefault(_manualBody, "{}"));
        }

        _allowWriteProbes = EditorGUILayout.ToggleLeft(
            new GUIContent("Разрешить тестовые POST-запросы", "Включайте только если понимаете, что POST может изменить данные гостевого игрока."),
            _allowWriteProbes);

        using (new EditorGUI.DisabledScope(IsManualWriteBlocked()))
        {
            if (Button("Отправить разовый запрос", "Отправляет один выбранный запрос."))
                StartProbe(RunManualProbe());
        }
    }

    private bool IsManualWriteBlocked()
    {
        string method = ManualMethods[Mathf.Clamp(_manualMethodIndex, 0, ManualMethods.Length - 1)];
        return method == "POST" && !_allowWriteProbes;
    }

    private bool Button(string text, string tooltip)
    {
        return GUILayout.Button(new GUIContent(text, tooltip), GUILayout.MinWidth(190f), GUILayout.MaxWidth(280f), GUILayout.Height(34f));
    }

    private void ValidateLocalContract()
    {
        int documented = 0;
        int runtimeAllowed = 0;
        int legacy = 0;
        foreach (ApiEndpoint endpoint in ApiContract.AllEndpoints)
        {
            if (endpoint.Documented)
                documented++;
            if (endpoint.RuntimeAllowed)
                runtimeAllowed++;
            if (endpoint.Kind == ApiEndpointKind.Legacy)
                legacy++;
        }

        _lastResult =
            "Локальный контракт API: OK\n" +
            "Документация: " + ApiContract.DocumentationUrl + "\n" +
            "Отслеживаемых документированных маршрутов: " + documented + "\n" +
            "Маршрутов, разрешенных runtime-клиенту: " + runtimeAllowed + "\n" +
            "Legacy/недокументированных рисков: " + legacy + "\n" +
            "Runtime блокирует admin: " + YesNo(!ApiContract.IsRuntimeAllowed("POST", "/admin/catalog/story")) + "\n" +
            "Runtime блокирует Unity publisher: " + YesNo(!ApiContract.IsRuntimeAllowed("POST", ApiRoutes.UnityChoiceCosts)) + "\n" +
            "Protected /content/catalog требует JWT: " + YesNo(ApiContract.RequiresBearerToken("GET", ApiRoutes.ContentCatalog));
    }

    private void StartProbe(IEnumerator routine)
    {
        _isBusy = true;
        _lastResult = "Проверка запущена...";
        Repaint();
        EditorCoroutineRunner.Start(WrapProbe(routine));
    }

    private IEnumerator WrapProbe(IEnumerator routine)
    {
        while (routine != null && routine.MoveNext())
            yield return routine.Current;

        _isBusy = false;
        Repaint();
    }

    private IEnumerator RunRuntimeReadOnlyProbe()
    {
        var report = new ApiProbeReport("Безопасная проверка player/content API", CleanBaseUrl(_baseUrl));

        ProbeResult auth = null;
        yield return SendProbe(
            "POST",
            ApiRoutes.AuthGuest,
            BuildGuestAuthJson(),
            "",
            "",
            result => auth = result);
        report.Add("Гостевая авторизация", auth, true, "Нужна, чтобы получить JWT для player/content маршрутов.");

        string token = ExtractAuthToken(auth != null ? auth.Body : "");
        if (string.IsNullOrWhiteSpace(token))
        {
            report.Fail("JWT не получен. Дальше player/content API проверить нельзя.");
            FinishReport(report);
            yield break;
        }

        ProbeResult catalog = null;
        yield return SendProbe("GET", ApiRoutes.ContentCatalog, null, token, "", result => catalog = result);
        report.Add("Каталог контента", catalog, true, "Unity должна получить и разобрать список сезонов/эпизодов.");

        string selectedEpisodeId = SaveDataSanitizer.SanitizeIdentifier(_episodeId);
        if (catalog != null && catalog.IsHttpOk)
        {
            List<CatalogSeasonResponse> seasons = NetworkManager.ParseCatalogResponse(catalog.Body);
            int episodeCount = CountEpisodes(seasons);
            report.Info("Каталог разобран Unity-парсером: сезонов " + seasons.Count + ", эпизодов " + episodeCount + ".");

            if (string.IsNullOrWhiteSpace(selectedEpisodeId))
                selectedEpisodeId = PickEpisodeId(seasons);

            if (episodeCount == 0)
                report.Warn("Каталог пришел, но эпизодов в нем нет. API живой, но играть с сервера нечего.");
        }

        if (!string.IsNullOrWhiteSpace(selectedEpisodeId))
        {
            report.Info("Эпизод для проверки JSON: " + selectedEpisodeId);
            yield return AddRuntimeProbe(report, "Версия JSON эпизода", ApiRoutes.ContentEpisodeVersion(selectedEpisodeId), token, true);
            yield return AddRuntimeProbe(report, "Graph JSON эпизода", ApiRoutes.ContentEpisodeGraph(selectedEpisodeId), token, true);
        }
        else
        {
            report.Warn("ID эпизода не найден. Проверка /content/episode/{id}/version и /graph пропущена.");
        }

        yield return AddRuntimeProbe(report, "Профиль игрока", ApiRoutes.PlayerProfile, token, true);
        yield return AddRuntimeProbe(report, "Фичи игрока", ApiRoutes.PlayerFeatures, token, true);
        yield return AddRuntimeProbe(report, "Баланс", ApiRoutes.PlayerBalance, token, true);
        yield return AddRuntimeProbe(report, "Прогресс", ApiRoutes.PlayerProgress, token, true);
        yield return AddRuntimeProbe(report, "Закладка", ApiRoutes.PlayerBookmark, token, false);
        yield return AddRuntimeProbe(report, "Гардероб", ApiRoutes.PlayerWardrobe, token, true);
        yield return AddRuntimeProbe(report, "Галерея", ApiRoutes.PlayerGallery, token, true);
        yield return AddRuntimeProbe(report, "Слоты сохранений", ApiRoutes.PlayerSlots, token, true);
        yield return AddRuntimeProbe(report, "Сцены эпизода", ApiRoutes.PlayerScenesViewedForEpisode(selectedEpisodeId), token, false);
        yield return AddRuntimeProbe(report, "Таро статус", ApiRoutes.PlayerTarotStatus, token, true);
        yield return AddRuntimeProbe(report, "Кости статус", ApiRoutes.PlayerDiceStatus, token, true);
        yield return AddRuntimeProbe(report, "Имя кота", ApiRoutes.PlayerCatName, token, false);
        yield return AddRuntimeProbe(report, "Избранное", ApiRoutes.PlayerFavorites, token, true);
        yield return AddRuntimeProbe(report, "Отношения", ApiRoutes.PlayerRelationships, token, true);
        yield return AddRuntimeProbe(report, "Цены магазина", ApiRoutes.ShopPrices, token, true);
        yield return AddRuntimeProbe(report, "Товары магазина", ApiRoutes.ShopItems, token, true);
        yield return AddRuntimeProbe(report, "История покупок", ApiRoutes.PurchasesHistory, token, false);
        yield return AddRuntimeProbe(report, "Продукты покупок", ApiRoutes.PurchasesProducts, token, false);
        yield return AddRuntimeProbe(report, "Статистика чтения", ApiRoutes.PlayerReadingStats, token, false);

        FinishReport(report);
    }

    private IEnumerator RunAdminReadOnlyProbe()
    {
        var report = new ApiProbeReport("Проверка admin/Unity publisher API только на чтение", CleanBaseUrl(_baseUrl));
        string key = FirstNonEmpty(_adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable));
        if (string.IsNullOrWhiteSpace(key) && !IsLoopbackBaseUrl(_baseUrl))
        {
            report.Fail("X-Admin-Key не указан. Введите его в поле или задайте переменную окружения " + AdminKeyEnvironmentVariable + ".");
            FinishReport(report);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(key))
            report.Warn("X-Admin-Key не указан, но адрес локальный. Для локального mock это допустимо, для реального сервера нет.");

        yield return AddAdminProbe(report, "Проверка admin сессии", "/admin/me", key, true);
        yield return AddAdminProbe(report, "Admin каталог", "/admin/catalog", key, true);
        yield return AddAdminProbe(report, "Цены выборов Unity", ApiRoutes.UnityChoiceCosts, key, true);
        yield return AddAdminProbe(report, "Цены гардероба Unity", ApiRoutes.UnityWardrobeCosts, key, true);
        yield return AddAdminProbe(report, "Media список", "/admin/media", key, false);

        FinishReport(report);
    }

    private IEnumerator RunTarotCardApiProbe()
    {
        var report = new ApiProbeReport("Проверка API карточек", CleanBaseUrl(_baseUrl));

        ProbeResult auth = null;
        yield return SendProbe(
            "POST",
            ApiRoutes.AuthGuest,
            BuildGuestAuthJson(),
            "",
            "",
            result => auth = result);
        report.Add("Гостевая авторизация", auth, true, "Нужна, чтобы получить JWT для карточного API.");

        string token = ExtractAuthToken(auth != null ? auth.Body : "");
        if (string.IsNullOrWhiteSpace(token))
        {
            report.Fail("JWT не получен. Дальше /player/tarot/status и /player/tarot/draw проверить нельзя.");
            FinishReport(report);
            yield break;
        }

        ProbeResult status = null;
        yield return SendProbe("GET", ApiRoutes.PlayerTarotStatus, null, token, "", result => status = result);
        report.Add("Статус вытягивания карты", status, true, "Проверяет canDraw/cooldown перед визуальным draw.");

        DivinationTarotStatusResponseDto parsedStatus =
            status != null && status.IsHttpOk
                ? DivinationBackendJsonParser.ParseStatusResponse(status.Body)
                : null;
        if (parsedStatus != null)
        {
            report.Info("Cooldown доступен: " + YesNo(parsedStatus.IsDrawAvailable(true)) +
                ", remainingSeconds=" + parsedStatus.remainingSeconds + ".");
        }

        ProbeResult draw = null;
        yield return SendProbe("POST", ApiRoutes.PlayerTarotDraw, "{}", token, "", result => draw = result);
        report.Add("Вытянуть карту", draw, true, "Проверяет фактический ответ с card.description/resultText/reward.");

        DivinationTarotDrawResponseDto parsedDraw =
            draw != null && draw.IsHttpOk
                ? DivinationBackendJsonParser.ParseDrawResponse(draw.Body)
                : null;
        DivinationCardBackendDto card = parsedDraw != null ? parsedDraw.SelectedCard : null;
        if (card == null)
        {
            report.Fail("Карта не распарсилась. UI не сможет показать карточку.");
            FinishReport(report);
            yield break;
        }

        report.Info("Карта: id=" + FirstNonEmpty(card.EffectiveId, "нет") +
            ", name=" + FirstNonEmpty(card.EffectiveTitle, "нет") + ".");
        report.Info("description: " + FirstNonEmpty(card.description, "пусто"));
        report.Info("resultText: " + FirstNonEmpty(card.resultText, "пусто"));
        report.Info("Визуальный текст Unity: " + FirstNonEmpty(card.EffectiveDescription, "пусто"));

        if (string.IsNullOrWhiteSpace(card.EffectiveDescription))
            report.Fail("description пустой. По текущему правилу UI не должен брать resultText, значит текст карты будет пустым или fallback.");
        else
            report.Info("OK: UI берет текст из description. resultText не используется для отображения.");

        if (!string.IsNullOrWhiteSpace(card.resultText))
            report.Warn("Сервер вернул resultText, но Unity его игнорирует. Если нужен этот текст на карточке, backend должен положить его в description.");

        FinishReport(report);
    }

    private IEnumerator RunUiTextsApiProbe()
    {
        var report = new ApiProbeReport("Проверка API текстов UI", CleanBaseUrl(_baseUrl));
        yield return AddUiTextReadCheck(report);
        FinishReport(report);
    }

    private IEnumerator RunUiTextUpsertProbe(bool verifyAfterWrite)
    {
        var report = new ApiProbeReport("Запись тестового UI-текста", CleanBaseUrl(_baseUrl));
        string key = FirstNonEmpty(_adminKey, Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable));
        if (string.IsNullOrWhiteSpace(key) && !IsLoopbackBaseUrl(_baseUrl))
        {
            report.Fail("X-Admin-Key не указан. Для реального backend запись admin-текста должна быть защищена ключом. Для локального mock ключ не нужен.");
            FinishReport(report);
            yield break;
        }

        string body = BuildUiTextUpsertJson();
        ProbeResult write = null;
        yield return SendProbe("POST", "/admin/ui-texts", body, "", key, result => write = result);
        report.Add("Записать UI-текст", write, true, "Отправляет один тестовый текст в admin endpoint. Локальный mock сохраняет его в ui_texts.local.json.");

        if (write == null || !write.IsHttpOk)
        {
            if (write != null && write.StatusCode == 404)
                report.Info("Расшифровка 404: backend не знает маршрут /admin/ui-texts. Локальный mock уже умеет, а на реальном сервере нужен такой admin endpoint или другой согласованный путь записи текстов.");
            else if (write != null && write.StatusCode == 401)
                report.Info("Расшифровка 401: backend не принял X-Admin-Key.");

            FinishReport(report);
            yield break;
        }

        report.Info("Отправленный текст: id=" + SaveDataSanitizer.SanitizeIdentifier(_uiTextId) +
            ", enabled=" + YesNo(_uiTextDraftEnabled) +
            ", locale=" + FirstNonEmpty(SaveDataSanitizer.SanitizeIdentifier(_uiTextLocale), "ru") +
            ", screenId=" + FirstNonEmpty(SaveDataSanitizer.SanitizeIdentifier(_uiTextScreenId), "общий") +
            ", storyId=" + FirstNonEmpty(SaveDataSanitizer.SanitizeIdentifier(_uiTextStoryId), "общий") + ".");

        if (verifyAfterWrite)
            yield return AddUiTextReadCheck(report);

        FinishReport(report);
    }

    private IEnumerator AddUiTextReadCheck(ApiProbeReport report)
    {
        ProbeResult auth = null;
        yield return SendProbe(
            "POST",
            ApiRoutes.AuthGuest,
            BuildGuestAuthJson(),
            "",
            "",
            result => auth = result);
        report.Add("Гостевая авторизация", auth, true, "Нужна, чтобы получить JWT для /content/ui-texts.");

        string token = ExtractAuthToken(auth != null ? auth.Body : "");
        if (string.IsNullOrWhiteSpace(token))
        {
            report.Fail("JWT не получен. Дальше /content/ui-texts проверить нельзя.");
            yield break;
        }

        string textId = SaveDataSanitizer.SanitizeIdentifier(_uiTextId);
        string screenId = SaveDataSanitizer.SanitizeIdentifier(_uiTextScreenId);
        string storyId = SaveDataSanitizer.SanitizeIdentifier(_uiTextStoryId);
        string locale = SaveDataSanitizer.SanitizeIdentifier(_uiTextLocale);
        if (string.IsNullOrWhiteSpace(locale))
            locale = "ru";

        string path = ApiRoutes.ContentUiTextsQuery(screenId, storyId, locale);

        ProbeResult response = null;
        yield return SendProbe("GET", path, null, token, "", result => response = result);
        report.Add("Получить UI-тексты", response, true, "Проверяет items[] и совместимость с Unity-парсером RemoteUiTextBinder.");

        if (response == null || !response.IsHttpOk)
        {
            if (response != null && response.StatusCode == 404)
                report.Info("Расшифровка 404: Unity дошла до сервера, но backend не знает маршрут /content/ui-texts. Клиент и локальный mock готовы, нужен такой endpoint на реальном backend или другой согласованный путь.");
            else if (response != null && response.StatusCode == 401)
                report.Info("Расшифровка 401: сервер требует JWT или не принял гостевую авторизацию.");

            yield break;
        }

        List<RemoteUiTextItem> items = NetworkManager.ParseUiTextResponse(response.Body);
        report.Info("Unity-парсер прочитал items: " + items.Count + ".");
        for (int i = 0; i < items.Count && i < 12; i++)
        {
            RemoteUiTextItem item = items[i];
            report.Info("item[" + i + "]: id=" + item.id +
                ", enabled=" + YesNo(item.enabled) +
                ", locale=" + FirstNonEmpty(item.locale, "общий") +
                ", screenId=" + FirstNonEmpty(item.screenId, "общий") +
                ", storyId=" + FirstNonEmpty(item.storyId, "общий") +
                ", text=" + FirstNonEmpty(item.text, "пусто"));
        }

        if (string.IsNullOrWhiteSpace(textId))
        {
            report.Fail("ID текста пустой. Укажите, например, cat_phase.");
            yield break;
        }

        RemoteUiTextItem selected = PickUiTextItem(items, textId, screenId, storyId, locale);
        if (selected == null)
        {
            report.Fail("Текст с id='" + textId + "' не найден для locale='" + locale + "', screenId='" + screenId + "', storyId='" + storyId + "'.");
            yield break;
        }

        report.Info("Выбранный текст: id=" + selected.id +
            ", enabled=" + YesNo(selected.enabled) +
            ", locale=" + FirstNonEmpty(selected.locale, "общий") +
            ", screenId=" + FirstNonEmpty(selected.screenId, "общий") +
            ", storyId=" + FirstNonEmpty(selected.storyId, "общий") + ".");

        if (!selected.enabled)
        {
            report.Info("Визуальный результат Unity: скрыто. Причина: enabled=false.");
        }
        else if (string.IsNullOrWhiteSpace(selected.text))
        {
            report.Info("Визуальный результат Unity: скрыто. Причина: text пустой.");
        }
        else
        {
            report.Info("Визуальный результат Unity: показан текст -> " + selected.text);
        }
    }

    private IEnumerator RunManualProbe()
    {
        string method = ManualMethods[Mathf.Clamp(_manualMethodIndex, 0, ManualMethods.Length - 1)];
        var report = new ApiProbeReport("Разовый API-запрос", CleanBaseUrl(_baseUrl));
        ProbeResult result = null;
        yield return SendProbe(
            method,
            _manualPath,
            method == "GET" || method == "DELETE" ? null : _manualBody,
            _manualBearerToken,
            _adminKey,
            response => result = response);
        report.Add(method + " " + ApiContract.RedactedEndpointForLog(_manualPath), result, true, "");
        FinishReport(report);
    }

    private IEnumerator AddRuntimeProbe(ApiProbeReport report, string label, string path, string token, bool required)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("episodeId=") && path.EndsWith("episodeId=", StringComparison.Ordinal))
        {
            report.Warn(label + ": пропущено, потому что нет ID эпизода.");
            yield break;
        }

        ProbeResult result = null;
        yield return SendProbe("GET", path, null, token, "", response => result = response);
        report.Add(label, result, required, "");
    }

    private IEnumerator AddAdminProbe(ApiProbeReport report, string label, string path, string key, bool required)
    {
        ProbeResult result = null;
        yield return SendProbe("GET", path, null, "", key, response => result = response);
        report.Add(label, result, required, "");
    }

    private IEnumerator SendProbe(
        string method,
        string path,
        string jsonBody,
        string bearerToken,
        string adminKey,
        Action<ProbeResult> callback)
    {
        string url = BuildUrl(path);
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(ProbeResult.ClientError(method, path, "Некорректный URL."));
            yield break;
        }

        using (UnityWebRequest request = CreateRequest(method, url, jsonBody))
        {
            request.timeout = 20;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-store");

            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken.Trim());
            if (!string.IsNullOrWhiteSpace(adminKey))
                request.SetRequestHeader("X-Admin-Key", adminKey.Trim());

            yield return request.SendWebRequest();

            callback?.Invoke(new ProbeResult
            {
                Method = method,
                Path = ApiContract.RedactedEndpointForLog(path),
                StatusCode = request.responseCode,
                Result = request.result.ToString(),
                Error = Redact(request.error),
                Body = Redact(request.downloadHandler != null ? request.downloadHandler.text : "")
            });
        }
    }

    private string BuildUrl(string path)
    {
        string root = CleanBaseUrl(_baseUrl);
        if (!Uri.TryCreate(root, UriKind.Absolute, out Uri baseUri))
            return "";

        string relative = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (relative.StartsWith("//", StringComparison.Ordinal) || Uri.TryCreate(relative, UriKind.Absolute, out _))
            return "";

        return new Uri(baseUri, relative.StartsWith("/", StringComparison.Ordinal) ? relative : "/" + relative).ToString();
    }

    private static UnityWebRequest CreateRequest(string method, string url, string jsonBody)
    {
        method = (method ?? "GET").Trim().ToUpperInvariant();
        if (method == "GET")
            return UnityWebRequest.Get(url);
        if (method == "DELETE")
        {
            var delete = UnityWebRequest.Delete(url);
            delete.downloadHandler = new DownloadHandlerBuffer();
            return delete;
        }

        var request = new UnityWebRequest(url, method);
        string body = string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private string BuildGuestAuthJson()
    {
        string safeDeviceId = SaveDataSanitizer.SanitizeIdentifier(_deviceId);
        if (string.IsNullOrWhiteSpace(safeDeviceId))
            safeDeviceId = "unity-api-check";

        return "{\"deviceId\":\"" + NetworkJson.Escape(safeDeviceId) +
            "\",\"platform\":\"unity-editor\",\"appVersion\":\"" + NetworkJson.Escape(Application.version) + "\"}";
    }

    private string BuildUiTextUpsertJson()
    {
        string textId = SaveDataSanitizer.SanitizeIdentifier(_uiTextId);
        string screenId = SaveDataSanitizer.SanitizeIdentifier(_uiTextScreenId);
        string storyId = SaveDataSanitizer.SanitizeIdentifier(_uiTextStoryId);
        string locale = SaveDataSanitizer.SanitizeIdentifier(_uiTextLocale);
        if (string.IsNullOrWhiteSpace(locale))
            locale = "ru";

        string text = _uiTextDraftText ?? "";
        if (text.Length > MaxUiTextDraftChars)
            text = text.Substring(0, MaxUiTextDraftChars);

        return "{\"items\":[{\"id\":\"" + NetworkJson.Escape(textId) +
            "\",\"text\":\"" + NetworkJson.Escape(text) +
            "\",\"enabled\":" + (_uiTextDraftEnabled ? "true" : "false") +
            ",\"locale\":\"" + NetworkJson.Escape(locale) +
            "\",\"screenId\":\"" + NetworkJson.Escape(screenId) +
            "\",\"storyId\":\"" + NetworkJson.Escape(storyId) + "\"}]}";
    }

    private static string ExtractAuthToken(string json)
    {
        return FirstNonEmpty(
            NetworkJson.GetString(json, "authToken"),
            NetworkJson.GetString(json, "token"),
            NetworkJson.GetString(json, "accessToken"));
    }

    private static int CountEpisodes(List<CatalogSeasonResponse> seasons)
    {
        int count = 0;
        if (seasons == null)
            return 0;

        foreach (var season in seasons)
            if (season != null && season.episodes != null)
                count += season.episodes.Count;

        return count;
    }

    private static string PickEpisodeId(List<CatalogSeasonResponse> seasons)
    {
        if (seasons == null)
            return "";

        string first = "";
        foreach (var season in seasons)
        {
            if (season == null || season.episodes == null)
                continue;

            foreach (var episode in season.episodes)
            {
                if (episode == null || string.IsNullOrWhiteSpace(episode.episodeId))
                    continue;

                if (string.IsNullOrWhiteSpace(first))
                    first = episode.episodeId;

                if (episode.hasRemoteContent)
                    return episode.episodeId;
            }
        }

        return first;
    }

    private static RemoteUiTextItem PickUiTextItem(
        List<RemoteUiTextItem> items,
        string textId,
        string screenId,
        string storyId,
        string locale)
    {
        if (items == null)
            return null;

        string targetId = NormalizeUiTextDiagnosticKey(textId);
        string targetScreen = NormalizeUiTextDiagnosticKey(screenId);
        string targetStory = NormalizeUiTextDiagnosticKey(storyId);
        string targetLocale = NormalizeUiTextDiagnosticKey(locale);
        RemoteUiTextItem best = null;
        int bestScore = -1;
        int bestOrder = int.MaxValue;

        foreach (RemoteUiTextItem item in items)
        {
            if (item == null)
                continue;
            if (NormalizeUiTextDiagnosticKey(item.id) != targetId)
                continue;
            if (!UiTextContextMatchesDiagnostic(item.screenId, targetScreen))
                continue;
            if (!UiTextContextMatchesDiagnostic(item.storyId, targetStory))
                continue;
            if (!UiTextContextMatchesDiagnostic(item.locale, targetLocale))
                continue;

            int score = UiTextDiagnosticScore(item);
            int order = item.Order;
            if (score > bestScore || score == bestScore && order < bestOrder)
            {
                best = item;
                bestScore = score;
                bestOrder = order;
            }
        }

        return best;
    }

    private static bool UiTextContextMatchesDiagnostic(string itemValue, string requestedKey)
    {
        string itemKey = NormalizeUiTextDiagnosticKey(itemValue);
        return string.IsNullOrEmpty(itemKey) || itemKey == requestedKey;
    }

    private static int UiTextDiagnosticScore(RemoteUiTextItem item)
    {
        int score = 0;
        if (!string.IsNullOrEmpty(NormalizeUiTextDiagnosticKey(item.screenId)))
            score++;
        if (!string.IsNullOrEmpty(NormalizeUiTextDiagnosticKey(item.storyId)))
            score++;
        if (!string.IsNullOrEmpty(NormalizeUiTextDiagnosticKey(item.locale)))
            score++;
        return score;
    }

    private static string NormalizeUiTextDiagnosticKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return SaveDataSanitizer.SanitizeIdentifier(value).ToLowerInvariant();
    }

    private void FinishReport(ApiProbeReport report)
    {
        _lastResult = report.Build();
        Repaint();
    }

    private static string CleanBaseUrl(string value)
    {
        return CleanDefault(value, ApiRoutes.BaseUrl).Trim().TrimEnd('/');
    }

    private static string CleanDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static bool IsLoopbackBaseUrl(string value)
    {
        return Uri.TryCreate(CleanBaseUrl(value), UriKind.Absolute, out Uri uri) && uri.IsLoopback;
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string redacted = BearerPattern.Replace(value, "Bearer [REDACTED]");
        return SecretPattern.Replace(redacted, "$1[REDACTED]");
    }

    private static string Trim(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? "";

        return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return "";

        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();

        return "";
    }

    private static string YesNo(bool value)
    {
        return value ? "да" : "нет";
    }

    private sealed class ProbeResult
    {
        public string Method;
        public string Path;
        public long StatusCode;
        public string Result;
        public string Error;
        public string Body;

        public bool IsHttpOk => StatusCode >= 200 && StatusCode < 300;

        public static ProbeResult ClientError(string method, string path, string error)
        {
            return new ProbeResult
            {
                Method = method,
                Path = ApiContract.RedactedEndpointForLog(path),
                StatusCode = 0,
                Result = "ClientError",
                Error = error,
                Body = ""
            };
        }
    }

    private sealed class ApiProbeReport
    {
        private readonly StringBuilder _builder = new StringBuilder(4096);
        private int _ok;
        private int _warn;
        private int _fail;

        public ApiProbeReport(string title, string baseUrl)
        {
            _builder.AppendLine(title);
            _builder.AppendLine("Адрес: " + baseUrl);
            _builder.AppendLine("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            _builder.AppendLine();
        }

        public void Add(string label, ProbeResult result, bool required, string note)
        {
            bool ok = result != null && result.IsHttpOk;
            if (ok)
                _ok++;
            else if (required)
                _fail++;
            else
                _warn++;

            _builder.Append(ok ? "OK " : required ? "ОШИБКА " : "ПРЕДУПРЕЖДЕНИЕ ");
            _builder.AppendLine(label);

            if (!string.IsNullOrWhiteSpace(note))
                _builder.AppendLine("  " + note);

            if (result == null)
            {
                _builder.AppendLine("  Нет результата.");
                _builder.AppendLine();
                return;
            }

            _builder.AppendLine("  " + result.Method + " " + result.Path);
            _builder.AppendLine("  Статус: " + result.StatusCode + " / " + result.Result);
            if (!string.IsNullOrWhiteSpace(result.Error))
                _builder.AppendLine("  Ошибка: " + result.Error);

            string body = Trim(result.Body, MaxResponsePreviewChars);
            if (!string.IsNullOrWhiteSpace(body))
                _builder.AppendLine("  Ответ: " + body.Replace("\n", "\n  "));

            _builder.AppendLine();
        }

        public void Info(string message)
        {
            _builder.AppendLine("INFO " + message);
        }

        public void Warn(string message)
        {
            _warn++;
            _builder.AppendLine("ПРЕДУПРЕЖДЕНИЕ " + message);
        }

        public void Fail(string message)
        {
            _fail++;
            _builder.AppendLine("ОШИБКА " + message);
        }

        public string Build()
        {
            _builder.AppendLine();
            _builder.AppendLine("Итог:");
            _builder.AppendLine("OK: " + _ok);
            _builder.AppendLine("Предупреждения: " + _warn);
            _builder.AppendLine("Ошибки: " + _fail);
            _builder.AppendLine(_fail == 0
                ? "Вывод: обязательные API-проверки прошли."
                : "Вывод: есть обязательные ошибки API, надо смотреть шаги выше.");
            return _builder.ToString();
        }
    }
}
