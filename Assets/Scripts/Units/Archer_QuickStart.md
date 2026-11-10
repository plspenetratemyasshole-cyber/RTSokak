# Archer Quick Start

## Что я сделал:

1. ✅ **Arrow.cs** — скрипт стрелы с предиктивным попаданием
2. ✅ **Archer.cs** — скрипт лучника с NavMesh-предсказанием траектории цели
3. ✅ **RTSSetupHelper** — добавлена кнопка "13. Spawn Archer" для быстрого спавна
4. ✅ **Archer_Setup.md** — полная инструкция по настройке

## Что нужно сделать ТЕБЕ:

### 1. Создать префаб Arrow (Стрела)

```
GameObject → Create Empty → Назвать "Arrow"
```

**Компоненты на Arrow:**
- `Arrow (Script)` — главный скрипт
- `NetworkIdentity` — обязательно!
- `NetworkTransform` (опционально)

**Параметры Arrow:**
- Speed = 15
- Damage = 20
- Hit Radius = 0.5
- Max Lifetime = 10

**Визуал стрелы (child GameObject):**
- Создай Cylinder как child
- Поверни чтобы он был направлен по оси Z
- Масштаб: (0.05, 0.05, 0.5)
- Перетащи в поле "Arrow Mesh" компонента Arrow

**Particles (опционально):**
- Создай Particle System для промаха
- Сохрани как отдельный префаб
- Перетащи в поле "Miss Particles Prefab"

**Сохрани Arrow как префаб** в папку Prefabs!

### 2. Добавить Arrow в NetworkManager

1. Открой сцену с NetworkManager
2. Найди NetworkManager объект
3. В Inspector → "Spawnable Prefabs" → добавь Arrow префаб
4. **ВАЖНО**: Без этого стрелы не будут создаваться по сети!

### 3. Создать префаб Archer (Лучник)

**У тебя уже есть скопированный Warrior префаб Archer!**

Открой его в Inspector:

1. **УДАЛИ** компонент `Warrior` (если есть)
2. **ДОБАВЬ** компонент `Archer`
3. **Настрой параметры Archer:**
   - Arrow Prefab → перетащи Arrow префаб
   - Shoot Range = 12
   - Arrow Speed = 15
   - Arrow Damage = 20
   - Shoot Cooldown = 2
   - Obstacle Mask → выбери слои препятствий (Buildings, Terrain)
   
4. **ToolHoldPoint:**
   - Найди child GameObject "ToolHoldPoint" (из Warrior)
   - Или создай новый Empty GameObject как child
   - Позиция примерно (0.5, 1.0, 0.3) — в руке
   - Перетащи ToolHoldPoint в поле "Tool Hold Point" Archer
   - **ВАЖНО**: Сюда нужно добавить визуал арбалета как child!

5. **Арбалет (crossbow визуал):**
   - Создай или импортируй модель арбалета
   - Поставь его как child ToolHoldPoint
   - Настрой позицию/поворот чтобы лучник держал его

6. **Сохрани префаб**

### 4. Добавить Archer в NetworkManager (если нужно спавнить через код)

Если будешь спавнить Archer через NetworkServer.Spawn, добавь его в Spawnable Prefabs тоже.

### 5. Тестирование

**В Unity Editor:**
1. Запусти игру как Host
2. Открой `Tools → RTS Setup Helper`
3. Нажми кнопку **"13. Spawn Archer"**
4. Лучник появится в мире
5. Спавни врага (Warrior противника)
6. Выдели лучника
7. ПКМ по врагу
8. Лучник должен подойти и начать стрелять!

### 6. Что проверить:

✅ **Лучник подходит** если цель далеко
✅ **Останавливается на дистанции** shootRange
✅ **Стрела летит** по прямой к предсказанной позиции
✅ **Попадание** = урон + красный пульс + откидывание
✅ **Промах** (если цель резко изменила курс) = партиклы
✅ **Препятствия** = лучник не стреляет через стены
✅ **Движущаяся цель** = стрела предсказывает траекторию

## Настройка параметров

Если нужно изменить баланс:

- **Дальность стрельбы**: Archer → Shoot Range (12м по умолчанию)
- **Скорость стрелы**: Archer → Arrow Speed (15 м/с)
- **Урон**: Archer → Arrow Damage (20)
- **Cooldown**: Archer → Shoot Cooldown (2 секунды)
- **Радиус попадания**: Arrow → Hit Radius (0.5м)
- **HP лучника**: Archer → Starting Max Health (80)

## Дополнительно

- Добавь Trail Renderer на стрелу для эффекта трассировки
- Добавь анимацию натягивания тетивы
- Добавь звук выстрела в RpcShootVisual
- Добавь партиклы попадания (кровь/искры)

## Подробная инструкция

Смотри **Archer_Setup.md** для полной информации!
