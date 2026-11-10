using UnityEngine;

namespace RTSokak.Graphics
{
    /// <summary>Persistent graphics prefs for Built-in Render Pipeline.</summary>
    public static class GraphicsSettingsStore
    {
        const string Prefix = "rtsokak.gfx.";

        public static int TargetFps
        {
            get => PlayerPrefs.GetInt(Prefix + "fps", 120);
            set { PlayerPrefs.SetInt(Prefix + "fps", value); PlayerPrefs.Save(); }
        }

        public static float ShadowDistance
        {
            get => PlayerPrefs.GetFloat(Prefix + "shadowDist", 60f);
            set { PlayerPrefs.SetFloat(Prefix + "shadowDist", value); PlayerPrefs.Save(); }
        }

        public static int CascadeCount
        {
            get => PlayerPrefs.GetInt(Prefix + "cascades", 2);
            set { PlayerPrefs.SetInt(Prefix + "cascades", Mathf.Clamp(value, 0, 4)); PlayerPrefs.Save(); }
        }

        // Kept for MainMenu UI compatibility (no-op / mapped loosely on Built-in).
        public static bool ContactShadows
        {
            get => PlayerPrefs.GetInt(Prefix + "contactShadows", 0) != 0;
            set { PlayerPrefs.SetInt(Prefix + "contactShadows", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool ScreenSpaceReflections
        {
            get => PlayerPrefs.GetInt(Prefix + "ssr", 0) != 0;
            set { PlayerPrefs.SetInt(Prefix + "ssr", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool MotionBlur
        {
            get => PlayerPrefs.GetInt(Prefix + "motionBlur", 0) != 0;
            set { PlayerPrefs.SetInt(Prefix + "motionBlur", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool BloomHighQuality
        {
            get => PlayerPrefs.GetInt(Prefix + "bloomHq", 0) != 0;
            set { PlayerPrefs.SetInt(Prefix + "bloomHq", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SoftAo
        {
            get => PlayerPrefs.GetInt(Prefix + "softAo", 1) != 0;
            set { PlayerPrefs.SetInt(Prefix + "softAo", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int ShadowQuality
        {
            get => PlayerPrefs.GetInt(Prefix + "shadowQuality", 2);
            set { PlayerPrefs.SetInt(Prefix + "shadowQuality", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        public static int AntiAliasing
        {
            get => PlayerPrefs.GetInt(Prefix + "aa", 1);
            set { PlayerPrefs.SetInt(Prefix + "aa", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
        }

        public static float RenderScale
        {
            get => PlayerPrefs.GetFloat(Prefix + "renderScale", 1f);
            set { PlayerPrefs.SetFloat(Prefix + "renderScale", Mathf.Clamp(value, 0.5f, 1.5f)); PlayerPrefs.Save(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(Prefix + "vsync", 0) != 0;
            set { PlayerPrefs.SetInt(Prefix + "vsync", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ApplyAll()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : (TargetFps > 0 ? TargetFps : -1);

            var manager = Object.FindFirstObjectByType<QualitySettingsManager>();
            if (manager != null)
                manager.ApplyFromStore();
            else
            {
                QualitySettings.shadowDistance = ShadowDistance;
                QualitySettings.shadowCascades = CascadeCount;
                QualitySettings.antiAliasing = AntiAliasing switch
                {
                    0 => 0,
                    2 => 4,
                    _ => 2
                };
            }
        }
    }
}
