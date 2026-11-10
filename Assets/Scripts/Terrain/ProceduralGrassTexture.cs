using UnityEngine;
using Mirror;

namespace RTSokak.Terrain
{
    /// <summary>
    /// Процедурная генерация текстуры ТРАВЫ через Perlin Noise
    /// АЛЬТЕРНАТИВА геометрической траве -- работает на чистой текстуре
    /// Очень высокая производительность (60+ FPS)
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ProceduralGrassTexture : NetworkBehaviour
    {
        [Header("Texture Settings")]
        [SerializeField] private int textureWidth = 4096;
        [SerializeField] private int textureHeight = 4096;
        
        [Header("Grass Colors")]
        [SerializeField] private Color grassBase = new Color(0.3f, 0.6f, 0.2f);
        [SerializeField] private Color grassTip = new Color(0.4f, 0.8f, 0.3f);
        [SerializeField] private Color grassShadow = new Color(0.15f, 0.3f, 0.1f);
        [SerializeField] private Color dirtColor = new Color(0.35f, 0.25f, 0.15f);
        
        [Header("Grass Parameters")]
        [Range(0f, 1f)]
        [SerializeField] private float grassDensity = 0.8f; // Плотность травы
        
        [Range(1f, 100f)]
        [SerializeField] private float grassBladeScale = 20f; // Масштаб травинок
        
        [Range(0f, 1f)]
        [SerializeField] private float grassVariation = 0.5f; // Вариация высоты травинок
        
        [Range(0f, 1f)]
        [SerializeField] private float dirtPatchAmount = 0.15f; // Процент голой земли
        
        [Header("Detail Layers")]
        [Range(1f, 50f)]
        [SerializeField] private float noiseScale1 = 8f; // Крупные пятна
        
        [Range(10f, 200f)]
        [SerializeField] private float noiseScale2 = 50f; // Средние детали
        
        [Range(50f, 500f)]
        [SerializeField] private float noiseScale3 = 150f; // Мелкие детали (травинки)
        
        [Header("Network Sync")]
        [SyncVar(hook = nameof(OnSeedChanged))]
        private int syncedSeed;
        
        [SerializeField] private int seed = 0; // 0 = random

        private MeshRenderer meshRenderer;
        private Texture2D generatedTexture;

        public override void OnStartServer()
        {
            base.OnStartServer();
            meshRenderer = GetComponent<MeshRenderer>();
            syncedSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            GenerateGrassTexture(syncedSeed);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            meshRenderer = GetComponent<MeshRenderer>();

            if (isServer)
                return;

            if (syncedSeed != 0)
                GenerateGrassTexture(syncedSeed);
        }

        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            if (newSeed == 0)
                return;
            if (isServer)
                return;
            GenerateGrassTexture(newSeed);
        }

        /// <summary>
        /// Генерирует процедурную текстуру травы
        /// </summary>
        private void GenerateGrassTexture(int useSeed)
        {
            Random.InitState(useSeed);
            
            float offsetX = Random.Range(0f, 10000f);
            float offsetY = Random.Range(0f, 10000f);
            
            generatedTexture = new Texture2D(textureWidth, textureHeight);
            generatedTexture.wrapMode = TextureWrapMode.Repeat;
            generatedTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureWidth * textureHeight];

            // Прогресс для больших текстур
            int totalPixels = textureWidth * textureHeight;
            int processedPixels = 0;

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    float u = (float)x / textureWidth;
                    float v = (float)y / textureHeight;

                    // Слой 1: Крупные пятна травы/земли
                    float noise1 = Mathf.PerlinNoise(
                        offsetX + u * noiseScale1, 
                        offsetY + v * noiseScale1
                    );

                    // Слой 2: Средние детали (клампы травы)
                    float noise2 = Mathf.PerlinNoise(
                        offsetX + u * noiseScale2, 
                        offsetY + v * noiseScale2
                    );

                    // Слой 3: Мелкие детали (отдельные травинки)
                    float noise3 = Mathf.PerlinNoise(
                        offsetX + u * noiseScale3, 
                        offsetY + v * noiseScale3
                    );

                    // Слой 4: Вертикальные полосы (имитация травинок)
                    float grassBlades = Mathf.PerlinNoise(
                        offsetX + u * grassBladeScale * 10f,
                        offsetY + v * grassBladeScale
                    );

                    // Определяем где земля
                    bool isDirt = noise1 < dirtPatchAmount;

                    Color pixelColor;

                    if (isDirt)
                    {
                        // Земля с небольшой вариацией
                        float dirtVariation = noise2 * 0.3f;
                        pixelColor = Color.Lerp(dirtColor * 0.8f, dirtColor, dirtVariation);
                    }
                    else
                    {
                        // Трава
                        float grassIntensity = noise2 * grassVariation;
                        
                        // Базовый цвет травы (смесь тёмной основы и светлых кончиков)
                        Color baseGrassColor = Color.Lerp(grassBase, grassTip, grassIntensity);
                        
                        // Добавляем вертикальные "травинки"
                        if (grassBlades > 0.6f)
                        {
                            // Светлые кончики травинок
                            pixelColor = Color.Lerp(baseGrassColor, grassTip, (grassBlades - 0.6f) * 2f);
                        }
                        else if (grassBlades < 0.4f)
                        {
                            // Тени между травинками
                            pixelColor = Color.Lerp(baseGrassColor, grassShadow, (0.4f - grassBlades) * 2f);
                        }
                        else
                        {
                            pixelColor = baseGrassColor;
                        }
                        
                        // Мелкие детали (кластеры)
                        if (noise3 > 0.7f)
                        {
                            pixelColor = Color.Lerp(pixelColor, grassTip, 0.3f);
                        }
                        else if (noise3 < 0.3f)
                        {
                            pixelColor = Color.Lerp(pixelColor, grassShadow, 0.2f);
                        }
                    }

                    // Глобальное осветление/затемнение
                    float brightness = Mathf.Lerp(0.85f, 1.15f, noise1);
                    pixelColor *= brightness;

                    pixels[y * textureWidth + x] = pixelColor;
                    processedPixels++;
                }

                // Показываем прогресс каждые 10%
                if (y % (textureHeight / 10) == 0)
                {
                    float progress = (float)processedPixels / totalPixels * 100f;
                    UnityEngine.Debug.Log($"[ProceduralGrassTexture] Generating... {progress:F0}%");
                }
            }

            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply();

            // Применяем к материалу
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.mainTexture = generatedTexture;
                
                // Tiling -- повторяем текстуру для детализации
                float tiling = 5f; // Можно настроить под размер карты
                meshRenderer.material.mainTextureScale = new Vector2(tiling, tiling);
            }

            UnityEngine.Debug.Log($"[ProceduralGrassTexture] Generated {textureWidth}x{textureHeight} texture with seed {useSeed}");
        }

        /// <summary>
        /// Регенерировать текстуру (только сервер)
        /// </summary>
        [Server]
        public void RegenerateTexture()
        {
            syncedSeed = Random.Range(1, int.MaxValue);
            GenerateGrassTexture(syncedSeed);
        }

        /// <summary>
        /// Обновить параметры в реальном времени
        /// </summary>
        [ContextMenu("Regenerate Now")]
        public void RegenerateNow()
        {
            if (NetworkServer.active)
            {
                RegenerateTexture();
            }
            else
            {
                GenerateGrassTexture(seed != 0 ? seed : Random.Range(1, int.MaxValue));
            }
        }

        private void OnDestroy()
        {
            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
            }
        }
    }
}