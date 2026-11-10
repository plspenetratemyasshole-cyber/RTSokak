# 🌿 Stylized Grass - Быстрая настройка

## ⚡ За 3 минуты (если у тебя HDRP):

### **1. Найди ProceduralGrassGenerator**
Выбери GameObject с этим компонентом в сцене.

### **2. Настрой параметры:**
```
Generation Mode: VFXGraph

VFX Grass Prefab: Assets/StylizedGrass/VFX Graphs/Grass/quads-Plane.vfx
VFX Area Size: (500, 500)

World Size: 500
```

### **3. Запусти игру** ✅
Трава появится автоматически!

---

## 🎨 Кастомизация (после запуска):

1. Найди в Hierarchy VFX объект травы
2. Измени в Inspector:
   - **Grass Color** - цвет
   - **Capacity** - количество (50000 по умолчанию)
   - **Wind Speed** - скорость ветра
   - **Wind Strength** - сила ветра

---

## ❌ Если нет HDRP:

```
Generation Mode: Prefab

Grass Prefab: [используй SimpleGrass или создай свой]
Grass Count: 5000
```

---

## 📂 Где найти VFX префабы:

**Обычная трава:**
`Assets/StylizedGrass/VFX Graphs/Grass/`
- quads-Plane.vfx ✅ **РЕКОМЕНДУЮ**
- quads-Circle.vfx
- tris-Plane.vfx

**Мягкая трава (красиво):**
`Assets/StylizedGrass/VFX Graphs/Soft Grass/`
- quads-Plane.vfx ✅ **САМАЯ КРАСИВАЯ**
- quads-Circle.vfx
- tris-Plane.vfx

---

## 🔥 Проблемы:

**Трава не появляется?**
→ Проверь что у тебя HDRP (Edit → Project Settings → Graphics)

**Лаги?**
→ Уменьши Capacity в VFX Inspector (с 50000 до 20000)

**Трава не двигается?**
→ Увеличь Wind Speed и Wind Strength

---

Полный гайд: `Assets/Scripts/Terrain/README_STYLIZED_GRASS.md`
