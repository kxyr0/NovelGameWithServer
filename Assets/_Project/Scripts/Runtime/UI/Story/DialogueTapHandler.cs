using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Обработчик тапа/клика для смены реплик.
///
/// Подключение:
/// 1. Создай пустой GameObject "TapArea" внутри Canvas (поверх сцены, под UI-кнопками).
/// 2. Добавь компонент Image (цвет прозрачный, alpha = 0) — нужен для raycast.
/// 3. Прикрепи этот скрипт.
/// 4. TapArea должен перекрывать всю область диалога, но находиться НИЖЕ кнопок выбора
///    и других интерактивных элементов в иерархии Canvas (чтобы они перехватывали нажатие первыми).
///
/// Поведение:
/// - Тап в любом месте TapArea → StoryManager.OnDialogueClick()
/// - Если активны кнопки выбора — тап НЕ срабатывает (choiceContainer не пустой)
/// - Работает и на мобильных (Touch), и в редакторе (Mouse)
/// </summary>
public class DialogueTapHandler : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Контейнер кнопок выбора. Если внутри есть активные варианты, тап по диалогу не переключает реплику.")]
    public Transform choiceContainer;

    [Tooltip("Сколько секунд блокировать тап, пока идёт анимация появления персонажа.")]
    public float tapCooldownAfterSceneChange = 0.3f;

    float _cooldownTimer = 0f;
    Graphic _raycastGraphic;

    void Awake()
    {
        // Убедиться что Image не блокирует рейкасты (цвет прозрачный, но raycastTarget = true)
        _raycastGraphic = EnsureRaycastGraphic();
        SetGraphicRaycastEnabled(true);
    }

    void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryAdvance();
    }

    /// <summary>
    /// Вызывается при тапе. Можно вызвать и вручную (например, из кнопки).
    /// </summary>
    public void TryAdvance(bool ignoreCooldown = false)
    {
        // Cooldown после смены сцены
        if (!ignoreCooldown && _cooldownTimer > 0f) return;

        // Если показаны варианты выбора — тап не пролистывает реплики
        if (choiceContainer != null && choiceContainer.childCount > 0) return;

        StoryManager.Instance?.OnDialogueClick();
    }

    /// <summary>
    /// Вызови это при смене сцены/появлении персонажа — предотвратит случайный пролистывание.
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownTimer = tapCooldownAfterSceneChange;
    }

    /// <summary>
    /// Включить/выключить область тапа (например, во время системных попапов).
    /// </summary>
    public void SetEnabled(bool value)
    {
        if (_raycastGraphic == null)
            _raycastGraphic = EnsureRaycastGraphic();

        SetGraphicRaycastEnabled(value);
    }

    Graphic EnsureRaycastGraphic()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
            return graphic;

        Image image = gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return image;
    }

    void SetGraphicRaycastEnabled(bool value)
    {
        if (_raycastGraphic == null)
            return;

        if (_raycastGraphic is Image image && image.sprite == null)
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);

        _raycastGraphic.raycastTarget = value;
    }
}
