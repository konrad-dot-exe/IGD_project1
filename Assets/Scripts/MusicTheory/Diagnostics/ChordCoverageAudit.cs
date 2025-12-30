using System.Collections.Generic;
using System.Linq;
using Sonoria.MusicTheory;

namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// Audits chord coverage in voiced chords and emits diagnostics for missing required tones
    /// or pathological doubling patterns.
    /// </summary>
    public static class ChordCoverageAudit
    {
        /// <summary>
        /// Audits a voiced chord for coverage issues and emits diagnostics.
        /// </summary>
        /// <param name="regionIndex">Zero-based index of the region</param>
        /// <param name="chordLabel">Label for the chord (e.g., "I", "V7")</param>
        /// <param name="chordEvent">The chord event containing recipe and key</param>
        /// <param name="satbMidi">SATB MIDI notes array [Bass, Tenor, Alto, Soprano]</param>
        /// <param name="diags">Diagnostics collector (can be null)</param>
        public static void Audit(
            int regionIndex,
            string chordLabel,
            ChordEvent chordEvent,
            int[] satbMidi,
            DiagnosticsCollector diags)
        {
            if (diags == null || satbMidi == null || satbMidi.Length == 0)
                return;

            // Compute realized pitch classes from SATB MIDI
            var realizedPCs = new HashSet<int>();
            foreach (int midi in satbMidi)
            {
                int pc = ((midi % 12) + 12) % 12;
                realizedPCs.Add(pc);
            }

            // Get expected chord tone pitch classes using existing helper
            var chordTonePcs = TheoryVoicing.GetChordTonePitchClasses(chordEvent);
            if (chordTonePcs == null || chordTonePcs.Count == 0)
                return;

            // Determine required tones based on chord type
            var requiredPCs = new HashSet<int>();
            bool isSeventhChord = chordEvent.Recipe.Extension == ChordExtension.Seventh &&
                                  chordEvent.Recipe.SeventhQuality != SeventhQuality.None;

            if (isSeventhChord)
            {
                // 7th chord: root + 3rd + 7th required (5th optional)
                if (chordTonePcs.Count >= 1) requiredPCs.Add(chordTonePcs[0]); // Root
                if (chordTonePcs.Count >= 2) requiredPCs.Add(chordTonePcs[1]); // 3rd
                if (chordTonePcs.Count >= 4) requiredPCs.Add(chordTonePcs[3]); // 7th
            }
            else
            {
                // Triad: root + 3rd required (5th optional)
                if (chordTonePcs.Count >= 1) requiredPCs.Add(chordTonePcs[0]); // Root
                if (chordTonePcs.Count >= 2) requiredPCs.Add(chordTonePcs[1]); // 3rd
            }

            // Fallback: if we couldn't determine structure, require all except 5th
            if (requiredPCs.Count == 0 && chordTonePcs.Count >= 3)
            {
                requiredPCs.Add(chordTonePcs[0]); // Root
                if (chordTonePcs.Count >= 2) requiredPCs.Add(chordTonePcs[1]); // 3rd
                if (chordTonePcs.Count >= 4) requiredPCs.Add(chordTonePcs[3]); // 7th if present
            }

            // Get root pitch class for tension detection
            int rootPc = chordTonePcs.Count > 0 ? chordTonePcs[0] : 0;
            
            // Check which extraneous pitch classes are allowed tensions (11/#11 in soprano)
            var allowedTensionPCs = new HashSet<int>();
            
            // For v1: only check soprano (highest MIDI note) for 11th tensions
            if (satbMidi != null && satbMidi.Length > 0)
            {
                int sopranoMidi = satbMidi[satbMidi.Length - 1]; // Soprano is last in SATB array
                int sopranoPc = ((sopranoMidi % 12) + 12) % 12;
                
                // Check if soprano is an 11th tension
                int rel = (sopranoPc - rootPc + 12) % 12;
                if (ChordTensionUtils.TryGetEleventhTensionKindFromInterval(rel, out TensionKind tensionKind))
                {
                    allowedTensionPCs.Add(sopranoPc);
                }
            }
            
            // Find extraneous pitch classes (present in realized but not in expected chord tones)
            // Exclude allowed tensions from extraneous list
            var extraneous = realizedPCs.Where(pc => !chordTonePcs.Contains(pc) && !allowedTensionPCs.Contains(pc)).ToList();
            
            // Find missing required tones
            var missing = requiredPCs.Where(pc => !realizedPCs.Contains(pc)).ToList();

            // Helper to format pitch class set as compact string
            string FormatPcSet(IEnumerable<int> pcs)
            {
                var names = pcs.OrderBy(pc => pc)
                    .Select(pc => TheoryPitch.GetPitchNameFromMidi(pc + 60, chordEvent.Key))
                    .ToList();
                return string.Join(",", names);
            }

            // Emit diagnostics (cap at 2 events per region, prioritize NON_CHORD_TONE_PRESENT)
            int eventsEmitted = 0;
            const int maxEvents = 2;
            
            // Priority 0: Allowed tensions (informational, not warnings)
            if (allowedTensionPCs.Count > 0 && eventsEmitted < maxEvents)
            {
                foreach (int tensionPc in allowedTensionPCs)
                {
                    int rel = (tensionPc - rootPc + 12) % 12;
                    if (ChordTensionUtils.TryGetEleventhTensionKindFromInterval(rel, out TensionKind kind))
                    {
                        string tensionName = kind == TensionKind.Eleven ? "11" : "#11";
                        string tensionPitchName = TheoryPitch.GetPitchNameFromMidi(tensionPc + 60, chordEvent.Key);
                        diags.Add(regionIndex, DiagSeverity.Info, DiagCode.VOICING_DONE,
                            $"Tension present: {tensionName} ({tensionPitchName})");
                        eventsEmitted++;
                        break; // Only report one tension per region
                    }
                }
            }

            // Priority 1: Non-chord tones (emit first, but after allowed tensions)
            if (extraneous.Count > 0 && eventsEmitted < maxEvents)
            {
                string extraneousNames = FormatPcSet(extraneous);
                string expectedSet = FormatPcSet(chordTonePcs);
                string realizedSet = FormatPcSet(realizedPCs);
                diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.NON_CHORD_TONE_PRESENT,
                    $"Non-chord tone(s) present: {extraneousNames}. Expected={expectedSet} Realized={realizedSet}");
                eventsEmitted++;
            }

            // Priority 2: Missing required tones (if room remains)
            if (missing.Count == 1 && eventsEmitted < maxEvents)
            {
                int missingPc = missing[0];
                // Use MIDI 60+pc for key-aware naming (C4 = 60, so pc maps to octave 4)
                string missingName = TheoryPitch.GetPitchNameFromMidi(missingPc + 60, chordEvent.Key);
                diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.MISSING_REQUIRED_TONE,
                    $"Missing required tone: {missingName} (PC={missingPc})");
                eventsEmitted++;
            }
            else if (missing.Count >= 2 && eventsEmitted < maxEvents)
            {
                var missingNames = missing.Select(pc => 
                    TheoryPitch.GetPitchNameFromMidi(pc + 60, chordEvent.Key)).ToList(); // Use MIDI 60+pc for key-aware naming
                diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.MISSING_MULTIPLE_REQUIRED_TONES,
                    $"Missing {missing.Count} required tones: {string.Join(", ", missingNames)}");
                eventsEmitted++;
            }

            // Special case: 7th chord missing its 7th
            if (isSeventhChord && missing.Count > 0 && eventsEmitted < maxEvents)
            {
                bool missingSeventh = false;
                if (chordTonePcs.Count >= 4)
                {
                    int seventhPc = chordTonePcs[3];
                    if (missing.Contains(seventhPc))
                    {
                        missingSeventh = true;
                    }
                }
                
                if (missingSeventh)
                {
                    diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.MISSING_7TH_IN_7TH_CHORD,
                        "7th chord is missing its 7th");
                    eventsEmitted++;
                }
            }

            // Check for unusual doubling (only if we haven't hit the cap)
            if (eventsEmitted < maxEvents)
            {
                // Unusual doubling: 2 or fewer distinct pitch classes in 4-voice texture
                if (satbMidi.Length >= 4 && realizedPCs.Count <= 2)
                {
                    diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.UNUSUAL_DOUBLING,
                        $"Unusual doubling: only {realizedPCs.Count} distinct pitch classes in 4-voice texture");
                    eventsEmitted++;
                }
                // Unison stack: 3+ voices share the same exact MIDI value
                else if (satbMidi.Length >= 3)
                {
                    var midiCounts = new Dictionary<int, int>();
                    foreach (int midi in satbMidi)
                    {
                        if (!midiCounts.ContainsKey(midi))
                            midiCounts[midi] = 0;
                        midiCounts[midi]++;
                    }
                    
                    foreach (var kvp in midiCounts)
                    {
                        if (kvp.Value >= 3)
                        {
                            string noteName = TheoryPitch.GetPitchNameFromMidi(kvp.Key, chordEvent.Key);
                            diags.Add(regionIndex, DiagSeverity.Warning, DiagCode.UNISON_STACK,
                                $"Unison stack: {kvp.Value} voices share {noteName} (MIDI {kvp.Key})");
                            eventsEmitted++;
                            break; // Only report one unison stack
                        }
                    }
                }
            }
        }
    }
}

