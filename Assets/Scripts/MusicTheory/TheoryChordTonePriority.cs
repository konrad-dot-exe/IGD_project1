namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Priority level for chord tones in SATB voicing.
    /// Determines which tones can be dropped when there are more distinct chord tones than voices.
    /// </summary>
    public enum ChordTonePriority
    {
        /// <summary>
        /// Okay to drop if necessary (e.g., perfect 5th in simple major/minor triads).
        /// </summary>
        Optional = 0,
        
        /// <summary>
        /// Include if possible, but can be sacrificed after Optional tones.
        /// </summary>
        Preferred = 1,
        
        /// <summary>
        /// Do not drop unless absolutely forced (e.g., altered 5ths, 3rds, 7ths in 7th chords).
        /// </summary>
        Required = 2
    }

    /// <summary>
    /// Role/position of a chord tone within the chord structure.
    /// </summary>
    public enum ChordToneRole
    {
        /// <summary>
        /// Root of the chord (degree 0).
        /// </summary>
        Root = 0,
        
        /// <summary>
        /// Third of the chord (major or minor 3rd above root).
        /// </summary>
        Third = 1,
        
        /// <summary>
        /// Fifth of the chord (perfect, diminished, or augmented 5th above root).
        /// </summary>
        Fifth = 2,
        
        /// <summary>
        /// Seventh of the chord (if present in 7th chords).
        /// </summary>
        Seventh = 3
    }

    /// <summary>
    /// Helper class for determining chord tone priorities based on chord type.
    /// Encodes musical rules about which tones are essential vs. optional in SATB voicing.
    /// </summary>
    public static class ChordTonePriorityHelper
    {
        /// <summary>
        /// Gets the priority for a specific chord tone role in a given chord recipe.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="role">The role of the chord tone (Root, Third, Fifth, or Seventh)</param>
        /// <returns>The priority level for this tone</returns>
        public static ChordTonePriority GetPriorityForTone(ChordRecipe recipe, ChordToneRole role)
        {
            // Determine if this chord has an altered 5th (diminished or augmented)
            bool hasAlteredFifth = recipe.Quality == ChordQuality.Diminished || 
                                   recipe.Quality == ChordQuality.Augmented;
            
            // Determine if this is a 7th chord
            bool hasSeventh = recipe.Extension == ChordExtension.Seventh && 
                             recipe.SeventhQuality != SeventhQuality.None;

            // Apply rules based on role
            switch (role)
            {
                case ChordToneRole.Root:
                    // Root is always important, but not always strictly required
                    // In most contexts, root is Required
                    return ChordTonePriority.Required;

                case ChordToneRole.Third:
                    // Third defines the chord quality (major/minor) and is always Required
                    return ChordTonePriority.Required;

                case ChordToneRole.Fifth:
                    // Fifth priority depends on whether it's altered
                    if (hasAlteredFifth)
                    {
                        // Altered 5ths (#5, b5) are color tones and must be kept
                        return ChordTonePriority.Required;
                    }
                    else
                    {
                        // Perfect 5th in simple chords is Optional (can be dropped for smoother voice leading)
                        return ChordTonePriority.Optional;
                    }

                case ChordToneRole.Seventh:
                    if (hasSeventh)
                    {
                        // 7th defines the function and is Required
                        return ChordTonePriority.Required;
                    }
                    else
                    {
                        // No 7th present
                        return ChordTonePriority.Optional;
                    }

                default:
                    // Unknown role - default to Optional
                    return ChordTonePriority.Optional;
            }
        }

        /// <summary>
        /// Gets priorities for all chord tone roles in a recipe.
        /// Returns a dictionary mapping role to priority.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <returns>Dictionary mapping each role to its priority</returns>
        public static System.Collections.Generic.Dictionary<ChordToneRole, ChordTonePriority> GetAllPriorities(ChordRecipe recipe)
        {
            var priorities = new System.Collections.Generic.Dictionary<ChordToneRole, ChordTonePriority>();
            priorities[ChordToneRole.Root] = GetPriorityForTone(recipe, ChordToneRole.Root);
            priorities[ChordToneRole.Third] = GetPriorityForTone(recipe, ChordToneRole.Third);
            priorities[ChordToneRole.Fifth] = GetPriorityForTone(recipe, ChordToneRole.Fifth);
            priorities[ChordToneRole.Seventh] = GetPriorityForTone(recipe, ChordToneRole.Seventh);
            return priorities;
        }

        /// <summary>
        /// Counts how many distinct chord tones have Required priority.
        /// Useful for the special case where all required tones must be kept without doubling.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="hasSeventh">Whether the chord actually has a 7th (affects which tones are present)</param>
        /// <returns>Number of distinct required tones</returns>
        public static int CountRequiredTones(ChordRecipe recipe, bool hasSeventh)
        {
            int count = 0;
            
            // Root and Third are always required
            count += 2;
            
            // Fifth is required if altered
            if (recipe.Quality == ChordQuality.Diminished || recipe.Quality == ChordQuality.Augmented)
            {
                count++;
            }
            
            // Seventh is required if present
            if (hasSeventh)
            {
                count++;
            }
            
            return count;
        }
    }
}

