using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sonoria.MusicTheory;
using Sonoria.MusicTheory.Timeline;

namespace EarFPS
{
    /// <summary>
    /// Lightweight UI helper component for displaying voiced chords (bass-tenor-alto-soprano)
    /// with step information for debug and educational purposes.
    /// </summary>
    public class VoicingViewer : MonoBehaviour
    {
        [Header("Voicing Display")]
        [Tooltip("Header text showing step information (e.g., 'Current Voicing — Step 3 of 7').")]
        [SerializeField] private TextMeshProUGUI headerText;

        [Tooltip("Text label for the bass voice (lowest note).")]
        [SerializeField] private TextMeshProUGUI bassText;

        [Tooltip("Text label for the tenor voice (second lowest note).")]
        [SerializeField] private TextMeshProUGUI tenorText;

        [Tooltip("Text label for the alto voice (second highest note).")]
        [SerializeField] private TextMeshProUGUI altoText;

        [Tooltip("Text label for the soprano voice (highest note).")]
        [SerializeField] private TextMeshProUGUI sopranoText;
        
        [Tooltip("Text label for the melody timeline (optional, used in timeline view mode).")]
        [SerializeField] private TextMeshProUGUI melodyText;
        
        [Tooltip("Text label for absolute chord symbols row (e.g., C, C/E, C7(b9)).")]
        [SerializeField] private TextMeshProUGUI chordSymbolRow;
        
        [Tooltip("Text label for Roman numerals row (e.g., I, I/3rd, V7, bII).")]
        [SerializeField] private TextMeshProUGUI romanNumeralRow;

        [Header("Voice-Leading Diagnostics")]
        [Tooltip("Semitone distance at or above which a voice movement is highlighted as a large leap.")]
        [SerializeField] private int largeLeapSemitoneThreshold = 5;
        
        [Header("Chord Header Styling")]
        [Tooltip("Color for non-diatonic chord labels in the chord symbol and Roman numeral rows. Uses HTML color format (e.g., #A070C0 for purple).")]
        [SerializeField] private Color nonDiatonicChordColor = new Color(0.627f, 0.439f, 0.753f, 1f); // #A070C0

        [Tooltip("ScrollRect for the voicing viewer.")]
        public ScrollRect scrollRect;
        
        [Tooltip("Content RectTransform inside the ScrollRect viewport. Used for dynamic width sizing.")]
        [SerializeField] private RectTransform scrollContent;
        
        [Header("Autoscroll")]
        [Tooltip("If true, the ScrollRect follows the highlighted step during playback.")]
        [SerializeField] private bool autoScrollEnabled = true;
        
        [Tooltip("Smoothing factor for autoscroll (higher = snappier).")]
        [Range(0f, 1f)]
        [SerializeField] private float autoScrollLerpStrength = 0.25f;
        
        /// <summary>
        /// Sets the highlight colors for playback highlighting. Called by ChordLabController.
        /// </summary>
        /// <param name="normal">Color for non-highlighted steps</param>
        /// <param name="highlight">Color for highlighted (currently playing) step</param>
        public void SetHighlightColors(Color normal, Color highlight)
        {
            normalColor = normal;
            highlightColor = highlight;
        }

        /// <summary>
        /// Fixed width for each timeline cell (note, dash, or blank) to ensure column alignment.
        /// </summary>
        private const int TimelineCellWidth = 4;

        // Accumulator strings for building up the sequence of voicings
        private string bassLine = string.Empty;
        private string tenorLine = string.Empty;
        private string altoLine = string.Empty;
        private string sopranoLine = string.Empty;
        private string melodyLine = string.Empty;
        private string chordSymbolLine = string.Empty;
        private string romanNumeralLine = string.Empty;
        
        // Per-step cell storage for timeline mode (enables highlighting)
        private List<string> melodyCells = new List<string>();
        private List<string> sopranoCells = new List<string>();
        private List<string> altoCells = new List<string>();
        private List<string> tenorCells = new List<string>();
        private List<string> bassCells = new List<string>();
        private List<string> chordSymbolCells = new List<string>();
        private List<string> romanCells = new List<string>();
        private List<bool> chordSymbolIsNonDiatonic = new List<bool>(); // Track non-diatonic status per cell
        private List<bool> romanIsNonDiatonic = new List<bool>(); // Track non-diatonic status per cell
        private int totalSteps = 0;
        private int currentStepIndex = -1; // Last highlighted step index, or -1 if none
        
        // Autoscroll target tracking
        private float targetScrollNormalizedPosition = 0f;
        private bool hasScrollTarget = false;
        
        // Highlight colors (set from ChordLabController)
        private Color normalColor = Color.white;
        private Color highlightColor = new Color(1f, 1f, 0.7f, 1f); // Light yellow default

        /// <summary>
        /// Last chord's sorted MIDI notes (bass to soprano) used for leap detection.
        /// </summary>
        private List<int> previousChordSortedMidi = null;
        
        /// <summary>
        /// Last MIDI note per voice for sustain dash detection (null means no previous note).
        /// </summary>
        private int? lastBassMidi = null;
        private int? lastTenorMidi = null;
        private int? lastAltoMidi = null;
        private int? lastSopranoMidi = null;
        private int? lastMelodyMidi = null;

        /// <summary>
        /// Ensures word wrapping is disabled on all text fields for proper alignment.
        /// </summary>
        void Awake()
        {
            // Disable word wrapping on all text fields to ensure proper monospaced alignment
            if (headerText != null) headerText.enableWordWrapping = false;
            if (bassText != null) bassText.enableWordWrapping = false;
            if (tenorText != null) tenorText.enableWordWrapping = false;
            if (altoText != null) altoText.enableWordWrapping = false;
            if (sopranoText != null) sopranoText.enableWordWrapping = false;
            if (melodyText != null) melodyText.enableWordWrapping = false;
            if (chordSymbolRow != null) chordSymbolRow.enableWordWrapping = false;
            if (romanNumeralRow != null) romanNumeralRow.enableWordWrapping = false;
        }

        /// <summary>
        /// Updates the voicing viewer by appending a voiced chord to the accumulating sequence.
        /// On the first step (stepIndex == 1), resets all accumulators and starts a new sequence.
        /// Each subsequent call appends the current chord's notes to the corresponding voice lines.
        /// </summary>
        /// <param name="key">TheoryKey used for enharmonic spelling.</param>
        /// <param name="stepIndex">1-based index of the current chord in the sequence.</param>
        /// <param name="totalSteps">Total number of chords in the sequence.</param>
        /// <param name="midiNotes">
        /// Collection of MIDI notes representing the voiced chord.
        /// CRITICAL: Expected order from TheoryVoicing is [Bass, Tenor, Alto, Soprano] (index 0-3).
        /// DO NOT sort these notes - use them in the order provided to preserve SATB voice assignment.
        /// </param>
        /// <param name="chordEvent">Optional chord event providing recipe context for canonical spelling. If null, falls back to key-based spelling.</param>
        /// <param name="trailingSpaces">Number of trailing spaces to add after this chord token (for duration-based spacing). Defaults to 0.</param>
        /// <summary>
        /// [LEGACY] Shows a single voicing step using the old per-chord model.
        /// Prefer ShowTimelineSatbOnly or ShowTimelineWithMelody for timeline-based views with proper alignment.
        /// This method is kept for backwards compatibility when useVoicingTimelineView is disabled.
        /// </summary>
        public void ShowVoicing(TheoryKey key, int stepIndex, int totalSteps, IReadOnlyList<int> midiNotes, ChordEvent? chordEvent = null, int trailingSpaces = 0)
        {
            // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
            // We use this exact order - NO sorting by pitch!

            // Debug logging: log what VoicingViewer receives
            if (TheoryVoicing.GetTendencyDebug())
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[Viewer Debug] Step {stepIndex}: Received MIDI notes: ");
                if (midiNotes != null)
                {
                    for (int v = 0; v < midiNotes.Count; v++)
                    {
                        int midi = midiNotes[v];
                        string name = TheoryPitch.GetPitchNameFromMidi(midi, key);
                        string voiceLabel = (v == 0) ? "Bass" : (v == 1) ? "Tenor" : (v == 2) ? "Alto" : "Soprano";
                        sb.Append($"{voiceLabel}={name}({midi}) ");
                    }
                }
                else
                {
                    sb.Append("null");
                }
                UnityEngine.Debug.Log(sb.ToString());
            }

            // Reset accumulators on first step
            if (stepIndex == 1)
            {
                Clear();
                if (headerText != null)
                {
                    headerText.text = $"Current Voicing — {totalSteps} steps";
                }
            }

            // Handle null/empty midiNotes by appending "(none)" tokens
            string bass, tenor, alto, soprano;
            
            if (midiNotes == null || midiNotes.Count == 0)
            {
                // Append "(none)" for each voice when no notes available
                bass = "(none)";
                tenor = "(none)";
                alto = "(none)";
                soprano = "(none)";
            }
            else
            {
                // CRITICAL: Use notes in the order provided by TheoryVoicing [Bass, Tenor, Alto, Soprano]
                // DO NOT sort - sorting breaks the SATB voice assignment!
                // TheoryVoicing already provides voices in the correct order.

                // Determine which voices have large leaps compared to previous chord.
                // Use original order (not sorted) for leap detection.
                bool[] largeLeapFlags = new bool[4]; // Bass, Tenor, Alto, Soprano

                if (stepIndex > 1 && previousChordSortedMidi != null)
                {
                    int voiceCount = Mathf.Min(midiNotes.Count, previousChordSortedMidi.Count, largeLeapFlags.Length);
                    for (int v = 0; v < voiceCount; v++)
                    {
                        int fromMidi = previousChordSortedMidi[v];
                        int toMidi = midiNotes[v]; // Use original order, not sorted
                        int diff = toMidi - fromMidi;
                        int semitones = Mathf.Abs(diff);

                        if (semitones >= largeLeapSemitoneThreshold)
                        {
                            largeLeapFlags[v] = true;
                        }
                    }
                }

                // Convert to note-name strings with octave numbers using canonical chord spelling when available,
                // falling back to key-aware spelling otherwise.
                // Use original order: index 0 = Bass, 1 = Tenor, 2 = Alto, 3 = Soprano
                // Check for sustain: if MIDI matches last, render dash instead of note name
                int? currentBassMidi = midiNotes.Count > 0 ? (int?)midiNotes[0] : null;
                int? currentTenorMidi = midiNotes.Count > 1 ? (int?)midiNotes[1] : null;
                int? currentAltoMidi = midiNotes.Count > 2 ? (int?)midiNotes[2] : null;
                int? currentSopranoMidi = midiNotes.Count > 3 ? (int?)midiNotes[3] : null;
                
                // Render note name or sustain dash based on whether MIDI matches last
                bass = currentBassMidi.HasValue
                    ? (lastBassMidi.HasValue && lastBassMidi.Value == currentBassMidi.Value
                        ? "-- "  // Sustain dash (4 chars to match fixed width)
                        : NoteNameWithOctave(currentBassMidi.Value, key, chordEvent))
                    : "(none)";
                    
                tenor = currentTenorMidi.HasValue
                    ? (lastTenorMidi.HasValue && lastTenorMidi.Value == currentTenorMidi.Value
                        ? "-- "
                        : NoteNameWithOctave(currentTenorMidi.Value, key, chordEvent))
                    : "(none)";
                    
                alto = currentAltoMidi.HasValue
                    ? (lastAltoMidi.HasValue && lastAltoMidi.Value == currentAltoMidi.Value
                        ? "-- "
                        : NoteNameWithOctave(currentAltoMidi.Value, key, chordEvent))
                    : "(none)";
                    
                soprano = currentSopranoMidi.HasValue
                    ? (lastSopranoMidi.HasValue && lastSopranoMidi.Value == currentSopranoMidi.Value
                        ? "-- "
                        : NoteNameWithOctave(currentSopranoMidi.Value, key, chordEvent))
                    : "(none)";

                // Wrap tokens in red color tags if they are marked as large leaps.
                // Note: Dashes are not wrapped in leap color (only actual note changes are highlighted)
                if (bass != "-- ") bass = ColorIfLeap(bass, largeLeapFlags[0]);
                if (tenor != "-- ") tenor = ColorIfLeap(tenor, largeLeapFlags[1]);
                if (alto != "-- ") alto = ColorIfLeap(alto, largeLeapFlags[2]);
                if (soprano != "-- ") soprano = ColorIfLeap(soprano, largeLeapFlags[3]);

                // Store current chord as previous for the next step (keep original order).
                previousChordSortedMidi = new List<int>(midiNotes);
                
                // Update last MIDI tracking for sustain dash detection
                lastBassMidi = currentBassMidi;
                lastTenorMidi = currentTenorMidi;
                lastAltoMidi = currentAltoMidi;
                lastSopranoMidi = currentSopranoMidi;
            }

            // Append current notes to accumulator strings with duration-based spacing
            bassLine = AppendTokenWithPadding(bassLine, bass, trailingSpaces);
            tenorLine = AppendTokenWithPadding(tenorLine, tenor, trailingSpaces);
            altoLine = AppendTokenWithPadding(altoLine, alto, trailingSpaces);
            sopranoLine = AppendTokenWithPadding(sopranoLine, soprano, trailingSpaces);

            // Update TMP fields with accumulated lines
            SetVoiceText(bassText, bassLine);
            SetVoiceText(tenorText, tenorLine);
            SetVoiceText(altoText, altoLine);
            SetVoiceText(sopranoText, sopranoLine);
        }

        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB only).
        /// All lists must have the same length (number of time steps/quarters).
        /// Each list contains nullable MIDI values: null means no note (blank), same MIDI as previous step means sustain (dash).
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (must match bass length)</param>
        public void ShowTimeline(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano)
        {
            ShowTimeline(bass, tenor, alto, soprano, null);
        }
        
        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB + Melody).
        /// All lists must have the same length (number of time steps/quarters).
        /// Each list contains nullable MIDI values: null means no note (blank), same MIDI as previous step means sustain (dash).
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (must match bass length)</param>
        /// <param name="melody">List of nullable MIDI notes for melody timeline (optional, must match bass length if provided)</param>
        public void ShowTimeline(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano,
            IReadOnlyList<int?> melody)
        {
            // Call the overload with null attack flags (legacy behavior: use MIDI comparison for sustains)
            ShowTimeline(bass, tenor, alto, soprano, melody, null);
        }
        
        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB + Melody).
        /// All lists must have the same length (number of time steps/quarters).
        /// For melody: uses attack flags to distinguish repeated attacks from sustains.
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (must match bass length)</param>
        /// <param name="melody">List of nullable MIDI notes for melody timeline (optional, must match bass length if provided)</param>
        /// <param name="melodyIsAttack">List of attack flags for melody (must match bass length if provided; true = note attack, false = sustain)</param>
        public void ShowTimeline(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano,
            IReadOnlyList<int?> melody,
            IReadOnlyList<bool> melodyIsAttack)
        {
            // Call the overload with null chordIsAttack (legacy behavior: use MIDI comparison for sustains)
            ShowTimeline(bass, tenor, alto, soprano, melody, melodyIsAttack, null);
        }
        
        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB + Melody).
        /// All lists must have the same length (number of time steps/quarters).
        /// For B/T/A: uses chordIsAttack to distinguish chord attacks from sustains.
        /// For melody: uses melodyIsAttack to distinguish repeated attacks from sustains.
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (must match bass length)</param>
        /// <param name="melody">List of nullable MIDI notes for melody timeline (optional, must match bass length if provided)</param>
        /// <param name="melodyIsAttack">List of attack flags for melody (must match bass length if provided; true = note attack, false = sustain)</param>
        /// <param name="chordIsAttack">List of attack flags for chord voices B/T/A (must match bass length if provided; true = chord attack, false = sustain)</param>
        public void ShowTimeline(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano,
            IReadOnlyList<int?> melody,
            IReadOnlyList<bool> melodyIsAttack,
            IReadOnlyList<bool> chordIsAttack)
        {
            // Validate that all lists have the same length
            int stepCount = bass != null ? bass.Count : 0;
            if (tenor == null || tenor.Count != stepCount ||
                alto == null || alto.Count != stepCount ||
                soprano == null || soprano.Count != stepCount ||
                (melody != null && melody.Count != stepCount) ||
                (melodyIsAttack != null && melodyIsAttack.Count != stepCount) ||
                (chordIsAttack != null && chordIsAttack.Count != stepCount))
            {
                UnityEngine.Debug.LogError($"[VoicingViewer] ShowTimeline: All voice lists must have the same length. Bass={stepCount}, Tenor={tenor?.Count ?? 0}, Alto={alto?.Count ?? 0}, Soprano={soprano?.Count ?? 0}, Melody={melody?.Count ?? 0}, MelodyIsAttack={melodyIsAttack?.Count ?? 0}, ChordIsAttack={chordIsAttack?.Count ?? 0}");
                return;
            }
            
            if (stepCount == 0)
            {
                Clear();
                return;
            }
            
            // Clear existing accumulators and reset last MIDI tracking
            Clear();
            
            // Get TheoryKey for note name conversion (fallback to C Major if not available)
            // Note: In timeline mode, we don't have chord context, so we'll use a default key
            TheoryKey defaultKey = new TheoryKey(Sonoria.MusicTheory.ScaleMode.Ionian);
            
            // Build timeline strings for each voice
            // Use chordIsAttack for B/T/A if available, otherwise fall back to MIDI comparison
            if (chordIsAttack != null)
            {
                bassLine = BuildTimelineChordVoiceString(bass, chordIsAttack, defaultKey, ref lastBassMidi);
                tenorLine = BuildTimelineChordVoiceString(tenor, chordIsAttack, defaultKey, ref lastTenorMidi);
                altoLine = BuildTimelineChordVoiceString(alto, chordIsAttack, defaultKey, ref lastAltoMidi);
                sopranoLine = BuildTimelineChordVoiceString(soprano, chordIsAttack, defaultKey, ref lastSopranoMidi);
            }
            else
            {
                // Legacy behavior: use MIDI comparison for sustains
                bassLine = BuildTimelineVoiceString(bass, defaultKey, ref lastBassMidi);
                tenorLine = BuildTimelineVoiceString(tenor, defaultKey, ref lastTenorMidi);
                altoLine = BuildTimelineVoiceString(alto, defaultKey, ref lastAltoMidi);
                sopranoLine = BuildTimelineVoiceString(soprano, defaultKey, ref lastSopranoMidi);
            }
            
            // Build melody timeline if provided (use attack flags if available, otherwise fall back to MIDI comparison)
            if (melody != null)
            {
                if (melodyIsAttack != null)
                {
                    melodyLine = BuildTimelineMelodyString(melody, melodyIsAttack, defaultKey, ref lastMelodyMidi);
                }
                else
                {
                    // Legacy behavior: use MIDI comparison for sustains
                    melodyLine = BuildTimelineVoiceString(melody, defaultKey, ref lastMelodyMidi);
                }
            }
            else
            {
                melodyLine = string.Empty;
            }
            
            // Update header
            if (headerText != null)
            {
                headerText.text = $"Current Voicing — {stepCount} quarters";
            }
            
            // Update TMP fields
            SetVoiceText(bassText, bassLine);
            SetVoiceText(tenorText, tenorLine);
            SetVoiceText(altoText, altoLine);
            // textSoprano is no longer written to (retired - use textMelody as top line via ShowTimelineTopAndSatb)
            SetVoiceText(melodyText, melodyLine);
        }
        
        /// <summary>
        /// Unified timeline API that uses Text_Melody as the top line for all modes.
        /// When topIsMelody is true, topLine represents melody (uses melodyIsAttack for attack/sustain).
        /// When topIsMelody is false, topLine represents soprano (uses chordIsAttack for attack/sustain).
        /// </summary>
        /// <param name="topLine">Top line data (melody or soprano depending on topIsMelody)</param>
        /// <param name="topIsMelody">True if topLine is melody, false if it's soprano</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice</param>
        /// <param name="bass">List of nullable MIDI notes for bass voice</param>
        /// <param name="melodyIsAttack">Attack flags for melody (used only when topIsMelody is true)</param>
        /// <param name="chordIsAttack">Attack flags for chord voices (used for A/T/B and top when topIsMelody is false)</param>
        /// <param name="absoluteChordSymbolsPerRegion">Optional: Region-aligned list of absolute chord symbols (e.g., "C", "C/E", "C7(b9)")</param>
        /// <param name="romanNumeralsPerRegion">Optional: Region-aligned list of Roman numerals (e.g., "I", "I/3rd", "V7")</param>
        /// <param name="isDiatonicPerRegion">Optional: Region-aligned list of diatonic status flags</param>
        /// <param name="regionDurationTicks">Optional: Region-aligned list of duration ticks (must match regions used to build timeline)</param>
        /// <param name="timelineSpec">Optional: Timeline specification for converting ticks to quarters</param>
        public void ShowTimelineTopAndSatb(
            IReadOnlyList<int?> topLine,
            bool topIsMelody,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> bass,
            IReadOnlyList<bool> melodyIsAttack,
            IReadOnlyList<bool> chordIsAttack,
            IReadOnlyList<string> absoluteChordSymbolsPerRegion = null,
            IReadOnlyList<string> romanNumeralsPerRegion = null,
            IReadOnlyList<bool> isDiatonicPerRegion = null,
            IReadOnlyList<int> regionDurationTicks = null,
            TimelineSpec timelineSpec = null)
        {
            // Validate that all lists have the same length
            int stepCount = bass != null ? bass.Count : 0;
            if (topLine == null || topLine.Count != stepCount ||
                alto == null || alto.Count != stepCount ||
                tenor == null || tenor.Count != stepCount ||
                chordIsAttack == null || chordIsAttack.Count != stepCount ||
                (topIsMelody && (melodyIsAttack == null || melodyIsAttack.Count != stepCount)))
            {
                UnityEngine.Debug.LogError($"[VoicingViewer] ShowTimelineTopAndSatb: All voice lists must have the same length. Bass={stepCount}, Top={topLine?.Count ?? 0}, Tenor={tenor?.Count ?? 0}, Alto={alto?.Count ?? 0}, ChordIsAttack={chordIsAttack?.Count ?? 0}, MelodyIsAttack={melodyIsAttack?.Count ?? 0}");
                return;
            }
            
            if (stepCount == 0)
            {
                Clear();
                return;
            }
            
            // Clear existing accumulators and reset last MIDI tracking
            Clear();
            
            // Store total steps for highlighting
            totalSteps = stepCount;
            currentStepIndex = -1; // Reset highlighted step when timeline is rebuilt
            
            // Clear per-step cell storage
            melodyCells.Clear();
            sopranoCells.Clear();
            altoCells.Clear();
            tenorCells.Clear();
            bassCells.Clear();
            chordSymbolCells.Clear();
            romanCells.Clear();
            chordSymbolIsNonDiatonic.Clear();
            romanIsNonDiatonic.Clear();
            
            // Get TheoryKey for note name conversion (fallback to C Major if not available)
            TheoryKey defaultKey = new TheoryKey(Sonoria.MusicTheory.ScaleMode.Ionian);
            
            // Build top line (melody or soprano) - always goes to textMelody
            // Also store per-step cells for highlighting
            System.Text.StringBuilder topBuilder = new System.Text.StringBuilder();
            for (int step = 0; step < stepCount; step++)
            {
                int? midiTop = topLine[step];
                string coreCell;
                
                if (midiTop.HasValue)
                {
                    if (topIsMelody)
                    {
                        // Melody semantics: use melodyIsAttack
                        bool isAttack = melodyIsAttack[step];
                        if (isAttack)
                        {
                            string noteName = NoteNameWithOctave(midiTop.Value, defaultKey, null);
                            coreCell = noteName.TrimEnd();
                        }
                        else
                        {
                            coreCell = "--";
                        }
                    }
                    else
                    {
                        // Harmony semantics: use chordIsAttack (soprano as harmonic voice)
                        bool isAttack = chordIsAttack[step];
                        if (isAttack)
                        {
                            string noteName = NoteNameWithOctave(midiTop.Value, defaultKey, null);
                            coreCell = noteName.TrimEnd();
                        }
                        else
                        {
                            coreCell = "--";
                        }
                    }
                }
                else
                {
                    coreCell = string.Empty;
                }
                
                // Normalize to fixed width
                string cell = coreCell.PadRight(TimelineCellWidth);
                topBuilder.Append(cell);
                
                // Store cell for highlighting (top line goes to melodyCells)
                melodyCells.Add(cell);
            }
            
            // Build SATB voices using chordIsAttack and store per-step cells
            bassLine = BuildTimelineChordVoiceStringWithCells(bass, chordIsAttack, defaultKey, ref lastBassMidi, bassCells);
            tenorLine = BuildTimelineChordVoiceStringWithCells(tenor, chordIsAttack, defaultKey, ref lastTenorMidi, tenorCells);
            altoLine = BuildTimelineChordVoiceStringWithCells(alto, chordIsAttack, defaultKey, ref lastAltoMidi, altoCells);
            
            // For soprano: if topIsMelody is false, topLine is soprano, so we need to extract soprano cells
            // Otherwise, soprano is in the SATB voices (but we're using topLine for melody)
            if (!topIsMelody)
            {
                // Top line is soprano, so use melodyCells for soprano
                sopranoCells = new List<string>(melodyCells);
            }
            else
            {
                // Soprano is in the SATB voices - we'd need to extract it, but for now use empty
                // This is a limitation: we don't have soprano separately in this path
                // For now, leave sopranoCells empty (it's not used in timeline mode anyway)
                sopranoCells.Clear();
                for (int i = 0; i < stepCount; i++)
                {
                    sopranoCells.Add(new string(' ', TimelineCellWidth));
                }
            }
            
            // Top line always goes to melodyText (regardless of whether it's melody or soprano)
            melodyLine = topBuilder.ToString();
            
            // Build chord symbol and Roman numeral rows if region data is provided
            if (absoluteChordSymbolsPerRegion != null && romanNumeralsPerRegion != null && 
                regionDurationTicks != null && timelineSpec != null)
            {
                BuildChordHeaderRows(
                    stepCount,
                    absoluteChordSymbolsPerRegion,
                    romanNumeralsPerRegion,
                    isDiatonicPerRegion,
                    regionDurationTicks,
                    timelineSpec);
            }
            else
            {
                // Clear chord header rows if no data provided
                chordSymbolLine = string.Empty;
                romanNumeralLine = string.Empty;
                // Fill with blank cells
                for (int i = 0; i < stepCount; i++)
                {
                    chordSymbolCells.Add(new string(' ', TimelineCellWidth));
                    romanCells.Add(new string(' ', TimelineCellWidth));
                    chordSymbolIsNonDiatonic.Add(false);
                    romanIsNonDiatonic.Add(false);
                }
            }
            
            // Update header
            if (headerText != null)
            {
                headerText.text = $"Current Voicing — {stepCount} quarters";
            }

            // Initial render (no highlight) - this sets the text fields
            RefreshDisplay(-1);
            
            // Calculate and set content width based on actual text width
            UpdateContentWidth();
            
            if (scrollRect != null)
            {
                // Make sure layout is up to date after width change
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    scrollRect.content as RectTransform
                );

                scrollRect.horizontalNormalizedPosition = 0f; // 0 = left, 1 = right
                targetScrollNormalizedPosition = 0f;
                hasScrollTarget = true;
            }
        }
        
        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB + Melody).
        /// Uses Melody row for melody, clears Soprano row.
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (not displayed, but must match bass length)</param>
        /// <param name="melody">List of nullable MIDI notes for melody timeline (must match bass length)</param>
        /// <param name="melodyIsAttack">List of attack flags for melody (must match bass length; true = note attack, false = sustain)</param>
        /// <param name="chordIsAttack">List of attack flags for chord voices B/T/A (must match bass length; true = chord attack, false = sustain)</param>
        public void ShowTimelineWithMelody(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano,
            IReadOnlyList<int?> melody,
            IReadOnlyList<bool> melodyIsAttack,
            IReadOnlyList<bool> chordIsAttack)
        {
            // Call unified API with melody as top line
            ShowTimelineTopAndSatb(melody, true, alto, tenor, bass, melodyIsAttack, chordIsAttack);
        }
        
        /// <summary>
        /// Shows a timeline-based voicing view with quarter-note grid resolution (SATB only, no melody).
        /// Uses Soprano row for soprano voice, clears Melody row.
        /// </summary>
        /// <param name="bass">List of nullable MIDI notes for bass voice (length defines total time steps)</param>
        /// <param name="tenor">List of nullable MIDI notes for tenor voice (must match bass length)</param>
        /// <param name="alto">List of nullable MIDI notes for alto voice (must match bass length)</param>
        /// <param name="soprano">List of nullable MIDI notes for soprano voice (must match bass length)</param>
        /// <param name="chordIsAttack">List of attack flags for chord voices B/T/A/S (must match bass length; true = chord attack, false = sustain)</param>
        public void ShowTimelineSatbOnly(
            IReadOnlyList<int?> bass,
            IReadOnlyList<int?> tenor,
            IReadOnlyList<int?> alto,
            IReadOnlyList<int?> soprano,
            IReadOnlyList<bool> chordIsAttack)
        {
            // Create dummy melodyIsAttack list (unused when topIsMelody is false)
            var dummyMelodyIsAttack = new List<bool>(bass.Count);
            for (int i = 0; i < bass.Count; i++)
            {
                dummyMelodyIsAttack.Add(false);
            }
            
            // Call unified API with soprano as top line
            ShowTimelineTopAndSatb(soprano, false, alto, tenor, bass, dummyMelodyIsAttack, chordIsAttack);
        }
        
        /// <summary>
        /// Helper method to build a timeline string for chord voices (B/T/A) using chord attack flags.
        /// Also stores per-step cells for highlighting.
        /// </summary>
        private string BuildTimelineChordVoiceStringWithCells(
            IReadOnlyList<int?> midiList,
            IReadOnlyList<bool> chordIsAttack,
            TheoryKey key,
            ref int? lastMidi,
            List<string> cellStorage)
        {
            lastMidi = null; // Reset tracking for this voice
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            cellStorage.Clear();
            
            for (int step = 0; step < midiList.Count; step++)
            {
                int? currentMidi = midiList[step];
                bool isChordAttack = chordIsAttack[step];
                string coreCell;
                
                if (currentMidi.HasValue)
                {
                    // Has a note at this step
                    if (isChordAttack)
                    {
                        // Always show note name at chord attack, even if same as last
                        string noteName = NoteNameWithOctave(currentMidi.Value, key, null);
                        coreCell = noteName.TrimEnd();
                        lastMidi = currentMidi;
                    }
                    else
                    {
                        // Non-attack quarter inside a region: sustain
                        coreCell = "--";
                        // lastMidi remains as-is (should already be set to currentMidi from previous attack)
                    }
                }
                else
                {
                    // No note at this step → blank (empty core)
                    // Reset lastMidi so next note is treated as new attack, not sustain
                    lastMidi = null;
                    coreCell = string.Empty;
                }
                
                // Normalize all cells to exactly TimelineCellWidth characters
                string cell = coreCell.PadRight(TimelineCellWidth);
                builder.Append(cell);
                cellStorage.Add(cell);
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Helper method to build a timeline string for a single voice from a list of nullable MIDI values.
        /// Each cell is exactly TimelineCellWidth characters wide (note, dash, or blank).
        /// </summary>
        private string BuildTimelineVoiceString(IReadOnlyList<int?> midiList, TheoryKey key, ref int? lastMidi)
        {
            lastMidi = null; // Reset tracking for this voice
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            
            for (int step = 0; step < midiList.Count; step++)
            {
                int? currentMidi = midiList[step];
                string coreCell;
                
                if (currentMidi.HasValue)
                {
                    // Has a note at this step
                    if (lastMidi.HasValue && lastMidi.Value == currentMidi.Value)
                    {
                        // Same as previous step → sustain dash (core without padding)
                        coreCell = "--";
                    }
                    else
                    {
                        // Different note or first note → render note name (already padded by NoteNameWithOctave, but we'll normalize)
                        string noteName = NoteNameWithOctave(currentMidi.Value, key, null);
                        // Remove any trailing spaces to get core, then we'll pad uniformly
                        coreCell = noteName.TrimEnd();
                    }
                    lastMidi = currentMidi;
                }
                else
                {
                    // No note at this step → blank (empty core)
                    // Reset lastMidi so next note is treated as new attack, not sustain
                    lastMidi = null;
                    coreCell = string.Empty;
                }
                
                // Normalize all cells to exactly TimelineCellWidth characters
                string cell = coreCell.PadRight(TimelineCellWidth);
                builder.Append(cell);
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Helper method to build a timeline string for melody voice using attack flags.
        /// Each cell is exactly TimelineCellWidth characters wide (note, dash, or blank).
        /// Uses attack flags to distinguish repeated attacks from sustains.
        /// </summary>
        private string BuildTimelineMelodyString(
            IReadOnlyList<int?> midiList, 
            IReadOnlyList<bool> isAttackList,
            TheoryKey key, 
            ref int? lastMidi)
        {
            lastMidi = null; // Reset tracking for this voice
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            
            for (int step = 0; step < midiList.Count; step++)
            {
                int? currentMidi = midiList[step];
                bool isAttack = isAttackList[step];
                string coreCell;
                
                if (currentMidi.HasValue)
                {
                    // Has a note at this step
                    if (isAttack)
                    {
                        // Always show note name on attack, even if same as last
                        string noteName = NoteNameWithOctave(currentMidi.Value, key, null);
                        coreCell = noteName.TrimEnd();
                        lastMidi = currentMidi;
                    }
                    else
                    {
                        // Non-attack with a MIDI: this is sustain
                        // (by construction, this should only happen when midi == lastMelodyMidi)
                        coreCell = "--";
                        // lastMidi stays as-is (should already be set to currentMidi from previous attack)
                    }
                }
                else
                {
                    // No note at this step → blank (empty core)
                    // Reset lastMidi so next note is treated as new attack, not sustain
                    lastMidi = null;
                    coreCell = string.Empty;
                }
                
                // Normalize all cells to exactly TimelineCellWidth characters
                string cell = coreCell.PadRight(TimelineCellWidth);
                builder.Append(cell);
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Helper method to build a timeline string for chord voices (B/T/A) using chord attack flags.
        /// Each cell is exactly TimelineCellWidth characters wide (note, dash, or blank).
        /// Uses chordIsAttack to distinguish chord attacks from sustains.
        /// </summary>
        private string BuildTimelineChordVoiceString(
            IReadOnlyList<int?> midiList,
            IReadOnlyList<bool> chordIsAttack,
            TheoryKey key,
            ref int? lastMidi)
        {
            lastMidi = null; // Reset tracking for this voice
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            
            for (int step = 0; step < midiList.Count; step++)
            {
                int? currentMidi = midiList[step];
                bool isChordAttack = chordIsAttack[step];
                string coreCell;
                
                if (currentMidi.HasValue)
                {
                    // Has a note at this step
                    if (isChordAttack)
                    {
                        // Always show note name at chord attack, even if same as last
                        string noteName = NoteNameWithOctave(currentMidi.Value, key, null);
                        coreCell = noteName.TrimEnd();
                        lastMidi = currentMidi;
                    }
                    else
                    {
                        // Non-attack quarter inside a region: sustain
                        coreCell = "--";
                        // lastMidi remains as-is (should already be set to currentMidi from previous attack)
                    }
                }
                else
                {
                    // No note at this step → blank (empty core)
                    // Reset lastMidi so next note is treated as new attack, not sustain
                    lastMidi = null;
                    coreCell = string.Empty;
                }
                
                // Normalize all cells to exactly TimelineCellWidth characters
                string cell = coreCell.PadRight(TimelineCellWidth);
                builder.Append(cell);
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Builds chord symbol and Roman numeral header rows by allowing labels to span the full width of each region.
        /// Each region's label can use up to (quartersInRegion * TimelineCellWidth) characters.
        /// </summary>
        /// <param name="stepCount">Total number of quarter-note steps in the timeline</param>
        /// <param name="absoluteChordSymbolsPerRegion">Region-aligned list of absolute chord symbols</param>
        /// <param name="romanNumeralsPerRegion">Region-aligned list of Roman numerals</param>
        /// <param name="isDiatonicPerRegion">Optional region-aligned list of diatonic status flags</param>
        /// <param name="regionDurationTicks">Region-aligned list of duration ticks</param>
        /// <param name="timelineSpec">Timeline specification for converting ticks to quarters</param>
        private void BuildChordHeaderRows(
            int stepCount,
            IReadOnlyList<string> absoluteChordSymbolsPerRegion,
            IReadOnlyList<string> romanNumeralsPerRegion,
            IReadOnlyList<bool> isDiatonicPerRegion,
            IReadOnlyList<int> regionDurationTicks,
            TimelineSpec timelineSpec)
        {
            if (absoluteChordSymbolsPerRegion == null || romanNumeralsPerRegion == null || 
                regionDurationTicks == null || timelineSpec == null)
            {
                chordSymbolLine = string.Empty;
                romanNumeralLine = string.Empty;
                return;
            }
            
            int ticksPerQuarter = timelineSpec.ticksPerQuarter > 0 ? timelineSpec.ticksPerQuarter : 4;
            
            // Clear cell storage
            chordSymbolCells.Clear();
            romanCells.Clear();
            chordSymbolIsNonDiatonic.Clear();
            romanIsNonDiatonic.Clear();
            
            // Build row strings per region (not per quarter)
            System.Text.StringBuilder absBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder romanBuilder = new System.Text.StringBuilder();
            
            // Track cumulative tick position for regions
            int cumulativeTick = 0;
            
            // Process each region
            for (int regionIdx = 0; regionIdx < absoluteChordSymbolsPerRegion.Count && regionIdx < romanNumeralsPerRegion.Count; regionIdx++)
            {
                int durationTicks = regionIdx < regionDurationTicks.Count ? regionDurationTicks[regionIdx] : ticksPerQuarter;
                if (durationTicks <= 0) durationTicks = ticksPerQuarter; // Fallback to 1 quarter
                
                // Compute quarters in this region
                int quartersInRegion = (durationTicks + ticksPerQuarter - 1) / ticksPerQuarter; // ceil
                if (quartersInRegion <= 0) quartersInRegion = 1;
                
                // Calculate character budget for this region (all quarters combined)
                int charBudget = quartersInRegion * TimelineCellWidth;
                
                // Get chord labels for this region (full labels, not truncated yet)
                string absLabel = regionIdx < absoluteChordSymbolsPerRegion.Count ? absoluteChordSymbolsPerRegion[regionIdx] : "";
                string romanLabel = regionIdx < romanNumeralsPerRegion.Count ? romanNumeralsPerRegion[regionIdx] : "";
                bool isDiatonic = (isDiatonicPerRegion != null && regionIdx < isDiatonicPerRegion.Count) 
                    ? isDiatonicPerRegion[regionIdx] 
                    : true; // Default to diatonic if not provided
                
                // Truncate labels to fit within character budget
                if (absLabel.Length > charBudget)
                    absLabel = absLabel.Substring(0, charBudget);
                if (romanLabel.Length > charBudget)
                    romanLabel = romanLabel.Substring(0, charBudget);
                
                // Pad labels to full region width
                string absRegionString = absLabel.PadRight(charBudget);
                string romanRegionString = romanLabel.PadRight(charBudget);
                
                // Apply color tag for non-diatonic chords (around entire region string)
                if (!isDiatonic)
                {
                    // Convert Color to hex string for TMP rich text
                    string colorHex = ColorUtility.ToHtmlStringRGBA(nonDiatonicChordColor);
                    absRegionString = $"<color=#{colorHex}>{absRegionString}</color>";
                    romanRegionString = $"<color=#{colorHex}>{romanRegionString}</color>";
                }
                
                // Append the entire region string at once
                absBuilder.Append(absRegionString);
                romanBuilder.Append(romanRegionString);
                
                // Store per-step cells for this region (split region string into individual cells)
                // Extract text without color tags for storage (color will be applied during highlighting)
                string absTextOnly = absRegionString;
                string romanTextOnly = romanRegionString;
                
                // Remove color tags to get raw text
                if (absTextOnly.Contains("<color="))
                {
                    int colorStart = absTextOnly.IndexOf("<color=");
                    int colorEnd = absTextOnly.IndexOf(">", colorStart);
                    int endTagStart = absTextOnly.IndexOf("</color>");
                    if (colorStart >= 0 && colorEnd >= 0 && endTagStart >= 0)
                    {
                        absTextOnly = absTextOnly.Substring(colorEnd + 1, endTagStart - colorEnd - 1);
                    }
                }
                if (romanTextOnly.Contains("<color="))
                {
                    int colorStart = romanTextOnly.IndexOf("<color=");
                    int colorEnd = romanTextOnly.IndexOf(">", colorStart);
                    int endTagStart = romanTextOnly.IndexOf("</color>");
                    if (colorStart >= 0 && colorEnd >= 0 && endTagStart >= 0)
                    {
                        romanTextOnly = romanTextOnly.Substring(colorEnd + 1, endTagStart - colorEnd - 1);
                    }
                }
                
                // Store whether this region is non-diatonic (for later color application)
                bool regionIsNonDiatonic = !isDiatonic;
                
                // Split into cells and store (with non-diatonic flag stored separately)
                for (int q = 0; q < quartersInRegion; q++)
                {
                    int startIdx = q * TimelineCellWidth;
                    string absCell;
                    string romanCell;
                    
                    if (startIdx < absTextOnly.Length)
                    {
                        int endIdx = Mathf.Min(startIdx + TimelineCellWidth, absTextOnly.Length);
                        absCell = absTextOnly.Substring(startIdx, endIdx - startIdx).PadRight(TimelineCellWidth);
                    }
                    else
                    {
                        absCell = new string(' ', TimelineCellWidth);
                    }
                    
                    if (startIdx < romanTextOnly.Length)
                    {
                        int endIdx = Mathf.Min(startIdx + TimelineCellWidth, romanTextOnly.Length);
                        romanCell = romanTextOnly.Substring(startIdx, endIdx - startIdx).PadRight(TimelineCellWidth);
                    }
                    else
                    {
                        romanCell = new string(' ', TimelineCellWidth);
                    }
                    
                    // Store cells and track non-diatonic status
                    chordSymbolCells.Add(absCell);
                    romanCells.Add(romanCell);
                    chordSymbolIsNonDiatonic.Add(regionIsNonDiatonic);
                    romanIsNonDiatonic.Add(regionIsNonDiatonic);
                }
                
                // Update cumulative tick for next region
                cumulativeTick += durationTicks;
            }
            
            // Ensure total length matches stepCount * TimelineCellWidth (for alignment with SATB)
            int targetLength = stepCount * TimelineCellWidth;
            
            // Pad if needed (shouldn't happen if regions sum correctly, but safety check)
            while (absBuilder.Length < targetLength)
            {
                absBuilder.Append(' ');
            }
            while (romanBuilder.Length < targetLength)
            {
                romanBuilder.Append(' ');
            }
            
            // Ensure cell storage matches stepCount
            while (chordSymbolCells.Count < stepCount)
            {
                chordSymbolCells.Add(new string(' ', TimelineCellWidth));
                chordSymbolIsNonDiatonic.Add(false);
            }
            while (romanCells.Count < stepCount)
            {
                romanCells.Add(new string(' ', TimelineCellWidth));
                romanIsNonDiatonic.Add(false);
            }
            
            // Truncate if somehow longer (shouldn't happen, but safety check)
            if (absBuilder.Length > targetLength)
            {
                absBuilder.Length = targetLength;
            }
            if (romanBuilder.Length > targetLength)
            {
                romanBuilder.Length = targetLength;
            }
            if (chordSymbolCells.Count > stepCount)
            {
                chordSymbolCells.RemoveRange(stepCount, chordSymbolCells.Count - stepCount);
            }
            if (romanCells.Count > stepCount)
            {
                romanCells.RemoveRange(stepCount, romanCells.Count - stepCount);
            }
            
            chordSymbolLine = absBuilder.ToString();
            romanNumeralLine = romanBuilder.ToString();
        }
        
        /// <summary>
        /// Sets the highlighted step index for playback highlighting.
        /// stepIndex is 0-based quarter-step index. Use -1 to clear highlight.
        /// </summary>
        public void SetHighlightedStep(int stepIndex)
        {
            currentStepIndex = stepIndex;
            RefreshDisplay(stepIndex);
            UpdateScrollPosition(stepIndex);
        }
        
        /// <summary>
        /// Updates the Content RectTransform width to match the actual timeline text width.
        /// Ensures short progressions don't have huge blank space, and long progressions get enough width.
        /// Minimum width is set to viewport width to prevent scrollable areas smaller than the viewport.
        /// </summary>
        private void UpdateContentWidth()
        {
            if (scrollContent == null) return;
            if (scrollRect == null) return;
            
            // Use one of the SATB text fields to determine required width
            // All SATB rows use the same monospaced font and contain the same number of cells
            TextMeshProUGUI referenceText = bassText;
            if (referenceText == null)
            {
                // Fallback to other SATB fields if bass is not available
                referenceText = tenorText ?? altoText ?? melodyText;
            }
            
            if (referenceText == null) return;
            
            // Make sure the text layout is up to date
            LayoutRebuilder.ForceRebuildLayoutImmediate(referenceText.rectTransform);
            
            // Get the preferred width from the text (monospaced, so this represents the timeline width)
            float contentWidth = referenceText.preferredWidth;
            
            // Add a small padding so the last cell isn't glued to the right edge
            const float extraPadding = 20f;
            contentWidth += extraPadding;
            
            // Minimum width = viewport width (prevents scrollable areas smaller than viewport)
            RectTransform viewportRect = scrollRect.viewport as RectTransform;
            if (viewportRect != null)
            {
                float viewportWidth = viewportRect.rect.width;
                if (contentWidth < viewportWidth)
                {
                    contentWidth = viewportWidth;
                }
            }
            
            // Apply width to Content RectTransform
            scrollContent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                contentWidth
            );
        }
        
        /// <summary>
        /// Updates the target scroll position to follow the highlighted step during playback.
        /// Maps step index (0 to totalSteps-1) to normalized scroll position (0 to 1).
        /// Actual scrolling is done smoothly in Update() when autoscroll is enabled.
        /// </summary>
        private void UpdateScrollPosition(int stepIndex)
        {
            if (scrollRect == null) return;
            if (totalSteps <= 1) return; // No scrolling needed for single step or empty timeline
            if (stepIndex < 0 || stepIndex >= totalSteps) return; // Invalid step or highlight cleared
            
            // Map step index to 0..1 along the horizontal axis
            // For stepIndex 0, we want position 0 (left)
            // For stepIndex (totalSteps-1), we want position 1 (right)
            float t = totalSteps > 1 ? (float)stepIndex / (totalSteps - 1) : 0f;
            
            // Set target position (actual scrolling happens in Update())
            targetScrollNormalizedPosition = Mathf.Clamp01(t);
            hasScrollTarget = true;
            
            // If autoscroll is disabled, we still want the user to be able to
            // manually scroll, so do not force the ScrollRect here when off.
            if (!autoScrollEnabled)
                return;
            
            // Note: Smooth scrolling is handled in Update() per frame
        }
        
        /// <summary>
        /// Per-frame update that smoothly interpolates the scroll position toward the target.
        /// Uses exponential smoothing for frame-rate-independent behavior.
        /// </summary>
        private void Update()
        {
            if (!autoScrollEnabled) return;
            if (scrollRect == null) return;
            if (!hasScrollTarget) return;
            
            float current = scrollRect.horizontalNormalizedPosition;
            float target = targetScrollNormalizedPosition;
            
            // Early-out if we're already basically there
            if (Mathf.Abs(current - target) < 0.0001f)
            {
                scrollRect.horizontalNormalizedPosition = target;
                return;
            }
            
            // Frame-rate-independent smoothing using exponential interpolation
            // autoScrollLerpStrength roughly controls how fast we approach the target
            float k = Mathf.Clamp01(autoScrollLerpStrength);
            float lerpT = 1f - Mathf.Exp(-k * Time.deltaTime * 60f);
            // (60 is an arbitrary scale; adjust if you want slower/faster response)
            
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(current, target, lerpT);
        }
        
        /// <summary>
        /// Refreshes the display with optional highlighting on the specified step.
        /// </summary>
        private void RefreshDisplay(int highlightedStepIndex)
        {
            if (totalSteps == 0 || melodyCells.Count == 0)
            {
                // No timeline data, use existing strings
                SetVoiceText(melodyText, melodyLine);
                SetVoiceText(bassText, bassLine);
                SetVoiceText(tenorText, tenorLine);
                SetVoiceText(altoText, altoLine);
                SetVoiceText(chordSymbolRow, chordSymbolLine);
                SetVoiceText(romanNumeralRow, romanNumeralLine);
                return;
            }
            
            // Build strings with highlighting
            System.Text.StringBuilder melodyBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder bassBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder tenorBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder altoBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder chordAbsBuilder = new System.Text.StringBuilder();
            System.Text.StringBuilder chordRomanBuilder = new System.Text.StringBuilder();
            
            for (int i = 0; i < totalSteps && i < melodyCells.Count; i++)
            {
                bool isHighlight = (i == highlightedStepIndex && highlightedStepIndex >= 0);
                
                melodyBuilder.Append(HighlightCell(melodyCells[i], isHighlight, isMelody: true));
                
                if (i < bassCells.Count)
                    bassBuilder.Append(HighlightCell(bassCells[i], isHighlight, isMelody: false));
                if (i < tenorCells.Count)
                    tenorBuilder.Append(HighlightCell(tenorCells[i], isHighlight, isMelody: false));
                if (i < altoCells.Count)
                    altoBuilder.Append(HighlightCell(altoCells[i], isHighlight, isMelody: false));
                
                if (i < chordSymbolCells.Count)
                {
                    bool isNonDiatonic = i < chordSymbolIsNonDiatonic.Count ? chordSymbolIsNonDiatonic[i] : false;
                    chordAbsBuilder.Append(HighlightChordCell(chordSymbolCells[i], isHighlight, isNonDiatonic));
                }
                if (i < romanCells.Count)
                {
                    bool isNonDiatonic = i < romanIsNonDiatonic.Count ? romanIsNonDiatonic[i] : false;
                    chordRomanBuilder.Append(HighlightChordCell(romanCells[i], isHighlight, isNonDiatonic));
                }
            }
            
            // Update TMP fields
            SetVoiceText(melodyText, melodyBuilder.ToString());
            SetVoiceText(bassText, bassBuilder.ToString());
            SetVoiceText(tenorText, tenorBuilder.ToString());
            SetVoiceText(altoText, altoBuilder.ToString());
            SetVoiceText(chordSymbolRow, chordAbsBuilder.ToString());
            SetVoiceText(romanNumeralRow, chordRomanBuilder.ToString());
        }
        
        /// <summary>
        /// Applies highlight color to a SATB cell. Preserves existing color tags (e.g., leap colors).
        /// If cell has leap color, highlight wraps around it. Otherwise applies highlight or normal color.
        /// </summary>
        private string HighlightCell(string cellText, bool isHighlight, bool isMelody)
        {
            // If cell is all spaces, return as-is (no markup needed)
            if (string.IsNullOrWhiteSpace(cellText) || cellText.Trim().Length == 0)
            {
                return cellText;
            }
            
            // If cell already has color tags (e.g., leap coloring), wrap with highlight if needed
            if (cellText.Contains("<color="))
            {
                if (isHighlight)
                {
                    // Wrap existing color with highlight (nested colors - TMP should handle this)
                    string highlightHex = ColorUtility.ToHtmlStringRGBA(highlightColor);
                    return $"<color=#{highlightHex}>{cellText}</color>";
                }
                else
                {
                    // Keep existing color (leap colors), no highlight
                    return cellText;
                }
            }
            
            // No existing color - apply highlight or normal color
            string colorHex = ColorUtility.ToHtmlStringRGBA(isHighlight ? highlightColor : normalColor);
            return $"<color=#{colorHex}>{cellText}</color>";
        }
        
        /// <summary>
        /// Applies highlight color to a chord header cell. Handles non-diatonic coloring.
        /// </summary>
        private string HighlightChordCell(string cellText, bool isHighlight, bool isNonDiatonic)
        {
            // If cell is all spaces, return as-is
            if (string.IsNullOrWhiteSpace(cellText) || cellText.Trim().Length == 0)
            {
                return cellText;
            }
            
            // Determine base color (non-diatonic takes priority over normal, but highlight overrides both)
            Color baseColor;
            if (isHighlight)
            {
                baseColor = highlightColor;
            }
            else if (isNonDiatonic)
            {
                baseColor = nonDiatonicChordColor;
            }
            else
            {
                baseColor = normalColor;
            }
            
            string colorHex = ColorUtility.ToHtmlStringRGBA(baseColor);
            return $"<color=#{colorHex}>{cellText}</color>";
        }
        
        /// <summary>
        /// Clears all voicing labels and resets accumulators. Safe to call when no chord is active.
        /// </summary>
        public void Clear()
        {
            // Reset accumulator strings
            bassLine = string.Empty;
            tenorLine = string.Empty;
            altoLine = string.Empty;
            sopranoLine = string.Empty;
            melodyLine = string.Empty;
            chordSymbolLine = string.Empty;
            romanNumeralLine = string.Empty;
            chordSymbolCells.Clear();
            romanCells.Clear();
            chordSymbolIsNonDiatonic.Clear();
            romanIsNonDiatonic.Clear();

            // Reset previous chord storage
            previousChordSortedMidi = null;
            
            // Reset last MIDI tracking for sustain dash detection
            lastBassMidi = null;
            lastTenorMidi = null;
            lastAltoMidi = null;
            lastSopranoMidi = null;
            lastMelodyMidi = null;

            if (headerText != null)
            {
                headerText.text = "Current Voicing";
            }

            SetVoiceText(bassText, string.Empty);
            SetVoiceText(tenorText, string.Empty);
            SetVoiceText(altoText, string.Empty);
            SetVoiceText(sopranoText, string.Empty);
            SetVoiceText(melodyText, string.Empty);
            SetVoiceText(chordSymbolRow, string.Empty);
            SetVoiceText(romanNumeralRow, string.Empty);
        }

        /// <summary>
        /// Chooses the appropriate display name for a voice note using canonical chord spelling when available,
        /// falling back to key-based spelling otherwise.
        /// </summary>
        /// <param name="midi">MIDI note number</param>
        /// <param name="key">TheoryKey for fallback spelling</param>
        /// <param name="chordEvent">Optional chord event providing recipe context for canonical spelling</param>
        /// <returns>Note name string with canonical spelling if matched to chord tone, otherwise key-based spelling</returns>
        private static string ChooseDisplayNameForVoice(int midi, TheoryKey key, ChordEvent? chordEvent)
        {
            // If no chord context, use key-based spelling (backward compatibility)
            if (!chordEvent.HasValue)
            {
                return TheoryPitch.GetPitchNameFromMidi(midi, key);
            }

            var chord = chordEvent.Value;
            int notePc = TheoryPitch.PitchClassFromMidi(midi);

            // Compute root pitch class from chord recipe
            int rootPc = TheoryScale.GetDegreePitchClass(chord.Key, chord.Recipe.Degree);
            if (rootPc < 0)
            {
                rootPc = 0; // Fallback to C
            }
            rootPc = (rootPc + chord.Recipe.RootSemitoneOffset + 12) % 12;
            if (rootPc < 0)
                rootPc += 12;

            // Get canonical triad spelling, using key's accidental preference for enharmonic disambiguation
            string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, chord.Recipe.Quality, chord.Key, chord.Recipe.RootSemitoneOffset);
            if (triadNames == null || triadNames.Length < 3)
            {
                // No canonical spelling available (diminished/augmented/other) - use fallback
                return TheoryPitch.GetPitchNameFromMidi(midi, key);
            }

            // Compute expected pitch classes for triad tones
            int thirdPc;
            int fifthPc;

            switch (chord.Recipe.Quality)
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
                default:
                    // Other qualities (shouldn't happen if triadNames is not null, but fallback just in case)
                    return TheoryPitch.GetPitchNameFromMidi(midi, key);
            }

            // Check if this chord has a 7th extension and try canonical 7th chord spelling
            if (chord.Recipe.Extension == ChordExtension.Seventh && 
                chord.Recipe.SeventhQuality != SeventhQuality.None)
            {
                // Get canonical 7th chord spelling (root, 3rd, 5th, 7th) using key's accidental preference
                string[] seventhChordNames = TheorySpelling.GetSeventhChordSpelling(
                    rootPc, 
                    chord.Recipe.Quality, 
                    chord.Recipe.SeventhQuality, 
                    chord.Key,
                    chord.Recipe.RootSemitoneOffset);
                
                if (seventhChordNames != null && seventhChordNames.Length >= 4)
                {
                    // Calculate 7th pitch class based on seventh quality
                    int seventhPc = -1;
                    switch (chord.Recipe.SeventhQuality)
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
                    
                    // Match note pitch class to chord tone (root, 3rd, 5th, or 7th) and return canonical name
                    if (notePc == rootPc)
                    {
                        return seventhChordNames[0];  // Root
                    }
                    else if (notePc == thirdPc)
                    {
                        return seventhChordNames[1];  // Third
                    }
                    else if (notePc == fifthPc)
                    {
                        return seventhChordNames[2];  // Fifth
                    }
                    else if (seventhPc >= 0 && notePc == seventhPc)
                    {
                        return seventhChordNames[3];  // Seventh
                    }
                    else
                    {
                        // Note is not a chord tone (extension, suspension, etc.) - use fallback
                        return TheoryPitch.GetPitchNameFromMidi(midi, key);
                    }
                }
            }
            
            // Match note pitch class to triad tone and return canonical name
            if (notePc == rootPc)
            {
                return triadNames[0];  // Root
            }
            else if (notePc == thirdPc)
            {
                return triadNames[1];  // Third
            }
            else if (notePc == fifthPc)
            {
                return triadNames[2];  // Fifth
            }
            else
            {
                // Note is not a triad tone (7th, extension, suspension, etc.) - use fallback
                return TheoryPitch.GetPitchNameFromMidi(midi, key);
            }
        }

        /// <summary>
        /// Converts a MIDI note to a note name with octave number (e.g., "C4", "F#5").
        /// Uses canonical chord spelling when available, falling back to key-based spelling.
        /// Pads to fixed width (4 characters) to preserve column alignment in the grid.
        /// </summary>
        /// <param name="midi">MIDI note number</param>
        /// <param name="key">TheoryKey for fallback spelling</param>
        /// <param name="chordEvent">Optional chord event providing recipe context for canonical spelling</param>
        /// <returns>Note name with octave, padded to 4 characters (e.g., "C4  ", "C#5 ", "Bb3 ")</returns>
        private static string NoteNameWithOctave(int midi, TheoryKey key, ChordEvent? chordEvent)
        {
            // Get note name using canonical spelling if available
            string noteName = ChooseDisplayNameForVoice(midi, key, chordEvent);
            
            // Calculate octave: MIDI 60 = C4, so octave = (midi / 12) - 1
            int octave = (midi / 12) - 1;
            
            string noteWithOctave = $"{noteName}{octave}";
            
            // Pad to fixed width (4 characters) to preserve column alignment
            // Examples: "C4" -> "C4  ", "C#5" -> "C#5 ", "Bb3" -> "Bb3 "
            const int fixedWidth = 4;
            if (noteWithOctave.Length < fixedWidth)
            {
                noteWithOctave = noteWithOctave.PadRight(fixedWidth);
            }
            else if (noteWithOctave.Length > fixedWidth)
            {
                noteWithOctave = noteWithOctave.Substring(0, fixedWidth);
            }
            
            return noteWithOctave;
        }

        /// <summary>
        /// Pads a note name to a fixed width of 2 characters for consistent alignment in the SATB grid.
        /// Natural notes like "C" become "C ", while accidentals like "C#" or "Db" remain unchanged.
        /// If the name is longer than 2 characters, it's truncated to 2 characters.
        /// </summary>
        /// <param name="name">The note name to pad</param>
        /// <returns>A padded note name with fixed width of 2 characters</returns>
        private static string PadNote(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "  ";

            if (name.Length >= 2)
                return name.Substring(0, 2);    // Clamp to 2 chars for now

            // name.Length == 1 → pad right with space
            return name + " ";
        }

        /// <summary>
        /// Wraps a token in red color tag if it represents a large leap.
        /// </summary>
        /// <param name="token">The token (note name) to potentially color</param>
        /// <param name="isLeap">Whether this token represents a large leap</param>
        /// <returns>The token, wrapped in color tag if it's a leap, otherwise unchanged</returns>
        private static string ColorIfLeap(string token, bool isLeap)
        {
            if (!isLeap || string.IsNullOrEmpty(token))
                return token;
            return $"<color=#FF6666>{token}</color>";
        }


        /// <summary>
        /// Helper method to append a token to an existing string with spacing.
        /// Uses minimal spacing (1 space) to keep baseline compact.
        /// </summary>
        /// <param name="existing">The existing string to append to</param>
        /// <param name="token">The token to append</param>
        /// <returns>The combined string with spacing</returns>
        private static string AppendToken(string existing, string token)
        {
            if (string.IsNullOrEmpty(token))
                token = "(none)";

            if (string.IsNullOrEmpty(existing))
                return token;

            // Minimal spacing between tokens (reduced from 3 to 1 for compact baseline)
            return existing + " " + token;
        }

        /// <summary>
        /// Helper method to append a token with duration-based trailing padding.
        /// The paddingSpaces parameter already includes base + extra spacing from ChordLabController.
        /// </summary>
        /// <param name="existing">The existing string to append to</param>
        /// <param name="token">The token to append</param>
        /// <param name="paddingSpaces">Total trailing spaces to add after the token (base + extra)</param>
        /// <returns>The combined string with spacing and padding</returns>
        private static string AppendTokenWithPadding(string existing, string token, int paddingSpaces)
        {
            if (string.IsNullOrEmpty(token))
                token = "(none)";

            if (string.IsNullOrEmpty(existing))
            {
                // First token: just add padding after it
                return token + new string(' ', paddingSpaces);
            }

            // Add minimal spacing between tokens (1 space) + padding after new token
            // The paddingSpaces already includes base + extra from ChordLabController
            return existing + " " + token + new string(' ', paddingSpaces);
        }

        /// <summary>
        /// Helper method to safely set text on a TextMeshProUGUI field.
        /// </summary>
        /// <param name="field">The TextMeshProUGUI field to update, or null</param>
        /// <param name="value">The text value to set</param>
        private static void SetVoiceText(TextMeshProUGUI field, string value)
        {
            if (field != null)
            {
                field.text = value ?? string.Empty;
            }
        }
    }
}

