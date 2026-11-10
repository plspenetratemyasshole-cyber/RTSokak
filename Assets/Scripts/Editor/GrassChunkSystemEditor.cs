using UnityEngine;
using UnityEditor;

namespace RTSokak.Grass
{
    [CustomEditor(typeof(GrassChunkSystem))]
    public class GrassChunkSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GrassChunkSystem system = (GrassChunkSystem)target;

            // Заголовок
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grass Chunk System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Система чанков покрывает всю карту травой с LOD оптимизацией", MessageType.Info);
            EditorGUILayout.Space();

            // Стандартные поля
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Кнопка генерации
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Chunks", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Generate Chunks", 
                    "Создать чанки травы по всей карте?\n\n(Старые чанки будут удалены)", 
                    "Generate", "Cancel"))
                {
                    system.RegenerateChunks();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Кнопка очистки
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear All Chunks", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear Chunks", 
                    "Удалить все чанки травы?", 
                    "Delete", "Cancel"))
                {
                    system.ClearChunks();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Кнопка обновления настроек
            if (GUILayout.Button("Update Grass Settings", GUILayout.Height(30)))
            {
                system.UpdateSettings();
                UnityEngine.Debug.Log("[GrassChunkSystem] Настройки обновлены");
            }

            EditorGUILayout.Space();

            // Предупреждения
            SerializedProperty prefabProp = serializedObject.FindProperty("grassPrefab");
            SerializedProperty materialProp = serializedObject.FindProperty("grassMaterial");

            if (prefabProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ Grass Prefab не назначен! Перетащи GroundPlane.fbx", MessageType.Warning);
            }

            if (materialProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ Grass Material не назначен! Перетащи GeometryGrass.mat", MessageType.Warning);
            }

            SerializedProperty tessellationProp = serializedObject.FindProperty("tessellation");
            if (tessellationProp.floatValue > 15f)
            {
                EditorGUILayout.HelpBox("⚠️ Tessellation >15 может снизить FPS!", MessageType.Warning);
            }

            SerializedProperty heightProp = serializedObject.FindProperty("bladeHeight");
            if (heightProp.floatValue > 0.5f)
            {
                EditorGUILayout.HelpBox("⚠️ Blade Height >0.5 — трава будет слишком высокой!", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
