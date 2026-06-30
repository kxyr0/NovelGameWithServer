using UnityEngine;
using UnityEngine.UI;

public static class RuntimeTextureFallback
{
    static Texture2D _placeholderTexture;
    static Sprite _placeholderSprite;

    public static Texture2D PlaceholderTexture
    {
        get
        {
            if (_placeholderTexture == null)
                _placeholderTexture = CreatePlaceholderTexture();

            return _placeholderTexture;
        }
    }

    public static Sprite PlaceholderSprite
    {
        get
        {
            if (_placeholderSprite == null)
            {
                Texture2D texture = PlaceholderTexture;
                _placeholderSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _placeholderSprite.name = "Runtime Texture Placeholder";
            }

            return _placeholderSprite;
        }
    }

    public static void EnsureImageVisible(Image image, Sprite preferredSprite = null)
    {
        if (image == null)
            return;

        if (preferredSprite != null)
            image.sprite = preferredSprite;
        else if (image.sprite == null)
            image.sprite = PlaceholderSprite;

        if (image.color.a <= 0.01f)
            image.color = Color.white;

        image.enabled = true;
        image.gameObject.SetActive(true);
    }

    public static void EnsureRawImageVisible(RawImage image, Texture preferredTexture = null)
    {
        if (image == null)
            return;

        if (preferredTexture != null)
            image.texture = preferredTexture;
        else if (image.texture == null)
            image.texture = PlaceholderTexture;

        if (image.color.a <= 0.01f)
            image.color = Color.white;

        image.enabled = true;
        image.gameObject.SetActive(true);
    }

    public static void ApplyImagePlaceholder(Image image)
    {
        if (image == null)
            return;

        image.sprite = PlaceholderSprite;
        image.color = Color.white;
        image.enabled = true;
        image.gameObject.SetActive(true);
    }

    static Texture2D CreatePlaceholderTexture()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "Runtime Texture Placeholder",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32 dark = new Color32(20, 20, 24, 255);
        Color32 light = new Color32(34, 34, 40, 255);
        texture.SetPixels32(new[] { dark, light, light, dark });
        texture.Apply(false, true);
        return texture;
    }
}
