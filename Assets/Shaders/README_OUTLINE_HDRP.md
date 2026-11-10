# HDRP Outline

Worker использует **QuickOutline** (`Outline` компонент) с HDRP-шейдерами в `Assets/QuickOutline/Resources/Shaders/`.

Дополнительно есть алиасы:
- `HDRP/OutlineMask` → `Assets/Shaders/OutlineMaskHDRP.shader`
- `HDRP/OutlineFill` → `Assets/Shaders/OutlineHDRP.shader`

Они нужны для `SimpleOutline` (`Shader.Find`). Для юнитов достаточно QuickOutline — после Reimport outline должен работать без ручного создания материалов.
