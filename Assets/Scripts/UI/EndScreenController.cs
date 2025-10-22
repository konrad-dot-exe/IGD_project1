// EndScreenController.cs (additions/changes)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace EarFPS
{
    public class EndScreenController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI statScore;
        [SerializeField] TextMeshProUGUI statTime;
        [SerializeField] TextMeshProUGUI statAccuracy;
        [SerializeField] TextMeshProUGUI statDestroyed;
        [SerializeField] TextMeshProUGUI statBestStreak;

        [Header("Buttons")]
        [SerializeField] Button btnRetry;
        [SerializeField] Button btnDashboard;
        [SerializeField] Selectable firstFocus; // e.g., btnRetry

        float _prevTimeScale = 1f;
        CursorLockMode _prevLock;
        bool _prevCursor;

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        public void Show(RunStats s, string titleOverride = null)
        {
            // 1) Set stats text
            if (!string.IsNullOrEmpty(titleOverride))
                title.text = titleOverride;
            else
                title.text = "Challenege Failed!";
            statScore.text = $"Score: <b>{s.score:n0}</b>";
            statTime.text = $"Time: <b>{FormatTime(s.timeSeconds)}</b>";
            statAccuracy.text = $"Accuracy: <b>{(s.total > 0 ? Mathf.RoundToInt(100f * s.correct / s.total) : 0)}%</b>";
            statDestroyed.text = $"Destroyed: <b>{s.enemiesDestroyed}</b>";
            statBestStreak.text = $"Best Streak: <b>{s.bestStreak}</b>";

            // 2) Pause world + unlock cursor
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _prevLock = Cursor.lockState;
            _prevCursor = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 3) Enable input & raycast blocking
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            gameObject.SetActive(true);

            // 4) Focus a button for keyboard/pad
            var es = EventSystem.current;
            if (es)
            {
                GameObject go = (firstFocus ? firstFocus.gameObject : (btnRetry ? btnRetry.gameObject : null));
                es.SetSelectedGameObject(go);
            }
        }

        public void Hide()
        {
            // restore time + cursor
            Time.timeScale = _prevTimeScale;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevCursor;

            HideImmediate();
        }

        void HideImmediate()
        {
            if (!group) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        static string FormatTime(float t)
        {
            if (t < 0f) t = 0f;
            int total = Mathf.RoundToInt(t);
            int m = total / 60;
            int s = total % 60;
            return $"{m:00}:{s:00}";
        }

    }
}
