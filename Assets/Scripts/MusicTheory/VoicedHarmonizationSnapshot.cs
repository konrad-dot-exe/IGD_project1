using System;
using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Serializable snapshot of a complete voiced harmonization for export/analysis.
    /// Includes SATB voicing information, melody, and chord progression data.
    /// </summary>
    [Serializable]
    public class VoicedHarmonizationSnapshot
    {
        public string Key;          // e.g. "C"
        public string Mode;         // e.g. "Ionian"
        public string Description;  // Free-form context note
        public List<VoicedStepSnapshot> Steps;
    }

    /// <summary>
    /// Serializable snapshot of a single step in a voiced harmonization.
    /// </summary>
    [Serializable]
    public class VoicedStepSnapshot
    {
        public float TimeBeats;
        public MelodyNoteSnapshot Melody;
        public ChordSnapshot Chord;
        public VoicingSnapshot Voicing;
    }

    /// <summary>
    /// Melody note information for a step.
    /// </summary>
    [Serializable]
    public class MelodyNoteSnapshot
    {
        public int Midi;
        public string NoteName;     // e.g. "G5"
        
        // Theory annotations
        public int ScaleDegree;         // 1-7, relative to current key/mode
        public int ChromaticOffset;     // -2..+2, -1 = flat, +1 = sharp
        public string DegreeLabel;      // e.g. "5", "b6", "#4", "bb7"
        public bool IsChordTone;        // true if melody pitch class is in the chord
    }

    /// <summary>
    /// Chord information for a step.
    /// </summary>
    [Serializable]
    public class ChordSnapshot
    {
        public string Roman;        // e.g. "bVI", "iiø7"
        public string ChordSymbol;  // e.g. "Abmaj7", "Dm7b5"
        
        // Quality refers to triad quality (Major, Minor, Diminished, Augmented)
        // This field is kept for backward compatibility and is mirrored by TriadQuality
        public string Quality;      // e.g. "Major", "Minor", "Diminished", "Augmented"
        
        // Root degree information
        public int RootDegree;           // 1-7, relative to current key/mode
        public int RootChromaticOffset;  // -2..+2, -1 = flat, +1 = sharp
        public string RootDegreeLabel;   // e.g. "V", "bVI", "#iv"
        
        // Diatonic vs chromatic
        public bool IsDiatonic;          // true for diatonic chords in the mode
        public string ChromaticFunction; // e.g. "bVI (borrowed from minor)",
                                         // "bII (Neapolitan)", "V/V", or "" for diatonic
        
        // Triad / base quality
        public string TriadQuality;     // "Major", "Minor", "Diminished", "Augmented", or ""
        
        // Power chord flag (root + fifth only, no 3rd)
        public bool IsPowerChord;
        
        // Suspensions / omissions
        public string Suspension;       // "", "sus2", "sus4", possibly other values later
        public bool OmitsThird;         // true if the 3rd is intentionally absent (sus or power chord)
        
        // Seventh / chord type
        public bool HasSeventh;
        public string SeventhType;      // e.g. "Dominant7", "Major7", "Minor7", "HalfDiminished7", "Diminished7", or ""
    }

    /// <summary>
    /// SATB voicing information for a step.
    /// </summary>
    [Serializable]
    public class VoicingSnapshot
    {
        public int BassMidi;
        public int TenorMidi;
        public int AltoMidi;
        public int SopranoMidi;
        public string BassNote;     // e.g. "C3"
        public string TenorNote;    // e.g. "E4"
        public string AltoNote;     // e.g. "G4"
        public string SopranoNote;  // e.g. "C5"
    }
}

