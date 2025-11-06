using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Theory-level checks (musical semantics) organized as small rules.
/// Focus: scales (interval formulas, root/key parsing).
/// </summary>
public static class TheoryValidator
{
    // ---------- Rule plumbing ----------
    public interface ITheoryRule
    {
        IEnumerable<MusicValidator.Issue> Evaluate(MusicData md);
    }

    public class TheoryProfile
    {
        public string Name;
        public List<ITheoryRule> Rules = new List<ITheoryRule>();
        public TheoryProfile(string name) { Name = name; }
    }

    public static class Profiles
    {
        // “Scale” theory profile — add more rules here over time.
        public static readonly TheoryProfile Scale = new TheoryProfile("Scale")
        {
            Rules = new List<ITheoryRule>
            {
                new RootPcMatchesScaleTextRule(),
                new IntervalsMatchScaleKindRule(),
                new FinalOctaveClosureRule()
            }
        };
    }

    public static MusicValidator.Report Run(MusicData md, TheoryProfile profile)
    {
        var r = new MusicValidator.Report();
        if (md == null) { r.Add(MusicValidator.Severity.Error, "NULL_DATA", "MusicData is null"); return r; }
        if (md.intervals_from_root == null || md.intervals_from_root.Count == 0)
        {
            r.Add(MusicValidator.Severity.Error, "NO_INTERVALS", "intervals_from_root missing/empty");
            return r;
        }

        foreach (var rule in profile.Rules)
            foreach (var issue in rule.Evaluate(md))
                r.Issues.Add(issue);

        return r;
    }

    // ---------- Helpers ----------
    static readonly Dictionary<string, int> NameToPc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        {"c",0}, {"b#",0},
        {"c#",1}, {"db",1},
        {"d",2},
        {"d#",3}, {"eb",3},
        {"e",4}, {"fb",4},
        {"f",5}, {"e#",5},
        {"f#",6}, {"gb",6},
        {"g",7},
        {"g#",8}, {"ab",8},
        {"a",9},
        {"a#",10}, {"bb",10},
        {"b",11}, {"cb",11}
    };

    static int? ParseRootPcFromScaleText(string scaleText)
    {
        if (string.IsNullOrEmpty(scaleText)) return null;
        // Expect things like "C Major", "Ab Lydian", "F# Dorian", allow single-token like "Chromatic" (returns null).
        var parts = scaleText.Trim().Split(new[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        // First token is usually root like "C", "F#", "Bb"
        var t0 = parts[0].Trim().ToLowerInvariant();
        if (NameToPc.ContainsKey(t0)) return NameToPc[t0];
        return null;
    }

    static string NormalizeKind(string kindRaw)
    {
        var s = (kindRaw ?? "").Trim().ToLowerInvariant();
        return s
            .Replace(" (h-w)","").Replace("(h-w)","")
            .Replace(" (w-h)","").Replace("(w-h)","")
            .Replace("scale","").Trim();
    }

    // Canonical interval formulas (semitones from root, ascending, including octave if typical).
    // You can extend this safely over time.
    static bool TryGetIntervalsForKind(string rawKind, out int[] intervals, out bool allowEitherOctatonic)
    {
        allowEitherOctatonic = false;
        var k = NormalizeKind(rawKind);

        // 7-note diatonic + octave
        if (k == "ionian" || k == "major") { intervals = new[]{0,2,4,5,7,9,11,12}; return true; }
        if (k == "dorian") { intervals = new[]{0,2,3,5,7,9,10,12}; return true; }
        if (k == "phrygian") { intervals = new[]{0,1,3,5,7,8,10,12}; return true; }
        if (k == "lydian") { intervals = new[]{0,2,4,6,7,9,11,12}; return true; }
        if (k == "mixolydian") { intervals = new[]{0,2,4,5,7,9,10,12}; return true; }
        if (k == "aeolian" || k == "minor") { intervals = new[]{0,2,3,5,7,8,10,12}; return true; }
        if (k == "locrian") { intervals = new[] { 0, 1, 3, 5, 6, 8, 10, 12 }; return true; }
        
        // harmonic minor modes
        if (k == "dorian #4" || k == "dorian sharp4" || k == "dorian ♯4")
        { intervals = new[]{0,2,3,6,7,9,10,12}; return true; }

        // Augmented (hexatonic): m3, M2, m3, M2, m3
        if (k == "augmented" || k == "augmented scale" || k == "hexatonic augmented")
        { intervals = new[]{0,3,4,7,8,11,12}; return true; }

        // Common 6/8/12-note sets
        if (k == "whole-tone" || k == "wholetone") { intervals = new[]{0,2,4,6,8,10,12}; return true; }
        if (k == "chromatic") { intervals = new[]{0,1,2,3,4,5,6,7,8,9,10,11,12}; return true; }

        // Octatonic: accept either half–whole or whole–half (many users say "octatonic" only)
        if (k.StartsWith("octatonic")) { intervals = new[]{0,1,3,4,6,7,9,10,12}; allowEitherOctatonic = true; return true; }
        if (k == "octatonic h-w" || k == "half whole diminished" || k == "half-whole diminished") { intervals = new[]{0,1,3,4,6,7,9,10,12}; return true; }
        if (k == "octatonic w-h" || k == "whole half diminished" || k == "whole-half diminished") { intervals = new[]{0,2,3,5,6,8,9,11,12}; return true; }

        // Jazz/Minor variants
        if (k == "melodic minor" || k == "jazz minor") { intervals = new[]{0,2,3,5,7,9,11,12}; return true; }
        if (k == "harmonic minor") { intervals = new[]{0,2,3,5,7,8,11,12}; return true; }
        if (k == "lydian dominant" || k == "lydian b7") { intervals = new[] { 0, 2, 4, 6, 7, 9, 10, 12 }; return true; }
        // Super-Locrian / Altered (7th mode of melodic minor)
        if (k == "super locrian" || k == "super-locrian" || k == "altered" || k == "altered scale")
        { intervals = new[]{0,1,3,4,6,8,10,12}; return true; }
        if (k == "hungarian minor" || k == "gypsy minor" || k == "hungarian gypsy")
        { intervals = new[]{0,2,3,6,7,8,11,12}; return true; }

        // Fallback: unknown kind → don’t error; rule will yield only an info.
        intervals = null;
        return false;
    }

    // ---------- Rules ----------

    /// <summary>Confirm the realized root pitch class matches the text root (if any).</summary>
    class RootPcMatchesScaleTextRule : ITheoryRule
    {
        public IEnumerable<MusicValidator.Issue> Evaluate(MusicData md)
        {
            var textPc = ParseRootPcFromScaleText(md.meta != null ? md.meta.scale_text : null); // set during realization:contentReference[oaicite:2]{index=2}
            if (!textPc.HasValue) yield break; // no explicit root in text → skip

            if (md.notes == null || md.notes.Count == 0) yield break;
            var realizedPc = md.notes[0].pitch % 12;

            if (realizedPc != textPc.Value)
                yield return new MusicValidator.Issue(
                    MusicValidator.Severity.Warning, "ROOT_PC_MISMATCH",
                    "Realized root pitch class does not match scale_text root");
        }
    }

    /// <summary>Compare intervals_from_root against the formula for the declared scale_kind.</summary>
    class IntervalsMatchScaleKindRule : ITheoryRule
    {
        public IEnumerable<MusicValidator.Issue> Evaluate(MusicData md)
        {
            string kind = md.meta != null ? md.meta.scale_kind : null;  // provided by interpretation & stored in realization:contentReference[oaicite:3]{index=3}
            int[] want;
            bool eitherOct = false;

            if (!TryGetIntervalsForKind(kind, out want, out eitherOct) || want == null)
            {
                yield return new MusicValidator.Issue(
                    MusicValidator.Severity.Info, "UNKNOWN_KIND",
                    "No canonical interval formula for this scale_kind; skipping pattern check");
                yield break;
            }

            var got = md.intervals_from_root.ToArray();

            // For octatonic “either” mode, allow match against both H–W and W–H
            if (eitherOct)
            {
                var hw = new[]{0,1,3,4,6,7,9,10,12};
                var wh = new[]{0,2,3,5,6,8,9,11,12};
                if (!ArraysEqual(got, hw) && !ArraysEqual(got, wh))
                    yield return new MusicValidator.Issue(
                        MusicValidator.Severity.Error, "INTERVAL_PATTERN",
                        "intervals_from_root do not match an accepted octatonic pattern");
                yield break;
            }

            if (!ArraysEqual(got, want))
                yield return new MusicValidator.Issue(
                    MusicValidator.Severity.Error, "INTERVAL_PATTERN",
                    "intervals_from_root do not match the canonical pattern for scale_kind");
        }

        static bool ArraysEqual(int[] a, int[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>Final note should resolve to an octave of the root (mod 12), for common scalar realizations.</summary>
    class FinalOctaveClosureRule : ITheoryRule
    {
        public IEnumerable<MusicValidator.Issue> Evaluate(MusicData md)
        {
            if (md.notes == null || md.notes.Count < 2) yield break;
            int root = md.notes[0].pitch;
            int final = md.notes[md.notes.Count - 1].pitch;
            if (((final - root) % 12) != 0)
                yield return new MusicValidator.Issue(
                    MusicValidator.Severity.Warning, "NO_OCTAVE_CLOSE",
                    "Scale does not end on an octave of the root (mod 12)");
        }
    }
}
