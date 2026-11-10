using UnityEngine;
using Mirror;
using System.Collections.Generic;
#if UNITY_VFX_GRAPH
using UnityEngine.VFX;
#endif

namespace RTSokak.Terrain
{
    public enum GrassMode
    {
        Prefab,
        VFXGraph
    }
    
    /// <summary>
    /// Локальная процедурная трава. Seed приходит с сервера (SyncVar или GrassSeedMessage).
    /// Каждый пир генерирует свои инстансы — трава не сетевая.
    /// </summary>
    public class ProceduralGrassGenerator : NetworkBehaviour
    {
        const string ResourcesGrassPath = "Terrain/Grass";

        [Header("Generation Mode")]
        [SerializeField] private GrassMode grassMode = GrassMode.Prefab;
        
        [Header("Prefab Mode Settings")]
        [SerializeField] private GameObject grassPrefab;
        [SerializeField] private int grassCount = 5000;
        
        [Header("VFX Graph Mode Settings")]
        [SerializeField] private GameObject vfxGrassPrefab;
        [SerializeField] private Vector2 vfxAreaSize = new Vector2(500f, 500f);
        [SerializeField] private float worldSize = 500f;
        
        [Header("Randomization")]
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 1.5f;
        [SerializeField] private bool randomRotation = true;
        
        [Header("Distribution")]
        [SerializeField] private float clumpiness = 0.5f;
        [SerializeField] private float noiseScale = 50f;
        
        [Header("Exclusion Zones")]
        [SerializeField] private float resourceExclusionRadius = 2f;
        
        [Header("Network Sync")]
        [SyncVar(hook = nameof(OnSeedChanged))]
        private int grassSeed;

        private List<GameObject> spawnedGrass = new List<GameObject>();
        private System.Random random;
        private bool generated;
        private static bool staticHandlerRegistered;
        private static int pendingSeed;
        private float nextRetryTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            staticHandlerRegistered = false;
            pendingSeed = 0;
        }

        private void Awake()
        {
            EnsurePrefab();
            EnsureStaticHandler();
        }

        private void OnEnable()
        {
            EnsureStaticHandler();
            if (pendingSeed != 0 && !generated)
                ApplySeed(pendingSeed);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (grassSeed == 0)
                grassSeed = Random.Range(1, int.MaxValue);
            GenerateGrass();
            ServerBroadcastSeed();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (isServer)
                return;

            EnsurePrefab();
            if (grassSeed != 0)
                GenerateGrass();
            else if (pendingSeed != 0)
                ApplySeed(pendingSeed);
        }

        private void Start()
        {
            EnsurePrefab();
            EnsureStaticHandler();

            if (!NetworkClient.active && !NetworkServer.active)
            {
                if (grassSeed == 0)
                    grassSeed = Random.Range(1, int.MaxValue);
                GenerateGrass();
                return;
            }

            if (NetworkServer.active && !generated)
            {
                if (grassSeed == 0)
                    grassSeed = Random.Range(1, int.MaxValue);
                GenerateGrass();
                ServerBroadcastSeed();
            }
            else if (NetworkClient.active && !NetworkServer.active && !generated)
            {
                if (grassSeed != 0)
                    GenerateGrass();
                else if (pendingSeed != 0)
                    ApplySeed(pendingSeed);
            }
        }

        private void Update()
        {
            // Клиент: если трава ещё не появилась — ретрай (prefab/seed могли прийти позже)
            if (generated || NetworkServer.active && !NetworkClient.active)
                return;
            if (!NetworkClient.active)
                return;
            if (Time.unscaledTime < nextRetryTime)
                return;

            nextRetryTime = Time.unscaledTime + 1f;
            EnsurePrefab();

            int seed = grassSeed != 0 ? grassSeed : pendingSeed;
            if (seed != 0 && grassPrefab != null)
                ApplySeed(seed);
        }

        static void EnsureStaticHandler()
        {
            if (staticHandlerRegistered)
                return;
            if (!NetworkClient.active && NetworkServer.active)
            {
                // На чистом сервере handler клиенту не нужен, но listen-host тоже клиент
            }
            NetworkClient.RegisterHandler<GrassSeedMessage>(OnStaticGrassSeedMessage, false);
            staticHandlerRegistered = true;
        }

        static void OnStaticGrassSeedMessage(GrassSeedMessage msg)
        {
            if (msg.seed == 0)
                return;

            pendingSeed = msg.seed;

            var gens = Object.FindObjectsByType<ProceduralGrassGenerator>(FindObjectsSortMode.None);
            if (gens == null || gens.Length == 0)
            {
                // В сцене нет генератора — создаём локальный визуальный (без NetworkBehaviour)
                if (NetworkClient.active && !NetworkServer.active)
                    ClientLocalGrass.EnsureAndGenerate(msg.seed);
                return;
            }

            for (int i = 0; i < gens.Length; i++)
            {
                if (gens[i] != null)
                    gens[i].ApplySeed(msg.seed);
            }
        }

        [Server]
        public void ServerBroadcastSeed()
        {
            if (grassSeed == 0)
                grassSeed = Random.Range(1, int.MaxValue);
            pendingSeed = grassSeed;
            NetworkServer.SendToAll(new GrassSeedMessage { seed = grassSeed });
        }

        public void ApplySeed(int seed)
        {
            if (seed == 0)
                return;
            if (generated && grassSeed == seed)
                return;

            grassSeed = seed;
            pendingSeed = seed;
            GenerateGrass();
        }

        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            if (newSeed == 0)
                return;
            if (isServer && generated)
                return;
            ApplySeed(newSeed);
        }

        private void EnsurePrefab()
        {
            if (grassPrefab != null)
                return;

            grassPrefab = global::UnityEngine.Resources.Load<GameObject>(ResourcesGrassPath);
            if (grassPrefab == null)
            {
                global::UnityEngine.Debug.LogWarning(
                    $"[ProceduralGrassGenerator] grassPrefab missing — put prefab at Resources/{ResourcesGrassPath}");
            }
        }

        private void GenerateGrass()
        {
            if (grassSeed == 0)
                return;

            EnsurePrefab();

            if (grassMode == GrassMode.Prefab || grassMode == GrassMode.VFXGraph)
            {
                if (grassMode == GrassMode.VFXGraph)
                {
                    UnityEngine.Debug.LogWarning(
                        "[ProceduralGrassGenerator] VFX Graph unavailable on Built-in RP — Prefab mode.");
                }
                GeneratePrefabGrass();
            }

            generated = spawnedGrass.Count > 0 || grassPrefab == null;
        }

        private void GeneratePrefabGrass()
        {
            if (grassPrefab == null)
            {
                UnityEngine.Debug.LogWarning("[ProceduralGrassGenerator] Grass prefab not assigned!");
                generated = false;
                return;
            }

            random = new System.Random(grassSeed);
            
            foreach (GameObject grass in spawnedGrass)
            {
                if (grass != null)
                    Destroy(grass);
            }
            spawnedGrass.Clear();

            float offsetX = (float)(random.NextDouble() * 10000);
            float offsetY = (float)(random.NextDouble() * 10000);

            // На клиенте ресурсы могут ещё не приехать — не режем всю траву exclusion'ом
            bool checkResources = NetworkServer.active ||
                                  Object.FindFirstObjectByType<Resources.Resource>() != null;

            int spawned = 0;
            int attempts = 0;
            int maxAttempts = grassCount * 3;

            while (spawned < grassCount && attempts < maxAttempts)
            {
                attempts++;

                float x = Mathf.Lerp(-worldSize / 2f, worldSize / 2f, (float)random.NextDouble());
                float z = Mathf.Lerp(-worldSize / 2f, worldSize / 2f, (float)random.NextDouble());

                float noiseValue = Mathf.PerlinNoise(
                    offsetX + x / noiseScale,
                    offsetY + z / noiseScale
                );

                if (noiseValue < clumpiness)
                    continue;

                Vector3 position = new Vector3(x, 0f, z);

                if (checkResources && IsNearResource(position))
                    continue;

                SpawnGrass(position);
                spawned++;
            }

            UnityEngine.Debug.Log(
                $"[ProceduralGrassGenerator] Spawned {spawned} grass (seed={grassSeed}, " +
                $"server={NetworkServer.active}, client={NetworkClient.active}, prefab={grassPrefab.name})");
        }

        private void SpawnGrass(Vector3 position)
        {
            Quaternion rotation = Quaternion.identity;
            if (randomRotation)
            {
                float angle = (float)(random.NextDouble() * 360.0);
                rotation = Quaternion.Euler(0, angle, 0);
            }

            GameObject grass = Instantiate(grassPrefab, position, rotation, transform);
            float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
            grass.transform.localScale = Vector3.one * scale;
            spawnedGrass.Add(grass);
        }

        private bool IsNearResource(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, resourceExclusionRadius);
            foreach (Collider col in colliders)
            {
                if (col != null && col.GetComponent<Resources.Resource>() != null)
                    return true;
            }
            return false;
        }

        [Server]
        public void RegenerateGrass()
        {
            generated = false;
            grassSeed = Random.Range(1, int.MaxValue);
            GenerateGrass();
            ServerBroadcastSeed();
        }

        private void OnDestroy()
        {
            foreach (GameObject grass in spawnedGrass)
            {
                if (grass != null)
                    Destroy(grass);
            }
            spawnedGrass.Clear();
        }

        /// <summary>FPS юнит-игрок: скрыть ~5k инстансов травы (главный GPU удар).</summary>
        public void SetFpsHidden(bool hidden)
        {
            for (int i = 0; i < spawnedGrass.Count; i++)
            {
                if (spawnedGrass[i] != null)
                    spawnedGrass[i].SetActive(!hidden);
            }
        }
    }

    public struct GrassSeedMessage : NetworkMessage
    {
        public int seed;
    }
}
