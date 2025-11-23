using UnityEngine;
using UnityEngine.EventSystems;

namespace EarFPS
{
    /// <summary>
    /// Changes the cursor to a pointing hand icon when hovering over a button.
    /// Attach this component to any UI button to enable cursor change on hover.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public class ButtonCursorHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Cursor Settings")]
        [Tooltip("Cursor texture to show on hover (pointing hand). If not assigned, will use system default.")]
        [SerializeField] Texture2D handCursorTexture;

        [Tooltip("Hotspot position for the cursor (typically the tip of the pointing finger).")]
        [SerializeField] Vector2 cursorHotspot = new Vector2(5, 2);

        private bool isUsingCustomCursor = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Only change cursor if button is interactable
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button != null && !button.interactable)
                return;

            // Change to hand cursor
            if (handCursorTexture != null)
            {
                Cursor.SetCursor(handCursorTexture, cursorHotspot, CursorMode.Auto);
                isUsingCustomCursor = true;
            }
            else
            {
                // Use system default hand cursor (if available)
                // On Windows, this would be the standard pointing hand
                // For cross-platform, you may want to use a custom texture
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Restore default cursor
            if (isUsingCustomCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isUsingCustomCursor = false;
            }
        }

        void OnDisable()
        {
            // Ensure cursor is reset when component is disabled
            if (isUsingCustomCursor)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isUsingCustomCursor = false;
            }
        }
    }
}

