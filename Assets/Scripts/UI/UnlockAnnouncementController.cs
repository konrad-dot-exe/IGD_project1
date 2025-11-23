// UnlockAnnouncementController.cs — Reusable unlock announcement UI
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace EarFPS
{
    /// <summary>
    /// Displays an announcement when a new module/node is unlocked.
    /// Reusable across mini-games. Shows before the end screen.
    /// </summary>
    public class UnlockAnnouncementController : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("CanvasGroup for the unlock announcement panel")]
        [SerializeField] CanvasGroup group;
        
        [Tooltip("Title text (will show 'New Module Unlocked: [Mode Name]')")]
        [SerializeField] TextMeshProUGUI titleText;
        
        [Tooltip("Continue button")]
        [SerializeField] Button continueButton;
        
        [Tooltip("First selectable element for focus")]
        [SerializeField] Selectable firstFocus;

        [Tooltip("Keyboard display component to show the unlocked mode")]
        [SerializeField] PianoKeyboardDisplay keyboardDisplay;

        [Header("Keyboard Display")]
        [Tooltip("Opacity for notes not in the unlocked mode (0.0 to 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] float nonFeaturedKeyOpacity = 0.3f;

        [Header("Animation")]
        [Tooltip("Fade-in duration in seconds")]
        [SerializeField] float fadeInDuration = 0.3f;

        private float _prevTimeScale = 1f;
        private CursorLockMode _prevLock;
        private bool _prevCursor;
        private System.Action onContinueCallback;
        private bool isInitialized = false;

        void Awake()
        {
            Initialize();
            HideImmediate();
        }

        /// <summary>
        /// Initializes the component (button listeners, etc.).
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

            // Setup button listener
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            isInitialized = true;
        }

        /// <summary>
        /// Shows the unlock announcement for a specific mode name.
        /// </summary>
        /// <param name="modeName">Name of the unlocked mode (e.g., "Mixolydian")</param>
        /// <param name="onContinue">Callback invoked when Continue button is clicked</param>
        public void ShowUnlock(string modeName, System.Action onContinue)
        {
            ShowUnlock(modeName, null, onContinue);
        }

        /// <summary>
        /// Shows the unlock announcement for a specific mode with keyboard display.
        /// </summary>
        /// <param name="modeName">Name of the unlocked mode (e.g., "Mixolydian")</param>
        /// <param name="mode">ScaleMode enum for the unlocked mode (used for keyboard display)</param>
        /// <param name="onContinue">Callback invoked when Continue button is clicked</param>
        public void ShowUnlock(string modeName, ScaleMode? mode, System.Action onContinue)
        {
            // Ensure initialization
            Initialize();

            // Store callback
            onContinueCallback = onContinue;

            // Update title text
            if (titleText != null)
            {
                titleText.text = $"Module Unlocked: {modeName}";
            }

            // Hide keyboard UI when unlock announcement appears
            var keyboardUI = FindFirstObjectByType<PianoKeyboardUI>();
            if (keyboardUI != null)
            {
                keyboardUI.HideImmediate();
            }

            // Stop drone sound when unlock announcement appears
            var dronePlayer = FindFirstObjectByType<DronePlayer>();
            if (dronePlayer != null)
            {
                dronePlayer.Stop();
            }
            
            // Play sparkles sound
            var fmodSfx = FindFirstObjectByType<FmodSfxPlayer>();
            if (fmodSfx != null)
            {
                fmodSfx.PlaySparkles();
            }

            // Pause time
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // Unlock cursor
            _prevLock = Cursor.lockState;
            _prevCursor = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Show UI FIRST (keyboard display needs parent to be active)
            if (!group) group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            gameObject.SetActive(true);

            // Show keyboard display for the unlocked mode (AFTER parent is active so coroutine can run)
            if (mode.HasValue && keyboardDisplay != null)
            {
                // Show keyboard immediately (C4-C5, MIDI 60-72)
                keyboardDisplay.ShowForMode(mode.Value, 60, 72, nonFeaturedKeyOpacity);
            }

            // Fade in
            StartCoroutine(FadeIn());

            // Focus first element
            FocusFirst();
        }

        /// <summary>
        /// Fades in the announcement panel.
        /// </summary>
        System.Collections.IEnumerator FadeIn()
        {
            if (!group) yield break;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                group.alpha = alpha;
                yield return null;
            }
            group.alpha = 1f;
        }

        /// <summary>
        /// Called when the Continue button is clicked.
        /// </summary>
        void OnContinueClicked()
        {
            Hide();

            // Invoke callback
            if (onContinueCallback != null)
            {
                onContinueCallback.Invoke();
                onContinueCallback = null;
            }
        }

        /// <summary>
        /// Hides the unlock announcement.
        /// </summary>
        public void Hide()
        {
            // Clear keyboard display
            if (keyboardDisplay != null)
            {
                keyboardDisplay.ClearDisplay();
            }

            // Restore time
            Time.timeScale = _prevTimeScale;

            // Restore cursor
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevCursor;

            HideImmediate();
        }

        /// <summary>
        /// Immediately hides the unlock announcement (no animation).
        /// </summary>
        void HideImmediate()
        {
            // Clear keyboard display
            if (keyboardDisplay != null)
            {
                keyboardDisplay.ClearDisplay();
            }

            if (!group) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Focuses the first selectable element.
        /// </summary>
        void FocusFirst()
        {
            var es = EventSystem.current;
            if (!es) return;
            GameObject go = (firstFocus ? firstFocus.gameObject : (continueButton ? continueButton.gameObject : null));
            es.SetSelectedGameObject(go);
        }
    }
}

