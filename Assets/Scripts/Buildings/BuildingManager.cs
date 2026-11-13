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

        [Header("Debug / Cheats")]
        [SerializeField] private bool enableCheats = false;
        public bool EnableCheats => enableCheats;

        private readonly Dictionary<Vector2Int, Building> occupiedCells = new Dictionary<Vector2Int, Building>();

        private bool cheatPlacementActive;
        private BuildingType? pendingCheatType;
        private BuildingDefinition pendingCheatDef;
        private GameObject cheatPreviewInstance;
        private bool cheatPreviewPlacementValid;
        private bool cheatPreviewGhostReady;
        private float cheatPreviewHeight;
        private bool cheatPlacementClickArmed;

        public bool IsCheatPlacing => cheatPlacementActive;
        public string PendingCheatTypeName => pendingCheatType?.ToString() ?? "";

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
            CancelCheatPlacement();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (cheatPlacementActive)
            {
                UpdateCheatPlacement();
            }
        }

        public void StartCheatPlacement(BuildingType type)
        {
            if (RTSokak.UI.DebugCheatUI.Instance != null)
                RTSokak.UI.DebugCheatUI.Instance.CancelUnitSpawnMode();

            if (RTSokak.UI.SelectionPanelUI.Instance != null)
                RTSokak.UI.SelectionPanelUI.Instance.ClearPreview();

            CancelCheatPlacement();

            BuildingDefinition def = GetDefinition(type);
            if (def == null || def.prefab == null)
            {
                UnityEngine.Debug.LogWarning($"[BuildingManager] StartCheatPlacement: No definition/prefab for {type}");
                return;
            }

            pendingCheatType = type;
            pendingCheatDef = def;
            cheatPreviewHeight = def.previewHeight;
            cheatPreviewPlacementValid = true;
            cheatPreviewGhostReady = false;

            cheatPreviewInstance = Instantiate(def.prefab);
            DisablePreviewNetworking(cheatPreviewInstance);
            SetPreviewGhost(cheatPreviewInstance, true);
            StripPreviewPhysics(cheatPreviewInstance);
            cheatPreviewGhostReady = true;

            if (RTSokak.UI.UnitSelectionManager.Instance != null)
                RTSokak.UI.UnitSelectionManager.Instance.SetPlacementMode(true);

            cheatPlacementActive = true;
        }

        public void CancelCheatPlacement()
        {
            cheatPlacementActive = false;
            pendingCheatType = null;
            pendingCheatDef = null;
            cheatPreviewGhostReady = false;
            cheatPlacementClickArmed = false;

            if (cheatPreviewInstance != null)
            {
                Destroy(cheatPreviewInstance);
                cheatPreviewInstance = null;
            }

            if (RTSokak.UI.UnitSelectionManager.Instance != null)
                RTSokak.UI.UnitSelectionManager.Instance.SetPlacementMode(false);
        }

        private void UpdateCheatPlacement()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (RTSokak.UI.UnitSelectionManager.Instance != null)
                    RTSokak.UI.UnitSelectionManager.Instance.SuppressNextRightClickCommand();
                CancelCheatPlacement();
                return;
            }

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null || cheatPreviewInstance == null || pendingCheatDef == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore);
            if (hasHit)
            {
                Vector2Int origin = WorldCenterToOrigin(hit.point, pendingCheatDef.Size);
                bool valid = CanPlaceAt(origin, pendingCheatDef.Size, out _);
                if (pendingCheatType == BuildingType.Capital && RTSokak.Network.RTSPlayer.localPlayer != null && RTSokak.Network.RTSPlayer.localPlayer.HasCapitalBuilding())
                {
                    valid = false;
                }

                Vector3 follow = OriginToWorldCenter(origin, pendingCheatDef.Size, cheatPreviewHeight);
                cheatPreviewInstance.transform.position = follow;

                if (!cheatPreviewGhostReady || valid != cheatPreviewPlacementValid)
                {
                    cheatPreviewPlacementValid = valid;
                    SetPreviewGhost(cheatPreviewInstance, cheatPreviewPlacementValid);
                    StripPreviewPhysics(cheatPreviewInstance);
                    cheatPreviewGhostReady = true;
                }
            }
            else
            {
                Vector3 follow = hit.point;
                follow.y = cheatPreviewHeight;
                cheatPreviewInstance.transform.position = follow;
            }

            if (Input.GetMouseButtonDown(0))
            {
                cheatPlacementClickArmed =
                    UnityEngine.EventSystems.EventSystem.current == null ||
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            }

            if (!Input.GetMouseButtonUp(0) || !cheatPlacementClickArmed)
                return;

            cheatPlacementClickArmed = false;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (!hasHit)
            {
                if (!Physics.Raycast(ray, out hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
                    return;
            }

            Vector2Int finalOrigin = WorldCenterToOrigin(hit.point, pendingCheatDef.Size);
            if (!CanPlaceAt(finalOrigin, pendingCheatDef.Size, out string reason))
            {
                UnityEngine.Debug.LogWarning($"[BuildingManager] Cheat place failed: {reason}");
                return;
            }

            if (pendingCheatType == BuildingType.Capital && RTSokak.Network.RTSPlayer.localPlayer != null && RTSokak.Network.RTSPlayer.localPlayer.HasCapitalBuilding())
            {
                UnityEngine.Debug.LogWarning("[BuildingManager] Cheat place failed: Capital already exists");
                return;
            }

            if (RTSokak.Network.RTSPlayer.localPlayer == null || !pendingCheatType.HasValue)
            {
                CancelCheatPlacement();
                return;
            }

            Vector3 pos = hit.point;
            pos.y = cheatPreviewHeight;

            BuildingType bType = pendingCheatType.Value;
            RTSokak.Network.RTSPlayer.localPlayer.CmdCheatPlaceBuildingInstant(bType, pos);

            CancelCheatPlacement();
        }

        public static void SetPreviewGhost(GameObject go, bool validPlacement)
        {
            if (go == null) return;
            Color tint = validPlacement
                ? new Color(0.55f, 1f, 0.55f, 0.4f)
                : new Color(1f, 0.35f, 0.35f, 0.4f);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name.IndexOf("BuildingPreviewGhost", System.StringComparison.Ordinal) < 0)
                        mats[i] = CreateGhostMaterial(mats[i], tint);
                    else
                        ApplyGhostTint(mats[i], tint);
                }
                r.materials = mats;
            }
        }

        private static void ApplyGhostTint(Material mat, Color tint)
        {
            if (mat == null) return;
            if (mat.HasProperty("_UnlitColor"))
                mat.SetColor("_UnlitColor", tint);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
        }

        private static Material CreateGhostMaterial(Material source, Color tint)
        {
            Shader shader = Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            if (shader == null && source != null)
                shader = source.shader;

            Material mat = shader != null ? new Material(shader) : new Material(source);
            mat.name = "BuildingPreviewGhost";

            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);

            return mat;
        }

        public static void StripPreviewPhysics(GameObject go)
        {
            if (go == null) return;

            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col == null) continue;
                col.enabled = false;
                Destroy(col);
            }
            foreach (var col2d in go.GetComponentsInChildren<Collider2D>(true))
            {
                if (col2d == null) continue;
                col2d.enabled = false;
                Destroy(col2d);
            }
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null) continue;
                rb.detectCollisions = false;
                rb.isKinematic = true;
            }
            foreach (var rb2d in go.GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (rb2d == null) continue;
                rb2d.simulated = false;
            }
            foreach (var obstacle in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                if (obstacle == null) continue;
                obstacle.enabled = false;
                Destroy(obstacle);
            }
        }

        public static void DisablePreviewNetworking(GameObject go)
        {
            if (go == null) return;

            foreach (var nb in go.GetComponentsInChildren<Mirror.NetworkBehaviour>(true))
                nb.enabled = false;
            foreach (var ni in go.GetComponentsInChildren<Mirror.NetworkIdentity>(true))
                ni.enabled = false;

            StripPreviewPhysics(go);
            SetLayerRecursive(go, LayerMask.NameToLayer("Ignore Raycast"));
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                    SetLayerRecursive(child.gameObject, layer);
            }
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

            ResourceManager resources = ResourceManager.Instance;

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
