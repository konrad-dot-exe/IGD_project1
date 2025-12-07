using UnityEngine;
using FMODUnity;

[DisallowMultipleComponent]
public class FmodSfxPlayer : MonoBehaviour
{
    [Header("FMOD Events")]
    public EventReference uiWin;
    public EventReference uiWrong;
    public EventReference stingerGameOver;
    public EventReference uiCardsSweep;
    public EventReference lightning;
    public EventReference thunder;
    public EventReference cardSlap;
    public EventReference sparkles;
    public EventReference levelComplete;
    public EventReference buttonClick;

    [Header("Debug")]
    [Tooltip("Enable debug logging to track sound playback")]
    [SerializeField] bool debugLog = false;

    // Sound call counters for debugging
    private static int _lightningCount = 0;
    private static int _thunderCount = 0;
    private static int _cardSlapCount = 0;
    private static float _lastLogTime = 0f;

    public void PlayWin()        => Play(uiWin, "Win");
    public void PlayWrong()      => Play(uiWrong, "Wrong");
    public void PlayGameOver()   => Play(stingerGameOver, "GameOver");
    public void PlayCardsSweep() => Play(uiCardsSweep, "CardsSweep");
    public void PlayLightning() => Play(lightning, "Lightning");
    public void PlayThunder() => Play(thunder, "Thunder");
    public void PlayCardSlap() => Play(cardSlap, "CardSlap");
    public void PlaySparkles() => Play(sparkles, "Sparkles");
    public void PlayLevelComplete() => Play(levelComplete, "LevelComplete");
    public void PlayButtonClick() => Play(buttonClick, "ButtonClick");

    void Play(EventReference ev, string soundName = "Unknown")
    {
        if (ev.IsNull) return;                       // safe no-op if unassigned
        
        // Track sound counts for debugging
        if (soundName == "Lightning") _lightningCount++;
        else if (soundName == "Thunder") _thunderCount++;
        else if (soundName == "CardSlap") _cardSlapCount++;
        
        // Log periodically to avoid spam
        if (debugLog && Time.time - _lastLogTime > 1f)
        {
            Debug.Log($"[FmodSfxPlayer] Sound stats - Lightning: {_lightningCount}, Thunder: {_thunderCount}, CardSlap: {_cardSlapCount}");
            _lastLogTime = Time.time;
        }
        
        if (debugLog)
        {
            Debug.Log($"[FmodSfxPlayer] Playing: {soundName} at {Time.time:F3}s");
        }
        
        RuntimeManager.PlayOneShot(ev, transform.position);
    }

    void OnDestroy()
    {
        // Reset counters when component is destroyed
        _lightningCount = 0;
        _thunderCount = 0;
        _cardSlapCount = 0;
    }
}
