using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sonoria.MusicTheory;
using Sonoria.MusicTheory.Timeline;
using Sonoria.MusicTheory.Diagnostics;
using MelodyEvent = Sonoria.MusicTheory.MelodyEvent;

namespace EarFPS
{
    /// <summary>
    /// Macro mode for voicing settings. Off = manual tuning, DriveAdvanced = macros overwrite advanced values.
    /// </summary>
    public enum VoicingMacroMode
    {
        Off,
        DriveAdvanced
    }

    /// <summary>
    /// Represents the active harmonic input source for Chord Lab playback.
    /// </summary>
    public enum HarmonicInputSource
    {
        None,
        Roman,
        Absolute
    }

    /// <summary>
    /// Controller for the Chord Lab panel - allows users to input Roman numeral progressions
    /// and play them using the TheoryChord system. Supports arbitrary tonic selection.
    /// </summary>
    public class ChordLabController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button buttonPlay;
        [SerializeField] private Button buttonNaiveHarmonize; // Optional: runtime button for naive harmonization playback
        [SerializeField] private Button buttonPlayVoiced; // Optional: runtime button for voiced manual progression playback
        [SerializeField] private Button buttonPlayChords; // Optional: runtime button for chord symbol playback
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private TMP_Dropdown tonicDropdown; // Optional: if null, defaults to C (tonicPc=0)
        [SerializeField] private TMP_InputField progressionInput;
        [SerializeField] private TMP_InputField Input_ChordSymbols; // User input field for chord symbols (absolute chord input)
        [SerializeField] private TMP_InputField Input_MelodyNoteNames; // User input field for melody note names
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI activeSourceLabel; // Optional: label showing active harmonic source

        [Header("Chord Grid")]
        [SerializeField] private Transform chordGridContainer;       // Content of Scroll_ChordGrid
        [SerializeField] private ChordColumnView chordColumnPrefab;

        [Header("Voicing Viewer")]
        [Tooltip("Optional UI panel that displays the actual voiced chord (bass–tenor–alto–soprano) during debug playback.")]
        [SerializeField] private VoicingViewer voicingViewer;
        [Tooltip("Optional piano keyboard display that highlights currently sounding MIDI notes.")]
        [SerializeField] private PianoKeyboardDisplay pianoKeyboardDisplay;
        [Tooltip("Optional melody piano roll display for visual melody timeline editing.")]
        [SerializeField] private MelodyPianoRoll melodyPianoRoll;
        
        [Header("Melody / Piano Roll")]
        [Tooltip("If true, SATB voiced playback (Play Voiced / SATB button) will use the MelodyPianoRoll grid as the melody source when available.")]
        [SerializeField] private bool usePianoRollMelodyForVoicedPlayback = true;

        [Header("Music")]
        [SerializeField] private MusicDataController musicDataController; // Optional, reserved for future use
        [SerializeField] private FmodNoteSynth synth;

        [Header("Settings")]
        [SerializeField] private TimelineSpec timelineSpec = new TimelineSpec();
        [SerializeField] private int rootOctave = 4;

        [Header("Duration Visual Spacing")]
        [Tooltip("Extra width (in pixels) per quarter note beyond 1 for right-side spacer. duration=1 has no spacer.")]
        [SerializeField] private float chordExtraWidthPerQuarter = 10f;
        [Tooltip("Base number of spaces between voicing columns (applies always, should be small).")]
        [SerializeField] private int voicingBaseSpaces = 1;
        [Tooltip("Extra spaces per quarter note beyond 1 in voicing viewer. duration=1 adds 0 extra spaces.")]
        [SerializeField] private int voicingSpacesPerExtraQuarter = 1;
        [Tooltip("Maximum extra spaces in voicing viewer to prevent runaway spacing.")]
        [SerializeField] private int voicingMaxExtraSpaces = 12;

        [Header("Chord Column Visual States")]
        [Tooltip("Alpha value for hidden columns (not yet reached). 0 = fully invisible, >0 = pre-visible at low opacity.")]
        [Range(0f, 1f)]
        [SerializeField] private float hiddenAlpha = 0f;
        [Tooltip("Alpha value for visible columns (already revealed).")]
        [Range(0f, 1f)]
        [SerializeField] private float visibleAlpha = 1f;
        [Tooltip("Alpha value for highlighted columns (currently playing).")]
        [Range(0f, 1f)]
        [SerializeField] private float highlightedAlpha = 1f;
        [Tooltip("Color tint for visible columns (applied to background). White = no tint.")]
        [SerializeField] private Color visibleTint = Color.white;
        [Tooltip("Color tint for highlighted columns (applied to background).")]
        [SerializeField] private Color highlightedTint = new Color(1f, 1f, 0.7f, 1f); // Light yellow tint
        
        [Header("Planned User-Facing Macros (WIP)")]
        [Tooltip("Off = manual tuning of advanced settings. DriveAdvanced = macros automatically overwrite advanced voicing values.\n\n" +
                 "⚠️ When DriveAdvanced is enabled, advanced fields below are derived from macros and will be overwritten on validate.")]
        [SerializeField] private VoicingMacroMode voicingMacroMode = VoicingMacroMode.Off;
        
        [Tooltip("Inner voice density (0=loose, 1=tight). Controls compression, compactness, and target gaps.")]
        [Range(0f, 1f)]
        [SerializeField] private float macroInnerVoiceDensity = 0.5f;
        
        [Tooltip("Voice-leading smoothness (0=prefer spacing/register, 1=prefer minimal movement).")]
        [Range(0f, 1f)]
        [SerializeField] private float macroSmoothness = 0.5f;
        
        [Tooltip("Register anchoring strength (0=no anchoring, 1=strong pull toward preferred centers).")]
        [Range(0f, 1f)]
        [SerializeField] private float macroRegisterAnchoring = 0.5f;
        
        // Guard to prevent recursion when applying macros
        private bool _isApplyingMacros = false;
        
        // Dev usability: Freeze button trigger (set to true to freeze derived values and switch to Off)
        [Tooltip("Set to true to freeze current advanced values and switch macro mode to Off.")]
        [SerializeField] private bool freezeDerivedAndSwitchToOff = false;
        
        // Dev usability: Read-only derived summary (computed in OnValidate)
        [Tooltip("Read-only summary of derived advanced values when macro mode is DriveAdvanced. Shows movement weight, compactness weights, target gaps, compression weights, and register weights.")]
        [SerializeField] private string derivedSummary = "";
        
        [Header("Voicing Compactness")]
        [Tooltip("Weight for Tenor-Bass gap in compactness cost. Higher values prefer tighter spacing between Tenor and Bass.")]
        [Range(0f, 1f)]
        [SerializeField] private float compactnessWeightTenorBass = 0.15f;
        
        [Tooltip("Weight for Alto-Tenor gap in compactness cost. Higher values prefer tighter spacing between Alto and Tenor.")]
        [Range(0f, 1f)]
        [SerializeField] private float compactnessWeightAltoTenor = 0.25f;
        
        [Header("Voice Leading Tendencies")]
        [Tooltip("Bonus for stepwise 7th resolution (negative = preference). More negative = stronger preference for resolving 7ths downward.")]
        [Range(-20f, 0f)]
        [SerializeField] private float seventhResolutionDownStepBonusNormal = -6.0f;
        
        [Tooltip("Penalty for avoiding 7th resolution when available (positive = penalty). Higher values = stronger penalty for not resolving 7ths.")]
        [Range(0f, 20f)]
        [SerializeField] private float seventhResolutionAvoidPenaltyNormal = 6.0f;
        
        [Tooltip("Penalty for spacing > 7 semitones between Soprano-Alto or Alto-Tenor. Higher values = stronger preference for tighter spacing.")]
        [Range(0f, 200f)]
        [SerializeField] private float spacingPreferredPenalty = 40f;
        
        [Tooltip("Penalty for Tenor-Bass gap > 12 semitones. Higher values = stronger preference for keeping bass and tenor closer.")]
        [Range(0f, 100f)]
        [SerializeField] private float spacingBassTenorPenalty = 15f;
        
        [Header("Register & Compression (Inner Voices)")]
        [Tooltip("Enable register gravity to pull Tenor/Alto toward preferred MIDI centers.")]
        [SerializeField] private bool enableRegisterGravity = true;
        
        [Tooltip("Preferred MIDI center for Tenor voice (~G3).")]
        [SerializeField] private float tenorRegisterCenter = 55f; // ~G3
        
        [Tooltip("Preferred MIDI center for Alto voice (~C4).")]
        [SerializeField] private float altoRegisterCenter = 60f;  // ~C4
        
        [Tooltip("Weight for Tenor register gravity. Higher values pull Tenor more strongly toward tenorRegisterCenter.")]
        [Range(0f, 2f)]
        [SerializeField] private float tenorRegisterWeight = 0.2f;
        
        [Tooltip("Weight for Alto register gravity. Higher values pull Alto more strongly toward altoRegisterCenter.")]
        [Range(0f, 2f)]
        [SerializeField] private float altoRegisterWeight = 0.3f;
        
        [Tooltip("Enable compression cost to penalize very wide inner spacing.")]
        [SerializeField] private bool enableCompressionCost = true;
        
        [Tooltip("Target gap between Alto and Tenor (semitones). Gaps larger than this incur a penalty.")]
        [SerializeField] private float targetAltoTenorGap = 7f;  // semitones (perfect 5th-ish max)
        
        [Tooltip("Target gap between Soprano and Alto (semitones). Gaps larger than this incur a penalty.")]
        [SerializeField] private float targetSopAltoGap = 7f;  // semitones
        
        [Tooltip("Weight for Alto-Tenor compression penalty.")]
        [Range(0f, 2f)]
        [SerializeField] private float compressionWeightAT = 0.5f;
        
        [Tooltip("Weight for Soprano-Alto compression penalty.")]
        [Range(0f, 2f)]
        [SerializeField] private float compressionWeightSA = 0.5f;
        
        [Header("Voice Leading Smoothness")]
        [Tooltip("Enable movement weighting to control how much voice-leading smoothness is prioritized.")]
        [SerializeField] private bool enableMovementWeighting = true;
        
        [Tooltip("Weight for inner voice movement cost. 1.0 = current behavior. Lower values allow spacing/register preferences to dominate.")]
        [Range(0f, 2f)]
        [SerializeField] private float movementWeightInnerVoices = 1.0f; // 1 = current behaviour
        
        [Header("Tensions / Extensions (Dev)")]
        [Tooltip("Enable soft cost adjustments for 11th tension voicing heuristics (sus bias for 11, register separation for #11).")]
        [SerializeField] private bool enableEleventhHeuristics = true;
        
        [Tooltip("Penalty when natural 11 (soprano) is present with 3rd in inner voices (Major chords).")]
        [Range(0f, 20f)]
        [SerializeField] private float penalty_11_withThird_Maj = 8f;
        
        [Tooltip("Penalty when natural 11 (soprano) is present with 3rd in inner voices (Dominant 7 chords).")]
        [Range(0f, 20f)]
        [SerializeField] private float penalty_11_withThird_Dom = 6f;
        
        [Tooltip("Penalty when natural 11 (soprano) is present with 3rd in inner voices (Minor chords).")]
        [Range(0f, 20f)]
        [SerializeField] private float penalty_11_withThird_Min = 0f;
        
        [Tooltip("Bonus (negative cost) when natural 11 (soprano) is present and 3rd is omitted.")]
        [Range(-10f, 0f)]
        [SerializeField] private float bonus_11_withoutThird = -2f;
        
        [Tooltip("Penalty when #11 (soprano) is too close to 3rd in inner voices (within 12 semitones).")]
        [Range(0f, 10f)]
        [SerializeField] private float penalty_sharp11_closeToThirdWithinOctave = 3f;
        
        [Tooltip("Bonus (negative cost) for #11 resolving upward by step on dominant 7 chords (strong tendency).")]
        [Range(-10f, 0f)]
        [SerializeField] private float sharp11ResolveUpBonus_Dominant = -4f;
        
        [Tooltip("Bonus (negative cost) for #11 resolving upward by step on major/maj7 chords (weak Lydian color tendency).")]
        [Range(-2f, 0f)]
        [SerializeField] private float sharp11ResolveUpBonus_Maj7 = -0.5f;
        
        [Header("Extension Placement Policy")]
        [Tooltip("Controls where requested extensions (9, b9, #11, etc.) appear in voicing. PreferSoprano = melodic tensions prefer soprano, add-tones prefer inner voices. ForceSoprano = melodic tensions must be in soprano. AnyVoice = no preference.")]
        [SerializeField] private TheoryVoicing.RequestedExtensionPlacementMode extensionPlacementMode = TheoryVoicing.RequestedExtensionPlacementMode.PreferSoprano;
        
        [Tooltip("Weight for preferring requested tensions (9, b9, #11, etc.) in the soprano voice. This is a style preference (cost bonus) that encourages tensions in soprano when feasible. Only applies when melody is not active. Default: 6.0 (moderate preference, breaks ties without overriding major constraints).")]
        [Range(0f, 20f)]
        [SerializeField] private float preferRequestedTensionInSopranoWeight = 6.0f;
        
        [Header("Debug")]
        [Tooltip("Enable debug logging for tension detection (11/#11).")]
        [SerializeField] private bool enableTensionDetectDebug = false;

        /// <summary>
        /// Computes the upper voice MIDI range based on rootOctave.
        /// Returns (upperMinMidi, upperMaxMidi) tuple.
        /// NOTE: upperMinMidi uses +5 (F) instead of +7 (G) so 7ths at the bottom
        /// of the range (e.g. G3 in A7/C#) can resolve down by step to F3.
        /// </summary>
        public (int upperMinMidi, int upperMaxMidi) ComputeUpperVoiceRange()
        {
            int upperMinMidi = rootOctave * 12 + 5;  // F in octave (rootOctave - 1), allows inner-voice 7ths like G3 to resolve down to F3
            int upperMaxMidi = (rootOctave + 3) * 12 + 9;  // A in octave (rootOctave + 2), allows A5 and higher when rootOctave = 4 (+12 semitones)
            return (upperMinMidi, upperMaxMidi);
        }
        [SerializeField] private float chordDurationSeconds = 1.0f;
        [SerializeField] private float gapBetweenChordsSeconds = 0.1f;
        
        [Tooltip("Velocity (0–1) used for SATB harmony chords.")]
        [Range(0f, 1f)]
        [SerializeField] private float harmonyVelocity = 0.8f;
        
        [Tooltip("Velocity (0–1) used for the lead melody timeline.")]
        [Range(0f, 1f)]
        [SerializeField] private float melodyVelocity = 0.9f;
        
        [Obsolete("Use harmonyVelocity and melodyVelocity instead. This field is kept for backward compatibility only.")]
        [SerializeField] private float velocity = 0.9f; // 0-1 range for FMOD
        [Header("Playback Debug Logging")]
        [Tooltip("Master gate for verbose playback logs. When OFF, only warnings/errors are shown.")]
        [SerializeField] private bool enablePlaybackDebug = false; // Master gate for verbose logs
        
        [Tooltip("Enable per-note/per-region trace logs (CHORD_NOTE_ON/OFF, SCHEDULER_FIRE, etc.). Requires enablePlaybackDebug=true.")]
        [SerializeField] private bool enablePlaybackTrace = false; // Per-note/per-region logs
        
        [Tooltip("Enable snapshot dumps at region boundaries (SNAPSHOT REGION_ENTER/EXIT). Requires enablePlaybackDebug=true.")]
        [SerializeField] private bool enablePlaybackSnapshots = false; // Snapshot dumps
        
        [Tooltip("Enable stack traces in cleanup/error logs. Requires enablePlaybackDebug=true.")]
        [SerializeField] private bool enablePlaybackStacks = false; // Stack traces
        
        [Tooltip("Enable playback summary logs (start/end summaries). Requires enablePlaybackDebug=true.")]
        [SerializeField] private bool enablePlaybackSummary = false; // Summary logs
        
        [Tooltip("Enable melody timeline debug logs (MELODY_PARSE, MELODY_TIMELINE, MELODY_SCHEDULER, etc.). Set to false to reduce console spam.")]
        [SerializeField] private bool enableMelodyTimelineDebug = false; // Melody timeline development logs
        
        // Legacy toggles (kept for backwards compatibility, now controlled by enablePlaybackDebug)
        [SerializeField] private bool enablePlaybackAudit = false; // PlaybackAudit: detailed diagnostic log (OBSERVATIONAL ONLY - no side effects)
        [SerializeField] private bool enableChordScheduleDebug = false; // ChordSchedule: per-region duration debugging
        [SerializeField] private bool EnablePlaybackTrace2 = false; // TRACE2: Unified event trace logging for diagnostic playback issues

        /// <summary>
        /// Gets the duration in quarter notes for a chord region.
        /// </summary>
        /// <param name="region">The chord region</param>
        /// <returns>Duration in quarters, or 1.0 as fallback if region is invalid</returns>
        private int GetDurationQuarters(ChordRegion region)
        {
            if (region.durationTicks <= 0 || timelineSpec == null || timelineSpec.ticksPerQuarter <= 0)
                return 1; // Minimum duration
            
            float quarters = region.durationTicks / (float)timelineSpec.ticksPerQuarter;
            return Mathf.Max(1, Mathf.RoundToInt(quarters));
        }

        /// <summary>
        /// Gets the duration in quarter notes for a region by index.
        /// </summary>
        /// <param name="regionIndex">Zero-based region index</param>
        /// <returns>Duration in quarters, or 1.0 as fallback</returns>
        private int GetDurationQuarters(int regionIndex)
        {
            if (_lastRegions == null || regionIndex < 0 || regionIndex >= _lastRegions.Count)
                return 1;
            
            return GetDurationQuarters(_lastRegions[regionIndex]);
        }

        /// <summary>
        /// Computes the spacer width for right-side duration spacing.
        /// </summary>
        /// <param name="quarters">Duration in quarter notes</param>
        /// <returns>Spacer width in pixels (0 for duration=1)</returns>
        private float GetChordSpacerWidth(int quarters)
        {
            if (quarters <= 1)
                return 0f;
            
            return (quarters - 1) * chordExtraWidthPerQuarter;
        }

        /// <summary>
        /// Computes the total number of trailing spaces for voicing viewer based on duration.
        /// </summary>
        /// <param name="quarters">Duration in quarter notes</param>
        /// <returns>Total trailing spaces (base + extra)</returns>
        private int GetVoicingPaddingSpaces(int quarters)
        {
            // Base spacing applies always
            int baseSpaces = voicingBaseSpaces;
            
            // Extra spacing only for duration > 1
            int extraSpaces = 0;
            if (quarters > 1)
            {
                extraSpaces = (quarters - 1) * voicingSpacesPerExtraQuarter;
                extraSpaces = Mathf.Min(extraSpaces, voicingMaxExtraSpaces);
            }
            
            return baseSpaces + extraSpaces;
        }

        /// <summary>
        /// Computes the hold duration in seconds for a chord region based on its durationTicks.
        /// Returns baseQuarterSeconds * quartersInRegion, where quartersInRegion = durationTicks / ticksPerQuarter.
        /// </summary>
        /// <param name="region">The chord region to compute hold time for</param>
        /// <returns>Hold duration in seconds, or chordDurationSeconds as fallback if region is invalid</returns>
        private float GetRegionHoldSeconds(ChordRegion region)
        {
            // ChordRegion is a struct, so check for invalid durationTicks instead of null
            if (region.durationTicks <= 0) return chordDurationSeconds; // fallback

            int tpq = (timelineSpec != null) ? timelineSpec.ticksPerQuarter : 4;
            if (tpq <= 0) tpq = 4;

            float quarters = region.durationTicks / (float)tpq;
            if (quarters <= 0f) quarters = 1f;

            return chordDurationSeconds * quarters;
        }
        
        [Header("Melody Register")]
        [Tooltip("Melody offset in octaves relative to its current register. Negative values move the melody down. Only affects playback, not theory/analysis.")]
        [SerializeField] private int melodyOctaveOffset = -1;
        
        /// <summary>
        /// Helper property to convert melody octave offset to semitones.
        /// </summary>
        private int MelodyOffsetSemitones => melodyOctaveOffset * 12;

        [Header("Theory Settings")]
        [SerializeField] private bool autoCorrectToMode = true;

        [Header("Export State (Runtime)")]
        [Tooltip("Last voiced harmonization state for JSON export. Automatically populated during voiced playback.")]
        private List<MelodyEvent> lastVoicedMelodyLine;
        private List<ChordEvent> lastVoicedChordEvents;
        private List<VoicedChord> lastVoicedChords;
        // Timeline v1: Store timeline melody events for independent playback
        private List<Sonoria.MusicTheory.Timeline.MelodyEvent> _lastTimelineMelodyEvents;
        private TheoryKey lastVoicedKey;
        
        [Header("Debug State (Editor)")]
        [Tooltip("Most recently constructed chord regions for debug inspection.")]
        private List<ChordRegion> _lastRegions;
        private DiagnosticsCollector _lastDiagnostics;
        private bool _isFirstRegionInSession = true; // Track first region for divider logic
        private Dictionary<int, string> _regionAnalysisInfoByIndex = new Dictionary<int, string>(); // Store non-diatonic labels per region
        private List<ChordColumnView> _chordColumnViewsByRegion = new List<ChordColumnView>(); // Store chord column views for progressive reveal

        /// <summary>
        /// Gets the most recently constructed chord regions (for editor debug inspection).
        /// </summary>
        public IReadOnlyList<ChordRegion> GetLastRegions() => _lastRegions;

        /// <summary>
        /// Gets the current timeline specification.
        /// </summary>
        public TimelineSpec GetTimelineSpec() => timelineSpec;

        [Header("Harmonization Settings (Naive Skeleton)")]
        [SerializeField] private bool harmonizationPreferTonicStart = true;
        [SerializeField] private bool harmonizationPreferChordContinuity = true;
        [SerializeField] private bool harmonizationEnableDetailedReasonLogs = true;

        [Header("Keyboard Timeline Tracking")]
        [Tooltip("When enabled, piano keyboard displays continuous timeline of active notes (harmony + melody). When disabled, uses legacy per-region snapshot updates.")]
        [SerializeField] private bool enableKeyboardTimelineTracking = true;
        
        [Header("Voicing Viewer Timeline")]
        [Tooltip("When enabled, VoicingViewer uses timeline-based view with quarter-note grid. When disabled, uses legacy per-chord step view.")]
        [SerializeField] private bool useVoicingTimelineView = true;

        [Header("Playback Settings")]
        [SerializeField]
        [Tooltip("If enabled, playback will double the chord's bass note an octave below.")]
        private bool emphasizeBassWithLowOctave = true;
        [SerializeField]
        [Tooltip("If enabled, playback uses voice-leading engine. If disabled, uses root-position chords.")]
        private bool useVoicingEngine = true;
        [SerializeField]
        [Tooltip("If true, playback uses a simple one-note-per-chord test melody and melody-constrained voicing. If false, use normal chord-only voicing.")]
        private bool useTestMelodyForPlayback = false;
        [SerializeField]
        [Tooltip("Scale degrees for the test melody (one per chord). Values wrap if progression is longer than this array. Example: [3, 4, 4, 2, 1] means degree 3 for first chord, degree 4 for second and third, etc.")]
        private int[] testMelodyDegrees = { 3, 4, 4, 2, 1 };

        [Header("Melody Input (Debug)")]
        [Tooltip("Optional test melody as space-separated note names with octaves, e.g. \"F5 E5 D5 B4 C5\". Used by naive harmonization when not empty.")]
        [TextArea]
        [SerializeField] private string testMelodyNoteNames = "";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDiagnostics = true; // Toggle to show/hide diagnostics console
        [SerializeField] private bool includeInfoDiagnostics = false; // Toggle to include Info events in summary
        [SerializeField] private bool enableVoicingPathDebug = false; // Toggle to log voicing path and effective weights
        [SerializeField] private bool enableFullSearchDebugSummary = false; // Toggle to log full search candidate counts and cost breakdowns
        [SerializeField] private bool enableHardPruneDebug = false; // Toggle to log hard constraint pruning counters
        [SerializeField] private bool useFullSearchEveryChord = false; // Dev toggle: force full search for every chord in SATB/N.H. (default OFF)
        [SerializeField] private bool enableUnityTraceLogs = false; // Controls whether TRACE Debug.Log lines are printed to Unity console
        [SerializeField] private bool includeTraceDiagnosticsInPanel = false; // Controls whether TRACE-like diagnostics are included in UI panel
        [SerializeField] private int maxDiagnosticsLinesInPanel = 20; // Hard cap on lines rendered in UI diagnostics panel
        [SerializeField] public bool enableRegressionHarness = false; // Master flag to enable regression harness (default OFF - no output when disabled)

        private Coroutine playRoutine;

        /// <summary>
        /// Helper to log TRACE messages only when enableUnityTraceLogs is enabled.
        /// </summary>
        private void TraceLog(string msg)
        {
            if (!enableUnityTraceLogs) return;
            Debug.Log(msg);
        }
        
        /// <summary>
        /// Centralized playback logging helpers. All playback logs should use these methods.
        /// </summary>
        private void LogPlaybackVerbose(string tag, string message)
        {
            if (!enablePlaybackDebug || !enablePlaybackTrace) return;
            UnityEngine.Debug.Log($"[{tag}] {message}");
        }
        
        private void LogPlaybackInfo(string tag, string message)
        {
            if (!enablePlaybackDebug || !enablePlaybackSummary) return;
            UnityEngine.Debug.Log($"[{tag}] {message}");
        }
        
        private void LogPlaybackWarn(string tag, string message)
        {
            // Warnings are always shown (even when debug is off)
            UnityEngine.Debug.LogWarning($"[{tag}] {message}");
        }
        
        private void LogPlaybackError(string tag, string message)
        {
            // Errors are always shown (even when debug is off)
            UnityEngine.Debug.LogError($"[{tag}] {message}");
        }
        
        private void LogPlaybackSnapshotMsg(string tag, string message)
        {
            if (!enablePlaybackDebug || !enablePlaybackSnapshots) return;
            UnityEngine.Debug.Log($"[{tag}] {message}");
        }
        
        private void LogPlaybackStack(string tag, string message, string stackTrace = null)
        {
            if (!enablePlaybackDebug || !enablePlaybackStacks) return;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                // Format stack trace as multi-line
                string[] stackLines = stackTrace.Split('\n');
                string truncatedStack = string.Join("\n", stackLines.Take(5));
                UnityEngine.Debug.Log($"[{tag}] {message}\nStack trace:\n{truncatedStack}");
            }
            else
            {
                UnityEngine.Debug.Log($"[{tag}] {message}");
            }
        }

        /// <summary>
        /// Sets the diagnostics collector and refreshes the display.
        /// </summary>
        private void SetDiagnosticsAndRefresh(DiagnosticsCollector diags)
        {
            _lastDiagnostics = diags ?? new DiagnosticsCollector();
            if (_lastDiagnostics != null)
            {
                _lastDiagnostics.EnableTrace = enableUnityTraceLogs;
            }
            ShowDiagnosticsSummary();
        }

        /// <summary>
        /// Builds and displays a compact diagnostics summary from _lastDiagnostics.
        /// </summary>
        private void ShowDiagnosticsSummary()
        {
            if (_lastDiagnostics == null || !showDiagnostics) return;
            
            var diags = _lastDiagnostics;

            var allRegions = diags.GetAll();

            // Compute event counts
            int totalEvents = 0;
            int forcedCount = 0;
            int warnCount = 0;
            int infoCount = 0;
            
            foreach (var regionDiags in allRegions)
            {
                foreach (var evt in regionDiags.events)
                {
                    totalEvents++;
                    switch (evt.severity)
                    {
                        case DiagSeverity.Forced:
                            forcedCount++;
                            break;
                        case DiagSeverity.Warning:
                            warnCount++;
                            break;
                        case DiagSeverity.Info:
                            infoCount++;
                            break;
                    }
                }
            }

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Diagnostics Summary (regions={diags.RegionCount}, events={totalEvents} | Forced={forcedCount} Warn={warnCount} Info={infoCount})");

            // Filter events based on includeInfoDiagnostics and includeTraceDiagnosticsInPanel
            bool hasFilteredEvents = false;
            var traceCodes = new HashSet<string> { "VOICING_START", "VOICING_DONE", "VOICED_REGION" };
            foreach (var regionDiags in allRegions)
            {
                var filteredEvents = regionDiags.events.Where(e => 
                {
                    // Always show Warning and Forced
                    if (e.severity == DiagSeverity.Warning || e.severity == DiagSeverity.Forced)
                        return true;
                    
                    // If includeInfoDiagnostics is false, exclude all Info
                    if (!includeInfoDiagnostics)
                        return false;
                    
                    // If includeTraceDiagnosticsInPanel is false, exclude trace-related Info events
                    if (!includeTraceDiagnosticsInPanel)
                    {
                        // Exclude trace codes
                        if (traceCodes.Contains(e.code))
                            return false;
                        
                        // Exclude events with "[TRACE" or "TRACE SNAPSHOT" in message (defensive filter)
                        if (e.message.Contains("[TRACE") || e.message.Contains("TRACE SNAPSHOT"))
                            return false;
                    }
                    
                    return true;
                }).ToList();
                
                if (filteredEvents.Count > 0)
                {
                    hasFilteredEvents = true;
                    string label = "?";
                    if (_lastRegions != null && regionDiags.regionIndex < _lastRegions.Count)
                    {
                        label = _lastRegions[regionDiags.regionIndex].debugLabel ?? $"R{regionDiags.regionIndex}";
                    }
                    else
                    {
                        label = $"R{regionDiags.regionIndex}";
                    }

                    int linesAdded = 0;
                    foreach (var evt in filteredEvents)
                    {
                        if (linesAdded >= maxDiagnosticsLinesInPanel)
                            break;
                            
                        string severityTag = evt.severity == DiagSeverity.Forced ? "[FORCED]" : 
                                            evt.severity == DiagSeverity.Warning ? "[WARN]" : "[INFO]";
                        string voiceInfo = evt.voiceIndex >= 0 ? $" (Voice {evt.voiceIndex})" : "";
                        string midiInfo = "";
                        if (evt.beforeMidi >= 0 && evt.afterMidi >= 0)
                        {
                            midiInfo = $" ({evt.beforeMidi}→{evt.afterMidi})";
                        }
                        summary.AppendLine($"R{regionDiags.regionIndex} ({label}): {severityTag} {evt.message}{voiceInfo}{midiInfo}");
                        linesAdded++;
                    }
                    
                    // Check if there are more events that were filtered out
                    if (filteredEvents.Count > maxDiagnosticsLinesInPanel)
                    {
                        int remaining = filteredEvents.Count - maxDiagnosticsLinesInPanel;
                        summary.AppendLine($"... ({remaining} more lines hidden; increase maxDiagnosticsLinesInPanel)");
                    }
                }
            }

            if (!hasFilteredEvents)
            {
                if (includeInfoDiagnostics)
                {
                    summary.AppendLine("No events detected.");
                }
                else
                {
                    summary.AppendLine("No Warning/Forced events. (Enable 'includeInfoDiagnostics' to view Info events.)");
                }
            }

            string summaryText = summary.ToString();
            if (enableDebugLogs)
            {
                Debug.Log($"[Diagnostics]\n{summaryText}");
            }

            // Display in status text if available (write diagnostics after status, not before)
            if (statusText != null && showDiagnostics)
            {
                // Append diagnostics after existing status (if any)
                string currentStatus = statusText.text ?? "";
                // Remove any previous diagnostics summary to avoid duplication
                if (currentStatus.Contains("Diagnostics Summary"))
                {
                    // Find where diagnostics start and keep everything before it
                    int diagIndex = currentStatus.IndexOf("Diagnostics Summary");
                    if (diagIndex >= 0)
                    {
                        currentStatus = currentStatus.Substring(0, diagIndex).TrimEnd();
                    }
                }
                
                // Append diagnostics after status
                if (!string.IsNullOrEmpty(currentStatus))
                {
                    statusText.text = currentStatus + "\n\n" + summaryText;
                }
                else
                {
                    statusText.text = summaryText;
                }

                ScrollToBottom();
            }
        }

        // Call after appending to the TMP text
        public void ScrollToBottom()
        {
            StopAllCoroutines();
            StartCoroutine(CoScrollToBottom());
        }

        IEnumerator CoScrollToBottom()
        {
            // Let TMP/layout calculate sizes
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            // Sometimes needs one more pass with TMP + ContentSizeFitter
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        void Awake()
        {
            // Handle freeze button
            if (freezeDerivedAndSwitchToOff)
            {
                voicingMacroMode = VoicingMacroMode.Off;
                freezeDerivedAndSwitchToOff = false;
                UnityEngine.Debug.Log("[MACRO] Frozen derived values and switched to Off mode. Advanced fields are now manual.");
            }
            
            // Apply macros if enabled (before syncing to TheoryVoicing)
            if (voicingMacroMode == VoicingMacroMode.DriveAdvanced)
            {
                ApplyVoicingMacrosToAdvancedFields_V1();
                UpdateDerivedSummary();
            }
            else
            {
                derivedSummary = "";
            }
            
            // Set voicing path debug flag
            TheoryVoicing.SetVoicingPathDebug(enableVoicingPathDebug);
            TheoryVoicing.SetFullSearchDebugSummary(enableFullSearchDebugSummary);
            TheoryVoicing.SetHardPruneDebug(enableHardPruneDebug);
            TheoryVoicing.SetUseFullSearchEveryChord(useFullSearchEveryChord);
            
            // Set compactness weights, voice leading tendencies, and register/compression settings in TheoryVoicing from Inspector values
            UpdateCompactnessWeights();
            UpdateVoiceLeadingTendencies();
            UpdateRegisterAndCompressionSettings();
            UpdateEleventhHeuristics();
            
            // Sync debug flags
            TheoryVoicing.SetHardPruneDebug(enableHardPruneDebug);
            TheoryVoicing.SetFullSearchDebugSummary(enableFullSearchDebugSummary);
            TheoryVoicing.SetVoicingPathDebug(enableVoicingPathDebug);
            TheoryVoicing.SetUseFullSearchEveryChord(useFullSearchEveryChord);
            TheoryVoicing.SetDebugTensionDetect(enableTensionDetectDebug);
            TheoryVoicing.SetExtensionPlacementMode(extensionPlacementMode);
            
            // Wire button click event (like ScaleUI does)
            if (buttonPlay != null)
            {
                buttonPlay.onClick.RemoveAllListeners();
                buttonPlay.onClick.AddListener(OnPlayClicked);
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Button wired in Awake");
            }
            else
            {
                Debug.LogWarning("[ChordLab] Button_Play reference is missing! Click events may not work.");
            }

            // Wire naive harmonization button (optional)
            if (buttonNaiveHarmonize != null)
            {
                buttonNaiveHarmonize.onClick.RemoveAllListeners();
                buttonNaiveHarmonize.onClick.AddListener(OnNaiveHarmonizeClicked);
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Naive Harmonize button wired in Awake");
            }

            // Wire voiced manual progression button (optional)
            if (buttonPlayVoiced != null)
            {
                buttonPlayVoiced.onClick.RemoveAllListeners();
                buttonPlayVoiced.onClick.AddListener(OnPlayVoicedClicked);
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Play Voiced button wired in Awake");
            }

            // Wire chord symbol playback button (optional) - DEPRECATED: Now handled by unified Play button
            // Keeping button reference for backward compatibility, but not wiring it
            if (buttonPlayChords != null)
            {
                buttonPlayChords.onClick.RemoveAllListeners();
                // Disable the button to indicate it's deprecated
                buttonPlayChords.interactable = false;
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Play Chords button disabled (unified Play button now handles both Roman and Absolute)");
            }

            // Subscribe to input field changes to update active source label
            if (progressionInput != null)
            {
                progressionInput.onValueChanged.AddListener(_ => RefreshActiveSourceUI());
            }
            if (Input_ChordSymbols != null)
            {
                Input_ChordSymbols.onValueChanged.AddListener(_ => RefreshActiveSourceUI());
            }

            // Initial refresh of active source label
            RefreshActiveSourceUI();

            // Initialize dropdown options if not set
            if (modeDropdown != null && modeDropdown.options.Count == 0)
            {
                SetupModeDropdown();
            }

            // Initialize tonic dropdown options if not set
            if (tonicDropdown != null && tonicDropdown.options.Count == 0)
            {
                SetupTonicDropdown();
            }

            // Set initial status
            if (statusText != null)
            {
                statusText.text = "Ready. Enter progression and click Play.";
            }

            // Validate melody input field (optional, but log if missing)
            if (Input_MelodyNoteNames == null && enableDebugLogs)
            {
                Debug.LogWarning("[ChordLab] Input_MelodyNoteNames reference is missing. Melody input will not be available.");
            }

            // Initialize piano keyboard display if available
            if (pianoKeyboardDisplay != null)
            {
                // Show keyboard by default with all keys dimmed (no active notes)
                pianoKeyboardDisplay.ShowDefault();
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Piano keyboard display initialized and visible");
            }
        }

        /// <summary>
        /// Updates TheoryVoicing with the current compactness weight values from Inspector.
        /// Called from Awake() and OnValidate() to ensure values are synchronized.
        /// </summary>
        private void UpdateCompactnessWeights()
        {
            TheoryVoicing.SetCompactnessWeights(compactnessWeightTenorBass, compactnessWeightAltoTenor);
        }
        
        /// <summary>
        /// Updates TheoryVoicing with the current voice leading tendency values from Inspector.
        /// Called from Awake() and OnValidate() to ensure values are synchronized.
        /// </summary>
        private void UpdateVoiceLeadingTendencies()
        {
            TheoryVoicing.SetVoiceLeadingTendencies(
                seventhResolutionDownStepBonusNormal,
                seventhResolutionAvoidPenaltyNormal,
                spacingPreferredPenalty,
                spacingBassTenorPenalty);
        }
        
        /// <summary>
        /// Updates TheoryVoicing with the current register gravity, compression, and movement weighting values from Inspector.
        /// Called from Awake() and OnValidate() to ensure values are synchronized.
        /// </summary>
        private void UpdateRegisterAndCompressionSettings()
        {
            TheoryVoicing.SetRegisterAndCompressionSettings(
                enableRegisterGravity,
                tenorRegisterCenter,
                altoRegisterCenter,
                tenorRegisterWeight,
                altoRegisterWeight,
                enableCompressionCost,
                targetAltoTenorGap,
                targetSopAltoGap,
                compressionWeightAT,
                compressionWeightSA,
                enableMovementWeighting,
                movementWeightInnerVoices);
        }
        
        /// <summary>
        /// Updates TheoryVoicing with the current 11th tension heuristics values from Inspector.
        /// </summary>
        private void UpdateEleventhHeuristics()
        {
            TheoryVoicing.SetEleventhHeuristics(
                enableEleventhHeuristics,
                penalty_11_withThird_Maj,
                penalty_11_withThird_Dom,
                penalty_11_withThird_Min,
                bonus_11_withoutThird,
                penalty_sharp11_closeToThirdWithinOctave);
            
            // Sync #11 resolution tendency bonuses
            TheoryVoicing.SetSharp11ResolutionTendencies(
                sharp11ResolveUpBonus_Dominant,
                sharp11ResolveUpBonus_Maj7);
            
            TheoryVoicing.SetPreferRequestedTensionInSopranoWeight(preferRequestedTensionInSopranoWeight);
        }
        
        /// <summary>
        /// Called when Inspector values change (including during play mode).
        /// Updates TheoryVoicing with new compactness weights, voice leading tendencies, and register/compression settings.
        /// </summary>
        private void OnValidate()
        {
            // Handle freeze button
            if (freezeDerivedAndSwitchToOff)
            {
                voicingMacroMode = VoicingMacroMode.Off;
                freezeDerivedAndSwitchToOff = false;
                UnityEngine.Debug.Log("[MACRO] Frozen derived values and switched to Off mode. Advanced fields are now manual.");
            }
            
            // Apply macros if enabled (before syncing to TheoryVoicing)
            if (voicingMacroMode == VoicingMacroMode.DriveAdvanced)
            {
                ApplyVoicingMacrosToAdvancedFields_V1();
                UpdateDerivedSummary();
            }
            else
            {
                derivedSummary = "";
            }
            
            // Set voicing path debug flag
            TheoryVoicing.SetVoicingPathDebug(enableVoicingPathDebug);
            TheoryVoicing.SetFullSearchDebugSummary(enableFullSearchDebugSummary);
            TheoryVoicing.SetHardPruneDebug(enableHardPruneDebug);
            TheoryVoicing.SetUseFullSearchEveryChord(useFullSearchEveryChord);
            TheoryVoicing.SetDebugTensionDetect(enableTensionDetectDebug);
            
            if (Application.isPlaying)
            {
                UpdateCompactnessWeights();
                UpdateVoiceLeadingTendencies();
                UpdateRegisterAndCompressionSettings();
                UpdateEleventhHeuristics();
            }
        }
        
        /// <summary>
        /// Applies macro values to advanced voicing fields (v1 mapping).
        /// Uses a guard to prevent recursion when Inspector values update.
        /// </summary>
        private void ApplyVoicingMacrosToAdvancedFields_V1()
        {
            // Guard against recursion
            if (_isApplyingMacros)
                return;
            
            _isApplyingMacros = true;
            
            try
            {
                // Inner Voice Density (0..1)
                enableCompressionCost = (macroInnerVoiceDensity > 0.01f);
                compressionWeightAT = Mathf.Lerp(0.0f, 1.0f, macroInnerVoiceDensity);
                compressionWeightSA = Mathf.Lerp(0.0f, 1.0f, macroInnerVoiceDensity);
                targetAltoTenorGap = Mathf.RoundToInt(Mathf.Lerp(12f, 3f, macroInnerVoiceDensity));
                targetSopAltoGap = Mathf.RoundToInt(Mathf.Lerp(12f, 3f, macroInnerVoiceDensity));
                compactnessWeightAltoTenor = Mathf.Lerp(0.0f, 0.5f, macroInnerVoiceDensity);
                compactnessWeightTenorBass = Mathf.Lerp(0.0f, 0.25f, macroInnerVoiceDensity);
                
                // Smoothness (0..1)
                enableMovementWeighting = (macroSmoothness > 0.01f);
                movementWeightInnerVoices = Mathf.Lerp(0.25f, 2.0f, macroSmoothness);
                
                // Register Anchoring (0..1)
                enableRegisterGravity = (macroRegisterAnchoring > 0.01f);
                tenorRegisterWeight = Mathf.Lerp(0.0f, 0.6f, macroRegisterAnchoring);
                altoRegisterWeight = Mathf.Lerp(0.0f, 0.8f, macroRegisterAnchoring);
                // Note: Do not change centers in v1 (leave user-set centers alone)
                
                // Hard spacing penalties: Leave unchanged in v1 (macro does not touch them yet)
                // Note: 7th resolution parameters (seventhResolutionDownStepBonusNormal, seventhResolutionAvoidPenaltyNormal)
                // are now dev controls only and not driven by macros.
                
                // Optional: Log macro application summary
                if (enableDebugLogs)
                {
                    UnityEngine.Debug.Log(
                        $"[MACRO] Applied DriveAdvanced settings:\n" +
                        $"  Compression: enable={enableCompressionCost}, AT={compressionWeightAT:F3}, SA={compressionWeightSA:F3}, gaps AT={targetAltoTenorGap} SA={targetSopAltoGap}\n" +
                        $"  Compactness: TB={compactnessWeightTenorBass:F3}, AT={compactnessWeightAltoTenor:F3}\n" +
                        $"  Movement: enable={enableMovementWeighting}, weight={movementWeightInnerVoices:F3}\n" +
                        $"  Register: enable={enableRegisterGravity}, T={tenorRegisterWeight:F3}, A={altoRegisterWeight:F3}"
                    );
                }
            }
            finally
            {
                _isApplyingMacros = false;
            }
        }
        
        /// <summary>
        /// Updates the read-only derived summary string when in DriveAdvanced mode.
        /// </summary>
        private void UpdateDerivedSummary()
        {
            if (voicingMacroMode != VoicingMacroMode.DriveAdvanced)
            {
                derivedSummary = "";
                return;
            }
            
            derivedSummary = $"Derived Values:\n" +
                $"  Movement: enable={enableMovementWeighting}, weight={movementWeightInnerVoices:F3}\n" +
                $"  Compactness: TB={compactnessWeightTenorBass:F3}, AT={compactnessWeightAltoTenor:F3}\n" +
                $"  Compression: enable={enableCompressionCost}, AT={compressionWeightAT:F3}, SA={compressionWeightSA:F3}, gaps AT={targetAltoTenorGap} SA={targetSopAltoGap}\n" +
                $"  Register: enable={enableRegisterGravity}, T={tenorRegisterWeight:F3}, A={altoRegisterWeight:F3}";
        }
        
        /// <summary>
        /// Context menu command to freeze derived values and switch to Off mode.
        /// </summary>
        [ContextMenu("Freeze Derived Values and Switch to Off")]
        private void FreezeDerivedValues()
        {
            if (voicingMacroMode == VoicingMacroMode.DriveAdvanced)
            {
                voicingMacroMode = VoicingMacroMode.Off;
                freezeDerivedAndSwitchToOff = false;
                derivedSummary = "";
                UnityEngine.Debug.Log("[MACRO] Frozen derived values and switched to Off mode. Advanced fields are now manual.");
            }
            else
            {
                UnityEngine.Debug.Log("[MACRO] Macro mode is already Off. No values to freeze.");
            }
        }

        /// <summary>
        /// Sets up the mode dropdown with Major/Minor options only.
        /// </summary>
        private void SetupModeDropdown()
        {
            if (modeDropdown == null) return;

            modeDropdown.options.Clear();
            
            // Add only Major and Minor options (UI restriction)
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Major"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Minor"));

            modeDropdown.value = 0; // Default to Major (maps to Ionian)
            modeDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Sets up the tonic dropdown with all 12 pitch classes.
        /// Uses concrete labels that match the accidental preference mapping:
        /// - Sharp-leaning keys: C, D, E, G, A, B
        /// - Flat-leaning keys: Db, Eb, F, Gb, Ab, Bb
        /// </summary>
        private void SetupTonicDropdown()
        {
            if (tonicDropdown == null) return;

            tonicDropdown.options.Clear();
            
            // Add all 12 pitch classes with concrete labels matching accidental preference
            // Index 0-11 maps to tonic pitch class 0-11
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("C"));      // 0
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Db"));    // 1
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("D"));     // 2
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Eb"));    // 3
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("E"));     // 4
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("F"));    // 5
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Gb"));   // 6
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("G"));    // 7
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Ab"));   // 8
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("A"));    // 9
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Bb"));    // 10
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("B"));     // 11

            tonicDropdown.value = 0; // Default to C
            tonicDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Determines the active harmonic input source based on input field contents.
        /// Precedence: Roman > Absolute > None
        /// </summary>
        /// <returns>The active harmonic input source</returns>
        private HarmonicInputSource GetActiveSource()
        {
            string romanInput = progressionInput != null ? progressionInput.text.Trim() : string.Empty;
            string absoluteInput = Input_ChordSymbols != null ? Input_ChordSymbols.text.Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(romanInput))
                return HarmonicInputSource.Roman;
            else if (!string.IsNullOrWhiteSpace(absoluteInput))
                return HarmonicInputSource.Absolute;
            else
                return HarmonicInputSource.None;
        }

        /// <summary>
        /// Refreshes the active source UI label to show which harmonic source is currently active.
        /// </summary>
        private void RefreshActiveSourceUI()
        {
            if (activeSourceLabel == null)
                return;

            HarmonicInputSource source = GetActiveSource();
            switch (source)
            {
                case HarmonicInputSource.Roman:
                    activeSourceLabel.text = "Active source: Roman";
                    break;
                case HarmonicInputSource.Absolute:
                    activeSourceLabel.text = "Active source: Absolute";
                    break;
                case HarmonicInputSource.None:
                    activeSourceLabel.text = "Active source: (none)";
                    break;
            }
        }

        /// <summary>
        /// Attempts to build ChordRegion[] from the active harmonic input source.
        /// Centralizes the logic for parsing and building regions from either Roman or Absolute input.
        /// </summary>
        /// <param name="regions">Output: List of ChordRegion objects if successful</param>
        /// <param name="recipes">Output: List of ChordRecipe objects if successful</param>
        /// <param name="originalTokens">Output: List of original input tokens if successful</param>
        /// <param name="durationsInQuarters">Output: List of durations in quarter notes if successful</param>
        /// <param name="skipAutoCorrection">Output: Whether auto-correction should be skipped (true for Absolute, false for Roman)</param>
        /// <param name="inputText">Output: The original input text used</param>
        /// <param name="errorMessage">Output: Error message if parsing failed</param>
        /// <returns>True if regions were successfully built, false otherwise</returns>
        private bool TryBuildRegionsFromActiveSource(
            out List<ChordRegion> regions,
            out List<ChordRecipe> recipes,
            out List<string> originalTokens,
            out List<int> durationsInQuarters,
            out bool skipAutoCorrection,
            out string inputText,
            out string errorMessage)
        {
            regions = null;
            recipes = null;
            originalTokens = null;
            durationsInQuarters = null;
            skipAutoCorrection = false;
            inputText = null;
            errorMessage = null;

            TheoryKey key = GetKeyFromDropdowns();
            HarmonicInputSource source = GetActiveSource();

            if (source == HarmonicInputSource.None)
            {
                errorMessage = "No progression entered. Please enter either a Roman numeral progression or absolute chord symbols.";
                return false;
            }

            if (source == HarmonicInputSource.Roman)
            {
                inputText = progressionInput != null ? progressionInput.text.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(inputText))
                {
                    errorMessage = "Roman numeral input is empty.";
                    return false;
                }

                // Parse Roman numerals with duration suffixes
                if (!TryBuildChordRecipesFromRomanInput(key, inputText, out originalTokens, out recipes, out durationsInQuarters))
                {
                    errorMessage = "Could not parse Roman progression. Check for invalid Roman numerals.";
                    return false;
                }

                skipAutoCorrection = false; // Roman numerals can use auto-correction
                
                // Build regions using Roman input helper
                regions = BuildRegionsFromRomanInput(inputText, key, timelineSpec, null, skipAutoCorrection: false);
                if (regions == null || regions.Count == 0)
                {
                    errorMessage = "Failed to build regions from Roman input.";
                    return false;
                }

                return true;
            }
            else // Absolute
            {
                inputText = Input_ChordSymbols != null ? Input_ChordSymbols.text.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(inputText))
                {
                    errorMessage = "Absolute chord input is empty.";
                    return false;
                }

                // Parse chord symbols with duration suffixes
                if (!TryBuildChordRecipesFromChordSymbolInput(key, inputText, out originalTokens, out recipes, out durationsInQuarters, out string parseError))
                {
                    errorMessage = $"Could not parse chord symbols: {parseError ?? "Check for invalid chord symbols."}";
                    return false;
                }

                skipAutoCorrection = true; // Chord symbols should sound exactly as typed
                
                // Build regions using Absolute input helper
                regions = BuildRegionsFromChordSymbolInput(inputText, key, timelineSpec, null);
                if (regions == null || regions.Count == 0)
                {
                    errorMessage = "Failed to build regions from Absolute input.";
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the TheoryKey from the mode and tonic dropdowns.
        /// If tonicDropdown is not assigned, defaults to C (tonicPc=0) for backwards compatibility.
        /// </summary>
        /// <returns>The TheoryKey representing the selected tonic and mode</returns>
        private TheoryKey GetKeyFromDropdowns()
        {
            // Get mode from dropdown
            Sonoria.MusicTheory.ScaleMode mode = GetModeFromDropdown(modeDropdown != null ? modeDropdown.value : 0);
            
            // Get tonic pitch class (0-11), defaulting to 0 (C) if dropdown is not assigned
            int tonicPc = 0;
            if (tonicDropdown != null)
            {
                tonicPc = tonicDropdown.value;
                // Clamp to valid range (0-11) in case of unexpected values
                if (tonicPc < 0) tonicPc = 0;
                if (tonicPc > 11) tonicPc = 11;
            }
            
            return new TheoryKey(tonicPc, mode);
        }

        /// <summary>
        /// Builds harmony heuristic settings from Inspector fields.
        /// </summary>
        private HarmonyHeuristicSettings BuildHarmonyHeuristicSettings()
        {
            return new HarmonyHeuristicSettings
            {
                PreferTonicStart = harmonizationPreferTonicStart,
                PreferChordContinuity = harmonizationPreferChordContinuity,
                EnableDetailedReasonLogs = harmonizationEnableDetailedReasonLogs
            };
        }

        /// <summary>
        /// Ensures a MIDI note is within the specified range by adjusting octave if needed.
        /// </summary>
        private static int EnsureInRange(int midi, int minMidi, int maxMidi)
        {
            if (midi < minMidi)
            {
                // Move up octaves until in range
                while (midi < minMidi && midi < maxMidi)
                {
                    midi += 12;
                }
            }
            else if (midi > maxMidi)
            {
                // Move down octaves until in range
                while (midi > maxMidi && midi > minMidi)
                {
                    midi -= 12;
                }
            }
            
            // Final clamp to ensure we're in range
            if (midi < minMidi) midi = minMidi;
            if (midi > maxMidi) midi = maxMidi;
            
            return midi;
        }

        /// <summary>
        /// Gets the melody input text from the UI input field.
        /// Returns empty string if the field is not assigned.
        /// </summary>
        public string GetMelodyInput()
        {
            return Input_MelodyNoteNames != null
                ? Input_MelodyNoteNames.text
                : string.Empty;
        }

        /// <summary>
        /// Called when the Play button is clicked.
        /// </summary>
        public void OnPlayClicked()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] OnPlayClicked called (unified Play button)");

            // Temporary debug log to test melody input
            Debug.Log("[MelodyInput] " + GetMelodyInput());

            // Stop any existing playback
            if (playRoutine != null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Stopping existing playback");
                StopPlaybackAndCleanup();
            }

            // Use unified active source builder to get regions and recipes
            if (!TryBuildRegionsFromActiveSource(
                out List<ChordRegion> regions,
                out List<ChordRecipe> recipes,
                out List<string> originalTokens,
                out List<int> durationsInQuarters,
                out bool skipAutoCorrection,
                out string inputText,
                out string errorMessage))
            {
                UpdateStatus(errorMessage);
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] {errorMessage}");
                return;
            }

            // Use shared playback coroutine with the built regions
            HarmonicInputSource source = GetActiveSource();
            string romanInputText = (source == HarmonicInputSource.Roman) ? inputText : null;
            string chordSymbolInputText = (source == HarmonicInputSource.Absolute) ? inputText : null;
            
            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Playing from {source} input ({recipes.Count} chords)");

            playRoutine = StartCoroutine(PlayChordRecipesCo(
                GetKeyFromDropdowns(),
                recipes,
                originalTokens,
                durationsInQuarters,
                skipAutoCorrection,
                romanInputText: romanInputText,
                chordSymbolInputText: chordSymbolInputText));
        }

        /// <summary>
        /// UI callback for the Naive Harmonize button.
        /// Uses the same core path as the debug menu to build, harmonize,
        /// voice, and play the test melody.
        /// </summary>
        private void OnNaiveHarmonizeClicked()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] OnNaiveHarmonizeClicked called");

            // Stop any existing playback (similar to OnPlayClicked)
            if (playRoutine != null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Stopping existing playback");
                StopCoroutine(playRoutine);
                playRoutine = null;
                if (synth != null)
                {
                    synth.StopAll();
                }
            }

            // Use shared core path for naive harmonization playback
            PlayNaiveHarmonizationForCurrentTestMelody();
        }

        /// <summary>
        /// UI callback for the Play Voiced (SATB) button.
        /// Uses unified active source detection to support both Roman numeral and chord symbol inputs.
        /// Voice-leads the progression with the current melody and plays it with SATB display.
        /// </summary>
        private void OnPlayVoicedClicked()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] OnPlayVoicedClicked (SATB) called");

            // Stop any existing playback (similar to OnPlayClicked)
            if (playRoutine != null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Stopping existing playback");
                StopCoroutine(playRoutine);
                playRoutine = null;
                if (synth != null)
                {
                    synth.StopAll();
                }
            }

            // Use unified active source builder
            if (!TryBuildRegionsFromActiveSource(
                out List<ChordRegion> regions,
                out List<ChordRecipe> recipes,
                out List<string> originalTokens,
                out List<int> durationsInQuarters,
                out bool skipAutoCorrection,
                out string inputText,
                out string errorMessage))
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] SATB: {errorMessage}");
                UpdateStatus(errorMessage);
                return;
            }

            if (recipes == null || recipes.Count == 0)
            {
                string error = "No valid chords found.";
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] {error}");
                UpdateStatus(error);
                return;
            }

            TheoryKey key = GetKeyFromDropdowns();
            string melodyInput = Input_MelodyNoteNames != null ? Input_MelodyNoteNames.text : string.Empty;

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] SATB: Using {recipes.Count} chords from {GetActiveSource()} (skipAutoCorrection={skipAutoCorrection})");

            // Call shared SATB pipeline
            // Pass inputText only when Roman input was used (for BuildRegionsFromRomanInput helper)
            HarmonicInputSource source = GetActiveSource();
            string romanInputTextForHelper = (source == HarmonicInputSource.Roman) ? inputText : null;
            RunSatbFromChordRecipes(key, recipes, melodyInput, durationsInQuarters, skipAutoCorrection, romanInputText: romanInputTextForHelper);
        }

        /// <summary>
        /// UI callback for the Play Chords button.
        /// Takes chord symbols from Input_ChordSymbols field, parses them,
        /// and plays them exactly as typed (no auto-correction).
        /// </summary>
        public void OnPlayChordsClicked()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] OnPlayChordsClicked called");

            // Stop any existing playback
            if (playRoutine != null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ChordLab] Stopping existing playback");
                StopPlaybackAndCleanup();
            }

            // Start new playback coroutine for chord symbols
            playRoutine = StartCoroutine(PlayChordSymbolsCo());
        }

        /// <summary>
        /// Coroutine that parses and plays chord symbols from Input_ChordSymbols field.
        /// Chord symbols are played exactly as typed (no auto-correction).
        /// </summary>
        private IEnumerator PlayChordSymbolsCo()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] PlayChordSymbolsCo started");

            // Validate required components
            if (modeDropdown == null || Input_ChordSymbols == null || synth == null)
            {
                string error = "Error: Missing UI or synth references.";
                Debug.LogError($"[ChordLab] {error} modeDropdown={modeDropdown}, Input_ChordSymbols={Input_ChordSymbols}, synth={synth}");
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            // Get TheoryKey from both dropdowns (tonic + mode)
            TheoryKey key = GetKeyFromDropdowns();
            if (enableDebugLogs)
            {
                Sonoria.MusicTheory.ScaleMode selectedMode = GetModeFromDropdown(modeDropdown != null ? modeDropdown.value : 0);
                int tonicPc = (tonicDropdown != null) ? tonicDropdown.value : 0;
                string modeDisplayName = GetModeDisplayName(selectedMode);
                Debug.Log($"[ChordLab] Selected key: {key} (tonicPc={tonicPc}, mode={modeDisplayName})");
            }

            // Clear piano keyboard display at start of playback
            if (pianoKeyboardDisplay != null)
            {
                pianoKeyboardDisplay.SetActiveNotes(new int[0]); // Clear all highlights
            }

            // Get chord symbol input
            string input = Input_ChordSymbols.text;
            if (string.IsNullOrWhiteSpace(input))
            {
                string error = "Error: Chord symbol input is empty.";
                Debug.LogWarning($"[ChordLab] {error}");
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Chord symbol input text: '{input}'");

            // Parse chord symbols (with duration suffix support)
            bool parseSuccess = false;
            List<string> originalTokens = null;
            List<ChordRecipe> recipes = null;
            List<int> durationsInQuarters = null;
            string errorMessage = null;

            try
            {
                parseSuccess = TryBuildChordRecipesFromChordSymbolInput(key, input, out originalTokens, out recipes, out durationsInQuarters, out errorMessage);
            }
            catch (System.Exception ex)
            {
                parseSuccess = false;
                errorMessage = $"Exception during parsing: {ex.Message}";
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ChordLab] {errorMessage}");
                }
            }

            if (!parseSuccess)
            {
                string error = $"Error: Could not parse chord symbols. {errorMessage ?? "Check for invalid chord symbols."}";
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ChordLab] {error}");
                }
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Parsed {recipes.Count} chord symbols");
                for (int i = 0; i < recipes.Count; i++)
                {
                    string roman = TheoryChord.RecipeToRomanNumeral(key, recipes[i]);
                    string token = originalTokens != null && i < originalTokens.Count ? originalTokens[i] : "?";
                    Debug.Log($"[ChordLab] Parsed chord {i + 1} '{token}' -> {recipes[i]}, Roman={roman}");
                }
            }

            // Use shared playback coroutine (chord symbols skip auto-correction)
            // Pass chordSymbolInputText for duration suffix support via BuildRegionsFromChordSymbolInput
            yield return StartCoroutine(PlayChordRecipesCo(key, recipes, originalTokens, durationsInQuarters, skipAutoCorrection: true, chordSymbolInputText: input));
        }

        /// <summary>
        /// Toggles the piano keyboard display visibility.
        /// </summary>
        public void TogglePianoKeyboard()
        {
            if (pianoKeyboardDisplay != null)
            {
                var canvasGroup = pianoKeyboardDisplay.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = pianoKeyboardDisplay.gameObject.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = canvasGroup.alpha > 0.5f ? 0f : 1f;
            }
        }

        /// <summary>
        /// Coroutine that parses and plays the progression.
        /// </summary>
        private IEnumerator PlayProgressionCo()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] PlayProgressionCo started");

            // Validate required components
            if (modeDropdown == null || progressionInput == null || synth == null)
            {
                string error = "Error: Missing UI or synth references.";
                Debug.LogError($"[ChordLab] {error} modeDropdown={modeDropdown}, progressionInput={progressionInput}, synth={synth}");
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            // Get TheoryKey from both dropdowns (tonic + mode)
            TheoryKey key = GetKeyFromDropdowns();
            if (enableDebugLogs)
            {
                Sonoria.MusicTheory.ScaleMode selectedMode = GetModeFromDropdown(modeDropdown != null ? modeDropdown.value : 0);
                int tonicPc = (tonicDropdown != null) ? tonicDropdown.value : 0;
                string modeDisplayName = GetModeDisplayName(selectedMode);
                Debug.Log($"[ChordLab] Selected key: {key} (tonicPc={tonicPc}, mode={modeDisplayName})");
            }

            // Clear piano keyboard display at start of playback
            if (pianoKeyboardDisplay != null)
            {
                pianoKeyboardDisplay.SetActiveNotes(new int[0]); // Clear all highlights
            }

            // Parse progression input using shared helper
            string input = progressionInput.text;
            if (string.IsNullOrWhiteSpace(input))
            {
                string error = "Error: Progression input is empty.";
                Debug.LogWarning($"[ChordLab] {error}");
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Input text: '{input}'");

            // Parse progression to tokens and recipes (Roman numerals)
            // Wrap in try/catch to handle any unexpected exceptions gracefully
            bool parseSuccess = false;
            List<string> originalTokens = null;
            List<ChordRecipe> recipes = null;
            
            List<int> durationsInQuarters = null;
            try
            {
                parseSuccess = TryBuildChordRecipesFromRomanInput(key, input, out originalTokens, out recipes, out durationsInQuarters);
            }
            catch (System.Exception ex)
            {
                // Catch any unexpected exceptions from parsing and treat as parse failure
                parseSuccess = false;
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ChordLab] Exception during parsing (treated as parse failure): {ex}");
                }
            }
            
            if (!parseSuccess)
            {
                string error = "Error: Could not parse progression. Check for invalid Roman numerals.";
                // Use warning instead of error for user-facing mistakes (gated by enableDebugLogs)
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ChordLab] {error}");
                }
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Parsed {originalTokens.Count} numerals: {string.Join(", ", originalTokens)}");
                for (int i = 0; i < originalTokens.Count && i < recipes.Count; i++)
                {
                    Debug.Log($"[ChordLab] Parsed '{originalTokens[i]}' -> {recipes[i]}");
                }
            }

            // Debug logging for inversion when tendency debug is enabled
            if (TheoryVoicing.GetTendencyDebug())
            {
                Debug.Log($"[Parse Debug] Parsed {originalTokens.Count} chords:");
                for (int i = 0; i < originalTokens.Count && i < recipes.Count; i++)
                {
                    Debug.Log($"[Parse Debug] Step {i + 1}: roman='{originalTokens[i]}', inversion={recipes[i].Inversion}");
                }
            }

            // Use shared playback coroutine (Roman numerals can use auto-correction)
            // Pass the original input text so the helper can parse duration suffixes
            yield return StartCoroutine(PlayChordRecipesCo(key, recipes, originalTokens, durationsInQuarters, skipAutoCorrection: false, romanInputText: input));
        }

        /// <summary>
        /// Shared coroutine that takes a list of ChordRecipe objects and performs rendering + playback.
        /// Used by both Roman numeral and chord symbol input paths.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipes">List of chord recipes to play</param>
        /// <param name="originalTokens">Original input tokens (for display purposes - can be Roman numerals or chord symbols)</param>
        /// <param name="durationsInQuarters">Optional list of durations in quarter notes for each chord (null = default 1 quarter each)</param>
        /// <param name="skipAutoCorrection">If true, skips quality adjustment (for chord symbols which should sound exactly as typed)</param>
        /// <param name="romanInputText">Optional original Roman numeral input text (for duration suffix parsing). If null, uses existing region construction.</param>
        /// <param name="chordSymbolInputText">Optional original chord symbol input text (for duration suffix parsing). If null, uses existing region construction.</param>
        private IEnumerator PlayChordRecipesCo(TheoryKey key, List<ChordRecipe> recipes, List<string> originalTokens, List<int> durationsInQuarters = null, bool skipAutoCorrection = false, string romanInputText = null, string chordSymbolInputText = null)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log("[ENTRY_POINT] PlayChordRecipesCo called (Play mode)");
            
            // Clear both viewers before starting new progression
            if (voicingViewer != null)
            {
                voicingViewer.Clear();
            }
            
            // Clear chord grid container (will be repopulated by RenderChordGrid, but clear early for consistency)
            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Adjust recipes to match diatonic triad quality for the mode (if enabled and not skipping)
            var adjustedRecipes = new List<ChordRecipe>(recipes.Count);
            var adjustedNumerals = new List<string>(recipes.Count);
            var hadAdjustments = false;
            var warningBuilder = new System.Text.StringBuilder();
            
            for (int i = 0; i < recipes.Count; i++)
            {
                var originalRecipe = recipes[i];
                
                if (!skipAutoCorrection && autoCorrectToMode)
                {
                    var adjusted = TheoryChord.AdjustTriadQualityToMode(key, originalRecipe, out bool wasAdjusted);
                    
                    adjustedRecipes.Add(adjusted);
                    
                    // Generate Roman numeral from adjusted recipe (key-aware to show 'n' when appropriate)
                    string adjustedNumeral = TheoryChord.RecipeToRomanNumeral(key, adjusted);
                    adjustedNumerals.Add(adjustedNumeral);
                    
                    if (wasAdjusted)
                    {
                        hadAdjustments = true;
                        warningBuilder.AppendLine(
                            $"Adjusted chord {i + 1} ('{originalTokens[i]}' → '{adjustedNumeral}') to {adjusted.Quality} to fit {key}.");
                        
                        if (enableDebugLogs)
                            Debug.Log($"[ChordLab] Adjusted '{originalTokens[i]}' from {originalRecipe.Quality} to {adjusted.Quality} (now '{adjustedNumeral}')");
                    }
                }
                else
                {
                    // No correction: keep original recipe (chord symbols always skip correction)
                    adjustedRecipes.Add(originalRecipe);
                    // For chord symbols, generate Roman numeral from recipe for display
                    // For Roman numerals, use original token
                    if (skipAutoCorrection)
                    {
                        string roman = TheoryChord.RecipeToRomanNumeral(key, originalRecipe);
                        adjustedNumerals.Add(roman);
                    }
                    else
                    {
                    adjustedNumerals.Add(originalTokens[i]);
                    }
                }
            }

            // Build regions using shared helper if Roman or chord symbol input is available, otherwise build from recipes
            List<ChordRegion> regions = null;
            List<ChordEvent> chordEvents = null;
            List<VoicedChord> voicedChords = null;

            // Use shared helper if chord symbol input text is available (supports :N duration suffixes)
            if (!string.IsNullOrWhiteSpace(chordSymbolInputText) && skipAutoCorrection)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Attempting to use BuildRegionsFromChordSymbolInput with input: '{chordSymbolInputText}'");
                    Debug.Log($"[ChordLab] Pre-parsed durationsInQuarters from parameter: {(durationsInQuarters != null ? $"[{string.Join(", ", durationsInQuarters)}]" : "null")}");
                }

                // Build melody MIDI list if test melody is enabled
                List<int> melodyMidiList = null;
                if (useTestMelodyForPlayback)
                {
                    // Estimate count from recipes (helper will parse and build correct count)
                    int estimatedCount = recipes != null ? recipes.Count : 0;
                    melodyMidiList = new List<int>(estimatedCount);
                    if (testMelodyDegrees == null || testMelodyDegrees.Length == 0)
                    {
                        testMelodyDegrees = new int[] { 3, 4, 4, 2, 1 };
                    }
                    
                    int melodyMinMidi = 60; // C4
                    int melodyMaxMidi = 80; // E5
                    
                    // Build melody for estimated count (will be matched to actual regions by helper)
                    for (int i = 0; i < estimatedCount; i++)
                    {
                        int degreeIndex = i % testMelodyDegrees.Length;
                        int degree = testMelodyDegrees[degreeIndex];
                        int baseMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                        if (baseMidi >= 0)
                        {
                            int melodyMidi = EnsureInRange(baseMidi, melodyMinMidi, melodyMaxMidi);
                            melodyMidiList.Add(melodyMidi);
                            
                            if (enableDebugLogs)
                            {
                                string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidi, key);
                                Debug.Log($"[ChordLab] Chord {i + 1}: melody = {melodyName} (MIDI {melodyMidi}, degree {degree})");
                            }
                        }
                        else
                        {
                            melodyMidiList.Add(-1); // Invalid, will be ignored
                        }
                    }
                }

                regions = BuildRegionsFromChordSymbolInput(chordSymbolInputText, key, timelineSpec, melodyMidiList);
                
                if (enableDebugLogs)
                {
                    if (regions != null)
                        Debug.Log($"[ChordLab] BuildRegionsFromChordSymbolInput succeeded, created {regions.Count} regions");
                    else
                        Debug.LogWarning($"[ChordLab] BuildRegionsFromChordSymbolInput returned null, falling back to old code");
                }
                
                // Extract chordEvents from regions for VoicingViewer
                if (regions != null)
                {
                    chordEvents = regions.Select(r => r.chordEvent).ToList();
                }
            }
            // Use shared helper if Roman input text is available (supports :N duration suffixes)
            else if (!string.IsNullOrWhiteSpace(romanInputText) && !skipAutoCorrection)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Attempting to use BuildRegionsFromRomanInput with input: '{romanInputText}'");
                    Debug.Log($"[ChordLab] Pre-parsed durationsInQuarters from parameter: {(durationsInQuarters != null ? $"[{string.Join(", ", durationsInQuarters)}]" : "null")}");
                }

                // Build melody MIDI list if test melody is enabled
                List<int> melodyMidiList = null;
                if (useTestMelodyForPlayback)
                {
                    // Estimate count from recipes (helper will parse and build correct count)
                    int estimatedCount = recipes != null ? recipes.Count : 0;
                    melodyMidiList = new List<int>(estimatedCount);
                    if (testMelodyDegrees == null || testMelodyDegrees.Length == 0)
                    {
                        testMelodyDegrees = new int[] { 3, 4, 4, 2, 1 };
                    }
                    
                    int melodyMinMidi = 60; // C4
                    int melodyMaxMidi = 80; // E5
                    
                    // Build melody for estimated count (will be matched to actual regions by helper)
                    for (int i = 0; i < estimatedCount; i++)
                    {
                        int degreeIndex = i % testMelodyDegrees.Length;
                        int degree = testMelodyDegrees[degreeIndex];
                        int baseMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                        if (baseMidi >= 0)
                        {
                            int melodyMidi = EnsureInRange(baseMidi, melodyMinMidi, melodyMaxMidi);
                            melodyMidiList.Add(melodyMidi);
                            
                            if (enableDebugLogs)
                            {
                                string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidi, key);
                                Debug.Log($"[ChordLab] Chord {i + 1}: melody = {melodyName} (MIDI {melodyMidi}, degree {degree})");
                            }
                        }
                        else
                        {
                            melodyMidiList.Add(-1); // Invalid, will be ignored
                        }
                    }
                }

                regions = BuildRegionsFromRomanInput(romanInputText, key, timelineSpec, melodyMidiList, skipAutoCorrection: false);
                
                if (enableDebugLogs)
                {
                    if (regions != null)
                        Debug.Log($"[ChordLab] BuildRegionsFromRomanInput succeeded, created {regions.Count} regions");
                    else
                        Debug.LogWarning($"[ChordLab] BuildRegionsFromRomanInput returned null, falling back to old code");
                }
                
                // Extract chordEvents from regions for VoicingViewer
                if (regions != null)
                {
                    chordEvents = regions.Select(r => r.chordEvent).ToList();
                }
            }

            // Fallback: build chordEvents and regions from recipes (for chord symbols or when helper unavailable)
            if (regions == null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Using fallback region construction (helper unavailable or returned null)");
                    Debug.Log($"[ChordLab] Fallback durationsInQuarters: {(durationsInQuarters != null ? $"[{string.Join(", ", durationsInQuarters)}]" : "null")}");
                }

                try
                {
                    chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);
                    
                    // Debug logging: For each chord, log original token and RequestedExtensions
                    if (TheoryVoicing.s_debugTensionDetect && chordEvents != null)
                    {
                        for (int i = 0; i < chordEvents.Count; i++)
                        {
                            var req = chordEvents[i].Recipe.RequestedExtensions;
                            var reqList = new List<string>();
                            if (req.Sus4) reqList.Add("sus4");
                            if (req.Add9) reqList.Add("add9");
                            if (req.Add11) reqList.Add("add11");
                            if (req.Tension9) reqList.Add("9");
                            if (req.TensionFlat9) reqList.Add("b9");
                            if (req.TensionSharp11) reqList.Add("#11");
                            string reqStr = reqList.Count > 0 ? string.Join(",", reqList) : "none";
                            string token = originalTokens != null && i < originalTokens.Count ? originalTokens[i] : "?";
                            UnityEngine.Debug.Log(
                                $"[PLAY_DEBUG] === CHORD {i} (PlayChordRecipesCo entry) ===\n" +
                                $"  originalToken='{token}'\n" +
                                $"  ChordRecipe.RequestedExtensions=[{reqStr}]\n" +
                                $"  TensionFlat9={req.TensionFlat9}, Tension9={req.Tension9}, TensionSharp11={req.TensionSharp11}");
                        }
                    }
                    
                    // Build test melody if enabled
                    if (useTestMelodyForPlayback && chordEvents != null)
                    {
                        if (testMelodyDegrees == null || testMelodyDegrees.Length == 0)
                        {
                            testMelodyDegrees = new int[] { 3, 4, 4, 2, 1 };
                        }
                        
                        int melodyMinMidi = 60; // C4
                        int melodyMaxMidi = 80; // E5
                        
                        for (int i = 0; i < chordEvents.Count; i++)
                        {
                            int degreeIndex = i % testMelodyDegrees.Length;
                            int degree = testMelodyDegrees[degreeIndex];
                            int baseMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                            if (baseMidi >= 0)
                            {
                                int melodyMidi = EnsureInRange(baseMidi, melodyMinMidi, melodyMaxMidi);
                                melodyMidi += MelodyOffsetSemitones;
                                
                                chordEvents[i] = new ChordEvent
                                {
                                    Key = chordEvents[i].Key,
                                    Recipe = chordEvents[i].Recipe,
                                    TimeBeats = chordEvents[i].TimeBeats,
                                    MelodyMidi = melodyMidi
                                };
                                
                                if (enableDebugLogs)
                                {
                                    string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidi, key);
                                    Debug.Log($"[ChordLab] Chord {i + 1}: melody = {melodyName} (MIDI {melodyMidi}, degree {degree})");
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ChordLab] Failed to build chord events: {ex}");
                    chordEvents = null;
                }

                // Build regions from chordEvents
                if (chordEvents != null)
                {
                    regions = new List<ChordRegion>(chordEvents.Count);
                    int cumulativeStartTick = 0;
                    for (int i = 0; i < chordEvents.Count; i++)
                    {
                        // Get debug label from originalTokens if available, otherwise from adjustedNumerals
                        string debugLabel = null;
                        if (originalTokens != null && i < originalTokens.Count)
                        {
                            debugLabel = originalTokens[i];
                        }
                        else if (adjustedNumerals != null && i < adjustedNumerals.Count)
                        {
                            debugLabel = adjustedNumerals[i];
                        }

                        // Get duration in quarters (default to 1 if not provided)
                        int quarters = (durationsInQuarters != null && i < durationsInQuarters.Count) 
                            ? durationsInQuarters[i] 
                            : 1;
                        int durationTicks = quarters * timelineSpec.ticksPerQuarter;

                        if (enableDebugLogs && i == 0)
                        {
                            Debug.Log($"[ChordLab Fallback] Region 0: quarters={quarters} (from durationsInQuarters[{i}]={(durationsInQuarters != null && i < durationsInQuarters.Count ? durationsInQuarters[i].ToString() : "N/A")}), durationTicks={durationTicks}");
                        }

                        var region = new ChordRegion
                        {
                            startTick = cumulativeStartTick,
                            durationTicks = durationTicks,
                            chordEvent = chordEvents[i],
                            debugLabel = debugLabel
                        };
                        regions.Add(region);

                        // Update cumulative startTick for next region
                        cumulativeStartTick += durationTicks;

                        // Debug logging for constructed regions
                        if (enableDebugLogs)
                        {
                            int rootPc = TheoryScale.GetDegreePitchClass(key, chordEvents[i].Recipe.Degree);
                            if (rootPc >= 0)
                            {
                                rootPc = (rootPc + chordEvents[i].Recipe.RootSemitoneOffset + 12) % 12;
                                if (rootPc < 0) rootPc += 12;
                            }
                            Debug.Log($"[ChordLab Region] Index={i}, Roman='{debugLabel ?? "?"}', startTick={region.startTick}, durationTicks={region.durationTicks} (quarters={quarters}), rootPc={rootPc}, ticksPerQuarter={timelineSpec.ticksPerQuarter}");
                        }
                    }
                }
            }

            // Store regions for debug inspection
            if (regions != null)
            {
                _lastRegions = regions;
            }

            // Create diagnostics collector at the start (will be populated if voicing is used)
            var diags = new DiagnosticsCollector();
            
            // Store diagnostics collector early so LogRegionHeadline can access it during playback
            _lastDiagnostics = diags;
            if (_lastDiagnostics != null)
            {
                _lastDiagnostics.EnableTrace = enableUnityTraceLogs;
            }

            // Voice the progression (if enabled for playback, or always for display)
            if ((useVoicingEngine || voicingViewer != null) && regions != null)
            {
                try
                {
                    // Calculate upper voice MIDI ranges based on rootOctave
                    var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
                    
                    // Debug logging for soprano range
                    if (TheoryVoicing.GetTendencyDebug())
                    {
                        Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
                    }

                        // Call VoiceLeadRegions adapter (routes to appropriate voicing method)
                        voicedChords = TheoryVoicing.VoiceLeadRegions(
                            key,
                            timelineSpec,
                            regions,
                            useMelodyConstraint: useTestMelodyForPlayback,
                            numVoices: 4,
                            rootOctave: rootOctave,
                            bassOctave: rootOctave - 1,
                            upperMinMidi: upperMinMidi,
                            upperMaxMidi: upperMaxMidi,
                            diags: diags);
                        
                        if (enableDebugLogs && voicedChords != null && voicedChords.Count == adjustedRecipes.Count)
                        {
                            Debug.Log($"[ChordLab] Using {(useTestMelodyForPlayback ? "melody-constrained" : "chord-only")} voicing ({voicedChords.Count} voiced chords)");
                            
                            // Log SATB MIDI for each chord
                            for (int i = 0; i < voicedChords.Count; i++)
                            {
                                var voiced = voicedChords[i];
                                if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                                {
                                    // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                                    int bass = voiced.VoicesMidi[0];
                                    int tenor = voiced.VoicesMidi[1];
                                    int alto = voiced.VoicesMidi[2];
                                    int soprano = voiced.VoicesMidi[3];
                                    string bassName = TheoryPitch.GetPitchNameFromMidi(bass, key);
                                    string tenorName = TheoryPitch.GetPitchNameFromMidi(tenor, key);
                                    string altoName = TheoryPitch.GetPitchNameFromMidi(alto, key);
                                    string sopranoName = TheoryPitch.GetPitchNameFromMidi(soprano, key);
                                    Debug.Log($"[ChordLab SATB] Chord {i + 1}: B={bass}({bassName}) T={tenor}({tenorName}) A={alto}({altoName}) S={soprano}({sopranoName})");
                                }
                                else if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                                {
                                    Debug.Log($"[ChordLab SATB] Chord {i + 1}: MIDI notes [{string.Join(", ", voiced.VoicesMidi)}]");
                                }
                            }
                        }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ChordLab] Voice-leading failed. Exception: {ex}");
                    voicedChords = null;
                }
            }
            
            if (!useVoicingEngine || voicedChords == null || voicedChords.Count != adjustedRecipes.Count)
            {
                if (enableDebugLogs && useVoicingEngine)
                {
                    Debug.Log("[ChordLab] Using root-position fallback for playback");
                }
            }

            // Build chords from adjusted recipes (used as fallback or for display)
            var chords = new List<int[]>(adjustedRecipes.Count);
            foreach (var recipe in adjustedRecipes)
            {
                var chordMidi = TheoryChord.BuildChord(key, recipe, rootOctave);
                if (chordMidi != null && chordMidi.Length > 0)
                {
                    chords.Add(chordMidi);
                }
            }
            
            if (chords == null || chords.Count == 0)
            {
                string error = "Error: Failed to build any chords.";
                Debug.LogError($"[ChordLab] {error}");
                UpdateStatus(error);
                playRoutine = null;
                yield break;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Built {chords.Count} chords");

            // Render chord grid with original tokens/recipes for analysis, adjusted recipes for display
            RenderChordGrid(key, originalTokens, recipes, chords, adjustedRecipes, null, null, "Play");

            // Timeline v1: Build SATB timeline and update VoicingViewer if enabled
            if (voicingViewer != null && useVoicingTimelineView && regions != null && regions.Count > 0)
            {
                // Extract VoicesMidi arrays from voiced chords (use fallback root-position chords if voiced unavailable)
                var voicesPerRegion = new List<int[]>();
                for (int i = 0; i < regions.Count; i++)
                {
                    if (voicedChords != null && voicedChords.Count == regions.Count && i < voicedChords.Count)
                    {
                        var voiced = voicedChords[i];
                        if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                        {
                            voicesPerRegion.Add(voiced.VoicesMidi);
                        }
                        else
                        {
                            // Fallback: use root-position chord if voiced chord is invalid
                            if (i < chords.Count && chords[i] != null && chords[i].Length > 0)
                            {
                                // Convert root-position chord to SATB format (repeat notes if needed)
                                int[] satb = new int[4];
                                for (int v = 0; v < 4; v++)
                                {
                                    satb[v] = v < chords[i].Length ? chords[i][v] : chords[i][0];
                                }
                                voicesPerRegion.Add(satb);
                            }
                            else
                            {
                                voicesPerRegion.Add(new int[4]);
                            }
                        }
                    }
                    else
                    {
                        // Fallback: use root-position chord
                        if (i < chords.Count && chords[i] != null && chords[i].Length > 0)
                        {
                            int[] satb = new int[4];
                            for (int v = 0; v < 4; v++)
                            {
                                satb[v] = v < chords[i].Length ? chords[i][v] : chords[i][0];
                            }
                            voicesPerRegion.Add(satb);
                        }
                        else
                        {
                            voicesPerRegion.Add(new int[4]);
                        }
                    }
                }
                
                // Build timeline and update viewer
                BuildSatbTimelineForSimplePlay(timelineSpec, regions, voicesPerRegion,
                    out var bass, out var tenor, out var alto, out var soprano, out var chordIsAttack);
                
                // Create dummy melodyIsAttack list (unused when topIsMelody is false)
                var dummyMelodyIsAttack = new List<bool>(bass.Count);
                for (int i = 0; i < bass.Count; i++)
                {
                    dummyMelodyIsAttack.Add(false);
                }
                
                // Build chord symbol and Roman numeral data for header rows
                BuildChordHeaderData(regions, key, out var absoluteChordSymbolsPerRegion, out var romanNumeralsPerRegion, out var isDiatonicPerRegion, out var regionDurationTicks);
                
                voicingViewer.ShowTimelineTopAndSatb(
                    topLine: soprano,
                    topIsMelody: false,
                    alto: alto,
                    tenor: tenor,
                    bass: bass,
                    melodyIsAttack: dummyMelodyIsAttack,
                    chordIsAttack: chordIsAttack,
                    absoluteChordSymbolsPerRegion: absoluteChordSymbolsPerRegion,
                    romanNumeralsPerRegion: romanNumeralsPerRegion,
                    isDiatonicPerRegion: isDiatonicPerRegion,
                    regionDurationTicks: regionDurationTicks,
                    timelineSpec: timelineSpec);
            }

            // Clear console at playback start
            ClearConsole();
            
            // Hide all chord columns for progressive reveal
            HideAllChordColumns();

            // Store diagnostics for per-region logging (will be populated during voicing)
            // Note: diagnostics are collected during voicing, so we'll use _lastDiagnostics after voicing completes

            // Play each chord
            int chordIndex = 0;
            foreach (var chord in chords)
            {
                if (chord == null || chord.Length == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ChordLab] Chord {chordIndex} is null or empty, skipping");
                    continue;
                }

                // Choose between voiced chords and fallback root-position chords
                int[] midiNotesToPlay = chord; // Default to fallback
                
                if (useVoicingEngine && voicedChords != null && 
                    voicedChords.Count == adjustedRecipes.Count && 
                    chordIndex < voicedChords.Count)
                {
                    var voiced = voicedChords[chordIndex];
                    if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                    {
                        midiNotesToPlay = voiced.VoicesMidi;
                    }
                }

                // Update VoicingViewer if available (legacy mode - only if timeline view is disabled)
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                if (voicingViewer != null && !useVoicingTimelineView)
                {
                    IReadOnlyList<int> midiNotesForDisplay = midiNotesToPlay;
                    ChordEvent? chordEventForDisplay = null;
                    
                    // Use voiced chords for display if available (even if voicing engine is disabled)
                    // Pass VoicesMidi directly - VoicingViewer expects [Bass, Tenor, Alto, Soprano] order
                    if (voicedChords != null && voicedChords.Count == adjustedRecipes.Count && chordIndex < voicedChords.Count)
                    {
                        var voiced = voicedChords[chordIndex];
                        if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                        {
                            midiNotesForDisplay = voiced.VoicesMidi;
                        }
                    }
                    
                    // Get corresponding chord event if available
                    if (chordEvents != null && chordIndex < chordEvents.Count)
                    {
                        chordEventForDisplay = chordEvents[chordIndex];
                    }
                    
                    // Compute trailing spaces for duration-based spacing
                    int durationQuarters = GetDurationQuarters(chordIndex);
                    int trailingSpaces = GetVoicingPaddingSpaces(durationQuarters);
                    
                    // Pass VoicesMidi array directly - VoicingViewer will use index order [Bass, Tenor, Alto, Soprano]
                    voicingViewer.ShowVoicing(
                        key,
                        stepIndex: chordIndex + 1,
                        totalSteps: chords.Count,
                        midiNotes: midiNotesForDisplay,
                        chordEvent: chordEventForDisplay,
                        trailingSpaces: trailingSpaces);
                }

                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Playing chord {chordIndex + 1}/{chords.Count}: MIDI notes [{string.Join(", ", midiNotesToPlay)}]");

                // Update piano keyboard display if available
                if (pianoKeyboardDisplay != null && midiNotesToPlay != null && midiNotesToPlay.Length > 0)
                {
                    pianoKeyboardDisplay.SetActiveNotes(midiNotesToPlay);
                }

                // Log region headline at region start (user-facing console)
                LogRegionHeadline(chordIndex);
                
                // Reveal and highlight chord column for this region (progressive reveal)
                RevealChordColumn(chordIndex);
                HighlightChordColumn(chordIndex);

                // Play all notes in the chord simultaneously (block chord)
                // This helper handles optional bass doubling based on emphasizeBassWithLowOctave
                PlayChord(midiNotesToPlay, chordDurationSeconds, regionIdx: -1, targetTimeFromStart: -1f);

                // Compute hold time based on region duration (if available)
                float holdSeconds = chordDurationSeconds; // Default fallback
                if (_lastRegions != null && chordIndex < _lastRegions.Count)
                {
                    holdSeconds = GetRegionHoldSeconds(_lastRegions[chordIndex]);
                    if (enableDebugLogs)
                    {
                        var region = _lastRegions[chordIndex];
                        int tpq = (timelineSpec != null) ? timelineSpec.ticksPerQuarter : 4;
                        float quarters = region.durationTicks / (float)tpq;
                        string label = region.debugLabel ?? "?";
                        Debug.Log($"[ChordLab] Playback i={chordIndex} label={label} durationTicks={region.durationTicks} quarters={quarters:F2} holdSeconds={holdSeconds:F2}");
                    }
                }
                else if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Playback i={chordIndex} (no region, using default holdSeconds={holdSeconds:F2})");
                }

                // Wait for chord duration
                // Timeline-based playback: If using regions, don't add gapBetweenChordsSeconds (it's legacy and conceptually invalid)
                // Non-timeline playback: Use gapBetweenChordsSeconds for spacing
                bool isTimelineBased = (_lastRegions != null && chordIndex < _lastRegions.Count);
                float waitTime = isTimelineBased ? holdSeconds : (holdSeconds + gapBetweenChordsSeconds);
                if (enableDebugLogs)
                {
                    string timingType = isTimelineBased ? "timeline-based (no gap)" : "sequential (with gap)";
                    Debug.Log($"[ChordLab] Waiting {waitTime} seconds before next chord ({timingType})");
                }
                yield return new WaitForSeconds(waitTime);
                chordIndex++;
            }

            // Playback complete
            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Playback complete");
            
            // Store diagnostics for potential future use, but don't dump to user console
            // (User console already shows per-region headlines during playback)
            _lastDiagnostics = diags ?? new DiagnosticsCollector();
            if (_lastDiagnostics != null)
            {
                _lastDiagnostics.EnableTrace = enableUnityTraceLogs;
            }
            
            // Note: We no longer call SetDiagnosticsAndRefresh() here to avoid bulk dump
            // Per-region headlines are already logged during playback via LogRegionHeadline()
            
            playRoutine = null;
        }

        /// <summary>
        /// Plays a chord with optional bass doubling an octave below.
        /// Only affects playback; does not modify the original chord data.
        /// </summary>
        /// <param name="midiNotes">The MIDI notes to play</param>
        /// <param name="duration">Duration of each note</param>
        // PlaybackAudit: Data structures for clean playback logging
        private class ScheduledRegion
        {
            public int regionIdx;
            public string label;
            public int startTick;
            public int durationTicks;
            public int? sopranoAnchorMidi;
        }
        
        private class ScheduledMelodyEvent
        {
            public int melodyIdx;
            public int midi;
            public int startTick;
            public int durationTicks;
        }
        
        /// <summary>
        /// EventTrace: Unified event trace model for diagnostic logging.
        /// Tracks an event from creation through scheduling, firing, and cleanup.
        /// </summary>
        private struct EventTrace
        {
            public int eventId; // Unique ID per event in this run
            public int playbackRunId;
            public string type; // "Chord" or "Melody"
            public int? regionIndex;
            public string voice; // "B", "T", "A", "S", or "Melody"
            public int midi;
            public int scheduledTick;
            public float scheduledSec;
            public int durationTicks;
            public float durationSec;
            public string instanceId; // FMOD instance ID (set after NoteOn)
            public string sourceTag; // "SATBVoicer", "MelodyScheduler", "SopranoAnchor", etc.
            
            public string ToCompactLog()
            {
                string regionStr = regionIndex.HasValue ? $"R{regionIndex.Value}" : "-";
                return $"eventId={eventId} run={playbackRunId} type={type} {regionStr} voice={voice} midi={midi} schedTick={scheduledTick} schedSec={scheduledSec:F3} durTick={durationTicks} durSec={durationSec:F3} inst={instanceId} src={sourceTag}";
            }
        }
        
        /// <summary>
        /// NoteHandle: Single source of truth for active note instances
        /// Used by playback layer to track and stop notes accurately
        /// </summary>
        private struct NoteHandle
        {
            public string instanceId; // FMOD instance handle as string
            public int midi;
            public string role; // "ChordB", "ChordT", "ChordA", "ChordS", or "Melody"
            public int? regionIdx; // For chord notes
            public int? melodyIdx; // For melody notes
            public float scheduledOnTime; // Time (from playback start) when this note was scheduled
            public FMOD.Studio.EventInstance instance; // FMOD instance for stopping
            public int eventId; // Event trace ID for diagnostic correlation
            public string sourceTag; // Where this note came from
        }
        
        private class PlaybackAuditEntry
        {
            public string eventType; // "NoteOn" or "NoteOff"
            public string kind; // "Chord" or "Melody"
            public int? regionIdx;
            public int? melodyIdx;
            public string voice; // "B", "T", "A", "S", or "Mel"
            public int midi;
            public string noteName;
            public float targetTimeFromStart;
            public float actualTimeFromStart;
            public string fmodInstanceId;
            public bool isVoiceCollision;
        }
        
        // Global playback run ID counter (increments with each playback)
        private static int _playbackRunCounter = 0;
        private int _currentPlaybackRunId = 0;
        
        // Event ID counter (increments per event in a run)
        private int _eventIdCounter = 0;
        
        // Event trace tracking: eventId -> EventTrace
        private Dictionary<int, EventTrace> _eventTraces = new Dictionary<int, EventTrace>();
        
        // Reverse lookup: instanceId -> eventId (for NoteOff diagnostics)
        private Dictionary<string, int> _instanceIdToEventId = new Dictionary<string, int>();
        
        // CENTRALIZED NOTE HANDLE TRACKING (single source of truth for playback)
        // Maps instanceId -> NoteHandle for accurate NoteOff
        private Dictionary<string, NoteHandle> _activeHandles = new Dictionary<string, NoteHandle>();
        
        // Per-region tracking for efficient region-based stops
        private Dictionary<int, List<string>> _activeHandlesByRegion = new Dictionary<int, List<string>>(); // regionIdx -> list of instanceIds
        
        // Per-melody tracking
        private Dictionary<int, List<string>> _activeHandlesByMelody = new Dictionary<int, List<string>>(); // melodyIdx -> list of instanceIds
        
        // Separate tracking for chord embellishments (bass doubling, etc.) - NOT counted as SATB voices
        private Dictionary<string, NoteHandle> _activeChordEmbellishments = new Dictionary<string, NoteHandle>(); // instanceId -> NoteHandle for embellishments
        private Dictionary<int, List<string>> _activeEmbellishmentsByRegion = new Dictionary<int, List<string>>(); // regionIdx -> list of embellishment instanceIds
        
        // Diagnostic counters for playback summary
        private int _regionsScheduled = 0;
        private int _regionsEntered = 0;
        private int _regionsExited = 0;
        private int _chordNotesOn = 0;
        private int _chordNotesOff = 0;
        private int _melodyNotesOn = 0;
        private int _melodyNotesOff = 0;
        
        // PlaybackAudit: Track playback start time and audit entries for summary (OBSERVATIONAL ONLY)
        private float _currentPlaybackStartTime = 0f;
        private List<PlaybackAuditEntry> _currentAuditEntries = new List<PlaybackAuditEntry>();
        private List<ScheduledRegion> _currentScheduledRegions = new List<ScheduledRegion>();
        private List<ScheduledMelodyEvent> _currentScheduledMelodyEvents = new List<ScheduledMelodyEvent>();
        
        // PlaybackAudit: Track pending note-ons (before FMOD call) to match with callback (OBSERVATIONAL ONLY)
        private Dictionary<int, (string kind, int? regionIdx, int? melodyIdx, string voice, float targetTimeFromStart)> _pendingNoteOns = 
            new Dictionary<int, (string, int?, int?, string, float)>();
        
        // Active Note Tracking for Piano Keyboard Display (Timeline v1)
        // Reference-counted tracking to handle overlapping notes (e.g., melody matching chord tone)
        private Dictionary<int, int> _activeNoteCounts = new Dictionary<int, int>(); // midi -> count
        private int _keyboardPlaybackToken = 0; // Increments on each playback start to invalidate stale coroutines
        
        // PlaybackAudit: Callback handler for FMOD note events (PURELY OBSERVATIONAL - READ-ONLY, NO MUTATIONS)
        private void OnFmodNoteEvent(int midi, FMOD.Studio.EventInstance instance, bool isNoteOn)
        {
            if (!enablePlaybackAudit) return;
            
            TheoryKey key = GetKeyFromDropdowns();
            string noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
            float actualTime = Time.time;
            float actualTimeFromStart = _currentPlaybackStartTime > 0f ? (actualTime - _currentPlaybackStartTime) : 0f;
            
            string instanceId = instance.isValid() ? instance.handle.ToString() : "invalid";
            
            bool isVoiceCollision = false;
            string kind = "Unknown";
            int? regionIdx = null;
            int? melodyIdx = null;
            string voice = "Unknown";
            float targetTimeFromStart = -1f;
            
            if (isNoteOn)
            {
                // OBSERVATIONAL: Check for collisions by reading from centralized tracking (do NOT modify)
                bool alreadyTracked = _activeHandles.ContainsKey(instanceId);
                if (alreadyTracked)
                {
                    isVoiceCollision = true;
                    UnityEngine.Debug.LogWarning($"[PlaybackAudit] VOICE COLLISION DETECTED (observational): MIDI {midi}({noteName}) instance {instanceId} already in _activeHandles!");
                }
                
                // Match with pending note-on (set before FMOD call)
                if (_pendingNoteOns.TryGetValue(midi, out var pending))
                {
                    kind = pending.kind;
                    regionIdx = pending.regionIdx;
                    melodyIdx = pending.melodyIdx;
                    voice = pending.voice;
                    targetTimeFromStart = pending.targetTimeFromStart;
                    _pendingNoteOns.Remove(midi);
                }
                else
                {
                    // Fallback: try to identify from centralized tracking (read-only)
                    if (_activeHandles.TryGetValue(instanceId, out var handle))
                    {
                        kind = handle.role.StartsWith("Chord") ? "Chord" : "Melody";
                        regionIdx = handle.regionIdx;
                        melodyIdx = handle.melodyIdx;
                        voice = handle.role;
                        targetTimeFromStart = handle.scheduledOnTime;
                    }
                    else
                    {
                        // Try to match from recent audit entries
                        var recent = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.midi == midi).OrderByDescending(e => e.actualTimeFromStart).FirstOrDefault();
                        if (recent != null)
                        {
                            kind = recent.kind;
                            regionIdx = recent.regionIdx;
                            melodyIdx = recent.melodyIdx;
                            voice = recent.voice;
                            targetTimeFromStart = recent.targetTimeFromStart;
                        }
                    }
                }
            }
            else
            {
                // Note-off: OBSERVATIONAL - read from centralized tracking (do NOT modify tracking dictionaries)
                if (_activeHandles.TryGetValue(instanceId, out var handle))
                {
                    kind = handle.role.StartsWith("Chord") ? "Chord" : "Melody";
                    regionIdx = handle.regionIdx;
                    melodyIdx = handle.melodyIdx;
                    voice = handle.role;
                    targetTimeFromStart = handle.scheduledOnTime;
                }
                else
                {
                    // Unknown instance - log error (observational only, do NOT stop or modify state)
                    UnityEngine.Debug.LogError($"[PlaybackAudit ERROR] NoteOff for unknown instance {instanceId} MIDI {midi}({noteName}). " +
                        $"ActiveHandles count: {_activeHandles.Count}. This suggests tracking mismatch.");
                }
            }
            
            // SINGLE AUDIT ENTRY PER EVENT (observational record, no state mutations)
            _currentAuditEntries.Add(new PlaybackAuditEntry
            {
                eventType = isNoteOn ? "NoteOn" : "NoteOff",
                kind = kind,
                regionIdx = regionIdx,
                melodyIdx = melodyIdx,
                voice = voice,
                midi = midi,
                noteName = noteName,
                targetTimeFromStart = targetTimeFromStart,
                actualTimeFromStart = actualTimeFromStart,
                fmodInstanceId = instanceId,
                isVoiceCollision = isVoiceCollision
            });
        }
        
        /// <summary>
        /// Logs a playback state snapshot at critical boundaries.
        /// </summary>
        private void LogPlaybackSnapshot(string tag, int run, int regionIndex, int? expectedChordNotes = null)
        {
            int activeChordCount = _activeHandles.Count(kv => kv.Value.role.StartsWith("Chord"));
            int activeMelodyCount = _activeHandles.Count(kv => kv.Value.role == "Melody");
            
            // Collect active chord instance IDs
            var activeChordIds = _activeHandles
                .Where(kv => kv.Value.role.StartsWith("Chord") && kv.Value.regionIdx == regionIndex)
                .Select(kv => kv.Key)
                .ToList();
            
            // Collect active melody instance IDs
            var activeMelodyIds = _activeHandles
                .Where(kv => kv.Value.role == "Melody")
                .Select(kv => kv.Key)
                .ToList();
            
            string expectedStr = expectedChordNotes.HasValue ? $" expectedChordNotes={expectedChordNotes.Value}" : "";
            
            LogPlaybackSnapshotMsg("SNAPSHOT",
                $"{tag} RUN={run} region={regionIndex}{expectedStr} " +
                $"activeChordCount={activeChordCount} " +
                $"activeMelodyCount={activeMelodyCount} " +
                $"activeChordIds=[{string.Join(",", activeChordIds)}] " +
                $"activeMelodyIds=[{string.Join(",", activeMelodyIds)}]");
        }
        
        /// <summary>
        /// Stops chord voices by region using centralized NoteHandle tracking.
        /// CORE PLAYBACK LOGIC - not dependent on audit state.
        /// </summary>
        /// <param name="regionIdx">Stop voices from this region (must be non-null)</param>
        /// <param name="reason">Reason for stopping (for logging)</param>
        private void StopChordVoicesByRegion(int regionIdx, string reason)
        {
            if (synth == null) return;
            
            // Add call site logging before cleanup
            string stackTrace = System.Environment.StackTrace;
            string[] stackLines = stackTrace.Split('\n');
            string formattedStack = string.Join("\n", stackLines.Take(5));
            float stopTime = Time.time;
            float stopTimeFromStart = _currentPlaybackStartTime > 0f ? (stopTime - _currentPlaybackStartTime) : 0f;
            
            LogPlaybackStack("CLEANUP_CALLSITE",
                $"RUN={_currentPlaybackRunId} regionIndex={regionIdx} " +
                $"time={stopTime:F4} timeFromStart={stopTimeFromStart:F4} reason={reason}",
                formattedStack);
            
            TheoryKey key = GetKeyFromDropdowns();
            
            // Get instance IDs for SATB voices from this region
            var satbInstanceIds = _activeHandlesByRegion.TryGetValue(regionIdx, out var satbIds) ? new List<string>(satbIds) : new List<string>();
            
            // Get instance IDs for embellishments from this region
            var embellishmentInstanceIds = _activeEmbellishmentsByRegion.TryGetValue(regionIdx, out var embIds) ? new List<string>(embIds) : new List<string>();
            
            // Combine all instances to stop
            var instancesToStop = new List<string>(satbInstanceIds);
            instancesToStop.AddRange(embellishmentInstanceIds);
            
            if (instancesToStop.Count == 0)
            {
                if (enablePlaybackTrace)
                {
                    UnityEngine.Debug.Log($"[TRACE] REGION_EXIT r={regionIdx} t={stopTimeFromStart:F4}s stoppingChordNotes=0 stoppingEmbellishments=0 (none active)");
                }
                return;
            }
            
            // Count active SATB chord voices before cleanup (exclude embellishments)
            int activeChordCount = _activeHandles.Count(kv => 
                kv.Value.role.StartsWith("Chord") && 
                !kv.Value.role.Contains("Doubling") &&
                (kv.Value.role == "ChordB" || kv.Value.role == "ChordT" || kv.Value.role == "ChordA" || kv.Value.role == "ChordS"));
            
            // Count active embellishments for this region
            int activeEmbellishmentCountBefore = embellishmentInstanceIds.Count;
            
            // SNAPSHOT: Region exit pre-cleanup
            LogPlaybackSnapshot("REGION_EXIT_PRE", _currentPlaybackRunId, regionIdx);
            
            // CHORD_CLEANUP: Log cleanup with explicit reason (include embellishment counts)
            string cleanupReason = reason.Contains("transition") ? "region_transition" : 
                                 (reason.Contains("cleanup") ? "end_of_playback" : 
                                 (reason.Contains("ERROR") ? "error_mismatch" : "manual_stop"));
            
            LogPlaybackVerbose("CHORD_CLEANUP",
                $"RUN={_currentPlaybackRunId} region={regionIdx} " +
                $"stoppingVoices={satbInstanceIds.Count} stoppingEmbellishments={embellishmentInstanceIds.Count} " +
                $"activeBefore={activeChordCount} activeEmbellishmentBefore={activeEmbellishmentCountBefore} reason={cleanupReason}");
            
            // OBSERVATIONAL LOGGING: Log what we're about to stop (before stopping)
            if (enablePlaybackTrace)
            {
                UnityEngine.Debug.Log($"[TRACE] REGION_EXIT r={regionIdx} t={stopTimeFromStart:F4}s stoppingVoices={satbInstanceIds.Count} stoppingEmbellishments={embellishmentInstanceIds.Count} ({reason})");
            }
            if (enablePlaybackAudit)
            {
                var stopDetails = new List<string>();
                foreach (var instanceId in satbInstanceIds)
                {
                    if (_activeHandles.TryGetValue(instanceId, out var handle))
                    {
                        stopDetails.Add($"Voice {handle.role} midi={handle.midi} inst={instanceId}");
                    }
                    else
                    {
                        stopDetails.Add($"Unknown voice inst={instanceId}");
                    }
                }
                foreach (var instanceId in embellishmentInstanceIds)
                {
                    if (_activeChordEmbellishments.TryGetValue(instanceId, out var handle))
                    {
                        string voiceTag = handle.role.Replace("ChordDoubling", "");
                        stopDetails.Add($"Embellishment {voiceTag} midi={handle.midi} inst={instanceId}");
                    }
                    else
                    {
                        stopDetails.Add($"Unknown embellishment inst={instanceId}");
                    }
                }
                UnityEngine.Debug.Log($"[STOP_AUDIT t={stopTime:F4} offset={stopTimeFromStart:F4}] stoppingVoices={satbInstanceIds.Count} stoppingEmbellishments={embellishmentInstanceIds.Count} ({reason}): {string.Join(", ", stopDetails)}");
            }
            
            // CORE PLAYBACK LOGIC: Stop each voice/embellishment using its exact instance handle
            foreach (var instanceId in instancesToStop)
            {
                NoteHandle? handle = null;
                bool isEmbellishment = false;
                
                // Try to find in SATB voices first
                if (_activeHandles.TryGetValue(instanceId, out var satbHandle))
                {
                    handle = satbHandle;
                    isEmbellishment = false;
                }
                // Then try embellishments
                else if (_activeChordEmbellishments.TryGetValue(instanceId, out var embHandle))
                {
                    handle = embHandle;
                    isEmbellishment = true;
                }
                
                if (!handle.HasValue)
                {
                    // Instance not found in either collection - log error with diagnostic info
                    int activeChordVoiceCount = _activeHandles.Count(kv => 
                        kv.Value.role.StartsWith("Chord") && 
                        !kv.Value.role.Contains("Doubling") &&
                        (kv.Value.role == "ChordB" || kv.Value.role == "ChordT" || kv.Value.role == "ChordA" || kv.Value.role == "ChordS"));
                    int activeEmbellishmentCount = _activeChordEmbellishments.Count;
                    
                    LogPlaybackError("StopChordVoicesByRegion ERROR", 
                        $"Instance {instanceId} not found in voices or embellishments. activeChordVoiceCount={activeChordVoiceCount} activeEmbellishmentCount={activeEmbellishmentCount} " +
                        $"RUN={_currentPlaybackRunId} r={regionIdx} reason={reason}");
                    continue;
                }
                
                var actualHandle = handle.Value;
                
                // Verify this is from the correct region
                if (actualHandle.regionIdx != regionIdx)
                {
                    LogPlaybackError("StopChordVoicesByRegion ERROR", 
                        $"Handle {instanceId} region mismatch: regionIdx={actualHandle.regionIdx}, expected={regionIdx}. Skipping.");
                    continue;
                }
                
                // Count active SATB chord voices before NoteOff (exclude embellishments)
                int activeChordBeforeNoteOff = _activeHandles.Count(kv => 
                    kv.Value.role.StartsWith("Chord") && 
                    !kv.Value.role.Contains("Doubling") &&
                    (kv.Value.role == "ChordB" || kv.Value.role == "ChordT" || kv.Value.role == "ChordA" || kv.Value.role == "ChordS"));
                    
                // Stop the FMOD instance
                if (actualHandle.instance.isValid())
                {
                    synth.NoteOffByInstance(actualHandle.instance, actualHandle.midi);
                    // Note: FMOD callback (if enabled) will observe this NoteOff and add to audit entries
                }
                
                // Log cleanup based on type
                string reasonStr = reason.Contains("transition") ? "region_exit" : (reason.Contains("cleanup") ? "cleanup" : "forced");
                float noteOffTime = Time.time - _currentPlaybackStartTime;
                
                if (isEmbellishment)
                {
                    // Embellishment cleanup - extract voiceTag and parentVoice from role
                    string voiceTag = actualHandle.role.Replace("ChordDoubling", "");
                    // Extract parentVoice from voiceTag (e.g., "B_dbl" -> parentVoice="B")
                    string parentVoice = "B"; // Default, but try to extract from voiceTag
                    if (voiceTag.StartsWith("B_"))
                    {
                        parentVoice = "B";
                    }
                    else if (voiceTag.StartsWith("T_"))
                    {
                        parentVoice = "T";
                    }
                    else if (voiceTag.StartsWith("A_"))
                    {
                        parentVoice = "A";
                    }
                    else if (voiceTag.StartsWith("S_"))
                    {
                        parentVoice = "S";
                    }
                    
                    LogPlaybackVerbose("CHORD_EMBELLISHMENT_OFF", 
                        $"RUN={_currentPlaybackRunId} region={regionIdx} voiceTag={voiceTag} parentVoice={parentVoice} midi={actualHandle.midi} inst={instanceId} reason={reasonStr}");
                }
                else
                {
                    // SATB voice cleanup
                    string voiceName = actualHandle.role.Replace("Chord", "");
                    int activeChordAfterNoteOffFinal = activeChordBeforeNoteOff - 1;
                        
                        LogPlaybackVerbose("CHORD_NOTE_OFF", 
                            $"RUN={_currentPlaybackRunId} region={regionIdx} voice={voiceName} midi={actualHandle.midi} inst={instanceId} " +
                            $"activeChordAfter={activeChordAfterNoteOffFinal} reason={reasonStr}");
                        _chordNotesOff++;
                    }
                    
                    // Remove from appropriate tracking collection
                    if (isEmbellishment)
                    {
                        _activeChordEmbellishments.Remove(instanceId);
                    }
                    else
                    {
                        _activeHandles.Remove(instanceId);
                    }
                }
            
            // Clear the region's instance lists
            _activeHandlesByRegion.Remove(regionIdx);
            _activeEmbellishmentsByRegion.Remove(regionIdx);
        }
        
        /// <summary>
        /// PlaybackTrace: Compact summary log for easy comparison across audit on/off
        /// </summary>
        private void LogPlaybackTrace(IReadOnlyList<ChordRegion> regions, List<Sonoria.MusicTheory.Timeline.MelodyEvent> melodyEvents, TheoryKey key, TimelineSpec timelineSpec)
        {
            UnityEngine.Debug.Log($"[TRACE] === PLAYBACK TRACE SUMMARY ===");
            
            // 1) Schedule summary: Regions
            if (regions != null && regions.Count > 0)
            {
                UnityEngine.Debug.Log($"[TRACE] Scheduled Regions ({regions.Count}):");
                for (int i = 0; i < regions.Count; i++)
                {
                    var region = regions[i];
                    int? sopranoMidi = region.chordEvent.MelodyMidi;
                    string sopranoStr = sopranoMidi.HasValue ? $"{sopranoMidi.Value}({TheoryPitch.GetPitchNameFromMidi(sopranoMidi.Value, key)})" : "none";
                    float startSec = SecondsFromTicks(region.startTick, timelineSpec);
                    float endSec = SecondsFromTicks(region.startTick + region.durationTicks, timelineSpec);
                    UnityEngine.Debug.Log($"[TRACE]   R{i}: startTick={region.startTick} durationTicks={region.durationTicks} startSec={startSec:F3}s endSec={endSec:F3}s label='{region.debugLabel ?? "?"}' soprano={sopranoStr}");
                }
            }
            
            // 2) Schedule summary: Melody
            if (melodyEvents != null && melodyEvents.Count > 0)
            {
                UnityEngine.Debug.Log($"[TRACE] Scheduled Melody ({melodyEvents.Count}):");
                for (int i = 0; i < melodyEvents.Count; i++)
                {
                    var evt = melodyEvents[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(evt.midi, key);
                    float startSec = SecondsFromTicks(evt.startTick, timelineSpec);
                    float endSec = SecondsFromTicks(evt.startTick + evt.durationTicks, timelineSpec);
                    UnityEngine.Debug.Log($"[TRACE]   M{i}: startTick={evt.startTick} durationTicks={evt.durationTicks} midi={evt.midi}({noteName}) startSec={startSec:F3}s endSec={endSec:F3}s");
                }
            }
            
            // 3) Runtime events: Actual NoteOn/NoteOff (from audit entries if available)
            var allEvents = _currentAuditEntries.OrderBy(e => e.actualTimeFromStart).ToList();
            if (allEvents.Count > 0)
            {
                UnityEngine.Debug.Log($"[TRACE] Actual Events ({allEvents.Count}):");
                foreach (var entry in allEvents)
                {
                    string regionStr = entry.regionIdx.HasValue ? $"R{entry.regionIdx.Value}" : "-";
                    string melodyStr = entry.melodyIdx.HasValue ? $"M{entry.melodyIdx.Value}" : "-";
                    string voiceStr = entry.voice != "Unknown" ? entry.voice : "-";
                    string noteName = entry.noteName ?? $"{entry.midi}";
                    UnityEngine.Debug.Log($"[TRACE]   t={entry.actualTimeFromStart:F4}s {entry.eventType} {entry.kind} {regionStr}/{melodyStr} {voiceStr} {noteName}({entry.midi}) inst={entry.fmodInstanceId}");
                }
            }
            else
            {
                UnityEngine.Debug.Log($"[TRACE] Actual Events: (0) - enable PlaybackAudit for detailed event tracking");
            }
            
            // 4) End-of-playback report: Verify expected vs actual
            int expectedChordRegions = regions != null ? regions.Count : 0;
            var actualChordRegions = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Chord").Select(e => e.regionIdx).Distinct().ToList();
            int actualChordRegionCount = actualChordRegions.Count;
            
            int expectedMelodyEvents = melodyEvents != null ? melodyEvents.Count : 0;
            var actualMelodyIndices = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Melody").Select(e => e.melodyIdx).Distinct().ToList();
            int actualMelodyEventCount = actualMelodyIndices.Count;
            
            UnityEngine.Debug.Log($"[TRACE] Expected vs Actual: Chord Regions {actualChordRegionCount}/{expectedChordRegions}, Melody Events {actualMelodyEventCount}/{expectedMelodyEvents}");
            
            if (actualChordRegionCount < expectedChordRegions)
            {
                var missingRegions = Enumerable.Range(0, expectedChordRegions).Where(idx => !actualChordRegions.Contains(idx)).ToList();
                UnityEngine.Debug.LogWarning($"[TRACE] MISSING Chord Regions: {string.Join(", ", missingRegions.Select(r => $"R{r}"))}");
            }
            
            if (actualMelodyEventCount < expectedMelodyEvents)
            {
                var missingMelodies = Enumerable.Range(0, expectedMelodyEvents).Where(idx => !actualMelodyIndices.Contains(idx)).ToList();
                UnityEngine.Debug.LogWarning($"[TRACE] MISSING Melody Events: {string.Join(", ", missingMelodies.Select(m => $"M{m}"))}");
            }
        }
        
        /// <summary>
        /// Stops any active playback and cleans up keyboard display.
        /// </summary>
        private void StopPlaybackAndCleanup()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            if (synth != null)
            {
                synth.StopAll();
            }
            
            // Timeline v1: Clear active notes when playback is stopped
            ClearActiveNotes();
            
            // Clear VoicingViewer highlight when playback stops
            if (voicingViewer != null)
            {
                voicingViewer.SetHighlightedStep(-1);
            }
            if (melodyPianoRoll != null)
            {
                melodyPianoRoll.SetHighlightedStep(-1);
            }
        }
        
        /// <summary>
        /// Active Note Tracking for Piano Keyboard Display (Timeline v1)
        /// Reference-counted system to handle overlapping notes (e.g., melody matching chord tone)
        /// </summary>
        
        /// <summary>
        /// Adds a MIDI note to the active set (increments reference count).
        /// </summary>
        private void AddActiveNote(int midi)
        {
            if (!enableKeyboardTimelineTracking || pianoKeyboardDisplay == null)
                return;
            
            if (!_activeNoteCounts.ContainsKey(midi))
            {
                _activeNoteCounts[midi] = 0;
            }
            _activeNoteCounts[midi]++;
            RefreshKeyboard();
        }
        
        /// <summary>
        /// Removes a MIDI note from the active set (decrements reference count).
        /// </summary>
        private void RemoveActiveNote(int midi)
        {
            if (!enableKeyboardTimelineTracking || pianoKeyboardDisplay == null)
                return;
            
            if (_activeNoteCounts.ContainsKey(midi))
            {
                _activeNoteCounts[midi]--;
                if (_activeNoteCounts[midi] <= 0)
                {
                    _activeNoteCounts.Remove(midi);
                }
                RefreshKeyboard();
            }
        }
        
        /// <summary>
        /// Refreshes the piano keyboard display with the current set of active notes.
        /// </summary>
        private void RefreshKeyboard()
        {
            if (!enableKeyboardTimelineTracking || pianoKeyboardDisplay == null)
                return;
            
            if (_activeNoteCounts.Count == 0)
            {
                // No active notes - show default dimmed state
                pianoKeyboardDisplay.ShowDefault();
            }
            else
            {
                // Show all active notes (keys with count > 0)
                pianoKeyboardDisplay.SetActiveNotes(_activeNoteCounts.Keys);
            }
        }
        
        /// <summary>
        /// Clears all active notes and resets keyboard to default state.
        /// </summary>
        private void ClearActiveNotes()
        {
            _activeNoteCounts.Clear();
            if (pianoKeyboardDisplay != null)
            {
                pianoKeyboardDisplay.ShowDefault();
            }
        }
        
        /// <summary>
        /// Coroutine to remove a note after a delay (for melody note-off timing).
        /// Checks playback token to avoid stale removals from previous playback runs.
        /// </summary>
        private IEnumerator CoRemoveNoteAfterDelay(int midi, float delaySeconds, int token)
        {
            yield return new WaitForSeconds(delaySeconds);
            
            // Only remove if this is still the current playback token
            if (token == _keyboardPlaybackToken)
            {
                RemoveActiveNote(midi);
            }
        }
        
        /// <summary>
        /// Coroutine to remove harmony notes after a delay (for region end timing).
        /// Checks playback token to avoid stale removals from previous playback runs.
        /// </summary>
        private IEnumerator CoRemoveHarmonyNotesAfterDelay(List<int> midiNotes, float delaySeconds, int token)
        {
            yield return new WaitForSeconds(delaySeconds);
            
            // Only remove if this is still the current playback token
            if (token == _keyboardPlaybackToken)
            {
                foreach (int midi in midiNotes)
                {
                    RemoveActiveNote(midi);
                }
            }
        }
        
        /// <summary>
        /// Builds region-aligned lists of chord symbols, Roman numerals, diatonic status, and duration ticks
        /// for the VoicingViewer header rows.
        /// </summary>
        /// <param name="regions">List of chord regions</param>
        /// <param name="key">TheoryKey for chord symbol and Roman numeral formatting</param>
        /// <param name="absoluteChordSymbolsPerRegion">Output: Region-aligned list of absolute chord symbols</param>
        /// <param name="romanNumeralsPerRegion">Output: Region-aligned list of Roman numerals</param>
        /// <param name="isDiatonicPerRegion">Output: Region-aligned list of diatonic status flags</param>
        /// <param name="regionDurationTicks">Output: Region-aligned list of duration ticks</param>
        private void BuildChordHeaderData(
            IReadOnlyList<ChordRegion> regions,
            TheoryKey key,
            out List<string> absoluteChordSymbolsPerRegion,
            out List<string> romanNumeralsPerRegion,
            out List<bool> isDiatonicPerRegion,
            out List<int> regionDurationTicks)
        {
            absoluteChordSymbolsPerRegion = new List<string>();
            romanNumeralsPerRegion = new List<string>();
            isDiatonicPerRegion = new List<bool>();
            regionDurationTicks = new List<int>();
            
            if (regions == null || regions.Count == 0)
            {
                return;
            }
            
            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                var chordEvent = region.chordEvent;
                var recipe = chordEvent.Recipe;
                
                // Get root note name
                string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                    key,
                    recipe.Degree,
                    recipe.RootSemitoneOffset);
                
                // Get bass MIDI for slash chord notation (if inversion)
                int? bassMidi = null;
                if (recipe.Inversion != ChordInversion.Root)
                {
                    // Compute bass note from inversion
                    var chordMidi = TheoryChord.BuildChord(key, recipe, rootOctave);
                    if (chordMidi != null && chordMidi.Length > 0)
                    {
                        // Inversions rotate notes, so bass is the first (lowest) note
                        bassMidi = chordMidi[0];
                    }
                }
                
                // Build absolute chord symbol (with tensions if available from voicing)
                // For now, use base symbol without tensions (can be enhanced later)
                string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootNoteName, bassMidi);
                
                // Get Roman numeral
                string romanNumeral = TheoryChord.RecipeToRomanNumeral(key, recipe);
                
                // Analyze diatonic status
                var profile = TheoryChord.AnalyzeChordProfile(key, recipe);
                bool isDiatonic = profile.DiatonicStatus == ChordDiatonicStatus.Diatonic;
                
                // Get duration ticks
                int durationTicks = region.durationTicks;
                
                absoluteChordSymbolsPerRegion.Add(chordSymbol);
                romanNumeralsPerRegion.Add(romanNumeral);
                isDiatonicPerRegion.Add(isDiatonic);
                regionDurationTicks.Add(durationTicks);
            }
        }
        
        /// <summary>
        /// Builds a quarter-note-based SATB timeline from regions and voiced chords.
        /// Each quarter note becomes one time step in the timeline.
        /// </summary>
        private void BuildSatbTimeline(
            TimelineSpec timeline,
            IReadOnlyList<ChordRegion> regions,
            IReadOnlyList<int[]> voicesPerRegion, // VoicesMidi per region [B,T,A,S]
            out List<int?> bass,
            out List<int?> tenor,
            out List<int?> alto,
            out List<int?> soprano)
        {
            BuildSatbAndMelodyTimeline(timeline, regions, voicesPerRegion, null, out bass, out tenor, out alto, out soprano, out _, out _, out _);
        }
        
        /// <summary>
        /// Builds a quarter-note-based SATB timeline from regions and voiced chords for the simple Play path.
        /// Each quarter note becomes one time step in the timeline.
        /// </summary>
        private void BuildSatbTimelineForSimplePlay(
            TimelineSpec timeline,
            IReadOnlyList<ChordRegion> regions,
            IReadOnlyList<int[]> voicesPerRegion, // VoicesMidi per region [B,T,A,S]
            out List<int?> bass,
            out List<int?> tenor,
            out List<int?> alto,
            out List<int?> soprano,
            out List<bool> chordIsAttack)
        {
            bass = new List<int?>();
            tenor = new List<int?>();
            alto = new List<int?>();
            soprano = new List<int?>();
            chordIsAttack = new List<bool>();
            
            if (regions == null || regions.Count == 0 || voicesPerRegion == null || voicesPerRegion.Count == 0)
            {
                // All out parameters are already initialized to empty lists, safe to return
                return;
            }
            
            int ticksPerQuarter = timeline != null ? timeline.ticksPerQuarter : 4;
            
            // Compute maxTick as max of region.startTick + region.durationTicks
            int maxTick = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                int regionEndTick = regions[i].startTick + regions[i].durationTicks;
                if (regionEndTick > maxTick)
                {
                    maxTick = regionEndTick;
                }
            }
            
            // Calculate total quarters (ceiling division)
            int totalQuarters = Mathf.CeilToInt(maxTick / (float)ticksPerQuarter);
            
            // Initialize all lists with null entries
            bass = new List<int?>(new int?[totalQuarters]);
            tenor = new List<int?>(new int?[totalQuarters]);
            alto = new List<int?>(new int?[totalQuarters]);
            soprano = new List<int?>(new int?[totalQuarters]);
            chordIsAttack = new List<bool>(new bool[totalQuarters]);
            
            // Fill SATB voices from regions and mark chord attacks
            for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
            {
                var region = regions[regionIdx];
                int regionStartTick = region.startTick;
                int regionEndTick = regionStartTick + region.durationTicks;
                
                // Compute start and end quarters for this region
                int startQ = regionStartTick / ticksPerQuarter;
                int endQ = (regionEndTick + ticksPerQuarter - 1) / ticksPerQuarter; // ceil
                
                // Clamp to valid range
                startQ = Mathf.Clamp(startQ, 0, totalQuarters - 1);
                endQ = Mathf.Clamp(endQ, startQ + 1, totalQuarters);
                
                // Get voices for this region
                if (regionIdx < voicesPerRegion.Count && voicesPerRegion[regionIdx] != null && voicesPerRegion[regionIdx].Length >= 4)
                {
                    var voices = voicesPerRegion[regionIdx];
                    
                    // Fill all quarters in [startQ, endQ) with this region's voices
                    for (int q = startQ; q < endQ; q++)
                    {
                        bass[q] = voices[0];   // Bass
                        tenor[q] = voices[1];  // Tenor
                        alto[q] = voices[2];   // Alto
                        soprano[q] = voices[3]; // Soprano
                    }
                    
                    // Mark attack at the region's first quarter
                    chordIsAttack[startQ] = true;
                }
            }
        }
        
        /// <summary>
        /// Builds a quarter-note-based SATB + Melody timeline from regions, voiced chords, and melody events.
        /// Each quarter note becomes one time step in the timeline.
        /// </summary>
        private void BuildSatbAndMelodyTimeline(
            TimelineSpec timeline,
            IReadOnlyList<ChordRegion> regions,
            IReadOnlyList<int[]> voicesPerRegion, // VoicesMidi per region [B,T,A,S]
            IReadOnlyList<Sonoria.MusicTheory.Timeline.MelodyEvent> melodyEvents,
            out List<int?> bass,
            out List<int?> tenor,
            out List<int?> alto,
            out List<int?> soprano,
            out List<int?> melody,
            out List<bool> melodyIsAttack,
            out List<bool> chordIsAttack)
        {
            bass = new List<int?>();
            tenor = new List<int?>();
            alto = new List<int?>();
            soprano = new List<int?>();
            melody = new List<int?>();
            melodyIsAttack = new List<bool>();
            chordIsAttack = new List<bool>();
            
            if (regions == null || regions.Count == 0 || voicesPerRegion == null || voicesPerRegion.Count == 0)
            {
                // All out parameters are already initialized to empty lists, safe to return
                return;
            }
            
            int ticksPerQuarter = timeline != null ? timeline.ticksPerQuarter : 4;
            
            // Compute maxTick as max of region.startTick + region.durationTicks
            int maxTick = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                int regionEndTick = regions[i].startTick + regions[i].durationTicks;
                if (regionEndTick > maxTick)
                {
                    maxTick = regionEndTick;
                }
            }
            
            // Also check melody events for max tick
            if (melodyEvents != null && melodyEvents.Count > 0)
            {
                for (int i = 0; i < melodyEvents.Count; i++)
                {
                    int melodyEndTick = melodyEvents[i].startTick + melodyEvents[i].durationTicks;
                    if (melodyEndTick > maxTick)
                    {
                        maxTick = melodyEndTick;
                    }
                }
            }
            
            // Calculate total quarters (ceiling division)
            int totalQuarters = Mathf.CeilToInt(maxTick / (float)ticksPerQuarter);
            
            // Initialize all lists with null entries
            bass = new List<int?>(new int?[totalQuarters]);
            tenor = new List<int?>(new int?[totalQuarters]);
            alto = new List<int?>(new int?[totalQuarters]);
            soprano = new List<int?>(new int?[totalQuarters]);
            melody = new List<int?>(new int?[totalQuarters]);
            melodyIsAttack = new List<bool>(new bool[totalQuarters]);
            chordIsAttack = new List<bool>(new bool[totalQuarters]);
            
            // Fill SATB voices from regions and mark chord attacks
            for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
            {
                var region = regions[regionIdx];
                int regionStartTick = region.startTick;
                int regionEndTick = regionStartTick + region.durationTicks;
                
                // Compute start and end quarters for this region
                int startQ = regionStartTick / ticksPerQuarter;
                int endQ = (regionEndTick + ticksPerQuarter - 1) / ticksPerQuarter; // ceil
                
                // Clamp to valid range
                startQ = Mathf.Clamp(startQ, 0, totalQuarters - 1);
                endQ = Mathf.Clamp(endQ, startQ + 1, totalQuarters);
                
                // Get voices for this region
                if (regionIdx < voicesPerRegion.Count && voicesPerRegion[regionIdx] != null && voicesPerRegion[regionIdx].Length >= 4)
                {
                    var voices = voicesPerRegion[regionIdx];
                    
                    // Fill all quarters in [startQ, endQ) with this region's voices
                    for (int q = startQ; q < endQ; q++)
                    {
                        bass[q] = voices[0];   // Bass
                        tenor[q] = voices[1];  // Tenor
                        alto[q] = voices[2];   // Alto
                        soprano[q] = voices[3]; // Soprano
                    }
                    
                    // Mark attack at the region's first quarter
                    chordIsAttack[startQ] = true;
                }
            }
            
            // Fill melody timeline from melody events
            if (melodyEvents != null && melodyEvents.Count > 0)
            {
                for (int i = 0; i < melodyEvents.Count; i++)
                {
                    var me = melodyEvents[i];
                    
                    // Compute start and end quarters (ceiling division for end)
                    int startQ = me.startTick / ticksPerQuarter;
                    int endQ = (me.startTick + me.durationTicks + ticksPerQuarter - 1) / ticksPerQuarter; // ceil
                    
                    // Clamp to valid range
                    startQ = Mathf.Clamp(startQ, 0, totalQuarters - 1);
                    endQ = Mathf.Clamp(endQ, startQ + 1, totalQuarters);
                    
                    // Fill all quarters in [startQ, endQ) with this melody note's MIDI
                    for (int q = startQ; q < endQ; q++)
                    {
                        melody[q] = me.midi;
                        // Note: If multiple melody events overlap, last write wins (monophonic assumption)
                    }
                    
                    // Mark the attack at the start quarter
                    melodyIsAttack[startQ] = true;
                }
            }
        }
        
        private void PlayChord(IReadOnlyList<int> midiNotes, float duration, int regionIdx = -1, float targetTimeFromStart = -1f)
        {
            if (midiNotes == null || midiNotes.Count == 0)
                return;

            TheoryKey key = GetKeyFromDropdowns();
            
            // CHORD_PLAN: Log plan before any modifications
            string regionLabel = regionIdx >= 0 && _lastRegions != null && regionIdx < _lastRegions.Count 
                ? (_lastRegions[regionIdx].debugLabel ?? "?") 
                : "?";
            
            // Get melody info at region start if available
            string melodyInfo = "none";
            int? melodyMidiAtStart = null;
            if (regionIdx >= 0 && _lastRegions != null && regionIdx < _lastRegions.Count)
            {
                var region = _lastRegions[regionIdx];
                melodyMidiAtStart = region.chordEvent.MelodyMidi;
                if (melodyMidiAtStart.HasValue)
                {
                    string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidiAtStart.Value, key);
                    melodyInfo = $"midi{melodyMidiAtStart.Value}({melodyName})";
                }
            }
            
            // Build planned SATB voices list (exactly 4)
            var plannedVoices = new List<(string voiceName, int midi, string pitchName, string sourceTag)>();
            string[] satbVoiceNames = { "B", "T", "A", "S" };
            int voiceCount = midiNotes.Count < 4 ? midiNotes.Count : 4;
            for (int i = 0; i < voiceCount; i++)
            {
                string voiceName = satbVoiceNames[i]; // Always exactly B, T, A, S
                string pitchName = TheoryPitch.GetPitchNameFromMidi(midiNotes[i], key);
                plannedVoices.Add((voiceName, midiNotes[i], pitchName, "SATBVoicer"));
            }
            
            // Check if any voice MIDI equals melody MIDI
            bool melodyMatchesVoice = false;
            if (melodyMidiAtStart.HasValue)
            {
                melodyMatchesVoice = midiNotes.Contains(melodyMidiAtStart.Value);
            }
            
            LogPlaybackInfo("CHORD_PLAN", 
                $"run={_currentPlaybackRunId} r={regionIdx} label={regionLabel} expected=4 flags{{melodyAsSoprano=false}} melodyAtStart={melodyInfo} melodyMatchesVoice={melodyMatchesVoice} " +
                $"voices=[{string.Join(", ", plannedVoices.Select(v => $"{v.voiceName}:{v.midi}({v.pitchName}) src={v.sourceTag}"))}]");

            // STRICT VALIDATION: SATB must be exactly 4 logical voices
            if (midiNotes.Count != 4)
            {
                LogPlaybackError("CHORD_PLAN_ERROR", 
                    $"run={_currentPlaybackRunId} r={regionIdx} SATB voice count={midiNotes.Count} != 4. MIDI=[{string.Join(", ", midiNotes)}]. NOT SCHEDULING CHORD EVENTS FOR THIS REGION.");
                return; // Fail fast
            }
            
            // Build render notes list: Start with 4 SATB voices
            var renderNotes = new List<(int midi, string voiceTag, string role, string parentVoice, string sourceTag)>();

            // Timeline v1 "Pianist" model:
            // When a Melody timeline exists, the melody is the only real soprano.
            // We derive a melody anchor at each chord boundary and voice B/T/A underneath it.
            // The SATB structure always contains 4 voices internally (for planning/voice-leading),
            // but in melody-as-soprano mode, we only play B/T/A to the synth (soprano comes from melody timeline).
            bool melodyAsSopranoMode = (_lastTimelineMelodyEvents != null && _lastTimelineMelodyEvents.Count > 0);
            int voicesToRender = melodyAsSopranoMode ? 3 : 4; // Only render B/T/A when melody-as-soprano mode, otherwise all 4
            
            for (int i = 0; i < voicesToRender; i++)
            {
                string voiceTag = satbVoiceNames[i];
                renderNotes.Add((midiNotes[i], voiceTag, "Chord", voiceTag, "SATBVoicer"));
            }
            
            // Add bass doubling as render-layer embellishment (if enabled)
            if (emphasizeBassWithLowOctave)
            {
                int bassMidi = midiNotes[0]; // First note is always bass
                int doubledMidi = bassMidi - 12;
                
                // Clamp to valid MIDI range (0-127)
                if (doubledMidi < 0) doubledMidi = 0;
                if (doubledMidi > 127) doubledMidi = 127;
                
                // Add doubling note as embellishment (NOT a 5th voice)
                if (!midiNotes.Contains(doubledMidi))
                {
                    renderNotes.Add((doubledMidi, "B_dbl", "ChordDoubling", "B", "EmphasizeBassLowOctave"));
                }
            }
            
            // CHORD_RENDER: Log render notes actually scheduled
            var renderNoteStrings = renderNotes.Select(rn => 
                $"{rn.voiceTag}:{rn.midi}({TheoryPitch.GetPitchNameFromMidi(rn.midi, key)}) src={rn.sourceTag}"
            ).ToList();
            LogPlaybackVerbose("CHORD_RENDER",
                $"run={_currentPlaybackRunId} r={regionIdx} " +
                $"render=[{string.Join(", ", renderNoteStrings)}]");

            // Debug logging: log what audio actually plays (when tendency debug is enabled)
            // GATED: Only log when tendency debug is enabled (separate from PlaybackAudit)
            if (TheoryVoicing.GetTendencyDebug())
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[Audio Debug] Playing chord: ");
                foreach (var rn in renderNotes)
                {
                    string name = TheoryPitch.GetPitchNameFromMidi(rn.midi, key);
                    sb.Append($"{rn.voiceTag}={name}({rn.midi}) ");
                }
                Debug.Log(sb.ToString());
            }

            float actualTime = Time.time;
            float actualTimeFromStart = _currentPlaybackStartTime > 0f ? (actualTime - _currentPlaybackStartTime) : 0f;
            
            // CENTRALIZED NOTE HANDLE TRACKING: Register all chord notes before playing
            if (regionIdx >= 0)
            {
                if (!_activeHandlesByRegion.ContainsKey(regionIdx))
                {
                    _activeHandlesByRegion[regionIdx] = new List<string>();
                }
                if (!_activeEmbellishmentsByRegion.ContainsKey(regionIdx))
                {
                    _activeEmbellishmentsByRegion[regionIdx] = new List<string>();
                }
            }
            
            // Schedule render notes (SATB voices + embellishments)
            for (int i = 0; i < renderNotes.Count; i++)
            {
                var renderNote = renderNotes[i];
                int midiNote = renderNote.midi;
                string voiceTag = renderNote.voiceTag;
                string role = renderNote.role; // "Chord" for SATB voices, "ChordDoubling" for embellishments
                string parentVoice = renderNote.parentVoice;
                string sourceTag = renderNote.sourceTag;
                
                // Determine full role string for tracking
                string fullRole = role == "Chord" ? $"Chord{voiceTag}" : $"ChordDoubling{voiceTag}";
                
                // CHORD_VOICE_INVALID: Error if SATB voice is not B,T,A,S (should never happen after validation)
                if (role == "Chord" && voiceTag != "B" && voiceTag != "T" && voiceTag != "A" && voiceTag != "S")
                {
                    string stackTrace = System.Environment.StackTrace;
                    string[] stackLines = stackTrace.Split('\n');
                    string shortStackTrace = string.Join(" | ", stackLines.Take(3));
                    
                    LogPlaybackError("CHORD_VOICE_INVALID",
                        $"run={_currentPlaybackRunId} r={regionIdx} voice={voiceTag} midi={midiNote} " +
                        $"sourceTag={sourceTag} stackTrace={shortStackTrace}");
                    continue; // Skip invalid voice
                }
                
                // PlaybackAudit: Track pending note-on BEFORE FMOD call (callback will create actual audit entry)
                if (enablePlaybackAudit)
                {
                    _pendingNoteOns[midiNote] = (
                        kind: role == "Chord" ? "Chord" : "ChordEmbellishment",
                        regionIdx: regionIdx >= 0 ? (int?)regionIdx : null,
                        melodyIdx: null,
                        voice: voiceTag,
                        targetTimeFromStart: targetTimeFromStart >= 0f ? targetTimeFromStart : actualTimeFromStart
                    );
                }
                
                // Use PlayOnceWithInstance to get the instance handle
                var instance = synth.PlayOnceWithInstance(midiNote, harmonyVelocity, duration);
                if (instance.isValid())
                {
                    string instanceId = instance.handle.ToString();
                    
                    // Count active chord handles before registration
                    int activeChordBefore = _activeHandles.Count(kv => kv.Value.role.StartsWith("Chord"));
                    
                    // TRACE2: Create event trace for this chord note
                    int eventId = ++_eventIdCounter;
                    // Note: regions not available in PlayChord scope - use 0 for tick values
                    int regionStartTick = 0; // Not available in this scope
                    int regionDurationTicks = 0; // Not available in this scope
                    float scheduledSec = targetTimeFromStart >= 0f ? targetTimeFromStart : actualTimeFromStart;
                    
                    var eventTrace = new EventTrace
                    {
                        eventId = eventId,
                        playbackRunId = _currentPlaybackRunId,
                        type = role == "Chord" ? "Chord" : "ChordEmbellishment",
                        regionIndex = regionIdx >= 0 ? (int?)regionIdx : null,
                        voice = voiceTag,
                        midi = midiNote,
                        scheduledTick = regionStartTick,
                        scheduledSec = scheduledSec,
                        durationTicks = regionDurationTicks,
                        durationSec = duration,
                        instanceId = instanceId,
                        sourceTag = sourceTag
                    };
                    _eventTraces[eventId] = eventTrace;
                    _instanceIdToEventId[instanceId] = eventId;
                    
                    // CENTRALIZED TRACKING: Register handle
                    var handle = new NoteHandle
                    {
                        instanceId = instanceId,
                        midi = midiNote,
                        role = fullRole,
                        regionIdx = regionIdx >= 0 ? (int?)regionIdx : null,
                        melodyIdx = null,
                        scheduledOnTime = scheduledSec,
                        instance = instance,
                        eventId = eventId,
                        sourceTag = sourceTag
                    };
                    
                    // Track SATB voices separately from embellishments
                    if (role == "Chord")
                    {
                        // SATB voice: track in main activeHandles and by region
                        _activeHandles[instanceId] = handle;
                        if (regionIdx >= 0)
                        {
                            _activeHandlesByRegion[regionIdx].Add(instanceId);
                        }
                    }
                    else
                    {
                        // Embellishment: track in separate collection
                        _activeChordEmbellishments[instanceId] = handle;
                        if (regionIdx >= 0)
                        {
                            _activeEmbellishmentsByRegion[regionIdx].Add(instanceId);
                        }
                    }
                    
                    // Count active SATB chord voices after registration (exclude embellishments)
                    int activeChordAfter = _activeHandles.Count(kv => 
                        kv.Value.role.StartsWith("Chord") && 
                        !kv.Value.role.Contains("Doubling") &&
                        (kv.Value.role == "ChordB" || kv.Value.role == "ChordT" || kv.Value.role == "ChordA" || kv.Value.role == "ChordS"));
                    
                    // TRACE2: Runtime registration log with event trace info
                    float delta = actualTimeFromStart - scheduledSec;
                    if (EnablePlaybackTrace2)
                    {
                        UnityEngine.Debug.Log(
                            $"[TRACE2] {(role == "Chord" ? "CHORD_NOTE_ON" : "CHORD_EMBELLISHMENT_ON")} {eventTrace.ToCompactLog()} " +
                            $"actualTime={actualTimeFromStart:F4} delta={delta:F4} " +
                            $"activeChordBefore={activeChordBefore} activeChordAfter={activeChordAfter}"
                        );
                    }
                    
                    // CHORD_NOTE_ON: Log SATB voices; CHORD_EMBELLISHMENT_ON: Log embellishments
                    if (role == "Chord")
                    {
                        LogPlaybackVerbose("CHORD_NOTE_ON",
                            $"RUN={_currentPlaybackRunId} region={regionIdx} voice={voiceTag} midi={midiNote} inst={instanceId} " +
                            $"activeChordAfter={activeChordAfter}");
                        _chordNotesOn++;
                    }
                    else
                    {
                        LogPlaybackVerbose("CHORD_EMBELLISHMENT_ON",
                            $"RUN={_currentPlaybackRunId} region={regionIdx} voiceTag={voiceTag} parentVoice={parentVoice} midi={midiNote} inst={instanceId} " +
                            $"sourceTag={sourceTag}");
                    }
                    
                    // PlaybackTrace: Log NoteOn
                    if (enablePlaybackTrace)
                    {
                        string regionStr = regionIdx >= 0 ? $"r={regionIdx}" : "r=-";
                        UnityEngine.Debug.Log($"[TRACE] ON t={actualTimeFromStart:F3}s sched={handle.scheduledOnTime:F3}s {regionStr} role={role} midi={midiNote} inst={instanceId}");
                    }
                }
            }
        }

        /// <summary>
        /// Maps dropdown index to ScaleMode enum.
        /// UI restriction: Only Major (Ionian) and Minor (Aeolian) are available.
        /// </summary>
        private Sonoria.MusicTheory.ScaleMode GetModeFromDropdown(int index)
        {
            return index switch
            {
                0 => Sonoria.MusicTheory.ScaleMode.Ionian,  // Major
                1 => Sonoria.MusicTheory.ScaleMode.Aeolian,  // Minor
                _ => Sonoria.MusicTheory.ScaleMode.Ionian // Fallback to Major
            };
        }

        /// <summary>
        /// Converts ScaleMode enum to user-friendly display name.
        /// Returns "Major" for Ionian, "Minor" for Aeolian, and enum name for others.
        /// </summary>
        private string GetModeDisplayName(Sonoria.MusicTheory.ScaleMode mode)
        {
            return mode switch
            {
                Sonoria.MusicTheory.ScaleMode.Ionian => "Major",
                Sonoria.MusicTheory.ScaleMode.Aeolian => "Minor",
                _ => mode.ToString() // Fallback to enum name for other modes (shouldn't occur in UI)
            };
        }

        /// <summary>
        /// Extracts the root note name from a chord symbol token.
        /// Examples:
        ///   "Bbmaj7" → "Bb"
        ///   "C/E"    → "C"
        ///   "F#m7"   → "F#"
        /// Returns null if the token does not look like a chord symbol.
        /// </summary>
        private static string ExtractRootNoteNameFromChordSymbol(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            token = token.Trim();

            // Handle slash chords: take the part before the slash
            int slashIndex = token.IndexOf('/');
            if (slashIndex >= 0)
            {
                token = token.Substring(0, slashIndex);
            }

            if (token.Length == 0)
                return null;

            // Check if this looks like a Roman numeral (starts with b/# followed by I/V/X, or just I/V/X)
            // This prevents "bVII" from being parsed as "b" (B note)
            if (token.Length > 1)
            {
                char first = char.ToUpperInvariant(token[0]);
                char second = char.ToUpperInvariant(token[1]);
                
                // Check for Roman numeral patterns: bI, bV, bX, #I, #V, #X, or just I, V, X
                if ((first == 'B' || first == '#' || first == '♯' || first == '♭') && 
                    (second == 'I' || second == 'V' || second == 'X'))
                {
                    return null; // This is a Roman numeral, not a chord symbol
                }
                if (first == 'I' || first == 'V' || first == 'X')
                {
                    return null; // This is a Roman numeral
                }
            }
            else if (token.Length == 1)
            {
                char first = char.ToUpperInvariant(token[0]);
                if (first == 'I' || first == 'V' || first == 'X')
                {
                    return null; // Single character Roman numeral
                }
            }

            // First character must be A–G
            char letter = char.ToUpperInvariant(token[0]);
            if (letter < 'A' || letter > 'G')
                return null;

            int index = 1;

            // Optional accidental
            if (index < token.Length)
            {
                char acc = token[index];
                if (acc == '#' || acc == 'b' || acc == '♯' || acc == '♭')
                {
                    index++;
                }
            }

            return token.Substring(0, index);
        }

        /// <summary>
        /// Renders the chord grid with visual chord representations.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="originalTokens">Original Roman numeral strings (what the user typed)</param>
        /// <param name="originalRecipes">Original chord recipes (before adjustment, for analysis)</param>
        /// <param name="chords">MIDI note arrays for each chord</param>
        /// <param name="adjustedRecipes">Adjusted chord recipes (for building chord symbols)</param>
        private void RenderChordGrid(
            TheoryKey key,
            IReadOnlyList<string> originalTokens,
            IReadOnlyList<ChordRecipe> originalRecipes,
            IReadOnlyList<int[]> chords,
            IReadOnlyList<ChordRecipe> adjustedRecipes,
            IReadOnlyList<VoicedChord> voicedChords = null,
            IReadOnlyList<ChordEvent> chordEvents = null,
            string voicingMode = "Play")
        {
            // Clear existing children
            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Clear stored analysis info when rebuilding grid
            _regionAnalysisInfoByIndex.Clear();
            // Clear chord column view references when rebuilding grid
            _chordColumnViewsByRegion.Clear();

            // Validate required references
            if (chordGridContainer == null || chordColumnPrefab == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ChordLab] Cannot render chord grid: missing container or prefab reference");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Rendering {chords.Count} chord columns");

            // Create a column for each chord
            for (int i = 0; i < chords.Count && i < adjustedRecipes.Count && i < originalRecipes.Count && i < originalTokens.Count; i++)
            {
                var chord = chords[i];
                var adjustedRecipe = adjustedRecipes[i];
                var originalRecipe = originalRecipes[i];
                var originalToken = originalTokens[i];

                if (chord == null || chord.Length == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ChordLab] Skipping empty chord at index {i}");
                    continue;
                }

                // Get chord function profile (this is the single source of truth for diatonic status)
                var profile = TheoryChord.AnalyzeChordProfile(key, originalRecipe);
                string analysisInfo = TheoryChord.BuildNonDiatonicInfo(profile);
                
                // Store analysis info for console display (reuse existing purple-column label)
                if (!string.IsNullOrEmpty(analysisInfo))
                {
                    _regionAnalysisInfoByIndex[i] = analysisInfo;
                }
                else
                {
                    // Remove entry if chord becomes diatonic (clear any stale data)
                    _regionAnalysisInfoByIndex.Remove(i);
                }
                
                // Use profile.DiatonicStatus for UI coloring (includes 7th quality checks)
                var status = profile.DiatonicStatus;

                // Optional debug logging for chord profile
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ChordLab] Profile for {originalToken}: " +
                        $"degree={profile.Degree}, pc={profile.RootPitchClass}, " +
                        $"diatonic={profile.DiatonicStatus}, " +
                        $"function={profile.FunctionTag}, " +
                        $"parallelModes={profile.ParallelModeMembership}, " +
                        $"secondaryTarget={profile.SecondaryTargetDegree}, " +
                        $"borrow={profile.BorrowSummary}, " +
                        $"info='{analysisInfo}'");
                }

                // Sort MIDI notes ascending (lowest to highest)
                var sortedChord = new List<int>(chord);
                sortedChord.Sort();

                // Try to preserve the root name exactly as typed when the input was a chord symbol.
                // If that fails (Roman numerals, or malformed token), fall back to key-aware spelling.
                string rootNoteName = ExtractRootNoteNameFromChordSymbol(originalToken);

                if (rootNoteName == null)
                {
                    // Fall back to key-aware spelling for Roman numerals
                // This ensures proper spelling (e.g., #vi in C Aeolian → A, not Bbb)
                    rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                    key,
                    adjustedRecipe.Degree,
                    adjustedRecipe.RootSemitoneOffset);
                }

                // Use canonical triad spelling lookup table for chord tones (root, 3rd, 5th)
                // This ensures consistent enharmonic spellings (e.g., bVII shows Bb-D-F, not A#-D-F)
                
                // Compute root pitch class from recipe (accounting for RootSemitoneOffset)
                int rootPc = TheoryScale.GetDegreePitchClass(key, originalRecipe.Degree);
                if (rootPc < 0)
                {
                    rootPc = 0; // Fallback to C
                }
                rootPc = (rootPc + originalRecipe.RootSemitoneOffset + 12) % 12;
                if (rootPc < 0)
                    rootPc += 12;
                
                // Get canonical triad spelling using key's accidental preference for enharmonic disambiguation
                string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, originalRecipe.Quality, key, originalRecipe.RootSemitoneOffset);
                
                // Build chord MIDI notes to match against
                var originalMidiNotes = TheoryChord.BuildChord(key, originalRecipe, rootOctave);
                var noteNames = new List<string>(sortedChord.Count);
                
                // Compute expected pitch classes for triad tones
                int thirdPc = -1;
                int fifthPc = -1;
                
                if (triadNames != null && triadNames.Length >= 3)
                {
                    switch (originalRecipe.Quality)
                    {
                        case ChordQuality.Major:
                            thirdPc = (rootPc + 4) % 12;  // Major third: +4 semitones
                            fifthPc = (rootPc + 7) % 12;  // Perfect fifth: +7 semitones
                            break;
                        case ChordQuality.Minor:
                            thirdPc = (rootPc + 3) % 12;  // Minor third: +3 semitones
                            fifthPc = (rootPc + 7) % 12;  // Perfect fifth: +7 semitones
                            break;
                        case ChordQuality.Diminished:
                            thirdPc = (rootPc + 3) % 12;  // Minor third: +3 semitones
                            fifthPc = (rootPc + 6) % 12;  // Diminished fifth: +6 semitones
                            break;
                        case ChordQuality.Augmented:
                            thirdPc = (rootPc + 4) % 12;  // Major third: +4 semitones
                            fifthPc = (rootPc + 8) % 12;  // Augmented fifth: +8 semitones
                            break;
                    }
                }
                
                // For each sorted MIDI note (highest to lowest), assign canonical name
                for (int sortedIdx = sortedChord.Count - 1; sortedIdx >= 0; sortedIdx--)
                {
                    int sortedMidi = sortedChord[sortedIdx];
                    int sortedPc = sortedMidi % 12;
                    
                    string noteName = null;
                    
                    // Check if this chord has a 7th extension and try canonical 7th chord spelling first
                    if (originalRecipe.Extension == ChordExtension.Seventh && 
                        originalRecipe.SeventhQuality != SeventhQuality.None)
                    {
                        // Get canonical 7th chord spelling (root, 3rd, 5th, 7th) using key's accidental preference
                        string[] seventhChordNames = TheorySpelling.GetSeventhChordSpelling(
                            rootPc, 
                            originalRecipe.Quality, 
                            originalRecipe.SeventhQuality, 
                            key,
                            originalRecipe.RootSemitoneOffset);
                        
                        if (seventhChordNames != null && seventhChordNames.Length >= 4)
                        {
                            // Calculate 7th pitch class based on seventh quality
                            int seventhPc = -1;
                            switch (originalRecipe.SeventhQuality)
                            {
                                case SeventhQuality.Major7:
                                    seventhPc = (rootPc + 11) % 12;  // Major 7th: +11 semitones
                                    break;
                                case SeventhQuality.Minor7:
                                case SeventhQuality.Dominant7:
                                case SeventhQuality.HalfDiminished7:
                                    seventhPc = (rootPc + 10) % 12;  // Minor 7th: +10 semitones
                                    break;
                                case SeventhQuality.Diminished7:
                                    seventhPc = (rootPc + 9) % 12;   // Diminished 7th: +9 semitones
                                    break;
                            }
                            
                            // Match to chord tones using canonical 7th chord spelling
                            if (sortedPc == rootPc)
                            {
                                noteName = seventhChordNames[0];  // Root
                            }
                            else if (thirdPc >= 0 && sortedPc == thirdPc)
                            {
                                noteName = seventhChordNames[1];  // Third
                            }
                            else if (fifthPc >= 0 && sortedPc == fifthPc)
                            {
                                noteName = seventhChordNames[2];  // Fifth
                            }
                            else if (seventhPc >= 0 && sortedPc == seventhPc)
                            {
                                noteName = seventhChordNames[3];  // Seventh
                            }
                        }
                    }
                    
                    // If no 7th chord spelling matched, try triad tones using canonical spelling
                    if (noteName == null && triadNames != null && triadNames.Length >= 3)
                    {
                        if (sortedPc == rootPc)
                        {
                            noteName = triadNames[0];  // Root
                        }
                        else if (thirdPc >= 0 && sortedPc == thirdPc)
                        {
                            noteName = triadNames[1];  // Third
                        }
                        else if (fifthPc >= 0 && sortedPc == fifthPc)
                        {
                            noteName = triadNames[2];  // Fifth
                        }
                    }
                    
                    // For non-chord tones (extensions, suspensions, etc.) or when canonical spelling unavailable,
                    // use key-based spelling as fallback
                    if (noteName == null)
                    {
                        noteName = TheoryPitch.GetPitchNameFromMidi(sortedMidi, key);
                    }
                    
                    noteNames.Add(noteName);
                }
                // noteNames is now highest to lowest (top to bottom for display)

                // Find the lowest MIDI note (bass note) for slash chord notation
                int bassMidi = chord.Min();

                // Determine the exact MIDI voicing to analyze (must match UI display)
                // Timeline v1: ONLY SATB voicing is analyzed for chord symbols. Timeline melody events are
                // played independently and are NOT included in chord symbol tension detection.
                // This ensures chord symbols reflect ONLY the harmonic voicing, not incidental melody notes.
                int[] analyzedMidi = null;
                
                // Priority: Use voiced chord if available (exact voicing shown in UI)
                if (voicedChords != null && i < voicedChords.Count)
                {
                    var voiced = voicedChords[i];
                    if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                    {
                        // Use exact SATB voicing [B,T,A,S] - Timeline melody events are EXCLUDED
                        analyzedMidi = new int[4];
                        analyzedMidi[0] = voiced.VoicesMidi[0]; // Bass
                        analyzedMidi[1] = voiced.VoicesMidi[1]; // Tenor
                        analyzedMidi[2] = voiced.VoicesMidi[2]; // Alto
                        analyzedMidi[3] = voiced.VoicesMidi[3]; // Soprano
                        bassMidi = analyzedMidi[0];
                    }
                    else if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                    {
                        analyzedMidi = voiced.VoicesMidi;
                        bassMidi = analyzedMidi[0];
                    }
                }
                
                // Fallback: Use basic chord array if no voiced chord available
                if (analyzedMidi == null)
                {
                    analyzedMidi = chord.ToArray();
                    bassMidi = analyzedMidi.Min();
                }

                // CRITICAL: Detect tensions from the exact realized SATB voicing (returns struct only, no label mutation)
                // Timeline v1: Timeline melody events are NOT included - only SATB voices are analyzed.
                // NOTE: This is ANALYSIS_ONLY for UI display. Play mode satisfaction comes from TheoryVoicing.VoiceLeadProgression.
                // This detection does NOT drive Play mode satisfaction results - it's only for chord symbol display.
                
                // DIAGNOSTIC: Log pitch classes used for symbol generation (verify melody is excluded)
                var pcsUsedForSymbol = new HashSet<int>();
                foreach (int midi in analyzedMidi)
                {
                    int pc = ((midi % 12) + 12) % 12;
                    pcsUsedForSymbol.Add(pc);
                }
                string symbolPcsStr = string.Join(",", pcsUsedForSymbol.OrderBy(pc => pc).Select(pc => $"{pc}"));
                if (enableMelodyTimelineDebug)
                    UnityEngine.Debug.Log($"[SYMBOL_PCS] regionIdx={i} pcsUsedForSymbol=[{symbolPcsStr}] (SATB voices only, melody excluded)");
                
                // GUARD: In Play mode, check if this is a duplicate execution and early-return from analysis-only path
                if (voicingMode == "Play")
                {
                    // Check if TheoryVoicing has already executed tension detection for this step
                    // We use reflection to check the private tracking dictionary (or we could make it public)
                    // For now, we'll log a warning and ensure this path is clearly marked as ANALYSIS_ONLY
                    if (TheoryVoicing.GetDebugTensionDetect())
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[GUARD_WARNING] ChordLabController ANALYSIS_ONLY tension detection for step={i} mode={voicingMode}. " +
                            $"This does NOT affect Play mode satisfaction. Play mode satisfaction comes from TheoryVoicing.VoiceLeadProgression. " +
                            $"analyzedMidi=[{string.Join(",", analyzedMidi)}]. " +
                            $"If you see [TENSION_DETECT] step={i} mode=Play token='...' from TheoryVoicing, that is the SINGLE_SOURCE_OF_TRUTH.");
                    }
                }
                
                var detectedTensions = ChordTensionDetector.DetectTensions(adjustedRecipe, analyzedMidi, key);
                var tensions = detectedTensions.Tensions;
                
                // Debug logging for tension detection
                if (TheoryVoicing.GetDebugTensionDetect())
                {
                    // Get root PC (use different name to avoid conflict with earlier rootPc)
                    int debugRootPc = TheoryScale.GetDegreePitchClass(key, adjustedRecipe.Degree);
                    if (debugRootPc < 0) debugRootPc = 0;
                    debugRootPc = (debugRootPc + adjustedRecipe.RootSemitoneOffset + 12) % 12;
                    if (debugRootPc < 0) debugRootPc += 12;
                    
                    // Get bass PC
                    int bassPc = ((bassMidi % 12) + 12) % 12;
                    
                    // Get soprano info from analyzed MIDI
                    string sopranoInfo = "none";
                    int sopranoPc = -1;
                    if (analyzedMidi != null && analyzedMidi.Length > 0)
                    {
                        int sopranoMidi = analyzedMidi[analyzedMidi.Length - 1]; // Last is highest (or soprano in SATB)
                        sopranoPc = ((sopranoMidi % 12) + 12) % 12;
                        string sopranoName = TheoryPitch.GetPitchNameFromMidi(sopranoMidi, key);
                        sopranoInfo = $"{sopranoMidi},{sopranoName},pc={sopranoPc}";
                    }
                    
                    // Get chord tone PCs
                    var chordTonePcs = TheoryVoicing.GetChordTonePitchClasses(new ChordEvent
                    {
                        Key = key,
                        Recipe = adjustedRecipe
                    });
                    string chordTonesPCStr = chordTonePcs != null && chordTonePcs.Count > 0
                        ? string.Join(",", chordTonePcs)
                        : "none";
                    
                    // Get base label (Roman numeral)
                    string baseLabel = TheoryChord.RecipeToRomanNumeral(key, adjustedRecipe);
                    
                    // Format detected tensions
                    string detectedStr = "none";
                    if (tensions != null && tensions.Count > 0)
                    {
                        var tensionNames = tensions.Select(t => t.Kind switch
                        {
                            TensionKind.FlatNine => "b9",
                            TensionKind.Nine => "9",
                            TensionKind.SharpNine => "#9",
                            TensionKind.Eleven => "11",
                            TensionKind.SharpEleven => "#11",
                            _ => "?"
                        }).ToList();
                        detectedStr = "{" + string.Join(",", tensionNames) + "}";
                    }
                    
                    // Check if melody note is present/valid
                    bool melodyPresent = chordEvents != null && i < chordEvents.Count && 
                                       chordEvents[i].MelodyMidi.HasValue;
                    string melodyInfo = melodyPresent 
                        ? $"melody={chordEvents[i].MelodyMidi.Value}({TheoryPitch.GetPitchNameFromMidi(chordEvents[i].MelodyMidi.Value, key)})"
                        : "melody=absent";
                    
                    // Format analyzed MIDI and PCs for debug
                    string midiStr = analyzedMidi != null && analyzedMidi.Length > 0
                        ? $"[{string.Join(",", analyzedMidi)}]"
                        : "[]";
                    string pcsStr = detectedTensions.AnalyzedPcs != null && detectedTensions.AnalyzedPcs.Length > 0
                        ? $"[{string.Join(",", detectedTensions.AnalyzedPcs)}]"
                        : "[]";
                    
                    // CRITICAL FIX: This is ANALYSIS_ONLY - does NOT drive Play mode satisfaction
                    // Play mode satisfaction comes from TheoryVoicing.VoiceLeadProgression tension detection
                    // This log is for UI display analysis only and must not overwrite Play results
                    UnityEngine.Debug.Log(
                        $"[TENSION_DETECT_ANALYSIS_ONLY] step={i} mode={voicingMode} baseLabel={baseLabel} " +
                        $"rootPC={debugRootPc} bassPC={bassPc} soprano={sopranoInfo} " +
                        $"chordTonesPC=[{chordTonesPCStr}] detected={detectedStr} {melodyInfo} " +
                        $"midi={midiStr} pcs={pcsStr} " +
                        $"(ANALYSIS_ONLY - does not affect Play mode satisfaction)");
                }
                
                // Note: finalSymbol will be logged after chordSymbol is built below

                // Ensure root name is computed from pitch class, not from token parsing
                // This fixes the bug where "bVII" was being parsed as "b" instead of "Bb"
                if (rootNoteName == null || rootNoteName.Length == 0 || 
                    (rootNoteName.Length == 1 && (rootNoteName == "b" || rootNoteName == "#")))
                {
                    // Recompute root name from recipe + key using pitch class
                    rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                        key,
                        adjustedRecipe.Degree,
                        adjustedRecipe.RootSemitoneOffset);
                }

                // Build chord symbol using adjusted recipe (for display) with tensions
                // rootNoteName is now the actual root, not the bass note
                // Build label once with tensions (no mutations inside detection)
                // This is the SINGLE canonical location for building chord labels with tensions
                string chordSymbol = tensions != null && tensions.Count > 0
                    ? TheoryChord.GetChordSymbolWithTensions(key, adjustedRecipe, rootNoteName, bassMidi, tensions)
                    : TheoryChord.GetChordSymbol(key, adjustedRecipe, rootNoteName, bassMidi);
                
                // Update debug log with final symbol
                if (TheoryVoicing.GetDebugTensionDetect())
                {
                    UnityEngine.Debug.Log($"[TENSION_DETECT_DEBUG] step={i} finalSymbol={chordSymbol}");
                }
                
                // Validate label formatting (prevent duplicates and malformed parentheses)
                ValidateChordLabelFormatting(chordSymbol, i);
                
                // Debug logging for requested extensions (comprehensive)
                if (enableTensionDetectDebug && adjustedRecipe.RequestedExtensions.HasAny)
                {
                    var req = adjustedRecipe.RequestedExtensions;
                    var realizedPcs = new HashSet<int>(analyzedMidi.Select(m => ((m % 12) + 12) % 12));
                    
                    // Compute root PC
                    int reqRootPc = TheoryScale.GetDegreePitchClass(key, adjustedRecipe.Degree);
                    if (reqRootPc < 0) reqRootPc = 0;
                    reqRootPc = (reqRootPc + adjustedRecipe.RootSemitoneOffset + 12) % 12;
                    if (reqRootPc < 0) reqRootPc += 12;
                    
                    // Build requested tensions list string
                    var reqList = new List<string>();
                    if (req.Sus4) reqList.Add("sus4");
                    if (req.Add9) reqList.Add("add9");
                    if (req.Add11) reqList.Add("add11");
                    if (req.Tension9) reqList.Add("9");
                    if (req.TensionFlat9) reqList.Add("b9");
                    if (req.TensionSharp11) reqList.Add("#11");
                    string reqStr = string.Join(",", reqList);
                    
                    // Build realized PCs list string
                    string realizedPcsStr = string.Join(",", realizedPcs.OrderBy(x => x));
                    
                    // Check each requested extension
                    var satisfied = new List<string>();
                    var unsatisfied = new List<string>();
                    
                    if (req.Sus4)
                    {
                        int fourthPc = (reqRootPc + 5) % 12;
                        if (realizedPcs.Contains(fourthPc))
                            satisfied.Add("sus4");
                        else
                            unsatisfied.Add("sus4");
                    }
                    
                    if (req.Add9)
                    {
                        int add9Pc = (reqRootPc + 2) % 12;
                        if (realizedPcs.Contains(add9Pc))
                            satisfied.Add("add9");
                        else
                            unsatisfied.Add("add9");
                    }
                    
                    if (req.Add11)
                    {
                        int add11Pc = (reqRootPc + 5) % 12;
                        if (realizedPcs.Contains(add11Pc))
                            satisfied.Add("add11");
                        else
                            unsatisfied.Add("add11");
                    }
                    
                    if (req.Tension9)
                    {
                        int ninePc = (reqRootPc + 2) % 12;
                        if (realizedPcs.Contains(ninePc))
                            satisfied.Add("9");
                        else
                            unsatisfied.Add("9");
                    }
                    
                    if (req.TensionFlat9)
                    {
                        int b9Pc = (reqRootPc + 1) % 12;
                        if (realizedPcs.Contains(b9Pc))
                            satisfied.Add("b9");
                        else
                            unsatisfied.Add("b9");
                    }
                    
                    if (req.TensionSharp11)
                    {
                        int sharp11Pc = (reqRootPc + 6) % 12;
                        if (realizedPcs.Contains(sharp11Pc))
                            satisfied.Add("#11");
                        else
                            unsatisfied.Add("#11");
                    }
                    
                    // Log comprehensive info
                    UnityEngine.Debug.Log(
                        $"[REQUESTED_EXT] step={i} input='{originalToken}' " +
                        $"root={rootNoteName} quality={adjustedRecipe.Quality} " +
                        $"requested=[{reqStr}] " +
                        $"realizedPCs=[{realizedPcsStr}] " +
                        $"satisfied=[{string.Join(",", satisfied)}] " +
                        $"unsatisfied=[{string.Join(",", unsatisfied)}]");
                }
                
                // Diagnostics for requested extensions (warnings)
                if (adjustedRecipe.RequestedExtensions.HasAny && _lastDiagnostics != null)
                {
                    var req = adjustedRecipe.RequestedExtensions;
                    
                    // Validate #11 on minor/diminished - warn if requested
                    if (req.TensionSharp11)
                    {
                        bool isMinorOrDim = (adjustedRecipe.Quality == ChordQuality.Minor || 
                                            adjustedRecipe.Quality == ChordQuality.Diminished);
                        if (isMinorOrDim)
                        {
                            _lastDiagnostics?.Add(i, DiagSeverity.Warning, DiagCode.NON_CHORD_TONE_SHARP11,
                                $"#11 requested on {adjustedRecipe.Quality} chord - ignored (not valid for minor/diminished).");
                        }
                    }
                }
                
                // Emit diagnostics for tension warnings
                if (tensions != null && tensions.Count > 0 && _lastDiagnostics != null)
                {
                    // Get realized pitch classes to check for 3rd presence
                    var realizedPcs = new HashSet<int>(analyzedMidi.Select(m => ((m % 12) + 12) % 12));
                    
                    // Get 3rd pitch class from recipe
                    int diagRootPc = TheoryScale.GetDegreePitchClass(key, adjustedRecipe.Degree);
                    if (diagRootPc < 0) diagRootPc = 0;
                    diagRootPc = (diagRootPc + adjustedRecipe.RootSemitoneOffset + 12) % 12;
                    if (diagRootPc < 0) diagRootPc += 12;
                    
                    int diagThirdInterval = adjustedRecipe.Quality switch
                    {
                        ChordQuality.Major => 4,
                        ChordQuality.Minor => 3,
                        ChordQuality.Diminished => 3,
                        ChordQuality.Augmented => 4,
                        _ => 4
                    };
                    int diagThirdPc = (diagRootPc + diagThirdInterval) % 12;
                    bool hasThirdInVoicing = realizedPcs.Contains(diagThirdPc);
                    
                    // Check each tension for warnings
                    foreach (var tension in tensions)
                    {
                        if (tension.Classification == TensionClassification.Suspension && hasThirdInVoicing)
                        {
                            _lastDiagnostics.Add(i, DiagSeverity.Warning, DiagCode.SUS4_CLASH_WITH_THIRD,
                                $"sus4 clash: 3rd present");
                        }
                        else if (tension.Classification == TensionClassification.AvoidTone)
                        {
                            _lastDiagnostics.Add(i, DiagSeverity.Warning, DiagCode.AVOID_TONE_11_OVER_DOM_WITH_3RD,
                                $"avoid tone (11 over dom w/3rd)");
                        }
                    }
                }

#if UNITY_EDITOR
                // Debug logging to verify root name computation
                if (enableDebugLogs)
                {
                    int computedRootPc = TheoryScale.GetDegreePitchClass(key, adjustedRecipe.Degree);
                    if (computedRootPc >= 0)
                    {
                        computedRootPc = (computedRootPc + adjustedRecipe.RootSemitoneOffset + 12) % 12;
                        if (computedRootPc < 0) computedRootPc += 12;
                    }
                    string romanNumeral = TheoryChord.RecipeToRomanNumeral(key, adjustedRecipe);
                    string tensionsStr = tensions != null && tensions.Count > 0 
                        ? string.Join(",", tensions.Select(t => t.Kind.ToString())) 
                        : "none";
                    Debug.Log($"[ChordSymbolDebug] key={key} degree={adjustedRecipe.Degree} roman={romanNumeral} " +
                              $"rootPc={computedRootPc} rootName='{rootNoteName}' tensions=[{tensionsStr}] " +
                              $"final='{chordSymbol}'");
                }
#endif

                // Generate key-aware Roman numeral for display (shows 'n' when appropriate)
                string displayRoman = TheoryChord.RecipeToRomanNumeral(key, adjustedRecipe);

                // Instantiate column prefab directly to container (preserves prefab appearance)
                var columnInstance = Instantiate(chordColumnPrefab, chordGridContainer);
                columnInstance.SetChord(chordSymbol, noteNames, displayRoman, status, analysisInfo);
                
                // Store reference for progressive reveal (index matches region index)
                // Ensure list is large enough
                while (_chordColumnViewsByRegion.Count <= i)
                {
                    _chordColumnViewsByRegion.Add(null);
                }
                _chordColumnViewsByRegion[i] = columnInstance;

                // Create spacer element for duration-based spacing (only if duration > 1)
                int durationQuarters = GetDurationQuarters(i);
                float spacerWidth = GetChordSpacerWidth(durationQuarters);
                
                if (spacerWidth > 0)
                {
                    GameObject spacer = new GameObject("DurationSpacer");
                    spacer.transform.SetParent(chordGridContainer, false);
                    
                    // Add LayoutElement to control spacer width
                    var layoutElement = spacer.AddComponent<UnityEngine.UI.LayoutElement>();
                    layoutElement.preferredWidth = spacerWidth;
                    layoutElement.flexibleWidth = 0;
                    layoutElement.minWidth = spacerWidth;
                    
                    // Set up spacer RectTransform to be invisible but take up space
                    var spacerRect = spacer.GetComponent<RectTransform>();
                    // Use stretch anchors vertically to match column height
                    spacerRect.anchorMin = new Vector2(0, 0);
                    spacerRect.anchorMax = new Vector2(0, 1);
                    spacerRect.pivot = new Vector2(0, 0.5f);
                    
                    // Make spacer invisible (fully transparent)
                    var image = spacer.AddComponent<UnityEngine.UI.Image>();
                    image.color = new Color(0, 0, 0, 0); // Fully transparent
                }

                if (enableDebugLogs)
                {
                    string notesStr = string.Join("/", noteNames);
                    Debug.Log($"[ChordLab] Column {i}: {chordSymbol} - {notesStr} ({noteNames.Count} notes) - {originalToken} - Status: {status}");
                }
            }
        }
        
        /// <summary>
        /// Validates chord label formatting to prevent duplicate tokens and malformed parentheses.
        /// Throws assertion errors in debug builds if issues are detected.
        /// </summary>
        private void ValidateChordLabelFormatting(string label, int stepIndex)
        {
            if (string.IsNullOrEmpty(label))
                return;
            
            // Check for double braces {{ or }}
            if (label.Contains("{{") || label.Contains("}}"))
            {
                UnityEngine.Debug.LogError($"[ChordLab] Step {stepIndex}: Label contains double braces: '{label}'");
            }
            
            // Check for double parentheses (( or ))
            if (label.Contains("((") || label.Contains("))"))
            {
                UnityEngine.Debug.LogError($"[ChordLab] Step {stepIndex}: Label contains double parentheses: '{label}'");
            }
            
            // Check for duplicate adjacent tension debug tokens (if any remain)
            // Pattern: {11} {11} or {#11} {#11}
            if (label.Contains("{11} {11}") || label.Contains("{#11} {#11}"))
            {
                UnityEngine.Debug.LogError($"[ChordLab] Step {stepIndex}: Label contains duplicate debug tokens: '{label}'");
            }
            
            // Check for duplicate tension patterns in parentheses
            // Pattern: (#11,#11) or (11,11)
            if (System.Text.RegularExpressions.Regex.IsMatch(label, @"\([^)]*#11[^)]*#11[^)]*\)") ||
                System.Text.RegularExpressions.Regex.IsMatch(label, @"\([^)]*11[^)]*11[^)]*\)"))
            {
                UnityEngine.Debug.LogError($"[ChordLab] Step {stepIndex}: Label contains duplicate tensions in parentheses: '{label}'");
            }
        }

        /// <summary>
        /// Updates the chord grid from a list of ChordEvents.
        /// Extracts recipes, builds chord MIDI arrays, and calls RenderChordGrid.
        /// </summary>
        private void UpdateChordGridFromChordEvents(TheoryKey key, IReadOnlyList<ChordEvent> chordEvents, IReadOnlyList<VoicedChord> voicedChords = null, string voicingMode = "Play")
        {
            if (chordEvents == null || chordEvents.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ChordLab] Cannot update chord grid: no chord events provided");
                return;
            }

            // Clear existing children
            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Extract recipes and build chord MIDI arrays
            var recipes = new List<ChordRecipe>(chordEvents.Count);
            var originalTokens = new List<string>(chordEvents.Count);
            var chords = new List<int[]>(chordEvents.Count);

            for (int i = 0; i < chordEvents.Count; i++)
            {
                var chordEvent = chordEvents[i];
                recipes.Add(chordEvent.Recipe);
                
                // Generate Roman numeral token from recipe
                string token = TheoryChord.RecipeToRomanNumeral(key, chordEvent.Recipe);
                originalTokens.Add(token);
                
                // Build chord MIDI array
                var chordMidi = TheoryChord.BuildChord(key, chordEvent.Recipe, rootOctave);
                if (chordMidi != null && chordMidi.Length > 0)
                {
                    chords.Add(chordMidi);
                }
                else
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ChordLab] Failed to build chord for event {i + 1}");
                    chords.Add(new int[0]);
                }
            }

            // Use the same recipes for both original and adjusted (no adjustment needed for harmonized chords)
            // Pass voiced chords if available for tension detection
            RenderChordGrid(key, originalTokens, recipes, chords, recipes, voicedChords, chordEvents, voicingMode);
        }

        /// <summary>
        /// Creates simple SATB voicing from a list of ChordEvents.
        /// Uses TheoryVoicing to generate voiced chords with appropriate voice ranges.
        /// </summary>
        private List<VoicedChord> CreateSimpleSATBVoicingFromChordEvents(TheoryKey key, IReadOnlyList<ChordEvent> chordEvents)
        {
            if (chordEvents == null || chordEvents.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ChordLab] Cannot create SATB voicing: no chord events provided");
                return null;
            }

            // Ensure all voicing settings are synced from Inspector before voicing calculations
            UpdateCompactnessWeights();
            UpdateVoiceLeadingTendencies();
            UpdateRegisterAndCompressionSettings();
            UpdateEleventhHeuristics();

            try
            {
                // Calculate upper voice MIDI ranges based on rootOctave
                var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
                
                // Debug logging for soprano range
                if (TheoryVoicing.GetTendencyDebug())
                {
                    Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
                }

                // Convert IReadOnlyList to List for TheoryVoicing
                var eventsList = new List<ChordEvent>(chordEvents);

                // Check if any chord events have melody
                bool hasMelody = eventsList.Any(evt => evt.MelodyMidi.HasValue);

                if (hasMelody)
                {
                    // Use melody-constrained voicing
                    return TheoryVoicing.VoiceLeadProgressionWithMelody(
                        eventsList,
                        numVoices: 4,
                        rootOctave: rootOctave,
                        bassOctave: rootOctave - 1,
                        upperMinMidi: upperMinMidi,
                        upperMaxMidi: upperMaxMidi
                    );
                }
                else
                {
                    // Use chord-only voicing
                    return TheoryVoicing.VoiceLeadProgression(
                        eventsList,
                        numVoices: 4,
                        bassOctave: rootOctave - 1,
                        upperMinMidi: upperMinMidi,
                        upperMaxMidi: upperMaxMidi
                    );
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChordLab] Failed to create SATB voicing: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Builds a Roman numeral progression string from a list of harmonized chord steps.
        /// Only includes steps that have a valid chosen chord (non-empty Roman string).
        /// Deduplicates consecutive identical chords at the same time slice.
        /// </summary>
        /// <param name="steps">List of harmonized chord steps from naive harmonization</param>
        /// <returns>Space-separated Roman numeral progression string (e.g., "I V6 viio6 I6 IV ii6 V I")</returns>
        private static string BuildRomanProgressionString(List<HarmonizedChordStep> steps)
        {
            if (steps == null || steps.Count == 0)
                return string.Empty;

            var romanTokens = new List<string>();
            string lastRoman = null;
            float lastTimeBeats = float.MinValue;

            foreach (var step in steps)
            {
                // Only include steps with valid chosen chords (non-empty Roman string)
                if (step.ChosenChord.Roman != null && step.ChosenChord.Roman != "")
                {
                    string currentRoman = step.ChosenChord.Roman;
                    float currentTimeBeats = step.MelodyEvent.TimeBeats;

                    // Avoid duplicating the same chord at the same time slice
                    // (skip if same Roman and same time as previous step)
                    bool isDuplicate = (currentRoman == lastRoman && 
                                       Mathf.Approximately(currentTimeBeats, lastTimeBeats));

                    if (!isDuplicate)
                    {
                        romanTokens.Add(currentRoman);
                        lastRoman = currentRoman;
                        lastTimeBeats = currentTimeBeats;
                    }
                }
            }

            return string.Join(" ", romanTokens);
        }

        /// <summary>
        /// Updates the status text display.
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Gets the voice name from voice index (0=Bass, 1=Tenor, 2=Alto, 3=Soprano).
        /// </summary>
        private string GetVoiceName(int voiceIndex)
        {
            return voiceIndex switch
            {
                0 => "Bass",
                1 => "Tenor",
                2 => "Alto",
                3 => "Soprano",
                _ => $"Voice{voiceIndex}"
            };
        }

        /// <summary>
        /// Appends a region header and optional extra info to the user-facing console.
        /// Called at region start during playback. Shows header "R{n} {ChordLabel}" and
        /// extra info lines (Forced diagnostics and/or borrowed/non-diatonic labels) if present.
        /// </summary>
        /// <param name="regionIndex">Zero-based region index</param>
        private void LogRegionHeadline(int regionIndex)
        {
            if (statusText == null) return;

            // Add visual separator before region (except first)
            if (!_isFirstRegionInSession && statusText.text.Length > 0)
            {
                statusText.text += "\n────────────";
            }
            _isFirstRegionInSession = false;

            // Get region label
            string regionLabel = "Region";
            if (_lastRegions != null && regionIndex < _lastRegions.Count)
            {
                regionLabel = _lastRegions[regionIndex].debugLabel ?? "Region";
            }

            // Get TheoryKey for note name conversion (needed for Forced messages)
            TheoryKey key = GetKeyFromDropdowns();

            // Build header line (no "OK" suffix)
            string header = $"{regionIndex + 1} {regionLabel}";

            // Collect extra info lines (Forced diagnostics and/or borrowed label)
            List<string> extraInfoLines = new List<string>();

            // Get Forced diagnostics only (suppress Warning and Info)
            RegionDiagEvent? bestForcedEvent = null;
            if (_lastDiagnostics != null)
            {
                var allRegions = _lastDiagnostics.GetAll();
                var regionDiags = allRegions.FirstOrDefault(r => r.regionIndex == regionIndex);
                
                if (regionDiags != null && regionDiags.events.Count > 0)
                {
                    // Priority order for codes (when multiple events at same severity)
                    var codePriority = new Dictionary<string, int>
                    {
                        { DiagCode.FORCED_7TH_RESOLUTION, 1 },
                        { DiagCode.COVERAGE_FIX_APPLIED, 2 },
                        { DiagCode.MISSING_REQUIRED_TONE, 3 },
                        { DiagCode.REGISTER_CLAMPED, 4 },
                        { DiagCode.SPACING_CLAMPED, 4 },
                    };

                    // Only consider Forced events (suppress Warning and Info)
                    var forcedEvents = regionDiags.events.Where(e => 
                        e.severity == DiagSeverity.Forced).ToList();

                    if (forcedEvents.Count > 0)
                    {
                        // If multiple Forced events, pick by code priority
                        bestForcedEvent = forcedEvents.OrderBy(e => codePriority.GetValueOrDefault(e.code, 999)).First();
                    }
                }
            }

            // Format Forced event message
            if (bestForcedEvent.HasValue)
            {
                var evt = bestForcedEvent.Value;
                
                if (evt.code == DiagCode.FORCED_7TH_RESOLUTION && 
                    evt.voiceIndex >= 0 && 
                    evt.beforeMidi >= 0 && 
                    evt.afterMidi >= 0)
                {
                    // Special formatting for 7th resolution: show voice + note motion
                    string voiceName = GetVoiceName(evt.voiceIndex);
                    string beforeNote = TheoryPitch.GetPitchNameFromMidi(evt.beforeMidi, key);
                    string afterNote = TheoryPitch.GetPitchNameFromMidi(evt.afterMidi, key);
                    extraInfoLines.Add($"Forced: Chordal 7th resolved down by step ({voiceName} {beforeNote} → {afterNote})");
                }
                else if (evt.voiceIndex >= 0 && evt.beforeMidi >= 0 && evt.afterMidi >= 0)
                {
                    // Other Forced events with before/after: show voice + note motion
                    string voiceName = GetVoiceName(evt.voiceIndex);
                    string beforeNote = TheoryPitch.GetPitchNameFromMidi(evt.beforeMidi, key);
                    string afterNote = TheoryPitch.GetPitchNameFromMidi(evt.afterMidi, key);
                    extraInfoLines.Add($"Forced: {evt.message} ({voiceName} {beforeNote} → {afterNote})");
                }
                else
                {
                    // Forced event without before/after: use message as-is
                    extraInfoLines.Add($"Forced: {evt.message}");
                }
            }

            // Add borrowed/non-diatonic label if present (reuse existing purple-column label)
            if (_regionAnalysisInfoByIndex.TryGetValue(regionIndex, out string analysisInfo) && 
                !string.IsNullOrEmpty(analysisInfo))
            {
                // Use the analysisInfo directly (same as purple column displays)
                extraInfoLines.Add(analysisInfo);
            }

            // Build console output
            if (statusText.text.Length > 0)
            {
                statusText.text += "\n" + header;
            }
            else
            {
                statusText.text = header;
            }

            // Add blank line and extra info if present
            if (extraInfoLines.Count > 0)
            {
                statusText.text += "\n" + string.Join("\n", extraInfoLines);
            }

            // Auto-scroll to bottom (lightweight - doesn't stop coroutines)
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// Clears the user-facing console.
        /// </summary>
        private void ClearConsole()
        {
            if (statusText != null)
            {
                statusText.text = "";
            }
            // Reset first region flag when clearing console
            _isFirstRegionInSession = true;
            // DO NOT clear _regionAnalysisInfoByIndex here - it's populated by RenderChordGrid
            // and needs to persist for LogRegionHeadline during playback
        }

        /// <summary>
        /// Current highlighted region index (-1 if none).
        /// </summary>
        private int _currentHighlightedRegion = -1;

        /// <summary>
        /// Hides all chord columns for progressive reveal at playback start.
        /// Uses CanvasGroup alpha to preserve layout spacing.
        /// </summary>
        private void HideAllChordColumns()
        {
            _currentHighlightedRegion = -1;
            foreach (var columnView in _chordColumnViewsByRegion)
            {
                if (columnView != null)
                {
                    columnView.SetVizState(
                        ChordColumnView.ColumnVizState.Hidden,
                        hiddenAlpha,
                        visibleAlpha,
                        highlightedAlpha,
                        visibleTint,
                        highlightedTint
                    );
                }
            }
        }

        /// <summary>
        /// Sets the visual state for a chord column at the given region index.
        /// </summary>
        /// <param name="regionIndex">Zero-based region index</param>
        /// <param name="state">The visual state to apply</param>
        private void SetChordColumnState(int regionIndex, ChordColumnView.ColumnVizState state)
        {
            if (regionIndex >= 0 && regionIndex < _chordColumnViewsByRegion.Count)
            {
                var columnView = _chordColumnViewsByRegion[regionIndex];
                if (columnView != null)
                {
                    columnView.SetVizState(
                        state,
                        hiddenAlpha,
                        visibleAlpha,
                        highlightedAlpha,
                        visibleTint,
                        highlightedTint
                    );
                }
            }
        }

        /// <summary>
        /// Highlights the chord column for the currently playing region.
        /// Called when playback reaches a region (mirrors voicing viewer timing).
        /// Previous highlighted region returns to Visible state.
        /// </summary>
        /// <param name="regionIndex">Zero-based region index of the currently playing region</param>
        private void HighlightChordColumn(int regionIndex)
        {
            // Return previous highlighted region to Visible state
            if (_currentHighlightedRegion >= 0 && _currentHighlightedRegion < _chordColumnViewsByRegion.Count)
            {
                SetChordColumnState(_currentHighlightedRegion, ChordColumnView.ColumnVizState.Visible);
            }

            // Set new highlighted region
            _currentHighlightedRegion = regionIndex;
            SetChordColumnState(regionIndex, ChordColumnView.ColumnVizState.Highlighted);
        }

        /// <summary>
        /// Reveals a chord column for the given region index.
        /// Called when playback reaches that region (mirrors voicing viewer timing).
        /// </summary>
        /// <param name="regionIndex">Zero-based region index</param>
        private void RevealChordColumn(int regionIndex)
        {
            // Reveal the column (will be highlighted by HighlightChordColumn)
            SetChordColumnState(regionIndex, ChordColumnView.ColumnVizState.Visible);
        }

        /// <summary>
        /// Parses Roman numeral progression input text into tokens and recipes.
        /// Reusable helper for both playback and debug analysis.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="inputText">The progression input text (space-separated Roman numerals)</param>
        /// <param name="originalTokens">Output: List of original Roman numeral strings</param>
        /// <param name="recipes">Output: List of parsed ChordRecipe objects</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        private bool TryBuildChordRecipesFromRomanInput(TheoryKey key, string inputText, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters)
        {
            originalTokens = new List<string>();
            recipes = new List<ChordRecipe>();
            durationsInQuarters = new List<int>();

            if (string.IsNullOrWhiteSpace(inputText))
            {
                return false;
            }

            // Split by whitespace into numerals
            string[] tokens = inputText.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            originalTokens = new List<string>(tokens);

            // Parse each token: extract Roman numeral and optional duration suffix (:N)
            foreach (string token in originalTokens)
            {
                // Split on ':' to separate Roman numeral from duration
                string[] parts = token.Split(new[] { ':' }, 2);
                string romanToken = parts[0];
                int quarters = 1; // Default: 1 quarter note

                // Parse duration suffix if present
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (int.TryParse(parts[1], out int parsedQuarters))
                    {
                        if (parsedQuarters >= 1)
                        {
                            quarters = parsedQuarters;
                        }
                        else
                        {
                            Debug.LogWarning($"[ChordLab] Invalid duration '{parts[1]}' in token '{token}' (must be >= 1). Using default 1 quarter.");
                            quarters = 1;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ChordLab] Could not parse duration '{parts[1]}' in token '{token}'. Using default 1 quarter.");
                        quarters = 1;
                    }
                }

                // Parse Roman numeral
                if (!TheoryChord.TryParseRomanNumeral(key, romanToken, out var recipe))
                {
                    // Enhanced diagnostics for regression harness
                    if (Sonoria.MusicTheory.RegressionHarness.EnableRegressionHarness)
                    {
                        UnityEngine.Debug.LogWarning($"[TryBuildChordRecipesFromRomanInput] Failed to parse token '{token}' (roman='{romanToken}') in key {key}");
                    }
                    return false; // Parsing failed
                }

                recipes.Add(recipe);
                durationsInQuarters.Add(quarters);

                // Debug logging for duration parsing
                if (enableDebugLogs && parts.Length > 1)
                {
                    int durationTicks = quarters * timelineSpec.ticksPerQuarter;
                    Debug.Log($"[ChordLab] Parsed token '{token}' -> roman='{romanToken}', quarters={quarters}, durationTicks={durationTicks}");
                }
            }

            return true;
        }

        /// <summary>
        /// Builds ChordRegion[] from Roman numeral input text with duration suffix support.
        /// Parses tokens with :N syntax, builds ChordEvents, optionally attaches melody MIDI,
        /// and creates regions with cumulative startTick.
        /// </summary>
        /// <param name="romanText">The Roman numeral progression input text (e.g., "I:2 V vi IV")</param>
        /// <param name="key">The key context</param>
        /// <param name="spec">Timeline specification</param>
        /// <param name="melodyMidiPerRegion">Optional list of melody MIDI notes (one per region). If provided and index in range, attaches to chordEvent.</param>
        /// <param name="skipAutoCorrection">If true, skips quality adjustment to mode</param>
        /// <returns>List of ChordRegion objects, or null if parsing failed</returns>
        public List<ChordRegion> BuildRegionsFromRomanInput(
            string romanText,
            TheoryKey key,
            TimelineSpec spec,
            IReadOnlyList<int> melodyMidiPerRegion = null,
            bool skipAutoCorrection = false)
        {
            if (string.IsNullOrWhiteSpace(romanText))
            {
                return null;
            }

            // Parse Roman numerals with duration suffixes
            if (!TryBuildChordRecipesFromRomanInput(key, romanText, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters))
            {
                if (enableDebugLogs || Sonoria.MusicTheory.RegressionHarness.EnableRegressionHarness)
                {
                    // Enhanced diagnostics: show which token failed
                    char[] separators = { ' ', '\t', '\n', '\r' };
                    string[] tokens = romanText.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
                    UnityEngine.Debug.LogWarning($"[BuildRegionsFromRomanInput] TryBuildChordRecipesFromRomanInput failed for input: '{romanText}'");
                    UnityEngine.Debug.LogWarning($"[BuildRegionsFromRomanInput] Split tokens ({tokens.Length}): [{string.Join(" | ", tokens.Select((t, i) => $"#{i}:'{t}'"))}]");
                    
                    // Try parsing each token individually to identify the failure
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        string token = tokens[i].Trim();
                        // Extract Roman part (before :N if present)
                        string[] parts = token.Split(':');
                        string romanToken = parts[0];
                        
                        if (!TheoryChord.TryParseRomanNumeral(key, romanToken, out var testRecipe))
                        {
                            UnityEngine.Debug.LogWarning($"[BuildRegionsFromRomanInput] ✗ Token #{i} '{token}' (roman='{romanToken}') FAILED TryParseRomanNumeral");
                        }
                        else
                        {
                            UnityEngine.Debug.Log($"[BuildRegionsFromRomanInput] ✓ Token #{i} '{token}' (roman='{romanToken}'): Parsed successfully");
                        }
                    }
                }
                return null;
            }

            if (recipes == null || recipes.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[BuildRegionsFromRomanInput] No recipes parsed from input: '{romanText}'");
                return null;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[BuildRegionsFromRomanInput] Parsed {durationsInQuarters.Count} durations: [{string.Join(", ", durationsInQuarters)}]");
                Debug.Log($"[BuildRegionsFromRomanInput] Parsed {originalTokens.Count} tokens: [{string.Join(", ", originalTokens)}]");
            }

            // Adjust recipes to match diatonic triad quality for the mode (if enabled and not skipping)
            var adjustedRecipes = new List<ChordRecipe>(recipes.Count);
            for (int i = 0; i < recipes.Count; i++)
            {
                var originalRecipe = recipes[i];
                if (!skipAutoCorrection && autoCorrectToMode)
                {
                    var adjusted = TheoryChord.AdjustTriadQualityToMode(key, originalRecipe, out bool wasAdjusted);
                    adjustedRecipes.Add(adjusted);
                    if (wasAdjusted && enableDebugLogs)
                    {
                        string adjustedNumeral = TheoryChord.RecipeToRomanNumeral(key, adjusted);
                        Debug.Log($"[ChordLab] Adjusted chord {i + 1} to '{adjustedNumeral}' to fit {key}");
                    }
                }
                else
                {
                    adjustedRecipes.Add(originalRecipe);
                }
            }

            // Build ChordEvents from recipes
            List<ChordEvent> chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);
            if (chordEvents == null || chordEvents.Count == 0)
            {
                return null;
            }

            // Attach melody MIDI if provided
            if (melodyMidiPerRegion != null)
            {
                for (int i = 0; i < chordEvents.Count && i < melodyMidiPerRegion.Count; i++)
                {
                    int melodyMidi = melodyMidiPerRegion[i];
                    // Apply melody octave offset (only affects playback register, not theory)
                    int melodyMidiWithOffset = melodyMidi + MelodyOffsetSemitones;
                    chordEvents[i] = new ChordEvent
                    {
                        Key = chordEvents[i].Key,
                        Recipe = chordEvents[i].Recipe,
                        TimeBeats = chordEvents[i].TimeBeats,
                        MelodyMidi = melodyMidiWithOffset
                    };
                }
            }

            // Build regions with cumulative startTick
            var regions = new List<ChordRegion>(chordEvents.Count);
            int cumulativeStartTick = 0;
            if (enableDebugLogs)
            {
                Debug.Log($"[BuildRegionsFromRomanInput] Building {chordEvents.Count} regions with durationsInQuarters: [{string.Join(", ", durationsInQuarters)}]");
            }
            for (int i = 0; i < chordEvents.Count; i++)
            {
                // Get duration in quarters (from parsed durations)
                int quarters = (durationsInQuarters != null && i < durationsInQuarters.Count)
                    ? durationsInQuarters[i]
                    : 1;
                int durationTicks = quarters * spec.ticksPerQuarter;

                if (enableDebugLogs && i == 0)
                {
                    Debug.Log($"[BuildRegionsFromRomanInput] Region 0: quarters={quarters} (from durationsInQuarters[{i}]={(durationsInQuarters != null && i < durationsInQuarters.Count ? durationsInQuarters[i].ToString() : "N/A")}), durationTicks={durationTicks}, ticksPerQuarter={spec.ticksPerQuarter}");
                }

                // Get debug label from original token
                string debugLabel = (originalTokens != null && i < originalTokens.Count)
                    ? originalTokens[i]
                    : null;

                var region = new ChordRegion
                {
                    startTick = cumulativeStartTick,
                    durationTicks = durationTicks,
                    chordEvent = chordEvents[i],
                    debugLabel = debugLabel
                };
                regions.Add(region);

                // Update cumulative startTick for next region
                cumulativeStartTick += durationTicks;

                // Debug logging for constructed regions
                if (enableDebugLogs)
                {
                    int rootPc = TheoryScale.GetDegreePitchClass(key, chordEvents[i].Recipe.Degree);
                    if (rootPc >= 0)
                    {
                        rootPc = (rootPc + chordEvents[i].Recipe.RootSemitoneOffset + 12) % 12;
                        if (rootPc < 0) rootPc += 12;
                    }
                    Debug.Log($"[ChordLab Region] Index={i}, Roman='{debugLabel ?? "?"}', startTick={region.startTick}, durationTicks={region.durationTicks} (quarters={quarters}), rootPc={rootPc}, ticksPerQuarter={spec.ticksPerQuarter}");
                }
            }

            return regions;
        }

        /// <summary>
        /// Builds ChordRegion[] from chord symbol input text with duration suffix support.
        /// Parses tokens with :N syntax, builds ChordEvents, optionally attaches melody MIDI,
        /// and creates regions with cumulative startTick.
        /// </summary>
        /// <param name="chordSymbolText">The chord symbol progression input text (e.g., "Cmaj7:2 F:1 G:4")</param>
        /// <param name="key">The key context</param>
        /// <param name="spec">Timeline specification</param>
        /// <param name="melodyMidiPerRegion">Optional list of melody MIDI notes (one per region). If provided and index in range, attaches to chordEvent.</param>
        /// <returns>List of ChordRegion objects, or null if parsing failed</returns>
        public List<ChordRegion> BuildRegionsFromChordSymbolInput(
            string chordSymbolText,
            TheoryKey key,
            TimelineSpec spec,
            IReadOnlyList<int> melodyMidiPerRegion = null)
        {
            if (string.IsNullOrWhiteSpace(chordSymbolText))
            {
                return null;
            }

            // Parse chord symbols with duration suffixes
            if (!TryBuildChordRecipesFromChordSymbolInput(key, chordSymbolText, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters, out string errorMessage))
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[BuildRegionsFromChordSymbolInput] TryBuildChordRecipesFromChordSymbolInput failed for input: '{chordSymbolText}'. Error: {errorMessage}");
                return null;
            }

            if (recipes == null || recipes.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[BuildRegionsFromChordSymbolInput] No recipes parsed from input: '{chordSymbolText}'");
                return null;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[BuildRegionsFromChordSymbolInput] Parsed {durationsInQuarters.Count} durations: [{string.Join(", ", durationsInQuarters)}]");
                Debug.Log($"[BuildRegionsFromChordSymbolInput] Parsed {originalTokens.Count} tokens: [{string.Join(", ", originalTokens)}]");
            }

            // Chord symbols skip auto-correction (they sound exactly as typed)
            var adjustedRecipes = new List<ChordRecipe>(recipes);
            // No adjustment needed for chord symbols

            // Build ChordEvents from recipes
            List<ChordEvent> chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);
            if (chordEvents == null || chordEvents.Count == 0)
            {
                return null;
            }

            // Attach melody MIDI if provided
            if (melodyMidiPerRegion != null)
            {
                for (int i = 0; i < chordEvents.Count && i < melodyMidiPerRegion.Count; i++)
                {
                    int melodyMidi = melodyMidiPerRegion[i];
                    // Apply melody octave offset (only affects playback register, not theory)
                    int melodyMidiWithOffset = melodyMidi + MelodyOffsetSemitones;
                    chordEvents[i] = new ChordEvent
                    {
                        Key = chordEvents[i].Key,
                        Recipe = chordEvents[i].Recipe,
                        TimeBeats = chordEvents[i].TimeBeats,
                        MelodyMidi = melodyMidiWithOffset
                    };
                }
            }

            // Build regions with cumulative startTick
            var regions = new List<ChordRegion>(chordEvents.Count);
            int cumulativeStartTick = 0;
            if (enableDebugLogs)
            {
                Debug.Log($"[BuildRegionsFromChordSymbolInput] Building {chordEvents.Count} regions with durationsInQuarters: [{string.Join(", ", durationsInQuarters)}]");
            }
            for (int i = 0; i < chordEvents.Count; i++)
            {
                // Get duration in quarters (from parsed durations)
                int quarters = (durationsInQuarters != null && i < durationsInQuarters.Count)
                    ? durationsInQuarters[i]
                    : 1;
                int durationTicks = quarters * spec.ticksPerQuarter;

                if (enableDebugLogs && i == 0)
                {
                    Debug.Log($"[BuildRegionsFromChordSymbolInput] Region 0: quarters={quarters} (from durationsInQuarters[{i}]={(durationsInQuarters != null && i < durationsInQuarters.Count ? durationsInQuarters[i].ToString() : "N/A")}), durationTicks={durationTicks}, ticksPerQuarter={spec.ticksPerQuarter}");
                }

                // Get debug label from original token
                string debugLabel = (originalTokens != null && i < originalTokens.Count)
                    ? originalTokens[i]
                    : null;

                var region = new ChordRegion
                {
                    startTick = cumulativeStartTick,
                    durationTicks = durationTicks,
                    chordEvent = chordEvents[i],
                    debugLabel = debugLabel
                };
                regions.Add(region);

                // Update cumulative startTick for next region
                cumulativeStartTick += durationTicks;

                // Debug logging for constructed regions
                if (enableDebugLogs)
                {
                    int rootPc = TheoryScale.GetDegreePitchClass(key, chordEvents[i].Recipe.Degree);
                    if (rootPc >= 0)
                    {
                        rootPc = (rootPc + chordEvents[i].Recipe.RootSemitoneOffset + 12) % 12;
                        if (rootPc < 0) rootPc += 12;
                    }
                    Debug.Log($"[ChordLab Region] Index={i}, ABS='{debugLabel ?? "?"}', startTick={region.startTick}, durationTicks={region.durationTicks} (quarters={quarters}), rootPc={rootPc}, ticksPerQuarter={spec.ticksPerQuarter}");
                }
            }

            return regions;
        }

        /// <summary>
        /// Parses chord symbol progression input text into recipes.
        /// Chord symbols are parsed exactly as typed (no auto-correction).
        /// Supports :N duration suffixes (e.g., "Cmaj7:2", "F#m7:3").
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="chordSymbolInput">The progression input text (space-separated chord symbols)</param>
        /// <param name="originalTokens">Output: List of original tokens (including :N suffixes)</param>
        /// <param name="recipes">Output: List of parsed ChordRecipe objects</param>
        /// <param name="durationsInQuarters">Output: List of durations in quarter notes for each chord (default 1 if no suffix)</param>
        /// <param name="errorMessage">Output: Error message if parsing failed</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public bool TryBuildChordRecipesFromChordSymbolInput(TheoryKey key, string chordSymbolInput, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters, out string errorMessage)
        {
            originalTokens = new List<string>();
            recipes = new List<ChordRecipe>();
            durationsInQuarters = new List<int>();
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(chordSymbolInput))
            {
                errorMessage = "Chord symbol input is empty";
                return false;
            }

            // Split by whitespace into tokens
            string[] tokens = chordSymbolInput.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                errorMessage = "No chord symbols found in input";
                return false;
            }

            originalTokens = new List<string>(tokens);

            // Parse each token: extract chord symbol and optional duration suffix (:N)
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                // Split on ':' to separate chord symbol from duration
                string[] parts = token.Split(new[] { ':' }, 2);
                string chordSymbolRaw = parts[0];
                int quarters = 1; // Default: 1 quarter note

                // Parse duration suffix if present
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (int.TryParse(parts[1], out int parsedQuarters))
                    {
                        if (parsedQuarters >= 1)
                        {
                            quarters = parsedQuarters;
                        }
                        else
                        {
                            Debug.LogWarning($"[ChordLab] Invalid duration '{parts[1]}' in token '{token}' (must be >= 1). Using default 1 quarter.");
                            quarters = 1;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ChordLab] Could not parse duration '{parts[1]}' in token '{token}'. Using default 1 quarter.");
                        quarters = 1;
                    }
                }

                // Parse chord symbol (without duration suffix)
                if (!TheoryChord.TryParseChordSymbol(key, chordSymbolRaw, out ChordRecipe recipe, out string tokenError))
                {
                    errorMessage = $"Chord {i + 1} ('{chordSymbolRaw}' from '{token}'): {tokenError}";
                    return false;
                }

                // Trace: Log parsed requested extensions right after parsing
                if (TheoryVoicing.s_debugTensionDetect)
                {
                    var req = recipe.RequestedExtensions;
                    var reqList = new List<string>();
                    if (req.Sus4) reqList.Add("sus4");
                    if (req.Add9) reqList.Add("add9");
                    if (req.Add11) reqList.Add("add11");
                    if (req.Tension9) reqList.Add("9");
                    if (req.TensionFlat9) reqList.Add("b9");
                    if (req.TensionSharp11) reqList.Add("#11");
                    string reqStr = reqList.Count > 0 ? string.Join(",", reqList) : "none";
                    UnityEngine.Debug.Log(
                        $"[REQ_EXT_TRACE] step={i} token='{token}' chordSymbol='{chordSymbolRaw}' parsedRequested=[{reqStr}]");
                }

                recipes.Add(recipe);
                durationsInQuarters.Add(quarters);

                // Debug logging for duration parsing
                if (enableDebugLogs && parts.Length > 1)
                {
                    int durationTicks = quarters * timelineSpec.ticksPerQuarter;
                    Debug.Log($"[ChordLab] Parsed ABS token '{token}' -> chord='{chordSymbolRaw}', quarters={quarters}, durationTicks={durationTicks}");
                }
            }

            if (recipes.Count == 0)
            {
                errorMessage = "No valid chord symbols could be parsed";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Editor-only debug method that logs detailed analysis of the current progression.
        /// Uses the same parsing and analysis pipeline as the Play button.
        /// </summary>
        public void DebugLogCurrentProgressionAnalysis()
        {
            try
            {
                // 1. Get the current key from dropdowns (reuse the existing helper)
                var key = GetKeyFromDropdowns();

                // 2. Get the progression text from the input field
                var progressionText = progressionInput != null ? progressionInput.text : string.Empty;

                if (string.IsNullOrWhiteSpace(progressionText))
                {
                    Debug.LogWarning("ChordLab Debug: Progression input is empty.");
                    return;
                }

                Debug.Log($"[ChordLab Debug] Key = {key}, progression = \"{progressionText}\"");

                // 3. Parse progression using the same logic as PlayProgressionCo
                bool parseSuccess = false;
                List<string> originalTokens = null;
                List<ChordRecipe> recipes = null;
                
                try
                {
                    parseSuccess = TryBuildChordRecipesFromRomanInput(key, progressionText, out originalTokens, out recipes, out List<int> _);
                }
                catch (System.Exception ex)
                {
                    parseSuccess = false;
                    Debug.LogWarning($"ChordLab Debug: Exception during parsing: {ex}");
                }
                
                if (!parseSuccess)
                {
                    Debug.LogWarning("ChordLab Debug: Failed to parse progression. Check for invalid Roman numerals.");
                    return;
                }

                Debug.Log($"[ChordLab Debug] Parsed {recipes.Count} chords");

                // 4. For each chord, analyze and log detailed information
                for (int i = 0; i < recipes.Count; i++)
                {
                    var recipe = recipes[i];
                    var originalToken = originalTokens[i];

                    // Analyze the original recipe (before any auto-correction)
                    var profile = TheoryChord.AnalyzeChordProfile(key, recipe);

                    // Build chord to get MIDI notes
                    int[] midiNotes = TheoryChord.BuildChord(key, recipe, rootOctave);
                    
                    // Get root note name using key-aware degree spelling
                    string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                        key,
                        recipe.Degree,
                        recipe.RootSemitoneOffset);

                    // Find the lowest MIDI note (bass note) for slash chord notation
                    int? bassMidi = midiNotes != null && midiNotes.Length > 0 ? midiNotes.Min() : (int?)null;

                    // Build chord symbol using existing helper
                    string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootNoteName, bassMidi);

                    // Generate key-aware Roman numeral for display
                    string displayRoman = TheoryChord.RecipeToRomanNumeral(key, recipe);

                    // Format MIDI notes and note names
                    string midiStr = midiNotes != null && midiNotes.Length > 0
                        ? string.Join(", ", midiNotes)
                        : "(none)";
                    
                    string pitchClassesStr = midiNotes != null && midiNotes.Length > 0
                        ? string.Join(", ", System.Array.ConvertAll(midiNotes, n => (n % 12).ToString()))
                        : "(none)";
                    
                    // Use canonical chord spelling for note names (ensures correct enharmonic spellings)
                    string noteNamesStr = "(none)";
                    if (midiNotes != null && midiNotes.Length > 0)
                    {
                        var canonicalNames = TheoryChord.GetSpelledChordTones(key, recipe);
                        if (canonicalNames != null && canonicalNames.Count == midiNotes.Length)
                        {
                            // Use canonical spelling from lookup tables
                            noteNamesStr = string.Join(", ", canonicalNames);
                        }
                        else
                        {
                            // Fallback to key-based spelling if canonical spelling unavailable
                            noteNamesStr = string.Join(", ", System.Array.ConvertAll(midiNotes, n => TheoryPitch.GetPitchNameFromMidi(n, key)));
                        }
                    }

                    // Build analysis info string
                    string analysisInfo = TheoryChord.BuildNonDiatonicInfo(profile);

                    // Log comprehensive chord analysis
                    Debug.Log(
                        $"[Chord {i + 1}]\n" +
                        $"  Roman Input: {originalToken}\n" +
                        $"  Display Roman: {displayRoman}\n" +
                        $"  Chord Symbol: {chordSymbol}\n" +
                        $"  Degree: {profile.Degree}\n" +
                        $"  Root Pitch Class: {profile.RootPitchClass}\n" +
                        $"  DiatonicStatus: {profile.DiatonicStatus}\n" +
                        $"  FunctionTag: {profile.FunctionTag}\n" +
                        $"  BorrowSummary: {profile.BorrowSummary}\n" +
                        $"  ParallelModeMembership: {profile.ParallelModeMembership}\n" +
                        $"  SecondaryTargetDegree: {(profile.SecondaryTargetDegree.HasValue ? profile.SecondaryTargetDegree.Value.ToString() : "null")}\n" +
                        $"  BassDegree: {profile.BassDegree}\n" +
                        $"  MIDI Notes: {midiStr}\n" +
                        $"  Pitch Classes: {pitchClassesStr}\n" +
                        $"  Note Names: {noteNamesStr}\n" +
                        $"  Analysis Info: {analysisInfo}"
                    );
                }

                Debug.Log($"[ChordLab Debug] Analysis complete for {recipes.Count} chords");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ChordLab Debug: Exception while logging progression: {ex}");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only debug menu item: Prints accidental preferences for all 12 tonic indices in Ionian mode.
        /// Verifies that the mapping matches the expected behavior:
        /// - Sharps: C(0), D(2), E(4), G(7), A(9), B(11)
        /// - Flats: Db(1), Eb(3), F(5), Gb(6), Ab(8), Bb(10)
        /// </summary>
        [UnityEditor.MenuItem("Tools/Chord Lab/Print Accidental Preferences (Ionian)")]
        public static void PrintAccidentalPreferences()
        {
            for (int tonicIndex = 0; tonicIndex < 12; tonicIndex++)
            {
                var key = new TheoryKey(tonicIndex, Sonoria.MusicTheory.ScaleMode.Ionian);
                var pref = TheoryPitch.GetAccidentalPreference(key);
                string tonicName = tonicIndex switch
                {
                    0 => "C",
                    1 => "Db",
                    2 => "D",
                    3 => "Eb",
                    4 => "E",
                    5 => "F",
                    6 => "Gb",
                    7 => "G",
                    8 => "Ab",
                    9 => "A",
                    10 => "Bb",
                    11 => "B",
                    _ => $"Index{tonicIndex}"
                };
                UnityEngine.Debug.Log($"TonicIndex {tonicIndex} ({tonicName}): {key} → {pref}");
            }
        }

        /// <summary>
        /// Editor-only debug menu item: Tests the chord symbol parser with a hard-coded list of examples.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Chord Lab/Debug Parse Chord Symbols")]
        public static void DebugParseChordSymbols()
        {
            // Create a test key (C Ionian)
            var key = new TheoryKey(0, Sonoria.MusicTheory.ScaleMode.Ionian);

            // Test cases
            string[] testSymbols = {
                "C",           // Major triad
                "Am",          // Minor triad
                "F#",          // Sharp root
                "Eb",          // Flat root
                "Bb",          // Flat root
                "Gm",          // Minor with flat root
                "Bdim",        // Diminished
                "C°",          // Diminished (alternative)
                "Eaug",        // Augmented
                "E+",          // Augmented (alternative)
                "C7",          // Dominant 7th
                "Cmaj7",       // Major 7th
                "CM7",         // Major 7th (alternative)
                "Cm7",         // Minor 7th
                "Am7",         // Minor 7th
                "Bm7b5",       // Half-diminished
                "Bø7",         // Half-diminished (alternative)
                "Bdim7",       // Fully diminished
                "C/E",         // Slash chord (first inversion)
                "Am/C",        // Slash chord (first inversion)
                "G7/B",        // Slash chord (first inversion)
                "Dm/F",        // Slash chord (first inversion)
                "Q7",          // Invalid (should fail)
                "Xxx"          // Invalid (should fail)
            };

            Debug.Log("=== Chord Symbol Parser Test ===");
            Debug.Log($"Key: {key}\n");

            int successCount = 0;
            int failCount = 0;

            foreach (string symbol in testSymbols)
            {
                bool success = TheoryChord.TryParseChordSymbol(key, symbol, out ChordRecipe recipe, out string errorMessage);

                if (success)
                {
                    successCount++;
                    string roman = TheoryChord.RecipeToRomanNumeral(key, recipe);
                    string rootName = TheoryPitch.GetNoteNameForDegreeWithOffset(key, recipe.Degree, recipe.RootSemitoneOffset);
                    string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootName, null);
                    
                    Debug.Log($"✓ '{symbol}' → Degree={recipe.Degree}, Offset={recipe.RootSemitoneOffset}, " +
                              $"Quality={recipe.Quality}, Extension={recipe.Extension}, " +
                              $"SeventhQuality={recipe.SeventhQuality}, Inversion={recipe.Inversion}, " +
                              $"Roman={roman}, Symbol={chordSymbol}");
                }
                else
                {
                    failCount++;
                    Debug.LogWarning($"✗ '{symbol}' → FAILED: {errorMessage}");
                }
            }

            Debug.Log($"\n=== Results: {successCount} succeeded, {failCount} failed ===");
        }

        /// <summary>
        /// Editor-only debug menu item: Tests chord symbol playback with a hard-coded progression.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Chord Lab/Debug Play Chord Symbols")]
        public static void DebugPlayChordSymbols()
        {
            // Find ChordLabController in the scene
            ChordLabController controller = UnityEngine.Object.FindFirstObjectByType<ChordLabController>();
            if (controller == null)
            {
                Debug.LogError("[ChordLab] Could not find ChordLabController in scene. Please open the LLM_Chat_Terminal scene.");
                return;
            }

            // Create a test key (C Ionian)
            var key = new TheoryKey(0, Sonoria.MusicTheory.ScaleMode.Ionian);

            // Test progression
            string testProgression = "C Am Dm G7";

            Debug.Log($"=== Testing Chord Symbol Playback ===");
            Debug.Log($"Key: {key}");
            Debug.Log($"Progression: {testProgression}\n");

            // Parse chord symbols
            bool success = controller.TryBuildChordRecipesFromChordSymbolInput(key, testProgression, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters, out string errorMessage);

            if (!success)
            {
                Debug.LogError($"[ChordLab] Failed to parse chord symbols: {errorMessage}");
                return;
            }

            Debug.Log($"✓ Successfully parsed {recipes.Count} chords:");
            if (durationsInQuarters != null && durationsInQuarters.Count > 0)
            {
                Debug.Log($"  Durations: [{string.Join(", ", durationsInQuarters)}] quarters");
            }
            for (int i = 0; i < recipes.Count; i++)
            {
                string roman = TheoryChord.RecipeToRomanNumeral(key, recipes[i]);
                string rootName = TheoryPitch.GetNoteNameForDegreeWithOffset(key, recipes[i].Degree, recipes[i].RootSemitoneOffset);
                string chordSymbol = TheoryChord.GetChordSymbol(key, recipes[i], rootName, null);
                Debug.Log($"  {i + 1}. {chordSymbol} → {roman} (Degree={recipes[i].Degree}, Quality={recipes[i].Quality})");
            }

            Debug.Log("\n[ChordLab] To test playback, add a runtime UI button or use the existing Play button with chord symbol input field.");
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only debug method that logs the voicing for the first chord in the progression.
        /// Uses the same parsing and voicing pipeline as the main system.
        /// </summary>
        public void DebugLogFirstChordVoicing()
        {
            try
            {
                // Get the current key from dropdowns
                var key = GetKeyFromDropdowns();

                // Get the progression text from the input field
                var text = progressionInput != null ? progressionInput.text : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.LogWarning("Chord Lab Voicing Debug: progression input is empty.");
                    return;
                }

                // Take only the first non-empty token (split on whitespace)
                var tokens = text.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    Debug.LogWarning("Chord Lab Voicing Debug: no tokens found in progression input.");
                    return;
                }

                var firstToken = tokens[0];

                // Parse the first Roman numeral
                if (!TheoryChord.TryParseRomanNumeral(key, firstToken, out var recipe))
                {
                    Debug.LogWarning($"Chord Lab Voicing Debug: failed to parse roman '{firstToken}' in key {key}.");
                    return;
                }

                // Build ChordEvent and voice it
                var chordEvent = new ChordEvent
                {
                    Key = key,
                    Recipe = recipe,
                    TimeBeats = 0f,
                    MelodyMidi = null
                };

                var voiced = TheoryVoicing.VoiceFirstChord(chordEvent);

                // Get chord symbol for display
                string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                    key,
                    recipe.Degree,
                    recipe.RootSemitoneOffset);
                string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootNoteName);

                // Build a human-readable log
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Voicing Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Roman: {firstToken}");
                sb.AppendLine($"Chord Symbol: {chordSymbol}");
                sb.AppendLine($"TimeBeats: {voiced.TimeBeats}");
                sb.AppendLine("Voices (low→high):");

                for (int i = 0; i < voiced.VoicesMidi.Length; i++)
                {
                    int midi = voiced.VoicesMidi[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                    sb.AppendLine($"  {i}: {midi} ({noteName})");
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Voicing Debug: Exception while logging voicing: {ex}");
            }
        }

        /// <summary>
        /// Editor-only debug method that logs the voicing for the entire progression.
        /// Uses VoiceLeadProgression to voice all chords with voice-leading.
        /// </summary>
        /// <summary>
        /// Helper method to build a test melody line for debug purposes.
        /// Uses the serialized <see cref="testMelodyDegrees"/> array from the Inspector.
        /// If the array is null or empty, falls back to the default pattern [1, 2, 3, 5, 3, 2, 1].
        /// Each degree in the array is interpreted in the current key using TheoryScale helpers,
        /// producing MelodyEvent objects with quarter note durations.
        /// </summary>
        /// <param name="key">The key context for interpreting scale degrees</param>
        /// <returns>List of MelodyEvent objects representing the test melody line</returns>
        private List<MelodyEvent> BuildTestMelodyLine(TheoryKey key)
        {
            // Use testMelodyDegrees from Inspector, with fallback to default pattern
            int[] degreesToUse;
            if (testMelodyDegrees == null || testMelodyDegrees.Length == 0)
            {
                // Fallback to original default pattern: degrees 1, 2, 3, 5, 3, 2, 1
                degreesToUse = new int[] { 1, 2, 3, 5, 3, 2, 1 };
            }
            else
            {
                degreesToUse = testMelodyDegrees;
            }

            int octave = 4; // C4 = MIDI 60

            var melody = new List<MelodyEvent>();
            for (int i = 0; i < degreesToUse.Length; i++)
            {
                int midi = TheoryScale.GetMidiForDegree(key, degreesToUse[i], octave);
                if (midi < 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"Chord Lab Melody Debug: Could not get MIDI for degree {degreesToUse[i]} in key {key}.");
                    continue;
                }

                melody.Add(new MelodyEvent
                {
                    TimeBeats = i,
                    DurationBeats = 1.0f,
                    Midi = midi
                });
            }

            return melody;
        }

        /// <summary>
        /// Builds melody events for voiced playback with piano roll priority.
        /// Priority: Piano roll (if enabled & non-empty) → note-name input → degree-based test melody.
        /// </summary>
        /// <param name="regions">Chord regions to calculate timeline length</param>
        /// <param name="timelineSpec">Timeline specification for tick calculations</param>
        /// <returns>List of MelodyEvent objects, or null/empty if no melody available</returns>
        private List<MelodyEvent> BuildMelodyEventsForVoicedPlayback(
            IReadOnlyList<ChordRegion> regions,
            TimelineSpec timelineSpec)
        {
            // Get key for note naming (used throughout this method)
            TheoryKey key = GetKeyFromDropdowns();
            
            // Calculate total timeline length in ticks
            int totalTicks = 0;
            if (regions != null && regions.Count > 0)
            {
                var last = regions[regions.Count - 1];
                totalTicks = last.startTick + last.durationTicks;
            }

            // Try piano roll melody first (if enabled)
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> eventsFromGrid = null;
            if (usePianoRollMelodyForVoicedPlayback && melodyPianoRoll != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Checking piano roll melody: usePianoRollMelodyForVoicedPlayback={usePianoRollMelodyForVoicedPlayback}, melodyPianoRoll={(melodyPianoRoll != null ? "assigned" : "null")}");
                }
                
                eventsFromGrid = melodyPianoRoll.BuildEventsFromGrid();

                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Piano roll BuildEventsFromGrid returned {eventsFromGrid?.Count ?? 0} events");
                }

                // Trim to timeline length if needed
                if (eventsFromGrid != null && eventsFromGrid.Count > 0 && totalTicks > 0)
                {
                    eventsFromGrid = TrimMelodyEventsToTimeline(eventsFromGrid, totalTicks);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ChordLab] After trimming to {totalTicks} ticks: {eventsFromGrid.Count} events remain");
                    }
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Piano-roll melody requested for voiced playback. events={eventsFromGrid?.Count ?? 0}, totalTicks={totalTicks}");
                }

                // If we have events from grid, convert to input MelodyEvent format and return
                if (eventsFromGrid != null && eventsFromGrid.Count > 0)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ChordLab] Using piano roll melody with {eventsFromGrid.Count} events");
                    }
                    
                    // Convert Timeline.MelodyEvent to input MelodyEvent format
                    var result = new List<MelodyEvent>(eventsFromGrid.Count);
                    foreach (var ev in eventsFromGrid)
                    {
                        // Convert ticks to beats (assuming 4/4 time, 1 quarter = 1 beat)
                        float timeBeats = ev.startTick / (float)timelineSpec.ticksPerQuarter;
                        float durationBeats = ev.durationTicks / (float)timelineSpec.ticksPerQuarter;
                        
                        result.Add(new MelodyEvent
                        {
                            TimeBeats = timeBeats,
                            DurationBeats = durationBeats,
                            Midi = ev.midi,
                            AccidentalHint = AccidentalHint.None
                        });
                    }
                    
                    // Mirror piano roll melody to text field with duration suffixes
                    if (Input_MelodyNoteNames != null)
                    {
                        var text = BuildNoteNameMelodyFromEventsWithDurations(eventsFromGrid, timelineSpec.ticksPerQuarter, key);
                        Input_MelodyNoteNames.text = text;

                        if (enableDebugLogs)
                            Debug.Log($"[ChordLab] SATB using piano-roll melody; mirrored {eventsFromGrid.Count} events to text field with durations: {text}");
                    }
                    else if (enableDebugLogs)
                    {
                        Debug.Log("[ChordLab] Input_MelodyNoteNames is null, skipping text field update");
                    }
                    
                    return result;
                }
                else if (enableDebugLogs)
                {
                    Debug.Log("[ChordLab] Piano roll returned empty events, falling back to text field melody");
                }
            }

            // Fallback: note-name melody input
            List<MelodyEvent> fallbackMelody = BuildNoteNameMelodyLineFromInspector();
            if (fallbackMelody != null && fallbackMelody.Count > 0)
            {
                return fallbackMelody;
            }

            // Fallback: degree-based test melody
            fallbackMelody = BuildTestMelodyLine(key);
            return fallbackMelody;
        }

        /// <summary>
        /// Builds a note-name melody string from a list of Timeline.MelodyEvents with duration suffixes.
        /// One token per onset with :N duration suffix in quarters.
        /// Format: "C4:2 F5:1 G4:3" etc.
        /// </summary>
        /// <param name="events">List of Timeline.MelodyEvents to convert</param>
        /// <param name="ticksPerQuarter">Ticks per quarter note for duration conversion</param>
        /// <param name="key">Key context for enharmonic spelling</param>
        /// <returns>Space-separated note names with octaves and duration suffixes, or empty string if events is null/empty</returns>
        private string BuildNoteNameMelodyFromEventsWithDurations(
            IReadOnlyList<Sonoria.MusicTheory.Timeline.MelodyEvent> events,
            int ticksPerQuarter,
            TheoryKey key)
        {
            if (events == null || events.Count == 0 || ticksPerQuarter <= 0)
                return string.Empty;

            var parts = new List<string>(events.Count);

            foreach (var ev in events)
            {
                // Get pitch name (without octave) using key-aware spelling
                string name = TheoryPitch.GetPitchNameFromMidi(ev.midi, key);
                
                // Calculate octave from MIDI: octave = (midi / 12) - 1
                int octave = (ev.midi / 12) - 1;
                
                // Compute duration in quarters (minimum 1 quarter)
                int quarters = Mathf.Max(1, ev.durationTicks / ticksPerQuarter);
                
                parts.Add($"{name}{octave}:{quarters}");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Builds a note-name melody string from a list of MelodyEvents (input format).
        /// One token per onset (ignores duration for now).
        /// Format: "C4 F5 G4" etc.
        /// </summary>
        /// <param name="events">List of MelodyEvents to convert</param>
        /// <returns>Space-separated note names with octaves, or empty string if events is null/empty</returns>
        private string BuildNoteNameMelodyFromEvents(IReadOnlyList<MelodyEvent> events)
        {
            if (events == null || events.Count == 0)
                return string.Empty;

            var parts = new List<string>(events.Count);

            foreach (var ev in events)
            {
                // One token per onset; ignore duration for now.
                // Get pitch name (without octave) and calculate octave from MIDI
                string name = TheoryPitch.GetPitchNameFromMidi(ev.Midi);
                int octave = (ev.Midi / 12) - 1;
                parts.Add($"{name}{octave}");
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Trims MelodyEvents to fit within [0, totalTicks) timeline.
        /// Clips events that extend past the end and removes events completely outside the timeline.
        /// </summary>
        /// <param name="source">Source list of MelodyEvents</param>
        /// <param name="totalTicks">Total timeline length in ticks</param>
        /// <returns>Trimmed list of MelodyEvents</returns>
        private static List<Sonoria.MusicTheory.Timeline.MelodyEvent> TrimMelodyEventsToTimeline(
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> source,
            int totalTicks)
        {
            if (source == null || source.Count == 0)
                return source;

            if (totalTicks <= 0)
                return source;

            var trimmed = new List<Sonoria.MusicTheory.Timeline.MelodyEvent>(source.Count);
            foreach (var ev in source)
            {
                // Ignore events completely outside the timeline
                if (ev.startTick >= totalTicks)
                    continue;
                if (ev.startTick + ev.durationTicks <= 0)
                    continue;

                // Clip event to timeline bounds
                int start = Mathf.Clamp(ev.startTick, 0, totalTicks);
                int end = Mathf.Clamp(ev.startTick + ev.durationTicks, 0, totalTicks);
                int dur = end - start;
                if (dur <= 0)
                    continue;

                trimmed.Add(new Sonoria.MusicTheory.Timeline.MelodyEvent
                {
                    midi = ev.midi,
                    startTick = start,
                    durationTicks = dur
                });
            }

            return trimmed;
        }

        /// <summary>
        /// Parses a note-name token like "F5", "C#4", "Eb3" into a MIDI note number.
        /// Returns true on success; false on failure.
        /// Expected format: Letter A–G, optional # or b, then octave integer (e.g., C4 = 60).
        /// </summary>
        private static bool TryParseNoteNameToMidi(string token, out int midi, out AccidentalHint accidentalHint)
        {
            midi = 0;
            accidentalHint = AccidentalHint.Natural;

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
                    accidentalHint = AccidentalHint.Sharp;
                    index++;
                }
                else if (acc == 'b' || acc == '♭') // Support both ASCII and Unicode flat
                {
                    accidentalOffset = -1;
                    accidentalHint = AccidentalHint.Flat;
                    index++;
                }
                else
                {
                    accidentalHint = AccidentalHint.Natural;
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

        /// <summary>
        /// Builds a one-note-per-beat melody line from the UI input field or Inspector string
        /// (space-separated note names with octaves).
        /// Returns null or empty list if parsing fails.
        /// </summary>
        private List<MelodyEvent> BuildNoteNameMelodyLineFromInspector()
        {
            // First, try to get melody from the UI input field
            string melodyInput = GetMelodyInput();
            
            // Fallback to old Inspector field if UI field is empty
            if (string.IsNullOrWhiteSpace(melodyInput))
            {
                melodyInput = testMelodyNoteNames;
            }
            
            Debug.Log($"[ChordLab] BuildNoteNameMelodyLineFromInspector called. melodyInput = '{(melodyInput ?? "NULL")}'");
            
            if (string.IsNullOrWhiteSpace(melodyInput))
            {
                Debug.Log("[ChordLab] Melody input is null or empty, returning null.");
                return null;
            }

            // Trim input to handle trailing whitespace/newlines that could cause parsing issues
            string trimmedInput = melodyInput.Trim();
            Debug.Log($"[ChordLab] Trimmed input length: {trimmedInput.Length} (original: {melodyInput.Length})");

            char[] separators = { ' ', '\t', '\n', '\r' };
            string[] tokens = trimmedInput
                .Split(separators, System.StringSplitOptions.RemoveEmptyEntries);

            Debug.Log($"[ChordLab] Split into {tokens.Length} tokens. Tokens: [{string.Join(" | ", tokens.Select((t, i) => $"#{i}:'{t}'"))}]");

            if (tokens.Length == 0)
            {
                Debug.LogWarning("[ChordLab] No tokens found after splitting melody input.");
                return null;
            }

            var melody = new List<MelodyEvent>(tokens.Length);

            // Timeline v1: Parse duration suffixes :N (quarters) and rests (_)
            // Default duration is 1 quarter note (1.0 beats)
            float defaultBeatDuration = 1.0f;
            float time = 0f; // Running time in beats (advances by each event's duration)
            int lastOctave = -1; // Track last octave for octave carry when duration suffix has no octave

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim(); // Trim each token individually
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] Processing token {i + 1}/{tokens.Length}: '{token}' (Length={token.Length})");

                    // Log character-by-character for debugging
                    if (token.Length > 0)
                    {
                        var chars = token.Select(c => $"'{c}'({(int)c})").ToArray();
                        Debug.Log($"[ChordLab] Token '{token}' characters: [{string.Join(", ", chars)}]");
                    }
                }

                // Parse duration suffix :N (quarters), e.g., "C5:2" -> note="C5", duration=2.0 beats
                string noteToken = token;
                float durationBeats = defaultBeatDuration;
                
                int colonIndex = token.IndexOf(':');
                if (colonIndex >= 0)
                {
                    noteToken = token.Substring(0, colonIndex).Trim();
                    string durationStr = token.Substring(colonIndex + 1).Trim();
                    
                    if (int.TryParse(durationStr, out int durationQuarters) && durationQuarters > 0)
                    {
                        durationBeats = durationQuarters; // 1 quarter = 1 beat
                        if (enableDebugLogs)
                            Debug.Log($"[ChordLab] Parsed duration suffix: '{durationStr}' -> {durationQuarters} quarters ({durationBeats} beats)");
                    }
                    else
                    {
                        Debug.LogWarning($"[ChordLab] Invalid duration suffix '{durationStr}' in token '{token}'. Using default {defaultBeatDuration} beats.");
                    }
                }

                // Check for rest token (_)
                if (noteToken == "_" || noteToken == "-")
                {
                    // Rest: advance time by duration without adding a note event
                    if (enableDebugLogs)
                        Debug.Log($"[ChordLab] Rest token '{token}' -> advancing time by {durationBeats} beats (time: {time} -> {time + durationBeats})");
                    time += durationBeats;
                    continue;
                }

                // Parse note name to MIDI
                // If parsing fails, check if it's a note name without octave (for duration suffix case like "C:4")
                if (!TryParseNoteNameToMidi(noteToken, out int midi, out AccidentalHint detectedAccidental))
                {
                    // Check if this token has a duration suffix but no octave (e.g., "C:4")
                    if (colonIndex >= 0)
                    {
                        // Check if noteToken is just a note name without octave (e.g., "C", "C#", "Db")
                        // Pattern: letter optionally followed by accidental, but no digit
                        bool isNoteNameWithoutOctave = false;
                        if (noteToken.Length > 0 && char.IsLetter(noteToken[0]))
                        {
                            // Check if second char is accidental (if present) and rest is letters only
                            if (noteToken.Length == 1)
                                isNoteNameWithoutOctave = true;
                            else if (noteToken.Length == 2 && (noteToken[1] == 'b' || noteToken[1] == '#' || noteToken[1] == '♭' || noteToken[1] == '♯'))
                                isNoteNameWithoutOctave = true;
                            else if (noteToken.Length > 1 && char.IsDigit(noteToken[noteToken.Length - 1]))
                                isNoteNameWithoutOctave = false; // Has digit, so has octave
                            else
                                isNoteNameWithoutOctave = false; // Unknown pattern, assume it has octave or is invalid
                        }
                        
                        if (isNoteNameWithoutOctave)
                        {
                            // Check if lastOctave is available for carry
                            if (lastOctave >= 0)
                            {
                                // Support octave carry: use last octave
                                string noteWithOctave = noteToken + lastOctave.ToString();
                                Debug.LogWarning($"[ChordLab] Token '{token}' has duration suffix but no octave. Using octave carry from previous token: '{noteWithOctave}'");
                                
                                if (TryParseNoteNameToMidi(noteWithOctave, out midi, out detectedAccidental))
                                {
                                    // Success with octave carry - continue to add event
                                }
                                else
                                {
                                    Debug.LogWarning($"[ChordLab] ⚠️ FAILED to parse token '{token}' even with octave carry '{noteWithOctave}' at position {i + 1}. Skipping.");
                                    continue;
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[ChordLab] ⚠️ Token '{token}' has duration suffix but no octave, and no previous octave available for carry. Skipping token at position {i + 1}. Melody duration tokens must include octave (e.g., C4:4, not C:4).");
                                continue;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[ChordLab] ⚠️ FAILED to parse token '{noteToken}' (from '{token}') at position {i + 1}. Skipping.");
                            continue;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ChordLab] ⚠️ FAILED to parse token '{noteToken}' at position {i + 1}. Skipping.");
                    continue; // Skip invalid tokens instead of aborting
                    }
                }
                else
                {
                    // Parsing succeeded - extract octave for potential future carry
                    // Extract octave from MIDI: midi = (octave + 1) * 12 + pitchClass
                    int pitchClass = (midi % 12 + 12) % 12;
                    lastOctave = (midi - pitchClass) / 12 - 1;
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"[ChordLab] ✓ Successfully parsed '{noteToken}' -> MIDI {midi}, AccidentalHint={detectedAccidental}, Duration={durationBeats} beats");
                }

                // Timeline v1: Create MelodyEvent with parsed duration
                var evt = new MelodyEvent
                {
                    TimeBeats = time,
                    DurationBeats = durationBeats,
                    Midi = midi,
                    AccidentalHint = detectedAccidental
                };
                melody.Add(evt);

                // Timeline v1: Advance running time by event duration (so users can align melody to chord regions)
                time += durationBeats;
                
                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Added melody event: MIDI={midi} at TimeBeats={evt.TimeBeats}, DurationBeats={durationBeats}, next time={time}");
            }

            Debug.Log($"[ChordLab] Final result: Built {melody.Count} melody events from {tokens.Length} tokens.");

            // Return null only if no valid notes were parsed
            if (melody.Count == 0)
            {
                Debug.LogWarning("[ChordLab] ⚠️ No valid notes could be parsed from melody input.");
                return null;
            }

            // INSTRUMENTATION: Log full event list after parsing
            if (enableMelodyTimelineDebug)
            {
                UnityEngine.Debug.Log($"[MELODY_PARSE] === Melody Events Built ===");
                UnityEngine.Debug.Log($"[MELODY_PARSE] Token count: {tokens.Length}, Event count: {melody.Count}");
            }
            for (int idx = 0; idx < melody.Count; idx++)
            {
                var evt = melody[idx];
                string noteName = TheoryPitch.GetPitchNameFromMidi(evt.Midi, GetKeyFromDropdowns());
                if (enableMelodyTimelineDebug)
                    UnityEngine.Debug.Log($"[MELODY_PARSE] Event #{idx}: noteName={noteName} midi={evt.Midi} TimeBeats={evt.TimeBeats:F2} DurationBeats={evt.DurationBeats:F2}");
            }
            if (melody.Count > 0)
            {
                var lastEvt = melody[melody.Count - 1];
                string lastNoteName = TheoryPitch.GetPitchNameFromMidi(lastEvt.Midi, GetKeyFromDropdowns());
                if (enableMelodyTimelineDebug)
                    UnityEngine.Debug.Log($"[MELODY_PARSE] Last event: #{melody.Count - 1}, noteName={lastNoteName}, midi={lastEvt.Midi}, TimeBeats={lastEvt.TimeBeats:F2}");
            }

            return melody;
        }

        /// <summary>
        /// Editor-only debug method: Logs melody analysis for a test melody.
        /// </summary>
        public void DebugLogTestMelodyAnalysis()
        {
            try
            {
                // 1. Get the current key from dropdowns
                var key = GetKeyFromDropdowns();

                // 2. Build test melody
                var melody = BuildTestMelodyLine(key);

                if (melody.Count == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning("Chord Lab Melody Debug: No valid melody events created.");
                    return;
                }

                // 3. Analyze the melody
                var analysis = TheoryMelody.AnalyzeMelodyLine(key, melody);

                // 4. Log a readable report
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Melody Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Total notes: {analysis.Count}");

                for (int i = 0; i < analysis.Count; i++)
                {
                    var a = analysis[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(a.Midi, key);
                    string diatonicStr = a.IsDiatonic ? "True" : "False";
                    string offsetStr = a.SemitoneOffset == 0 ? "0" : 
                        a.SemitoneOffset > 0 ? $"+{a.SemitoneOffset}" : $"{a.SemitoneOffset}";

                    sb.AppendLine($"Note {i + 1}: t={a.TimeBeats:F1} midi={a.Midi} ({noteName}) → pc={a.PitchClass}, degree={a.Degree}, offset={offsetStr}, diatonic={diatonicStr}");
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Melody Debug: Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Editor-only debug method: Logs harmony candidates for each note in the test melody.
        /// </summary>
        public void DebugLogHarmonyCandidatesForTestMelody()
        {
            try
            {
                // a) Get the current key from dropdowns
                var key = GetKeyFromDropdowns();

                // b) Build test melody using shared helper
                var melody = BuildTestMelodyLine(key);

                if (melody.Count == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning("Chord Lab Harmony Candidates Debug: No valid melody events created.");
                    return;
                }

                // c) Analyze the melody line
                var analyses = TheoryMelody.AnalyzeMelodyLine(key, melody);

                // d) Build report
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Harmony Candidates Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Total notes: {analyses.Count}");
                sb.AppendLine();

                for (int i = 0; i < analyses.Count; i++)
                {
                    var analysis = analyses[i];
                    var melodyEvent = i < melody.Count ? melody[i] : default;
                    string noteName = TheoryPitch.GetPitchNameFromMidi(analysis.Midi, key);
                    string diatonicStr = analysis.IsDiatonic ? "True" : "False";
                    
                    sb.AppendLine($"Note {i + 1}: t={analysis.TimeBeats:F1} midi={analysis.Midi} ({noteName}) → degree={analysis.Degree}, diatonic={diatonicStr}");

                    // Get chord candidates for this note (pass accidental hint from melody event)
                    var candidates = TheoryHarmonization.GetChordCandidatesForMelodyNote(key, analysis, melodyEvent.AccidentalHint);
                    
                    if (candidates.Count > 0)
                    {
                        sb.AppendLine("  Candidates:");
                        foreach (var candidate in candidates)
                        {
                            sb.AppendLine($"    - {candidate.Roman} ({candidate.ChordSymbol})  [{candidate.Reason}]");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  Candidates: (none - note is non-diatonic or key is not Ionian)");
                    }
                    
                    sb.AppendLine();
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Harmony Candidates Debug: Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Editor-only debug method: Logs naive harmonization for the test melody.
        /// </summary>
        public void DebugLogNaiveHarmonizationForTestMelody()
        {
            try
            {
                // Get the current key from dropdowns
                var key = GetKeyFromDropdowns();

                // Build test melody using shared helper
                var melody = BuildTestMelodyLine(key);

                if (melody.Count == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning("Chord Lab Naive Harmonization Debug: No valid melody events created.");
                    return;
                }

                // Build settings from Inspector fields
                var settings = BuildHarmonyHeuristicSettings();

                // Build naive harmonization
                var harmonization = TheoryHarmonization.BuildNaiveHarmonization(melody, key, settings);

                // Build report
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Naive Harmonization Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Settings: PreferTonicStart={settings.PreferTonicStart}, PreferChordContinuity={settings.PreferChordContinuity}, EnableDetailedReasonLogs={settings.EnableDetailedReasonLogs}");
                sb.AppendLine($"Total steps: {harmonization.Count}");
                sb.AppendLine();

                for (int i = 0; i < harmonization.Count; i++)
                {
                    var step = harmonization[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(step.MelodyEvent.Midi, key);
                    string diatonicStr = step.MelodyAnalysis.IsDiatonic ? "diatonic" : "non-diatonic";
                    
                    sb.AppendLine($"Step {i + 1}: t={step.MelodyEvent.TimeBeats:F1} midi={step.MelodyEvent.Midi} ({noteName}) → degree={step.MelodyAnalysis.Degree}, {diatonicStr}");

                    if (step.Candidates != null && step.Candidates.Count > 0)
                    {
                        sb.AppendLine("  Candidates:");
                        foreach (var candidate in step.Candidates)
                        {
                            sb.AppendLine($"    - {candidate.Roman} ({candidate.ChordSymbol})  [{candidate.Reason}]");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  Candidates: (none)");
                    }

                    if (step.ChosenChord.Roman != null)
                    {
                        sb.AppendLine($"  Chosen: {step.ChosenChord.Roman} ({step.ChosenChord.ChordSymbol})");
                    }
                    else
                    {
                        sb.AppendLine("  Chosen: (none)");
                    }

                    sb.AppendLine($"  Reason: {step.Reason}");
                    sb.AppendLine();
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Naive Harmonization Debug: Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Builds the test melody, runs naive harmonization, converts it
        /// to a HarmonizationSnapshot, and logs the JSON to the Console.
        /// </summary>
        public void DebugLogNaiveHarmonizationSnapshotJson()
        {
#if UNITY_EDITOR
            try
            {
                var key = GetKeyFromDropdowns();
                var melodyLine = BuildTestMelodyLine(key);
                if (melodyLine == null || melodyLine.Count == 0)
                {
                    Debug.LogWarning("[ChordLab] No test melody available for harmonization snapshot.");
                    return;
                }

                var settings = BuildHarmonyHeuristicSettings();

                var steps = TheoryHarmonization.BuildNaiveHarmonization(melodyLine, key, settings);

                var snapshot = TheoryHarmonization.BuildSnapshotFromHarmonization(
                    key,
                    "Chord Lab test melody naive harmonization",
                    steps);

                var json = JsonUtility.ToJson(snapshot, true);
                Debug.Log("[ChordLab] Harmonization Snapshot JSON:\n" + json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Harmonization Snapshot Debug: Exception: {ex.Message}\n{ex.StackTrace}");
            }
#endif
        }

        /// <summary>
        /// Computes scale degree and chromatic offset for a melody note.
        /// Returns degree (1-7), chromatic offset (-2..+2), and degree label (e.g., "5", "b6", "#4").
        /// </summary>
        private static void ComputeScaleDegreeAndOffset(TheoryKey key, int midi, out int degree, out int chromaticOffset, out string degreeLabel)
        {
            // Use existing TheoryMelody.AnalyzeEvent which already does this computation
            var melodyEvent = new MelodyEvent { Midi = midi, TimeBeats = 0f, DurationBeats = 1f };
            var analysis = TheoryMelody.AnalyzeEvent(key, melodyEvent);
            
            degree = analysis.Degree;
            chromaticOffset = analysis.SemitoneOffset;
            
            // Clamp offset to practical range (-2..+2)
            chromaticOffset = Mathf.Clamp(chromaticOffset, -2, 2);
            
            // Build degree label: accidentals + Arabic numeral
            string accidentals = "";
            if (chromaticOffset > 0)
            {
                accidentals = new string('#', chromaticOffset);
            }
            else if (chromaticOffset < 0)
            {
                accidentals = new string('b', -chromaticOffset);
            }
            
            degreeLabel = $"{accidentals}{degree}";
        }

        /// <summary>
        /// Computes root degree and chromatic offset for a chord root.
        /// Returns degree (1-7), chromatic offset (-2..+2), and root degree label (e.g., "V", "bVI", "#iv").
        /// </summary>
        private static void ComputeRootDegreeAndOffset(TheoryKey key, int rootPitchClass, ChordRecipe recipe, out int degree, out int chromaticOffset, out string rootDegreeLabel)
        {
            // Get diatonic pitch classes for all degrees
            var degreeToPc = new Dictionary<int, int>();
            for (int deg = 1; deg <= 7; deg++)
            {
                int diatonicPc = TheoryScale.GetDegreePitchClass(key, deg);
                if (diatonicPc >= 0)
                {
                    degreeToPc[deg] = diatonicPc % 12;
                }
            }
            
            // Find best-fit degree by comparing semitone differences
            int bestDegree = recipe.Degree; // Start with recipe's degree
            int bestDiff = recipe.RootSemitoneOffset; // Use recipe's offset as initial diff
            
            // Verify by comparing actual root pitch class to diatonic degrees
            // Find the closest diatonic degree to the actual root pitch class
            int bestAbsDiff = Mathf.Abs(bestDiff);
            foreach (var kvp in degreeToPc)
            {
                int deg = kvp.Key;
                int diatonicPc = kvp.Value;
                
                // Compute semitone difference (mod 12, then normalize to -6..+6)
                int diff = (rootPitchClass - diatonicPc + 12) % 12;
                if (diff > 6) diff -= 12;
                
                // Choose degree with smallest absolute difference
                int absDiff = Mathf.Abs(diff);
                if (absDiff < bestAbsDiff)
                {
                    bestDegree = deg;
                    bestDiff = diff;
                    bestAbsDiff = absDiff;
                }
            }
            
            // Clamp offset to practical range (-2..+2)
            chromaticOffset = Mathf.Clamp(bestDiff, -2, 2);
            degree = bestDegree;
            
            // Build root degree label: accidentals + Roman numeral
            string accidentals = "";
            if (chromaticOffset > 0)
            {
                accidentals = new string('#', chromaticOffset);
            }
            else if (chromaticOffset < 0)
            {
                accidentals = new string('b', -chromaticOffset);
            }
            
            // Convert degree to Roman numeral (use case from recipe quality)
            string degreeRoman = DegreeToRomanNumeral(degree, recipe);
            
            rootDegreeLabel = $"{accidentals}{degreeRoman}";
        }
        
        /// <summary>
        /// Converts a degree (1-7) to a Roman numeral, using the case convention based on chord quality.
        /// Major/Augmented → uppercase, Minor/Diminished → lowercase.
        /// </summary>
        private static string DegreeToRomanNumeral(int degree, ChordRecipe recipe)
        {
            // Map degree to base Roman numeral
            string baseRoman = degree switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                6 => "VI",
                7 => "VII",
                _ => "I"
            };
            
            // Apply case based on chord quality (matching RecipeToRomanNumeral logic)
            return recipe.Quality switch
            {
                ChordQuality.Major => baseRoman,
                ChordQuality.Minor => baseRoman.ToLowerInvariant(),
                ChordQuality.Diminished => baseRoman.ToLowerInvariant(),
                ChordQuality.Augmented => baseRoman,
                _ => baseRoman
            };
        }
        
        /// <summary>
        /// Determines if a melody note is a chord tone by checking if its pitch class
        /// matches any of the chord's tone pitch classes.
        /// </summary>
        private static bool IsMelodyNoteChordTone(int melodyMidi, ChordEvent chordEvent)
        {
            int melodyPc = TheoryPitch.PitchClassFromMidi(melodyMidi);
            
            // Get chord tone pitch classes
            int rootPc = TheoryScale.GetDegreePitchClass(chordEvent.Key, chordEvent.Recipe.Degree);
            if (rootPc < 0) rootPc = 0;
            rootPc = (rootPc + chordEvent.Recipe.RootSemitoneOffset + 12) % 12;
            if (rootPc < 0) rootPc += 12;
            
            // Calculate third and fifth based on quality
            int thirdPc, fifthPc;
            switch (chordEvent.Recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdPc = (rootPc + 4) % 12;
                    fifthPc = (rootPc + 7) % 12;
                    break;
                case ChordQuality.Minor:
                    thirdPc = (rootPc + 3) % 12;
                    fifthPc = (rootPc + 7) % 12;
                    break;
                case ChordQuality.Diminished:
                    thirdPc = (rootPc + 3) % 12;
                    fifthPc = (rootPc + 6) % 12;
                    break;
                case ChordQuality.Augmented:
                    thirdPc = (rootPc + 4) % 12;
                    fifthPc = (rootPc + 8) % 12;
                    break;
                default:
                    return false;
            }
            
            // Check if melody pitch class matches root, third, or fifth
            if (melodyPc == rootPc || melodyPc == thirdPc || melodyPc == fifthPc)
            {
                return true;
            }
            
            // Check for 7th if present
            if (chordEvent.Recipe.Extension == ChordExtension.Seventh && 
                chordEvent.Recipe.SeventhQuality != SeventhQuality.None)
            {
                int seventhInterval = GetSeventhInterval(chordEvent.Recipe.SeventhQuality);
                int seventhPc = (rootPc + seventhInterval) % 12;
                if (melodyPc == seventhPc)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the semitone interval for a seventh quality.
        /// </summary>
        private static int GetSeventhInterval(SeventhQuality quality)
        {
            return quality switch
            {
                SeventhQuality.Major7 => 11,
                SeventhQuality.Minor7 => 10,
                SeventhQuality.Diminished7 => 9,
                SeventhQuality.HalfDiminished7 => 10,
                _ => 10 // Default to minor 7th
            };
        }

        /// <summary>
        /// Normalizes chromatic function info to user-friendly format.
        /// Examples: "from || minor" → "bVI (borrowed from minor)", "sec. to IV" → "V/V"
        /// </summary>
        private static string NormalizeChromaticFunction(string functionInfo, ChordFunctionProfile profile, string roman)
        {
            if (string.IsNullOrEmpty(functionInfo))
                return "";
            
            // Handle secondary dominant: "sec. to IV" → "V/IV" format
            if (profile.FunctionTag == ChordFunctionTag.SecondaryDominant && profile.SecondaryTargetDegree.HasValue)
            {
                // Convert target degree to Roman numeral
                int targetDegree = profile.SecondaryTargetDegree.Value;
                string targetRoman = targetDegree switch
                {
                    1 => "I",
                    2 => "II",
                    3 => "III",
                    4 => "IV",
                    5 => "V",
                    6 => "VI",
                    7 => "VII",
                    _ => targetDegree.ToString()
                };
                // Format as "V/{targetRoman}" where V represents the dominant function
                return $"V/{targetRoman}";
            }
            // Handle Neapolitan: "Neapolitan" → "bII (Neapolitan)"
            else if (profile.FunctionTag == ChordFunctionTag.Neapolitan)
            {
                return $"{roman} (Neapolitan)";
            }
            // Handle borrowed chords: "from || minor" → "bVI (borrowed from minor)"
            else if (functionInfo.Contains("from || minor"))
            {
                return $"{roman} (borrowed from minor)";
            }
            else if (functionInfo.Contains("from || major"))
            {
                return $"{roman} (borrowed from major)";
            }
            else if (functionInfo.Contains("borrowed"))
            {
                // Extract the mode names from "borrowed ∥ Dorian/Mixolydian"
                string modeInfo = functionInfo.Replace("borrowed ∥ ", "").Replace("borrowed || ", "").Trim();
                return $"{roman} (borrowed from {modeInfo})";
            }
            
            // Default: format as "Roman (function info)"
            return $"{roman} ({functionInfo})";
        }

        /// <summary>
        /// Returns a user-friendly triad quality string from chord recipe data.
        /// </summary>
        private static string GetTriadQualityString(ChordRecipe recipe)
        {
            return recipe.Quality switch
            {
                ChordQuality.Major => "Major",
                ChordQuality.Minor => "Minor",
                ChordQuality.Diminished => "Diminished",
                ChordQuality.Augmented => "Augmented",
                _ => ""
            };
        }

        /// <summary>
        /// Detects whether the chord should be treated as a power chord (root + fifth only, no third).
        /// Best-effort detection based on chord symbol or recipe analysis.
        /// </summary>
        private static bool DetectIsPowerChord(ChordRecipe recipe, ChordFunctionProfile profile, string chordSymbol)
        {
            // Check chord symbol for power chord indicator (ends with "5")
            if (!string.IsNullOrEmpty(chordSymbol))
            {
                string symbolUpper = chordSymbol.ToUpperInvariant();
                // Power chords typically end with "5" (e.g., "C5", "G5")
                // But avoid false positives like "M7B5" (half-diminished) or "M5" (major 5th)
                if (symbolUpper.EndsWith("5") && 
                    !symbolUpper.EndsWith("M5") && 
                    !symbolUpper.EndsWith("M7B5") &&
                    !symbolUpper.EndsWith("7B5") &&
                    !symbolUpper.EndsWith("B5"))
                {
                    return true;
                }
            }
            
            // Future: Could also check if recipe/profile indicates only root and fifth are present
            // For now, rely on chord symbol detection
            
            return false;
        }

        /// <summary>
        /// Detects suspension type (sus2, sus4) from chord symbol or recipe.
        /// </summary>
        private static string DetectSuspension(ChordRecipe recipe, string chordSymbol)
        {
            if (!string.IsNullOrEmpty(chordSymbol))
            {
                string symbolUpper = chordSymbol.ToUpperInvariant();
                
                // Check for sus2 (more specific first)
                if (symbolUpper.Contains("SUS2"))
                {
                    return "sus2";
                }
                
                // Check for sus4 (more specific first)
                if (symbolUpper.Contains("SUS4"))
                {
                    return "sus4";
                }
                
                // Generic "sus" defaults to sus4
                if (symbolUpper.Contains("SUS"))
                {
                    return "sus4";
                }
            }
            
            // Future: Could also analyze recipe/profile to detect if 2nd or 4th replaces 3rd
            
            return "";
        }

        /// <summary>
        /// Detects seventh presence and type from recipe and chord symbol.
        /// </summary>
        private static void DetectSeventhType(ChordRecipe recipe, string chordSymbol, out bool hasSeventh, out string seventhType)
        {
            hasSeventh = false;
            seventhType = "";
            
            // First, check recipe (preferred source)
            if (recipe.Extension == ChordExtension.Seventh && recipe.SeventhQuality != SeventhQuality.None)
            {
                hasSeventh = true;
                
                // Map SeventhQuality enum to string representation
                seventhType = recipe.SeventhQuality switch
                {
                    SeventhQuality.Dominant7 => "Dominant7",
                    SeventhQuality.Major7 => "Major7",
                    SeventhQuality.Minor7 => "Minor7",
                    SeventhQuality.HalfDiminished7 => "HalfDiminished7",
                    SeventhQuality.Diminished7 => "Diminished7",
                    _ => ""
                };
                
                return; // Recipe is authoritative
            }
            
            // Fallback: parse chord symbol if recipe doesn't have 7th info
            if (!string.IsNullOrEmpty(chordSymbol))
            {
                string symbolUpper = chordSymbol.ToUpperInvariant();
                
                // Check if chord symbol contains 7th indicators
                if (symbolUpper.Contains("7") || symbolUpper.Contains("Δ") || symbolUpper.Contains("MAJ7") || symbolUpper.Contains("DIM7"))
                {
                    hasSeventh = true;
                    
                    // Try to determine type from symbol
                    // Order matters: check more specific patterns first
                    
                    // Major 7th: maj7, Δ7, Δ, maj
                    if (symbolUpper.Contains("MAJ7") || symbolUpper.Contains("Δ"))
                    {
                        seventhType = "Major7";
                    }
                    // Half-diminished: m7b5, ø7, dim7b5
                    else if (symbolUpper.Contains("M7B5") || symbolUpper.Contains("B5") || symbolUpper.Contains("Ø7"))
                    {
                        seventhType = "HalfDiminished7";
                    }
                    // Diminished 7th: dim7, o7
                    else if (symbolUpper.Contains("DIM7") || symbolUpper.Contains("O7"))
                    {
                        seventhType = "Diminished7";
                    }
                    // Minor 7th: m7 (but check it's not maj7)
                    else if (symbolUpper.Contains("M7") && !symbolUpper.Contains("MAJ"))
                    {
                        // "m7" could be minor7 or maj7 - need context
                        // If symbol has "M" or "MIN" explicitly, it's minor7
                        // Otherwise, check for major7 indicators first
                        if (symbolUpper.Contains("MIN7"))
                        {
                            seventhType = "Minor7";
                        }
                        else
                        {
                            // Ambiguous - default to minor7 for "m7" pattern
                            seventhType = "Minor7";
                        }
                    }
                    else
                    {
                        // Default: dominant 7th (most common for just "7")
                        seventhType = "Dominant7";
                    }
                }
            }
        }

        /// <summary>
        /// Builds a JSON string representing the currently voiced harmonization.
        /// Uses the last stored voiced state from the most recent voiced playback.
        /// Returns empty string if no voiced state is available.
        /// </summary>
        private string BuildCurrentVoicedHarmonizationJson()
        {
            // Check if we have stored voiced state
            if (lastVoicedChordEvents == null || lastVoicedChordEvents.Count == 0 ||
                lastVoicedChords == null || lastVoicedChords.Count == 0)
            {
                return string.Empty;
            }
            
            // Melody line is optional (may be null or empty for chord-only progressions)
            // We'll handle that gracefully in the step building loop

            if (lastVoicedChordEvents.Count != lastVoicedChords.Count)
            {
                Debug.LogWarning("[ChordLab] Mismatch between chord events and voiced chords count. Cannot export.");
                return string.Empty;
            }

            // Build the snapshot
            // Get key name: construct MIDI note from tonic (use octave 4 = 60 + tonicPc)
            int tonicMidi = 60 + lastVoicedKey.TonicPitchClass;
            string keyName = TheoryPitch.GetPitchNameFromMidi(tonicMidi, lastVoicedKey);
            
            var snapshot = new VoicedHarmonizationSnapshot
            {
                Key = keyName,
                Mode = GetModeDisplayName(lastVoicedKey.Mode),
                Description = "Voiced harmonization from Chord Lab",
                Steps = new List<VoicedStepSnapshot>()
            };

            // Build steps - one per chord event
            for (int i = 0; i < lastVoicedChordEvents.Count; i++)
            {
                var chordEvent = lastVoicedChordEvents[i];
                var voicedChord = lastVoicedChords[i];

                // Get melody info - find matching melody event by TimeBeats or use index
                MelodyNoteSnapshot melodySnapshot = null;
                if (lastVoicedMelodyLine != null && i < lastVoicedMelodyLine.Count)
                {
                    var melodyEvent = lastVoicedMelodyLine[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.Midi, lastVoicedKey);
                    int octave = (melodyEvent.Midi / 12) - 1;
                    
                    // Compute scale degree and chromatic offset
                    ComputeScaleDegreeAndOffset(lastVoicedKey, melodyEvent.Midi, out int degree, out int offset, out string degreeLabel);
                    
                    // Check if melody is a chord tone
                    bool isChordTone = IsMelodyNoteChordTone(melodyEvent.Midi, chordEvent);
                    
                    melodySnapshot = new MelodyNoteSnapshot
                    {
                        Midi = melodyEvent.Midi,
                        NoteName = $"{noteName}{octave}",
                        ScaleDegree = degree,
                        ChromaticOffset = offset,
                        DegreeLabel = degreeLabel,
                        IsChordTone = isChordTone
                    };
                }
                else if (chordEvent.MelodyMidi.HasValue)
                {
                    // Fallback: use melody from chord event
                    string noteName = TheoryPitch.GetPitchNameFromMidi(chordEvent.MelodyMidi.Value, lastVoicedKey);
                    int octave = (chordEvent.MelodyMidi.Value / 12) - 1;
                    
                    // Compute scale degree and chromatic offset
                    ComputeScaleDegreeAndOffset(lastVoicedKey, chordEvent.MelodyMidi.Value, out int degree, out int offset, out string degreeLabel);
                    
                    // Check if melody is a chord tone
                    bool isChordTone = IsMelodyNoteChordTone(chordEvent.MelodyMidi.Value, chordEvent);
                    
                    melodySnapshot = new MelodyNoteSnapshot
                    {
                        Midi = chordEvent.MelodyMidi.Value,
                        NoteName = $"{noteName}{octave}",
                        ScaleDegree = degree,
                        ChromaticOffset = offset,
                        DegreeLabel = degreeLabel,
                        IsChordTone = isChordTone
                    };
                }

                // Get chord info
                string roman = TheoryChord.RecipeToRomanNumeral(lastVoicedKey, chordEvent.Recipe);
                int rootMidi = TheoryScale.GetMidiForDegree(lastVoicedKey, chordEvent.Recipe.Degree, 4);
                string rootName = rootMidi >= 0 ? TheoryPitch.GetPitchNameFromMidi(rootMidi, lastVoicedKey) : "?";
                string chordSymbol = TheoryChord.GetChordSymbol(lastVoicedKey, chordEvent.Recipe, rootName, null);

                // Compute root pitch class (accounting for RootSemitoneOffset)
                int rootPc = TheoryScale.GetDegreePitchClass(lastVoicedKey, chordEvent.Recipe.Degree);
                if (rootPc < 0) rootPc = 0;
                rootPc = (rootPc + chordEvent.Recipe.RootSemitoneOffset + 12) % 12;
                if (rootPc < 0) rootPc += 12;
                
                // Compute root degree and offset
                ComputeRootDegreeAndOffset(lastVoicedKey, rootPc, chordEvent.Recipe, out int rootDegree, out int rootOffset, out string rootDegreeLabel);
                
                // Analyze chord profile for diatonic status and function
                var profile = TheoryChord.AnalyzeChordProfile(lastVoicedKey, chordEvent.Recipe);
                bool isDiatonic = profile.DiatonicStatus == ChordDiatonicStatus.Diatonic;
                
                // Build chromatic function string
                string chromaticFunction = "";
                if (!isDiatonic)
                {
                    string functionInfo = TheoryChord.BuildNonDiatonicInfo(profile);
                    if (!string.IsNullOrEmpty(functionInfo))
                    {
                        // Normalize function info to user-friendly format
                        string normalizedFunction = NormalizeChromaticFunction(functionInfo, profile, roman);
                        if (!string.IsNullOrEmpty(normalizedFunction))
                        {
                            chromaticFunction = normalizedFunction;
                        }
                    }
                }

                // Get triad quality string
                string triadQuality = GetTriadQualityString(chordEvent.Recipe);
                
                // Detect power chord
                bool isPowerChord = DetectIsPowerChord(chordEvent.Recipe, profile, chordSymbol);
                
                // Detect suspension
                string suspension = DetectSuspension(chordEvent.Recipe, chordSymbol);
                
                // OmitsThird: true if sus or power chord
                bool omitsThird = isPowerChord || !string.IsNullOrEmpty(suspension);
                
                // Detect seventh type
                DetectSeventhType(chordEvent.Recipe, chordSymbol, out bool hasSeventh, out string seventhType);
                
                var chordSnapshot = new ChordSnapshot
                {
                    Roman = roman,
                    ChordSymbol = chordSymbol,
                    Quality = chordEvent.Recipe.Quality.ToString(), // Existing field for backward compatibility
                    RootDegree = rootDegree,
                    RootChromaticOffset = rootOffset,
                    RootDegreeLabel = rootDegreeLabel,
                    IsDiatonic = isDiatonic,
                    ChromaticFunction = chromaticFunction,
                    
                    // New fields for detailed chord analysis
                    TriadQuality = triadQuality,
                    IsPowerChord = isPowerChord,
                    Suspension = suspension,
                    OmitsThird = omitsThird,
                    HasSeventh = hasSeventh,
                    SeventhType = seventhType
                };

                // Get voicing info - use canonical spelling like VoicingViewer
                var voicingSnapshot = new VoicingSnapshot();
                if (voicedChord.VoicesMidi != null && voicedChord.VoicesMidi.Length >= 4)
                {
                    // SATB ordering: bass, tenor, alto, soprano (low to high)
                    voicingSnapshot.BassMidi = voicedChord.VoicesMidi[0];
                    voicingSnapshot.TenorMidi = voicedChord.VoicesMidi.Length > 1 ? voicedChord.VoicesMidi[1] : -1;
                    voicingSnapshot.AltoMidi = voicedChord.VoicesMidi.Length > 2 ? voicedChord.VoicesMidi[2] : -1;
                    voicingSnapshot.SopranoMidi = voicedChord.VoicesMidi.Length > 3 ? voicedChord.VoicesMidi[3] : -1;

                    // Get note names using VoicingViewer's canonical spelling logic
                    voicingSnapshot.BassNote = GetNoteNameWithOctave(voicingSnapshot.BassMidi, lastVoicedKey, chordEvent);
                    voicingSnapshot.TenorNote = voicingSnapshot.TenorMidi >= 0 ? GetNoteNameWithOctave(voicingSnapshot.TenorMidi, lastVoicedKey, chordEvent) : "";
                    voicingSnapshot.AltoNote = voicingSnapshot.AltoMidi >= 0 ? GetNoteNameWithOctave(voicingSnapshot.AltoMidi, lastVoicedKey, chordEvent) : "";
                    voicingSnapshot.SopranoNote = voicingSnapshot.SopranoMidi >= 0 ? GetNoteNameWithOctave(voicingSnapshot.SopranoMidi, lastVoicedKey, chordEvent) : "";
                }
                else if (voicedChord.VoicesMidi != null && voicedChord.VoicesMidi.Length > 0)
                {
                    // Handle 3-voice chords (no alto)
                    voicingSnapshot.BassMidi = voicedChord.VoicesMidi[0];
                    voicingSnapshot.TenorMidi = voicedChord.VoicesMidi.Length > 1 ? voicedChord.VoicesMidi[1] : -1;
                    voicingSnapshot.AltoMidi = -1;
                    voicingSnapshot.SopranoMidi = voicedChord.VoicesMidi.Length > 2 ? voicedChord.VoicesMidi[2] : -1;

                    voicingSnapshot.BassNote = GetNoteNameWithOctave(voicingSnapshot.BassMidi, lastVoicedKey, chordEvent);
                    voicingSnapshot.TenorNote = voicingSnapshot.TenorMidi >= 0 ? GetNoteNameWithOctave(voicingSnapshot.TenorMidi, lastVoicedKey, chordEvent) : "";
                    voicingSnapshot.AltoNote = "";
                    voicingSnapshot.SopranoNote = voicingSnapshot.SopranoMidi >= 0 ? GetNoteNameWithOctave(voicingSnapshot.SopranoMidi, lastVoicedKey, chordEvent) : "";
                }

                var stepSnapshot = new VoicedStepSnapshot
                {
                    TimeBeats = chordEvent.TimeBeats,
                    Melody = melodySnapshot,
                    Chord = chordSnapshot,
                    Voicing = voicingSnapshot
                };

                snapshot.Steps.Add(stepSnapshot);
            }

            // Serialize to JSON
            string json = JsonUtility.ToJson(snapshot, true);
            return json;
        }

        /// <summary>
        /// Gets a note name with octave (e.g., "G5") using canonical spelling for chord tones
        /// when available, matching VoicingViewer's display logic.
        /// </summary>
        private static string GetNoteNameWithOctave(int midi, TheoryKey key, ChordEvent? chordEvent)
        {
            if (midi < 0)
                return "";

            // Use canonical spelling logic similar to VoicingViewer
            string noteName;
            if (chordEvent.HasValue)
            {
                var chord = chordEvent.Value;
                int notePc = TheoryPitch.PitchClassFromMidi(midi);

                // Compute root pitch class from chord recipe
                int rootPc = TheoryScale.GetDegreePitchClass(chord.Key, chord.Recipe.Degree);
                if (rootPc < 0)
                    rootPc = 0; // Fallback to C
                rootPc = (rootPc + chord.Recipe.RootSemitoneOffset + 12) % 12;
                if (rootPc < 0)
                    rootPc += 12;

                // Get canonical triad spelling using key's accidental preference
                string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, chord.Recipe.Quality, key, chord.Recipe.RootSemitoneOffset);
                if (triadNames != null && triadNames.Length >= 3)
                {
                    // Compute expected pitch classes for triad tones
                    int thirdPc, fifthPc;
                    switch (chord.Recipe.Quality)
                    {
                        case ChordQuality.Major:
                            thirdPc = (rootPc + 4) % 12;
                            fifthPc = (rootPc + 7) % 12;
                            break;
                        case ChordQuality.Minor:
                            thirdPc = (rootPc + 3) % 12;
                            fifthPc = (rootPc + 7) % 12;
                            break;
                        case ChordQuality.Diminished:
                            thirdPc = (rootPc + 3) % 12;
                            fifthPc = (rootPc + 6) % 12;
                            break;
                        case ChordQuality.Augmented:
                            thirdPc = (rootPc + 4) % 12;
                            fifthPc = (rootPc + 8) % 12;
                            break;
                        default:
                            noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                            int defaultOctave = (midi / 12) - 1;
                            return $"{noteName}{defaultOctave}";
                    }

                    // Match to triad tone and use canonical name
                    if (notePc == rootPc)
                        noteName = triadNames[0];
                    else if (notePc == thirdPc)
                        noteName = triadNames[1];
                    else if (notePc == fifthPc)
                        noteName = triadNames[2];
                    else
                        noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                }
                else
                {
                    // No canonical spelling available - use key-based spelling
                    noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                }
            }
            else
            {
                noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
            }

            int octave = (midi / 12) - 1;
            return $"{noteName}{octave}";
        }

        /// <summary>
        /// Core path: builds the test melody, runs naive harmonization,
        /// voices it, and starts the playback coroutine. No editor-only code here
        /// so it can be reused by runtime UI as well.
        /// </summary>
        private void PlayNaiveHarmonizationForCurrentTestMelody()
        {
            UnityEngine.Debug.Log("[ENTRY_POINT] PlayNaiveHarmonizationForCurrentTestMelody called (NH mode)");
            if (enableDebugLogs)
                Debug.Log("[ChordLab] PlayNaiveHarmonizationForCurrentTestMelody started.");

            // 1. Build the test melody line
            var key = GetKeyFromDropdowns();
            
            // Try note-name melody from Inspector first
            List<MelodyEvent> melodyLine = BuildNoteNameMelodyLineFromInspector();

            // Fallback: existing degree-based test melody
            if (melodyLine == null || melodyLine.Count == 0)
            {
                Debug.LogWarning($"[ChordLab] ⚠️ Note-name melody build returned {(melodyLine == null ? "NULL" : "EMPTY")}. Falling back to degree-based test melody.");
                melodyLine = BuildTestMelodyLine(key);
            }
            else
            {
                Debug.Log($"[ChordLab] ✓ Using note-name melody with {melodyLine.Count} events.");
            }

            if (melodyLine == null || melodyLine.Count == 0)
            {
                Debug.LogWarning("[ChordLab] No test melody available for naive harmonization playback.");
                return;
            }

            Debug.Log($"[ChordLab] Using melody with {melodyLine.Count} events for harmonization.");

            // 2. Build heuristic settings from Inspector
            var settings = BuildHarmonyHeuristicSettings();

            // 3. Build naive harmonization steps
            var steps = TheoryHarmonization.BuildNaiveHarmonization(melodyLine, key, settings);
            Debug.Log($"[ChordLab] BuildNaiveHarmonization returned {steps?.Count ?? 0} steps from {melodyLine.Count} melody events.");

            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Naive harmonization produced no steps; nothing to play.");
                return;
            }

            // Log each step to see what happened
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                string noteName = TheoryPitch.GetPitchNameFromMidi(step.MelodyEvent.Midi, key);
                bool hasChord = step.ChosenChord.Roman != null && step.ChosenChord.Roman != "";
                Debug.Log($"[ChordLab] Step {i + 1}: Melody={noteName} (MIDI {step.MelodyEvent.Midi}), " +
                          $"Degree={step.MelodyAnalysis.Degree}, Diatonic={step.MelodyAnalysis.IsDiatonic}, " +
                          $"HasChord={hasChord}, Chord={step.ChosenChord.Roman}, Reason={step.Reason}");
            }

            // 4. Convert steps to chord events with melody
            var chordEvents = TheoryHarmonization.BuildChordEventsFromHarmonization(steps, key);
            Debug.Log($"[ChordLab] BuildChordEventsFromHarmonization returned {chordEvents?.Count ?? 0} chord events from {steps.Count} steps.");
            
            // Apply melody octave offset to chord events (only affects playback register, not theory)
            if (chordEvents != null && MelodyOffsetSemitones != 0)
            {
                for (int i = 0; i < chordEvents.Count; i++)
                {
                    if (chordEvents[i].MelodyMidi.HasValue)
                    {
                        int originalMidi = chordEvents[i].MelodyMidi.Value;
                        int offsetMidi = originalMidi + MelodyOffsetSemitones;
                        chordEvents[i] = new ChordEvent
                        {
                            Key = chordEvents[i].Key,
                            Recipe = chordEvents[i].Recipe,
                            TimeBeats = chordEvents[i].TimeBeats,
                            MelodyMidi = offsetMidi
                        };
                    }
                }
            }

            if (chordEvents == null || chordEvents.Count == 0)
            {
                Debug.LogWarning("[ChordLab] No chord events constructed from naive harmonization; nothing to play.");
                return;
            }

            // Log chord events to see what made it through
            for (int i = 0; i < chordEvents.Count; i++)
            {
                var evt = chordEvents[i];
                string melodyNoteName = evt.MelodyMidi.HasValue 
                    ? TheoryPitch.GetPitchNameFromMidi(evt.MelodyMidi.Value, evt.Key)
                    : "(none)";
                
                // Get root MIDI from recipe degree for chord symbol
                int rootMidi = TheoryScale.GetMidiForDegree(evt.Key, evt.Recipe.Degree, 4); // Use octave 4
                string rootName = rootMidi >= 0 ? TheoryPitch.GetPitchNameFromMidi(rootMidi, evt.Key) : "?";
                string chordSymbol = TheoryChord.GetChordSymbol(evt.Key, evt.Recipe, rootName, null);
                
                Debug.Log($"[ChordLab] ChordEvent {i + 1}: Melody={melodyNoteName} (MIDI {evt.MelodyMidi?.ToString() ?? "null"}), " +
                          $"Chord={chordSymbol} (Degree={evt.Recipe.Degree}), TimeBeats={evt.TimeBeats}");
            }

            // 5. Build ChordRegion[] from chordEvents with timeline information
            var regions = new List<ChordRegion>(chordEvents.Count);
            int cumulativeStartTick = 0;
            for (int i = 0; i < chordEvents.Count; i++)
            {
                // Get debug label from harmonization step if available
                string debugLabel = null;
                if (i < steps.Count && steps[i].ChosenChord.Roman != null)
                {
                    debugLabel = steps[i].ChosenChord.Roman;
                }

                // Naive harmonization uses default 1 quarter per chord
                int quarters = 1;
                int durationTicks = quarters * timelineSpec.ticksPerQuarter;

                var region = new ChordRegion
                {
                    startTick = cumulativeStartTick,
                    durationTicks = durationTicks,
                    chordEvent = chordEvents[i],
                    debugLabel = debugLabel
                };
                regions.Add(region);

                // Update cumulative startTick for next region
                cumulativeStartTick += durationTicks;

                // Debug logging for constructed regions
                if (enableDebugLogs)
                {
                    int rootPc = TheoryScale.GetDegreePitchClass(key, chordEvents[i].Recipe.Degree);
                    if (rootPc >= 0)
                    {
                        rootPc = (rootPc + chordEvents[i].Recipe.RootSemitoneOffset + 12) % 12;
                        if (rootPc < 0) rootPc += 12;
                    }
                    Debug.Log($"[ChordLab Region] Index={i}, Roman='{debugLabel ?? "?"}', startTick={region.startTick}, durationTicks={region.durationTicks} (quarters={quarters}), rootPc={rootPc}, ticksPerQuarter={timelineSpec.ticksPerQuarter}");
                }
            }

            // Store regions for debug inspection
            _lastRegions = regions;

            // 6. Use TheoryVoicing to voice-lead the progression with melody in soprano
            // Calculate upper voice MIDI ranges based on rootOctave
            var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
            
            // Debug logging for soprano range
            if (TheoryVoicing.GetTendencyDebug())
            {
                Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
            }

            var diags = new DiagnosticsCollector();
            var voicedChords = TheoryVoicing.VoiceLeadRegions(
                key,
                timelineSpec,
                regions,
                useMelodyConstraint: true, // Naive harmonization always uses melody
                numVoices: 4,
                rootOctave: rootOctave,
                bassOctave: rootOctave - 1,
                upperMinMidi: upperMinMidi,
                upperMaxMidi: upperMaxMidi,
                diags: diags);
            // Store diagnostics for per-region logging (no bulk dump)
            _lastDiagnostics = diags;
            if (_lastDiagnostics != null)
            {
                _lastDiagnostics.EnableTrace = enableUnityTraceLogs;
            }

            if (voicedChords == null || voicedChords.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Voicing failed; nothing to play.");
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Voiced {voicedChords.Count} chords for naive harmonization playback");
                
                // Log SATB MIDI for each chord
                for (int i = 0; i < voicedChords.Count; i++)
                {
                    var voiced = voicedChords[i];
                    if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                    {
                        // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                        int bass = voiced.VoicesMidi[0];
                        int tenor = voiced.VoicesMidi[1];
                        int alto = voiced.VoicesMidi[2];
                        int soprano = voiced.VoicesMidi[3];
                        string bassName = TheoryPitch.GetPitchNameFromMidi(bass, key);
                        string tenorName = TheoryPitch.GetPitchNameFromMidi(tenor, key);
                        string altoName = TheoryPitch.GetPitchNameFromMidi(alto, key);
                        string sopranoName = TheoryPitch.GetPitchNameFromMidi(soprano, key);
                        Debug.Log($"[ChordLab SATB] Chord {i + 1}: B={bass}({bassName}) T={tenor}({tenorName}) A={alto}({altoName}) S={soprano}({sopranoName})");
                    }
                    else if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                    {
                        Debug.Log($"[ChordLab SATB] Chord {i + 1}: MIDI notes [{string.Join(", ", voiced.VoicesMidi)}]");
                    }
                }
            }

            // Clear both viewers before starting playback (ensures clean state if previous run aborted)
            if (voicingViewer != null)
            {
                voicingViewer.Clear();
            }
            
            // Clear chord grid container
            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // 6. Update chord grid with harmonized chords (NH mode)
            UpdateChordGridFromChordEvents(key, chordEvents, voicedChords, "NH");

            // 6.5. Store state for export
            lastVoicedMelodyLine = new List<MelodyEvent>(melodyLine);
            lastVoicedChordEvents = new List<ChordEvent>(chordEvents);
            lastVoicedChords = new List<VoicedChord>(voicedChords);
            lastVoicedKey = key;

            // 7. Build and write Roman numeral progression back to input field
            string romanProgressionString = BuildRomanProgressionString(steps);
            if (!string.IsNullOrEmpty(romanProgressionString) && progressionInput != null)
            {
                progressionInput.text = romanProgressionString;
                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Wrote harmonized progression to input field: {romanProgressionString}");
            }

            // 8. Play the voiced chords using the existing playback helpers
            // Timeline v1: Pass melody input for independent timeline playback
            // Pass regions for timing if available
            StartCoroutine(PlayVoicedChordSequenceCo(voicedChords, chordEvents, key, regions: _lastRegions, melodyInput: melodyLine));
        }

        /// <summary>
        /// Shared core SATB pipeline that takes a list of ChordRecipes and a melody,
        /// voice-leads them, and plays the result with SATB display.
        /// This method is used by both Roman numeral and chord symbol SATB paths.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipes">List of chord recipes to voice-lead</param>
        /// <param name="melodyInput">Melody input string (note names or empty for test melody)</param>
        /// <param name="durationsInQuarters">Optional list of durations in quarter notes for each chord (null = default 1 quarter each)</param>
        /// <param name="skipAutoCorrection">If true, skip auto-correction to mode (for chord symbols)</param>
        /// <param name="romanInputText">Optional original Roman numeral input text (for duration suffix parsing). If null, uses existing region construction.</param>
        private void RunSatbFromChordRecipes(TheoryKey key, List<ChordRecipe> recipes, string melodyInput, List<int> durationsInQuarters = null, bool skipAutoCorrection = false, string romanInputText = null)
        {
            UnityEngine.Debug.Log("[ENTRY] UI SATB button -> RunSatbFromChordRecipes -> VoiceLeadProgressionWithMelody -> BuildUpperVoicesIncrementalWithMelody");
            if (enableDebugLogs)
                Debug.Log($"[ChordLab] RunSatbFromChordRecipes started with {recipes.Count} chords, skipAutoCorrection={skipAutoCorrection}");

            // 1. Adjust recipes to match diatonic triad quality for the mode (if enabled and not skipped)
            var adjustedRecipes = new List<ChordRecipe>(recipes.Count);
            for (int i = 0; i < recipes.Count; i++)
            {
                var originalRecipe = recipes[i];
                if (!skipAutoCorrection && autoCorrectToMode)
                {
                    var adjusted = TheoryChord.AdjustTriadQualityToMode(key, originalRecipe, out bool wasAdjusted);
                    adjustedRecipes.Add(adjusted);
                    if (wasAdjusted && enableDebugLogs)
                    {
                        string adjustedNumeral = TheoryChord.RecipeToRomanNumeral(key, adjusted);
                        Debug.Log($"[ChordLab] Adjusted chord {i + 1} to '{adjustedNumeral}' to fit {key}");
                    }
                }
                else
                {
                    adjustedRecipes.Add(originalRecipe);
                }
            }

            // 3. Build ChordEvents from recipes with timing
            List<ChordEvent> chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);

            if (chordEvents == null || chordEvents.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Failed to build chord events from recipes.");
                return;
            }

            // 4. Build ChordRegion[] from chordEvents with timeline information
            List<ChordRegion> regions = null;

            // Debug: Check helper condition
            if (enableDebugLogs)
            {
                Debug.Log($"[RunSatbFromChordRecipes] Checking helper condition: romanInputText='{romanInputText}', skipAutoCorrection={skipAutoCorrection}, IsNullOrWhiteSpace={string.IsNullOrWhiteSpace(romanInputText)}");
            }

            // Use shared helper if Roman input text is available (supports :N duration suffixes)
            if (!string.IsNullOrWhiteSpace(romanInputText) && !skipAutoCorrection)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[RunSatbFromChordRecipes] Attempting to use BuildRegionsFromRomanInput with input: '{romanInputText}'");
                    Debug.Log($"[RunSatbFromChordRecipes] Pre-parsed durationsInQuarters from parameter: {(durationsInQuarters != null ? $"[{string.Join(", ", durationsInQuarters)}]" : "null")}");
                }

                // Extract melody MIDI from chordEvents
                List<int> melodyMidiList = new List<int>(chordEvents.Count);
                for (int i = 0; i < chordEvents.Count; i++)
                {
                    if (chordEvents[i].MelodyMidi.HasValue)
                    {
                        // Remove offset (helper will re-apply it)
                        melodyMidiList.Add(chordEvents[i].MelodyMidi.Value - MelodyOffsetSemitones);
                    }
                    else
                    {
                        melodyMidiList.Add(-1); // Invalid, will be ignored
                    }
                }

                regions = BuildRegionsFromRomanInput(romanInputText, key, timelineSpec, melodyMidiList, skipAutoCorrection: false);

                if (enableDebugLogs)
                {
                    if (regions != null)
                        Debug.Log($"[RunSatbFromChordRecipes] BuildRegionsFromRomanInput succeeded, created {regions.Count} regions");
                    else
                        Debug.LogWarning($"[RunSatbFromChordRecipes] BuildRegionsFromRomanInput returned null, falling back to old code");
                }
            }

            // Fallback: build regions from existing chordEvents (for chord symbols or when helper unavailable)
            if (regions == null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[RunSatbFromChordRecipes FALLBACK] durationsInQuarters={(durationsInQuarters != null ? $"[{string.Join(", ", durationsInQuarters)}]" : "null")}, chordEvents.Count={chordEvents.Count}, adjustedRecipes.Count={adjustedRecipes.Count}");
                }

                regions = new List<ChordRegion>(chordEvents.Count);
                int cumulativeStartTick = 0;
                for (int i = 0; i < chordEvents.Count; i++)
                {
                    // Get debug label from recipe (Roman numeral) if available
                    string debugLabel = null;
                    if (i < adjustedRecipes.Count)
                    {
                        debugLabel = TheoryChord.RecipeToRomanNumeral(key, adjustedRecipes[i]);
                    }

                    // Get duration in quarters (default to 1 if not provided)
                    int quarters = (durationsInQuarters != null && i < durationsInQuarters.Count) 
                        ? durationsInQuarters[i] 
                        : 1;
                    int durationTicks = quarters * timelineSpec.ticksPerQuarter;

                    if (enableDebugLogs && i == 0)
                    {
                        Debug.Log($"[RunSatbFromChordRecipes FALLBACK] Region 0: quarters={quarters} (from durationsInQuarters[{i}]={(durationsInQuarters != null && i < durationsInQuarters.Count ? durationsInQuarters[i].ToString() : "N/A")}), durationTicks={durationTicks}");
                    }

                    var region = new ChordRegion
                    {
                        startTick = cumulativeStartTick,
                        durationTicks = durationTicks,
                        chordEvent = chordEvents[i],
                        debugLabel = debugLabel
                    };
                    regions.Add(region);

                    // Update cumulative startTick for next region
                    cumulativeStartTick += durationTicks;

                    // Debug logging for constructed regions
                    if (enableDebugLogs)
                    {
                        int rootPc = TheoryScale.GetDegreePitchClass(key, chordEvents[i].Recipe.Degree);
                        if (rootPc >= 0)
                        {
                            rootPc = (rootPc + chordEvents[i].Recipe.RootSemitoneOffset + 12) % 12;
                            if (rootPc < 0) rootPc += 12;
                        }
                        Debug.Log($"[ChordLab Region] Index={i}, Roman='{debugLabel ?? "?"}', startTick={region.startTick}, durationTicks={region.durationTicks} (quarters={quarters}), rootPc={rootPc}, ticksPerQuarter={timelineSpec.ticksPerQuarter}");
                    }
                }
            }

            // Store regions for debug inspection
            _lastRegions = regions;

            // 1.5. Build the melody line with piano roll priority (after regions are built for timeline length)
            List<MelodyEvent> melodyLine = BuildMelodyEventsForVoicedPlayback(regions, timelineSpec);

            if (melodyLine == null || melodyLine.Count == 0)
            {
                string error = "SATB requires a non-empty melody. Please provide melody via piano roll, note-name input, or test melody pattern.";
                Debug.LogWarning($"[ChordLab] {error}");
                UpdateStatus(error);
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Using melody with {melodyLine.Count} events for manual progression voicing.");

            // 4.5. Timeline v1: Build timeline melody events from input melody line
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> timelineMelodyEvents = null;
            if (melodyLine != null && melodyLine.Count > 0)
            {
                timelineMelodyEvents = BuildTimelineMelodyEvents(melodyLine, timelineSpec);
                if (enableDebugLogs)
                    Debug.Log($"[RunSatbFromChordRecipes] Built {timelineMelodyEvents.Count} timeline melody events from {melodyLine.Count} input events");
                
                // DEBUG: Dump all parsed MelodyEvents to diagnose duplicate triggers
                UnityEngine.Debug.Log($"[MelodyEvents] Parsed {timelineMelodyEvents.Count} events (from RunSatbFromChordRecipes):");
                for (int i = 0; i < timelineMelodyEvents.Count; i++)
                {
                    var me = timelineMelodyEvents[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(me.midi, key);
                    UnityEngine.Debug.Log($"[MelodyEvents] idx={i} midi={me.midi} ({noteName}) startTick={me.startTick} durationTicks={me.durationTicks}");
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Timeline v1: Matching melody events to regions by onset (not by index)");
                if (timelineMelodyEvents != null)
                    Debug.Log($"[ChordLab] Timeline melody events: {timelineMelodyEvents.Count}, ChordEvents: {chordEvents.Count}");
            }

            // 5.5. Timeline v1: Boundary Anchor Policy - Match timeline melody events to regions by boundary
            // Policy: For each region, find the melody note that is sounding at region.startTick.
            // A note is "sounding at boundary" if: regionStart ∈ [eventStart, eventEnd)
            // This anchors soprano voicing to the melody note active at the chord boundary.
            // If provided, externalSopranoAnchors[r] forces the soprano pitch for region r.
            // Used in Timeline mode to treat the melody as the true soprano and only harmonize B/T/A underneath.
            if (timelineMelodyEvents != null && timelineMelodyEvents.Count > 0 && regions != null)
            {
                // Build boundary anchors: melody note sounding at each region's startTick
                var externalSopranoAnchors = BuildBoundaryMelodyAnchors(regions, timelineMelodyEvents);
                
                for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
                {
                    var region = regions[regionIdx];
                    int? anchorMidi = externalSopranoAnchors[regionIdx];
                    
                    Sonoria.MusicTheory.Timeline.MelodyEvent? matchingMelodyEvent = null;
                    string matchReason = "none";
                    
                    if (anchorMidi.HasValue)
                    {
                        // Find the melody event that corresponds to this anchor
                        foreach (var melodyEvent in timelineMelodyEvents)
                        {
                            int eventStart = melodyEvent.startTick;
                            int eventEnd = melodyEvent.startTick + melodyEvent.durationTicks;
                            
                            // Check if this event is sounding at the region boundary
                            if (region.startTick >= eventStart && region.startTick < eventEnd && melodyEvent.midi == anchorMidi.Value)
                            {
                                matchingMelodyEvent = melodyEvent;
                                matchReason = $"boundary_anchor(tick={region.startTick}, event=[{eventStart}, {eventEnd}))";
                                break;
                            }
                        }
                    }
                    
                    // Update ChordEvent in region with matched melody MIDI (if found)
                    // This is CRITICAL: VoiceLeadRegions reads region.chordEvent.MelodyMidi to constrain soprano
                    // NOTE: ChordRegion is a struct, so we must reassign the modified struct back to the list
                    if (matchingMelodyEvent.HasValue)
                    {
                        var melodyEvent = matchingMelodyEvent.Value;
                        // Apply melody octave offset (only affects playback register, not theory)
                        int melodyMidiWithOffset = melodyEvent.midi + MelodyOffsetSemitones;
                        
                        // Update the ChordEvent in the region (this is what the voicing engine reads)
                        region.chordEvent = new ChordEvent
                        {
                            Key = region.chordEvent.Key,
                            Recipe = region.chordEvent.Recipe,
                            TimeBeats = region.chordEvent.TimeBeats, // Keep original TimeBeats
                            MelodyMidi = melodyMidiWithOffset
                        };
                        
                        // Reassign modified struct back to list (required for structs)
                        regions[regionIdx] = region;
                        
                        // DIAGNOSTIC: Log soprano anchor selection
                        string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                        int regionEndTick = region.startTick + region.durationTicks;
                        UnityEngine.Debug.Log($"[SOPRANO_ANCHOR] regionIdx={regionIdx} startTick={region.startTick} endTick={regionEndTick} chosenMidi={melodyEvent.midi}({noteName}) chosenReason={matchReason}");
                    }
                    else
                    {
                        // No melody event found for this region - clear MelodyMidi
                        region.chordEvent = new ChordEvent
                        {
                            Key = region.chordEvent.Key,
                            Recipe = region.chordEvent.Recipe,
                            TimeBeats = region.chordEvent.TimeBeats,
                            MelodyMidi = null
                        };
                        
                        // Reassign modified struct back to list (required for structs)
                        regions[regionIdx] = region;
                        
                        int regionEndTick = region.startTick + region.durationTicks;
                        UnityEngine.Debug.Log($"[SOPRANO_ANCHOR] regionIdx={regionIdx} startTick={region.startTick} endTick={regionEndTick} chosenMidi=none chosenReason=no_melody_event_found");
                    }
                }
            }

            // 6. Voice-lead the progression with melody using the existing voicing engine
            var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
            
            // Debug logging for soprano range
            if (TheoryVoicing.GetTendencyDebug())
            {
                Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
            }

            var diags = new DiagnosticsCollector();
            var voicedChords = TheoryVoicing.VoiceLeadRegions(
                key,
                timelineSpec,
                regions,
                useMelodyConstraint: true, // RunSatbFromChordRecipes always uses melody
                numVoices: 4,
                rootOctave: rootOctave,
                bassOctave: rootOctave - 1,
                upperMinMidi: upperMinMidi,
                upperMaxMidi: upperMaxMidi,
                diags: diags);
            // Store diagnostics for per-region logging (no bulk dump)
            _lastDiagnostics = diags;
            if (_lastDiagnostics != null)
            {
                _lastDiagnostics.EnableTrace = enableUnityTraceLogs;
            }

            if (voicedChords == null || voicedChords.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Voicing failed; nothing to play.");
                return;
            }

            // TRACE2: Build output snapshot (after SATB voicing is built)
            if (EnablePlaybackTrace2)
            {
                UnityEngine.Debug.Log($"[TRACE2] === BUILD OUTPUT SNAPSHOT (run={_currentPlaybackRunId}) ===");
                for (int i = 0; i < regions.Count && i < voicedChords.Count; i++)
                {
                    var region = regions[i];
                    var voiced = voicedChords[i];
                    string label = region.debugLabel ?? "";
                    int? sopranoAnchorMidi = region.chordEvent.MelodyMidi;
                    
                    // List chord notes with voice names
                    var chordNotesList = new List<string>();
                    if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                    {
                        chordNotesList.Add($"B={voiced.VoicesMidi[0]}");
                        chordNotesList.Add($"T={voiced.VoicesMidi[1]}");
                        chordNotesList.Add($"A={voiced.VoicesMidi[2]}");
                        chordNotesList.Add($"S={voiced.VoicesMidi[3]}");
                        
                        // Check if soprano anchor is part of chord voices
                        bool sopranoInChord = sopranoAnchorMidi.HasValue && sopranoAnchorMidi.Value == voiced.VoicesMidi[3];
                        string sopranoStatus = sopranoAnchorMidi.HasValue 
                            ? $"sopranoAnchorMidi={sopranoAnchorMidi.Value} ({(sopranoInChord ? "IN_CHORD_VOICES" : "SEPARATE")})"
                            : "sopranoAnchorMidi=none";
                        
                        // List melody events in this region
                        var melodyEventsInRegion = new List<string>();
                        if (timelineMelodyEvents != null)
                        {
                            int regionEndTick = region.startTick + region.durationTicks;
                            for (int mIdx = 0; mIdx < timelineMelodyEvents.Count; mIdx++)
                            {
                                var melEvt = timelineMelodyEvents[mIdx];
                                if (melEvt.startTick >= region.startTick && melEvt.startTick < regionEndTick)
                                {
                                    melodyEventsInRegion.Add($"M{mIdx}:midi={melEvt.midi} tick={melEvt.startTick}");
                                }
                            }
                        }
                        
                        UnityEngine.Debug.Log(
                            $"[TRACE2] BUILD R{i}: label={label} startTick={region.startTick} endTick={region.startTick + region.durationTicks} " +
                            $"chordNotes=[{string.Join(", ", chordNotesList)}] {sopranoStatus} " +
                            $"melodyEvents=[{string.Join(", ", melodyEventsInRegion)}]"
                        );
                    }
                    else if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[TRACE2] BUILD R{i}: label={label} WARNING: chord has {voiced.VoicesMidi.Length} voices (expected 4) " +
                            $"MIDI=[{string.Join(", ", voiced.VoicesMidi)}]"
                        );
                    }
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Voiced {voicedChords.Count} chords for manual progression playback");
                
                // Log SATB MIDI for each chord
                for (int i = 0; i < voicedChords.Count; i++)
                {
                    var voiced = voicedChords[i];
                    if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                    {
                        // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                        int bass = voiced.VoicesMidi[0];
                        int tenor = voiced.VoicesMidi[1];
                        int alto = voiced.VoicesMidi[2];
                        int soprano = voiced.VoicesMidi[3];
                        string bassName = TheoryPitch.GetPitchNameFromMidi(bass, key);
                        string tenorName = TheoryPitch.GetPitchNameFromMidi(tenor, key);
                        string altoName = TheoryPitch.GetPitchNameFromMidi(alto, key);
                        string sopranoName = TheoryPitch.GetPitchNameFromMidi(soprano, key);
                        Debug.Log($"[ChordLab SATB] Chord {i + 1}: B={bass}({bassName}) T={tenor}({tenorName}) A={alto}({altoName}) S={soprano}({sopranoName})");
                    }
                    else if (voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                    {
                        Debug.Log($"[ChordLab SATB] Chord {i + 1}: MIDI notes [{string.Join(", ", voiced.VoicesMidi)}]");
                    }
                }
            }

            // 6. Store state for export
            lastVoicedMelodyLine = new List<MelodyEvent>(melodyLine);
            lastVoicedChordEvents = new List<ChordEvent>(chordEvents);
            lastVoicedChords = new List<VoicedChord>(voicedChords);
            lastVoicedKey = key;

            // 7. Clear both viewers before starting playback
            if (voicingViewer != null)
            {
                voicingViewer.Clear();
            }

            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // 8. Update ChordGrid with the manual progression (SATB mode)
            UpdateChordGridFromChordEvents(key, chordEvents, voicedChords, "SATB");

            // 9. Start playback coroutine that updates VoicingViewer and plays audio
            // Timeline v1: Pass melody input for independent timeline playback
            // Use the same melodyLine that was used for voicing (which respects piano roll priority)
            List<MelodyEvent> melodyInputForTimeline = melodyLine; // Use the melody that was already built with piano roll priority
            // Pass regions for timing if available
            StartCoroutine(PlayVoicedChordSequenceCo(voicedChords, chordEvents, key, regions: _lastRegions, melodyInput: melodyInputForTimeline));
        }

        /// <summary>
        /// Takes the current manual Roman numeral progression from the input field,
        /// parses it into chords, voice-leads it with the current melody, and plays it
        /// with SATB voicing display. Does NOT reharmonize - treats the progression as
        /// fixed ground truth harmony.
        /// </summary>
        private void PlayManualProgressionWithMelodyVoiced()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] PlayManualProgressionWithMelodyVoiced started.");

            // 1. Get the current key from dropdowns
            TheoryKey key = GetKeyFromDropdowns();

            // 2. Parse the Roman numeral progression from the input field
            if (progressionInput == null || string.IsNullOrWhiteSpace(progressionInput.text))
            {
                Debug.LogWarning("[ChordLab] Progression input is empty. Cannot voice manual progression.");
                return;
            }

            bool parseSuccess = TryBuildChordRecipesFromRomanInput(key, progressionInput.text, out List<string> originalTokens, out List<ChordRecipe> recipes, out List<int> durationsInQuarters);

            if (!parseSuccess || recipes == null || recipes.Count == 0)
            {
                string error = "Could not parse progression. Check for invalid Roman numerals.";
                Debug.LogWarning($"[ChordLab] {error}");
                UpdateStatus(error);
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Parsed {recipes.Count} chords from progression: {string.Join(" ", originalTokens)}");

            // 3. Get melody input (for consistency, though RunSatbFromChordRecipes will build it)
            string melodyInput = Input_MelodyNoteNames != null ? Input_MelodyNoteNames.text : string.Empty;

            // 4. Call shared SATB pipeline (Roman numerals use auto-correction if enabled)
            // Pass the original input text so the helper can parse duration suffixes
            RunSatbFromChordRecipes(key, recipes, melodyInput, durationsInQuarters, skipAutoCorrection: false, romanInputText: progressionInput.text);
        }

        /// <summary>
        /// Editor-only debug helper that:
        /// - Builds the test melody line
        /// - Builds naive harmonization using TheoryHarmonization
        /// - Converts it to chord events
        /// - Uses TheoryVoicing to voice-lead the progression with melody in soprano
        /// - Plays the resulting voiced chords via FmodNoteSynth.
        /// </summary>
        public void DebugPlayNaiveHarmonizationForTestMelody()
        {
#if UNITY_EDITOR
            PlayNaiveHarmonizationForCurrentTestMelody();
#endif
        }

        /// <summary>
        /// Public wrapper for editor menu item: takes the current manual progression
        /// from the input field, voice-leads it with the current melody, and plays it
        /// with SATB voicing display. Does NOT reharmonize.
        /// </summary>
        public void DebugPlayManualProgressionWithMelodyVoiced()
        {
#if UNITY_EDITOR
            PlayManualProgressionWithMelodyVoiced();
#endif
        }

        /// <summary>
        /// Editor-only debug helper: exports the currently voiced harmonization to JSON
        /// and logs it to the console. Also copies to clipboard if available.
        /// </summary>
        public void DebugExportCurrentVoicedHarmonization()
        {
#if UNITY_EDITOR
            string json = BuildCurrentVoicedHarmonizationJson();
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[ChordLab] No voiced harmonization state available for export. Please run Naive Harmonization or Play Voiced first.");
                return;
            }

            Debug.Log($"[ChordLab JSON Export]\n{json}");

            // Copy to clipboard (editor-only)
            try
            {
                UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
                Debug.Log("[ChordLab] JSON copied to clipboard.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ChordLab] Failed to copy JSON to clipboard: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Coroutine that plays a sequence of voiced chords (each chord is a list/array
        /// of MIDI notes) using the existing FmodNoteSynth and timing settings.
        /// </summary>
        // ============================================================================
        // Timeline v1: Melody Events (Independent Timeline Lane)
        // ============================================================================

        /// <summary>
        /// Converts TimeBeats-based melody events to Timeline.MelodyEvent with tick-based timing.
        /// Timeline v1: Melody becomes an independent timeline lane with multiple notes per chord region.
        /// </summary>
        private List<Sonoria.MusicTheory.Timeline.MelodyEvent> BuildTimelineMelodyEvents(
            List<MelodyEvent> melodyEvents, 
            TimelineSpec spec)
        {
            if (melodyEvents == null || melodyEvents.Count == 0)
                return new List<Sonoria.MusicTheory.Timeline.MelodyEvent>();

            var timelineMelody = new List<Sonoria.MusicTheory.Timeline.MelodyEvent>(melodyEvents.Count);
            int ticksPerQuarter = spec != null ? spec.ticksPerQuarter : 4;

            foreach (var evt in melodyEvents)
            {
                // Convert TimeBeats to ticks (assumes 1 beat = 1 quarter note)
                int startTick = (int)(evt.TimeBeats * ticksPerQuarter);
                int durationTicks = (int)(evt.DurationBeats * ticksPerQuarter);

                timelineMelody.Add(new Sonoria.MusicTheory.Timeline.MelodyEvent
                {
                    startTick = startTick,
                    durationTicks = durationTicks,
                    midi = evt.Midi
                });
            }

            // Ensure deterministic ordering: sort by startTick, then by MIDI (stable tie-break)
            timelineMelody.Sort((a, b) =>
            {
                int tickCompare = a.startTick.CompareTo(b.startTick);
                return tickCompare != 0 ? tickCompare : a.midi.CompareTo(b.midi);
            });

            // INSTRUMENTATION: Log full timeline event list after conversion
            if (enableMelodyTimelineDebug)
            {
                TheoryKey keyForLogging = GetKeyFromDropdowns();
                UnityEngine.Debug.Log($"[MELODY_TIMELINE] === Timeline Melody Events ===");
                UnityEngine.Debug.Log($"[MELODY_TIMELINE] Event count: {timelineMelody.Count}, ticksPerQuarter: {ticksPerQuarter}");
                for (int idx = 0; idx < timelineMelody.Count; idx++)
                {
                    var evt = timelineMelody[idx];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(evt.midi, keyForLogging);
                    UnityEngine.Debug.Log($"[MELODY_TIMELINE] Event #{idx}: noteName={noteName} midi={evt.midi} startTick={evt.startTick} durationTicks={evt.durationTicks}");
                }
                if (timelineMelody.Count > 0)
                {
                    var lastEvt = timelineMelody[timelineMelody.Count - 1];
                    string lastNoteName = TheoryPitch.GetPitchNameFromMidi(lastEvt.midi, keyForLogging);
                    UnityEngine.Debug.Log($"[MELODY_TIMELINE] Last event: #{timelineMelody.Count - 1}, noteName={lastNoteName}, midi={lastEvt.midi}, startTick={lastEvt.startTick}");
                }
            }

            return timelineMelody;
        }

        /// <summary>
        /// Calculates total quarter-note steps from a list of chord regions.
        /// Uses the same logic as BuildSatbTimelineForSimplePlay.
        /// </summary>
        private int CalculateTotalStepsFromRegions(IReadOnlyList<ChordRegion> regions, TimelineSpec spec)
        {
            if (regions == null || regions.Count == 0)
                return 0;
            
            int ticksPerQuarter = (spec != null && spec.ticksPerQuarter > 0) ? spec.ticksPerQuarter : 4;
            
            // Compute maxTick as max of region.startTick + region.durationTicks
            int maxTick = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                int regionEndTick = regions[i].startTick + regions[i].durationTicks;
                if (regionEndTick > maxTick)
                {
                    maxTick = regionEndTick;
                }
            }
            
            // Calculate total quarters (ceiling division)
            int totalQuarters = Mathf.CeilToInt(maxTick / (float)ticksPerQuarter);
            return totalQuarters;
        }

        /// <summary>
        /// Helper to convert ticks to seconds using TimelineSpec.
        /// Formula: seconds = chordDurationSeconds * (ticks / ticksPerQuarter)
        /// </summary>
        private float SecondsFromTicks(int ticks, TimelineSpec spec)
        {
            if (spec == null || spec.ticksPerQuarter <= 0)
                return ticks * (chordDurationSeconds / 4f); // Fallback: assume 4 ticks per quarter

            float quarters = ticks / (float)spec.ticksPerQuarter;
            return chordDurationSeconds * quarters;
        }

        /// <summary>
        /// Checks if a MelodyEvent overlaps with a ChordRegion.
        /// Overlap test: max(starts) < min(ends)
        /// Timeline v1: Melody events may overlap multiple chord regions.
        /// </summary>
        private bool MelodyEventOverlapsRegion(
            Sonoria.MusicTheory.Timeline.MelodyEvent melodyEvent,
            ChordRegion region)
        {
            int melodyStart = melodyEvent.startTick;
            int melodyEnd = melodyEvent.startTick + melodyEvent.durationTicks;
            int regionStart = region.startTick;
            int regionEnd = region.startTick + region.durationTicks;

            // Overlap if: max(starts) < min(ends)
            int maxStart = Mathf.Max(melodyStart, regionStart);
            int minEnd = Mathf.Min(melodyEnd, regionEnd);
            return maxStart < minEnd;
        }

        /// <summary>
        /// Classification of a melody note relative to a chord.
        /// Timeline v1: Informational only, does NOT affect voicing or chord symbols.
        /// </summary>
        public enum MelodyNoteClassification
        {
            ChordTone,              // Root, 3rd, 5th, or 7th (if present)
            RequestedExtension,     // Explicitly requested extension/tension (b9, 9, #11, etc.)
            NonChordTone            // Otherwise: NCT (passing tone, neighbor, etc.)
        }

        /// <summary>
        /// Classifies a melody note (pitch class) relative to a chord recipe.
        /// Timeline v1: This is purely informational and does NOT alter voicing or chord symbols.
        /// </summary>
        private MelodyNoteClassification ClassifyMelodyNote(
            int melodyPc,
            ChordRecipe recipe,
            TheoryKey key)
        {
            // Get chord tone pitch classes
            var chordTonePcs = TheoryVoicing.GetChordTonePitchClasses(new ChordEvent
            {
                Key = key,
                Recipe = recipe
            });

            // Check if it's a chord tone
            if (chordTonePcs != null && chordTonePcs.Contains(melodyPc))
            {
                return MelodyNoteClassification.ChordTone;
            }

            // Check if it's a requested extension
            var req = recipe.RequestedExtensions;
            if (req.HasAny)
            {
                int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
                if (rootPc < 0) rootPc = 0;
                rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
                if (rootPc < 0) rootPc += 12;

                // Check requested tensions
                if (req.TensionFlat9 && melodyPc == (rootPc + 1) % 12)
                    return MelodyNoteClassification.RequestedExtension;
                if (req.Tension9 && melodyPc == (rootPc + 2) % 12)
                    return MelodyNoteClassification.RequestedExtension;
                if (req.TensionSharp9 && melodyPc == (rootPc + 3) % 12)
                    return MelodyNoteClassification.RequestedExtension;
                if (req.TensionSharp11 && melodyPc == (rootPc + 6) % 12)
                    return MelodyNoteClassification.RequestedExtension;
                if (req.Add9 && melodyPc == (rootPc + 2) % 12)
                    return MelodyNoteClassification.RequestedExtension;
                if (req.Add11 && melodyPc == (rootPc + 5) % 12)
                    return MelodyNoteClassification.RequestedExtension;
            }

            // Otherwise: non-chord tone
            return MelodyNoteClassification.NonChordTone;
        }

        /// <summary>
        /// Analyzes melody events against chord regions and generates diagnostic summary.
        /// Timeline v1: Purely observational, does NOT affect voicing or chord symbols.
        /// </summary>
        private void AnalyzeMelodyAgainstRegions(
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> melodyEvents,
            IReadOnlyList<ChordRegion> regions,
            TheoryKey key,
            DiagnosticsCollector diags)
        {
            if (melodyEvents == null || regions == null || diags == null)
                return;

            // Analyze each region
            for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
            {
                var region = regions[regionIdx];
                int chordToneCount = 0;
                int requestedExtensionCount = 0;
                int nonChordToneCount = 0;
                var nctExamples = new List<(int tick, string noteName, int midi)>();

                // Find all melody events that overlap this region
                foreach (var melodyEvent in melodyEvents)
                {
                    if (!MelodyEventOverlapsRegion(melodyEvent, region))
                        continue;

                    int melodyPc = (melodyEvent.midi % 12 + 12) % 12;
                    var classification = ClassifyMelodyNote(melodyPc, region.chordEvent.Recipe, key);

                    switch (classification)
                    {
                        case MelodyNoteClassification.ChordTone:
                            chordToneCount++;
                            break;
                        case MelodyNoteClassification.RequestedExtension:
                            requestedExtensionCount++;
                            break;
                        case MelodyNoteClassification.NonChordTone:
                            nonChordToneCount++;
                            if (nctExamples.Count < 3) // Cap at 3 examples
                            {
                                string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                                nctExamples.Add((melodyEvent.startTick, noteName, melodyEvent.midi));
                            }
                            break;
                    }
                }

                // Add diagnostic summary (Info level, quiet by default)
                if (chordToneCount > 0 || requestedExtensionCount > 0 || nonChordToneCount > 0)
                {
                    string summary = $"Melody: {chordToneCount} chord tones, {requestedExtensionCount} requested, {nonChordToneCount} NCT";
                    if (nctExamples.Count > 0)
                    {
                        var exampleStrs = nctExamples.Select(e => $"{e.noteName}@{e.tick}").ToList();
                        summary += $" (NCT examples: {string.Join(", ", exampleStrs)})";
                    }
                    
                    diags.Add(regionIdx, DiagSeverity.Info, "MelodyAnalysis", summary, -1, -1, -1);
                }
            }
        }

        private IEnumerator PlayVoicedChordSequenceCo(List<VoicedChord> voicedChords, IReadOnlyList<ChordEvent> chordEvents, TheoryKey key, IReadOnlyList<ChordRegion> regions = null, List<MelodyEvent> melodyInput = null)
        {
            if (voicedChords == null || voicedChords.Count == 0)
                yield break;
            
            // Increment playback run ID at start
            _currentPlaybackRunId = ++_playbackRunCounter;
            
            // Timeline v1: Increment keyboard playback token and clear active notes
            _keyboardPlaybackToken++;
            ClearActiveNotes();

            // Validate that chordEvents list matches voicedChords count
            if (chordEvents == null || chordEvents.Count != voicedChords.Count)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] Mismatch: {voicedChords.Count} voiced chords but {chordEvents?.Count ?? 0} chord events. Continuing without chord context.");
            }

            // Validate regions count matches voicedChords count
            bool useRegionTiming = (regions != null && regions.Count == voicedChords.Count);
            if (regions != null && regions.Count != voicedChords.Count)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] Mismatch: {voicedChords.Count} voiced chords but {regions.Count} regions. Falling back to fixed timing.");
            }

            // TIMING UNIFICATION: Use timeline-aware scheduling for chords (same master clock as melody)
            // Get timeline spec and timing parameters for diagnostics
            int tpq = (timelineSpec != null) ? timelineSpec.ticksPerQuarter : 4;
            float quarterNoteSeconds = chordDurationSeconds; // One quarter note = chordDurationSeconds seconds
            
            // TEMPORARY DIAGNOSTICS: Log timing parameters and first 3 regions/events (gated by debug)
            if (enablePlaybackDebug && enablePlaybackTrace)
            {
                LogPlaybackVerbose("TIMING", "=== Playback Timing Parameters ===");
                LogPlaybackVerbose("TIMING", $"ticksPerQuarter (tpq) = {tpq}");
                LogPlaybackVerbose("TIMING", $"chordDurationSeconds = {chordDurationSeconds} (one quarter note = {quarterNoteSeconds}s)");
                LogPlaybackVerbose("TIMING", $"SecondsFromTicks({tpq}) = {SecondsFromTicks(tpq, timelineSpec):F4}s (should equal {quarterNoteSeconds}s for one quarter)");
                
                if (regions != null && regions.Count > 0)
                {
                    LogPlaybackVerbose("TIMING", $"Total regions: {regions.Count}");
                    for (int diagIdx = 0; diagIdx < Mathf.Min(3, regions.Count); diagIdx++)
                    {
                        var region = regions[diagIdx];
                        float regionStartSeconds = SecondsFromTicks(region.startTick, timelineSpec);
                        float regionDurationSeconds = SecondsFromTicks(region.durationTicks, timelineSpec);
                        string label = region.debugLabel ?? "?";
                        LogPlaybackVerbose("TIMING", $"Region #{diagIdx}: '{label}' startTick={region.startTick} durationTicks={region.durationTicks} -> start={regionStartSeconds:F4}s duration={regionDurationSeconds:F4}s");
                    }
                }
            }

            // Timeline v1: Convert melody input to timeline events (if provided)
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> timelineMelodyEvents = null;
            
            // Timeline v1 "Pianist" model:
            // When a Melody timeline exists, the melody is the only real soprano.
            // We derive a melody anchor at each chord boundary and voice B/T/A underneath it.
            bool melodyAsSopranoMode = (melodyInput != null && melodyInput.Count > 0);
            
            if (melodyAsSopranoMode)
            {
                timelineMelodyEvents = BuildTimelineMelodyEvents(melodyInput, timelineSpec);
                _lastTimelineMelodyEvents = timelineMelodyEvents;
                
                if (enableDebugLogs)
                    Debug.Log($"[ChordLab Timeline] Built {timelineMelodyEvents.Count} timeline melody events from {melodyInput.Count} input events");
                
                // Phase 1: Render piano roll from timeline melody events
                if (melodyPianoRoll != null && timelineMelodyEvents != null && timelineMelodyEvents.Count > 0 && regions != null && regions.Count > 0)
                {
                    int totalSteps = CalculateTotalStepsFromRegions(regions, timelineSpec);
                    melodyPianoRoll.RenderFromEvents(timelineMelodyEvents, totalSteps, timelineSpec);
                }
                
                // DEBUG: Dump all parsed MelodyEvents to diagnose duplicate triggers
                UnityEngine.Debug.Log($"[MelodyEvents] Parsed {timelineMelodyEvents.Count} events:");
                for (int i = 0; i < timelineMelodyEvents.Count; i++)
                {
                    var me = timelineMelodyEvents[i];
                    string noteName = TheoryPitch.GetPitchNameFromMidi(me.midi, key);
                    UnityEngine.Debug.Log($"[MelodyEvents] idx={i} midi={me.midi} ({noteName}) startTick={me.startTick} durationTicks={me.durationTicks}");
                }

                // REDUCED: Only log melody timing if playback debug trace enabled (to reduce FMOD starvation)
                if (enablePlaybackDebug && enablePlaybackTrace)
                {
                    LogPlaybackVerbose("TIMING", $"Total melody events: {timelineMelodyEvents.Count}");
                    for (int diagIdx = 0; diagIdx < Mathf.Min(3, timelineMelodyEvents.Count); diagIdx++)
                    {
                        var melodyEvent = timelineMelodyEvents[diagIdx];
                        float melodyStartSeconds = SecondsFromTicks(melodyEvent.startTick, timelineSpec);
                        float melodyDurationSeconds = SecondsFromTicks(melodyEvent.durationTicks, timelineSpec);
                        string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                        LogPlaybackVerbose("TIMING", $"Melody #{diagIdx}: {noteName}({melodyEvent.midi}) startTick={melodyEvent.startTick} durationTicks={melodyEvent.durationTicks} -> start={melodyStartSeconds:F4}s duration={melodyDurationSeconds:F4}s");
                    }
                }

                // Analyze melody against regions for diagnostics
                if (regions != null && _lastDiagnostics != null)
                {
                    AnalyzeMelodyAgainstRegions(timelineMelodyEvents, regions, key, _lastDiagnostics);
                }
            }
            else
            {
                _lastTimelineMelodyEvents = null;
                // Phase 1: Clear piano roll when no melody
                if (melodyPianoRoll != null)
                {
                    melodyPianoRoll.Clear();
                }
            }
            
            // Timeline v1: Build SATB + Melody timeline and update VoicingViewer if enabled
            if (voicingViewer != null && useVoicingTimelineView && useRegionTiming && regions != null && regions.Count > 0)
            {
                // Extract VoicesMidi arrays from voiced chords
                var voicesPerRegion = new List<int[]>();
                for (int i = 0; i < voicedChords.Count; i++)
                {
                    if (voicedChords[i].VoicesMidi != null && voicedChords[i].VoicesMidi.Length >= 4)
                    {
                        voicesPerRegion.Add(voicedChords[i].VoicesMidi);
                    }
                    else
                    {
                        // Fallback: create empty array if voices are missing
                        voicesPerRegion.Add(new int[4]);
                    }
                }
                
                // Build timeline with melody events (now that timelineMelodyEvents is available)
                BuildSatbAndMelodyTimeline(timelineSpec, regions, voicesPerRegion, timelineMelodyEvents,
                    out var bass, out var tenor, out var alto, out var soprano, out var melody, out var melodyIsAttack, out var chordIsAttack);
                
                // Build chord symbol and Roman numeral data for header rows
                BuildChordHeaderData(regions, key, out var absoluteChordSymbolsPerRegion, out var romanNumeralsPerRegion, out var isDiatonicPerRegion, out var regionDurationTicks);
                
                voicingViewer.ShowTimelineTopAndSatb(
                    topLine: melody,
                    topIsMelody: true,
                    alto: alto,
                    tenor: tenor,
                    bass: bass,
                    melodyIsAttack: melodyIsAttack,
                    chordIsAttack: chordIsAttack,
                    absoluteChordSymbolsPerRegion: absoluteChordSymbolsPerRegion,
                    romanNumeralsPerRegion: romanNumeralsPerRegion,
                    isDiatonicPerRegion: isDiatonicPerRegion,
                    regionDurationTicks: regionDurationTicks,
                    timelineSpec: timelineSpec);
            }
            
            // PlaybackAudit: Log scheduled regions and melody events
            if (enablePlaybackAudit)
            {
                UnityEngine.Debug.Log($"[PlaybackAudit] === SCHEDULED PLAYBACK ===");
                
                // Log scheduled regions
                if (regions != null && regions.Count > 0)
                {
                    UnityEngine.Debug.Log($"[PlaybackAudit] Scheduled Chord Regions ({regions.Count}):");
                    for (int i = 0; i < regions.Count; i++)
                    {
                        var region = regions[i];
                        string label = region.debugLabel ?? "?";
                        int? sopranoMidi = region.chordEvent.MelodyMidi;
                        string sopranoStr = sopranoMidi.HasValue ? $"{sopranoMidi.Value}({TheoryPitch.GetPitchNameFromMidi(sopranoMidi.Value, key)})" : "none";
                        UnityEngine.Debug.Log($"[PlaybackAudit]   Region {i}: label='{label}' startTick={region.startTick} durationTicks={region.durationTicks} sopranoAnchor={sopranoStr}");
                        
                        _currentScheduledRegions.Add(new ScheduledRegion
                        {
                            regionIdx = i,
                            label = label,
                            startTick = region.startTick,
                            durationTicks = region.durationTicks,
                            sopranoAnchorMidi = sopranoMidi
                        });
                    }
                }
                else
                {
                    UnityEngine.Debug.Log($"[PlaybackAudit] No regions scheduled (regions is null or empty)");
                }
                
                // Log scheduled melody events
                if (timelineMelodyEvents != null && timelineMelodyEvents.Count > 0)
                {
                    UnityEngine.Debug.Log($"[PlaybackAudit] Scheduled Melody Events ({timelineMelodyEvents.Count}):");
                    for (int i = 0; i < timelineMelodyEvents.Count; i++)
                    {
                        var melodyEvent = timelineMelodyEvents[i];
                        string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                        UnityEngine.Debug.Log($"[PlaybackAudit]   Melody {i}: {noteName}({melodyEvent.midi}) startTick={melodyEvent.startTick} durationTicks={melodyEvent.durationTicks}");
                        
                        _currentScheduledMelodyEvents.Add(new ScheduledMelodyEvent
                        {
                            melodyIdx = i,
                            midi = melodyEvent.midi,
                            startTick = melodyEvent.startTick,
                            durationTicks = melodyEvent.durationTicks
                        });
                    }
                }
                else
                {
                    UnityEngine.Debug.Log($"[PlaybackAudit] No melody events scheduled");
                }
            }

            // Clear console at playback start
            ClearConsole();
            
            // Hide all chord columns for progressive reveal
            HideAllChordColumns();

            // UNIFIED TIMING: Schedule chords by region.startTick using Time.time as master clock (same as melody)
            // CRITICAL: Capture playbackStartTime FIRST, then pass it to both chord and melody schedulers
            float playbackStartTime = Time.time; // Capture Unity time once at start of playback
            _currentPlaybackStartTime = playbackStartTime; // Store for PlaybackAudit
            
            // [PLAYBACK] start summary log
            int melodyEventCount = timelineMelodyEvents != null ? timelineMelodyEvents.Count : 0;
            int regionCount = regions != null ? regions.Count : 0;
            LogPlaybackInfo("PLAYBACK",
                $"runId={_currentPlaybackRunId} regions={regionCount} melodyEvents={melodyEventCount} tpq={tpq} chordDurSec={chordDurationSeconds:F4}");
            
            // Clear playback tracking (centralized handles)
            _activeHandles.Clear();
            _activeHandlesByRegion.Clear();
            _activeHandlesByMelody.Clear();
            
            // Clear audit tracking (observational only)
            _currentAuditEntries.Clear();
            _currentScheduledRegions.Clear();
            _currentScheduledMelodyEvents.Clear();
            _pendingNoteOns.Clear();
            
            // PlaybackAudit: Wire up FMOD note event callback
            if (enablePlaybackAudit && synth != null)
            {
                synth.OnNoteEvent = OnFmodNoteEvent;
            }
            
            // Timeline v1: Schedule melody events in parallel with SATB chord playback
            // CRITICAL: Pass the same playbackStartTime to melody scheduler (no stale field)
            Coroutine melodySchedulingCoroutine = null;
            if (timelineMelodyEvents != null && timelineMelodyEvents.Count > 0)
            {
                melodySchedulingCoroutine = StartCoroutine(ScheduleTimelineMelodyEvents(timelineMelodyEvents, timelineSpec, key, playbackStartTime));
            }
            
            // Timeline highlighting: Start coroutine to update VoicingViewer highlight as playback progresses
            Coroutine highlightingCoroutine = null;
            if (voicingViewer != null && useVoicingTimelineView && regions != null && regions.Count > 0)
            {
                // Pass highlight colors from visual state settings
                voicingViewer.SetHighlightColors(visibleTint, highlightedTint);
                
                // Calculate total quarters for highlighting
                int totalQuarters = 0;
                if (regions.Count > 0)
                {
                    int maxTick = 0;
                    for (int r = 0; r < regions.Count; r++)
                    {
                        int regionEndTick = regions[r].startTick + regions[r].durationTicks;
                        if (regionEndTick > maxTick)
                            maxTick = regionEndTick;
                    }
                    totalQuarters = Mathf.CeilToInt(maxTick / (float)tpq);
                }
                
                highlightingCoroutine = StartCoroutine(UpdateVoicingViewerHighlight(playbackStartTime, totalQuarters, timelineSpec));
            }

            for (int i = 0; i < voicedChords.Count; i++)
            {
                var voiced = voicedChords[i];
                if (voiced.VoicesMidi == null || voiced.VoicesMidi.Length == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ChordLab] Voiced chord {i + 1} is null or empty, skipping");
                    continue;
                }

                // UNIFIED TIMING: Calculate chord target time using Time.time + offset from startTick
                float chordTargetTime;
                if (useRegionTiming && i < regions.Count)
                {
                    // target = startTime + SecondsFromTicks(region.startTick)
                    float offsetFromStart = SecondsFromTicks(regions[i].startTick, timelineSpec);
                    chordTargetTime = playbackStartTime + offsetFromStart;
                }
                else
                {
                    // Fallback: sequential timing (old behavior for backwards compatibility)
                    // Use previous chord end time (if any) + default duration
                    float lastEndTime = (i > 0) ? (playbackStartTime + SecondsFromTicks(regions[i-1].startTick + regions[i-1].durationTicks, timelineSpec)) : playbackStartTime;
                    chordTargetTime = lastEndTime;
                }
                
                // Wait until chord target time using WaitUntil (aligned with melody scheduling)
                float waitSeconds = Mathf.Max(0f, chordTargetTime - Time.time);
                float actualTime = Time.time;
                float delta = actualTime - chordTargetTime;
                
                if (waitSeconds > 0f)
                {
                    if (enableDebugLogs)
                    {
                        int startTick = (useRegionTiming && i < regions.Count) ? regions[i].startTick : -1;
                        Debug.Log($"[ChordLab] Waiting {waitSeconds:F4}s until chord {i + 1} target at {chordTargetTime:F4}s (Time.time={Time.time:F4}s, tick={startTick})");
                    }
                    yield return new WaitUntil(() => Time.time >= chordTargetTime);
                    actualTime = Time.time;
                    delta = actualTime - chordTargetTime;
                }
                else
                {
                    // Event is late - log and continue
                    LogPlaybackVerbose("SCHEDULER_LATE",
                        $"RUN={_currentPlaybackRunId} " +
                        $"type=Chord " +
                        $"index={i} " +
                        $"lateness={-delta:F4} " +
                        $"action=played_immediately");
                }
                
                // SCHEDULER_FIRE: Log when chord region fires
                actualTime = Time.time;
                delta = actualTime - chordTargetTime;
                int activeChordBeforeFire = _activeHandles.Count(kv => kv.Value.role.StartsWith("Chord"));
                int expectedChordNotesFire = (voiced.VoicesMidi != null) ? voiced.VoicesMidi.Length : 0;
                
                LogPlaybackVerbose("SCHEDULER_FIRE",
                    $"RUN={_currentPlaybackRunId} region={i} " +
                    $"scheduledTime={chordTargetTime:F4} actualTime={actualTime:F4} delta={delta:F4} " +
                    $"expectedChordNotes={expectedChordNotesFire} activeChordBefore={activeChordBeforeFire}");
                
                // REGION TRANSITION: Stop previous region's chord voices AT this region's start time
                // CRITICAL: This happens AFTER the wait, at the exact moment this region starts
                // This ensures previous region plays for its full duration, and we stop it only when the new region begins
                if (i > 0 && useRegionTiming && regions != null)
                {
                    int previousRegionIdx = i - 1;
                    
                    // REGION_EXIT: Log before stopping previous region
                    float exitTime = Time.time - _currentPlaybackStartTime;
                    int activeChordAfterExit = _activeHandles.Count(kv => kv.Value.role.StartsWith("Chord"));
                    int notesStopped = _activeHandlesByRegion.ContainsKey(previousRegionIdx) ? _activeHandlesByRegion[previousRegionIdx].Count : 0;
                    
                    LogPlaybackVerbose("REGION_EXIT",
                        $"RUN={_currentPlaybackRunId} " +
                        $"regionIndex={previousRegionIdx} " +
                        $"time={exitTime:F4} " +
                        $"reason=transition " +
                        $"activeChordAfter={activeChordAfterExit} " +
                        $"notesStopped={notesStopped}");
                    _regionsExited++;
                    
                    StopChordVoicesByRegion(previousRegionIdx, $"Region transition: stopping R{previousRegionIdx} at R{i} start time");
                }
                
                // REGION_ENTER: Log when entering a region
                float enterTime = Time.time - _currentPlaybackStartTime;
                int activeChordBefore = _activeHandles.Count(kv => kv.Value.role.StartsWith("Chord"));
                int expectedChordNotes = (voiced.VoicesMidi != null) ? voiced.VoicesMidi.Length : 0;
                
                // SNAPSHOT: Region enter
                LogPlaybackSnapshot("REGION_ENTER", _currentPlaybackRunId, i, expectedChordNotes);
                
                LogPlaybackVerbose("REGION_ENTER",
                    $"RUN={_currentPlaybackRunId} " +
                    $"regionIndex={i} " +
                    $"time={enterTime:F4} " +
                    $"expectedChordNotes={expectedChordNotes} " +
                    $"activeChordBefore={activeChordBefore}");
                _regionsEntered++;
                
                // PlaybackTrace: Log region start (observational only)
                if (enablePlaybackAudit || enablePlaybackTrace)
                {
                    float regionStartTime = Time.time;
                    float regionStartTimeFromStart = _currentPlaybackStartTime > 0f ? (regionStartTime - _currentPlaybackStartTime) : 0f;
                    string label = (useRegionTiming && i < regions.Count) ? (regions[i].debugLabel ?? "?") : "?";
                    if (enablePlaybackAudit)
                    {
                        UnityEngine.Debug.Log($"[PlaybackAudit] BEGIN REGION R{i} '{label}' at {regionStartTime:F4}s (offset={regionStartTimeFromStart:F4}s)");
                    }
                    if (enablePlaybackTrace)
                    {
                        UnityEngine.Debug.Log($"[TRACE] BEGIN REGION R{i} '{label}' at {regionStartTimeFromStart:F4}s");
                    }
                }

                // Log region headline at region start (user-facing console)
                LogRegionHeadline(i);
                
                // Reveal and highlight chord column for this region (progressive reveal)
                RevealChordColumn(i);
                HighlightChordColumn(i);

                if (enableDebugLogs)
                {
                    string label = (useRegionTiming && i < regions.Count) ? (regions[i].debugLabel ?? "?") : "?";
                    float actualPlayTime = Time.time;
                    Debug.Log($"[ChordLab] Playing voiced chord {i + 1}/{voicedChords.Count} '{label}' at Time.time={actualPlayTime:F4}s (target={chordTargetTime:F4}s): MIDI notes [{string.Join(", ", voiced.VoicesMidi)}]");
                }
                
                // Debug logging for melody register when tendency debug is enabled
                if (TheoryVoicing.GetTendencyDebug() && voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4)
                {
                    int bassMidi = voiced.VoicesMidi[0];
                    int sopranoMidi = voiced.VoicesMidi[voiced.VoicesMidi.Length - 1];
                    
                    // Get melody info for this step
                    string melodyInfo = "none";
                    if (chordEvents != null && i < chordEvents.Count && chordEvents[i].MelodyMidi.HasValue)
                    {
                        int melodyMidi = chordEvents[i].MelodyMidi.Value;
                        string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidi, key);
                        int melodyPc = (melodyMidi % 12 + 12) % 12;
                        int sopranoPc = (sopranoMidi % 12 + 12) % 12;
                        melodyInfo = $"{melodyName}({melodyMidi}, pc={melodyPc})";
                        
                        // Check if soprano matches melody pitch class
                        if (melodyPc != sopranoPc)
                        {
                            Debug.LogWarning($"[Playback Debug] Step {i + 1}: SOPRANO PITCH CLASS MISMATCH! " +
                                $"melodyPc={melodyPc}, sopranoPc={sopranoPc}, melodyMidi={melodyMidi}, sopranoMidi={sopranoMidi}");
                        }
                    }
                    
                    Debug.Log($"[Playback Debug] Step {i + 1}: Melody octave offset = {melodyOctaveOffset}, " +
                              $"melody={melodyInfo}, " +
                              $"Soprano MIDI (with offset) = {sopranoMidi}, " +
                              $"Bass MIDI = {bassMidi}");
                }

                // Update voicing viewer if available (only if not using timeline view)
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                // Pass VoicesMidi directly to VoicingViewer - it expects this exact order.
                if (voicingViewer != null && !useVoicingTimelineView)
                {
                    // Get corresponding chord event if available
                    ChordEvent? chordEvent = null;
                    if (chordEvents != null && i < chordEvents.Count)
                    {
                        chordEvent = chordEvents[i];
                    }

                    // Compute trailing spaces for duration-based spacing
                    int durationQuarters = GetDurationQuarters(i);
                    int trailingSpaces = GetVoicingPaddingSpaces(durationQuarters);
                    
                    // AUG5_SNAP: Log state in UI layer before VoicingViewer renders (for chord IV only)
                    if (i == 2 && voiced.VoicesMidi != null && voiced.VoicesMidi.Length >= 4 && chordEvent.HasValue) // chord IV (index 2)
                    {
                        var evt = chordEvent.Value;
                        string roman = TheoryChord.RecipeToRomanNumeral(evt.Key, evt.Recipe);
                        if (roman == "F" || roman == "IV")
                        {
                            var voices = voiced.VoicesMidi;
                            string bassName = TheoryPitch.GetPitchNameFromMidi(voices[0], evt.Key);
                            string tenorName = TheoryPitch.GetPitchNameFromMidi(voices[1], evt.Key);
                            string altoName = TheoryPitch.GetPitchNameFromMidi(voices[2], evt.Key);
                            string sopranoName = TheoryPitch.GetPitchNameFromMidi(voices[3], evt.Key);
                            UnityEngine.Debug.Log(
                                $"[AUG5_SNAP] stage=BeforeVoicingViewerRender chordIndex=2 roman={roman} SATB=[Bass={voices[0]}({bassName}), Tenor={voices[1]}({tenorName}), Alto={voices[2]}({altoName}), Soprano={voices[3]}({sopranoName})] source=voiced.VoicesMidi");
                        }
                    }
                    
                    // Pass VoicesMidi array directly - VoicingViewer will use index order [Bass, Tenor, Alto, Soprano]
                    voicingViewer.ShowVoicing(
                        key,
                        stepIndex: i + 1,
                        totalSteps: voicedChords.Count,
                        midiNotes: voiced.VoicesMidi,
                        chordEvent: chordEvent,
                        trailingSpaces: trailingSpaces);
                }

                // Schedule chord playback at this region's onset time
                // Use existing PlayChord helper which handles bass doubling and synth playback
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                // PlayChord receives the same VoicesMidi array that VoicingViewer uses.
                // Note: PlayChord's duration parameter is for the synth note duration
                float chordNoteDuration = chordDurationSeconds; // Default fallback
                ChordRegion? currentRegion = null;

                if (useRegionTiming && i < regions.Count)
                {
                    currentRegion = regions[i];
                    chordNoteDuration = SecondsFromTicks(currentRegion.Value.durationTicks, timelineSpec);
                }
                
                // Timeline v1: Track harmony notes for keyboard display (if enabled)
                // Note: Only sustain B/T/A (indices 0-2). Soprano (index 3) is driven by MelodyEvent timeline.
                if (enableKeyboardTimelineTracking && voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                {
                    // Add only Bass/Tenor/Alto (indices 0-2) to active set at region start
                    // Exclude Soprano (index 3) - it's driven exclusively by melody timeline events
                    // voiced.VoicesMidi order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                    var harmonyNotes = new List<int>();
                    int harmonyVoiceCount = Mathf.Min(3, voiced.VoicesMidi.Length); // Include indices 0, 1, 2 (B/T/A), exclude index 3 (Soprano)
                    for (int voiceIdx = 0; voiceIdx < harmonyVoiceCount; voiceIdx++)
                    {
                        int midi = voiced.VoicesMidi[voiceIdx];
                        AddActiveNote(midi);
                        harmonyNotes.Add(midi);
                    }
                    
                    // Schedule removal at region end (if using region timing)
                    // Only remove the B/T/A notes that were added
                    if (useRegionTiming && currentRegion.HasValue && harmonyNotes.Count > 0)
                    {
                        float regionEndTime = playbackStartTime + SecondsFromTicks(
                            currentRegion.Value.startTick + currentRegion.Value.durationTicks, 
                            timelineSpec);
                        float delayUntilEnd = regionEndTime - Time.time;
                        if (delayUntilEnd > 0f)
                        {
                            StartCoroutine(CoRemoveHarmonyNotesAfterDelay(
                                harmonyNotes, 
                                delayUntilEnd, 
                                _keyboardPlaybackToken));
                        }
                    }
                }
                else if (!enableKeyboardTimelineTracking && pianoKeyboardDisplay != null && voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                {
                    // Legacy behavior: snapshot update per region
                    pianoKeyboardDisplay.SetActiveNotes(voiced.VoicesMidi);
                }
                
                // SANITY CHECK: If duration is too small, log error and skip
                if (chordNoteDuration <= 0.001f)
                {
                    int durationTicks = currentRegion.HasValue ? currentRegion.Value.durationTicks : -1;
                    int specTpq = timelineSpec?.ticksPerQuarter ?? -1;
                    bool hasTimelineSpec = timelineSpec != null;
                    UnityEngine.Debug.LogError($"[ChordSchedule ERROR] Region R{i}: chordNoteDuration={chordNoteDuration:F6}s <= 0.001s! " +
                        $"region.durationTicks={durationTicks}, " +
                        $"timelineSpec={hasTimelineSpec}, tpq={specTpq}, " +
                        $"chordDurationSeconds={chordDurationSeconds}. Skipping region.");
                    continue; // Skip this region
                }
                
                // ChordSchedule debug logging
                if (enableChordScheduleDebug && currentRegion.HasValue)
                {
                    var region = currentRegion.Value;
                    float regionStartSeconds = SecondsFromTicks(region.startTick, timelineSpec);
                    float regionDurationSeconds = SecondsFromTicks(region.durationTicks, timelineSpec);
                    float noteOnTargetSeconds = playbackStartTime + regionStartSeconds;
                    float noteOffTargetSeconds = noteOnTargetSeconds + regionDurationSeconds;
                    string midiList = voiced.VoicesMidi != null ? string.Join(",", voiced.VoicesMidi) : "null";
                    
                    UnityEngine.Debug.Log($"[ChordSchedule] R{i}: startTick={region.startTick} durationTicks={region.durationTicks}, " +
                        $"regionStartSeconds={regionStartSeconds:F4}s, regionDurationSeconds={regionDurationSeconds:F4}s, " +
                        $"noteOnTarget={noteOnTargetSeconds:F4}s, noteOffTarget={noteOffTargetSeconds:F4}s, " +
                        $"MIDI=[{midiList}]");
                }
                
                    if (enableDebugLogs)
                    {
                    string label = currentRegion.HasValue ? (currentRegion.Value.debugLabel ?? "?") : "?";
                    float actualOnset = Time.time;
                    float regionOffset = currentRegion.HasValue ? SecondsFromTicks(currentRegion.Value.startTick, timelineSpec) : 0f;
                    float regionDuration = currentRegion.HasValue ? SecondsFromTicks(currentRegion.Value.durationTicks, timelineSpec) : chordDurationSeconds;
                    float quarters = currentRegion.HasValue ? (currentRegion.Value.durationTicks / (float)tpq) : 1f;
                    Debug.Log($"[ChordLab] Chord {i + 1} '{label}': playing at Time.time={actualOnset:F4}s (offset={regionOffset:F4}s from start), duration={regionDuration:F4}s (quarters={quarters:F2})");
                }
                
                float targetTimeFromStart = useRegionTiming ? SecondsFromTicks(regions[i].startTick, timelineSpec) : -1f;
                
                // Always pass full SATB (4 voices) to PlayChord for planning/validation.
                // PlayChord will internally drop soprano when rendering if melody-as-soprano mode is active.
                PlayChord(voiced.VoicesMidi, chordNoteDuration, regionIdx: i, targetTimeFromStart: targetTimeFromStart);
                
                // INVARIANT CHECK: After all chord notes for this region are scheduled (count only SATB voices, exclude embellishments)
                int activeChordAfterScheduled = _activeHandles.Count(kv => 
                    kv.Value.regionIdx == i &&
                    kv.Value.role.StartsWith("Chord") && 
                    !kv.Value.role.Contains("Doubling") &&
                    (kv.Value.role == "ChordB" || kv.Value.role == "ChordT" || kv.Value.role == "ChordA" || kv.Value.role == "ChordS"));
                if (activeChordAfterScheduled > expectedChordNotes)
                {
                    LogPlaybackWarn("INVARIANT_VIOLATION",
                        $"activeChordNotes={activeChordAfterScheduled} expected={expectedChordNotes} RUN={_currentPlaybackRunId} region={i} (after scheduling)");
                }
                
                // Note: We don't wait here - each chord is scheduled independently by its region.startTick
                // The loop continues immediately to schedule the next chord's wait
            }

            // Timeline v1: Wait for melody scheduling to complete (if it was started)
            if (melodySchedulingCoroutine != null)
            {
                yield return melodySchedulingCoroutine;
            }

            // END-OF-PLAYBACK CLEANUP: Wait until final region's end time, then stop all remaining active chord voices
            // The last chord region never gets stopped by "stop at next region start" logic
            if (useRegionTiming && regions != null && regions.Count > 0 && voicedChords.Count > 0)
            {
                int finalRegionIdx = regions.Count - 1;
                var finalRegion = regions[finalRegionIdx];
                
                // Calculate final region's end time (start + duration)
                float finalRegionStartSec = SecondsFromTicks(finalRegion.startTick, timelineSpec);
                float finalRegionDurationSec = SecondsFromTicks(finalRegion.durationTicks, timelineSpec);
                float finalRegionEndSec = finalRegionStartSec + finalRegionDurationSec;
                float playbackEndTime = _currentPlaybackStartTime + finalRegionEndSec;
                
                // [PLAYBACK_END_SCHEDULED]: Log when we schedule the wait for playback end
                float now = Time.time;
                LogPlaybackInfo("PLAYBACK_END_SCHEDULED",
                    $"RUN={_currentPlaybackRunId} " +
                    $"finalRegionIdx={finalRegionIdx} finalRegionStartSec={finalRegionStartSec:F4} finalRegionDurationSec={finalRegionDurationSec:F4} " +
                    $"finalRegionEndSec={finalRegionEndSec:F4} playbackEndTime={playbackEndTime:F4} now={now:F4}");
                
                // Wait until the final region's end time is reached
                // CRITICAL: This ensures the last region plays for its full duration before cleanup
                while (Time.time < playbackEndTime)
                {
                    yield return null; // Wait one frame and check again
                }
                
                // [PLAYBACK_END_REACHED]: Log when we've reached the end time and cleanup is allowed
                now = Time.time;
                LogPlaybackInfo("PLAYBACK_END_REACHED",
                    $"RUN={_currentPlaybackRunId} " +
                    $"now={now:F4} targetEndTime={playbackEndTime:F4} delta={now - playbackEndTime:F4}");
                
                // Add call site logging before cleanup
                string stackTrace = System.Environment.StackTrace;
                string[] stackLines = stackTrace.Split('\n');
                string formattedStack = string.Join("\n", stackLines.Take(5));
                LogPlaybackStack("CLEANUP_CALLSITE",
                    $"RUN={_currentPlaybackRunId} regionIndex={finalRegionIdx} " +
                    $"time={now:F4} reason=end_of_playback",
                    formattedStack);
                
                StopChordVoicesByRegion(finalRegionIdx, "End-of-playback cleanup");
            }
            
            // Timeline v1: Clear active notes at end of playback
            ClearActiveNotes();
            
            // Clear highlighting when playback completes
            if (voicingViewer != null)
            {
                voicingViewer.SetHighlightedStep(-1);
            }
            
            // [PLAYBACK_END] summary log
            float endTime = Time.time;
            float elapsed = _currentPlaybackStartTime > 0f ? (endTime - _currentPlaybackStartTime) : 0f;
            LogPlaybackInfo("PLAYBACK_END",
                $"runId={_currentPlaybackRunId} elapsed={elapsed:F4} endSec={endTime:F4} cleanupReason=completed");
            
            // Melody notes should self-cleanup via ReleaseAfter coroutines - no force-stop needed

            // PlaybackTrace: Compact summary (observational only, independent of audit)
            if (enablePlaybackTrace)
            {
                LogPlaybackTrace(regions, timelineMelodyEvents, key, timelineSpec);
            }

            // PlaybackAudit: Print summary comparing expected vs actual
            if (enablePlaybackAudit)
            {
                UnityEngine.Debug.Log($"[PlaybackAudit] === PLAYBACK SUMMARY ===");
                
                // Count expected vs actual chord regions
                int expectedChordRegions = _currentScheduledRegions.Count;
                var actualChordRegions = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Chord").Select(e => e.regionIdx).Distinct().ToList();
                int actualChordRegionCount = actualChordRegions.Count;
                
                UnityEngine.Debug.Log($"[PlaybackAudit] Chord Regions: Expected={expectedChordRegions}, Actual={actualChordRegionCount}");
                if (actualChordRegionCount < expectedChordRegions)
                {
                    var missingRegions = _currentScheduledRegions.Where(r => !actualChordRegions.Contains(r.regionIdx)).ToList();
                    if (missingRegions.Count > 0)
                    {
                        var missingStr = string.Join(", ", missingRegions.Select(r => $"#{r.regionIdx}({r.label})"));
                        UnityEngine.Debug.LogWarning($"[PlaybackAudit] MISSING Chord Regions: {missingStr}");
                    }
                }
                
                // Count expected vs actual melody events
                int expectedMelodyEvents = _currentScheduledMelodyEvents.Count;
                var actualMelodyIndices = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Melody").Select(e => e.melodyIdx).Distinct().ToList();
                int actualMelodyEventCount = actualMelodyIndices.Count;
                
                // Check for voice collisions
                var collisions = _currentAuditEntries.Where(e => e.isVoiceCollision).ToList();
                if (collisions.Count > 0)
                {
                    UnityEngine.Debug.LogWarning($"[PlaybackAudit] VOICE COLLISIONS DETECTED: {collisions.Count} collisions");
                    foreach (var col in collisions)
                    {
                        UnityEngine.Debug.LogWarning($"[PlaybackAudit]   Collision: {col.kind} {col.voice} {col.noteName}({col.midi}) at {col.actualTimeFromStart:F4}s, instance={col.fmodInstanceId}");
                    }
                }
                
                // Compute actual held duration per chord note (actualOff - actualOn)
                var chordNoteOns = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Chord").ToList();
                var chordNoteOffs = _currentAuditEntries.Where(e => e.eventType == "NoteOff" && e.kind == "Chord").ToList();
                
                if (enablePlaybackAudit && chordNoteOns.Count > 0)
                {
                    UnityEngine.Debug.Log($"[PlaybackSummary] Chord note hold durations:");
                    foreach (var noteOn in chordNoteOns)
                    {
                        var matchingOff = chordNoteOffs
                            .Where(e => e.midi == noteOn.midi && e.regionIdx == noteOn.regionIdx && e.voice == noteOn.voice)
                            .OrderBy(e => e.actualTimeFromStart)
                            .FirstOrDefault();
                        
                        if (matchingOff != null)
                        {
                            float heldDuration = matchingOff.actualTimeFromStart - noteOn.actualTimeFromStart;
                            string regionStr = noteOn.regionIdx.HasValue ? $"R{noteOn.regionIdx.Value}" : "-";
                            UnityEngine.Debug.Log($"[PlaybackSummary]   Chord {regionStr} {noteOn.voice} {noteOn.noteName}({noteOn.midi}): held={heldDuration:F4}s (On={noteOn.actualTimeFromStart:F4}s, Off={matchingOff.actualTimeFromStart:F4}s)");
                        }
                        else
                        {
                            string regionStr = noteOn.regionIdx.HasValue ? $"R{noteOn.regionIdx.Value}" : "-";
                            UnityEngine.Debug.LogWarning($"[PlaybackSummary]   Chord {regionStr} {noteOn.voice} {noteOn.noteName}({noteOn.midi}): NO NoteOff found!");
                        }
                    }
                }
                
                // Check for notes that never had NoteOff (already handled above with separated tracking)
                
                UnityEngine.Debug.Log($"[PlaybackAudit] Melody Events: Expected={expectedMelodyEvents}, Actual={actualMelodyEventCount}");
                if (actualMelodyEventCount < expectedMelodyEvents)
                {
                    var missingMelodies = _currentScheduledMelodyEvents.Where(m => !actualMelodyIndices.Contains(m.melodyIdx)).ToList();
                    if (missingMelodies.Count > 0)
                    {
                        var missingStr = string.Join(", ", missingMelodies.Select(m => $"#{m.melodyIdx}(MIDI{m.midi})"));
                        UnityEngine.Debug.LogWarning($"[PlaybackAudit] MISSING Melody Events: {missingStr}");
                    }
                }
                
                // Check final melody event
                if (_currentScheduledMelodyEvents.Count > 0)
                {
                    var finalScheduled = _currentScheduledMelodyEvents[_currentScheduledMelodyEvents.Count - 1];
                    var finalActual = _currentAuditEntries.Where(e => e.eventType == "NoteOn" && e.kind == "Melody" && e.melodyIdx == finalScheduled.melodyIdx).FirstOrDefault();
                    if (finalActual != null)
                    {
                        UnityEngine.Debug.Log($"[PlaybackAudit] Final Melody Event: Expected={finalScheduled.midi}({TheoryPitch.GetPitchNameFromMidi(finalScheduled.midi, key)}), Actual={finalActual.midi}({finalActual.noteName}), instance={finalActual.fmodInstanceId}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[PlaybackAudit] Final Melody Event: Expected={finalScheduled.midi}({TheoryPitch.GetPitchNameFromMidi(finalScheduled.midi, key)}), Actual=MISSING");
                    }
                }
                
                // List all audit entries in chronological order (separate NoteOn and NoteOff)
                var sortedEntries = _currentAuditEntries.OrderBy(e => e.actualTimeFromStart).ThenBy(e => e.eventType == "NoteOff" ? 1 : 0).ToList();
                int noteOnCount = sortedEntries.Count(e => e.eventType == "NoteOn");
                int noteOffCount = sortedEntries.Count(e => e.eventType == "NoteOff");
                UnityEngine.Debug.Log($"[PlaybackAudit] Total Events: {sortedEntries.Count} (NoteOn: {noteOnCount}, NoteOff: {noteOffCount})");
                
                // Group by region/melody and show instance IDs
                foreach (var entry in sortedEntries)
                {
                    string regionStr = entry.regionIdx.HasValue ? $"R{entry.regionIdx.Value}" : "-";
                    string melodyStr = entry.melodyIdx.HasValue ? $"M{entry.melodyIdx.Value}" : "-";
                    string collisionStr = entry.isVoiceCollision ? " [COLLISION!]" : "";
                    string instanceStr = entry.fmodInstanceId != "pending" ? $" instance={entry.fmodInstanceId}" : "";
                    string targetStr = entry.targetTimeFromStart >= 0f ? $" target={entry.targetTimeFromStart:F4}s" : "";
                    UnityEngine.Debug.Log($"[PlaybackAudit]   {entry.eventType} {entry.kind} {regionStr}/{melodyStr} {entry.voice} {entry.noteName}({entry.midi}){targetStr} actual={entry.actualTimeFromStart:F4}s{instanceStr}{collisionStr}");
                }
                
                // Cleanup callback
                if (synth != null)
                {
                    synth.OnNoteEvent = null;
                }
            }

            if (enableDebugLogs)
                Debug.Log("[ChordLab] Naive harmonization playback complete");

            // Voicing viewer is NOT cleared here - accumulated SATB sequence remains visible after playback
        }

        /// <summary>
        /// Timeline v1: For each region, find the melody note sounding at region.startTick.
        /// Returns null for a region if no melody note is active at that boundary.
        /// Used to anchor soprano voicing to the timeline melody.
        /// </summary>
        private List<int?> BuildBoundaryMelodyAnchors(
            IReadOnlyList<ChordRegion> regions,
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> melodyEvents)
        {
            var anchors = new List<int?>(regions.Count);

            for (int r = 0; r < regions.Count; r++)
            {
                int regionStart = regions[r].startTick;
                int? anchorMidi = null;

                if (melodyEvents != null)
                {
                    for (int i = 0; i < melodyEvents.Count; i++)
                    {
                        var me = melodyEvents[i];
                        int eventStart = me.startTick;
                        int eventEnd = me.startTick + me.durationTicks;

                        // "Sounding at boundary": regionStart ∈ [eventStart, eventEnd)
                        if (regionStart >= eventStart && regionStart < eventEnd)
                        {
                            anchorMidi = me.midi;
                            break;
                        }
                    }
                }

                anchors.Add(anchorMidi);
            }

            return anchors;
        }

        /// <summary>
        /// Timeline v1: Schedules melody events independently on the timeline.
        /// Plays melody notes using their startTick and durationTicks timing.
        /// Runs in parallel with SATB chord playback.
        /// 
        /// Design note: The melody lane is a separate overlay instrument, not a replacement for the soprano.
        /// Full SATB (including soprano) is still played by PlayChord. In a future "Option C" we may
        /// integrate melody with structural soprano (NCT handling, etc.), but for Timeline v1 we prefer
        /// complete chord tone coverage over avoiding doublings.
        /// </summary>
        /// <summary>
        /// Coroutine that updates VoicingViewer highlighting based on current playback time.
        /// Tracks the current quarter-step and updates highlight in real-time.
        /// </summary>
        private IEnumerator UpdateVoicingViewerHighlight(float playbackStartTime, int totalQuarters, TimelineSpec timelineSpec)
        {
            if (voicingViewer == null || timelineSpec == null)
                yield break;
            
            int ticksPerQuarter = timelineSpec.ticksPerQuarter > 0 ? timelineSpec.ticksPerQuarter : 4;
            float quarterNoteSeconds = chordDurationSeconds; // One quarter = chordDurationSeconds
            
            // Calculate playback end time
            float playbackEndTime = playbackStartTime + (totalQuarters * quarterNoteSeconds);
            
            // Update highlight every frame while playback is active
            while (Time.time < playbackEndTime && _currentPlaybackRunId > 0)
            {
                // Calculate elapsed time since playback start
                float elapsedTime = Time.time - playbackStartTime;
                
                // Calculate current quarter-step (0-based)
                int currentStep = Mathf.FloorToInt(elapsedTime / quarterNoteSeconds);
                
                // Clamp to valid range
                if (currentStep >= 0 && currentStep < totalQuarters)
                {
                    voicingViewer.SetHighlightedStep(currentStep);
                    // Phase 1: Update piano roll highlight to match voicing viewer
                    if (melodyPianoRoll != null)
                    {
                        melodyPianoRoll.SetHighlightedStep(currentStep);
                    }
                }
                else if (currentStep >= totalQuarters)
                {
                    // Past end - clear highlight
                    voicingViewer.SetHighlightedStep(-1);
                    if (melodyPianoRoll != null)
                    {
                        melodyPianoRoll.SetHighlightedStep(-1);
                    }
                    yield break;
                }
                
                yield return null; // Wait one frame
            }
            
            // Clear highlight when playback ends
            voicingViewer.SetHighlightedStep(-1);
            if (melodyPianoRoll != null)
            {
                melodyPianoRoll.SetHighlightedStep(-1);
            }
        }
        
        private IEnumerator ScheduleTimelineMelodyEvents(
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> melodyEvents,
            TimelineSpec spec,
            TheoryKey key,
            float playbackStartTime)
        {
            // CRITICAL DEBUG: Always log entry to confirm scheduler is called
            UnityEngine.Debug.Log($"[MelodyScheduler ENTER] count={melodyEvents?.Count ?? 0} time={Time.time:F3}\n{System.Environment.StackTrace}");
            
            if (melodyEvents == null || melodyEvents.Count == 0 || synth == null)
            {
                UnityEngine.Debug.Log("[ChordLab Timeline] ScheduleTimelineMelodyEvents EXIT: No events or synth is null");
                yield break;
            }

            int ticksPerQuarter = spec != null ? spec.ticksPerQuarter : 4;
            // CRITICAL: Use passed playbackStartTime parameter (same as chord scheduler), not stale field
            
            // Sort by startTick (should already be sorted, but ensure deterministic ordering)
            var sortedEvents = new List<Sonoria.MusicTheory.Timeline.MelodyEvent>(melodyEvents);
            sortedEvents.Sort((a, b) =>
            {
                int tickCompare = a.startTick.CompareTo(b.startTick);
                return tickCompare != 0 ? tickCompare : a.midi.CompareTo(b.midi);
            });

            if (enableDebugLogs)
                Debug.Log($"[ChordLab Timeline] Scheduling {sortedEvents.Count} melody events starting at Time.time={playbackStartTime:F4}s");

            // Tracks, per MIDI, when the last scheduled melody note ends (seconds from playback start)
            Dictionary<int, float> melodyNoteEndTimes = new Dictionary<int, float>();

            // Track event index for instrumentation of last 2 events
            int eventIndex = 0;
            foreach (var melodyEvent in sortedEvents)
            {
                // Calculate target time using Time.time + offset from startTick (same as chord scheduling)
                // target = startTime + SecondsFromTicks(melodyEvent.startTick)
                float offsetFromStart = SecondsFromTicks(melodyEvent.startTick, spec);
                float targetTime = playbackStartTime + offsetFromStart;
                
                // INSTRUMENTATION: Log trigger details for last 2 events (only if melody timeline debug enabled)
                bool isLastTwoEvents = (eventIndex >= sortedEvents.Count - 2);
                if (isLastTwoEvents && enableMelodyTimelineDebug)
                {
                    float currentTime = Time.time;
                    float timeFromStart = currentTime - playbackStartTime;
                    string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                    UnityEngine.Debug.Log($"[MELODY_SCHEDULER] Event #{eventIndex}/{sortedEvents.Count - 1} (last 2): noteName={noteName} midi={melodyEvent.midi}");
                    UnityEngine.Debug.Log($"[MELODY_SCHEDULER]   startTick={melodyEvent.startTick} offsetFromStart={offsetFromStart:F4}s targetTime={targetTime:F4}s");
                    UnityEngine.Debug.Log($"[MELODY_SCHEDULER]   Time.time={currentTime:F4}s, actualTimeFromStart={timeFromStart:F4}s, delta={timeFromStart - offsetFromStart:F4}s");
                }
                
                // Wait until target time using WaitUntil (aligned with chord scheduling)
                float waitSeconds = Mathf.Max(0f, targetTime - Time.time);
                if (waitSeconds > 0f)
                {
                    if (enableDebugLogs || isLastTwoEvents)
                        Debug.Log($"[ChordLab Timeline] Waiting {waitSeconds:F4}s until melody event target at {targetTime:F4}s (Time.time={Time.time:F4}s, tick={melodyEvent.startTick})");
                    yield return new WaitUntil(() => Time.time >= targetTime);
                }
                else if (enableDebugLogs || isLastTwoEvents)
                {
                    Debug.Log($"[ChordLab Timeline] Melody event target {targetTime:F4}s already passed (Time.time={Time.time:F4}s, tick={melodyEvent.startTick}), playing immediately");
                }

                    // Play the melody note
                    if (synth != null)
                    {
                        // Use PlayOnce with duration (approximate note-off via duration)
                        // Duration = (durationTicks / ticksPerQuarter) * chordDurationSeconds
                        float noteDuration = SecondsFromTicks(melodyEvent.durationTicks, spec);
                        
                        float actualPlayTime = Time.time;
                        float actualTimeFromStart = actualPlayTime - playbackStartTime;
                        
                        // Get note name once for use throughout this scope
                        string noteName = TheoryPitch.GetPitchNameFromMidi(melodyEvent.midi, key);
                        
                        // PlaybackAudit: Track pending note-on BEFORE FMOD call (callback will create actual audit entry)
                        // CRITICAL: Only call synth.PlayOnce once per MelodyEvent to prevent duplicate NoteOn
                        if (enablePlaybackAudit)
                        {
                            _pendingNoteOns[melodyEvent.midi] = (
                                kind: "Melody",
                                regionIdx: null,
                                melodyIdx: eventIndex,
                                voice: "Mel",
                                targetTimeFromStart: offsetFromStart
                            );
                        }
                        
                        // GATED: Verbose FMOD logs only when debug logs enabled (separate from PlaybackAudit)
                        if (enableDebugLogs || isLastTwoEvents)
                        {
                            Debug.Log($"[ChordLab Timeline] Playing melody: {noteName}({melodyEvent.midi}) at Time.time={actualPlayTime:F4}s (actualTimeFromStart={actualTimeFromStart:F4}s, offset={offsetFromStart:F4}s, tick={melodyEvent.startTick}), duration={noteDuration:F4}s");
                        }

                        // Timeline v1: Add melody note to keyboard active set
                        if (enableKeyboardTimelineTracking)
                        {
                            AddActiveNote(melodyEvent.midi);
                            
                            // Schedule removal after note duration
                            StartCoroutine(CoRemoveNoteAfterDelay(
                                melodyEvent.midi, 
                                noteDuration, 
                                _keyboardPlaybackToken));
                        }
                        
                        // Compute when this event will start and end, in seconds from playback start
                        // CRITICAL: Use offsetFromStart (scheduled time) for comparison, not actualTimeFromStart
                        // This ensures we compare scheduled times, not actual play times (which may drift)
                        float noteStartTime = offsetFromStart;
                        float noteEndTime = noteStartTime + noteDuration;
                        
                        // DEBUG: Log scheduling details before decision logic
                        UnityEngine.Debug.Log($"[MelodySchedule] idx={eventIndex} midi={melodyEvent.midi} ({noteName}) offsetFromStart={offsetFromStart:F3} noteDuration={noteDuration:F3}");
                    
                    bool shouldStopExistingNote = true;
                    bool shouldCreateNewInstance = true;
                    
                    // See if we've already scheduled a note for this MIDI
                    if (melodyNoteEndTimes.TryGetValue(melodyEvent.midi, out float previousEndTime))
                    {
                        // If the previous note for this MIDI is still within its duration,
                        // don't stop it just to restart the same pitch
                        // (treat this as a sustain/overlap case)
                        if (noteStartTime < previousEndTime)
                        {
                            shouldStopExistingNote = false;
                            shouldCreateNewInstance = false; // Don't create overlapping instance
                            
                            // ENHANCED DEBUG: Log detailed overlap detection (always log for diagnosis)
                            if (true) // Always log these critical messages for debugging
                            {
                                float overlapDuration = previousEndTime - noteStartTime;
                                Debug.Log($"[ChordLab Timeline] SUSTAINING melody note: {noteName} ({melodyEvent.midi}) - " +
                                    $"previous note active until {previousEndTime:0.000}s, " +
                                    $"current scheduled start={noteStartTime:0.000}s, " +
                                    $"overlap={overlapDuration:0.000}s, " +
                                    $"eventIndex={eventIndex}");
                            }
                        }
                        else
                        {
                            // Previous note has ended - this is a retrigger
                            if (true) // Always log these critical messages for debugging
                            {
                                float gap = noteStartTime - previousEndTime;
                                Debug.Log($"[ChordLab Timeline] RETRIGGERING melody note: {noteName} ({melodyEvent.midi}) - " +
                                    $"previous note ended at {previousEndTime:0.000}s, " +
                                    $"current scheduled start={noteStartTime:0.000}s, " +
                                    $"gap={gap:0.000}s, " +
                                    $"eventIndex={eventIndex}");
                            }
                        }
                    }
                    else
                    {
                        // First time this MIDI is being played
                        if (true) // Always log these critical messages for debugging
                        {
                            Debug.Log($"[ChordLab Timeline] FIRST melody note: {noteName} ({melodyEvent.midi}) - " +
                                $"scheduled start={noteStartTime:0.000}s, " +
                                $"eventIndex={eventIndex}");
                        }
                    }
                    
                    // DEBUG: Log decision before NoteOff/NoteOn
                    UnityEngine.Debug.Log($"[MelodyDecision] idx={eventIndex} midi={melodyEvent.midi} ({noteName}) " +
                        $"shouldStopExisting={shouldStopExistingNote} " +
                        $"shouldCreateNewInstance={shouldCreateNewInstance} " +
                        $"noteStart={noteStartTime:F3} noteEnd={noteEndTime:F3}");
                    
                    // If it's safe to retrigger (no active overlap), stop any previous instance
                    if (shouldStopExistingNote)
                    {
                        synth.NoteOff(melodyEvent.midi);
                        
                        if (true) // Always log these critical messages for debugging
                        {
                            Debug.Log($"[ChordLab Timeline] Called NoteOff for {noteName} ({melodyEvent.midi}) before retrigger, eventIndex={eventIndex}");
                        }
                    }
                    
                    // Only create a new instance if we're not sustaining
                    FMOD.Studio.EventInstance instance = default(FMOD.Studio.EventInstance);
                    if (shouldCreateNewInstance)
                    {
                        // Use PlayOnceWithInstance to get the instance handle
                        instance = synth.PlayOnceWithInstance(melodyEvent.midi, melodyVelocity, noteDuration);
                        
                        // DEBUG: Log NoteOn after PlayOnceWithInstance call
                        UnityEngine.Debug.Log($"[MelodyNoteOn] idx={eventIndex} midi={melodyEvent.midi} ({noteName}) schedOffset={offsetFromStart:F3}");
                        
                        if (true) // Always log these critical messages for debugging
                        {
                            if (instance.isValid())
                            {
                                Debug.Log($"[ChordLab Timeline] Created new instance for {noteName} ({melodyEvent.midi}), eventIndex={eventIndex}");
                            }
                            else
                            {
                                Debug.LogWarning($"[ChordLab Timeline] FAILED to create instance for {noteName} ({melodyEvent.midi}) - NoteOn returned invalid, eventIndex={eventIndex}");
                            }
                        }
                    }
                    else
                    {
                        // Sustaining - log that we're skipping instance creation
                        if (true) // Always log these critical messages for debugging
                        {
                            Debug.Log($"[ChordLab Timeline] SKIPPING instance creation for sustaining note: {noteName} ({melodyEvent.midi}) - " +
                                $"previous note still active, eventIndex={eventIndex}");
                        }
                    }
                    
                    // Always update the end time for this MIDI (even when sustaining, to track the extended duration)
                    melodyNoteEndTimes[melodyEvent.midi] = noteEndTime;
                    
                    if (instance.isValid())
                    {
                        string instanceId = instance.handle.ToString();
                        
                        // Count active melody handles before registration
                        int activeMelodyBefore = _activeHandles.Count(kv => kv.Value.role == "Melody");
                        
                        // CENTRALIZED TRACKING: Register handle in single source of truth
                        var handle = new NoteHandle
                        {
                            instanceId = instanceId,
                            midi = melodyEvent.midi,
                            role = "Melody",
                            regionIdx = null,
                            melodyIdx = eventIndex,
                            scheduledOnTime = offsetFromStart,
                            instance = instance
                        };
                        
                        _activeHandles[instanceId] = handle;
                        
                        // Also track by melody index for potential future use
                        if (!_activeHandlesByMelody.ContainsKey(eventIndex))
                        {
                            _activeHandlesByMelody[eventIndex] = new List<string>();
                        }
                        _activeHandlesByMelody[eventIndex].Add(instanceId);
                        
                        // Count active melody handles after registration
                        int activeMelodyAfter = _activeHandles.Count(kv => kv.Value.role == "Melody");
                        
                        // MELODY_NOTE_ON: Log every melody note registration
                        LogPlaybackVerbose("MELODY_NOTE_ON",
                            $"RUN={_currentPlaybackRunId} " +
                            $"melodyIndex={eventIndex} " +
                            $"midi={melodyEvent.midi} " +
                            $"instanceId={instanceId} " +
                            $"time={actualTimeFromStart:F4} " +
                            $"activeMelodyBefore={activeMelodyBefore} " +
                            $"activeMelodyAfter={activeMelodyAfter}");
                        _melodyNotesOn++;
                        
                        // PlaybackTrace: Log NoteOn
                        if (enablePlaybackTrace)
                        {
                            UnityEngine.Debug.Log($"[TRACE] ON t={actualTimeFromStart:F3}s sched={offsetFromStart:F3}s e={eventIndex} role=Melody midi={melodyEvent.midi} inst={instanceId}");
                        }
                    }
                }
                
                eventIndex++;
            }

            if (enableDebugLogs)
                Debug.Log("[ChordLab Timeline] Melody scheduling complete");
        }

        /// <summary>
        /// Logs voice movement between consecutive voiced chords.
        /// Expects chords to be ordered from bass to soprano, with 3 or 4 voices.
        /// </summary>
        private void DebugLogVoiceMovementsForVoicedChords(
            TheoryKey key,
            List<int[]> voicedChords)
        {
            if (voicedChords == null || voicedChords.Count < 2)
            {
                Debug.LogWarning("[ChordLab] Not enough voiced chords to analyze voice movements.");
                return;
            }

            Debug.Log("[ChordLab] === Voice Movement Analysis (Naive Harmonization) ===");

            // Helper to name notes.
            string Name(int midi) => TheoryPitch.GetPitchNameFromMidi(midi, key);

            for (int i = 0; i < voicedChords.Count - 1; i++)
            {
                var from = voicedChords[i];
                var to = voicedChords[i + 1];

                if (from == null || to == null || from.Length == 0 || to.Length == 0)
                {
                    Debug.LogWarning($"[ChordLab] Skipping step {i + 1} → {i + 2} due to missing chord data.");
                    continue;
                }

                // Assume both chords have the same number of voices.
                int voiceCount = Mathf.Min(from.Length, to.Length);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Step {i + 1} → {i + 2}:");

                for (int v = 0; v < voiceCount; v++)
                {
                    int fromMidi = from[v];
                    int toMidi = to[v];
                    int diff = toMidi - fromMidi;

                    string voiceLabel;
                    switch (v)
                    {
                        case 0: voiceLabel = "Bass   "; break;
                        case 1: voiceLabel = "Tenor  "; break;
                        case 2: voiceLabel = "Alto   "; break;
                        default: voiceLabel = "Soprano"; break;
                    }

                    string direction =
                        diff > 0 ? "up" :
                        diff < 0 ? "down" :
                        "stay";

                    int semitones = Mathf.Abs(diff);

                    sb.AppendLine(
                        $"{voiceLabel}: {Name(fromMidi)} → {Name(toMidi)}  ({direction} {semitones} semitones)");
                }

                Debug.Log(sb.ToString());
            }
        }

        /// <summary>
        /// Builds the test melody, runs naive harmonization, voices it,
        /// and logs per-voice movement between consecutive chords.
        /// </summary>
        public void DebugLogNaiveHarmonizationVoiceMovements()
        {
#if UNITY_EDITOR
            if (enableDebugLogs)
                Debug.Log("[ChordLab] DebugLogNaiveHarmonizationVoiceMovements started.");

            // 1. Build the test melody line
            var key = GetKeyFromDropdowns();
            
            // Try note-name melody from Inspector first
            List<MelodyEvent> melodyLine = BuildNoteNameMelodyLineFromInspector();

            // Fallback: existing degree-based test melody
            if (melodyLine == null || melodyLine.Count == 0)
            {
                melodyLine = BuildTestMelodyLine(key);
            }

            if (melodyLine == null || melodyLine.Count == 0)
            {
                Debug.LogWarning("[ChordLab] No test melody available for voice movement analysis.");
                return;
            }

            // 2. Build heuristic settings from Inspector
            var settings = BuildHarmonyHeuristicSettings();

            // 3. Build naive harmonization steps
            var steps = TheoryHarmonization.BuildNaiveHarmonization(melodyLine, key, settings);
            if (steps == null || steps.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Naive harmonization produced no steps; nothing to analyze.");
                return;
            }

            // 4. Convert steps to chord events with melody
            var chordEvents = TheoryHarmonization.BuildChordEventsFromHarmonization(steps, key);
            if (chordEvents == null || chordEvents.Count == 0)
            {
                Debug.LogWarning("[ChordLab] No chord events constructed from naive harmonization.");
                return;
            }

            // 5. Use TheoryVoicing to voice-lead the progression with melody in soprano
            // Calculate upper voice MIDI ranges based on rootOctave
            var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
            
            // Debug logging for soprano range
            if (TheoryVoicing.GetTendencyDebug())
            {
                Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
            }

            var voicedChords = TheoryVoicing.VoiceLeadProgressionWithMelody(
                chordEvents,
                numVoices: 4,
                rootOctave: rootOctave,
                bassOctave: rootOctave - 1,
                upperMinMidi: upperMinMidi,
                upperMaxMidi: upperMaxMidi
            );

            if (voicedChords == null || voicedChords.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Voicing failed; nothing to analyze.");
                return;
            }

            // 6. Convert VoicedChord list to List<int[]> for analysis
            var voicedChordsMidi = new List<int[]>();
            foreach (var chord in voicedChords)
            {
                if (chord.VoicesMidi != null && chord.VoicesMidi.Length > 0)
                {
                    voicedChordsMidi.Add(chord.VoicesMidi);
                }
            }

            if (voicedChordsMidi.Count < 2)
            {
                Debug.LogWarning("[ChordLab] Not enough voiced chords to analyze voice movements.");
                return;
            }

            // 7. Analyze and log voice movements
            DebugLogVoiceMovementsForVoicedChords(key, voicedChordsMidi);
#endif
        }

        public void DebugLogProgressionVoicing()
        {
            try
            {
                // Get the current key from dropdowns
                var key = GetKeyFromDropdowns();

                // Get the progression text from the input field
                var text = progressionInput != null ? progressionInput.text : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.LogWarning("Chord Lab Voicing Debug: progression input is empty.");
                    return;
                }

                // Parse all tokens
                var tokens = text.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    Debug.LogWarning("Chord Lab Voicing Debug: no tokens found in progression input.");
                    return;
                }

                // Build list of ChordEvents with incremental TimeBeats
                var events = new List<ChordEvent>();
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!TheoryChord.TryParseRomanNumeral(key, tokens[i], out var recipe))
                    {
                        Debug.LogWarning($"Chord Lab Voicing Debug: failed to parse roman '{tokens[i]}' in key {key}. Skipping.");
                        continue;
                    }

                    events.Add(new ChordEvent
                    {
                        Key = key,
                        Recipe = recipe,
                        TimeBeats = i, // Incremental beats: 0, 1, 2, 3...
                        MelodyMidi = null
                    });
                }

                if (events.Count == 0)
                {
                    Debug.LogWarning("Chord Lab Voicing Debug: no valid chords parsed from progression.");
                    return;
                }

                // Voice the progression
                var voicedProgression = TheoryVoicing.VoiceLeadProgression(events);

                // Build a human-readable log
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Progression Voicing Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Progression: {text}");
                sb.AppendLine($"Total Chords: {voicedProgression.Count}");
                sb.AppendLine();

                for (int i = 0; i < voicedProgression.Count && i < events.Count; i++)
                {
                    var voiced = voicedProgression[i];
                    var evt = events[i];
                    string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                        key,
                        evt.Recipe.Degree,
                        evt.Recipe.RootSemitoneOffset);
                    string chordSymbol = TheoryChord.GetChordSymbol(key, evt.Recipe, rootNoteName);

                    sb.AppendLine($"Chord {i + 1}: {tokens[i]} ({chordSymbol})");
                    sb.AppendLine($"  TimeBeats: {voiced.TimeBeats}");
                    sb.AppendLine("  Voices (low→high):");

                    for (int v = 0; v < voiced.VoicesMidi.Length; v++)
                    {
                        int midi = voiced.VoicesMidi[v];
                        string noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                        sb.AppendLine($"    {v}: {midi} ({noteName})");
                    }

                    sb.AppendLine();
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Voicing Debug: Exception while logging progression voicing: {ex}");
            }
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only debug method that logs melody-constrained voicing for a test progression.
        /// </summary>
        public void DebugLogMelodyConstrainedVoicing()
        {
            try
            {
                // a) Get key from dropdowns
                var key = GetKeyFromDropdowns();

                // b) Build a small test progression (e.g. I IV ii V I)
                string[] testProgression = { "I", "IV", "ii", "V", "I" };
                int[] testMelodyDegrees = { 3, 4, 4, 2, 1 }; // Melody: 3, 4, 4, 2, 1
                int octave = 4; // C4 = MIDI 60

                var events = new List<ChordEvent>();
                for (int i = 0; i < testProgression.Length; i++)
                {
                    if (!TheoryChord.TryParseRomanNumeral(key, testProgression[i], out var recipe))
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning($"Chord Lab Melody-Constrained Voicing Debug: failed to parse '{testProgression[i]}' in key {key}. Skipping.");
                        continue;
                    }

                    // Convert melody degree to MIDI
                    int melodyMidi = -1;
                    if (i < testMelodyDegrees.Length)
                    {
                        melodyMidi = TheoryScale.GetMidiForDegree(key, testMelodyDegrees[i], octave);
                    }

                    events.Add(new ChordEvent
                    {
                        Key = key,
                        Recipe = recipe,
                        TimeBeats = i,
                        MelodyMidi = melodyMidi >= 0 ? (int?)melodyMidi : null
                    });
                }

                if (events.Count == 0)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning("Chord Lab Melody-Constrained Voicing Debug: no valid chords parsed.");
                    return;
                }

                // e) Call TheoryVoicing.VoiceLeadProgressionWithMelody
                // Use rootOctave from the controller settings
                var (upperMinMidi, upperMaxMidi) = ComputeUpperVoiceRange();
                
                // Debug logging for soprano range
                if (TheoryVoicing.GetTendencyDebug())
                {
                    Debug.Log($"[Range Debug] Soprano range: min={upperMinMidi} max={upperMaxMidi}");
                }
                
                var voicedProgression = TheoryVoicing.VoiceLeadProgressionWithMelody(
                    events,
                    numVoices: 4,
                    rootOctave: rootOctave,
                    bassOctave: rootOctave - 1,
                    upperMinMidi: upperMinMidi,
                    upperMaxMidi: upperMaxMidi
                );

                // f) Log a readable report
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[ChordLab Melody-Constrained Voicing Debug]");
                sb.AppendLine($"Key: {key}");
                sb.AppendLine($"Progression: {string.Join(" ", testProgression)}");
                sb.AppendLine($"Total chords: {voicedProgression.Count}");
                sb.AppendLine();

                for (int i = 0; i < voicedProgression.Count && i < events.Count; i++)
                {
                    var voiced = voicedProgression[i];
                    var evt = events[i];
                    
                    string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                        key,
                        evt.Recipe.Degree,
                        evt.Recipe.RootSemitoneOffset);
                    string chordSymbol = TheoryChord.GetChordSymbol(key, evt.Recipe, rootNoteName);

                    // Get melody info
                    string melodyInfo = "None";
                    if (evt.MelodyMidi.HasValue)
                    {
                        int melodyMidi = evt.MelodyMidi.Value;
                        string melodyName = TheoryPitch.GetPitchNameFromMidi(melodyMidi, key);
                        var melodyEvent = new MelodyEvent
                        {
                            TimeBeats = evt.TimeBeats,
                            DurationBeats = 1.0f,
                            Midi = melodyMidi
                        };
                        var analysis = TheoryMelody.AnalyzeEvent(key, melodyEvent);
                        melodyInfo = $"{melodyName} (degree {analysis.Degree})";
                    }

                    sb.AppendLine($"Chord {i + 1}: {testProgression[i]} ({chordSymbol})  Melody: {melodyInfo}");
                    sb.AppendLine("  Voices (low→high):");

                    for (int v = 0; v < voiced.VoicesMidi.Length; v++)
                    {
                        int midi = voiced.VoicesMidi[v];
                        string noteName = TheoryPitch.GetPitchNameFromMidi(midi, key);
                        string marker = (evt.MelodyMidi.HasValue && v == voiced.VoicesMidi.Length - 1 && midi == evt.MelodyMidi.Value) 
                            ? "  <-- matches melody" 
                            : "";
                        sb.AppendLine($"    {v}: {midi} ({noteName}){marker}");
                    }

                    sb.AppendLine();
                }

                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Chord Lab Melody-Constrained Voicing Debug: Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only menu item: Toggle augmented 5th debug logging.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Chord Lab/Toggle Augmented 5th Debug Logging")]
        public static void ToggleAug5DebugLogging()
        {
            bool currentState = Sonoria.MusicTheory.TheoryVoicing.GetAug5Debug();
            bool newState = !currentState;
            Sonoria.MusicTheory.TheoryVoicing.SetAug5Debug(newState);
            UnityEngine.Debug.Log($"[Chord Lab] Augmented 5th debug logging: {(newState ? "ENABLED" : "DISABLED")}");
        }
        
        /// <summary>
        /// Editor-only menu item: Run regression test suite.
        /// Only works when enableRegressionHarness is true.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Chord Lab/Run Regression Suite")]
        public static void RunRegressionSuite()
        {
            // Find ChordLabController instance
            ChordLabController controller = UnityEngine.Object.FindObjectOfType<ChordLabController>();
            if (controller == null)
            {
                UnityEngine.Debug.LogError("[Regression] ChordLabController not found in scene.");
                return;
            }

            // Check if regression harness is enabled
            if (!controller.enableRegressionHarness)
            {
                UnityEngine.Debug.LogWarning("[Regression] Regression harness is disabled. Enable 'Enable Regression Harness' in ChordLabController Inspector to run tests.");
                return;
            }

            // Sync the global flag with controller setting
            Sonoria.MusicTheory.RegressionHarness.EnableRegressionHarness = controller.enableRegressionHarness;

            // Run all cases
            // NOTE: voiceLeadFunc uses the exact same entry point as UI "Play/SATB" button:
            // - VoiceLeadRegions routes to VoiceLeadProgression (when useMelody=false) or VoiceLeadProgressionWithMelody (when useMelody=true)
            // - VoiceLeadProgression applies continuity adjustment (AdjustPlayVoicingForContinuity) to the first chord
            // - Same parameters: rootOctave, bassOctave, upperMinMidi, upperMaxMidi from ComputeUpperVoiceRange()
            // CRITICAL: buildRegionsFunc routes to the appropriate parser based on input type
            // Roman numerals (I, IV, V, etc.) use BuildRegionsFromRomanInput
            // Chord symbols (C, Fm, G7, etc.) use BuildRegionsFromChordSymbolInput
            // This ensures the same parsing and recipe generation as the UI.
            var report = Sonoria.MusicTheory.RegressionRunner.RunAllCases(
                buildRegionsFunc: (input, key, spec, melody) =>
                {
                    // Simple heuristic: If input starts with Roman numeral pattern (I, i, V, v, etc.), use Roman parser
                    // Otherwise, try chord symbol parser
                    char[] separators = { ' ', '\t', '\n', '\r' };
                    string[] tokens = input.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
                    bool looksLikeRoman = tokens.Length > 0 && tokens[0].Length > 0 && 
                                        (tokens[0][0] == 'I' || tokens[0][0] == 'i' || tokens[0][0] == 'V' || tokens[0][0] == 'v' ||
                                         tokens[0][0] == 'X' || tokens[0][0] == 'x' || tokens[0].StartsWith("b") || tokens[0].StartsWith("#"));
                    
                    if (looksLikeRoman)
                    {
                        // Try Roman numeral parser
                        return controller.BuildRegionsFromRomanInput(input, key, spec, melody);
                    }
                    else
                    {
                        // Try chord symbol parser
                        return controller.BuildRegionsFromChordSymbolInput(input, key, spec, melody);
                    }
                },
                voiceLeadFunc: (key, spec, regions, useMelody, numVoices, rootOct, bassOct, upperMin, upperMax, diags) =>
                {
                    var (upperMinMidi, upperMaxMidi) = controller.ComputeUpperVoiceRange();
                    return TheoryVoicing.VoiceLeadRegions(key, spec, regions, useMelody, numVoices, rootOct, bassOct, upperMinMidi, upperMaxMidi, diags);
                }
            );

            if (report != null)
            {
                DisplayRegressionReport(report);
            }
        }

        /// <summary>
        /// Displays regression report in console (only when regression harness is enabled).
        /// </summary>
        private static void DisplayRegressionReport(Sonoria.MusicTheory.RegressionReport report)
        {
            if (!Sonoria.MusicTheory.RegressionHarness.EnableRegressionHarness)
                return; // Silent when disabled

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== REGRESSION TEST REPORT ===");
            sb.AppendLine($"Cases: {report.caseCount}, Passed: {report.passCount}, Failed: {report.failCount}");
            
            // List included test case names
            sb.AppendLine("\nIncluded:");
            var allCaseNames = new HashSet<string>();
            if (report.caseDiagnostics != null)
            {
                foreach (var kvp in report.caseDiagnostics)
                {
                    allCaseNames.Add(kvp.Key);
                }
            }
            // Also collect from failures
            if (report.failures != null)
            {
                foreach (var failure in report.failures)
                {
                    allCaseNames.Add(failure.caseName);
                }
            }
            // Get all test cases from RegressionRunner
            var allCases = Sonoria.MusicTheory.RegressionRunner.GetAllCases();
            foreach (var testCase in allCases)
            {
                string status = allCaseNames.Contains(testCase.name) ? 
                    (report.failures != null && report.failures.Any(f => f.caseName == testCase.name) ? " [FAILED]" : " [PASSED]") : 
                    " [NOT RUN]";
                string checksStr = testCase.checks.ToString();
                sb.AppendLine($" - {testCase.name}{status} (checks: {checksStr})");
            }

            // Print detailed diagnostics only for failed cases (to keep output minimal for passing cases)
            // Collect set of failed case names
            var failedCaseNames = new HashSet<string>();
            if (report.failCount > 0)
            {
                foreach (var failure in report.failures)
                {
                    failedCaseNames.Add(failure.caseName);
                }
            }

            // Print diagnostic details only for failed cases
            if (report.caseDiagnostics != null && report.caseDiagnostics.Count > 0 && report.failCount > 0)
            {
                foreach (var kvp in report.caseDiagnostics)
                {
                    string caseName = kvp.Key;
                    var diags = kvp.Value;
                    
                    // Only show detailed diagnostics for failed cases
                    if (failedCaseNames.Contains(caseName))
                    {
                        sb.AppendLine($"\n--- Case: {caseName} (FAILED) ---");
                        
                        // Print final post-continuity BTAS MIDI pitches per region (same data used by UI for rendering/playback)
                        sb.AppendLine("Final Post-Continuity BTAS MIDI Pitches (per region):");
                        foreach (var voicing in diags.regionVoicings)
                        {
                            sb.AppendLine($"  Region {voicing.regionIndex} ({voicing.chordLabel}):");
                            sb.AppendLine($"    Bass:    MIDI {voicing.bassMidi} = {voicing.bassName}");
                            sb.AppendLine($"    Tenor:   MIDI {voicing.tenorMidi} = {voicing.tenorName}");
                            sb.AppendLine($"    Alto:    MIDI {voicing.altoMidi} = {voicing.altoName}");
                            sb.AppendLine($"    Soprano: MIDI {voicing.sopranoMidi} = {voicing.sopranoName}");
                        }
                        
                        // Print 7th resolution check details (checked against final post-continuity voicing)
                        if (diags.seventhResolutionChecks.Count > 0)
                        {
                            sb.AppendLine("\n=== 7th Resolution Check Details (per region transition) ===");
                            foreach (var check in diags.seventhResolutionChecks)
                            {
                                sb.AppendLine($"\n  Region {check.regionIndex} → {check.regionIndex + 1}:");
                                
                                // Computed seventhPc (or "none")
                                if (check.seventhPc >= 0)
                                {
                                    sb.AppendLine($"    Computed seventhPc: {check.seventhPc} ({check.seventhPcName})");
                                }
                                else
                                {
                                    sb.AppendLine($"    Computed seventhPc: none ({check.seventhPcName})");
                                }
                                
                                // Which voice holds it in region N
                                if (check.voiceIndex >= 0 && check.voiceName != "none")
                                {
                                    sb.AppendLine($"    Voice holding 7th in region N: {check.voiceName} (index {check.voiceIndex}) = MIDI {check.voiceMidi} ({check.voiceNoteName})");
                                }
                                else
                                {
                                    sb.AppendLine($"    Voice holding 7th in region N: {check.voiceName} ({check.voiceNoteName})");
                                }
                                
                                // Computed resolution PCs (-1/-2)
                                if (check.seventhPc >= 0)
                                {
                                    sb.AppendLine($"    Computed resolution PCs:");
                                    sb.AppendLine($"      -1 semitone: PC {check.destPcDown1} ({check.destPcDown1Name})");
                                    sb.AppendLine($"      -2 semitones: PC {check.destPcDown2} ({check.destPcDown2Name})");
                                }
                                
                                // Destination chord-tone PCs set and whether it contains either resolution pc
                                if (check.destChordTonePcs != null && check.destChordTonePcs.Count > 0)
                                {
                                    string destPcsStr = string.Join(", ", check.destChordTonePcs);
                                    sb.AppendLine($"    Destination chord-tone PCs: [{destPcsStr}]");
                                    sb.AppendLine($"      Contains PC {check.destPcDown1} (-1 semitone): {check.hasPcDown1InDest}");
                                    sb.AppendLine($"      Contains PC {check.destPcDown2} (-2 semitones): {check.hasPcDown2InDest}");
                                }
                                
                                // That voice's destination pc
                                if (check.resolvedToMidi >= 0)
                                {
                                    sb.AppendLine($"    Voice's destination: MIDI {check.resolvedToMidi} ({check.resolvedToName}) PC={check.resolvedToPc}");
                                }
                                else if (check.voiceIndex >= 0)
                                {
                                    sb.AppendLine($"    Voice's destination: N/A (check skipped)");
                                }
                                
                                // Result: PASS/FAIL/SKIP + reason
                                sb.AppendLine($"    Result: {check.result}");
                                if (!string.IsNullOrEmpty(check.reason))
                                {
                                    sb.AppendLine($"    Reason: {check.reason}");
                                }
                            }
                        }
                        
                        // Print required chord tones check details (checked against final post-continuity voicing)
                        if (diags.requiredChordTonesChecks != null && diags.requiredChordTonesChecks.Count > 0)
                        {
                            sb.AppendLine("\n=== Required Chord Tones Check Details (per region) ===");
                            foreach (var check in diags.requiredChordTonesChecks)
                            {
                                if (check.result == "FAIL")
                                {
                                    sb.AppendLine($"\n  Region {check.regionIndex} ({check.chordLabel}):");
                                    sb.AppendLine($"    Required chord tone PCs: [{string.Join(", ", check.requiredPcs.Select((pc, idx) => $"{pc} ({check.requiredPcNames[idx]})"))}]");
                                    sb.AppendLine($"    Realized BTAS PCs: [{string.Join(", ", check.realizedPcs.OrderBy(pc => pc))}]");
                                    sb.AppendLine($"    Realized BTAS: B={check.realizedNames[0]}({check.realizedMidi[0]}), T={check.realizedNames[1]}({check.realizedMidi[1]}), A={check.realizedNames[2]}({check.realizedMidi[2]}), S={check.realizedNames[3]}({check.realizedMidi[3]})");
                                    sb.AppendLine($"    Missing required tones: {string.Join(", ", check.missingPcNames)}");
                                    sb.AppendLine($"    Result: {check.result}");
                                    if (!string.IsNullOrEmpty(check.reason))
                                    {
                                        sb.AppendLine($"    Reason: {check.reason}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (report.failCount > 0)
            {
                sb.AppendLine("\n=== FAILURES ===");
                foreach (var failure in report.failures)
                {
                    sb.AppendLine($"  [{failure.caseName}] Region {failure.regionIndex} → {failure.regionIndex + 1}, Voice: {failure.voiceName}");
                    sb.AppendLine($"    From: MIDI {failure.fromMidi}, To: MIDI {failure.toMidi}");
                    sb.AppendLine($"    Message: {failure.message}");
                    if (!string.IsNullOrEmpty(failure.stage))
                    {
                        sb.AppendLine($"    Stage: {failure.stage}");
                    }
                }
            }
            else
            {
                sb.AppendLine("\n=== All tests passed! ===");
            }

            UnityEngine.Debug.Log(sb.ToString());
        }
#endif
    }
}

