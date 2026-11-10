# 🌿 НАСТРОЙКА GEOMETRY GRASS SHADER

## ✅ БЫСТРЫЙ СТАРТ

### Шаг 1: Создать объект с травой

1. **Hierarchy → Create Empty**
   - Название: `GrassSystem`

2. **Add Component → RTSGrassManager**

3. **Назначить Material:**
   - Найди: `Assets/GrassShader/Materials/GeometryGrass.mat`
   - Перетащи в поле **Grass Material**

4. **Play** — трава появится автоматически!

---

## 🎨 НАСТРОЙКИ (Inspector → RTSGrassManager)

### **Grass Settings**
- **Map Size**: 100x100 (размер карты)
- **Chunk Size**: 50 (для оптимизации)

### **Grass Appearance**
- **Grass Color Bottom**: RGB(60, 120, 40) — тёмно-зелёный корень
- **Grass Color Top**: RGB(120, 200, 80) — светло-зелёная верхушка
- **Grass Height**: 0.5 (высота травинок)
- **Grass Width**: 0.1 (ширина)
- **Grass Density**: 15 ⚠️ **НЕ БОЛЬШЕ 20 ДЛЯ RTS!**

### **Wind Settings**
- **Wind Speed**: 0.8 (скорость)
- **Wind Strength**: 0.2 (сила раскачивания)
- **Wind Direction**: (1, 0) — направление →

### **Optimization**
- **Enable LOD**: ✅ (включить)
- **LOD Distance**: 100 (дистанция отключения)
- **Cast Shadows**: ❌ (отключить для FPS)

---

## ⚡ НАСТРОЙКИ КАЧЕСТВА

### Low (слабые ПК)
```
Grass Density: 10
Grass Height: 0.3
LOD Distance: 50
```

### Medium (рекомендовано)
```
Grass Density: 15
Grass Height: 0.5
LOD Distance: 100
```

### High (мощные ПК)
```
Grass Density: 20
Grass Height: 0.6
LOD Distance: 150
```

---

## 🎯 ИСПОЛЬЗОВАНИЕ В КОДЕ

```csharp
// Получить менеджер
RTSGrassManager grassManager = FindObjectOfType<RTSGrassManager>();

// Изменить плотность
grassManager.SetGrassDensity(10f); // Low quality

// Изменить качество
grassManager.SetGrassQuality("medium");
```

---

## 🐛 ПРОБЛЕМЫ

### Трава не видна
- Проверь что **Grass Material** назначен
- Убедись что камера смотрит на GrassPlane
- Проверь что Shader скомпилировался (не розовый)

### Трава розовая
- Shader не скомпилирован
- Reimport: Right-click на `GeometryGrassShader.shader` → Reimport

### FPS низкий
- Уменьши **Grass Density** до 10
- Отключи **Cast Shadows**
- Уменьши **LOD Distance** до 50

---

## 📁 ФАЙЛЫ

**Главные:**
- `GeometryGrassShader.shader` — основной shader
- `GeometryGrass.mat` — материал травы
- `RTSGrassManager.cs` — менеджер (МОЙ)

**Опционально:**
- `GrassDisplacementCamera.cs` — интерактивное примятие травы
- `GrassDisplacementObject.cs` — объекты приминающие траву
