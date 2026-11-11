using Mirror;
using UnityEngine;
using UnityEngine.AI;
using RTSokak.UI;
using RTSokak.Graphics;
using RTSokak.Resources;
using RTSokak.Terrain;

namespace RTSokak.Network
{
    /// <summary>
    /// FPS-управление юнитом-игроком: WASD, мышь, прыжок, спринт, ЛКМ.
    /// </summary>
    public class UnitPlayerController : MonoBehaviour
    {
        public static UnitPlayerController Instance { get; private set; }

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Move Defaults")]
        [SerializeField] private float moveSpeedMultiplier = 1f;
        [SerializeField] private float defaultSprintMultiplier = 1.6f;
        [SerializeField] private float defaultJumpForce = 6.5f;
        [SerializeField] private float gravity = -22f;

        [Header("Collision")]
        [SerializeField] private float capsuleRadius = 0.35f;
        [SerializeField] private float capsuleHeight = 1.7f;
        [SerializeField] private float skinWidth = 0.005f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Net")]
        [SerializeField] private float moveSendRate = 12f;

        [Header("Fog (экранный — видно на всех объектах)")]
        [SerializeField] private bool enableUnitFog = false;
        [Tooltip("Цвет тумана. На юните Unit Player → Fog Radius задаёт дальность.")]
        [SerializeField] private Color fogColor = new Color(0.62f, 0.68f, 0.74f, 1f);
        [Tooltip("0 = туман с нуля, 0.35 = прозрачно до 35% радиуса, потом густеет")]
        [Range(0f, 0.7f)]
        [SerializeField] private float fogStartFraction = 0.15f;
        [Tooltip("1 = полный закрас к концу радиуса; 0.7 = слабее")]
        [Range(0.3f, 1.5f)]
        [SerializeField] private float fogIntensity = 1.1f;
        [Tooltip("farClip = радиус * множитель (должен быть > 1, иначе клип раньше тумана)")]
        [SerializeField] private float fogFarClipMul = 1.6f;
        [Tooltip("Подмешивать цвет DayNight к fogColor")]
        [SerializeField] private bool tintFogFromDayNight = true;

        [Header("Distance Culling (FPS Unit Player)")]
        [Tooltip("Включить отсечение (culling) объектов по дистанции для юнита-игрока")]
        [SerializeField] private bool enableDistanceCulling = true;
        [Tooltip("Дистанция скрытия ресурсов (деревья, камни, руда). 0 = без скрытия")]
        [SerializeField] private float resourceCullDistance = 70f;
        [Tooltip("Дистанция скрытия зданий. 0 = без скрытия")]
        [SerializeField] private float buildingCullDistance = 120f;
        [Tooltip("Дистанция скрытия других юнитов. 0 = без скрытия")]
        [SerializeField] private float unitCullDistance = 85f;

        private RTSUnit controlledUnit;
        private UnityEngine.Camera fpsCamera;
        private Transform cameraAnchor;
        private Transform cameraRig;
        private float yaw;
        private float pitch;
        private bool active;
        private GameObject[] hiddenObjects;
        private bool[] hiddenWasActive;
        private float nextOutlineRefreshTime;

        private float verticalVel;
        private bool grounded;
        private float nextMoveSendTime;
        private Vector3 lastSentPos;
        private float lastSentYaw;
        private float cachedMoveSpeed = 3.5f;
        private float cachedSprintMul = 1.6f;
        private float cachedJumpForce = 6.5f;
        private float cachedFogRadius = 45f;
        private float savedShadowDistance = 50f;
        private float savedLodBias = 1f;
        private float savedFarClip = 400f;
        private ShadowQuality savedShadowQuality;
        private CameraClearFlags savedClearFlags = CameraClearFlags.Skybox;

        private bool fogWasEnabled;
        private FogMode fogPrevMode;
        private float fogPrevDensity;
        private float fogPrevStart;
        private float fogPrevEnd;
        private Color fogPrevColor;

        private float cachedGroundY;
        private bool hasCachedGround;
        private Vector3 lastGroundSampleXZ;
        private float nextGroundSampleTime;

        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private readonly RaycastHit[] moveHits = new RaycastHit[8];
        private GameObject grassSystemCached;
        private bool grassWasActive;
        private ProceduralGrassGenerator proceduralGrass;
        private bool proceduralGrassWasEnabled;
        private UnitPlayerScreenFog screenFog;
        private Rigidbody controlledRb;
        private RigidbodyInterpolation savedRbInterpolation;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Clamp(value, 0.05f, 20f);
        }

        public bool IsActive => active && controlledUnit != null;
        public RTSUnit ControlledUnit => controlledUnit;
        public float CurrentYaw => yaw;
        public UnityEngine.Camera FpsCamera => fpsCamera;
        public float FogRadius => cachedFogRadius;

        public bool EnableDistanceCulling => enableDistanceCulling;
        public float ResourceCullDistance
        {
            get => resourceCullDistance;
            set
            {
                resourceCullDistance = value;
                if (UnitPlayerResourceCuller.Instance != null && UnitPlayerResourceCuller.Instance.ResourceCullDistance != value)
                    UnitPlayerResourceCuller.Instance.SetCullDistances(resourceCullDistance, unitCullDistance, buildingCullDistance, true);
            }
        }
        public float BuildingCullDistance
        {
            get => buildingCullDistance;
            set
            {
                buildingCullDistance = value;
                if (UnitPlayerResourceCuller.Instance != null && UnitPlayerResourceCuller.Instance.BuildingCullDistance != value)
                    UnitPlayerResourceCuller.Instance.SetCullDistances(resourceCullDistance, unitCullDistance, buildingCullDistance, true);
            }
        }
        public float UnitCullDistance
        {
            get => unitCullDistance;
            set
            {
                unitCullDistance = value;
                if (UnitPlayerResourceCuller.Instance != null && UnitPlayerResourceCuller.Instance.UnitCullDistance != value)
                    UnitPlayerResourceCuller.Instance.SetCullDistances(resourceCullDistance, unitCullDistance, buildingCullDistance, true);
            }
        }

        /// <summary>
        /// Проверяет, нужно ли прекратить поворот и обновление полоски HP / имени из-за дистанции.
        /// </summary>
        public bool ShouldCullHealthBar(Vector3 worldPos, bool isBuilding)
        {
            if (controlledUnit == null)
                return false;
            float distSqr = (worldPos - controlledUnit.transform.position).sqrMagnitude;
            if (enableUnitFog)
            {
                float r = cachedFogRadius * 1.05f;
                return distSqr > r * r;
            }
            if (enableDistanceCulling)
            {
                float maxDist = isBuilding ? buildingCullDistance : unitCullDistance;
                if (maxDist > 1f)
                    return distSqr > maxDist * maxDist;
            }
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            resourceCullDistance = PlayerPrefs.GetFloat("UnitPlayerResourceCullDist", resourceCullDistance);
            unitCullDistance = PlayerPrefs.GetFloat("UnitPlayerUnitCullDist", unitCullDistance);
            buildingCullDistance = PlayerPrefs.GetFloat("UnitPlayerBuildingCullDist", buildingCullDistance);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Detach();
        }

        public void AttachToUnit(RTSUnit unit)
        {
            Detach();
            if (unit == null)
                return;

            controlledUnit = unit;
            cameraAnchor = unit.UnitPlayerCameraAnchor;
            if (cameraAnchor == null)
            {
                GameObject anchorGo = new GameObject("UnitPlayerCameraAnchor");
                cameraAnchor = anchorGo.transform;
                cameraAnchor.SetParent(unit.transform, false);
                cameraAnchor.localPosition = new Vector3(0f, 1.6f, 0f);
                cameraAnchor.localRotation = Quaternion.identity;
            }

            cameraAnchor.localRotation = Quaternion.identity;

            ReadUnitPlayerSettings(unit);
            EnsureFpsCamera();
            if (cameraRig == null)
            {
                GameObject rigGo = new GameObject("UnitPlayerCameraRig");
                cameraRig = rigGo.transform;
            }
            fpsCamera.transform.SetParent(cameraRig, false);
            fpsCamera.transform.localPosition = Vector3.zero;
            fpsCamera.transform.localRotation = Quaternion.identity;
            fpsCamera.enabled = true;
            fpsCamera.orthographic = false;
            fpsCamera.fieldOfView = 75f;
            fpsCamera.nearClipPlane = 0.05f;
            fpsCamera.allowHDR = false;
            if (enableUnitFog)
                fpsCamera.depthTextureMode |= DepthTextureMode.Depth;
            else
                fpsCamera.depthTextureMode = DepthTextureMode.None;
            savedFarClip = fpsCamera.farClipPlane;
            savedClearFlags = fpsCamera.clearFlags;
            ApplyFogCameraClip();
            EnsureScreenFog();

            var rtsCam = Object.FindFirstObjectByType<RTSokak.Camera.RTSCameraController>();
            if (rtsCam != null)
            {
                var c = rtsCam.GetComponent<UnityEngine.Camera>();
                if (c != null) c.enabled = false;
                rtsCam.enabled = false;
            }

            yaw = unit.transform.eulerAngles.y;
            pitch = 0f;
            verticalVel = 0f;
            lastSentPos = unit.transform.position;
            lastSentYaw = yaw;
            hasCachedGround = false;

            ApplyUnitFog(true);
            HideLocalMeshes(unit);

            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.updateRotation = false;
                agent.updatePosition = false;
                agent.enabled = false;
            }

            var rb = unit.GetComponent<Rigidbody>();
            controlledRb = rb;
            if (rb != null)
            {
                savedRbInterpolation = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            SnapUnitToGround(immediate: true);
            active = true;

            float saved = PlayerPrefs.GetFloat("UnitPlayerMouseSensitivity", mouseSensitivity);
            mouseSensitivity = Mathf.Clamp(saved, 0.05f, 20f);

            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadowQuality = QualitySettings.shadows;
            savedLodBias = QualitySettings.lodBias;
            if (enableUnitFog)
            {
                QualitySettings.shadowDistance = Mathf.Min(savedShadowDistance, Mathf.Max(18f, cachedFogRadius * 0.7f));
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.lodBias = Mathf.Min(savedLodBias, 0.5f);
            }
            else
            {
                // В FPS режиме ограничиваем тени дистанцией отсечения ресурсов (экономит тысячи draw call'ов)
                QualitySettings.shadowDistance = Mathf.Min(savedShadowDistance, Mathf.Clamp(resourceCullDistance, 40f, 85f));
            }

            if (enableDistanceCulling || enableUnitFog)
            {
                var culler = UnitPlayerResourceCuller.Ensure();
                culler.Activate(unit, this);
            }

            grassSystemCached = GameObject.Find("GrassSystem");
            if (grassSystemCached != null)
            {
                grassWasActive = grassSystemCached.activeSelf;
                grassSystemCached.SetActive(false);
            }

            proceduralGrass = Object.FindFirstObjectByType<ProceduralGrassGenerator>();
            if (proceduralGrass != null)
            {
                proceduralGrassWasEnabled = proceduralGrass.gameObject.activeSelf;
                proceduralGrass.SetFpsHidden(true);
            }

            var localGrass = Object.FindFirstObjectByType<ClientLocalGrass>();
            if (localGrass != null)
                localGrass.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            RefreshFactionOutlines();
        }

        public void Detach()
        {
            RestoreHiddenMeshes();
            ApplyUnitFog(false);
            QualitySettings.shadowDistance = savedShadowDistance;
            QualitySettings.shadows = savedShadowQuality;
            QualitySettings.lodBias = savedLodBias;

            if (controlledRb != null)
            {
                controlledRb.interpolation = savedRbInterpolation;
                controlledRb = null;
            }

            if (UnitPlayerResourceCuller.Instance != null)
                UnitPlayerResourceCuller.Instance.Deactivate();

            if (grassSystemCached != null)
            {
                grassSystemCached.SetActive(grassWasActive);
                grassSystemCached = null;
            }

            if (proceduralGrass != null)
            {
                proceduralGrass.SetFpsHidden(false);
                if (proceduralGrassWasEnabled)
                    proceduralGrass.gameObject.SetActive(true);
                proceduralGrass = null;
            }

            var localGrass = Object.FindFirstObjectByType<ClientLocalGrass>(FindObjectsInactive.Include);
            if (localGrass != null)
                localGrass.gameObject.SetActive(true);

            if (controlledUnit != null)
            {
                var agent = controlledUnit.GetComponent<NavMeshAgent>();
                if (agent != null && !controlledUnit.IsPlayerControlled && !agent.enabled)
                    agent.enabled = true;
            }

            active = false;
            controlledUnit = null;
            cameraAnchor = null;
            hasCachedGround = false;

            if (screenFog != null)
            {
                screenFog.enabled = false;
                screenFog = null;
            }

            if (fpsCamera != null)
            {
                fpsCamera.farClipPlane = savedFarClip > 1f ? savedFarClip : 400f;
                fpsCamera.clearFlags = savedClearFlags;
                fpsCamera.enabled = false;
                fpsCamera.transform.SetParent(null, true);
            }

            var rtsCam = Object.FindFirstObjectByType<RTSokak.Camera.RTSCameraController>();
            if (rtsCam != null)
            {
                var c = rtsCam.GetComponent<UnityEngine.Camera>();
                if (c != null)
                {
                    c.enabled = true;
                    c.orthographic = true;
                }
                rtsCam.enabled = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RefreshFactionOutlines();
        }

        private void ReadUnitPlayerSettings(RTSUnit unit)
        {
            cachedMoveSpeed = unit.UnitPlayerMoveSpeed > 0.01f ? unit.UnitPlayerMoveSpeed : 3.5f;
            cachedSprintMul = unit.UnitPlayerSprintMultiplier > 0.01f
                ? unit.UnitPlayerSprintMultiplier
                : defaultSprintMultiplier;
            cachedJumpForce = unit.UnitPlayerJumpForce > 0.01f
                ? unit.UnitPlayerJumpForce
                : defaultJumpForce;
            cachedFogRadius = unit.UnitPlayerFogRadius > 1f ? unit.UnitPlayerFogRadius : 45f;
            moveSpeedMultiplier = 1f;
        }

        private void EnsureFpsCamera()
        {
            if (fpsCamera != null)
                return;

            GameObject go = new GameObject("UnitPlayerFPSCamera");
            fpsCamera = go.AddComponent<UnityEngine.Camera>();
            fpsCamera.nearClipPlane = 0.05f;
            fpsCamera.farClipPlane = 1000f;
            fpsCamera.clearFlags = CameraClearFlags.Skybox;
            fpsCamera.renderingPath = RenderingPath.Forward;
            go.tag = "MainCamera";
            fpsCamera.enabled = false;
        }

        private void ApplyFogCameraClip()
        {
            if (fpsCamera == null)
                return;
            if (enableUnitFog)
            {
                fpsCamera.farClipPlane = Mathf.Max(50f, cachedFogRadius * fogFarClipMul);
                fpsCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
            else
            {
                fpsCamera.farClipPlane = 1000f;
                fpsCamera.depthTextureMode = DepthTextureMode.None;
            }
            fpsCamera.clearFlags = CameraClearFlags.Skybox;
            fpsCamera.renderingPath = RenderingPath.Forward;
        }

        private void EnsureScreenFog()
        {
            if (fpsCamera == null)
                return;
            if (!enableUnitFog)
            {
                if (screenFog != null)
                {
                    Destroy(screenFog);
                    screenFog = null;
                }
                return;
            }
            screenFog = fpsCamera.GetComponent<UnitPlayerScreenFog>();
            if (screenFog == null)
                screenFog = fpsCamera.gameObject.AddComponent<UnitPlayerScreenFog>();
            screenFog.enabled = true;
            PushScreenFogParams();
        }

        private void PushScreenFogParams()
        {
            if (screenFog == null || !enableUnitFog)
                return;

            float end = Mathf.Max(12f, cachedFogRadius);
            float start = Mathf.Clamp(end * fogStartFraction, 0.5f, end * 0.85f);

            Color c = fogColor;
            c.a = 1f;
            if (tintFogFromDayNight)
            {
                // Лёгкий tint от текущего RenderSettings (DayNight пишет цвет)
                Color day = RenderSettings.fogColor;
                c = Color.Lerp(fogColor, day, 0.35f);
                c.a = 1f;
            }

            screenFog.FogColor = c;
            screenFog.FogStart = start;
            screenFog.FogEnd = end;
            screenFog.FogIntensity = fogIntensity;
            screenFog.enabled = true;
        }

        private void LateUpdate()
        {
            if (!active || controlledUnit == null || cameraRig == null || cameraAnchor == null)
                return;

            // Туман каждый кадр — DayNightCycle иначе перебивает / цвет без density
            if (enableUnitFog)
                ForceUnitFogSettings();

            Vector3 targetCamPos = cameraAnchor != null
                ? cameraAnchor.position
                : controlledUnit.transform.position + Vector3.up * 1.6f;
            cameraRig.position = targetCamPos;
            cameraRig.rotation = Quaternion.Euler(pitch, yaw, 0f);
            EnforceHiddenObjects();
        }

        private void Update()
        {
            if (!active || controlledUnit == null || !controlledUnit.IsAlive)
            {
                if (active) Detach();
                return;
            }

            if (RTSPlayer.localPlayer == null ||
                RTSPlayer.localPlayer.Role != PlayerRole.UnitPlaying ||
                RTSPlayer.localPlayer.ControlledUnitNetId != controlledUnit.netId)
            {
                Detach();
                return;
            }

            if (Time.time >= nextOutlineRefreshTime)
            {
                nextOutlineRefreshTime = Time.time + 0.5f;
                RefreshFactionOutlines();
            }

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Look();
            MoveAndJump();

            if (Input.GetMouseButtonDown(0))
                TryPrimaryAction();

            if (Input.GetKeyDown(KeyCode.F6))
                CycleCullDistancePreset();
        }

        private int cullPresetIndex = 1;
        public void CycleCullDistancePreset()
        {
            cullPresetIndex = (cullPresetIndex + 1) % 4;
            switch (cullPresetIndex)
            {
                case 0:
                    SetCullDistances(50f, 65f, 90f);
                    UnityEngine.Debug.Log("[Culling] Preset: Close (Resources: 50m, Units: 65m, Buildings: 90m)");
                    break;
                case 1:
                    SetCullDistances(70f, 85f, 120f);
                    UnityEngine.Debug.Log("[Culling] Preset: Medium (Resources: 70m, Units: 85m, Buildings: 120m)");
                    break;
                case 2:
                    SetCullDistances(95f, 115f, 160f);
                    UnityEngine.Debug.Log("[Culling] Preset: Far (Resources: 95m, Units: 115m, Buildings: 160m)");
                    break;
                case 3:
                    SetCullDistances(140f, 160f, 220f);
                    UnityEngine.Debug.Log("[Culling] Preset: Ultra (Resources: 140m, Units: 160m, Buildings: 220m)");
                    break;
            }
        }

        public void SetCullDistances(float res, float unit, float bld)
        {
            resourceCullDistance = res;
            unitCullDistance = unit;
            buildingCullDistance = bld;
            if (fpsCamera != null && !enableUnitFog)
                QualitySettings.shadowDistance = Mathf.Min(savedShadowDistance, Mathf.Clamp(resourceCullDistance, 40f, 85f));
            if (UnitPlayerResourceCuller.Instance != null)
                UnitPlayerResourceCuller.Instance.SetCullDistances(res, unit, bld, true);
        }

        private void Look()
        {
            float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            yaw += mx;
            pitch = Mathf.Clamp(pitch - my, minPitch, maxPitch);

            controlledUnit.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraAnchor != null)
                cameraAnchor.localRotation = Quaternion.identity;
        }

        private void MoveAndJump()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool jumpPressed = Input.GetKeyDown(KeyCode.Space);

            Vector3 flat = controlledUnit.transform.forward * v + controlledUnit.transform.right * h;
            if (flat.sqrMagnitude > 0.001f)
                flat.Normalize();

            float speed = cachedMoveSpeed * moveSpeedMultiplier * (sprint ? cachedSprintMul : 1f);
            Vector3 wish = flat * (speed * Time.deltaTime);

            Vector3 before = controlledUnit.transform.position;
            float standOffset = GetStandOffset();

            bool isMovingHoriz = wish.sqrMagnitude > 1e-7f;
            if (!hasCachedGround || isMovingHoriz || Time.time >= nextGroundSampleTime)
            {
                if (TryGetGroundSurfaceY(before, out float sy))
                {
                    cachedGroundY = sy;
                    hasCachedGround = true;
                    nextGroundSampleTime = Time.time + 0.05f;
                }
            }

            float standY = hasCachedGround ? cachedGroundY + standOffset : before.y;

            if (jumpPressed && grounded)
            {
                verticalVel = cachedJumpForce;
                grounded = false;
            }

            Vector3 horizontal = ResolveHorizontalMove(before, wish);
            Vector3 pos = before + horizontal;

            if (grounded)
            {
                if (isMovingHoriz && TryGetGroundSurfaceY(pos, out float newSy))
                {
                    cachedGroundY = newSy;
                    standY = newSy + standOffset;
                }

                // Плавное следование за высотой земли без дёрганья от гравитации
                pos.y = Mathf.MoveTowards(pos.y, standY, 30f * Time.deltaTime);
                if (Mathf.Abs(pos.y - standY) < 0.02f)
                    pos.y = standY;

                if (pos.y > standY + 0.4f)
                {
                    grounded = false;
                    verticalVel = 0f;
                }
                else
                {
                    verticalVel = 0f;
                }
            }
            else
            {
                verticalVel += gravity * Time.deltaTime;
                pos.y = before.y + verticalVel * Time.deltaTime;

                if (verticalVel <= 0f && hasCachedGround && pos.y <= standY + 0.12f)
                {
                    pos.y = standY;
                    verticalVel = 0f;
                    grounded = true;
                }
                else if (verticalVel <= 0f && NavMesh.SamplePosition(pos, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas))
                {
                    float navStandY = navHit.position.y + standOffset;
                    if (pos.y <= navStandY + 0.12f)
                    {
                        pos.y = navStandY;
                        verticalVel = 0f;
                        grounded = true;
                        cachedGroundY = navHit.position.y;
                        hasCachedGround = true;
                    }
                }
            }

            controlledUnit.transform.position = pos;

            // Хост уже авторитет: Cmd только для SyncVar наблюдателям.
            // На хосте ServerApplyPlayerMove НЕ должен перезаписывать transform (это давало дёрганье).
            bool movedEnough = (pos - lastSentPos).sqrMagnitude > 0.0025f;
            bool yawChanged = Mathf.Abs(Mathf.DeltaAngle(lastSentYaw, yaw)) > 1.5f;
            if (Time.time >= nextMoveSendTime && RTSPlayer.localPlayer != null && (movedEnough || yawChanged))
            {
                nextMoveSendTime = Time.time + 1f / Mathf.Max(6f, moveSendRate);
                lastSentPos = pos;
                lastSentYaw = yaw;
                RTSPlayer.localPlayer.CmdUnitPlayerMove(controlledUnit.netId, pos, yaw);
            }
        }

        private Vector3 ResolveHorizontalMove(Vector3 from, Vector3 wishDelta)
        {
            if (wishDelta.sqrMagnitude < 1e-8f)
                return Vector3.zero;

            float standOffset = GetStandOffset();
            float height = Mathf.Max(capsuleRadius * 2.1f, capsuleHeight);
            Vector3 feet = from - Vector3.up * (standOffset - 0.1f);
            Vector3 bottom = feet + Vector3.up * capsuleRadius;
            Vector3 top = feet + Vector3.up * (height - capsuleRadius);

            Vector3 remaining = wishDelta;
            Vector3 total = Vector3.zero;

            for (int iter = 0; iter < 2; iter++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f)
                    break;

                Vector3 dir = remaining / dist;
                int hitCount = Physics.CapsuleCastNonAlloc(
                    bottom + total, top + total, capsuleRadius, dir,
                    moveHits, dist + skinWidth, collisionMask, QueryTriggerInteraction.Ignore);

                RaycastHit? best = null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider col = moveHits[i].collider;
                    if (col == null || !IsBlockingObstacle(col))
                        continue;
                    if (moveHits[i].distance < bestDist)
                    {
                        bestDist = moveHits[i].distance;
                        best = moveHits[i];
                    }
                }

                if (!best.HasValue)
                {
                    total += remaining;
                    break;
                }

                float travel = Mathf.Max(0f, best.Value.distance - skinWidth);
                total += dir * travel;

                Vector3 n = best.Value.normal;
                n.y = 0f;
                if (n.sqrMagnitude < 1e-4f)
                    break;
                n.Normalize();
                remaining = Vector3.ProjectOnPlane(remaining - dir * travel, n);
            }

            return total;
        }

        /// <summary>Только ресурсы/здания/юниты — не вся меш-земля (иначе stutter).</summary>
        private bool IsBlockingObstacle(Collider col)
        {
            if (controlledUnit != null &&
                (col.transform.IsChildOf(controlledUnit.transform) || col.transform == controlledUnit.transform))
                return false;

            if (col.GetComponentInParent<Resource>() != null)
                return true;
            if (col.GetComponentInParent<RTSokak.Buildings.Building>() != null)
                return true;
            if (col.GetComponentInParent<RTSUnit>() != null)
                return true;
            return false;
        }

        private static bool IsGroundCollider(Collider col)
        {
            string n = col.gameObject.name;
            return n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Plane", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private float GetStandOffset()
        {
            var agent = controlledUnit != null ? controlledUnit.GetComponent<NavMeshAgent>() : null;
            if (agent != null && agent.baseOffset > 0.01f)
                return agent.baseOffset;
            return 1f;
        }

        private void SnapUnitToGround(bool immediate)
        {
            if (controlledUnit == null)
                return;
            Vector3 pos = controlledUnit.transform.position;
            float standOffset = GetStandOffset();
            if (TryGetGroundSurfaceY(pos, out float surfaceY))
            {
                pos.y = surfaceY + standOffset;
                controlledUnit.transform.position = pos;
                verticalVel = 0f;
                grounded = true;
                cachedGroundY = surfaceY;
                hasCachedGround = true;
            }
            else if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                pos.y = hit.position.y + standOffset;
                controlledUnit.transform.position = pos;
                verticalVel = 0f;
                grounded = true;
                cachedGroundY = hit.position.y;
                hasCachedGround = true;
            }
        }

        private bool TryGetGroundSurfaceY(Vector3 from, out float surfaceY)
        {
            surfaceY = from.y;
            Vector3 origin = new Vector3(from.x, from.y + 1.2f, from.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 6.0f, ~0, QueryTriggerInteraction.Ignore);

            float namedBest = float.MinValue;
            bool namedFound = false;
            float groundBest = float.MinValue;
            bool groundFound = false;

            for (int i = 0; i < count; i++)
            {
                Collider col = groundHits[i].collider;
                if (col == null || col.isTrigger)
                    continue;
                if (controlledUnit != null &&
                    (col.transform.IsChildOf(controlledUnit.transform) || col.transform == controlledUnit.transform))
                    continue;
                if (col.GetComponentInParent<Resource>() != null)
                    continue;
                if (col.GetComponentInParent<RTSUnit>() != null)
                    continue;
                if (col.GetComponentInParent<RTSokak.Buildings.Building>() != null)
                    continue;

                float y = groundHits[i].point.y;
                if (y > from.y + 0.8f)
                    continue;

                if (IsGroundCollider(col))
                {
                    if (!namedFound || y > namedBest)
                    {
                        namedBest = y;
                        namedFound = true;
                    }
                }
                else if (!groundFound || y > groundBest)
                {
                    groundBest = y;
                    groundFound = true;
                }
            }

            if (namedFound)
            {
                surfaceY = namedBest;
                return true;
            }
            if (groundFound)
            {
                surfaceY = groundBest;
                return true;
            }

            if (NavMesh.SamplePosition(from, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                surfaceY = navHit.position.y;
                return true;
            }

            return false;
        }

        private void TryPrimaryAction()
        {
            if (fpsCamera == null || RTSPlayer.localPlayer == null)
                return;

            Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RTSPlayer.localPlayer.CmdUnitPlayerPrimary(controlledUnit.netId, ray.origin, ray.direction);
        }

        private void HideLocalMeshes(RTSUnit unit)
        {
            RestoreHiddenMeshes();

            GameObject[] objs = unit.UnitPlayerLocalHideObjects;
            if (objs == null || objs.Length == 0)
                return;

            var list = new System.Collections.Generic.List<GameObject>(objs.Length);
            var flags = new System.Collections.Generic.List<bool>(objs.Length);
            for (int i = 0; i < objs.Length; i++)
            {
                GameObject go = objs[i];
                if (go == null)
                    continue;
                list.Add(go);
                flags.Add(go.activeSelf);
                go.SetActive(false);
            }
            hiddenObjects = list.ToArray();
            hiddenWasActive = flags.ToArray();
        }

        private void EnforceHiddenObjects()
        {
            if (hiddenObjects == null)
                return;
            for (int i = 0; i < hiddenObjects.Length; i++)
            {
                if (hiddenObjects[i] != null && hiddenObjects[i].activeSelf)
                    hiddenObjects[i].SetActive(false);
            }
        }

        private void RestoreHiddenMeshes()
        {
            if (hiddenObjects == null)
                return;
            for (int i = 0; i < hiddenObjects.Length; i++)
            {
                if (hiddenObjects[i] != null)
                    hiddenObjects[i].SetActive(hiddenWasActive[i]);
            }
            hiddenObjects = null;
            hiddenWasActive = null;
        }

        private void ApplyUnitFog(bool enable)
        {
            if (!enableUnitFog)
            {
                RenderSettings.fog = false;
                if (screenFog != null)
                    screenFog.enabled = false;
                if (fpsCamera != null)
                {
                    fpsCamera.clearFlags = CameraClearFlags.Skybox;
                    fpsCamera.farClipPlane = 1000f;
                }
                return;
            }

            if (enable)
            {
                fogWasEnabled = RenderSettings.fog;
                fogPrevMode = RenderSettings.fogMode;
                fogPrevDensity = RenderSettings.fogDensity;
                fogPrevStart = RenderSettings.fogStartDistance;
                fogPrevEnd = RenderSettings.fogEndDistance;
                fogPrevColor = RenderSettings.fogColor;
                ForceUnitFogSettings();
                ApplyFogCameraClip();
            }
            else
            {
                RenderSettings.fog = fogWasEnabled;
                RenderSettings.fogMode = fogPrevMode;
                RenderSettings.fogDensity = fogPrevDensity;
                RenderSettings.fogStartDistance = fogPrevStart;
                RenderSettings.fogEndDistance = fogPrevEnd;
                RenderSettings.fogColor = fogPrevColor;
            }
        }

        /// <summary>
        /// Экранный fog каждый кадр + far clip. RenderSettings.fog вторичен
        /// (много шейдеров его игнорируют — поэтому был только серый клип).
        /// </summary>
        private void ForceUnitFogSettings()
        {
            if (!enableUnitFog)
            {
                if (screenFog != null)
                    screenFog.enabled = false;
                RenderSettings.fog = false;
                if (fpsCamera != null)
                    fpsCamera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            PushScreenFogParams();
            ApplyFogCameraClip();

            // Опционально дублируем в RenderSettings для Standard-материалов
            float end = Mathf.Max(12f, cachedFogRadius);
            float start = Mathf.Clamp(end * fogStartFraction, 0.5f, end * 0.85f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogColor = fogColor;

            if (fpsCamera != null)
            {
                fpsCamera.backgroundColor = fogColor;
                // Skybox без depth не туманится post-effect'ом так же — solid ближе к туману
                if (fpsCamera.clearFlags == CameraClearFlags.Skybox)
                    fpsCamera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        public static void RefreshFactionOutlines()
        {
            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
                u?.ForceUpdateOutline();
            foreach (var b in Object.FindObjectsByType<RTSokak.Buildings.Building>(FindObjectsSortMode.None))
                b?.ForceUpdateOutline();
        }
    }
}
