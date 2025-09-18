using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;  

namespace EarFPS
{
    public class UIHud : MonoBehaviour
    {
        public static UIHud Instance { get; private set; }
        void Awake() { Instance = this; }



        [Header("Labels")]
        [SerializeField] TextMeshProUGUI scoreText;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI remainingText;
        [SerializeField] TextMeshProUGUI accuracyText;
        [SerializeField] TextMeshProUGUI streakText;
        [SerializeField] TextMeshProUGUI selectedIntervalText;

        [Header("FX")]
        [SerializeField] GameObject worldTextPrefab; // a TMP world-space text prefab
        [Header("Screen Flash")]
        [SerializeField] CanvasGroup screenFlash;
        [SerializeField] CanvasGroup screenFlashGroup;   // alpha starts at 0
        [SerializeField] Image screenFlashImage;   // the fullscreen Image
        Coroutine hitCo;



        [Header("Toast")]
        [SerializeField] CanvasGroup toastGroup;       // CanvasGroup on the toast label (alpha starts at 0)
        [SerializeField] TextMeshProUGUI toastText;    // TMP label for messages
        [SerializeField] float toastFadeIn = 0.12f;
        [SerializeField] float toastHold = 0.8f;
        [SerializeField] float toastFadeOut = 0.18f;

        Coroutine toastCo;

        public void SetScore(int score, int delta)
        {
            scoreText.text = $"Score: {score} {(delta != 0 ? (delta > 0 ? $"+{delta}" : $"{delta}") : "")}";
        }
        public void SetTimer(float sec)
        {
            int m = Mathf.FloorToInt(sec / 60f);
            int s = Mathf.FloorToInt(sec % 60f);
            timerText.text = $"Time: {m:00}:{s:00}";
        }
        public void SetRemaining(int r) => remainingText.text = $"Remaining: {r}";
        public void SetAccuracy(int correct, int attempts)
        {
            float acc = attempts == 0 ? 0f : (100f * correct / attempts);
            accuracyText.text = $"Accuracy: {acc:0}%";
            streakText.text = $"Best Streak: {GameManagerStats.bestStreakTemp}"; // optional, updated below
        }
        public void SetSelectedInterval(IntervalDef def)
        {
            selectedIntervalText.text = $"{def.shortName} — {def.displayName}";
        }

        public void ToastCorrect(string name, Vector3 worldPos)
        {
            if (!worldTextPrefab) return;
            var go = Instantiate(worldTextPrefab, worldPos, Quaternion.identity);
            var toast = go.GetComponent<WorldToast>();
            if (toast) toast.SetText(name + "!");
            else
            {
                // fallback if script not present
                var tmp = go.GetComponent<TextMeshPro>();
                if (tmp) tmp.text = name + "!";
            }
        }

        public void FlashWrong()
        {
            if (!screenFlash) return;
            StopAllCoroutines();
            StartCoroutine(FlashCo());
        }
        System.Collections.IEnumerator FlashCo()
        {
            screenFlash.alpha = 0.7f;
            yield return new WaitForSeconds(0.25f);
            screenFlash.alpha = 0f;
        }

        // Optional stat pass-throughs
        public void ShowWin(int score, float time, int bestStreak, int correct, int attempts)
        {
            // For v1, just log. You can add a panel later.
            Debug.Log($"WIN! Score {score} Time {time:F1} BestStreak {bestStreak} {correct}/{attempts}");
        }
        public void ShowLose(int score, float time, int bestStreak, int correct, int attempts)
        {
            Debug.Log($"LOSE. Score {score} Time {time:F1} BestStreak {bestStreak} {correct}/{attempts}");
        }

        public void Toast(string msg, float? holdOverride = null)
        {
            if (!toastGroup || !toastText) { Debug.Log(msg); return; }
            if (toastCo != null) StopCoroutine(toastCo);
            toastCo = StartCoroutine(ToastRoutine(msg, holdOverride ?? toastHold));
        }

        IEnumerator ToastRoutine(string msg, float hold)
        {
            toastText.text = msg;
            yield return FadeCanvas(toastGroup, toastGroup.alpha, 1f, toastFadeIn);
            yield return new WaitForSeconds(hold);
            yield return FadeCanvas(toastGroup, 1f, 0f, toastFadeOut);
        }

        static IEnumerator FadeCanvas(CanvasGroup g, float a, float b, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(a, b, t / time);
                yield return null;
            }
            g.alpha = b;
        }
        
        public void HitStrobe(int pulses = 3, float on = 0.06f, float off = 0.05f, Color? color = null)
        {
            if (!screenFlashGroup || !screenFlashImage) return;
            if (hitCo != null) StopCoroutine(hitCo);
            hitCo = StartCoroutine(HitStrobeCo(pulses, on, off, color ?? Color.white));
        }

        IEnumerator HitStrobeCo(int pulses, float on, float off, Color color)
        {
            var oldColor = screenFlashImage.color;
            screenFlashImage.color = new Color(color.r, color.g, color.b, 1f);

            for (int i = 0; i < pulses; i++)
            {
                // quick up
                yield return Fade(screenFlashGroup, screenFlashGroup.alpha, 1f, 0.035f);
                yield return new WaitForSecondsRealtime(on);
                // quick down
                yield return Fade(screenFlashGroup, 1f, 0f, 0.08f);
                if (i < pulses - 1) yield return new WaitForSecondsRealtime(off);
            }

            screenFlashImage.color = oldColor;
            hitCo = null;
        }

        static IEnumerator Fade(CanvasGroup g, float a, float b, float t)
        {
            float s = 0f;
            while (s < t)
            {
                s += Time.unscaledDeltaTime;
                g.alpha = Mathf.Lerp(a, b, s / t);
                yield return null;
            }
            g.alpha = b;
        }


    }

    // tiny helper for bestStreak display (optional)
    public static class GameManagerStats { public static int bestStreakTemp = 0; }
}
