using System;
using System.Collections.Generic;
using System.Linq;


public static class MusicValidator
{
    public enum Severity { Info, Warning, Error }

    public class Issue
    {
        public Severity Level;
        public string Code;
        public string Message;
        public int? NoteIndex;

        public Issue(Severity level, string code, string message, int? noteIndex = null)
        {
            Level = level;
            Code = code;
            Message = message;
            NoteIndex = noteIndex;
        }

        public override string ToString() =>
            $"{Level} [{Code}]{(NoteIndex is int i ? $"(i={i})" : "")}: {Message}";
    }

    public sealed class Report
    {
        public List<Issue> Issues { get; } = new();
        public bool Ok => Issues.All(x => x.Level != Severity.Error);

        public void Add(Severity s, string code, string msg, int? idx = null)
            => Issues.Add(new Issue(s, code, msg, idx));

        public override string ToString()
            => string.Join("\n", Issues.Select(i =>
                 $"{i.Level} [{i.Code}]{(i.NoteIndex is int k ? $"(i={k})" : "")}: {i.Message}"));
    }

    // Entry point used by orchestrator and tests
    public static Report ValidateScale(MusicData md)
    {
        var r = new Report();

        // ---- Structure & meta
        if (md == null) { r.Add(Severity.Error, "NULL_DATA", "MusicData is null"); return r; }
        if (md.notes == null || md.notes.Count == 0)
            r.Add(Severity.Error, "NO_NOTES", "No notes present");
        if (md.meta == null)
            r.Add(Severity.Warning, "NO_META", "Meta missing (tempo/timeSig may default upstream)");

        if (md.intervals_from_root == null)
            r.Add(Severity.Error, "NO_INTERVALS", "intervals_from_root missing");
        else if (md.notes != null && md.intervals_from_root.Count != md.notes.Count)
            r.Add(Severity.Error, "COUNT_MISMATCH", "interval count != note count");

        if (!r.Ok) return r; // stop early if fundamental structure is broken

        // ---- Per-note checks
        for (int i = 0; i < md.notes.Count; i++)
        {
            var n = md.notes[i];
            if (n.pitch < 0 || n.pitch > 127)
                r.Add(Severity.Error, "PITCH_RANGE", $"pitch {n.pitch} out of MIDI range 0..127", i);

            if (n.dur_beats <= 0f)
                r.Add(Severity.Error, "NONPOS_DUR", $"duration must be > 0 (got {n.dur_beats})", i);

            if (n.t_beats < 0f)
                r.Add(Severity.Warning, "NEG_START", $"t_beats < 0 (got {n.t_beats})", i);

            if (n.velocity < 1 || n.velocity > 127)
                r.Add(Severity.Warning, "VEL_RANGE", $"velocity {n.velocity} will be clamped upstream", i);

            if (n.channel < 1)
                r.Add(Severity.Warning, "BAD_CHAN", $"channel {n.channel} will be normalized upstream", i);
        }

        // ---- Monotonic ascending check (scales)
        for (int i = 1; i < md.notes.Count; i++)
            if (md.notes[i].pitch < md.notes[i - 1].pitch)
                r.Add(Severity.Error, "NOT_ASCENDING", "scale is not ascending by pitch", i);

        // ---- Interval deltas check (root-relative)
        int root = md.notes[0].pitch;
        for (int i = 0; i < md.notes.Count; i++)
        {
            int got = md.notes[i].pitch - root;
            int want = md.intervals_from_root[i];

            bool last = i == md.notes.Count - 1;
            bool ok = last ? (got == want || (got % 12) == (want % 12)) : (got == want);

            if (!ok)
                r.Add(Severity.Error, "DIFF_MISMATCH",
                      $"interval mismatch: got {got}, want {want}", i);
        }

        // ---- Duplicate pitches (non-root interior duplicates usually smell wrong)
        // (allow same pitch only if consecutive equal intervals—rare in scales)
        for (int i = 1; i < md.notes.Count - 1; i++)
            if (md.notes[i].pitch == md.notes[i - 1].pitch)
                r.Add(Severity.Warning, "DUP_PITCH", "duplicate consecutive pitch", i);

        // ---- Rhythmic sanity: strictly increasing start times for 1-note-per-step
        float prevStart = md.notes[0].t_beats;
        for (int i = 1; i < md.notes.Count; i++)
        {
            if (md.notes[i].t_beats <= prevStart)
                r.Add(Severity.Warning, "NON_MONO_TIMES", "non-increasing t_beats for scale steps", i);
            prevStart = md.notes[i].t_beats;
        }

        // ---- Meta hints (informational)
        if (md.meta != null)
        {
            if (md.meta.tempo_bpm <= 0) r.Add(Severity.Info, "DEFAULT_TEMPO", "tempo will default upstream");
            if (string.IsNullOrEmpty(md.meta.time_signature))
                r.Add(Severity.Info, "DEFAULT_TIMESIG", "time_signature will default upstream");
        }

        return r;
    }
}

