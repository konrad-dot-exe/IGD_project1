using UnityEngine;
using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Quick validation tests for TheoryChord.
    /// </summary>
    public static class TheoryChordTests
    {
        /// <summary>
        /// Tests diatonic triads in C Ionian (major scale).
        /// Tests parsing and building chords for all 7 scale degrees.
        /// </summary>
        public static void TestCIonianTriads()
        {
            Debug.Log("=== TheoryChord: C Ionian Diatonic Triads ===");

            // Create C Ionian key (Phase 1: tonic is always C)
            var key = new TheoryKey(ScaleMode.Ionian);

            // Standard diatonic triads in major: I, ii, iii, IV, V, vi, viidim
            string[] numerals = { "I", "ii", "iii", "IV", "V", "vi", "viidim" };

            foreach (var numeral in numerals)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse numeral '{numeral}'");
                    continue;
                }

                // Build chord at octave 4 (C4 = 60)
                var midi = TheoryChord.BuildChord(key, recipe, 4);
                
                if (midi == null || midi.Length == 0)
                {
                    Debug.LogError($"{numeral} -> Failed to build chord");
                    continue;
                }

                // Format MIDI array as string
                string midiStr = string.Join(", ", midi);
                Debug.Log($"{numeral} -> Degree {recipe.Degree}, Quality {recipe.Quality}, Extension {recipe.Extension}, MIDI: [{midiStr}]");
            }

            Debug.Log("=== Test Complete ===");
        }

        /// <summary>
        /// Tests augmented chord parsing (Iaug).
        /// </summary>
        public static void TestAugmentedChord()
        {
            Debug.Log("=== TheoryChord: Augmented Chord Test ===");
            var key = new TheoryKey(ScaleMode.Ionian);

            if (TheoryChord.TryParseRomanNumeral(key, "Iaug", out var recipe))
            {
                var midi = TheoryChord.BuildChord(key, recipe, 4);
                string midiStr = midi != null && midi.Length > 0 ? string.Join(", ", midi) : "null";
                Debug.Log($"Iaug -> {recipe}, MIDI: [{midiStr}]");
            }
            else
            {
                Debug.LogError("Failed to parse Iaug");
            }
        }

        /// <summary>
        /// Tests diminished chord parsing (viidim and vii°).
        /// </summary>
        public static void TestDiminishedVariants()
        {
            Debug.Log("=== TheoryChord: Diminished Chord Variants ===");
            var key = new TheoryKey(ScaleMode.Ionian);

            string[] variants = { "viidim", "VIIdim", "vii°" };

            foreach (var variant in variants)
            {
                if (TheoryChord.TryParseRomanNumeral(key, variant, out var recipe))
                {
                    Debug.Log($"{variant} -> {recipe} (Quality: {recipe.Quality})");
                }
                else
                {
                    Debug.LogWarning($"{variant} -> Parse failed");
                }
            }
        }

        /// <summary>
        /// Tests building a simple progression.
        /// </summary>
        public static void TestBuildProgression()
        {
            Debug.Log("=== TheoryChord: Build Progression Test ===");
            var key = new TheoryKey(ScaleMode.Ionian);

            // Common progression: I - V - vi - IV
            var numerals = new List<string> { "I", "V", "vi", "IV" };
            var chords = TheoryChord.BuildProgression(key, numerals, 4);

            Debug.Log($"Built {chords.Count} chords:");
            for (int i = 0; i < chords.Count; i++)
            {
                string midiStr = chords[i] != null && chords[i].Length > 0 ? string.Join(", ", chords[i]) : "null";
                Debug.Log($"  {numerals[i]}: [{midiStr}]");
            }
        }

        /// <summary>
        /// Tests diatonic triad quality inference for different modes.
        /// </summary>
        public static void TestDiatonicTriadQuality()
        {
            Debug.Log("=== TheoryChord: Diatonic Triad Quality Test ===");

            // Test C Ionian
            var ionianKey = new TheoryKey(ScaleMode.Ionian);
            Debug.Log("C Ionian triad qualities:");
            for (int degree = 1; degree <= 7; degree++)
            {
                var quality = TheoryChord.GetDiatonicTriadQuality(ionianKey, degree);
                Debug.Log($"  Degree {degree}: {quality}");
            }

            // Test C Dorian
            var dorianKey = new TheoryKey(ScaleMode.Dorian);
            Debug.Log("C Dorian triad qualities:");
            for (int degree = 1; degree <= 7; degree++)
            {
                var quality = TheoryChord.GetDiatonicTriadQuality(dorianKey, degree);
                Debug.Log($"  Degree {degree}: {quality}");
            }

            Debug.Log("=== Test Complete ===");
        }

        /// <summary>
        /// Tests seventh chord parsing and building in C Ionian.
        /// </summary>
        public static void TestCIonianSevenths()
        {
            Debug.Log("=== TheoryChord: C Ionian Seventh Chords Test ===");

            var key = new TheoryKey(ScaleMode.Ionian);
            string[] numerals = { "Imaj7", "ii7", "V7", "viidim7" };

            foreach (var numeral in numerals)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse {numeral}");
                    continue;
                }

                var midi = TheoryChord.BuildChord(key, recipe, 4);
                
                if (midi == null || midi.Length == 0)
                {
                    Debug.LogError($"{numeral} -> Failed to build chord");
                    continue;
                }

                string midiStr = string.Join(", ", midi);
                Debug.Log($"{numeral} -> Degree {recipe.Degree}, Quality {recipe.Quality}, Extension {recipe.Extension}, MIDI: [{midiStr}], Count: {midi.Length}");
            }

            Debug.Log("=== Test Complete ===");
        }

        /// <summary>
        /// Tests explicit seventh-quality parsing, building, and Roman numeral conversion.
        /// </summary>
        [ContextMenu("Test Explicit Seventh Qualities")]
        public static void TestExplicitSeventhQualities()
        {
            Debug.Log("=== TheoryChord: Explicit Seventh Qualities Test ===");

            var key = new TheoryKey(ScaleMode.Ionian);

            // Acceptance criteria A: parsing/building of various seventh numerals.
            string[] numerals =
            {
                "Imaj7",
                "V7",
                "iiø7",
                "viio7",
                "ivm7",
                "VImaj7",
                "#ivdim7"
            };

            foreach (var numeral in numerals)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse numeral '{numeral}'");
                    continue;
                }

                var midi = TheoryChord.BuildChord(key, recipe, 4);
                if (midi == null || midi.Length == 0)
                {
                    Debug.LogError($"{numeral} -> Failed to build chord");
                    continue;
                }

                string midiStr = string.Join(", ", midi);
                string roundTrip = TheoryChord.RecipeToRomanNumeral(recipe);

                Debug.Log($"{numeral} -> Recipe: {recipe}, SeventhQuality: {recipe.SeventhQuality}, MIDI: [{midiStr}], RoundTrip: {roundTrip}");
            }

            // Acceptance criteria B: secondary dominant II7 → D7 (D–F#–A–C) in C Ionian.
            if (TheoryChord.TryParseRomanNumeral(key, "II7", out var ii7Recipe))
            {
                var midi = TheoryChord.BuildChord(key, ii7Recipe, 4);
                string midiStr = midi != null && midi.Length > 0 ? string.Join(", ", midi) : "null";
                Debug.Log($"II7 in C Ionian -> MIDI: [{midiStr}] (expected D–F#–A–C)");
            }
            else
            {
                Debug.LogError("Failed to parse II7");
            }

            // Acceptance criteria C: Imaj7 in C Ionian → C–E–G–B.
            if (TheoryChord.TryParseRomanNumeral(key, "Imaj7", out var imaj7Recipe))
            {
                var midi = TheoryChord.BuildChord(key, imaj7Recipe, 4);
                string midiStr = midi != null && midi.Length > 0 ? string.Join(", ", midi) : "null";
                Debug.Log($"Imaj7 in C Ionian -> MIDI: [{midiStr}] (expected C–E–G–B)");
            }
            else
            {
                Debug.LogError("Failed to parse Imaj7");
            }

            // Acceptance criteria D: existing triad behaviour unchanged for chords without 7th suffix.
            string[] triadNumerals = { "I", "ii", "V", "viidim" };
            foreach (var numeral in triadNumerals)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse triad numeral '{numeral}'");
                    continue;
                }

                var midi = TheoryChord.BuildChord(key, recipe, 4);
                string midiStr = midi != null && midi.Length > 0 ? string.Join(", ", midi) : "null";
                Debug.Log($"Triad {numeral} -> MIDI: [{midiStr}] (should match pre-existing behaviour)");
            }

            Debug.Log("=== Explicit Seventh Qualities Test Complete ===");
        }

        /// <summary>
        /// Tests diatonic seventh chord quality patterns for different modes.
        /// </summary>
        public static void TestDiatonicSeventhQualities()
        {
            Debug.Log("=== TheoryChord: Diatonic Seventh Chord Qualities Test ===");

            var keyIonian = new TheoryKey(ScaleMode.Ionian);
            var keyDorian = new TheoryKey(ScaleMode.Dorian);

            Debug.Log("C Ionian diatonic seventh qualities:");
            LogSeventhPattern(keyIonian);

            Debug.Log("C Dorian diatonic seventh qualities:");
            LogSeventhPattern(keyDorian);

            Debug.Log("=== Test Complete ===");
        }

        /// <summary>
        /// Helper method to log the seventh chord quality pattern for a given key.
        /// </summary>
        private static void LogSeventhPattern(TheoryKey key)
        {
            var sb = new System.Text.StringBuilder();
            for (int degree = 1; degree <= 7; degree++)
            {
                var q = TheoryChord.GetDiatonicSeventhQuality(key, degree);
                sb.Append($"{degree}:{q} ");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Tests chord analysis for various Roman numerals in C Ionian.
        /// </summary>
        [ContextMenu("Test Chord Analysis - CIonian")]
        public static void TestChordAnalysisCIonian()
        {
            Debug.Log("=== TheoryChord: Chord Analysis Test - C Ionian ===");

            var key = new TheoryKey(ScaleMode.Ionian);
            string[] numerals = { "I", "ii", "II", "V7", "II7", "viidim7" };

            foreach (var numeral in numerals)
            {
                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse numeral '{numeral}'");
                    continue;
                }

                var analysis = TheoryChord.AnalyzeChord(key, recipe);
                Debug.Log(
                    $"{numeral}: " +
                    $"ExpectedTriad={analysis.ExpectedTriad}, " +
                    $"IsDiatonicTriad={analysis.IsDiatonicTriad}, " +
                    $"Status={analysis.Status}");
            }

            Debug.Log("=== Test Complete ===");
        }

        /// <summary>
        /// Tests chord spelling in D Ionian to ensure no double accidentals (Fx, Cx, Gx).
        /// Verifies that diatonic chords and borrowed chords are spelled correctly.
        /// </summary>
        public static void TestDIonianSpelling()
        {
            Debug.Log("=== TheoryChord: D Ionian Chord Spelling ===");

            var key = new TheoryKey(2, ScaleMode.Ionian); // D Ionian

            // Test cases: progression with diatonic and borrowed chords
            var testCases = new Dictionary<string, string[]>
            {
                { "Imaj7", new[] { "D", "F#", "A", "C#" } },
                { "IVmaj7", new[] { "G", "B", "D", "F#" } },
                { "iiim7", new[] { "E", "G", "B", "D" } },
                { "V", new[] { "A", "C#", "E" } },
                { "bVI", new[] { "Bb", "D", "F" } }
            };

            bool allPassed = true;
            foreach (var testCase in testCases)
            {
                string numeral = testCase.Key;
                string[] expectedTones = testCase.Value;

                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse numeral '{numeral}'");
                    allPassed = false;
                    continue;
                }

                var spelledTones = TheoryChord.GetSpelledChordTones(key, recipe);
                
                if (spelledTones == null || spelledTones.Count == 0)
                {
                    Debug.LogError($"{numeral} -> Failed to get spelled tones");
                    allPassed = false;
                    continue;
                }

                // Check for double accidentals
                bool hasDoubleAccidental = false;
                foreach (var tone in spelledTones)
                {
                    if (tone.Contains("x") || tone.Contains("bb"))
                    {
                        Debug.LogError($"{numeral} -> Found double accidental in tone: {tone}");
                        hasDoubleAccidental = true;
                        allPassed = false;
                    }
                }

                if (hasDoubleAccidental)
                {
                    continue;
                }

                // Verify expected tones (order may vary, so check if all expected tones are present)
                bool matches = true;
                if (spelledTones.Count != expectedTones.Length)
                {
                    matches = false;
                }
                else
                {
                    for (int i = 0; i < expectedTones.Length; i++)
                    {
                        // Allow for case-insensitive comparison and check if tone is in the list
                        bool found = false;
                        foreach (var spelled in spelledTones)
                        {
                            if (spelled.Equals(expectedTones[i], System.StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    Debug.Log($"{numeral} -> ✓ Spelled correctly: [{string.Join(", ", spelledTones)}]");
                }
                else
                {
                    Debug.LogWarning($"{numeral} -> ✗ Expected [{string.Join(", ", expectedTones)}], got [{string.Join(", ", spelledTones)}]");
                    allPassed = false;
                }
            }

            if (allPassed)
            {
                Debug.Log("=== All D Ionian Spelling Tests Passed ===");
            }
            else
            {
                Debug.LogWarning("=== Some D Ionian Spelling Tests Failed ===");
            }
        }

        /// <summary>
        /// Tests chord spelling in C Ionian with borrowed chords (bVI, bII).
        /// Ensures borrowed chords use correct letters and single accidentals.
        /// </summary>
        public static void TestCIonianBorrowedSpelling()
        {
            Debug.Log("=== TheoryChord: C Ionian Borrowed Chord Spelling ===");

            var key = new TheoryKey(0, ScaleMode.Ionian); // C Ionian

            var testCases = new Dictionary<string, string[]>
            {
                { "bVI", new[] { "Ab", "C", "Eb" } }, // Should be Ab, not G#
                { "bII", new[] { "Db", "F", "Ab" } }  // Should be Db, not C#
            };

            bool allPassed = true;
            foreach (var testCase in testCases)
            {
                string numeral = testCase.Key;
                string[] expectedTones = testCase.Value;

                if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                {
                    Debug.LogError($"Failed to parse numeral '{numeral}'");
                    allPassed = false;
                    continue;
                }

                var spelledTones = TheoryChord.GetSpelledChordTones(key, recipe);
                
                if (spelledTones == null || spelledTones.Count == 0)
                {
                    Debug.LogError($"{numeral} -> Failed to get spelled tones");
                    allPassed = false;
                    continue;
                }

                // Check for double accidentals
                bool hasDoubleAccidental = false;
                foreach (var tone in spelledTones)
                {
                    if (tone.Contains("x") || tone.Contains("bb"))
                    {
                        Debug.LogError($"{numeral} -> Found double accidental in tone: {tone}");
                        hasDoubleAccidental = true;
                        allPassed = false;
                    }
                }

                if (hasDoubleAccidental)
                {
                    continue;
                }

                // Verify root tone matches expected (first element)
                bool rootMatches = false;
                if (spelledTones.Count > 0)
                {
                    string root = spelledTones[0];
                    rootMatches = root.Equals(expectedTones[0], System.StringComparison.OrdinalIgnoreCase);
                }

                if (rootMatches)
                {
                    Debug.Log($"{numeral} -> ✓ Root spelled correctly: {spelledTones[0]} (full chord: [{string.Join(", ", spelledTones)}])");
                }
                else
                {
                    string actualRoot = spelledTones.Count > 0 ? spelledTones[0] : "none";
                    Debug.LogWarning($"{numeral} -> ✗ Expected root '{expectedTones[0]}', got '{actualRoot}' (full chord: [{string.Join(", ", spelledTones)}])");
                    allPassed = false;
                }
            }

            if (allPassed)
            {
                Debug.Log("=== All C Ionian Borrowed Spelling Tests Passed ===");
            }
            else
            {
                Debug.LogWarning("=== Some C Ionian Borrowed Spelling Tests Failed ===");
            }
        }

        /// <summary>
        /// Comprehensive test: ensures no double accidentals across multiple keys and progressions.
        /// </summary>
        public static void TestNoDoubleAccidentals()
        {
            Debug.Log("=== TheoryChord: No Double Accidentals Test ===");

            var keys = new[]
            {
                new TheoryKey(0, ScaleMode.Ionian),  // C Ionian
                new TheoryKey(2, ScaleMode.Ionian),  // D Ionian
                new TheoryKey(4, ScaleMode.Ionian),  // E Ionian
                new TheoryKey(7, ScaleMode.Ionian),  // G Ionian
                new TheoryKey(9, ScaleMode.Ionian)   // A Ionian
            };

            string[] testNumerals = { "I", "IV", "V", "Imaj7", "IVmaj7", "V7", "bVI", "bII", "#iv" };

            bool allPassed = true;
            foreach (var key in keys)
            {
                foreach (var numeral in testNumerals)
                {
                    if (!TheoryChord.TryParseRomanNumeral(key, numeral, out var recipe))
                    {
                        continue; // Skip invalid numerals for this key
                    }

                    var spelledTones = TheoryChord.GetSpelledChordTones(key, recipe);
                    if (spelledTones == null) continue;

                    foreach (var tone in spelledTones)
                    {
                        if (tone.Contains("x") || tone.Contains("bb"))
                        {
                            Debug.LogError($"Key {key}, {numeral} -> Found double accidental: {tone}");
                            allPassed = false;
                        }
                    }
                }
            }

            if (allPassed)
            {
                Debug.Log("=== No Double Accidentals Found (All Tests Passed) ===");
            }
            else
            {
                Debug.LogWarning("=== Double Accidentals Found (Some Tests Failed) ===");
            }
        }
    }
}

