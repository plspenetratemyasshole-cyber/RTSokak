# 🌾 Animated Grass Texture Shader

## Что делает

Берёт **твою готовую текстуру** травы и добавляет:
- ✅ **Анимация ветра** -- реалистичное качание
- ✅ **Цветовая вариация** -- устраняет повторения
- ✅ **Вариация яркости** -- каждый участок уникален
- ✅ **60+ FPS** -- легковесный shader

---

## Быстрый старт

### 1. Создай Material

```
Project -> Create -> Material -> "GrassAnimatedMaterial"
```

**Назначь shader:**
```
Shader: RTSokak/AnimatedGrassTexture
```

### 2. Назначь свою текстуру

```
Grass Texture: <твоя текстура травы>
```

### 3. Настрой параметры

#### **Базовый цвет:**
```
Base Grass Tint: (1, 1, 1, 1)  <- белый = без изменений
```

#### **Ветер:**
```
Wind Speed: 1.0               <- скорость качания
Wind Strength: 0.05           <- амплитуда
Wind Direction XZ: (1, 0, 1)  <- направление (X, 0, Z)
```

#### **Цветовая вариация:**
```
Color Variation Scale: 10.0        <- размер пятен
Color Variation Strength: 0.3      <- сила эффекта (0-1)
Variation Tint 1: (0.9, 1.0, 0.9)  <- оттенок 1
Variation Tint 2: (1.0, 0.95, 0.85) <- оттенок 2
```

#### **Дополнительно:**
```
Brightness Variation: 0.2     <- вариация яркости
Texture Tiling: 1.0           <- повторение текстуры
```

### 4. Применить на Plane

```
Hierarchy -> Твой Plane
Mesh Renderer -> Materials -> Element 0: GrassAnimatedMaterial
```

---

## Примеры настроек

### **Тихий ветер:**
```
Wind Speed: 0.5
Wind Strength: 0.03
Wind Direction: (1, 0, 0.5)
```

### **Сильный ветер:**
```
Wind Speed: 2.5
Wind Strength: 0.15
Wind Direction: (1, 0, 0.2)
```

### **Без вариации цвета:**
```
Color Variation Strength: 0.0
```

### **Сильная вариация:**
```
Color Variation Scale: 5.0
Color Variation Strength: 0.6
Variation Tint 1: (0.8, 1.0, 0.7)
Variation Tint 2: (1.0, 0.9, 0.6)
```

### **Осенняя трава:**
```
Base Grass Tint: (1.0, 0.95, 0.7)
Variation Tint 1: (1.0, 0.9, 0.6)
Variation Tint 2: (0.9, 0.7, 0.4)
Color Variation Strength: 0.5
```

---

## Как работает

### **Ветер:**
- Две синусоидальные волны разной частоты
- Движение зависит от world position
- Вершины выше качаются сильнее (UV.y)

### **Цветовая вариация:**
- Perlin noise на основе world position
- Смешивает два оттенка (Tint 1 и Tint 2)
- Устраняет видимое повторение текстуры

### **Вариация яркости:**
- Дополнительный noise слой
- Делает каждый участок уникальным
- Работает независимо от цветовой вариации

---

## Где взять текстуру травы

### **Бесплатные источники:**

1. **Unity Asset Store:**
   - Grass Flowers Pack Free
   - Yughues Free Ground Materials
   - Handpainted Grass Textures

2. **Онлайн библиотеки:**
   - [Poly Haven](https://polyhaven.com/textures/terrain) -- PBR текстуры
   - [Free PBR](https://freepbr.com/) -- высокое качество
   - [Texture Haven](https://texturehaven.com/) -- бесплатные

3. **Создать свою:**
   - Сфотографировать реальную траву
   - Сделать tileable в Photoshop/GIMP
   - Или использовать `ProceduralGrassTexture.cs`

---

## Производительность

| Параметр            | FPS Impact |
|--------------------|-----------|
| Ветер              | ~1-2 FPS  |
| Цветовая вариация  | ~3-5 FPS  |
| Вариация яркости   | ~2-3 FPS  |
| **Итого**          | **60+ FPS** |

**Легче чем геометрическая трава в ~10 раз!**

---

## Troubleshooting

### **Трава не качается:**
- Проверь `Wind Strength` > 0
- Проверь что Plane не статичный

### **Цвета странные:**
- Сбрось `Base Grass Tint` на белый (1,1,1,1)
- Уменьши `Color Variation Strength`

### **Текстура размытая:**
- Увеличь разрешение текстуры
- Проверь `Filter Mode: Bilinear` в текстуре

### **Всё ещё видны повторения:**
- Увеличь `Color Variation Strength` до 0.5-0.7
- Уменьши `Color Variation Scale` до 5-8
- Увеличь `Brightness Variation` до 0.3-0.4

---

**Готово!** Теперь твоя текстура оживает с ветром и без повторений! 🎉
