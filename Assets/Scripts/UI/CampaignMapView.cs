// CampaignMapView.cs — Displays campaign map with mode nodes and unlock states
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// UI component that displays the campaign map with mode nodes.
    /// Shows lock/unlock states and handles node selection.
    /// </summary>
    public class CampaignMapView : MonoBehaviour
    {
        [Header("Map UI")]
        [Tooltip("Parent container for mode node buttons")]
        [SerializeField] Transform nodeContainer;

        [Tooltip("Prefab for a mode node button")]
        [SerializeField] GameObject nodeButtonPrefab;

        [Tooltip("Prefab for a node progress bar (will be created above each node button)")]
        [SerializeField] GameObject progressBarPrefab;

        [Header("Progress Bar Settings")]
        [Tooltip("Vertical offset of progress bars above node buttons (positive = above, negative = below)")]
        [SerializeField] float progressBarVerticalOffset = 50f;
        
        [Tooltip("Opacity for progress bars on locked nodes (0.0 = transparent, 1.0 = opaque)")]
        [Range(0f, 1f)]
        [SerializeField] float lockedProgressBarOpacity = 0.3f;

        [Header("Level Picker")]
        [Tooltip("Reference to the level picker UI (will be shown when a node is clicked)")]
        [SerializeField] CampaignLevelPicker levelPicker;

        [Header("Visual Settings")]
        [Tooltip("Color for unlocked nodes")]
        [SerializeField] Color unlockedColor = Color.white;

        [Tooltip("Color for locked nodes")]
        [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [Header("Navigation")]
        [Tooltip("Back button to return to main menu")]
        [SerializeField] Button backButton;

        [Tooltip("Main menu scene name")]
        [SerializeField] string mainMenuSceneName = "MainMenu";

        private CampaignService campaignService;
        private Button[] nodeButtons;
        private NodeProgressBar[] progressBars;

        void Awake()
        {
            // Auto-enable if disabled in editor (for workflow convenience)
            // Must be in Awake() so it's active before other scripts' Start() methods run
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        void Start()
        {
            campaignService = CampaignService.Instance;
            if (campaignService == null)
            {
                Debug.LogError("[CampaignMapView] CampaignService not found!");
                return;
            }

            // Disable raycast target on background image to allow clicks through to buttons
            var backgroundImage = GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
                Debug.Log("[CampaignMapView] Disabled raycast target on background image");
            }

            // Setup back button
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackToMainMenuClicked);
            }

            BuildMap();
        }

        /// <summary>
        /// Builds the map UI with all mode nodes.
        /// </summary>
        void BuildMap()
        {
            if (campaignService == null || campaignService.Campaign == null)
            {
                Debug.LogError("[CampaignMapView] Cannot build map: CampaignService or Campaign is null");
                return;
            }

            var campaign = campaignService.Campaign;
            if (campaign.nodes == null || campaign.nodes.Length == 0)
            {
                Debug.LogWarning("[CampaignMapView] Campaign has no nodes");
                return;
            }

            // Clear existing buttons
            if (nodeContainer != null)
            {
                foreach (Transform child in nodeContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            nodeButtons = new Button[campaign.nodes.Length];
            progressBars = new NodeProgressBar[campaign.nodes.Length];

            // Create button for each node
            for (int i = 0; i < campaign.nodes.Length; i++)
            {
                var node = campaign.nodes[i];
                if (node == null) continue;

                GameObject buttonObj;
                if (nodeButtonPrefab != null && nodeContainer != null)
                {
                    buttonObj = Instantiate(nodeButtonPrefab, nodeContainer);
                    
                    // Ensure the instantiated button has proper setup
                    Button prefabButton = buttonObj.GetComponent<Button>();
                    if (prefabButton == null)
                        prefabButton = buttonObj.AddComponent<Button>();
                    
                    // Ensure button's image has raycast target enabled
                    var prefabImage = buttonObj.GetComponent<Image>();
                    if (prefabImage != null)
                    {
                        prefabImage.raycastTarget = true;
                    }
                    
                    Debug.Log($"[CampaignMapView] Instantiated button prefab for node {i}, interactable={prefabButton.interactable}");
                }
                else
                {
                    // Fallback: create a simple button
                    buttonObj = new GameObject($"NodeButton_{i}");
                    if (nodeContainer != null)
                        buttonObj.transform.SetParent(nodeContainer, false);
                    
                    var rect = buttonObj.AddComponent<RectTransform>();
                    
                    // Check if parent has a layout group - if so, use LayoutElement for preferred size
                    var layoutGroup = nodeContainer != null ? nodeContainer.GetComponent<UnityEngine.UI.LayoutGroup>() : null;
                    if (layoutGroup != null)
                    {
                        // Parent has layout group - use LayoutElement for size control
                        var layoutElement = buttonObj.AddComponent<UnityEngine.UI.LayoutElement>();
                        layoutElement.preferredWidth = 200;
                        layoutElement.preferredHeight = 100;
                        layoutElement.flexibleWidth = 0;
                        layoutElement.flexibleHeight = 0;
                        
                        // Set anchors for layout group compatibility
                        rect.anchorMin = new Vector2(0, 0.5f);
                        rect.anchorMax = new Vector2(0, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.sizeDelta = new Vector2(200, 100);
                    }
                    else
                    {
                        // No layout group - use direct sizing
                        rect.anchorMin = new Vector2(0, 0.5f);
                        rect.anchorMax = new Vector2(0, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.sizeDelta = new Vector2(200, 100);
                    }
                    
                    var image = buttonObj.AddComponent<Image>();
                    image.color = unlockedColor;
                    image.raycastTarget = true; // Ensure buttons are clickable
                    
                    buttonObj.AddComponent<Button>();
                    var textObj = new GameObject("Text");
                    textObj.transform.SetParent(buttonObj.transform, false);
                    var textRect = textObj.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                    var text = textObj.AddComponent<TextMeshProUGUI>();
                    text.text = node.GetModeName();
                    text.alignment = TextAlignmentOptions.Center;
                    text.color = Color.black;
                    text.raycastTarget = false; // Text shouldn't block clicks
                }

                Button button = buttonObj.GetComponent<Button>();
                if (button == null)
                    button = buttonObj.AddComponent<Button>();

                // Remove any existing listeners to prevent duplicates
                button.onClick.RemoveAllListeners();
                
                int nodeIndex = i; // Capture for closure
                button.onClick.AddListener(() => OnNodeClicked(nodeIndex));

                // Set button appearance based on unlock state
                bool isUnlocked = campaignService.IsNodeUnlocked(nodeIndex);
                Debug.Log($"[CampaignMapView] BuildMap: Node {nodeIndex} ({node.GetModeName()}) - IsNodeUnlocked returned: {isUnlocked}, CampaignService exists: {campaignService != null}");
                UpdateNodeButton(button, node, isUnlocked);

                nodeButtons[i] = button;

                // Create progress bar for this node
                CreateProgressBar(buttonObj, i);
                
                // Set initial progress bar opacity based on unlock state
                UpdateProgressBarOpacity(i, isUnlocked);
            }

            RefreshMap();
        }

        /// <summary>
        /// Creates a progress bar for a node button.
        /// </summary>
        void CreateProgressBar(GameObject buttonObj, int nodeIndex)
        {
            if (buttonObj == null) return;

            NodeProgressBar progressBar = null;

            // Try to find existing progress bar in the button prefab
            progressBar = buttonObj.GetComponentInChildren<NodeProgressBar>();

            // If not found and we have a prefab, create from prefab
            if (progressBar == null && progressBarPrefab != null)
            {
                GameObject progressBarObj = Instantiate(progressBarPrefab, buttonObj.transform);
                progressBar = progressBarObj.GetComponent<NodeProgressBar>();
                
                if (progressBar == null)
                {
                    Debug.LogWarning($"[CampaignMapView] Progress bar prefab doesn't have NodeProgressBar component! Creating default progress bar for node {nodeIndex}.");
                    progressBar = progressBarObj.AddComponent<NodeProgressBar>();
                }
                
                // Always apply positioning to ensure offset is correct
                var progressRect = progressBarObj.GetComponent<RectTransform>();
                if (progressRect != null)
                {
                    // Set anchors and pivot for positioning above button
                    progressRect.anchorMin = new Vector2(0.5f, 1f);
                    progressRect.anchorMax = new Vector2(0.5f, 1f);
                    progressRect.pivot = new Vector2(0.5f, 1f);
                    // Apply the configurable vertical offset
                    progressRect.anchoredPosition = new Vector2(0, progressBarVerticalOffset);
                    // Only set size if it's zero (prefab might already have size set)
                    if (progressRect.sizeDelta == Vector2.zero)
                    {
                        progressRect.sizeDelta = new Vector2(100, 600);
                    }
                }
            }

            // If still no progress bar, create one programmatically
            if (progressBar == null)
            {
                GameObject progressBarObj = new GameObject("ProgressBar");
                progressBarObj.transform.SetParent(buttonObj.transform, false);
                progressBar = progressBarObj.AddComponent<NodeProgressBar>();

                // Set up RectTransform for progress bar (position above button)
                var progressRect = progressBarObj.GetComponent<RectTransform>();
                if (progressRect == null)
                    progressRect = progressBarObj.AddComponent<RectTransform>();

                // Position progress bar above the button
                // Anchor to top-center of button, pivot at top-center
                progressRect.anchorMin = new Vector2(0.5f, 1f);
                progressRect.anchorMax = new Vector2(0.5f, 1f);
                progressRect.pivot = new Vector2(0.5f, 1f); // Pivot at top-center
                progressRect.anchoredPosition = new Vector2(0, progressBarVerticalOffset); // Configurable offset above button's top edge
                progressRect.sizeDelta = new Vector2(100, 600); // Default size
                
                // Set the progress bar's initial progress to 0 (will be updated by RefreshMap)
                progressBar.SetProgressImmediate(0f);
            }

            // Ensure progress bar is positioned above the button visually
            // Set as last sibling so it renders on top of button (for visual clarity, though positioning handles the actual placement)
            progressBar.transform.SetAsLastSibling();

            // Store reference
            progressBars[nodeIndex] = progressBar;
        }

        /// <summary>
        /// Updates a node button's appearance based on unlock state.
        /// </summary>
        void UpdateNodeButton(Button button, ModeNode node, bool isUnlocked)
        {
            if (button == null) return;

            // Set interactable state FIRST
            button.interactable = isUnlocked;
            
            Debug.Log($"[CampaignMapView] Node {node.GetModeName()}: isUnlocked={isUnlocked}, interactable={button.interactable}");

            // Ensure unlocked color has full alpha
            Color finalUnlockedColor = unlockedColor;
            if (isUnlocked)
            {
                finalUnlockedColor.a = 1.0f; // Force full opacity for unlocked buttons
            }

            // Update button ColorBlock
            var colors = button.colors;
            if (isUnlocked)
            {
                // Unlocked: use full-opacity unlocked color for normal state
                colors.normalColor = finalUnlockedColor;
                colors.highlightedColor = finalUnlockedColor;
                colors.pressedColor = new Color(finalUnlockedColor.r * 0.8f, finalUnlockedColor.g * 0.8f, finalUnlockedColor.b * 0.8f, 1.0f);
                colors.selectedColor = finalUnlockedColor;
            }
            else
            {
                // Locked: use locked color
                colors.normalColor = lockedColor;
                colors.highlightedColor = lockedColor;
                colors.pressedColor = lockedColor;
                colors.selectedColor = lockedColor;
            }
            colors.disabledColor = lockedColor; // Disabled state uses locked color
            button.colors = colors;

            // Update text
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = node.GetModeName();
                // if (!isUnlocked)
                // {
                //     text.text += "\n🔒";
                // }
            }

            // Update image directly - ensure full opacity when unlocked
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                if (isUnlocked)
                {
                    // Unlocked: use full-opacity color
                    image.color = finalUnlockedColor;
                }
                else
                {
                    // Locked: use locked color (with reduced opacity)
                    image.color = lockedColor;
                }
                Debug.Log($"[CampaignMapView] Button image color set to: {image.color} (RGBA: {image.color.r}, {image.color.g}, {image.color.b}, {image.color.a}), raycastTarget={image.raycastTarget}, interactable={button.interactable}");
            }
        }

        /// <summary>
        /// Updates the opacity of a progress bar based on node unlock state.
        /// </summary>
        void UpdateProgressBarOpacity(int nodeIndex, bool isUnlocked)
        {
            if (progressBars == null || nodeIndex < 0 || nodeIndex >= progressBars.Length)
                return;

            if (progressBars[nodeIndex] == null)
                return;

            // Set opacity based on unlock state
            float opacity = isUnlocked ? 1.0f : lockedProgressBarOpacity;
            progressBars[nodeIndex].SetOpacity(opacity);
        }

        /// <summary>
        /// Updates the progress bar for a node based on completion percentage.
        /// </summary>
        void UpdateProgressBar(int nodeIndex, bool animate = true)
        {
            if (progressBars == null || nodeIndex < 0 || nodeIndex >= progressBars.Length)
                return;

            if (progressBars[nodeIndex] == null)
                return;

            if (campaignService == null || campaignService.Campaign == null)
                return;

            var campaign = campaignService.Campaign;
            if (nodeIndex >= campaign.nodes.Length || campaign.nodes[nodeIndex] == null)
                return;

            // Calculate completion percentage
            int completedCount = campaignService.GetCompletedLevelCount(nodeIndex);
            int totalLevels = 6; // Each node has 6 levels
            float progress = totalLevels > 0 ? (float)completedCount / totalLevels : 0f;

            // Update progress bar (will animate if animate is true)
            progressBars[nodeIndex].SetProgress(progress, animate);
        }

        /// <summary>
        /// Called when a node button is clicked.
        /// </summary>
        void OnNodeClicked(int nodeIndex)
        {
            Debug.Log($"[CampaignMapView] OnNodeClicked called for node index {nodeIndex}");
            
            if (campaignService == null)
            {
                Debug.LogError("[CampaignMapView] CampaignService is null!");
                return;
            }

            if (!campaignService.IsNodeUnlocked(nodeIndex))
            {
                Debug.LogWarning($"[CampaignMapView] Node {nodeIndex} is locked!");
                return;
            }

            Debug.Log($"[CampaignMapView] Node {nodeIndex} is unlocked. Opening level picker...");

            // Show level picker for this node
            if (levelPicker != null)
            {
                levelPicker.ShowForNode(nodeIndex);
                gameObject.SetActive(false); // Hide map view
                Debug.Log("[CampaignMapView] Level picker shown, map hidden");
            }
            else
            {
                Debug.LogWarning("[CampaignMapView] Level picker not assigned! Cannot show levels.");
            }
        }

        /// <summary>
        /// Refreshes the map display (updates unlock states and progress bars).
        /// </summary>
        public void RefreshMap()
        {
            if (campaignService == null || campaignService.Campaign == null) return;

            var campaign = campaignService.Campaign;
            if (nodeButtons == null || nodeButtons.Length != campaign.nodes.Length) return;

            for (int i = 0; i < nodeButtons.Length && i < campaign.nodes.Length; i++)
            {
                if (nodeButtons[i] != null && campaign.nodes[i] != null)
                {
                    bool isUnlocked = campaignService.IsNodeUnlocked(i);
                    Debug.Log($"[CampaignMapView] RefreshMap: Node {i} ({campaign.nodes[i].GetModeName()}) - IsNodeUnlocked returned: {isUnlocked}");
                    UpdateNodeButton(nodeButtons[i], campaign.nodes[i], isUnlocked);
                    
                    // Update progress bar position (in case offset changed), opacity, and progress value
                    UpdateProgressBarPosition(i);
                    UpdateProgressBarOpacity(i, isUnlocked);
                    UpdateProgressBar(i, animate: true);
                }
            }
        }

        /// <summary>
        /// Updates the position of a progress bar based on the current vertical offset setting.
        /// </summary>
        void UpdateProgressBarPosition(int nodeIndex)
        {
            if (progressBars == null || nodeIndex < 0 || nodeIndex >= progressBars.Length)
                return;

            if (progressBars[nodeIndex] == null)
                return;

            var progressRect = progressBars[nodeIndex].GetComponent<RectTransform>();
            if (progressRect != null)
            {
                // Update the vertical offset
                progressRect.anchoredPosition = new Vector2(progressRect.anchoredPosition.x, progressBarVerticalOffset);
            }
        }

        /// <summary>
        /// Called when values are changed in the Inspector.
        /// Updates progress bar positions if they already exist.
        /// </summary>
        void OnValidate()
        {
            // If progress bars are already created, update their positions
            if (progressBars != null && Application.isPlaying)
            {
                for (int i = 0; i < progressBars.Length; i++)
                {
                    if (progressBars[i] != null)
                    {
                        UpdateProgressBarPosition(i);
                    }
                }
            }
        }

        /// <summary>
        /// Shows the map view.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            RefreshMap(); // This will update progress bars with animation
        }

        /// <summary>
        /// Hides the map view.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called when the back button is clicked. Returns to main menu.
        /// </summary>
        void OnBackToMainMenuClicked()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[CampaignMapView] Main menu scene name is not set! Cannot navigate back.");
            }
        }
    }
}

