using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RTSokak.Buildings;
using RTSokak.Camera;
using RTSokak.Network;
using RTSokak.Resources;

namespace RTSokak.UI
{
    /// <summary>
    /// Миникарта всего мира (низ справа).
    /// Текстура мира обновляется по interval; FOV-обводка — отдельный UI-слой сразу при движении камеры.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MinimapUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Layout")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-16f, 16f);
        [SerializeField] private Vector2 mapSize = new Vector2(220f, 220f);
        [SerializeField] private float borderWidth = 3f;
        [SerializeField] private Color borderColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.08f, 0.06f, 1f);

        [Header("World Bounds")]
        [SerializeField] private bool autoWorldBounds = true;
        [SerializeField] private float worldMinX = -475f;
        [SerializeField] private float worldMaxX = 475f;
        [SerializeField] private float worldMinZ = -475f;
        [SerializeField] private float worldMaxZ = 475f;
        [SerializeField] private float boundsPadding = 0f;

        [Header("Texture / Performance")]
        [SerializeField] private int textureResolution = 128;
        [Tooltip("Как часто перерисовывать точки мира (ресурсы/юниты/здания)")]
        [SerializeField] [Range(0.25f, 5f)] private float worldRefreshInterval = 0.75f;
        [Tooltip("Как часто заново собирать список объектов (дорого)")]
        [SerializeField] [Range(1f, 10f)] private float objectScanInterval = 3f;

        [Header("Resource Dots")]
        [SerializeField] private Color woodColor = new Color(0.35f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color oreColor = new Color(0.65f, 0.7f, 0.8f, 1f);
        [SerializeField] private Color foodColor = new Color(0.95f, 0.8f, 0.25f, 1f);
        [SerializeField] [Range(1, 8)] private int resourceDotRadius = 1;
        [SerializeField] private bool showResources = true;

        [Header("Unit Dots")]
        [SerializeField] private Color ownUnitColor = new Color(0.2f, 0.95f, 0.3f, 1f);
        [SerializeField] private Color enemyUnitColor = new Color(1f, 0.15f, 0.12f, 1f);
        [SerializeField] [Range(1, 10)] private int unitDotRadius = 2;
        [SerializeField] private bool showUnits = true;

        [Header("Building Dots")]
        [SerializeField] private Color ownBuildingColor = new Color(0.15f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color enemyBuildingColor = new Color(1f, 0.1f, 0.1f, 1f);
        [SerializeField] [Range(1, 12)] private int buildingDotRadius = 3;
        [SerializeField] private bool showBuildings = true;
        [SerializeField] private bool showIncompleteBuildings = true;

        [Header("Camera FOV Outline (Mayor / World)")]
        [SerializeField] private bool showCameraFov = true;
        [SerializeField] private Color fovOutlineColor = new Color(0.25f, 1f, 0.35f, 1f);
        [SerializeField] [Range(1f, 6f)] private float fovLineThickness = 2f;
        [SerializeField] private float groundPlaneY = 0f;

        [Header("Player Unit Minimap")]
        [SerializeField] private Color playerFovColor = new Color(1f, 0.92f, 0.016f, 1f);
        [SerializeField] [Range(1f, 6f)] private float playerFovLineThickness = 2f;
        [SerializeField] [Range(20f, 300f)] private float playerFovLength = 110f;
        [SerializeField] private float playerFovAngle = 75f;
        [SerializeField] private Color playerMarkerColor = new Color(1f, 0.92f, 0.016f, 1f);
        [SerializeField] private float playerMarkerSize = 10f;

        [Header("Click")]
        [SerializeField] private bool clickToTeleport = true;

        [Header("References (optional)")]
        [SerializeField] private RawImage mapImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private RectTransform fovOverlay;
        [SerializeField] private RTSCameraController cameraController;
        [SerializeField] private UnityEngine.Camera worldCamera;

        private Texture2D mapTexture;
        private Color32[] pixels;
        private Color32[] resourceLayerPixels;
        private bool resourceLayerDirty = true;
        private int lastResourceCount = -1;
        private RectTransform rectTransform;
        private RectTransform mapRect;

        private float nextWorldRedrawTime;
        private float nextObjectScanTime;
        private bool worldDirty = true;

        // Редкий scan объектов; позиции юнитов/зданий обновляются при каждой перерисовке текстуры
        private readonly List<Vector3> resourcePositions = new List<Vector3>(1024);
        private readonly List<byte> resourceTypes = new List<byte>(1024);
        private readonly List<RTSUnit> trackedUnits = new List<RTSUnit>(128);
        private readonly List<Building> trackedBuildings = new List<Building>(64);

        // FOV: 4 линии UI для камеры мэра
        private readonly RectTransform[] fovLines = new RectTransform[4];
        private readonly Image[] fovLineImages = new Image[4];
        private Vector3 lastCamPos;
        private Quaternion lastCamRot;
        private float lastCamOrtho;
        private bool hasCamSample;

        // FOV: 3 линии UI (конус) и кружочек для юнита-игрока
        private readonly RectTransform[] playerFovLines = new RectTransform[3];
        private readonly Image[] playerFovLineImages = new Image[3];
        private RectTransform playerMarker;
        private Image playerMarkerImage;

        // Пульсация точек Столицы и Складов при полном инвентаре Worker
        private readonly List<RectTransform> depositPulseRings = new List<RectTransform>(8);
        private readonly List<Image> depositPulseImages = new List<Image>(8);

        private static readonly Vector3[] ViewportCorners =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f)
        };

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            EnsureLayout();
            EnsureFovOverlay();
            EnsureTexture();
            ResolveCamera();
            TryAutoBounds();
            ForceWorldRedraw();
        }

        private void OnEnable()
        {
            worldDirty = true;
            resourceLayerDirty = true;
            nextWorldRedrawTime = 0f;
            nextObjectScanTime = 0f;
            hasCamSample = false;
        }

        private void OnDestroy()
        {
            if (mapTexture != null)
            {
                Destroy(mapTexture);
                mapTexture = null;
            }
        }

        private void LateUpdate()
        {
            ResolveCamera();

            if (Time.unscaledTime >= nextObjectScanTime)
            {
                nextObjectScanTime = Time.unscaledTime + Mathf.Max(1f, objectScanInterval);
                ScanWorldObjects();
            }

            if (Time.unscaledTime >= nextWorldRedrawTime)
            {
                nextWorldRedrawTime = Time.unscaledTime + Mathf.Max(0.2f, worldRefreshInterval);
                if (autoWorldBounds)
                    TryAutoBounds();
                RedrawWorldTexture();
                worldDirty = false;
            }

            // FOV мэра/камеры — каждый кадр при изменении камеры
            if (showCameraFov)
                UpdateFovOverlayImmediate();
            else
                HideFovLines();

            // FOV и маркер юнита-игрока (желтый конус и желтый кружочек)
            UpdatePlayerUnitOverlay();

            // Пульсация точек Столицы и Складов при полном инвентаре Worker
            UpdateFullInventoryDepositPulse();
        }

        private void ForceWorldRedraw()
        {
            resourceLayerDirty = true;
            nextWorldRedrawTime = 0f;
            RedrawWorldTexture();
            worldDirty = false;
        }

        private void EnsureLayout()
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = mapSize + Vector2.one * (borderWidth * 2f);

            if (borderImage == null)
            {
                borderImage = GetComponent<Image>();
                if (borderImage == null)
                    borderImage = gameObject.AddComponent<Image>();
            }

            // Без спрайта Image рисует белый квад — даём 1x1 и тёмный цвет
            if (borderImage.sprite == null)
                borderImage.sprite = GetWhiteSprite();
            borderImage.type = Image.Type.Simple;
            borderImage.color = borderColor;
            borderImage.raycastTarget = false;

            if (mapImage == null)
            {
                Transform existing = transform.Find("Map");
                if (existing != null)
                    mapImage = existing.GetComponent<RawImage>();
            }

            if (mapImage == null)
            {
                GameObject mapGo = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer));
                mapGo.transform.SetParent(transform, false);
                mapRect = mapGo.GetComponent<RectTransform>();
                mapImage = mapGo.AddComponent<RawImage>();
            }
            else
            {
                mapRect = mapImage.rectTransform;
            }

            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.one * borderWidth;
            mapRect.offsetMax = -Vector2.one * borderWidth;

            mapImage.raycastTarget = true;
            mapImage.color = Color.white;
            mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        private void EnsureFovOverlay()
        {
            if (mapRect == null && mapImage != null)
                mapRect = mapImage.rectTransform;

            if (fovOverlay == null)
            {
                Transform existing = transform.Find("FovOverlay");
                if (existing != null)
                    fovOverlay = existing as RectTransform;
            }

            if (fovOverlay == null)
            {
                GameObject go = new GameObject("FovOverlay", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                fovOverlay = go.GetComponent<RectTransform>();
            }

            fovOverlay.SetAsLastSibling();
            fovOverlay.anchorMin = Vector2.zero;
            fovOverlay.anchorMax = Vector2.one;
            fovOverlay.offsetMin = Vector2.one * borderWidth;
            fovOverlay.offsetMax = -Vector2.one * borderWidth;

            Sprite white = GetWhiteSprite();
            // 4 линии FOV мэра/камеры
            for (int i = 0; i < 4; i++)
            {
                if (fovLines[i] != null)
                    continue;

                Transform child = fovOverlay.Find($"Line{i}");
                GameObject lineGo;
                if (child != null)
                {
                    lineGo = child.gameObject;
                }
                else
                {
                    lineGo = new GameObject($"Line{i}", typeof(RectTransform), typeof(CanvasRenderer));
                    lineGo.transform.SetParent(fovOverlay, false);
                }

                fovLines[i] = lineGo.GetComponent<RectTransform>();
                fovLineImages[i] = lineGo.GetComponent<Image>();
                if (fovLineImages[i] == null)
                    fovLineImages[i] = lineGo.AddComponent<Image>();

                fovLineImages[i].sprite = white;
                fovLineImages[i].color = fovOutlineColor;
                fovLineImages[i].raycastTarget = false;
                fovLines[i].pivot = new Vector2(0.5f, 0.5f);
            }

            // 3 линии FOV конуса юнита-игрока (желтые)
            for (int i = 0; i < 3; i++)
            {
                if (playerFovLines[i] != null)
                    continue;

                Transform child = fovOverlay.Find($"PlayerFovLine{i}");
                GameObject lineGo;
                if (child != null)
                {
                    lineGo = child.gameObject;
                }
                else
                {
                    lineGo = new GameObject($"PlayerFovLine{i}", typeof(RectTransform), typeof(CanvasRenderer));
                    lineGo.transform.SetParent(fovOverlay, false);
                }

                playerFovLines[i] = lineGo.GetComponent<RectTransform>();
                playerFovLineImages[i] = lineGo.GetComponent<Image>();
                if (playerFovLineImages[i] == null)
                    playerFovLineImages[i] = lineGo.AddComponent<Image>();

                playerFovLineImages[i].sprite = white;
                playerFovLineImages[i].color = playerFovColor;
                playerFovLineImages[i].raycastTarget = false;
                playerFovLines[i].pivot = new Vector2(0.5f, 0.5f);
                playerFovLines[i].gameObject.SetActive(false);
            }

            // Желтый кружочек — сам юнит-игрок
            if (playerMarker == null)
            {
                Transform child = fovOverlay.Find("PlayerMarker");
                GameObject markerGo;
                if (child != null)
                {
                    markerGo = child.gameObject;
                }
                else
                {
                    markerGo = new GameObject("PlayerMarker", typeof(RectTransform), typeof(CanvasRenderer));
                    markerGo.transform.SetParent(fovOverlay, false);
                }

                playerMarker = markerGo.GetComponent<RectTransform>();
                playerMarkerImage = markerGo.GetComponent<Image>();
                if (playerMarkerImage == null)
                    playerMarkerImage = markerGo.AddComponent<Image>();

                playerMarkerImage.sprite = GetCircleSprite();
                playerMarkerImage.color = playerMarkerColor;
                playerMarkerImage.raycastTarget = false;
                playerMarker.pivot = new Vector2(0.5f, 0.5f);
                playerMarker.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);
                playerMarker.gameObject.SetActive(false);
            }

            // Пул индикаторов пульсации для сдачи ресурсов (до 8 штук)
            Sprite circle = GetCircleSprite();
            while (depositPulseRings.Count < 8)
            {
                int idx = depositPulseRings.Count;
                Transform child = fovOverlay.Find($"DepositPulse_{idx}");
                GameObject pulseGo;
                if (child != null)
                {
                    pulseGo = child.gameObject;
                }
                else
                {
                    pulseGo = new GameObject($"DepositPulse_{idx}", typeof(RectTransform), typeof(CanvasRenderer));
                    pulseGo.transform.SetParent(fovOverlay, false);
                }

                RectTransform rt = pulseGo.GetComponent<RectTransform>();
                Image img = pulseGo.GetComponent<Image>();
                if (img == null)
                    img = pulseGo.AddComponent<Image>();

                img.sprite = circle;
                img.raycastTarget = false;
                rt.pivot = new Vector2(0.5f, 0.5f);
                pulseGo.SetActive(false);

                depositPulseRings.Add(rt);
                depositPulseImages.Add(img);
            }
        }

        private void EnsureTexture()
        {
            int res = Mathf.Clamp(textureResolution, 64, 512);
            if (mapTexture != null && mapTexture.width == res && pixels != null && pixels.Length == res * res)
            {
                if (mapImage != null && mapImage.texture != mapTexture)
                    mapImage.texture = mapTexture;
                return;
            }

            if (mapTexture != null)
                Destroy(mapTexture);

            mapTexture = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinimapTexture"
            };
            pixels = new Color32[res * res];

            // Сразу залить фон — иначе RawImage может выглядеть белым до первого Apply
            Color32 bg = backgroundColor;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bg;
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply(false, false);

            if (mapImage != null)
                mapImage.texture = mapTexture;
        }

        private void ResolveCamera()
        {
            if (cameraController == null)
                cameraController = Object.FindFirstObjectByType<RTSCameraController>();
            if (worldCamera == null && cameraController != null)
                worldCamera = cameraController.GetComponent<UnityEngine.Camera>();
            if (worldCamera == null)
                worldCamera = UnityEngine.Camera.main;
        }

        static RTSPlayer cachedMayorForUnitPlayer;
        static float nextMayorCacheTime;

        /// <summary>Мэр, чью камеру показывать юниту-игроку на FOV-обводке.</summary>
        private static RTSPlayer FindMayorForLocalUnitPlayer()
        {
            var lp = RTSPlayer.localPlayer;
            if (lp == null || lp.Role != PlayerRole.UnitPlaying)
            {
                cachedMayorForUnitPlayer = null;
                return null;
            }

            if (cachedMayorForUnitPlayer != null &&
                cachedMayorForUnitPlayer.Role == PlayerRole.MayorPlaying &&
                Time.unscaledTime < nextMayorCacheTime)
                return cachedMayorForUnitPlayer;

            nextMayorCacheTime = Time.unscaledTime + 1f;
            cachedMayorForUnitPlayer = null;

            string faction = RTSUnit.LocalFactionNickname;
            if (string.IsNullOrEmpty(faction))
                return null;

            foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p == null || p.Role != PlayerRole.MayorPlaying)
                    continue;
                if (string.Equals(p.Nickname, faction, System.StringComparison.OrdinalIgnoreCase))
                {
                    cachedMayorForUnitPlayer = p;
                    return p;
                }
            }
            return null;
        }

        private void TryAutoBounds()
        {
            if (!autoWorldBounds)
                return;

            var rm = Object.FindFirstObjectByType<ResourceManager>();
            if (rm == null)
                return;

            float half = rm.WorldSize * 0.5f;
            if (half < 1f)
                return;

            float pad = boundsPadding;
            worldMinX = -half - pad;
            worldMaxX = half + pad;
            worldMinZ = -half - pad;
            worldMaxZ = half + pad;
        }

        private void ScanWorldObjects()
        {
            if (showResources)
            {
                int count = Resource.AllResources.Count;
                if (count != lastResourceCount)
                {
                    lastResourceCount = count;
                    resourceLayerDirty = true;
                }
            }
        }

        private void RebuildResourceLayer()
        {
            EnsureTexture();
            if (pixels == null)
                return;

            int res = mapTexture.width;
            int len = res * res;
            if (resourceLayerPixels == null || resourceLayerPixels.Length != len)
                resourceLayerPixels = new Color32[len];

            Color32 bg = backgroundColor;
            for (int i = 0; i < len; i++)
                resourceLayerPixels[i] = bg;

            if (showResources)
            {
                var resources = Resource.AllResources;
                for (int i = 0; i < resources.Count; i++)
                {
                    Resource r = resources[i];
                    if (r == null || r.IsHarvested)
                        continue;
                    Color32 c = r.Type switch
                    {
                        ResourceType.Wood => woodColor,
                        ResourceType.Ore => oreColor,
                        ResourceType.Food => foodColor,
                        _ => (Color32)Color.white
                    };
                    DrawDotWorldToBuffer(resourceLayerPixels, res, r.transform.position, resourceDotRadius, c);
                }
            }

            resourceLayerDirty = false;
        }

        private void RedrawWorldTexture()
        {
            EnsureTexture();
            if (mapTexture == null || pixels == null)
                return;

            int res = mapTexture.width;
            int len = res * res;

            if (resourceLayerDirty || resourceLayerPixels == null || resourceLayerPixels.Length != len)
            {
                RebuildResourceLayer();
            }

            System.Array.Copy(resourceLayerPixels, pixels, len);

            if (showBuildings)
            {
                bool isWorkerFull = UnitPlayerController.Instance != null &&
                                    UnitPlayerController.Instance.IsActive &&
                                    UnitPlayerController.Instance.ControlledUnit is Worker pw &&
                                    pw.IsInventoryFull;

                var buildings = Building.AllBuildings;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building b = buildings[i];
                    if (b == null)
                        continue;
                    if (!showIncompleteBuildings && !b.IsComplete)
                        continue;

                    Vector3 pos = b.FinalPosition != Vector3.zero ? b.FinalPosition : b.transform.position;
                    bool isDepositPulse = isWorkerFull && b.IsComplete && b.IsOwnedByLocalPlayer() &&
                                          (b.Type == BuildingType.Capital || b.Type == BuildingType.Warehouse);

                    int dotRadius = isDepositPulse ? buildingDotRadius + 2 : buildingDotRadius;
                    Color32 c = isDepositPulse
                        ? new Color32(255, 235, 45, 255)
                        : (b.IsOwnedByLocalPlayer() ? ownBuildingColor : enemyBuildingColor);

                    DrawDotWorld(res, pos, dotRadius, c);
                }
            }

            if (showUnits)
            {
                var units = RTSUnit.AllUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    RTSUnit u = units[i];
                    if (u == null || !u.IsAlive)
                        continue;

                    Color32 c = u.IsOwned ? ownUnitColor : enemyUnitColor;
                    DrawDotWorld(res, u.transform.position, unitDotRadius, c);
                }
            }

            mapTexture.SetPixels32(pixels);
            mapTexture.Apply(false, false);

            if (mapImage != null && mapImage.texture != mapTexture)
                mapImage.texture = mapTexture;
        }

        private void UpdateFovOverlayImmediate()
        {
            if (fovOverlay == null || mapRect == null)
                return;

            if (!fovOverlay.gameObject.activeSelf)
                fovOverlay.gameObject.SetActive(true);

            // Юнит-игрок: виртуальная ortho-камера мэра → зеленые линии
            RTSPlayer mayor = FindMayorForLocalUnitPlayer();
            if (mayor != null && mayor.HasCameraView)
            {
                float zoom = Mathf.Max(1f, mayor.CameraZoom);
                Vector3 pos = mayor.CameraPosition;
                float yaw = mayor.CameraYaw;

                bool moved = !hasCamSample ||
                             (pos - lastCamPos).sqrMagnitude > 0.01f ||
                             Mathf.Abs(zoom - lastCamOrtho) > 0.01f ||
                             Mathf.Abs(Mathf.DeltaAngle(lastCamRot.eulerAngles.y, yaw)) > 0.1f;
                if (moved)
                {
                    lastCamPos = pos;
                    lastCamRot = Quaternion.Euler(0f, yaw, 0f);
                    lastCamOrtho = zoom;
                    hasCamSample = true;

                    if (!TryDrawFovFromVirtualOrtho(pos, zoom, yaw))
                        HideFovLines();
                }
                return;
            }

            if (worldCamera == null || !worldCamera.orthographic || !worldCamera.enabled)
            {
                HideFovLines();
                return;
            }

            Transform camT = worldCamera.transform;
            float ortho = worldCamera.orthographicSize;

            bool camMoved = !hasCamSample ||
                            (camT.position - lastCamPos).sqrMagnitude > 0.0001f ||
                            Quaternion.Angle(camT.rotation, lastCamRot) > 0.05f ||
                            Mathf.Abs(ortho - lastCamOrtho) > 0.001f;
            if (camMoved)
            {
                lastCamPos = camT.position;
                lastCamRot = camT.rotation;
                lastCamOrtho = ortho;
                hasCamSample = true;

                // Реальные углы viewport → земля
                Vector2[] localCorners = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!TryViewportToGround(ViewportCorners[i], out Vector3 ground))
                    {
                        HideFovLines();
                        return;
                    }
                    localCorners[i] = WorldToMapLocal(ground);
                }

                DrawFovLines(localCorners);
            }
        }

        /// <summary>
        /// Отображение FOV камеры и маркера для юнита-игрока (желтый цвет).
        /// </summary>
        private void UpdatePlayerUnitOverlay()
        {
            if (fovOverlay == null || mapRect == null)
                return;

            var lp = RTSPlayer.localPlayer;
            if (lp == null || lp.Role != PlayerRole.UnitPlaying)
            {
                HidePlayerUnitOverlay();
                return;
            }

            RTSUnit unit = null;
            if (UnitPlayerController.Instance != null && UnitPlayerController.Instance.IsActive)
                unit = UnitPlayerController.Instance.ControlledUnit;

            if (unit == null)
            {
                var units = RTSUnit.AllUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u != null && u.IsAlive && u.IsOwned && u.IsPlayerControlled)
                    {
                        unit = u;
                        break;
                    }
                }
            }

            if (unit == null || !unit.IsAlive)
            {
                HidePlayerUnitOverlay();
                return;
            }

            Vector3 unitPos = unit.transform.position;
            float yaw = unit.transform.eulerAngles.y;
            if (UnitPlayerController.Instance != null && UnitPlayerController.Instance.IsActive)
            {
                yaw = UnitPlayerController.Instance.CurrentYaw;
            }

            // Позиция маркера
            Vector2 mapPos = WorldToMapLocal(unitPos);
            Rect r = mapRect.rect;
            mapPos.x = Mathf.Clamp(mapPos.x, r.xMin, r.xMax);
            mapPos.y = Mathf.Clamp(mapPos.y, r.yMin, r.yMax);

            if (playerMarker != null)
            {
                playerMarker.gameObject.SetActive(true);
                playerMarker.anchoredPosition = mapPos;
                playerMarker.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);
                if (playerMarkerImage != null)
                    playerMarkerImage.color = playerMarkerColor;
            }

            // Конус обзора: вершина (юнит) и 2 луча (влево и вправо)
            float halfAngle = playerFovAngle * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0f, yaw - halfAngle, 0f) * Vector3.forward;
            Vector3 rightDir = Quaternion.Euler(0f, yaw + halfAngle, 0f) * Vector3.forward;

            Vector3 worldLeft = unitPos + leftDir * playerFovLength;
            Vector3 worldRight = unitPos + rightDir * playerFovLength;

            Vector2 mapLeft = WorldToMapLocal(worldLeft);
            Vector2 mapRight = WorldToMapLocal(worldRight);

            mapLeft.x = Mathf.Clamp(mapLeft.x, r.xMin, r.xMax);
            mapLeft.y = Mathf.Clamp(mapLeft.y, r.yMin, r.yMax);
            mapRight.x = Mathf.Clamp(mapRight.x, r.xMin, r.xMax);
            mapRight.y = Mathf.Clamp(mapRight.y, r.yMin, r.yMax);

            // Рисуем 3 желтые линии треугольника FOV
            if (playerFovLines[0] != null)
            {
                playerFovLineImages[0].color = playerFovColor;
                PlaceLine(playerFovLines[0], mapPos, mapLeft, playerFovLineThickness);
            }
            if (playerFovLines[1] != null)
            {
                playerFovLineImages[1].color = playerFovColor;
                PlaceLine(playerFovLines[1], mapLeft, mapRight, playerFovLineThickness);
            }
            if (playerFovLines[2] != null)
            {
                playerFovLineImages[2].color = playerFovColor;
                PlaceLine(playerFovLines[2], mapRight, mapPos, playerFovLineThickness);
            }
        }

        private void HidePlayerUnitOverlay()
        {
            if (playerMarker != null && playerMarker.gameObject.activeSelf)
                playerMarker.gameObject.SetActive(false);

            for (int i = 0; i < 3; i++)
            {
                if (playerFovLines[i] != null && playerFovLines[i].gameObject.activeSelf)
                    playerFovLines[i].gameObject.SetActive(false);
            }
        }

        private void UpdateFullInventoryDepositPulse()
        {
            if (fovOverlay == null || mapRect == null)
            {
                HideDepositPulse();
                return;
            }

            var lp = RTSPlayer.localPlayer;
            if (lp == null || lp.Role != PlayerRole.UnitPlaying)
            {
                HideDepositPulse();
                return;
            }

            var upc = UnitPlayerController.Instance;
            if (upc == null || !upc.IsActive || !(upc.ControlledUnit is Worker w) || !w.IsInventoryFull)
            {
                HideDepositPulse();
                return;
            }

            var buildings = Building.AllBuildings;
            int ringIdx = 0;
            float wave = Mathf.Repeat(Time.unscaledTime * 1.4f, 1f);
            float size = Mathf.Lerp(10f, 24f, wave);
            Color pulseColor = new Color(1f, 0.9f, 0.2f, Mathf.Lerp(0.95f, 0.15f, wave));

            for (int i = 0; i < buildings.Count && ringIdx < depositPulseRings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || !b.IsComplete || !b.IsOwnedByLocalPlayer())
                    continue;

                if (b.Type != BuildingType.Capital && b.Type != BuildingType.Warehouse)
                    continue;

                Vector3 pos = b.FinalPosition != Vector3.zero ? b.FinalPosition : b.transform.position;
                Vector2 mapPos = WorldToMapLocal(pos);
                Rect r = mapRect.rect;
                mapPos.x = Mathf.Clamp(mapPos.x, r.xMin, r.xMax);
                mapPos.y = Mathf.Clamp(mapPos.y, r.yMin, r.yMax);

                var rt = depositPulseRings[ringIdx];
                var img = depositPulseImages[ringIdx];
                if (rt != null && img != null)
                {
                    if (!rt.gameObject.activeSelf)
                        rt.gameObject.SetActive(true);
                    rt.anchoredPosition = mapPos;
                    rt.sizeDelta = new Vector2(size, size);
                    img.color = pulseColor;
                }
                ringIdx++;
            }

            for (int i = ringIdx; i < depositPulseRings.Count; i++)
            {
                if (depositPulseRings[i] != null && depositPulseRings[i].gameObject.activeSelf)
                    depositPulseRings[i].gameObject.SetActive(false);
            }
        }

        private void HideDepositPulse()
        {
            for (int i = 0; i < depositPulseRings.Count; i++)
            {
                if (depositPulseRings[i] != null && depositPulseRings[i].gameObject.activeSelf)
                    depositPulseRings[i].gameObject.SetActive(false);
            }
        }

        private bool TryDrawFovFromVirtualOrtho(Vector3 camPos, float orthoSize, float yawDeg)
        {
            float aspect = 16f / 9f;
            if (worldCamera != null)
                aspect = worldCamera.aspect;
            else if (Screen.height > 0)
                aspect = (float)Screen.width / Screen.height;

            // Типичный RTS-наклон ~50° вниз
            Quaternion rot = Quaternion.Euler(50f, yawDeg, 0f);
            float halfH = orthoSize;
            float halfW = orthoSize * aspect;

            // 4 угла far-plane на расстоянии до земли через луч
            Vector3[] locals =
            {
                new Vector3(-halfW, -halfH, 0f),
                new Vector3( halfW, -halfH, 0f),
                new Vector3( halfW,  halfH, 0f),
                new Vector3(-halfW,  halfH, 0f)
            };

            Vector2[] mapCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                // Орто: луч параллелен forward камеры из угла на near/plane
                Vector3 origin = camPos + rot * locals[i];
                Vector3 dir = rot * Vector3.forward;
                if (Mathf.Abs(dir.y) < 0.0001f)
                    return false;
                float t = (groundPlaneY - origin.y) / dir.y;
                if (t < 0f)
                {
                    // Камера под/на земле — fallback: проекция XZ
                    mapCorners[i] = WorldToMapLocal(new Vector3(origin.x, groundPlaneY, origin.z));
                }
                else
                {
                    Vector3 ground = origin + dir * t;
                    mapCorners[i] = WorldToMapLocal(ground);
                }
            }

            DrawFovLines(mapCorners);
            return true;
        }

        private void DrawFovLines(Vector2[] localCorners)
        {
            // Клип в пределы карты — иначе линии раздуваются в «квадрат»
            Rect r = mapRect.rect;
            for (int i = 0; i < 4; i++)
            {
                localCorners[i].x = Mathf.Clamp(localCorners[i].x, r.xMin, r.xMax);
                localCorners[i].y = Mathf.Clamp(localCorners[i].y, r.yMin, r.yMax);
            }

            for (int i = 0; i < 4; i++)
            {
                if (fovLines[i] == null || fovLineImages[i] == null)
                    continue;
                fovLines[i].gameObject.SetActive(true);
                fovLineImages[i].color = fovOutlineColor;
                fovLineImages[i].raycastTarget = false;
                PlaceLine(fovLines[i], localCorners[i], localCorners[(i + 1) % 4], fovLineThickness);
            }
        }

        private void HideFovLines()
        {
            for (int j = 0; j < 4; j++)
            {
                if (fovLines[j] != null)
                    fovLines[j].gameObject.SetActive(false);
            }
        }

        private Vector2 WorldToMapLocal(Vector3 world)
        {
            Rect r = mapRect.rect;
            float w = Mathf.Max(0.001f, worldMaxX - worldMinX);
            float d = Mathf.Max(0.001f, worldMaxZ - worldMinZ);
            float u = (world.x - worldMinX) / w;
            float v = (world.z - worldMinZ) / d;
            // local space of mapRect (pivot center by default after stretch → rect.min/max)
            float x = Mathf.Lerp(r.xMin, r.xMax, u);
            float y = Mathf.Lerp(r.yMin, r.yMax, v);
            return new Vector2(x, y);
        }

        private static void PlaceLine(RectTransform line, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                line.gameObject.SetActive(false);
                return;
            }

            float t = Mathf.Clamp(thickness, 1f, 4f);
            line.gameObject.SetActive(true);
            line.anchorMin = new Vector2(0.5f, 0.5f);
            line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(length, t);
            line.anchoredPosition = (a + b) * 0.5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
            line.localScale = Vector3.one;
        }

        private bool TryViewportToGround(Vector3 viewport, out Vector3 ground)
        {
            ground = Vector3.zero;
            Ray ray = worldCamera.ViewportPointToRay(viewport);
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return false;

            float t = (groundPlaneY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
                return false;

            ground = ray.origin + ray.direction * t;
            return true;
        }

        private void DrawDotWorld(int res, Vector3 world, int radius, Color32 color)
        {
            DrawDotWorldToBuffer(pixels, res, world, radius, color);
        }

        private void DrawDotWorldToBuffer(Color32[] buffer, int res, Vector3 world, int radius, Color32 color)
        {
            if (buffer == null) return;
            if (!WorldToPixel(world, res, out int px, out int py))
                return;
            DrawFilledCircleToBuffer(buffer, res, px, py, Mathf.Max(1, radius), color);
        }

        private bool WorldToPixel(Vector3 world, int res, out int px, out int py)
        {
            float w = Mathf.Max(0.001f, worldMaxX - worldMinX);
            float d = Mathf.Max(0.001f, worldMaxZ - worldMinZ);
            float u = (world.x - worldMinX) / w;
            float v = (world.z - worldMinZ) / d;
            px = Mathf.RoundToInt(u * (res - 1));
            py = Mathf.RoundToInt(v * (res - 1));
            return px >= 0 && px < res && py >= 0 && py < res;
        }

        private bool PixelToWorld(Vector2 localPoint, Vector2 rectSize, out Vector3 world)
        {
            float u = localPoint.x / Mathf.Max(0.001f, rectSize.x);
            float v = localPoint.y / Mathf.Max(0.001f, rectSize.y);
            world = new Vector3(
                Mathf.Lerp(worldMinX, worldMaxX, u),
                groundPlaneY,
                Mathf.Lerp(worldMinZ, worldMaxZ, v));
            return true;
        }

        private void DrawFilledCircle(int res, int cx, int cy, int radius, Color32 color)
        {
            DrawFilledCircleToBuffer(pixels, res, cx, cy, radius, color);
        }

        private void DrawFilledCircleToBuffer(Color32[] buffer, int res, int cx, int cy, int radius, Color32 color)
        {
            if (buffer == null) return;
            int r2 = radius * radius;
            int yMin = Mathf.Max(0, cy - radius);
            int yMax = Mathf.Min(res - 1, cy + radius);
            int xMin = Mathf.Max(0, cx - radius);
            int xMax = Mathf.Min(res - 1, cx + radius);

            for (int py = yMin; py <= yMax; py++)
            {
                int dy = py - cy;
                int row = py * res;
                for (int px = xMin; px <= xMax; px++)
                {
                    int dx = px - cx;
                    if (dx * dx + dy * dy <= r2)
                        buffer[row + px] = color;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!clickToTeleport || mapImage == null)
                return;

            RectTransform target = mapRect != null ? mapRect : mapImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect r = target.rect;
            Vector2 fromBottomLeft = local - r.min;
            if (!PixelToWorld(fromBottomLeft, r.size, out Vector3 world))
                return;

            ResolveCamera();
            if (cameraController != null)
                cameraController.FocusLookAtGroundPoint(world);
            else if (worldCamera != null)
            {
                Vector3 p = worldCamera.transform.position;
                p.x = world.x;
                p.z = world.z;
                worldCamera.transform.position = p;
            }
        }

        private static Sprite whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "MinimapWhiteSprite";
            return whiteSprite;
        }

        private static Sprite circleSprite;

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
                return circleSprite;

            int res = 32;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            Color[] cols = new Color[res * res];
            Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
            float radius = res * 0.46f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                    cols[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 1f);
            circleSprite.name = "MinimapCircleSprite";
            return circleSprite;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
                rectTransform.sizeDelta = mapSize + Vector2.one * (borderWidth * 2f);
            }
            worldDirty = true;
        }
#endif
    }
}
