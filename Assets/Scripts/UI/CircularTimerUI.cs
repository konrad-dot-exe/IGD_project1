using UnityEngine;
using UnityEngine.UI;
using EarFPS;

namespace Sonoria.Dictation
{
    /// <summary>
    /// Displays a circular countdown timer that disappears clockwise from 12 o'clock.
    /// The fill amount represents the fraction of time remaining.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CircularTimerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image component for the timer circle. Auto-found if not assigned.")]
        [SerializeField] Image timerImage;

        [Tooltip("Reference to MelodicDictationController. Auto-found if autoFindController is true.")]
        [SerializeField] MelodicDictationController controller;

        [Header("Settings")]
        [Tooltip("Auto-find MelodicDictationController in scene if true.")]
        [SerializeField] bool autoFindController = true;

        [Tooltip("Color of the timer circle.")]
        [SerializeField] Color timerColor = new Color(0f, 0.48f, 1f, 1f); // #007BFF

        private void Awake()
        {
            // Auto-find Image component if not assigned
            if (timerImage == null)
            {
                timerImage = GetComponent<Image>();
                if (timerImage == null)
                {
                    Debug.LogError("[CircularTimerUI] No Image component found! Please add an Image component to this GameObject.");
                    return;
                }
            }

            // Configure Image for radial fill
            timerImage.type = Image.Type.Filled;
            timerImage.fillMethod = Image.FillMethod.Radial360;
            timerImage.fillOrigin = (int)Image.Origin360.Top; // 12 o'clock
            timerImage.fillClockwise = false; // Fill counter-clockwise so it disappears clockwise
            timerImage.fillAmount = 1.0f; // Start with full circle

            // Set initial color
            timerImage.color = timerColor;
        }

        private void Start()
        {
            // Auto-find controller if enabled
            if (autoFindController && controller == null)
            {
                controller = FindFirstObjectByType<MelodicDictationController>(FindObjectsInactive.Exclude);
                if (controller == null)
                {
                    Debug.LogWarning("[CircularTimerUI] MelodicDictationController not found in scene. Timer will not update.");
                }
            }
        }

        private void Update()
        {
            // Update fill amount based on time remaining
            if (timerImage == null) return;

            // Update color if changed in Inspector
            if (timerImage.color != timerColor)
            {
                timerImage.color = timerColor;
            }

            // Get time data from controller
            if (controller == null)
            {
                // Controller not available - show full circle
                timerImage.fillAmount = 1.0f;
                return;
            }

            float timeLimit = controller.GetTimeLimit();
            float timeRemaining = controller.GetTimeLimitRemaining();
            bool isActive = controller.IsTimeLimitActive();
            bool isListening = controller.IsListening();

            // Handle edge cases
            if (timeLimit <= 0f)
            {
                // Division by zero protection - show full circle
                timerImage.fillAmount = 1.0f;
                return;
            }

            if (!isActive)
            {
                // Timer hasn't started yet - show full circle
                timerImage.fillAmount = 1.0f;
                return;
            }

            // Stop countdown when melody is completed (not in Listening state)
            if (!isListening)
            {
                // Freeze the timer at current value - don't update fillAmount
                // This preserves the current state when melody is completed
                return;
            }

            // Calculate fill amount: remaining / total
            // When timeRemaining = timeLimit, fillAmount = 1.0 (full circle)
            // When timeRemaining = 0, fillAmount = 0.0 (empty circle)
            float fillAmount = Mathf.Clamp01(timeRemaining / timeLimit);
            timerImage.fillAmount = fillAmount;
        }
    }
}

