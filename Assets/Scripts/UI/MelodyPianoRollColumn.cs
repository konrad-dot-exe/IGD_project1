using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EarFPS
{
    /// <summary>
    /// Individual column component for the melody piano roll.
    /// Represents one time step (quarter note) in the timeline.
    /// Phase 2a: Supports click-to-edit melody input.
    /// </summary>
    public class MelodyPianoRollColumn : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [Tooltip("Background Image component for this column (for highlighting / grouping). Must have raycastTarget enabled for click detection.")]
        [SerializeField] private Image backgroundImage;
        
        [Tooltip("RectTransform for the note bar (positioned vertically based on MIDI pitch).")]
        [SerializeField] private RectTransform noteBarRect;
        
        [Tooltip("Image component for the note bar (should be a child of noteBarRect).")]
        [SerializeField] private Image noteBarImage;

        [Header("Timeline Grouping")]
        [Tooltip("Number of steps in one visual group (e.g., 4 for 4-tick banding).")]
        [SerializeField] private int groupSize = 4;

        [Tooltip("Background color used for even-numbered groups (0,2,4,...)")]
        [SerializeField] private Color groupAColor = new Color(0.18f, 0.18f, 0.18f);

        [Tooltip("Background color used for odd-numbered groups (1,3,5,...)")]
        [SerializeField] private Color groupBColor = new Color(0.14f, 0.14f, 0.14f);

        // Internal state
        private int stepIndex;
        private int lowestMidi;
        private int highestMidi;
        private Color normalBackgroundColor;
        private Color highlightBackgroundColor;
        private Color noteBarColor;
        private int? currentMidi;

        private bool isHighlighted;
        
        // Phase 2a: Reference to parent piano roll for click callbacks
        private MelodyPianoRoll parentPianoRoll;

        /// <summary>
        /// Gets the step index (0-based quarter-note step).
        /// </summary>
        public int StepIndex => stepIndex;

        /// <summary>
        /// Initializes the column with pitch range and colors.
        /// Phase 2a: Now accepts parent piano roll reference for click callbacks.
        /// </summary>
        public void Initialize(
            int stepIndex,
            int lowestMidi,
            int highestMidi,
            Color normalBgColor,
            Color highlightBgColor,
            Color noteColor,
            MelodyPianoRoll parent = null)
        {
            this.stepIndex = stepIndex;
            this.lowestMidi = lowestMidi;
            this.highestMidi = highestMidi;
            this.normalBackgroundColor = normalBgColor;
            this.highlightBackgroundColor = highlightBgColor;
            this.noteBarColor = noteColor;
            this.parentPianoRoll = parent;

            // On init, no highlight state yet
            isHighlighted = false;

            UpdateBackgroundColor();

            // Initially hide note bar
            if (noteBarRect != null)
            {
                noteBarRect.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Hides the note bar for this column.
        /// </summary>
        public void HideNote()
        {
            SetNote(null);
        }

        /// <summary>
        /// Sets the note for this column (null = no note).
        /// </summary>
        public void SetNote(int? midi)
        {
            currentMidi = midi;

            if (midi == null || midi < lowestMidi || midi > highestMidi)
            {
                // No note or out of range: hide note bar
                if (noteBarRect != null)
                {
                    noteBarRect.gameObject.SetActive(false);
                }
                return;
            }

            // Show note bar
            if (noteBarRect != null)
            {
                noteBarRect.gameObject.SetActive(true);

                // Calculate normalized position (0 = lowestMidi, 1 = highestMidi)
                float normalized = (midi.Value - lowestMidi) / (float)(highestMidi - lowestMidi);
                normalized = Mathf.Clamp01(normalized);

                // Position the note bar vertically to align with pitch rows
                RectTransform columnRect = transform as RectTransform;
                if (columnRect != null)
                {
                    int pitchCount = highestMidi - lowestMidi + 1;
                    
                    // Calculate which row this note belongs to (0 to pitchCount-1)
                    float index = midi.Value - lowestMidi;
                    float minY = index / (float)pitchCount;
                    float maxY = (index + 1) / (float)pitchCount;
                    
                    // Center the bar in its row (use a small height, e.g., 80% of row height)
                    float barHeightRatio = 0.8f;
                    float rowHeight = (maxY - minY);
                    float barHeight = rowHeight * barHeightRatio;
                    float centerY = (minY + maxY) * 0.5f;
                    float barMinY = centerY - barHeight * 0.5f;
                    float barMaxY = centerY + barHeight * 0.5f;
                    
                    // Unity anchors: 0=left/bottom, 1=right/top
                    noteBarRect.anchorMin = new Vector2(0f, barMinY);
                    noteBarRect.anchorMax = new Vector2(1f, barMaxY);
                    noteBarRect.offsetMin = Vector2.zero;
                    noteBarRect.offsetMax = Vector2.zero;
                }

                // Set note bar color (single color for all pitches)
                if (noteBarImage != null)
                {
                    noteBarImage.color = noteBarColor;
                }
            }
        }

        /// <summary>
        /// Sets whether this column is highlighted (currently playing step).
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            UpdateBackgroundColor();
        }

        /// <summary>
        /// Computes and applies the correct background color based on
        /// grouping (every N steps) and highlight state.
        /// </summary>
        private void UpdateBackgroundColor()
        {
            if (backgroundImage == null)
                return;

            if (isHighlighted)
            {
                backgroundImage.color = highlightBackgroundColor;
                return;
            }

            // Default to normal background if grouping is disabled
            Color baseColor = normalBackgroundColor;

            if (groupSize > 0)
            {
                int groupIndex = stepIndex / groupSize;      // 0,0,0,0, 1,1,1,1, 2,2,2,2...
                bool useGroupA = (groupIndex % 2 == 0);
                baseColor = useGroupA ? groupAColor : groupBColor;
            }

            backgroundImage.color = baseColor;
        }

        /// <summary>
        /// Phase 2a: Handles pointer clicks on this column.
        /// Calculates which pitch row was clicked and notifies the parent piano roll.
        /// Requires: backgroundImage must have raycastTarget enabled.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (parentPianoRoll == null)
            {
                return; // No parent to notify
            }

            // Get the column's RectTransform
            RectTransform columnRect = transform as RectTransform;
            if (columnRect == null)
            {
                return;
            }

            // Convert click position to local coordinates
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                columnRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return; // Click was outside the column
            }

            // Calculate which pitch row was clicked based on Y position
            // Pitch rows are arranged vertically: bottom = lowestMidi, top = highestMidi
            // This matches how SetNote() positions note bars using anchor coordinates (0=bottom, 1=top)
            Rect rect = columnRect.rect;
            float localY = localPoint.y;
            
            // Normalize Y to [0, 1] range (0 = bottom of column, 1 = top)
            // rect.yMin is bottom edge, rect.yMax is top edge
            float normalizedY = (localY - rect.yMin) / rect.height;
            normalizedY = Mathf.Clamp01(normalizedY);

            // Map normalized Y to pitch row
            // normalizedY = 0 → bottom → lowestMidi (row 0)
            // normalizedY = 1 → top → highestMidi (row pitchCount-1)
            int pitchCount = highestMidi - lowestMidi + 1;
            int row = Mathf.FloorToInt(normalizedY * pitchCount);
            row = Mathf.Clamp(row, 0, pitchCount - 1);
            
            int midi = lowestMidi + row;
            
            // Clamp to valid range (should already be in range, but be safe)
            midi = Mathf.Clamp(midi, lowestMidi, highestMidi);

            // Notify parent piano roll
            parentPianoRoll.HandleCellClick(stepIndex, midi);
        }
    }
}