// EndScreenController.cs — multi-module end screen
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Sonoria.Dictation;

namespace EarFPS
{
    public class EndScreenController : MonoBehaviour
    {
        public enum Mode { Interval, Dictation, Campaign }
        [Header("General")]
        [SerializeField] Mode mode = Mode.Interval;           // <- pick per scene
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI title;

        [Header("Interval Refs (Interval FPS)")]
        [SerializeField] TextMeshProUGUI statScore;
        [SerializeField] TextMeshProUGUI statTime;
        [SerializeField] TextMeshProUGUI statAccuracy;

        [Header("Dictation Refs (Melodic Dictation)")]
        [SerializeField] TextMeshProUGUI dictScore;
        [SerializeField] TextMeshProUGUI dictTime;
        [SerializeField] TextMeshProUGUI dictRounds;

        [Header("Buttons")]
        [SerializeField] Button btnRetry;
        [SerializeField] Button btnDashboard;
        [SerializeField] Selectable firstFocus;

        [Header("Campaign Mode Buttons")]
        [Tooltip("Continue button (shown in campaign mode - goes to next level)")]
        [SerializeField] Button btnContinue;
        [Tooltip("Back to Map button (shown in campaign mode - returns to level picker)")]
        [SerializeField] Button btnBackToMap;
        [Tooltip("Reference to CampaignLevelPicker (to show level picker when Back is clicked)")]
        [SerializeField] CampaignLevelPicker campaignLevelPicker;
        [Tooltip("Reference to CampaignMapView (fallback if level picker is not available)")]
        [SerializeField] CampaignMapView campaignMapView;

        [Header("Navigation")]
        [SerializeField] string retrySceneName = "";
        [SerializeField] string dashboardSceneName = "MainMenu";

        float _prevTimeScale = 1f;
        CursorLockMode _prevLock;
        bool _prevCursor;

        private bool isInitialized = false;

        void Awake()
        {
            Initialize();
            HideImmediate();
        }

        /// <summary>
        /// Ensures the endscreen is initialized (component references, button listeners).
        /// Safe to call multiple times. Will be called automatically in Awake(), but can be called
        /// manually if the GameObject was disabled and needs to be initialized on first show.
        /// </summary>
        void Initialize()
        {
            if (isInitialized) return;

            // Ensure GameObject is active for initialization
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!group) group = GetComponent<CanvasGroup>();
            
            // Setup button listeners (safe to call multiple times - RemoveAllListeners first)
            if (btnRetry != null)
            {
                btnRetry.onClick.RemoveAllListeners();
                btnRetry.onClick.AddListener(OnClickRetry);
            }
            if (btnDashboard != null)
            {
                btnDashboard.onClick.RemoveAllListeners();
                btnDashboard.onClick.AddListener(OnClickDashboard);
            }
            if (btnContinue != null)
            {
                btnContinue.onClick.RemoveAllListeners();
                btnContinue.onClick.AddListener(OnClickContinue);
            }
            if (btnBackToMap != null)
            {
                btnBackToMap.onClick.RemoveAllListeners();
                btnBackToMap.onClick.AddListener(OnClickBackToMap);
            }

            isInitialized = true;
        }

        // Keep existing API for Interval module
        public void Show(RunStats s, string titleOverride = null)
        {
            mode = Mode.Interval;
            ApplyCommonOpen();

            title.text = string.IsNullOrEmpty(titleOverride) ? "Mission Failed!" : titleOverride;

            // Show Interval refs
            SetActive(statScore, true);      statScore.text      = $"Score: <b>{s.score:n0}</b>";
            SetActive(statTime, true);       statTime.text       = $"Time: <b>{FormatTime(s.timeSeconds)}</b>";
            SetActive(statAccuracy, true);   statAccuracy.text   = $"Accuracy: <b>{(s.total > 0 ? Mathf.RoundToInt(100f * s.correct / s.total) : 0)}%</b>";

            // Hide Dictation refs
            SetActive(dictScore, false);
            SetActive(dictTime, false);
            SetActive(dictRounds, false);

            FocusFirst();
        }

        // New: Dictation-specific API
        public void ShowDictation(int score, int roundsCompleted, float timeSeconds, string titleOverride = "Game Over")
        {
            // Check if we're in campaign mode
            bool isCampaignMode = CampaignService.Instance != null && CampaignService.Instance.CurrentLevel != null;

            if (isCampaignMode)
            {
                ShowCampaign(score, roundsCompleted, timeSeconds, titleOverride);
            }
            else
            {
                mode = Mode.Dictation;
                ApplyCommonOpen();

                title.text = string.IsNullOrEmpty(titleOverride) ? "Game Over" : titleOverride;

                // Hide Interval refs
                SetActive(statScore, false);
                SetActive(statTime, false);
                SetActive(statAccuracy, false);

                // Show Dictation refs
                SetActive(dictScore, true);  dictScore.text  = $"Level Score: <b>{score:n0}</b>";
                SetActive(dictTime,  true);  dictTime.text   = $"Level Time: <b>{FormatTime(timeSeconds)}</b>";
                SetActive(dictRounds,true);  dictRounds.text = $"Rounds: <b>{roundsCompleted}</b>";

                // Hide campaign buttons, show normal buttons
                SetActive(btnContinue, false);
                SetActive(btnBackToMap, false);
                SetActive(btnRetry, true);
                SetActive(btnDashboard, true);

                FocusFirst();
            }
        }

        // New: Campaign-specific API
        public void ShowCampaign(int score, int roundsCompleted, float timeSeconds, string titleOverride = "Level Complete!")
        {
            mode = Mode.Campaign;
            ApplyCommonOpen();

            title.text = string.IsNullOrEmpty(titleOverride) ? "Level Complete!" : titleOverride;

            // Hide Interval refs
            SetActive(statScore, false);
            SetActive(statTime, false);
            SetActive(statAccuracy, false);

            // Show Dictation refs
            SetActive(dictScore, true);  if (dictScore != null) dictScore.text  = $"Score: <b>{score:n0}</b>";
            SetActive(dictTime,  true);  if (dictTime != null) dictTime.text   = $"Time: <b>{FormatTime(timeSeconds)}</b>";
            SetActive(dictRounds,true);  if (dictRounds != null) dictRounds.text = $"Rounds: <b>{roundsCompleted}</b>";

            // Check if this is a game over (player failed) or level completion (player succeeded)
            bool isGameOver = !string.IsNullOrEmpty(titleOverride) && titleOverride.ToLower().Contains("game over");

            if (isGameOver)
            {
                // Game Over: Player failed - can only retry same level or go back to map
                // Hide Continue button (cannot proceed to next level)
                SetActive(btnContinue, false);
                
                // Show Retry button (retry same level)
                SetActive(btnRetry, true);
                
                // Show Back to Map button
                SetActive(btnBackToMap, true);
                
                // Hide Dashboard button (campaign mode)
                SetActive(btnDashboard, false);
            }
            else
            {
                // Level Complete: Player succeeded - can continue to next level or go back to map
                // Play level complete sound
                var fmodSfx = FindFirstObjectByType<FmodSfxPlayer>();
                if (fmodSfx != null)
                {
                    fmodSfx.PlayLevelComplete();
                }
                
                // Hide Retry button (no need to retry)
                SetActive(btnRetry, false);
                SetActive(btnDashboard, false);

                // Always show Back to Map button
                SetActive(btnBackToMap, true);

                // Show Continue button only if there's a next level
                if (btnContinue != null && CampaignService.Instance != null)
                {
                    int nextLevel = CampaignService.Instance.GetCurrentNodeNextLevel();
                    if (nextLevel >= 0)
                    {
                        // There's a next level - show Continue button
                        SetActive(btnContinue, true);
                        btnContinue.interactable = true;
                        
                        // Update button text
                        var continueText = btnContinue.GetComponentInChildren<TextMeshProUGUI>();
                        if (continueText != null)
                        {
                            continueText.text = "Continue";
                        }
                    }
                    else
                    {
                        // All levels complete - hide Continue button
                        SetActive(btnContinue, false);
                    }
                }
                else
                {
                    // Fallback: hide Continue button if CampaignService is not available
                    SetActive(btnContinue, false);
                }
            }

            FocusFirst();
        }

        // Buttons
        public void OnClickRetry()
        {
            Hide();
            
            // Try to restart without reloading the scene
            var dictationController = FindFirstObjectByType<MelodicDictationController>();
            if (dictationController != null)
            {
                // Restart the round (will trigger environment reinitialization if game over occurred)
                dictationController.StartRound();
            }
            else
            {
                // Fallback: reload scene if controller not found
                Debug.LogWarning("[EndScreenController] MelodicDictationController not found. Falling back to scene reload.");
                string sceneToLoad = string.IsNullOrEmpty(retrySceneName)
                    ? SceneManager.GetActiveScene().name
                    : retrySceneName;
                SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            }
        }
        public void OnClickDashboard()
        {
            Hide();
            // Explicitly unlock cursor before loading MainMenu (don't restore previous locked state)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!string.IsNullOrEmpty(dashboardSceneName))
                SceneManager.LoadScene(dashboardSceneName, LoadSceneMode.Single);
        }

        // Campaign mode buttons
        public void OnClickContinue()
        {
            Hide();
            
            if (CampaignService.Instance != null)
            {
                // Start next level in current node
                CampaignService.Instance.StartNextLevel();
            }
        }

        public void OnClickBackToMap()
        {
            Hide();
            
            if (CampaignService.Instance != null)
            {
                // Check if all levels in the current node are complete
                int nextLevel = CampaignService.Instance.GetCurrentNodeNextLevel();
                bool allLevelsComplete = (nextLevel < 0); // -1 means all levels complete
                
                if (allLevelsComplete)
                {
                    // All levels complete - navigate to campaign map
                    if (campaignMapView != null)
                    {
                        campaignMapView.Show();
                    }
                    else
                    {
                        Debug.LogWarning("[EndScreenController] All levels complete but CampaignMapView is not assigned! Cannot navigate to map.");
                    }
                }
                else
                {
                    // Not all levels complete - show level picker for current node
                    int currentNodeIndex = CampaignService.Instance.CurrentNodeIndex;
                    if (currentNodeIndex >= 0 && campaignLevelPicker != null)
                    {
                        campaignLevelPicker.ShowForNode(currentNodeIndex);
                    }
                    else
                    {
                        Debug.LogWarning("[EndScreenController] No current node index available or level picker not assigned. Falling back to map view.");
                        // Fallback to map view
                        if (campaignMapView != null)
                        {
                            campaignMapView.Show();
                        }
                    }
                }
            }
            else
            {
                // Fallback to map view if CampaignService is not available
                if (campaignMapView != null)
                {
                    campaignMapView.Show();
                }
                else
                {
                    Debug.LogWarning("[EndScreenController] CampaignService not available and CampaignMapView is not assigned! Cannot navigate back.");
                }
            }
        }

        // Open/close
        void ApplyCommonOpen()
        {
            // Ensure initialization before showing (in case GameObject was disabled in editor)
            Initialize();

            // Hide keyboard UI when end screen appears
            var keyboardUI = FindFirstObjectByType<PianoKeyboardUI>();
            if (keyboardUI != null)
            {
                keyboardUI.HideImmediate();
            }

            _prevTimeScale = Time.timeScale;  Time.timeScale = 0f;
            _prevLock = Cursor.lockState;     _prevCursor = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            if (!group) group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
            }
            gameObject.SetActive(true);
        }
        public void Hide()
        {
            Time.timeScale = _prevTimeScale;
            Cursor.lockState = _prevLock; Cursor.visible = _prevCursor;
            HideImmediate();
        }
        void HideImmediate()
        {
            if (!group) return;
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
        void FocusFirst()
        {
            var es = EventSystem.current;
            if (!es) return;
            
            // Determine which button to focus based on what's visible
            GameObject go = null;
            if (firstFocus != null)
            {
                go = firstFocus.gameObject;
            }
            else if (btnRetry != null && btnRetry.gameObject.activeSelf)
            {
                go = btnRetry.gameObject;
            }
            else if (btnContinue != null && btnContinue.gameObject.activeSelf)
            {
                go = btnContinue.gameObject;
            }
            else if (btnBackToMap != null && btnBackToMap.gameObject.activeSelf)
            {
                go = btnBackToMap.gameObject;
            }
            
            es.SetSelectedGameObject(go);
        }

        static void SetActive(Behaviour b, bool active)
        {
            if (!b) return; b.gameObject.SetActive(active);
        }
        static string FormatTime(float t)
        {
            if (t < 0f) t = 0f;
            int total = Mathf.RoundToInt(t);
            int m = total / 60, s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}
