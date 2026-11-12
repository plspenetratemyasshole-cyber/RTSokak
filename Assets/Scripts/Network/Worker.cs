using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RTSokak.Network
{
    /// <summary>
    /// Состояния рабочего
    /// </summary>
    public enum WorkerState
    {
        Idle,
        Moving,
        Harvesting,
        SearchingResource,
        Depositing,
        MovingToBuild,
        Building,
        Farming
    }

    /// <summary>
    /// Профессии рабочих
    /// </summary>
    public enum WorkerProfession
    {
        Worker,
        Miner,
        Builder,
        Scout
    }

    /// <summary>
    /// Рабочий: добыча, стройка, ферма + ближний бой (базовый RTSUnit).
    /// </summary>
    [RequireComponent(typeof(Outline))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class Worker : RTSUnit
    {
        [SyncVar(hook = nameof(OnProfessionChanged))]
        private WorkerProfession profession = WorkerProfession.Worker;

        [SyncVar(hook = nameof(OnActiveToolChanged))]
        private int activeToolType = -1; // -1 = нет, 0 = топор, 1 = кирка, 2 = молоток, 3 = меч (через base sword)

        [SyncVar(hook = nameof(OnInventoryWoodChanged))]
        private int inventoryWood;
        [SyncVar(hook = nameof(OnInventoryOreChanged))]
        private int inventoryOre;
        [SyncVar(hook = nameof(OnInventoryFoodChanged))]
        private int inventoryFood;

        public WorkerProfession Profession => profession;
        public int InventoryWood => inventoryWood;
        public int InventoryOre => inventoryOre;
        public int InventoryFood => inventoryFood;
        public int MaxInventory => maxInventory;
        public int InventoryTotal => inventoryWood + inventoryOre + inventoryFood;
        public bool IsInventoryFull => InventoryTotal >= maxInventory;

        public override bool CanDoEconomyTasks => true;
        public override bool HasInventory => true;

        public event System.Action OnInventoryUpdated;

        // Legacy name used in older code paths
        private bool isOwned => IsOwned;

        // Harvesting state
        private WorkerState currentState = WorkerState.Idle;
        private RTSokak.Resources.Resource targetResource;
        private RTSokak.Resources.ResourceType harvestingResourceType;
        private float harvestStartTime;
        private bool isSearchingForResource = false;
        private RTSokak.Resources.ResourceManager cachedResourceManager;

        // Inventory / deposit
        private Vector3 lastHarvestPoint;
        private bool resumeHarvestAfterDeposit;
        private RTSokak.Resources.ResourceType resumeHarvestType;
        private bool useLastHarvestPointForSearch;
        private bool resumeFarmingAfterDeposit;

        // Construction
        private RTSokak.Buildings.Building targetBuilding;
        private float nextBuildSwingTime;
        private Vector3 cachedApproachPoint;
        private bool hasCachedApproach;

        // Farming
        private RTSokak.Buildings.GardenBed targetGarden;
        private int farmingPlotIndex = -1;
        private bool farmingHarvestIntent;
        private Vector3 farmingNavDestination;
        private float farmingMoveStartTime;
        [Tooltip("Насколько близко нужно подойти к точке посадки/сбора (м)")]
        [SerializeField] private float farmInteractDistance = 0.9f;
        [SerializeField] private float farmStuckTimeout = 5f;
        private float farmingSavedStoppingDistance = -1f;

        // Плавный поворот к ресурсу
        private Quaternion harvestTargetRotation;
        private bool isRotatingToHarvest = false;

        private RTSokak.UI.UnitNameplate nameplate;
        
        [Header("Harvesting Settings")]
        [Tooltip("Время на добычу дерева (секунды)")]
        [SerializeField] private float woodHarvestDuration = 1.5f;
        [Tooltip("Время на добычу руды (секунды)")]
        [SerializeField] private float oreHarvestDuration = 2.0f;
        [Tooltip("Длительность одного удара юнита-игрока (сек). Ресурсы выдаются сразу после этого.")]
        [SerializeField] private float playerToolSwingDuration = 0.45f;
        [SerializeField] private float harvestDistanceTolerance = 6f;
        [SerializeField] private float autoHarvestRadius = 10f;
        [Tooltip("Радиус поиска свободного ресурса, если кликнутый уже занят")]
        [SerializeField] private float redirectSearchRadius = 80f;
        
        [Header("UI")]
        [SerializeField] private GameObject nameplatePrefab;
        
        [Header("Inventory")]
        [Tooltip("Максимум ресурсов в инвентаре юнита")]
        [SerializeField] private int maxInventory = 500;

        [Header("Harvest Tools")]
        [Tooltip("Prefab топора для добычи дерева")]
        [SerializeField] private GameObject axePrefab;
        [Tooltip("Prefab кирки для добычи руды")]
        [SerializeField] private GameObject pickaxePrefab;
        [Tooltip("Prefab молотка для строительства")]
        [SerializeField] private GameObject hammerPrefab;
        
        // Runtime переменные для анимации harvest tools
        private GameObject currentToolInstance;
        private float toolAnimationStartTime;
        private bool isAnimatingTool;
        /// <summary>0 = CurrentHarvestDuration; иначе длительность одного свинга (юнит-игрок).</summary>
        private float toolAnimDurationOverride;

        // Кэш NetworkIdentity (alias к base.unitNetIdentity)
        private NetworkIdentity netIdentity;

        /// <summary>
        /// Возвращает длительность добычи для текущего типа ресурса
        /// </summary>
        private float CurrentHarvestDuration
        {
            get
            {
                if (currentState == WorkerState.Building)
                    return 1f;

                return harvestingResourceType == RTSokak.Resources.ResourceType.Wood
                    ? woodHarvestDuration
                    : oreHarvestDuration;
            }
        }

        private float EffectiveToolAnimDuration =>
            toolAnimDurationOverride > 0.01f ? toolAnimDurationOverride : CurrentHarvestDuration;

        private void OnInventoryWoodChanged(int o, int n) => OnInventoryUpdated?.Invoke();
        private void OnInventoryOreChanged(int o, int n) => OnInventoryUpdated?.Invoke();
        private void OnInventoryFoodChanged(int o, int n) => OnInventoryUpdated?.Invoke();

        protected override void Awake()
        {
            base.Awake();
            netIdentity = unitNetIdentity;
        }

        /// <summary>
        /// Серверное движение: warp на NavMesh при необходимости.
        /// </summary>
        [Server]
        private new bool TrySetAgentDestination(Vector3 destination, float sampleRadius = 8f)
        {
            if (navAgent == null || !navAgent.enabled)
                return false;

            if (!navAgent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                    navAgent.Warp(hit.position);
                else
                {
                    UnityEngine.Debug.LogWarning($"[Worker] {name} not on NavMesh at {transform.position}");
                    return false;
                }
            }

            if (sampleRadius > 0.01f &&
                NavMesh.SamplePosition(destination, out NavMeshHit destHit, sampleRadius, NavMesh.AllAreas))
                destination = destHit.position;

            if (navAgent.hasPath && !navAgent.pathPending)
            {
                Vector3 cur = navAgent.destination;
                cur.y = 0f;
                Vector3 dst = destination;
                dst.y = 0f;
                if (Vector3.Distance(cur, dst) < 0.15f)
                {
                    navAgent.isStopped = false;
                    return true;
                }
            }

            navAgent.isStopped = false;
            return navAgent.SetDestination(destination);
        }

        protected override void OnDestroy()
        {
            if (nameplate != null)
            {
                Destroy(nameplate.gameObject);
                nameplate = null;
            }

            if (isServer)
            {
                ReleaseCurrentResourceClaim();
                LeaveConstruction();
                LeaveFarming();
                ServerStopCombat();
            }

            base.OnDestroy();
        }

        protected override void Start()
        {
            base.Start();

            if (isServer)
            {
                UnityEngine.Debug.Log($"[Worker Start] stoppingDistance={stoppingDistance}, harvestTolerance={harvestDistanceTolerance}, autoHarvestRadius={autoHarvestRadius}, moveSpeed={moveSpeed}");
                
                cachedResourceManager = FindObjectOfType<RTSokak.Resources.ResourceManager>();
                if (navAgent != null)
                    navAgent.stoppingDistance = stoppingDistance;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CreateNameplate();
            
            // Убедиться что outline обновлён после всех инициализаций
            UpdateOutline();
        }

        /// <summary>
        /// Создаёт nameplate для юнита
        /// </summary>
        private void CreateNameplate()
        {
            // Если есть префаб - используем его
            if (nameplatePrefab != null)
            {
                GameObject nameplateObj = Instantiate(nameplatePrefab, transform.position, Quaternion.identity);
                nameplate = nameplateObj.GetComponent<RTSokak.UI.UnitNameplate>();
            }
            else
            {
                // Создаём nameplate программно
                GameObject nameplateObj = new GameObject("Nameplate");
                nameplate = nameplateObj.AddComponent<RTSokak.UI.UnitNameplate>();
                
                // Добавляем Canvas
                Canvas canvas = nameplateObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                
                // Добавляем CanvasScaler
                CanvasScaler scaler = nameplateObj.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10;
                
                // Создаём текст
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(nameplateObj.transform);
                
                TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.fontSize = 36;
                text.color = Color.white;
                
                RectTransform textRect = text.GetComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(200, 50);
                textRect.localPosition = Vector3.zero;
                
                // Настраиваем nameplate component через reflection
                var textField = typeof(RTSokak.UI.UnitNameplate).GetField("professionText",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (textField != null)
                {
                    textField.SetValue(nameplate, text);
                }
                
                var canvasField = typeof(RTSokak.UI.UnitNameplate).GetField("canvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (canvasField != null)
                {
                    canvasField.SetValue(nameplate, canvas);
                }

                // Устанавливаем textSize и fontSize через reflection
                var textSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("textSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (textSizeField != null)
                {
                    textSizeField.SetValue(nameplate, 0.02f); // Маленький размер
                }

                var fontSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("fontSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fontSizeField != null)
                {
                    fontSizeField.SetValue(nameplate, 18f);
                }

                // Вызываем Awake чтобы применить настройки
                System.Reflection.MethodInfo awakeMethod = typeof(RTSokak.UI.UnitNameplate).GetMethod("Awake",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (awakeMethod != null)
                {
                    awakeMethod.Invoke(nameplate, null);
                }
            }
            
            if (nameplate != null)
            {
                nameplate.SetTarget(transform);
                nameplate.UpdateProfession(profession.ToString());
            }
        }

        /// <summary>
        /// Вызывается когда клиент получает authority над объектом
        /// </summary>
        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
        }

        /// <summary>
        /// Hook вызывается когда профессия меняется
        /// </summary>
        private void OnProfessionChanged(WorkerProfession oldProf, WorkerProfession newProf)
        {
            UnityEngine.Debug.Log($"Worker profession changed: {oldProf} -> {newProf}");
            
            // Обновляем nameplate
            if (nameplate != null)
            {
                nameplate.UpdateProfession(newProf.ToString());
            }
        }

        /// <summary>
        /// Устанавливает профессию рабочего (только на сервере)
        /// </summary>
        [Server]
        public void SetProfession(WorkerProfession newProfession)
        {
            profession = newProfession;
        }

        protected override void Update()
        {
            base.Update();

            if (isServer && IsPlayerControlled)
            {
                ServerUpdatePlayerHarvestPending();
                ServerUpdatePlayerAutoDeposit();
            }

            // Плавный поворот к ресурсу перед добычей
            if (isRotatingToHarvest && currentState == WorkerState.Harvesting)
            {
                // Плавно поворачиваем используя angularSpeed из NavMeshAgent
                float rotationSpeed = angularSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, harvestTargetRotation, rotationSpeed);
                
                // Проверяем достигли ли нужного угла
                if (Quaternion.Angle(transform.rotation, harvestTargetRotation) < 5f)
                {
                    isRotatingToHarvest = false;
                    // Начинаем анимацию инструмента после завершения поворота
                    if (currentToolInstance != null && !isAnimatingTool)
                    {
                        isAnimatingTool = true;
                        toolAnimationStartTime = Time.time;
                        UnityEngine.Debug.Log("Rotation complete, starting harvest animation");
                    }
                }
            }
            
            // Если поворот завершен но анимация еще не начата (инструмент может быть создан позже)
            if (!isRotatingToHarvest && currentState == WorkerState.Harvesting && 
                currentToolInstance != null && !isAnimatingTool)
            {
                isAnimatingTool = true;
                toolAnimationStartTime = Time.time;
                UnityEngine.Debug.Log("Starting harvest animation (tool created after rotation)");
            }
            
            // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: если уже долго добываем но анимация не запустилась
            // (race condition между RpcStartHarvesting и OnActiveToolChanged)
            if (currentState == WorkerState.Harvesting && currentToolInstance != null && 
                !isAnimatingTool && !isRotatingToHarvest && Time.time - harvestStartTime > 1f)
            {
                isAnimatingTool = true;
                toolAnimationStartTime = Time.time;
                UnityEngine.Debug.Log("Starting harvest animation (delayed start after 1 second)");
            }
            
            // Outline в FPS юнит-игрока выключен (см. RTSUnit.UpdateOutline) — не дёргаем каждый сек
            if (Time.frameCount % 60 == 0 &&
                (RTSPlayer.localPlayer == null || RTSPlayer.localPlayer.Role != PlayerRole.UnitPlaying))
            {
                UpdateOutline();
            }

            // В FPS юнит-игрока линии путей всем юнитам не рисуем — дорого
            bool unitPlaying = RTSPlayer.localPlayer != null &&
                               RTSPlayer.localPlayer.Role == PlayerRole.UnitPlaying;
            if (unitPlaying)
            {
                if (pathLine != null)
                    pathLine.enabled = false;
            }
            else if (isSelected && IsOwned && syncedDestination != Vector3.zero)
            {
                float distXZ = HorizontalDistance(transform.position, syncedDestination);
                if (distXZ > 0.75f)
                {
                    if (navAgent != null && navAgent.enabled && navAgent.hasPath)
                        DrawPath();
                    else
                        UpdatePathVisualization(syncedDestination);
                }
                else if (pathLine != null)
                {
                    pathLine.enabled = false;
                }
            }
            else if (pathLine != null)
            {
                pathLine.enabled = false;
            }

            // Анимируем инструмент если он активен (на ВСЕХ клиентах, не только сервере)
            if (isAnimatingTool)
            {
                AnimateHarvestTool();
            }

            // Обновляем состояние добычи (только на сервере). Юнит-игрок — без AI harvest.
            if (isServer && !IsPlayerControlled)
            {
                if (!IsInCombat)
                    UpdateHarvestingState();
            }
        }

        /// <summary>
        /// Обновляет состояние добычи ресурсов
        /// </summary>
        [Server]
        private void UpdateHarvestingState()
        {
            switch (currentState)
            {
                case WorkerState.Idle:
                    // Ничего не делаем
                    break;

                case WorkerState.Moving:
                    // Проверяем достигли ли цели
                    if (targetResource == null || targetResource.IsHarvested)
                    {
                        ReleaseCurrentResourceClaim();
                        targetResource = null;
                        isSearchingForResource = false;
                        currentState = WorkerState.SearchingResource;
                        break;
                    }

                    // Ресурс заняли другим юнитом — ищем другой
                    if (targetResource.IsClaimed && !targetResource.IsClaimedBy(netIdentity))
                    {
                        UnityEngine.Debug.Log($"[Worker] Target claimed by another unit, searching free resource");
                        targetResource = null;
                        isSearchingForResource = false;
                        currentState = WorkerState.SearchingResource;
                        break;
                    }

                    {
                        // Вычисляем центр ресурса с учётом коллайдера
                        Vector3 resourceCenter = targetResource.GetCenterPosition();
                        
                        float distance = Vector3.Distance(transform.position, resourceCenter);
                        
                        // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Проверяем расстояние СРАЗУ, до pathPending
                        // Если Worker УЖЕ стоит близко (например после предыдущей добычи), начинаем харвест немедленно
                        if (distance <= navAgent.stoppingDistance + harvestDistanceTolerance)
                        {
                            UnityEngine.Debug.Log($"Worker already close to resource! distance={distance:F2}, starting harvest immediately");
                            StartHarvesting();
                            break; // Выходим из switch, чтобы не делать лишних проверок
                        }
                        
                        // ДИАГНОСТИКА: проверяем состояние NavAgent
                        if (Time.frameCount % 60 == 0)
                        {
                            UnityEngine.Debug.Log($"[Worker] State=Moving, pathPending={navAgent.pathPending}, hasPath={navAgent.hasPath}, isStopped={navAgent.isStopped}, velocity={navAgent.velocity.magnitude:F2}");
                        }
                        
                        if (!navAgent.pathPending)
                        {
                            // Debug каждые 60 кадров (раз в секунду)
                            if (Time.frameCount % 60 == 0)
                            {
                                UnityEngine.Debug.Log($"Moving to resource: distance={distance:F2}, stoppingDistance={navAgent.stoppingDistance}, threshold={navAgent.stoppingDistance + harvestDistanceTolerance}");
                            }
                            
                            else if (!navAgent.hasPath || navAgent.velocity.magnitude < 0.1f)
                            {
                                // ФИКС: Если потеряли путь или застряли, перезапускаем навигацию
                                if (Time.frameCount % 120 == 0) // Проверяем раз в 2 секунды
                                {
                                    UnityEngine.Debug.LogWarning($"[Worker] Stuck detected! hasPath={navAgent.hasPath}, velocity={navAgent.velocity.magnitude:F2}, distance={distance:F2}");
                                    TrySetAgentDestination(resourceCenter);
                                }
                            }
                        }
                    }
                    break;

                case WorkerState.Harvesting:
                    // Убедимся что инструмент существует
                    if (activeToolType == -1 && targetResource != null)
                    {
                        // Инструмент исчез -- создаём заново
                        SpawnHarvestTool();
                    }
                    
                    // Проверяем завершилась ли добыча
                    if (Time.time >= harvestStartTime + CurrentHarvestDuration)
                    {
                        CompleteHarvesting();
                    }
                    break;

                case WorkerState.SearchingResource:
                    // Ищем следующий ресурс в радиусе (только один раз)
                    if (!isSearchingForResource)
                    {
                        UnityEngine.Debug.Log($"[Worker] Entering SearchingResource state, calling FindNextResourceInRadius()");
                        isSearchingForResource = true;
                        FindNextResourceInRadius();
                    }
                    break;

                case WorkerState.Depositing:
                    UpdateDepositingState();
                    break;

                case WorkerState.MovingToBuild:
                    UpdateMovingToBuildState();
                    break;

                case WorkerState.Building:
                    UpdateBuildingState();
                    break;

                case WorkerState.Farming:
                    UpdateFarmingState();
                    break;
            }
        }

        /// <summary>
        /// Устанавливает владельца юнита
        /// </summary>
        [Server]
        public override void SetOwner(NetworkConnectionToClient conn)
        {
            base.SetOwner(conn);
            netIdentity = unitNetIdentity;
        }

        [Server]
        public void ServerRestoreInventory(int wood, int ore, int food)
        {
            inventoryWood = Mathf.Max(0, wood);
            inventoryOre = Mathf.Max(0, ore);
            inventoryFood = Mathf.Max(0, food);
        }

        [Server]
        public override void ServerWriteSaveData(RTSokak.Shared.UnitSaveData entry)
        {
            base.ServerWriteSaveData(entry);
            if (entry == null) return;

            entry.inventoryWood = inventoryWood;
            entry.inventoryOre = inventoryOre;
            entry.inventoryFood = inventoryFood;
            entry.workerState = (int)currentState;
            entry.profession = (int)profession;
            entry.harvestResourceType = (int)harvestingResourceType;
            entry.lastHarvestPoint = RTSokak.Shared.SaveRounding.R2(lastHarvestPoint);
            entry.resumeHarvestAfterDeposit = resumeHarvestAfterDeposit;
            entry.resumeHarvestType = (int)resumeHarvestType;
            entry.resumeFarmingAfterDeposit = resumeFarmingAfterDeposit;
            entry.useLastHarvestPointForSearch = useLastHarvestPointForSearch;
            entry.farmingPlotIndex = farmingPlotIndex;
            entry.farmingHarvestIntent = farmingHarvestIntent;
            entry.farmingNavDestination = RTSokak.Shared.SaveRounding.R2(farmingNavDestination);

            if (targetResource != null && !targetResource.IsHarvested)
            {
                entry.harvestGridX = targetResource.gridPosition.x;
                entry.harvestGridZ = targetResource.gridPosition.y;
                entry.harvestResourceType = (int)targetResource.Type;
            }
            else
            {
                entry.harvestGridX = int.MinValue;
                entry.harvestGridZ = int.MinValue;
            }

            if (targetBuilding != null)
            {
                entry.targetBuildingPos = RTSokak.Shared.SaveRounding.R2(targetBuilding.FinalPosition);
                entry.targetBuildingType = (int)targetBuilding.Type;
            }
            else
            {
                entry.targetBuildingPos = Vector3.zero;
                entry.targetBuildingType = -1;
            }

            if (targetGarden != null)
            {
                entry.targetGardenPos = RTSokak.Shared.SaveRounding.R2(targetGarden.transform.position);
            }
            else
            {
                entry.targetGardenPos = Vector3.zero;
            }
        }

        [Server]
        public override void ServerApplySaveData(RTSokak.Shared.UnitSaveData save)
        {
            if (save == null) return;

            ServerRestoreInventory(save.inventoryWood, save.inventoryOre, save.inventoryFood);
            profession = (WorkerProfession)save.profession;
            harvestingResourceType = (RTSokak.Resources.ResourceType)save.harvestResourceType;
            lastHarvestPoint = save.lastHarvestPoint;
            resumeHarvestAfterDeposit = save.resumeHarvestAfterDeposit;
            resumeHarvestType = (RTSokak.Resources.ResourceType)save.resumeHarvestType;
            resumeFarmingAfterDeposit = save.resumeFarmingAfterDeposit;
            useLastHarvestPointForSearch = save.useLastHarvestPointForSearch;

            var state = (WorkerState)save.workerState;
            EnsureNetIdentity();

            // Resolve world targets first.
            RTSokak.Resources.Resource resource = null;
            if (save.harvestGridX != int.MinValue)
                resource = FindResourceAt(save.harvestGridX, save.harvestGridZ);

            RTSokak.Buildings.Building building = null;
            if (save.targetBuildingType >= 0 && save.targetBuildingPos.sqrMagnitude > 0.01f)
                building = FindBuildingNear(save.targetBuildingPos, (RTSokak.Buildings.BuildingType)save.targetBuildingType);

            RTSokak.Buildings.GardenBed garden = null;
            if (save.targetGardenPos.sqrMagnitude > 0.01f)
                garden = FindGardenNear(save.targetGardenPos);

            switch (state)
            {
                case WorkerState.Moving:
                case WorkerState.Harvesting:
                    if (resource != null && !IsInventoryFull)
                    {
                        LeaveConstruction();
                        LeaveFarming();
                        ServerStopCombat();
                        ReleaseCurrentResourceClaim();
                        targetResource = resource;
                        harvestingResourceType = resource.Type;
                        Vector3 center = resource.GetCenterPosition();
                        if (state == WorkerState.Harvesting && resource.TryClaim(netIdentity))
                        {
                            currentState = WorkerState.Harvesting;
                            syncedDestination = Vector3.zero;
                            harvestStartTime = Time.time;
                            SpawnHarvestTool();
                            RpcStartHarvesting(resource.gameObject);
                        }
                        else
                        {
                            currentState = WorkerState.Moving;
                            syncedDestination = center;
                            TrySetAgentDestination(center);
                        }
                    }
                    else if (save.hasDestination)
                    {
                        base.ServerApplySaveData(save);
                        currentState = WorkerState.Idle;
                    }
                    break;

                case WorkerState.SearchingResource:
                    LeaveConstruction();
                    LeaveFarming();
                    ServerStopCombat();
                    harvestingResourceType = (RTSokak.Resources.ResourceType)save.harvestResourceType;
                    currentState = WorkerState.SearchingResource;
                    isSearchingForResource = false;
                    FindNextResourceInRadius();
                    break;

                case WorkerState.Depositing:
                    if (InventoryTotal > 0)
                    {
                        LeaveConstruction();
                        LeaveFarming();
                        ServerStopCombat();
                        if (building != null &&
                            (building.Type == RTSokak.Buildings.BuildingType.Capital ||
                             building.Type == RTSokak.Buildings.BuildingType.Warehouse))
                        {
                            cachedApproachPoint = building.GetApproachPosition(transform.position);
                            hasCachedApproach = true;
                            targetBuilding = building;
                            syncedDestination = cachedApproachPoint;
                            currentState = WorkerState.Depositing;
                            TrySetAgentDestination(cachedApproachPoint);
                        }
                        else
                        {
                            BeginDepositToCapital();
                        }
                    }
                    break;

                case WorkerState.MovingToBuild:
                case WorkerState.Building:
                    if (building != null && !building.IsComplete)
                    {
                        ReleaseCurrentResourceClaim();
                        LeaveFarming();
                        ServerStopCombat();
                        AssignToBuilding(building);
                        if (state == WorkerState.Building)
                        {
                            float dist = HorizontalDistance(transform.position, cachedApproachPoint);
                            if (dist <= 2.5f)
                                StartBuilding();
                        }
                    }
                    break;

                case WorkerState.Farming:
                    if (garden != null)
                    {
                        ReleaseCurrentResourceClaim();
                        LeaveConstruction();
                        ServerStopCombat();
                        BeginFarming(garden);
                        if (save.farmingPlotIndex >= 0)
                        {
                            farmingPlotIndex = save.farmingPlotIndex;
                            farmingHarvestIntent = save.farmingHarvestIntent;
                            farmingNavDestination = save.farmingNavDestination;
                            if (farmingNavDestination.sqrMagnitude > 0.01f)
                            {
                                syncedDestination = farmingNavDestination;
                                TrySetAgentDestination(farmingNavDestination, 0.5f);
                            }
                        }
                    }
                    break;

                default:
                    currentState = WorkerState.Idle;
                    base.ServerApplySaveData(save);
                    break;
            }

            // Бой поверх idle / move без добычи.
            if (state == WorkerState.Idle || (state == WorkerState.Moving && resource == null))
            {
                if (save.combatState != 0)
                    base.ServerApplySaveData(save);
            }
        }

        static RTSokak.Resources.Resource FindResourceAt(int gridX, int gridZ)
        {
            var rm = Object.FindFirstObjectByType<RTSokak.Resources.ResourceManager>();
            if (rm != null)
            {
                var at = rm.GetResourceAt(new Vector2Int(gridX, gridZ));
                if (at != null && !at.IsHarvested)
                    return at;
            }

            foreach (var r in Object.FindObjectsByType<RTSokak.Resources.Resource>(FindObjectsSortMode.None))
            {
                if (r != null && !r.IsHarvested &&
                    r.gridPosition.x == gridX && r.gridPosition.y == gridZ)
                    return r;
            }
            return null;
        }

        static RTSokak.Buildings.Building FindBuildingNear(Vector3 pos, RTSokak.Buildings.BuildingType type)
        {
            RTSokak.Buildings.Building best = null;
            float bestDist = 3f;
            foreach (var b in Object.FindObjectsByType<RTSokak.Buildings.Building>(FindObjectsSortMode.None))
            {
                if (b == null || b.Type != type) continue;
                float d = Vector3.Distance(b.FinalPosition, pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }
            return best;
        }

        static RTSokak.Buildings.GardenBed FindGardenNear(Vector3 pos)
        {
            RTSokak.Buildings.GardenBed best = null;
            float bestDist = 3f;
            foreach (var g in Object.FindObjectsByType<RTSokak.Buildings.GardenBed>(FindObjectsSortMode.None))
            {
                if (g == null) continue;
                float d = Vector3.Distance(g.transform.position, pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = g;
                }
            }
            return best;
        }

        [Server]
        protected override void OnServerCancelTasksForMove()
        {
            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            LeaveFarming();
            resumeHarvestAfterDeposit = false;
            hasCachedApproach = false;
            currentState = WorkerState.Idle;
            targetResource = null;
            isSearchingForResource = false;
            DestroyHarvestTool();
        }

        [Server]
        protected override bool CanRetaliateWhileIdle()
        {
            return currentState == WorkerState.Idle;
        }

        /// <summary>
        /// Отдаёт команду юниту добывать ресурс
        /// </summary>
        public void HarvestResource(RTSokak.Resources.Resource resource)
        {
            UnityEngine.Debug.Log($"HarvestResource called: isOwned={isOwned}, connId={ownerConnectionId}, resource={resource?.Type}");
            
            if (!isOwned || resource == null) return;

            UnityEngine.Debug.Log($"HarvestResource executing for {resource.Type}");

            // Отправляем команду на сервер
            CmdHarvestResource(resource.gameObject);
        }

        /// <summary>
        /// Серверная команда для добычи ресурса.
        /// Если ресурс уже занят — ищем ближайший свободный того же типа у точки клика.
        /// </summary>
        [Command(requiresAuthority = false)]
        private void CmdHarvestResource(GameObject resourceObj)
        {
            if (resourceObj == null) return;

            RTSokak.Resources.Resource clicked = resourceObj.GetComponent<RTSokak.Resources.Resource>();
            if (clicked == null || clicked.IsHarvested)
            {
                UnityEngine.Debug.LogWarning("Invalid resource or already harvested");
                return;
            }

            EnsureNetIdentity();
            LeaveConstruction();
            LeaveFarming();
            ServerStopCombat();

            // Полный инвентарь → сначала сдать в столицу, потом добывать
            if (IsInventoryFull)
            {
                lastHarvestPoint = clicked.GetCenterPosition();
                resumeHarvestAfterDeposit = true;
                resumeHarvestType = clicked.Type;
                useLastHarvestPointForSearch = true;
                harvestingResourceType = clicked.Type;
                BeginDepositToCapital();
                return;
            }

            Vector3 clickPoint = clicked.GetCenterPosition();
            RTSokak.Resources.ResourceType type = clicked.Type;

            ReleaseCurrentResourceClaim();

            RTSokak.Resources.Resource resource = clicked;
            if (!resource.TryClaim(netIdentity))
            {
                resource = FindNearestFreeResource(clickPoint, type, redirectSearchRadius);
                if (resource == null || !resource.TryClaim(netIdentity))
                {
                    UnityEngine.Debug.Log($"[Worker] No free {type} near click {clickPoint}");
                    currentState = WorkerState.Idle;
                    targetResource = null;
                    return;
                }

                UnityEngine.Debug.Log($"[Worker] Clicked resource busy → redirected to {resource.name}");
            }

            BeginMoveToClaimedResource(resource);
        }

        [Server]
        private void EnsureNetIdentity()
        {
            if (netIdentity == null)
                netIdentity = GetComponent<NetworkIdentity>();
        }

        [Server]
        private void ReleaseCurrentResourceClaim()
        {
            EnsureNetIdentity();
            if (targetResource != null)
            {
                targetResource.ReleaseClaim(netIdentity);
            }
        }

        /// <summary>
        /// Назначить уже claimed ресурс и начать движение к нему.
        /// </summary>
        [Server]
        private void BeginMoveToClaimedResource(RTSokak.Resources.Resource resource)
        {
            targetResource = resource;
            harvestingResourceType = resource.Type;

            Vector3 resourceCenter = resource.GetCenterPosition();
            currentState = WorkerState.Moving;
            syncedDestination = resourceCenter;
            isSearchingForResource = false;

            TrySetAgentDestination(resourceCenter);
            UnityEngine.Debug.Log($"Moving to resource at {resourceCenter}");
            RpcStartHarvestMovement(resource.gameObject, resourceCenter);
        }

        [Server]
        private RTSokak.Resources.Resource FindNearestFreeResource(
            Vector3 nearPoint,
            RTSokak.Resources.ResourceType type,
            float radius)
        {
            if (cachedResourceManager == null)
                cachedResourceManager = FindObjectOfType<RTSokak.Resources.ResourceManager>();

            if (cachedResourceManager == null)
                return null;

            EnsureNetIdentity();
            return cachedResourceManager.GetNearestAvailableResource(nearPoint, type, radius, netIdentity);
        }
        
        /// <summary>
        /// ClientRpc для синхронизации начала движения к ресурсу на клиентах
        /// </summary>
        [ClientRpc]
        private void RpcStartHarvestMovement(GameObject resourceObj, Vector3 resourceCenter)
        {
            if (isServer)
                return;

            // Только локальный state + линия; позицию двигает сервер через NetworkTransform
            RTSokak.Resources.Resource resource = resourceObj != null
                ? resourceObj.GetComponent<RTSokak.Resources.Resource>()
                : null;
            if (resource != null)
            {
                targetResource = resource;
                harvestingResourceType = resource.Type;
            }

            currentState = WorkerState.Moving;
            syncedDestination = resourceCenter;
        }

        /// <summary>
        /// Начинает процесс добычи
        /// </summary>
        [Server]
        private void StartHarvesting()
        {
            EnsureNetIdentity();

            if (targetResource == null || targetResource.IsHarvested)
            {
                ReleaseCurrentResourceClaim();
                targetResource = null;
                isSearchingForResource = false;
                currentState = WorkerState.SearchingResource;
                return;
            }

            // Подтверждаем/восстанавливаем claim
            if (!targetResource.TryClaim(netIdentity))
            {
                UnityEngine.Debug.Log($"[Worker] Cannot start harvest — resource claimed by another");
                targetResource = null;
                isSearchingForResource = false;
                currentState = WorkerState.SearchingResource;
                return;
            }

            currentState = WorkerState.Harvesting;
            harvestStartTime = Time.time;
            navAgent.isStopped = true;

            // НЕ поворачиваем мгновенно - NavMeshAgent будет поворачиваться сам используя angularSpeed
            // Просто сохраняем направление для постепенного поворота в Update
            Vector3 directionToResource = targetResource.transform.position - transform.position;
            directionToResource.y = 0; // Игнорируем разницу по высоте
            
            // Запоминаем целевой поворот для плавного вращения
            if (directionToResource.sqrMagnitude > 0.001f)
            {
                harvestTargetRotation = Quaternion.LookRotation(directionToResource);
                isRotatingToHarvest = true;
            }

            // Спавним инструмент для анимации (но не начинаем анимацию пока не повернулись)
            SpawnHarvestTool();

            UnityEngine.Debug.Log($"Started harvesting {targetResource.Type}");
            
            // Синхронизируем состояние добычи на клиентах
            if (isServer)
            {
                RpcStartHarvesting(targetResource.gameObject);
            }
        }
        
        /// <summary>
        /// ClientRpc для синхронизации начала добычи на клиентах
        /// </summary>
        [ClientRpc]
        private void RpcStartHarvesting(GameObject resourceObj)
        {
            if (!isServer)
            {
                RTSokak.Resources.Resource resource = resourceObj.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null && !resource.IsHarvested)
                {
                    targetResource = resource;
                    harvestingResourceType = resource.Type; // ВАЖНО: устанавливаем тип для правильной работы инструмента
                    currentState = WorkerState.Harvesting;
                    harvestStartTime = Time.time;
                    
                    if (navAgent != null)
                    {
                        navAgent.isStopped = true;
                    }
                    
                    // Устанавливаем поворот к ресурсу
                    Vector3 directionToResource = resource.transform.position - transform.position;
                    directionToResource.y = 0;
                    
                    if (directionToResource.sqrMagnitude > 0.001f)
                    {
                        harvestTargetRotation = Quaternion.LookRotation(directionToResource);
                        isRotatingToHarvest = true;
                    }
                    
                    UnityEngine.Debug.Log($"Client: Started harvesting {resource.Type}, harvestingResourceType={harvestingResourceType}");
                }
            }
        }
        
        /// <summary>
        /// ДИАГНОСТИКА: Выводит полное состояние Worker для отладки
        /// Вызывается автоматически каждые 5 секунд если Worker не Idle
        /// </summary>
        [Server]
        private void DiagnoseWorkerState()
        {
            if (currentState == WorkerState.Idle) return;
            
            string diagnosis = $"[WORKER DIAGNOSIS]\n" +
                $"State: {currentState}\n" +
                $"Position: {transform.position}\n" +
                $"StoppingDistance: {stoppingDistance} (NavAgent: {navAgent.stoppingDistance})\n" +
                $"HarvestTolerance: {harvestDistanceTolerance}\n" +
                $"Threshold: {navAgent.stoppingDistance + harvestDistanceTolerance}\n" +
                $"TargetResource: {(targetResource != null ? targetResource.Type.ToString() : "NULL")}\n" +
                $"NavAgent.hasPath: {navAgent.hasPath}\n" +
                $"NavAgent.velocity: {navAgent.velocity.magnitude:F2}\n" +
                $"CachedRM: {(cachedResourceManager != null ? "OK" : "NULL")}";
            
            if (targetResource != null)
            {
                Vector3 resCenter = targetResource.transform.position;
                BoxCollider col = targetResource.GetComponent<BoxCollider>();
                if (col != null)
                {
                    resCenter = targetResource.transform.TransformPoint(col.center);
                }
                float dist = Vector3.Distance(transform.position, resCenter);
                diagnosis += $"\nDistance to resource: {dist:F2}";
            }
            
            UnityEngine.Debug.Log(diagnosis);
        }

        [Server]
        private void CompleteHarvesting()
        {
            if (targetResource == null)
            {
                currentState = WorkerState.Idle;
                return;
            }

            lastHarvestPoint = targetResource.GetCenterPosition();
            
            // Ударяем по ресурсу (получаем ресурсы за этот удар)
            int amount = targetResource.Harvest();
            
            // Проверяем уничтожен ли ресурс после удара
            bool resourceDestroyed = targetResource == null || targetResource.IsHarvested;
            
            if (resourceDestroyed)
            {
                // Ресурс полностью добыт
                targetResource = null;
                UnityEngine.Debug.Log($"Resource fully harvested, got {amount} {harvestingResourceType}");
            }
            else
            {
                // Ресурс ещё жив -- продолжаем добывать
                UnityEngine.Debug.Log($"Hit resource, got {amount} {harvestingResourceType}. HP left: {targetResource.CurrentHealth}");
            }

            AddToInventory(harvestingResourceType, amount);
            
            // Показываем floating text с количеством добытого ресурса
            if (amount > 0)
            {
                RpcShowHarvestFloatingText(lastHarvestPoint, harvestingResourceType, amount);
            }
            
            // Инвентарь переполнен?
            if (IsInventoryFull)
            {
                // Освобождаем claim если ресурс всё ещё жив
                if (!resourceDestroyed && targetResource != null)
                {
                    targetResource.ReleaseClaim(netIdentity);
                }
                
                targetResource = null;
                DestroyHarvestTool();
                navAgent.isStopped = false;
                
                resumeHarvestAfterDeposit = true;
                resumeHarvestType = harvestingResourceType;
                useLastHarvestPointForSearch = true;
                BeginDepositToCapital();
                return;
            }

            // Если ресурс уничтожен -- ищем новый
            if (resourceDestroyed)
            {
                DestroyHarvestTool();
                navAgent.isStopped = false;
                isSearchingForResource = false;
                currentState = WorkerState.SearchingResource;
            }
            else
            {
                // Ресурс жив -- продолжаем добывать (возвращаемся в Harvesting)
                currentState = WorkerState.Harvesting;
                harvestStartTime = Time.time; // Начинаем новый цикл добычи
                isAnimatingTool = false; // Сбрасываем флаг чтобы анимация заработала снова
                isRotatingToHarvest = false; // Сбрасываем поворот
                toolAnimationStartTime = 0f; // Сбрасываем таймер анимации
            }
        }

        [Server]
        private void AddToInventory(RTSokak.Resources.ResourceType type, int amount)
        {
            if (amount <= 0) return;

            int free = Mathf.Max(0, maxInventory - InventoryTotal);
            int add = Mathf.Min(free, amount);
            if (add <= 0) return;

            switch (type)
            {
                case RTSokak.Resources.ResourceType.Wood:
                    inventoryWood += add;
                    break;
                case RTSokak.Resources.ResourceType.Ore:
                    inventoryOre += add;
                    break;
                case RTSokak.Resources.ResourceType.Food:
                    inventoryFood += add;
                    break;
            }
        }

        [Server]
        private RTSPlayer GetOwnerPlayer()
        {
            if (connectionToClient != null && connectionToClient.identity != null)
                return connectionToClient.identity.GetComponent<RTSPlayer>();

            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p.ConnectionId == ownerConnectionId)
                    return p;
            }

            return null;
        }

        [Server]
        private void BeginDepositToCapital()
        {
            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            // Не снимаем assignment с грядки при сдаче еды — вернёмся через resumeFarmingAfterDeposit

            RTSPlayer player = GetOwnerPlayer();
            RTSokak.Buildings.Building deposit = player != null
                ? player.FindNearestDepositBuilding(transform.position)
                : null;
            if (deposit == null)
            {
                UnityEngine.Debug.LogWarning("[Worker] No capital/warehouse to deposit — idle with inventory");
                currentState = WorkerState.Idle;
                syncedDestination = Vector3.zero;
                return;
            }

            // Фиксируем точку подхода один раз — иначе каждый кадр она «ездит» вокруг здания
            cachedApproachPoint = deposit.GetApproachPosition(transform.position);
            hasCachedApproach = true;
            syncedDestination = cachedApproachPoint;
            currentState = WorkerState.Depositing;
            targetBuilding = deposit;
            TrySetAgentDestination(cachedApproachPoint);
        }

        [Server]
        private void UpdateDepositingState()
        {
            RTSPlayer player = GetOwnerPlayer();
            RTSokak.Buildings.Building deposit = targetBuilding != null
                ? targetBuilding
                : (player != null ? player.FindNearestDepositBuilding(transform.position) : null);

            if (deposit == null ||
                (deposit.Type != RTSokak.Buildings.BuildingType.Capital &&
                 deposit.Type != RTSokak.Buildings.BuildingType.Warehouse) ||
                !deposit.IsComplete)
            {
                // Цель пропала — ищем другую точку сдачи
                deposit = player != null ? player.FindNearestDepositBuilding(transform.position) : null;
                if (deposit == null)
                {
                    currentState = WorkerState.Idle;
                    hasCachedApproach = false;
                    syncedDestination = Vector3.zero;
                    return;
                }

                targetBuilding = deposit;
                hasCachedApproach = false;
            }

            targetBuilding = deposit;
            if (!hasCachedApproach)
            {
                cachedApproachPoint = deposit.GetApproachPosition(transform.position);
                hasCachedApproach = true;
                syncedDestination = cachedApproachPoint;
                TrySetAgentDestination(cachedApproachPoint);
            }

            float dist = HorizontalDistance(transform.position, cachedApproachPoint);
            if (dist > Mathf.Max(1.25f, stoppingDistance))
            {
                // Не дёргаем SetDestination каждый кадр — только если реально застряли
                if (navAgent != null && !navAgent.pathPending && navAgent.velocity.magnitude < 0.05f &&
                    (!navAgent.hasPath || navAgent.remainingDistance < 0.15f))
                {
                    TrySetAgentDestination(cachedApproachPoint);
                }

                return;
            }

            // Сдаём ресурсы
            if (player != null && InventoryTotal > 0)
            {
                int depositedWood = inventoryWood;
                int depositedOre = inventoryOre;
                int depositedFood = inventoryFood;
                
                if (inventoryWood > 0) { player.AddWood(inventoryWood); inventoryWood = 0; }
                if (inventoryOre > 0) { player.AddOre(inventoryOre); inventoryOre = 0; }
                if (inventoryFood > 0) { player.AddFood(inventoryFood); inventoryFood = 0; }
                
                // Показываем floating text возле юнита
                RpcShowDepositFloatingText(transform.position, depositedWood, depositedOre, depositedFood);
            }

            targetBuilding = null;
            hasCachedApproach = false;
            syncedDestination = Vector3.zero;
            if (navAgent != null)
                navAgent.isStopped = false;

            if (resumeHarvestAfterDeposit)
            {
                resumeHarvestAfterDeposit = false;
                harvestingResourceType = resumeHarvestType;
                isSearchingForResource = false;
                useLastHarvestPointForSearch = true;
                currentState = WorkerState.SearchingResource;
            }
            else if (resumeFarmingAfterDeposit)
            {
                resumeFarmingAfterDeposit = false;
                if (targetGarden != null && targetGarden.IsAssignedTo(netIdentity))
                {
                    farmingPlotIndex = -1;
                    currentState = WorkerState.Farming;
                }
                else
                {
                    currentState = WorkerState.Idle;
                }
            }
            else
            {
                currentState = WorkerState.Idle;
            }
        }

        [Server]
        private void UpdateMovingToBuildState()
        {
            if (targetBuilding == null || targetBuilding.IsComplete)
            {
                RTSokak.Buildings.Building finished = targetBuilding;
                LeaveConstruction();
                hasCachedApproach = false;
                syncedDestination = Vector3.zero;

                var bed = finished != null ? finished.GetComponent<RTSokak.Buildings.GardenBed>() : null;
                if (bed != null && bed.TryAssignFarmer(netIdentity))
                    BeginFarming(bed);
                else
                    currentState = WorkerState.Idle;
                return;
            }

            if (!hasCachedApproach)
            {
                cachedApproachPoint = targetBuilding.GetApproachPosition(transform.position);
                hasCachedApproach = true;
                syncedDestination = cachedApproachPoint;
                TrySetAgentDestination(cachedApproachPoint);
            }

            float dist = HorizontalDistance(transform.position, cachedApproachPoint);
            if (dist <= Mathf.Max(1.25f, stoppingDistance))
            {
                StartBuilding();
                return;
            }

            if (navAgent != null && !navAgent.pathPending && navAgent.velocity.magnitude < 0.05f &&
                (!navAgent.hasPath || navAgent.remainingDistance < 0.15f))
            {
                TrySetAgentDestination(cachedApproachPoint);
            }
        }

        [Server]
        private void StartBuilding()
        {
            if (targetBuilding == null)
            {
                currentState = WorkerState.Idle;
                return;
            }

            EnsureNetIdentity();
            targetBuilding.TryAddBuilder(netIdentity);
            currentState = WorkerState.Building;
            hasCachedApproach = false;
            syncedDestination = Vector3.zero;
            if (navAgent != null)
                navAgent.isStopped = true;

            nextBuildSwingTime = Time.time;
            activeToolType = 2; // hammer — SyncVar → клиенты
            toolAnimationStartTime = Time.time;
            isAnimatingTool = true;
            harvestStartTime = Time.time;
            RpcStartBuildingAnimation();
        }

        [Server]
        private void UpdateBuildingState()
        {
            if (targetBuilding == null || targetBuilding.IsComplete)
            {
                // Основной переход на ферму — через ServerOnBuildingCompleted.
                // Здесь только fallback, если уведомление не пришло.
                RTSokak.Buildings.Building finished = targetBuilding;
                DestroyHarvestTool();
                LeaveConstruction();

                var bed = finished != null ? finished.GetComponent<RTSokak.Buildings.GardenBed>() : null;
                if (bed != null && (targetGarden == bed || bed.TryAssignFarmer(netIdentity)))
                {
                    if (currentState != WorkerState.Farming)
                        BeginFarming(bed);
                }
                else if (currentState != WorkerState.Farming)
                {
                    currentState = WorkerState.Idle;
                    if (navAgent != null)
                        navAgent.isStopped = false;
                }
                return;
            }

            // Стучим каждую секунду
            if (Time.time >= nextBuildSwingTime)
            {
                nextBuildSwingTime = Time.time + 1f;
                toolAnimationStartTime = Time.time;
                isAnimatingTool = true;
                harvestStartTime = Time.time;
                RpcBuildSwing();
            }
        }

        /// <summary>
        /// Вызывается зданием при завершении стройки — фермер сразу продолжает работу сам.
        /// </summary>
        [Server]
        public void ServerOnBuildingCompleted(RTSokak.Buildings.Building building)
        {
            if (building == null)
                return;

            // Только если этот юнит реально строил это здание (или ещё идёт к нему)
            if (targetBuilding != null && targetBuilding != building)
                return;
            if (currentState != WorkerState.Building && currentState != WorkerState.MovingToBuild)
                return;

            DestroyHarvestTool();
            LeaveConstruction();

            var bed = building.GetComponent<RTSokak.Buildings.GardenBed>();
            if (bed != null && bed.TryAssignFarmer(netIdentity))
            {
                BeginFarming(bed);
                return;
            }

            currentState = WorkerState.Idle;
            if (navAgent != null)
                navAgent.isStopped = false;
        }

        [ClientRpc]
        private void RpcStartBuildingAnimation()
        {
            isAnimatingTool = true;
            toolAnimationStartTime = Time.time;
            harvestStartTime = Time.time;
            if (activeToolType != 2 && currentToolInstance == null)
            {
                // SyncVar может прийти чуть позже — CreateToolInstance вызовет hook
            }
        }

        [ClientRpc]
        private void RpcBuildSwing()
        {
            isAnimatingTool = true;
            toolAnimationStartTime = Time.time;
            harvestStartTime = Time.time;
        }

        /// <summary>
        /// ClientRpc для отображения floating text при добыче ресурса
        /// </summary>
        [ClientRpc]
        private void RpcShowHarvestFloatingText(Vector3 worldPos, RTSokak.Resources.ResourceType resourceType, int amount)
        {
            // Показываем floating text только владельцу юнита
            if (!IsOwned)
                return;
                
            if (RTSokak.UI.FloatingTextManager.Instance != null)
            {
                RTSokak.UI.FloatingTextManager.Instance.ShowResourceText(worldPos, resourceType, amount);
            }
        }

        /// <summary>
        /// ClientRpc для отображения floating text при сдаче ресурсов
        /// </summary>
        [ClientRpc]
        private void RpcShowDepositFloatingText(Vector3 worldPos, int woodAmount, int oreAmount, int foodAmount)
        {
            // Показываем floating text только владельцу юнита
            if (!IsOwned)
                return;
                
            if (RTSokak.UI.FloatingTextManager.Instance != null)
            {
                RTSokak.UI.FloatingTextManager.Instance.ShowResourceDeposited(worldPos, woodAmount, oreAmount, foodAmount);
            }
        }

        [Server]
        private void LeaveConstruction()
        {
            EnsureNetIdentity();
            if (targetBuilding != null)
            {
                targetBuilding.RemoveBuilder(netIdentity);
                targetBuilding = null;
            }

            hasCachedApproach = false;

            if (currentState == WorkerState.Building || activeToolType == 2)
                DestroyHarvestTool();
        }

        [Server]
        private void BeginFarming(RTSokak.Buildings.GardenBed bed)
        {
            if (bed == null)
            {
                currentState = WorkerState.Idle;
                return;
            }

            // На случай если assignment ещё не стоял
            bed.TryAssignFarmer(netIdentity);

            targetGarden = bed;
            farmingPlotIndex = -1;
            farmingHarvestIntent = false;
            resumeFarmingAfterDeposit = false;
            currentState = WorkerState.Farming;

            // Ближе подходим к каждой точке
            if (navAgent != null)
            {
                if (farmingSavedStoppingDistance < 0f)
                    farmingSavedStoppingDistance = navAgent.stoppingDistance;
                navAgent.stoppingDistance = 0.15f;
                navAgent.isStopped = false;
            }

            UnityEngine.Debug.Log($"[Worker] BeginFarming on {bed.name}, plots={bed.PlotCount}");
        }

        [Server]
        private void RestoreFarmingAgentSettings()
        {
            if (navAgent != null && farmingSavedStoppingDistance >= 0f)
            {
                navAgent.stoppingDistance = farmingSavedStoppingDistance;
                farmingSavedStoppingDistance = -1f;
            }
        }

        [Server]
        private void LeaveFarming()
        {
            EnsureNetIdentity();
            if (targetGarden != null)
            {
                targetGarden.ReleaseFarmer(netIdentity);
                targetGarden = null;
            }

            farmingPlotIndex = -1;
            resumeFarmingAfterDeposit = false;
            RestoreFarmingAgentSettings();
        }

        [Server]
        private void UpdateFarmingState()
        {
            EnsureNetIdentity();
            if (targetGarden == null || !targetGarden.IsAssignedTo(netIdentity))
            {
                if (targetGarden != null && targetGarden.TryAssignFarmer(netIdentity))
                {
                    // ок
                }
                else
                {
                    targetGarden = null;
                    farmingPlotIndex = -1;
                    RestoreFarmingAgentSettings();
                    currentState = WorkerState.Idle;
                    syncedDestination = Vector3.zero;
                    return;
                }
            }

            if (farmingPlotIndex < 0)
            {
                // По порядку точек (0..N), не «всё что рядом» — юнит обходит каждую
                if (!targetGarden.TryGetNextWork(out int plot, out bool harvest))
                {
                    syncedDestination = Vector3.zero;
                    if (navAgent != null && navAgent.isOnNavMesh)
                        navAgent.isStopped = true;
                    return;
                }

                StartFarmPlotTask(plot, harvest);
                return;
            }

            if (HasReachedFarmPlot(farmingPlotIndex))
            {
                ExecuteFarmPlotAction();
                return;
            }

            // Застрял — ещё раз путь к ЭТОЙ же точке; действие только если уже у её nav-точки
            if (Time.time - farmingMoveStartTime > farmStuckTimeout)
            {
                float distNav = HorizontalDistance(transform.position, farmingNavDestination);
                if (distNav <= farmInteractDistance)
                {
                    ExecuteFarmPlotAction();
                    return;
                }

                farmingMoveStartTime = Time.time;
                TrySetAgentDestination(farmingNavDestination);
                return;
            }

            if (navAgent != null && !navAgent.pathPending && navAgent.velocity.magnitude < 0.05f &&
                (!navAgent.hasPath || navAgent.remainingDistance < 0.2f))
            {
                TrySetAgentDestination(farmingNavDestination);
            }
        }

        [Server]
        private void StartFarmPlotTask(int plot, bool harvest)
        {
            farmingPlotIndex = plot;
            farmingHarvestIntent = harvest;
            farmingMoveStartTime = Time.time;

            Vector3 plotPos = targetGarden.GetPlotWorldPosition(plot);
            plotPos.y = transform.position.y;

            // Точка назначения — максимально близко к empty (маленький sample radius),
            // иначе все точки схлопываются в один край грядки
            farmingNavDestination = plotPos;
            if (UnityEngine.AI.NavMesh.SamplePosition(plotPos, out UnityEngine.AI.NavMeshHit hit, 0.85f, UnityEngine.AI.NavMesh.AllAreas))
                farmingNavDestination = hit.position;
            else if (UnityEngine.AI.NavMesh.SamplePosition(plotPos, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                farmingNavDestination = hit.position;

            if (navAgent != null)
            {
                navAgent.stoppingDistance = 0.1f;
                navAgent.isStopped = false;
                navAgent.ResetPath();
            }

            syncedDestination = farmingNavDestination;
            TrySetAgentDestination(farmingNavDestination, 0.5f);
        }

        [Server]
        private bool HasReachedFarmPlot(int plot)
        {
            if (targetGarden == null || plot < 0)
                return false;

            float distNav = HorizontalDistance(transform.position, farmingNavDestination);
            if (distNav > farmInteractDistance)
                return false;

            // Ещё не успели выйти к новой точке после предыдущей посадки
            if (Time.time - farmingMoveStartTime < 0.15f && distNav > 0.25f)
                return false;

            bool agentSettled = navAgent == null ||
                                (!navAgent.pathPending &&
                                 (navAgent.velocity.magnitude < 0.12f ||
                                  !navAgent.hasPath ||
                                  navAgent.remainingDistance <= 0.3f));

            return agentSettled;
        }

        [Server]
        private void ExecuteFarmPlotAction()
        {
            if (targetGarden == null || farmingPlotIndex < 0)
                return;

            int plot = farmingPlotIndex;
            var state = targetGarden.GetPlotState(plot);

            if (state == RTSokak.Buildings.GardenPlotState.Ready)
            {
                int need = targetGarden.FoodPerHarvest;
                int free = Mathf.Max(0, maxInventory - InventoryTotal);
                if (free < need)
                {
                    resumeFarmingAfterDeposit = true;
                    farmingPlotIndex = -1;
                    BeginDepositToCapital();
                    return;
                }

                int gained = targetGarden.ServerHarvestAndReplant(plot);
                if (gained > 0)
                    inventoryFood += gained;
            }
            else if (state == RTSokak.Buildings.GardenPlotState.Empty)
            {
                targetGarden.ServerPlant(plot);
            }
            // Growing — просто берём следующую задачу

            farmingPlotIndex = -1;
        }

        public void RequestFarmGarden(RTSokak.Buildings.GardenBed bed)
        {
            if (!IsOwned || bed == null) return;
            CmdFarmGarden(bed.gameObject);
        }

        [Command(requiresAuthority = false)]
        private void CmdFarmGarden(GameObject bedObj)
        {
            if (bedObj == null) return;
            var bed = bedObj.GetComponent<RTSokak.Buildings.GardenBed>();
            var building = bedObj.GetComponent<RTSokak.Buildings.Building>();
            if (bed == null || building == null || !building.IsComplete)
                return;
            if (!building.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                return;

            EnsureNetIdentity();
            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            LeaveFarming();
            resumeHarvestAfterDeposit = false;

            if (!bed.TryAssignFarmer(netIdentity))
            {
                UnityEngine.Debug.Log("[Worker] Garden bed already has a farmer");
                return;
            }

            BeginFarming(bed);
        }

        /// <summary>
        /// Клиент: размещение через RTSPlayer (у локального игрока всегда есть authority).
        /// </summary>
        public void RequestPlaceBuilding(RTSokak.Buildings.BuildingType type, Vector3 worldPos)
        {
            if (!IsOwned) return;

            if (RTSPlayer.localPlayer != null)
            {
                RTSPlayer.localPlayer.CmdPlaceBuilding(type, worldPos, netId);
                return;
            }

            // Fallback (host без localPlayer ещё)
            CmdPlaceBuilding(type, worldPos);
        }

        [Server]
        public void ServerStartConstruction(RTSokak.Buildings.Building building)
        {
            if (building == null) return;
            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            LeaveFarming();
            resumeHarvestAfterDeposit = false;
            AssignToBuilding(building);
        }

        public void RequestJoinBuilding(RTSokak.Buildings.Building building)
        {
            if (!IsOwned || building == null) return;
            CmdJoinBuilding(building.gameObject);
        }

        /// <summary>
        /// Отправить юнита сдать ресурсы в ближайшую столицу или склад.
        /// </summary>
        public void RequestDepositToCapital()
        {
            if (!IsOwned) return;
            CmdDepositToCapital(0);
        }

        /// <summary>Сдать ресурсы в конкретную столицу/склад (ПКМ по зданию).</summary>
        public void RequestDepositToBuilding(RTSokak.Buildings.Building building)
        {
            if (!IsOwned || building == null) return;
            if (building.Type != RTSokak.Buildings.BuildingType.Capital &&
                building.Type != RTSokak.Buildings.BuildingType.Warehouse)
                return;
            CmdDepositToCapital(building.netId);
        }

        [Command(requiresAuthority = false)]
        private void CmdDepositToCapital(uint preferredBuildingNetId)
        {
            if (InventoryTotal <= 0)
            {
                UnityEngine.Debug.Log("[Worker] Inventory empty — nothing to deposit");
                return;
            }

            RememberWorkToResumeAfterDeposit();

            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            if (!resumeFarmingAfterDeposit)
                LeaveFarming();
            targetResource = null;
            isSearchingForResource = false;
            DestroyHarvestTool();

            RTSokak.Buildings.Building preferred = null;
            if (preferredBuildingNetId != 0 &&
                NetworkServer.spawned.TryGetValue(preferredBuildingNetId, out NetworkIdentity id))
            {
                preferred = id.GetComponent<RTSokak.Buildings.Building>();
                if (preferred != null &&
                    preferred.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId) &&
                    preferred.IsComplete &&
                    (preferred.Type == RTSokak.Buildings.BuildingType.Capital ||
                     preferred.Type == RTSokak.Buildings.BuildingType.Warehouse))
                {
                    BeginDepositToBuilding(preferred);
                    return;
                }
            }

            BeginDepositToCapital();
        }

        /// <summary>
        /// При ручном Deposit запоминаем добычу/ферму, чтобы после сдачи вернуться к делу.
        /// </summary>
        [Server]
        private void RememberWorkToResumeAfterDeposit()
        {
            bool wasHarvesting =
                currentState == WorkerState.Harvesting ||
                currentState == WorkerState.Moving ||
                currentState == WorkerState.SearchingResource ||
                resumeHarvestAfterDeposit;

            bool wasFarming =
                currentState == WorkerState.Farming ||
                resumeFarmingAfterDeposit;

            if (wasHarvesting)
            {
                if (targetResource != null)
                    lastHarvestPoint = targetResource.GetCenterPosition();

                resumeHarvestType = harvestingResourceType;
                resumeHarvestAfterDeposit = true;
                useLastHarvestPointForSearch = true;
            }
            else
            {
                resumeHarvestAfterDeposit = false;
                useLastHarvestPointForSearch = false;
            }

            if (wasFarming && targetGarden != null)
                resumeFarmingAfterDeposit = true;
            else if (!wasFarming)
                resumeFarmingAfterDeposit = false;
        }

        [Server]
        private void BeginDepositToBuilding(RTSokak.Buildings.Building deposit)
        {
            if (deposit == null)
            {
                BeginDepositToCapital();
                return;
            }

            ReleaseCurrentResourceClaim();
            LeaveConstruction();

            cachedApproachPoint = deposit.GetApproachPosition(transform.position);
            hasCachedApproach = true;
            syncedDestination = cachedApproachPoint;
            currentState = WorkerState.Depositing;
            targetBuilding = deposit;
            TrySetAgentDestination(cachedApproachPoint);
        }

        [Command(requiresAuthority = false)]
        private void CmdPlaceBuilding(RTSokak.Buildings.BuildingType type, Vector3 worldPos)
        {
            // Legacy fallback — основная логика в RTSPlayer.CmdPlaceBuilding
            RTSPlayer player = GetOwnerPlayer();
            if (player == null) return;
            player.ServerPlaceBuilding(type, worldPos, this);
        }

        [Command(requiresAuthority = false)]
        private void CmdJoinBuilding(GameObject buildingObj)
        {
            if (buildingObj == null) return;
            var building = buildingObj.GetComponent<RTSokak.Buildings.Building>();
            if (building == null || building.IsComplete)
                return;
            if (!building.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                return;

            ReleaseCurrentResourceClaim();
            LeaveConstruction();
            LeaveFarming();
            resumeHarvestAfterDeposit = false;
            AssignToBuilding(building);
        }

        [Server]
        private void AssignToBuilding(RTSokak.Buildings.Building building)
        {
            targetBuilding = building;
            cachedApproachPoint = building.GetApproachPosition(transform.position);
            hasCachedApproach = true;
            syncedDestination = cachedApproachPoint;
            currentState = WorkerState.MovingToBuild;
            TrySetAgentDestination(cachedApproachPoint);
        }

        /// <summary>
        /// Ищет следующий свободный ресурс того же типа в радиусе
        /// </summary>
        [Server]
        private void FindNextResourceInRadius()
        {
            if (cachedResourceManager == null)
            {
                cachedResourceManager = FindObjectOfType<RTSokak.Resources.ResourceManager>();
            }
            
            if (cachedResourceManager == null)
            {
                UnityEngine.Debug.LogWarning("ResourceManager not found! Retrying next frame...");
                isSearchingForResource = false;
                return;
            }

            EnsureNetIdentity();
            ReleaseCurrentResourceClaim();
            targetResource = null;

            Vector3 searchFrom = transform.position;
            if (useLastHarvestPointForSearch)
            {
                searchFrom = lastHarvestPoint;
                useLastHarvestPointForSearch = false;
            }

            // После сдачи в столицу ищем относительно запомненной точки, без лимита autoHarvestRadius —
            // используем увеличенный радиус
            float radius = Vector3.Distance(searchFrom, transform.position) > autoHarvestRadius
                ? redirectSearchRadius
                : autoHarvestRadius;
            if (resumeHarvestType != default && currentState == WorkerState.SearchingResource)
            {
                // keep harvestingResourceType
            }

            UnityEngine.Debug.Log($"[FindNextResource] Searching at {searchFrom}, radius={radius}, type={harvestingResourceType}");

            RTSokak.Resources.Resource closest = cachedResourceManager.GetNearestAvailableResource(
                searchFrom,
                harvestingResourceType,
                radius,
                netIdentity);

            if (closest != null && closest.TryClaim(netIdentity))
            {
                BeginMoveToClaimedResource(closest);
                UnityEngine.Debug.Log($"Found next free resource at dist={Vector3.Distance(transform.position, closest.GetCenterPosition()):F1}");
            }
            else
            {
                currentState = WorkerState.Idle;
                targetResource = null;
                isSearchingForResource = false;
                UnityEngine.Debug.Log("No more free resources in radius, going idle");
            }
        }

        // CmdMoveTo / MoveTo — в RTSUnit; отмена экономики через OnServerCancelTasksForMove

        /// <summary>
        /// Рисует линию пути от юнита до цели
        /// </summary>
        private void DrawPath()
        {
            if (pathLine == null || navAgent == null || !navAgent.hasPath)
            {
                return;
            }

            NavMeshPath path = navAgent.path;
            
            if (path.corners.Length < 2)
            {
                pathLine.enabled = false;
                return;
            }

            pathLine.enabled = true;
            ApplyPathLineMaterial();

            // Проверяем идём ли мы к ресурсу
            bool movingToResource = (currentState == WorkerState.Moving || currentState == WorkerState.Harvesting) 
                                    && targetResource != null;

            // Если идём к ресурсу, добавляем финальную точку в центре ресурса
            int totalPoints = path.corners.Length;
            if (movingToResource)
            {
                totalPoints++; // Дополнительная точка для центра ресурса
            }

            pathLine.positionCount = totalPoints;

            for (int i = 0; i < path.corners.Length; i++)
            {
                Vector3 point = path.corners[i];
                
                // Линия на высоте lineHeight
                if (i == 0)
                {
                    // Первая точка - от центра юнита (текущая позиция)
                    point = transform.position;
                }
                else
                {
                    // Остальные точки - на земле + небольшой отступ
                    point.y = lineHeight;
                }
                
                pathLine.SetPosition(i, point);
            }

            // Добавляем финальную точку в центре ресурса
            if (movingToResource)
            {
                Vector3 resourceCenter = targetResource.transform.position;
                BoxCollider collider = targetResource.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    resourceCenter = targetResource.transform.TransformPoint(collider.center);
                }
                resourceCenter.y = lineHeight;

                pathLine.SetPosition(totalPoints - 1, resourceCenter);
            }
        }
        
        /// <summary>
        /// Спавнит инструмент для текущего типа ресурса
        /// СЕРВЕР: устанавливает activeToolType, который синхронизируется на клиенты
        /// </summary>
        [Server]
        private void SpawnHarvestTool()
        {
            // Проверяем что есть точка крепления
            if (toolHoldPoint == null)
            {
                UnityEngine.Debug.LogWarning("toolHoldPoint not assigned! Cannot spawn harvest tool.");
                return;
            }
            
            // Удаляем предыдущий инструмент если есть
            DestroyHarvestTool();
            
            // Устанавливаем тип инструмента (синхронизируется на клиенты)
            if (harvestingResourceType == RTSokak.Resources.ResourceType.Wood)
            {
                activeToolType = 0; // Топор
            }
            else if (harvestingResourceType == RTSokak.Resources.ResourceType.Ore)
            {
                activeToolType = 1; // Кирка
            }
            else
            {
                activeToolType = -1; // Нет инструмента
            }
            
            // Hook OnActiveToolChanged автоматически создаст инструмент на всех клиентах (включая сервер)
        }
        
        /// <summary>
        /// Удаляет текущий инструмент
        /// СЕРВЕР: сбрасывает activeToolType, что синхронизируется на клиенты
        /// </summary>
        [Server]
        private void DestroyHarvestTool()
        {
            activeToolType = -1; // hook OnActiveToolChanged удалит инструмент везде
        }
        
        /// <summary>
        /// Hook для SyncVar activeToolType - вызывается на ВСЕХ клиентах когда сервер меняет значение
        /// </summary>
        private void OnActiveToolChanged(int oldValue, int newValue)
        {
            // Удаляем старый инструмент
            if (currentToolInstance != null)
            {
                Destroy(currentToolInstance);
                currentToolInstance = null;
            }
            isAnimatingTool = false;
            
            // Создаём новый если нужно
            if (newValue >= 0)
            {
                CreateToolInstance(newValue);
                // Молоток (стройка): анимация сразу на всех машинах
                if (newValue == 2)
                {
                    isAnimatingTool = true;
                    toolAnimationStartTime = Time.time;
                    harvestStartTime = Time.time;
                }
            }
        }
        
        /// <summary>
        /// Создаёт instance инструмента (вызывается на сервере и клиентах)
        /// </summary>
        private void CreateToolInstance(int toolType)
        {
            if (toolHoldPoint == null) return;
            
            GameObject toolPrefab = null;
            if (toolType == 0 && axePrefab != null)
            {
                toolPrefab = axePrefab;
            }
            else if (toolType == 1 && pickaxePrefab != null)
            {
                toolPrefab = pickaxePrefab;
            }
            else if (toolType == 2 && hammerPrefab != null)
            {
                toolPrefab = hammerPrefab;
            }
            
            if (toolPrefab == null)
            {
                UnityEngine.Debug.LogWarning($"No tool prefab assigned for tool type {toolType}");
                return;
            }
            
            // Спавним инструмент
            currentToolInstance = Instantiate(toolPrefab, toolHoldPoint);
            currentToolInstance.transform.localPosition = Vector3.zero;
            currentToolInstance.transform.localRotation = Quaternion.Euler(toolStartAngle, 0, 0);
            
            // НЕ запускаем анимацию сразу - она начнется после завершения поворота
            toolAnimationStartTime = Time.time;
            // isAnimatingTool будет установлен в true после завершения поворота
            
            UnityEngine.Debug.Log($"Created tool type {toolType} at {toolHoldPoint.position}");
        }
        
        /// <summary>
        /// Анимирует поворот инструмента
        /// Вызывается в Update() когда isAnimatingTool = true
        /// РАБОТАЕТ НА ВСЕХ КЛИЕНТАХ (не [Server])
        /// </summary>
        private void AnimateHarvestTool()
        {
            if (!isAnimatingTool || currentToolInstance == null)
                return;

            float duration = Mathf.Max(0.05f, EffectiveToolAnimDuration);
            float elapsed = Time.time - toolAnimationStartTime;

            // Один свинг юнита-игрока — останавливаем анимацию в конце
            if (toolAnimDurationOverride > 0.01f && elapsed >= duration)
            {
                currentToolInstance.transform.localRotation = Quaternion.Euler(toolStartAngle, 0, 0);
                isAnimatingTool = false;
                toolAnimDurationOverride = 0f;
                return;
            }

            float halfDuration = duration * 0.5f;
            float t;
            float angle;

            if (elapsed < halfDuration)
            {
                t = elapsed / halfDuration;
                t = EaseInOutQuad(t);
                angle = Mathf.Lerp(toolStartAngle, toolEndAngle, t);
            }
            else
            {
                t = Mathf.Clamp01((elapsed - halfDuration) / halfDuration);
                t = EaseInQuad(t);
                angle = Mathf.Lerp(toolEndAngle, toolStartAngle, t);
            }

            currentToolInstance.transform.localRotation = Quaternion.Euler(angle, 0, 0);
        }
        
        /// <summary>
        /// Easing функции для плавной анимации
        /// </summary>
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
        
        private float EaseInQuad(float t)
        {
            return t * t;
        }

        private float nextPlayerPrimaryTime;
        private float nextPlayerAutoDepositTime;
        private bool playerHarvestPending;
        private float playerHarvestReadyTime;
        private RTSokak.Resources.ResourceType playerHarvestType;
        private NetworkIdentity playerHarvestTarget;
        private NetworkIdentity playerGardenTarget;
        private int playerGardenPlot = -1;
        private bool playerGardenIsHarvest;

        public override float GetPlayerActionCooldown()
        {
            int slot = playerHotbarSlot;
            if (RTSokak.UI.UnitPlayerHud.Instance != null && RTSokak.UI.UnitPlayerHud.Instance.SelectedSlot >= 0)
                slot = RTSokak.UI.UnitPlayerHud.Instance.SelectedSlot;
            return slot == 0 ? attackCooldown : Mathf.Max(0.2f, playerToolSwingDuration);
        }

        [Server]
        private RTSPlayer FindMayorPlayerForWorker()
        {
            if (!string.IsNullOrEmpty(ownerNickname))
            {
                foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                {
                    if (p != null && string.Equals(p.Nickname, ownerNickname, System.StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }

            if (ownerPlayerIndex >= 0)
            {
                foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                {
                    if (p != null && p.PlayerIndex == ownerPlayerIndex)
                        return p;
                }
            }

            return GetOwnerPlayer();
        }

        [Server]
        private void ServerUpdatePlayerAutoDeposit()
        {
            if (Time.time < nextPlayerAutoDepositTime)
                return;
            nextPlayerAutoDepositTime = Time.time + 0.2f;

            if (!IsAlive || InventoryTotal <= 0)
                return;

            var buildings = FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
            Buildings.Building nearestBuilding = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < buildings.Length; i++)
            {
                var b = buildings[i];
                if (b == null || !b.IsComplete)
                    continue;
                if (b.Type != Buildings.BuildingType.Capital && b.Type != Buildings.BuildingType.Warehouse)
                    continue;
                if (!b.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                    continue;

                float dist = DistanceToBuildingSurface(b);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestBuilding = b;
                }
            }

            if (nearestBuilding != null && nearestDist <= 3.6f)
            {
                RTSPlayer mayor = FindMayorPlayerForWorker();
                if (mayor != null)
                {
                    int depWood = inventoryWood;
                    int depOre = inventoryOre;
                    int depFood = inventoryFood;

                    if (depWood > 0) { mayor.AddWood(depWood); inventoryWood = 0; }
                    if (depOre > 0) { mayor.AddOre(depOre); inventoryOre = 0; }
                    if (depFood > 0) { mayor.AddFood(depFood); inventoryFood = 0; }

                    RpcShowDepositFloatingText(nearestBuilding.transform.position + Vector3.up * 2f, depWood, depOre, depFood);
                    RpcNotifyPlayerWorkerDeposited(depWood, depOre, depFood);
                }
            }
        }

        [ClientRpc]
        private void RpcNotifyPlayerWorkerDeposited(int wood, int ore, int food)
        {
            if (UnitPlayerController.Instance != null &&
                UnitPlayerController.Instance.ControlledUnit == this)
            {
                if (RTSokak.UI.UnitPlayerHud.Instance != null)
                {
                    RTSokak.UI.UnitPlayerHud.Instance.OnPlayerDeposited(wood, ore, food);
                }
            }
        }

        /// <summary>Worker hotbar: 0 Sword, 1 Axe, 2 Pickaxe, -1 empty. Всегда можно экипировать.</summary>
        [Server]
        public override void ServerApplyHotbarSlot(int slotIndex)
        {
            playerHotbarSlot = slotIndex;
            CancelPlayerHarvestPending();

            if (slotIndex < 0)
            {
                DestroyHarvestTool();
                EquipSword(false);
                return;
            }

            if (slotIndex == 0)
            {
                DestroyHarvestTool();
                activeToolType = -1;
                EquipSword(true);
                return;
            }

            EquipSword(false);
            if (slotIndex == 1)
                activeToolType = 0; // axe
            else if (slotIndex == 2)
                activeToolType = 1; // pickaxe
            else
                activeToolType = -1;
        }

        [Server]
        void CancelPlayerHarvestPending()
        {
            playerHarvestPending = false;
            playerHarvestTarget = null;
            playerGardenTarget = null;
            playerGardenPlot = -1;
        }

        [Server]
        void ServerUpdatePlayerHarvestPending()
        {
            if (!playerHarvestPending || Time.time < playerHarvestReadyTime)
                return;

            playerHarvestPending = false;

            RTSokak.Resources.Resource best = null;
            if (playerHarvestTarget != null)
            {
                best = playerHarvestTarget.GetComponent<RTSokak.Resources.Resource>();
                if (best != null && best.IsHarvested)
                    best = null;
            }

            // Fallback: ближайший подходящий ресурс в радиусе
            if (best == null)
            {
                float tol = harvestDistanceTolerance;
                Collider[] cols = Physics.OverlapSphere(transform.position, tol);
                float bestDist = float.MaxValue;
                for (int i = 0; i < cols.Length; i++)
                {
                    var res = cols[i].GetComponentInParent<RTSokak.Resources.Resource>();
                    if (res == null || res.IsHarvested)
                        continue;
                    bool typeOk =
                        (playerHarvestType == RTSokak.Resources.ResourceType.Wood &&
                         res.Type == RTSokak.Resources.ResourceType.Wood) ||
                        (playerHarvestType == RTSokak.Resources.ResourceType.Ore &&
                         res.Type == RTSokak.Resources.ResourceType.Ore);
                    if (!typeOk)
                        continue;
                    float d = Vector3.Distance(transform.position, res.GetCenterPosition());
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = res;
                    }
                }
            }
            else
            {
                float d = Vector3.Distance(transform.position, best.GetCenterPosition());
                if (d > harvestDistanceTolerance + 1.5f)
                    best = null;
            }

            if (best != null && !IsInventoryFull)
            {
                int amount = best.Harvest();
                harvestingResourceType = best.Type;
                if (amount > 0)
                {
                    AddToInventory(best.Type, amount);
                    RpcShowHarvestFloatingText(best.GetCenterPosition() + Vector3.up * 1.2f, best.Type, amount);
                }
            }

            playerHarvestTarget = null;

            if (playerGardenTarget != null && playerGardenPlot >= 0)
            {
                var garden = playerGardenTarget.GetComponent<RTSokak.Buildings.GardenBed>();
                if (garden != null)
                {
                    if (playerGardenIsHarvest)
                    {
                        int gained = garden.ServerHarvestAndReplant(playerGardenPlot);
                        if (gained > 0)
                        {
                            AddToInventory(RTSokak.Resources.ResourceType.Food, gained);
                            RpcShowHarvestFloatingText(transform.position + transform.forward * 1.2f + Vector3.up * 1.0f, RTSokak.Resources.ResourceType.Food, gained);
                        }
                    }
                    else
                        garden.ServerPlant(playerGardenPlot);
                }
                playerGardenTarget = null;
                playerGardenPlot = -1;
            }
        }

        [Server]
        public override void ServerPlayerPrimaryAction(Vector3 rayOrigin, Vector3 rayDir)
        {
            if (!IsPlayerControlled || playerHotbarSlot < 0)
                return;
            if (Time.time < nextPlayerPrimaryTime || playerHarvestPending)
                return;

            // Sword — сразу удар
            if (playerHotbarSlot == 0)
            {
                base.ServerPlayerPrimaryAction(rayOrigin, rayDir);
                nextPlayerPrimaryTime = Time.time + attackCooldown;
                return;
            }

            // Axe / Pickaxe — короткий свинг; ресурсы сразу после анимации (не wood/ore AI duration)
            float duration = Mathf.Max(0.2f, playerToolSwingDuration);
            playerHarvestType = playerHotbarSlot == 1
                ? RTSokak.Resources.ResourceType.Wood
                : RTSokak.Resources.ResourceType.Ore;
            harvestingResourceType = playerHarvestType;

            playerHarvestTarget = null;
            playerGardenTarget = null;
            playerGardenPlot = -1;

            Vector3 dir = rayDir.sqrMagnitude > 0.0001f ? rayDir.normalized : transform.forward;
            if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, 5f))
            {
                var res = hit.collider.GetComponentInParent<RTSokak.Resources.Resource>();
                if (res != null && !res.IsHarvested && res.Type == playerHarvestType)
                {
                    var ni = res.GetComponent<NetworkIdentity>();
                    if (ni != null)
                        playerHarvestTarget = ni;
                }
                else
                {
                    var garden = hit.collider.GetComponentInParent<RTSokak.Buildings.GardenBed>();
                    if (garden != null && garden.TryGetNextWorkNear(transform.position, out int plot, out bool harvest))
                    {
                        var ni = garden.GetComponent<NetworkIdentity>();
                        if (ni != null)
                        {
                            playerGardenTarget = ni;
                            playerGardenPlot = plot;
                            playerGardenIsHarvest = harvest;
                        }
                    }
                }
            }

            // Ближайший ресурс у ног, если прицел мимо
            if (playerHarvestTarget == null && playerGardenTarget == null)
            {
                Collider[] cols = Physics.OverlapSphere(transform.position, harvestDistanceTolerance);
                float bestDist = float.MaxValue;
                for (int i = 0; i < cols.Length; i++)
                {
                    var res = cols[i].GetComponentInParent<RTSokak.Resources.Resource>();
                    if (res == null || res.IsHarvested || res.Type != playerHarvestType)
                        continue;
                    float d = Vector3.Distance(transform.position, res.GetCenterPosition());
                    if (d < bestDist)
                    {
                        bestDist = d;
                        playerHarvestTarget = res.GetComponent<NetworkIdentity>();
                    }
                }
            }

            if (playerHotbarSlot == 1)
                activeToolType = 0;
            else if (playerHotbarSlot == 2)
                activeToolType = 1;

            playerHarvestPending = true;
            playerHarvestReadyTime = Time.time + duration;
            nextPlayerPrimaryTime = playerHarvestReadyTime;
            RpcPlayerToolSwing(duration);
        }

        [ClientRpc(includeOwner = true)]
        private void RpcPlayerToolSwing(float duration)
        {
            toolAnimDurationOverride = Mathf.Max(0.2f, duration);
            isAnimatingTool = true;
            toolAnimationStartTime = Time.time;
            harvestStartTime = Time.time;

            // Если инструмент ещё не создан SyncVar-хуком — создадим локально
            if (currentToolInstance == null && activeToolType >= 0)
                CreateToolInstance(activeToolType);
        }
    }
}
