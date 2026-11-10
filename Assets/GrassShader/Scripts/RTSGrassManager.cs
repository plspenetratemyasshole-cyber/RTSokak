using UnityEngine;

namespace RTSokak.Grass
{
    /// <summary>
    /// Управляет травой для RTS игры: создание, настройка и оптимизация
    /// </summary>
    public class RTSGrassManager : MonoBehaviour
    {
        [Header("Grass Settings")]
        [Tooltip("Материал травы: Assets/GrassShader/Materials/GeometryGrass.mat")]
        [SerializeField] private Material grassMaterial;
        
        [Tooltip("ОБЯЗАТЕЛЬНО! Модель с высокой плотностью вершин: Assets/GrassShader/Models/GroundPlane.fbx")]
        [SerializeField] private GameObject grassPlanePrefab;
        
        [SerializeField] private Vector2Int mapSize = new Vector2Int(100, 100);
        [SerializeField] private int chunkSize = 50;

        [Header("Grass Appearance")]
        [SerializeField] private Color grassColorBottom = new Color(0.24f, 0.47f, 0.16f); // #3D7828
        [SerializeField] private Color grassColorTop = new Color(0.47f, 0.78f, 0.31f); // #78C74F
        
        [Tooltip("Высота травинок в метрах")]
        [SerializeField] private float grassHeight = 0.3f;  // УМЕНЬШЕНО с 0.5 до 0.3
        
        [Tooltip("Ширина травинок")]
        [SerializeField] private float grassWidth = 0.05f;  // УМЕНЬШЕНО с 0.1 до 0.05
        
        [Tooltip("Плотность (tessellation) - чем больше, тем гуще трава. Для RTS: 8-15")]
        [SerializeField] private float grassDensity = 10f;  // УМЕНЬШЕНО с 15 до 10

        [Header("Wind Settings")]
        [SerializeField] private float windSpeed = 0.8f;
        [SerializeField] private float windStrength = 0.2f;
        [SerializeField] private Vector2 windDirection = new Vector2(1, 0);

        [Header("Optimization")]
        [SerializeField] private bool enableLOD = true;
        [SerializeField] private float lodDistance = 100f;
        [SerializeField] private bool castShadows = false;
        
        private GameObject grassContainer;

        void Start()
        {
            // Локальная визуализация — должна работать и у хоста, и у клиента
            if (grassMaterial == null)
            {
                UnityEngine.Debug.LogError("[RTSGrassManager] Grass Material не назначен! Назначь Materials/GeometryGrass.mat");
                return;
            }

            CreateGrassSystem();
        }

        /// <summary>
        /// Создаёт систему травы на карте
        /// </summary>
        public void CreateGrassSystem()
        {
            // Контейнер для всех чанков травы
            grassContainer = new GameObject("GrassSystem");
            grassContainer.transform.SetParent(transform);

            // Применяем настройки к материалу
            ApplyGrassSettings();

            // Создаём плоскость с травой
            CreateGrassPlane();

            UnityEngine.Debug.Log($"[RTSGrassManager] Система травы создана: {mapSize.x}x{mapSize.y}m, плотность: {grassDensity}");
        }

        /// <summary>
        /// Применяет настройки к материалу травы
        /// </summary>
        void ApplyGrassSettings()
        {
            if (grassMaterial == null) return;

            // ВАЖНО: Используем правильные имена из shader!
            
            // Цвета (если есть в shader)
            if (grassMaterial.HasProperty("_ColorBottom"))
                grassMaterial.SetColor("_ColorBottom", grassColorBottom);
            if (grassMaterial.HasProperty("_ColorTop"))
                grassMaterial.SetColor("_ColorTop", grassColorTop);

            // Размеры (правильные имена из GeometryGrassShader)
            grassMaterial.SetFloat("_BladeHeight", grassHeight);        // НЕ _GrassHeight!
            grassMaterial.SetFloat("_BladeWidth", grassWidth);          // НЕ _GrassWidth!
            
            // Плотность через Tessellation
            if (grassMaterial.HasProperty("_TessellationUniform"))
                grassMaterial.SetFloat("_TessellationUniform", grassDensity);

            // Ветер
            grassMaterial.SetFloat("_WindStrength", windStrength);
            grassMaterial.SetVector("_WindFrequency", new Vector4(windDirection.x * windSpeed, windDirection.y * windSpeed, 0, 0));

            // Убрать маску чтобы трава росла везде
            if (grassMaterial.HasProperty("_GrassMaskThreshold"))
                grassMaterial.SetFloat("_GrassMaskThreshold", 0.0f);  // 0 = трава везде
        }

        /// <summary>
        /// Создаёт плоскость земли с травой
        /// </summary>
        void CreateGrassPlane()
        {
            if (grassPlanePrefab == null)
            {
                UnityEngine.Debug.LogError("[RTSGrassManager] Grass Plane Prefab не назначен! Перетащи Assets/GrassShader/Models/GroundPlane.fbx в Inspector");
                return;
            }

            // Создаём экземпляр GroundPlane
            GameObject grassPlane = Instantiate(grassPlanePrefab, grassContainer.transform);
            grassPlane.name = "GrassPlane";
            grassPlane.transform.localPosition = new Vector3(mapSize.x / 2f, 0, mapSize.y / 2f);
            grassPlane.transform.localScale = new Vector3(mapSize.x / 10f, 1, mapSize.y / 10f);

            // Применяем материал
            Renderer renderer = grassPlane.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = grassPlane.GetComponentInChildren<Renderer>();
            }
            
            if (renderer != null)
            {
                renderer.material = grassMaterial;
                renderer.shadowCastingMode = castShadows 
                    ? UnityEngine.Rendering.ShadowCastingMode.On 
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                UnityEngine.Debug.LogError("[RTSGrassManager] Renderer не найден на GroundPlane!");
            }

            // Добавляем коллайдер
            if (grassPlane.GetComponent<Collider>() == null && grassPlane.GetComponentInChildren<Collider>() == null)
            {
                MeshCollider collider = grassPlane.AddComponent<MeshCollider>();
                MeshFilter meshFilter = grassPlane.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = grassPlane.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    collider.sharedMesh = meshFilter.sharedMesh;
                }
            }
        }

        /// <summary>
        /// Обновляет настройки травы в реальном времени (для Editor)
        /// </summary>
        public void UpdateGrassSettings()
        {
            ApplyGrassSettings();
        }

        /// <summary>
        /// Устанавливает плотность травы (для настроек графики)
        /// </summary>
        public void SetGrassDensity(float density)
        {
            grassDensity = Mathf.Clamp(density, 5f, 50f);
            if (grassMaterial != null && grassMaterial.HasProperty("_TessellationUniform"))
            {
                grassMaterial.SetFloat("_TessellationUniform", grassDensity);
            }
        }

        /// <summary>
        /// Устанавливает качество (Low/Medium/High)
        /// </summary>
        public void SetGrassQuality(string quality)
        {
            switch (quality.ToLower())
            {
                case "low":
                    SetGrassDensity(8f);      // Tessellation
                    grassHeight = 0.25f;       // Низкая трава
                    grassWidth = 0.04f;
                    lodDistance = 50f;
                    break;
                case "medium":
                    SetGrassDensity(10f);     // Tessellation
                    grassHeight = 0.3f;        // Средняя трава
                    grassWidth = 0.05f;
                    lodDistance = 100f;
                    break;
                case "high":
                    SetGrassDensity(15f);     // Tessellation
                    grassHeight = 0.4f;        // Высокая трава
                    grassWidth = 0.06f;
                    lodDistance = 150f;
                    break;
            }
            ApplyGrassSettings();
        }

        void OnValidate()
        {
            // Обновляем настройки в Editor при изменении
            if (Application.isPlaying && grassMaterial != null)
            {
                ApplyGrassSettings();
            }
        }
    }
}
