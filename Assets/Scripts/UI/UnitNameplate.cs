using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RTSokak.UI
{
    /// <summary>
    /// WorldSpace UI nameplate показывающая профессию юнита над головой
    /// </summary>
    public class UnitNameplate : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI professionText;
        [SerializeField] private Canvas canvas;
        
        [Header("Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);
        [SerializeField] private float textSize = 0.02f; // Значительно уменьшен (измени в инспекторе для тонкой настройки)
        [SerializeField] private float fontSize = 18f; // Размер шрифта (измени в инспекторе)

        private Transform targetUnit;
        private UnityEngine.Camera mainCamera;

        private void Awake()
        {
            mainCamera = UnityEngine.Camera.main;
            
            // Настраиваем Canvas
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }
            
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
                
                // Настраиваем размер
                RectTransform rectTransform = canvas.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(200, 50);
                rectTransform.localScale = Vector3.one * textSize;
            }

            // Настраиваем размер шрифта
            if (professionText != null)
            {
                professionText.fontSize = fontSize;
            }
        }

        private void LateUpdate()
        {
            if (targetUnit == null)
            {
                Destroy(gameObject);
                return;
            }

            mainCamera = UnitHealthBar.ResolveBillboardCamera(mainCamera);
            if (mainCamera == null)
                return;

            var upc = RTSokak.Network.UnitPlayerController.Instance;
            if (upc != null && upc.IsActive && upc.ControlledUnit != null && targetUnit == upc.ControlledUnit.transform)
            {
                if (canvas != null && canvas.enabled)
                    canvas.enabled = false;
                return;
            }

            if (upc != null && upc.IsActive)
            {
                bool culled = upc.ShouldCullHealthBar(targetUnit.position, false);
                if (canvas != null && canvas.enabled == culled)
                    canvas.enabled = !culled;
                if (culled)
                    return;
            }
            else if (canvas != null && !canvas.enabled)
            {
                canvas.enabled = true;
            }

            if (canvas != null && canvas.worldCamera != mainCamera)
                canvas.worldCamera = mainCamera;

            transform.position = targetUnit.position + offset;
            transform.rotation = mainCamera.transform.rotation;
        }

        /// <summary>
        /// Устанавливает юнит для отслеживания
        /// </summary>
        public void SetTarget(Transform unit)
        {
            targetUnit = unit;
        }

        /// <summary>
        /// Обновляет текст профессии
        /// </summary>
        public void UpdateProfession(string professionName)
        {
            if (professionText != null)
            {
                professionText.text = professionName;
            }
        }

        /// <summary>
        /// Устанавливает цвет текста
        /// </summary>
        public void SetTextColor(Color color)
        {
            if (professionText != null)
            {
                professionText.color = color;
            }
        }
    }
}
