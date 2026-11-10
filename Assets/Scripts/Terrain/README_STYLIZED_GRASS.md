# 🌿 SMM - Stylized Grass - Гайд по использованию

## ⚠️ ВАЖНО: Требования

**SMM - Stylized Grass работает ТОЛЬКО с HDRP (High Definition Render Pipeline)!**

Если твой проект использует:
- ✅ **HDRP** - можешь использовать Stylized Grass
- ❌ **URP (Universal RP)** - Stylized Grass НЕ БУДЕТ РАБОТАТЬ
- ❌ **Built-in Pipeline** - Stylized Grass НЕ БУДЕТ РАБОТАТЬ

---

## 🎯 Два способа использования:

### **Способ 1: VFX Graph Mode (рекомендуется для HDRP)**
Используй VFX Graph префабы из Stylized Grass - они создают тысячи травинок очень эффективно.

### **Способ 2: Prefab Mode (универсальный)**
Используй обычные GameObject префабы - работает в любом pipeline, но менее производительно.

---

## 📋 Способ 1: VFX Graph Mode (ТОЛЬКО HDRP)

### **Шаг 1: Проверь что у тебя HDRP**
1. `Edit → Project Settings → Graphics`
2. Должно быть написано "HDRenderPipelineAsset"
3. Если нет - читай раздел "Как переключиться на HDRP" ниже

### **Шаг 2: Выбери VFX Grass префаб**
В `Assets/StylizedGrass/VFX Graphs/` есть два варианта:

**Grass/** - обычная трава:
- `quads-Circle.vfx` - квадратные травинки, круглая область
- `quads-Plane.vfx` - квадратные травинки, прямоугольная область ✅ **РЕКОМЕНДУЮ**
- `tris-Plane.vfx` - треугольные травинки, прямоугольная область

**Soft Grass/** - мягкая трава (с alpha blending):
- `quads-Circle.vfx`
- `quads-Plane.vfx` ✅ **КРАСИВО**
- `tris-Plane.vfx`

**Рекомендация:** Начни с `Grass/quads-Plane.vfx` или `Soft Grass/quads-Plane.vfx`

### **Шаг 3: Настрой ProceduralGrassGenerator**

1. Найди GameObject с компонентом **ProceduralGrassGenerator**
2. Установи параметры:

```
Generation Mode: VFXGraph

VFX Graph Mode Settings:
  - VFX Grass Prefab: [перетащи quads-Plane.vfx сюда]
  - VFX Area Size: (500, 500) [размер покрытия травой]

Common Settings:
  - World Size: 500 [должен совпадать с картой]
  
Distribution:
  - Clumpiness: 0 [для VFX не используется]
  - Noise Scale: 50
```

3. Запусти игру - трава появится как VFX эффект! 🎉

### **Шаг 4: Настрой параметры VFX (опционально)**

После создания VFX в сцене, выбери его и настрой в Inspector:

**Основные параметры:**
- **Capacity** - количество травинок (по умолчанию 50000)
- **Rate** - скорость спавна
- **Grass Color** - цвет травы
- **Wind Speed** - скорость ветра
- **Wind Strength** - сила ветра

---

## 📋 Способ 2: Prefab Mode (работает везде)

### **Вариант A: Создать простой префаб из Stylized Grass**

1. Открой сцену `Assets/StylizedGrass/Demo/Demo - Grass.unity`
2. Посмотри на примеры травы в Scene
3. Возьми один образец и сделай из него префаб
4. Используй этот префаб в `ProceduralGrassGenerator`

```
Generation Mode: Prefab

Prefab Mode Settings:
  - Grass Prefab: [твой созданный префаб]
  - Grass Count: 5000 [начни с малого]
```

### **Вариант B: Использовать SimpleGrass (наш собственный)**

Если VFX не работает, используй наш простой `SimpleGrass.cs`:

1. Создай пустой GameObject → "GrassPrefab"
2. Добавь компонент **SimpleGrass**
3. Настрой:
   - Grass Color: RGB(77, 179, 77)
   - Height: 0.5
   - Width: 0.3
4. Сделай префаб (перетащи в Assets/Prefabs/)
5. Укажи этот префаб в ProceduralGrassGenerator

---

## 🔧 Как переключиться на HDRP (если нужно)

⚠️ **ВНИМАНИЕ:** Переход на HDRP - серьёзное изменение! Сделай бэкап проекта!

### **Шаг 1: Установи HDRP пакет**
1. `Window → Package Manager`
2. Найди **"High Definition RP"**
3. Нажми **Install**

### **Шаг 2: Создай HDRP Asset**
1. `Assets → Create → Rendering → HDRP Asset`
2. Назови "HDRPSettings"

### **Шаг 3: Примени HDRP**
1. `Edit → Project Settings → Graphics`
2. В поле "Scriptable Render Pipeline Settings" перетащи созданный HDRPSettings

### **Шаг 4: Конвертируй материалы**
1. `Edit → Render Pipeline → HD Render Pipeline → Upgrade from Built-in pipeline`
2. Или: `Window → Rendering → Render Pipeline Converter`

---

## 🎨 Кастомизация Stylized Grass

### **Изменить цвет травы:**
1. Выбери VFX объект в сцене
2. Inspector → найди параметр "Grass Color"
3. Измени цвет

### **Изменить плотность:**
1. Параметр "Capacity" - общее количество травинок
2. Параметр "Rate" - скорость генерации

### **Настроить ветер:**
1. "Wind Speed" - скорость анимации
2. "Wind Strength" - амплитуда колебаний
3. "Wind Frequency" - частота порывов

### **Изменить размер травинок:**
1. Параметр "Scale" или "Size"
2. Можно задать Min/Max для вариации

---

## 🚀 Производительность

### **VFX Graph Mode:**
- ✅ **50,000+ травинок** - отличная производительность
- ✅ GPU Instancing встроен
- ✅ Автоматический culling (скрытие вне камеры)

### **Prefab Mode:**
- ⚠️ **5,000 объектов** - хорошо
- ⚠️ **10,000 объектов** - средне
- ❌ **20,000+ объектов** - лаги

**Рекомендация:** Если используешь HDRP - используй VFX Mode!

---

## 📝 Примечания

### **Network синхронизация:**
- VFX объекты **не синхронизируются** по сети (чисто визуальные)
- Каждый клиент создаёт свою траву с одним seed
- Это ХОРОШО - экономит трафик и производительность

### **Colliders:**
- Трава обычно **не имеет коллайдеров** (она декоративная)
- Если нужно - добавь их вручную на префабы

### **LOD (Level of Detail):**
- VFX Graph автоматически управляет LOD
- Для Prefab Mode можешь добавить LOD Group вручную

---

## 🎯 Рекомендуемая конфигурация

Для RTS игры рекомендую:

```
Если HDRP доступен:
  Mode: VFXGraph
  VFX: Soft Grass/quads-Plane.vfx
  Area Size: (500, 500)
  Capacity: 50000
  
Если HDRP недоступен:
  Mode: Prefab
  Prefab: SimpleGrass (наш)
  Count: 8000
  Randomization: включить
```

---

## 🔗 Полезные ссылки

- [GitHub документация](https://github.com/SirMishMash/Unity-StylizedGrass)
- [Asset Store страница](https://assetstore.unity.com/packages/3d/environments/smm-stylized-grass-184975)
- Demo сцена: `Assets/StylizedGrass/Demo/Demo - Grass.unity`

---

## ❓ FAQ

**Q: Трава не появляется!**
A: Проверь:
1. HDRP включен? (для VFX Mode)
2. VFX Prefab назначен в Inspector?
3. Area Size достаточно большой?
4. Камера смотрит в правильное место?

**Q: Трава слишком яркая/тёмная**
A: Измени параметр "Grass Color" в VFX Inspector

**Q: Трава не двигается от ветра**
A: Проверь параметры "Wind Speed" и "Wind Strength"

**Q: Лаги!**
A: 
1. Уменьши "Capacity" (количество травинок)
2. Используй "Grass/" вместо "Soft Grass/"
3. Уменьши "VFX Area Size"

---

Удачи с красивой травой! 🌿✨
