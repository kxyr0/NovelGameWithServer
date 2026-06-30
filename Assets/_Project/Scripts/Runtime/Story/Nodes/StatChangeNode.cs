using UnityEngine;

/// <summary>
/// Нода изменения стата.
/// Поля:
///   statId     — ключ стата (например "town", "reputation", "story")
///   delta      — на сколько изменить (может быть отрицательным)
///   displayName — отображаемое название для тоста (если пусто — тост не показывается)
///   systemMessage — кастомное системное сообщение (если пусто — генерируется автоматически)
/// </summary>
public class StatChangeNode : BaseStoryNode
{
    public string statId;
    public int delta;

    [Tooltip("Заголовок всплывающего сообщения, например 'Отношения с Этаном'. Если оставить пустым, сообщение не появится.")]
    public string displayName;

    [Tooltip("Свой текст системного сообщения. Если оставить пустым, текст будет собран автоматически из названия и изменения значения.")]
    public string systemMessage;
}
