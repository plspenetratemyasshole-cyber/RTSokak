using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

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
        private List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        
        private float lastUpdateTime;
        private bool needsRebake = false;

        private void Start()
        {
            // Создаём NavMesh данные
            navMeshData = new NavMeshData();
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
            
            // Первичное запекание
            RebakeNavMesh();
        }

        private void Update()
        {
            if (autoUpdate && Time.time - lastUpdateTime > updateInterval)
            {
                if (needsRebake)
                {
                    RebakeNavMesh();
                    needsRebake = false;
                }
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Перезапекает NavMesh
        /// </summary>
        public void RebakeNavMesh()
        {
            sources.Clear();
            
            // Собираем все поверхности для ходьбы
            CollectWalkableSurfaces();
            
            // Собираем все препятствия
            CollectObstacles();
            
            // Настройки NavMesh
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            
            // Bounds для запекания
            Bounds bounds = new Bounds(transform.position, navMeshSize);
            
            // Запекаем NavMesh
            NavMeshBuilder.UpdateNavMeshData(navMeshData, settings, sources, bounds);
            
            UnityEngine.Debug.Log($"NavMesh rebaked with {sources.Count} sources");
        }

        /// <summary>
        /// Собирает поверхности для ходьбы
        /// </summary>
        private void CollectWalkableSurfaces()
        {
            // Находим все объекты на walkable слоях
            GameObject[] walkableObjects = FindObjectsInLayers(walkableLayers);
            
            foreach (GameObject obj in walkableObjects)
            {
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    NavMeshBuildSource source = new NavMeshBuildSource();
                    source.shape = NavMeshBuildSourceShape.Mesh;
                    source.sourceObject = meshFilter.sharedMesh;
                    source.transform = meshFilter.transform.localToWorldMatrix;
                    source.area = 0; // Walkable area
                    sources.Add(source);
                }
            }
        }

        /// <summary>
        /// Собирает препятствия
        /// </summary>
        private void CollectObstacles()
        {
            // Находим все объекты на obstacle слоях
            GameObject[] obstacleObjects = FindObjectsInLayers(obstacleLayers);
            
            foreach (GameObject obj in obstacleObjects)
            {
                Collider collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    NavMeshBuildSource source = new NavMeshBuildSource();
                    source.shape = NavMeshBuildSourceShape.Box;
                    source.transform = Matrix4x4.TRS(collider.bounds.center, obj.transform.rotation, collider.bounds.size);
                    source.area = 1; // Not walkable area
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

        /// <summary>
        /// Находит все объекты на указанных слоях
        /// </summary>
        private GameObject[] FindObjectsInLayers(LayerMask layers)
        {
            List<GameObject> result = new List<GameObject>();
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                if (((1 << obj.layer) & layers) != 0)
                {
                    result.Add(obj);
                }
            }
            
            return result.ToArray();
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
