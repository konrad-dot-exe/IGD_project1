using UnityEngine;
using TMPro;

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
        [SerializeField] CanvasGroup screenFlash;
        [SerializeField] GameObject worldTextPrefab; // a TMP world-space text prefab

        public void SetScore(int score, int delta)
        {
            scoreText.text = $"Score: {score} {(delta!=0 ? (delta>0? $"+{delta}" : $"{delta}") : "")}";
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
    }

    // tiny helper for bestStreak display (optional)
    public static class GameManagerStats { public static int bestStreakTemp = 0; }
}
