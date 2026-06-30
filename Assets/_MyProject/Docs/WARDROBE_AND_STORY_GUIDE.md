# Гайд: Wardrobe, история, фоны и музыка

Этот документ описывает, как добавлять гардероб героини, одежду, прически, фоны, музыку и story JSON.

## Общий Flow

Первый запуск истории работает так:

1. Игрок нажимает кнопку истории.
2. Если это первый запуск, игра спрашивает имя героини.
3. `MenuController` закрывает экран истории и открывает экран `Wardrobe`.
4. `WardrobeHeroSetupPage` запускает шаги:
   - внешность;
   - одежда;
   - прическа.
5. Игрок нажимает `Готово`.
6. Игра возвращается к экрану истории.
7. История начинается уже с выбранными настройками героини.

Выбранные данные хранятся в `HeroCustomizationState`:

```text
playerName
appearance
outfitId
hairId
```

`PlayerAppearance` остается главным фасадом для остального кода. Через него история узнает имя, внешность, выбранную одежду и прическу.

## Как работает смена одежды

Важно: одежда больше не заменяет тело персонажа.

Сейчас персонаж собирается слоями:

```text
Body   -> базовое тело / внешность
Outfit -> одежда поверх тела
Hair   -> прическа поверх тела и одежды
Face   -> эмоции / лицо, если используется layered mode
```

Когда игрок выбирает одежду:

1. Игрок кликает вариант одежды в `WardrobeHeroSetupPage` или `WardrobeController`.
2. Берется выбранный `ClothingItem`.
3. Его `id` сохраняется как outfit id.
4. Его `sprite` сохраняется в `PlayerAppearance.OutfitSprite`.
5. В `GameState` пишется ключ:

```text
hero:outfit -> hero_outfit_black_dress
```

Когда игрок выбирает прическу:

```text
hero:hair -> hero_hair_long_brown
```

В истории `CharacterViewManager` смотрит, что у `CharacterData` включено `inheritAppearanceFromPlayer`, и добавляет поверх тела два runtime-слоя:

```text
[Equipment] Outfit
[Equipment] Hair
```

Поэтому outfit не трогает `CharacterData.defaultSprite` и не затирает body.

## Что такое ClothingItem

Одежда и прически создаются как `ClothingItem`.

Создание:

```text
Create -> VN -> Clothing Item
```

Обязательные поля:

```text
id
type
sprite
```

Пример одежды:

```text
id: hero_outfit_black_dress
type: Outfit
sprite: прозрачный PNG с платьем
```

Пример прически:

```text
id: hero_hair_long_brown
type: Hair
sprite: прозрачный PNG с волосами
```

Правила:

- `id` должен быть уникальным.
- Не меняй `id` после релиза, иначе старые сейвы не найдут одежду.
- `Outfit` должен быть отдельным слоем одежды.
- `Hair` должен быть отдельным слоем волос.
- Не клади тело персонажа в outfit, если это не специальный full-body костюм.
- Лучше использовать lowercase и underscore:

```text
hero_outfit_school_uniform
hero_outfit_red_dress
hero_hair_short_black
hero_hair_long_brown
```

## Настройка WardrobeHeroSetupPage

На экране `Wardrobe` должен быть компонент `WardrobeHeroSetupPage`.

Основные списки:

```text
Appearance Options
Outfit Items
Hair Items
```

`Appearance Options` отвечает за внешность:

```text
European
Asian
Latino
```

`Outfit Items` содержит все `ClothingItem` с:

```text
type = Outfit
```

`Hair Items` содержит все `ClothingItem` с:

```text
type = Hair
```

Важные поля:

```text
Target Character Id: hero
Outfit Slot Suffix: outfit
Hair Slot Suffix: hair
```

Итоговые ключи в `GameState` будут такими:

```text
hero:outfit
hero:hair
```

### Несколько экранов гардероба для разных историй

Если новая история должна выглядеть иначе, не меняй старый экран гардероба напрямую. Создай отдельный объект с `WardrobeHeroSetupPage` внутри общего `wardrobePanel` и привяжи его к истории.

В блоке `Story Binding`:

```text
Story Ids: <story_id>
Chapter Ids: <episode_id или chapter_id, опционально>
Use As Fallback For Unmatched Stories: false
```

На старом общем экране оставь:

```text
Story Ids: empty
Chapter Ids: empty
Use As Fallback For Unmatched Stories: true
```

Когда открывается `openWardrobe`, `appearanceChoice` или `wardrobeChoice`, игра выбирает самый подходящий `WardrobeHeroSetupPage`: сначала по `storyId + chapterId`, потом по `storyId`, потом по `chapterId`, потом fallback.

Для **«Привычка притворяться»** арты бери из:

```text
Assets/_MyProject/Art/Привычка притворяться/
```

Минимальный набор для отдельного wardrobe screen:

- `Гардероб/IMG_8524.PNG` или другой фон экрана;
- `Главная героиня/Тело` для body/appearance;
- `Главная героиня/Одежда` для `ClothingItem` типа `Outfit`;
- `Главная героиня/Прически` для `ClothingItem` типа `Hair`;
- `Плашки` для frame/button/stat/dialogue UI, если экран должен полностью отличаться.

### Диалоговое окно и root гардероба по истории

Для полностью другой истории можно переключать не только `WardrobeHeroSetupPage`, но и весь dialogue UI.

На объекте `StoryManager` есть блок `Story UI Profiles`:

```text
Story Asset / Story Ids                          -> какая история использует профиль
Dialogue User Interface -> Dialogue UI           -> DialogueUIManager этой истории
Dialogue User Interface -> Cutscene User Interface -> отдельный DialogueUIManager для катсцен
Dialogue User Interface -> Enable/Disable        -> объекты, которые включаются или выключаются для истории
Wardrobe User Interface -> Wardrobe Panel        -> root гардероба этой истории
Wardrobe User Interface -> Setup Page            -> конкретная WardrobeHeroSetupPage, если panel не задан
```

При выборе истории `StoryManager.SelectStory()` применяет лучший профиль по `StoryData`, `storyId`, `storyName` или имени ассета. Если профиль не найден, остаются дефолтные ссылки из сцены.

Важно: `appearanceChoice` и `wardrobeChoice` сначала пытаются открыть story-specific `WardrobeHeroSetupPage`, и только если его нет, падают в старый стрелочный `WardrobeController`.

## Настройка CharacterData героини

Для главной героини в `CharacterData` желательно:

```text
inheritAppearanceFromPlayer = true
```

Если используются разные варианты внешности, заполняй:

```text
appearanceVariants
```

Для layered-персонажа:

```text
bodySprite
emotionLayers
```

Тело должно быть отдельно. Одежда и волосы приходят из гардероба.

## Предпросмотр и Offset слоёв

Предпросмотр теперь делается только на реальном экране гардероба в Unity, а не в отдельной картинке внутри Inspector.

Как поставить любого персонажа на этот экран:

1. Открой сцену с `WardrobeHeroSetupPage`.
2. Выдели нужный `CharacterData`.
3. Открой верхнюю кнопку `Story Preview` в Unity toolbar и выбери нужную историю/главу гардероба.
4. Меняй `AppearanceVariant.previewOffset`, `previewWidth`, `previewHeight` прямо в `CharacterData` - изменения через `OnValidate` сразу обновят настоящую героиню на экране.

Реальный экран рисует слои в том же порядке, который используется в истории:

```text
Body -> Outfit -> Emotion/Face -> Hair
```

Где настраивать сдвиг и размер:

- Тело и внешность в гардеробе/preview: `AppearanceVariant.previewOffset`, `previewWidth`, `previewHeight`.
- Full-body эмоции в preview: `CharacterEmotion.previewOffset`, `previewWidth`, `previewHeight`.
- Лицо/голова в layered preview: `CharacterEmotionLayer.previewOffset`, `previewWidth`, `previewHeight`.
- Общие wardrobe-настройки персонажа: `CharacterData.wardrobeLayerLayout`.
- Wardrobe-настройки под конкретную внешность героини: `appearanceVariants -> Wardrobe Layer Layout`.
- Одежда и волосы в гардеробе для конкретной вещи/внешности: `CharacterData.wardrobeEquipmentLayouts`.
- Последний точечный override на самом предмете: `ClothingItem.wardrobeOffset`, `wardrobeWidth`, `wardrobeHeight`, `wardrobeScale`, `wardrobeAppearanceLayouts`.
- Story-настройки истории: `CharacterData.storyLayerLayout`, `AppearanceVariant.storyLayerLayout`, `CharacterEmotion.storyLayout`, `CharacterEmotionLayer.storyLayout`.
- Одежда и волосы в истории для конкретного предмета: `CharacterData.storyEquipmentLayouts`.

В гардеробе `ClothingItem.wardrobeOffset` считается от нулевой позиции слоя. То есть слой сначала сбрасывается в `(0, 0, 0)`, а потом к нему применяется offset выбранной одежды или волос. Эти поля больше не применяются в истории.

Если один outfit или hair идеально стоит на European, но плывет на Asian/Latino, сначала правь это в `Hero (CharacterData)`, а не в каждом `ClothingItem`:

- общая поправка для всех вещей: `wardrobeLayerLayout -> Outfit/Hair`;
- поправка только для Asian/Latino/European: `appearanceVariants -> нужная внешность -> Wardrobe Layer Layout -> Outfit/Hair`;
- поправка конкретной вещи: `wardrobeEquipmentLayouts`, где можно выбрать `item`, `anyAppearance = false` и нужный `appearanceType`.

`ClothingItem.wardrobeAppearanceLayouts` оставь для редких случаев, когда конкретный предмет должен хранить свою собственную подгонку независимо от персонажа.

В истории настройки не общие для всех персонажей. Настраивай их прямо в `CharacterData`:

- `storyLayerLayout.body` - базовое тело персонажа в истории.
- `storyLayerLayout.emotion` - общий fallback для эмоций/лица.
- `storyLayerLayout.outfit` и `storyLayerLayout.hair` - fallback для одежды/волос этого персонажа.
- `Permanent Story Equipment -> Permanent Outfit` - одежда, которая всегда надета на этого персонажа в story-сценах.
- `Permanent Story Equipment -> Permanent Hair` - волосы/прическа, которые всегда надеты на этого персонажа в story-сценах.
- `appearanceVariants -> Story Layer Layout` - overrides для конкретной внешности героини, например Asian/Latino/European.
- `storyEquipmentLayouts` - точные overrides для конкретного `ClothingItem`. Это только layout уже надетого предмета, не сам факт экипировки. Укажи `item`, выбери `anyAppearance` или конкретный `appearanceType`, потом настрой `layout`.

Если story offset идеально настроен под European, но ломается на другой внешности героини, используй не общий `storyLayerLayout`, а overrides внутри `appearanceVariants` или `storyEquipmentLayouts`:

- тело/позиция всей героини: `appearanceVariants -> Story Layer Layout` и `Story Position Layout`;
- конкретная одежда или волосы: `storyEquipmentLayouts`, `anyAppearance = false`, `appearanceType = Asian/Latino/European`.

Чтобы NPC всегда был в одной одежде, создай или выбери `ClothingItem`, назначь его в `Permanent Outfit` у `CharacterData`, а сдвиг/масштаб этого предмета настрой через `storyEquipmentLayouts`. Для Ивана это `Permanent Outfit = Ivan_defolt`, а `storyEquipmentLayouts` нужен только для подгонки позиции этой одежды.

В `StoryLayerLayout` значение `0` у `width` или `height` значит "оставь размер слоя как в сцене". Можно менять только ширину или только высоту; вторая ось останется дефолтной.

Проверка делается через верхнюю кнопку `Story Preview` в Unity toolbar: выбери историю, главу или конкретную точку. Отдельных кнопок `Story Left`, `Story Center`, `Story Right` и ручного story-preview в Inspector больше нет.

Если эмоция - это отдельная голова/лицо, включи `useLayeredEmotions` и заполни `emotionLayers`. Для старых ассетов, где головы уже лежат в `emotions`, история тоже возьмёт их как overlay-слой поверх `bodySprite`, если `useLayeredEmotions = true`. Для героини с разными внешностями можно также хранить overlay-эмоции в `AppearanceVariant.emotions` - история возьмёт их как слой лица. Если эмоции - это полные спрайты героя, оставь `useLayeredEmotions = false` и заполняй `emotions`.

## Как поменять одежду в самой истории

Есть два варианта.

### Вариант 1: открыть полный гардероб

JSON-нода:

```json
{
  "id": "open_wardrobe_intro",
  "type": "openWardrobe",
  "next": "after_wardrobe"
}
```

Игрок пройдет полный flow:

```text
внешность -> одежда -> прическа -> готово
```

После этого история продолжится с `after_wardrobe`.

### Вариант 2: выбор конкретной одежды

JSON-нода:

```json
{
  "id": "choose_party_outfit",
  "type": "wardrobeChoice",
  "characterId": "hero",
  "clothes": [
    "hero_outfit_black_dress",
    "hero_outfit_red_dress"
  ],
  "exits": [
    "after_black_dress",
    "after_red_dress"
  ]
}
```

Как это работает:

1. История открывает выбор одежды.
2. Игрок выбирает один `ClothingItem`.
3. Если это `Outfit`, он сохраняется в `hero:outfit`.
4. Если это `Hair`, он сохраняется в `hero:hair`.
5. История идет в соответствующий `exit`.

Если `clothes[0]` выбран, история идет в `exits[0]`.

Если `clothes[1]` выбран, история идет в `exits[1]`.

## Как выдать одежду игроку

Используй `addClothing`.

```json
{
  "id": "unlock_black_dress",
  "type": "addClothing",
  "clothing": "hero_outfit_black_dress",
  "next": "choose_party_outfit"
}
```

Это добавит предмет в гардероб игрока.

## Как проверить, есть ли одежда

Используй `wardrobeCheck`.

```json
{
  "id": "has_black_dress",
  "type": "wardrobeCheck",
  "itemId": "hero_outfit_black_dress",
  "hasItemNext": "wear_black_dress",
  "noItemNext": "unlock_black_dress"
}
```

Если вещь есть, история идет в `hasItemNext`.

Если вещи нет, история идет в `noItemNext`.

## Story JSON: базовая структура

```json
{
  "version": 1,
  "storyId": "zls",
  "chapterId": "zls_1",
  "episodeId": "zls_1",
  "title": "Глава 1",
  "characters": [],
  "nodes": []
}
```

Частые типы нод:

```text
scene
dialogue
choice
appearanceChoice
wardrobeChoice
addClothing
openWardrobe
wardrobeCheck
image
phoneDialogue
statChange
condition
premium
effect
```

## Где создаются выборы

Обычные story-выборы начинаются с JSON-ноды:

```json
{
  "id": "choice_example",
  "type": "choice",
  "promptText": "Выберите вариант.",
  "choices": [
    { "text": "Первый вариант", "next": "after_first" },
    { "text": "Второй вариант", "next": "after_second" }
  ]
}
```

Путь выполнения:

```text
StoryJsonConverter.ConfigureChoiceNode
-> StoryManager.ProcessChoice
-> DialogueUIManager.ShowChoice
-> Instantiate(choiceButtonPrefab, choiceContainer)
```

Prefab кнопки назначается в сцене на `DialogueUIManager`:

```text
Choice Button Prefab
Choice Container
```

`Choice Container` должен быть UI-объектом вне двигающегося `cameraRoot`, иначе выборы будут уезжать вместе с камерой и появляться не там. В сцене `Game` это `Novel/ChoicePanel`.

Камера для story-панорамирования должна двигать фон и персонажей, но не UI. Поэтому `CameraController.cameraRoot` должен указывать на `Novel/Background`, а `Linked Camera Roots` должен содержать `Novel/CharactersRoot`. Тогда при пане камеры фон и персонажи едут вместе, например левый персонаж уходит влево при сдвиге камеры вправо, а `ChoicePanel` и `DialoguePanel` остаются на своих UI-позициях.

`CanvasGroup` на `Novel` - служебный alpha всего story-экрана для `StoryScreenNavigator`. Не используй его для настройки прозрачности фона: когда story UI прозрачный, под ним виден `Main Camera -> Background Color`, поэтому визуально может казаться, что фон не меняется. Для прозрачности конкретной картинки меняй `Image Color Alpha` у `Novel/Background`, а для затемнения добавляй отдельный overlay поверх фона.

## Фоны

Scene-нода с фоном:

```json
{
  "id": "scene_village_evening",
  "type": "scene",
  "label": "Деревня вечером",
  "background": "bg_village_evening",
  "music": "music_village_evening",
  "next": "dialogue_intro"
}
```

Поля:

```text
background        Sprite id
backgroundVideo   VideoClip id
backgroundGif     TextAsset id для .gif.bytes
backgroundOverlay Sprite id для оверлея
```

Как добавить обычный фон:

1. Импортируй PNG/JPG в Unity.
2. В import settings поставь:

```text
Texture Type: Sprite (2D and UI)
```

3. Назови ассет так же, как id в JSON:

```text
bg_village_evening.png
```

4. Добавь его в `StoryJsonAssetLibrary` как `Sprite` с id:

```text
bg_village_evening
```

Или положи ассет в `Resources`, если хочешь грузить через `Resources.Load`.

## Hot Reload фонов без перегенерации JSON

Теперь JSON не нужно перегенерировать, если меняется только ассет.

Например, в JSON уже есть:

```json
{
  "type": "scene",
  "background": "bg_village_evening"
}
```

Можно:

- заменить картинку `bg_village_evening.png`;
- добавить отсутствующий фон с таким именем;
- поменять ссылку `bg_village_evening` в `StoryJsonAssetLibrary`;
- заменить музыку с тем же id.

При входе в scene-ноду `StoryManager` заново ищет ассеты по сохраненным id:

```text
backgroundId
backgroundVideoId
backgroundGifId
backgroundOverlayId
musicId
startSfxId
```

Порядок поиска:

1. `StoryJsonAssetLibrary`.
2. fallback resolver.
3. `Resources`.
4. В Unity Editor поиск по имени/id в Project.

Важно: в билде editor-поиска по Project нет. Для билда ассеты должны быть:

- в `StoryJsonAssetLibrary`;
- или в `Resources`;
- или явно зацеплены другим asset reference.

JSON нужно менять только если меняется сам id:

```text
bg_village_evening -> bg_castle_night
```

## Музыка и SFX

Scene-нода с музыкой:

```json
{
  "id": "scene_forest",
  "type": "scene",
  "background": "bg_forest_day",
  "music": "music_forest_theme",
  "startSfx": "sfx_birds",
  "next": "dialogue_forest"
}
```

Как добавить музыку:

1. Импортируй `.ogg`, `.mp3` или `.wav`.
2. Назови AudioClip стабильным id:

```text
music_forest_theme
sfx_birds
```

3. Добавь в `StoryJsonAssetLibrary` как `AudioClip`.
4. Укажи id в JSON:

```json
"music": "music_forest_theme"
```

Рекомендуемые import settings:

Для длинной музыки:

```text
Load Type: Streaming
Compression Format: Vorbis
```

Для коротких SFX:

```text
Load Type: Decompress On Load
Compression Format: PCM или Vorbis
```

Музыка переключается только если новый клип отличается от текущего.

`startSfx` проигрывается один раз при входе в scene-ноду.

## StoryJsonAssetLibrary

Лучше всего все story-ассеты держать в `StoryJsonAssetLibrary`.

Туда добавляются:

```text
CharacterData
ClothingItem
Sprite
AudioClip
VideoClip
TextAsset
DialogueStyle
```

Пример:

```text
bg_village_evening -> Sprite
music_village_evening -> AudioClip
hero_outfit_black_dress -> ClothingItem
hero -> CharacterData
```

Так история будет стабильно работать и в Editor, и в билде.

## Проверка контента

В Unity запусти:

```text
Tools -> Novel Template -> Validate Content
```

Валидатор проверяет:

- пустые `ClothingItem.id`;
- дубли `ClothingItem.id`;
- одежду без sprite;
- `StoryData` без глав;
- графы без `StartNode`;
- дубли node guid;
- пустые wardrobe choice;
- premium choice с неправильной ценой;
- персонажей без базового sprite/body.

Перед релизом истории ошибки валидатора лучше исправлять обязательно.

## Чеклист новой истории

1. Создать `StoryData`.
2. Создать `ChapterData`.
3. Назначить JSON TextAsset или StoryGraph.
4. Назначить `StoryJsonAssetLibrary`.
5. Добавить в library всех персонажей.
6. Добавить все фоны.
7. Добавить музыку и SFX.
8. Создать все `ClothingItem`.
9. Добавить одежду и волосы в `WardrobeHeroSetupPage`.
10. Запустить `Tools -> Novel Template -> Validate Content`.
11. Проверить flow: имя -> wardrobe -> история.
