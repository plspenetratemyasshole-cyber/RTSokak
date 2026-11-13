using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RTSokak.UI;

namespace RTSokak.EditorTools
{
    public static class UnitPlayerHudSetupMenu
    {
        [MenuItem("RTSokak/UI/Create Unit Player HUD")]
        public static void CreateUnitPlayerHud()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Unit Player HUD", "В сцене нет Canvas.", "OK");
                return;
            }

            Transform existing = canvas.transform.Find("UnitPlayerHUD");
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                EditorUtility.DisplayDialog("Unit Player HUD", "Уже есть на Canvas.", "OK");
                return;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "EventSystem");
            }

            GameObject root = new GameObject("UnitPlayerHUD", typeof(RectTransform), typeof(UnitPlayerHud));
            Undo.RegisterCreatedObjectUndo(root, "Create Unit Player HUD");
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            GameObject hudRoot = new GameObject("HudRoot", typeof(RectTransform));
            hudRoot.transform.SetParent(root.transform, false);
            Stretch(hudRoot.GetComponent<RectTransform>());

            // Crosshair
            GameObject cross = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cross.transform.SetParent(hudRoot.transform, false);
            var crt = cross.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(4f, 4f);
            var crossImg = cross.GetComponent<Image>();
            crossImg.sprite = GetWhiteSprite();
            crossImg.color = new Color(1f, 1f, 1f, 0.85f);
            crossImg.raycastTarget = false;

            // Resources above health bar
            GameObject resGo = new GameObject("ResourcesText", typeof(RectTransform));
            resGo.transform.SetParent(hudRoot.transform, false);
            var rrt = resGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0f);
            rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 124f);
            rrt.sizeDelta = new Vector2(480f, 28f);
            var resText = resGo.AddComponent<TextMeshProUGUI>();
            resText.text = "Wood 0   Ore 0   Food 0";
            resText.fontSize = 18;
            resText.alignment = TextAlignmentOptions.Center;
            resText.color = Color.white;
            resText.raycastTarget = false;

            // Health bar above hotbar
            GameObject hbGo = new GameObject("HealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hbGo.transform.SetParent(hudRoot.transform, false);
            var hbRt = hbGo.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0f);
            hbRt.anchorMax = new Vector2(0.5f, 0f);
            hbRt.pivot = new Vector2(0.5f, 0f);
            hbRt.anchoredPosition = new Vector2(0f, 92f);
            hbRt.sizeDelta = new Vector2(260f, 26f);
            var bgImg = hbGo.GetComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 0.88f);
            bgImg.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(hbGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = GetWhiteSprite();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.2f, 0.85f, 0.3f, 1f);
            fillImg.raycastTarget = false;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(hbGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var hpText = textGo.AddComponent<TextMeshProUGUI>();
            hpText.fontSize = 15;
            hpText.fontStyle = FontStyles.Bold;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.color = Color.white;
            hpText.raycastTarget = false;
            hpText.text = "HP: 100 / 100";

            // Hotbar root
            GameObject hotbar = new GameObject("Hotbar", typeof(RectTransform));
            hotbar.transform.SetParent(hudRoot.transform, false);
            var hrt = hotbar.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 24f);
            hrt.sizeDelta = new Vector2(400f, 64f);

            var ui = root.GetComponent<UnitPlayerHud>();
            var so = new SerializedObject(ui);
            so.FindProperty("hudRoot").objectReferenceValue = hudRoot;
            so.FindProperty("crosshair").objectReferenceValue = cross;
            so.FindProperty("hotbarRoot").objectReferenceValue = hotbar.GetComponent<RectTransform>();
            so.FindProperty("resourcesText").objectReferenceValue = resText;
            so.FindProperty("healthBarRoot").objectReferenceValue = hbRt;
            so.FindProperty("healthFillImage").objectReferenceValue = fillImg;
            so.FindProperty("healthText").objectReferenceValue = hpText;
            so.ApplyModifiedPropertiesWithoutUndo();

            hudRoot.SetActive(false);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            UnityEngine.Debug.Log("[RTSokak] Unit Player HUD created on Canvas.");
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite GetWhiteSprite()
        {
            Texture2D tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}
