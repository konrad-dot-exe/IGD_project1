namespace Sonoria.MusicTheory.Timeline
{
    /// <summary>
    /// Placeholder for melody events on a timeline.
    /// Not wired up yet; reserved for future implementation.
    /// </summary>
    public struct MelodyEvent
    {
        /// <summary>
        /// Start tick position on the timeline (0-based).
        /// </summary>
        public int startTick;

        /// <summary>
        /// Duration of the melody event in ticks.
        /// </summary>
        public int durationTicks;

        /// <summary>
        /// MIDI note number (0-127).
        /// </summary>
        public int midi;
    }
}

