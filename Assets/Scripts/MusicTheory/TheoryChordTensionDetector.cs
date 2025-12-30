using System.Collections.Generic;
using System.Linq;

namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Detects chord tensions (9ths, 11ths, 13ths) from realized voicings.
    /// </summary>
    public static class ChordTensionDetector
    {
        /// <summary>
        /// Detects any b9/9/#9 tensions present in the given MIDI notes for this chord.
        /// Does NOT modify voicing; returns a unique set of tensions.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="midiNotes">All MIDI notes in the voicing (SATB voices + melody if present)</param>
        /// <param name="key">The key context</param>
        /// <returns>List of detected 9th tensions (may be empty)</returns>
        public static List<ChordTension> DetectNinthTensions(
            ChordRecipe recipe,
            IEnumerable<int> midiNotes,
            TheoryKey key)
        {
            var tensions = new List<ChordTension>();
            
            if (midiNotes == null)
                return tensions;

            // Get the root pitch class of the chord from recipe + key
            int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPc < 0)
                rootPc = 0; // Fallback to C
            rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
            if (rootPc < 0)
                rootPc += 12;

            // Get core chord tone pitch classes to avoid counting them as tensions
            var coreChordTonePcs = new HashSet<int>();
            
            // Root
            coreChordTonePcs.Add(rootPc);
            
            // Third
            int thirdInterval = recipe.Quality switch
            {
                ChordQuality.Major => 4,
                ChordQuality.Minor => 3,
                ChordQuality.Diminished => 3,
                ChordQuality.Augmented => 4,
                _ => 4
            };
            int thirdPc = (rootPc + thirdInterval) % 12;
            coreChordTonePcs.Add(thirdPc);
            
            // Fifth
            int fifthInterval = recipe.Quality switch
            {
                ChordQuality.Major => 7,
                ChordQuality.Minor => 7,
                ChordQuality.Diminished => 6,
                ChordQuality.Augmented => 8,
                _ => 7
            };
            int fifthPc = (rootPc + fifthInterval) % 12;
            coreChordTonePcs.Add(fifthPc);
            
            // Seventh (if present)
            bool hasSeventh = recipe.Extension == ChordExtension.Seventh && 
                             recipe.SeventhQuality != SeventhQuality.None;
            if (hasSeventh)
            {
                int seventhInterval = recipe.SeventhQuality switch
                {
                    SeventhQuality.Major7 => 11,
                    SeventhQuality.Minor7 => 10,
                    SeventhQuality.Dominant7 => 10,
                    SeventhQuality.HalfDiminished7 => 10,
                    SeventhQuality.Diminished7 => 9,
                    _ => 10
                };
                int seventhPc = (rootPc + seventhInterval) % 12;
                coreChordTonePcs.Add(seventhPc);
            }

            // Track which tensions we've already found (to avoid duplicates)
            var foundTensions = new HashSet<TensionKind>();

            // For each MIDI note, check if it's a 9th tension
            foreach (int midi in midiNotes)
            {
                int notePc = ((midi % 12) + 12) % 12;
                
                // Skip if this is a core chord tone
                if (coreChordTonePcs.Contains(notePc))
                    continue;
                
                // Compute the interval above the root in semitones (mod 12)
                int rel = (notePc - rootPc + 12) % 12;
                
                // Check if this interval corresponds to a 9th tension
                if (ChordTensionUtils.TryGetNinthTensionKindFromInterval(rel, out TensionKind kind))
                {
                    // Add to list if not already present
                    if (!foundTensions.Contains(kind))
                    {
                        tensions.Add(new ChordTension(kind));
                        foundTensions.Add(kind);
                    }
                }
            }

            return tensions;
        }
        
        /// <summary>
        /// Gets tension candidate notes for v1 (soprano-only detection).
        /// Returns only the soprano note for now; structured for future expansion to inner voices.
        /// </summary>
        /// <param name="midiNotes">All MIDI notes in the voicing (SATB voices + melody if present)</param>
        /// <returns>Enumerable of MIDI notes to check for tensions (v1: soprano only)</returns>
        private static IEnumerable<int> GetTensionCandidates_V1(IEnumerable<int> midiNotes)
        {
            if (midiNotes == null)
                yield break;
            
            // v1: Only check soprano (highest note)
            // For SATB, soprano is typically the last voice, but we'll take the highest MIDI note
            int maxMidi = int.MinValue;
            foreach (int midi in midiNotes)
            {
                if (midi > maxMidi)
                    maxMidi = midi;
            }
            
            if (maxMidi >= 0)
            {
                yield return maxMidi;
            }
        }
        
        /// <summary>
        /// Detects any 11/#11 tensions present in the soprano (v1: soprano-only).
        /// Does NOT modify voicing; returns a unique set of tensions.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="midiNotes">All MIDI notes in the voicing (SATB voices + melody if present)</param>
        /// <param name="key">The key context</param>
        /// <returns>List of detected 11th tensions (may be empty)</returns>
        public static List<ChordTension> DetectEleventhTensions(
            ChordRecipe recipe,
            IEnumerable<int> midiNotes,
            TheoryKey key)
        {
            var tensions = new List<ChordTension>();
            
            if (midiNotes == null)
                return tensions;

            // Get the root pitch class of the chord from recipe + key
            int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
            if (rootPc < 0)
                rootPc = 0; // Fallback to C
            rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
            if (rootPc < 0)
                rootPc += 12;

            // Get core chord tone pitch classes to avoid counting them as tensions
            var coreChordTonePcs = new HashSet<int>();
            
            // Root
            coreChordTonePcs.Add(rootPc);
            
            // Third
            int thirdInterval = recipe.Quality switch
            {
                ChordQuality.Major => 4,
                ChordQuality.Minor => 3,
                ChordQuality.Diminished => 3,
                ChordQuality.Augmented => 4,
                _ => 4
            };
            int thirdPc = (rootPc + thirdInterval) % 12;
            coreChordTonePcs.Add(thirdPc);
            
            // Fifth
            int fifthInterval = recipe.Quality switch
            {
                ChordQuality.Major => 7,
                ChordQuality.Minor => 7,
                ChordQuality.Diminished => 6,
                ChordQuality.Augmented => 8,
                _ => 7
            };
            int fifthPc = (rootPc + fifthInterval) % 12;
            coreChordTonePcs.Add(fifthPc);
            
            // Seventh (if present)
            bool hasSeventh = recipe.Extension == ChordExtension.Seventh && 
                             recipe.SeventhQuality != SeventhQuality.None;
            if (hasSeventh)
            {
                int seventhInterval = recipe.SeventhQuality switch
                {
                    SeventhQuality.Major7 => 11,
                    SeventhQuality.Minor7 => 10,
                    SeventhQuality.Dominant7 => 10,
                    SeventhQuality.HalfDiminished7 => 10,
                    SeventhQuality.Diminished7 => 9,
                    _ => 10
                };
                int seventhPc = (rootPc + seventhInterval) % 12;
                coreChordTonePcs.Add(seventhPc);
            }

            // Check if 3rd is present in realized voicing
            var realizedPcs = new HashSet<int>(midiNotes.Select(m => ((m % 12) + 12) % 12));
            bool hasThirdInVoicing = realizedPcs.Contains(thirdPc);

            // Track which tensions we've already found (to avoid duplicates)
            var foundTensions = new HashSet<TensionKind>();

            // v1: Only check soprano (highest note)
            foreach (int midi in GetTensionCandidates_V1(midiNotes))
            {
                int notePc = ((midi % 12) + 12) % 12;
                
                // Skip if this is a core chord tone
                if (coreChordTonePcs.Contains(notePc))
                    continue;
                
                // Compute the interval above the root in semitones (mod 12)
                int rel = (notePc - rootPc + 12) % 12;
                
                // Check if this interval corresponds to an 11th tension
                if (ChordTensionUtils.TryGetEleventhTensionKindFromInterval(rel, out TensionKind kind))
                {
                    // Add to list if not already present
                    if (!foundTensions.Contains(kind))
                    {
                        // Classify the tension based on chord type and voicing
                        TensionClassification classification = ClassifyEleventhTension(
                            kind, recipe, hasSeventh, hasThirdInVoicing);
                        tensions.Add(new ChordTension(kind, classification));
                        foundTensions.Add(kind);
                    }
                }
            }

            return tensions;
        }
        
        /// <summary>
        /// Classifies an 11th tension based on chord type and voicing context.
        /// </summary>
        private static TensionClassification ClassifyEleventhTension(
            TensionKind kind,
            ChordRecipe recipe,
            bool hasSeventh,
            bool hasThirdInVoicing)
        {
            if (kind == TensionKind.Eleven)
            {
                // Natural 11 rules
                if (hasSeventh && recipe.SeventhQuality == SeventhQuality.Dominant7)
                {
                    // Dominant 7: always classify as Suspension (will warn if 3rd is present)
                    return TensionClassification.Suspension; // 11 over dom7 = 7sus4 (warn if 3rd present)
                }
                else if (hasSeventh && (recipe.SeventhQuality == SeventhQuality.Major7 || 
                                        recipe.SeventhQuality == SeventhQuality.Minor7))
                {
                    // Major7 or Minor7
                    return TensionClassification.Suspension; // sus4
                }
                else
                {
                    // Triad (major or minor)
                    return TensionClassification.Suspension; // sus4
                }
            }
            else if (kind == TensionKind.SharpEleven)
            {
                // #11 rules
                if (hasSeventh && recipe.SeventhQuality == SeventhQuality.Dominant7)
                {
                    return TensionClassification.ColorTone; // #11 on dom7 = color tone
                }
                else if (hasSeventh && recipe.SeventhQuality == SeventhQuality.Major7)
                {
                    return TensionClassification.ColorTone; // #11 on maj7 = color tone
                }
                else if (!hasSeventh && recipe.Quality == ChordQuality.Major)
                {
                    return TensionClassification.ColorTone; // #11 on major triad = color tone
                }
                else if (hasSeventh && recipe.SeventhQuality == SeventhQuality.Minor7)
                {
                    return TensionClassification.NonChordTone; // #11 on min7 = non-chord tone
                }
                else if (recipe.Quality == ChordQuality.Minor || 
                         recipe.Quality == ChordQuality.Diminished)
                {
                    return TensionClassification.NonChordTone; // #11 on minor/dim = non-chord tone
                }
                else
                {
                    return TensionClassification.NonChordTone; // Default: non-chord tone
                }
            }
            
            // Default classification for other tensions
            return TensionClassification.ColorTone;
        }
        
        /// <summary>
        /// Detects all tensions (9ths and 11ths) from a realized voicing.
        /// Returns a DetectedTensions struct containing tensions and the analyzed voicing.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="analyzedMidi">The exact MIDI voicing analyzed [B,T,A,S] or all notes</param>
        /// <param name="key">The key context</param>
        /// <returns>DetectedTensions struct with tensions and analyzed voicing info</returns>
        public static DetectedTensions DetectTensions(
            ChordRecipe recipe,
            int[] analyzedMidi,
            TheoryKey key)
        {
            if (analyzedMidi == null || analyzedMidi.Length == 0)
            {
                return new DetectedTensions(new List<ChordTension>(), new int[0], new int[0]);
            }
            
            // Compute pitch classes from analyzed MIDI
            var analyzedPcs = analyzedMidi.Select(m => ((m % 12) + 12) % 12).ToArray();
            
            // Detect all tensions (9ths use default ColorTone classification for now)
            var tensions = new List<ChordTension>();
            var ninthTensions = DetectNinthTensions(recipe, analyzedMidi, key);
            // Assign default ColorTone classification to 9ths (they don't need special classification yet)
            tensions.AddRange(ninthTensions.Select(t => new ChordTension(t.Kind, TensionClassification.ColorTone)));
            tensions.AddRange(DetectEleventhTensions(recipe, analyzedMidi, key));
            
            // Debug logging: print comprehensive tension detection info
            if (TheoryVoicing.GetDebugTensionDetect())
            {
                // Get root PC
                int rootPc = TheoryScale.GetDegreePitchClass(key, recipe.Degree);
                if (rootPc < 0) rootPc = 0;
                rootPc = (rootPc + recipe.RootSemitoneOffset + 12) % 12;
                if (rootPc < 0) rootPc += 12;
                
                // Get bass PC
                int bassPc = analyzedMidi.Length > 0 ? ((analyzedMidi[0] % 12) + 12) % 12 : -1;
                
                // Get soprano info
                int sopranoMidi = analyzedMidi.Length > 0 ? analyzedMidi[analyzedMidi.Length - 1] : -1;
                int sopranoPc = sopranoMidi >= 0 ? ((sopranoMidi % 12) + 12) % 12 : -1;
                
                // Get realized PCs set
                var realizedPcsSet = new HashSet<int>(analyzedPcs);
                
                // Get chord tone PCs set
                var chordTonePcs = TheoryVoicing.GetChordTonePitchClasses(new ChordEvent
                {
                    Key = key,
                    Recipe = recipe
                });
                var chordTonePcsSet = chordTonePcs != null ? new HashSet<int>(chordTonePcs) : new HashSet<int>();
                
                // Format chord quality
                string qualityStr = recipe.Quality.ToString();
                bool hasSeventh = recipe.Extension == ChordExtension.Seventh && 
                                 recipe.SeventhQuality != SeventhQuality.None;
                if (hasSeventh)
                {
                    qualityStr += "/" + recipe.SeventhQuality.ToString();
                }
                
                // Format detected tensions
                string tensionsStr = tensions.Count > 0 
                    ? string.Join(",", tensions.Select(t => $"{t.Kind}({t.Classification})"))
                    : "none";
                
                UnityEngine.Debug.Log(
                    $"[TENSION_DETECT_DEBUG] quality={qualityStr} " +
                    $"rootPC={rootPc} bassPC={bassPc} " +
                    $"soprano={sopranoMidi}(pc={sopranoPc}) " +
                    $"realizedPCs=[{string.Join(",", realizedPcsSet.OrderBy(x => x))}] " +
                    $"chordTonePCs=[{string.Join(",", chordTonePcsSet.OrderBy(x => x))}] " +
                    $"detected=[{tensionsStr}]");
            }
            
            return new DetectedTensions(tensions, analyzedMidi, analyzedPcs);
        }
    }
}

