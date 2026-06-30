using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

[AttributeUsage(AttributeTargets.Field)]
public sealed class PhoneLTRBAttribute : PropertyAttribute
{
    public PhoneLTRBAttribute(bool allowNegative = true)
    {
    }
}

public enum PhoneSenderNameAnchor
{
    TopLeft,
    TopRight,
    Custom
}

public enum PhoneSenderNameRelativeTo
{
    BubbleContainer,
    MessageRoot
}

public enum PhoneMessageAppearAnimation
{
    None,
    Fade,
    Slide,
    FadeAndSlide,
    Scale,
    FadeAndScale,
    SlideAndScale,
    FadeSlideAndScale
}

[Serializable]
public sealed class PhoneMessageTemplateLayoutSettings
{
    const int CurrentSettingsVersion = 7;

    [SerializeField, HideInInspector] int _settingsVersion;

    [Header("Имя отправителя")]
    [InspectorName("Показывать имя")]
    [Tooltip("Включает TMP_Text имени отправителя внутри конкретного шаблона баббла. Работает самостоятельно; глобальная галка Показывать имена в бабблах нужна только если хочешь включить имена сразу везде.")]
    public bool showSenderName = false;
    [InspectorName("Скрыть аватар")]
    [Tooltip("Отключает Avatar/AvatarCircle у сообщений этого шаблона. Для телефонных SMS обычно включено, чтобы не появлялась круглая голова поверх баббла.")]
    public bool hideAvatar = true;
    [InspectorName("Отступ имени снизу")]
    [Tooltip("Вертикальный зазор между именем отправителя и бабблом, если имя включено.")]
    public float senderNameBottomSpacing = 4f;
    [InspectorName("Сдвиг имени")]
    [Tooltip("Дополнительный X/Y-сдвиг TMP_Text имени отправителя внутри строки сообщения.")]
    public Vector2 senderNameOffset;
    [InspectorName("Sender Name Anchor")]
    [Tooltip("Anchor used for the sender name. TopLeft/TopRight pin it to the chosen top corner; Custom keeps the template anchors and only applies offset.")]
    public PhoneSenderNameAnchor senderNameAnchor = PhoneSenderNameAnchor.TopLeft;
    [InspectorName("Sender Name Relative To")]
    [Tooltip("Reference rect for Sender Name Anchor: the bubble container itself or the whole message root row.")]
    public PhoneSenderNameRelativeTo senderNameRelativeTo = PhoneSenderNameRelativeTo.BubbleContainer;
    [InspectorName("Добавка размера имени")]
    [Tooltip("Добавляет X/Y к RectTransform имени отправителя. Помогает, если TMP_Text имени обрезается или нужно дать ему больше места.")]
    public Vector2 senderNameSizeOffset;
    [InspectorName("Margin имени")]
    [Tooltip("Полный TMP margin имени в формате Left / Right / Top / Bottom. Нули означают: использовать Отступ имени снизу.")]
    [PhoneLTRB]
    public Vector4 senderNameMargin;
    [InspectorName("Размер шрифта имени")]
    [Tooltip("Размер шрифта TMP_Text имени. 0 = оставить размер из шаблона.")]
    [Min(0f)] public float senderNameFontSize;
    [InspectorName("Переопределить Auto Size имени")]
    [Tooltip("Если включено, этот layout сам задает TMP Auto Size для имени. Если выключено, остается настройка из шаблона.")]
    public bool overrideSenderNameAutoSize;
    [InspectorName("Auto Size имени")]
    [Tooltip("Включает или выключает TMP Auto Size для имени, если активно Переопределить Auto Size имени.")]
    public bool senderNameAutoSize;
    [InspectorName("Мин. шрифт имени")]
    [Tooltip("Минимальный шрифт имени для TMP Auto Size. 0 = оставить значение шаблона.")]
    [Min(0f)] public float senderNameMinFontSize;
    [InspectorName("Макс. шрифт имени")]
    [Tooltip("Максимальный шрифт имени для TMP Auto Size. 0 = оставить значение шаблона.")]
    [Min(0f)] public float senderNameMaxFontSize;
    [InspectorName("Line Spacing имени")]
    [Tooltip("Межстрочный интервал TMP_Text имени. 0 = оставить значение из шаблона.")]
    public float senderNameLineSpacing;

    [Header("Текст времени")]
    [InspectorName("Показывать время")]
    [Tooltip("Включает TMP_Text времени сообщения, например 15:25. Если в шаблоне нет ссылки TimeText, проверка ссылок покажет предупреждение.")]
    public bool showTimeText = true;
    [InspectorName("Сдвиг времени")]
    [Tooltip("Дополнительный X/Y-сдвиг TMP_Text времени сообщения.")]
    public Vector2 timeTextOffset;
    [InspectorName("Добавка размера времени")]
    [Tooltip("Добавляет X/Y к RectTransform текста времени.")]
    public Vector2 timeTextSizeOffset;
    [InspectorName("Margin времени")]
    [Tooltip("TMP margin текста времени в формате Left / Right / Top / Bottom. Нули оставляют margin из шаблона.")]
    [PhoneLTRB]
    public Vector4 timeTextMargin;
    [InspectorName("Размер шрифта времени")]
    [Tooltip("Размер шрифта TMP_Text времени. 0 оставляет значение из шаблона.")]
    [Min(0f)] public float timeTextFontSize;
    [InspectorName("Переопределить Auto Size времени")]
    [Tooltip("Если включено, этот layout управляет TMP Auto Size для текста времени.")]
    public bool overrideTimeTextAutoSize;
    [InspectorName("Auto Size времени")]
    [Tooltip("Включает или выключает TMP Auto Size для текста времени, если активно переопределение Auto Size.")]
    public bool timeTextAutoSize;
    [InspectorName("Мин. шрифт времени")]
    [Tooltip("Минимальный шрифт текста времени для TMP Auto Size. 0 оставляет значение из шаблона.")]
    [Min(0f)] public float timeTextMinFontSize;
    [InspectorName("Макс. шрифт времени")]
    [Tooltip("Максимальный шрифт текста времени для TMP Auto Size. 0 оставляет значение из шаблона.")]
    [Min(0f)] public float timeTextMaxFontSize;
    [InspectorName("Line Spacing времени")]
    [Tooltip("Межстрочный интервал TMP_Text времени. 0 оставляет значение из шаблона.")]
    public float timeTextLineSpacing;

    [Header("Баббл")]
    [InspectorName("Вертикальный отступ строки")]
    [Tooltip("Минимальный вертикальный зазор вокруг строки этого шаблона. Общий список сообщений также использует Message Vertical Spacing.")]
    public float verticalSpacing;
    [InspectorName("Горизонтальный зазор")]
    [Tooltip("X-сдвиг баббла этого типа сообщения. Положительное значение двигает вправо, отрицательное — влево. 0 означает: взять общий Bubble Horizontal Offset.")]
    public float horizontalOffset;
    [InspectorName("Padding сверху")]
    [Tooltip("Внутренний верхний отступ текста от края баббла.")]
    public float topPadding = 10f;
    [InspectorName("Padding снизу")]
    [Tooltip("Внутренний нижний отступ текста от края баббла.")]
    public float bottomPadding = 10f;
    [InspectorName("Padding слева")]
    [Tooltip("Внутренний левый отступ текста от края баббла.")]
    public float leftPadding = 14f;
    [InspectorName("Padding справа")]
    [Tooltip("Внутренний правый отступ текста от края баббла.")]
    public float rightPadding = 14f;
    [InspectorName("Макс. ширина")]
    [Tooltip("Максимальная ширина баббла как доля ширины viewport телефона. 0.74 означает 74%.")]
    public float maxWidthPercent = 0.74f;
    [InspectorName("Мин. ширина")]
    [Tooltip("Минимальная ширина баббла в пикселях, чтобы короткие сообщения не схлопывались.")]
    public float minWidth = 64f;
    [InspectorName("Сдвиг текста внутри баббла")]
    [Tooltip("Дополнительный сдвиг текста внутри баббла. Используй, если текст визуально прилипает к декоративной подложке.")]
    public Vector2 textOffsetInsideBubble;
    [Header("Body Text")]
    [InspectorName("Body Text Scale")]
    [Tooltip("Local scale applied to the message body TMP_Text after layout normalization. Use X = -1 to un-mirror text when the message template or parent is mirrored.")]
    public Vector3 bodyTextScale = Vector3.one;
    [InspectorName("Размер шрифта body")]
    [Tooltip("Размер шрифта основного текста сообщения. 0 = оставить размер из TMP_Text шаблона.")]
    [Min(0f)] public float bodyFontSize;
    [InspectorName("Переопределить Auto Size body")]
    [Tooltip("Если включено, этот layout сам задает TMP Auto Size для основного текста. Если выключено, остается настройка из шаблона.")]
    public bool overrideBodyAutoSize;
    [InspectorName("Auto Size body")]
    [Tooltip("Включает или выключает TMP Auto Size для body-текста, если активно Переопределить Auto Size body.")]
    public bool bodyAutoSize;
    [InspectorName("Мин. шрифт body")]
    [Tooltip("Минимальный шрифт основного текста для TMP Auto Size. 0 = оставить значение шаблона.")]
    [Min(0f)] public float bodyMinFontSize;
    [InspectorName("Макс. шрифт body")]
    [Tooltip("Максимальный шрифт основного текста для TMP Auto Size. 0 = оставить значение шаблона.")]
    [Min(0f)] public float bodyMaxFontSize;
    [InspectorName("Line Spacing body")]
    [Tooltip("Межстрочный интервал основного текста сообщения. 0 = оставить значение из шаблона.")]
    public float bodyLineSpacing;
    [InspectorName("Padding строки")]
    [Tooltip("Отступы HorizontalLayoutGroup строки сообщения в формате Left / Right / Top / Bottom.")]
    [PhoneLTRB]
    public Vector4 rowPadding;
    [InspectorName("Сдвиг строки")]
    [Tooltip("Дополнительный X/Y-сдвиг всей строки сообщения после спавна.")]
    public Vector2 rowPositionOffset;
    [InspectorName("Сдвиг баббла")]
    [Tooltip("Дополнительный X/Y-сдвиг контейнера баббла относительно строки.")]
    public Vector2 bubblePositionOffset;
    [InspectorName("Добавка размера баббла")]
    [Tooltip("Добавляет ширину/высоту к рассчитанному размеру баббла. Можно использовать отрицательные значения, но итоговый размер не станет меньше Min Bubble Width.")]
    public Vector2 bubbleSizeOffset;
    [InspectorName("Сдвиг аватара")]
    [Tooltip("Дополнительный X/Y-сдвиг Avatar/AvatarCircle, если аватары включены.")]
    public Vector2 avatarOffset;
    [InspectorName("Размер фото")]
    [Tooltip("Размер image-вложения для сообщений с [photo]/[фото], если вложение действительно назначено.")]
    public Vector2 photoMessageSize = new Vector2(220f, 160f);
    [InspectorName("Сдвиг фото")]
    [Tooltip("Дополнительный X/Y-сдвиг image-вложения внутри баббла.")]
    public Vector2 photoOffset;
    [InspectorName("Выравнивание строки")]
    [Tooltip("Выравнивание строки сообщения: слева для incoming, справа для outgoing, если шаблон не переопределён.")]
    public TextAnchor rowAlignment = TextAnchor.MiddleLeft;

    [Header("Message Root VerticalLayoutGroup")]
    [InspectorName("Use Message VerticalLayoutGroup")]
    [Tooltip("If enabled, the spawned PhoneMessage root uses VerticalLayoutGroup instead of the default HorizontalLayoutGroup.")]
    public bool useMessageRootVerticalLayout;
    [InspectorName("Message Padding")]
    [Tooltip("Padding for VerticalLayoutGroup on the spawned PhoneMessage root in Left / Right / Top / Bottom order.")]
    [PhoneLTRB]
    public Vector4 messageRootVerticalLayoutPadding;
    [InspectorName("Message Spacing")]
    [Tooltip("Spacing for VerticalLayoutGroup on the spawned PhoneMessage root.")]
    public float messageRootVerticalLayoutSpacing;
    [InspectorName("Message Child Alignment")]
    [Tooltip("Child Alignment for VerticalLayoutGroup on the spawned PhoneMessage root.")]
    public TextAnchor messageRootVerticalLayoutChildAlignment = TextAnchor.UpperLeft;
    [InspectorName("Message Reverse Arrangement")]
    [Tooltip("Reverses child order in VerticalLayoutGroup on the spawned PhoneMessage root.")]
    public bool messageRootVerticalLayoutReverseArrangement;
    [InspectorName("Message Control Child Width")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root controls child widths.")]
    public bool messageRootVerticalLayoutControlChildWidth;
    [InspectorName("Message Control Child Height")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root controls child heights.")]
    public bool messageRootVerticalLayoutControlChildHeight;
    [InspectorName("Message Use Child Scale Width")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root uses child width scale.")]
    public bool messageRootVerticalLayoutUseChildScaleWidth;
    [InspectorName("Message Use Child Scale Height")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root uses child height scale.")]
    public bool messageRootVerticalLayoutUseChildScaleHeight;
    [InspectorName("Message Force Expand Width")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root forces children to expand by width.")]
    public bool messageRootVerticalLayoutChildForceExpandWidth;
    [InspectorName("Message Force Expand Height")]
    [Tooltip("VerticalLayoutGroup on the spawned PhoneMessage root forces children to expand by height.")]
    public bool messageRootVerticalLayoutChildForceExpandHeight;

    [Header("Container VerticalLayoutGroup")]
    [InspectorName("Переопределить VerticalLayoutGroup контейнера")]
    [Tooltip("Если включено, PhoneDialogueUI на spawned-копии настраивает VerticalLayoutGroup объекта Container. Это помогает уместить текст внутри баббла и не зависеть от случайных padding в шаблоне.")]
    public bool overrideContainerVerticalLayout = true;
    [InspectorName("Padding контейнера")]
    [Tooltip("Padding VerticalLayoutGroup на Container в формате Left / Right / Top / Bottom, как в стандартном компоненте Unity. Обычно 0, потому что внутренние отступы текста задаются Padding баббла.")]
    [PhoneLTRB]
    public Vector4 containerPadding;
    [InspectorName("Spacing контейнера")]
    [Tooltip("Расстояние между дочерними элементами Container, например между именем/текстом/фото, если они находятся внутри Container.")]
    public float containerSpacing;
    [InspectorName("Выравнивание контейнера")]
    [Tooltip("Child Alignment для VerticalLayoutGroup на Container.")]
    public TextAnchor containerChildAlignment = TextAnchor.MiddleLeft;
    [InspectorName("Reverse Arrangement контейнера")]
    [Tooltip("Разворачивает порядок дочерних элементов Container, если нужен обратный порядок.")]
    public bool containerReverseArrangement;
    [InspectorName("Control Child Width контейнера")]
    [Tooltip("VerticalLayoutGroup будет управлять шириной дочерних элементов Container.")]
    public bool containerControlChildWidth = false;
    [InspectorName("Control Child Height контейнера")]
    [Tooltip("VerticalLayoutGroup будет управлять высотой дочерних элементов Container.")]
    public bool containerControlChildHeight = false;
    [InspectorName("Use Child Scale Width контейнера")]
    [Tooltip("Учитывать scale дочерних элементов по ширине при расчёте Container layout.")]
    public bool containerUseChildScaleWidth;
    [InspectorName("Use Child Scale Height контейнера")]
    [Tooltip("Учитывать scale дочерних элементов по высоте при расчёте Container layout.")]
    public bool containerUseChildScaleHeight;
    [InspectorName("Force Expand Width контейнера")]
    [Tooltip("Растягивать дочерние элементы Container по ширине.")]
    public bool containerChildForceExpandWidth;
    [InspectorName("Force Expand Height контейнера")]
    [Tooltip("Растягивать дочерние элементы Container по высоте.")]
    public bool containerChildForceExpandHeight;

    [Header("Container ContentSizeFitter")]
    [InspectorName("Переопределить ContentSizeFitter контейнера")]
    [Tooltip("Если включено, PhoneDialogueUI на spawned-копии настраивает ContentSizeFitter объекта Container.")]
    public bool overrideContainerContentSizeFitter = true;
    [InspectorName("Horizontal Fit контейнера")]
    [Tooltip("Horizontal Fit для ContentSizeFitter на Container.")]
    public ContentSizeFitter.FitMode containerHorizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    [InspectorName("Vertical Fit контейнера")]
    [Tooltip("Vertical Fit для ContentSizeFitter на Container.")]
    public ContentSizeFitter.FitMode containerVerticalFit = ContentSizeFitter.FitMode.PreferredSize;

    [Header("Background Stretch")]
    [InspectorName("Растягивать Background")]
    [Tooltip("Если включено, фон баббла растягивается по Container через stretch anchors. Это нужно, чтобы подложка покрывала весь текст после пересчёта размеров.")]
    public bool stretchBackground = true;
    [InspectorName("Stretch offsets Background")]
    [Tooltip("Отступы stretch-фона в формате Left / Right / Top / Bottom. Можно задавать отрицательные значения, чтобы фон выходил за Container.")]
    [PhoneLTRB(true)]
    public Vector4 backgroundStretchOffsets;
    [InspectorName("Background Ignore Layout")]
    [Tooltip("Включает Ignore Layout у LayoutElement на Background, чтобы фон не участвовал в VerticalLayoutGroup и не сдвигал текст.")]
    public bool backgroundIgnoreLayout = true;
    [InspectorName("Background в начало siblings")]
    [Tooltip("Перемещает Background первым дочерним элементом Container, чтобы текст и имя рисовались поверх фона.")]
    public bool backgroundSendToBack = true;

    public void Normalize()
    {
        if (_settingsVersion < CurrentSettingsVersion)
        {
            int previousSettingsVersion = _settingsVersion;
            if (previousSettingsVersion < 5)
            {
                messageRootVerticalLayoutPadding = rowPadding;
                messageRootVerticalLayoutSpacing = verticalSpacing;
                messageRootVerticalLayoutChildAlignment = rowAlignment;
                messageRootVerticalLayoutControlChildWidth = false;
                messageRootVerticalLayoutControlChildHeight = false;
                messageRootVerticalLayoutUseChildScaleWidth = false;
                messageRootVerticalLayoutUseChildScaleHeight = false;
                messageRootVerticalLayoutChildForceExpandWidth = false;
                messageRootVerticalLayoutChildForceExpandHeight = false;
            }

            if (previousSettingsVersion < 6)
                bodyTextScale = Vector3.one;

            if (previousSettingsVersion < 7)
            {
                senderNameAnchor = IsRightAligned(rowAlignment)
                    ? PhoneSenderNameAnchor.TopRight
                    : PhoneSenderNameAnchor.TopLeft;
                senderNameRelativeTo = PhoneSenderNameRelativeTo.BubbleContainer;
            }

            if (previousSettingsVersion < 4)
            {
                overrideContainerVerticalLayout = true;
                containerChildAlignment = TextAnchor.MiddleLeft;
                containerControlChildWidth = false;
                containerControlChildHeight = false;
                containerUseChildScaleWidth = false;
                containerUseChildScaleHeight = false;
                containerChildForceExpandWidth = false;
                containerChildForceExpandHeight = false;
                overrideContainerContentSizeFitter = true;
                containerHorizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                containerVerticalFit = ContentSizeFitter.FitMode.PreferredSize;
                stretchBackground = true;
                backgroundIgnoreLayout = true;
                backgroundSendToBack = true;
                showTimeText = true;
            }

            _settingsVersion = CurrentSettingsVersion;
        }

        senderNameFontSize = Mathf.Max(0f, senderNameFontSize);
        senderNameMinFontSize = Mathf.Max(0f, senderNameMinFontSize);
        senderNameMaxFontSize = Mathf.Max(0f, senderNameMaxFontSize);
        bodyFontSize = Mathf.Max(0f, bodyFontSize);
        bodyMinFontSize = Mathf.Max(0f, bodyMinFontSize);
        bodyMaxFontSize = Mathf.Max(0f, bodyMaxFontSize);
        if (bodyTextScale == Vector3.zero)
            bodyTextScale = Vector3.one;
        timeTextFontSize = Mathf.Max(0f, timeTextFontSize);
        timeTextMinFontSize = Mathf.Max(0f, timeTextMinFontSize);
        timeTextMaxFontSize = Mathf.Max(0f, timeTextMaxFontSize);
    }

    static bool IsRightAligned(TextAnchor alignment)
    {
        return alignment == TextAnchor.UpperRight ||
               alignment == TextAnchor.MiddleRight ||
               alignment == TextAnchor.LowerRight;
    }
}

[Serializable]
public sealed class PhoneMessageTemplateReferences
{
    [Header("Корень шаблона")]
    [InspectorName("Root шаблона")]
    [Tooltip("Отключённый объект-шаблон баббла, который копируется при спавне реального сообщения. Сам шаблон не должен быть видимым как сообщение.")]
    public GameObject root;

    [Header("Текст")]
    [InspectorName("Текст имени отправителя")]
    [Tooltip("TMP_Text имени отправителя внутри этого шаблона. Можно оставить выключенным через layout, но ссылка нужна для preview/debug.")]
    public TMP_Text senderNameText;
    [InspectorName("Контейнер баббла")]
    [Tooltip("RectTransform тела баббла. Именно ему задаются рассчитанные ширина/высота и Bubble Offset.")]
    public RectTransform container;
    [InspectorName("Фон баббла")]
    [Tooltip("Image декоративной подложки баббла. Используется для диагностики и автозаполнения.")]
    public Image backgroundImage;
    [InspectorName("Текст сообщения")]
    [Tooltip("TMP_Text основного текста сообщения внутри баббла.")]
    public TMP_Text messageText;
    [InspectorName("Текст времени")]
    [Tooltip("TMP_Text времени сообщения, например 15:25. Это отдельная ссылка, не MessageText и не SenderNameText.")]
    public TMP_Text timeText;

    [Header("Аватар и вложения")]
    [InspectorName("Круг аватара")]
    [Tooltip("RectTransform AvatarCircle. Обычно скрывается для SMS, чтобы не появлялась лишняя голова рядом с бабблом.")]
    public RectTransform avatarCircle;
    [InspectorName("Аватар")]
    [Tooltip("Image аватара внутри шаблона. Обычно скрывается флагом Hide Avatar.")]
    public Image avatarImage;
    [InspectorName("Image вложения")]
    [Tooltip("Image для фото-вложения. Заполняется только когда сообщение реально содержит назначенный Sprite-вложение.")]
    public Image attachmentImage;

    [Header("Layout")]
    [InspectorName("LayoutElement строки")]
    [Tooltip("LayoutElement на root шаблона строки. Если не назначен, будет добавлен на копию при спавне.")]
    public LayoutElement rootLayoutElement;
    [InspectorName("LayoutGroup строки")]
    [Tooltip("Horizontal/Vertical Layout Group на root шаблона строки. Через него применяются выравнивание, зазоры и Row Padding.")]
    public HorizontalOrVerticalLayoutGroup rootLayoutGroup;
    [InspectorName("LayoutGroup контейнера")]
    [Tooltip("Layout Group внутри контейнера баббла, если он есть в шаблоне.")]
    public HorizontalOrVerticalLayoutGroup containerLayoutGroup;
    [InspectorName("ContentSizeFitter контейнера")]
    [Tooltip("ContentSizeFitter контейнера баббла, если шаблон использует автосайзинг.")]
    public ContentSizeFitter containerSizeFitter;
    [InspectorName("Layout шаблона")]
    [Tooltip("Отдельные layout-настройки этого конкретного шаблона. Incoming, outgoing и photo могут отличаться полностью.")]
    public PhoneMessageTemplateLayoutSettings layout = new PhoneMessageTemplateLayoutSettings();

    public bool HasAnyReferences()
    {
        return root != null ||
               senderNameText != null ||
               container != null ||
               backgroundImage != null ||
               messageText != null ||
               timeText != null ||
               avatarCircle != null ||
               avatarImage != null ||
               attachmentImage != null ||
               rootLayoutElement != null ||
               rootLayoutGroup != null ||
               containerLayoutGroup != null ||
               containerSizeFitter != null;
    }

    public GameObject ResolveRootObject()
    {
        return root;
    }

    public void Ensure()
    {
        if (layout == null)
            layout = new PhoneMessageTemplateLayoutSettings();
        layout.Normalize();
    }

    public void CopyFrom(PhoneMessageTemplateReferences source, bool overwrite)
    {
        if (source == null)
            return;

        if (overwrite || root == null)
            root = source.root;
        if (overwrite || senderNameText == null)
            senderNameText = source.senderNameText;
        if (overwrite || container == null)
            container = source.container;
        if (overwrite || backgroundImage == null)
            backgroundImage = source.backgroundImage;
        if (overwrite || messageText == null)
            messageText = source.messageText;
        if (overwrite || timeText == null)
            timeText = source.timeText;
        if (overwrite || avatarCircle == null)
            avatarCircle = source.avatarCircle;
        if (overwrite || avatarImage == null)
            avatarImage = source.avatarImage;
        if (overwrite || attachmentImage == null)
            attachmentImage = source.attachmentImage;
        if (overwrite || rootLayoutElement == null)
            rootLayoutElement = source.rootLayoutElement;
        if (overwrite || rootLayoutGroup == null)
            rootLayoutGroup = source.rootLayoutGroup;
        if (overwrite || containerLayoutGroup == null)
            containerLayoutGroup = source.containerLayoutGroup;
        if (overwrite || containerSizeFitter == null)
            containerSizeFitter = source.containerSizeFitter;

        if (layout == null)
            layout = new PhoneMessageTemplateLayoutSettings();
        if (source.layout != null)
        {
            source.layout.Normalize();
            layout.showSenderName = overwrite || !layout.showSenderName ? source.layout.showSenderName : layout.showSenderName;
            layout.hideAvatar = overwrite ? source.layout.hideAvatar : layout.hideAvatar;
            layout.senderNameBottomSpacing = overwrite ? source.layout.senderNameBottomSpacing : layout.senderNameBottomSpacing;
            layout.senderNameOffset = overwrite || layout.senderNameOffset == Vector2.zero ? source.layout.senderNameOffset : layout.senderNameOffset;
            layout.senderNameAnchor = overwrite ? source.layout.senderNameAnchor : layout.senderNameAnchor;
            layout.senderNameRelativeTo = overwrite ? source.layout.senderNameRelativeTo : layout.senderNameRelativeTo;
            layout.senderNameSizeOffset = overwrite || layout.senderNameSizeOffset == Vector2.zero ? source.layout.senderNameSizeOffset : layout.senderNameSizeOffset;
            layout.senderNameMargin = overwrite || layout.senderNameMargin == Vector4.zero ? source.layout.senderNameMargin : layout.senderNameMargin;
            layout.senderNameFontSize = overwrite || layout.senderNameFontSize <= 0f ? source.layout.senderNameFontSize : layout.senderNameFontSize;
            layout.overrideSenderNameAutoSize = overwrite || !layout.overrideSenderNameAutoSize ? source.layout.overrideSenderNameAutoSize : layout.overrideSenderNameAutoSize;
            layout.senderNameAutoSize = overwrite || !layout.senderNameAutoSize ? source.layout.senderNameAutoSize : layout.senderNameAutoSize;
            layout.senderNameMinFontSize = overwrite || layout.senderNameMinFontSize <= 0f ? source.layout.senderNameMinFontSize : layout.senderNameMinFontSize;
            layout.senderNameMaxFontSize = overwrite || layout.senderNameMaxFontSize <= 0f ? source.layout.senderNameMaxFontSize : layout.senderNameMaxFontSize;
            layout.senderNameLineSpacing = overwrite || Mathf.Approximately(layout.senderNameLineSpacing, 0f) ? source.layout.senderNameLineSpacing : layout.senderNameLineSpacing;
            layout.showTimeText = overwrite ? source.layout.showTimeText : layout.showTimeText;
            layout.timeTextOffset = overwrite || layout.timeTextOffset == Vector2.zero ? source.layout.timeTextOffset : layout.timeTextOffset;
            layout.timeTextSizeOffset = overwrite || layout.timeTextSizeOffset == Vector2.zero ? source.layout.timeTextSizeOffset : layout.timeTextSizeOffset;
            layout.timeTextMargin = overwrite || layout.timeTextMargin == Vector4.zero ? source.layout.timeTextMargin : layout.timeTextMargin;
            layout.timeTextFontSize = overwrite || layout.timeTextFontSize <= 0f ? source.layout.timeTextFontSize : layout.timeTextFontSize;
            layout.overrideTimeTextAutoSize = overwrite || !layout.overrideTimeTextAutoSize ? source.layout.overrideTimeTextAutoSize : layout.overrideTimeTextAutoSize;
            layout.timeTextAutoSize = overwrite || !layout.timeTextAutoSize ? source.layout.timeTextAutoSize : layout.timeTextAutoSize;
            layout.timeTextMinFontSize = overwrite || layout.timeTextMinFontSize <= 0f ? source.layout.timeTextMinFontSize : layout.timeTextMinFontSize;
            layout.timeTextMaxFontSize = overwrite || layout.timeTextMaxFontSize <= 0f ? source.layout.timeTextMaxFontSize : layout.timeTextMaxFontSize;
            layout.timeTextLineSpacing = overwrite || Mathf.Approximately(layout.timeTextLineSpacing, 0f) ? source.layout.timeTextLineSpacing : layout.timeTextLineSpacing;
            layout.verticalSpacing = overwrite ? source.layout.verticalSpacing : layout.verticalSpacing;
            layout.horizontalOffset = overwrite ? source.layout.horizontalOffset : layout.horizontalOffset;
            layout.topPadding = overwrite ? source.layout.topPadding : layout.topPadding;
            layout.bottomPadding = overwrite ? source.layout.bottomPadding : layout.bottomPadding;
            layout.leftPadding = overwrite ? source.layout.leftPadding : layout.leftPadding;
            layout.rightPadding = overwrite ? source.layout.rightPadding : layout.rightPadding;
            layout.maxWidthPercent = overwrite ? source.layout.maxWidthPercent : layout.maxWidthPercent;
            layout.minWidth = overwrite ? source.layout.minWidth : layout.minWidth;
            layout.textOffsetInsideBubble = overwrite || layout.textOffsetInsideBubble == Vector2.zero ? source.layout.textOffsetInsideBubble : layout.textOffsetInsideBubble;
            layout.bodyTextScale = overwrite || layout.bodyTextScale == Vector3.zero ? source.layout.bodyTextScale : layout.bodyTextScale;
            layout.bodyFontSize = overwrite || layout.bodyFontSize <= 0f ? source.layout.bodyFontSize : layout.bodyFontSize;
            layout.overrideBodyAutoSize = overwrite || !layout.overrideBodyAutoSize ? source.layout.overrideBodyAutoSize : layout.overrideBodyAutoSize;
            layout.bodyAutoSize = overwrite || !layout.bodyAutoSize ? source.layout.bodyAutoSize : layout.bodyAutoSize;
            layout.bodyMinFontSize = overwrite || layout.bodyMinFontSize <= 0f ? source.layout.bodyMinFontSize : layout.bodyMinFontSize;
            layout.bodyMaxFontSize = overwrite || layout.bodyMaxFontSize <= 0f ? source.layout.bodyMaxFontSize : layout.bodyMaxFontSize;
            layout.bodyLineSpacing = overwrite || Mathf.Approximately(layout.bodyLineSpacing, 0f) ? source.layout.bodyLineSpacing : layout.bodyLineSpacing;
            layout.rowPadding = overwrite || layout.rowPadding == Vector4.zero ? source.layout.rowPadding : layout.rowPadding;
            layout.rowPositionOffset = overwrite || layout.rowPositionOffset == Vector2.zero ? source.layout.rowPositionOffset : layout.rowPositionOffset;
            layout.bubblePositionOffset = overwrite || layout.bubblePositionOffset == Vector2.zero ? source.layout.bubblePositionOffset : layout.bubblePositionOffset;
            layout.bubbleSizeOffset = overwrite || layout.bubbleSizeOffset == Vector2.zero ? source.layout.bubbleSizeOffset : layout.bubbleSizeOffset;
            layout.avatarOffset = overwrite || layout.avatarOffset == Vector2.zero ? source.layout.avatarOffset : layout.avatarOffset;
            layout.photoMessageSize = overwrite || layout.photoMessageSize == Vector2.zero ? source.layout.photoMessageSize : layout.photoMessageSize;
            layout.photoOffset = overwrite || layout.photoOffset == Vector2.zero ? source.layout.photoOffset : layout.photoOffset;
            layout.rowAlignment = overwrite ? source.layout.rowAlignment : layout.rowAlignment;
            layout.useMessageRootVerticalLayout = overwrite ? source.layout.useMessageRootVerticalLayout : layout.useMessageRootVerticalLayout;
            layout.messageRootVerticalLayoutPadding = overwrite || layout.messageRootVerticalLayoutPadding == Vector4.zero ? source.layout.messageRootVerticalLayoutPadding : layout.messageRootVerticalLayoutPadding;
            layout.messageRootVerticalLayoutSpacing = overwrite ? source.layout.messageRootVerticalLayoutSpacing : layout.messageRootVerticalLayoutSpacing;
            layout.messageRootVerticalLayoutChildAlignment = overwrite ? source.layout.messageRootVerticalLayoutChildAlignment : layout.messageRootVerticalLayoutChildAlignment;
            layout.messageRootVerticalLayoutReverseArrangement = overwrite ? source.layout.messageRootVerticalLayoutReverseArrangement : layout.messageRootVerticalLayoutReverseArrangement;
            layout.messageRootVerticalLayoutControlChildWidth = overwrite ? source.layout.messageRootVerticalLayoutControlChildWidth : layout.messageRootVerticalLayoutControlChildWidth;
            layout.messageRootVerticalLayoutControlChildHeight = overwrite ? source.layout.messageRootVerticalLayoutControlChildHeight : layout.messageRootVerticalLayoutControlChildHeight;
            layout.messageRootVerticalLayoutUseChildScaleWidth = overwrite ? source.layout.messageRootVerticalLayoutUseChildScaleWidth : layout.messageRootVerticalLayoutUseChildScaleWidth;
            layout.messageRootVerticalLayoutUseChildScaleHeight = overwrite ? source.layout.messageRootVerticalLayoutUseChildScaleHeight : layout.messageRootVerticalLayoutUseChildScaleHeight;
            layout.messageRootVerticalLayoutChildForceExpandWidth = overwrite ? source.layout.messageRootVerticalLayoutChildForceExpandWidth : layout.messageRootVerticalLayoutChildForceExpandWidth;
            layout.messageRootVerticalLayoutChildForceExpandHeight = overwrite ? source.layout.messageRootVerticalLayoutChildForceExpandHeight : layout.messageRootVerticalLayoutChildForceExpandHeight;
            layout.overrideContainerVerticalLayout = overwrite ? source.layout.overrideContainerVerticalLayout : layout.overrideContainerVerticalLayout;
            layout.containerPadding = overwrite || layout.containerPadding == Vector4.zero ? source.layout.containerPadding : layout.containerPadding;
            layout.containerSpacing = overwrite ? source.layout.containerSpacing : layout.containerSpacing;
            layout.containerChildAlignment = overwrite ? source.layout.containerChildAlignment : layout.containerChildAlignment;
            layout.containerReverseArrangement = overwrite ? source.layout.containerReverseArrangement : layout.containerReverseArrangement;
            layout.containerControlChildWidth = overwrite ? source.layout.containerControlChildWidth : layout.containerControlChildWidth;
            layout.containerControlChildHeight = overwrite ? source.layout.containerControlChildHeight : layout.containerControlChildHeight;
            layout.containerUseChildScaleWidth = overwrite ? source.layout.containerUseChildScaleWidth : layout.containerUseChildScaleWidth;
            layout.containerUseChildScaleHeight = overwrite ? source.layout.containerUseChildScaleHeight : layout.containerUseChildScaleHeight;
            layout.containerChildForceExpandWidth = overwrite ? source.layout.containerChildForceExpandWidth : layout.containerChildForceExpandWidth;
            layout.containerChildForceExpandHeight = overwrite ? source.layout.containerChildForceExpandHeight : layout.containerChildForceExpandHeight;
            layout.overrideContainerContentSizeFitter = overwrite ? source.layout.overrideContainerContentSizeFitter : layout.overrideContainerContentSizeFitter;
            layout.containerHorizontalFit = overwrite ? source.layout.containerHorizontalFit : layout.containerHorizontalFit;
            layout.containerVerticalFit = overwrite ? source.layout.containerVerticalFit : layout.containerVerticalFit;
            layout.stretchBackground = overwrite ? source.layout.stretchBackground : layout.stretchBackground;
            layout.backgroundStretchOffsets = overwrite || layout.backgroundStretchOffsets == Vector4.zero ? source.layout.backgroundStretchOffsets : layout.backgroundStretchOffsets;
            layout.backgroundIgnoreLayout = overwrite ? source.layout.backgroundIgnoreLayout : layout.backgroundIgnoreLayout;
            layout.backgroundSendToBack = overwrite ? source.layout.backgroundSendToBack : layout.backgroundSendToBack;
        }
        Ensure();
    }

    public void AutoFillFrom(GameObject template, bool overwrite = false)
    {
        if (template == null)
            return;

        if (overwrite || root == null)
            root = template;

        if (overwrite || rootLayoutElement == null)
            rootLayoutElement = template.GetComponent<LayoutElement>();
        if (overwrite || rootLayoutGroup == null)
            rootLayoutGroup = template.GetComponent<HorizontalOrVerticalLayoutGroup>();

        Transform templateTransform = template.transform;
        Transform containerTransform = FindTransformByName(templateTransform, "Container");
        if (containerTransform == null)
            containerTransform = FindTransformByName(templateTransform, "Bubble");
        if (containerTransform == null)
            containerTransform = FindTransformByName(templateTransform, "Background");
        if (containerTransform == null)
            containerTransform = templateTransform;

        if (overwrite || container == null)
            container = containerTransform.GetComponent<RectTransform>();
        if (overwrite || containerLayoutGroup == null)
            containerLayoutGroup = containerTransform.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (overwrite || containerSizeFitter == null)
            containerSizeFitter = containerTransform.GetComponent<ContentSizeFitter>();

        if (overwrite || senderNameText == null)
            senderNameText = FindNamedText(templateTransform, "ContactName", "Sender", "Name");
        if (overwrite || timeText == null)
            timeText = FindNamedText(templateTransform, "TimeText", "Timestamp", "Time", "Clock");
        if (overwrite || messageText == null)
            messageText = FindMessageText(templateTransform, senderNameText, timeText);
        if (overwrite || backgroundImage == null)
            backgroundImage = FindNamedImage(templateTransform, "Background", "Bubble");
        if ((overwrite || backgroundImage == null) && containerTransform != null)
            backgroundImage = containerTransform.GetComponentInChildren<Image>(true);
        if (overwrite || avatarCircle == null)
        {
            Transform avatarCircleTransform = FindTransformByName(templateTransform, "AvatarCircle");
            avatarCircle = avatarCircleTransform != null ? avatarCircleTransform.GetComponent<RectTransform>() : null;
        }
        if (overwrite || avatarImage == null)
            avatarImage = FindNamedImage(templateTransform, "Avatar");
        if (overwrite || attachmentImage == null)
            attachmentImage = FindNamedImage(templateTransform, "Attachment", "Photo");

        Ensure();
    }

    public TMP_Text FindSenderNameTextIn(GameObject instance)
    {
        return FindInstanceComponent(root, instance, senderNameText) ??
               FindNamedText(instance != null ? instance.transform : null, "ContactName", "Sender", "Name");
    }

    public TMP_Text FindMessageTextIn(GameObject instance)
    {
        TMP_Text text = FindInstanceComponent(root, instance, messageText);
        if (text != null)
            return text;

        Transform instanceTransform = instance != null ? instance.transform : null;
        TMP_Text named = FindNamedText(instanceTransform, "MessageText", "BodyText", "Message");
        TMP_Text sender = FindSenderNameTextIn(instance);
        TMP_Text time = FindTimeTextIn(instance);
        if (named != null && named != sender && named != time)
            return named;

        TMP_Text[] texts = instance != null ? instance.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i] != sender && texts[i] != time && !LooksLikeTimeText(texts[i]))
                return texts[i];
        }

        return null;
    }

    public TMP_Text FindTimeTextIn(GameObject instance)
    {
        return FindInstanceComponent(root, instance, timeText) ??
               FindNamedText(instance != null ? instance.transform : null, "TimeText", "Timestamp", "Time", "Clock");
    }

    public Image FindAttachmentImageIn(GameObject instance)
    {
        return FindInstanceComponent(root, instance, attachmentImage) ??
               FindNamedImage(instance != null ? instance.transform : null, "Attachment", "Photo");
    }

    public Image FindBackgroundImageIn(GameObject instance)
    {
        return FindInstanceComponent(root, instance, backgroundImage) ??
               FindNamedImage(instance != null ? instance.transform : null, "Background", "Bubble");
    }

    public RectTransform FindContainerIn(GameObject instance)
    {
        return FindInstanceComponent(root, instance, container);
    }

    public HorizontalOrVerticalLayoutGroup FindContainerLayoutGroupIn(GameObject instance)
    {
        HorizontalOrVerticalLayoutGroup group = FindInstanceComponent(root, instance, containerLayoutGroup);
        if (group != null)
            return group;

        RectTransform containerRect = FindContainerIn(instance);
        return containerRect != null ? containerRect.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
    }

    public ContentSizeFitter FindContainerSizeFitterIn(GameObject instance)
    {
        ContentSizeFitter fitter = FindInstanceComponent(root, instance, containerSizeFitter);
        if (fitter != null)
            return fitter;

        RectTransform containerRect = FindContainerIn(instance);
        return containerRect != null ? containerRect.GetComponent<ContentSizeFitter>() : null;
    }

    static T FindInstanceComponent<T>(GameObject templateRoot, GameObject instanceRoot, T templateComponent)
        where T : Component
    {
        if (templateRoot == null || instanceRoot == null || templateComponent == null)
            return null;

        string path = BuildPath(templateRoot.transform, templateComponent.transform);
        Transform target = string.IsNullOrEmpty(path) ? instanceRoot.transform : instanceRoot.transform.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    static string BuildPath(Transform rootTransform, Transform target)
    {
        if (rootTransform == null || target == null || rootTransform == target)
            return "";

        var parts = new Stack<string>();
        Transform cursor = target;
        while (cursor != null && cursor != rootTransform)
        {
            parts.Push(cursor.name);
            cursor = cursor.parent;
        }

        return cursor == rootTransform ? string.Join("/", parts.ToArray()) : "";
    }

    static TMP_Text FindMessageText(Transform rootTransform, TMP_Text sender, TMP_Text time)
    {
        TMP_Text named = FindNamedText(rootTransform, "MessageText", "BodyText", "Message");
        if (named != null && named != sender && named != time)
            return named;

        TMP_Text[] texts = rootTransform != null ? rootTransform.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i] != sender && texts[i] != time && !LooksLikeTimeText(texts[i]))
                return texts[i];
        }

        return null;
    }

    static bool LooksLikeTimeText(TMP_Text text)
    {
        if (text == null)
            return false;

        string objectName = text.gameObject != null ? text.gameObject.name : "";
        if (objectName.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("clock", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string value = text.text;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        if (value.Length < 3 || value.Length > 8)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsDigit(c) && c != ':' && c != '.')
                return false;
        }

        return value.IndexOf(':') >= 0 || value.IndexOf('.') >= 0;
    }

    static TMP_Text FindNamedText(Transform rootTransform, params string[] tokens)
    {
        Transform transform = FindTransformByName(rootTransform, tokens);
        return transform != null ? transform.GetComponent<TMP_Text>() : null;
    }

    static Image FindNamedImage(Transform rootTransform, params string[] tokens)
    {
        Transform transform = FindTransformByName(rootTransform, tokens);
        return transform != null ? transform.GetComponent<Image>() : null;
    }

    static Transform FindTransformByName(Transform rootTransform, params string[] tokens)
    {
        if (rootTransform == null || tokens == null || tokens.Length == 0)
            return null;

        Transform[] children = rootTransform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
                continue;

            string name = child.name.ToLowerInvariant();
            for (int j = 0; j < tokens.Length; j++)
            {
                string token = tokens[j];
                if (!string.IsNullOrWhiteSpace(token) && name.Contains(token.Trim().ToLowerInvariant()))
                    return child;
            }
        }

        return null;
    }
}

[Serializable]
public sealed class PhoneDialogueUIReferences
{
    [Header("Корни телефона")]
    [InspectorName("PhoneDialogueUI")]
    [Tooltip("Компонент runtime-экрана телефона в текущей сцене. StoryUserInterface передаёт ему все ссылки и layout.")]
    public PhoneDialogueUI phoneDialogueUI;
    [InspectorName("Root preview")]
    [Tooltip("Корневой GameObject экрана телефона, который включается при preview/runtime показе.")]
    public GameObject previewRoot;
    [InspectorName("Root экрана телефона")]
    [Tooltip("RectTransform визуального экрана телефона. Используется как верхний контейнер для safe area и layout.")]
    public RectTransform phoneScreenRoot;
    [InspectorName("Safe Area")]
    [Tooltip("RectTransform безопасной зоны внутри телефона. К нему применяются Safe Area Padding.")]
    public RectTransform safeArea;
    [InspectorName("Header")]
    [Tooltip("Верхняя панель телефона с кнопкой назад, именем контакта и аватаром.")]
    public RectTransform header;
    [InspectorName("Кнопка назад")]
    [Tooltip("Button стрелки назад в телефоне. Может закрывать phone preview/runtime диалог.")]
    public Button backButton;

    [Header("Контакт")]
    [InspectorName("Имя контакта в header")]
    [Tooltip("TMP_Text имени контакта в верхней панели телефона.")]
    public TMP_Text headerContactNameText;
    [InspectorName("Аватар контакта в header")]
    [Tooltip("Image аватара контакта в верхней панели. Это не аватар в бабблах.")]
    public Image headerContactAvatarImage;
    [InspectorName("Аватар по умолчанию")]
    [Tooltip("Sprite аватара контакта, если PhoneDialogueNode не передал свой аватар.")]
    public Sprite defaultAvatarSprite;

    [Header("Сообщения")]
    [InspectorName("ScrollRect сообщений")]
    [Tooltip("ScrollRect списка сообщений телефона.")]
    public ScrollRect messagesScrollRect;
    [InspectorName("Viewport сообщений")]
    [Tooltip("Viewport ScrollRect. По его ширине рассчитывается максимальная ширина бабблов.")]
    public RectTransform messageViewport;
    [InspectorName("Content сообщений")]
    [Tooltip("RectTransform Content внутри ScrollRect. Сюда спавнятся реальные копии бабблов.")]
    public RectTransform messageContent;
    [InspectorName("Incoming шаблон")]
    [Tooltip("Шаблон входящего сообщения. В текущей логике quick preview это сторона игрока/NAME/{PlayerName}, если контакт считается outgoing.")]
    public PhoneMessageTemplateReferences incomingTemplate = new PhoneMessageTemplateReferences();
    [InspectorName("Outgoing шаблон")]
    [Tooltip("Шаблон исходящего сообщения. В текущей логике quick preview это сообщения контакта, например Мэг, с синей подложкой.")]
    public PhoneMessageTemplateReferences outgoingTemplate = new PhoneMessageTemplateReferences();
    [InspectorName("Photo шаблон")]
    [Tooltip("Отдельный шаблон сообщения с фото-вложением. Используется только когда у сообщения реально есть Sprite attachment.")]
    public PhoneMessageTemplateReferences photoMessageTemplate = new PhoneMessageTemplateReferences();
    [InspectorName("Фото по умолчанию")]
    [Tooltip("Sprite фото-вложения по умолчанию. Quick preview не подставляет его автоматически, пока не включён флаг Use Default Photo Sprite In Quick Preview.")]
    public Sprite defaultPhotoSprite;

    [Header("Поле ввода")]
    [InspectorName("Панель ввода")]
    [Tooltip("Нижняя панель ввода телефона. Runtime телефон показывает её как декоративную часть экрана.")]
    public RectTransform inputBar;
    [InspectorName("Placeholder ввода")]
    [Tooltip("TMP_Text placeholder в поле ввода, например Message...")]
    public TMP_Text inputPlaceholderText;
    [InspectorName("Текст ввода")]
    [Tooltip("TMP_Text активного текста ввода, если он есть в макете телефона.")]
    public TMP_Text inputText;

    [Header("Legacy: старые ссылки для миграции")]
    [FormerlySerializedAs("contactNameText")]
    [SerializeField, HideInInspector] private TMP_Text legacyContactNameText;
    [FormerlySerializedAs("contactAvatarImage")]
    [SerializeField, HideInInspector] private Image legacyContactAvatarImage;
    [FormerlySerializedAs("messageScrollRect")]
    [SerializeField, HideInInspector] private ScrollRect legacyMessageScrollRect;
    [FormerlySerializedAs("incomingBubbleTemplate")]
    [SerializeField, HideInInspector] private GameObject legacyIncomingBubbleTemplate;
    [FormerlySerializedAs("outgoingBubbleTemplate")]
    [SerializeField, HideInInspector] private GameObject legacyOutgoingBubbleTemplate;
    [FormerlySerializedAs("photoMessageTemplate")]
    [SerializeField, HideInInspector] private GameObject legacyPhotoMessageTemplate;
    [FormerlySerializedAs("bubbleRect")]
    [SerializeField, HideInInspector] private RectTransform legacyBubbleRect;
    [FormerlySerializedAs("bubbleBackgroundImage")]
    [SerializeField, HideInInspector] private Image legacyBubbleBackgroundImage;
    [FormerlySerializedAs("bubbleText")]
    [SerializeField, HideInInspector] private TMP_Text legacyBubbleText;
    [FormerlySerializedAs("bubbleAvatarImage")]
    [SerializeField, HideInInspector] private Image legacyBubbleAvatarImage;
    [FormerlySerializedAs("bubblePhotoImage")]
    [SerializeField, HideInInspector] private Image legacyBubblePhotoImage;

    public void Ensure()
    {
        if (incomingTemplate == null)
            incomingTemplate = new PhoneMessageTemplateReferences();
        if (outgoingTemplate == null)
            outgoingTemplate = new PhoneMessageTemplateReferences();
        if (photoMessageTemplate == null)
            photoMessageTemplate = new PhoneMessageTemplateReferences();

        MigrateLegacyFields(null, false);
        incomingTemplate.Ensure();
        outgoingTemplate.Ensure();
        photoMessageTemplate.Ensure();
    }

    public bool HasAnyReferences()
    {
        Ensure();
        return phoneDialogueUI != null ||
               previewRoot != null ||
               phoneScreenRoot != null ||
               safeArea != null ||
               header != null ||
               backButton != null ||
               headerContactNameText != null ||
               headerContactAvatarImage != null ||
               defaultAvatarSprite != null ||
               messagesScrollRect != null ||
               messageViewport != null ||
               messageContent != null ||
               inputBar != null ||
               inputPlaceholderText != null ||
               inputText != null ||
               defaultPhotoSprite != null ||
               incomingTemplate.HasAnyReferences() ||
               outgoingTemplate.HasAnyReferences() ||
               photoMessageTemplate.HasAnyReferences() ||
               HasLegacyReferences();
    }

    public bool HasLegacyReferences()
    {
        return legacyContactNameText != null ||
               legacyContactAvatarImage != null ||
               legacyMessageScrollRect != null ||
               legacyIncomingBubbleTemplate != null ||
               legacyOutgoingBubbleTemplate != null ||
               legacyPhotoMessageTemplate != null ||
               legacyBubbleRect != null ||
               legacyBubbleBackgroundImage != null ||
               legacyBubbleText != null ||
               legacyBubbleAvatarImage != null ||
               legacyBubblePhotoImage != null;
    }

    public PhoneDialogueUI ResolvePhoneDialogueUI(PhoneDialogueUI fallback = null)
    {
        return phoneDialogueUI != null ? phoneDialogueUI : fallback;
    }

    public GameObject ResolveRoot(PhoneDialogueUI owner)
    {
        Ensure();
        return previewRoot != null ? previewRoot : owner != null ? owner.panel : null;
    }

    public TMP_Text ResolveContactNameText(PhoneDialogueUI owner)
    {
        Ensure();
        return headerContactNameText != null ? headerContactNameText : owner != null ? owner.contactNameText : null;
    }

    public Image ResolveContactAvatarImage(PhoneDialogueUI owner)
    {
        Ensure();
        return headerContactAvatarImage != null ? headerContactAvatarImage : owner != null ? owner.contactAvatarImage : null;
    }

    public RectTransform ResolveMessageContent(PhoneDialogueUI owner)
    {
        Ensure();
        if (messageContent != null)
            return messageContent;
        return owner != null ? owner.messagesContainer : null;
    }

    public ScrollRect ResolveScrollRect(PhoneDialogueUI owner)
    {
        Ensure();
        if (messagesScrollRect != null)
            return messagesScrollRect;
        RectTransform content = ResolveMessageContent(owner);
        return content != null ? content.GetComponentInParent<ScrollRect>(true) : null;
    }

    public RectTransform ResolveViewport(PhoneDialogueUI owner)
    {
        Ensure();
        if (messageViewport != null)
            return messageViewport;
        ScrollRect scrollRect = ResolveScrollRect(owner);
        return scrollRect != null ? scrollRect.viewport : null;
    }

    public GameObject ResolveIncomingBubbleTemplate(PhoneDialogueUI owner)
    {
        Ensure();
        GameObject template = incomingTemplate.ResolveRootObject();
        return template != null ? template : owner != null ? owner.incomingBubblePrefab : null;
    }

    public GameObject ResolveOutgoingBubbleTemplate(PhoneDialogueUI owner)
    {
        Ensure();
        GameObject template = outgoingTemplate.ResolveRootObject();
        return template != null ? template : owner != null ? owner.outgoingBubblePrefab : null;
    }

    public GameObject ResolvePhotoBubbleTemplate(PhoneDialogueUI owner)
    {
        Ensure();
        return photoMessageTemplate.ResolveRootObject();
    }

    public PhoneMessageTemplateReferences ResolveTemplateReferences(PhoneMessageSide side, bool hasAttachment)
    {
        Ensure();
        if (hasAttachment && photoMessageTemplate.ResolveRootObject() != null)
            return photoMessageTemplate;
        return side == PhoneMessageSide.Incoming ? incomingTemplate : outgoingTemplate;
    }

    public void CopyFrom(PhoneDialogueUIReferences source, PhoneDialogueUI ownerFallback, bool overwrite)
    {
        if (source == null)
            return;

        source.Ensure();
        if (overwrite || phoneDialogueUI == null)
            phoneDialogueUI = source.phoneDialogueUI != null ? source.phoneDialogueUI : ownerFallback;
        if (overwrite || previewRoot == null)
            previewRoot = source.previewRoot;
        if (overwrite || phoneScreenRoot == null)
            phoneScreenRoot = source.phoneScreenRoot;
        if (overwrite || safeArea == null)
            safeArea = source.safeArea;
        if (overwrite || header == null)
            header = source.header;
        if (overwrite || backButton == null)
            backButton = source.backButton;
        if (overwrite || headerContactNameText == null)
            headerContactNameText = source.headerContactNameText;
        if (overwrite || headerContactAvatarImage == null)
            headerContactAvatarImage = source.headerContactAvatarImage;
        if (overwrite || defaultAvatarSprite == null)
            defaultAvatarSprite = source.defaultAvatarSprite;
        if (overwrite || messagesScrollRect == null)
            messagesScrollRect = source.messagesScrollRect;
        if (overwrite || messageViewport == null)
            messageViewport = source.messageViewport;
        if (overwrite || messageContent == null)
            messageContent = source.messageContent;
        if (overwrite || inputBar == null)
            inputBar = source.inputBar;
        if (overwrite || inputPlaceholderText == null)
            inputPlaceholderText = source.inputPlaceholderText;
        if (overwrite || inputText == null)
            inputText = source.inputText;
        if (overwrite || defaultPhotoSprite == null)
            defaultPhotoSprite = source.defaultPhotoSprite;

        incomingTemplate.CopyFrom(source.incomingTemplate, overwrite);
        outgoingTemplate.CopyFrom(source.outgoingTemplate, overwrite);
        photoMessageTemplate.CopyFrom(source.photoMessageTemplate, overwrite);
        MigrateLegacyFields(ownerFallback, overwrite);
        Ensure();
    }

    public void AutoFillFrom(PhoneDialogueUI owner, bool overwrite = false)
    {
        if (owner == null)
            return;

        Ensure();
        MigrateLegacyFields(owner, overwrite);

        if (overwrite || phoneDialogueUI == null)
            phoneDialogueUI = owner;
        if (overwrite || previewRoot == null)
            previewRoot = owner.panel != null ? owner.panel : owner.gameObject;
        if (overwrite || header == null)
            header = FindRect(owner.transform, "header");
        if (overwrite || headerContactNameText == null)
            headerContactNameText = FindHeaderContactNameText(owner, header);
        if (overwrite || headerContactAvatarImage == null)
            headerContactAvatarImage = owner.contactAvatarImage;
        if (overwrite || messageContent == null)
            messageContent = owner.messagesContainer;

        GameObject incomingRoot = ResolveIncomingBubbleTemplate(owner);
        GameObject outgoingRoot = ResolveOutgoingBubbleTemplate(owner);
        if (incomingRoot != null)
            incomingTemplate.AutoFillFrom(incomingRoot, overwrite);
        if (outgoingRoot != null)
            outgoingTemplate.AutoFillFrom(outgoingRoot, overwrite);
        if (photoMessageTemplate.ResolveRootObject() != null)
            photoMessageTemplate.AutoFillFrom(photoMessageTemplate.ResolveRootObject(), overwrite);

        ScrollRect scroll = ResolveScrollRect(owner);
        if (overwrite || messagesScrollRect == null)
            messagesScrollRect = scroll;
        if (messageViewport == null && scroll != null)
            messageViewport = scroll.viewport;
        if (messageContent == null && scroll != null)
            messageContent = scroll.content;

        if (phoneScreenRoot == null && previewRoot != null)
            phoneScreenRoot = previewRoot.GetComponent<RectTransform>();
        if (safeArea == null)
            safeArea = FindRect(owner.transform, "safe");
        if (inputBar == null)
            inputBar = FindRect(owner.transform, "input");

        if (backButton == null)
            backButton = FindComponentByName<Button>(owner.transform, "back");
        if (inputPlaceholderText == null)
            inputPlaceholderText = FindTextByName(owner.transform, "placeholder");
        if (inputText == null)
            inputText = FindTextByName(owner.transform, "message");
    }

    public void MigrateLegacyFields(PhoneDialogueUI owner, bool overwrite)
    {
        if (overwrite || headerContactNameText == null)
            headerContactNameText = legacyContactNameText != null ? legacyContactNameText : headerContactNameText;
        if (overwrite || headerContactAvatarImage == null)
            headerContactAvatarImage = legacyContactAvatarImage != null ? legacyContactAvatarImage : headerContactAvatarImage;
        if (overwrite || messagesScrollRect == null)
            messagesScrollRect = legacyMessageScrollRect != null ? legacyMessageScrollRect : messagesScrollRect;

        if (incomingTemplate == null)
            incomingTemplate = new PhoneMessageTemplateReferences();
        if (outgoingTemplate == null)
            outgoingTemplate = new PhoneMessageTemplateReferences();
        if (photoMessageTemplate == null)
            photoMessageTemplate = new PhoneMessageTemplateReferences();

        GameObject incomingRoot = legacyIncomingBubbleTemplate != null ? legacyIncomingBubbleTemplate : owner != null ? owner.incomingBubblePrefab : null;
        GameObject outgoingRoot = legacyOutgoingBubbleTemplate != null ? legacyOutgoingBubbleTemplate : owner != null ? owner.outgoingBubblePrefab : null;
        if (incomingRoot != null && (overwrite || incomingTemplate.ResolveRootObject() == null))
            incomingTemplate.AutoFillFrom(incomingRoot, overwrite);
        if (outgoingRoot != null && (overwrite || outgoingTemplate.ResolveRootObject() == null))
            outgoingTemplate.AutoFillFrom(outgoingRoot, overwrite);
        if (legacyPhotoMessageTemplate != null && (overwrite || photoMessageTemplate.ResolveRootObject() == null))
            photoMessageTemplate.AutoFillFrom(legacyPhotoMessageTemplate, overwrite);

        if (legacyBubbleRect != null && (overwrite || incomingTemplate.container == null))
            incomingTemplate.container = legacyBubbleRect;
        if (legacyBubbleBackgroundImage != null && (overwrite || incomingTemplate.backgroundImage == null))
            incomingTemplate.backgroundImage = legacyBubbleBackgroundImage;
        if (legacyBubbleText != null && (overwrite || incomingTemplate.messageText == null))
            incomingTemplate.messageText = legacyBubbleText;
        if (legacyBubbleAvatarImage != null && (overwrite || incomingTemplate.avatarImage == null))
            incomingTemplate.avatarImage = legacyBubbleAvatarImage;
        if (legacyBubblePhotoImage != null && (overwrite || incomingTemplate.attachmentImage == null))
            incomingTemplate.attachmentImage = legacyBubblePhotoImage;
    }

    static RectTransform FindRect(Transform root, string token)
    {
        Transform found = FindTransformByName(root, token);
        return found != null ? found.GetComponent<RectTransform>() : null;
    }

    static TMP_Text FindHeaderContactNameText(PhoneDialogueUI owner, RectTransform header)
    {
        if (owner == null)
            return null;

        if (header == null)
            header = FindRect(owner.transform, "header");

        TMP_Text legacyText = owner.contactNameText;
        if (legacyText != null && (header == null || IsTransformChildOf(legacyText.transform, header)))
            return legacyText;

        if (header != null)
        {
            TMP_Text directText = header.GetComponent<TMP_Text>();
            if (directText != null)
                return directText;

            TMP_Text namedText = FindNamedHeaderText(header.GetComponentsInChildren<TMP_Text>(true));
            if (namedText != null)
                return namedText;
        }

        return legacyText != null ? legacyText : FindTextByName(owner.transform, "ContactName");
    }

    static TMP_Text FindNamedHeaderText(TMP_Text[] texts)
    {
        if (texts == null || texts.Length == 0)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null && candidate.name.IndexOf("contact", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null && candidate.name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        return texts[0];
    }

    static bool IsTransformChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }

        return false;
    }

    static TMP_Text FindTextByName(Transform root, string token)
    {
        Transform found = FindTransformByName(root, token);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    static T FindComponentByName<T>(Transform root, string token) where T : Component
    {
        Transform found = FindTransformByName(root, token);
        return found != null ? found.GetComponent<T>() : null;
    }

    static Transform FindTransformByName(Transform root, string token)
    {
        if (root == null || string.IsNullOrWhiteSpace(token))
            return null;

        string prepared = token.Trim().ToLowerInvariant();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name.ToLowerInvariant().Contains(prepared))
                return child;
        }

        return null;
    }
}

[Serializable]
public sealed class PhoneDialogueLayoutSettings
{
    const int CurrentLayoutSettingsVersion = 9;

    [SerializeField, HideInInspector] int _layoutSettingsVersion;

    [Header("Master")]
    [InspectorName("Disable All Phone Layout Settings")]
    [Tooltip("Disables every layout override in this phone category at runtime/preview without deleting saved values: offsets, padding, sizes, text margins/fonts, layout groups, background stretch, avatar hiding and header/message layout overrides.")]
    public bool disableAllPhoneLayoutSettings;

    [Header("Отступы safe area")]
    [InspectorName("Safe Area Padding")]
    [Tooltip("Отступы безопасной зоны телефона в формате Left / Right / Top / Bottom.")]
    [PhoneLTRB]
    public Vector4 safeAreaPadding = new Vector4(20f, 20f, 24f, 24f);
    [InspectorName("Padding Content сообщений")]
    [Tooltip("Отступы внутри Content списка сообщений в формате Left / Right / Top / Bottom. Это главный способ отодвинуть все бабблы от краёв экрана телефона.")]
    [PhoneLTRB]
    public Vector4 messageContentPadding = new Vector4(8f, 8f, 8f, 8f);

    [Header("Header Contact")]
    [InspectorName("Show Header Contact")]
    [Tooltip("Shows the contact name TMP_Text in the phone header. Runtime text comes from PhoneDialogueNode.contactName; quick previews can use PhonePreviewSettings.")]
    public bool showHeaderContactName = true;
    [InspectorName("Header Contact Offset")]
    [Tooltip("Extra X/Y offset for the contact name in the top phone header.")]
    public Vector2 headerContactNameOffset;
    [InspectorName("Header Contact Size Offset")]
    [Tooltip("Adds X/Y to the RectTransform size of the top contact name text.")]
    public Vector2 headerContactNameSizeOffset;
    [InspectorName("Header Contact Margin")]
    [Tooltip("TMP margin for the top contact name text in Left / Right / Top / Bottom order. Zero keeps the prefab margin.")]
    [PhoneLTRB]
    public Vector4 headerContactNameMargin;
    [InspectorName("Header Contact Font Size")]
    [Tooltip("TMP font size for the top contact name. 0 keeps the prefab font size.")]
    [Min(0f)] public float headerContactNameFontSize;
    [InspectorName("Override Header Auto Size")]
    [Tooltip("When enabled, this layout explicitly controls TMP Auto Size for the top contact name.")]
    public bool overrideHeaderContactNameAutoSize;
    [InspectorName("Header Contact Auto Size")]
    [Tooltip("Auto Size value applied to the top contact name when Override Header Auto Size is enabled.")]
    public bool headerContactNameAutoSize;
    [InspectorName("Header Contact Min Font")]
    [Tooltip("Minimum TMP auto-size font for the top contact name. 0 keeps the prefab value.")]
    [Min(0f)] public float headerContactNameMinFontSize;
    [InspectorName("Header Contact Max Font")]
    [Tooltip("Maximum TMP auto-size font for the top contact name. 0 keeps the prefab value.")]
    [Min(0f)] public float headerContactNameMaxFontSize;
    [InspectorName("Header Contact Line Spacing")]
    [Tooltip("TMP line spacing for the top contact name. 0 keeps the prefab value.")]
    public float headerContactNameLineSpacing;

    [Header("Бабблы")]
    [InspectorName("Вертикальный зазор сообщений")]
    [Tooltip("Глобальный вертикальный зазор между строками сообщений в Content.")]
    public float messageVerticalSpacing = 10f;
    [InspectorName("Горизонтальный offset баббла")]
    [Tooltip("Глобальный X-сдвиг баббла. Положительное значение двигает вправо, отрицательное — влево. Incoming/Outgoing/Photo могут переопределить это значение.")]
    public float bubbleHorizontalOffset = 18f;
    [InspectorName("Incoming Horizontal Offset")]
    [Tooltip("Отдельный horizontal offset для входящих сообщений. 0 означает: использовать значение из Incoming Layout, а затем общий Bubble Horizontal Offset.")]
    public float incomingBubbleHorizontalOffset;
    [InspectorName("Outgoing Horizontal Offset")]
    [Tooltip("Отдельный horizontal offset для исходящих сообщений. 0 означает: использовать значение из Outgoing Layout, а затем общий Bubble Horizontal Offset.")]
    public float outgoingBubbleHorizontalOffset;
    [InspectorName("Photo Horizontal Offset")]
    [Tooltip("Отдельный horizontal offset для сообщений с фото-вложением. 0 означает: использовать значение из Photo Layout, а затем общий Bubble Horizontal Offset.")]
    public float photoBubbleHorizontalOffset;
    [InspectorName("Фото следует стороне")]
    [Tooltip("Если включено, фото-сообщение берёт позицию строки, alignment, row/bubble offsets и horizontal offset от Incoming/Outgoing Layout по стороне сообщения. Photo Layout при этом продолжает управлять размером, текстом, фоном и вложением.")]
    public bool photoUsesMessageSidePosition = true;
    [InspectorName("Padding баббла сверху")]
    [Tooltip("Глобальный верхний внутренний отступ текста в баббле.")]
    public float bubbleTopPadding = 10f;
    [InspectorName("Padding баббла снизу")]
    [Tooltip("Глобальный нижний внутренний отступ текста в баббле.")]
    public float bubbleBottomPadding = 10f;
    [InspectorName("Padding баббла слева")]
    [Tooltip("Глобальный левый внутренний отступ текста в баббле.")]
    public float bubbleLeftPadding = 14f;
    [InspectorName("Padding баббла справа")]
    [Tooltip("Глобальный правый внутренний отступ текста в баббле.")]
    public float bubbleRightPadding = 14f;
    [InspectorName("Макс. ширина баббла")]
    [Tooltip("Глобальная максимальная ширина баббла как доля ширины viewport телефона.")]
    public float maxBubbleWidthPercent = 0.74f;
    [InspectorName("Мин. ширина баббла")]
    [Tooltip("Глобальная минимальная ширина баббла в пикселях.")]
    public float minBubbleWidth = 64f;
    [InspectorName("Сдвиг текста")]
    [Tooltip("Глобальный сдвиг текста внутри баббла. Шаблоны могут иметь свой отдельный Text Offset Inside Bubble.")]
    public Vector2 textOffsetInsideBubble;
    [InspectorName("Размер фото")]
    [Tooltip("Глобальный размер фото-вложения. Шаблон photo может переопределить его.")]
    public Vector2 photoMessageSize = new Vector2(220f, 160f);
    [InspectorName("Скроллить вниз")]
    [Tooltip("После добавления сообщения автоматически прокручивает ScrollRect к последнему сообщению.")]
    public bool scrollToBottom = true;
    [InspectorName("Показывать имена в бабблах")]
    [Tooltip("Глобально включает имена отправителей во всех бабблах. Если нужна точная настройка, можно выключить это поле и включать Показывать имя отдельно в Incoming/Outgoing/Photo Layout.")]
    public bool showSenderNamesInBubbles = false;
    [InspectorName("Скрывать аватары в бабблах")]
    [Tooltip("Глобально отключает Avatar/AvatarCircle во всех бабблах, чтобы не появлялась лишняя голова.")]
    public bool hideAvatarsInBubbles = true;
    [InspectorName("Принудительный Vertical Layout")]
    [Tooltip("Настраивает VerticalLayoutGroup на Content сообщений перед preview/runtime показом.")]
    public bool enforceContentVerticalLayout = true;
    [InspectorName("Не трогать Content")]
    [Tooltip("Если включено, PhoneDialogueUI не перезаписывает RectTransform, VerticalLayoutGroup и ContentSizeFitter объекта Content, не делает принудительный rebuild и возвращает ручную позицию/размер после preview/runtime-спавна. Отключи только если хочешь, чтобы код сам управлял Content-layout.")]
    public bool preserveMessageContentLayout = true;
    [InspectorName("Выключать ContentSizeFitter")]
    [Tooltip("Аварийный режим. Если включено вместе с 'Не трогать Content', PhoneDialogueUI отключает ContentSizeFitter на объекте Content. Обычно это должно быть выключено, иначе ScrollRect content перестаёт нормально расти по высоте.")]
    public bool disableMessageContentSizeFitterWhenPreserved = false;
    [InspectorName("Строки на всю ширину")]
    [Tooltip("Делает каждую строку сообщения шириной viewport, чтобы выравнивание left/right работало предсказуемо.")]
    public bool forceFullWidthMessageRows = true;

    [Header("Animation")]
    [InspectorName("Message Appear Animation")]
    [Tooltip("Runtime animation used when phone messages appear. Preview stays static so layout can be edited precisely.")]
    public PhoneMessageAppearAnimation messageAppearAnimation = PhoneMessageAppearAnimation.Fade;
    [InspectorName("Message Appear Duration")]
    [Tooltip("Duration of the phone message appear animation in seconds.")]
    [Min(0f)] public float messageAppearDuration = 0.4f;
    [InspectorName("Message Post Appear Delay")]
    [Tooltip("Extra pause after a message appear animation before the next phone message starts typing.")]
    [Min(0f)] public float messagePostAppearDelay = 0.05f;
    [InspectorName("Message Slide Offset")]
    [Tooltip("Slide start offset in pixels. X is mirrored by message side: incoming starts from -X, outgoing starts from +X.")]
    public Vector2 messageAppearSlideOffset = new Vector2(22f, 0f);
    [InspectorName("Message Scale From")]
    [Tooltip("Start scale for scale-based message animations. 1 keeps the original size.")]
    [Min(0.01f)] public float messageAppearScaleFrom = 0.98f;
    [InspectorName("Message Appear Ease")]
    [Tooltip("DOTween ease used by phone message appear animations.")]
    public Ease messageAppearEase = Ease.OutCubic;

    [Header("Incoming")]
    [InspectorName("Layout incoming")]
    [Tooltip("Отдельные настройки входящих сообщений.")]
    public PhoneMessageTemplateLayoutSettings incomingLayout = new PhoneMessageTemplateLayoutSettings
    {
        rowAlignment = TextAnchor.MiddleLeft
    };

    [Header("Outgoing")]
    [InspectorName("Layout outgoing")]
    [Tooltip("Отдельные настройки исходящих сообщений.")]
    public PhoneMessageTemplateLayoutSettings outgoingLayout = new PhoneMessageTemplateLayoutSettings
    {
        rowAlignment = TextAnchor.MiddleRight
    };

    [Header("Photo")]
    [InspectorName("Layout photo")]
    [Tooltip("Отдельные настройки сообщений с фото-вложением.")]
    public PhoneMessageTemplateLayoutSettings photoLayout = new PhoneMessageTemplateLayoutSettings
    {
        rowAlignment = TextAnchor.MiddleLeft
    };

    public void Normalize()
    {
        bool resetLegacyTemplateHorizontalOffsets = _layoutSettingsVersion < 5;
        if (_layoutSettingsVersion < CurrentLayoutSettingsVersion)
        {
            if (_layoutSettingsVersion < 1)
                photoUsesMessageSidePosition = true;
            if (_layoutSettingsVersion < 2)
                preserveMessageContentLayout = true;
            if (_layoutSettingsVersion < 3)
                disableMessageContentSizeFitterWhenPreserved = true;
            if (_layoutSettingsVersion < 4)
                disableMessageContentSizeFitterWhenPreserved = false;
            if (_layoutSettingsVersion < 6)
                showHeaderContactName = true;
            if (_layoutSettingsVersion < 8)
            {
                if (messageAppearAnimation == PhoneMessageAppearAnimation.None)
                    messageAppearAnimation = PhoneMessageAppearAnimation.Fade;
                if (messageAppearDuration <= 0f)
                    messageAppearDuration = 0.4f;
                if (messagePostAppearDelay <= 0f)
                    messagePostAppearDelay = 0.05f;
                if (messageAppearSlideOffset == Vector2.zero)
                    messageAppearSlideOffset = new Vector2(22f, 0f);
                if (messageAppearScaleFrom <= 0f)
                    messageAppearScaleFrom = 0.98f;
                if (messageAppearEase == Ease.Unset)
                    messageAppearEase = Ease.OutCubic;
            }
            if (_layoutSettingsVersion < 9 &&
                messageAppearAnimation == PhoneMessageAppearAnimation.FadeAndSlide &&
                messageAppearSlideOffset == new Vector2(22f, 0f))
            {
                messageAppearAnimation = PhoneMessageAppearAnimation.Fade;
            }
            _layoutSettingsVersion = CurrentLayoutSettingsVersion;
        }

        if (incomingLayout == null)
            incomingLayout = new PhoneMessageTemplateLayoutSettings { rowAlignment = TextAnchor.MiddleLeft };
        if (outgoingLayout == null)
            outgoingLayout = new PhoneMessageTemplateLayoutSettings { rowAlignment = TextAnchor.MiddleRight };
        if (photoLayout == null)
            photoLayout = new PhoneMessageTemplateLayoutSettings { rowAlignment = TextAnchor.MiddleLeft };

        if (resetLegacyTemplateHorizontalOffsets)
        {
            ResetLegacyTemplateHorizontalOffset(incomingLayout);
            ResetLegacyTemplateHorizontalOffset(outgoingLayout);
            ResetLegacyTemplateHorizontalOffset(photoLayout);
        }

        ApplyDefaults(incomingLayout, TextAnchor.MiddleLeft);
        ApplyDefaults(outgoingLayout, TextAnchor.MiddleRight);
        ApplyDefaults(photoLayout, TextAnchor.MiddleLeft);

        headerContactNameFontSize = Mathf.Max(0f, headerContactNameFontSize);
        headerContactNameMinFontSize = Mathf.Max(0f, headerContactNameMinFontSize);
        headerContactNameMaxFontSize = Mathf.Max(0f, headerContactNameMaxFontSize);
        messageAppearDuration = Mathf.Max(0f, messageAppearDuration);
        messagePostAppearDelay = Mathf.Max(0f, messagePostAppearDelay);
        messageAppearScaleFrom = Mathf.Max(0.01f, messageAppearScaleFrom);
        if (messageAppearEase == Ease.Unset)
            messageAppearEase = Ease.OutCubic;
    }

    void ResetLegacyTemplateHorizontalOffset(PhoneMessageTemplateLayoutSettings template)
    {
        if (template != null && Mathf.Approximately(template.horizontalOffset, bubbleHorizontalOffset))
            template.horizontalOffset = 0f;
    }

    public PhoneMessageTemplateLayoutSettings ResolveTemplateLayout(PhoneMessageSide side, bool hasAttachment)
    {
        Normalize();
        if (hasAttachment && photoLayout != null)
            return photoLayout;
        return side == PhoneMessageSide.Incoming ? incomingLayout : outgoingLayout;
    }

    public PhoneMessageTemplateLayoutSettings ResolvePositionLayout(
        PhoneMessageSide side,
        bool hasAttachment,
        PhoneMessageTemplateLayoutSettings fallback)
    {
        Normalize();
        if (hasAttachment && photoUsesMessageSidePosition)
            return side == PhoneMessageSide.Incoming ? incomingLayout : outgoingLayout;
        return fallback;
    }

    public TextAnchor ResolveRowAlignment(
        PhoneMessageSide side,
        bool hasAttachment,
        PhoneMessageTemplateLayoutSettings fallback)
    {
        PhoneMessageTemplateLayoutSettings positionLayout = ResolvePositionLayout(side, hasAttachment, fallback);
        if (positionLayout != null)
            return positionLayout.rowAlignment;
        return side == PhoneMessageSide.Incoming ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
    }

    public float ResolveHorizontalOffset(
        PhoneMessageSide side,
        bool hasAttachment,
        PhoneMessageTemplateLayoutSettings templateLayout)
    {
        Normalize();

        PhoneMessageTemplateLayoutSettings positionLayout = ResolvePositionLayout(side, hasAttachment, templateLayout);
        float resolved = positionLayout != null ? positionLayout.horizontalOffset : 0f;

        float sideOffset = side == PhoneMessageSide.Incoming
            ? incomingBubbleHorizontalOffset
            : outgoingBubbleHorizontalOffset;
        bool hasSideOverride = !Mathf.Approximately(sideOffset, 0f);
        bool hasPhotoOverride = hasAttachment && !Mathf.Approximately(photoBubbleHorizontalOffset, 0f);

        if (hasAttachment && !photoUsesMessageSidePosition)
            return resolved + (hasPhotoOverride ? photoBubbleHorizontalOffset : bubbleHorizontalOffset);

        resolved += hasSideOverride ? sideOffset : bubbleHorizontalOffset;

        if (hasAttachment && hasPhotoOverride)
            resolved += photoBubbleHorizontalOffset;

        return resolved;
    }

    public void CopyFrom(PhoneDialogueLayoutSettings source, bool overwrite)
    {
        if (source == null)
            return;

        source.Normalize();
        if (overwrite || safeAreaPadding == Vector4.zero)
            safeAreaPadding = source.safeAreaPadding;
        if (overwrite || messageContentPadding == Vector4.zero)
            messageContentPadding = source.messageContentPadding;
        if (overwrite)
            disableAllPhoneLayoutSettings = source.disableAllPhoneLayoutSettings;
        if (overwrite)
            showHeaderContactName = source.showHeaderContactName;
        if (overwrite || headerContactNameOffset == Vector2.zero)
            headerContactNameOffset = source.headerContactNameOffset;
        if (overwrite || headerContactNameSizeOffset == Vector2.zero)
            headerContactNameSizeOffset = source.headerContactNameSizeOffset;
        if (overwrite || headerContactNameMargin == Vector4.zero)
            headerContactNameMargin = source.headerContactNameMargin;
        if (overwrite || headerContactNameFontSize <= 0f)
            headerContactNameFontSize = source.headerContactNameFontSize;
        if (overwrite)
            overrideHeaderContactNameAutoSize = source.overrideHeaderContactNameAutoSize;
        if (overwrite)
            headerContactNameAutoSize = source.headerContactNameAutoSize;
        if (overwrite || headerContactNameMinFontSize <= 0f)
            headerContactNameMinFontSize = source.headerContactNameMinFontSize;
        if (overwrite || headerContactNameMaxFontSize <= 0f)
            headerContactNameMaxFontSize = source.headerContactNameMaxFontSize;
        if (overwrite || Mathf.Approximately(headerContactNameLineSpacing, 0f))
            headerContactNameLineSpacing = source.headerContactNameLineSpacing;
        if (overwrite)
            messageVerticalSpacing = source.messageVerticalSpacing;
        if (overwrite)
            bubbleHorizontalOffset = source.bubbleHorizontalOffset;
        if (overwrite)
            incomingBubbleHorizontalOffset = source.incomingBubbleHorizontalOffset;
        if (overwrite)
            outgoingBubbleHorizontalOffset = source.outgoingBubbleHorizontalOffset;
        if (overwrite)
            photoBubbleHorizontalOffset = source.photoBubbleHorizontalOffset;
        if (overwrite)
            photoUsesMessageSidePosition = source.photoUsesMessageSidePosition;
        if (overwrite)
            bubbleTopPadding = source.bubbleTopPadding;
        if (overwrite)
            bubbleBottomPadding = source.bubbleBottomPadding;
        if (overwrite)
            bubbleLeftPadding = source.bubbleLeftPadding;
        if (overwrite)
            bubbleRightPadding = source.bubbleRightPadding;
        if (overwrite)
            maxBubbleWidthPercent = source.maxBubbleWidthPercent;
        if (overwrite)
            minBubbleWidth = source.minBubbleWidth;
        if (overwrite || textOffsetInsideBubble == Vector2.zero)
            textOffsetInsideBubble = source.textOffsetInsideBubble;
        if (overwrite || photoMessageSize == Vector2.zero)
            photoMessageSize = source.photoMessageSize;
        if (overwrite)
            scrollToBottom = source.scrollToBottom;
        if (overwrite)
        {
            showSenderNamesInBubbles = source.showSenderNamesInBubbles;
            hideAvatarsInBubbles = source.hideAvatarsInBubbles;
            enforceContentVerticalLayout = source.enforceContentVerticalLayout;
            preserveMessageContentLayout = source.preserveMessageContentLayout;
            disableMessageContentSizeFitterWhenPreserved = source.disableMessageContentSizeFitterWhenPreserved;
            forceFullWidthMessageRows = source.forceFullWidthMessageRows;
            messageAppearAnimation = source.messageAppearAnimation;
            messageAppearDuration = source.messageAppearDuration;
            messagePostAppearDelay = source.messagePostAppearDelay;
            messageAppearSlideOffset = source.messageAppearSlideOffset;
            messageAppearScaleFrom = source.messageAppearScaleFrom;
            messageAppearEase = source.messageAppearEase;
        }

        if (incomingLayout == null)
            incomingLayout = new PhoneMessageTemplateLayoutSettings();
        if (outgoingLayout == null)
            outgoingLayout = new PhoneMessageTemplateLayoutSettings();
        if (photoLayout == null)
            photoLayout = new PhoneMessageTemplateLayoutSettings();

        CopyTemplateLayout(incomingLayout, source.incomingLayout, overwrite);
        CopyTemplateLayout(outgoingLayout, source.outgoingLayout, overwrite);
        CopyTemplateLayout(photoLayout, source.photoLayout, overwrite);
        Normalize();
    }

    void ApplyDefaults(PhoneMessageTemplateLayoutSettings template, TextAnchor alignment)
    {
        if (template == null)
            return;

        if (Mathf.Approximately(template.topPadding, 0f))
            template.topPadding = bubbleTopPadding;
        if (Mathf.Approximately(template.bottomPadding, 0f))
            template.bottomPadding = bubbleBottomPadding;
        if (Mathf.Approximately(template.leftPadding, 0f))
            template.leftPadding = bubbleLeftPadding;
        if (Mathf.Approximately(template.rightPadding, 0f))
            template.rightPadding = bubbleRightPadding;
        if (Mathf.Approximately(template.maxWidthPercent, 0f))
            template.maxWidthPercent = maxBubbleWidthPercent;
        if (Mathf.Approximately(template.minWidth, 0f))
            template.minWidth = minBubbleWidth;
        if (template.textOffsetInsideBubble == Vector2.zero)
            template.textOffsetInsideBubble = textOffsetInsideBubble;
        if (template.photoMessageSize == Vector2.zero)
            template.photoMessageSize = photoMessageSize;
        if (template.rowAlignment == TextAnchor.UpperLeft)
            template.rowAlignment = alignment;
        if (template.messageRootVerticalLayoutChildAlignment == TextAnchor.UpperLeft)
            template.messageRootVerticalLayoutChildAlignment = alignment;
        template.Normalize();
    }

    static void CopyTemplateLayout(
        PhoneMessageTemplateLayoutSettings target,
        PhoneMessageTemplateLayoutSettings source,
        bool overwrite)
    {
        if (target == null || source == null)
            return;

        source.Normalize();

        if (overwrite)
            target.showSenderName = source.showSenderName;
        if (overwrite)
            target.hideAvatar = source.hideAvatar;
        if (overwrite)
            target.senderNameBottomSpacing = source.senderNameBottomSpacing;
        if (overwrite || target.senderNameOffset == Vector2.zero)
            target.senderNameOffset = source.senderNameOffset;
        if (overwrite)
            target.senderNameAnchor = source.senderNameAnchor;
        if (overwrite)
            target.senderNameRelativeTo = source.senderNameRelativeTo;
        if (overwrite || target.senderNameSizeOffset == Vector2.zero)
            target.senderNameSizeOffset = source.senderNameSizeOffset;
        if (overwrite || target.senderNameMargin == Vector4.zero)
            target.senderNameMargin = source.senderNameMargin;
        if (overwrite || target.senderNameFontSize <= 0f)
            target.senderNameFontSize = source.senderNameFontSize;
        if (overwrite)
            target.overrideSenderNameAutoSize = source.overrideSenderNameAutoSize;
        if (overwrite)
            target.senderNameAutoSize = source.senderNameAutoSize;
        if (overwrite || target.senderNameMinFontSize <= 0f)
            target.senderNameMinFontSize = source.senderNameMinFontSize;
        if (overwrite || target.senderNameMaxFontSize <= 0f)
            target.senderNameMaxFontSize = source.senderNameMaxFontSize;
        if (overwrite || Mathf.Approximately(target.senderNameLineSpacing, 0f))
            target.senderNameLineSpacing = source.senderNameLineSpacing;
        if (overwrite)
            target.showTimeText = source.showTimeText;
        if (overwrite || target.timeTextOffset == Vector2.zero)
            target.timeTextOffset = source.timeTextOffset;
        if (overwrite || target.timeTextSizeOffset == Vector2.zero)
            target.timeTextSizeOffset = source.timeTextSizeOffset;
        if (overwrite || target.timeTextMargin == Vector4.zero)
            target.timeTextMargin = source.timeTextMargin;
        if (overwrite || target.timeTextFontSize <= 0f)
            target.timeTextFontSize = source.timeTextFontSize;
        if (overwrite)
            target.overrideTimeTextAutoSize = source.overrideTimeTextAutoSize;
        if (overwrite)
            target.timeTextAutoSize = source.timeTextAutoSize;
        if (overwrite || target.timeTextMinFontSize <= 0f)
            target.timeTextMinFontSize = source.timeTextMinFontSize;
        if (overwrite || target.timeTextMaxFontSize <= 0f)
            target.timeTextMaxFontSize = source.timeTextMaxFontSize;
        if (overwrite || Mathf.Approximately(target.timeTextLineSpacing, 0f))
            target.timeTextLineSpacing = source.timeTextLineSpacing;
        if (overwrite)
            target.verticalSpacing = source.verticalSpacing;
        if (overwrite)
            target.horizontalOffset = source.horizontalOffset;
        if (overwrite)
            target.topPadding = source.topPadding;
        if (overwrite)
            target.bottomPadding = source.bottomPadding;
        if (overwrite)
            target.leftPadding = source.leftPadding;
        if (overwrite)
            target.rightPadding = source.rightPadding;
        if (overwrite)
            target.maxWidthPercent = source.maxWidthPercent;
        if (overwrite)
            target.minWidth = source.minWidth;
        if (overwrite || target.textOffsetInsideBubble == Vector2.zero)
            target.textOffsetInsideBubble = source.textOffsetInsideBubble;
        if (overwrite || target.bodyTextScale == Vector3.zero)
            target.bodyTextScale = source.bodyTextScale;
        if (overwrite || target.bodyFontSize <= 0f)
            target.bodyFontSize = source.bodyFontSize;
        if (overwrite)
            target.overrideBodyAutoSize = source.overrideBodyAutoSize;
        if (overwrite)
            target.bodyAutoSize = source.bodyAutoSize;
        if (overwrite || target.bodyMinFontSize <= 0f)
            target.bodyMinFontSize = source.bodyMinFontSize;
        if (overwrite || target.bodyMaxFontSize <= 0f)
            target.bodyMaxFontSize = source.bodyMaxFontSize;
        if (overwrite || Mathf.Approximately(target.bodyLineSpacing, 0f))
            target.bodyLineSpacing = source.bodyLineSpacing;
        if (overwrite || target.rowPadding == Vector4.zero)
            target.rowPadding = source.rowPadding;
        if (overwrite || target.rowPositionOffset == Vector2.zero)
            target.rowPositionOffset = source.rowPositionOffset;
        if (overwrite || target.bubblePositionOffset == Vector2.zero)
            target.bubblePositionOffset = source.bubblePositionOffset;
        if (overwrite || target.bubbleSizeOffset == Vector2.zero)
            target.bubbleSizeOffset = source.bubbleSizeOffset;
        if (overwrite || target.avatarOffset == Vector2.zero)
            target.avatarOffset = source.avatarOffset;
        if (overwrite || target.photoMessageSize == Vector2.zero)
            target.photoMessageSize = source.photoMessageSize;
        if (overwrite || target.photoOffset == Vector2.zero)
            target.photoOffset = source.photoOffset;
        if (overwrite)
            target.rowAlignment = source.rowAlignment;
        if (overwrite)
            target.useMessageRootVerticalLayout = source.useMessageRootVerticalLayout;
        if (overwrite || target.messageRootVerticalLayoutPadding == Vector4.zero)
            target.messageRootVerticalLayoutPadding = source.messageRootVerticalLayoutPadding;
        if (overwrite)
            target.messageRootVerticalLayoutSpacing = source.messageRootVerticalLayoutSpacing;
        if (overwrite)
            target.messageRootVerticalLayoutChildAlignment = source.messageRootVerticalLayoutChildAlignment;
        if (overwrite)
            target.messageRootVerticalLayoutReverseArrangement = source.messageRootVerticalLayoutReverseArrangement;
        if (overwrite)
            target.messageRootVerticalLayoutControlChildWidth = source.messageRootVerticalLayoutControlChildWidth;
        if (overwrite)
            target.messageRootVerticalLayoutControlChildHeight = source.messageRootVerticalLayoutControlChildHeight;
        if (overwrite)
            target.messageRootVerticalLayoutUseChildScaleWidth = source.messageRootVerticalLayoutUseChildScaleWidth;
        if (overwrite)
            target.messageRootVerticalLayoutUseChildScaleHeight = source.messageRootVerticalLayoutUseChildScaleHeight;
        if (overwrite)
            target.messageRootVerticalLayoutChildForceExpandWidth = source.messageRootVerticalLayoutChildForceExpandWidth;
        if (overwrite)
            target.messageRootVerticalLayoutChildForceExpandHeight = source.messageRootVerticalLayoutChildForceExpandHeight;
        if (overwrite)
            target.overrideContainerVerticalLayout = source.overrideContainerVerticalLayout;
        if (overwrite || target.containerPadding == Vector4.zero)
            target.containerPadding = source.containerPadding;
        if (overwrite)
            target.containerSpacing = source.containerSpacing;
        if (overwrite)
            target.containerChildAlignment = source.containerChildAlignment;
        if (overwrite)
            target.containerReverseArrangement = source.containerReverseArrangement;
        if (overwrite)
            target.containerControlChildWidth = source.containerControlChildWidth;
        if (overwrite)
            target.containerControlChildHeight = source.containerControlChildHeight;
        if (overwrite)
            target.containerUseChildScaleWidth = source.containerUseChildScaleWidth;
        if (overwrite)
            target.containerUseChildScaleHeight = source.containerUseChildScaleHeight;
        if (overwrite)
            target.containerChildForceExpandWidth = source.containerChildForceExpandWidth;
        if (overwrite)
            target.containerChildForceExpandHeight = source.containerChildForceExpandHeight;
        if (overwrite)
            target.overrideContainerContentSizeFitter = source.overrideContainerContentSizeFitter;
        if (overwrite)
            target.containerHorizontalFit = source.containerHorizontalFit;
        if (overwrite)
            target.containerVerticalFit = source.containerVerticalFit;
        if (overwrite)
            target.stretchBackground = source.stretchBackground;
        if (overwrite || target.backgroundStretchOffsets == Vector4.zero)
            target.backgroundStretchOffsets = source.backgroundStretchOffsets;
        if (overwrite)
            target.backgroundIgnoreLayout = source.backgroundIgnoreLayout;
        if (overwrite)
            target.backgroundSendToBack = source.backgroundSendToBack;
        target.Normalize();
    }
}

[Serializable]
public sealed class PhonePreviewSettings
{
    [InspectorName("Quick Preview Contact")]
    [Tooltip("Contact name used by editor quick previews when they build a temporary PhoneDialogueNode.")]
    public string quickPreviewContactName = "\u0420\u043E\u0431";

    [InspectorName("Лимит сообщений preview")]
    [Tooltip("Сколько сообщений максимум рисовать в editor/quick preview, чтобы случайно не заспавнить слишком длинную переписку.")]
    public int editorPreviewMessageLimit = 24;
    [InspectorName("Статичный preview в Edit Mode")]
    [Tooltip("Разрешает показывать телефон прямо в Edit Mode без DOTween-корутин и Play Mode.")]
    public bool showStaticPreviewInEditMode = true;
    [InspectorName("Очищать preview при выключении")]
    [Tooltip("Удаляет заспавненные preview-бабблы при выключении PhoneDialogueUI.")]
    public bool clearPreviewOnDisable = true;
    [InspectorName("Скрывать персонажей при preview")]
    [Tooltip("Во время phone preview скрывает обычные story-персонажи, чтобы телефон не перекрывался лишними спрайтами.")]
    public bool hideStoryCharactersDuringPhonePreview = true;
    [InspectorName("Задержка печати runtime")]
    [Tooltip("Задержка между сообщениями в runtime-показе телефона, если PhoneDialogueNode не задал своё значение.")]
    public float runtimeTypingDelay = 0.15f;
    [InspectorName("Подставлять фото по умолчанию в quick preview")]
    [Tooltip("Если выключено, токен [photo]/[фото] не создаёт картинку без явно назначенного attachment. Оставь выключенным, чтобы не появлялась случайная маленькая голова.")]
    public bool useDefaultPhotoSpriteInQuickPreview = false;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(quickPreviewContactName))
            quickPreviewContactName = "\u0420\u043E\u0431";
        else
            quickPreviewContactName = quickPreviewContactName.Trim();
        editorPreviewMessageLimit = Mathf.Clamp(editorPreviewMessageLimit, 1, 200);
        runtimeTypingDelay = Mathf.Max(0f, runtimeTypingDelay);
    }

    public void CopyFrom(PhonePreviewSettings source, bool overwrite)
    {
        if (source == null)
            return;

        if (overwrite || string.IsNullOrWhiteSpace(quickPreviewContactName))
            quickPreviewContactName = source.quickPreviewContactName;
        if (overwrite || editorPreviewMessageLimit <= 0)
            editorPreviewMessageLimit = source.editorPreviewMessageLimit;
        if (overwrite)
        {
            showStaticPreviewInEditMode = source.showStaticPreviewInEditMode;
            clearPreviewOnDisable = source.clearPreviewOnDisable;
            hideStoryCharactersDuringPhonePreview = source.hideStoryCharactersDuringPhonePreview;
            useDefaultPhotoSpriteInQuickPreview = source.useDefaultPhotoSpriteInQuickPreview;
        }
        if (overwrite || runtimeTypingDelay <= 0f)
            runtimeTypingDelay = source.runtimeTypingDelay;
        Normalize();
    }
}

public sealed class PhonePreviewValidationResult
{
    readonly List<string> _warnings = new List<string>();
    readonly List<string> _errors = new List<string>();

    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;
    public bool HasWarnings => _warnings.Count > 0;
    public bool HasErrors => _errors.Count > 0;

    public void Warn(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _warnings.Add(message);
    }

    public void Error(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _errors.Add(message);
    }
}

public static class PhonePreviewValidator
{
    public static PhonePreviewValidationResult Validate(PhoneDialogueUI ui, PhoneDialogueNode node = null, bool requireMessages = false)
    {
        var result = new PhonePreviewValidationResult();
        if (ui == null)
        {
            result.Error("Не назначен PhoneDialogueUI. Предпросмотр телефона невозможен.");
            return result;
        }

        PhoneDialogueUIReferences references = ui.PhoneReferences;
        if (references.ResolveRoot(ui) == null)
            result.Error("Не назначен корневой объект телефона.");
        if (references.ResolveMessageContent(ui) == null)
            result.Error("Не назначен контейнер сообщений телефона. Сообщения не могут быть отображены.");
        if (references.ResolveIncomingBubbleTemplate(ui) == null)
            result.Error("Не назначен шаблон входящего SMS-баббла. Статический предпросмотр сообщений недоступен.");
        if (references.ResolveOutgoingBubbleTemplate(ui) == null)
            result.Error("Не назначен шаблон исходящего SMS-баббла. Статический предпросмотр сообщений недоступен.");
        if (references.ResolveContactNameText(ui) == null)
            result.Warn("Не назначен TMP_Text имени контакта телефона.");

        ValidateBubbleTemplate(references.ResolveIncomingBubbleTemplate(ui), "входящего", result);
        ValidateBubbleTemplate(references.ResolveOutgoingBubbleTemplate(ui), "исходящего", result);
        ValidateTemplateReferences(references.incomingTemplate, "входящего", result);
        ValidateTemplateReferences(references.outgoingTemplate, "исходящего", result);
        if (references.photoMessageTemplate != null && references.photoMessageTemplate.ResolveRootObject() != null)
            ValidateTemplateReferences(references.photoMessageTemplate, "фото", result);

        if (node == null)
        {
            result.Warn("Не назначен PhoneDialogueNode. Будет использован текст предпросмотра.");
        }
        else if (requireMessages && (node.messages == null || node.messages.Count == 0))
        {
            result.Warn("Сообщения не найдены. Проверьте PhoneDialogueNode.");
        }

        return result;
    }

    static void ValidateBubbleTemplate(GameObject template, string label, PhonePreviewValidationResult result)
    {
        if (template == null)
            return;

        if (template.GetComponent<RectTransform>() == null)
            result.Warn("У шаблона " + label + " баббла нет RectTransform.");
        if (template.GetComponentInChildren<TMP_Text>(true) == null)
            result.Warn("Не назначен TMP_Text внутри " + label + " баббла. Текст сообщения не будет отображён.");
    }

    static void ValidateTemplateReferences(PhoneMessageTemplateReferences template, string label, PhonePreviewValidationResult result)
    {
        if (template == null || template.ResolveRootObject() == null)
            return;

        if (template.messageText == null)
            result.Warn("У шаблона " + label + " сообщения не назначен MessageText. Spawn будет искать текст по имени, но лучше назначить ссылку явно.");
        if (template.layout != null && template.layout.showTimeText && template.timeText == null)
            result.Warn("Template " + label + " has Show Time Text enabled but no explicit TimeText reference. Runtime will create TimeText automatically; assign it explicitly for exact prefab positioning.");
        if (template.senderNameText == null)
            result.Warn("У шаблона " + label + " сообщения не назначен Sender/ContactName TMP_Text.");
        if (template.container == null)
            result.Warn("У шаблона " + label + " сообщения не назначен контейнер баббла.");
        if (template.backgroundImage == null)
            result.Warn("У шаблона " + label + " сообщения не назначен Image фона баббла.");
    }
}

public static class PhoneLayoutValidator
{
    static int _nextRecalcId;

    public static int ValidateAndLog(PhoneDialogueUI ui, string reason, PhoneDialogueNode node = null)
    {
        int recalcId = ++_nextRecalcId;
        if (ui == null)
            return recalcId;

        PhoneDialogueUIReferences references = ui.PhoneReferences;
        RectTransform viewport = references.ResolveViewport(ui);
        RectTransform content = references.ResolveMessageContent(ui);

        var metadata = LogMetadata.Of(
            "recalcId", recalcId,
            "reason", reason,
            "resolution", Screen.width + "x" + Screen.height,
            "node", node != null ? node.name : "",
            "contactName", node != null ? node.contactName : "",
            "object", ui.gameObject != null ? ui.gameObject.name : "");

        if (viewport == null)
        {
            ThrottledAppLogger.Warn(
                "PhoneLayoutMissingViewport",
                AppLogCategory.Layout,
                nameof(PhoneLayoutValidator),
                nameof(ValidateAndLog),
                "Не назначен viewport сообщений телефона.",
                metadata);
            return recalcId;
        }

        if (content == null)
        {
            ThrottledAppLogger.Warn(
                "PhoneLayoutMissingContent",
                AppLogCategory.Layout,
                nameof(PhoneLayoutValidator),
                nameof(ValidateAndLog),
                "Не назначен content сообщений телефона.",
                metadata);
            return recalcId;
        }

        if (!IsFinite(viewport.rect.size) || !IsFinite(content.rect.size))
        {
            ThrottledAppLogger.Warn(
                "PhoneLayoutInvalidRect",
                AppLogCategory.Layout,
                nameof(PhoneLayoutValidator),
                nameof(ValidateAndLog),
                "Размеры viewport/content телефона содержат NaN или Infinity.",
                metadata);
            return recalcId;
        }

        Rect viewportRect = GetWorldRect(viewport);
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            Rect childRect = GetWorldRect(child);
            if (!viewportRect.Overlaps(childRect))
            {
                ThrottledAppLogger.Warn(
                    "LayoutBubbleOutsideViewport:" + i,
                    AppLogCategory.Layout,
                    nameof(PhoneLayoutValidator),
                    nameof(ValidateAndLog),
                    "Баббл сообщения вышел за пределы viewport после пересчёта.",
                    LogMetadata.Of(
                        "recalcId", recalcId,
                        "reason", reason,
                        "messageIndex", i,
                        "viewport", FormatRect(viewportRect),
                        "bubbleRect", FormatRect(childRect),
                        "node", node != null ? node.name : "",
                        "object", child.name));
            }
        }

        AppLogger.Trace(
            AppLogCategory.Layout,
            nameof(PhoneLayoutValidator),
            nameof(ValidateAndLog),
            "Layout телефона пересчитан.",
            metadata);
        return recalcId;
    }

    static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[0].x;
        float maxY = corners[0].y;

        for (int i = 1; i < corners.Length; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxX = Mathf.Max(maxX, corners[i].x);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    static string FormatRect(Rect rect)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "x={0:0.##},y={1:0.##},w={2:0.##},h={3:0.##}",
            rect.x,
            rect.y,
            rect.width,
            rect.height);
    }
}

public static class PhoneDialogueTweenCleanup
{
    public static void KillHierarchy(GameObject root)
    {
        if (root == null || DOTween.instance == null)
            return;

        DOTween.Kill(root);
        DOTween.Kill(root.transform);

        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            DOTween.Kill(component);
            if (component is RectTransform rectTransform)
                DOTween.Kill(rectTransform);
        }
    }
}

public sealed class PhoneDialoguePreviewMessageMarker : MonoBehaviour
{
    [NonSerialized] public PhoneMessageTemplateReferences templateReferences;
    [NonSerialized] public PhoneMessageTemplateLayoutSettings templateLayout;
    [NonSerialized] public PhoneMessageSide side;
    [NonSerialized] public string senderName;
    [NonSerialized] public string resolvedTimeText;
    [NonSerialized] public bool hasAttachment;
    [NonSerialized] public bool usesPhotoLayout;
    [NonSerialized] public Vector2 appliedRowOffset;
    [NonSerialized] public Vector2 appliedBubbleOffset;
    [NonSerialized] public Vector2 appliedSenderNameOffset;
    [NonSerialized] public Vector2 appliedSenderNameSizeOffset;
    [NonSerialized] public Vector2 appliedTimeTextOffset;
    [NonSerialized] public Vector2 appliedTimeTextSizeOffset;
    [NonSerialized] public Vector2 appliedAvatarOffset;
    [NonSerialized] public Vector2 appliedPhotoOffset;

    public void Configure(
        PhoneMessageTemplateReferences references,
        PhoneMessageTemplateLayoutSettings layout,
        PhoneMessageSide messageSide,
        string messageSenderName,
        bool messageHasAttachment,
        bool messageUsesPhotoLayout)
    {
        templateReferences = references;
        templateLayout = layout;
        side = messageSide;
        senderName = messageSenderName;
        hasAttachment = messageHasAttachment;
        usesPhotoLayout = messageUsesPhotoLayout;
    }
}

public sealed class PhoneDialogueEditorPreviewRenderer
{
    public bool Render(PhoneDialogueUI ui, PhoneDialogueNode node, string reason = "EditModePreview")
    {
        if (ui == null)
            return false;

        return ui.ShowStaticPreview(node, reason);
    }
}

public sealed class PhoneDialogueRuntimePlayer
{
    public bool Play(PhoneDialogueUI ui, PhoneDialogueNode node, Action onComplete = null)
    {
        if (!Application.isPlaying)
        {
            ThrottledAppLogger.Warn(
                "RuntimePhonePreviewRequiresPlayMode",
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueRuntimePlayer),
                nameof(Play),
                "Runtime-предпросмотр телефона доступен только в Play Mode.");
            return false;
        }

        if (ui == null)
        {
            ThrottledAppLogger.Warn(
                "RuntimePhonePreviewMissingUI",
                AppLogCategory.PhoneDialogue,
                nameof(PhoneDialogueRuntimePlayer),
                nameof(Play),
                "PhoneDialogueUI не найден в открытой сцене.");
            return false;
        }

        ui.Show(node, onComplete);
        return true;
    }
}
