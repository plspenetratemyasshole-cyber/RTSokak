# ✅ ЧЕКЛИСТ НАСТРОЙКИ ТРАВЫ

## 🎯 БЫСТРАЯ НАСТРОЙКА (5 минут)

### 1. Создать объект
- [ ] Hierarchy → Create Empty → название `GrassSystem`

### 2. Добавить компонент
- [ ] Add Component → RTSGrassManager

### 3. Назначить материал
- [ ] Inspector → Grass Material
- [ ] Перетащить `Assets/GrassShader/Materials/GeometryGrass.mat`

### 4. Запустить
- [ ] Нажать Play
- [ ] Трава должна появиться автоматически

### 5. Настроить качество (Inspector)
- [ ] Нажать кнопку `Medium Quality` (рекомендовано)
- [ ] Или настроить вручную:
  - Grass Density: **15**
  - Grass Height: **0.5**
  - Wind Speed: **0.8**
  - Cast Shadows: **OFF**

---

## 🔧 РУЧНАЯ НАСТРОЙКА

Если автоматика не работает:

### 1. Создать Plane
- [ ] Hierarchy → 3D Object → Plane
- [ ] Position: (0, 0, 0)
- [ ] Scale: (10, 1, 10)

### 2. Применить Material
- [ ] Drag `GeometryGrass.mat` на Plane

### 3. Настроить параметры
В Material Inspector:
- [ ] Color Bottom: RGB(60, 120, 40)
- [ ] Color Top: RGB(120, 200, 80)
- [ ] Grass Density: **15**
- [ ] Grass Height: **0.5**
- [ ] Wind Speed: **0.8**

### 4. Оптимизация
- [ ] Mesh Renderer → Cast Shadows: **OFF**

---

## 🐛 ПРОВЕРКА ПРОБЛЕМ

### Трава не видна?
- [ ] Material назначен?
- [ ] Shader не розовый? (Reimport если розовый)
- [ ] Камера смотрит на Plane?
- [ ] Play нажат?

### FPS низкий?
- [ ] Density <= 15?
- [ ] Cast Shadows = OFF?
- [ ] LOD Distance = 100?

### Трава розовая?
- [ ] Right-click на `GeometryGrassShader.shader` → Reimport
- [ ] Unity перезапущен?

---

## 📁 ФАЙЛЫ (Проверь что всё на месте)

### Обязательные:
- [ ] `Assets/GrassShader/Shaders/GeometryGrassShader.shader`
- [ ] `Assets/GrassShader/Materials/GeometryGrass.mat`
- [ ] `Assets/GrassShader/Scripts/RTSGrassManager.cs`
- [ ] `Assets/Scripts/Editor/RTSGrassManagerEditor.cs`

### Текстуры:
- [ ] `Assets/GrassShader/Textures/WindDistortion.png`
- [ ] `Assets/GrassShader/Textures/GrassMask.png`

### Документация:
- [ ] `Assets/GrassShader/README_RTS_SETUP.md`
- [ ] `Assets/GrassShader/PREFAB_GUIDE.md`
- [ ] `Assets/GrassShader/CHECKLIST.md` (этот файл)

---

## 🎨 НАСТРОЙКИ ПО КАЧЕСТВУ

### Low (слабые ПК)
- [ ] Grass Density: **10**
- [ ] Grass Height: **0.3**
- [ ] LOD Distance: **50**
- [ ] Cast Shadows: **OFF**
- Ожидаемый FPS: **100+**

### Medium (рекомендовано)
- [ ] Grass Density: **15**
- [ ] Grass Height: **0.5**
- [ ] LOD Distance: **100**
- [ ] Cast Shadows: **OFF**
- Ожидаемый FPS: **80+**

### High (мощные ПК)
- [ ] Grass Density: **20**
- [ ] Grass Height: **0.6**
- [ ] LOD Distance: **150**
- [ ] Cast Shadows: **ON** (опционально)
- Ожидаемый FPS: **60+**

---

## 🚀 ИНТЕГРАЦИЯ С RTS ИГРОЙ

### 1. Добавить в Main Menu
```csharp
// В настройках графики добавить:
RTSGrassManager grassManager = FindObjectOfType<RTSGrassManager>();
grassManager.SetGrassQuality(PlayerPrefs.GetString("GrassQuality", "medium"));
```

### 2. Добавить в Graphics Settings UI
- [ ] Dropdown с вариантами: Low / Medium / High
- [ ] Callback на изменение:
```csharp
void OnGrassQualityChanged(int index)
{
    string[] qualities = { "low", "medium", "high" };
    string quality = qualities[index];
    
    RTSGrassManager grassManager = FindObjectOfType<RTSGrassManager>();
    grassManager.SetGrassQuality(quality);
    
    PlayerPrefs.SetString("GrassQuality", quality);
}
```

### 3. Для больших карт (>200x200m)
- [ ] Использовать систему чанков
- [ ] Включить Frustum Culling
- [ ] Настроить LOD Distance

---

## ✅ ФИНАЛЬНАЯ ПРОВЕРКА

Всё работает если:
- [ ] Трава зелёная (не розовая)
- [ ] Трава колышется на ветру
- [ ] FPS >= 60
- [ ] Нет ошибок в Console
- [ ] При движении камеры всё плавно

---

## 📞 НУЖНА ПОМОЩЬ?

### Проблема: Трава не появляется
**Решение:** Проверь Console на ошибки, убедись что Material назначен

### Проблема: FPS низкий
**Решение:** Уменьши Density до 10, отключи Cast Shadows

### Проблема: Трава розовая
**Решение:** Reimport shader, перезапусти Unity

### Всё сломалось
**Решение:** Удали `Assets/GrassShader/`, скачай заново и импортируй правильно
