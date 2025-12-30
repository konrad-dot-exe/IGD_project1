using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Result of tension detection for a single chord.
    /// Contains the detected tensions and metadata about the detection.
    /// </summary>
    public struct DetectedTensions
    {
        public List<ChordTension> Tensions;
        public int[] AnalyzedMidi; // The exact MIDI voicing analyzed [B,T,A,S]
        public int[] AnalyzedPcs;  // Pitch classes of analyzed voicing
        
        public DetectedTensions(List<ChordTension> tensions, int[] analyzedMidi, int[] analyzedPcs)
        {
            Tensions = tensions ?? new List<ChordTension>();
            AnalyzedMidi = analyzedMidi ?? new int[0];
            AnalyzedPcs = analyzedPcs ?? new int[0];
        }
        
        public bool HasTensions => Tensions != null && Tensions.Count > 0;
    }
}

