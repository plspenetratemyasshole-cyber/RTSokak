using UnityEngine;

namespace RTSokak.UI
{
    /// <summary>
    /// Менеджер для создания floating text надписей
    /// Singleton для доступа из любого места кода
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        private static FloatingTextManager instance;
        public static FloatingTextManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<FloatingTextManager>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("FloatingTextManager");
                        instance = go.AddComponent<FloatingTextManager>();
                    }
                }
                return instance;
            }
        }

        [Header("References")]
        [SerializeField] private Canvas floatingTextCanvas;

        [Header("Colors")]
        [SerializeField] private Color woodColor = new Color(0.6f, 0.4f, 0.2f); // Коричневый
        [SerializeField] private Color oreColor = new Color(0.7f, 0.7f, 0.8f);  // Серый металлический
        [SerializeField] private Color foodColor = new Color(0.9f, 0.7f, 0.3f); // Желтоватый

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Создаём canvas если его нет
            if (floatingTextCanvas == null)
            {
                CreateFloatingTextCanvas();
            }
        }

        /// <summary>
        /// Создать canvas для floating text
        /// </summary>
        private void CreateFloatingTextCanvas()
        {
            GameObject canvasGO = new GameObject("FloatingTextCanvas");
            canvasGO.transform.SetParent(transform);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Поверх всего остального UI

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            floatingTextCanvas = canvas;
            UnityEngine.Debug.Log("FloatingTextCanvas created");
        }

        /// <summary>
        /// Показать floating text для добычи дерева
        /// </summary>
        public void ShowWoodGained(Vector3 worldPos, int amount)
        {
            ShowResourceGained(worldPos, amount, "Wood", woodColor);
        }

        /// <summary>
        /// Показать floating text для добычи руды
        /// </summary>
        public void ShowOreGained(Vector3 worldPos, int amount)
        {
            ShowResourceGained(worldPos, amount, "Ore", oreColor);
        }

        /// <summary>
        /// Показать floating text для добычи еды
        /// </summary>
        public void ShowFoodGained(Vector3 worldPos, int amount)
        {
            ShowResourceGained(worldPos, amount, "Food", foodColor);
        }

        /// <summary>
        /// Показать floating text для добычи ресурса
        /// </summary>
        public void ShowResourceGained(Vector3 worldPos, int amount, string resourceName, Color color)
        {
            if (floatingTextCanvas == null)
            {
                CreateFloatingTextCanvas();
            }

            string text = $"+{amount} {resourceName}";
            FloatingText.Create(worldPos, text, color, floatingTextCanvas);
        }

        /// <summary>
        /// Показать floating text для сдачи ресурсов в столицу
        /// </summary>
        public void ShowResourceDeposited(Vector3 worldPos, int woodAmount, int oreAmount, int foodAmount)
        {
            if (floatingTextCanvas == null)
            {
                CreateFloatingTextCanvas();
            }

            // Показываем каждый ресурс если количество > 0
            Vector3 offset = Vector3.zero;

            if (woodAmount > 0)
            {
                string text = $"+{woodAmount} Wood";
                FloatingText.Create(worldPos + offset, text, woodColor, floatingTextCanvas);
                offset += Vector3.up * 0.3f; // Смещаем следующий текст вверх
            }

            if (oreAmount > 0)
            {
                string text = $"+{oreAmount} Ore";
                FloatingText.Create(worldPos + offset, text, oreColor, floatingTextCanvas);
                offset += Vector3.up * 0.3f;
            }

            if (foodAmount > 0)
            {
                string text = $"+{foodAmount} Food";
                FloatingText.Create(worldPos + offset, text, foodColor, floatingTextCanvas);
            }
        }

        /// <summary>
        /// Показать floating text для любого типа ресурса
        /// </summary>
        public void ShowResourceText(Vector3 worldPos, RTSokak.Resources.ResourceType type, int amount)
        {
            switch (type)
            {
                case RTSokak.Resources.ResourceType.Wood:
                    ShowWoodGained(worldPos, amount);
                    break;
                case RTSokak.Resources.ResourceType.Ore:
                    ShowOreGained(worldPos, amount);
                    break;
                case RTSokak.Resources.ResourceType.Food:
                    ShowFoodGained(worldPos, amount);
                    break;
            }
        }

        /// <summary>
        /// Показать произвольное сообщение на экране
        /// </summary>
        public void Show(string text, Vector3 screenPosition, Color color, float duration = 2f)
        {
            if (floatingTextCanvas == null)
            {
                CreateFloatingTextCanvas();
            }

            FloatingText.Create(screenPosition, text, color, floatingTextCanvas, duration);
        }
    }
}
