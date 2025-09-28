// Assets/Scripts/FX/ExplosionLightPulse.cs
using UnityEngine;

namespace EarFPS
{
    /// Add to a point Light on the ExplosionVFX prefab.
    /// Call Play(color, durationScale) after you tint/spawn the VFX.
    public class ExplosionLightPulse : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] Light pointLight;   // assign your child Light
        [SerializeField] ParticleSystem ps;  // optional; if set we’ll sync to lifetime

        [Header("Timing")]
        [SerializeField] float baseDuration = 0.50f; // seconds if no ParticleSystem
        [SerializeField] float durationScale = 1f;   // extra multiplier

        [Header("Curves")]
        // “fat onset” (very quick ramp), then longer tail off.
        // Keyframes: t0=0,v0=0  t1=0.03,v=1  t2=0.25,v=0.55  t3=1,v=0
        [SerializeField] AnimationCurve intensityCurve = new AnimationCurve(
            new Keyframe(0f,   0f,     0f,  80f),
            new Keyframe(0.03f,1f,     0f,   0f),
            new Keyframe(0.25f,0.55f,  0f,   0f),
            new Keyframe(1f,   0f,     0f,   0f)
        );

        // Range grows a hair on the pop then falls off.
        [SerializeField] AnimationCurve rangeCurve = new AnimationCurve(
            new Keyframe(0f,   0.85f),
            new Keyframe(0.05f,1.05f),
            new Keyframe(1f,   0.0f)
        );

        [Header("Magnitudes")]
        [SerializeField] float maxIntensity = 3500f; // URP uses physical-ish values; tweak
        [SerializeField] float baseRange    = 10f;   // multiplied by rangeCurve

        float t, dur;
        Color tint = Color.white;
        bool playing;

        void Reset()
        {
            if (!pointLight) pointLight = GetComponent<Light>();
            if (!ps) ps = GetComponentInParent<ParticleSystem>();
        }

        void Awake()
        {
            if (!pointLight) pointLight = GetComponent<Light>();
            if (pointLight) pointLight.intensity = 0f;
        }

        public void Play(Color color, float durationMultiplier = 1f)
        {
            tint = color;
            if (pointLight) pointLight.color = tint;

            dur = baseDuration;
            if (ps)
            {
                var m = ps.main;
                dur = Mathf.Max(0.05f, m.startLifetime.constantMax);
            }
            dur *= durationScale * Mathf.Max(0.01f, durationMultiplier);

            t = 0f;
            playing = true;
            enabled = true;
        }

        void Update()
        {
            if (!playing || !pointLight) return;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            pointLight.intensity = maxIntensity * Mathf.Max(0f, intensityCurve.Evaluate(u));
            pointLight.range     = baseRange    * Mathf.Max(0f, rangeCurve.Evaluate(u));

            if (u >= 1f)
            {
                pointLight.intensity = 0f;
                playing = false;
                // Let the VFX destroy itself; we’re just a child.
                enabled = false;
            }
        }
    }
}
