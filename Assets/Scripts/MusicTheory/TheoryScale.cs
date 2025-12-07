using System;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Provides scale-related operations: pitch classes, degrees, and scale membership.
    /// Encapsulates the mode → interval pattern → pitch class mapping.
    /// </summary>
    public static class TheoryScale
    {
        // Interval step patterns for each mode (semitones from one degree to the next)
        // These patterns wrap around (7 steps for 7 degrees, leading back to tonic)
        private static readonly int[] IonianSteps = { 2, 2, 1, 2, 2, 2, 1 };
        private static readonly int[] DorianSteps = { 2, 1, 2, 2, 2, 1, 2 };
        private static readonly int[] PhrygianSteps = { 1, 2, 2, 2, 1, 2, 2 };
        private static readonly int[] LydianSteps = { 2, 2, 2, 1, 2, 2, 1 };
        private static readonly int[] MixolydianSteps = { 2, 2, 1, 2, 2, 1, 2 };
        private static readonly int[] AeolianSteps = { 2, 1, 2, 2, 1, 2, 2 };
        private static readonly int[] LocrianSteps = { 1, 2, 2, 1, 2, 2, 2 };

        /// <summary>
        /// Gets the interval step pattern for the given mode.
        /// </summary>
        private static int[] GetStepPattern(ScaleMode mode)
        {
            return mode switch
            {
                ScaleMode.Ionian => IonianSteps,
                ScaleMode.Dorian => DorianSteps,
                ScaleMode.Phrygian => PhrygianSteps,
                ScaleMode.Lydian => LydianSteps,
                ScaleMode.Mixolydian => MixolydianSteps,
                ScaleMode.Aeolian => AeolianSteps,
                ScaleMode.Locrian => LocrianSteps,
                _ => IonianSteps
            };
        }

        /// <summary>
        /// Returns the 7 pitch classes (0-11) that belong to the diatonic scale of the key.
        /// Returns the pitch classes starting from the key's tonic.
        /// </summary>
        /// <param name="key">The key to get pitch classes for</param>
        /// <returns>Array of 7 pitch classes in ascending order</returns>
        public static int[] GetDiatonicPitchClasses(TheoryKey key)
        {
            int[] pitchClasses = new int[7];
            for (int i = 0; i < pitchClasses.Length; i++)
            {
                pitchClasses[i] = DegreeToPitchClass(key, i);
            }
            return pitchClasses;
        }

        /// <summary>
        /// Canonical helper to convert a degree index (0-based) into a pitch class for the given key.
        /// Degree 0 = tonic, 1 = supertonic, ..., 6 = leading tone/subtonic.
        /// </summary>
        /// <param name="key">The key (tonic + mode)</param>
        /// <param name="degreeIndex">0-based scale degree index</param>
        /// <returns>Pitch class 0-11, or -1 if index is outside 0..6</returns>
        public static int DegreeToPitchClass(TheoryKey key, int degreeIndex)
        {
            if (degreeIndex < 0 || degreeIndex > 6)
            {
                return -1;
            }

            int[] steps = GetStepPattern(key.Mode);
            int tonic = TheoryKeyUtils.GetTonicPitchClass(key);

            int pitchClass = tonic;
            for (int i = 0; i < degreeIndex; i++)
            {
                pitchClass = (pitchClass + steps[i]) % 12;
            }

            return pitchClass;
        }

        /// <summary>
        /// Legacy 1-based wrapper around <see cref="DegreeToPitchClass(TheoryKey,int)"/>.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="degree">Scale degree (1 = tonic, 2 = supertonic, ..., 7 = leading tone/subtonic)</param>
        /// <returns>Pitch class 0-11, or -1 if degree is out of range</returns>
        public static int GetDegreePitchClass(TheoryKey key, int degree)
        {
            // Legacy wrapper (1-based); delegate to canonical helper
            return DegreeToPitchClass(key, degree - 1);
        }

        /// <summary>
        /// Returns true if the given MIDI note is diatonic (belongs to) the scale.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="midiNote">MIDI note number</param>
        /// <returns>True if the note's pitch class is in the scale</returns>
        public static bool IsNoteInScale(TheoryKey key, int midiNote)
        {
            int pc = TheoryPitch.PitchClassFromMidi(midiNote);
            int[] scalePcs = GetDiatonicPitchClasses(key);
            
            foreach (int scalePc in scalePcs)
            {
                if (scalePc == pc)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Computes the MIDI note for a given scale degree and octave.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="degree">Scale degree (1-7)</param>
        /// <param name="octave">Octave number (MIDI standard: C4 = octave 4, MIDI 60)</param>
        /// <returns>MIDI note number, or -1 if degree is invalid</returns>
        public static int GetMidiForDegree(TheoryKey key, int degree, int octave)
        {
            int pc = GetDegreePitchClass(key, degree);
            if (pc < 0)
                return -1;
            
            // MIDI: C4 = 60 = (4+1)*12 + 0 = 60
            // Formula: (octave + 1) * 12 + pitchClass
            return (octave + 1) * 12 + pc;
        }
    }
}

