using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Нода диалога в стиле SMS/мессенджера.
/// Реплики появляются как пузыри — входящие слева, исходящие справа.
///
/// Поля:
///   messages    — список сообщений (отправитель + текст + сторона)
///   contactName — имя контакта в шапке (например "Этан")
///   contactAvatar — аватар контакта
///   typingDelay — задержка перед появлением каждого сообщения (имитация печати)
/// </summary>
public class PhoneDialogueNode : BaseStoryNode
{
    [System.NonSerialized] public string previewStoryId;

    [Tooltip("Имя собеседника, которое показывается в шапке чата.")]
    public string contactName;

    [Tooltip("Controls which name is shown in the phone header while this phone dialogue plays.")]
    public PhoneHeaderContactMode headerContactMode = PhoneHeaderContactMode.CurrentIncomingSender;

    [Tooltip("Необязательный аватар собеседника в чате.")]
    public Sprite contactAvatar;

    [Tooltip("Пауза перед появлением каждого сообщения, в секундах.")]
    public float typingDelay = 0.8f;

    public List<PhoneMessage> messages = new List<PhoneMessage>();
}

[System.Serializable]
public class PhoneMessage
{
    [Tooltip("Имя отправителя именно для этой реплики. Например: Мэг или {PlayerName}. Если пусто, UI попытается вывести имя по стороне сообщения.")]
    public string senderName;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Необязательный текст времени для этого сообщения, например 15:25. Если оставить пустым, текст времени из шаблона не перезаписывается.")]
    public string timeText;

    [Tooltip("Сторона сообщения: Incoming означает входящую реплику слева от собеседника, Outgoing - исходящую справа от героини.")]
    public PhoneMessageSide side = PhoneMessageSide.Incoming;

    [Tooltip("Необязательное изображение-вложение, например фото в чате.")]
    public Sprite attachment;

    [Tooltip("Use the phone photo-message layout path even when attachment is empty. Set automatically by [photo]/[\\u0444\\u043E\\u0442\\u043E] tokens.")]
    public bool usePhotoLayout;
}

public enum PhoneHeaderContactMode
{
    CurrentIncomingSender,
    ContactName
}

public enum PhoneMessageSide
{
    Incoming,   // Слева (собеседник)
    Outgoing    // Справа (героиня / ГГ)
}
