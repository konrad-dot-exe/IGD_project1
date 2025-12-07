using System;
using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Serializable snapshot of a complete harmonization for export/analysis.
    /// </summary>
    [Serializable]
    public class HarmonizationSnapshot
    {
        public string KeyName;        // e.g. "C Ionian"
        public string Description;    // free-form note about context (e.g. "Test melody from Chord Lab")
        public List<HarmonizationStepSnapshot> Steps;
    }

    /// <summary>
    /// Serializable snapshot of a single step in a harmonization.
    /// </summary>
    [Serializable]
    public class HarmonizationStepSnapshot
    {
        public int MelodyMidi;
        public int MelodyDegree;
        public bool MelodyIsDiatonic;

        public List<CandidateChordSnapshot> Candidates;
        public ChosenChordSnapshot Chosen;
    }

    /// <summary>
    /// Serializable snapshot of a candidate chord.
    /// </summary>
    [Serializable]
    public class CandidateChordSnapshot
    {
        public string Roman;          // e.g. "ii6"
        public string ChordSymbol;    // e.g. "Dm/F"
        public string Reason;         // reason string from ChordCandidate
    }

    /// <summary>
    /// Serializable snapshot of the chosen chord for a step.
    /// </summary>
    [Serializable]
    public class ChosenChordSnapshot
    {
        public string Roman;
        public string ChordSymbol;
        public string Reason;         // step-level choice reason
    }
}

