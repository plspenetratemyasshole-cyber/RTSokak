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
        private readonly List<int> wheatStageIndices = new List<int>();
        private readonly List<float> wheatSpawnTimes = new List<float>();
        private readonly List<Vector3> wheatBaseScales = new List<Vector3>();
        private readonly List<Renderer[]> wheatRenderers = new List<Renderer[]>();
        private readonly List<Color> wheatLastColors = new List<Color>();
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
                    wheatStageIndices[i] = -1;
                    continue;
                }

                if (wheatVisuals[i] == null)
                {
                    wheatVisuals[i] = SpawnWheatVisual(i, 1);
                    wheatStageIndices[i] = 1;
                    wheatSpawnTimes[i] = Time.time;
                }
            }

            UpdateWheatVisuals();
        }

        private void EnsureWheatListSize()
        {
            int n = plantPoints != null ? plantPoints.Length : 0;
            while (wheatVisuals.Count < n)
            {
                wheatVisuals.Add(null);
                wheatStageIndices.Add(-1);
                wheatSpawnTimes.Add(0f);
                wheatBaseScales.Add(Vector3.one);
                wheatRenderers.Add(null);
                wheatLastColors.Add(Color.clear);
            }
            while (wheatVisuals.Count > n)
            {
                int last = wheatVisuals.Count - 1;
                if (wheatVisuals[last] != null)
                    Destroy(wheatVisuals[last]);
                wheatVisuals.RemoveAt(last);
                wheatStageIndices.RemoveAt(last);
                wheatSpawnTimes.RemoveAt(last);
                wheatBaseScales.RemoveAt(last);
                wheatRenderers.RemoveAt(last);
                wheatLastColors.RemoveAt(last);
            }
        }

        private GameObject SpawnWheatVisual(int index, int stage)
        {
            GameObject prefab = wheatStage1Prefab;
            if (stage == 2 && wheatStage2Prefab != null) prefab = wheatStage2Prefab;
            else if (stage == 3 && wheatStage3Prefab != null) prefab = wheatStage3Prefab;

            if (prefab == null)
                prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Wheat");

            GameObject go;
            Vector3 baseScale;
            if (prefab != null)
            {
                go = Instantiate(prefab);
                baseScale = prefab.transform.localScale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
                baseScale = new Vector3(0.25f, 0.45f, 0.25f);
            }

            go.name = $"Wheat_{index}";
            go.transform.SetParent(null, true);
            go.transform.localScale = baseScale;
            go.transform.rotation = Quaternion.identity;

            if (index >= 0 && index < wheatBaseScales.Count)
            {
                wheatBaseScales[index] = baseScale;
                wheatRenderers[index] = go.GetComponentsInChildren<Renderer>(true);
                wheatLastColors[index] = Color.clear;
            }

            return go;
        }

        private void UpdateWheatVisuals()
        {
            if (plantPoints == null)
                return;

            if (building == null)
                building = GetComponent<Building>();
            bool isBuildingComplete = building != null && building.IsComplete;

            var cam = RTSokak.UI.UnitHealthBar.ResolveBillboardCamera(null);
            bool farFromCam = cam != null && (cam.transform.position - transform.position).sqrMagnitude > 60f * 60f;

            double now = NetworkTime.time;
            for (int i = 0; i < wheatVisuals.Count && i < plotStates.Count; i++)
            {
                Transform point = plantPoints[i];
                if (point == null)
                    continue;

                byte state = plotStates[i];
                if (state == (byte)GardenPlotState.Empty)
                {
                    if (wheatVisuals[i] != null)
                    {
                        Destroy(wheatVisuals[i]);
                        wheatVisuals[i] = null;
                    }
                    wheatStageIndices[i] = -1;
                    if (i < wheatRenderers.Count) wheatRenderers[i] = null;
                    if (i < wheatLastColors.Count) wheatLastColors[i] = Color.clear;
                    continue;
                }

                float t = 1f;
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

                int targetStage = t < 0.33f ? 1 : (t < 0.66f ? 2 : 3);

                // Заменять модель только при реальной смене стадии
                if (wheatVisuals[i] == null || wheatStageIndices[i] != targetStage)
                {
                    if (wheatVisuals[i] != null)
                        Destroy(wheatVisuals[i]);

                    wheatVisuals[i] = SpawnWheatVisual(i, targetStage);
                    wheatStageIndices[i] = targetStage;
                    wheatSpawnTimes[i] = Time.time;
                }

                GameObject wheat = wheatVisuals[i];
                if (wheat == null)
                    continue;

                // Установить позицию на точке посадки (не обновляем каждый кадр если здание завершено)
                if (!isBuildingComplete || (wheat.transform.position - point.position).sqrMagnitude > 0.001f)
                    wheat.transform.position = point.position;

                // Покачивание колосьев на ветру (пропускаем если далеко от камеры > 60м)
                if (!farFromCam)
                {
                    float windX = Mathf.Sin(Time.time * 2.2f + i * 1.3f) * 4.5f * (0.3f + 0.7f * t);
                    float windZ = Mathf.Cos(Time.time * 1.8f + i * 0.9f) * 3.5f * (0.3f + 0.7f * t);
                    wheat.transform.rotation = Quaternion.Euler(windX, (i * 57f) % 360f, windZ);
                }

                // Плавный pop-in и мягкий рост масштаба
                Vector3 baseScale = i < wheatBaseScales.Count ? wheatBaseScales[i] : Vector3.one;
                float popElapsed = Time.time - (i < wheatSpawnTimes.Count ? wheatSpawnTimes[i] : 0f);
                float pop = popElapsed < 0.25f ? Mathf.SmoothStep(0.25f, 1f, popElapsed / 0.25f) : 1f;
                float growthScale = Mathf.Lerp(0.65f, 1.0f, t);
                wheat.transform.localScale = baseScale * (growthScale * pop);

                // Изменить цвет от молодого к спелому
                Color c = Color.Lerp(youngColor, ripeColor, t);
                ApplyColor(i, c);
            }
        }

        private void ApplyColor(int index, Color color)
        {
            if (index < 0 || index >= wheatRenderers.Count)
                return;

            Color last = wheatLastColors[index];
            if (Mathf.Abs(color.r - last.r) + Mathf.Abs(color.g - last.g) + Mathf.Abs(color.b - last.b) < 0.01f)
                return;

            wheatLastColors[index] = color;
            Renderer[] renderers = wheatRenderers[index];
            if (renderers == null)
                return;

            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend == null) continue;
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_UnlitColor", color);
                mpb.SetColor("_BaseColor", color);
                mpb.SetColor("_Color", color);
                rend.SetPropertyBlock(mpb);
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
            wheatStageIndices.Clear();
            wheatSpawnTimes.Clear();
            wheatBaseScales.Clear();
            wheatRenderers.Clear();
            wheatLastColors.Clear();
        }

        /// <summary>
        /// Поиск ближайшей лунки для игрока в FPS режиме.
        /// </summary>
        public bool TryFindPlayerInteractablePlot(Vector3 playerPos, Vector3 lookDir, float maxDist, out int plotIndex, out GardenPlotState state, out float growProgress)
        {
            plotIndex = -1;
            state = GardenPlotState.Empty;
            growProgress = 0f;

            if (building == null)
                building = GetComponent<Building>();
            if (building == null || !building.IsComplete)
                return false;

            if (plantPoints == null || plantPoints.Length == 0 || plotStates.Count == 0)
                return false;

            Vector3 lookFlat = new Vector3(lookDir.x, 0f, lookDir.z);
            if (lookFlat.sqrMagnitude > 0.001f)
                lookFlat.Normalize();
            else
                lookFlat = Vector3.forward;

            int bestActionPlot = -1;
            float bestActionScore = float.MaxValue;
            GardenPlotState bestActionState = GardenPlotState.Empty;

            int bestGrowingPlot = -1;
            float bestGrowingDist = float.MaxValue;
            float bestGrowingProgress = 0f;

            double now = NetworkTime.time;

            for (int i = 0; i < plantPoints.Length && i < plotStates.Count; i++)
            {
                if (plantPoints[i] == null)
                    continue;

                Vector3 plotWorld = plantPoints[i].position;
                Vector3 diff = plotWorld - playerPos;
                float distXZ = new Vector2(diff.x, diff.z).magnitude;

                GardenPlotState s = (GardenPlotState)plotStates[i];

                if (s == GardenPlotState.Ready || s == GardenPlotState.Empty)
                {
                    if (distXZ <= maxDist)
                    {
                        Vector3 dirToPlot = diff;
                        dirToPlot.y = 0f;
                        if (dirToPlot.sqrMagnitude > 0.001f)
                            dirToPlot.Normalize();

                        float dot = Vector3.Dot(lookFlat, dirToPlot);
                        float priorityBias = (s == GardenPlotState.Ready) ? -1.2f : 0f;
                        float score = distXZ - dot * 0.9f + priorityBias;

                        if (score < bestActionScore)
                        {
                            bestActionScore = score;
                            bestActionPlot = i;
                            bestActionState = s;
                        }
                    }
                }
                else if (s == GardenPlotState.Growing)
                {
                    if (distXZ <= 2.2f && distXZ < bestGrowingDist)
                    {
                        bestGrowingDist = distXZ;
                        bestGrowingPlot = i;

                        float dur = i < plotGrowDurations.Count ? plotGrowDurations[i] : 1f;
                        double start = i < plotGrowStarts.Count ? plotGrowStarts[i] : now;
                        bestGrowingProgress = dur > 0.01f ? Mathf.Clamp01((float)((now - start) / dur)) : 0f;
                    }
                }
            }

            if (bestActionPlot >= 0)
            {
                plotIndex = bestActionPlot;
                state = bestActionState;
                growProgress = (state == GardenPlotState.Ready) ? 1f : 0f;
                return true;
            }

            if (bestGrowingPlot >= 0)
            {
                plotIndex = bestGrowingPlot;
                state = GardenPlotState.Growing;
                growProgress = bestGrowingProgress;
                return true;
            }

            return false;
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
