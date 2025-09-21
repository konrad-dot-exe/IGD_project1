using System.Collections;
using UnityEngine;

namespace EarFPS
{
    public class CameraShake : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] Transform target; // set to CamShakeTarget

        [Header("Feel")]
        [SerializeField] float frequency = 28f;
        [SerializeField] float posAmount = 0.25f;
        [SerializeField] float rotAmount = 8f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = true;

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        Coroutine co;
        Vector3 posOff;
        Quaternion rotOff = Quaternion.identity;

        void Awake()
        {
            if (!target) target = transform; // fallback (but prefer a child!)
            baseLocalPos = target.localPosition;
            baseLocalRot = target.localRotation;
            //if (debugLogs) Debug.Log($"[Shake] Awake target={(target? target.name : \"null\")}");
        }

        public void Shake(float duration = 0.25f, float amplitude = 0.25f)
        {
            if (!isActiveAndEnabled) return;
            if (debugLogs) Debug.Log($"[Shake] start dur={duration:F2} amp={amplitude:F2} tScale={Time.timeScale}");
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(ShakeCo(duration, amplitude));
        }

        IEnumerator ShakeCo(float dur, float amp)
        {
            float noiseT = Random.value * 100f;
            float t = 0f;
            int frame = 0;
            if (debugLogs) Debug.Log("[Shake] coroutine running");

            while (t < dur)
            {
                float dt = Time.unscaledDeltaTime;
                t      += dt;
                noiseT += dt * frequency;

                float damper = 1f - Mathf.Clamp01(t / dur);
                float a = amp * damper;

                float nx = Mathf.PerlinNoise(noiseT, 0.0f) * 2f - 1f;
                float ny = Mathf.PerlinNoise(0.0f, noiseT) * 2f - 1f;
                float nz = Mathf.PerlinNoise(noiseT * 0.7f, noiseT * 1.3f) * 2f - 1f;

                posOff = new Vector3(nx, ny, 0f) * (posAmount * a * 0.25f);
                rotOff = Quaternion.Euler(ny * rotAmount * a, nx * rotAmount * a, nz * (rotAmount * 0.5f) * a);

                if (debugLogs && (frame++ % 10 == 0))
                    Debug.Log($"[Shake] t={t:F2}/{dur:F2} a={a:F2} posOff={posOff}");

                yield return null; // apply in LateUpdate
            }

            posOff = Vector3.zero;
            rotOff = Quaternion.identity;
            co = null;
            if (debugLogs) Debug.Log("[Shake] end");
        }

        void LateUpdate()
        {
            if (!target) return;

            // DO NOT overwrite the parent’s motion—only offset the child
            target.localPosition = baseLocalPos + posOff;
            target.localRotation = baseLocalRot * rotOff;
        }
    }
}
