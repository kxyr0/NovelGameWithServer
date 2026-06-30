using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DecodedAnimatedGif
{
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public List<Texture2D> Frames { get; } = new List<Texture2D>();
    public List<float> Delays { get; } = new List<float>();
}

public static class AnimatedGifDecoder
{
    const int MaxDictionarySize = 4096;

    public static DecodedAnimatedGif Decode(byte[] bytes, string textureName = "Animated GIF")
    {
        if (bytes == null || bytes.Length < 13)
            throw new ArgumentException("GIF data is empty or too small.");

        var reader = new GifByteReader(bytes);
        string signature = reader.ReadString(6);
        if (signature != "GIF87a" && signature != "GIF89a")
            throw new ArgumentException("Data is not a GIF file.");

        int screenWidth = reader.ReadUInt16();
        int screenHeight = reader.ReadUInt16();
        if (screenWidth <= 0 || screenHeight <= 0)
            throw new ArgumentException("GIF has invalid dimensions.");

        byte packed = reader.ReadByte();
        bool hasGlobalColorTable = (packed & 0x80) != 0;
        int globalColorTableSize = 1 << ((packed & 0x07) + 1);
        reader.ReadByte();
        reader.ReadByte();

        Color32[] globalColorTable = hasGlobalColorTable
            ? reader.ReadColorTable(globalColorTableSize)
            : null;

        var result = new DecodedAnimatedGif
        {
            Width = screenWidth,
            Height = screenHeight
        };

        var canvas = new Color32[screenWidth * screenHeight];
        Clear(canvas, Transparent);

        var graphicControl = GraphicControl.Default;
        int frameIndex = 0;

        while (!reader.EndOfData)
        {
            byte marker = reader.ReadByte();
            if (marker == 0x3B)
                break;

            if (marker == 0x21)
            {
                graphicControl = ReadExtension(reader, graphicControl);
                continue;
            }

            if (marker != 0x2C)
                throw new ArgumentException($"Unexpected GIF block marker 0x{marker:X2}.");

            ReadImage(
                reader,
                globalColorTable,
                graphicControl,
                result,
                canvas,
                textureName,
                frameIndex++);

            graphicControl = GraphicControl.Default;
        }

        return result;
    }

    static GraphicControl ReadExtension(GifByteReader reader, GraphicControl current)
    {
        byte label = reader.ReadByte();
        if (label != 0xF9)
        {
            reader.SkipSubBlocks();
            return current;
        }

        int blockSize = reader.ReadByte();
        if (blockSize < 4)
        {
            reader.Skip(blockSize);
            reader.SkipBlockTerminator();
            return current;
        }

        byte packed = reader.ReadByte();
        int delayHundredths = reader.ReadUInt16();
        byte transparentIndex = reader.ReadByte();

        if (blockSize > 4)
            reader.Skip(blockSize - 4);

        reader.SkipBlockTerminator();

        return new GraphicControl
        {
            DisposalMethod = (packed >> 2) & 0x07,
            HasTransparency = (packed & 0x01) != 0,
            TransparentColorIndex = transparentIndex,
            DelaySeconds = delayHundredths > 0 ? delayHundredths / 100f : 1f / 24f
        };
    }

    static void ReadImage(
        GifByteReader reader,
        Color32[] globalColorTable,
        GraphicControl graphicControl,
        DecodedAnimatedGif result,
        Color32[] canvas,
        string textureName,
        int frameIndex)
    {
        int left = reader.ReadUInt16();
        int top = reader.ReadUInt16();
        int width = reader.ReadUInt16();
        int height = reader.ReadUInt16();
        byte packed = reader.ReadByte();

        bool hasLocalColorTable = (packed & 0x80) != 0;
        bool interlaced = (packed & 0x40) != 0;
        int localColorTableSize = 1 << ((packed & 0x07) + 1);

        Color32[] colorTable = hasLocalColorTable
            ? reader.ReadColorTable(localColorTableSize)
            : globalColorTable;

        if (colorTable == null || colorTable.Length == 0)
            throw new ArgumentException("GIF frame has no color table.");

        int lzwMinimumCodeSize = reader.ReadByte();
        byte[] imageData = reader.ReadSubBlocks();
        int[] indices = DecodeLzw(imageData, lzwMinimumCodeSize, width * height);

        Color32[] previousCanvas = graphicControl.DisposalMethod == 3
            ? (Color32[])canvas.Clone()
            : null;

        DrawFrame(canvas, result.Width, result.Height, left, top, width, height, colorTable, indices, interlaced, graphicControl);

        var texture = new Texture2D(result.Width, result.Height, TextureFormat.RGBA32, false)
        {
            name = $"{textureName}_{frameIndex:000}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(canvas);
        texture.Apply(false, true);

        result.Frames.Add(texture);
        result.Delays.Add(Mathf.Max(0.01f, graphicControl.DelaySeconds));

        ApplyDisposal(canvas, result.Width, result.Height, left, top, width, height, graphicControl, previousCanvas);
    }

    static void DrawFrame(
        Color32[] canvas,
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int frameWidth,
        int frameHeight,
        Color32[] colorTable,
        int[] indices,
        bool interlaced,
        GraphicControl graphicControl)
    {
        int sourceIndex = 0;
        if (!interlaced)
        {
            for (int y = 0; y < frameHeight; y++)
                DrawRow(canvas, screenWidth, screenHeight, left, top, frameWidth, colorTable, indices, graphicControl, y, ref sourceIndex);
            return;
        }

        DrawInterlacedRows(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight, colorTable, indices, graphicControl, ref sourceIndex);
    }

    static void DrawInterlacedRows(
        Color32[] canvas,
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int frameWidth,
        int frameHeight,
        Color32[] colorTable,
        int[] indices,
        GraphicControl graphicControl,
        ref int sourceIndex)
    {
        DrawInterlacePass(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight, colorTable, indices, graphicControl, 0, 8, ref sourceIndex);
        DrawInterlacePass(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight, colorTable, indices, graphicControl, 4, 8, ref sourceIndex);
        DrawInterlacePass(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight, colorTable, indices, graphicControl, 2, 4, ref sourceIndex);
        DrawInterlacePass(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight, colorTable, indices, graphicControl, 1, 2, ref sourceIndex);
    }

    static void DrawInterlacePass(
        Color32[] canvas,
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int frameWidth,
        int frameHeight,
        Color32[] colorTable,
        int[] indices,
        GraphicControl graphicControl,
        int start,
        int step,
        ref int sourceIndex)
    {
        for (int y = start; y < frameHeight; y += step)
            DrawRow(canvas, screenWidth, screenHeight, left, top, frameWidth, colorTable, indices, graphicControl, y, ref sourceIndex);
    }

    static void DrawRow(
        Color32[] canvas,
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int frameWidth,
        Color32[] colorTable,
        int[] indices,
        GraphicControl graphicControl,
        int frameY,
        ref int sourceIndex)
    {
        int screenY = top + frameY;
        if (screenY < 0 || screenY >= screenHeight)
        {
            sourceIndex += frameWidth;
            return;
        }

        int textureY = screenHeight - 1 - screenY;
        int rowStart = textureY * screenWidth;

        for (int x = 0; x < frameWidth; x++)
        {
            if (sourceIndex >= indices.Length)
                return;

            int colorIndex = indices[sourceIndex++];
            int screenX = left + x;
            if (screenX < 0 || screenX >= screenWidth)
                continue;

            if (graphicControl.HasTransparency && colorIndex == graphicControl.TransparentColorIndex)
                continue;

            if (colorIndex >= 0 && colorIndex < colorTable.Length)
                canvas[rowStart + screenX] = colorTable[colorIndex];
        }
    }

    static void ApplyDisposal(
        Color32[] canvas,
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int frameWidth,
        int frameHeight,
        GraphicControl graphicControl,
        Color32[] previousCanvas)
    {
        if (graphicControl.DisposalMethod == 2)
        {
            ClearRect(canvas, screenWidth, screenHeight, left, top, frameWidth, frameHeight);
        }
        else if (graphicControl.DisposalMethod == 3 && previousCanvas != null && previousCanvas.Length == canvas.Length)
        {
            Array.Copy(previousCanvas, canvas, canvas.Length);
        }
    }

    static int[] DecodeLzw(byte[] data, int minimumCodeSize, int expectedSize)
    {
        if (minimumCodeSize < 2 || minimumCodeSize > 8)
            throw new ArgumentException("GIF LZW minimum code size is invalid.");

        int clearCode = 1 << minimumCodeSize;
        int endCode = clearCode + 1;
        int codeSize = minimumCodeSize + 1;
        int bitPosition = 0;

        var dictionary = new List<int[]>(MaxDictionarySize);
        ResetDictionary(dictionary, clearCode);

        var output = new List<int>(Mathf.Max(expectedSize, 0));
        int[] previous = null;

        while (true)
        {
            int code = ReadCode(data, ref bitPosition, codeSize);
            if (code < 0)
                break;

            if (code == clearCode)
            {
                ResetDictionary(dictionary, clearCode);
                codeSize = minimumCodeSize + 1;
                previous = null;
                continue;
            }

            if (code == endCode)
                break;

            int[] entry;
            if (code < dictionary.Count && dictionary[code] != null)
            {
                entry = dictionary[code];
            }
            else if (code == dictionary.Count && previous != null)
            {
                entry = Append(previous, previous[0]);
            }
            else
            {
                break;
            }

            output.AddRange(entry);

            if (previous != null && dictionary.Count < MaxDictionarySize)
            {
                dictionary.Add(Append(previous, entry[0]));
                if (dictionary.Count == (1 << codeSize) && codeSize < 12)
                    codeSize++;
            }

            previous = entry;

            if (expectedSize > 0 && output.Count >= expectedSize)
                break;
        }

        if (expectedSize > 0 && output.Count > expectedSize)
            output.RemoveRange(expectedSize, output.Count - expectedSize);

        return output.ToArray();
    }

    static void ResetDictionary(List<int[]> dictionary, int clearCode)
    {
        dictionary.Clear();
        for (int i = 0; i < clearCode; i++)
            dictionary.Add(new[] { i });

        dictionary.Add(null);
        dictionary.Add(null);
    }

    static int ReadCode(byte[] data, ref int bitPosition, int codeSize)
    {
        int code = 0;
        for (int i = 0; i < codeSize; i++)
        {
            int absoluteBit = bitPosition + i;
            int byteIndex = absoluteBit >> 3;
            if (byteIndex >= data.Length)
                return -1;

            if ((data[byteIndex] & (1 << (absoluteBit & 7))) != 0)
                code |= 1 << i;
        }

        bitPosition += codeSize;
        return code;
    }

    static int[] Append(int[] source, int value)
    {
        var result = new int[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = value;
        return result;
    }

    static void Clear(Color32[] pixels, Color32 color)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
    }

    static void ClearRect(Color32[] canvas, int screenWidth, int screenHeight, int left, int top, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            int screenY = top + y;
            if (screenY < 0 || screenY >= screenHeight)
                continue;

            int textureY = screenHeight - 1 - screenY;
            int rowStart = textureY * screenWidth;

            for (int x = 0; x < width; x++)
            {
                int screenX = left + x;
                if (screenX < 0 || screenX >= screenWidth)
                    continue;

                canvas[rowStart + screenX] = Transparent;
            }
        }
    }

    static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

    struct GraphicControl
    {
        public int DisposalMethod;
        public bool HasTransparency;
        public int TransparentColorIndex;
        public float DelaySeconds;

        public static GraphicControl Default => new GraphicControl
        {
            DisposalMethod = 0,
            HasTransparency = false,
            TransparentColorIndex = -1,
            DelaySeconds = 1f / 24f
        };
    }

    sealed class GifByteReader
    {
        readonly byte[] _bytes;
        int _position;

        public GifByteReader(byte[] bytes)
        {
            _bytes = bytes;
        }

        public bool EndOfData => _position >= _bytes.Length;

        public byte ReadByte()
        {
            if (_position >= _bytes.Length)
                throw new ArgumentException("Unexpected end of GIF data.");

            return _bytes[_position++];
        }

        public int ReadUInt16()
        {
            int low = ReadByte();
            int high = ReadByte();
            return low | (high << 8);
        }

        public string ReadString(int count)
        {
            if (_position + count > _bytes.Length)
                throw new ArgumentException("Unexpected end of GIF data.");

            string value = System.Text.Encoding.ASCII.GetString(_bytes, _position, count);
            _position += count;
            return value;
        }

        public Color32[] ReadColorTable(int size)
        {
            var colors = new Color32[size];
            for (int i = 0; i < size; i++)
            {
                byte r = ReadByte();
                byte g = ReadByte();
                byte b = ReadByte();
                colors[i] = new Color32(r, g, b, 255);
            }

            return colors;
        }

        public byte[] ReadSubBlocks()
        {
            var data = new List<byte>();
            while (true)
            {
                int length = ReadByte();
                if (length == 0)
                    break;

                if (_position + length > _bytes.Length)
                    throw new ArgumentException("Unexpected end of GIF sub-block data.");

                for (int i = 0; i < length; i++)
                    data.Add(_bytes[_position++]);
            }

            return data.ToArray();
        }

        public void SkipSubBlocks()
        {
            while (true)
            {
                int length = ReadByte();
                if (length == 0)
                    return;

                Skip(length);
            }
        }

        public void SkipBlockTerminator()
        {
            if (!EndOfData)
                ReadByte();
        }

        public void Skip(int count)
        {
            if (_position + count > _bytes.Length)
                throw new ArgumentException("Unexpected end of GIF data.");

            _position += count;
        }
    }
}
