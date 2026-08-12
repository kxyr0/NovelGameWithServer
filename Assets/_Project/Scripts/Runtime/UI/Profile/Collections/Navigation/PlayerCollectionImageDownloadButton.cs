using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Image Download Button")]
public sealed class PlayerCollectionImageDownloadButton : MonoBehaviour
{
	private const string LogPrefix = "[IMAGE_EXPORT][PROFILE]";

	[SerializeField] private Button _button;

	private Coroutine _saveRoutine;

	private void Reset()
	{
		_button = GetComponent<Button>();
	}

	private void Awake()
	{
		ResolveButton();
	}

	private void OnEnable()
	{
		Button button = ResolveButton();
		if (button != null)
			button.onClick.AddListener(DownloadSelectedImage);

		PlayerCollectionSelectionState.Changed += Refresh;
		Refresh();
	}

	private void OnDisable()
	{
		if (_button != null)
			_button.onClick.RemoveListener(DownloadSelectedImage);

		PlayerCollectionSelectionState.Changed -= Refresh;

		if (_saveRoutine != null)
		{
			StopCoroutine(_saveRoutine);
			_saveRoutine = null;
		}
	}

	public void DownloadSelectedImage()
	{
		PlayerCollectionItemDefinition item =
			PlayerCollectionSelectionState.CurrentItem;
		Sprite sprite = PlayerCollectionSelectionState.CurrentImage;

		Debug.Log(
			$"{LogPrefix}[CLICK] platform={Application.platform} busy={_saveRoutine != null} " +
			$"item='{(item != null ? item.Title : "<null>")}' " +
			$"title='{(item != null ? item.Title : "")}' " +
			$"sprite='{(sprite != null ? sprite.name : "<null>")}'",
			this);

		if (_saveRoutine != null)
			return;

		if (item == null || sprite == null)
		{
			Debug.LogWarning(
				$"{LogPrefix}[BLOCKED] reason=Selected_item_or_image_missing " +
				$"hasItem={item != null} hasSprite={sprite != null}",
				this);
			return;
		}

		_saveRoutine = StartCoroutine(SaveImage(item, sprite));
	}

	private IEnumerator SaveImage(
		PlayerCollectionItemDefinition item,
		Sprite sprite)
	{
		SetInteractable(false);
		yield return CutsceneGalleryPermission.RequestSaveAccessIfNeeded();

		if (!CutsceneGalleryPermission.HasSaveAccess)
		{
			Finish(false, "Нет разрешения на сохранение изображения.", "permission", item, sprite);
			yield break;
		}

		if (!SpritePngEncoder.TryEncodeToPng(
				sprite,
				out byte[] png,
				out string error))
		{
			Finish(false, error, "encode", item, sprite);
			yield break;
		}

		string fileName = BuildFileName(item, sprite);
		if (!GalleryImageSaver.TrySavePng(
				png,
				fileName,
				out string path,
				out error))
		{
			Finish(false, error, "gallery_save", item, sprite);
			yield break;
		}

		Debug.Log(
			$"{LogPrefix}[SUCCESS] item='{item.Title}' sprite='{sprite.name}' " +
			$"path='{path}' pngBytes={png.Length}",
			this);
		Finish(true, "", "complete", item, sprite);
	}

	private void Finish(
		bool success,
		string error,
		string stage,
		PlayerCollectionItemDefinition item,
		Sprite sprite)
	{
		if (!success)
		{
			Debug.LogWarning(
				$"{LogPrefix}[FAILED] stage={stage} platform={Application.platform} " +
				$"item='{(item != null ? item.Title : "<null>")}' " +
				$"sprite='{(sprite != null ? sprite.name : "<null>")}' reason='{error}'",
				this);
		}

		_saveRoutine = null;
		Refresh();
	}

	private void Refresh()
	{
		if (_saveRoutine != null)
			return;

		SetInteractable(
			PlayerCollectionSelectionState.CurrentItem != null &&
			PlayerCollectionSelectionState.CurrentImage != null);
	}

	private void SetInteractable(bool interactable)
	{
		Button button = ResolveButton();
		if (button != null)
			button.interactable = interactable;
	}

	private Button ResolveButton()
	{
		if (_button == null)
			_button = GetComponent<Button>();

		Debug.Log(_button.gameObject.name);
		return _button;
	}

	private static string BuildFileName(
		PlayerCollectionItemDefinition item,
		Sprite sprite)
	{
		string kind = item.Kind == PlayerCollectionKind.Moment
			? "moment"
			: "card";
		string title = string.IsNullOrWhiteSpace(item.Title)
			? "image"
			: item.Title.Trim();
		string spriteName = string.IsNullOrWhiteSpace(sprite.name)
			? "selected"
			: sprite.name.Trim();

		return $"nocturne_{kind}_{title}_{spriteName}";
	}
}
