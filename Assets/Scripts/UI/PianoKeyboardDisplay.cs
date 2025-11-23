// PianoKeyboardDisplay.cs — Non-interactive piano keyboard display for level preview
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarFPS;
using Sonoria.Dictation;

namespace EarFPS
{
    /// <summary>
    /// Non-interactive display-only piano keyboard component.
    /// Shows featured notes (allowed degrees) at full opacity and non-featured notes at reduced opacity.
    /// Used in CampaignLevelPicker to preview level content.
    /// </summary>
    public class PianoKeyboardDisplay : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Parent container for keys")]
        [SerializeField] RectTransform keysParent;
        
        [Tooltip("Prefab for white keys")]
        [SerializeField] PianoKeyUI whiteKeyPrefab;
        
        [Tooltip("Prefab for black keys")]
        [SerializeField] PianoKeyUI blackKeyPrefab;
        
        [Tooltip("Width of white keys")]
        [SerializeField] float whiteWidth = 48f;
        
        [Tooltip("Height of white keys")]
        [SerializeField] float whiteHeight = 180f;
        
        [Tooltip("Black key width as ratio of white key width")]
        [Range(0.4f, 0.75f)]
        [SerializeField] float blackWidthRatio = 0.6f;
        
        [Tooltip("Black key height as ratio of white key height")]
        [Range(0.5f, 0.9f)]
        [SerializeField] float blackHeightRatio = 0.65f;
        
        [Tooltip("Spacing between white keys")]
        [SerializeField] float whiteSpacing = 2f;
        
        [Tooltip("Black key horizontal offset relative to white width")]
        [Range(0f, 1f)]
        [SerializeField] float blackHorizontalOffset = 0.65f;

        [Header("Container Sizing")]
        [Tooltip("Height of the keyboard container. Set to 0 to auto-size based on whiteHeight. If a parent VerticalLayoutGroup is present, you may need to disable layout control for this element.")]
        [SerializeField] float containerHeight = 0f;

        [Tooltip("If true, this element will ignore parent layout groups and use manual sizing")]
        [SerializeField] bool ignoreLayoutGroups = false;

        [Header("Featured Notes Opacity")]
        [Tooltip("Opacity for keys that are NOT featured in the current level. Featured keys remain at 1.0.")]
        [Range(0f, 1f)]
        [SerializeField] float nonFeaturedKeyOpacity = 0.3f;

        [Header("Display")]
        [Tooltip("Optional canvas group for show/hide control")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Keyboard Range")]
        [Tooltip("Fixed MIDI note range for the keyboard display. Keys within this range will always be shown, regardless of the selected level's range.")]
        [Range(0, 127)]
        [SerializeField] int displayRangeMinMidi = 48;
        
        [Tooltip("Fixed MIDI note range for the keyboard display. Keys within this range will always be shown, regardless of the selected level's range.")]
        [Range(0, 127)]
        [SerializeField] int displayRangeMaxMidi = 84;

        // Internal state
        private Dictionary<int, PianoKeyUI> _keyByNote = new Dictionary<int, PianoKeyUI>();
        private HashSet<int> featuredNotes = new HashSet<int>();
        private int currentDisplayMin = -1;
        private int currentDisplayMax = -1;
        private bool keyboardBuilt = false;

        // Pitch class constants (same as PianoKeyboardUI)
        readonly int[] whitePCs = { 0, 2, 4, 5, 7, 9, 11 }; // pitch-classes with white keys
        readonly int[] blackPCs = { 1, 3, 6, 8, 10 }; // black keys

        void Awake()
        {
            // Ensure keysParent is set
            if (keysParent == null)
                keysParent = GetComponent<RectTransform>();

            // Ensure canvas group exists
            if (canvasGroup == null)
            {
                var go = keysParent != null ? keysParent.gameObject : this.gameObject;
                canvasGroup = go.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = go.AddComponent<CanvasGroup>();
            }
        }

        void Reset()
        {
            keysParent = GetComponent<RectTransform>();
        }

        void OnValidate()
        {
            // Ensure display range is valid
            if (displayRangeMinMidi > displayRangeMaxMidi)
            {
                // Swap if min > max
                int temp = displayRangeMinMidi;
                displayRangeMinMidi = displayRangeMaxMidi;
                displayRangeMaxMidi = temp;
            }
            
            // Clamp to valid MIDI range
            displayRangeMinMidi = Mathf.Clamp(displayRangeMinMidi, 0, 127);
            displayRangeMaxMidi = Mathf.Clamp(displayRangeMaxMidi, 0, 127);

            // Update container height in editor if containerHeight is set
            // This allows previewing the height change in the editor
            if (containerHeight > 0f && keysParent != null)
            {
                var parentRT = keysParent.GetComponent<RectTransform>();
                if (parentRT != null)
                {
                    var size = parentRT.sizeDelta;
                    // Only update height, preserve width
                    parentRT.sizeDelta = new Vector2(size.x, containerHeight);
                }
            }
        }

        /// <summary>
        /// Updates the keyboard display for a selected level.
        /// </summary>
        public void UpdateDisplayForLevel(DictationLevel level)
        {
            if (level == null || level.profile == null)
            {
                Debug.LogWarning("[PianoKeyboardDisplay] Cannot update display: level or profile is null");
                ClearDisplay();
                return;
            }

            var profile = level.profile;

            // Extract register range
            int registerMin = profile.registerMinMidi;
            int registerMax = profile.registerMaxMidi;

            // Validate range
            if (registerMin < 0 || registerMax < 0 || registerMin > registerMax || registerMin > 127 || registerMax > 127)
            {
                Debug.LogWarning($"[PianoKeyboardDisplay] Invalid register range: {registerMin}-{registerMax}");
                ClearDisplay();
                return;
            }

            // Extract mode
            ScaleMode? modeToUse = null;

            if (level.useModeOverride)
            {
                // Use override mode
                modeToUse = level.modeOverride;
            }
            else
            {
                // Check for non-standard cases (placeholder behavior)
                if (profile.randomizeModeEachRound || 
                    (profile.allowedModes != null && profile.allowedModes.Length > 1))
                {
                    // Placeholder: show all notes at full opacity
                    modeToUse = null;
                }
                else if (profile.allowedModes != null && profile.allowedModes.Length > 0)
                {
                    // Use first allowed mode
                    modeToUse = profile.allowedModes[0];
                }
            }

            // Extract allowed degrees
            bool[] allowedDegrees = profile.allowedDegrees;

            // Validate and clamp display range
            int displayMin = Mathf.Clamp(displayRangeMinMidi, 0, 127);
            int displayMax = Mathf.Clamp(displayRangeMaxMidi, 0, 127);
            if (displayMin > displayMax)
            {
                // Swap if min > max
                int temp = displayMin;
                displayMin = displayMax;
                displayMax = temp;
            }
            
            // Check if keyboard needs to be rebuilt (only if display range changed or first time)
            if (!keyboardBuilt || displayMin != currentDisplayMin || displayMax != currentDisplayMax)
            {
                BuildKeyboard(displayMin, displayMax);
                currentDisplayMin = displayMin;
                currentDisplayMax = displayMax;
                keyboardBuilt = true;
            }

            // Calculate featured notes (uses actual level range, not display range)
            // Keys outside the level's range will show at reduced opacity
            if (modeToUse.HasValue && allowedDegrees != null && allowedDegrees.Length == 7)
            {
                featuredNotes = GetFeaturedMidiNotes(modeToUse.Value, allowedDegrees, registerMin, registerMax);
            }
            else
            {
                // Placeholder: show all notes at full opacity (empty set means all keys at full opacity)
                featuredNotes.Clear();
            }

            // Show the keyboard first (ensure GameObject is active before starting coroutine)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false; // Non-interactive
                canvasGroup.blocksRaycasts = false; // Don't block raycasts
            }
            
            // Ensure this GameObject is active (required for coroutines)
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            
            // Ensure keysParent is also active (keys need to be in active hierarchy)
            if (keysParent != null && !keysParent.gameObject.activeSelf)
                keysParent.gameObject.SetActive(true);

            // Update key opacity immediately
            UpdateKeyOpacityForFeaturedNotes();
            
            // Also update after a frame to ensure keys are fully initialized (fixes first-click issue)
            // Only start coroutine if GameObject is active in hierarchy (parent chain must be active)
            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(UpdateKeyOpacityAfterBuild());
            }
            else
            {
                // If coroutine can't start, just skip the delayed update (immediate update already happened)
                // This can happen if a parent GameObject is inactive
                Debug.LogWarning("[PianoKeyboardDisplay] Could not start opacity update coroutine - GameObject not active in hierarchy. Immediate update was applied.");
            }
        }

        /// <summary>
        /// Clears/hides the keyboard display.
        /// </summary>
        public void ClearDisplay()
        {
            // Hide the keyboard
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);

            // Clear featured notes
            featuredNotes.Clear();
        }

        /// <summary>
        /// Shows the keyboard display for a specific mode with all 7 scale degrees at full opacity.
        /// Used for unlock announcements and other mode preview displays.
        /// </summary>
        /// <param name="mode">The scale mode to display</param>
        /// <param name="registerMin">Minimum MIDI note in the register range</param>
        /// <param name="registerMax">Maximum MIDI note in the register range</param>
        /// <param name="nonFeaturedOpacity">Opacity for notes not in the mode (default uses nonFeaturedKeyOpacity field)</param>
        public void ShowForMode(ScaleMode mode, int registerMin, int registerMax, float? nonFeaturedOpacity = null)
        {
            // Validate range
            if (registerMin < 0 || registerMax < 0 || registerMin > registerMax || registerMin > 127 || registerMax > 127)
            {
                Debug.LogWarning($"[PianoKeyboardDisplay] Invalid register range: {registerMin}-{registerMax}");
                ClearDisplay();
                return;
            }

            // Use provided opacity or fall back to field value
            float opacityToUse = nonFeaturedOpacity ?? nonFeaturedKeyOpacity;

            // Validate and clamp display range (use register range as display range for unlock announcements)
            int displayMin = Mathf.Clamp(registerMin, 0, 127);
            int displayMax = Mathf.Clamp(registerMax, 0, 127);
            if (displayMin > displayMax)
            {
                // Swap if min > max
                int temp = displayMin;
                displayMin = displayMax;
                displayMax = temp;
            }

            // Check if keyboard needs to be rebuilt (only if display range changed or first time)
            if (!keyboardBuilt || displayMin != currentDisplayMin || displayMax != currentDisplayMax)
            {
                BuildKeyboard(displayMin, displayMax);
                currentDisplayMin = displayMin;
                currentDisplayMax = displayMax;
                keyboardBuilt = true;
            }

            // Calculate featured notes using all 7 degrees of the mode
            featuredNotes = GetFeaturedMidiNotesForMode(mode, registerMin, registerMax);

            // Show the keyboard first (ensure GameObject is active before starting coroutine)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false; // Non-interactive
                canvasGroup.blocksRaycasts = false; // Don't block raycasts
            }

            // Ensure this GameObject is active (required for coroutines)
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // Ensure keysParent is also active (keys need to be in active hierarchy)
            if (keysParent != null && !keysParent.gameObject.activeSelf)
                keysParent.gameObject.SetActive(true);

            // Update opacity
            // If keyboard was already built (reusing keys), update immediately
            // Also start coroutine as safety net to catch any keys that weren't initialized yet
            bool keyboardWasAlreadyBuilt = keyboardBuilt && _keyByNote.Count > 0;
            
            if (keyboardWasAlreadyBuilt)
            {
                // Keys should already be initialized, so update immediately
                UpdateKeyOpacityForFeaturedNotes(opacityToUse);
            }

            // Always start coroutine if GameObject is active in hierarchy (safety net + handles newly built keys)
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(UpdateKeyOpacityAfterBuild(opacityToUse));
            }
            else
            {
                // If GameObject is not active in hierarchy and keyboard wasn't built, we have a problem
                if (!keyboardWasAlreadyBuilt)
                {
                    Debug.LogWarning("[PianoKeyboardDisplay] GameObject not active in hierarchy and keyboard not built. Opacity update may not apply correctly. Ensure parent GameObject is active before calling ShowForMode().");
                }
            }
        }

        /// <summary>
        /// Builds the keyboard for the given MIDI note range.
        /// This range is fixed and always the same, regardless of the selected level.
        /// </summary>
        /// <param name="displayMinMidi">Minimum MIDI note to display</param>
        /// <param name="displayMaxMidi">Maximum MIDI note to display</param>
        void BuildKeyboard(int displayMinMidi, int displayMaxMidi)
        {
            if (keysParent == null)
            {
                Debug.LogError("[PianoKeyboardDisplay] Cannot build keyboard: keysParent is null");
                return;
            }

            if (whiteKeyPrefab == null || blackKeyPrefab == null)
            {
                Debug.LogError("[PianoKeyboardDisplay] Cannot build keyboard: key prefabs are null");
                return;
            }

            // Ensure keysParent is active so instantiated keys will have Awake() run immediately
            if (keysParent.gameObject != null && !keysParent.gameObject.activeSelf)
                keysParent.gameObject.SetActive(true);

            // Clean up old keys
            for (int i = keysParent.childCount - 1; i >= 0; i--)
            {
                Destroy(keysParent.GetChild(i).gameObject);
            }
            _keyByNote.Clear();

            // First pass: create all white keys, track X positions
            var whiteX = new Dictionary<int, float>(); // midi note -> x pos for its white
            float x = 0f;

            for (int n = displayMinMidi; n <= displayMaxMidi; n++)
            {
                if (!IsWhite(PitchClass(n))) continue;

                var key = Instantiate(whiteKeyPrefab, keysParent);
                key.midiNote = n;

                // Ensure key is active immediately so Awake() runs and Image component is initialized
                if (!key.gameObject.activeSelf)
                    key.gameObject.SetActive(true);

                var rt = (RectTransform)key.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(whiteWidth, whiteHeight);

                // Disable interactivity on the key (after it's active so components are initialized)
                DisableKeyInteractivity(key);

                _keyByNote[n] = key;
                whiteX[n] = x;

                x += whiteWidth + whiteSpacing;
            }

            // Second pass: create black keys
            float blackW = whiteWidth * blackWidthRatio;
            float blackH = whiteHeight * blackHeightRatio;
            float blackOffset = whiteWidth * blackHorizontalOffset;

            for (int n = displayMinMidi; n <= displayMaxMidi; n++)
            {
                int pc = PitchClass(n);
                if (IsWhite(pc)) continue;

                // Find the white key to the left
                int leftWhite = n - 1;
                while (leftWhite >= displayMinMidi && !IsWhite(PitchClass(leftWhite))) leftWhite--;
                if (!whiteX.ContainsKey(leftWhite)) continue;

                float baseX = whiteX[leftWhite];

                var key = Instantiate(blackKeyPrefab, keysParent);
                key.midiNote = n;

                // Ensure key is active immediately so Awake() runs and Image component is initialized
                if (!key.gameObject.activeSelf)
                    key.gameObject.SetActive(true);

                var rt = (RectTransform)key.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(baseX + blackOffset - blackW * 0.5f, whiteHeight - blackH);
                rt.sizeDelta = new Vector2(blackW, blackH);

                key.transform.SetAsLastSibling();

                // Disable interactivity on the key (after it's active so components are initialized)
                DisableKeyInteractivity(key);

                _keyByNote[n] = key;
            }

            // Optionally stretch parent to fit
            var totalWidth = x;
            var parentRT = (RectTransform)keysParent;
            var size = parentRT.sizeDelta;

            // Set container height
            float heightToUse = containerHeight > 0f ? containerHeight : Mathf.Max(size.y, whiteHeight);

            // Handle layout groups
            var layoutElement = keysParent.GetComponent<LayoutElement>();
            if (ignoreLayoutGroups)
            {
                if (layoutElement == null)
                    layoutElement = keysParent.gameObject.AddComponent<LayoutElement>();
                layoutElement.ignoreLayout = true;
            }

            parentRT.sizeDelta = new Vector2(totalWidth, heightToUse);
        }

        /// <summary>
        /// Disables interactivity on a piano key (makes it non-clickable).
        /// PianoKeyUI uses IPointerDownHandler/IPointerUpHandler which Unity's EventSystem handles.
        /// By disabling raycastTarget, pointer events won't reach the key.
        /// Also ensures the key's Image is initialized so SetOpacity() will work correctly.
        /// </summary>
        void DisableKeyInteractivity(PianoKeyUI key)
        {
            if (key == null) return;

            // Ensure key is active so Awake() runs and Image component is initialized
            if (!key.gameObject.activeSelf)
                key.gameObject.SetActive(true);

            // Force initialization by accessing the Image component
            var image = key.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                
                // Force the Image to be initialized by accessing its color
                // This ensures PianoKeyUI's Awake() has run and _img is set
                var _ = image.color;
            }
        }

        /// <summary>
        /// Gets the set of MIDI notes that correspond to allowed scale degrees in the given mode and register.
        /// Returns empty set if unrestricted (null or all-false allowedDegrees).
        /// </summary>
        static HashSet<int> GetFeaturedMidiNotes(ScaleMode mode, bool[] allowedDegrees, int registerMin, int registerMax)
        {
            var result = new HashSet<int>();

            // If unrestricted, return empty set (all keys will be shown at full opacity)
            if (allowedDegrees == null || allowedDegrees.Length != 7)
                return result;

            bool anyAllowed = false;
            for (int i = 0; i < 7; i++)
            {
                if (allowedDegrees[i])
                {
                    anyAllowed = true;
                    break;
                }
            }
            if (!anyAllowed)
                return result;

            // Get pitch-class array for the mode (same mapping as PianoKeyboardUI and MelodyGenerator)
            int[] degreeOrder = mode switch
            {
                ScaleMode.Ionian => new int[] { 0, 2, 4, 5, 7, 9, 11 },
                ScaleMode.Dorian => new int[] { 0, 2, 3, 5, 7, 9, 10 },
                ScaleMode.Phrygian => new int[] { 0, 1, 3, 5, 7, 8, 10 },
                ScaleMode.Lydian => new int[] { 0, 2, 4, 6, 7, 9, 11 },
                ScaleMode.Mixolydian => new int[] { 0, 2, 4, 5, 7, 9, 10 },
                ScaleMode.Aeolian => new int[] { 0, 2, 3, 5, 7, 8, 10 },
                _ => new int[] { 0, 2, 4, 5, 7, 9, 11 } // Ionian fallback
            };

            // For each allowed degree (1-7), collect all MIDI notes in register with matching pitch-class
            for (int degree = 1; degree <= 7; degree++)
            {
                if (!allowedDegrees[degree - 1]) continue; // Skip if this degree is not allowed

                int targetPC = degreeOrder[degree - 1]; // Pitch-class for this degree

                // Collect all MIDI notes in register range with this pitch-class
                for (int midi = registerMin; midi <= registerMax; midi++)
                {
                    if (PitchClass(midi) == targetPC)
                    {
                        result.Add(midi);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the set of MIDI notes that correspond to all 7 scale degrees in the given mode and register.
        /// Used for displaying a complete mode (e.g., for unlock announcements).
        /// </summary>
        static HashSet<int> GetFeaturedMidiNotesForMode(ScaleMode mode, int registerMin, int registerMax)
        {
            // All 7 degrees are enabled
            bool[] allDegrees = new bool[7] { true, true, true, true, true, true, true };
            return GetFeaturedMidiNotes(mode, allDegrees, registerMin, registerMax);
        }

        /// <summary>
        /// Updates the opacity of all keys based on whether they are featured.
        /// Featured keys: opacity 1.0, Non-featured keys: opacity from nonFeaturedKeyOpacity or provided value.
        /// </summary>
        /// <param name="nonFeaturedOpacityOverride">Optional opacity for non-featured keys. If null, uses nonFeaturedKeyOpacity field.</param>
        void UpdateKeyOpacityForFeaturedNotes(float? nonFeaturedOpacityOverride = null)
        {
            float opacityToUse = nonFeaturedOpacityOverride ?? nonFeaturedKeyOpacity;

            // If no featured notes (unrestricted), show all keys at full opacity
            if (featuredNotes.Count == 0)
            {
                foreach (var kvp in _keyByNote)
                {
                    if (kvp.Value != null)
                    {
                        // Ensure key is active and Image is initialized
                        if (!kvp.Value.gameObject.activeSelf)
                            kvp.Value.gameObject.SetActive(true);
                        
                        // Force initialization by getting Image component
                        var image = kvp.Value.GetComponent<Image>();
                        if (image != null)
                        {
                            kvp.Value.SetOpacity(1.0f);
                        }
                    }
                }
                return;
            }

            // Set opacity based on whether key is featured
            foreach (var kvp in _keyByNote)
            {
                if (kvp.Value == null) continue;

                // Ensure key is active and Image is initialized
                if (!kvp.Value.gameObject.activeSelf)
                    kvp.Value.gameObject.SetActive(true);
                
                // Force initialization by getting Image component
                var image = kvp.Value.GetComponent<Image>();
                if (image == null) continue;

                bool isFeatured = featuredNotes.Contains(kvp.Key);
                float opacity = isFeatured ? 1.0f : opacityToUse;
                kvp.Value.SetOpacity(opacity);
            }
        }

        /// <summary>
        /// Coroutine that updates key opacity after a brief delay to ensure keys are fully initialized.
        /// </summary>
        /// <param name="nonFeaturedOpacityOverride">Optional opacity for non-featured keys. If null, uses nonFeaturedKeyOpacity field.</param>
        System.Collections.IEnumerator UpdateKeyOpacityAfterBuild(float? nonFeaturedOpacityOverride = null)
        {
            // Wait one frame to ensure all keys are fully initialized (using unscaled time since time might be paused)
            yield return null;
            
            // Now update opacity with the specified override
            UpdateKeyOpacityForFeaturedNotes(nonFeaturedOpacityOverride);
        }

        // Helper methods
        bool IsWhite(int pc)
        {
            for (int i = 0; i < whitePCs.Length; i++)
                if (whitePCs[i] == pc) return true;
            return false;
        }

        static int PitchClass(int midi) => midi % 12;
    }
}

