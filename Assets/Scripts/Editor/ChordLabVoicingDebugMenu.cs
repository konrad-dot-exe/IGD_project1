#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EarFPS;

namespace EarFPS
{
    /// <summary>
    /// Editor-only debug menu for Chord Lab voicing visualization.
    /// </summary>
    public static class ChordLabVoicingDebugMenu
    {
        [MenuItem("Tools/Chord Lab/Log First Chord Voicing")]
        public static void LogFirstChordVoicing()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Voicing Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogFirstChordVoicing();
        }

        [MenuItem("Tools/Chord Lab/Log Progression Voicing")]
        public static void LogProgressionVoicing()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Voicing Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogProgressionVoicing();
        }
    }
}
#endif

