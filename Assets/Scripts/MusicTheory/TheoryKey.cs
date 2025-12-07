using System;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Represents a musical scale mode.
    /// Phase 1: Only includes the 7 diatonic modes.
    /// </summary>
    public enum ScaleMode
    {
        Ionian,
        Dorian,
        Phrygian,
        Lydian,
        Mixolydian,
        Aeolian,
        Locrian
    }

    /// <summary>
    /// Represents a musical key (tonic + mode).
    /// Supports arbitrary tonic pitch classes (0-11) for any scale mode.
    /// </summary>
    public readonly struct TheoryKey
    {
        private readonly int _tonicPc;

        /// <summary>
        /// The scale mode of this key.
        /// </summary>
        public ScaleMode Mode { get; }

        /// <summary>
        /// The tonic pitch class (0-11). 0 = C, 1 = C#/Db, ..., 11 = B.
        /// </summary>
        public int TonicPitchClass => _tonicPc;

        /// <summary>
        /// Creates a TheoryKey with the specified tonic pitch class and mode.
        /// </summary>
        /// <param name="tonicPc">Tonic pitch class (0-11). Values outside this range are normalized modulo 12.</param>
        /// <param name="mode">The scale mode</param>
        public TheoryKey(int tonicPc, ScaleMode mode)
        {
            // Normalize tonicPc to 0-11 range (mod 12)
            _tonicPc = ((tonicPc % 12) + 12) % 12;
            Mode = mode;
        }

        /// <summary>
        /// Creates a TheoryKey with the specified mode, defaulting to C (pitch class 0).
        /// This constructor maintains backwards compatibility with existing code.
        /// </summary>
        /// <param name="mode">The scale mode</param>
        public TheoryKey(ScaleMode mode) : this(0, mode)
        {
        }

        /// <summary>
        /// Creates a TheoryKey from a tonic pitch class and mode.
        /// </summary>
        /// <param name="tonicPc">Tonic pitch class (0-11)</param>
        /// <param name="mode">The scale mode</param>
        /// <returns>A new TheoryKey instance</returns>
        public static TheoryKey FromPitchClass(int tonicPc, ScaleMode mode) => new TheoryKey(tonicPc, mode);

        /// <summary>
        /// Creates a TheoryKey with C as the tonic (pitch class 0) and the specified mode.
        /// </summary>
        /// <param name="mode">The scale mode</param>
        /// <returns>A new TheoryKey instance with tonic C</returns>
        public static TheoryKey C(ScaleMode mode) => new TheoryKey(0, mode);

        /// <summary>
        /// Returns a string representation of the key (e.g., "C Ionian", "D Dorian", "F# Lydian").
        /// </summary>
        public override string ToString()
        {
            // Use TheoryPitch to get note name from pitch class
            // Pass pitch class as MIDI note (GetPitchNameFromMidi extracts pitch class anyway)
            string tonicName = TheoryPitch.GetPitchNameFromMidi(_tonicPc, preferFlats: true);
            return $"{tonicName} {Mode}";
        }
    }
}

