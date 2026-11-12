using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using RTSokak.Shared;

namespace RTSokak.Network
{
    /// <summary>
    /// Базовый юнит: владение, выбор, движение, HP, ближний бой.
    /// </summary>
    [RequireComponent(typeof(Outline))]
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class RTSUnit : NetworkBehaviour
    {
        public enum UnitCombatState
        {
            None,
            Chasing,
            Attacking
        }

        [SyncVar(hook = nameof(OnOwnerConnectionIdChanged))]
        protected int ownerConnectionId = -1;

        /// <summary>Стабильный индекс игрока-владельца (не меняется при reconnect).</summary>
        [SyncVar(hook = nameof(OnOwnerPlayerIndexChanged))]
        protected int ownerPlayerIndex = -1;

        /// <summary>Ник владельца — главный критерий «свой/чужой» на клиентах.</summary>
        [SyncVar(hook = nameof(OnOwnerNicknameChanged))]
        protected string ownerNickname = "";

        /// <summary>netId RTSPlayer, который управляет юнитом в FPS. 0 = AI.</summary>
        [SyncVar(hook = nameof(OnControllerPlayerNetIdChanged))]
        protected uint controllerPlayerNetId;

        [SyncVar(hook = nameof(OnSelectedChanged))]
        protected bool isSelected;

        [SyncVar(hook = nameof(OnDestinationChanged))]
        protected Vector3 syncedDestination;

        [SyncVar(hook = nameof(OnHealthChanged))]
        protected int currentHealth = 100;

        [SyncVar]
        protected int maxHealth = 100;

        [Header("Health")]
        [SerializeField] protected int startingMaxHealth = 100;

        [Header("Outline Colors")]
        [SerializeField] protected Color selectedOutlineColor = new Color(0.15f, 1f, 0.2f, 1f);
        [SerializeField] protected Color enemyOutlineColor = new Color(1f, 0.08f, 0.08f, 1f);

        [Header("Movement")]
        [SerializeField] protected float moveSpeed = 3.5f;
        [SerializeField] protected float stoppingDistance = 2.5f;
        [Tooltip("Stopping distance при плотном строе (групповой MoveTo)")]
        [SerializeField] protected float formationStoppingDistance = 0.12f;
        [SerializeField] protected float angularSpeed = 360f;
        [SerializeField] protected float acceleration = 8f;

        [Header("Path Line")]
        [SerializeField] protected float outlineWidth = 2f;
        [SerializeField] protected float pathLineWidth = 0.3f;
        [SerializeField] protected float lineHeight = 0.1f;
        [SerializeField] protected Color pathLineColor = Color.green;

        [Header("Combat")]
        [SerializeField] protected Transform toolHoldPoint;
        [SerializeField] protected GameObject swordPrefab;
        [SerializeField] protected float attackRange = 2.2f;
        [SerializeField] protected float attackCooldown = 1.05f;
        [SerializeField] protected float attackAnimDuration = 0.38f;
        [SerializeField] protected int attackDamage = 15;
        [SerializeField] protected float toolStartAngle = 90f;
        [SerializeField] protected float toolEndAngle = 0f;
        [SerializeField] protected float autoAttackSearchRange = 80f;
        [SerializeField] protected float chaseRetargetInterval = 0.12f;

        [Header("Hit Reaction")]
        [SerializeField] protected float knockbackForce = 3.1f;
        [SerializeField] protected float knockbackUpForce = 1.55f;
        [SerializeField] protected float knockbackMinDuration = 0.38f;
        [SerializeField] protected float knockbackMaxDuration = 1.05f;
        [SerializeField] protected float knockbackGroundSnapHeight = 0.4f;
        [SerializeField] protected float knockbackSettleVerticalSpeed = 1.4f;
        [SerializeField] protected float knockbackSettleDuration = 0.22f;
        [SerializeField] protected float hitFlashDuration = 0.2f;
        [SerializeField] protected Color hitFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

        [Header("Health Bar")]
        [SerializeField] protected Vector3 healthBarOffset = new Vector3(0f, 2.5f, 0f);
        [Tooltip("Если выкл — размер/цвет из RTSokak → Health Bar → Global Settings")]
        [SerializeField] protected bool overrideHealthBarBaseValues;
        [SerializeField] protected Gradient healthBarGradient;
        [Tooltip("X = длина при Reference HP, Y = толщина. Только при Override")]
        [SerializeField] protected Vector2 healthBarSize = new Vector2(0.1f, 0.015f);
        [SerializeField] protected float healthBarReferenceHp = 100f;

        [Header("Net Sync / Prediction")]
        [Tooltip("Как часто сервер шлёт авторитетную позицию клиентам")]
        [SerializeField] protected float netPositionSyncInterval = 0.08f;
        [SerializeField] protected float softCorrectDistance = 0.35f;
        [SerializeField] protected float hardCorrectDistance = 2.6f;
        [SerializeField] protected float softCorrectSpeed = 14f;
        [SerializeField] protected float knockbackFollowSpeed = 22f;

        [Header("Combat Fairness")]
        [Tooltip("Макс. добавка к радиусу атаки из-за RTT (м)")]
        [SerializeField] protected float maxLatencyRangeBonus = 1.75f;
        [Tooltip("Макс. урезание радиуса хоста (м)")]
        [SerializeField] protected float maxHostRangePenalty = 0.85f;

        protected Outline outline;
        protected NavMeshAgent navAgent;
        protected Rigidbody unitRigidbody;
        protected LineRenderer pathLine;
        protected Material pathLineMaterial;
        protected Material enemyPathLineMaterial;
        protected NetworkIdentity unitNetIdentity;

        protected UnitCombatState combatState = UnitCombatState.None;
        protected RTSUnit attackTarget;
        protected Buildings.Building attackTargetBuilding;
        protected float nextAttackTime;
        protected bool useEnemyPathLine;

        [SyncVar]
        protected uint syncedAttackTargetNetId;

        [SyncVar(hook = nameof(OnSyncedNetPositionChanged))]
        private Vector3 syncedNetPosition;

        [SyncVar]
        private bool syncedAgentStopped;

        [SyncVar]
        private bool syncedKnockedBack;

        [SyncVar(hook = nameof(OnSwordEquippedChanged))]
        protected bool isSwordEquipped;

        private GameObject swordInstance;
        private bool isAnimatingSword;
        private float swordAnimStartTime;
        private bool? cachedIsOwned;
        private float lastOwnershipCheck;
        private float nextChaseRetargetTime;
        private float nextNetPosSyncTime;

        /// <summary>После убийства ищем следующую цель с тем же владельцем.</summary>
        protected bool hasHuntOwner;
        private string huntOwnerNickname = "";
        private int huntOwnerPlayerIndex = -1;
        private int huntOwnerConnectionId = -1;
        private float savedStoppingDistance = -1f;
        
        public static readonly List<RTSUnit> AllUnits = new List<RTSUnit>();

        protected virtual void OnEnable()
        {
            AllUnits.Add(this);
        }

        protected virtual void OnDisable()
        {
            AllUnits.Remove(this);
        }

        protected RTSokak.UI.UnitHealthBar healthBar;
        private NetworkTransformBase networkTransform;
        private Vector3 clientCorrectionTarget;
        private bool hasClientCorrectionTarget;

        // Hit FX
        private float flashEndTime;
        private Renderer[] cachedRenderers;
        private Color[] originalColors;
        private float knockbackStartTime;
        private bool isKnockedBack;
        private bool isKnockbackSettling;
        private float knockbackSettleStartTime;
        private Vector3 knockbackSettleFrom;
        private Vector3 knockbackSettleTo;
        private Vector3 clientSmoothVel;
        private RigidbodyConstraints idleRbConstraints;
        private RigidbodyConstraints knockbackRbConstraints;

        public int OwnerConnectionId => ownerConnectionId;
        public int OwnerPlayerIndex => ownerPlayerIndex;
        public string OwnerNickname => ownerNickname;
        public bool IsSelected => isSelected;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsInCombat => combatState != UnitCombatState.None;
        public float AttackRange => attackRange;
        public Transform ToolHoldPoint => toolHoldPoint;
        public bool IsPlayerControlled => controllerPlayerNetId != 0;
        public uint ControllerPlayerNetId => controllerPlayerNetId;

        [Header("Unit Player")]
        [Tooltip("Empty Transform (CameraPoint) — позиция FPS-камеры")]
        [SerializeField] protected Transform unitPlayerCameraAnchor;
        [Tooltip("Объекты (напр. googly_eyes), скрываемые только у локального юнита-игрока")]
        [SerializeField] protected GameObject[] unitPlayerLocalHideObjects;
        [SerializeField] protected float unitPlayerMoveSpeed = 3.5f;
        [SerializeField] protected float unitPlayerSprintMultiplier = 1.65f;
        [SerializeField] protected float unitPlayerJumpForce = 6.5f;
        [Tooltip("Линейный туман: конец тумана = этот радиус (метры)")]
        [SerializeField] protected float unitPlayerFogRadius = 48f;

        public Transform UnitPlayerCameraAnchor => unitPlayerCameraAnchor;
        public GameObject[] UnitPlayerLocalHideObjects => unitPlayerLocalHideObjects;
        public float UnitPlayerMoveSpeed => unitPlayerMoveSpeed;
        public float UnitPlayerSprintMultiplier => unitPlayerSprintMultiplier;
        public float UnitPlayerJumpForce => unitPlayerJumpForce;
        public float UnitPlayerFogRadius => unitPlayerFogRadius;

        /// <summary>
        /// «Своя» фракция для локального игрока.
        /// У юнита-игрока — ник мэра (владельца юнита), не личный ник.
        /// </summary>
        public static string LocalFactionNickname
        {
            get
            {
                var lp = RTSPlayer.localPlayer;
                if (lp == null)
                    return RTSokak.Shared.RTSPlayerSession.Nickname ?? "";

                var upc = UnitPlayerController.Instance;
                if (upc != null && upc.IsActive && upc.ControlledUnit != null && !string.IsNullOrEmpty(upc.ControlledUnit.ownerNickname))
                {
                    return upc.ControlledUnit.ownerNickname;
                }

                if (lp.Role == PlayerRole.UnitPlaying && lp.ControlledUnitNetId != 0)
                {
                    if (NetworkClient.spawned.TryGetValue(lp.ControlledUnitNetId, out NetworkIdentity id))
                    {
                        var u = id.GetComponent<RTSUnit>();
                        if (u != null && !string.IsNullOrEmpty(u.ownerNickname))
                            return u.ownerNickname;
                    }
                }

                if (!string.IsNullOrEmpty(lp.Nickname))
                    return lp.Nickname;
                return RTSokak.Shared.RTSPlayerSession.Nickname ?? "";
            }
        }

        public bool IsOwned
        {
            get
            {
                if (cachedIsOwned.HasValue && Time.time - lastOwnershipCheck < 1f)
                    return cachedIsOwned.Value;

                bool owned = false;
                if (UnitPlayerController.Instance != null && UnitPlayerController.Instance.IsActive && UnitPlayerController.Instance.ControlledUnit != null)
                {
                    owned = IsSameOwnerAs(UnitPlayerController.Instance.ControlledUnit);
                }
                else if (RTSPlayer.localPlayer != null && RTSPlayer.localPlayer.Role == PlayerRole.UnitPlaying && RTSPlayer.localPlayer.ControlledUnitNetId != 0 &&
                         NetworkClient.spawned.TryGetValue(RTSPlayer.localPlayer.ControlledUnitNetId, out NetworkIdentity id) &&
                         id != null && id.TryGetComponent<RTSUnit>(out var controlledUnit))
                {
                    owned = IsSameOwnerAs(controlledUnit);
                }
                else if (RTSPlayer.localPlayer != null)
                {
                    string factionNick = LocalFactionNickname;

                    if (!string.IsNullOrEmpty(ownerNickname) && !string.IsNullOrEmpty(factionNick))
                        owned = string.Equals(ownerNickname, factionNick, System.StringComparison.OrdinalIgnoreCase);
                    else if (string.IsNullOrEmpty(ownerNickname))
                    {
                        // Fallback только пока ник владельца ещё не пришёл
                        if (RTSPlayer.localPlayer.Role != PlayerRole.UnitPlaying)
                        {
                            if (ownerPlayerIndex >= 0 && RTSPlayer.localPlayer.PlayerIndex >= 0)
                                owned = ownerPlayerIndex == RTSPlayer.localPlayer.PlayerIndex;
                            else
                                owned = ownerConnectionId == RTSPlayer.localPlayer.ConnectionId;
                        }
                    }
                }
                cachedIsOwned = owned;
                lastOwnershipCheck = Time.time;
                return owned;
            }
        }

        /// <summary>Один ли владелец у двух юнитов (по нику, иначе по connectionId).</summary>
        public bool IsSameOwnerAs(RTSUnit other)
        {
            if (other == null) return false;
            if (!string.IsNullOrEmpty(ownerNickname) && !string.IsNullOrEmpty(other.ownerNickname))
                return string.Equals(ownerNickname, other.ownerNickname, System.StringComparison.OrdinalIgnoreCase);
            if (ownerPlayerIndex >= 0 && other.ownerPlayerIndex >= 0)
                return ownerPlayerIndex == other.ownerPlayerIndex;
            return ownerConnectionId == other.ownerConnectionId;
        }

        public bool IsOwnedByNickname(string nick)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(ownerNickname))
                return false;
            return string.Equals(ownerNickname, nick, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Может ли юнит добывать ресурсы / строить / фармить.</summary>
        public virtual bool CanDoEconomyTasks => false;

        /// <summary>Есть ли инвентарь (для UI).</summary>
        public virtual bool HasInventory => false;

        protected virtual void Awake()
        {
            unitNetIdentity = GetComponent<NetworkIdentity>();
            outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            // OutlineAll очень дорогой по fill-rate; Visible достаточно для RTS
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            outline.OutlineWidth = outlineWidth;
            outline.enabled = false;

            navAgent = GetComponent<NavMeshAgent>();
            navAgent.speed = moveSpeed;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.angularSpeed = angularSpeed;
            navAgent.acceleration = acceleration;
            navAgent.baseOffset = 1f;

            unitRigidbody = GetComponent<Rigidbody>();
            if (unitRigidbody != null)
            {
                // Agent mode: kinematic so NavMeshAgent owns transform.
                // Knockback: non-kinematic + FreezeRotation only.
                unitRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                knockbackRbConstraints = RigidbodyConstraints.FreezeRotation;
                idleRbConstraints = RigidbodyConstraints.FreezeRotation;
                SetAgentDrivenPhysics(true);
            }

            networkTransform = GetComponent<NetworkTransformBase>();
            // Свою предикцию/коррекцию вместо NT — иначе дерганье и конфликт с агентом
            if (networkTransform != null)
                networkTransform.enabled = false;

            SetupPathLineMaterials();
            CacheRenderers();
        }

        protected virtual void Start()
        {
            UpdateOutline();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            maxHealth = Mathf.Max(1, startingMaxHealth);
            currentHealth = maxHealth;
            syncedNetPosition = transform.position;
            nextNetPosSyncTime = Time.time;

            ConfigureNavAgentForSimulation();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateOutline();
            CreateHealthBar();

            if (isSwordEquipped)
                SetSwordEquippedVisual(true);

            if (networkTransform == null)
                networkTransform = GetComponent<NetworkTransformBase>();
            if (networkTransform != null)
                networkTransform.enabled = false;

            // Клиент (не хост): локальный NavMeshAgent предсказывает путь к syncedDestination
            if (!isServer)
            {
                ConfigureNavAgentForSimulation();
                clientCorrectionTarget = syncedNetPosition != Vector3.zero ? syncedNetPosition : transform.position;
                hasClientCorrectionTarget = true;
            }
        }

        private void ConfigureNavAgentForSimulation()
        {
            if (navAgent == null)
                return;

            navAgent.enabled = true;
            navAgent.speed = moveSpeed;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.angularSpeed = angularSpeed;
            navAgent.acceleration = acceleration;
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            navAgent.avoidancePriority = 50;
            if (!navAgent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        private void CreateHealthBar()
        {
            if (healthBar != null)
                return;

            RTSokak.UI.UnitHealthBar.ResolveStyle(
                overrideHealthBarBaseValues,
                healthBarSize,
                healthBarReferenceHp,
                healthBarGradient,
                out Vector2 sizeAtRef,
                out float refHp,
                out Gradient gradient);

            Vector2 size = RTSokak.UI.UnitHealthBar.SizeForMaxHealth(
                Mathf.Max(1, maxHealth), sizeAtRef, refHp);

            healthBar = RTSokak.UI.UnitHealthBar.Create(transform, healthBarOffset, size, gradient);
            float normalizedHealth = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
            healthBar.UpdateHealthBar(normalizedHealth);
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            UpdateOutline();
        }

        protected virtual void Update()
        {
            UpdateHitFlashVisual();

            // Линии путей в FPS юнит-игрока — лишний CPU
            bool unitPlaying = RTSPlayer.localPlayer != null &&
                               RTSPlayer.localPlayer.Role == PlayerRole.UnitPlaying;
            if (!unitPlaying)
                UpdateLivePathLine();
            else if (pathLine != null && pathLine.enabled)
                pathLine.enabled = false;

            if (isAnimatingSword)
                AnimateSword();

            if (isServer)
            {
                ServerUpdateKnockback();
                // Игрок-юнит сам ходит/бьёт — AI-бой и chase отключены
                if (!isKnockedBack && !isKnockbackSettling && !IsPlayerControlled)
                    ServerUpdateCombat();

                // Хост в FPS: не синкаем каждый юнит за туманом так же часто
                if (unitPlaying && IsPlayerControlled)
                    ServerBroadcastNetState();
                else if (unitPlaying)
                {
                    if ((netId + (uint)Time.frameCount) % 2 == 0)
                        ServerBroadcastNetState();
                }
                else
                    ServerBroadcastNetState();
            }
            else if (IsPlayerControlled)
            {
                // Чужой клиент: только плавно тянем к серверной позиции (без NavMesh Warp — он ломает Y)
                // Локальный водитель никогда не follow'ит — иначе SyncVar дёргает позицию.
                if (RTSPlayer.localPlayer == null ||
                    RTSPlayer.localPlayer.ControlledUnitNetId != netId)
                    ClientFollowPlayerControlledUnit();
            }
            else if (unitPlaying)
            {
                // FPS: prediction всех AI-юнитов каждый кадр — дорого; прореживаем + дистанция
                float fogR = UnitPlayerController.Instance != null
                    ? UnitPlayerController.Instance.FogRadius
                    : 48f;
                Transform camT = UnitPlayerController.Instance != null &&
                                 UnitPlayerController.Instance.FpsCamera != null
                    ? UnitPlayerController.Instance.FpsCamera.transform
                    : null;
                if (camT != null)
                {
                    float sqr = (transform.position - camT.position).sqrMagnitude;
                    if (sqr > fogR * fogR * 1.21f)
                        return; // за туманом — не симулируем
                }
                // ~20 Hz вместо 60
                if ((netId + (uint)Time.frameCount) % 3 == 0)
                    ClientUpdatePrediction();
            }
            else
            {
                ClientUpdatePrediction();
            }
        }

        private void ClientFollowPlayerControlledUnit()
        {
            if (!hasClientCorrectionTarget)
                return;

            Vector3 target = clientCorrectionTarget;
            float err = (transform.position - target).sqrMagnitude;
            if (err < 0.0001f)
                return;

            if (err > hardCorrectDistance * hardCorrectDistance)
            {
                transform.position = target;
                clientSmoothVel = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, target, ref clientSmoothVel, 0.08f, softCorrectSpeed * 1.5f);
        }

        protected virtual void OnDestroy()
        {
            if (pathLineMaterial != null)
                Destroy(pathLineMaterial);
            if (enemyPathLineMaterial != null)
                Destroy(enemyPathLineMaterial);
            
            if (healthBar != null)
            {
                Destroy(healthBar.gameObject);
                healthBar = null;
            }
        }

        [Server]
        public virtual void SetOwner(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            ownerConnectionId = conn.connectionId;

            RTSPlayer ownerPlayer = null;
            if (conn.identity != null)
                ownerPlayer = conn.identity.GetComponent<RTSPlayer>();
            if (ownerPlayer == null)
            {
                foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
                {
                    if (p != null && p.ConnectionId == conn.connectionId)
                    {
                        ownerPlayer = p;
                        break;
                    }
                }
            }
            if (ownerPlayer != null)
            {
                ownerPlayerIndex = ownerPlayer.PlayerIndex;
                if (!string.IsNullOrEmpty(ownerPlayer.Nickname))
                    ownerNickname = ownerPlayer.Nickname;
            }

            if (unitNetIdentity != null)
                unitNetIdentity.AssignClientAuthority(conn);
        }

        [Server]
        public void ServerSetOwnerPlayerIndex(int playerIndex)
        {
            ownerPlayerIndex = playerIndex;
        }

        [Server]
        public void ServerSetOwnerNickname(string nick)
        {
            ownerNickname = nick ?? "";
            cachedIsOwned = null;
        }

        void OnControllerPlayerNetIdChanged(uint oldId, uint newId)
        {
            if (navAgent == null)
                return;

            if (newId != 0)
            {
                if (navAgent.enabled)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }
                navAgent.enabled = false;
            }
            else if (oldId != 0)
            {
                navAgent.enabled = true;
                ConfigureNavAgentForSimulation();
            }
        }

        [Server]
        public void ServerSetPlayerController(RTSPlayer player)
        {
            if (player == null) return;
            controllerPlayerNetId = player.netId;
            ServerStopCombat();
            if (navAgent != null)
            {
                if (navAgent.enabled)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }
                // Игрок сам двигает transform — агент только мешает Y
                navAgent.enabled = false;
            }
            syncedDestination = Vector3.zero;
        }

        [Server]
        public virtual void ServerClearPlayerController()
        {
            controllerPlayerNetId = 0;
            if (navAgent != null)
            {
                navAgent.enabled = true;
                ConfigureNavAgentForSimulation();
                navAgent.isStopped = false;
            }
        }

        public RTSPlayer FindControllingPlayer()
        {
            if (controllerPlayerNetId == 0)
                return null;
            if (NetworkServer.active &&
                NetworkServer.spawned.TryGetValue(controllerPlayerNetId, out NetworkIdentity id))
                return id.GetComponent<RTSPlayer>();
            if (NetworkClient.active &&
                NetworkClient.spawned.TryGetValue(controllerPlayerNetId, out NetworkIdentity cid))
                return cid.GetComponent<RTSPlayer>();
            return null;
        }

        [Server]
        public void ServerRestoreHealth(int health, int max)
        {
            maxHealth = Mathf.Max(1, max);
            currentHealth = Mathf.Clamp(health, 1, maxHealth);
        }

        /// <summary>Базовый снапшот движения/боя. Worker дополняет экономическими задачами.</summary>
        [Server]
        public virtual void ServerWriteSaveData(RTSokak.Shared.UnitSaveData entry)
        {
            if (entry == null) return;

            entry.hasDestination = syncedDestination != Vector3.zero;
            entry.destination = entry.hasDestination
                ? RTSokak.Shared.SaveRounding.R2(syncedDestination)
                : Vector3.zero;

            entry.combatState = (int)combatState;
            if (attackTarget != null && attackTarget.IsAlive)
            {
                entry.attackTargetPos = RTSokak.Shared.SaveRounding.R2(attackTarget.transform.position);
                entry.attackTargetKind = attackTarget is Worker ? "Worker"
                    : attackTarget is Archer ? "Archer" : "Warrior";
                entry.attackTargetOwnerIndex = FindOwnerPlayerIndex(attackTarget.OwnerConnectionId);
            }
            else
            {
                entry.attackTargetPos = Vector3.zero;
                entry.attackTargetKind = string.Empty;
                entry.attackTargetOwnerIndex = -1;
            }
        }

        [Server]
        public virtual void ServerApplySaveData(RTSokak.Shared.UnitSaveData save)
        {
            if (save == null) return;

            if (save.hasDestination && save.destination != Vector3.zero)
            {
                syncedDestination = save.destination;
                TrySetAgentDestination(save.destination);
            }

            if (save.combatState != (int)UnitCombatState.None &&
                save.attackTargetPos.sqrMagnitude > 0.01f)
            {
                RTSUnit target = FindUnitNear(
                    save.attackTargetPos,
                    save.attackTargetKind,
                    save.attackTargetOwnerIndex);
                if (target != null)
                    ServerStartCombat(target);
            }
        }

        static int FindOwnerPlayerIndex(int connectionId)
        {
            foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.ConnectionId == connectionId)
                    return p.PlayerIndex;
            }
            return -1;
        }

        static RTSUnit FindUnitNear(Vector3 pos, string kind, int ownerPlayerIndex)
        {
            RTSUnit best = null;
            float bestDist = 2.5f;
            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (u == null || !u.IsAlive) continue;
                if (ownerPlayerIndex >= 0)
                {
                    int idx = u.OwnerPlayerIndex >= 0
                        ? u.OwnerPlayerIndex
                        : FindOwnerPlayerIndex(u.OwnerConnectionId);
                    if (idx != ownerPlayerIndex) continue;
                }

                if (!string.IsNullOrEmpty(kind))
                {
                    string actual = u is Worker ? "Worker" : u is Archer ? "Archer" : "Warrior";
                    if (!actual.Equals(kind, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                float d = Vector3.Distance(u.transform.position, pos);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            return best;
        }

        public virtual void Select()
        {
            if (!IsOwned) return;

            if (outline != null)
            {
                ApplySelectionVisualColor(selectedOutlineColor);
                outline.enabled = true;
            }

            CmdSetSelected(true);
        }

        public virtual void Deselect()
        {
            if (!IsOwned) return;

            if (outline != null)
                outline.enabled = false;
            if (pathLine != null)
                pathLine.enabled = false;

            CmdSetSelected(false);
        }

        [Command(requiresAuthority = false)]
        private void CmdSetSelected(bool selected)
        {
            isSelected = selected;
        }

        protected void OnSelectedChanged(bool oldV, bool newV)
        {
            UpdateOutline();
            
            // Отключить линию траектории когда юнит снят с выделения
            if (!newV && pathLine != null)
            {
                pathLine.enabled = false;
            }
        }

        protected void OnOwnerConnectionIdChanged(int oldId, int newId)
        {
            // Сброс кеша владельца и обновление outline
            cachedIsOwned = null;
            UpdateOutline();
        }

        protected void OnOwnerPlayerIndexChanged(int oldIndex, int newIndex)
        {
            cachedIsOwned = null;
            UpdateOutline();
        }

        protected void OnOwnerNicknameChanged(string oldNick, string newNick)
        {
            cachedIsOwned = null;
            UpdateOutline();
        }

        protected void OnDestinationChanged(Vector3 oldD, Vector3 newD)
        {
            if (pathLine == null) return;
            if (newD != Vector3.zero)
                UpdatePathVisualization(newD);
            else
                pathLine.enabled = false;
        }

        protected void OnHealthChanged(int oldHp, int newHp)
        {
            if (healthBar != null)
            {
                float normalized = maxHealth > 0 ? (float)newHp / maxHealth : 1f;
                healthBar.UpdateHealthBar(normalized);
            }
        }

        public virtual void MoveTo(Vector3 destination, bool tightArrival = false)
        {
            if (!IsOwned) return;
            destination.y = 0f;
            CmdMoveTo(destination, tightArrival);
        }

        [Command(requiresAuthority = false)]
        protected virtual void CmdMoveTo(Vector3 destination, bool tightArrival)
        {
            if (IsPlayerControlled) return;

            ServerStopCombat();
            OnServerCancelTasksForMove();

            useEnemyPathLine = false;
            destination.y = 0f;
            syncedDestination = destination;

            if (navAgent != null && navAgent.enabled)
            {
                // Иначе при строе юниты останавливаются за 2.5м от слота и армия «размазывана»
                navAgent.stoppingDistance = tightArrival
                    ? Mathf.Max(0.05f, formationStoppingDistance)
                    : stoppingDistance;
            }

            if (!TrySetAgentDestination(destination))
                return;

            RpcSetPathLineMode(false);
            RpcMoveVisual(destination);
        }

        /// <summary>Отменить экономику / спец. задачи перед движением.</summary>
        [Server]
        protected virtual void OnServerCancelTasksForMove() { }

        public void AttackUnit(RTSUnit target)
        {
            if (!IsOwned || target == null || !target.IsAlive) return;
            if (IsSameOwnerAs(target)) return;
            CmdAttackUnit(target.netId);
        }

        public void AttackBuilding(Buildings.Building target)
        {
            if (!IsOwned || target == null) return;
            NetworkIdentity targetIdentity = target.GetComponent<NetworkIdentity>();
            if (targetIdentity == null) return;
            if (target.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                return;
            CmdAttackBuilding(targetIdentity.netId);
        }

        [Command(requiresAuthority = false)]
        private void CmdAttackUnit(uint targetNetId)
        {
            if (IsPlayerControlled) return;
            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity id))
                return;

            RTSUnit target = id.GetComponent<RTSUnit>();
            if (target == null || !target.IsAlive) return;
            if (IsSameOwnerAs(target)) return;

            OnServerCancelTasksForMove();
            ServerStartCombat(target);
        }

        [Command(requiresAuthority = false)]
        private void CmdAttackBuilding(uint targetNetId)
        {
            if (IsPlayerControlled) return;
            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity id))
                return;

            Buildings.Building target = id.GetComponent<Buildings.Building>();
            if (target == null || target.CurrentHealth <= 0) return;
            if (target.IsSameOwner(ownerNickname, ownerPlayerIndex, ownerConnectionId)) return;

            OnServerCancelTasksForMove();
            ServerStartCombatWithBuilding(target);
        }

        [Server]
        protected virtual void ServerStartCombat(RTSUnit target)
        {
            if (IsPlayerControlled)
                return;
            if (target == null || !target.IsAlive)
                return;

            RememberHuntOwnerFromUnit(target);
            RestoreBuildingChaseStoppingDistance();

            attackTarget = target;
            attackTargetBuilding = null;
            syncedAttackTargetNetId = target.netId;
            combatState = UnitCombatState.Chasing;
            useEnemyPathLine = true;
            nextAttackTime = 0f;
            nextChaseRetargetTime = 0f;
            EquipSword(true);

            Vector3 dest = target.transform.position;
            dest.y = 0f;
            syncedDestination = dest;
            TrySetAgentDestination(dest);
            RpcSetPathLineMode(true);
            RpcMoveVisual(dest);
        }

        [Server]
        protected virtual void ServerStartCombatWithBuilding(Buildings.Building target)
        {
            if (IsPlayerControlled)
                return;
            if (target == null || target.CurrentHealth <= 0)
                return;

            RememberHuntOwnerFromBuilding(target);
            ApplyBuildingChaseStoppingDistance();

            attackTarget = null;
            attackTargetBuilding = target;
            NetworkIdentity targetIdentity = target.GetComponent<NetworkIdentity>();
            syncedAttackTargetNetId = targetIdentity != null ? targetIdentity.netId : 0;
            combatState = UnitCombatState.Chasing;
            useEnemyPathLine = true;
            nextAttackTime = 0f;
            nextChaseRetargetTime = 0f;
            EquipSword(true);

            Vector3 dest = GetBuildingAttackDestination(target);
            syncedDestination = dest;
            TrySetAgentDestination(dest);
            RpcSetPathLineMode(true);
            RpcMoveVisual(dest);
        }

        [Server]
        protected virtual void ServerStopCombat()
        {
            combatState = UnitCombatState.None;
            attackTarget = null;
            attackTargetBuilding = null;
            syncedAttackTargetNetId = 0;
            useEnemyPathLine = false;
            ClearHuntOwner();
            RestoreBuildingChaseStoppingDistance();
            EquipSword(false);
            isAnimatingSword = false;
            RpcSetPathLineMode(false);
        }

        [Server]
        protected virtual void ServerUpdateCombat()
        {
            if (combatState == UnitCombatState.None)
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
                if (!ServerTryAcquireNextEnemy())
                {
                    ServerStopCombat();
                    syncedDestination = Vector3.zero;
                }
                return;
            }

            Vector3 facePos;
            float dist;
            Vector3 chaseDest;

            if (hasUnitTarget)
            {
                facePos = attackTarget.transform.position;
                dist = HorizontalDistance(transform.position, facePos);
                chaseDest = facePos;
                chaseDest.y = 0f;
                RestoreBuildingChaseStoppingDistance();
            }
            else
            {
                facePos = attackTargetBuilding.transform.position;
                dist = DistanceToBuildingSurface(attackTargetBuilding);
                chaseDest = GetBuildingAttackDestination(attackTargetBuilding);
                ApplyBuildingChaseStoppingDistance();
            }

            bool mutualEqualRange = hasUnitTarget && IsMutualEqualRangeEngagement(attackTarget);
            float effectiveRange = mutualEqualRange ? attackRange : GetEffectiveAttackRange();

            if (dist > effectiveRange)
            {
                combatState = UnitCombatState.Chasing;
                if (navAgent != null)
                    navAgent.isStopped = false;

                syncedDestination = chaseDest;

                bool needRetarget = Time.time >= nextChaseRetargetTime;
                if (navAgent != null && navAgent.enabled)
                {
                    needRetarget = needRetarget || !navAgent.hasPath || navAgent.remainingDistance < 0.35f ||
                                   HorizontalDistance(navAgent.destination, chaseDest) > 0.5f;
                }

                if (needRetarget)
                {
                    nextChaseRetargetTime = Time.time + chaseRetargetInterval;
                    TrySetAgentDestination(chaseDest);
                }
                return;
            }

            combatState = UnitCombatState.Attacking;
            if (navAgent != null)
                navAgent.isStopped = true;

            FaceTarget(facePos);

            if (nextAttackTime <= 0f)
                nextAttackTime = mutualEqualRange ? Time.time : Time.time + GetHostAttackDelay();

            if (Time.time >= nextAttackTime)
            {
                if (hasUnitTarget && TryResolveSimultaneousMelee(attackTarget))
                    return;

                nextAttackTime = Time.time + attackCooldown;
                RpcSwordSwing();

                if (hasUnitTarget)
                    DealDamageToTarget();
                else if (hasBuildingTarget)
                    DealDamageToBuilding();
            }
        }

        /// <summary>
        /// Оба юнита целятся друг в друга, оба готовы бить, одинаковый attackRange.
        /// </summary>
        [Server]
        private bool IsMutualEqualRangeEngagement(RTSUnit other)
        {
            if (other == null || !other.IsAlive)
                return false;
            if (other.attackTarget != this)
                return false;
            if (Mathf.Abs(attackRange - other.attackRange) > 0.05f)
                return false;
            return true;
        }

        /// <summary>
        /// Одновременный обмен ударами: урон и knockback обоих юнитов в одном тике.
        /// Обрабатывает только юнит с меньшим netId, чтобы не задвоить.
        /// </summary>
        [Server]
        private bool TryResolveSimultaneousMelee(RTSUnit other)
        {
            if (IsPlayerControlled || (other != null && other.IsPlayerControlled))
                return false;
            if (!IsMutualEqualRangeEngagement(other))
                return false;
            if (other.combatState == UnitCombatState.None)
                return false;
            if (other.nextAttackTime > Time.time)
                return false;
            // Лучники / ranged не участвуют в melee exchange
            if (this is Archer || other is Archer)
                return false;
            if (netId > other.netId)
                return false;

            nextAttackTime = Time.time + attackCooldown;
            other.nextAttackTime = Time.time + other.attackCooldown;

            Vector3 dirToOther = other.transform.position - transform.position;
            dirToOther.y = 0f;
            if (dirToOther.sqrMagnitude < 0.001f)
                dirToOther = transform.forward;
            dirToOther.Normalize();
            Vector3 dirToSelf = -dirToOther;

            RpcSwordSwing();
            other.RpcSwordSwing();

            other.ServerTakeDamageFromSimultaneous(attackDamage, dirToOther, this);
            ServerTakeDamageFromSimultaneous(other.attackDamage, dirToSelf, other);
            return true;
        }

        /// <summary>Урон без смены цели (для одновременного обмена ударами).</summary>
        [Server]
        private void ServerTakeDamageFromSimultaneous(int damage, Vector3 knockbackDir, RTSUnit attacker)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            ServerApplyKnockback(knockbackDir.normalized);
            RpcHitFlash();

            if (attacker != null && damage > 0 && attacker.IsPlayerControlled)
            {
                attacker.RpcShowHitmarker();
            }

            if (currentHealth <= 0)
                ServerDie();
        }

        /// <summary>
        /// Компенсация пинга: удалённый игрок бьёт чуть дальше, хост — чуть ближе.
        /// Не применяется при взаимном бое с одинаковым attackRange.
        /// </summary>
        [Server]
        private float GetEffectiveAttackRange()
        {
            float ownerRtt = GetOwnerRttSeconds();
            float range = attackRange;

            if (ownerRtt > 0.025f)
            {
                float bonus = moveSpeed * Mathf.Clamp(ownerRtt, 0f, 0.5f);
                range += Mathf.Min(maxLatencyRangeBonus, bonus);
            }
            else
            {
                float maxRemote = GetMaxRemoteRttSeconds();
                if (maxRemote > 0.025f)
                {
                    float penalty = moveSpeed * Mathf.Clamp(maxRemote * 0.65f, 0f, 0.45f);
                    range -= Mathf.Min(maxHostRangePenalty, penalty);
                    range = Mathf.Max(attackRange * 0.6f, range);
                }
            }

            return range;
        }

        [Server]
        private float GetHostAttackDelay()
        {
            float ownerRtt = GetOwnerRttSeconds();
            if (ownerRtt > 0.025f)
                return 0f;

            float maxRemote = GetMaxRemoteRttSeconds();
            if (maxRemote <= 0.025f)
                return 0f;

            // Хост ждёт ~половину RTT клиента, прежде чем нанести удар
            return Mathf.Clamp(maxRemote * 0.5f, 0f, 0.2f);
        }

        [Server]
        private float GetOwnerRttSeconds()
        {
            if (!NetworkServer.connections.TryGetValue(ownerConnectionId, out NetworkConnectionToClient conn) ||
                conn == null)
                return 0f;
            return Mathf.Max(0f, (float)conn.rtt);
        }

        [Server]
        private static float GetMaxRemoteRttSeconds()
        {
            float max = 0f;
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn == null || conn.identity == null)
                    continue;
                // Локальный хост-клиент обычно connectionId совпадает с host — rtt ~0
                float rtt = (float)conn.rtt;
                if (rtt > max)
                    max = rtt;
            }

            return max;
        }

        [Server]
        private void ServerBroadcastNetState()
        {
            bool agentStopped = navAgent != null && (!navAgent.enabled || navAgent.isStopped);
            if (syncedAgentStopped != agentStopped)
                syncedAgentStopped = agentStopped;

            bool knockedOrSettling = isKnockedBack || isKnockbackSettling;
            if (syncedKnockedBack != knockedOrSettling)
                syncedKnockedBack = knockedOrSettling;

            float interval = knockedOrSettling
                ? 0.04f
                : Mathf.Max(0.05f, netPositionSyncInterval);

            if (Time.time < nextNetPosSyncTime)
                return;

            nextNetPosSyncTime = Time.time + interval;
            Vector3 pos = transform.position;
            if ((pos - syncedNetPosition).sqrMagnitude > 0.0001f)
                syncedNetPosition = pos;
        }

        private void OnSyncedNetPositionChanged(Vector3 oldPos, Vector3 newPos)
        {
            if (isServer)
                return;

            clientCorrectionTarget = newPos;
            hasClientCorrectionTarget = true;
        }

        /// <summary>
        /// Клиент: сам ведёт агента к цели, периодически подтягивается к серверной позиции.
        /// </summary>
        private void ClientUpdatePrediction()
        {
            if (navAgent == null)
                return;

            if (syncedKnockedBack)
            {
                if (navAgent.enabled)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    navAgent.enabled = false;
                }

                // SmoothDamp — меньше дёрганья между снапшотами knockback
                if (hasClientCorrectionTarget)
                {
                    float follow = Mathf.Max(knockbackFollowSpeed, moveSpeed * 3f);
                    transform.position = Vector3.SmoothDamp(
                        transform.position,
                        clientCorrectionTarget,
                        ref clientSmoothVel,
                        0.06f,
                        follow);
                }
                return;
            }

            clientSmoothVel = Vector3.zero;

            if (!navAgent.enabled)
            {
                navAgent.enabled = true;
                if (!navAgent.isOnNavMesh &&
                    NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    navAgent.Warp(hit.position);
            }

            ApplyClientPositionCorrection();

            Vector3 moveTarget = Vector3.zero;
            bool haveTarget = false;

            if (syncedAttackTargetNetId != 0 &&
                NetworkClient.spawned.TryGetValue(syncedAttackTargetNetId, out NetworkIdentity attackId) &&
                attackId != null)
            {
                Buildings.Building asBuilding = attackId.GetComponent<Buildings.Building>();
                if (asBuilding != null)
                {
                    moveTarget = GetBuildingAttackDestination(asBuilding);
                }
                else
                {
                    moveTarget = attackId.transform.position;
                    NavMeshAgent enemyAgent = attackId.GetComponent<NavMeshAgent>();
                    if (enemyAgent != null && enemyAgent.enabled && enemyAgent.velocity.sqrMagnitude > 0.25f)
                        moveTarget += enemyAgent.velocity * 0.12f;
                    moveTarget.y = 0f;
                }
                haveTarget = true;
            }
            else if (syncedDestination != Vector3.zero)
            {
                moveTarget = syncedDestination;
                moveTarget.y = 0f;
                haveTarget = true;
            }

            if (syncedAgentStopped || !haveTarget)
            {
                navAgent.isStopped = true;
                return;
            }

            navAgent.isStopped = false;
            if (!navAgent.pathPending)
            {
                if (!navAgent.hasPath ||
                    HorizontalDistance(navAgent.destination, moveTarget) > 0.4f)
                {
                    TrySetAgentDestination(moveTarget);
                }
            }
        }

        private void ApplyClientPositionCorrection()
        {
            if (!hasClientCorrectionTarget || navAgent == null)
                return;

            Vector3 target = clientCorrectionTarget;
            float err = HorizontalDistance(transform.position, target);

            if (err >= hardCorrectDistance)
            {
                Vector3 warp = target;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    warp = hit.position;

                if (navAgent.enabled)
                    navAgent.Warp(warp);
                else
                    transform.position = warp;
                clientSmoothVel = Vector3.zero;
                return;
            }

            if (err < softCorrectDistance)
                return;

            bool standing = syncedAgentStopped || navAgent.isStopped || !navAgent.hasPath;

            if (standing)
            {
                Vector3 next = Vector3.SmoothDamp(
                    transform.position, target, ref clientSmoothVel, 0.1f, softCorrectSpeed);
                if (navAgent.enabled && navAgent.isOnNavMesh)
                    navAgent.Warp(next);
                else
                    transform.position = next;
            }
            else if (err > 0.45f)
            {
                // Во время бега — мягкий blend вдоль горизонтали, без ломки пути агента
                float t = Mathf.Clamp01(softCorrectSpeed * 0.35f * Time.deltaTime);
                Vector3 blended = transform.position;
                blended.x = Mathf.Lerp(blended.x, target.x, t);
                blended.z = Mathf.Lerp(blended.z, target.z, t);
                transform.position = blended;
            }
        }

        [Server]
        protected bool ServerTryAcquireNextEnemy()
        {
            // После убийства — только цели того же владельца, что у убитой цели
            RTSUnit nearestUnit = hasHuntOwner
                ? FindNearestEnemy(autoAttackSearchRange, true)
                : FindNearestEnemy(autoAttackSearchRange, false);

            Buildings.Building nearestBuilding = hasHuntOwner
                ? FindNearestEnemyBuilding(autoAttackSearchRange, true)
                : FindNearestEnemyBuilding(autoAttackSearchRange, false);

            if (nearestUnit == null && nearestBuilding == null)
                return false;

            float unitDist = nearestUnit != null
                ? HorizontalDistance(transform.position, nearestUnit.transform.position)
                : float.MaxValue;

            float buildingDist = nearestBuilding != null
                ? DistanceToBuildingSurface(nearestBuilding)
                : float.MaxValue;

            if (nearestUnit != null && unitDist <= buildingDist)
                ServerStartCombat(nearestUnit);
            else if (nearestBuilding != null)
                ServerStartCombatWithBuilding(nearestBuilding);
            else
                return false;

            return true;
        }

        [Server]
        protected Buildings.Building FindNearestEnemyBuilding(float range, bool requireHuntOwner)
        {
            Buildings.Building[] buildings = FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
            Buildings.Building best = null;
            float bestDist = float.MaxValue;
            float rangeLimit = range < 0f ? float.MaxValue : range;

            Vector3 selfPos = transform.position;
            for (int i = 0; i < buildings.Length; i++)
            {
                Buildings.Building b = buildings[i];
                if (b == null || !b.CanBeAttackedBy(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                    continue;
                if (requireHuntOwner && !MatchesHuntOwner(b.OwnerNickname, b.OwnerPlayerIndex, b.OwnerConnectionId))
                    continue;

                float d = DistanceToBuildingSurface(b);
                if (d > rangeLimit || d >= bestDist)
                    continue;

                bestDist = d;
                best = b;
            }

            return best;
        }

        [Server]
        protected RTSUnit FindNearestEnemy(float range, bool requireHuntOwner)
        {
            RTSUnit[] units = FindObjectsByType<RTSUnit>(FindObjectsSortMode.None);
            RTSUnit best = null;
            float bestDist = float.MaxValue;
            float rangeLimit = range < 0f ? float.MaxValue : range;

            Vector3 selfPos = transform.position;
            for (int i = 0; i < units.Length; i++)
            {
                RTSUnit u = units[i];
                if (u == null || u == this || !u.IsAlive)
                    continue;
                if (IsSameOwnerAs(u))
                    continue;
                if (requireHuntOwner && !MatchesHuntOwner(u.OwnerNickname, u.OwnerPlayerIndex, u.OwnerConnectionId))
                    continue;

                float d = HorizontalDistance(selfPos, u.transform.position);
                if (d > rangeLimit || d >= bestDist)
                    continue;

                bestDist = d;
                best = u;
            }

            return best;
        }

        protected void RememberHuntOwnerFromUnit(RTSUnit u)
        {
            if (u == null) return;
            hasHuntOwner = true;
            huntOwnerNickname = u.OwnerNickname ?? "";
            huntOwnerPlayerIndex = u.OwnerPlayerIndex;
            huntOwnerConnectionId = u.OwnerConnectionId;
        }

        protected void RememberHuntOwnerFromBuilding(Buildings.Building b)
        {
            if (b == null) return;
            hasHuntOwner = true;
            huntOwnerNickname = b.OwnerNickname ?? "";
            huntOwnerPlayerIndex = b.OwnerPlayerIndex;
            huntOwnerConnectionId = b.OwnerConnectionId;
        }

        protected void ClearHuntOwner()
        {
            hasHuntOwner = false;
            huntOwnerNickname = "";
            huntOwnerPlayerIndex = -1;
            huntOwnerConnectionId = -1;
        }

        protected bool MatchesHuntOwner(string nick, int playerIndex, int connectionId)
        {
            if (!hasHuntOwner)
                return true;
            if (!string.IsNullOrEmpty(huntOwnerNickname) && !string.IsNullOrEmpty(nick))
                return string.Equals(huntOwnerNickname, nick, System.StringComparison.OrdinalIgnoreCase);
            if (huntOwnerPlayerIndex >= 0 && playerIndex >= 0)
                return huntOwnerPlayerIndex == playerIndex;
            return huntOwnerConnectionId == connectionId;
        }

        /// <summary>Дистанция до поверхности коллайдера здания (не до центра).</summary>
        protected float DistanceToBuildingSurface(Buildings.Building building)
        {
            if (building == null)
                return float.MaxValue;

            Collider col = building.GetComponent<Collider>();
            if (col == null)
                return HorizontalDistance(transform.position, building.transform.position);

            Vector3 closest = col.ClosestPoint(transform.position);
            return HorizontalDistance(transform.position, closest);
        }

        /// <summary>Точка подхода у края здания (walkable), а не центр под NavMesh carve.</summary>
        protected Vector3 GetBuildingAttackDestination(Buildings.Building building)
        {
            if (building == null)
                return transform.position;

            Vector3 approach = building.GetApproachPosition(transform.position);
            approach.y = 0f;

            Collider col = building.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 closest = col.ClosestPoint(transform.position);
                Vector3 outward = transform.position - building.transform.position;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.001f)
                    outward = transform.forward;
                outward.Normalize();

                // Чуть снаружи поверхности — агент не упирается в carve
                Vector3 edge = closest + outward * 0.4f;
                edge.y = 0f;

                if (NavMesh.SamplePosition(edge, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    return hit.position;
                if (NavMesh.SamplePosition(approach, out hit, 4f, NavMesh.AllAreas))
                    return hit.position;
                return edge;
            }

            if (NavMesh.SamplePosition(approach, out NavMeshHit approachHit, 4f, NavMesh.AllAreas))
                return approachHit.position;
            return approach;
        }

        [Server]
        private void ApplyBuildingChaseStoppingDistance()
        {
            if (navAgent == null)
                return;
            if (savedStoppingDistance < 0f)
                savedStoppingDistance = navAgent.stoppingDistance;
            // Иначе агент останавливается за 2.5м до точки подхода и не входит в attackRange
            navAgent.stoppingDistance = 0.15f;
        }

        [Server]
        private void RestoreBuildingChaseStoppingDistance()
        {
            if (navAgent == null || savedStoppingDistance < 0f)
                return;
            navAgent.stoppingDistance = savedStoppingDistance;
            savedStoppingDistance = -1f;
        }

        [Server]
        private void DealDamageToTarget()
        {
            if (attackTarget == null || !attackTarget.IsAlive)
                return;

            Vector3 dir = attackTarget.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                dir = transform.forward;
            dir.Normalize();

            attackTarget.ServerTakeDamage(attackDamage, dir, this);
        }

        [Server]
        private void DealDamageToBuilding()
        {
            if (attackTargetBuilding == null || attackTargetBuilding.CurrentHealth <= 0)
                return;

            NetworkIdentity myIdentity = GetComponent<NetworkIdentity>();
            bool wasDestroyed;
            attackTargetBuilding.TakeDamage(attackDamage, myIdentity, out wasDestroyed);

            if (wasDestroyed)
            {
                RememberHuntOwnerFromBuilding(attackTargetBuilding);
                attackTargetBuilding = null;
                if (!ServerTryAcquireNextEnemy())
                {
                    ServerStopCombat();
                    syncedDestination = Vector3.zero;
                }
            }
        }

        [Server]
        public void ServerTakeDamage(int damage, Vector3 knockbackDir, RTSUnit attacker)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            ServerApplyKnockback(knockbackDir.normalized);
            RpcHitFlash();

            if (attacker != null && damage > 0 && attacker.IsPlayerControlled)
            {
                attacker.RpcShowHitmarker();
            }

            // Ответный удар: в бою — смена цели; в простое — начать бой с обидчиком (только AI)
            if (currentHealth > 0 &&
                !IsPlayerControlled &&
                attacker != null &&
                attacker.IsAlive &&
                !IsSameOwnerAs(attacker))
            {
                bool switchTarget = combatState != UnitCombatState.None && attacker != attackTarget;
                bool idleRetaliate = combatState == UnitCombatState.None && CanRetaliateWhileIdle();
                if (switchTarget || idleRetaliate)
                    ServerStartCombat(attacker);
            }

            if (currentHealth <= 0)
                ServerDie();
        }

        [ClientRpc]
        public void RpcShowHitmarker()
        {
            if (UnitPlayerController.Instance != null &&
                UnitPlayerController.Instance.IsActive &&
                UnitPlayerController.Instance.ControlledUnit == this)
            {
                RTSokak.UI.UnitPlayerHud.Instance?.ShowHitmarker();
            }
        }

        /// <summary>Можно ли начать бой в ответ, если юнит сейчас не в бою (стоит без дел).</summary>
        [Server]
        protected virtual bool CanRetaliateWhileIdle() => true;

        /// <summary>
        /// Agent mode = kinematic (NavMesh owns pose). Physics mode = non-kinematic for knockback.
        /// </summary>
        private void SetAgentDrivenPhysics(bool agentDriven)
        {
            if (unitRigidbody == null)
                return;

            if (agentDriven)
            {
                unitRigidbody.linearVelocity = Vector3.zero;
                unitRigidbody.angularVelocity = Vector3.zero;
                unitRigidbody.isKinematic = true;
                unitRigidbody.useGravity = false;
                unitRigidbody.constraints = idleRbConstraints;
            }
            else
            {
                unitRigidbody.isKinematic = false;
                unitRigidbody.useGravity = true;
                unitRigidbody.constraints = knockbackRbConstraints;
            }
        }

        [Server]
        private void ServerApplyKnockback(Vector3 knockbackDir)
        {
            Vector3 flat = knockbackDir;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f)
                flat = transform.forward;
            flat.Normalize();
            // Небольшой разброс — бой выглядит живее, не «по рельсам»
            flat = Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f) * flat;

            float forceScale = 0.9f + Random.Range(0f, 0.35f);
            Vector3 impulse = flat * (knockbackForce * forceScale)
                              + Vector3.up * (knockbackUpForce * (0.85f + Random.Range(0f, 0.3f)));

            if (IsPlayerControlled)
            {
                RpcApplyPlayerKnockback(impulse);
                return;
            }

            if (unitRigidbody == null)
                unitRigidbody = GetComponent<Rigidbody>();
            if (unitRigidbody == null)
            {
                UnityEngine.Debug.LogWarning($"[RTSUnit] {name}: no Rigidbody for knockback");
                return;
            }

            // Новый удар прерывает settle
            isKnockbackSettling = false;

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.enabled = false;
            }

            SetAgentDrivenPhysics(false);
            unitRigidbody.linearVelocity = Vector3.zero;
            unitRigidbody.angularVelocity = Vector3.zero;

            unitRigidbody.AddForce(impulse, ForceMode.Impulse);

            isKnockedBack = true;
            knockbackStartTime = Time.time;
            syncedKnockedBack = true;
            syncedNetPosition = transform.position;
            nextNetPosSyncTime = Time.time;
        }

        [ClientRpc(includeOwner = true)]
        private void RpcApplyPlayerKnockback(Vector3 impulse)
        {
            if (UnitPlayerController.Instance != null &&
                UnitPlayerController.Instance.IsActive &&
                UnitPlayerController.Instance.ControlledUnit == this)
            {
                UnitPlayerController.Instance.ApplyKnockback(impulse);
            }
        }

        [Server]
        private void ServerUpdateKnockback()
        {
            if (isKnockbackSettling)
            {
                UpdateKnockbackSettle();
                return;
            }

            if (!isKnockedBack)
                return;

            float elapsed = Time.time - knockbackStartTime;
            if (elapsed < knockbackMinDuration)
                return;

            if (elapsed < knockbackMaxDuration && !IsKnockbackLanded())
                return;

            BeginKnockbackSettle();
        }

        [Server]
        private bool IsKnockbackLanded()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                return false;

            float baseOff = navAgent != null ? navAgent.baseOffset : 0f;
            float heightAboveStand = transform.position.y - (hit.position.y + baseOff);
            float vy = unitRigidbody != null ? unitRigidbody.linearVelocity.y : 0f;
            return heightAboveStand <= knockbackGroundSnapHeight && vy <= knockbackSettleVerticalSpeed;
        }

        [Server]
        private void BeginKnockbackSettle()
        {
            isKnockedBack = false;
            isKnockbackSettling = true;
            knockbackSettleStartTime = Time.time;
            knockbackSettleFrom = transform.position;

            float baseOff = navAgent != null ? navAgent.baseOffset : 0f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                knockbackSettleTo = hit.position + Vector3.up * baseOff;
            else
                knockbackSettleTo = transform.position;

            if (unitRigidbody != null)
            {
                // Гасим скорость, но оставляем non-kinematic на кадр — позицию ведём вручную
                unitRigidbody.linearVelocity = Vector3.zero;
                unitRigidbody.angularVelocity = Vector3.zero;
                unitRigidbody.isKinematic = true;
                unitRigidbody.useGravity = false;
            }

            syncedKnockedBack = true;
            syncedNetPosition = transform.position;
            nextNetPosSyncTime = Time.time;
        }

        [Server]
        private void UpdateKnockbackSettle()
        {
            float t = knockbackSettleDuration > 0.001f
                ? Mathf.Clamp01((Time.time - knockbackSettleStartTime) / knockbackSettleDuration)
                : 1f;
            // Ease-out cubic — мягкое «прилипание» к земле без телепорта
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(knockbackSettleFrom, knockbackSettleTo, eased);

            if (t < 1f)
            {
                syncedNetPosition = transform.position;
                return;
            }

            FinishKnockbackSettle();
        }

        [Server]
        private void FinishKnockbackSettle()
        {
            isKnockbackSettling = false;
            syncedKnockedBack = false;

            if (IsPlayerControlled)
                return;

            if (unitRigidbody != null)
                SetAgentDrivenPhysics(true);

            if (navAgent == null)
                return;

            navAgent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                navAgent.Warp(hit.position);
            else
                navAgent.Warp(transform.position);

            syncedNetPosition = transform.position;
            nextNetPosSyncTime = Time.time;

            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            navAgent.isStopped = false;

            if (combatState != UnitCombatState.None && attackTarget != null && attackTarget.IsAlive)
            {
                Vector3 dest = attackTarget.transform.position;
                dest.y = 0f;
                syncedDestination = dest;
                nextChaseRetargetTime = 0f;
                TrySetAgentDestination(dest);
            }
        }

        [Server]
        protected virtual void ServerDie()
        {
            ServerStopCombat();
            isKnockedBack = false;
            isKnockbackSettling = false;
            syncedKnockedBack = false;

            if (IsPlayerControlled)
            {
                RTSPlayer ctrl = FindControllingPlayer();
                ServerClearPlayerController();
                if (ctrl != null)
                    ctrl.ServerReleaseControlledUnitToWaiting();
            }

            RTSPlayer owner = FindOwnerPlayer();
            if (owner != null)
                owner.AddCurrentPopulation(-1);

            // Все юниты мертвы → поражение (снос зданий+юнитов, включая этот)
            if (owner != null && owner.Role == PlayerRole.MayorPlaying)
            {
                int remaining = 0;
                if (!string.IsNullOrEmpty(ownerNickname))
                    remaining = WorldSnapshotService.CountUnitsByNickname(ownerNickname) - 1;
                if (remaining <= 0)
                {
                    owner.ServerDemoteMayorToWaitingAfterWipe();
                    return;
                }
            }

            NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        private void RpcHitFlash()
        {
            flashEndTime = Time.time + hitFlashDuration;
            ApplyFlash(true);
        }

        [ClientRpc(includeOwner = true)]
        private void RpcSwordSwing()
        {
            TriggerSwordSwingAnimation();
        }

        public void TriggerSwordSwingAnimation()
        {
            isAnimatingSword = true;
            swordAnimStartTime = Time.time;
            if (swordInstance == null)
                CreateSwordInstance();
        }

        [ClientRpc]
        protected void RpcSetPathLineMode(bool enemy)
        {
            useEnemyPathLine = enemy;
            ApplyPathLineMaterial();
        }

        [ClientRpc]
        protected void RpcMoveVisual(Vector3 destination)
        {
            if (isServer) return;
            syncedDestination = destination;
        }

        private void OnSwordEquippedChanged(bool oldVal, bool newVal)
        {
            SetSwordEquippedVisual(newVal);
        }

        public void SetSwordEquippedVisual(bool equip)
        {
            if (!equip)
            {
                if (swordInstance != null)
                {
                    Destroy(swordInstance);
                    swordInstance = null;
                }
                isAnimatingSword = false;
                return;
            }

            CreateSwordInstance();
        }

        [Server]
        protected void EquipSword(bool equip)
        {
            isSwordEquipped = equip;
            SetSwordEquippedVisual(equip);
            RpcEquipSword(equip);
        }

        [ClientRpc]
        private void RpcEquipSword(bool equip)
        {
            SetSwordEquippedVisual(equip);
        }

        private void CreateSwordInstance()
        {
            if (swordInstance != null || swordPrefab == null || toolHoldPoint == null)
                return;

            swordInstance = Instantiate(swordPrefab, toolHoldPoint);
            swordInstance.transform.localPosition = Vector3.zero;
            swordInstance.transform.localRotation = Quaternion.Euler(toolStartAngle, 0f, 0f);
            swordInstance.transform.localScale = Vector3.one;

            foreach (var col in swordInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        private void AnimateSword()
        {
            if (swordInstance == null || toolHoldPoint == null)
            {
                isAnimatingSword = false;
                return;
            }

            float duration = Mathf.Max(0.05f, attackAnimDuration);
            float t = (Time.time - swordAnimStartTime) / duration;
            if (t >= 1f)
            {
                swordInstance.transform.localRotation = Quaternion.Euler(toolStartAngle, 0f, 0f);
                isAnimatingSword = false;
                return;
            }

            float half = t < 0.5f ? t * 2f : (1f - t) * 2f;
            half = half * half * (3f - 2f * half); // smoothstep
            float angle = Mathf.Lerp(toolStartAngle, toolEndAngle, half);
            swordInstance.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        [Server]
        public void ServerApplyPlayerMove(Vector3 position, float yaw)
        {
            if (!IsPlayerControlled)
                return;

            // Клиент — авторитет позиции (пол/прыжок). Не Warp через NavMesh — ломает Y.
            if (navAgent != null && navAgent.enabled)
                navAgent.enabled = false;

            // Хост сам двигает юнит в UnitPlayerController. Перезапись transform/yaw из Cmd
            // (часто со старым сэмплом) даёт видимое дёрганье. Только SyncVar для зрителей.
            bool hostLocalDriver =
                NetworkClient.active &&
                RTSPlayer.localPlayer != null &&
                RTSPlayer.localPlayer.ControlledUnitNetId == netId;

            if (!hostLocalDriver)
            {
                transform.position = position;
                Vector3 e = transform.eulerAngles;
                e.y = yaw;
                transform.eulerAngles = e;
            }

            syncedNetPosition = position;
            syncedDestination = Vector3.zero;
        }

        /// <summary>Хотбар юнита-игрока: -1 = пустые руки. Worker: 0 Sword, 1 Axe, 2 Pickaxe.</summary>
        [SyncVar]
        protected int playerHotbarSlot = -1;

        public int PlayerHotbarSlot => playerHotbarSlot;

        /// <summary>Кулдаун действия юнита-игрока (для индикатора под прицелом).</summary>
        public virtual float GetPlayerActionCooldown() => attackCooldown;

        [Server]
        public virtual void ServerApplyHotbarSlot(int slotIndex)
        {
            playerHotbarSlot = slotIndex;
            bool hasSword = (this is Warrior && slotIndex == 0) || (!(this is Archer) && slotIndex == 0);
            EquipSword(hasSword);
        }

        /// <summary>Действие юнита-игрока по лучу (переопределяется в Worker/Warrior/Archer).</summary>
        [Server]
        public virtual void ServerPlayerPrimaryAction(Vector3 rayOrigin, Vector3 rayDir)
        {
            if (!IsPlayerControlled)
                return;

            // Без выбранного оружия — ничего
            if (playerHotbarSlot < 0)
                return;

            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + attackCooldown;

            // Всегда анимируем взмах мечом при нажатии игроком
            RpcSwordSwing();

            Vector3 dir = rayDir.sqrMagnitude > 0.001f ? rayDir.normalized : transform.forward;
            float reach = attackRange + 1.2f;

            RaycastHit hit;
            bool didHit = Physics.SphereCast(rayOrigin, 0.45f, dir, out hit, reach, ~0, QueryTriggerInteraction.Ignore);
            if (!didHit)
                didHit = Physics.Raycast(rayOrigin, dir, out hit, reach + 0.5f, ~0, QueryTriggerInteraction.Ignore);

            if (didHit && hit.collider != null)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                    return;

                RTSUnit enemy = hit.collider.GetComponentInParent<RTSUnit>();
                if (enemy != null && enemy.IsAlive && !IsSameOwnerAs(enemy))
                {
                    Vector3 pushDir = enemy.transform.position - transform.position;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.forward;
                    enemy.ServerTakeDamage(attackDamage, pushDir.normalized, this);
                    return;
                }

                Buildings.Building building = hit.collider.GetComponentInParent<Buildings.Building>();
                if (building != null && building.CurrentHealth > 0 &&
                    building.CanBeAttackedBy(ownerNickname, ownerPlayerIndex, ownerConnectionId))
                {
                    building.TakeDamage(attackDamage, unitNetIdentity, out _);
                    if (IsPlayerControlled)
                        RpcShowHitmarker();
                }
            }
        }

        protected bool TrySetAgentDestination(Vector3 destination, float sampleRadius = 8f)
        {
            if (navAgent == null || !navAgent.enabled)
                return false;

            if (!navAgent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                    navAgent.Warp(hit.position);
                else
                    return false;
            }

            if (sampleRadius > 0.01f &&
                NavMesh.SamplePosition(destination, out NavMeshHit destHit, sampleRadius, NavMesh.AllAreas))
                destination = destHit.position;

            navAgent.isStopped = false;
            return navAgent.SetDestination(destination);
        }

        protected void UpdateOutline()
        {
            if (outline == null) return;

            if (UnitPlayerController.Instance != null && UnitPlayerController.Instance.IsActive && UnitPlayerController.Instance.ControlledUnit == this)
            {
                if (!outline.enabled) return;
                outline.enabled = false;
                return;
            }

            bool targetEnabled;
            Outline.Mode targetMode = Outline.Mode.OutlineAll;
            Color targetColor;
            float targetWidth;

            if (IsOwned)
            {
                if (isSelected)
                {
                    targetEnabled = true;
                    targetColor = selectedOutlineColor;
                    targetWidth = outlineWidth;
                }
                else
                {
                    targetEnabled = false;
                    targetColor = outline.OutlineColor;
                    targetWidth = outlineWidth;
                }
            }
            else
            {
                targetEnabled = true;
                targetColor = enemyOutlineColor;
                targetWidth = outlineWidth * 1.15f;
            }

            if (outline.enabled == targetEnabled &&
                (!targetEnabled || (outline.OutlineMode == targetMode &&
                                    outline.OutlineColor == targetColor &&
                                    Mathf.Approximately(outline.OutlineWidth, targetWidth))))
            {
                return;
            }

            outline.OutlineMode = targetMode;
            ApplySelectionVisualColor(targetColor);
            outline.OutlineWidth = targetWidth;
            outline.enabled = targetEnabled;
        }
        
        /// <summary>
        /// Принудительно обновить outline (сбросив кеш IsOwned)
        /// Используется когда localPlayer устанавливается после спавна юнита
        /// </summary>
        public void ForceUpdateOutline()
        {
            cachedIsOwned = null;
            UpdateOutline();
        }

        protected void ApplySelectionVisualColor(Color color)
        {
            if (outline != null)
                outline.OutlineColor = color;
            ApplyPathLineMaterial();
        }

        protected void SetupPathLineMaterials()
        {
            pathLine = GetComponent<LineRenderer>();
            if (pathLine == null)
                pathLine = gameObject.AddComponent<LineRenderer>();

            pathLine.startWidth = pathLineWidth;
            pathLine.endWidth = pathLineWidth * 0.6f;
            pathLine.positionCount = 0;
            pathLine.enabled = false;
            pathLine.useWorldSpace = true;

            Material src = UnityEngine.Resources.Load<Material>("Materials/LineMaterial");
            if (src != null)
            {
                pathLineMaterial = new Material(src);
                pathLineColor = GetMaterialDisplayColor(pathLineMaterial);
            }

            Material enemySrc = UnityEngine.Resources.Load<Material>("Materials/EnemyLineMaterial");
            if (enemySrc != null)
                enemyPathLineMaterial = new Material(enemySrc);

            Material outlineMat = UnityEngine.Resources.Load<Material>("Materials/Outline");
            if (outlineMat != null)
                selectedOutlineColor = GetMaterialDisplayColor(outlineMat);

            Material enemyOutlineMat = UnityEngine.Resources.Load<Material>("Materials/EnemyOutline");
            if (enemyOutlineMat != null)
                enemyOutlineColor = GetMaterialDisplayColor(enemyOutlineMat);

            ApplyPathLineMaterial();
        }

        protected void ApplyPathLineMaterial()
        {
            if (pathLine == null) return;

            Material mat = useEnemyPathLine && enemyPathLineMaterial != null
                ? enemyPathLineMaterial
                : pathLineMaterial;

            if (mat != null)
            {
                pathLine.material = mat;
                pathLine.sharedMaterial = mat;
                Color c = GetMaterialDisplayColor(mat);
                pathLine.startColor = c;
                pathLine.endColor = c;
            }
            else
            {
                pathLine.startColor = useEnemyPathLine ? enemyOutlineColor : pathLineColor;
                pathLine.endColor = pathLine.startColor;
            }
        }

        protected void UpdatePathVisualization(Vector3 destination)
        {
            if (pathLine == null) return;
            if (!IsOwned || !isSelected)
            {
                pathLine.enabled = false;
                return;
            }

            ApplyPathLineMaterial();

            // На клиенте и хосте рисуем ломаную по NavMesh, если путь уже посчитан
            if (TryDrawNavMeshPath(destination))
                return;

            pathLine.enabled = true;
            pathLine.positionCount = 2;
            Vector3 a = transform.position;
            a.y = lineHeight;
            Vector3 b = destination;
            b.y = lineHeight;
            pathLine.SetPosition(0, a);
            pathLine.SetPosition(1, b);
        }

        /// <summary>
        /// Рисует путь по углам NavMesh (и на клиенте — локальный агент предсказывает тот же путь).
        /// </summary>
        protected bool TryDrawNavMeshPath(Vector3 destination)
        {
            if (pathLine == null || navAgent == null || !navAgent.enabled)
                return false;

            NavMeshPath path = navAgent.path;
            if (!navAgent.hasPath || path == null || path.corners == null || path.corners.Length < 2)
            {
                // Если агент ещё не построил путь — попробуем CalculatePath
                path = new NavMeshPath();
                Vector3 from = transform.position;
                if (!NavMesh.CalculatePath(from, destination, NavMesh.AllAreas, path) ||
                    path.corners == null || path.corners.Length < 2)
                    return false;
            }

            pathLine.enabled = true;
            pathLine.positionCount = path.corners.Length;
            for (int i = 0; i < path.corners.Length; i++)
            {
                Vector3 point = path.corners[i];
                if (i == 0)
                    point = transform.position;
                point.y = lineHeight;
                pathLine.SetPosition(i, point);
            }

            return true;
        }

        /// <summary>
        /// Каждый кадр двигает конец линии за живой целью (и начало — за юнитом).
        /// </summary>
        protected void UpdateLivePathLine()
        {
            if (pathLine == null || !IsOwned || !isSelected)
                return;

            if (syncedAttackTargetNetId != 0 &&
                NetworkClient.spawned.TryGetValue(syncedAttackTargetNetId, out NetworkIdentity id) &&
                id != null)
            {
                UpdatePathVisualization(id.transform.position);
                return;
            }

            if (syncedDestination != Vector3.zero)
                UpdatePathVisualization(syncedDestination);
        }

        protected static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected void FaceTarget(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        protected static Color GetMaterialDisplayColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_UnlitColor"))
                return material.GetColor("_UnlitColor");
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            return Color.white;
        }

        private void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var mat = cachedRenderers[i].material;
                originalColors[i] = GetMaterialDisplayColor(mat);
            }
        }

        private void ApplyFlash(bool on)
        {
            if (cachedRenderers == null) return;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] == null) continue;
                var mat = cachedRenderers[i].material;
                Color c = on ? hitFlashColor : originalColors[i];
                if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", c);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_EmissiveColor"))
                    mat.SetColor("_EmissiveColor", on ? hitFlashColor * 2f : Color.black);
            }
        }

        private void UpdateHitFlashVisual()
        {
            if (flashEndTime <= 0f) return;
            if (Time.time >= flashEndTime)
            {
                flashEndTime = 0f;
                ApplyFlash(false);
            }
        }

        protected RTSPlayer FindOwnerPlayer()
        {
            foreach (var p in FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p.ConnectionId == ownerConnectionId)
                    return p;
            }
            return null;
        }

        protected void EnsureUnitNetIdentity()
        {
            if (unitNetIdentity == null)
                unitNetIdentity = GetComponent<NetworkIdentity>();
        }
    }
}
