// Assets/Scripts/FX/SpawnAppear.cs
using System.Collections;
using UnityEngine;

namespace EarFPS
{
    public class SpawnAppear : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;          // default: use this transform

        [Header("Timing")]
        [SerializeField] float duration = 1.0f;     // seconds
        [SerializeField] bool useUnscaledTime = false;

        [Header("Easing")]
        // Default is a nice easeOutBack-style curve (starts fast, tiny overshoot, settles)
        [SerializeField] AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.5f),
            new Keyframe(0.85f, 1.05f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

        Vector3 baseScale;
        Coroutine co;

        void Awake()
        {
            if (!target) target = transform;
            baseScale = target.localScale;   // remember prefab/original scale
        }

        void OnEnable()
        {
            // restart tween each time this object is (re)spawned
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(AppearCo());
        }

        IEnumerator AppearCo()
        {
            if (duration <= 0f)
            {
                target.localScale = baseScale;
                co = null;
                yield break;
            }

            float t = 0f;
            // start at zero scale
            target.localScale = Vector3.zero;

            while (t < duration)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float e = curve != null ? curve.Evaluate(u) : u; // ease
                target.localScale = baseScale * e;
                yield return null;
            }

            target.localScale = baseScale;
            co = null;
        }
    }
}
