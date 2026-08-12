using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Main/Prediction Card Screen")]
public sealed class MainMenuPredictionCardScreenController : MonoBehaviour
{
    private const string ScreenId = "CardScreenMainMenu";
    private const string ReturnScreenId = "MainScreen";

    [SerializeField] private Image _cardImage;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _closeButton;
    [SerializeField, Min(1)] private int _imageRequestTimeoutSeconds = 15;

    private StoryScreenNavigator _screenNavigator;
    private Sprite _sceneFallbackSprite;
    private Sprite _ownedRuntimeSprite;
    private Coroutine _imageRoutine;
    private int _contentRevision;

    private void Awake()
    {
        ResolveReferences();
        BindCloseButton();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindCloseButton();
    }

    private void OnDisable()
    {
        StopImageRoutine();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Close);
        DestroyOwnedRuntimeSprite();
    }

    public void Show(MainMenuPredictionOfferContent content)
    {
        if (content == null || !content.IsValid)
            return;

        ResolveReferences();
        _contentRevision++;
        int revision = _contentRevision;

        if (_titleText != null)
            _titleText.text = content.Title;
        if (_descriptionText != null)
            _descriptionText.text = content.Description;

        StopImageRoutine();
        DestroyOwnedRuntimeSprite();

        if (!TryApplyLocalCardSprite(content.CardId))
            ApplySprite(_sceneFallbackSprite);

        string imageUrl = ResolveImageUrl(content.ImageUrl);
        if (!string.IsNullOrEmpty(imageUrl))
            _imageRoutine = StartCoroutine(LoadImage(imageUrl, content.CardId, revision));
    }

    public void Close()
    {
        ResolveNavigator();
        if (_screenNavigator == null || !_screenNavigator.OpenScreen(ReturnScreenId))
        {
            Debug.LogWarning(
                $"[PredictionOffer] Cannot close '{ScreenId}': navigator or '{ReturnScreenId}' is missing.",
                this);
        }
    }

    private IEnumerator LoadImage(string url, string cardId, int revision)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = Mathf.Clamp(_imageRequestTimeoutSeconds, 1, 60);
            yield return request.SendWebRequest();

            if (revision != _contentRevision)
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[PredictionOffer] Card image failed to load: " + request.error, this);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
                yield break;

            DestroyOwnedRuntimeSprite();
            _ownedRuntimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _ownedRuntimeSprite.name = "MainMenuPrediction_" +
                SaveDataSanitizer.SafeKeyPart(cardId, "card", 64);
            ApplySprite(_ownedRuntimeSprite);
        }

        _imageRoutine = null;
    }

    private bool TryApplyLocalCardSprite(string cardId)
    {
        cardId = SaveDataSanitizer.SanitizeIdentifier(cardId);
        if (string.IsNullOrEmpty(cardId))
            return false;

        DivinationCardDisplayController[] controllers =
            FindObjectsOfType<DivinationCardDisplayController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            DivinationCardDisplayController controller = controllers[i];
            if (controller != null && controller.TryResolveSprite(cardId, out Sprite sprite) && sprite != null)
            {
                ApplySprite(sprite);
                return true;
            }
        }

        return false;
    }

    private string ResolveImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "";

        imageUrl = imageUrl.Trim();
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttps || absoluteUri.Scheme == Uri.UriSchemeHttp))
        {
            return imageUrl;
        }

        if (!imageUrl.StartsWith("/", StringComparison.Ordinal))
            return "";

        string baseUrl = !string.IsNullOrWhiteSpace(NetworkManager.ActiveBaseUrl)
            ? NetworkManager.ActiveBaseUrl
            : ApiRoutes.BaseUrl;
        return baseUrl.TrimEnd('/') + imageUrl;
    }

    private void ResolveReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        Image[] images = GetComponentsInChildren<Image>(true);

        if (_titleText == null)
            _titleText = FindText(texts, "TitleText");
        if (_descriptionText == null)
            _descriptionText = FindText(texts, "DescripriotnText", "DescriptionText");
        if (_cardImage == null)
            _cardImage = FindImage(images, "CardIcon");
        if (_cardImage != null && _sceneFallbackSprite == null)
            _sceneFallbackSprite = _cardImage.sprite;

        if (_closeButton == null)
            _closeButton = FindCloseButton();
        if (_closeButton == null)
            _closeButton = CreateCloseButton();

        ResolveNavigator();
    }

    private void ResolveNavigator()
    {
        if (_screenNavigator == null)
            _screenNavigator = FindObjectOfType<StoryScreenNavigator>(true);
    }

    private void BindCloseButton()
    {
        if (_closeButton == null)
            return;

        _closeButton.onClick.RemoveListener(Close);
        _closeButton.onClick.AddListener(Close);
    }

    private Button FindCloseButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            string objectName = button.gameObject.name;
            if (objectName.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return button;
            }
        }

        return null;
    }

    private Button CreateCloseButton()
    {
        var buttonObject = new GameObject(
            "PredictionOfferCloseButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-72f, -72f);
        rect.sizeDelta = new Vector2(88f, 88f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.38f);

        var labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "×";
        label.fontSize = 58f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = _titleText != null ? _titleText.color : Color.white;
        label.raycastTarget = false;
        if (_titleText != null && _titleText.font != null)
            label.font = _titleText.font;

        return buttonObject.GetComponent<Button>();
    }

    private void StopImageRoutine()
    {
        if (_imageRoutine == null)
            return;

        StopCoroutine(_imageRoutine);
        _imageRoutine = null;
    }

    private void DestroyOwnedRuntimeSprite()
    {
        if (_ownedRuntimeSprite == null)
            return;

        Texture2D texture = _ownedRuntimeSprite.texture;
        Destroy(_ownedRuntimeSprite);
        if (texture != null)
            Destroy(texture);
        _ownedRuntimeSprite = null;
    }

    private void ApplySprite(Sprite sprite)
    {
        if (_cardImage == null || sprite == null)
            return;

        _cardImage.sprite = sprite;
        _cardImage.enabled = true;
        _cardImage.preserveAspect = true;
    }

    private static TMP_Text FindText(TMP_Text[] texts, params string[] names)
    {
        if (texts == null || names == null)
            return null;

        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && string.Equals(text.gameObject.name, names[n], StringComparison.OrdinalIgnoreCase))
                    return text;
            }
        }

        return null;
    }

    private static Image FindImage(Image[] images, string objectName)
    {
        if (images == null)
            return null;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && string.Equals(image.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                return image;
        }

        return null;
    }
}
