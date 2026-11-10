#if UNITY_EDITOR
using RTSokak.Network;
using RTSokak.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RTSokak.Editor
{
    /// <summary>
    /// Creates editable Canvas UI in MainMenu / SampleScene and wires SerializeFields.
    /// </summary>
    public static class MenuUiBuilder
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        static readonly Color PanelBg = new Color(0.08f, 0.1f, 0.08f, 0.94f);
        static readonly Color ButtonBg = new Color(0.18f, 0.28f, 0.2f, 1f);
        static readonly Color InputBg = new Color(0.12f, 0.14f, 0.12f, 1f);
        static readonly Color EntryBg = new Color(0.12f, 0.14f, 0.12f, 0.9f);
        static readonly Color TextCol = new Color(0.92f, 0.96f, 0.92f, 1f);
        static readonly Color DialogBg = new Color(0.1f, 0.12f, 0.1f, 0.96f);

        [MenuItem("Tools/RTS Setup/Setup Main Menu UI")]
        public static void SetupMainMenuUi()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            BuildMainMenuUi(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UnityEngine.Debug.Log("[RTSokak] Main Menu UI created in scene. Edit Canvas children freely.");
        }

        [MenuItem("Tools/RTS Setup/Setup Pause Menu UI")]
        public static void SetupPauseMenuUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            BuildPauseMenuUi(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UnityEngine.Debug.Log("[RTSokak] Pause Menu UI created in SampleScene.");
        }

        public static void BuildMainMenuUi(Scene scene)
        {
            EnsureEventSystem();

            // Remove old auto-built canvas / empty MainMenuUI roots.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "MainMenuCanvas" || root.name == "MainMenuUI")
                    Object.DestroyImmediate(root);
            }

            GameObject canvasGo = new GameObject("MainMenuCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Stretch(canvasGo.GetComponent<RectTransform>());

            MainMenuUI menu = canvasGo.AddComponent<MainMenuUI>();
            Transform rootT = canvasGo.transform;

            GameObject mainPanel = FullPanel(rootT, "MainPanel", PanelBg);
            Title(mainPanel.transform, "Title", "RTSokak", 64, new Vector2(0, 220));
            Button spBtn = Btn(mainPanel.transform, "SinglePlayerButton", "Single Player", new Vector2(0, 60));
            Button netBtn = Btn(mainPanel.transform, "NetworkGameButton", "Network Game", new Vector2(0, -20));
            Button setBtn = Btn(mainPanel.transform, "SettingsButton", "Settings", new Vector2(0, -100));
            Button exitBtn = Btn(mainPanel.transform, "ExitButton", "Exit", new Vector2(0, -180));

            GameObject spPanel = FullPanel(rootT, "SinglePlayerPanel", PanelBg);
            Title(spPanel.transform, "Title", "Worlds", 48, new Vector2(0, 300));
            TextMeshProUGUI emptyWorlds = Label(spPanel.transform, "EmptyWorldsLabel", "No worlds yet.", 22, new Vector2(0, 80), new Vector2(500, 40));
            Transform worldContent = Scroll(spPanel.transform, "WorldScrollView", new Vector2(0, 40), new Vector2(560, 320));
            GameObject worldTemplate = EntryTemplate(worldContent, "WorldEntryTemplate");
            Button playBtn = Btn(spPanel.transform, "PlayButton", "Play", new Vector2(-200, -280), new Vector2(180, 44));
            Button delBtn = Btn(spPanel.transform, "DeleteButton", "Delete", new Vector2(0, -280), new Vector2(180, 44));
            Button createBtn = Btn(spPanel.transform, "CreateNewWorldButton", "Create New", new Vector2(200, -280), new Vector2(180, 44));
            Button folderBtn = Btn(spPanel.transform, "OpenSavesFolderButton", "Open Folder", new Vector2(-200, -360), new Vector2(180, 44));
            Button spBack = Btn(spPanel.transform, "BackButton", "Back", new Vector2(200, -360), new Vector2(180, 44));

            GameObject createPanel = FullPanel(rootT, "CreateWorldPanel", PanelBg);
            Title(createPanel.transform, "Title", "Create World", 48, new Vector2(0, 120));
            TMP_InputField worldName = Input(createPanel.transform, "WorldNameInput", "World name", new Vector2(0, 20), new Vector2(420, 44));
            Button createContinue = Btn(createPanel.transform, "ContinueButton", "Create", new Vector2(-120, -80));
            Button createBack = Btn(createPanel.transform, "BackButton", "Back", new Vector2(120, -80));

            GameObject deletePanel = FullPanel(rootT, "DeleteConfirmPanel", new Color(0.12f, 0.04f, 0.04f, 0.95f));
            Title(deletePanel.transform, "Title", "Delete this world?", 36, new Vector2(0, 60));
            Button confirmDel = Btn(deletePanel.transform, "ConfirmDeleteButton", "Delete", new Vector2(-120, -40));
            Button cancelDel = Btn(deletePanel.transform, "CancelButton", "Cancel", new Vector2(120, -40));
            deletePanel.SetActive(false);

            GameObject netPanel = FullPanel(rootT, "NetworkPanel", PanelBg);
            Title(netPanel.transform, "Title", "Network", 48, new Vector2(0, 320));
            TextMeshProUGUI status = Label(netPanel.transform, "StatusText", "…", 20, new Vector2(0, 250), new Vector2(800, 36));
            Transform serverContent = Scroll(netPanel.transform, "ServerScrollView", new Vector2(0, 40), new Vector2(720, 300));
            GameObject serverTemplate = EntryTemplate(serverContent, "ServerEntryTemplate");
            TMP_InputField ip = Input(netPanel.transform, "IPInput", "ip:port", new Vector2(-80, -220), new Vector2(360, 44));
            Button connect = Btn(netPanel.transform, "ConnectDirectlyButton", "Connect", new Vector2(220, -220), new Vector2(160, 44));
            Button refresh = Btn(netPanel.transform, "RefreshButton", "Refresh", new Vector2(-200, -300));
            Button netBack = Btn(netPanel.transform, "BackButton", "Back", new Vector2(200, -300));

            GameObject settingsPanel = FullPanel(rootT, "SettingsPanel", PanelBg);
            Title(settingsPanel.transform, "Title", "Settings", 48, new Vector2(0, 400));
            float y = 320f;
            TMP_InputField port = LabeledInput(settingsPanel.transform, "PortInput", "Port", ref y);
            TMP_Dropdown networkDd = LabeledDropdown(settingsPanel.transform, "NetworkDropdown", "Network", ref y);
            TMP_Dropdown transportDd = LabeledDropdown(settingsPanel.transform, "TransportDropdown", "Transport", ref y);
            Label(settingsPanel.transform, "GraphicsHeader", "Graphics", 28, new Vector2(0, y), new Vector2(400, 36));
            y -= 50f;
            TMP_Dropdown fpsDd = LabeledDropdown(settingsPanel.transform, "FPSDropdown", "FPS limit", ref y);
            Toggle vsync = LabeledToggle(settingsPanel.transform, "VsyncToggle", "VSync", ref y);
            TMP_Dropdown shadowQ = LabeledDropdown(settingsPanel.transform, "ShadowQualityDropdown", "Shadow quality", ref y);
            TextMeshProUGUI shadowDistLabel;
            Slider shadowDist = LabeledSlider(settingsPanel.transform, "ShadowDistanceSlider", "Shadow distance", 50f, 400f, 200f, ref y, out shadowDistLabel);
            TMP_Dropdown aaDd = LabeledDropdown(settingsPanel.transform, "AADropdown", "Anti-aliasing", ref y);
            Slider renderScale = LabeledSlider(settingsPanel.transform, "RenderScaleSlider", "Render scale", 0.5f, 1.5f, 1f, ref y, out _);
            Toggle contact = LabeledToggle(settingsPanel.transform, "ContactShadowsToggle", "Contact shadows", ref y);
            Toggle ssr = LabeledToggle(settingsPanel.transform, "SSRToggle", "SSR", ref y);
            Toggle motion = LabeledToggle(settingsPanel.transform, "MotionBlurToggle", "Motion blur", ref y);
            Toggle bloom = LabeledToggle(settingsPanel.transform, "BloomHQToggle", "Bloom HQ", ref y);
            Button apply = Btn(settingsPanel.transform, "ApplyButton", "Apply", new Vector2(-120, -420));
            Button setBack = Btn(settingsPanel.transform, "BackButton", "Back", new Vector2(120, -420));

            // Default visibility
            mainPanel.SetActive(true);
            spPanel.SetActive(false);
            createPanel.SetActive(false);
            netPanel.SetActive(false);
            settingsPanel.SetActive(false);

            SerializedObject so = new SerializedObject(menu);
            Set(so, "mainPanel", mainPanel);
            Set(so, "singlePlayerPanel", spPanel);
            Set(so, "networkPanel", netPanel);
            Set(so, "createWorldPanel", createPanel);
            Set(so, "deleteConfirmPanel", deletePanel);
            Set(so, "settingsPanel", settingsPanel);

            Set(so, "singlePlayerButton", spBtn);
            Set(so, "networkGameButton", netBtn);
            Set(so, "settingsButton", setBtn);
            Set(so, "exitButton", exitBtn);

            Set(so, "singlePlayerBackButton", spBack);
            Set(so, "worldListContent", worldContent);
            Set(so, "worldEntryTemplate", worldTemplate);
            Set(so, "worldPlayButton", playBtn);
            Set(so, "worldDeleteButton", delBtn);
            Set(so, "openSavesFolderButton", folderBtn);
            Set(so, "createNewWorldButton", createBtn);
            Set(so, "emptyWorldsLabel", emptyWorlds);

            Set(so, "worldNameInput", worldName);
            Set(so, "createWorldContinueButton", createContinue);
            Set(so, "createWorldBackButton", createBack);

            Set(so, "confirmDeleteWorldButton", confirmDel);
            Set(so, "cancelDeleteWorldButton", cancelDel);

            Set(so, "networkStatusText", status);
            Set(so, "serverListContent", serverContent);
            Set(so, "serverEntryTemplate", serverTemplate);
            Set(so, "refreshServersButton", refresh);
            Set(so, "ipInput", ip);
            Set(so, "connectDirectlyButton", connect);
            Set(so, "networkBackButton", netBack);

            Set(so, "settingsPortInput", port);
            Set(so, "settingsNetworkDropdown", networkDd);
            Set(so, "settingsTransportDropdown", transportDd);
            Set(so, "settingsApplyButton", apply);
            Set(so, "settingsBackButton", setBack);

            Set(so, "settingsFpsDropdown", fpsDd);
            Set(so, "settingsShadowQualityDropdown", shadowQ);
            Set(so, "settingsAaDropdown", aaDd);
            Set(so, "settingsShadowDistanceSlider", shadowDist);
            Set(so, "settingsRenderScaleSlider", renderScale);
            Set(so, "settingsVsyncToggle", vsync);
            Set(so, "settingsContactShadowsToggle", contact);
            Set(so, "settingsSsrToggle", ssr);
            Set(so, "settingsMotionBlurToggle", motion);
            Set(so, "settingsBloomHqToggle", bloom);
            Set(so, "settingsShadowDistLabel", shadowDistLabel);

            RTSNetworkManager nm = Object.FindFirstObjectByType<RTSNetworkManager>();
            Set(so, "networkManager", nm);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
        }

        public static void BuildPauseMenuUi(Scene scene)
        {
            EnsureEventSystem();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "PauseMenuCanvas" || root.name == "PauseMenuUI")
                    Object.DestroyImmediate(root);
            }

            GameObject canvasGo = new GameObject("PauseMenuCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Stretch(canvasGo.GetComponent<RectTransform>());

            PauseMenuUI pause = canvasGo.AddComponent<PauseMenuUI>();

            GameObject pauseRoot = FullPanel(canvasGo.transform, "PauseRoot", new Color(0, 0, 0, 0.55f));
            GameObject mainPanel = CenterBox(pauseRoot.transform, "MainPanel", new Vector2(420, 420), DialogBg);
            Title(mainPanel.transform, "Title", "Paused", 40, new Vector2(0, 160));
            Button resume = Btn(mainPanel.transform, "ResumeButton", "Resume", new Vector2(0, 80), new Vector2(260, 44));
            Button openLan = Btn(mainPanel.transform, "OpenToLanButton", "Open to LAN", new Vector2(0, 20), new Vector2(260, 44));
            Button save = Btn(mainPanel.transform, "SaveButton", "Save", new Vector2(0, -40), new Vector2(260, 44));
            Button saveExit = Btn(mainPanel.transform, "SaveAndExitButton", "Save and Exit", new Vector2(0, -100), new Vector2(260, 44));
            Button exit = Btn(mainPanel.transform, "ExitButton", "Exit", new Vector2(0, -160), new Vector2(260, 44));

            GameObject saveOk = CenterBox(pauseRoot.transform, "SaveSuccessPanel", new Vector2(420, 220), DialogBg);
            Title(saveOk.transform, "Title", "World saved.", 32, new Vector2(0, 40));
            Button saveClose = Btn(saveOk.transform, "CloseButton", "OK", new Vector2(0, -40), new Vector2(180, 44));
            saveOk.SetActive(false);

            GameObject exitConfirm = CenterBox(pauseRoot.transform, "ExitConfirmPanel", new Vector2(420, 220), DialogBg);
            Title(exitConfirm.transform, "Title", "Exit without saving?", 28, new Vector2(0, 50));
            Button confirmExit = Btn(exitConfirm.transform, "ConfirmExitButton", "Exit", new Vector2(-120, -40), new Vector2(160, 44));
            Button cancelExit = Btn(exitConfirm.transform, "CancelExitButton", "Cancel", new Vector2(120, -40), new Vector2(160, 44));
            exitConfirm.SetActive(false);

            pauseRoot.SetActive(false);

            SerializedObject so = new SerializedObject(pause);
            Set(so, "pauseRoot", pauseRoot);
            Set(so, "mainPanel", mainPanel);
            Set(so, "saveSuccessPanel", saveOk);
            Set(so, "exitConfirmPanel", exitConfirm);
            Set(so, "resumeButton", resume);
            Set(so, "openToLanButton", openLan);
            Set(so, "saveButton", save);
            Set(so, "saveAndExitButton", saveExit);
            Set(so, "exitButton", exit);
            Set(so, "saveSuccessCloseButton", saveClose);
            Set(so, "confirmExitButton", confirmExit);
            Set(so, "cancelExitButton", cancelExit);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pause);
        }

        // ---- helpers ----

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static void Set(SerializedObject so, string name, Object value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
                p.objectReferenceValue = value;
            else
                UnityEngine.Debug.LogWarning($"[MenuUiBuilder] Missing field: {name}");
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject FullPanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject CenterBox(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static TextMeshProUGUI Title(Transform parent, string name, string text, int size, Vector2 pos)
        {
            return Label(parent, name, text, size, pos, new Vector2(800, size + 20));
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 rectSize,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rectSize;
            rt.anchoredPosition = pos;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = TextCol;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button Btn(Transform parent, string name, string text, Vector2 pos, Vector2? size = null)
        {
            Vector2 s = size ?? new Vector2(220, 48);
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = s;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = ButtonBg;
            TextMeshProUGUI label = Label(go.transform, "Text", text, 22, Vector2.zero, s);
            Stretch(label.rectTransform);
            return go.GetComponent<Button>();
        }

        static TMP_InputField Input(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = InputBg;

            GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            RectTransform areaRt = area.GetComponent<RectTransform>();
            Stretch(areaRt);
            areaRt.offsetMin = new Vector2(10, 4);
            areaRt.offsetMax = new Vector2(-10, -4);

            TextMeshProUGUI text = Label(area.transform, "Text", string.Empty, 20, Vector2.zero, size, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            TextMeshProUGUI ph = Label(area.transform, "Placeholder", placeholder, 20, Vector2.zero, size, TextAlignmentOptions.MidlineLeft);
            Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(1, 1, 1, 0.35f);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.textViewport = areaRt;
            input.textComponent = text;
            input.placeholder = ph;
            return input;
        }

        static TMP_Dropdown Dropdown(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = InputBg;

            TextMeshProUGUI caption = Label(go.transform, "Label", "Option", 18, Vector2.zero, size, TextAlignmentOptions.MidlineLeft);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(12, 0);
            caption.rectTransform.offsetMax = new Vector2(-28, 0);

            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            template.SetActive(false);
            RectTransform trt = template.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 150);
            template.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.1f, 1f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            viewport.GetComponent<Image>().color = Color.white;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 28);

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            RectTransform irt = item.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(1, 0.5f);
            irt.sizeDelta = new Vector2(0, 28);

            GameObject itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            Stretch(itemBg.GetComponent<RectTransform>());
            itemBg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.15f, 1f);

            GameObject check = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(item.transform, false);
            RectTransform checkRt = check.GetComponent<RectTransform>();
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(0, 0.5f);
            checkRt.sizeDelta = new Vector2(14, 14);
            checkRt.anchoredPosition = new Vector2(12, 0);
            check.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f, 1f);

            TextMeshProUGUI itemLabel = Label(item.transform, "Item Label", "Option", 18, Vector2.zero, size, TextAlignmentOptions.MidlineLeft);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(28, 1);
            itemLabel.rectTransform.offsetMax = new Vector2(-8, -1);

            Toggle toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();

            ScrollRect sr = template.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.horizontal = false;

            TMP_Dropdown dd = go.AddComponent<TMP_Dropdown>();
            dd.template = trt;
            dd.captionText = caption;
            dd.itemText = itemLabel;
            dd.targetGraphic = go.GetComponent<Image>();
            return dd;
        }

        static Toggle Toggle(Transform parent, string name, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(36, 36);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.2f, 1f);

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(go.transform, false);
            Stretch(check.GetComponent<RectTransform>());
            check.GetComponent<RectTransform>().offsetMin = new Vector2(4, 4);
            check.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -4);
            check.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f, 1f);

            Toggle t = go.GetComponent<Toggle>();
            t.targetGraphic = go.GetComponent<Image>();
            t.graphic = check.GetComponent<Image>();
            return t;
        }

        static Slider SliderCtrl(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float value)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            RectTransform bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = new Vector2(0, 0.25f);
            bgrt.anchorMax = new Vector2(1, 0.75f);
            bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.15f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fart = fillArea.GetComponent<RectTransform>();
            fart.anchorMin = new Vector2(0, 0.25f);
            fart.anchorMax = new Vector2(1, 0.75f);
            fart.offsetMin = new Vector2(5, 0);
            fart.offsetMax = new Vector2(-15, 0);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f, 1f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 0);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        static Transform Scroll(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            RectTransform srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = size;
            srt.anchoredPosition = pos;
            scrollGo.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.05f, 0.85f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            viewport.GetComponent<Image>().color = Color.white;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 4;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = crt;
            sr.horizontal = false;
            return content.transform;
        }

        static GameObject EntryTemplate(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 42;
            go.GetComponent<Image>().color = EntryBg;
            TextMeshProUGUI label = Label(go.transform, "Label", "Entry", 18, Vector2.zero, new Vector2(100, 40), TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12, 0);
            label.rectTransform.offsetMax = new Vector2(-12, 0);
            go.SetActive(false);
            return go;
        }

        static TMP_InputField LabeledInput(Transform parent, string name, string label, ref float y)
        {
            Label(parent, name + "Label", label, 18, new Vector2(-260, y), new Vector2(240, 30), TextAlignmentOptions.MidlineRight);
            TMP_InputField input = Input(parent, name, label, new Vector2(80, y), new Vector2(360, 40));
            y -= 55f;
            return input;
        }

        static TMP_Dropdown LabeledDropdown(Transform parent, string name, string label, ref float y)
        {
            Label(parent, name + "Label", label, 18, new Vector2(-260, y), new Vector2(240, 30), TextAlignmentOptions.MidlineRight);
            TMP_Dropdown dd = Dropdown(parent, name, new Vector2(80, y), new Vector2(360, 40));
            y -= 55f;
            return dd;
        }

        static Toggle LabeledToggle(Transform parent, string name, string label, ref float y)
        {
            Label(parent, name + "Label", label, 18, new Vector2(-260, y), new Vector2(240, 30), TextAlignmentOptions.MidlineRight);
            Toggle t = Toggle(parent, name, new Vector2(-40, y));
            y -= 48f;
            return t;
        }

        static Slider LabeledSlider(Transform parent, string name, string label, float min, float max, float value, ref float y, out TextMeshProUGUI valueLabel)
        {
            valueLabel = Label(parent, name + "Label", label, 18, new Vector2(-260, y), new Vector2(240, 30), TextAlignmentOptions.MidlineRight);
            Slider s = SliderCtrl(parent, name, new Vector2(80, y), new Vector2(360, 24), min, max, value);
            y -= 55f;
            return s;
        }
    }
}
#endif
