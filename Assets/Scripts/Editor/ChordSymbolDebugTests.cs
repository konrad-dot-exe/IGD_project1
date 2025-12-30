#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sonoria.MusicTheory;

namespace Sonoria.Editor
{
    /// <summary>
    /// Debug test harness for chord symbol formatting, specifically for flat-root borrowed chords.
    /// </summary>
    public static class ChordSymbolDebugTests
    {
        [MenuItem("Tools/Chord Lab/Debug/Print Flat Chord Symbols")]
        private static void DebugPrintFlatChordSymbols()
        {
            // Key: C Ionian
            var key = new TheoryKey(0, Sonoria.MusicTheory.ScaleMode.Ionian);

            var recipes = new List<ChordRecipe>();

            // bVII major triad in C (Bb)
            if (TheoryChord.TryParseRomanNumeral(key, "bVII", out var r1))
            {
                recipes.Add(r1);
            }
            else
            {
                Debug.LogError("[ChordSymbolTest] Failed to parse bVII");
            }

            // bVII7 in C (Bb7)
            if (TheoryChord.TryParseRomanNumeral(key, "bVII7", out var r2))
            {
                recipes.Add(r2);
            }
            else
            {
                Debug.LogError("[ChordSymbolTest] Failed to parse bVII7");
            }

            // bIII major triad in C (Eb)
            if (TheoryChord.TryParseRomanNumeral(key, "bIII", out var r3))
            {
                recipes.Add(r3);
            }
            else
            {
                Debug.LogError("[ChordSymbolTest] Failed to parse bIII");
            }

            Debug.Log("[ChordSymbolTest] Testing flat-root borrowed chord symbols:");
            Debug.Log("============================================================");

            foreach (var recipe in recipes)
            {
                // Compute root name from pitch class (the correct way)
                int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
                if (rootPc >= 0)
                {
                    rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
                    if (rootPc < 0) rootPc += 12;
                }
                else
                {
                    rootPc = 0;
                }

                string rootName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                    key,
                    recipe.Degree,
                    recipe.RootSemitoneOffset);

                // Get base symbol
                string baseSymbol = TheoryChord.GetChordSymbol(key, recipe, rootName, null);

                // Get symbol with no tensions
                var emptyTensions = new List<ChordTension>();
                string symbolWithTensions = TheoryChord.GetChordSymbolWithTensions(
                    key, recipe, rootName, null, emptyTensions);

                // Get Roman numeral for display
                string romanNumeral = TheoryChord.RecipeToRomanNumeral(key, recipe);

                Debug.Log($"[ChordSymbolTest] Roman={romanNumeral} | " +
                          $"Degree={recipe.Degree} Offset={recipe.RootSemitoneOffset} | " +
                          $"RootPC={rootPc} RootName='{rootName}' | " +
                          $"BaseSymbol='{baseSymbol}' | " +
                          $"FinalSymbol='{symbolWithTensions}'");

                // Verify expected results
                bool isCorrect = false;
                if (romanNumeral == "bVII" && symbolWithTensions == "Bb")
                    isCorrect = true;
                else if (romanNumeral == "bVII7" && symbolWithTensions == "Bb7")
                    isCorrect = true;
                else if (romanNumeral == "bIII" && symbolWithTensions == "Eb")
                    isCorrect = true;

                if (!isCorrect)
                {
                    Debug.LogWarning($"[ChordSymbolTest] ❌ FAILED: Expected correct symbol for {romanNumeral}, got '{symbolWithTensions}'");
                }
                else
                {
                    Debug.Log($"[ChordSymbolTest] ✓ PASSED: {romanNumeral} → '{symbolWithTensions}'");
                }
            }

            Debug.Log("============================================================");
        }
    }
}
#endif

