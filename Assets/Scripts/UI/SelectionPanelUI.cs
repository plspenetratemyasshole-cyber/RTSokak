using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RTSokak.Buildings;
using RTSokak.Network;

namespace RTSokak.UI
{
    /// <summary>
    /// Нижняя панель: кнопки строительства, инвентарь юнита, прогресс/снос здания.
    /// Сам GameObject всегда активен (иначе Start/подписки не работают).
    /// </summary>
    public class SelectionPanelUI : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Визуальный корень (Image). Если пусто — этот объект. Не выключает GameObject со скриптом.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Worker")]
        [SerializeField] private GameObject workerSection;
        [SerializeField] private Button capitalButton;
        [SerializeField] private Button houseButton;
        [SerializeField] private Button gardenButton;
        [SerializeField] private Button warehouseButton;
        [SerializeField] private Button barracksButton;
        [SerializeField] private Button depositButton;
        [SerializeField] private TextMeshProUGUI inventoryText;

        [Header("Building")]
        [SerializeField] private GameObject buildingSection;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject progressGroup;
        [SerializeField] private Button trainWorkerButton;
        [SerializeField] private TextMeshProUGUI trainWorkerLabel;
        [SerializeField] private Button trainWarriorButton;
        [SerializeField] private TextMeshProUGUI trainWarriorLabel;
        [SerializeField] private Button trainArcherButton;
        [SerializeField] private TextMeshProUGUI trainArcherLabel;

        [Header("Capital Queue (bottom-left)")]
        [SerializeField] private RectTransform trainQueueRoot;
        [SerializeField] private float queueSlotSize = 56f;
        [SerializeField] private float queueSlotGap = 8f;

        [Header("Delete Confirm")]
        [SerializeField] private GameObject deleteConfirmPanel;
        [SerializeField] private TextMeshProUGUI deleteConfirmText;
        [SerializeField] private Button deleteConfirmYesButton;
        [SerializeField] private Button deleteConfirmNoButton;

        [Header("Tooltip")]
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TextMeshProUGUI tooltipText;

        [Header("Placement")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Convert Unit ↔ Player")]
        [SerializeField] private GameObject convertSection;
        [SerializeField] private Button convertToPlayerButton;
        [SerializeField] private Button convertToAiButton;
        [SerializeField] private RectTransform convertPlayerListRoot;

        private UnitSelectionManager selectionManager;
        private BuildingType? pendingBuildType;
        private BuildingDefinition pendingBuildDef;
        private GameObject previewInstance;
        private float previewHeight;
        private bool previewPlacementValid;
        private bool previewGhostReady;
        private UnityEngine.Camera mainCamera;
        private CanvasGroup canvasGroup;
        private Graphic rootGraphic;
        private IUnitTrainingQueue subscribedProduction;
        private readonly System.Collections.Generic.List<GameObject> queueSlots = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<TextMeshProUGUI> queuePercents = new System.Collections.Generic.List<TextMeshProUGUI>();
        private readonly System.Collections.Generic.List<Image> queueFills = new System.Collections.Generic.List<Image>();
        private IUnitTrainingQueue queueShownFor;
        private static Sprite queueWhiteSprite;

        private void Awake()
        {
            EnsureSelectionManager();
            EnsureCanvasGroup();
            SetPanelVisible(false);
        }

        private void Start()
        {
            mainCamera = UnityEngine.Camera.main;
            EnsureSelectionManager();

            if (selectionManager != null)
            {
                selectionManager.OnSelectionChanged -= Refresh;
                selectionManager.OnSelectionChanged += Refresh;
            }

            if (capitalButton != null)
            {
                capitalButton.onClick.RemoveAllListeners();
                capitalButton.onClick.AddListener(() => BeginPlacement(BuildingType.Capital));
                AddHover(capitalButton.gameObject, BuildingType.Capital);
            }

            if (houseButton != null)
            {
                houseButton.onClick.RemoveAllListeners();
                houseButton.onClick.AddListener(() => BeginPlacement(BuildingType.House));
                AddHover(houseButton.gameObject, BuildingType.House);
            }

            EnsureGardenButton();
            if (gardenButton != null)
            {
                gardenButton.onClick.RemoveAllListeners();
                gardenButton.onClick.AddListener(() => BeginPlacement(BuildingType.GardenBed));
                AddHover(gardenButton.gameObject, BuildingType.GardenBed);
            }

            EnsureWarehouseButton();
            if (warehouseButton != null)
            {
                warehouseButton.onClick.RemoveAllListeners();
                warehouseButton.onClick.AddListener(() => BeginPlacement(BuildingType.Warehouse));
                AddHover(warehouseButton.gameObject, BuildingType.Warehouse);
            }

            EnsureBarracksButton();
            if (barracksButton != null)
            {
                barracksButton.onClick.RemoveAllListeners();
                barracksButton.onClick.AddListener(() => BeginPlacement(BuildingType.Barracks));
                AddHover(barracksButton.gameObject, BuildingType.Barracks);
            }

            EnsureDepositButton();
            if (depositButton != null)
            {
                depositButton.onClick.RemoveAllListeners();
                depositButton.onClick.AddListener(OnDepositClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(OnDeleteClicked);
            }

            EnsureTrainWorkerButton();
            if (trainWorkerButton != null)
            {
                trainWorkerButton.onClick.RemoveAllListeners();
                trainWorkerButton.onClick.AddListener(OnTrainWorkerClicked);
            }

            EnsureTrainWarriorButton();
            if (trainWarriorButton != null)
            {
                trainWarriorButton.onClick.RemoveAllListeners();
                trainWarriorButton.onClick.AddListener(OnTrainWarriorClicked);
            }

            EnsureTrainArcherButton();
            if (trainArcherButton != null)
            {
                trainArcherButton.onClick.RemoveAllListeners();
                trainArcherButton.onClick.AddListener(OnTrainArcherClicked);
            }

            EnsureTrainQueueRoot();
            EnsureDeleteConfirmPanel();
            WireDeleteConfirmButtons();
            HideDeleteConfirm();
            EnsureConvertUi();

            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
                DisableTooltipRaycasts(tooltipRoot.gameObject);
            }

            Refresh();
        }

        private static void DisableTooltipRaycasts(GameObject root)
        {
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }

        private void OnEnable()
        {
            EnsureSelectionManager();
            if (selectionManager != null)
            {
                selectionManager.OnSelectionChanged -= Refresh;
                selectionManager.OnSelectionChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            if (selectionManager != null)
                selectionManager.OnSelectionChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (selectionManager != null)
                selectionManager.OnSelectionChanged -= Refresh;
            UnsubscribeProduction();
            ClearPreview();
        }

        private void EnsureSelectionManager()
        {
            if (selectionManager != null)
                return;

            selectionManager = FindFirstObjectByType<UnitSelectionManager>();
            if (selectionManager == null)
            {
                var go = new GameObject("UnitSelectionManager");
                selectionManager = go.AddComponent<UnitSelectionManager>();
                UnityEngine.Debug.Log("[SelectionPanelUI] Created missing UnitSelectionManager");
            }
        }

        private void EnsureCanvasGroup()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            rootGraphic = GetComponent<Graphic>();
        }

        private void SetPanelVisible(bool visible)
        {
            // Никогда не выключаем gameObject со скриптом — только прячем визуал
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (rootGraphic != null)
                rootGraphic.enabled = visible;

            // Секции — дочерние объекты, их можно toggle
            if (!visible)
            {
                if (workerSection != null) workerSection.SetActive(false);
                if (buildingSection != null) buildingSection.SetActive(false);
                if (convertSection != null) convertSection.SetActive(false);
                SetTrainQueueVisible(false);
            }
        }

        private void Update()
        {
            if (selectionManager == null)
                EnsureSelectionManager();

            if (pendingBuildType.HasValue)
                UpdatePlacement();

            if (selectionManager != null && selectionManager.SelectedBuilding != null)
            {
                RefreshBuildingProgress(selectionManager.SelectedBuilding);
                RefreshTrainQueuePercents();
            }

            if (selectionManager != null && selectionManager.SelectedWorker != null)
                RefreshInventory(selectionManager.SelectedWorker);
        }

        private void Refresh()
        {
            Worker worker = selectionManager != null ? selectionManager.SelectedWorker : null;
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            RTSUnit anyUnit = selectionManager != null ? selectionManager.SelectedUnit : null;

            // Панель: worker/building экономика, либо convert для любого своего юнита мэра
            bool convertRelevant = RTSPlayer.localPlayer != null &&
                                   RTSPlayer.localPlayer.Role == PlayerRole.MayorPlaying &&
                                   anyUnit != null && anyUnit.IsOwned;
            bool show = worker != null || building != null || convertRelevant;
            SetPanelVisible(show);

            if (workerSection != null)
                workerSection.SetActive(show && worker != null && building == null);

            if (buildingSection != null)
                buildingSection.SetActive(show && building != null);

            if (worker != null)
                RefreshInventory(worker);

            if (building != null)
            {
                RefreshBuildingProgress(building);
                RefreshTrainUI(building);
            }
            else
            {
                UnsubscribeProduction();
                SetTrainQueueVisible(false);
                if (trainWorkerButton != null)
                    trainWorkerButton.gameObject.SetActive(false);
                if (trainWarriorButton != null)
                    trainWarriorButton.gameObject.SetActive(false);
                if (trainArcherButton != null)
                    trainArcherButton.gameObject.SetActive(false);
                HideDeleteConfirm();
            }

            if (worker == null)
                ClearPreview();

            RefreshConvertUi();
        }

        private void EnsureConvertUi()
        {
            if (convertSection != null)
                return;

            // Runtime-создаём блок конвертации справа от панели
            Transform parent = panelRoot != null ? panelRoot.transform : transform;
            convertSection = new GameObject("ConvertSection", typeof(RectTransform));
            convertSection.transform.SetParent(parent, false);
            var rt = convertSection.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 90f);
            rt.sizeDelta = new Vector2(220f, 200f);

            convertToPlayerButton = CreateRuntimeButton(convertSection.transform, "ConvertToPlayer", "Assign to player",
                new Vector2(0f, 160f), new Vector2(200f, 36f));
            convertToAiButton = CreateRuntimeButton(convertSection.transform, "ConvertToAi", "Return to AI",
                new Vector2(0f, 160f), new Vector2(200f, 36f));

            GameObject listGo = new GameObject("PlayerList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listGo.transform.SetParent(convertSection.transform, false);
            convertPlayerListRoot = listGo.GetComponent<RectTransform>();
            convertPlayerListRoot.anchorMin = new Vector2(0f, 0f);
            convertPlayerListRoot.anchorMax = new Vector2(1f, 1f);
            convertPlayerListRoot.offsetMin = new Vector2(0f, 0f);
            convertPlayerListRoot.offsetMax = new Vector2(0f, -44f);
            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 4f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            listGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            convertToPlayerButton.onClick.AddListener(OnConvertToPlayerClicked);
            convertToAiButton.onClick.AddListener(OnConvertToAiClicked);
            convertSection.SetActive(false);
        }

        private static Button CreateRuntimeButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.26f, 0.95f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private void RefreshConvertUi()
        {
            EnsureConvertUi();
            if (convertSection == null)
                return;

            RTSPlayer local = RTSPlayer.localPlayer;
            RTSUnit unit = selectionManager != null ? selectionManager.SelectedUnit : null;
            bool mayor = local != null && local.Role == PlayerRole.MayorPlaying;
            bool ownUnit = unit != null && unit.IsAlive && unit.IsOwned;

            if (!mayor || !ownUnit)
            {
                convertSection.SetActive(false);
                return;
            }

            convertSection.SetActive(true);

            if (unit.IsPlayerControlled)
            {
                if (convertToPlayerButton != null) convertToPlayerButton.gameObject.SetActive(false);
                if (convertPlayerListRoot != null) convertPlayerListRoot.gameObject.SetActive(false);
                if (convertToAiButton != null) convertToAiButton.gameObject.SetActive(true);
            }
            else
            {
                if (convertToAiButton != null) convertToAiButton.gameObject.SetActive(false);
                var waiting = PlayerRoleUI.GetWaitingUnitPlayers();
                bool any = waiting != null && waiting.Count > 0;
                if (convertToPlayerButton != null)
                {
                    convertToPlayerButton.gameObject.SetActive(any);
                    convertToPlayerButton.interactable = any;
                }
                if (convertPlayerListRoot != null)
                    convertPlayerListRoot.gameObject.SetActive(false);
            }
        }

        private void OnConvertToPlayerClicked()
        {
            var waiting = PlayerRoleUI.GetWaitingUnitPlayers();
            if (waiting == null || waiting.Count == 0 || convertPlayerListRoot == null)
                return;

            // Показать список ников
            for (int i = convertPlayerListRoot.childCount - 1; i >= 0; i--)
                Destroy(convertPlayerListRoot.GetChild(i).gameObject);

            convertPlayerListRoot.gameObject.SetActive(true);
            RTSUnit unit = selectionManager != null ? selectionManager.SelectedUnit : null;
            if (unit == null) return;

            foreach (var p in waiting)
            {
                if (p == null) continue;
                string nick = string.IsNullOrEmpty(p.Nickname) ? $"Player#{p.netId}" : p.Nickname;
                Button btn = CreateRuntimeButton(convertPlayerListRoot, "Wait_" + p.netId, nick,
                    Vector2.zero, new Vector2(200f, 32f));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 32f;
                uint playerNetId = p.netId;
                uint unitNetId = unit.netId;
                btn.onClick.AddListener(() =>
                {
                    RTSPlayer.localPlayer?.CmdConvertUnitToPlayer(unitNetId, playerNetId);
                    convertPlayerListRoot.gameObject.SetActive(false);
                });
            }
        }

        private void OnConvertToAiClicked()
        {
            RTSUnit unit = selectionManager != null ? selectionManager.SelectedUnit : null;
            if (unit == null || RTSPlayer.localPlayer == null)
                return;
            RTSPlayer.localPlayer.CmdConvertUnitToAi(unit.netId);
        }

        private void RefreshInventory(Worker worker)
        {
            if (inventoryText == null || worker == null) return;
            inventoryText.text =
                $"Inventory: Wood {worker.InventoryWood} | Ore {worker.InventoryOre} | Food {worker.InventoryFood}  (max {worker.MaxInventory})";
        }

        private void RefreshBuildingProgress(Building building)
        {
            if (building == null) return;

            bool constructing = building.IsUnderConstruction;
            if (progressGroup != null)
                progressGroup.SetActive(constructing);

            float p = Mathf.Clamp01(building.BuildProgress);
            if (progressFill != null)
                progressFill.fillAmount = p;
            if (progressText != null)
                progressText.text = constructing ? $"{Mathf.RoundToInt(p * 100f)}%" : "Built";
        }

        private void RefreshTrainUI(Building building)
        {
            EnsureTrainWorkerButton();
            EnsureTrainWarriorButton();
            EnsureTrainArcherButton();
            EnsureTrainQueueRoot();

            var capitalProd = building != null ? building.GetComponent<CapitalProduction>() : null;
            var barracksProd = building != null ? building.GetComponent<BarracksProduction>() : null;

            bool showWorker = building != null
                              && building.Type == BuildingType.Capital
                              && building.IsComplete
                              && building.IsOwnedByLocalPlayer()
                              && capitalProd != null;

            bool showBarracks = building != null
                               && building.Type == BuildingType.Barracks
                               && building.IsComplete
                               && building.IsOwnedByLocalPlayer()
                               && barracksProd != null;

            if (trainWorkerButton != null)
            {
                trainWorkerButton.gameObject.SetActive(showWorker);
                if (showWorker && trainWorkerLabel != null)
                    trainWorkerLabel.text = $"Worker\n{capitalProd.WorkerFoodCost} Food";
            }

            if (trainWarriorButton != null)
            {
                trainWarriorButton.gameObject.SetActive(showBarracks);
                if (showBarracks && trainWarriorLabel != null)
                    trainWarriorLabel.text = $"Warrior\n{barracksProd.WarriorFoodCost} Food";
            }

            if (trainArcherButton != null)
            {
                trainArcherButton.gameObject.SetActive(showBarracks);
                if (showBarracks && trainArcherLabel != null)
                    trainArcherLabel.text = $"Archer\n{barracksProd.ArcherFoodCost} Food";
            }

            IUnitTrainingQueue activeQueue = showWorker ? (IUnitTrainingQueue)capitalProd
                : showBarracks ? barracksProd : null;

            if (activeQueue != null)
            {
                SubscribeProduction(activeQueue);
                RebuildTrainQueue(activeQueue);
                SetTrainQueueVisible(true);
            }
            else
            {
                UnsubscribeProduction();
                SetTrainQueueVisible(false);
            }
        }

        private void SubscribeProduction(IUnitTrainingQueue prod)
        {
            if (subscribedProduction == prod)
                return;

            UnsubscribeProduction();
            subscribedProduction = prod;
            if (subscribedProduction != null)
                subscribedProduction.OnQueueChanged += OnProductionQueueChanged;
        }

        private void UnsubscribeProduction()
        {
            if (subscribedProduction != null)
            {
                subscribedProduction.OnQueueChanged -= OnProductionQueueChanged;
                subscribedProduction = null;
            }

            queueShownFor = null;
        }

        private void OnProductionQueueChanged()
        {
            if (subscribedProduction != null)
                RebuildTrainQueue(subscribedProduction);
        }

        private void OnTrainWorkerClicked()
        {
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            var prod = building != null ? building.GetComponent<CapitalProduction>() : null;
            if (prod == null)
                return;
            prod.RequestEnqueueWorker();
        }

        private void OnTrainWarriorClicked()
        {
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            var prod = building != null ? building.GetComponent<BarracksProduction>() : null;
            if (prod == null)
                return;
            prod.RequestEnqueueWarrior();
        }

        private void OnTrainArcherClicked()
        {
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            var prod = building != null ? building.GetComponent<BarracksProduction>() : null;
            if (prod == null)
                return;
            prod.RequestEnqueueArcher();
        }

        private void EnsureTrainWorkerButton()
        {
            if (trainWorkerButton != null || buildingSection == null)
                return;

            GameObject btnObj = new GameObject("TrainWorkerButton");
            btnObj.transform.SetParent(buildingSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-80f, -10f);
            rt.sizeDelta = new Vector2(120f, 48f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.3f, 1f);

            trainWorkerButton = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            trainWorkerLabel = textObj.AddComponent<TextMeshProUGUI>();
            trainWorkerLabel.text = "Worker\n150 Food";
            trainWorkerLabel.fontSize = 14;
            trainWorkerLabel.color = Color.white;
            trainWorkerLabel.alignment = TextAlignmentOptions.Center;

            btnObj.SetActive(false);
        }

        private void EnsureTrainWarriorButton()
        {
            if (trainWarriorButton != null || buildingSection == null)
                return;

            GameObject btnObj = new GameObject("TrainWarriorButton");
            btnObj.transform.SetParent(buildingSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-80f, -10f);
            rt.sizeDelta = new Vector2(120f, 48f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.45f, 0.28f, 0.25f, 1f);

            trainWarriorButton = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            trainWarriorLabel = textObj.AddComponent<TextMeshProUGUI>();
            trainWarriorLabel.text = "Warrior\n200 Food";
            trainWarriorLabel.fontSize = 14;
            trainWarriorLabel.color = Color.white;
            trainWarriorLabel.alignment = TextAlignmentOptions.Center;

            btnObj.SetActive(false);
        }

        private void EnsureTrainArcherButton()
        {
            if (trainArcherButton != null || buildingSection == null)
                return;

            GameObject btnObj = new GameObject("TrainArcherButton");
            btnObj.transform.SetParent(buildingSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(50f, -10f); // Справа от Warrior
            rt.sizeDelta = new Vector2(120f, 48f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.35f, 0.42f, 0.5f, 1f); // Синеватый цвет для лучника

            trainArcherButton = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            trainArcherLabel = textObj.AddComponent<TextMeshProUGUI>();
            trainArcherLabel.text = "Archer\n250 Food";
            trainArcherLabel.fontSize = 14;
            trainArcherLabel.color = Color.white;
            trainArcherLabel.alignment = TextAlignmentOptions.Center;

            btnObj.SetActive(false);
        }

        private void EnsureTrainQueueRoot()
        {
            if (trainQueueRoot != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            GameObject root = new GameObject("CapitalTrainQueue");
            root.transform.SetParent(canvas.transform, false);
            trainQueueRoot = root.AddComponent<RectTransform>();
            trainQueueRoot.anchorMin = new Vector2(0f, 0f);
            trainQueueRoot.anchorMax = new Vector2(0f, 0f);
            trainQueueRoot.pivot = new Vector2(0f, 0f);
            trainQueueRoot.anchoredPosition = new Vector2(16f, 16f);
            trainQueueRoot.sizeDelta = new Vector2(400f, 70f);
            root.SetActive(false);
        }

        private void SetTrainQueueVisible(bool visible)
        {
            if (trainQueueRoot != null)
                trainQueueRoot.gameObject.SetActive(visible);

            if (!visible)
            {
                ClearQueueSlots();
                queueShownFor = null;
            }
        }

        private void RebuildTrainQueue(IUnitTrainingQueue prod)
        {
            EnsureTrainQueueRoot();
            if (trainQueueRoot == null || prod == null)
                return;

            int count = prod.QueueCount;
            // Пересоздаём только если изменилось число слотов
            if (queueShownFor == prod && queueSlots.Count == count)
            {
                RefreshTrainQueuePercents();
                return;
            }

            ClearQueueSlots();
            queueShownFor = prod;

            for (int i = 0; i < count; i++)
            {
                int slotIndex = i;
                GameObject slot = new GameObject($"QueueSlot_{i}");
                slot.transform.SetParent(trainQueueRoot, false);
                RectTransform rt = slot.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(queueSlotSize, queueSlotSize);
                rt.anchoredPosition = new Vector2(i * (queueSlotSize + queueSlotGap), 0f);

                // Фон слота (тёмный)
                Image bg = slot.AddComponent<Image>();
                bg.sprite = GetQueueWhiteSprite();
                bg.color = new Color(0.12f, 0.14f, 0.16f, 0.95f);

                // Вертикальный progress fill снизу вверх
                GameObject fillObj = new GameObject("Fill");
                fillObj.transform.SetParent(slot.transform, false);
                RectTransform fillRt = fillObj.AddComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                Image fill = fillObj.AddComponent<Image>();
                fill.sprite = GetQueueWhiteSprite();
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = Mathf.Clamp01(prod.GetSlotProgress(i));
                fill.color = new Color(0.2f, 0.75f, 0.35f, 0.95f);
                fill.raycastTarget = false;

                Button btn = slot.AddComponent<Button>();
                int captured = slotIndex;
                btn.onClick.AddListener(() =>
                {
                    if (subscribedProduction != null)
                        subscribedProduction.RequestCancelSlot(captured);
                });

                GameObject textObj = new GameObject("Percent");
                textObj.transform.SetParent(slot.transform, false);
                RectTransform textRt = textObj.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{prod.GetSlotPercent(i)}%";
                tmp.fontSize = 16;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;

                queueSlots.Add(slot);
                queuePercents.Add(tmp);
                queueFills.Add(fill);
            }
        }

        private void RefreshTrainQueuePercents()
        {
            if (subscribedProduction == null)
                return;

            if (queueSlots.Count != subscribedProduction.QueueCount)
            {
                RebuildTrainQueue(subscribedProduction);
                return;
            }

            for (int i = 0; i < queuePercents.Count; i++)
            {
                float progress = Mathf.Clamp01(subscribedProduction.GetSlotProgress(i));
                if (queuePercents[i] != null)
                    queuePercents[i].text = $"{subscribedProduction.GetSlotPercent(i)}%";
                if (i < queueFills.Count && queueFills[i] != null)
                    queueFills[i].fillAmount = progress;
            }
        }

        private void ClearQueueSlots()
        {
            for (int i = 0; i < queueSlots.Count; i++)
            {
                if (queueSlots[i] != null)
                    Destroy(queueSlots[i]);
            }

            queueSlots.Clear();
            queuePercents.Clear();
            queueFills.Clear();
        }

        private static Sprite GetQueueWhiteSprite()
        {
            if (queueWhiteSprite != null)
                return queueWhiteSprite;

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            queueWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            queueWhiteSprite.name = "QueueSlotWhiteSprite";
            return queueWhiteSprite;
        }

        private void BeginPlacement(BuildingType type)
        {
            Worker worker = selectionManager != null ? selectionManager.SelectedWorker : null;
            if (worker == null)
            {
                UnityEngine.Debug.LogWarning($"[SelectionPanelUI] BeginPlacement({type}) failed: no worker selected. SELECT A WORKER FIRST!");
                
                // Показать подсказку игроку
                if (FloatingTextManager.Instance != null)
                {
                    Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
                    FloatingTextManager.Instance.Show("Select a Worker first!", screenCenter, Color.yellow, 2f);
                }
                return;
            }

            var def = BuildingManager.Instance != null
                ? BuildingManager.Instance.GetDefinition(type)
                : null;
            if (def == null || def.prefab == null)
            {
                UnityEngine.Debug.LogWarning($"No definition/prefab for {type}");
                return;
            }

            RTSPlayer player = RTSPlayer.localPlayer;
            if (player != null)
            {
                if (type == BuildingType.Capital && player.HasCapitalBuilding())
                {
                    UnityEngine.Debug.LogWarning("Capital already exists");
                    return;
                }

                if (type != BuildingType.Capital && !player.HasCapital)
                {
                    UnityEngine.Debug.LogWarning("Build Capital first");
                    return;
                }

                if (player.Wood < def.woodCost || player.Ore < def.oreCost || player.Food < def.foodCost)
                {
                    UnityEngine.Debug.LogWarning("Not enough resources");
                    return;
                }
            }

            ClearPreview();
            pendingBuildType = type;
            pendingBuildDef = def;
            previewHeight = def.previewHeight;
            previewPlacementValid = true;
            previewGhostReady = false;
            previewInstance = Instantiate(def.prefab);
            DisablePreviewNetworking(previewInstance);
            SetPreviewGhost(previewInstance, true);
            // Повторно снять коллайдеры — Building/Awake могли добавить их после Instantiate
            StripPreviewPhysics(previewInstance);
            previewGhostReady = true;
            if (selectionManager != null)
                selectionManager.SetPlacementMode(true);
        }

        private bool placementClickArmed;

        private void UpdatePlacement()
        {
            if (Input.GetMouseButtonDown(1))
            {
                // Отмена постройки: не отдавать этот ПКМ как команду движения юниту
                if (selectionManager != null)
                    selectionManager.SuppressNextRightClickCommand();
                ClearPreview();
                return;
            }

            if (mainCamera == null)
                mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null || previewInstance == null)
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 5000f, groundMask, QueryTriggerInteraction.Ignore);
            if (hasHit && pendingBuildDef != null && BuildingManager.Instance != null)
            {
                Vector2Int origin = BuildingManager.Instance.WorldCenterToOrigin(hit.point, pendingBuildDef.Size);
                bool valid = BuildingManager.Instance.CanPlaceAt(origin, pendingBuildDef.Size, out _);
                Vector3 follow = BuildingManager.Instance.OriginToWorldCenter(origin, pendingBuildDef.Size, previewHeight);
                previewInstance.transform.position = follow;
                if (!previewGhostReady || valid != previewPlacementValid)
                {
                    previewPlacementValid = valid;
                    SetPreviewGhost(previewInstance, previewPlacementValid);
                    StripPreviewPhysics(previewInstance);
                    previewGhostReady = true;
                }
            }
            else if (hasHit)
            {
                Vector3 follow = hit.point;
                follow.y = previewHeight;
                previewInstance.transform.position = follow;
            }

            // Arm on press (не над UI), confirm on release — стабильнее для клиента
            if (Input.GetMouseButtonDown(0))
            {
                placementClickArmed =
                    EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            }

            if (!Input.GetMouseButtonUp(0) || !placementClickArmed)
                return;

            placementClickArmed = false;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!hasHit)
            {
                // Повторный raycast на момент отпускания
                if (!Physics.Raycast(ray, out hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    UnityEngine.Debug.LogWarning("[SelectionPanel] Place failed: no ground hit");
                    return;
                }
            }

            if (pendingBuildDef != null && BuildingManager.Instance != null)
            {
                Vector2Int origin = BuildingManager.Instance.WorldCenterToOrigin(hit.point, pendingBuildDef.Size);
                if (!BuildingManager.Instance.CanPlaceAt(origin, pendingBuildDef.Size, out string reason))
                {
                    UnityEngine.Debug.LogWarning($"[SelectionPanel] Place failed: {reason}");
                    return;
                }
            }

            Vector3 pos = hit.point;
            pos.y = previewHeight;

            Worker worker = selectionManager != null ? selectionManager.SelectedWorker : null;
            if (worker == null && selectionManager != null)
            {
                var list = selectionManager.GetSelectedWorkers();
                if (list.Count > 0)
                    worker = list[0];
            }

            if (worker == null || !pendingBuildType.HasValue)
            {
                UnityEngine.Debug.LogWarning("[SelectionPanel] Place failed: no worker or build type");
                ClearPreview();
                return;
            }

            if (RTSPlayer.localPlayer == null)
            {
                UnityEngine.Debug.LogWarning("[SelectionPanel] Place failed: localPlayer is null");
                return;
            }

            BuildingType type = pendingBuildType.Value;
            worker.RequestPlaceBuilding(type, pos);
            ClearPreview();
        }

        private void ClearPreview()
        {
            pendingBuildType = null;
            pendingBuildDef = null;
            previewGhostReady = false;
            placementClickArmed = false;
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            if (selectionManager != null)
                selectionManager.SetPlacementMode(false);
        }

        private void EnsureGardenButton()
        {
            if (gardenButton != null || workerSection == null)
                return;

            GameObject btnObj = new GameObject("GardenButton");
            btnObj.transform.SetParent(workerSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-130f, -20f);
            rt.sizeDelta = new Vector2(110f, 36f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.28f, 0.5f, 0.28f, 1f);

            gardenButton = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Garden";
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void EnsureWarehouseButton()
        {
            if (warehouseButton != null || workerSection == null)
                return;

            warehouseButton = CreateBuildPanelButton(
                "WarehouseButton", "Warehouse", new Vector2(-10f, -20f),
                new Color(0.45f, 0.4f, 0.25f, 1f));
        }

        private void EnsureBarracksButton()
        {
            if (barracksButton != null || workerSection == null)
                return;

            barracksButton = CreateBuildPanelButton(
                "BarracksButton", "Barracks", new Vector2(110f, -20f),
                new Color(0.5f, 0.28f, 0.28f, 1f));
        }

        private Button CreateBuildPanelButton(string name, string label, Vector2 anchoredPos, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(workerSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(110f, 36f);

            Image img = btnObj.AddComponent<Image>();
            img.color = color;

            Button button = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void OnDepositClicked()
        {
            Worker worker = selectionManager != null ? selectionManager.SelectedWorker : null;
            if (worker == null) return;
            worker.RequestDepositToCapital();
        }

        private void EnsureDepositButton()
        {
            if (depositButton != null || workerSection == null)
                return;

            GameObject btnObj = new GameObject("DepositButton");
            btnObj.transform.SetParent(workerSection.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(130f, 30f);
            rt.sizeDelta = new Vector2(110f, 40f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.55f, 0.35f, 1f);

            depositButton = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Deposit";
            tmp.fontSize = 15;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void OnDeleteClicked()
        {
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            if (building == null) return;

            EnsureDeleteConfirmPanel();
            string name = GetBuildingDisplayName(building);
            if (deleteConfirmText != null)
                deleteConfirmText.text = $"Are you sure you want to delete {name}?";

            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(true);
        }

        private void WireDeleteConfirmButtons()
        {
            if (deleteConfirmYesButton != null)
            {
                deleteConfirmYesButton.onClick.RemoveAllListeners();
                deleteConfirmYesButton.onClick.AddListener(OnDeleteConfirmYes);
            }

            if (deleteConfirmNoButton != null)
            {
                deleteConfirmNoButton.onClick.RemoveAllListeners();
                deleteConfirmNoButton.onClick.AddListener(OnDeleteConfirmNo);
            }
        }

        private void OnDeleteConfirmYes()
        {
            Building building = selectionManager != null ? selectionManager.SelectedBuilding : null;
            HideDeleteConfirm();
            if (building == null || selectionManager == null)
                return;
            selectionManager.RequestDemolishBuilding(building);
        }

        private void OnDeleteConfirmNo()
        {
            HideDeleteConfirm();
        }

        private void HideDeleteConfirm()
        {
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);
        }

        private static string GetBuildingDisplayName(Building building)
        {
            if (building == null)
                return "Building";

            var def = BuildingManager.Instance != null
                ? BuildingManager.Instance.GetDefinition(building.Type)
                : null;
            if (def != null && !string.IsNullOrEmpty(def.displayName))
                return def.displayName;

            return building.Type.ToString();
        }

        private void EnsureDeleteConfirmPanel()
        {
            if (deleteConfirmPanel != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            // Dim overlay
            GameObject panel = new GameObject("DeleteConfirmPanel");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            Image overlay = panel.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.55f);

            // Dialog box
            GameObject box = new GameObject("Dialog");
            box.transform.SetParent(panel.transform, false);
            RectTransform boxRt = box.AddComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(420f, 160f);
            boxRt.anchoredPosition = Vector2.zero;
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            GameObject textObj = new GameObject("Message");
            textObj.transform.SetParent(box.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.45f);
            textRt.anchorMax = new Vector2(0.95f, 0.92f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            deleteConfirmText = textObj.AddComponent<TextMeshProUGUI>();
            deleteConfirmText.text = "Are you sure you want to delete Building?";
            deleteConfirmText.fontSize = 20;
            deleteConfirmText.color = Color.white;
            deleteConfirmText.alignment = TextAlignmentOptions.Center;
            deleteConfirmText.enableWordWrapping = true;

            deleteConfirmYesButton = CreateConfirmButton(box.transform, "YesButton", "Yes",
                new Vector2(-70f, -40f), new Color(0.7f, 0.2f, 0.2f, 1f));
            deleteConfirmNoButton = CreateConfirmButton(box.transform, "NoButton", "No",
                new Vector2(70f, -40f), new Color(0.3f, 0.35f, 0.4f, 1f));

            deleteConfirmPanel = panel;
            panel.SetActive(false);
            WireDeleteConfirmButtons();
        }

        private static Button CreateConfirmButton(Transform parent, string name, string label, Vector2 pos, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110f, 40f);

            Image img = btnObj.AddComponent<Image>();
            img.color = color;
            Button btn = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        private void AddHover(GameObject go, BuildingType type)
        {
            var hover = go.GetComponent<BuildButtonHover>() ?? go.AddComponent<BuildButtonHover>();
            hover.Init(type, ShowTooltip, HideTooltip);
        }

        private void ShowTooltip(BuildingType type, RectTransform buttonRect)
        {
            var def = BuildingManager.Instance != null
                ? BuildingManager.Instance.GetDefinition(type)
                : null;
            if (def == null || tooltipRoot == null || tooltipText == null)
                return;

            tooltipText.text =
                $"{def.displayName}\nWood: {def.woodCost}\nOre: {def.oreCost}\nFood: {def.foodCost}\nTime: {def.buildDuration:0.#}s";

            DisableTooltipRaycasts(tooltipRoot.gameObject);
            tooltipRoot.gameObject.SetActive(true);

            // Над кнопкой, не под курсором — не перекрывает кнопку и не мигает
            PositionTooltipAboveButton(buttonRect);
        }

        private void PositionTooltipAboveButton(RectTransform buttonRect)
        {
            if (tooltipRoot == null || buttonRect == null)
                return;

            Canvas canvas = tooltipRoot.GetComponentInParent<Canvas>();
            UnityEngine.Camera eventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCam = canvas.worldCamera;

            Vector3[] corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            // corners: 0=bl, 1=tl, 2=tr, 3=br — берём середину верхнего края
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCam, topCenter);

            tooltipRoot.pivot = new Vector2(0.5f, 0f);
            RectTransform canvasRect = canvas != null
                ? canvas.transform as RectTransform
                : tooltipRoot.parent as RectTransform;

            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, eventCam, out Vector2 localPoint))
            {
                tooltipRoot.SetParent(canvasRect, false);
                tooltipRoot.anchoredPosition = localPoint + new Vector2(0f, 12f);
            }
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null)
                tooltipRoot.gameObject.SetActive(false);
        }

        private static void DisablePreviewNetworking(GameObject go)
        {
            foreach (var nb in go.GetComponentsInChildren<Mirror.NetworkBehaviour>(true))
                nb.enabled = false;
            foreach (var ni in go.GetComponentsInChildren<Mirror.NetworkIdentity>(true))
                ni.enabled = false;

            StripPreviewPhysics(go);
            SetLayerRecursive(go, LayerMask.NameToLayer("Ignore Raycast"));
        }

        /// <summary>
        /// Preview не должен толкать юнитов / ловить raycast — выключаем и удаляем физику.
        /// </summary>
        private static void StripPreviewPhysics(GameObject go)
        {
            if (go == null) return;

            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col == null) continue;
                col.enabled = false;
                Object.Destroy(col);
            }
            foreach (var col2d in go.GetComponentsInChildren<Collider2D>(true))
            {
                if (col2d == null) continue;
                col2d.enabled = false;
                Object.Destroy(col2d);
            }
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null) continue;
                rb.detectCollisions = false;
                rb.isKinematic = true;
            }
            foreach (var rb2d in go.GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (rb2d == null) continue;
                rb2d.simulated = false;
            }
            foreach (var obstacle in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                if (obstacle == null) continue;
                obstacle.enabled = false;
                Object.Destroy(obstacle);
            }
        }
        
        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                    SetLayerRecursive(child.gameObject, layer);
            }
        }

        private static void SetPreviewGhost(GameObject go, bool validPlacement)
        {
            Color tint = validPlacement
                ? new Color(0.55f, 1f, 0.55f, 0.4f)
                : new Color(1f, 0.35f, 0.35f, 0.4f);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name.IndexOf("BuildingPreviewGhost", System.StringComparison.Ordinal) < 0)
                        mats[i] = CreateGhostMaterial(mats[i], tint);
                    else
                        ApplyGhostTint(mats[i], tint);
                }
                r.materials = mats;
            }
        }

        private static void ApplyGhostTint(Material mat, Color tint)
        {
            if (mat.HasProperty("_UnlitColor"))
                mat.SetColor("_UnlitColor", tint);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
        }

        private static Material CreateGhostMaterial(Material source, Color tint)
        {
            Shader shader = Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            if (shader == null && source != null)
                shader = source.shader;

            Material mat = shader != null ? new Material(shader) : new Material(source);
            mat.name = "BuildingPreviewGhost";

            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f); // Transparent (Standard)
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);

            return mat;
        }
    }

    public class BuildButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private BuildingType type;
        private System.Action<BuildingType, RectTransform> onEnter;
        private System.Action onExit;
        private RectTransform rect;

        public void Init(BuildingType buildingType, System.Action<BuildingType, RectTransform> enter, System.Action exit)
        {
            type = buildingType;
            onEnter = enter;
            onExit = exit;
            rect = transform as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke(type, rect);
        public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();
    }
}
