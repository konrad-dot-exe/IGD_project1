using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class MusicDataController : MonoBehaviour
{
    public MusicData Current { get; private set; }

    public void Clear() => Current = null;

    /// <summary>
    /// Parse and load MusicData from JSON. Performs light validation and clamping only.
    /// No theory/interval logic is applied here (that now lives in the LLM + validator flow).
    /// </summary>
    public bool LoadFromJson(string json, out string error)
    {
        error = null;
        try
        {
            var data = JsonConvert.DeserializeObject<MusicData>(json);
            if (data == null)
            {
                error = "Empty JSON.";
                return false;
            }

            if (data.notes == null || data.notes.Count == 0)
            {
                error = "No notes.";
                return false;
            }

            // Ensure meta exists + sensible defaults
            data.meta ??= new MusicMeta();
            if (data.meta.tempo_bpm <= 0) data.meta.tempo_bpm = 120;
            if (string.IsNullOrEmpty(data.meta.time_signature)) data.meta.time_signature = "4/4";

            // Ensure intervals list exists so validators can reason about it (may be empty)
            data.intervals_from_root ??= new List<int>();

            // Clamp/normalize notes
            foreach (var n in data.notes)
            {
                // MIDI bounds
                n.pitch = Mathf.Clamp(n.pitch, 0, 127);

                // Default velocity/channel if missing or invalid
                if (n.velocity <= 0) n.velocity = 90;
                n.velocity = Mathf.Clamp(n.velocity, 1, 127);
                if (n.channel <= 0) n.channel = 1;

                // Durations/times cannot be negative
                if (n.dur_beats <= 0f) n.dur_beats = 1f;
                if (n.t_beats < 0f) n.t_beats = 0f;
            }

            Current = data;
            return true;
        }
        catch (Exception ex)
        {
            error = "Parse error: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Convenience for scheduling playback/bounds.
    /// </summary>
    public float GetTotalBeats()
    {
        if (Current == null || Current.notes == null) return 0f;
        float max = 0f;
        foreach (var n in Current.notes)
            max = Mathf.Max(max, n.t_beats + n.dur_beats);
        return max;
    }

    /// <summary>
    /// Pretty JSON for terminal logging / debugging.
    /// </summary>
    public string ToPrettyJson()
    {
        if (Current == null) return "{}";
        return JsonConvert.SerializeObject(Current, Formatting.Indented);
    }
}
