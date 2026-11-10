using System.Collections.Generic;
using Mirror;
using RTSokak.Shared;
using UnityEngine;

namespace RTSokak.Resources
{
    /// <summary>
    /// Управляет генерацией и спавном ресурсов на сервере
    /// </summary>
    public class ResourceManager : NetworkBehaviour
    {
        [Header("Generation")]
        [SerializeField] private ResourceGenerationConfig config;
        
        [Header("Generation Seed")]
        [Tooltip("0 = random seed on each start")]
        [SerializeField] private int overrideSeed = 0;
        
        [Header("Resource Randomization")]
        [Tooltip("Random Y rotation for all resources")]
        [SerializeField] private bool randomRotation = true;

        [Header("Harvest Particles")]
        [Tooltip("Префаб ParticleSystem для дерева (Looping = false)")]
        [SerializeField] private GameObject woodHarvestParticles;
        [Tooltip("Префаб ParticleSystem для руды (Looping = false)")]
        [SerializeField] private GameObject oreHarvestParticles;

        [SyncVar]
        private int worldSeed;

        private Dictionary<Vector2Int, Resource> spawnedResources = new Dictionary<Vector2Int, Resource>();
        private System.Random random;
        private bool skipAutoGenerate;

        public int WorldSeed => worldSeed;

        /// <summary>Размер мира по XZ (grid/world units). Для миникарты и границ.</summary>
        public int WorldSize => config != null ? Mathf.Max(1, config.worldSize) : 100;

        public void SetSkipAutoGenerate(bool skip) => skipAutoGenerate = skip;

        private void Start()
        {
            // Генерация происходит только на сервере
            // Используем NetworkServer.active вместо isServer для надёжности
            if (Mirror.NetworkServer.active)
            {
                if (skipAutoGenerate || RTSokak.Shared.WorldSession.ShouldRestoreFromSave)
                {
                    skipAutoGenerate = true;
                    UnityEngine.Debug.Log("[ResourceManager] Auto-generate skipped (loading save).");
                    return;
                }

                // Проверяем что config существует перед генерацией
                if (config == null)
                {
                    UnityEngine.Debug.LogError("ResourceManager: config is null! Skipping world generation. " +
                        "Please use Tools → RTS Setup Helper → '6. Create ResourceManager' to create properly configured ResourceManager.");
                    return;
                }
                
                GenerateWorld();
            }
        }

        [Server]
        public void ServerSetWorldSeed(int seed)
        {
            worldSeed = seed;
            random = new System.Random(seed);
        }

        [Server]
        public bool ServerSpawnResourceFromSave(ResourceSaveData save)
        {
            if (config == null || save == null)
                return false;

            GameObject prefab = save.resourceType == (int)ResourceType.Wood ? config.treePrefab : config.orePrefab;
            if (prefab == null)
                return false;

            Vector2Int grid = new Vector2Int(save.gridX, save.gridZ);
            if (spawnedResources.ContainsKey(grid))
                return false;

            Quaternion rot = Quaternion.Euler(save.eulerAngles);
            GameObject obj = Object.Instantiate(prefab, save.position, rot);
            NetworkServer.Spawn(obj);
            Resource resource = obj.GetComponent<Resource>();
            if (resource == null)
            {
                NetworkServer.Destroy(obj);
                return false;
            }

            resource.ServerInitFromSave((ResourceType)save.resourceType, grid, save.amount);
            spawnedResources[grid] = resource;
            return true;
        }

        public void RegisterSpawnedResource(Vector2Int grid, Resource resource)
        {
            if (resource != null)
                spawnedResources[grid] = resource;
        }

        /// <summary>
        /// Генерирует весь мир ресурсов
        /// </summary>
        [Server]
        private void GenerateWorld()
        {
            // Проверяем конфигурацию
            if (config == null)
            {
                UnityEngine.Debug.LogError("ResourceGenerationConfig is null! Cannot generate resources.");
                return;
            }

            if (config.treePrefab == null || config.orePrefab == null)
            {
                UnityEngine.Debug.LogError("Resource prefabs not assigned in ResourceGenerationConfig!");
                return;
            }

            // Определяем seed
            worldSeed = overrideSeed != 0 ? overrideSeed : UnityEngine.Random.Range(1, int.MaxValue);
            random = new System.Random(worldSeed);

            UnityEngine.Debug.Log($"Generating resource world with seed: {worldSeed}");

            // Генерируем леса
            GenerateDeposits(ResourceType.Wood, config.forestCount, 
                            config.minForestSize, config.maxForestSize, config.forestDensity);

            // Генерируем месторождения руды
            GenerateDeposits(ResourceType.Ore, config.oreDepositCount,
                            config.minOreSize, config.maxOreSize, config.oreDensity);

            UnityEngine.Debug.Log($"Generated {spawnedResources.Count} resources total");
        }

        /// <summary>
        /// Генерирует залежи определённого типа
        /// </summary>
        [Server]
        private void GenerateDeposits(ResourceType type, int count, int minSize, int maxSize, float density)
        {
            List<Vector2Int> depositCenters = new List<Vector2Int>();
            
            // Генерируем центры залежей
            for (int i = 0; i < count; i++)
            {
                Vector2Int center = FindValidDepositCenter(depositCenters, count);
                depositCenters.Add(center);

                // Создаём залежь
                ResourceDeposit deposit = new ResourceDeposit
                {
                    type = type,
                    centerPosition = center,
                    minSize = minSize,
                    maxSize = maxSize,
                    density = density
                };

                // Генерируем позиции ресурсов в залежи
                List<Vector2Int> positions = deposit.GeneratePositions(random);

                // Спавним ресурсы
                foreach (Vector2Int pos in positions)
                {
                    if (!spawnedResources.ContainsKey(pos))
                    {
                        SpawnResource(type, pos);
                    }
                }
            }
        }

        /// <summary>
        /// Находит валидную позицию для центра залежи
        /// </summary>
        private Vector2Int FindValidDepositCenter(List<Vector2Int> existingCenters, int totalDeposits)
        {
            int attempts = 0;
            int maxAttempts = 100;
            // Dense maps (hundreds of deposits) need smaller spacing
            int minDistance = Mathf.Max(4, config.worldSize / Mathf.Max(20, totalDeposits * 2));

            while (attempts < maxAttempts)
            {
                int x = random.Next(-config.worldSize / 2, config.worldSize / 2);
                int z = random.Next(-config.worldSize / 2, config.worldSize / 2);
                Vector2Int candidate = new Vector2Int(x, z);

                // Проверяем расстояние до существующих центров
                bool valid = true;
                foreach (Vector2Int existing in existingCenters)
                {
                    float distance = Vector2Int.Distance(candidate, existing);
                    if (distance < minDistance)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                    return candidate;

                attempts++;
            }

            // Если не нашли валидную позицию, возвращаем случайную
            return new Vector2Int(
                random.Next(-config.worldSize / 2, config.worldSize / 2),
                random.Next(-config.worldSize / 2, config.worldSize / 2)
            );
        }

        /// <summary>
        /// Спавнит один ресурс на grid позиции
        /// КРИТИЧНО: Проверяет что в этой клетке нет другого типа ресурса
        /// </summary>
        [Server]
        private void SpawnResource(ResourceType type, Vector2Int gridPos)
        {
            // ПРОВЕРКА: Есть ли уже ресурс в этой клетке?
            if (spawnedResources.TryGetValue(gridPos, out Resource existingResource))
            {
                // Если уже есть ресурс ДРУГОГО типа - пропускаем
                if (existingResource != null && existingResource.Type != type)
                {
                    // Ресурсы разных типов не могут быть в одной клетке
                    return;
                }
            }
            
            GameObject prefab = type == ResourceType.Wood ? config.treePrefab : config.orePrefab;
            float yOffset = type == ResourceType.Wood ? config.treeYOffset : config.oreYOffset;

            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning($"Prefab for {type} is not assigned!");
                return;
            }

            // Конвертируем grid координаты в world
            Vector3 worldPos = new Vector3(gridPos.x, yOffset, gridPos.y);

            // Рандомный поворот по Y
            Quaternion rotation = Quaternion.identity;
            if (randomRotation)
            {
                float randomYAngle = (float)(random.NextDouble() * 360.0);
                rotation = Quaternion.Euler(0, randomYAngle, 0);
            }

            // Создаём ресурс с поворотом
            GameObject resourceObj = Instantiate(prefab, worldPos, rotation);

            // КРИТИЧНО: Добавляем NetworkIdentity если его нет
            NetworkIdentity netId = resourceObj.GetComponent<NetworkIdentity>();
            if (netId == null)
            {
                netId = resourceObj.AddComponent<NetworkIdentity>();
                UnityEngine.Debug.Log($"Added NetworkIdentity to {type} resource at {gridPos}");
            }

            // Настраиваем компонент Resource
            Resource resource = resourceObj.GetComponent<Resource>();
            if (resource == null)
            {
                resource = resourceObj.AddComponent<Resource>();
            }
            resource.SetGridPosition(gridPos.x, gridPos.y);
            resource.OptimizeStaticResource();

            // Спавним на сервере и синхронизируем с клиентами
            NetworkServer.Spawn(resourceObj);

            // Сохраняем в словарь
            spawnedResources[gridPos] = resource;
        }

        /// <summary>
        /// Убрать ресурс из внутреннего словаря (вызывать перед Destroy).
        /// </summary>
        [Server]
        public void UnregisterResource(Resource resource)
        {
            if (resource == null) return;

            Vector2Int key = resource.gridPosition;
            if (spawnedResources.TryGetValue(key, out Resource existing) && existing == resource)
            {
                spawnedResources.Remove(key);
            }
            else
            {
                // Fallback: поиск по ссылке
                Vector2Int? found = null;
                foreach (var kvp in spawnedResources)
                {
                    if (kvp.Value == resource)
                    {
                        found = kvp.Key;
                        break;
                    }
                }

                if (found.HasValue)
                    spawnedResources.Remove(found.Value);
            }
        }

        /// <summary>
        /// Спавн партиклов добычи на всех клиентах.
        /// </summary>
        [Server]
        public void PlayHarvestParticles(ResourceType type, Vector3 worldPosition)
        {
            // Локально на хосте сразу (на случай если Rpc ещё не готов)
            SpawnHarvestParticlesLocal(type, worldPosition);

            // Рассылка клиентам, только если объект заспавнен в сети
            if (netIdentity != null && netIdentity.netId != 0 && NetworkServer.active)
            {
                RpcPlayHarvestParticles(type, worldPosition);
            }
        }

        [ClientRpc]
        private void RpcPlayHarvestParticles(ResourceType type, Vector3 worldPosition)
        {
            // На хосте уже сыграли в PlayHarvestParticles — не дублируем
            if (isServer)
                return;

            SpawnHarvestParticlesLocal(type, worldPosition);
        }

        private static Material cachedHdrpParticleMaterial;

        private void SpawnHarvestParticlesLocal(ResourceType type, Vector3 worldPosition)
        {
            try
            {
                GameObject prefab = type == ResourceType.Wood ? woodHarvestParticles : oreHarvestParticles;
                if (prefab == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ResourceManager] Harvest particles not assigned for {type}. " +
                        "Assign Wood/Ore Harvest Particles on ResourceManager.");
                    return;
                }

                GameObject instance = Instantiate(prefab, worldPosition, Quaternion.identity);
                EnsureBuiltinParticleMaterials(instance);

                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = false;
                    ps.Clear(true);
                    ps.Play(true);
                }

                float lifetime = EstimateParticleLifetime(instance);
                Destroy(instance, lifetime);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static void EnsureBuiltinParticleMaterials(GameObject root)
        {
            // УБРАНО: Принудительная замена материала на белый
            // Теперь используем цвета из префаба
            // Материал заменяется только если он null или HDRP-специфичный
            
            foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                // Проверяем нужна ли замена материала
                bool needsReplacement = renderer.sharedMaterial == null 
                    || renderer.sharedMaterial.shader == null
                    || renderer.sharedMaterial.shader.name.Contains("HDRP");
                
                if (needsReplacement)
                {
                    Material mat = GetBuiltinParticleMaterial();
                    if (mat != null)
                    {
                        renderer.sharedMaterial = mat;
                    }
                }
                
                // Trail material оставляем без изменений если работает
                if (renderer.trailMaterial != null && renderer.trailMaterial.shader == null)
                {
                    Material mat = GetBuiltinParticleMaterial();
                    if (mat != null)
                    {
                        renderer.trailMaterial = mat;
                    }
                }
            }
        }

        private static Material GetBuiltinParticleMaterial()
        {
            if (cachedHdrpParticleMaterial != null)
                return cachedHdrpParticleMaterial;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            cachedHdrpParticleMaterial = new Material(shader);
            cachedHdrpParticleMaterial.name = "HarvestParticleBuiltin";
            
            // УБРАНО: Установка белого цвета
            // Оставляем цвет по умолчанию из shader'а
            // if (cachedHdrpParticleMaterial.HasProperty("_Color"))
            //     cachedHdrpParticleMaterial.SetColor("_Color", Color.white);
            // if (cachedHdrpParticleMaterial.HasProperty("_TintColor"))
            //     cachedHdrpParticleMaterial.SetColor("_TintColor", Color.white);
            
            cachedHdrpParticleMaterial.renderQueue = 3000;
            return cachedHdrpParticleMaterial;
        }

        private static float EstimateParticleLifetime(GameObject root)
        {
            float maxLife = 3f;
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    float startMax = 1f;
                    switch (main.startLifetime.mode)
                    {
                        case ParticleSystemCurveMode.TwoConstants:
                            startMax = main.startLifetime.constantMax;
                            break;
                        case ParticleSystemCurveMode.Constant:
                            startMax = main.startLifetime.constant;
                            break;
                        default:
                            startMax = Mathf.Max(main.startLifetime.constant, main.startLifetime.constantMax);
                            break;
                    }

                    float delay = 0f;
                    switch (main.startDelay.mode)
                    {
                        case ParticleSystemCurveMode.TwoConstants:
                            delay = main.startDelay.constantMax;
                            break;
                        default:
                            delay = main.startDelay.constant;
                            break;
                    }

                    float life = main.duration + startMax + delay;
                    if (life > maxLife)
                        maxLife = life;
                }
            }
            catch
            {
                maxLife = 5f;
            }

            return maxLife + 0.5f;
        }

        /// <summary>
        /// Получить ресурс на grid позиции
        /// </summary>
        public Resource GetResourceAt(Vector2Int gridPos)
        {
            spawnedResources.TryGetValue(gridPos, out Resource resource);
            return resource;
        }

        /// <summary>
        /// Получить все ресурсы определённого типа в радиусе.
        /// onlyAvailable: пропускает уже занятые (claimed) другими юнитами.
        /// </summary>
        public List<Resource> GetResourcesInRadius(Vector3 worldPosition, float radius, ResourceType type, bool onlyAvailable = false)
        {
            List<Resource> result = new List<Resource>();
            
            Collider[] colliders = Physics.OverlapSphere(worldPosition, radius);
            
            foreach (Collider col in colliders)
            {
                Resource resource = col.GetComponent<Resource>();
                if (resource == null || resource.IsHarvested || resource.Type != type)
                    continue;

                if (onlyAvailable && resource.IsClaimed)
                    continue;

                result.Add(resource);
            }

            return result;
        }

        /// <summary>
        /// Ближайший свободный ресурс типа type относительно точки (клик / позиция юнита).
        /// allowClaimedBy — ресурс, уже занятый этим worker'ом, тоже подходит.
        /// </summary>
        public Resource GetNearestAvailableResource(
            Vector3 nearPoint,
            ResourceType type,
            float radius,
            NetworkIdentity allowClaimedBy = null)
        {
            Resource best = null;
            float bestDist = float.MaxValue;

            Collider[] colliders = Physics.OverlapSphere(nearPoint, radius);
            foreach (Collider col in colliders)
            {
                Resource resource = col.GetComponent<Resource>();
                if (resource == null || resource.IsHarvested || resource.Type != type)
                    continue;

                if (resource.IsClaimed && !resource.IsClaimedBy(allowClaimedBy))
                    continue;

                float dist = Vector3.Distance(nearPoint, resource.GetCenterPosition());
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = resource;
                }
            }

            return best;
        }
    }
}
