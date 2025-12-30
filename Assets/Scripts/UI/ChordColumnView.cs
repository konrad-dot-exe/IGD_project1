using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sonoria.MusicTheory;

namespace EarFPS
{
    /// <summary>
    /// UI component for displaying a single chord column in the Chord Lab.
    /// Shows chord symbol, 3-4 stacked note names (triad or 7th), and Roman numeral.
    /// </summary>
    public class ChordColumnView : MonoBehaviour
    {
        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI chordNameText;
        [SerializeField] private TextMeshProUGUI noteTopText;
        [SerializeField] private TextMeshProUGUI noteUpperMiddleText;
        [SerializeField] private TextMeshProUGUI noteLowerMiddleText;
        [SerializeField] private TextMeshProUGUI noteBottomText;
        [SerializeField] private TextMeshProUGUI romanText;
        [SerializeField] private TextMeshProUGUI analysisLabel;

        [Header("Styling")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color diatonicColor = Color.white;
        [SerializeField] private Color nonDiatonicColor = Color.red;
        [SerializeField] private TextMeshProUGUI statusTagText;

        private CanvasGroup canvasGroup;
        private Color originalBackgroundColor; // Store original color for tinting
        private bool originalColorStored = false;
        
        // Cached child visuals for state-based tinting
        private List<Image> childImages = new List<Image>();
        private List<Color> childImageBaseColors = new List<Color>();
        private List<TMPro.TextMeshProUGUI> childTexts = new List<TMPro.TextMeshProUGUI>();
        private List<Color> childTextBaseColors = new List<Color>();
        private bool visualsCached = false;

        /// <summary>
        /// Sets all text elements for this chord column.
        /// Supports both triads (3 notes) and 7th chords (4 notes).
        /// </summary>
        /// <param name="chordName">Chord symbol (e.g., "C", "Am", "G7", "Dm7")</param>
        /// <param name="notesTopToBottom">List of note names from highest to lowest pitch (3 or 4 elements)</param>
        /// <param name="roman">Roman numeral (e.g., "I", "vi", "V7", "ii7")</param>
        /// <param name="status">Diatonic status of the chord (Diatonic or NonDiatonic)</param>
        /// <param name="analysisInfo">Optional analysis info string for non-diatonic chords (e.g., "sec. to V", "borrowed ∥ major")</param>
        public void SetChord(string chordName, IReadOnlyList<string> notesTopToBottom, string roman, ChordDiatonicStatus status, string analysisInfo = null)
        {
            // Set chord name and Roman numeral
            if (chordNameText != null)
                chordNameText.text = chordName;
            
            if (romanText != null)
                romanText.text = roman;

            // Organize note label fields into an array for easier indexing
            // Order: top (0), upperMiddle (1), lowerMiddle (2), bottom (3)
            TextMeshProUGUI[] noteLabels = new TextMeshProUGUI[]
            {
                noteTopText,
                noteUpperMiddleText,
                noteLowerMiddleText,
                noteBottomText
            };

            int slotCount = noteLabels.Length;

            // Clear all slots first
            for (int i = 0; i < slotCount; i++)
            {
                if (noteLabels[i] != null)
                {
                    noteLabels[i].text = string.Empty;
                    noteLabels[i].gameObject.SetActive(false);
                }
            }

            // Populate note fields with bottom-alignment
            if (notesTopToBottom == null || notesTopToBottom.Count == 0)
                return;

            int noteCount = Mathf.Min(notesTopToBottom.Count, slotCount);

            // Bottom-align: fill from the bottom upwards
            // Example: 4 slots, 3 notes → indices 1,2,3 get filled; index 0 stays empty.
            for (int i = 0; i < noteCount; i++)
            {
                int srcIndex = i; // ith note in the chord (0 = highest pitch, noteCount-1 = lowest pitch)
                int destIndex = slotCount - noteCount + i; // bottom-aligned destination slot

                if (destIndex >= 0 && destIndex < slotCount && noteLabels[destIndex] != null)
                {
                    noteLabels[destIndex].text = notesTopToBottom[srcIndex];
                    noteLabels[destIndex].gameObject.SetActive(true);
                }
            }

            // Apply visual styling based on diatonic status
            if (backgroundImage != null)
            {
                backgroundImage.color = status == ChordDiatonicStatus.Diatonic
                    ? diatonicColor
                    : nonDiatonicColor;
                
                // Store original color for tinting (reset flag so it updates if SetChord is called again)
                originalBackgroundColor = backgroundImage.color;
                originalColorStored = true;
            }

            // Re-cache child visuals in case structure changed
            CacheChildVisuals();

            if (statusTagText != null)
            {
                if (status == ChordDiatonicStatus.Diatonic)
                {
                    statusTagText.text = string.Empty;
                }
                else
                {
                    statusTagText.text = "non-diatonic";
                }
            }

            // Set analysis info label (non-diatonic function description)
            if (analysisLabel != null)
            {
                analysisLabel.text = analysisInfo ?? string.Empty;
            }

            // Re-cache child visuals in case structure changed
            CacheChildVisuals();
        }

        void Awake()
        {
            CacheChildVisuals();
        }

        /// <summary>
        /// Caches all child Image and TMP_Text components for state-based tinting.
        /// Called on Awake and can be called again if structure changes.
        /// </summary>
        private void CacheChildVisuals()
        {
            childImages.Clear();
            childImageBaseColors.Clear();
            childTexts.Clear();
            childTextBaseColors.Clear();

            // Get all child Images (including inactive ones)
            Image[] allImages = GetComponentsInChildren<Image>(includeInactive: true);
            foreach (var img in allImages)
            {
                // Skip the root background image - it's handled separately
                if (img == backgroundImage)
                    continue;
                    
                childImages.Add(img);
                childImageBaseColors.Add(img.color);
            }

            // Get all child TMP_Text components (including inactive ones)
            TMPro.TextMeshProUGUI[] allTexts = GetComponentsInChildren<TMPro.TextMeshProUGUI>(includeInactive: true);
            foreach (var text in allTexts)
            {
                childTexts.Add(text);
                childTextBaseColors.Add(text.color);
            }

            visualsCached = true;
        }

        /// <summary>
        /// Multiplies RGB components of two colors, preserving the base color's alpha.
        /// </summary>
        private static Color MultiplyRGB(Color baseColor, Color tint)
        {
            return new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a // Preserve base alpha
            );
        }

        /// <summary>
        /// Visual state for chord columns during playback.
        /// </summary>
        public enum ColumnVizState
        {
            Hidden,      // Not yet reached in playback
            Visible,     // Already revealed/played
            Highlighted  // Currently playing region
        }

        /// <summary>
        /// Sets the visual state of this chord column.
        /// Uses CanvasGroup alpha to preserve layout spacing (doesn't collapse width).
        /// </summary>
        /// <param name="state">The visual state to apply</param>
        /// <param name="hiddenAlpha">Alpha value for Hidden state (0-1)</param>
        /// <param name="visibleAlpha">Alpha value for Visible state (0-1)</param>
        /// <param name="highlightedAlpha">Alpha value for Highlighted state (0-1)</param>
        /// <param name="visibleTint">Color tint for Visible state (applied to background)</param>
        /// <param name="highlightedTint">Color tint for Highlighted state (applied to background)</param>
        public void SetVizState(ColumnVizState state, float hiddenAlpha, float visibleAlpha, float highlightedAlpha, Color visibleTint, Color highlightedTint)
        {
            // Get or create CanvasGroup for alpha control
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Store original background color on first call
            if (!originalColorStored && backgroundImage != null)
            {
                originalBackgroundColor = backgroundImage.color;
                originalColorStored = true;
            }

            // Apply alpha based on state
            float targetAlpha = state switch
            {
                ColumnVizState.Hidden => hiddenAlpha,
                ColumnVizState.Visible => visibleAlpha,
                ColumnVizState.Highlighted => highlightedAlpha,
                _ => visibleAlpha
            };

            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0.01f; // Allow interaction if visible
            canvasGroup.interactable = targetAlpha > 0.01f;

            // Determine tint color based on state (Hidden uses same tint as Visible)
            Color tint = state switch
            {
                ColumnVizState.Hidden => visibleTint, // Use visible tint (alpha will be lower)
                ColumnVizState.Visible => visibleTint,
                ColumnVizState.Highlighted => highlightedTint,
                _ => Color.white
            };

            // Ensure visuals are cached
            if (!visualsCached)
            {
                CacheChildVisuals();
            }

            // Apply color tint to background image
            if (backgroundImage != null && originalColorStored)
            {
                // Multiply original color by tint to preserve diatonic/non-diatonic coloring
                backgroundImage.color = MultiplyRGB(originalBackgroundColor, tint);
            }

            // Apply tint to all child Images (note tiles, etc.)
            for (int i = 0; i < childImages.Count && i < childImageBaseColors.Count; i++)
            {
                if (childImages[i] != null)
                {
                    childImages[i].color = MultiplyRGB(childImageBaseColors[i], tint);
                }
            }

            // Apply tint to all child TMP_Text components
            for (int i = 0; i < childTexts.Count && i < childTextBaseColors.Count; i++)
            {
                if (childTexts[i] != null)
                {
                    childTexts[i].color = MultiplyRGB(childTextBaseColors[i], tint);
                }
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility. Use SetVizState() instead.
        /// </summary>
        [System.Obsolete("Use SetVizState() instead")]
        public void SetRevealed(bool revealed)
        {
            SetVizState(revealed ? ColumnVizState.Visible : ColumnVizState.Hidden, 0f, 1f, 1f, Color.white, Color.white);
        }

        /// <summary>
        /// Legacy method for backward compatibility. Use SetChord() instead.
        /// </summary>
        [System.Obsolete("Use SetChord() instead")]
        public void SetTexts(string chordName, string top, string middle, string bottom, string roman)
        {
            var notes = new List<string> { top, middle, bottom };
            SetChord(chordName, notes, roman, ChordDiatonicStatus.Diatonic);
        }
    }
}

