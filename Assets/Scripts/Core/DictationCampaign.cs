using UnityEngine;

namespace Sonoria.Dictation
{
    /// <summary>
    /// The root campaign asset that defines the complete node map and progression order.
    /// </summary>
    [CreateAssetMenu(menuName = "Sonoria/Dictation Campaign", fileName = "DictationCampaign")]
    public class DictationCampaign : ScriptableObject
    {
        [Header("Campaign Structure")]
        [Tooltip("Ordered array of mode nodes. Expected order: Ionian → Mixolydian → Dorian → Aeolian → Phrygian → Lydian")]
        public ModeNode[] nodes;

        [Header("Campaign Start")]
        [Tooltip("Index of the starting node (typically 0 for Ionian)")]
        public int startNodeIndex = 0;

        private void OnValidate()
        {
            // Ensure startNodeIndex is valid
            if (nodes == null || nodes.Length == 0)
            {
                startNodeIndex = 0;
                return;
            }

            if (startNodeIndex < 0)
                startNodeIndex = 0;
            if (startNodeIndex >= nodes.Length)
                startNodeIndex = nodes.Length - 1;
        }

        /// <summary>
        /// Gets the starting node, or null if invalid.
        /// </summary>
        public ModeNode GetStartNode()
        {
            if (nodes == null || nodes.Length == 0)
                return null;

            if (startNodeIndex < 0 || startNodeIndex >= nodes.Length)
                return null;

            return nodes[startNodeIndex];
        }

        /// <summary>
        /// Validates the campaign structure.
        /// </summary>
        public bool IsValid()
        {
            if (nodes == null || nodes.Length == 0)
                return false;

            // Check that all nodes are assigned and valid
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                {
                    Debug.LogWarning($"[Campaign] Node at index {i} is null");
                    return false;
                }

                if (!nodes[i].IsValid())
                {
                    Debug.LogWarning($"[Campaign] Node at index {i} ({nodes[i].GetModeName()}) has invalid levels");
                    return false;
                }
            }

            return true;
        }
    }
}

