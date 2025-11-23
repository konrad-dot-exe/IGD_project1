using System;
using System.IO;
using UnityEngine;

namespace Sonoria.Dictation
{
    /// <summary>
    /// Manages campaign state, save/load, and progression logic.
    /// Singleton service that persists across scenes.
    /// </summary>
    public class CampaignService : MonoBehaviour
    {
        public static CampaignService Instance { get; private set; }

        [Header("Campaign Data")]
        [Tooltip("The campaign asset containing all nodes and levels")]
        [SerializeField] DictationCampaign campaign;

        [Header("Save Settings")]
        [Tooltip("Name of the save file (will be saved in Application.persistentDataPath)")]
        [SerializeField] string saveFileName = "sonoria_campaign.json";

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] bool debugLog = false;

        // Runtime state
        private CampaignSave currentSave;
        private int currentNodeIndex = -1;
        private int currentLevelIndex = -1;
        private int winsThisLevel = 0;

        [Header("Scene References")]
        [Tooltip("Reference to DifficultyProfileApplier (optional, will find if not assigned)")]
        [SerializeField] DifficultyProfileApplier difficultyApplier;

        [Tooltip("Reference to MelodicDictationController (optional, will find if not assigned)")]
        [SerializeField] EarFPS.MelodicDictationController dictationController;

        // Properties
        public ModeNode CurrentNode 
        { 
            get 
            { 
                if (campaign == null || currentNodeIndex < 0 || currentNodeIndex >= campaign.nodes.Length)
                    return null;
                return campaign.nodes[currentNodeIndex];
            }
        }

        public DictationLevel CurrentLevel
        {
            get
            {
                var node = CurrentNode;
                if (node == null || currentLevelIndex < 0 || currentLevelIndex >= node.levels.Length)
                    return null;
                return node.levels[currentLevelIndex];
            }
        }

        public int CurrentNodeIndex => currentNodeIndex;
        public int CurrentLevelIndex => currentLevelIndex;

        public DictationCampaign Campaign => campaign;

        /// <summary>
        /// Gets the mode name for a node index.
        /// </summary>
        public string GetNodeModeName(int nodeIndex)
        {
            if (campaign == null || campaign.nodes == null || nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
                return "";
            
            var node = campaign.nodes[nodeIndex];
            return node != null ? node.GetModeName() : "";
        }

        /// <summary>
        /// Gets the ScaleMode enum for a node index.
        /// </summary>
        public EarFPS.ScaleMode GetNodeMode(int nodeIndex)
        {
            if (campaign == null || campaign.nodes == null || nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
                return EarFPS.ScaleMode.Ionian;
            
            var node = campaign.nodes[nodeIndex];
            return node != null ? node.mode : EarFPS.ScaleMode.Ionian;
        }

        void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CampaignService] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (debugLog) Debug.Log("[CampaignService] Instance created and marked as DontDestroyOnLoad");
            
            // Load save data in Awake to ensure it's available before other scripts' Start() methods
            // This ensures CampaignMapView can query unlock states immediately
            if (campaign != null)
            {
                LoadSave();
            }
        }

        void Start()
        {
            // Save is already loaded in Awake, but reload if campaign wasn't assigned yet
            if (currentSave == null && campaign != null)
            {
                LoadSave();
            }
        }

        /// <summary>
        /// Gets the save file path.
        /// </summary>
        string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, saveFileName);
        }

        /// <summary>
        /// Loads campaign save data from disk, or creates default if missing.
        /// </summary>
        public void LoadSave()
        {
            if (campaign == null)
            {
                Debug.LogError("[CampaignService] Campaign asset is not assigned!");
                return;
            }

            string path = GetSavePath();

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    currentSave = JsonUtility.FromJson<CampaignSave>(json);

                    if (currentSave == null)
                    {
                        Debug.LogWarning("[CampaignService] Failed to parse save file. Creating default.");
                        CreateDefaultSave();
                        return;
                    }

                    // Ensure save data matches campaign structure
                    SyncSaveWithCampaign();

                    // Set mode names from campaign if missing
                    for (int i = 0; i < campaign.nodes.Length && i < currentSave.nodes.Count; i++)
                    {
                        if (string.IsNullOrEmpty(currentSave.nodes[i].mode))
                        {
                            currentSave.nodes[i].mode = campaign.nodes[i].GetModeName();
                        }
                    }

                    if (debugLog)
                    {
                        Debug.Log($"[CampaignService] Loaded save from {path}");
                        // Log unlock states for debugging
                        for (int i = 0; i < currentSave.nodes.Count; i++)
                        {
                            Debug.Log($"[CampaignService] Node {i} ({currentSave.nodes[i].mode}): unlocked={currentSave.nodes[i].unlocked}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CampaignService] Error loading save: {e.Message}");
                    CreateDefaultSave();
                }
            }
            else
            {
                if (debugLog) Debug.Log("[CampaignService] Save file not found. Creating default.");
                CreateDefaultSave();
            }
        }

        /// <summary>
        /// Saves campaign data to disk.
        /// </summary>
        public void SaveToDisk()
        {
            if (currentSave == null)
            {
                Debug.LogWarning("[CampaignService] No save data to write.");
                return;
            }

            try
            {
                string path = GetSavePath();
                string json = JsonUtility.ToJson(currentSave, true);
                File.WriteAllText(path, json);

                if (debugLog) Debug.Log($"[CampaignService] Saved to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignService] Error saving: {e.Message}");
            }
        }

        /// <summary>
        /// Creates a default save with first node unlocked.
        /// </summary>
        void CreateDefaultSave()
        {
            if (campaign == null || campaign.nodes == null)
            {
                Debug.LogError("[CampaignService] Cannot create default save: campaign is null");
                return;
            }

            currentSave = CampaignSave.CreateDefault(campaign.nodes.Length);

            // Set mode names from campaign
            for (int i = 0; i < campaign.nodes.Length && i < currentSave.nodes.Count; i++)
            {
                currentSave.nodes[i].mode = campaign.nodes[i].GetModeName();
            }

            SaveToDisk();
        }

        /// <summary>
        /// Ensures save data structure matches campaign (adds missing nodes, removes extra ones).
        /// Also ensures first node is always unlocked.
        /// </summary>
        void SyncSaveWithCampaign()
        {
            if (campaign == null || campaign.nodes == null) return;

            int campaignNodeCount = campaign.nodes.Length;

            // Remove extra nodes
            while (currentSave.nodes.Count > campaignNodeCount)
            {
                currentSave.nodes.RemoveAt(currentSave.nodes.Count - 1);
            }

            // Add missing nodes
            while (currentSave.nodes.Count < campaignNodeCount)
            {
                int index = currentSave.nodes.Count;
                var nodeData = new NodeSaveData
                {
                    mode = campaign.nodes[index].GetModeName(),
                    unlocked = (index == 0), // Only first node unlocked by default
                    levels = new System.Collections.Generic.List<bool>(6) { false, false, false, false, false, false },
                    winsPerLevel = new System.Collections.Generic.List<int>(6) { 0, 0, 0, 0, 0, 0 }
                };
                currentSave.nodes.Add(nodeData);
            }

            // CRITICAL: Ensure first node is always unlocked (even if save file says otherwise)
            // This ensures the campaign always starts correctly
            if (currentSave.nodes.Count > 0)
            {
                if (!currentSave.nodes[0].unlocked)
                {
                    Debug.LogWarning("[CampaignService] First node was locked in save file. Unlocking it now.");
                    currentSave.nodes[0].unlocked = true;
                    // Save immediately to persist this fix
                    SaveToDisk();
                }
                else
                {
                    if (debugLog) Debug.Log("[CampaignService] First node is correctly unlocked.");
                }
            }
            else
            {
                Debug.LogError("[CampaignService] SyncSaveWithCampaign: No nodes in save data!");
            }
        }

        /// <summary>
        /// Checks if a node is unlocked.
        /// </summary>
        public bool IsNodeUnlocked(int nodeIndex)
        {
            // Safety: If save isn't loaded yet, ensure first node is unlocked (default state)
            if (currentSave == null)
            {
                if (debugLog) Debug.LogWarning("[CampaignService] IsNodeUnlocked called but save is not loaded yet. Returning true for node 0 only.");
                return (nodeIndex == 0); // First node is always unlocked by default
            }
            
            bool isUnlocked = currentSave.IsNodeUnlocked(nodeIndex);
            
            // Safety: Always ensure first node is unlocked
            if (nodeIndex == 0 && !isUnlocked)
            {
                Debug.LogWarning("[CampaignService] Node 0 should always be unlocked. Fixing now.");
                if (currentSave.nodes.Count > 0)
                {
                    currentSave.nodes[0].unlocked = true;
                    SaveToDisk();
                }
                return true;
            }
            
            return isUnlocked;
        }

        /// <summary>
        /// Checks if a level is completed.
        /// </summary>
        public bool IsLevelComplete(int nodeIndex, int levelIndex)
        {
            if (currentSave == null) return false;
            return currentSave.IsLevelComplete(nodeIndex, levelIndex);
        }

        /// <summary>
        /// Gets the number of completed levels for a node.
        /// </summary>
        public int GetCompletedLevelCount(int nodeIndex)
        {
            if (currentSave == null) return 0;
            return currentSave.GetCompletedLevelCount(nodeIndex);
        }

        /// <summary>
        /// Gets the next incomplete level index for a node, or -1 if all are complete.
        /// </summary>
        public int GetNextIncompleteLevelIndex(int nodeIndex)
        {
            if (currentSave == null) return -1;
            return currentSave.GetNextIncompleteLevelIndex(nodeIndex);
        }

        /// <summary>
        /// Marks a level as completed and checks for node unlock.
        /// Returns the index of the newly unlocked node, or -1 if no node was unlocked.
        /// </summary>
        public int MarkLevelComplete(int nodeIndex, int levelIndex)
        {
            if (currentSave == null)
            {
                Debug.LogError("[CampaignService] Cannot mark level complete: save data is null");
                return -1;
            }

            currentSave.MarkLevelComplete(nodeIndex, levelIndex);

            // Check if 3+ levels are completed in this node
            int completedCount = currentSave.GetCompletedLevelCount(nodeIndex);
            if (completedCount >= 3)
            {
                // Unlock next node
                int nextNodeIndex = nodeIndex + 1;
                if (nextNodeIndex < campaign.nodes.Length)
                {
                    if (!currentSave.IsNodeUnlocked(nextNodeIndex))
                    {
                        currentSave.UnlockNode(nextNodeIndex);
                        if (debugLog) Debug.Log($"[CampaignService] Unlocked node {nextNodeIndex} ({campaign.nodes[nextNodeIndex].GetModeName()})");
                        SaveToDisk();
                        return nextNodeIndex; // Return the newly unlocked node index
                    }
                }
            }

            SaveToDisk();
            return -1; // No node was unlocked
        }

        /// <summary>
        /// Resets all progress to default state.
        /// </summary>
        [ContextMenu("Debug: Reset Progress")]
        public void ResetProgress()
        {
            if (debugLog) Debug.Log("[CampaignService] Resetting progress...");
            CreateDefaultSave();
            currentNodeIndex = -1;
            currentLevelIndex = -1;
        }

        /// <summary>
        /// Unlocks all nodes (debug/testing).
        /// </summary>
        [ContextMenu("Debug: Unlock All Nodes")]
        public void UnlockAll()
        {
            if (currentSave == null)
            {
                Debug.LogWarning("[CampaignService] Cannot unlock all: save data is null");
                return;
            }

            if (debugLog) Debug.Log("[CampaignService] Unlocking all nodes...");

            for (int i = 0; i < currentSave.nodes.Count; i++)
            {
                currentSave.UnlockNode(i);
            }

            SaveToDisk();
        }

        /// <summary>
        /// Auto-completes 3 levels per node (debug/testing).
        /// </summary>
        [ContextMenu("Debug: Auto Complete All Levels (3 per node)")]
        public void AutoCompleteAllLevels()
        {
            if (currentSave == null)
            {
                Debug.LogWarning("[CampaignService] Cannot auto-complete: save data is null");
                return;
            }

            if (debugLog) Debug.Log("[CampaignService] Auto-completing 3 levels per node...");

            for (int nodeIdx = 0; nodeIdx < currentSave.nodes.Count; nodeIdx++)
            {
                // Complete first 3 levels
                for (int levelIdx = 0; levelIdx < 3 && levelIdx < 6; levelIdx++)
                {
                    currentSave.MarkLevelComplete(nodeIdx, levelIdx);
                }
                // Unlock node
                currentSave.UnlockNode(nodeIdx);
            }

            SaveToDisk();
        }

        /// <summary>
        /// Debug method to start a level from the map (for testing).
        /// </summary>
        [ContextMenu("Debug: Start Level 0-0")]
        public void DebugStartLevel00()
        {
            StartFromMap(0, 0);
        }

        /// <summary>
        /// Debug method to start any level regardless of completion status (for testing).
        /// Bypasses order enforcement.
        /// </summary>
        public void StartLevelDebug(int nodeIndex, int levelIndex)
        {
            if (campaign == null || campaign.nodes == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: campaign is null");
                return;
            }

            if (nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
            {
                Debug.LogError($"[CampaignService] Invalid node index: {nodeIndex}");
                return;
            }

            // Debug mode: skip unlock and order validation
            var node = campaign.nodes[nodeIndex];
            if (levelIndex < 0 || levelIndex >= node.levels.Length || node.levels[levelIndex] == null)
            {
                Debug.LogError($"[CampaignService] Invalid level index: {levelIndex} for node {nodeIndex}");
                return;
            }

            var level = node.levels[levelIndex];

            // Set current node and level
            currentNodeIndex = nodeIndex;
            currentLevelIndex = levelIndex;
            winsThisLevel = 0;

            // Find or cache references
            if (difficultyApplier == null)
                difficultyApplier = FindFirstObjectByType<DifficultyProfileApplier>();
            if (dictationController == null)
                dictationController = FindFirstObjectByType<EarFPS.MelodicDictationController>();

            if (difficultyApplier == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: DifficultyProfileApplier not found");
                return;
            }

            if (dictationController == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: MelodicDictationController not found");
                return;
            }

            // Apply the level's profile
            if (level.profile != null)
            {
                ApplyLevelProfile(level);
                ConfigureControllerForCampaign(level);
                
                // Hide map and level picker when level starts
                HideCampaignUI();
                
                // Start the first round (now public method)
                if (dictationController != null)
                {
                    dictationController.StartRound();
                    if (debugLog) Debug.Log("[CampaignService] Started first round");
                }
            }
            else
            {
                Debug.LogError($"[CampaignService] Level {levelIndex} in node {nodeIndex} has no profile assigned");
                return;
            }

            UpdateHUD();

            if (debugLog) Debug.Log($"[CampaignService] [DEBUG] Started level: Node {nodeIndex} ({node.GetModeName()}), Level {levelIndex} ({level.title})");
        }

        [ContextMenu("Debug: Start Level 0-0 (Force)")]
        public void DebugStartLevel00Force()
        {
            StartLevelDebug(0, 0);
        }

        /// <summary>
        /// Gets the next incomplete level index for the current node.
        /// </summary>
        public int GetCurrentNodeNextLevel()
        {
            if (currentNodeIndex < 0) return -1;
            return GetNextIncompleteLevelIndex(currentNodeIndex);
        }

        /// <summary>
        /// Starts the next level in the current node, or returns to map if no next level.
        /// </summary>
        public void StartNextLevel()
        {
            if (currentNodeIndex < 0)
            {
                Debug.LogWarning("[CampaignService] Cannot start next level: no current node");
                return;
            }

            int nextLevel = GetNextIncompleteLevelIndex(currentNodeIndex);
            if (nextLevel >= 0)
            {
                StartFromMap(currentNodeIndex, nextLevel);
            }
            else
            {
                if (debugLog) Debug.Log("[CampaignService] No next level in current node. Return to map.");
                // Return to map (handled by UI in later stages)
            }
        }

        /// <summary>
        /// Starts a level from the campaign map. Validates unlock status and level order, then applies the level's profile.
        /// </summary>
        public void StartFromMap(int nodeIndex, int levelIndex)
        {
            if (campaign == null || campaign.nodes == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: campaign is null");
                return;
            }

            if (nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
            {
                Debug.LogError($"[CampaignService] Invalid node index: {nodeIndex}");
                return;
            }

            // Validate node is unlocked
            if (!IsNodeUnlocked(nodeIndex))
            {
                Debug.LogWarning($"[CampaignService] Cannot start level: Node {nodeIndex} is not unlocked");
                return;
            }

            // Validate level index - allow starting if:
            // 1. It's the next incomplete level (normal progression), OR
            // 2. It's already completed (replay scenario)
            int nextIncompleteLevel = GetNextIncompleteLevelIndex(nodeIndex);
            bool isLevelComplete = IsLevelComplete(nodeIndex, levelIndex);
            
            if (levelIndex != nextIncompleteLevel && !isLevelComplete)
            {
                Debug.LogWarning($"[CampaignService] Cannot start level {levelIndex}: Next incomplete level is {nextIncompleteLevel}");
                return;
            }
            
            // If replaying a completed level, log it for debugging
            if (isLevelComplete && levelIndex != nextIncompleteLevel)
            {
                if (debugLog) Debug.Log($"[CampaignService] Replaying completed level {levelIndex} (next incomplete is {nextIncompleteLevel})");
            }

            var node = campaign.nodes[nodeIndex];
            if (levelIndex < 0 || levelIndex >= node.levels.Length || node.levels[levelIndex] == null)
            {
                Debug.LogError($"[CampaignService] Invalid level index: {levelIndex} for node {nodeIndex}");
                return;
            }

            var level = node.levels[levelIndex];

            // Set current node and level
            currentNodeIndex = nodeIndex;
            currentLevelIndex = levelIndex;
            winsThisLevel = 0;

            // Find or cache references
            if (difficultyApplier == null)
                difficultyApplier = FindFirstObjectByType<DifficultyProfileApplier>();
            if (dictationController == null)
                dictationController = FindFirstObjectByType<EarFPS.MelodicDictationController>();

            if (difficultyApplier == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: DifficultyProfileApplier not found");
                return;
            }

            if (dictationController == null)
            {
                Debug.LogError("[CampaignService] Cannot start level: MelodicDictationController not found");
                return;
            }

            // Apply the level's profile
            if (level.profile != null)
            {
                // Apply profile using DifficultyProfileApplier
                ApplyLevelProfile(level);
                
                // Configure controller for campaign mode
                ConfigureControllerForCampaign(level);
                
                // Hide map and level picker when level starts
                HideCampaignUI();
                
                // Start the first round (now public method)
                if (dictationController != null)
                {
                    dictationController.StartRound();
                    if (debugLog) Debug.Log("[CampaignService] Started first round");
                }
            }
            else
            {
                Debug.LogError($"[CampaignService] Level {levelIndex} in node {nodeIndex} has no profile assigned");
                return;
            }

            // Update HUD if available (placeholder for now)
            UpdateHUD();

            if (debugLog) Debug.Log($"[CampaignService] Started level: Node {nodeIndex} ({node.GetModeName()}), Level {levelIndex} ({level.title})");
        }

        /// <summary>
        /// Applies a DifficultyProfile directly to the applier's targets.
        /// </summary>
        /// <param name="level">The dictation level containing the profile and optional mode override</param>
        void ApplyLevelProfile(DictationLevel level)
        {
            if (difficultyApplier == null || level == null || level.profile == null) return;
            
            // Determine if we should use the mode override
            EarFPS.ScaleMode? modeOverride = null;
            if (level.useModeOverride)
            {
                modeOverride = level.modeOverride;
                if (debugLog) Debug.Log($"[CampaignService] Using mode override: {modeOverride.Value} for level {level.title}");
            }
            
            difficultyApplier.ApplyProfile(level.profile, modeOverride);
        }

        /// <summary>
        /// Configures the controller for campaign mode with the level's settings.
        /// </summary>
        void ConfigureControllerForCampaign(DictationLevel level)
        {
            if (dictationController == null || level == null) return;

            // Set wins required for this level (uses public method)
            dictationController.SetWinsRequiredForCampaign(level.roundsToWin);
        }

        /// <summary>
        /// Hides campaign UI (map and level picker) when starting gameplay.
        /// </summary>
        void HideCampaignUI()
        {
            // Use reflection to find and hide CampaignMapView and CampaignLevelPicker
            // This avoids assembly reference issues
            var mapViewType = System.Type.GetType("EarFPS.CampaignMapView, Assembly-CSharp");
            if (mapViewType != null)
            {
                var mapView = FindFirstObjectByType(mapViewType);
                if (mapView != null)
                {
                    var hideMethod = mapViewType.GetMethod("Hide", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (hideMethod != null)
                    {
                        hideMethod.Invoke(mapView, null);
                        if (debugLog) Debug.Log("[CampaignService] Hidden campaign map");
                    }
                    else
                    {
                        // Fallback: just deactivate
                        ((MonoBehaviour)mapView).gameObject.SetActive(false);
                    }
                }
            }

            var levelPickerType = System.Type.GetType("EarFPS.CampaignLevelPicker, Assembly-CSharp");
            if (levelPickerType != null)
            {
                var levelPicker = FindFirstObjectByType(levelPickerType);
                if (levelPicker != null)
                {
                    var hideMethod = levelPickerType.GetMethod("Hide", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (hideMethod != null)
                    {
                        hideMethod.Invoke(levelPicker, null);
                        if (debugLog) Debug.Log("[CampaignService] Hidden level picker");
                    }
                    else
                    {
                        // Fallback: just deactivate
                        ((MonoBehaviour)levelPicker).gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Updates HUD with current mode and level.
        /// </summary>
        void UpdateHUD()
        {
            // CampaignHUD will update itself automatically via its Update() method
            // This method is kept for potential future use or manual refresh calls

            if (debugLog)
            {
                var node = CurrentNode;
                var level = CurrentLevel;
                if (node != null && level != null)
                {
                    Debug.Log($"[CampaignService] HUD: Mode: {node.GetModeName()} — Level: {currentLevelIndex + 1}");
                }
            }
        }

        /// <summary>
        /// Result structure for RecordLevelWin operation.
        /// </summary>
        public struct LevelWinResult
        {
            public bool levelComplete;
            public int newlyUnlockedNodeIndex; // -1 if no node was unlocked
        }

        /// <summary>
        /// Records a win for the current level. Called by MelodicDictationController when a round is completed.
        /// Returns a LevelWinResult containing level completion status and newly unlocked node index.
        /// </summary>
        public LevelWinResult RecordLevelWin()
        {
            LevelWinResult result = new LevelWinResult
            {
                levelComplete = false,
                newlyUnlockedNodeIndex = -1
            };

            if (currentNodeIndex < 0 || currentLevelIndex < 0)
            {
                Debug.LogWarning("[CampaignService] RecordLevelWin called but no level is active (currentNodeIndex=" + currentNodeIndex + ", currentLevelIndex=" + currentLevelIndex + ")");
                return result;
            }

            winsThisLevel++;

            var level = CurrentLevel;
            if (level == null)
            {
                Debug.LogError("[CampaignService] Cannot record win: CurrentLevel is null");
                return result;
            }

            if (debugLog) Debug.Log($"[CampaignService] Round win recorded: {winsThisLevel}/{level.roundsToWin}");

            // Check if level is complete
            if (winsThisLevel >= level.roundsToWin)
            {
                if (debugLog) Debug.Log($"[CampaignService] Level completed! Wins: {winsThisLevel}/{level.roundsToWin}");
                
                // Mark level as complete and get newly unlocked node index
                result.newlyUnlockedNodeIndex = MarkLevelComplete(currentNodeIndex, currentLevelIndex);
                result.levelComplete = true;
                
                // Reset wins counter
                winsThisLevel = 0;

                // Notify that level is complete (will be handled by UI in later stages)
                OnLevelCompleted();
            }

            return result;
        }

        /// <summary>
        /// Called when a level is completed. Handles progression and UI.
        /// </summary>
        void OnLevelCompleted()
        {
            // Check if there's a next level in the current node
            int nextLevel = GetNextIncompleteLevelIndex(currentNodeIndex);
            
            if (nextLevel >= 0)
            {
                if (debugLog) Debug.Log($"[CampaignService] Next level available in current node: {nextLevel}");
                // Continue will start next level (handled by UI)
            }
            else
            {
                if (debugLog) Debug.Log("[CampaignService] All levels in current node are complete");
                // Return to map (handled by UI)
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

