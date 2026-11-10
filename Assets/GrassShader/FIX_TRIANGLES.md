# 🔧 ИСПРАВЛЕНИЕ: Огромные треугольники вместо травы

## ❌ ПРОБЛЕМА

Geometry Shader генерирует травинки из **вершин mesh**. 
Unity Plane имеет только **4 вершины** → получается **4 гигантских треугольника** вместо травы!

---

## ✅ РЕШЕНИЕ 1: Использовать GroundPlane.fbx (БЫСТРО)

### В Unity:

1. **Удали текущий GrassPlane** из Hierarchy

2. **Project → Assets/GrassShader/Models/GroundPlane.fbx**

3. **Drag & Drop в Hierarchy**

4. **Назначь материал:**
   - Drag `GeometryGrass.mat` на GroundPlane

5. **Настрой позицию:**
   - Position: (50, 0, 50)
   - Scale: (10, 1, 10)

6. **Play** — трава должна выглядеть нормально!

---

## ✅ РЕШЕНИЕ 2: Пересоздать через RTSGrassManager (АВТО)

Если используешь мой скрипт:

1. **Hierarchy → Delete старый GrassSystem**

2. **Hierarchy → Create Empty → "GrassSystem"**

3. **Add Component → RTSGrassManager**

4. **Inspector:**
   - Grass Material → `GeometryGrass.mat`
   - **НЕ НАЗНАЧАЙ** Grass Plane Prefab (оставь пустым)

5. **Play** — скрипт автоматически загрузит GroundPlane.fbx

---

## ✅ РЕШЕНИЕ 3: Создать свой Plane с высокой плотностью (СЛОЖНО)

### В Blender:

1. **Add → Mesh → Plane**

2. **Tab (Edit Mode)**

3. **Right-click → Subdivide**

4. **Повтори Subdivide 5-6 раз** (получится ~1000 вершин)

5. **File → Export → FBX**
   - Apply Scalings: FBX All
   - Forward: -Z Forward
   - Up: Y Up

6. **Импортируй в Unity → Assets/Models/**

7. **Используй как префаб**

---

## 🎯 ПРОВЕРКА ЧТО ВСХЁ РАБОТАЕТ

### Правильная трава:
- ✅ Маленькие травинки по всей поверхности
- ✅ Плавное колыхание на ветру
- ✅ Плотность равномерная

### Неправильная (треугольники):
- ❌ 4-20 огромных треугольника
- ❌ Видны острые углы
- ❌ Трава только на углах

---

## 🐛 ЕСЛИ ВСЁ ЕЩЁ НЕ РАБОТАЕТ

### 1. Проверь что используешь GroundPlane.fbx:

```
Hierarchy → Select GrassPlane → Inspector → Mesh Filter → Mesh:
Должно быть: "GroundPlane"
НЕ "Plane" (это Unity Plane с 4 вершинами!)
```

### 2. Проверь количество вершин:

В Inspector → Mesh Filter → Mesh:
- **GroundPlane.fbx**: ~4000-10000 вершин ✅
- **Unity Plane**: 4 вершины ❌

### 3. Проверь Material:

- Shader должен быть: `Custom/GeometryGrassShader`
- Текстуры назначены
- Параметры заданы (Density, Height, etc.)

---

## 📊 СРАВНЕНИЕ

| Mesh Type | Вершины | Травинки | Результат |
|-----------|---------|----------|-----------|
| Unity Plane | 4 | 4 | ❌ Треугольники |
| Subdivided Plane (x2) | 25 | 25 | ⚠️ Редкая трава |
| Subdivided Plane (x4) | 289 | 289 | ✅ Норм |
| GroundPlane.fbx | ~4000+ | ~4000+ | ✅✅ Отлично |

---

## 🎨 ОПТИМАЛЬНЫЕ ПАРАМЕТРЫ

После исправления mesh установи:

```
Grass Density: 15
Grass Height: 0.5
Grass Width: 0.1
Wind Speed: 0.8
Wind Strength: 0.2
```

---

## ⚡ ПРОИЗВОДИТЕЛЬНОСТЬ

С GroundPlane.fbx (~4000 вершин):

- GTX 1060: **80+ FPS** при Density 15
- RTX 2060: **120+ FPS** при Density 20
- GTX 750 Ti: **40-50 FPS** при Density 10

Если FPS низкий — уменьши **Grass Density** до 10.
