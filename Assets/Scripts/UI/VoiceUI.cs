// Assets/Scripts/UI/VoiceUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EarFPS
{
    /// <summary>
    /// Minimal voice status UI: shows "Listening…" while PTT is down,
    /// then a short success/fail label after a recognition/submit.
    /// No processing pill/spinner.
    /// </summary>
    public class VoiceUI : MonoBehaviour
    {
        public static VoiceUI Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] CanvasGroup group;          // optional (for dimming)
        [SerializeField] Image        micFill;       // optional tint only
        [SerializeField] TextMeshProUGUI statusText; // main status label

        [Header("Colors")]
        [SerializeField] Color listeningColor = new(0.3f, 0.9f, 1f);
        [SerializeField] Color successColor   = new(0.2f, 1f, 0.6f);
        [SerializeField] Color failColor      = new(1f, 0.3f, 0.6f);

        [Header("Behavior")]
        [SerializeField] float resultHoldSeconds = 0.8f; // how long OK/FAIL stays

        float clearAt = -1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetText("");
        }

        void Update()
        {
            if (clearAt > 0f && Time.unscaledTime >= clearAt)
            {
                clearAt = -1f;
                SetText("");
            }
        }

        public void SetListening(bool on)
        {
            // Text & colors
            SetColor(listeningColor);
            SetText(on ? "Listening…" : "");
            if (group) group.alpha = on ? 1f : 0.7f;
            // keep micFill tinted to listeningColor while listening
            if (micFill) micFill.color = new Color(listeningColor.r, listeningColor.g, listeningColor.b, micFill.color.a);
        }

        public void ShowHeard(string raw) { /* optional: no-op now */ }

        /// <summary>Show success/fail and auto-clear after ResultHoldSeconds.</summary>
        public void ShowResult(bool success, string label)
        {
            SetColor(success ? successColor : failColor);
            SetText(success ? $" {label}" : $" {label}");
            clearAt = Time.unscaledTime + resultHoldSeconds;
        }

        // ---- helpers ----
        void SetText(string t) { if (statusText) statusText.text = t; }
        void SetColor(Color c)
        {
            if (statusText) statusText.color = c;
            if (micFill) micFill.color = new Color(c.r, c.g, c.b, micFill ? micFill.color.a : 1f);
        }
    }
}
