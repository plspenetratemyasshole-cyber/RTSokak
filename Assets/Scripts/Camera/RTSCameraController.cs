using UnityEngine;
using RTSokak.Network;

namespace RTSokak.Camera
{
    /// <summary>
    /// Контроллер RTS камеры с ортографическим видом
    /// ПКМ - перемещение камеры
    /// Колесико мыши - зум
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float panSpeedHorizontal = 2f;
        [SerializeField] private float panSpeedVertical = 2f;
        [SerializeField] private float panBorderThickness = 10f;
        [SerializeField] private bool enableEdgeScrolling = false;

        [Header("Zoom Settings")]
        [SerializeField] private float scrollSpeed = 5f;
        [SerializeField] private float minOrthographicSize = 5f;
        [SerializeField] private float maxOrthographicSize = 50f;

        [Header("Movement Limits")]
        [SerializeField] private bool enableMovementLimits = true;
        [SerializeField] private float minX = -500f;
        [SerializeField] private float maxX = 500f;
        [SerializeField] private float minZ = -500f;
        [SerializeField] private float maxZ = 500f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 200f;
        [SerializeField] private float rotationStep = 90f;

        [Header("World Save Sync")]
        [SerializeField] private float reportInterval = 2f;

        private UnityEngine.Camera cam;
        private Vector3 lastMousePosition;
        private bool isPanning = false;

        // Вращение камеры
        private float targetYRotation;
        private bool isRotating = false;
        private float nextReportTime;
        private bool appliedWorldSaveView;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            
            // Устанавливаем ортографический режим
            cam.orthographic = true;
            
            // Инициализировать целевое вращение по текущему
            targetYRotation = transform.eulerAngles.y;
        }

        private void Start()
        {
            // Локальный fallback (PlayerPrefs). Мировой сейв поверх через TargetRpc.
            if (!appliedWorldSaveView &&
                RTSokak.Shared.RTSPlayerSession.TryLoadCameraPosition(out Vector3 savedPosition, out float savedZoom))
            {
                transform.position = savedPosition;
                cam.orthographicSize = savedZoom;
                UnityEngine.Debug.Log($"[RTSCamera] Restored local PlayerPrefs camera: {savedPosition}, zoom: {savedZoom}");
            }
        }

        private void OnDisable()
        {
            PersistLocalAndReportToServer();
        }

        private void OnApplicationQuit()
        {
            PersistLocalAndReportToServer();
        }

        private void Update()
        {
            HandlePanning();
            HandleZoom();
            HandleRotation();

            if (Time.unscaledTime >= nextReportTime)
            {
                nextReportTime = Time.unscaledTime + Mathf.Max(0.5f, reportInterval);
                ReportCameraToServer();
            }
        }

        public void GetView(out Vector3 position, out float zoom, out float yaw)
        {
            position = transform.position;
            zoom = cam != null ? cam.orthographicSize : 20f;
            yaw = targetYRotation;
        }

        /// <summary>Восстановление из сейва мира (хост и клиент).</summary>
        public void ApplySavedView(Vector3 position, float zoom, float yaw)
        {
            appliedWorldSaveView = true;
            Vector3 pos = position;
            if (enableMovementLimits)
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            }

            transform.position = pos;
            if (cam != null)
                cam.orthographicSize = Mathf.Clamp(zoom, minOrthographicSize, maxOrthographicSize);

            targetYRotation = yaw;
            Vector3 e = transform.eulerAngles;
            e.y = yaw;
            transform.eulerAngles = e;
            isRotating = false;

            RTSokak.Shared.RTSPlayerSession.SaveCameraPosition(transform.position, cam != null ? cam.orthographicSize : zoom);
            UnityEngine.Debug.Log($"[RTSCamera] Applied world-save camera: {pos}, zoom={zoom}, yaw={yaw}");
        }

        private void PersistLocalAndReportToServer()
        {
            if (cam == null)
                cam = GetComponent<UnityEngine.Camera>();
            if (cam == null)
                return;

            RTSokak.Shared.RTSPlayerSession.SaveCameraPosition(transform.position, cam.orthographicSize);
            ReportCameraToServer();
        }

        private void ReportCameraToServer()
        {
            if (RTSPlayer.localPlayer == null || !RTSPlayer.localPlayer.isLocalPlayer)
                return;
            if (cam == null)
                return;

            RTSPlayer.localPlayer.CmdReportCameraView(
                transform.position,
                cam.orthographicSize,
                targetYRotation);
        }

        /// <summary>
        /// Обработка перемещения камеры (ПКМ)
        /// </summary>
        private void HandlePanning()
        {
            Vector3 position = transform.position;

            // Начало панорамирования на ПКМ
            if (Input.GetMouseButtonDown(1))
            {
                isPanning = true;
                lastMousePosition = Input.mousePosition;
            }

            // Панорамирование при зажатой ПКМ
            if (Input.GetMouseButton(1) && isPanning)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                
                // Масштабируем движение в зависимости от размера orthographic камеры
                float scaleFactor = cam.orthographicSize / Screen.height;
                
                // Учитываем поворот камеры - используем локальные оси
                Vector3 right = transform.right;   // Локальная ось X камеры
                Vector3 forward = transform.forward; // Локальная ось Z камеры
                
                // ИСПРАВЛЕНИЕ: Убираем компоненту Y чтобы камера не двигалась по высоте
                // Проецируем оси на горизонтальную плоскость
                Vector3 horizontalRight = new Vector3(right.x, 0, right.z).normalized;
                Vector3 horizontalUp = new Vector3(-forward.x, 0, -forward.z).normalized;
                
                // Тащим мир курсором без вертикального смещения
                // ИНВЕРТИРУЕМ delta.y чтобы движение было естественным (вверх = мышь вверх)
                position += horizontalRight * delta.x * scaleFactor * panSpeedHorizontal;
                position += horizontalUp * (-delta.y) * scaleFactor * panSpeedVertical;

                lastMousePosition = Input.mousePosition;
            }

            // Конец панорамирования
            if (Input.GetMouseButtonUp(1))
            {
                isPanning = false;
            }

            // Edge scrolling (опционально, включается через enableEdgeScrolling)
            if (enableEdgeScrolling)
            {
                float edgeSpeedH = panSpeedHorizontal * 0.5f; // Немного медленнее для edge scrolling
                float edgeSpeedV = panSpeedVertical * 0.5f;
                
                if (Input.mousePosition.y >= Screen.height - panBorderThickness)
                {
                    position.z += edgeSpeedV * Time.deltaTime;
                }
                if (Input.mousePosition.y <= panBorderThickness)
                {
                    position.z -= edgeSpeedV * Time.deltaTime;
                }
                if (Input.mousePosition.x >= Screen.width - panBorderThickness)
                {
                    position.x += edgeSpeedH * Time.deltaTime;
                }
                if (Input.mousePosition.x <= panBorderThickness)
                {
                    position.x -= edgeSpeedH * Time.deltaTime;
                }
            }

            // Применяем ограничения движения
            if (enableMovementLimits)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
                position.z = Mathf.Clamp(position.z, minZ, maxZ);
            }

            transform.position = position;
        }

        /// <summary>
        /// Обработка поворота камеры по Y (стрелки влево/вправо — шаг 90°)
        /// Камера вращается вокруг точки в центре экрана
        /// </summary>
        private void HandleRotation()
        {
            // Запускаем поворот только по одному нажатию (не держим)
            if (!isRotating)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    targetYRotation += rotationStep;
                    isRotating = true;
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    targetYRotation -= rotationStep;
                    isRotating = true;
                }
            }

            if (isRotating)
            {
                float currentY = transform.eulerAngles.y;
                float newY = Mathf.MoveTowardsAngle(currentY, targetYRotation, rotationSpeed * Time.deltaTime);
                
                // Вычислить точку в центре экрана (на поверхности земли Y=0)
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
                Vector3 pivotPoint;
                
                // Пересечение луча с плоскостью Y=0
                if (ray.direction.y != 0)
                {
                    float distance = -ray.origin.y / ray.direction.y;
                    pivotPoint = ray.origin + ray.direction * distance;
                }
                else
                {
                    // Если луч параллелен земле, используем проекцию позиции камеры
                    pivotPoint = new Vector3(transform.position.x, 0, transform.position.z);
                }
                
                // Вычислить вектор от pivot к камере
                Vector3 offset = transform.position - pivotPoint;
                
                // Повернуть offset вокруг Y оси
                float deltaAngle = Mathf.DeltaAngle(currentY, newY);
                Quaternion rotation = Quaternion.Euler(0, deltaAngle, 0);
                Vector3 newOffset = rotation * offset;
                
                // Применить новую позицию и поворот
                transform.position = pivotPoint + newOffset;
                transform.eulerAngles = new Vector3(transform.eulerAngles.x, newY, transform.eulerAngles.z);

                // Достигли цели
                if (Mathf.Abs(Mathf.DeltaAngle(newY, targetYRotation)) < 0.1f)
                {
                    transform.eulerAngles = new Vector3(transform.eulerAngles.x, targetYRotation, transform.eulerAngles.z);
                    isRotating = false;
                }
            }
        }

        /// <summary>
        /// Обработка зума камеры (колесико мыши)
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (scroll != 0f)
            {
                float newSize = cam.orthographicSize - scroll * scrollSpeed;
                cam.orthographicSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
            }
        }

        /// <summary>
        /// Устанавливает позицию камеры программно
        /// </summary>
        public void SetCameraPosition(Vector3 position)
        {
            Vector3 newPos = position;
            newPos.y = transform.position.y;
            transform.position = newPos;
        }

        /// <summary>
        /// Фокусирует камеру на точке
        /// </summary>
        public void FocusOnPoint(Vector3 point)
        {
            Vector3 newPos = point;
            newPos.y = transform.position.y;
            
            if (enableMovementLimits)
            {
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);
            }
            
            transform.position = newPos;
        }

        /// <summary>
        /// Сдвигает камеру так, чтобы центр экрана смотрел на groundPoint (Y игнорируется).
        /// Учитывает наклон/поворот — для клика по миникарте.
        /// </summary>
        public void FocusLookAtGroundPoint(Vector3 groundPoint)
        {
            if (cam == null)
                cam = GetComponent<UnityEngine.Camera>();

            groundPoint.y = 0f;

            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 currentLookAt;
            if (Mathf.Abs(centerRay.direction.y) > 0.0001f)
            {
                float t = -centerRay.origin.y / centerRay.direction.y;
                currentLookAt = centerRay.origin + centerRay.direction * t;
            }
            else
            {
                currentLookAt = new Vector3(transform.position.x, 0f, transform.position.z);
            }

            Vector3 offset = transform.position - currentLookAt;
            Vector3 newPos = groundPoint + offset;

            if (enableMovementLimits)
            {
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);
            }

            transform.position = newPos;
        }
    }
}
