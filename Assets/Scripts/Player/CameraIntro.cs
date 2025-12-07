using System.Collections;
using UnityEngine;
using TMPro;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// Manages the camera intro sequence: rotates from sky view (-65°) to table view (38°)
    /// when a level starts from the campaign map.
    /// </summary>
    public class CameraIntro : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to animate. If null, uses this transform.")]
        [SerializeField] Transform target;

        [Header("Rotation Settings")]
        [Tooltip("Starting X rotation angle (pointing at sky)")]
        [SerializeField] float startRotationX = -65f;
        
        [Tooltip("Ending X rotation angle (pointing at table)")]
        [SerializeField] float endRotationX = 38f;
        
        [Tooltip("Delay before rotation starts (gives player time to read intro message)")]
        [SerializeField, Range(0f, 5f)] float rotationDelay = 1f;
        
        [Tooltip("Animation duration in seconds")]
        [SerializeField, Range(0.1f, 5f)] float animationDuration = 1f;
        
        [Tooltip("Use smooth easing (ease-in-out)")]
        [SerializeField] bool useEasing = true;

        [Header("Level Intro Message")]
        [Tooltip("Text element to display level introduction message (e.g., 'Mixolydian — Level 4')")]
        [SerializeField] TextMeshProUGUI introMessageText;
        
        [Tooltip("Fade-in duration in seconds (quick fade-in when intro starts)")]
        [SerializeField, Range(0.05f, 0.5f)] float fadeInDuration = 0.2f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        private float baseRotationY;
        private float baseRotationZ;
        private bool hasPlayedIntro = false;
        private Coroutine introCoroutine;

        void Awake()
        {
            if (!target) target = transform;
            
            // Store base Y and Z rotation values
            Vector3 currentEuler = target.rotation.eulerAngles;
            baseRotationY = currentEuler.y;
            baseRotationZ = currentEuler.z;
            
            // Set initial rotation to start angle (pointing at sky)
            target.rotation = Quaternion.Euler(startRotationX, baseRotationY, baseRotationZ);
            
            if (debugLogs)
            {
                Debug.Log($"[CameraIntro] Initialized on {target.name}. Set initial rotation to X={startRotationX}°");
            }
        }

        /// <summary>
        /// Plays the intro animation, rotating from start angle to end angle.
        /// </summary>
        /// <param name="onComplete">Callback invoked when animation completes</param>
        public void PlayIntro(System.Action onComplete = null)
        {
            if (hasPlayedIntro)
            {
                if (debugLogs) Debug.Log("[CameraIntro] Intro already played, skipping");
                if (onComplete != null) onComplete();
                return;
            }

            if (introCoroutine != null)
            {
                StopCoroutine(introCoroutine);
            }

            introCoroutine = StartCoroutine(PlayIntroCoroutine(onComplete));
        }

        private IEnumerator PlayIntroCoroutine(System.Action onComplete)
        {
            if (!target)
            {
                if (onComplete != null) onComplete();
                yield break;
            }

            if (debugLogs) Debug.Log($"[CameraIntro] Starting intro animation: {startRotationX}° -> {endRotationX}° over {animationDuration}s");

            // Setup level intro message
            bool hasMessage = introMessageText != null;
            Color messageColor = Color.white;
            if (hasMessage)
            {
                // Get level info from CampaignService
                var campaignService = CampaignService.Instance;
                if (campaignService != null)
                {
                    var currentNode = campaignService.CurrentNode;
                    var currentLevel = campaignService.CurrentLevel;
                    int currentLevelIndex = campaignService.CurrentLevelIndex;

                    if (currentNode != null && currentLevel != null && currentLevelIndex >= 0)
                    {
                        string modeName = currentNode.GetModeName();
                        int displayLevel = currentLevelIndex + 1; // 1-based for display
                        introMessageText.text = $"{modeName} — Level {displayLevel}";
                        
                        // Store original color and set alpha to 0
                        messageColor = introMessageText.color;
                        messageColor.a = 0f;
                        introMessageText.color = messageColor;
                        introMessageText.gameObject.SetActive(true);
                        
                        if (debugLogs) Debug.Log($"[CameraIntro] Displaying level intro message: {introMessageText.text}");
                    }
                    else
                    {
                        hasMessage = false; // No valid level info, don't show message
                    }
                }
                else
                {
                    hasMessage = false; // No CampaignService, don't show message
                }
            }

            float elapsed = 0f;
            float startX = startRotationX;
            float endX = endRotationX;
            float fadeInElapsed = 0f;

            // Phase 1: Fade in message
            while (fadeInElapsed < fadeInDuration)
            {
                float deltaTime = Time.deltaTime;
                fadeInElapsed += deltaTime;
                
                if (hasMessage)
                {
                    float fadeInT = Mathf.Clamp01(fadeInElapsed / fadeInDuration);
                    messageColor.a = fadeInT;
                    introMessageText.color = messageColor;
                }
                
                yield return null;
            }

            // Ensure message is fully visible
            if (hasMessage)
            {
                messageColor.a = 1f;
                introMessageText.color = messageColor;
            }

            // Phase 2: Wait for delay (message stays fully visible)
            if (rotationDelay > 0f)
            {
                if (debugLogs) Debug.Log($"[CameraIntro] Message visible, waiting {rotationDelay}s before starting rotation");
                yield return new WaitForSeconds(rotationDelay);
            }

            // Phase 3: Rotate camera and fade out message
            elapsed = 0f;
            while (elapsed < animationDuration)
            {
                float deltaTime = Time.deltaTime;
                elapsed += deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);

                // Apply easing if enabled
                if (useEasing)
                {
                    t = Mathf.SmoothStep(0f, 1f, t);
                }

                // Interpolate X rotation
                float currentX = Mathf.Lerp(startX, endX, t);

                // Apply rotation (preserve Y and Z)
                target.rotation = Quaternion.Euler(currentX, baseRotationY, baseRotationZ);

                // Fade out message during rotation
                if (hasMessage)
                {
                    float fadeOutT = Mathf.Clamp01(elapsed / animationDuration);
                    messageColor.a = 1f - fadeOutT;
                    introMessageText.color = messageColor;
                }

                yield return null;
            }

            // Ensure final rotation is exact
            target.rotation = Quaternion.Euler(endRotationX, baseRotationY, baseRotationZ);
            
            // Hide message if it was shown
            if (hasMessage && introMessageText != null)
            {
                introMessageText.gameObject.SetActive(false);
                if (debugLogs) Debug.Log("[CameraIntro] Level intro message hidden");
            }
            
            hasPlayedIntro = true;
            introCoroutine = null;

            if (debugLogs) Debug.Log($"[CameraIntro] Intro animation complete. Final rotation: X={endRotationX}°");

            if (onComplete != null) onComplete();
        }

        /// <summary>
        /// Resets the intro state (allows intro to play again)
        /// </summary>
        public void ResetIntro()
        {
            hasPlayedIntro = false;
            if (introCoroutine != null)
            {
                StopCoroutine(introCoroutine);
                introCoroutine = null;
            }
            
            // Reset to start rotation
            if (target != null)
            {
                target.rotation = Quaternion.Euler(startRotationX, baseRotationY, baseRotationZ);
            }
            
            // Hide message if visible
            if (introMessageText != null && introMessageText.gameObject.activeSelf)
            {
                introMessageText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Gets whether the intro has already played
        /// </summary>
        public bool HasPlayedIntro => hasPlayedIntro;
    }
}

