using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RTSokak.EditorTools
{
    /// <summary>
    /// One-shot helpers after switching project to Built-in Render Pipeline.
    /// Menu: Tools/RTSokak/Convert Materials To Built-in
    /// </summary>
    public static class ConvertToBuiltinPipeline
    {
        [MenuItem("Tools/RTSokak/Convert Materials To Built-in")]
        public static void ConvertMaterials()
        {
            Shader standard = Shader.Find("Standard");
            Shader unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Unlit/Texture");
            Shader particles = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            string[] guids = AssetDatabase.FindAssets("t:Material");
            int converted = 0;
            var report = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.StartsWith("Packages/"))
                    continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;

                string shaderName = mat.shader.name;
                bool isHdrp = shaderName.StartsWith("HDRP/")
                    || shaderName.Contains("HDRenderPipeline")
                    || shaderName == "Hidden/InternalErrorShader"
                    || mat.shader.name.Contains("Error");

                // Also catch pink/missing after package removal
                if (!isHdrp && mat.shader.isSupported && !shaderName.Contains("Hidden/InternalError"))
                {
                    // Already Built-in / custom project shaders — skip
                    if (!shaderName.StartsWith("HDRP/") && !shaderName.StartsWith("Universal Render Pipeline/"))
                        continue;
                }

                Color color = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_UnlitColor"))
                    color = mat.GetColor("_UnlitColor");
                else if (mat.HasProperty("_Color"))
                    color = mat.GetColor("_Color");

                Texture mainTex = null;
                if (mat.HasProperty("_BaseColorMap"))
                    mainTex = mat.GetTexture("_BaseColorMap");
                else if (mat.HasProperty("_MainTex"))
                    mainTex = mat.GetTexture("_MainTex");

                bool wantUnlit = shaderName.Contains("Unlit") || path.Contains("Outline") || path.Contains("Line");
                bool wantParticle = path.Contains("FX") || path.Contains("Particle");

                Shader target = wantParticle && particles != null
                    ? particles
                    : (wantUnlit && unlit != null ? unlit : standard);

                if (target == null)
                {
                    report.Add($"SKIP (no target shader): {path}");
                    continue;
                }

                mat.shader = target;
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                if (mat.HasProperty("_MainTex") && mainTex != null)
                    mat.SetTexture("_MainTex", mainTex);
                if (mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Glossiness"))
                    mat.SetFloat("_Glossiness", 0.4f);

                EditorUtility.SetDirty(mat);
                converted++;
                report.Add($"{path} -> {target.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[ConvertToBuiltin] Converted {converted} materials.\n" + string.Join("\n", report));
        }

        [MenuItem("Tools/RTSokak/Strip Missing HDRP Scripts From Open Scene")]
        public static void StripMissingScriptsInOpenScenes()
        {
            int removed = 0;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            UnityEngine.Debug.Log($"[ConvertToBuiltin] Removed {removed} missing script components from active scene.");
        }
    }
}
