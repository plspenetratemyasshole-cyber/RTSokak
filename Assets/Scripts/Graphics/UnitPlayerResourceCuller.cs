using System.Collections.Generic;
using UnityEngine;
using RTSokak.Resources;
using RTSokak.Buildings;
using RTSokak.Network;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Высокопроизводительный time-sliced FPS culler:
    /// - Полное отсутствие фризов/статтеров: проверка распределена по кадрам (time-slicing).
    /// - Мгновенный cull на frame 0 в InitialScan() (поднимает FPS с 40 до 200+).
    /// - Настраиваемые дистанции отсечения через PlayerPrefs и API (ресурсы, здания, юниты).
    /// - Периодическая мягкая синхронизация вражеского outline по закешированным спискам (0 FindObjectsByType).
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitPlayerResourceCuller : MonoBehaviour
    {
        public static UnitPlayerResourceCuller Instance { get; private set; }

        public const string PrefResourceDist = "UnitPlayerResourceCullDist";
        public const string PrefUnitDist = "UnitPlayerUnitCullDist";
        public const string PrefBuildingDist = "UnitPlayerBuildingCullDist";

        public const float DefaultResourceCullDist = 70f;
        public const float DefaultUnitCullDist = 85f;
        public const float DefaultBuildingCullDist = 120f;

        private const int ResourcesPerFrame = 70;
        private const int BuildingsPerFrame = 20;
        private const int UnitsPerFrame = 25;
        private const int MaxTogglesPerSlice = 12;

        private class TrackedItem
        {
            public Transform transform;
            public Renderer[] renderers;
            public bool isCulled;
        }

        private readonly List<TrackedItem> trackedResources = new List<TrackedItem>(1024);
        private readonly List<TrackedItem> trackedBuildings = new List<TrackedItem>(128);
        private readonly List<TrackedItem> trackedUnits = new List<TrackedItem>(128);
        private readonly List<Renderer> tempBuf = new List<Renderer>(16);

        private RTSUnit playerUnit;
        private UnitPlayerController controller;
        private bool active;

        private float resourceCullDistance = DefaultResourceCullDist;
        private float unitCullDistance = DefaultUnitCullDist;
        private float buildingCullDistance = DefaultBuildingCullDist;

        public float ResourceCullDistance => resourceCullDistance;
        public float UnitCullDistance => unitCullDistance;
        public float BuildingCullDistance => buildingCullDistance;

        private int resSliceIndex;
        private int bldSliceIndex;
        private int unitSliceIndex;
        private float nextDynamicScan;
        private float nextOutlineSync;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            LoadCullDistances();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            RestoreAll();
        }

        public static UnitPlayerResourceCuller Ensure()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("UnitPlayerResourceCuller");
            DontDestroyOnLoad(go);
            return go.AddComponent<UnitPlayerResourceCuller>();
        }

        public void LoadCullDistances()
        {
            resourceCullDistance = PlayerPrefs.GetFloat(PrefResourceDist, DefaultResourceCullDist);
            unitCullDistance = PlayerPrefs.GetFloat(PrefUnitDist, DefaultUnitCullDist);
            buildingCullDistance = PlayerPrefs.GetFloat(PrefBuildingDist, DefaultBuildingCullDist);
        }

        public void SetCullDistances(float resDist, float unitDist, float bldDist, bool save = true)
        {
            resourceCullDistance = Mathf.Max(10f, resDist);
            unitCullDistance = Mathf.Max(10f, unitDist);
            buildingCullDistance = Mathf.Max(10f, bldDist);

            if (controller != null)
            {
                controller.ResourceCullDistance = resourceCullDistance;
                controller.UnitCullDistance = unitCullDistance;
                controller.BuildingCullDistance = buildingCullDistance;
            }

            if (save)
            {
                PlayerPrefs.SetFloat(PrefResourceDist, resourceCullDistance);
                PlayerPrefs.SetFloat(PrefUnitDist, unitCullDistance);
                PlayerPrefs.SetFloat(PrefBuildingDist, buildingCullDistance);
                PlayerPrefs.Save();
            }

            if (active)
                ReevaluateAllDistances();
        }

        public void Activate(RTSUnit unit, UnitPlayerController upc)
        {
            playerUnit = unit;
            controller = upc;
            active = true;
            enabled = true;
            resSliceIndex = 0;
            bldSliceIndex = 0;
            unitSliceIndex = 0;
            nextDynamicScan = Time.unscaledTime + 4f;
            nextOutlineSync = Time.unscaledTime + 1.5f;

            LoadCullDistances();
            if (controller != null)
            {
                controller.ResourceCullDistance = resourceCullDistance;
                controller.UnitCullDistance = unitCullDistance;
                controller.BuildingCullDistance = buildingCullDistance;
            }

            InitialScan();
        }

        public void Activate(Transform followTarget, float fogRadius)
        {
            playerUnit = followTarget != null ? followTarget.GetComponent<RTSUnit>() : null;
            controller = UnitPlayerController.Instance;
            active = true;
            enabled = true;
            resSliceIndex = 0;
            bldSliceIndex = 0;
            unitSliceIndex = 0;
            nextDynamicScan = Time.unscaledTime + 4f;
            nextOutlineSync = Time.unscaledTime + 1.5f;

            LoadCullDistances();
            if (controller != null)
            {
                controller.ResourceCullDistance = resourceCullDistance;
                controller.UnitCullDistance = unitCullDistance;
                controller.BuildingCullDistance = buildingCullDistance;
            }

            InitialScan();
        }

        public void Deactivate()
        {
            active = false;
            playerUnit = null;
            controller = null;
            RestoreAll();
            enabled = false;
        }

        private void Update()
        {
            if (!active)
                return;

            if (controller == null)
                controller = UnitPlayerController.Instance;

            bool cullingEnabled = controller == null || controller.EnableDistanceCulling;
            if (!cullingEnabled)
            {
                RestoreAll();
                return;
            }

            Transform follow = playerUnit != null ? playerUnit.transform : (controller != null && controller.ControlledUnit != null ? controller.ControlledUnit.transform : null);
            if (follow == null)
                return;

            Vector3 origin = follow.position;
            float resSqr = resourceCullDistance > 1f ? resourceCullDistance * resourceCullDistance : float.MaxValue;
            float bldSqr = buildingCullDistance > 1f ? buildingCullDistance * buildingCullDistance : float.MaxValue;
            float unitSqr = unitCullDistance > 1f ? unitCullDistance * unitCullDistance : float.MaxValue;

            // 1. Time-sliced проверка ресурсов (70 за кадр, 0 спайков)
            ProcessSlice(trackedResources, ref resSliceIndex, ResourcesPerFrame, origin, resSqr, MaxTogglesPerSlice);

            // 2. Time-sliced проверка зданий
            ProcessSlice(trackedBuildings, ref bldSliceIndex, BuildingsPerFrame, origin, bldSqr, MaxTogglesPerSlice);

            // 3. Time-sliced проверка юнитов
            ProcessSlice(trackedUnits, ref unitSliceIndex, UnitsPerFrame, origin, unitSqr, MaxTogglesPerSlice);

            float now = Time.unscaledTime;

            // Периодическое мягкое обновление динамических сущностей (только здания и юниты, БЕЗ ресурсов)
            if (now >= nextDynamicScan)
            {
                nextDynamicScan = now + 4f;
                SoftUpdateDynamicEntities();
            }

            // Периодическая синхронизация вражеских контуров по закешированным объектам (0 FindObjectsByType)
            if (now >= nextOutlineSync)
            {
                nextOutlineSync = now + 2.5f;
                SyncOutlinesFromCache();
            }
        }

        private static void ProcessSlice(List<TrackedItem> list, ref int sliceIndex, int countToCheck, Vector3 origin, float maxDistSqr, int maxToggles)
        {
            int total = list.Count;
            if (total == 0) return;

            int checks = Mathf.Min(countToCheck, total);
            int toggles = 0;

            for (int i = 0; i < checks; i++)
            {
                if (sliceIndex >= list.Count)
                    sliceIndex = 0;

                TrackedItem item = list[sliceIndex];
                if (item == null || item.transform == null)
                {
                    list.RemoveAt(sliceIndex);
                    if (list.Count == 0) break;
                    continue;
                }

                sliceIndex++;

                bool shouldCull = (item.transform.position - origin).sqrMagnitude > maxDistSqr;
                if (item.isCulled != shouldCull)
                {
                    item.isCulled = shouldCull;
                    bool visible = !shouldCull;
                    var renderers = item.renderers;
                    for (int j = 0; j < renderers.Length; j++)
                    {
                        Renderer r = renderers[j];
                        if (r != null)
                            r.enabled = visible;
                    }

                    toggles++;
                    if (toggles >= maxToggles)
                        break;
                }
            }
        }

        private void InitialScan()
        {
            Transform follow = playerUnit != null ? playerUnit.transform : (controller != null && controller.ControlledUnit != null ? controller.ControlledUnit.transform : null);
            Vector3 origin = follow != null ? follow.position : Vector3.zero;

            float resSqr = resourceCullDistance > 1f ? resourceCullDistance * resourceCullDistance : float.MaxValue;
            float bldSqr = buildingCullDistance > 1f ? buildingCullDistance * buildingCullDistance : float.MaxValue;
            float unitSqr = unitCullDistance > 1f ? unitCullDistance * unitCullDistance : float.MaxValue;

            // 1. Ресурсы (сканируются единожды при старте FPS режима)
            trackedResources.Clear();
            var foundRes = Object.FindObjectsByType<Resource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < foundRes.Length; i++)
            {
                Resource res = foundRes[i];
                if (res != null && !res.IsHarvested)
                {
                    bool shouldCull = follow != null && (res.transform.position - origin).sqrMagnitude > resSqr;
                    trackedResources.Add(CreateTrackedItem(res.gameObject, shouldCull));
                }
            }

            // 2. Здания
            trackedBuildings.Clear();
            var foundBld = Object.FindObjectsByType<Building>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < foundBld.Length; i++)
            {
                Building b = foundBld[i];
                if (b != null)
                {
                    bool shouldCull = follow != null && (b.transform.position - origin).sqrMagnitude > bldSqr;
                    trackedBuildings.Add(CreateTrackedItem(b.gameObject, shouldCull));
                }
            }

            // 3. Юниты (кроме самого игрока)
            trackedUnits.Clear();
            var foundUnits = Object.FindObjectsByType<RTSUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < foundUnits.Length; i++)
            {
                RTSUnit u = foundUnits[i];
                if (u != null && u.IsAlive)
                {
                    if (u == playerUnit || (controller != null && u == controller.ControlledUnit))
                        continue;
                    bool shouldCull = follow != null && (u.transform.position - origin).sqrMagnitude > unitSqr;
                    trackedUnits.Add(CreateTrackedItem(u.gameObject, shouldCull));
                }
            }
        }

        private void ReevaluateAllDistances()
        {
            Transform follow = playerUnit != null ? playerUnit.transform : (controller != null && controller.ControlledUnit != null ? controller.ControlledUnit.transform : null);
            if (follow == null) return;
            Vector3 origin = follow.position;

            float resSqr = resourceCullDistance > 1f ? resourceCullDistance * resourceCullDistance : float.MaxValue;
            float bldSqr = buildingCullDistance > 1f ? buildingCullDistance * buildingCullDistance : float.MaxValue;
            float unitSqr = unitCullDistance > 1f ? unitCullDistance * unitCullDistance : float.MaxValue;

            ReevaluateList(trackedResources, origin, resSqr);
            ReevaluateList(trackedBuildings, origin, bldSqr);
            ReevaluateList(trackedUnits, origin, unitSqr);
        }

        private static void ReevaluateList(List<TrackedItem> list, Vector3 origin, float maxDistSqr)
        {
            for (int i = 0; i < list.Count; i++)
            {
                TrackedItem item = list[i];
                if (item == null || item.transform == null) continue;
                bool shouldCull = (item.transform.position - origin).sqrMagnitude > maxDistSqr;
                if (item.isCulled != shouldCull)
                {
                    item.isCulled = shouldCull;
                    bool visible = !shouldCull;
                    for (int j = 0; j < item.renderers.Length; j++)
                    {
                        Renderer r = item.renderers[j];
                        if (r != null) r.enabled = visible;
                    }
                }
            }
        }

        private void SoftUpdateDynamicEntities()
        {
            Transform follow = playerUnit != null ? playerUnit.transform : (controller != null && controller.ControlledUnit != null ? controller.ControlledUnit.transform : null);
            Vector3 origin = follow != null ? follow.position : Vector3.zero;
            float bldSqr = buildingCullDistance > 1f ? buildingCullDistance * buildingCullDistance : float.MaxValue;
            float unitSqr = unitCullDistance > 1f ? unitCullDistance * unitCullDistance : float.MaxValue;

            // Мягкое добавление новых зданий (их мало, единицы)
            var foundBld = Object.FindObjectsByType<Building>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < foundBld.Length; i++)
            {
                Building b = foundBld[i];
                if (b == null) continue;
                bool exists = false;
                for (int j = 0; j < trackedBuildings.Count; j++)
                {
                    if (trackedBuildings[j].transform == b.transform)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    bool shouldCull = follow != null && (b.transform.position - origin).sqrMagnitude > bldSqr;
                    trackedBuildings.Add(CreateTrackedItem(b.gameObject, shouldCull));
                }
            }

            // Мягкое добавление новых юнитов (их мало)
            var foundUnits = Object.FindObjectsByType<RTSUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < foundUnits.Length; i++)
            {
                RTSUnit u = foundUnits[i];
                if (u == null || !u.IsAlive) continue;
                if (u == playerUnit || (controller != null && u == controller.ControlledUnit)) continue;

                bool exists = false;
                for (int j = 0; j < trackedUnits.Count; j++)
                {
                    if (trackedUnits[j].transform == u.transform)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    bool shouldCull = follow != null && (u.transform.position - origin).sqrMagnitude > unitSqr;
                    trackedUnits.Add(CreateTrackedItem(u.gameObject, shouldCull));
                }
            }
        }

        private void SyncOutlinesFromCache()
        {
            for (int i = 0; i < trackedBuildings.Count; i++)
            {
                var item = trackedBuildings[i];
                if (item != null && item.transform != null)
                {
                    var b = item.transform.GetComponent<Building>();
                    if (b != null)
                        b.ForceUpdateOutline();
                }
            }

            for (int i = 0; i < trackedUnits.Count; i++)
            {
                var item = trackedUnits[i];
                if (item != null && item.transform != null)
                {
                    var u = item.transform.GetComponent<RTSUnit>();
                    if (u != null)
                        u.ForceUpdateOutline();
                }
            }
        }

        private TrackedItem CreateTrackedItem(GameObject go, bool isCulled = false)
        {
            tempBuf.Clear();
            go.GetComponentsInChildren(true, tempBuf);
            // Исключаем UI canvas
            for (int i = tempBuf.Count - 1; i >= 0; i--)
            {
                if (tempBuf[i] == null || tempBuf[i].GetComponentInParent<Canvas>() != null)
                    tempBuf.RemoveAt(i);
            }

            var renderers = tempBuf.ToArray();
            if (isCulled)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].enabled = false;
                }
            }

            return new TrackedItem
            {
                transform = go.transform,
                renderers = renderers,
                isCulled = isCulled
            };
        }

        private void RestoreAll()
        {
            RestoreList(trackedResources);
            RestoreList(trackedBuildings);
            RestoreList(trackedUnits);
        }

        private static void RestoreList(List<TrackedItem> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                TrackedItem item = list[i];
                if (item != null && item.renderers != null)
                {
                    item.isCulled = false;
                    for (int j = 0; j < item.renderers.Length; j++)
                    {
                        Renderer r = item.renderers[j];
                        if (r != null)
                            r.enabled = true;
                    }
                }
            }
            list.Clear();
        }
    }
}
