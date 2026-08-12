FIX: CURRENT SAVE CLICK MUST OPEN CONFIRMATION

Причина предыдущего бага:
определение "это текущий save" было ошибочно связано с CanCreateBranchFromCurrent(),
а тот требовал живой GameState.currentNode. Если runtime в этот момент не считался
saveable, клик по выбранному слоту проваливался в StartSlot() и сразу загружал историю.

Теперь:
- текущий слот определяется ТОЛЬКО через StorySaveSlotSelection;
- клик по текущему слоту всегда открывает подтверждение и карточку этого save;
- клик по другому существующему слоту по-прежнему загружает его;
- "Новое сохранение" по-прежнему создаёт новый save сразу без подтверждения;
- при подтверждении overwrite runtime snapshot дополнительно проверяется на совпадение storyId,
  чтобы нельзя было случайно записать состояние другой истории в выбранный слот.

Диагностические логи:
[SAVE][CARD_CLICK]
[SAVE][OVERWRITE_CONFIRM_OPEN]
[SAVE][OVERWRITE_REJECTED]
[SAVE][OVERWRITE_SUCCESS]
