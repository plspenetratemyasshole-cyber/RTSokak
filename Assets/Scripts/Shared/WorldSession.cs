using System.Collections.Generic;

namespace RTSokak.Shared
{
    public static class WorldSession
    {
        public static string ActiveWorldName { get; private set; }
        public static bool IsCreatingNewWorld { get; private set; }
        public static RTSWorldSaveData PendingLoad { get; private set; }
        public static bool WorldGeometryRestored { get; set; }

        /// <summary>
        /// Полный сейв мира, доступный всю сессию (для позднего входа клиентов).
        /// </summary>
        public static RTSWorldSaveData ActiveSaveData { get; private set; }

        static readonly HashSet<string> restoredNicknames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public static bool HasActiveWorld => !string.IsNullOrWhiteSpace(ActiveWorldName);

        public static bool ShouldRestoreFromSave =>
            ActiveSaveData != null && HasSaveContent(ActiveSaveData);

        public static void SelectExistingWorld(string worldName)
        {
            ActiveWorldName = WorldSaveService.SanitizeWorldName(worldName);
            IsCreatingNewWorld = false;
            WorldGeometryRestored = false;
            PendingLoad = null;
            ActiveSaveData = null;
            restoredNicknames.Clear();

            if (WorldSaveService.TryLoadWorld(ActiveWorldName, out RTSWorldSaveData data, out string error))
            {
                PendingLoad = data;
                ActiveSaveData = data;
                UnityEngine.Debug.Log(
                    $"[WorldSession] Loaded '{ActiveWorldName}': " +
                    $"res={data.resources?.Count ?? 0}, units={data.units?.Count ?? 0}, " +
                    $"buildings={data.buildings?.Count ?? 0}, players={data.players?.Count ?? 0}, " +
                    $"seed={data.worldSeed}, restore={ShouldRestoreFromSave}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[WorldSession] Failed to load '{ActiveWorldName}': {error}");
            }
        }

        public static void BeginCreateWorld(string worldName)
        {
            ActiveWorldName = WorldSaveService.SanitizeWorldName(worldName);
            IsCreatingNewWorld = true;
            WorldGeometryRestored = false;
            PendingLoad = null;
            ActiveSaveData = null;
            restoredNicknames.Clear();
            UnityEngine.Debug.Log($"[WorldSession] Creating NEW world: '{ActiveWorldName}'");
        }

        public static void MarkNicknameRestored(string nick)
        {
            if (!string.IsNullOrWhiteSpace(nick))
                restoredNicknames.Add(nick.Trim());
        }

        public static bool IsNicknameRestored(string nick) =>
            !string.IsNullOrWhiteSpace(nick) && restoredNicknames.Contains(nick.Trim());

        /// <summary>
        /// Обновить ActiveSaveData после сохранения мира.
        /// Это позволяет подключающимся клиентам получать актуальные данные.
        /// </summary>
        public static void UpdateActiveSaveData(RTSWorldSaveData data)
        {
            ActiveSaveData = data;
        }

        // Legacy stubs (playerIndex) — больше не используем как источник истины
        public static void MarkPlayerRestored(int playerIndex) { }

        public static bool IsPlayerRestored(int playerIndex) => false;

        public static void ClearPendingLoad()
        {
            PendingLoad = null;
        }

        public static void Clear()
        {
            ActiveWorldName = null;
            IsCreatingNewWorld = false;
            PendingLoad = null;
            ActiveSaveData = null;
            WorldGeometryRestored = false;
            restoredNicknames.Clear();
        }

        static bool HasSaveContent(RTSWorldSaveData data)
        {
            if (data == null)
                return false;
            return (data.resources != null && data.resources.Count > 0) ||
                   (data.buildings != null && data.buildings.Count > 0) ||
                   (data.units != null && data.units.Count > 0);
        }
    }
}
