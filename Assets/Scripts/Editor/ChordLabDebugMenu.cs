#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EarFPS;

namespace EarFPS
{
    /// <summary>
    /// Editor-only debug menu for Chord Lab progression analysis.
    /// </summary>
    public static class ChordLabDebugMenu
    {
        [MenuItem("Tools/Chord Lab/Log Current Progression Analysis")]
        public static void LogCurrentProgressionAnalysis()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("ChordLabDebugMenu: No ChordLabController found in the current scene.");
                return;
            }

            controller.DebugLogCurrentProgressionAnalysis();
        }
    }
}
#endif

