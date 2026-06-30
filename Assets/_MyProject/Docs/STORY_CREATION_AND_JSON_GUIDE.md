# Гайд по созданию истории и JSON v2

Этот проект поддерживает два способа создания главы:

- вручную в Unity через `VN/Story Creation Wizard` и xNode-граф;
- через JSON v2: JSON импортируется в `StoryGraph`, экспортируется обратно, может лежать в `ChapterData.jsonGraph` или приходить с сервера как `graphJson`.

JSON v2 обратно совместим с уже созданными JSON v1. Главное отличие v2 сейчас: `condition` умеет сравнивать два числовых ключа между собой через `comparison` + `compareVariableKey`.

Основной путь данных сейчас такой: `StoryData -> Chapters -> ChapterData -> StoryGraph/JSON`. `SeasonData` оставлен только для старых ассетов.

## Создание через Wizard

1. Открой `VN/Story Creation Wizard`.
2. Выбери `New story pack`, если создаёшь новую историю, или `Add chapter`, если добавляешь главу в существующую `StoryData`.
3. Заполни:
   - `Story name` и `Story ID`;
   - `Chapter name` и `Chapter ID`;
   - `Episode ID` - обычно тот же ID, который сервер использует для прогресса;
   - `Graph name`.
4. Нажми `Create story pack` или `Add chapter`.
5. Wizard создаст `StoryData`, `ChapterData`, `StoryGraph` и стартовые ноды `Start -> Scene -> Dialogue`.

Глава отображается в игре из списка `StoryData.chapters`. Если глава не добавлена туда, меню и запуск истории её не увидят.

## Импорт JSON

1. Положи `.json` в проект Unity так, чтобы он стал `TextAsset`.
2. Открой `VN/Story Creation Wizard`.
3. Переключись на `JSON tools`.
4. Укажи `Target StoryData`, если главу нужно сразу добавить в историю.
5. Укажи `Chapter JSON`.
6. Нажми `Validate Chapter JSON`, если хочешь проверить файл без создания ассетов.
7. Нажми `Import Chapter JSON`.

Импорт создаёт `StoryGraph` и `ChapterData`. Если включено `Assign JSON to ChapterData`, исходный JSON также будет записан в поле `ChapterData.jsonGraph`.

## Автоимпорт JSON в Unity

JSON v2 можно просто закинуть в любую папку внутри `Assets`. После импорта Unity автоматически проверит файл:

- если это обычный `package.json`, конфиг сети или другой не-story JSON, он будет проигнорирован;
- если это story JSON v1/v2 с полями `version` и `nodes`, будет создан `StoryGraph`;
- будет создан или обновлён `ChapterData`, где `graph` указывает на созданный граф, а `jsonGraph` указывает на сам JSON;
- если в проекте уже есть `StoryData` с таким же `storyId`, глава автоматически добавится в `StoryData.chapters`;
- если `StoryData` с таким `storyId` не найден, Unity создаст его автоматически.

Сгенерированные ассеты кладутся рядом с JSON в папку `StoryJsonGenerated/<storyId>/`:

```text
StoryJsonGenerated/
  <storyId>/
    <storyId>_Story.asset
    Chapters/<chapterId>.asset
    Graphs/<chapterId>_JsonGraph.asset
```

Если JSON уже был импортирован и ты поменяла файл, Unity пересоберёт авто-граф и обновит ссылку в `ChapterData`. Для ручного повторного запуска можно выделить JSON в Project и нажать `VN/Reimport Selected Story JSON`.

Автоимпорт пытается сам найти ближайший `StoryJsonAssetLibrary` рядом с JSON. Если библиотека не найдена, Editor всё равно попробует восстановить ассеты по GUID, пути, имени или `Resources.Load`.

### Логи автоимпорта

Автоимпорт пишет сообщения с префиксом `[StoryJsonAutoImporter]`.

Что будет видно в Console:

- старт импорта: путь к JSON, `storyId`, `chapterId`, `episodeId`;
- какая `StoryJsonAssetLibrary` выбрана, либо сообщение, что используется fallback resolver;
- папки, куда создаются `StoryData`, `ChapterData`, `StoryGraph`;
- путь созданного/обновлённого графа и количество нод;
- warning-и конвертера: отсутствующие ассеты, пустые ссылки, временные персонажи;
- `Debug.LogError`, если JSON похож на story JSON, но не является валидным story JSON;
- `Debug.LogException`, если Unity/AssetDatabase выбросили исключение во время автоимпорта.

Обычные JSON-файлы проекта, например `package.json` или сетевые конфиги, игнорируются без логов.

## Создание шаблона JSON

В `VN/Story Creation Wizard -> JSON tools` есть блок `JSON template`.

1. Заполни `Story ID`, `Chapter ID`, `Episode ID` и `Title`.
2. Нажми `Create Chapter JSON Template`.
3. Wizard сохранит готовый `.json` с базовой структурой `Start -> Scene -> Dialogue -> Choice`.
4. После сохранения шаблон автоматически подставится в поле `Chapter JSON`, его можно сразу валидировать или импортировать.

## Библиотека ассетов

Для runtime JSON есть `StoryJsonAssetLibrary` (`Create -> VN -> Story JSON Asset Library`). Это таблица строковых id из JSON в реальные Unity-ассеты.

Пример:

| JSON id | Поле в библиотеке |
| --- | --- |
| `hero` | `CharacterData` героини |
| `forest_evening` | `Sprite` фона |
| `forest_theme` | `AudioClip` музыки |
| `casual_dress` | `ClothingItem` одежды |

В `VN/Story Creation Wizard -> JSON tools` можно нажать `Create Empty JSON Asset Library`, заполнить библиотеку в инспекторе и указать её в поле `Asset Library`.

При импорте Wizard запишет библиотеку в `ChapterData`. При запуске игры `StoryManager` будет читать `ChapterData.jsonAssetLibrary` и резолвить ассеты по id из JSON. Если id не найден в библиотеке, resolver попробует fallback: в Editor поиск по GUID/пути/имени, в runtime `Resources.Load`.

Если персонаж из JSON не найден ни в `jsonAssetLibrary`, ни через fallback, игра не падает. Resolver пишет в консоль `Debug.LogError` с id персонажа и создаёт временный `CharacterData`, чтобы диалог мог продолжиться. Это аварийная защита, а не нормальный рабочий режим: после такого сообщения нужно добавить персонажа в `StoryJsonAssetLibrary` или положить `CharacterData` в `Resources` с нужным id.

Картинки, музыка, одежда, GIF, видео и стили диалогов при отсутствии пишут `Debug.LogWarning` и подставляются как `null`. UI должен просто пропустить отсутствующий ассет, а не ломать главу.

## Экспорт JSON

1. Открой `VN/Story Creation Wizard`.
2. Переключись на `JSON tools`.
3. Укажи `StoryGraph` или просто выдели граф в Project.
4. Нажми `Export Selected Graph JSON`.

Экспорт сохраняет JSON с теми же `id` нод и переходами. В Editor ссылки на ассеты экспортируются как Unity GUID, чтобы импорт мог восстановить их точно. На сервере можно использовать и обычные строковые id/пути.

## Runtime JSON

В `ChapterData` есть два источника графа:

- `graph` - обычный локальный `StoryGraph`;
- `jsonGraph` - `TextAsset` с JSON v1/v2.
- `jsonAssetLibrary` - опциональная библиотека id -> Unity asset для JSON.

При запуске `StoryManager` выбирает граф так:

1. если сервер отдал remote `graphJson`, он пробует его;
2. если в `ChapterData.jsonGraph` есть JSON, он пробует его;
3. если JSON битый или отсутствует, используется локальный `StoryGraph`.

Игра не падает при ошибке JSON: в консоль пишется `Debug.LogError`, затем происходит fallback на `ChapterData.graph`. Если ошибка только в отсутствующем персонаже, глава продолжит работу с временным персонажем и отдельным `Debug.LogError` по этому id.

## Серверный graphJson

Сервер может отдавать `graphJson` в формате JSON v2. Старый формат `scenes/nodes/choices.branch` тоже поддержан как legacy fallback, а JSON v1 продолжает работать как совместимый подформат.

Если глава использует `ChapterData.jsonAssetLibrary`, серверный `graphJson` тоже будет резолвить ассеты через эту библиотеку.

Минимальный ответ сервера может содержать:

```json
{
  "episodeId": "chapter_1",
  "contentVersion": "1",
  "graphJson": "{\"version\":2,\"chapterId\":\"chapter_1\",\"nodes\":[...]}"
}
```

## Формат JSON v2

Top-level поля:

| Поле | Тип | Описание |
| --- | --- | --- |
| `version` | number | `2` для нового формата. `1` всё ещё поддерживается для старых глав. |
| `storyId` | string | ID истории. |
| `chapterId` | string | ID главы. |
| `episodeId` | string | ID эпизода для сервера и прогресса. |
| `title` | string | Название главы. |
| `characters` | array | Опциональный список персонажей. |
| `nodes` | array | Ноды графа. |

Каждая нода обязана иметь:

| Поле | Тип | Описание |
| --- | --- | --- |
| `id` | string | Уникальный ID ноды. Он становится `BaseStoryNode.guid`. |
| `type` | string | Тип ноды. |
| `next` | string | Обычный переход по `exit`, если у типа нет специальных портов. |

## Пример главы

```json
{
  "version": 2,
  "storyId": "only_the_heart_sees_clearly",
  "chapterId": "chapter_1",
  "episodeId": "chapter_1",
  "title": "ГЛАВА 1: ПОДЛЕСЬЕ",
  "characters": [
    { "id": "hero", "name": "Алиса" },
    { "id": "ethan", "name": "Этан" }
  ],
  "nodes": [
    {
      "id": "start",
      "type": "start",
      "next": "scene_forest"
    },
    {
      "id": "scene_forest",
      "type": "scene",
      "label": "Подлесье. Сумерки.",
      "background": "forest_evening",
      "music": "forest_theme",
      "next": "dialogue_intro"
    },
    {
      "id": "dialogue_intro",
      "type": "dialogue",
      "activeCharacters": [
        { "character": "hero", "emotion": "Idle", "position": "Center" }
      ],
      "lines": [
        { "speaker": "hero", "emotion": "Thinking", "text": "Я не помню, как оказалась здесь..." }
      ],
      "next": "choice_path"
    },
    {
      "id": "choice_path",
      "type": "choice",
      "choicePrompt": "Куда пойти?",
      "choices": [
        { "text": "К старой тропе", "next": "stat_bravery" },
        { "text": "Остаться на месте", "next": "dialogue_wait" }
      ]
    },
    {
      "id": "stat_bravery",
      "type": "statChange",
      "statId": "bravery",
      "statDelta": 1,
      "statDisplayName": "Смелость",
      "next": "dialogue_end"
    },
    {
      "id": "dialogue_wait",
      "type": "dialogue",
      "lines": [
        { "speaker": "hero", "text": "Лучше сначала осмотреться." }
      ],
      "next": "dialogue_end"
    },
    {
      "id": "dialogue_end",
      "type": "dialogue",
      "lines": [
        { "speaker": "ethan", "emotion": "Serious", "text": "Ты опоздала." }
      ]
    }
  ]
}
```

## Типы нод

| `type` | Основные поля | Переходы |
| --- | --- | --- |
| `start` | нет | `next` |
| `scene` | `label`, `background`, `backgroundVideo`, `backgroundGif`, `backgroundOverlay`, `music`, `startSfx`, `suggestedBackground`, `suggestedMusic` | `next` |
| `dialogue` | `title`, `activeCharacters`, `lines` | `next` |
| `choice` | `title`, `activeCharacters`, `lines`, `choicePrompt`, `choices` | `choices[].next`, опционально `next` |
| `statChange` | `statId`, `statDelta`, `statDisplayName`, `systemMessage` | `next` |
| `variableChange` | `variableKey`, `deltaValue`, `add` | `next` |
| `condition` | `variableKey`, `requiredValue`; опционально `comparison`, `compareVariableKey` | `trueNext`, `falseNext` |
| `premium` | `cost` | `successNext`, опционально `failNext` |
| `camera` | `mode`, `targetPosition`, `xOffset`, `duration` | `next` |
| `image` | `image`, `video`, `gif`, `caption`, `description`, `zoomable` | `next` |
| `phoneDialogue` | `contactName`, `contactAvatar`, `typingDelay`, `messages` | `next` |
| `effect` | `effect`, `duration`, `intensity` | `next` |
| `appearanceChoice` | `promptText`, `singleExit`, `appearanceOptions` | если `singleExit = true`, то `next`; иначе `appearanceOptions[].next` |
| `wardrobeChoice` | `characterId`, `clothes` | `exits[]`, опционально `next` |
| `addClothing` | `clothing` | `next` |
| `openWardrobe` | нет | `next` |
| `wardrobeCheck` | `itemId` | `hasItemNext`, `noItemNext` |

## Статы, переменные и проверки

В JSON есть два типа нод, которые выглядят похоже:

```json
{ "type": "statChange", "statId": "respect", "statDelta": 1 }
```

```json
{ "type": "variableChange", "variableKey": "respect", "deltaValue": 1, "add": true }
```

Они оба работают с одним и тем же числовым хранилищем `GameState.stats`.

- `statChange` вызывает `GameState.AddStat(statId, statDelta)`.
- `variableChange` вызывает `GameState.SetInt(variableKey, value)`.
- `choice.requiredVariable` читает `GameState.GetInt(requiredVariable)`.
- `condition.variableKey` тоже читает `GameState.GetInt(variableKey)`.
- `condition.compareVariableKey`, если задан, читает второй ключ и сравнивает два значения напрямую.

То есть `statChange` с `statId: "respect"` и `choice.requiredVariable: "respect"` используют один и тот же ключ. Это работает:

```json
{
  "id": "respect_plus_001",
  "type": "statChange",
  "statId": "respect",
  "statDelta": 1,
  "statDisplayName": "Уважение",
  "systemMessage": "Уважение к вам выросло.",
  "next": "choice_respect"
}
```

```json
{
  "id": "choice_respect",
  "type": "choice",
  "choicePrompt": "Что сделать?",
  "choices": [
    {
      "text": "Попросить помощи у деревенских",
      "requiredVariable": "respect",
      "requiredValue": 3,
      "next": "village_help"
    },
    {
      "text": "Справиться самой",
      "next": "alone"
    }
  ]
}
```

Рекомендация:

- для видимых авторских статов используй `statChange`: `fairytale`, `city`, `respect`;
- для скрытых флагов, счётчиков и служебной логики используй `variableChange`;
- `requiredVariable` исторически называется variable, но фактически проверяет общий integer-ключ из `GameState.stats`.
- для развилок вида «больше Принципов / больше Чувств» используй `condition` с `comparison` и `compareVariableKey`, а не скрытый балансный счётчик.

Обычная проверка старого формата:

```json
{
  "id": "condition_respect",
  "type": "condition",
  "variableKey": "respect",
  "requiredValue": 3,
  "trueNext": "respect_branch",
  "falseNext": "default_branch"
}
```

Сравнение двух статов:

```json
{
  "id": "condition_principles_vs_feelings",
  "type": "condition",
  "variableKey": "principles",
  "comparison": "greaterThan",
  "compareVariableKey": "feelings",
  "trueNext": "principles_branch",
  "falseNext": "feelings_or_tie_branch"
}
```

Поддерживаемые значения `comparison`: `equals`, `notEquals`, `greaterThan`, `greaterOrEqual`, `lessThan`, `lessOrEqual`.

Пример основных статов этой истории:

| JSON key | Отображение |
| --- | --- |
| `fairytale` | Сказка |
| `city` | Город |
| `respect` | Уважение |

## Вложенные поля

`activeCharacters`:

```json
{ "character": "ethan", "emotion": "Happy", "position": "Left" }
```

`lines`:

```json
{ "speaker": "ethan", "emotion": "Serious", "text": "Текст реплики", "style": "style_id", "authorComment": "комментарий автора" }
```

`choices`:

```json
{ "text": "Ответ", "next": "node_id", "isPremium": false, "premiumCost": 0, "requiredVariable": "", "requiredValue": 0 }
```

`messages` для `phoneDialogue`:

```json
{ "text": "Сообщение", "side": "Incoming", "attachment": "photo_id" }
```

`appearanceOptions`:

```json
{ "label": "Европейская", "type": "European", "previewSprite": "preview_id", "next": "node_id" }
```

## Ошибки импорта

Импорт остановится, если:

- у ноды нет `id`;
- `id` повторяется;
- указан неизвестный `type`;
- `next`, `choices[].next`, `trueNext`, `falseNext`, `successNext`, `hasItemNext` или `noItemNext` ссылаются на несуществующую ноду;
- у выбора нет обязательного `next`.

Если ассет не найден, импорт продолжится с warning. Это удобно для серверного JSON: можно сначала собрать логику, а ассеты назначить позже в Unity.

## Тестирование главы

1. Проверь, что `StoryData.chapters` содержит нужный `ChapterData`.
2. Проверь, что у `ChapterData` есть `graph` или `jsonGraph`.
3. Запусти сцену игры и выбери историю в меню.
4. Если глава берётся с сервера, убедись, что `episodeId` совпадает с ID в каталоге.
5. При проблемах смотри Console: `StoryManager` пишет причину fallback на локальный граф.

## Чеклист переноса новой истории в JSON

Используй этот порядок для историй, которые не относятся к «Зорко лишь сердце».

1. Создай отдельный story pack через `VN/Story Creation Wizard -> New story pack`.
2. Выбери стабильные id:
   - `storyId` - один на всю историю;
   - `chapterId` - один на главу;
   - `episodeId` - серверный id прогресса и покупок.
3. Положи JSON рядом с папкой истории, чтобы автоимпорт нашёл ближайший `StoryJsonAssetLibrary`.
4. Заполни `<story_id>_JsonAssetLibrary.asset`: персонажи, фоны, катсцены, видео, музыку, одежду, прически, UI-плашки.
5. Проверь, что все `background`, `image`, `cutsceneImage`, `music`, `character`, `clothes` ссылаются на id из этой библиотеки.
6. Для гардероба добавь отдельную `WardrobeHeroSetupPage` и привяжи её через `Story Binding -> Story Ids`.
7. Прогони `Validate Chapter JSON`.
8. Импортируй JSON и проверь граф: старт, переходы, выборы, условия, гардероб, концовка.
9. Запусти историю в Play Mode и проверь первый вход, сейв, повторный вход, платные выборы и end screen.

### Арты «Привычка притворяться»

Все исходные арты этой истории сейчас лежат в:

```text
Assets/_MyProject/Art/Привычка притворяться/
```

Рекомендуемая раскладка id в `StoryJsonAssetLibrary`:

| Папка | Что добавлять | Пример id |
| --- | --- | --- |
| `Фоны` | обычные фоны, видеофоны, phone/sms assets | `pp_bg_bedroom_gg`, `pp_video_street_day` |
| `Кат-сцены` | CG/катсцены | `pp_cg_intro_phone` |
| `Персонажи` | `CharacterData` и эмоции персонажей | `pp_vlad`, `pp_mag`, `pp_remi` |
| `Главная героиня/Тело` | body/appearance sprites | `pp_hero_body_european` |
| `Главная героиня/Одежда` | `ClothingItem` с `type = Outfit` | `pp_outfit_ritm_goroda` |
| `Главная героиня/Прически` | `ClothingItem` с `type = Hair` | `pp_hair_silk_brown` |
| `Гардероб` | фон отдельного wardrobe screen | `pp_wardrobe_bg` |
| `Плашки` | dialogue/name/stat/end-screen UI sprites | `pp_ui_dialogue`, `pp_ui_stats` |

Префикс `pp_` не обязателен, но он сильно снижает риск пересечения id с другой историей.

### Story-specific wardrobe в JSON

JSON сам не выбирает визуальный экран гардероба. JSON описывает только событие:

```json
{
  "id": "pp_open_wardrobe",
  "type": "openWardrobe",
  "next": "after_wardrobe"
}
```

Экран выбирается в Unity по `WardrobeHeroSetupPage -> Story Binding`.

Для выборов одежды внутри главы используй `wardrobeChoice`:

```json
{
  "id": "pp_choose_outfit",
  "type": "wardrobeChoice",
  "characterId": "hero",
  "clothes": [
    "pp_outfit_ritm_goroda",
    "pp_outfit_ocharovanie"
  ],
  "optionRules": [
    {
      "premiumCost": 0,
      "purchaseKey": "pp_1.outfit.ritm_goroda"
    },
    {
      "premiumCost": 15,
      "purchaseKey": "pp_1.outfit.ocharovanie"
    }
  ],
  "next": "after_outfit"
}
```

Важно:

- `clothes[]` должен ссылаться на `ClothingItem`, а не напрямую на PNG.
- Бесплатные вещи тоже лучше указывать в `optionRules` с `premiumCost = 0`, чтобы структура была стабильной.
- `purchaseKey` должен быть уникальным и неизменным после релиза.
- Гардеробные id новой истории не должны повторять `zls_*`, `outfit_*`, `hair_*` из другой истории.

### Перед импортом JSON

Проверь документ/таблицу истории до конвертации:

- нет реплик с временными именами вроде `Героиня`, если уже нужен конкретный speaker id;
- у каждого выбора есть `next`;
- у каждой катсцены есть следующий обычный фон, если после неё должен вернуться диалог;
- `wardrobeChoice` одежды и волос идут подряд, если UX должен быть непрерывным;
- отношения/статы используют единые id по всей истории;
- все платные варианты имеют серверный `purchaseKey`.
