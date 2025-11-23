using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sonoria.Dictation
{
    /// <summary>
    /// Serializable save data for campaign progress. Persisted as JSON.
    /// </summary>
    [Serializable]
    public class CampaignSave
    {
        [Header("Save Metadata")]
        public int version = 1;

        [Header("Node Progress")]
        [Tooltip("Progress data for each node in the campaign")]
        public List<NodeSaveData> nodes = new List<NodeSaveData>();

        [Header("Last Played")]
        [Tooltip("Last node index that was played")]
        public int lastNodeIndex = 0;

        [Tooltip("Last level index that was played")]
        public int lastLevelIndex = 0;

        /// <summary>
        /// Creates a default save with the first node unlocked and all levels incomplete.
        /// </summary>
        public static CampaignSave CreateDefault(int nodeCount)
        {
            var save = new CampaignSave
            {
                version = 1,
                nodes = new List<NodeSaveData>(),
                lastNodeIndex = 0,
                lastLevelIndex = 0
            };

            // Initialize all nodes
            for (int i = 0; i < nodeCount; i++)
            {
                var nodeData = new NodeSaveData
                {
                    mode = "", // Will be set from campaign data
                    unlocked = (i == 0), // Only first node unlocked
                    levels = new List<bool>(6) { false, false, false, false, false, false },
                    winsPerLevel = new List<int>(6) { 0, 0, 0, 0, 0, 0 }
                };

                save.nodes.Add(nodeData);
            }

            return save;
        }

        /// <summary>
        /// Gets the save data for a specific node, creating it if it doesn't exist.
        /// </summary>
        public NodeSaveData GetOrCreateNodeData(int nodeIndex, string modeName)
        {
            // Ensure list is large enough
            while (nodes.Count <= nodeIndex)
            {
                nodes.Add(new NodeSaveData
                {
                    mode = "",
                    unlocked = false,
                    levels = new List<bool>(6) { false, false, false, false, false, false },
                    winsPerLevel = new List<int>(6) { 0, 0, 0, 0, 0, 0 }
                });
            }

            var nodeData = nodes[nodeIndex];
            if (string.IsNullOrEmpty(nodeData.mode))
            {
                nodeData.mode = modeName;
            }

            // Ensure levels arrays are correct size
            while (nodeData.levels.Count < 6)
                nodeData.levels.Add(false);
            while (nodeData.winsPerLevel.Count < 6)
                nodeData.winsPerLevel.Add(0);

            return nodeData;
        }

        /// <summary>
        /// Checks if a node is unlocked.
        /// </summary>
        public bool IsNodeUnlocked(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return false;

            return nodes[nodeIndex].unlocked;
        }

        /// <summary>
        /// Checks if a level is completed.
        /// </summary>
        public bool IsLevelComplete(int nodeIndex, int levelIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return false;

            var nodeData = nodes[nodeIndex];
            if (levelIndex < 0 || levelIndex >= nodeData.levels.Count)
                return false;

            return nodeData.levels[levelIndex];
        }

        /// <summary>
        /// Marks a level as completed.
        /// </summary>
        public void MarkLevelComplete(int nodeIndex, int levelIndex)
        {
            var nodeData = GetOrCreateNodeData(nodeIndex, "");
            if (levelIndex >= 0 && levelIndex < nodeData.levels.Count)
            {
                nodeData.levels[levelIndex] = true;
            }
        }

        /// <summary>
        /// Gets the number of completed levels for a node.
        /// </summary>
        public int GetCompletedLevelCount(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return 0;

            var nodeData = nodes[nodeIndex];
            int count = 0;
            foreach (bool completed in nodeData.levels)
            {
                if (completed) count++;
            }

            return count;
        }

        /// <summary>
        /// Unlocks a node.
        /// </summary>
        public void UnlockNode(int nodeIndex)
        {
            var nodeData = GetOrCreateNodeData(nodeIndex, "");
            nodeData.unlocked = true;
        }

        /// <summary>
        /// Gets the next incomplete level index for a node, or -1 if all are complete.
        /// </summary>
        public int GetNextIncompleteLevelIndex(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return -1;

            var nodeData = nodes[nodeIndex];
            for (int i = 0; i < nodeData.levels.Count && i < 6; i++)
            {
                if (!nodeData.levels[i])
                    return i;
            }

            return -1; // All levels complete
        }
    }

    /// <summary>
    /// Save data for a single mode node.
    /// </summary>
    [Serializable]
    public class NodeSaveData
    {
        [Tooltip("Mode name (e.g., 'Ionian', 'Mixolydian')")]
        public string mode;

        [Tooltip("Whether this node is unlocked")]
        public bool unlocked;

        [Tooltip("Completion flags for each level (L1-L6)")]
        public List<bool> levels = new List<bool>(6);

        [Tooltip("Win count per level (runtime-only, optional for resume-in-level feature)")]
        public List<int> winsPerLevel = new List<int>(6);
    }
}

