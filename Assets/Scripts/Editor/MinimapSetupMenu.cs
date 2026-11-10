using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RTSokak.UI;

namespace RTSokak.EditorTools
{
    public static class MinimapSetupMenu
    {
        [MenuItem("RTSokak/UI/Create Minimap (bottom-right)")]
        public static void CreateMinimap()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Minimap", "В сцене нет Canvas.", "OK");
                return;
            }

            Transform existing = canvas.transform.Find("Minimap");
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                EditorUtility.DisplayDialog("Minimap", "Minimap уже есть на Canvas — выделен в Hierarchy.", "OK");
                return;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            GameObject root = new GameObject("Minimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MinimapUI));
            Undo.RegisterCreatedObjectUndo(root, "Create Minimap");
            root.transform.SetParent(canvas.transform, false);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            UnityEngine.Debug.Log("[RTSokak] Minimap создан (низ справа). Настройки — на компоненте MinimapUI.");
        }
    }
}
