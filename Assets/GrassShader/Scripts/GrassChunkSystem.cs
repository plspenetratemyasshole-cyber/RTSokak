using UnityEngine;
using System.Collections.Generic;

namespace RTSokak.Grass
{
    /// <summary>
    /// Система чанков травы для больших карт с LOD
    /// </summary>
    public class GrassChunkSystem : MonoBehaviour
    {
        [Header("Grass Prefab")]
        [Tooltip("Префаб: Assets/GrassShader/Models/GroundPlane.fbx")]
        [SerializeField] private GameObject grassPrefab;
        
        [Tooltip("Материал травы: Assets/GrassShader/Materials/GeometryGrass.mat")]
        [SerializeField] private Material grassMaterial;

        [Header("Map Settings")]
        [Tooltip("Размер карты в метрах")]
        [SerializeField] private Vector2 mapSize = new Vector2(1000, 1000);
        
        [Tooltip("Размер одного чанка в метрах")]
        [SerializeField] private float chunkSize = 100f;

        [Header("Chunk Prefab Scale")]
        [Tooltip("Масштаб каждого префаба (по умолчанию 10 = 100x100м)")]
        [SerializeField] private Vector3 chunkScale = new Vector3(10, 1, 10);

        [Header("LOD Settings")]
        [Tooltip("Максимальная дальность видимости травы")]
        [SerializeField] private float maxDrawDistance = 500f;
        
        [Tooltip("Обновлять видимость каждые N секунд")]
        [SerializeField] private float updateInterval = 0.5f;

        [Header("Grass Appearance")]
        [SerializeField] private float bladeHeight = 0.25f;
        [SerializeField] private float bladeWidth = 0.04f;
        [SerializeField] private float tessellation = 8f;
        [SerializeField] private float windStrength = 0.5f;

        private List<GrassChunk> chunks = new List<GrassChunk>();
        private Transform cameraTransform;
        private float updateTimer;

        void Start()
        {
            if (grassPrefab == null)
            {
                UnityEngine.Debug.LogError("[GrassChunkSystem] Grass Prefab не назначен! Назначь GroundPlane.fbx");
                return;
            }

            if (grassMaterial == null)
            {
                UnityEngine.Debug.LogError("[GrassChunkSystem] Grass Material не назначен! Назначь GeometryGrass.mat");
                return;
            }

            cameraTransform = UnityEngine.Camera.main?.transform;
            if (cameraTransform == null)
            {
                UnityEngine.Debug.LogError("[GrassChunkSystem] Main Camera не найдена!");
                return;
            }

            GenerateChunks();
            ApplyGrassSettings();
        }

        /// <summary>
        /// Генерирует сетку чанков по всей карте
        /// </summary>
        void GenerateChunks()
        {
            int chunksX = Mathf.CeilToInt(mapSize.x / chunkSize);
            int chunksZ = Mathf.CeilToInt(mapSize.y / chunkSize);

            UnityEngine.Debug.Log($"[GrassChunkSystem] Создаём {chunksX}x{chunksZ} = {chunksX * chunksZ} чанков");

            for (int x = 0; x < chunksX; x++)
            {
                for (int z = 0; z < chunksZ; z++)
                {
                    CreateChunk(x, z);
                }
            }

            UnityEngine.Debug.Log($"[GrassChunkSystem] Создано {chunks.Count} чанков травы");
        }

        /// <summary>
        /// Создаёт один чанк травы
        /// </summary>
        void CreateChunk(int x, int z)
        {
            // Позиция чанка
            Vector3 position = new Vector3(
                x * chunkSize + chunkSize / 2f,
                0,
                z * chunkSize + chunkSize / 2f
            );

            // Создаём экземпляр
            GameObject chunkObj = Instantiate(grassPrefab, position, Quaternion.identity, transform);
            chunkObj.name = $"GrassChunk_{x}_{z}";
            chunkObj.transform.localScale = chunkScale;

            // Применяем материал
            Renderer renderer = chunkObj.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = chunkObj.GetComponentInChildren<Renderer>();
            }

            if (renderer != null)
            {
                renderer.material = grassMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Добавляем в список
            GrassChunk chunk = new GrassChunk
            {
                gameObject = chunkObj,
                renderer = renderer,
                position = position
            };
            chunks.Add(chunk);
        }

        /// <summary>
        /// Применяет настройки к материалу
        /// </summary>
        void ApplyGrassSettings()
        {
            if (grassMaterial == null) return;

            grassMaterial.SetFloat("_BladeHeight", bladeHeight);
            grassMaterial.SetFloat("_BladeWidth", bladeWidth);
            
            if (grassMaterial.HasProperty("_TessellationUniform"))
                grassMaterial.SetFloat("_TessellationUniform", tessellation);

            grassMaterial.SetFloat("_WindStrength", windStrength);

            // Убрать маску
            if (grassMaterial.HasProperty("_GrassMaskThreshold"))
                grassMaterial.SetFloat("_GrassMaskThreshold", 0f);

            UnityEngine.Debug.Log($"[GrassChunkSystem] Применены настройки травы: Height={bladeHeight}, Width={bladeWidth}, Tess={tessellation}");
        }

        void Update()
        {
            if (cameraTransform == null) return;

            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateChunkVisibility();
            }
        }

        /// <summary>
        /// Обновляет видимость чанков по дистанции
        /// </summary>
        void UpdateChunkVisibility()
        {
            Vector3 camPos = cameraTransform.position;

            foreach (var chunk in chunks)
            {
                float distance = Vector3.Distance(camPos, chunk.position);
                bool shouldBeVisible = distance <= maxDrawDistance;

                if (chunk.renderer != null && chunk.renderer.enabled != shouldBeVisible)
                {
                    chunk.renderer.enabled = shouldBeVisible;
                }
            }
        }

        /// <summary>
        /// Обновляет настройки в реальном времени (для Editor)
        /// </summary>
        public void UpdateSettings()
        {
            ApplyGrassSettings();
        }

        /// <summary>
        /// Очищает все чанки
        /// </summary>
        public void ClearChunks()
        {
            foreach (var chunk in chunks)
            {
                if (chunk.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(chunk.gameObject);
                    else
                        DestroyImmediate(chunk.gameObject);
                }
            }
            chunks.Clear();
            UnityEngine.Debug.Log("[GrassChunkSystem] Все чанки удалены");
        }

        /// <summary>
        /// Пересоздаёт все чанки
        /// </summary>
        public void RegenerateChunks()
        {
            ClearChunks();
            GenerateChunks();
            ApplyGrassSettings();
        }

        void OnValidate()
        {
            // Обновляем настройки в Editor
            if (Application.isPlaying && grassMaterial != null)
            {
                ApplyGrassSettings();
            }
        }

        void OnDrawGizmosSelected()
        {
            // Рисуем границы карты
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3(mapSize.x / 2f, 0, mapSize.y / 2f),
                new Vector3(mapSize.x, 0, mapSize.y)
            );

            // Рисуем дистанцию прорисовки
            if (cameraTransform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(cameraTransform.position, maxDrawDistance);
            }
        }

        [System.Serializable]
        private class GrassChunk
        {
            public GameObject gameObject;
            public Renderer renderer;
            public Vector3 position;
        }
    }
}
