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
            cross.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            cross.GetComponent<Image>().raycastTarget = false;

            // Resources above hotbar
            GameObject resGo = new GameObject("Resources", typeof(RectTransform));
            resGo.transform.SetParent(hudRoot.transform, false);
            var rrt = resGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0f);
            rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 88f);
            rrt.sizeDelta = new Vector2(480f, 28f);
            var resText = resGo.AddComponent<TextMeshProUGUI>();
            resText.text = "Wood 0   Ore 0   Food 0";
            resText.fontSize = 18;
            resText.alignment = TextAlignmentOptions.Center;
            resText.color = Color.white;
            resText.raycastTarget = false;

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
    }
}
