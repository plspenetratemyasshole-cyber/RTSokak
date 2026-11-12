using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Универсальная система размножения префабов по карте с LOD
/// </summary>
public class PrefabGridSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Любой префаб который нужно размножить")]
    [SerializeField] private GameObject prefab;

    [Header("Map Settings")]
    [Tooltip("Размер карты в метрах")]
    [SerializeField] private Vector2 mapSize = new Vector2(1000, 1000);
    
    [Tooltip("Размер одной ячейки сетки")]
    [SerializeField] private float cellSize = 100f;

    [Header("Prefab Settings")]
    [Tooltip("Масштаб каждого экземпляра")]
    [SerializeField] private Vector3 prefabScale = new Vector3(10, 1, 10);
    
    [Tooltip("Высота Y для всех экземпляров")]
    [SerializeField] private float spawnHeight = 0f;

    [Header("LOD Settings")]
    [Tooltip("Максимальная дальность видимости (0 = без LOD)")]
    [SerializeField] private float maxDrawDistance = 500f;
    
    [Tooltip("Использовать LOD систему")]
    [SerializeField] private bool useLOD = true;
    
    [Tooltip("Обновлять видимость каждые N секунд")]
    [SerializeField] private float updateInterval = 0.5f;

    private List<SpawnedObject> spawnedObjects = new List<SpawnedObject>();
    private Transform cameraTransform;
    private float updateTimer;

    void Start()
    {
        if (prefab == null)
        {
            Debug.LogError("[PrefabGridSpawner] Prefab is not assigned!");
            return;
        }

        if (useLOD)
        {
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                Debug.LogWarning("[PrefabGridSpawner] Main Camera not found, LOD disabled");
                useLOD = false;
            }
        }

        GenerateGrid();
    }

    /// <summary>
    /// Генерирует сетку префабов по всей карте
    /// </summary>
    void GenerateGrid()
    {
        int cellsX = Mathf.CeilToInt(mapSize.x / cellSize);
        int cellsZ = Mathf.CeilToInt(mapSize.y / cellSize);

        Debug.Log($"[PrefabGridSpawner] Creating {cellsX}x{cellsZ} = {cellsX * cellsZ} instances");

        for (int x = 0; x < cellsX; x++)
        {
            for (int z = 0; z < cellsZ; z++)
            {
                SpawnInstance(x, z);
            }
        }

        Debug.Log($"[PrefabGridSpawner] Created {spawnedObjects.Count} instances");
    }

    /// <summary>
    /// Создаёт один экземпляр префаба
    /// </summary>
    void SpawnInstance(int x, int z)
    {
        Vector3 position = new Vector3(
            x * cellSize + cellSize / 2f,
            spawnHeight,
            z * cellSize + cellSize / 2f
        );

        GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
        instance.name = $"{prefab.name}_{x}_{z}";
        instance.transform.localScale = prefabScale;

        // Получаем все Renderer'ы (может быть несколько в дочерних объектах)
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();

        SpawnedObject obj = new SpawnedObject
        {
            gameObject = instance,
            renderers = renderers,
            position = position
        };
        spawnedObjects.Add(obj);
    }

    void Update()
    {
        if (!useLOD || cameraTransform == null) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateLOD();
        }
    }

    /// <summary>
    /// Обновляет видимость по дистанции
    /// </summary>
    void UpdateLOD()
    {
        Vector3 camPos = cameraTransform.position;

        foreach (var obj in spawnedObjects)
        {
            if (obj.gameObject == null) continue;

            float distance = Vector3.Distance(camPos, obj.position);
            bool shouldBeVisible = distance <= maxDrawDistance;

            // Включаем/выключаем все рендереры
            foreach (var renderer in obj.renderers)
            {
                if (renderer != null && renderer.enabled != shouldBeVisible)
                {
                    renderer.enabled = shouldBeVisible;
                }
            }
        }
    }

    /// <summary>
    /// Очищает все экземпляры
    /// </summary>
    public void ClearAll()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(obj.gameObject);
                else
                    DestroyImmediate(obj.gameObject);
            }
        }
        spawnedObjects.Clear();
        Debug.Log("[PrefabGridSpawner] All instances removed");
    }

    /// <summary>
    /// Пересоздаёт всё с нуля
    /// </summary>
    public void Regenerate()
    {
        ClearAll();
        GenerateGrid();
    }

    void OnDrawGizmosSelected()
    {
        // Границы карты
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(mapSize.x / 2f, spawnHeight, mapSize.y / 2f),
            new Vector3(mapSize.x, 0, mapSize.y)
        );

        // Дальность прорисовки
        if (useLOD && cameraTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cameraTransform.position, maxDrawDistance);
        }

        // Сетка ячеек
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        int cellsX = Mathf.CeilToInt(mapSize.x / cellSize);
        int cellsZ = Mathf.CeilToInt(mapSize.y / cellSize);

        for (int x = 0; x <= cellsX; x++)
        {
            float xPos = x * cellSize;
            Gizmos.DrawLine(
                new Vector3(xPos, spawnHeight, 0),
                new Vector3(xPos, spawnHeight, mapSize.y)
            );
        }

        for (int z = 0; z <= cellsZ; z++)
        {
            float zPos = z * cellSize;
            Gizmos.DrawLine(
                new Vector3(0, spawnHeight, zPos),
                new Vector3(mapSize.x, spawnHeight, zPos)
            );
        }
    }

    [System.Serializable]
    private class SpawnedObject
    {
        public GameObject gameObject;
        public Renderer[] renderers;
        public Vector3 position;
    }
}
