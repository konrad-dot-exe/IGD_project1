// Test script (create in Editor folder if needed)
using UnityEngine;
using Sonoria.Dictation;

public class CampaignSaveTest : MonoBehaviour
{
    void Start()
    {
        // Test default creation
        var save = CampaignSave.CreateDefault(6);
        Debug.Log($"Created default save with {save.nodes.Count} nodes");
        
        // Test node unlock
        Debug.Log($"Node 0 unlocked: {save.IsNodeUnlocked(0)}"); // Should be true
        Debug.Log($"Node 1 unlocked: {save.IsNodeUnlocked(1)}"); // Should be false
        
        // Test level completion
        save.MarkLevelComplete(0, 0);
        Debug.Log($"Level 0-0 complete: {save.IsLevelComplete(0, 0)}"); // Should be true
        Debug.Log($"Completed count: {save.GetCompletedLevelCount(0)}"); // Should be 1
        
        // Test next incomplete level
        int next = save.GetNextIncompleteLevelIndex(0);
        Debug.Log($"Next incomplete level: {next}"); // Should be 1
    }
}
