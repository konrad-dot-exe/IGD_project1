using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class MusicOrchestrator : MonoBehaviour
{
    [Header("Refs")]
    public LLMManager llm;                  // your existing provider instance
    public MusicDataController dataCtrl;    // your existing data holder

    [Header("Defaults")]
    public int defaultTempoBpm = 120;
    public string defaultTimeSig = "4/4";
    public int defaultVelocity = 90;
    public int defaultChannel = 1;

    // ---------- Public API ----------

    /// <summary>
    /// Milestone 1: Robust scale generation (Interpret -> Realize -> Validate -> (opt) Audit)
    /// Returns true on success. On success, dataCtrl.Current is set.
    /// </summary>
    public async Task<(bool ok, string error)> GenerateScaleAsync(
        string scaleText,
        Action<string> log = null,
        CancellationToken ct = default)
    {
        log ??= _ => { };

        // Step 1: Interpret (root + intervals only)
        var interp = await InterpretScaleAsync(scaleText, log, ct);
        if (interp == null) return (false, "interpretation failed");

        // NEW: Guard root — if text has an explicit root, force root_pc to match it
        var textPc = TryParseRootPcFromScaleText(scaleText);
        if (textPc.HasValue && interp.root_pc != textPc.Value)
        {
            log("<color=#ffaa00>[warn]</color> interpreter root_pc != text root; overriding with text root.");
            interp.root_pc = textPc.Value;
        }

        // Step 2: Realize MIDI locally
        var md = RealizeScale(scaleText, interp);
       
       // Technical validation
        var report = MusicValidator.ValidateScale(md);
        if (!report.Ok)
        {
            log($"<color=#ffaa00>[warn]</color> validation failed; attempting audit…\n{report}");
            // Keep your existing audit flow; afterwards, re-validate:
            var fixedPitches = await AuditScaleAsync(60 + interp.root_pc, interp.intervals_from_root, md, log, ct);
            if (fixedPitches != null && fixedPitches.Count == md.notes.Count)
            {
                for (int i = 0; i < fixedPitches.Count; i++) md.notes[i].pitch = fixedPitches[i];
                var report2 = MusicValidator.ValidateScale(md);
                if (!report2.Ok)
                    log("<color=#ff6666>[error]</color> audit returned inconsistent data; using original.");
                else
                    log("<color=#cccc66>[info]</color> audit corrected pitches.\n" + report2);
            }
            else
            {
                log("<color=#ff6666>[error]</color> audit failed; using original.");
            }
        }

        // === Theory (semantic) pass for scales ===
        var theoryReport = TheoryValidator.Run(md, TheoryValidator.Profiles.Scale);

        // Log the outcome (do not block the pipeline yet; just inform)
        if (!theoryReport.Ok)
        {
            log("<color=#ffaa00>[theory]</color>\n" + theoryReport.ToString());
        }

        bool needRetry =
            theoryReport.Issues.Exists(i => i.Code == "INTERVAL_PATTERN") ||
            theoryReport.Issues.Exists(i => i.Code == "UNKNOWN_KIND");

        if (needRetry)
        {
            log("<color=#ffaa00>[theory]</color> mismatch/unknown; strict canonical retry…");

            // NEW: create a critic tied to your existing LLMManager (field 'llm')
            var critic = new TheoryCritic(llm);

            // Call the instance method (no 'llm' argument here)
            var fixedIntervals = await critic.StrictReinterpretIntervalsAsync(scaleText, ct);

            if (fixedIntervals != null && fixedIntervals.Length > 0)
            {
                // rebuild purely locally
                var interp2 = new InterpScale
                {
                    root_pc = interp.root_pc,                 // (possibly override earlier if you add a root guard)
                    scale_kind = interp.scale_kind,
                    intervals_from_root = new System.Collections.Generic.List<int>(fixedIntervals)
                };
                md = RealizeScale(scaleText, interp2);

                var structural2 = MusicValidator.ValidateScale(md);
                var theory2 = TheoryValidator.Run(md, TheoryValidator.Profiles.Scale);

                if (structural2.Ok && theory2.Ok)
                    log("<color=#66cc66>[theory]</color> strict retry corrected pattern.");
                else
                    log("<color=#ff6666>[theory]</color> strict retry still inconsistent; keeping original.");
            }
            
            // log rationale and summary
            var cr = await critic.CritiqueAsync(md, "Scale", ct);
            if (!string.IsNullOrEmpty(cr?.rationale))
                log($"<color=#888>[llm-critique]</color> {cr.rationale}");
            if (!string.IsNullOrEmpty(cr?.summary))
                log($"<color=#888>[llm-critique]</color> {cr.summary}");
        }

        dataCtrl.Clear();
        // save into controller (pretty clamping happens inside controller)
        var json = JsonConvert.SerializeObject(md);
        if (!dataCtrl.LoadFromJson(json, out var err))
        {
            log("<color=#ff6666>[error]</color> controller parse failed: " + err);
            return (false, err);
        }

        return (true, null);
    }

    // ---------- Internals ----------

    [Serializable]
    class InterpScale
    {
        public int root_pc; // 0=C ... 11=B
        public string scale_kind;
        public List<int> intervals_from_root;
        public string rationale;
    }

    async Task<InterpScale> InterpretScaleAsync(string scaleText, Action<string> log, CancellationToken ct)
    {
        string system =
        "Output ONLY valid JSON. No markdown, tags, or prose.\n" +
        "Return exactly: { \"root_pc\": int, \"scale_kind\": string, \"intervals_from_root\": int[], \"rationale\": string }.\n" +
        "root_pc is 0..11 where 0=C, 1=C#, 2=D, 3=Eb, 4=E, 5=F, 6=F#, 7=G, 8=Ab, 9=A, 10=Bb, 11=B.\n" +
        "intervals_from_root are integer semitone offsets (e.g., 0,2,3,5,7,9,10,12). No #, ♯, b, ♭, or strings.\n" +
        "rationale is a brief (<120 chars) plain-text justification describing how the scale was derived (no chain-of-thought).\n" +
        "\n" +
        "Examples:\n" +
        "  Major:      [0,2,4,5,7,9,11,12]\n" +
        "  Dorian:     [0,2,3,5,7,9,10,12]\n" +
        "  Aeolian:    [0,2,3,5,7,8,10,12]\n" +
        "  Whole-tone: [0,2,4,6,8,10,12]\n" +
        "  Octatonic(H-W): [0,1,3,4,6,7,9,10,12]\n" +
        "  Melodic Minor:     [0,2,3,5,7,9,11,12]\n" +
        "  Harmonic Minor:    [0,2,3,5,7,8,11,12]\n" +
        "  Lydian Dominant:   [0,2,4,6,7,9,10,12]\n" +
        "  Super Locrian (altered):     [0,1,3,4,6,8,10,12]\n" +
        "  Dorian #4:         [0,2,3,6,7,9,10,12]\n" +
        "  Augmented (hex):   [0,3,4,7,8,11,12]\n" +
        "  Hungarian Minor:   [0,2,3,6,7,8,11,12]\n" +
        "\n" +
        "Return the FULL span up to the octave when a scale implies it.\n" +
        "If the user asks for a hexatonic scale (e.g., Augmented), return 6 steps + octave (7 numbers total).\n";

        string user =
            $"Interpret scale_text = \"{scaleText}\" and return JSON only with these fields (root_pc, scale_kind, intervals_from_root, rationale).";

        string reply;
        try
        {
            reply = await llm.SendPromptAsync(system + "\n" + user);
        }
        catch (Exception ex)
        {
            log("<color=#ff6666>[error]</color> interpret call failed: " + ex.Message);
            return null;
        }

        reply = ExtractJsonObject(reply);

        // Re-request if output uses sharps/flats or text instead of numeric intervals
        if (reply.IndexOf('#') >= 0 || reply.IndexOf('♯') >= 0 || reply.IndexOf('♭') >= 0 || reply.ToLowerInvariant().Contains(" b"))
        {
            log("<color=#ffaa00>[warn]</color> interpretation used musical symbols; requesting numeric intervals only…");
            string userRetry =
                $"Your previous output used symbols. Re-interpret scale_text = \"{scaleText}\" " +
                "and return JSON with numeric integer semitone offsets only. " +
                "Do NOT use #, ♯, b, ♭, or any strings in intervals_from_root, but still include the rationale string.";
            try
            {
                var raw2 = await llm.SendPromptAsync(system + "\n" + userRetry);
                reply = ExtractJsonObject(raw2);
            }
            catch (Exception ex2)
            {
                log("<color=#ff6666>[error]</color> interpret retry failed: " + ex2.Message);
                return null;
            }
        }

        try
        {
            var interp = JsonConvert.DeserializeObject<InterpScale>(reply);
            if (interp == null || interp.intervals_from_root == null || interp.intervals_from_root.Count == 0)
            {
                log("<color=#ff6666>[error]</color> interpret JSON missing fields.");
                return null;
            }

            // Ensure 0 is present in intervals
            if (!interp.intervals_from_root.Contains(0))
                interp.intervals_from_root.Insert(0, 0);

            // === NEW: Log rationale if the model provided one ===
            if (!string.IsNullOrEmpty(interp.rationale))
                log($"<color=#888>[llm]</color> {interp.rationale}");

            return interp;
        }
        catch (Exception ex)
        {
            log("<color=#ff6666>[error]</color> interpret parse error: " + ex.Message);
            return null;
        }
    }

    MusicData RealizeScale(string scaleText, InterpScale interp)
    {
        int rootMidi = 60 + interp.root_pc; // C4=60 baseline, so root is near ~C4..B4

        var md = new MusicData
        {
            meta = new MusicMeta
            {
                task = "generate_scale",
                scale_text = scaleText,
                scale_kind = interp.scale_kind ?? "",
                tempo_bpm = defaultTempoBpm,
                time_signature = defaultTimeSig,
                program = null
            },
            intervals_from_root = new List<int>(interp.intervals_from_root),
            notes = new List<MusicNote>()
        };

        for (int i = 0; i < interp.intervals_from_root.Count; i++)
        {
            int pitch = rootMidi + interp.intervals_from_root[i];
            md.notes.Add(new MusicNote { t_beats = i, dur_beats = 1f, pitch = pitch, velocity = defaultVelocity, channel = defaultChannel });
        }
        return md;
    }

    // --- Root parsing helper (robust) ---
    private static int? TryParseRootPcFromScaleText(string scaleText)
    {
        if (string.IsNullOrWhiteSpace(scaleText)) return null;

        // Normalize simple Unicode sharps/flats and trim quotes
        string s = scaleText.Trim().Trim('"', '“', '”', '\'')
                            .Replace('♯', '#')
                            .Replace('♭', 'b')
                            .Trim();

        // First token up to whitespace (handles "B Locrian", "F# lydian", etc.)
        int space = s.IndexOfAny(new[] { ' ', '\t' });
        string root = (space >= 0 ? s.Substring(0, space) : s).ToLowerInvariant();

        // Map of common spellings to pitch classes
        switch (root)
        {
            case "c": case "b#": return 0;
            case "c#": case "db": return 1;
            case "d": return 2;
            case "d#": case "eb": return 3;
            case "e": case "fb": return 4;
            case "f": case "e#": return 5;
            case "f#": case "gb": return 6;
            case "g": return 7;
            case "g#": case "ab": return 8;
            case "a": return 9;
            case "a#": case "bb": return 10;
            case "b": case "cb": return 11;
            default: return null; // unknown or rootless name like "Chromatic"
        }
    }

    async Task<List<int>> AuditScaleAsync(int rootMidi, List<int> intervals, MusicData realized, Action<string> log, CancellationToken ct)
    {
        // ask for pitches only: { "pitches": [int,...] }
        string system =
        "Output ONLY valid JSON with { \"pitches\": int[] }. No prose, no tags.\n" +
        "Given root_midi, intervals_from_root, and current pitches, return corrected pitches so diffs match intervals.";

        var curr = new List<int>(realized.notes.Count);
        foreach (var n in realized.notes) curr.Add(n.pitch);

        string user =
        $"root_midi: {rootMidi}\n" +
        $"intervals_from_root: [{string.Join(",", intervals)}]\n" +
        $"current_pitches: [{string.Join(",", curr)}]\n" +
        "Return: {\"pitches\": [...]} only.";

        string raw;
        try { raw = await llm.SendPromptAsync(system + "\n" + user); }
        catch (Exception ex) { log("<color=#ff6666>[error]</color> audit call failed: " + ex.Message); return null; }

        string reply = ExtractJsonObject(raw);
        try
        {
            var obj = JsonConvert.DeserializeObject<TempPitches>(reply);
            if (obj?.pitches == null || obj.pitches.Count != curr.Count) return null;
            return obj.pitches;
        }
        catch (Exception ex)
        {
            log($"<color=#ff6666>[error]</color> audit parse error: {ex.Message}");
            return null;
        }
    }

    class TempPitches { public List<int> pitches; }

    // ---------- Utility ----------

    public static string ExtractJsonObject(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("```json", "").Replace("```", "")
             .Replace("<json>", "").Replace("</json>", "")
             .Replace("<mono>", "").Replace("</mono>", "");
        int a = s.IndexOf('{'); int b = s.LastIndexOf('}');
        return (a >= 0 && b > a) ? s.Substring(a, b - a + 1) : s.Trim();
    }
}
