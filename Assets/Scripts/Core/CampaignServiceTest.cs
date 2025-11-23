// Assets/Scripts/Core/CampaignServiceTester.cs
using UnityEngine;

namespace Sonoria.Dictation
{
    public class CampaignServiceTester : MonoBehaviour
    {
        [ContextMenu("Test: Complete Level 0-0")]
        void TestCompleteLevel00()
        {
            if (CampaignService.Instance != null)
            {
                CampaignService.Instance.MarkLevelComplete(0, 0);
                Debug.Log("Completed Node 0, Level 0");
            }
        }

        [ContextMenu("Test: Complete Level 0-1")]
        void TestCompleteLevel01()
        {
            if (CampaignService.Instance != null)
            {
                CampaignService.Instance.MarkLevelComplete(0, 1);
                Debug.Log("Completed Node 0, Level 1");
            }
        }

        [ContextMenu("Test: Complete Level 0-2")]
        void TestCompleteLevel02()
        {
            if (CampaignService.Instance != null)
            {
                CampaignService.Instance.MarkLevelComplete(0, 2);
                Debug.Log("Completed Node 0, Level 2 (should unlock Node 1)");
            }
        }

        [ContextMenu("Test: Check Node 1 Unlocked")]
        void TestCheckNode1Unlocked()
        {
            if (CampaignService.Instance != null)
            {
                bool unlocked = CampaignService.Instance.IsNodeUnlocked(1);
                Debug.Log($"Node 1 is unlocked: {unlocked}");
            }
        }

        [ContextMenu("Test: Check Completed Count Node 0")]
        void TestCheckCompletedCount()
        {
            if (CampaignService.Instance != null)
            {
                int count = CampaignService.Instance.GetCompletedLevelCount(0);
                Debug.Log($"Node 0 completed levels: {count}");
            }
        }
    }
}