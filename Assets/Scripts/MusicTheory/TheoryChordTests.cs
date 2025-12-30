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

        /// <summary>
        /// Tests that "Vb9" is normalized to "V7b9" (dominant + b9 implies 7th).
        /// Verifies that both produce the same chord recipe and voicing behavior.
        /// </summary>
        public static void TestVb9Normalization()
        {
            Debug.Log("=== TheoryChord: Vb9 Normalization Test ===");
            var key = new TheoryKey(ScaleMode.Ionian);

            // Parse both variants
            if (!TheoryChord.TryParseRomanNumeral(key, "Vb9", out var recipeVb9))
            {
                Debug.LogError("Failed to parse 'Vb9'");
                return;
            }

            if (!TheoryChord.TryParseRomanNumeral(key, "V7b9", out var recipeV7b9))
            {
                Debug.LogError("Failed to parse 'V7b9'");
                return;
            }

            // Verify they produce the same recipe
            bool recipesMatch = recipeVb9.Degree == recipeV7b9.Degree &&
                               recipeVb9.Quality == recipeV7b9.Quality &&
                               recipeVb9.Extension == recipeV7b9.Extension &&
                               recipeVb9.SeventhQuality == recipeV7b9.SeventhQuality &&
                               recipeVb9.RequestedExtensions.TensionFlat9 == recipeV7b9.RequestedExtensions.TensionFlat9 &&
                               recipeVb9.RequestedExtensions.Tension9 == recipeV7b9.RequestedExtensions.Tension9;

            if (!recipesMatch)
            {
                Debug.LogError($"Vb9 and V7b9 produce different recipes:\n" +
                             $"  Vb9:  Degree={recipeVb9.Degree}, Quality={recipeVb9.Quality}, Extension={recipeVb9.Extension}, " +
                             $"SeventhQuality={recipeVb9.SeventhQuality}, TensionFlat9={recipeVb9.RequestedExtensions.TensionFlat9}\n" +
                             $"  V7b9: Degree={recipeV7b9.Degree}, Quality={recipeV7b9.Quality}, Extension={recipeV7b9.Extension}, " +
                             $"SeventhQuality={recipeV7b9.SeventhQuality}, TensionFlat9={recipeV7b9.RequestedExtensions.TensionFlat9}");
                return;
            }

            // Verify Vb9 was normalized to include 7th
            if (recipeVb9.Extension != ChordExtension.Seventh || recipeVb9.SeventhQuality != SeventhQuality.Dominant7)
            {
                Debug.LogError($"Vb9 was not normalized: Extension={recipeVb9.Extension}, SeventhQuality={recipeVb9.SeventhQuality} " +
                             $"(expected Extension=Seventh, SeventhQuality=Dominant7)");
                return;
            }

            // Verify b9 tension is present
            if (!recipeVb9.RequestedExtensions.TensionFlat9)
            {
                Debug.LogError("Vb9 does not have TensionFlat9 flag set");
                return;
            }

            // Get chord tone pitch classes for both
            var chordEventVb9 = new ChordEvent { Key = key, Recipe = recipeVb9 };
            var chordEventV7b9 = new ChordEvent { Key = key, Recipe = recipeV7b9 };
            
            var chordTonePcsVb9 = TheoryVoicing.GetChordTonePitchClasses(chordEventVb9);
            var chordTonePcsV7b9 = TheoryVoicing.GetChordTonePitchClasses(chordEventV7b9);

            // Verify chord tone pitch classes match
            if (chordTonePcsVb9.Count != chordTonePcsV7b9.Count)
            {
                Debug.LogError($"Chord tone PC count mismatch: Vb9={chordTonePcsVb9.Count}, V7b9={chordTonePcsV7b9.Count}");
                return;
            }

            bool pcsMatch = true;
            for (int i = 0; i < chordTonePcsVb9.Count; i++)
            {
                if (chordTonePcsVb9[i] != chordTonePcsV7b9[i])
                {
                    pcsMatch = false;
                    break;
                }
            }

            if (!pcsMatch)
            {
                Debug.LogError($"Chord tone PCs don't match:\n" +
                             $"  Vb9:  [{string.Join(", ", chordTonePcsVb9)}]\n" +
                             $"  V7b9: [{string.Join(", ", chordTonePcsV7b9)}]");
                return;
            }

            Debug.Log($"✓ Vb9 normalization test PASSED:\n" +
                     $"  Vb9 recipe:  Degree={recipeVb9.Degree}, Extension={recipeVb9.Extension}, " +
                     $"SeventhQuality={recipeVb9.SeventhQuality}, TensionFlat9={recipeVb9.RequestedExtensions.TensionFlat9}\n" +
                     $"  V7b9 recipe: Degree={recipeV7b9.Degree}, Extension={recipeV7b9.Extension}, " +
                     $"SeventhQuality={recipeV7b9.SeventhQuality}, TensionFlat9={recipeV7b9.RequestedExtensions.TensionFlat9}\n" +
                     $"  Chord tone PCs: [{string.Join(", ", chordTonePcsVb9)}]");
        }

        /// <summary>
        /// Tests that "V#9" is normalized to "V7#9" (dominant + #9 implies 7th).
        /// Verifies that both produce the same chord recipe and voicing behavior.
        /// </summary>
        public static void TestVSharp9Normalization()
        {
            Debug.Log("=== TheoryChord: V#9 Normalization Test ===");
            var key = new TheoryKey(ScaleMode.Ionian);

            // Parse both variants
            if (!TheoryChord.TryParseRomanNumeral(key, "V#9", out var recipeVSharp9))
            {
                Debug.LogError("Failed to parse 'V#9'");
                return;
            }

            if (!TheoryChord.TryParseRomanNumeral(key, "V7#9", out var recipeV7Sharp9))
            {
                Debug.LogError("Failed to parse 'V7#9'");
                return;
            }

            // Verify they produce the same recipe
            bool recipesMatch = recipeVSharp9.Degree == recipeV7Sharp9.Degree &&
                               recipeVSharp9.Quality == recipeV7Sharp9.Quality &&
                               recipeVSharp9.Extension == recipeV7Sharp9.Extension &&
                               recipeVSharp9.SeventhQuality == recipeV7Sharp9.SeventhQuality &&
                               recipeVSharp9.RequestedExtensions.TensionSharp9 == recipeV7Sharp9.RequestedExtensions.TensionSharp9 &&
                               recipeVSharp9.RequestedExtensions.Tension9 == recipeV7Sharp9.RequestedExtensions.Tension9 &&
                               recipeVSharp9.RequestedExtensions.TensionFlat9 == recipeV7Sharp9.RequestedExtensions.TensionFlat9;

            if (!recipesMatch)
            {
                Debug.LogError($"V#9 and V7#9 produce different recipes:\n" +
                             $"  V#9:  Degree={recipeVSharp9.Degree}, Quality={recipeVSharp9.Quality}, Extension={recipeVSharp9.Extension}, " +
                             $"SeventhQuality={recipeVSharp9.SeventhQuality}, TensionSharp9={recipeVSharp9.RequestedExtensions.TensionSharp9}\n" +
                             $"  V7#9: Degree={recipeV7Sharp9.Degree}, Quality={recipeV7Sharp9.Quality}, Extension={recipeV7Sharp9.Extension}, " +
                             $"SeventhQuality={recipeV7Sharp9.SeventhQuality}, TensionSharp9={recipeV7Sharp9.RequestedExtensions.TensionSharp9}");
                return;
            }

            // Verify V#9 was normalized to include 7th
            if (recipeVSharp9.Extension != ChordExtension.Seventh || recipeVSharp9.SeventhQuality != SeventhQuality.Dominant7)
            {
                Debug.LogError($"V#9 was not normalized: Extension={recipeVSharp9.Extension}, SeventhQuality={recipeVSharp9.SeventhQuality} " +
                             $"(expected Extension=Seventh, SeventhQuality=Dominant7)");
                return;
            }

            // Verify #9 tension is present
            if (!recipeVSharp9.RequestedExtensions.TensionSharp9)
            {
                Debug.LogError("V#9 does not have TensionSharp9 flag set");
                return;
            }

            // Get chord tone pitch classes for both
            var chordEventVSharp9 = new ChordEvent { Key = key, Recipe = recipeVSharp9 };
            var chordEventV7Sharp9 = new ChordEvent { Key = key, Recipe = recipeV7Sharp9 };

            var chordTonePcsVSharp9 = TheoryVoicing.GetChordTonePitchClasses(chordEventVSharp9);
            var chordTonePcsV7Sharp9 = TheoryVoicing.GetChordTonePitchClasses(chordEventV7Sharp9);

            // Verify chord tone pitch classes match
            if (chordTonePcsVSharp9.Count != chordTonePcsV7Sharp9.Count)
            {
                Debug.LogError($"Chord tone PC count mismatch: V#9={chordTonePcsVSharp9.Count}, V7#9={chordTonePcsV7Sharp9.Count}");
                return;
            }

            bool pcsMatch = true;
            for (int i = 0; i < chordTonePcsVSharp9.Count; i++)
            {
                if (chordTonePcsVSharp9[i] != chordTonePcsV7Sharp9[i])
                {
                    pcsMatch = false;
                    break;
                }
            }

            if (!pcsMatch)
            {
                Debug.LogError($"Chord tone PCs don't match:\n" +
                             $"  V#9:  [{string.Join(", ", chordTonePcsVSharp9)}]\n" +
                             $"  V7#9: [{string.Join(", ", chordTonePcsV7Sharp9)}]");
                return;
            }

            Debug.Log($"✓ V#9 normalization test PASSED:\n" +
                     $"  V#9 recipe:  Degree={recipeVSharp9.Degree}, Extension={recipeVSharp9.Extension}, " +
                     $"SeventhQuality={recipeVSharp9.SeventhQuality}, TensionSharp9={recipeVSharp9.RequestedExtensions.TensionSharp9}\n" +
                     $"  V7#9 recipe: Degree={recipeV7Sharp9.Degree}, Extension={recipeV7Sharp9.Extension}, " +
                     $"SeventhQuality={recipeV7Sharp9.SeventhQuality}, TensionSharp9={recipeV7Sharp9.RequestedExtensions.TensionSharp9}\n" +
                     $"  Chord tone PCs: [{string.Join(", ", chordTonePcsVSharp9)}]");
        }
    }
}

