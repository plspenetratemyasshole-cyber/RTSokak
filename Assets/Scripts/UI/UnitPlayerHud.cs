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

        [Header("Cooldown Bar")]
        private RectTransform cooldownBarRoot;
        private Image cooldownBgImage;
        private Image cooldownFillImage;
        private float cooldownStartTime;
        private float cooldownEndTime;
        private float cooldownDuration;

        private Coroutine resPunchRoutine;
        private Color resourcesOriginalColor = Color.white;
        private bool resourcesOriginalColorCached;

        public int SelectedSlot => selectedSlot;
        public bool IsOnCooldown => Time.time < cooldownEndTime;

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
            EnsureCooldownBarUI();
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

        private void Show()
        {
            EnsureHealthBarUI();
            EnsureCooldownBarUI();
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
            if (hudRoot != null && hudRoot.activeSelf)
                hudRoot.SetActive(false);
        }
    }
}
