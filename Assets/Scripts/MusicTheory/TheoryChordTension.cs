namespace Sonoria.MusicTheory
{
    /// <summary>
    /// Helper class for identifying chord tensions (altered tones, extensions, etc.).
    /// Used to determine which chord tones have special resolution tendencies.
    /// </summary>
    public static class ChordTensionHelper
    {
        /// <summary>
        /// Returns true if this role is an augmented fifth (#5) in this chord.
        /// </summary>
        /// <param name="recipe">The chord recipe</param>
        /// <param name="role">The role of the chord tone (Root, Third, Fifth, or Seventh)</param>
        /// <returns>True if the role is an augmented 5th, false otherwise</returns>
        public static bool IsAugmentedFifth(ChordRecipe recipe, ChordToneRole role)
        {
            // Only the Fifth role can be an augmented 5th
            if (role != ChordToneRole.Fifth)
                return false;

            // Check if chord quality is Augmented (this means the 5th is augmented)
            if (recipe.Quality == ChordQuality.Augmented)
                return true;

            // Future: If ChordRecipe gains explicit #5 alteration flags, check those here
            // For now, Augmented quality is the only way to have a #5

            return false;
        }
    }
}

