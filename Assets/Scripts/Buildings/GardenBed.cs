using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace RTSokak.Buildings
{
    public enum GardenPlotState : byte
    {
        Empty = 0,
        Growing = 1,
        Ready = 2
    }

    /// <summary>
    /// Грядка: точки посадки (empty children), рост пшеницы, один фермер.
    /// </summary>
    [RequireComponent(typeof(Building))]
    public class GardenBed : NetworkBehaviour
    {
        [Header("Plant Points")]
        [Tooltip("Точки посадки. Если пусто — ищутся дочерние объекты с именем empty / plant")]
        [SerializeField] private Transform[] plantPoints;

        [Header("Wheat")]
        [Tooltip("Модель пшеницы стадия 1 (сразу после посадки)")]
        [SerializeField] private GameObject wheatStage1Prefab;
        [Tooltip("Модель пшеницы стадия 2 (промежуточный рост)")]
        [SerializeField] private GameObject wheatStage2Prefab;
        [Tooltip("Модель пшеницы стадия 3 (готова к сбору)")]
        [SerializeField] private GameObject wheatStage3Prefab;
        [SerializeField] private Color youngColor = new Color(0.25f, 0.75f, 0.2f, 1f);
        [SerializeField] private Color ripeColor = new Color(0.86f, 0.75f, 0.38f, 1f);

        [Header("Growth")]
        [SerializeField] private float minGrowTime = 30f;
        [SerializeField] private float maxGrowTime = 60f;
        [SerializeField] private int foodPerHarvest = 50;

        private readonly SyncList<byte> plotStates = new SyncList<byte>();
        private readonly SyncList<double> plotGrowStarts = new SyncList<double>();
        private readonly SyncList<float> plotGrowDurations = new SyncList<float>();

        private readonly List<GameObject> wheatVisuals = new List<GameObject>();
        private MaterialPropertyBlock mpb;

        [SyncVar]
        private uint farmerNetId;

        private Building building;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();

            // Юнит должен свободно ходить по грядке
            var box = GetComponent<BoxCollider>();
            if (box != null)
                box.isTrigger = true;

            DisableNavMeshCarve();
        }

        public int PlotCount => plantPoints != null ? plantPoints.Length : 0;
        public int FoodPerHarvest => foodPerHarvest;
        public bool HasFarmer => farmerNetId != 0;
        public uint FarmerNetId => farmerNetId;

        public override void OnStartServer()
        {
            base.OnStartServer();
            building = GetComponent<Building>();
            ResolvePlantPoints();
            DisableNavMeshCarve();

            if (building != null)
                building.OnConstructionFinished += OnConstructionFinished;

            if (building != null && building.IsComplete)
                EnsurePlotsInitialized();
        }

        public override void OnStopServer()
        {
            if (building != null)
                building.OnConstructionFinished -= OnConstructionFinished;
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            building = GetComponent<Building>();
            ResolvePlantPoints();
            DisableNavMeshCarve();
            plotStates.Callback += OnPlotStatesChanged;
            RebuildAllWheatVisuals();
        }

        public override void OnStopClient()
        {
            plotStates.Callback -= OnPlotStatesChanged;
            ClearWheatVisuals();
            base.OnStopClient();
        }

        private void Update()
        {
            if (isServer)
                ServerTickGrowth();

            UpdateWheatVisuals();
        }

        private void OnConstructionFinished(Building b)
        {
            EnsurePlotsInitialized();
        }

        [Server]
        private void EnsurePlotsInitialized()
        {
            ResolvePlantPoints();
            if (plantPoints == null || plantPoints.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"[GardenBed] {name}: no plant points — add empty children named empty/plant");
                return;
            }

            if (plotStates.Count == plantPoints.Length)
                return;

            plotStates.Clear();
            plotGrowStarts.Clear();
            plotGrowDurations.Clear();
            for (int i = 0; i < plantPoints.Length; i++)
            {
                plotStates.Add((byte)GardenPlotState.Empty);
                plotGrowStarts.Add(0);
                plotGrowDurations.Add(0f);
            }
        }

        [Server]
        private void ServerTickGrowth()
        {
            if (building != null && !building.IsComplete)
                return;
            if (plotStates.Count == 0)
                return;

            double now = NetworkTime.time;
            for (int i = 0; i < plotStates.Count; i++)
            {
                if (plotStates[i] != (byte)GardenPlotState.Growing)
                    continue;
                if (now < plotGrowStarts[i] + plotGrowDurations[i])
                    continue;

                plotStates[i] = (byte)GardenPlotState.Ready;
            }
        }

        [Server]
        public bool TryAssignFarmer(NetworkIdentity worker)
        {
            if (worker == null)
                return false;
            if (building != null && !building.IsComplete)
                return false;

            EnsurePlotsInitialized();

            if (farmerNetId != 0 && farmerNetId != worker.netId)
                return false;

            farmerNetId = worker.netId;
            return true;
        }

        [Server]
        public void ReleaseFarmer(NetworkIdentity worker)
        {
            if (worker == null)
                return;
            if (farmerNetId == worker.netId)
                farmerNetId = 0;
        }

        public bool IsAssignedTo(NetworkIdentity worker)
        {
            return worker != null && farmerNetId == worker.netId;
        }

        public Vector3 GetPlotWorldPosition(int index)
        {
            if (plantPoints == null || index < 0 || index >= plantPoints.Length || plantPoints[index] == null)
                return transform.position;
            return plantPoints[index].position;
        }

        public bool TryGetNextWork(out int plotIndex, out bool harvest)
        {
            return TryGetNextWorkNear(Vector3.zero, out plotIndex, out harvest);
        }

        public bool TryGetNextWorkNear(Vector3 fromWorld, out int plotIndex, out bool harvest)
        {
            plotIndex = -1;
            harvest = false;
            if (plotStates.Count == 0)
                return false;

            // Сначала готовые к сбору — ближайшая
            if (FindBestPlot((byte)GardenPlotState.Ready, fromWorld, out plotIndex))
            {
                harvest = true;
                return true;
            }

            // Потом пустые под посадку — ближайшая
            if (FindBestPlot((byte)GardenPlotState.Empty, fromWorld, out plotIndex))
            {
                harvest = false;
                return true;
            }

            return false;
        }

        private bool FindBestPlot(byte wanted, Vector3 fromWorld, out int plotIndex)
        {
            plotIndex = -1;
            float best = float.MaxValue;
            bool useDistance = fromWorld.sqrMagnitude > 0.0001f;

            for (int i = 0; i < plotStates.Count; i++)
            {
                if (plotStates[i] != wanted)
                    continue;

                if (!useDistance)
                {
                    plotIndex = i;
                    return true;
                }

                float d = (GetPlotWorldPosition(i) - fromWorld).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    plotIndex = i;
                }
            }

            return plotIndex >= 0;
        }

        public GardenPlotState GetPlotState(int index)
        {
            if (index < 0 || index >= plotStates.Count)
                return GardenPlotState.Empty;
            return (GardenPlotState)plotStates[index];
        }

        [Server]
        public bool ServerPlant(int index)
        {
            EnsurePlotsInitialized();
            if (index < 0 || index >= plotStates.Count)
                return false;
            if (plotStates[index] != (byte)GardenPlotState.Empty)
                return false;

            float duration = Random.Range(Mathf.Min(minGrowTime, maxGrowTime), Mathf.Max(minGrowTime, maxGrowTime));
            plotGrowStarts[index] = NetworkTime.time;
            plotGrowDurations[index] = duration;
            plotStates[index] = (byte)GardenPlotState.Growing;
            return true;
        }

        /// <summary>
        /// Сбор + сразу новая посадка. Возвращает еду за сбор.
        /// </summary>
        [Server]
        public int ServerHarvestAndReplant(int index)
        {
            EnsurePlotsInitialized();
            if (index < 0 || index >= plotStates.Count)
                return 0;
            if (plotStates[index] != (byte)GardenPlotState.Ready)
                return 0;

            int food = foodPerHarvest;
            float duration = Random.Range(Mathf.Min(minGrowTime, maxGrowTime), Mathf.Max(minGrowTime, maxGrowTime));
            plotGrowStarts[index] = NetworkTime.time;
            plotGrowDurations[index] = duration;
            plotStates[index] = (byte)GardenPlotState.Growing;
            return food;
        }

        private void OnPlotStatesChanged(SyncList<byte>.Operation op, int index, byte oldItem, byte newItem)
        {
            RebuildAllWheatVisuals();
        }

        private void RebuildAllWheatVisuals()
        {
            ResolvePlantPoints();
            EnsureWheatListSize();

            for (int i = 0; i < wheatVisuals.Count; i++)
            {
                GardenPlotState state = i < plotStates.Count
                    ? (GardenPlotState)plotStates[i]
                    : GardenPlotState.Empty;

                if (state == GardenPlotState.Empty)
                {
                    if (wheatVisuals[i] != null)
                    {
                        Destroy(wheatVisuals[i]);
                        wheatVisuals[i] = null;
                    }
                    continue;
                }

                if (wheatVisuals[i] == null)
                    wheatVisuals[i] = SpawnWheatVisual(i);
            }

            UpdateWheatVisuals();
        }

        private void EnsureWheatListSize()
        {
            int n = plantPoints != null ? plantPoints.Length : 0;
            while (wheatVisuals.Count < n)
                wheatVisuals.Add(null);
            while (wheatVisuals.Count > n)
            {
                int last = wheatVisuals.Count - 1;
                if (wheatVisuals[last] != null)
                    Destroy(wheatVisuals[last]);
                wheatVisuals.RemoveAt(last);
            }
        }

        private GameObject SpawnWheatVisual(int index)
        {
            // Создаём пшеницу стадии 1 (только что посажена)
            GameObject prefab = wheatStage1Prefab;
            if (prefab == null)
                prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Wheat");

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Wheat";
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
            }

            go.name = $"Wheat_{index}";
            go.transform.SetParent(null, true);
            go.transform.localScale = prefab != null ? prefab.transform.localScale : new Vector3(0.25f, 0.45f, 0.25f);
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        private void UpdateWheatVisuals()
        {
            if (plantPoints == null)
                return;

            double now = NetworkTime.time;
            for (int i = 0; i < wheatVisuals.Count && i < plotStates.Count; i++)
            {
                GameObject wheat = wheatVisuals[i];
                if (wheat == null)
                    continue;

                Transform point = plantPoints[i];
                if (point == null)
                    continue;

                float t = 1f;
                byte state = plotStates[i];
                if (state == (byte)GardenPlotState.Growing)
                {
                    float dur = i < plotGrowDurations.Count ? plotGrowDurations[i] : 1f;
                    double start = i < plotGrowStarts.Count ? plotGrowStarts[i] : now;
                    if (dur > 0.01f)
                        t = Mathf.Clamp01((float)((now - start) / dur));
                }
                else if (state == (byte)GardenPlotState.Ready)
                {
                    t = 1f;
                }
                else
                {
                    t = 0f;
                }

                // Определить какая стадия нужна (1, 2 или 3)
                GameObject targetPrefab;
                if (t < 0.33f)
                    targetPrefab = wheatStage1Prefab; // Стадия 1: 0-33%
                else if (t < 0.66f)
                    targetPrefab = wheatStage2Prefab; // Стадия 2: 33-66%
                else
                    targetPrefab = wheatStage3Prefab; // Стадия 3: 66-100% (готова к сбору)

                // Проверить нужно ли заменить модель
                if (targetPrefab != null && !IsSamePrefab(wheat, targetPrefab))
                {
                    // Заменить модель
                    Vector3 currentPos = wheat.transform.position;
                    Quaternion currentRot = wheat.transform.rotation;
                    
                    Destroy(wheat);
                    GameObject newWheat = Instantiate(targetPrefab);
                    newWheat.name = $"Wheat_{i}";
                    newWheat.transform.position = currentPos;
                    newWheat.transform.rotation = currentRot;
                    newWheat.transform.SetParent(null, true);
                    
                    wheatVisuals[i] = newWheat;
                    wheat = newWheat;
                }

                // Установить позицию на точке посадки (без изменения высоты)
                wheat.transform.position = point.position;
                wheat.transform.rotation = Quaternion.identity;

                // Изменить цвет от молодого к спелому
                Color c = Color.Lerp(youngColor, ripeColor, t);
                ApplyColor(wheat, c);
            }
        }

        /// <summary>
        /// Проверить является ли объект экземпляром данного prefab
        /// </summary>
        private bool IsSamePrefab(GameObject obj, GameObject prefab)
        {
            if (obj == null || prefab == null)
                return false;
            
            // Простая проверка по имени (можно улучшить)
            string objName = obj.name.Replace("(Clone)", "").Trim();
            string prefabName = prefab.name.Trim();
            return objName.StartsWith(prefabName);
        }

        private void ApplyColor(GameObject go, Color color)
        {
            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_UnlitColor", color);
                mpb.SetColor("_BaseColor", color);
                mpb.SetColor("_Color", color);
                r.SetPropertyBlock(mpb);
            }
        }

        private void ClearWheatVisuals()
        {
            for (int i = 0; i < wheatVisuals.Count; i++)
            {
                if (wheatVisuals[i] != null)
                    Destroy(wheatVisuals[i]);
            }
            wheatVisuals.Clear();
        }

        private void ResolvePlantPoints()
        {
            // Убрать null из массива
            if (plantPoints != null && plantPoints.Length > 0)
            {
                List<Transform> cleaned = new List<Transform>(plantPoints.Length);
                for (int i = 0; i < plantPoints.Length; i++)
                {
                    if (plantPoints[i] != null)
                        cleaned.Add(plantPoints[i]);
                }

                if (cleaned.Count == plantPoints.Length)
                    return;

                if (cleaned.Count > 0)
                {
                    plantPoints = cleaned.ToArray();
                    return;
                }
            }

            List<Transform> found = new List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform)
                    continue;
                string n = child.name.ToLowerInvariant();
                if (n.Contains("empty") || n.Contains("plant"))
                    found.Add(child);
            }

            if (found.Count > 0)
                plantPoints = found.ToArray();
        }

        private void DisableNavMeshCarve()
        {
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = false;
        }

        /// <summary>
        /// Сериализация состояния грядок для сохранения.
        /// </summary>
        [Server]
        public string ServerGetPlotStatesJson()
        {
            if (plotStates.Count == 0)
                return "";

            var data = new GardenPlotsSaveData
            {
                states = new byte[plotStates.Count],
                growStarts = new double[plotGrowStarts.Count],
                growDurations = new float[plotGrowDurations.Count]
            };

            for (int i = 0; i < plotStates.Count; i++)
            {
                data.states[i] = plotStates[i];
            }

            for (int i = 0; i < plotGrowStarts.Count; i++)
            {
                data.growStarts[i] = plotGrowStarts[i];
            }

            for (int i = 0; i < plotGrowDurations.Count; i++)
            {
                data.growDurations[i] = plotGrowDurations[i];
            }

            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Восстановление состояния грядок из сохранения.
        /// </summary>
        [Server]
        public void ServerRestorePlotStates(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<GardenPlotsSaveData>(json);
                if (data == null || data.states == null || data.states.Length == 0)
                    return;

                EnsurePlotsInitialized();

                // Восстановить состояния
                int count = Mathf.Min(data.states.Length, plotStates.Count);
                for (int i = 0; i < count; i++)
                {
                    plotStates[i] = data.states[i];
                }

                // Восстановить время начала роста
                if (data.growStarts != null)
                {
                    int growCount = Mathf.Min(data.growStarts.Length, plotGrowStarts.Count);
                    for (int i = 0; i < growCount; i++)
                    {
                        plotGrowStarts[i] = data.growStarts[i];
                    }
                }

                // Восстановить длительность роста
                if (data.growDurations != null)
                {
                    int durCount = Mathf.Min(data.growDurations.Length, plotGrowDurations.Count);
                    for (int i = 0; i < durCount; i++)
                    {
                        plotGrowDurations[i] = data.growDurations[i];
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[GardenBed] Failed to restore plot states: {e.Message}");
            }
        }

        [System.Serializable]
        private class GardenPlotsSaveData
        {
            public byte[] states;
            public double[] growStarts;
            public float[] growDurations;
        }
    }
}
