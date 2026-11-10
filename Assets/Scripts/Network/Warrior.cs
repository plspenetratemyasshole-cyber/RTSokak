using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace RTSokak.Network
{
    /// <summary>
    /// Воин: только движение и ближний бой (без добычи / стройки / инвентаря).
    /// </summary>
    public class Warrior : RTSUnit
    {
        [Header("Warrior")]
        [SerializeField] private string displayName = "Warrior";

        private RTSokak.UI.UnitNameplate nameplate;

        public override bool CanDoEconomyTasks => false;
        public override bool HasInventory => false;

        public override void OnStartClient()
        {
            base.OnStartClient();
            CreateNameplate();
            ForceUpdateOutline();
        }

        protected override void OnServerCancelTasksForMove()
        {
            // У воина нет экономики — только сброс боя уже делает base.CmdMoveTo
        }

        [Server]
        public override void ServerApplyHotbarSlot(int slotIndex)
        {
            playerHotbarSlot = slotIndex;
            EquipSword(slotIndex == 0);
        }

        private void CreateNameplate()
        {
            GameObject nameplateObj = new GameObject("Nameplate");
            nameplate = nameplateObj.AddComponent<RTSokak.UI.UnitNameplate>();

            Canvas canvas = nameplateObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CanvasScaler scaler = nameplateObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(nameplateObj.transform);

            TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 36;
            text.color = Color.white;

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200, 50);
            textRect.localPosition = Vector3.zero;

            var textField = typeof(RTSokak.UI.UnitNameplate).GetField("professionText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            textField?.SetValue(nameplate, text);

            var canvasField = typeof(RTSokak.UI.UnitNameplate).GetField("canvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            canvasField?.SetValue(nameplate, canvas);

            var textSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("textSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            textSizeField?.SetValue(nameplate, 0.02f);

            var fontSizeField = typeof(RTSokak.UI.UnitNameplate).GetField("fontSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fontSizeField?.SetValue(nameplate, 18f);

            var awakeMethod = typeof(RTSokak.UI.UnitNameplate).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awakeMethod?.Invoke(nameplate, null);

            nameplate.SetTarget(transform);
            nameplate.UpdateProfession(displayName);
        }

        protected override void OnDestroy()
        {
            if (nameplate != null)
            {
                Destroy(nameplate.gameObject);
                nameplate = null;
            }
            base.OnDestroy();
        }
    }
}
