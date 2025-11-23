// CampaignDebugPanel.cs — Debug panel for campaign testing
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// Debug panel for campaign testing (dev only).
    /// Provides buttons to reset progress, unlock all nodes, and auto-complete levels.
    /// </summary>
    public class CampaignDebugPanel : MonoBehaviour
    {
        [Header("Debug Buttons")]
        [Tooltip("Reset Progress button")]
        [SerializeField] Button btnResetProgress;

        [Tooltip("Unlock All Nodes button")]
        [SerializeField] Button btnUnlockAll;

        [Tooltip("Auto Complete All Levels button")]
        [SerializeField] Button btnAutoComplete;

        [Header("Settings")]
        [Tooltip("Show this panel only in development builds")]
        [SerializeField] bool devOnly = true;

        private CampaignService campaignService;

        void Start()
        {
            campaignService = CampaignService.Instance;

            // Hide panel if devOnly and not in development build
            if (devOnly && !Debug.isDebugBuild)
            {
                gameObject.SetActive(false);
                return;
            }

            // Setup button listeners
            if (btnResetProgress != null)
                btnResetProgress.onClick.AddListener(OnResetProgress);

            if (btnUnlockAll != null)
                btnUnlockAll.onClick.AddListener(OnUnlockAll);

            if (btnAutoComplete != null)
                btnAutoComplete.onClick.AddListener(OnAutoComplete);
        }

        void OnResetProgress()
        {
            if (campaignService == null)
            {
                Debug.LogWarning("[CampaignDebugPanel] CampaignService not found!");
                return;
            }

            campaignService.ResetProgress();
            Debug.Log("[CampaignDebugPanel] Progress reset!");
        }

        void OnUnlockAll()
        {
            if (campaignService == null)
            {
                Debug.LogWarning("[CampaignDebugPanel] CampaignService not found!");
                return;
            }

            campaignService.UnlockAll();
            Debug.Log("[CampaignDebugPanel] All nodes unlocked!");
        }

        void OnAutoComplete()
        {
            if (campaignService == null)
            {
                Debug.LogWarning("[CampaignDebugPanel] CampaignService not found!");
                return;
            }

            campaignService.AutoCompleteAllLevels();
            Debug.Log("[CampaignDebugPanel] All levels auto-completed!");
        }
    }
}

