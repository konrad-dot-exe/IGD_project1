using System;
using System.Collections.Generic;
using System.Linq;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Chord quality types for triads and extended chords.
    /// </summary>
    public enum ChordQuality
    {
        Major,
        Minor,
        Diminished,
        Augmented
    }

    /// <summary>
    /// Seventh quality types for extended chords.
    /// Explicitly represents presence/absence and flavor of the 7th.
    /// </summary>
    public enum SeventhQuality
    {
        None = 0,      // no 7th present
        Major7,
        Minor7,
        Dominant7,     // major triad + minor 7th (functional dominant)
        HalfDiminished7,
        Diminished7
    }

    /// <summary>
    /// Chord extensions beyond the triad.
    /// </summary>
    public enum ChordExtension
    {
        None,
        Seventh
        // Future: Maj7, Dim7, etc.
    }

    /// <summary>
    /// Represents the quality of a diatonic seventh chord.
    /// Describes the full 7th chord flavor (separate from triad quality).
    /// </summary>
    public enum SeventhChordQuality
    {
        Major7,
        Minor7,
        Dominant7,
        HalfDiminished7,
        Diminished7
    }

    /// <summary>
    /// Chord inversion (voicing position).
    /// </summary>
    public enum ChordInversion
    {
        Root = 0,
        First = 1,
        Second = 2,
        Third = 3
    }

    /// <summary>
    /// Coarse classification of whether a chord is diatonic or non-diatonic in a key.
    /// </summary>
    public enum ChordDiatonicStatus
    {
        Diatonic,
        NonDiatonic
    }

    /// <summary>
    /// Bit flags indicating which parallel modes on the same tonic a chord is diatonic to.
    /// For Phase 1 this is a placeholder; all values will be None.
    /// </summary>
    [Flags]
    public enum ParallelModeFlag
    {
        None        = 0,
        Ionian      = 1 << 0,
        Dorian      = 1 << 1,
        Phrygian    = 1 << 2,
        Lydian      = 1 << 3,
        Mixolydian  = 1 << 4,
        Aeolian     = 1 << 5,
        Locrian     = 1 << 6
    }

    /// <summary>
    /// High-level summary of borrowing relative to the tonic's parallel major and minor.
    /// </summary>
    public enum BorrowSummary
    {
        /// <summary>
        /// No borrowing detected, or the chord is diatonic in the current mode.
        /// </summary>
        None = 0,
        /// <summary>
        /// Chord is non-diatonic in the current mode but is diatonic to the
        /// parallel major (Ionian) on the same tonic.
        /// </summary>
        FromParallelMajor,
        /// <summary>
        /// Chord is non-diatonic in the current mode but is diatonic to the
        /// parallel minor (Aeolian) on the same tonic.
        /// </summary>
        FromParallelMinor,
        /// <summary>
        /// Chord is non-diatonic in the current mode and is diatonic only to
        /// other parallel modes (e.g. Phrygian, Lydian, etc.), or is ambiguous
        /// between multiple parallel sources.
        /// </summary>
        FromOtherModes
    }

    /// <summary>
    /// Classification of a chord's functional role in the context of a key.
    /// Used to generate informative labels for non-diatonic chords.
    /// </summary>
    public enum ChordFunctionTag
    {
        /// <summary>
        /// Chord is diatonic to the current key/mode.
        /// </summary>
        Diatonic,
        /// <summary>
        /// Chord functions as a secondary dominant or applied leading-tone chord.
        /// </summary>
        SecondaryDominant,
        /// <summary>
        /// Chord is borrowed from the parallel major mode.
        /// </summary>
        BorrowedParallelMajor,
        /// <summary>
        /// Chord is borrowed from the parallel minor mode.
        /// </summary>
        BorrowedParallelMinor,
        /// <summary>
        /// Chord is borrowed from other parallel modes (Dorian, Phrygian, Lydian, etc.).
        /// </summary>
        BorrowedOtherModes,
        /// <summary>
        /// Chord is chromatic but doesn't fit into the above categories.
        /// </summary>
        OtherChromatic,
        /// <summary>
        /// Neapolitan chord: major triad (optionally with a 7th) on bII.
        /// </summary>
        Neapolitan
    }

    /// <summary>
    /// Early-stage container for richer chord analysis data.
    /// For Phase 1 this simply wraps the existing diatonic status and a few
    /// basic fields. Later phases will populate ParallelModeMembership and
    /// SecondaryTargetDegree.
    /// </summary>
    public struct ChordFunctionProfile
    {
        /// <summary>
        /// Diatonic vs non-diatonic status in the current key/mode.
        /// This is the same value returned by TheoryChord.AnalyzeChord.
        /// </summary>
        public ChordDiatonicStatus DiatonicStatus;

        /// <summary>
        /// The scale degree (1–7) as parsed from the Roman numeral.
        /// </summary>
        public int Degree;

        /// <summary>
        /// Root pitch class (0–11) of the chord, derived from the degree.
        /// For Phase 1 this is computed from the mode degree only.
        /// </summary>
        public int RootPitchClass;

        /// <summary>
        /// Bit flags indicating which parallel modes on the same tonic
        /// this chord is diatonic to. For Phase 1 this will remain None.
        /// </summary>
        public ParallelModeFlag ParallelModeMembership;

        /// <summary>
        /// If this chord functions as a secondary dominant or applied
        /// leading-tone chord, the diatonic degree (1–7) it targets.
        /// For Phase 1 this is always null.
        /// </summary>
        public int? SecondaryTargetDegree;

        /// <summary>
        /// High-level summary of borrowing relative to the tonic's parallel major and minor.
        /// </summary>
        public BorrowSummary BorrowSummary;

        /// <summary>
        /// Classification of the chord's functional role in the context of the key.
        /// Used to generate informative labels for non-diatonic chords.
        /// </summary>
        public ChordFunctionTag FunctionTag;

        /// <summary>
        /// Scale degree (1–7) of the bass note relative to the current key/mode,
        /// or 0 if the bass pitch does not match a diatonic scale degree.
        /// </summary>
        public int BassDegree;
    }

    /// <summary>
    /// Analysis result for a chord in the context of a key.
    /// Provides expected diatonic qualities and whether the supplied recipe matches them.
    /// </summary>
    public struct ChordAnalysis
    {
        public TheoryKey Key;
        public ChordRecipe OriginalRecipe;
        public ChordQuality ExpectedTriad;
        public SeventhChordQuality? ExpectedSeventh;
        public bool IsDiatonicTriad;
        public bool IsDiatonicSeventh;
        public ChordDiatonicStatus Status;
    }

    /// <summary>
    /// Represents a chord recipe (degree + quality + extension).
    /// Used to build chords from a TheoryKey.
    /// </summary>
    public readonly struct ChordRecipe
    {
        public int Degree { get; }               // 1..7
        public ChordQuality Quality { get; }
        public ChordExtension Extension { get; }
        /// <summary>
        /// Explicit seventh quality for this chord. SeventhQuality.None means no 7th.
        /// </summary>
        public SeventhQuality SeventhQuality { get; }
        /// <summary>
        /// Semitone offset applied to the chord root relative to the diatonic degree
        /// in the current key. 0 = unaltered scale degree; -1 = flattened root (b),
        /// +1 = sharpened root (#).
        /// </summary>
        public int RootSemitoneOffset { get; }

        /// <summary>
        /// Chord inversion (voicing position). Root = 0, First = 1, Second = 2, Third = 3.
        /// </summary>
        public ChordInversion Inversion { get; }

        public ChordRecipe(
            int degree,
            ChordQuality quality,
            ChordExtension extension = ChordExtension.None,
            int rootSemitoneOffset = 0,
            SeventhQuality seventhQuality = SeventhQuality.None,
            ChordInversion inversion = ChordInversion.Root)
        {
            Degree = degree;
            Quality = quality;
            Extension = extension;
            RootSemitoneOffset = rootSemitoneOffset;
            SeventhQuality = seventhQuality;
            Inversion = inversion;
        }

        public override string ToString() => $"Degree {Degree}, {Quality}, {Extension}, Offset {RootSemitoneOffset}, Inversion {Inversion}";
    }

    /// <summary>
    /// Provides chord-building and Roman numeral parsing utilities.
    /// </summary>
    public static class TheoryChord
    {
        /// <summary>
        /// Builds a chord (triad or seventh) in the given key.
        /// Root note comes from the mode; third and fifth intervals are calculated from ChordQuality.
        /// For 7th chords, the 7th interval is determined by the explicit SeventhQuality on the recipe.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">Chord recipe (degree, quality, extension)</param>
        /// <param name="octave">Root octave (MIDI standard: C4 = octave 4)</param>
        /// <returns>Array of MIDI notes (length 3 for triads, 4 for sevenths)</returns>
        public static int[] BuildChord(TheoryKey key, ChordRecipe recipe, int octave)
        {
            // Get root MIDI note from the scale
            int rootMidi = TheoryScale.GetMidiForDegree(key, recipe.Degree, octave);
            if (rootMidi < 0)
            {
                UnityEngine.Debug.LogError($"TheoryChord.BuildChord: Invalid degree {recipe.Degree}");
                return new int[0];
            }

            // Apply chromatic alteration from the Roman numeral (bII, #IV, etc.)
            rootMidi += recipe.RootSemitoneOffset;

            // Compute triad intervals from root based on quality
            int thirdInterval;
            int fifthInterval;
            
            switch (recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4;  // major third
                    fifthInterval = 7; // perfect fifth
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3;  // minor third
                    fifthInterval = 7;  // perfect fifth
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3;  // minor third
                    fifthInterval = 6;  // diminished fifth
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4;  // major third
                    fifthInterval = 8;  // augmented fifth
                    break;
                default:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
            }

            // Build triad
            var midiNotes = new List<int>(4)
            {
                rootMidi,
                rootMidi + thirdInterval,
                rootMidi + fifthInterval
            };

            // Add 7th note if extension is Seventh and an explicit seventh quality is present
            if (recipe.Extension == ChordExtension.Seventh && recipe.SeventhQuality != SeventhQuality.None)
            {
                int seventhInterval;
                switch (recipe.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11; // major 7th
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10; // minor 7th
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9; // diminished 7th
                        break;
                    default:
                        // No seventh added when quality is None or unrecognized.
                        seventhInterval = 0;
                        break;
                }

                if (recipe.SeventhQuality != SeventhQuality.None)
                {
                    midiNotes.Add(rootMidi + seventhInterval);
                }
            }

            // Apply inversion rotation if needed
            if (midiNotes.Count > 1)
            {
                int rotations = (int)recipe.Inversion;
                rotations = UnityEngine.Mathf.Clamp(rotations, 0, midiNotes.Count - 1);

                for (int i = 0; i < rotations; i++)
                {
                    int lowest = midiNotes[0];
                    midiNotes.RemoveAt(0);
                    midiNotes.Add(lowest + 12); // Move lowest note up an octave
                }
            }

            return midiNotes.ToArray();
        }

        /// <summary>
        /// Builds a chord via <see cref="BuildChord"/> and returns its pitch classes.
        /// Primarily intended for tests and debugging utilities.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">Chord recipe to build</param>
        /// <param name="octave">Root octave (defaults to 4 / middle C)</param>
        /// <returns>List of pitch classes (duplicates preserved)</returns>
        public static List<int> BuildChordPitchClasses(TheoryKey key, ChordRecipe recipe, int octave = 4)
        {
            var midiNotes = BuildChord(key, recipe, octave);
            var pitchClasses = new List<int>(midiNotes.Length);
            for (int i = 0; i < midiNotes.Length; i++)
            {
                pitchClasses.Add(TheoryPitch.PitchClassFromMidi(midiNotes[i]));
            }
            return pitchClasses;
        }

        /// <summary>
        /// Wraps a degree (1-7) when it goes past 7.
        /// Examples: 7 → 7, 8 → 1, 9 → 2, etc.
        /// </summary>
        private static int WrapDegree(int degree)
        {
            // Convert to 0-based: degree 1 → index 0, degree 7 → index 6
            int index = (degree - 1) % 7;
            // Convert back to 1-based: index 0 → degree 1
            return index + 1;
        }

        /// <summary>
        /// Parses a Roman numeral string into a ChordRecipe.
        /// Supports triads: "I", "ii", "iii", "IV", "V", "vi", "viidim", "Iaug"
        /// Supports 7ths: "I7", "ii7", "V7", "viidim7", "Iaug7"
        /// Supports root accidentals: "bII", "#iv", "bVII", etc. (single flat or sharp before the numeral)
        /// </summary>
        /// <param name="key">The key context (currently unused, reserved for future key-dependent parsing)</param>
        /// <param name="numeral">Roman numeral string to parse</param>
        /// <param name="recipe">Output recipe if parsing succeeds</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseRomanNumeral(TheoryKey key, string numeral, out ChordRecipe recipe)
        {
            recipe = default;

            // Null/empty check
            if (string.IsNullOrWhiteSpace(numeral))
                return false;

            // Normalize: trim whitespace
            string normalized = numeral.Trim();

            // Detect and parse leading accidental (b, #, or n/N)
            int rootOffset = 0;
            bool useNaturalFromParallelIonian = false;
            if (!string.IsNullOrEmpty(normalized))
            {
                char first = normalized[0];
                if (first == 'b' || first == 'B')
                {
                    rootOffset = -1;
                    normalized = normalized.Substring(1); // Strip accidental for existing parser

                    if (string.IsNullOrEmpty(normalized))
                    {
                        // Invalid: just "b" with no numeral
                        return false;
                    }
                }
                else if (first == '#')
                {
                    rootOffset = 1;
                    normalized = normalized.Substring(1); // Strip accidental for existing parser

                    if (string.IsNullOrEmpty(normalized))
                    {
                        // Invalid: just "#" with no numeral
                        return false;
                    }
                }
                else if (first == 'n' || first == 'N')
                {
                    // Special "natural" behaviour: use the pitch from parallel Ionian
                    useNaturalFromParallelIonian = true;
                    normalized = normalized.Substring(1); // Strip accidental for existing parser

                    if (string.IsNullOrEmpty(normalized))
                    {
                        // Invalid: just "n" with no numeral
                        return false;
                    }
                }
            }

            // Extract inversion suffix (e.g., /3rd, /5th, /7th) before parsing other suffixes
            // The inversion suffix comes after everything else, so we extract it first
            string inversionPart = null;
            int slashIndex = normalized.IndexOf('/');
            if (slashIndex >= 0)
            {
                inversionPart = normalized.Substring(slashIndex + 1);
                normalized = normalized.Substring(0, slashIndex); // Keep part before '/' for existing parsing
            }

            // Optional: accept "vii°" as alias for "viidim" (7th handled by explicit suffix logic below)
            if (normalized.EndsWith("°", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1) + "dim";
            }

            // Initialize extension, triad quality, and seventh quality.
            ChordExtension extension = ChordExtension.None;
            ChordQuality quality = ChordQuality.Major; // default
            SeventhQuality seventhQuality = SeventhQuality.None;
            string baseNumeral = normalized;

            // First, detect explicit 7th-quality suffixes (order of precedence):
            // maj7, hdim7 (half-dim), dim7/o7 (fully-dim), ø7/m7b5 (half-dim), m7, plain 7.
            // Note: hdim7 must be checked before dim7 to avoid false matches.
            if (normalized.EndsWith("maj7", StringComparison.OrdinalIgnoreCase))
            {
                seventhQuality = SeventhQuality.Major7;
                extension = ChordExtension.Seventh;
                baseNumeral = normalized.Substring(0, normalized.Length - 4);
            }
            else if (normalized.EndsWith("hdim7", StringComparison.OrdinalIgnoreCase))
            {
                seventhQuality = SeventhQuality.HalfDiminished7;
                extension = ChordExtension.Seventh;
                baseNumeral = normalized.Substring(0, normalized.Length - 5);
            }
            else if (normalized.EndsWith("dim7", StringComparison.OrdinalIgnoreCase) ||
                     normalized.EndsWith("o7", StringComparison.OrdinalIgnoreCase))
            {
                seventhQuality = SeventhQuality.Diminished7;
                extension = ChordExtension.Seventh;
                // dim7 is 4 chars, o7 is 2 chars
                if (normalized.EndsWith("dim7", StringComparison.OrdinalIgnoreCase))
                    baseNumeral = normalized.Substring(0, normalized.Length - 4);
                else
                    baseNumeral = normalized.Substring(0, normalized.Length - 2);
            }
            else if (normalized.EndsWith("ø7", StringComparison.Ordinal) ||
                     normalized.EndsWith("m7b5", StringComparison.OrdinalIgnoreCase))
            {
                seventhQuality = SeventhQuality.HalfDiminished7;
                extension = ChordExtension.Seventh;
                if (normalized.EndsWith("ø7", StringComparison.Ordinal))
                    baseNumeral = normalized.Substring(0, normalized.Length - 2);
                else
                    baseNumeral = normalized.Substring(0, normalized.Length - 4);
            }
            else if (normalized.EndsWith("m7", StringComparison.OrdinalIgnoreCase))
            {
                seventhQuality = SeventhQuality.Minor7;
                extension = ChordExtension.Seventh;
                baseNumeral = normalized.Substring(0, normalized.Length - 2);
            }
            else if (normalized.EndsWith("7", StringComparison.Ordinal))
            {
                seventhQuality = SeventhQuality.Dominant7;
                extension = ChordExtension.Seventh;
                baseNumeral = normalized.Substring(0, normalized.Length - 1);
            }

            // Now detect triad quality suffixes on the remaining base numeral.
            normalized = baseNumeral;

            // Parse triad-quality suffixes in order: dim, aug.
            if (normalized.EndsWith("dim", StringComparison.OrdinalIgnoreCase))
            {
                quality = ChordQuality.Diminished;
                baseNumeral = normalized.Substring(0, normalized.Length - 3);
            }
            else if (normalized.EndsWith("aug", StringComparison.OrdinalIgnoreCase))
            {
                quality = ChordQuality.Augmented;
                baseNumeral = normalized.Substring(0, normalized.Length - 3);
            }

            // Determine quality from case if not already set by suffix
            if (quality == ChordQuality.Major) // Only determine from case if no quality suffix was found
            {
                baseNumeral = baseNumeral.Trim();
                bool allUpper = true;
                bool allLower = true;

                foreach (char c in baseNumeral)
                {
                    if (char.IsLetter(c))
                    {
                        if (char.IsUpper(c))
                            allLower = false;
                        else if (char.IsLower(c))
                            allUpper = false;
                    }
                }

                if (allUpper && !allLower)
                    quality = ChordQuality.Major;
                else if (allLower && !allUpper)
                    quality = ChordQuality.Minor;
                // Mixed case: default to Major (could be refined later)
            }

            // Infer 7th quality from triad quality when just "7" is specified (not "m7" or "maj7")
            // This handles cases like "ii7" (should be Minor7) vs "V7" (should be Dominant7)
            if (extension == ChordExtension.Seventh && seventhQuality == SeventhQuality.Dominant7)
            {
                // If we got Dominant7 from the plain "7" suffix, infer based on triad quality
                if (quality == ChordQuality.Minor)
                {
                    // Minor triad + "7" → Minor7 (e.g., "ii7" in major key)
                    seventhQuality = SeventhQuality.Minor7;
                }
                // If quality is Major, keep Dominant7 (e.g., "V7" in major key)
            }

            // CRITICAL FIX: Fully diminished and half-diminished 7th chords REQUIRE a diminished triad base.
            // If seventhQuality is Diminished7 or HalfDiminished7, force the triad quality to Diminished.
            if (extension == ChordExtension.Seventh)
            {
                if (seventhQuality == SeventhQuality.Diminished7 || seventhQuality == SeventhQuality.HalfDiminished7)
                {
                    quality = ChordQuality.Diminished;
                }
            }

            // Map base Roman numeral to degree (1-7)
            baseNumeral = baseNumeral.Trim();
            int degree = ParseRomanNumeralToDegree(baseNumeral);
            if (degree < 1 || degree > 7)
                return false;

            // Parse inversion from the extracted inversion part
            ChordInversion inversion = ParseInversionSpecifier(inversionPart);

            // Compute final root offset
            int finalRootOffset;
            if (useNaturalFromParallelIonian)
            {
                // Get the diatonic pitch class for this degree in the current mode
                int modePc = TheoryScale.GetDegreePitchClass(key, degree);
                if (modePc < 0)
                {
                    // Invalid degree - fall back to normal rootOffset (shouldn't happen, but be safe)
                    finalRootOffset = rootOffset;
                }
                else
                {
                    // Construct a parallel Ionian key with the same tonic
                    var ionianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Ionian);
                    int ionianPc = TheoryScale.GetDegreePitchClass(ionianKey, degree);
                    
                    if (ionianPc < 0)
                    {
                        // Invalid - fall back
                        finalRootOffset = rootOffset;
                    }
                    else
                    {
                        // Compute the difference in semitones needed to move from current mode to Ionian
                        int diff = ionianPc - modePc;
                        
                        // Normalize into a small range (-6..+6) to avoid weird wraparounds
                        if (diff > 6) diff -= 12;
                        if (diff < -6) diff += 12;
                        
                        finalRootOffset = diff;
                    }
                }
            }
            else
            {
                // Normal b/# behaviour
                finalRootOffset = rootOffset;
            }

            // Create recipe with parsed degree, quality, extension, seventh quality, root offset, and inversion.
            recipe = new ChordRecipe(degree, quality, extension, finalRootOffset, seventhQuality, inversion);
            return true;
        }

        /// <summary>
        /// Parses a base Roman numeral (I-VII or i-vii) to a scale degree (1-7).
        /// </summary>
        private static int ParseRomanNumeralToDegree(string numeral)
        {
            // Normalize to uppercase for lookup
            string upper = numeral.ToUpperInvariant();

            // Simple lookup table
            return upper switch
            {
                "I" => 1,
                "II" => 2,
                "III" => 3,
                "IV" => 4,
                "V" => 5,
                "VI" => 6,
                "VII" => 7,
                _ => -1 // Invalid
            };
        }

        /// <summary>
        /// Parses an inversion specifier string (e.g., "3", "3rd", "5th", "7", "7th") into a ChordInversion enum value.
        /// Supports both numeric ("3", "5", "7") and ordinal ("3rd", "5th", "7th") formats.
        /// </summary>
        /// <param name="inversionPart">The inversion specifier string (e.g., "3rd", "5", "7th")</param>
        /// <returns>ChordInversion enum value (Root for invalid/empty input)</returns>
        private static ChordInversion ParseInversionSpecifier(string inversionPart)
        {
            if (string.IsNullOrEmpty(inversionPart))
                return ChordInversion.Root;

            inversionPart = inversionPart.Trim().ToLowerInvariant();

            // Strip "st", "nd", "rd", "th" if present
            if (inversionPart.EndsWith("st") || inversionPart.EndsWith("nd") ||
                inversionPart.EndsWith("rd") || inversionPart.EndsWith("th"))
            {
                inversionPart = inversionPart.Substring(0, inversionPart.Length - 2);
            }

            switch (inversionPart)
            {
                case "1":
                    return ChordInversion.Root;
                case "3":
                    return ChordInversion.First;
                case "5":
                    return ChordInversion.Second;
                case "7":
                    return ChordInversion.Third;
                default:
                    // Unknown specifier; fall back to root position
                    return ChordInversion.Root;
            }
        }

        /// <summary>
        /// Maps a scale degree (1-7) to its Roman numeral representation (I-VII).
        /// Returns degree.ToString() for values outside the 1-7 range.
        /// </summary>
        private static string DegreeToRoman(int degree)
        {
            return degree switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                6 => "VI",
                7 => "VII",
                _ => degree.ToString() // Fallback for invalid degrees
            };
        }

        /// <summary>
        /// Converts a ChordRecipe to a Roman numeral string representation, key-aware.
        /// Can emit 'n' for naturalized chords (Ionian version of a degree in non-Ionian modes).
        /// Format: Major uses uppercase (I, II, III, etc.), Minor uses lowercase (i, ii, iii, etc.),
        /// Diminished uses lowercase + "dim" (iidim, viidim, etc.), Augmented uses uppercase + "aug" (Iaug, etc.).
        /// 7th chords append "7", "dim7", or "aug7" as appropriate.
        /// </summary>
        /// <param name="key">The key context (used to determine if 'n' should be used instead of b/#)</param>
        /// <param name="recipe">The chord recipe to convert</param>
        /// <returns>Roman numeral string (e.g., "I", "ii", "viidim", "Iaug", "I7", "ii7", "viidim7", "nvi", "bVII")</returns>
        public static string RecipeToRomanNumeral(TheoryKey key, ChordRecipe recipe)
        {
            // Map degree to base Roman numeral
            string baseRoman = recipe.Degree switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                6 => "VI",
                7 => "VII",
                _ => "I" // Fallback
            };

            string result;

            // Apply quality formatting
            if (recipe.Extension == ChordExtension.Seventh && recipe.SeventhQuality != SeventhQuality.None)
            {
                // 7th chord: base Roman numeral casing from triad quality,
                // then append explicit 7th-quality suffix.
                string romanWithCase = recipe.Quality switch
                {
                    ChordQuality.Major => baseRoman,                     // Uppercase
                    ChordQuality.Minor => baseRoman.ToLowerInvariant(),  // Lowercase
                    ChordQuality.Diminished => baseRoman.ToLowerInvariant(), // Lowercase
                    ChordQuality.Augmented => baseRoman,                 // Uppercase
                    _ => baseRoman
                };

                string seventhSuffix = recipe.SeventhQuality switch
                {
                    SeventhQuality.Dominant7 => "7",
                    SeventhQuality.Major7 => "maj7",
                    SeventhQuality.Minor7 => "m7",
                    SeventhQuality.HalfDiminished7 => "ø7",
                    SeventhQuality.Diminished7 => "dim7",
                    _ => string.Empty
                };

                // Preserve augmented-7th legacy naming (e.g., Iaug7) when applicable.
                if (recipe.Quality == ChordQuality.Augmented && recipe.SeventhQuality == SeventhQuality.Dominant7)
                {
                    result = baseRoman + "aug7";
                }
                else
                {
                    result = romanWithCase + seventhSuffix;
                }
            }
            else
            {
                // Triad (or chord without explicit 7th): use legacy triad formatting.
                result = recipe.Quality switch
                {
                    ChordQuality.Major => baseRoman, // Uppercase (already uppercase)
                    ChordQuality.Minor => baseRoman.ToLowerInvariant(), // Lowercase
                    ChordQuality.Diminished => baseRoman.ToLowerInvariant() + "dim", // Lowercase + dim
                    ChordQuality.Augmented => baseRoman + "aug", // Uppercase + aug
                    _ => baseRoman
                };
            }

            // Append inversion suffix for non-root inversions
            string inversionSuffix = null;
            switch (recipe.Inversion)
            {
                case ChordInversion.First:
                    inversionSuffix = "/3rd";
                    break;
                case ChordInversion.Second:
                    inversionSuffix = "/5th";
                    break;
                case ChordInversion.Third:
                    inversionSuffix = "/7th";
                    break;
                case ChordInversion.Root:
                default:
                    inversionSuffix = null;
                    break;
            }

            if (!string.IsNullOrEmpty(inversionSuffix))
            {
                result += inversionSuffix;
            }

            // Compute the Ionian diff for this degree
            int degree = recipe.Degree;
            int modePc = TheoryScale.GetDegreePitchClass(key, degree);
            var ionianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Ionian);
            int ionianPc = TheoryScale.GetDegreePitchClass(ionianKey, degree);
            
            int ionianDiff = 0;
            if (modePc >= 0 && ionianPc >= 0)
            {
                ionianDiff = ionianPc - modePc;
                // Normalize into a small range (-6..+6) to avoid weird wraparounds
                if (ionianDiff > 6) ionianDiff -= 12;
                if (ionianDiff < -6) ionianDiff += 12;
            }

            // Read the actual root offset from the recipe
            int offset = recipe.RootSemitoneOffset;

            // Decide on the accidental prefix
            string accidentalPrefix = string.Empty;
            if (offset != 0)
            {
                // If offset matches the Ionian "naturalization" diff, prefer 'n'
                if (offset == ionianDiff && ionianDiff != 0)
                {
                    accidentalPrefix = "n";
                }
                else
                {
                    // Otherwise, fall back to b/# notation based purely on offset
                    if (offset < 0)
                    {
                        // Support multi-flats (though typically only -1)
                        accidentalPrefix = new string('b', -offset);
                    }
                    else if (offset > 0)
                    {
                        // Multi-sharps similarly
                        accidentalPrefix = new string('#', offset);
                    }
                }
            }

            // Prepend accidental prefix to the result
            return accidentalPrefix + result;
        }

        /// <summary>
        /// Converts a ChordRecipe to a Roman numeral string representation.
        /// Backwards-compatible overload that assumes Ionian mode.
        /// For key-aware conversion, use RecipeToRomanNumeral(TheoryKey, ChordRecipe).
        /// </summary>
        /// <param name="recipe">The chord recipe to convert</param>
        /// <returns>Roman numeral string (e.g., "I", "ii", "viidim", "Iaug", "I7", "ii7", "viidim7")</returns>
        public static string RecipeToRomanNumeral(ChordRecipe recipe)
        {
            // Default to C Ionian for backwards compatibility
            var defaultKey = new TheoryKey(ScaleMode.Ionian);
            return RecipeToRomanNumeral(defaultKey, recipe);
        }

        /// <summary>
        /// Returns the expected diatonic triad quality for a given scale degree in the specified key.
        /// Triad quality only; 7ths and extended chords are not handled here.
        /// Degree values outside 1-7 range are normalized using wrap logic.
        /// </summary>
        /// <param name="key">The key context (mode)</param>
        /// <param name="degree">Scale degree (1-7). Values outside range are normalized.</param>
        /// <returns>Expected diatonic triad quality (Major, Minor, or Diminished)</returns>
        public static ChordQuality GetDiatonicTriadQuality(TheoryKey key, int degree)
        {
            // Normalize degree to 1-7 range
            int normalizedDegree = ((degree - 1) % 7) + 1;

            // Define diatonic triad quality arrays for each mode
            // Index 0 = degree 1, index 6 = degree 7
            ChordQuality[] qualities = key.Mode switch
            {
                ScaleMode.Ionian => new[]
                {
                    ChordQuality.Major,      // I
                    ChordQuality.Minor,      // ii
                    ChordQuality.Minor,      // iii
                    ChordQuality.Major,      // IV
                    ChordQuality.Major,      // V
                    ChordQuality.Minor,      // vi
                    ChordQuality.Diminished  // vii°
                },
                ScaleMode.Dorian => new[]
                {
                    ChordQuality.Minor,      // i
                    ChordQuality.Minor,      // ii♭
                    ChordQuality.Major,      // III
                    ChordQuality.Major,      // IV
                    ChordQuality.Minor,      // v
                    ChordQuality.Diminished, // vi°
                    ChordQuality.Major       // VII
                },
                ScaleMode.Phrygian => new[]
                {
                    ChordQuality.Minor,      // i
                    ChordQuality.Major,      // II
                    ChordQuality.Major,      // III
                    ChordQuality.Minor,      // iv
                    ChordQuality.Diminished, // v°
                    ChordQuality.Major,      // VI
                    ChordQuality.Minor       // vii
                },
                ScaleMode.Lydian => new[]
                {
                    ChordQuality.Major,      // I
                    ChordQuality.Major,      // II
                    ChordQuality.Minor,      // iii
                    ChordQuality.Diminished, // #iv°
                    ChordQuality.Major,      // V
                    ChordQuality.Minor,      // vi
                    ChordQuality.Minor       // vii
                },
                ScaleMode.Mixolydian => new[]
                {
                    ChordQuality.Major,      // I
                    ChordQuality.Minor,      // ii
                    ChordQuality.Diminished, // iii°
                    ChordQuality.Major,      // IV
                    ChordQuality.Minor,      // v
                    ChordQuality.Minor,      // vi
                    ChordQuality.Major       // VII
                },
                ScaleMode.Aeolian => new[]
                {
                    ChordQuality.Minor,      // i
                    ChordQuality.Diminished, // ii°
                    ChordQuality.Major,      // III
                    ChordQuality.Minor,      // iv
                    ChordQuality.Minor,      // v
                    ChordQuality.Major,      // VI
                    ChordQuality.Major       // VII
                },
                ScaleMode.Locrian => new[]
                {
                    ChordQuality.Diminished, // i°
                    ChordQuality.Major,      // II
                    ChordQuality.Minor,      // iii
                    ChordQuality.Minor,      // iv
                    ChordQuality.Major,      // V
                    ChordQuality.Major,      // VI
                    ChordQuality.Minor       // VII
                },
                _ => new[]
                {
                    ChordQuality.Major,      // Fallback to Ionian pattern
                    ChordQuality.Minor,
                    ChordQuality.Minor,
                    ChordQuality.Major,
                    ChordQuality.Major,
                    ChordQuality.Minor,
                    ChordQuality.Diminished
                }
            };

            // Return quality for normalized degree (convert to 0-based index)
            return qualities[normalizedDegree - 1];
        }

        /// <summary>
        /// Base pattern for diatonic 7th chords in Ionian (Major) mode.
        /// Pattern: [Maj7, m7, m7, Maj7, Dom7, m7, HalfDim7]
        /// </summary>
        private static readonly SeventhChordQuality[] IonianSeventhPattern = new SeventhChordQuality[]
        {
            SeventhChordQuality.Major7,        // I
            SeventhChordQuality.Minor7,        // ii
            SeventhChordQuality.Minor7,        // iii
            SeventhChordQuality.Major7,        // IV
            SeventhChordQuality.Dominant7,     // V
            SeventhChordQuality.Minor7,        // vi
            SeventhChordQuality.HalfDiminished7 // viiø7
        };

        /// <summary>
        /// Converts a SeventhQuality enum value to a SeventhChordQuality enum value.
        /// Returns null if the quality is None (no 7th present).
        /// </summary>
        /// <param name="seventhQuality">The SeventhQuality value to convert</param>
        /// <returns>Corresponding SeventhChordQuality, or null if None</returns>
        private static SeventhChordQuality? ConvertSeventhQualityToSeventhChordQuality(SeventhQuality seventhQuality)
        {
            return seventhQuality switch
            {
                SeventhQuality.None => null,
                SeventhQuality.Major7 => SeventhChordQuality.Major7,
                SeventhQuality.Minor7 => SeventhChordQuality.Minor7,
                SeventhQuality.Dominant7 => SeventhChordQuality.Dominant7,
                SeventhQuality.HalfDiminished7 => SeventhChordQuality.HalfDiminished7,
                SeventhQuality.Diminished7 => SeventhChordQuality.Diminished7,
                _ => null
            };
        }

        /// <summary>
        /// Returns the expected diatonic seventh chord quality for a given scale degree in the specified key.
        /// Uses rotation of the Ionian pattern based on the mode.
        /// </summary>
        /// <param name="key">The key context (mode)</param>
        /// <param name="degree">Scale degree (1-7). Values outside range are normalized.</param>
        /// <returns>Expected diatonic seventh chord quality</returns>
        public static SeventhChordQuality GetDiatonicSeventhQuality(TheoryKey key, int degree)
        {
            if (IonianSeventhPattern == null || IonianSeventhPattern.Length != 7)
            {
                throw new System.Exception("Ionian seventh pattern must have length 7.");
            }

            // Normalize degree to 0-6 range (0-based index)
            int normalizedDegree = ((degree - 1) % 7 + 7) % 7; // gives 0..6

            // Map mode to rotation offset (each mode is a rotation of the Ionian pattern)
            int modeOffset = key.Mode switch
            {
                ScaleMode.Ionian => 0,
                ScaleMode.Dorian => 1,
                ScaleMode.Phrygian => 2,
                ScaleMode.Lydian => 3,
                ScaleMode.Mixolydian => 4,
                ScaleMode.Aeolian => 5,
                ScaleMode.Locrian => 6,
                _ => 0
            };

            // Rotate the pattern based on mode offset
            int index = (modeOffset + normalizedDegree) % 7;
            return IonianSeventhPattern[index];
        }

        /// <summary>
        /// Returns the 7 letter names (C..B) for degrees 1..7 of this key.
        /// The array index corresponds to degree-1 (index 0 = degree 1, index 6 = degree 7).
        /// </summary>
        /// <param name="key">The key context</param>
        /// <returns>Array of 7 letters representing the scale degrees</returns>
        private static char[] GetDegreeLettersForKey(TheoryKey key)
        {
            // Map tonic pitch class to letter
            char tonicLetter = key.TonicPitchClass switch
            {
                0 => 'C',
                1 => 'C', // C#/Db - we'll use C as base, accidentals handled separately
                2 => 'D',
                3 => 'D', // D#/Eb
                4 => 'E',
                5 => 'F',
                6 => 'F', // F#/Gb
                7 => 'G',
                8 => 'G', // G#/Ab
                9 => 'A',
                10 => 'A', // A#/Bb
                11 => 'B',
                _ => 'C' // Fallback
            };

            // For sharps/flats, determine the actual letter from the tonic name
            // We'll use GetNoteNameForDegreeWithOffset to get the proper spelling
            string tonicName = TheoryPitch.GetNoteNameForDegreeWithOffset(key, 1, 0);
            if (tonicName.Length > 0 && tonicName[0] >= 'A' && tonicName[0] <= 'G')
            {
                tonicLetter = tonicName[0];
            }

            // Build the 7-element array by stepping through the musical alphabet
            char[] letters = new char[7];
            string musicalAlphabet = "CDEFGAB";
            int tonicIndex = musicalAlphabet.IndexOf(tonicLetter);
            if (tonicIndex < 0) tonicIndex = 0; // Fallback to C

            for (int i = 0; i < 7; i++)
            {
                letters[i] = musicalAlphabet[(tonicIndex + i) % 7];
            }

            return letters;
        }

        /// <summary>
        /// Given a key and a letter (C..B), returns the diatonic pitch class of that letter
        /// in this key (i.e., the pitch class of the scale degree that uses that letter).
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="letter">The letter name (C, D, E, F, G, A, or B)</param>
        /// <returns>The diatonic pitch class (0-11) for that letter in this key, or -1 if not found</returns>
        private static int GetNaturalPitchClassForLetterInKey(TheoryKey key, char letter)
        {
            char[] degreeLetters = GetDegreeLettersForKey(key);

            // Find the index j such that degreeLetters[j] == letter
            for (int j = 0; j < degreeLetters.Length; j++)
            {
                if (degreeLetters[j] == letter)
                {
                    // The corresponding diatonic pitch class is from degree j+1
                    return TheoryScale.GetDegreePitchClass(key, j + 1);
                }
            }

            // Fallback: use raw C major mapping if letter not found in key
            return letter switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => -1
            };
        }

        /// <summary>
        /// Normalizes a degree to the range 1-7.
        /// </summary>
        /// <param name="degree">The degree (may be outside 1-7 range)</param>
        /// <returns>Normalized degree in range 1-7</returns>
        private static int NormalizeDegree(int degree)
        {
            return ((degree - 1) % 7 + 7) % 7 + 1;
        }

        /// <summary>
        /// Returns chord tone names with key-aware spelling.
        /// Uses the current key's accidental preference for diatonic chords,
        /// and the source mode's preference for borrowed chords (parallel minor/major).
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">The chord recipe</param>
        /// <returns>List of note names in the same order as BuildChord returns MIDI notes</returns>
        public static List<string> GetSpelledChordTones(TheoryKey key, ChordRecipe recipe)
        {
            int[] midiNotes = BuildChord(key, recipe, 4);
            var names = new List<string>();

            if (midiNotes == null)
                return names;

            // Compute root pitch class from recipe
            int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPc < 0)
            {
                rootPc = 0; // Fallback to C
            }
            rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
            if (rootPc < 0)
                rootPc += 12;

            // Try canonical spelling first (for both triads and 7th chords)
            string[] canonicalNames = null;
            bool hasSeventh = recipe.Extension == ChordExtension.Seventh && recipe.SeventhQuality != SeventhQuality.None;
            
            if (hasSeventh)
            {
                // Try canonical 7th chord spelling
                canonicalNames = TheorySpelling.GetSeventhChordSpelling(
                    rootPc,
                    recipe.Quality,
                    recipe.SeventhQuality,
                    recipe.RootSemitoneOffset);
            }
            
            // If no 7th chord spelling available, try triad spelling
            if (canonicalNames == null)
            {
                canonicalNames = TheorySpelling.GetTriadSpelling(rootPc, recipe.Quality, recipe.RootSemitoneOffset);
            }

            // If canonical spelling is available, use it for chord tones
            if (canonicalNames != null)
            {
                // Calculate expected pitch classes for chord tones
                int thirdPc = -1;
                int fifthPc = -1;
                int seventhPc = -1;

                switch (recipe.Quality)
                {
                    case ChordQuality.Major:
                        thirdPc = (rootPc + 4) % 12;
                        fifthPc = (rootPc + 7) % 12;
                        break;
                    case ChordQuality.Minor:
                        thirdPc = (rootPc + 3) % 12;
                        fifthPc = (rootPc + 7) % 12;
                        break;
                    case ChordQuality.Diminished:
                        thirdPc = (rootPc + 3) % 12;
                        fifthPc = (rootPc + 6) % 12;
                        break;
                    case ChordQuality.Augmented:
                        thirdPc = (rootPc + 4) % 12;
                        fifthPc = (rootPc + 8) % 12;
                        break;
                }

                if (hasSeventh)
                {
                    switch (recipe.SeventhQuality)
                    {
                        case SeventhQuality.Major7:
                            seventhPc = (rootPc + 11) % 12;
                            break;
                        case SeventhQuality.Minor7:
                        case SeventhQuality.Dominant7:
                        case SeventhQuality.HalfDiminished7:
                            seventhPc = (rootPc + 10) % 12;
                            break;
                        case SeventhQuality.Diminished7:
                            seventhPc = (rootPc + 9) % 12;
                            break;
                    }
                }

                // Decide which key to use for accidental preference (fallback)
                TheoryKey spellingKey = key;
                var profile = AnalyzeChordProfile(key, recipe);
                if (profile.BorrowSummary == BorrowSummary.FromParallelMinor)
                {
                    spellingKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Aeolian);
                }
                else if (profile.BorrowSummary == BorrowSummary.FromParallelMajor)
                {
                    spellingKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Ionian);
                }

                // Match each MIDI note to chord tone and use canonical spelling
                foreach (var midi in midiNotes)
                {
                    int notePc = (midi % 12 + 12) % 12;
                    string noteName = null;

                    if (notePc == rootPc && canonicalNames.Length > 0)
                    {
                        noteName = canonicalNames[0];  // Root
                    }
                    else if (thirdPc >= 0 && notePc == thirdPc && canonicalNames.Length > 1)
                    {
                        noteName = canonicalNames[1];  // Third
                    }
                    else if (fifthPc >= 0 && notePc == fifthPc && canonicalNames.Length > 2)
                    {
                        noteName = canonicalNames[2];  // Fifth
                    }
                    else if (hasSeventh && seventhPc >= 0 && notePc == seventhPc && canonicalNames.Length > 3)
                    {
                        noteName = canonicalNames[3];  // Seventh
                    }

                    // Fall back to key-based spelling if not a chord tone or canonical name unavailable
                    if (noteName == null)
                    {
                        noteName = TheoryPitch.GetPitchNameFromMidi(midi, spellingKey);
                    }

                    names.Add(noteName);
                }
            }
            else
            {
                // No canonical spelling available - use key-based spelling (original behavior)
                TheoryKey spellingKey = key;
                var profile = AnalyzeChordProfile(key, recipe);
                if (profile.BorrowSummary == BorrowSummary.FromParallelMinor)
                {
                    spellingKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Aeolian);
                }
                else if (profile.BorrowSummary == BorrowSummary.FromParallelMajor)
                {
                    spellingKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Ionian);
                }

                foreach (var midi in midiNotes)
                {
                    names.Add(TheoryPitch.GetPitchNameFromMidi(midi, spellingKey));
                }
            }

            return names;
        }


        /// <summary>
        /// Gets the chord-aware spelling for a bass note in slash chord notation.
        /// Uses GetSpelledChordTones to find the correct enharmonic spelling for the bass note.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="bassMidiNote">The MIDI note number of the bass note</param>
        /// <returns>Chord-aware note name for the bass note (e.g., "Eb" instead of "D#")</returns>
        private static string GetChordAwareBassNoteName(TheoryKey key, ChordRecipe recipe, int bassMidiNote)
        {
            // Get chord-aware note names
            var chordToneNames = GetSpelledChordTones(key, recipe);
            
            // Get MIDI notes to match pitch classes
            int[] midiNotes = BuildChord(key, recipe, 4);
            if (midiNotes == null || midiNotes.Length == 0 || chordToneNames.Count == 0)
            {
                // Fallback to key-aware naming
                return TheoryPitch.GetPitchNameFromMidi(bassMidiNote, key);
            }

            // Find which chord tone corresponds to the bass note (by pitch class)
            int bassPc = (bassMidiNote % 12 + 12) % 12;
            for (int i = 0; i < midiNotes.Length && i < chordToneNames.Count; i++)
            {
                int tonePc = (midiNotes[i] % 12 + 12) % 12;
                if (tonePc == bassPc)
                {
                    return chordToneNames[i];
                }
            }

            // Fallback if not found (shouldn't happen, but be safe)
            return TheoryPitch.GetPitchNameFromMidi(bassMidiNote, key);
        }

        /// <summary>
        /// Returns a chord symbol (e.g., C, Cmaj7, G7, Bm7b5, Cmaj7/E) based on the key,
        /// the chord recipe, and the spelled root note name. Uses diatonic
        /// seventh quality information when the chord matches the mode;
        /// otherwise falls back to quality-only naming. Supports slash chord notation
        /// when bass note differs from root.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="rootName">The root note name (e.g., "C", "D", "F#")</param>
        /// <param name="bassMidiNote">Optional MIDI note number of the bass note for slash chord notation</param>
        /// <returns>Chord symbol string (e.g., "C", "Cmaj7", "G7", "Dm7", "Bm7b5", "Cmaj7/E")</returns>
        public static string GetChordSymbol(TheoryKey key, ChordRecipe recipe, string rootName, int? bassMidiNote = null)
        {
            // First build the triad symbol exactly as for non-7th chords.
            string symbol = rootName;
            switch (recipe.Quality)
            {
                case ChordQuality.Minor:
                    symbol += "m";
                    break;
                case ChordQuality.Diminished:
                    symbol += "dim";
                    break;
                case ChordQuality.Augmented:
                    symbol += "aug";
                    break;
                case ChordQuality.Major:
                default:
                    // No suffix for major
                    break;
            }

            // If there's no 7th extension or no explicit seventh quality, check for slash chord before returning.
            if (recipe.Extension != ChordExtension.Seventh || recipe.SeventhQuality == SeventhQuality.None)
            {
                // Add slash chord notation if bass note differs from root
                if (bassMidiNote.HasValue)
                {
                    int bassPc = (bassMidiNote.Value % 12 + 12) % 12;
                    
                    // Calculate root pitch class from recipe (same logic as in AnalyzeChordProfile)
                    int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
                    if (rootPc >= 0)
                    {
                        rootPc = (rootPc + recipe.RootSemitoneOffset) % 12;
                        if (rootPc < 0)
                            rootPc += 12;
                    }
                    else
                    {
                        rootPc = 0; // Fallback
                    }

                    if (bassPc != rootPc)
                    {
                        string bassName = TheoryPitch.GetPitchNameFromMidi(bassMidiNote.Value, key);
                        symbol = $"{symbol}/{bassName}";
                    }
                }
                return symbol;
            }

            // Append 7th-quality suffix based on explicit SeventhQuality.
            switch (recipe.SeventhQuality)
            {
                case SeventhQuality.Dominant7:
                    // Dominant 7th: e.g., G7, D7
                    symbol += "7";
                    break;

                case SeventhQuality.Major7:
                    // Major 7th: e.g., Cmaj7, Fmaj7
                    symbol += "maj7";
                    break;

                case SeventhQuality.Minor7:
                    // Minor 7th: e.g., Dm7, Am7
                    // Avoid duplicating the 'm' if minor is already encoded in the triad.
                    if (symbol.EndsWith("m", StringComparison.Ordinal))
                    {
                        symbol += "7";
                    }
                    else
                    {
                        symbol += "m7";
                    }
                    break;

                case SeventhQuality.HalfDiminished7:
                    // Half-diminished: represent as m7b5 (Bm7b5)
                    symbol = rootName + "m7b5";
                    break;

                case SeventhQuality.Diminished7:
                    // Fully diminished: Bdim7
                    if (symbol.EndsWith("dim", StringComparison.Ordinal))
                    {
                        symbol += "7";
                    }
                    else
                    {
                        symbol = rootName + "dim7";
                    }
                    break;

                default:
                    break;
            }

            // Add slash chord notation if bass note differs from root
            if (bassMidiNote.HasValue)
            {
                int bassPc = (bassMidiNote.Value % 12 + 12) % 12;
                
                // Calculate root pitch class from recipe (same logic as in AnalyzeChordProfile)
                int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
                if (rootPc >= 0)
                {
                    rootPc = (rootPc + recipe.RootSemitoneOffset) % 12;
                    if (rootPc < 0)
                        rootPc += 12;
                }
                else
                {
                    rootPc = 0; // Fallback
                }

                if (bassPc != rootPc)
                {
                    // Use chord-aware spelling for the bass note to ensure correct enharmonics
                    // (e.g., Cm7/Eb instead of Cm7/D#)
                    string bassName = GetChordAwareBassNoteName(key, recipe, bassMidiNote.Value);
                    symbol = $"{symbol}/{bassName}";
                }
            }

            return symbol;
        }

        /// <summary>
        /// Returns a ChordRecipe whose triad quality has been adjusted to match
        /// the diatonic triad quality for the given key+degree, if needed.
        /// If an adjustment is made, wasAdjusted is set to true.
        /// Adjusts triad quality for both triads and extended chords (extension is preserved).
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">The original chord recipe</param>
        /// <param name="wasAdjusted">Set to true if quality was adjusted, false otherwise</param>
        /// <returns>Adjusted recipe (or original if no adjustment needed)</returns>
        public static ChordRecipe AdjustTriadQualityToMode(
            TheoryKey key,
            ChordRecipe recipe,
            out bool wasAdjusted)
        {
            wasAdjusted = false;

            // Get expected diatonic quality for this degree in this mode
            ChordQuality expectedQuality = GetDiatonicTriadQuality(key, recipe.Degree);

            // If already matches, no adjustment needed
            if (recipe.Quality == expectedQuality)
            {
                return recipe;
            }

            // Create adjusted recipe with expected quality (extension, seventh quality, root offset, and inversion stay unchanged)
            wasAdjusted = true;
            return new ChordRecipe(
                recipe.Degree,
                expectedQuality,
                recipe.Extension,
                recipe.RootSemitoneOffset,
                recipe.SeventhQuality,
                recipe.Inversion);
        }

        /// <summary>
        /// Builds a progression of chords from Roman numeral strings.
        /// All chords use the same rootOctave.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="numerals">List of Roman numeral strings</param>
        /// <param name="rootOctave">Octave for all chord roots (MIDI standard: C4 = octave 4)</param>
        /// <returns>List of MIDI note arrays (one per chord)</returns>
        public static List<int[]> BuildProgression(TheoryKey key, IReadOnlyList<string> numerals, int rootOctave)
        {
            var chords = new List<int[]>(numerals.Count);

            foreach (var numeral in numerals)
            {
                if (!TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    UnityEngine.Debug.LogWarning($"TheoryChord.BuildProgression: failed to parse numeral '{numeral}', skipping");
                    continue;
                }

                var chordMidi = BuildChord(key, recipe, rootOctave);
                if (chordMidi != null && chordMidi.Length > 0)
                {
                    chords.Add(chordMidi);
                }
            }

            return chords;
        }

        /// <summary>
        /// Analyze a chord in the context of a key: what triad/seventh qualities
        /// are diatonic on this degree, and does the supplied recipe match them?
        /// This does NOT modify the recipe; it is pure analysis.
        /// </summary>
        public static ChordAnalysis AnalyzeChord(TheoryKey key, ChordRecipe recipe)
        {
            // Normalize degree to 1-7 range
            int degree = recipe.Degree;
            int normalizedDegree = ((degree - 1) % 7 + 7) % 7 + 1; // 1..7

            // Compute diatonic expectations
            var expectedTriad = GetDiatonicTriadQuality(key, normalizedDegree);
            SeventhChordQuality? expectedSeventh = null;

            if (recipe.Extension == ChordExtension.Seventh)
            {
                expectedSeventh = GetDiatonicSeventhQuality(key, normalizedDegree);
            }

            // Compare recipe vs expectations
            bool isDiatonicTriad = (recipe.Quality == expectedTriad);
            bool isDiatonicSeventh = true;

            if (recipe.Extension == ChordExtension.Seventh && expectedSeventh.HasValue)
            {
                // Check if the recipe's explicit 7th quality matches the diatonic expectation
                if (recipe.SeventhQuality != SeventhQuality.None)
                {
                    var recipeSeventhChordQuality = ConvertSeventhQualityToSeventhChordQuality(recipe.SeventhQuality);
                    if (recipeSeventhChordQuality.HasValue)
                    {
                        isDiatonicSeventh = (recipeSeventhChordQuality.Value == expectedSeventh.Value);
                    }
                    else
                    {
                        // Unknown 7th quality - treat as non-diatonic
                        isDiatonicSeventh = false;
                    }
                }
                else
                {
                    // No explicit 7th quality specified - assume diatonic if triad matches
                    // (for backwards compatibility with recipes that don't specify 7th quality)
                    isDiatonicSeventh = isDiatonicTriad;
                }
            }

            var status = (isDiatonicTriad && isDiatonicSeventh)
                ? ChordDiatonicStatus.Diatonic
                : ChordDiatonicStatus.NonDiatonic;

            return new ChordAnalysis
            {
                Key = key,
                OriginalRecipe = recipe,
                ExpectedTriad = expectedTriad,
                ExpectedSeventh = expectedSeventh,
                IsDiatonicTriad = isDiatonicTriad,
                IsDiatonicSeventh = isDiatonicSeventh,
                Status = status
            };
        }

        /// <summary>
        /// Gets the set of pitch classes (0-11) that belong to the scale of the given key.
        /// </summary>
        private static HashSet<int> GetScalePitchClasses(TheoryKey key)
        {
            // Assumes a 7-note scale; collect degree→pitch class.
            var pcs = new HashSet<int>();
            for (int degree = 1; degree <= 7; degree++)
            {
                int pc = TheoryScale.GetDegreePitchClass(key, degree);
                if (pc >= 0)
                {
                    pcs.Add(pc % 12);
                }
            }

            return pcs;
        }

        /// <summary>
        /// Gets a dictionary mapping scale degrees (1-7) to their pitch classes (0-11) in the given key.
        /// </summary>
        private static Dictionary<int, int> GetDegreeToPitchClassMap(TheoryKey key)
        {
            var map = new Dictionary<int, int>();
            for (int degree = 1; degree <= 7; degree++)
            {
                int pc = TheoryScale.GetDegreePitchClass(key, degree);
                if (pc >= 0)
                {
                    map[degree] = pc % 12;
                }
            }
            return map;
        }

        /// <summary>
        /// Checks if all chord pitch classes are diatonic to the given key by comparing
        /// the actual chord notes against the scale's pitch classes.
        /// </summary>
        private static bool IsChordDiatonicToKey(TheoryKey key, IReadOnlyList<int> chordPitchClasses)
        {
            var scalePcs = GetScalePitchClasses(key);

            foreach (var pc in chordPitchClasses)
            {
                if (!scalePcs.Contains(pc))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Detects if a chord functions as a secondary dominant or applied leading-tone chord
        /// and returns the target degree (1-7) if found, null otherwise.
        /// Only considers non-diatonic chords with dominant-like or leading-tone-like qualities.
        /// </summary>
        private static int? DetectSecondaryTargetDegree(
            TheoryKey key,
            ChordRecipe recipe,
            int rootPitchClass)
        {
            // Only consider obviously "dominant-like" or "leading-tone-like" chords.
            bool couldBeDominantTriad =
                recipe.Quality == ChordQuality.Major ||
                recipe.Quality == ChordQuality.Augmented;

            bool couldBeLeadingTriad =
                recipe.Quality == ChordQuality.Diminished;

            // For 7th chords, check if the 7th quality suggests dominant or leading-tone function.
            // Since ChordRecipe doesn't store 7th quality directly, we check if it's a 7th extension
            // and infer from the context. For Phase 3, we simplify and use triad-based detection
            // primarily, but allow 7th extensions to be considered if the triad quality matches.
            bool couldBeDominantSeventh = false;
            bool couldBeLeadingSeventh = false;

            if (recipe.Extension == ChordExtension.Seventh)
            {
                // For 7th chords, we can check the diatonic 7th quality for the degree,
                // but for secondary dominants, we need to check if the actual chord is dominant-like.
                // Since we don't have the actual 7th quality stored, we'll use a simplified approach:
                // Major/Augmented triads with 7th extension could be dominant 7ths.
                // Diminished triads with 7th extension could be leading-tone 7ths.
                if (couldBeDominantTriad)
                {
                    // Major triad + 7th could be dominant 7th (though it could also be maj7)
                    // For Phase 3, we'll be lenient and consider it as potentially dominant-like
                    couldBeDominantSeventh = true;
                }
                if (couldBeLeadingTriad)
                {
                    // Diminished triad + 7th could be half-diminished or fully diminished
                    couldBeLeadingSeventh = true;
                }
            }

            bool couldBeDominantLike = couldBeDominantTriad || couldBeDominantSeventh;
            bool couldBeLeadingLike = couldBeLeadingTriad || couldBeLeadingSeventh;

            var degreeMap = GetDegreeToPitchClassMap(key);

            foreach (var kvp in degreeMap)
            {
                int degree = kvp.Key;
                int targetPc = kvp.Value;

                int dominantPc = (targetPc + 7) % 12;
                int leadingPc = (targetPc + 11) % 12;

                if (couldBeDominantLike && rootPitchClass == dominantPc)
                {
                    return degree; // V/degree
                }

                if (couldBeLeadingLike && rootPitchClass == leadingPc)
                {
                    return degree; // vii°/degree
                }
            }

            return null;
        }

        /// <summary>
        /// Detects if a chord is a Neapolitan chord.
        /// A Neapolitan chord is a major triad (optionally with a 7th) built on bII
        /// (degree 2 with RootSemitoneOffset == -1), that is non-diatonic in the current key/mode.
        /// </summary>
        /// <param name="diatonicStatus">The diatonic status of the chord</param>
        /// <param name="recipe">The chord recipe to check</param>
        /// <returns>True if the chord is a Neapolitan chord, false otherwise</returns>
        private static bool IsNeapolitan(ChordDiatonicStatus diatonicStatus, ChordRecipe recipe)
        {
            if (diatonicStatus != ChordDiatonicStatus.NonDiatonic)
                return false;

            // Must be on scale degree 2 with a flattened root (bII)
            if (recipe.Degree != 2 || recipe.RootSemitoneOffset != -1)
                return false;

            // Triad must be major (we allow augmented/other variants later if desired)
            if (recipe.Quality != ChordQuality.Major)
                return false;

            // 7th quality is not critical; allow any seventh (or none)
            return true;
        }

        /// <summary>
        /// Returns a richer chord analysis profile while preserving the existing
        /// AnalyzeChord behavior. Records the diatonic status, degree, root pitch class,
        /// parallel mode membership, and secondary target degree (for secondary dominants
        /// and applied leading-tone chords).
        /// </summary>
        public static ChordFunctionProfile AnalyzeChordProfile(TheoryKey key, ChordRecipe recipe)
        {
            // Use the existing, battle-tested diatonic analysis.
            var analysis = AnalyzeChord(key, recipe);

            // Override diatonic status for non-diatonic 7ths
            // If the recipe includes a 7th and that 7th quality does not match the diatonic one,
            // force the status to NonDiatonic
            if (recipe.SeventhQuality != SeventhQuality.None)
            {
                var diatonicSeventh = GetDiatonicSeventhQuality(key, recipe.Degree);
                var recipeSeventhChordQuality = ConvertSeventhQualityToSeventhChordQuality(recipe.SeventhQuality);
                
                if (recipeSeventhChordQuality.HasValue && recipeSeventhChordQuality.Value != diatonicSeventh)
                {
                    // 7th quality doesn't match diatonic expectation - force non-diatonic
                    analysis.Status = ChordDiatonicStatus.NonDiatonic;
                }
            }

            // Get root pitch class from the scale degree.
            // For Phase 1, root is taken from the scale degree; later phases can
            // refine this if/when we support chromatic roots / accidentals.
            int rootPitchClass = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPitchClass < 0)
            {
                // Fallback to 0 if degree is invalid (shouldn't happen in normal use)
                rootPitchClass = 0;
            }
            else
            {
                // Apply chromatic alteration from the Roman numeral (bII, #IV, etc.)
                rootPitchClass = (rootPitchClass + recipe.RootSemitoneOffset) % 12;
                if (rootPitchClass < 0)
                    rootPitchClass += 12;
            }

            // Build the actual chord notes in the current key.
            // Use root octave 4 (arbitrary, since we only need pitch classes).
            int[] chordMidiNotes = BuildChord(key, recipe, 4);

            // Calculate bass degree from the lowest note
            int bassDegree = 0;
            if (chordMidiNotes != null && chordMidiNotes.Length > 0)
            {
                int bassMidi = chordMidiNotes.Min();
                int bassPc = (bassMidi % 12 + 12) % 12;
                
                var degreeMap = GetDegreeToPitchClassMap(key);
                foreach (var kvp in degreeMap)
                {
                    if (kvp.Value == bassPc)
                    {
                        bassDegree = kvp.Key;
                        break;
                    }
                }
            }

            // Extract unique pitch classes from the chord notes.
            var chordPitchClasses = new List<int>();
            if (chordMidiNotes != null)
            {
                foreach (var midi in chordMidiNotes)
                {
                    int pc = midi % 12;
                    if (!chordPitchClasses.Contains(pc))
                    {
                        chordPitchClasses.Add(pc);
                    }
                }
            }

            // Compute parallel mode membership by checking if chord pitch classes
            // are diatonic to each of the 7 parallel modes.
            // Parallel modes share the same tonic as the current key.
            ParallelModeFlag membership = ParallelModeFlag.None;

            // Create parallel keys for each mode on the same tonic.
            var ionianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Ionian);
            var dorianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Dorian);
            var phrygianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Phrygian);
            var lydianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Lydian);
            var mixolydianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Mixolydian);
            var aeolianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Aeolian);
            var locrianKey = new TheoryKey(key.TonicPitchClass, ScaleMode.Locrian);

            // Check diatonicity against each parallel mode using actual chord notes.
            if (IsChordDiatonicToKey(ionianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Ionian;

            if (IsChordDiatonicToKey(dorianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Dorian;

            if (IsChordDiatonicToKey(phrygianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Phrygian;

            if (IsChordDiatonicToKey(lydianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Lydian;

            if (IsChordDiatonicToKey(mixolydianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Mixolydian;

            if (IsChordDiatonicToKey(aeolianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Aeolian;

            if (IsChordDiatonicToKey(locrianKey, chordPitchClasses))
                membership |= ParallelModeFlag.Locrian;

            // Detect secondary dominant or applied leading-tone function (only for non-diatonic chords)
            int? secondaryTarget = null;
            if (analysis.Status == ChordDiatonicStatus.NonDiatonic)
            {
                secondaryTarget = DetectSecondaryTargetDegree(key, recipe, rootPitchClass);
            }

            // Compute borrowing summary based on parallel mode membership
            BorrowSummary borrowSummary = BorrowSummary.None;
            if (analysis.Status == ChordDiatonicStatus.NonDiatonic)
            {
                bool inIonian = (membership & ParallelModeFlag.Ionian) != 0;
                bool inAeolian = (membership & ParallelModeFlag.Aeolian) != 0;

                // Is this chord diatonic to parallel major/minor but not to the current mode?
                bool canBorrowFromParallelMajor =
                    inIonian && key.Mode != ScaleMode.Ionian;
                bool canBorrowFromParallelMinor =
                    inAeolian && key.Mode != ScaleMode.Aeolian;

                if (canBorrowFromParallelMajor && !canBorrowFromParallelMinor)
                {
                    borrowSummary = BorrowSummary.FromParallelMajor;
                }
                else if (!canBorrowFromParallelMajor && canBorrowFromParallelMinor)
                {
                    borrowSummary = BorrowSummary.FromParallelMinor;
                }
                else
                {
                    // Either ambiguous between major/minor or only diatonic to other modes.
                    // If the chord is diatonic to some parallel mode(s) but we can't
                    // cleanly attribute it to parallel major or minor, classify as Other.
                    ParallelModeFlag nonMajorMinorMask =
                        membership & ~(ParallelModeFlag.Ionian | ParallelModeFlag.Aeolian);
                    if (nonMajorMinorMask != ParallelModeFlag.None ||
                        (canBorrowFromParallelMajor && canBorrowFromParallelMinor))
                    {
                        borrowSummary = BorrowSummary.FromOtherModes;
                    }
                    else
                    {
                        // No parallel-mode diatonic memberships at all → keep None.
                        borrowSummary = BorrowSummary.None;
                    }
                }
            }

            // Derive FunctionTag based on priority:
            // 1. Diatonic → Diatonic
            // 2. Else if secondaryTarget is set → SecondaryDominant
            // 3. Else if Neapolitan → Neapolitan
            // 4. Else map BorrowSummary to appropriate tag or OtherChromatic
            ChordFunctionTag functionTag;
            if (analysis.Status == ChordDiatonicStatus.Diatonic)
            {
                functionTag = ChordFunctionTag.Diatonic;
            }
            else if (secondaryTarget.HasValue)
            {
                functionTag = ChordFunctionTag.SecondaryDominant;
            }
            else if (IsNeapolitan(analysis.Status, recipe))
            {
                functionTag = ChordFunctionTag.Neapolitan;
            }
            else
            {
                // Map BorrowSummary to FunctionTag
                functionTag = borrowSummary switch
                {
                    BorrowSummary.FromParallelMajor => ChordFunctionTag.BorrowedParallelMajor,
                    BorrowSummary.FromParallelMinor => ChordFunctionTag.BorrowedParallelMinor,
                    BorrowSummary.FromOtherModes => ChordFunctionTag.BorrowedOtherModes,
                    _ => ChordFunctionTag.OtherChromatic // None or other cases
                };
            }

            var profile = new ChordFunctionProfile
            {
                DiatonicStatus = analysis.Status,
                Degree = recipe.Degree,
                RootPitchClass = rootPitchClass,
                ParallelModeMembership = membership,
                SecondaryTargetDegree = secondaryTarget,
                BorrowSummary = borrowSummary,
                FunctionTag = functionTag,
                BassDegree = bassDegree
            };

            return profile;
        }

        /// <summary>
        /// Builds a short informational string describing a non-diatonic chord's function.
        /// Returns string.Empty for diatonic chords.
        /// </summary>
        /// <param name="profile">The chord function profile to analyze</param>
        /// <returns>Short info string (e.g., "sec. to V", "borrowed ∥ major", "borrowed ∥ Dorian/Phrygian") or empty string for diatonic chords</returns>
        public static string BuildNonDiatonicInfo(ChordFunctionProfile profile)
        {
            // Return empty string for diatonic chords
            if (profile.DiatonicStatus == ChordDiatonicStatus.Diatonic)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            // Handle secondary dominant
            if (profile.FunctionTag == ChordFunctionTag.SecondaryDominant && profile.SecondaryTargetDegree.HasValue)
            {
                string targetRoman = DegreeToRoman(profile.SecondaryTargetDegree.Value);
                parts.Add($"sec. to {targetRoman}");
            }
            // Handle Neapolitan
            else if (profile.FunctionTag == ChordFunctionTag.Neapolitan)
            {
                parts.Add("Neapolitan");
            }
            // Handle borrowed chords
            else if (profile.FunctionTag == ChordFunctionTag.BorrowedParallelMajor)
            {
                parts.Add("from || major");
            }
            else if (profile.FunctionTag == ChordFunctionTag.BorrowedParallelMinor)
            {
                parts.Add("from || minor");
            }
            else if (profile.FunctionTag == ChordFunctionTag.BorrowedOtherModes)
            {
                // Build mode list from ParallelModeMembership flags
                var modeNames = new List<string>();
                var membership = profile.ParallelModeMembership;

                // Exclude Ionian and Aeolian (already handled by BorrowedParallelMajor/Minor)
                if ((membership & ParallelModeFlag.Dorian) != 0)
                    modeNames.Add("Dorian");
                if ((membership & ParallelModeFlag.Phrygian) != 0)
                    modeNames.Add("Phrygian");
                if ((membership & ParallelModeFlag.Lydian) != 0)
                    modeNames.Add("Lydian");
                if ((membership & ParallelModeFlag.Mixolydian) != 0)
                    modeNames.Add("Mixolydian");
                if ((membership & ParallelModeFlag.Locrian) != 0)
                    modeNames.Add("Locrian");

                if (modeNames.Count > 0)
                {
                    parts.Add($"borrowed ∥ {string.Join("/", modeNames)}");
                }
                else
                {
                    // Fallback if no modes found (shouldn't happen, but be safe)
                    parts.Add("borrowed ∥ other");
                }
            }
            // OtherChromatic - no specific label needed, but we could add one if desired
            // For now, we'll leave it empty or add a generic label

            // Join multiple parts with " · "
            return string.Join(" · ", parts);
        }
    }
}

