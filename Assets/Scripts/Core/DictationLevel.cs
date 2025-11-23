using UnityEngine;
using EarFPS;

namespace Sonoria.Dictation
{
    /// <summary>
    /// Represents a single level in the campaign. Wraps a DifficultyProfile with level-specific metadata.
    /// </summary>
    [CreateAssetMenu(menuName = "Sonoria/Dictation Level", fileName = "DictationLevel")]
    public class DictationLevel : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier within the campaign (e.g., 'Ionian-L3')")]
        public string id;

        [Tooltip("Display title (e.g., 'Level 3 — Up to Fourths')")]
        public string title;

        [Tooltip("Intro text placeholder for future tutorial content")]
        [TextArea(2, 5)]
        public string intro;

        [Header("Gameplay")]
        [Tooltip("Difficulty profile that defines the melody generation parameters for this level")]
        public DifficultyProfile profile;

        [Tooltip("Override the scale mode from the profile. If enabled, the mode specified below will be used instead of the profile's allowedModes.")]
        public bool useModeOverride = false;

        [Tooltip("Scale mode to use when useModeOverride is enabled. Ignored if useModeOverride is false.")]
        public EarFPS.ScaleMode modeOverride = EarFPS.ScaleMode.Ionian;

        [Tooltip("Number of successful rounds required to complete this level")]
        [Min(1)]
        public int roundsToWin = 3;

        private void OnValidate()
        {
            // Ensure roundsToWin is at least 1
            if (roundsToWin < 1)
                roundsToWin = 1;

            // Auto-generate ID if empty
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title))
            {
                id = title.Replace(" ", "-").Replace("—", "-");
            }
        }
    }
}

