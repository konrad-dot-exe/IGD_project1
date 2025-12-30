using System.Collections.Generic;
using System.Linq;

namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// Collects diagnostic events grouped by region index.
    /// </summary>
    public class DiagnosticsCollector
    {
        private Dictionary<int, RegionDiagnostics> _regions = new Dictionary<int, RegionDiagnostics>();
        private const int MaxEventsPerRegion = 10;
        
        /// <summary>
        /// Controls whether TRACE logging is enabled. Set by UI controller.
        /// </summary>
        public bool EnableTrace { get; set; } = false;

        /// <summary>
        /// Adds a diagnostic event for the specified region.
        /// </summary>
        public void Add(int regionIndex, DiagSeverity severity, string code, string message, int voiceIndex = -1, int beforeMidi = -1, int afterMidi = -1)
        {
            if (!_regions.TryGetValue(regionIndex, out var regionDiags))
            {
                regionDiags = new RegionDiagnostics(regionIndex);
                _regions[regionIndex] = regionDiags;
            }

            // Check if we've hit the cap
            if (regionDiags.events.Count >= MaxEventsPerRegion)
            {
                // Check if we already added the "omitted" message
                bool hasOmitted = regionDiags.events.Any(e => e.code == "(...more omitted)");
                if (!hasOmitted)
                {
                    regionDiags.Add(DiagSeverity.Info, "(...more omitted)", $"(Max {MaxEventsPerRegion} events per region)");
                }
                return; // Drop the event
            }

            // Dedupe: check if we already have an identical event
            bool isDuplicate = regionDiags.events.Any(e =>
                e.code == code &&
                e.message == message &&
                e.voiceIndex == voiceIndex &&
                e.beforeMidi == beforeMidi &&
                e.afterMidi == afterMidi);

            if (!isDuplicate)
            {
                regionDiags.Add(severity, code, message, voiceIndex, beforeMidi, afterMidi);
            }
        }

        /// <summary>
        /// Gets all region diagnostics, ordered by region index.
        /// </summary>
        public List<RegionDiagnostics> GetAll()
        {
            return _regions.Values.OrderBy(r => r.regionIndex).ToList();
        }

        /// <summary>
        /// Clears all collected diagnostics.
        /// </summary>
        public void Clear()
        {
            _regions.Clear();
        }

        /// <summary>
        /// Gets the number of regions with diagnostics.
        /// </summary>
        public int RegionCount => _regions.Count;
    }
}

