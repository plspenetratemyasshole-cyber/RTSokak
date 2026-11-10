# 📦 СОЗДАНИЕ ПРЕФАБА ТРАВЫ

## ВАРИАНТ 1: Автоматически (Рекомендуется)

1. **Hierarchy → Create Empty**
   - Название: `GrassSystem`

2. **Add Component → RTSGrassManager**

3. **Inspector:**
   - Grass Material → `Assets/GrassShader/Materials/GeometryGrass.mat`

4. **Play** — трава создастся автоматически

5. **Настрой кнопками:**
   - `Low Quality` — 10 density
   - `Medium Quality` — 15 density (рекомендовано)
   - `High Quality` — 20 density

---

## ВАРИАНТ 2: Вручную

### Шаг 1: Создать Plane

1. **Hierarchy → 3D Object → Plane**
   - Название: `GrassPlane`
   - Position: (0, 0, 0)
   - Scale: (10, 1, 10) — 100x100 метров

### Шаг 2: Применить Material

1. **Project → Assets/GrassShader/Materials/GeometryGrass.mat**

2. **Drag & Drop на GrassPlane**

### Шаг 3: Настроить Material (Inspector → GrassPlane → Material)

**Цвета:**
```
Color Bottom: RGB(60, 120, 40)
Color Top: RGB(120, 200, 80)
```

**Размеры:**
```
Grass Height: 0.5
Grass Width: 0.1
Grass Density: 15  ⚠️ НЕ БОЛЬШЕ 20!
```

**Ветер:**
```
Wind Speed: 0.8
Wind Strength: 0.2
Wind Direction: (1, 0)
```

### Шаг 4: Оптимизация

1. **Inspector → GrassPlane → Mesh Renderer:**
   - Cast Shadows: **OFF**
   - Receive Shadows: **ON**

2. **Add Component → RTSGrassManager** (опционально)
   - Для управления качеством через код

---

## 🎨 ПРЕДУСТАНОВКИ ЦВЕТОВ

### Сочный зелёный (по умолчанию)
```
Bottom: RGB(60, 120, 40)  #3C7828
Top: RGB(120, 200, 80)    #78C850
```

### Сухая трава (пустыня)
```
Bottom: RGB(140, 120, 60)  #8C783C
Top: RGB(200, 180, 100)    #C8B464
```

### Тёмная трава (лес)
```
Bottom: RGB(40, 80, 30)   #28501E
Top: RGB(80, 140, 60)     #508C3C
```

### Осенняя трава
```
Bottom: RGB(120, 100, 50) #78642C
Top: RGB(180, 160, 80)    #B4A050
```

---

## 🎯 ИСПОЛЬЗОВАНИЕ ПРЕФАБА

### После создания сохрани как Prefab:

1. **Drag GrassPlane из Hierarchy в Project → Prefabs/**

2. **В других сценах:**
   - Drag prefab в Hierarchy
   - Настрой Scale под размер карты

---

## ⚙️ ДИНАМИЧЕСКОЕ СОЗДАНИЕ

```csharp
// В коде RTS игры
public class MapGenerator : MonoBehaviour
{
    public GameObject grassPrefab;
    
    void GenerateMap()
    {
        GameObject grass = Instantiate(grassPrefab);
        grass.transform.position = new Vector3(0, 0, 0);
        grass.transform.localScale = new Vector3(10, 1, 10); // 100x100m
        
        // Настроить качество
        RTSGrassManager manager = grass.GetComponent<RTSGrassManager>();
        if (manager != null)
        {
            manager.SetGrassQuality("medium");
        }
    }
}
```

---

## 📊 ПРОИЗВОДИТЕЛЬНОСТЬ

**Рекомендации для RTS:**

| Размер карты | Density | FPS (GTX 1060) |
|--------------|---------|----------------|
| 100x100m     | 10      | 100+ FPS       |
| 100x100m     | 15      | 80+ FPS        |
| 100x100m     | 20      | 60+ FPS        |
| 200x200m     | 10      | 70+ FPS        |
| 200x200m     | 15      | 50+ FPS        |

⚠️ **Для карт >200x200m используй систему чанков!**
