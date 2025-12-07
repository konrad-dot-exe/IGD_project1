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
            }

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

