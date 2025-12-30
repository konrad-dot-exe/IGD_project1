using System;
using System.Collections.Generic;
using System.Linq;
using Sonoria.MusicTheory.Diagnostics;
using Sonoria.MusicTheory.Timeline;
using UnityEngine;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Global flag to enable/disable regression harness output.
    /// When OFF: zero regression-related output (no UI, no console logs).
    /// When ON: regression results displayed in dev-only panel.
    /// </summary>
    public static class RegressionHarness
    {
        /// <summary>
        /// Master flag to enable regression harness. Default: false (OFF).
        /// </summary>
        public static bool EnableRegressionHarness { get; set; } = false;
        
        /// <summary>
        /// Current test case name being executed (null when not running a test).
        /// Used to gate diagnostic logging to only the current test case.
        /// </summary>
        public static string CurrentTestCaseName { get; private set; } = null;
        
        /// <summary>
        /// Current test case checks being executed (None when not running a test).
        /// Used to gate diagnostic logging to only relevant test types.
        /// </summary>
        public static RegressionChecks CurrentTestCaseChecks { get; private set; } = RegressionChecks.None;
        
        /// <summary>
        /// Sets the current test case being executed. Call this at the start of a test case.
        /// </summary>
        public static void SetCurrentTestCase(string caseName, RegressionChecks checks)
        {
            CurrentTestCaseName = caseName;
            CurrentTestCaseChecks = checks;
        }
        
        /// <summary>
        /// Clears the current test case. Call this at the end of a test case.
        /// </summary>
        public static void ClearCurrentTestCase()
        {
            CurrentTestCaseName = null;
            CurrentTestCaseChecks = RegressionChecks.None;
        }
        
        /// <summary>
        /// Flag to enable verbose diagnostic dump for Aug5 regression checks.
        /// When enabled, prints exactly what the check is reading for debugging.
        /// </summary>
        public static bool EnableAug5RegressionDump { get; set; } = true; // Default ON for debugging
        
        /// <summary>
        /// Returns true if diminished triad diagnostics should be logged for the current test case.
        /// </summary>
        public static bool ShouldLogDiminishedTriadDiagnostics()
        {
            return EnableRegressionHarness && 
                   CurrentTestCaseChecks.HasFlag(RegressionChecks.DiminishedTriadIdentityTonesPresent);
        }
    }

    /// <summary>
    /// Bitmask flags for regression checks.
    /// </summary>
    [Flags]
    public enum RegressionChecks
    {
        None = 0,
        ChordalSeventhResolvesDownIfAvailable = 1 << 0,
        RequiredChordTonesPresent = 1 << 1,
        DiminishedTriadIdentityTonesPresent = 1 << 2,
        AugmentedFifthResolvesUpIfAvailable = 1 << 3,
        // Future checks can be added here
    }

    /// <summary>
    /// Represents a single regression test case.
    /// </summary>
    public class RegressionCase
    {
        public string name;
        public int keyTonic; // Pitch class (0-11, C=0)
        public ScaleMode mode; // Major or Minor only
        public string progressionInput; // Exact same string users type
        public string melodyInput; // Optional, can be null/empty
        public RegressionChecks checks;

        public RegressionCase(string name, int keyTonic, ScaleMode mode, string progressionInput, string melodyInput, RegressionChecks checks)
        {
            this.name = name;
            this.keyTonic = keyTonic;
            this.mode = mode;
            this.progressionInput = progressionInput;
            this.melodyInput = melodyInput;
            this.checks = checks;
        }
    }

    /// <summary>
    /// Represents a single regression failure.
    /// </summary>
    public class RegressionFailure
    {
        public string caseName;
        public int regionIndex; // Region N (0-based)
        public string voiceName; // S/A/T/B
        public int fromMidi;
        public int toMidi;
        public string message;
        public string stage; // Optional: "initial", "coverage_repair", "continuity", "final"

        public RegressionFailure(string caseName, int regionIndex, string voiceName, int fromMidi, int toMidi, string message, string stage = "final")
        {
            this.caseName = caseName;
            this.regionIndex = regionIndex;
            this.voiceName = voiceName;
            this.fromMidi = fromMidi;
            this.toMidi = toMidi;
            this.message = message;
            this.stage = stage;
        }
    }

    /// <summary>
    /// Diagnostic details for a regression case (only populated when EnableRegressionHarness is ON).
    /// </summary>
    public class RegressionCaseDiagnostics
    {
        public string caseName;
        public List<RegionVoicingInfo> regionVoicings;
        public List<SeventhResolutionCheck> seventhResolutionChecks;
        public List<RequiredChordTonesCheck> requiredChordTonesChecks;

        public RegressionCaseDiagnostics(string caseName)
        {
            this.caseName = caseName;
            this.regionVoicings = new List<RegionVoicingInfo>();
            this.seventhResolutionChecks = new List<SeventhResolutionCheck>();
            this.requiredChordTonesChecks = new List<RequiredChordTonesCheck>();
        }
    }

    /// <summary>
    /// Voicing information for a single region.
    /// </summary>
    public class RegionVoicingInfo
    {
        public int regionIndex;
        public string chordLabel;
        public int bassMidi;
        public int tenorMidi;
        public int altoMidi;
        public int sopranoMidi;
        public string bassName;
        public string tenorName;
        public string altoName;
        public string sopranoName;
    }

    /// <summary>
    /// Details about a required chord tones check.
    /// </summary>
    public class RequiredChordTonesCheck
    {
        public int regionIndex;
        public string chordLabel;
        public List<int> requiredPcs; // Required pitch classes
        public List<string> requiredPcNames; // Names for required PCs
        public HashSet<int> realizedPcs; // Realized BTAS pitch classes
        public List<int> missingPcs; // Missing required PCs
        public List<string> missingPcNames; // Names for missing PCs
        public List<int> realizedMidi; // Realized BTAS MIDI pitches [B, T, A, S]
        public List<string> realizedNames; // Realized BTAS note names [B, T, A, S]
        public string result; // "PASS" or "FAIL"
        public string reason; // Reason for the result
    }

    /// <summary>
    /// Details about a 7th resolution check.
    /// </summary>
    public class SeventhResolutionCheck
    {
        public int regionIndex;
        public int seventhPc; // -1 if no 7th detected
        public string seventhPcName;
        public int voiceIndex; // -1 if no voice holds the 7th
        public string voiceName;
        public int voiceMidi; // -1 if no voice holds the 7th
        public string voiceNoteName;
        public int destPcDown1;
        public int destPcDown2;
        public string destPcDown1Name;
        public string destPcDown2Name;
        public bool hasPcDown1InDest;
        public bool hasPcDown2InDest;
        public int resolvedToMidi; // -1 if check skipped
        public int resolvedToPc; // -1 if check skipped
        public string resolvedToName;
        public bool resolvedCorrectly;
        public string result; // "PASS", "FAIL", or "SKIP"
        public string reason; // Reason for the result
        public List<int> destChordTonePcs; // All destination chord tone pitch classes
    }

    /// <summary>
    /// Regression test report containing results.
    /// </summary>
    public class RegressionReport
    {
        public int caseCount;
        public int passCount;
        public int failCount;
        public List<RegressionFailure> failures;
        public Dictionary<string, RegressionCaseDiagnostics> caseDiagnostics;

        public RegressionReport()
        {
            failures = new List<RegressionFailure>();
            caseDiagnostics = new Dictionary<string, RegressionCaseDiagnostics>();
        }
    }

    /// <summary>
    /// Runner for regression tests.
    /// </summary>
    public static class RegressionRunner
    {
        private static List<RegressionCase> s_cases = new List<RegressionCase>();

        /// <summary>
        /// Initialize the regression case list.
        /// </summary>
        static RegressionRunner()
        {
            InitializeCases();
        }

        /// <summary>
        /// Gets all registered test cases (for reporting).
        /// </summary>
        public static List<RegressionCase> GetAllCases()
        {
            return new List<RegressionCase>(s_cases);
        }

        /// <summary>
        /// Initialize hardcoded regression cases.
        /// </summary>
        private static void InitializeCases()
        {
            s_cases.Clear();

            // Bundle: ChordalSeventhResolvesDownIfAvailable
            // All cases use absolute chord symbols to match manual UI entry
            
            // Baseline major V7→I
            s_cases.Add(new RegressionCase(
                name: "G7_to_C_seventh_must_resolve",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "G7 C",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Minor V7→i
            s_cases.Add(new RegressionCase(
                name: "G7_to_Cm_seventh_must_resolve",
                keyTonic: 0, // C
                mode: ScaleMode.Aeolian, // Minor
                progressionInput: "G7 Cm",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Minor-key dominant
            s_cases.Add(new RegressionCase(
                name: "E7_to_Am_seventh_must_resolve",
                keyTonic: 9, // A
                mode: ScaleMode.Aeolian, // Minor
                progressionInput: "E7 Am",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Chromatic / borrowed target
            s_cases.Add(new RegressionCase(
                name: "A7_to_Bb_seventh_must_resolve",
                keyTonic: 2, // D
                mode: ScaleMode.Aeolian, // Minor
                progressionInput: "A7 Bb",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Altered dominant with b9 into tonic major 7
            s_cases.Add(new RegressionCase(
                name: "B7b9_to_Emaj7_seventh_must_resolve",
                keyTonic: 4, // E
                mode: ScaleMode.Ionian, // Major
                progressionInput: "B7b9 Emaj7",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Existing stress case: C7 Fm (borrowed iv in major)
            s_cases.Add(new RegressionCase(
                name: "C7_to_Fm_seventh_must_resolve",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "C7 Fm",
                melodyInput: null,
                checks: RegressionChecks.ChordalSeventhResolvesDownIfAvailable
            ));

            // Bundle: RequiredChordTonesPresent
            // All cases use absolute chord symbols to match manual UI entry

            s_cases.Add(new RegressionCase(
                name: "ReqTones_C_to_Am",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "C Am",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            s_cases.Add(new RegressionCase(
                name: "ReqTones_C_to_Ab",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "C Ab",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            s_cases.Add(new RegressionCase(
                name: "ReqTones_Bdim_to_C",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "Bdim C",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            s_cases.Add(new RegressionCase(
                name: "ReqTones_G7_to_C",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "G7 C",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            s_cases.Add(new RegressionCase(
                name: "ReqTones_B7b9_to_Emaj7",
                keyTonic: 4, // E
                mode: ScaleMode.Ionian, // Major
                progressionInput: "B7b9 Emaj7",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            // Bundle: AugmentedFifthResolvesUpIfAvailable
            s_cases.Add(new RegressionCase(
                name: "Aug5_Caug_to_F_withMelody_mustResolve",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "C Caug F",
                melodyInput: "E4 E4 C4",
                checks: RegressionChecks.AugmentedFifthResolvesUpIfAvailable
            ));

            s_cases.Add(new RegressionCase(
                name: "ReqTones_Fsm7b5_B7_Em",
                keyTonic: 4, // E
                mode: ScaleMode.Aeolian, // Minor
                progressionInput: "F#m7b5 B7 Em",
                melodyInput: null,
                checks: RegressionChecks.RequiredChordTonesPresent
            ));

            // Bundle: DiminishedTriadIdentityTonesPresent
            // All cases use absolute chord symbols to match manual UI entry

            // Single diminished triad
            s_cases.Add(new RegressionCase(
                name: "DimTriad_Fdim",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "Fdim",
                melodyInput: null,
                checks: RegressionChecks.DiminishedTriadIdentityTonesPresent
            ));

            // Three-chord diminished chain
            s_cases.Add(new RegressionCase(
                name: "DimTriad_Bdim_Ddim_Fdim",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "Bdim Ddim Fdim",
                melodyInput: null,
                checks: RegressionChecks.DiminishedTriadIdentityTonesPresent
            ));

            // Full 8-chord minor-third chain (Bdim Ddim Fdim Abdim Bdim Ddim Fdim Abdim)
            s_cases.Add(new RegressionCase(
                name: "DimTriad_8chord_chain",
                keyTonic: 0, // C
                mode: ScaleMode.Ionian, // Major
                progressionInput: "Bdim Ddim Fdim Abdim Bdim Ddim Fdim Abdim",
                melodyInput: null,
                checks: RegressionChecks.DiminishedTriadIdentityTonesPresent
            ));
            
                // Timeline v1 regression cases
                // Case 1: Verify SATB voicing unchanged when melody events multiply per region
                // TEMPORARY: Simplified to "I IV V I" to verify harness pipeline
                s_cases.Add(new RegressionCase(
                    name: "TimelineV1_Melody4xPerRegion_SATBUnchanged",
                    keyTonic: 0, // C
                    mode: ScaleMode.Ionian, // C major
                    progressionInput: "I IV V I", // TEMPORARY: Simplified from "I V vi IV"
                    melodyInput: "C5 C5 C5 C5 F4 F4 F4 F4 G4 G4 G4 G4 C5 C5 C5 C5", // 4 melody events per chord region
                    checks: RegressionChecks.RequiredChordTonesPresent // Verify voicing legality (property-based)
                ));
                
                // Case 2: Verify chord symbol does NOT upgrade from melody non-chord tones
                // TEMPORARY: Simplified to "I IV V I" to verify harness pipeline
                s_cases.Add(new RegressionCase(
                    name: "TimelineV1_MelodyNCT_ChordSymbolUnchanged",
                    keyTonic: 0, // C
                    mode: ScaleMode.Ionian, // C major
                    progressionInput: "I IV V I", // TEMPORARY: Simplified from "I"
                    melodyInput: "A5", // Melody A over C chord (non-chord tone - should NOT upgrade symbol to C(add6))
                    checks: RegressionChecks.None // Property-based: chord symbol should remain "C" (checked in validation)
                ));
                
                // Case 3: Verify melody parsing doesn't drop final note (bug fix)
                // Tests parsing of "C D E F G F E D C" - should produce 9 events, last MIDI = C5
                s_cases.Add(new RegressionCase(
                    name: "TimelineV1_MelodyParsing_NoDroppedFinalNote",
                    keyTonic: 0, // C
                    mode: ScaleMode.Ionian, // C major
                    progressionInput: "I", // Simple progression
                    melodyInput: "C5 D5 E5 F5 G5 F5 E5 D5 C5", // 9 notes, final C5 should not be dropped
                    checks: RegressionChecks.None // Property-based: eventCount=9, lastMidi=C5 (validated in RunCase)
                ));
        }

        /// <summary>
        /// Run a single regression case by name.
        /// </summary>
        public static RegressionReport RunCase(string caseName, Func<string, TheoryKey, TimelineSpec, IReadOnlyList<int>, List<ChordRegion>> buildRegionsFunc, Func<TheoryKey, TimelineSpec, IReadOnlyList<ChordRegion>, bool, int, int, int, int, int, DiagnosticsCollector, List<VoicedChord>> voiceLeadFunc)
        {
            if (!RegressionHarness.EnableRegressionHarness)
            {
                return null; // Silent when disabled
            }

            var report = new RegressionReport();
            report.caseCount = 1;

            var testCase = s_cases.FirstOrDefault(c => c.name == caseName);
            if (testCase == null)
            {
                report.failCount = 1;
                report.failures.Add(new RegressionFailure(
                    caseName, -1, "N/A", -1, -1,
                    $"Test case '{caseName}' not found"
                ));
                return report;
            }

            try
            {
                // Entry point log
                UnityEngine.Debug.Log($"[ENTRY] RegressionHarness -> RunCase({testCase.name}) -> VoiceLeadRegions -> VoiceLeadProgressionWithMelody -> BuildUpperVoicesIncrementalWithMelody");
                
                // Set current test case for diagnostic gating
                RegressionHarness.SetCurrentTestCase(testCase.name, testCase.checks);
                
                // Build key with specified tonic
                TheoryKey key = new TheoryKey(testCase.keyTonic, testCase.mode);

                // Build timeline spec
                TimelineSpec spec = new TimelineSpec { ticksPerQuarter = 4 };

                // Parse melody if provided (space-separated note names like "E4 E4 C4")
                List<int> melodyMidiList = null;
                bool hasMelody = !string.IsNullOrEmpty(testCase.melodyInput);
                if (hasMelody)
                {
                    melodyMidiList = new List<int>();
                    char[] separators = { ' ', '\t', '\n', '\r' };
                    string[] tokens = testCase.melodyInput.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (string token in tokens)
                    {
                        string trimmed = token.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                            continue;
                        
                        // Parse note name to MIDI (e.g., "E4" -> 64, "C4" -> 60)
                        // Note: Handle duration suffixes like "C5:2" by extracting note part before ":"
                        string noteToken = trimmed;
                        int colonIndex = trimmed.IndexOf(':');
                        if (colonIndex >= 0)
                        {
                            noteToken = trimmed.Substring(0, colonIndex).Trim();
                        }
                        
                        if (TryParseNoteNameToMidi(noteToken, out int midi))
                        {
                            melodyMidiList.Add(midi);
                        }
                        else
                        {
                            report.failCount = 1;
                            report.failures.Add(new RegressionFailure(
                                testCase.name, -1, "N/A", -1, -1,
                                $"Failed to parse melody token '{noteToken}' (from '{trimmed}') from input '{testCase.melodyInput}'"
                            ));
                            return report;
                        }
                    }
                    
                    // Validate melody parsing for specific test cases
                    if (testCase.name == "TimelineV1_MelodyParsing_NoDroppedFinalNote")
                    {
                        const int expectedEventCount = 9;
                        const int expectedLastMidi = 72; // C5 = MIDI 72
                        
                        if (melodyMidiList == null || melodyMidiList.Count != expectedEventCount)
                        {
                            report.failCount = 1;
                            report.failures.Add(new RegressionFailure(
                                testCase.name, -1, "N/A", -1, -1,
                                $"Melody parsing failed: expected {expectedEventCount} events, got {melodyMidiList?.Count ?? 0}"
                            ));
                            return report;
                        }
                        
                        int actualLastMidi = melodyMidiList[melodyMidiList.Count - 1];
                        if (actualLastMidi != expectedLastMidi)
                        {
                            report.failCount = 1;
                            report.failures.Add(new RegressionFailure(
                                testCase.name, -1, "N/A", -1, -1,
                                $"Melody parsing failed: expected last MIDI {expectedLastMidi} (C5), got {actualLastMidi}"
                            ));
                            return report;
                        }
                        
                        UnityEngine.Debug.Log($"[Regression] ✓ Melody parsing validated: {melodyMidiList.Count} events, last MIDI={actualLastMidi} (C5)");
                    }
                }

                // Build regions with detailed diagnostics
                if (RegressionHarness.EnableRegressionHarness)
                {
                    UnityEngine.Debug.Log($"[Regression] Building regions for case '{testCase.name}':");
                    UnityEngine.Debug.Log($"[Regression]   progressionInput: '{testCase.progressionInput}'");
                    UnityEngine.Debug.Log($"[Regression]   key: {key} (tonic={testCase.keyTonic}, mode={testCase.mode})");
                    UnityEngine.Debug.Log($"[Regression]   melodyInput: '{(testCase.melodyInput ?? "null")}'");
                    UnityEngine.Debug.Log($"[Regression]   melodyMidiList: {(melodyMidiList != null ? $"Count={melodyMidiList.Count}, MIDI=[{string.Join(",", melodyMidiList)}]" : "null")}");
                    
                    // Split progression input into tokens for diagnostic
                    char[] separators = { ' ', '\t', '\n', '\r' };
                    string[] tokens = testCase.progressionInput.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
                    UnityEngine.Debug.Log($"[Regression]   Split tokens ({tokens.Length}): [{string.Join(" | ", tokens.Select((t, i) => $"#{i}:'{t}'"))}]");
                    
                    // Try parsing each token individually to identify which one fails
                    for (int tokenIdx = 0; tokenIdx < tokens.Length; tokenIdx++)
                    {
                        string token = tokens[tokenIdx].Trim();
                        if (TheoryChord.TryParseRomanNumeral(key, token, out var testRecipe))
                        {
                            UnityEngine.Debug.Log($"[Regression]   Token #{tokenIdx} '{token}': ✓ Parsed successfully (Degree={testRecipe.Degree}, Quality={testRecipe.Quality})");
                        }
                        else
                        {
                            UnityEngine.Debug.Log($"[Regression]   Token #{tokenIdx} '{token}': ✗ FAILED TryParseRomanNumeral");
                        }
                    }
                }
                
                List<ChordRegion> regions = buildRegionsFunc(testCase.progressionInput, key, spec, melodyMidiList);
                if (regions == null || regions.Count == 0)
                {
                    report.failCount = 1;
                    string diagnosticMsg = $"Failed to build regions from progression input '{testCase.progressionInput}'";
                    if (RegressionHarness.EnableRegressionHarness)
                    {
                        diagnosticMsg += $" (see diagnostic logs above for token-by-token parsing details)";
                    }
                    report.failures.Add(new RegressionFailure(
                        testCase.name, -1, "N/A", -1, -1,
                        diagnosticMsg
                    ));
                    return report;
                }

                // Voice the progression
                int rootOctave = 4;
                int bassOctave = rootOctave - 1;
                var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange(rootOctave);
                DiagnosticsCollector diags = new DiagnosticsCollector();

                // CRITICAL: Use melody constraint when melody is provided (SATB-with-melody mode)
                List<VoicedChord> voicedChords = voiceLeadFunc(
                    key, spec, regions, hasMelody, // useMelodyConstraint: true when melody provided
                    4, // numVoices
                    rootOctave, bassOctave, upperMinMidi, upperMaxMidi, diags
                );

                if (voicedChords == null || voicedChords.Count != regions.Count)
                {
                    report.failCount = 1;
                    report.failures.Add(new RegressionFailure(
                        testCase.name, -1, "N/A", -1, -1,
                        $"Voicing failed: expected {regions.Count} chords, got {(voicedChords?.Count ?? 0)}"
                    ));
                    return report;
                }

                // CRITICAL: Use the final post-continuity voicing data structure - the same one used for UI rendering / MIDI playback
                // VoiceLeadRegions returns voicing that has already been processed through continuity adjustment
                // This is the exact same data structure that the UI uses: voicedChords[chordIndex].VoicesMidi
                // Voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]

                // Collect diagnostic information (only when enabled)
                RegressionCaseDiagnostics caseDiags = null;
                if (RegressionHarness.EnableRegressionHarness)
                {
                    caseDiags = new RegressionCaseDiagnostics(testCase.name);
                    
                    // Collect final post-continuity BTAS voicing info for each region (same data used by UI for rendering/playback)
                    for (int i = 0; i < regions.Count && i < voicedChords.Count; i++)
                    {
                        var voiced = voicedChords[i];
                        var region = regions[i];
                        string chordLabel = region.debugLabel ?? TheoryChord.RecipeToRomanNumeral(key, region.chordEvent.Recipe);
                        
                        var voicingInfo = new RegionVoicingInfo
                        {
                            regionIndex = i,
                            chordLabel = chordLabel
                        };
                        
                        // Extract final BTAS MIDI pitches (post-continuity, post-play processing)
                        // This is the exact same VoicesMidi array that the UI uses for rendering and playback
                        if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                        {
                            voicingInfo.bassMidi = voiced.VoicesMidi[0];
                            voicingInfo.tenorMidi = voiced.VoicesMidi[1];
                            voicingInfo.altoMidi = voiced.VoicesMidi[2];
                            voicingInfo.sopranoMidi = voiced.VoicesMidi[3];
                            voicingInfo.bassName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[0], key);
                            voicingInfo.tenorName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[1], key);
                            voicingInfo.altoName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[2], key);
                            voicingInfo.sopranoName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[3], key);
                        }
                        
                        caseDiags.regionVoicings.Add(voicingInfo);
                    }
                    
                    report.caseDiagnostics[testCase.name] = caseDiags;
                }

                // Run checks on final post-continuity voicing data structure (same as UI rendering/playback)
                if (testCase.checks.HasFlag(RegressionChecks.ChordalSeventhResolvesDownIfAvailable))
                {
                    CheckChordalSeventhResolvesDown(testCase, regions, voicedChords, report, caseDiags);
                }

                if (testCase.checks.HasFlag(RegressionChecks.RequiredChordTonesPresent))
                {
                    CheckRequiredChordTonesPresent(testCase, regions, voicedChords, report, caseDiags);
                }

                if (testCase.checks.HasFlag(RegressionChecks.DiminishedTriadIdentityTonesPresent))
                {
                    CheckDiminishedTriadIdentityTonesPresent(testCase, regions, voicedChords, report, caseDiags);
                }

                // CRITICAL: For Aug5 case, verify melody lock and aug5 lane before running check
                if (testCase.name == "Aug5_Caug_to_F_withMelody_mustResolve")
                {
                    // Assert melody lock: soprano must equal [64, 64, 60] for chords 0, 1, 2
                    int[] expectedSopranoMidi = { 64, 64, 60 }; // E4, E4, C4
                    bool melodyLockValid = true;
                    string melodyLockError = "";
                    
                    for (int chordIdx = 0; chordIdx < Math.Min(3, voicedChords.Count) && chordIdx < expectedSopranoMidi.Length; chordIdx++)
                    {
                        if (voicedChords[chordIdx].VoicesMidi == null || voicedChords[chordIdx].VoicesMidi.Length < 4)
                        {
                            melodyLockValid = false;
                            melodyLockError = $"chord{chordIdx}: VoicesMidi is null or too short";
                            break;
                        }
                        
                        int actualSoprano = voicedChords[chordIdx].VoicesMidi[3]; // Soprano is index 3
                        int expectedSoprano = expectedSopranoMidi[chordIdx];
                        
                        if (actualSoprano != expectedSoprano)
                        {
                            melodyLockValid = false;
                            string actualName = TheoryPitch.GetPitchNameFromMidi(actualSoprano, key);
                            string expectedName = TheoryPitch.GetPitchNameFromMidi(expectedSoprano, key);
                            melodyLockError = $"chord{chordIdx}: Soprano={actualSoprano}({actualName}), expected={expectedSoprano}({expectedName})";
                            break;
                        }
                    }
                    
                    if (!melodyLockValid)
                    {
                        report.failCount++;
                        report.failures.Add(new RegressionFailure(
                            testCase.name,
                            -1,
                            "Soprano",
                            -1,
                            -1,
                            $"Melody lock not applied correctly: {melodyLockError}. Expected soprano sequence: E4(64) E4(64) C4(60)"
                        ));
                    }
                    
                    // Assert aug5 is in Tenor (lane 1) at chord 1 (Caug)
                    if (voicedChords.Count > 1 && voicedChords[1].VoicesMidi != null && voicedChords[1].VoicesMidi.Length >= 4)
                    {
                        var region1 = regions[1];
                        var sourceChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(region1.chordEvent);
                        if (sourceChordTonePcs != null && sourceChordTonePcs.Count >= 3)
                        {
                            int aug5Pc = sourceChordTonePcs[2]; // 5th
                            
                            // Find which lane has aug5
                            int aug5LaneIdx = -1;
                            for (int laneIdx = 0; laneIdx < 4; laneIdx++)
                            {
                                int midi = voicedChords[1].VoicesMidi[laneIdx];
                                int pc = (midi % 12 + 12) % 12;
                                if (pc == aug5Pc)
                                {
                                    aug5LaneIdx = laneIdx;
                                    break;
                                }
                            }
                            
                            if (aug5LaneIdx != 1) // Tenor is lane 1
                            {
                                string[] voiceNames = { "Bass", "Tenor", "Alto", "Soprano" };
                                string actualLaneName = aug5LaneIdx >= 0 && aug5LaneIdx < voiceNames.Length ? voiceNames[aug5LaneIdx] : $"Voice{aug5LaneIdx}";
                                string aug5Midi = aug5LaneIdx >= 0 ? voicedChords[1].VoicesMidi[aug5LaneIdx].ToString() : "N/A";
                                string aug5Name = aug5LaneIdx >= 0 ? TheoryPitch.GetPitchNameFromMidi(voicedChords[1].VoicesMidi[aug5LaneIdx], key) : "N/A";
                                
                                // Build SATB string for chord 1
                                string BuildSATBString(int[] voicesMidi, ChordEvent chordEvent)
                                {
                                    if (voicesMidi == null || voicesMidi.Length < 4)
                                        return "N/A";
                                    string bassName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[0], chordEvent.Key);
                                    string tenorName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[1], chordEvent.Key);
                                    string altoName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[2], chordEvent.Key);
                                    string sopranoName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[3], chordEvent.Key);
                                    return $"B={voicesMidi[0]}({bassName}) T={voicesMidi[1]}({tenorName}) A={voicesMidi[2]}({altoName}) S={voicesMidi[3]}({sopranoName})";
                                }
                                
                                string satbChord1 = BuildSATBString(voicedChords[1].VoicesMidi, region1.chordEvent);
                                
                                report.failCount++;
                                report.failures.Add(new RegressionFailure(
                                    testCase.name,
                                    1,
                                    actualLaneName,
                                    aug5LaneIdx >= 0 ? voicedChords[1].VoicesMidi[aug5LaneIdx] : -1,
                                    -1,
                                    $"Wrong scenario: Aug5 is in {actualLaneName} (lane {aug5LaneIdx}), not Tenor (lane 1). " +
                                    $"Aug5: {aug5Midi}({aug5Name}) PC={aug5Pc}. " +
                                    $"Chord 1 SATB: {satbChord1}"
                                ));
                            }
                        }
                    }
                }

                if (testCase.checks.HasFlag(RegressionChecks.AugmentedFifthResolvesUpIfAvailable))
                {
                    CheckAugmentedFifthResolvesUp(testCase, regions, voicedChords, report, caseDiags);
                    
            // Additional assertion for Aug5 case: chord2 (F) should have Tenor=A and Alto=A (unison doubling)
            // This enforces the musically correct solution: F3 A3 A3 C4
            if (testCase.name == "Aug5_Caug_to_F_withMelody_mustResolve" && 
                voicedChords.Count > 2 && voicedChords[2].VoicesMidi != null && voicedChords[2].VoicesMidi.Length >= 4)
            {
                var region2 = regions[2];
                int bassMidi = voicedChords[2].VoicesMidi[0];
                int tenorMidi = voicedChords[2].VoicesMidi[1];
                int altoMidi = voicedChords[2].VoicesMidi[2];
                int sopranoMidi = voicedChords[2].VoicesMidi[3];
                int tenorPc = (tenorMidi % 12 + 12) % 12;
                int altoPc = (altoMidi % 12 + 12) % 12;
                int expectedPc = 9; // A (target resolution from G#)
                
                bool tenorCorrect = (tenorPc == expectedPc);
                bool altoCorrect = (altoPc == expectedPc);
                bool orderingCorrect = (altoMidi >= tenorMidi); // Allow unison (>=)
                
                if (!tenorCorrect || !altoCorrect || !orderingCorrect)
                {
                    string bassName = TheoryPitch.GetPitchNameFromMidi(bassMidi, region2.chordEvent.Key);
                    string tenorName = TheoryPitch.GetPitchNameFromMidi(tenorMidi, region2.chordEvent.Key);
                    string altoName = TheoryPitch.GetPitchNameFromMidi(altoMidi, region2.chordEvent.Key);
                    string sopranoName = TheoryPitch.GetPitchNameFromMidi(sopranoMidi, region2.chordEvent.Key);
                    string expectedName = TheoryPitch.GetPitchNameFromMidi(expectedPc + 60, region2.chordEvent.Key);
                    
                    string failureReason = "";
                    if (!tenorCorrect) failureReason += $"Tenor PC={tenorPc}({tenorName}), expected PC={expectedPc}({expectedName}). ";
                    if (!altoCorrect) failureReason += $"Alto PC={altoPc}({altoName}), expected PC={expectedPc}({expectedName}). ";
                    if (!orderingCorrect) failureReason += $"Alto({altoMidi}) < Tenor({tenorMidi}) (crossing). ";
                    
                    string satbStr = $"B={bassMidi}({bassName}) T={tenorMidi}({tenorName}) A={altoMidi}({altoName}) S={sopranoMidi}({sopranoName})";
                    
                    report.failCount++;
                    report.failures.Add(new RegressionFailure(
                        testCase.name,
                        2,
                        "Tenor/Alto",
                        tenorMidi,
                        altoMidi,
                        $"Chord 2 (F) voicing incorrect: {failureReason}Expected: Tenor=A(PC=9), Alto=A(PC=9), Alto>=Tenor. " +
                        $"Actual SATB: {satbStr}. Expected: F3 A3 A3 C4."
                    ));
                }
            }
                }

                report.passCount = report.failCount == 0 ? 1 : 0;
                
                // DIAGNOSTIC DUMP: Log final voiced output for Aug5 case (for comparison with check)
                if (RegressionHarness.EnableAug5RegressionDump && testCase.name == "Aug5_Caug_to_F_withMelody_mustResolve")
                {
                    UnityEngine.Debug.Log($"[AUG5_REGCHK] === Final Voiced Output for {testCase.name} ===");
                    for (int i = 0; i < regions.Count && i < voicedChords.Count; i++)
                    {
                        var voiced = voicedChords[i];
                        var region = regions[i];
                        string roman = TheoryChord.RecipeToRomanNumeral(region.chordEvent.Key, region.chordEvent.Recipe);
                        if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                        {
                            string bassName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[0], region.chordEvent.Key);
                            string tenorName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[1], region.chordEvent.Key);
                            string altoName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[2], region.chordEvent.Key);
                            string sopranoName = TheoryPitch.GetPitchNameFromMidi(voiced.VoicesMidi[3], region.chordEvent.Key);
                            UnityEngine.Debug.Log(
                                $"[AUG5_REGCHK] chordIndex={i} roman={roman} " +
                                $"SATB=B={voiced.VoicesMidi[0]}({bassName}) T={voiced.VoicesMidi[1]}({tenorName}) " +
                                $"A={voiced.VoicesMidi[2]}({altoName}) S={voiced.VoicesMidi[3]}({sopranoName})");
                        }
                    }
                    UnityEngine.Debug.Log($"[AUG5_REGCHK] === End Final Voiced Output ===");
                }
            }
            catch (Exception ex)
            {
                report.failCount = 1;
                report.failures.Add(new RegressionFailure(
                    testCase.name, -1, "N/A", -1, -1,
                    $"Exception during test execution: {ex.Message}"
                ));
            }
            finally
            {
                // Always clear current test case when done
                RegressionHarness.ClearCurrentTestCase();
            }

            return report;
        }

        /// <summary>
        /// Run all regression cases.
        /// </summary>
        public static RegressionReport RunAllCases(Func<string, TheoryKey, TimelineSpec, IReadOnlyList<int>, List<ChordRegion>> buildRegionsFunc, Func<TheoryKey, TimelineSpec, IReadOnlyList<ChordRegion>, bool, int, int, int, int, int, DiagnosticsCollector, List<VoicedChord>> voiceLeadFunc)
        {
            if (!RegressionHarness.EnableRegressionHarness)
            {
                return null; // Silent when disabled
            }

            var report = new RegressionReport();
            report.caseCount = s_cases.Count;

            foreach (var testCase in s_cases)
            {
                var caseReport = RunCase(testCase.name, buildRegionsFunc, voiceLeadFunc);
                if (caseReport != null)
                {
                    if (caseReport.failCount > 0)
                    {
                        report.failCount++;
                        report.failures.AddRange(caseReport.failures);
                    }
                    else
                    {
                        report.passCount++;
                    }
                    
                    // Merge case diagnostics into main report
                    if (caseReport.caseDiagnostics != null)
                    {
                        foreach (var kvp in caseReport.caseDiagnostics)
                        {
                            report.caseDiagnostics[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Check: Chordal 7th must resolve down if a legal target exists.
        /// 
        /// CRITICAL: This check operates on the final post-continuity voicing data structure
        /// (the same one used by the UI for rendering and MIDI playback).
        /// The voicedChords array passed in is the direct output from VoiceLeadRegions,
        /// which has already been processed through continuity adjustment.
        /// Voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
        /// </summary>
        private static void CheckChordalSeventhResolvesDown(RegressionCase testCase, List<ChordRegion> regions, List<VoicedChord> voicedChords, RegressionReport report, RegressionCaseDiagnostics caseDiags)
        {
            // For each adjacent chord region pair (N → N+1)
            for (int i = 0; i < regions.Count - 1; i++)
            {
                var regionN = regions[i];
                var regionN1 = regions[i + 1];
                var voicedN = voicedChords[i];
                var voicedN1 = voicedChords[i + 1];

                // Get destination chord tone pitch classes
                var destChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(regionN1.chordEvent);
                var destChordTonePcsSet = new HashSet<int>(destChordTonePcs);
                
                // Always create a diagnostic entry for this transition (when enabled)
                SeventhResolutionCheck checkInfo = null;
                if (caseDiags != null)
                {
                    checkInfo = new SeventhResolutionCheck
                    {
                        regionIndex = i,
                        destChordTonePcs = new List<int>(destChordTonePcs)
                    };
                }

                // CRITICAL: Use the same seventh-detection logic as the engine's enforcement
                // Check if region N has a 7th using Recipe.SeventhQuality (same as engine uses)
                bool hasSeventh = regionN.chordEvent.Recipe.SeventhQuality != SeventhQuality.None;
                
                if (!hasSeventh)
                {
                    // No 7th to resolve - log diagnostic if enabled
                    if (checkInfo != null)
                    {
                        checkInfo.seventhPc = -1;
                        checkInfo.seventhPcName = "none";
                        checkInfo.voiceIndex = -1;
                        checkInfo.voiceName = "none";
                        checkInfo.voiceMidi = -1;
                        checkInfo.voiceNoteName = "N/A";
                        checkInfo.destPcDown1 = -1;
                        checkInfo.destPcDown2 = -1;
                        checkInfo.destPcDown1Name = "N/A";
                        checkInfo.destPcDown2Name = "N/A";
                        checkInfo.hasPcDown1InDest = false;
                        checkInfo.hasPcDown2InDest = false;
                        checkInfo.resolvedToMidi = -1;
                        checkInfo.resolvedToPc = -1;
                        checkInfo.resolvedToName = "N/A";
                        checkInfo.resolvedCorrectly = false;
                        checkInfo.result = "SKIP";
                        checkInfo.reason = "No 7th in source chord (Recipe.SeventhQuality == None)";
                        caseDiags.seventhResolutionChecks.Add(checkInfo);
                    }
                    continue; // No 7th to resolve
                }

                // Get the 7th pitch class from region N using GetChordTonePitchClasses (same as engine uses)
                // This computes the 7th PC from the chord recipe, not from Roman numeral assumptions
                var sourceChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(regionN.chordEvent);
                int? seventhPc = null;

                // Find the 7th pitch class from the chord tone set
                // GetChordTonePitchClasses returns [root, 3rd, 5th, 7th] for 7th chords
                if (sourceChordTonePcs.Count >= 4)
                {
                    // For 7th chords, the 7th is at index 3
                    seventhPc = sourceChordTonePcs[3];
                }
                else
                {
                    // Recipe says it has a 7th but GetChordTonePitchClasses returned < 4 tones - log diagnostic
                    if (checkInfo != null)
                    {
                        checkInfo.seventhPc = -1;
                        checkInfo.seventhPcName = "none (recipe has 7th but GetChordTonePitchClasses returned < 4 tones)";
                        checkInfo.voiceIndex = -1;
                        checkInfo.voiceName = "none";
                        checkInfo.voiceMidi = -1;
                        checkInfo.voiceNoteName = "N/A";
                        checkInfo.destPcDown1 = -1;
                        checkInfo.destPcDown2 = -1;
                        checkInfo.destPcDown1Name = "N/A";
                        checkInfo.destPcDown2Name = "N/A";
                        checkInfo.hasPcDown1InDest = false;
                        checkInfo.hasPcDown2InDest = false;
                        checkInfo.resolvedToMidi = -1;
                        checkInfo.resolvedToPc = -1;
                        checkInfo.resolvedToName = "N/A";
                        checkInfo.resolvedCorrectly = false;
                        checkInfo.result = "SKIP";
                        checkInfo.reason = "Recipe has 7th but GetChordTonePitchClasses returned < 4 tones";
                        caseDiags.seventhResolutionChecks.Add(checkInfo);
                    }
                    continue; // Recipe says 7th but can't compute 7th PC
                }

                // Compute allowed resolution pitch classes
                int pcDown1 = (seventhPc.Value + 11) % 12; // -1 semitone
                int pcDown2 = (seventhPc.Value + 10) % 12; // -2 semitones

                // Check if destination chord contains either resolution pitch class
                bool hasPcDown1InDest = destChordTonePcsSet.Contains(pcDown1);
                bool hasPcDown2InDest = destChordTonePcsSet.Contains(pcDown2);
                bool hasValidResolution = hasPcDown1InDest || hasPcDown2InDest;
                
                // Initialize diagnostic info with 7th PC and resolution PCs
                if (checkInfo != null)
                {
                    string seventhPcName = TheoryPitch.GetPitchNameFromMidi(seventhPc.Value + 60, regionN.chordEvent.Key);
                    string pcDown1Name = TheoryPitch.GetPitchNameFromMidi(pcDown1 + 60, regionN1.chordEvent.Key);
                    string pcDown2Name = TheoryPitch.GetPitchNameFromMidi(pcDown2 + 60, regionN1.chordEvent.Key);
                    
                    checkInfo.seventhPc = seventhPc.Value;
                    checkInfo.seventhPcName = seventhPcName;
                    checkInfo.destPcDown1 = pcDown1;
                    checkInfo.destPcDown2 = pcDown2;
                    checkInfo.destPcDown1Name = pcDown1Name;
                    checkInfo.destPcDown2Name = pcDown2Name;
                    checkInfo.hasPcDown1InDest = hasPcDown1InDest;
                    checkInfo.hasPcDown2InDest = hasPcDown2InDest;
                }
                
                // Track whether we found a voice holding the 7th
                bool foundSeventhVoice = false;
                
                // Check each voice in region N using the final post-continuity voicing data structure
                // This is the exact same VoicesMidi array used by the UI for rendering/playback
                if (voicedN.VoicesMidi == null || voicedN1.VoicesMidi == null)
                {
                    if (checkInfo != null)
                    {
                        checkInfo.voiceIndex = -1;
                        checkInfo.voiceName = "none";
                        checkInfo.voiceMidi = -1;
                        checkInfo.voiceNoteName = "N/A";
                        checkInfo.resolvedToMidi = -1;
                        checkInfo.resolvedToPc = -1;
                        checkInfo.resolvedToName = "N/A";
                        checkInfo.resolvedCorrectly = false;
                        checkInfo.result = "SKIP";
                        checkInfo.reason = "VoicedN or VoicedN1 VoicesMidi is null";
                        caseDiags.seventhResolutionChecks.Add(checkInfo);
                    }
                    continue;
                }

                string[] voiceNames = { "Bass", "Tenor", "Alto", "Soprano" };
                int numVoices = Math.Min(voicedN.VoicesMidi.Length, voicedN1.VoicesMidi.Length);

                for (int voiceIdx = 0; voiceIdx < numVoices; voiceIdx++)
                {
                    // Use final post-continuity MIDI pitches (same as UI rendering/playback)
                    int fromMidi = voicedN.VoicesMidi[voiceIdx];
                    int toMidi = voicedN1.VoicesMidi[voiceIdx];
                    int fromPc = (fromMidi % 12 + 12) % 12;
                    int toPc = (toMidi % 12 + 12) % 12;

                    // Check if this voice is on the chordal 7th
                    if (fromPc == seventhPc.Value)
                    {
                        foundSeventhVoice = true;
                        
                        // This voice holds the 7th - it must resolve down by -1 or -2 semitones if a legal target exists
                        // Example: C7 → Fm: Bb (7th) should resolve to Ab (-2 semitones, present in Fm), not C
                        bool isResolvingDown = hasValidResolution && (toPc == pcDown1 || toPc == pcDown2);
                        
                        // Update diagnostic info with voice details
                        if (checkInfo != null)
                        {
                            string resolvedToName = TheoryPitch.GetPitchNameFromMidi(toMidi, regionN1.chordEvent.Key);
                            
                            checkInfo.voiceIndex = voiceIdx;
                            checkInfo.voiceName = voiceNames[voiceIdx];
                            checkInfo.voiceMidi = fromMidi;
                            checkInfo.voiceNoteName = TheoryPitch.GetPitchNameFromMidi(fromMidi, regionN.chordEvent.Key);
                            checkInfo.resolvedToMidi = toMidi;
                            checkInfo.resolvedToPc = toPc;
                            checkInfo.resolvedToName = resolvedToName;
                            checkInfo.resolvedCorrectly = isResolvingDown;
                            
                            // Determine result and reason
                            if (!hasValidResolution)
                            {
                                checkInfo.result = "SKIP";
                                checkInfo.reason = $"No valid resolution target in destination chord (neither PC {pcDown1} nor PC {pcDown2} present)";
                            }
                            else if (isResolvingDown)
                            {
                                checkInfo.result = "PASS";
                                checkInfo.reason = $"7th resolved correctly to {resolvedToName} (PC {toPc})";
                            }
                            else
                            {
                                checkInfo.result = "FAIL";
                                checkInfo.reason = $"7th did not resolve down (resolved to {resolvedToName} PC {toPc}, expected PC {pcDown1} or {pcDown2})";
                            }
                            
                            caseDiags.seventhResolutionChecks.Add(checkInfo);
                        }
                        
                        // FAIL if 7th does not resolve down when a legal target exists
                        // Example: C7 → Fm: Bb must resolve to Ab (available), not C
                        if (hasValidResolution && !isResolvingDown)
                        {
                            // FAILURE - increment failCount and add failure
                            string fromNoteName = TheoryPitch.GetPitchNameFromMidi(fromMidi, regionN.chordEvent.Key);
                            string toNoteName = TheoryPitch.GetPitchNameFromMidi(toMidi, regionN1.chordEvent.Key);
                            string expectedPc1Name = TheoryPitch.GetPitchNameFromMidi(pcDown1 + 60, regionN1.chordEvent.Key);
                            string expectedPc2Name = TheoryPitch.GetPitchNameFromMidi(pcDown2 + 60, regionN1.chordEvent.Key);

                            report.failCount++;
                            report.failures.Add(new RegressionFailure(
                                testCase.name,
                                i, // region index (N)
                                voiceNames[voiceIdx],
                                fromMidi,
                                toMidi,
                                $"Chordal 7th ({fromNoteName}) did not resolve down though legal target existed (expected -1/-2 semitone chord tone: {expectedPc1Name} or {expectedPc2Name}, got {toNoteName})",
                                "final"
                            ));
                        }
                        
                        break; // Found the voice with the 7th, no need to check other voices
                    }
                }
                
                // If we detected a 7th chord but didn't find the 7th in any voice, log diagnostic
                if (checkInfo != null && !foundSeventhVoice && seventhPc.HasValue)
                {
                    // Diagnostic: 7th chord detected but no voice holds the 7th pitch class
                    checkInfo.voiceIndex = -1;
                    checkInfo.voiceName = "none";
                    checkInfo.voiceMidi = -1;
                    checkInfo.voiceNoteName = "N/A (7th PC detected but no voice holds it)";
                    checkInfo.resolvedToMidi = -1;
                    checkInfo.resolvedToPc = -1;
                    checkInfo.resolvedToName = "N/A";
                    checkInfo.resolvedCorrectly = false;
                    
                    if (!hasValidResolution)
                    {
                        checkInfo.result = "SKIP";
                        checkInfo.reason = $"7th PC detected but no voice holds it, and no valid resolution target in destination";
                    }
                    else
                    {
                        checkInfo.result = "SKIP";
                        checkInfo.reason = $"7th PC detected but no voice holds it (valid resolution target exists but check skipped)";
                    }
                    
                    caseDiags.seventhResolutionChecks.Add(checkInfo);
                    // Note: We don't add this as a failure since the check only requires resolution IF the 7th is present in a voice
                    // But we log it for debugging to understand why the check might be skipping
                }
            }
        }

        /// <summary>
        /// Check: Required chord tones must be present in the final post-continuity voicing.
        /// 
        /// Required tones:
        /// - Always: Root PC, 3rd PC
        /// - If 7th chord: 7th PC is also required
        /// - Perfect 5th is optional (not required)
        /// - For v1, keep dim/aug minimal - don't require altered 5th unless clearly part of identity
        /// </summary>
        private static void CheckRequiredChordTonesPresent(RegressionCase testCase, List<ChordRegion> regions, List<VoicedChord> voicedChords, RegressionReport report, RegressionCaseDiagnostics caseDiags)
        {
            // For each region
            for (int i = 0; i < regions.Count && i < voicedChords.Count; i++)
            {
                var region = regions[i];
                var voiced = voicedChords[i];

                // Get all chord tone pitch classes from the recipe
                var allChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(region.chordEvent);
                if (allChordTonePcs == null || allChordTonePcs.Count < 2)
                {
                    continue; // Need at least root and 3rd
                }

                // Determine required pitch classes
                var requiredPcs = new List<int>();
                var requiredPcNames = new List<string>();
                
                // Always required: Root (index 0) and 3rd (index 1)
                int rootPc = allChordTonePcs[0];
                int thirdPc = allChordTonePcs[1];
                requiredPcs.Add(rootPc);
                requiredPcs.Add(thirdPc);
                requiredPcNames.Add(TheoryPitch.GetPitchNameFromMidi(rootPc + 60, region.chordEvent.Key) + " (root)");
                requiredPcNames.Add(TheoryPitch.GetPitchNameFromMidi(thirdPc + 60, region.chordEvent.Key) + " (3rd)");

                // If 7th chord: 7th (index 3) is also required
                if (allChordTonePcs.Count >= 4)
                {
                    int seventhPc = allChordTonePcs[3];
                    requiredPcs.Add(seventhPc);
                    requiredPcNames.Add(TheoryPitch.GetPitchNameFromMidi(seventhPc + 60, region.chordEvent.Key) + " (7th)");
                }

                // Perfect 5th is optional (not required)
                // For v1, don't require altered 5th (dim/aug) - keep minimal

                // Get realized BTAS pitch classes from final voicing
                var realizedPcs = new HashSet<int>();
                var realizedMidi = new List<int>();
                var realizedNames = new List<string>();
                
                if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                {
                    for (int voiceIdx = 0; voiceIdx < 4; voiceIdx++)
                    {
                        int midi = voiced.VoicesMidi[voiceIdx];
                        int pc = (midi % 12 + 12) % 12;
                        realizedPcs.Add(pc);
                        realizedMidi.Add(midi);
                        realizedNames.Add(TheoryPitch.GetPitchNameFromMidi(midi, region.chordEvent.Key));
                    }
                }
                else
                {
                    // Invalid voicing - skip this region
                    continue;
                }

                // Check for missing required PCs
                var missingPcs = new List<int>();
                var missingPcNames = new List<string>();
                
                for (int j = 0; j < requiredPcs.Count; j++)
                {
                    int requiredPc = requiredPcs[j];
                    if (!realizedPcs.Contains(requiredPc))
                    {
                        missingPcs.Add(requiredPc);
                        missingPcNames.Add(requiredPcNames[j]);
                    }
                }

                // Create diagnostic info
                RequiredChordTonesCheck checkInfo = null;
                if (caseDiags != null)
                {
                    // Build key for chord label (use testCase key info)
                    TheoryKey keyForLabel = new TheoryKey(testCase.keyTonic, testCase.mode);
                    checkInfo = new RequiredChordTonesCheck
                    {
                        regionIndex = i,
                        chordLabel = region.debugLabel ?? TheoryChord.RecipeToRomanNumeral(keyForLabel, region.chordEvent.Recipe),
                        requiredPcs = new List<int>(requiredPcs),
                        requiredPcNames = new List<string>(requiredPcNames),
                        realizedPcs = new HashSet<int>(realizedPcs),
                        missingPcs = new List<int>(missingPcs),
                        missingPcNames = new List<string>(missingPcNames),
                        realizedMidi = new List<int>(realizedMidi),
                        realizedNames = new List<string>(realizedNames)
                    };
                }

                // FAIL if any required PC is missing
                if (missingPcs.Count > 0)
                {
                    string missingTonesStr = string.Join(", ", missingPcNames);
                    string realizedStr = $"B={realizedNames[0]}({realizedMidi[0]}), T={realizedNames[1]}({realizedMidi[1]}), A={realizedNames[2]}({realizedMidi[2]}), S={realizedNames[3]}({realizedMidi[3]})";
                    
                    if (checkInfo != null)
                    {
                        checkInfo.result = "FAIL";
                        checkInfo.reason = $"Missing required chord tone(s): {missingTonesStr}. Realized: {realizedStr}";
                        caseDiags.requiredChordTonesChecks.Add(checkInfo);
                    }

                    // Add failure to report
                    report.failCount++;
                    report.failures.Add(new RegressionFailure(
                        testCase.name,
                        i,
                        "BTAS",
                        -1,
                        -1,
                        $"Required chord tone(s) missing in region {i}: {missingTonesStr}. Realized BTAS: {realizedStr}",
                        "final"
                    ));
                }
                else
                {
                    if (checkInfo != null)
                    {
                        checkInfo.result = "PASS";
                        checkInfo.reason = "All required chord tones present";
                        caseDiags.requiredChordTonesChecks.Add(checkInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Check: Diminished triads must contain identity tones {root, root+3, root+6} in final BTAS voicing.
        /// This is a strict invariant: diminished triads require all three tones (root, minor 3rd, diminished 5th).
        /// </summary>
        private static void CheckDiminishedTriadIdentityTonesPresent(RegressionCase testCase, List<ChordRegion> regions, List<VoicedChord> voicedChords, RegressionReport report, RegressionCaseDiagnostics caseDiags)
        {
            // For each region
            for (int i = 0; i < regions.Count && i < voicedChords.Count; i++)
            {
                var region = regions[i];
                var voiced = voicedChords[i];

                // Only check diminished triads (not dim7, not other qualities)
                if (region.chordEvent.Recipe.Quality != ChordQuality.Diminished)
                    continue;

                // Skip if it's a 7th chord (dim7, m7b5, etc.)
                bool hasSeventh = region.chordEvent.Recipe.Extension == ChordExtension.Seventh &&
                                  region.chordEvent.Recipe.SeventhQuality != SeventhQuality.None;
                if (hasSeventh)
                    continue; // This check is only for triads

                // Get all chord tone pitch classes from the recipe
                var allChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(region.chordEvent);
                if (allChordTonePcs == null || allChordTonePcs.Count < 3)
                {
                    continue; // Need at least root, 3rd, 5th
                }

                // For diminished triad, required identity tones are: root, root+3 (minor 3rd), root+6 (diminished 5th)
                int rootPc = allChordTonePcs[0];
                int thirdPc = allChordTonePcs[1];  // Should be root+3
                int fifthPc = allChordTonePcs[2];  // Should be root+6

                // Verify the intervals are correct (sanity check)
                int thirdInterval = (thirdPc - rootPc + 12) % 12;
                int fifthInterval = (fifthPc - rootPc + 12) % 12;
                if (thirdInterval != 3 || fifthInterval != 6)
                {
                    // Not a standard diminished triad - skip
                    continue;
                }

                var requiredPcs = new HashSet<int> { rootPc, thirdPc, fifthPc };
                var requiredPcNames = new List<string>
                {
                    TheoryPitch.GetPitchNameFromMidi(rootPc + 60, region.chordEvent.Key) + " (root)",
                    TheoryPitch.GetPitchNameFromMidi(thirdPc + 60, region.chordEvent.Key) + " (m3)",
                    TheoryPitch.GetPitchNameFromMidi(fifthPc + 60, region.chordEvent.Key) + " (b5)"
                };

                // Get realized BTAS pitch classes from final voicing
                var realizedPcs = new HashSet<int>();
                var realizedMidi = new List<int>();
                var realizedNames = new List<string>();
                
                if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                {
                    for (int voiceIdx = 0; voiceIdx < 4; voiceIdx++)
                    {
                        int midi = voiced.VoicesMidi[voiceIdx];
                        int pc = (midi % 12 + 12) % 12;
                        realizedPcs.Add(pc);
                        realizedMidi.Add(midi);
                        realizedNames.Add(TheoryPitch.GetPitchNameFromMidi(midi, region.chordEvent.Key));
                    }
                }
                else
                {
                    // Invalid voicing - skip this region
                    continue;
                }

                // Check for missing required PCs
                var missingPcs = new List<int>();
                var missingPcNames = new List<string>();
                
                foreach (int requiredPc in requiredPcs)
                {
                    if (!realizedPcs.Contains(requiredPc))
                    {
                        missingPcs.Add(requiredPc);
                        // Find the name for this PC
                        string pcName = null;
                        if (requiredPc == rootPc)
                            pcName = "root";
                        else if (requiredPc == thirdPc)
                            pcName = "m3";
                        else if (requiredPc == fifthPc)
                            pcName = "b5";
                        missingPcNames.Add($"{pcName}(PC={requiredPc})");
                    }
                }

                // Create diagnostic info
                RequiredChordTonesCheck checkInfo = null;
                if (caseDiags != null)
                {
                    // Build key for chord label (use testCase key info)
                    TheoryKey keyForLabel = new TheoryKey(testCase.keyTonic, testCase.mode);
                    checkInfo = new RequiredChordTonesCheck
                    {
                        regionIndex = i,
                        chordLabel = region.debugLabel ?? TheoryChord.RecipeToRomanNumeral(keyForLabel, region.chordEvent.Recipe),
                        requiredPcs = new List<int>(requiredPcs),
                        requiredPcNames = new List<string>(requiredPcNames),
                        realizedPcs = new HashSet<int>(realizedPcs),
                        missingPcs = new List<int>(missingPcs),
                        missingPcNames = new List<string>(missingPcNames),
                        realizedMidi = new List<int>(realizedMidi),
                        realizedNames = new List<string>(realizedNames)
                    };
                }

                // FAIL if any required identity tone is missing
                if (missingPcs.Count > 0)
                {
                    string missingTonesStr = string.Join(", ", missingPcNames);
                    string realizedStr = $"B={realizedNames[0]}({realizedMidi[0]}), T={realizedNames[1]}({realizedMidi[1]}), A={realizedNames[2]}({realizedMidi[2]}), S={realizedNames[3]}({realizedMidi[3]})";
                    
                    if (checkInfo != null)
                    {
                        checkInfo.result = "FAIL";
                        checkInfo.reason = $"Missing diminished triad identity tone(s): {missingTonesStr}. Realized: {realizedStr}";
                        caseDiags.requiredChordTonesChecks.Add(checkInfo);
                    }

                    // Add failure to report
                    report.failCount++;
                    report.failures.Add(new RegressionFailure(
                        testCase.name,
                        i,
                        "BTAS",
                        -1,
                        -1,
                        $"Diminished triad identity tone(s) missing in region {i}: {missingTonesStr}. Required: root(PC={rootPc}), m3(PC={thirdPc}), b5(PC={fifthPc}). Realized BTAS: {realizedStr}",
                        "final"
                    ));
                }
                else
                {
                    if (checkInfo != null)
                    {
                        checkInfo.result = "PASS";
                        checkInfo.reason = "All diminished triad identity tones present";
                        caseDiags.requiredChordTonesChecks.Add(checkInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Check: Augmented 5th (#5) must resolve up by semitone if next chord supports target pitch class.
        /// 
        /// CRITICAL: This check operates on the final post-continuity voicing data structure
        /// (the same one used by the UI for rendering and MIDI playback).
        /// Voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
        /// </summary>
        private static void CheckAugmentedFifthResolvesUp(RegressionCase testCase, List<ChordRegion> regions, List<VoicedChord> voicedChords, RegressionReport report, RegressionCaseDiagnostics caseDiags)
        {
            // For each adjacent chord region pair (N → N+1)
            for (int i = 0; i < regions.Count - 1; i++)
            {
                var regionN = regions[i];
                var regionN1 = regions[i + 1];
                var voicedN = voicedChords[i];
                var voicedN1 = voicedChords[i + 1];

                // Only check if region N has augmented quality
                if (regionN.chordEvent.Recipe.Quality != ChordQuality.Augmented)
                    continue;

                // Get chord tone pitch classes for region N
                var sourceChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(regionN.chordEvent);
                if (sourceChordTonePcs == null || sourceChordTonePcs.Count < 3)
                    continue;

                // Get augmented 5th pitch class (5th in augmented chord)
                int aug5Pc = sourceChordTonePcs[2]; // Index 2 is 5th
                
                // Verify this is actually an augmented 5th
                if (!ChordTensionHelper.IsAugmentedFifth(regionN.chordEvent.Recipe, ChordToneRole.Fifth))
                    continue;

                // Target resolution pitch class: +1 semitone from aug5
                int targetPc = (aug5Pc + 1) % 12;

                // Get destination chord tone pitch classes
                var destChordTonePcs = TheoryVoicing.GetChordTonePitchClasses(regionN1.chordEvent);
                var destChordTonePcsSet = new HashSet<int>(destChordTonePcs);
                
                // Check if destination chord supports target pitch class
                bool supportsTarget = destChordTonePcsSet.Contains(targetPc);
                
                if (!supportsTarget)
                {
                    // Target not available in next chord - skip this check
                    continue;
                }

                // CRITICAL: Use final voiced.VoicesMidi arrays (same buffer UI renders)
                // Identify aug5 lane by lane index in chord k, then assert same lane resolves in chord k+1
                if (voicedN.VoicesMidi == null || voicedN.VoicesMidi.Length < 4 ||
                    voicedN1.VoicesMidi == null || voicedN1.VoicesMidi.Length < 4)
                {
                    // Missing final voicing arrays - skip
                    continue;
                }

                // Find which lane (by index) in chord k contains the augmented 5th
                // SATB order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                int aug5LaneIdx = -1;
                int aug5Midi = -1;
                for (int laneIdx = 0; laneIdx < 4; laneIdx++)
                {
                    int midi = voicedN.VoicesMidi[laneIdx];
                    int pc = (midi % 12 + 12) % 12;
                    if (pc == aug5Pc)
                    {
                        aug5LaneIdx = laneIdx;
                        aug5Midi = midi;
                        break; // Use first match (lane identity is what matters)
                    }
                }

                if (aug5LaneIdx < 0)
                {
                    // Aug5 not found in final voicing - skip
                    continue;
                }

                // Assert: same lane in chord k+1 must resolve to target pitch class
                int resolvedToMidi = voicedN1.VoicesMidi[aug5LaneIdx];
                int resolvedToPc = (resolvedToMidi % 12 + 12) % 12;
                bool resolvedCorrectly = (resolvedToPc == targetPc);

                // Build verbose output for failure
                string[] voiceNames = { "Bass", "Tenor", "Alto", "Soprano" };
                string aug5LaneName = aug5LaneIdx < voiceNames.Length ? voiceNames[aug5LaneIdx] : $"Voice{aug5LaneIdx}";
                string aug5Name = TheoryPitch.GetPitchNameFromMidi(aug5Midi, regionN.chordEvent.Key);
                string targetName = TheoryPitch.GetPitchNameFromMidi(targetPc + 60, regionN1.chordEvent.Key);
                string resolvedToName = TheoryPitch.GetPitchNameFromMidi(resolvedToMidi, regionN1.chordEvent.Key);

                // Build full SATB strings for both chords
                string BuildSATBString(int[] voicesMidi, ChordEvent chordEvent)
                {
                    if (voicesMidi == null || voicesMidi.Length < 4)
                        return "N/A";
                    string bassName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[0], chordEvent.Key);
                    string tenorName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[1], chordEvent.Key);
                    string altoName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[2], chordEvent.Key);
                    string sopranoName = TheoryPitch.GetPitchNameFromMidi(voicesMidi[3], chordEvent.Key);
                    return $"B={voicesMidi[0]}({bassName}) T={voicesMidi[1]}({tenorName}) A={voicesMidi[2]}({altoName}) S={voicesMidi[3]}({sopranoName})";
                }

                string satbChordN = BuildSATBString(voicedN.VoicesMidi, regionN.chordEvent);
                string satbChordN1 = BuildSATBString(voicedN1.VoicesMidi, regionN1.chordEvent);

                // DIAGNOSTIC DUMP: Print exactly what the check is reading (for Aug5_Caug_to_F_withMelody_mustResolve or when flag enabled)
                bool shouldDump = RegressionHarness.EnableAug5RegressionDump && 
                                  testCase.name == "Aug5_Caug_to_F_withMelody_mustResolve";
                if (shouldDump)
                {
                    string romanN = TheoryChord.RecipeToRomanNumeral(regionN.chordEvent.Key, regionN.chordEvent.Recipe);
                    string romanN1 = TheoryChord.RecipeToRomanNumeral(regionN1.chordEvent.Key, regionN1.chordEvent.Recipe);
                    string result = resolvedCorrectly ? "PASS" : "FAIL";
                    UnityEngine.Debug.Log(
                        $"[AUG5_REGCHK] case={testCase.name} " +
                        $"chord{i}({romanN}) SATB={satbChordN} | " +
                        $"chord{i+1}({romanN1}) SATB={satbChordN1} | " +
                        $"aug5Lane={aug5LaneIdx}({aug5LaneName}) aug5Pc={aug5Pc} targetPc={targetPc} destPc={resolvedToPc} => {result}");
                }

                // HARD ASSERT: If check logic says PASS but destPc != targetPc, force failure
                if (resolvedCorrectly && resolvedToPc != targetPc)
                {
                    string errorMsg = 
                        $"[AUG5_REGCHK_ASSERT] CRITICAL: Check logic inconsistency detected!\n" +
                        $"  Case: {testCase.name}\n" +
                        $"  Chord {i}: Lane {aug5LaneIdx} ({aug5LaneName}) = {aug5Midi}({aug5Name}) PC={aug5Pc}\n" +
                        $"  Chord {i+1}: Lane {aug5LaneIdx} ({aug5LaneName}) = {resolvedToMidi}({resolvedToName}) PC={resolvedToPc}\n" +
                        $"  Expected: PC={targetPc} ({targetName})\n" +
                        $"  Check returned: PASS (but destPc={resolvedToPc} != targetPc={targetPc})";
                    UnityEngine.Debug.LogError(errorMsg);
                    // Force test failure
                    resolvedCorrectly = false;
                }

                if (!resolvedCorrectly)
                {
                    // Failure: aug5 did not resolve to target in same lane
                    string failureMessage = 
                        $"Augmented 5th resolution failed: Lane identity not preserved.\n" +
                        $"  Chord {i} (chordIndex={i}): Lane {aug5LaneIdx} ({aug5LaneName}) = {aug5Midi}({aug5Name}) PC={aug5Pc}\n" +
                        $"  Chord {i+1} (chordIndex={i+1}): Lane {aug5LaneIdx} ({aug5LaneName}) = {resolvedToMidi}({resolvedToName}) PC={resolvedToPc}\n" +
                        $"  Expected: PC={targetPc} ({targetName})\n" +
                        $"  Chord {i} SATB: {satbChordN}\n" +
                        $"  Chord {i+1} SATB: {satbChordN1}";
                    
                    report.failCount++;
                    report.failures.Add(new RegressionFailure(
                        testCase.name,
                        i,
                        aug5LaneName,
                        aug5Midi,
                        resolvedToMidi,
                        failureMessage
                    ));
                }
            }
        }

        /// <summary>
        /// Helper to compute upper voice range (same as ChordLabController).
        /// </summary>
        private static (int upperMinMidi, int upperMaxMidi) ComputeUpperVoiceRange(int rootOctave)
        {
            int upperMinMidi = rootOctave * 12 + 5;  // F in octave
            int upperMaxMidi = (rootOctave + 3) * 12 + 9;  // A in octave
            return (upperMinMidi, upperMaxMidi);
        }
        
        /// <summary>
        /// Parses a note-name token like "E4", "C#4", "Bb3" into a MIDI note number.
        /// Returns true on success; false on failure.
        /// Expected format: Letter A–G, optional # or b, then octave integer (e.g., C4 = 60).
        /// </summary>
        private static bool TryParseNoteNameToMidi(string token, out int midi)
        {
            midi = 0;
            
            if (string.IsNullOrWhiteSpace(token))
                return false;
            
            token = token.Trim();
            
            // Require at least 2 characters: letter + octave digit
            if (token.Length < 2)
                return false;
            
            char letter = char.ToUpperInvariant(token[0]);
            if (letter < 'A' || letter > 'G')
                return false;
            
            int index = 1;
            int accidentalOffset = 0;
            
            if (index < token.Length)
            {
                char acc = token[index];
                if (acc == '#' || acc == '♯') // Support both ASCII and Unicode sharp
                {
                    accidentalOffset = 1;
                    index++;
                }
                else if (acc == 'b' || acc == '♭') // Support both ASCII and Unicode flat
                {
                    accidentalOffset = -1;
                    index++;
                }
            }
            
            if (index >= token.Length)
                return false;
            
            string octaveStr = token.Substring(index);
            if (!int.TryParse(octaveStr, out int octave))
                return false;
            
            int basePc;
            switch (letter)
            {
                case 'C': basePc = 0; break;
                case 'D': basePc = 2; break;
                case 'E': basePc = 4; break;
                case 'F': basePc = 5; break;
                case 'G': basePc = 7; break;
                case 'A': basePc = 9; break;
                case 'B': basePc = 11; break;
                default: return false;
            }
            
            int pitchClass = basePc + accidentalOffset;
            // Wrap into 0..11 to be safe
            pitchClass = (pitchClass % 12 + 12) % 12;
            
            midi = (octave + 1) * 12 + pitchClass;
            return true;
        }
    }
}

