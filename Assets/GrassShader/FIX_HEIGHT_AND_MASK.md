# 🔧 ИСПРАВЛЕНИЕ: Трава огромная и в рандомных местах

## ✅ ЧТО ИСПРАВЛЕНО

### 1. **Правильные имена параметров shader**
Было: `_GrassHeight`, `_GrassWidth`  
Стало: `_BladeHeight`, `_BladeWidth` ← правильные имена!

### 2. **Реалистичные значения по умолчанию**
```
Grass Height: 0.3 м (было 0.5)
Grass Width: 0.05 м (было 0.1)
Grass Density: 10 (было 15)
```

### 3. **Отключена маска травы**
`_GrassMaskThreshold = 0` → трава растёт **везде равномерно**

---

## 🎯 КАК ПРИМЕНИТЬ ИСПРАВЛЕНИЯ

### Вариант 1: Пересоздать GrassSystem (БЫСТРО)

1. **Hierarchy → Delete старый GrassSystem**

2. **Create Empty → "GrassSystem"**

3. **Add Component → RTSGrassManager**

4. **Inspector:**
   - Grass Material → `GeometryGrass.mat`
   - Grass Plane Prefab → `GroundPlane.fbx`

5. **Play** — трава будет нормального размера!

---

### Вариант 2: Обновить вручную (если не хочешь пересоздавать)

В Unity **Inspector → GeometryGrass Material:**

1. **Уменьши высоту:**
   ```
   Blade Height: 0.3 (вместо 0.5-1.0)
   ```

2. **Уменьши ширину:**
   ```
   Blade Width: 0.05 (вместо 0.1)
   ```

3. **Отключи маску:**
   ```
   Grass Mask Threshold: 0 (было 0.1-1.0)
   ```

4. **Уменьши плотность:**
   ```
   Tessellation Uniform: 10 (вместо 15-20)
   ```

---

## 📊 ПРАВИЛЬНЫЕ ЗНАЧЕНИЯ ДЛЯ RTS

### Low (слабые ПК)
```
Blade Height: 0.25
Blade Width: 0.04
Tessellation Uniform: 8
```

### Medium (рекомендовано)
```
Blade Height: 0.3
Blade Width: 0.05
Tessellation Uniform: 10
```

### High (мощные ПК)
```
Blade Height: 0.4
Blade Width: 0.06
Tessellation Uniform: 15
```

---

## 🎨 ПОЧЕМУ ТРАВА БЫЛА В РАНДОМНЫХ МЕСТАХ?

**Grass Mask** — текстура которая определяет где растёт трава:
- **Белые области** → трава есть
- **Чёрные области** → травы нет
- **Threshold = 0.1** → только в белых областях

**Решение:**
```
Grass Mask Threshold = 0
```
Теперь трава **игнорирует маску** и растёт везде равномерно!

---

## 🐛 ЕСЛИ ВСЁ ЕЩЁ ПРОБЛЕМЫ

### Трава всё ещё огромная?
**Проверь в Material:**
- `Blade Height` должен быть **0.3** (НЕ 0.5-1.0!)
- `Blade Width` должен быть **0.05** (НЕ 0.1!)

### Трава всё ещё в рандомных местах?
**Проверь в Material:**
- `Grass Mask Threshold` должен быть **0**
- Или назначь белую текстуру в `Grass Mask`

### grassHeight в Inspector не работает?
**Причина:** Старая версия скрипта с неправильными именами  
**Решение:** Пересоздай GrassSystem (см. Вариант 1)

---

## ✅ ПРОВЕРКА ЧТО ВСЁ ПРАВИЛЬНО

В Material Inspector должно быть:
```
✅ Blade Height: ~0.3
✅ Blade Width: ~0.05  
✅ Tessellation Uniform: ~10
✅ Grass Mask Threshold: 0
✅ Wind Strength: 0.2-0.8
```

В Game View:
```
✅ Трава невысокая (ниже юнитов)
✅ Трава везде равномерно
✅ Плавное колыхание
✅ FPS >= 60
```
