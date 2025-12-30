# Chord Lab Panel Documentation

## Overview

The Chord Lab panel is a new UI feature in the `LLM_Chat_Terminal` scene that allows users to input Roman numeral chord progressions and play them using the TheoryChord system. This is a manual, interactive tool that demonstrates chord kernel functionality. The system includes a sophisticated voicing layer (TheoryVoicing) with intelligent voice-leading rules, including hard constraints for 7th chord resolution, resolution-aware first-chord placement, and melody-aware behavior. Editor-only debug tools are available for analyzing chord voicings and voice-leading patterns.

## Location

- **Scene:** `Assets/Scenes/LLM_Chat_Terminal.unity`
- **Panel GameObject:** `Canvas/Panel_ChordLab`
- **Controller Script:** `Assets/Scripts/UI/ChordLabController.cs`

## Features

### Current Capabilities

1. **Key Selection**

   - **Tonic Dropdown:** Select any of 12 pitch classes as the key center (C, C#/Db, D, Eb, E, F, F#/Gb, G, Ab, A, Bb, B)
   - **Mode Dropdown:** Select from 7 diatonic modes:
     - Ionian (Major)
     - Dorian
     - Phrygian
     - Lydian
     - Mixolydian
     - Aeolian (Natural Minor)
     - Locrian
   - Combined selection creates a `TheoryKey` (e.g., G Ionian, D Aeolian, F# Lydian)
   - Default: C Ionian (preserves previous behavior)

2. **Roman Numeral Input**

   - Text input field for space-separated Roman numerals
   - Supports:
     - Basic triads: `I`, `ii`, `iii`, `IV`, `V`, `vi`, `vii`
     - Quality suffixes: `viidim`, `Iaug`
     - 7th chords: `I7`, `ii7`, `V7`, `viidim7`, `Iaug7`, `Imaj7`, `iiø7` (half-diminished)
     - Case indicates quality: uppercase = Major, lowercase = Minor
     - Optional `°` symbol support for diminished (e.g., `vii°`, `vii°7`)
     - Leading accidentals: `b` (flat), `#` (sharp), `n`/`N` (natural - parallel Ionian)
     - Inversion suffixes: `/3rd` or `/3` (first inversion), `/5th` or `/5` (second inversion), `/7th` or `/7` (third inversion)
     - **Extension suffixes:** `9`, `b9`, `add9`, `11`, `#11`, `add11`, `sus4`, `7sus4` (e.g., `V7b9`, `Imaj9`, `ii7sus4`)
       - Extensions are parsed and enforced in voicing (requested tensions must appear in realized voicing)
       - `add9`/`add11` are optional color tones (preferred but not required)
       - `9`/`b9`/`#11` are melodic tensions (required when specified)
       - `sus4`/`7sus4` are suspension modifiers (affect chord structure)
     - **Duration suffixes:** `:N` syntax for specifying chord duration in quarter notes (e.g., `I:2` = 2 quarters, `V:4` = 4 quarters/whole note)
       - Default duration is 1 quarter note when no suffix is present
       - Duration affects both timeline representation and playback timing
       - Invalid durations (e.g., `:0`) are warned and default to 1 quarter
     - Examples: `bII`, `#iv`, `nvi`, `IVmaj7/3rd`, `V7/5th`, `I:2 V vi IV`, `V7b9`, `Imaj9`, `ii7sus4` (first chord held for 2 quarters)
   - **Quality Correction (Optional):**
     - Can be enabled/disabled via `autoCorrectToMode` toggle in Inspector
     - When enabled: Automatically adjusts triad qualities to match diatonic triads for the selected mode
     - When disabled: Chords play exactly as typed (non-diatonic chords sound with their specified quality)
     - 7th chords are not adjusted (quality adjustment only applies to triads)
     - Roman numerals in display show original user input (not corrected)
     - Warning messages indicate when adjustments are made (only when auto-correct is enabled)

3. **Chord Playback**

   - Builds chords using `TheoryChord.BuildChord()` (individual chord building)
   - Root note comes from the mode; third and fifth intervals are calculated from `ChordQuality`
   - Non-diatonic chords (e.g., III, iv) now sound correctly with their specified quality when auto-correct is disabled
   - Supports both triads (3 notes) and 7th chords (4 notes)
   - Supports inversions: chords can be rotated with lowest note(s) moved up an octave
   - Plays block chords using `FmodNoteSynth.PlayOnce()`
   - **Timeline-Based Playback Timing:**
     - Chord hold duration is proportional to the region's `durationTicks` (from `:N` suffix)
     - Formula: `holdSeconds = chordDurationSeconds * (durationTicks / ticksPerQuarter)`
     - Example: `I:2` with `chordDurationSeconds=1.0` and `ticksPerQuarter=4` → holds for 2.0 seconds
     - Regular chords without suffix (default 1 quarter) still use `chordDurationSeconds`
     - Gap between chords remains fixed (`gapBetweenChordsSeconds`)
     - Chords are played once (not repeated) but held longer for extended durations
   - Configurable:
     - Chord duration (default: 1.0s) - base duration for one quarter note
     - Gap between chords (default: 0.1s) - fixed pause between chords
     - Root octave (default: 4)
     - Velocity (default: 0.9)
     - Bass doubling: `emphasizeBassWithLowOctave` toggle (default: true) - doubles the bass note an octave below
     - Timeline resolution: `ticksPerQuarter` (default: 4) - controls timeline granularity

4. **Status Messages and Diagnostics**

   - Real-time feedback on parsing and playback
   - Error messages for invalid numerals
   - Progress indication during playback
   - Quality adjustment warnings when chord qualities are corrected to match the mode
   - Format: `"Adjusted chord {index} ('{original}' → '{adjusted}') to {Quality} to fit C {Mode}."`
   - **Diagnostics Panel:**
     - Shows diagnostic events from the voicing system (Forced, Warning, Info)
     - Default view: Shows only Warning and Forced events (quiet by default)
     - With `includeInfoDiagnostics`: Shows Info events (useful Info only, excludes trace spam)
     - With `includeTraceDiagnosticsInPanel`: Includes TRACE-related Info events in panel
     - Line cap: Limited to `maxDiagnosticsLinesInPanel` lines (default: 20) with "... (X more lines hidden)" message
     - Auto-scrolls to bottom when new diagnostics are added
     - Filtered by severity and trace flags to reduce noise
     - Summary header shows event counts: `Diagnostics Summary (regions=X, events=Y | Forced=A Warn=B Info=C)`

5. **Visual Chord Representation**

   - Displays chord columns in a horizontal scrollable grid
   - Each column shows:
     - Chord symbol (e.g., "C", "Am", "Gdim", "Cmaj7", "Dm7", "Bm7b5", "Cmaj7/E", "G7(b9)", "C9", "C(add9)") - reflects adjusted quality, 7th extensions, slash chord notation for inversions, and detected 9th tensions (b9, 9, #9)
     - 3-4 stacked note names (depending on chord type) with background boxes
       - Triads: 3 notes (Root, Third, Fifth)
       - 7th chords: 4 notes (Root, Third, Fifth, Seventh)
       - Note names are sorted highest to lowest (top to bottom)
       - **Canonical chord spelling:** Triad tones (root, 3rd, 5th) use lookup-table-based canonical spelling via `TheorySpelling`:
         - Ensures musically correct enharmonic spellings (e.g., bVII shows Bb-D-F, not A#-D-F)
         - Supports major, minor, diminished, and augmented triads
         - Non-triad tones (7ths, extensions) use key-aware spelling as fallback
         - Accounts for enharmonic disambiguation via `RootSemitoneOffset` (e.g., Gb major vs F# major)
     - Roman numeral (e.g., "I", "vi", "viidim", "I7", "ii7", "viidim7", "nvi", "IVmaj7/3rd") - shows key-aware display (may show 'n' for naturalized chords)
     - Diatonic status indicator: "non-diatonic" tag for chords that don't match the mode
     - Analysis info: Shows function tags like "sec. to IV", "borrowed ∥ major", "Neapolitan" for non-diatonic chords
   - **Diatonic Status Visualization:**
     - Diatonic chords: Normal background color (configurable)
     - Non-diatonic chords: Different background color (configurable, default: red) with "non-diatonic" tag
     - Status is determined by analyzing the original user input (before any auto-correction)
     - Visual feedback helps users identify borrowed/altered chords
   - Columns appear left-to-right in progression order
   - Real-time rendering when progression is parsed
   - Chord columns dynamically adjust to show 3 or 4 notes based on chord type
   - **Progressive Reveal During Playback:**
     - Chord columns start in Hidden state when playback begins
     - As each region starts playing, its chord column becomes Visible
     - The currently playing region's column is Highlighted
     - Previously revealed columns remain Visible
     - This mirrors the voicing viewer's one-by-one reveal behavior
   - **Visual State System (Hidden / Visible / Highlighted):**
     - Three distinct visual states for chord columns during playback
     - **Hidden:** Not yet reached in playback (configurable alpha, typically 0)
     - **Visible:** Already revealed/played (configurable alpha and color tint)
     - **Highlighted:** Currently playing region (configurable alpha and color tint)
     - State-based tinting applies to ALL visuals inside each column:
       - Background Image
       - Note tile Images (small squares behind each note)
       - All TMP_Text components (note labels, chord name, roman, analysis/status)
     - Uses same tint values for all elements (simplified v1 approach)
     - CanvasGroup alpha control preserves layout spacing (doesn't collapse width)
     - Inspector-tunable styling parameters for all states

6. **Debug Logging and Diagnostics**

   - Comprehensive debug logs for troubleshooting
   - Can be enabled/disabled via Inspector
   - **Trace Logging Control:**
     - `enableUnityTraceLogs` (default: false) - Controls all `[TRACE]` Debug.Log output to Unity console
     - When disabled: No TRACE logs appear in console (quiet by default)
     - When enabled: Full TRACE logging including snapshots, candidate generation, and voice selection
     - Trace functions check `DiagnosticsCollector.EnableTrace` flag (set automatically from `enableUnityTraceLogs`)
   - **Diagnostics Panel Filtering:**
     - `includeInfoDiagnostics` (default: false) - Controls whether Info events appear in diagnostics panel
     - `includeTraceDiagnosticsInPanel` (default: false) - Controls whether TRACE-related Info events appear in panel
     - `maxDiagnosticsLinesInPanel` (default: 20) - Hard cap on displayed lines with overflow message
     - Default view: Shows only Warning and Forced events (quiet, focused on issues)
     - With Info enabled: Shows useful Info events but excludes TRACE spam unless explicitly enabled
     - Auto-scrolls to bottom when new diagnostics are added
   - **Tendency Debug Logging:** Separate toggle via `Tools → Chord Lab → Toggle Tendency Debug Logging`
     - When enabled, provides detailed logging of voice-leading rules and 7th resolution enforcement
     - See "Editor-Only Voicing Debug Tools" section for full details
   - **Chord Symbol Debug Logging:** When `enableDebugLogs` is true, logs chord symbol computation details:
     - Key, degree, Roman numeral, root pitch class, root name, detected tensions, and final symbol
     - Helps diagnose root name computation and tension detection issues
   - **Timeline Debug Logging:** When `enableDebugLogs` is true, logs timeline and playback timing:
     - Duration suffix parsing: `[ChordLab] Parsed token 'I:2' -> roman='I', quarters=2, durationTicks=8`
     - Region construction: `[ChordLab Region] Index=0, Roman='I:2', startTick=0, durationTicks=8 (quarters=2), rootPc=0, ticksPerQuarter=4`
     - Playback timing: `[ChordLab] Playback i=0 label=I:2 durationTicks=8 quarters=2.00 holdSeconds=2.00`
   - **Region Dump Tool:** `Tools → Chord Lab → Debug → Dump Current Regions`
     - Displays complete timeline information for all regions
     - Shows startTick, durationTicks, beats (start/duration), root pitch class, and melody notes
     - Useful for verifying duration suffix parsing and cumulative startTick calculation
   - Standard logs include:
     - Button click events
     - Mode selection
     - Numeral parsing (including duration suffixes)
     - Chord building
     - MIDI note playback
     - Timing information (region-based hold durations)
     - Chord grid rendering

7. **Editor-Only Voicing Debug Tools**

   - **Menu Item:** `Tools → Chord Lab → Log First Chord Voicing`
     - Voices the first chord in the progression using block voicing (3-4 voices)
     - Uses resolution-aware 7th placement when next chord is known
     - Logs MIDI numbers and note names for each voice (bass, tenor, alto, soprano)
   - **Menu Item:** `Tools → Chord Lab → Log Progression Voicing`
     - Voices the entire progression with advanced voice-leading rules
     - Applies hard constraints for 7th resolution (all voices when no melody)
     - Keeps common tones in same voices when possible
     - Moves voices to nearest chord tone otherwise
     - Respects chord inversions (bass uses 3rd/5th/7th for inversions)
     - Enforces correct 7th resolutions with post-selection override
     - Logs complete voicing analysis for each chord
   - **Menu Item:** `Tools → Chord Lab → Toggle Tendency Debug Logging`
     - Enables/disables detailed debug logging for voice-leading rules
     - When enabled, logs:
       - Rule A: Hard 7th resolution enforcement (HARD GOOD/VETO messages)
       - Rule A: Soft 7th resolution preferences (Normal/AVOID messages)
       - Rule A: Melody doubling special cases
       - Rule B: Global leading tone preferences
       - Rule C: Local leading tone preferences
       - Post-selection enforcement forcing 7th resolutions
       - Voice locking for protected 7th resolutions
       - First-chord resolution-aware 7th placement
       - Common-tone 3rd→7th bonuses
       - Leading-tone softening when 7th coverage is needed
       - Bass selection decisions and candidate evaluation
       - Complete SATB voicing before and after chord tone coverage fixes
       - Chord tone coverage enforcement (7th prioritization)
   - **Menu Item:** `Tools → Chord Lab → Log Naive Harmonization Voice Movements`
     - Analyzes voice movement between consecutive chords in naive harmonization
     - Logs per-voice intervals (semitone difference, direction) for each step transition
     - Helps debug voice-leading issues and large leaps
   - **Menu Item:** `Tools → Chord Lab → Export Current Voiced Harmonization To JSON`
     - Exports currently voiced harmonization as structured JSON
     - Requires running naive harmonization or "Play Voiced" first
     - Logs JSON to console and copies to clipboard
     - Intended for LLM analysis and external tools
   - **Menu Item:** `Tools → Chord Lab → Run Regression Suite`
     - Executes in-app regression test harness (no Unity Test Runner, no asmdefs)
     - All regression output gated behind `enableRegressionHarness` flag in Inspector
     - Validates correctness invariants (property-based, not exact voicing checks)
     - Reports pass/fail counts and detailed diagnostics for failed cases
     - See "Regression Test Harness" section below for details
   - **Menu Item:** `Tools → Chord Lab → Debug → Print Flat Chord Symbols`
     - Tests chord symbol formatting for borrowed flat chords (bVII, bVII7, bIII)
     - Verifies that flat-root chords display correctly (e.g., "Bb", "Bb7", "Eb" instead of "b", "b7", "b")
     - Logs expected vs. actual symbols for debugging
   - These tools use the `TheoryVoicing` system (Phase 1 & 2) for voicing analysis
   - Editor-only: No runtime UI changes, pure debug visualization

8. **Melody Analysis System**

   - **TheoryMelody** class provides melodic event analysis
   - **Menu Item:** `Tools → Chord Lab → Log Test Melody Analysis`
     - Analyzes a test melody line (degrees: 1, 2, 3, 5, 3, 2, 1)
     - Maps each note to its scale degree and semitone offset
     - Identifies diatonic vs. non-diatonic notes
     - Logs pitch class, degree, offset, and diatonic status for each note

9. **Melody-Constrained Voicing**

   - **Menu Item:** `Tools → Chord Lab → Log Melody-Constrained Voicing`
     - Demonstrates voicing with melody constraints
     - Test progression: I IV ii V I with melody degrees: 3, 4, 4, 2, 1
     - Shows how soprano voice is locked to melody notes
     - Logs complete voicing with melody note markers
   - **Playback Toggle:** `useTestMelodyForPlayback` (Inspector-only)
     - When enabled: Playback uses a test melody and melody-constrained voicing
     - Test melody pattern configurable via `testMelodyDegrees` array (default: [3, 4, 4, 2, 1])
     - Melody pattern wraps if progression is longer than the array
     - Soprano voice is constrained to match the melody note for each chord
     - When disabled: Normal chord-only voicing (existing behavior)

10. **Harmony Candidate Generator**

    - **TheoryHarmonization** class generates chord candidates for melody notes
    - **Menu Item:** `Tools → Chord Lab → Log Harmony Candidates For Test Melody`
      - Analyzes test melody and suggests chord candidates for each note
      - Currently supports Ionian mode and diatonic notes only
      - Maps scale degrees to classic major-key chord progressions:
        - Degree 1: I, vi
        - Degree 2: ii, V
        - Degree 3: I, iii, vi
        - Degree 4: IV, ii
        - Degree 5: V, I
        - Degree 6: vi, IV
        - Degree 7: V, viidim
      - Validates that melody pitch class is actually present in each candidate chord
      - Logs chord symbols, Roman numerals, and reasoning for each candidate

11. **Naive Harmonization with Runtime UI**

    - **Runtime Button:** "N.H." button triggers naive harmonization with voiced SATB playback
    - **Note-Name Melody Input:** Text area (`testMelodyNoteNames`) for entering melodies in scientific pitch notation
      - Format: Space-separated note names with octaves (e.g., "F5 E5 D5 B4 C5")
      - Supports accidentals: `#`, `b`, `♯`, `♭`
      - Falls back to degree-based test melody if parsing fails
    - **Roman Progression Output:** Generated progression is automatically written to the progression input field for editing
    - **Synchronized Updates:** Both ChordGrid and VoicingViewer update together when harmonization completes
    - **Editor Menu:** `Tools → Chord Lab → Play Naive Harmonization For Test Melody (Voiced)` (also available)

12. **Manual Progression with SATB Voicing**

    - **Runtime Button:** "Play Voiced" button voices manual progression with current melody
    - Takes Roman numerals from progression input field
    - Voices progression with melody constraints (soprano locked to melody)
    - Updates both ChordGrid and VoicingViewer
    - **Editor Menu:** `Tools → Chord Lab → Play Manual Progression With Melody (Voiced)` (also available)

13. **JSON Export**

    - **Menu Item:** `Tools → Chord Lab → Export Current Voiced Harmonization To JSON`
    - Exports currently voiced harmonization as structured JSON
    - Captures key, mode, melody, chord progression, and SATB voicing with note names and MIDI
    - **Enhanced Chord Analysis:**
      - Triad quality (Major, Minor, Diminished, Augmented)
      - Power chord detection (root + fifth only, no third)
      - Suspension detection (sus2, sus4)
      - Third omission flag (true for power chords and suspensions)
      - Seventh chord detection and type (Dominant7, Major7, Minor7, HalfDiminished7, Diminished7)
      - All fields populated from chord recipe data with best-effort fallback to chord symbol parsing
    - JSON is logged to console and copied to clipboard (editor-only)
    - Requires running naive harmonization or "Play Voiced" first to populate voiced state
    - Uses `VoicedHarmonizationSnapshot` DTO classes for serialization
    - Intended for LLM analysis and external tools

14. **Voicing Viewer**
    - Real-time SATB voicing display during naive harmonization and manual progression playback
    - **Component:** `VoicingViewer` (MonoBehaviour) at `Assets/Scripts/UI/VoicingViewer.cs`
    - Displays accumulated sequence of voiced chords (bass-tenor-alto-soprano)
    - Each voice line shows all notes from the progression, accumulating as playback progresses
    - Example display format:
      ```
      S: E   F   G   G   E
      A: C   D   E   F   C
      T: G   A   B   C   G
      B: C   C   C   D   C
      ```
    - **Voice Order:** Uses the exact order from TheoryVoicing without sorting: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
      - Preserves the SATB voice assignment from the voicing engine
      - Display order matches audio playback exactly
      - No pitch-based sorting that could reorder voices
    - **Canonical Chord Spelling:** Uses `TheorySpelling` lookup table for chord tones (root, 3rd, 5th) to ensure musically correct enharmonic spellings (e.g., bVII shows Bb-D-F, not A#-D-F)
    - **Note Name Padding:** All note names are padded to 2-character width for visual alignment (e.g., "C " for natural notes, "C#" remains "C#")
    - **Large Leap Highlighting:** Notes that represent large voice movements (≥5 semitones by default, configurable) are highlighted in red
    - **Debug Logging:** When `TheoryVoicing.GetTendencyDebug()` is enabled, logs received MIDI notes with voice labels for debugging
    - Header shows total number of steps (e.g., "Current Voicing — 5 steps")
    - Automatically clears when new playback starts
    - Accumulated sequence remains visible after playback completes
    - **Integration:** Wired into naive harmonization playback via `PlayVoicedChordSequenceCo()` and manual progression playback via "Play Voiced" button
    - **Synchronization:** Uses the exact same `VoicesMidi` array from TheoryVoicing that audio playback uses
    - **Optional:** If `voicingViewer` field is unassigned, playback continues without errors

15. **Melody Piano Roll**
    - Interactive monophonic melody editor with onset grid model (Phase 1: Display, Phase 2: Editing, Phase 2b: Grid→Events, Phase 2c: SATB integration)
    - **Component:** `MelodyPianoRoll` (MonoBehaviour) at `Assets/Scripts/UI/MelodyPianoRoll.cs`
    - **Onset Grid Model:** The grid represents note onsets, not full-duration sounding notes
      - `midiAtStep[t]` means "A note starts here at this step" (onset) or null (gap)
      - Durations are inferred from spacing between onsets
      - Adjacent onsets (even with same pitch) → repeated short notes (repeated attacks)
      - A note followed by one or more empty steps → one note whose duration includes the gap(s)
    - **Visual Display:** Shows only onset tiles (not full-duration bars)
      - Each note is visualized as a single tile at its onset step
      - Durations are not visualized (only used in engine & text export)
      - Deleting an onset creates a visual gap at that step
      - Repeated notes appear as distinct tiles (not fused bars)
    - **Click-to-Edit:** Simple onset-only editing
      - Clicking an empty step places a new onset
      - Clicking an existing onset with the same pitch deletes it (toggle off)
      - Clicking an existing onset with a different pitch changes the pitch
      - Clicking in the middle of a sustained note (where there's no onset) adds a new onset at that step
    - **Pitch Background:** Procedurally generated horizontal rows showing black/white key distinction
      - White key pitches (C, D, E, F, G, A, B) use lighter background color
      - Black key pitches (C#, D#, F#, G#, A#) use darker background color
      - Makes half-step and chromatic movement visually obvious
    - **Note Tiles:** Single-color note tiles positioned vertically based on MIDI pitch
      - Note tiles are centered within their respective pitch rows
      - All notes use the same color (no pitch-based color distinction)
      - Tiles are aligned to pitch rows for clear visual correspondence
    - **Timeline Grouping:** Columns alternate background colors in groups (e.g., every 4 steps) for easier timeline reading
      - Even-numbered groups use one background color
      - Odd-numbered groups use another background color
      - Grouping size is configurable per column
    - **Playback Highlighting:** Currently playing step is highlighted with distinct background color
      - Highlighted step is synchronized with VoicingViewer during playback
      - Highlight is cleared when playback ends or when no melody is present
    - **Scroll Synchronization:** Piano roll scroll position can be synchronized with VoicingViewer scroll position
      - Optional feature for keeping both viewers aligned during manual scrolling
    - **Pitch Range:** Configurable MIDI range (default: 60-79, C4-G5)
      - All pitches in range are displayed as horizontal rows
      - Notes outside range are clamped to range boundaries
    - **Grid-to-Events Conversion:** `BuildEventsFromGrid()` converts onset grid to `Timeline.MelodyEvent` list
      - Scans `midiAtStep[]` for all onsets
      - For each onset, duration = distance to next onset (or `totalSteps` if last)
      - Converts step-space to tick-space using `timelineSpec.ticksPerQuarter`
      - Repeated adjacent onsets create separate 1-step events
      - Gaps contribute to the previous note's duration
    - **SATB Integration:** Piano roll melody can be used as soprano source for SATB voicing
      - `usePianoRollMelodyForVoicedPlayback` toggle (Inspector, default: true)
      - When enabled and piano roll has notes, SATB playback uses piano roll melody
      - Falls back to text field melody or test melody if piano roll is empty
      - Piano roll melody is automatically mirrored to text field with duration suffixes (e.g., "C5:2 D5:1 G5:3")
    - **Text Field Mirroring:** When SATB uses piano roll melody, the text field is automatically updated
      - Format: Space-separated note names with octaves and duration suffixes (e.g., "C5:2 D5:1 G5:3")
      - One token per onset with `:N` duration suffix in quarters
      - Uses key-aware enharmonic spelling for note names
      - Only updates when piano roll melody is actually used (not when falling back)
    - **Integration:** 
      - Renders from `Timeline.MelodyEvent` list during naive harmonization and manual progression playback
      - `RenderFromEvents()` writes only onsets into `midiAtStep[]` (not full duration)
      - Automatically clears when no melody is present
      - Updates highlight position in sync with VoicingViewer during playback
    - **Optional:** If `melodyPianoRoll` field is unassigned, playback continues without errors

## UI Structure

```
Canvas
  └── Panel_ChordLab
       └── ConsoleContainer
          ├── Label_Title          (TextMeshProUGUI: "Chord Lab")
          ├── Console_Scrollview     (ScrollRect: Horizontal scrolling container)
          │    └── Viewport
          │         └── Content    (GameObject with VerticalLayoutGroup)
          │              └── (TextMeshProUGUI: Console text output)
       └── MainContainer
          ├── Scroll_ChordGrid     (ScrollRect: Horizontal scrolling container)
          │    └── Viewport
          │         └── Content    (GameObject with HorizontalLayoutGroup)
          │              └── (ChordColumnView instances instantiated here)
          ├── MelodyPianoRoll      (GameObject with MelodyPianoRoll component - optional)
          │    ├── Scroll_MelodyPianoRoll (ScrollRect: Horizontal scrolling container)
          │    │    └── Viewport
          │    │         ├── PitchBackgroundContainer (RectTransform: Stretched to fill viewport)
          │    │         │    └── (PitchRow_* GameObjects procedurally generated here)
          │    │         └── Content (GameObject with HorizontalLayoutGroup)
          │    │              └── (MelodyPianoRollColumn instances instantiated here)
          │    └── (columnPrefab reference for column instantiation)
          ├── Dropdown_Tonic       (TMP_Dropdown: Tonic/key center selection - 12 pitch classes)
          ├── Dropdown_Mode        (TMP_Dropdown: Mode selection)
          ├── Input_Progression    (TMP_InputField: Roman numeral input)
          ├── Button_Play          (Button: Triggers playback)
          ├── Button_NaiveHarmonize (Button: Triggers naive harmonization with voiced playback - optional)
          ├── Button_PlayVoiced    (Button: Plays manual progression with SATB voicing - optional)
          ├── Text_Status          (TextMeshProUGUI: Status/error messages and diagnostics - with ScrollRect for auto-scroll)
          └── Voicing_Viewer       (GameObject with VoicingViewer component - optional)
                ├── Text_Soprano    (TextMeshProUGUI: Soprano voice line)
                ├── Text_Alto       (TextMeshProUGUI: Alto voice line)
                ├── Text_Tenor      (TextMeshProUGUI: Tenor voice line)
                └── Text_Bass       (TextMeshProUGUI: Bass voice line)
```

### ChordColumnView Prefab Structure

Each chord column is instantiated from a prefab with the following structure:

```
ChordColumnView (GameObject)
├── Canvas (nested, auto-created by Unity)
├── VerticalLayoutGroup component
├── ChordColumnView script
├── Text_ChordName (TextMeshProUGUI: Chord symbol)
├── Text_Seventh (TextMeshProUGUI: 7th if present)
├── Text_Fifth (TextMeshProUGUI: 5th)
├── Text_Third (TextMeshProUGUI: 3rd)
├── Text_Root (TextMeshProUGUI: Root note)
└── Desc_Container
    └── Text_ChordName (TextMeshProUGUI: Lead Sheet Chord Symbol)
    └── Text_Roman (TextMeshProUGUI: Roman numeral)
    └── Text_Analysis (TextMeshProUGUI: Additional explanation if non-diatonic)
```

**Note:** The nested Canvas is required for proper rendering of background images and layout spacing. Unity auto-creates this when UI elements are added.

**Note Display Logic:**

- **Triads (3 notes):** Uses `Text_Fifth`, `Text_Third`, `Text_Root` (skips `Text_Seventh`)
- **7th Chords (4 notes):** Uses all four note fields: `Text_Seventh`, `Text_Fifth`, `Text_Third`, `Text_Root`
- Fields are automatically enabled/disabled based on chord type

## Component Configuration

### ChordLabController Serialized Fields

**UI References:**

- `buttonPlay` - Button component that triggers playback (chords are voiced automatically)
- `buttonNaiveHarmonize` - Button component for naive harmonization with voiced playback (placeholder for eventual AI Harmonizaton) 
- `buttonPlayVoiced` - Button component for playing manual progression with SATB voicing using fixed melody input
- `tonicDropdown` - TMP_Dropdown for tonic/key center selection (12 pitch classes: C, C#/Db, D, Eb, E, F, F#/Gb, G, Ab, A, Bb, B)
- `modeDropdown` - TMP_Dropdown for mode selection, has been limited to Major (Ionian) and Minor (Aeolian) intentionally
- `progressionInput` - TMP_InputField for Roman numeral entry
- `chordGridContainer` - Transform of Content GameObject (Scroll_ChordGrid/Viewport/Content)
- `chordColumnPrefab` - Prefab reference for ChordColumnView instances
- `voicingViewer` - VoicingViewer component reference for SATB display

**Music References:**

- `musicDataController` - MusicDataController (optional, reserved for future)
- `synth` - FmodNoteSynth for audio playback

**Settings:**

- `rootOctave` - Root octave for all chords (default: 4) *this feature is depreciated and should be updated or removed*
- `chordDurationSeconds` - Duration of each chord (default: 1.0) 
- `gapBetweenChordsSeconds` - Pause between chords (default: 0) *this feature is depreciated and should be updated or removed*
- `velocity` - MIDI velocity 0-1 (default: 0.9)

**Voicing Parameters (Inspector-Tunable):**

- **Register & Compression (Inner Voices):**

  - `enableRegisterGravity` - Enable register gravity for inner voices (default: true)
  - `tenorRegisterCenter` - Preferred MIDI center for tenor voice (default: 55, ~G3)
  - `altoRegisterCenter` - Preferred MIDI center for alto voice (default: 60, ~C4)
  - `tenorRegisterWeight` - Weight for tenor register gravity (default: 0.2f, range: 0-2)
  - `altoRegisterWeight` - Weight for alto register gravity (default: 0.3f, range: 0-2)
  - `enableCompressionCost` - Enable compression incentive for inner voice spacing (default: true)
  - `targetAltoTenorGap` - Target gap between alto and tenor in semitones (default: 7f, perfect 5th)
  - `targetSopAltoGap` - Target gap between soprano and alto in semitones (default: 7f)
  - `compressionWeightAT` - Weight for alto-tenor compression (default: 0.5f, range: 0-2)
  - `compressionWeightSA` - Weight for soprano-alto compression (default: 0.5f, range: 0-2)

- **Voice Leading Smoothness:**
  - `enableMovementWeighting` - Enable movement cost weighting (default: true)
  - `movementWeightInnerVoices` - Weight for inner voice movement cost (default: 1.0f, range: 0-2)
    - 1.0 = current behavior, <1.0 = prioritize smoothness, >1.0 = prioritize other costs

**Playback Settings:**

- `emphasizeBassWithLowOctave` - If enabled, doubles the bass note an octave below during playback (default: true)
- `useVoicingEngine` - If enabled, playback uses voice-leading engine. If disabled, uses root-position chords (default: true)
- `useTestMelodyForPlayback` - If true, playback uses a simple one-note-per-chord test melody and melody-constrained voicing. If false, use normal chord-only voicing (default: false)
- `testMelodyDegrees` - Scale degrees for the test melody (one per chord). Values wrap if progression is longer than this array. Example: [3, 4, 4, 2, 1] means degree 3 for first chord, degree 4 for second and third, etc. (default: [3, 4, 4, 2, 1])

**Visual State Styling (Inspector-Tunable):**

- `hiddenAlpha` - Alpha value for Hidden state (0-1, default: 0.0) - Controls opacity of columns not yet reached in playback
- `visibleAlpha` - Alpha value for Visible state (0-1, default: 1.0) - Controls opacity of columns already revealed
- `highlightedAlpha` - Alpha value for Highlighted state (0-1, default: 1.0) - Controls opacity of currently playing column
- `visibleTint` - Color tint for Visible state (default: white) - Applied multiplicatively to all visuals (background, note tiles, text)
- `highlightedTint` - Color tint for Highlighted state (default: white) - Applied multiplicatively to all visuals (background, note tiles, text)
- **Note:** Hidden state uses `visibleTint` for color (alpha is controlled by `hiddenAlpha`), allowing for "pre-visible" states when `hiddenAlpha > 0`
- **Note:** Tinting applies to all child visuals automatically (no manual assignment needed):
  - Background Image
  - All child Image components (note tiles)
  - All TMP_Text components (note labels, chord name, roman, analysis, status)

**Theory Settings:**

- `autoCorrectToMode` - Toggle to enable/disable automatic quality correction (default: true) 
  - When `true`: Chords are automatically adjusted to match diatonic triads for the mode
  - When `false`: Chords play exactly as typed (non-diatonic chords sound with their specified quality)

**Melody Input (Debug):**

- `testMelodyNoteNames` - Optional text area for note-name melody input (space-separated, e.g., "F5 E5 D5 B4 C5"). Used by naive harmonization when not empty. Falls back to degree-based melody if parsing fails.

**Debug:**

- `enableDebugLogs` - Enable/disable debug logging (default: true)
- `showDiagnostics` - Toggle to show/hide diagnostics console (default: true)
- `includeInfoDiagnostics` - Toggle to include Info events in diagnostics summary (default: false)
- `enableUnityTraceLogs` - Controls whether TRACE Debug.Log lines are printed to Unity console (default: false)
- `includeTraceDiagnosticsInPanel` - Controls whether TRACE-like diagnostics are included in UI panel (default: false)
- `maxDiagnosticsLinesInPanel` - Hard cap on lines rendered in UI diagnostics panel (default: 20)
- `showDiagnostics` - Toggle to show/hide diagnostics console (default: true)
- `includeInfoDiagnostics` - Toggle to include Info events in diagnostics summary (default: false)
- `enableUnityTraceLogs` - Controls whether TRACE Debug.Log lines are printed to Unity console (default: false)
- `includeTraceDiagnosticsInPanel` - Controls whether TRACE-like diagnostics are included in UI panel (default: false)
- `maxDiagnosticsLinesInPanel` - Hard cap on lines rendered in UI diagnostics panel (default: 20)

## How It Works

### Playback Flow

1. User selects mode from dropdown
2. User enters Roman numeral progression (e.g., "I V vi IV" or "I:2 V vi IV")
3. User clicks Play button
4. `OnPlayClicked()` is called:
   - Stops any existing playback
   - Starts `PlayProgressionCo()` coroutine
5. Coroutine:
   - Validates UI references
   - Gets tonic and mode from dropdowns
   - Creates `TheoryKey` with selected tonic and mode
   - Parses input using `TryBuildChordRecipesFromRomanInput()`:
     - Splits input by whitespace into tokens
     - For each token, extracts Roman numeral and optional `:N` duration suffix
     - Validates each numeral using `TheoryChord.TryParseRomanNumeral()` (supports b/#/n accidentals and inversions)
     - Parses duration suffix (defaults to 1 quarter if missing or invalid)
     - Returns recipes and `durationsInQuarters` list
   - Builds `ChordRegion[]` using `BuildRegionsFromRomanInput()` helper:
     - Creates regions with cumulative `startTick` (each region starts after previous region's end)
     - Sets `durationTicks = quarters * ticksPerQuarter` for each region
     - Stores regions in `_lastRegions` for debug inspection
   - Conditionally adjusts chord qualities using `TheoryChord.AdjustTriadQualityToMode()` (if `autoCorrectToMode` is enabled)
   - Analyzes original recipes using `TheoryChord.AnalyzeChordProfile()` to determine diatonic status (includes 7th quality checks)
   - Builds chords from adjusted recipes using `TheoryChord.BuildChord()` (respects ChordQuality for intervals, applies inversions)
   - Renders visual chord grid using `RenderChordGrid()` with key-aware Roman numerals and status information
   - Hides all chord columns at playback start using `HideAllChordColumns()`
   - Plays each chord using `PlayChord()` helper (handles optional bass doubling)
   - Highlights current region's chord column using `HighlightChordColumn()` (reveals and highlights progressively)
   - Computes hold duration using `GetRegionHoldSeconds()`:
     - Uses region's `durationTicks` to calculate `holdSeconds = chordDurationSeconds * (durationTicks / ticksPerQuarter)`
     - Falls back to `chordDurationSeconds` if region unavailable
   - Waits for `holdSeconds + gapBetweenChordsSeconds` before next chord
   - Updates status text with warnings if any adjustments were made (only when auto-correct is enabled)

### Integration with MusicTheory System

The Chord Lab leverages the Phase 2 chord kernel:

- **TheoryKey** - Represents the key (tonic pitch class + mode)
- **TheoryChord.TryParseRomanNumeral()** - Parses input strings (supports triads, 7th chords, b/#/n accidentals, and inversions)
- **TheoryChord.GetDiatonicTriadQuality()** - Returns expected triad quality for a degree in a mode
- **TheoryChord.GetDiatonicSeventhQuality()** - Returns expected 7th chord quality for a degree in a mode
- **TheoryChord.AdjustTriadQualityToMode()** - Adjusts chord recipe quality to match diatonic expectations (triads only, preserves inversion)
- **TheoryChord.AnalyzeChordProfile()** - Analyzes a chord to determine diatonic status, function tags, and borrowing (includes 7th quality checks)
- **TheoryChord.BuildNonDiatonicInfo()** - Generates analysis strings like "sec. to IV", "borrowed ∥ major"
- **TheoryChord.RecipeToRomanNumeral(TheoryKey, ChordRecipe)** - Key-aware conversion to Roman numeral string (shows 'n' when appropriate)
- **TheoryChord.GetChordSymbol()** - Generates chord symbols with slash chord notation for inversions
- **TheoryChord.GetChordSymbolWithTensions()** - Generates chord symbols with detected 9th tensions (b9, 9, #9)
  - Automatically detects tensions from realized voicing (SATB voices + melody)
  - Formats symbols with tensions: "G7(b9)", "C9", "C(add9)", etc.
  - For 7th chords with natural 9 only: promotes "7" → "9", "maj7" → "maj9", "m7" → "m9"
  - For 7th chords with altered 9ths: appends "(b9)", "(#9)", etc.
  - For triads: uses "add" syntax: "C(add9)", "Cm(addb9)", etc.
- **TheoryChord.TryParseRequestedExtensions()** - Parses extension tokens from chord symbols and Roman numerals
  - Supports: `9`, `b9`, `add9`, `11`, `#11`, `add11`, `sus4`, `7sus4`
  - Returns `RequestedExtensions` struct with boolean flags for each extension type
  - Extensions are preserved in `ChordRecipe` and passed to voicing engine
- **TheoryChord.BuildChord()** - Builds individual chords from recipes (3 notes for triads, 4 for 7ths, applies inversions)
  - Root note comes from the mode (with optional b/#/n offset)
  - Third and fifth intervals are calculated from `ChordQuality` (Major: 4,7; Minor: 3,7; Dim: 3,6; Aug: 4,8)
  - 7th interval is determined by explicit `SeventhQuality` on the recipe
  - Inversions rotate lowest note(s) up an octave
- **TheoryVoicing.GetChordTonePitchClasses()** - Returns the canonical chord tone pitch classes for a chord
  - For dominant 9 chords (V9, etc.), automatically includes the 7th (b7) even if `Extension != Seventh`
  - This ensures V9 is treated as "dominant 7th + 9" rather than "triad + 9"
- **TheoryScale** - Provides scale degree → pitch class mapping
- **TheoryPitch.GetPitchNameFromMidi()** - Converts MIDI notes to note names with key-aware enharmonic spelling
- **TheoryPitch.GetAccidentalPreference()** - Determines sharp/flat preference based on key (mode-aware: minor keys prefer flats)
- **TheoryPitch.GetNoteNameForDegreeWithOffset()** - Key-aware note name spelling for chord roots
- **TheoryChord.GetSpelledChordTones()** - Key-aware enharmonic spelling for chord tones:
  - Uses the current key's accidental preference for diatonic chords
  - Uses the parallel minor key's preference (flats) for borrowed minor chords
  - Uses the parallel major key's preference for borrowed major chords
  - Ensures musically sensible spellings (e.g., Cm in G Ionian = C–Eb–G, not C–D#–G)
- **TheoryVoicing.VoiceFirstChord()** - Block voicing for a single chord (3-4 voices, close position)
- **TheoryVoicing.VoiceLeadProgression()** - Progression voicing with basic voice-leading (keeps common tones, moves to nearest chord tone)
- **TheoryVoicing.VoiceLeadProgressionWithMelody()** - Progression voicing with melody constraints (soprano locked to melody notes)
- **TheoryVoicing.VoiceLeadRegions()** - Timeline-aware voicing adapter
  - Accepts `TimelineSpec` and `IReadOnlyList<ChordRegion>`
  - Extracts `ChordEvent[]` from regions and routes to appropriate voicing method
  - Currently adapter-only (does not interpret ticks for timing yet)
  - Threads `TimelineSpec` through for future tempo-based timing support
- **TheoryMelody.AnalyzeEvent()** - Analyzes a single melodic event, mapping MIDI to scale degree and semitone offset
- **TheoryMelody.AnalyzeMelodyLine()** - Analyzes an entire melody line, returning analysis for each note
- **TheoryHarmonization.GetChordCandidatesForMelodyNote()** - Generates chord candidates for a melody note (Ionian mode, supports chromatic candidates with accidental hints)
- **TheorySpelling.GetTriadSpelling()** - Returns canonical note names (root, 3rd, 5th) for triads using lookup table
  - Supports major, minor, diminished, and augmented triads
  - Ensures musically correct enharmonic spellings
  - Handles enharmonic disambiguation via `RootSemitoneOffset` parameter
  - Used by both ChordGrid and VoicingViewer for consistent display
- **ChordTensionDetector.DetectNinthTensions()** - Detects 9th tensions (b9, 9, #9) from realized voicing
  - Analyzes all MIDI notes (SATB voices + melody) for each chord
  - Identifies tensions by interval above root (mod 12)
  - Excludes core chord tones (root, 3rd, 5th, 7th) from detection
  - Returns unique list of detected tensions
- **ChordTensionHelper.IsAugmentedFifth()** - Identifies augmented 5th (#5) chord tones
  - Used by voice-leading tendency system for augmented 5th resolution

## Example Usage

### Example 1: Standard Progression

1. Open `LLM_Chat_Terminal` scene
2. Select tonic: "C", mode: "Ionian"
3. Enter progression: `I V vi IV`
4. Click Play
5. Hears: C major, G major, A minor, F major (all in C Ionian context)
   - Each chord held for 1.0 second (default duration)

### Example 1b: Progression with Duration Suffixes

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `I:2 V vi IV`
3. Click Play
4. System:
   - Parses `I:2` as 2 quarters, `V`, `vi`, `IV` as 1 quarter each
   - First chord (I) is held for 2.0 seconds (2× the base duration)
   - Remaining chords held for 1.0 second each
   - Timeline: startTicks = 0, 8, 12, 16; durations = 8, 4, 4, 4 ticks
5. Hears: C major (held longer), G major, A minor, F major

### Example 2: Quality Adjustment (Auto-Correct Enabled)

1. Select tonic: "C", mode: "Ionian"
2. Ensure `autoCorrectToMode` is `true` in Inspector
3. Enter progression: `I vii I` (note: "vii" without "dim")
4. Click Play
5. System automatically:
   - Analyzes "vii" as non-diatonic (visual indicator shows non-diatonic color/tag)
   - Adjusts "vii" to Diminished quality (to match Ionian mode)
   - Displays "vii" as Roman numeral (original input preserved)
   - Displays "Bdim" as chord symbol (corrected quality)
   - Shows warning: `"Adjusted chord 2 ('vii' → 'viidim') to Diminished to fit C Ionian."`
6. Hears: C major, B diminished, C major

### Example 2b: Non-Diatonic Chords (Auto-Correct Disabled)

1. Select tonic: "C", mode: "Ionian"
2. Set `autoCorrectToMode` to `false` in Inspector
3. Enter progression: `I II V I`
4. Click Play
5. System:
   - Analyzes "II" as non-diatonic (visual indicator shows non-diatonic color/tag)
   - No quality adjustment applied
   - Displays "II" as Roman numeral (original input)
   - Displays "D" as chord symbol (major II chord)
   - No adjustment warning in status text
6. Hears: C major, D major, G major, C major (II chord sounds as major, not minor)

### Example 3: Seventh Chords

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `ii7 V7 I7`
3. Click Play
4. System:
   - Parses 7th chords: `ii7` (Dm7), `V7` (G7), `I7` (C7 - non-diatonic, secondary to IV)
   - Displays 4 stacked notes per chord
   - Shows chord symbols: "Dm7", "G7", "C7"
   - Shows Roman numerals: "ii7", "V7", "I7"
   - I7 column shows as non-diatonic (red) with "sec. to IV" tag
5. Hears: D minor 7th, G dominant 7th, C dominant 7th

### Example 4: Inversions

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `I IVmaj7/3rd I`
3. Click Play
4. System:
   - Parses: `I` (root), `IVmaj7/3rd` (first inversion), `I` (root)
   - Shows chord symbols: "C", "Fmaj7/A", "C"
   - Shows Roman numerals: "I", "IVmaj7/3rd", "I"
   - IVmaj7/3rd plays with A in the bass
5. Hears: C major, F major 7th (first inversion), C major

### Example 5: Natural Accidentals (Parallel Ionian)

1. Select tonic: "C", mode: "Aeolian"
2. Enter progression: `i nvi iv i`
3. Click Play
4. System:
   - Parses: `i` (Cm, diatonic), `nvi` (Am, parallel Ionian version of VI), `iv` (Fm, diatonic), `i` (Cm)
   - Shows chord symbols: "Cm", "Am", "Fm", "Cm"
   - Shows Roman numerals: "i", "nvi", "iv", "i"
   - nvi column shows as non-diatonic (red) with "borrowed ∥ major" tag
5. Hears: C minor, A minor (borrowed from parallel major), F minor, C minor

### Example 6: Naive Harmonization with Runtime UI

1. Open `LLM_Chat_Terminal` scene
2. Select tonic: "C", mode: "Ionian"
3. Optionally enter note-name melody in `testMelodyNoteNames` field (e.g., "F5 E5 D5 B4 C5")
4. Click "N.H." button (or use menu: `Tools → Chord Lab → Play Naive Harmonization For Test Melody (Voiced)`)
5. System:
   - Builds melody (from note names if provided, otherwise from degree-based test melody)
   - Generates chord candidates for each melody note
   - Selects chords using naive heuristics
   - Voice-leads the progression with melody constraints
   - Updates ChordGrid with harmonized progression
   - Updates VoicingViewer with SATB voicing sequence
   - Writes Roman numeral progression to input field (e.g., "I IV vi V I")
6. ChordGrid and VoicingViewer remain synchronized throughout
7. Large leaps in voicing are highlighted in red

### Example 7: Manual Progression with SATB Voicing

1. Open `LLM_Chat_Terminal` scene
2. Select tonic: "C", mode: "Ionian"
3. Enter progression: `I vi IV V`
4. Optionally enter note-name melody in `testMelodyNoteNames` field
5. Click "Play Voiced" button (or use menu: `Tools → Chord Lab → Play Manual Progression With Melody (Voiced)`)
6. System:
   - Parses Roman numeral progression
   - Builds melody (from note names if provided, otherwise from degree-based test melody)
   - Voice-leads progression with melody constraints (soprano locked to melody)
   - Updates ChordGrid with progression
   - Updates VoicingViewer with SATB voicing during playback
7. Both viewers show the same progression in different formats

### Example 8: JSON Export

1. Run naive harmonization or "Play Voiced" to generate a voiced harmonization
2. Use menu: `Tools → Chord Lab → Export Current Voiced Harmonization To JSON`
3. System:
   - Builds JSON from current voiced state (melody, chords, SATB voicing)
   - Logs JSON to console
   - Copies JSON to clipboard (editor-only)
4. JSON includes:
   - Key and mode
   - Melody notes with MIDI, note names (e.g., "G5"), scale degree, chromatic offset, degree label, and chord tone flag
   - Chord progression with detailed analysis:
     - Roman numerals, chord symbols, qualities
     - Root degree and chromatic offset information
     - Diatonic status and chromatic function
     - Triad quality (Major, Minor, Diminished, Augmented)
     - Power chord flag (IsPowerChord, OmitsThird)
     - Suspension type (sus2, sus4, or empty)
     - Seventh chord presence (HasSeventh) and type (Dominant7, Major7, Minor7, HalfDiminished7, Diminished7)
   - SATB voicing with MIDI and note names for each voice
   - Timing information (TimeBeats)

Example JSON structure for a chord:

```json
{
  "Roman": "V7",
  "ChordSymbol": "G7",
  "Quality": "Major",
  "TriadQuality": "Major",
  "IsPowerChord": false,
  "Suspension": "",
  "OmitsThird": false,
  "HasSeventh": true,
  "SeventhType": "Dominant7",
  "IsDiatonic": true,
  "RootDegree": 5,
  "RootChromaticOffset": 0,
  "RootDegreeLabel": "V"
}
```

## Known Limitations

1. **No LLM Integration**

   - Currently manual input only
   - LLM-driven progression suggestions will come later

2. **Simple Playback**

   - Block chords only (all notes played simultaneously)
   - ✅ Runtime voicing control available via "N.H." and "Play Voiced" buttons
   - ✅ SATB voicing display via VoicingViewer component
   - No rhythmic variations

3. **Roman Numeral Subset**

   - ✅ Basic triads and 7th chords supported
   - ✅ 7th chord syntax: `I7`, `ii7`, `V7`, `viidim7`, `Iaug7`, `Imaj7`, `iiø7` (half-diminished)
   - ✅ Leading accidentals: `b` (flat), `#` (sharp), `n`/`N` (natural - parallel Ionian)
   - ✅ Inversion syntax: `/3rd` or `/3` (first), `/5th` or `/5` (second), `/7th` or `/7` (third)
   - ⏳ Secondary dominants (detected in analysis but not parsed from syntax like "V/V")
   - ⏳ Suspensions and other alterations

4. **Quality Adjustment Behavior**
   - Controlled by `autoCorrectToMode` toggle (default: enabled)
   - When enabled: Only adjusts triad qualities (7ths/extensions pass through unchanged)
   - When enabled: Adjusts to match expected diatonic quality for the mode
   - When disabled: Chords play exactly as typed, allowing non-diatonic/borrowed chords
   - Augmented chords are never expected in standard modes (but can be explicitly requested)
   - Users can match expected quality to avoid adjustments (e.g., use "viidim" in Ionian)
   - Visual analysis always reflects original input (non-diatonic chords are highlighted regardless of auto-correct setting)

## Dependencies

- `Sonoria.MusicTheory` namespace:

  - `TheoryKey`
  - `TheoryChord`
  - `TheoryScale`
  - `TheoryVoicing`
  - `TheoryMelody`
  - `TheoryHarmonization`
  - `TheorySpelling`
  - `TheoryPitch`
  - `ScaleMode` enum
  - `VoicedHarmonizationSnapshot` (DTO classes for JSON export with enhanced chord and melody analysis)

- `Sonoria.MusicTheory.Timeline` namespace:

  - `TimelineSpec` - Timeline configuration (ticksPerQuarter, tempo, time signature)
  - `ChordRegion` - Timeline region for a chord (startTick, durationTicks, chordEvent, debugLabel)
  - `MelodyEvent` - Timeline region for a melody note (startTick, durationTicks, midi) - placeholder for future use

- Existing Unity systems:
  - `FmodNoteSynth` for audio playback
  - `MusicDataController` (referenced but not actively used)

## Regression Test Harness

The Chord Lab includes an in-app regression testing framework to ensure correctness of chord voicings, particularly for identity tones and voice-leading rules.

### Configuration

- **Global Flag:** `enableRegressionHarness` (Inspector, default: `false`)
  - When `OFF`: No regression-related output is produced (zero console pollution)
  - When `ON`: Regression results are displayed in the dev console with detailed diagnostics
  - All regression output is gated behind this single flag

- **Menu Item:** `Tools → Chord Lab → Run Regression Suite`
  - Editor-only menu item to execute all regression cases
  - Only visible/usable when regression flag is enabled

### Test Framework Components

- **Data Model:** `RegressionCase` with fields for `name`, `keyTonic`, `mode`, `progressionInput`, `melodyInput` (optional), and `RegressionChecks` (bitmask flags)
- **Runner:** `RegressionRunner` with `RunCase(string caseName)` and `RunAllCases()` methods
  - Executes the same UI pipeline: parse → build chord regions → harmonize → produce realized voicings
  - Results collected into structured `RegressionReport` (case count, pass count, fail count, per-failure entries)
  - Report displayed only when `enableRegressionHarness` is ON

### Test Bundles

The regression suite includes three bundles of test cases, each validating a specific correctness invariant:

1. **Chordal 7th Resolution (`ChordalSeventhResolvesDownIfAvailable`)**
   - Validates that chordal 7ths resolve down by step (1-2 semitones) when a legal target exists
   - 6 test cases covering major/minor progressions, chromatic targets, and altered dominants:
     - `C7_to_Fm_seventh_must_resolve` (C Major, C7 Fm)
     - `G7_to_C_seventh_must_resolve` (C Major, G7 C)
     - `G7_to_Cm_seventh_must_resolve` (C Minor, G7 Cm)
     - `E7_to_Am_seventh_must_resolve` (A Minor, E7 Am)
     - `A7_to_Bb_seventh_must_resolve` (D Minor, A7 Bb)
     - `B7b9_to_Emaj7_seventh_must_resolve` (E Major, B7b9 Emaj7)

2. **Required Chord Tones (`RequiredChordTonesPresent`)**
   - Validates that all required chord tones (root, 3rd, 7th if present, altered 5th if present) are present in final voicings
   - 6 test cases covering various chord types and progressions:
     - `ReqTones_C_to_Am` (C Major, C Am)
     - `ReqTones_C_to_Ab` (C Major, C Ab)
     - `ReqTones_Bdim_to_C` (C Major, Bdim C)
     - `ReqTones_G7_to_C` (C Major, G7 C)
     - `ReqTones_B7b9_to_Emaj7` (E Major, B7b9 Emaj7)
     - `ReqTones_Fsm7b5_B7_Em` (E Minor, F#m7b5 B7 Em)

3. **Diminished Triad Identity Tones (`DiminishedTriadIdentityTonesPresent`)**
   - Validates that diminished triads always contain all three identity tones: root, minor 3rd, and diminished 5th
   - 3 test cases covering single chords and progressions:
     - `DimTriad_Fdim` (single chord)
     - `DimTriad_Bdim_Ddim_Fdim` (progression of 3 diminished triads)
     - `DimTriad_8chord_chain` (full 8-chord minor-third chain: Bdim Ddim Fdim Abdim Bdim Ddim Fdim Abdim)

4. **Augmented 5th Resolution (`AugmentedFifthResolvesUpIfAvailable`)**
   - Validates that augmented 5ths (#5) resolve up by semitone when a legal target exists in the next chord
   - 1 test case with melody constraints:
     - `Aug5_Caug_to_F_withMelody_mustResolve` (C Major, C Caug F with soprano melody E4 E4 C4)
     - Ensures the same voice that contains the augmented 5th in the source chord resolves to the target pitch class in the destination chord
     - Validates that both Tenor and Alto can share the same resolution note (unison doubling) when needed

### Recent Bug Fixes

**Diminished Triad Fix:**
- **Issue:** Diminished triads were missing their required diminished 5th (b5) in final voicings, resulting in tripled roots and missing identity tones.
- **Root Cause:** In `FixChordToneCoverage`, when evaluating candidates to replace duplicated required tones (e.g., duplicated roots) with missing required tones (e.g., b5), the `betterChoice` logic failed to handle the initial selection case. When `bestVoiceIndex < 0` (no candidate selected yet), valid candidates that passed all spacing and ordering checks were incorrectly rejected due to protection status evaluation, preventing the first valid candidate from being selected.
- **Fix:** Updated `betterChoice` logic to explicitly handle the initial selection case: when `bestVoiceIndex < 0`, any valid candidate that passes all checks is automatically selected (`betterChoice = true`), ensuring that the first valid candidate is chosen regardless of protection status. This allows duplicated roots to be correctly replaced with missing diminished 5ths, ensuring all identity tones are present in diminished triads.

**Augmented 5th Resolution Fix:**
- **Issue:** Augmented 5ths (#5) in augmented chords (e.g., G# in Caug) were not resolving to the target pitch class (A) in the next chord when using SATB voicing with melody constraints, despite the resolution being musically correct and available in the candidate pool.
- **Root Cause:** The voicing system prevented inner voices (Tenor/Alto) from sharing the same MIDI note (unison doubling), even when it was the only valid resolution option. Additionally, voice crossing constraints were being "fixed" by swapping lanes rather than preventing crossings during candidate selection.
- **Fix:** 
  - Implemented hard constraint enforcement: When an augmented 5th is detected in a voice and the next chord supports the resolution target, candidates are filtered to only include the target pitch class if available.
  - Allowed unison doubling for inner voices: Tenor and Alto can now share the same MIDI note when it's the only valid option that satisfies both the resolution requirement and the no-crossing constraint.
  - Enforced strict no-crossing during candidate selection: Alto candidates are filtered to be >= Tenor (allowing unison), preventing illegal crossings from being generated in the first place.
  - Preserved lane identity: Fixed lane assignment to prevent post-selection reordering that could swap Tenor/Alto after correct resolution was chosen.

## Future Enhancements

Potential improvements for future phases:

1. **LLM Integration**

   - Generate progressions from text prompts
   - Suggest progressions based on mode

2. **Advanced Playback**

   - ✅ Basic voicing system (TheoryVoicing) - Editor debug tools available
   - ✅ Melody-constrained voicing - Soprano voice can be locked to melody notes
   - ✅ Hard 7th resolution constraints - All voices (including soprano when no melody) must resolve 7ths down by step when possible
   - ✅ Resolution-aware first-chord placement - First chord's 7th is placed to allow correct resolution in the next chord
   - ✅ Post-selection enforcement - Forces correct 7th resolutions even when cost function prefers other candidates
   - ✅ Voice locking - Protects correct 7th resolutions from being undone by fix-up logic
   - ✅ 7th chord tone coverage prioritization - 7th chords prioritize 7th over 5th (Root, 3rd, 7th required; 5th optional)
   - ✅ Common-tone 3rd→7th preference - Encourages smooth voice-leading in circle-of-fifths progressions
   - ✅ Leading-tone softening - Leading-tone rules don't override 7th coverage requirements
   - ✅ Test melody playback toggle - Configurable melody pattern for testing
   - ✅ Runtime voicing control - "N.H." and "Play Voiced" buttons with SATB display
   - ✅ Note-name melody input - Text area for entering melodies in scientific pitch notation
   - ✅ Synchronized ChordGrid and VoicingViewer - Both update together
   - ✅ Canonical chord spelling - Lookup-table-based enharmonic spellings
   - ✅ Large leap highlighting - Visual feedback for voice-leading issues
   - ✅ Augmented 5th resolution tendency - Augmented 5ths prefer to resolve up by semitone
   - ✅ Play voicing continuity adjustment - Tames awkward leaps and extreme registers in simple Play voicing
   - ✅ Timeline-based playback timing - Chord hold duration reflects `:N` duration suffixes
   - ✅ Duration suffix support - `I:2` syntax for extended chord durations
   - ⏳ User-defined melody input interface (currently Inspector-only text area)
   - ⏳ Tempo-based playback scaling (currently uses fixed `chordDurationSeconds` as base)
   - Arpeggiated chords
   - Strumming patterns
   - Rhythmic variations

3. **Extended Roman Numeral Support** (Partially Implemented)

   - ✅ Basic 7th chords (`I7`, `ii7`, `V7`, `viidim7`, `Iaug7`)
   - ✅ Extended 7th syntax (`maj7`, `ø7`/`m7b5` for half-diminished)
   - ✅ Leading accidentals (`b`, `#`, `n`/`N` for parallel Ionian)
   - ✅ Inversion syntax (`/3rd`, `/5th`, `/7th` or `/3`, `/5`, `/7`)
   - ✅ Slash chord notation in chord symbols (e.g., `Cmaj7/E` for first inversion)
   - ✅ 9th tension detection and display (b9, 9, #9) in chord symbols
     - Automatically detected from realized voicing (SATB + melody)
     - Displayed in chord symbols: "G7(b9)", "C9", "C(add9)", etc.
   - ✅ Explicit extension parsing from input (9, b9, add9, 11, #11, add11, sus4, 7sus4)
     - Parsed from both Roman numerals (e.g., `V7b9`, `Imaj9`, `ii7sus4`) and absolute chord symbols (e.g., `G7b9`, `Cmaj9`)
     - Requested tensions (9, b9, #11) are enforced in voicing (must appear in realized voicing)
     - Add-tones (add9, add11) are optional color tones (preferred but not required)
     - Sus4/7sus4 are suspension modifiers (affect chord structure)
     - Extensions are preserved through parsing → recipe → voicing pipeline
   - ⏳ Secondary dominants (detected in analysis but not parsed from syntax like "V/V")
   - ⏳ Other alterations

4. **Visual Feedback** (Partially Implemented)

   - ✅ Visual chord columns with chord symbols and note names
   - ✅ Key-aware Roman numeral display (shows 'n' for naturalized chords when appropriate)
   - ✅ Diatonic status visualization (color coding and tags)
   - ✅ Function tag display ("sec. to IV", "borrowed ∥ major", "Neapolitan", etc.)
   - ✅ Quality adjustment warnings (when auto-correct is enabled)
   - ✅ Support for 3-4 note chords (triads and 7ths)
   - ✅ 7th-aware chord symbols (maj7, m7, m7b5, aug7)
   - ✅ Slash chord notation for inversions (e.g., "Cmaj7/E")
   - ✅ Key-aware root note spelling (proper enharmonic spelling based on key context)
   - ✅ 9th tension display in chord symbols (b9, 9, #9)
   - ✅ Flat-root chord symbol fix (borrowed chords like Bb, Eb display correctly)
   - ⏳ Highlight keyboard keys for each chord
   - ⏳ Interactive chord selection
   - ⏳ Display MIDI note information

5. **Preset Progressions**

   - Common progressions library
   - Save/load custom progressions
   - Share progressions

6. **Melody and Harmonization Features**
   - ✅ Melody analysis system (TheoryMelody) - Analyzes melodic events in key context
   - ✅ Melody-constrained voicing - Soprano voice locked to melody notes
   - ✅ Harmony candidate generator (TheoryHarmonization) - Suggests chords for melody notes
   - ✅ Naive harmonization snapshot export (JSON) - Exports a single harmonized test melody as a structured JSON snapshot:
     - Menu item: `Tools → Chord Lab → Log Naive Harmonization Snapshot (JSON)`
     - Uses `HarmonizationSnapshot` DTOs to capture key, melody notes, candidate chords, and chosen chords (with reasons)
     - Intended for LLM experiments and external analysis tools; does not affect runtime playback
   - ✅ Voiced harmonization JSON export - Exports currently voiced harmonization (melody, chords, SATB voicing) as JSON:
     - Menu item: `Tools → Chord Lab → Export Current Voiced Harmonization To JSON`
     - Uses `VoicedHarmonizationSnapshot` DTOs to capture complete voiced state
     - Includes detailed chord analysis: triad quality, power chords, suspensions, seventh types
     - Includes melody analysis: scale degree, chromatic offset, chord tone detection
     - JSON is logged to console and copied to clipboard (editor-only)
     - Requires running naive harmonization or "Play Voiced" first to populate voiced state
   - ✅ Runtime naive harmonization button - "N.H." button triggers naive harmonization with voiced playback
   - ✅ Note-name melody input - Text area for entering melodies in scientific pitch notation (e.g., "F5 E5 D5 B4 C5")
   - ✅ Manual progression with SATB voicing - "Play Voiced" button voices manual progression with current melody
   - ✅ Synchronized ChordGrid and VoicingViewer - Both viewers update together in response to harmonization or manual input
   - ✅ Roman progression written to input field - Naive harmonization writes resulting progression back to input for editing
   - ✅ Interactive melody piano roll editor - Click-to-edit monophonic melody input with onset grid model
     - Onset grid: `midiAtStep[t]` marks note starts (not full duration)
     - Durations inferred from spacing between onsets
     - Visual display shows only onset tiles (not full-duration bars)
     - Piano roll melody can be used as soprano source for SATB voicing
     - Piano roll melody automatically mirrored to text field with duration suffixes (e.g., "C5:2 D5:1 G5:3")
   - ⏳ Extended mode support for harmonization (currently Ionian only)
   - ⏳ Chromatic note support for harmonization (currently supports chromatic candidates with accidental hints)
   - ⏳ Real-time harmonization suggestions

## Troubleshooting

### Button Not Working

- Ensure `Button_Play` is assigned to `buttonPlay` field in Inspector
- Check Console for debug messages
- Verify button's `onClick` event is wired (automatically done in `Awake()`)

### No Audio Playback

- Verify `synth` reference is assigned (MusicSystem/FmodNoteSynth)
- Check that FMOD is initialized
- Ensure audio samples are loaded
- Check Console for errors

### Parsing Errors

- Verify Roman numeral format (uppercase = Major, lowercase = Minor)
- Check for typos or unsupported suffixes
- See status text for specific error messages
- Enable debug logs for detailed parsing information

### Chords Sound Wrong

- Verify mode selection matches intended scale
- Check that numerals are valid for the selected mode
- Confirm root octave is appropriate for your audio setup
- Check if quality adjustments are occurring (see status messages)
- Verify that chord qualities match expected diatonic triads for the mode

### Quality Adjustments

**Chords are being adjusted unexpectedly:**

- Review the warning messages to see which chords were adjusted
- The system corrects qualities to match diatonic expectations
- To avoid adjustments, use the correct quality suffix (e.g., "viidim" in Ionian)
- Check that you're using the intended mode for your progression

**No adjustments shown but chords seem wrong:**

- Verify mode selection matches your intended scale
- Check that you're using the correct Roman numeral quality for the mode
- Enable debug logs to see the adjustment process

### Visual Display Issues

**Columns not appearing:**

- Verify `chordGridContainer` is assigned to Content GameObject
- Verify `chordColumnPrefab` is assigned
- Check Console for rendering errors
- Enable debug logs to see rendering messages

**Columns appearing but empty:**

- Verify all TextMeshProUGUI fields are assigned in prefab
- Check that prefab structure matches expected hierarchy
- Ensure nested Canvas is present (required for background images)

**Columns not vertically centered:**

- In Content's HorizontalLayoutGroup: Uncheck `Child Force Expand: Height`
- Set `Child Alignment` to `Middle Center`
- Ensure Content's RectTransform has proper anchors (stretch vertically)

**Diatonic status not showing:**

- Verify `backgroundImage` is assigned in ChordColumnView prefab
- Verify `statusTagText` is assigned (optional, but needed for "non-diatonic" tag)
- Check that colors are set appropriately in Inspector
- Ensure `AnalyzeChord` is being called on original recipes (not adjusted)
- Enable debug logs to see analysis results

### Diagnostics Panel Issues

**Diagnostics not showing:**

- Verify `showDiagnostics` is enabled in Inspector
- Check that `statusText` and `scrollRect` are assigned
- Default view shows only Warning/Forced events - enable `includeInfoDiagnostics` to see Info events
- TRACE-related events are hidden by default - enable `includeTraceDiagnosticsInPanel` to see them

**Too many diagnostics lines:**

- Adjust `maxDiagnosticsLinesInPanel` value (default: 20)
- Increase value to see more lines, or decrease to reduce clutter
- Overflow message shows "... (X more lines hidden)" when cap is reached

**Panel not auto-scrolling:**

- Verify `scrollRect` is assigned to the ScrollRect component wrapping `statusText`
- Check that `ScrollToBottom()` is being called after diagnostics updates
- Ensure ScrollRect has proper layout setup (ContentSizeFitter on content)

## Code Structure

### Key Methods

- `OnPlayClicked()` - Entry point for button click
- `PlayProgressionCo()` - Main playback coroutine (includes quality adjustment logic)
- `RenderChordGrid()` - Creates and displays visual chord columns with adjusted numerals
- `GetKeyFromDropdowns()` - Combines tonic and mode dropdowns to create TheoryKey
- `GetModeFromDropdown()` - Maps dropdown index to ScaleMode enum
- `UpdateStatus()` - Updates status text display (includes adjustment warnings)
- `SetupModeDropdown()` - Populates dropdown options on Awake
- `TraceLog(string msg)` - Helper to log TRACE messages only when `enableUnityTraceLogs` is enabled
- `SetDiagnosticsAndRefresh(DiagnosticsCollector diags)` - Sets diagnostics collector and refreshes display, configures `EnableTrace` flag
- `ShowDiagnosticsSummary()` - Builds and displays filtered diagnostics summary with line cap and auto-scroll
- `ScrollToBottom()` - Auto-scrolls diagnostics panel to bottom when new content is added
- `CoScrollToBottom()` - Coroutine that handles layout updates and scrolling
- `BuildRegionsFromRomanInput()` - Shared helper that builds ChordRegion[] from Roman input with duration suffix support
  - Parses `:N` duration suffixes (e.g., `I:2` = 2 quarters)
  - Builds ChordEvents with optional melody MIDI attachment
  - Creates regions with cumulative startTick and correct durationTicks
  - Used by Play, SATB, and Naive Harmonize flows
- `GetRegionHoldSeconds()` - Computes playback hold duration from region's durationTicks
  - Formula: `chordDurationSeconds * (durationTicks / ticksPerQuarter)`
  - Falls back to `chordDurationSeconds` if region invalid
- `TryBuildChordRecipesFromRomanInput()` - Parses Roman numerals with optional `:N` duration suffixes
  - Returns `durationsInQuarters` list alongside recipes
  - Validates durations (warns if < 1, defaults to 1)
- `DebugLogFirstChordVoicing()` - Editor-only: Logs voicing for first chord in progression
- `DebugLogProgressionVoicing()` - Editor-only: Logs voicing for entire progression with voice-leading
- `DebugLogTestMelodyAnalysis()` - Editor-only: Logs melody analysis for test melody
- `DebugLogMelodyConstrainedVoicing()` - Editor-only: Logs melody-constrained voicing for test progression
- `DebugLogHarmonyCandidatesForTestMelody()` - Editor-only: Logs harmony candidates for each note in test melody
- `BuildTestMelodyLine()` - Helper method to build test melody for debug purposes
- `DebugPlayNaiveHarmonizationForTestMelody()` - Editor-only: Plays naive harmonization with voiced chords and updates VoicingViewer
- `PlayNaiveHarmonizationForCurrentTestMelody()` - Core method for naive harmonization (runtime-accessible, used by UI button)
- `OnNaiveHarmonizeClicked()` - UI callback for "N.H." button
- `PlayManualProgressionWithMelodyVoiced()` - Voices manual progression with current melody and plays with SATB display
- `OnPlayVoicedClicked()` - UI callback for "Play Voiced" button
- `PlayVoicedChordSequenceCo()` - Coroutine that plays voiced chord sequence and updates VoicingViewer in real-time
  - Passes `VoicedChord.VoicesMidi` array directly to both VoicingViewer and audio playback
  - TheoryVoicing voice order: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
  - Ensures UI and audio use the exact same voicing data
  - **Debug Logging:** When `TheoryVoicing.GetTendencyDebug()` is enabled, logs audio playback MIDI notes
- `BuildNoteNameMelodyLineFromInspector()` - Builds melody line from note-name text input (e.g., "F5 E5 D5")
- `BuildMelodyEventsForVoicedPlayback()` - Determines melody source for SATB playback (piano roll → text field → test melody)
  - Priority: Piano roll (if enabled & non-empty) → note-name text field → degree-based test melody
  - Trims melody events to timeline length if needed
  - Automatically mirrors piano roll melody to text field with duration suffixes when used
- `TrimMelodyEventsToTimeline()` - Static helper to clip melody events to fit within timeline duration
- `BuildNoteNameMelodyFromEventsWithDurations()` - Converts Timeline.MelodyEvent list to note-name string with duration suffixes
  - Format: "C5:2 D5:1 G5:3" (one token per onset with :N duration suffix in quarters)
  - Uses key-aware enharmonic spelling for note names
- `UpdateChordGridFromChordEvents()` - Helper to update chord grid from ChordEvent list
- `CreateSimpleSATBVoicingFromChordEvents()` - Helper to create SATB voicing from ChordEvent list
- `ClearViewers()` - Helper to clear both ChordGrid and VoicingViewer
- `BuildRomanProgressionString()` - Builds space-separated Roman numeral string from harmonized steps
- `BuildCurrentVoicedHarmonizationJson()` - Builds JSON export from current voiced harmonization state with detailed chord and melody analysis
- `DebugExportCurrentVoicedHarmonization()` - Editor-only: Exports voiced harmonization to JSON and copies to clipboard
- `GetTriadQualityString()` - Helper to get triad quality string from chord recipe
- `DetectIsPowerChord()` - Helper to detect power chords from recipe and chord symbol
- `DetectSuspension()` - Helper to detect suspension type (sus2/sus4) from chord symbol
- `DetectSeventhType()` - Helper to detect seventh presence and type from recipe and chord symbol

### ChordColumnView Component

The `ChordColumnView` script (`Assets/Scripts/UI/ChordColumnView.cs`) manages individual chord column display:

- `SetChord()` - Updates all text elements (chord symbol, note list, Roman numeral, diatonic status)
  - Accepts a list of note names (3 for triads, 4 for 7ths)
  - Accepts `ChordDiatonicStatus` to control visual styling
  - Automatically enables/disables note fields based on chord type
  - Applies background color and status tag based on diatonic status
  - Re-caches child visuals for state-based tinting (called automatically)
- `SetVizState()` - Sets the visual state of the chord column (Hidden / Visible / Highlighted)
  - Accepts state enum, alpha values, and color tints
  - Applies alpha via CanvasGroup (preserves layout spacing)
  - Applies color tint multiplicatively to all child visuals:
    - Background Image
    - All child Image components (note tiles)
    - All TMP_Text components (note labels, chord name, roman, analysis, status)
  - Uses cached child visual references (automatically discovered on Awake and SetChord)
  - Preserves original colors for reversible tinting
- `CacheChildVisuals()` - Private method that discovers and caches all child visuals
  - Finds all child Image components (excluding root background image)
  - Finds all TMP_Text components
  - Stores original colors for each visual
  - Called automatically on Awake and when SetChord is called
- `SetTexts()` - Legacy method (marked obsolete, use `SetChord()` instead)
- **Visual State Enum:** `ColumnVizState` (Hidden, Visible, Highlighted)
- Serialized fields for all TextMeshProUGUI references:
  - `noteTopText` - Highest note (triads and 7ths)
  - `noteUpperMiddleText` - Upper middle note (7ths only)
  - `noteLowerMiddleText` - Lower middle note (triads and 7ths)
  - `noteBottomText` - Lowest note (triads and 7ths)
  - `statusTagText` - Small label for "non-diatonic" tag (optional)
- Serialized fields for styling:
  - `backgroundImage` - Reference to column background Image component
  - `diatonicColor` - Background color for diatonic chords (default: white)
  - `nonDiatonicColor` - Background color for non-diatonic chords (default: red)
- **Internal State Management:**
  - `canvasGroup` - CanvasGroup component for alpha control (auto-created if missing)
  - `originalBackgroundColor` - Stores original background color for tinting
  - Cached child visual lists (Images and TMP_Text) with original colors
  - Automatic discovery of child visuals (no manual Inspector assignment needed)
- Note ordering: Highest to lowest (top to bottom for display)

### MelodyPianoRoll Component

The `MelodyPianoRoll` script (`Assets/Scripts/UI/MelodyPianoRoll.cs`) provides an interactive monophonic melody editor with onset grid model:

- **Onset Grid Model:**
  - `midiAtStep[]` array stores onsets: `midiAtStep[t]` = MIDI note that starts at step t, or null (gap)
  - Durations are inferred from spacing between onsets (not stored explicitly)
  - Adjacent onsets create repeated short notes (repeated attacks)
  - Gaps extend the previous note's duration

- `RenderFromEvents()` - Renders the piano roll from a list of timeline melody events
  - Converts `Timeline.MelodyEvent` (tick-based) to onset grid
  - Only marks onset steps in `midiAtStep[]` (not full duration)
  - Creates pitch background rows procedurally based on pitch range
  - Creates column instances for each timeline step
  - Calls `RedrawFromEvents()` to show onset tiles
  - Accepts `totalSteps` (must match VoicingViewer) and `TimelineSpec` for tick-to-step conversion

- `HandleCellClick(int stepIndex, int midi)` - Handles user clicks on the piano roll grid
  - Simple onset-only editing: only touches `midiAtStep[stepIndex]`
  - No event scanning or tick conversion logic
  - Logic:
    - `existing == null` → place onset
    - `existing == midi` → delete onset (toggle off)
    - `existing != midi` → change onset pitch
  - Rebuilds events and redraws after each click

- `BuildEventsFromGrid()` - Converts onset grid to `Timeline.MelodyEvent` list
  - Scans `midiAtStep[]` for all onsets
  - For each onset i at step t_i:
    - `startTick = t_i * ticksPerQuarter`
    - `endTick = next onset's step, or totalSteps if last`
    - `durationTicks = (endTick - startTick) * ticksPerQuarter`
    - `midi = midiAtStep[t_i].Value`
  - Repeated adjacent onsets create separate 1-step events
  - Gaps contribute to the previous note's duration

- `RedrawFromEvents(List<MelodyEvent> events)` - Visual rendering from events
  - Shows only onset tiles (not full-duration bars)
  - Each event draws a single tile at its start step
  - Clears all columns first, then shows tiles for each event's onset
  - Durations are not visualized (only used in engine & text export)

- `SetHighlightedStep()` - Sets the highlighted step index (0-based, -1 to clear)
  - Must match the step index used by VoicingViewer.SetHighlightedStep
  - Updates all column highlight states

- `Clear()` - Clears the piano roll display (removes all pitch rows and columns)
  - Called automatically when no melody is present

- `SyncFromVoicing()` - Synchronizes scroll position with VoicingViewer (optional feature)

- **Pitch Background:** Procedurally generates horizontal rows for each MIDI pitch in range
  - Rows are colored based on black/white key detection (C#, D#, F#, G#, A# are black keys)
  - White key rows use `whiteKeyRowColor` (lighter)
  - Black key rows use `blackKeyRowColor` (darker)

- **Serialized Fields:**
  - `lowestMidi` / `highestMidi` - MIDI range for display (default: 60-79, C4-G5)
  - `pitchBackgroundContainer` - RectTransform for pitch background rows (should be stretched to fill viewport)
  - `columnsContainer` - Transform for column GameObjects (Content with HorizontalLayoutGroup)
  - `columnPrefab` - Prefab reference for MelodyPianoRollColumn instances
  - `pianoRollScrollRect` / `voicingScrollRect` - ScrollRect references for scroll synchronization (optional)
  - `normalBackgroundColor` - Background color for normal (non-highlighted) columns
  - `highlightBackgroundColor` - Background color for highlighted (currently playing) columns
  - `noteBarColor` - Color for note tiles (single color for all pitches)
  - `whiteKeyRowColor` - Background color for white key pitch rows
  - `blackKeyRowColor` - Background color for black key pitch rows
  - `enableDebugLogs` - Enable debug logging for click handling and editing
  - `logEventsOnChange` - Log MelodyEvents whenever the grid changes (for testing)

### MelodyPianoRollColumn Component

The `MelodyPianoRollColumn` script (`Assets/Scripts/UI/MelodyPianoRollColumn.cs`) represents a single time step (column) in the piano roll:

- `Initialize()` - Initializes the column with pitch range and colors
  - Sets step index, MIDI range, background colors, and note tile color
  - Accepts parent `MelodyPianoRoll` reference for click callbacks
  - Calculates initial background color based on timeline grouping
- `SetNote(int? midi)` - Sets the note tile for this column (null = no note)
  - Positions note tile vertically based on MIDI pitch
  - Centers note tile within its pitch row
  - Hides note tile if MIDI is null or out of range
- `HideNote()` - Hides the note tile for this column
  - Helper method that calls `SetNote(null)`
- `SetHighlighted()` - Sets whether this column is highlighted (currently playing step)
  - Updates background color to use highlight color when highlighted
  - Uses timeline grouping colors when not highlighted
- `OnPointerClick(PointerEventData eventData)` - Handles pointer clicks on this column
  - Implements `IPointerClickHandler` interface
  - Calculates which pitch row was clicked based on Y position
  - Notifies parent `MelodyPianoRoll` via `HandleCellClick(stepIndex, midi)`
  - Requires `backgroundImage.raycastTarget` to be enabled for click detection
- **Timeline Grouping:** Columns alternate background colors in groups for easier reading
  - `groupSize` - Number of steps in one visual group (e.g., 4 for 4-step banding)
  - `groupAColor` - Background color for even-numbered groups (0, 2, 4, ...)
  - `groupBColor` - Background color for odd-numbered groups (1, 3, 5, ...)
  - Grouping colors are overridden by highlight color when column is highlighted
- **Serialized Fields:**
  - `backgroundImage` - Image component for column background (for highlighting and grouping, must have raycastTarget enabled)
  - `noteBarRect` - RectTransform for the note tile (positioned vertically based on MIDI pitch)
  - `noteBarImage` - Image component for the note tile
  - `groupSize` - Number of steps in one visual group (default: 4)
  - `groupAColor` - Background color for even-numbered groups
  - `groupBColor` - Background color for odd-numbered groups

### VoicingViewer Component

The `VoicingViewer` script (`Assets/Scripts/UI/VoicingViewer.cs`) displays accumulated SATB voicings during naive harmonization and manual progression playback:

- `ShowVoicing()` - Appends a voiced chord to the accumulating sequence
  - On first step (stepIndex == 1): Resets all accumulators and starts new sequence
  - **Voice Order:** Uses the exact order from TheoryVoicing without sorting: [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
    - Preserves the SATB voice assignment from `TheoryVoicing.VoiceLeadProgressionWithMelody()`
    - Index 0 = Bass, Index 1 = Tenor, Index 2 = Alto, Index 3 = Soprano
    - Maps directly to display labels (no pitch-based reordering)
  - Uses canonical chord spelling via `TheorySpelling.GetTriadSpelling()` for chord tones (root, 3rd, 5th)
  - Falls back to key-aware spelling for non-triad tones (7ths, extensions, suspensions)
  - Pads all note names to 2-character width for visual alignment
  - Highlights large leaps (≥5 semitones by default) in red using TextMeshPro color tags
    - Leap detection uses original voice order (not sorted) to compare consecutive chords
  - Appends notes to accumulator strings with spacing (three spaces between tokens)
  - Updates all four voice line text fields with accumulated sequences
  - **Debug Logging:** When `TheoryVoicing.GetTendencyDebug()` is enabled, logs received MIDI notes with voice labels (e.g., `[Viewer Debug] Step 4: Received MIDI notes: Bass=G(55) Tenor=D(50) Alto=B(59) Soprano=G(67)`)
- `Clear()` - Resets all accumulator strings and clears all text fields
  - Called automatically at start of new playback session
  - Called manually to reset display
- Serialized fields for TextMeshProUGUI references:
  - `headerText` - Header showing step information (e.g., "Current Voicing — 5 steps")
  - `bassText` - Bass voice line (accumulated notes)
  - `tenorText` - Tenor voice line (accumulated notes)
  - `altoText` - Alto voice line (accumulated notes)
  - `sopranoText` - Soprano voice line (accumulated notes)
- Serialized fields for configuration:
  - `largeLeapSemitoneThreshold` - Semitone distance threshold for highlighting large leaps (default: 5)
- Internal accumulator strings: `bassLine`, `tenorLine`, `altoLine`, `sopranoLine`
- Internal state tracking: `previousChordSortedMidi` - Stores previous chord's MIDI notes in original order for leap detection
- Sequence accumulation: Each call to `ShowVoicing()` appends the current chord's notes to the existing sequence
- Display persists after playback completes (not cleared at end of playback)
- Canonical spelling: Uses lookup table for theoretically correct enharmonic spellings (e.g., bVII shows Bb-D-F, not A#-D-F)
- **Synchronization:** Uses the exact same `VoicesMidi` array from TheoryVoicing that audio playback uses, ensuring UI and audio match exactly

## Timeline v1: Melody as Independent Timeline Lane

Timeline v1 refactors melody to be an independent timeline lane with multiple notes per chord region, while harmony and SATB voicing remain one-voicing-per-chord-region.

### Key Concepts

- **MelodyEvent (Timeline):** Represents a melody note with `startTick`, `durationTicks`, and `midi` on the timeline
- **Independent Playback:** Melody events are scheduled independently alongside SATB chord playback, using timeline tick timing
- **Multiple Events Per Region:** Melody events may occur multiple times within a single chord region and may overlap chord boundaries
- **Chord Symbol Rule:** Chord symbols reflect ONLY SATB voicing and explicitly requested extensions. Melody non-chord tones do NOT upgrade chord symbols (e.g., melody A over C chord does NOT display "C(add6)" unless add6 was explicitly requested)

### Implementation

- **Melody Event Creation:** `BuildTimelineMelodyEvents()` converts TimeBeats-based melody input to tick-based Timeline.MelodyEvent list
- **Overlap Detection:** `MelodyEventOverlapsRegion()` determines which melody events overlap each chord region
- **Melody Classification:** `ClassifyMelodyNote()` categorizes melody notes as: ChordTone, RequestedExtension, or NonChordTone (informational only)
- **Playback:** `ScheduleTimelineMelodyEvents()` coroutine plays melody events using timeline tick timing in parallel with SATB playback
- **Diagnostics:** `AnalyzeMelodyAgainstRegions()` generates per-region summaries of melody chord tones vs non-chord tones

### Regression Tests

Two property-based regression cases verify Timeline v1 invariants:
- `TimelineV1_Melody4xPerRegion_SATBUnchanged`: Verifies SATB voicing remains legal when melody events multiply per region
- `TimelineV1_MelodyNCT_ChordSymbolUnchanged`: Verifies chord symbol does not change when melody introduces non-chord tones

## Related Documentation

- [REFACTOR_PLAN.md](REFACTOR_PLAN.md) - Phase 1 theory kernel
- [CHORD_KERNEL.md](CHORD_KERNEL.md) - Phase 2 chord kernel specification
- [CHORD_LAB_VISUAL_SETUP.md](CHORD_LAB_VISUAL_SETUP.md) - Detailed prefab setup guide

## Implementation Notes

- Button is automatically wired in `Awake()` (similar to `ScaleUI`)
- Debug logging is comprehensive for troubleshooting
- All error paths update status text and log to Console
- Coroutine-based playback allows for timing control
- Uses existing MusicSystem infrastructure for audio
- Visual chord grid is rendered before playback starts
- Chord columns use Root/Third/Fifth naming (musically accurate)
- Nested Canvas in prefab is required for proper Image rendering
- Root note names use key-aware spelling via `GetNoteNameForDegreeWithOffset()` (proper enharmonic spelling based on key context)
  - For borrowed flat chords (e.g., bVII, bIII), root names are computed from pitch class, not parsed from Roman numeral tokens
  - This ensures "Bb", "Eb" display correctly instead of "b", "b"
- Chord tone names use canonical spelling via `TheorySpelling.GetTriadSpelling()`:
  - Lookup-table-based canonical spellings for all triad tones (major, minor, diminished, augmented)
  - Ensures musically correct enharmonic spellings (e.g., bVII shows Bb-D-F, not A#-D-F)
  - Used by both ChordGrid and VoicingViewer for consistent display
  - Handles enharmonic disambiguation via `RootSemitoneOffset` (e.g., Gb major vs F# major)
  - Non-triad tones (7ths, extensions) use key-aware spelling as fallback
- Accidental preference is mode-aware: minor keys (Aeolian) always prefer flats, major keys use tonic-based preference
- Diatonic status analysis uses `AnalyzeChordProfile()` which includes 7th quality checks (I7 in C Ionian is non-diatonic)
- Roman numerals in display are key-aware and show 'n' when appropriate (e.g., `nvi` in C Aeolian)
- Warning messages clearly indicate when and how chords were adjusted (only when auto-correct is enabled)
- Seventh chord support: parses `7`, `maj7`, `m7`, `ø7`/`m7b5`, `dim7`, `aug7` suffixes
- Chord columns dynamically display 3 or 4 notes based on chord type
- 7th-aware chord symbols: `maj7`, `m7`, `m7b5`, `aug7`
- Slash chord notation for inversions (e.g., "Cmaj7/E" for first inversion)
- Quality adjustment only applies to triads (7th chords pass through unchanged, inversion preserved)
- `BuildChord` respects `ChordQuality` for interval calculation and applies inversions by rotating notes
- Root note comes from mode (with optional b/#/n offset); third/fifth intervals come from quality (Major: 4,7; Minor: 3,7; Dim: 3,6; Aug: 4,8)
- 7th interval is determined by explicit `SeventhQuality` on the recipe
- Bass doubling can be enabled/disabled via `emphasizeBassWithLowOctave` toggle
- **TheoryVoicing system** (Editor debug tools):
  - `VoiceFirstChord()` - Block voicing for single chords (3-4 voices, close position)
    - **Resolution-aware 7th placement:** When the first chord has a 7th and the next chord is known, the 7th is placed in a position that allows downward step resolution (1-2 semitones) to a chord tone in the next chord
    - Accepts optional `nextChordEvent` parameter for one-step lookahead
    - Ensures the first chord's 7th can resolve correctly when voicing the second chord
    - **First-chord tension enforcement:** Uses a priority-ordered multi-voice search (Soprano → Alto → Tenor) when enforcing required tensions (b9, 9, #11) to ensure reliable placement on the first chord
  - `VoiceLeadProgression()` - Progression voicing with voice-leading (common tones preserved, smooth movement)
  - `VoiceLeadProgressionWithMelody()` - Progression voicing with melody constraints (soprano locked to melody)
  - **Voice Order Convention:** Returns `VoicedChord.VoicesMidi` array in order [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
    - This exact order is preserved by VoicingViewer and used by audio playback
    - No sorting or reordering is performed after voicing is complete
  - Respects chord inversions (bass uses 3rd/5th/7th for inversions, not always root)
    - Root position preference when no inversion is explicitly specified
    - Bass inversion preference with cost-based selection and safeguard rules
    - **Bass-tenor constraint:** Bass selection is constrained to be strictly below the tenor minimum
    - Prevents voice crossing between bass and tenor
  - Ensures chord tone coverage (triads contain root/3rd/5th, 7th chords contain root/3rd/7th at minimum)
    - 7th chords prioritize 7th over 5th: Root, 3rd, and 7th are required; 5th is optional
    - Never leaves a 7th chord without its 7th if it's in the recipe
    - **V9 chord fix:** Dominant 9 chords (V9, etc.) are treated as "dominant 7th + 9" rather than "triad + 9". The `GetChordTonePitchClasses()` function automatically includes the 7th (b7) in the chord tone set for dominant chords with natural 9, even if the recipe's `Extension` is not explicitly set to `Seventh`. This ensures V9 chords always have the 7th available for voicing.
    - **Global 3rd enforcement:** Ensures every chord has a 3rd (converts duplicate root/fifth voices to 3rd when needed)
    - **Triad duplicate limits:** Prevents tripled notes in triads (no pitch class appears 3+ times)
    - **7th chord coverage:** Requires root, 3rd, 7th, and diversity (at least 3 distinct chord tone pitch classes)
    - **Chord tone coverage repair (`FixChordToneCoverage`):** When placing missing required tones (e.g., 7th), the system prioritizes replacing the perfect 5th (lowest priority) over requested tensions
      - Requested tensions are protected by a direct guard that runs at the top of each voice selection pass
      - When no legal victim is found in normal passes, a fallback explicitly searches for the perfect 5th, including Soprano when not melody-locked (Play mode)
      - Comprehensive diagnostic logging helps identify why voices are rejected or selected
    - **Requested extension enforcement:** When extensions are requested (9, b9, #11, sus4, add9, add11), they are enforced in voicing
      - Requested melodic tensions (9, b9, #11) have highest priority and are hard-protected once placed
      - Priority ordering: requested tensions > 3rd/7th > root > 5th
      - System tries all non-protected voices before giving up on placing a requested tension
      - Allows overwriting duplicated required tones (e.g., doubled 3rd) to place requested tensions
      - Add-tones (add9, add11) are optional (preferred but not required)
      - Natural 9 never satisfies b9 requirement (strict pitch class matching)
      - **Direct guard protection:** Requested tension pitch classes are checked at the top of each voice selection pass, before any other logic, ensuring they can never be selected as victims for replacement
      - **Perfect 5th fallback:** When no legal victim is found for placing a required tone (e.g., 7th), the system explicitly searches for the perfect 5th across all voices (including Soprano when `protectSoprano=false` in Play mode) as a fallback, since the 5th is the lowest-priority chord tone
      - **Diagnostic logging:** Comprehensive logging includes `protectSoprano`, `maxVoiceIndex`, soprano exclusion status, and detailed voice rejection reasons to help diagnose voicing issues
  - When `MelodyMidi` is set on `ChordEvent`, soprano voice is forced to that MIDI note
    - **Soprano protection:** Soprano is protected from modification when melody is present
    - Inner voices are constrained to be strictly below the soprano when melody is present
    - Initial voicing generation ensures soprano remains highest voice
    - Coverage fixes and 3rd enforcement respect soprano protection
  - **Tonal Tendency Rules:** Preferences and hard constraints for musically intuitive voice-leading:
    - **Rule A: Chord 7th Resolution (Hard Constraint when no melody):**
      - When there's no explicit melody (`MelodyMidi == null`), all voices (including soprano) with chord 7ths must resolve down by step (1-2 semitones) if a valid resolution tone exists within the voice's range
      - Strong bonus (-10.0f) for correct downward step resolution
      - Large penalty (+1000.0f) for non-resolving candidates when a valid resolution exists (effectively forbidden)
      - Post-selection enforcement overrides candidate selection to force correct resolution
      - Voice locking protects resolved 7ths from being undone by fix-up logic
      - When melody is present, soprano uses softer preferences (allows melodic freedom)
    - **Rule A (Soft when melody present):** Inner-voice 7ths prefer downward step resolution with bonuses/penalties
    - **Rule A (Melody doubling case):** When the melody already has the resolution tone, inner voices can double it for stronger resolution
    - **Common-tone 3rd→7th preference:** Voices that are the 3rd of the current chord get a bonus if they hold that pitch when it becomes the 7th of the next chord (common-tone resolution in circle-of-fifths progressions)
    - **Rule B: Global leading tone:** Prefers to resolve up to tonic (softly)
      - Softened when next chord's 7th would be missing (preserves 7th coverage)
    - **Rule C: Local leading tone:** 3rd of secondary dominants prefers to resolve up to target root (softly)
      - Softened when next chord's 7th would be missing (preserves 7th coverage)
    - **Rule D: Augmented 5th Resolution:** Augmented 5ths (#5) prefer to resolve up by semitone
      - Strong bonus for upward semitone resolution (+1 semitone)
      - Small penalty for staying or stepping down
      - Larger penalty for leaps or contrary motion
      - Configurable weight via `augmentedFifthResolutionWeight` (default: 1.5f)
      - Applied in both SATB and Play voicing paths that use full cost evaluation
      - **Hard Constraint Enforcement:** When an augmented 5th is detected in a voice and the next chord supports the resolution target, candidates are filtered to only include the target pitch class if available (similar to 7th resolution enforcement)
      - **Unison Doubling Support:** Inner voices (Tenor/Alto) can share the same MIDI note when it's the only valid resolution option that satisfies both the resolution requirement and voice ordering constraints
      - **Hard Constraint Enforcement:** When an augmented 5th is detected in a voice and the next chord supports the resolution target, candidates are filtered to only include the target pitch class if available (similar to 7th resolution enforcement)
      - **Unison Doubling Support:** Inner voices (Tenor/Alto) can share the same MIDI note when it's the only valid resolution option that satisfies both the resolution requirement and voice ordering constraints
  - **Advanced Voicing Preferences (Inspector-Tunable):**
    - **Register Gravity:** Pulls inner voices (Tenor/Alto) toward preferred MIDI centers
      - Adds cost penalty based on distance from preferred register centers
      - Configurable centers and weights for Tenor and Alto separately
    - **Voice Compression Incentive:** Soft preference for narrower inner voice spacing
      - Penalizes overly wide gaps between Alto-Tenor and Soprano-Alto
      - Configurable target gaps and weights
    - **Voice Leading Smoothness Priority:** Explicit weight on movement cost vs. other costs
      - Allows trading off smoothness against compactness, register, and compression
      - Weight of 1.0 = current behavior, <1.0 = prioritize smoothness, >1.0 = prioritize other costs
  - **Play Voicing Continuity Adjustment:** Tames awkward leaps and extreme registers in simple Play voicing
    - `PlayVoicingSettings` struct with configurable leap limits and voice ranges
    - `AdjustPlayVoicingForContinuity()` method shifts voices by octaves to reduce large leaps
    - Preserves pitch classes while adjusting register
    - Only affects Play path (no melody); SATB path uses full search with melody constraints
    - Default settings: Max leap = 9 semitones, voice ranges: Soprano (C4-G5), Alto (G3-D5), Tenor (C3-G4), Bass (E2-C4)
  - **Hard Constraints (Always Enforced):**
    - **Voice crossing prevention:** Bass must be ≤ Tenor, Tenor ≤ Alto, Alto ≤ Soprano (allows unison for inner voices)
      - Enforced during candidate selection in SATB+melody path (Alto candidates filtered to be >= Tenor)
      - Lane identity preserved: No post-selection reordering that could swap voices
    - **Spacing limits:** Soprano-Alto ≤ octave, Alto-Tenor ≤ octave, Tenor-Bass ≤ 2 octaves
    - **Chord tone coverage:** Essential chord tones (root, 3rd, 5th for triads; root, 3rd, 7th for 7ths) must be present
    - **No non-chord tones:** Only chord tone pitch classes are allowed in voicings
    - **Triad duplicate limits:** No pitch class appears 3+ times in triads
    - **Augmented 5th resolution:** When an augmented 5th is present and the next chord supports the resolution target, the same voice must resolve to the target pitch class if available
  - **Debug Logging:** `TheoryVoicing.SetTendencyDebug(true)` enables detailed logging:
    - Bass selection decisions and candidate evaluation
    - Voice tendency analysis and cost adjustments (Rule A/B/C)
    - Hard 7th resolution enforcement (HARD GOOD/VETO messages)
    - Post-selection enforcement forcing 7th resolutions
    - Voice locking for protected 7th resolutions
    - First-chord resolution-aware 7th placement
    - Complete SATB voicing before and after chord tone coverage fixes
    - Chord tone coverage enforcement (7th prioritization)
    - Common-tone 3rd→7th bonuses and leading-tone softening
    - Register gravity and compression cost calculations
    - Movement weighting and total cost breakdowns
    - First chord voicing state (before/after FixChordToneCoverage)
    - Non-chord tone detection warnings
    - Bass selection with tenor floor constraints
    - Augmented 5th resolution tendency (Rule D) with interval and penalty/bonus values
    - Requested extension enforcement and protection (b9, 9, #11)
    - Comprehensive failure traces when requested tensions are missing (voice constraints, candidate availability, enforcement decisions)
    - **Chord tone coverage repair diagnostics:** When `FixChordToneCoverage` is called, logs include:
      - `protectSoprano`, `maxVoiceIndex`, and soprano exclusion status
      - Current voices with MIDI and pitch classes
      - Required, optional, and requested tension pitch classes
      - Protected voice indices and reasons (required tone vs. requested tension)
      - Voice rejection reasons in each pass (direct guard, protected, excluded by maxVoiceIndex, etc.)
      - Fallback perfect 5th search when no legal victim is found
      - Final victim selection with reason (optional, duplicated, perfect 5th, etc.)
  - **Trace Logging Control:**
    - All `[TRACE]` and `[TRACE SNAPSHOT]` logs are gated behind `DiagnosticsCollector.EnableTrace` flag
    - Set automatically from `ChordLabController.enableUnityTraceLogs` toggle
    - When disabled: No TRACE logs appear in Unity console (quiet by default)
    - When enabled: Full TRACE logging including:
      - Entry/exit points for voicing functions
      - Candidate generation and selection
      - Injected tendency candidates (7th resolution, local leading tones)
      - Voice selection decisions
      - Post-selection enforcement actions
      - SATB snapshots with legality checks (Selected, AfterPostSelectionEnforcement, AfterFixChordToneCoverage)
    - `VOICED_REGION` tripwire diagnostic only emitted when trace is enabled
  - Editor menu items available: `Tools → Chord Lab → Log First Chord Voicing`, `Log Progression Voicing`, and `Log Melody-Constrained Voicing`
- **TheoryMelody system**:
  - `AnalyzeEvent()` - Maps a melodic event (MIDI note) to scale degree, semitone offset, and diatonic status
  - `AnalyzeMelodyLine()` - Analyzes a sequence of melodic events
  - Editor menu item: `Tools → Chord Lab → Log Test Melody Analysis`
- **TheoryHarmonization system**:
  - `GetChordCandidatesForMelodyNote()` - Returns chord candidates for a melody note based on scale degree
  - Currently supports Ionian mode and diatonic notes only (returns empty list for others)
  - Uses classic major-key tonal harmony mappings
  - Validates that melody pitch class is present in each candidate chord
  - Editor menu item: `Tools → Chord Lab → Log Harmony Candidates For Test Melody`
- **VoicingViewer system**:
  - `ShowVoicing()` - Appends voiced chord notes to accumulating SATB sequence display
    - **Preserves Voice Order:** Uses exact order from TheoryVoicing [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
      - No pitch-based sorting that could reorder voices
      - Direct index mapping: 0→Bass, 1→Tenor, 2→Alto, 3→Soprano
    - Uses canonical chord spelling via `TheorySpelling` lookup table for chord tones
    - Pads note names to 2-character width for visual alignment
    - Highlights large leaps (≥5 semitones by default) in red
    - Leap detection uses original voice order (not sorted)
    - **Debug Logging:** When `TheoryVoicing.GetTendencyDebug()` is enabled, logs received MIDI notes with voice labels
  - `Clear()` - Resets all accumulators and clears display
  - Integrated into `PlayVoicedChordSequenceCo()` for real-time display during naive harmonization and manual progression playback
  - Accumulated sequence remains visible after playback completes
  - Configurable leap threshold via `largeLeapSemitoneThreshold` serialized field (default: 5)
  - **Synchronization:** Uses the exact same `VoicesMidi` array from TheoryVoicing that audio playback uses
- **TheorySpelling system**:
  - `GetTriadSpelling()` - Returns canonical note names (root, 3rd, 5th) for major/minor/diminished/augmented triads
  - Ensures musically correct enharmonic spellings (e.g., Gb major shows "Gb, Bb, Db", not "F#, A#, C#")
  - Supports enharmonic disambiguation via `RootSemitoneOffset` parameter
- **Timeline system**:
  - `TimelineSpec` - Timeline configuration DTO (ticksPerQuarter, optional tempo/time signature)
  - `ChordRegion` - Timeline region DTO (startTick, durationTicks, chordEvent, debugLabel)
  - `MelodyEvent` (Timeline namespace) - Timeline region DTO for melody notes (startTick, durationTicks, midi)
    - Timeline v1: Melody is an independent timeline lane with multiple events per chord region
    - Melody events are scheduled independently alongside SATB chord playback
    - Melody events may overlap chord boundaries and occur multiple times per region
  - Regions use cumulative `startTick` calculation (each region starts after previous region's end)
  - Duration suffixes (`:N`) are parsed and stored in `durationTicks = N * ticksPerQuarter`
  - Playback timing uses `GetRegionHoldSeconds()` to compute hold duration from `durationTicks`
- **TheoryChordTension system**:
  - `ChordTensionHelper.IsAugmentedFifth()` - Identifies augmented 5th (#5) chord tones for voice-leading tendencies
  - `ChordTensionDetector.DetectNinthTensions()` - Detects 9th tensions from realized voicing
  - `ChordTensionUtils` - Utilities for mapping tensions to intervals and vice versa
- **VoicedHarmonizationSnapshot system**:
  - DTO classes for exporting complete voiced harmonization state as JSON
  - Captures key, mode, melody, chord progression, and SATB voicing with note names and MIDI
  - Enhanced chord analysis fields:
    - Triad quality (Major, Minor, Diminished, Augmented)
    - Power chord detection (IsPowerChord, OmitsThird)
    - Suspension detection (Suspension: sus2/sus4)
    - Seventh chord detection (HasSeventh, SeventhType)
  - Enhanced melody analysis fields:
    - Scale degree and chromatic offset
    - Degree label (e.g., "5", "b6", "#4")
    - Chord tone detection (IsChordTone)
  - Used by JSON export functionality for LLM analysis
- **Chord Tension System**:
  - `TensionKind` enum: FlatNine (b9), Nine (9), SharpNine (#9)
  - `ChordTension` struct: Represents a single tension
  - `ChordTensionUtils` class: Utilities for interval mapping (semitone offsets, interval → tension kind)
  - `ChordTensionDetector.DetectNinthTensions()`: Detects tensions from realized voicing
  - `TheoryChord.TryParseRequestedExtensions()`: Parses extension tokens (9, b9, add9, 11, #11, add11, sus4, 7sus4) from chord symbols and Roman numerals
  - **Requested Extension Enforcement:**
    - Extensions can be specified in input (e.g., `G7b9`, `V7b9`, `Cmaj9`, `ii7sus4`)
    - Requested melodic tensions (9, b9, #11) are enforced in voicing (must appear in realized voicing)
    - Add-tones (add9, add11) are optional color tones (preferred but not required)
    - Sus4/7sus4 are suspension modifiers (affect chord structure)
    - Extensions are preserved through parsing → recipe → voicing pipeline
    - Hard protection: Once a requested tension is placed, it cannot be overwritten by lower-priority chord tone coverage
    - Priority ordering: requested tensions > 3rd/7th > root > 5th
    - System searches all non-protected voices before giving up on placing a requested tension
    - Allows overwriting duplicated required tones (e.g., doubled 3rd) to place requested tensions
    - Natural 9 never satisfies b9 requirement (strict pitch class matching: b9 = root+1, 9 = root+2)
    - **First-chord enforcement strategy:** For the first chord in a progression, enforcement uses a priority-ordered multi-voice search:
      - Tries voices in order: Soprano → Alto → Tenor
      - Accepts the first valid placement that passes spacing checks
      - Ensures reliable tension placement when no voice-leading constraints exist (e.g., `V7b9 | i` reliably includes b9 on the first chord)
  - **Tension Detection and Display:**
    - Tensions are detected from SATB voicing only (Timeline v1: Timeline melody events are NOT included in chord symbol detection)
    - Displayed in chord symbols via `FormatChordSymbolWithNinthTensions()`:
      - 7th chords with natural 9: "G7" → "G9", "Cmaj7" → "Cmaj9", "Cm7" → "Cm9"
      - 7th chords with altered 9ths: "G7" + b9 → "G7(b9)", "G7" + b9,#9 → "G7(b9,#9)"
      - Triads with tensions: "C" + 9 → "C(add9)", "C" + b9 → "C(addb9)"
  - **Debug Tracing:**
    - Comprehensive trace logs when requested tensions are missing (gated by `s_debugTensionDetect`)
    - Shows parsing, candidate availability, voice constraints, and enforcement decisions
    - Helps diagnose why a requested tension couldn't be placed
  - Extensible for future 11th/13th support
