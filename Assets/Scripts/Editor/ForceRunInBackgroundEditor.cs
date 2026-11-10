#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RTSokak.Editor
{
    /// <summary>
    /// Listen-server не должен замирать при Alt+Tab — Run In Background всегда включён в Player Settings.
    /// </summary>
    [InitializeOnLoad]
    public static class ForceRunInBackgroundEditor
    {
        static ForceRunInBackgroundEditor()
        {
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                UnityEngine.Debug.Log("[RTS] PlayerSettings.runInBackground forced ON (host must keep simulating when unfocused).");
            }
        }
    }
}
#endif
