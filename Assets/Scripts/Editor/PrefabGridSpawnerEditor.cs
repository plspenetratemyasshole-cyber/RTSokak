using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PrefabGridSpawner))]
public class PrefabGridSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PrefabGridSpawner spawner = (PrefabGridSpawner)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab Grid Spawner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Универсальная система размножения префабов по карте", MessageType.Info);
        EditorGUILayout.Space();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Grid", GUILayout.Height(40)))
        {
            spawner.Regenerate();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear All", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear All", 
                "Удалить все экземпляры?", 
                "Delete", "Cancel"))
            {
                spawner.ClearAll();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        SerializedProperty prefabProp = serializedObject.FindProperty("prefab");
        if (prefabProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("⚠️ Prefab не назначен!", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
