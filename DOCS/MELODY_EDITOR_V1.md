# Melody Editor V1 Specification Document

## 1. **Purpose & Goals**

Add a simple, monophonic piano-roll style melody editor to Chord Lab that:

- Makes melody input much more intuitive (especially for other users).
- Integrates cleanly with the existing melody timeline and text-based input.
- Maintains deterministic, timeline-based playback and existing MelodyEvent logic.
- Is modular and reversible (can be disabled / removed without breaking core functionality).
- We are aiming for “Mario Paint level” simplicity, not a DAW.

## 2. **Scope & Non-Goals**

In scope (v1)

- Monophonic grid-based melody editor (one note per step).
- Shared timebase with existing Timeline v1 (same step resolution as current melody timeline).

- **Conversion**:

  Text → MelodyEvents → Piano Roll grid
  Piano Roll grid → MelodyEvents → Text
  Visual sync with playback:
  Highlighted step indicator aligned with existing voicing viewer / autoscroll.
  Support for durations via contiguous active cells.

- **Out of scope** (for v1)

  Polyphonic melody (stacked notes per step).
  Allow optional gaps (silence) in melody, implemented as “no MelodyEvent over those ticks.”
  Drag-resizing note durations.
  Mouse drag to draw lines of notes.
  Velocity editing per note.
  Advanced tools (selection, copy/paste, zoom, undo/redo).
  Multiple lanes / multi-instrument editing.
  Any changes to theory engine, Timeline v1 semantics, or SATB voicing logic.

## 3. **High-Level UX**

- **What the user sees**

  A horizontal grid above or near the voicing viewer:
  X-axis = time (steps / ticks)
  Y-axis = pitch rows (e.g. a chosen range around typical soprano/melody register)
  Colored rectangles/cells marking where the melody sounds.
  A playback highlight column that moves in sync with the existing voicing viewer highlight and autoscroll.
  The existing Melody Text Input still present and functional.

- **What the user does**

- Click on a cell:

  If the column has no note → creates a note at that pitch.
  If the column already has a note at a different pitch → moves the note to the clicked pitch.
  If the column already has a note at that pitch → removes it.

- Adjusting the grid will:

  Update the internal melody data.
  Regenerate the melody text string to keep it in sync.

## 4. **Source of Truth & Data Flow**

- **Single source of truth: Melody timeline events**
- We’ll treat the MelodyEvent timeline (what Timeline v1 already uses) as the canonical representation of melody, not the UI.
- Text edit → parse → update MelodyEvents → repaint piano roll.
- Piano-roll edit → update MelodyEvents → regenerate text.

- In practice:

  Text → Events:
  Already implemented in your current system (text like C4:4 A4:2... → list of MelodyEvents with startTick, durationTicks, midi).
  Events → Grid (for display):
  New: fill a monophonic 2D boolean array (pitch × step) for the piano roll.
  Grid → Events (after user edit):
  New: scan grid left-to-right to reconstruct monophonic MelodyEvents (with durations).
  Events → Text:
  Use your existing serialization logic to write back into the melody text field.
  This keeps all logic consistent with your existing engine and avoids duplicated timing semantics.

## 5. **Timeline & Resolution**

Use the existing tick resolution already defined by Timeline v1:
Same ticksPerQuarter / steps-per-region logic as voicing viewer.

Let:

- totalSteps = number of timeline steps (quarters or sub-quarters) used for SATB + melody visualization.
- Piano roll columns must line up 1:1 with these steps:
- Column i in the piano roll corresponds exactly to step i in playback and voicing viewer highlight.
- No new timebase is introduced.

## 6. **Pitch Range & Grid Size**

For v1:

    - Vertical axis = a fixed pitch range around typical melodic register, for example:
    - MIDI 60–79 (C4 to G5) or a small configurable range.

This can be:

    - Configured as serialized fields: lowestMidi, highestMidi.
    - Or hard-coded for now and exposed later if needed.

Grid size:

    - Columns = totalSteps.
    - Rows = pitchRows = highestMidi - lowestMidi + 1.

## 7. **UI Architecture** (Unity)

New component: MelodyPianoRoll

    Create a new MonoBehaviour (and prefab) something like:

    public class MelodyPianoRoll : MonoBehaviour
    {
    // References to UI elements (grid container, cell prefab, etc.)
    // API:
    // - RenderFromEvents(List<MelodyEvent>)
    // - List<MelodyEvent> BuildEventsFromGrid()
    // - OnCellClicked(int step, int midi)
    }

Placement:

    Likely anchor under the main Chord Lab panel, above the voicing viewer:
    Panel_ChordLab/MainContainer/MelodyPianoRoll
    For v1, it’s okay if the piano roll has its own viewport/ScrollRect or shares the same horizontal scroll with voicing viewer (second option is ideal later; first is simpler now).

Integration points:

    ChordLabController:

        When melody text changes / is parsed:
        Update MelodyEvent list and call MelodyPianoRoll.RenderFromEvents(events).

    When piano roll changes:

        events = pianoRoll.BuildEventsFromGrid()
        Update internal melody timeline + regenerate text.

## 8. **Grid Data Model**

Internally in MelodyPianoRoll, maintain:

    int lowestMidi;
    int highestMidi;
    int totalSteps;

    // monophonic: at most one MIDI per step
    int?[] midiAtStep; // length = totalSteps, holds either null (no note) or midi value

No need for a full 2D bool array if we are monophonic and we just store one midi per step; the grid’s visual layer consults midiAtStep[step] when painting.

## 9. **Events → Grid Algorithm**

Given a list of MelodyEvents (assumed monophonic / non-overlapping):

For each MelodyEvent ev:

    Let start = ev.startTick
    Let end = ev.startTick + ev.durationTicks (exclusive)
    Let m = ev.midi (clamped to [lowestMidi, highestMidi] if out-of-range).

For every step t in [start, end):

    if (t < 0 || t >= totalSteps) skip;
    midiAtStep[t] = m;

This will create a run of identical midi values in midiAtStep corresponding to the duration of the note.

## 10. **Grid → Events Algorithm** (Monophonic, with durations)

We scan midiAtStep left-to-right once.

State:

    int? currentMidi = null;
    int currentStart = 0;

Algorithm:

    for (int t = 0; t < totalSteps; t++)
    {
    int? m = midiAtStep[t];

        if (currentMidi == null)
        {
            if (m != null)
            {
                // start a new note
                currentMidi = m;
                currentStart = t;
            }
        }
        else // currentMidi != null
        {
            if (m == currentMidi)
            {
                // sustain
                continue;
            }
            else
            {
                // note ended at t
                events.Add(new MelodyEvent {
                    midi = currentMidi.Value,
                    startTick = currentStart,
                    durationTicks = t - currentStart
                });

                currentMidi = null;

                if (m != null)
                {
                    // new note starts immediately
                    currentMidi = m;
                    currentStart = t;
                }
            }
        }

    }

    // End of timeline: close final note if any
    if (currentMidi != null)
    {
    events.Add(new MelodyEvent {
    midi = currentMidi.Value,
    startTick = currentStart,
    durationTicks = totalSteps - currentStart
    });
    }

Rules implied:

- Back-to-back same pitch across steps → a single longer note.
- Change of pitch → closes previous note and starts a new one.
- Gaps (null between notes) → notes should sustain during gaps for now (no rests in V1).
- This matches the “no re-articulation vs sustain distinction at smallest step” compromise we agreed on.

## 11. **Click Behavior** (Monophonic rules)

For a click on cell (step t, pitch m):

    Determine the current note at that step:
    int? existing = midiAtStep[t];

Cases:

No note at this step (existing == null):
Set midiAtStep[t] = m;
(Optionally: immediately update the grid visuals + optionally preview sound.)

Same note at this step (existing == m):
Remove it: midiAtStep[t] = null;
This may shorten or split a run when we later rebuild events.

Different note at this step (existing != null && existing != m):
Move the note: midiAtStep[t] = m;

After a click:

    Rebuild MelodyEvent list via the scan algorithm.
    Regenerate melody text string from events.
    Notify the rest of the system (Timeline v1) that melody changed.
    Re-render the piano roll (or just update the relevant column’s visuals).
    We do not directly manage durations on click; durations emerge naturally from contiguous steps when we regenerate events.

## 12. **Visual Representation**

Each step is a column that can be visually highlighted and scrolled (similar to voicing viewer).

We can implement this in several ways; simplest v1:
Use a rectangular grid of Image or Button objects inside a GridLayoutGroup, with: - constraint = FixedColumnCount = totalSteps OR - FixedRowCount = pitchRows, depending on orientation.

    However, 100% interactive cell GameObjects per step per pitch could be heavy for large grids.

Alternative low-cost v1:

    Each column = one UI element (e.g. a VerticalLayoutGroup or custom script) that:
        draws an “active note bar” (Image) at the proper vertical position based on midiAtStep[t].

    Handling the click means raycasting within this column rect to find which pitch row was clicked.

We can choose the simpler to implement for now; the spec doesn’t force an approach, but we keep it monophonic + column-based either way.

## 13. **Playback Highlight Integration**

Piano roll should share the same highlighted step index as voicing viewer.

When VoicingViewer.SetHighlightedStep(stepIndex) is called, the piano roll should also receive that index and:

    visually highlight the vertical band for that step (e.g. by tinting background or drawing a highlight line).

This keeps the user’s eye anchored across VOICES + MELODY.

## 14. **Phase Plan** (for safe incremental implementation)

Phase 1 – Read-only visual roll

    Implement MelodyPianoRoll.RenderFromEvents(events):

    Convert events → midiAtStep → draw static grid / bars.

    Hook it to existing melody parsing (text → events).

    Sync playback highlight and scroll (but no editing yet).

Phase 2 – Click editing

    Add click handling:

    Update midiAtStep per click.

    Rebuild events from midiAtStep.

    Regenerate text and notify the timeline.

    Test monophonic and duration behavior.

Phase 3 – Polish (optional)

    Visual feedback for hover.

    Basic constraints (limit pitch to a toggleable range).

    Minor aesthetic tuning.

## 15. **Known Limitations** (Conscious Tradeoffs)

Cannot distinguish two immediate short notes of same pitch from a single longer note at that quantization resolution.

No manual control of duration beyond the “runs of steps” approach.

Melody is strictly monophonic (by design).

No explicit rest tokens, but silent gaps are supported via absence of events.

These are accepted constraints for v1 in exchange for simplicity and robustness.
