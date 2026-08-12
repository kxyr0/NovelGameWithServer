using System;
using UnityEngine;

public sealed partial class RemoteUiTextBinder
{
    [Header("Additional visibility")]
    [SerializeField, InspectorName("Дополнительные Canvas Group")]
    [Tooltip("Эти группы скрываются и показываются вместе с удалённым текстом. Добавьте CanvasGroup на нужный общий root и перетащите его сюда.")]
    private CanvasGroup[] _additionalVisibilityGroups = Array.Empty<CanvasGroup>();

    private void ApplyAdditionalVisibility(bool visible)
    {
        if (_additionalVisibilityGroups == null)
            return;

        for (int i = 0; i < _additionalVisibilityGroups.Length; i++)
        {
            CanvasGroup group = _additionalVisibilityGroups[i];
            if (group == null || group == _visibilityCanvasGroup)
                continue;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }

    private void ValidateAdditionalVisibilityGroups()
    {
        if (_additionalVisibilityGroups == null)
            _additionalVisibilityGroups = Array.Empty<CanvasGroup>();
    }
}
