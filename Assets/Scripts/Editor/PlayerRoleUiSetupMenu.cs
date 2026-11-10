using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RTSokak.UI;

namespace RTSokak.EditorTools
{
    public static class PlayerRoleUiSetupMenu
    {
        [MenuItem("RTSokak/UI/Create Player Role UI")]
        public static void CreatePlayerRoleUi()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Player Role UI", "В сцене нет Canvas.", "OK");
                return;
            }

            Transform existing = canvas.transform.Find("PlayerRoleUI");
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                EditorUtility.DisplayDialog("Player Role UI", "Уже есть на Canvas.", "OK");
                return;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "EventSystem");
            }

            GameObject root = new GameObject("PlayerRoleUI", typeof(RectTransform), typeof(PlayerRoleUI));
            Undo.RegisterCreatedObjectUndo(root, "Create Player Role UI");
            root.transform.SetParent(canvas.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // --- Choose ---
            GameObject choose = CreatePanel(root.transform, "ChoosePanel", new Color(0f, 0f, 0f, 0.55f));
            var chooseTitle = CreateText(choose.transform, "Title", "Choose your role", 42, TextAlignmentOptions.Center);
            var chooseTitleRt = chooseTitle.GetComponent<RectTransform>();
            chooseTitleRt.anchorMin = new Vector2(0.1f, 0.7f);
            chooseTitleRt.anchorMax = new Vector2(0.9f, 0.9f);

            var mayorBtn = CreateBigButton(choose.transform, "MayorButton", "Play as Mayor",
                new Vector2(0.5f, 0.48f), new Color(0.2f, 0.55f, 0.35f, 0.95f));
            var unitBtn = CreateBigButton(choose.transform, "UnitButton", "Play as Unit",
                new Vector2(0.5f, 0.28f), new Color(0.35f, 0.4f, 0.55f, 0.95f));

            // --- Mayor Place ---
            GameObject place = CreatePanel(root.transform, "MayorPlacePanel", new Color(0f, 0f, 0f, 0.35f));
            var placeHint = CreateText(place.transform, "Hint",
                "Choose a place for your settlement, then press ENTER to begin",
                28, TextAlignmentOptions.Center);
            var placeHintRt = placeHint.GetComponent<RectTransform>();
            placeHintRt.anchorMin = new Vector2(0.1f, 0.75f);
            placeHintRt.anchorMax = new Vector2(0.9f, 0.95f);
            var backPlace = CreateButton(place.transform, "BackButton", "Back to choice",
                new Vector2(0.5f, 0.12f), new Vector2(220, 44));

            // --- Unit Wait ---
            GameObject wait = CreatePanel(root.transform, "UnitWaitPanel", new Color(0f, 0f, 0f, 0.4f));
            var waitHint = CreateText(wait.transform, "Hint",
                "Waiting for a mayor to assign you a unit.\nYou can spectate the world in the meantime.",
                26, TextAlignmentOptions.Center);
            var waitHintRt = waitHint.GetComponent<RectTransform>();
            waitHintRt.anchorMin = new Vector2(0.1f, 0.7f);
            waitHintRt.anchorMax = new Vector2(0.9f, 0.95f);
            var backWait = CreateButton(wait.transform, "BackButton", "Back to choice",
                new Vector2(0.5f, 0.12f), new Vector2(220, 44));

            var ui = root.GetComponent<PlayerRoleUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("choosePanel").objectReferenceValue = choose;
            so.FindProperty("mayorPlacePanel").objectReferenceValue = place;
            so.FindProperty("unitWaitPanel").objectReferenceValue = wait;
            so.FindProperty("chooseMayorButton").objectReferenceValue = mayorBtn.GetComponent<Button>();
            so.FindProperty("chooseUnitButton").objectReferenceValue = unitBtn.GetComponent<Button>();
            so.FindProperty("mayorPlaceHint").objectReferenceValue = placeHint;
            so.FindProperty("backFromPlaceButton").objectReferenceValue = backPlace.GetComponent<Button>();
            so.FindProperty("unitWaitHint").objectReferenceValue = waitHint;
            so.FindProperty("backFromWaitButton").objectReferenceValue = backWait.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Place/Wait — не блокируют клики (камера, миникарта)
            place.GetComponent<Image>().raycastTarget = false;
            wait.GetComponent<Image>().raycastTarget = false;

            choose.SetActive(false);
            place.SetActive(false);
            wait.SetActive(false);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            UnityEngine.Debug.Log("[RTSokak] Player Role UI created on Canvas.");
        }

        static GameObject CreatePanel(Transform parent, string name, Color bg)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = true;
            return go;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        static GameObject CreateBigButton(Transform parent, string name, string label, Vector2 anchorCenter, Color color)
        {
            GameObject go = CreateButton(parent, name, label, anchorCenter, new Vector2(360, 90));
            go.GetComponent<Image>().color = color;
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.fontSize = 28;
            return go;
        }

        static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchorCenter, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.26f, 0.95f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            StretchFull(textGo.GetComponent<RectTransform>());
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
