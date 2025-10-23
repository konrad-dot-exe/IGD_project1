// EndScreenController.cs — multi-module end screen
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace EarFPS
{
    public class EndScreenController : MonoBehaviour
    {
        public enum Mode { Interval, Dictation }
        [Header("General")]
        [SerializeField] Mode mode = Mode.Interval;           // <- pick per scene
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI title;

        [Header("Interval Refs (Interval FPS)")]
        [SerializeField] TextMeshProUGUI statScore;
        [SerializeField] TextMeshProUGUI statTime;
        [SerializeField] TextMeshProUGUI statAccuracy;
        [SerializeField] TextMeshProUGUI statDestroyed;
        [SerializeField] TextMeshProUGUI statBestStreak;

        [Header("Dictation Refs (Melodic Dictation)")]
        [SerializeField] TextMeshProUGUI dictScore;
        [SerializeField] TextMeshProUGUI dictTime;
        [SerializeField] TextMeshProUGUI dictRounds;

        [Header("Buttons")]
        [SerializeField] Button btnRetry;
        [SerializeField] Button btnDashboard;
        [SerializeField] Selectable firstFocus;

        [Header("Navigation")]
        [SerializeField] string retrySceneName = "";
        [SerializeField] string dashboardSceneName = "MainMenu";

        float _prevTimeScale = 1f;
        CursorLockMode _prevLock;
        bool _prevCursor;

        void Awake()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (btnRetry)     btnRetry.onClick.AddListener(OnClickRetry);
            if (btnDashboard) btnDashboard.onClick.AddListener(OnClickDashboard);
            HideImmediate();
        }

        // Keep existing API for Interval module
        public void Show(RunStats s, string titleOverride = null)
        {
            mode = Mode.Interval;
            ApplyCommonOpen();

            title.text = string.IsNullOrEmpty(titleOverride) ? "Mission Failed!" : titleOverride;

            // Show Interval refs
            SetActive(statScore, true);      statScore.text      = $"Score: <b>{s.score:n0}</b>";
            SetActive(statTime, true);       statTime.text       = $"Time: <b>{FormatTime(s.timeSeconds)}</b>";
            SetActive(statAccuracy, true);   statAccuracy.text   = $"Accuracy: <b>{(s.total > 0 ? Mathf.RoundToInt(100f * s.correct / s.total) : 0)}%</b>";
            SetActive(statDestroyed, true);  statDestroyed.text  = $"Destroyed: <b>{s.enemiesDestroyed}</b>";
            SetActive(statBestStreak, true); statBestStreak.text = $"Best Streak: <b>{s.bestStreak}</b>";

            // Hide Dictation refs
            SetActive(dictScore, false);
            SetActive(dictTime, false);
            SetActive(dictRounds, false);

            FocusFirst();
        }

        // New: Dictation-specific API
        public void ShowDictation(int score, int roundsCompleted, float timeSeconds, string titleOverride = "Game Over")
        {
            mode = Mode.Dictation;
            ApplyCommonOpen();

            title.text = string.IsNullOrEmpty(titleOverride) ? "Game Over" : titleOverride;

            // Hide Interval refs
            SetActive(statScore, false);
            SetActive(statTime, false);
            SetActive(statAccuracy, false);
            SetActive(statDestroyed, false);
            SetActive(statBestStreak, false);

            // Show Dictation refs
            SetActive(dictScore, true);  dictScore.text  = $"Score: <b>{score:n0}</b>";
            SetActive(dictTime,  true);  dictTime.text   = $"Time: <b>{FormatTime(timeSeconds)}</b>";
            SetActive(dictRounds,true);  dictRounds.text = $"Rounds: <b>{roundsCompleted}</b>";

            FocusFirst();
        }

        // Buttons
        public void OnClickRetry()
        {
            Hide();
            string sceneToLoad = string.IsNullOrEmpty(retrySceneName)
                ? SceneManager.GetActiveScene().name
                : retrySceneName;
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
        public void OnClickDashboard()
        {
            Hide();
            if (!string.IsNullOrEmpty(dashboardSceneName))
                SceneManager.LoadScene(dashboardSceneName, LoadSceneMode.Single);
        }

        // Open/close
        void ApplyCommonOpen()
        {
            _prevTimeScale = Time.timeScale;  Time.timeScale = 0f;
            _prevLock = Cursor.lockState;     _prevCursor = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
            gameObject.SetActive(true);
        }
        public void Hide()
        {
            Time.timeScale = _prevTimeScale;
            Cursor.lockState = _prevLock; Cursor.visible = _prevCursor;
            HideImmediate();
        }
        void HideImmediate()
        {
            if (!group) return;
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
        void FocusFirst()
        {
            var es = EventSystem.current;
            if (!es) return;
            GameObject go = (firstFocus ? firstFocus.gameObject : (btnRetry ? btnRetry.gameObject : null));
            es.SetSelectedGameObject(go);
        }

        static void SetActive(Behaviour b, bool active)
        {
            if (!b) return; b.gameObject.SetActive(active);
        }
        static string FormatTime(float t)
        {
            if (t < 0f) t = 0f;
            int total = Mathf.RoundToInt(t);
            int m = total / 60, s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}
