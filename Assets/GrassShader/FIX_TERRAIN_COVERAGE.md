# 🔧 ПРАВИЛЬНАЯ НАСТРОЙКА ТРАВЫ НА СУЩЕСТВУЮЩИЙ TERRAIN

## ❌ ТЕКУЩАЯ ПРОБЛЕМА

На скрине видно:
- **Зелёный фон** = твой основной Plane без травы
- **Диагональная полоса с травой** = маленький GroundPlane поверх
- **Земля под травой** = текстура из материала

**Проблема:** Создаётся ВТОРОЙ plane вместо применения травы на СУЩЕСТВУЮЩИЙ!

---

## ✅ ПРАВИЛЬНОЕ РЕШЕНИЕ

### Вариант 1: Применить траву на существующий Plane (РЕКОМЕНДУЕТСЯ)

1. **Hierarchy → найди "Plane"** (твой основной terrain)

2. **Inspector → Mesh Renderer → Materials:**
   - Drag `Assets/GrassShader/Materials/GeometryGrass.mat`

3. **❗ВАЖНО: Заменить Mesh!**
   
   Твой Plane имеет **4 вершины** → огромные треугольники!
   
   **Нужно:**
   - Project → `Assets/GrassShader/Models/GroundPlane.fbx`
   - Drag в **Hierarchy** (создастся новый объект)
   - **Delete старый Plane**
   - Переименуй GroundPlane → `Plane`
   - Настрой Position/Scale под твою карту

---

### Вариант 2: Увеличить GroundPlane чтобы покрыть всю карту

Если уже создал через RTSGrassManager:

1. **Hierarchy → GrassSystem → GrassPlane**

2. **Inspector → Transform:**
   ```
   Position: (0, 0, 0)  // Центр карты
   Scale: (100, 1, 100) // Под размер карты (у тебя ~1000x1000)
   ```

3. **Delete старый зелёный Plane** из Hierarchy

---

## 🎯 ПОШАГОВАЯ ИНСТРУКЦИЯ (С НУЛЯ)

### 1. Очистить сцену

```
Hierarchy → Delete:
- GrassSystem (если есть)
- Старый Plane (зелёный)
```

### 2. Создать правильную траву

**A. Добавить GroundPlane в сцену:**
```
Project → Assets/GrassShader/Models/GroundPlane.fbx
Drag в Hierarchy
```

**B. Настроить Transform:**
```
Position: (0, 0, 0)
Rotation: (0, 0, 0)
Scale: (50, 1, 50)  // Начни с 50, потом увеличь
```

**C. Применить материал:**
```
Inspector → Mesh Renderer → Materials:
Drag: Assets/GrassShader/Materials/GeometryGrass.mat
```

**D. Настроить Material:**
```
Blade Height: 0.2-0.3
Blade Width: 0.04-0.05
Tessellation Uniform: 8-10
Grass Mask Threshold: 0
Wind Strength: 0.5
```

### 3. Убрать землю под травой

В Material:
```
Ground Texture: Назначь ЗЕЛЁНУЮ текстуру
Или создай простую зелёную текстуру 32x32px
```

---

## 📐 ПРАВИЛЬНЫЕ РАЗМЕРЫ

### Для карты 100x100 метров:
```
GroundPlane Scale: (10, 1, 10)
```

### Для карты 500x500 метров:
```
GroundPlane Scale: (50, 1, 50)
```

### Для карты 1000x1000 метров (как у тебя):
```
GroundPlane Scale: (100, 1, 100)
```

---

## 🐛 ИСПРАВЛЕНИЕ ТЕКУЩЕЙ СИТУАЦИИ

Если уже всё сломалось:

### Шаг 1: Удалить всё лишнее
```
Hierarchy → Delete:
- GrassSystem
- GrassPlane
- Старый Plane (если зелёный)
```

### Шаг 2: Создать заново правильно

1. **Project → GroundPlane.fbx → Drag в Hierarchy**

2. **Rename → "Terrain"**

3. **Transform:**
   ```
   Position: (500, 0, 500)  // Центр карты 1000x1000
   Scale: (100, 1, 100)
   ```

4. **Material → GeometryGrass.mat**

5. **В Material настрой:**
   ```
   Blade Height: 0.25
   Blade Width: 0.04
   Tessellation: 8
   Mask Threshold: 0
   ```

---

## ✅ ПРОВЕРКА ЧТО ВСЁ ПРАВИЛЬНО

Должно быть:
- ✅ **ОДИН** объект с травой (GroundPlane)
- ✅ Трава покрывает **ВСЮ карту**
- ✅ Нет зелёного фона
- ✅ Трава невысокая (ниже деревьев)
- ✅ Трава везде равномерно

НЕ должно быть:
- ❌ Двух plane (зелёный + трава)
- ❌ Диагональной полосы
- ❌ Видимой земли под травой
- ❌ Огромной травы

---

## 💡 ПОЧЕМУ ТАК СЛОЖНО?

**Geometry Shader работает ТОЛЬКО с mesh с высокой плотностью вершин!**

- Unity Plane = 4 вершины ❌
- GroundPlane.fbx = ~4000 вершин ✅

Поэтому **НЕЛЬЗЯ** просто применить материал на стандартный Plane!

**Нужно:**
1. Заменить mesh на GroundPlane.fbx
2. Применить материал GeometryGrass.mat
3. Правильно масштабировать

---

## 🎨 НАСТРОЙКИ ДЛЯ RTS (ФИНАЛ)

После исправления установи:

```
Blade Height: 0.2-0.3 (не больше!)
Blade Width: 0.04-0.05
Blade Height Random: 0.1
Tessellation Uniform: 8-10
Grass Mask Threshold: 0
Wind Strength: 0.3-0.5
Wind Frequency: (0.05, 0.05)
```

Это даст **невысокую, плотную траву** как в настоящих RTS играх.
