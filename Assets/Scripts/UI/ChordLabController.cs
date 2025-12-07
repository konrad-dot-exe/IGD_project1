using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sonoria.MusicTheory;

namespace EarFPS
{
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
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private TMP_Dropdown tonicDropdown; // Optional: if null, defaults to C (tonicPc=0)
        [SerializeField] private TMP_InputField progressionInput;
        [SerializeField] private TMP_InputField Input_MelodyNoteNames; // User input field for melody note names
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Chord Grid")]
        [SerializeField] private Transform chordGridContainer;       // Content of Scroll_ChordGrid
        [SerializeField] private ChordColumnView chordColumnPrefab;

        [Header("Voicing Viewer")]
        [Tooltip("Optional UI panel that displays the actual voiced chord (bass–tenor–alto–soprano) during debug playback.")]
        [SerializeField] private VoicingViewer voicingViewer;
        [Tooltip("Optional piano keyboard display that highlights currently sounding MIDI notes.")]
        [SerializeField] private PianoKeyboardDisplay pianoKeyboardDisplay;

        [Header("Music")]
        [SerializeField] private MusicDataController musicDataController; // Optional, reserved for future use
        [SerializeField] private FmodNoteSynth synth;

        [Header("Settings")]
        [SerializeField] private int rootOctave = 4;

        /// <summary>
        /// Computes the upper voice MIDI range based on rootOctave.
        /// Returns (upperMinMidi, upperMaxMidi) tuple.
        /// NOTE: upperMinMidi uses +5 (F) instead of +7 (G) so 7ths at the bottom
        /// of the range (e.g. G3 in A7/C#) can resolve down by step to F3.
        /// </summary>
        private (int upperMinMidi, int upperMaxMidi) ComputeUpperVoiceRange()
        {
            int upperMinMidi = rootOctave * 12 + 5;  // F in octave (rootOctave - 1), allows inner-voice 7ths like G3 to resolve down to F3
            int upperMaxMidi = (rootOctave + 3) * 12 + 9;  // A in octave (rootOctave + 2), allows A5 and higher when rootOctave = 4 (+12 semitones)
            return (upperMinMidi, upperMaxMidi);
        }
        [SerializeField] private float chordDurationSeconds = 1.0f;
        [SerializeField] private float gapBetweenChordsSeconds = 0.1f;
        [SerializeField] private float velocity = 0.9f; // 0-1 range for FMOD
        
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
        private TheoryKey lastVoicedKey;

        [Header("Harmonization Settings (Naive Skeleton)")]
        [SerializeField] private bool harmonizationPreferTonicStart = true;
        [SerializeField] private bool harmonizationPreferChordContinuity = true;
        [SerializeField] private bool harmonizationEnableDetailedReasonLogs = true;

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

        private Coroutine playRoutine;

        void Awake()
        {
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
        /// Sets up the mode dropdown with all 7 diatonic modes.
        /// </summary>
        private void SetupModeDropdown()
        {
            if (modeDropdown == null) return;

            modeDropdown.options.Clear();
            
            // Add modes (tonic is now selected separately)
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Ionian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Dorian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Phrygian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Lydian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Mixolydian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Aeolian"));
            modeDropdown.options.Add(new TMP_Dropdown.OptionData("Locrian"));

            modeDropdown.value = 0; // Default to Ionian
            modeDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Sets up the tonic dropdown with all 12 pitch classes.
        /// </summary>
        private void SetupTonicDropdown()
        {
            if (tonicDropdown == null) return;

            tonicDropdown.options.Clear();
            
            // Add all 12 pitch classes
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("C"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("C#/Db"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("D"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Eb"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("E"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("F"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("F#/Gb"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("G"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Ab"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("A"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("Bb"));
            tonicDropdown.options.Add(new TMP_Dropdown.OptionData("B"));

            tonicDropdown.value = 0; // Default to C
            tonicDropdown.RefreshShownValue();
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
                Debug.Log("[ChordLab] OnPlayClicked called");

            // Temporary debug log to test melody input
            Debug.Log("[MelodyInput] " + GetMelodyInput());

            // Stop any existing playback
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

            // Start new playback coroutine
            playRoutine = StartCoroutine(PlayProgressionCo());
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
        /// UI callback for the Play Voiced button.
        /// Takes the current manual progression from the input field,
        /// voice-leads it with the current melody, and plays it with SATB display.
        /// </summary>
        private void OnPlayVoicedClicked()
        {
            if (enableDebugLogs)
                Debug.Log("[ChordLab] OnPlayVoicedClicked called");

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

            // Use the new method to voice-lead manual progression with melody
            PlayManualProgressionWithMelodyVoiced();
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
                Debug.Log($"[ChordLab] Selected key: {key} (tonicPc={tonicPc}, mode={selectedMode})");
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

            // Parse progression to tokens and recipes
            // Wrap in try/catch to handle any unexpected exceptions gracefully
            bool parseSuccess = false;
            List<string> originalTokens = null;
            List<ChordRecipe> recipes = null;
            
            try
            {
                parseSuccess = ParseProgressionToRecipes(key, input, out originalTokens, out recipes);
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

            // Adjust recipes to match diatonic triad quality for the mode (if enabled)
            var adjustedRecipes = new List<ChordRecipe>(recipes.Count);
            var adjustedNumerals = new List<string>(recipes.Count);
            var hadAdjustments = false;
            var warningBuilder = new System.Text.StringBuilder();
            
            for (int i = 0; i < recipes.Count; i++)
            {
                var originalRecipe = recipes[i];
                
                if (autoCorrectToMode)
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
                    // No correction: keep original recipe
                    adjustedRecipes.Add(originalRecipe);
                    adjustedNumerals.Add(originalTokens[i]);
                }
            }

            // Build chord events for voicing display (always created, even if voicing engine is disabled)
            List<ChordEvent> chordEvents = null;
            List<VoicedChord> voicedChords = null;
            
            // Always create chord events for VoicingViewer display
                try
                {
                chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);
                    
                    // Build test melody if enabled
                    if (useTestMelodyForPlayback)
                    {
                        // Use serialized test melody pattern (defaults to [3, 4, 4, 2, 1] if empty)
                        if (testMelodyDegrees == null || testMelodyDegrees.Length == 0)
                        {
                            testMelodyDegrees = new int[] { 3, 4, 4, 2, 1 };
                        }
                        
                        int melodyMinMidi = 60; // C4
                        int melodyMaxMidi = 80; // E5
                        
                    for (int i = 0; i < chordEvents.Count; i++)
                        {
                            // Wrap melody pattern if needed
                            int degreeIndex = i % testMelodyDegrees.Length;
                            int degree = testMelodyDegrees[degreeIndex];
                            
                            // Get base MIDI from degree in octave 4
                            int baseMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                            if (baseMidi >= 0)
                            {
                                // Ensure melody is in reasonable soprano range (60-80)
                                int melodyMidi = EnsureInRange(baseMidi, melodyMinMidi, melodyMaxMidi);
                                
                                // Apply melody octave offset (only affects playback register, not theory)
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

            // Voice the progression (if enabled for playback, or always for display)
            if (useVoicingEngine || voicingViewer != null)
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
                    
                    // Generate voiced chords for playback or display
                    if (chordEvents != null)
                    {
                    // Call appropriate voicing method based on melody flag
                    if (useTestMelodyForPlayback)
                    {
                        voicedChords = TheoryVoicing.VoiceLeadProgressionWithMelody(
                                chordEvents,
                            numVoices: 4,
                            rootOctave: rootOctave,
                            bassOctave: rootOctave - 1,
                            upperMinMidi: upperMinMidi,
                            upperMaxMidi: upperMaxMidi
                        );
                        
                        if (enableDebugLogs && voicedChords != null && voicedChords.Count == adjustedRecipes.Count)
                        {
                                Debug.Log($"[ChordLab] Using melody-constrained voicing ({voicedChords.Count} voiced chords)");
                        }
                    }
                    else
                    {
                        voicedChords = TheoryVoicing.VoiceLeadProgression(
                                chordEvents,
                            numVoices: 4,
                                bassOctave: rootOctave - 1,
                            upperMinMidi: upperMinMidi,
                            upperMaxMidi: upperMaxMidi
                        );
                        
                        if (enableDebugLogs && voicedChords != null && voicedChords.Count == adjustedRecipes.Count)
                        {
                                Debug.Log($"[ChordLab] Using chord-only voicing ({voicedChords.Count} voiced chords)");
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
            RenderChordGrid(key, originalTokens, recipes, chords, adjustedRecipes);

            // Update status
            string progressionStr = string.Join(" ", originalTokens);
            string statusMessage = $"Playing {chords.Count} chords in {key}: {progressionStr}";
            
            // Append warnings if any adjustments were made (only when auto-correct is enabled)
            if (autoCorrectToMode && hadAdjustments)
            {
                statusMessage += "\n" + warningBuilder.ToString();
            }
            
            UpdateStatus(statusMessage);

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

                // Update VoicingViewer if available (always use voiced chords for display if available)
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                if (voicingViewer != null)
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
                    
                    // Pass VoicesMidi array directly - VoicingViewer will use index order [Bass, Tenor, Alto, Soprano]
                    voicingViewer.ShowVoicing(
                        key,
                        stepIndex: chordIndex + 1,
                        totalSteps: chords.Count,
                        midiNotes: midiNotesForDisplay,
                        chordEvent: chordEventForDisplay);
                }

                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Playing chord {chordIndex + 1}/{chords.Count}: MIDI notes [{string.Join(", ", midiNotesToPlay)}]");

                // Update piano keyboard display if available
                if (pianoKeyboardDisplay != null && midiNotesToPlay != null && midiNotesToPlay.Length > 0)
                {
                    pianoKeyboardDisplay.SetActiveNotes(midiNotesToPlay);
                }

                // Play all notes in the chord simultaneously (block chord)
                // This helper handles optional bass doubling based on emphasizeBassWithLowOctave
                PlayChord(midiNotesToPlay, chordDurationSeconds);

                // Wait for chord duration + gap before next chord
                float waitTime = chordDurationSeconds + gapBetweenChordsSeconds;
                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Waiting {waitTime} seconds before next chord");
                yield return new WaitForSeconds(waitTime);
                chordIndex++;
            }

            // Playback complete
            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Playback complete");
            
            string completionMessage = $"Completed. Parsed progression: {progressionStr}";
            if (autoCorrectToMode && hadAdjustments)
            {
                completionMessage += "\n" + warningBuilder.ToString();
            }
            
            UpdateStatus(completionMessage);
            playRoutine = null;
        }

        /// <summary>
        /// Plays a chord with optional bass doubling an octave below.
        /// Only affects playback; does not modify the original chord data.
        /// </summary>
        /// <param name="midiNotes">The MIDI notes to play</param>
        /// <param name="duration">Duration of each note</param>
        private void PlayChord(IReadOnlyList<int> midiNotes, float duration)
        {
            if (midiNotes == null || midiNotes.Count == 0)
                return;

            // Build a local list so we don't mutate the original
            var notesToPlay = new List<int>(midiNotes);

            if (emphasizeBassWithLowOctave)
            {
                // Find the lowest note in the chord (bass note)
                int bass = notesToPlay[0];
                for (int i = 1; i < notesToPlay.Count; i++)
                {
                    if (notesToPlay[i] < bass)
                        bass = notesToPlay[i];
                }

                // Calculate bass note an octave below
                int lowBass = bass - 12;
                
                // Clamp to valid MIDI range (0-127)
                if (lowBass < 0) lowBass = 0;
                if (lowBass > 127) lowBass = 127;

                // Add the low bass note if it's not already in the chord
                // (unlikely but possible if chord spans multiple octaves)
                if (!notesToPlay.Contains(lowBass))
                {
                    notesToPlay.Insert(0, lowBass); // Insert at front for clarity
                }
            }

            // Debug logging: log what audio actually plays (when tendency debug is enabled)
            if (TheoryVoicing.GetTendencyDebug())
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[Audio Debug] Playing chord: ");
                for (int i = 0; i < notesToPlay.Count; i++)
                {
                    int midi = notesToPlay[i];
                    string name = TheoryPitch.GetPitchNameFromMidi(midi, GetKeyFromDropdowns());
                    sb.Append($"Note{i}={name}({midi}) ");
                }
                Debug.Log(sb.ToString());
            }

            // Play all notes in the augmented list
            foreach (int midiNote in notesToPlay)
            {
                // if (enableDebugLogs)
                //     Debug.Log($"[ChordLab] Calling synth.PlayOnce(midi={midiNote}, velocity={velocity}, duration={duration})");
                synth.PlayOnce(midiNote, velocity, duration);
            }
        }

        /// <summary>
        /// Maps dropdown index to ScaleMode enum.
        /// </summary>
        private Sonoria.MusicTheory.ScaleMode GetModeFromDropdown(int index)
        {
            return index switch
            {
                0 => Sonoria.MusicTheory.ScaleMode.Ionian,
                1 => Sonoria.MusicTheory.ScaleMode.Dorian,
                2 => Sonoria.MusicTheory.ScaleMode.Phrygian,
                3 => Sonoria.MusicTheory.ScaleMode.Lydian,
                4 => Sonoria.MusicTheory.ScaleMode.Mixolydian,
                5 => Sonoria.MusicTheory.ScaleMode.Aeolian,
                6 => Sonoria.MusicTheory.ScaleMode.Locrian,
                _ => Sonoria.MusicTheory.ScaleMode.Ionian // Fallback
            };
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
            IReadOnlyList<ChordRecipe> adjustedRecipes)
        {
            // Clear existing children
            if (chordGridContainer != null)
            {
                foreach (Transform child in chordGridContainer)
                {
                    Destroy(child.gameObject);
                }
            }

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

                // Get root note name using key-aware degree spelling
                // This ensures proper spelling (e.g., #vi in C Aeolian → A, not Bbb)
                string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                    key,
                    adjustedRecipe.Degree,
                    adjustedRecipe.RootSemitoneOffset);

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
                
                // Get canonical triad spelling using RootSemitoneOffset for enharmonic disambiguation
                string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, originalRecipe.Quality, originalRecipe.RootSemitoneOffset);
                
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
                        // Get canonical 7th chord spelling (root, 3rd, 5th, 7th)
                        string[] seventhChordNames = TheorySpelling.GetSeventhChordSpelling(
                            rootPc, 
                            originalRecipe.Quality, 
                            originalRecipe.SeventhQuality, 
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

                // Build chord symbol using adjusted recipe (for display)
                // rootNoteName is now the actual root, not the bass note
                string chordSymbol = TheoryChord.GetChordSymbol(key, adjustedRecipe, rootNoteName, bassMidi);

                // Generate key-aware Roman numeral for display (shows 'n' when appropriate)
                string displayRoman = TheoryChord.RecipeToRomanNumeral(key, adjustedRecipe);

                // Instantiate column prefab and set chord data with status and analysis info
                var columnInstance = Instantiate(chordColumnPrefab, chordGridContainer);
                columnInstance.SetChord(chordSymbol, noteNames, displayRoman, status, analysisInfo);

                if (enableDebugLogs)
                {
                    string notesStr = string.Join("/", noteNames);
                    Debug.Log($"[ChordLab] Column {i}: {chordSymbol} - {notesStr} ({noteNames.Count} notes) - {originalToken} - Status: {status}");
                }
            }
        }

        /// <summary>
        /// Updates the chord grid from a list of ChordEvents.
        /// Extracts recipes, builds chord MIDI arrays, and calls RenderChordGrid.
        /// </summary>
        private void UpdateChordGridFromChordEvents(TheoryKey key, IReadOnlyList<ChordEvent> chordEvents)
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
            RenderChordGrid(key, originalTokens, recipes, chords, recipes);
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
        /// Parses the progression input text into tokens and recipes.
        /// Reusable helper for both playback and debug analysis.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="inputText">The progression input text</param>
        /// <param name="originalTokens">Output: List of original Roman numeral strings</param>
        /// <param name="recipes">Output: List of parsed ChordRecipe objects</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        private bool ParseProgressionToRecipes(TheoryKey key, string inputText, out List<string> originalTokens, out List<ChordRecipe> recipes)
        {
            originalTokens = new List<string>();
            recipes = new List<ChordRecipe>();

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

            // Validate each numeral and store recipes
            foreach (string token in originalTokens)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, token, out var recipe))
                {
                    return false; // Parsing failed
                }
                recipes.Add(recipe);
            }

            return true;
        }

#if UNITY_EDITOR
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
                    parseSuccess = ParseProgressionToRecipes(key, progressionText, out originalTokens, out recipes);
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

            char[] separators = { ' ', '\t', '\n', '\r' };
            string[] tokens = melodyInput
                .Split(separators, System.StringSplitOptions.RemoveEmptyEntries);

            Debug.Log($"[ChordLab] Split into {tokens.Length} tokens. Tokens: [{string.Join(" | ", tokens.Select((t, i) => $"#{i}:'{t}'"))}]");

            if (tokens.Length == 0)
            {
                Debug.LogWarning("[ChordLab] No tokens found after splitting melody input.");
                return null;
            }

            var melody = new List<MelodyEvent>(tokens.Length);

            // Match the beat spacing BuildTestMelodyLine() uses (1.0f per note)
            float beatDuration = 1.0f;
            float time = 0f;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim(); // Trim each token individually
                
                Debug.Log($"[ChordLab] Processing token {i + 1}/{tokens.Length}: '{token}' (Length={token.Length})");

                // Log character-by-character for debugging
                if (token.Length > 0)
                {
                    var chars = token.Select(c => $"'{c}'({(int)c})").ToArray();
                    Debug.Log($"[ChordLab] Token '{token}' characters: [{string.Join(", ", chars)}]");
                }

                if (!TryParseNoteNameToMidi(token, out int midi, out AccidentalHint detectedAccidental))
                {
                    Debug.LogWarning($"[ChordLab] ⚠️ FAILED to parse token '{token}' at position {i + 1}. Skipping.");
                    continue; // Skip invalid tokens instead of aborting
                }

                Debug.Log($"[ChordLab] ✓ Successfully parsed '{token}' -> MIDI {midi}, AccidentalHint={detectedAccidental}");

                // Use the same MelodyEvent construction pattern as BuildTestMelodyLine()
                // but include the accidental hint from the parsed token
                var evt = new MelodyEvent
                {
                    TimeBeats = time,
                    DurationBeats = beatDuration,
                    Midi = midi,
                    AccidentalHint = detectedAccidental
                };
                melody.Add(evt);

                time += beatDuration;
            }

            Debug.Log($"[ChordLab] Final result: Built {melody.Count} melody events from {tokens.Length} tokens.");

            // Return null only if no valid notes were parsed
            if (melody.Count == 0)
            {
                Debug.LogWarning("[ChordLab] ⚠️ No valid notes could be parsed from melody input.");
                return null;
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
                Mode = lastVoicedKey.Mode.ToString(),
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

                // Get canonical triad spelling
                string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, chord.Recipe.Quality, chord.Recipe.RootSemitoneOffset);
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
                Debug.LogWarning("[ChordLab] Voicing failed; nothing to play.");
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Voiced {voicedChords.Count} chords for naive harmonization playback");
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

            // 6. Update chord grid with harmonized chords
            UpdateChordGridFromChordEvents(key, chordEvents);

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
            StartCoroutine(PlayVoicedChordSequenceCo(voicedChords, chordEvents, key));
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

            // 2. Build the melody line (same logic as N.H.)
            List<MelodyEvent> melodyLine = BuildNoteNameMelodyLineFromInspector();

            // Fallback: existing degree-based test melody
            if (melodyLine == null || melodyLine.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ChordLab] Note-name melody build returned null/empty. Falling back to degree-based test melody.");
                melodyLine = BuildTestMelodyLine(key);
            }

            if (melodyLine == null || melodyLine.Count == 0)
            {
                Debug.LogWarning("[ChordLab] No test melody available for voiced manual progression playback.");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Using melody with {melodyLine.Count} events for manual progression voicing.");

            // 3. Parse the Roman numeral progression from the input field
            if (progressionInput == null || string.IsNullOrWhiteSpace(progressionInput.text))
            {
                Debug.LogWarning("[ChordLab] Progression input is empty. Cannot voice manual progression.");
                return;
            }

            bool parseSuccess = ParseProgressionToRecipes(key, progressionInput.text, out List<string> originalTokens, out List<ChordRecipe> recipes);

            if (!parseSuccess || recipes == null || recipes.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Could not parse progression. Check for invalid Roman numerals.");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Parsed {recipes.Count} chords from progression: {string.Join(" ", originalTokens)}");

            // 4. Adjust recipes to match diatonic triad quality for the mode (if enabled)
            var adjustedRecipes = new List<ChordRecipe>(recipes.Count);
            for (int i = 0; i < recipes.Count; i++)
            {
                var originalRecipe = recipes[i];
                if (autoCorrectToMode)
                {
                    var adjusted = TheoryChord.AdjustTriadQualityToMode(key, originalRecipe, out bool wasAdjusted);
                    adjustedRecipes.Add(adjusted);
                    if (wasAdjusted && enableDebugLogs)
                    {
                        string adjustedNumeral = TheoryChord.RecipeToRomanNumeral(key, adjusted);
                        Debug.Log($"[ChordLab] Adjusted '{originalTokens[i]}' to '{adjustedNumeral}' to fit {key}");
                    }
                }
                else
                {
                    adjustedRecipes.Add(originalRecipe);
                }
            }

            // 5. Build ChordEvents from recipes with timing
            List<ChordEvent> chordEvents = TheoryVoicing.BuildChordEventsFromRecipes(key, adjustedRecipes, 0f, 1f);

            if (chordEvents == null || chordEvents.Count == 0)
            {
                Debug.LogWarning("[ChordLab] Failed to build chord events from recipes.");
                return;
            }

            // 6. Match melody events to chord events (1:1 by index, updating TimeBeats and MelodyMidi)
            // If we have more chords than melody notes, repeat the last melody note for remaining chords
            // If we have more melody notes than chords, only use the first N melody notes
            int melodyCount = melodyLine.Count;
            int chordCount = chordEvents.Count;

            for (int i = 0; i < chordCount; i++)
            {
                int melodyIndex = i < melodyCount ? i : melodyCount - 1; // Use last melody note if we run out
                var melodyEvent = melodyLine[melodyIndex];

                // Apply melody octave offset (only affects playback register, not theory)
                int melodyMidiWithOffset = melodyEvent.Midi + MelodyOffsetSemitones;
                
                chordEvents[i] = new ChordEvent
                {
                    Key = chordEvents[i].Key,
                    Recipe = chordEvents[i].Recipe,
                    TimeBeats = melodyEvent.TimeBeats, // Use melody's time to keep them synchronized
                    MelodyMidi = melodyMidiWithOffset
                };
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ChordLab] Matched {chordCount} chords with {melodyCount} melody events");
                for (int i = 0; i < chordEvents.Count; i++)
                {
                    var evt = chordEvents[i];
                    string melodyName = evt.MelodyMidi.HasValue
                        ? TheoryPitch.GetPitchNameFromMidi(evt.MelodyMidi.Value, evt.Key)
                        : "(none)";
                    Debug.Log($"[ChordLab] ChordEvent {i + 1}: Melody={melodyName}, TimeBeats={evt.TimeBeats}");
                }
            }

            // 7. Voice-lead the progression with melody using the existing voicing engine
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
                Debug.LogWarning("[ChordLab] Voicing failed; nothing to play.");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[ChordLab] Voiced {voicedChords.Count} chords for manual progression playback");

            // 8. Store state for export
            lastVoicedMelodyLine = new List<MelodyEvent>(melodyLine);
            lastVoicedChordEvents = new List<ChordEvent>(chordEvents);
            lastVoicedChords = new List<VoicedChord>(voicedChords);
            lastVoicedKey = key;

            // 9. Clear both viewers before starting playback
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

            // 10. Update ChordGrid with the manual progression
            UpdateChordGridFromChordEvents(key, chordEvents);

            // 11. Start playback coroutine that updates VoicingViewer and plays audio
            StartCoroutine(PlayVoicedChordSequenceCo(voicedChords, chordEvents, key));
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
        private IEnumerator PlayVoicedChordSequenceCo(List<VoicedChord> voicedChords, IReadOnlyList<ChordEvent> chordEvents, TheoryKey key)
        {
            if (voicedChords == null || voicedChords.Count == 0)
                yield break;

            // Clear piano keyboard display at start of playback
            if (pianoKeyboardDisplay != null)
            {
                pianoKeyboardDisplay.SetActiveNotes(new int[0]); // Clear all highlights
            }

            // Validate that chordEvents list matches voicedChords count
            if (chordEvents == null || chordEvents.Count != voicedChords.Count)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[ChordLab] Mismatch: {voicedChords.Count} voiced chords but {chordEvents?.Count ?? 0} chord events. Continuing without chord context.");
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

                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Playing voiced chord {i + 1}/{voicedChords.Count}: MIDI notes [{string.Join(", ", voiced.VoicesMidi)}]");
                
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

                // Update voicing viewer if available
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                // Pass VoicesMidi directly to VoicingViewer - it expects this exact order.
                if (voicingViewer != null)
                {
                    // Get corresponding chord event if available
                    ChordEvent? chordEvent = null;
                    if (chordEvents != null && i < chordEvents.Count)
                    {
                        chordEvent = chordEvents[i];
                    }

                    // Pass VoicesMidi array directly - VoicingViewer will use index order [Bass, Tenor, Alto, Soprano]
                    voicingViewer.ShowVoicing(
                        key,
                        stepIndex: i + 1,
                        totalSteps: voicedChords.Count,
                        midiNotes: voiced.VoicesMidi,
                        chordEvent: chordEvent);
                }

                // Update piano keyboard display if available
                if (pianoKeyboardDisplay != null && voiced.VoicesMidi != null && voiced.VoicesMidi.Length > 0)
                {
                    pianoKeyboardDisplay.SetActiveNotes(voiced.VoicesMidi);
                }

                // Use existing PlayChord helper which handles bass doubling and synth playback
                // TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
                // PlayChord receives the same VoicesMidi array that VoicingViewer uses.
                PlayChord(voiced.VoicesMidi, chordDurationSeconds);

                // Wait for chord duration + gap before next chord
                float waitTime = chordDurationSeconds + gapBetweenChordsSeconds;
                if (enableDebugLogs)
                    Debug.Log($"[ChordLab] Waiting {waitTime} seconds before next chord");
                yield return new WaitForSeconds(waitTime);
            }

            if (enableDebugLogs)
                Debug.Log("[ChordLab] Naive harmonization playback complete");

            // Voicing viewer is NOT cleared here - accumulated SATB sequence remains visible after playback
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
    }
}

