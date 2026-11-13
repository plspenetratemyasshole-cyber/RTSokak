using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace RTSokak.Network
{
    /// <summary>
    /// Лучник: дальнобойный юнит с предиктивной стрельбой.
    /// Предсказывает будущую позицию движущейся цели с учётом NavMesh.
    /// </summary>
    public class Archer : RTSUnit
    {
        [Header("Archer")]
        [SerializeField] private string displayName = "Archer";
        
        [Header("Ranged Combat")]
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private GameObject crossbowPrefab;
        [SerializeField] private float shootRange = 12f;
        [SerializeField] private float arrowSpeed = 15f;
        [SerializeField] private int arrowDamage = 20;
        [SerializeField] private float shootCooldown = 2f;
        [SerializeField] private LayerMask obstacleMask; // Маска препятствий для raycast
        
        private RTSokak.UI.UnitNameplate nameplate;
        private float nextShootTime;
        private float archerNextChaseRetargetTime;
        private GameObject crossbowInstance;

        [SyncVar(hook = nameof(OnCrossbowEquippedChanged))]
        private bool isCrossbowEquipped = true;

        public override float GetPlayerActionCooldown() => shootCooldown;

        private void OnCrossbowEquippedChanged(bool oldVal, bool newVal)
        {
            SetCrossbowVisual(newVal);
        }

        private void SetCrossbowVisual(bool show)
        {
            if (show)
            {
                CreateCrossbow();
                if (crossbowInstance != null)
                    crossbowInstance.SetActive(true);
            }
            else
            {
                if (crossbowInstance != null)
                    crossbowInstance.SetActive(false);
            }
        }
        
        private enum ArcherCombatState
        {
            None,
            Chasing,
            Shooting
        }
        
        private ArcherCombatState archerCombatState = ArcherCombatState.None;

        public override bool CanDoEconomyTasks => false;
        public override bool HasInventory => false;

        protected override void Awake()
        {
            base.Awake();
            
            // Переопределить параметры атаки для лучника
            attackRange = shootRange;
            attackCooldown = shootCooldown;
            attackDamage = arrowDamage;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CreateNameplate();
            ForceUpdateOutline();
            SetCrossbowVisual(isCrossbowEquipped);
        }
        
        private void CreateCrossbow()
        {
            if (crossbowInstance != null || crossbowPrefab == null || toolHoldPoint == null)
                return;
            
            crossbowInstance = Instantiate(crossbowPrefab, toolHoldPoint);
            crossbowInstance.transform.localPosition = Vector3.zero;
            // Арбалет должен смотреть ВПЕРЁД горизонтально (не применяем toolStartAngle)
            crossbowInstance.transform.localRotation = Quaternion.identity;

            foreach (var col in crossbowInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        [Server]
        protected override void ServerStartCombat(RTSUnit target)
        {
            ServerStartArcherCombat(target);
        }

        [Server]
        protected override void ServerStartCombatWithBuilding(Buildings.Building target)
        {
            ServerStartArcherCombatWithBuilding(target);
        }

        [Server]
        protected override void ServerStopCombat()
        {
            ServerStopArcherCombat();
        }

        [Server]
        protected override void ServerUpdateCombat()
        {
            ServerUpdateArcherCombat();
        }

        /// <summary>
        /// Обновление боя лучника (вместо ближнего боя RTSUnit)
        /// </summary>
        [Server]
        private void ServerUpdateArcherCombat()
        {
            if (archerCombatState == ArcherCombatState.None)
                return;

            if (attackTarget != null && !attackTarget.IsAlive)
            {
                RememberHuntOwnerFromUnit(attackTarget);
                attackTarget = null;
            }
            if (attackTargetBuilding != null && attackTargetBuilding.CurrentHealth <= 0)
            {
                RememberHuntOwnerFromBuilding(attackTargetBuilding);
                attackTargetBuilding = null;
            }

            bool hasUnitTarget = attackTarget != null && attackTarget.IsAlive;
            bool hasBuildingTarget = attackTargetBuilding != null && attackTargetBuilding.CurrentHealth > 0;

            if (!hasUnitTarget && !hasBuildingTarget)
            {
                if (Time.time < nextEnemySearchTime)
                    return;
                nextEnemySearchTime = Time.time + 0.25f;

                if (!ServerTryAcquireNextEnemyArcher())
                {
                    ServerStopArcherCombat();
                    syncedDestination = Vector3.zero;
                }
                return;
            }

            Vector3 aimPos;
            float dist;
            Vector3 chaseDest;

            if (hasUnitTarget)
            {
                aimPos = attackTarget.transform.position + Vector3.up * 1f;
                dist = HorizontalDistance(transform.position, attackTarget.transform.position);
                chaseDest = attackTarget.transform.position;
                chaseDest.y = 0f;
            }
            else
            {
                aimPos = attackTargetBuilding.transform.position + Vector3.up * 0.75f;
                dist = DistanceToBuildingSurface(attackTargetBuilding);
                chaseDest = GetBuildingAttackDestination(attackTargetBuilding);
            }

            bool hasLineOfSight = CheckLineOfSight(aimPos);

            if (dist > shootRange || !hasLineOfSight)
            {
                archerCombatState = ArcherCombatState.Chasing;
                combatState = UnitCombatState.Chasing;
                if (navAgent != null)
                    navAgent.isStopped = false;

                bool needRetarget = Time.time >= archerNextChaseRetargetTime;
                if (navAgent != null && navAgent.enabled)
                {
                    needRetarget = needRetarget || !navAgent.hasPath || navAgent.remainingDistance < 0.35f ||
                                   HorizontalDistance(navAgent.destination, chaseDest) > 0.5f;
                }

                if (needRetarget)
                {
                    archerNextChaseRetargetTime = Time.time + chaseRetargetInterval;
                    if ((chaseDest - syncedDestination).sqrMagnitude > 0.35f)
                        syncedDestination = chaseDest;
                    TrySetAgentDestination(chaseDest);
                }
                return;
            }

            archerCombatState = ArcherCombatState.Shooting;
            combatState = UnitCombatState.Attacking;

            if (navAgent != null && navAgent.enabled)
            {
                if (dist < shootRange * 0.7f)
                    navAgent.isStopped = true;
                else
                    navAgent.isStopped = false;
            }

            FaceTarget(aimPos);

            if (Time.time >= nextShootTime)
            {
                nextShootTime = Time.time + shootCooldown;

                if (hasUnitTarget)
                    ServerShootAtUnit(attackTarget);
                else if (hasBuildingTarget)
                    ServerShootAtBuilding(attackTargetBuilding);
            }
        }

        [Server]
        private bool CheckLineOfSight(Vector3 targetPos)
        {
            if (obstacleMask == 0)
                return true;

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 direction = targetPos - origin;
            // Не дотягиваем луч до самой цели — иначе собственный коллайдер здания блокирует LoS
            float distance = direction.magnitude - 0.4f;
            if (distance <= 0.05f)
                return true;

            if (Physics.Raycast(origin, direction.normalized, distance, obstacleMask))
                return false;

            return true;
        }

        [Server]
        private Vector3 GetArrowSpawnPosition()
        {
            // Стрела из точки камеры (anchor), иначе toolHoldPoint
            if (unitPlayerCameraAnchor != null)
                return unitPlayerCameraAnchor.position;
            if (toolHoldPoint != null)
                return toolHoldPoint.position;
            return transform.position + Vector3.up * 1.4f;
        }

        [Server]
        private void ServerShootAtUnit(RTSUnit target)
        {
            if (target == null || !target.IsAlive || arrowPrefab == null)
                return;

            Vector3 spawnPos = GetArrowSpawnPosition();
            Vector3 predictedPos = PredictTargetPosition(target);
            predictedPos.y = spawnPos.y;

            float distance = Vector3.Distance(spawnPos, predictedPos);
            float hitTime = distance / arrowSpeed;

            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
            Arrow arrow = arrowObj.GetComponent<Arrow>();
            if (arrow != null)
            {
                arrow.SetShooter(this);
                Vector3 shootDir = (predictedPos - spawnPos).normalized;
                arrow.Initialize(spawnPos, predictedPos, hitTime, target.netId, arrowDamage, shootDir, shootRange);
            }
            NetworkServer.Spawn(arrowObj);

            RpcShootVisual(predictedPos);
        }

        [Server]
        private void ServerShootAtBuilding(Buildings.Building target)
        {
            if (target == null || target.CurrentHealth <= 0 || arrowPrefab == null)
                return;

            Vector3 spawnPos = GetArrowSpawnPosition();
            Vector3 targetPos = target.transform.position + Vector3.up * 0.75f;
            float distance = Vector3.Distance(spawnPos, targetPos);
            float hitTime = distance / arrowSpeed;

            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
            Arrow arrow = arrowObj.GetComponent<Arrow>();
            if (arrow != null)
            {
                arrow.SetShooter(this);
                Vector3 shootDir = (targetPos - spawnPos).normalized;
                NetworkIdentity targetIdentity = target.GetComponent<NetworkIdentity>();
                uint targetId = targetIdentity != null ? targetIdentity.netId : 0;
                arrow.Initialize(spawnPos, targetPos, hitTime, targetId, arrowDamage, shootDir, shootRange);
            }
            NetworkServer.Spawn(arrowObj);

            RpcShootVisual(targetPos);
        }

        public override void ApplyLocalPlayerHotbarVisual(int slotIndex)
        {
            playerHotbarSlot = slotIndex;
            SetCrossbowVisual(slotIndex == 0);
        }

        [Server]
        public override void ServerApplyHotbarSlot(int slotIndex)
        {
            playerHotbarSlot = slotIndex;
            bool equip = (slotIndex == 0);
            isCrossbowEquipped = equip;
            SetCrossbowVisual(equip);
            RpcSetCrossbowEquipped(equip);
        }

        [ClientRpc]
        private void RpcSetCrossbowEquipped(bool equip)
        {
            SetCrossbowVisual(equip);
        }

        [Server]
        public override void ServerPlayerPrimaryAction(Vector3 rayOrigin, Vector3 rayDir)
        {
            if (!IsPlayerControlled || playerHotbarSlot < 0)
                return;
            if (Time.time < nextShootTime)
                return;
            if (arrowPrefab == null)
                return;

            rayDir = rayDir.normalized;
            Vector3 spawnPos = rayOrigin + rayDir * 0.55f;
            float maxFlight = shootRange * 2.5f;
            nextShootTime = Time.time + shootCooldown;

            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(rayDir));
            Arrow arrow = arrowObj.GetComponent<Arrow>();
            if (arrow != null)
            {
                arrow.InitializeFromPlayerRay(spawnPos, rayDir, arrowDamage, maxFlight, this);
            }
            NetworkServer.Spawn(arrowObj);

            RpcShootVisual(spawnPos + rayDir * maxFlight);
        }

        [Server]
        private Vector3 PredictTargetPosition(RTSUnit target)
        {
            if (target == null)
                return transform.position;

            NavMeshAgent targetAgent = target.GetComponent<NavMeshAgent>();
            Vector3 feet = target.transform.position;
            if (targetAgent == null || !targetAgent.enabled || targetAgent.isStopped || !targetAgent.hasPath)
                return feet;

            float currentDistance = Vector3.Distance(transform.position, feet);
            float timeToReach = currentDistance / arrowSpeed;
            Vector3 predictedPos = feet;

            if (targetAgent.hasPath && targetAgent.path.corners.Length > 1)
            {
                float distanceToTravel = targetAgent.velocity.magnitude * timeToReach;
                predictedPos = GetPositionAlongPath(targetAgent, distanceToTravel);
            }
            else
            {
                predictedPos = feet + targetAgent.velocity * timeToReach;
            }

            float newDistance = Vector3.Distance(transform.position, predictedPos);
            float newTimeToReach = newDistance / arrowSpeed;

            if (Mathf.Abs(newTimeToReach - timeToReach) > 0.1f)
            {
                float avgTime = (timeToReach + newTimeToReach) * 0.5f;
                float distanceToTravel = targetAgent.velocity.magnitude * avgTime;
                predictedPos = GetPositionAlongPath(targetAgent, distanceToTravel);
            }

            return predictedPos;
        }

        [Server]
        private Vector3 GetPositionAlongPath(NavMeshAgent agent, float distance)
        {
            if (agent == null || !agent.hasPath || agent.path.corners.Length == 0)
                return agent.transform.position;

            Vector3[] corners = agent.path.corners;
            Vector3 currentPos = agent.transform.position;
            float traveledDistance = 0f;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 segmentStart = i == 1 ? currentPos : corners[i - 1];
                Vector3 segmentEnd = corners[i];
                float segmentLength = Vector3.Distance(segmentStart, segmentEnd);

                if (traveledDistance + segmentLength >= distance)
                {
                    float remainingDistance = distance - traveledDistance;
                    Vector3 direction = (segmentEnd - segmentStart).normalized;
                    return segmentStart + direction * remainingDistance;
                }

                traveledDistance += segmentLength;
            }

            return corners[corners.Length - 1];
        }

        [ClientRpc(includeOwner = true)]
        private void RpcShootVisual(Vector3 targetPos)
        {
            if (!IsPlayerControlled)
                FaceTarget(targetPos);
        }

        [Server]
        private void ServerStopArcherCombat()
        {
            archerCombatState = ArcherCombatState.None;
            combatState = UnitCombatState.None;
            attackTarget = null;
            attackTargetBuilding = null;
            syncedAttackTargetNetId = 0;
            useEnemyPathLine = false;
            ClearHuntOwner();
            RpcSetPathLineMode(false);
        }

        [Server]
        private bool ServerTryAcquireNextEnemyArcher()
        {
            bool requireHunt = hasHuntOwner;
            RTSUnit nearestUnit = FindNearestEnemy(autoAttackSearchRange, requireHunt);
            Buildings.Building nearestBuilding = FindNearestEnemyBuilding(autoAttackSearchRange, requireHunt);

            if (nearestUnit == null && nearestBuilding == null)
                return false;

            float unitDist = nearestUnit != null
                ? HorizontalDistance(transform.position, nearestUnit.transform.position)
                : float.MaxValue;
            float buildingDist = nearestBuilding != null
                ? DistanceToBuildingSurface(nearestBuilding)
                : float.MaxValue;

            if (nearestUnit != null && unitDist <= buildingDist)
                ServerStartArcherCombat(nearestUnit);
            else if (nearestBuilding != null)
                ServerStartArcherCombatWithBuilding(nearestBuilding);
            else
                return false;

            return true;
        }

        [Server]
        private void ServerStartArcherCombat(RTSUnit target)
        {
            if (target == null || !target.IsAlive)
                return;

            isCrossbowEquipped = true;
            SetCrossbowVisual(true);
            RpcSetCrossbowEquipped(true);

            RememberHuntOwnerFromUnit(target);

            attackTarget = target;
            attackTargetBuilding = null;
            syncedAttackTargetNetId = target.netId;
            archerCombatState = ArcherCombatState.Chasing;
            combatState = UnitCombatState.Chasing;
            useEnemyPathLine = true;
            nextShootTime = 0f;
            archerNextChaseRetargetTime = 0f;

            Vector3 dest = target.transform.position;
            dest.y = 0f;
            syncedDestination = dest;
            TrySetAgentDestination(dest);
            RpcSetPathLineMode(true);
            RpcMoveVisual(dest);
        }

        [Server]
        private void ServerStartArcherCombatWithBuilding(Buildings.Building target)
        {
            if (target == null || target.CurrentHealth <= 0)
                return;

            isCrossbowEquipped = true;
            SetCrossbowVisual(true);
            RpcSetCrossbowEquipped(true);

            RememberHuntOwnerFromBuilding(target);

            attackTarget = null;
            attackTargetBuilding = target;
            NetworkIdentity targetIdentity = target.GetComponent<NetworkIdentity>();
            syncedAttackTargetNetId = targetIdentity != null ? targetIdentity.netId : 0;
            archerCombatState = ArcherCombatState.Chasing;
            combatState = UnitCombatState.Chasing;
            useEnemyPathLine = true;
            nextShootTime = 0f;
            archerNextChaseRetargetTime = 0f;

            Vector3 dest = GetBuildingAttackDestination(target);
            syncedDestination = dest;
            TrySetAgentDestination(dest);
            RpcSetPathLineMode(true);
            RpcMoveVisual(dest);
        }

        [Server]
        public override void ServerClearPlayerController()
        {
            base.ServerClearPlayerController();
            isCrossbowEquipped = true;
            SetCrossbowVisual(true);
            RpcSetCrossbowEquipped(true);
        }

        protected override void OnServerCancelTasksForMove()
        {
            ServerStopArcherCombat();
        }

        [Server]
        public override void ServerWriteSaveData(RTSokak.Shared.UnitSaveData entry)
        {
            base.ServerWriteSaveData(entry);
            if (entry == null) return;

            // Сохраняем archer combat как обычный combatState
            if (archerCombatState != ArcherCombatState.None)
                entry.combatState = archerCombatState == ArcherCombatState.Shooting
                    ? (int)UnitCombatState.Attacking
                    : (int)UnitCombatState.Chasing;
        }

        private void CreateNameplate()
        {
            GameObject nameplateObj = new GameObject("Nameplate");
            nameplate = nameplateObj.AddComponent<RTSokak.UI.UnitNameplate>();

            Canvas canvas = nameplateObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CanvasScaler scaler = nameplateObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(nameplateObj.transform);

            TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 36;
            text.color = Color.white;

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200, 50);
            textRect.localPosition = Vector3.zero;

            var textField = typeof(RTSokak.UI.UnitNameplate).GetField("professionText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            textField?.SetValue(nameplate, text);

            var canvasField = typeof(RTSokak.UI.UnitNameplate).GetField("canvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            canvasField?.SetValue(nameplate, canvas);

            var textSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("textSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            textSizeField?.SetValue(nameplate, 0.02f);

            var fontSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("fontSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fontSizeField?.SetValue(nameplate, 18f);

            var awakeMethod = typeof(RTSokak.UI.UnitNameplate).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awakeMethod?.Invoke(nameplate, null);

            nameplate.SetTarget(transform);
            nameplate.UpdateProfession(displayName);
        }

        protected override void OnDestroy()
        {
            if (crossbowInstance != null)
            {
                Destroy(crossbowInstance);
                crossbowInstance = null;
            }
            
            if (nameplate != null)
            {
                Destroy(nameplate.gameObject);
                nameplate = null;
            }
            base.OnDestroy();
        }

    }
}
