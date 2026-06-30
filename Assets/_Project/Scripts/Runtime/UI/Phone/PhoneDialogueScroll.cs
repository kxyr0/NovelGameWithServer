using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PhoneDialogueScroll : MonoBehaviour
{
    const float ScrollBottomStickEpsilon = 0.035f;

    [Header("Scroll refs")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;

    [Header("Prefab")]
    [SerializeField] private PhoneMessageBubble messagePrefab;

    private Coroutine rebuildRoutine;

    private void Reset()
    {
        scrollRect = GetComponentInChildren<ScrollRect>();
        if (scrollRect != null)
        {
            viewport = scrollRect.viewport;
            content = scrollRect.content;
        }
    }

    private void Awake()
    {
        if (scrollRect != null)
        {
            if (viewport == null)
                viewport = scrollRect.viewport;

            if (content == null)
                content = scrollRect.content;
        }
    }

    public PhoneMessageBubble AddIncoming(string contactName, string message, string time = "")
    {
        return AddMessage(PhoneMessageSide.Incoming, contactName, message, time);
    }

    public PhoneMessageBubble AddOutgoing(string message, string time = "")
    {
        return AddMessage(PhoneMessageSide.Outgoing, string.Empty, message, time);
    }

    public PhoneMessageBubble AddMessage(
        PhoneMessageSide side,
        string contactName,
        string message,
        string time = "")
    {
        if (messagePrefab == null)
        {
            Debug.LogError("PhoneDialogueScroll: messagePrefab is not assigned.", this);
            return null;
        }

        if (content == null)
        {
            Debug.LogError("PhoneDialogueScroll: content is not assigned.", this);
            return null;
        }

        bool shouldStickToBottom = ShouldStickToBottom();
        PhoneMessageBubble instance = Instantiate(messagePrefab, content, false);
        instance.SetViewport(viewport);
        instance.Setup(side, contactName, message, time);

        RequestRebuildAndStickToBottom(shouldStickToBottom);
        return instance;
    }

    public void ClearMessages()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        RequestRebuildAndStickToBottom();
    }

    public void RequestRebuildAndStickToBottom()
    {
        RequestRebuildAndStickToBottom(true);
    }

    public void RequestRebuildAndStickToBottom(bool shouldStickToBottom)
    {
        if (!isActiveAndEnabled)
            return;

        if (rebuildRoutine != null)
            StopCoroutine(rebuildRoutine);

        rebuildRoutine = StartCoroutine(RebuildAndStickToBottomRoutine(shouldStickToBottom));
    }

    private IEnumerator RebuildAndStickToBottomRoutine(bool shouldStickToBottom)
    {
        // Даём Unity собрать размеры TMP/Layout после Instantiate.
        yield return null;

        Canvas.ForceUpdateCanvases();

        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null && shouldStickToBottom)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 0f; // 0 = bottom
        }

        rebuildRoutine = null;
    }

    bool ShouldStickToBottom()
    {
        if (scrollRect == null)
            return true;

        RectTransform activeViewport = viewport != null ? viewport : scrollRect.viewport;
        RectTransform activeContent = content != null ? content : scrollRect.content;
        if (activeContent == null || activeViewport == null)
            return scrollRect.verticalNormalizedPosition <= ScrollBottomStickEpsilon;

        if (activeContent.rect.height <= activeViewport.rect.height + 1f)
            return true;

        return scrollRect.verticalNormalizedPosition <= ScrollBottomStickEpsilon;
    }
}
