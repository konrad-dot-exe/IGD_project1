using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Hint about the accidental spelling of a melody note, based on user input.
    /// Used to disambiguate enharmonic equivalents (e.g., Ab vs G#) for chromatic harmonization.
    /// </summary>
    public enum AccidentalHint
    {
        /// <summary>
        /// No accidental hint available (default for degree-based melodies or MIDI-only input).
        /// </summary>
        None,
        /// <summary>
        /// Natural note (no accidental in user's spelling).
        /// </summary>
        Natural,
        /// <summary>
        /// Sharp accidental (# or ♯) was used in user's spelling.
        /// </summary>
        Sharp,
        /// <summary>
        /// Flat accidental (b or ♭) was used in user's spelling.
        /// </summary>
        Flat
    }

    /// <summary>
    /// A single melodic event: a note at a time, with duration.
    /// </summary>
    public struct MelodyEvent
    {
        public float TimeBeats;      // start time in beats
        public float DurationBeats;  // duration in beats
        public int Midi;             // MIDI note number
        /// <summary>
        /// Hint about the accidental spelling from user input, used for enharmonic disambiguation.
        /// Defaults to None for degree-based melodies.
        /// </summary>
        public AccidentalHint AccidentalHint;
    }

    /// <summary>
    /// Analysis result for a melodic event in the context of a key.
    /// </summary>
    public struct MelodyAnalysis
    {
        public float TimeBeats;
        public float DurationBeats;
        public int Midi;
        public int PitchClass;       // 0..11
        public int Degree;           // 1..7 diatonic degree (best-fit)
        public int SemitoneOffset;   // semitone diff from diatonic pc (e.g. +1 = #, -1 = b)
        public bool IsDiatonic;      // true if SemitoneOffset == 0
    }

    /// <summary>
    /// Static helper class for analyzing melodic events in the context of a key.
    /// </summary>
    public static class TheoryMelody
    {
        /// <summary>
        /// Analyze a single melodic event in the context of a key.
        /// Maps MIDI → (degree, semitone offset, diatonic/non-diatonic).
        /// </summary>
        public static MelodyAnalysis AnalyzeEvent(TheoryKey key, MelodyEvent ev)
        {
            // 1. Extract pitch class from MIDI
            int pitchClass = TheoryPitch.PitchClassFromMidi(ev.Midi);

            // 2. Get diatonic pitch classes for degrees 1..7
            // Build a map of degree → pitch class
            var degreeToPc = new Dictionary<int, int>();
            for (int degree = 1; degree <= 7; degree++)
            {
                int diatonicPc = TheoryScale.GetDegreePitchClass(key, degree);
                if (diatonicPc >= 0)
                {
                    degreeToPc[degree] = diatonicPc % 12;
                }
            }

            // 3. Find the best-fit degree by comparing semitone differences
            int bestDegree = 1;
            int bestDiff = 12; // Initialize to worst case
            bool foundMatch = false;

            foreach (var kvp in degreeToPc)
            {
                int degree = kvp.Key;
                int diatonicPc = kvp.Value;

                // Compute semitone difference (mod 12, then normalize to -6..+6)
                int diff = (pitchClass - diatonicPc + 12) % 12;
                if (diff > 6) diff -= 12;

                // Choose degree with smallest absolute difference
                // Tie-break: prefer 0 (diatonic), then ±1, then ±2, etc.
                int absDiff = System.Math.Abs(diff);
                int absBestDiff = System.Math.Abs(bestDiff);

                if (!foundMatch || 
                    absDiff < absBestDiff || 
                    (absDiff == absBestDiff && diff == 0 && bestDiff != 0))
                {
                    bestDegree = degree;
                    bestDiff = diff;
                    foundMatch = true;
                }
            }

            // 4. If no match found (shouldn't happen, but be safe), default to degree 1
            if (!foundMatch)
            {
                bestDegree = 1;
                bestDiff = 0;
            }

            // 5. Build and return MelodyAnalysis
            return new MelodyAnalysis
            {
                TimeBeats = ev.TimeBeats,
                DurationBeats = ev.DurationBeats,
                Midi = ev.Midi,
                PitchClass = pitchClass,
                Degree = bestDegree,
                SemitoneOffset = bestDiff,
                IsDiatonic = (bestDiff == 0)
            };
        }

        /// <summary>
        /// Analyze an entire melody line.
        /// </summary>
        public static List<MelodyAnalysis> AnalyzeMelodyLine(TheoryKey key, IList<MelodyEvent> melody)
        {
            var result = new List<MelodyAnalysis>();
            if (melody == null) return result;

            foreach (var ev in melody)
            {
                result.Add(AnalyzeEvent(key, ev));
            }

            return result;
        }
    }
}

