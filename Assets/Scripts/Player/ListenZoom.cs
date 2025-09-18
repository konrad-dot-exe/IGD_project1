// Assets/Scripts/Player/ListenZoom.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarFPS
{
    public class ListenZoom : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera cam;
        [SerializeField] float normalFOV = 60f;
        [SerializeField] float zoomFOV   = 42f;
        [SerializeField] float zoomInTime  = 0.12f;
        [SerializeField] float zoomOutTime = 0.10f;

        [Header("Post FX (URP Global Volume)")]
        [SerializeField] Volume globalVolume;           // drag your Global Volume here
        [SerializeField] bool driveVignette = true;
        [SerializeField, Range(0f,1f)] float normalVignette = 0.25f;
        [SerializeField, Range(0f,1f)] float zoomVignette   = 0.45f;

        [Header("Overlay")]
        [SerializeField] CanvasGroup overlayGroup;      // e.g., a dim/scope UI image
        [SerializeField, Range(0f,1f)] float overlayAlpha = 0.35f;
        [SerializeField] RectTransform overlayRect;    // usually same GO as overlayGroup
        [SerializeField] float overlayScaleNormal = 1.0f;
        [SerializeField] float overlayScaleZoom   = 1.15f; // grows slightly when listening

        Vignette vignette;
        Coroutine co;

        void Awake()
        {
            if (!cam) cam = GetComponent<Camera>();
            if (globalVolume && globalVolume.profile)
                globalVolume.profile.TryGet(out vignette);
            if (!overlayRect && overlayGroup)
                overlayRect = overlayGroup.GetComponent<RectTransform>();
        }

        public void SetListening(bool listening)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Tween(listening));
        }

        IEnumerator Tween(bool listening)
        {
            float dur = listening ? zoomInTime : zoomOutTime;
            float t = 0f;

            float f0 = cam.fieldOfView;
            float f1 = listening ? zoomFOV : normalFOV;

            float v0 = (vignette != null) ? vignette.intensity.value : 0f;
            float v1 = listening ? zoomVignette : normalVignette;

            float a0 = overlayGroup ? overlayGroup.alpha : 0f;
            float a1 = listening ? overlayAlpha : 0f;

            Vector3 s0 = overlayRect ? overlayRect.localScale : Vector3.one;
            Vector3 s1 = Vector3.one * (listening ? overlayScaleZoom : overlayScaleNormal);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);

                cam.fieldOfView = Mathf.Lerp(f0, f1, u);
                if (vignette && driveVignette) vignette.intensity.value = Mathf.Lerp(v0, v1, u);
                if (overlayGroup) overlayGroup.alpha = Mathf.Lerp(a0, a1, u);
                if (overlayRect) overlayRect.localScale = Vector3.Lerp(s0, s1, u);

                yield return null;
            }

            cam.fieldOfView = f1;
            if (vignette && driveVignette) vignette.intensity.value = v1;
            if (overlayGroup) overlayGroup.alpha = a1;
            if (overlayRect) overlayRect.localScale = s1;
            co = null;
        }
    }
}
