using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RTSokak.UI
{
    /// <summary>
    /// Оверлей FPS и RTT (пинг). Не ограничивает частоту кадров.
    /// </summary>
    public class NetworkPerfHUD : MonoBehaviour
    {
        [SerializeField] private float refreshInterval = 0.35f;
        [SerializeField] private int fontSize = 18;
        [SerializeField] private Vector2 anchoredPos = new Vector2(-16f, -16f);

        private TextMeshProUGUI label;
        private float nextRefresh;
        private float fpsSmooth;
        private int lastFps;
        private int lastPingMs = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<NetworkPerfHUD>() != null)
                return;

            var go = new GameObject("NetworkPerfHUD");
            DontDestroyOnLoad(go);
            go.AddComponent<NetworkPerfHUD>();
        }

        private void Awake()
        {
            EnsureUi();
        }

        private void EnsureUi()
        {
            if (label != null)
                return;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("PerfLabel");
            textGo.transform.SetParent(transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(320f, 88f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.TopRight;
            label.color = new Color(0.95f, 0.95f, 0.95f, 0.9f);
            label.raycastTarget = false;
            label.text = "FPS: --\nPing: --";
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
            {
                float inst = 1f / dt;
                fpsSmooth = fpsSmooth <= 1f ? inst : Mathf.Lerp(fpsSmooth, inst, 0.12f);
            }

            if (Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
            lastFps = Mathf.RoundToInt(fpsSmooth);

            if (NetworkClient.active && NetworkClient.isConnected)
                lastPingMs = Mathf.RoundToInt((float)(NetworkTime.rtt * 1000.0));
            else if (NetworkServer.active && NetworkServer.localConnection != null)
                lastPingMs = 0; // хост локально
            else
                lastPingMs = -1;

            if (label == null)
                EnsureUi();

            string pingText = lastPingMs < 0 ? "--" : $"{lastPingMs} ms";
            string warn = string.Empty;
            if (NetworkServer.active && Application.isEditor && !Application.isFocused)
                warn = "\nEDITOR THROTTLED";
            else if (NetworkServer.active && Application.isEditor)
                warn = "\nHost in Editor: Alt+Tab throttles";

            label.text = $"FPS: {lastFps}\nPing: {pingText}{warn}";
        }
    }
}
