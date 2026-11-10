using UnityEngine;

namespace RTSokak.UI
{
    /// <summary>
    /// Жёлтая точка-подсказка мэра юниту-игроку. Видна сквозь геометрию, светится, гаснет за 1с.
    /// </summary>
    public class MayorOrderMarker : MonoBehaviour
    {
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float startScale = 1.2f;
        [SerializeField] private Color color = new Color(1f, 0.92f, 0.15f, 1f);

        private float age;
        private Material mat;
        private static MayorOrderMarker active;
        private static AudioSource hintAudio;
        private static AudioClip hintClip;

        public static void Spawn(Vector3 worldPos)
        {
            if (active != null)
                Destroy(active.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MayorOrderMarker";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos + Vector3.up * 0.35f;
            go.transform.localScale = Vector3.one * 1.2f;

            var marker = go.AddComponent<MayorOrderMarker>();
            active = marker;

            var mr = go.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            marker.mat = new Material(shader);
            marker.mat.color = marker.color;
            // Рендер поверх всего
            marker.mat.renderQueue = 4000;
            mr.sharedMaterial = marker.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public static void PlayHintSound()
        {
            if (hintAudio == null)
            {
                GameObject go = new GameObject("MayorHintAudio");
                Object.DontDestroyOnLoad(go);
                hintAudio = go.AddComponent<AudioSource>();
                hintAudio.playOnAwake = false;
                hintAudio.spatialBlend = 0f;
            }

            if (hintClip == null)
                hintClip = BuildBeepClip();

            if (hintClip != null)
                hintAudio.PlayOneShot(hintClip, 0.55f);
        }

        static AudioClip BuildBeepClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.12f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("MayorHintBeep", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = 1f - t / duration;
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.35f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            float fade = 1f - t;
            if (mat != null)
            {
                Color c = color;
                c.a = fade;
                mat.color = c;
            }

            float s = Mathf.Lerp(startScale, startScale * 0.4f, t);
            transform.localScale = Vector3.one * s;

            if (t >= 1f)
            {
                if (active == this) active = null;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (mat != null)
                Destroy(mat);
            if (active == this)
                active = null;
        }
    }
}
