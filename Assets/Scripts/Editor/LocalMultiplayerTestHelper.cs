#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RTSokak.Editor
{
    /// <summary>
    /// Unity 6 Editor при Alt+Tab специально троттлит Play Mode (~5–10 FPS) — by design.
    /// Для теста мультиплеера на одном ПК хост нужно запускать из Standalone build.
    /// </summary>
    public static class LocalMultiplayerTestHelper
    {
        private const string BuildFolder = "Builds/LocalHostTest";
        private const string ExeName = "RTSokakHost.exe";

        [MenuItem("Tools/RTS Setup/Build Host for Local Multiplayer Test", priority = 50)]
        public static void BuildHost()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Host",
                    "Unity 6 Editor при потере фокуса (Alt+Tab) специально режет FPS до ~5–10.\n" +
                    "runInBackground это НЕ отключает — так задумано Unity.\n\n" +
                    "Соберу Standalone-хост. Потом:\n" +
                    "1) Запусти билд (Host)\n" +
                    "2) В Editor нажми Play и зайди как Client\n\n" +
                    "Продолжить сборку?",
                    "Build",
                    "Cancel"))
                return;

            string dir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, BuildFolder);
            Directory.CreateDirectory(dir);
            string exePath = Path.Combine(dir, ExeName);

            string[] scenePaths = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled),
                s => s.path);

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            // Fallback: current open scenes if build settings empty
            if (options.scenes == null || options.scenes.Length == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
                if (guids.Length == 0)
                {
                    EditorUtility.DisplayDialog("Error", "No scenes found in Assets/Scenes", "OK");
                    return;
                }

                string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                options.scenes = new[] { scenePath };
            }

            PlayerSettings.runInBackground = true;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build Failed", report.summary.result.ToString(), "OK");
                return;
            }

            bool run = EditorUtility.DisplayDialog(
                "Build OK",
                $"Host build:\n{exePath}\n\nЗапустить сейчас?",
                "Run Host",
                "Later");

            if (run)
                RunHostBuild();
        }

        [MenuItem("Tools/RTS Setup/Run Host Build", priority = 51)]
        public static void RunHostBuild()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, BuildFolder);
            string exePath = Path.Combine(dir, ExeName);
            if (!File.Exists(exePath))
            {
                EditorUtility.DisplayDialog(
                    "No Host Build",
                    "Сначала: Tools → RTS Setup → Build Host for Local Multiplayer Test",
                    "OK");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = dir,
                UseShellExecute = true
            };
            Process.Start(psi);
            UnityEngine.Debug.Log($"[RTS] Started host build: {exePath}");
        }

        [MenuItem("Tools/RTS Setup/Explain Editor Alt+Tab Throttle", priority = 52)]
        public static void ExplainThrottle()
        {
            EditorUtility.DisplayDialog(
                "Why host freezes on Alt+Tab",
                "Unity 6 (у вас 6000.0.x) при потере фокуса Editor специально троттлит Play Mode до ~5–10 FPS.\n\n" +
                "Это официально As Designed (issuetracker):\n" +
                "«There is no setting to reduce the throttling in the Editor».\n\n" +
                "Application.runInBackground влияет на Standalone Player, но НЕ отменяет троттлинг Editor.\n\n" +
                "Как тестировать на одном ПК:\n" +
                "• Host = Standalone build (Tools → Build Host…)\n" +
                "• Client = Editor Play Mode\n" +
                "Тогда Alt+Tab по Editor не останавливает сервер.",
                "OK");
        }
    }
}
#endif
