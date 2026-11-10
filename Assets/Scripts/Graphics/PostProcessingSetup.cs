using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Optimized post-processing profile setup for RTS game.
    /// Automatically configures Post-Processing Stack v2 with performance-friendly settings.
    /// 
    /// USAGE:
    /// 1. Add PostProcessVolume to scene
    /// 2. Create new PostProcessProfile asset
    /// 3. Add this script to any GameObject
    /// 4. Assign profile reference in Inspector
    /// 5. Click "Apply RTS Optimized Settings" button in Inspector
    /// 
    /// Or use Tools → RTS Setup → Setup Post Processing
    /// </summary>
    public class PostProcessingSetup : MonoBehaviour
    {
        [Header("Profile Reference")]
        [SerializeField] private PostProcessProfile profile;

        [Header("Quality Presets")]
        [SerializeField] private QualityPreset preset = QualityPreset.Balanced;

        public enum QualityPreset
        {
            Performance,  // Lowest impact, ~1-2% GPU
            Balanced,     // Recommended, ~3-5% GPU
            Quality       // Best visuals, ~7-10% GPU
        }

        /// <summary>
        /// Apply optimized post-processing settings based on selected preset
        /// </summary>
        public void ApplyRTSOptimizedSettings()
        {
            if (profile == null)
            {
                UnityEngine.Debug.LogError("[PostProcessingSetup] Profile is null! Assign a PostProcessProfile in Inspector.");
                return;
            }

            // Remove all existing settings
            profile.settings.Clear();

            // Apply settings based on preset
            switch (preset)
            {
                case QualityPreset.Performance:
                    ApplyPerformancePreset();
                    break;
                case QualityPreset.Balanced:
                    ApplyBalancedPreset();
                    break;
                case QualityPreset.Quality:
                    ApplyQualityPreset();
                    break;
            }

            UnityEngine.Debug.Log($"[PostProcessingSetup] Applied {preset} preset to profile!");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(profile);
#endif
        }

        private void ApplyPerformancePreset()
        {
            // Ambient Occlusion - Light
            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.Override(true);
            ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.intensity.Override(0.25f);
            ao.thicknessModifier.Override(1.0f);
            ao.color.Override(Color.black);

            // Color Grading - Essential (0% GPU cost)
            var colorGrading = profile.AddSettings<ColorGrading>();
            colorGrading.enabled.Override(true);
            colorGrading.tonemapper.Override(Tonemapper.ACES);
            colorGrading.temperature.Override(5f);  // Warm tone
            colorGrading.saturation.Override(10f);  // Slight saturation boost
            colorGrading.contrast.Override(5f);     // Slight contrast

            // Vignette - Free
            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.mode.Override(VignetteMode.Classic);
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.4f);

            // Anti-aliasing via FXAA (cheapest)
            var aa = profile.AddSettings<AutoExposure>();
            aa.enabled.Override(false); // Disable for performance
        }

        private void ApplyBalancedPreset()
        {
            // Ambient Occlusion - Medium
            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.Override(true);
            ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.intensity.Override(0.4f);
            ao.thicknessModifier.Override(1.5f);
            ao.color.Override(new Color(0f, 0f, 0f, 1f));

            // Bloom - Light
            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(1.1f);
            bloom.softKnee.Override(0.5f);
            bloom.clamp.Override(65472f);
            bloom.diffusion.Override(7f);
            bloom.fastMode.Override(true); // Performance optimization

            // Color Grading - Enhanced
            var colorGrading = profile.AddSettings<ColorGrading>();
            colorGrading.enabled.Override(true);
            colorGrading.tonemapper.Override(Tonemapper.ACES);
            colorGrading.temperature.Override(8f);
            colorGrading.tint.Override(0f);
            colorGrading.saturation.Override(15f);
            colorGrading.contrast.Override(8f);
            colorGrading.hueShift.Override(0f);

            // Vignette
            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.mode.Override(VignetteMode.Classic);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            vignette.roundness.Override(1f);

            // Chromatic Aberration - Very subtle
            var ca = profile.AddSettings<ChromaticAberration>();
            ca.enabled.Override(true);
            ca.intensity.Override(0.1f);
            ca.fastMode.Override(true);

            // Grain - Very subtle film grain
            var grain = profile.AddSettings<Grain>();
            grain.enabled.Override(true);
            grain.intensity.Override(0.15f);
            grain.size.Override(1f);
            grain.lumContrib.Override(0.8f);
        }

        private void ApplyQualityPreset()
        {
            // Ambient Occlusion - High
            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.Override(true);
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
            ao.intensity.Override(0.5f);
            ao.thicknessModifier.Override(2f);
            ao.color.Override(new Color(0f, 0f, 0f, 1f));

            // Bloom - Medium
            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.0f);
            bloom.softKnee.Override(0.6f);
            bloom.clamp.Override(65472f);
            bloom.diffusion.Override(7f);
            bloom.anamorphicRatio.Override(0f);
            bloom.fastMode.Override(false); // Higher quality

            // Color Grading - Full
            var colorGrading = profile.AddSettings<ColorGrading>();
            colorGrading.enabled.Override(true);
            colorGrading.tonemapper.Override(Tonemapper.ACES);
            colorGrading.temperature.Override(10f);
            colorGrading.tint.Override(-5f);
            colorGrading.saturation.Override(20f);
            colorGrading.contrast.Override(10f);
            colorGrading.hueShift.Override(0f);
            
            // Lift Gamma Gain for fine-tuning
            colorGrading.lift.Override(new Vector4(1f, 1f, 1f, 0f));
            colorGrading.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
            colorGrading.gain.Override(new Vector4(1f, 1f, 1f, 0f));

            // Vignette
            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.mode.Override(VignetteMode.Classic);
            vignette.intensity.Override(0.35f);
            vignette.smoothness.Override(0.6f);
            vignette.roundness.Override(1f);

            // Chromatic Aberration - Subtle
            var ca = profile.AddSettings<ChromaticAberration>();
            ca.enabled.Override(true);
            ca.intensity.Override(0.2f);
            ca.fastMode.Override(false);

            // Grain - Film grain
            var grain = profile.AddSettings<Grain>();
            grain.enabled.Override(true);
            grain.intensity.Override(0.2f);
            grain.size.Override(1.2f);
            grain.lumContrib.Override(0.8f);

            // Screen Space Reflections - Light (expensive!)
            var ssr = profile.AddSettings<ScreenSpaceReflections>();
            ssr.enabled.Override(true);
            ssr.preset.Override(ScreenSpaceReflectionPreset.Medium);
            ssr.maximumIterationCount.Override(32);
            ssr.thickness.Override(4f);
            ssr.maximumMarchDistance.Override(100f);
            ssr.distanceFade.Override(0.5f);
            ssr.vignette.Override(0.5f);
        }
    }
}
