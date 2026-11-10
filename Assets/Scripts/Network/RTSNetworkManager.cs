using Mirror;
using RTSokak.Buildings;
using RTSokak.Shared;
using UnityEngine;

namespace RTSokak.Network
{
    public class RTSNetworkManager : NetworkManager
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string GameSceneName = "SampleScene";

        public static RTSNetworkManager Singleton { get; private set; }

        [Header("RTS Settings")]
        [SerializeField] private GameObject workerPrefab;
        [SerializeField] private GameObject warriorPrefab;
        [SerializeField] private GameObject archerPrefab;
        [SerializeField] private int workersPerPlayer = 3;
        [SerializeField] private float workerSpawnRadius = 5f;

        [Header("Resource System")]
        [SerializeField] private bool autoGenerateResources = true;

        [Header("Discovery")]
        [SerializeField] RTSNetworkDiscovery networkDiscovery;

        RTSokak.Resources.ResourceManager resourceManager;
        bool lanAdvertised;
        bool isSinglePlayerHostSession;

        public GameObject WorkerPrefab => workerPrefab;
        public GameObject WarriorPrefab => warriorPrefab;
        public GameObject ArcherPrefab => archerPrefab;
        public bool IsLanAdvertised => lanAdvertised;
        public RTSNetworkDiscovery NetworkDiscovery => networkDiscovery;

        public override void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
                return;
            }

            Singleton = this;
            ForceRunInBackground();

            if (string.IsNullOrWhiteSpace(offlineScene))
                offlineScene = MainMenuSceneName;
            if (string.IsNullOrWhiteSpace(onlineScene))
                onlineScene = GameSceneName;

            NetworkManagerHUD hud = GetComponent<NetworkManagerHUD>();
            if (hud != null)
                hud.enabled = false;

            if (networkDiscovery == null)
                networkDiscovery = GetComponent<RTSNetworkDiscovery>();

            base.Awake();

            EnsurePrefabsAssigned();

            NetworkTransportConfig transportConfig = GetComponent<NetworkTransportConfig>();
            if (transportConfig == null)
                transportConfig = gameObject.AddComponent<NetworkTransportConfig>();
            else
                transportConfig.ApplyFromSession();
        }

        /// <summary>
        /// Если в сцене забыли проставить префабы — подтягиваем из Assets (Editor) / spawnPrefabs.
        /// </summary>
        void EnsurePrefabsAssigned()
        {
#if UNITY_EDITOR
            if (playerPrefab == null)
            {
                playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RTSPlayer.prefab");
                if (playerPrefab == null)
                    UnityEngine.Debug.LogWarning("[RTSNetworkManager] Failed to load RTSPlayer.prefab");
            }
            if (workerPrefab == null)
            {
                workerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Worker.prefab");
                if (workerPrefab == null)
                    UnityEngine.Debug.LogWarning("[RTSNetworkManager] Failed to load Worker.prefab");
            }
            if (warriorPrefab == null)
            {
                warriorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Warrior.prefab");
                if (warriorPrefab == null)
                    UnityEngine.Debug.LogWarning("[RTSNetworkManager] Failed to load Warrior.prefab");
            }

            RegisterSpawnPrefab(playerPrefab);
            RegisterSpawnPrefab(workerPrefab);
            RegisterSpawnPrefab(warriorPrefab);
            if (archerPrefab == null)
            {
                archerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Archer.prefab");
                if (archerPrefab == null)
                    UnityEngine.Debug.LogWarning("[RTSNetworkManager] Failed to load Archer.prefab");
            }
            RegisterSpawnPrefab(archerPrefab);
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Terrain/Tree.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Terrain/Iron.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Buildings/Capital.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Buildings/House.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Buildings/Warehouse.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Buildings/Barracks.prefab");
            RegisterSpawnPrefabFromPath("Assets/Prefabs/Buildings/Garden bed.prefab");
#else
            // Build: берём из уже заполненного spawnPrefabs.
            if (workerPrefab == null)
            {
                for (int i = 0; i < spawnPrefabs.Count; i++)
                {
                    GameObject p = spawnPrefabs[i];
                    if (p != null && p.GetComponent<Worker>() != null)
                    {
                        workerPrefab = p;
                        break;
                    }
                }
            }

            if (warriorPrefab == null)
            {
                for (int i = 0; i < spawnPrefabs.Count; i++)
                {
                    GameObject p = spawnPrefabs[i];
                    if (p != null && p.GetComponent<Warrior>() != null)
                    {
                        warriorPrefab = p;
                        break;
                    }
                }
            }

            if (archerPrefab == null)
            {
                for (int i = 0; i < spawnPrefabs.Count; i++)
                {
                    GameObject p = spawnPrefabs[i];
                    if (p != null && p.GetComponent<Archer>() != null)
                    {
                        archerPrefab = p;
                        break;
                    }
                }
            }
#endif

            if (playerPrefab == null)
                UnityEngine.Debug.LogError("[RTSNetworkManager] playerPrefab is not assigned!");
            if (workerPrefab == null)
                UnityEngine.Debug.LogError("[RTSNetworkManager] workerPrefab is not assigned!");
        }

        void RegisterSpawnPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[RTSNetworkManager] Attempted to register null spawn prefab - skipping");
                return;
            }
            
            if (spawnPrefabs.Contains(prefab))
                return;
                
            spawnPrefabs.Add(prefab);
        }

#if UNITY_EDITOR
        void RegisterSpawnPrefabFromPath(string path)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning($"[RTSNetworkManager] Failed to load prefab from path: {path}");
                return;
            }
            RegisterSpawnPrefab(prefab);
        }
#endif

        void OnDestroy()
        {
            if (Singleton == this)
                Singleton = null;
        }

        public void ApplySessionPort()
        {
            ushort port = PlayerSession.Port == 0 ? PlayerSession.DefaultPort : PlayerSession.Port;

            if (transport is PortTransport assignedTransport)
                assignedTransport.Port = port;

            if (Transport.active is PortTransport activeTransport)
                activeTransport.Port = port;

            PortTransport[] ports = GetComponents<PortTransport>();
            for (int i = 0; i < ports.Length; i++)
            {
                if (ports[i] != null)
                    ports[i].Port = port;
            }
        }

        public ushort GetActiveListenPort()
        {
            if (Transport.active is PortTransport active)
                return active.Port;
            if (transport is PortTransport assigned)
                return assigned.Port;
            return PlayerSession.Port;
        }

        public void StartSinglePlayerHost()
        {
            if (PlayerSession.Port == 0)
                PlayerSession.SetPort(PlayerSession.DefaultPort);

            NetworkTransportConfig.Instance?.ApplyFromSession();
            ApplySessionPort();
            networkAddress = "127.0.0.1";
            lanAdvertised = false;
            isSinglePlayerHostSession = true;
            networkDiscovery?.StopDiscovery();

            CleanupSpawnPrefabs();

            try
            {
                StartHost();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[RTSNetworkManager] StartHost failed: {ex}");
                throw;
            }
        }

        public void OpenToLan()
        {
            if (!NetworkServer.active)
                return;

            networkDiscovery?.AdvertiseServerOnInterface(PlayerSession.LanInterfaceIPv4);
            lanAdvertised = true;
            isSinglePlayerHostSession = false;
        }

        public void StopLanAdvertisement()
        {
            networkDiscovery?.StopDiscovery();
            lanAdvertised = false;
        }

        public void StartLanClient(System.Uri uri)
        {
            if (uri == null)
            {
                UnityEngine.Debug.LogError("[RTSNetworkManager] StartLanClient: uri is null");
                return;
            }

            UnityEngine.Debug.Log($"[RTSNetworkManager] StartLanClient: Connecting to {uri.Host}:{uri.Port}");
            WorldSession.Clear();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            ApplySessionPort();
            networkAddress = uri.Host;
            if (Transport.active is PortTransport portTransport)
                portTransport.Port = (ushort)uri.Port;

            CleanupSpawnPrefabs();
            UnityEngine.Debug.Log($"[RTSNetworkManager] StartLanClient: Calling StartClient(uri)");
            StartClient(uri);
        }

        public void StartDirectClient(string host, ushort port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                UnityEngine.Debug.LogError("[RTSNetworkManager] StartDirectClient: host is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"[RTSNetworkManager] StartDirectClient: Connecting to {host}:{port}");
            WorldSession.Clear();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            ApplySessionPort();
            networkAddress = host;
            if (Transport.active is PortTransport portTransport)
                portTransport.Port = port;

            CleanupSpawnPrefabs();
            UnityEngine.Debug.Log($"[RTSNetworkManager] StartDirectClient: Calling StartClient()");
            StartClient();
        }

        /// <summary>
        /// Удалить null и невалидные prefabs из spawnPrefabs перед запуском клиента/сервера
        /// </summary>
        void CleanupSpawnPrefabs()
        {
            int removed = 0;
            for (int i = spawnPrefabs.Count - 1; i >= 0; i--)
            {
                GameObject prefab = spawnPrefabs[i];
                if (prefab == null)
                {
                    UnityEngine.Debug.LogWarning($"[RTSNetworkManager] Removing null prefab from spawnPrefabs at index {i}");
                    spawnPrefabs.RemoveAt(i);
                    removed++;
                }
                else if (prefab.GetComponent<NetworkIdentity>() == null)
                {
                    UnityEngine.Debug.LogWarning($"[RTSNetworkManager] Removing invalid prefab '{prefab.name}' (no NetworkIdentity) from spawnPrefabs at index {i}");
                    spawnPrefabs.RemoveAt(i);
                    removed++;
                }
            }
            
            if (removed > 0)
                UnityEngine.Debug.Log($"[RTSNetworkManager] Cleaned up {removed} invalid prefab(s) from spawnPrefabs");
        }

        public override void OnStartServer()
        {
            ForceRunInBackground();
            base.OnStartServer();

            // Terrain / ResourceManager создаём в OnServerSceneChanged после загрузки SampleScene.
            // OnStartServer при StartHost из MainMenu ещё в offline-сцене — иначе объекты теряются.

            if (WorldSession.IsCreatingNewWorld && WorldSession.HasActiveWorld &&
                !WorldSaveService.WorldExists(WorldSession.ActiveWorldName))
            {
                WorldSaveService.TryCreateWorld(WorldSession.ActiveWorldName, out _);
            }

            ushort listenPort = GetActiveListenPort();
            PlayerSession.SetPort(listenPort);
            UnityEngine.Debug.Log(
                $"[RTSNetworkManager] Server listening on port {listenPort} " +
                $"({(transport != null ? transport.GetType().Name : "null")}).");
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (!IsGameScene(sceneName))
                return;

            CreateTerrainIfNeeded();

            if (autoGenerateResources)
                CreateResourceManager(WorldSession.ShouldRestoreFromSave);

            UnityEngine.Debug.Log(
                $"[RTSNetworkManager] Game scene ready. restore={WorldSession.ShouldRestoreFromSave}, " +
                $"world={WorldSession.ActiveWorldName}, rm={(resourceManager != null)}");
        }

        public override void OnStartClient()
        {
            ForceRunInBackground();
            base.OnStartClient();
        }

        public override void OnStopServer()
        {
            // НЕ сохраняем здесь - сохранение происходит при отключении каждого клиента в OnServerDisconnect
            // К моменту OnStopServer все NetworkIdentity уже мертвы, сохранение вернёт пустой мир
            StopLanAdvertisement();
            resourceManager = null;
            base.OnStopServer();
        }

        public override void OnStopClient()
        {
            // Не чистим WorldSession здесь — смена MainMenu→SampleScene может дернуть stop.
            // Clear делается в MainMenuUI.Awake / ExitToMenu / StartLanClient.
            base.OnStopClient();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            EnsurePrefabsAssigned();
            EnsureGameWorldReady();

            if (playerPrefab == null)
            {
                UnityEngine.Debug.LogError("[RTSNetworkManager] Cannot add player — playerPrefab is null.");
                return;
            }

            // Получить IP адрес клиента (для логирования и legacy поддержки)
            string clientIp = conn.address;

            // НОВОЕ ПОДКЛЮЧЕНИЕ - создать player object
            // Nickname будет установлен через Command после подключения
            GameObject playerObject = Instantiate(playerPrefab);
            NetworkServer.AddPlayerForConnection(conn, playerObject);

            RTSPlayer player = playerObject.GetComponent<RTSPlayer>();
            int playerIndex = NetworkServer.connections.Count - 1;
            if (player != null)
            {
                player.SetPlayerIndex(playerIndex);
                player.SetConnectionId(conn.connectionId);
                player.SetIpAddress(clientIp);
            }

            if (resourceManager == null)
                resourceManager = FindFirstObjectByType<RTSokak.Resources.ResourceManager>();

            bool restore = WorldSession.ShouldRestoreFromSave;
            RTSWorldSaveData data = WorldSession.ActiveSaveData ?? WorldSession.PendingLoad;

            UnityEngine.Debug.Log(
                $"[RTSNetworkManager] AddPlayer idx={playerIndex} ip={clientIp} restore={restore} " +
                $"pendingUnits={data?.units?.Count ?? 0} pendingRes={data?.resources?.Count ?? 0} " +
                $"rm={(resourceManager != null)} workerPrefab={(workerPrefab != null)}");

            // Геометрия + ВСЕ юниты всех игроков — один раз при первом входе в мир
            if (restore && data != null && !WorldSession.WorldGeometryRestored)
            {
                WorldSnapshotService.RestoreWorldGeometry(data, resourceManager);
                WorldSession.ClearPendingLoad();
            }

            // Экономику / роль / поселение — в CmdRegisterClient.
            // Стартовых воркеров больше не спавним здесь: мэр подтверждает точку поселения.

            // Поздний клиент: повторно разослать seed травы и время суток
            var grass = FindFirstObjectByType<RTSokak.Terrain.ProceduralGrassGenerator>();
            if (grass != null)
                grass.ServerBroadcastSeed();

            var dayNight = FindFirstObjectByType<RTSokak.Environment.DayNightCycle>();
            if (dayNight != null)
                dayNight.SetTimeOfDay(dayNight.GetTimeOfDay());
        }

        static int CountOwnedUnits(int connectionId)
        {
            int count = 0;
            foreach (var u in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u != null && u.IsAlive && u.OwnerConnectionId == connectionId)
                    count++;
            }
            return count;
        }

        void EnsureGameWorldReady()
        {
            if (!NetworkServer.active)
                return;
            if (!IsGameScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
                return;

            CreateTerrainIfNeeded();
            EnsureDayNightCycleExists();
            EnsureGrassNetworked();
            if (autoGenerateResources && resourceManager == null)
                CreateResourceManager(WorldSession.ShouldRestoreFromSave);
            else if (resourceManager == null)
                resourceManager = FindFirstObjectByType<RTSokak.Resources.ResourceManager>();
        }

        void EnsureDayNightCycleExists()
        {
            var existing = FindFirstObjectByType<RTSokak.Environment.DayNightCycle>();
            if (existing != null)
            {
                if (existing.GetComponent<RTSokak.Environment.SkyVisuals>() == null)
                    existing.gameObject.AddComponent<RTSokak.Environment.SkyVisuals>();
                return;
            }

            GameObject go = new GameObject("DayNightCycle");
            go.AddComponent<RTSokak.Environment.DayNightCycle>();
            go.AddComponent<RTSokak.Environment.SkyVisuals>();
            UnityEngine.Debug.Log("[RTSNetworkManager] Created DayNightCycle + SkyVisuals (was missing in scene)");
        }

        /// <summary>
        /// Если генератор травы есть в сцене с NetworkIdentity — заспавнить, чтобы SyncVar seed дошёл до клиентов.
        /// Без NetworkIdentity клиентский генератор получит seed через GrassSeedMessage.
        /// </summary>
        void EnsureGrassNetworked()
        {
            var grassGen = FindFirstObjectByType<RTSokak.Terrain.ProceduralGrassGenerator>();
            if (grassGen == null)
                return;

            NetworkIdentity identity = grassGen.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                // Без NI — seed уйдёт клиентам сообщением из самого генератора
                grassGen.ServerBroadcastSeed();
                return;
            }

            if (identity.netId == 0)
                NetworkServer.Spawn(grassGen.gameObject);
        }

        static bool IsGameScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            return string.Equals(name, GameSceneName, System.StringComparison.OrdinalIgnoreCase);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // НЕ уничтожаем юниты - они остаются в мире!
            // Игрок может переподключиться по тому же IP
            
            UnityEngine.Debug.Log($"[RTSNetworkManager] Client disconnected: {conn.address} (connId={conn.connectionId})");
            
            // КРИТИЧНО: Сохранить мир ПЕРЕД base.OnServerDisconnect, пока player object ещё жив!
            if (WorldSession.HasActiveWorld && conn.identity != null)
            {
                RTSPlayer player = conn.identity.GetComponent<RTSPlayer>();
                if (player != null)
                {
                    UnityEngine.Debug.Log($"[RTSNetworkManager] Saving player data before disconnect (nickname='{player.Nickname}', IP={player.IpAddress}, playerIndex={player.PlayerIndex})");
                }
                
                UnityEngine.Debug.Log($"[RTSNetworkManager] Auto-saving world before disconnect...");
                WorldSnapshotService.TrySaveActiveWorld(out string error);
                if (!string.IsNullOrEmpty(error))
                    UnityEngine.Debug.LogError($"[RTSNetworkManager] Auto-save failed: {error}");
            }
            
            // Роли: unit→AI, mayor→отпустить unit-players в Waiting
            if (conn.identity != null)
            {
                RTSPlayer disconnecting = conn.identity.GetComponent<RTSPlayer>();
                if (disconnecting != null)
                    disconnecting.ServerHandleDisconnectRoles();
            }

            // Снять client authority со всех юнитов и зданий чтобы они не уничтожились
            RemoveClientAuthorityFromPlayerObjects(conn);
            
            // base.OnServerDisconnect уничтожит player object через RemovePlayerForConnection
            // Но мы уже сохранили мир выше, поэтому экономика сохранена
            base.OnServerDisconnect(conn);
        }
        
        /// <summary>
        /// Снять client authority со всех юнитов и зданий игрока
        /// чтобы они не уничтожились при отключении клиента
        /// </summary>
        [Server]
        void RemoveClientAuthorityFromPlayerObjects(NetworkConnectionToClient conn)
        {
            int removedCount = 0;
            
            // Снять authority с юнитов
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            foreach (RTSUnit unit in units)
            {
                if (unit != null && unit.OwnerConnectionId == conn.connectionId)
                {
                    NetworkIdentity netId = unit.GetComponent<NetworkIdentity>();
                    if (netId != null && netId.connectionToClient == conn)
                    {
                        netId.RemoveClientAuthority();
                        removedCount++;
                    }
                }
            }
            
            // Снять authority со зданий (хотя у них обычно нет authority)
            Building[] buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (Building building in buildings)
            {
                if (building != null && building.OwnerConnectionId == conn.connectionId)
                {
                    NetworkIdentity netId = building.GetComponent<NetworkIdentity>();
                    if (netId != null && netId.connectionToClient == conn)
                    {
                        netId.RemoveClientAuthority();
                        removedCount++;
                    }
                }
            }
            
            UnityEngine.Debug.Log($"[RTSNetworkManager] Removed client authority from {removedCount} objects for disconnecting player");
        }

        public void SpawnWorkersForPlayerPublic(NetworkConnectionToClient conn, GameObject playerObject)
        {
            RTSPlayer player = playerObject != null ? playerObject.GetComponent<RTSPlayer>() : null;
            int playerIndex = player != null && player.PlayerIndex >= 0
                ? player.PlayerIndex
                : Mathf.Max(0, NetworkServer.connections.Count - 1);
            Vector3 basePosition = new Vector3(playerIndex * 15f, 1f, playerIndex * 15f);
            SpawnWorkersForPlayerAt(conn, playerObject, basePosition);
        }

        public void SpawnWorkersForPlayerAtPublic(NetworkConnectionToClient conn, GameObject playerObject, Vector3 center)
        {
            SpawnWorkersForPlayerAt(conn, playerObject, center);
        }

        void SpawnWorkersForPlayerAt(NetworkConnectionToClient conn, GameObject playerObject, Vector3 center)
        {
            if (workerPrefab == null)
            {
                UnityEngine.Debug.LogError("Worker Prefab is not assigned in RTSNetworkManager!");
                return;
            }

            RTSPlayer player = playerObject != null ? playerObject.GetComponent<RTSPlayer>() : null;

            // Не спавнить второй набор, если у игрока уже есть юниты
            if (player != null)
            {
                int existing = 0;
                if (!string.IsNullOrEmpty(player.Nickname))
                    existing = WorldSnapshotService.CountUnitsByNickname(player.Nickname);
                if (existing <= 0 && conn != null)
                    existing = CountOwnedUnits(conn.connectionId);
                if (existing > 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RTSNetworkManager] Skip SpawnWorkers: '{player.Nickname}' already has {existing} units");
                    return;
                }
            }

            center.y = 1f;

            for (int i = 0; i < workersPerPlayer; i++)
            {
                Vector3 spawnPosition = center + Random.insideUnitSphere * workerSpawnRadius;
                spawnPosition.y = 1f;

                GameObject worker = Instantiate(workerPrefab, spawnPosition, Quaternion.identity);
                NetworkServer.Spawn(worker);

                Worker workerComponent = worker.GetComponent<Worker>();
                if (workerComponent != null)
                {
                    workerComponent.SetOwner(conn);
                    if (player != null && !string.IsNullOrEmpty(player.Nickname))
                        workerComponent.ServerSetOwnerNickname(player.Nickname);
                }
            }

            if (player != null)
                player.AddCurrentPopulation(workersPerPlayer);
        }

        void DestroyWorkersForPlayer(NetworkConnectionToClient conn)
        {
            Worker[] allWorkers = FindObjectsOfType<Worker>();
            foreach (Worker worker in allWorkers)
            {
                if (worker.OwnerConnectionId == conn.connectionId)
                    NetworkServer.Destroy(worker.gameObject);
            }

            var buildings = FindObjectsOfType<RTSokak.Buildings.Building>();
            foreach (var b in buildings)
            {
                if (b.OwnerConnectionId == conn.connectionId)
                    NetworkServer.Destroy(b.gameObject);
            }

            Warrior[] warriors = FindObjectsOfType<Warrior>();
            foreach (Warrior warrior in warriors)
            {
                if (warrior.OwnerConnectionId == conn.connectionId)
                    NetworkServer.Destroy(warrior.gameObject);
            }
        }

        void CreateResourceManager(bool skipGenerate)
        {
            resourceManager = FindObjectOfType<RTSokak.Resources.ResourceManager>();

            if (resourceManager == null)
            {
                GameObject rmObj = new GameObject("ResourceManager");
                rmObj.AddComponent<NetworkIdentity>();
                resourceManager = rmObj.AddComponent<RTSokak.Resources.ResourceManager>();

                RTSokak.Resources.ResourceGenerationConfig generatedConfig =
                    new RTSokak.Resources.ResourceGenerationConfig();
                generatedConfig.worldSize = 100;
                generatedConfig.forestCount = 5;
                generatedConfig.minForestSize = 8;
                generatedConfig.maxForestSize = 20;
                generatedConfig.forestDensity = 0.7f;
                generatedConfig.oreDepositCount = 3;
                generatedConfig.minOreSize = 5;
                generatedConfig.maxOreSize = 12;
                generatedConfig.oreDensity = 0.5f;

                generatedConfig.treePrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Terrain/Tree");
                if (generatedConfig.treePrefab == null)
                {
#if UNITY_EDITOR
                    string[] treeGuids = UnityEditor.AssetDatabase.FindAssets("Tree t:Prefab", new[] { "Assets/Prefabs/Terrain" });
                    if (treeGuids.Length > 0)
                    {
                        string treePath = UnityEditor.AssetDatabase.GUIDToAssetPath(treeGuids[0]);
                        generatedConfig.treePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                    }
#endif
                }

                generatedConfig.orePrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Terrain/Iron");
                if (generatedConfig.orePrefab == null)
                {
#if UNITY_EDITOR
                    string[] oreGuids = UnityEditor.AssetDatabase.FindAssets("Iron t:Prefab", new[] { "Assets/Prefabs/Terrain" });
                    if (oreGuids.Length > 0)
                    {
                        string orePath = UnityEditor.AssetDatabase.GUIDToAssetPath(oreGuids[0]);
                        generatedConfig.orePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(orePath);
                    }
#endif
                }

                generatedConfig.treeYOffset = 0f;
                generatedConfig.oreYOffset = 0f;

                var configField = typeof(RTSokak.Resources.ResourceManager).GetField("config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (configField != null)
                    configField.SetValue(resourceManager, generatedConfig);

                if (skipGenerate)
                    resourceManager.SetSkipAutoGenerate(true);

                NetworkServer.Spawn(rmObj);
            }
            else if (skipGenerate)
            {
                resourceManager.SetSkipAutoGenerate(true);
            }
        }

        void CreateTerrainIfNeeded()
        {
            GameObject existingPlane = GameObject.Find("Plane");
            GameObject existingTerrain = GameObject.Find("Terrain");

            if (existingPlane != null || existingTerrain != null)
                return;

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Terrain";
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(10, 1, 10);

            Material terrainMat = new Material(Shader.Find("Standard"));
            terrainMat.color = Color.green;
            plane.GetComponent<Renderer>().material = terrainMat;

            if (plane.GetComponent<NetworkIdentity>() == null)
                plane.AddComponent<NetworkIdentity>();

            if (NetworkServer.active)
                NetworkServer.Spawn(plane);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            ForceRunInBackground();
            if (!hasFocus)
                EnsureBackgroundSimulationAlive();
        }

        void OnApplicationPause(bool paused)
        {
            ForceRunInBackground();
            if (paused)
                EnsureBackgroundSimulationAlive();
        }

        void Update()
        {
            if (!Application.isFocused && (NetworkServer.active || NetworkClient.active))
            {
                ForceRunInBackground();
                if (Time.timeScale < 0.99f)
                    Time.timeScale = 1f;
            }
        }

        static void ForceRunInBackground() => Application.runInBackground = true;

        static void EnsureBackgroundSimulationAlive()
        {
            ForceRunInBackground();
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Найти существующего игрока по IP адресу (для переподключения).
        /// </summary>
        RTSPlayer FindPlayerByIp(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return null;

            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.IpAddress == ipAddress)
                    return p;
            }

            return null;
        }

        /// <summary>
        /// Переназначить владельца всех юнитов и зданий для игрока.
        /// </summary>
        [Server]
        public void ReassignOwnershipPublic(int playerIndex, int newConnectionId)
        {
            ReassignOwnership(playerIndex, newConnectionId);
        }

        [Server]
        void ReassignOwnership(int playerIndex, int newConnectionId)
        {
            ReassignOwnership(playerIndex, -1, newConnectionId);
        }

        /// <summary>
        /// Переназначить юниты/здания игроку после reconnect.
        /// oldConnectionId — connectionId ДО SetConnectionId (если известен).
        /// Матчим по ownerPlayerIndex (предпочтительно) или по oldConnectionId.
        /// </summary>
        public void ReassignOwnership(int playerIndex, int oldConnectionId, int newConnectionId)
        {
            UnityEngine.Debug.Log(
                $"[RTSNetworkManager] ReassignOwnership START: playerIndex={playerIndex}, " +
                $"oldConnId={oldConnectionId}, newConnId={newConnectionId}");

            if (!NetworkServer.connections.TryGetValue(newConnectionId, out NetworkConnectionToClient conn))
            {
                UnityEngine.Debug.LogError($"[RTSNetworkManager] Connection {newConnectionId} not found in NetworkServer.connections");
                return;
            }

            RTSPlayer existingPlayer = null;
            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.PlayerIndex == playerIndex)
                {
                    existingPlayer = p;
                    break;
                }
            }

            if (existingPlayer == null)
            {
                UnityEngine.Debug.LogWarning($"[RTSNetworkManager] Could not find player object for playerIndex={playerIndex}");
                return;
            }

            int unitsReassigned = 0;
            foreach (var unit in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (unit == null)
                    continue;

                bool matchByNick = !string.IsNullOrEmpty(existingPlayer.Nickname) &&
                                   unit.IsOwnedByNickname(existingPlayer.Nickname);
                bool matchByIndex = unit.OwnerPlayerIndex >= 0 && unit.OwnerPlayerIndex == playerIndex;
                bool matchByOldConn = oldConnectionId >= 0 && unit.OwnerConnectionId == oldConnectionId;
                // Fallback: юнит всё ещё ссылается на текущего игрока по старому/текущему conn
                bool matchByCurrentPlayerConn =
                    unit.OwnerConnectionId == existingPlayer.ConnectionId ||
                    (oldConnectionId < 0 && GetPlayerOwner(unit.OwnerConnectionId)?.PlayerIndex == playerIndex);

                if (!matchByNick && !matchByIndex && !matchByOldConn && !matchByCurrentPlayerConn)
                    continue;

                UnityEngine.Debug.Log($"[RTSNetworkManager] - REASSIGNING unit {unit.name} to newConnId={newConnectionId}");
                unit.ServerSetOwnerPlayerIndex(playerIndex);
                if (!string.IsNullOrEmpty(existingPlayer.Nickname))
                    unit.ServerSetOwnerNickname(existingPlayer.Nickname);
                unit.SetOwner(conn);
                unitsReassigned++;
            }

            int buildingsReassigned = 0;
            foreach (var building in FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (building == null)
                    continue;

                bool matchByNick = !string.IsNullOrEmpty(existingPlayer.Nickname) &&
                                   building.IsOwnedByNickname(existingPlayer.Nickname);
                bool matchByOldConn = oldConnectionId >= 0 && building.OwnerConnectionId == oldConnectionId;
                bool matchByPlayer = GetPlayerOwner(building.OwnerConnectionId)?.PlayerIndex == playerIndex;
                bool matchBySavedIndex = building.OwnerPlayerIndex >= 0 && building.OwnerPlayerIndex == playerIndex;

                if (!matchByNick && !matchByOldConn && !matchByPlayer && !matchBySavedIndex)
                    continue;

                UnityEngine.Debug.Log($"[RTSNetworkManager] - REASSIGNING building {building.name} to newConnId={newConnectionId}");
                building.ServerSetOwnerPlayerIndex(playerIndex);
                if (!string.IsNullOrEmpty(existingPlayer.Nickname))
                    building.ServerSetOwnerNickname(existingPlayer.Nickname);
                building.ServerSetOwnerConnectionId(newConnectionId);
                buildingsReassigned++;
            }

            UnityEngine.Debug.Log(
                $"[RTSNetworkManager] ReassignOwnership DONE: reassigned {unitsReassigned} units and " +
                $"{buildingsReassigned} buildings to playerIndex={playerIndex}");
        }

        /// <summary>
        /// Найти RTSPlayer по connectionId
        /// </summary>
        [Server]
        RTSPlayer GetPlayerOwner(int connectionId)
        {
            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.ConnectionId == connectionId)
                    return p;
            }
            return null;
        }
    }
}

