# ⛏️ Система здоровья ресурсов

## Что добавлено

Теперь **каждый ресурс** имеет **здоровье (HP)** -- требуется несколько ударов чтобы полностью добыть ресурс.

### ✅ Преимущества

- **Ресурсы служат дольше** -- не кончаются мгновенно
- **Партиклы при КАЖДОМ ударе** -- больше визуального фидбека
- **Ресурсы при КАЖДОМ ударе** -- плавное накопление
- **Настраиваемо** -- можно задать любое количество HP

---

## 🎯 Как настроить

### **Шаг 1: Открой префаб ресурса**

Например:
```
Assets/Prefabs/Terrain/Tree.prefab
Assets/Prefabs/Terrain/OreDeposit.prefab
Assets/Prefabs/Terrain/FarmPlot.prefab
```

### **Шаг 2: В Inspector найди компонент Resource**

#### **Параметры:**

```
Resource Settings:
├─ Resource Type: Ore           <- Тип ресурса
├─ Resource Amount: 50          <- ОБЩЕЕ количество ресурсов
└─ ...

Health Settings:
├─ Max Health: 3                <- Сколько ударов нужно
└─ Split Amount By Hits: [x]    <- Делить ресурсы на удары?
```

---

## ⚙️ Режимы работы

### **Режим 1: Split Amount By Hits = TRUE (рекомендуется)**

**Общее количество ресурсов делится на количество ударов.**

#### Пример:
```
Resource Amount: 50
Max Health: 3

Удар 1: +17 ресурсов (50/3 = 16.66 -> 17)
Удар 2: +17 ресурсов
Удар 3: +17 ресурсов (ресурс уничтожается)

ИТОГО: 51 ресурс (из-за округления вверх)
```

**Плюсы:**
- ✅ Баланс не ломается
- ✅ Ресурсов столько же, просто добывать дольше
- ✅ Можно регулировать скорость добычи через Max Health

---

### **Режим 2: Split Amount By Hits = FALSE (OP)**

**ПОЛНОЕ количество ресурсов даётся при КАЖДОМ ударе.**

#### Пример:
```
Resource Amount: 50
Max Health: 3

Удар 1: +50 ресурсов
Удар 2: +50 ресурсов
Удар 3: +50 ресурсов (ресурс уничтожается)

ИТОГО: 150 ресурсов!!!
```

**Когда использовать:**
- Cheat mode
- Debug режим для быстрого тестирования
- Специальные "богатые" месторождения

---

## 📊 Рекомендуемые настройки

### **Деревья (Wood):**
```
Resource Amount: 50
Max Health: 3
Split Amount By Hits: [x]

Результат: 3 удара, ~17 дерева за удар
```

### **Руда (Ore):**
```
Resource Amount: 40
Max Health: 5
Split Amount By Hits: [x]

Результат: 5 ударов, 8 руды за удар
```

### **Еда (Food):**
```
Resource Amount: 30
Max Health: 2
Split Amount By Hits: [x]

Результат: 2 удара, 15 еды за удар
```

### **Богатая руда (редкая):**
```
Resource Amount: 100
Max Health: 10
Split Amount By Hits: [x]

Результат: 10 ударов, 10 руды за удар
```

---

## 🎮 Как это работает в игре

### **Процесс добычи:**

1. **Worker подходит к ресурсу**
2. **Начинает добычу** (анимация, таймер)
3. **Удар по ресурсу:**
   - ✅ Воспроизводятся партиклы
   - ✅ Worker получает ресурсы
   - ✅ HP ресурса уменьшается на 1
4. **Если HP > 0:**
   - Повторяется шаг 2 (Worker бьёт снова)
5. **Если HP = 0:**
   - Ресурс уничтожается
   - Worker ищет новый ресурс

### **Визуальный фидбек:**

- Партиклы играются **при КАЖДОМ ударе**
- Worker не отходит от ресурса между ударами
- Ресурсы поступают **постепенно**

---

## 🔧 Быстрая настройка всех ресурсов

### **Вариант 1: Вручную**

Открой каждый префаб и настрой `Max Health`.

### **Вариант 2: Через скрипт (быстро)**

Создай временный скрипт:

```csharp
using UnityEngine;
using RTSokak.Resources;

public class SetResourceHealth : MonoBehaviour
{
    [ContextMenu("Set All Resource Health")]
    void SetHealth()
    {
        // Найти все Resource в сцене
        Resource[] resources = FindObjectsByType<Resource>(FindObjectsSortMode.None);
        
        foreach (var res in resources)
        {
            // Используем рефлексию чтобы установить приватные поля
            var field = typeof(Resource).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
                
            if (field != null)
            {
                switch (res.Type)
                {
                    case ResourceType.Wood:
                        field.SetValue(res, 3);
                        break;
                    case ResourceType.Ore:
                        field.SetValue(res, 5);
                        break;
                    case ResourceType.Food:
                        field.SetValue(res, 2);
                        break;
                }
            }
        }
        
        Debug.Log($"Set health for {resources.Length} resources");
    }
}
```

**Использование:**
1. Создай пустой GameObject в сцене
2. Добавь этот скрипт
3. Right Click -> Set All Resource Health
4. Готово!

---

## 💡 Дополнительные идеи

### **Визуальная деградация:**

Можно добавить визуальное изменение ресурса при снижении HP:

```csharp
// В Resource.cs, метод Harvest():
// После CurrentHealth--;

float healthPercent = (float)CurrentHealth / maxHealth;

if (healthPercent <= 0.66f && healthPercent > 0.33f)
{
    // Средний урон -- слегка потемнить
    foreach (var r in GetComponentsInChildren<Renderer>())
        r.material.color = Color.Lerp(Color.white, Color.gray, 0.3f);
}
else if (healthPercent <= 0.33f)
{
    // Критическое состояние -- сильно потемнить
    foreach (var r in GetComponentsInChildren<Renderer>())
        r.material.color = Color.Lerp(Color.white, Color.gray, 0.6f);
}
```

### **Разные партиклы в зависимости от HP:**

```csharp
// В ResourceManager.PlayHarvestParticles():
// Добавить перегрузку с параметром healthPercent

public void PlayHarvestParticles(ResourceType type, Vector3 position, float healthPercent)
{
    if (healthPercent <= 0.33f)
    {
        // Финальный удар -- больше партиклов
        // ...
    }
}
```

### **Прочность инструмента:**

Worker'у тоже можно добавить износ инструмента:

```csharp
[SerializeField] private int toolDurability = 100;
private int currentToolDurability;

// При каждом ударе:
currentToolDurability--;
if (currentToolDurability <= 0)
{
    // Инструмент сломался -- вернуться на базу за новым
}
```

---

## 🐛 Troubleshooting

### **Ресурсы всё равно кончаются быстро:**

Увеличь `Max Health` до 5-10 в префабе.

### **Worker не бьёт второй раз:**

Проверь что `Split Amount By Hits = TRUE` (иначе баг с claim).

### **Партиклы не играются:**

Проверь что `ResourceManager` существует в сцене и имеет партиклы.

### **Worker застревает после первого удара:**

Убедись что обновлён `Worker.cs` (метод `CompleteHarvesting`).

---

**Готово!** Теперь ресурсы служат дольше! ⛏️🌲⚒️
