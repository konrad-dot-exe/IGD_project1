namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// Severity level for diagnostic events.
    /// </summary>
    public enum DiagSeverity
    {
        /// <summary>
        /// Informational event (e.g., voicing started/completed).
        /// </summary>
        Info,

        /// <summary>
        /// Warning event (e.g., register clamped, spacing adjusted).
        /// </summary>
        Warning,

        /// <summary>
        /// Forced event (e.g., forced 7th resolution, coverage fix applied).
        /// </summary>
        Forced
    }
}

