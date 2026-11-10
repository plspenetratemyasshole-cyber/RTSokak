using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using RTSokak.Network;
using RTSokak.Buildings;

namespace RTSokak.UI
{
    /// <summary>
    /// Выбор юнитов (Worker / Warrior) или одного здания.
    /// </summary>
    public class UnitSelectionManager : MonoBehaviour
    {
        public static UnitSelectionManager Instance { get; private set; }
        
        private readonly List<RTSUnit> selectedUnits = new List<RTSUnit>();
        private Building selectedBuilding;

        private UnityEngine.Camera mainCamera;
        private Vector3 rightMouseDownPosition;
        private bool wasCameraMoved;
        private const float cameraMoveThreshold = 5f;
        private bool placementMode;
        /// <summary>Пропустить следующий ПКМ-up как команду движения (отмена preview здания).</summary>
        private bool suppressNextRightClickCommand;

        private bool boxSelectActive;
        private bool leftClickPending;
        private Vector2 boxStart;
        private Vector2 boxEnd;
        private const float boxDragThreshold = 8f;

        [Header("Formation")]
        [Tooltip("Центр-центр соседних слотов. Для agent radius 0.5 оптимально ≈1.05–1.15 (вплотную без наложений)")]
        public float formationSpacing = 1.1f;

        private static Texture2D whiteTexture;

        public RTSUnit SelectedUnit => selectedUnits.Count > 0 ? selectedUnits[0] : null;
        public Worker SelectedWorker => SelectedUnit as Worker;
        public Building SelectedBuilding => selectedBuilding;
        public IReadOnlyList<RTSUnit> SelectedUnits => selectedUnits;
        public event System.Action OnSelectionChanged;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);
        }

        private void Start()
        {
            mainCamera = UnityEngine.Camera.main;
        }

        public void SetPlacementMode(bool enabled)
        {
            placementMode = enabled;
            if (enabled)
                CancelBoxSelect();
        }

        public void SuppressNextRightClickCommand()
        {
            suppressNextRightClickCommand = true;
        }

        public bool IsPlacementMode => placementMode;

        private void Update()
        {
            if (mainCamera == null)
                mainCamera = UnityEngine.Camera.main;

            if (placementMode)
            {
                if (Input.GetMouseButtonUp(0))
                    leftClickPending = false;
            }
            else
            {
                HandleSelection();
            }

            HandleMovementCommand();
        }

        private void OnGUI()
        {
            if (!boxSelectActive)
                return;

            Rect screenRect = GetNormalizedScreenRect(boxStart, boxEnd);
            if (screenRect.width < 2f && screenRect.height < 2f)
                return;

            Color fill = new Color(0.15f, 0.85f, 0.25f, 0.18f);
            Color border = new Color(0.2f, 1f, 0.3f, 0.9f);
            DrawScreenRect(screenRect, fill);
            DrawScreenRectBorder(screenRect, border, 2f);
        }

        private void HandleSelection()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    leftClickPending = false;
                    return;
                }

                leftClickPending = true;
                boxStart = Input.mousePosition;
                boxEnd = boxStart;
                boxSelectActive = shift;
            }

            if (Input.GetMouseButton(0) && boxSelectActive && shift)
                boxEnd = Input.mousePosition;

            if (!Input.GetMouseButtonUp(0))
                return;

            if (!leftClickPending)
            {
                CancelBoxSelect();
                return;
            }

            leftClickPending = false;
            bool wasBoxing = boxSelectActive;
            Vector2 start = boxStart;
            Vector2 end = Input.mousePosition;
            CancelBoxSelect();

            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float dragDist = Vector2.Distance(start, end);

            if (wasBoxing && shift && dragDist >= boxDragThreshold)
            {
                BoxSelectUnits(start, end, additive: true);
                return;
            }

            HandleClickSelect(shift);
        }

        private void HandleClickSelect(bool shift)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                if (!shift)
                    DeselectAll();
                return;
            }

            RTSUnit unit = hit.collider.GetComponentInParent<RTSUnit>();
            if (unit != null && unit.IsOwned)
            {
                if (shift)
                    AddOrToggleUnit(unit);
                else
                    SelectUnitOnly(unit);
                return;
            }

            if (shift)
                return;

            Building building = hit.collider.GetComponentInParent<Building>();
            if (building != null && building.IsOwnedByLocalPlayer())
            {
                SelectBuilding(building);
                return;
            }

            DeselectAll();
        }

        private void BoxSelectUnits(Vector2 screenStart, Vector2 screenEnd, bool additive)
        {
            Rect rect = GetInputSpaceRect(screenStart, screenEnd);
            ClearBuildingSelectionOnly();

            if (!additive)
                ClearUnitsSelectionOnly();

            RTSUnit[] all = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            bool any = false;
            foreach (RTSUnit unit in all)
            {
                if (unit == null || !unit.IsOwned || !unit.IsAlive)
                    continue;

                Vector3 sp = mainCamera.WorldToScreenPoint(unit.transform.position);
                if (sp.z < 0f)
                    continue;

                if (!rect.Contains(new Vector2(sp.x, sp.y)))
                    continue;

                if (!selectedUnits.Contains(unit))
                {
                    unit.Select();
                    selectedUnits.Add(unit);
                }

                any = true;
            }

            if (any || !additive)
                OnSelectionChanged?.Invoke();
        }

        private void HandleMovementCommand()
        {
            if (Input.GetMouseButtonDown(1))
            {
                rightMouseDownPosition = Input.mousePosition;
                wasCameraMoved = false;
                // ПКМ во время placement = отмена постройки, не приказ юниту
                if (placementMode)
                    suppressNextRightClickCommand = true;
            }

            if (Input.GetMouseButton(1))
            {
                if (Vector3.Distance(Input.mousePosition, rightMouseDownPosition) > cameraMoveThreshold)
                    wasCameraMoved = true;
            }

            if (!Input.GetMouseButtonUp(1))
                return;

            if (suppressNextRightClickCommand)
            {
                suppressNextRightClickCommand = false;
                return;
            }

            if (wasCameraMoved || selectedUnits.Count == 0 || placementMode || boxSelectActive)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            // Атака чужого юнита (только AI-подчиненные)
            RTSUnit enemy = hit.collider.GetComponentInParent<RTSUnit>();
            if (enemy != null && !enemy.IsOwned && enemy.IsAlive)
            {
                foreach (RTSUnit unit in selectedUnits)
                {
                    if (unit != null && unit.IsAlive && !unit.IsPlayerControlled)
                        unit.AttackUnit(enemy);
                }
                return;
            }

            // Атака вражеского здания
            Building enemyBuilding = hit.collider.GetComponentInParent<Building>();
            if (enemyBuilding != null && !enemyBuilding.IsOwnedByLocalPlayer() && enemyBuilding.IsComplete)
            {
                foreach (RTSUnit unit in selectedUnits)
                {
                    if (unit != null && unit.IsAlive && !unit.IsPlayerControlled)
                        unit.AttackBuilding(enemyBuilding);
                }
                return;
            }

            var resource = hit.collider.GetComponentInParent<RTSokak.Resources.Resource>();
            if (resource != null)
            {
                foreach (RTSUnit unit in selectedUnits)
                {
                    if (unit is Worker worker && !unit.IsPlayerControlled)
                        worker.HarvestResource(resource);
                }
                return;
            }

            Building building = hit.collider.GetComponentInParent<Building>();
            if (building != null && building.IsOwnedByLocalPlayer())
            {
                if (building.IsUnderConstruction)
                {
                    foreach (RTSUnit unit in selectedUnits)
                    {
                        if (unit is Worker worker && !unit.IsPlayerControlled)
                            worker.RequestJoinBuilding(building);
                    }
                    return;
                }

                var garden = building.GetComponent<GardenBed>();
                if (garden != null)
                {
                    foreach (RTSUnit unit in selectedUnits)
                    {
                        if (unit is Worker worker && !unit.IsPlayerControlled)
                        {
                            worker.RequestFarmGarden(garden);
                            break;
                        }
                    }
                    return;
                }

                if (building.Type == BuildingType.Capital || building.Type == BuildingType.Warehouse)
                {
                    foreach (RTSUnit unit in selectedUnits)
                    {
                        if (unit is Worker worker && !unit.IsPlayerControlled)
                            worker.RequestDepositToBuilding(building);
                    }
                    return;
                }
            }

            IssueMoveFormation(hit.point);
        }

        private void IssueMoveFormation(Vector3 center)
        {
            var units = new List<RTSUnit>(selectedUnits.Count);
            var playerUnits = new List<RTSUnit>();
            foreach (RTSUnit u in selectedUnits)
            {
                if (u == null) continue;
                if (u.IsPlayerControlled)
                    playerUnits.Add(u);
                else
                    units.Add(u);
            }

            // Жёлтая точка только для юнитов-игроков (подсказка, без path)
            if (playerUnits.Count > 0 && RTSPlayer.localPlayer != null)
            {
                Vector3 hint = center;
                hint.y = 0f;
                for (int i = 0; i < playerUnits.Count; i++)
                    RTSPlayer.localPlayer.CmdMayorHintUnitPlayer(playerUnits[i].netId, hint);
            }

            int count = units.Count;
            if (count == 0)
                return;

            if (count == 1)
            {
                Vector3 dest = center;
                dest.y = 0f;
                units[0].MoveTo(dest, tightArrival: true);
                return;
            }

            // Плотная квадратная сетка + назначение ближайшего юнита к слоту (меньше пересечений)
            List<Vector3> slots = BuildCompactGridSlots(center, count, Mathf.Max(0.6f, formationSpacing));
            bool[] used = new bool[count];

            for (int s = 0; s < slots.Count; s++)
            {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int u = 0; u < count; u++)
                {
                    if (used[u])
                        continue;
                    float d = HorizontalDistSq(units[u].transform.position, slots[s]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = u;
                    }
                }

                if (best < 0)
                    continue;

                used[best] = true;
                Vector3 dest = slots[s];
                dest.y = 0f;
                units[best].MoveTo(dest, tightArrival: true);
            }
        }

        /// <summary>Квадратная сетка вокруг центра, слоты почти вплотную.</summary>
        private static List<Vector3> BuildCompactGridSlots(Vector3 center, int count, float spacing)
        {
            var slots = new List<Vector3>(count);
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)cols);

            float ox = (cols - 1) * 0.5f;
            float oz = (rows - 1) * 0.5f;

            int i = 0;
            for (int row = 0; row < rows && i < count; row++)
            {
                for (int col = 0; col < cols && i < count; col++, i++)
                {
                    slots.Add(new Vector3(
                        center.x + (col - ox) * spacing,
                        0f,
                        center.z + (row - oz) * spacing));
                }
            }

            return slots;
        }

        private static float HorizontalDistSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void SelectUnitOnly(RTSUnit unit)
        {
            ClearBuildingSelectionOnly();
            ClearUnitsSelectionOnly();

            unit.Select();
            selectedUnits.Add(unit);
            OnSelectionChanged?.Invoke();
        }

        private void AddOrToggleUnit(RTSUnit unit)
        {
            ClearBuildingSelectionOnly();

            if (selectedUnits.Contains(unit))
            {
                unit.Deselect();
                selectedUnits.Remove(unit);
            }
            else
            {
                unit.Select();
                selectedUnits.Add(unit);
            }

            OnSelectionChanged?.Invoke();
        }

        private void SelectBuilding(Building building)
        {
            ClearUnitsSelectionOnly();

            if (selectedBuilding != null && selectedBuilding != building)
                selectedBuilding.Deselect();

            selectedBuilding = building;
            building.Select();
            OnSelectionChanged?.Invoke();
        }

        private void ClearUnitsSelectionOnly()
        {
            foreach (RTSUnit unit in selectedUnits)
            {
                if (unit != null)
                    unit.Deselect();
            }

            selectedUnits.Clear();
        }

        private void ClearBuildingSelectionOnly()
        {
            if (selectedBuilding == null) return;
            selectedBuilding.Deselect();
            selectedBuilding = null;
        }

        private void DeselectAll()
        {
            ClearUnitsSelectionOnly();
            ClearBuildingSelectionOnly();
            OnSelectionChanged?.Invoke();
        }

        private void CancelBoxSelect()
        {
            boxSelectActive = false;
        }

        public void RequestDemolishBuilding(Building building)
        {
            if (building == null) return;
            building.RequestDemolish();
            if (selectedBuilding == building)
                DeselectAll();
        }

        public List<Worker> GetSelectedWorkers()
        {
            var list = new List<Worker>();
            foreach (RTSUnit u in selectedUnits)
            {
                if (u is Worker w)
                    list.Add(w);
            }
            return list;
        }

        private static Rect GetInputSpaceRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float xMax = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float yMax = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect GetNormalizedScreenRect(Vector2 inputStart, Vector2 inputEnd)
        {
            float xMin = Mathf.Min(inputStart.x, inputEnd.x);
            float xMax = Mathf.Max(inputStart.x, inputEnd.x);
            float yMin = Mathf.Min(inputStart.y, inputEnd.y);
            float yMax = Mathf.Max(inputStart.y, inputEnd.y);

            float guiYMin = Screen.height - yMax;
            float guiYMax = Screen.height - yMin;
            return Rect.MinMaxRect(xMin, guiYMin, xMax, guiYMax);
        }

        private static void DrawScreenRect(Rect rect, Color color)
        {
            EnsureWhiteTexture();
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = prev;
        }

        private static void DrawScreenRectBorder(Rect rect, Color color, float thickness)
        {
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private static void EnsureWhiteTexture()
        {
            if (whiteTexture != null) return;
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }
    }
}
