using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] string intervalSceneName = "IntervalTrainer";     // set in Inspector
    [SerializeField] string dictationSceneName = "MelodicDictation";    // set in Inspector

    [Header("UI")]
    [SerializeField] Button btnInterval;
    [SerializeField] Button btnDictation;
    [SerializeField] TMP_Text intervalTopScoreText;
    [SerializeField] TMP_Text dictationTopScoreText;

    const string KeyInterval = "HighScore_Intervals";
    const string KeyDictation = "HighScore_Dictation";

    void Awake()
    {
        if (btnInterval)  btnInterval.onClick.AddListener(() => LoadScene(intervalSceneName));
        if (btnDictation) btnDictation.onClick.AddListener(() => LoadScene(dictationSceneName));
    }

    void Start()
    {
        // Ensure cursor is unlocked when MainMenu loads (safety measure)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshScores();
    }

    void RefreshScores()
    {
        int s1 = PlayerPrefs.GetInt(KeyInterval, 0);
        int s2 = PlayerPrefs.GetInt(KeyDictation, 0);
        if (intervalTopScoreText)  intervalTopScoreText.text  = $"Top Score: {s1:n0}";
        if (dictationTopScoreText) dictationTopScoreText.text = $"Top Score: {s2:n0}";
    }

    static void LoadScene(string name)
    {
        if (!string.IsNullOrEmpty(name))
            SceneManager.LoadScene(name, LoadSceneMode.Single);
    }

    // Static helpers if you want to refresh from other scripts later.
    public static void SetIntervalHighScore(int score)
    {
        if (score > PlayerPrefs.GetInt(KeyInterval, 0)) PlayerPrefs.SetInt(KeyInterval, score);
    }
    public static void SetDictationHighScore(int score)
    {
        if (score > PlayerPrefs.GetInt(KeyDictation, 0)) PlayerPrefs.SetInt(KeyDictation, score);
    }

    public void ResetScores()
    {
        PlayerPrefs.DeleteKey("HighScore_Intervals");
        PlayerPrefs.DeleteKey("HighScore_Dictation");
        PlayerPrefs.Save();
        RefreshScores();
    }

    // Right-click > Reset Top Scores (Editor) on this component
    #if UNITY_EDITOR
    [ContextMenu("Reset Top Scores (Editor)")]
    private void ResetTopScoresContext()
    {
        ResetScores();
    }
    #endif
}
