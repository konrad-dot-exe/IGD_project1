// NodeProgressBar.cs — Reusable progress bar component for node completion display
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace EarFPS
{
    /// <summary>
    /// Reusable progress bar component that displays a completion percentage.
    /// Shows an outlined rectangle with a filled portion that animates from bottom to top.
    /// </summary>
    public class NodeProgressBar : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("Image component for the outline/border (should use sliced sprite). If not assigned, will be created automatically.")]
        [SerializeField] Image outlineImage;
        
        [Tooltip("Image component for the fill (should be solid color). If not assigned, will be created automatically.")]
        [SerializeField] Image fillImage;

        [Header("Sprites")]
        [Tooltip("Sprite for the outline/border (should be a sliced sprite for borders). If not assigned, will try to load from Assets/Art/Images/outline.png")]
        [SerializeField] Sprite outlineSprite;

        [Header("Settings")]
        [Tooltip("Default width of the progress bar")]
        [SerializeField] float width = 100f;
        
        [Tooltip("Default height of the progress bar")]
        [SerializeField] float height = 600f;
        
        [Tooltip("Fill color for the progress bar")]
        [SerializeField] Color fillColor = new Color(53f / 255f, 53f / 255f, 53f / 255f, 1f); // #353535
        
        [Tooltip("Animation duration in seconds when progress changes")]
        [SerializeField] float animationDuration = 1.0f;

        private RectTransform rectTransform;
        private RectTransform fillRectTransform;
        private CanvasGroup canvasGroup;
        private float currentProgress = 0f;
        private float targetProgress = 0f;
        private Coroutine animationCoroutine;

        void Awake()
        {
            // Get or create RectTransform
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
                rectTransform = gameObject.AddComponent<RectTransform>();

            // Get or create CanvasGroup for opacity control
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Initialize components if not assigned
            InitializeComponents();

            // Set initial size
            rectTransform.sizeDelta = new Vector2(width, height);

            // Setup fill image
            SetupFillImage();
        }

        /// <summary>
        /// Initializes the outline and fill images if not already assigned.
        /// </summary>
        void InitializeComponents()
        {
            // Find or create outline image
            if (outlineImage == null)
            {
                GameObject outlineObj = new GameObject("Outline");
                outlineObj.transform.SetParent(transform, false);
                outlineImage = outlineObj.AddComponent<Image>();
                
                var outlineRect = outlineObj.GetComponent<RectTransform>();
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.sizeDelta = Vector2.zero;
                outlineRect.offsetMin = Vector2.zero;
                outlineRect.offsetMax = Vector2.zero;

                // Load outline sprite if not assigned (user should assign it in Inspector for best results)
                if (outlineSprite == null)
                {
                    // Note: Resources.Load requires the sprite to be in a Resources folder
                    // For best results, assign the sprite directly in the Inspector
                    // If you want to use Resources.Load, move the sprite to Assets/Resources/Art/Images/outline.png
                    // outlineSprite = Resources.Load<Sprite>("Art/Images/outline");
                }

                // Set sprite and make it sliced
                if (outlineSprite != null)
                {
                    outlineImage.sprite = outlineSprite;
                    outlineImage.type = Image.Type.Sliced;
                }
                else
                {
                    // Fallback: use a simple colored border
                    outlineImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black
                    outlineImage.type = Image.Type.Simple;
                }
            }
            else
            {
                // Outline image exists, ensure it has the sprite set
                if (outlineImage.sprite == null && outlineSprite != null)
                {
                    outlineImage.sprite = outlineSprite;
                    outlineImage.type = Image.Type.Sliced;
                }
            }

            // Find or create fill image
            if (fillImage == null)
            {
                GameObject fillObj = new GameObject("Fill");
                fillObj.transform.SetParent(transform, false);
                fillImage = fillObj.AddComponent<Image>();
                
                fillRectTransform = fillObj.GetComponent<RectTransform>();
                fillRectTransform.anchorMin = new Vector2(0, 0); // Anchor to bottom
                fillRectTransform.anchorMax = new Vector2(1, 0); // Anchor to bottom
                fillRectTransform.pivot = new Vector2(0.5f, 0); // Pivot at bottom center
                fillRectTransform.offsetMin = Vector2.zero;
                fillRectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                fillRectTransform = fillImage.GetComponent<RectTransform>();
            }

            // Ensure fill is a child of this transform
            if (fillImage.transform.parent != transform)
                fillImage.transform.SetParent(transform, false);

            // Ensure outline is a child of this transform (should be behind fill)
            if (outlineImage.transform.parent != transform)
                outlineImage.transform.SetParent(transform, false);

            // Set outline to be behind fill (lower sibling index)
            outlineImage.transform.SetAsFirstSibling();
            fillImage.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Sets up the fill image with proper anchoring and initial state.
        /// </summary>
        void SetupFillImage()
        {
            if (fillRectTransform == null) return;

            // Anchor fill to bottom
            fillRectTransform.anchorMin = new Vector2(0, 0);
            fillRectTransform.anchorMax = new Vector2(1, 0);
            fillRectTransform.pivot = new Vector2(0.5f, 0);
            
            // Set initial size (0 height)
            fillRectTransform.offsetMin = Vector2.zero;
            fillRectTransform.offsetMax = new Vector2(0, 0);
            
            // Set fill color
            if (fillImage != null)
            {
                fillImage.color = fillColor;
                fillImage.raycastTarget = false; // Don't block clicks
            }
        }

        /// <summary>
        /// Sets the progress percentage (0.0 to 1.0) with optional animation.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 (0%) to 1.0 (100%)</param>
        /// <param name="animate">Whether to animate the change (default: true)</param>
        public void SetProgress(float progress, bool animate = true)
        {
            progress = Mathf.Clamp01(progress);
            targetProgress = progress;

            if (animate && animationDuration > 0f)
            {
                // Stop any existing animation
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                }
                
                // Start new animation
                animationCoroutine = StartCoroutine(AnimateProgress());
            }
            else
            {
                // Set immediately without animation
                currentProgress = progress;
                UpdateFillSize();
            }
        }

        /// <summary>
        /// Sets the progress immediately without animation.
        /// </summary>
        public void SetProgressImmediate(float progress)
        {
            SetProgress(progress, animate: false);
        }

        /// <summary>
        /// Coroutine that animates the progress bar from current to target value.
        /// </summary>
        IEnumerator AnimateProgress()
        {
            float startProgress = currentProgress;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                
                // Use smooth interpolation (ease-in-out)
                t = t * t * (3f - 2f * t); // Smoothstep
                
                currentProgress = Mathf.Lerp(startProgress, targetProgress, t);
                UpdateFillSize();
                
                yield return null;
            }

            // Ensure we end at exact target value
            currentProgress = targetProgress;
            UpdateFillSize();
            animationCoroutine = null;
        }

        /// <summary>
        /// Updates the fill image size based on current progress.
        /// </summary>
        void UpdateFillSize()
        {
            if (fillRectTransform == null || rectTransform == null) return;

            // Calculate fill height based on progress
            float fillHeight = rectTransform.rect.height * currentProgress;
            
            // Update fill rect transform
            fillRectTransform.offsetMin = Vector2.zero;
            fillRectTransform.offsetMax = new Vector2(0, fillHeight);
        }

        /// <summary>
        /// Sets the size of the progress bar.
        /// </summary>
        public void SetSize(float newWidth, float newHeight)
        {
            width = newWidth;
            height = newHeight;
            
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(width, height);
                UpdateFillSize(); // Update fill to match new size
            }
        }

        /// <summary>
        /// Sets the fill color.
        /// </summary>
        public void SetFillColor(Color color)
        {
            fillColor = color;
            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }
        }

        /// <summary>
        /// Gets the current progress value (0.0 to 1.0).
        /// </summary>
        public float GetProgress()
        {
            return currentProgress;
        }

        /// <summary>
        /// Sets the overall opacity of the progress bar (affects both outline and fill).
        /// </summary>
        /// <param name="opacity">Opacity value from 0.0 (transparent) to 1.0 (opaque)</param>
        public void SetOpacity(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = opacity;
            }
            else
            {
                // Fallback: set opacity on individual images if CanvasGroup doesn't exist
                if (outlineImage != null)
                {
                    var color = outlineImage.color;
                    color.a = opacity;
                    outlineImage.color = color;
                }
                if (fillImage != null)
                {
                    var color = fillImage.color;
                    color.a = opacity;
                    fillImage.color = color;
                }
            }
        }

        void OnValidate()
        {
            // Update size if changed in inspector
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(width, height);
            }

            // Update fill color if changed in inspector
            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            // Ensure animation duration is positive
            if (animationDuration < 0f)
                animationDuration = 0f;
        }
    }
}

