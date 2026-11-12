using Mirror;
using UnityEngine;
using RTSokak.Buildings;
using RTSokak.Shared;

namespace RTSokak.Network
{
    /// <summary>
    /// Игрок RTS: ресурсы, население, столица.
    /// </summary>
    public class RTSPlayer : NetworkBehaviour
    {
        public static RTSPlayer localPlayer { get; private set; }

        [Header("Starting Resources")]
        [SerializeField] private int startingWood = 100;
        [SerializeField] private int startingOre = 100;
        [SerializeField] private int startingFood = 0;

        [SyncVar]
        private int playerIndex = -1;

        [SyncVar]
        private int connectionId = -1;

        [SyncVar]
        private string ipAddress = "";  // IP адрес для переподключения

        [SyncVar(hook = nameof(OnNicknameChanged))]
        private string nickname = "";   // Ник игрока из аккаунта

        [SyncVar]
        private string accountEmail = ""; // Email аккаунта (для идентификации)

        [SyncVar]
        private Color playerColor = Color.white;

        [SyncVar(hook = nameof(OnWoodChanged))]
        private int wood;

        [SyncVar(hook = nameof(OnOreChanged))]
        private int ore;

        [SyncVar(hook = nameof(OnFoodChanged))]
        private int food;

        [SyncVar(hook = nameof(OnPopulationChanged))]
        private int currentPopulation;

        [SyncVar(hook = nameof(OnMaxPopulationChanged))]
        private int maxPopulation;

        [Header("Debug / Cheats")]
        [SyncVar(hook = nameof(OnCheatsEnabledChanged))]
        private bool cheatsEnabledOnServer;
        public bool CheatsEnabled => cheatsEnabledOnServer;

        private void OnCheatsEnabledChanged(bool oldVal, bool newVal)
        {
            if (isLocalPlayer && newVal)
            {
                RTSokak.UI.DebugCheatUI.EnsureExists();
            }
        }

        [SyncVar]
        private bool hasCapital;

        /// <summary>Поражение мэра — при следующем входе сразу UnitWaiting.</summary>
        [SyncVar]
        private bool mayorDefeated;

        [SyncVar(hook = nameof(OnPlayerRoleChanged))]
        private PlayerRole playerRole = PlayerRole.Choosing;

        /// <summary>netId юнита, которым управляет этот игрок (UnitPlaying). 0 = нет.</summary>
        [SyncVar(hook = nameof(OnControlledUnitNetIdChanged))]
        private uint controlledUnitNetId;

        // Камера — SyncVar, чтобы юнит-игрок видел FOV мэра на миникарте
        [SyncVar] private bool hasCameraView;
        [SyncVar] private Vector3 cameraPosition;
        [SyncVar] private float cameraZoom = 20f;
        [SyncVar] private float cameraYaw;

        public int PlayerIndex => playerIndex;
        public int ConnectionId => connectionId;
        public string IpAddress => ipAddress;
        public string Nickname => nickname;
        public string AccountEmail => accountEmail;
        public Color PlayerColor => playerColor;
        public int Wood => wood;
        public int Ore => ore;
        public int Food => food;
        public int CurrentPopulation => currentPopulation;
        public int MaxPopulation => maxPopulation;
        public bool HasCapital => hasCapital;
        public bool MayorDefeated => mayorDefeated;
        public PlayerRole Role => playerRole;
        public uint ControlledUnitNetId => controlledUnitNetId;
        public bool HasCameraView => hasCameraView;
        public Vector3 CameraPosition => cameraPosition;
        public float CameraZoom => cameraZoom;
        public float CameraYaw => cameraYaw;

        /// <summary>wood, ore, food, currentPop, maxPop</summary>
        public event System.Action<int, int, int, int, int> OnPlayerStatsChanged;
        public event System.Action<PlayerRole> OnRoleChanged;
        public event System.Action<uint> OnControlledUnitChanged;

        public bool IsMayorRts => playerRole == PlayerRole.MayorPlaying;
        public bool IsChoosingOrPlacing =>
            playerRole == PlayerRole.Choosing || playerRole == PlayerRole.MayorPlacing;
        public bool IsUnitSpectatorOrPlaying =>
            playerRole == PlayerRole.UnitWaiting || playerRole == PlayerRole.UnitPlaying;

        public override void OnStartServer()
        {
            base.OnStartServer();
            wood = startingWood;
            ore = startingOre;
            food = startingFood;
            maxPopulation = 0;
            // currentPopulation выставляется при спавне воркеров

            bool cheats = false;
            if (RTSNetworkManager.Singleton != null && RTSNetworkManager.Singleton.EnableCheats)
                cheats = true;
            else if (NetworkManager.singleton is RTSNetworkManager rtsNm && rtsNm.EnableCheats)
                cheats = true;
            else if (BuildingManager.Instance != null && BuildingManager.Instance.EnableCheats)
                cheats = true;
            else
            {
                var bm = Object.FindFirstObjectByType<BuildingManager>();
                if (bm != null && bm.EnableCheats)
                    cheats = true;
            }
            cheatsEnabledOnServer = cheats;
            if (cheats)
                UnityEngine.Debug.Log($"[RTSPlayer SERVER] Cheats enabled on server for conn={connectionToClient?.connectionId}");

            // Хост: сразу проставить nickname из локальной сессии (до CmdRegisterClient),
            // чтобы restore по сейву мог сопоставить игрока.
            if (connectionToClient != null && NetworkServer.localConnection == connectionToClient)
            {
                string nick = RTSokak.Shared.RTSPlayerSession.Nickname;
                string email = RTSokak.Shared.RTSPlayerSession.AccountEmail;
                if (!string.IsNullOrWhiteSpace(nick))
                    SetNickname(nick);
                if (!string.IsNullOrWhiteSpace(email))
                    SetAccountEmail(email);
            }
        }

        private void Start()
        {
            if (isLocalPlayer)
            {
                localPlayer = this;
                NotifyStatsChanged();
                RefreshAllUnitOutlines();
                if (cheatsEnabledOnServer)
                    RTSokak.UI.DebugCheatUI.EnsureExists();
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Отправить nickname и email серверу ТОЛЬКО от локального игрока
            // OnStartClient вызывается после полной инициализации Mirror, Commands точно работают
            if (isLocalPlayer)
            {
                localPlayer = this;
                string nick = RTSokak.Shared.RTSPlayerSession.Nickname;
                string email = RTSokak.Shared.RTSPlayerSession.AccountEmail;
                UnityEngine.Debug.Log($"[RTSPlayer CLIENT] OnStartClient - calling CmdRegisterClient with nickname='{nick}', email='{email}'");
                CmdRegisterClient(nick, email);
                RefreshAllUnitOutlines();
                if (cheatsEnabledOnServer)
                    RTSokak.UI.DebugCheatUI.EnsureExists();
            }
        }
        
        /// <summary>
        /// Обновить outline юнитов и зданий после установки localPlayer
        /// (здания часто спавнятся раньше игрока — иначе вражеские без красного outline).
        /// </summary>
        private void RefreshAllUnitOutlines()
        {
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null)
                    units[i].ForceUpdateOutline();
            }

            Buildings.Building[] buildings = FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] != null)
                    buildings[i].ForceUpdateOutline();
            }
        }

        public override void OnStopClient()
        {
            if (localPlayer == this)
                localPlayer = null;
            base.OnStopClient();
        }

        private void OnWoodChanged(int o, int n) => NotifyStatsChanged();
        private void OnOreChanged(int o, int n) => NotifyStatsChanged();
        private void OnFoodChanged(int o, int n) => NotifyStatsChanged();
        private void OnPopulationChanged(int o, int n) => NotifyStatsChanged();
        private void OnMaxPopulationChanged(int o, int n) => NotifyStatsChanged();

        private void NotifyStatsChanged()
        {
            OnPlayerStatsChanged?.Invoke(wood, ore, food, currentPopulation, maxPopulation);
        }

        [Server]
        public void AddResource(RTSokak.Resources.ResourceType type, int amount)
        {
            if (amount == 0) return;

            switch (type)
            {
                case RTSokak.Resources.ResourceType.Wood:
                    wood += amount;
                    break;
                case RTSokak.Resources.ResourceType.Ore:
                    ore += amount;
                    break;
                case RTSokak.Resources.ResourceType.Food:
                    food += amount;
                    break;
            }
        }

        [Server]
        public void AddWood(int amount) => wood += amount;

        [Server]
        public void AddOre(int amount) => ore += amount;

        [Server]
        public void AddFood(int amount) => food += amount;

        [Server]
        public void ServerRestoreEconomy(int woodAmount, int oreAmount, int foodAmount, int curPop, int maxPop, bool capitalFlag)
        {
            UnityEngine.Debug.Log($"[RTSPlayer] ServerRestoreEconomy: wood={woodAmount}, ore={oreAmount}, food={foodAmount}, curPop={curPop}, maxPop={maxPop}, hasCapital={capitalFlag}");
            
            wood = Mathf.Max(0, woodAmount);
            ore = Mathf.Max(0, oreAmount);
            food = Mathf.Max(0, foodAmount);
            currentPopulation = Mathf.Max(0, curPop);
            maxPopulation = Mathf.Max(0, maxPop);
            if (capitalFlag)
                NotifyCapitalBuilt();
            NotifyStatsChanged();
        }

        [Server]
        public bool TrySpend(int woodCost, int oreCost, int foodCost)
        {
            if (wood < woodCost || ore < oreCost || food < foodCost)
                return false;

            wood -= woodCost;
            ore -= oreCost;
            food -= foodCost;
            return true;
        }

        [Server]
        public void AddCurrentPopulation(int delta)
        {
            currentPopulation = Mathf.Max(0, currentPopulation + delta);
        }

        [Server]
        public void AddMaxPopulation(int delta)
        {
            maxPopulation = Mathf.Max(0, maxPopulation + delta);
        }

        [Server]
        public void NotifyCapitalBuilt()
        {
            hasCapital = true;
        }

        [Server]
        public void NotifyCapitalDestroyed()
        {
            hasCapital = false;
            // Столица сломана → все здания пропадают, мэр → Waiting (юниты тоже сносим,
            // чтобы можно было снова выбрать роль и основать поселение)
            if (playerRole == PlayerRole.MayorPlaying || HasSettlementForRole())
                ServerDemoteMayorToWaitingAfterWipe();
        }

        /// <summary>Полное поражение: здания + юниты, роль Waiting.</summary>
        [Server]
        public void ServerDemoteMayorToWaitingAfterWipe()
        {
            ServerReleaseOwnedUnitPlayersToWaiting();
            ServerWipeOwnedBuildings();
            ServerWipeOwnedUnits();
            currentPopulation = 0;
            maxPopulation = 0;
            hasCapital = false;
            mayorDefeated = true;
            controlledUnitNetId = 0;
            ServerSetRole(PlayerRole.UnitWaiting);
        }

        [Server]
        void ServerReleaseOwnedUnitPlayersToWaiting()
        {
            if (string.IsNullOrEmpty(nickname))
                return;
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                RTSUnit u = units[i];
                if (u == null || !u.IsPlayerControlled || !u.IsOwnedByNickname(nickname))
                    continue;
                RTSPlayer ctrl = u.FindControllingPlayer();
                u.ServerClearPlayerController();
                if (ctrl != null)
                {
                    ctrl.controlledUnitNetId = 0;
                    ctrl.ServerSetRole(PlayerRole.UnitWaiting);
                }
            }
        }

        [Server]
        void ServerWipeOwnedUnits()
        {
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                RTSUnit u = units[i];
                if (u == null) continue;
                bool mine = (!string.IsNullOrEmpty(nickname) && u.IsOwnedByNickname(nickname))
                            || u.OwnerConnectionId == connectionId;
                if (mine)
                    NetworkServer.Destroy(u.gameObject);
            }
        }

        /// <summary>Столицу сломали, пока мэр оффлайн — снос поселения + флаг в ActiveSaveData.</summary>
        [Server]
        public static void ServerOfflineMayorDefeat(string nick, int ownerConnId)
        {
            // Снести здания
            Building[] buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                Building b = buildings[i];
                if (b == null) continue;
                bool mine = (!string.IsNullOrEmpty(nick) && b.IsOwnedByNickname(nick))
                            || (ownerConnId >= 0 && b.OwnerConnectionId == ownerConnId);
                if (mine)
                    NetworkServer.Destroy(b.gameObject);
            }

            // Снести юнитов (и отпустить unit-players)
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                RTSUnit u = units[i];
                if (u == null) continue;
                bool mine = (!string.IsNullOrEmpty(nick) && u.IsOwnedByNickname(nick))
                            || (ownerConnId >= 0 && u.OwnerConnectionId == ownerConnId);
                if (!mine) continue;
                if (u.IsPlayerControlled)
                {
                    RTSPlayer ctrl = u.FindControllingPlayer();
                    u.ServerClearPlayerController();
                    if (ctrl != null)
                    {
                        ctrl.controlledUnitNetId = 0;
                        ctrl.ServerSetRole(PlayerRole.UnitWaiting);
                    }
                }
                NetworkServer.Destroy(u.gameObject);
            }

            var data = WorldSession.ActiveSaveData;
            if (data?.players == null || string.IsNullOrEmpty(nick))
                return;
            for (int i = 0; i < data.players.Count; i++)
            {
                PlayerSaveData p = data.players[i];
                if (p == null || string.IsNullOrEmpty(p.nickname))
                    continue;
                if (!string.Equals(p.nickname, nick, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                p.hasCapital = false;
                p.mayorDefeated = true;
                p.currentPopulation = 0;
                p.maxPopulation = 0;
                break;
            }
        }

        [Server]
        public bool CanBuild(BuildingType type, BuildingDefinition def, out string reason)
        {
            reason = null;
            if (def == null || def.prefab == null)
            {
                reason = "Building definition missing";
                return false;
            }

            if (type == BuildingType.Capital)
            {
                if (HasCapitalBuilding())
                {
                    reason = "Capital already exists";
                    return false;
                }
            }
            else
            {
                if (!hasCapital)
                {
                    reason = "Build Capital first";
                    return false;
                }
            }

            if (wood < def.woodCost || ore < def.oreCost || food < def.foodCost)
            {
                reason = "Not enough resources";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Есть ли столица (даже строящаяся) — для лимита «только одна».
        /// </summary>
        public bool HasCapitalBuilding()
        {
            return FindCapital() != null;
        }

        /// <summary>
        /// Находит столицу этого игрока (построенную или строящуюся).
        /// </summary>
        public Building FindCapital()
        {
            Building[] all = FindObjectsByType<Building>(FindObjectsSortMode.None);
            Building best = null;
            foreach (var b in all)
            {
                if (b == null || b.Type != BuildingType.Capital)
                    continue;
                bool mine = (!string.IsNullOrEmpty(nickname) && b.IsOwnedByNickname(nickname))
                            || b.OwnerConnectionId == connectionId;
                if (!mine)
                    continue;
                if (b.IsComplete)
                    return b;
                best = b;
            }

            return best;
        }

        /// <summary>
        /// Ближайшая завершённая точка сдачи ресурсов: столица или склад.
        /// </summary>
        public Building FindNearestDepositBuilding(Vector3 fromWorld)
        {
            Building[] all = FindObjectsByType<Building>(FindObjectsSortMode.None);
            Building best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < all.Length; i++)
            {
                Building b = all[i];
                if (b == null || b.OwnerConnectionId != connectionId)
                    continue;
                if (!b.IsComplete)
                    continue;
                if (b.Type != BuildingType.Capital && b.Type != BuildingType.Warehouse)
                    continue;

                Vector3 p = b.FinalPosition;
                float dx = fromWorld.x - p.x;
                float dz = fromWorld.z - p.z;
                float d = dx * dx + dz * dz;
                if (d >= bestDist)
                    continue;

                bestDist = d;
                best = b;
            }

            return best;
        }

        [Server]
        public void SetPlayerIndex(int index)
        {
            playerIndex = index;
            playerColor = GetPlayerColor(index);
        }

        [Server]
        public void SetConnectionId(int connId)
        {
            connectionId = connId;
        }

        [Server]
        public void SetIpAddress(string ip)
        {
            ipAddress = ip ?? "";
        }

        [Server]
        public void SetNickname(string nick)
        {
            nickname = nick ?? "";
        }

        void OnNicknameChanged(string oldNick, string newNick)
        {
            if (isLocalPlayer)
                RefreshAllUnitOutlines();
        }

        /// <summary>
        /// Проставить ник только юнитам без ника, которые уже привязаны к этому connection.
        /// Не перезаписывает чужие ники (владение строго по нику).
        /// </summary>
        [Server]
        public void ServerStampOwnerNicknameOnOwnedObjects()
        {
            if (string.IsNullOrEmpty(nickname))
                return;

            foreach (var unit in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (unit == null)
                    continue;
                // Уже с нашим ником — обновить index; с чужим ником — не трогать
                if (unit.IsOwnedByNickname(nickname))
                {
                    if (playerIndex >= 0)
                        unit.ServerSetOwnerPlayerIndex(playerIndex);
                    continue;
                }
                // Пустой ник + наш connection — это свежие стартовые воркеры
                if (string.IsNullOrEmpty(unit.OwnerNickname) && unit.OwnerConnectionId == connectionId)
                {
                    unit.ServerSetOwnerNickname(nickname);
                    if (playerIndex >= 0)
                        unit.ServerSetOwnerPlayerIndex(playerIndex);
                }
            }

            foreach (var building in FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (building == null)
                    continue;
                if (building.IsOwnedByNickname(nickname))
                {
                    if (playerIndex >= 0)
                        building.ServerSetOwnerPlayerIndex(playerIndex);
                    continue;
                }
                if (string.IsNullOrEmpty(building.OwnerNickname) && building.OwnerConnectionId == connectionId)
                {
                    building.ServerSetOwnerNickname(nickname);
                    if (playerIndex >= 0)
                        building.ServerSetOwnerPlayerIndex(playerIndex);
                }
            }
        }

        /// <summary>
        /// После регистрации ника: экономика из сейва + authority на юниты с этим ником.
        /// </summary>
        [Server]
        public void ServerApplyNicknameOwnership(NetworkConnectionToClient conn)
        {
            if (string.IsNullOrEmpty(nickname) || conn == null)
                return;

            var data = WorldSession.ActiveSaveData;
            int owned = 0;
            PlayerSaveData eco = null;

            UnityEngine.Debug.Log($"[RTSPlayer] ServerApplyNicknameOwnership: nickname='{nickname}', " +
                $"hasActiveSaveData={data != null}, shouldRestore={WorldSession.ShouldRestoreFromSave}, " +
                $"playerCount={data?.players?.Count ?? 0}");

            if (data != null && WorldSession.ShouldRestoreFromSave)
            {
                eco = WorldSnapshotService.FindEconomyByNickname(data, nickname);
                
                UnityEngine.Debug.Log($"[RTSPlayer] Found economy for '{nickname}': " +
                    $"{(eco != null ? $"wood={eco.wood}, ore={eco.ore}, food={eco.food}" : "NULL")}");
                
                if (eco != null && eco.playerIndex >= 0)
                    SetPlayerIndex(eco.playerIndex);

                owned = WorldSnapshotService.ClaimAndRestoreByNickname(this, conn, data);
                WorldSession.MarkNicknameRestored(nickname);

                if (eco != null && eco.hasCameraView)
                    ServerApplySavedCamera(eco, conn);
            }
            else
            {
                UnityEngine.Debug.Log($"[RTSPlayer] No save data or not restoring - using default economy");
                ServerStampOwnerNicknameOnOwnedObjects();
                owned = WorldSnapshotService.CountUnitsByNickname(nickname);
            }

            WorldSnapshotService.ClaimUnitsAndBuildingsByNickname(this, conn, nickname);

            int unitCount = WorldSnapshotService.CountUnitsByNickname(nickname);
            bool hasSettlement = unitCount > 0 || HasCapitalBuilding() || hasCapital;

            if (hasSettlement)
            {
                ServerSetRole(PlayerRole.MayorPlaying);
                controlledUnitNetId = 0;
            }
            else if (eco != null && eco.mayorDefeated)
            {
                mayorDefeated = true;
                ServerSetRole(PlayerRole.UnitWaiting);
                controlledUnitNetId = 0;
                UnityEngine.Debug.Log($"[RTSPlayer] nick='{nickname}' → Role UnitWaiting (defeated mayor)");
            }
            else
            {
                // Новый игрок или unit-игрок после reconnect → выбор роли
                mayorDefeated = false;
                ServerSetRole(PlayerRole.Choosing);
                controlledUnitNetId = 0;
                UnityEngine.Debug.Log($"[RTSPlayer] nick='{nickname}' → Role Choosing (no settlement)");
            }
        }

        [Server]
        public void ServerSetRole(PlayerRole role)
        {
            playerRole = role;
        }

        void OnPlayerRoleChanged(PlayerRole oldRole, PlayerRole newRole)
        {
            OnRoleChanged?.Invoke(newRole);
            if (isLocalPlayer)
            {
                RTSokak.UI.PlayerRoleUI.NotifyLocalRoleChanged(this, newRole);
                RefreshAllUnitOutlines();
            }
        }

        void OnControlledUnitNetIdChanged(uint oldId, uint newId)
        {
            OnControlledUnitChanged?.Invoke(newId);
            if (isLocalPlayer)
            {
                RTSokak.UI.PlayerRoleUI.NotifyLocalControlledUnitChanged(this, newId);
                RefreshAllUnitOutlines();
            }
        }

        /// <summary>Клиент выбрал роль мэра → режим выбора точки поселения.</summary>
        [Command(requiresAuthority = false)]
        public void CmdChooseMayorRole()
        {
            if (playerRole != PlayerRole.Choosing && playerRole != PlayerRole.UnitWaiting)
                return;
            // Нельзя вернуться к выбору, если уже есть поселение
            if (HasSettlementForRole())
                return;

            mayorDefeated = false;
            ServerSetRole(PlayerRole.MayorPlacing);
            controlledUnitNetId = 0;
        }

        /// <summary>Клиент выбрал роль юнита → ожидание convert.</summary>
        [Command(requiresAuthority = false)]
        public void CmdChooseUnitRole()
        {
            if (playerRole != PlayerRole.Choosing && playerRole != PlayerRole.MayorPlacing)
                return;
            if (HasSettlementForRole())
                return;

            mayorDefeated = false;
            ServerSetRole(PlayerRole.UnitWaiting);
            controlledUnitNetId = 0;
        }

        /// <summary>Из MayorPlacing обратно к Choosing (только без поселения).</summary>
        [Command(requiresAuthority = false)]
        public void CmdBackToRoleChoice()
        {
            if (HasSettlementForRole())
                return;
            if (playerRole != PlayerRole.MayorPlacing && playerRole != PlayerRole.UnitWaiting)
                return;

            ServerSetRole(PlayerRole.Choosing);
            controlledUnitNetId = 0;
        }

        /// <summary>Мэр подтвердил точку поселения — спавн стартовых воркеров.</summary>
        [Command(requiresAuthority = false)]
        public void CmdConfirmSettlement(Vector3 worldPos)
        {
            if (playerRole != PlayerRole.MayorPlacing)
                return;

            // Сразу выходим из Placing — защита от двойного Enter / гонок
            if (HasSettlementForRole() || CountOwnedUnitsAny() > 0)
            {
                ServerSetRole(PlayerRole.MayorPlaying);
                return;
            }

            var manager = NetworkManager.singleton as RTSNetworkManager;
            if (manager == null || connectionToClient == null)
                return;

            ServerSetRole(PlayerRole.MayorPlaying);
            mayorDefeated = false;

            worldPos.y = 1f;
            manager.SpawnWorkersForPlayerAtPublic(connectionToClient, gameObject, worldPos);
            ServerStampOwnerNicknameOnOwnedObjects();
            UnityEngine.Debug.Log($"[RTSPlayer] Settlement confirmed for '{nickname}' at {worldPos}");
        }

        [Server]
        int CountOwnedUnitsAny()
        {
            int n = 0;
            foreach (var u in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u == null || !u.IsAlive) continue;
                if ((!string.IsNullOrEmpty(nickname) && u.IsOwnedByNickname(nickname)) ||
                    u.OwnerConnectionId == connectionId)
                    n++;
            }
            return n;
        }

        [Server]
        bool HasSettlementForRole()
        {
            if (hasCapital || HasCapitalBuilding())
                return true;
            if (!string.IsNullOrEmpty(nickname) &&
                WorldSnapshotService.CountUnitsByNickname(nickname) > 0)
                return true;
            if (CountOwnedUnitsAny() > 0)
                return true;
            return false;
        }

        /// <summary>Мэр назначает своего AI-юнита ожидающему игроку.</summary>
        [Command(requiresAuthority = false)]
        public void CmdConvertUnitToPlayer(uint unitNetId, uint targetPlayerNetId)
        {
            if (playerRole != PlayerRole.MayorPlaying)
                return;
            if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity unitId))
                return;
            if (!NetworkServer.spawned.TryGetValue(targetPlayerNetId, out NetworkIdentity playerId))
                return;

            RTSUnit unit = unitId.GetComponent<RTSUnit>();
            RTSPlayer target = playerId.GetComponent<RTSPlayer>();
            if (unit == null || !unit.IsAlive || target == null)
                return;
            if (!unit.IsOwnedByNickname(nickname))
                return;
            if (target.Role != PlayerRole.UnitWaiting)
                return;
            if (unit.IsPlayerControlled)
                return;

            unit.ServerSetPlayerController(target);
            target.controlledUnitNetId = unit.netId;
            target.ServerSetRole(PlayerRole.UnitPlaying);
        }

        /// <summary>Мэр снимает игрока с юнита → снова AI, игрок Waiting.</summary>
        [Command(requiresAuthority = false)]
        public void CmdConvertUnitToAi(uint unitNetId)
        {
            if (playerRole != PlayerRole.MayorPlaying)
                return;
            if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity unitId))
                return;

            RTSUnit unit = unitId.GetComponent<RTSUnit>();
            if (unit == null || !unit.IsOwnedByNickname(nickname))
                return;
            if (!unit.IsPlayerControlled)
                return;

            RTSPlayer ctrl = unit.FindControllingPlayer();
            unit.ServerClearPlayerController();
            if (ctrl != null)
            {
                ctrl.controlledUnitNetId = 0;
                ctrl.ServerSetRole(PlayerRole.UnitWaiting);
            }
        }

        /// <summary>Юнит-игрок умер / отключён — вернуть в Waiting.</summary>
        [Server]
        public void ServerReleaseControlledUnitToWaiting()
        {
            controlledUnitNetId = 0;
            if (playerRole == PlayerRole.UnitPlaying)
                ServerSetRole(PlayerRole.UnitWaiting);
        }

        /// <summary>Поражение мэра (нет столицы и юнитов) → Waiting spectator.</summary>
        [Server]
        public void ServerDemoteMayorToWaiting()
        {
            controlledUnitNetId = 0;
            hasCapital = false;
            ServerSetRole(PlayerRole.UnitWaiting);
        }

        /// <summary>Юнит-игрок синхронизирует позицию/yaw на сервер.</summary>
        [Command(requiresAuthority = false)]
        public void CmdUnitPlayerMove(uint unitNetId, Vector3 position, float yaw)
        {
            if (playerRole != PlayerRole.UnitPlaying || controlledUnitNetId != unitNetId)
                return;
            if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity id))
                return;

            RTSUnit unit = id.GetComponent<RTSUnit>();
            if (unit == null || unit.ControllerPlayerNetId != netId)
                return;

            unit.ServerApplyPlayerMove(position, yaw);
        }

        /// <summary>ЛКМ юнита-игрока: удар / добыча / выстрел по лучу из камеры.</summary>
        [Command(requiresAuthority = false)]
        public void CmdUnitPlayerPrimary(uint unitNetId, Vector3 rayOrigin, Vector3 rayDir)
        {
            if (playerRole != PlayerRole.UnitPlaying || controlledUnitNetId != unitNetId)
                return;
            if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity id))
                return;

            RTSUnit unit = id.GetComponent<RTSUnit>();
            if (unit == null || unit.ControllerPlayerNetId != netId)
                return;

            unit.ServerPlayerPrimaryAction(rayOrigin, rayDir);
        }

        /// <summary>Мэр даёт жёлтую подсказку юниту-игроку (не pathfinding).</summary>
        [Command(requiresAuthority = false)]
        public void CmdMayorHintUnitPlayer(uint unitNetId, Vector3 worldPoint)
        {
            if (playerRole != PlayerRole.MayorPlaying)
                return;
            if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity id))
                return;

            RTSUnit unit = id.GetComponent<RTSUnit>();
            if (unit == null || !unit.IsOwnedByNickname(nickname) || !unit.IsPlayerControlled)
                return;

            RTSPlayer ctrl = unit.FindControllingPlayer();
            TargetShowMayorHint(connectionToClient, worldPoint, playSound: false);
            if (ctrl != null && ctrl.connectionToClient != null)
                TargetShowMayorHint(ctrl.connectionToClient, worldPoint, playSound: true);
        }

        [TargetRpc]
        private void TargetShowMayorHint(NetworkConnectionToClient target, Vector3 worldPoint, bool playSound)
        {
            RTSokak.UI.MayorOrderMarker.Spawn(worldPoint);
            if (playSound)
                RTSokak.UI.MayorOrderMarker.PlayHintSound();
        }

        /// <summary>Хотбар юнита-игрока: -1 = руки пусты, иначе индекс слота.</summary>
        [Command(requiresAuthority = false)]
        public void CmdUnitPlayerSelectHotbar(int slotIndex)
        {
            if (playerRole != PlayerRole.UnitPlaying || controlledUnitNetId == 0)
                return;
            if (!NetworkServer.spawned.TryGetValue(controlledUnitNetId, out NetworkIdentity id))
                return;

            RTSUnit unit = id.GetComponent<RTSUnit>();
            if (unit == null || unit.ControllerPlayerNetId != netId)
                return;

            unit.ServerApplyHotbarSlot(slotIndex);
        }

        /// <summary>Проверка поражения мэра: нет столицы и нет юнитов.</summary>
        [Server]
        public void ServerCheckMayorDefeat()
        {
            if (playerRole != PlayerRole.MayorPlaying)
                return;

            int units = 0;
            if (!string.IsNullOrEmpty(nickname))
                units = WorldSnapshotService.CountUnitsByNickname(nickname);

            bool hasCap = HasCapitalBuilding() || hasCapital;
            if (units > 0 || hasCap)
                return;

            ServerDemoteMayorToWaitingAfterWipe();
        }

        [Server]
        public void ServerWipeOwnedBuildings()
        {
            Building[] all = FindObjectsByType<Building>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Building b = all[i];
                if (b == null) continue;
                if (!string.IsNullOrEmpty(nickname) && b.IsOwnedByNickname(nickname))
                    NetworkServer.Destroy(b.gameObject);
                else if (b.OwnerConnectionId == connectionId)
                    NetworkServer.Destroy(b.gameObject);
            }
            hasCapital = false;
        }

        /// <summary>Отключение: снять контроль с юнита / отпустить unit-players мэра.</summary>
        [Server]
        public void ServerHandleDisconnectRoles()
        {
            if (playerRole == PlayerRole.UnitPlaying && controlledUnitNetId != 0)
            {
                if (NetworkServer.spawned.TryGetValue(controlledUnitNetId, out NetworkIdentity id))
                {
                    RTSUnit unit = id.GetComponent<RTSUnit>();
                    if (unit != null)
                        unit.ServerClearPlayerController();
                }
                controlledUnitNetId = 0;
            }

            if (playerRole == PlayerRole.MayorPlaying && !string.IsNullOrEmpty(nickname))
            {
                RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
                for (int i = 0; i < units.Length; i++)
                {
                    RTSUnit u = units[i];
                    if (u == null || !u.IsPlayerControlled || !u.IsOwnedByNickname(nickname))
                        continue;
                    RTSPlayer ctrl = u.FindControllingPlayer();
                    u.ServerClearPlayerController();
                    if (ctrl != null)
                    {
                        ctrl.controlledUnitNetId = 0;
                        ctrl.ServerSetRole(PlayerRole.UnitWaiting);
                    }
                }
            }
        }

        [Server]
        public void SetAccountEmail(string email)
        {
            accountEmail = email ?? "";
        }

        [Server]
        public void ServerSetCameraView(Vector3 position, float zoom, float yaw)
        {
            hasCameraView = true;
            cameraPosition = position;
            cameraZoom = zoom;
            cameraYaw = yaw;
        }

        [Server]
        private void ServerApplySavedCamera(PlayerSaveData eco, NetworkConnectionToClient conn)
        {
            if (eco == null || !eco.hasCameraView)
                return;

            ServerSetCameraView(
                new Vector3(eco.cameraX, eco.cameraY, eco.cameraZ),
                eco.cameraZoom,
                eco.cameraYaw);

            if (conn != null)
                TargetApplyCameraView(conn, cameraPosition, cameraZoom, cameraYaw);
        }

        [Command(requiresAuthority = false)]
        public void CmdReportCameraView(Vector3 position, float zoom, float yaw)
        {
            ServerSetCameraView(position, zoom, yaw);
        }

        [TargetRpc]
        private void TargetApplyCameraView(NetworkConnectionToClient target, Vector3 position, float zoom, float yaw)
        {
            var cam = Object.FindFirstObjectByType<RTSokak.Camera.RTSCameraController>();
            if (cam != null)
                cam.ApplySavedView(position, zoom, yaw);
            else
                UnityEngine.Debug.LogWarning("[RTSPlayer] TargetApplyCameraView: no RTSCameraController");
        }

        /// <summary>
        /// Клиент отправляет свой nickname и email после подключения.
        /// Сервер проверяет нет ли уже игрока с таким nickname (переподключение).
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdRegisterClient(string clientNickname, string clientEmail, NetworkConnectionToClient sender = null)
        {
            UnityEngine.Debug.Log($"[RTSPlayer SERVER] CmdRegisterClient called: nickname='{clientNickname}', email='{clientEmail}', sender.connectionId={sender.connectionId}");
            
            if (string.IsNullOrWhiteSpace(clientNickname))
                clientNickname = "Player";

            // Проверить есть ли уже игрок с таким nickname
            RTSPlayer existingPlayer = FindPlayerByNickname(clientNickname);
            
            if (existingPlayer != null && existingPlayer != this)
            {
                // Проверяем: это переподключение или попытка занять ник
                // Переподключение = existingPlayer не имеет активного connection
                bool isReconnect = !NetworkServer.connections.ContainsKey(existingPlayer.connectionId);
                
                if (!isReconnect)
                {
                    // Это ДРУГОЙ клиент пытается подключиться с занятым ником
                    UnityEngine.Debug.LogWarning($"[RTSPlayer] Nickname '{clientNickname}' already in use by another player (connId={existingPlayer.connectionId})! Disconnecting new client.");
                    
                    // Уничтожить все юниты нового игрока
                    DestroyNewPlayerUnits(sender.connectionId);
                    
                    // Уничтожить нового игрока и отключить клиента
                    NetworkServer.RemovePlayerForConnection(sender);
                    NetworkServer.Destroy(gameObject);
                    sender.Disconnect();
                    return;
                }

                // Это ПЕРЕПОДКЛЮЧЕНИЕ отключённого клиента
                int oldConnId = existingPlayer.connectionId;
                UnityEngine.Debug.Log($"[RTSPlayer] Reconnecting player '{clientNickname}' (email={clientEmail}), playerIndex={existingPlayer.PlayerIndex}, oldConnId={oldConnId} -> newConnId={sender.connectionId}");
                
                // Уничтожить все юниты нового игрока (они были созданы в OnServerAddPlayer)
                DestroyNewPlayerUnits(sender.connectionId);
                
                // Уничтожить этот новый player object
                NetworkServer.RemovePlayerForConnection(sender);
                NetworkServer.Destroy(gameObject);
                
                // Переназначить соединение существующему игроку
                NetworkServer.AddPlayerForConnection(sender, existingPlayer.gameObject);

                // ВАЖНО: переназначить юниты/здания ПО СТАРОМУ connectionId ДО смены id игрока
                var manager = NetworkManager.singleton as RTSNetworkManager;
                if (manager != null)
                {
                    manager.ReassignOwnership(existingPlayer.PlayerIndex, oldConnId, sender.connectionId);
                }

                existingPlayer.SetConnectionId(sender.connectionId);
                existingPlayer.ServerApplyNicknameOwnership(sender);
                
                return;
            }

            // НОВОЕ ПОДКЛЮЧЕНИЕ / смена ника — владение ТОЛЬКО по нику
            UnityEngine.Debug.Log($"[RTSPlayer] Player registered as: '{clientNickname}' (email={clientEmail})");
            SetNickname(clientNickname);
            SetAccountEmail(clientEmail);
            SetIpAddress(sender.address);
            ServerApplyNicknameOwnership(sender);
        }

        /// <summary>
        /// Уничтожить все юниты принадлежащие указанному connectionId
        /// </summary>
        [Server]
        void DestroyNewPlayerUnits(int connectionId)
        {
            UnityEngine.Debug.Log($"[RTSPlayer] DestroyNewPlayerUnits: destroying units for connectionId={connectionId}");
            int destroyed = 0;
            
            // Уничтожить юниты
            foreach (var unit in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (unit != null && unit.OwnerConnectionId == connectionId)
                {
                    UnityEngine.Debug.Log($"[RTSPlayer] - Destroying unit: {unit.name}, ownerConnId={unit.OwnerConnectionId}");
                    NetworkServer.Destroy(unit.gameObject);
                    destroyed++;
                }
            }
            
            // Уничтожить здания
            foreach (var building in FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (building != null && building.OwnerConnectionId == connectionId)
                {
                    UnityEngine.Debug.Log($"[RTSPlayer] - Destroying building: {building.name}, ownerConnId={building.OwnerConnectionId}");
                    NetworkServer.Destroy(building.gameObject);
                    destroyed++;
                }
            }
            
            UnityEngine.Debug.Log($"[RTSPlayer] DestroyNewPlayerUnits: destroyed {destroyed} objects for connectionId={connectionId}");
        }

        RTSPlayer FindPlayerByNickname(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick))
            {
                UnityEngine.Debug.Log($"[RTSPlayer] FindPlayerByNickname: nickname is empty");
                return null;
            }

            UnityEngine.Debug.Log($"[RTSPlayer] FindPlayerByNickname: searching for '{nick}'");
            
            var allPlayers = FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None);
            UnityEngine.Debug.Log($"[RTSPlayer] Found {allPlayers.Length} total players on server");
            
            foreach (var p in allPlayers)
            {
                if (p != null)
                {
                    UnityEngine.Debug.Log($"[RTSPlayer] - Player: nickname='{p.Nickname}', playerIndex={p.PlayerIndex}, connId={p.connectionId}, isThis={p == this}");
                    
                    if (!string.IsNullOrEmpty(p.Nickname) && 
                        p.Nickname.Equals(nick, System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEngine.Debug.Log($"[RTSPlayer] FindPlayerByNickname: FOUND match '{p.Nickname}'");
                        return p;
                    }
                }
            }

            UnityEngine.Debug.Log($"[RTSPlayer] FindPlayerByNickname: NO match found for '{nick}'");
            return null;
        }

        private Color GetPlayerColor(int index)
        {
            Color[] colors =
            {
                Color.blue,
                Color.red,
                Color.green,
                Color.yellow,
                Color.cyan,
                Color.magenta
            };

            return colors[index % colors.Length];
        }

        /// <summary>
        /// Клиент с authority на player-объекте размещает здание и назначает worker.
        /// </summary>
        [Command]
        public void CmdPlaceBuilding(BuildingType type, Vector3 worldPos, uint workerNetId)
        {
            Worker worker = null;
            if (NetworkServer.spawned.TryGetValue(workerNetId, out NetworkIdentity workerIdentity))
                worker = workerIdentity.GetComponent<Worker>();

            if (worker == null || worker.OwnerConnectionId != connectionId)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RTSPlayer] CmdPlaceBuilding rejected: worker missing or not owned. netId={workerNetId}");
                return;
            }

            ServerPlaceBuilding(type, worldPos, worker);
        }

        [Server]
        public void ServerPlaceBuilding(BuildingType type, Vector3 worldPos, Worker worker)
        {
            if (worker == null)
                return;

            var manager = BuildingManager.Instance;
            if (manager == null)
            {
                UnityEngine.Debug.LogError("[RTSPlayer] BuildingManager missing on server");
                return;
            }

            BuildingDefinition def = manager.GetDefinition(type);
            if (!CanBuild(type, def, out string reason))
            {
                UnityEngine.Debug.LogWarning($"[RTSPlayer] Cannot build: {reason}");
                return;
            }

            if (!BuildingManager.Instance.TrySnapPlacement(worldPos, def, out Vector2Int origin, out Vector3 snappedCenter, out string placeReason))
            {
                UnityEngine.Debug.LogWarning($"[RTSPlayer] Cannot place: {placeReason}");
                return;
            }

            if (!TrySpend(def.woodCost, def.oreCost, def.foodCost))
            {
                UnityEngine.Debug.LogWarning("[RTSPlayer] TrySpend failed");
                return;
            }

            Vector3 finalPos = snappedCenter;
            finalPos.y = 0f; // Завершённые здания всегда на высоте 0

            // Префаб создаётся на финальной позиции, InitializeConstruction установит правильную начальную высоту
            GameObject obj = Instantiate(def.prefab, finalPos, Quaternion.identity);
            Building building = obj.GetComponent<Building>();
            if (building == null)
                building = obj.AddComponent<Building>();

            if (type == BuildingType.GardenBed && obj.GetComponent<GardenBed>() == null)
                obj.AddComponent<GardenBed>();

            if (type == BuildingType.Capital && obj.GetComponent<CapitalProduction>() == null)
                obj.AddComponent<CapitalProduction>();

            if (type == BuildingType.Barracks && obj.GetComponent<BarracksProduction>() == null)
                obj.AddComponent<BarracksProduction>();

            if (obj.GetComponent<NetworkIdentity>() == null)
                obj.AddComponent<NetworkIdentity>();

            NetworkServer.Spawn(obj);
            building.InitializeConstruction(
                connectionId, def, finalPos,
                def.woodCost, def.oreCost, def.foodCost,
                origin, def.Size);
            worker.ServerStartConstruction(building);

            UnityEngine.Debug.Log($"[RTSPlayer] Placed {type} at {finalPos} grid={origin} size={def.Size}, worker={worker.name}");
        }

        #region Debug / Cheats Commands

        [Command(requiresAuthority = false)]
        public void CmdCheatAddResources(int woodDelta, int oreDelta, int foodDelta, NetworkConnectionToClient sender = null)
        {
            if (!CheatsEnabled || playerRole != PlayerRole.MayorPlaying) return;
            if (sender != null && connectionToClient != null && sender != connectionToClient) return;

            AddWood(woodDelta);
            AddOre(oreDelta);
            AddFood(foodDelta);
            UnityEngine.Debug.Log($"[RTSPlayer] Cheat added resources (+{woodDelta} wood, +{oreDelta} ore, +{foodDelta} food) for conn={connectionToClient?.connectionId}");
        }

        [Command(requiresAuthority = false)]
        public void CmdCheatSpawnUnit(string unitKind, Vector3 worldPos, NetworkConnectionToClient sender = null)
        {
            if (!CheatsEnabled || playerRole != PlayerRole.MayorPlaying) return;
            if (sender != null && connectionToClient != null && sender != connectionToClient) return;

            var nm = NetworkManager.singleton as RTSNetworkManager;
            if (nm == null) nm = RTSNetworkManager.Singleton;
            if (nm == null)
            {
                UnityEngine.Debug.LogError("[RTSPlayer] RTSNetworkManager not found");
                return;
            }

            GameObject prefab = null;
            if (string.Equals(unitKind, "Worker", System.StringComparison.OrdinalIgnoreCase))
                prefab = nm.WorkerPrefab;
            else if (string.Equals(unitKind, "Warrior", System.StringComparison.OrdinalIgnoreCase))
                prefab = nm.WarriorPrefab;
            else if (string.Equals(unitKind, "Archer", System.StringComparison.OrdinalIgnoreCase))
                prefab = nm.ArcherPrefab;

            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning($"[RTSPlayer] CmdCheatSpawnUnit: Unknown unit kind or null prefab '{unitKind}'");
                return;
            }

            Vector3 spawnPos = worldPos;
            if (UnityEngine.AI.NavMesh.SamplePosition(worldPos, out UnityEngine.AI.NavMeshHit navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPos = navHit.position;
            }

            GameObject unitObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(unitObj);

            RTSUnit unit = unitObj.GetComponent<RTSUnit>();
            if (unit != null)
            {
                NetworkConnectionToClient conn = connectionToClient ?? NetworkServer.localConnection;
                if (conn != null)
                    unit.SetOwner(conn);
                if (playerIndex >= 0) unit.ServerSetOwnerPlayerIndex(playerIndex);
                if (!string.IsNullOrEmpty(nickname)) unit.ServerSetOwnerNickname(nickname);
                AddCurrentPopulation(1);
            }

            UnityEngine.Debug.Log($"[RTSPlayer] Cheat spawned {unitKind} at {spawnPos} for conn={connectionToClient?.connectionId}");
        }

        [Command(requiresAuthority = false)]
        public void CmdCheatPlaceBuildingInstant(BuildingType type, Vector3 worldPos, NetworkConnectionToClient sender = null)
        {
            if (!CheatsEnabled || playerRole != PlayerRole.MayorPlaying) return;
            if (sender != null && connectionToClient != null && sender != connectionToClient) return;

            var manager = BuildingManager.Instance;
            if (manager == null)
            {
                UnityEngine.Debug.LogError("[RTSPlayer] BuildingManager missing on server");
                return;
            }

            BuildingDefinition def = manager.GetDefinition(type);
            if (def == null || def.prefab == null)
            {
                UnityEngine.Debug.LogWarning($"[RTSPlayer] CmdCheatPlaceBuildingInstant: No definition/prefab for {type}");
                return;
            }

            if (type == BuildingType.Capital && HasCapitalBuilding())
            {
                UnityEngine.Debug.LogWarning("[RTSPlayer] CmdCheatPlaceBuildingInstant: Capital already exists");
                return;
            }

            if (!manager.TrySnapPlacement(worldPos, def, out Vector2Int origin, out Vector3 snappedCenter, out string placeReason))
            {
                UnityEngine.Debug.LogWarning($"[RTSPlayer] CmdCheatPlaceBuildingInstant: Cannot place: {placeReason}");
                return;
            }

            Vector3 finalPos = snappedCenter;
            finalPos.y = 0f;

            GameObject obj = Instantiate(def.prefab, finalPos, Quaternion.identity);
            Building building = obj.GetComponent<Building>();
            if (building == null)
                building = obj.AddComponent<Building>();

            if (type == BuildingType.GardenBed && obj.GetComponent<GardenBed>() == null)
                obj.AddComponent<GardenBed>();

            if (type == BuildingType.Capital && obj.GetComponent<CapitalProduction>() == null)
                obj.AddComponent<CapitalProduction>();

            if (type == BuildingType.Barracks && obj.GetComponent<BarracksProduction>() == null)
                obj.AddComponent<BarracksProduction>();

            if (obj.GetComponent<NetworkIdentity>() == null)
                obj.AddComponent<NetworkIdentity>();

            NetworkServer.Spawn(obj);

            building.ServerInitializeInstantComplete(
                connectionId, playerIndex, nickname,
                def, finalPos, origin, def.Size);

            var dynNav = Object.FindFirstObjectByType<RTSokak.Navigation.DynamicNavMeshManager>();
            if (dynNav != null)
                dynNav.MarkDirty();

            UnityEngine.Debug.Log($"[RTSPlayer] Cheat instant placed {type} at {finalPos} grid={origin} size={def.Size}");
        }

        #endregion
    }
}
