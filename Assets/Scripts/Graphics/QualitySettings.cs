using UnityEngine;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Built-in RP quality helpers: FPS lock, shadows, AA.
    /// </summary>
    public class QualitySettingsManager : MonoBehaviour
    {
        [Header("Shadows")]
        [SerializeField] private float shadowDistance = 60f;
        [SerializeField, Range(0, 4)] private int shadowCascades = 2;

        [Header("Frame Rate")]
        [SerializeField] private int targetFrameRate = 120;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<QualitySettingsManager>() != null)
                return;

            var go = new GameObject("QualitySettingsManager");
            DontDestroyOnLoad(go);
            go.AddComponent<QualitySettingsManager>();
        }

        private void Awake()
        {
            GraphicsSettingsStore.ApplyAll();
        }

        public void ApplyFromStore()
        {
            targetFrameRate = GraphicsSettingsStore.TargetFps;
            shadowDistance = GraphicsSettingsStore.ShadowDistance;
            shadowCascades = GraphicsSettingsStore.CascadeCount;
            ApplyFrameRateLock();
            ApplyQualitySettings();
        }

        private void ApplyFrameRateLock()
        {
            QualitySettings.vSyncCount = GraphicsSettingsStore.VSync ? 1 : 0;
            if (GraphicsSettingsStore.VSync)
                Application.targetFrameRate = -1;
            else
                Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : -1;
        }

        private void ApplyQualitySettings()
        {
            QualitySettings.shadowDistance = Mathf.Max(10f, shadowDistance);
            QualitySettings.shadowCascades = Mathf.Clamp(shadowCascades, 0, 4);
            QualitySettings.shadows = GraphicsSettingsStore.ShadowQuality switch
            {
                0 => ShadowQuality.Disable,
                1 => ShadowQuality.HardOnly,
                _ => ShadowQuality.All
            };

            QualitySettings.antiAliasing = GraphicsSettingsStore.AntiAliasing switch
            {
                0 => 0,
                2 => 4,
                _ => 2
            };

            UnityEngine.Debug.Log(
                $"[QualitySettings] Built-in shadows distance={shadowDistance}, cascades={shadowCascades}, aa={QualitySettings.antiAliasing}");
        }

        public void SetShadowDistance(float distance)
        {
            shadowDistance = distance;
            ApplyQualitySettings();
        }
    }
}
