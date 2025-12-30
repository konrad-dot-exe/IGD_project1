namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// A single diagnostic event for a chord region.
    /// </summary>
    public struct RegionDiagEvent
    {
        /// <summary>
        /// Zero-based index of the region this event applies to.
        /// </summary>
        public int regionIndex;

        /// <summary>
        /// Severity level of the event.
        /// </summary>
        public DiagSeverity severity;

        /// <summary>
        /// Diagnostic code (e.g., "FORCED_7TH_RESOLUTION").
        /// </summary>
        public string code;

        /// <summary>
        /// Human-readable message describing the event.
        /// </summary>
        public string message;

        /// <summary>
        /// Voice index (0=Bass, 1=Tenor, 2=Alto, 3=Soprano) if applicable, else -1.
        /// </summary>
        public int voiceIndex;

        /// <summary>
        /// MIDI note before the event (if applicable), else -1.
        /// </summary>
        public int beforeMidi;

        /// <summary>
        /// MIDI note after the event (if applicable), else -1.
        /// </summary>
        public int afterMidi;

        public RegionDiagEvent(int regionIndex, DiagSeverity severity, string code, string message, int voiceIndex = -1, int beforeMidi = -1, int afterMidi = -1)
        {
            this.regionIndex = regionIndex;
            this.severity = severity;
            this.code = code;
            this.message = message;
            this.voiceIndex = voiceIndex;
            this.beforeMidi = beforeMidi;
            this.afterMidi = afterMidi;
        }
    }
}

