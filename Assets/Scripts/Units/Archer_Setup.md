# Archer Setup Instructions

## Обзор
Archer (Лучник) — дальнобойный юнит с предиктивной стрельбой. Стреляет стрелами по движущимся целям, предсказывая их будущую позицию с учётом NavMesh.

## Файлы
- `Assets/Scripts/Network/Archer.cs` — скрипт лучника
- `Assets/Scripts/Units/Arrow.cs` — скрипт стрелы (снаряд)

## Настройка префаба Archer

### 1. Базовая настройка
- Используй существующий Warrior префаб как основу (ты уже скопировал его)
- **УДАЛИ** компонент `Warrior` если он есть
- **ДОБАВЬ** компонент `Archer`
- **ОБЯЗАТЕЛЬНО** добавь компонент `NetworkIdentity` если его нет

### 2. Параметры Archer в Inspector

#### Display Name
- `displayName = "Archer"` (имя для nameplate)

#### Ranged Combat
- `Arrow Prefab` — перетащи префаб стрелы (создай его, см. ниже)
- `Shoot Range = 12` (дальность стрельбы в метрах, настраивай по вкусу)
- `Arrow Speed = 15` (скорость полёта стрелы м/с)
- `Arrow Damage = 20` (урон стрелы)
- `Shoot Cooldown = 2` (время между выстрелами в секундах)
- `Obstacle Mask` — выбери слои препятствий (стены, деревья и т.д.) для проверки линии видимости

#### Combat (из RTSUnit)
- `Auto Attack Search Range = 80` (радиус автоматического поиска врагов после убийства цели)

#### Tool Hold Point
- `Tool Hold Point` — перетащи ChildObject который держит арбалет (как у Worker топор/кирка)
  - Создай Empty GameObject как child Archer
  - Назови его "ToolHoldPoint"
  - Позиция: примерно `(0.5, 1.0, 0.3)` (в руке лучника)

#### Health
- `Starting Max Health = 80` (HP лучника, немного меньше чем у воина)

#### Outline Colors
- `Selected Outline Color` — зелёный для своих выбранных юнитов
- `Enemy Outline Color` — красный для врагов

## Настройка префаба Arrow (Стрела)

### 1. Создай новый GameObject
- `GameObject -> Create Empty`
- Назови его "Arrow"

### 2. Добавь компоненты
- **Arrow (Script)** — главный скрипт
- **NetworkIdentity** — обязательно для сетевой синхронизации
- **NetworkTransform** (опционально, но рекомендуется)

### 3. Параметры Arrow в Inspector

#### Arrow Properties
- `Speed = 15` (скорость полёта, должна совпадать с Archer.Arrow Speed)
- `Damage = 20` (урон при попадании)
- `Hit Radius = 0.5` (радиус попадания в метрах, увеличь если нужно)
- `Max Lifetime = 10` (максимальное время жизни стрелы в секундах)

#### Visual
- `Arrow Mesh` — перетащи визуальную модель стрелы (child GameObject с MeshRenderer)
  - Создай примитив Cylinder или используй свою модель
  - Поверни его так чтобы он был направлен вперёд по оси Z
  - Масштаб примерно `(0.05, 0.05, 0.5)` для Cylinder

#### Particles
- `Miss Particles Prefab` — префаб партиклов когда стрела пропадает без попадания
  - Создай Particle System (например искры/пыль)
  - Сохрани как префаб
  - Перетащи сюда

### 4. Сохрани как префаб
- Перетащи Arrow GameObject в папку Prefabs
- **ВАЖНО**: Arrow должен быть в папке Resources или зарегистрирован в NetworkManager для спавна

## Настройка NetworkManager

### Добавь Arrow в список спавнящихся префабов:
1. Открой сцену с NetworkManager
2. Найди объект NetworkManager
3. В Inspector найди секцию "Spawnable Prefabs"
4. Добавь Arrow префаб в список

## Регистрация Archer в RTSSetupHelper (опционально)

Если хочешь кнопку быстрого спавна лучника в Helper:

```csharp
// В RTSSetupHelper.cs добавь новую кнопку:

[Button("9. Spawn Archer", ButtonSizes.Large)]
private void SpawnArcher()
{
    if (!NetworkServer.active)
    {
        UnityEngine.Debug.LogWarning("NetworkServer is not active");
        return;
    }

    var player = Object.FindFirstObjectByType<RTSPlayer>();
    if (player == null)
    {
        UnityEngine.Debug.LogWarning("No RTSPlayer found");
        return;
    }

    // Загрузи Archer префаб
    GameObject archerPrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Archer");
    if (archerPrefab == null)
    {
        UnityEngine.Debug.LogWarning("Archer prefab not found in Resources/Prefabs/");
        return;
    }

    Vector3 spawnPos = new Vector3(5, 1, 5);
    GameObject archerObj = Object.Instantiate(archerPrefab, spawnPos, Quaternion.identity);
    NetworkServer.Spawn(archerObj);

    Archer archer = archerObj.GetComponent<Archer>();
    if (archer != null)
    {
        archer.SetOwner(player.connectionToClient);
        player.AddCurrentPopulation(1);
    }
}
```

## Тестирование

1. **Запусти игру как Host**
2. **Спавни Archer** через Helper или вручную
3. **Спавни цель** (Warrior противника)
4. **Выдели лучника** и **кликни правой кнопкой по цели**
5. **Лучник должен:**
   - Подойти если цель далеко
   - Остановиться на дистанции `shootRange`
   - Предсказать будущую позицию движущейся цели
   - Выстрелить стрелой
   - Стрела летит по прямой к предсказанной точке
   - При попадании: урон + красный пульс + откидывание
   - При промахе (если цель резко изменила курс): партиклы на месте исчезновения

## Проверка линии видимости

Лучник не будет стрелять если между ним и целью есть препятствия (стены, деревья).
Настрой `Obstacle Mask` правильно:
- Включи слои: Buildings, Terrain, Trees
- Исключи слои: Units, Ground

## Параметры для настройки

### Дальность
- `shootRange` — больше значение = стреляет издалека
- Рекомендуется 10-15 метров

### Скорость стрелы
- `arrowSpeed` — больше значение = стрела летит быстрее = проще попасть
- Рекомендуется 12-20 м/с

### Cooldown
- `shootCooldown` — меньше значение = стреляет чаще
- Рекомендуется 1.5-3 секунды

### Урон
- `arrowDamage` — урон за выстрел
- Warrior HP = 100, поэтому 20 урона = 5 выстрелов на убийство

### Радиус попадания
- `hitRadius` в Arrow — больше значение = проще попасть
- 0.5м = средняя сложность
- 1.0м = легко попасть
- 0.3м = сложно попасть

## Известные особенности

1. **Предсказание работает точно** если цель просто идёт по NavMesh
2. **Если игрок СРАЗУ ПОСЛЕ ВЫСТРЕЛА** отдаёт цели команду MoveTo в другое место:
   - Цель резко меняет направление
   - Стрела летит в старую предсказанную точку
   - Стрела пропадает через `predictedHitTime + 0.3s` с партиклами
3. Лучник **может двигаться во время стрельбы** (в отличие от Warrior)
4. Лучник **сам найдёт следующую цель** после убийства текущей (как Warrior)

## Визуальные улучшения (опционально)

- Добавь анимацию натягивания тетивы в `RpcShootVisual`
- Добавь звук выстрела
- Добавь trail renderer на стрелу для эффекта трассировки
- Добавь партиклы попадания (кровь/искры) в Arrow.CheckHit
