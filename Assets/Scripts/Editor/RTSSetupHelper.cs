using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.AI;

namespace RTSokak.Editor
{
    /// <summary>
    /// Автоматизация настройки RTS игры
    /// Меню: Tools → RTS Setup
    /// </summary>
    public class RTSSetupHelper : EditorWindow
    {
        [MenuItem("Tools/RTS Setup Helper")]
        public static void ShowWindow()
        {
            GetWindow<RTSSetupHelper>("RTS Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("RTS Game Setup Helper", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // === NAVMESH ===
            GUILayout.Label("NavMesh", EditorStyles.boldLabel);
            
            if (GUILayout.Button("1. Make Ground Walkable", GUILayout.Height(30)))
            {
                MakeGroundWalkable();
            }
            
            if (GUILayout.Button("2. Bake NavMesh", GUILayout.Height(30)))
            {
                BakeNavMesh();
            }
            
            GUILayout.Space(10);
            
            // === COMPONENTS ===
            GUILayout.Label("Worker Setup", EditorStyles.boldLabel);
            
            if (GUILayout.Button("3. Setup Worker Prefab", GUILayout.Height(30)))
            {
                SetupWorkerPrefab();
            }
            
            GUILayout.Space(10);
            
            // === VALIDATION ===
            GUILayout.Label("Validation", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Check Setup", GUILayout.Height(30)))
            {
                ValidateSetup();
            }
            
            GUILayout.Space(10);
            
            // === HUD ===
            GUILayout.Label("Network HUD", EditorStyles.boldLabel);
            
            if (GUILayout.Button("4. Add Network HUD", GUILayout.Height(30)))
            {
                AddNetworkHUD();
            }
            
            GUILayout.Space(10);
            
            // === RESOURCES ===
            GUILayout.Label("Resource Prefabs", EditorStyles.boldLabel);
            
            if (GUILayout.Button("5. Setup Resource Prefabs", GUILayout.Height(30)))
            {
                SetupResourcePrefabs();
            }
            
            if (GUILayout.Button("5b. Configure Resource Health", GUILayout.Height(30)))
            {
                ConfigureResourceHealth();
            }
            
            if (GUILayout.Button("5c. Debug Resource Values", GUILayout.Height(30)))
            {
                DebugResourceValues();
            }
            
            if (GUILayout.Button("6. Create ResourceManager", GUILayout.Height(30)))
            {
                CreateResourceManagerInScene();
            }
            
            if (GUILayout.Button("7. Create Resource UI", GUILayout.Height(30)))
            {
                CreateResourceUI();
            }

            if (GUILayout.Button("8. Create Nameplate Prefab", GUILayout.Height(30)))
            {
                CreateNameplatePrefab();
            }
            
            if (GUILayout.Button("9. Update Worker Prefab Values", GUILayout.Height(30)))
            {
                UpdateWorkerPrefabValues();
            }

            GUILayout.Space(10);
            GUILayout.Label("Buildings & Selection UI", EditorStyles.boldLabel);

            if (GUILayout.Button("10. Setup Buildings + Full UI", GUILayout.Height(40)))
            {
                SetupBuildingsAndFullUI();
            }

            if (GUILayout.Button("11. Create Wheat Prefab + Wire Garden Bed", GUILayout.Height(35)))
            {
                CreateWheatAndWireGarden();
            }

            if (GUILayout.Button("12. Add Warehouse + Barracks", GUILayout.Height(35)))
            {
                SetupWarehouseAndBarracks();
            }

            GUILayout.Space(10);
            GUILayout.Label("Units Spawning (Runtime Only)", EditorStyles.boldLabel);

            if (GUILayout.Button("13. Spawn Archer (Host only)", GUILayout.Height(35)))
            {
                SpawnArcher();
            }
        }

        /// <summary>
        /// Делает все объекты с именем "Plane" или "Ground" Navigation Static
        /// </summary>
        private void MakeGroundWalkable()
        {
            int count = 0;
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.Contains("Plane") || obj.name.ToLower().Contains("ground"))
                {
                    // Делаем Navigation Static
                    GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.NavigationStatic);
                    
                    // Добавляем Collider если его нет
                    if (obj.GetComponent<Collider>() == null)
                    {
                        obj.AddComponent<MeshCollider>();
                    }
                    
                    count++;
                    UnityEngine.Debug.Log($"Made {obj.name} Navigation Static");
                }
            }
            
            if (count > 0)
            {
                EditorUtility.DisplayDialog("Success", 
                    $"Made {count} object(s) Navigation Static.\nNow click 'Bake NavMesh'.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Warning", 
                    "No objects named 'Plane' or 'Ground' found.\nCreate a Plane first (GameObject → 3D Object → Plane).", 
                    "OK");
            }
        }

        /// <summary>
        /// Запекает NavMesh с оптимальными настройками для RTS
        /// </summary>
        private void BakeNavMesh()
        {
            // Используем стандартный запекатель Unity Editor
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            
            EditorUtility.DisplayDialog("Success", 
                "NavMesh baked!\nGround should be blue in Scene view.\nNow setup Worker prefab.", 
                "OK");
        }

        /// <summary>
        /// Настраивает Worker prefab: добавляет все нужные компоненты
        /// </summary>
        private void SetupWorkerPrefab()
        {
            // Ищем Worker prefab
            string[] guids = AssetDatabase.FindAssets("Worker t:Prefab");
            
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Worker prefab not found!\nCreate a prefab named 'Worker' first.", 
                    "OK");
                return;
            }
            
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (workerPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load Worker prefab.", "OK");
                return;
            }
            
            // Создаём временный экземпляр для редактирования
            GameObject instance = PrefabUtility.InstantiatePrefab(workerPrefab) as GameObject;
            
            // === 1. NavMeshAgent ===
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = instance.AddComponent<NavMeshAgent>();
            }
            agent.speed = 5f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 1f; // ВАЖНО! Worker на высоте y=1
            
            // === 2. LineRenderer ===
            LineRenderer lineRenderer = instance.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = instance.AddComponent<LineRenderer>();
            }
            lineRenderer.startWidth = 0.15f;
            lineRenderer.endWidth = 0.15f;
            lineRenderer.positionCount = 0;
            
            // Используем Particles/Standard Unlit - всегда видимый шейдер
            Material lineMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            lineMaterial.SetColor("_Color", Color.green);
            lineRenderer.material = lineMaterial;
            
            // Дополнительные настройки для видимости
            lineRenderer.startColor = Color.green;
            lineRenderer.endColor = Color.green;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.useWorldSpace = true; // ВАЖНО! Используем мировые координаты
            lineRenderer.enabled = false;
            
            // === 3. Collider ===
            BoxCollider collider = instance.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = instance.AddComponent<BoxCollider>();
            }
            // Коллайдер должен быть на высоте юнита (y=1)
            collider.center = new Vector3(0f, 1f, 0f);
            collider.size = new Vector3(1f, 2f, 1f);
            
            // === 4. Проверяем NetworkIdentity ===
            if (instance.GetComponent<Mirror.NetworkIdentity>() == null)
            {
                UnityEngine.Debug.LogWarning("Worker prefab needs Mirror NetworkIdentity! Add it manually.");
            }
            
            // Сохраняем изменения в prefab
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            DestroyImmediate(instance);
            
            EditorUtility.DisplayDialog("Success", 
                $"Worker prefab setup complete!\n\n" +
                "Added:\n" +
                "✓ NavMeshAgent\n" +
                "✓ LineRenderer\n" +
                "✓ BoxCollider\n\n" +
                "⚠ Don't forget to add:\n" +
                "- Mirror NetworkIdentity\n" +
                "- Outline component (from Asset Store)", 
                "OK");
            
            // Выбираем prefab в Project
            Selection.activeObject = workerPrefab;
            EditorGUIUtility.PingObject(workerPrefab);
        }

        /// <summary>
        /// Проверяет что всё настроено правильно
        /// </summary>
        private void ValidateSetup()
        {
            string report = "=== RTS Setup Validation ===\n\n";
            bool allGood = true;
            
            // 1. NavMesh
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices.Length > 0)
            {
                report += $"✓ NavMesh is baked ({triangulation.vertices.Length} vertices)\n";
            }
            else
            {
                report += "✗ NavMesh NOT baked - click 'Bake NavMesh' button!\n";
                allGood = false;
            }
            
            // 2. Ground
            GameObject[] grounds = GameObject.FindObjectsOfType<GameObject>();
            bool foundGround = false;
            foreach (GameObject obj in grounds)
            {
                if (obj.name.Contains("Plane") || obj.name.ToLower().Contains("ground"))
                {
                    if (GameObjectUtility.GetStaticEditorFlags(obj).HasFlag(StaticEditorFlags.NavigationStatic))
                    {
                        foundGround = true;
                        break;
                    }
                }
            }
            
            if (foundGround)
            {
                report += "✓ Ground is Navigation Static\n";
            }
            else
            {
                report += "✗ No Navigation Static ground found\n";
                allGood = false;
            }
            
            // 3. Worker prefab
            string[] guids = AssetDatabase.FindAssets("Worker t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                bool hasNavMeshAgent = workerPrefab.GetComponent<NavMeshAgent>() != null;
                bool hasLineRenderer = workerPrefab.GetComponent<LineRenderer>() != null;
                bool hasCollider = workerPrefab.GetComponent<Collider>() != null;
                
                report += $"\nWorker Prefab:\n";
                report += hasNavMeshAgent ? "✓ NavMeshAgent\n" : "✗ NavMeshAgent missing\n";
                report += hasLineRenderer ? "✓ LineRenderer\n" : "✗ LineRenderer missing\n";
                report += hasCollider ? "✓ Collider\n" : "✗ Collider missing\n";
                
                if (!hasNavMeshAgent || !hasLineRenderer || !hasCollider)
                {
                    allGood = false;
                }
            }
            else
            {
                report += "✗ Worker prefab not found\n";
                allGood = false;
            }
            
            report += "\n" + (allGood ? "🎉 Everything is ready!" : "⚠ Some issues found - use buttons above to fix");
            
            EditorUtility.DisplayDialog("Setup Validation", report, "OK");
            UnityEngine.Debug.Log(report);
        }

        /// <summary>
        /// Добавляет NetworkManagerHUD на объект с NetworkManager в сцене
        /// </summary>
        private void AddNetworkHUD()
        {
            // Ищем RTSNetworkManager в сцене
            RTSokak.Network.RTSNetworkManager networkManager = FindObjectOfType<RTSokak.Network.RTSNetworkManager>();
            
            if (networkManager == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "RTSNetworkManager not found in scene!\n\n" +
                    "Add RTSNetworkManager to the scene first:\n" +
                    "1. Create empty GameObject\n" +
                    "2. Add Component → Network → RTS Network Manager", 
                    "OK");
                return;
            }
            
            // Проверяем, есть ли уже NetworkManagerHUD
            Mirror.NetworkManagerHUD existingHUD = networkManager.GetComponent<Mirror.NetworkManagerHUD>();
            
            if (existingHUD != null)
            {
                EditorUtility.DisplayDialog("Info", 
                    "NetworkManagerHUD already exists on this object!", 
                    "OK");
                return;
            }
            
            // Добавляем NetworkManagerHUD
            Mirror.NetworkManagerHUD hud = networkManager.gameObject.AddComponent<Mirror.NetworkManagerHUD>();
            
            // Настраиваем смещение чтобы HUD не перекрывал логи
            hud.offsetX = 0;
            hud.offsetY = 0;
            
            // Помечаем объект как изменённый
            UnityEditor.EditorUtility.SetDirty(networkManager.gameObject);
            
            EditorUtility.DisplayDialog("Success", 
                "NetworkManagerHUD added to RTSNetworkManager!\n\n" +
                "Now you can start Host/Server/Client from Unity Editor.\n" +
                "The HUD will appear in Play mode in the top-left corner.", 
                "OK");
            
            // Выбираем NetworkManager в иерархии
            Selection.activeGameObject = networkManager.gameObject;
            EditorGUIUtility.PingObject(networkManager.gameObject);
        }

        /// <summary>
        /// Настраивает префабы ресурсов (Tree и Iron)
        /// </summary>
        private void SetupResourcePrefabs()
        {
            int setupCount = 0;
            
            // Ищем Tree.prefab
            string[] treeGuids = AssetDatabase.FindAssets("Tree t:Prefab", new[] { "Assets/Prefabs/Terrain" });
            if (treeGuids.Length > 0)
            {
                string treePath = AssetDatabase.GUIDToAssetPath(treeGuids[0]);
                GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                
                if (SetupSingleResourcePrefab(treePrefab, RTSokak.Resources.ResourceType.Wood))
                {
                    setupCount++;
                }
            }
            
            // Ищем Iron.prefab
            string[] ironGuids = AssetDatabase.FindAssets("Iron t:Prefab", new[] { "Assets/Prefabs/Terrain" });
            if (ironGuids.Length > 0)
            {
                string ironPath = AssetDatabase.GUIDToAssetPath(ironGuids[0]);
                GameObject ironPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ironPath);
                
                if (SetupSingleResourcePrefab(ironPrefab, RTSokak.Resources.ResourceType.Ore))
                {
                    setupCount++;
                }
            }
            
            if (setupCount > 0)
            {
                EditorUtility.DisplayDialog("Success",
                    $"Setup {setupCount} resource prefab(s)!\n\n" +
                    "Added:\n" +
                    "✓ NetworkIdentity\n" +
                    "✓ Resource component\n" +
                    "✓ BoxCollider\n" +
                    "✓ NavMeshObstacle (юниты не будут проходить сквозь ресурсы)",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error",
                    "Resource prefabs not found!\n\n" +
                    "Expected:\n" +
                    "- Assets/Prefabs/Terrain/Tree.prefab\n" +
                    "- Assets/Prefabs/Terrain/Iron.prefab",
                    "OK");
            }
        }

        /// <summary>
        /// Настраивает один префаб ресурса
        /// </summary>
        private bool SetupSingleResourcePrefab(GameObject prefab, RTSokak.Resources.ResourceType resourceType)
        {
            if (prefab == null)
                return false;

            // Создаём временный экземпляр
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            // === 1. NetworkIdentity ===
            Mirror.NetworkIdentity netId = instance.GetComponent<Mirror.NetworkIdentity>();
            if (netId == null)
            {
                netId = instance.AddComponent<Mirror.NetworkIdentity>();
            }

            // === 2. Resource Component ===
            RTSokak.Resources.Resource resource = instance.GetComponent<RTSokak.Resources.Resource>();
            if (resource == null)
            {
                resource = instance.AddComponent<RTSokak.Resources.Resource>();
            }
            
            // Используем рефлексию для установки приватного поля resourceType
            var field = typeof(RTSokak.Resources.Resource).GetField("resourceType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(resource, resourceType);
            }

            // === 3. BoxCollider ===
            BoxCollider collider = instance.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = instance.AddComponent<BoxCollider>();
                // Устанавливаем размер коллайдера
                collider.size = new Vector3(1f, 2f, 1f);
                collider.center = new Vector3(0f, 1f, 0f);
            }

            // === 4. NavMeshObstacle ===
            UnityEngine.AI.NavMeshObstacle obstacle = instance.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = instance.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            }
            obstacle.carving = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;

            // Сохраняем изменения в prefab
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            DestroyImmediate(instance);

            UnityEngine.Debug.Log($"Setup resource prefab: {prefab.name} as {resourceType}");
            return true;
        }

        /// <summary>
        /// Создаёт ResourceManager в сцене с настройками по умолчанию
        /// </summary>
        private void CreateResourceManagerInScene()
        {
            // Проверяем что ResourceManager ещё не существует
            RTSokak.Resources.ResourceManager existing = FindObjectOfType<RTSokak.Resources.ResourceManager>();
            
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Info",
                    "ResourceManager already exists in the scene!",
                    "OK");
                
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            // Создаём GameObject
            GameObject rmObj = new GameObject("ResourceManager");
            RTSokak.Resources.ResourceManager rm = rmObj.AddComponent<RTSokak.Resources.ResourceManager>();

            // Добавляем NetworkIdentity
            Mirror.NetworkIdentity netId = rmObj.AddComponent<Mirror.NetworkIdentity>();

            // Загружаем префабы ресурсов
            string[] treeGuids = AssetDatabase.FindAssets("Tree t:Prefab", new[] { "Assets/Prefabs/Terrain" });
            string[] ironGuids = AssetDatabase.FindAssets("Iron t:Prefab", new[] { "Assets/Prefabs/Terrain" });

            GameObject treePrefab = null;
            GameObject orePrefab = null;

            if (treeGuids.Length > 0)
            {
                string treePath = AssetDatabase.GUIDToAssetPath(treeGuids[0]);
                treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
            }

            if (ironGuids.Length > 0)
            {
                string ironPath = AssetDatabase.GUIDToAssetPath(ironGuids[0]);
                orePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ironPath);
            }

            // Настраиваем конфигурацию через рефлексию
            var configField = typeof(RTSokak.Resources.ResourceManager).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (configField != null)
            {
                RTSokak.Resources.ResourceGenerationConfig config = new RTSokak.Resources.ResourceGenerationConfig();
                config.worldSize = 100;
                config.forestCount = 5;
                config.minForestSize = 8;
                config.maxForestSize = 20;
                config.forestDensity = 0.7f;
                config.oreDepositCount = 3;
                config.minOreSize = 5;
                config.maxOreSize = 12;
                config.oreDensity = 0.5f;
                config.treePrefab = treePrefab;
                config.treeYOffset = 0f;
                config.orePrefab = orePrefab;
                config.oreYOffset = 0f;

                configField.SetValue(rm, config);
            }

            // Помечаем как изменённый
            EditorUtility.SetDirty(rmObj);

            string message = "ResourceManager created!\n\n" +
                "✓ NetworkIdentity added\n" +
                "✓ Default config set\n";

            if (treePrefab != null)
                message += "✓ Tree prefab assigned\n";
            else
                message += "⚠ Tree prefab not found\n";

            if (orePrefab != null)
                message += "✓ Ore prefab assigned\n";
            else
                message += "⚠ Ore prefab not found\n";

            message += "\nYou can adjust settings in the Inspector:\n" +
                       "- World Size\n" +
                       "- Forest/Ore count, size, density\n" +
                       "- Y-offsets for each prefab";

            EditorUtility.DisplayDialog("Success", message, "OK");

            Selection.activeGameObject = rmObj;
            EditorGUIUtility.PingObject(rmObj);
        }

        /// <summary>
        /// Создаёт UI для отображения ресурсов игрока
        /// </summary>
        private void CreateResourceUI()
        {
            // Проверяем есть ли Canvas в сцене
            Canvas canvas = FindObjectOfType<Canvas>();
            
            if (canvas == null)
            {
                // Создаём Canvas
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // Высокий порядок сортировки
                
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                
                UnityEngine.Debug.Log("Created new Canvas for UI");
            }
            else
            {
                UnityEngine.Debug.Log($"Using existing Canvas: {canvas.name}");
            }

            // Проверяем EventSystem
            UnityEngine.EventSystems.EventSystem eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                UnityEngine.Debug.Log("Created EventSystem");
            }

            // Создаём панель ресурсов
            GameObject panelObj = new GameObject("ResourcePanel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(200, 80);

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            // Создаём текст для дерева
            GameObject woodTextObj = new GameObject("WoodText");
            woodTextObj.transform.SetParent(panelObj.transform, false);

            RectTransform woodRect = woodTextObj.AddComponent<RectTransform>();
            woodRect.anchorMin = new Vector2(0, 0.5f);
            woodRect.anchorMax = new Vector2(1, 1);
            woodRect.offsetMin = new Vector2(10, 0);
            woodRect.offsetMax = new Vector2(-10, -5);

            TMPro.TextMeshProUGUI woodText = woodTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            woodText.text = "Wood: 0";
            woodText.fontSize = 18;
            woodText.color = Color.white;

            // Создаём текст для руды
            GameObject oreTextObj = new GameObject("OreText");
            oreTextObj.transform.SetParent(panelObj.transform, false);

            RectTransform oreRect = oreTextObj.AddComponent<RectTransform>();
            oreRect.anchorMin = new Vector2(0, 0);
            oreRect.anchorMax = new Vector2(1, 0.5f);
            oreRect.offsetMin = new Vector2(10, 5);
            oreRect.offsetMax = new Vector2(-10, 0);

            TMPro.TextMeshProUGUI oreText = oreTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            oreText.text = "Ore: 0";
            oreText.fontSize = 18;
            oreText.color = Color.white;

            // Добавляем компонент PlayerResourcesUI
            RTSokak.UI.PlayerResourcesUI resourceUI = panelObj.AddComponent<RTSokak.UI.PlayerResourcesUI>();

            // Настраиваем через рефлексию
            var woodTextField = typeof(RTSokak.UI.PlayerResourcesUI).GetField("woodText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (woodTextField != null)
            {
                woodTextField.SetValue(resourceUI, woodText);
            }

            var oreTextField = typeof(RTSokak.UI.PlayerResourcesUI).GetField("oreText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (oreTextField != null)
            {
                oreTextField.SetValue(resourceUI, oreText);
            }

            var panelField = typeof(RTSokak.UI.PlayerResourcesUI).GetField("panel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (panelField != null)
            {
                panelField.SetValue(resourceUI, panelObj);
            }

            EditorUtility.SetDirty(panelObj);

            EditorUtility.DisplayDialog("Success",
                "Resource UI created!\n\n" +
                "✓ Canvas created (if needed)\n" +
                "✓ Resource panel in top-left corner\n" +
                "✓ Wood and Ore counters\n" +
                "✓ PlayerResourcesUI component\n\n" +
                "The UI will show resources when you start the game.",
                "OK");

            Selection.activeGameObject = panelObj;
            EditorGUIUtility.PingObject(panelObj);
        }

        /// <summary>
        /// Создаёт prefab для UnitNameplate с правильными настройками
        /// </summary>
        private void CreateNameplatePrefab()
        {
            // Создаём GameObject для nameplate
            GameObject nameplateObj = new GameObject("UnitNameplate");

            // Добавляем Canvas
            Canvas canvas = nameplateObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Добавляем CanvasScaler
            CanvasScaler scaler = nameplateObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            // Настраиваем RectTransform канваса
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 50);

            // Создаём текстовый объект
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(nameplateObj.transform, false);

            TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "Worker";
            text.fontSize = 18f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.localPosition = Vector3.zero;

            // Добавляем UnitNameplate компонент
            RTSokak.UI.UnitNameplate nameplate = nameplateObj.AddComponent<RTSokak.UI.UnitNameplate>();

            // Настраиваем через reflection
            var textField = typeof(RTSokak.UI.UnitNameplate).GetField("professionText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (textField != null)
            {
                textField.SetValue(nameplate, text);
            }

            var canvasField = typeof(RTSokak.UI.UnitNameplate).GetField("canvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (canvasField != null)
            {
                canvasField.SetValue(nameplate, canvas);
            }

            var textSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("textSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (textSizeField != null)
            {
                textSizeField.SetValue(nameplate, 0.02f);
            }

            var fontSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("fontSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fontSizeField != null)
            {
                fontSizeField.SetValue(nameplate, 18f);
            }

            // Устанавливаем локальный scale для правильного размера
            nameplateObj.transform.localScale = Vector3.one * 0.02f;

            // Сохраняем как prefab
            string prefabPath = "Assets/Prefabs/UI/UnitNameplate.prefab";
            
            // Создаём директорию если нужно
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Сохраняем prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(nameplateObj, prefabPath);
            DestroyImmediate(nameplateObj);

            EditorUtility.DisplayDialog("Success",
                "UnitNameplate prefab created!\n\n" +
                $"Path: {prefabPath}\n\n" +
                "Настройки:\n" +
                "✓ Text Size: 0.02 (очень маленький)\n" +
                "✓ Font Size: 18\n" +
                "✓ WorldSpace Canvas\n\n" +
                "Чтобы изменить размер текста:\n" +
                "1. Выбери prefab в Project\n" +
                "2. Найди UnitNameplate компонент\n" +
                "3. Измени Text Size (0.01-0.05) и Font Size (10-30)",
                "OK");

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        
        /// <summary>
        /// Обновляет SerializeField значения в Worker prefab
        /// </summary>
        private void UpdateWorkerPrefabValues()
        {
            // КРИТИЧНО: Правильный путь к prefab!
            string prefabPath = "Assets/Prefabs/Units/Worker.prefab";
            GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (workerPrefab == null)
            {
                // Пробуем старый путь для обратной совместимости
                prefabPath = "Assets/Prefabs/Worker.prefab";
                workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            
            if (workerPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Worker prefab not found!\n\n" +
                    "Tried paths:\n" +
                    "• Assets/Prefabs/Units/Worker.prefab\n" +
                    "• Assets/Prefabs/Worker.prefab\n\n" +
                    "Please locate the Worker prefab manually.", 
                    "OK");
                return;
            }
            
            // Создаём временный экземпляр для редактирования
            GameObject tempWorker = (GameObject)PrefabUtility.InstantiatePrefab(workerPrefab);
            RTSokak.Network.Worker worker = tempWorker.GetComponent<RTSokak.Network.Worker>();
            
            if (worker == null)
            {
                EditorUtility.DisplayDialog("Error", "Worker component not found on prefab!", "OK");
                DestroyImmediate(tempWorker);
                return;
            }
            
            // Используем SerializedObject для изменения приватных SerializeField
            SerializedObject serializedWorker = new SerializedObject(worker);
            
            // Movement Settings
            serializedWorker.FindProperty("moveSpeed").floatValue = 3.5f;
            serializedWorker.FindProperty("stoppingDistance").floatValue = 2.5f;
            serializedWorker.FindProperty("angularSpeed").floatValue = 360f;
            serializedWorker.FindProperty("acceleration").floatValue = 8f;
            
            // Harvesting Settings
            var woodDur = serializedWorker.FindProperty("woodHarvestDuration");
            if (woodDur != null) woodDur.floatValue = 1.5f;
            var oreDur = serializedWorker.FindProperty("oreHarvestDuration");
            if (oreDur != null) oreDur.floatValue = 2f;
            serializedWorker.FindProperty("harvestDistanceTolerance").floatValue = 6f;
            serializedWorker.FindProperty("autoHarvestRadius").floatValue = 25f;

            var maxInv = serializedWorker.FindProperty("maxInventory");
            if (maxInv != null) maxInv.intValue = 500;
            
            // Visual Settings
            serializedWorker.FindProperty("outlineWidth").floatValue = 2.5f;
            serializedWorker.FindProperty("pathLineWidth").floatValue = 0.3f;
            serializedWorker.FindProperty("lineHeight").floatValue = 0.1f;
            serializedWorker.FindProperty("pathLineColor").colorValue = Color.green;
            
            // Применяем изменения
            serializedWorker.ApplyModifiedProperties();
            
            // Сохраняем prefab
            PrefabUtility.SaveAsPrefabAsset(tempWorker, prefabPath);
            DestroyImmediate(tempWorker);
            
            EditorUtility.DisplayDialog("Success",
                "Worker prefab values updated!\n\n" +
                "Updated values:\n" +
                "• Stopping Distance: 2.5\n" +
                "• Harvest Tolerance: 6.0 (увеличено!)\n" +
                "• Outline Width: 2.5\n" +
                "• Path Line Width: 0.3\n\n" +
                "Worker будет останавливаться\n" +
                "на расстоянии от ресурсов!",
                "OK");
            
            Selection.activeObject = workerPrefab;
            EditorGUIUtility.PingObject(workerPrefab);
        }

        /// <summary>
        /// Создаёт префабы зданий, BuildingManager, обновляет HUD и нижнюю панель выбора.
        /// </summary>
        private void SetupBuildingsAndFullUI()
        {
            EnsureDirectory("Assets/Prefabs/Buildings");
            EnsureDirectory("Assets/Prefabs/UI");
            EnsureDirectory("Assets/Resources/Prefabs");

            GameObject capitalPrefab = CreateBuildingPrefab(
                "Capital",
                new Color(0.2f, 0.45f, 0.9f),
                new Vector3(3f, 2.5f, 3f),
                RTSokak.Buildings.BuildingType.Capital);

            GameObject housePrefab = CreateBuildingPrefab(
                "House",
                new Color(0.75f, 0.55f, 0.25f),
                new Vector3(2f, 1.8f, 2f),
                RTSokak.Buildings.BuildingType.House);

            GameObject warehousePrefab = CreateBuildingPrefab(
                "Warehouse",
                new Color(0.55f, 0.5f, 0.3f),
                new Vector3(2.5f, 2.2f, 2.5f),
                RTSokak.Buildings.BuildingType.Warehouse);

            GameObject barracksPrefab = CreateBuildingPrefab(
                "Barracks",
                new Color(0.55f, 0.3f, 0.28f),
                new Vector3(3f, 2.4f, 3f),
                RTSokak.Buildings.BuildingType.Barracks);
            EnsureBarracksProductionOnPrefab(barracksPrefab);

            GameObject wheatPrefab = CreateWheatPrefabAsset();
            GameObject gardenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Garden bed.prefab");
            if (gardenPrefab != null)
                WireGardenBedPrefab(gardenPrefab, wheatPrefab);

            // BuildingManager в сцене
            RTSokak.Buildings.BuildingManager existingBm =
                Object.FindFirstObjectByType<RTSokak.Buildings.BuildingManager>();
            GameObject bmObj = existingBm != null ? existingBm.gameObject : new GameObject("BuildingManager");
            if (existingBm == null)
                existingBm = bmObj.AddComponent<RTSokak.Buildings.BuildingManager>();

            var defs = new System.Collections.Generic.List<RTSokak.Buildings.BuildingDefinition>
            {
                new RTSokak.Buildings.BuildingDefinition
                {
                    type = RTSokak.Buildings.BuildingType.Capital,
                    displayName = "Capital",
                    prefab = capitalPrefab,
                    previewHeight = 1.25f,
                    buildDuration = 12f,
                    buryDepth = 3f,
                    approachDistance = 5f,
                    sizeX = 3,
                    sizeY = 3,
                    woodCost = 100,
                    oreCost = 100,
                    foodCost = 0,
                    populationProvided = 5,
                    maxHealth = 2000
                },
                new RTSokak.Buildings.BuildingDefinition
                {
                    type = RTSokak.Buildings.BuildingType.House,
                    displayName = "House",
                    prefab = housePrefab,
                    previewHeight = 0.9f,
                    buildDuration = 8f,
                    buryDepth = 2.5f,
                    approachDistance = 4f,
                    sizeX = 2,
                    sizeY = 2,
                    woodCost = 50,
                    oreCost = 30,
                    foodCost = 0,
                    populationProvided = 10,
                    maxHealth = 800
                },
                new RTSokak.Buildings.BuildingDefinition
                {
                    type = RTSokak.Buildings.BuildingType.GardenBed,
                    displayName = "Garden bed",
                    prefab = gardenPrefab,
                    previewHeight = 0.05f,
                    buildDuration = 10f,
                    buryDepth = 1.5f,
                    approachDistance = 3f,
                    sizeX = 2,
                    sizeY = 4,
                    woodCost = 40,
                    oreCost = 20,
                    foodCost = 0,
                    populationProvided = 0,
                    maxHealth = 400
                },
                new RTSokak.Buildings.BuildingDefinition
                {
                    type = RTSokak.Buildings.BuildingType.Warehouse,
                    displayName = "Warehouse",
                    prefab = warehousePrefab,
                    previewHeight = 1.1f,
                    buildDuration = 10f,
                    buryDepth = 2.5f,
                    approachDistance = 4f,
                    sizeX = 2,
                    sizeY = 2,
                    woodCost = 80,
                    oreCost = 40,
                    foodCost = 0,
                    populationProvided = 0,
                    maxHealth = 1200
                },
                new RTSokak.Buildings.BuildingDefinition
                {
                    type = RTSokak.Buildings.BuildingType.Barracks,
                    displayName = "Barracks",
                    prefab = barracksPrefab,
                    previewHeight = 1.2f,
                    buildDuration = 14f,
                    buryDepth = 2.8f,
                    approachDistance = 5f,
                    sizeX = 3,
                    sizeY = 3,
                    woodCost = 120,
                    oreCost = 80,
                    foodCost = 0,
                    populationProvided = 0,
                    maxHealth = 1500
                }
            };
            existingBm.EditorSetBuildings(defs);
            EditorUtility.SetDirty(existingBm);

            RegisterSpawnPrefab(capitalPrefab);
            RegisterSpawnPrefab(housePrefab);
            if (gardenPrefab != null)
                RegisterSpawnPrefab(gardenPrefab);
            RegisterSpawnPrefab(warehousePrefab);
            RegisterSpawnPrefab(barracksPrefab);

            CreateOrUpdateResourceHUD();
            CreateSelectionPanelUI();

            // UnitSelectionManager если нет
            if (Object.FindFirstObjectByType<RTSokak.UI.UnitSelectionManager>() == null)
            {
                GameObject sel = new GameObject("UnitSelectionManager");
                sel.AddComponent<RTSokak.UI.UnitSelectionManager>();
            }

            EditorUtility.DisplayDialog("Success",
                "Buildings + UI setup done!\n\n" +
                "• Prefabs: Capital, House, Garden bed, Wheat\n" +
                "• BuildingManager — sizeX/sizeY, costs, Garden bed\n" +
                "• Garden button on selection panel\n\n" +
                "Garden bed: empty children = plant points.\n" +
                "Assign Wheat prefab on GardenBed if needed.",
                "OK");

            Selection.activeGameObject = bmObj;
        }

        private void CreateWheatAndWireGarden()
        {
            EnsureDirectory("Assets/Resources/Prefabs");
            GameObject wheat = CreateWheatPrefabAsset();
            GameObject garden = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/Garden bed.prefab");
            if (garden != null)
                WireGardenBedPrefab(garden, wheat);
            RegisterSpawnPrefab(garden);

            var bm = Object.FindFirstObjectByType<RTSokak.Buildings.BuildingManager>();
            if (bm != null && garden != null)
            {
                var defs = new System.Collections.Generic.List<RTSokak.Buildings.BuildingDefinition>(bm.GetAllDefinitions());
                bool found = false;
                for (int i = 0; i < defs.Count; i++)
                {
                    if (defs[i].type == RTSokak.Buildings.BuildingType.GardenBed)
                    {
                        defs[i].prefab = garden;
                        defs[i].sizeX = Mathf.Max(1, defs[i].sizeX);
                        defs[i].sizeY = Mathf.Max(1, defs[i].sizeY);
                        if (defs[i].sizeX == 1 && defs[i].sizeY == 1)
                        {
                            defs[i].sizeX = 2;
                            defs[i].sizeY = 4;
                        }
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    defs.Add(new RTSokak.Buildings.BuildingDefinition
                    {
                        type = RTSokak.Buildings.BuildingType.GardenBed,
                        displayName = "Garden bed",
                        prefab = garden,
                        previewHeight = 0.05f,
                        buildDuration = 10f,
                        buryDepth = 1.5f,
                        approachDistance = 3f,
                        sizeX = 2,
                        sizeY = 4,
                        woodCost = 40,
                        oreCost = 20,
                        foodCost = 0,
                        populationProvided = 0
                    });
                }

                bm.EditorSetBuildings(defs);
                EditorUtility.SetDirty(bm);
            }

            EditorUtility.DisplayDialog("Success",
                "Wheat: Assets/Resources/Prefabs/Wheat.prefab\n" +
                "Garden bed wired with plant points + wheat prefab.\n" +
                "BuildingManager updated with GardenBed entry.",
                "OK");
            Selection.activeObject = wheat;
        }

        private void SetupWarehouseAndBarracks()
        {
            EnsureDirectory("Assets/Prefabs/Buildings");

            GameObject warehousePrefab = CreateBuildingPrefab(
                "Warehouse",
                new Color(0.55f, 0.5f, 0.3f),
                new Vector3(2.5f, 2.2f, 2.5f),
                RTSokak.Buildings.BuildingType.Warehouse);

            GameObject barracksPrefab = CreateBuildingPrefab(
                "Barracks",
                new Color(0.55f, 0.3f, 0.28f),
                new Vector3(3f, 2.4f, 3f),
                RTSokak.Buildings.BuildingType.Barracks);
            EnsureBarracksProductionOnPrefab(barracksPrefab);

            var bm = Object.FindFirstObjectByType<RTSokak.Buildings.BuildingManager>();
            if (bm == null)
            {
                GameObject bmObj = new GameObject("BuildingManager");
                bm = bmObj.AddComponent<RTSokak.Buildings.BuildingManager>();
            }

            bm.EditorUpsertDefinition(new RTSokak.Buildings.BuildingDefinition
            {
                type = RTSokak.Buildings.BuildingType.Warehouse,
                displayName = "Warehouse",
                prefab = warehousePrefab,
                previewHeight = 1.1f,
                buildDuration = 10f,
                buryDepth = 2.5f,
                approachDistance = 4f,
                sizeX = 2,
                sizeY = 2,
                woodCost = 80,
                oreCost = 40,
                foodCost = 0,
                populationProvided = 0,
                maxHealth = 1200
            });

            bm.EditorUpsertDefinition(new RTSokak.Buildings.BuildingDefinition
            {
                type = RTSokak.Buildings.BuildingType.Barracks,
                displayName = "Barracks",
                prefab = barracksPrefab,
                previewHeight = 1.2f,
                buildDuration = 14f,
                buryDepth = 2.8f,
                approachDistance = 5f,
                sizeX = 3,
                sizeY = 3,
                woodCost = 120,
                oreCost = 80,
                foodCost = 0,
                populationProvided = 0,
                maxHealth = 1500
            });
            EditorUtility.SetDirty(bm);

            RegisterSpawnPrefab(warehousePrefab);
            RegisterSpawnPrefab(barracksPrefab);

            var warrior = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/Warrior.prefab");
            if (warrior != null)
                RegisterSpawnPrefab(warrior);

            var nm = Object.FindFirstObjectByType<RTSokak.Network.RTSNetworkManager>();
            if (nm != null && warrior != null)
            {
                var so = new SerializedObject(nm);
                var warriorProp = so.FindProperty("warriorPrefab");
                if (warriorProp != null)
                {
                    warriorProp.objectReferenceValue = warrior;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(nm);
                }
            }

            EditorUtility.DisplayDialog("Success",
                "Warehouse + Barracks prefabs ready.\n" +
                "BuildingManager definitions updated.\n" +
                "WarriorPrefab assigned on NetworkManager (if present).",
                "OK");
        }

        private static void EnsureBarracksProductionOnPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<RTSokak.Buildings.BarracksProduction>() == null)
                root.AddComponent<RTSokak.Buildings.BarracksProduction>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static GameObject CreateWheatPrefabAsset()
        {
            string path = "Assets/Resources/Prefabs/Wheat.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Wheat";
            go.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                Material mat = new Material(shader);
                Color green = new Color(0.25f, 0.75f, 0.2f, 1f);
                if (mat.HasProperty("_UnlitColor"))
                    mat.SetColor("_UnlitColor", green);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", green);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", green);
                renderer.sharedMaterial = mat;
                AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/Wheat.mat");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void WireGardenBedPrefab(GameObject gardenPrefab, GameObject wheatPrefab)
        {
            string path = AssetDatabase.GetAssetPath(gardenPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            var bed = root.GetComponent<RTSokak.Buildings.GardenBed>();
            if (bed == null)
                bed = root.AddComponent<RTSokak.Buildings.GardenBed>();

            // Collect / create empty plant points
            var points = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                string n = child.name.ToLowerInvariant();
                if (n.Contains("empty") || n.Contains("plant"))
                    points.Add(child);
            }

            if (points.Count == 0)
            {
                Vector3[] locals =
                {
                    new Vector3(-0.3f, 0.6f, -0.35f),
                    new Vector3(0.3f, 0.6f, -0.35f),
                    new Vector3(-0.3f, 0.6f, 0f),
                    new Vector3(0.3f, 0.6f, 0f),
                    new Vector3(-0.3f, 0.6f, 0.35f),
                    new Vector3(0.3f, 0.6f, 0.35f)
                };
                for (int i = 0; i < locals.Length; i++)
                {
                    GameObject empty = new GameObject("empty");
                    empty.transform.SetParent(root.transform, false);
                    empty.transform.localPosition = locals[i];
                    points.Add(empty.transform);
                }
            }

            var so = new SerializedObject(bed);
            so.FindProperty("plantPoints").arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                so.FindProperty("plantPoints").GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            if (wheatPrefab != null)
                so.FindProperty("wheatPrefab").objectReferenceValue = wheatPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            var obstacle = root.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.enabled = false;
                obstacle.carving = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureDirectory(string path)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
        }

        private GameObject CreateBuildingPrefab(string name, Color color, Vector3 size, RTSokak.Buildings.BuildingType type)
        {
            string path = $"Assets/Prefabs/Buildings/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                Material mat = new Material(shader);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                renderer.sharedMaterial = mat;
                AssetDatabase.CreateAsset(mat, $"Assets/Prefabs/Buildings/{name}_Mat.mat");
            }

            // Collider already on cube
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box != null)
                box.center = Vector3.zero;

            if (go.GetComponent<Mirror.NetworkIdentity>() == null)
                go.AddComponent<Mirror.NetworkIdentity>();

            var building = go.AddComponent<RTSokak.Buildings.Building>();
            if (go.GetComponent<Outline>() == null)
                go.AddComponent<Outline>();

            var obstacle = go.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null)
                obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private void RegisterSpawnPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            var nm = Object.FindFirstObjectByType<Mirror.NetworkManager>();
            if (nm == null) return;

            if (!nm.spawnPrefabs.Contains(prefab))
            {
                nm.spawnPrefabs.Add(prefab);
                EditorUtility.SetDirty(nm);
            }
        }

        private void CreateOrUpdateResourceHUD()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Transform old = canvas.transform.Find("ResourcePanel");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject panelObj = new GameObject("ResourcePanel");
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(220, 130);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            TMPro.TextMeshProUGUI wood = CreateHudText(panelObj.transform, "WoodText", "Wood: 0", 0);
            TMPro.TextMeshProUGUI ore = CreateHudText(panelObj.transform, "OreText", "Ore: 0", 1);
            TMPro.TextMeshProUGUI food = CreateHudText(panelObj.transform, "FoodText", "Food: 0", 2);
            TMPro.TextMeshProUGUI pop = CreateHudText(panelObj.transform, "PopText", "Pop: 0/0", 3);

            var ui = panelObj.AddComponent<RTSokak.UI.PlayerResourcesUI>();
            SetPrivate(ui, "woodText", wood);
            SetPrivate(ui, "oreText", ore);
            SetPrivate(ui, "foodText", food);
            SetPrivate(ui, "populationText", pop);
            SetPrivate(ui, "panel", panelObj);
        }

        private static TMPro.TextMeshProUGUI CreateHudText(Transform parent, string name, string text, int index)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            float h = 1f / 4f;
            float yMax = 1f - index * h;
            float yMin = yMax - h;
            rt.anchorMin = new Vector2(0, yMin);
            rt.anchorMax = new Vector2(1, yMax);
            rt.offsetMin = new Vector2(10, 2);
            rt.offsetMax = new Vector2(-10, -2);
            var tmp = obj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            return tmp;
        }

        private void CreateSelectionPanelUI()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform old = canvas.transform.Find("SelectionPanel");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject root = new GameObject("SelectionPanel");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0, 12);
            rootRt.sizeDelta = new Vector2(520, 140);
            Image rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            // Worker section
            GameObject workerSec = new GameObject("WorkerSection");
            workerSec.transform.SetParent(root.transform, false);
            Stretch(workerSec);

            Button capitalBtn = CreateUIButton(workerSec.transform, "CapitalButton", "Capital", new Vector2(-150, 30), new Vector2(90, 40));
            Button houseBtn = CreateUIButton(workerSec.transform, "HouseButton", "House", new Vector2(-50, 30), new Vector2(90, 40));
            Button gardenBtn = CreateUIButton(workerSec.transform, "GardenButton", "Garden", new Vector2(50, 30), new Vector2(90, 40));
            Button depositBtn = CreateUIButton(workerSec.transform, "DepositButton", "Deposit", new Vector2(150, 30), new Vector2(90, 40));

            GameObject invObj = new GameObject("InventoryText");
            invObj.transform.SetParent(workerSec.transform, false);
            RectTransform invRt = invObj.AddComponent<RectTransform>();
            invRt.anchorMin = new Vector2(0, 0);
            invRt.anchorMax = new Vector2(1, 0);
            invRt.pivot = new Vector2(0.5f, 0);
            invRt.anchoredPosition = new Vector2(0, 8);
            invRt.sizeDelta = new Vector2(-20, 36);
            var invText = invObj.AddComponent<TMPro.TextMeshProUGUI>();
            invText.fontSize = 14;
            invText.color = Color.white;
            invText.alignment = TMPro.TextAlignmentOptions.Center;
            invText.text = "Inventory: ...";

            // Building section
            GameObject buildSec = new GameObject("BuildingSection");
            buildSec.transform.SetParent(root.transform, false);
            Stretch(buildSec);
            buildSec.SetActive(false);

            GameObject progressGroup = new GameObject("ProgressGroup");
            progressGroup.transform.SetParent(buildSec.transform, false);
            RectTransform pgRt = progressGroup.AddComponent<RectTransform>();
            pgRt.anchorMin = new Vector2(0.1f, 0.45f);
            pgRt.anchorMax = new Vector2(0.75f, 0.7f);
            pgRt.offsetMin = Vector2.zero;
            pgRt.offsetMax = Vector2.zero;

            Image pgBg = progressGroup.AddComponent<Image>();
            pgBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(progressGroup.transform, false);
            RectTransform fillRt = fillObj.AddComponent<RectTransform>();
            Stretch(fillObj);
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.85f, 0.3f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            GameObject pctObj = new GameObject("ProgressText");
            pctObj.transform.SetParent(buildSec.transform, false);
            RectTransform pctRt = pctObj.AddComponent<RectTransform>();
            pctRt.anchorMin = new Vector2(0.1f, 0.72f);
            pctRt.anchorMax = new Vector2(0.75f, 0.92f);
            pctRt.offsetMin = Vector2.zero;
            pctRt.offsetMax = Vector2.zero;
            var pctText = pctObj.AddComponent<TMPro.TextMeshProUGUI>();
            pctText.fontSize = 18;
            pctText.color = Color.white;
            pctText.alignment = TMPro.TextAlignmentOptions.Center;
            pctText.text = "0%";

            Button deleteBtn = CreateUIButton(buildSec.transform, "DeleteButton", "X", new Vector2(190, 20), new Vector2(50, 50));
            var deleteColors = deleteBtn.colors;
            deleteColors.normalColor = new Color(0.8f, 0.2f, 0.2f, 1f);
            deleteBtn.colors = deleteColors;

            // Tooltip
            GameObject tip = new GameObject("BuildTooltip");
            tip.transform.SetParent(canvas.transform, false);
            RectTransform tipRt = tip.AddComponent<RectTransform>();
            tipRt.sizeDelta = new Vector2(160, 110);
            Image tipImg = tip.AddComponent<Image>();
            tipImg.color = new Color(0, 0, 0, 0.9f);
            GameObject tipTextObj = new GameObject("Text");
            tipTextObj.transform.SetParent(tip.transform, false);
            Stretch(tipTextObj);
            var tipText = tipTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            tipText.fontSize = 14;
            tipText.color = Color.white;
            tipText.alignment = TMPro.TextAlignmentOptions.TopLeft;
            tipText.margin = new Vector4(8, 8, 8, 8);
            tip.SetActive(false);

            var panel = root.AddComponent<RTSokak.UI.SelectionPanelUI>();
            SetPrivate(panel, "panelRoot", root);
            SetPrivate(panel, "workerSection", workerSec);
            SetPrivate(panel, "capitalButton", capitalBtn);
            SetPrivate(panel, "houseButton", houseBtn);
            SetPrivate(panel, "gardenButton", gardenBtn);
            SetPrivate(panel, "depositButton", depositBtn);
            SetPrivate(panel, "inventoryText", invText);
            SetPrivate(panel, "buildingSection", buildSec);
            SetPrivate(panel, "progressFill", fillImg);
            SetPrivate(panel, "progressText", pctText);
            SetPrivate(panel, "deleteButton", deleteBtn);
            SetPrivate(panel, "progressGroup", progressGroup);
            SetPrivate(panel, "tooltipRoot", tipRt);
            SetPrivate(panel, "tooltipText", tipText);

            // Важно: объект со скриптом должен оставаться активным
            root.SetActive(true);
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 1f);
            Button btn = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            Stretch(textObj);
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            return btn;
        }

        private static void Stretch(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                UnityEngine.Debug.LogWarning($"Field '{fieldName}' not found on {target.GetType().Name}");
        }

        /// <summary>
        /// Настройка здоровья (Max Health) для всех ресурсных префабов
        /// </summary>
        private void ConfigureResourceHealth()
        {
            UnityEngine.Debug.Log("=== Configuring Resource Health ===");

            // Найти префабы
            string treePath = "Assets/Prefabs/Terrain/Tree.prefab";
            string orePath = "Assets/Prefabs/Terrain/IronOre.prefab";
            string farmPath = "Assets/Prefabs/Terrain/FarmPlot.prefab";

            int configured = 0;

            // Дерево -- 3 удара
            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
            if (treePrefab != null)
            {
                var resource = treePrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    SetPrivate(resource, "maxHealth", 3);
                    SetPrivate(resource, "splitAmountByHits", true);
                    EditorUtility.SetDirty(treePrefab);
                    configured++;
                    UnityEngine.Debug.Log("✓ Tree: Max Health = 3");
                }
            }

            // Руда -- 5 ударов
            GameObject orePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(orePath);
            if (orePrefab != null)
            {
                var resource = orePrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    SetPrivate(resource, "maxHealth", 5);
                    SetPrivate(resource, "splitAmountByHits", true);
                    EditorUtility.SetDirty(orePrefab);
                    configured++;
                    UnityEngine.Debug.Log("✓ Ore: Max Health = 5");
                }
            }

            // Ферма -- 2 удара
            GameObject farmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(farmPath);
            if (farmPrefab != null)
            {
                var resource = farmPrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    SetPrivate(resource, "maxHealth", 2);
                    SetPrivate(resource, "splitAmountByHits", true);
                    EditorUtility.SetDirty(farmPrefab);
                    configured++;
                    UnityEngine.Debug.Log("✓ Farm: Max Health = 2");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"=== Resource Health Configured: {configured} prefabs ===");
            EditorUtility.DisplayDialog("Success",
                $"Configured health for {configured} resource prefabs:\n\n" +
                "• Tree: 3 hits\n" +
                "• Ore: 5 hits\n" +
                "• Farm: 2 hits\n\n" +
                "Resources will now last longer!",
                "OK");
        }

        /// <summary>
        /// Проверка и вывод текущих значений ресурсов из префабов
        /// </summary>
        private void DebugResourceValues()
        {
            UnityEngine.Debug.Log("=== Current Resource Prefab Values ===");

            string treePath = "Assets/Prefabs/Terrain/Tree.prefab";
            string orePath = "Assets/Prefabs/Terrain/IronOre.prefab";
            string farmPath = "Assets/Prefabs/Terrain/FarmPlot.prefab";

            // Дерево
            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
            if (treePrefab != null)
            {
                var resource = treePrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    var amountField = typeof(RTSokak.Resources.Resource).GetField("resourceAmount",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var healthField = typeof(RTSokak.Resources.Resource).GetField("maxHealth",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    int amount = amountField != null ? (int)amountField.GetValue(resource) : -1;
                    int health = healthField != null ? (int)healthField.GetValue(resource) : -1;
                    
                    UnityEngine.Debug.Log($"Tree: Amount={amount}, MaxHealth={health}");
                }
            }

            // Руда
            GameObject orePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(orePath);
            if (orePrefab != null)
            {
                var resource = orePrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    var amountField = typeof(RTSokak.Resources.Resource).GetField("resourceAmount",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var healthField = typeof(RTSokak.Resources.Resource).GetField("maxHealth",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    int amount = amountField != null ? (int)amountField.GetValue(resource) : -1;
                    int health = healthField != null ? (int)healthField.GetValue(resource) : -1;
                    
                    UnityEngine.Debug.Log($"Ore: Amount={amount}, MaxHealth={health}");
                }
            }

            // Ферма
            GameObject farmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(farmPath);
            if (farmPrefab != null)
            {
                var resource = farmPrefab.GetComponent<RTSokak.Resources.Resource>();
                if (resource != null)
                {
                    var amountField = typeof(RTSokak.Resources.Resource).GetField("resourceAmount",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var healthField = typeof(RTSokak.Resources.Resource).GetField("maxHealth",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    int amount = amountField != null ? (int)amountField.GetValue(resource) : -1;
                    int health = healthField != null ? (int)healthField.GetValue(resource) : -1;
                    
                    UnityEngine.Debug.Log($"Farm: Amount={amount}, MaxHealth={health}");
                }
            }

            UnityEngine.Debug.Log("=== End Resource Values ===");
        }

        /// <summary>
        /// Спавнит Archer в runtime (только в Host режиме)
        /// </summary>
        private void SpawnArcher()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", 
                    "This button only works in Play Mode (Host).\nStart the game first.", 
                    "OK");
                return;
            }

            if (!Mirror.NetworkServer.active)
            {
                EditorUtility.DisplayDialog("Error", 
                    "NetworkServer is not active.\nYou must be Host to spawn units.", 
                    "OK");
                return;
            }

            var player = Object.FindFirstObjectByType<RTSokak.Network.RTSPlayer>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "No RTSPlayer found in scene.\nJoin as player first.", 
                    "OK");
                return;
            }

            // Ищем Archer префаб
            string[] guids = AssetDatabase.FindAssets("Archer t:Prefab");
            
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Archer prefab not found!\nCreate Archer prefab first (copy Warrior and add Archer component).", 
                    "OK");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (archerPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Failed to load Archer prefab.", 
                    "OK");
                return;
            }

            // Спавним лучника рядом с камерой
            Vector3 spawnPos = new Vector3(5, 1, 5); // Высота 1 для NavMesh baseOffset
            GameObject archerObj = Object.Instantiate(archerPrefab, spawnPos, Quaternion.identity);
            Mirror.NetworkServer.Spawn(archerObj);

            // Назначаем владельца
            var archer = archerObj.GetComponent<RTSokak.Network.Archer>();
            if (archer != null)
            {
                archer.SetOwner(player.connectionToClient);
                player.AddCurrentPopulation(1);
                UnityEngine.Debug.Log($"[RTSSetupHelper] Spawned Archer at {spawnPos} for player {player.PlayerIndex}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[RTSSetupHelper] Archer component not found on prefab!");
            }
        }
    }
}
