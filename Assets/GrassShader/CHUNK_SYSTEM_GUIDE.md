# 🎯 GRASS CHUNK SYSTEM - Инструкция

## ✅ ЧТО ЭТО

Система автоматически расставляет чанки травы по всей карте с:
- **LOD** — трава отрисовывается только вблизи камеры
- **Настраиваемый размер** чанков
- **Настраиваемая дальность** прорисовки
- **Автоматическое покрытие** всей карты

---

## 🚀 БЫСТРАЯ НАСТРОЙКА (3 ШАГА)

### 1. Создать объект
```
Hierarchy → Create Empty → "GrassChunkSystem"
```

### 2. Добавить компонент
```
Add Component → GrassChunkSystem
```

### 3. Назначить в Inspector

**Grass Prefab:**
```
Assets/GrassShader/Models/GroundPlane.fbx
```

**Grass Material:**
```
Assets/GrassShader/Materials/GeometryGrass.mat
```

### 4. Нажать кнопку
```
Inspector → Generate Chunks
```

**Готово!** Вся карта покрыта травой.

---

## ⚙️ НАСТРОЙКИ

### **Map Settings**

**Map Size:** Размер карты в метрах
```
Пример: (1000, 1000) для карты 1000x1000м
```

**Chunk Size:** Размер одного чанка
```
Рекомендуется: 100м
Меньше = больше чанков = больше контроля LOD
Больше = меньше чанков = меньше overhead
```

### **Chunk Prefab Scale**

Масштаб каждого префаба GroundPlane:
```
По умолчанию: (10, 1, 10) = 100x100м
Если чанки маленькие: увеличь до (20, 1, 20)
```

### **LOD Settings**

**Max Draw Distance:** Максимальная дальность видимости
```
Low: 300м
Medium: 500м  ← Рекомендуется
High: 800м
```

**Update Interval:** Как часто обновлять видимость
```
0.5 сек = хороший баланс FPS/отзывчивость
Меньше = плавнее, но больше нагрузка
```

### **Grass Appearance**

**Blade Height:** Высота травинок
```
Low: 0.2
Medium: 0.25  ← Рекомендуется
High: 0.3
```

**Blade Width:** Ширина
```
Рекомендуется: 0.04-0.05
```

**Tessellation:** Плотность
```
Low: 6
Medium: 8   ← Рекомендуется
High: 12
⚠️ НЕ БОЛЬШЕ 15!
```

**Wind Strength:** Сила ветра
```
Calm: 0.3
Normal: 0.5  ← Рекомендуется
Strong: 0.8
```

---

## 🎮 ИСПОЛЬЗОВАНИЕ

### Создать чанки
```
Inspector → Generate Chunks
```
Создаст сетку чанков по всей карте.

### Обновить настройки
```
Inspector → Update Grass Settings
```
Применит изменения Height/Width/Tessellation к материалу.

### Удалить все чанки
```
Inspector → Clear All Chunks
```
Удалит все чанки (для пересоздания).

### Пересоздать с новыми настройками
1. Измени Map Size или Chunk Size
2. Нажми **Generate Chunks**
3. Старые удалятся, создадутся новые

---

## 📊 ПРИМЕРЫ КОНФИГУРАЦИЙ

### Маленькая карта (500x500м)
```
Map Size: (500, 500)
Chunk Size: 100
Chunk Scale: (10, 1, 10)
Max Draw Distance: 300
Tessellation: 8
```
**Результат:** 5x5 = 25 чанков

### Средняя карта (1000x1000м)
```
Map Size: (1000, 1000)
Chunk Size: 100
Chunk Scale: (10, 1, 10)
Max Draw Distance: 500
Tessellation: 8
```
**Результат:** 10x10 = 100 чанков

### Большая карта (2000x2000м)
```
Map Size: (2000, 2000)
Chunk Size: 200
Chunk Scale: (20, 1, 20)
Max Draw Distance: 500
Tessellation: 6
```
**Результат:** 10x10 = 100 чанков (больше размер)

---

## ⚡ ОПТИМИЗАЦИЯ

### Для слабых ПК:
```
Chunk Size: 150
Max Draw Distance: 300
Tessellation: 6
Update Interval: 1.0
```

### Для мощных ПК:
```
Chunk Size: 100
Max Draw Distance: 800
Tessellation: 12
Update Interval: 0.3
```

### Для огромных карт (>2000м):
```
Chunk Size: 250-300
Max Draw Distance: 600
Tessellation: 6-8
```

---

## 🐛 TROUBLESHOOTING

### Трава не появляется
- Проверь что назначены Prefab и Material
- Нажми **Generate Chunks**
- Проверь Console на ошибки

### FPS низкий
- Уменьши **Max Draw Distance** до 300
- Уменьши **Tessellation** до 6
- Увеличь **Chunk Size** до 150
- Увеличь **Update Interval** до 1.0

### Трава появляется/исчезает резко
- Уменьши **Update Interval** до 0.3
- Увеличь **Max Draw Distance**

### Трава слишком высокая/низкая
- Измени **Blade Height**
- Нажми **Update Grass Settings**

### Между чанками видны швы
- Увеличь **Chunk Scale** чтобы чанки перекрывались
- Или уменьши **Chunk Size**

---

## 📐 КАК РАБОТАЕТ LOD

Система каждые **Update Interval** секунд:
1. Проверяет дистанцию от камеры до каждого чанка
2. Если дистанция > Max Draw Distance → отключает Renderer
3. Если дистанция <= Max Draw Distance → включает Renderer

**Результат:** Рендерятся только близкие чанки = высокий FPS!

---

## 🎯 ВИЗУАЛИЗАЦИЯ (Scene View)

Когда выделен GrassChunkSystem, в Scene View видно:
- **Жёлтая рамка** = границы карты (Map Size)
- **Зелёная сфера** = дальность прорисовки (Max Draw Distance)

---

## 💡 СОВЕТЫ

1. **Начни с дефолтных настроек** — они уже оптимальны
2. **Chunk Size = 100м** — хороший баланс для большинства карт
3. **Max Draw Distance** зависит от камеры:
   - Top-down близко: 300-400м
   - Top-down далеко: 500-600м
   - Perspective: 600-800м
4. **Tessellation** сильно влияет на FPS — не ставь >15!
5. Для огромных карт увеличь **Chunk Size**, а не количество чанков

---

## ✅ ФИНАЛЬНЫЙ ЧЕКЛИСТ

- [ ] Grass Prefab назначен (GroundPlane.fbx)
- [ ] Grass Material назначен (GeometryGrass.mat)
- [ ] Map Size соответствует реальному размеру карты
- [ ] Нажата кнопка **Generate Chunks**
- [ ] В Console нет ошибок
- [ ] Трава видна в Game View
- [ ] FPS >= 60
- [ ] Трава исчезает вдалеке (проверь переместив камеру)

---

Готово! Теперь у тебя система травы как в профессиональных RTS играх 🎮
