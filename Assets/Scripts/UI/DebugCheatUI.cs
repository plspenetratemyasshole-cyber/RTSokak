using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RTSokak.Network;
using RTSokak.Buildings;

namespace RTSokak.UI
{
    /// <summary>
    /// Debug/Cheat UI для роли мэра (MayorPlaying).
    /// Автоматически создаётся в рантайме, управляется через F1 и кнопку сворачивания.
    /// Синхронизируется с сервером через SyncVar RTSPlayer.CheatsEnabled.
    /// </summary>
    public class DebugCheatUI : MonoBehaviour
    {
        public static DebugCheatUI Instance { get; private set; }

        private GameObject rootCanvasObj;
        private GameObject mainPanel;
        private GameObject toggleButton;
        private TextMeshProUGUI toggleButtonText;
        private TextMeshProUGUI spawnStatusText;
        private TextMeshProUGUI buildingStatusText;

        private GameObject spawnMarker;
        private string activeUnitSpawnKind;
        private bool spawnClickArmed;
        private bool isExpanded = true;
        private LayerMask groundMask = ~0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            EnsureExists();
        }

        public static DebugCheatUI EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var existing = Object.FindFirstObjectByType<DebugCheatUI>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("DebugCheatUI");
            Instance = go.AddComponent<DebugCheatUI>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateUI();
            CreateSpawnMarker();

            if (rootCanvasObj != null)
                rootCanvasObj.SetActive(false);
        }

        private void OnDestroy()
        {
            if (spawnMarker != null)
            {
                Destroy(spawnMarker);
                spawnMarker = null;
            }

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            var lp = RTSPlayer.localPlayer;
            bool canShow = lp != null && lp.Role == PlayerRole.MayorPlaying && lp.CheatsEnabled;

            if (rootCanvasObj != null && rootCanvasObj.activeSelf != canShow)
            {
                rootCanvasObj.SetActive(canShow);
                if (!canShow)
                {
                    CancelUnitSpawnMode();
                    BuildingManager.Instance?.CancelCheatPlacement();
                }
            }

            if (!canShow)
                return;

            if (Input.GetKeyDown(KeyCode.F1))
            {
                SetExpanded(!isExpanded);
            }

            UpdateStatusText();

            if (!string.IsNullOrEmpty(activeUnitSpawnKind))
            {
                UpdateUnitSpawnPlacement();
            }
        }

        public void SetExpanded(bool expanded)
        {
            isExpanded = expanded;
            if (mainPanel != null)
                mainPanel.SetActive(isExpanded);
            if (toggleButtonText != null)
                toggleButtonText.text = isExpanded ? "CHEATS [F1] ▼" : "CHEATS [F1] ▲";
        }

        private void UpdateStatusText()
        {
            if (spawnStatusText != null)
            {
                if (!string.IsNullOrEmpty(activeUnitSpawnKind))
                    spawnStatusText.text = $"<color=#55ff55>Spawn: {activeUnitSpawnKind}</color>\n[LMB] Spawn  [RMB/Esc] Exit";
                else
                    spawnStatusText.text = "";
            }

            if (buildingStatusText != null)
            {
                if (BuildingManager.Instance != null && BuildingManager.Instance.IsCheatPlacing)
                    buildingStatusText.text = $"<color=#ffaa44>Build: {BuildingManager.Instance.PendingCheatTypeName}</color>\n[LMB] Build  [RMB/Esc] Exit";
                else
                    buildingStatusText.text = "";
            }
        }

        #region Unit Spawn Mode

        public void StartUnitSpawnMode(string kind)
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.CancelCheatPlacement();
            if (SelectionPanelUI.Instance != null)
                SelectionPanelUI.Instance.ClearPreview();

            activeUnitSpawnKind = kind;
            spawnClickArmed = false;

            if (UnitSelectionManager.Instance != null)
                UnitSelectionManager.Instance.SetPlacementMode(true);

            if (spawnMarker == null)
                CreateSpawnMarker();
        }

        public void CancelUnitSpawnMode()
        {
            if (!string.IsNullOrEmpty(activeUnitSpawnKind))
            {
                activeUnitSpawnKind = null;
                if (UnitSelectionManager.Instance != null)
                    UnitSelectionManager.Instance.SetPlacementMode(false);
            }

            if (spawnMarker != null)
                spawnMarker.SetActive(false);

            spawnClickArmed = false;
        }

        private void UpdateUnitSpawnPlacement()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (UnitSelectionManager.Instance != null)
                    UnitSelectionManager.Instance.SuppressNextRightClickCommand();
                CancelUnitSpawnMode();
                return;
            }

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 5000f, groundMask, QueryTriggerInteraction.Ignore);

            if (hasHit)
            {
                if (spawnMarker != null)
                {
                    spawnMarker.SetActive(true);
                    spawnMarker.transform.position = hit.point + new Vector3(0f, 0.05f, 0f);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    spawnClickArmed = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                }

                if (Input.GetMouseButtonUp(0) && spawnClickArmed)
                {
                    spawnClickArmed = false;

                    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    {
                        var lp = RTSPlayer.localPlayer;
                        if (lp != null && !string.IsNullOrEmpty(activeUnitSpawnKind))
                        {
                            lp.CmdCheatSpawnUnit(activeUnitSpawnKind, hit.point);
                        }
                    }
                }
            }
            else
            {
                if (spawnMarker != null)
                    spawnMarker.SetActive(false);
            }
        }

        private void CreateSpawnMarker()
        {
            if (spawnMarker != null)
                return;

            spawnMarker = new GameObject("CheatSpawnMarker");
            spawnMarker.transform.SetParent(transform);

            LineRenderer lr = spawnMarker.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;

            int segments = 36;
            lr.positionCount = segments;
            float radius = 0.8f;
            Vector3[] pts = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                pts[i] = new Vector3(Mathf.Cos(angle) * radius, 0.03f, Mathf.Sin(angle) * radius);
            }
            lr.SetPositions(pts);

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material mat = shader != null ? new Material(shader) : null;
            if (mat != null)
            {
                mat.color = new Color(0.2f, 0.9f, 1f, 0.85f);
                lr.material = mat;
            }
            lr.startColor = new Color(0.2f, 0.9f, 1f, 0.85f);
            lr.endColor = new Color(0.2f, 0.9f, 1f, 0.85f);

            spawnMarker.SetActive(false);
        }

        #endregion

        #region UI Construction

        private void StartCheatBuilding(BuildingType type)
        {
            CancelUnitSpawnMode();
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.StartCheatPlacement(type);
            else
                UnityEngine.Debug.LogWarning("[DebugCheatUI] BuildingManager.Instance is null!");
        }

        private void CreateUI()
        {
            if (rootCanvasObj != null)
                return;

            // Ensure EventSystem
            if (EventSystem.current == null && Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            // Root Canvas
            rootCanvasObj = new GameObject("DebugCheatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootCanvasObj.transform.SetParent(transform);

            Canvas canvas = rootCanvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = rootCanvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Toggle Button (Top Right)
            toggleButton = CreateButton(
                rootCanvasObj.transform,
                "CheatToggleButton",
                "CHEATS [F1] ▼",
                new Vector2(-15f, -15f),
                new Vector2(130f, 32f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Color(0.14f, 0.20f, 0.26f, 0.95f),
                () => SetExpanded(!isExpanded));
            toggleButtonText = toggleButton.GetComponentInChildren<TextMeshProUGUI>();

            // Main Panel (Below Toggle Button)
            float panelWidth = 260f;
            float panelHeight = 470f;
            mainPanel = new GameObject("CheatMainPanel", typeof(RectTransform), typeof(Image));
            mainPanel.transform.SetParent(rootCanvasObj.transform, false);

            RectTransform panelRt = mainPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 1f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 1f);
            panelRt.anchoredPosition = new Vector2(-15f, -52f);
            panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);

            Image panelBg = mainPanel.GetComponent<Image>();
            panelBg.color = new Color(0.09f, 0.11f, 0.15f, 0.94f);

            // Title
            CreateLabel(mainPanel.transform, "Title", "MAYOR CHEAT PANEL", new Vector2(10f, -8f), new Vector2(panelWidth - 20f, 22f), 13, FontStyles.Bold, new Color(1f, 0.82f, 0.25f, 1f), TextAlignmentOptions.Center);

            // --- Section: Resources ---
            float curY = -34f;
            CreateLabel(mainPanel.transform, "ResHeader", "— RESOURCES —", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 18f), 11, FontStyles.Bold, new Color(0.75f, 0.85f, 1f, 1f), TextAlignmentOptions.Center);

            curY -= 22f;
            CreateButton(
                mainPanel.transform,
                "BtnAllRes",
                "+1000 All",
                new Vector2(10f, curY),
                new Vector2(panelWidth - 20f, 26f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Color(0.20f, 0.52f, 0.32f, 1f),
                () => RTSPlayer.localPlayer?.CmdCheatAddResources(1000, 1000, 1000));

            curY -= 30f;
            float resBtnW = (panelWidth - 20f - 8f) / 3f; // 3 buttons
            CreateButton(mainPanel.transform, "BtnWood", "+500 Wood", new Vector2(10f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.50f, 0.35f, 0.20f, 1f), () => RTSPlayer.localPlayer?.CmdCheatAddResources(500, 0, 0));
            CreateButton(mainPanel.transform, "BtnOre", "+500 Ore", new Vector2(10f + resBtnW + 4f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.35f, 0.45f, 0.55f, 1f), () => RTSPlayer.localPlayer?.CmdCheatAddResources(0, 500, 0));
            CreateButton(mainPanel.transform, "BtnFood", "+500 Food", new Vector2(10f + (resBtnW + 4f) * 2f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.30f, 0.50f, 0.25f, 1f), () => RTSPlayer.localPlayer?.CmdCheatAddResources(0, 0, 500));

            // --- Section: Spawn Units ---
            curY -= 34f;
            CreateLabel(mainPanel.transform, "UnitsHeader", "— SPAWN UNITS —", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 18f), 11, FontStyles.Bold, new Color(0.75f, 0.85f, 1f, 1f), TextAlignmentOptions.Center);

            curY -= 22f;
            CreateButton(mainPanel.transform, "BtnWorker", "Worker", new Vector2(10f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.22f, 0.42f, 0.60f, 1f), () => StartUnitSpawnMode("Worker"));
            CreateButton(mainPanel.transform, "BtnWarrior", "Warrior", new Vector2(10f + resBtnW + 4f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.22f, 0.42f, 0.60f, 1f), () => StartUnitSpawnMode("Warrior"));
            CreateButton(mainPanel.transform, "BtnArcher", "Archer", new Vector2(10f + (resBtnW + 4f) * 2f, curY), new Vector2(resBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.22f, 0.42f, 0.60f, 1f), () => StartUnitSpawnMode("Archer"));

            curY -= 30f;
            var spawnStatusObj = CreateLabel(mainPanel.transform, "SpawnStatus", "", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 26f), 10, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            spawnStatusText = spawnStatusObj.GetComponent<TextMeshProUGUI>();

            // --- Section: Instant Buildings ---
            curY -= 24f;
            CreateLabel(mainPanel.transform, "BuildHeader", "— INSTANT BUILDINGS —", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 18f), 11, FontStyles.Bold, new Color(0.75f, 0.85f, 1f, 1f), TextAlignmentOptions.Center);

            curY -= 22f;
            float bBtnW = (panelWidth - 20f - 4f) / 2f;
            CreateButton(mainPanel.transform, "BtnCapital", "Capital", new Vector2(10f, curY), new Vector2(bBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.48f, 0.32f, 0.58f, 1f), () => StartCheatBuilding(BuildingType.Capital));
            CreateButton(mainPanel.transform, "BtnHouse", "House", new Vector2(10f + bBtnW + 4f, curY), new Vector2(bBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.48f, 0.32f, 0.58f, 1f), () => StartCheatBuilding(BuildingType.House));

            curY -= 30f;
            CreateButton(mainPanel.transform, "BtnWarehouse", "Warehouse", new Vector2(10f, curY), new Vector2(bBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.48f, 0.32f, 0.58f, 1f), () => StartCheatBuilding(BuildingType.Warehouse));
            CreateButton(mainPanel.transform, "BtnBarracks", "Barracks", new Vector2(10f + bBtnW + 4f, curY), new Vector2(bBtnW, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.48f, 0.32f, 0.58f, 1f), () => StartCheatBuilding(BuildingType.Barracks));

            curY -= 30f;
            CreateButton(mainPanel.transform, "BtnGarden", "Garden Bed", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.48f, 0.32f, 0.58f, 1f), () => StartCheatBuilding(BuildingType.GardenBed));

            curY -= 30f;
            var buildStatusObj = CreateLabel(mainPanel.transform, "BuildStatus", "", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 26f), 10, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            buildingStatusText = buildStatusObj.GetComponent<TextMeshProUGUI>();

            // Footer tip
            curY -= 26f;
            CreateLabel(mainPanel.transform, "Tip", "<color=#888888>[LMB] Action  |  [RMB / Esc] Cancel\n[F1] Hide/Show Panel</color>", new Vector2(10f, curY), new Vector2(panelWidth - 20f, 28f), 9, FontStyles.Normal, Color.gray, TextAlignmentOptions.Center);
        }

        private static GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 pos,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Color bgColor,
            System.Action onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f);
            cb.selectedColor = bgColor;
            btn.colors = cb;

            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = label;
            tmp.fontSize = 11;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btnObj;
        }

        private static GameObject CreateLabel(
            Transform parent,
            string name,
            string text,
            Vector2 pos,
            Vector2 size,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;

            return obj;
        }

        #endregion
    }
}
