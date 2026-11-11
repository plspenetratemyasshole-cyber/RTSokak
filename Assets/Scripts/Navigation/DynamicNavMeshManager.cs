using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using RTSokak.Buildings;

namespace RTSokak.Navigation
{
    /// <summary>
    /// Динамический NavMesh для RTS игры
    /// Автоматически обновляет NavMesh когда появляются/исчезают здания
    /// </summary>
    public class DynamicNavMeshManager : MonoBehaviour
    {
        [Header("NavMesh Settings")]
        [SerializeField] private Vector3 navMeshSize = new Vector3(200, 10, 200);
        [SerializeField] private LayerMask walkableLayers;
        [SerializeField] private LayerMask obstacleLayers;
        
        [Header("Auto-Update")]
        [SerializeField] private bool autoUpdate = true;
        [SerializeField] private float updateInterval = 1f; // Обновлять каждую секунду
        
        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshInstance;
        private readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private readonly List<NavMeshBuildSource> walkableSources = new List<NavMeshBuildSource>();
        private AsyncOperation bakeOperation;
        
        private float lastUpdateTime;
        private bool needsRebake = false;

        private void Start()
        {
            // Создаём NavMesh данные
            navMeshData = new NavMeshData();
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
            
            // Кэшируем поверхности для ходьбы один раз при старте
            CacheWalkableSurfaces();

            // Первичное запекание
            RebakeNavMesh();
        }

        private void Update()
        {
            if (autoUpdate && Time.time - lastUpdateTime > updateInterval)
            {
                if (needsRebake && (bakeOperation == null || bakeOperation.isDone))
                {
                    needsRebake = false;
                    RebakeNavMesh();
                }
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Перезапекает NavMesh асинхронно
        /// </summary>
        public void RebakeNavMesh()
        {
            if (bakeOperation != null && !bakeOperation.isDone)
            {
                needsRebake = true;
                return;
            }

            sources.Clear();
            sources.AddRange(walkableSources);
            
            // Собираем все препятствия (здания)
            CollectObstacles();
            
            // Настройки NavMesh
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            
            // Bounds для запекания
            Bounds bounds = new Bounds(transform.position, navMeshSize);
            
            // Запекаем NavMesh асинхронно
            bakeOperation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settings, sources, bounds);
        }

        /// <summary>
        /// Кэширует поверхности для ходьбы один раз при старте
        /// </summary>
        private void CacheWalkableSurfaces()
        {
            walkableSources.Clear();
            var allFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allFilters.Length; i++)
            {
                MeshFilter meshFilter = allFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                if (((1 << meshFilter.gameObject.layer) & walkableLayers) != 0)
                {
                    NavMeshBuildSource source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = meshFilter.sharedMesh,
                        transform = meshFilter.transform.localToWorldMatrix,
                        area = 0 // Walkable area
                    };
                    walkableSources.Add(source);
                }
            }
        }

        /// <summary>
        /// Собирает препятствия из активных зданий
        /// </summary>
        private void CollectObstacles()
        {
            var buildings = Building.AllBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                if (b == null) continue;

                Collider collider = b.GetComponent<Collider>();
                if (collider != null)
                {
                    NavMeshBuildSource source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        transform = Matrix4x4.TRS(collider.bounds.center, b.transform.rotation, collider.bounds.size),
                        area = 1 // Not walkable area
                    };
                    sources.Add(source);
                }
            }
        }

        /// <summary>
        /// Помечает что NavMesh нужно обновить
        /// </summary>
        public void MarkDirty()
        {
            needsRebake = true;
        }

        private void OnDestroy()
        {
            if (navMeshInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshInstance);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, navMeshSize);
        }
    }
}
