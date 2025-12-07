using EarFPS;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Utility functions for working with TheoryKey.
    /// Includes conversion helpers for backward compatibility with EarFPS.ScaleMode.
    /// </summary>
    public static class TheoryKeyUtils
    {
        /// <summary>
        /// Converts from legacy EarFPS.ScaleMode to new Sonoria.MusicTheory.ScaleMode.
        /// </summary>
        public static ScaleMode FromLegacyMode(EarFPS.ScaleMode legacy)
        {
            return legacy switch
            {
                EarFPS.ScaleMode.Ionian => ScaleMode.Ionian,
                EarFPS.ScaleMode.Dorian => ScaleMode.Dorian,
                EarFPS.ScaleMode.Phrygian => ScaleMode.Phrygian,
                EarFPS.ScaleMode.Lydian => ScaleMode.Lydian,
                EarFPS.ScaleMode.Mixolydian => ScaleMode.Mixolydian,
                EarFPS.ScaleMode.Aeolian => ScaleMode.Aeolian,
                EarFPS.ScaleMode.Locrian => ScaleMode.Locrian,
                _ => ScaleMode.Ionian // fallback
            };
        }

        /// <summary>
        /// Converts from new Sonoria.MusicTheory.ScaleMode to legacy EarFPS.ScaleMode.
        /// Note: Locrian will map to Ionian (fallback) if EarFPS.ScaleMode doesn't have Locrian yet.
        /// </summary>
        public static EarFPS.ScaleMode ToLegacyMode(ScaleMode mode)
        {
            return mode switch
            {
                ScaleMode.Ionian => EarFPS.ScaleMode.Ionian,
                ScaleMode.Dorian => EarFPS.ScaleMode.Dorian,
                ScaleMode.Phrygian => EarFPS.ScaleMode.Phrygian,
                ScaleMode.Lydian => EarFPS.ScaleMode.Lydian,
                ScaleMode.Mixolydian => EarFPS.ScaleMode.Mixolydian,
                ScaleMode.Aeolian => EarFPS.ScaleMode.Aeolian,
                ScaleMode.Locrian => EarFPS.ScaleMode.Locrian,
                _ => EarFPS.ScaleMode.Ionian
            };
        }

        /// <summary>
        /// Gets the tonic pitch class for a key (0-11).
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>Tonic pitch class (0 = C, 1 = C#/Db, ..., 11 = B)</returns>
        public static int GetTonicPitchClass(TheoryKey key)
        {
            return key.TonicPitchClass;
        }
    }
}

