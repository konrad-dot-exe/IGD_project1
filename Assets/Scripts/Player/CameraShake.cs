// Assets/Scripts/Player/CameraShake.cs
using System.Collections;
using UnityEngine;

namespace EarFPS
{
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] float frequency = 35f;   // noise speed
        Vector3 basePos;
        Quaternion baseRot;
        Coroutine co;

        void Awake() { basePos = transform.localPosition; baseRot = transform.localRotation; }

        public void Shake(float duration = 0.25f, float amplitude = 0.25f)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(ShakeCo(duration, amplitude));
        }

        IEnumerator ShakeCo(float dur, float amp)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float damper = 1f - (t / dur);            // ease out
                float a = amp * damper;

                // lightweight noise
                float nx = (Mathf.PerlinNoise(0f, Time.unscaledTime * frequency) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(1f, Time.unscaledTime * frequency) - 0.5f) * 2f;

                transform.localPosition = basePos + new Vector3(nx, ny, 0f) * a * 0.25f;
                transform.localRotation = baseRot * Quaternion.Euler(ny * a * 8f, nx * a * 8f, 0f);
                yield return null;
            }
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
            co = null;
        }
    }
}
