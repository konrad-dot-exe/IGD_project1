// CampaignLevelPicker.cs — Displays level selection UI for a mode node
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// UI component that displays 6 level tiles for a mode node.
    /// Shows completion status and enforces level order.
    /// </summary>
    public class CampaignLevelPicker : MonoBehaviour
    {
        [Header("Level UI")]
        [Tooltip("Parent container for level tiles")]
        [SerializeField] Transform levelContainer;

        [Tooltip("Prefab for a level tile button")]
        [SerializeField] GameObject levelTilePrefab;

        [Header("UI Elements")]
        [Tooltip("Text showing the mode name")]
        [SerializeField] TextMeshProUGUI modeNameText;

        [Tooltip("Text showing the selected level name (e.g., 'Level 3')")]
        [SerializeField] TextMeshProUGUI levelNameText;

        [Tooltip("Back button to return to map")]
        [SerializeField] Button backButton;

        [Tooltip("Play button (only enabled for next incomplete level)")]
        [SerializeField] Button playButton;

        [Tooltip("Reference to CampaignMapView (to show when back is clicked)")]
        [SerializeField] CampaignMapView mapView;

        [Header("Keyboard Display")]
        [Tooltip("Piano keyboard display component (shows featured notes for selected level)")]
        [SerializeField] PianoKeyboardDisplay keyboardDisplay;

        [Header("Visual Settings")]
        [Tooltip("Color for completed levels")]
        [SerializeField] Color completedColor = new Color(0.3f, 1f, 0.3f, 1f);

        [Tooltip("Color for available (next incomplete) level")]
        [SerializeField] Color availableColor = Color.white;

        [Tooltip("Color for locked (not yet available) levels")]
        [SerializeField] Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [Tooltip("Sprite for the next incomplete level (filled outline). If not assigned, will use default outline sprite.")]
        [SerializeField] Sprite filledOutlineSprite;

        [Tooltip("Text opacity for locked levels (0.0 = fully transparent, 1.0 = fully opaque)")]
        [SerializeField] [Range(0f, 1f)] float lockedTextOpacity = 0.03f;

        [Header("Triangle Indicator")]
        [Tooltip("Sprite for the triangle indicator. If not assigned, triangle will not be visible.")]
        [SerializeField] Sprite triangleSprite;

        [Tooltip("Animation speed in cycles per second")]
        [SerializeField] float animationSpeed = 2.0f;

        [Tooltip("Vertical movement amplitude in pixels (subtle movement)")]
        [SerializeField] float animationAmplitude = 8.0f;

        [Tooltip("Vertical offset below selected button in pixels")]
        [SerializeField] float verticalOffset = 10.0f;

        [Tooltip("Scale/size of the triangle indicator (width and height in pixels)")]
        [SerializeField] Vector2 triangleScale = new Vector2(20, 20);

        private CampaignService campaignService;
        private int currentNodeIndex = -1;
        private int selectedLevelIndex = -1;
        private Button[] levelButtons;
        private Image triangleIndicator;
        private RectTransform triangleRectTransform;

        void Awake()
        {
            // Auto-enable if disabled in editor (for workflow convenience)
            // Must be in Awake() so it's active before other scripts' Start() methods run
            // We'll hide it in Start(), but it needs to be active for initialization
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
                Debug.LogError("[CampaignLevelPicker] CampaignService not found!");
                return;
            }

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            // Hide by default (will be shown when a node is clicked)
            Hide();
        }

        /// <summary>
        /// Shows the level picker for a specific node.
        /// </summary>
        public void ShowForNode(int nodeIndex)
        {
            if (campaignService == null || campaignService.Campaign == null)
            {
                Debug.LogError("[CampaignLevelPicker] Cannot show levels: CampaignService or Campaign is null");
                return;
            }

            var campaign = campaignService.Campaign;
            if (nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
            {
                Debug.LogError($"[CampaignLevelPicker] Invalid node index: {nodeIndex}");
                return;
            }

            currentNodeIndex = nodeIndex;
            var node = campaign.nodes[nodeIndex];

            // Update mode name
            if (modeNameText != null)
                modeNameText.text = node.GetModeName();

            // Build level tiles
            BuildLevelTiles();

            // Show this UI
            gameObject.SetActive(true);

            // Force layout rebuild to ensure buttons are positioned correctly
            // This is important for triangle positioning
            if (levelContainer != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(levelContainer.GetComponent<RectTransform>());
            }

            // Auto-select the highest unlocked level
            int highestUnlockedLevel = GetHighestUnlockedLevelIndex(nodeIndex);
            if (highestUnlockedLevel >= 0)
            {
                selectedLevelIndex = highestUnlockedLevel;
                UpdatePlayButton();
                UpdateLevelNameText();
                UpdateKeyboardForSelectedLevel();
            }
            else
            {
                // No unlocked levels (shouldn't happen, but handle gracefully)
                selectedLevelIndex = -1;
                UpdatePlayButton();
                UpdateLevelNameText();
                
                // Clear keyboard display (no level selected)
                if (keyboardDisplay != null)
                {
                    keyboardDisplay.ClearDisplay();
                }
            }

            // Initialize triangle indicator AFTER setting selection
            // This ensures the triangle exists and can be shown immediately
            InitializeTriangleIndicator();
            
            // Update button selection (which will show the triangle)
            UpdateLevelButtonSelection();
            
            // Force one more frame delay to ensure layout is complete, then show triangle
            StartCoroutine(EnsureTriangleVisibleAfterLayout());
        }

        /// <summary>
        /// Builds the level tiles UI.
        /// </summary>
        void BuildLevelTiles()
        {
            if (campaignService == null || campaignService.Campaign == null) return;
            if (currentNodeIndex < 0) return;

            var campaign = campaignService.Campaign;
            var node = campaign.nodes[currentNodeIndex];
            if (node == null || node.levels == null) return;

            // Clear existing tiles
            if (levelContainer != null)
            {
                foreach (Transform child in levelContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            levelButtons = new Button[node.levels.Length];
            int nextIncompleteLevel = campaignService.GetNextIncompleteLevelIndex(currentNodeIndex);

            // Create tile for each level
            for (int i = 0; i < node.levels.Length; i++)
            {
                var level = node.levels[i];
                if (level == null) continue;

                bool isComplete = campaignService.IsLevelComplete(currentNodeIndex, i);
                bool isAvailable = (i == nextIncompleteLevel);
                bool isLocked = !isComplete && !isAvailable;

                GameObject tileObj;
                if (levelTilePrefab != null && levelContainer != null)
                {
                    tileObj = Instantiate(levelTilePrefab, levelContainer);
                }
                else
                {
                    // Fallback: create a simple button
                    tileObj = new GameObject($"LevelTile_{i + 1}");
                    if (levelContainer != null)
                        tileObj.transform.SetParent(levelContainer, false);
                    
                    var rect = tileObj.AddComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(150, 100);
                    
                    var image = tileObj.AddComponent<Image>();
                    image.color = isComplete ? completedColor : (isAvailable ? availableColor : lockedColor);
                    
                    var textObj = new GameObject("Text");
                    textObj.transform.SetParent(tileObj.transform, false);
                    var textRect = textObj.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;
                    var text = textObj.AddComponent<TextMeshProUGUI>();
                    text.text = $"Level {i + 1}";
                    // if (isComplete) text.text += "\n✓";
                    text.alignment = TextAlignmentOptions.Center;
                    text.color = Color.black;
                }

                Button button = tileObj.GetComponent<Button>();
                if (button == null)
                    button = tileObj.AddComponent<Button>();

                int levelIndex = i; // Capture for closure
                button.onClick.AddListener(() => OnLevelClicked(levelIndex));

                // Set button state
                button.interactable = isAvailable || isComplete; // Can click completed or available levels

                // Update appearance
                UpdateLevelTile(button, level, isComplete, isAvailable, isLocked);

                levelButtons[i] = button;
            }
        }

        /// <summary>
        /// Updates a level tile's appearance.
        /// </summary>
        void UpdateLevelTile(Button button, DictationLevel level, bool isComplete, bool isAvailable, bool isLocked)
        {
            if (button == null) return;

            // Update colors
            var colors = button.colors;
            if (isComplete)
            {
                colors.normalColor = completedColor;
            }
            else if (isAvailable)
            {
                colors.normalColor = availableColor;
            }
            else
            {
                colors.normalColor = lockedColor;
            }
            button.colors = colors;

            // Update text
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = level.title ?? $"Level {level.id}";
                
                // Apply opacity to text color for locked levels
                var textColor = text.color;
                if (isLocked)
                {
                    textColor.a = lockedTextOpacity;
                }
                else
                {
                    // Reset to full opacity for unlocked levels
                    textColor.a = 1f;
                }
                text.color = textColor;
            }

            // Update image sprite and color
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                // Use filled outline sprite for the next incomplete level (available level)
                if (isAvailable && filledOutlineSprite != null)
                {
                    image.sprite = filledOutlineSprite;
                }
                // Note: If not available, the sprite will remain as set in the prefab (outline)
                
                image.color = isComplete ? completedColor : (isAvailable ? availableColor : lockedColor);
            }
        }

        /// <summary>
        /// Called when a level tile is clicked.
        /// </summary>
        void OnLevelClicked(int levelIndex)
        {
            if (campaignService == null) return;

            // Check if this level is available (next incomplete or already completed)
            int nextIncompleteLevel = campaignService.GetNextIncompleteLevelIndex(currentNodeIndex);
            bool isComplete = campaignService.IsLevelComplete(currentNodeIndex, levelIndex);
            bool isAvailable = (levelIndex == nextIncompleteLevel);

            if (!isAvailable && !isComplete)
            {
                Debug.LogWarning($"[CampaignLevelPicker] Level {levelIndex} is not available yet!");
                return;
            }

            selectedLevelIndex = levelIndex;
            UpdatePlayButton();
            UpdateLevelButtonSelection();
            UpdateLevelNameText();

            // Update keyboard display for selected level
            UpdateKeyboardForSelectedLevel();
        }

        /// <summary>
        /// Gets the highest unlocked level index for a node.
        /// A level is unlocked if it's either completed or is the next incomplete level.
        /// </summary>
        int GetHighestUnlockedLevelIndex(int nodeIndex)
        {
            if (campaignService == null || campaignService.Campaign == null)
                return -1;

            var campaign = campaignService.Campaign;
            if (nodeIndex < 0 || nodeIndex >= campaign.nodes.Length)
                return -1;

            var node = campaign.nodes[nodeIndex];
            if (node == null || node.levels == null)
                return -1;

            int nextIncompleteLevel = campaignService.GetNextIncompleteLevelIndex(nodeIndex);
            
            // Iterate from highest to lowest index to find the highest unlocked level
            for (int i = node.levels.Length - 1; i >= 0; i--)
            {
                bool isComplete = campaignService.IsLevelComplete(nodeIndex, i);
                bool isAvailable = (i == nextIncompleteLevel);
                
                // Level is unlocked if it's completed or available
                if (isComplete || isAvailable)
                {
                    return i;
                }
            }

            // No unlocked levels found
            return -1;
        }

        /// <summary>
        /// Updates the keyboard display for the currently selected level.
        /// </summary>
        void UpdateKeyboardForSelectedLevel()
        {
            if (keyboardDisplay == null || campaignService == null || currentNodeIndex < 0 || selectedLevelIndex < 0)
            {
                // No keyboard display or no level selected - clear display
                if (keyboardDisplay != null)
                    keyboardDisplay.ClearDisplay();
                return;
            }

            var campaign = campaignService.Campaign;
            if (campaign == null || campaign.nodes == null || currentNodeIndex >= campaign.nodes.Length)
                return;

            var node = campaign.nodes[currentNodeIndex];
            if (node == null || node.levels == null || selectedLevelIndex >= node.levels.Length)
                return;

            var level = node.levels[selectedLevelIndex];
            if (level != null)
            {
                keyboardDisplay.UpdateDisplayForLevel(level);
            }
        }

        /// <summary>
        /// Updates the visual selection state of level buttons.
        /// </summary>
        void UpdateLevelButtonSelection()
        {
            if (levelButtons == null) return;

            // Get EventSystem if available (for button selection highlighting)
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] == null) continue;

                bool isSelected = (i == selectedLevelIndex);
                var button = levelButtons[i];
                
                if (isSelected && eventSystem != null)
                {
                    // Set the button as selected in the EventSystem for visual highlighting
                    eventSystem.SetSelectedGameObject(button.gameObject);
                }
            }

            // Update triangle visibility - ensure it exists first
            if (triangleIndicator == null || triangleIndicator.gameObject == null)
            {
                // Triangle doesn't exist - initialize it
                InitializeTriangleIndicator();
            }

            // Now update visibility
            if (triangleIndicator != null && triangleIndicator.gameObject != null)
            {
                bool shouldBeVisible = selectedLevelIndex >= 0;
                if (triangleIndicator.gameObject.activeSelf != shouldBeVisible)
                {
                    triangleIndicator.gameObject.SetActive(shouldBeVisible);
                }
            }
        }

        /// <summary>
        /// Updates the level name text to show the selected level number.
        /// </summary>
        void UpdateLevelNameText()
        {
            if (levelNameText == null) return;

            if (selectedLevelIndex >= 0)
            {
                // Display level number (level indices are 0-based, so add 1 for display)
                levelNameText.text = $"Level {selectedLevelIndex + 1}";
            }
            else
            {
                // No level selected - clear the text
                levelNameText.text = "";
            }
        }

        /// <summary>
        /// Updates the play button state.
        /// </summary>
        void UpdatePlayButton()
        {
            if (playButton == null) return;

            bool canPlay = selectedLevelIndex >= 0;
            playButton.interactable = canPlay;

            // Update play button text if it has text
            var text = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null && canPlay)
            {
                text.text = campaignService.IsLevelComplete(currentNodeIndex, selectedLevelIndex)
                    ? "Replay" : "Play";
            }
        }

        /// <summary>
        /// Called when the play button is clicked.
        /// </summary>
        void OnPlayClicked()
        {
            if (campaignService == null || selectedLevelIndex < 0) return;

            // Start the level
            campaignService.StartFromMap(currentNodeIndex, selectedLevelIndex);

            // Hide level picker
            Hide();
        }

        /// <summary>
        /// Called when the back button is clicked.
        /// </summary>
        void OnBackClicked()
        {
            Hide();
            
            // Show map view
            if (mapView != null)
            {
                mapView.Show();
            }
        }

        /// <summary>
        /// Shows the level picker.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the level picker.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            selectedLevelIndex = -1;

            // Clear keyboard display when hiding
            if (keyboardDisplay != null)
            {
                keyboardDisplay.ClearDisplay();
            }

            // Hide triangle indicator
            if (triangleIndicator != null)
            {
                triangleIndicator.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Initializes the triangle indicator GameObject.
        /// </summary>
        void InitializeTriangleIndicator()
        {
            // More robust check: verify the GameObject actually exists
            // Unity's == null check works for destroyed objects, but let's be extra safe
            bool triangleExists = triangleIndicator != null && 
                                   triangleIndicator.gameObject != null && 
                                   triangleRectTransform != null;

            // If triangle doesn't exist or was destroyed, clear references and recreate
            if (!triangleExists)
            {
                // Clear any stale references
                triangleIndicator = null;
                triangleRectTransform = null;

                // Also check if there's a leftover GameObject with the name (shouldn't happen, but safety check)
                Transform existingTriangle = transform.Find("TriangleIndicator");
                if (existingTriangle != null)
                {
                    DestroyImmediate(existingTriangle.gameObject);
                }
            }

            // Create triangle if it doesn't exist
            if (triangleIndicator == null)
            {
                GameObject triangleObj = new GameObject("TriangleIndicator");
                
                // Parent to this GameObject (not levelContainer) to avoid being destroyed
                // when BuildLevelTiles() clears levelContainer children
                triangleObj.transform.SetParent(transform, false);

                // Add RectTransform
                triangleRectTransform = triangleObj.AddComponent<RectTransform>();
                triangleRectTransform.sizeDelta = triangleScale; // Size adjustable in Inspector
                triangleRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                triangleRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                triangleRectTransform.pivot = new Vector2(0.5f, 0.5f);

                // Add Image component
                triangleIndicator = triangleObj.AddComponent<Image>();
                triangleIndicator.color = Color.black;
                
                // Set sprite if assigned
                if (triangleSprite != null)
                {
                    triangleIndicator.sprite = triangleSprite;
                }

                // Initially hide
                triangleObj.SetActive(false);
            }
            else
            {
                // Update sprite if it changed
                if (triangleSprite != null && triangleIndicator.sprite != triangleSprite)
                {
                    triangleIndicator.sprite = triangleSprite;
                }

                // Update scale if triangle already exists
                if (triangleRectTransform != null)
                {
                    triangleRectTransform.sizeDelta = triangleScale;
                }
            }

            // Note: Triangle visibility will be handled by UpdateLevelButtonSelection()
            // and Update() method to ensure proper timing with button layout
        }

        /// <summary>
        /// Updates the triangle indicator position and animation.
        /// </summary>
        void Update()
        {
            // Only animate when picker is active and a level is selected
            if (!gameObject.activeSelf || selectedLevelIndex < 0)
            {
                // Ensure triangle is hidden if conditions aren't met
                if (triangleIndicator != null && triangleIndicator.gameObject != null && triangleIndicator.gameObject.activeSelf)
                {
                    triangleIndicator.gameObject.SetActive(false);
                }
                return;
            }

            // Ensure triangle exists - recreate if needed
            if (triangleIndicator == null || triangleIndicator.gameObject == null || triangleRectTransform == null)
            {
                InitializeTriangleIndicator();
                // If still null after initialization, something went wrong
                if (triangleIndicator == null || triangleIndicator.gameObject == null)
                {
                    return;
                }
            }

            if (levelButtons == null || selectedLevelIndex >= levelButtons.Length || levelButtons[selectedLevelIndex] == null)
            {
                // Buttons not ready yet - hide triangle until they are
                if (triangleIndicator.gameObject.activeSelf)
                {
                    triangleIndicator.gameObject.SetActive(false);
                }
                return;
            }

            // Get selected button's RectTransform
            RectTransform buttonRect = levelButtons[selectedLevelIndex].GetComponent<RectTransform>();
            if (buttonRect == null)
            {
                // Button rect not ready - hide triangle
                if (triangleIndicator.gameObject.activeSelf)
                {
                    triangleIndicator.gameObject.SetActive(false);
                }
                return;
            }

            // Ensure triangle is visible now that we have valid button
            if (!triangleIndicator.gameObject.activeSelf)
            {
                triangleIndicator.gameObject.SetActive(true);
            }

            // Calculate base position: centered horizontally below button
            Vector3 buttonWorldPos = buttonRect.position;
            
            // Check if button has valid layout (rect size should be non-zero)
            // This ensures the layout system has positioned the button
            if (buttonRect.rect.width <= 0 || buttonRect.rect.height <= 0)
            {
                // Layout not complete yet - wait a frame
                return;
            }

            Vector3 basePosition = new Vector3(
                buttonWorldPos.x, // Centered horizontally
                buttonWorldPos.y - (buttonRect.rect.height * 0.5f) - verticalOffset, // Below button
                buttonWorldPos.z
            );

            // Calculate sinusoidal vertical offset
            float sinOffset = Mathf.Sin(Time.time * animationSpeed * 2f * Mathf.PI) * animationAmplitude;

            // Apply position (instant horizontal, animated vertical)
            triangleRectTransform.position = new Vector3(
                basePosition.x,
                basePosition.y + sinOffset,
                basePosition.z
            );
        }

        /// <summary>
        /// Coroutine to ensure triangle is visible after layout is complete.
        /// This helps with the intermittent visibility issue.
        /// </summary>
        System.Collections.IEnumerator EnsureTriangleVisibleAfterLayout()
        {
            // Wait for end of frame to ensure layout is complete
            yield return new WaitForEndOfFrame();
            
            // Wait one more frame for safety
            yield return null;

            // Ensure triangle exists and is shown if a level is selected
            if (selectedLevelIndex >= 0)
            {
                // Re-initialize if needed (in case it was destroyed)
                if (triangleIndicator == null || triangleIndicator.gameObject == null)
                {
                    InitializeTriangleIndicator();
                }

                // Force show the triangle
                if (triangleIndicator != null && triangleIndicator.gameObject != null)
                {
                    triangleIndicator.gameObject.SetActive(true);
                }
            }
        }
    }
}

