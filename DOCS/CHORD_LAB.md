# Chord Lab Panel Documentation

## Overview

The Chord Lab panel is a new UI feature in the `LLM_Chat_Terminal` scene that allows users to input Roman numeral chord progressions and play them using the TheoryChord system. This is a manual, interactive tool that demonstrates the Phase 2 chord kernel functionality. The system includes a sophisticated voicing layer (TheoryVoicing) with intelligent voice-leading rules, including hard constraints for 7th chord resolution, resolution-aware first-chord placement, and melody-aware behavior. Editor-only debug tools are available for analyzing chord voicings and voice-leading patterns.

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
     - Examples: `bII`, `#iv`, `nvi`, `IVmaj7/3rd`, `V7/5th`
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
   - Configurable:
     - Chord duration (default: 1.0s)
     - Gap between chords (default: 0.1s)
     - Root octave (default: 4)
     - Velocity (default: 0.9)
     - Bass doubling: `emphasizeBassWithLowOctave` toggle (default: true) - doubles the bass note an octave below

4. **Status Messages**

   - Real-time feedback on parsing and playback
   - Error messages for invalid numerals
   - Progress indication during playback
   - Quality adjustment warnings when chord qualities are corrected to match the mode
   - Format: `"Adjusted chord {index} ('{original}' → '{adjusted}') to {Quality} to fit C {Mode}."`

5. **Visual Chord Representation**

   - Displays chord columns in a horizontal scrollable grid
   - Each column shows:
     - Chord symbol (e.g., "C", "Am", "Gdim", "Cmaj7", "Dm7", "Bm7b5", "Cmaj7/E") - reflects adjusted quality, 7th extensions, and slash chord notation for inversions
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

6. **Debug Logging**

   - Comprehensive debug logs for troubleshooting
   - Can be enabled/disabled via Inspector
   - **Tendency Debug Logging:** Separate toggle via `Tools → Chord Lab → Toggle Tendency Debug Logging`
     - When enabled, provides detailed logging of voice-leading rules and 7th resolution enforcement
     - See "Editor-Only Voicing Debug Tools" section for full details
   - Standard logs include:
     - Button click events
     - Mode selection
     - Numeral parsing
     - Chord building
     - MIDI note playback
     - Timing information
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

## UI Structure

```
Canvas
  └── Panel_ChordLab
       ├── Label_Title          (TextMeshProUGUI: "Chord Lab")
       ├── Scroll_ChordGrid     (ScrollRect: Horizontal scrolling container)
       │    └── Viewport
       │         └── Content    (GameObject with HorizontalLayoutGroup)
       │              └── (ChordColumnView instances instantiated here)
       ├── Dropdown_Tonic       (TMP_Dropdown: Tonic/key center selection - 12 pitch classes)
       ├── Dropdown_Mode        (TMP_Dropdown: Mode selection)
       ├── Input_Progression    (TMP_InputField: Roman numeral input)
       ├── Button_Play          (Button: Triggers playback)
       ├── Button_NaiveHarmonize (Button: Triggers naive harmonization with voiced playback - optional)
       ├── Button_PlayVoiced    (Button: Plays manual progression with SATB voicing - optional)
       ├── Text_Status          (TextMeshProUGUI: Status/error messages)
       └── Voicing_Viewer       (GameObject with VoicingViewer component - optional)
            ├── Text_Header     (TextMeshProUGUI: Step information header)
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
├── Text_NoteTop (TextMeshProUGUI: Highest note - for triads and 7ths)
├── Text_NoteUpperMiddle (TextMeshProUGUI: Upper middle note - for 7ths only)
├── Text_NoteLowerMiddle (TextMeshProUGUI: Lower middle note - for triads and 7ths)
├── Text_NoteBottom (TextMeshProUGUI: Lowest note - for triads and 7ths)
└── Text_Roman (TextMeshProUGUI: Roman numeral)
```

**Note:** The nested Canvas is required for proper rendering of background images and layout spacing. Unity auto-creates this when UI elements are added.

**Note Display Logic:**

- **Triads (3 notes):** Uses `Text_NoteTop`, `Text_NoteLowerMiddle`, `Text_NoteBottom` (skips `Text_NoteUpperMiddle`)
- **7th Chords (4 notes):** Uses all four note fields: `Text_NoteTop`, `Text_NoteUpperMiddle`, `Text_NoteLowerMiddle`, `Text_NoteBottom`
- Fields are automatically enabled/disabled based on chord type

## Component Configuration

### ChordLabController Serialized Fields

**UI References:**

- `buttonPlay` - Button component that triggers playback
- `buttonNaiveHarmonize` - Button component for naive harmonization with voiced playback (optional, runtime UI)
- `buttonPlayVoiced` - Button component for playing manual progression with SATB voicing (optional)
- `tonicDropdown` - TMP_Dropdown for tonic/key center selection (12 pitch classes: C, C#/Db, D, Eb, E, F, F#/Gb, G, Ab, A, Bb, B)
- `modeDropdown` - TMP_Dropdown for mode selection
- `progressionInput` - TMP_InputField for Roman numeral entry
- `statusText` - TextMeshProUGUI for status messages
- `chordGridContainer` - Transform of Content GameObject (Scroll_ChordGrid/Viewport/Content)
- `chordColumnPrefab` - Prefab reference for ChordColumnView instances
- `voicingViewer` - VoicingViewer component reference (optional, for SATB display during naive harmonization and manual progression playback)

**Music References:**

- `musicDataController` - MusicDataController (optional, reserved for future)
- `synth` - FmodNoteSynth for audio playback

**Settings:**

- `rootOctave` - Root octave for all chords (default: 4)
- `chordDurationSeconds` - Duration of each chord (default: 1.0)
- `gapBetweenChordsSeconds` - Pause between chords (default: 0.1)
- `velocity` - MIDI velocity 0-1 (default: 0.9)

**Playback Settings:**

- `emphasizeBassWithLowOctave` - If enabled, doubles the bass note an octave below during playback (default: true)
- `useVoicingEngine` - If enabled, playback uses voice-leading engine. If disabled, uses root-position chords (default: true)
- `useTestMelodyForPlayback` - If true, playback uses a simple one-note-per-chord test melody and melody-constrained voicing. If false, use normal chord-only voicing (default: false)
- `testMelodyDegrees` - Scale degrees for the test melody (one per chord). Values wrap if progression is longer than this array. Example: [3, 4, 4, 2, 1] means degree 3 for first chord, degree 4 for second and third, etc. (default: [3, 4, 4, 2, 1])

**Theory Settings:**

- `autoCorrectToMode` - Toggle to enable/disable automatic quality correction (default: true)
  - When `true`: Chords are automatically adjusted to match diatonic triads for the mode
  - When `false`: Chords play exactly as typed (non-diatonic chords sound with their specified quality)

**Melody Input (Debug):**

- `testMelodyNoteNames` - Optional text area for note-name melody input (space-separated, e.g., "F5 E5 D5 B4 C5"). Used by naive harmonization when not empty. Falls back to degree-based melody if parsing fails.

**Debug:**

- `enableDebugLogs` - Enable/disable debug logging (default: true)

## How It Works

### Playback Flow

1. User selects mode from dropdown
2. User enters Roman numeral progression (e.g., "I V vi IV")
3. User clicks Play button
4. `OnPlayClicked()` is called:
   - Stops any existing playback
   - Starts `PlayProgressionCo()` coroutine
5. Coroutine:
   - Validates UI references
   - Gets tonic and mode from dropdowns
   - Creates `TheoryKey` with selected tonic and mode
   - Splits input by whitespace into numerals
   - Validates each numeral using `TheoryChord.TryParseRomanNumeral()` (supports b/#/n accidentals and inversions)
   - Stores original ChordRecipe objects for each parsed numeral
   - Conditionally adjusts chord qualities using `TheoryChord.AdjustTriadQualityToMode()` (if `autoCorrectToMode` is enabled)
   - Analyzes original recipes using `TheoryChord.AnalyzeChordProfile()` to determine diatonic status (includes 7th quality checks)
   - Builds chords from adjusted recipes using `TheoryChord.BuildChord()` (respects ChordQuality for intervals, applies inversions)
   - Renders visual chord grid using `RenderChordGrid()` with key-aware Roman numerals and status information
   - Plays each chord using `PlayChord()` helper (handles optional bass doubling)
   - Waits for duration + gap before next chord
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
- **TheoryChord.BuildChord()** - Builds individual chords from recipes (3 notes for triads, 4 for 7ths, applies inversions)
  - Root note comes from the mode (with optional b/#/n offset)
  - Third and fifth intervals are calculated from `ChordQuality` (Major: 4,7; Minor: 3,7; Dim: 3,6; Aug: 4,8)
  - 7th interval is determined by explicit `SeventhQuality` on the recipe
  - Inversions rotate lowest note(s) up an octave
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
- **TheoryMelody.AnalyzeEvent()** - Analyzes a single melodic event, mapping MIDI to scale degree and semitone offset
- **TheoryMelody.AnalyzeMelodyLine()** - Analyzes an entire melody line, returning analysis for each note
- **TheoryHarmonization.GetChordCandidatesForMelodyNote()** - Generates chord candidates for a melody note (Ionian mode, supports chromatic candidates with accidental hints)
- **TheorySpelling.GetTriadSpelling()** - Returns canonical note names (root, 3rd, 5th) for triads using lookup table
  - Supports major, minor, diminished, and augmented triads
  - Ensures musically correct enharmonic spellings
  - Handles enharmonic disambiguation via `RootSemitoneOffset` parameter
  - Used by both ChordGrid and VoicingViewer for consistent display

## Example Usage

### Example 1: Standard Progression

1. Open `LLM_Chat_Terminal` scene
2. Select tonic: "C", mode: "Mixolydian"
3. Enter progression: `I V vi IV`
4. Click Play
5. Hears: C major, G major, A minor, F major (all in C Mixolydian context)

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

- Existing Unity systems:
  - `FmodNoteSynth` for audio playback
  - `MusicDataController` (referenced but not actively used)

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
   - ⏳ User-defined melody input interface (currently Inspector-only text area)
   - Arpeggiated chords
   - Strumming patterns
   - Rhythmic variations

3. **Extended Roman Numeral Support** (Partially Implemented)

   - ✅ Basic 7th chords (`I7`, `ii7`, `V7`, `viidim7`, `Iaug7`)
   - ✅ Extended 7th syntax (`maj7`, `ø7`/`m7b5` for half-diminished)
   - ✅ Leading accidentals (`b`, `#`, `n`/`N` for parallel Ionian)
   - ✅ Inversion syntax (`/3rd`, `/5th`, `/7th` or `/3`, `/5`, `/7`)
   - ✅ Slash chord notation in chord symbols (e.g., `Cmaj7/E` for first inversion)
   - ⏳ Secondary dominants (detected in analysis but not parsed from syntax like "V/V")
   - ⏳ Suspensions and other alterations

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
   - ⏳ Extended mode support for harmonization (currently Ionian only)
   - ⏳ Chromatic note support for harmonization (currently supports chromatic candidates with accidental hints)
   - ⏳ User melody input interface (currently Inspector-only)
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

## Code Structure

### Key Methods

- `OnPlayClicked()` - Entry point for button click
- `PlayProgressionCo()` - Main playback coroutine (includes quality adjustment logic)
- `RenderChordGrid()` - Creates and displays visual chord columns with adjusted numerals
- `GetKeyFromDropdowns()` - Combines tonic and mode dropdowns to create TheoryKey
- `GetModeFromDropdown()` - Maps dropdown index to ScaleMode enum
- `UpdateStatus()` - Updates status text display (includes adjustment warnings)
- `SetupModeDropdown()` - Populates dropdown options on Awake
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
- `SetTexts()` - Legacy method (marked obsolete, use `SetChord()` instead)
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
- Note ordering: Highest to lowest (top to bottom for display)

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
  - `VoiceLeadProgression()` - Progression voicing with voice-leading (common tones preserved, smooth movement)
  - `VoiceLeadProgressionWithMelody()` - Progression voicing with melody constraints (soprano locked to melody)
  - **Voice Order Convention:** Returns `VoicedChord.VoicesMidi` array in order [0=Bass, 1=Tenor, 2=Alto, 3=Soprano]
    - This exact order is preserved by VoicingViewer and used by audio playback
    - No sorting or reordering is performed after voicing is complete
  - Respects chord inversions (bass uses 3rd/5th/7th for inversions, not always root)
    - Root position preference when no inversion is explicitly specified
    - Bass inversion preference with cost-based selection and safeguard rules
  - Ensures chord tone coverage (triads contain root/3rd/5th, 7th chords contain root/3rd/7th at minimum)
    - 7th chords prioritize 7th over 5th: Root, 3rd, and 7th are required; 5th is optional
    - Never leaves a 7th chord without its 7th if it's in the recipe
  - When `MelodyMidi` is set on `ChordEvent`, soprano voice is forced to that MIDI note
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
