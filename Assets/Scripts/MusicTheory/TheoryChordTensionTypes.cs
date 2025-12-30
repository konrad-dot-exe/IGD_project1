using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Kind of chord tension (9th-type tensions for now).
    /// </summary>
    public enum TensionKind
    {
        /// <summary>
        /// Flat 9th (b9) - 13 semitones above root
        /// </summary>
        FlatNine,
        
        /// <summary>
        /// Natural 9th (9) - 14 semitones above root
        /// </summary>
        Nine,
        
        /// <summary>
        /// Sharp 9th (#9) - 15 semitones above root
        /// </summary>
        SharpNine,
        
        /// <summary>
        /// Natural 11th (11) - 17 semitones above root (perfect 4th, PC +5)
        /// </summary>
        Eleven,
        
        /// <summary>
        /// Sharp 11th (#11) - 18 semitones above root (tritone, PC +6)
        /// </summary>
        SharpEleven
    }

    /// <summary>
    /// Classification of how a tension should be interpreted in the chord context.
    /// </summary>
    public enum TensionClassification
    {
        /// <summary>
        /// The note is a core chord tone (not a tension)
        /// </summary>
        ChordTone,
        
        /// <summary>
        /// Color tone - valid extension that adds color (e.g., #11 on dominant/major)
        /// </summary>
        ColorTone,
        
        /// <summary>
        /// Suspension - replaces the 3rd (e.g., 11 on triads/maj7/min7, or 11 on dom7 without 3rd)
        /// </summary>
        Suspension,
        
        /// <summary>
        /// Non-chord tone - not a valid extension for this chord type
        /// </summary>
        NonChordTone,
        
        /// <summary>
        /// Avoid tone - clashes with essential chord tones (e.g., 11 over dom7 with 3rd)
        /// </summary>
        AvoidTone
    }

    /// <summary>
    /// Represents a chord tension (e.g., b9, 9, #9).
    /// </summary>
    public struct ChordTension
    {
        /// <summary>
        /// The kind of tension (b9, 9, #9, etc.)
        /// </summary>
        public TensionKind Kind;

        /// <summary>
        /// How this tension should be interpreted in the chord context.
        /// </summary>
        public TensionClassification Classification;

        public ChordTension(TensionKind kind, TensionClassification classification = TensionClassification.ColorTone)
        {
            Kind = kind;
            Classification = classification;
        }

        public override string ToString()
        {
            return Kind switch
            {
                TensionKind.FlatNine => "b9",
                TensionKind.Nine => "9",
                TensionKind.SharpNine => "#9",
                TensionKind.Eleven => "11",
                TensionKind.SharpEleven => "#11",
                _ => "?"
            };
        }
    }

    /// <summary>
    /// Utility functions for working with chord tensions.
    /// </summary>
    public static class ChordTensionUtils
    {
        /// <summary>
        /// Returns the semitone offset above the root for a given 9th tension kind.
        /// All measured upward from root.
        /// </summary>
        /// <param name="kind">The tension kind</param>
        /// <returns>Semitone offset above root (13 for b9, 14 for 9, 15 for #9)</returns>
        public static int GetSemitoneOffsetFromRoot(TensionKind kind)
        {
            switch (kind)
            {
                case TensionKind.FlatNine:
                    return 13;  // b9 = 13 semitones above root
                case TensionKind.Nine:
                    return 14;  // 9 = 14 semitones above root
                case TensionKind.SharpNine:
                    return 15;  // #9 = 15 semitones above root
                case TensionKind.Eleven:
                    return 17;  // 11 = 17 semitones above root (perfect 4th, PC +5)
                case TensionKind.SharpEleven:
                    return 18;  // #11 = 18 semitones above root (tritone, PC +6)
                default:
                    return 14; // Default to natural 9
            }
        }

        /// <summary>
        /// Tries to get a 9th tension kind from a semitone interval above the root (mod 12).
        /// </summary>
        /// <param name="semitoneInterval">Interval in semitones above root (will be normalized mod 12)</param>
        /// <param name="kind">Output tension kind if found</param>
        /// <returns>True if the interval corresponds to a 9th tension, false otherwise</returns>
        public static bool TryGetNinthTensionKindFromInterval(int semitoneInterval, out TensionKind kind)
        {
            // Normalize interval to [0..11] (mod 12)
            int rel = ((semitoneInterval % 12) + 12) % 12;
            
            switch (rel)
            {
                case 1:  // b9 (13 mod 12 = 1)
                    kind = TensionKind.FlatNine;
                    return true;
                case 2:  // 9 (14 mod 12 = 2)
                    kind = TensionKind.Nine;
                    return true;
                case 3:  // #9 (15 mod 12 = 3)
                    kind = TensionKind.SharpNine;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
        
        /// <summary>
        /// Tries to get an 11th tension kind from a semitone interval above the root (mod 12).
        /// </summary>
        /// <param name="semitoneInterval">Interval in semitones above root (will be normalized mod 12)</param>
        /// <param name="kind">Output tension kind if found</param>
        /// <returns>True if the interval corresponds to an 11th tension, false otherwise</returns>
        public static bool TryGetEleventhTensionKindFromInterval(int semitoneInterval, out TensionKind kind)
        {
            // Normalize interval to [0..11] (mod 12)
            int rel = ((semitoneInterval % 12) + 12) % 12;
            
            switch (rel)
            {
                case 5:  // 11 (17 mod 12 = 5) - perfect 4th
                    kind = TensionKind.Eleven;
                    return true;
                case 6:  // #11 (18 mod 12 = 6) - tritone
                    kind = TensionKind.SharpEleven;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }
}

