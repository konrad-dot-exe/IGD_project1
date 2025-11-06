// TheoryCritic.cs
// Drop-in: replace your current file with this version.
// Adds rationale/explanation fields while keeping backward compatibility.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Interface for theory-level LLM critique. Kept tiny on purpose.
/// </summary>
public interface ITheoryCritic
{
    /// <summary>
    /// Ask the LLM for a concise, bounded-JSON critique (no edits applied).
    /// Use for teacher-style feedback in the UI.
    /// </summary>
    Task<CritiqueResult> CritiqueAsync(MusicData md, string profile, CancellationToken ct = default);
}

/// <summary>
/// Result container for critique (messages + optional suggested edits).
/// </summary>
public class CritiqueResult
{
    public bool ok;
    public string summary;

    // NEW: brief answer-level rationale (≤120 chars). No chain-of-thought.
    public string rationale;

    public CritItem[] items;   // code/message/severity
    public Edit[] edits;       // NOT auto-applied; only suggestions
}

/// <summary>One critique item.</summary>
public class CritItem { public string code; public string message; public string severity; }

/// <summary>
/// Proposed edit (for future use). For now we do not auto-apply any edits
/// and prefer replacing the whole interval pattern via StrictReinterpretIntervalsAsync.
/// </summary>
public class Edit { public int noteIndex; public string field; public string action; public string value; }

/// <summary>
/// Return type for strict reinterpretation containing the canonical interval pattern
/// and a short explanation suitable for logging.
/// </summary>
public class ReinterpretResult
{
    public int[] intervals_from_root;
    public string explanation;   // brief one-liner; safe to log
}

/// <summary>
/// LLM-backed "theory critic". Provides two utilities:
/// 1) CritiqueAsync: concise JSON critique for UI (+ rationale),
/// 2) StrictReinterpretIntervalsAsync: canonical intervals_from_root (with/without explanation).
/// </summary>
public sealed class TheoryCritic : ITheoryCritic
{
    private readonly LLMManager _llm;

    public TheoryCritic(LLMManager llm) { _llm = llm; }

    // -------------------------------
    // 1) Concise critique (messages)
    // -------------------------------
    public async Task<CritiqueResult> CritiqueAsync(MusicData md, string profile, CancellationToken ct = default)
    {
        // Defensive defaults
        var fallback = new CritiqueResult
        {
            ok = true,
            summary = "No critique (fallback).",
            rationale = "",
            items = new CritItem[0],
            edits = new Edit[0]
        };

        if (_llm == null || md == null) return fallback;

        string payload = JsonConvert.SerializeObject(md);

        // STRICT: JSON only; small schema; no prose outside JSON.
        string prompt =
@"System: You are a music theory reviewer for a Unity education app.
Return ONLY valid JSON with EXACT schema:
{ ""ok"":bool,
  ""summary"":string,
  ""rationale"":string,
  ""items"":[{""code"":string,""message"":string,""severity"":""info""|""warn""|""error""}],
  ""edits"":[{""noteIndex"":int,""field"":""pitch""|""dur_beats""|""t_beats"",""action"":""set"",""value"":string}]
}
Guidelines:
- Profile: " + profile + @"
- Keep rationale a brief (<=120 chars) answer-level justification; no step-by-step chain-of-thought.
- Be concise (<= 6 items). Prefer theory-level observations (mode formula, characteristic tones, root alignment).
- If data appears to be the wrong scale/mode, set ok=false and state the expected correction in summary.
- No prose outside JSON.

User:
Review the following MusicData:
" + payload;

        try
        {
            string raw = await _llm.SendPromptAsync(prompt);
            string clean = MusicOrchestrator.ExtractJsonObject(raw); // your existing helper
            var result = JsonConvert.DeserializeObject<CritiqueResult>(clean);
            return result ?? fallback;
        }
        catch (Exception ex)
        {
            return new CritiqueResult
            {
                ok = true,
                summary = "Critique unavailable: " + ex.Message,
                rationale = "",
                items = new CritItem[0],
                edits = new Edit[0]
            };
        }
    }

    // -----------------------------------------------------------------
    // 2) Strict canonical re-interpretation (robust fixer for scales)
    // -----------------------------------------------------------------

    /// <summary>
    /// Back-compatible version: returns only the canonical intervals array (old signature).
    /// </summary>
    public async Task<int[]> StrictReinterpretIntervalsAsync(string scaleText, CancellationToken ct = default)
    {
        var res = await StrictReinterpretIntervalsWithInfoAsync(scaleText, ct);
        return res?.intervals_from_root;
    }

    /// <summary>
    /// Preferred version: returns intervals + short explanation for logging.
    /// </summary>
    public async Task<ReinterpretResult> StrictReinterpretIntervalsWithInfoAsync(string scaleText, CancellationToken ct = default)
    {
        if (_llm == null || string.IsNullOrWhiteSpace(scaleText)) return null;

        string prompt =
@"System: Return ONLY valid JSON with EXACT schema:
{ ""intervals_from_root"": [int, ...], ""explanation"": string }
Rules:
- If the user gives a named scale (e.g., ""D Altered"", ""E Hungarian Minor""), output the canonical semitone offsets from the ROOT, including the octave step.
- Keep 'explanation' brief (<=120 chars). No chain-of-thought.
- Examples (do not echo):
  Major (Ionian): [0,2,4,5,7,9,11,12]
  Dorian: [0,2,3,5,7,9,10,12]
  Lydian: [0,2,4,6,7,9,11,12]
  Harmonic Minor: [0,2,3,5,7,8,11,12]
  Super Locrian (Altered): [0,1,3,4,6,8,10,12]
  Hungarian Minor (Gypsy Minor): [0,2,3,6,7,8,11,12]
  Augmented (hexatonic): [0,3,4,7,8,11,12]
- No prose, no trailing text, no code fences.

User:
Give canonical intervals_from_root (include octave) for: " + scaleText;

        try
        {
            string raw = await _llm.SendPromptAsync(prompt);
            string clean = MusicOrchestrator.ExtractJsonObject(raw);
            var obj = JObject.Parse(clean);

            var arr = obj["intervals_from_root"] as JArray;
            if (arr == null) return null;

            var list = new System.Collections.Generic.List<int>(arr.Count);
            foreach (var t in arr) list.Add((int)t);

            string expl = (string)(obj["explanation"] ?? "");

            return new ReinterpretResult
            {
                intervals_from_root = list.ToArray(),
                explanation = expl ?? ""
            };
        }
        catch
        {
            return null; // Caller decides how to proceed.
        }
    }
}
