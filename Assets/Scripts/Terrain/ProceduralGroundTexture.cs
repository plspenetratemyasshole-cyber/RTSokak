using UnityEngine;
using Mirror;

namespace RTSokak.Terrain
{
    /// <summary>
    /// Процедурная генерация текстуры для плоскости земли
    /// Использует Perlin Noise для создания естественного вида травы/почвы
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ProceduralGroundTexture : NetworkBehaviour
    {
        [Header("Texture Settings")]
        [SerializeField] private int textureWidth = 2048;
        [SerializeField] private int textureHeight = 2048;
        [SerializeField] private float noiseScale = 50f;
        
        [Header("Ground Colors")]
        [SerializeField] private Color grassColorDark = new Color(0.2f, 0.5f, 0.2f);
        [SerializeField] private Color grassColorLight = new Color(0.3f, 0.7f, 0.3f);
        [SerializeField] private Color dirtColor = new Color(0.4f, 0.3f, 0.2f);
        
        [Header("Variation")]
        [SerializeField] private float grassCoverage = 0.7f; // 0-1, процент травы
        [SerializeField] private float detailScale = 200f; // Детализация мелких элементов
        [SerializeField] private int seed = 0; // 0 = random
        
        [Header("Network Sync")]
        [SyncVar(hook = nameof(OnSeedChanged))]
        private int syncedSeed;

        private MeshRenderer meshRenderer;
        private Texture2D generatedTexture;

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (NetworkServer.active)
            {
                // Сервер генерирует seed
                syncedSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
                GenerateTexture(syncedSeed);
            }
        }

        /// <summary>
        /// Hook вызывается когда seed синхронизируется с клиентом
        /// </summary>
        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            if (!NetworkServer.active && newSeed != 0)
            {
                // Клиент получил seed от сервера
                GenerateTexture(newSeed);
            }
        }

        /// <summary>
        /// Генерирует процедурную текстуру земли
        /// </summary>
        private void GenerateTexture(int useSeed)
        {
            Random.InitState(useSeed);
            
            // Случайные смещения для Perlin Noise
            float offsetX = Random.Range(0f, 10000f);
            float offsetY = Random.Range(0f, 10000f);
            
            generatedTexture = new Texture2D(textureWidth, textureHeight);
            generatedTexture.wrapMode = TextureWrapMode.Repeat;
            generatedTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // Основной шум для базового цвета
                    float xCoord = offsetX + (float)x / textureWidth * noiseScale;
                    float yCoord = offsetY + (float)y / textureHeight * noiseScale;
                    float baseNoise = Mathf.PerlinNoise(xCoord, yCoord);

                    // Детальный шум для текстуры травы
                    float xDetail = offsetX + (float)x / textureWidth * detailScale;
                    float yDetail = offsetY + (float)y / textureHeight * detailScale;
                    float detailNoise = Mathf.PerlinNoise(xDetail, yDetail);

                    // Определяем цвет
                    Color pixelColor;
                    
                    if (baseNoise > (1f - grassCoverage))
                    {
                        // Земля/грязь
                        pixelColor = Color.Lerp(dirtColor, grassColorDark, detailNoise * 0.3f);
                    }
                    else
                    {
                        // Трава с вариацией между тёмной и светлой
                        pixelColor = Color.Lerp(grassColorDark, grassColorLight, detailNoise);
                        
                        // Добавляем случайные тёмные пятна для реализма
                        if (detailNoise < 0.2f)
                        {
                            pixelColor = Color.Lerp(pixelColor, grassColorDark, 0.5f);
                        }
                    }

                    // Небольшое затемнение/осветление для разнообразия
                    float brightness = Mathf.Lerp(0.9f, 1.1f, Mathf.PerlinNoise(xDetail * 2f, yDetail * 2f));
                    pixelColor *= brightness;

                    pixels[y * textureWidth + x] = pixelColor;
                }
            }

            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply();

            // Применяем текстуру к материалу
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.mainTexture = generatedTexture;
                
                // Настраиваем tiling для повторения текстуры
                // Для plane размером 1000x1000 с текстурой 2048x2048
                float tiling = 10f; // Повторять текстуру 10 раз по каждой оси
                meshRenderer.material.mainTextureScale = new Vector2(tiling, tiling);
            }

            UnityEngine.Debug.Log($"[ProceduralGroundTexture] Generated {textureWidth}x{textureHeight} texture with seed {useSeed}");
        }

        /// <summary>
        /// Регенерировать текстуру (только сервер)
        /// </summary>
        [Server]
        public void RegenerateTexture()
        {
            syncedSeed = Random.Range(1, int.MaxValue);
            GenerateTexture(syncedSeed);
        }

        private void OnDestroy()
        {
            // Освобождаем текстуру из памяти
            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
            }
        }
    }
}
