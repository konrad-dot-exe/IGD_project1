#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EarFPS;
using System.Collections.Generic;
using System.Text;
using Sonoria.MusicTheory;
using Sonoria.MusicTheory.Timeline;

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

        [MenuItem("Tools/Chord Lab/Debug/Dump Current Regions")]
        public static void DumpCurrentRegions()
        {
            var controller = Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogWarning("[ChordLab Regions] No ChordLabController found in the current scene.");
                return;
            }

            var regions = controller.GetLastRegions();
            var timelineSpec = controller.GetTimelineSpec();

            if (regions == null || regions.Count == 0)
            {
                Debug.Log("[ChordLab Regions] No regions captured yet — run Play/SATB/N.H. first");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Chord Lab Regions Dump ===");
            sb.AppendLine($"Timeline Spec: ticksPerQuarter={timelineSpec.ticksPerQuarter}");
            if (timelineSpec.tempoBpm.HasValue)
                sb.AppendLine($"  Tempo: {timelineSpec.tempoBpm.Value} BPM");
            if (timelineSpec.timeSigNumerator.HasValue && timelineSpec.timeSigDenominator.HasValue)
                sb.AppendLine($"  Time Signature: {timelineSpec.timeSigNumerator}/{timelineSpec.timeSigDenominator}");
            sb.AppendLine();
            sb.AppendLine($"Total Regions: {regions.Count}");
            sb.AppendLine();

            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                var chordEvent = region.chordEvent;
                
                // Calculate beats from ticks
                float startBeats = region.startTick / (float)timelineSpec.ticksPerQuarter;
                float durationBeats = region.durationTicks / (float)timelineSpec.ticksPerQuarter;

                // Get root pitch class
                int rootPc = -1;
                if (chordEvent.Recipe.Degree >= 0)
                {
                    rootPc = TheoryScale.GetDegreePitchClass(chordEvent.Key, chordEvent.Recipe.Degree);
                    if (rootPc >= 0)
                    {
                        rootPc = (rootPc + chordEvent.Recipe.RootSemitoneOffset + 12) % 12;
                        if (rootPc < 0) rootPc += 12;
                    }
                }

                // Get inversion info (if available from recipe)
                string inversionInfo = "";
                if (chordEvent.Recipe.Inversion != ChordInversion.Root)
                {
                    inversionInfo = $" (Inv: {chordEvent.Recipe.Inversion})";
                }

                // Get melody MIDI if present
                string melodyInfo = "";
                if (chordEvent.MelodyMidi.HasValue)
                {
                    string melodyName = TheoryPitch.GetPitchNameFromMidi(chordEvent.MelodyMidi.Value, chordEvent.Key);
                    melodyInfo = $" | Melody: {melodyName} (MIDI {chordEvent.MelodyMidi.Value})";
                }

                sb.AppendLine($"Region {i}:");
                sb.AppendLine($"  Label/Roman: '{region.debugLabel ?? "?"}'");
                sb.AppendLine($"  Timeline: startTick={region.startTick}, durationTicks={region.durationTicks}");
                sb.AppendLine($"  Beats: start={startBeats:F2}, duration={durationBeats:F2}");
                sb.AppendLine($"  Root PC: {rootPc}{inversionInfo}{melodyInfo}");
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
    }
}
#endif

