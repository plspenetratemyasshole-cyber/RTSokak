using UnityEditor;
using UnityEngine;
using RTSokak.UI;

namespace RTSokak.EditorTools
{
    public static class HealthBarGlobalSettingsMenu
    {
        const string AssetPath = "Assets/Resources/UI/HealthBarGlobalSettings.asset";

        [MenuItem("RTSokak/Health Bar/Select Global Settings")]
        public static void SelectOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HealthBarGlobalSettings>(AssetPath);
            if (settings == null)
            {
                string dir = "Assets/Resources/UI";
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets/Resources", "UI");

                settings = ScriptableObject.CreateInstance<HealthBarGlobalSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log($"[RTSokak] Created {AssetPath} — настраивай размер/цвет HP bar здесь.");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
