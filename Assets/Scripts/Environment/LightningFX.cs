using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightningFX : MonoBehaviour
{
    [Header("Light")]
    public Light flashLight;                         // Directional light used only for lightning
    public Color flashColor = new(0.75f, 0.85f, 1f); // cool bluish-white
    public float peakIntensity = 3.0f;               // how bright the flash gets
    public float duration = 0.35f;                   // per-flash length
    public bool castShadows = false;                 // usually not needed (fast)

    // --- STORM: chain multiple lightning strikes over a short window ---
    [Header("Storm Defaults")]
    [SerializeField] public float stormDuration = 1.0f;    // total time window for all flashes
    [SerializeField] public int stormMinStrikes = 6;       // inclusive
    [SerializeField] public int stormMaxStrikes = 8;       // inclusive

    [SerializeField] bool  logStormSchedule = false; // optional debug logs

    [Header("Flicker Curve (0..1 time)")]
    // Default curve: fast attack, quick fall, small afterglow
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0.00f, 0.00f,  0,   0),
        new Keyframe(0.03f, 1.00f,  0,  -8),
        new Keyframe(0.10f, 0.25f,  0,   4),
        new Keyframe(0.18f, 0.75f,  0,  -6),
        new Keyframe(0.35f, 0.00f,  0,   0)
    );

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip sfxLightning;                   // short crack
    public AudioClip sfxThunder;                     // rolling thunder
    public Vector2 thunderDelayRange = new(0.25f, 0.8f); // delay to taste

    Coroutine flashRoutine;

    void Reset()
    {
        // Auto create a directional light if you forget to assign one
        if (!flashLight)
        {
            var go = new GameObject("LightningLight");
            go.transform.SetParent(transform, false);
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Directional;
            flashLight.shadows = LightShadows.None;
            flashLight.color = flashColor;
            flashLight.intensity = 0f;
            flashLight.enabled = false;
        }
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>Public entry point. pattern: 0=single, 1=double flicker.</summary>
    public void Strike(int pattern = 1)
    {
        // stop only the ongoing flash, not the storm scheduler
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(DoStrike(pattern));
    }
    IEnumerator DoStrike(int pattern)
    {
        if (sfxLightning && audioSource) audioSource.PlayOneShot(sfxLightning, 0.5f);

        // Aim light slightly from the sky
        transform.rotation = Quaternion.Euler(Random.Range(5, 20), Random.Range(20, 160), 0);

        yield return FlashOnce(1f);
        if (pattern == 1)
        {
            yield return new WaitForSeconds(0.06f);
            yield return FlashOnce(0.6f);
        }

        if (sfxThunder && audioSource)
        {
            float delay = Random.Range(thunderDelayRange.x, thunderDelayRange.y);
            yield return new WaitForSeconds(delay);
            audioSource.PlayOneShot(sfxThunder, 1f);
        }
    }

    IEnumerator FlashOnce(float scale)
    {
        if (!flashLight) yield break;

        var prevColor = flashLight.color;
        var prevInt = flashLight.intensity;
        var prevShadows = flashLight.shadows;

        flashLight.color = flashColor;
        flashLight.intensity = 0f;
        flashLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        flashLight.enabled = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = intensityCurve.Evaluate(Mathf.Clamp01(t / duration));
            flashLight.intensity = peakIntensity * scale * k;
            yield return null;
        }

        flashLight.enabled = false;
        flashLight.intensity = prevInt;
        flashLight.color = prevColor;
        flashLight.shadows = prevShadows;
    }

    public Coroutine Storm(float durationOverride = -1f, int minStrikes = -1, int maxStrikes = -1)
    {
        if (durationOverride <= 0f) durationOverride = stormDuration;
        if (minStrikes   < 0)      minStrikes       = stormMinStrikes;
        if (maxStrikes   < 0)      maxStrikes       = stormMaxStrikes;
        return StartCoroutine(StormCo(durationOverride, minStrikes, maxStrikes));
    }

    IEnumerator StormCo(float duration, int minStrikes, int maxStrikes)
    {
        int strikes = Mathf.Clamp(Random.Range(minStrikes, maxStrikes + 1), 0, 512);
        if (strikes <= 0) yield break;

        // schedule uneven timings within [0, duration]
        var times = new List<float>(strikes);
        for (int i = 0; i < strikes; i++) times.Add(Random.Range(0f, duration));
        times.Sort();

        if (logStormSchedule)
        {
            Debug.Log($"[LightningFX] Storm scheduled: duration={duration:F2}s, strikes={strikes}");
            for (int i = 0; i < times.Count; i++) Debug.Log($"  t[{i}] = {times[i]:F3}s");
        }

        float cursor = 0f;
        for (int i = 0; i < times.Count; i++)
        {
            float wait = Mathf.Max(0f, times[i] - cursor);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            cursor = times[i];

            // double-flicker ~60% of the time
            int pattern = (Random.value < 0.6f) ? 1 : 0;
            Strike(pattern);

            // optional micro-jitter AFTER a strike (keeps rhythm organic)
            // Comment out if you want the schedule to match exactly the duration window.
            yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));
        }
    }

    public Coroutine PlayStorm(float duration, int minStrikes, int maxStrikes, bool log = false)
    {
        return StartCoroutine(StormCo(duration, minStrikes, maxStrikes));
    }

    // --- DEBUG MENUS ---
    [ContextMenu("Debug/Storm (2s fixed)")]
    void _dbg_StormFixed2s() => Storm(2f, 6, 8);

    [ContextMenu("Debug/Storm (Inspector Settings)")]
    void _dbg_StormFromInspector() => Storm();   // uses stormDuration/min/max from Inspector
}
