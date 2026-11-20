using Mirror;
using UnityEngine;
using UnityEngine.UI;
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

        private UIValuePunch woodPunch;
        private UIValuePunch orePunch;
        private UIValuePunch foodPunch;
        private UIValuePunch popPunch;

        private static readonly Color SpendColor = new Color(1f, 0.42f, 0.42f, 1f);
        private static readonly Color WoodGainColor = new Color(0.91f, 0.66f, 0.35f, 1f);
        private static readonly Color OreGainColor = new Color(0.61f, 0.72f, 0.85f, 1f);
        private static readonly Color FoodGainColor = new Color(0.55f, 0.88f, 0.42f, 1f);
        private static readonly Color PopGainColor = new Color(0.24f, 0.86f, 0.50f, 1f);

        private void Start()
        {
            if (panel != null)
            {
                panel.SetActive(true);
                var img = panel.GetComponent<Image>();
                if (img != null)
                    UIStyleHelper.ApplyRoundedCard(img, UIThemePalette.PanelDark, UIThemePalette.BorderSubtle, 12);
            }

            EnsurePunchComponents();
            UpdateResourceDisplay(0, 0, 0, 0, 0);
            RefreshTrackedPlayer();
        }

        private void EnsurePunchComponents()
        {
            if (woodText != null && woodPunch == null)
                woodPunch = woodText.GetComponent<UIValuePunch>() ?? woodText.gameObject.AddComponent<UIValuePunch>();
            if (oreText != null && orePunch == null)
                orePunch = oreText.GetComponent<UIValuePunch>() ?? oreText.gameObject.AddComponent<UIValuePunch>();
            if (foodText != null && foodPunch == null)
                foodPunch = foodText.GetComponent<UIValuePunch>() ?? foodText.gameObject.AddComponent<UIValuePunch>();
            if (populationText != null && popPunch == null)
                popPunch = populationText.GetComponent<UIValuePunch>() ?? populationText.gameObject.AddComponent<UIValuePunch>();
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

        private int lastWood = -1;
        private int lastOre = -1;
        private int lastFood = -1;
        private int lastPop = -1;
        private int lastMaxPop = -1;

        private void UpdateResourceDisplay(int wood, int ore, int food, int pop, int maxPop)
        {
            if (wood == lastWood && ore == lastOre && food == lastFood && pop == lastPop && maxPop == lastMaxPop)
                return;

            EnsurePunchComponents();

            if (lastWood >= 0)
            {
                if (wood > lastWood) woodPunch?.Punch(WoodGainColor);
                else if (wood < lastWood) woodPunch?.Punch(SpendColor);

                if (ore > lastOre) orePunch?.Punch(OreGainColor);
                else if (ore < lastOre) orePunch?.Punch(SpendColor);

                if (food > lastFood) foodPunch?.Punch(FoodGainColor);
                else if (food < lastFood) foodPunch?.Punch(SpendColor);

                if (pop > lastPop) popPunch?.Punch(PopGainColor);
                else if (pop < lastPop) popPunch?.Punch(SpendColor);
            }

            lastWood = wood;
            lastOre = ore;
            lastFood = food;
            lastPop = pop;
            lastMaxPop = maxPop;

            if (woodText != null) woodText.text = $"<color=#E8A85A>Wood:</color> {wood}";
            if (oreText != null) oreText.text = $"<color=#9BB8D9>Ore:</color> {ore}";
            if (foodText != null) foodText.text = $"<color=#8CE06C>Food:</color> {food}";
            if (populationText != null) populationText.text = $"<color=#EAEFF5>Pop:</color> {pop}/{maxPop}";
        }

        private void OnDestroy()
        {
            if (trackedPlayer != null)
                trackedPlayer.OnPlayerStatsChanged -= UpdateResourceDisplay;
        }
    }
}
