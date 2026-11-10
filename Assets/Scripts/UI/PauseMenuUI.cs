using Mirror;
using RTSokak.Network;
using RTSokak.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTSokak.UI
{
    /// <summary>
    /// ESC pause menu. Wire UI via Tools → RTS Setup → Setup Pause Menu UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuUI : MonoBehaviour
    {
        const string SensPrefKey = "UnitPlayerMouseSensitivity";

        [Header("Roots")]
        [SerializeField] GameObject pauseRoot;
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject saveSuccessPanel;
        [SerializeField] GameObject exitConfirmPanel;

        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button openToLanButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button saveAndExitButton;
        [SerializeField] Button exitButton;
        [SerializeField] Button saveSuccessCloseButton;
        [SerializeField] Button confirmExitButton;
        [SerializeField] Button cancelExitButton;

        [Header("Unit Player Sensitivity")]
        [SerializeField] GameObject sensitivitySection;
        [SerializeField] Slider sensitivitySlider;
        [SerializeField] TMP_InputField sensitivityInput;

        bool isOpen;
        bool lanAdvertised;
        bool wiringSens;

        void Awake()
        {
            WireButtons();
            EnsureSensitivityUi();
            SetOpen(false);
        }

        void WireButtons()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (openToLanButton != null) openToLanButton.onClick.AddListener(OnOpenToLan);
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);
            if (saveAndExitButton != null) saveAndExitButton.onClick.AddListener(OnSaveAndExit);
            if (exitButton != null) exitButton.onClick.AddListener(OnExit);
            if (saveSuccessCloseButton != null)
                saveSuccessCloseButton.onClick.AddListener(() =>
                {
                    if (saveSuccessPanel != null) saveSuccessPanel.SetActive(false);
                    if (mainPanel != null) mainPanel.SetActive(true);
                });
            if (confirmExitButton != null) confirmExitButton.onClick.AddListener(ExitToMenu);
            if (cancelExitButton != null)
                cancelExitButton.onClick.AddListener(() =>
                {
                    if (exitConfirmPanel != null) exitConfirmPanel.SetActive(false);
                    if (mainPanel != null) mainPanel.SetActive(true);
                });
        }

        void Update()
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                if (isOpen) SetOpen(false);
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (exitConfirmPanel != null && exitConfirmPanel.activeSelf)
            {
                exitConfirmPanel.SetActive(false);
                if (mainPanel != null) mainPanel.SetActive(true);
                return;
            }

            if (saveSuccessPanel != null && saveSuccessPanel.activeSelf)
            {
                saveSuccessPanel.SetActive(false);
                if (mainPanel != null) mainPanel.SetActive(true);
                return;
            }

            SetOpen(!isOpen);
        }

        void SetOpen(bool open)
        {
            isOpen = open;
            if (pauseRoot != null)
                pauseRoot.SetActive(open);

            if (!open)
                return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            var nm = RTSNetworkManager.Singleton;
            lanAdvertised = nm != null && nm.IsLanAdvertised;
            bool host = NetworkServer.active;

            if (openToLanButton != null)
                openToLanButton.gameObject.SetActive(host && !lanAdvertised);
            if (saveButton != null)
                saveButton.gameObject.SetActive(host);
            if (saveAndExitButton != null)
                saveAndExitButton.gameObject.SetActive(host);

            if (mainPanel != null) mainPanel.SetActive(true);
            if (saveSuccessPanel != null) saveSuccessPanel.SetActive(false);
            if (exitConfirmPanel != null) exitConfirmPanel.SetActive(false);

            RefreshSensitivitySection();
        }

        void EnsureSensitivityUi()
        {
            if (sensitivitySection != null)
            {
                WireSensitivityControls();
                return;
            }

            if (mainPanel == null)
                return;

            sensitivitySection = new GameObject("SensitivitySection", typeof(RectTransform));
            sensitivitySection.transform.SetParent(mainPanel.transform, false);
            var rt = sensitivitySection.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -210f);
            rt.sizeDelta = new Vector2(360f, 56f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(sensitivitySection.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0.55f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Mouse sensitivity";
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(sensitivitySection.transform, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0.72f, 0.5f);
            srt.offsetMin = new Vector2(8f, 4f);
            srt.offsetMax = new Vector2(-4f, 0f);
            sensitivitySlider = sliderGo.GetComponent<Slider>();
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 10f;
            // Minimal background
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderGo.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f, 1f);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(16f, 0f);
            handle.GetComponent<Image>().color = Color.white;
            sensitivitySlider.fillRect = fill.GetComponent<RectTransform>();
            sensitivitySlider.handleRect = handle.GetComponent<RectTransform>();
            sensitivitySlider.targetGraphic = handle.GetComponent<Image>();

            GameObject inputGo = new GameObject("SensInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(sensitivitySection.transform, false);
            var irt = inputGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.74f, 0f);
            irt.anchorMax = new Vector2(1f, 0.5f);
            irt.offsetMin = new Vector2(4f, 4f);
            irt.offsetMax = new Vector2(-8f, 0f);
            inputGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            sensitivityInput = inputGo.GetComponent<TMP_InputField>();
            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            Stretch(textArea.GetComponent<RectTransform>());
            var text = new GameObject("Text", typeof(RectTransform));
            text.transform.SetParent(textArea.transform, false);
            Stretch(text.GetComponent<RectTransform>());
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            sensitivityInput.textViewport = textArea.GetComponent<RectTransform>();
            sensitivityInput.textComponent = tmp;
            sensitivityInput.contentType = TMP_InputField.ContentType.DecimalNumber;

            WireSensitivityControls();
            sensitivitySection.SetActive(false);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void WireSensitivityControls()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(OnSensitivitySlider);
                sensitivitySlider.onValueChanged.AddListener(OnSensitivitySlider);
            }
            if (sensitivityInput != null)
            {
                sensitivityInput.onEndEdit.RemoveListener(OnSensitivityInput);
                sensitivityInput.onEndEdit.AddListener(OnSensitivityInput);
            }
        }

        void RefreshSensitivitySection()
        {
            EnsureSensitivityUi();
            bool unitPlaying = RTSPlayer.localPlayer != null &&
                               RTSPlayer.localPlayer.Role == PlayerRole.UnitPlaying;
            if (sensitivitySection != null)
                sensitivitySection.SetActive(unitPlaying);
            if (!unitPlaying)
                return;

            float sens = PlayerPrefs.GetFloat(SensPrefKey, 2f);
            if (UnitPlayerController.Instance != null)
                sens = UnitPlayerController.Instance.MouseSensitivity;

            wiringSens = true;
            if (sensitivitySlider != null)
                sensitivitySlider.SetValueWithoutNotify(sens);
            if (sensitivityInput != null)
                sensitivityInput.SetTextWithoutNotify(sens.ToString("0.##"));
            wiringSens = false;
        }

        void OnSensitivitySlider(float value)
        {
            if (wiringSens) return;
            ApplySensitivity(value);
        }

        void OnSensitivityInput(string text)
        {
            if (wiringSens) return;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) &&
                !float.TryParse(text, out v))
                return;
            ApplySensitivity(v);
        }

        void ApplySensitivity(float value)
        {
            value = Mathf.Clamp(value, 0.05f, 20f);
            PlayerPrefs.SetFloat(SensPrefKey, value);
            if (UnitPlayerController.Instance != null)
                UnitPlayerController.Instance.MouseSensitivity = value;
            wiringSens = true;
            if (sensitivitySlider != null)
                sensitivitySlider.SetValueWithoutNotify(value);
            if (sensitivityInput != null)
                sensitivityInput.SetTextWithoutNotify(value.ToString("0.##"));
            wiringSens = false;
        }

        void OnResume()
        {
            SetOpen(false);
            // Вернуть lock курсора юниту-игроку
            if (RTSPlayer.localPlayer != null &&
                RTSPlayer.localPlayer.Role == PlayerRole.UnitPlaying &&
                UnitPlayerController.Instance != null &&
                UnitPlayerController.Instance.IsActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        void OnOpenToLan()
        {
            RTSNetworkManager.Singleton?.OpenToLan();
            lanAdvertised = true;
            if (openToLanButton != null)
                openToLanButton.gameObject.SetActive(false);
        }

        void OnSave()
        {
            if (!NetworkServer.active)
                return;
            if (!WorldSnapshotService.TrySaveActiveWorld(out string error))
            {
                UnityEngine.Debug.LogWarning($"[Pause] Save failed: {error}");
                return;
            }

            if (mainPanel != null) mainPanel.SetActive(false);
            if (saveSuccessPanel != null) saveSuccessPanel.SetActive(true);
        }

        void OnSaveAndExit()
        {
            if (NetworkServer.active)
                WorldSnapshotService.TrySaveActiveWorld(out _);
            ExitToMenu();
        }

        void OnExit()
        {
            if (NetworkServer.active && WorldSession.HasActiveWorld)
            {
                if (mainPanel != null) mainPanel.SetActive(false);
                if (exitConfirmPanel != null) exitConfirmPanel.SetActive(true);
                return;
            }

            ExitToMenu();
        }

        void ExitToMenu()
        {
            SetOpen(false);
            var nm = RTSNetworkManager.Singleton ?? FindFirstObjectByType<RTSNetworkManager>();
            if (nm == null)
                return;

            if (NetworkServer.active && NetworkClient.active)
                nm.StopHost();
            else if (NetworkServer.active)
                nm.StopServer();
            else if (NetworkClient.active)
                nm.StopClient();

            WorldSession.Clear();
        }
    }
}
