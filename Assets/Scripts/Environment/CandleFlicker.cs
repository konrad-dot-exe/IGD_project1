using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class CandleFlicker : MonoBehaviour
{
    [Header("Light References")]
    [Tooltip("The candle cone (Spot Light) above the flame.")]
    public Light spotLight;                // your CandleLight (type=Spot)
    [Tooltip("Optional soft fill (Point Light) at the flame.")]
    public Light fillPointLight;           // your Point Light (type=Point)

    [Header("Flame Visuals (optional)")]
    public Renderer flameRenderer;         // emissive mesh/quad
    public ParticleSystem flameParticles;  // optional particle flame

    [Header("Flicker Settings")]
    [Tooltip("Base intensity for the Spot Light (cone).")]
    public float spotBaseIntensity = 1f;
    [Tooltip("Base intensity for the Point Light (fill).")]
    public float pointBaseIntensity = 0.025f;
    public float flickerAmplitude = 0.35f;
    public float flickerSpeed = 1.6f;

    [Header("Emission (renderer flame)")]
    public Color emissionColor = new(1f, 0.66f, 0.29f);
    public float emissionBase = 2.2f;
    public float emissionAmp  = 0.7f;

    [Header("Extinguish / Ignite Durations")]
    public float defaultFadeSeconds = 0.30f;
    public float defaultRiseSeconds = 0.30f;

    // --- Violent Flicker Override ----------------------------------------------
    [Header("Violent Flicker (override)")]
    [Tooltip("Default amplitude multiplier when violent flicker is triggered.")]
    public float violentAmpMultiplier   = 2.2f;
    [Tooltip("Default speed multiplier when violent flicker is triggered.")]
    public float violentSpeedMultiplier = 2.4f;
    [Tooltip("Optional base-intensity boost during violent flicker.")]
    public float violentBaseBoost = 1.12f;
    
    // ================= Debug Self-Test =================
    #region Debug Self-Test

    [Header("Debug Self-Test (optional)")]
    [SerializeField] float dbgFlickerDur   = 0.70f;
    [SerializeField] float dbgAmpMul       = 2.0f;  // >1 = wilder brightness modulation
    [SerializeField] float dbgSpeedMul     = 2.0f;  // >1 = faster noise
    [SerializeField] float dbgBaseMul      = 1.25f; // >1 = slightly brighter cone/fill during gust

    [SerializeField] float dbgExtinguishFade = 0.35f;
    [SerializeField] float dbgRelightRise   = 0.35f;
    Coroutine _dbgRoutine;
    Coroutine violentRoutine;

    // ---- internal ----
    float seed;
    bool isLit = true;
    Coroutine fadeRoutine;
    float cachedSpotBase, cachedPointBase, cachedEmissionBase;
    float cachedFlickerAmplitude, cachedFlickerSpeed; // Cache for violent flicker restoration

    public bool IsLit => isLit;

    void Awake()
    {
        seed = Random.value * 100f;

        cachedSpotBase    = spotBaseIntensity;
        cachedPointBase   = pointBaseIntensity;
        cachedEmissionBase= emissionBase;
        cachedFlickerAmplitude = flickerAmplitude;
        cachedFlickerSpeed = flickerSpeed;

        if (flameRenderer)
            flameRenderer.material.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        if (!isLit) return;

        float t = Time.time * flickerSpeed;
        float n = Mathf.PerlinNoise(seed + t, seed - t);
        float f = Mathf.Lerp(1f - flickerAmplitude, 1f + flickerAmplitude, n);

        // spot cone
        if (spotLight) spotLight.intensity = spotBaseIntensity * f;

        // point fill (lower, softer)
        if (fillPointLight) fillPointLight.intensity = pointBaseIntensity * f;

        // emissive flicker (renderer)
        if (flameRenderer)
        {
            float e = emissionBase + (n - 0.5f) * emissionAmp;
            flameRenderer.material.SetColor("_EmissionColor",
                emissionColor * Mathf.LinearToGammaSpace(e));
        }
    }

    // ---------- API ----------
    public void Extinguish(float delay = 0f, float fadeSeconds = -1f)
    {
        if (fadeSeconds <= 0f) fadeSeconds = defaultFadeSeconds;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(ExtinguishCo(delay, fadeSeconds));
    }

    public void Ignite(float riseSeconds = -1f)
    {
        if (riseSeconds <= 0f) riseSeconds = defaultRiseSeconds;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        
        // Stop any active violent flicker coroutine and restore base values immediately
        if (_dbgRoutine != null)
        {
            StopCoroutine(_dbgRoutine);
            _dbgRoutine = null;
            // Restore base values that may have been modified by violent flicker
            flickerAmplitude = cachedFlickerAmplitude;
            flickerSpeed = cachedFlickerSpeed;
            spotBaseIntensity = cachedSpotBase;
            pointBaseIntensity = cachedPointBase;
        }
        
        fadeRoutine = StartCoroutine(IgniteCo(riseSeconds));
    }

    IEnumerator ExtinguishCo(float delay, float fadeSeconds)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        isLit = false; // pause flicker
        float t = 0f;

        float startSpot   = (spotLight      ? spotLight.intensity      : 0f);
        float startPoint  = (fillPointLight ? fillPointLight.intensity : 0f);
        float startEmBase = emissionBase;

        // optional particle lifetime shrink
        ParticleSystem.MainModule main = default;
        float startLifetime = 0f;
        if (flameParticles)
        {
            main = flameParticles.main;
            startLifetime = main.startLifetime.constant;
        }

        while (t < fadeSeconds)
        {
            float u = t / fadeSeconds;

            if (spotLight)      spotLight.intensity      = Mathf.Lerp(startSpot,  0f, u);
            if (fillPointLight) fillPointLight.intensity = Mathf.Lerp(startPoint, 0f, u);

            emissionBase = Mathf.Lerp(startEmBase, 0f, u);
            if (flameRenderer)
                flameRenderer.material.SetColor("_EmissionColor",
                    emissionColor * Mathf.LinearToGammaSpace(emissionBase));

            if (flameParticles)
            {
                float life = Mathf.Lerp(startLifetime, Mathf.Max(0.05f, startLifetime * 0.15f), u);
                main.startLifetime = new ParticleSystem.MinMaxCurve(life);
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (spotLight)      { spotLight.intensity = 0f;      spotLight.enabled = false; }
        if (fillPointLight) { fillPointLight.intensity = 0f;  fillPointLight.enabled = false; }

        emissionBase = 0f;
        if (flameRenderer)
            flameRenderer.material.SetColor("_EmissionColor", Color.black);

        if (flameParticles)
            flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        fadeRoutine = null;
    }

    IEnumerator IgniteCo(float riseSeconds)
    {
        if (spotLight) { spotLight.enabled = true; spotLight.intensity = 0f; }
        if (fillPointLight) { fillPointLight.enabled = true; fillPointLight.intensity = 0f; }
        if (flameParticles)
        {
            var emission = flameParticles.emission;
            emission.enabled = true;
            if (!flameParticles.isPlaying) flameParticles.Play();
        }

        float t = 0f;
        while (t < riseSeconds)
        {
            float u = t / riseSeconds;

            if (spotLight) spotLight.intensity = Mathf.Lerp(0f, cachedSpotBase, u);
            if (fillPointLight) fillPointLight.intensity = Mathf.Lerp(0f, cachedPointBase, u);

            emissionBase = Mathf.Lerp(0f, cachedEmissionBase, u);
            if (flameRenderer)
                flameRenderer.material.SetColor("_EmissionColor",
                    emissionColor * Mathf.LinearToGammaSpace(emissionBase));

            t += Time.deltaTime;
            yield return null;
        }

        if (spotLight) spotLight.intensity = cachedSpotBase;
        if (fillPointLight) fillPointLight.intensity = cachedPointBase;
        emissionBase = cachedEmissionBase;

        isLit = true;
        fadeRoutine = null;
    }


        // ---------- Debug ----------
        [ContextMenu("Debug/Extinguish (now)")] void _dbg_ExtinguishNow() => Extinguish();
        [ContextMenu("Debug/Ignite")] void _dbg_Ignite() => Ignite();
        
    /// <summary>
    /// Temporarily makes the candle flicker harder/faster, then restores settings.
    /// Safe to call while lit; does nothing if already extinguished.
    /// </summary>
    public void ViolentFlicker(float duration, float ampMul = 2f, float speedMul = 2f, float baseMul = 1.2f)
    {
        if (!isLit) return;
        if (_dbgRoutine != null) StopCoroutine(_dbgRoutine);
        _dbgRoutine = StartCoroutine(ViolentFlickerCo(duration, ampMul, speedMul, baseMul));
    }

    IEnumerator ViolentFlickerCo(float duration, float ampMul, float speedMul, float baseMul)
    {
        // cache current (store in class fields for potential restoration if interrupted)
        float a0 = flickerAmplitude;
        float s0 = flickerSpeed;
        float spot0  = spotBaseIntensity;
        float point0 = pointBaseIntensity;
        
        // Update cached values in case we need to restore them
        cachedFlickerAmplitude = a0;
        cachedFlickerSpeed = s0;

        // ease in/out so it feels gusty, not a hard snap
        float t = 0f;
        float easeIn  = Mathf.Min(0.15f, duration * 0.25f);
        float easeOut = Mathf.Min(0.20f, duration * 0.30f);

        while (t < duration && isLit)
        {
            float u = t / Mathf.Max(0.0001f, duration);

            // bell-shaped envelope (0→1→0) using smoothstep segments
            float inK  = (easeIn  > 0f) ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / easeIn)) : 1f;
            float outK = (easeOut > 0f) ? Mathf.SmoothStep(1, 0, Mathf.Clamp01((t - (duration - easeOut)) / Mathf.Max(0.0001f, easeOut))) : 1f;
            float env  = Mathf.Min(inK, outK);

            flickerAmplitude   = Mathf.Lerp(a0, a0 * ampMul,   env);
            flickerSpeed       = Mathf.Lerp(s0, s0 * speedMul, env);
            spotBaseIntensity  = Mathf.Lerp(spot0,  spot0  * baseMul, env);
            pointBaseIntensity = Mathf.Lerp(point0, point0 * baseMul, env);

            t += Time.deltaTime;
            yield return null;
        }

        // restore
        flickerAmplitude   = a0;
        flickerSpeed       = s0;
        spotBaseIntensity  = spot0;
        pointBaseIntensity = point0;
        _dbgRoutine = null;
    }

    // ------- Context menu helpers (appear on the component's ⋯ menu) -------
    [ContextMenu("Debug/Violent Flicker (test)")]
    void _dbg_ViolentFlicker() => ViolentFlicker(dbgFlickerDur, dbgAmpMul, dbgSpeedMul, dbgBaseMul);

    [ContextMenu("Debug/Extinguish (test)")]
    void _dbg_ExtinguishTest() => Extinguish(0f, dbgExtinguishFade);

    [ContextMenu("Debug/Ignite (test)")]
    void _dbg_IgniteTest() => Ignite(dbgRelightRise);

    [ContextMenu("Debug/Full Test: Flicker → Extinguish → Ignite")]
    void _dbg_FullTest()
    {
        if (_dbgRoutine != null) StopCoroutine(_dbgRoutine);
        _dbgRoutine = StartCoroutine(_dbg_FullTestCo());
    }
    IEnumerator _dbg_FullTestCo()
    {
        // If currently dark, bring it up first
        if (!isLit) Ignite(dbgRelightRise);
        yield return new WaitForSeconds(0.15f);

        ViolentFlicker(dbgFlickerDur, dbgAmpMul, dbgSpeedMul, dbgBaseMul);
        yield return new WaitForSeconds(dbgFlickerDur * 0.7f);

        Extinguish(0f, dbgExtinguishFade);
        yield return new WaitForSeconds(dbgExtinguishFade + 0.3f);

        Ignite(dbgRelightRise);
        _dbgRoutine = null;
    }

    #endregion
    // ===================================================

}

