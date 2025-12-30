using System;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Preference for accidental spelling when converting pitch classes to note names.
    /// Used as a heuristic for generic pitch naming (not for root note spelling, which uses key-aware degree logic).
    /// </summary>
    public enum AccidentalPreference
    {
        /// <summary>
        /// Prefer flat spellings (Db, Eb, Gb, Ab, Bb)
        /// </summary>
        Flats,
        /// <summary>
        /// Prefer sharp spellings (C#, D#, F#, G#, A#)
        /// </summary>
        Sharps
    }

    /// <summary>
    /// Low-level utilities for pitch operations: MIDI ↔ pitch class conversion.
    /// Phase 1: Focuses on pitch class extraction from MIDI notes.
    /// </summary>
    public static class TheoryPitch
    {
        // Note name tables for display purposes
        private static readonly string[] SharpNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly string[] FlatNames = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

        /// <summary>
        /// Extracts the pitch class (0-11) from a MIDI note number.
        /// C = 0, C# = 1, D = 2, ..., B = 11.
        /// Handles negative MIDI values correctly.
        /// </summary>
        /// <param name="midiNote">MIDI note number (0-127 standard, but handles any int)</param>
        /// <returns>Pitch class 0-11</returns>
        public static int PitchClassFromMidi(int midiNote)
        {
            return ((midiNote % 12) + 12) % 12;
        }

        /// <summary>
        /// Gets a note name string from a MIDI note number (pitch class only, no octave).
        /// Used for display purposes in chord representations.
        /// This overload defaults to flat preference for backwards compatibility.
        /// </summary>
        /// <param name="midiNote">MIDI note number</param>
        /// <param name="preferFlats">If true, uses flat names (Db, Eb, etc.), otherwise uses sharp names (C#, D#, etc.)</param>
        /// <returns>Note name string (e.g., "C", "C#", "Db", "G")</returns>
        public static string GetPitchNameFromMidi(int midiNote, bool preferFlats = true)
        {
            int pc = PitchClassFromMidi(midiNote);
            return GetPitchNameFromPitchClass(pc, preferFlats ? AccidentalPreference.Flats : AccidentalPreference.Sharps);
        }

        /// <summary>
        /// Gets a note name string from a MIDI note number using key-aware enharmonic spelling.
        /// The accidental preference (sharps vs flats) is determined from the key context.
        /// Use this overload when displaying chord tones or bass notes in a key-aware context.
        /// </summary>
        /// <param name="midiNote">MIDI note number</param>
        /// <param name="key">The key context for determining accidental preference</param>
        /// <returns>Note name string with key-appropriate spelling (e.g., "F#" in G major, "Gb" in Db major)</returns>
        public static string GetPitchNameFromMidi(int midiNote, TheoryKey key)
        {
            int pc = PitchClassFromMidi(midiNote);
            AccidentalPreference preference = GetAccidentalPreference(key);
            return GetPitchNameFromPitchClass(pc, preference);
        }

        /// <summary>
        /// Gets a note name from a pitch class using the specified accidental preference.
        /// </summary>
        /// <param name="pc">Pitch class (0-11)</param>
        /// <param name="preference">Whether to prefer sharps or flats</param>
        /// <returns>Note name string</returns>
        private static string GetPitchNameFromPitchClass(int pc, AccidentalPreference preference)
        {
            return preference == AccidentalPreference.Sharps ? SharpNames[pc] : FlatNames[pc];
        }

        /// <summary>
        /// Determines whether a key prefers sharp or flat spellings for generic pitch class naming.
        /// Uses a hard-coded mapping based on tonic index (0-11) for consistent enharmonic spelling.
        /// For Ionian (major), the mapping is:
        /// - Sharps: C(0), D(2), E(4), G(7), A(9), B(11)
        /// - Flats: Db(1), Eb(3), F(5), Gb(6), Ab(8), Bb(10)
        /// For non-Ionian modes, the same mapping is used based on tonic index only.
        /// </summary>
        /// <param name="key">The key to analyze</param>
        /// <returns>AccidentalPreference.Sharps for keys that use sharps, Flats for keys that use flats</returns>
        public static AccidentalPreference GetAccidentalPreference(TheoryKey key)
        {
            int tonicIndex = key.TonicPitchClass; // 0..11, C=0, C#/Db=1, etc.

            switch (tonicIndex)
            {
                case 0:  // C
                case 2:  // D
                case 4:  // E
                case 7:  // G
                case 9:  // A
                case 11: // B
                    return AccidentalPreference.Sharps;

                case 1:  // Db
                case 3:  // Eb
                case 5:  // F
                case 6:  // Gb
                case 8:  // Ab
                case 10: // Bb
                    return AccidentalPreference.Flats;

                default:
                    return AccidentalPreference.Sharps; // fallback (should never happen with normalized 0-11 range)
            }
        }

        /// <summary>
        /// Checks if a note name spelling is "crazy" (contains double accidentals).
        /// Used as a sanity filter to prevent double sharps (x) or double flats (bb).
        /// </summary>
        /// <param name="name">The note name to check</param>
        /// <returns>True if the spelling contains double accidentals, false otherwise</returns>
        public static bool IsCrazySpelling(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            // Double sharp / double flat or multiple accidentals:
            if (name.Contains("x") || name.Contains("bb"))
                return true;

            int accidentalCount = 0;
            foreach (char c in name)
            {
                if (c == '#' || c == 'b')
                    accidentalCount++;
            }

            return accidentalCount > 1;
        }

        /// <summary>
        /// Gets a simple pitch name from MIDI using sharp-based spelling.
        /// No key awareness. Always sane. Use this for fallback when key-aware spelling fails.
        /// </summary>
        /// <param name="midi">MIDI note number</param>
        /// <returns>Simple note name string (e.g., "C", "C#", "D", "F#")</returns>
        public static string GetSimplePitchNameFromMidi(int midi)
        {
            // Simple sharp-based spelling. No key awareness. Always sane.
            return GetPitchNameFromMidi(midi, preferFlats: false);
        }

        /// <summary>
        /// Returns a pitch name (e.g. "C", "Db", "F#", "A") for a given scale degree
        /// in the specified key, plus an additional chromatic offset in semitones
        /// relative to that degree (e.g. -1 = flat, +1 = sharp).
        /// This uses the diatonic spelling of the degree in the key as the base,
        /// then applies accidentals.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="degree">Scale degree (1-7)</param>
        /// <param name="semitoneOffset">Chromatic offset in semitones (-1 = flat, 0 = natural, +1 = sharp, etc.)</param>
        /// <returns>Note name string with proper spelling (e.g., "C", "Ab", "A", "F#")</returns>
        public static string GetNoteNameForDegreeWithOffset(TheoryKey key, int degree, int semitoneOffset)
        {
            // Get the diatonic pitch class for this degree
            int basePc = TheoryScale.GetDegreePitchClass(key, degree);
            if (basePc < 0)
            {
                // Invalid degree - fall back to key-aware MIDI-based naming
                int fallbackMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                if (fallbackMidi >= 0)
                {
                    fallbackMidi += semitoneOffset;
                    return GetPitchNameFromMidi(fallbackMidi, key);
                }
                return "C"; // Ultimate fallback
            }

            // Calculate target pitch class
            int targetPc = (basePc + semitoneOffset) % 12;
            if (targetPc < 0) targetPc += 12;

            // Get the diatonic note name for the base degree to extract the letter
            // For C-root modes (Phase 1), the letter mapping is: 1=C, 2=D, 3=E, 4=F, 5=G, 6=A, 7=B
            string[] degreeLetters = { "C", "D", "E", "F", "G", "A", "B" };
            if (degree < 1 || degree > 7)
            {
                // Invalid degree - fall back to key-aware MIDI-based naming
                int invalidDegreeMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
                if (invalidDegreeMidi >= 0)
                {
                    invalidDegreeMidi += semitoneOffset;
                    return GetPitchNameFromMidi(invalidDegreeMidi, key);
                }
                return "C";
            }

            string baseLetter = degreeLetters[degree - 1];

            // Get the natural pitch class for this letter (C=0, D=2, E=4, F=5, G=7, A=9, B=11)
            int[] letterToNaturalPc = { 0, 2, 4, 5, 7, 9, 11 };
            int letterIndex = Array.IndexOf(degreeLetters, baseLetter);
            if (letterIndex < 0)
            {
                // Fallback if letter not found - use key-aware naming
                int fallbackMidi = (4 + 1) * 12 + targetPc;
                return GetPitchNameFromMidi(fallbackMidi, key);
            }

            int naturalPc = letterToNaturalPc[letterIndex];

            // Calculate how many semitones the target is from the natural letter
            int semitoneDiff = targetPc - naturalPc;
            if (semitoneDiff > 6) semitoneDiff -= 12; // Prefer flats for large differences
            if (semitoneDiff < -6) semitoneDiff += 12;

            // Map semitone difference to accidental string
            string accidental = semitoneDiff switch
            {
                -2 => "bb",
                -1 => "b",
                0 => "",
                1 => "#",
                2 => "x", // double sharp (could also use "##" if preferred)
                _ => "" // For extreme cases, fall back to MIDI-based naming below
            };

            // For standard cases, return letter + accidental
            if (semitoneDiff >= -2 && semitoneDiff <= 2)
            {
                string result = baseLetter + accidental;
                
                // Sanity check: if the result is "crazy" (double sharps/flats), fall back to key-aware MIDI-based naming
                if (IsCrazySpelling(result))
                {
                    // Build the actual MIDI note for this degree, then fallback to key-aware naming
                    int midi = TheoryScale.GetMidiForDegree(key, degree, 4);
                    if (midi >= 0)
                    {
                        midi += semitoneOffset; // apply the offset that was intended by 'accidental'
                        return GetPitchNameFromMidi(midi, key);
                    }
                }
                
                return result;
            }

            // Fallback for extreme cases: use key-aware MIDI-based naming
            int extremeCaseMidi = TheoryScale.GetMidiForDegree(key, degree, 4);
            if (extremeCaseMidi >= 0)
            {
                extremeCaseMidi += semitoneOffset;
                return GetPitchNameFromMidi(extremeCaseMidi, key);
            }

            return baseLetter; // Ultimate fallback
        }
    }
}

