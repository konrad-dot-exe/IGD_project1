using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sonoria.MusicTheory.Timeline;

namespace EarFPS
{
    /// <summary>
    /// Piano roll display for monophonic melody timeline.
    /// Phase 1: Display only, no editing.
    /// Phase 2a: Click-to-edit melody input (local editing only, no timeline rebuild yet).
    /// Phase 2b: Grid → MelodyEvents conversion (BuildEventsFromGrid, GetCurrentMelodyEvents).
    /// </summary>
    public class MelodyPianoRoll : MonoBehaviour
    {
        [Header("Pitch Range")]
        [Tooltip("Lowest MIDI note to display (inclusive).")]
        [SerializeField] private int lowestMidi = 60; // C4
        
        [Tooltip("Highest MIDI note to display (inclusive).")]
        [SerializeField] private int highestMidi = 79; // G5

        [Header("UI References")]
        [Tooltip("Container for pitch background rows. Should be stretched to fill the viewport.")]
        [SerializeField] private RectTransform pitchBackgroundContainer;
        
        [Tooltip("Container for column GameObjects. Should have HorizontalLayoutGroup component.")]
        [SerializeField] private Transform columnsContainer;
        
        [Tooltip("Prefab for individual column GameObjects. Must have MelodyPianoRollColumn component.")]
        [SerializeField] private GameObject columnPrefab;

        [Header("Scroll Sync")]
        [SerializeField] private ScrollRect pianoRollScrollRect;
        [SerializeField] private ScrollRect voicingScrollRect;

        private bool isSyncingFromVoicing = false;

        [Header("Styling")]
        [Tooltip("Background color for normal (non-highlighted) columns.")]
        [SerializeField] private Color normalBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        [Tooltip("Background color for highlighted (currently playing) columns.")]
        [SerializeField] private Color highlightBackgroundColor = new Color(1f, 1f, 0.7f, 1f);

        [Tooltip("Color for note bars (all pitches use this single color).")]
        [SerializeField] private Color noteBarColor = new Color(0.5f, 0.8f, 1f, 1f); // Light cyan

        [Tooltip("Background color for white key pitch rows (C, D, E, F, G, A, B).")]
        [SerializeField] private Color whiteKeyRowColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Light gray
        
        [Tooltip("Background color for black key pitch rows (C#, D#, F#, G#, A#).")]
        [SerializeField] private Color blackKeyRowColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Darker gray

        // Internal state
        private int totalSteps = 0;
        private int?[] midiAtStep; // One MIDI value per step, null = no note
        private int currentHighlightedStep = -1;
        private List<MelodyPianoRollColumn> columnInstances = new List<MelodyPianoRollColumn>();
        private TimelineSpec timelineSpec;

        [Header("Debug")]
        [Tooltip("Enable debug logging for click handling and editing.")]
        [SerializeField] private bool enableDebugLogs = false;
        
        [Tooltip("Log MelodyEvents whenever the grid changes (for testing Phase-2b).")]
        [SerializeField] private bool logEventsOnChange = false;

        // Phase 2b: Scratch list for building events (reused to avoid allocations)
        private List<MelodyEvent> _scratchEvents;

        /// <summary>
        /// Renders the piano roll from a list of timeline melody events.
        /// Converts Timeline.MelodyEvent (tick-based) to quarter-note step grid.
        /// </summary>
        /// <param name="events">List of timeline melody events</param>
        /// <param name="totalSteps">Total number of quarter-note steps (must match voicing viewer)</param>
        /// <param name="spec">Timeline specification for tick-to-step conversion</param>
        public void RenderFromEvents(
            List<Sonoria.MusicTheory.Timeline.MelodyEvent> events, 
            int totalSteps,
            TimelineSpec spec)
        {
            this.totalSteps = totalSteps;
            this.timelineSpec = spec;

            // Initialize or resize midiAtStep array
            if (midiAtStep == null || midiAtStep.Length != totalSteps)
            {
                midiAtStep = new int?[totalSteps];
            }
            else
            {
                System.Array.Clear(midiAtStep, 0, totalSteps);
            }

            // Convert events to onset grid (only mark start ticks, not full duration)
            if (events != null && events.Count > 0 && spec != null && spec.ticksPerQuarter > 0)
            {
                int ticksPerQuarter = spec.ticksPerQuarter;

                foreach (var ev in events)
                {
                    // Convert tick-based event start to quarter-note step
                    int startStep = ev.startTick / ticksPerQuarter;

                    // Clamp to valid range
                    startStep = Mathf.Clamp(startStep, 0, totalSteps - 1);

                    // Clamp MIDI to display range
                    int midi = Mathf.Clamp(ev.midi, lowestMidi, highestMidi);

                    // Onset grid: only mark the start tick
                    midiAtStep[startStep] = midi;
                }
            }

            // Rebuild pitch background rows
            RebuildPitchBackground();
            
            // Rebuild visual columns
            RebuildColumns();
            
            // Redraw bars from events (not from midiAtStep continuity)
            RedrawFromEvents(events);
        }

        /// <summary>
        /// Sets the highlighted step index (0-based). Use -1 to clear highlight.
        /// Must match the step index used by VoicingViewer.SetHighlightedStep.
        /// </summary>
        public void SetHighlightedStep(int stepIndex)
        {
            currentHighlightedStep = stepIndex;

            // Update all columns
            for (int i = 0; i < columnInstances.Count; i++)
            {
                var column = columnInstances[i];
                if (column != null)
                {
                    bool isHighlighted = (column.StepIndex == stepIndex && stepIndex >= 0);
                    column.SetHighlighted(isHighlighted);
                }
            }
        }

        /// <summary>
        /// Clears the piano roll display.
        /// </summary>
        public void Clear()
        {
            totalSteps = 0;
            midiAtStep = null;
            currentHighlightedStep = -1;
            
            // Clear pitch background rows
            if (pitchBackgroundContainer != null)
            {
                for (int i = pitchBackgroundContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(pitchBackgroundContainer.GetChild(i).gameObject);
                }
            }
            
            // Clear column instances
            if (columnsContainer != null)
            {
                foreach (Transform child in columnsContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            columnInstances.Clear();
        }

        /// <summary>
        /// Rebuilds all column visuals based on current midiAtStep array.
        /// </summary>
        private void RebuildColumns()
        {
            if (columnsContainer == null)
            {
                Debug.LogError("[MelodyPianoRoll] columnsContainer is not assigned!");
                return;
            }

            if (columnPrefab == null)
            {
                Debug.LogError("[MelodyPianoRoll] columnPrefab is not assigned!");
                return;
            }

            // Clear existing columns
            foreach (Transform child in columnsContainer)
            {
                Destroy(child.gameObject);
            }
            columnInstances.Clear();

            if (midiAtStep == null || totalSteps <= 0)
            {
                return; // Nothing to render
            }

            // Create columns for each step
            for (int step = 0; step < totalSteps; step++)
            {
                // Instantiate column prefab
                GameObject columnObj = Instantiate(columnPrefab, columnsContainer);
                MelodyPianoRollColumn columnScript = columnObj.GetComponent<MelodyPianoRollColumn>();

                if (columnScript == null)
                {
                    Debug.LogError($"[MelodyPianoRoll] Column prefab at step {step} is missing MelodyPianoRollColumn component!");
                    Destroy(columnObj);
                    continue;
                }

                // Initialize column (Phase 2a: pass parent reference for click callbacks)
                columnScript.Initialize(step, lowestMidi, highestMidi, normalBackgroundColor, highlightBackgroundColor, noteBarColor, this);

                // Don't set note here - notes are set by RedrawFromEvents() after columns are created
                // Initially hide note bar
                columnScript.HideNote();

                // Set initial highlight state
                bool isHighlighted = (step == currentHighlightedStep && currentHighlightedStep >= 0);
                columnScript.SetHighlighted(isHighlighted);

                columnInstances.Add(columnScript);
            }
        }

        /// <summary>
        /// Rebuilds the pitch background rows showing black/white key distinction.
        /// Creates horizontal stripes for each pitch in the range.
        /// </summary>
        private void RebuildPitchBackground()
        {
            if (pitchBackgroundContainer == null)
            {
                return;
            }

            // Clear old rows
            for (int i = pitchBackgroundContainer.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(pitchBackgroundContainer.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(pitchBackgroundContainer.GetChild(i).gameObject);
                }
            }

            int pitchCount = highestMidi - lowestMidi + 1;
            if (pitchCount <= 0)
            {
                return;
            }

            // Create a row for each pitch
            for (int midi = lowestMidi; midi <= highestMidi; midi++)
            {
                GameObject rowGO = new GameObject($"PitchRow_{midi}", typeof(RectTransform), typeof(Image));
                rowGO.transform.SetParent(pitchBackgroundContainer, false);

                RectTransform rowRect = rowGO.GetComponent<RectTransform>();
                Image img = rowGO.GetComponent<Image>();

                // Decide color based on black/white key
                bool isBlack = IsBlackKey(midi);
                img.color = isBlack ? blackKeyRowColor : whiteKeyRowColor;

                // Stretch horizontally, slice vertically as a fraction of total height
                float index = midi - lowestMidi; // 0..pitchCount-1
                float minY = index / (float)pitchCount;
                float maxY = (index + 1) / (float)pitchCount;

                // Unity anchors: 0=bottom, 1=top (no inversion needed)
                rowRect.anchorMin = new Vector2(0f, minY);
                rowRect.anchorMax = new Vector2(1f, maxY);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// Determines if a MIDI note is a black key (C#, D#, F#, G#, A#).
        /// </summary>
        private bool IsBlackKey(int midi)
        {
            int pitchClass = midi % 12;
            // Black keys: C#(1), D#(3), F#(6), G#(8), A#(10)
            return pitchClass == 1 || pitchClass == 3 || pitchClass == 6 || pitchClass == 8 || pitchClass == 10;
        }

        // Called by the voicing ScrollRect's OnValueChanged
        public void SyncFromVoicing(Vector2 normalizedPos)
        {
            if (isSyncingFromVoicing) return; // guard against loops if needed

            if (pianoRollScrollRect == null) return;

            isSyncingFromVoicing = true;
            pianoRollScrollRect.horizontalNormalizedPosition = normalizedPos.x;
            isSyncingFromVoicing = false;
        }

        /// <summary>
        /// Phase 2a: Handles a cell click from a column.
        /// Simple onset-only editing: only touches midiAtStep[stepIndex].
        /// Does NOT scan events or compute event spans.
        /// </summary>
        /// <param name="stepIndex">The step index (0-based quarter-note step)</param>
        /// <param name="midi">The MIDI pitch that was clicked</param>
        public void HandleCellClick(int stepIndex, int midi)
        {
            // Bounds checks
            if (stepIndex < 0 || stepIndex >= totalSteps)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MelodyPianoRoll] Click on invalid step index: {stepIndex} (totalSteps={totalSteps})");
                return;
            }

            if (midi < lowestMidi || midi > highestMidi)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MelodyPianoRoll] Click on invalid MIDI: {midi} (range=[{lowestMidi}, {highestMidi}])");
                return;
            }

            // Ensure midiAtStep array is valid and properly sized
            if (midiAtStep == null || midiAtStep.Length != totalSteps)
            {
                midiAtStep = new int?[totalSteps];
            }

            // Get existing onset at this step
            int? existing = midiAtStep[stepIndex];

            // Simple onset grid logic: only touch midiAtStep[stepIndex]
            if (existing == null)
            {
                // No onset here yet → place one
                midiAtStep[stepIndex] = midi;
            }
            else if (existing == midi)
            {
                // Same pitch → toggle off (delete this onset)
                midiAtStep[stepIndex] = null;
            }
            else
            {
                // Different pitch → change the onset's pitch
                midiAtStep[stepIndex] = midi;
            }

            // Debug logging
            if (enableDebugLogs)
            {
                string result = midiAtStep[stepIndex]?.ToString() ?? "null";
                Debug.Log($"[MelodyPianoRoll] Click step={stepIndex} midi={midi} (existing={existing?.ToString() ?? "null"}) -> {result}");
            }

            // Rebuild events from the onset grid and redraw
            var events = BuildEventsFromGrid();
            RedrawFromEvents(events);
            OnGridChanged();
        }

        /// <summary>
        /// Redraws visual tiles from MelodyEvents - shows only onset tiles, not full-duration bars.
        /// Each event draws a single tile at its start step.
        /// </summary>
        /// <param name="events">List of MelodyEvents to render. If null, clears all tiles.</param>
        private void RedrawFromEvents(List<MelodyEvent> events)
        {
            // Clear all columns first
            if (columnInstances != null)
            {
                foreach (var column in columnInstances)
                {
                    if (column != null)
                    {
                        column.HideNote();
                    }
                }
            }

            if (events == null || events.Count == 0)
            {
                return; // No events to render
            }

            if (timelineSpec == null || timelineSpec.ticksPerQuarter <= 0)
            {
                return; // Can't convert ticks to steps
            }

            int ticksPerQuarter = timelineSpec.ticksPerQuarter;

            // For each event, show only a tile at its onset step
            foreach (var ev in events)
            {
                // Convert tick-based start to step
                int startStep = ev.startTick / ticksPerQuarter;
                
                if (startStep < 0 || startStep >= columnInstances.Count)
                {
                    continue; // Out of range
                }

                var column = columnInstances[startStep];
                if (column != null)
                {
                    // Draw a single tile at the onset
                    column.SetNote(ev.midi);
                }
            }
        }

        /// <summary>
        /// Phase 2a: Redraws a single column's note bar based on current midiAtStep[stepIndex] value.
        /// Note: This is now deprecated in favor of RedrawFromEvents, but kept for compatibility.
        /// </summary>
        /// <param name="stepIndex">The step index to redraw</param>
        private void RedrawColumn(int stepIndex)
        {
            // Rebuild events and redraw from events instead of using midiAtStep directly
            var events = BuildEventsFromGrid();
            RedrawFromEvents(events);
        }

        /// <summary>
        /// Phase 2a: Redraws all columns from the current midiAtStep[] array.
        /// Note: This is now deprecated in favor of RedrawFromEvents, but kept for compatibility.
        /// </summary>
        private void RedrawAllColumnsFromMidiAtStep()
        {
            // Rebuild events and redraw from events instead of using midiAtStep directly
            var events = BuildEventsFromGrid();
            RedrawFromEvents(events);
        }

        /// <summary>
        /// Phase 2b: Builds a list of MelodyEvents from the current midiAtStep[] onset grid.
        /// Uses onset-based spacing: each non-null entry is an onset, duration = distance to next onset.
        /// Works in step space, then converts to ticks using timelineSpec.ticksPerQuarter.
        /// </summary>
        /// <returns>List of MelodyEvents representing the current grid state. Returns empty list if grid is invalid.</returns>
        public List<MelodyEvent> BuildEventsFromGrid()
        {
            var result = new List<MelodyEvent>();

            // Handle edge cases
            if (midiAtStep == null || midiAtStep.Length == 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[MelodyPianoRoll] BuildEventsFromGrid: midiAtStep is null or empty");
                return result;
            }

            if (totalSteps <= 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[MelodyPianoRoll] BuildEventsFromGrid: totalSteps <= 0");
                return result;
            }

            // If timelineSpec is null, we can't convert steps to ticks, so return empty
            if (timelineSpec == null || timelineSpec.ticksPerQuarter <= 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[MelodyPianoRoll] BuildEventsFromGrid: timelineSpec is null or invalid (ticksPerQuarter={timelineSpec?.ticksPerQuarter ?? 0})");
                return result;
            }

            int ticksPerQuarter = timelineSpec.ticksPerQuarter;

            // Collect all onset steps (where midiAtStep[step] != null)
            var onsetSteps = new List<int>();
            for (int step = 0; step < totalSteps; step++)
            {
                if (step < midiAtStep.Length && midiAtStep[step].HasValue)
                {
                    onsetSteps.Add(step);
                }
            }

            if (onsetSteps.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[MelodyPianoRoll] BuildEventsFromGrid: no onsets found");
                return result;
            }

            // Build events: duration = distance to next onset (or totalSteps if last)
            // Work in step space, then convert to ticks
            for (int i = 0; i < onsetSteps.Count; i++)
            {
                int startStep = onsetSteps[i];
                int endStep = (i + 1 < onsetSteps.Count) ? onsetSteps[i + 1] : totalSteps;
                int durSteps = Mathf.Max(1, endStep - startStep);

                int midi = midiAtStep[startStep].Value;

                result.Add(new MelodyEvent
                {
                    midi = midi,
                    startTick = startStep * ticksPerQuarter,
                    durationTicks = durSteps * ticksPerQuarter
                });
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[MelodyPianoRoll] BuildEventsFromGrid: built {result.Count} events from {onsetSteps.Count} onsets (totalSteps={totalSteps})");
            }

            return result;
        }

        /// <summary>
        /// Phase 2b: Gets the current melody events as a read-only list.
        /// Builds events fresh from the grid each time (no caching yet).
        /// </summary>
        /// <returns>Read-only list of current MelodyEvents, or empty array if grid is invalid.</returns>
        public IReadOnlyList<MelodyEvent> GetCurrentMelodyEvents()
        {
            if (midiAtStep == null || totalSteps <= 0)
            {
                return System.Array.Empty<MelodyEvent>();
            }

            // Build fresh from grid
            _scratchEvents ??= new List<MelodyEvent>();
            _scratchEvents.Clear();
            _scratchEvents.AddRange(BuildEventsFromGrid());
            return _scratchEvents;
        }

        /// <summary>
        /// Phase 2b: Optional callback when grid changes (for testing/debugging).
        /// Logs the current MelodyEvents if logEventsOnChange is enabled.
        /// </summary>
        private void OnGridChanged()
        {
            if (!logEventsOnChange)
            {
                return;
            }

            var events = BuildEventsFromGrid();
            Debug.Log($"[MelodyPianoRoll] Grid changed -> {events.Count} MelodyEvents");
            
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                Debug.Log($"    MelodyEvent[{i}] midi={ev.midi} startTick={ev.startTick} durationTicks={ev.durationTicks}");
            }
        }
    }
}

