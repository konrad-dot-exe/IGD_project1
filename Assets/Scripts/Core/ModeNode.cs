using UnityEngine;
using EarFPS;

namespace Sonoria.Dictation
{
    /// <summary>
    /// Represents a mode node in the campaign map. Contains 6 levels for a specific scale mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Sonoria/Mode Node", fileName = "ModeNode")]
    public class ModeNode : ScriptableObject
    {
        [Header("Mode Identity")]
        [Tooltip("The scale mode for this node (Ionian, Mixolydian, Dorian, etc.)")]
        public EarFPS.ScaleMode mode = EarFPS.ScaleMode.Ionian;

        [Header("Levels")]
        [Tooltip("The 6 levels for this mode (L1 through L6)")]
        public DictationLevel[] levels = new DictationLevel[6];

        private void OnValidate()
        {
            // Ensure levels array is exactly 6 elements
            if (levels == null)
            {
                levels = new DictationLevel[6];
            }
            else if (levels.Length != 6)
            {
                System.Array.Resize(ref levels, 6);
            }
        }

        /// <summary>
        /// Gets the display name of the mode (e.g., "Ionian", "Mixolydian").
        /// </summary>
        public string GetModeName()
        {
            return mode.ToString();
        }

        /// <summary>
        /// Validates that all 6 levels are assigned.
        /// </summary>
        public bool IsValid()
        {
            if (levels == null || levels.Length != 6)
                return false;

            for (int i = 0; i < 6; i++)
            {
                if (levels[i] == null)
                    return false;
            }

            return true;
        }
    }
}

