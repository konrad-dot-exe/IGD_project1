// Assets/Scripts/VFX/ShieldBubble.cs
using UnityEngine;

namespace EarFPS
{
    /// <summary>
    /// Expanding/fading bubble. Call Init(color, radius) after instantiate.
    /// Designed for URP/Unlit (Transparent/Additive). Works with _BaseColor or _Color.
    /// </summary>
    public class ShieldBubble : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [SerializeField] float popInTime = 0.06f;   // fast ramp-in
        [SerializeField] float holdTime  = 0.05f;   // tiny peak hold
        [SerializeField] float fadeTime  = 0.35f;   // slower fade out

        [Header("Scale")]
        [SerializeField] float overshoot = 1.12f;   // slight overshoot at peak

        [Header("Opacity")]
        [SerializeField] float peakAlpha = 0.9f;    // how “solid” the bubble looks at peak (additive: 0.4–0.9)

        [Header("Renderers")]
        [SerializeField] Renderer[] renderers;      // auto-filled if empty

        MaterialPropertyBlock _mpb;
        Color _baseColor = Color.cyan;              // without alpha
        float _targetRadius = 2f;                   // world units (bubble radius at peak)

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Color (tint, no HDR needed) and target radius (world units).</summary>
        public void Init(Color tint, float radius)
        {
            _baseColor = new Color(tint.r, tint.g, tint.b, 1f);
            _targetRadius = Mathf.Max(0.01f, radius);
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        System.Collections.IEnumerator Run()
        {
            // start tiny
            transform.localScale = Vector3.zero;

            // POP IN (0 -> peak)
            float t = 0f;
            while (t < popInTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / popInTime);
                float a = Mathf.Lerp(0f, peakAlpha, u * u); // ease-in for opacity
                float s = Mathf.Lerp(0f, _targetRadius * 2f * overshoot, u); // diameter
                Apply(a, s);
                yield return null;
            }
            Apply(peakAlpha, _targetRadius * 2f * overshoot);

            // HOLD
            if (holdTime > 0f) yield return new WaitForSeconds(holdTime);

            // FADE OUT while settling size
            t = 0f;
            float startScale = transform.localScale.x;
            float endScale   = _targetRadius * 2f; // settle to exact radius
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeTime);
                float a = Mathf.Lerp(peakAlpha, 0f, u); // linear fade
                float s = Mathf.Lerp(startScale, endScale, u);
                Apply(a, s);
                yield return null;
            }

            Destroy(gameObject);
        }

        void Apply(float alpha, float diameter)
        {
            transform.localScale = Vector3.one * diameter;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                r.GetPropertyBlock(_mpb);

                // Support both URP _BaseColor and legacy _Color
                if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                    _mpb.SetColor("_BaseColor", new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha));
                else
                    _mpb.SetColor("_Color",     new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha));

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
