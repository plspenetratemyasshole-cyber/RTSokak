# Post-Processing Setup Guide for RTS Game

## 🎨 Обзор

Оптимизированная конфигурация **Post-Processing Stack v2** для RTS игры с **TND Upscaler**.

Три профиля на выбор:
- **Performance** (~1-2% GPU) — минимальный визуальный эффект
- **Balanced** (~3-5% GPU) — рекомендуется ⭐
- **Quality** (~7-10% GPU) — максимальная красота

---

## 🚀 БЫСТРЫЙ СТАРТ

### Вариант 1: Автоматический Setup (1 минута)

1. **Unity Menu:**
   ```
   Tools → RTS Setup → Setup Post Processing
   ```

2. **В Inspector:**
   - Выбери preset: **Performance / Balanced / Quality**
   - Нажми **"Apply RTS Optimized Settings"**

3. **Готово!** 🎉

---

### Вариант 2: Ручная настройка

1. **Main Camera** → Add Component → **Post Process Layer**
   - Volume Trigger: Main Camera
   - Layer: Everything
   - Anti-aliasing: FXAA (Fast Mode)

2. **Main Camera** → Add Component → **Post Process Volume**
   - Is Global: ✅
   - Weight: 1.0
   - Priority: 0

3. **Create Profile:**
   - Project → Right-click → Create → **Post-process Profile**
   - Название: `PostProcessProfile_RTS`
   - Assign в PostProcessVolume → Profile

4. **Add PostProcessingSetup:**
   - Hierarchy → Create Empty → название: `PostProcessingSetup`
   - Add Component → **PostProcessingSetup**
   - Assign profile
   - Click "Apply RTS Optimized Settings"

---

## 📊 ЧТО ВКЛЮЧАЕТ КАЖДЫЙ PRESET

### **Performance Preset** (~1-2% GPU)

✅ **Ambient Occlusion** (Light)
- Intensity: 0.25
- Mode: Scalable AO
- Добавляет глубину в углах

✅ **Color Grading** (0% GPU!)
- Tonemapper: ACES
- Temperature: +5 (тёплый оттенок)
- Saturation: +10
- Contrast: +5

✅ **Vignette** (0% GPU!)
- Intensity: 0.25
- Затемнение по краям

**Результат:** Базовое улучшение с минимальным FPS impact

---

### **Balanced Preset** (~3-5% GPU) ⭐ РЕКОМЕНДУЕТСЯ

Всё из Performance +

✅ **Bloom** (Light)
- Intensity: 0.15
- Threshold: 1.1
- Fast Mode: ON
- Свечение ярких объектов

✅ **Chromatic Aberration** (Subtle)
- Intensity: 0.1
- Fast Mode: ON
- Лёгкая цветовая аберрация

✅ **Grain**
- Intensity: 0.15
- Киношный эффект плёнки

✅ **Enhanced Color Grading**
- Temperature: +8
- Saturation: +15
- Contrast: +8

**Результат:** Профессиональная картинка без просадки FPS

---

### **Quality Preset** (~7-10% GPU)

Всё из Balanced +

✅ **Ambient Occlusion** (High)
- Mode: Multi-Scale Volumetric Obscurance
- Intensity: 0.5
- Максимальная глубина

✅ **Bloom** (Medium)
- Intensity: 0.25
- Fast Mode: OFF
- Лучшее качество

✅ **Screen Space Reflections** (Light)
- Preset: Medium
- Max Iterations: 32
- Отражения на воде/металле

✅ **Advanced Color Grading**
- Full Lift/Gamma/Gain control
- Temperature: +10
- Saturation: +20
- Contrast: +10

**Результат:** AAA-качество картинки

---

## 🎯 РЕКОМЕНДУЕМЫЕ НАСТРОЙКИ ДЛЯ RTS

### Для Top-Down камеры:

```
Balanced Preset с изменениями:

Ambient Occlusion:
- Intensity: 0.3 (меньше чем обычно)
- Причина: сверху AO менее заметен

Bloom:
- Threshold: 1.2 (выше)
- Причина: только яркие объекты (огонь, вода)

Vignette:
- Intensity: 0.2 (меньше)
- Причина: не отвлекать от краёв карты

Color Grading:
- Saturation: +12
- Temperature: +6 (тёплый для дневного света)
```

---

## 💡 ДОПОЛНИТЕЛЬНЫЕ ЭФФЕКТЫ (ручная настройка)

### 1. Auto Exposure (динамическая яркость)

**НЕ рекомендуется для RTS!**
- Причина: игрок сам управляет камерой
- Автояркость будет мешать

### 2. Motion Blur (размытие движения)

**НЕ рекомендуется для RTS!**
- Причина: игрок часто двигает камеру
- Размытие отвлекает и мешает видеть юнитов

### 3. Depth of Field (размытие фона)

**НЕ рекомендуется для RTS!**
- Причина: игрок должен видеть ВСЁ
- Размытие части карты недопустимо

### 4. Lens Distortion (искажение линз)

**Можно добавить очень слабое:**
```
Intensity: -5 (лёгкая "рыбий глаз")
Scale: 1.0
```
Создаёт эффект широкоугольной камеры.

---

## 🔧 ИНТЕГРАЦИЯ С TND UPSCALER

TND Upscaler + Post-Processing = мощная комбинация!

### Правильный порядок компонентов на камере:

```
Main Camera:
1. Camera (базовый)
2. TND Upscaler (рендерит в меньшее разрешение)
3. Post Process Layer (применяет эффекты после upscale)
```

### Настройка TND Upscaler:

```
Render Scale: 0.75 (75% от нативного разрешения)
Upscale Quality: High
Sharpness: 0.5-0.7
```

**Результат:** +50% FPS при незначительной потере качества!

---

## 📈 ОПТИМИЗАЦИЯ ПРОИЗВОДИТЕЛЬНОСТИ

### Если FPS низкий:

1. **Уменьши Ambient Occlusion:**
   ```
   Intensity: 0.2 → 0.15
   или Mode: MSVO → Scalable AO
   ```

2. **Отключи Bloom:**
   ```
   Enabled: OFF
   ```
   Bloom самый "дорогой" эффект после SSR.

3. **Упрости Anti-Aliasing:**
   ```
   PostProcessLayer → Antialiasing: None
   Используй MSAA 2x в Quality Settings вместо FXAA
   ```

4. **Используй Performance Preset:**
   ```
   PostProcessingSetup → Preset: Performance
   Apply RTS Optimized Settings
   ```

---

## 🎨 ЦВЕТОВАЯ КОРРЕКЦИЯ ДЛЯ ВРЕМЕНИ СУТОК

### День (яркий, тёплый):
```csharp
colorGrading.temperature.Override(10f);   // Тёплый
colorGrading.tint.Override(-5f);          // Чуть зеленоватый
colorGrading.saturation.Override(20f);    // Насыщенный
```

### Вечер (оранжевый):
```csharp
colorGrading.temperature.Override(25f);   // Очень тёплый
colorGrading.tint.Override(0f);
colorGrading.saturation.Override(15f);
colorGrading.contrast.Override(12f);      // Больше контраста
```

### Ночь (холодный, синеватый):
```csharp
colorGrading.temperature.Override(-15f);  // Холодный
colorGrading.tint.Override(10f);          // Синеватый
colorGrading.saturation.Override(8f);     // Меньше насыщенности
colorGrading.contrast.Override(5f);
```

---

## 🐛 Troubleshooting

### Post-Processing не работает:

**Проверь:**
1. Main Camera → Post Process Layer компонент добавлен?
2. Post Process Volume → Is Global = ✅?
3. Profile назначен в Volume?
4. PostProcessingSetup → Apply Settings был нажат?

### Розовый экран:

**Причина:** Shader не найден

**Решение:**
```
Edit → Project Settings → Graphics
Scriptable Render Pipeline Settings: None (Built-in)
```

### Слишком тёмно/светло:

**Измени:**
```
Color Grading → Lift: (1, 1, 1, 0.05) — светлее
Color Grading → Lift: (1, 1, 1, -0.05) — темнее
```

### FPS просел:

**Отключи по очереди:**
1. Screen Space Reflections (самый дорогой)
2. Bloom
3. Ambient Occlusion → Fast Mode
4. Grain

---

## 📚 Дополнительные ресурсы

**Unity Docs:**
- [Post-Processing Stack v2](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.2/manual/index.html)

**Туториалы:**
- [Brackeys: Post Processing](https://www.youtube.com/watch?v=9tjYz6Ab0oc)
- [Unity Official: Color Grading](https://learn.unity.com/tutorial/working-with-post-processing)

---

## ✅ CHECKLIST

После Setup проверь:

- [ ] PostProcessVolume на камере
- [ ] PostProcessLayer на камере
- [ ] Profile создан и назначен
- [ ] Preset выбран (Balanced рекомендуется)
- [ ] Apply RTS Optimized Settings нажата
- [ ] В Game view видны эффекты (AO в углах, тёплый оттенок)
- [ ] FPS стабилен (падение не больше 5%)

---

## 💰 СТОИМОСТЬ ЭФФЕКТОВ (GPU time)

| Эффект | Impact | Рекомендация |
|--------|--------|--------------|
| Color Grading | 0% | Всегда ON ✅ |
| Vignette | 0% | Всегда ON ✅ |
| Grain | <1% | ON ✅ |
| Chromatic Aberration | 1-2% | ON ✅ |
| Bloom (Fast) | 2-3% | ON ✅ |
| Ambient Occlusion (SAO) | 2-4% | ON ✅ |
| FXAA | 1-2% | ON ✅ |
| Bloom (High Quality) | 4-6% | Опционально |
| AO (MSVO) | 5-8% | Опционально |
| Screen Space Reflections | 8-15% | Выключить для RTS ❌ |
| Motion Blur | 3-5% | Выключить для RTS ❌ |

---

**Итого:** Balanced preset = ~3-5% GPU для максимального визуального эффекта! 🚀
