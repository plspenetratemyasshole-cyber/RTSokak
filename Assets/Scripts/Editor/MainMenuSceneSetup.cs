#if UNITY_EDITOR
using kcp2k;
using Mirror;
using RTSokak.Network;
using RTSokak.Shared;
using RTSokak.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTSokak.Editor
{
    public static class MainMenuSceneSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/RTS Setup/Setup Main Menu Scene")]
        public static void SetupMainMenuScene()
        {
            EnsureFolders();
            CreateOrUpdateMainMenuScene();
            CleanupGameSceneHud();
            EnsurePauseInGameScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[RTSokak] Main menu setup complete. Open MainMenu.unity and press Play.");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        static void CreateOrUpdateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Prefer copying NetworkManager from SampleScene if present.
            GameObject networkManagerObject = null;
            RTSNetworkManager existingNm = null;

            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in gameScene.GetRootGameObjects())
            {
                existingNm = root.GetComponentInChildren<RTSNetworkManager>(true);
                if (existingNm != null)
                {
                    networkManagerObject = Object.Instantiate(existingNm.gameObject);
                    networkManagerObject.name = "NetworkManager";
                    SceneManager.MoveGameObjectToScene(networkManagerObject, scene);
                    break;
                }
            }

            EditorSceneManager.CloseScene(gameScene, true);

            if (networkManagerObject == null)
            {
                networkManagerObject = new GameObject("NetworkManager");
                networkManagerObject.AddComponent<RTSNetworkManager>();
                networkManagerObject.AddComponent<KcpTransport>();
                networkManagerObject.AddComponent<RTSNetworkDiscovery>();
                networkManagerObject.AddComponent<NetworkTransportConfig>();
            }

            RTSNetworkManager networkManager = networkManagerObject.GetComponent<RTSNetworkManager>();
            KcpTransport kcp = networkManagerObject.GetComponent<KcpTransport>();
            if (kcp == null)
                kcp = networkManagerObject.AddComponent<KcpTransport>();

            RTSNetworkDiscovery discovery = networkManagerObject.GetComponent<RTSNetworkDiscovery>();
            if (discovery == null)
                discovery = networkManagerObject.AddComponent<RTSNetworkDiscovery>();

            if (networkManagerObject.GetComponent<NetworkTransportConfig>() == null)
                networkManagerObject.AddComponent<NetworkTransportConfig>();

            NetworkManagerHUD hud = networkManagerObject.GetComponent<NetworkManagerHUD>();
            if (hud == null)
                hud = networkManagerObject.AddComponent<NetworkManagerHUD>();
            hud.enabled = false;

            kcp.Port = PlayerSession.DefaultPort;
            discovery.transport = kcp;
            networkManager.transport = kcp;
            networkManager.dontDestroyOnLoad = true;
            networkManager.offlineScene = "MainMenu";
            networkManager.onlineScene = "SampleScene";

            SerializedObject so = new SerializedObject(networkManager);
            var discProp = so.FindProperty("networkDiscovery");
            if (discProp != null)
                discProp.objectReferenceValue = discovery;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssignNetworkPrefabs(networkManager);

            GameObject menuObject = new GameObject("MainMenuUI");
            menuObject.AddComponent<MainMenuUI>();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            // Build editable Canvas hierarchy + wire SerializeFields.
            MenuUiBuilder.BuildMainMenuUi(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            UnityEngine.Debug.Log(
                $"[RTSokak] Saved MainMenu NM: player={networkManager.playerPrefab != null}, " +
                $"worker={networkManager.WorkerPrefab != null}, spawns={networkManager.spawnPrefabs.Count}");
        }

        [MenuItem("Tools/RTS Setup/Assign Network Prefabs (MainMenu)")]
        public static void AssignNetworkPrefabsMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            RTSNetworkManager nm = Object.FindFirstObjectByType<RTSNetworkManager>();
            if (nm == null)
            {
                EditorUtility.DisplayDialog("Assign Prefabs", "RTSNetworkManager not found in MainMenu.", "OK");
                return;
            }

            AssignNetworkPrefabs(nm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UnityEngine.Debug.Log(
                $"[RTSokak] Prefabs assigned: player={nm.playerPrefab != null}, worker={nm.WorkerPrefab != null}, " +
                $"spawns={nm.spawnPrefabs.Count}");
        }

        static void AssignNetworkPrefabs(RTSNetworkManager networkManager)
        {
            if (networkManager == null)
                return;

            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RTSPlayer.prefab");
            GameObject worker = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Worker.prefab");
            GameObject warrior = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Warrior.prefab");
            GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Terrain/Tree.prefab");
            GameObject iron = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Terrain/Iron.prefab");
            GameObject capital = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Capital.prefab");
            GameObject house = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/House.prefab");
            GameObject warehouse = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Warehouse.prefab");
            GameObject barracks = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Barracks.prefab");
            GameObject garden = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Garden bed.prefab");

            SerializedObject so = new SerializedObject(networkManager);
            so.FindProperty("playerPrefab").objectReferenceValue = player;
            so.FindProperty("workerPrefab").objectReferenceValue = worker;
            so.FindProperty("warriorPrefab").objectReferenceValue = warrior;
            so.ApplyModifiedPropertiesWithoutUndo();

            void AddSpawn(GameObject prefab)
            {
                if (prefab == null || networkManager.spawnPrefabs.Contains(prefab))
                    return;
                networkManager.spawnPrefabs.Add(prefab);
            }

            AddSpawn(player);
            AddSpawn(worker);
            AddSpawn(warrior);
            AddSpawn(tree);
            AddSpawn(iron);
            AddSpawn(capital);
            AddSpawn(house);
            AddSpawn(warehouse);
            AddSpawn(barracks);
            AddSpawn(garden);

            EditorUtility.SetDirty(networkManager);
        }

        static void CleanupGameSceneHud()
        {
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            RTSNetworkManager[] managers = Object.FindObjectsByType<RTSNetworkManager>(FindObjectsSortMode.None);
            if (managers.Length == 0)
                UnityEngine.Debug.LogWarning("[RTSokak] No NetworkManager in SampleScene to remove (already moved to MainMenu?).");

            foreach (var nm in managers)
                Object.DestroyImmediate(nm.gameObject);

            NetworkManagerHUD[] huds = Object.FindObjectsByType<NetworkManagerHUD>(FindObjectsSortMode.None);
            foreach (var hud in huds)
            {
                if (hud != null)
                    Object.DestroyImmediate(hud);
            }

            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
        }

        static void EnsurePauseInGameScene()
        {
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            MenuUiBuilder.BuildPauseMenuUi(gameScene);
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
        }

        static void UpdateBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
#endif
