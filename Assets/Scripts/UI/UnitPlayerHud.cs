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

        public int SelectedSlot => selectedSlot;

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
            healthText.raycastTarget = false;
            healthText.text = "HP: 100 / 100";
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureHealthBarUI();
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
            if (RTSPlayer.localPlayer != null)
                RTSPlayer.localPlayer.CmdUnitPlayerSelectHotbar(-1);
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

            if (unit is Worker w)
            {
                if (!resourcesText.gameObject.activeSelf)
                    resourcesText.gameObject.SetActive(true);

                int wood = w.InventoryWood;
                int ore = w.InventoryOre;
                int food = w.InventoryFood;

                if (wood != lastWood || ore != lastOre || food != lastFood)
                {
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

        private void Show()
        {
            EnsureHealthBarUI();
            if (hudRoot != null && !hudRoot.activeSelf)
                hudRoot.SetActive(true);
            if (crosshair != null)
                crosshair.SetActive(Cursor.lockState == CursorLockMode.Locked);
        }

        private void Hide()
        {
            boundUnit = null;
            lastHp = -1;
            lastMaxHp = -1;
            lastWood = -1;
            lastOre = -1;
            lastFood = -1;
            if (hudRoot != null && hudRoot.activeSelf)
                hudRoot.SetActive(false);
        }
    }
}
