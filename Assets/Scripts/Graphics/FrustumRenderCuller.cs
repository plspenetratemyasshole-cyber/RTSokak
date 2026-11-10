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

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextRescan)
            {
                nextRescan = now + 1f;
                Rescan();
            }

            if (now < nextRefresh)
                return;
            nextRefresh = now + Mathf.Max(0.05f, refreshInterval);

            if (cam == null)
                cam = UnityEngine.Camera.main;
            if (cam == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(cam, planes);

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                Renderer r = tracked[i];
                if (r == null)
                {
                    tracked.RemoveAt(i);
                    continue;
                }

                int id = r.GetInstanceID();
                Bounds b = r.bounds;
                b.Expand(padding * 2f);
                bool inView = GeometryUtility.TestPlanesAABB(planes, b);

                if (inView)
                {
                    if (culledByUs.Remove(id))
                        r.enabled = true;
                }
                else
                {
                    // Не включаем обратно то, что уже было выключено не нами
                    // (срубленное дерево, скрытый объект).
                    if (r.enabled)
                    {
                        r.enabled = false;
                        culledByUs.Add(id);
                    }
                }
            }
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
