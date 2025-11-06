using System;
using System.Collections.Generic;

[Serializable]
public class MusicMeta
{
    public string task;
    public string scale_text;
    public string key_signature;
    public int tempo_bpm;            // default 120
    public string time_signature;    // "4/4"
    public int? program;             // unused now
    public string scale_kind;  // e.g., "whole-tone", "dorian", "octatonic (H-W)"
}

[Serializable]
public class MusicNote
{
    public float t_beats;   // start time in beats
    public float dur_beats; // duration in beats
    public int pitch;       // 0..127
    public int velocity;    // 1..127 (we’ll map to 0..1 for FMOD)
    public int channel;     // 1
}

[Serializable]
public class MusicData
{
    public MusicMeta meta;
    public List<MusicNote> notes = new();
    public List<int> intervals_from_root = new(); // e.g., [0,2,4,6,8,10,12]
}


