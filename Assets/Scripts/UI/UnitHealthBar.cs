using UnityEngine;
using UnityEngine.UI;

namespace RTSokak.UI
{
    /// <summary>
    /// World-space HP bar: billboard к камере, цвет fill из градиента, фон всегда чёрный.
    /// </summary>
    public class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private Gradient healthGradient;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Vector2 barSize = new Vector2(0.1f, 0.015f);

        private Transform targetTransform;
        private UnityEngine.Camera mainCamera;
        private Canvas cachedCanvas;
        private bool useFixedWorldPosition;
        private Vector3 fixedWorldPosition;
        private bool useSolidFillColor;
        private Color solidFillColor = Color.green;
        private static Sprite whiteSprite;

        /// <summary>
        /// Линейная длина от max HP (высота = baseSize.y).
        /// referenceHp=100, length=0.1 → 100 HP = 0.1, 1000 HP = 1.0.
        /// </summary>
        public static Vector2 SizeForMaxHealth(int maxHealth, Vector2 baseSize, float referenceHp = 100f)
        {
            float refHp = Mathf.Max(1f, referenceHp);
            float scale = Mathf.Max(1f, maxHealth) / refHp;
            float width = Mathf.Max(0.001f, baseSize.x * scale);
            float height = Mathf.Max(0.001f, baseSize.y);
            return new Vector2(width, height);
        }

        /// <summary>
        /// Базовый размер + градиент: глобальные настройки, либо override с префаба.
        /// </summary>
        public static void ResolveStyle(
            bool overrideBaseValues,
            Vector2 overrideSizeAtReference,
            float overrideReferenceHp,
            Gradient overrideGradient,
            out Vector2 sizeAtReference,
            out float referenceHp,
            out Gradient gradient)
        {
            HealthBarGlobalSettings global = HealthBarGlobalSettings.Get();

            if (overrideBaseValues)
            {
                sizeAtReference = new Vector2(
                    Mathf.Max(0.001f, overrideSizeAtReference.x),
                    Mathf.Max(0.001f, overrideSizeAtReference.y));
                referenceHp = Mathf.Max(1f, overrideReferenceHp);
                gradient = overrideGradient != null &&
                           overrideGradient.colorKeys != null &&
                           overrideGradient.colorKeys.Length > 0
                    ? overrideGradient
                    : global.FillGradient;
            }
            else
            {
                sizeAtReference = global.SizeAtReferenceHp;
                referenceHp = global.ReferenceHp;
                gradient = global.FillGradient;
            }
        }

        /// <summary>Создаёт HP bar в мире (не child цели — иначе крутится вместе с ней).</summary>
        public static UnitHealthBar Create(Transform target, Vector3 customOffset, Vector2 size, Gradient gradient)
        {
            GameObject root = new GameObject("HealthBar");
            root.transform.position = target.position + customOffset;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = size;
            root.transform.localScale = Vector3.one;

            GameObject sliderGo = new GameObject("HealthSlider", typeof(RectTransform));
            sliderGo.transform.SetParent(root.transform, false);
            RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            Sprite sprite = GetWhiteSprite();

            // Background — всегда чёрный (недостающее HP)
            GameObject bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sliderGo.transform, false);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Simple;
            bg.color = Color.black;
            bg.raycastTarget = false;
            Stretch(bgGo.GetComponent<RectTransform>());

            // Fill Area
            GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            Stretch(fillAreaRect);

            // Fill
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            Image fill = fillGo.AddComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Simple; // Slider сам клипает через fillRect
            fill.color = Color.green;
            fill.raycastTarget = false;
            Stretch(fillGo.GetComponent<RectTransform>());

            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.targetGraphic = fill;
            // Без handle
            slider.handleRect = null;

            UnitHealthBar bar = root.AddComponent<UnitHealthBar>();
            bar.healthSlider = slider;
            bar.fillImage = fill;
            bar.backgroundImage = bg;
            bar.offset = customOffset;
            bar.barSize = size;
            bar.healthGradient = EnsureGradient(gradient);
            bar.targetTransform = target;
            bar.mainCamera = UnityEngine.Camera.main;
            rootRect.sizeDelta = size;
            bar.UpdateHealthBar(1f);
            return bar;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            // 1x1 белая текстура — Image.color тогда реально красит полоску
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "HealthBarWhiteSprite";
            return whiteSprite;
        }

        static Gradient EnsureGradient(Gradient source)
        {
            if (source != null && source.colorKeys != null && source.colorKeys.Length > 0)
                return source;

            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.green, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return g;
        }

        public void Initialize(Transform target, Vector3 customOffset)
        {
            targetTransform = target;
            offset = customOffset;
            healthGradient = EnsureGradient(healthGradient);
            if (backgroundImage != null)
            {
                backgroundImage.sprite = GetWhiteSprite();
                backgroundImage.color = backgroundColor;
            }
            if (fillImage != null && fillImage.sprite == null)
                fillImage.sprite = GetWhiteSprite();
            UpdateHealthBar(1f);
        }

        public void UpdateHealthBar(float normalizedHealth)
        {
            if (healthSlider == null || fillImage == null)
                return;

            normalizedHealth = Mathf.Clamp01(normalizedHealth);
            healthSlider.value = normalizedHealth;

            Color c;
            if (useSolidFillColor)
            {
                c = solidFillColor;
            }
            else
            {
                healthGradient = EnsureGradient(healthGradient);
                c = healthGradient.Evaluate(normalizedHealth);
            }
            c.a = 1f;
            fillImage.color = c;

            if (backgroundImage != null)
            {
                Color bg = backgroundColor;
                bg.a = 1f;
                backgroundImage.color = bg;
            }
        }

        /// <summary>Фиксированная world-позиция (не следует за target) — для бара стройки.</summary>
        public void LockWorldPosition(Vector3 worldPosition)
        {
            useFixedWorldPosition = true;
            fixedWorldPosition = worldPosition;
            transform.position = worldPosition;
        }

        public void SetSolidFillColor(Color color)
        {
            useSolidFillColor = true;
            solidFillColor = color;
            if (healthSlider != null)
                UpdateHealthBar(healthSlider.value);
        }

        private void LateUpdate()
        {
            if (!useFixedWorldPosition && targetTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            if (cachedCanvas == null)
                cachedCanvas = GetComponent<Canvas>();

            mainCamera = ResolveBillboardCamera(mainCamera);

            // FPS: бары за дистанцией culling скрываем, не крутим каждый кадр
            var upc = RTSokak.Network.UnitPlayerController.Instance;
            if (upc != null && upc.IsActive && mainCamera != null)
            {
                Vector3 p = useFixedWorldPosition ? fixedWorldPosition
                    : (targetTransform != null ? targetTransform.position : transform.position);
                bool isBuilding = useFixedWorldPosition || (targetTransform != null && targetTransform.GetComponent<RTSokak.Buildings.Building>() != null);
                bool culled = upc.ShouldCullHealthBar(p, isBuilding);
                if (cachedCanvas != null && cachedCanvas.enabled == culled)
                    cachedCanvas.enabled = !culled;
                if (culled)
                    return;
            }
            else if (cachedCanvas != null && !cachedCanvas.enabled)
            {
                cachedCanvas.enabled = true;
            }

            transform.position = useFixedWorldPosition
                ? fixedWorldPosition
                : targetTransform.position + offset;

            if (mainCamera != null)
                transform.rotation = mainCamera.transform.rotation;
        }

        /// <summary>FPS камера юнита-игрока, иначе Camera.main.</summary>
        public static UnityEngine.Camera ResolveBillboardCamera(UnityEngine.Camera current)
        {
            var upc = RTSokak.Network.UnitPlayerController.Instance;
            if (upc != null && upc.IsActive && upc.FpsCamera != null && upc.FpsCamera.enabled)
                return upc.FpsCamera;

            if (current != null && current.enabled)
                return current;

            return UnityEngine.Camera.main;
        }

        public void SetOffset(Vector3 newOffset) => offset = newOffset;

        public void SetBarSize(Vector2 newSize)
        {
            barSize = newSize;
            var rt = GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = barSize;
        }

        public void SetGradient(Gradient gradient)
        {
            healthGradient = EnsureGradient(gradient);
            if (healthSlider != null)
                UpdateHealthBar(healthSlider.value);
        }
    }
}
