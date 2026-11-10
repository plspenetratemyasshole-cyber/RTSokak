#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.PostProcessing;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Editor helper for quick post-processing setup
    /// </summary>
    [CustomEditor(typeof(PostProcessingSetup))]
    public class PostProcessingSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PostProcessingSetup setup = (PostProcessingSetup)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Apply RTS-optimized post-processing settings to the assigned profile.\n\n" +
                "Performance: ~1-2% GPU (AO + Color Grading + Vignette)\n" +
                "Balanced: ~3-5% GPU (+ Bloom + Chromatic Aberration + Grain)\n" +
                "Quality: ~7-10% GPU (+ Screen Space Reflections)",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Apply RTS Optimized Settings", GUILayout.Height(40)))
            {
                setup.ApplyRTSOptimizedSettings();
                EditorUtility.DisplayDialog("Post-Processing Setup",
                    "RTS-optimized settings applied!\n\nCheck the profile in Inspector to verify all effects are configured.",
                    "OK");
            }
        }
    }

    /// <summary>
    /// Menu item for quick post-processing setup
    /// </summary>
    public static class PostProcessingSetupMenu
    {
        [MenuItem("Tools/RTS Setup/Setup Post Processing")]
        public static void SetupPostProcessing()
        {
            // Find or create PostProcessVolume
            PostProcessVolume volume = Object.FindFirstObjectByType<PostProcessVolume>();
            
            if (volume == null)
            {
                // Find main camera
                UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
                if (mainCamera == null)
                {
                    EditorUtility.DisplayDialog("Setup Post Processing",
                        "Main Camera not found!\n\nTag a camera as MainCamera first.",
                        "OK");
                    return;
                }

                // Create PostProcessVolume on camera
                volume = mainCamera.gameObject.AddComponent<PostProcessVolume>();
                volume.isGlobal = true;
                volume.weight = 1f;
                volume.priority = 0;

                UnityEngine.Debug.Log("[PostProcessingSetup] Created PostProcessVolume on Main Camera");
            }

            // Create profile if missing
            if (volume.sharedProfile == null)
            {
                string path = "Assets/Settings/PostProcessProfile_RTS.asset";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

                PostProcessProfile profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, path);
                volume.sharedProfile = profile;

                UnityEngine.Debug.Log($"[PostProcessingSetup] Created profile at {path}");
            }

            // Add PostProcessLayer to camera if missing
            UnityEngine.Camera cam = volume.GetComponent<UnityEngine.Camera>();
            if (cam != null)
            {
                PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();
                if (layer == null)
                {
                    layer = cam.gameObject.AddComponent<PostProcessLayer>();
                    layer.volumeTrigger = cam.transform;
                    layer.volumeLayer = LayerMask.GetMask("Default");
                    
                    // Enable anti-aliasing
                    layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                    layer.fastApproximateAntialiasing.fastMode = true;
                    layer.fastApproximateAntialiasing.keepAlpha = false;

                    UnityEngine.Debug.Log("[PostProcessingSetup] Added PostProcessLayer to camera");
                }
            }

            // Create setup helper object
            GameObject setupObj = new GameObject("PostProcessingSetup");
            PostProcessingSetup setup = setupObj.AddComponent<PostProcessingSetup>();
            
            // Assign profile via SerializedObject
            SerializedObject so = new SerializedObject(setup);
            so.FindProperty("profile").objectReferenceValue = volume.sharedProfile;
            so.FindProperty("preset").enumValueIndex = 1; // Balanced
            so.ApplyModifiedProperties();

            // Select in hierarchy
            Selection.activeGameObject = setupObj;

            EditorUtility.DisplayDialog("Setup Post Processing",
                "Post-Processing setup complete!\n\n" +
                "1. PostProcessVolume created/found\n" +
                "2. Profile created/assigned\n" +
                "3. PostProcessLayer added to camera\n" +
                "4. PostProcessingSetup helper created\n\n" +
                "Now click 'Apply RTS Optimized Settings' in Inspector!",
                "OK");
        }
    }
}
#endif
