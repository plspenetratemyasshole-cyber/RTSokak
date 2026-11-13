using Mirror;
using UnityEngine;
using TMPro;
using RTSokak.Network;

namespace RTSokak.UI
{
    /// <summary>
    /// HUD: дерево, руда, еда, население (мэра/фракции).
    /// Юнит-игрок видит экономику своего мэра.
    /// </summary>
    public class PlayerResourcesUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI woodText;
        [SerializeField] private TextMeshProUGUI oreText;
        [SerializeField] private TextMeshProUGUI foodText;
        [SerializeField] private TextMeshProUGUI populationText;
        [SerializeField] private GameObject panel;

        private RTSPlayer trackedPlayer;
        private float nextFindPlayerTime;

        private void Start()
        {
            if (panel != null)
                panel.SetActive(true);

            UpdateResourceDisplay(0, 0, 0, 0, 0);
            RefreshTrackedPlayer();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextFindPlayerTime)
                return;

            nextFindPlayerTime = Time.unscaledTime + 0.35f;

            RefreshTrackedPlayer();

            if (panel != null && !panel.activeSelf)
            {
                var lp = RTSPlayer.localPlayer;
                if (lp != null && (lp.Role == PlayerRole.MayorPlaying || lp.Role == PlayerRole.UnitPlaying))
                    panel.SetActive(true);
            }

            if (trackedPlayer != null)
            {
                UpdateResourceDisplay(
                    trackedPlayer.Wood, trackedPlayer.Ore, trackedPlayer.Food,
                    trackedPlayer.CurrentPopulation, trackedPlayer.MaxPopulation);
            }
        }

        private void RefreshTrackedPlayer()
        {
            RTSPlayer desired = ResolveDesiredPlayer();
            if (trackedPlayer != desired)
            {
                if (trackedPlayer != null)
                    trackedPlayer.OnPlayerStatsChanged -= UpdateResourceDisplay;

                trackedPlayer = desired;
                if (trackedPlayer != null)
                {
                    trackedPlayer.OnPlayerStatsChanged += UpdateResourceDisplay;
                    UpdateResourceDisplay(
                        trackedPlayer.Wood, trackedPlayer.Ore, trackedPlayer.Food,
                        trackedPlayer.CurrentPopulation, trackedPlayer.MaxPopulation);
                }
            }
        }

        private RTSPlayer ResolveDesiredPlayer()
        {
            var local = RTSPlayer.localPlayer;
            if (local == null)
            {
                var all = RTSPlayer.AllPlayers;
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p != null && p.isLocalPlayer)
                        return p;
                }
                return null;
            }

            if (local.Role == PlayerRole.MayorPlaying || local.Role == PlayerRole.MayorPlacing)
                return local;

            if (local.Role == PlayerRole.UnitPlaying || local.Role == PlayerRole.UnitWaiting)
            {
                RTSPlayer mayor = FindMayorForUnit(local);
                if (mayor != null)
                    return mayor;
            }

            return local;
        }

        private static RTSPlayer FindMayorForUnit(RTSPlayer local)
        {
            if (local == null) return null;

            string targetOwnerNick = null;
            int targetOwnerIndex = -1;

            if (local.ControlledUnitNetId != 0 &&
                NetworkClient.spawned.TryGetValue(local.ControlledUnitNetId, out NetworkIdentity id) &&
                id != null)
            {
                var unit = id.GetComponent<RTSUnit>();
                if (unit != null)
                {
                    targetOwnerNick = unit.OwnerNickname;
                    targetOwnerIndex = unit.OwnerPlayerIndex;
                }
            }

            RTSPlayer fallbackMayor = null;
            var all = RTSPlayer.AllPlayers;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || p.Role != PlayerRole.MayorPlaying)
                    continue;

                if (!string.IsNullOrEmpty(targetOwnerNick) &&
                    string.Equals(p.Nickname, targetOwnerNick, System.StringComparison.OrdinalIgnoreCase))
                    return p;

                if (targetOwnerIndex >= 0 && p.PlayerIndex == targetOwnerIndex)
                    return p;

                if (fallbackMayor == null)
                    fallbackMayor = p;
            }

            return fallbackMayor;
        }

        private void UpdateResourceDisplay(int wood, int ore, int food, int pop, int maxPop)
        {
            if (woodText != null) woodText.text = $"Wood: {wood}";
            if (oreText != null) oreText.text = $"Ore: {ore}";
            if (foodText != null) foodText.text = $"Food: {food}";
            if (populationText != null) populationText.text = $"Pop: {pop}/{maxPop}";
        }

        private void OnDestroy()
        {
            if (trackedPlayer != null)
                trackedPlayer.OnPlayerStatsChanged -= UpdateResourceDisplay;
        }
    }
}
