using Mirror;
using UnityEngine;
using UnityEngine.AI;
using RTSokak.Network;

namespace RTSokak.Buildings
{
    /// <summary>
    /// Очередь производства юнитов (Warrior, Archer) в казарме.
    /// </summary>
    [RequireComponent(typeof(Building))]
    public class BarracksProduction : NetworkBehaviour, IUnitTrainingQueue
    {
        public enum UnitType
        {
            Warrior,
            Archer
        }

        [System.Serializable]
        public class UnitConfig
        {
            public UnitType type;
            public int foodCost = 200;
            public float trainTime = 12f;
        }

        [Header("Unit Training")]
        [SerializeField] private UnitConfig[] unitConfigs = new UnitConfig[]
        {
            new UnitConfig { type = UnitType.Warrior, foodCost = 200, trainTime = 12f },
            new UnitConfig { type = UnitType.Archer, foodCost = 250, trainTime = 15f }
        };
        [SerializeField] private int maxQueueSize = 5;

        [System.Serializable]
        private struct QueueSlot
        {
            public UnitType unitType;
            public float progress;
        }

        // SyncList для очереди
        private readonly SyncList<int> queuedUnitTypes = new SyncList<int>();
        
        [SyncVar(hook = nameof(OnProgressChanged))]
        private float activeProgress;

        private Building building;

        public int WarriorFoodCost => GetUnitConfig(UnitType.Warrior)?.foodCost ?? 200;
        public float WarriorTrainTime => GetUnitConfig(UnitType.Warrior)?.trainTime ?? 12f;
        public int ArcherFoodCost => GetUnitConfig(UnitType.Archer)?.foodCost ?? 250;
        public float ArcherTrainTime => GetUnitConfig(UnitType.Archer)?.trainTime ?? 15f;
        public int MaxQueueSize => Mathf.Max(1, maxQueueSize);
        public int QueueCount => queuedUnitTypes.Count;
        public float ActiveProgress => Mathf.Clamp01(activeProgress);

        public event System.Action OnQueueChanged;

        private UnitConfig GetUnitConfig(UnitType type)
        {
            foreach (var config in unitConfigs)
            {
                if (config.type == type)
                    return config;
            }
            return null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            building = GetComponent<Building>();
            queuedUnitTypes.Callback += OnQueueListChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            building = GetComponent<Building>();
            queuedUnitTypes.Callback += OnQueueListChanged;
            OnQueueChanged?.Invoke();
        }

        private void OnQueueListChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
        {
            OnQueueChanged?.Invoke();
        }

        private void OnProgressChanged(float oldV, float newV) => OnQueueChanged?.Invoke();

        [Server]
        public void ServerRestoreQueue(int count, float progress, UnitType[]  unitTypes = null)
        {
            queuedUnitTypes.Clear();
            if (unitTypes != null)
            {
                for (int i = 0; i < Mathf.Min(count, unitTypes.Length); i++)
                {
                    queuedUnitTypes.Add((int)unitTypes[i]);
                }
            }
            else
            {
                // Fallback для старых сохранений - все Warrior
                for (int i = 0; i < count; i++)
                {
                    queuedUnitTypes.Add((int)UnitType.Warrior);
                }
            }
            activeProgress = queuedUnitTypes.Count > 0 ? Mathf.Clamp01(progress) : 0f;
        }

        private void Update()
        {
            if (!isServer)
                return;

            if (building == null || !building.IsComplete)
                return;

            if (queuedUnitTypes.Count <= 0)
            {
                if (activeProgress != 0f)
                    activeProgress = 0f;
                return;
            }

            // Получить тип текущего юнита
            UnitType currentType = (UnitType)queuedUnitTypes[0];
            UnitConfig config = GetUnitConfig(currentType);
            if (config == null)
            {
                UnityEngine.Debug.LogWarning($"[BarracksProduction] Unknown unit type {currentType}");
                queuedUnitTypes.RemoveAt(0);
                activeProgress = 0f;
                return;
            }

            float duration = Mathf.Max(0.1f, config.trainTime);
            float p = activeProgress + Time.deltaTime / duration;
            if (p >= 1f)
            {
                queuedUnitTypes.RemoveAt(0);
                activeProgress = 0f;
                SpawnTrainedUnit(currentType);
            }
            else
            {
                activeProgress = p;
            }
        }

        public float GetSlotProgress(int index)
        {
            if (index < 0 || index >= queuedUnitTypes.Count)
                return 0f;
            if (index == 0)
                return ActiveProgress;
            return 0f;
        }

        public int GetSlotPercent(int index)
        {
            return Mathf.RoundToInt(GetSlotProgress(index) * 100f);
        }

        public void RequestEnqueueWarrior()
        {
            if (building == null || !building.IsOwnedByLocalPlayer())
                return;
            CmdEnqueueUnit(UnitType.Warrior);
        }

        public void RequestEnqueueArcher()
        {
            if (building == null || !building.IsOwnedByLocalPlayer())
                return;
            CmdEnqueueUnit(UnitType.Archer);
        }

        public void RequestCancelSlot(int index)
        {
            if (building == null || !building.IsOwnedByLocalPlayer())
                return;
            CmdCancelSlot(index);
        }

        [Command(requiresAuthority = false)]
        private void CmdEnqueueUnit(UnitType unitType, NetworkConnectionToClient sender = null)
        {
            if (sender == null || building == null)
                return;
            if (sender.connectionId != building.OwnerConnectionId)
                return;
            if (!building.IsComplete)
                return;

            if (queuedUnitTypes.Count >= MaxQueueSize)
            {
                UnityEngine.Debug.Log("[BarracksProduction] Queue full");
                return;
            }

            UnitConfig config = GetUnitConfig(unitType);
            if (config == null)
            {
                UnityEngine.Debug.LogWarning($"[BarracksProduction] Unknown unit type {unitType}");
                return;
            }

            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player == null)
                return;

            if (player.CurrentPopulation + queuedUnitTypes.Count >= player.MaxPopulation)
            {
                UnityEngine.Debug.Log("[BarracksProduction] Not enough population");
                return;
            }

            if (!player.TrySpend(0, 0, config.foodCost))
            {
                UnityEngine.Debug.Log($"[BarracksProduction] Not enough food ({config.foodCost} needed)");
                return;
            }

            queuedUnitTypes.Add((int)unitType);
            UnityEngine.Debug.Log($"[BarracksProduction] Queued {unitType}, queue={queuedUnitTypes.Count}");
        }

        [Command(requiresAuthority = false)]
        private void CmdCancelSlot(int index, NetworkConnectionToClient sender = null)
        {
            if (sender == null || building == null)
                return;
            if (sender.connectionId != building.OwnerConnectionId)
                return;
            if (index < 0 || index >= queuedUnitTypes.Count)
                return;

            // Получить тип юнита для возврата ресурсов
            UnitType unitType = (UnitType)queuedUnitTypes[index];
            UnitConfig config = GetUnitConfig(unitType);
            int refundAmount = config != null ? config.foodCost : 200;

            if (index == 0)
                activeProgress = 0f;

            queuedUnitTypes.RemoveAt(index);

            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player != null)
                player.AddFood(refundAmount);

            UnityEngine.Debug.Log($"[BarracksProduction] Cancelled slot {index} ({unitType}), refunded {refundAmount} food");
        }

        [Server]
        private void SpawnTrainedUnit(UnitType unitType)
        {
            RTSPlayer player = FindPlayer(building.OwnerConnectionId);
            if (player == null)
                return;

            var nm = NetworkManager.singleton as RTSNetworkManager;
            if (nm == null)
            {
                UnityEngine.Debug.LogError("[BarracksProduction] RTSNetworkManager missing");
                return;
            }

            GameObject prefab = null;
            string unitName = "";
            
            switch (unitType)
            {
                case UnitType.Warrior:
                    prefab = nm.WarriorPrefab;
                    unitName = "Warrior";
                    break;
                case UnitType.Archer:
                    prefab = nm.ArcherPrefab;
                    unitName = "Archer";
                    break;
            }

            if (prefab == null)
            {
                UnityEngine.Debug.LogError($"[BarracksProduction] {unitName} prefab missing on RTSNetworkManager");
                return;
            }

            if (!NetworkServer.connections.TryGetValue(building.OwnerConnectionId, out NetworkConnectionToClient conn))
            {
                UnityEngine.Debug.LogWarning("[BarracksProduction] Owner connection missing");
                return;
            }

            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject unitObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(unitObj);

            RTSUnit unit = unitObj.GetComponent<RTSUnit>();
            if (unit != null)
                unit.SetOwner(conn);

            player.AddCurrentPopulation(1);
            UnityEngine.Debug.Log($"[BarracksProduction] Spawned {unitName} at {spawnPos}");
        }

        [Server]
        private Vector3 GetRandomSpawnPosition()
        {
            float radius = 5f;
            if (BuildingManager.Instance != null)
            {
                var def = BuildingManager.Instance.GetDefinition(BuildingType.Barracks);
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
            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p.ConnectionId == connId)
                    return p;
            }

            return null;
        }
    }
}
