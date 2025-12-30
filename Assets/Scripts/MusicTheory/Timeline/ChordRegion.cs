using Sonoria.MusicTheory;

namespace Sonoria.MusicTheory.Timeline
{
    /// <summary>
    /// Represents a chord region on a timeline.
    /// Each region has a start tick, duration, and the chord event data.
    /// </summary>
    public struct ChordRegion
    {
        /// <summary>
        /// Start tick position on the timeline (0-based).
        /// </summary>
        public int startTick;

        /// <summary>
        /// Duration of the chord region in ticks.
        /// </summary>
        public int durationTicks;

        /// <summary>
        /// The chord event (recipe, key, melody, etc.).
        /// Reuses existing ChordEvent representation.
        /// </summary>
        public ChordEvent chordEvent;

        /// <summary>
        /// Optional debug label (e.g., Roman numeral string) for easier debugging.
        /// </summary>
        public string debugLabel;
    }
}

