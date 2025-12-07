using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Sonoria.MusicTheory;

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

        [Header("Voice-Leading Diagnostics")]
        [Tooltip("Semitone distance at or above which a voice movement is highlighted as a large leap.")]
        [SerializeField] private int largeLeapSemitoneThreshold = 5;

        // Accumulator strings for building up the sequence of voicings
        private string bassLine = string.Empty;
        private string tenorLine = string.Empty;
        private string altoLine = string.Empty;
        private string sopranoLine = string.Empty;

        /// <summary>
        /// Last chord's sorted MIDI notes (bass to soprano) used for leap detection.
        /// </summary>
        private List<int> previousChordSortedMidi = null;

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
        public void ShowVoicing(TheoryKey key, int stepIndex, int totalSteps, IReadOnlyList<int> midiNotes, ChordEvent? chordEvent = null)
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
                bass = midiNotes.Count > 0 ? NoteNameWithOctave(midiNotes[0], key, chordEvent) : "(none)";
                tenor = midiNotes.Count > 1 ? NoteNameWithOctave(midiNotes[1], key, chordEvent) : "(none)";
                alto = midiNotes.Count > 2 ? NoteNameWithOctave(midiNotes[2], key, chordEvent) : "(none)";
                soprano = midiNotes.Count > 3 ? NoteNameWithOctave(midiNotes[3], key, chordEvent) : "(none)";

                // Wrap tokens in red color tags if they are marked as large leaps.
                bass = ColorIfLeap(bass, largeLeapFlags[0]);
                tenor = ColorIfLeap(tenor, largeLeapFlags[1]);
                alto = ColorIfLeap(alto, largeLeapFlags[2]);
                soprano = ColorIfLeap(soprano, largeLeapFlags[3]);

                // Store current chord as previous for the next step (keep original order).
                previousChordSortedMidi = new List<int>(midiNotes);
            }

            // Append current notes to accumulator strings
            bassLine = AppendToken(bassLine, bass);
            tenorLine = AppendToken(tenorLine, tenor);
            altoLine = AppendToken(altoLine, alto);
            sopranoLine = AppendToken(sopranoLine, soprano);

            // Update TMP fields with accumulated lines
            SetVoiceText(bassText, bassLine);
            SetVoiceText(tenorText, tenorLine);
            SetVoiceText(altoText, altoLine);
            SetVoiceText(sopranoText, sopranoLine);
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

            // Reset previous chord storage
            previousChordSortedMidi = null;

            if (headerText != null)
            {
                headerText.text = "Current Voicing";
            }

            SetVoiceText(bassText, string.Empty);
            SetVoiceText(tenorText, string.Empty);
            SetVoiceText(altoText, string.Empty);
            SetVoiceText(sopranoText, string.Empty);
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

            // Get canonical triad spelling, using RootSemitoneOffset for enharmonic disambiguation
            string[] triadNames = TheorySpelling.GetTriadSpelling(rootPc, chord.Recipe.Quality, chord.Recipe.RootSemitoneOffset);
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
                // Get canonical 7th chord spelling (root, 3rd, 5th, 7th)
                string[] seventhChordNames = TheorySpelling.GetSeventhChordSpelling(
                    rootPc, 
                    chord.Recipe.Quality, 
                    chord.Recipe.SeventhQuality, 
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

            // Add spacing between tokens (three spaces for readability)
            return existing + "   " + token;
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

