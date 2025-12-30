using System.Collections.Generic;

namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// Collection of diagnostic events for a single chord region.
    /// </summary>
    public class RegionDiagnostics
    {
        /// <summary>
        /// Zero-based index of the region.
        /// </summary>
        public int regionIndex;

        /// <summary>
        /// List of diagnostic events for this region.
        /// </summary>
        public List<RegionDiagEvent> events = new List<RegionDiagEvent>();

        public RegionDiagnostics(int regionIndex)
        {
            this.regionIndex = regionIndex;
        }

        /// <summary>
        /// Adds a diagnostic event to this region.
        /// </summary>
        public void Add(RegionDiagEvent evt)
        {
            events.Add(evt);
        }

        /// <summary>
        /// Adds a diagnostic event with convenience parameters.
        /// </summary>
        public void Add(DiagSeverity severity, string code, string message, int voiceIndex = -1, int beforeMidi = -1, int afterMidi = -1)
        {
            events.Add(new RegionDiagEvent(regionIndex, severity, code, message, voiceIndex, beforeMidi, afterMidi));
        }
    }
}

