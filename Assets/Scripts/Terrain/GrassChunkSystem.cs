using UnityEngine;

namespace RTSokak.Terrain
{
    /// <summary>
    /// VFX Graph grass is NOT supported on Built-in RP.
    /// Component stays so scenes don't lose references; Start disables itself.
    /// Use ProceduralGrassGenerator with GrassMode.Prefab, or Terrain details.
    /// </summary>
    public class GrassChunkSystem : MonoBehaviour
    {
        [Header("Deprecated on Built-in RP")]
        [SerializeField] private GameObject vfxGrassPrefab;
        [SerializeField, Min(1)] private int prefabCount = 1;
        [SerializeField] private Vector3 spawnPosition = Vector3.zero;
        [SerializeField] private bool uniqueSeedPerInstance = true;
        [SerializeField] private bool playOnSpawn = true;
        [SerializeField] private Vector3 grassSpawnArea = new Vector3(500f, 0f, 500f);
        [SerializeField] private Vector3 boundsSize = new Vector3(520f, 20f, 520f);
        [SerializeField] private bool applyCoverageOnSpawn = true;
        [SerializeField] private bool showDebugGizmos = true;

        private void Start()
        {
            UnityEngine.Debug.LogWarning(
                "[GrassChunkSystem] StylizedGrass / VFX Graph requires URP or HDRP. " +
                "Disabled on Built-in Render Pipeline. Use ProceduralGrassGenerator (Prefab mode) instead.");
            enabled = false;
            gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.25f);
            Gizmos.DrawWireCube(spawnPosition, new Vector3(grassSpawnArea.x, 1f, grassSpawnArea.z));
            Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.35f);
            Gizmos.DrawWireCube(spawnPosition, boundsSize);
        }
    }
}

