using System.Collections.Generic;
using UnityEngine;

public static class NoteNamer
{
    // Pitch-class name tables
    static readonly string[] NAMES_SHARP = { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
    static readonly string[] NAMES_FLAT  = { "C","Db","D","Eb","E","F","Gb","G","Ab","A","Bb","B" };

    public enum Mode { Ionian=0, Dorian=1, Phrygian=2, Lydian=3, Mixolydian=4, Aeolian=5, Locrian=6 }

    // Semitone offset from a *modal tonic* to its *parent MAJOR* tonic.
    // Examples (tonicPc = C = 0):
    // - C Dorian      -> Bb major  (0 + -2  = 10)  ✓
    // - C Phrygian    -> Ab major  (0 + -4  = 8)   ✓
    // - C Lydian      -> G  major  (0 + +7  = 7)   ✓
    // - C Mixolydian  -> F  major  (0 + +5  = 5)   ✓  *** FIXED (was -5) ***
    // - C Aeolian     -> Eb major  (0 + +3  = 3)   ✓
    // - C Locrian     -> Db major  (0 + +1  = 1)   ✓
    static readonly int[] PARENT_MAJOR_OFF = { 0, -2, -4, +7, +5, +3, +1 };

    // Parent major keys that conventionally prefer sharps.
    // (Flats are the complement: F(5), Bb(10), Eb(3), Ab(8), Db(1), Gb(6), Cb(11).)
    static readonly HashSet<int> SHARP_KEYS = new HashSet<int>
    {
        7,  // G
        2,  // D
        9,  // A
        4,  // E
        11, // B
        6,  // F#
        1   // C#
    };

    public struct KeyContext
    {
        public int tonicPc;   // 0..11 (C=0)
        public Mode mode;
        public KeyContext(int tonicPc, Mode mode)
        {
            this.tonicPc = Mod12(tonicPc);
            this.mode = mode;
        }
    }

    static int Mod12(int v) => (v % 12 + 12) % 12;

    // Decide which accidental set to prefer for naming, based on the parent major.
    static bool PreferSharps(KeyContext ctx)
    {
        int parentPc = Mod12(ctx.tonicPc + PARENT_MAJOR_OFF[(int)ctx.mode]);
        return SHARP_KEYS.Contains(parentPc);
    }

    public static int OctaveOfMidi(int midi) => (midi / 12) - 1;

    public static string NameForMidi(int midi, KeyContext ctx)
    {
        int pc = Mod12(midi);
        bool sharp = PreferSharps(ctx);
        return (sharp ? NAMES_SHARP : NAMES_FLAT)[pc];
    }

    // Convenience overload if you only have a pitch-class and an explicit preference
    public static string NameForPc(int pc, bool preferSharps) =>
        (preferSharps ? NAMES_SHARP : NAMES_FLAT)[Mod12(pc)];
}
