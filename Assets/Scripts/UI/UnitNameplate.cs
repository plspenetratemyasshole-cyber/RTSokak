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
        private RTSokak.Network.RTSUnit cachedUnit;
        private UnityEngine.Camera mainCamera;
        private Vector3 lastPos = Vector3.positiveInfinity;

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
            else
            {
                if (cachedUnit == null && targetUnit != null)
                    cachedUnit = targetUnit.GetComponent<RTSokak.Network.RTSUnit>();

                bool isSelected = cachedUnit != null && cachedUnit.IsSelected;
                float distSqr = (targetUnit.position - mainCamera.transform.position).sqrMagnitude;
                bool inRange = distSqr <= 35f * 35f;

                bool show = isSelected || inRange;
                if (canvas != null && canvas.enabled != show)
                    canvas.enabled = show;

                if (!show)
                    return;
            }

            if (canvas != null && canvas.worldCamera != mainCamera)
                canvas.worldCamera = mainCamera;

            Vector3 targetPos = targetUnit.position + offset;
            if ((targetPos - lastPos).sqrMagnitude > 0.0001f)
            {
                transform.position = targetPos;
                lastPos = targetPos;
            }

            Quaternion camRot = mainCamera.transform.rotation;
            if (Quaternion.Angle(transform.rotation, camRot) > 0.1f)
            {
                transform.rotation = camRot;
            }
        }

        /// <summary>
        /// Устанавливает юнит для отслеживания
        /// </summary>
        public void SetTarget(Transform unit)
        {
            targetUnit = unit;
            cachedUnit = unit != null ? unit.GetComponent<RTSokak.Network.RTSUnit>() : null;
            lastPos = Vector3.positiveInfinity;
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
