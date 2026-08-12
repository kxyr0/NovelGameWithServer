#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ContentReleaseUploadDestinationEditor
{
    private static readonly string[] ModeIds =
    {
        ContentReleaseUploadDestinationSettings.ManualMode,
        ContentReleaseUploadDestinationSettings.FtpMode,
        ContentReleaseUploadDestinationSettings.SftpMode,
        ContentReleaseUploadDestinationSettings.LocalMode
    };

    private static readonly GUIContent[] ModeLabels =
    {
        new GUIContent("Ручная загрузка / CDN", "План покажет, какие файлы и куда положить вручную."),
        new GUIContent("FTP", "Путь вида ftp://user@host/path/[BuildTarget]. Пароль храните в FTP-клиенте, не в Unity."),
        new GUIContent("SFTP", "Путь вида sftp://user@host/var/www/cdn/[BuildTarget]."),
        new GUIContent("Локальная папка", "Папка на диске или сетевой диск, например D:\\cdn\\stage\\[BuildTarget].")
    };

    public static void Draw(string environmentId)
    {
        ContentReleaseUploadDestinationEntry entry = ContentReleaseUploadDestinationSettings.Get(environmentId);
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Назначение Addressables", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Публичный URL нужен игре для скачивания. Путь загрузки нужен человеку или скрипту, чтобы положить файлы на CDN, FTP, SFTP или в папку.",
            MessageType.Info);
        if (string.IsNullOrWhiteSpace(entry.uploadRootPath))
            EditorGUILayout.HelpBox("CDN/R2 для Addressables пока не подключён. Сборку и план можно готовить, но реальная загрузка файлов будет доступна после выдачи R2/CDN пути.", MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        entry.publicLoadRootUrl = EditorGUILayout.TextField(
            new GUIContent("Публичный URL для Unity", "Адрес, откуда игроки будут скачивать Addressables. Можно использовать [BuildTarget]."),
            entry.publicLoadRootUrl,
            GUILayout.Height(28f));
        entry.uploadMode = ModeIds[Mathf.Clamp(EditorGUILayout.Popup(
            new GUIContent("Способ загрузки", "Выберите, как будут переноситься файлы после сборки."),
            IndexOf(ModeIds, entry.uploadMode),
            ModeLabels,
            GUILayout.Height(28f)), 0, ModeIds.Length - 1)];
        entry.uploadRootPath = EditorGUILayout.TextField(
            new GUIContent("Абсолютный путь загрузки", "FTP/SFTP/CDN/локальный путь, куда нужно положить Addressables. Например: sftp://user@host/var/www/cdn/stage/[BuildTarget] или D:\\cdn\\stage\\[BuildTarget]."),
            entry.uploadRootPath,
            GUILayout.Height(28f));
        entry.notes = EditorGUILayout.TextField(
            new GUIContent("Заметка", "Любая подсказка для того, кто будет выкладывать файлы."),
            entry.notes,
            GUILayout.Height(28f));
        if (EditorGUI.EndChangeCheck())
            ContentReleaseUploadDestinationSettings.Save(entry);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Пример SFTP", "Подставляет пример SFTP-пути без пароля."), GUILayout.Height(28f)))
            SaveExample(entry, ContentReleaseUploadDestinationSettings.SftpMode, "sftp://user@host/var/www/cdn/" + entry.environmentId + "/[BuildTarget]");
        if (GUILayout.Button(new GUIContent("Скопировать путь", "Копирует текущий путь загрузки."), GUILayout.Height(28f)))
            EditorGUIUtility.systemCopyBuffer = entry.uploadRootPath;
        if (GUILayout.Button(new GUIContent("Сбросить", "Возвращает стандартный URL и очищает путь загрузки."), GUILayout.Height(28f)))
            ContentReleaseUploadDestinationSettings.Reset(environmentId);
        if (GUILayout.Button(new GUIContent("Открыть JSON", "Открывает файл, где хранятся эти настройки."), GUILayout.Height(28f)))
            ContentReleaseUploadDestinationSettings.RevealConfig();
        EditorGUILayout.EndHorizontal();
    }

    private static void SaveExample(
        ContentReleaseUploadDestinationEntry entry,
        string mode,
        string uploadRootPath)
    {
        entry.uploadMode = mode;
        entry.uploadRootPath = uploadRootPath;
        ContentReleaseUploadDestinationSettings.Save(entry);
    }

    private static int IndexOf(string[] values, string value)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i] == value)
                return i;
        return 0;
    }
}
#endif
