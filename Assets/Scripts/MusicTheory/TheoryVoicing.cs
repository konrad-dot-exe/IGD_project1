using System;
using System.Collections.Generic;
using System.Linq;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Represents a chord event in a progression context.
    /// Used as input for voicing functions.
    /// </summary>
    public struct ChordEvent
    {
        /// <summary>
        /// The key/mode context for this chord.
        /// </summary>
        public TheoryKey Key;

        /// <summary>
        /// The chord recipe (degree, quality, extension, etc.).
        /// </summary>
        public ChordRecipe Recipe;

        /// <summary>
        /// Position in beats (for future use in progression voice-leading).
        /// </summary>
        public float TimeBeats;

        /// <summary>
        /// Optional melody note to lock to top voice (unused in Phase 1).
        /// Will be used later for melody-harmonization.
        /// </summary>
        public int? MelodyMidi;
    }

    /// <summary>
    /// Represents a chord voiced into multiple parts (SATB-style).
    /// </summary>
    public struct VoicedChord
    {
        /// <summary>
        /// Position in beats (copied from ChordEvent).
        /// </summary>
        public float TimeBeats;

        /// <summary>
        /// MIDI notes for each voice, low-to-high: [bass, tenor, alto, soprano] for 4 voices.
        /// Length is 3 or 4 depending on numVoices parameter.
        /// </summary>
        public int[] VoicesMidi;
    }

    /// <summary>
    /// Static class providing voicing functions for chords.
    /// Phase 1: Simple block voicing for individual chords.
    /// Phase 2: Progression voice-leading with soft tonal tendency rules.
    /// 
    /// Voice-leading includes soft preferences for classical patterns:
    /// - Chord 7ths prefer to resolve down by step
    /// - Global leading tone (degree 7) prefers to resolve up to tonic (when appropriate)
    /// - Local leading tone (3rd of secondary dominants) prefers to resolve up to target root
    /// These are SOFT biases that enhance but do not override basic distance-based voice-leading.
    /// </summary>
    public static class TheoryVoicing
    {
        /// <summary>
        /// Debug flag to enable logging of tendency rule decisions.
        /// When true, logs cases where tendencies exist but textbook resolution wasn't chosen.
        /// </summary>
        private static bool enableTendencyDebug = false;
        
        // ========================================================================
        // TONAL TENDENCY RULE CONSTANTS
        // ========================================================================
        
        // Rule A: Chord 7th resolution
        private const float SeventhResolutionDownStepBonus = -3.5f;
        private const float SeventhResolutionHoldPenalty = +2.0f;
        private const float SeventhResolutionLargeLeapPenalty = +0.5f;
        
        // Rule A: Strong resolution bonuses/penalties when step-down resolution exists
        private const float SeventhResolutionDownStrongBonus = -8.0f;  // Increased from -6.0f for stronger preference
        private const float SeventhResolutionAvoidPenalty = +10.0f;   // Increased from +6.0f to strongly penalize upward motion when resolution available
        
        // Rule A: Normal 7th resolution (no melody doubling) - stepwise downward resolution
        private const float SeventhResolutionDownStepBonusNormal = -6.0f;
        private const float SeventhResolutionAvoidPenaltyNormal = +6.0f;
        
        // Rule A special case: 7th resolution with melody doubling
        private const float SeventhResolutionWithMelodyDoubleBonus = -4.0f;
        private const float SeventhResolutionWithMelodyDoublePenalty = +4.0f;
        
        // Rule A hard constraint: 7th resolution (non-soprano only, when valid resolution exists)
        private const float SeventhResolutionDownStepBonusHard = -10.0f;
        private const float SeventhResolutionHardPenalty = +1000.0f;
        
        // Voice crossing hard constraint: prevents Bass > Tenor, Tenor > Alto, Alto > Soprano
        private const float VoiceCrossingPenalty = +100000f; // Effectively vetoes candidates with voice crossing
        
        // Voice spacing hard constraints: maximum allowed intervals between adjacent voices
        private const int MaxSopranoAltoInterval = 12;  // One octave
        private const int MaxAltoTenorInterval = 12;   // One octave
        private const int MaxTenorBassInterval = 24;   // Two octaves
        
        // Voice spacing soft penalties: applied as cost terms during candidate evaluation
        private const float SpacingLargePenalty = 200f;        // big but not infinite
        private const float SpacingPreferredPenalty = 40f;     // medium, for "not ideal but okay"
        private const float SpacingBassTenorPenalty = 15f;     // small
        
        // Common tone 3rd→7th preference (for diatonic 7th-chord chains)
        private const float CommonThirdToSeventhBonus = -3.0f;
        
        // Leading tone rule softening when next chord needs its 7th
        private const float LeadingToneSoftenFactor = 0.1f; // Multiply leading-tone bonus by this when 7th coverage is at risk
        
        /// <summary>
        /// Sets the debug flag for tendency rule logging.
        /// </summary>
        public static void SetTendencyDebug(bool enabled)
        {
            enableTendencyDebug = enabled;
        }

        /// <summary>
        /// Gets the current state of the tendency debug flag.
        /// </summary>
        public static bool GetTendencyDebug()
        {
            return enableTendencyDebug;
        }

        /// <summary>
        /// Voices a single chord into 3-4 parts using simple block voicing (close position).
        /// This is a heuristic v1 implementation - not full chorale rules.
        /// </summary>
        /// <param name="chordEvent">The chord event to voice</param>
        /// <param name="numVoices">Number of voices (3 or 4). Clamped to [3, 4] range.</param>
        /// <param name="bassOctave">Octave for the bass voice (default: 3, around MIDI 48)</param>
        /// <param name="upperMinMidi">Minimum MIDI note for upper voices (default: 55, around G3)</param>
        /// <param name="upperMaxMidi">Maximum MIDI note for upper voices (default: 80, around G5)</param>
        /// <returns>VoicedChord with bass and upper voices in close position</returns>
        public static VoicedChord VoiceFirstChord(
            ChordEvent chordEvent,
            int numVoices = 4,
            int bassOctave = 3,
            int upperMinMidi = 55,
            int upperMaxMidi = 80,
            ChordEvent? nextChordEvent = null)
        {
            // Clamp numVoices to valid range [3, 4]
            if (numVoices < 3) numVoices = 3;
            if (numVoices > 4) numVoices = 4;

            // Compute bass voice: use inversion-aware bass pitch class
            int bassPc = GetBassPitchClassForChord(chordEvent.Key, chordEvent.Recipe);
            
            // Calculate MIDI note for bass in the specified octave
            // Start with C in the bass octave, then add semitones to reach the target pitch class
            int bassMidi = (bassOctave + 1) * 12 + bassPc;
            int bassVoice = bassMidi;

            // Calculate root pitch class directly from the recipe (regardless of inversion)
            int rootPc = TheoryScale.GetDegreePitchClass(chordEvent.Key, chordEvent.Recipe.Degree);
            if (rootPc < 0)
            {
                rootPc = 0; // Fallback to C
            }
            rootPc = (rootPc + chordEvent.Recipe.RootSemitoneOffset) % 12;
            if (rootPc < 0) rootPc += 12;

            // Determine if this is a 7th chord
            bool hasSeventh = chordEvent.Recipe.Extension == ChordExtension.Seventh &&
                              chordEvent.Recipe.SeventhQuality != SeventhQuality.None;

            // Calculate intervals from root based on chord quality
            int thirdInterval, fifthInterval, seventhInterval = 0;
            
            switch (chordEvent.Recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4;  // major third
                    fifthInterval = 7; // perfect fifth
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3;  // minor third
                    fifthInterval = 7;  // perfect fifth
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3;  // minor third
                    fifthInterval = 6;  // diminished fifth
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4;  // major third
                    fifthInterval = 8;  // augmented fifth
                    break;
                default:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
            }

            // Calculate 7th interval if present
            if (hasSeventh)
            {
                switch (chordEvent.Recipe.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11; // major 7th
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10; // minor 7th
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9; // diminished 7th
                        break;
                    default:
                        seventhInterval = 10;
                        break;
                }
            }

            // Calculate pitch classes for each chord tone
            int thirdPc = (rootPc + thirdInterval) % 12;
            int fifthPc = (rootPc + fifthInterval) % 12;
            int seventhPc = hasSeventh ? (rootPc + seventhInterval) % 12 : -1;

            // Resolution-aware 7th placement: if the chord has a 7th and we know the next chord,
            // try to place the 7th at a MIDI height that allows downward step resolution
            int? resolutionAwareSeventhMidi = null;
            bool canLookAhead = hasSeventh && nextChordEvent.HasValue && seventhPc >= 0;
            
            if (canLookAhead)
            {
                var nextChord = nextChordEvent.Value;
                
                // Compute next chord's pitch classes (similar to current chord calculation)
                int nextRootPc = TheoryScale.GetDegreePitchClass(nextChord.Key, nextChord.Recipe.Degree);
                if (nextRootPc < 0) nextRootPc = 0;
                nextRootPc = (nextRootPc + nextChord.Recipe.RootSemitoneOffset) % 12;
                if (nextRootPc < 0) nextRootPc += 12;
                
                // Calculate intervals for next chord
                int nextThirdInterval, nextFifthInterval, nextSeventhInterval = 0;
                switch (nextChord.Recipe.Quality)
                {
                    case ChordQuality.Major:
                        nextThirdInterval = 4;
                        nextFifthInterval = 7;
                        break;
                    case ChordQuality.Minor:
                        nextThirdInterval = 3;
                        nextFifthInterval = 7;
                        break;
                    case ChordQuality.Diminished:
                        nextThirdInterval = 3;
                        nextFifthInterval = 6;
                        break;
                    case ChordQuality.Augmented:
                        nextThirdInterval = 4;
                        nextFifthInterval = 8;
                        break;
                    default:
                        nextThirdInterval = 4;
                        nextFifthInterval = 7;
                        break;
                }
                
                bool nextHasSeventh = nextChord.Recipe.Extension == ChordExtension.Seventh &&
                                     nextChord.Recipe.SeventhQuality != SeventhQuality.None;
                if (nextHasSeventh)
                {
                    switch (nextChord.Recipe.SeventhQuality)
                    {
                        case SeventhQuality.Major7:
                            nextSeventhInterval = 11;
                            break;
                        case SeventhQuality.Minor7:
                        case SeventhQuality.Dominant7:
                        case SeventhQuality.HalfDiminished7:
                            nextSeventhInterval = 10;
                            break;
                        case SeventhQuality.Diminished7:
                            nextSeventhInterval = 9;
                            break;
                        default:
                            nextSeventhInterval = 10;
                            break;
                    }
                }
                
                int nextThirdPc = (nextRootPc + nextThirdInterval) % 12;
                int nextFifthPc = (nextRootPc + nextFifthInterval) % 12;
                int nextSeventhPc = nextHasSeventh ? (nextRootPc + nextSeventhInterval) % 12 : -1;
                
                // Build list of next chord's pitch classes
                var nextChordPcs = new List<int> { nextRootPc, nextThirdPc, nextFifthPc };
                if (nextHasSeventh && nextSeventhPc >= 0)
                {
                    nextChordPcs.Add(nextSeventhPc);
                }
                var nextChordPcsSet = new HashSet<int>(nextChordPcs);
                
                // Compute valid resolution pitch classes (1-2 semitones down from the 7th)
                int resPcMinus1 = (seventhPc + 11) % 12;  // -1 semitone
                int resPcMinus2 = (seventhPc + 10) % 12;  // -2 semitones
                
                // Helper to check if next chord contains a pitch class
                bool NextChordContains(int pc)
                {
                    return nextChordPcsSet.Contains(pc);
                }
                
                // Check if the next chord contains either resolution pitch class
                bool hasValidResolution = NextChordContains(resPcMinus1) || NextChordContains(resPcMinus2);
                
                if (hasValidResolution)
                {
                    // Search for good 7th placements within the upper voice range
                    int mid = (upperMinMidi + upperMaxMidi) / 2;
                    
                    for (int midi = upperMinMidi; midi <= upperMaxMidi; midi++)
                    {
                        // Only consider MIDI notes with the 7th's pitch class
                        if ((midi % 12 + 12) % 12 != seventhPc)
                            continue;
                        
                        // Potential downward step resolutions
                        int candRes1 = midi - 1;
                        int candRes2 = midi - 2;
                        
                        // Check if candidate resolution 1 is valid (1 semitone down)
                        bool cand1Ok = candRes1 >= upperMinMidi && candRes1 <= upperMaxMidi;
                        if (cand1Ok)
                        {
                            int candRes1Pc = (candRes1 % 12 + 12) % 12;
                            cand1Ok = NextChordContains(candRes1Pc) && 
                                     (candRes1Pc == resPcMinus1 || candRes1Pc == resPcMinus2);
                        }
                        
                        // Check if candidate resolution 2 is valid (2 semitones down)
                        bool cand2Ok = candRes2 >= upperMinMidi && candRes2 <= upperMaxMidi;
                        if (cand2Ok)
                        {
                            int candRes2Pc = (candRes2 % 12 + 12) % 12;
                            cand2Ok = NextChordContains(candRes2Pc) && 
                                     (candRes2Pc == resPcMinus1 || candRes2Pc == resPcMinus2);
                        }
                        
                        if (cand1Ok || cand2Ok)
                        {
                            // Prefer placement closest to middle of the range for smoother spacing
                            if (!resolutionAwareSeventhMidi.HasValue ||
                                Math.Abs(midi - mid) < Math.Abs(resolutionAwareSeventhMidi.Value - mid))
                            {
                                resolutionAwareSeventhMidi = midi;
                            }
                        }
                    }
                    
                    if (resolutionAwareSeventhMidi.HasValue && enableTendencyDebug)
                    {
                        string seventhName = TheoryPitch.GetPitchNameFromMidi(resolutionAwareSeventhMidi.Value, chordEvent.Key);
                        var nextChordPcNames = new List<string>();
                        foreach (int pc in nextChordPcs)
                        {
                            // Find a representative MIDI note for this pitch class to get the name
                            int sampleMidi = (4 + 1) * 12 + pc; // Use octave 4 as sample
                            nextChordPcNames.Add(TheoryPitch.GetPitchNameFromMidi(sampleMidi, nextChord.Key));
                        }
                        UnityEngine.Debug.Log($"[FirstChord Debug] Resolution-aware 7th placement: chord={chordEvent.Recipe}, seventhPc={seventhPc}, chosenMidi={resolutionAwareSeventhMidi.Value} ({seventhName}), nextChord={nextChord.Recipe}, nextChordPcs=[{string.Join(",", nextChordPcNames)}]");
                    }
                }
            }

            // Calculate number of upper voices needed
            int upperVoices = numVoices - 1; // 2 or 3 upper voices

            // Build upper voices in close position
            var upperVoicesMidi = new List<int>();

            // Handle melody constraint if present
            if (chordEvent.MelodyMidi.HasValue)
            {
                int melodyMidi = chordEvent.MelodyMidi.Value;
                int melodyPc = (melodyMidi % 12 + 12) % 12;
                
                // Force soprano (highest voice) to melody note
                upperVoicesMidi.Add(melodyMidi);
                
                // Get chord tone pitch classes (excluding melody if it matches one)
                var availablePcs = new List<int> { rootPc, thirdPc, fifthPc };
                bool seventhIsMelody = hasSeventh && (melodyPc == seventhPc);
                
                if (hasSeventh && !seventhIsMelody)
                {
                    availablePcs.Add(seventhPc);
                }
                
                // Remove melody pitch class from available pool to avoid duplicates
                availablePcs.RemoveAll(pc => pc == melodyPc);
                
                // Generate candidates below the melody note
                var candidates = GenerateCandidatesInRange(availablePcs, upperMinMidi, Math.Min(upperMaxMidi, melodyMidi - 1));
                
                // If we have resolution-aware 7th placement and 7th is not the melody, prefer it in candidates
                if (hasSeventh && !seventhIsMelody && resolutionAwareSeventhMidi.HasValue)
                {
                    int resolvedSeventhMidi = resolutionAwareSeventhMidi.Value;
                    // Only use resolution-aware placement if it's below the melody and in range
                    if (resolvedSeventhMidi < melodyMidi && 
                        resolvedSeventhMidi >= upperMinMidi && 
                        resolvedSeventhMidi <= upperMaxMidi &&
                        !candidates.Contains(resolvedSeventhMidi))
                    {
                        // Insert resolution-aware 7th at the front of candidates to prioritize it
                        candidates.Insert(0, resolvedSeventhMidi);
                    }
                }
                
                // Fill remaining upper voices (need upperVoices - 1 more, since melody takes one slot)
                int remainingVoices = upperVoices - 1;
                var usedPcs = new HashSet<int> { melodyPc };
                
                for (int i = 0; i < remainingVoices; i++)
                {
                    int bestCandidate = -1;
                    
                    // First, try to use candidates from the generated list
                    if (candidates.Count > 0)
                    {
                        foreach (int candidate in candidates)
                        {
                            if (candidate < melodyMidi && !upperVoicesMidi.Contains(candidate))
                            {
                                bestCandidate = candidate;
                                break; // Take first valid candidate (they're sorted)
                            }
                        }
                        
                        if (bestCandidate >= 0)
                        {
                            upperVoicesMidi.Add(bestCandidate);
                            candidates.Remove(bestCandidate);
                        }
                    }
                    
                    // If no candidate found, use fallback
                    if (bestCandidate < 0)
                    {
                        if (availablePcs.Count > 0)
                        {
                            int fallbackPc = availablePcs[0];
                            availablePcs.RemoveAt(0);
                            int fallbackMidi = PlaceInMidRegister(fallbackPc, upperMinMidi, Math.Min(upperMaxMidi, melodyMidi - 1));
                            if (fallbackMidi < melodyMidi && !upperVoicesMidi.Contains(fallbackMidi))
                            {
                                upperVoicesMidi.Add(fallbackMidi);
                            }
                            else
                            {
                                // If fallback conflicts, try an octave lower
                                fallbackMidi -= 12;
                                if (fallbackMidi >= upperMinMidi && fallbackMidi < melodyMidi && !upperVoicesMidi.Contains(fallbackMidi))
                                {
                                    upperVoicesMidi.Add(fallbackMidi);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Original logic when no melody constraint
                if (hasSeventh)
                {
                    // Determine 7th MIDI: use resolution-aware placement if available, otherwise use mid-register
                    int seventhMidi;
                    if (resolutionAwareSeventhMidi.HasValue)
                    {
                        seventhMidi = resolutionAwareSeventhMidi.Value;
                    }
                    else
                    {
                        seventhMidi = PlaceInMidRegister(seventhPc, upperMinMidi, upperMaxMidi);
                        
                        if (enableTendencyDebug && canLookAhead)
                        {
                            UnityEngine.Debug.Log($"[FirstChord Debug] No resolution-aware placement found, using mid-register for 7th: seventhPc={seventhPc}, midi={seventhMidi}");
                        }
                    }
                    
                    // 7th chord voicing
                    if (upperVoices == 2)
                    {
                        // 3 voices total: bass = root, upper = [3rd, 7th]
                        upperVoicesMidi.Add(PlaceInMidRegister(thirdPc, upperMinMidi, upperMaxMidi));
                        upperVoicesMidi.Add(seventhMidi);
                    }
                    else // upperVoices == 3
                    {
                        // 4 voices total: bass = root, upper = [3rd, 5th, 7th]
                        upperVoicesMidi.Add(PlaceInMidRegister(thirdPc, upperMinMidi, upperMaxMidi));
                        upperVoicesMidi.Add(PlaceInMidRegister(fifthPc, upperMinMidi, upperMaxMidi));
                        upperVoicesMidi.Add(seventhMidi);
                    }
                }
                else
                {
                    // Triad voicing
                    if (upperVoices == 2)
                    {
                        // 3 voices total: bass = root, upper = [3rd, 5th]
                        upperVoicesMidi.Add(PlaceInMidRegister(thirdPc, upperMinMidi, upperMaxMidi));
                        upperVoicesMidi.Add(PlaceInMidRegister(fifthPc, upperMinMidi, upperMaxMidi));
                    }
                    else // upperVoices == 3
                    {
                        // 4 voices total: bass = root, upper = [3rd, 5th, root+12]
                        upperVoicesMidi.Add(PlaceInMidRegister(thirdPc, upperMinMidi, upperMaxMidi));
                        upperVoicesMidi.Add(PlaceInMidRegister(fifthPc, upperMinMidi, upperMaxMidi));
                        
                        // Add root an octave above, ensuring it fits in range and is above the 5th
                        int rootOctaveAbove = PlaceInMidRegister(rootPc, upperMinMidi, upperMaxMidi);
                        // If root in mid register is below or equal to the 5th, move it up an octave
                        if (rootOctaveAbove <= upperVoicesMidi[upperVoicesMidi.Count - 1])
                        {
                            rootOctaveAbove += 12;
                            // Clamp to max if needed
                            if (rootOctaveAbove > upperMaxMidi)
                            {
                                rootOctaveAbove -= 12;
                            }
                        }
                        upperVoicesMidi.Add(rootOctaveAbove);
                    }
                }
            }

            // Ensure upper voices are strictly ascending
            upperVoicesMidi.Sort();

            // Assemble final voicing: bass + upper voices
            var allVoices = new List<int> { bassVoice };
            allVoices.AddRange(upperVoicesMidi);
            var voicesArray = allVoices.ToArray();
            
            // Compute spacing penalty for complete SATB voicing (for diagnostics)
            if (voicesArray.Length >= 4)
            {
                int candidateBass = voicesArray[0];
                int candidateTenor = voicesArray[1];
                int candidateAlto = voicesArray[2];
                int candidateSoprano = voicesArray[3];
                
                float spacingPenalty = GetSpacingPenalty(candidateBass, candidateTenor, candidateAlto, candidateSoprano);
                if (spacingPenalty != 0f && GetTendencyDebug())
                {
                    UnityEngine.Debug.Log(
                        $"[Spacing Soft] VoiceFirstChord: B={candidateBass}, T={candidateTenor}, A={candidateAlto}, S={candidateSoprano}, penalty={spacingPenalty}");
                }
            }
            
            // No locked resolutions for the first chord (no previous chord to resolve from)
            FixChordToneCoverage(chordEvent, voicesArray, numVoices, upperMinMidi, upperMaxMidi, protectSoprano: false, lockedResolutionVoices: null);
            
            // Compute spacing penalty after FixChordToneCoverage (for diagnostics)
            if (voicesArray.Length >= 4)
            {
                int finalBass = voicesArray[0];
                int finalTenor = voicesArray[1];
                int finalAlto = voicesArray[2];
                int finalSoprano = voicesArray[3];
                
                float spacingPenalty = GetSpacingPenalty(finalBass, finalTenor, finalAlto, finalSoprano);
                if (spacingPenalty != 0f && GetTendencyDebug())
                {
                    UnityEngine.Debug.Log(
                        $"[Spacing Soft] VoiceFirstChord after FixChordToneCoverage: B={finalBass}, T={finalTenor}, A={finalAlto}, S={finalSoprano}, penalty={spacingPenalty}");
                }
            }

            // Final validation: check for voice crossing (debug/assertion only - should never trigger after fixes)
            if (!ValidateVoiceOrder(voicesArray, out string validationError))
            {
                if (enableTendencyDebug)
                {
                    UnityEngine.Debug.LogWarning($"[Voice Crossing] Final validation failed in VoiceFirstChord: {validationError}");
                }
            }

            // Final validation: check for hard spacing violations (debug/assertion only - should never trigger after fixes)
            if (voicesArray.Length >= 4)
            {
                int finalBassMidi = voicesArray[0];
                int finalTenorMidi = voicesArray[1];
                int finalAltoMidi = voicesArray[2];
                int finalSopranoMidi = voicesArray[3];
                
                if (ViolatesHardSpacing(finalBassMidi, finalTenorMidi, finalAltoMidi, finalSopranoMidi))
                {
                    if (GetTendencyDebug())
                    {
                        UnityEngine.Debug.LogWarning($"[Spacing Final Check] Chosen voicing violates hard spacing in VoiceFirstChord: B={finalBassMidi}, T={finalTenorMidi}, A={finalAltoMidi}, S={finalSopranoMidi}");
                    }
                }
            }

            return new VoicedChord
            {
                TimeBeats = chordEvent.TimeBeats,
                VoicesMidi = voicesArray
            };
        }

        /// <summary>
        /// Convenience wrapper for voicing a single chord event with default parameters.
        /// </summary>
        /// <param name="chordEvent">The chord event to voice</param>
        /// <returns>VoicedChord with default voicing (4 voices)</returns>
        public static VoicedChord VoiceSingleEvent(ChordEvent chordEvent)
        {
            return VoiceFirstChord(chordEvent);
        }

        /// <summary>
        /// Builds a list of ChordEvent objects from a list of ChordRecipe objects.
        /// Useful for converting parsed recipes into events for voice-leading.
        /// </summary>
        /// <param name="key">The key context for all chords</param>
        /// <param name="recipes">List of chord recipes to convert</param>
        /// <param name="startBeat">Starting beat time for the first chord (default: 0f)</param>
        /// <param name="beatStep">Beat increment between chords (default: 1f)</param>
        /// <returns>List of ChordEvent objects with incremental TimeBeats</returns>
        public static List<ChordEvent> BuildChordEventsFromRecipes(
            TheoryKey key,
            IList<ChordRecipe> recipes,
            float startBeat = 0f,
            float beatStep = 1f)
        {
            var events = new List<ChordEvent>(recipes.Count);
            float t = startBeat;
            foreach (var recipe in recipes)
            {
                events.Add(new ChordEvent
                {
                    Key = key,
                    Recipe = recipe,
                    TimeBeats = t,
                    MelodyMidi = null // Phase 1: no melody locking yet
                });
                t += beatStep;
            }
            return events;
        }

        /// <summary>
        /// Voices a progression of chords with basic voice-leading.
        /// Tier 1 heuristic: keeps common tones in same voices when possible,
        /// otherwise moves each voice to the nearest chord tone in the new chord.
        /// </summary>
        /// <param name="events">Sequence of chord events to voice</param>
        /// <param name="numVoices">Number of voices (3 or 4). Clamped to [3, 4] range.</param>
        /// <param name="bassOctave">Octave for the bass voice (default: 3, around MIDI 48)</param>
        /// <param name="upperMinMidi">Minimum MIDI note for upper voices (default: 55, around G3)</param>
        /// <param name="upperMaxMidi">Maximum MIDI note for upper voices (default: 80, around G5)</param>
        /// <returns>List of VoicedChord, one per input event</returns>
        public static List<VoicedChord> VoiceLeadProgression(
            IList<ChordEvent> events,
            int numVoices = 4,
            int bassOctave = 3,
            int upperMinMidi = 55,
            int upperMaxMidi = 80)
        {
            var result = new List<VoicedChord>();

            if (events == null || events.Count == 0)
            {
                return result;
            }

            // Voice the first chord using the standard block voicing with one-step lookahead for resolution-aware 7th placement
            var nextChordEvent = events.Count > 1 ? (ChordEvent?)events[1] : null;
            var firstVoiced = VoiceFirstChord(events[0], numVoices, bassOctave, upperMinMidi, upperMaxMidi, nextChordEvent: nextChordEvent);
            result.Add(firstVoiced);

            // Voice each subsequent chord with voice-leading from the previous
            for (int i = 1; i < events.Count; i++)
            {
                var previousVoiced = result[i - 1];
                var previousEvent = events[i - 1];
                var current = events[i];
                var nextVoiced = VoiceNextChord(previousVoiced, previousEvent, current, numVoices, bassOctave, upperMinMidi, upperMaxMidi, stepIndex: i + 1);
                result.Add(nextVoiced);
            }

            return result;
        }

        /// <summary>
        /// Voices a progression of chords with basic voice-leading, respecting melody constraints.
        /// When MelodyMidi is set on a ChordEvent, the soprano (highest voice) will be locked to that MIDI note.
        /// </summary>
        /// <param name="events">Sequence of chord events to voice</param>
        /// <param name="numVoices">Number of voices (3 or 4). Clamped to [3, 4] range.</param>
        /// <param name="rootOctave">Octave for calculating upper voice ranges (default: 4)</param>
        /// <param name="bassOctave">Octave for the bass voice (default: 3, around MIDI 48)</param>
        /// <param name="upperMinMidi">Minimum MIDI note for upper voices (default: 60, around C4)</param>
        /// <param name="upperMaxMidi">Maximum MIDI note for upper voices (default: 76, around E5)</param>
        /// <returns>List of VoicedChord, one per input event</returns>
        public static List<VoicedChord> VoiceLeadProgressionWithMelody(
            IList<ChordEvent> events,
            int numVoices = 4,
            int rootOctave = 4,
            int bassOctave = 3,
            int upperMinMidi = 60,
            int upperMaxMidi = 76)
        {
            var result = new List<VoicedChord>();

            if (events == null || events.Count == 0)
            {
                return result;
            }

            // Voice the first chord using the standard block voicing (with melody support)
            // Voice the first chord using the standard block voicing with one-step lookahead for resolution-aware 7th placement
            var nextChordEvent = events.Count > 1 ? (ChordEvent?)events[1] : null;
            var firstVoiced = VoiceFirstChord(events[0], numVoices, bassOctave, upperMinMidi, upperMaxMidi, nextChordEvent: nextChordEvent);
            result.Add(firstVoiced);

            // Voice each subsequent chord with voice-leading from the previous
            for (int i = 1; i < events.Count; i++)
            {
                var previousVoiced = result[i - 1];
                var previousEvent = events[i - 1];
                var current = events[i];
                var nextVoiced = VoiceNextChord(previousVoiced, previousEvent, current, numVoices, bassOctave, upperMinMidi, upperMaxMidi, stepIndex: i + 1);
                result.Add(nextVoiced);
            }

            return result;
        }

        /// <summary>
        /// Voices the next chord in a progression with voice-leading from the previous chord.
        /// Tier 1 heuristic: keeps common tones in same voices when possible,
        /// otherwise moves each voice to the nearest chord tone.
        /// Applies soft tonal tendency rules for more classical voice-leading.
        /// </summary>
        /// <param name="previous">The previously voiced chord</param>
        /// <param name="previousEvent">The previous chord event (for tendency analysis)</param>
        /// <param name="current">The current chord event to voice</param>
        /// <param name="numVoices">Number of voices (3 or 4)</param>
        /// <param name="bassOctave">Octave for bass voice fallback</param>
        /// <param name="upperMinMidi">Minimum MIDI for upper voices</param>
        /// <param name="upperMaxMidi">Maximum MIDI for upper voices</param>
        /// <returns>VoicedChord for the current event with voice-leading</returns>
        private static VoicedChord VoiceNextChord(
            VoicedChord previous,
            ChordEvent previousEvent,
            ChordEvent current,
            int numVoices,
            int bassOctave,
            int upperMinMidi,
            int upperMaxMidi,
            int stepIndex = -1)
        {
            // Clamp numVoices to valid range [3, 4]
            if (numVoices < 3) numVoices = 3;
            if (numVoices > 4) numVoices = 4;

            // Get chord analyses for tendency rules (gracefully degrade if unavailable)
            ChordFunctionProfile previousAnalysis;
            ChordFunctionProfile currentAnalysis;
            try
            {
                previousAnalysis = TheoryChord.AnalyzeChordProfile(previousEvent.Key, previousEvent.Recipe);
                currentAnalysis = TheoryChord.AnalyzeChordProfile(current.Key, current.Recipe);
            }
            catch
            {
                // Fallback: create empty profiles if analysis fails
                previousAnalysis = new ChordFunctionProfile();
                currentAnalysis = new ChordFunctionProfile();
            }

            // Get target chord tone pitch classes for the current chord
            var chordTonePcs = GetChordTonePitchClasses(current);
            if (chordTonePcs.Count == 0)
            {
                // Fallback: use VoiceFirstChord if we can't get chord tones
                return VoiceFirstChord(current, numVoices, bassOctave, upperMinMidi, upperMaxMidi);
            }

            // Voice bass (index 0): use cost-based selection with root position preference
            int prevBass = previous.VoicesMidi[0];
            
            // Debug logging for recipe inversion
            if (enableTendencyDebug)
            {
                UnityEngine.Debug.Log($"[Voicing Debug] Step {stepIndex}: recipe inversion = {current.Recipe.Inversion}");
            }
            
            // Voice upper voices (indices 1..N-1)
            var prevUpper = new List<int>();
            for (int i = 1; i < previous.VoicesMidi.Length; i++)
            {
                prevUpper.Add(previous.VoicesMidi[i]);
            }

            // Analyze tendencies for each previous voice (for soft voice-leading rules)
            var tendencyInfos = new VoiceTendencyInfo[prevUpper.Count];
            for (int v = 0; v < prevUpper.Count; v++)
            {
                tendencyInfos[v] = AnalyzeVoiceTendencies(
                    prevUpper[v],
                    previousEvent.Key,
                    previousEvent.Recipe,
                    previousAnalysis);
            }
            
            // Track which selected MIDI corresponds to which previous voice index (for 7th resolution enforcement)
            // Maps: previous voice index -> selected MIDI (before sorting)
            var voiceSelectionMap = new Dictionary<int, int>();

            int bassVoice = FindBestBassNoteWithInversionPreference(
                prevBass, current.Key, current.Recipe, bassOctave, stepIndex: stepIndex);

            // Handle melody constraint if present
            var upperVoices = new List<int>();
            int? targetSopranoMidi = null; // Target soprano MIDI (computed early, locked at end)
            int? melodyPcForLock = null; // Melody pitch class for final verification
            
                if (current.MelodyMidi.HasValue)
                {
                    // CRITICAL: Compute target soprano MIDI early, preserving melody pitch class
                    // The melodyMidi already has any octave offset applied by ChordLabController
                    int melodyMidi = current.MelodyMidi.Value;
                    int melodyPc = (melodyMidi % 12 + 12) % 12;
                    melodyPcForLock = melodyPc;
                    
                    // Calculate target soprano MIDI: start with melody MIDI, adjust octave to stay in range
                    int targetSoprano = melodyMidi;
                    
                    // Adjust by octaves so it sits in [upperMinMidi, upperMaxMidi]
                    while (targetSoprano < upperMinMidi) targetSoprano += 12;
                    while (targetSoprano > upperMaxMidi) targetSoprano -= 12;
                    
                    // Verify pitch class is preserved (should always be true, but check anyway)
                    int targetPc = (targetSoprano % 12 + 12) % 12;
                    if (targetPc != melodyPc)
                    {
                        // Force pitch class match by recalculating from melody pitch class
                        int targetOctave = targetSoprano / 12;
                        targetSoprano = targetOctave * 12 + melodyPc;
                        
                        // Re-adjust octave if needed
                        while (targetSoprano < upperMinMidi) targetSoprano += 12;
                        while (targetSoprano > upperMaxMidi) targetSoprano -= 12;
                    }
                    
                    targetSopranoMidi = targetSoprano;
                
                // Remove melody pitch class from available pool to avoid duplicates in inner voices
                var availablePcs = new List<int>(chordTonePcs);
                availablePcs.RemoveAll(pc => pc == melodyPc);
                
                // Generate candidates below the melody note (for inner voices only)
                var candidates = GenerateCandidatesInRange(availablePcs, upperMinMidi, Math.Min(upperMaxMidi, targetSoprano - 1));
                
                // Fill remaining upper voices (need numVoices - 2 more: one for bass, one for melody)
                int remainingVoices = numVoices - 2;
                var usedCandidates = new HashSet<int> { melodyMidi };
                
                // Try to preserve common tones from previous chord, but below melody
                // Use float costs with tendency adjustments for voice-leading
                for (int voiceIdx = 0; voiceIdx < prevUpper.Count && remainingVoices > 0; voiceIdx++)
                {
                    int prevVoice = prevUpper[voiceIdx];
                    int prevPc = (prevVoice % 12 + 12) % 12;
                    
                    // Determine which voice we're selecting based on how many we've already selected
                    // voiceIdx=0, upperVoices.Count=0 -> selecting Tenor (first upper voice)
                    // voiceIdx=1, upperVoices.Count=1 -> selecting Alto (second upper voice)
                    bool isSelectingTenor = (upperVoices.Count == 0);
                    bool isSelectingAlto = (upperVoices.Count == 1);
                    
                    // Get known voices for spacing checks
                    int knownTenor = isSelectingTenor ? -1 : (upperVoices.Count > 0 ? upperVoices[0] : -1);
                    int knownAlto = isSelectingAlto ? -1 : (upperVoices.Count > 1 ? upperVoices[1] : -1);
                    int knownSoprano = targetSopranoMidi.HasValue ? targetSopranoMidi.Value : -1;
                    bool hasSoprano = targetSopranoMidi.HasValue;
                    
                    int bestNote = -1;
                    float bestCost = float.MaxValue;
                    float chosenBaseCost = 0f;
                    float chosenTendAdjust = 0f;
                    
                    // Spacing-aware candidate tracking
                    float bestCostWithSpacing = float.MaxValue;
                    int bestMidiWithSpacing = -1;
                    float bestCostNoSpacing = float.MaxValue;
                    int bestMidiNoSpacing = -1;
                    
                    // Get tendency info for this voice (gracefully handle if index out of range)
                    var tendencyInfo = voiceIdx < tendencyInfos.Length ? tendencyInfos[voiceIdx] : new VoiceTendencyInfo { midiNote = prevVoice };
                    
                    // Build voice-specific candidate list, starting with normal candidates
                    var voiceCandidates = new List<int>(candidates);
                    
                    // For local leading tones, inject a nearby target root candidate (within ±3 semitones)
                    if (tendencyInfo.isLocalLeadingTone && tendencyInfo.localTargetRootPc >= 0)
                    {
                        int targetRootMidi = FindNearbyTargetRootForLocalLeadingTone(
                            prevVoice,
                            tendencyInfo.localTargetRootPc);
                        
                        // Add target root candidate if found and not already in list
                        // For this local-leading-tone case, we ignore range constraints (±3 semitones is almost always safe)
                        // Only check it's below melody to avoid voice crossing
                        if (targetRootMidi >= 0 && !voiceCandidates.Contains(targetRootMidi) && targetRootMidi < melodyMidi)
                        {
                            voiceCandidates.Add(targetRootMidi);
                        }
                    }
                    
                    // For chord 7ths, inject a resolution candidate (step down by 1-2 semitones)
                    if (tendencyInfo.isChordSeventh)
                    {
                        int seventhPc = (prevVoice % 12 + 12) % 12;
                        var chordTonePcsSet = new HashSet<int>(chordTonePcs);
                        int resolutionMidi = FindSeventhResolutionCandidate(
                            prevVoice,
                            seventhPc,
                            upperMinMidi,
                            Math.Min(upperMaxMidi, melodyMidi - 1),
                            chordTonePcsSet,
                            current.MelodyMidi);
                        
                        if (resolutionMidi >= 0 && !voiceCandidates.Contains(resolutionMidi) && resolutionMidi < melodyMidi)
                        {
                            voiceCandidates.Add(resolutionMidi);
                            
                            if (enableTendencyDebug)
                            {
                                int resolutionPc = (resolutionMidi % 12 + 12) % 12;
                                UnityEngine.Debug.Log($"[Tendency Debug] Injected 7th-resolution candidate: prev={prevVoice} ({seventhPc}), injected={resolutionMidi} ({resolutionPc})");
                            }
                        }
                    }
                    
                    // First, look for common tone (same pitch class) below melody
                    foreach (int candidate in voiceCandidates)
                    {
                        if (usedCandidates.Contains(candidate) || candidate >= melodyMidi)
                            continue;
                        
                        int candidatePc = (candidate % 12 + 12) % 12;
                        if (candidatePc == prevPc)
                        {
                            // Convert integer distance to float base cost
                            float baseCost = Math.Abs(candidate - prevVoice);
                            
                            // Check for common-tone 3rd→7th bonus: if prev voice is 3rd of current chord
                            // and candidate (same pitch) would be 7th of next chord
                            float commonThirdToSeventhAdjust = 0f;
                            var currentChordTonePcs = GetChordTonePitchClasses(new ChordEvent { Key = current.Key, Recipe = previousEvent.Recipe });
                            if (currentChordTonePcs.Count >= 2)
                            {
                                int currentThirdPc = currentChordTonePcs[1]; // Index 1 is 3rd
                                bool prevIsThirdOfCurrent = (prevPc == currentThirdPc);
                                
                                if (prevIsThirdOfCurrent)
                                {
                                    // Check if next chord has a 7th and if this pitch class is that 7th
                                    bool nextChordHasSeventh = current.Recipe.Extension == ChordExtension.Seventh &&
                                                              current.Recipe.SeventhQuality != SeventhQuality.None;
                                    if (nextChordHasSeventh && chordTonePcs.Count >= 4)
                                    {
                                        int nextSeventhPc = chordTonePcs[3]; // Index 3 is 7th
                                        if (candidatePc == nextSeventhPc)
                                        {
                                            commonThirdToSeventhAdjust = CommonThirdToSeventhBonus;
                                            
                                            if (enableTendencyDebug)
                                            {
                                                string prevName = TheoryPitch.GetPitchNameFromMidi(prevVoice, current.Key);
                                                string candName = TheoryPitch.GetPitchNameFromMidi(candidate, current.Key);
                                                UnityEngine.Debug.Log($"[Tendency Debug] Common 3rd→7th: prev={prevName}(3rd of {previousEvent.Recipe}) held as {candName}(7th of {current.Recipe}), bonus={CommonThirdToSeventhBonus}");
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Check for voice crossing: candidate must be > bass and > all previously selected upper voices
                            if (WouldCauseVoiceCrossing(candidate, bassVoice, upperVoices, out string crossingError))
                            {
                                // Veto this candidate - skip it entirely
                                if (enableTendencyDebug)
                                {
                                    UnityEngine.Debug.Log($"[Voice Crossing] {crossingError}");
                                }
                                continue; // Skip to next candidate
                            }
                            
                            // Apply tendency adjustments (inner voices only, not soprano)
                            // Pass voice range for hard 7th resolution constraint
                            float tendAdjust = ComputeTendencyCostAdjustment(
                                tendencyInfo, candidate, current.Key,
                                previousEvent.Recipe, current.Recipe,
                                previousAnalysis, currentAnalysis,
                                isSoprano: false, nextMelodyMidi: current.MelodyMidi,
                                voiceMinMidi: upperMinMidi, voiceMaxMidi: Math.Min(upperMaxMidi, melodyMidi - 1));
                            
                            float totalCost = baseCost + tendAdjust + commonThirdToSeventhAdjust;
                            
                            // Check spacing constraints based on which voice we're selecting
                            bool spacingOK = true;
                            if (isSelectingTenor)
                            {
                                // Tenor spacing: Tenor - Bass ≤ MaxTenorBassInterval
                                int gapTB = candidate - bassVoice;
                                spacingOK = gapTB <= MaxTenorBassInterval;
                            }
                            else if (isSelectingAlto)
                            {
                                // Alto spacing: Alto - Tenor ≤ MaxAltoTenorInterval
                                int gapAT = candidate - knownTenor;
                                spacingOK = gapAT <= MaxAltoTenorInterval;
                                
                                // Also check Soprano - Alto if soprano is known
                                if (hasSoprano && spacingOK)
                                {
                                    int gapSA = knownSoprano - candidate;
                                    spacingOK = gapSA <= MaxSopranoAltoInterval;
                                }
                            }
                            
                            // Track best candidate with spacing constraint
                            if (spacingOK)
                            {
                                if (totalCost < bestCostWithSpacing)
                                {
                                    bestCostWithSpacing = totalCost;
                                    bestMidiWithSpacing = candidate;
                                }
                            }
                            
                            // Always track best overall candidate for fallback
                            if (totalCost < bestCostNoSpacing)
                            {
                                bestCostNoSpacing = totalCost;
                                bestMidiNoSpacing = candidate;
                            }
                            
                            // Keep old logic for backward compatibility during transition
                            if (totalCost < bestCost)
                            {
                                bestNote = candidate;
                                bestCost = totalCost;
                                chosenBaseCost = baseCost;
                                chosenTendAdjust = tendAdjust;
                            }
                        }
                    }
                    
                    // If no common tone found, find nearest chord tone below melody with tendency adjustments
                    if (bestMidiWithSpacing < 0 && bestMidiNoSpacing < 0)
                    {
                        foreach (int candidate in voiceCandidates)
                        {
                            if (usedCandidates.Contains(candidate) || candidate >= melodyMidi)
                                continue;
                            
                            // Convert integer distance to float base cost
                            float baseCost = Math.Abs(candidate - prevVoice);
                            
                            // Check for voice crossing: candidate must be > bass and > all previously selected upper voices
                            if (WouldCauseVoiceCrossing(candidate, bassVoice, upperVoices, out string crossingError))
                            {
                                // Veto this candidate with huge penalty
                                if (enableTendencyDebug)
                                {
                                    UnityEngine.Debug.Log($"[Voice Crossing] {crossingError}");
                                }
                                // Don't evaluate this candidate further - skip it
                                continue;
                            }
                            
                            // Apply tendency adjustments (inner voices only, not soprano)
                            // Pass voice range for hard 7th resolution constraint
                            float tendAdjust = ComputeTendencyCostAdjustment(
                                tendencyInfo, candidate, current.Key,
                                previousEvent.Recipe, current.Recipe,
                                previousAnalysis, currentAnalysis,
                                isSoprano: false, nextMelodyMidi: current.MelodyMidi,
                                voiceMinMidi: upperMinMidi, voiceMaxMidi: Math.Min(upperMaxMidi, melodyMidi - 1));
                            
                            float totalCost = baseCost + tendAdjust;
                            
                            // Check spacing constraints based on which voice we're selecting
                            bool spacingOK = true;
                            if (isSelectingTenor)
                            {
                                // Tenor spacing: Tenor - Bass ≤ MaxTenorBassInterval
                                int gapTB = candidate - bassVoice;
                                spacingOK = gapTB <= MaxTenorBassInterval;
                            }
                            else if (isSelectingAlto)
                            {
                                // Alto spacing: Alto - Tenor ≤ MaxAltoTenorInterval
                                int gapAT = candidate - knownTenor;
                                spacingOK = gapAT <= MaxAltoTenorInterval;
                                
                                // Also check Soprano - Alto if soprano is known
                                if (hasSoprano && spacingOK)
                                {
                                    int gapSA = knownSoprano - candidate;
                                    spacingOK = gapSA <= MaxSopranoAltoInterval;
                                }
                            }
                            
                            // Track best candidate with spacing constraint
                            if (spacingOK)
                            {
                                if (totalCost < bestCostWithSpacing)
                                {
                                    bestCostWithSpacing = totalCost;
                                    bestMidiWithSpacing = candidate;
                                }
                            }
                            
                            // Always track best overall candidate for fallback
                            if (totalCost < bestCostNoSpacing)
                            {
                                bestCostNoSpacing = totalCost;
                                bestMidiNoSpacing = candidate;
                            }
                            
                            // Keep old logic for backward compatibility during transition
                            if (totalCost < bestCost)
                            {
                                bestNote = candidate;
                                bestCost = totalCost;
                                chosenBaseCost = baseCost;
                                chosenTendAdjust = tendAdjust;
                            }
                        }
                    }
                    
                    // Choose best candidate: prefer spacing-constrained if available, otherwise fallback
                    int chosenNote = -1;
                    float finalCost = float.MaxValue;
                    float finalBaseCost = 0f;
                    float finalTendAdjust = 0f;
                    
                    if (bestMidiWithSpacing >= 0)
                    {
                        // At least one candidate satisfied spacing – use it
                        chosenNote = bestMidiWithSpacing;
                        finalCost = bestCostWithSpacing;
                        // Recompute base cost and tendency adjust for chosen note (for logging)
                        finalBaseCost = Math.Abs(chosenNote - prevVoice);
                        finalTendAdjust = ComputeTendencyCostAdjustment(
                            tendencyInfo, chosenNote, current.Key,
                            previousEvent.Recipe, current.Recipe,
                            previousAnalysis, currentAnalysis,
                            isSoprano: false, nextMelodyMidi: current.MelodyMidi,
                            voiceMinMidi: upperMinMidi, voiceMaxMidi: Math.Min(upperMaxMidi, melodyMidi - 1));
                    }
                    else if (bestMidiNoSpacing >= 0)
                    {
                        // Fallback: no candidate fit spacing, use best overall and log
                        chosenNote = bestMidiNoSpacing;
                        finalCost = bestCostNoSpacing;
                        // Recompute base cost and tendency adjust for chosen note (for logging)
                        finalBaseCost = Math.Abs(chosenNote - prevVoice);
                        finalTendAdjust = ComputeTendencyCostAdjustment(
                            tendencyInfo, chosenNote, current.Key,
                            previousEvent.Recipe, current.Recipe,
                            previousAnalysis, currentAnalysis,
                            isSoprano: false, nextMelodyMidi: current.MelodyMidi,
                            voiceMinMidi: upperMinMidi, voiceMaxMidi: Math.Min(upperMaxMidi, melodyMidi - 1));
                        
                        if (GetTendencyDebug())
                        {
                            if (isSelectingTenor)
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[Spacing Relax Tenor] No tenor candidate satisfied spacing; using best overall: " +
                                    $"Bass={bassVoice}, Tenor={chosenNote}");
                            }
                            else if (isSelectingAlto)
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[Spacing Relax Alto] No alto candidate satisfied spacing; using best overall: " +
                                    $"B={bassVoice}, T={knownTenor}, A={chosenNote}, S={(hasSoprano ? knownSoprano : -1)}");
                            }
                        }
                    }
                    else if (bestNote >= 0)
                    {
                        // Fallback to old logic if spacing tracking didn't find anything
                        chosenNote = bestNote;
                        finalCost = bestCost;
                        finalBaseCost = chosenBaseCost;
                        finalTendAdjust = chosenTendAdjust;
                    }
                    
                    if (chosenNote >= 0)
                    {
                        // Debug logging for tendency cases
                        LogTendencyDebugInfo(
                            stepIndex: stepIndex,
                            voiceIndex: voiceIdx + 1,
                            tendencyInfo,
                            prevVoice,
                            chosenNote,
                            current.Key,
                            current.Recipe,
                            currentAnalysis,
                            finalBaseCost,
                            finalTendAdjust,
                            finalCost);
                        
                        // Store mapping: previous voice index -> selected MIDI (for 7th resolution enforcement)
                        if (!voiceSelectionMap.ContainsKey(voiceIdx))
                        {
                            voiceSelectionMap[voiceIdx] = chosenNote;
                        }
                        
                        upperVoices.Add(chosenNote);
                        usedCandidates.Add(chosenNote);
                        remainingVoices--;
                    }
                }
                
                // Fill any remaining slots with available chord tones below melody
                while (remainingVoices > 0 && candidates.Count > 0)
                {
                    int bestCandidate = -1;
                    foreach (int candidate in candidates)
                    {
                        if (candidate < melodyMidi && !usedCandidates.Contains(candidate))
                        {
                            bestCandidate = candidate;
                            break;
                        }
                    }
                    
                    if (bestCandidate >= 0)
                    {
                        upperVoices.Add(bestCandidate);
                        usedCandidates.Add(bestCandidate);
                        candidates.Remove(bestCandidate);
                        remainingVoices--;
                    }
                    else
                    {
                        // Fallback: use PlaceInMidRegister for remaining pitch classes
                        if (availablePcs.Count > 0)
                        {
                            int fallbackPc = availablePcs[0];
                            availablePcs.RemoveAt(0);
                            int fallbackMidi = PlaceInMidRegister(fallbackPc, upperMinMidi, Math.Min(upperMaxMidi, melodyMidi - 1));
                            if (fallbackMidi < melodyMidi && !usedCandidates.Contains(fallbackMidi))
                            {
                                upperVoices.Add(fallbackMidi);
                                usedCandidates.Add(fallbackMidi);
                                remainingVoices--;
                            }
                        }
                        else
                        {
                            break; // No more options
                        }
                    }
                }
            }
            else
            {
                // Original logic when no melody constraint
                // Generate candidate MIDI notes for upper voices
                var candidates = GenerateCandidatesInRange(chordTonePcs, upperMinMidi, upperMaxMidi);

                // Assign upper voices: try common tones first, then nearest chord tone
                // Use float costs with tendency adjustments for voice-leading
                var usedCandidates = new HashSet<int>();

                for (int voiceIdx = 0; voiceIdx < prevUpper.Count; voiceIdx++)
                {
                    int prevVoice = prevUpper[voiceIdx];
                    int prevPc = (prevVoice % 12 + 12) % 12;
                    int bestNote = -1;
                    float bestCost = float.MaxValue;
                    float chosenBaseCost = 0f;
                    float chosenTendAdjust = 0f;

                    // Get tendency info for this voice (gracefully handle if index out of range)
                    var tendencyInfo = voiceIdx < tendencyInfos.Length ? tendencyInfos[voiceIdx] : new VoiceTendencyInfo { midiNote = prevVoice };

                    // Determine if this will be the soprano (highest voice) - it's the last one we're assigning
                    bool willBeSoprano = (voiceIdx == prevUpper.Count - 1);
                    
                    // Build voice-specific candidate list, starting with normal candidates
                    var voiceCandidates = new List<int>(candidates);
                    
                    // For local leading tones, inject a nearby target root candidate (within ±3 semitones)
                    if (tendencyInfo.isLocalLeadingTone && tendencyInfo.localTargetRootPc >= 0)
                    {
                        int targetRootMidi = FindNearbyTargetRootForLocalLeadingTone(
                            prevVoice,
                            tendencyInfo.localTargetRootPc);
                        
                        // Add target root candidate if found and not already in list
                        // For this local-leading-tone case, we ignore range constraints (±3 semitones is almost always safe)
                        if (targetRootMidi >= 0 && !voiceCandidates.Contains(targetRootMidi))
                        {
                            voiceCandidates.Add(targetRootMidi);
                        }
                    }
                    
                    // For chord 7ths, inject a resolution candidate (step down by 1-2 semitones)
                    if (tendencyInfo.isChordSeventh)
                    {
                        int seventhPc2 = (prevVoice % 12 + 12) % 12;
                        var chordTonePcsSet = new HashSet<int>(chordTonePcs);
                        int resolutionMidi = FindSeventhResolutionCandidate(
                            prevVoice,
                            seventhPc2,
                            upperMinMidi,
                            upperMaxMidi,
                            chordTonePcsSet,
                            current.MelodyMidi);
                        
                        if (resolutionMidi >= 0 && !voiceCandidates.Contains(resolutionMidi))
                        {
                            voiceCandidates.Add(resolutionMidi);
                            
                            if (enableTendencyDebug)
                            {
                                int resolutionPc = (resolutionMidi % 12 + 12) % 12;
                                UnityEngine.Debug.Log($"[Tendency Debug] Injected 7th-resolution candidate: prev={prevVoice} ({seventhPc2}), injected={resolutionMidi} ({resolutionPc})");
                            }
                        }
                    }

                    // First, look for common tone (same pitch class)
                    foreach (int candidate in voiceCandidates)
                    {
                        if (usedCandidates.Contains(candidate))
                            continue;

                        int candidatePc = (candidate % 12 + 12) % 12;
                        if (candidatePc == prevPc)
                        {
                            // Convert integer distance to float base cost
                            float baseCost = Math.Abs(candidate - prevVoice);
                            
                            // Check for common-tone 3rd→7th bonus: if prev voice is 3rd of current chord
                            // and candidate (same pitch) would be 7th of next chord
                            float commonThirdToSeventhAdjust = 0f;
                            var currentChordTonePcs = GetChordTonePitchClasses(new ChordEvent { Key = current.Key, Recipe = previousEvent.Recipe });
                            if (currentChordTonePcs.Count >= 2)
                            {
                                int currentThirdPc = currentChordTonePcs[1]; // Index 1 is 3rd
                                bool prevIsThirdOfCurrent = (prevPc == currentThirdPc);
                                
                                if (prevIsThirdOfCurrent)
                                {
                                    // Check if next chord has a 7th and if this pitch class is that 7th
                                    bool nextChordHasSeventh = current.Recipe.Extension == ChordExtension.Seventh &&
                                                              current.Recipe.SeventhQuality != SeventhQuality.None;
                                    if (nextChordHasSeventh && chordTonePcs.Count >= 4)
                                    {
                                        int nextSeventhPc = chordTonePcs[3]; // Index 3 is 7th
                                        if (candidatePc == nextSeventhPc)
                                        {
                                            commonThirdToSeventhAdjust = CommonThirdToSeventhBonus;
                                            
                                            if (enableTendencyDebug)
                                            {
                                                string prevName = TheoryPitch.GetPitchNameFromMidi(prevVoice, current.Key);
                                                string candName = TheoryPitch.GetPitchNameFromMidi(candidate, current.Key);
                                                UnityEngine.Debug.Log($"[Tendency Debug] Common 3rd→7th: prev={prevName}(3rd of {previousEvent.Recipe}) held as {candName}(7th of {current.Recipe}), bonus={CommonThirdToSeventhBonus}");
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Check for voice crossing: candidate must be > bass and > all previously selected upper voices
                            if (WouldCauseVoiceCrossing(candidate, bassVoice, upperVoices, out string crossingError2))
                            {
                                // Veto this candidate - skip it entirely
                                if (enableTendencyDebug)
                                {
                                    UnityEngine.Debug.Log($"[Voice Crossing] {crossingError2}");
                                }
                                continue; // Skip to next candidate
                            }
                            
                            // Apply tendency adjustments
                            // Pass voice range for hard 7th resolution constraint
                            // When there's no melody, soprano also needs voice range for hard 7th rule
                            bool noMelodyForSoprano2 = !current.MelodyMidi.HasValue;
                            float tendAdjust = ComputeTendencyCostAdjustment(
                                tendencyInfo, candidate, current.Key,
                                previousEvent.Recipe, current.Recipe,
                                previousAnalysis, currentAnalysis,
                                isSoprano: willBeSoprano, nextMelodyMidi: current.MelodyMidi,
                                voiceMinMidi: (willBeSoprano && !noMelodyForSoprano2) ? -1 : upperMinMidi, 
                                voiceMaxMidi: (willBeSoprano && !noMelodyForSoprano2) ? -1 : upperMaxMidi);
                            
                            float totalCost = baseCost + tendAdjust + commonThirdToSeventhAdjust;
                            
                            if (totalCost < bestCost)
                            {
                                bestNote = candidate;
                                bestCost = totalCost;
                                chosenBaseCost = baseCost;
                                chosenTendAdjust = tendAdjust + commonThirdToSeventhAdjust;
                            }
                        }
                    }

                    // If no common tone found, find nearest chord tone with tendency adjustments
                    if (bestNote < 0)
                    {
                        foreach (int candidate in voiceCandidates)
                        {
                            if (usedCandidates.Contains(candidate))
                                continue;

                            // Convert integer distance to float base cost
                            float baseCost = Math.Abs(candidate - prevVoice);
                            
                            // Check for voice crossing: candidate must be > bass and > all previously selected upper voices
                            if (WouldCauseVoiceCrossing(candidate, bassVoice, upperVoices, out string crossingError3))
                            {
                                // Veto this candidate - skip it entirely
                                if (enableTendencyDebug)
                                {
                                    UnityEngine.Debug.Log($"[Voice Crossing] {crossingError3}");
                                }
                                continue; // Skip to next candidate
                            }
                            
                            // Apply tendency adjustments
                            // Pass voice range for hard 7th resolution constraint
                            // When there's no melody, soprano also needs voice range for hard 7th rule
                            bool noMelodyForNearest = !current.MelodyMidi.HasValue;
                            float tendAdjust = ComputeTendencyCostAdjustment(
                                tendencyInfo, candidate, current.Key,
                                previousEvent.Recipe, current.Recipe,
                                previousAnalysis, currentAnalysis,
                                isSoprano: willBeSoprano, nextMelodyMidi: current.MelodyMidi,
                                voiceMinMidi: (willBeSoprano && !noMelodyForNearest) ? -1 : upperMinMidi, 
                                voiceMaxMidi: (willBeSoprano && !noMelodyForNearest) ? -1 : upperMaxMidi);
                            
                            float totalCost = baseCost + tendAdjust;
                            
                            if (totalCost < bestCost)
                            {
                                bestNote = candidate;
                                bestCost = totalCost;
                                chosenBaseCost = baseCost;
                                chosenTendAdjust = tendAdjust;
                            }
                        }
                    }

                    // If still no candidate found, fallback to mid register placement
                    if (bestNote < 0)
                    {
                        // Use first available chord tone pitch class
                        int fallbackPc = chordTonePcs.Count > 1 ? chordTonePcs[1] : chordTonePcs[0];
                        bestNote = PlaceInMidRegister(fallbackPc, upperMinMidi, upperMaxMidi);
                        chosenBaseCost = Math.Abs(bestNote - prevVoice);
                        chosenTendAdjust = 0f;
                    }

                    // Debug logging for tendency cases
                    if (bestNote >= 0)
                    {
                        LogTendencyDebugInfo(
                            stepIndex: stepIndex,
                            voiceIndex: voiceIdx + 1,
                            tendencyInfo,
                            prevVoice,
                            bestNote,
                            current.Key,
                            current.Recipe,
                            currentAnalysis,
                            chosenBaseCost,
                            chosenTendAdjust,
                            bestCost);
                    }

                    // Store mapping: previous voice index -> selected MIDI (for 7th resolution enforcement)
                    if (!voiceSelectionMap.ContainsKey(voiceIdx))
                    {
                        voiceSelectionMap[voiceIdx] = bestNote;
                    }

                    upperVoices.Add(bestNote);
                    usedCandidates.Add(bestNote);
                }
            }

            // Ensure upper voices are strictly ascending (low→high)
            // Sort inner voices (soprano will be added separately if melody is present)
            upperVoices.Sort();
            
            // Add soprano at highest position (if melody was present)
            if (targetSopranoMidi.HasValue)
            {
                // Check that soprano doesn't cause crossing
                if (upperVoices.Count > 0 && targetSopranoMidi.Value <= upperVoices[upperVoices.Count - 1])
                {
                    if (enableTendencyDebug)
                    {
                        UnityEngine.Debug.Log($"[Voice Crossing] Soprano ({targetSopranoMidi.Value}) <= highest upper voice ({upperVoices[upperVoices.Count - 1]})");
                    }
                    // This should not happen if melody constraint is set correctly, but log if it does
                }
                upperVoices.Add(targetSopranoMidi.Value);
            }

            // Assemble final voicing
            var allVoices = new List<int> { bassVoice };
            allVoices.AddRange(upperVoices);
            var voicesArray = allVoices.ToArray();
            
            // Compute spacing penalty for complete SATB voicing (for diagnostics)
            // Note: This is computed after selection, so it doesn't influence selection in the current incremental architecture
            if (voicesArray.Length >= 4)
            {
                int candidateBass = voicesArray[0];
                int candidateTenor = voicesArray[1];
                int candidateAlto = voicesArray[2];
                int candidateSoprano = voicesArray[3];
                
                float spacingPenalty = GetSpacingPenalty(candidateBass, candidateTenor, candidateAlto, candidateSoprano);
                if (spacingPenalty != 0f && GetTendencyDebug())
                {
                    UnityEngine.Debug.Log(
                        $"[Spacing Soft] B={candidateBass}, T={candidateTenor}, A={candidateAlto}, S={candidateSoprano}, penalty={spacingPenalty}");
                }
            }
            
            // Fix chord tone coverage: ensure required tones are present
            // NOTE: This only modifies upper voices (indices 1..N-1); bass (index 0) remains unchanged.
            // When melody is present, soprano (highest voice) is also protected and not modified.
            bool melodyLocked = current.MelodyMidi.HasValue;
            bool noMelody = !current.MelodyMidi.HasValue;
            
            // Track which voices have hard-locked 7th resolutions (to protect them from fix-up logic)
            // lockedResolutionVoices[i] = true means voicesArray[i] should not be modified by FixChordToneCoverage
            bool[] lockedResolutionVoices = new bool[voicesArray.Length];
            
            // Post-selection enforcement: Force 7th resolution (inner voices always, soprano when no melody)
            // This overrides the cost-based selection to ensure correct 7th resolution even when
            // other candidates would have won based on cost alone.
            // We use voiceSelectionMap to track which selected MIDI corresponds to which previous voice index.
            if (voicesArray.Length >= 4 && voiceSelectionMap.Count > 0)
            {
                // Get chord tone pitch classes for resolution checking
                var chordTonePcsSet = new HashSet<int>(chordTonePcs);
                
                // Determine max voice index to check: include soprano when there's no melody
                int maxVoiceIndexToCheck = voicesArray.Length - 1; // Include soprano by default
                if (melodyLocked && !noMelody)
                {
                    maxVoiceIndexToCheck = voicesArray.Length - 2; // Exclude soprano when melody is locked
                }
                
                // After sorting, match voices to their previous voice indices
                // by finding which selected MIDI in voicesArray matches which entry in voiceSelectionMap
                for (int voiceArrayIndex = 1; voiceArrayIndex <= maxVoiceIndexToCheck; voiceArrayIndex++)
                {
                    int selectedMidi = voicesArray[voiceArrayIndex];
                    
                    // Find which previous voice index this selected MIDI corresponds to
                    int matchingPrevVoiceIndex = -1;
                    foreach (var kvp in voiceSelectionMap)
                    {
                        if (kvp.Value == selectedMidi)
                        {
                            matchingPrevVoiceIndex = kvp.Key;
                            break;
                        }
                    }
                    
                    // If we found a match and that previous voice was a 7th, enforce resolution
                    if (matchingPrevVoiceIndex >= 0 && 
                        matchingPrevVoiceIndex < tendencyInfos.Length && 
                        matchingPrevVoiceIndex < prevUpper.Count)
                    {
                        var tendencyInfo = tendencyInfos[matchingPrevVoiceIndex];
                        
                        if (tendencyInfo.isChordSeventh)
                        {
                            int prevMidi = prevUpper[matchingPrevVoiceIndex];
                            int prevPc = (prevMidi % 12 + 12) % 12;
                            
                            // Determine valid resolution pitch classes (1-2 semitones down)
                            int resolutionPc1 = (prevPc + 11) % 12;   // -1 semitone
                            int resolutionPc2 = (prevPc + 10) % 12;   // -2 semitones
                            
                            // Check if the next chord contains either resolution pitch class
                            bool hasResolutionTone = chordTonePcsSet.Contains(resolutionPc1) || chordTonePcsSet.Contains(resolutionPc2);
                            
                            if (hasResolutionTone)
                            {
                                // Determine voice range for this voice
                                int voiceMinMidi = upperMinMidi;
                                int voiceMaxMidi = upperMaxMidi;
                                
                                // Check if this is the soprano voice (last voice index)
                                bool isSopranoVoice = (voiceArrayIndex == voicesArray.Length - 1);
                                
                                // If melody is present, inner voices should be below soprano
                                // But if there's no melody, soprano can use the full range
                                if (targetSopranoMidi.HasValue && (!noMelody || !isSopranoVoice))
                                {
                                    voiceMaxMidi = Math.Min(voiceMaxMidi, targetSopranoMidi.Value - 1);
                                }
                                
                                // Try to find a downward resolution (prioritize resolutionPc1, then resolutionPc2)
                                int resolutionMidi = -1;
                                
                                // Try resolutionPc1 first (more common -1 semitone resolution)
                                if (chordTonePcsSet.Contains(resolutionPc1))
                                {
                                    resolutionMidi = FindDownwardSeventhResolution(prevMidi, resolutionPc1, voiceMinMidi, voiceMaxMidi);
                                }
                                
                                // Try resolutionPc2 if resolutionPc1 didn't yield a result
                                if (resolutionMidi < 0 && chordTonePcsSet.Contains(resolutionPc2))
                                {
                                    resolutionMidi = FindDownwardSeventhResolution(prevMidi, resolutionPc2, voiceMinMidi, voiceMaxMidi);
                                }
                                
                                // If we found a valid resolution MIDI and the current choice is different, override it
                                if (resolutionMidi >= 0 && selectedMidi != resolutionMidi)
                                {
                                    voicesArray[voiceArrayIndex] = resolutionMidi;
                                    
                                    if (enableTendencyDebug)
                                    {
                                        string voiceLabel = (voiceArrayIndex == 1) ? "Tenor" : (voiceArrayIndex == 2) ? "Alto" : (isSopranoVoice ? "Soprano" : "Voice" + voiceArrayIndex);
                                        string prevName = TheoryPitch.GetPitchNameFromMidi(prevMidi, current.Key);
                                        string resolutionName = TheoryPitch.GetPitchNameFromMidi(resolutionMidi, current.Key);
                                        string beforeName = TheoryPitch.GetPitchNameFromMidi(selectedMidi, current.Key);
                                        UnityEngine.Debug.Log($"[Tendency Debug] Forcing 7th resolution: voice={voiceLabel}, prev={prevName}({prevMidi}) -> forced={resolutionName}({resolutionMidi}) (was {beforeName}({selectedMidi}))");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Detect and lock voices with hard 7th resolutions (to protect them from fix-up logic)
            // Check each voice (inner voices, and soprano if no melody) to see if it represents a hard-locked 7th resolution
            // We check all previous voices to see if any of them was a 7th that resolves to this voice's current MIDI
            // Note: noMelody is already defined above
            var chordTonePcsSetForLock = new HashSet<int>(chordTonePcs);
            // Include soprano in locking logic when there's no melody (so it can be locked like inner voices)
            int maxVoiceIndexForLocking = voicesArray.Length - 1; // Include all voices (soprano is last)
            if (melodyLocked && !noMelody)
            {
                maxVoiceIndexForLocking = voicesArray.Length - 2; // Exclude soprano when melody is locked
            }
            for (int voiceArrayIndex = 1; voiceArrayIndex <= maxVoiceIndexForLocking; voiceArrayIndex++)
            {
                int selectedMidi = voicesArray[voiceArrayIndex];
                int selectedPc = (selectedMidi % 12 + 12) % 12;
                
                // Check each previous voice to see if this could be its 7th resolution
                for (int prevVoiceIdx = 0; prevVoiceIdx < prevUpper.Count && prevVoiceIdx < tendencyInfos.Length; prevVoiceIdx++)
                {
                    var tendencyInfo = tendencyInfos[prevVoiceIdx];
                    
                    // Skip if this previous voice wasn't a 7th
                    if (!tendencyInfo.isChordSeventh)
                        continue;
                    
                    int prevMidi = prevUpper[prevVoiceIdx];
                    int prevPc = (prevMidi % 12 + 12) % 12;
                    
                    // Determine valid resolution pitch classes (1-2 semitones down)
                    int resolutionPc1 = (prevPc + 11) % 12;   // -1 semitone
                    int resolutionPc2 = (prevPc + 10) % 12;   // -2 semitones
                    
                    // Check if the current voice's pitch class is a valid resolution for this 7th
                    bool isResolvingToThisVoice = (selectedPc == resolutionPc1 || selectedPc == resolutionPc2);
                    
                    if (isResolvingToThisVoice)
                    {
                        // Check if the next chord contains this resolution pitch class
                        bool hasResolutionTone = chordTonePcsSetForLock.Contains(resolutionPc1) || chordTonePcsSetForLock.Contains(resolutionPc2);
                        
                        if (hasResolutionTone)
                        {
                            // Determine voice range for checking
                            int voiceMinMidi = upperMinMidi;
                            int voiceMaxMidi = upperMaxMidi;
                            
                            // Check if this is the soprano voice (last voice index)
                            bool isSopranoVoice = (voiceArrayIndex == voicesArray.Length - 1);
                            
                            // If melody is present, inner voices should be below soprano
                            // But if there's no melody, soprano can use the full range
                            if (targetSopranoMidi.HasValue && (!noMelody || !isSopranoVoice))
                            {
                                voiceMaxMidi = Math.Min(voiceMaxMidi, targetSopranoMidi.Value - 1);
                            }
                            
                            // Try to find the expected downward resolution MIDI
                            int expectedResolutionMidi = -1;
                            
                            // Try resolutionPc1 first
                            if (chordTonePcsSetForLock.Contains(resolutionPc1))
                            {
                                expectedResolutionMidi = FindDownwardSeventhResolution(prevMidi, resolutionPc1, voiceMinMidi, voiceMaxMidi);
                            }
                            
                            // Try resolutionPc2 if resolutionPc1 didn't yield a result
                            if (expectedResolutionMidi < 0 && chordTonePcsSetForLock.Contains(resolutionPc2))
                            {
                                expectedResolutionMidi = FindDownwardSeventhResolution(prevMidi, resolutionPc2, voiceMinMidi, voiceMaxMidi);
                            }
                            
                            // If the selected MIDI matches the expected resolution AND a valid resolution exists in range,
                            // this means Rule A HARD was applied (or post-selection enforcement forced it), so lock this voice
                            if (expectedResolutionMidi >= 0 && selectedMidi == expectedResolutionMidi)
                            {
                                lockedResolutionVoices[voiceArrayIndex] = true;
                                
                                if (enableTendencyDebug)
                                {
                                    string voiceLabel = (voiceArrayIndex == 1) ? "Tenor" : (voiceArrayIndex == 2) ? "Alto" : (isSopranoVoice ? "Soprano" : "Voice" + voiceArrayIndex);
                                    string prevName = TheoryPitch.GetPitchNameFromMidi(prevMidi, current.Key);
                                    string resolutionName = TheoryPitch.GetPitchNameFromMidi(selectedMidi, current.Key);
                                    UnityEngine.Debug.Log($"[Voicing Debug] Locking 7th resolution in voice {voiceArrayIndex} ({voiceLabel}) at step {stepIndex}: prev={prevName}({prevMidi}), chosen={resolutionName}({selectedMidi})");
                                }
                                
                                // Break after finding a match (each voice can only resolve one 7th)
                                break;
                            }
                        }
                    }
                }
            }
            
            // Debug logging before FixChordToneCoverage
            if (enableTendencyDebug && stepIndex >= 0)
            {
                var sbBefore = new System.Text.StringBuilder();
                sbBefore.Append($"[Voicing Debug] Step {stepIndex} BEFORE FixChordToneCoverage: ");
                for (int v = 0; v < voicesArray.Length; v++)
                {
                    int midi = voicesArray[v];
                    string name = TheoryPitch.GetPitchNameFromMidi(midi, current.Key);
                    string voiceLabel = (v == 0) ? "Bass" : (v == 1) ? "Tenor" : (v == 2) ? "Alto" : "Soprano";
                    sbBefore.Append($"{voiceLabel}={name}({midi}) ");
                }
                UnityEngine.Debug.Log(sbBefore.ToString());
            }
            
            FixChordToneCoverage(current, voicesArray, numVoices, upperMinMidi, upperMaxMidi, protectSoprano: melodyLocked, lockedResolutionVoices: lockedResolutionVoices);
            
            // Compute spacing penalty after FixChordToneCoverage (for diagnostics)
            if (voicesArray.Length >= 4)
            {
                int finalBass = voicesArray[0];
                int finalTenor = voicesArray[1];
                int finalAlto = voicesArray[2];
                int finalSoprano = voicesArray[3];
                
                float spacingPenalty = GetSpacingPenalty(finalBass, finalTenor, finalAlto, finalSoprano);
                if (spacingPenalty != 0f && GetTendencyDebug())
                {
                    UnityEngine.Debug.Log(
                        $"[Spacing Soft] After FixChordToneCoverage: B={finalBass}, T={finalTenor}, A={finalAlto}, S={finalSoprano}, penalty={spacingPenalty}");
                }
            }
            
            // Debug logging after FixChordToneCoverage
            if (enableTendencyDebug && stepIndex >= 0)
            {
                var sbAfter = new System.Text.StringBuilder();
                sbAfter.Append($"[Voicing Debug] Step {stepIndex} AFTER FixChordToneCoverage: ");
                for (int v = 0; v < voicesArray.Length; v++)
                {
                    int midi = voicesArray[v];
                    string name = TheoryPitch.GetPitchNameFromMidi(midi, current.Key);
                    string voiceLabel = (v == 0) ? "Bass" : (v == 1) ? "Tenor" : (v == 2) ? "Alto" : "Soprano";
                    sbAfter.Append($"{voiceLabel}={name}({midi}) ");
                }
                UnityEngine.Debug.Log(sbAfter.ToString());
            }

            // Final validation: check for voice crossing (debug/assertion only - should never trigger after fixes)
            if (!ValidateVoiceOrder(voicesArray, out string validationError))
            {
                if (enableTendencyDebug)
                {
                    UnityEngine.Debug.LogWarning($"[Voice Crossing] Final validation failed at step {stepIndex}: {validationError}");
                }
            }

            // Final validation: check for hard spacing violations (debug/assertion only - should never trigger after fixes)
            if (voicesArray.Length >= 4)
            {
                int finalBassMidi = voicesArray[0];
                int finalTenorMidi = voicesArray[1];
                int finalAltoMidi = voicesArray[2];
                int finalSopranoMidi = voicesArray[3];
                
                if (ViolatesHardSpacing(finalBassMidi, finalTenorMidi, finalAltoMidi, finalSopranoMidi))
                {
                    if (GetTendencyDebug())
                    {
                        UnityEngine.Debug.LogWarning($"[Spacing Final Check] Chosen voicing violates hard spacing at step {stepIndex}: B={finalBassMidi}, T={finalTenorMidi}, A={finalAltoMidi}, S={finalSopranoMidi}");
                    }
                }
            }

            return new VoicedChord
            {
                TimeBeats = current.TimeBeats,
                VoicesMidi = voicesArray
            };
        }

        /// <summary>
        /// Gets the pitch class that should be in the bass based on the chord's inversion.
        /// Inversion-aware: respects Root, First (3rd in bass), Second (5th in bass), Third (7th in bass).
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="recipe">The chord recipe with inversion information</param>
        /// <returns>Pitch class (0-11) for the bass note</returns>
        private static int GetBassPitchClassForChord(TheoryKey key, ChordRecipe recipe)
        {
            // Calculate root pitch class
            int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPc < 0)
            {
                rootPc = 0; // Fallback to C
            }
            rootPc = (rootPc + recipe.RootSemitoneOffset) % 12;
            if (rootPc < 0) rootPc += 12;

            // Root position: bass is the root
            if (recipe.Inversion == ChordInversion.Root)
            {
                return rootPc;
            }

            // Calculate intervals from root based on chord quality
            int thirdInterval, fifthInterval, seventhInterval = 0;

            switch (recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3;
                    fifthInterval = 7;
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3;
                    fifthInterval = 6;
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4;
                    fifthInterval = 8;
                    break;
                default:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
            }

            // Calculate 7th interval if needed for third inversion
            bool hasSeventh = recipe.Extension == ChordExtension.Seventh &&
                              recipe.SeventhQuality != SeventhQuality.None;
            if (hasSeventh && recipe.Inversion == ChordInversion.Third)
            {
                switch (recipe.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11;
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10;
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9;
                        break;
                    default:
                        seventhInterval = 10;
                        break;
                }
            }

            // Return bass pitch class based on inversion
            switch (recipe.Inversion)
            {
                case ChordInversion.First:
                    // First inversion: 3rd in bass
                    return (rootPc + thirdInterval) % 12;
                case ChordInversion.Second:
                    // Second inversion: 5th in bass
                    return (rootPc + fifthInterval) % 12;
                case ChordInversion.Third:
                    // Third inversion: 7th in bass (only valid for 7th chords)
                    if (hasSeventh)
                    {
                        return (rootPc + seventhInterval) % 12;
                    }
                    // Fallback: if no 7th, treat as root position
                    return rootPc;
                default:
                    return rootPc;
            }
        }

        /// <summary>
        /// Extracts chord tone pitch classes from a chord event.
        /// Returns list of pitch classes: [root, 3rd, 5th, 7th] (7th only if present).
        /// </summary>
        private static List<int> GetChordTonePitchClasses(ChordEvent chordEvent)
        {
            var pitchClasses = new List<int>();

            // Calculate root pitch class
            int rootPc = TheoryScale.GetDegreePitchClass(chordEvent.Key, chordEvent.Recipe.Degree);
            if (rootPc < 0)
            {
                rootPc = 0; // Fallback to C
            }
            rootPc = (rootPc + chordEvent.Recipe.RootSemitoneOffset) % 12;
            if (rootPc < 0) rootPc += 12;
            pitchClasses.Add(rootPc);

            // Determine if this is a 7th chord
            bool hasSeventh = chordEvent.Recipe.Extension == ChordExtension.Seventh &&
                              chordEvent.Recipe.SeventhQuality != SeventhQuality.None;

            // Calculate intervals from root based on chord quality
            int thirdInterval, fifthInterval, seventhInterval = 0;

            switch (chordEvent.Recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3;
                    fifthInterval = 7;
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3;
                    fifthInterval = 6;
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4;
                    fifthInterval = 8;
                    break;
                default:
                    thirdInterval = 4;
                    fifthInterval = 7;
                    break;
            }

            // Calculate 7th interval if present
            if (hasSeventh)
            {
                switch (chordEvent.Recipe.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11;
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10;
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9;
                        break;
                    default:
                        seventhInterval = 10;
                        break;
                }
            }

            // Calculate pitch classes for each chord tone
            int thirdPc = (rootPc + thirdInterval) % 12;
            int fifthPc = (rootPc + fifthInterval) % 12;
            pitchClasses.Add(thirdPc);
            pitchClasses.Add(fifthPc);

            if (hasSeventh)
            {
                int seventhPc = (rootPc + seventhInterval) % 12;
                pitchClasses.Add(seventhPc);
            }

            return pitchClasses;
        }

        /// <summary>
        /// Generates all candidate MIDI notes for given pitch classes within a range.
        /// Returns all valid MIDI notes (pc + 12*k) that fall within [minMidi, maxMidi].
        /// </summary>
        private static List<int> GenerateCandidatesInRange(List<int> pitchClasses, int minMidi, int maxMidi)
        {
            var candidates = new List<int>();

            foreach (int pc in pitchClasses)
            {
                // Find the lowest octave that fits in range
                int octave = (minMidi - pc) / 12;
                int midi = octave * 12 + pc;

                // Generate all octaves within range
                while (midi <= maxMidi)
                {
                    if (midi >= minMidi)
                    {
                        candidates.Add(midi);
                    }
                    midi += 12;
                }
            }

            return candidates;
        }

        /// <summary>
        /// Finds the nearest MIDI note within [minMidi, maxMidi] whose pitch class equals targetPitchClass
        /// and whose absolute distance from referenceMidi is minimal.
        /// Returns -1 if no such note exists in range.
        /// </summary>
        private static int FindNearestPitchClassInRange(
            int targetPitchClass,
            int referenceMidi,
            int minMidi,
            int maxMidi)
        {
            // Normalize pitch class to 0-11
            targetPitchClass = (targetPitchClass % 12 + 12) % 12;
            
            // Find the first occurrence of targetPitchClass at or below maxMidi
            int baseOctave = (maxMidi - targetPitchClass) / 12;
            int candidate = baseOctave * 12 + targetPitchClass;
            
            int bestCandidate = -1;
            int bestDistance = int.MaxValue;
            
            // Search downward from maxMidi to minMidi
            while (candidate >= minMidi - 12) // Allow one octave below min to catch edge cases
            {
                if (candidate >= minMidi && candidate <= maxMidi)
                {
                    int distance = Math.Abs(candidate - referenceMidi);
                    if (distance < bestDistance)
                    {
                        bestCandidate = candidate;
                        bestDistance = distance;
                    }
                }
                candidate -= 12;
            }
            
            // Also search upward (in case we're near the bottom of range)
            candidate = baseOctave * 12 + targetPitchClass + 12;
            while (candidate <= maxMidi + 12) // Allow one octave above max
            {
                if (candidate >= minMidi && candidate <= maxMidi)
                {
                    int distance = Math.Abs(candidate - referenceMidi);
                    if (distance < bestDistance)
                    {
                        bestCandidate = candidate;
                        bestDistance = distance;
                    }
                }
                candidate += 12;
            }
            
            return bestCandidate;
        }

        /// <summary>
        /// Finds a nearby target root for local leading tone resolution.
        /// Only searches within ±3 semitones of the current note.
        /// Prioritizes upward movement (1-3 semitones up) then downward (1-3 semitones down).
        /// Returns -1 if no target root is found within this small window.
        /// </summary>
        private static int FindNearbyTargetRootForLocalLeadingTone(
            int fromMidi,
            int targetPitchClass)
        {
            // Normalize pitch class to 0-11
            targetPitchClass = (targetPitchClass % 12 + 12) % 12;
            
            // First, try upward moves (1-3 semitones)
            for (int delta = 1; delta <= 3; delta++)
            {
                int candidate = fromMidi + delta;
                int candidatePc = (candidate % 12 + 12) % 12;
                if (candidatePc == targetPitchClass)
                {
                    return candidate;
                }
            }
            
            // Then, try downward moves (1-3 semitones)
            for (int delta = 1; delta <= 3; delta++)
            {
                int candidate = fromMidi - delta;
                int candidatePc = (candidate % 12 + 12) % 12;
                if (candidatePc == targetPitchClass)
                {
                    return candidate;
                }
            }
            
            // No target root found within ±3 semitones
            return -1;
        }

        /// <summary>
        /// Finds a nearby resolution candidate for a chord 7th.
        /// Prioritizes downward step resolution in the same octave region (e.g., G3→F3, not G3→F4).
        /// Searches for a pitch class that is 1-2 semitones below the 7th's pitch class.
        /// Returns -1 if no valid resolution is found within range.
        /// </summary>
        private static int FindSeventhResolutionCandidate(
            int fromMidi,
            int fromPc,
            int minMidi,
            int maxMidi,
            HashSet<int> chordTonePcs,
            int? melodyMidi = null)
        {
            // First, check if melody is on a valid resolution tone (1-2 semitones down)
            if (melodyMidi.HasValue && melodyMidi.Value >= 0)
            {
                int melodyPc = (melodyMidi.Value % 12 + 12) % 12;
                int pcDown1 = (fromPc + 11) % 12;   // -1 semitone
                int pcDown2 = (fromPc + 10) % 12;   // -2 semitones
                
                if (melodyPc == pcDown1 || melodyPc == pcDown2)
                {
                    // Melody is on a valid resolution tone - use that pitch class
                    if (chordTonePcs.Contains(melodyPc))
                    {
                        // Prefer downward resolution in same octave region
                        int downwardCandidate = FindDownwardSeventhResolution(fromMidi, melodyPc, minMidi, maxMidi);
                        if (downwardCandidate >= 0)
                            return downwardCandidate;
                        
                        // Fallback to nearest if downward not found
                        return FindNearestPitchClassInRange(melodyPc, fromMidi, minMidi, maxMidi);
                    }
                }
            }
            
            // Fallback: find any chord tone that's 1-2 semitones down
            int fallbackPcDown1 = (fromPc + 11) % 12;   // -1 semitone
            int fallbackPcDown2 = (fromPc + 10) % 12;   // -2 semitones
            
            // Try fallbackPcDown1 first (more common resolution) - prioritize downward
            if (chordTonePcs.Contains(fallbackPcDown1))
            {
                int downwardCandidate = FindDownwardSeventhResolution(fromMidi, fallbackPcDown1, minMidi, maxMidi);
                if (downwardCandidate >= 0)
                    return downwardCandidate;
                
                // Fallback to nearest if downward not found
                int candidate = FindNearestPitchClassInRange(fallbackPcDown1, fromMidi, minMidi, maxMidi);
                if (candidate >= 0)
                    return candidate;
            }
            
            // Try fallbackPcDown2 as fallback - prioritize downward
            if (chordTonePcs.Contains(fallbackPcDown2))
            {
                int downwardCandidate = FindDownwardSeventhResolution(fromMidi, fallbackPcDown2, minMidi, maxMidi);
                if (downwardCandidate >= 0)
                    return downwardCandidate;
                
                // Fallback to nearest if downward not found
                int candidate = FindNearestPitchClassInRange(fallbackPcDown2, fromMidi, minMidi, maxMidi);
                if (candidate >= 0)
                    return candidate;
            }
            
            return -1;
        }
        
        /// <summary>
        /// Finds a downward step resolution candidate for a 7th, starting from prevMidi - 1 and scanning downward.
        /// Returns the closest valid downward step (1-2 semitones) in the same octave region.
        /// Returns -1 if no such candidate exists within range.
        /// </summary>
        private static int FindDownwardSeventhResolution(
            int prevMidi,
            int targetPc,
            int minMidi,
            int maxMidi)
        {
            // Start from prevMidi - 1 and scan downward to find the closest valid resolution
            // This ensures we get G3→F3, not G3→F4
            for (int offset = 1; offset <= 2; offset++) // Check 1-2 semitones down
            {
                int candidateMidi = prevMidi - offset;
                if (candidateMidi < minMidi)
                    break; // Gone below range
                
                int candidatePc = (candidateMidi % 12 + 12) % 12;
                if (candidatePc == targetPc)
                {
                    // Found a valid downward step resolution
                    return candidateMidi;
                }
            }
            
            // If no step-down found in immediate range, try one octave down
            for (int offset = 10; offset <= 11; offset++) // Check 10-11 semitones down (octave - 1 or 2)
            {
                int candidateMidi = prevMidi - offset;
                if (candidateMidi < minMidi)
                    break; // Gone below range
                
                int candidatePc = (candidateMidi % 12 + 12) % 12;
                if (candidatePc == targetPc)
                {
                    // Found a valid downward resolution (octave down)
                    return candidateMidi;
                }
            }
            
            return -1;
        }

        /// <summary>
        /// Finds the nearest bass note with the target pitch class, starting from the previous bass.
        /// Tries ±2 octaves from previous bass, within reasonable bass band [36, 60].
        /// Allows one candidate slightly above the upper bound (≤2 semitones) if it produces a smaller interval.
        /// </summary>
        private static int FindNearestBassNote(int prevBass, int targetPc, int fallbackOctave)
        {
            const int bassMinMidi = 36; // C2
            const int bassMaxMidi = 60; // C4

            int bestInRangeNote = -1;
            int bestInRangeDistance = int.MaxValue;

            // Generate all in-range candidates for targetPc
            // Try ±2 octaves from previous bass to find candidates with targetPc
            for (int octaveOffset = -2; octaveOffset <= 2; octaveOffset++)
            {
                int candidate = prevBass + (octaveOffset * 12);
                int candidatePc = (candidate % 12 + 12) % 12;

                if (candidatePc == targetPc && candidate >= bassMinMidi && candidate <= bassMaxMidi)
                {
                    int distance = Math.Abs(candidate - prevBass);
                    if (distance < bestInRangeDistance)
                    {
                        bestInRangeNote = candidate;
                        bestInRangeDistance = distance;
                    }
                }
            }

            // Also generate candidates directly from targetPc within range
            int startOctave = (bassMinMidi - targetPc + 11) / 12; // Ceiling division
            int directCandidate = startOctave * 12 + targetPc;
            if (directCandidate < bassMinMidi)
            {
                directCandidate += 12;
            }

            while (directCandidate <= bassMaxMidi)
            {
                int distance = Math.Abs(directCandidate - prevBass);
                if (distance < bestInRangeDistance)
                {
                    bestInRangeNote = directCandidate;
                    bestInRangeDistance = distance;
                }
                directCandidate += 12;
            }

            // Generate one candidate an octave above the highest in-range candidate (even if slightly out of bounds)
            int octaveAbove = (bassMaxMidi - targetPc) / 12 + 1;
            int outOfRangeCandidate = octaveAbove * 12 + targetPc;
            
            // Check if this out-of-range candidate is within 2 semitones of the upper bound
            bool isWithin2Semitones = (outOfRangeCandidate - bassMaxMidi) <= 2;
            
            int bestNote = bestInRangeNote;
            
            if (isWithin2Semitones && bestInRangeNote >= 0)
            {
                int outOfRangeDistance = Math.Abs(outOfRangeCandidate - prevBass);
                
                // If out-of-range candidate is closer than the best in-range candidate, prefer it
                if (outOfRangeDistance < bestInRangeDistance)
                {
                    bestNote = outOfRangeCandidate;
                }
            }

            // If no candidate found in band, fallback to root in bassOctave
            if (bestNote < 0)
            {
                int rootMidi = (fallbackOctave + 1) * 12 + targetPc;
                bestNote = rootMidi;
            }

            return bestNote;
        }

        /// <summary>
        /// Finds the nearest root pitch class for bass within +/-12 semitones of the previous bass.
        /// Used to guarantee a root candidate is available when inversion is not explicit.
        /// </summary>
        private static int FindNearestRootForBass(int prevBassMidi, int rootPitchClass)
        {
            int bestMidi = -1;
            int bestDistance = int.MaxValue;

            // Search up and down within +/- 12 semitones
            for (int delta = -12; delta <= 12; delta++)
            {
                if (delta == 0) continue;
                
                int candidate = prevBassMidi + delta;
                if (candidate < 0) continue; // Safety guard

                int pc = candidate % 12;
                if (pc < 0) pc += 12;

                if (pc == rootPitchClass)
                {
                    int dist = Math.Abs(delta);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestMidi = candidate;
                    }
                }
            }

            return bestMidi;
        }

        /// <summary>
        /// Finds the best bass note with root position preference when no inversion is specified.
        /// Considers all chord tones (root, 3rd, 5th, 7th) and selects based on distance + inversion preference.
        /// </summary>
        private static int FindBestBassNoteWithInversionPreference(
            int prevBass,
            TheoryKey key,
            ChordRecipe recipe,
            int bassOctave,
            int stepIndex = -1)
        {
            const int bassMinMidi = 36; // C2
            const int bassMaxMidi = 60; // C4

            // Calculate root pitch class
            int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPc < 0) rootPc = 0;
            rootPc = (rootPc + recipe.RootSemitoneOffset) % 12;
            if (rootPc < 0) rootPc += 12;

            // Get chord tone pitch classes
            var chordTonePcs = new HashSet<int> { rootPc };
            
            // Add 3rd, 5th, 7th based on chord quality
            int thirdInterval, fifthInterval, seventhInterval = 0;
            switch (recipe.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4; fifthInterval = 7;
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3; fifthInterval = 7;
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3; fifthInterval = 6;
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4; fifthInterval = 8;
                    break;
                default:
                    thirdInterval = 4; fifthInterval = 7;
                    break;
            }

            int thirdPc = (rootPc + thirdInterval) % 12;
            int fifthPc = (rootPc + fifthInterval) % 12;
            chordTonePcs.Add(thirdPc);
            chordTonePcs.Add(fifthPc);

            bool hasSeventh = recipe.Extension == ChordExtension.Seventh &&
                              recipe.SeventhQuality != SeventhQuality.None;
            int seventhPc = -1;
            if (hasSeventh)
            {
                // Calculate seventh interval based on seventh quality
                switch (recipe.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11;
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10;
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9;
                        break;
                    default:
                        seventhInterval = 10;
                        break;
                }
                seventhPc = (rootPc + seventhInterval) % 12;
                chordTonePcs.Add(seventhPc);
            }

            // Determine if inversion is explicitly specified (when Inversion != Root, it was parsed from Roman numeral)
            bool inversionExplicitlySpecified = (recipe.Inversion != ChordInversion.Root);
            int requestedBassPc = GetBassPitchClassForChord(key, recipe);

            // Guarantee candidates when needed
            int forcedRootCandidateMidi = -1;
            int forcedRequestedBassCandidateMidi = -1;
            
            if (!inversionExplicitlySpecified)
            {
                // Guarantee a nearby root candidate when inversion is not explicit
                forcedRootCandidateMidi = FindNearestRootForBass(prevBass, rootPc);
            }
            else
            {
                // Guarantee a nearby candidate for the requested bass when inversion is explicit
                forcedRequestedBassCandidateMidi = FindNearestRootForBass(prevBass, requestedBassPc);
            }

            // Generate candidates and compute costs
            int bestCandidate = -1;
            float bestCost = float.MaxValue;
            bool hasRootCandidateWithin7 = false;
            int bestRootMidi = -1;
            float bestRootTotalCost = float.MaxValue;

            // Track all candidates for debug logging
            var candidateMidis = new List<int>();
            var candidateDegrees = new List<string>();
            var candidateBaseCosts = new List<float>();
            var candidateTotalCosts = new List<float>();

            // First, evaluate the forced root candidate if it exists
            if (forcedRootCandidateMidi >= 0)
            {
                float baseCost = Math.Abs(forcedRootCandidateMidi - prevBass);
                float prefAdjust = -4.0f; // Strong root bonus when no inversion specified
                float totalCost = baseCost + prefAdjust;

                // Track for debug
                candidateMidis.Add(forcedRootCandidateMidi);
                candidateDegrees.Add("Root(forced)");
                candidateBaseCosts.Add(baseCost);
                candidateTotalCosts.Add(totalCost);

                // Track as best overall candidate
                if (totalCost < bestCost)
                {
                    bestCandidate = forcedRootCandidateMidi;
                    bestCost = totalCost;
                }

                // Track as root candidate
                if (baseCost <= 7.0f)
                {
                    hasRootCandidateWithin7 = true;
                    if (totalCost < bestRootTotalCost)
                    {
                        bestRootMidi = forcedRootCandidateMidi;
                        bestRootTotalCost = totalCost;
                    }
                }
            }

            // Generate candidates for all chord tones
            foreach (int candidatePc in chordTonePcs)
            {
                // HARD CONSTRAINT: Filter out candidates with wrong pitch class based on inversion
                if (inversionExplicitlySpecified)
                {
                    // Explicit inversion: ONLY allow candidates matching the required bass pitch class
                    if (candidatePc != requestedBassPc)
                    {
                        // Skip this candidate entirely - don't evaluate cost
                        if (enableTendencyDebug)
                        {
                            // Generate one candidate to log (for debug purposes)
                            int debugStartOct = (bassMinMidi - candidatePc + 11) / 12;
                            int debugCand = debugStartOct * 12 + candidatePc;
                            if (debugCand < bassMinMidi) debugCand += 12;
                            if (debugCand <= bassMaxMidi + 2)
                            {
                                UnityEngine.Debug.Log($"[Inversion Veto] Rejecting bass {debugCand} (PC {candidatePc}) — required PC {requestedBassPc}.");
                            }
                        }
                        continue; // Skip to next pitch class
                    }
                }
                else
                {
                    // No inversion specified (root position): ONLY allow root pitch class
                    if (candidatePc != rootPc)
                    {
                        // Skip this candidate entirely - root position must have root in bass
                        if (enableTendencyDebug)
                        {
                            // Generate one candidate to log (for debug purposes)
                            int debugStartOct = (bassMinMidi - candidatePc + 11) / 12;
                            int debugCand = debugStartOct * 12 + candidatePc;
                            if (debugCand < bassMinMidi) debugCand += 12;
                            if (debugCand <= bassMaxMidi + 2)
                            {
                                UnityEngine.Debug.Log($"[Inversion Veto] Rejecting bass {debugCand} (PC {candidatePc}) — root position requires root PC {rootPc}.");
                            }
                        }
                        continue; // Skip to next pitch class
                    }
                }

                // Generate MIDI candidates for this pitch class in bass range
                int startOct = (bassMinMidi - candidatePc + 11) / 12;
                int cand = startOct * 12 + candidatePc;
                if (cand < bassMinMidi) cand += 12;

                while (cand <= bassMaxMidi + 2) // Allow slight out-of-range
                {
                    if (cand < bassMinMidi) { cand += 12; continue; }

                    // Base cost: distance from previous bass
                    float baseCost = Math.Abs(cand - prevBass);

                    // Inversion preference adjustments (now only for bonus, not penalty since wrong PCs are filtered)
                    float prefAdjust = 0f;
                    
                    if (inversionExplicitlySpecified)
                    {
                        // Explicit inversion: candidate already matches requestedBassPc (filtered above)
                        // No adjustment needed - all candidates here are correct
                        prefAdjust = 0f;
                    }
                    else
                    {
                        // No inversion specified: candidate already matches rootPc (filtered above)
                        // Give bonus for root position
                        prefAdjust = -4.0f; // Strong bonus for root position
                    }

                    float totalCost = baseCost + prefAdjust;

                    // Determine degree type for debug logging
                    string degreeType;
                    if (candidatePc == rootPc)
                        degreeType = "Root";
                    else if (candidatePc == thirdPc)
                        degreeType = "3rd";
                    else if (candidatePc == fifthPc)
                        degreeType = "5th";
                    else if (hasSeventh && candidatePc == seventhPc)
                        degreeType = "7th";
                    else
                        degreeType = "Other";

                    // Track for debug
                    candidateMidis.Add(cand);
                    candidateDegrees.Add(degreeType);
                    candidateBaseCosts.Add(baseCost);
                    candidateTotalCosts.Add(totalCost);

                    // Track best overall candidate
                    if (totalCost < bestCost)
                    {
                        bestCandidate = cand;
                        bestCost = totalCost;
                    }

                    // Track root candidate for safeguard rule
                    if (candidatePc == rootPc && baseCost <= 7.0f)
                    {
                        hasRootCandidateWithin7 = true;
                        if (totalCost < bestRootTotalCost)
                        {
                            bestRootMidi = cand;
                            bestRootTotalCost = totalCost;
                        }
                    }

                    cand += 12;
                }
            }

            // Apply decisive safeguards
            int chosenMidi;
            if (inversionExplicitlySpecified)
            {
                // When inversion is explicitly specified, find the best candidate matching the requested bass pitch class
                // and force it (similar to root safeguard for implicit inversions)
                int bestRequestedMidi = -1;
                float bestRequestedCost = float.MaxValue;
                
                // Find the best candidate with the requested bass pitch class
                for (int i = 0; i < candidateMidis.Count; i++)
                {
                    int candidateMidi = candidateMidis[i];
                    int candidatePc = candidateMidi % 12;
                    if (candidatePc < 0) candidatePc += 12;
                    
                    if (candidatePc == requestedBassPc)
                    {
                        float candidateCost = candidateTotalCosts[i];
                        if (candidateCost < bestRequestedCost)
                        {
                            bestRequestedCost = candidateCost;
                            bestRequestedMidi = candidateMidi;
                        }
                    }
                }
                
                if (bestRequestedMidi >= 0)
                {
                    // FORCE the bass to the requested inversion bass pitch class
                    chosenMidi = bestRequestedMidi;
                }
                else
                {
                    // Fallback: use best overall candidate (shouldn't happen if candidates are generated correctly)
                    chosenMidi = bestCandidate;
                }
            }
            else if (hasRootCandidateWithin7)
            {
                // FORCE the bass to the best root candidate within 7 semitones when no inversion specified
                chosenMidi = bestRootMidi;
            }
            else
            {
                // Use the normal "best overall" candidate
                chosenMidi = bestCandidate;
            }

            // Fallback if no candidate found
            if (chosenMidi < 0)
            {
                chosenMidi = FindNearestBassNote(prevBass, requestedBassPc, bassOctave);
            }

            // Final validation: ensure chosen bass has correct pitch class (safety assert)
            int chosenPc = (chosenMidi % 12 + 12) % 12;
            if (chosenPc != requestedBassPc)
            {
                // This should never happen after filtering, but correct it if it does
                int oldMidi = chosenMidi;
                chosenMidi = FindNearestBassNote(prevBass, requestedBassPc, bassOctave);
                if (enableTendencyDebug)
                {
                    UnityEngine.Debug.LogWarning($"[Inversion Fix] Bass {oldMidi} (PC {chosenPc}) corrected to {chosenMidi} (required PC {requestedBassPc}).");
                }
            }

            // Unconditional debug logging when tendency debug is on
            if (enableTendencyDebug)
            {
                UnityEngine.Debug.Log($"[Bass Debug] Step {stepIndex}: prevBass={prevBass}, inversionExplicit={inversionExplicitlySpecified}, " +
                    $"hasRootCandidateWithin7={hasRootCandidateWithin7}, chosenBass={chosenMidi}");
                
                // Log all candidates evaluated
                for (int i = 0; i < candidateMidis.Count; i++)
                {
                    string marker = (candidateMidis[i] == chosenMidi) ? " [CHOSEN]" : "";
                    UnityEngine.Debug.Log($"[Bass Debug]   Candidate {i}: midi={candidateMidis[i]}, degree={candidateDegrees[i]}, " +
                        $"baseCost={candidateBaseCosts[i]:F1}, totalCost={candidateTotalCosts[i]:F2}{marker}");
                }
                
                if (hasRootCandidateWithin7)
                {
                    UnityEngine.Debug.Log($"[Bass Debug]   RootCandidate: midi={bestRootMidi}, totalCost={bestRootTotalCost:F2}");
                }
            }

            return chosenMidi;
        }

        /// <summary>
        /// Ensures that the voiced chord contains the important chord tones
        /// (root, 3rd, 5th, 7th) at least once, adjusting upper voices if necessary.
        /// Only adjusts upper voices (indices 1..N-1); bass (index 0) remains unchanged.
        /// </summary>
        /// <param name="chordEvent">The chord event being voiced</param>
        /// <param name="voices">Array of MIDI notes (will be modified in-place)</param>
        /// <param name="numVoices">Number of voices</param>
        /// <param name="upperMinMidi">Minimum MIDI for upper voices</param>
        /// <param name="upperMaxMidi">Maximum MIDI for upper voices</param>
        /// <param name="protectSoprano">If true, don't modify the highest voice (soprano)</param>
        /// <param name="lockedResolutionVoices">Array indicating which voices have locked 7th resolutions and should not be modified (null = no locks)</param>
        private static void FixChordToneCoverage(
            ChordEvent chordEvent,
            int[] voices,
            int numVoices,
            int upperMinMidi,
            int upperMaxMidi,
            bool protectSoprano = false,
            bool[] lockedResolutionVoices = null)
        {
            // Get chord tone pitch classes
            var chordTonePcs = GetChordTonePitchClasses(chordEvent);
            if (chordTonePcs.Count < 3)
            {
                return; // Can't fix if we don't have enough chord tones
            }

            int rootPc = chordTonePcs[0];
            int thirdPc = chordTonePcs[1];
            int fifthPc = chordTonePcs[2];
            int seventhPc = chordTonePcs.Count > 3 ? chordTonePcs[3] : -1;

            // Determine required pitch classes based on chord type
            var requiredPcs = new HashSet<int>();
            bool hasSeventh = chordEvent.Recipe.Extension == ChordExtension.Seventh &&
                              chordEvent.Recipe.SeventhQuality != SeventhQuality.None;

            if (hasSeventh)
            {
                // 7th chord: require root, 3rd, 7th (5th is optional - can be dropped if needed)
                // Essential set for 4-part writing on 7th chords: Root + 3rd + 7th
                requiredPcs.Add(rootPc);
                requiredPcs.Add(thirdPc);
                requiredPcs.Add(seventhPc);
                // Note: 5th is NOT required - we prefer to keep 7th over 5th
            }
            else
            {
                // Triad: require root, 3rd, 5th
                requiredPcs.Add(rootPc);
                requiredPcs.Add(thirdPc);
                requiredPcs.Add(fifthPc);
            }

            // Count occurrences of each pitch class in current voicing
            var pcCounts = new Dictionary<int, int>();
            for (int i = 0; i < voices.Length; i++)
            {
                int pc = (voices[i] % 12 + 12) % 12;
                pcCounts[pc] = pcCounts.GetValueOrDefault(pc, 0) + 1;
            }

            // Debug logging for 7th chord coverage enforcement
            if (enableTendencyDebug && hasSeventh)
            {
                var beforePcs = new List<string>();
                for (int i = 0; i < voices.Length; i++)
                {
                    int midi = voices[i];
                    string name = TheoryPitch.GetPitchNameFromMidi(midi, chordEvent.Key);
                    beforePcs.Add(name);
                }
                UnityEngine.Debug.Log($"[ChordCoverage Debug] Step: Chord has 7th, enforcing presence. Before: {string.Join(", ", beforePcs)}");
            }

            // For each required pitch class that is missing, fix it
            // Process 7th first if present (highest priority), then root, then 3rd, then others
            var sortedRequiredPcs = new List<int>(requiredPcs);
            if (hasSeventh && seventhPc >= 0)
            {
                // Prioritize 7th - move it to front of list if it's missing
                if (!pcCounts.ContainsKey(seventhPc) || pcCounts[seventhPc] == 0)
                {
                    sortedRequiredPcs.Remove(seventhPc);
                    sortedRequiredPcs.Insert(0, seventhPc);
                }
            }
            
            foreach (int requiredPc in sortedRequiredPcs)
            {
                if (pcCounts.ContainsKey(requiredPc) && pcCounts[requiredPc] > 0)
                {
                    continue; // Already present
                }

                // Generate candidate MIDI notes for the required pitch class
                var candidates = GenerateCandidatesInRange(new List<int> { requiredPc }, upperMinMidi, upperMaxMidi);
                if (candidates.Count == 0)
                {
                    continue; // No valid candidates in range
                }

                // Find a candidate upper voice to repurpose
                // Prefer voices whose pitch class is duplicated (count > 1)
                int bestVoiceIndex = -1;
                int bestCandidateMidi = -1;
                int bestDistance = int.MaxValue;
                bool foundDuplicated = false;

                // Determine the last voice index we can modify (exclude soprano if melody is locked)
                int maxVoiceIndex = voices.Length - 1;
                if (protectSoprano)
                {
                    maxVoiceIndex = voices.Length - 2; // Exclude soprano (last index)
                }
                
                // First pass: look for duplicated pitch classes (skip locked voices)
                for (int i = 1; i <= maxVoiceIndex; i++)
                {
                    // Skip locked resolution voices (7th resolutions that must be preserved)
                    if (lockedResolutionVoices != null && i < lockedResolutionVoices.Length && lockedResolutionVoices[i])
                    {
                        if (enableTendencyDebug)
                        {
                            UnityEngine.Debug.Log($"[Coverage Debug] Skipping locked resolution voice {i} (voice has hard-locked 7th resolution)");
                        }
                        continue;
                    }
                    
                    int currentPc = (voices[i] % 12 + 12) % 12;
                    if (pcCounts[currentPc] > 1) // Duplicated
                    {
                        foundDuplicated = true;
                        // Find nearest MIDI note with requiredPc near this voice
                        foreach (int candidate in candidates)
                        {
                            int distance = Math.Abs(candidate - voices[i]);
                            if (distance < bestDistance)
                            {
                                bestVoiceIndex = i;
                                bestCandidateMidi = candidate;
                                bestDistance = distance;
                            }
                        }
                    }
                }

                // Second pass: if no duplicated voices, use any upper voice (except soprano if protected)
                if (!foundDuplicated)
                {
                    for (int i = 1; i <= maxVoiceIndex; i++)
                    {
                        // Skip locked resolution voices
                        if (lockedResolutionVoices != null && i < lockedResolutionVoices.Length && lockedResolutionVoices[i])
                        {
                            continue;
                        }
                        
                        foreach (int candidate in candidates)
                        {
                            int distance = Math.Abs(candidate - voices[i]);
                            if (distance < bestDistance)
                            {
                                bestVoiceIndex = i;
                                bestCandidateMidi = candidate;
                                bestDistance = distance;
                            }
                        }
                    }
                }

                // If we found a candidate, replace it
                if (bestVoiceIndex >= 0 && bestCandidateMidi >= 0)
                {
                    int oldPc = (voices[bestVoiceIndex] % 12 + 12) % 12;

                    // Update the voice
                    voices[bestVoiceIndex] = bestCandidateMidi;

                    // Update counts
                    pcCounts[oldPc] = pcCounts[oldPc] - 1;
                    if (pcCounts[oldPc] == 0)
                    {
                        pcCounts.Remove(oldPc);
                    }
                    pcCounts[requiredPc] = pcCounts.GetValueOrDefault(requiredPc, 0) + 1;
                }
            }
            
            // Debug logging after coverage fixes
            if (enableTendencyDebug && hasSeventh)
            {
                var afterPcs = new List<string>();
                for (int i = 0; i < voices.Length; i++)
                {
                    int midi = voices[i];
                    string name = TheoryPitch.GetPitchNameFromMidi(midi, chordEvent.Key);
                    afterPcs.Add(name);
                }
                UnityEngine.Debug.Log($"[ChordCoverage Debug] Step: Chord has 7th, enforcing presence. After: {string.Join(", ", afterPcs)}");
            }

            // Re-sort upper voices (indices 1..N-1) to maintain low→high order
            if (voices.Length > 2)
            {
                var upperVoices = new List<int>();
                for (int i = 1; i < voices.Length; i++)
                {
                    upperVoices.Add(voices[i]);
                }
                upperVoices.Sort();
                for (int i = 0; i < upperVoices.Count; i++)
                {
                    voices[i + 1] = upperVoices[i];
                }
            }
        }

        /// <summary>
        /// Hard spacing rules for SATB voicing.
        /// Returns true if the spacing is *invalid* and the candidate should be vetoed.
        /// </summary>
        /// <param name="bass">MIDI note of bass voice</param>
        /// <param name="tenor">MIDI note of tenor voice</param>
        /// <param name="alto">MIDI note of alto voice</param>
        /// <param name="soprano">MIDI note of soprano voice</param>
        /// <returns>True if spacing violates hard rules, false if spacing is acceptable</returns>
        private static bool ViolatesHardSpacing(int bass, int tenor, int alto, int soprano)
        {
            // Soprano–Alto must not exceed an octave
            if (soprano - alto > MaxSopranoAltoInterval)
                return true;
            
            // Alto–Tenor must not exceed an octave
            if (alto - tenor > MaxAltoTenorInterval)
                return true;
            
            // Tenor–Bass may be wider, but > 24 semitones (two octaves) is considered invalid
            if (tenor - bass > MaxTenorBassInterval)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Computes spacing penalty for a complete SATB voicing.
        /// Returns a penalty value (higher = worse spacing).
        /// No mutation, no sorting — just a number.
        /// </summary>
        private static float GetSpacingPenalty(int bass, int tenor, int alto, int soprano)
        {
            float penalty = 0f;

            int sa = soprano - alto;
            int at = alto - tenor;
            int tb = tenor - bass;

            // Hard-ish caps: >12 between S-A or A-T is strongly discouraged.
            if (sa > 12) penalty += SpacingLargePenalty;
            if (at > 12) penalty += SpacingLargePenalty;

            // Very wide tenor-bass gets a large penalty too.
            if (tb > 24) penalty += SpacingLargePenalty;

            // Preferred range: we *prefer* ≤7 between S-A and A-T.
            if (sa > 7) penalty += SpacingPreferredPenalty;
            if (at > 7) penalty += SpacingPreferredPenalty;

            // Prefer tenor not drifting more than an octave above bass.
            if (tb > 12) penalty += SpacingBassTenorPenalty;

            return penalty;
        }

        /// <summary>
        /// Validates that voices are in correct SATB order: Bass ≤ Tenor ≤ Alto ≤ Soprano.
        /// Returns true if valid, false if crossing detected.
        /// </summary>
        /// <param name="midi">Array of MIDI notes for voices [Bass, Tenor, Alto, Soprano]</param>
        /// <param name="error">Output error message if crossing detected</param>
        /// <returns>True if voices are in correct order, false if crossing detected</returns>
        private static bool ValidateVoiceOrder(int[] midi, out string error)
        {
            error = null;
            if (midi == null || midi.Length < 4) return true;
            
            for (int i = 0; i < 3; i++)
            {
                if (midi[i] > midi[i + 1])
                {
                    string v1 = (i == 0) ? "Bass" : (i == 1) ? "Tenor" : "Alto";
                    string v2 = (i + 1 == 1) ? "Tenor" : (i + 1 == 2) ? "Alto" : "Soprano";
                    error = $"{v1} ({midi[i]}) > {v2} ({midi[i + 1]})";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if a candidate upper voice would cause voice crossing with already-selected voices.
        /// Returns true if crossing would occur, false if safe.
        /// </summary>
        /// <param name="candidateMidi">MIDI note of the candidate upper voice</param>
        /// <param name="bassMidi">MIDI note of the bass voice</param>
        /// <param name="selectedUpperVoices">List of already-selected upper voices (in ascending order)</param>
        /// <param name="error">Output error message if crossing detected</param>
        /// <returns>True if crossing would occur, false if safe</returns>
        private static bool WouldCauseVoiceCrossing(int candidateMidi, int bassMidi, List<int> selectedUpperVoices, out string error)
        {
            error = null;
            
            // Check: candidate must be > bass (Bass ≤ Tenor)
            if (candidateMidi <= bassMidi)
            {
                error = $"Bass ({bassMidi}) >= candidate ({candidateMidi})";
                return true;
            }
            
            // Check: candidate must be > all previously selected upper voices
            for (int i = 0; i < selectedUpperVoices.Count; i++)
            {
                if (candidateMidi <= selectedUpperVoices[i])
                {
                    string voiceName = (i == 0) ? "Tenor" : (i == 1) ? "Alto" : $"Voice{i+1}";
                    error = $"{voiceName} ({selectedUpperVoices[i]}) >= candidate ({candidateMidi})";
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Helper method to place a pitch class in the mid register (around MIDI 60/C4)
        /// while ensuring it fits within the specified range.
        /// </summary>
        /// <param name="pitchClass">Pitch class (0-11)</param>
        /// <param name="minMidi">Minimum allowed MIDI note</param>
        /// <param name="maxMidi">Maximum allowed MIDI note</param>
        /// <returns>MIDI note number in the appropriate octave</returns>
        private static int PlaceInMidRegister(int pitchClass, int minMidi, int maxMidi)
        {
            // Start with octave 4 (MIDI 60 = C4)
            int octave = 4;
            int midi = (octave + 1) * 12 + pitchClass;

            // Adjust octave if needed to fit in range
            while (midi < minMidi)
            {
                midi += 12;
            }
            while (midi > maxMidi)
            {
                midi -= 12;
            }

            // Final clamp to ensure we're in range (shouldn't be needed, but be safe)
            if (midi < minMidi)
            {
                midi = minMidi;
            }
            if (midi > maxMidi)
            {
                midi = maxMidi;
            }

            return midi;
        }

        // ============================================================================
        // TONAL TENDENCY RULES (Soft Preferences for Voice-Leading)
        // ============================================================================
        // These rules add gentle biases toward classical voice-leading patterns:
        // 1. Chord 7ths prefer to resolve down by step
        // 2. Global leading tone (degree 7) prefers to resolve up to tonic (softly)
        // 3. Local leading tone (3rd of secondary dominants) prefers to resolve up to target root
        // These are SOFT preferences that bias but don't override existing logic.
        // ============================================================================

        /// <summary>
        /// Information about a voice's tendency characteristics for applying soft voice-leading rules.
        /// </summary>
        private struct VoiceTendencyInfo
        {
            public int midiNote;
            public bool isChordSeventh;
            public bool isChordThird;
            public bool isGlobalLeadingTone;
            public bool isLocalLeadingTone; // e.g., 3rd of secondary dominant
            public int scaleDegree;         // 1-7 in current key, or 0 if unknown/non-diatonic
            public int localTargetRootPc;   // Pitch class of the target root for local leading tone (-1 if not applicable)
        }

        /// <summary>
        /// Analyzes voice tendencies for a given MIDI note in the context of a chord and key.
        /// </summary>
        private static VoiceTendencyInfo AnalyzeVoiceTendencies(
            int midiNote,
            TheoryKey key,
            ChordRecipe chord,
            ChordFunctionProfile analysis)
        {
            var info = new VoiceTendencyInfo
            {
                midiNote = midiNote,
                isChordSeventh = false,
                isChordThird = false,
                isGlobalLeadingTone = false,
                isLocalLeadingTone = false,
                scaleDegree = 0,
                localTargetRootPc = -1
            };

            int notePc = (midiNote % 12 + 12) % 12;

            // Calculate chord root and intervals
            int rootPc = TheoryScale.GetDegreePitchClass(key, chord.Degree);
            if (rootPc < 0) rootPc = 0;
            rootPc = (rootPc + chord.RootSemitoneOffset) % 12;
            if (rootPc < 0) rootPc += 12;

            // Calculate third and seventh intervals based on chord quality
            int thirdInterval = 0;
            int seventhInterval = 0;
            bool hasSeventh = chord.Extension == ChordExtension.Seventh && 
                             chord.SeventhQuality != SeventhQuality.None;

            switch (chord.Quality)
            {
                case ChordQuality.Major:
                    thirdInterval = 4;
                    break;
                case ChordQuality.Minor:
                    thirdInterval = 3;
                    break;
                case ChordQuality.Diminished:
                    thirdInterval = 3;
                    break;
                case ChordQuality.Augmented:
                    thirdInterval = 4;
                    break;
            }

            if (hasSeventh)
            {
                switch (chord.SeventhQuality)
                {
                    case SeventhQuality.Major7:
                        seventhInterval = 11;
                        break;
                    case SeventhQuality.Minor7:
                    case SeventhQuality.Dominant7:
                    case SeventhQuality.HalfDiminished7:
                        seventhInterval = 10;
                        break;
                    case SeventhQuality.Diminished7:
                        seventhInterval = 9;
                        break;
                }
            }

            int thirdPc = (rootPc + thirdInterval) % 12;
            int seventhPc = hasSeventh ? (rootPc + seventhInterval) % 12 : -1;

            // Check if this note is the chord's 7th
            if (seventhPc >= 0 && notePc == seventhPc)
            {
                info.isChordSeventh = true;
            }

            // Check if this note is the chord's 3rd
            if (notePc == thirdPc)
            {
                info.isChordThird = true;
            }

            // Check if this is a global leading tone (degree 7 of the key)
            var scalePcs = TheoryScale.GetDiatonicPitchClasses(key);
            if (scalePcs != null && scalePcs.Length >= 7)
            {
                int leadingTonePc = scalePcs[6]; // Degree 7 (0-indexed is 6)
                if (notePc == leadingTonePc)
                {
                    info.isGlobalLeadingTone = true;
                    info.scaleDegree = 7;
                }
                else
                {
                    // Try to find scale degree
                    for (int i = 0; i < 7; i++)
                    {
                        if (scalePcs[i] == notePc)
                        {
                            info.scaleDegree = i + 1;
                            break;
                        }
                    }
                }
            }

            // Check if this is a local leading tone (3rd of a secondary dominant)
            if (info.isChordThird && 
                analysis.FunctionTag == ChordFunctionTag.SecondaryDominant &&
                analysis.SecondaryTargetDegree.HasValue)
            {
                int targetDegree = analysis.SecondaryTargetDegree.Value;
                
                // Calculate target root pitch class
                int targetRootPc = TheoryScale.GetDegreePitchClass(key, targetDegree);
                if (targetRootPc >= 0)
                {
                    // Normalize pitch class
                    targetRootPc = (targetRootPc + 12) % 12;
                    info.localTargetRootPc = targetRootPc;
                    info.isLocalLeadingTone = true;
                }
            }

            return info;
        }

        /// <summary>
        /// Computes a soft cost adjustment based on tonal tendency rules.
        /// Returns a float adjustment (negative = bonus, positive = penalty).
        /// Magnitudes are kept small (-2 to +2 range) to bias but not override base distances.
        /// </summary>
        /// <param name="from">Tendency info for the voice we're moving from (in previous chord)</param>
        /// <param name="toMidiNote">Candidate MIDI note we're evaluating (in next chord)</param>
        /// <param name="key">The key context</param>
        /// <param name="currentChord">The chord the voice is coming from (previous chord in progression)</param>
        /// <param name="nextChord">The chord the voice is going to (current/next chord in progression)</param>
        /// <param name="currentAnalysis">Analysis of the previous chord</param>
        /// <param name="nextAnalysis">Analysis of the current/next chord</param>
        /// <param name="isSoprano">True if this is the soprano voice (melody-locked)</param>
        /// <param name="nextMelodyMidi">Optional melody MIDI for the next chord (for soprano rule)</param>
        /// <param name="voiceMinMidi">Optional minimum MIDI for this voice's range (for hard 7th resolution constraint)</param>
        /// <param name="voiceMaxMidi">Optional maximum MIDI for this voice's range (for hard 7th resolution constraint)</param>
        private static float ComputeTendencyCostAdjustment(
            VoiceTendencyInfo from,
            int toMidiNote,
            TheoryKey key,
            ChordRecipe currentChord,
            ChordRecipe nextChord,
            ChordFunctionProfile currentAnalysis,
            ChordFunctionProfile nextAnalysis,
            bool isSoprano,
            int? nextMelodyMidi,
            int voiceMinMidi = -1,
            int voiceMaxMidi = -1)
        {
            // Unconditional debug log when tendency debug is enabled (to confirm function is being called)
            if (enableTendencyDebug)
            {
                string melodyStr = nextMelodyMidi.HasValue ? nextMelodyMidi.Value.ToString() : "null";
                UnityEngine.Debug.Log($"[Tendency Debug] Check voice tendency: prev={from.midiNote}, cand={toMidiNote}, " +
                    $"is7th={from.isChordSeventh}, isGlobalLT={from.isGlobalLeadingTone}, isLocalLT={from.isLocalLeadingTone}, " +
                    $"isSoprano={isSoprano}, melodyNext={melodyStr}");
            }
            
            float adjustment = 0f;
            int semitoneDistance = Math.Abs(toMidiNote - from.midiNote);
            int direction = Math.Sign(toMidiNote - from.midiNote);

            // Get next chord tone pitch classes
            var nextChordTonePcs = GetChordTonePitchClasses(new ChordEvent
            {
                Key = key,
                Recipe = nextChord
            });
            int toPc = (toMidiNote % 12 + 12) % 12;
            bool toIsChordTone = nextChordTonePcs.Contains(toPc);

            // ========================================================================
            // RULE A: Chord 7ths prefer to resolve down by step
            // ========================================================================
            if (from.isChordSeventh && toIsChordTone)
            {
                int prev = from.midiNote;
                int cand = toMidiNote;
                int semitoneDelta = cand - prev;  // >0 up, <0 down
                int absDelta = Math.Abs(semitoneDelta);
                
                int prevPc = (prev % 12 + 12) % 12;
                int candPc = (cand % 12 + 12) % 12;
                
                // Case A2: Melody already has the resolution tone
                bool melodyOnResolution = false;
                if (!isSoprano && nextMelodyMidi.HasValue && nextMelodyMidi.Value >= 0)
                {
                    int melodyMidi = nextMelodyMidi.Value;
                    int melodyPc = (melodyMidi % 12 + 12) % 12;
                    
                    // A simple "step down" test in pitch-class space:
                    // previous 7th -> resolution should be 1 or 2 semitones below
                    int pcDown1 = (prevPc + 11) % 12;   // -1 semitone
                    int pcDown2 = (prevPc + 10) % 12;   // -2 semitones
                    
                    if (melodyPc == pcDown1 || melodyPc == pcDown2)
                    {
                        melodyOnResolution = true;
                    }
                }
                
                // Apply special-case bonuses/penalties when melodyOnResolution is true
                if (melodyOnResolution)
                {
                    int melodyMidi = nextMelodyMidi.Value;
                    int melodyPc = (melodyMidi % 12 + 12) % 12;
                    
                    bool resolvesDownToMelody = (cand < prev) &&
                                                (candPc == melodyPc) &&
                                                (absDelta >= 1 && absDelta <= 3);
                    
                    if (resolvesDownToMelody)
                    {
                        adjustment += SeventhResolutionWithMelodyDoubleBonus;
                        
                        if (enableTendencyDebug)
                        {
                            UnityEngine.Debug.Log($"[Tendency Debug] RuleA-MelodyDouble GOOD: prev={prev} ({prevPc}), cand={cand} ({candPc}), " +
                                $"melodyNext={melodyMidi} ({melodyPc}) → bonus {SeventhResolutionWithMelodyDoubleBonus}");
                        }
                    }
                    else
                    {
                        adjustment += SeventhResolutionWithMelodyDoublePenalty;
                        
                        if (enableTendencyDebug)
                        {
                            UnityEngine.Debug.Log($"[Tendency Debug] RuleA-MelodyDouble BAD: prev={prev} ({prevPc}), cand={cand} ({candPc}), " +
                                $"melodyNext={melodyMidi} ({melodyPc}) → penalty {SeventhResolutionWithMelodyDoublePenalty}");
                        }
                    }
                    // Special case takes precedence - skip normal Rule A logic
                }
                else
                {
                    // Case A1: Normal "7th resolves down" (no melody special case)
                    // Check for stepwise downward resolution for chord 7ths
                    // When there's no melody, soprano 7ths should also follow the hard rule
                    bool noMelody = nextMelodyMidi == null;
                    bool shouldApplyHardRule = noMelody || !isSoprano;
                    
                    if (shouldApplyHardRule)
                    {
                        // Determine valid resolution pitch classes (1-2 semitones down)
                        int resolutionPc1 = (prevPc + 11) % 12;   // -1 semitone
                        int resolutionPc2 = (prevPc + 10) % 12;   // -2 semitones
                        
                        // Check if the next chord contains either resolution pitch class
                        bool hasResolutionTone = nextChordTonePcs.Contains(resolutionPc1) || nextChordTonePcs.Contains(resolutionPc2);
                        
                        // Helper to check if a pitch class is a valid 7th resolution
                        bool isValidSeventhResolution(int fromPc, int toPc)
                        {
                            int pcDown1 = (fromPc + 11) % 12;
                            int pcDown2 = (fromPc + 10) % 12;
                            return (toPc == pcDown1 || toPc == pcDown2);
                        }
                        
                        // HARD CONSTRAINT: If voice range is provided and a valid downward resolution exists in range,
                        // enforce it as a hard constraint (only allow the resolution, veto everything else)
                        bool hasValidDownwardResolution = false;
                        int resolutionMidi = -1;
                        
                        if (hasResolutionTone && voiceMinMidi >= 0 && voiceMaxMidi >= 0)
                        {
                            // Try to find a valid downward resolution within the voice's range
                            // Try resolutionPc1 first (more common -1 semitone resolution)
                            if (nextChordTonePcs.Contains(resolutionPc1))
                            {
                                resolutionMidi = FindDownwardSeventhResolution(prev, resolutionPc1, voiceMinMidi, voiceMaxMidi);
                            }
                            
                            // Try resolutionPc2 if resolutionPc1 didn't yield a result
                            if (resolutionMidi < 0 && nextChordTonePcs.Contains(resolutionPc2))
                            {
                                resolutionMidi = FindDownwardSeventhResolution(prev, resolutionPc2, voiceMinMidi, voiceMaxMidi);
                            }
                            
                            hasValidDownwardResolution = (resolutionMidi >= 0);
                        }
                        
                        if (hasValidDownwardResolution)
                        {
                            // HARD CONSTRAINT: Only allow the exact resolution MIDI, veto everything else
                            if (cand == resolutionMidi)
                            {
                                // Good: apply strong bonus
                                adjustment += SeventhResolutionDownStepBonusHard;
                                
                                if (enableTendencyDebug)
                                {
                                    string prevName = TheoryPitch.GetPitchNameFromMidi(prev, key);
                                    string candName = TheoryPitch.GetPitchNameFromMidi(cand, key);
                                    string resolutionName = TheoryPitch.GetPitchNameFromMidi(resolutionMidi, key);
                                    string voiceLabel = isSoprano ? "Soprano" : "Inner";
                                    UnityEngine.Debug.Log($"[Tendency Debug] RuleA-HARD GOOD (7th) [{voiceLabel}]: prev={prevName}({prev}) -> {candName}({cand}), resolution={resolutionName}({resolutionMidi}), bonus={SeventhResolutionDownStepBonusHard}");
                                }
                            }
                            else
                            {
                                // BAD: hard veto unless this candidate is the only allowed one due to range
                                adjustment += SeventhResolutionHardPenalty;
                                
                                if (enableTendencyDebug)
                                {
                                    string prevName = TheoryPitch.GetPitchNameFromMidi(prev, key);
                                    string candName = TheoryPitch.GetPitchNameFromMidi(cand, key);
                                    string resolutionName = TheoryPitch.GetPitchNameFromMidi(resolutionMidi, key);
                                    string voiceLabel = isSoprano ? "Soprano" : "Inner";
                                    UnityEngine.Debug.Log($"[Tendency Debug] RuleA-HARD VETO (7th) [{voiceLabel}]: prev={prevName}({prev}) -> {candName}({cand}), resolution={resolutionName}({resolutionMidi}), penalty={SeventhResolutionHardPenalty}");
                                }
                            }
                            
                            // When hard rule triggers, SKIP the rest of Rule A's normal logic
                            // Return immediately to avoid applying additional "Normal" bonuses/penalties
                            return adjustment;
                        }
                        else if (hasResolutionTone)
                        {
                            // Soft constraint: valid resolution exists but range not provided or resolution out of range
                            // Use existing "Normal" Rule A logic
                            // Check if this candidate is a stepwise downward resolution (1-2 semitones down)
                            bool isStepwiseDownwardResolution = (cand < prev) && 
                                                               (absDelta >= 1 && absDelta <= 2) && 
                                                               isValidSeventhResolution(prevPc, candPc);
                            
                            if (isStepwiseDownwardResolution)
                            {
                                // Strong bonus for correct stepwise downward resolution
                                adjustment += SeventhResolutionDownStepBonusNormal;
                                
                                if (enableTendencyDebug)
                                {
                                    string prevName = TheoryPitch.GetPitchNameFromMidi(prev, key);
                                    string candName = TheoryPitch.GetPitchNameFromMidi(cand, key);
                                    UnityEngine.Debug.Log($"[Tendency Debug] RuleA-Normal GOOD: prev={prevName}({prev}) -> {candName}({cand}), delta={semitoneDelta}, bonus={SeventhResolutionDownStepBonusNormal}");
                                }
                            }
                            else if (cand >= prev)
                            {
                                // Strong penalty for upward or hold motion when a valid down step resolution exists
                                adjustment += SeventhResolutionAvoidPenaltyNormal;
                                
                                if (enableTendencyDebug)
                                {
                                    string prevName = TheoryPitch.GetPitchNameFromMidi(prev, key);
                                    string candName = TheoryPitch.GetPitchNameFromMidi(cand, key);
                                    UnityEngine.Debug.Log($"[Tendency Debug] RuleA-Normal AVOID: prev={prevName}({prev}) -> {candName}({cand}), delta={semitoneDelta}, penalty={SeventhResolutionAvoidPenaltyNormal}");
                                }
                            }
                            // Large downward leaps (absDelta > 2) are left neutral or mild penalty
                            // so the injected step-down resolution will always win when available
                        }
                        else
                        {
                            // No step-down resolution available - use milder adjustments
                            if (semitoneDelta < 0 && absDelta >= 1 && absDelta <= 3)
                            {
                                adjustment += SeventhResolutionDownStepBonus;
                            }
                            // Check if holding the 7th while a good resolution exists
                            else if (cand == prev)
                            {
                                // Check if a good step-down resolution exists in the next chord
                                bool hasGoodResolution = false;
                                
                                foreach (int candidatePc in nextChordTonePcs)
                                {
                                    int pcDiff = (candidatePc - prevPc + 12) % 12;
                                    // 9-11 semitones up in pitch class = 1-3 semitones down
                                    if (pcDiff >= 9 && pcDiff <= 11)
                                    {
                                        hasGoodResolution = true;
                                        break;
                                    }
                                }
                                
                                if (hasGoodResolution)
                                {
                                    adjustment += SeventhResolutionHoldPenalty;
                                }
                            }
                            // Small penalty for large upward leaps
                            else if (semitoneDelta > 0 && absDelta >= 5)
                            {
                                adjustment += SeventhResolutionLargeLeapPenalty;
                            }
                            
                            if (enableTendencyDebug && adjustment != 0f)
                            {
                                UnityEngine.Debug.Log($"[Tendency Debug] RuleA-Normal: prev={prev}, cand={cand}, delta={semitoneDelta}, adjust={adjustment}");
                            }
                        }
                    }
                    else
                    {
                        // Soprano voice - use milder adjustments (soprano is melody-locked, so 7th resolution is less critical)
                        if (semitoneDelta < 0 && absDelta >= 1 && absDelta <= 3)
                        {
                            adjustment += SeventhResolutionDownStepBonus;
                        }
                        // Check if holding the 7th while a good resolution exists
                        else if (cand == prev)
                        {
                            // Check if a good step-down resolution exists in the next chord
                            bool hasGoodResolution = false;
                            
                            foreach (int candidatePc in nextChordTonePcs)
                            {
                                int pcDiff = (candidatePc - prevPc + 12) % 12;
                                // 9-11 semitones up in pitch class = 1-3 semitones down
                                if (pcDiff >= 9 && pcDiff <= 11)
                                {
                                    hasGoodResolution = true;
                                    break;
                                }
                            }
                            
                            if (hasGoodResolution)
                            {
                                adjustment += SeventhResolutionHoldPenalty;
                            }
                        }
                        // Small penalty for large upward leaps
                        else if (semitoneDelta > 0 && absDelta >= 5)
                        {
                            adjustment += SeventhResolutionLargeLeapPenalty;
                        }
                        
                        if (enableTendencyDebug && adjustment != 0f)
                        {
                            UnityEngine.Debug.Log($"[Tendency Debug] RuleA-Normal: prev={prev}, cand={cand}, delta={semitoneDelta}, adjust={adjustment}");
                        }
                    }
                }
            }

            // ========================================================================
            // RULE B: Global leading tone prefers to resolve up to tonic (softly)
            // ========================================================================
            if (from.isGlobalLeadingTone && toIsChordTone)
            {
                // Check if next chord needs its 7th (to avoid weakening leading tone when 7th coverage is critical)
                bool nextChordHasSeventh = nextChord.Extension == ChordExtension.Seventh &&
                                          nextChord.SeventhQuality != SeventhQuality.None;
                
                bool nextChordSeventhMissing = false;
                if (nextChordHasSeventh)
                {
                    // Calculate what the 7th pitch class should be
                    int nextRootPc = TheoryScale.GetDegreePitchClass(key, nextChord.Degree);
                    if (nextRootPc < 0) nextRootPc = 0;
                    nextRootPc = (nextRootPc + nextChord.RootSemitoneOffset) % 12;
                    if (nextRootPc < 0) nextRootPc += 12;
                    
                    // Determine 7th interval based on chord quality
                    int nextSeventhInterval = 10; // Default
                    switch (nextChord.SeventhQuality)
                    {
                        case SeventhQuality.Major7:
                            nextSeventhInterval = 11;
                            break;
                        case SeventhQuality.Minor7:
                        case SeventhQuality.Dominant7:
                        case SeventhQuality.HalfDiminished7:
                            nextSeventhInterval = 10;
                            break;
                        case SeventhQuality.Diminished7:
                            nextSeventhInterval = 9;
                            break;
                    }
                    int nextSeventhPc = (nextRootPc + nextSeventhInterval) % 12;
                    
                    // Check if the next chord's 7th is missing in the current candidate set
                    // (We can't check all voices here, but we can check if the candidate we're evaluating is NOT the 7th)
                    nextChordSeventhMissing = (toPc != nextSeventhPc);
                }
                
                // Check if next chord contains tonic (degree 1)
                var scalePcs = TheoryScale.GetDiatonicPitchClasses(key);
                if (scalePcs != null && scalePcs.Length >= 7)
                {
                    int tonicPc = scalePcs[0]; // Degree 1
                    bool nextChordContainsTonic = nextChordTonePcs.Contains(tonicPc);
                    
                    if (nextChordContainsTonic && toPc == tonicPc)
                    {
                        // Moving to tonic by small upward step
                        if (direction > 0 && semitoneDistance >= 1 && semitoneDistance <= 2)
                        {
                            float leadingToneBonus = 0f;
                            
                            if (isSoprano)
                            {
                                // For soprano: only apply if melody already moves up to tonic
                                if (nextMelodyMidi.HasValue && nextMelodyMidi.Value == toMidiNote)
                                {
                                    leadingToneBonus = -2.5f; // Stronger bonus when melody already wants this
                                }
                                // Don't fight the melody - if melody goes elsewhere, no adjustment
                            }
                            else
                            {
                                // For inner voices: stronger preference for 7→1
                                leadingToneBonus = -1.25f;
                                
                                // Soften leading-tone bonus if next chord needs its 7th and we're not providing it
                                if (nextChordHasSeventh && nextChordSeventhMissing)
                                {
                                    leadingToneBonus *= LeadingToneSoftenFactor;
                                    
                                    if (enableTendencyDebug)
                                    {
                                        string prevName = TheoryPitch.GetPitchNameFromMidi(from.midiNote, key);
                                        UnityEngine.Debug.Log($"[Tendency Debug] Leading tone softened to preserve 7th coverage: prev={prevName}, nextChord has 7th that needs coverage");
                                    }
                                }
                            }
                            
                            adjustment += leadingToneBonus;
                        }
                    }
                }
            }

            // ========================================================================
            // RULE C: Local leading tone (3rd of secondary dominants) resolves up to target root
            // ========================================================================
            if (from.isLocalLeadingTone && from.localTargetRootPc >= 0)
            {
                int targetRootPc = from.localTargetRootPc; // Already computed in AnalyzeVoiceTendencies
                
                // Check if next chord's root matches the target (accounting for potential chromatic alterations)
                int nextRootPc = TheoryScale.GetDegreePitchClass(key, nextChord.Degree);
                if (nextRootPc >= 0)
                {
                    nextRootPc = (nextRootPc + nextChord.RootSemitoneOffset) % 12;
                    if (nextRootPc < 0) nextRootPc += 12;

                    bool targetIsAvailable = (nextRootPc == targetRootPc);
                    
                    // Check if candidate is resolving to the target root (by pitch class match)
                    // Note: toPc is already computed earlier in the method
                    if (toPc == targetRootPc)
                    {
                        // Moving to the target root by small step (1-3 semitones in either direction)
                        // Use Math.Abs for distance check to recognize both upward and downward resolutions
                        if (semitoneDistance >= 1 && semitoneDistance <= 3)
                        {
                            // Prefer upward movement with stronger bonus, but also reward downward resolution
                            if (direction > 0)
                            {
                                adjustment -= 2.5f; // Strong bonus for resolving up to target
                            }
                            else
                            {
                                adjustment -= 2.0f; // Still good bonus for downward resolution
                            }
                        }
                    }
                    // Penalty for holding the local leading tone when target is available
                    else if (toMidiNote == from.midiNote && targetIsAvailable)
                    {
                        adjustment += 1.5f; // Penalty for holding when resolution available
                    }
                }
            }

            return adjustment;
        }

        /// <summary>
        /// Helper method to log tendency debug information for a chosen voice mapping.
        /// Only logs when enableTendencyDebug is true and the voice has relevant tendencies.
        /// </summary>
        private static void LogTendencyDebugInfo(
            int stepIndex,
            int voiceIndex,
            VoiceTendencyInfo tendencyInfo,
            int fromMidi,
            int chosenMidi,
            TheoryKey key,
            ChordRecipe nextChord,
            ChordFunctionProfile nextAnalysis,
            float baseCost,
            float tendAdjust,
            float totalCost)
        {
            if (!enableTendencyDebug) return;
            
            // Only log if this voice has relevant tendencies
            if (!tendencyInfo.isChordSeventh && !tendencyInfo.isGlobalLeadingTone && !tendencyInfo.isLocalLeadingTone)
                return;

            string fromNote = TheoryPitch.GetPitchNameFromMidi(fromMidi, key);
            string toNote = TheoryPitch.GetPitchNameFromMidi(chosenMidi, key);
            
            var logParts = new System.Text.StringBuilder();
            logParts.Append($"[Tendency Debug] Step {stepIndex}, Voice {voiceIndex}: ");
            logParts.Append($"{fromNote} ({fromMidi}) → {toNote} ({chosenMidi}) | ");
            logParts.Append($"BaseCost={baseCost:F2}, TendAdjust={tendAdjust:F2}, TotalCost={totalCost:F2} | ");
            
            if (tendencyInfo.isChordSeventh)
                logParts.Append("isChordSeventh ");
            if (tendencyInfo.isGlobalLeadingTone)
                logParts.Append("isGlobalLeadingTone ");
            if (tendencyInfo.isLocalLeadingTone)
            {
                logParts.Append($"isLocalLeadingTone (target={TheoryPitch.GetPitchNameFromMidi(tendencyInfo.localTargetRootPc + 60, key)}) ");
            }
            
            UnityEngine.Debug.Log(logParts.ToString());
        }
    }
}


