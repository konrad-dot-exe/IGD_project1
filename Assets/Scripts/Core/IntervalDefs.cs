using UnityEngine;
using System;
using System.Collections.Generic;

namespace EarFPS
{
    public enum IntervalQuality { Perfect, Major, Minor, Augmented, Diminished }

    [Serializable]
    public struct IntervalDef
    {
        public string shortName;   // e.g., "P5"
        public string displayName; // e.g., "Perfect Fifth"
        public IntervalQuality quality;
        public int number;         // 1..9 (use 9 for 9th)
        public int semitones;      // 0..14 for up to M9
    }

    public static class IntervalTable
    {
        // Ascending intervals up to M9; feel free to expand.
        public static readonly IntervalDef[] All = new IntervalDef[]
        {
            Def("P1","Perfect Unison", IntervalQuality.Perfect, 1, 0),
            Def("m2","Minor Second",   IntervalQuality.Minor,   2, 1),
            Def("M2","Major Second",   IntervalQuality.Major,   2, 2),
            Def("m3","Minor Third",    IntervalQuality.Minor,   3, 3),
            Def("M3","Major Third",    IntervalQuality.Major,   3, 4),
            Def("P4","Perfect Fourth", IntervalQuality.Perfect, 4, 5),
            Def("TT","Tritone",        IntervalQuality.Augmented,4,6), // treat as A4
            Def("P5","Perfect Fifth",  IntervalQuality.Perfect, 5, 7),
            Def("m6","Minor Sixth",    IntervalQuality.Minor,   6, 8),
            Def("M6","Major Sixth",    IntervalQuality.Major,   6, 9),
            Def("m7","Minor Seventh",  IntervalQuality.Minor,   7, 10),
            Def("M7","Major Seventh",  IntervalQuality.Major,   7, 11),
            Def("P8","Octave",         IntervalQuality.Perfect, 8, 12),
            Def("m9","Minor Ninth",    IntervalQuality.Minor,   9, 13),
            Def("M9","Major Ninth",    IntervalQuality.Major,   9, 14),
        };

        static IntervalDef Def(string s, string d, IntervalQuality q, int n, int st)
            => new IntervalDef { shortName = s, displayName = d, quality = q, number = n, semitones = st };

        public static IntervalDef ByIndex(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];
        public static int Count => All.Length;
    }
}
