using System.Collections.Generic;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Settings that control heuristic behavior for harmonization algorithms.
    /// </summary>
    public struct HarmonyHeuristicSettings
    {
        /// <summary>
        /// If true, the harmonization algorithm will prefer starting with a tonic chord (I).
        /// This encourages progressions that begin on the home chord, which is common in tonal music.
        /// </summary>
        public bool PreferTonicStart;

        /// <summary>
        /// If true, the harmonization algorithm will prefer maintaining chord continuity between steps.
        /// This encourages reusing the same chord or moving to closely related chords when possible,
        /// resulting in smoother harmonic progressions.
        /// </summary>
        public bool PreferChordContinuity;

        /// <summary>
        /// If true, the harmonization algorithm will generate detailed reason strings explaining
        /// why each chord was chosen. This is useful for debugging and understanding the algorithm's decisions.
        /// </summary>
        public bool EnableDetailedReasonLogs;
    }

    /// <summary>
    /// Represents a single step in a harmonized progression, containing the melody note,
    /// its analysis, candidate chords, and the chosen harmonization.
    /// </summary>
    public struct HarmonizedChordStep
    {
        /// <summary>
        /// The original melodic event (note with timing and MIDI information).
        /// </summary>
        public MelodyEvent MelodyEvent;

        /// <summary>
        /// The analysis of the melody event in the context of the key, including
        /// scale degree, pitch class, and diatonic status.
        /// </summary>
        public MelodyAnalysis MelodyAnalysis;

        /// <summary>
        /// List of candidate chords that could harmonize this melody note.
        /// Generated based on the melody's scale degree and harmonic rules.
        /// </summary>
        public List<ChordCandidate> Candidates;

        /// <summary>
        /// The chord that was chosen to harmonize this melody note.
        /// This is the result of applying harmonization heuristics to the candidate list.
        /// </summary>
        public ChordCandidate ChosenChord;

        /// <summary>
        /// Human-readable explanation of why this chord was chosen for this melody note.
        /// May include information about heuristics applied, candidate ranking, or other factors.
        /// </summary>
        public string Reason;
    }

    /// <summary>
    /// A single candidate chord for a melody note.
    /// </summary>
    public struct ChordCandidate
    {
        public ChordRecipe Recipe;          // The underlying chord recipe
        public string Roman;                // Roman numeral text
        public string ChordSymbol;          // e.g. "Am7"
        public string Reason;               // Short explanation, e.g. "Melody is chord 3rd"
    }

    /// <summary>
    /// Static helper class for generating chord candidates for melody notes.
    /// </summary>
    public static class TheoryHarmonization
    {
        /// <summary>
        /// Get candidate chords for a single analyzed melody note in a key.
        /// For v1, only supports Ionian and diatonic notes; others return an empty list.
        /// </summary>
        /// <param name="key">The key context</param>
        /// <param name="note">The analyzed melody note</param>
        /// <param name="accidentalHint">Hint about accidental spelling from user input, used for enharmonic disambiguation</param>
        public static List<ChordCandidate> GetChordCandidatesForMelodyNote(
            TheoryKey key,
            MelodyAnalysis note,
            AccidentalHint accidentalHint = AccidentalHint.None)
        {
            var candidates = new List<ChordCandidate>();

            // Handle Ionian major and diatonic notes
            if (key.Mode == ScaleMode.Ionian && note.IsDiatonic)
            {

            // Use note.Degree (1..7) to choose a small set of Roman numerals
            // that typically contain that scale degree in major keys.
            var romans = new List<string>();

            switch (note.Degree)
            {
                case 1:
                    romans.Add("I");
                    romans.Add("vi");
                    break;
                case 2:
                    romans.Add("ii");
                    romans.Add("V");
                    break;
                case 3:
                    romans.Add("I");
                    romans.Add("iii");
                    romans.Add("vi");
                    break;
                case 4:
                    romans.Add("IV");
                    romans.Add("ii");
                    break;
                case 5:
                    romans.Add("V");
                    romans.Add("I");
                    break;
                case 6:
                    romans.Add("vi");
                    romans.Add("IV");
                    break;
                case 7:
                    romans.Add("V");
                    romans.Add("viidim"); // Use "viidim" format that the parser expects
                    break;
                default:
                    return candidates;
            }

            foreach (var roman in romans)
            {
                // Use existing Roman numeral parser to get a ChordRecipe.
                if (TryBuildCandidateFromRoman(key, note, roman, out var candidate))
                {
                    candidates.Add(candidate);
                }
            }
            }

            // Add chromatic candidates for non-diatonic notes
            if (!note.IsDiatonic)
            {
                var chromatic = GetChromaticCandidates(note.Midi, note.PitchClass, key, accidentalHint);
                if (chromatic != null && chromatic.Count > 0)
                {
                    candidates.AddRange(chromatic);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Helper that tries to parse a Roman numeral and build a ChordCandidate.
        /// </summary>
        private static bool TryBuildCandidateFromRoman(
            TheoryKey key,
            MelodyAnalysis note,
            string roman,
            out ChordCandidate candidate)
        {
            candidate = default;

            // Use existing parser from TheoryChord
            if (!TheoryChord.TryParseRomanNumeral(key, roman, out var recipe))
                return false;

            // Build chord MIDI in some reference octave and check that the
            // melody pitch class is actually present in the chord (mod 12).
            int[] chordMidi = TheoryChord.BuildChord(key, recipe, 4);
            if (chordMidi == null || chordMidi.Length == 0)
                return false;

            int melodyPc = note.PitchClass;
            bool containsMelodyPc = false;

            foreach (var midi in chordMidi)
            {
                int pc = ((midi % 12) + 12) % 12;
                if (pc == melodyPc)
                {
                    containsMelodyPc = true;
                    break;
                }
            }

            if (!containsMelodyPc)
                return false;

            // Build chord symbol string using existing helpers
            string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                key,
                recipe.Degree,
                recipe.RootSemitoneOffset);
            
            string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootNoteName);
            if (string.IsNullOrEmpty(chordSymbol))
                chordSymbol = roman; // Fallback to Roman numeral if symbol building fails

            string reason = $"Melody degree {note.Degree} is chord tone in {roman}";

            candidate = new ChordCandidate
            {
                Recipe = recipe,
                Roman = roman,
                ChordSymbol = chordSymbol,
                Reason = reason
            };

            return true;
        }

        /// <summary>
        /// Generates chromatic chord candidates for non-diatonic melody notes.
        /// This is a V1 naive strategy for handling chromatic passing tones and altered scale degrees.
        /// More sophisticated chromatic candidate generation may replace this later.
        /// </summary>
        /// <param name="melodyMidi">MIDI note number of the melody note</param>
        /// <param name="pitchClass">Pitch class (0-11) of the melody note (absolute)</param>
        /// <param name="key">The key context</param>
        /// <param name="accidentalHint">Hint about accidental spelling from user input</param>
        /// <returns>List of chromatic chord candidates, or empty list if none found</returns>
        private static List<ChordCandidate> GetChromaticCandidates(
            int melodyMidi,
            int pitchClass,
            TheoryKey key,
            AccidentalHint accidentalHint)
        {
            var candidates = new List<ChordCandidate>();

            // Only support Ionian mode for now
            if (key.Mode != ScaleMode.Ionian)
                return candidates;

            // If no accidental hint (None/Natural), don't generate chromatic candidates.
            // This preserves backward compatibility and respects "only when we have spelling" design.
            if (accidentalHint == AccidentalHint.None || accidentalHint == AccidentalHint.Natural)
                return candidates;

            int tonicPc = key.TonicPitchClass;

            // Compute pitch class relative to key tonic
            // relPc == 0 means same as tonic, 1 means one semitone above, etc.
            int relPc = (pitchClass - tonicPc + 12) % 12;

            // Map chromatic alterations to chord candidates based on relative pitch class and accidental hint.
            // Use accidental hint to disambiguate enharmonic equivalents.
            // Switch on relPc to handle ambiguous cases based on accidental spelling.

            switch (relPc)
            {
                case 1: // C#/Db relative to tonic
                    if (accidentalHint == AccidentalHint.Flat)
                    {
                        // Db (b2) → Neapolitan (bII)
                        var recipe = new ChordRecipe(2, ChordQuality.Major, ChordExtension.None, -1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "bII",
                            "[CHROMATIC] Db (b2) → bII (Neapolitan)", candidates);
                    }
                    else if (accidentalHint == AccidentalHint.Sharp)
                    {
                        // C# (#1) → V/ii (A major/A7) - C# is the 3rd of A
                        // V/ii means dominant of ii (D minor), which is A major
                        var recipe = new ChordRecipe(6, ChordQuality.Major, ChordExtension.None, 0, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "V/ii",
                            "[CHROMATIC] C# (#1) → V/ii (secondary dominant of ii)", candidates);
                        
                        // Also try A7
                        var recipe7 = new ChordRecipe(6, ChordQuality.Major, ChordExtension.Seventh, 0, SeventhQuality.Dominant7);
                        TryBuildAndAddChromaticCandidate(key, recipe7, pitchClass, melodyMidi, "V7/ii",
                            "[CHROMATIC] C# (#1) → V7/ii (secondary dominant of ii)", candidates);
                    }
                    break;

                case 3: // D#/Eb relative to tonic
                    if (accidentalHint == AccidentalHint.Flat)
                    {
                        // Eb (b3) → bIII
                        var recipe = new ChordRecipe(3, ChordQuality.Major, ChordExtension.None, -1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "bIII",
                            "[CHROMATIC] Eb (b3) → bIII (mode mixture)", candidates);
                    }
                    else if (accidentalHint == AccidentalHint.Sharp)
                    {
                        // D# (#2) → V/V (secondary dominant of V)
                        TryAddChromaticDominant7th(key, pitchClass, melodyMidi, 4, "V/V",
                            "[CHROMATIC] D# (#2) → V/V (secondary dominant of V)", candidates);
                    }
                    break;

                case 6: // F#/Gb relative to tonic
                    if (accidentalHint == AccidentalHint.Sharp)
                    {
                        // F# (#4) → V/V
                        TryAddChromaticDominant7th(key, pitchClass, melodyMidi, 7, "V/V",
                            "[CHROMATIC] F# (#4) → V/V (secondary dominant of V)", candidates);
                    }
                    else if (accidentalHint == AccidentalHint.Flat)
                    {
                        // Gb (b5) → bV (Gb major) - Gb is the lowered 5th degree
                        var recipe = new ChordRecipe(5, ChordQuality.Major, ChordExtension.None, -1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "bV",
                            "[CHROMATIC] Gb (b5) → bV (borrowed chord)", candidates);
                    }
                    break;

                case 8: // G#/Ab relative to tonic
                    if (accidentalHint == AccidentalHint.Flat)
                    {
                        // Ab (b6) → bVI
                        var recipe = new ChordRecipe(6, ChordQuality.Major, ChordExtension.None, -1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "bVI",
                            "[CHROMATIC] Ab (b6) → bVI (borrowed chord)", candidates);
                    }
                    else if (accidentalHint == AccidentalHint.Sharp)
                    {
                        // G# (#5) → V/vi (E major/E7) - G# is the 3rd of E
                        var recipe = new ChordRecipe(3, ChordQuality.Major, ChordExtension.None, 0, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "V/vi",
                            "[CHROMATIC] G# (#5) → V/vi (secondary dominant of vi)", candidates);
                        
                        // Also try E7
                        var recipe7 = new ChordRecipe(3, ChordQuality.Major, ChordExtension.Seventh, 0, SeventhQuality.Dominant7);
                        TryBuildAndAddChromaticCandidate(key, recipe7, pitchClass, melodyMidi, "V7/vi",
                            "[CHROMATIC] G# (#5) → V7/vi (secondary dominant of vi)", candidates);
                    }
                    break;

                case 10: // A#/Bb relative to tonic
                    if (accidentalHint == AccidentalHint.Flat)
                    {
                        // Bb (b7) → bVII (borrowed chord)
                        var recipe = new ChordRecipe(7, ChordQuality.Major, ChordExtension.None, -1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "bVII",
                            "[CHROMATIC] Bb (b7) → bVII (borrowed chord)", candidates);
                    }
                    else if (accidentalHint == AccidentalHint.Sharp)
                    {
                        // A# (#6) → F# major - A# is the 3rd of F#
                        var recipe = new ChordRecipe(4, ChordQuality.Major, ChordExtension.None, 1, SeventhQuality.None);
                        TryBuildAndAddChromaticCandidate(key, recipe, pitchClass, melodyMidi, "F#",
                            "[CHROMATIC] A# (#6) → F# major", candidates);
                    }
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// Helper to try adding a dominant 7th chromatic candidate where the melody note
        /// is at a specific interval from the root.
        /// </summary>
        private static void TryAddChromaticDominant7th(
            TheoryKey key,
            int melodyPc,
            int melodyMidi,
            int rootIntervalSemitones,
            string roman,
            string reason,
            List<ChordCandidate> candidates)
        {
            // Compute root pitch class: (melodyPc - rootIntervalSemitones + 12) % 12
            int rootPc = (melodyPc - rootIntervalSemitones + 12) % 12;
            
            // Find which degree this root corresponds to (if any)
            int rootDegree = -1;
            int rootOffset = 0;
            
            for (int deg = 1; deg <= 7; deg++)
            {
                int degreePc = TheoryScale.GetDegreePitchClass(key, deg);
                int diff = (rootPc - degreePc + 12) % 12;
                if (diff > 6) diff -= 12;
                
                if (System.Math.Abs(diff) <= 1) // Allow ±1 semitone offset
                {
                    rootDegree = deg;
                    rootOffset = diff;
                    break;
                }
            }
            
            if (rootDegree < 0)
                return; // Couldn't find a matching degree
            
            var recipe = new ChordRecipe(
                rootDegree,
                ChordQuality.Major,
                ChordExtension.Seventh,
                rootOffset,
                SeventhQuality.Dominant7
            );
            
            TryBuildAndAddChromaticCandidate(key, recipe, melodyPc, melodyMidi, roman, reason, candidates);
        }

        /// <summary>
        /// Helper to build and add a chromatic candidate, verifying the melody note is in the chord.
        /// </summary>
        private static void TryBuildAndAddChromaticCandidate(
            TheoryKey key,
            ChordRecipe recipe,
            int melodyPc,
            int melodyMidi,
            string roman,
            string reason,
            List<ChordCandidate> candidates)
        {
            // Build chord and verify melody note is present
            int[] chordMidi = TheoryChord.BuildChord(key, recipe, 4);
            if (chordMidi == null || chordMidi.Length == 0)
                return;

            bool containsMelody = false;
            foreach (var midi in chordMidi)
            {
                int pc = TheoryPitch.PitchClassFromMidi(midi);
                if (pc == melodyPc)
                {
                    containsMelody = true;
                    break;
                }
            }

            if (!containsMelody)
                return;

            // Build chord symbol
            string rootNoteName = TheoryPitch.GetNoteNameForDegreeWithOffset(
                key,
                recipe.Degree,
                recipe.RootSemitoneOffset);
            
            string chordSymbol = TheoryChord.GetChordSymbol(key, recipe, rootNoteName);
            if (string.IsNullOrEmpty(chordSymbol))
                chordSymbol = roman;

            candidates.Add(new ChordCandidate
            {
                Recipe = recipe,
                Roman = roman,
                ChordSymbol = chordSymbol,
                Reason = reason
            });
        }

        /// <summary>
        /// Builds a naive harmonization for a melody line using simple heuristics.
        /// </summary>
        /// <param name="melodyLine">The melody line to harmonize</param>
        /// <param name="key">The key context for harmonization</param>
        /// <param name="settings">Heuristic settings that control harmonization behavior</param>
        /// <returns>List of harmonized chord steps, one per melody note</returns>
        public static List<HarmonizedChordStep> BuildNaiveHarmonization(
            List<MelodyEvent> melodyLine,
            TheoryKey key,
            HarmonyHeuristicSettings settings)
        {
            var result = new List<HarmonizedChordStep>();

            // If melodyLine is empty → return empty list
            if (melodyLine == null || melodyLine.Count == 0)
                return result;

            // If key mode is not Ionian → return empty list
            // TODO: Extend to other modes in future versions
            if (key.Mode != ScaleMode.Ionian)
                return result;

            // Analyze the melody line
            var analyses = TheoryMelody.AnalyzeMelodyLine(key, melodyLine);

            if (analyses.Count != melodyLine.Count)
            {
                // Analysis failed for some notes, return empty list
                return result;
            }

            ChordCandidate previousChord = default;
            bool hasPreviousChord = false;

            for (int i = 0; i < melodyLine.Count; i++)
            {
                var melodyEvent = melodyLine[i];
                var analysis = analyses[i];

                // Get candidates for this note (pass accidental hint from melody event)
                var candidates = GetChordCandidatesForMelodyNote(key, analysis, melodyEvent.AccidentalHint);

                HarmonizedChordStep step = default;
                step.MelodyEvent = melodyEvent;
                step.MelodyAnalysis = analysis;
                step.Candidates = candidates;

                // If no candidates → try to reuse previous chord for non-diatonic notes
                if (candidates == null || candidates.Count == 0)
                {
                    // If we have a previous valid chord, reuse it (common for chromatic passing tones)
                    // This is a naive strategy: hold the previous harmony under non-diatonic melody notes.
                    // More sophisticated chromatic candidate generation may replace this later.
                    if (i > 0 && hasPreviousChord && previousChord.Roman != null && previousChord.Roman != "")
                    {
                        // Reuse the previous chord (holding the harmony under a chromatic passing tone)
                        step.ChosenChord = previousChord;
                        step.Reason = $"Non-diatonic melody note; reused previous chord {previousChord.Roman} ({previousChord.ChordSymbol}) (holding harmony under passing tone).";
                        // Note: previousChord remains unchanged, so if multiple non-diatonic notes follow,
                        // they will all reuse the same chord, and subsequent steps will still reference it as "previous"
                        result.Add(step);
                        continue;
                    }
                    else
                    {
                        // No previous chord available (e.g., first note is non-diatonic) - leave empty
                        step.ChosenChord = default;
                        step.Reason = $"No chord candidates found for melody note (degree {analysis.Degree}, diatonic: {analysis.IsDiatonic})";
                        result.Add(step);
                        continue;
                    }
                }

                ChordCandidate chosen;

                // If first note
                if (i == 0)
                {
                    // If settings.PreferTonicStart AND "I" exists → choose I
                    if (settings.PreferTonicStart)
                    {
                        var tonicCandidate = candidates.Find(c => c.Roman == "I");
                        if (tonicCandidate.Roman == "I")
                        {
                            chosen = tonicCandidate;
                            step.Reason = "Chose I (tonic) as starting chord (PreferTonicStart enabled)";
                        }
                        else
                        {
                            // I not available, choose first candidate
                            chosen = candidates[0];
                            step.Reason = $"Chose {chosen.Roman} ({chosen.ChordSymbol}) - first candidate (I not available, PreferTonicStart enabled)";
                        }
                    }
                    else
                    {
                        // Choose first candidate
                        chosen = candidates[0];
                        step.Reason = $"Chose {chosen.Roman} ({chosen.ChordSymbol}) - first candidate";
                    }
                }
                else
                {
                    // For later notes
                    // If settings.PreferChordContinuity AND previous chord still fits → keep previous
                    if (settings.PreferChordContinuity && hasPreviousChord)
                    {
                        // Check if previous chord is still in candidates
                        var previousInCandidates = candidates.Find(c => c.Roman == previousChord.Roman);
                        if (previousInCandidates.Roman == previousChord.Roman)
                        {
                            chosen = previousInCandidates;
                            step.Reason = $"Kept previous chord {chosen.Roman} ({chosen.ChordSymbol}) (PreferChordContinuity enabled)";
                        }
                        else
                        {
                            // Previous chord doesn't fit, choose best transition
                            chosen = ChooseBestTransition(previousChord, candidates);
                            step.Reason = BuildTransitionReason(previousChord, chosen, settings);
                        }
                    }
                    else
                    {
                        // No continuity preference or no previous chord, choose best transition
                        if (hasPreviousChord)
                        {
                            chosen = ChooseBestTransition(previousChord, candidates);
                            step.Reason = BuildTransitionReason(previousChord, chosen, settings);
                        }
                        else
                        {
                            // No previous chord, just pick first
                            chosen = candidates[0];
                            step.Reason = $"Chose {chosen.Roman} ({chosen.ChordSymbol}) - first candidate (no previous chord)";
                        }
                    }
                }

                step.ChosenChord = chosen;
                previousChord = chosen;
                hasPreviousChord = true;

                result.Add(step);
            }

            return result;
        }

        /// <summary>
        /// Converts a list of HarmonizedChordStep objects into a list of ChordEvent
        /// suitable for use with TheoryVoicing.VoiceLeadProgressionWithMelody().
        /// 
        /// - Uses the chosen chord recipe from each step.
        /// - Sets the MelodyMidi of each ChordEvent to the melody note from the step.
        /// - Uses the provided TheoryKey for any required context.
        /// </summary>
        /// <param name="steps">List of harmonized chord steps to convert</param>
        /// <param name="key">The key context for the chord events</param>
        /// <returns>List of ChordEvent objects ready for voicing</returns>
        public static List<ChordEvent> BuildChordEventsFromHarmonization(
            List<HarmonizedChordStep> steps,
            TheoryKey key)
        {
            var result = new List<ChordEvent>();

            // If steps is null/empty, return an empty list
            if (steps == null || steps.Count == 0)
                return result;

            // For each step whose ChosenChord has a valid Recipe
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];

                // Skip steps with no valid chosen chord
                if (step.ChosenChord.Roman == null || step.ChosenChord.Roman == "")
                    continue;

                // Create a new ChordEvent
                var chordEvent = new ChordEvent
                {
                    Key = key,
                    Recipe = step.ChosenChord.Recipe,
                    TimeBeats = step.MelodyEvent.TimeBeats,
                    MelodyMidi = step.MelodyEvent.Midi
                };

                result.Add(chordEvent);
            }

            return result;
        }

        /// <summary>
        /// Chooses the best chord transition from previous chord to available candidates
        /// based on functional harmony preferences.
        /// </summary>
        /// <param name="prev">The previous chord in the progression</param>
        /// <param name="candidates">Available candidate chords for the current melody note</param>
        /// <returns>The best candidate chord for the transition</returns>
        private static ChordCandidate ChooseBestTransition(ChordCandidate prev, List<ChordCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return default;

            string prevRoman = prev.Roman;

            // From I → prefer ii or IV → else V → else first
            if (prevRoman == "I")
            {
                var preferred = candidates.Find(c => c.Roman == "ii" || c.Roman == "IV");
                if (preferred.Roman != null)
                    return preferred;

                var v = candidates.Find(c => c.Roman == "V");
                if (v.Roman == "V")
                    return v;

                return candidates[0];
            }

            // From ii or IV → prefer V or I
            if (prevRoman == "ii" || prevRoman == "IV")
            {
                var preferred = candidates.Find(c => c.Roman == "V" || c.Roman == "I");
                if (preferred.Roman != null)
                    return preferred;

                return candidates[0];
            }

            // From V → prefer I or vi
            if (prevRoman == "V")
            {
                var preferred = candidates.Find(c => c.Roman == "I" || c.Roman == "vi");
                if (preferred.Roman != null)
                    return preferred;

                return candidates[0];
            }

            // From vi → prefer ii or IV
            if (prevRoman == "vi")
            {
                var preferred = candidates.Find(c => c.Roman == "ii" || c.Roman == "IV");
                if (preferred.Roman != null)
                    return preferred;

                return candidates[0];
            }

            // From iii → prefer vi
            if (prevRoman == "iii")
            {
                var preferred = candidates.Find(c => c.Roman == "vi");
                if (preferred.Roman == "vi")
                    return preferred;

                return candidates[0];
            }

            // From vii° → prefer I or iii
            if (prevRoman == "viidim")
            {
                var preferred = candidates.Find(c => c.Roman == "I" || c.Roman == "iii");
                if (preferred.Roman != null)
                    return preferred;

                return candidates[0];
            }

            // Default: return first candidate
            return candidates[0];
        }

        /// <summary>
        /// Builds a reason string explaining a chord transition choice.
        /// </summary>
        private static string BuildTransitionReason(ChordCandidate prev, ChordCandidate chosen, HarmonyHeuristicSettings settings)
        {
            if (settings.EnableDetailedReasonLogs)
            {
                return $"Transitioned from {prev.Roman} ({prev.ChordSymbol}) to {chosen.Roman} ({chosen.ChordSymbol}) - functional preference";
            }
            else
            {
                return $"Chose {chosen.Roman} ({chosen.ChordSymbol}) - best transition from {prev.Roman}";
            }
        }

        /// <summary>
        /// Builds a HarmonizationSnapshot from a list of HarmonizedChordStep objects for JSON export.
        /// </summary>
        /// <param name="key">The key context for the harmonization</param>
        /// <param name="description">Free-form description of the harmonization context</param>
        /// <param name="steps">List of harmonized chord steps to convert</param>
        /// <returns>Populated HarmonizationSnapshot ready for JSON serialization</returns>
        public static HarmonizationSnapshot BuildSnapshotFromHarmonization(
            TheoryKey key,
            string description,
            List<HarmonizedChordStep> steps)
        {
            var snapshot = new HarmonizationSnapshot
            {
                KeyName = key.ToString(),
                Description = description ?? "Harmonization snapshot",
                Steps = new List<HarmonizationStepSnapshot>()
            };

            // If steps is null/empty, return a snapshot with empty Steps
            if (steps == null || steps.Count == 0)
                return snapshot;

            // For each HarmonizedChordStep, create a HarmonizationStepSnapshot
            foreach (var step in steps)
            {
                var stepSnapshot = new HarmonizationStepSnapshot
                {
                    MelodyMidi = step.MelodyEvent.Midi,
                    MelodyDegree = step.MelodyAnalysis.Degree,
                    MelodyIsDiatonic = step.MelodyAnalysis.IsDiatonic,
                    Candidates = new List<CandidateChordSnapshot>(),
                    Chosen = new ChosenChordSnapshot()
                };

                // Map Candidates to CandidateChordSnapshot
                if (step.Candidates != null)
                {
                    foreach (var candidate in step.Candidates)
                    {
                        stepSnapshot.Candidates.Add(new CandidateChordSnapshot
                        {
                            Roman = candidate.Roman ?? "",
                            ChordSymbol = candidate.ChordSymbol ?? "",
                            Reason = candidate.Reason ?? ""
                        });
                    }
                }

                // Map ChosenChord + step.Reason into a ChosenChordSnapshot
                if (step.ChosenChord.Roman != null)
                {
                    stepSnapshot.Chosen = new ChosenChordSnapshot
                    {
                        Roman = step.ChosenChord.Roman,
                        ChordSymbol = step.ChosenChord.ChordSymbol ?? "",
                        Reason = step.Reason ?? ""
                    };
                }
                else
                {
                    // No chosen chord - set empty values
                    stepSnapshot.Chosen = new ChosenChordSnapshot
                    {
                        Roman = "",
                        ChordSymbol = "",
                        Reason = step.Reason ?? "No chord chosen"
                    };
                }

                snapshot.Steps.Add(stepSnapshot);
            }

            return snapshot;
        }
    }
}

