#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Sonoria.MusicTheory;
using EarFPS;

namespace EarFPS
{
    /// <summary>
    /// Editor-only debug menu for Chord Lab melody analysis visualization.
    /// </summary>
    public static class ChordLabMelodyDebugMenu
    {
        [MenuItem("Tools/Chord Lab/Log Test Melody Analysis")]
        private static void LogTestMelodyAnalysis()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Melody Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogTestMelodyAnalysis();
        }

        [MenuItem("Tools/Chord Lab/Log Melody-Constrained Voicing")]
        private static void LogMelodyConstrainedVoicing()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Melody Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogMelodyConstrainedVoicing();
        }

        [MenuItem("Tools/Chord Lab/Log Harmony Candidates For Test Melody")]
        private static void LogHarmonyCandidatesForTestMelody()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Melody Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogHarmonyCandidatesForTestMelody();
        }

        [MenuItem("Tools/Chord Lab/Log Naive Harmonization For Test Melody")]
        private static void LogNaiveHarmonizationForTestMelody()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("Chord Lab Melody Debug: No ChordLabController found in the scene.");
                return;
            }

            controller.DebugLogNaiveHarmonizationForTestMelody();
        }

        [MenuItem("Tools/Chord Lab/Play Naive Harmonization For Test Melody (Voiced)")]
        private static void PlayNaiveHarmonizationForTestMelodyVoiced()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene for naive harmonization playback.");
                return;
            }

            controller.DebugPlayNaiveHarmonizationForTestMelody();
        }

        [MenuItem("Tools/Chord Lab/Play Manual Progression With Melody (Voiced)")]
        private static void PlayManualProgressionWithMelodyVoiced()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene for voiced manual progression playback.");
                return;
            }

            controller.DebugPlayManualProgressionWithMelodyVoiced();
        }

        [MenuItem("Tools/Chord Lab/Log Naive Harmonization Snapshot (JSON)")]
        private static void LogNaiveHarmonizationSnapshotJson()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene for snapshot export.");
                return;
            }

            controller.DebugLogNaiveHarmonizationSnapshotJson();
        }

        [MenuItem("Tools/Chord Lab/Log Naive Harmonization Voice Movements")]
        private static void LogNaiveHarmonizationVoiceMovements()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene for voice movement analysis.");
                return;
            }

            controller.DebugLogNaiveHarmonizationVoiceMovements();
        }

        [MenuItem("Tools/Chord Lab/Export Current Voiced Harmonization To JSON")]
        private static void ExportCurrentVoicedHarmonization()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene for voiced harmonization export.");
                return;
            }

            controller.DebugExportCurrentVoicedHarmonization();
        }

        [MenuItem("Tools/Chord Lab/Toggle Tendency Debug Logging")]
        private static void ToggleTendencyDebugLogging()
        {
            bool currentState = TheoryVoicing.GetTendencyDebug();
            TheoryVoicing.SetTendencyDebug(!currentState);
            
            Debug.Log($"[ChordLab] Tendency debug logging: {(currentState ? "OFF → ON" : "ON → OFF")}");
        }

        [MenuItem("Tools/Chord Lab/Toggle Tendency Debug Logging", true)]
        private static bool ToggleTendencyDebugLoggingValidate()
        {
            // Show checkmark when enabled
            Menu.SetChecked("Tools/Chord Lab/Toggle Tendency Debug Logging", TheoryVoicing.GetTendencyDebug());
            return true;
        }
    }
}
#endif

