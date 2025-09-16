using UnityEngine;
using TMPro;

namespace EarFPS
{
    public class WorldToast : MonoBehaviour
    {
        [SerializeField] TextMeshPro text;
        [SerializeField] float lifetime = 1.2f;
        [SerializeField] float risePerSec = 1.2f;
        [SerializeField] float startScale = 0.75f;
        [SerializeField] float endScale = 1.0f;

        Camera cam;
        float t0;
        Color baseColor = Color.white;

        void Awake()
        {
            if (!text) text = GetComponent<TextMeshPro>();
            if (text) baseColor = text.color;
            t0 = Time.time;
        }

        public void SetText(string s)
        {
            if (text) text.text = s;
        }

        void LateUpdate()
        {
            // Billboard
            if (cam == null) cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            // Rise + scale + fade
            float u = Mathf.InverseLerp(0f, lifetime, Time.time - t0);
            transform.position += Vector3.up * risePerSec * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, u);

            if (text)
            {
                var c = baseColor; c.a = 1f - u;
                text.color = c;
            }

            if (Time.time - t0 >= lifetime) Destroy(gameObject);
        }
    }
}

