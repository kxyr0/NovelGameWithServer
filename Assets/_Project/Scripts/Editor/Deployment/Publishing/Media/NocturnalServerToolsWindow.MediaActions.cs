#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

public sealed partial class NocturnalServerToolsWindow
{
    private string _mediaFilePath = "";
    private string _mediaDeleteFilename = "";
    private string _mediaPublicUrl = "";

    private void DrawMediaPage()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Медиа: картинки, аудио, видео", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Здесь загружаются png, jpg, jpeg, webp, mp3, ogg, wav, mp4 до 50MB. Публичная ссылка работает без авторизации: https://nocturnedc.ru/media/{filename}.",
            MessageType.Info);
        EditorGUIUtility.labelWidth = 190f;
        using (new EditorGUI.DisabledScope(_isBusy))
        {
            _baseUrl = EditorGUILayout.TextField(new GUIContent("Адрес сервера", "Для реального сервера: https://nocturnedc.ru"), _baseUrl, GUILayout.Height(28f));
            _adminKey = EditorGUILayout.PasswordField(new GUIContent("X-Admin-Key", "Ключ нужен для загрузки, списка и удаления media. В код он не сохраняется."), _adminKey, GUILayout.Height(28f));
            _allowUnsigned = EditorGUILayout.Toggle(new GUIContent("Разрешить без ключа", "Только для локального тестового сервера."), _allowUnsigned, GUILayout.Height(24f));

            ActionGroup("1. Загрузка");
            EditorGUILayout.BeginHorizontal();
            _mediaFilePath = EditorGUILayout.TextField(new GUIContent("Абсолютный путь файла", "Выберите файл на диске или вставьте абсолютный путь."), _mediaFilePath, GUILayout.Height(28f));
            if (GUILayout.Button(new GUIContent("Выбрать", "Открывает выбор файла."), GUILayout.Width(110f), GUILayout.Height(28f)))
                _mediaFilePath = EditorUtility.OpenFilePanel("Выбрать media файл", "", "");
            EditorGUILayout.EndHorizontal();

            BeginActionRow();
            if (ActionButton("Загрузить файл", "Отправляет выбранный файл в /admin/media/upload."))
                UploadMediaFile();
            if (ActionButton("Список файлов", "Запрашивает /admin/media."))
                ListMediaFiles();
            EndActionRow();

            ActionGroup("2. Удаление");
            _mediaDeleteFilename = EditorGUILayout.TextField(new GUIContent("Filename", "Имя файла из ответа upload или списка /admin/media."), _mediaDeleteFilename, GUILayout.Height(28f));
            BeginActionRow();
            if (ActionButton("Удалить файл", "Удаляет файл через DELETE /admin/media/{filename}."))
                DeleteMediaFile();
            if (ActionButton("Публичная ссылка", "Создаёт ссылку https://nocturnedc.ru/media/{filename}."))
                BuildMediaPublicUrl();
            EndActionRow();

            ActionGroup("3. Ссылка для игры");
            _mediaPublicUrl = EditorGUILayout.TextField(new GUIContent("Публичная ссылка", "Эту ссылку можно использовать в JSON/контенте игры."), _mediaPublicUrl, GUILayout.Height(28f));
            BeginActionRow();
            if (ActionButton("Скопировать ссылку", "Копирует публичную ссылку в буфер."))
                EditorGUIUtility.systemCopyBuffer = _mediaPublicUrl;
            if (ActionButton("Открыть ссылку", "Открывает публичную ссылку в браузере."))
                Application.OpenURL(_mediaPublicUrl);
            EndActionRow();
        }
    }

    private void UploadMediaFile()
    {
        if (!ConfirmBackendWrite("Загрузка media файла"))
            return;
        StartBackendRequest(CurrentBackendMediaClient.Upload(
            _mediaFilePath,
            OnMediaRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void ListMediaFiles()
    {
        StartBackendRequest(CurrentBackendMediaClient.List(OnMediaRequestFinished, _baseUrl, _adminKey, _allowUnsigned));
    }

    private void DeleteMediaFile()
    {
        if (!ConfirmBackendWrite("Удаление media файла"))
            return;
        StartBackendRequest(CurrentBackendMediaClient.Delete(
            _mediaDeleteFilename,
            OnMediaRequestFinished,
            _baseUrl,
            _adminKey,
            _allowUnsigned));
    }

    private void BuildMediaPublicUrl()
    {
        string filename = CurrentBackendMediaRoutes.SanitizeFilename(_mediaDeleteFilename);
        if (StopWithResponse(string.IsNullOrWhiteSpace(filename), "Укажите filename."))
            return;
        _mediaPublicUrl = BuildAbsoluteMediaUrl(CurrentBackendMediaRoutes.PublicMedia(filename));
        EditorGUIUtility.systemCopyBuffer = _mediaPublicUrl;
        _lastResponse = "Публичная ссылка скопирована:\n" + _mediaPublicUrl;
        Repaint();
    }

    private void OnMediaRequestFinished(UnityPublisherRequestResult result)
    {
        OnBackendRequestFinished(result);
        if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Body))
            return;

        string filename = NetworkJson.GetString(result.Body, "filename");
        string url = NetworkJson.GetString(result.Body, "url");
        if (!string.IsNullOrWhiteSpace(filename))
            _mediaDeleteFilename = filename;
        if (!string.IsNullOrWhiteSpace(url))
        {
            _mediaPublicUrl = BuildAbsoluteMediaUrl(url);
            EditorGUIUtility.systemCopyBuffer = _mediaPublicUrl;
            _lastResponse += "\n\nПубличная ссылка скопирована:\n" + _mediaPublicUrl;
        }
    }

    private string BuildAbsoluteMediaUrl(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath))
            return "";
        if (urlOrPath.StartsWith("http://") || urlOrPath.StartsWith("https://"))
            return urlOrPath;
        return FirstNonEmpty(_baseUrl, ApiRoutes.BaseUrl).Trim().TrimEnd('/') + "/" + urlOrPath.TrimStart('/');
    }
}
#endif
