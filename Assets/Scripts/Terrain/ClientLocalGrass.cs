using System.Collections.Generic;
using UnityEngine;

namespace RTSokak.Terrain
{
    /// <summary>
    /// Локальная трава на чистом клиенте, если в сцене нет ProceduralGrassGenerator.
    /// </summary>
    public class ClientLocalGrass : MonoBehaviour
    {
        const string ResourcesGrassPath = "Terrain/Grass";

        [SerializeField] int grassCount = 5000;
        [SerializeField] float worldSize = 500f;
        [SerializeField] float minScale = 0.5f;
        [SerializeField] float maxScale = 1.5f;
        [SerializeField] float clumpiness = 0.5f;
        [SerializeField] float noiseScale = 50f;

        readonly List<GameObject> spawned = new List<GameObject>();
        GameObject grassPrefab;
        int appliedSeed;

        public static void EnsureAndGenerate(int seed)
        {
            if (seed == 0) return;

            var existing = Object.FindFirstObjectByType<ClientLocalGrass>();
            if (existing == null)
            {
                var go = new GameObject("ClientLocalGrass");
                existing = go.AddComponent<ClientLocalGrass>();
            }

            existing.Generate(seed);
        }

        public void Generate(int seed)
        {
            if (seed == 0 || (appliedSeed == seed && spawned.Count > 0))
                return;

            if (grassPrefab == null)
                grassPrefab = global::UnityEngine.Resources.Load<GameObject>(ResourcesGrassPath);

            if (grassPrefab == null)
            {
                global::UnityEngine.Debug.LogWarning($"[ClientLocalGrass] Missing Resources/{ResourcesGrassPath}");
                return;
            }

            foreach (var g in spawned)
            {
                if (g != null) Destroy(g);
            }
            spawned.Clear();

            var random = new System.Random(seed);
            float offsetX = (float)(random.NextDouble() * 10000);
            float offsetY = (float)(random.NextDouble() * 10000);

            int count = 0;
            int attempts = 0;
            int maxAttempts = grassCount * 3;

            while (count < grassCount && attempts < maxAttempts)
            {
                attempts++;
                float x = Mathf.Lerp(-worldSize / 2f, worldSize / 2f, (float)random.NextDouble());
                float z = Mathf.Lerp(-worldSize / 2f, worldSize / 2f, (float)random.NextDouble());
                float noise = Mathf.PerlinNoise(offsetX + x / noiseScale, offsetY + z / noiseScale);
                if (noise < clumpiness)
                    continue;

                Quaternion rot = Quaternion.Euler(0f, (float)(random.NextDouble() * 360.0), 0f);
                var grass = Instantiate(grassPrefab, new Vector3(x, 0f, z), rot, transform);
                float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                grass.transform.localScale = Vector3.one * scale;
                spawned.Add(grass);
                count++;
            }

            appliedSeed = seed;
            global::UnityEngine.Debug.Log($"[ClientLocalGrass] Spawned {count} grass with seed {seed}");
        }

        void OnDestroy()
        {
            foreach (var g in spawned)
            {
                if (g != null) Destroy(g);
            }
            spawned.Clear();
        }
    }
}
