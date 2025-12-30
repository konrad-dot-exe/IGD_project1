using System;

namespace Sonoria.MusicTheory.Timeline
{
    /// <summary>
    /// Timeline specification for musical sequences.
    /// Defines the timing resolution and optional tempo/time signature information.
    /// </summary>
    [Serializable]
    public class TimelineSpec
    {
        /// <summary>
        /// Number of ticks per quarter note (default: 4).
        /// This defines the resolution of the timeline.
        /// </summary>
        public int ticksPerQuarter = 4;

        /// <summary>
        /// Optional tempo in beats per minute.
        /// </summary>
        public float? tempoBpm;

        /// <summary>
        /// Optional time signature numerator (e.g., 4 for 4/4 time).
        /// </summary>
        public int? timeSigNumerator;

        /// <summary>
        /// Optional time signature denominator (e.g., 4 for 4/4 time).
        /// </summary>
        public int? timeSigDenominator;

        /// <summary>
        /// Creates a default timeline spec with 4 ticks per quarter note.
        /// </summary>
        public static TimelineSpec Default => new TimelineSpec
        {
            ticksPerQuarter = 4
        };

        /// <summary>
        /// Default constructor initializes ticksPerQuarter to 4.
        /// </summary>
        public TimelineSpec()
        {
            ticksPerQuarter = 4;
        }
    }
}

