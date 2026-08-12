using System;
using UnityEngine;

public static class SpritePngEncoder
{
    private const string LogPrefix = "[IMAGE_EXPORT][ENCODER]";

    public static bool TryEncodeToPng(Sprite sprite, out byte[] png, out string error)
    {
        png = null;
        error = "";

        if (sprite == null)
        {
            error = "Sprite is null.";
            Debug.LogWarning($"{LogPrefix}[FAILED] reason='{error}'");
            return false;
        }

        if (sprite.texture == null)
        {
            error = $"Sprite '{sprite.name}' has no texture.";
            Debug.LogWarning($"{LogPrefix}[FAILED] reason='{error}'");
            return false;
        }

        Texture2D readableCopy = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Texture2D source = sprite.texture;
            if (!TryGetSpriteTextureRect(sprite, out RectInt rect, out string rectSource, out string rectError))
            {
                error = rectError;
                Debug.LogWarning(
                    $"{LogPrefix}[FAILED] stage=resolve_rect sprite='{sprite.name}' texture='{source.name}' " +
                    $"textureSize={source.width}x{source.height} readable={source.isReadable} " +
                    $"packed={sprite.packed} packingMode={sprite.packingMode} packingRotation={sprite.packingRotation} " +
                    $"reason='{error}'");
                return false;
            }

            Debug.Log(
                $"{LogPrefix}[BEGIN] sprite='{sprite.name}' texture='{source.name}' " +
                $"textureSize={source.width}x{source.height} rect={rect.x},{rect.y},{rect.width},{rect.height} " +
                $"rectSource={rectSource} readable={source.isReadable} packed={sprite.packed} " +
                $"packingMode={sprite.packingMode} packingRotation={sprite.packingRotation}");

            readableCopy = new Texture2D(
                rect.width,
                rect.height,
                TextureFormat.RGBA32,
                false,
                false);

            if (source.isReadable)
                CopyReadablePixels(source, rect, readableCopy);
            else
                CopyGpuPixels(source, rect, readableCopy, ref renderTexture);

            png = readableCopy.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                error = "Texture2D.EncodeToPNG returned an empty PNG.";
                Debug.LogWarning(
                    $"{LogPrefix}[FAILED] stage=encode sprite='{sprite.name}' reason='{error}'");
                return false;
            }

            Debug.Log(
                $"{LogPrefix}[SUCCESS] sprite='{sprite.name}' pngBytes={png.Length} " +
                $"size={rect.width}x{rect.height}");
            return true;
        }
        catch (Exception exception)
        {
            Texture2D source = sprite.texture;
            error = $"{exception.GetType().Name}: {exception.Message}";
            Debug.LogWarning(
                $"{LogPrefix}[FAILED] stage=exception sprite='{sprite.name}' " +
                $"texture='{(source != null ? source.name : "<null>")}' " +
                $"readable={source != null && source.isReadable} packed={sprite.packed} " +
                $"packingMode={sprite.packingMode} packingRotation={sprite.packingRotation} " +
                $"reason='{error}'");
            return false;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);

            if (readableCopy != null)
                UnityEngine.Object.Destroy(readableCopy);
        }
    }

    private static bool TryGetSpriteTextureRect(
        Sprite sprite,
        out RectInt rect,
        out string source,
        out string error)
    {
        rect = default;
        source = "textureRect";
        error = "";

        try
        {
            Rect textureRect = sprite.textureRect;
            rect = ClampRect(textureRect, sprite.texture.width, sprite.texture.height);
            return rect.width > 0 && rect.height > 0;
        }
        catch (Exception textureRectException)
        {
            // Tight-packed sprites may reject textureRect in player builds.
            // UV bounds still identify the occupied area inside the atlas.
            Vector2[] uv = sprite.uv;
            if (uv == null || uv.Length == 0)
            {
                error =
                    $"Sprite.textureRect failed ({textureRectException.GetType().Name}: " +
                    $"{textureRectException.Message}) and sprite has no UVs.";
                return false;
            }

            float minU = float.PositiveInfinity;
            float minV = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float maxV = float.NegativeInfinity;

            for (int i = 0; i < uv.Length; i++)
            {
                minU = Mathf.Min(minU, uv[i].x);
                minV = Mathf.Min(minV, uv[i].y);
                maxU = Mathf.Max(maxU, uv[i].x);
                maxV = Mathf.Max(maxV, uv[i].y);
            }

            int textureWidth = sprite.texture.width;
            int textureHeight = sprite.texture.height;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(minU * textureWidth), 0, textureWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(minV * textureHeight), 0, textureHeight - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(maxU * textureWidth), xMin + 1, textureWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(maxV * textureHeight), yMin + 1, textureHeight);

            rect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            source = "uvBoundsFallback";

            if (rect.width <= 0 || rect.height <= 0)
            {
                error =
                    $"Could not derive a valid atlas rect from UVs after textureRect failed: " +
                    $"{textureRectException.Message}";
                return false;
            }

            Debug.LogWarning(
                $"{LogPrefix}[RECT_FALLBACK] sprite='{sprite.name}' " +
                $"textureRectError='{textureRectException.Message}' uvRect={rect.x},{rect.y},{rect.width},{rect.height}");
            return true;
        }
    }

    private static RectInt ClampRect(Rect source, int textureWidth, int textureHeight)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(source.x), 0, textureWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(source.y), 0, textureHeight - 1);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(source.xMax), x + 1, textureWidth);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(source.yMax), y + 1, textureHeight);
        return new RectInt(x, y, xMax - x, yMax - y);
    }

    private static void CopyReadablePixels(
        Texture2D source,
        RectInt rect,
        Texture2D destination)
    {
        Color[] pixels = source.GetPixels(
            rect.x,
            rect.y,
            rect.width,
            rect.height);
        destination.SetPixels(pixels);
        destination.Apply(false, false);
    }

    private static void CopyGpuPixels(
        Texture2D source,
        RectInt rect,
        Texture2D destination,
        ref RenderTexture renderTexture)
    {
        renderTexture = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);

        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;
        destination.ReadPixels(
            new Rect(rect.x, rect.y, rect.width, rect.height),
            0,
            0,
            false);
        destination.Apply(false, false);
    }
}
