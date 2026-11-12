using UnityEngine;
using TMPro;

namespace RTSokak.UI
{
    /// <summary>
    /// Летающий текст который появляется, улетает вверх и исчезает
    /// Используется для отображения +N Wood/Ore при добыче и сдаче ресурсов
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float fadeStartTime = 1.0f;
        
        [Header("Randomization")]
        [SerializeField] private Vector3 randomOffset = new Vector3(0.3f, 0, 0.3f);
        
        private TextMeshProUGUI textComponent;
        private RectTransform rectTransform;
        private UnityEngine.Camera mainCamera;
        private Vector3 worldPosition;
        private Vector3 velocity;
        private float spawnTime;
        private Color originalColor;

        private void Awake()
        {
            textComponent = GetComponent<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();
            
            if (textComponent != null)
            {
                originalColor = textComponent.color;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
            }
        }

        public static UnityEngine.Camera ResolveActiveCamera()
        {
            if (RTSokak.Network.UnitPlayerController.Instance != null &&
                RTSokak.Network.UnitPlayerController.Instance.IsActive &&
                RTSokak.Network.UnitPlayerController.Instance.FpsCamera != null)
            {
                return RTSokak.Network.UnitPlayerController.Instance.FpsCamera;
            }
            return UnityEngine.Camera.main;
        }

        private void Start()
        {
            mainCamera = ResolveActiveCamera();
            spawnTime = Time.time;
            
            // Добавляем случайное смещение
            Vector3 randomDir = new Vector3(
                Random.Range(-randomOffset.x, randomOffset.x),
                0,
                Random.Range(-randomOffset.z, randomOffset.z)
            );
            worldPosition += randomDir;
            
            // Начальная скорость вверх
            velocity = Vector3.up * moveSpeed;
        }

        private void Update()
        {
            mainCamera = ResolveActiveCamera();
            if (mainCamera == null) return;

            float elapsed = Time.time - spawnTime;

            // Уничтожаем после lifetime
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Анимация масштаба (scale punch): 0.5 -> 1.15 -> 1.0 за первые 0.18 сек
            if (rectTransform != null)
            {
                if (elapsed < 0.18f)
                {
                    float t = elapsed / 0.18f;
                    float s = t < 0.65f
                        ? Mathf.Lerp(0.5f, 1.15f, t / 0.65f)
                        : Mathf.Lerp(1.15f, 1.0f, (t - 0.65f) / 0.35f);
                    rectTransform.localScale = new Vector3(s, s, 1f);
                }
                else if (rectTransform.localScale != Vector3.one)
                {
                    rectTransform.localScale = Vector3.one;
                }
            }

            // Двигаем world position вверх
            worldPosition += velocity * Time.deltaTime;

            // Конвертируем world position в screen position
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPosition);

            // Проверяем что точка перед камерой
            if (screenPos.z > 0)
            {
                rectTransform.position = screenPos;
            }
            else
            {
                // Позади камеры -- скрываем
                rectTransform.position = new Vector3(-10000, -10000, 0);
            }

            // Fade out
            if (elapsed >= fadeStartTime && textComponent != null)
            {
                float fadeProgress = (elapsed - fadeStartTime) / (lifetime - fadeStartTime);
                Color color = originalColor;
                color.a = Mathf.Lerp(1f, 0f, fadeProgress);
                textComponent.color = color;
            }
        }

        /// <summary>
        /// Инициализация floating text
        /// </summary>
        public void Initialize(Vector3 worldPos, string text, Color color)
        {
            worldPosition = worldPos;
            
            if (textComponent != null)
            {
                textComponent.text = text;
                textComponent.color = color;
                originalColor = color;
            }
        }

        /// <summary>
        /// Создать floating text на canvas
        /// </summary>
        public static void Create(Vector3 worldPos, string text, Color color, Canvas canvas)
        {
            Create(worldPos, text, color, canvas, 2f);
        }

        public static void Create(Vector3 worldPos, string text, Color color, Canvas canvas, float duration)
        {
            if (canvas == null)
            {
                UnityEngine.Debug.LogWarning("FloatingText.Create: Canvas is null");
                return;
            }

            // Создаём GameObject
            GameObject go = new GameObject("FloatingText");
            go.transform.SetParent(canvas.transform, false);

            // Добавляем RectTransform
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 50);

            // Добавляем TextMeshProUGUI
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            
            // Добавляем outline для читаемости
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            // Добавляем FloatingText компонент
            FloatingText ft = go.AddComponent<FloatingText>();
            ft.lifetime = duration;
            ft.Initialize(worldPos, text, color);
            
            // СРАЗУ установить экранную позицию чтобы не было телепортации
            UnityEngine.Camera mainCam = ResolveActiveCamera();
            if (mainCam != null)
            {
                Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
                if (screenPos.z > 0)
                {
                    rt.position = screenPos;
                }
            }
        }
    }
}
