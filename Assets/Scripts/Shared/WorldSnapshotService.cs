using System.Collections.Generic;
using Mirror;
using RTSokak.Buildings;
using RTSokak.Network;
using RTSokak.Resources;
using UnityEngine;

namespace RTSokak.Shared
{
    /// <summary>Снимает / восстанавливает полный снапшот мира (только на сервере).</summary>
    public static class WorldSnapshotService
    {
        public static RTSWorldSaveData Capture()
        {
            var data = new RTSWorldSaveData();

            var rm = Object.FindFirstObjectByType<ResourceManager>();
            if (rm != null)
                data.worldSeed = rm.WorldSeed;

            // Сохранить время суток
            var dayNight = Object.FindFirstObjectByType<RTSokak.Environment.DayNightCycle>();
            if (dayNight != null)
            {
                data.timeOfDay = dayNight.GetTimeOfDay();
                UnityEngine.Debug.Log($"[WorldSnapshot] Saving time of day: {data.timeOfDay:F3} ({dayNight.GetCurrentHour()}:00)");
            }

            var allPlayers = Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None);
            UnityEngine.Debug.Log($"[WorldSnapshot] Capture: found {allPlayers.Length} RTSPlayer objects");

            // Дедуп по playerIndex: при reconnect могли остаться лишние объекты
            var playersByIndex = new Dictionary<int, RTSPlayer>();
            foreach (var p in allPlayers)
            {
                if (p == null || p.PlayerIndex < 0)
                    continue;

                if (!playersByIndex.TryGetValue(p.PlayerIndex, out RTSPlayer existing) || existing == null)
                {
                    playersByIndex[p.PlayerIndex] = p;
                    continue;
                }

                // Оставляем игрока с большим суммарным «весом» ресурсов/населения
                int existingScore = existing.Wood + existing.Ore + existing.Food + existing.MaxPopulation;
                int score = p.Wood + p.Ore + p.Food + p.MaxPopulation;
                if (score > existingScore ||
                    (!string.IsNullOrEmpty(p.Nickname) && string.IsNullOrEmpty(existing.Nickname)))
                    playersByIndex[p.PlayerIndex] = p;
            }

            // Сохранить живых игроков
            foreach (var kv in playersByIndex)
            {
                var p = kv.Value;
                UnityEngine.Debug.Log($"[WorldSnapshot] Saving player {p.PlayerIndex}: wood={p.Wood}, ore={p.Ore}, food={p.Food}, pop={p.CurrentPopulation}/{p.MaxPopulation}");
                
                var entry = new PlayerSaveData
                {
                    playerIndex = p.PlayerIndex,
                    ipAddress = p.IpAddress,
                    nickname = p.Nickname,
                    accountEmail = p.AccountEmail,
                    wood = p.Wood,
                    ore = p.Ore,
                    food = p.Food,
                    currentPopulation = p.CurrentPopulation,
                    maxPopulation = p.MaxPopulation,
                    hasCapital = p.HasCapital,
                    mayorDefeated = p.MayorDefeated
                };
                FillCameraIntoSave(p, entry);
                data.players.Add(entry);
            }
            
            // MERGE: добавить отключённых игроков из предыдущего сейва
            var previousSave = WorldSession.ActiveSaveData;
            if (previousSave != null && previousSave.players != null)
            {
                foreach (var oldPlayer in previousSave.players)
                {
                    if (oldPlayer == null || string.IsNullOrEmpty(oldPlayer.nickname))
                        continue;
                    
                    // Пропустить если уже есть живой игрок с таким же playerIndex
                    if (playersByIndex.ContainsKey(oldPlayer.playerIndex))
                        continue;
                    
                    // Пропустить если уже есть игрок с таким же nickname (но другим playerIndex)
                    bool nicknameTaken = false;
                    foreach (var existing in data.players)
                    {
                        if (!string.IsNullOrEmpty(existing.nickname) && 
                            existing.nickname.Equals(oldPlayer.nickname, System.StringComparison.OrdinalIgnoreCase))
                        {
                            nicknameTaken = true;
                            break;
                        }
                    }
                    
                    if (!nicknameTaken)
                    {
                        UnityEngine.Debug.Log($"[WorldSnapshot] Merging disconnected player {oldPlayer.playerIndex} ('{oldPlayer.nickname}'): " +
                            $"wood={oldPlayer.wood}, ore={oldPlayer.ore}, food={oldPlayer.food}");
                        data.players.Add(oldPlayer);
                    }
                }
            }

            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                int buildingOwnerIndex = b.OwnerPlayerIndex >= 0
                    ? b.OwnerPlayerIndex
                    : FindPlayerIndex(b.OwnerConnectionId);
                string buildingOwnerNick = b.OwnerNickname;
                if (string.IsNullOrEmpty(buildingOwnerNick) && buildingOwnerIndex >= 0)
                {
                    foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                    {
                        if (p != null && p.PlayerIndex == buildingOwnerIndex)
                        {
                            buildingOwnerNick = p.Nickname;
                            break;
                        }
                    }
                }
                if (buildingOwnerIndex < 0 && string.IsNullOrEmpty(buildingOwnerNick))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[WorldSnapshot] Skipping building {b.name}: unknown owner (connId={b.OwnerConnectionId})");
                    continue;
                }

                var entry = new BuildingSaveData
                {
                    buildingType = (int)b.Type,
                    ownerPlayerIndex = buildingOwnerIndex,
                    ownerNickname = buildingOwnerNick ?? "",
                    finalPosition = SaveRounding.R2(b.FinalPosition),
                    buildProgress = SaveRounding.R2(b.BuildProgress),
                    isComplete = b.IsComplete,
                    buryDepth = 3f,
                    buildDuration = 10f,
                    spentWood = b.SpentWood,
                    spentOre = b.SpentOre,
                    spentFood = b.SpentFood,
                    populationProvided = 0,
                    populationGranted = b.IsComplete,
                    gridOriginX = b.GridOrigin.x,
                    gridOriginZ = b.GridOrigin.y,
                    gridSizeX = b.GridSize.x,
                    gridSizeZ = b.GridSize.y
                };

                var capital = b.GetComponent<CapitalProduction>();
                if (capital != null)
                {
                    entry.capitalQueueCount = capital.QueueCount;
                    entry.capitalActiveProgress = SaveRounding.R2(capital.ActiveProgress);
                }

                var barracks = b.GetComponent<BarracksProduction>();
                if (barracks != null)
                {
                    entry.barracksQueueCount = barracks.QueueCount;
                    entry.barracksActiveProgress = SaveRounding.R2(barracks.ActiveProgress);
                }

                var def = BuildingManager.Instance != null
                    ? BuildingManager.Instance.GetDefinition(b.Type)
                    : null;
                if (def != null)
                {
                    entry.buryDepth = SaveRounding.R2(def.buryDepth);
                    entry.buildDuration = SaveRounding.R2(def.buildDuration);
                    entry.populationProvided = def.populationProvided;
                }

                data.buildings.Add(entry);
            }

            // Сохранение состояния грядок (GardenBed)
            foreach (var garden in Object.FindObjectsByType<GardenBed>(FindObjectsSortMode.None))
            {
                if (garden == null) continue;
                
                var building = garden.GetComponent<Building>();
                if (building == null || !building.IsComplete) continue;

                var gardenData = new GardenBedSaveData
                {
                    buildingNetIdHint = (int)garden.netId,
                    buildingPosition = SaveRounding.R2(building.FinalPosition),
                    plotStatesJson = garden.ServerGetPlotStatesJson()
                };
                
                data.gardenBeds.Add(gardenData);
            }

            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u == null || !u.IsAlive) continue;
                
                string kind = "Warrior"; // default
                if (u is Worker)
                    kind = "Worker";
                else if (u is Archer)
                    kind = "Archer";
                
                int unitOwnerIndex = u.OwnerPlayerIndex >= 0
                    ? u.OwnerPlayerIndex
                    : FindPlayerIndex(u.OwnerConnectionId);
                string unitOwnerNick = u.OwnerNickname;
                if (string.IsNullOrEmpty(unitOwnerNick) && unitOwnerIndex >= 0)
                {
                    foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                    {
                        if (p != null && p.PlayerIndex == unitOwnerIndex)
                        {
                            unitOwnerNick = p.Nickname;
                            break;
                        }
                    }
                }

                // Юниты с ником сохраняем даже без playerIndex (оффлайн-владелец)
                if (unitOwnerIndex < 0 && string.IsNullOrEmpty(unitOwnerNick))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[WorldSnapshot] Skipping unit {u.name}: unknown owner (connId={u.OwnerConnectionId})");
                    continue;
                }

                var entry = new UnitSaveData
                {
                    unitKind = kind,
                    ownerPlayerIndex = unitOwnerIndex,
                    ownerNickname = unitOwnerNick ?? "",
                    position = SaveRounding.R2(u.transform.position),
                    eulerAngles = SaveRounding.R2(u.transform.eulerAngles),
                    currentHealth = u.CurrentHealth,
                    maxHealth = u.MaxHealth
                };

                u.ServerWriteSaveData(entry);
                data.units.Add(entry);
            }

            foreach (var r in Object.FindObjectsByType<Resource>(FindObjectsSortMode.None))
            {
                if (r == null || r.IsHarvested) continue;
                data.resources.Add(new ResourceSaveData
                {
                    resourceType = (int)r.Type,
                    gridX = r.gridPosition.x,
                    gridZ = r.gridPosition.y,
                    amount = r.Amount,
                    position = SaveRounding.R2(r.transform.position),
                    eulerAngles = SaveRounding.R2(r.transform.eulerAngles)
                });
            }

            UnityEngine.Debug.Log($"[WorldSnapshot] Capture complete: players={data.players.Count}, buildings={data.buildings.Count}, units={data.units.Count}, resources={data.resources.Count}");
            return data;
        }

        public static bool TrySaveActiveWorld(out string error)
        {
            error = null;
            UnityEngine.Debug.Log($"[WorldSnapshot] TrySaveActiveWorld: world='{WorldSession.ActiveWorldName}'");
            
            if (!NetworkServer.active)
            {
                error = "Only the host can save.";
                return false;
            }

            if (!WorldSession.HasActiveWorld)
            {
                error = "No active world.";
                return false;
            }

            RTSWorldSaveData snapshot = Capture();
            bool success = WorldSaveService.TrySaveWorld(WorldSession.ActiveWorldName, snapshot, out error);
            
            if (success)
            {
                // Обновить ActiveSaveData чтобы следующий подключающийся клиент получил свежие данные
                WorldSession.UpdateActiveSaveData(snapshot);
                UnityEngine.Debug.Log($"[WorldSnapshot] ActiveSaveData updated after save: players={snapshot.players?.Count ?? 0}");
            }
            
            return success;
        }

        public static void RestoreWorldGeometry(RTSWorldSaveData data, ResourceManager resourceManager)
        {
            if (data == null || !NetworkServer.active)
                return;

            if (resourceManager == null)
                resourceManager = Object.FindFirstObjectByType<ResourceManager>();

            int spawnedResources = 0;
            if (resourceManager != null)
            {
                resourceManager.SetSkipAutoGenerate(true);
                if (data.worldSeed != 0)
                    resourceManager.ServerSetWorldSeed(data.worldSeed);

                if (data.resources != null)
                {
                    for (int i = 0; i < data.resources.Count; i++)
                    {
                        if (resourceManager.ServerSpawnResourceFromSave(data.resources[i]))
                            spawnedResources++;
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[WorldSnapshot] ResourceManager missing — cannot restore resources.");
            }

            int spawnedBuildings = 0;
            if (data.buildings != null)
            {
                for (int i = 0; i < data.buildings.Count; i++)
                {
                    BuildingSaveData bSave = data.buildings[i];
                    if (bSave == null) continue;
                    // Не отдаём чужие здания хосту: conn только если владелец онлайн
                    int ownerConn = ResolveConnectionIdByNickname(bSave.ownerNickname, bSave.ownerPlayerIndex);
                    if (RestoreBuilding(bSave, ownerConn, bSave.ownerPlayerIndex))
                        spawnedBuildings++;
                }
            }

            // ВСЕ юниты всех игроков — сразу при загрузке мира (даже если клиент оффлайн)
            int spawnedUnits = RestoreAllUnits(data, null, null, null);

            // Восстановить состояние грядок после создания всех зданий
            RestoreGardenBeds(data);

            // Восстановить время суток
            var dayNight = Object.FindFirstObjectByType<RTSokak.Environment.DayNightCycle>();
            if (dayNight != null && data.timeOfDay >= 0f && data.timeOfDay <= 1f)
            {
                dayNight.SetTimeOfDay(data.timeOfDay);
                UnityEngine.Debug.Log($"[WorldSnapshot] Restored time of day: {data.timeOfDay:F3} ({dayNight.GetCurrentHour()}:00)");
            }

            WorldSession.WorldGeometryRestored = true;
            UnityEngine.Debug.Log(
                $"[WorldSnapshot] Restored geometry: resources={spawnedResources}/{data.resources?.Count ?? 0}, " +
                $"buildings={spawnedBuildings}/{data.buildings?.Count ?? 0}, units={spawnedUnits}/{data.units?.Count ?? 0}");
        }

        /// <summary>
        /// Спавн всех юнитов из сейва. prefab'ы берутся из RTSNetworkManager если не переданы.
        /// </summary>
        public static int RestoreAllUnits(RTSWorldSaveData data,
            GameObject workerPrefab, GameObject warriorPrefab, GameObject archerPrefab)
        {
            if (data?.units == null || !NetworkServer.active)
                return 0;

            var nm = NetworkManager.singleton as RTSNetworkManager;
            if (workerPrefab == null) workerPrefab = nm != null ? nm.WorkerPrefab : null;
            if (warriorPrefab == null) warriorPrefab = nm != null ? nm.WarriorPrefab : null;
            if (archerPrefab == null) archerPrefab = nm != null ? nm.ArcherPrefab : null;

            int spawned = 0;
            var spawnedUnits = new List<(RTSUnit unit, UnitSaveData save)>();

            for (int i = 0; i < data.units.Count; i++)
            {
                UnitSaveData unitSave = data.units[i];
                if (unitSave == null) continue;

                // Уже есть юнит на этой позиции с тем же ником — не дублируем
                if (FindExistingUnit(unitSave) != null)
                    continue;

                if (TrySpawnUnitOffline(unitSave, workerPrefab, warriorPrefab, archerPrefab, out RTSUnit unit))
                {
                    spawned++;
                    spawnedUnits.Add((unit, unitSave));
                }
            }

            for (int i = 0; i < spawnedUnits.Count; i++)
            {
                var (unit, unitSave) = spawnedUnits[i];
                if (unit != null)
                    unit.ServerApplySaveData(unitSave);
            }

            return spawned;
        }

        static RTSUnit FindExistingUnit(UnitSaveData save)
        {
            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u == null || !u.IsAlive) continue;
                if (Vector3.Distance(u.transform.position, save.position) > 0.75f)
                    continue;
                if (!string.IsNullOrEmpty(save.ownerNickname) &&
                    !string.IsNullOrEmpty(u.OwnerNickname) &&
                    !string.Equals(save.ownerNickname, u.OwnerNickname, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                return u;
            }
            return null;
        }

        /// <summary>
        /// Восстановить экономику и забрать authority на юниты/здания СТРОГО по нику.
        /// Юниты уже должны быть в мире (RestoreAllUnits).
        /// </summary>
        public static int ClaimAndRestoreByNickname(RTSPlayer player, NetworkConnectionToClient conn,
            RTSWorldSaveData data)
        {
            if (player == null || data == null || !NetworkServer.active)
                return 0;

            string nick = player.Nickname;
            if (string.IsNullOrWhiteSpace(nick))
                return 0;

            PlayerSaveData economy = FindEconomyByNickname(data, nick);
            if (economy == null)
                economy = FindEconomyByPlayerIndex(data, player.PlayerIndex);

            int ownedPop = ClaimUnitsAndBuildingsByNickname(player, conn, nick);

            if (economy != null)
            {
                player.ServerRestoreEconomy(
                    economy.wood, economy.ore, economy.food,
                    ownedPop > 0 ? ownedPop : economy.currentPopulation,
                    economy.maxPopulation, economy.hasCapital);
                UnityEngine.Debug.Log(
                    $"[WorldSnapshot] ClaimAndRestore nick='{nick}': wood={economy.wood}, ore={economy.ore}, " +
                    $"food={economy.food}, units={ownedPop}");
            }
            else if (ownedPop > 0)
            {
                player.AddCurrentPopulation(ownedPop);
                UnityEngine.Debug.Log(
                    $"[WorldSnapshot] ClaimAndRestore nick='{nick}': no economy in save, units={ownedPop}");
            }

            return ownedPop;
        }

        public static PlayerSaveData FindEconomyByNickname(RTSWorldSaveData data, string nick)
        {
            if (data?.players == null || string.IsNullOrWhiteSpace(nick))
                return null;

            for (int i = 0; i < data.players.Count; i++)
            {
                var p = data.players[i];
                if (p == null || string.IsNullOrEmpty(p.nickname))
                    continue;
                if (p.nickname.Equals(nick, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        static PlayerSaveData FindEconomyByPlayerIndex(RTSWorldSaveData data, int playerIndex)
        {
            if (data?.players == null || playerIndex < 0)
                return null;
            for (int i = 0; i < data.players.Count; i++)
            {
                if (data.players[i] != null && data.players[i].playerIndex == playerIndex)
                    return data.players[i];
            }
            return null;
        }

        /// <summary>Назначить connection/authority всем юнитам и зданиям с данным ником.</summary>
        public static int ClaimUnitsAndBuildingsByNickname(RTSPlayer player, NetworkConnectionToClient conn, string nick)
        {
            if (player == null || string.IsNullOrWhiteSpace(nick))
                return 0;

            int ownedPop = 0;
            foreach (var unit in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                bool matchNick = unit.IsOwnedByNickname(nick);
                // Legacy-сейвы без ника: матч по playerIndex, затем проставить ник
                bool matchIndex = !matchNick &&
                                  string.IsNullOrEmpty(unit.OwnerNickname) &&
                                  player.PlayerIndex >= 0 &&
                                  unit.OwnerPlayerIndex == player.PlayerIndex;
                if (!matchNick && !matchIndex)
                    continue;

                if (player.PlayerIndex >= 0)
                    unit.ServerSetOwnerPlayerIndex(player.PlayerIndex);
                unit.ServerSetOwnerNickname(nick);
                if (conn != null)
                    unit.SetOwner(conn);
                ownedPop++;
            }

            foreach (var building in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
            {
                if (building == null)
                    continue;
                bool matchNick = building.IsOwnedByNickname(nick);
                bool matchIndex = !matchNick &&
                                  string.IsNullOrEmpty(building.OwnerNickname) &&
                                  player.PlayerIndex >= 0 &&
                                  building.OwnerPlayerIndex == player.PlayerIndex;
                if (!matchNick && !matchIndex)
                    continue;

                if (player.PlayerIndex >= 0)
                    building.ServerSetOwnerPlayerIndex(player.PlayerIndex);
                building.ServerSetOwnerNickname(nick);
                if (conn != null)
                    building.ServerSetOwnerConnectionId(conn.connectionId);
            }

            return ownedPop;
        }

        /// <summary>Сколько юнитов в мире принадлежат нику.</summary>
        public static int CountUnitsByNickname(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick))
                return 0;
            int count = 0;
            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u != null && u.IsAlive && u.IsOwnedByNickname(nick))
                    count++;
            }
            return count;
        }

        /// <summary>Есть ли в сейве запись игрока / юнитов с этим ником.</summary>
        public static bool SaveHasNickname(RTSWorldSaveData data, string nick)
        {
            if (data == null || string.IsNullOrWhiteSpace(nick))
                return false;
            if (FindEconomyByNickname(data, nick) != null)
                return true;
            if (data.units != null)
            {
                for (int i = 0; i < data.units.Count; i++)
                {
                    var u = data.units[i];
                    if (u != null && !string.IsNullOrEmpty(u.ownerNickname) &&
                        u.ownerNickname.Equals(nick, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Legacy API — делегирует в ClaimAndRestoreByNickname.</summary>
        public static void RestorePlayerState(RTSPlayer player, int playerIndex, NetworkConnectionToClient conn,
            RTSWorldSaveData data, GameObject workerPrefab, GameObject warriorPrefab, GameObject archerPrefab)
        {
            if (player == null || data == null)
                return;

            // Доспавн юнитов на случай если geometry restore ещё не сделал их
            RestoreAllUnits(data, workerPrefab, warriorPrefab, archerPrefab);
            ClaimAndRestoreByNickname(player, conn, data);
        }

        static bool TrySpawnUnitOffline(UnitSaveData save,
            GameObject workerPrefab, GameObject warriorPrefab, GameObject archerPrefab, out RTSUnit unit)
        {
            unit = null;

            GameObject prefab = null;
            if (string.IsNullOrEmpty(save.unitKind) || save.unitKind.Equals("Worker", System.StringComparison.OrdinalIgnoreCase))
                prefab = workerPrefab;
            else if (save.unitKind.Equals("Archer", System.StringComparison.OrdinalIgnoreCase))
                prefab = archerPrefab;
            else
                prefab = warriorPrefab;

            if (prefab == null)
            {
                UnityEngine.Debug.LogError(
                    $"[WorldSnapshot] Cannot restore {save.unitKind}: prefab null");
                return false;
            }

            Vector3 pos = save.position;
            if (pos.y < 0.1f)
                pos.y = 1f;

            Quaternion rot = Quaternion.Euler(save.eulerAngles);
            GameObject obj = Object.Instantiate(prefab, pos, rot);
            NetworkServer.Spawn(obj);

            unit = obj.GetComponent<RTSUnit>();
            if (unit == null)
            {
                NetworkServer.Destroy(obj);
                return false;
            }

            if (save.ownerPlayerIndex >= 0)
                unit.ServerSetOwnerPlayerIndex(save.ownerPlayerIndex);

            string nick = save.ownerNickname;
            if (string.IsNullOrEmpty(nick) && save.ownerPlayerIndex >= 0)
            {
                // Fallback: ник из players[] сейва
                var data = WorldSession.ActiveSaveData;
                var eco = FindEconomyByPlayerIndex(data, save.ownerPlayerIndex);
                if (eco != null)
                    nick = eco.nickname;
            }
            if (!string.IsNullOrEmpty(nick))
                unit.ServerSetOwnerNickname(nick);

            // Без connection — юнит живёт в мире, authority назначится при клейме по нику
            unit.ServerRestoreHealth(save.currentHealth, save.maxHealth);
            if (unit is Worker worker)
                worker.ServerRestoreInventory(save.inventoryWood, save.inventoryOre, save.inventoryFood);
            return true;
        }

        static bool TrySpawnUnit(UnitSaveData save, NetworkConnectionToClient conn,
            GameObject workerPrefab, GameObject warriorPrefab, GameObject archerPrefab, out RTSUnit unit)
        {
            if (!TrySpawnUnitOffline(save, workerPrefab, warriorPrefab, archerPrefab, out unit))
                return false;
            if (conn != null)
                unit.SetOwner(conn);
            return true;
        }

        static bool RestoreUnit(UnitSaveData save, NetworkConnectionToClient conn,
            GameObject workerPrefab, GameObject warriorPrefab, GameObject archerPrefab)
        {
            if (!TrySpawnUnit(save, conn, workerPrefab, warriorPrefab, archerPrefab, out RTSUnit unit))
                return false;
            unit.ServerApplySaveData(save);
            return true;
        }

        static bool RestoreBuilding(BuildingSaveData save, int ownerConnId, int ownerPlayerIndex = -1)
        {
            if (save == null)
                return false;

            if (BuildingManager.Instance == null)
            {
                UnityEngine.Debug.LogError("[WorldSnapshot] BuildingManager missing — cannot restore buildings.");
                return false;
            }

            BuildingType type = (BuildingType)save.buildingType;
            BuildingDefinition def = BuildingManager.Instance.GetDefinition(type);
            if (def == null || def.prefab == null)
                return false;

            Vector3 pos = save.finalPosition;
            pos.y = 0f; // Завершённые здания всегда на высоте 0
            GameObject obj = Object.Instantiate(def.prefab, pos, Quaternion.identity);
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

            Vector2Int origin = new Vector2Int(save.gridOriginX, save.gridOriginZ);
            Vector2Int size = new Vector2Int(
                save.gridSizeX > 0 ? save.gridSizeX : def.Size.x,
                save.gridSizeZ > 0 ? save.gridSizeZ : def.Size.y);

            float duration = save.buildDuration > 0.1f ? save.buildDuration : def.buildDuration;
            float depth = save.buryDepth > 0.1f ? save.buryDepth : def.buryDepth;
            int pop = save.populationProvided > 0 ? save.populationProvided : def.populationProvided;

            building.ServerRestoreFromSave(
                ownerConnId, type, pos,
                save.buildProgress, save.isComplete, duration, depth,
                save.spentWood, save.spentOre, save.spentFood,
                pop, save.populationGranted || save.isComplete,
                origin, size);

            int resolvedPlayerIndex = ownerPlayerIndex >= 0 ? ownerPlayerIndex : save.ownerPlayerIndex;
            if (resolvedPlayerIndex >= 0)
                building.ServerSetOwnerPlayerIndex(resolvedPlayerIndex);

            string nick = save.ownerNickname;
            if (string.IsNullOrEmpty(nick) && resolvedPlayerIndex >= 0)
            {
                var eco = FindEconomyByPlayerIndex(WorldSession.ActiveSaveData, resolvedPlayerIndex);
                if (eco != null)
                    nick = eco.nickname;
            }
            if (!string.IsNullOrEmpty(nick))
                building.ServerSetOwnerNickname(nick);

            var capital = building.GetComponent<CapitalProduction>();
            if (capital != null && save.capitalQueueCount > 0)
                capital.ServerRestoreQueue(save.capitalQueueCount, save.capitalActiveProgress);

            var barracks = building.GetComponent<BarracksProduction>();
            if (barracks != null && save.barracksQueueCount > 0)
                barracks.ServerRestoreQueue(save.barracksQueueCount, save.barracksActiveProgress);

            return true;
        }

        static void RestoreGardenBeds(RTSWorldSaveData data)
        {
            if (data == null || data.gardenBeds == null || data.gardenBeds.Count == 0)
                return;

            var allGardens = Object.FindObjectsByType<GardenBed>(FindObjectsSortMode.None);
            
            foreach (var gardenData in data.gardenBeds)
            {
                if (gardenData == null || string.IsNullOrEmpty(gardenData.plotStatesJson))
                    continue;

                // Найти соответствующий GardenBed по позиции
                GardenBed matchingGarden = null;
                float minDist = float.MaxValue;
                
                foreach (var garden in allGardens)
                {
                    if (garden == null) continue;
                    
                    var building = garden.GetComponent<Building>();
                    if (building == null || !building.IsComplete) continue;
                    
                    float dist = Vector3.Distance(building.FinalPosition, gardenData.buildingPosition);
                    if (dist < 0.5f && dist < minDist)
                    {
                        minDist = dist;
                        matchingGarden = garden;
                    }
                }

                if (matchingGarden != null)
                {
                    matchingGarden.ServerRestorePlotStates(gardenData.plotStatesJson);
                }
            }
            
            UnityEngine.Debug.Log($"[WorldSnapshot] Restored {data.gardenBeds.Count} garden bed(s)");
        }

        /// <summary>
        /// Найти сохранённый playerIndex по nickname/email. Fallback — joinOrderIndex.
        /// </summary>
        public static int FindSavedPlayerIndex(RTSWorldSaveData data, string nickname, string email, int joinOrderIndex)
        {
            if (data?.players == null || data.players.Count == 0)
                return joinOrderIndex;

            if (!string.IsNullOrWhiteSpace(email))
            {
                for (int i = 0; i < data.players.Count; i++)
                {
                    var p = data.players[i];
                    if (p == null || string.IsNullOrEmpty(p.accountEmail))
                        continue;
                    if (p.accountEmail.Equals(email, System.StringComparison.OrdinalIgnoreCase))
                        return p.playerIndex;
                }
            }

            if (!string.IsNullOrWhiteSpace(nickname))
            {
                for (int i = 0; i < data.players.Count; i++)
                {
                    var p = data.players[i];
                    if (p == null || string.IsNullOrEmpty(p.nickname))
                        continue;
                    if (p.nickname.Equals(nickname, System.StringComparison.OrdinalIgnoreCase))
                        return p.playerIndex;
                }
            }

            // Хост обычно первый (index 0); иначе join order, если такой индекс есть в сейве
            for (int i = 0; i < data.players.Count; i++)
            {
                if (data.players[i] != null && data.players[i].playerIndex == joinOrderIndex)
                    return joinOrderIndex;
            }

            return joinOrderIndex;
        }

        static int ResolveConnectionId(int playerIndex)
        {
            foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.PlayerIndex == playerIndex)
                    return p.ConnectionId;
            }
            return -1;
        }

        static int ResolveConnectionIdByNickname(string nickname, int playerIndexFallback)
        {
            if (!string.IsNullOrWhiteSpace(nickname))
            {
                foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                {
                    if (p != null && !string.IsNullOrEmpty(p.Nickname) &&
                        p.Nickname.Equals(nickname, System.StringComparison.OrdinalIgnoreCase))
                        return p.ConnectionId;
                }
            }
            return ResolveConnectionId(playerIndexFallback);
        }

        static void FillCameraIntoSave(RTSPlayer player, PlayerSaveData entry)
        {
            if (player == null || entry == null)
                return;

            if (player.HasCameraView)
            {
                entry.hasCameraView = true;
                entry.cameraX = player.CameraPosition.x;
                entry.cameraY = player.CameraPosition.y;
                entry.cameraZ = player.CameraPosition.z;
                entry.cameraZoom = player.CameraZoom;
                entry.cameraYaw = player.CameraYaw;
                return;
            }

            // Хост: сцена с камерой на той же машине — снять напрямую
            if (NetworkServer.active &&
                player.connectionToClient != null &&
                NetworkServer.localConnection == player.connectionToClient)
            {
                var cam = Object.FindFirstObjectByType<RTSokak.Camera.RTSCameraController>();
                if (cam != null)
                {
                    cam.GetView(out Vector3 pos, out float zoom, out float yaw);
                    entry.hasCameraView = true;
                    entry.cameraX = pos.x;
                    entry.cameraY = pos.y;
                    entry.cameraZ = pos.z;
                    entry.cameraZoom = zoom;
                    entry.cameraYaw = yaw;
                    player.ServerSetCameraView(pos, zoom, yaw);
                }
            }
        }

        static int FindPlayerIndex(int connectionId)
        {
            foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.ConnectionId == connectionId)
                    return p.PlayerIndex;
            }

            // Неизвестного владельца не приписываем хосту (раньше return 0 ломал сейвы)
            return -1;
        }
    }
}
