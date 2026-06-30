# Phone Dialogue: padding и layout

## Формат Left / Right / Top / Bottom

В инспекторе телефона поля padding теперь рисуются так же, как в стандартных Unity-компонентах `VerticalLayoutGroup` и `HorizontalLayoutGroup`:

- `Left` - отступ слева.
- `Right` - отступ справа.
- `Top` - отступ сверху.
- `Bottom` - отступ снизу.

Так отображаются:

- `Safe Area Padding`
- `Padding Content сообщений`
- `Margin имени`
- `Padding строки`
- `Padding контейнера`
- `Stretch offsets Background`

Внутри данные всё ещё хранятся как `Vector4`, чтобы не потерять старые сериализованные значения в сцене и ассетах.

## Padding контейнера

`Padding контейнера` напрямую применяется к `VerticalLayoutGroup.padding` на объекте `Container` spawned-сообщения.

Обычно его можно оставить `0 / 0 / 0 / 0`, потому что основной внутренний отступ текста задаётся полями:

- `Padding сверху`
- `Padding снизу`
- `Padding слева`
- `Padding справа`

Если нужно двигать весь контент внутри `Container`, меняй именно `Padding контейнера`.

## Control Child Width / Height

Для телефонных бабблов эти флаги по умолчанию выключены:

- `Control Child Width контейнера`
- `Control Child Height контейнера`

Причина: если `VerticalLayoutGroup` начинает сам управлять шириной TMP-текста до того, как bubble получил рассчитанную ширину, TMP может схлопнуться и начать переносить текст по одной букве на строку.

Включай эти флаги только если шаблон специально собран под Unity auto-layout и ты видишь, что дочерние элементы должны полностью контролироваться `VerticalLayoutGroup`.

## Background Stretch

`Background Stretch` растягивает `Background` по `Container`.

`Stretch offsets Background`:

- `Left` - смещение левого края.
- `Right` - смещение правого края.
- `Top` - смещение верхнего края.
- `Bottom` - смещение нижнего края.

Здесь можно использовать отрицательные значения, если подложка должна выходить за пределы контейнера.
