using System;
using UnityEngine;

public enum DivinationCardVisualEffectMode
{
    None = 0,
    Flash = 1,
    Shader = 2,
    MaterialSwap = 3,
    Custom = 4
}

[Serializable]
public struct DivinationCardViewConfig
{
    [Tooltip("ID/ключ карты, который возвращает /player/tarot/draw. Должен совпадать с TarotCard.id из админки.")]
    public string CardId;

    [Tooltip("Локальный спрайт лицевой стороны карты. Назначается вручную в инспекторе Unity.")]
    public Sprite FrontSprite;

    [Tooltip("Локальное запасное название. Используется только если бэкенд не вернул title/name.")]
    public string FallbackTitle;

    [TextArea(2, 6)]
    [Tooltip("Локальное запасное описание. Используется только если бэкенд не вернул description.")]
    public string FallbackDescription;

    [Tooltip("Включите, чтобы для этой карты переопределить стандартный режим визуального эффекта.")]
    public bool OverrideVisualEffectMode;

    [Tooltip("Режим визуального эффекта для этой карты. Работает только при включенном переопределении режима эффекта.")]
    public DivinationCardVisualEffectMode VisualEffectMode;

    [Tooltip("Материал для режимов Shader или MaterialSwap у этой карты. Если задан, имеет приоритет над настройками по умолчанию.")]
    public Material OverrideMaterial;

    [Tooltip("Шейдер для режима Shader у этой карты, если материал переопределения не назначен.")]
    public Shader OverrideShader;

    public string NormalizedCardId => DivinationCardIdUtility.Normalize(CardId);
}

public static class DivinationCardIdUtility
{
    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}
