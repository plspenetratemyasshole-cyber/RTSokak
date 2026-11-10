# ⚡ БЫСТРАЯ НАСТРОЙКА ТРАВЫ (2 МИНУТЫ)

## 📋 ЧТО НУЖНО

1. **Material:** `Assets/GrassShader/Materials/GeometryGrass.mat`
2. **Model:** `Assets/GrassShader/Models/GroundPlane.fbx` ← **ОБЯЗАТЕЛЬНО!**

---

## 🎯 НАСТРОЙКА (4 ШАГА)

### 1. Создать объект
```
Hierarchy → Create Empty GameObject → "GrassSystem"
```

### 2. Добавить компонент
```
Add Component → RTSGrassManager
```

### 3. Назначить в Inspector:

**Grass Material:**
- Drag: `Assets/GrassShader/Materials/GeometryGrass.mat`

**Grass Plane Prefab:**  
- Drag: `Assets/GrassShader/Models/GroundPlane.fbx` ← **БЕЗ ЭТОГО НЕ РАБОТАЕТ!**

### 4. Play!

Трава появится автоматически.

---

## ⚙️ НАСТРОЙКИ (ОПЦИОНАЛЬНО)

В Inspector → RTSGrassManager:

```
Map Size: 100x100 (размер карты)
Grass Density: 15 (плотность)
Grass Height: 0.5
Wind Speed: 0.8
Cast Shadows: OFF (для FPS)
```

Или нажми кнопку **"Medium Quality"**

---

## ❌ ЧАСТЫЕ ОШИБКИ

### "Огромные треугольники вместо травы"
**Причина:** Не назначен GroundPlane.fbx  
**Решение:** Перетащи `Assets/GrassShader/Models/GroundPlane.fbx` в **Grass Plane Prefab**

### "Трава не появляется"
**Причина:** Не назначен Material или Prefab  
**Решение:** Проверь оба поля в Inspector

### "FPS низкий"
**Причина:** Слишком высокая плотность  
**Решение:** Grass Density = **10**, Cast Shadows = **OFF**

---

## 🎨 ПОЧЕМУ GROUNDPLANE.FBX?

Geometry Shader генерирует **травинки из вершин mesh**:

- **Unity Plane** = 4 вершины → 4 гигантских треугольника ❌
- **GroundPlane.fbx** = ~4000 вершин → 4000 травинок ✅

Без GroundPlane.fbx трава **не будет работать!**

---

## ✅ ПРОВЕРКА

Всё правильно если:
- ✅ Маленькие травинки по всей поверхности
- ✅ Плавное колыхание на ветру
- ✅ FPS >= 60
- ✅ Нет розовых материалов

Неправильно:
- ❌ Огромные треугольники
- ❌ Трава не видна
- ❌ Розовый цвет

---

## 🚀 ВСЁ РАБОТАЕТ? СДЕЛАЙ ПРЕФАБ!

```
Hierarchy → GrassSystem → Drag в Project/Prefabs/
```

Теперь можешь использовать в других сценах!
