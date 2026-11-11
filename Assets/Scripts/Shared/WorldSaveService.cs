using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RTSokak.Shared
{
    public static class WorldSaveService
    {
        public const string LevelFileName = "world.json";
        const int MaxWorldNameLength = 48;

        public static string LevelsDirectory =>
            Path.Combine(Application.persistentDataPath, "Worlds");

        public static IReadOnlyList<string> ListWorlds()
        {
            string root = LevelsDirectory;
            if (!Directory.Exists(root))
                return Array.Empty<string>();

            string[] directories = Directory.GetDirectories(root);
            var worlds = new List<string>();
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (File.Exists(GetWorldLevelPath(name)))
                    worlds.Add(name);
            }

            // Сортировка по дате последнего изменения (самые свежие сверху)
            worlds.Sort((a, b) =>
            {
                try
                {
                    string pathA = GetWorldLevelPath(a);
                    string pathB = GetWorldLevelPath(b);
                    
                    DateTime timeA = File.GetLastWriteTime(pathA);
                    DateTime timeB = File.GetLastWriteTime(pathB);
                    
                    // Сортируем от новых к старым (обратный порядок)
                    return timeB.CompareTo(timeA);
                }
                catch
                {
                    // Если ошибка -- сортируем алфавитно как fallback
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                }
            });
            
            return worlds;
        }

        public static string SanitizeWorldName(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
                return string.Empty;
            return worldName.Trim();
        }

        public static string GetWorldDirectory(string worldName) =>
            Path.Combine(LevelsDirectory, SanitizeWorldName(worldName));

        public static string GetWorldLevelPath(string worldName) =>
            Path.Combine(GetWorldDirectory(worldName), LevelFileName);

        public static bool TryValidateWorldName(string worldName, out string sanitized, out string error)
        {
            sanitized = SanitizeWorldName(worldName);
            error = null;
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                error = "World name cannot be empty.";
                return false;
            }

            if (sanitized.Length > MaxWorldNameLength)
            {
                error = $"World name must be {MaxWorldNameLength} characters or fewer.";
                return false;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            if (sanitized.Any(c => invalid.Contains(c)))
            {
                error = "World name contains invalid characters.";
                return false;
            }

            return true;
        }

        public static bool WorldExists(string worldName) =>
            Directory.Exists(GetWorldDirectory(worldName)) && File.Exists(GetWorldLevelPath(worldName));

        public static bool TryCreateWorld(string worldName, out string error)
        {
            if (!TryValidateWorldName(worldName, out string sanitized, out error))
                return false;

            string directory = GetWorldDirectory(sanitized);
            if (Directory.Exists(directory))
            {
                error = "A world with this name already exists.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                var empty = new RTSWorldSaveData { worldSeed = 0 };
                File.WriteAllText(GetWorldLevelPath(sanitized), ToJson(empty));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryDeleteWorld(string worldName, out string error)
        {
            error = null;
            string directory = GetWorldDirectory(worldName);
            if (!Directory.Exists(directory))
            {
                error = "World not found.";
                return false;
            }

            try
            {
                Directory.Delete(directory, true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryLoadWorld(string worldName, out RTSWorldSaveData data, out string error)
        {
            data = null;
            error = null;
            string path = GetWorldLevelPath(worldName);
            if (!File.Exists(path))
            {
                error = "Save file not found.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                data = FromJson(json);
                if (data == null)
                {
                    error = "Invalid save JSON.";
                    return false;
                }

                UnityEngine.Debug.Log($"[WorldSave] Loaded world from: {path}");
                if (data.players != null && data.players.Count > 0)
                {
                    var p = data.players[0];
                    UnityEngine.Debug.Log($"[WorldSave] Player 0 data: wood={p.wood}, ore={p.ore}, food={p.food}, pop={p.currentPopulation}/{p.maxPopulation}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySaveWorld(string worldName, RTSWorldSaveData data, out string error)
        {
            error = null;
            if (!TryValidateWorldName(worldName, out string sanitized, out error))
                return false;
            if (data == null)
            {
                error = "No data.";
                return false;
            }

            try
            {
                string directory = GetWorldDirectory(sanitized);
                Directory.CreateDirectory(directory);
                string path = GetWorldLevelPath(sanitized);

                var dto = SaveFileDto.FromRuntime(data);
                string json = JsonUtility.ToJson(dto, false);

                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tempPath, path);

                UnityEngine.Debug.Log($"[WorldSave] Saved world '{sanitized}' to: {path} ({json.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static System.Threading.Tasks.Task<bool> TrySaveWorldAsync(string worldName, RTSWorldSaveData data)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                return TrySaveWorld(worldName, data, out _);
            });
        }

        public static void OpenSavesFolder()
        {
            string root = LevelsDirectory;
            Directory.CreateDirectory(root);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = root,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WorldSaveService] Open folder failed: {ex.Message}");
            }
        }

        /// <summary>JsonUtility is reliable with arrays; Lists are converted at the boundary.</summary>
        public static string ToJson(RTSWorldSaveData data, bool prettyPrint = false)
        {
            var dto = SaveFileDto.FromRuntime(data);
            return JsonUtility.ToJson(dto, prettyPrint);
        }

        public static RTSWorldSaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            SaveFileDto dto = JsonUtility.FromJson<SaveFileDto>(json);
            return dto?.ToRuntime();
        }

        [Serializable]
        class SaveFileDto
        {
            public int version = 1;
            public int worldSeed;
            public PlayerSaveData[] players;
            public BuildingSaveData[] buildings;
            public UnitSaveData[] units;
            public ResourceSaveData[] resources;
            public GardenBedSaveData[] gardenBeds;

            public static SaveFileDto FromRuntime(RTSWorldSaveData data)
            {
                var dto = new SaveFileDto
                {
                    version = data.version,
                    worldSeed = data.worldSeed,
                    players = data.players?.ToArray() ?? Array.Empty<PlayerSaveData>(),
                    buildings = data.buildings?.ToArray() ?? Array.Empty<BuildingSaveData>(),
                    units = data.units?.ToArray() ?? Array.Empty<UnitSaveData>(),
                    resources = data.resources?.ToArray() ?? Array.Empty<ResourceSaveData>(),
                    gardenBeds = data.gardenBeds?.ToArray() ?? Array.Empty<GardenBedSaveData>()
                };
                return dto;
            }

            public RTSWorldSaveData ToRuntime()
            {
                return new RTSWorldSaveData
                {
                    version = version,
                    worldSeed = worldSeed,
                    players = players != null ? new List<PlayerSaveData>(players) : new List<PlayerSaveData>(),
                    buildings = buildings != null ? new List<BuildingSaveData>(buildings) : new List<BuildingSaveData>(),
                    units = units != null ? new List<UnitSaveData>(units) : new List<UnitSaveData>(),
                    resources = resources != null ? new List<ResourceSaveData>(resources) : new List<ResourceSaveData>(),
                    gardenBeds = gardenBeds != null ? new List<GardenBedSaveData>(gardenBeds) : new List<GardenBedSaveData>()
                };
            }
        }
    }

    public static class SaveRounding
    {
        public static float R2(float v) => Mathf.Round(v * 100f) / 100f;

        public static Vector3 R2(Vector3 v) => new Vector3(R2(v.x), R2(v.y), R2(v.z));
    }
}
