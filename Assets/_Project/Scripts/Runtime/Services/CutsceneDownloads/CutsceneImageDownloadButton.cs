using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public sealed class CutsceneImageDownloadButton : MonoBehaviour
{
    private const string LogPrefix = "[IMAGE_EXPORT][CUTSCENE]";

    [Header("Unity Button")]
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Text")]
    [SerializeField] private string readyText = "Скачать";
    [SerializeField] private string busyText = "Сохраняю...";
    [SerializeField] private string successText = "Сохранено";
    [SerializeField] private string errorText = "Ошибка";
    [SerializeField, Min(0f)] private float messageSeconds = 1.5f;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenNoCutsceneImage = true;

    private CutsceneImageDownloadInfo _current;
    private Coroutine _saveRoutine;
    private Coroutine _messageRoutine;

    private void Reset()
    {
        button = GetComponent<Button>();
        visibilityGroup = GetComponent<CanvasGroup>();
        statusLabel = GetComponentInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (visibilityGroup == null)
            visibilityGroup = GetComponent<CanvasGroup>();

        if (visibilityGroup == null)
            visibilityGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(HandleClick);
        CutsceneImageDownloadState.Changed += HandleCutsceneImageChanged;
        HandleCutsceneImageChanged(CutsceneImageDownloadState.Current);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(HandleClick);
        CutsceneImageDownloadState.Changed -= HandleCutsceneImageChanged;
    }

    private void HandleCutsceneImageChanged(CutsceneImageDownloadInfo info)
    {
        _current = info;
        bool hasImage = info.HasImage;

        if (hideWhenNoCutsceneImage)
            SetVisible(hasImage);

        if (_saveRoutine == null)
        {
            button.interactable = hasImage;
            SetLabel(readyText);
        }
    }

    private void HandleClick()
    {
        Debug.Log(
            $"{LogPrefix}[CLICK] platform={Application.platform} busy={_saveRoutine != null} " +
            $"hasImage={_current.HasImage} sprite='{(_current.Sprite != null ? _current.Sprite.name : "<null>")}' " +
            $"file='{_current.SuggestedFileName}'",
            this);

        if (_saveRoutine != null)
            return;

        if (!_current.HasImage)
        {
            Debug.LogWarning($"{LogPrefix}[BLOCKED] reason=No_current_cutscene_image", this);
            return;
        }

        _saveRoutine = StartCoroutine(SaveCurrentImage(_current));
    }

    private IEnumerator SaveCurrentImage(CutsceneImageDownloadInfo info)
    {
        StopMessageRoutine();
        button.interactable = false;
        SetLabel(busyText);

        yield return CutsceneGalleryPermission.RequestSaveAccessIfNeeded();

        if (!CutsceneGalleryPermission.HasSaveAccess)
        {
            FinishSave(false, "Нет разрешения на сохранение файла.", "permission");
            yield break;
        }

        if (!SpritePngEncoder.TryEncodeToPng(info.Sprite, out byte[] png, out string error))
        {
            FinishSave(false, error, "encode");
            yield break;
        }

        if (!GalleryImageSaver.TrySavePng(
                png,
                info.SuggestedFileName,
                out string path,
                out error))
        {
            FinishSave(false, error, "gallery_save");
            yield break;
        }

        Debug.Log(
            $"{LogPrefix}[SUCCESS] sprite='{info.Sprite.name}' path='{path}' pngBytes={png.Length}",
            this);
        FinishSave(true, "", "complete");
    }

    private void FinishSave(bool success, string error, string stage)
    {
        if (!success)
        {
            Debug.LogWarning(
                $"{LogPrefix}[FAILED] stage={stage} platform={Application.platform} " +
                $"sprite='{(_current.Sprite != null ? _current.Sprite.name : "<null>")}' reason='{error}'",
                this);
        }

        _saveRoutine = null;
        SetLabel(success ? successText : errorText);
        _messageRoutine = StartCoroutine(RestoreReadyStateAfterDelay());
    }

    private IEnumerator RestoreReadyStateAfterDelay()
    {
        if (messageSeconds > 0f)
            yield return new WaitForSecondsRealtime(messageSeconds);

        _messageRoutine = null;
        button.interactable = _current.HasImage;
        SetLabel(readyText);
    }

    private void StopMessageRoutine()
    {
        if (_messageRoutine == null)
            return;

        StopCoroutine(_messageRoutine);
        _messageRoutine = null;
    }

    private void SetLabel(string value)
    {
        if (statusLabel != null)
            statusLabel.text = value;
    }

    private void SetVisible(bool visible)
    {
        if (visibilityGroup == null)
            return;

        visibilityGroup.alpha = visible ? 1f : 0f;
        visibilityGroup.interactable = visible;
        visibilityGroup.blocksRaycasts = visible;
    }
}
