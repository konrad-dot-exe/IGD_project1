# Chord Lab Visual Representation Setup Guide

## Overview

This guide explains how to set up the visual chord representation UI in the Chord Lab panel.

## Implementation Status

✅ **Completed:**

- `TheoryPitch.GetPitchNameFromMidi()` - Note name helper method with key-aware enharmonic spelling
- `TheoryPitch.GetNoteNameForDegreeWithOffset()` - Key-aware root note spelling
- `TheoryChord.GetSpelledChordTones()` - Chord-aware enharmonic spelling for chord tones (legacy)
- `TheorySpelling.GetTriadSpelling()` - Canonical lookup-table-based spelling for triad tones (used in ChordGrid and VoicingViewer)
- `ChordColumnView.cs` - Script for individual chord column display (supports 3-4 notes)
- `ChordLabController` - Extended with chord grid rendering and tonic selector
- `RenderChordGrid()` method - Creates visual chord columns with canonical chord spelling
- Tonic/key-center support - Select any of 12 pitch classes as the key center
- Seventh chord parsing and display support
- 7th-aware chord symbol generation
- Slash chord notation for inversions
- Inversion support (`/3rd`, `/5th`, `/7th`)
- Natural accidental support (`n`/`N` for parallel Ionian)
- Function tag display (secondary dominants, borrowed chords, Neapolitan)
- VoicingViewer with canonical spelling, note padding, and leap highlighting
- Synchronized ChordGrid and VoicingViewer updates
- Runtime UI buttons for naive harmonization and manual progression voicing
- JSON export for voiced harmonization with enhanced chord analysis (triad quality, power chords, suspensions, seventh types)
- Note-name melody input support

## UI Structure to Create

Under `Panel_ChordLab`, create the following hierarchy:

```
Panel_ChordLab
  ├── ... (existing UI elements)
  ├── Dropdown_Tonic (TMP_Dropdown: Tonic/key center selection)
  ├── Dropdown_Mode (TMP_Dropdown: Mode selection)
  └── Scroll_ChordGrid (ScrollRect)
       └── Content (GameObject with HorizontalLayoutGroup)
            └── (ChordColumnView instances will be instantiated here)
```

### ScrollRect Setup

1. **Create Scroll_ChordGrid:**

   - Right-click `Panel_ChordLab` → UI → Scroll View
   - Rename to `Scroll_ChordGrid`
   - Configure ScrollRect component:
     - Horizontal: enabled
     - Vertical: disabled (or enabled if you want vertical scrolling)
     - Movement Type: Elastic or Clamped

2. **Configure Content:**
   - The ScrollRect automatically creates a `Content` child
   - Add `Horizontal Layout Group` component to Content
   - Settings:
     - Spacing: 10-20 pixels
     - Child Alignment: Upper Center (or Middle Center)
     - Child Controls Size: Both checked
     - Child Force Expand: Width unchecked, Height unchecked
   - Set Content size as needed (it will expand horizontally as columns are added)

## ChordColumnView Prefab

### Create the Prefab

1. **Create Prefab Structure:**

   ```
   ChordColumnView (GameObject)
   ├── Canvas (nested, auto-created by Unity)
   ├── VerticalLayoutGroup component
   ├── ChordColumnView script
   ├── Text_ChordName (TextMeshProUGUI: Chord symbol)
   ├── Text_NoteTop (TextMeshProUGUI: Highest note - used for triads and 7ths)
   ├── Text_NoteUpperMiddle (TextMeshProUGUI: Upper middle note - 7ths only)
   ├── Text_NoteLowerMiddle (TextMeshProUGUI: Lower middle note - triads and 7ths)
   ├── Text_NoteBottom (TextMeshProUGUI: Lowest note - triads and 7ths)
   └── Text_Roman (TextMeshProUGUI: Roman numeral)
   ```

   **Note:** Each note text element can optionally have a background Image component for the box effect. The layout should accommodate both 3-note triads and 4-note 7th chords.

2. **Setup Steps:**

   - Create empty GameObject: `ChordColumnView`
   - Add `VerticalLayoutGroup` component
     - Spacing: 5-10 pixels
     - Child Alignment: Upper Center
     - Child Controls Size: Width unchecked, Height checked
     - Child Force Expand: Width unchecked, Height unchecked
   - Add `ChordColumnView` script component

3. **Create Text Elements:**

   - **Text_ChordName**

     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Font size: 24-28
     - Alignment: Center
     - Text: "C" (placeholder)
     - **Note:** Layout position depends on your design (top or bottom with Roman numeral)

   - **Text_NoteTop** (highest note)

     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Add Image component as background (optional, for box effect)
     - Font size: 18-22
     - Alignment: Center
     - Preferred width: 50-60 pixels
     - Preferred height: 40-50 pixels
     - Text: "G" (placeholder)

   - **Text_NoteUpperMiddle** (upper middle note - for 7th chords)

     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Add Image component as background (optional, for box effect)
     - Font size: 18-22
     - Alignment: Center
     - Preferred width: 50-60 pixels
     - Preferred height: 40-50 pixels
     - Text: "E" (placeholder)
     - **Important:** This field is only used for 7th chords (4 notes). For triads, it will be hidden.

   - **Text_NoteLowerMiddle** (lower middle note)

     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Add Image component as background (optional, for box effect)
     - Font size: 18-22
     - Alignment: Center
     - Preferred width: 50-60 pixels
     - Preferred height: 40-50 pixels
     - Text: "E" (placeholder for triads) or "C" (for 7th chords)

   - **Text_NoteBottom** (lowest note)

     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Add Image component as background (optional, for box effect)
     - Font size: 18-22
     - Alignment: Center
     - Preferred width: 50-60 pixels
     - Preferred height: 40-50 pixels
     - Text: "C" (placeholder)

   - **Text_Roman**
     - Create: Right-click ChordColumnView → UI → Text - TextMeshPro
     - Font size: 20-24
     - Alignment: Center
     - Text: "I" (placeholder)
     - **Note:** Layout position depends on your design (bottom with chord name)

4. **Wire Script References:**

   - Select `ChordColumnView` GameObject
   - In `ChordColumnView` script component:
     - Drag `Text_ChordName` to `chordNameText`
     - Drag `Text_NoteTop` to `noteTopText`
     - Drag `Text_NoteUpperMiddle` to `noteUpperMiddleText` (NEW - for 7th chords)
     - Drag `Text_NoteLowerMiddle` to `noteLowerMiddleText`
     - Drag `Text_NoteBottom` to `noteBottomText`
     - Drag `Text_Roman` to `romanText`

5. **Create Prefab:**
   - Drag `ChordColumnView` from Hierarchy to `Assets/Prefabs/` (or wherever you store prefabs)
   - Delete the instance from the scene (we'll instantiate from prefab)

## VoicingViewer Setup

The VoicingViewer component displays accumulated SATB voicings during naive harmonization and manual progression playback. This is an optional feature that supports canonical chord spelling, note name padding for alignment, and large leap highlighting.

### Create Voicing_Viewer GameObject

1. **Create Structure:**

   ```
   Voicing_Viewer (GameObject)
   ├── VoicingViewer script component
   ├── Text_Header (TextMeshProUGUI: Step information)
   ├── Text_Soprano (TextMeshProUGUI: Soprano voice line)
   ├── Text_Alto (TextMeshProUGUI: Alto voice line)
   ├── Text_Tenor (TextMeshProUGUI: Tenor voice line)
   └── Text_Bass (TextMeshProUGUI: Bass voice line)
   ```

2. **Setup Steps:**

   - Create empty GameObject: `Voicing_Viewer` (under `Panel_ChordLab`)
   - Add `VoicingViewer` script component

3. **Create Text Elements:**

   - **Text_Header**

     - Create: Right-click Voicing_Viewer → UI → Text - TextMeshPro
     - Font size: 18-22
     - Alignment: Center
     - Text: "Current Voicing" (placeholder)

   - **Text_Soprano**

     - Create: Right-click Voicing_Viewer → UI → Text - TextMeshPro
     - Font size: 16-20
     - Alignment: Left (or Center, depending on layout preference)
     - Text: "S:" (placeholder, or just empty)

   - **Text_Alto**

     - Create: Right-click Voicing_Viewer → UI → Text - TextMeshPro
     - Font size: 16-20
     - Alignment: Left (or Center, depending on layout preference)
     - Text: "A:" (placeholder, or just empty)

   - **Text_Tenor**

     - Create: Right-click Voicing_Viewer → UI → Text - TextMeshPro
     - Font size: 16-20
     - Alignment: Left (or Center, depending on layout preference)
     - Text: "T:" (placeholder, or just empty)

   - **Text_Bass**
     - Create: Right-click Voicing_Viewer → UI → Text - TextMeshPro
     - Font size: 16-20
     - Alignment: Left (or Center, depending on layout preference)
     - Text: "B:" (placeholder, or just empty)

4. **Wire Script References:**
   - Select `Voicing_Viewer` GameObject
   - In `VoicingViewer` script component:
     - Drag `Text_Header` to `headerText`
     - Drag `Text_Soprano` to `sopranoText`
     - Drag `Text_Alto` to `altoText`
     - Drag `Text_Tenor` to `tenorText`
     - Drag `Text_Bass` to `bassText`
     - **Optional:** Configure `Large Leap Semitone Threshold` (default: 5) - Notes with voice movements at or above this threshold will be highlighted in red

## Melody Piano Roll Setup

The Melody Piano Roll displays melody events as a visual grid-based piano roll with procedural pitch background rows showing black/white key distinction.

### Create MelodyPianoRoll GameObject

1. **Create Structure:**

   ```
   MelodyPianoRoll (GameObject)
   ├── MelodyPianoRoll script component
   ├── Scroll_MelodyPianoRoll (ScrollRect)
   │    └── Viewport
   │         ├── PitchBackgroundContainer (RectTransform: Stretched to fill viewport)
   │         └── Content (GameObject with HorizontalLayoutGroup)
   │              └── (MelodyPianoRollColumn instances will be instantiated here)
   └── (columnPrefab reference)
   ```

2. **Setup Steps:**

   - Create empty GameObject: `MelodyPianoRoll` (under `MainContainer`)
   - Add `MelodyPianoRoll` script component

3. **Create ScrollRect:**

   - Right-click `MelodyPianoRoll` → UI → Scroll View
   - Rename to `Scroll_MelodyPianoRoll`
   - Configure ScrollRect component:
     - Horizontal: enabled
     - Vertical: disabled
     - Movement Type: Elastic or Clamped

4. **Create PitchBackgroundContainer:**

   - Under `Scroll_MelodyPianoRoll/Viewport`, create empty GameObject: `PitchBackgroundContainer`
   - Add `RectTransform` component (auto-added)
   - Configure RectTransform:
     - Anchor Preset: Stretch-Stretch (to fill the viewport)
     - Left, Right, Top, Bottom: all 0
     - **Important:** This GameObject should appear BEFORE `Content` in the Hierarchy (so columns draw on top)

5. **Configure Content:**

   - The ScrollRect automatically creates a `Content` child
   - Add `Horizontal Layout Group` component to Content
   - Settings:
     - Spacing: 2-4 pixels (or as desired)
     - Child Alignment: Middle Left (or Upper Left)
     - Child Controls Size: Both checked
     - Child Force Expand: Width unchecked, Height unchecked
   - Add `Content Size Fitter` component to Content:
     - Horizontal Fit: Preferred Size
     - Vertical Fit: Unconstrained
   - This ensures Content expands horizontally to accommodate all columns

## MelodyPianoRollColumn Prefab

### Create the Prefab

1. **Create Prefab Structure:**

   ```
   columnPrefab (GameObject)
   ├── RectTransform
   ├── MelodyPianoRollColumn script
   ├── Image (Background - for highlighting and grouping)
   └── NoteBar (GameObject)
        ├── RectTransform (anchored for vertical positioning)
        └── Image (Note bar visual)
   ```

2. **Setup Steps:**

   - Create empty GameObject: `columnPrefab`
   - Add `Image` component (this will be the column background)
   - Add `MelodyPianoRollColumn` script component
   - Add `LayoutElement` component:
     - Preferred Width: 30-40 pixels (or as desired)
     - Preferred Height: (matches piano roll height, typically 250-300 pixels)

3. **Create NoteBar:**

   - Right-click `columnPrefab` → Create Empty
   - Rename to `NoteBar`
   - Add `RectTransform` component (auto-added)
   - Add `Image` component (this will be the note bar visual)
   - Configure RectTransform:
     - Anchor Preset: Stretch-Stretch
     - Left, Right: 0
     - Initially set anchors to span full height (will be adjusted at runtime)
   - Configure Image:
     - Color: Light cyan/blue (will be overridden by script)

4. **Wire Script References:**

   - Select `columnPrefab` GameObject
   - In `MelodyPianoRollColumn` script component:
     - Drag the background `Image` component to `Background Image`
     - Drag `NoteBar` RectTransform to `Note Bar Rect`
     - Drag `NoteBar/Image` component to `Note Bar Image`
   - Configure Timeline Grouping (optional):
     - `Group Size`: 4 (or desired number of steps per group)
     - `Group A Color`: Lighter gray for even-numbered groups
     - `Group B Color`: Darker gray for odd-numbered groups

5. **Create Prefab:**

   - Drag `columnPrefab` from Hierarchy to `Assets/Prefabs/` (or wherever you store prefabs)
   - Delete the instance from the scene (we'll instantiate from prefab)

## Wiring ChordLabController

1. **Select `Panel_ChordLab` in Hierarchy**

2. **In `Chord Lab Controller` component:**

   **Chord Grid section:**

   - `Chord Grid Container` → Drag `Scroll_ChordGrid/Content` GameObject
   - `Chord Column Prefab` → Drag the `ChordColumnView` prefab you created

   **Voicing Viewer section:**

   - `Voicing Viewer` → Drag the `Voicing_Viewer` GameObject (optional - leave unassigned if not using)

   **Melody Piano Roll section:**

   - `Melody Piano Roll` → Drag the `MelodyPianoRoll` GameObject (optional - leave unassigned if not using)

## Wiring MelodyPianoRoll

1. **Select `MelodyPianoRoll` GameObject in Hierarchy**

2. **In `Melody Piano Roll` script component:**

   **UI References section:**

   - `Pitch Background Container` → Drag `Scroll_MelodyPianoRoll/Viewport/PitchBackgroundContainer` GameObject
   - `Columns Container` → Drag `Scroll_MelodyPianoRoll/Viewport/Content` GameObject
   - `Column Prefab` → Drag the `columnPrefab` prefab you created
   - `Piano Roll Scroll Rect` → Drag `Scroll_MelodyPianoRoll` ScrollRect component (optional, for scroll sync)
   - `Voicing Scroll Rect` → Drag VoicingViewer's ScrollRect (optional, for scroll sync)

   **Pitch Range section:**

   - `Lowest Midi` → Lowest MIDI note to display (default: 60, C4)
   - `Highest Midi` → Highest MIDI note to display (default: 79, G5)

   **Styling section:**

   - `Normal Background Color` → Background color for normal (non-highlighted) columns
   - `Highlight Background Color` → Background color for highlighted (currently playing) columns
   - `Note Bar Color` → Color for note bars (single color for all pitches, default: light cyan)
   - `White Key Row Color` → Background color for white key pitch rows (default: light gray)
   - `Black Key Row Color` → Background color for black key pitch rows (default: darker gray)

   **Visual State Styling section:**

   - `Hidden Alpha` → Alpha value for Hidden state (0-1, default: 0.0)
   - `Visible Alpha` → Alpha value for Visible state (0-1, default: 1.0)
   - `Highlighted Alpha` → Alpha value for Highlighted state (0-1, default: 1.0)
   - `Visible Tint` → Color tint for Visible state (default: white)
   - `Highlighted Tint` → Color tint for Highlighted state (default: white)
   - **Note:** These parameters control the visual appearance of chord columns during playback. Tinting applies to all visuals (background, note tiles, text) automatically.

   **UI Controls section (if using runtime buttons):**

   - `Button Naive Harmonize` → Drag button for naive harmonization (optional - runtime UI)
   - `Button Play Voiced` → Drag button for playing manual progression with SATB voicing (optional)

   **Note:** Both buttons are optional. If unassigned, the corresponding functionality is still available via Editor menu items.

## Visual Styling Suggestions

- **Column Width:** 60-80 pixels
- **Column Spacing:** 10-20 pixels
- **Note Boxes:** Add subtle background images or borders for box effect
- **Font Colors:**
  - Chord name: White or accent color
  - Note names: White or light gray
  - Roman numeral: White or accent color
- **Layout:** VerticalLayoutGroup makes stacking automatic
- **Visual State System:**
  - Chord columns support three visual states during playback: Hidden, Visible, Highlighted
  - All visuals (background, note tiles, text) are automatically tinted based on state
  - Configure alpha and tint values in ChordLabController Inspector
  - Hidden state allows "pre-visible" columns when `hiddenAlpha > 0` (e.g., 0.3 for faint preview)
  - Highlighted state can use a different tint to emphasize the currently playing chord
  - Visual state changes are applied automatically during playback (no manual setup needed)

## Testing

### Test 1: Triads

After setup:

1. Open `LLM_Chat_Terminal` scene
2. Select tonic: "C", mode: "Ionian"
3. Enter progression: `I V vi IV`
4. Click Play
5. You should see 4 columns appear (each with 3 notes):
   - **C** / G/E/C (notes) / **I**
   - **G** / D/B/G (notes) / **V**
   - **Am** / E/C/A (notes) / **vi**
   - **F** / C/A/F (notes) / **IV**
   - Note: `Text_NoteUpperMiddle` should be hidden for triads
   - All columns should have white/light background (diatonic)

### Test 2: Seventh Chords

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `ii7 V7 I7`
3. Click Play
4. You should see 3 columns appear (each with 4 notes):
   - **Dm7** / A/F/D/C (notes) / **ii7** (diatonic, white background)
   - **G7** / D/B/G/F (notes) / **V7** (diatonic, white background)
   - **C7** / Bb/G/E/C (notes) / **I7** (non-diatonic, red background, "sec. to IV" tag)
   - Note: All 4 note fields should be visible for 7th chords
   - I7 is non-diatonic because it has a dominant 7th instead of major 7th

### Test 3: Mixed Triads and 7ths

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `I ii7 V I7`
3. Click Play
4. You should see:
   - Column 1: **C** with 3 notes (triad, diatonic)
   - Column 2: **Dm7** with 4 notes (7th, diatonic)
   - Column 3: **G** with 3 notes (triad, diatonic)
   - Column 4: **C7** with 4 notes (7th, non-diatonic, "sec. to IV")

### Test 4: Inversions

1. Select tonic: "C", mode: "Ionian"
2. Enter progression: `I IVmaj7/3rd I`
3. Click Play
4. You should see:
   - Column 1: **C** with 3 notes (root position)
   - Column 2: **Fmaj7/A** with 4 notes (first inversion - A in bass)
   - Column 3: **C** with 3 notes (root position)
   - Roman numerals: "I", "IVmaj7/3rd", "I"

### Test 5: Natural Accidentals

1. Select tonic: "C", mode: "Aeolian"
2. Enter progression: `i nvi iv i`
3. Click Play
4. You should see:
   - Column 1: **Cm** with 3 notes (diatonic)
   - Column 2: **Am** with 3 notes (non-diatonic, "borrowed ∥ major", Roman shows "nvi")
   - Column 3: **Fm** with 3 notes (diatonic)
   - Column 4: **Cm** with 3 notes (diatonic)

## Testing VoicingViewer

### Test 1: Naive Harmonization Playback

After setting up VoicingViewer:

1. Open `LLM_Chat_Terminal` scene
2. Ensure `voicingViewer` field is assigned in ChordLabController Inspector
3. Run menu item: `Tools → Chord Lab → Play Naive Harmonization For Test Melody (Voiced)`
4. You should see:
   - Header updates to show "Current Voicing — N steps" (where N is the number of chords)
   - SATB voice lines accumulate as each chord plays:
     ```
     S: E   F   G   G   E
     A: C   D   E   F   C
     T: G   A   B   C   G
     B: C   C   C   D   C
     ```
   - Notes are spaced with three spaces between tokens
   - All note names are padded to 2-character width for visual alignment
   - Large leaps (≥5 semitones by default) are highlighted in red
   - Canonical chord spelling ensures musically correct enharmonics (e.g., bVII shows Bb-D-F, not A#-D-F)
   - Display persists after playback completes

### Test 2: Multiple Playback Sessions

1. Run naive harmonization playback (as above)
2. Let it complete and verify SATB sequence remains visible
3. Run naive harmonization playback again
4. You should see:
   - Display clears at start of new playback (fresh state)
   - New sequence accumulates as before
   - Previous sequence is replaced (not appended)

## Troubleshooting

**VoicingViewer not updating during playback:**

- Verify `voicingViewer` field is assigned in ChordLabController Inspector
- Check Console for errors during playback
- Ensure VoicingViewer component has all TextMeshProUGUI fields assigned
- Enable debug logs in ChordLabController to see playback messages

**VoicingViewer showing wrong note names:**

- Verify key selection matches intended key
- Check that note names use canonical chord spelling (e.g., bVII shows Bb-D-F, not A#-D-F)
- Canonical spelling is used for triad tones (root, 3rd, 5th) via `TheorySpelling` lookup table
- Non-triad tones (7ths, extensions) use key-aware spelling as fallback
- Ensure chord context is passed correctly to `ShowVoicing()` method

**VoicingViewer accumulating incorrectly:**

- Verify Clear() is called at start of playback (check debug logs)
- Check that stepIndex starts at 1 for first chord
- Ensure ShowVoicing() is called once per chord in sequence

**No columns appear:**

- Check Console for errors
- Verify `chordGridContainer` is assigned
- Verify `chordColumnPrefab` is assigned
- Check that prefab has `ChordColumnView` script attached
- Enable debug logs in ChordLabController to see rendering messages

**Columns appear but are empty:**

- Check that all text fields are assigned in the prefab
- Verify text elements are child objects of the prefab root
- Check Console for parsing errors

**Columns appear but text is wrong:**

- Verify note names are being generated correctly (check debug logs)
- Check that chord symbols match expected format (including 7th suffixes: maj7, m7, m7b5, aug7)
- Check for slash chord notation in inversions (e.g., "Cmaj7/E")
- Verify Roman numerals show key-aware notation (may show 'n' for naturalized chords)
- Ensure MIDI notes are being sorted correctly
- For 7th chords, verify all 4 note fields are visible
- For triads, verify only 3 note fields are visible (upperMiddle should be hidden)
- Root note names should use proper key-aware spelling (e.g., "Ab" in C Aeolian, not "G#")
- Chord tone names should use chord-aware spelling (e.g., Cm in G Ionian should show C–Eb–G, not C–D#–G)

**7th chords not displaying 4 notes:**

- Verify `noteUpperMiddleText` field is assigned in prefab
- Check that `SetChord()` method is being called (not legacy `SetTexts()`)
- Enable debug logs to see note count in console
- Verify chord parsing is recognizing 7th extension correctly

## Troubleshooting Melody Piano Roll

**Piano roll not displaying:**

- Verify `melodyPianoRoll` field is assigned in ChordLabController Inspector
- Check Console for errors during playback
- Ensure MelodyPianoRoll component has all required references assigned:
  - `pitchBackgroundContainer` must be assigned
  - `columnsContainer` must be assigned
  - `columnPrefab` must be assigned
- Verify melody input is not empty and melody events are being generated

**Pitch background rows not appearing:**

- Verify `pitchBackgroundContainer` is assigned and is a child of Viewport
- Check that `PitchBackgroundContainer` appears BEFORE `Content` in Hierarchy (so columns draw on top)
- Verify `PitchBackgroundContainer` RectTransform is set to Stretch-Stretch (fills viewport)
- Ensure `lowestMidi` and `highestMidi` are set to valid values (e.g., 60-91)

**Note bars not appearing or positioned incorrectly:**

- Verify `columnPrefab` has `MelodyPianoRollColumn` component
- Check that `noteBarRect` and `noteBarImage` are assigned in prefab
- Ensure note bars are positioned within valid pitch range
- Verify note bar RectTransform is set up correctly (anchors should be adjusted at runtime)

**Columns compressing instead of scrolling:**

- Verify `Content` GameObject has `ContentSizeFitter` component
- Set `ContentSizeFitter.Horizontal Fit` to `Preferred Size`
- Ensure `columnPrefab` has `LayoutElement` with `Preferred Width` set (e.g., 30-40 pixels)
- Check that `HorizontalLayoutGroup` on Content has `Child Controls Size: Width` checked

**Highlighting not synchronized with VoicingViewer:**

- Verify both `melodyPianoRoll` and `voicingViewer` are assigned in ChordLabController
- Check that `SetHighlightedStep()` is being called for both during playback
- Ensure step indices match (both use 0-based quarter-note steps)

**Scroll synchronization not working:**

- Verify `pianoRollScrollRect` and `voicingScrollRect` are assigned in MelodyPianoRoll Inspector
- Check that scroll synchronization callback is wired up (if using this feature)

## Code Reference

- `MelodyPianoRoll.cs` - Script for piano roll display
  - `RenderFromEvents()` - Renders piano roll from timeline melody events
  - `SetHighlightedStep()` - Sets highlighted step (synchronized with VoicingViewer)
  - `Clear()` - Clears piano roll display
  - `RebuildPitchBackground()` - Procedurally generates pitch background rows
  - `RebuildColumns()` - Creates column instances for each timeline step
- `MelodyPianoRollColumn.cs` - Script for individual column
  - `Initialize()` - Initializes column with pitch range and colors
  - `SetNote()` - Sets note for column and positions note bar
  - `SetHighlighted()` - Sets highlight state
  - Timeline grouping support for alternating background colors
- `MelodyPianoRoll.cs` - Script for piano roll display
  - `RenderFromEvents()` - Renders piano roll from timeline melody events
  - `SetHighlightedStep()` - Sets highlighted step (synchronized with VoicingViewer)
  - `Clear()` - Clears piano roll display
  - `RebuildPitchBackground()` - Procedurally generates pitch background rows
  - `RebuildColumns()` - Creates column instances for each timeline step
- `MelodyPianoRollColumn.cs` - Script for individual column
  - `Initialize()` - Initializes column with pitch range and colors
  - `SetNote()` - Sets note for column and positions note bar
  - `SetHighlighted()` - Sets highlight state
  - Timeline grouping support for alternating background colors
- `ChordColumnView.cs` - Script for individual chord column
  - `SetChord()` - Main method (accepts note list, handles 3-4 notes, diatonic status, analysis info)
  - `SetVizState()` - Sets visual state (Hidden / Visible / Highlighted) with alpha and tint control
    - Applies tinting to all child visuals automatically (background, note tiles, text)
    - Uses CanvasGroup for alpha control (preserves layout spacing)
    - Caches child visuals automatically (no manual assignment needed)
  - `CacheChildVisuals()` - Private method that discovers and caches all child visuals for tinting
  - `SetTexts()` - Legacy method (obsolete, kept for compatibility)
  - `ColumnVizState` enum - Visual state enumeration (Hidden, Visible, Highlighted)
- `VoicingViewer.cs` - Script for SATB voicing display
  - `ShowVoicing()` - Appends voiced chord notes to accumulating sequence
    - Uses canonical chord spelling via `TheorySpelling` lookup table
    - Pads note names to 2-character width for alignment
    - Highlights large leaps (≥5 semitones) in red
  - `Clear()` - Resets all accumulators and clears display
  - Configurable leap threshold via `largeLeapSemitoneThreshold` field
- `TheorySpelling.cs` - Static helper class for canonical chord tone spellings
  - `GetTriadSpelling()` - Returns canonical root/3rd/5th names for triads
  - Supports major, minor, diminished, and augmented triads
  - Handles enharmonic disambiguation via `RootSemitoneOffset`
- `ChordLabController.RenderChordGrid()` - Method that creates columns with key-aware Roman numerals
- `ChordLabController.PlayChord()` - Helper method for playback with optional bass doubling
- `TheoryPitch.GetPitchNameFromMidi()` - Converts MIDI to note name with key-aware enharmonic spelling
- `TheoryPitch.GetNoteNameForDegreeWithOffset()` - Key-aware root note spelling
- `TheoryChord.GetSpelledChordTones()` - Chord-aware enharmonic spelling for chord tones
- `TheoryChord.TryParseRomanNumeral()` - Parses Roman numerals (supports 7th suffixes, b/#/n accidentals, inversions)
- `TheoryChord.BuildChord()` - Builds chords (3 notes for triads, 4 for 7ths, applies inversions)
- `TheoryChord.RecipeToRomanNumeral(TheoryKey, ChordRecipe)` - Key-aware conversion to Roman numeral (shows 'n' when appropriate)
- `TheoryChord.GetChordSymbol()` - Generates chord symbols with slash chord notation for inversions
- `TheoryChord.AnalyzeChordProfile()` - Analyzes chords for diatonic status, function tags, and borrowing

## Advanced Features

### Seventh Chord Support

The Chord Lab supports seventh chords end-to-end:

- **Parsing:** Accepts `I7`, `ii7`, `V7`, `viidim7`, `Iaug7`, `Imaj7`, `iiø7`/`iim7b5`
- **Building:** Creates 4-note chords for 7th extensions
- **Display:** Shows all 4 chord tones in columns
- **Symbols:** Displays 7th-aware chord symbols:
  - Major 7th → `Cmaj7`
  - Minor 7th → `Dm7`
  - Half-diminished → `Bm7b5` (for viidim7 in major)
  - Augmented 7th → `Caug7`
- **Quality Adjustment:** Only applies to triads; 7th chords pass through unchanged
- **Diatonic Analysis:** 7th quality must match diatonic 7th for the degree (I7 in C Ionian is non-diatonic)

### Inversion Support

- **Parsing:** Accepts inversion suffixes `/3rd` or `/3` (first), `/5th` or `/5` (second), `/7th` or `/7` (third)
- **Building:** Rotates lowest note(s) up an octave based on inversion
- **Display:** Shows slash chord notation in chord symbols (e.g., "Cmaj7/E" for first inversion)
- **Roman Numerals:** Shows inversion suffix (e.g., "IVmaj7/3rd")

### Natural Accidental Support

- **Parsing:** Accepts `n` or `N` prefix for parallel Ionian (e.g., `nvi` in C Aeolian = A, same as VI in C Ionian)
- **Display:** Key-aware Roman numerals show `n` when appropriate (e.g., `nvi` in C Aeolian)
- **Semantics:** `n` means "use the pitch this degree would have in parallel Ionian mode"

### Key-Aware and Chord-Aware Spelling

- **Root note names:** Use proper enharmonic spelling based on key context
  - Example: In C Aeolian, degree vi is spelled "Ab" (not "G#"), and `#vi` is spelled "A" (not "Bbb")
  - Uses `TheoryPitch.GetNoteNameForDegreeWithOffset()` for key-aware spelling
- **Chord tone names:** Use canonical lookup-table-based spelling for triad tones
  - Example: bVII in C Ionian shows Bb-D-F (not A#-D-F)
  - Example: Gb major shows Gb-Bb-Db (not F#-A#-C#)
  - Uses `TheorySpelling.GetTriadSpelling()` for canonical chord tone spelling in both ChordGrid and VoicingViewer
  - Ensures musically correct enharmonic spellings via lookup table (supports major, minor, diminished, augmented triads)
  - Non-triad tones (7ths, extensions) use key-aware spelling as fallback
