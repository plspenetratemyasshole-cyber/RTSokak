using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTSokak.UI
{
    /// <summary>
    /// Procedural 9-slice rounded rectangle and border sprites cached at runtime.
    /// </summary>
    public static class UIThemeSprites
    {
        private static readonly Dictionary<int, Sprite> rectCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> borderCache = new Dictionary<int, Sprite>();

        public static Sprite GetRoundedRect(int radius = 12)
        {
            radius = Mathf.Clamp(radius, 2, 30);
            if (rectCache.TryGetValue(radius, out Sprite cached) && cached != null)
                return cached;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UITheme_RoundedRect_{radius}"
            };

            Color32[] pixels = new Color32[size * size];
            float r = radius;
            float boxHalf = 32f - r;

            for (int y = 0; y < size; y++)
            {
                float py = (y + 0.5f) - 32f;
                float qy = Mathf.Abs(py) - boxHalf;

                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) - 32f;
                    float qx = Mathf.Abs(px) - boxHalf;

                    float ox = Mathf.Max(qx, 0f);
                    float oy = Mathf.Max(qy, 0f);
                    float dist = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;

                    float alpha = Mathf.Clamp01(0.5f - dist);
                    byte a = (byte)(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            Vector4 border = new Vector4(radius, radius, radius, radius);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            rectCache[radius] = sprite;
            return sprite;
        }

        public static Sprite GetRoundedBorder(int radius = 12, int borderPx = 2)
        {
            radius = Mathf.Clamp(radius, 2, 30);
            borderPx = Mathf.Clamp(borderPx, 1, 16);
            int key = (radius << 8) | borderPx;

            if (borderCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"UITheme_RoundedBorder_{radius}_{borderPx}"
            };

            Color32[] pixels = new Color32[size * size];
            float r = radius;
            float boxHalf = 32f - r;
            float bPx = borderPx;

            for (int y = 0; y < size; y++)
            {
                float py = (y + 0.5f) - 32f;
                float qy = Mathf.Abs(py) - boxHalf;

                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) - 32f;
                    float qx = Mathf.Abs(px) - boxHalf;

                    float ox = Mathf.Max(qx, 0f);
                    float oy = Mathf.Max(qy, 0f);
                    float distOuter = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float distInner = distOuter + bPx;

                    float alphaOuter = Mathf.Clamp01(0.5f - distOuter);
                    float alphaInner = Mathf.Clamp01(distInner + 0.5f);
                    float alpha = Mathf.Min(alphaOuter, alphaInner);

                    byte a = (byte)(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            Vector4 border = new Vector4(radius, radius, radius, radius);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            borderCache[key] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// Modern tactical dark-slate & emerald/gold theme palette.
    /// </summary>
    public static class UIThemePalette
    {
        public static readonly Color OverlayDim    = new Color(0.03f, 0.05f, 0.07f, 0.72f);
        public static readonly Color PanelDark     = new Color(0.07f, 0.09f, 0.12f, 0.93f);
        public static readonly Color PanelCard     = new Color(0.11f, 0.14f, 0.18f, 0.94f);
        public static readonly Color BorderSubtle  = new Color(0.32f, 0.42f, 0.52f, 0.35f);
        public static readonly Color ButtonPrimary = new Color(0.14f, 0.48f, 0.30f, 0.96f);
        public static readonly Color ButtonNeutral = new Color(0.16f, 0.21f, 0.27f, 0.95f);
        public static readonly Color ButtonDanger  = new Color(0.58f, 0.16f, 0.18f, 0.96f);
        public static readonly Color InputBg       = new Color(0.09f, 0.11f, 0.15f, 0.98f);
        public static readonly Color AccentEmerald = new Color(0.24f, 0.86f, 0.50f, 1f);
        public static readonly Color AccentGold    = new Color(0.98f, 0.80f, 0.28f, 1f);
    }

    /// <summary>
    /// Smooth hover, press, and disabled animations for UI Buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.045f;
        [SerializeField] private float pressedScale = 0.95f;
        [SerializeField] private float animSpeed = 16f;

        private Button button;
        private Image targetImage;
        private Color baseColor = Color.white;
        private bool baseColorSet;
        private bool isHovered;
        private bool isPressed;

        public float HoverScale
        {
            get => hoverScale;
            set => hoverScale = value;
        }

        public void SetBaseColor(Color color)
        {
            baseColor = color;
            baseColorSet = true;
            if (targetImage != null)
                targetImage.color = color;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            if (targetImage == null)
            {
                targetImage = (button != null && button.targetGraphic is Image img) ? img : GetComponent<Image>();
            }

            if (targetImage != null && !baseColorSet)
            {
                baseColor = targetImage.color;
                baseColorSet = true;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool interactable = button == null || button.interactable;

            float targetScale = 1.0f;
            Color targetColor = baseColor;

            if (!interactable)
            {
                targetScale = 1.0f;
                targetColor = new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, baseColor.a * 0.5f);
                isHovered = false;
                isPressed = false;
            }
            else if (isPressed)
            {
                targetScale = pressedScale;
                targetColor = Color.Lerp(baseColor, Color.black, 0.15f);
            }
            else if (isHovered)
            {
                targetScale = hoverScale;
                targetColor = Color.Lerp(baseColor, Color.white, 0.12f);
            }

            float t = 1f - Mathf.Exp(-animSpeed * dt);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, t);

            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(targetImage.color, targetColor, t);
            }
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            transform.localScale = Vector3.one;

            if (targetImage != null && baseColorSet)
                targetImage.color = baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || button.interactable)
                isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button == null || button.interactable)
                isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }
    }

    /// <summary>
    /// Smooth entrance animation (alpha, scale, slide) for UI panels.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPanelAnimator : MonoBehaviour
    {
        public float duration = 0.16f;
        public float startScale = 0.97f;
        public Vector2 slideOffset = new Vector2(0f, -10f);

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 baseAnchoredPosition;
        private bool hasCachedBasePosition;
        private Coroutine animRoutine;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            EnsureCanvasGroup();
            CacheBasePosition();
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        private void CacheBasePosition()
        {
            if (!hasCachedBasePosition && rectTransform != null)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                hasCachedBasePosition = true;
            }
        }

        private void OnEnable()
        {
            EnsureCanvasGroup();
            CacheBasePosition();

            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }

            if (rectTransform != null)
                rectTransform.anchoredPosition = baseAnchoredPosition + slideOffset;

            transform.localScale = Vector3.one * startScale;
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            animRoutine = StartCoroutine(AnimateIn());
        }

        private void OnDisable()
        {
            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }

            if (hasCachedBasePosition && rectTransform != null)
                rectTransform.anchoredPosition = baseAnchoredPosition;

            transform.localScale = Vector3.one;

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private IEnumerator AnimateIn()
        {
            float dur = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // EaseOutCubic
                float p = 1f - Mathf.Pow(1f - t, 3f);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, p);

                transform.localScale = Vector3.LerpUnclamped(Vector3.one * startScale, Vector3.one, p);

                if (rectTransform != null)
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(baseAnchoredPosition + slideOffset, baseAnchoredPosition, p);

                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            transform.localScale = Vector3.one;

            if (rectTransform != null)
                rectTransform.anchoredPosition = baseAnchoredPosition;

            animRoutine = null;
        }
    }

    /// <summary>
    /// Pops scale and flashes text color briefly when a value changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIValuePunch : MonoBehaviour
    {
        private TextMeshProUGUI targetText;
        private Color originalColor = Color.white;
        private bool originalColorCached;
        private Vector3 baseScale = Vector3.one;
        private Coroutine punchRoutine;

        private void Awake()
        {
            targetText = GetComponent<TextMeshProUGUI>();
            if (targetText != null)
            {
                originalColor = targetText.color;
                originalColorCached = true;
            }
            baseScale = transform.localScale;
        }

        public void Punch(Color flashColor, float scale = 1.22f, float duration = 0.25f)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (targetText == null)
                targetText = GetComponent<TextMeshProUGUI>();

            if (targetText != null && !originalColorCached)
            {
                originalColor = targetText.color;
                originalColorCached = true;
            }

            if (punchRoutine != null)
                StopCoroutine(punchRoutine);

            punchRoutine = StartCoroutine(DoPunch(flashColor, scale, duration));
        }

        private IEnumerator DoPunch(Color flashColor, float punchScale, float dur)
        {
            float totalDur = Mathf.Max(0.02f, dur);
            float elapsed = 0f;

            if (targetText != null)
                targetText.color = flashColor;

            while (elapsed < totalDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / totalDur);

                float s = Mathf.Lerp(punchScale, 1f, 1f - Mathf.Pow(1f - t, 2f));
                transform.localScale = baseScale * s;

                if (targetText != null)
                    targetText.color = Color.Lerp(flashColor, originalColor, t);

                yield return null;
            }

            transform.localScale = baseScale;
            if (targetText != null)
                targetText.color = originalColor;

            punchRoutine = null;
        }

        private void OnDisable()
        {
            if (punchRoutine != null)
            {
                StopCoroutine(punchRoutine);
                punchRoutine = null;
            }

            transform.localScale = baseScale;
            if (targetText != null && originalColorCached)
                targetText.color = originalColor;
        }
    }

    /// <summary>
    /// Helper functions for applying rounded styling and animation to UI elements.
    /// </summary>
    public static class UIStyleHelper
    {
        public static void ApplyRoundedCard(Image img, Color bgColor, Color borderColor, int radius = 12)
        {
            if (img == null) return;

            img.sprite = UIThemeSprites.GetRoundedRect(radius);
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            Transform borderTr = img.transform.Find("UIBorder");
            Image borderImg;
            if (borderTr != null)
            {
                borderImg = borderTr.GetComponent<Image>();
            }
            else
            {
                GameObject borderGo = new GameObject("UIBorder", typeof(RectTransform), typeof(Image));
                borderGo.transform.SetParent(img.transform, false);
                borderGo.transform.SetAsLastSibling();
                RectTransform rt = borderGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                borderImg = borderGo.GetComponent<Image>();
            }

            if (borderImg != null)
            {
                borderImg.sprite = UIThemeSprites.GetRoundedBorder(radius, 2);
                borderImg.type = Image.Type.Sliced;
                borderImg.color = borderColor;
                borderImg.raycastTarget = false;
            }
        }

        public static void StyleButton(Button btn, Color bgColor, bool compact = false)
        {
            if (btn == null) return;

            Image img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
            if (img == null)
                img = btn.gameObject.AddComponent<Image>();

            btn.targetGraphic = img;
            ApplyRoundedCard(img, bgColor, UIThemePalette.BorderSubtle, compact ? 8 : 10);

            TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;

                Shadow shadow = tmp.GetComponent<Shadow>();
                if (shadow == null)
                    shadow = tmp.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }

            btn.transition = Selectable.Transition.None;

            UIButtonJuice juice = btn.GetComponent<UIButtonJuice>();
            if (juice == null)
                juice = btn.gameObject.AddComponent<UIButtonJuice>();
            juice.SetBaseColor(bgColor);
            if (compact)
                juice.HoverScale = 1.02f;
        }

        public static void StyleInputAndDropdowns(GameObject root)
        {
            if (root == null) return;

            var inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input == null) continue;
                Image img = input.targetGraphic as Image ?? input.GetComponent<Image>();
                if (img != null)
                    ApplyRoundedCard(img, UIThemePalette.InputBg, UIThemePalette.BorderSubtle, 8);
            }

            var dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                var dd = dropdowns[i];
                if (dd == null) continue;
                Image img = dd.targetGraphic as Image ?? dd.GetComponent<Image>();
                if (img != null)
                    ApplyRoundedCard(img, UIThemePalette.InputBg, UIThemePalette.BorderSubtle, 8);
            }
        }

        public static void StyleAllButtonsIn(GameObject root)
        {
            if (root == null) return;

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                string name = btn.gameObject.name;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                string label = tmp != null ? tmp.text : "";
                string lower = (name + " " + label).ToLowerInvariant();

                Color col;
                if (lower.Contains("delete") || lower.Contains("exit") || lower.Contains("sign out"))
                {
                    col = UIThemePalette.ButtonDanger;
                }
                else if (lower.Contains("play") || lower.Contains("continue") || lower.Contains("apply") ||
                         lower.Contains("resume") || lower.Contains("mayor") || lower.Contains("connect"))
                {
                    col = UIThemePalette.ButtonPrimary;
                }
                else
                {
                    col = UIThemePalette.ButtonNeutral;
                }

                StyleButton(btn, col);
            }
        }
    }
}
