using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RTSokak.Camera;
using RTSokak.Network;

namespace RTSokak.UI
{
    /// <summary>
    /// UI выбора роли (Mayor / Unit), размещения поселения и ожидания convert.
    /// Создаётся через RTSokak → UI → Create Player Role UI.
    /// </summary>
    public class PlayerRoleUI : MonoBehaviour
    {
        public static PlayerRoleUI Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject choosePanel;
        [SerializeField] private GameObject mayorPlacePanel;
        [SerializeField] private GameObject unitWaitPanel;

        [Header("Choose")]
        [SerializeField] private Button chooseMayorButton;
        [SerializeField] private Button chooseUnitButton;

        [Header("Mayor Place")]
        [SerializeField] private TextMeshProUGUI mayorPlaceHint;
        [SerializeField] private Button backFromPlaceButton;

        [Header("Unit Wait")]
        [SerializeField] private TextMeshProUGUI unitWaitHint;
        [SerializeField] private Button backFromWaitButton;

        private RTSPlayer boundPlayer;
        private PlayerRole lastRole = (PlayerRole)255;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (chooseMayorButton != null)
                chooseMayorButton.onClick.AddListener(() => boundPlayer?.CmdChooseMayorRole());
            if (chooseUnitButton != null)
                chooseUnitButton.onClick.AddListener(() => boundPlayer?.CmdChooseUnitRole());
            if (backFromPlaceButton != null)
                backFromPlaceButton.onClick.AddListener(() => boundPlayer?.CmdBackToRoleChoice());
            if (backFromWaitButton != null)
                backFromWaitButton.onClick.AddListener(() => boundPlayer?.CmdBackToRoleChoice());

            if (mayorPlaceHint != null && string.IsNullOrEmpty(mayorPlaceHint.text))
                mayorPlaceHint.text = "Choose a place for your settlement, then press ENTER to begin";
            if (unitWaitHint != null && string.IsNullOrEmpty(unitWaitHint.text))
                unitWaitHint.text = "Waiting for a mayor to assign you a unit.\nYou can spectate the world in the meantime.";

            // Place/Wait: фон не должен блокировать миникарту и клики по миру
            MakeOverlayPassThrough(mayorPlacePanel);
            MakeOverlayPassThrough(unitWaitPanel);

            HideAll();
        }

        static void MakeOverlayPassThrough(GameObject panel)
        {
            if (panel == null) return;
            var img = panel.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (boundPlayer == null || !boundPlayer.isLocalPlayer)
            {
                if (RTSPlayer.localPlayer != null)
                    Bind(RTSPlayer.localPlayer);
                else
                    return;
            }

            if (boundPlayer.Role != lastRole)
                ApplyRole(boundPlayer.Role);

            // Mayor placing: Enter confirms camera look-at / position as settlement
            if (boundPlayer.Role == PlayerRole.MayorPlacing && Input.GetKeyDown(KeyCode.Return))
                ConfirmSettlementFromCamera();
        }

        public static void NotifyLocalRoleChanged(RTSPlayer player, PlayerRole role)
        {
            if (Instance == null || player == null || !player.isLocalPlayer)
                return;
            Instance.Bind(player);
            Instance.ApplyRole(role);
        }

        public static void NotifyLocalControlledUnitChanged(RTSPlayer player, uint unitNetId)
        {
            if (Instance == null || player == null || !player.isLocalPlayer)
                return;
            Instance.ApplyRole(player.Role);
            Instance.SyncUnitPlayerController(unitNetId);
        }

        private void Bind(RTSPlayer player)
        {
            boundPlayer = player;
        }

        private void ApplyRole(PlayerRole role)
        {
            lastRole = role;
            HideAll();

            switch (role)
            {
                case PlayerRole.Choosing:
                    if (choosePanel != null) choosePanel.SetActive(true);
                    SetRtsInputEnabled(false);
                    UnlockCursor();
                    break;
                case PlayerRole.MayorPlacing:
                    if (mayorPlacePanel != null) mayorPlacePanel.SetActive(true);
                    SetRtsInputEnabled(false); // только камера + миникарта
                    EnableRtsCamera(true);
                    UnlockCursor();
                    break;
                case PlayerRole.MayorPlaying:
                    SetRtsInputEnabled(true);
                    EnableRtsCamera(true);
                    UnlockCursor();
                    break;
                case PlayerRole.UnitWaiting:
                    if (unitWaitPanel != null) unitWaitPanel.SetActive(true);
                    SetRtsInputEnabled(false); // spectate: камера/миникарта есть, приказов нет
                    EnableRtsCamera(true);
                    UnlockCursor();
                    RefreshWorldOutlines();
                    break;
                case PlayerRole.UnitPlaying:
                    SetRtsInputEnabled(false);
                    SyncUnitPlayerController(boundPlayer != null ? boundPlayer.ControlledUnitNetId : 0);
                    UnitPlayerController.RefreshFactionOutlines();
                    break;
            }

            if (role != PlayerRole.UnitPlaying)
                DetachUnitPlayerController();
        }

        private void SyncUnitPlayerController(uint unitNetId)
        {
            if (boundPlayer == null || boundPlayer.Role != PlayerRole.UnitPlaying || unitNetId == 0)
            {
                DetachUnitPlayerController();
                return;
            }

            if (!NetworkClient.spawned.TryGetValue(unitNetId, out NetworkIdentity id))
            {
                DetachUnitPlayerController();
                return;
            }

            RTSUnit unit = id.GetComponent<RTSUnit>();
            if (unit == null || !unit.IsAlive)
            {
                DetachUnitPlayerController();
                return;
            }

            EnsureUnitPlayerController().AttachToUnit(unit);
        }

        private static void DetachUnitPlayerController()
        {
            if (UnitPlayerController.Instance != null)
                UnitPlayerController.Instance.Detach();
        }

        private static UnitPlayerController EnsureUnitPlayerController()
        {
            if (UnitPlayerController.Instance != null)
                return UnitPlayerController.Instance;

            GameObject go = new GameObject("UnitPlayerController");
            return go.AddComponent<UnitPlayerController>();
        }

        private void ConfirmSettlementFromCamera()
        {
            if (boundPlayer == null)
                return;

            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            Vector3 pos;
            if (cam != null)
            {
                cam.GetView(out pos, out _, out _);
                // Центр экрана на земле
                var unityCam = cam.GetComponent<UnityEngine.Camera>();
                if (unityCam != null)
                {
                    Ray ray = unityCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Mathf.Abs(ray.direction.y) > 0.0001f)
                    {
                        float t = -ray.origin.y / ray.direction.y;
                        if (t > 0f)
                            pos = ray.origin + ray.direction * t;
                    }
                }
            }
            else
            {
                pos = Vector3.zero;
            }

            boundPlayer.CmdConfirmSettlement(pos);
        }

        private void HideAll()
        {
            if (choosePanel != null) choosePanel.SetActive(false);
            if (mayorPlacePanel != null) mayorPlacePanel.SetActive(false);
            if (unitWaitPanel != null) unitWaitPanel.SetActive(false);
        }

        private static void UnlockCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static void SetRtsInputEnabled(bool enabled)
        {
            var sel = UnitSelectionManager.Instance;
            if (sel != null)
                sel.enabled = enabled;

            var panel = Object.FindFirstObjectByType<SelectionPanelUI>();
            if (panel != null)
                panel.enabled = enabled;
        }

        private static void EnableRtsCamera(bool enabled)
        {
            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            if (cam == null)
                return;
            cam.enabled = enabled;
            var c = cam.GetComponent<UnityEngine.Camera>();
            if (c != null)
            {
                c.enabled = enabled;
                if (enabled)
                    c.orthographic = true;
            }
        }

        private static void RefreshWorldOutlines()
        {
            foreach (var u in Object.FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
                u?.ForceUpdateOutline();
            foreach (var b in Object.FindObjectsByType<RTSokak.Buildings.Building>(FindObjectsSortMode.None))
                b?.ForceUpdateOutline();
        }

        /// <summary>Список игроков в UnitWaiting (для convert UI мэра).</summary>
        public static List<RTSPlayer> GetWaitingUnitPlayers()
        {
            var list = new List<RTSPlayer>();
            foreach (var p in Object.FindObjectsByType<RTSPlayer>(FindObjectsSortMode.None))
            {
                if (p != null && p.Role == PlayerRole.UnitWaiting)
                    list.Add(p);
            }
            return list;
        }
    }
}
