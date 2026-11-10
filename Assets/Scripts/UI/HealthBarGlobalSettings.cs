using UnityEngine;

namespace RTSokak.UI
{
    /// <summary>
    /// Общие настройки HP bar для всех юнитов и зданий.
    /// Ассет: Resources/UI/HealthBarGlobalSettings
    /// </summary>
    [CreateAssetMenu(fileName = "HealthBarGlobalSettings", menuName = "RTSokak/Health Bar Global Settings")]
    public class HealthBarGlobalSettings : ScriptableObject
    {
        public const string ResourcesPath = "UI/HealthBarGlobalSettings";

        [Header("Size (world meters)")]
        [Tooltip("Длина бара при Reference HP (обычно 100). Дальше length *= maxHP / referenceHp")]
        [SerializeField] private float lengthForReferenceHp = 0.1f;

        [Tooltip("Толщина (высота) полоски — одинаковая для всех")]
        [SerializeField] private float barHeight = 0.015f;

        [Tooltip("HP, при котором длина = Length For Reference Hp")]
        [SerializeField] private float referenceHp = 100f;

        [Header("HP Colors")]
        [SerializeField] private Gradient fillGradient;

        [Header("Construction Progress Bar")]
        [Tooltip("Длина бара при Reference Build Duration. Дальше length *= buildDuration / reference")]
        [SerializeField] private float lengthForReferenceBuildDuration = 0.1f;

        [Tooltip("buildDuration (сек), при котором длина = Length For Reference Build Duration")]
        [SerializeField] private float referenceBuildDuration = 10f;

        [Tooltip("Цвет заполнения полоски стройки (фон всегда чёрный)")]
        [SerializeField] private Color constructionBarColor = new Color(0.25f, 0.85f, 0.35f, 1f);

        private static HealthBarGlobalSettings cached;

        public float LengthForReferenceHp => Mathf.Max(0.001f, lengthForReferenceHp);
        public float BarHeight => Mathf.Max(0.001f, barHeight);
        public float ReferenceHp => Mathf.Max(1f, referenceHp);

        public float LengthForReferenceBuildDuration => Mathf.Max(0.001f, lengthForReferenceBuildDuration);
        public float ReferenceBuildDuration => Mathf.Max(0.01f, referenceBuildDuration);
        public Color ConstructionBarColor => constructionBarColor;

        public Gradient FillGradient
        {
            get
            {
                if (fillGradient == null || fillGradient.colorKeys == null || fillGradient.colorKeys.Length == 0)
                    fillGradient = CreateDefaultGradient();
                return fillGradient;
            }
        }

        public Vector2 SizeAtReferenceHp => new Vector2(LengthForReferenceHp, BarHeight);

        public Vector2 SizeForBuildDuration(float buildDurationSeconds)
        {
            float scale = Mathf.Max(0.01f, buildDurationSeconds) / ReferenceBuildDuration;
            return new Vector2(LengthForReferenceBuildDuration * scale, BarHeight);
        }

        public static HealthBarGlobalSettings Get()
        {
            if (cached != null)
                return cached;

            cached = UnityEngine.Resources.Load<HealthBarGlobalSettings>(ResourcesPath);
            if (cached == null)
            {
                cached = CreateInstance<HealthBarGlobalSettings>();
                cached.name = "HealthBarGlobalSettings (Runtime Default)";
                cached.fillGradient = CreateDefaultGradient();
            }

            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

        public static Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.green, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return g;
        }
    }
}
