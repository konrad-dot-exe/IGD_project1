#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Sonoria.MusicTheory;

/// <summary>
/// Editor utility to audit chord spellings and catch enharmonic spelling bugs.
/// Ensures that standard triads and 7ths in common Ionian keys are spelled correctly.
/// </summary>
public static class ChordSpellingAudit
{
    /// <summary>
    /// Helper: Parses a Roman numeral and returns the spelled chord tones.
    /// </summary>
    /// <param name="tonicIndex">Tonic index 0..11 (C=0, Db=1, ..., B=11)</param>
    /// <param name="roman">Roman numeral string (e.g., "I", "ii", "V7")</param>
    /// <returns>List of spelled note names (root, 3rd, 5th, [7th]), or null if parsing/spelling failed</returns>
    private static List<string> SpellRomanChord(int tonicIndex, string roman)
    {
        var key = new TheoryKey(tonicIndex, Sonoria.MusicTheory.ScaleMode.Ionian);

        bool ok = TheoryChord.TryParseRomanNumeral(key, roman, out ChordRecipe recipe);
        if (!ok)
        {
            Debug.LogError($"[ChordSpellingAudit] Failed to parse Roman numeral '{roman}' in key index {tonicIndex}.");
            return null;
        }

        var spelled = TheoryChord.GetSpelledChordTones(key, recipe);
        if (spelled == null)
        {
            Debug.LogError($"[ChordSpellingAudit] GetSpelledChordTones returned null for '{roman}' in key index {tonicIndex}.");
            return null;
        }

        return new List<string>(spelled);
    }

    /// <summary>
    /// Helper: Parses a chord symbol and returns the spelled chord tones.
    /// </summary>
    /// <param name="tonicIndex">Tonic index 0..11 (C=0, Db=1, ..., B=11)</param>
    /// <param name="symbol">Chord symbol string (e.g., "C#m", "F#m7")</param>
    /// <returns>List of spelled note names (root, 3rd, 5th, [7th]), or null if parsing/spelling failed</returns>
    private static List<string> SpellChordSymbol(int tonicIndex, string symbol)
    {
        var key = new TheoryKey(tonicIndex, Sonoria.MusicTheory.ScaleMode.Ionian);

        bool ok = TheoryChord.TryParseChordSymbol(key, symbol, out ChordRecipe recipe, out string errorMessage);
        if (!ok)
        {
            Debug.LogError($"[ChordSpellingAudit] Failed to parse chord symbol '{symbol}' in key index {tonicIndex}: {errorMessage}");
            return null;
        }

        var spelled = TheoryChord.GetSpelledChordTones(key, recipe);
        if (spelled == null)
        {
            Debug.LogError($"[ChordSpellingAudit] GetSpelledChordTones returned null for symbol '{symbol}' in key index {tonicIndex}.");
            return null;
        }

        return new List<string>(spelled);
    }

    /// <summary>
    /// Helper: Compares actual spelling against expected spelling and logs errors if they don't match.
    /// </summary>
    /// <param name="actual">Actual spelled chord tones</param>
    /// <param name="expected">Expected note names (root, 3rd, 5th, [7th])</param>
    /// <returns>True if spelling matches, false otherwise</returns>
    private static bool CompareSpelling(List<string> actual, params string[] expected)
    {
        if (actual == null)
            return false;

        if (actual.Count != expected.Length)
        {
            Debug.LogError($"[ChordSpellingAudit] Expected {expected.Length} notes, got {actual.Count}. " +
                           $"Expected: {string.Join(" ", expected)} | Actual: {string.Join(" ", actual)}");
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                Debug.LogError($"[ChordSpellingAudit] Mismatch at index {i}. " +
                               $"Expected: {expected[i]} | Actual: {actual[i]} | Full: {string.Join(" ", actual)}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Audits chord spellings for C Ionian.
    /// </summary>
    private static int Audit_C_Ionian()
    {
        int failures = 0;
        int t = 0; // C

        // Triads
        if (!CompareSpelling(SpellRomanChord(t, "I"), "C", "E", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "D", "F", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "E", "G", "B")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "F", "A", "C")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "G", "B", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "A", "C", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "B", "D", "F")) failures++;

        // Sevenths
        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "D", "F", "A", "C")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "G", "B", "D", "F")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "A", "C", "E", "G")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for G Ionian.
    /// </summary>
    private static int Audit_G_Ionian()
    {
        int failures = 0;
        int t = 7; // G

        if (!CompareSpelling(SpellRomanChord(t, "I"), "G", "B", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "A", "C", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "B", "D", "F#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "C", "E", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "D", "F#", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "E", "G", "B")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "F#", "A", "C")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "A", "C", "E", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "D", "F#", "A", "C")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "E", "G", "B", "D")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for D Ionian.
    /// </summary>
    private static int Audit_D_Ionian()
    {
        int failures = 0;
        int t = 2; // D

        if (!CompareSpelling(SpellRomanChord(t, "I"), "D", "F#", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "E", "G", "B")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "F#", "A", "C#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "G", "B", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "A", "C#", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "B", "D", "F#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "C#", "E", "G")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "E", "G", "B", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "A", "C#", "E", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "B", "D", "F#", "A")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for A Ionian.
    /// </summary>
    private static int Audit_A_Ionian()
    {
        int failures = 0;
        int t = 9; // A

        if (!CompareSpelling(SpellRomanChord(t, "I"), "A", "C#", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "B", "D", "F#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "C#", "E", "G#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "D", "F#", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "E", "G#", "B")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "F#", "A", "C#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "G#", "B", "D")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "B", "D", "F#", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "E", "G#", "B", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "F#", "A", "C#", "E")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for E Ionian.
    /// </summary>
    private static int Audit_E_Ionian()
    {
        int failures = 0;
        int t = 4; // E

        if (!CompareSpelling(SpellRomanChord(t, "I"), "E", "G#", "B")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "F#", "A", "C#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "G#", "B", "D#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "A", "C#", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "B", "D#", "F#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "C#", "E", "G#")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "D#", "F#", "A")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "F#", "A", "C#", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "B", "D#", "F#", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "C#", "E", "G#", "B")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for F Ionian (flat key).
    /// </summary>
    private static int Audit_F_Ionian()
    {
        int failures = 0;
        int t = 5; // F

        if (!CompareSpelling(SpellRomanChord(t, "I"), "F", "A", "C")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "G", "Bb", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "A", "C", "E")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "Bb", "D", "F")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "C", "E", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "D", "F", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "E", "G", "Bb")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "G", "Bb", "D", "F")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "C", "E", "G", "Bb")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "D", "F", "A", "C")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord spellings for Bb Ionian (flat key).
    /// </summary>
    private static int Audit_Bb_Ionian()
    {
        int failures = 0;
        int t = 10; // Bb

        if (!CompareSpelling(SpellRomanChord(t, "I"), "Bb", "D", "F")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "ii"), "C", "Eb", "G")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "iii"), "D", "F", "A")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "IV"), "Eb", "G", "Bb")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V"), "F", "A", "C")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi"), "G", "Bb", "D")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "viio"), "A", "C", "Eb")) failures++;

        if (!CompareSpelling(SpellRomanChord(t, "ii7"), "C", "Eb", "G", "Bb")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "V7"), "F", "A", "C", "Eb")) failures++;
        if (!CompareSpelling(SpellRomanChord(t, "vi7"), "G", "Bb", "D", "F")) failures++;

        return failures;
    }

    /// <summary>
    /// Audits chord symbol spellings in A Ionian (spot check).
    /// </summary>
    private static int Audit_A_Ionian_Symbols()
    {
        int failures = 0;
        int t = 9; // A

        if (!CompareSpelling(SpellChordSymbol(t, "C#m"), "C#", "E", "G#")) failures++;
        if (!CompareSpelling(SpellChordSymbol(t, "F#m7"), "F#", "A", "C#", "E")) failures++;
        if (!CompareSpelling(SpellChordSymbol(t, "Bm"), "B", "D", "F#")) failures++;
        if (!CompareSpelling(SpellChordSymbol(t, "E"), "E", "G#", "B")) failures++;

        return failures;
    }

    /// <summary>
    /// Menu item: Runs the complete chord spelling audit and reports results to the Console.
    /// </summary>
    [MenuItem("Tools/Chord Lab/Run Chord Spelling Audit")]
    public static void RunChordSpellingAudit()
    {
        Debug.Log("[ChordSpellingAudit] Starting chord spelling audit...");

        int failures = 0;

        // Call per-key audit methods, accumulate failures
        failures += Audit_C_Ionian();
        failures += Audit_G_Ionian();
        failures += Audit_D_Ionian();
        failures += Audit_A_Ionian();
        failures += Audit_E_Ionian();
        failures += Audit_F_Ionian();
        failures += Audit_Bb_Ionian();
        failures += Audit_A_Ionian_Symbols();

        if (failures == 0)
        {
            Debug.Log("[ChordSpellingAudit] ✓ All chord spelling checks passed.");
        }
        else
        {
            Debug.LogError($"[ChordSpellingAudit] ✗ Completed with {failures} failures. See log above for details.");
        }
    }
}
#endif

