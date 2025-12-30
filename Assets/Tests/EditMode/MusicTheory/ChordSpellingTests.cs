using NUnit.Framework;
using System.Collections.Generic;
using Sonoria.MusicTheory;

namespace Sonoria.Tests
{
    /// <summary>
    /// Regression tests for chord spelling to catch enharmonic spelling bugs.
    /// Ensures that standard triads and 7ths in common Ionian keys are spelled correctly.
    /// </summary>
    public class ChordSpellingTests
    {
        /// <summary>
        /// Helper: Parses a Roman numeral and returns the spelled chord tones.
        /// </summary>
        /// <param name="tonicIndex">Tonic index 0..11 (C=0, Db=1, ..., B=11)</param>
        /// <param name="roman">Roman numeral string (e.g., "I", "ii", "V7")</param>
        /// <returns>Array of spelled note names (root, 3rd, 5th, [7th])</returns>
        private static List<string> SpellRomanChord(int tonicIndex, string roman)
        {
            var key = new TheoryKey(tonicIndex, ScaleMode.Ionian);

            // Use the same parser ChordLabController uses
            bool ok = TheoryChord.TryParseRomanNumeral(key, roman, out ChordRecipe recipe);
            Assert.IsTrue(ok, $"Failed to parse Roman numeral '{roman}' in key {key}");

            var spelled = TheoryChord.GetSpelledChordTones(key, recipe);
            Assert.IsNotNull(spelled, $"Spelled chord tones returned null for '{roman}' in {key}");
            Assert.Greater(spelled.Count, 0, $"Spelled chord tones is empty for '{roman}' in {key}");

            return spelled;
        }

        /// <summary>
        /// Helper: Parses a chord symbol and returns the spelled chord tones.
        /// </summary>
        /// <param name="tonicIndex">Tonic index 0..11 (C=0, Db=1, ..., B=11)</param>
        /// <param name="symbol">Chord symbol string (e.g., "C#m", "F#m7")</param>
        /// <returns>Array of spelled note names (root, 3rd, 5th, [7th])</returns>
        private static List<string> SpellChordSymbol(int tonicIndex, string symbol)
        {
            var key = new TheoryKey(tonicIndex, ScaleMode.Ionian);

            bool ok = TheoryChord.TryParseChordSymbol(key, symbol, out ChordRecipe recipe, out string errorMessage);
            Assert.IsTrue(ok, $"Failed to parse chord symbol '{symbol}' in key {key}: {errorMessage}");

            var spelled = TheoryChord.GetSpelledChordTones(key, recipe);
            Assert.IsNotNull(spelled, $"Spelled chord tones returned null for '{symbol}' in {key}");
            Assert.Greater(spelled.Count, 0, $"Spelled chord tones is empty for '{symbol}' in {key}");

            return spelled;
        }

        /// <summary>
        /// Helper: Asserts that the actual spelling matches the expected spelling.
        /// </summary>
        /// <param name="actual">Actual spelled chord tones</param>
        /// <param name="expected">Expected note names (root, 3rd, 5th, [7th])</param>
        private static void AssertSpelling(List<string> actual, params string[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count,
                $"Expected {expected.Length} notes, got {actual.Count} ({string.Join(" ", actual)})");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i],
                    $"Mismatch at index {i}: expected {expected[i]}, got {actual[i]} (full: {string.Join(" ", actual)})");
            }
        }

        [Test]
        public void C_Ionian_DiatonicTriadsAndSevenths()
        {
            // Triads
            AssertSpelling(SpellRomanChord(0, "I"), "C", "E", "G");
            AssertSpelling(SpellRomanChord(0, "ii"), "D", "F", "A");
            AssertSpelling(SpellRomanChord(0, "iii"), "E", "G", "B");
            AssertSpelling(SpellRomanChord(0, "IV"), "F", "A", "C");
            AssertSpelling(SpellRomanChord(0, "V"), "G", "B", "D");
            AssertSpelling(SpellRomanChord(0, "vi"), "A", "C", "E");
            AssertSpelling(SpellRomanChord(0, "viio"), "B", "D", "F");

            // Sevenths
            AssertSpelling(SpellRomanChord(0, "ii7"), "D", "F", "A", "C");
            AssertSpelling(SpellRomanChord(0, "V7"), "G", "B", "D", "F");
            AssertSpelling(SpellRomanChord(0, "vi7"), "A", "C", "E", "G");
        }

        [Test]
        public void G_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(7, "I"), "G", "B", "D");
            AssertSpelling(SpellRomanChord(7, "ii"), "A", "C", "E");
            AssertSpelling(SpellRomanChord(7, "iii"), "B", "D", "F#");
            AssertSpelling(SpellRomanChord(7, "IV"), "C", "E", "G");
            AssertSpelling(SpellRomanChord(7, "V"), "D", "F#", "A");
            AssertSpelling(SpellRomanChord(7, "vi"), "E", "G", "B");
            AssertSpelling(SpellRomanChord(7, "viio"), "F#", "A", "C");

            AssertSpelling(SpellRomanChord(7, "ii7"), "A", "C", "E", "G");
            AssertSpelling(SpellRomanChord(7, "V7"), "D", "F#", "A", "C");
            AssertSpelling(SpellRomanChord(7, "vi7"), "E", "G", "B", "D");
        }

        [Test]
        public void D_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(2, "I"), "D", "F#", "A");
            AssertSpelling(SpellRomanChord(2, "ii"), "E", "G", "B");
            AssertSpelling(SpellRomanChord(2, "iii"), "F#", "A", "C#");
            AssertSpelling(SpellRomanChord(2, "IV"), "G", "B", "D");
            AssertSpelling(SpellRomanChord(2, "V"), "A", "C#", "E");
            AssertSpelling(SpellRomanChord(2, "vi"), "B", "D", "F#");
            AssertSpelling(SpellRomanChord(2, "viio"), "C#", "E", "G");

            AssertSpelling(SpellRomanChord(2, "ii7"), "E", "G", "B", "D");
            AssertSpelling(SpellRomanChord(2, "V7"), "A", "C#", "E", "G");
            AssertSpelling(SpellRomanChord(2, "vi7"), "B", "D", "F#", "A");
        }

        [Test]
        public void A_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(9, "I"), "A", "C#", "E");
            AssertSpelling(SpellRomanChord(9, "ii"), "B", "D", "F#");
            AssertSpelling(SpellRomanChord(9, "iii"), "C#", "E", "G#");
            AssertSpelling(SpellRomanChord(9, "IV"), "D", "F#", "A");
            AssertSpelling(SpellRomanChord(9, "V"), "E", "G#", "B");
            AssertSpelling(SpellRomanChord(9, "vi"), "F#", "A", "C#");
            AssertSpelling(SpellRomanChord(9, "viio"), "G#", "B", "D");

            AssertSpelling(SpellRomanChord(9, "ii7"), "B", "D", "F#", "A");
            AssertSpelling(SpellRomanChord(9, "V7"), "E", "G#", "B", "D");
            AssertSpelling(SpellRomanChord(9, "vi7"), "F#", "A", "C#", "E");
        }

        [Test]
        public void E_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(4, "I"), "E", "G#", "B");
            AssertSpelling(SpellRomanChord(4, "ii"), "F#", "A", "C#");
            AssertSpelling(SpellRomanChord(4, "iii"), "G#", "B", "D#");
            AssertSpelling(SpellRomanChord(4, "IV"), "A", "C#", "E");
            AssertSpelling(SpellRomanChord(4, "V"), "B", "D#", "F#");
            AssertSpelling(SpellRomanChord(4, "vi"), "C#", "E", "G#");
            AssertSpelling(SpellRomanChord(4, "viio"), "D#", "F#", "A");

            AssertSpelling(SpellRomanChord(4, "ii7"), "F#", "A", "C#", "E");
            AssertSpelling(SpellRomanChord(4, "V7"), "B", "D#", "F#", "A");
            AssertSpelling(SpellRomanChord(4, "vi7"), "C#", "E", "G#", "B");
        }

        [Test]
        public void F_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(5, "I"), "F", "A", "C");
            AssertSpelling(SpellRomanChord(5, "ii"), "G", "Bb", "D");
            AssertSpelling(SpellRomanChord(5, "iii"), "A", "C", "E");
            AssertSpelling(SpellRomanChord(5, "IV"), "Bb", "D", "F");
            AssertSpelling(SpellRomanChord(5, "V"), "C", "E", "G");
            AssertSpelling(SpellRomanChord(5, "vi"), "D", "F", "A");
            AssertSpelling(SpellRomanChord(5, "viio"), "E", "G", "Bb");

            AssertSpelling(SpellRomanChord(5, "ii7"), "G", "Bb", "D", "F");
            AssertSpelling(SpellRomanChord(5, "V7"), "C", "E", "G", "Bb");
            AssertSpelling(SpellRomanChord(5, "vi7"), "D", "F", "A", "C");
        }

        [Test]
        public void Bb_Ionian_DiatonicTriadsAndSevenths()
        {
            AssertSpelling(SpellRomanChord(10, "I"), "Bb", "D", "F");
            AssertSpelling(SpellRomanChord(10, "ii"), "C", "Eb", "G");
            AssertSpelling(SpellRomanChord(10, "iii"), "D", "F", "A");
            AssertSpelling(SpellRomanChord(10, "IV"), "Eb", "G", "Bb");
            AssertSpelling(SpellRomanChord(10, "V"), "F", "A", "C");
            AssertSpelling(SpellRomanChord(10, "vi"), "G", "Bb", "D");
            AssertSpelling(SpellRomanChord(10, "viio"), "A", "C", "Eb");

            AssertSpelling(SpellRomanChord(10, "ii7"), "C", "Eb", "G", "Bb");
            AssertSpelling(SpellRomanChord(10, "V7"), "F", "A", "C", "Eb");
            AssertSpelling(SpellRomanChord(10, "vi7"), "G", "Bb", "D", "F");
        }

        [Test]
        public void A_Ionian_CsharpMinorSymbolSpelling()
        {
            // Test that chord symbol parser + speller agree with expected spellings
            AssertSpelling(SpellChordSymbol(9, "C#m"), "C#", "E", "G#");
            AssertSpelling(SpellChordSymbol(9, "F#m7"), "F#", "A", "C#", "E");
            AssertSpelling(SpellChordSymbol(9, "Bm"), "B", "D", "F#");
            AssertSpelling(SpellChordSymbol(9, "E"), "E", "G#", "B");
        }
    }
}

