using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PhoneMessageBubble : MonoBehaviour
{
    [Header("Layout refs")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private LayoutElement leftSpacer;
    [SerializeField] private LayoutElement bubbleLayout;
    [SerializeField] private LayoutElement rightSpacer;

    [Header("Text refs")]
    [SerializeField] private TMP_Text contactNameText;
    [SerializeField] private LayoutElement contactNameLayout;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private LayoutElement messageLayout;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private LayoutElement timeLayout;

    [Header("Sizing")]
    [Range(0.45f, 0.90f)]
    [SerializeField] private float maxBubbleWidthRatio = 0.74f;
    [SerializeField] private float maxBubbleWidthAbsolute = 420f;
    [SerializeField] private float minBubbleWidth = 56f;
    [SerializeField] private float minInnerWidth = 28f;

    [Tooltip("Left + Right padding from Bubble VerticalLayoutGroup.")]
    [SerializeField] private float horizontalPadding = 24f;

    [Tooltip("Top + Bottom padding from Bubble VerticalLayoutGroup.")]
    [SerializeField] private float verticalPadding = 16f;

    [SerializeField] private float verticalSpacing = 2f;

    [Header("Hard wrap")]
    [Tooltip("Adds zero-width spaces into very long runs without spaces: links, hashes, aaaaaaaaaaaaa, 123123123...")]
    [SerializeField] private int hardWrapEvery = 18;

    public void SetViewport(RectTransform newViewport)
    {
        viewport = newViewport;
        Recalculate();
    }

    public void Setup(PhoneMessageSide side, string contactName, string message, string time)
    {
        bool outgoing = side == PhoneMessageSide.Outgoing;

        SetSide(outgoing);

        if (contactNameText != null)
        {
            bool showName = !outgoing && !string.IsNullOrWhiteSpace(contactName);
            contactNameText.gameObject.SetActive(showName);
            contactNameText.text = contactName ?? string.Empty;
            ConfigureText(contactNameText);
        }

        if (messageText != null)
        {
            messageText.text = BreakLongRuns(message ?? string.Empty, hardWrapEvery);
            ConfigureText(messageText);
        }

        if (timeText != null)
        {
            bool showTime = !string.IsNullOrWhiteSpace(time);
            timeText.gameObject.SetActive(showTime);
            timeText.text = time ?? string.Empty;
            ConfigureText(timeText);
        }

        Recalculate();
    }

    private void OnValidate()
    {
        ConfigureText(contactNameText);
        ConfigureText(messageText);
        ConfigureText(timeText);
        Recalculate();
    }

    private void OnRectTransformDimensionsChange()
    {
        Recalculate();
    }

    private void SetSide(bool outgoing)
    {
        ApplySpacer(leftSpacer, outgoing ? 1f : 0f);
        ApplySpacer(rightSpacer, outgoing ? 0f : 1f);
    }

    private static void ApplySpacer(LayoutElement spacer, float flexibleWidth)
    {
        if (spacer == null)
            return;

        spacer.minWidth = 0f;
        spacer.preferredWidth = 0f;
        spacer.flexibleWidth = flexibleWidth;

        spacer.minHeight = 0f;
        spacer.preferredHeight = 0f;
        spacer.flexibleHeight = 0f;
    }

    private static void ConfigureText(TMP_Text text)
    {
        if (text == null)
            return;

        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
    }

    public void Recalculate()
    {
        if (viewport == null || bubbleLayout == null || messageText == null)
            return;

        float availableWidth = viewport.rect.width;
        if (availableWidth <= 1f)
            return;

        float maxBubbleWidth = Mathf.Min(maxBubbleWidthAbsolute, availableWidth * maxBubbleWidthRatio);
        maxBubbleWidth = Mathf.Max(minBubbleWidth, maxBubbleWidth);

        float maxInnerWidth = Mathf.Max(minInnerWidth, maxBubbleWidth - horizontalPadding);

        float wantedInnerWidth = Mathf.Max(
            GetNaturalWidth(contactNameText),
            GetNaturalWidth(messageText),
            GetNaturalWidth(timeText)
        );

        wantedInnerWidth = Mathf.Clamp(wantedInnerWidth, minInnerWidth, maxInnerWidth);

        float bubbleWidth = Mathf.Clamp(
            Mathf.Ceil(wantedInnerWidth + horizontalPadding),
            minBubbleWidth,
            maxBubbleWidth
        );

        float innerWidth = Mathf.Max(minInnerWidth, bubbleWidth - horizontalPadding);
        float preferredHeight = verticalPadding;
        int visibleTextBlocks = 0;

        preferredHeight += ApplyTextSize(contactNameText, contactNameLayout, innerWidth, ref visibleTextBlocks);
        preferredHeight += ApplyTextSize(messageText, messageLayout, innerWidth, ref visibleTextBlocks);
        preferredHeight += ApplyTextSize(timeText, timeLayout, innerWidth, ref visibleTextBlocks);

        if (visibleTextBlocks > 1)
            preferredHeight += verticalSpacing * (visibleTextBlocks - 1);

        bubbleLayout.minWidth = minBubbleWidth;
        bubbleLayout.preferredWidth = bubbleWidth;
        bubbleLayout.flexibleWidth = 0f;

        bubbleLayout.minHeight = Mathf.Ceil(preferredHeight);
        bubbleLayout.preferredHeight = Mathf.Ceil(preferredHeight);
        bubbleLayout.flexibleHeight = 0f;
    }

    private static float GetNaturalWidth(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeSelf)
            return 0f;

        ConfigureText(text);
        text.ForceMeshUpdate();
        return Mathf.Ceil(text.GetPreferredValues(text.text, 10000f, 0f).x);
    }

    private static float ApplyTextSize(
        TMP_Text text,
        LayoutElement layoutElement,
        float width,
        ref int visibleTextBlocks)
    {
        if (text == null || !text.gameObject.activeSelf)
            return 0f;

        ConfigureText(text);

        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        text.ForceMeshUpdate();

        Vector2 preferred = text.GetPreferredValues(text.text, width, 0f);
        float height = Mathf.Ceil(preferred.y);

        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (layoutElement != null)
        {
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0f;

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0f;
        }

        visibleTextBlocks++;
        return height;
    }

    private static string BreakLongRuns(string source, int every)
    {
        if (string.IsNullOrEmpty(source) || every <= 0)
            return source;

        const char zeroWidthSpace = '\u200B';

        StringBuilder builder = new StringBuilder(source.Length + source.Length / every);
        int runLength = 0;
        bool insideRichTextTag = false;

        foreach (char character in source)
        {
            if (character == '<')
                insideRichTextTag = true;

            builder.Append(character);

            if (insideRichTextTag)
            {
                if (character == '>')
                    insideRichTextTag = false;

                continue;
            }

            bool isBreakPoint = char.IsWhiteSpace(character)
                                || character == '-'
                                || character == '/'
                                || character == '\\'
                                || character == '_'
                                || character == zeroWidthSpace;

            if (isBreakPoint)
            {
                runLength = 0;
                continue;
            }

            runLength++;
            if (runLength >= every)
            {
                builder.Append(zeroWidthSpace);
                runLength = 0;
            }
        }

        return builder.ToString();
    }
}
