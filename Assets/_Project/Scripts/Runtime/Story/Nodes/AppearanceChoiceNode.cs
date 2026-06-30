using System.Collections.Generic;
using XNode;
using UnityEngine;

/// <summary>
/// Нода выбора внешности ГГ.
/// Устанавливает AppearanceType — все персонажи с inheritAppearanceFromPlayer
/// автоматически сменят спрайты.
///
/// В инспекторе:
///   - promptText     — вопрос игроку, например "Выбери внешность героини"
///   - options        — список вариантов (текст + тип внешности)
///
/// Выходы генерируются динамически через dynamicPortList "choices".
/// Если нужен один выход для всех — используй singleExit = true.
/// </summary>
public class AppearanceChoiceNode : BaseStoryNode
{
    [TextArea]
    public string promptText = "Выбери внешность героини";

    public List<AppearanceOption> options = new List<AppearanceOption>();

    [Tooltip("Если включено, все варианты ведут в один общий выход. Используй, когда продолжение одинаковое для любого выбора.")]
    public bool singleExit = false;

    [Output(dynamicPortList = true)]
    public List<BaseStoryNode> choices;
}

[System.Serializable]
public class AppearanceOption
{
    public string label;           // Текст кнопки: "Европейская", "Афроамериканская" и т.д.
    public AppearanceType type;    // Тип внешности
    public Sprite previewSprite;   // Превью (опционально)
}
