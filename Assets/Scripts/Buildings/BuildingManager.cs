using System.Collections.Generic;
using UnityEngine;
using RTSokak.Resources;

namespace RTSokak.Buildings
{
    /// <summary>
    /// Конфиг зданий сцены + занятость клеток сетки (та же, что у деревьев/руды).
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Header("Building Definitions")]
        [Tooltip("Capital / House / Garden bed — префаб, размер сетки, стоимость, время")]
        [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();

        private readonly Dictionary<Vector2Int, Building> occupiedCells = new Dictionary<Vector2Int, Building>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[BuildingManager] Duplicate instance — disabling this one.");
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public BuildingDefinition GetDefinition(BuildingType type)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].type == type)
                    return buildings[i];
            }

            return null;
        }

        public IReadOnlyList<BuildingDefinition> GetAllDefinitions()
        {
            return buildings;
        }

        public static Vector2Int WorldToGrid(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
        }

        /// <summary>
        /// Мировая точка клика → origin (минимальный угол footprint) на сетке.
        /// </summary>
        public Vector2Int WorldCenterToOrigin(Vector3 worldCenter, Vector2Int size)
        {
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            float ox = worldCenter.x - (size.x - 1) * 0.5f;
            float oz = worldCenter.z - (size.y - 1) * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(ox), Mathf.RoundToInt(oz));
        }

        public Vector3 OriginToWorldCenter(Vector2Int origin, Vector2Int size, float y)
        {
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            float cx = origin.x + (size.x - 1) * 0.5f;
            float cz = origin.y + (size.y - 1) * 0.5f;
            return new Vector3(cx, y, cz);
        }

        public bool CanPlaceAt(Vector2Int origin, Vector2Int size, out string reason)
        {
            reason = null;
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);

            ResourceManager resources = Object.FindFirstObjectByType<ResourceManager>();

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int cell = new Vector2Int(origin.x + x, origin.y + z);
                    if (occupiedCells.ContainsKey(cell))
                    {
                        reason = "Cell occupied by building";
                        return false;
                    }

                    if (resources != null && resources.GetResourceAt(cell) != null)
                    {
                        reason = "Cell occupied by resource";
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TrySnapPlacement(Vector3 worldHint, BuildingDefinition def, out Vector2Int origin, out Vector3 snappedCenter, out string reason)
        {
            origin = default;
            snappedCenter = worldHint;
            reason = null;

            if (def == null)
            {
                reason = "No definition";
                return false;
            }

            Vector2Int size = def.Size;
            origin = WorldCenterToOrigin(worldHint, size);
            if (!CanPlaceAt(origin, size, out reason))
                return false;

            snappedCenter = OriginToWorldCenter(origin, size, def.previewHeight);
            return true;
        }

        public void RegisterBuilding(Building building, Vector2Int origin, Vector2Int size)
        {
            if (building == null) return;

            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                {
                    Vector2Int cell = new Vector2Int(origin.x + x, origin.y + z);
                    occupiedCells[cell] = building;
                }
            }
        }

        public void UnregisterBuilding(Building building)
        {
            if (building == null) return;

            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var kv in occupiedCells)
            {
                if (kv.Value == building)
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                occupiedCells.Remove(toRemove[i]);
        }

#if UNITY_EDITOR
        public void EditorSetBuildings(List<BuildingDefinition> defs)
        {
            buildings = defs;
        }

        public void EditorUpsertDefinition(BuildingDefinition def)
        {
            if (def == null) return;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].type == def.type)
                {
                    buildings[i] = def;
                    return;
                }
            }

            buildings.Add(def);
        }
#endif
    }
}
