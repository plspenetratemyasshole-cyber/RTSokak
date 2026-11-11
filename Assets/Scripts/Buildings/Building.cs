using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace RTSokak.Buildings
{
    /// <summary>
    /// Здание: стройка несколькими юнитами, прогресс, снос с возвратом 50% ресурсов.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Building : NetworkBehaviour
    {
        public static readonly List<Building> AllBuildings = new List<Building>();

        protected virtual void OnEnable()
        {
            AllBuildings.Add(this);
        }

        protected virtual void OnDisable()
        {
            AllBuildings.Remove(this);
        }

        [SyncVar]
        private BuildingType buildingType;
        [SerializeField] private float outlineWidth = 2f;

        [SyncVar(hook = nameof(OnOwnerConnectionIdChanged))]
        private int ownerConnectionId = -1;

        [SyncVar(hook = nameof(OnOwnerPlayerIndexChanged))]
        private int ownerPlayerIndex = -1;

        [SyncVar(hook = nameof(OnOwnerNicknameChanged))]
        private string ownerNickname = "";

        [SyncVar(hook = nameof(OnProgressChanged))]
        private float buildProgress;

        [SyncVar(hook = nameof(OnIsCompleteChanged))]
        private bool isComplete;

        [SyncVar(hook = nameof(OnHealthChanged))]
        private int currentHealth = 1000;

        [SyncVar]
        private int maxHealth = 1000;

        [Header("Building Health")]
        [Tooltip("Fallback HP, если в BuildingManager нет maxHealth для типа. Основное значение — BuildingDefinition.maxHealth")]
        [SerializeField] private int startingMaxHealth = 1000;
        [SerializeField] private Color enemyOutlineColor = new Color(1f, 0.08f, 0.08f, 1f);

        [Header("Health Bar")]
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 4.5f, 0f);
        [Tooltip("Если выкл — размер/цвет из RTSokak → Health Bar → Global Settings")]
        [SerializeField] private bool overrideHealthBarBaseValues;
        [SerializeField] private Gradient healthBarGradient;
        [Tooltip("X = длина при Reference HP, Y = толщина. Только при Override")]
        [SerializeField] private Vector2 healthBarSize = new Vector2(0.1f, 0.018f);
        [SerializeField] private float healthBarReferenceHp = 100f;

        [SyncVar]
        private Vector3 finalPosition;

        [SyncVar]
        private float buryDepth = 3f;

        [SyncVar]
        private float buildDuration = 10f;

        [SyncVar]
        private int spentWood;
        [SyncVar]
        private int spentOre;
        [SyncVar]
        private int spentFood;

        [SyncVar]
        private int populationProvided;

        [SyncVar]
        private bool populationGranted;

        [SyncVar]
        private int gridOriginX;
        [SyncVar]
        private int gridOriginZ;
        [SyncVar]
        private int gridSizeX = 1;
        [SyncVar]
        private int gridSizeZ = 1;

        private readonly HashSet<NetworkIdentity> builders = new HashSet<NetworkIdentity>();
        private Outline outline;
        private bool locallySelected;
        private Color selectionColor = new Color(0.15f, 1f, 0.2f, 1f);
        private RTSokak.UI.UnitHealthBar healthBar;
        private RTSokak.UI.UnitHealthBar constructionBar;

        public BuildingType Type => buildingType;
        public int OwnerConnectionId => ownerConnectionId;
        public int OwnerPlayerIndex => ownerPlayerIndex;
        public string OwnerNickname => ownerNickname;
        public float BuildProgress => buildProgress;
        public bool IsComplete => isComplete;
        public bool IsUnderConstruction => !isComplete;
        public int BuilderCount => builders.Count;
        public Vector3 FinalPosition => finalPosition;
        public int SpentWood => spentWood;
        public int SpentOre => spentOre;
        public int SpentFood => spentFood;
        public Vector2Int GridOrigin => new Vector2Int(gridOriginX, gridOriginZ);
        public Vector2Int GridSize => new Vector2Int(Mathf.Max(1, gridSizeX), Mathf.Max(1, gridSizeZ));
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        public event System.Action<Building> OnProgressUpdated;
        public event System.Action<Building> OnConstructionFinished;

        private void Awake()
        {
            outline = GetComponent<Outline>();
            if (outline == null)
                outline = gameObject.AddComponent<Outline>();

            // OutlineAll + сложные FBX даёт односторонний контур (зависит от угла камеры).
            // Visible — как у юнитов; readable meshes нужны для smooth normals.
            outline.OutlineMode = Outline.Mode.OutlineVisible;
            outline.OutlineWidth = outlineWidth;
            outline.enabled = false;
            Material lineMat = UnityEngine.Resources.Load<Material>("Materials/Outline");
            if (lineMat != null)
                selectionColor = GetColorFromMaterial(lineMat);
            outline.OutlineColor = selectionColor;

            // Инициализация здоровья
            if (maxHealth <= 0)
                maxHealth = Mathf.Max(1, startingMaxHealth);
            if (currentHealth <= 0)
                currentHealth = maxHealth;
        }

        private int ResolveMaxHealthFromDefinition(BuildingDefinition def)
        {
            if (def != null && def.maxHealth > 0)
                return def.maxHealth;
            return Mathf.Max(1, startingMaxHealth);
        }

        private static Color GetColorFromMaterial(Material material)
        {
            if (material.HasProperty("_UnlitColor"))
                return material.GetColor("_UnlitColor");
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            return new Color(0.15f, 1f, 0.2f, 1f);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyVerticalProgress();
            UpdateOutline();
            if (isComplete)
                CreateHealthBar();
            else
                CreateConstructionBar();
        }

        /// <summary>Высота бара над финальной позицией здания (одинаковая для HP и стройки).</summary>
        private Vector3 ResolveBarWorldOffset()
        {
            Vector3 offset = healthBarOffset;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                // Высота коллайдера относительно pivot — не зависит от bury
                float top = col.bounds.size.y;
                if (top > 0.5f)
                    offset = new Vector3(0f, top + 0.35f, 0f);
            }
            return offset;
        }

        private Vector3 GetFinalBarWorldPosition()
        {
            Vector3 basePos = finalPosition != Vector3.zero ? finalPosition : transform.position;
            return basePos + ResolveBarWorldOffset();
        }

        private void CreateHealthBar()
        {
            if (healthBar != null)
                return;

            DestroyConstructionBar();

            Vector3 offset = ResolveBarWorldOffset();

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

            healthBar = RTSokak.UI.UnitHealthBar.Create(transform, offset, size, gradient);
            float normalized = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;
            healthBar.UpdateHealthBar(normalized);
        }

        private void CreateConstructionBar()
        {
            if (constructionBar != null || isComplete)
                return;

            var global = RTSokak.UI.HealthBarGlobalSettings.Get();
            Vector2 size = global.SizeForBuildDuration(buildDuration > 0.01f ? buildDuration : global.ReferenceBuildDuration);

            constructionBar = RTSokak.UI.UnitHealthBar.Create(
                transform, ResolveBarWorldOffset(), size, null);
            constructionBar.SetSolidFillColor(global.ConstructionBarColor);
            constructionBar.LockWorldPosition(GetFinalBarWorldPosition());
            constructionBar.UpdateHealthBar(Mathf.Clamp01(buildProgress));
        }

        private void DestroyConstructionBar()
        {
            if (constructionBar == null)
                return;
            Destroy(constructionBar.gameObject);
            constructionBar = null;
        }

        private void OnDestroy()
        {
            if (healthBar != null)
            {
                Destroy(healthBar.gameObject);
                healthBar = null;
            }
            DestroyConstructionBar();
        }

        /// <summary>Вызывать после появления localPlayer (rejoин клиента).</summary>
        public void ForceUpdateOutline()
        {
            UpdateOutline();
        }

        private void UpdateOutline()
        {
            if (outline == null) return;

            bool hasLocalPlayer = RTSokak.Network.RTSPlayer.localPlayer != null;
            bool isEnemy = hasLocalPlayer && !IsOwnedByLocalPlayer();

            bool targetEnabled;
            Outline.Mode targetMode = Outline.Mode.OutlineAll;
            Color targetColor;
            float targetWidth;

            if (isEnemy)
            {
                targetEnabled = true;
                targetMode = Outline.Mode.OutlineAll;
                targetColor = enemyOutlineColor;
                targetWidth = outlineWidth * 1.15f;
            }
            else if (locallySelected)
            {
                targetEnabled = true;
                targetMode = Outline.Mode.OutlineAll;
                targetColor = selectionColor;
                targetWidth = outlineWidth;
            }
            else
            {
                targetEnabled = false;
                targetColor = outline.OutlineColor;
                targetWidth = outline.OutlineWidth;
            }

            if (outline.enabled == targetEnabled &&
                (!targetEnabled || (outline.OutlineMode == targetMode &&
                                    outline.OutlineColor == targetColor &&
                                    Mathf.Approximately(outline.OutlineWidth, targetWidth))))
            {
                return;
            }

            outline.OutlineMode = targetMode;
            outline.OutlineColor = targetColor;
            outline.OutlineWidth = targetWidth;
            outline.enabled = targetEnabled;
        }

        private void Update()
        {
            ApplyVerticalProgress();

            // Держим бар стройки на финальной высоте (здание поднимается отдельно)
            if (constructionBar != null && !isComplete && finalPosition != Vector3.zero)
                constructionBar.LockWorldPosition(GetFinalBarWorldPosition());

            if (isServer && !isComplete && builders.Count > 0 && buildDuration > 0.01f)
            {
                float rate = builders.Count / buildDuration;
                float newProgress = Mathf.Min(1f, buildProgress + rate * Time.deltaTime);
                if (!Mathf.Approximately(newProgress, buildProgress))
                {
                    buildProgress = newProgress;
                    if (buildProgress >= 1f)
                        CompleteConstruction();
                }
            }
        }

        private void OnProgressChanged(float oldValue, float newValue)
        {
            ApplyVerticalProgress();
            if (constructionBar != null)
                constructionBar.UpdateHealthBar(Mathf.Clamp01(newValue));
            OnProgressUpdated?.Invoke(this);
        }

        private void OnOwnerConnectionIdChanged(int oldId, int newId)
        {
            UpdateOutline();
        }

        private void OnOwnerPlayerIndexChanged(int oldIdx, int newIdx)
        {
            UpdateOutline();
        }

        private void OnOwnerNicknameChanged(string oldNick, string newNick)
        {
            UpdateOutline();
        }

        private void OnIsCompleteChanged(bool oldValue, bool newValue)
        {
            UpdateOutline();
            if (newValue)
            {
                DestroyConstructionBar();
                CreateHealthBar();
            }
        }

        private void OnHealthChanged(int oldHp, int newHp)
        {
            if (healthBar != null)
            {
                float normalized = maxHealth > 0 ? (float)newHp / maxHealth : 1f;
                healthBar.UpdateHealthBar(normalized);
            }
        }

        private void ApplyVerticalProgress()
        {
            if (finalPosition == Vector3.zero && !isComplete)
                return;

            float y = finalPosition.y - buryDepth * (1f - Mathf.Clamp01(buildProgress));
            transform.position = new Vector3(finalPosition.x, y, finalPosition.z);
        }

        [Server]
        public void InitializeConstruction(
            int ownerConnId,
            BuildingDefinition def,
            Vector3 finishedWorldPos,
            int wood, int ore, int food,
            Vector2Int origin,
            Vector2Int size)
        {
            ownerConnectionId = ownerConnId;
            ownerPlayerIndex = ResolvePlayerIndex(ownerConnId);
            ownerNickname = ResolvePlayerNickname(ownerConnId);
            buildingType = def.type;
            buildDuration = Mathf.Max(0.1f, def.buildDuration);
            buryDepth = def.buryDepth; // Разрешаем любые значения для анимации подъёма/опускания
            populationProvided = def.populationProvided;
            spentWood = wood;
            spentOre = ore;
            spentFood = food;
            finalPosition = finishedWorldPos;
            gridOriginX = origin.x;
            gridOriginZ = origin.y;
            gridSizeX = Mathf.Max(1, size.x);
            gridSizeZ = Mathf.Max(1, size.y);
            buildProgress = 0f;
            isComplete = false;
            populationGranted = false;

            maxHealth = ResolveMaxHealthFromDefinition(def);
            currentHealth = maxHealth;

            ApplyVerticalProgress(); // Установить начальную высоту на основе buildProgress=0

            if (BuildingManager.Instance != null)
                BuildingManager.Instance.RegisterBuilding(this, GridOrigin, GridSize);
        }

        [Server]
        public void ServerRestoreFromSave(int ownerConnId, BuildingType type, Vector3 finishedWorldPos,
            float progress, bool complete, float duration, float depth,
            int wood, int ore, int food, int popProvided, bool popGranted,
            Vector2Int origin, Vector2Int size)
        {
            ownerConnectionId = ownerConnId;
            if (ownerPlayerIndex < 0)
                ownerPlayerIndex = ResolvePlayerIndex(ownerConnId);
            buildingType = type;
            buildDuration = Mathf.Max(0.1f, duration);
            buryDepth = depth; // Разрешаем любые значения для анимации подъёма/опускания
            populationProvided = popProvided;
            spentWood = wood;
            spentOre = ore;
            spentFood = food;
            finalPosition = finishedWorldPos;
            gridOriginX = origin.x;
            gridOriginZ = origin.y;
            gridSizeX = Mathf.Max(1, size.x);
            gridSizeZ = Mathf.Max(1, size.y);
            buildProgress = Mathf.Clamp01(progress);
            isComplete = complete;
            populationGranted = popGranted;

            BuildingDefinition def = BuildingManager.Instance != null
                ? BuildingManager.Instance.GetDefinition(type)
                : null;
            maxHealth = ResolveMaxHealthFromDefinition(def);
            currentHealth = maxHealth;

            if (isComplete)
                transform.position = finishedWorldPos;
            else
                transform.position = finishedWorldPos + Vector3.down * buryDepth * (1f - buildProgress);

            if (BuildingManager.Instance != null)
                BuildingManager.Instance.RegisterBuilding(this, GridOrigin, GridSize);
        }

        [Server]
        public void ServerSetOwnerConnectionId(int connId) => ownerConnectionId = connId;

        [Server]
        public void ServerSetOwnerPlayerIndex(int playerIndex) => ownerPlayerIndex = playerIndex;

        [Server]
        public void ServerSetOwnerNickname(string nick) => ownerNickname = nick ?? "";

        [Server]
        public bool TryAddBuilder(NetworkIdentity workerIdentity)
        {
            if (isComplete || workerIdentity == null)
                return false;

            builders.Add(workerIdentity);
            return true;
        }

        [Server]
        public void RemoveBuilder(NetworkIdentity workerIdentity)
        {
            if (workerIdentity == null)
                return;

            builders.Remove(workerIdentity);
        }

        [Server]
        private void CompleteConstruction()
        {
            if (isComplete)
                return;

            buildProgress = 1f;
            isComplete = true;
            ApplyVerticalProgress();

            // Сначала уведомляем строителей (до Clear), чтобы фермер сразу продолжил работу
            var builderList = new List<NetworkIdentity>(builders);
            builders.Clear();

            if (!populationGranted)
            {
                populationGranted = true;
                var player = FindPlayer(ownerConnectionId);
                if (player != null)
                {
                    player.AddMaxPopulation(populationProvided);
                    if (buildingType == BuildingType.Capital)
                        player.NotifyCapitalBuilt();
                }
            }

            OnConstructionFinished?.Invoke(this);

            for (int i = 0; i < builderList.Count; i++)
            {
                NetworkIdentity id = builderList[i];
                if (id == null) continue;
                var worker = id.GetComponent<RTSokak.Network.Worker>();
                if (worker != null)
                    worker.ServerOnBuildingCompleted(this);
            }

            UnityEngine.Debug.Log($"[Building] {buildingType} completed for conn {ownerConnectionId}");
        }

        public void RequestDemolish()
        {
            if (!IsOwnedByLocalPlayer()) return;
            CmdDemolish();
        }

        [Command(requiresAuthority = false)]
        private void CmdDemolish(NetworkConnectionToClient sender = null)
        {
            if (sender != null && sender.connectionId != ownerConnectionId)
                return;

            DemolishAndRefund();
        }

        /// <summary>
        /// Снос / отмена: вернуть 50% ресурсов, снять population, уничтожить.
        /// </summary>
        [Server]
        public void DemolishAndRefund()
        {
            var player = FindPlayer(ownerConnectionId);
            if (player != null)
            {
                player.AddWood(spentWood / 2);
                player.AddOre(spentOre / 2);
                player.AddFood(spentFood / 2);

                if (populationGranted)
                {
                    player.AddMaxPopulation(-populationProvided);
                    populationGranted = false;
                }

                if (buildingType == BuildingType.Capital)
                    player.NotifyCapitalDestroyed();
            }

            builders.Clear();

            if (BuildingManager.Instance != null)
                BuildingManager.Instance.UnregisterBuilding(this);

            NetworkServer.Destroy(gameObject);
        }

        public Vector3 GetApproachPosition(Vector3 fromWorld)
        {
            Vector3 center = new Vector3(finalPosition.x, 0f, finalPosition.z);
            if (finalPosition == Vector3.zero)
                center = new Vector3(transform.position.x, 0f, transform.position.z);

            Vector3 dir = fromWorld - center;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                dir = Vector3.forward;
            dir.Normalize();

            float distance = 4f;
            var def = BuildingManager.Instance != null
                ? BuildingManager.Instance.GetDefinition(buildingType)
                : null;
            if (def != null)
                distance = def.approachDistance;

            Vector3 pos = center + dir * distance;
            pos.y = 1f;
            return pos;
        }

        public void Select()
        {
            locallySelected = true;
            UpdateOutline();
        }

        public void Deselect()
        {
            locallySelected = false;
            UpdateOutline();
        }

        public bool IsOwnedByLocalPlayer()
        {
            if (RTSokak.Network.RTSPlayer.localPlayer == null)
                return false;

            // Юнит-игрок: фракция = ник мэра (владельца контролируемого юнита)
            string factionNick = RTSokak.Network.RTSUnit.LocalFactionNickname;
            if (!string.IsNullOrEmpty(ownerNickname) && !string.IsNullOrEmpty(factionNick))
                return string.Equals(ownerNickname, factionNick, System.StringComparison.OrdinalIgnoreCase);

            if (RTSokak.Network.RTSPlayer.localPlayer.Role == RTSokak.Network.PlayerRole.UnitPlaying)
                return false;

            if (ownerPlayerIndex >= 0 && RTSokak.Network.RTSPlayer.localPlayer.PlayerIndex >= 0)
                return ownerPlayerIndex == RTSokak.Network.RTSPlayer.localPlayer.PlayerIndex;
            return ownerConnectionId == RTSokak.Network.RTSPlayer.localPlayer.ConnectionId;
        }

        public bool IsOwnedByNickname(string nick)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(ownerNickname))
                return false;
            return string.Equals(ownerNickname, nick, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSameOwner(string nick, int playerIndex, int connectionId)
        {
            if (!string.IsNullOrEmpty(ownerNickname) && !string.IsNullOrEmpty(nick))
                return string.Equals(ownerNickname, nick, System.StringComparison.OrdinalIgnoreCase);
            if (ownerPlayerIndex >= 0 && playerIndex >= 0)
                return ownerPlayerIndex == playerIndex;
            return ownerConnectionId == connectionId;
        }

        static int ResolvePlayerIndex(int connectionId)
        {
            foreach (var p in Object.FindObjectsByType<RTSokak.Network.RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.ConnectionId == connectionId)
                    return p.PlayerIndex;
            }
            return -1;
        }

        static string ResolvePlayerNickname(int connectionId)
        {
            foreach (var p in Object.FindObjectsByType<RTSokak.Network.RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.ConnectionId == connectionId)
                    return p.Nickname ?? "";
            }
            return "";
        }

        /// <summary>
        /// Проверить можно ли атаковать это здание (чужое и завершенное).
        /// </summary>
        public bool CanBeAttacked(int attackerOwnerConnectionId)
        {
            return isComplete && ownerConnectionId != attackerOwnerConnectionId;
        }

        public bool CanBeAttackedBy(string attackerNick, int attackerPlayerIndex, int attackerConnId)
        {
            if (!isComplete) return false;
            return !IsSameOwner(attackerNick, attackerPlayerIndex, attackerConnId);
        }

        /// <summary>
        /// Получить урон (только на сервере).
        /// </summary>
        [Server]
        public bool TakeDamage(int damage, NetworkIdentity attacker, out bool wasDestroyed)
        {
            wasDestroyed = false;

            if (!isComplete || currentHealth <= 0)
                return false;

            currentHealth = Mathf.Max(0, currentHealth - damage);

            if (currentHealth <= 0)
            {
                wasDestroyed = true;
                DestroyBuilding(attacker);
            }

            return true;
        }

        [Server]
        private void DestroyBuilding(NetworkIdentity destroyer)
        {
            var player = FindPlayer(ownerConnectionId);
            if (player == null && !string.IsNullOrEmpty(ownerNickname))
                player = FindPlayerByNickname(ownerNickname);

            if (player != null)
            {
                if (populationGranted)
                {
                    player.AddMaxPopulation(-populationProvided);
                    populationGranted = false;
                }

                if (buildingType == BuildingType.Capital)
                    player.NotifyCapitalDestroyed();
            }
            else if (buildingType == BuildingType.Capital)
            {
                // Мэр оффлайн — снести поселение и пометить поражение в сейве
                RTSokak.Network.RTSPlayer.ServerOfflineMayorDefeat(ownerNickname, ownerConnectionId);
            }

            builders.Clear();

            if (BuildingManager.Instance != null)
                BuildingManager.Instance.UnregisterBuilding(this);

            NetworkServer.Destroy(gameObject);

            UnityEngine.Debug.Log($"[Building] {buildingType} destroyed by {destroyer?.name ?? "unknown"}");
        }

        private static RTSokak.Network.RTSPlayer FindPlayer(int connId)
        {
            foreach (var p in FindObjectsByType<RTSokak.Network.RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p.ConnectionId == connId)
                    return p;
            }

            return null;
        }

        private static RTSokak.Network.RTSPlayer FindPlayerByNickname(string nick)
        {
            if (string.IsNullOrEmpty(nick))
                return null;
            foreach (var p in FindObjectsByType<RTSokak.Network.RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && !string.IsNullOrEmpty(p.Nickname) &&
                    string.Equals(p.Nickname, nick, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }
    }
}
