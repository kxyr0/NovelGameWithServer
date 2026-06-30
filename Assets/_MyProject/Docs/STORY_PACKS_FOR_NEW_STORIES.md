# Story Packs для новых историй

Эта структура нужна для историй, которые не связаны с «Зорко лишь сердце» и живут как отдельный пакет данных.

## Быстрый путь

1. Открой `VN/Story Creation Wizard`.
2. Выбери `New story pack`.
3. Заполни `Story name`, `Story ID`, `Chapter name`, `Chapter ID`, `Episode ID`.
4. Оставь включенными:
   - `Create asset library`;
   - `Create GameData`;
   - `Add to Game Catalog`.
5. Нажми `Create story pack`.

Wizard создаст отдельную папку:

```text
Assets/_MyProject/Data/Stories/<story_id>/
  <story_id>_Story.asset
  <story_id>_JsonAssetLibrary.asset
  Audio/
  Backgrounds/
  Characters/
  Chapters/
  Cutscenes/
  Graphs/
  Json/
  Menu/
  UI/
```

`GameData` сразу добавляется в `Assets/_MyProject/Data/Games/Game Catalog.asset`, поэтому новая история появляется в меню без ручного перетаскивания.

## Правила для независимых историй

- У каждой истории должен быть уникальный `storyId`.
- У каждой главы должен быть уникальный `chapterId` и `episodeId`.
- Все ассеты конкретной истории лучше хранить внутри ее папки.
- Для JSON-историй используй отдельный `<story_id>_JsonAssetLibrary.asset`: туда добавляются персонажи, фоны, катсцены, музыка, одежда и UI-стили этой истории.
- Не переиспользуй id из другой истории, если это не общий системный ассет.

## Отдельный экран гардероба для истории

Новая история может использовать свой экран гардероба без правок кода и без подмены старых ассетов.

1. Оставь общий экран навигации `Wardrobe` в `StoryScreenNavigator`.
2. Внутри `wardrobePanel` создай или продублируй отдельный объект с `WardrobeHeroSetupPage`.
3. В блоке `Story Binding` у этой страницы заполни:
   - `Story Ids` - `storyId` новой истории;
   - `Chapter Ids` - опционально, если экран нужен только для конкретной главы/episode;
   - `Use As Fallback For Unmatched Stories` выключи на story-specific странице.
4. На старой общей странице гардероба оставь пустые `Story Ids` и включённый `Use As Fallback For Unmatched Stories`.
5. В новые `Appearance Options`, `Outfit Items`, `Hair Items`, `Default Outfit Item`, `Default Hair Item` назначай ассеты только этой истории.

Runtime выбирает страницу так: точное совпадение `storyId + chapterId`, затем `storyId`, затем `chapterId`, затем fallback-страница. Это защищает от ситуации, когда новая история случайно открывает старый гардероб.

Для истории **«Привычка притворяться»** исходные арты лежат здесь:

```text
Assets/_MyProject/Art/Привычка притворяться/
  Гардероб/
  Главная героиня/
  Плашки/
  Фоны/
  Кат-сцены/
  Персонажи/
```

Экран гардероба этой истории должен брать фон из `Гардероб`, тело/одежду/прически из `Главная героиня`, а UI-плашки из `Плашки`.

## UI-профиль истории

Если у новой истории должен быть свой диалоговый фрейм или отдельный root гардероба, настрой это на `StoryManager`, не через правку кода:

1. Открой `StoryManager` в сцене.
2. В блоке `Story UI Profiles` добавь новый элемент.
3. Заполни `Story Asset` или `Story Ids` значением `storyId` новой истории.
4. В `Dialogue User Interface` назначь `Dialogue UI` с нужным диалоговым окном.
5. В `Dialogue User Interface -> Cutscene User Interface` назначь UI для катсцен, если он отличается.
6. В `Wardrobe User Interface -> Wardrobe Panel` назначь root гардероба этой истории, если он отдельный.
7. Если root гардероба не нужен, можно назначить `Wardrobe User Interface -> Setup Page` и включить `Use Setup Page Root When Panel Empty`.
8. В `Dialogue User Interface -> Enable When Selected` и `Disable When Selected` можно положить story-specific UI-объекты, чтобы они включались только для своей истории.

Если профиль не найден, игра использует дефолтные ссылки `StoryManager`: старый `DialogueUIManager`, старый cutscene UI и fallback wardrobe.

## JSON

Шаблон главы можно создать через `VN/Story Creation Wizard -> JSON tools -> Create Chapter JSON Template`.

Если JSON лежит внутри папки истории, автоимпорт найдет ближайшую `StoryJsonAssetLibrary` и привяжет ее к `ChapterData`. Если библиотека не найдена, импорт не упадет, но ассеты придется назначить позже.

## Добавление главы

Для новой главы выбери `Add chapter`, укажи нужный `StoryData`, и Wizard положит главу в ту же папку истории. Ближайшая `StoryJsonAssetLibrary` будет назначена автоматически.
