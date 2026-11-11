using System.Collections.Generic;
using UnityEngine;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Явно выключает Renderer вне frustum основной камеры (с padding).
    /// Не трогает UI и объекты, которые уже были выключены вручную (например harvested resource).
    /// </summary>
    [DisallowMultipleComponent]
    public class FrustumRenderCuller : MonoBehaviour
    {
        [SerializeField] float padding = 10f;
        [SerializeField] float refreshInterval = 0.15f;

        readonly List<Renderer> tracked = new List<Renderer>(1024);
        readonly HashSet<int> culledByUs = new HashSet<int>();
        Plane[] planes = new Plane[6];
        float nextRefresh;
        float nextRescan;
        UnityEngine.Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Disabled: scene-wide renderer scanning was slower than Unity's built-in frustum culling
            // for large worlds (hundreds of trees/ore).
        }

        void Awake()
        {
            enabled = false;
        }

        void OnEnable()
        {
            enabled = false;
        }

        void LateUpdate()
        {
            return;
        }

        void Rescan()
        {
            tracked.Clear();
            var found = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];
                if (r == null) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;
                if (r.GetComponentInParent<RTSokak.UI.UnitNameplate>() != null) continue;
                // Камера / sky / volume visuals
                if (r.GetComponentInParent<UnityEngine.Camera>() != null) continue;

                tracked.Add(r);
            }
        }
    }
}
