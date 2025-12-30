namespace Sonoria.MusicTheory.Diagnostics
{
    /// <summary>
    /// Diagnostic event codes (string constants for flexibility).
    /// </summary>
    public static class DiagCode
    {
        // Voicing lifecycle
        public const string VOICING_START = "VOICING_START";
        public const string VOICING_DONE = "VOICING_DONE";
        public const string VOICED_REGION = "VOICED_REGION"; // Tripwire for regionIndex verification

        // Forced events
        public const string FORCED_7TH_RESOLUTION = "FORCED_7TH_RESOLUTION";
        public const string COVERAGE_FIX_APPLIED = "COVERAGE_FIX_APPLIED";
        public const string BLOCKED_ILLEGAL_RESOLUTION = "BLOCKED_ILLEGAL_RESOLUTION";
        public const string REDIRECTED_ILLEGAL_RESOLUTION = "REDIRECTED_ILLEGAL_RESOLUTION";
        public const string APPLIED_LEGAL_RESOLUTION = "APPLIED_LEGAL_RESOLUTION";
        public const string RESOLUTION_CHECK = "RESOLUTION_CHECK";
        public const string REJECTED_ILLEGAL_TENDENCY_CANDIDATE = "REJECTED_ILLEGAL_TENDENCY_CANDIDATE";

        // Warnings
        public const string REGISTER_CLAMPED = "REGISTER_CLAMPED";
        public const string SPACING_CLAMPED = "SPACING_CLAMPED";
        public const string MELODY_CONSTRAINT_BLOCKED = "MELODY_CONSTRAINT_BLOCKED";
        
        // Coverage audit warnings
        public const string MISSING_REQUIRED_TONE = "MISSING_REQUIRED_TONE";
        public const string MISSING_MULTIPLE_REQUIRED_TONES = "MISSING_MULTIPLE_REQUIRED_TONES";
        public const string MISSING_7TH_IN_7TH_CHORD = "MISSING_7TH_IN_7TH_CHORD";
        public const string NON_CHORD_TONE_PRESENT = "NON_CHORD_TONE_PRESENT";
        public const string UNUSUAL_DOUBLING = "UNUSUAL_DOUBLING";
        public const string UNISON_STACK = "UNISON_STACK";
        
        // Tension warnings
        public const string SUS4_CLASH_WITH_THIRD = "SUS4_CLASH_WITH_THIRD";
        public const string AVOID_TONE_11_OVER_DOM_WITH_3RD = "AVOID_TONE_11_OVER_DOM_WITH_3RD";
        public const string NON_CHORD_TONE_SHARP11 = "NON_CHORD_TONE_SHARP11";
    }
}

