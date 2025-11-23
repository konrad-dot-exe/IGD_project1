// CampaignHUD.cs — Displays campaign mode and level information during play
using UnityEngine;
using TMPro;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// HUD component that displays the current campaign mode and level during gameplay.
    /// </summary>
    public class CampaignHUD : MonoBehaviour
    {
        [Header("HUD Elements")]
        [Tooltip("Text element that displays 'Mode: {Name} — Level {N}'")]
        [SerializeField] TextMeshProUGUI modeLevelText;

        [Header("Settings")]
        [Tooltip("Hide HUD when not in campaign mode")]
        [SerializeField] bool hideWhenNotInCampaign = true;

        private CampaignService campaignService;
        private bool isVisible = false;

        void Start()
        {
            campaignService = CampaignService.Instance;
            UpdateDisplay();
        }

        void Update()
        {
            // Update display if campaign state changes
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if (campaignService == null)
            {
                campaignService = CampaignService.Instance;
            }

            if (campaignService == null)
            {
                // No campaign service - hide if configured to do so
                if (hideWhenNotInCampaign && modeLevelText != null)
                {
                    modeLevelText.gameObject.SetActive(false);
                    isVisible = false;
                }
                return;
            }

            var currentNode = campaignService.CurrentNode;
            var currentLevel = campaignService.CurrentLevel;
            int currentLevelIndex = campaignService.CurrentLevelIndex;

            if (currentNode != null && currentLevel != null && currentLevelIndex >= 0)
            {
                // Show mode and level
                string modeName = currentNode.GetModeName();
                int displayLevel = currentLevelIndex + 1; // 1-based for display
                
                if (modeLevelText != null)
                {
                    modeLevelText.text = $"Mode: {modeName} — Level: {displayLevel}";
                    modeLevelText.gameObject.SetActive(true);
                    isVisible = true;
                }
            }
            else
            {
                // No active campaign level - hide if configured to do so
                if (hideWhenNotInCampaign && modeLevelText != null)
                {
                    modeLevelText.gameObject.SetActive(false);
                    isVisible = false;
                }
            }
        }

        /// <summary>
        /// Manually refresh the display (called externally if needed).
        /// </summary>
        public void Refresh()
        {
            UpdateDisplay();
        }
    }
}

