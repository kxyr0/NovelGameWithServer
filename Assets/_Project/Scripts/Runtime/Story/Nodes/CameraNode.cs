using UnityEngine;
using XNode;

/// <summary>
/// Нода ручного управления камерой.
/// Позволяет плавно сдвинуть "камеру" (фон + персонажи) в любую сторону.
///
/// Режимы:
///   Position  — переместить к конкретной позиции персонажа (Left / Center / Right)
///   Offset    — сдвинуть на произвольное количество пикселей по X
///   Reset     — мгновенно вернуть в центр
///
/// Использование:
///   Добавь ноду в граф между SceneSetupNode и DialogueNode.
///   Например, перед панорамным показом фона — CameraNode с Offset = 300, потом -300.
/// </summary>
public class CameraNode : BaseStoryNode
{
    public enum CameraMode
    {
        /// <summary>Переместить к позиции персонажа</summary>
        Position,
        /// <summary>Сдвинуть на произвольное смещение в пикселях</summary>
        Offset,
        /// <summary>Мгновенно вернуть в центр</summary>
        Reset
    }

    [Header("Режим")]
    public CameraMode mode = CameraMode.Position;

    [Header("Цель позиции")]
    [Tooltip("Слот персонажа, к которому нужно переместить камеру в режиме Position.")]
    public CharacterPosition targetPosition = CharacterPosition.Center;

    [Header("Ручное смещение")]
    [Tooltip("Смещение камеры по X в пикселях. Положительное значение сдвигает вправо, отрицательное — влево.")]
    public float xOffset = 200f;

    [Header("Настройки анимации")]
    [Tooltip("Длительность анимации камеры. 0 означает использовать стандартную длительность из CameraController.")]
    public float duration = 0f;
}
