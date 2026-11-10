using UnityEngine;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Экранный depth-fog для FPS юнита-игрока.
    /// Работает на ЛЮБЫХ материалах (в отличие от RenderSettings.fog, который
    /// многие кастомные/Unlit шейдеры игнорируют).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class UnitPlayerScreenFog : MonoBehaviour
    {
        static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        static readonly int FogStartId = Shader.PropertyToID("_FogStart");
        static readonly int FogEndId = Shader.PropertyToID("_FogEnd");
        static readonly int FogIntensityId = Shader.PropertyToID("_FogIntensity");

        Material mat;
        UnityEngine.Camera cam;

        public Color FogColor { get; set; } = new Color(0.55f, 0.62f, 0.68f, 1f);
        public float FogStart { get; set; } = 10f;
        public float FogEnd { get; set; } = 48f;
        public float FogIntensity { get; set; } = 1f;

        void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
            Shader shader = Shader.Find("Hidden/RTSokak/UnitPlayerScreenFog");
            if (shader == null)
            {
                UnityEngine.Debug.LogError("[UnitPlayerScreenFog] Missing shader Hidden/RTSokak/UnitPlayerScreenFog");
                enabled = false;
                return;
            }
            mat = new Material(shader);
        }

        void OnEnable()
        {
            if (cam != null)
                cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnDisable()
        {
            // depth mode могут использовать другие эффекты — не снимаем флаг агрессивно
        }

        void OnDestroy()
        {
            if (mat != null)
                Destroy(mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (mat == null || FogEnd <= FogStart + 0.01f || FogIntensity <= 0.01f)
            {
                UnityEngine.Graphics.Blit(src, dest);
                return;
            }

            if (cam != null)
                cam.depthTextureMode |= DepthTextureMode.Depth;

            mat.SetColor(FogColorId, FogColor);
            mat.SetFloat(FogStartId, FogStart);
            mat.SetFloat(FogEndId, FogEnd);
            mat.SetFloat(FogIntensityId, FogIntensity);
            UnityEngine.Graphics.Blit(src, dest, mat);
        }
    }
}
