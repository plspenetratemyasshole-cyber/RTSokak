using Mirror;
using UnityEngine;
using UnityEngine.AI;
using RTSokak.Network;

namespace RTSokak.Buildings
{
    /// <summary>
    /// Очередь производства Worker в столице. Одновременно тренируется только первый в очереди.
    /// </summary>
    [RequireComponent(typeof(Building))]
    public class CapitalProduction : NetworkBehaviour, IUnitTrainingQueue
    {
        [Header("Worker Training")]
        [SerializeField] private int workerFoodCost = 150;
        [SerializeField] private float workerTrainTime = 10f;
        [Tooltip("Сколько заказов в очереди (тренируется только один сразу)")]
        [SerializeField] private int maxQueueSize = 5;

        [SyncVar(hook = nameof(OnQueueCountChanged))]
        private int queueCount;

        [SyncVar(hook = nameof(OnProgressChanged))]
        private float activeProgress;

        private Building building;

        public int WorkerFoodCost => workerFoodCost;
        public float WorkerTrainTime => Mathf.Max(0.1f, workerTrainTime);
        public int MaxQueueSize => Mathf.Max(1, maxQueueSize);
        public int QueueCount => queueCount;
        public float ActiveProgress => Mathf.Clamp01(activeProgress);

        public event System.Action OnQueueChanged;

        public override void OnStartServer()
        {
            base.OnStartServer();
            building = GetComponent<Building>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            building = GetComponent<Building>();
            OnQueueChanged?.Invoke();
        }

        private void OnQueueCountChanged(int oldV, int newV) => OnQueueChanged?.Invoke();
        private void OnProgressChanged(float oldV, float newV) => OnQueueChanged?.Invoke();

        [Server]
        public void ServerRestoreQueue(int count, float progress)
        {
            queueCount = Mathf.Clamp(count, 0, MaxQueueSize);
            activeProgress = queueCount > 0 ? Mathf.Clamp01(progress) : 0f;
        }

        private void Update()
        {
            if (!isServer)
                return;

            if (building == null || !building.IsComplete)
                return;

            if (queueCount <= 0)
            {
                if (activeProgress != 0f)
                    activeProgress = 0f;
                return;
            }

            float duration = WorkerTrainTime;
            float p = activeProgress + Time.deltaTime / duration;
            if (p >= 1f)
            {
                queueCount = Mathf.Max(0, queueCount - 1);
                activeProgress = 0f;
                SpawnTrainedWorker();
            }
            else
            {
                activeProgress = p;
            }
        }

        /// <summary>Прогресс слота 0..1 (только первый тренируется, остальные 0).</summary>
        public float GetSlotProgress(int index)
        {
            if (index < 0 || index >= queueCount)
                return 0f;
            if (index == 0)
                return ActiveProgress;
            return 0f;
        }

        public int GetSlotPercent(int index)
        {
            return Mathf.RoundToInt(GetSlotProgress(index) * 100f);
        }

        public void RequestEnqueueWorker()
        {
            if (building == null || !building.IsOwnedByLocalPlayer())
                return;
            CmdEnqueueWorker();
        }

        public void RequestCancelSlot(int index)
        {
            if (building == null || !building.IsOwnedByLocalPlayer())
                return;
            CmdCancelSlot(index);
        }

        [Command(requiresAuthority = false)]
        private void CmdEnqueueWorker(NetworkConnectionToClient sender = null)
        {
            if (sender == null || building == null)
                return;
            if (sender.connectionId != building.OwnerConnectionId)
                return;
            if (!building.IsComplete)
                return;

            if (queueCount >= MaxQueueSize)
            {
                UnityEngine.Debug.Log("[CapitalProduction] Queue full");
                return;
            }

            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player == null)
                return;

            if (player.CurrentPopulation + queueCount >= player.MaxPopulation)
            {
                UnityEngine.Debug.Log("[CapitalProduction] Not enough population");
                return;
            }

            if (!player.TrySpend(0, 0, workerFoodCost))
            {
                UnityEngine.Debug.Log("[CapitalProduction] Not enough food");
                return;
            }

            queueCount++;
            UnityEngine.Debug.Log($"[CapitalProduction] Queued worker, queue={queueCount}");
        }

        [Command(requiresAuthority = false)]
        private void CmdCancelSlot(int index, NetworkConnectionToClient sender = null)
        {
            if (sender == null || building == null)
                return;
            if (sender.connectionId != building.OwnerConnectionId)
                return;
            if (index < 0 || index >= queueCount)
                return;

            if (index == 0)
                activeProgress = 0f;

            queueCount--;

            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player != null)
                player.AddFood(workerFoodCost);

            UnityEngine.Debug.Log($"[CapitalProduction] Cancelled slot {index}, refunded {workerFoodCost} food");
        }

        [Server]
        private void SpawnTrainedWorker()
        {
            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player == null)
                return;

            var nm = NetworkManager.singleton as RTSNetworkManager;
            if (nm == null || nm.WorkerPrefab == null)
            {
                UnityEngine.Debug.LogError("[CapitalProduction] Worker prefab missing on RTSNetworkManager");
                return;
            }

            if (!NetworkServer.connections.TryGetValue(building.OwnerConnectionId, out NetworkConnectionToClient conn))
            {
                UnityEngine.Debug.LogWarning("[CapitalProduction] Owner connection missing");
                return;
            }

            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject workerObj = Instantiate(nm.WorkerPrefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(workerObj);

            Worker worker = workerObj.GetComponent<Worker>();
            if (worker != null)
                worker.SetOwner(conn);

            player.AddCurrentPopulation(1);
            UnityEngine.Debug.Log($"[CapitalProduction] Spawned worker at {spawnPos}");
        }

        [Server]
        private Vector3 GetRandomSpawnPosition()
        {
            float radius = 5f;
            if (BuildingManager.Instance != null)
            {
                var def = BuildingManager.Instance.GetDefinition(BuildingType.Capital);
                if (def != null)
                    radius = Mathf.Max(1f, def.approachDistance);
            }

            Vector3 center = building != null ? building.FinalPosition : transform.position;
            center.y = 0f;

            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude < 0.01f)
                circle = Vector2.right;

            Vector3 pos = center + new Vector3(circle.x, 0f, circle.y) * radius;
            pos.y = 1f;

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, radius + 4f, NavMesh.AllAreas))
                return hit.position + Vector3.up * 0.05f;

            return pos;
        }

        private static RTSPlayer FindPlayer(int connId)
        {
            var all = RTSPlayer.AllPlayers;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p != null && p.ConnectionId == connId)
                    return p;
            }

            return null;
        }
    }
}
