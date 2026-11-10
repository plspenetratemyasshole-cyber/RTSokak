# QuickOutline + HDRP

## Главный фикс

HDRP рисует transparent только в диапазоне **2900–3100**.
Старый QuickOutline ставил Fill в `Transparent+110` (= **3110**) — HDRP молча не рисовал outline.

Сейчас:
- Mask → queue **3010**
- Fill → queue **3020**
- `Outline.cs` принудительно выставляет `renderQueue`

## Что сделать

1. Дождись реимпорта / перезапусти Play Mode
2. Выбери юнита — должен быть зелёный outline
