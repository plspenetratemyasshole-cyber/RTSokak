using UnityEngine;

namespace RTSokak.Environment
{
    /// <summary>
    /// Видимое небо: procedural skybox, солнце, луна, звёзды.
    /// Управляется из DayNightCycle (время из сейва мира сохраняется как раньше).
    /// </summary>
    [DisallowMultipleComponent]
    public class SkyVisuals : MonoBehaviour
    {
        public static SkyVisuals Instance { get; private set; }

        [Header("Celestials")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private float moonIntensity = 0.25f;
        [SerializeField] private Color moonColor = new Color(0.55f, 0.65f, 0.95f, 1f);
        [SerializeField] private float celestialDistance = 260f;
        [SerializeField] private float sunDiscSize = 32f;
        [SerializeField] private float moonDiscSize = 24f;

        [Header("Stars")]
        [SerializeField] private int starCount = 600;
        [SerializeField] private float starRadius = 400f;
        [SerializeField] private float starSize = 1.8f;
        [SerializeField] private Color starColor = new Color(1f, 1f, 0.95f, 1f);

        [Header("Skybox")]
        [SerializeField] private Material skyboxMaterial;

        private ParticleSystem stars;
        private ParticleSystem.MainModule starsMain;
        private GameObject sunVisual;
        private Material sunMat;
        private GameObject moonVisual;
        private Material moonMat;
        private UnityEngine.Camera activeCam;
        private bool built;

        private void Awake()
        {
            Instance = this;
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (sunVisual != null)
                Destroy(sunVisual);
            if (moonVisual != null)
                Destroy(moonVisual);
        }

        public void EnsureBuilt()
        {
            if (built)
                return;
            built = true;

            if (sunLight == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l != null && l.type == LightType.Directional && l != moonLight)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            if (moonLight == null)
            {
                GameObject moonGo = new GameObject("MoonLight");
                moonGo.transform.SetParent(transform, false);
                moonLight = moonGo.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.shadows = LightShadows.None;
                moonLight.color = moonColor;
                moonLight.intensity = 0f;
            }

            if (sunLight != null)
            {
                RenderSettings.sun = sunLight;
            }

            EnsureSkyboxMaterial();
            EnsureStars();
            EnsureCelestials();
        }

        void EnsureSkyboxMaterial()
        {
            if (skyboxMaterial == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                    shader = Shader.Find("Skybox/Cubemap");
                if (shader != null)
                {
                    skyboxMaterial = new Material(shader) { name = "RTSokakProceduralSky" };
                    if (shader.name.Contains("Procedural"))
                    {
                        skyboxMaterial.SetInt("_SunDisk", 2);
                        skyboxMaterial.SetFloat("_SunSize", 0.05f);
                        skyboxMaterial.SetFloat("_SunSizeConvergence", 5f);
                        skyboxMaterial.SetFloat("_AtmosphereThickness", 1.05f);
                        skyboxMaterial.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));
                        skyboxMaterial.SetColor("_GroundColor", new Color(0.25f, 0.22f, 0.18f));
                        skyboxMaterial.SetFloat("_Exposure", 1.15f);
                    }
                }
            }

            if (skyboxMaterial != null)
                RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        void EnsureStars()
        {
            if (stars != null)
                return;

            GameObject go = new GameObject("NightStars");
            go.transform.SetParent(transform, false);
            stars = go.AddComponent<ParticleSystem>();
            starsMain = stars.main;
            starsMain.loop = false;
            starsMain.playOnAwake = false;
            starsMain.startLifetime = Mathf.Infinity;
            starsMain.startSize = starSize;
            starsMain.startColor = starColor;
            starsMain.maxParticles = starCount;
            starsMain.simulationSpace = ParticleSystemSimulationSpace.World;
            starsMain.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = stars.emission;
            emission.enabled = false;
            var shape = stars.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material mat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Unlit/Transparent"));
            if (mat != null)
            {
                mat.SetColor("_Color", starColor);
                renderer.sharedMaterial = mat;
            }

            // Одноразовый разброс по сфере
            var particles = new ParticleSystem.Particle[starCount];
            for (int i = 0; i < starCount; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (dir.y < 0.05f)
                    dir.y = Mathf.Abs(dir.y) + 0.05f;
                dir.Normalize();
                particles[i].position = dir * starRadius;
                particles[i].startSize = Random.Range(starSize * 0.5f, starSize * 1.4f);
                particles[i].startColor = starColor;
                particles[i].remainingLifetime = 99999f;
                particles[i].startLifetime = 99999f;
            }
            stars.SetParticles(particles, starCount);
        }

        void EnsureCelestials()
        {
            if (sunVisual == null)
            {
                sunVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sunVisual.name = "SunVisual";
                sunVisual.layer = 0;
                var col = sunVisual.GetComponent<Collider>();
                if (col != null) Destroy(col);

                sunMat = CreateCelestialMaterial(false);
                var mr = sunVisual.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = sunMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                DontDestroyOnLoad(sunVisual);
            }

            if (moonVisual == null)
            {
                moonVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                moonVisual.name = "MoonVisual";
                moonVisual.layer = 0;
                var col = moonVisual.GetComponent<Collider>();
                if (col != null) Destroy(col);

                moonMat = CreateCelestialMaterial(true);
                var mr = moonVisual.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = moonMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                DontDestroyOnLoad(moonVisual);
            }
        }

        private static Material CreateCelestialMaterial(bool isMoon)
        {
            Shader shader = Shader.Find("Custom/CelestialDisc")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            Material mat = new Material(shader) { name = isMoon ? "MoonDiscMat" : "SunDiscMat" };
            mat.mainTexture = CreateCelestialDiscTexture(isMoon);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 0f);
            return mat;
        }

        private static Texture2D CreateCelestialDiscTexture(bool isMoon)
        {
            int res = 128;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[res * res];
            Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
            float radius = res * 0.46f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float normDist = dist / radius;
                    if (isMoon)
                    {
                        if (normDist <= 0.72f)
                        {
                            float limb = Mathf.SmoothStep(0.72f, 0.65f, normDist);
                            cols[y * res + x] = Color.Lerp(new Color(0.92f, 0.95f, 1f, 1f), Color.white, limb);
                        }
                        else if (normDist <= 1f)
                        {
                            float glow = Mathf.SmoothStep(1f, 0.72f, normDist);
                            cols[y * res + x] = new Color(0.7f, 0.8f, 1f, glow * 0.6f);
                        }
                        else
                        {
                            cols[y * res + x] = Color.clear;
                        }
                    }
                    else
                    {
                        if (normDist <= 0.52f)
                        {
                            cols[y * res + x] = Color.white;
                        }
                        else if (normDist <= 1f)
                        {
                            float t = (normDist - 0.52f) / (1f - 0.52f);
                            float alpha = Mathf.SmoothStep(1f, 0f, t);
                            Color corona = Color.Lerp(new Color(1f, 0.96f, 0.82f, 1f), new Color(1f, 0.68f, 0.18f, 1f), t);
                            corona.a = alpha;
                            cols[y * res + x] = corona;
                        }
                        else
                        {
                            cols[y * res + x] = Color.clear;
                        }
                    }
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false, false);
            return tex;
        }

        private void LateUpdate()
        {
            if (activeCam == null || !activeCam.isActiveAndEnabled)
            {
                activeCam = UnityEngine.Camera.main;
                if (activeCam == null)
                    activeCam = FindFirstObjectByType<UnityEngine.Camera>();
            }

            Vector3 camPos = activeCam != null ? activeCam.transform.position : transform.position;

            if (stars != null)
                stars.transform.position = camPos;

            UpdateCelestialPositions(camPos);
        }

        private void UpdateCelestialPositions(Vector3 camPos)
        {
            if (sunVisual != null && sunLight != null && sunVisual.activeSelf)
            {
                Vector3 sunDir = -sunLight.transform.forward;
                Vector3 sunPos = camPos + sunDir.normalized * celestialDistance;
                sunVisual.transform.position = sunPos;
                Vector3 toCam = camPos - sunPos;
                if (toCam.sqrMagnitude > 0.001f)
                    sunVisual.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                sunVisual.transform.localScale = Vector3.one * sunDiscSize;
            }

            if (moonVisual != null && moonLight != null && moonVisual.activeSelf)
            {
                Vector3 moonDir = -moonLight.transform.forward;
                Vector3 moonPos = camPos + moonDir.normalized * celestialDistance;
                moonVisual.transform.position = moonPos;
                Vector3 toCam = camPos - moonPos;
                if (toCam.sqrMagnitude > 0.001f)
                    moonVisual.transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                moonVisual.transform.localScale = Vector3.one * moonDiscSize;
            }
        }

        /// <summary>t = 0..1 время суток. sunLight уже повёрнут DayNightCycle.</summary>
        public void ApplyTime(float t, Light drivenSun)
        {
            EnsureBuilt();
            if (drivenSun != null)
            {
                sunLight = drivenSun;
                if (RenderSettings.sun != sunLight)
                    RenderSettings.sun = sunLight;
            }

            // Солнце: день ярко, ночь гасим
            float dayFactor = DayFactor(t);
            float nightFactor = 1f - dayFactor;

            if (sunLight != null)
            {
                sunLight.shadows = dayFactor > 0.15f ? LightShadows.Soft : LightShadows.None;
            }

            if (moonLight != null)
            {
                // Луна напротив солнца
                if (sunLight != null)
                    moonLight.transform.rotation = sunLight.transform.rotation * Quaternion.Euler(180f, 0f, 0f);
                else
                {
                    float moonAngle = t * 360f + 90f;
                    moonLight.transform.rotation = Quaternion.Euler(moonAngle, 170f, 0f);
                }
                moonLight.intensity = moonIntensity * nightFactor;
                moonLight.color = moonColor;
                moonLight.enabled = nightFactor > 0.05f;
            }

            if (skyboxMaterial != null && skyboxMaterial.HasProperty("_AtmosphereThickness"))
            {
                float atmo = Mathf.Lerp(0.55f, 1.2f, dayFactor);
                skyboxMaterial.SetFloat("_AtmosphereThickness", atmo);
                skyboxMaterial.SetFloat("_Exposure", Mathf.Lerp(0.55f, 1.2f, dayFactor));
            }

            // Солнце видимо когда находится над горизонтом
            if (sunVisual != null && sunLight != null)
            {
                Vector3 sunDir = -sunLight.transform.forward;
                bool sunAboveHorizon = sunDir.y > -0.04f;
                sunVisual.SetActive(sunAboveHorizon);
                if (sunAboveHorizon && sunMat != null)
                {
                    Color c = Color.white;
                    float horizonFade = Mathf.Clamp01((sunDir.y + 0.04f) / 0.12f);
                    c.a = horizonFade;
                    if (sunMat.HasProperty("_Color"))
                        sunMat.SetColor("_Color", c);
                }
            }

            // Луна видима когда находится над горизонтом
            if (moonVisual != null && moonLight != null)
            {
                Vector3 moonDir = -moonLight.transform.forward;
                bool moonAboveHorizon = moonDir.y > -0.04f;
                moonVisual.SetActive(moonAboveHorizon);
                if (moonAboveHorizon && moonMat != null)
                {
                    Color c = Color.white;
                    float horizonFade = Mathf.Clamp01((moonDir.y + 0.04f) / 0.12f);
                    c.a = horizonFade;
                    if (moonMat.HasProperty("_Color"))
                        moonMat.SetColor("_Color", c);
                }
            }

            if (stars != null)
            {
                var r = stars.GetComponent<ParticleSystemRenderer>();
                if (r != null)
                {
                    Color c = starColor;
                    c.a = Mathf.Clamp01(nightFactor * 1.2f);
                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                        r.sharedMaterial.SetColor("_Color", c);
                    r.enabled = nightFactor > 0.08f;
                }
            }

            // Trilight ambient по времени
            Color sky = Color.Lerp(new Color(0.05f, 0.06f, 0.12f), new Color(0.55f, 0.7f, 0.95f), dayFactor);
            Color equ = Color.Lerp(new Color(0.08f, 0.08f, 0.12f), new Color(0.55f, 0.55f, 0.55f), dayFactor);
            Color grn = Color.Lerp(new Color(0.04f, 0.04f, 0.05f), new Color(0.3f, 0.28f, 0.22f), dayFactor);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equ;
            RenderSettings.ambientGroundColor = grn;
        }

        static float DayFactor(float t)
        {
            // 0=midnight, 0.25=6:00, 0.5=noon, 0.75=18:00
            float hour = t * 24f;
            if (hour >= 7f && hour <= 17f)
                return 1f;
            if (hour > 5f && hour < 7f)
                return Mathf.SmoothStep(0f, 1f, (hour - 5f) / 2f);
            if (hour > 17f && hour < 19.5f)
                return Mathf.SmoothStep(1f, 0f, (hour - 17f) / 2.5f);
            return 0f;
        }
    }
}
