using System.Collections.Generic;
using UnityEngine;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Provides canonical note name spellings for chord tones based on root pitch class, triad quality, and seventh quality.
    /// Uses lookup tables to ensure consistent enharmonic spellings regardless of key context.
    /// Supports both triads (root-3rd-5th) and seventh chords (root-3rd-5th-7th).
    /// </summary>
    public static class TheorySpelling
    {
        // 12 canonical root names, chosen to avoid double sharps/flats:
        // C, Db, D, Eb, E, F, F#, G, Ab, A, Bb, B
        // We'll build triads from these with canonical spellings.

        private static readonly Dictionary<(int rootPc, ChordQuality quality), string[]> TriadSpellings
            = new Dictionary<(int, ChordQuality), string[]>
            {
                // Major triads
                { (0,  ChordQuality.Major), new[] { "C",  "E",  "G"  } }, // C
                { (1,  ChordQuality.Major), new[] { "Db", "F",  "Ab" } }, // Db
                { (2,  ChordQuality.Major), new[] { "D",  "F#", "A"  } }, // D
                { (3,  ChordQuality.Major), new[] { "Eb", "G",  "Bb" } }, // Eb
                { (4,  ChordQuality.Major), new[] { "E",  "G#", "B"  } }, // E
                { (5,  ChordQuality.Major), new[] { "F",  "A",  "C"  } }, // F
                { (6,  ChordQuality.Major), new[] { "F#", "A#", "C#" } }, // F#
                { (7,  ChordQuality.Major), new[] { "G",  "B",  "D"  } }, // G
                { (8,  ChordQuality.Major), new[] { "Ab", "C",  "Eb" } }, // Ab
                { (9,  ChordQuality.Major), new[] { "A",  "C#", "E"  } }, // A
                { (10, ChordQuality.Major), new[] { "Bb", "D",  "F"  } }, // Bb
                { (11, ChordQuality.Major), new[] { "B",  "D#", "F#" } }, // B

                // Minor triads
                { (0,  ChordQuality.Minor), new[] { "C",  "Eb", "G"  } }, // Cm
                { (1,  ChordQuality.Minor), new[] { "Db", "E",  "Ab" } }, // Dbm
                { (2,  ChordQuality.Minor), new[] { "D",  "F",  "A"  } }, // Dm
                { (3,  ChordQuality.Minor), new[] { "Eb", "Gb", "Bb" } }, // Ebm
                { (4,  ChordQuality.Minor), new[] { "E",  "G",  "B"  } }, // Em
                { (5,  ChordQuality.Minor), new[] { "F",  "Ab", "C"  } }, // Fm
                { (6,  ChordQuality.Minor), new[] { "F#", "A",  "C#" } }, // F#m
                { (7,  ChordQuality.Minor), new[] { "G",  "Bb", "D"  } }, // Gm
                { (8,  ChordQuality.Minor), new[] { "Ab", "Cb", "Eb" } }, // Abm (we accept one Cb case here)
                { (9,  ChordQuality.Minor), new[] { "A",  "C",  "E"  } }, // Am
                { (10, ChordQuality.Minor), new[] { "Bb", "Db", "F"  } }, // Bbm
                { (11, ChordQuality.Minor), new[] { "B",  "D",  "F#" } }, // Bm

                // Diminished triads
                { (0,  ChordQuality.Diminished), new[] { "C",  "Eb", "Gb" } }, // Cdim
                { (1,  ChordQuality.Diminished), new[] { "Db", "Fb", "Abb" } }, // Dbdim (Fb and Abb are theoretically correct)
                { (2,  ChordQuality.Diminished), new[] { "D",  "F",  "Ab" } }, // Ddim
                { (3,  ChordQuality.Diminished), new[] { "Eb", "Gb", "Bbb" } }, // Ebdim (Bbb is correct dim5 above Gb)
                { (4,  ChordQuality.Diminished), new[] { "E",  "G",  "Bb" } }, // Edim
                { (5,  ChordQuality.Diminished), new[] { "F",  "Ab", "Cb" } }, // Fdim
                { (6,  ChordQuality.Diminished), new[] { "F#", "A",  "C" } }, // F#dim
                { (7,  ChordQuality.Diminished), new[] { "G",  "Bb", "Db" } }, // Gdim
                { (8,  ChordQuality.Diminished), new[] { "Ab", "Cb", "Ebb" } }, // Abdim
                { (9,  ChordQuality.Diminished), new[] { "A",  "C",  "Eb" } }, // Adim
                { (10, ChordQuality.Diminished), new[] { "Bb", "Db", "Fb" } }, // Bbdim
                { (11, ChordQuality.Diminished), new[] { "B",  "D",  "F" } }, // Bdim

                // Augmented triads
                { (0,  ChordQuality.Augmented), new[] { "C",  "E",  "G#" } }, // C+
                { (1,  ChordQuality.Augmented), new[] { "Db", "F",  "A" } }, // Db+
                { (2,  ChordQuality.Augmented), new[] { "D",  "F#", "A#" } }, // D+
                { (3,  ChordQuality.Augmented), new[] { "Eb", "G",  "B" } }, // Eb+
                { (4,  ChordQuality.Augmented), new[] { "E",  "G#", "B#" } }, // E+ (B# is theoretically correct aug5 above G#)
                { (5,  ChordQuality.Augmented), new[] { "F",  "A",  "C#" } }, // F+
                { (6,  ChordQuality.Augmented), new[] { "F#", "A#", "C##" } }, // F#+ (C## = C double-sharp)
                { (7,  ChordQuality.Augmented), new[] { "G",  "B",  "D#" } }, // G+
                { (8,  ChordQuality.Augmented), new[] { "Ab", "C",  "E" } }, // Ab+
                { (9,  ChordQuality.Augmented), new[] { "A",  "C#", "E#" } }, // A+
                { (10, ChordQuality.Augmented), new[] { "Bb", "D",  "F#" } }, // Bb+
                { (11, ChordQuality.Augmented), new[] { "B",  "D#", "Fx" } }, // B+ (Fx = F double-sharp)
            };

        // Additional spellings for enharmonic equivalents (used when RootSemitoneOffset indicates flat/sharp preference)
        private static readonly Dictionary<(int rootPc, ChordQuality quality), string[]> EnharmonicSpellings
            = new Dictionary<(int, ChordQuality), string[]>
            {
                // Gb major (flat-side spelling for pitch class 6)
                { (6, ChordQuality.Major), new[] { "Gb", "Bb", "Db" } }, // Gb major
            };

        // Seventh chord spelling lookup table: maps (rootPc, triadQuality, seventhQuality) to 7th note name
        // The root-3rd-5th names are reused from TriadSpellings, so this table only stores the 7th note name
        private static readonly Dictionary<(int rootPc, ChordQuality triadQuality, SeventhQuality seventhQuality), string> SeventhSpellings
            = new Dictionary<(int, ChordQuality, SeventhQuality), string>
            {
                // Dominant7 (major triad + minor 7th = 10 semitones) - 12 roots
                { (0,  ChordQuality.Major, SeventhQuality.Dominant7), "Bb" }, // C7: C-E-G-Bb
                { (1,  ChordQuality.Major, SeventhQuality.Dominant7), "Cb" }, // Db7: Db-F-Ab-Cb (theoretically correct, matches flat-side root)
                { (2,  ChordQuality.Major, SeventhQuality.Dominant7), "C"  }, // D7: D-F#-A-C
                { (3,  ChordQuality.Major, SeventhQuality.Dominant7), "Db" }, // Eb7: Eb-G-Bb-Db
                { (4,  ChordQuality.Major, SeventhQuality.Dominant7), "D"  }, // E7: E-G#-B-D
                { (5,  ChordQuality.Major, SeventhQuality.Dominant7), "Eb" }, // F7: F-A-C-Eb
                { (6,  ChordQuality.Major, SeventhQuality.Dominant7), "E"  }, // F#7: F#-A#-C#-E
                { (7,  ChordQuality.Major, SeventhQuality.Dominant7), "F"  }, // G7: G-B-D-F
                { (8,  ChordQuality.Major, SeventhQuality.Dominant7), "Gb" }, // Ab7: Ab-C-Eb-Gb
                { (9,  ChordQuality.Major, SeventhQuality.Dominant7), "G"  }, // A7: A-C#-E-G
                { (10, ChordQuality.Major, SeventhQuality.Dominant7), "Ab" }, // Bb7: Bb-D-F-Ab
                { (11, ChordQuality.Major, SeventhQuality.Dominant7), "A"  }, // B7: B-D#-F#-A

                // Major7 (major triad + major 7th) - 12 roots
                { (0,  ChordQuality.Major, SeventhQuality.Major7), "B"  }, // Cmaj7: C-E-G-B
                { (1,  ChordQuality.Major, SeventhQuality.Major7), "C"  }, // Dbmaj7: Db-F-Ab-C
                { (2,  ChordQuality.Major, SeventhQuality.Major7), "C#" }, // Dmaj7: D-F#-A-C#
                { (3,  ChordQuality.Major, SeventhQuality.Major7), "D"  }, // Ebmaj7: Eb-G-Bb-D
                { (4,  ChordQuality.Major, SeventhQuality.Major7), "D#" }, // Emaj7: E-G#-B-D#
                { (5,  ChordQuality.Major, SeventhQuality.Major7), "E"  }, // Fmaj7: F-A-C-E
                { (6,  ChordQuality.Major, SeventhQuality.Major7), "E#" }, // F#maj7: F#-A#-C#-E# (F enharmonically)
                { (7,  ChordQuality.Major, SeventhQuality.Major7), "F#" }, // Gmaj7: G-B-D-F#
                { (8,  ChordQuality.Major, SeventhQuality.Major7), "G"  }, // Abmaj7: Ab-C-Eb-G
                { (9,  ChordQuality.Major, SeventhQuality.Major7), "G#" }, // Amaj7: A-C#-E-G#
                { (10, ChordQuality.Major, SeventhQuality.Major7), "A"  }, // Bbmaj7: Bb-D-F-A
                { (11, ChordQuality.Major, SeventhQuality.Major7), "A#" }, // Bmaj7: B-D#-F#-A# (Bb enharmonically)

                // Minor7 (minor triad + minor 7th = 10 semitones) - 12 roots
                { (0,  ChordQuality.Minor, SeventhQuality.Minor7), "Bb" }, // Cm7: C-Eb-G-Bb
                { (1,  ChordQuality.Minor, SeventhQuality.Minor7), "Cb" }, // Dbm7: Db-E-Ab-Cb (theoretically correct, matches flat-side root)
                { (2,  ChordQuality.Minor, SeventhQuality.Minor7), "C"  }, // Dm7: D-F-A-C
                { (3,  ChordQuality.Minor, SeventhQuality.Minor7), "Db" }, // Ebm7: Eb-Gb-Bb-Db
                { (4,  ChordQuality.Minor, SeventhQuality.Minor7), "D"  }, // Em7: E-G-B-D
                { (5,  ChordQuality.Minor, SeventhQuality.Minor7), "Eb" }, // Fm7: F-Ab-C-Eb
                { (6,  ChordQuality.Minor, SeventhQuality.Minor7), "E"  }, // F#m7: F#-A-C#-E
                { (7,  ChordQuality.Minor, SeventhQuality.Minor7), "F"  }, // Gm7: G-Bb-D-F
                { (8,  ChordQuality.Minor, SeventhQuality.Minor7), "Gb" }, // Abm7: Ab-Cb-Eb-Gb
                { (9,  ChordQuality.Minor, SeventhQuality.Minor7), "G"  }, // Am7: A-C-E-G
                { (10, ChordQuality.Minor, SeventhQuality.Minor7), "Ab" }, // Bbm7: Bb-Db-F-Ab
                { (11, ChordQuality.Minor, SeventhQuality.Minor7), "A"  }, // Bm7: B-D-F#-A

                // Half-diminished7 (diminished triad + minor 7th = 10 semitones) - 12 roots
                { (0,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Bb" }, // Cø7: C-Eb-Gb-Bb
                { (1,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Cb" }, // Dbø7: Db-Fb-Abb-Cb (theoretically correct, matches flat-side root)
                { (2,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "C"  }, // Dø7: D-F-Ab-C
                { (3,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Db" }, // Ebø7: Eb-Gb-Bbb-Db
                { (4,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "D"  }, // Eø7: E-G-Bb-D
                { (5,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Eb" }, // Fø7: F-Ab-Cb-Eb
                { (6,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "E"  }, // F#ø7: F#-A-C-E
                { (7,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "F"  }, // Gø7: G-Bb-Db-F
                { (8,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Gb" }, // Abø7: Ab-Cb-Ebb-Gb
                { (9,  ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "G"  }, // Aø7: A-C-Eb-G
                { (10, ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "Ab" }, // Bbø7: Bb-Db-Fb-Ab
                { (11, ChordQuality.Diminished, SeventhQuality.HalfDiminished7), "A"  }, // Bø7: B-D-F-A

                // Fully diminished7 (diminished triad + diminished 7th) - 12 roots
                { (0,  ChordQuality.Diminished, SeventhQuality.Diminished7), "Bbb" }, // Cdim7: C-Eb-Gb-Bbb (A enharmonically)
                { (1,  ChordQuality.Diminished, SeventhQuality.Diminished7), "Cb"  }, // Dbdim7: Db-Fb-Abb-Cb (B enharmonically)
                { (2,  ChordQuality.Diminished, SeventhQuality.Diminished7), "C"   }, // Ddim7: D-F-Ab-C
                { (3,  ChordQuality.Diminished, SeventhQuality.Diminished7), "Db"  }, // Ebdim7: Eb-Gb-Bbb-Db (D enharmonically)
                { (4,  ChordQuality.Diminished, SeventhQuality.Diminished7), "D"   }, // Edim7: E-G-Bb-D
                { (5,  ChordQuality.Diminished, SeventhQuality.Diminished7), "Eb"  }, // Fdim7: F-Ab-Cb-Eb (E enharmonically)
                { (6,  ChordQuality.Diminished, SeventhQuality.Diminished7), "E"   }, // F#dim7: F#-A-C-E
                { (7,  ChordQuality.Diminished, SeventhQuality.Diminished7), "F"   }, // Gdim7: G-Bb-Db-F
                { (8,  ChordQuality.Diminished, SeventhQuality.Diminished7), "Gb"  }, // Abdim7: Ab-Cb-Ebb-Gb (G enharmonically)
                { (9,  ChordQuality.Diminished, SeventhQuality.Diminished7), "G"   }, // Adim7: A-C-Eb-G
                { (10, ChordQuality.Diminished, SeventhQuality.Diminished7), "Ab"  }, // Bbdim7: Bb-Db-Fb-Ab (A enharmonically)
                { (11, ChordQuality.Diminished, SeventhQuality.Diminished7), "Ab"  }, // Bdim7: B-D-F-Ab (fully diminished)
            };

        // Enharmonic spellings for 7th chords (used when RootSemitoneOffset indicates flat/sharp preference)
        private static readonly Dictionary<(int rootPc, ChordQuality triadQuality, SeventhQuality seventhQuality), string> EnharmonicSeventhSpellings
            = new Dictionary<(int, ChordQuality, SeventhQuality), string>
            {
                // Gb dominant7 (flat-side spelling for pitch class 6)
                { (6, ChordQuality.Major, SeventhQuality.Dominant7), "Gb" }, // Gb7: Gb-Bb-Db-Gb (7th is Fb enharmonically, but we use Gb to match root spelling)
            };

        /// <summary>
        /// Returns canonical root/third/fifth names for a triad defined by
        /// root pitch class and triad quality. Returns null if not found.
        /// </summary>
        /// <param name="rootPitchClass">Root pitch class (0-11, where 0=C, 1=C#/Db, etc.)</param>
        /// <param name="quality">Chord quality (Major, Minor, Diminished, or Augmented)</param>
        /// <returns>Array of 3 strings [root, third, fifth] with canonical spellings, or null if not found</returns>
        public static string[] GetTriadSpelling(int rootPitchClass, ChordQuality quality)
        {
            // Normalize pitch class to 0-11 range
            int normalizedPc = (rootPitchClass % 12 + 12) % 12;

            // Support Major, Minor, Diminished, and Augmented
            // Other qualities will fall back to existing TheoryPitch behavior
            if (quality != ChordQuality.Major && quality != ChordQuality.Minor &&
                quality != ChordQuality.Diminished && quality != ChordQuality.Augmented)
                return null;

            if (TriadSpellings.TryGetValue((normalizedPc, quality), out var names))
                return names;

            return null;
        }

        /// <summary>
        /// Returns canonical root/third/fifth names for a triad, with enharmonic disambiguation
        /// based on root semitone offset. For pitch class 6 (F#/Gb), uses Gb spelling if offset is negative.
        /// </summary>
        /// <param name="rootPitchClass">Root pitch class (0-11, where 0=C, 1=C#/Db, etc.)</param>
        /// <param name="quality">Chord quality (Major, Minor, Diminished, or Augmented)</param>
        /// <param name="rootSemitoneOffset">Semitone offset from diatonic degree (negative = flat, positive = sharp)</param>
        /// <returns>Array of 3 strings [root, third, fifth] with canonical spellings, or null if not found</returns>
        public static string[] GetTriadSpelling(int rootPitchClass, ChordQuality quality, int rootSemitoneOffset)
        {
            // Normalize pitch class to 0-11 range
            int normalizedPc = (rootPitchClass % 12 + 12) % 12;

            // Support Major, Minor, Diminished, and Augmented
            // Other qualities will fall back to existing TheoryPitch behavior
            if (quality != ChordQuality.Major && quality != ChordQuality.Minor &&
                quality != ChordQuality.Diminished && quality != ChordQuality.Augmented)
                return null;

            // Special handling for pitch class 6 (F#/Gb): use Gb if offset is negative (lowered/flattened)
            if (normalizedPc == 6 && rootSemitoneOffset < 0)
            {
                if (EnharmonicSpellings.TryGetValue((normalizedPc, quality), out var enharmonicNames))
                    return enharmonicNames;
            }

            // Default lookup
            if (TriadSpellings.TryGetValue((normalizedPc, quality), out var names))
                return names;

            return null;
        }

        /// <summary>
        /// Returns canonical root/third/fifth/seventh names for a seventh chord defined by
        /// root pitch class, triad quality, and seventh quality. Returns null if not found.
        /// Uses lookup tables to ensure consistent enharmonic spellings for all four chord tones.
        /// </summary>
        /// <param name="rootPitchClass">Root pitch class (0-11, where 0=C, 1=C#/Db, etc.)</param>
        /// <param name="triadQuality">Triad quality (Major, Minor, Diminished, or Augmented)</param>
        /// <param name="seventhQuality">Seventh quality (Dominant7, Major7, Minor7, HalfDiminished7, Diminished7)</param>
        /// <param name="rootSemitoneOffset">Optional semitone offset from diatonic degree for enharmonic disambiguation (default: 0)</param>
        /// <returns>Array of 4 strings [root, third, fifth, seventh] with canonical spellings, or null if not found</returns>
        public static string[] GetSeventhChordSpelling(
            int rootPitchClass,
            ChordQuality triadQuality,
            SeventhQuality seventhQuality,
            int rootSemitoneOffset = 0)
        {
            // Normalize pitch class to 0-11 range
            int normalizedPc = (rootPitchClass % 12 + 12) % 12;

            // Get triad spelling (root, 3rd, 5th) - reuse existing lookup
            string[] triadNames = GetTriadSpelling(normalizedPc, triadQuality, rootSemitoneOffset);
            if (triadNames == null || triadNames.Length < 3)
            {
                // No triad spelling available - cannot build seventh chord spelling
                return null;
            }

            // Check for enharmonic 7th spelling first
            string seventhName = null;
            if (normalizedPc == 6 && rootSemitoneOffset < 0)
            {
                if (EnharmonicSeventhSpellings.TryGetValue((normalizedPc, triadQuality, seventhQuality), out seventhName))
                {
                    // Found enharmonic spelling
                }
            }

            // If no enharmonic spelling found, use standard lookup
            if (seventhName == null)
            {
                if (!SeventhSpellings.TryGetValue((normalizedPc, triadQuality, seventhQuality), out seventhName))
                {
                    // No 7th spelling found - fall back to null (caller can use heuristic)
                    return null;
                }
            }

            // Combine triad names with 7th name
            return new[] { triadNames[0], triadNames[1], triadNames[2], seventhName };
        }
    }
}

