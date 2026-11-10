using System.Collections;
using System.Collections.Generic;
using RTSokak.Graphics;
using RTSokak.Network;
using RTSokak.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTSokak.UI
{
    /// <summary>
    /// Main menu controller. UI must live in the scene — use Tools → RTS Setup → Setup Main Menu UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuUI : MonoBehaviour
    {
        readonly Dictionary<long, RTSServerResponse> discoveredServers = new Dictionary<long, RTSServerResponse>();
        readonly List<PublicServerEntry> publicServers = new List<PublicServerEntry>();
        readonly HashSet<string> publicServerKeys = new HashSet<string>();
        readonly List<GameObject> worldEntryObjects = new List<GameObject>();
        readonly List<GameObject> serverEntryObjects = new List<GameObject>();

        [Header("Panels")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject singlePlayerPanel;
        [SerializeField] GameObject networkPanel;
        [SerializeField] GameObject createWorldPanel;
        [SerializeField] GameObject deleteConfirmPanel;
        [SerializeField] GameObject settingsPanel;

        [Header("Main Panel")]
        [SerializeField] Button singlePlayerButton;
        [SerializeField] Button networkGameButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("Single Player")]
        [SerializeField] Button singlePlayerBackButton;
        [SerializeField] Transform worldListContent;
        [SerializeField] GameObject worldEntryTemplate;
        [SerializeField] Button worldPlayButton;
        [SerializeField] Button worldDeleteButton;
        [SerializeField] Button openSavesFolderButton;
        [SerializeField] Button createNewWorldButton;
        [SerializeField] TextMeshProUGUI emptyWorldsLabel;

        [Header("Create World")]
        [SerializeField] TMP_InputField worldNameInput;
        [SerializeField] Button createWorldContinueButton;
        [SerializeField] Button createWorldBackButton;

        [Header("Delete Confirm")]
        [SerializeField] Button confirmDeleteWorldButton;
        [SerializeField] Button cancelDeleteWorldButton;

        [Header("Network")]
        [SerializeField] TextMeshProUGUI networkStatusText;
        [SerializeField] Transform serverListContent;
        [SerializeField] GameObject serverEntryTemplate;
        [SerializeField] Button refreshServersButton;
        [SerializeField] TMP_InputField ipInput;
        [SerializeField] Button connectDirectlyButton;
        [SerializeField] Button networkBackButton;
        [SerializeField] string publicServersUrl = PublicServerList.DefaultUrl;

        [Header("Settings — Network")]
        [SerializeField] TMP_InputField settingsPortInput;
        [SerializeField] TMP_Dropdown settingsNetworkDropdown;
        [SerializeField] TMP_Dropdown settingsTransportDropdown;
        [SerializeField] Button settingsApplyButton;
        [SerializeField] Button settingsBackButton;

        [Header("Settings — Graphics")]
        [SerializeField] TMP_Dropdown settingsFpsDropdown;
        [SerializeField] TMP_Dropdown settingsShadowQualityDropdown;
        [SerializeField] TMP_Dropdown settingsAaDropdown;
        [SerializeField] Slider settingsShadowDistanceSlider;
        [SerializeField] Slider settingsRenderScaleSlider;
        [SerializeField] Toggle settingsVsyncToggle;
        [SerializeField] Toggle settingsContactShadowsToggle;
        [SerializeField] Toggle settingsSsrToggle;
        [SerializeField] Toggle settingsMotionBlurToggle;
        [SerializeField] Toggle settingsBloomHqToggle;
        [SerializeField] TextMeshProUGUI settingsShadowDistLabel;

        [Header("Networking")]
        [SerializeField] RTSNetworkManager networkManager;

        RTSNetworkDiscovery networkDiscovery;
        string selectedWorldName;
        List<NetworkAdapterChoice> settingsNetworkChoices;
        Coroutine refreshServersRoutine;
        readonly Color worldEntryNormal = new Color(0.12f, 0.14f, 0.12f, 0.9f);
        readonly Color worldEntrySelected = new Color(0.25f, 0.45f, 0.3f, 0.95f);

        void Awake()
        {
            RTSPlayerSession.Load();  // Загрузить nickname, email, token из PlayerPrefs
            WorldSession.Clear();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (networkManager == null)
                networkManager = RTSNetworkManager.Singleton ?? FindFirstObjectByType<RTSNetworkManager>();
            networkDiscovery = networkManager != null
                ? networkManager.GetComponent<RTSNetworkDiscovery>()
                : FindFirstObjectByType<RTSNetworkDiscovery>();

            if (worldEntryTemplate != null)
                worldEntryTemplate.SetActive(false);
            if (serverEntryTemplate != null)
                serverEntryTemplate.SetActive(false);

            WireButtons();
            ShowMainPanel();
        }

        void OnEnable()
        {
            if (networkDiscovery != null)
                networkDiscovery.OnServerFound.AddListener(OnServerFound);
        }

        void OnDisable()
        {
            if (networkDiscovery != null)
                networkDiscovery.OnServerFound.RemoveListener(OnServerFound);
            if (refreshServersRoutine != null)
                StopCoroutine(refreshServersRoutine);
        }

        void WireButtons()
        {
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(ShowSinglePlayerPanel);
            if (networkGameButton != null)
                networkGameButton.onClick.AddListener(ShowNetworkPanel);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettingsPanel);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (singlePlayerBackButton != null)
                singlePlayerBackButton.onClick.AddListener(ShowMainPanel);
            if (worldPlayButton != null)
                worldPlayButton.onClick.AddListener(OnPlayWorldClicked);
            if (worldDeleteButton != null)
                worldDeleteButton.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrWhiteSpace(selectedWorldName) && deleteConfirmPanel != null)
                        deleteConfirmPanel.SetActive(true);
                });
            if (openSavesFolderButton != null)
                openSavesFolderButton.onClick.AddListener(WorldSaveService.OpenSavesFolder);
            if (createNewWorldButton != null)
                createNewWorldButton.onClick.AddListener(ShowCreateWorldPanel);

            if (createWorldContinueButton != null)
                createWorldContinueButton.onClick.AddListener(OnCreateWorldConfirm);
            if (createWorldBackButton != null)
                createWorldBackButton.onClick.AddListener(ShowSinglePlayerPanel);

            if (confirmDeleteWorldButton != null)
                confirmDeleteWorldButton.onClick.AddListener(OnConfirmDeleteWorld);
            if (cancelDeleteWorldButton != null)
                cancelDeleteWorldButton.onClick.AddListener(() =>
                {
                    if (deleteConfirmPanel != null)
                        deleteConfirmPanel.SetActive(false);
                });

            if (refreshServersButton != null)
                refreshServersButton.onClick.AddListener(RefreshServerList);
            if (connectDirectlyButton != null)
                connectDirectlyButton.onClick.AddListener(OnConnectDirect);
            if (networkBackButton != null)
                networkBackButton.onClick.AddListener(ShowMainPanel);

            if (settingsApplyButton != null)
                settingsApplyButton.onClick.AddListener(() =>
                {
                    ApplySettingsFromUi();
                    ShowMainPanel();
                });
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(() =>
                {
                    ApplySettingsFromUi();
                    ShowMainPanel();
                });

            if (settingsShadowDistanceSlider != null)
                settingsShadowDistanceSlider.onValueChanged.AddListener(UpdateShadowDistLabel);
        }

        void ShowMainPanel() => SetOnly(mainPanel);

        void ShowSinglePlayerPanel()
        {
            SetOnly(singlePlayerPanel);
            RefreshWorldList();
        }

        void ShowNetworkPanel()
        {
            NetworkTransportConfig.Instance?.ApplyFromSession();
            SetOnly(networkPanel);
            RefreshServerList();
        }

        void ShowSettingsPanel()
        {
            SetOnly(settingsPanel);
            PopulateSettingsFields();
        }

        void ShowCreateWorldPanel()
        {
            SetOnly(createWorldPanel);
            if (worldNameInput != null)
                worldNameInput.text = string.Empty;
        }

        void SetOnly(GameObject panel)
        {
            if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
            if (singlePlayerPanel != null) singlePlayerPanel.SetActive(panel == singlePlayerPanel);
            if (networkPanel != null) networkPanel.SetActive(panel == networkPanel);
            if (createWorldPanel != null) createWorldPanel.SetActive(panel == createWorldPanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        }

        void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void RefreshWorldList()
        {
            ClearSpawned(worldEntryObjects);
            IReadOnlyList<string> worlds = WorldSaveService.ListWorlds();
            if (emptyWorldsLabel != null)
                emptyWorldsLabel.gameObject.SetActive(worlds.Count == 0);

            for (int i = 0; i < worlds.Count; i++)
            {
                string worldName = worlds[i];
                GameObject entry = SpawnEntry(worldEntryTemplate, worldListContent, worldName, () =>
                {
                    selectedWorldName = worldName;
                    HighlightWorldEntries(worldName);
                });
                if (entry != null)
                    worldEntryObjects.Add(entry);
            }
        }

        void HighlightWorldEntries(string selected)
        {
            for (int i = 0; i < worldEntryObjects.Count; i++)
            {
                var img = worldEntryObjects[i].GetComponent<Image>();
                var label = worldEntryObjects[i].GetComponentInChildren<TextMeshProUGUI>();
                bool on = label != null && label.text == selected;
                if (img != null)
                    img.color = on ? worldEntrySelected : worldEntryNormal;
            }
        }

        void OnPlayWorldClicked()
        {
            if (string.IsNullOrWhiteSpace(selectedWorldName))
                return;
            UnityEngine.Debug.Log($"[MainMenu] Playing world: '{selectedWorldName}'");
            WorldSession.SelectExistingWorld(selectedWorldName);
            StartHostGame();
        }

        void OnCreateWorldConfirm()
        {
            string name = worldNameInput != null ? worldNameInput.text : string.Empty;
            if (!WorldSaveService.TryCreateWorld(name, out string error))
            {
                UnityEngine.Debug.LogWarning($"[MainMenu] Create world failed: {error}");
                return;
            }

            WorldSession.BeginCreateWorld(name);
            StartHostGame();
        }

        void OnConfirmDeleteWorld()
        {
            if (string.IsNullOrWhiteSpace(selectedWorldName))
                return;
            WorldSaveService.TryDeleteWorld(selectedWorldName, out _);
            selectedWorldName = null;
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);
            RefreshWorldList();
        }

        void StartHostGame()
        {
            if (networkManager == null)
                networkManager = RTSNetworkManager.Singleton ?? FindFirstObjectByType<RTSNetworkManager>();
            if (networkManager == null)
            {
                UnityEngine.Debug.LogError("[MainMenu] RTSNetworkManager missing.");
                return;
            }

            networkManager.StartSinglePlayerHost();
        }

        void RefreshServerList()
        {
            discoveredServers.Clear();
            publicServers.Clear();
            publicServerKeys.Clear();
            ClearSpawned(serverEntryObjects);
            SetNetworkStatus("Searching…");

            networkDiscovery?.StopDiscovery();
            networkDiscovery?.StartDiscovery();

            if (refreshServersRoutine != null)
                StopCoroutine(refreshServersRoutine);
            refreshServersRoutine = StartCoroutine(FetchPublicServersRoutine());
        }

        IEnumerator FetchPublicServersRoutine()
        {
            yield return PublicServerList.Fetch(publicServersUrl,
                list =>
                {
                    publicServers.Clear();
                    publicServers.AddRange(list);
                    RebuildServerListUi();
                },
                err => SetNetworkStatus($"Public list: {err}"));
        }

        void OnServerFound(RTSServerResponse response)
        {
            if (response.serverId == 0 && response.uri == null)
                return;
            discoveredServers[response.serverId] = response;
            RebuildServerListUi();
        }

        void RebuildServerListUi()
        {
            ClearSpawned(serverEntryObjects);
            int count = 0;

            foreach (var kv in discoveredServers)
            {
                RTSServerResponse s = kv.Value;
                string label = string.IsNullOrWhiteSpace(s.serverName) ? "LAN Server" : s.serverName;
                string host = s.uri != null ? $"{s.uri.Host}:{s.uri.Port}" : "?";
                GameObject entry = SpawnEntry(serverEntryTemplate, serverListContent, $"[LAN] {label}  {host}", () =>
                {
                    UnityEngine.Debug.Log($"[MainMenu] LAN server button clicked: {label} at {host}");
                    if (s.uri != null && networkManager != null)
                    {
                        UnityEngine.Debug.Log($"[MainMenu] Calling StartLanClient with uri: {s.uri}");
                        networkManager.StartLanClient(s.uri);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"[MainMenu] Cannot connect: uri={s.uri}, networkManager={networkManager}");
                    }
                });
                if (entry != null)
                    serverEntryObjects.Add(entry);
                count++;
            }

            for (int i = 0; i < publicServers.Count; i++)
            {
                PublicServerEntry pub = publicServers[i];
                if (pub == null || string.IsNullOrWhiteSpace(pub.address))
                    continue;
                string key = pub.address.ToLowerInvariant();
                if (!publicServerKeys.Add(key))
                    continue;

                GameObject entry = SpawnEntry(serverEntryTemplate, serverListContent,
                    $"[Public] {pub.name}  {pub.address}", () =>
                    {
                        UnityEngine.Debug.Log($"[MainMenu] Public server button clicked: {pub.name} at {pub.address}");
                        if (PublicServerList.TryParseAddress(pub.address, out string host, out ushort port) &&
                            networkManager != null)
                        {
                            UnityEngine.Debug.Log($"[MainMenu] Calling StartDirectClient with host={host}, port={port}");
                            networkManager.StartDirectClient(host, port);
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"[MainMenu] Cannot parse address or networkManager is null");
                        }
                    });
                if (entry != null)
                    serverEntryObjects.Add(entry);
                count++;
            }

            SetNetworkStatus(count == 0
                ? "No servers found. Refresh or connect by IP."
                : $"Found {count} server(s).");
        }

        void SetNetworkStatus(string text)
        {
            if (networkStatusText != null)
                networkStatusText.text = text;
        }

        void OnConnectDirect()
        {
            string raw = ipInput != null ? ipInput.text : string.Empty;
            if (!PublicServerList.TryParseAddress(raw, out string host, out ushort port))
            {
                SetNetworkStatus("Invalid address.");
                return;
            }

            if (networkManager != null)
                networkManager.StartDirectClient(host, port);
        }

        void PopulateSettingsFields()
        {
            if (settingsPortInput != null)
                settingsPortInput.text = PlayerSession.Port.ToString();

            settingsNetworkChoices = NetworkAdapterUtility.GetIPv4Choices();
            if (settingsNetworkDropdown != null)
            {
                settingsNetworkDropdown.ClearOptions();
                var options = new List<string> { "Auto (any)" };
                int selected = 0;
                for (int i = 0; i < settingsNetworkChoices.Count; i++)
                {
                    options.Add(settingsNetworkChoices[i].DisplayName);
                    if (!string.IsNullOrEmpty(PlayerSession.LanInterfaceIPv4) &&
                        PlayerSession.LanInterfaceIPv4 == settingsNetworkChoices[i].IPv4)
                        selected = i + 1;
                }
                settingsNetworkDropdown.AddOptions(options);
                settingsNetworkDropdown.SetValueWithoutNotify(selected);
                settingsNetworkDropdown.RefreshShownValue();
            }

            if (settingsTransportDropdown != null)
            {
                settingsTransportDropdown.ClearOptions();
                settingsTransportDropdown.AddOptions(new List<string> { "KCP (UDP)", "Telepathy (TCP)" });
                settingsTransportDropdown.SetValueWithoutNotify(PlayerSession.UseTcpTransport ? 1 : 0);
                settingsTransportDropdown.RefreshShownValue();
            }

            if (settingsFpsDropdown != null)
            {
                settingsFpsDropdown.ClearOptions();
                settingsFpsDropdown.AddOptions(new List<string> { "60", "120", "144", "Unlimited" });
                int fpsIdx = GraphicsSettingsStore.TargetFps switch
                {
                    60 => 0,
                    144 => 2,
                    <= 0 => 3,
                    _ => 1
                };
                settingsFpsDropdown.SetValueWithoutNotify(fpsIdx);
                settingsFpsDropdown.RefreshShownValue();
            }

            if (settingsShadowQualityDropdown != null)
            {
                settingsShadowQualityDropdown.ClearOptions();
                settingsShadowQualityDropdown.AddOptions(new List<string> { "Low", "Medium", "High", "Ultra" });
                settingsShadowQualityDropdown.SetValueWithoutNotify(GraphicsSettingsStore.ShadowQuality);
                settingsShadowQualityDropdown.RefreshShownValue();
            }

            if (settingsAaDropdown != null)
            {
                settingsAaDropdown.ClearOptions();
                settingsAaDropdown.AddOptions(new List<string> { "Off", "2x MSAA", "4x MSAA" });
                settingsAaDropdown.SetValueWithoutNotify(GraphicsSettingsStore.AntiAliasing);
                settingsAaDropdown.RefreshShownValue();
            }

            if (settingsShadowDistanceSlider != null)
            {
                settingsShadowDistanceSlider.SetValueWithoutNotify(GraphicsSettingsStore.ShadowDistance);
                UpdateShadowDistLabel(GraphicsSettingsStore.ShadowDistance);
            }

            if (settingsRenderScaleSlider != null)
                settingsRenderScaleSlider.SetValueWithoutNotify(GraphicsSettingsStore.RenderScale);

            if (settingsVsyncToggle != null)
                settingsVsyncToggle.SetIsOnWithoutNotify(GraphicsSettingsStore.VSync);
            if (settingsContactShadowsToggle != null)
                settingsContactShadowsToggle.SetIsOnWithoutNotify(GraphicsSettingsStore.ContactShadows);
            if (settingsSsrToggle != null)
                settingsSsrToggle.SetIsOnWithoutNotify(GraphicsSettingsStore.ScreenSpaceReflections);
            if (settingsMotionBlurToggle != null)
                settingsMotionBlurToggle.SetIsOnWithoutNotify(GraphicsSettingsStore.MotionBlur);
            if (settingsBloomHqToggle != null)
                settingsBloomHqToggle.SetIsOnWithoutNotify(GraphicsSettingsStore.BloomHighQuality);
        }

        void ApplySettingsFromUi()
        {
            if (settingsPortInput != null)
            {
                if (!PlayerSession.TrySetPort(settingsPortInput.text, out _))
                    PlayerSession.SetPort(PlayerSession.DefaultPort);
                settingsPortInput.text = PlayerSession.Port.ToString();
            }

            if (settingsNetworkDropdown != null && settingsNetworkChoices != null)
            {
                if (settingsNetworkDropdown.value <= 0)
                    PlayerSession.SetLanInterfaceIPv4(string.Empty);
                else
                {
                    int idx = settingsNetworkDropdown.value - 1;
                    if (idx >= 0 && idx < settingsNetworkChoices.Count)
                        PlayerSession.SetLanInterfaceIPv4(settingsNetworkChoices[idx].IPv4);
                }
            }

            if (settingsTransportDropdown != null)
                NetworkTransportConfig.SetUseTcp(settingsTransportDropdown.value == 1);

            if (settingsFpsDropdown != null)
            {
                GraphicsSettingsStore.TargetFps = settingsFpsDropdown.value switch
                {
                    0 => 60,
                    2 => 144,
                    3 => 0,
                    _ => 120
                };
            }

            if (settingsShadowQualityDropdown != null)
            {
                GraphicsSettingsStore.ShadowQuality = settingsShadowQualityDropdown.value;
                GraphicsSettingsStore.CascadeCount = settingsShadowQualityDropdown.value switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    _ => 4
                };
            }

            if (settingsAaDropdown != null)
                GraphicsSettingsStore.AntiAliasing = settingsAaDropdown.value;

            if (settingsShadowDistanceSlider != null)
                GraphicsSettingsStore.ShadowDistance = settingsShadowDistanceSlider.value;
            if (settingsRenderScaleSlider != null)
                GraphicsSettingsStore.RenderScale = settingsRenderScaleSlider.value;

            if (settingsVsyncToggle != null)
                GraphicsSettingsStore.VSync = settingsVsyncToggle.isOn;
            if (settingsContactShadowsToggle != null)
                GraphicsSettingsStore.ContactShadows = settingsContactShadowsToggle.isOn;
            if (settingsSsrToggle != null)
                GraphicsSettingsStore.ScreenSpaceReflections = settingsSsrToggle.isOn;
            if (settingsMotionBlurToggle != null)
                GraphicsSettingsStore.MotionBlur = settingsMotionBlurToggle.isOn;
            if (settingsBloomHqToggle != null)
                GraphicsSettingsStore.BloomHighQuality = settingsBloomHqToggle.isOn;

            GraphicsSettingsStore.ApplyAll();
        }

        void UpdateShadowDistLabel(float value)
        {
            if (settingsShadowDistLabel != null)
                settingsShadowDistLabel.text = $"Shadow distance: {value:0}";
        }

        static void ClearSpawned(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    Destroy(list[i]);
            }
            list.Clear();
        }

        static GameObject SpawnEntry(GameObject template, Transform parent, string text,
            UnityEngine.Events.UnityAction onClick)
        {
            if (template == null || parent == null)
                return null;

            GameObject entry = Instantiate(template, parent);
            entry.SetActive(true);
            entry.name = "Entry";

            var label = entry.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = text;

            var button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }

            return entry;
        }
    }
}
