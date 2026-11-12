using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RTSokak.Network;

namespace RTSokak.UI
{
    /// <summary>
    /// HUD юнита-игрока: crosshair, хотбар инструментов, ресурсы в инвентаре юнита.
    /// Создаётся через RTSokak → UI → Create Unit Player HUD.
    /// </summary>
    public class UnitPlayerHud : MonoBehaviour
    {
        public static UnitPlayerHud Instance { get; private set; }

        public static UnitPlayerHud Ensure()
        {
            if (Instance != null)
            {
                if (!Instance.gameObject.activeSelf)
                    Instance.gameObject.SetActive(true);
                return Instance;
            }

            var existing = Object.FindFirstObjectByType<UnitPlayerHud>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                Instance = existing;
                return existing;
            }

            var go = new GameObject("UnitPlayerHudCanvas");
            var hud = go.AddComponent<UnitPlayerHud>();
            Instance = hud;
            return hud;
        }

        [Header("Roots")]
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private RectTransform hotbarRoot;
        [SerializeField] private TextMeshProUGUI resourcesText;

        [Header("Health Bar")]
        [SerializeField] private RectTransform healthBarRoot;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Hotbar")]
        [SerializeField] private Color slotNormal = new Color(0.15f, 0.16f, 0.2f, 0.9f);
        [SerializeField] private Color slotSelected = new Color(0.15f, 0.65f, 0.25f, 0.95f);
        [SerializeField] private float slotSize = 56f;
        [SerializeField] private float slotGap = 8f;

        private readonly List<Image> slotImages = new List<Image>();
        private readonly List<TextMeshProUGUI> slotLabels = new List<TextMeshProUGUI>();
        private int selectedSlot = -1; // -1 = hands empty
        private string[] currentTools = System.Array.Empty<string>();
        private RTSUnit boundUnit;

        private int lastHp = -1;
        private int lastMaxHp = -1;
        private float displayedFill = 1f;
        private int lastWood = -1;
        private int lastOre = -1;
        private int lastFood = -1;
        private Sprite whiteSprite;

        [Header("Cooldown Bar")]
        private RectTransform cooldownBarRoot;
        private Image cooldownBgImage;
        private Image cooldownFillImage;
        private float cooldownStartTime;
        private float cooldownEndTime;
        private float cooldownDuration;

        [Header("Hitmarker")]
        private RectTransform hitmarkerRoot;
        private CanvasGroup hitmarkerCg;
        private readonly RectTransform[] hitmarkerTicks = new RectTransform[4];
        private float hitmarkerEndTime;

        [Header("Garden Interaction Prompt")]
        private RectTransform gardenPromptRoot;
        private CanvasGroup gardenPromptCg;
        private TextMeshProUGUI gardenPromptText;
        private Image gardenPromptBg;
        private float gardenPromptPunch = 1.0f;

        private Coroutine resPunchRoutine;
        private Color resourcesOriginalColor = Color.white;
        private bool resourcesOriginalColorCached;

        public int SelectedSlot => selectedSlot;
        public bool IsOnCooldown => Time.time < cooldownEndTime;

        public void TriggerGardenPromptPunch()
        {
            gardenPromptPunch = 1.25f;
        }

        public void ShowHitmarker()
        {
            EnsureHitmarkerUI();
            hitmarkerEndTime = Time.unscaledTime + 0.18f;
            if (hitmarkerRoot != null)
            {
                hitmarkerRoot.gameObject.SetActive(true);
                if (hitmarkerCg != null)
                    hitmarkerCg.alpha = 1f;
            }
        }

        public void TriggerCooldown(float duration)
        {
            if (duration <= 0.01f) duration = 0.1f;
            cooldownDuration = duration;
            cooldownStartTime = Time.time;
            cooldownEndTime = Time.time + duration;
            EnsureCooldownBarUI();
            if (cooldownBarRoot != null)
            {
                cooldownBarRoot.gameObject.SetActive(true);
                UpdateCooldownBar();
            }
        }

        public void ResetCooldown()
        {
            cooldownEndTime = 0f;
            if (cooldownBarRoot != null && cooldownBarRoot.gameObject.activeSelf)
                cooldownBarRoot.gameObject.SetActive(false);
        }

        public void EnsureCooldownBarUI()
        {
            if (cooldownBarRoot != null || hudRoot == null)
                return;

            Transform existing = hudRoot.transform.Find("CooldownBar");
            if (existing != null)
            {
                cooldownBarRoot = existing as RectTransform;
                if (cooldownBarRoot != null)
                {
                    Transform fillTr = cooldownBarRoot.Find("Fill");
                    if (fillTr != null)
                        cooldownFillImage = fillTr.GetComponent<Image>();
                    return;
                }
            }

            GameObject barGo = new GameObject("CooldownBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barGo.transform.SetParent(hudRoot.transform, false);
            cooldownBarRoot = barGo.GetComponent<RectTransform>();
            cooldownBarRoot.anchorMin = new Vector2(0.5f, 0.5f);
            cooldownBarRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cooldownBarRoot.pivot = new Vector2(0.5f, 0.5f);
            cooldownBarRoot.anchoredPosition = new Vector2(0f, -18f);
            cooldownBarRoot.sizeDelta = new Vector2(36f, 4f);

            cooldownBgImage = barGo.GetComponent<Image>();
            cooldownBgImage.sprite = GetWhiteSprite();
            cooldownBgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
            cooldownBgImage.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(cooldownBarRoot, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            cooldownFillImage = fillGo.GetComponent<Image>();
            cooldownFillImage.sprite = GetWhiteSprite();
            cooldownFillImage.color = new Color(0.92f, 0.96f, 1f, 0.92f);
            cooldownFillImage.raycastTarget = false;

            barGo.SetActive(false);
        }

        private void UpdateCooldownBar()
        {
            if (cooldownBarRoot == null)
                return;

            if (Time.time < cooldownEndTime)
            {
                if (!cooldownBarRoot.gameObject.activeSelf)
                    cooldownBarRoot.gameObject.SetActive(true);

                float total = Mathf.Max(0.001f, cooldownDuration);
                float progress = Mathf.Clamp01((Time.time - cooldownStartTime) / total);
                if (cooldownFillImage != null)
                {
                    var rt = cooldownFillImage.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(progress, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }
            else
            {
                if (cooldownBarRoot.gameObject.activeSelf)
                    cooldownBarRoot.gameObject.SetActive(false);
            }
        }

        private Sprite GetWhiteSprite()
        {
            if (whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return whiteSprite;
        }

        public void EnsureHealthBarUI()
        {
            if (healthBarRoot != null || hudRoot == null)
                return;

            Transform existing = hudRoot.transform.Find("HealthBar");
            if (existing != null)
            {
                healthBarRoot = existing as RectTransform;
                if (healthBarRoot != null)
                {
                    Transform fillTr = healthBarRoot.Find("Fill");
                    if (fillTr != null)
                        healthFillImage = fillTr.GetComponent<Image>();
                    Transform textTr = healthBarRoot.Find("Text");
                    if (textTr != null)
                        healthText = textTr.GetComponent<TextMeshProUGUI>();
                    return;
                }
            }

            // Adjust resourcesText position if needed so it doesn't overlap HealthBar
            if (resourcesText != null)
            {
                var resRt = resourcesText.rectTransform;
                if (resRt != null && Mathf.Abs(resRt.anchoredPosition.y - 88f) < 5f)
                {
                    resRt.anchoredPosition = new Vector2(resRt.anchoredPosition.x, 124f);
                }
            }

            // Create HealthBar container
            GameObject hbGo = new GameObject("HealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hbGo.transform.SetParent(hudRoot.transform, false);
            healthBarRoot = hbGo.GetComponent<RectTransform>();
            healthBarRoot.anchorMin = new Vector2(0.5f, 0f);
            healthBarRoot.anchorMax = new Vector2(0.5f, 0f);
            healthBarRoot.pivot = new Vector2(0.5f, 0f);
            healthBarRoot.anchoredPosition = new Vector2(0f, 92f);
            healthBarRoot.sizeDelta = new Vector2(260f, 26f);

            var bgImage = hbGo.GetComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.12f, 0.88f);
            bgImage.raycastTarget = false;

            // Child Fill
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(healthBarRoot, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            healthFillImage = fillGo.GetComponent<Image>();
            healthFillImage.sprite = GetWhiteSprite();
            healthFillImage.type = Image.Type.Filled;
            healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFillImage.fillAmount = 1f;
            healthFillImage.color = new Color(0.2f, 0.85f, 0.3f, 1f);
            healthFillImage.raycastTarget = false;

            // Child Text
            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(healthBarRoot, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            healthText = textGo.AddComponent<TextMeshProUGUI>();
            healthText.fontSize = 15;
            healthText.fontStyle = FontStyles.Bold;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.color = Color.white;
            healthText.outlineWidth = 0.2f;
            healthText.outlineColor = Color.black;
            healthText.raycastTarget = false;
            healthText.text = "HP: 100 / 100";
        }

        public void EnsureBaseHudUI()
        {
            // Canvas check on this or parent
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (canvas.sortingOrder < 90)
                    canvas.sortingOrder = 90;
            }

            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            if (scaler == null)
                scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // IMPORTANT: hudRoot must never be this.gameObject!
            if (hudRoot == null || hudRoot == gameObject)
            {
                Transform existingRoot = transform.Find("HudRoot");
                if (existingRoot != null)
                {
                    hudRoot = existingRoot.gameObject;
                }
                else
                {
                    GameObject newRoot = new GameObject("HudRoot", typeof(RectTransform));
                    newRoot.transform.SetParent(transform, false);
                    RectTransform rt = newRoot.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    hudRoot = newRoot;
                }
            }

            // Crosshair check
            if (crosshair == null)
            {
                Transform existingCh = hudRoot.transform.Find("Crosshair");
                if (existingCh != null)
                {
                    crosshair = existingCh.gameObject;
                }
                else
                {
                    GameObject chGo = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    chGo.transform.SetParent(hudRoot.transform, false);
                    RectTransform rt = chGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(6f, 6f);

                    Image img = chGo.GetComponent<Image>();
                    img.sprite = GetWhiteSprite();
                    img.color = Color.white;
                    img.raycastTarget = false;
                    crosshair = chGo;
                }
            }

            // HotbarRoot check
            if (hotbarRoot == null)
            {
                Transform existingHb = hudRoot.transform.Find("Hotbar");
                if (existingHb != null)
                {
                    hotbarRoot = existingHb as RectTransform;
                }
                else
                {
                    GameObject hbGo = new GameObject("Hotbar", typeof(RectTransform));
                    hbGo.transform.SetParent(hudRoot.transform, false);
                    hotbarRoot = hbGo.GetComponent<RectTransform>();
                    hotbarRoot.anchorMin = new Vector2(0.5f, 0f);
                    hotbarRoot.anchorMax = new Vector2(0.5f, 0f);
                    hotbarRoot.pivot = new Vector2(0.5f, 0f);
                    hotbarRoot.anchoredPosition = new Vector2(0f, 22f);
                    hotbarRoot.sizeDelta = new Vector2(300f, 64f);
                }
            }

            // ResourcesText check
            if (resourcesText == null)
            {
                Transform existingRes = hudRoot.transform.Find("ResourcesText");
                if (existingRes != null)
                {
                    resourcesText = existingRes.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    GameObject resGo = new GameObject("ResourcesText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    resGo.transform.SetParent(hudRoot.transform, false);
                    RectTransform rt = resGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 132f);
                    rt.sizeDelta = new Vector2(420f, 32f);

                    resourcesText = resGo.GetComponent<TextMeshProUGUI>();
                    resourcesText.fontSize = 18;
                    resourcesText.fontStyle = FontStyles.Bold;
                    resourcesText.alignment = TextAlignmentOptions.Center;
                    resourcesText.color = Color.white;
                    resourcesText.outlineWidth = 0.2f;
                    resourcesText.outlineColor = Color.black;
                    resourcesText.raycastTarget = false;
                    resourcesText.text = "Wood 0   Ore 0   Food 0";
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureBaseHudUI();
            EnsureHealthBarUI();
            EnsureCooldownBarUI();
            EnsureHitmarkerUI();
            EnsureGardenPromptUI();
            if (hudRoot != null)
                hudRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (RTSPlayer.localPlayer == null ||
                RTSPlayer.localPlayer.Role != PlayerRole.UnitPlaying)
            {
                Hide();
                return;
            }

            RTSUnit unit = ResolveControlledUnit();
            if (unit == null || !unit.IsAlive)
            {
                Hide();
                return;
            }

            if (boundUnit != unit)
                BindUnit(unit);

            Show();
            UpdateCooldownBar();
            UpdateHitmarker();
            UpdateGardenPrompt();
            HandleHotbarInput();
            RefreshHealth(unit);
            RefreshResources(unit);
        }

        private RTSUnit ResolveControlledUnit()
        {
            uint id = RTSPlayer.localPlayer.ControlledUnitNetId;
            if (id == 0 || !NetworkClient.spawned.TryGetValue(id, out NetworkIdentity ni))
                return null;
            return ni.GetComponent<RTSUnit>();
        }

        private void BindUnit(RTSUnit unit)
        {
            boundUnit = unit;
            selectedSlot = -1;
            currentTools = BuildToolList(unit);
            RebuildHotbar();
            if (currentTools.Length > 0)
                SelectSlot(0);
            else
                SelectSlot(-1);
        }

        public static string[] BuildToolList(RTSUnit unit)
        {
            if (unit is Worker)
                return new[] { "Sword", "Axe", "Pickaxe" };
            if (unit is Warrior)
                return new[] { "Sword" };
            if (unit is Archer)
                return new[] { "Crossbow" };
            return System.Array.Empty<string>();
        }

        private void HandleHotbarInput()
        {
            if (currentTools.Length == 0)
                return;

            // Digits 1..9
            for (int i = 0; i < currentTools.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    ToggleSelect(i);
                    return;
                }
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            if (selectedSlot < 0)
            {
                SelectSlot(0);
                return;
            }

            int next = selectedSlot + (scroll > 0f ? -1 : 1);
            if (next < 0) next = currentTools.Length - 1;
            if (next >= currentTools.Length) next = 0;
            SelectSlot(next);
        }

        private void ToggleSelect(int index)
        {
            if (selectedSlot == index)
                SelectSlot(-1);
            else
                SelectSlot(index);
        }

        private void SelectSlot(int index)
        {
            selectedSlot = index;
            ResetCooldown();
            ApplySlotColors();
            if (RTSPlayer.localPlayer != null)
                RTSPlayer.localPlayer.CmdUnitPlayerSelectHotbar(selectedSlot);
        }

        private void RebuildHotbar()
        {
            if (hotbarRoot == null)
                return;

            for (int i = hotbarRoot.childCount - 1; i >= 0; i--)
                Destroy(hotbarRoot.GetChild(i).gameObject);
            slotImages.Clear();
            slotLabels.Clear();

            float total = currentTools.Length * slotSize + Mathf.Max(0, currentTools.Length - 1) * slotGap;
            float startX = -total * 0.5f + slotSize * 0.5f;

            for (int i = 0; i < currentTools.Length; i++)
            {
                GameObject slot = new GameObject($"Slot{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slot.transform.SetParent(hotbarRoot, false);
                var rt = slot.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(slotSize, slotSize);
                rt.anchoredPosition = new Vector2(startX + i * (slotSize + slotGap), 0f);

                var img = slot.GetComponent<Image>();
                img.color = slotNormal;
                slotImages.Add(img);

                GameObject numGo = new GameObject("Num", typeof(RectTransform));
                numGo.transform.SetParent(slot.transform, false);
                var numRt = numGo.GetComponent<RectTransform>();
                numRt.anchorMin = new Vector2(0f, 0.55f);
                numRt.anchorMax = new Vector2(1f, 1f);
                numRt.offsetMin = Vector2.zero;
                numRt.offsetMax = Vector2.zero;
                var num = numGo.AddComponent<TextMeshProUGUI>();
                num.text = (i + 1).ToString();
                num.fontSize = 14;
                num.alignment = TextAlignmentOptions.Center;
                num.color = Color.white;
                num.raycastTarget = false;

                GameObject labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(slot.transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0.6f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = currentTools[i];
                label.fontSize = 12;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                slotLabels.Add(label);
            }

            ApplySlotColors();
        }

        private void ApplySlotColors()
        {
            for (int i = 0; i < slotImages.Count; i++)
            {
                if (slotImages[i] != null)
                    slotImages[i].color = i == selectedSlot ? slotSelected : slotNormal;
            }
        }

        private void RefreshHealth(RTSUnit unit)
        {
            if (unit == null)
                return;

            EnsureHealthBarUI();
            if (healthBarRoot == null)
                return;

            int hp = Mathf.Max(0, unit.CurrentHealth);
            int maxHp = Mathf.Max(1, unit.MaxHealth);
            float targetFill = Mathf.Clamp01((float)hp / maxHp);

            displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * 12f);
            if (Mathf.Abs(displayedFill - targetFill) < 0.001f)
                displayedFill = targetFill;

            if (healthFillImage != null)
            {
                if (healthFillImage.type == Image.Type.Filled)
                {
                    healthFillImage.fillAmount = displayedFill;
                }
                else
                {
                    healthFillImage.rectTransform.anchorMax = new Vector2(displayedFill, 1f);
                }

                float ratio = (float)hp / maxHp;
                Color barColor;
                if (ratio > 0.5f)
                    barColor = new Color(0.2f, 0.85f, 0.3f, 1f);
                else if (ratio >= 0.25f)
                    barColor = new Color(0.95f, 0.8f, 0.15f, 1f);
                else
                    barColor = new Color(0.95f, 0.2f, 0.2f, 1f);

                healthFillImage.color = barColor;
            }

            if (hp != lastHp || maxHp != lastMaxHp)
            {
                lastHp = hp;
                lastMaxHp = maxHp;
                if (healthText != null)
                    healthText.text = $"HP: {hp} / {maxHp}";
            }
        }

        private void RefreshResources(RTSUnit unit)
        {
            if (resourcesText == null)
                return;

            if (!resourcesOriginalColorCached)
            {
                resourcesOriginalColor = resourcesText.color;
                resourcesOriginalColorCached = true;
            }

            if (unit is Worker w)
            {
                if (!resourcesText.gameObject.activeSelf)
                    resourcesText.gameObject.SetActive(true);

                int wood = w.InventoryWood;
                int ore = w.InventoryOre;
                int food = w.InventoryFood;

                if (wood != lastWood || ore != lastOre || food != lastFood)
                {
                    if (lastWood >= 0 && lastOre >= 0 && lastFood >= 0)
                    {
                        if (wood > lastWood)
                            OnResourceGained("Wood", wood - lastWood, new Color(0.95f, 0.7f, 0.3f, 1f));
                        if (ore > lastOre)
                            OnResourceGained("Ore", ore - lastOre, new Color(0.85f, 0.9f, 1f, 1f));
                        if (food > lastFood)
                            OnResourceGained("Food", food - lastFood, new Color(0.55f, 0.95f, 0.45f, 1f));
                    }

                    lastWood = wood;
                    lastOre = ore;
                    lastFood = food;
                    resourcesText.text = $"Wood {wood}   Ore {ore}   Food {food}";
                }
            }
            else
            {
                if (resourcesText.gameObject.activeSelf)
                    resourcesText.gameObject.SetActive(false);
            }
        }

        private void TriggerResourcePunch(Color flashColor, float punchScale = 1.22f)
        {
            if (resourcesText == null) return;
            if (!resourcesOriginalColorCached)
            {
                resourcesOriginalColor = resourcesText.color;
                resourcesOriginalColorCached = true;
            }
            if (resPunchRoutine != null)
                StopCoroutine(resPunchRoutine);
            resPunchRoutine = StartCoroutine(AnimateResourcePunch(flashColor, punchScale));
        }

        private System.Collections.IEnumerator AnimateResourcePunch(Color flashColor, float punchScale)
        {
            RectTransform rt = resourcesText.rectTransform;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Lerp(punchScale, 1.0f, t);
                rt.localScale = new Vector3(s, s, 1f);
                resourcesText.color = Color.Lerp(flashColor, resourcesOriginalColor, t);
                yield return null;
            }

            rt.localScale = Vector3.one;
            resourcesText.color = resourcesOriginalColor;
            resPunchRoutine = null;
        }

        private void OnResourceGained(string resName, int amount, Color resColor)
        {
            TriggerResourcePunch(resColor, 1.22f);
            StartCoroutine(AnimateGainPopup($"+{amount} {resName}", resColor));
        }

        private System.Collections.IEnumerator AnimateGainPopup(string text, Color color)
        {
            if (resourcesText == null || hudRoot == null) yield break;

            GameObject popGo = new GameObject("ResourceGainPop", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            popGo.transform.SetParent(resourcesText.transform.parent != null ? resourcesText.transform.parent : hudRoot.transform, false);

            RectTransform rt = popGo.GetComponent<RectTransform>();
            rt.anchorMin = resourcesText.rectTransform.anchorMin;
            rt.anchorMax = resourcesText.rectTransform.anchorMax;
            rt.pivot = resourcesText.rectTransform.pivot;
            Vector2 startPos = resourcesText.rectTransform.anchoredPosition + new Vector2(0f, 26f);
            rt.anchoredPosition = startPos;
            rt.sizeDelta = new Vector2(200f, 30f);

            TextMeshProUGUI tmp = popGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;

            float duration = 0.9f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Плавно взлетает вверх на 40 пикселей
                rt.anchoredPosition = startPos + new Vector2(0f, 40f * t);

                // Pop-in масштаб (0.5 -> 1.15 -> 1.0)
                float scale;
                if (t < 0.2f)
                    scale = Mathf.Lerp(0.5f, 1.15f, t / 0.2f);
                else if (t < 0.35f)
                    scale = Mathf.Lerp(1.15f, 1.0f, (t - 0.2f) / 0.15f);
                else
                    scale = 1.0f;
                rt.localScale = new Vector3(scale, scale, 1f);

                // Растворяется за 0.9f сек
                float alpha = t > 0.6f ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;
                Color c = color;
                c.a = alpha;
                tmp.color = c;

                yield return null;
            }

            Destroy(popGo);
        }

        public void OnPlayerDeposited(int wood, int ore, int food)
        {
            if (wood <= 0 && ore <= 0 && food <= 0)
                return;

            TriggerResourcePunch(new Color(0.35f, 1f, 0.5f, 1f), 1.35f);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Deposited to Treasury: ");
            bool first = true;
            if (wood > 0)
            {
                sb.Append($"+{wood} Wood");
                first = false;
            }
            if (ore > 0)
            {
                if (!first) sb.Append("   ");
                sb.Append($"+{ore} Ore");
                first = false;
            }
            if (food > 0)
            {
                if (!first) sb.Append("   ");
                sb.Append($"+{food} Food");
            }

            StartCoroutine(AnimateDepositBanner(sb.ToString()));
        }

        private System.Collections.IEnumerator AnimateDepositBanner(string bannerText)
        {
            if (hudRoot == null) yield break;

            GameObject bannerGo = new GameObject("DepositBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            bannerGo.transform.SetParent(hudRoot.transform, false);

            RectTransform rt = bannerGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 startPos = new Vector2(0f, 175f);
            rt.anchoredPosition = startPos;
            rt.sizeDelta = new Vector2(400f, 44f);

            var bg = bannerGo.GetComponent<Image>();
            bg.sprite = GetWhiteSprite();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            bg.raycastTarget = false;

            var cg = bannerGo.GetComponent<CanvasGroup>();

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(bannerGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = bannerText;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.45f, 1f, 0.55f, 1f);
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;

            float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Плавно поднимается вверх
                rt.anchoredPosition = startPos + new Vector2(0f, 35f * t);

                // Пружинящий scale: 0.4 -> 1.12 -> 1.0
                float scale;
                if (t < 0.18f)
                    scale = Mathf.Lerp(0.4f, 1.12f, t / 0.18f);
                else if (t < 0.32f)
                    scale = Mathf.Lerp(1.12f, 1.0f, (t - 0.18f) / 0.14f);
                else
                    scale = 1.0f;
                rt.localScale = new Vector3(scale, scale, 1f);

                // Исчезает в конце
                if (cg != null)
                {
                    cg.alpha = t > 0.7f ? Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f) : 1f;
                }

                yield return null;
            }

            Destroy(bannerGo);
        }

        public void EnsureHitmarkerUI()
        {
            if (hitmarkerRoot != null || hudRoot == null)
                return;

            Transform existing = hudRoot.transform.Find("Hitmarker");
            if (existing != null)
            {
                hitmarkerRoot = existing as RectTransform;
                hitmarkerCg = existing.GetComponent<CanvasGroup>();
                for (int i = 0; i < 4; i++)
                {
                    Transform t = hitmarkerRoot.Find($"Tick_{i}");
                    if (t != null) hitmarkerTicks[i] = t as RectTransform;
                }
                return;
            }

            GameObject hmGo = new GameObject("Hitmarker", typeof(RectTransform), typeof(CanvasGroup));
            hmGo.transform.SetParent(hudRoot.transform, false);
            hitmarkerRoot = hmGo.GetComponent<RectTransform>();
            hitmarkerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            hitmarkerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            hitmarkerRoot.pivot = new Vector2(0.5f, 0.5f);
            hitmarkerRoot.anchoredPosition = Vector2.zero;
            hitmarkerRoot.sizeDelta = Vector2.zero;

            hitmarkerCg = hmGo.GetComponent<CanvasGroup>();
            hitmarkerCg.blocksRaycasts = false;
            hitmarkerCg.interactable = false;

            float[] angles = new float[] { -45f, 45f, -45f, 45f };
            for (int i = 0; i < 4; i++)
            {
                GameObject tickGo = new GameObject($"Tick_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tickGo.transform.SetParent(hitmarkerRoot, false);
                RectTransform rt = tickGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(2.5f, 8f);
                rt.localRotation = Quaternion.Euler(0f, 0f, angles[i]);

                Image img = tickGo.GetComponent<Image>();
                img.sprite = GetWhiteSprite();
                img.color = new Color(1f, 0.25f, 0.25f, 0.95f);
                img.raycastTarget = false;

                hitmarkerTicks[i] = rt;
            }

            hmGo.SetActive(false);
        }

        private void UpdateHitmarker()
        {
            if (hitmarkerRoot == null)
                return;

            if (Time.unscaledTime < hitmarkerEndTime)
            {
                if (!hitmarkerRoot.gameObject.activeSelf)
                    hitmarkerRoot.gameObject.SetActive(true);

                float t = Mathf.Clamp01((hitmarkerEndTime - Time.unscaledTime) / 0.18f);
                float dist = Mathf.Lerp(8f, 16f, 1f - t);

                if (hitmarkerTicks[0] != null) hitmarkerTicks[0].anchoredPosition = new Vector2(dist, dist);
                if (hitmarkerTicks[1] != null) hitmarkerTicks[1].anchoredPosition = new Vector2(dist, -dist);
                if (hitmarkerTicks[2] != null) hitmarkerTicks[2].anchoredPosition = new Vector2(-dist, -dist);
                if (hitmarkerTicks[3] != null) hitmarkerTicks[3].anchoredPosition = new Vector2(-dist, dist);

                if (hitmarkerCg != null)
                    hitmarkerCg.alpha = Mathf.Clamp01(t * 1.5f);
            }
            else
            {
                if (hitmarkerRoot.gameObject.activeSelf)
                    hitmarkerRoot.gameObject.SetActive(false);
            }
        }

        public void EnsureGardenPromptUI()
        {
            if (gardenPromptRoot != null || hudRoot == null)
                return;

            Transform existing = hudRoot.transform.Find("GardenPrompt");
            if (existing != null)
            {
                gardenPromptRoot = existing as RectTransform;
                gardenPromptCg = existing.GetComponent<CanvasGroup>();
                gardenPromptBg = existing.GetComponent<Image>();
                Transform textTr = existing.Find("Text");
                if (textTr != null)
                    gardenPromptText = textTr.GetComponent<TextMeshProUGUI>();
                return;
            }

            GameObject promptGo = new GameObject("GardenPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            promptGo.transform.SetParent(hudRoot.transform, false);

            gardenPromptRoot = promptGo.GetComponent<RectTransform>();
            gardenPromptRoot.anchorMin = new Vector2(0.5f, 0.5f);
            gardenPromptRoot.anchorMax = new Vector2(0.5f, 0.5f);
            gardenPromptRoot.pivot = new Vector2(0.5f, 0.5f);
            gardenPromptRoot.anchoredPosition = new Vector2(0f, -68f);
            gardenPromptRoot.sizeDelta = new Vector2(380f, 36f);

            gardenPromptBg = promptGo.GetComponent<Image>();
            gardenPromptBg.sprite = GetWhiteSprite();
            gardenPromptBg.color = new Color(0.06f, 0.08f, 0.1f, 0.88f);
            gardenPromptBg.raycastTarget = false;

            gardenPromptCg = promptGo.GetComponent<CanvasGroup>();
            gardenPromptCg.alpha = 0f;
            gardenPromptCg.blocksRaycasts = false;
            gardenPromptCg.interactable = false;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(promptGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            gardenPromptText = textGo.AddComponent<TextMeshProUGUI>();
            gardenPromptText.fontSize = 15;
            gardenPromptText.fontStyle = FontStyles.Bold;
            gardenPromptText.alignment = TextAlignmentOptions.Center;
            gardenPromptText.color = Color.white;
            gardenPromptText.outlineWidth = 0.2f;
            gardenPromptText.outlineColor = Color.black;
            gardenPromptText.raycastTarget = false;

            promptGo.SetActive(false);
        }

        private void UpdateGardenPrompt()
        {
            EnsureGardenPromptUI();
            if (gardenPromptRoot == null || gardenPromptCg == null)
                return;

            float targetAlpha = 0f;
            string promptStr = "";
            Color promptColor = Color.white;

            if (boundUnit is Worker worker && worker.IsAlive)
            {
                var upc = UnitPlayerController.Instance;
                Vector3 lookDir = upc != null && upc.FpsCamera != null
                    ? upc.FpsCamera.transform.forward
                    : worker.transform.forward;

                var buildings = Buildings.Building.AllBuildings;
                Buildings.GardenBed bestGarden = null;
                int bestPlot = -1;
                Buildings.GardenPlotState bestState = Buildings.GardenPlotState.Empty;
                float bestProgress = 0f;
                float bestDist = float.MaxValue;

                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b == null || !b.IsComplete || !b.IsOwnedByLocalPlayer())
                        continue;

                    var garden = b.GetComponent<Buildings.GardenBed>();
                    if (garden == null)
                        continue;

                    float d = Vector3.Distance(worker.transform.position, garden.transform.position);
                    if (d > 8f)
                        continue;

                    if (garden.TryFindPlayerInteractablePlot(worker.transform.position, lookDir, 2.6f, out int plot, out Buildings.GardenPlotState s, out float prog))
                    {
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestGarden = garden;
                            bestPlot = plot;
                            bestState = s;
                            bestProgress = prog;
                        }
                    }
                }

                if (bestGarden != null && bestPlot >= 0)
                {
                    targetAlpha = 1f;
                    if (worker.IsInventoryFull && bestState == Buildings.GardenPlotState.Ready)
                    {
                        promptStr = "Инвентарь полон! Отнесите ресурсы на склад";
                        promptColor = Color.Lerp(new Color(1f, 0.4f, 0.15f), new Color(1f, 0.15f, 0.15f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
                    }
                    else if (bestState == Buildings.GardenPlotState.Ready)
                    {
                        promptStr = "[F] Собрать пшеницу и засеять (+50 Food)";
                        promptColor = new Color(0.85f, 1f, 0.25f, 1f);
                    }
                    else if (bestState == Buildings.GardenPlotState.Empty)
                    {
                        promptStr = "[F] Посадить пшеницу";
                        promptColor = new Color(0.45f, 1f, 0.55f, 1f);
                    }
                    else if (bestState == Buildings.GardenPlotState.Growing)
                    {
                        promptStr = $"Пшеница растёт... {Mathf.RoundToInt(bestProgress * 100f)}%";
                        promptColor = new Color(0.85f, 0.92f, 1f, 0.9f);
                    }
                }
            }

            if (targetAlpha > 0f)
            {
                if (!gardenPromptRoot.gameObject.activeSelf)
                    gardenPromptRoot.gameObject.SetActive(true);

                if (gardenPromptText != null)
                {
                    gardenPromptText.text = promptStr;
                    gardenPromptText.color = promptColor;
                }
            }

            gardenPromptCg.alpha = Mathf.MoveTowards(gardenPromptCg.alpha, targetAlpha, Time.unscaledDeltaTime * 6f);
            gardenPromptPunch = Mathf.MoveTowards(gardenPromptPunch, 1.0f, Time.unscaledDeltaTime * 2.5f);

            float breath = 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 5f);
            float scale = Mathf.Lerp(0.75f, 1.0f, gardenPromptCg.alpha) * breath * gardenPromptPunch;
            gardenPromptRoot.localScale = new Vector3(scale, scale, 1f);

            if (gardenPromptCg.alpha <= 0.01f && targetAlpha == 0f && gardenPromptRoot.gameObject.activeSelf)
            {
                gardenPromptRoot.gameObject.SetActive(false);
            }
        }

        private void Show()
        {
            EnsureBaseHudUI();
            EnsureHealthBarUI();
            EnsureCooldownBarUI();
            EnsureHitmarkerUI();
            EnsureGardenPromptUI();
            if (hudRoot != null && !hudRoot.activeSelf)
                hudRoot.SetActive(true);
            if (crosshair != null)
                crosshair.SetActive(Cursor.lockState == CursorLockMode.Locked);
        }

        private void Hide()
        {
            boundUnit = null;
            ResetCooldown();
            lastHp = -1;
            lastMaxHp = -1;
            lastWood = -1;
            lastOre = -1;
            lastFood = -1;
            if (hitmarkerRoot != null && hitmarkerRoot.gameObject.activeSelf)
                hitmarkerRoot.gameObject.SetActive(false);
            if (gardenPromptRoot != null && gardenPromptRoot.gameObject.activeSelf)
                gardenPromptRoot.gameObject.SetActive(false);
            if (gardenPromptCg != null)
                gardenPromptCg.alpha = 0f;
            if (hudRoot != null && hudRoot.activeSelf)
                hudRoot.SetActive(false);
        }
    }
}
