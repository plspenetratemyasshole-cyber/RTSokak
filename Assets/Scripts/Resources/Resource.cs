using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace RTSokak.Resources
{
    public enum ResourceType
    {
        Wood,
        Ore,
        Food
    }

    /// <summary>
    /// Ресурсный объект. Один claim = один Worker.
    /// Lightweight: no NetworkTransform / Rigidbody.
    /// NavMeshObstacle carves once (stationary) so units path around resources.
    /// Теперь с системой здоровья -- требует несколько ударов для добычи.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(NavMeshObstacle))]
    public class Resource : NetworkBehaviour
    {
        [Header("Resource Settings")]
        [SerializeField] private ResourceType resourceType;
        [SerializeField] private int resourceAmount = 50;
        
        [Header("Health Settings")]
        [Tooltip("Сколько ударов нужно чтобы полностью добыть ресурс")]
        [SerializeField] private int maxHealth = 3;
        
        [Tooltip("Сколько ресурсов даётся за один удар (при maxHealth=3 и amount=50 -> 50/3=~16 за удар)")]
        [SerializeField] private bool splitAmountByHits = true;

        [Header("Grid Position")]
        public Vector2Int gridPosition;

        public ResourceType Type => resourceType;
        public int Amount => resourceAmount;
        public bool IsHarvested { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        private NetworkIdentity claimedBy;

        public bool IsClaimed => claimedBy != null;
        public bool IsAvailable => !IsHarvested && claimedBy == null;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            OptimizeStaticResource();
        }

        /// <summary>
        /// Strip expensive sync components, cheap visuals, block NavMesh once.
        /// </summary>
        public void OptimizeStaticResource()
        {
            // Resources never move — NetworkTransform only wastes CPU/bandwidth
            foreach (var nt in GetComponents<NetworkTransformBase>())
            {
                if (nt != null)
                    Destroy(nt);
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                Destroy(rb);

            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null)
                col = gameObject.AddComponent<BoxCollider>();

            // Block pathfinding: carve once when stationary (not every frame).
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
                obstacle = gameObject.AddComponent<NavMeshObstacle>();

            obstacle.enabled = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            // Slightly larger than collider so agents don't clip the mesh
            Vector3 size = col.size;
            size.x = Mathf.Max(0.4f, size.x * 1.05f);
            size.y = Mathf.Max(0.4f, size.y);
            size.z = Mathf.Max(0.4f, size.z * 1.05f);
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingMoveThreshold = 0.1f;
            obstacle.carvingTimeToStationary = 0.05f;

            // Keep default shadow casting (resources should cast shadows)
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r.sharedMaterial != null)
                    r.sharedMaterial.enableInstancing = true;
            }

            syncInterval = 0.5f;
        }

        public bool IsClaimedBy(NetworkIdentity workerIdentity)
        {
            return workerIdentity != null && claimedBy == workerIdentity;
        }

        [Server]
        public bool TryClaim(NetworkIdentity workerIdentity)
        {
            if (IsHarvested || workerIdentity == null)
                return false;

            if (claimedBy != null && claimedBy != workerIdentity)
                return false;

            claimedBy = workerIdentity;
            return true;
        }

        [Server]
        public void ReleaseClaim(NetworkIdentity workerIdentity)
        {
            if (workerIdentity != null && claimedBy == workerIdentity)
                claimedBy = null;
        }

        [Server]
        public void ForceReleaseClaim()
        {
            claimedBy = null;
        }

        /// <summary>
        /// Ударить по ресурсу. Возвращает количество ресурсов ЗА ЭТОТ УДАР.
        /// При достижении 0 HP ресурс уничтожается.
        /// Партиклы играются при КАЖДОМ ударе.
        /// </summary>
        [Server]
        public int Harvest()
        {
            if (IsHarvested || CurrentHealth <= 0)
                return 0;

            // Уменьшаем здоровье
            CurrentHealth--;

            // Вычисляем сколько ресурсов даём за этот удар
            int amountThisHit;
            if (splitAmountByHits)
            {
                // Равномерно делим общее количество на число ударов
                amountThisHit = Mathf.CeilToInt((float)resourceAmount / maxHealth);
            }
            else
            {
                // Весь объём ресурсов за каждый удар (OP режим)
                amountThisHit = resourceAmount;
            }

            // Воспроизводим партиклы при КАЖДОМ ударе
            Vector3 fxPos = GetCenterPosition();
            ResourceType type = resourceType;

            try
            {
                ResourceManager manager = FindFirstObjectByType<ResourceManager>();
                if (manager != null)
                {
                    manager.PlayHarvestParticles(type, fxPos);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            // Если здоровье кончилось -- уничтожаем
            if (CurrentHealth <= 0)
            {
                IsHarvested = true;
                claimedBy = null;
                HideLocally();

                try
                {
                    ResourceManager manager = FindFirstObjectByType<ResourceManager>();
                    if (manager != null)
                    {
                        manager.UnregisterResource(this);
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
                finally
                {
                    DestroyHarvestedObject();
                }
            }

            return amountThisHit;
        }

        private void HideLocally()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true))
                c.enabled = false;

            // Open path again after harvest
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.carving = false;
                obstacle.enabled = false;
            }
        }

        private void DestroyHarvestedObject()
        {
            if (isServer)
                NetworkServer.Destroy(gameObject);
            else
                Destroy(gameObject);
        }

        public Vector3 GetCenterPosition()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            if (col != null)
                return transform.TransformPoint(col.center);
            return transform.position;
        }

        public void SetGridPosition(int x, int y)
        {
            gridPosition = new Vector2Int(x, y);
        }

        public void SetResourceType(ResourceType type)
        {
            resourceType = type;
        }

        [Server]
        public void ServerInitFromSave(ResourceType type, Vector2Int grid, int amount)
        {
            resourceType = type;
            gridPosition = grid;
            resourceAmount = Mathf.Max(0, amount);
            IsHarvested = false;
            claimedBy = null;
            CurrentHealth = maxHealth; // Полное здоровье при загрузке
            OptimizeStaticResource();
        }
    }
}
