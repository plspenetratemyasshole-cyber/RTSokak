using UnityEngine;
using UnityEditor;

namespace RTSokak.Grass
{
    [CustomEditor(typeof(RTSGrassManager))]
    public class RTSGrassManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RTSGrassManager grassManager = (RTSGrassManager)target;

            // Заголовок
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RTS Grass Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Управление травой для RTS игры. Назначь Grass Material из Assets/GrassShader/Materials/", MessageType.Info);
            EditorGUILayout.Space();

            // Отрисовать стандартные поля
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            // Кнопки быстрых настроек
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Low Quality", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Set Low Quality", 
                    "Density: 10\nHeight: 0.3\nLOD: 50m\n\nДля слабых ПК", "Apply", "Cancel"))
                {
                    Undo.RecordObject(grassManager, "Set Low Quality");
                    grassManager.SetGrassQuality("low");
                    EditorUtility.SetDirty(grassManager);
                    UnityEngine.Debug.Log("[RTSGrassManager] Установлено Low Quality");
                }
            }

            if (GUILayout.Button("Medium Quality", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Set Medium Quality", 
                    "Density: 15\nHeight: 0.5\nLOD: 100m\n\nРекомендовано для RTS", "Apply", "Cancel"))
                {
                    Undo.RecordObject(grassManager, "Set Medium Quality");
                    grassManager.SetGrassQuality("medium");
                    EditorUtility.SetDirty(grassManager);
                    UnityEngine.Debug.Log("[RTSGrassManager] Установлено Medium Quality");
                }
            }

            if (GUILayout.Button("High Quality", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Set High Quality", 
                    "Density: 20\nHeight: 0.6\nLOD: 150m\n\nТолько для мощных ПК", "Apply", "Cancel"))
                {
                    Undo.RecordObject(grassManager, "Set High Quality");
                    grassManager.SetGrassQuality("high");
                    EditorUtility.SetDirty(grassManager);
                    UnityEngine.Debug.Log("[RTSGrassManager] Установлено High Quality");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Кнопка обновления настроек
            if (GUILayout.Button("Update Grass Settings", GUILayout.Height(35)))
            {
                grassManager.UpdateGrassSettings();
                UnityEngine.Debug.Log("[RTSGrassManager] Настройки травы обновлены");
            }

            EditorGUILayout.Space();

            // Предупреждения
            SerializedProperty grassMaterialProp = serializedObject.FindProperty("grassMaterial");
            if (grassMaterialProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ Grass Material не назначен! Перетащи Materials/GeometryGrass.mat", MessageType.Warning);
            }

            SerializedProperty densityProp = serializedObject.FindProperty("grassDensity");
            if (densityProp.floatValue > 20f)
            {
                EditorGUILayout.HelpBox("⚠️ Плотность >20 может сильно снизить FPS в RTS игре!", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
